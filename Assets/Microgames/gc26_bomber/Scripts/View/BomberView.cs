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
        BomberLight _light;
        /// <summary>«Тёмная станция» lighting (on by default, saved).</summary>
        public bool Dark { get; private set; } = true;
        public void SetDark(bool on) { Dark = on; PlayerPrefs.SetInt(GC26.Core.MicrogameSave.Key(BomberGame.Id, "dark"), on ? 1 : 0); PlayerPrefs.Save(); }

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
            Dark = PlayerPrefs.GetInt(GC26.Core.MicrogameSave.Key(BomberGame.Id, "dark"), 1) == 1;
            _light = new BomberLight(transform, 26);
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
            _light?.Release();
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
            _light?.Resize(_viewH);
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
            Cam.backgroundColor = (Color)w.Theme.Tint;
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
            var near = w.NearProp();
            foreach (var pr in w.Props)
            {
                var sr = _pools["barrel"].Get();
                sr.sprite = pr.Kind == "chest" ? (pr.Opened ? art.ChestOpen : art.Chest) : pr.Kind == "altar" ? (pr.Opened ? art.AltarUsed : art.Altar) : pr.Kind == "bench" ? art.Bench
                    : pr.Kind == "station" ? art.Terminal : pr.Kind == "cell" ? art.Cell : art.Stand;
                sr.color = pr.Sold ? new Color(0.5f, 0.5f, 0.6f, 0.7f) : Color.white; sr.transform.position = U(pr.X, pr.Y);
                if (pr.Kind == "station")
                {
                    var st = BomberMetaData.Station(pr.Id); var sc = (Color)st.Color;
                    var scr = _pools["pickup"].Get(); scr.sprite = art.Pixel; scr.sortingOrder = 2; sc.a = 0.55f + 0.25f * Mathf.Sin(time * 3f + pr.X); scr.color = sc;
                    scr.transform.position = U(pr.X, pr.Y - 4f); scr.transform.localScale = new Vector3(6.5f, 4.5f, 1f);
                    var hg = _pools["light"].Get(); hg.sprite = art.Glow; sc.a = 0.3f; hg.color = sc; hg.transform.position = U(pr.X, pr.Y); hg.transform.localScale = Vector3.one * (90f / 32f);
                }
                if (pr.Kind == "cell" && !pr.Opened)
                {
                    var nd = BomberMetaData.Npc(pr.Id); var who = _pools["pickup"].Get(); who.sprite = art.Npc; who.color = (Color)nd.Color; who.sortingOrder = 2; who.transform.position = U(pr.X, pr.Y + Mathf.Round(Mathf.Sin(time * 3f)) * 2f);
                    var hg = _pools["light"].Get(); hg.sprite = art.Glow; var nc = (Color)nd.Color; nc.a = 0.3f; hg.color = nc; hg.transform.position = U(pr.X, pr.Y); hg.transform.localScale = Vector3.one * (80f / 32f);
                }
                if (pr == near) { var g = _pools["light"].Get(); g.sprite = art.Glow; g.color = new Color(0.43f, 1f, 0.69f, 0.35f + 0.15f * Mathf.Sin(time * 6f)); g.transform.position = U(pr.X, pr.Y); g.transform.localScale = Vector3.one * (44f / 32f); }
                if (pr.Kind == "shop" && !pr.Sold) { var ic = _pools["pickup"].Get(); ic.sprite = pr.Item.Type == "weapon" ? art.Gun : pr.Item.Type == "companion" ? art.CompGunner : pr.Item.Type == "module" ? art.EmpPickup : art.Repair; ic.transform.position = U(pr.X, pr.Y - 4f); ic.transform.localScale = Vector3.one * 1.6f; ic.sortingOrder = 2; }
            }
            foreach (var n in w.Npcs)
            {
                var nd = BomberMetaData.Npc(n.Id); var sr = _pools["player"].Get(); sr.sprite = art.Npc; sr.color = (Color)nd.Color; sr.sortingOrder = 19;
                sr.transform.position = U(n.X, n.Y + Mathf.Round(Mathf.Sin(time * 4f + n.X)) * 2f);
                var sh = _pools["fxUnder"].Get(); sh.sprite = art.Pixel; sh.color = new Color(0, 0, 0, 0.35f); sh.transform.position = U(n.X, n.Y + 8f); sh.transform.localScale = new Vector3(5f, 1.5f, 1f);
            }
            foreach (var t in w.Turrets)
            {
                var sr = _pools["barrel"].Get(); sr.sprite = art.ForgeTurret; sr.transform.position = U(t.X, t.Y);
                Line(t.X, t.Y, t.X + Mathf.Cos(t.A) * 15f, t.Y + Mathf.Sin(t.A) * 15f, new Color(1f, 0.81f, 0.54f), 5f, "enemyFx");
                var bar = _pools["enemyFx"].Get(); bar.sprite = art.Pixel; bar.color = new Color(1f, 0.62f, 0.26f); bar.transform.position = U(t.X - 9f + 9f * t.T / 9f, t.Y + 13f); bar.transform.localScale = new Vector3(9f * t.T / 9f, 1f, 1f);
            }
            foreach (var t in w.Throws) { var sr = _pools["shot"].Get(); sr.sprite = art.Dynamite; sr.transform.position = U(t.X, t.Y); sr.transform.localRotation = Rot(time * 12f); }
            foreach (var t in w.Trails) { var sr = _pools["fxUnder"].Get(); sr.sprite = art.Glow; sr.color = new Color(0.5f, 0.89f, 1f, t.T * 0.4f); sr.transform.position = U(t.X, t.Y); sr.transform.localScale = Vector3.one * (12f / 32f); }
            foreach (var z in w.Zones) DrawZone(z, art, time);
            foreach (var e in w.Effects) DrawEffect(e, art);
            foreach (var e in w.Enemies) { if (e.IsBoss) DrawBoss(e, art, time); else DrawEnemy(e, art, time); }
            DrawPlayer(w, art, time);
            if (w.P.Beam.On) { var bm = w.P.Beam; Line(bm.Sx, bm.Sy, bm.Ex, bm.Ey, bm.Zap ? new Color(0.85f, 0.94f, 1f, 0.5f) : new Color(1f, 0.6f, 0.87f, 0.5f), 5f, "glow"); Line(bm.Sx, bm.Sy, bm.Ex, bm.Ey, Color.white, 1.5f, "shot"); }
            foreach (var bo in w.Bolts) for (int i = 1; i < bo.Pts.Count; i++) { var a0 = bo.Pts[i - 1]; var a1 = bo.Pts[i]; for (int k = 0; k < 4; k++) { float t0 = k / 4f, t1 = (k + 1) / 4f; var j0 = k == 0 ? Vector2.zero : Random.insideUnitCircle * 7f; var j1 = k == 3 ? Vector2.zero : Random.insideUnitCircle * 7f; var q0 = Vector2.Lerp(a0, a1, t0) + j0; var q1 = Vector2.Lerp(a0, a1, t1) + j1; Line(q0.x, q0.y, q1.x, q1.y, new Color(0.85f, 0.94f, 1f), 2f, "fx"); } }
            foreach (var c in w.P.Comps)
            {
                var sr = _pools["player"].Get(); sr.sortingOrder = 25; sr.sprite = c.Id == "gunner" ? art.CompGunner : c.Id == "guard" ? art.CompGuard : c.Id == "medic" ? art.CompMedic : art.CompStriker;
                sr.transform.position = U(c.X, c.Y); if (c.Id == "striker") sr.transform.localRotation = Rot(c.St == "dive" && c.Tgt != null ? Mathf.Atan2(c.Tgt.Y - c.Y, c.Tgt.X - c.X) : time * 4f);
                var g = _pools["light"].Get(); g.sprite = art.Glow; g.color = new Color(0.62f, 0.82f, 1f, 0.25f); g.transform.position = U(c.X, c.Y); g.transform.localScale = Vector3.one * (16f / 32f);
            }
            if (w.P.Shield > 0f) Ring(w.P.X, w.P.Y, w.P.R + 7f, new Color(0.62f, 0.82f, 1f, 0.7f), "fx");
            foreach (var s in w.Shots)
            {
                var g = _pools["glow"].Get(); g.sprite = art.Glow; var gc = s.Enemy ? new Color(1f, 0.3f, 0.4f, 0.55f) : new Color(0.5f, 0.89f, 1f, 0.35f); if (!s.Enemy && s.Heavy) gc.a = 0.6f; g.color = gc;
                g.transform.position = U(s.X, s.Y); g.transform.localScale = Vector3.one * (s.R * (s.Enemy ? 3.4f : 3f) / 32f);
                var sr = _pools["shot"].Get();
                if (s.Enemy) { sr.sprite = art.EnemyShot; sr.color = (Color)s.Color; sr.transform.localScale = Vector3.one * (s.R / 5f) * 1.3f; }
                else if (s.Look == "rocket" || s.Homing) { sr.sprite = art.Rocket; sr.transform.localRotation = Rot(Mathf.Atan2(s.Vy, s.Vx)); }
                else if (s.Look == "disc") { sr.sprite = art.Saw; sr.transform.localRotation = Rot(time * 30f); sr.transform.localScale = Vector3.one * Mathf.Max(1f, s.R / 7f); }
                else if (s.Look == "flame") { sr.sprite = art.Flame; var fc = (Color)s.Color; fc.a = Mathf.Clamp01(s.Life / 0.4f); sr.color = fc; sr.transform.localScale = Vector3.one * (s.R / 6f) * (1.6f - Mathf.Min(1f, s.Life)); }
                else { sr.sprite = s.Heavy || s.R >= 6f ? art.ShotHeavy : art.Shot; sr.color = (Color)s.Color; sr.transform.localRotation = Rot(Mathf.Atan2(s.Vy, s.Vx)); if (s.R >= 6f) sr.transform.localScale = Vector3.one * (s.R / 7f); }
                sr.transform.position = U(s.X, s.Y);
            }
            foreach (var p in w.Particles)
            {
                var sr = _pools["part"].Get(); sr.sprite = art.Pixel; var c = (Color)p.Color; c.a *= Mathf.Clamp01(p.Life / p.Max); sr.color = c;
                sr.transform.position = U(p.X, p.Y); sr.transform.localScale = Vector3.one * Mathf.Max(1f, p.Size / 2f);
            }
            foreach (var p in _pools.Values) p.End();
            _light.Enabled = Dark;
            if (Dark) { CollectLights(w, time); _light.Apply(); }
        }

        // ================= «Тёмная станция» =================
        void CollectLights(BomberWorld w, float time)
        {
            bool hub = w.Mode == BomberMode.Hub; var p = w.P; var core = w.Core;
            _light.Begin(hub ? 0.5f : 0.72f, hub ? new Color32(10, 7, 4, 255) : new Color32(2, 4, 10, 255));
            if (hub)
            {
                _light.Add(BomberData.W / 2f, BomberData.H / 2f, 460f, 0.5f);
                foreach (var pr in w.Props) _light.Add(pr.X, pr.Y, 130f, 0.85f);
                foreach (var n in w.Npcs) _light.Add(n.X, n.Y, 60f, 0.5f);
                _light.Add(p.X, p.Y, 190f, 1f);
            }
            else
            {
                _light.Add(p.X, p.Y, core.State == CoreState.Overheated ? 85f : core.State == CoreState.Overdrive ? 290f + Mathf.Sin(time * 30f) * 14f : 250f, core.State == CoreState.Overheated ? 0.6f : 1f);
                if (p.MuzzleT > 0f) _light.Add(p.X + Mathf.Cos(p.Aim) * 22f, p.Y + Mathf.Sin(p.Aim) * 22f, 160f, 0.85f);
                if (p.Beam.On) { _light.Add(p.Beam.Ex, p.Beam.Ey, 90f, 0.8f); _light.Add((p.Beam.Sx + p.Beam.Ex) / 2f, (p.Beam.Sy + p.Beam.Ey) / 2f, 70f, 0.5f); }
                foreach (var bo in w.Bolts) if (bo.Pts.Count > 0) { var q = bo.Pts[bo.Pts.Count - 1]; _light.Add(q.x, q.y, 90f, 0.8f); }
                foreach (var s in w.Shots) _light.Add(s.X, s.Y, s.Enemy ? 36f : 46f, 0.8f);
                foreach (var e in w.Effects)
                {
                    float k = e.K;
                    if (e.Kind == "blast") _light.Add(e.X, e.Y, e.R * 2.4f, 1f - k * 0.4f);
                    else if (e.Kind == "mine" || e.Kind == "shell") _light.Add(e.X, e.Y, (e.R > 0f ? e.R : 52f) * 1.4f, 0.65f);
                    else if (e.Kind == "spawn") _light.Add(e.X, e.Y, 70f, 0.6f);
                    else if (e.Kind == "ring") _light.Add(e.X, e.Y, e.R * 1.3f, 0.5f * (1f - k));
                }
                foreach (var z in w.Zones) _light.Add(z.X + z.W / 2f, z.Y + z.H / 2f, Mathf.Max(z.W, z.H) * (z.Kind == "hot" ? 1f : 1.1f), z.Kind == "hot" ? 0.6f : z.Tele > 0f ? 0.55f : 0.9f);
                foreach (var en in w.Enemies) _light.Add(en.X, en.Y, en.IsBoss ? en.R * 4.5f : en.R * 2.8f, en.IsBoss ? 0.85f : en.St == "tele" ? 0.6f : 0.35f);
                foreach (var pk in w.Pickups) _light.Add(pk.X, pk.Y, 26f, 0.6f);
                foreach (var t in w.Turrets) _light.Add(t.X, t.Y, 70f, 0.6f);
                foreach (var c in p.Comps) _light.Add(c.X, c.Y, 50f, 0.5f);
                foreach (var t in w.Throws) _light.Add(t.X, t.Y, 70f, 0.7f);
                foreach (var b in w.Barrels) _light.Add(b.X, b.Y, 44f, 0.45f);
                foreach (var pr in w.Props) if (!pr.Opened && !pr.Sold) _light.Add(pr.X, pr.Y, 95f, 0.7f);
                // room beacons: slow pulsing lamps in the corners
                for (int i = 0; i < _beacons.Count; i++) { float k = Mathf.Max(0f, Mathf.Sin(time * 3f + i * 2f)); _light.Add(_beacons[i].x, _beacons[i].y, 170f, 0.35f + 0.35f * k); }
            }
            foreach (var d in w.Doors) _light.Add((d.X0 + d.X1) / 2f, BomberData.Cell, d.Open ? 120f : 70f, d.Open ? 0.85f : 0.5f);
            if (w.Mode != BomberMode.Hub) _light.Add(BomberData.W / 2f, BomberData.H - 20f, 140f, 0.5f); // entrance
        }

        readonly List<Vector2> _beacons = new List<Vector2>();

        void DrawZone(BomberZone z, BomberArt art, float time)
        {
            float cx = z.X + z.W / 2f, cy = z.Y + z.H / 2f;
            Color fill, edge;
            if (z.Kind == "hot") { fill = new Color(1f, 0.47f, 0.24f, 0.1f + 0.04f * Mathf.Sin(time * 3f)); edge = new Color(1f, 0.55f, 0.24f, 0.5f); }
            else
            {
                bool acid = z.Kind == "acid";
                if (z.Tele > 0f) { fill = new Color(0, 0, 0, 0f); edge = acid ? new Color(0.63f, 1f, 0.35f, 0.4f + 0.4f * Mathf.Sin(time * 20f)) : new Color(1f, 0.35f, 0.24f, 0.4f + 0.4f * Mathf.Sin(time * 20f)); }
                else { fill = acid ? new Color(0.47f, (200f + Mathf.Sin(time * 6f) * 30f) / 255f, 0.24f, 0.42f) : new Color(1f, (80f + Mathf.Sin(time * 6f) * 30f) / 255f, 0.16f, 0.45f); edge = fill; edge.a = 0.8f; }
                var lg = _pools["light"].Get(); lg.sprite = art.Glow; var lc = acid ? new Color(0.61f, 1f, 0.43f, 0.18f) : new Color(1f, 0.35f, 0.16f, 0.2f); lg.color = lc;
                lg.transform.position = U(cx, cy); lg.transform.localScale = Vector3.one * (Mathf.Max(z.W, z.H) * 1.6f / 32f);
            }
            if (fill.a > 0f) { var f = _pools["fxUnder"].Get(); f.sprite = art.Pixel; f.color = fill; f.transform.position = U(cx, cy); f.transform.localScale = new Vector3(z.W / 2f, z.H / 2f, 1f); }
            Line(z.X, z.Y + 1f, z.X + z.W, z.Y + 1f, edge, 2f, "fxUnder"); Line(z.X, z.Y + z.H - 1f, z.X + z.W, z.Y + z.H - 1f, edge, 2f, "fxUnder");
            Line(z.X + 1f, z.Y, z.X + 1f, z.Y + z.H, edge, 2f, "fxUnder"); Line(z.X + z.W - 1f, z.Y, z.X + z.W - 1f, z.Y + z.H, edge, 2f, "fxUnder");
        }

        void DrawBoss(BomberEnemy b, BomberArt art, float time)
        {
            var d = b.Boss; var col = (Color)d.Color;
            if (b.Act == "chargeTele") { Line(b.X, b.Y, b.X + Mathf.Cos(b.AimA) * 1100f, b.Y + Mathf.Sin(b.AimA) * 1100f, new Color(1f, 0.35f, 0.43f, 0.2f), b.R * 1.6f, "fxUnder"); }
            if (b.Act == "snipe") foreach (var o in new[] { -0.12f, 0f, 0.12f }) Line(b.X, b.Y, b.X + Mathf.Cos(b.AimA + o) * 1200f, b.Y + Mathf.Sin(b.AimA + o) * 1200f, b.Tm < 0.3f ? new Color(1f, 0.13f, 0.27f) : new Color(1f, 0.24f, 0.35f, 0.5f), b.Tm < 0.3f ? 2.5f : 1f, "fx");
            if (b.Act == "sweepTele" || b.Act == "sweep")
                for (int k = 0; k < b.Beams; k++)
                {
                    float aa = b.AimA + k * Mathf.PI * 2f / b.Beams; var end = _ctx.World.RayEndPublic(b.X, b.Y, aa, 1100f);
                    if (b.Act == "sweepTele") Line(b.X, b.Y, end.x, end.y, new Color(0.43f, 0.78f, 1f, 0.3f + 0.3f * Mathf.Sin(time * 25f)), 2f, "fx");
                    else { Line(b.X, b.Y, end.x, end.y, new Color(0.43f, 0.78f, 1f, 0.5f), 7f, "glow"); Line(b.X, b.Y, end.x, end.y, Color.white, 2f, "fx"); }
                }
            if (b.Act == "throwTele")
            {
                float k = 1f - b.Tm / 0.85f;
                foreach (var t in b.Targets)
                {
                    Ring(t.x, t.y, 46f, new Color(1f, 0.35f, 0.43f, 0.9f), "fxUnder");
                    var f = _pools["fxUnder"].Get(); f.sprite = art.Disc; f.color = new Color(1f, 0.35f, 0.43f, 0.18f); f.transform.position = U(t.x, t.y); f.transform.localScale = Vector3.one * (46f * Mathf.Max(0f, k) / 32f);
                }
            }
            var glow = _pools["light"].Get(); glow.sprite = art.Glow; var gc = col; gc.a = 0.3f * b.Alpha; glow.color = gc; glow.transform.position = U(b.X, b.Y); glow.transform.localScale = Vector3.one * (b.R * 6f / 32f);
            var shadow = _pools["fxUnder"].Get(); shadow.sprite = art.Disc; shadow.color = new Color(0, 0, 0, 0.35f * b.Alpha); shadow.transform.position = U(b.X, b.Y + b.R * 0.5f); shadow.transform.localScale = new Vector3(b.R / 32f, b.R * 0.4f / 32f, 1f);
            var sr = _pools["enemy"].Get(); sr.sprite = art.Boss(d); sr.transform.position = U(b.X, b.Y);
            float spin = time * (d.Key == "oracle" ? 0.6f : d.Key == "warden" ? 0.3f : 0.15f); sr.transform.localRotation = Quaternion.Euler(0, 0, -spin * Mathf.Rad2Deg);
            sr.color = b.Flash > 0f ? new Color(1f, 0.6f, 0.6f, b.Alpha) : b.Stun > 0f ? new Color(0.7f, 0.85f, 1f, b.Alpha) : new Color(1f, 1f, 1f, b.Alpha);
            if ((b.Act == "ring" || b.Act == "aimed") && b.Tm > 0.2f) { var core = _pools["enemyFx"].Get(); core.sprite = art.Pixel; core.transform.position = U(b.X, b.Y); core.transform.localScale = Vector3.one * 6f; }
            if (b.Burn > 0f) { var bg2 = _pools["glow"].Get(); bg2.sprite = art.Glow; bg2.color = new Color(1f, 0.55f, 0.15f, 0.35f); bg2.transform.position = U(b.X, b.Y); bg2.transform.localScale = Vector3.one * (b.R * 2.4f / 32f); }
        }

        void DrawEffect(BomberEffect e, BomberArt art)
        {
            float k = e.K;
            switch (e.Kind)
            {
                case "mine": case "shell":
                {
                    var col = e.Kind == "shell" ? new Color(1f, 0.66f, 0.3f) : new Color(0.43f, 0.78f, 1f); float r = e.R > 0f ? e.R : 52f;
                    Ring(e.X, e.Y, r, col, "fxUnder");
                    var f = _pools["fxUnder"].Get(); f.sprite = art.Disc; var fc = col; fc.a = 0.12f + 0.25f * k; f.color = fc; f.transform.position = U(e.X, e.Y); f.transform.localScale = Vector3.one * (r * k / 32f);
                    var lg = _pools["light"].Get(); lg.sprite = art.Glow; var lc = col; lc.a = 0.15f; lg.color = lc; lg.transform.position = U(e.X, e.Y); lg.transform.localScale = Vector3.one * (r * 2.8f / 32f);
                    if (e.Kind == "mine") { var m = _pools["fx"].Get(); m.sprite = art.Pixel; m.color = Mathf.Sin(_ctx.World.Time * (10f + 20f * k)) > 0f ? Color.white : col; m.transform.position = U(e.X, e.Y); m.transform.localScale = Vector3.one * 3f; }
                    break;
                }
                case "beam":
                    Line(e.X, e.Y, e.X2, e.Y2, new Color(0.49f, 1f, 0.66f, 1f - k), 3f, "fx");
                    break;
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
            float rot = e.Kind == "brute" ? (e.St == "charge" || e.St == "tele" ? e.AimA : a) : e.Kind == "laser" ? time : e.Kind == "sniper" ? (e.St == "aim" ? e.AimA : a)
                : e.Kind == "turret" || e.Kind == "kamikaze" || e.Kind == "splitter" || e.Kind == "mortar" ? 0f : a;
            if (e.Kind == "laser" && (e.St == "tele" || e.St == "sweep"))
            {
                var end = w.RayEndPublic(e.X, e.Y, e.AimA, 900f);
                if (e.St == "tele") Line(e.X, e.Y, end.x, end.y, new Color(1f, 0.24f, 0.95f, 0.3f + 0.3f * Mathf.Sin(time * 25f)), 2f, "fx");
                else { Line(e.X, e.Y, end.x, end.y, new Color(1f, 0.24f, 0.95f, 0.5f), 6f, "glow"); Line(e.X, e.Y, end.x, end.y, Color.white, 2f, "fx"); }
            }
            if (e.Kind == "sniper" && e.St == "aim") Line(e.X, e.Y, e.X + Mathf.Cos(e.AimA) * 1200f, e.Y + Mathf.Sin(e.AimA) * 1200f, e.Tm < 0.3f ? new Color(1f, 0.13f, 0.27f) : new Color(1f, 0.24f, 0.35f, 0.55f), e.Tm < 0.3f ? 2.5f : 1f, "fx");
            if (e.Kind == "brute" && e.St == "tele") Line(e.X, e.Y, e.X + Mathf.Cos(e.AimA) * 260f, e.Y + Mathf.Sin(e.AimA) * 260f, new Color(1f, 0.42f, 0.24f, 0.35f), e.R * 1.6f, "fxUnder");
            var shadow = _pools["fxUnder"].Get(); shadow.sprite = art.Pixel; shadow.color = new Color(0, 0, 0, 0.35f);
            shadow.transform.position = U(e.X, e.Y + e.R * 0.8f); shadow.transform.localScale = new Vector3(e.R, 2f, 1f);
            var sr = _pools["enemy"].Get(); sr.sprite = art.Enemy(e.Kind, e.R, frame); sr.transform.position = U(e.X, e.Y); sr.transform.localRotation = Rot(rot);
            sr.color = e.Flash > 0f ? new Color(1f, 0.55f, 0.55f, e.Alpha) : e.Stun > 0f ? new Color(0.7f, 0.85f, 1f, e.Alpha) : new Color(1f, 1f, 1f, e.Alpha);
            shadow.color = new Color(0, 0, 0, 0.35f * e.Alpha);
            if (e.Kind == "shield") for (int i = 0; i < 7; i++) { float a0 = e.Face - 1.05f + i * 0.35f, a1 = a0 + 0.35f, rr = e.R + 5f; Line(e.X + Mathf.Cos(a0) * rr, e.Y + Mathf.Sin(a0) * rr, e.X + Mathf.Cos(a1) * rr, e.Y + Mathf.Sin(a1) * rr, new Color(0.91f, 0.84f, 1f), 3f, "enemyFx"); }
            if (e.ShieldHp > 0f) Ring(e.X, e.Y, e.R + 3f, new Color(0.62f, 0.82f, 1f, 0.9f), "enemyFx");
            if (e.Kind == "phantom" && e.St == "slash") Ring(e.X, e.Y, e.R + 28f, new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.Sin(time * 40f)), "enemyFx");
            if (Dark && e.Alpha > 0.3f)
            {
                bool tele = e.St == "tele" || e.St == "aim"; float ea = tele && e.Kind != "kamikaze" ? e.AimA : a;
                if (e.Kind == "turret" || e.Kind == "laser" || e.Kind == "mortar") ea = a;
                var eye = _pools["glow"].Get(); eye.sprite = art.Glow; eye.color = tele ? new Color(1f, 1f, 1f, 0.7f * e.Alpha) : e.Elite ? new Color(1f, 0.84f, 0.36f, 0.5f * e.Alpha) : new Color(1f, 0.19f, 0.28f, 0.5f * e.Alpha);
                eye.transform.position = U(e.X + Mathf.Cos(ea) * e.R * 0.5f, e.Y + Mathf.Sin(ea) * e.R * 0.5f); eye.transform.localScale = Vector3.one * ((tele ? 18f : 12f) / 32f);
            }
            if (e.Kind == "turret") { var h = _pools["enemyFx"].Get(); h.sprite = art.TurretHead; h.transform.position = U(e.X, e.Y); h.transform.localRotation = Rot(e.St == "fire" ? e.AimA : e.Face); }
            if (e.Burn > 0f) { var bg2 = _pools["glow"].Get(); bg2.sprite = art.Glow; bg2.color = new Color(1f, 0.55f, 0.15f, 0.45f); bg2.transform.position = U(e.X, e.Y); bg2.transform.localScale = Vector3.one * (e.R * 2.4f / 32f); }
            if (e.Slow > 0f) Ring(e.X, e.Y, e.R + 3f, new Color(0.62f, 0.9f, 1f, 0.8f), "enemyFx");
            if (e.Elite) { var ac = e.Affix != null ? (Color)BomberData.Affixes[e.Affix].Color : new Color(1f, 0.84f, 0.36f); ac.a = (0.5f + 0.3f * Mathf.Sin(time * 6f)) * e.Alpha; Ring(e.X, e.Y, e.R + 6f, ac, "enemyFx"); }
            if (e.St == "tele" && e.Kind != "brute") { Ring(e.X, e.Y, e.R + 3f, new Color(1f, 1f, 1f, 0.5f + 0.5f * Mathf.Sin(time * 40f)), "enemyFx"); var tg = _pools["glow"].Get(); tg.sprite = art.Glow; tg.color = new Color(1f, 0.4f, 0.45f, 0.45f); tg.transform.position = U(e.X, e.Y); tg.transform.localScale = Vector3.one * (e.R * 3f / 32f); }
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
            var hull = _pools["player"].Get(); hull.sprite = art.DroneOf(w.CharDef); hull.transform.position = U(p.X, p.Y + bob);
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

        void Line(float x0, float y0, float x1, float y1, Color c, float width, string pool)
        {
            var sr = _pools[pool].Get(); sr.sprite = _ctx.Art.Pixel; sr.color = c;
            float dx = x1 - x0, dy = y1 - y0, len = Mathf.Sqrt(dx * dx + dy * dy);
            sr.transform.position = U((x0 + x1) / 2f, (y0 + y1) / 2f); sr.transform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            sr.transform.localScale = new Vector3(len / 2f, width / 2f, 1f);
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
            var sec = w.Theme;
            _beacons.Clear();
            if (w.Mode != BomberMode.Hub)
            {
                _beacons.Add(new Vector2(BomberData.Cell * 1.5f, BomberData.Cell * 1.5f)); _beacons.Add(new Vector2(BomberData.W - BomberData.Cell * 1.5f, BomberData.H - BomberData.Cell * 1.5f));
                if ((w.RoomVersion & 1) == 0) _beacons.Add(new Vector2(BomberData.W - BomberData.Cell * 1.5f, BomberData.Cell * 1.5f));
                foreach (var bc in _beacons)
                {
                    var lamp = _ctx.Spawn("Beacon", _roomRoot).AddComponent<SpriteRenderer>(); lamp.sprite = _ctx.Art.Disc; lamp.sortingOrder = 27;
                    var lc = (Color)sec.Accent; lc = Color.Lerp(lc, Color.white, 0.3f); lamp.color = lc; lamp.transform.position = U(bc.x, bc.y); lamp.transform.localScale = Vector3.one * (8f / 32f);
                }
            }
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
                    var edge = _ctx.Spawn("Edge", go.transform).AddComponent<SpriteRenderer>(); edge.sprite = _ctx.Art.Pixel; edge.sortingOrder = 27; // neon edge stays readable in the dark
                    var ac = (Color)sec.Accent; ac.a = 0.9f; edge.color = ac;
                    edge.transform.position = U(wall.X + wall.W / 2f, wall.Y + 1f); edge.transform.localScale = new Vector3(wall.W / 2f, 1f, 1f);
                }
            }
        }
    }
}
