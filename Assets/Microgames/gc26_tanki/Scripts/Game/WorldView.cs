using System.Collections.Generic;
using GC26.Tanki.Rules;
using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>Draws the simulation with simple boxes. All models are built in code.</summary>
    public sealed partial class WorldView
    {
        static readonly Color Sand = Hex("D9BC8C");
        static readonly Color SandDark = Hex("C7A676");
        static readonly Color Rim = Hex("6E4F3A");
        static readonly Color WaterColor = Hex("2F8FA3");
        static readonly Color OilColor = Hex("2A2433");
        static readonly Color ReactorMetal = Hex("3C4A57");
        static readonly Color ReactorGlow = Hex("6FF2E0");
        static readonly Color Metal = Hex("2B2E33");
        static readonly Color PlayerMark = Hex("35F2D0");
        static readonly Color HealthGreen = Hex("45E84A");
        static readonly Color HealthYellow = Hex("FFD21F");
        static readonly Color HealthRed = Hex("FF3B30");
        static readonly Color HealthEmpty = Hex("4A4552");
        static readonly Color HealthBack = Hex("15101C");

        static readonly Color BossTint = new Color(1f, 0.55f, 0.5f);
        static readonly Color BeamColor = Hex("8FFFF0");

        // The boss has far more health than fits in segments, so its bar shows tenths.
        const int BossBarSegments = 10;

        readonly Transform root;
        readonly Transform tileRoot;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly Dictionary<string, Material> spriteMaterials = new Dictionary<string, Material>();
        readonly GameObject[,] tiles = new GameObject[Levels.Width, Levels.Height];
        readonly Dictionary<Tank, TankView> tankViews = new Dictionary<Tank, TankView>();
        readonly List<Tank> staleTanks = new List<Tank>();
        readonly List<GameObject> bulletViews = new List<GameObject>();
        readonly List<Debris> debris = new List<Debris>();
        readonly Material baseMaterial;

        Material groundMaterial;
        Material tileMaterial;
        Material bonusMaterial;
        Transform bonusIcon;
        Transform bonusModel;
        GameObject bonusView;
        BonusKind bonusViewKind;
        Transform reactorCore;
        float clock;

        sealed class TankView
        {
            public Transform Root;
            public Transform Model;
            public Renderer[] Paint;
            public Material Normal;
            public Material Flash;
            public Transform Shield;
            public Transform MarkerRing;
            public Transform HealthBar;
            public Renderer[] HealthSegments;
            public int SegmentCount;
        }

        sealed class Beam
        {
            public Transform Item;
            public float Life;
            public float Width;
        }

        readonly List<Beam> beams = new List<Beam>();

        sealed class Debris
        {
            public Transform Item;
            public Vector3 Velocity;
            public float Life;
        }

        public WorldView(Transform parent)
        {
            baseMaterial = Resources.Load<Material>("gc26_tanki/SteelLineLit");
            if (baseMaterial == null) baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            root = new GameObject("World").transform;
            root.SetParent(parent, false);
            tileRoot = new GameObject("Tiles").transform;
            tileRoot.SetParent(root, false);
        }

        public static Vector3 ToWorld(float x, float y, float height = 0f)
        {
            return new Vector3(x, height, Levels.Height - y);
        }

        // ---- Level ----

        public void BuildLevel(GameSim sim)
        {
            Clear();
            BuildGround();
            for (int y = 0; y < Levels.Height; y++)
                for (int x = 0; x < Levels.Width; x++)
                    RefreshTile(sim, x, y);
        }

        void Clear()
        {
            for (int i = tileRoot.childCount - 1; i >= 0; i--) Kill(tileRoot.GetChild(i).gameObject);
            foreach (TankView view in tankViews.Values) Kill(view.Root.gameObject);
            tankViews.Clear();
            foreach (GameObject bullet in bulletViews) Kill(bullet);
            bulletViews.Clear();
            foreach (Debris item in debris) Kill(item.Item.gameObject);
            debris.Clear();
            foreach (Beam beam in beams) Kill(beam.Item.gameObject);
            beams.Clear();
            if (bonusView != null) Kill(bonusView);
            bonusView = null;
            reactorCore = null;
            System.Array.Clear(tiles, 0, tiles.Length);
        }

        void BuildGround()
        {
            float w = Levels.Width;
            float h = Levels.Height;
            // One textured quad instead of a box per cell keeps draw calls low on phones.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Kill(ground.GetComponent<Collider>());
            ground.transform.SetParent(tileRoot, false);
            ground.transform.position = new Vector3(w / 2f, 0f, h / 2f);
            ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(w, h, 1f);
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            const float t = 0.35f;
            Box(tileRoot, new Vector3(w / 2f, 0.15f, -t / 2f), new Vector3(w + t * 2f, 0.7f, t), Rim);
            Box(tileRoot, new Vector3(w / 2f, 0.15f, h + t / 2f), new Vector3(w + t * 2f, 0.7f, t), Rim);
            Box(tileRoot, new Vector3(-t / 2f, 0.15f, h / 2f), new Vector3(t, 0.7f, h), Rim);
            Box(tileRoot, new Vector3(w + t / 2f, 0.15f, h / 2f), new Vector3(t, 0.7f, h), Rim);
        }

        Material GroundMaterial()
        {
            if (groundMaterial != null) return groundMaterial;

            var texture = new Texture2D(Levels.Width, Levels.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < Levels.Height; y++)
                for (int x = 0; x < Levels.Width; x++)
                    texture.SetPixel(x, y, ((x + y) & 1) == 0 ? SandDark : Sand);
            texture.Apply();

            groundMaterial = new Material(baseMaterial) { mainTexture = texture };
            groundMaterial.SetFloat("_Smoothness", 0.05f);
            return groundMaterial;
        }

        public void RefreshTile(GameSim sim, int x, int y)
        {
            if (tiles[x, y] != null) Kill(tiles[x, y]);
            tiles[x, y] = null;

            Tile tile = sim.Tiles[x, y];
            if (tile == Tile.Empty) return;

            var cell = new GameObject("Tile " + x + "," + y).transform;
            cell.SetParent(tileRoot, false);
            cell.position = ToWorld(x + 0.5f, y + 0.5f);
            tiles[x, y] = cell.gameObject;

            switch (tile)
            {
                case Tile.Crate: TileModel(cell, sim.CrateHp(x, y) >= Tuning.CrateHp ? "crate" : "crate_broken"); break;
                case Tile.Concrete: TileModel(cell, "concrete"); break;
                case Tile.Net:
                    // Mixing shapes and turning them keeps a row of bushes from looking copied.
                    TileModel(cell, "bush" + (x * 7 + y * 13) % 3).localRotation = Quaternion.Euler(0f, (x + y * 3) % 4 * 90f, 0f);
                    break;
                case Tile.Water: BuildWater(cell); break;
                case Tile.Oil: BuildOil(cell, x, y); break;
                case Tile.Base: BuildReactor(cell); break;
            }
        }

        // Crates, concrete and bushes come from Art/build_tiles.py.
        Transform TileModel(Transform cell, string name)
        {
            if (tileMaterial == null)
            {
                tileMaterial = new Material(baseMaterial) { mainTexture = Resources.Load<Texture2D>("gc26_tanki/Tiles/atlas") };
                tileMaterial.SetFloat("_Smoothness", 0.05f);
            }

            var model = new GameObject(name);
            model.transform.SetParent(cell, false);
            model.AddComponent<MeshFilter>().sharedMesh =
                Resources.Load<GameObject>("gc26_tanki/Tiles/" + name).GetComponentInChildren<MeshFilter>().sharedMesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = tileMaterial;
            return model.transform;
        }

        void BuildWater(Transform cell)
        {
            Box(cell, new Vector3(0f, 0.02f, 0f), new Vector3(1f, 0.04f, 1f), WaterColor);
        }

        void BuildOil(Transform cell, int x, int y)
        {
            Box(cell, new Vector3(0f, 0.012f, 0f), new Vector3(0.98f, 0.024f, 0.98f), OilColor);
            float shift = ((x * 13 + y * 7) % 5) * 0.08f - 0.16f;
            Box(cell, new Vector3(shift, 0.028f, -shift), new Vector3(0.28f, 0.01f, 0.16f), Hex("4C3F6B"));
        }

        void BuildReactor(Transform cell)
        {
            Box(cell, new Vector3(0f, 0.08f, 0f), new Vector3(0.94f, 0.16f, 0.94f), ReactorMetal);
            Cylinder(cell, new Vector3(0f, 0.36f, 0f), new Vector3(0.62f, 0.2f, 0.62f), Metal);
            reactorCore = Cylinder(cell, new Vector3(0f, 0.66f, 0f), new Vector3(0.34f, 0.2f, 0.34f), ReactorGlow, true).transform;
            Box(cell, new Vector3(-0.36f, 0.42f, -0.36f), new Vector3(0.1f, 0.5f, 0.1f), ReactorMetal);
            Box(cell, new Vector3(0.36f, 0.42f, -0.36f), new Vector3(0.1f, 0.5f, 0.1f), ReactorMetal);
            Box(cell, new Vector3(-0.36f, 0.42f, 0.36f), new Vector3(0.1f, 0.5f, 0.1f), ReactorMetal);
            Box(cell, new Vector3(0.36f, 0.42f, 0.36f), new Vector3(0.1f, 0.5f, 0.1f), ReactorMetal);
        }

        // ---- Per-frame sync ----

        public void Sync(GameSim sim, float dt, bool snap = false)
        {
            clock += dt;

            if (sim.Player.Alive) SyncTank(sim.Player, sim, dt, snap);
            foreach (Tank enemy in sim.Enemies) SyncTank(enemy, sim, dt, snap);
            RemoveStaleTanks(sim);

            SyncBullets(sim);
            SyncBonus(sim);
            UpdateDebris(dt);
            UpdateBeams(dt);

            if (reactorCore != null)
            {
                float pulse = 0.34f + Mathf.Sin(clock * 3f) * 0.03f;
                reactorCore.localScale = new Vector3(pulse, 0.2f, pulse);
            }
        }

        void SyncTank(Tank tank, GameSim sim, float dt, bool snap)
        {
            if (!tankViews.TryGetValue(tank, out TankView view))
            {
                view = BuildTank(tank);
                tankViews[tank] = view;
                snap = true;
            }

            Vector3 target = ToWorld(tank.X, tank.Y);
            Quaternion facing = Quaternion.Euler(0f, (int)tank.Dir * 90f, 0f);
            if (snap)
            {
                view.Root.position = target;
                view.Root.rotation = facing;
            }
            else
            {
                view.Root.position = Vector3.Lerp(view.Root.position, target, 1f - Mathf.Exp(-30f * dt));
                view.Root.rotation = Quaternion.RotateTowards(view.Root.rotation, facing, 900f * dt);
            }

            float bob = tank.Moving ? Mathf.Sin(clock * 30f) * 0.012f : 0f;
            view.Model.localPosition = new Vector3(0f, bob, 0f);
            view.Model.gameObject.SetActive(!tank.Spawning || Mathf.Repeat(clock * 12f, 1f) < 0.5f);

            bool flashing = tank.Carrier && Mathf.Repeat(clock * 3f, 1f) < 0.5f;
            Material paint = flashing ? view.Flash : view.Normal;
            foreach (Renderer part in view.Paint) part.sharedMaterial = paint;

            if (view.MarkerRing != null) view.MarkerRing.localScale = Vector3.one * (1f + Mathf.Sin(clock * 4f) * 0.06f);
            if (view.HealthBar != null)
            {
                int segments = tank.IsPlayer ? tank.MaxHp : BossBarSegments;
                if (segments != view.SegmentCount) BuildHealthBar(view, view.Root, segments, tank.IsPlayer ? 1.3f : 1.7f);
                view.HealthBar.rotation = Quaternion.Euler(CameraRig.TiltDegrees, 0f, 0f);
                ShowHealth(view, tank.Hp, tank.MaxHp);
            }

            // A short blink after a hit, while the tank cannot be hurt again.
            if (tank.IsPlayer && tank.HurtTimer > 0f) view.Model.gameObject.SetActive(Mathf.Repeat(clock * 14f, 1f) < 0.6f);

            view.Shield.gameObject.SetActive(tank.Shielded);
            if (tank.Shielded) view.Shield.localRotation = Quaternion.Euler(0f, clock * 240f, 0f);
        }

        void RemoveStaleTanks(GameSim sim)
        {
            staleTanks.Clear();
            foreach (Tank tank in tankViews.Keys)
            {
                bool current = tank == sim.Player ? tank.Alive : tank.Alive && sim.Enemies.Contains(tank);
                if (!current) staleTanks.Add(tank);
            }
            foreach (Tank tank in staleTanks)
            {
                Kill(tankViews[tank].Root.gameObject);
                tankViews.Remove(tank);
            }
        }

        void SyncBullets(GameSim sim)
        {
            while (bulletViews.Count < sim.Bullets.Count)
                bulletViews.Add(Box(root, Vector3.zero, new Vector3(0.14f, 0.14f, 0.24f), Hex("FFE9A8"), true));

            for (int i = 0; i < bulletViews.Count; i++)
            {
                bool active = i < sim.Bullets.Count;
                bulletViews[i].SetActive(active);
                if (!active) continue;
                Bullet bullet = sim.Bullets[i];
                bulletViews[i].transform.position = ToWorld(bullet.X, bullet.Y, 0.42f);
                bulletViews[i].transform.rotation = Quaternion.Euler(0f, (int)bullet.Dir * 90f, 0f);
            }
        }

        void SyncBonus(GameSim sim)
        {
            Bonus bonus = sim.Bonus;
            if (bonus == null || bonusView == null || bonusViewKind != bonus.Kind)
            {
                if (bonusView != null) Kill(bonusView);
                bonusView = null;
                if (bonus == null) return;
                bonusView = BuildBonus(bonus.Kind);
                bonusViewKind = bonus.Kind;
            }

            bonusView.SetActive(bonus.Timer > 3f || Mathf.Repeat(clock * 5f, 1f) < 0.6f);
            bonusView.transform.position = ToWorld(bonus.X + 0.5f, bonus.Y + 0.5f);
            bonusIcon.localPosition = new Vector3(0f, 0.62f + Mathf.Sin(clock * 3f) * 0.06f, 0f);
            // Rocks instead of spinning, so the shape never turns edge-on to the camera.
            bonusModel.localRotation = Quaternion.Euler(0f, Mathf.Sin(clock * 2f) * 34f, 0f);
        }

        // ---- Effects ----

        /// <summary>The reactor strike: a bright beam from the tank to the edge of the map that fades out.</summary>
        public void Strike(float x, float y, Dir dir)
        {
            float dx = GameSim.DirX(dir), dy = GameSim.DirY(dir);
            float length = dx != 0f ? (dx > 0f ? Levels.Width - x : x) : (dy > 0f ? Levels.Height - y : y);
            Vector3 start = ToWorld(x, y, 0.45f);
            Vector3 end = ToWorld(x + dx * length, y + dy * length, 0.45f);
            GameObject item = Box(root, (start + end) / 2f, Vector3.one, BeamColor, true);
            item.transform.rotation = Quaternion.Euler(0f, (int)dir * 90f, 0f);
            item.transform.localScale = new Vector3(1.4f, 0.3f, length);
            item.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beams.Add(new Beam { Item = item.transform, Life = 0.45f, Width = 1.4f });
            for (float step = 0.5f; step < length; step += 1.2f)
                Burst(x + dx * step, y + dy * step, BeamColor, 3, 2f);
        }

        void UpdateBeams(float dt)
        {
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                Beam beam = beams[i];
                beam.Life -= dt;
                if (beam.Life <= 0f)
                {
                    Kill(beam.Item.gameObject);
                    beams.RemoveAt(i);
                    continue;
                }
                Vector3 scale = beam.Item.localScale;
                scale.x = beam.Width * beam.Life / 0.45f;
                beam.Item.localScale = scale;
            }
        }

        public void Burst(float x, float y, Color color, int count, float force)
        {
            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(0.06f, 0.18f);
                GameObject item = Box(root, ToWorld(x, y, 0.4f), Vector3.one * size, color, true);
                debris.Add(new Debris
                {
                    Item = item.transform,
                    Velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.6f, 1.6f), Random.Range(-1f, 1f)) * force,
                    Life = Random.Range(0.35f, 0.75f)
                });
            }
        }

        void UpdateDebris(float dt)
        {
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                Debris item = debris[i];
                item.Life -= dt;
                if (item.Life <= 0f)
                {
                    Kill(item.Item.gameObject);
                    debris.RemoveAt(i);
                    continue;
                }
                item.Velocity += Vector3.down * 9f * dt;
                item.Item.position += item.Velocity * dt;
                item.Item.Rotate(400f * dt, 300f * dt, 0f);
            }
        }

        // ---- Models ----

        // Enemy kinds share one drawing until they get their own, so size and tint tell them apart.
        static float SpriteScale(Tank tank)
        {
            if (tank.IsPlayer) return 1f;
            switch (tank.Kind)
            {
                case EnemyKind.Boss: return 1.5f;
                case EnemyKind.Scout: return 0.85f;
                case EnemyKind.Heavy: return 1.14f;
                default: return 1f;
            }
        }

        static Color SpriteTint(Tank tank)
        {
            if (tank.IsPlayer) return Color.white;
            switch (tank.Kind)
            {
                case EnemyKind.Scout: return new Color(1f, 1f, 0.55f);
                case EnemyKind.Gunner: return new Color(0.72f, 0.8f, 1f);
                case EnemyKind.Heavy: return new Color(0.62f, 0.55f, 0.55f);
                case EnemyKind.Boss: return BossTint;
                default: return Color.white;
            }
        }

        // The drawing lights itself a little, so dark art stays readable on the sand.
        Material SpriteGlowMaterial(string name, Color tint, Color glow)
        {
            string key = name + ColorUtility.ToHtmlStringRGB(tint) + ColorUtility.ToHtmlStringRGB(glow);
            if (spriteMaterials.TryGetValue(key, out Material material)) return material;

            Texture2D texture = Resources.Load<Texture2D>("gc26_tanki/Tanks/" + name);
            material = new Material(Resources.Load<Material>("gc26_tanki/SteelLineModel"))
            {
                mainTexture = texture,
                color = tint
            };
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", glow);
            spriteMaterials[key] = material;
            return material;
        }

        // Models come from Art/build_models.py: the drawings raised into 3D, with the drawing as texture.
        static Mesh ModelMesh(string name)
        {
            return Resources.Load<GameObject>("gc26_tanki/Tanks/" + name + "_model").GetComponentInChildren<MeshFilter>().sharedMesh;
        }

        TankView BuildTank(Tank tank)
        {
            var body = new GameObject(tank.IsPlayer ? "Player" : tank.Kind.ToString()).transform;
            body.SetParent(root, false);
            var model = new GameObject("Model").transform;
            model.SetParent(body, false);

            string spriteName = tank.IsPlayer ? "player" : "enemy";
            Color tint = SpriteTint(tank);

            var hull = new GameObject("Hull");
            hull.transform.SetParent(model, false);
            hull.transform.localScale = Vector3.one * SpriteScale(tank);
            hull.AddComponent<MeshFilter>().sharedMesh = ModelMesh(spriteName);
            var hullRenderer = hull.AddComponent<MeshRenderer>();
            hullRenderer.sharedMaterial = SpriteGlowMaterial(spriteName, tint, tint * 0.3f);
            var paint = new List<Renderer> { hullRenderer };

            var shield = new GameObject("Shield").transform;
            shield.SetParent(body, false);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI / 2f;
                Box(shield, new Vector3(Mathf.Cos(angle) * 0.56f, 0.32f, Mathf.Sin(angle) * 0.56f), new Vector3(0.1f, 0.1f, 0.1f), ReactorGlow, true);
            }

            var view = new TankView
            {
                Root = body,
                Model = model,
                Paint = paint.ToArray(),
                Normal = paint[0].sharedMaterial,
                Flash = SpriteGlowMaterial(spriteName, Color.white, new Color(1.6f, 1.45f, 1f)),
                Shield = shield
            };

            // A glowing ring and a health bar, so the player's tank is easy to find, even under a net.
            if (tank.IsPlayer)
            {
                view.MarkerRing = MarkerPart(body, RingMesh(0.62f, 0.76f, 40), new Vector3(0f, 0.025f, 0f), PlayerMark);
                BuildHealthBar(view, body, tank.MaxHp, 1.3f);
            }
            else if (tank.Kind == EnemyKind.Boss)
            {
                BuildHealthBar(view, body, BossBarSegments, 1.7f);
            }
            return view;
        }

        // The bar keeps the same width however many segments it has, so more armour means thinner segments.
        void BuildHealthBar(TankView view, Transform body, int segments, float height)
        {
            if (view.HealthBar != null) Kill(view.HealthBar.gameObject);
            var bar = new GameObject("Health bar").transform;
            bar.SetParent(body, false);
            bar.localPosition = new Vector3(0f, height, 0f);
            view.HealthBar = bar;
            view.SegmentCount = segments;

            const float width = 1.24f;
            const float gap = 0.03f;
            BarPart(bar, new Vector3(0f, 0f, 0.02f), new Vector3(width + 0.06f, 0.25f, 0.02f), HealthBack);
            float segment = (width - gap * (segments - 1)) / segments;

            view.HealthSegments = new Renderer[segments];
            for (int i = 0; i < segments; i++)
            {
                float x = -width / 2f + segment / 2f + i * (segment + gap);
                view.HealthSegments[i] = BarPart(bar, new Vector3(x, 0f, 0f), new Vector3(segment, 0.17f, 0.02f), HealthEmpty);
            }
        }

        Renderer BarPart(Transform bar, Vector3 localPosition, Vector3 size, Color color)
        {
            var renderer = Box(bar, localPosition, size, color, true).GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // Green while more than two thirds are left, yellow down to a third, red below.
        void ShowHealth(TankView view, int hp, int maxHp)
        {
            float share = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            int lit = Mathf.CeilToInt(share * view.SegmentCount - 0.001f);
            Color full = share > 0.67f ? HealthGreen : share > 0.34f ? HealthYellow : HealthRed;
            for (int i = 0; i < view.SegmentCount; i++)
                view.HealthSegments[i].sharedMaterial = MaterialFor(i < lit ? full : HealthEmpty, true);
        }

        Transform MarkerPart(Transform parent, Mesh mesh, Vector3 localPosition, Color color)
        {
            var part = new GameObject("Marker");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color, true);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        static Mesh RingMesh(float inner, float outer, int segments)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;

                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = next * 2;
                triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = next * 2;
                triangles[t + 4] = next * 2 + 1;
                triangles[t + 5] = i * 2 + 1;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }


        GameObject BuildBonus(BonusKind kind)
        {
            var holder = new GameObject("Bonus " + kind);
            holder.transform.SetParent(root, false);

            // A coloured ring on the ground shows where to drive to pick the bonus up.
            MarkerPart(holder.transform, RingMesh(0.4f, 0.48f, 28), new Vector3(0f, 0.02f, 0f), BonusColor(kind));

            // The icon faces the camera; the model inside it rocks from side to side.
            bonusIcon = new GameObject("Icon").transform;
            bonusIcon.SetParent(holder.transform, false);
            bonusIcon.localRotation = Quaternion.Euler(CameraRig.TiltDegrees, 0f, 0f);

            var model = new GameObject("Model");
            model.transform.SetParent(bonusIcon, false);
            model.AddComponent<MeshFilter>().sharedMesh =
                Resources.Load<GameObject>("gc26_tanki/Bonuses/" + kind.ToString().ToLowerInvariant()).GetComponentInChildren<MeshFilter>().sharedMesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = BonusMaterial();
            bonusModel = model.transform;
            return holder;
        }

        // Bonus models come from Art/build_bonuses.py.
        Material BonusMaterial()
        {
            if (bonusMaterial != null) return bonusMaterial;

            bonusMaterial = new Material(baseMaterial) { mainTexture = Resources.Load<Texture2D>("gc26_tanki/Bonuses/atlas") };
            bonusMaterial.SetFloat("_Smoothness", 0.05f);
            return bonusMaterial;
        }

        public static Color BonusColor(BonusKind kind)
        {
            switch (kind)
            {
                case BonusKind.Star: return Hex("FFD34D");
                case BonusKind.Bomb: return Hex("FF6B4A");
                case BonusKind.Shield: return Hex("6FF2E0");
                case BonusKind.Fort: return Hex("C9D1D6");
                case BonusKind.Freeze: return Hex("8CC8FF");
                default: return Hex("7CE05A");
            }
        }

        // ---- Building blocks ----

        GameObject Box(Transform parent, Vector3 localPosition, Vector3 size, Color color, bool glow = false)
        {
            return Primitive(PrimitiveType.Cube, parent, localPosition, size, color, glow);
        }

        GameObject Cylinder(Transform parent, Vector3 localPosition, Vector3 size, Color color, bool glow = false)
        {
            return Primitive(PrimitiveType.Cylinder, parent, localPosition, size, color, glow);
        }

        GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 size, Color color, bool glow)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            Kill(item.GetComponent<Collider>());
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = MaterialFor(color, glow);
            return item;
        }

        Material MaterialFor(Color color, bool glow = false)
        {
            Color key = glow ? new Color(color.r, color.g, color.b, 0.5f) : color;
            if (materials.TryGetValue(key, out Material material)) return material;

            material = new Material(glow ? Resources.Load<Material>("gc26_tanki/SteelLineGlow") : baseMaterial) { color = color };
            material.SetFloat("_Smoothness", 0.1f);
            // Above 1 in HDR, so the bloom picks these parts up.
            if (glow) material.SetColor("_EmissionColor", color * 1.4f);
            materials[key] = material;
            return material;
        }

        static void Kill(Object item)
        {
            if (Application.isPlaying) Object.Destroy(item);
            else Object.DestroyImmediate(item);
        }

        public static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
            return color;
        }
    }
}
