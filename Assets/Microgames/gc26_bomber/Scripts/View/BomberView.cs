using System.Collections.Generic;
using UnityEngine;

namespace GC26.Bomber
{
    /// <summary>
    /// Draws BomberWorld with pooled SpriteRenderers. The camera renders into a 272x480 RenderTexture
    /// (1 texel = 1 art pixel = 2 world px) with point filtering, so the result is crisp pixel art;
    /// BomberPhoneUI shows that texture inside the phone layout.
    /// </summary>
    public sealed class BomberView : MonoBehaviour
    {
        public const int TexW = 272, TexH = 480;
        const float PxPerUnit = BomberData.Cell, H = BomberData.H;

        public RenderTexture Target { get; private set; }
        public Camera Cam { get; private set; }

        BomberContext _ctx;
        Transform _roomRoot, _dyn;
        int _builtRoom = -1;
        Sprite _floor, _shaft;
        float _viewH = BomberData.H, _shake;
        Vector3 _camBase;
        readonly Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();

        sealed class Pool
        {
            readonly List<SpriteRenderer> _items = new List<SpriteRenderer>(); int _used; readonly Transform _parent; readonly int _order;
            public Pool(Transform parent, int order) { _parent = parent; _order = order; }
            public SpriteRenderer Get()
            {
                if (_used == _items.Count)
                {
                    var go = new GameObject("px"); go.transform.SetParent(_parent, false);
                    var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = _order; _items.Add(sr);
                }
                var r = _items[_used++]; if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
                r.color = Color.white; r.transform.localScale = Vector3.one; r.transform.localRotation = Quaternion.identity; r.flipX = false;
                return r;
            }
            public void Begin() => _used = 0;
            public void End() { for (int i = _used; i < _items.Count; i++) if (_items[i].gameObject.activeSelf) _items[i].gameObject.SetActive(false); }
        }

        public void Init(BomberContext ctx)
        {
            _ctx = ctx;
            var camGo = ctx.Spawn("gc26_bomber_Camera", transform);
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true; Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Color.black;
            SetViewHeight(BomberData.H);
            _roomRoot = ctx.Spawn("Room", transform).transform;
            _dyn = ctx.Spawn("Dynamic", transform).transform;
            foreach (var (name, order) in new[] { ("light", -40), ("pickup", 0), ("barrel", 1), ("fxUnder", 2), ("enemy", 10), ("enemyFx", 11), ("player", 20), ("glow", 28), ("shot", 30), ("fx", 35), ("part", 40) })
                _pools[name] = new Pool(_dyn, order);
        }

        void OnDestroy()
        {
            if (Cam) Cam.targetTexture = null;
            if (Target) { Target.Release(); Destroy(Target); }
        }

        /// <summary>
        /// Height of the visible world in world px (>= the room). Taller phones see more of the station below the
        /// room entrance instead of black bars. Recreates the render texture and returns it for the UI.
        /// </summary>
        public RenderTexture SetViewHeight(float h)
        {
            _viewH = Mathf.Max(BomberData.H, Mathf.Round(h / 2f) * 2f);
            if (Target) { Cam.targetTexture = null; Target.Release(); Destroy(Target); }
            Target = new RenderTexture(TexW, Mathf.RoundToInt(_viewH / 2f), 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "gc26_bomber_World" };
            Cam.targetTexture = Target; Cam.orthographicSize = _viewH / PxPerUnit / 2f;
            _camBase = new Vector3(BomberData.W / PxPerUnit / 2f, (2f * BomberData.H - _viewH) / PxPerUnit / 2f, -10f);
            Cam.transform.position = _camBase;
            _builtRoom = -1; // rebuild the floor so the shaft below the room fills the new space
            return Target;
        }

        public void AddShake(float amount) => _shake = Mathf.Max(_shake, amount);

        static Vector3 U(float x, float y) => new Vector3(x / PxPerUnit, (H - y) / PxPerUnit, 0f);
        static Quaternion Rot(float a) { const float step = Mathf.PI / 8f; return Quaternion.Euler(0f, 0f, -Mathf.Round(a / step) * step * Mathf.Rad2Deg); }

        public void Sync()
        {
            var w = _ctx.World; var art = _ctx.Art;
            if (_builtRoom != w.RoomVersion) BuildRoom(w);
            Cam.backgroundColor = (Color)BomberData.Sectors[w.Sector].Tint;
            foreach (var p in _pools.Values) p.Begin();
            float time = w.Time;
            // screen shake
            _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime * 2.5f);
            Cam.transform.position = _camBase + (_shake > 0f ? (Vector3)(Random.insideUnitCircle * _shake * 6f / PxPerUnit) : Vector3.zero);
            // decals (scorch / oil) become children of the room, so they stay until the next room
            foreach (var (dx, dy, dr, dc) in w.NewDecals) AddDecal(dx, dy, dr, dc);
            w.NewDecals.Clear();

            // doors: glow when open, red bar when closed
            foreach (var d in w.Doors)
            {
                var sr = _pools["fxUnder"].Get(); sr.sprite = art.Pixel;
                var col = d.Open ? (Color)BomberData.RoomColor[d.Type] : new Color(1f, 0.35f, 0.43f);
                col.a = d.Open ? 0.5f + 0.3f * Mathf.Sin(time * 4f) : 1f; sr.color = col;
                sr.transform.position = U((d.X0 + d.X1) / 2f, BomberData.Cell - 2f); sr.transform.localScale = new Vector3((d.X1 - d.X0) / 2f, 2f, 1f);
            }
            foreach (var p in w.Pickups)
            {
                var pg = _pools["light"].Get(); pg.sprite = art.Glow; pg.color = p.Kind == "scrap" ? new Color(1f, 0.81f, 0.36f, 0.3f) : p.Kind == "repair" ? new Color(0.43f, 1f, 0.69f, 0.4f) : new Color(0.62f, 0.82f, 1f, 0.4f);
                pg.transform.position = U(p.X, p.Y); pg.transform.localScale = Vector3.one * ((p.Kind == "scrap" ? 10f : 16f) / 32f);
                var sr = _pools["pickup"].Get(); sr.sprite = p.Kind == "scrap" ? art.Scrap : p.Kind == "repair" ? art.Repair : art.EmpPickup;
                sr.transform.position = U(p.X, p.Y); if (p.Kind == "scrap") sr.transform.localRotation = Rot(time * 3f + p.X);
            }
            foreach (var b in w.Barrels) { var sr = _pools["barrel"].Get(); sr.sprite = art.Barrel; sr.transform.position = U(b.X, b.Y); }
            foreach (var e in w.Effects) DrawEffect(e, art);
            foreach (var e in w.Enemies) DrawEnemy(e, art, time);
            DrawPlayer(w, art, time);
            foreach (var s in w.Shots)
            {
                var g = _pools["glow"].Get(); g.sprite = art.Glow; var gc = s.Enemy ? new Color(1f, 0.3f, 0.4f, 0.55f) : new Color(0.5f, 0.89f, 1f, 0.35f); if (!s.Enemy && s.Heavy) gc.a = 0.6f; g.color = gc;
                g.transform.position = U(s.X, s.Y); g.transform.localScale = Vector3.one * (s.R * (s.Enemy ? 3.4f : 3f) / 32f);
                var sr = _pools["shot"].Get();
                if (s.Enemy) { sr.sprite = art.EnemyShot; sr.color = (Color)s.Color; sr.transform.localScale = Vector3.one * (s.R / 5f) * 1.3f; }
                else { sr.sprite = s.Heavy ? art.ShotHeavy : art.Shot; sr.color = (Color)s.Color; sr.transform.localRotation = Rot(Mathf.Atan2(s.Vy, s.Vx)); }
                sr.transform.position = U(s.X, s.Y);
            }
            foreach (var p in w.Particles)
            {
                var sr = _pools["part"].Get(); sr.sprite = art.Pixel; var c = (Color)p.Color; c.a *= Mathf.Clamp01(p.Life / p.Max); sr.color = c;
                sr.transform.position = U(p.X, p.Y); sr.transform.localScale = Vector3.one * Mathf.Max(1f, p.Size / 2f);
            }
            foreach (var p in _pools.Values) p.End();
        }

        void DrawEffect(BomberEffect e, BomberArt art)
        {
            float k = e.K;
            switch (e.Kind)
            {
                case "spawn":
                {
                    var sr = _pools["fxUnder"].Get(); sr.sprite = art.Ring; var c = e.Elite ? new Color(1f, 0.84f, 0.36f) : new Color(1f, 0.35f, 0.43f); c.a = 0.4f + 0.6f * k; sr.color = c;
                    sr.transform.position = U(e.X, e.Y); sr.transform.localScale = Vector3.one * ((22f * (1f - k) + 6f) / 32f);
                    var beam = _pools["fxUnder"].Get(); beam.sprite = art.Pixel; c.a = 0.12f + 0.25f * k; beam.color = c;
                    beam.transform.position = U(e.X, e.Y / 2f); beam.transform.localScale = new Vector3((6f + 8f * k) / 2f, e.Y / 2f, 1f);
                    break;
                }
                case "ring":
                {
                    var sr = _pools["fx"].Get(); sr.sprite = art.Ring; var c = (Color)e.Color; c.a = 1f - k; sr.color = c;
                    sr.transform.position = U(e.X, e.Y); sr.transform.localScale = Vector3.one * (e.R * (0.3f + 0.7f * k) / 32f);
                    break;
                }
                case "blast":
                {
                    var gl = _pools["glow"].Get(); gl.sprite = art.Glow; gl.color = new Color(1f, 0.75f, 0.4f, (1f - k) * 0.8f);
                    gl.transform.position = U(e.X, e.Y); gl.transform.localScale = Vector3.one * (e.R * 2.2f / 32f);
                    var sr = _pools["fx"].Get(); sr.sprite = art.Disc; sr.color = new Color(1f, 0.67f, 0.27f, (1f - k) * 0.55f);
                    sr.transform.position = U(e.X, e.Y); sr.transform.localScale = Vector3.one * (e.R * (0.5f + 0.5f * k) / 32f);
                    var core = _pools["fx"].Get(); core.sprite = art.Disc; core.color = new Color(1f, 1f, 0.85f, Mathf.Max(0f, 0.9f - k * 2.5f));
                    core.transform.position = U(e.X, e.Y); core.transform.localScale = Vector3.one * (e.R * 0.45f / 32f);
                    break;
                }
            }
        }

        void DrawEnemy(BomberEnemy e, BomberArt art, float time)
        {
            var w = _ctx.World;
            float a = Mathf.Atan2(w.P.Y - e.Y, w.P.X - e.X);
            bool frame = (e.Kind == "beetle" || e.Kind == "mini") && Mathf.Sin(time * 22f + e.X * 0.1f) > 0f;
            float rot = e.Kind == "turret" || e.Kind == "kamikaze" || e.Kind == "splitter" ? 0f : a;
            var shadow = _pools["fxUnder"].Get(); shadow.sprite = art.Pixel; shadow.color = new Color(0, 0, 0, 0.35f);
            shadow.transform.position = U(e.X, e.Y + e.R * 0.8f); shadow.transform.localScale = new Vector3(e.R, 2f, 1f);
            var sr = _pools["enemy"].Get(); sr.sprite = art.Enemy(e.Kind, e.R, frame); sr.transform.position = U(e.X, e.Y); sr.transform.localRotation = Rot(rot);
            sr.color = e.Flash > 0f ? new Color(3f, 3f, 3f) : e.Stun > 0f ? new Color(0.7f, 0.85f, 1f) : Color.white;
            if (e.Flash > 0f) sr.color = new Color(1f, 0.55f, 0.55f);
            if (e.Kind == "turret") { var h = _pools["enemyFx"].Get(); h.sprite = art.TurretHead; h.transform.position = U(e.X, e.Y); h.transform.localRotation = Rot(e.St == "fire" ? e.AimA : e.Face); }
            if (e.Elite) Ring(e.X, e.Y, e.R + 6f, new Color(1f, 0.84f, 0.36f, 0.5f + 0.3f * Mathf.Sin(time * 6f)), "enemyFx");
            if (e.St == "tele") { Ring(e.X, e.Y, e.R + 3f, new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.Sin(time * 40f)), "enemyFx"); var tg = _pools["glow"].Get(); tg.sprite = art.Glow; tg.color = new Color(1f, 0.4f, 0.45f, 0.45f); tg.transform.position = U(e.X, e.Y); tg.transform.localScale = Vector3.one * (e.R * 3f / 32f); }
            if (e.St == "tele" && e.Kind == "kamikaze") { float kk = 1f - e.Tm / 0.55f; Ring(e.X, e.Y, 80f, new Color(1f, 0.3f, 0.3f, 0.9f), "fxUnder"); var f = _pools["fxUnder"].Get(); f.sprite = art.Disc; f.color = new Color(1f, 0.3f, 0.3f, 0.15f); f.transform.position = U(e.X, e.Y); f.transform.localScale = Vector3.one * (80f * Mathf.Max(0f, kk) / 32f); }
            if (e.Kind == "kamikaze") { var core = _pools["enemyFx"].Get(); core.sprite = art.Pixel; core.color = Mathf.Sin(time * (e.St == "tele" ? 40f : 10f)) > 0f ? Color.white : new Color(1f, 0.16f, 0.16f); core.transform.position = U(e.X, e.Y); core.transform.localScale = Vector3.one * 2f; }
            if (e.Hp < e.MaxHp)
            {
                var bg = _pools["enemyFx"].Get(); bg.sprite = art.Pixel; bg.color = new Color(0, 0, 0, 0.65f); bg.transform.position = U(e.X, e.Y + e.R + 6f); bg.transform.localScale = new Vector3(12f, 1.5f, 1f);
                var fg = _pools["enemyFx"].Get(); fg.sprite = art.Pixel; fg.color = e.Elite ? new Color(1f, 0.84f, 0.36f) : new Color(1f, 0.48f, 0.54f);
                float k = Mathf.Clamp01(e.Hp / e.MaxHp); fg.transform.position = U(e.X - 12f + 12f * k, e.Y + e.R + 6f); fg.transform.localScale = new Vector3(12f * k, 1.5f, 1f);
            }
        }

        void DrawPlayer(BomberWorld w, BomberArt art, float time)
        {
            var p = w.P; var core = w.Core;
            bool over = core.State == CoreState.Overheated;
            float alpha = p.ITime > 0f && p.DashT <= 0f && Mathf.FloorToInt(time * 20f) % 2 == 1 ? 0.45f : 1f;
            float bob = Mathf.Round(Mathf.Sin(time * 5f) * 0.8f) * 2f;
            var light = _pools["light"].Get(); light.sprite = art.Glow; var lc = over ? new Color(1f, 0.3f, 0.35f) : HeatColor(core); lc.a = over ? 0.12f : 0.22f; light.color = lc;
            light.transform.position = U(p.X, p.Y); light.transform.localScale = Vector3.one * ((core.State == CoreState.Overdrive ? 95f : 80f) / 32f);
            if (p.MuzzleT > 0f)
            {
                var mf = _pools["glow"].Get(); mf.sprite = art.Glow; mf.color = new Color(1f, 0.95f, 0.78f, 0.85f);
                mf.transform.position = U(p.X + Mathf.Cos(p.Aim) * 20f, p.Y + bob + Mathf.Sin(p.Aim) * 20f); mf.transform.localScale = Vector3.one * (16f / 32f);
            }
            var shadow = _pools["fxUnder"].Get(); shadow.sprite = art.Pixel; shadow.color = new Color(0, 0, 0, 0.4f * alpha); shadow.transform.position = U(p.X, p.Y + 15f); shadow.transform.localScale = new Vector3(12f, 2f, 1f);
            var gun = _pools["player"].Get(); gun.sprite = art.Gun; gun.transform.position = U(p.X, p.Y + bob); gun.transform.localRotation = Rot(p.Aim); gun.color = new Color(1, 1, 1, alpha);
            var hull = _pools["player"].Get(); hull.sprite = art.Drone; hull.transform.position = U(p.X, p.Y + bob);
            hull.color = over ? new Color(0.55f, 0.6f, 0.7f, alpha) : core.State == CoreState.Overdrive ? new Color(1f, 0.93f, 0.78f, alpha) : new Color(1, 1, 1, alpha);
            hull.sortingOrder = 21;
            float hb = 5.5f * 2f, spin = over ? 0f : time * 38f; int i = 0;
            foreach (var (hx, hy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                var disc = _pools["player"].Get(); disc.sprite = art.Disc; disc.color = new Color(0.81f, 0.88f, 1f, (over ? 0.12f : 0.22f) * alpha);
                disc.transform.position = U(p.X + hx * hb, p.Y + bob + hy * hb); disc.transform.localScale = Vector3.one * (9f / 32f); disc.sortingOrder = 22;
                var r = _pools["player"].Get(); r.sprite = art.Rotor; r.color = new Color(1, 1, 1, (over ? 0.5f : 0.85f) * alpha);
                r.transform.position = disc.transform.position; r.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Round((spin + i * 1.3f) / (Mathf.PI / 4f)) * 45f); r.sortingOrder = 23; i++;
            }
            var lens = _pools["player"].Get(); lens.sprite = art.Lens; lens.sortingOrder = 24; lens.transform.position = U(p.X + Mathf.Cos(p.Aim) * 2.4f, p.Y + bob + Mathf.Sin(p.Aim) * 2.4f);
            lens.color = over ? new Color(0.42f, 0.48f, 0.6f) : HeatColor(core);
            // heat ring around the drone with the Overdrive mark
            if (core.Heat > 1f || over)
            {
                var ring = _pools["fx"].Get(); ring.sprite = art.Ring; var c = over ? new Color(1f, 0.13f, 0.27f) : core.State == CoreState.Overdrive ? new Color(1f, 0.7f, 0.28f) : new Color(0.31f, 0.72f, 1f);
                c.a = 0.25f + 0.6f * core.Normalized; ring.color = c; ring.transform.position = U(p.X, p.Y); ring.transform.localScale = Vector3.one * (26f / 32f);
            }
            if (p.AbT > 0f) Ring(p.X, p.Y, p.R + 5f + Mathf.Sin(time * 20f) * 2f, new Color(0.5f, 0.89f, 1f, 0.6f), "fx");
            if (w.Governor.Forsage) Ring(p.X, p.Y, 34f, new Color(1f, 0.7f, 0.28f, 0.3f + 0.25f * Mathf.Sin(time * 10f)), "fx");
        }

        public static Color HeatColor(HeatModel core)
        {
            float k = Mathf.Clamp01((core.Normalized - 0.3f) / 0.5f);
            return new Color((127 + 128 * k) / 255f, (227 - 48 * k) / 255f, (255 - 184 * k) / 255f);
        }

        void Ring(float x, float y, float r, Color c, string pool)
        {
            var sr = _pools[pool].Get(); sr.sprite = _ctx.Art.Ring; sr.color = c; sr.transform.position = U(x, y); sr.transform.localScale = Vector3.one * (r / 32f);
        }

        void AddDecal(float x, float y, float r, Color32 c)
        {
            var scorch = _ctx.Spawn("Decal", _roomRoot).AddComponent<SpriteRenderer>(); scorch.sprite = _ctx.Art.Glow; scorch.sortingOrder = -90;
            scorch.color = new Color(0f, 0f, 0f, 0.5f); scorch.transform.position = U(x, y); scorch.transform.localScale = Vector3.one * (r * 2f / 32f);
            for (int i = 0; i < 5; i++)
            {
                var d = _ctx.Spawn("Oil", _roomRoot).AddComponent<SpriteRenderer>(); d.sprite = _ctx.Art.Pixel; d.sortingOrder = -89;
                var col = (Color)c; col.a = c.r + c.g + c.b < 30 ? 0.6f : 0.35f; d.color = col;
                float a = Random.value * Mathf.PI * 2f, dist = Random.value * r; d.transform.position = U(x + Mathf.Cos(a) * dist, y + Mathf.Sin(a) * dist);
                d.transform.localScale = Vector3.one * (1f + Random.value);
            }
        }

        void BuildRoom(BomberWorld w)
        {
            _builtRoom = w.RoomVersion;
            for (int i = _roomRoot.childCount - 1; i >= 0; i--) Destroy(_roomRoot.GetChild(i).gameObject);
            var sec = BomberData.Sectors[w.Sector];
            var floor = _ctx.Spawn("Floor", _roomRoot).AddComponent<SpriteRenderer>();
            _ctx.Art.Free(_floor);
            floor.sprite = _floor = _ctx.Art.MakeFloor(BomberData.GW, BomberData.GH, sec, BomberRng.Hash(w.Seed, (uint)(w.RoomVersion * 31 + w.Sector)));
            floor.transform.position = new Vector3(BomberData.W / PxPerUnit / 2f, BomberData.H / PxPerUnit / 2f, 0f);
            floor.sortingOrder = -100;
            // maintenance shaft under the entrance: fills the extra height of tall phones
            float extra = _viewH - BomberData.H;
            _ctx.Art.Free(_shaft); _shaft = null;
            if (extra > 1f)
            {
                int rows = Mathf.CeilToInt(extra / BomberData.Cell) + 1;
                var dark = new BomberSectorDef { Floor = new Color32((byte)(sec.Floor.r * 0.6f), (byte)(sec.Floor.g * 0.6f), (byte)(sec.Floor.b * 0.65f), 255), Accent = sec.Accent, Tint = sec.Tint };
                var shaft = _ctx.Spawn("Shaft", _roomRoot).AddComponent<SpriteRenderer>();
                shaft.sprite = _shaft = _ctx.Art.MakeFloor(BomberData.GW, rows, dark, BomberRng.Hash(w.Seed, 777u));
                shaft.transform.position = new Vector3(BomberData.W / PxPerUnit / 2f, -rows / 2f, 0f); shaft.sortingOrder = -100;
                foreach (var side in new[] { 0, BomberData.GW - 1 })
                {
                    var wsr = _ctx.Spawn("ShaftWall", _roomRoot).AddComponent<SpriteRenderer>(); wsr.sprite = _ctx.Art.Wall; wsr.drawMode = SpriteDrawMode.Tiled;
                    wsr.size = new Vector2(1f, rows); wsr.sortingOrder = -50; wsr.transform.position = new Vector3(side + 0.5f, -rows / 2f, 0f);
                }
                var veil = _ctx.Spawn("ShaftShade", _roomRoot).AddComponent<SpriteRenderer>(); veil.sprite = _ctx.Art.Pixel; veil.color = new Color(0f, 0f, 0f, 0.45f); veil.sortingOrder = -45;
                veil.transform.position = new Vector3(BomberData.W / PxPerUnit / 2f, -rows / 2f, 0f); veil.transform.localScale = new Vector3(BomberData.W / 2f, rows * 16f, 1f);
            }
            foreach (var wall in w.Walls)
            {
                if (wall.DoorOut) continue;
                var go = _ctx.Spawn(wall.Cover ? "Cover" : "Wall", _roomRoot);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = _ctx.Art.Wall; sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(wall.W / PxPerUnit, wall.H / PxPerUnit); sr.sortingOrder = -50;
                if (wall.DoorIn) sr.color = new Color(0.55f, 0.35f, 0.4f);
                go.transform.position = U(wall.X + wall.W / 2f, wall.Y + wall.H / 2f);
                if (wall.Cover)
                {
                    var edge = _ctx.Spawn("Edge", go.transform).AddComponent<SpriteRenderer>(); edge.sprite = _ctx.Art.Pixel; edge.sortingOrder = -49;
                    var ac = (Color)sec.Accent; ac.a = 0.9f; edge.color = ac;
                    edge.transform.position = U(wall.X + wall.W / 2f, wall.Y + 1f); edge.transform.localScale = new Vector3(wall.W / 2f, 1f, 1f);
                }
            }
        }
    }
}
