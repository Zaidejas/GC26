using GC26.Tanki.Rules;
using UnityEngine;

namespace GC26.Tanki.Game
{
    /// <summary>Immediate-mode HUD and menus. Good enough for now and easy to replace later.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        GameRunner game;
        Texture2D disc;
        Texture2D ring;
        Texture2D heart;
        Texture2D arrow;
        Texture2D pixel;
        GUIStyle fireLabel;
        GUIStyle chosenButton;
        GUIStyle rightLabel;
        GUIStyle label;
        GUIStyle title;
        GUIStyle button;
        GUIStyle small;
        GUIStyle superLabel;
        GUIStyle noteLabel;
        float unit;

        public void Init(GameRunner runner)
        {
            game = runner;
            disc = MakeCircle(128, false);
            ring = MakeCircle(128, true);
            heart = MakeHeart(64);
            arrow = MakeArrow(32);
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        public static string BonusLine(BonusKind kind)
        {
            switch (kind)
            {
                case BonusKind.Star: return "Звезда: орудие сильнее на " + (int)Tuning.StarTime + " секунд";
                case BonusKind.Bomb: return "Граната: все враги уничтожены";
                case BonusKind.Shield: return "Щит на 8 секунд";
                case BonusKind.Fort: return "База укреплена бетоном";
                case BonusKind.Freeze: return "Враги заморожены";
                default: return "Дополнительная жизнь";
            }
        }

        static readonly string[] StatNames = { "Броня", "Пушка", "Скорострельность", "Скорость" };
        static readonly string[] StatNotes =
        {
            "+1 к здоровью танка",
            "Снаряды быстрее, на 3-м уровне двойной урон",
            "Быстрее перезарядка, на 2-м уровне второй снаряд",
            "Танк ездит быстрее"
        };

        public static string UpgradeName(RunUpgrade upgrade)
        {
            switch (upgrade)
            {
                case RunUpgrade.Armor: return "Броня +1";
                case RunUpgrade.Gun: return "Пушка +1";
                case RunUpgrade.FireRate: return "Скорострельность +1";
                case RunUpgrade.Speed: return "Скорость +1";
                case RunUpgrade.Repair: return "Ремонт и жизнь";
                case RunUpgrade.SuperCharge: return "Быстрый заряд удара";
                default: return "Бетон у реактора";
            }
        }

        public static string UpgradeNote(RunUpgrade upgrade)
        {
            switch (upgrade)
            {
                case RunUpgrade.Armor: return "Больше здоровья и +1 сразу";
                case RunUpgrade.Gun: return "Быстрее снаряды, потом больше урона";
                case RunUpgrade.FireRate: return "Перезарядка короче, больше снарядов";
                case RunUpgrade.Speed: return "Танк ездит быстрее";
                case RunUpgrade.Repair: return "Танк и реактор как новые, +1 жизнь";
                case RunUpgrade.SuperCharge: return "Удару нужно на 1 убийство меньше";
                default: return "Реактор под бетоном 30 секунд";
            }
        }

        void OnGUI()
        {
            if (game == null || game.Sim == null) return;
            PrepareStyles();
            PrepareSkin();

            if (game.OnSplash)
            {
                DrawSplash();
                return;
            }

            switch (game.Page)
            {
                case Page.Menu: DrawTitle(); return;
                case Page.LevelSelect: DrawLevelSelect(); return;
                case Page.Workshop: DrawWorkshop(); return;
                case Page.Settings: DrawSettings(); return;
                case Page.Assault: DrawAssaultSelect(); return;
                case Page.Garage: DrawGarage(); return;
                case Page.Awards: DrawAwards(); return;
            }

            DrawTopBar();
            DrawBossBar();
            DrawTutorialHint();
            if (game.Sim.Phase == GamePhase.Playing && !game.Paused)
            {
                DrawControls();
                DrawToast();
            }
            DrawOverlay();
        }

        void PrepareStyles()
        {
            unit = Mathf.Max(Mathf.Min(Screen.width, Screen.height) / 400f, 0.5f);
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
                label.normal.textColor = new Color(1f, 0.95f, 0.85f);
                title = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal, wordWrap = true };
                fireLabel = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
                rightLabel = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
                superLabel = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
                noteLabel = new GUIStyle(label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Normal, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                button.normal.textColor = Color.white;
                button.hover.textColor = Color.white;
                button.active.textColor = Color.white;

                // Flat, with dark text, drawn over a light panel.
                chosenButton = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
                chosenButton.normal.textColor = new Color(0.17f, 0.13f, 0.21f);
                chosenButton.hover.textColor = chosenButton.normal.textColor;
                chosenButton.active.textColor = chosenButton.normal.textColor;
            }
            label.fontSize = Mathf.RoundToInt(15 * unit);
            fireLabel.fontSize = Mathf.RoundToInt(16 * unit);
            rightLabel.fontSize = label.fontSize;
            title.fontSize = Mathf.RoundToInt(28 * unit);
            small.fontSize = Mathf.RoundToInt(14 * unit);
            superLabel.fontSize = Mathf.RoundToInt(13 * unit);
            noteLabel.fontSize = Mathf.RoundToInt(12 * unit);
            button.fontSize = Mathf.RoundToInt(17 * unit);
            chosenButton.fontSize = button.fontSize;
        }

        Rect SafeRect()
        {
            Rect safe = Screen.safeArea;
            return new Rect(safe.x, Screen.height - safe.yMax, safe.width, safe.height);
        }

        void DrawSplash()
        {
            WorldView world = game.World;
            float shake = world.SplashTitleShake * 8f * unit;

            GUI.Label(new Rect(0f, Screen.height * 0.1f - shake, Screen.width, 60f * unit), "СТАЛЬНОЙ РУБЕЖ", title);

            float left = Screen.width * 0.09f;
            float width = Screen.width * 0.82f;
            float barHeight = 14f * unit;
            float barY = Screen.height * 0.87f;
            GUI.Label(new Rect(left, barY - 26f * unit, width, 22f * unit), "Зарядка реактора", label);
            GUI.Label(new Rect(left, barY - 26f * unit, width, 22f * unit), Mathf.RoundToInt(world.SplashCharge * 100f) + "%", rightLabel);

            Fill(new Rect(left, barY, width, barHeight), new Color(0.08f, 0.06f, 0.11f));
            const int segments = 3;
            float gap = 2f * unit;
            float inner = 2f * unit;
            float segment = (width - inner * 2f - gap * (segments - 1)) / segments;
            for (int i = 0; i < segments; i++)
            {
                float x = left + inner + i * (segment + gap);
                float amount = Mathf.Clamp01((world.SplashCharge - i / (float)segments) * segments);
                Fill(new Rect(x, barY + inner, segment, barHeight - inner * 2f), new Color(0.29f, 0.27f, 0.32f));
                Fill(new Rect(x, barY + inner, segment * amount, barHeight - inner * 2f), new Color(0.21f, 0.95f, 0.82f));
            }

            if (world.SplashFlash > 0f)
                Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 0.96f, 0.87f, world.SplashFlash));
        }

        void DrawTopBar()
        {
            GameSim sim = game.Sim;
            Rect safe = SafeRect();
            float height = 26f * unit;
            float y = safe.y + 4f * unit;
            float size = height * 1.5f;
            float x = safe.x + size + 14f * unit;

            Fill(new Rect(0f, 0f, Screen.width, y + height * 2f), new Color(0.1f, 0.07f, 0.14f, 0.72f));

            if (sim.Phase == GamePhase.Playing &&
                GUI.Button(new Rect(safe.x + 6f * unit, y, size, size), game.Paused ? ">" : "II", button))
            {
                game.TogglePause();
            }

            float heartsLeft = DrawHearts(safe.xMax - 8f * unit, y + height * 0.5f, Mathf.Max(sim.Lives, 0));
            GUI.Label(new Rect(x, y, heartsLeft - x, height), sim.LevelName, label);

            string stats = (sim.Mode == GameMode.Assault ? "Турелей " + sim.TurretsLeft : "Врагов " + sim.EnemiesLeft) + "   Очки " + sim.Score;
            if (sim.StarLevel > 0) stats += "   Звезда " + sim.StarLevel + " · " + Mathf.CeilToInt(sim.StarTimer) + "с";
            GUI.Label(new Rect(x, y + height, safe.xMax - x, height), stats, label);
        }

        // A wide bar under the top bar: the boss while it is out, otherwise the enemy reactor in an assault.
        void DrawBossBar()
        {
            GameSim sim = game.Sim;
            Tank boss = sim.Boss;
            string caption = "БОСС";
            float share;
            if (boss != null && !boss.Spawning) share = Mathf.Clamp01(boss.Hp / (float)boss.MaxHp);
            else if (sim.Mode == GameMode.Assault && sim.CoreMax > 0)
            {
                caption = "ВРАЖЕСКИЙ РЕАКТОР " + sim.CoreHp + "/" + sim.CoreMax;
                share = Mathf.Clamp01(sim.CoreHp / (float)sim.CoreMax);
            }
            else if (sim.Mode == GameMode.Survival || sim.Mode == GameMode.Campaign)
            {
                caption = "РЕАКТОР " + sim.BaseHp + "/" + sim.BaseMax;
                share = Mathf.Clamp01(sim.BaseHp / (float)sim.BaseMax);
            }
            else return;
            Rect safe = SafeRect();
            float top = safe.y + 4f * unit + 26f * unit * 2f + 8f * unit;
            float left = Screen.width * 0.12f;
            float width = Screen.width * 0.76f;
            float height = 12f * unit;
            GUI.Label(new Rect(left, top, width, 20f * unit), caption, small);
            float barY = top + 20f * unit;
            Fill(new Rect(left, barY, width, height), new Color(0.08f, 0.06f, 0.11f, 0.9f));
            Fill(new Rect(left + 2f * unit, barY + 2f * unit, (width - 4f * unit) * share, height - 4f * unit), new Color(1f, 0.33f, 0.28f));
        }

        // The current tutorial step, on a plate under the top bar.
        void DrawTutorialHint()
        {
            string text = game.TutorialText;
            if (text == null || game.Sim.Phase != GamePhase.Playing || game.Paused) return;
            Rect safe = SafeRect();
            float width = Screen.width * 0.86f;
            float textWidth = width - 24f * unit;
            float height = TextHeight(text, plateText, textWidth) + 30f * unit;
            var box = new Rect((Screen.width - width) / 2f, safe.y + 4f * unit + 26f * unit * 2f + 12f * unit, width, height);
            GUI.Box(box, GUIContent.none, choice);
            captionText.normal.textColor = Mark;
            GUI.Label(new Rect(box.x, box.y + 6f * unit, box.width, 14f * unit), "ОБУЧЕНИЕ · ШАГ " + (game.TutorialStep + 1) + " ИЗ " + GameRunner.TutorialSteps, captionText);
            captionText.normal.textColor = Muted;
            GUI.Label(new Rect(box.x + 12f * unit, box.y + 22f * unit, textWidth, height - 26f * unit), text, plateText);
        }

        void DrawSuperButton(Controls controls)
        {
            GameSim sim = game.Sim;
            Vector2 centre = ToGui(controls.SuperCentre);
            float radius = controls.SuperRadius;
            bool ready = sim.SuperReady;
            float pulse = ready ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f : 1f;

            Color fill = ready ? new Color(0.21f, 0.95f, 0.82f, 0.95f) : new Color(0.1f, 0.07f, 0.14f, 0.6f);
            DrawCircle(disc, centre, radius * pulse, fill);
            DrawCircle(ring, centre, radius * pulse, new Color(0.21f, 0.95f, 0.82f, ready ? 1f : 0.55f));

            // Charge shown as dots around the edge, lit one per kill.
            int need = Mathf.Max(1, sim.SuperNeed);
            float dot = Mathf.Max(4f * unit, radius * 0.16f);
            for (int i = 0; i < need; i++)
            {
                float angle = -Mathf.PI / 2f + i * Mathf.PI * 2f / need;
                var at = new Vector2(centre.x + Mathf.Cos(angle) * radius * 1.22f, centre.y + Mathf.Sin(angle) * radius * 1.22f);
                bool lit = i < sim.SuperCharge;
                DrawCircle(disc, at, dot / 2f, lit ? new Color(0.21f, 0.95f, 0.82f) : new Color(1f, 1f, 1f, 0.2f));
            }

            superLabel.normal.textColor = ready ? new Color(0.1f, 0.07f, 0.14f) : new Color(1f, 0.95f, 0.85f, 0.75f);
            GUI.Label(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), GameRunner.AbilityLabel(sim.Ability), superLabel);
        }

        // Draws from the right edge inwards and returns the left edge of what it drew.
        float DrawHearts(float right, float centreY, int lives)
        {
            const int maxHearts = 5;
            float size = 21f * unit;
            float step = size + 3f * unit;
            float x = right;

            if (lives > maxHearts)
            {
                float width = 28f * unit;
                x -= width;
                GUI.Label(new Rect(x, centreY - size / 2f, width, size), "+" + (lives - maxHearts), label);
            }

            int slots = Mathf.Max(Tuning.StartLives, Mathf.Min(lives, maxHearts));
            for (int i = slots - 1; i >= 0; i--)
            {
                x -= step;
                Color previous = GUI.color;
                GUI.color = i < lives ? new Color(0.94f, 0.22f, 0.31f) : new Color(1f, 1f, 1f, 0.16f);
                GUI.DrawTexture(new Rect(x, centreY - size / 2f, size, size), heart);
                GUI.color = previous;
            }
            return x;
        }

        void DrawControls()
        {
            Controls controls = game.Controls;
            float radius = controls.StickRadius;
            Vector2 centre = ToGui(controls.StickCentre);

            DrawCircle(disc, centre, radius, new Color(0.1f, 0.07f, 0.14f, 0.55f));
            DrawCircle(ring, centre, radius, new Color(1f, 0.95f, 0.85f, 0.45f));

            for (int d = 0; d < 4; d++)
            {
                bool on = controls.Direction.HasValue && (int)controls.Direction.Value == d;
                DrawArrow(centre, radius * 0.75f, d * 90f, on);
            }

            Vector2 knob = new Vector2(centre.x + controls.KnobOffset.x, centre.y - controls.KnobOffset.y);
            DrawCircle(disc, knob, radius * 0.42f, new Color(1f, 0.95f, 0.85f, 0.8f));

            Vector2 fire = ToGui(controls.FireCentre);
            float fireRadius = radius * (controls.FireHeld ? 0.8f : 0.85f);
            Color fireColor = controls.FireHeld ? new Color(0.91f, 0.39f, 0.23f, 1f) : new Color(0.91f, 0.39f, 0.23f, 0.82f);
            DrawCircle(disc, fire, fireRadius, fireColor);
            DrawCircle(ring, fire, fireRadius, new Color(1f, 0.95f, 0.85f, 0.6f));
            GUI.Label(new Rect(fire.x - fireRadius, fire.y - fireRadius, fireRadius * 2f, fireRadius * 2f), "ОГОНЬ", fireLabel);

            DrawSuperButton(controls);
        }

        void DrawArrow(Vector2 centre, float distance, float angle, bool on)
        {
            float size = 13f * unit;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previous = GUI.color;
            GUIUtility.RotateAroundPivot(angle, centre);
            GUI.color = on ? new Color(0.21f, 0.95f, 0.82f) : new Color(1f, 0.95f, 0.85f, 0.55f);
            GUI.DrawTexture(new Rect(centre.x - size / 2f, centre.y - distance - size / 2f, size, size), arrow);
            GUI.color = previous;
            GUI.matrix = previousMatrix;
        }

        void DrawToast()
        {
            if (game.ToastTimer <= 0f) return;
            var area = new Rect(0f, Screen.height * 0.16f, Screen.width, 30f * unit);
            GUI.Label(area, game.Toast, small);
        }

        // ---------- Menus: a painted picture behind, a dark plate with buttons in front ----------

        enum Art { Menu, Map, Workshop, Arena, Victory, Defeat }
        static readonly string[] ArtNames = { "menu", "map", "workshop", "arena", "victory", "defeat" };
        static readonly Color Paper = new Color(1f, 0.95f, 0.85f);
        static readonly Color Muted = new Color(0.8f, 0.73f, 0.66f);
        static readonly Color Sand = new Color(0.85f, 0.74f, 0.55f);
        static readonly Color Mark = new Color(0.21f, 0.95f, 0.82f);
        static readonly Color Ink = new Color(0.08f, 0.06f, 0.11f);

        Texture2D[] art;
        Texture2D shadeTexture;
        Art shownArt = (Art)(-1);
        float artChangedAt;
        float skinUnit;
        GUIStyle plate, primary, quiet, tile, bossTile, nextTile, lockedTile, chosenTile, row, choice, chip;
        GUIStyle heading, logoTag, logoBig, plateText, plateNote, valueText, captionText;

        void LoadArt()
        {
            art = new Texture2D[ArtNames.Length];
            for (int i = 0; i < ArtNames.Length; i++)
            {
                art[i] = Resources.Load<Texture2D>("gc26_tanki/Backgrounds/" + ArtNames[i]);
                if (art[i] != null) art[i].filterMode = FilterMode.Point;
            }

            // Darker at the top and bottom, where the title and the last buttons sit. Row 0 is the bottom.
            shadeTexture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float fromTop = 1f - y / 63f;
                float alpha = fromTop < 0.3f ? Mathf.Lerp(0.5f, 0f, fromTop / 0.3f)
                    : fromTop > 0.62f ? Mathf.Lerp(0f, 0.6f, (fromTop - 0.62f) / 0.38f) : 0f;
                shadeTexture.SetPixel(0, y, new Color(0.055f, 0.04f, 0.08f, alpha));
            }
            shadeTexture.Apply();
        }

        /// <summary>The picture fills the screen, drifting slowly, and fades in when it changes.</summary>
        void DrawArt(Art which)
        {
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            if (art == null) LoadArt();
            if (which != shownArt)
            {
                shownArt = which;
                artChangedAt = Time.unscaledTime;
            }

            Fill(screen, new Color(0.086f, 0.063f, 0.11f));
            Texture2D picture = art[(int)which];
            if (picture != null)
            {
                float drift = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.13f);
                float aspect = picture.width / (float)picture.height;
                float height = Mathf.Max(Screen.height, Screen.width / aspect) * (1.04f + 0.05f * drift);
                float width = height * aspect;
                float x = (Screen.width - width) / 2f - drift * Screen.width * 0.015f;
                float y = (Screen.height - height) / 2f - drift * Screen.height * 0.012f;
                GUI.DrawTexture(new Rect(x, y, width, height), picture);
            }
            GUI.DrawTexture(screen, shadeTexture);

            float fade = 1f - (Time.unscaledTime - artChangedAt) / 0.35f;
            if (fade > 0f) Fill(screen, new Color(0.086f, 0.063f, 0.11f, fade));
        }

        void PrepareSkin()
        {
            if (plate != null && Mathf.Abs(skinUnit - unit) < 0.01f) return;
            skinUnit = unit;
            int cut = Mathf.Max(4, Mathf.RoundToInt(8f * unit));
            int edge = Mathf.Max(1, Mathf.RoundToInt(1.5f * unit));
            int shadeRows = Mathf.Max(2, Mathf.RoundToInt(3f * unit));
            int size = cut * 4 + 8;

            Texture2D Panel(Color top, Color bottom, Color rim, bool allCorners, float lift = 0f, float shade = 0f) =>
                MakePanel(size, cut, top, bottom, rim, edge, allCorners, lift, shade, shadeRows);
            GUIStyle Style(Texture2D normal, Texture2D hover, Texture2D active, Color text, int fontSize, TextAnchor anchor = TextAnchor.MiddleCenter)
            {
                var style = new GUIStyle
                {
                    border = new RectOffset(cut + 2, cut + 2, cut + 2, cut + 2),
                    alignment = anchor,
                    fontStyle = FontStyle.Bold,
                    fontSize = fontSize,
                    wordWrap = true,
                    padding = new RectOffset(Mathf.RoundToInt(12 * unit), Mathf.RoundToInt(12 * unit), 4, 4)
                };
                style.normal.background = normal;
                style.hover.background = hover ?? normal;
                style.active.background = active ?? normal;
                style.normal.textColor = style.hover.textColor = style.active.textColor = text;
                return style;
            }

            var clear = new Color(0f, 0f, 0f, 0f);
            var brass = new Color(0.85f, 0.74f, 0.55f, 0.35f);
            int buttonFont = Mathf.RoundToInt(18 * unit);
            plate = Style(Panel(new Color(0.12f, 0.086f, 0.15f, 0.86f), new Color(0.07f, 0.05f, 0.094f, 0.92f), brass, true), null, null, Paper, 14);
            primary = Style(Panel(new Color(0.95f, 0.48f, 0.3f), new Color(0.72f, 0.26f, 0.12f), clear, false, 0.35f, 0.5f),
                Panel(new Color(1f, 0.56f, 0.36f), new Color(0.8f, 0.31f, 0.15f), clear, false, 0.35f, 0.5f),
                Panel(new Color(0.8f, 0.36f, 0.2f), new Color(0.6f, 0.2f, 0.08f), clear, false, 0.1f, 0.3f), Color.white, buttonFont);
            quiet = Style(Panel(new Color(0.27f, 0.23f, 0.33f, 0.92f), new Color(0.17f, 0.13f, 0.22f, 0.92f), new Color(0.85f, 0.74f, 0.55f, 0.5f), false, 0.12f),
                Panel(new Color(0.33f, 0.28f, 0.4f, 0.95f), new Color(0.22f, 0.17f, 0.28f, 0.95f), new Color(0.85f, 0.74f, 0.55f, 0.7f), false, 0.12f),
                Panel(new Color(0.2f, 0.16f, 0.25f, 0.95f), new Color(0.14f, 0.1f, 0.18f, 0.95f), new Color(0.85f, 0.74f, 0.55f, 0.7f), false), Paper, buttonFont);
            int tileFont = Mathf.RoundToInt(22 * unit);
            tile = Style(Panel(new Color(0.35f, 0.31f, 0.39f), new Color(0.23f, 0.19f, 0.27f), clear, false, 0.18f, 0.35f), null, null, Paper, tileFont);
            bossTile = Style(Panel(new Color(0.72f, 0.26f, 0.18f), new Color(0.45f, 0.14f, 0.1f), clear, false, 0.2f, 0.35f), null, null, Paper, tileFont);
            nextTile = Style(Panel(new Color(0.18f, 0.83f, 0.74f), new Color(0.09f, 0.55f, 0.5f), Mark, false, 0.4f, 0.3f), null, null, Ink, tileFont);
            lockedTile = Style(Panel(new Color(0.12f, 0.094f, 0.15f, 0.85f), new Color(0.12f, 0.094f, 0.15f, 0.85f), new Color(1f, 0.95f, 0.85f, 0.1f), false), null, null, new Color(1f, 0.95f, 0.85f, 0.3f), Mathf.RoundToInt(12 * unit));
            chosenTile = Style(Panel(Sand, new Color(0.72f, 0.6f, 0.42f), clear, false, 0.3f, 0.3f), null, null, Ink, Mathf.RoundToInt(13 * unit));
            chosenTile.wordWrap = false;
            chosenTile.padding = new RectOffset(2, 2, 2, 2);
            row = Style(Panel(new Color(1f, 0.95f, 0.85f, 0.07f), new Color(1f, 0.95f, 0.85f, 0.05f), new Color(1f, 0.95f, 0.85f, 0.1f), false), null, null, Paper, 14);
            choice = Style(Panel(new Color(0.12f, 0.33f, 0.33f, 0.92f), new Color(0.1f, 0.12f, 0.16f, 0.92f), new Color(0.21f, 0.95f, 0.82f, 0.6f), false),
                Panel(new Color(0.15f, 0.42f, 0.4f, 0.95f), new Color(0.12f, 0.16f, 0.2f, 0.95f), Mark, false),
                Panel(new Color(0.1f, 0.26f, 0.26f, 0.95f), new Color(0.08f, 0.1f, 0.13f, 0.95f), Mark, false), Paper, 14, TextAnchor.UpperLeft);
            chip = Style(Panel(new Color(0.055f, 0.04f, 0.08f, 0.8f), new Color(0.055f, 0.04f, 0.08f, 0.8f), brass, true), null, null, Paper, Mathf.RoundToInt(13 * unit));

            heading = new GUIStyle(title) { fontSize = Mathf.RoundToInt(34 * unit), wordWrap = true };
            logoTag = new GUIStyle(title) { fontSize = Mathf.RoundToInt(13 * unit) };
            logoTag.normal.textColor = Mark;
            logoBig = new GUIStyle(title) { fontSize = Mathf.RoundToInt(60 * unit) };
            plateText = new GUIStyle(small) { fontSize = Mathf.RoundToInt(14 * unit) };
            plateNote = new GUIStyle(noteLabel) { fontSize = Mathf.RoundToInt(12 * unit) };
            plateNote.normal.textColor = Muted;
            valueText = new GUIStyle(title) { fontSize = Mathf.RoundToInt(22 * unit) };
            captionText = new GUIStyle(small) { fontSize = Mathf.RoundToInt(10 * unit) };
            captionText.normal.textColor = Muted;
        }

        /// <summary>Text with a dark drop shadow, so it reads on any picture.</summary>
        void Shadowed(Rect area, string text, GUIStyle style, Color colour)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = new Color(0.16f, 0.11f, 0.08f, 0.9f);
            GUI.Label(new Rect(area.x, area.y + 3f * unit, area.width, area.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(area, text, style);
            style.normal.textColor = previous;
        }

        void DrawHeading(float y, string text, Color colour)
        {
            Shadowed(new Rect(Screen.width * 0.05f, y, Screen.width * 0.9f, 84f * unit), text.ToUpperInvariant(), heading, colour);
        }

        /// <summary>The logo: gold letters with a lighter top half, like polished metal, and a reactor-coloured line under them.</summary>
        void DrawLogo(float y)
        {
            Shadowed(new Rect(0f, y, Screen.width, 20f * unit), "З А Щ И Т И   Р Е А К Т О Р", logoTag, Mark);
            float line = 56f * unit;
            string[] words = { "СТАЛЬНОЙ", "РУБЕЖ" };
            for (int i = 0; i < words.Length; i++)
            {
                var area = new Rect(0f, y + 20f * unit + i * line, Screen.width, line + 8f * unit);
                Shadowed(area, words[i], logoBig, new Color(0.78f, 0.6f, 0.37f));
                GUI.BeginGroup(new Rect(0f, area.y, Screen.width, area.height * 0.5f));
                logoBig.normal.textColor = new Color(1f, 0.95f, 0.86f);
                GUI.Label(new Rect(0f, 0f, Screen.width, area.height), words[i], logoBig);
                GUI.EndGroup();
            }
            float lineY = y + 20f * unit + line * 2f + 12f * unit;
            for (int i = 0; i < 12; i++)
            {
                float share = 1f - Mathf.Abs(i - 5.5f) / 6f;
                float width = Screen.width * 0.6f / 12f;
                Fill(new Rect(Screen.width * 0.2f + i * width, lineY, width + 1f, 3f * unit), new Color(Mark.r, Mark.g, Mark.b, share));
            }
        }

        void DrawChips(float y, params string[] texts)
        {
            float gap = 8f * unit;
            float height = 28f * unit;
            var widths = new float[texts.Length];
            float total = -gap;
            for (int i = 0; i < texts.Length; i++)
            {
                widths[i] = chip.CalcSize(new GUIContent(texts[i])).x + 8f * unit;
                total += widths[i] + gap;
            }
            float x = (Screen.width - total) / 2f;
            for (int i = 0; i < texts.Length; i++)
            {
                GUI.Box(new Rect(x, y, widths[i], height), texts[i], chip);
                x += widths[i] + gap;
            }
        }

        float PlateWidth => Mathf.Min(Screen.width - 32f * unit, 390f * unit);
        float PlateLeft => (Screen.width - PlateWidth) / 2f;
        float Pad => 16f * unit;
        float ButtonHeight => 50f * unit;

        Rect DrawPlate(float y, float innerHeight)
        {
            var area = new Rect(PlateLeft, y, PlateWidth, innerHeight + Pad * 2f);
            GUI.Box(area, GUIContent.none, plate);
            return new Rect(area.x + Pad, area.y + Pad, area.width - Pad * 2f, innerHeight);
        }

        /// <summary>Buttons stacked in a column; the first one is the main one.</summary>
        int ButtonColumn(Rect inner, float y, string[] labels, int main = 0)
        {
            float gap = 10f * unit;
            float width = Mathf.Min(inner.width, 300f * unit);
            float x = inner.x + (inner.width - width) / 2f;
            int pressed = -1;
            for (int i = 0; i < labels.Length; i++)
                if (GUI.Button(new Rect(x, y + i * (ButtonHeight + gap), width, ButtonHeight), labels[i], i == main ? primary : quiet)) pressed = i;
            return pressed;
        }

        static float ColumnHeight(int count, float buttonHeight, float gap) => count * buttonHeight + (count - 1) * gap;

        /// <summary>Two buttons side by side, the first one the main one.</summary>
        int ButtonRow(Rect inner, float y, string first, string second)
        {
            float gap = 10f * unit;
            float width = (inner.width - gap) / 2f;
            if (GUI.Button(new Rect(inner.x, y, width, ButtonHeight), first, primary)) return 0;
            if (GUI.Button(new Rect(inner.x + width + gap, y, width, ButtonHeight), second, quiet)) return 1;
            return -1;
        }

        float TextHeight(string text, GUIStyle style, float width) => style.CalcHeight(new GUIContent(text), width);

        void DrawTitle()
        {
            DrawArt(Art.Menu);
            float logo = 20f * unit + 56f * unit * 2f + 22f * unit;
            float buttons = ColumnHeight(6, ButtonHeight, 10f * unit);
            float total = logo + 14f * unit + 28f * unit + 16f * unit + buttons + Pad * 2f;
            float y = Mathf.Max(SafeRect().y + 12f * unit, (Screen.height - total) / 2f - 20f * unit);

            DrawLogo(y);
            y += logo + 14f * unit;
            string records = "Рекорд " + game.Save.BestScore;
            string stars = "★ " + game.Save.TotalStars;
            if (game.Save.BestWave > 0) DrawChips(y, "Детали " + game.Save.Parts, records, "Волна " + game.Save.BestWave, stars);
            else DrawChips(y, "Детали " + game.Save.Parts, records, stars);
            y += 28f * unit + 16f * unit;

            // The campaign on its own line, the other six in pairs, the exit at the bottom.
            Rect inner = DrawPlate(y, buttons);
            float gap = 10f * unit;
            float width = Mathf.Min(inner.width, 320f * unit);
            float x = inner.x + (inner.width - width) / 2f;
            float half = (width - gap) / 2f;
            float rowY = inner.y;
            if (GUI.Button(new Rect(x, rowY, width, ButtonHeight), "КАМПАНИЯ", primary)) game.Open(Page.LevelSelect);
            string[] pairs = { "ШТУРМ", "ВЫЖИВАНИЕ", "ГАРАЖ", "МАСТЕРСКАЯ", "НАГРАДЫ", "НАСТРОЙКИ", "ОБУЧЕНИЕ", "ВЫХОД" };
            int font = quiet.fontSize;
            quiet.fontSize = Mathf.RoundToInt(15 * unit);
            for (int i = 0; i < pairs.Length; i++)
            {
                var box = new Rect(x + (i % 2) * (half + gap), rowY + (ButtonHeight + gap) * (1 + i / 2), half, ButtonHeight);
                if (!GUI.Button(box, pairs[i], quiet)) continue;
                switch (i)
                {
                    case 0: game.Open(Page.Assault); break;
                    case 1: game.StartSurvival(); break;
                    case 2: game.Open(Page.Garage); break;
                    case 3: game.Open(Page.Workshop); break;
                    case 4: awardsPage = 0; game.Open(Page.Awards); break;
                    case 5: game.Open(Page.Settings); break;
                    case 6: game.StartTutorial(); break;
                    case 7: game.Quit(); break;
                }
            }
            quiet.fontSize = font;
        }

        static string StarLine(int stars) => new string('★', stars) + new string('☆', 3 - stars);

        // ---------- Assault, garage, awards ----------

        void DrawAssaultSelect()
        {
            DrawArt(Art.Arena);
            const int columns = 3;
            float gap = 8f * unit;
            float textWidth = PlateWidth - Pad * 2f;
            string help = "Теперь нападаешь ты: пробейся к вражескому реактору и разбей его. Турели бьют по прямой.";
            float helpHeight = TextHeight(help, plateText, textWidth);
            float cellHeight = 74f * unit;
            float grid = 2f * cellHeight + gap;
            float inner = helpHeight + 10f * unit + grid + 10f * unit + 22f * unit + 12f * unit + ButtonHeight;
            float total = 84f * unit + inner + Pad * 2f;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - total) / 2f);

            DrawHeading(y, "Штурм", Paper);
            Rect area = DrawPlate(y + 84f * unit, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, helpHeight), help, plateText);

            float top = area.y + helpHeight + 10f * unit;
            float cell = (area.width - gap * (columns - 1)) / columns;
            int unlocked = game.Save.AssaultUnlocked;
            int shown = unlocked;
            for (int i = 0; i < Levels.Assault.Length; i++)
            {
                var box = new Rect(area.x + i % columns * (cell + gap), top + i / columns * (cellHeight + gap), cell, cellHeight);
                if (i > unlocked)
                {
                    GUI.Box(box, "закрыта", lockedTile);
                    continue;
                }
                if (box.Contains(Event.current.mousePosition)) shown = i;
                if (GUI.Button(box, (i + 1).ToString(), i == unlocked ? nextTile : bossTile)) game.StartAssault(i);
                captionText.normal.textColor = i == unlocked ? Ink : new Color(1f, 0.85f, 0.4f);
                GUI.Label(new Rect(box.x, box.yMax - 18f * unit, box.width, 14f * unit), StarLine(game.Save.AssaultStars[i]), captionText);
                captionText.normal.textColor = Muted;
            }

            float nameY = top + grid + 10f * unit;
            plateText.normal.textColor = Sand;
            GUI.Label(new Rect(area.x, nameY, area.width, 22f * unit), (shown + 1) + ". " + Levels.Assault[shown].Name, plateText);
            plateText.normal.textColor = Paper;
            if (ButtonColumn(area, nameY + 34f * unit, new[] { "НАЗАД" }) == 0) game.Open(Page.Menu);
        }

        readonly System.Collections.Generic.Dictionary<string, Texture2D> tankPictures = new System.Collections.Generic.Dictionary<string, Texture2D>();

        Texture2D TankPicture(string model)
        {
            if (!tankPictures.TryGetValue(model, out Texture2D picture))
            {
                picture = Resources.Load<Texture2D>("gc26_tanki/Tanks/" + model);
                tankPictures[model] = picture;
            }
            return picture;
        }

        void DrawGarage()
        {
            DrawArt(Art.Workshop);
            float rowHeight = 92f * unit;
            float gap = 8f * unit;
            int count = System.Enum.GetValues(typeof(PlayerTank)).Length;
            float inner = 22f * unit + 10f * unit + count * rowHeight + (count - 1) * gap + 14f * unit + ButtonHeight;
            float total = 84f * unit + 28f * unit + 12f * unit + inner + Pad * 2f;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - total) / 2f);

            DrawHeading(y, "Гараж", Paper);
            y += 84f * unit;
            DrawChips(y, "Детали " + game.Save.Parts);
            y += 28f * unit + 12f * unit;
            Rect area = DrawPlate(y, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, 22f * unit), "У каждого танка своя способность", plateText);

            float top = area.y + 32f * unit;
            float buyWidth = 92f * unit;
            float picture = 64f * unit;
            for (int i = 0; i < count; i++)
            {
                var tank = (PlayerTank)i;
                TankDef def = TankDef.For(tank);
                bool owned = game.Save.Owns(tank);
                bool chosen = game.Save.ChosenTank == tank;
                var box = new Rect(area.x, top + i * (rowHeight + gap), area.width, rowHeight);
                GUI.Box(box, GUIContent.none, chosen ? choice : row);

                Texture2D art = TankPicture(def.Model);
                if (art != null)
                    GUI.DrawTexture(new Rect(box.x + 4f * unit, box.y + (rowHeight - picture) / 2f - 6f * unit, picture, picture + 12f * unit), art, ScaleMode.ScaleToFit);

                float textLeft = box.x + picture + 10f * unit;
                float textWidth = box.width - picture - buyWidth - 24f * unit;
                GUI.Label(new Rect(textLeft, box.y + 4f * unit, textWidth, 24f * unit), def.Name, label);
                GUI.Label(new Rect(textLeft, box.y + 27f * unit, textWidth, 30f * unit), def.Note, plateNote);
                plateNote.normal.textColor = Mark;
                GUI.Label(new Rect(textLeft, box.y + 56f * unit, textWidth, 32f * unit), def.Power, plateNote);
                plateNote.normal.textColor = Muted;

                var buy = new Rect(box.xMax - buyWidth - 8f * unit, box.y + (rowHeight - 50f * unit) / 2f, buyWidth, 50f * unit);
                if (chosen)
                {
                    GUI.Label(buy, "Выбран", plateText);
                    continue;
                }
                int font = primary.fontSize;
                if (owned)
                {
                    quiet.fontSize = Mathf.RoundToInt(14 * unit);
                    if (GUI.Button(buy, "Выбрать", quiet)) game.ChooseTank(tank);
                    quiet.fontSize = Mathf.RoundToInt(18 * unit);
                    continue;
                }
                bool affordable = game.Save.Parts >= def.Price;
                GUIStyle buyStyle = affordable ? primary : quiet;
                font = buyStyle.fontSize;
                buyStyle.fontSize = Mathf.RoundToInt(14 * unit);
                GUI.enabled = affordable;
                if (GUI.Button(buy, "Купить\n" + def.Price + " дет.", buyStyle)) game.BuyTank(tank);
                GUI.enabled = true;
                buyStyle.fontSize = font;
            }

            if (ButtonColumn(area, top + count * rowHeight + (count - 1) * gap + 14f * unit, new[] { "НАЗАД" }) == 0) game.Open(Page.Menu);
        }

        int awardsPage;
        const int AwardsPerPage = 6;

        void DrawAwards()
        {
            DrawArt(Art.Victory);
            var all = TankiAwards.All;
            int pages = (all.Length + AwardsPerPage - 1) / AwardsPerPage;
            awardsPage = Mathf.Clamp(awardsPage, 0, pages - 1);
            float rowHeight = 50f * unit;
            float gap = 6f * unit;
            float inner = AwardsPerPage * rowHeight + (AwardsPerPage - 1) * gap + 12f * unit + ButtonHeight + 10f * unit + ButtonHeight;
            float total = 84f * unit + 28f * unit + 12f * unit + inner + Pad * 2f;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - total) / 2f);

            DrawHeading(y, "Награды", Paper);
            y += 84f * unit;
            DrawChips(y, "Получено " + game.Save.Awards.Count + "/" + all.Length, "★ " + game.Save.TotalStars);
            y += 28f * unit + 12f * unit;
            Rect area = DrawPlate(y, inner);

            for (int i = 0; i < AwardsPerPage; i++)
            {
                int index = awardsPage * AwardsPerPage + i;
                if (index >= all.Length) break;
                TankiAwards.Award award = all[index];
                bool done = game.Save.Awards.Contains(award.Id);
                int value = Mathf.Min(award.Goal, award.Value(game.Save));
                var box = new Rect(area.x, area.y + i * (rowHeight + gap), area.width, rowHeight);
                GUI.Box(box, GUIContent.none, done ? choice : row);
                float rewardWidth = 84f * unit;
                float textWidth = box.width - rewardWidth - 20f * unit;
                GUI.Label(new Rect(box.x + 10f * unit, box.y + 3f * unit, textWidth, 22f * unit), award.Name, label);
                string text = award.Text + (award.Goal > 1 && !done ? " (" + value + "/" + award.Goal + ")" : "");
                GUI.Label(new Rect(box.x + 10f * unit, box.y + 24f * unit, textWidth, 18f * unit), text, plateNote);
                Fill(new Rect(box.x + 10f * unit, box.yMax - 6f * unit, textWidth, 3f * unit), new Color(1f, 1f, 1f, 0.12f));
                Fill(new Rect(box.x + 10f * unit, box.yMax - 6f * unit, textWidth * value / award.Goal, 3f * unit), Mark);
                captionText.normal.textColor = new Color(0.91f, 0.75f, 0.38f);
                captionText.fontSize = Mathf.RoundToInt(12 * unit);
                GUI.Label(new Rect(box.xMax - rewardWidth - 6f * unit, box.y, rewardWidth, rowHeight), done ? "получено" : "+" + award.Reward + " дет.", captionText);
                captionText.fontSize = Mathf.RoundToInt(10 * unit);
                captionText.normal.textColor = Muted;
            }

            float pagerY = area.y + AwardsPerPage * rowHeight + (AwardsPerPage - 1) * gap + 12f * unit;
            float third = (area.width - 20f * unit) / 3f;
            GUI.enabled = awardsPage > 0;
            if (GUI.Button(new Rect(area.x, pagerY, third, ButtonHeight), "<", quiet)) awardsPage--;
            GUI.enabled = true;
            GUI.Label(new Rect(area.x + third + 10f * unit, pagerY, third, ButtonHeight), (awardsPage + 1) + " / " + pages, plateText);
            GUI.enabled = awardsPage < pages - 1;
            if (GUI.Button(new Rect(area.x + (third + 10f * unit) * 2f, pagerY, third, ButtonHeight), ">", quiet)) awardsPage++;
            GUI.enabled = true;
            if (ButtonColumn(area, pagerY + ButtonHeight + 10f * unit, new[] { "НАЗАД" }) == 0) game.Open(Page.Menu);
        }

        void DrawLevelSelect()
        {
            DrawArt(Art.Map);
            const int columns = 4;
            float gap = 8f * unit;
            float textHeight = 22f * unit;
            float cellHeight = 60f * unit;
            float rows = Mathf.Ceil(Levels.All.Length / (float)columns);
            float grid = rows * cellHeight + (rows - 1) * gap;
            float inner = textHeight + 10f * unit + grid + 10f * unit + 22f * unit + 12f * unit + ButtonHeight;
            float total = 84f * unit + inner + Pad * 2f;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - total) / 2f);

            DrawHeading(y, "Кампания", Paper);
            Rect area = DrawPlate(y + 84f * unit, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, textHeight), "Звёзды: победа, без потерь, быстрее нормы", plateText);

            float top = area.y + textHeight + 10f * unit;
            float cell = (area.width - gap * (columns - 1)) / columns;
            int unlocked = game.Save.UnlockedLevel;
            int shown = unlocked;
            for (int i = 0; i < Levels.All.Length; i++)
            {
                var box = new Rect(area.x + i % columns * (cell + gap), top + i / columns * (cellHeight + gap), cell, cellHeight);
                bool open = i <= unlocked;
                bool boss = (i + 1) % Tuning.BossEvery == 0;
                if (!open)
                {
                    GUI.Box(box, "закрыт", lockedTile);
                    continue;
                }
                if (box.Contains(Event.current.mousePosition)) shown = i;
                GUIStyle style = i == unlocked ? nextTile : boss ? bossTile : tile;
                if (GUI.Button(box, (i + 1).ToString(), style)) game.StartLevel(i);
                captionText.normal.textColor = i == unlocked ? Ink : boss ? new Color(1f, 0.82f, 0.78f) : new Color(1f, 0.85f, 0.4f);
                string caption = StarLine(game.Save.Stars[i]) + (boss ? " БОСС" : "");
                GUI.Label(new Rect(box.x, box.yMax - 17f * unit, box.width, 14f * unit), caption, captionText);
                captionText.normal.textColor = Muted;
            }

            float nameY = top + grid + 10f * unit;
            plateText.normal.textColor = Sand;
            GUI.Label(new Rect(area.x, nameY, area.width, 22f * unit), (shown + 1) + ". " + Levels.All[shown].Name, plateText);
            plateText.normal.textColor = Paper;
            if (ButtonColumn(area, nameY + 34f * unit, new[] { "НАЗАД" }) == 0) game.Open(Page.Menu);
        }

        void DrawWorkshop()
        {
            DrawArt(Art.Workshop);
            float rowHeight = 82f * unit;
            float gap = 8f * unit;
            float inner = 22f * unit + 10f * unit + 4f * rowHeight + 3f * gap + 14f * unit + ButtonHeight;
            float total = 84f * unit + 28f * unit + 12f * unit + inner + Pad * 2f;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - total) / 2f);

            DrawHeading(y, "Мастерская", Paper);
            y += 84f * unit;
            DrawChips(y, "Детали " + game.Save.Parts);
            y += 28f * unit + 12f * unit;
            Rect area = DrawPlate(y, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, 22f * unit), "Детали дают за врагов и пройденные уровни", plateText);

            float top = area.y + 32f * unit;
            float buyWidth = 96f * unit;
            for (int i = 0; i < 4; i++)
            {
                var stat = (Stat)i;
                var box = new Rect(area.x, top + i * (rowHeight + gap), area.width, rowHeight);
                GUI.Box(box, GUIContent.none, row);
                float textLeft = box.x + 12f * unit;
                float textWidth = box.width - buyWidth - 30f * unit;
                GUI.Label(new Rect(textLeft, box.y + 6f * unit, textWidth, 24f * unit), StatNames[i], label);
                GUI.Label(new Rect(textLeft, box.y + 30f * unit, textWidth, 32f * unit), StatNotes[i], plateNote);

                int level = game.Save.Upgrades[i];
                float pipWidth = 22f * unit;
                for (int p = 0; p < Tuning.WorkshopMaxLevel; p++)
                    Fill(new Rect(textLeft + p * (pipWidth + 4f * unit), box.yMax - 14f * unit, pipWidth, 6f * unit),
                        p < level ? Mark : new Color(1f, 1f, 1f, 0.16f));

                int cost = game.Save.NextCost(stat);
                var buy = new Rect(box.xMax - buyWidth - 10f * unit, box.y + (rowHeight - 54f * unit) / 2f, buyWidth, 54f * unit);
                if (cost < 0)
                {
                    GUI.Label(buy, "Максимум", plateText);
                    continue;
                }
                bool affordable = game.Save.Parts >= cost;
                GUIStyle buyStyle = affordable ? primary : quiet;
                int font = buyStyle.fontSize;
                buyStyle.fontSize = Mathf.RoundToInt(14 * unit);
                GUI.enabled = affordable;
                if (GUI.Button(buy, "Улучшить\n" + cost + " дет.", buyStyle)) game.Buy(stat);
                GUI.enabled = true;
                buyStyle.fontSize = font;
            }

            if (ButtonColumn(area, top + 4f * rowHeight + 3f * gap + 14f * unit, new[] { "НАЗАД" }) == 0) game.Open(Page.Menu);
        }

        Art PauseArt => game.Sim.Mode == GameMode.Campaign ? Art.Menu : Art.Arena;

        void DrawSettings()
        {
            DrawArt(game.Paused ? PauseArt : Art.Menu);
            float textWidth = PlateWidth - Pad * 2f;
            string help = "Слева джойстик, справа огонь, внизу способность танка";
            float helpHeight = TextHeight(help, plateText, textWidth);
            float buttons = ColumnHeight(3, ButtonHeight, 10f * unit);
            float inner = helpHeight + 12f * unit + 22f * unit + 6f * unit + 44f * unit + 16f * unit + buttons;
            float total = 84f * unit + inner + Pad * 2f;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - total) / 2f);

            DrawHeading(y, "Настройки", Paper);
            Rect area = DrawPlate(y + 84f * unit, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, helpHeight), help, plateText);
            float pickerY = area.y + helpHeight + 12f * unit;
            GUI.Label(new Rect(area.x, pickerY, area.width, 22f * unit), "Сложность (со следующего боя)", plateText);
            DrawDifficultyPicker(area, pickerY + 28f * unit);

            string[] labels =
            {
                "ЗВУК: " + (game.Save.SoundOn ? "ВКЛ" : "ВЫКЛ"),
                "ВИБРАЦИЯ: " + (game.Save.VibrationOn ? "ВКЛ" : "ВЫКЛ"),
                "НАЗАД"
            };
            switch (ButtonColumn(area, pickerY + 28f * unit + 44f * unit + 16f * unit, labels, 2))
            {
                case 0: game.SetSound(!game.Save.SoundOn); break;
                case 1: game.SetVibration(!game.Save.VibrationOn); break;
                case 2: game.CloseSettings(); break;
            }
        }

        void DrawDifficultyPicker(Rect area, float y)
        {
            string[] names = { "Легко", "Нормально", "Тяжело" };
            float gap = 6f * unit;
            float width = (area.width - gap * 2f) / 3f;
            for (int i = 0; i < names.Length; i++)
            {
                bool chosen = (int)game.ChosenDifficulty == i;
                var box = new Rect(area.x + i * (width + gap), y, width, 44f * unit);
                quiet.fontSize = Mathf.RoundToInt(13 * unit);
                if (GUI.Button(box, names[i], chosen ? chosenTile : quiet)) game.ChooseDifficulty((Difficulty)i);
                quiet.fontSize = Mathf.RoundToInt(18 * unit);
            }
        }

        void DrawOverlay()
        {
            GameSim sim = game.Sim;
            if (game.Paused)
            {
                DrawPause();
                return;
            }

            bool assault = sim.Mode == GameMode.Assault;
            switch (sim.Phase)
            {
                case GamePhase.LevelCleared:
                    switch (DrawResult(Art.Victory, assault ? "Крепость взята" : "Уровень пройден", sim.LevelName, new Color(1f, 0.9f, 0.66f), "ДАЛЬШЕ", "В МЕНЮ"))
                    {
                        case 0: game.ContinueToNextLevel(); break;
                        case 1: game.ToMenu(); break;
                    }
                    break;
                case GamePhase.Victory when sim.Mode == GameMode.Tutorial:
                    string note = game.TutorialFirstWin ? "+10 деталей за первое прохождение" : "Ты всё помнишь. Удачи на рубеже!";
                    switch (DrawResult(Art.Victory, "Обучение пройдено", note, new Color(1f, 0.9f, 0.66f), "КАМПАНИЯ", "В МЕНЮ"))
                    {
                        case 0:
                            game.ToMenu();
                            game.Open(Page.LevelSelect);
                            break;
                        case 1: game.ToMenu(); break;
                    }
                    break;
                case GamePhase.Victory:
                    string done = assault ? "Все крепости взяты!" : "Рубеж удержан!";
                    string where = assault ? "Вражеские реакторы погашены" : "Все 20 уровней пройдены";
                    switch (DrawResult(Art.Victory, done, where, new Color(1f, 0.9f, 0.66f), assault ? "ВЫЖИВАНИЕ" : "ШТУРМ", "В МЕНЮ"))
                    {
                        case 0:
                            if (assault) game.StartSurvival();
                            else { game.ToMenu(); game.Open(Page.Assault); }
                            break;
                        case 1: game.ToMenu(); break;
                    }
                    break;
                case GamePhase.GameOver:
                    string headline = sim.Lives <= 0 ? "Танки закончились" : "Реактор уничтожен";
                    switch (DrawResult(Art.Defeat, headline, sim.LevelName, new Color(1f, 0.54f, 0.47f), "ЕЩЁ РАЗ", "ГАРАЖ", "В МЕНЮ"))
                    {
                        case 0: game.Retry(); break;
                        case 1:
                            game.ToMenu();
                            game.Open(Page.Garage);
                            break;
                        case 2: game.ToMenu(); break;
                    }
                    break;
                case GamePhase.WaveCleared:
                    DrawWaveChoice();
                    break;
            }
        }

        void DrawPause()
        {
            DrawArt(PauseArt);
            float textWidth = PlateWidth - Pad * 2f;
            string help = "Слева джойстик, справа огонь, внизу способность танка";
            float helpHeight = TextHeight(help, plateText, textWidth);
            float buttons = ColumnHeight(4, ButtonHeight, 10f * unit);
            float inner = helpHeight + 14f * unit + buttons;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - 84f * unit - inner - Pad * 2f) / 2f);

            DrawHeading(y, "Пауза", Paper);
            Rect area = DrawPlate(y + 84f * unit, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, helpHeight), help, plateText);
            switch (ButtonColumn(area, area.y + helpHeight + 14f * unit, new[] { "ПРОДОЛЖИТЬ", "НАСТРОЙКИ", "НАЧАТЬ ЗАНОВО", "В МЕНЮ" }))
            {
                case 0: game.TogglePause(); break;
                case 1: game.Open(Page.Settings); break;
                case 2: game.Retry(); break;
                case 3: game.ToMenu(); break;
            }
        }

        /// <summary>A result screen: headline, where it happened, three numbers, then buttons. Returns the pressed button.</summary>
        int DrawResult(Art picture, string headline, string where, Color colour, params string[] buttons)
        {
            DrawArt(picture);
            float tiles = 62f * unit;
            float buttonsHeight = buttons.Length > 2 ? ColumnHeight(buttons.Length, ButtonHeight, 10f * unit) : ButtonHeight;
            bool won = (game.Sim.Phase == GamePhase.LevelCleared || game.Sim.Phase == GamePhase.Victory) && game.Sim.Mode != GameMode.Tutorial;
            float starsHeight = won ? 62f * unit : 0f;
            float inner = 22f * unit + 10f * unit + starsHeight + tiles + 14f * unit + buttonsHeight;
            float headlineHeight = 90f * unit;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - headlineHeight - inner - Pad * 2f) / 2f);

            DrawHeading(y, headline, colour);
            Rect area = DrawPlate(y + headlineHeight, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, 22f * unit), where, plateText);

            float top = area.y + 32f * unit;
            if (won)
            {
                // Stars won, with what each one was for.
                GameSim sim = game.Sim;
                int fontBefore = valueText.fontSize;
                valueText.fontSize = Mathf.RoundToInt(32 * unit);
                valueText.normal.textColor = new Color(1f, 0.83f, 0.3f);
                GUI.Label(new Rect(area.x, top, area.width, 36f * unit), StarLine(sim.StarsWon), valueText);
                valueText.normal.textColor = Paper;
                valueText.fontSize = fontBefore;
                string why = "Победа" + (sim.LivesLost == 0 ? " · без потерь" : "")
                    + (sim.Time <= sim.ParTime ? " · быстрее " : " · норма ") + Mathf.RoundToInt(sim.ParTime) + " с";
                GUI.Label(new Rect(area.x, top + 36f * unit, area.width, 20f * unit), why, plateNote.alignment == TextAnchor.UpperLeft ? plateText : plateNote);
                top += starsHeight;
            }

            string[] values = { game.Sim.Score.ToString(), game.BestScore.ToString(), "+" + game.LastParts };
            string[] captions = { "ОЧКИ", "РЕКОРД", "ДЕТАЛЕЙ" };
            float gap = 8f * unit;
            float width = (area.width - gap * 2f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var box = new Rect(area.x + i * (width + gap), top, width, tiles);
                GUI.Box(box, GUIContent.none, row);
                GUI.Label(new Rect(box.x, box.y + 8f * unit, box.width, 28f * unit), values[i], valueText);
                GUI.Label(new Rect(box.x, box.y + 38f * unit, box.width, 16f * unit), captions[i], captionText);
            }

            float buttonsY = top + tiles + 14f * unit;
            return buttons.Length > 2 ? ButtonColumn(area, buttonsY, buttons) : ButtonRow(area, buttonsY, buttons[0], buttons[1]);
        }

        void DrawWaveChoice()
        {
            GameSim sim = game.Sim;
            DrawArt(Art.Arena);
            float height = 70f * unit;
            float gap = 10f * unit;
            float inner = 22f * unit + 10f * unit + sim.Choices.Count * height + (sim.Choices.Count - 1) * gap;
            float y = Mathf.Max(SafeRect().y + 8f * unit, (Screen.height - 84f * unit - inner - Pad * 2f) / 2f);

            DrawHeading(y, "Волна " + sim.Wave + " отбита", new Color(1f, 0.9f, 0.66f));
            Rect area = DrawPlate(y + 84f * unit, inner);
            GUI.Label(new Rect(area.x, area.y, area.width, 22f * unit), "+" + game.LastParts + " деталей. Выбери улучшение:", plateText);

            for (int i = 0; i < sim.Choices.Count; i++)
            {
                RunUpgrade upgrade = sim.Choices[i];
                var box = new Rect(area.x, area.y + 32f * unit + i * (height + gap), area.width, height);
                if (GUI.Button(box, GUIContent.none, choice))
                {
                    game.ChooseUpgrade(upgrade);
                    return;
                }
                GUI.Label(new Rect(box.x + 14f * unit, box.y + 10f * unit, box.width - 20f * unit, 26f * unit), UpgradeName(upgrade), label);
                GUI.Label(new Rect(box.x + 14f * unit, box.y + 36f * unit, box.width - 24f * unit, 28f * unit), UpgradeNote(upgrade), plateNote);
            }
        }

        /// <summary>
        /// A panel drawing for nine-slice styles: vertical gradient, cut corners (all four or top-left and bottom-right),
        /// a thin rim, a light line along the top and a darker band along the bottom.
        /// </summary>
        static Texture2D MakePanel(int size, int cut, Color top, Color bottom, Color rim, int rimWidth, bool allCorners,
            float lift, float shade, int shadeRows)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            bool Inside(int x, int y, int inset)
            {
                // y counts from the bottom; fy from the top.
                int fy = size - 1 - y;
                if (x < inset || y < inset || x > size - 1 - inset || y > size - 1 - inset) return false;
                int c = cut + inset / 2;
                if (x + fy < c) return false;                         // top left
                if ((size - 1 - x) + y < c) return false;             // bottom right
                if (allCorners && ((size - 1 - x) + fy < c || x + y < c)) return false;
                return true;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!Inside(x, y, 0))
                    {
                        texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                        continue;
                    }
                    Color colour = Color.Lerp(bottom, top, y / (size - 1f));
                    int fy = size - 1 - y;
                    if (fy < Mathf.Max(1, shadeRows / 2) + rimWidth && lift > 0f) colour = Color.Lerp(colour, new Color(1f, 0.9f, 0.8f, colour.a), lift);
                    if (y < shadeRows && shade > 0f) colour = Color.Lerp(colour, new Color(0.2f, 0.06f, 0.02f, colour.a), shade);
                    if (rim.a > 0f && !Inside(x, y, rimWidth)) colour = Color.Lerp(colour, new Color(rim.r, rim.g, rim.b, Mathf.Max(colour.a, rim.a)), rim.a > 0.5f ? 1f : 0.85f);
                    texture.SetPixel(x, y, colour);
                }
            }
            texture.Apply();
            return texture;
        }

        void Fill(Rect area, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, pixel);
            GUI.color = previous;
        }

        static void DrawCircle(Texture2D texture, Vector2 centre, float radius, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), texture);
            GUI.color = previous;
        }

        static Vector2 ToGui(Vector2 screen)
        {
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        static Texture2D MakeArrow(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture rows start at the bottom, so the point of the arrow is at the top.
                    float rowsFromTop = size - y - 0.5f;
                    float width = rowsFromTop / size * size / 2f;
                    float alpha = Mathf.Clamp01(width - Mathf.Abs(x + 0.5f - size / 2f) + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return texture;
        }

        static Texture2D MakeHeart(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = ((x + 0.5f) / size - 0.5f) * 2.5f;
                    float py = ((y + 0.5f) / size - 0.5f) * 2.5f + 0.1f;
                    float a = px * px + py * py - 1f;
                    float value = a * a * a - px * px * py * py * py;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(-value * size * 0.5f)));
                }
            }
            texture.Apply();
            return texture;
        }

        static Texture2D MakeCircle(int size, bool hollow)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float centre = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / centre;
                    float alpha = Mathf.Clamp01((1f - distance) * centre);
                    if (hollow) alpha *= Mathf.Clamp01((distance - 0.88f) * centre);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
