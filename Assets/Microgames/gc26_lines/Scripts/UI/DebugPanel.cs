using System;
using System.Collections.Generic;
using GC26.Lines.Game;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// The hidden Debug panel (GDD → Debug panel; the prototype's #debug): test tools for our own phones, opened by
    /// 5 quick taps on the version at the bottom of the menu. English only on purpose: only we use it, so its words
    /// are not in <see cref="Texts"/>. A pop-up card a little wider than the others, with two (or three) small buttons
    /// per row in groups. Game tools close it and open the game; the leaderboard and "Forget games" tools keep it
    /// open and say what they did on its last line.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float CardWidth = 88f, CardRadius = 5f, PadTop = 7f, PadSide = 5f, PadBottom = 6f, GlowSize = 8f;
        const float TitleSize = 7f, GroupSize = 3f, GroupTop = 3f, GroupGap = 1.4f, ButtonHeight = 10f, ButtonGap = 2f;
        const float ButtonText = 3.2f, NoteSize = 3.2f, NoteTop = 3f, CloseSize = 10f, CloseInset = 3f;
        public const string Hint = "Game tools open the game in the chosen mode.";

        static readonly (string group, (DebugTool tool, string label)[] buttons)[] Groups =
        {
            ("NEXT MUSHROOM", new[] { (DebugTool.RainbowNext, "Rainbow next"), (DebugTool.FrozenNext, "Frozen next") }),
            ("PUFFBALL", new[] { (DebugTool.AddPuffball, "+1 Puffball"), (DebugTool.StartAt190, "Start at 190") }),
            ("GAME OVER NOW", new[] { (DebugTool.GameOverTop, "Top 10"), (DebugTool.GameOverShort, "Not top 10"), (DebugTool.GameOverZero, "0 points") }),
            ("GAME", new[] { (DebugTool.ClockTo35, "Clock to 0:35"), (DebugTool.ForgetGames, "Forget games") }),
            ("LEADERBOARD", new[] { (DebugTool.ClearLeaderboards, "Clear both"), (DebugTool.FillLeaderboards, "Fill top 10") }),
        };

        RectTransform root, card;
        Image cardImage, cardGlow;
        TextMeshProUGUI title, note;
        PillButton close;
        readonly List<TextMeshProUGUI> groupLabels = new List<TextMeshProUGUI>();
        readonly List<List<PillButton>> rows = new List<List<PillButton>>();
        readonly Dictionary<DebugTool, PillButton> buttons = new Dictionary<DebugTool, PillButton>();
        PopupMotion motion;
        float unit = 10f;

        /// <summary>A tool was pressed (the panel has closed already for game tools).</summary>
        public event Action<DebugTool> ToolChosen;

        public bool IsOpen => motion != null && motion.IsOpen;
        public string NoteText => note.text;
        public PillButton Button(DebugTool tool) => buttons[tool];
        public float CardWidthPixels => card.sizeDelta.x;

        /// <summary>The tools that stay on the menu (the panel stays open and says what happened).</summary>
        public static bool KeepsPanelOpen(DebugTool tool) =>
            tool == DebugTool.ClearLeaderboards || tool == DebugTool.FillLeaderboards || tool == DebugTool.ForgetGames;

        public static DebugPanel Create(RectTransform canvas)
        {
            var go = new GameObject("Debug panel", typeof(RectTransform));
            var panel = go.AddComponent<DebugPanel>();
            panel.Build(canvas);
            go.SetActive(false);
            return panel;
        }

        void Build(RectTransform canvas)
        {
            root = (RectTransform)transform;
            root.SetParent(canvas, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var dim = gameObject.AddComponent<Image>();
            dim.color = Palette.Dim;
            dim.raycastTarget = true; // nothing behind can be tapped
            var outside = gameObject.AddComponent<Button>(); // a tap outside the card closes it, like Settings
            outside.transition = Selectable.Transition.None;
            outside.targetGraphic = dim;
            outside.onClick.AddListener(Close);

            cardGlow = NewRect("Glow", root).gameObject.AddComponent<Image>();
            cardGlow.rectTransform.anchorMin = cardGlow.rectTransform.anchorMax = cardGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            cardGlow.color = Palette.CardGlow;
            cardGlow.raycastTarget = false;
            card = NewRect("Card", root);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = Palette.Card;
            cardImage.raycastTarget = true;
            var inside = card.gameObject.AddComponent<Button>(); // takes taps on the card itself, so only outside closes it
            inside.transition = Selectable.Transition.None;
            inside.targetGraphic = cardImage;

            title = NewText("Title", TextAlignmentOptions.Center);
            UiFonts.UseDisplay(title);
            title.text = "Debug";
            foreach (var (group, tools) in Groups)
            {
                var label = NewText("Group " + group, TextAlignmentOptions.Left);
                UiFonts.UseBold(label);
                label.color = Palette.SoftText;
                label.characterSpacing = 14f;
                label.text = group;
                groupLabels.Add(label);
                var row = new List<PillButton>();
                foreach (var (tool, text) in tools)
                {
                    var b = PillButton.Create(card, text);
                    b.TextUnits = ButtonText;
                    var chosen = tool;
                    b.Clicked += () => Choose(chosen);
                    buttons[tool] = b;
                    row.Add(b);
                }
                rows.Add(row);
            }
            note = NewText("Note", TextAlignmentOptions.Center);
            UiFonts.UseBody(note);
            note.color = Palette.SoftText;
            note.text = Hint;
            close = PillButton.CreateIcon(card, SpriteFactory.CloseIcon(), "Close");
            close.MakePlain();
            close.Clicked += Close;
            motion = new PopupMotion(gameObject, card);
        }

        /// <summary>Lays the card out for a screen where 1 unit is <paramref name="unitPixels"/> pixels.</summary>
        public void ApplyLayout(float unitPixels)
        {
            unit = unitPixels;
            float u = unit;
            float width = CardWidth * u, inner = width - 2f * PadSide * u;
            float titleH = TitleSize * 1.25f * u, groupH = GroupSize * 1.4f * u, noteH = NoteSize * 1.4f * u;
            float height = PadTop * u + titleH + Groups.Length * ((GroupTop + GroupGap) * u + groupH + ButtonHeight * u)
                + NoteTop * u + noteH + PadBottom * u;
            card.sizeDelta = new Vector2(width, height);
            cardGlow.rectTransform.sizeDelta = card.sizeDelta + Vector2.one * 2f * GlowSize * u;
            UiShapes.MakeGlow(cardGlow, GlowSize * u);
            UiShapes.MakeRounded(cardImage, CardRadius * u);

            float y = height - PadTop * u - titleH;
            title.fontSize = TitleSize * u;
            Place(title.rectTransform, PadSide * u, y, inner, titleH);
            for (int g = 0; g < rows.Count; g++)
            {
                y -= GroupTop * u + groupH;
                groupLabels[g].fontSize = GroupSize * u;
                Place(groupLabels[g].rectTransform, PadSide * u, y, inner, groupH);
                y -= GroupGap * u + ButtonHeight * u;
                var row = rows[g];
                float w = (inner - (row.Count - 1) * ButtonGap * u) / row.Count;
                for (int i = 0; i < row.Count; i++)
                    row[i].Place(new Rect(PadSide * u + i * (w + ButtonGap * u), y, w, ButtonHeight * u), u);
            }
            y -= NoteTop * u + noteH;
            note.fontSize = NoteSize * u;
            Place(note.rectTransform, PadSide * u, y, inner, noteH);
            close.Place(new Rect(width - (CloseInset + CloseSize) * u, height - (CloseInset + CloseSize) * u, CloseSize * u, CloseSize * u), u);
        }

        public void Open()
        {
            note.text = Hint;
            transform.SetAsLastSibling();
            motion.Open();
        }

        public void Close() { if (IsOpen) motion.Close(); }

        /// <summary>The last line says what a tool did ("Leaderboards cleared"); null puts the hint back.</summary>
        public void SetStatus(string text) => note.text = string.IsNullOrEmpty(text) ? Hint : text;

        /// <summary>Presses a tool's button (tests too).</summary>
        public void Choose(DebugTool tool)
        {
            if (!IsOpen) return;
            if (!KeepsPanelOpen(tool)) Close();
            ToolChosen?.Invoke(tool);
        }

        RectTransform NewRect(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        TextMeshProUGUI NewText(string name, TextAlignmentOptions align)
        {
            var tmp = NewRect(name, card).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.color = Palette.Text;
            tmp.alignment = align;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
