using System.Collections.Generic;
using GC26.Lines.Core;
using GC26.Lines.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GC26.Lines.UI
{
    /// <summary>
    /// The strip above the board: SCORE on the left (with a "+N" that floats up after a clear) and
    /// NEXT on the right (the 3 pieces that arrive after the next move). Built from code with uGUI and
    /// TextMeshPro; sizes follow the prototype, in units of 1% of the content width.
    /// The canvas is drawn by the game camera, so it also appears in layout screenshots.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        // Prototype sizes (cqw).
        const float LabelSize = 3f, ScoreSize = 10f, GainSize = 5f, PieceSize = 8.5f, PieceGap = 1.6f, Inset = 1f;
        static readonly Color GainColor = Palette.Points; // points: warm gold (time added is mint, Palette.Bonus)

        Canvas canvas;
        RectTransform area;
        TextMeshProUGUI scoreLabel, scoreText, gainText, nextLabel;
        readonly Image[] next = new Image[GameConfig.BallsPerTurn];
        readonly Image[] nextIce = new Image[GameConfig.BallsPerTurn]; // L15: a frozen next mushroom shows its ice
        float unit = 10f;
        TweenHandle gainTween;
        // L04: the gold crown next to the score above the best, and the "New best!" that pops up over the board.
        public const float CrownSize = 5f, CrownGap = 1.2f, NewBestSize = 8f, NewBestSeconds = 2f, CrownPopSeconds = 0.7f;
        Image crown, crownGlow;
        // L09 Timed mode: the clock just above SCORE / NEXT, its digits centred on the screen, with a fairy ring of
        // 12 spores around a small stopwatch hanging to their left (prototype: .time-stat, .ring).
        public const float ClockSize = 7f, ClockIconSize = 4f, TimeGainSize = 4.6f;
        public const float RingSize = 9.4f, RingGap = 1.6f, SporeSize = 1.25f, SporeRadius = 4f, SporeGlowSize = 3.6f, ClockAboveLabels = 8.2f;
        public const int Spores = 12;
        public static readonly Color ClockWarn = Palette.ClockWarn, ClockLow = Palette.ClockLow;
        Image clockIcon;
        RectTransform ring;
        readonly Image[] spores = new Image[Spores], sporeGlows = new Image[Spores];
        int litSpores = Spores;
        bool clockLive;
        float ringGotUntil = -1f;
        // The last lit spore breathes clearly enough to see on a phone: half again as big and bright, then small and dim.
        public const float RingGotSeconds = 0.6f, BreatheSeconds = 1.1f, BreatheBig = 1.5f, BreatheSmall = 0.75f, BreatheDim = 0.25f;
        TextMeshProUGUI clockText, timeGain;
        TweenHandle timeGainTween;
        bool timed;
        BoardLayout lastLayout;
        bool hasLayout;
        TextMeshProUGUI newBest;
        TweenHandle newBestTween;
        AlertToast alert;
        PillButton undo, reset, pause;
        // L16: the puffball button in the middle of the bottom row, its bubble, and the "A puffball for you!" banner.
        PuffballButton puffball;
        HintBubble bubble;
        RewardBanner banner;
        /// <summary>The player pressed the puffball button.</summary>
        public event System.Action PuffballClicked;
        /// <summary>L18: the player pressed Pause (Timed mode).</summary>
        public event System.Action PauseClicked;
        public PuffballButton Puffball => puffball;
        public HintBubble Bubble => bubble;
        public RewardBanner Banner => banner;
        public PillButton PauseButton => pause;
        public bool PauseShowing => pause.gameObject.activeSelf;
        ConfirmDialog dialog;
        GameOverDialog gameOver;

        /// <summary>The player pressed Reset.</summary>
        public event System.Action ResetClicked;
        public PillButton ResetButton => reset;
        /// <summary>The player pressed the house button (top-left): back to the main menu.</summary>
        public event System.Action BackClicked;
        public PillButton BackButton => back;
        PillButton back, settingsButton;
        SettingsDialog settings;
        /// <summary>The player pressed the gear (top-right): opens Settings.</summary>
        public event System.Action SettingsClicked;
        public PillButton SettingsButton => settingsButton;
        /// <summary>The Settings pop-up (opened from the game screen or the main menu).</summary>
        public SettingsDialog Settings => settings;
        GameObject popups; // own canvas on top, so dialogs also work while the HUD is hidden (main menu)
        /// <summary>The "Start a new game?" question (and later pop-ups).</summary>
        public ConfirmDialog Dialog => dialog;
        public GameOverDialog GameOver => gameOver;

        /// <summary>The player pressed Undo.</summary>
        public event System.Action UndoClicked;
        public PillButton UndoButton => undo;

        public string ScoreText => scoreText.text;
        public IReadOnlyList<Image> NextPieces => next;
        public int Score { get; private set; }

        public static HudView Create(Camera camera)
        {
            var go = new GameObject("HUD");
            var hud = go.AddComponent<HudView>();
            hud.Build(camera);
            return hud;
        }

        void Build(Camera camera)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 20; // in front of the board
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // we size everything in pixels ourselves
            gameObject.AddComponent<GraphicRaycaster>();

            area = NewRect("Area", (RectTransform)transform);
            scoreLabel = NewText("Score label", "", Palette.SoftText, TextAlignmentOptions.BottomLeft, bold: true);
            scoreText = NewText("Score", "0", Color.white, TextAlignmentOptions.BottomLeft, bold: true);
            gainText = NewText("Gain", "", GainColor, TextAlignmentOptions.BottomLeft, bold: true);
            gainText.alpha = 0f;
            UiFonts.UseDisplay(scoreText);
            UiFonts.UseDisplay(gainText);
            // Points are gold: the score's letters have the title's gold gradient (GDD → Art → Colours).
            scoreText.enableVertexGradient = true;
            scoreText.colorGradient = new VertexGradient(Palette.ScoreTop, Palette.ScoreTop, Palette.ScoreBottom, Palette.ScoreBottom);
            nextLabel = NewText("Next label", "", Palette.SoftText, TextAlignmentOptions.BottomRight, bold: true);
            crownGlow = NewRect("Crown glow", area).gameObject.AddComponent<Image>();
            crownGlow.sprite = SpriteFactory.SoftDisc();
            crownGlow.color = new Color(1f, 0.8f, 0.35f, 0.55f);
            crownGlow.raycastTarget = false;
            crown = NewRect("Crown", area).gameObject.AddComponent<Image>();
            crown.sprite = SpriteFactory.Crown();
            crown.preserveAspect = true;
            crown.raycastTarget = false;
            ((RectTransform)crown.transform).pivot = ((RectTransform)crownGlow.transform).pivot = new Vector2(0.5f, 0.5f);
            SetCrown(false);
            ring = NewRect("Spore ring", (RectTransform)transform);
            ring.pivot = new Vector2(0.5f, 0.5f);
            for (int k = 0; k < Spores; k++)
            {
                sporeGlows[k] = NewRect("Spore glow " + k, ring).gameObject.AddComponent<Image>();
                sporeGlows[k].sprite = SpriteFactory.SoftDisc();
                sporeGlows[k].raycastTarget = false;
                spores[k] = NewRect("Spore " + k, ring).gameObject.AddComponent<Image>();
                spores[k].raycastTarget = false;
                ((RectTransform)spores[k].transform).pivot = ((RectTransform)sporeGlows[k].transform).pivot = new Vector2(0.5f, 0.5f);
            }
            clockIcon = NewRect("Clock icon", ring).gameObject.AddComponent<Image>();
            clockIcon.sprite = SpriteFactory.StopwatchIcon();
            clockIcon.preserveAspect = true;
            clockIcon.raycastTarget = false;
            ((RectTransform)clockIcon.transform).pivot = new Vector2(0.5f, 0.5f);
            // Centred in its box with the pivot in the middle, so the digits' pulse grows evenly from their middle
            // (from the left edge it pushed the numbers sideways on every beat). Vertically "Center" places the line
            // by the font's fixed line height; "Midline" centred each time's own ink, and Mystery Quest's digits
            // differ in height (some hang below the line), so the time jumped up and down as it changed.
            clockText = NewText("Clock", "3:00", Palette.Clock, TextAlignmentOptions.Center, bold: true);
            clockText.rectTransform.SetParent(transform, false);
            UiFonts.UseDisplay(clockText);
            clockText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            timeGain = NewText("Time gain", "", Palette.Bonus, TextAlignmentOptions.MidlineLeft, bold: true);
            timeGain.rectTransform.SetParent(transform, false);
            UiFonts.UseDisplay(timeGain);
            timeGain.alpha = 0f;
            clockText.enabled = false; // Classic until SetTimed
            ring.gameObject.SetActive(false);
            newBest = NewText("New best", "", Color.white, TextAlignmentOptions.Center, bold: true);
            newBest.rectTransform.SetParent(transform, false); // over the board, not inside the score area
            UiFonts.UseDisplay(newBest);
            newBest.enableVertexGradient = true; // the gold of the game's title: light at the top, orange below
            newBest.colorGradient = new VertexGradient(new Color(1f, 0.95f, 0.76f), new Color(1f, 0.95f, 0.76f), new Color(1f, 0.6f, 0.24f), new Color(1f, 0.6f, 0.24f));
            newBest.alpha = 0f;
            alert = AlertToast.Create((RectTransform)transform);
            undo = PillButton.Create((RectTransform)transform, "");
            undo.SetLeadingIcon(SpriteFactory.UndoIcon());
            undo.Interactable = false; // nothing to undo at the start
            undo.Clicked += () => UndoClicked?.Invoke();
            reset = PillButton.Create((RectTransform)transform, "");
            reset.SetLeadingIcon(SpriteFactory.ResetIcon());
            reset.SetDanger(filled: false); // ember red: starting over is more serious than Undo or Pause
            reset.Clicked += () => ResetClicked?.Invoke();
            pause = PillButton.Create((RectTransform)transform, "");
            pause.SetLeadingIcon(SpriteFactory.PauseIcon());
            pause.Clicked += () => PauseClicked?.Invoke();
            pause.gameObject.SetActive(false); // Timed mode only, in Undo's place
            puffball = PuffballButton.Create((RectTransform)transform);
            puffball.Clicked += () => PuffballClicked?.Invoke();
            bubble = HintBubble.Create((RectTransform)transform);
            banner = RewardBanner.Create((RectTransform)transform);
            back = PillButton.CreateIcon((RectTransform)transform, SpriteFactory.HomeIcon(), "Home");
            back.MakeNav(PillButton.HouseIconSize); // just the blue-grey house, no ring or disc (teacher's feedback, prototype)
            back.Clicked += () => BackClicked?.Invoke();
            settingsButton = PillButton.CreateIcon((RectTransform)transform, SpriteFactory.GearIcon(), "Settings");
            settingsButton.MakeNav(PillButton.GearIconSize); // just the blue-grey gear, opposite the house
            settingsButton.Clicked += () => SettingsClicked?.Invoke();

            popups = new GameObject("Popups");
            var popupCanvas = popups.AddComponent<Canvas>();
            popupCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            popupCanvas.worldCamera = camera;
            popupCanvas.planeDistance = 4f;
            popupCanvas.sortingOrder = 30;
            popups.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            popups.AddComponent<GraphicRaycaster>();
            gameOver = GameOverDialog.Create((RectTransform)popups.transform);
            dialog = ConfirmDialog.Create((RectTransform)popups.transform);
            settings = SettingsDialog.Create((RectTransform)popups.transform);
            for (int i = 0; i < next.Length; i++)
            {
                next[i] = NewRect("Next " + i, area).gameObject.AddComponent<Image>();
                next[i].preserveAspect = true;
                next[i].raycastTarget = false;
                nextIce[i] = NewRect("Ice", next[i].rectTransform).gameObject.AddComponent<Image>();
                nextIce[i].sprite = SpecialArt.Ice();
                nextIce[i].preserveAspect = true;
                nextIce[i].raycastTarget = false;
                nextIce[i].enabled = false;
            }
            RefreshTexts();
        }

        /// <summary>Puts the HUD's texts and its pop-ups' texts in the current language (<see cref="Texts"/>).</summary>
        public void RefreshTexts()
        {
            scoreLabel.text = Texts.Get("score");
            nextLabel.text = Texts.Get("next");
            undo.SetText(Texts.Get("undo"));
            reset.SetText(Texts.Get("reset"));
            pause.SetText(Texts.Get("pause"));
            dialog.RefreshTexts();
            gameOver.RefreshTexts();
            settings.RefreshTexts();
            if (unit > 0f && area != null && area.sizeDelta.x > 0f) AlignScore();
        }

        /// <summary>Places the HUD in <see cref="BoardLayout.Hud"/> (screen pixels, origin bottom-left).</summary>
        public void ApplyLayout(BoardLayout layout)
        {
            unit = layout.Unit;
            lastLayout = layout; hasLayout = true;
            var rect = layout.Hud;
            Place(area, rect.x, rect.y, rect.width, rect.height);

            float u = unit;
            Place(scoreText.rectTransform, Inset * u, 0, 50 * u, ScoreSize * 1.1f * u);
            scoreText.fontSize = ScoreSize * u;
            Place(scoreLabel.rectTransform, Inset * u, ScoreSize * 1.1f * u, 40 * u, LabelSize * 1.4f * u);
            scoreLabel.fontSize = LabelSize * u;
            AlignScore();
            gainText.fontSize = GainSize * u;
            PlaceGain();

            float right = rect.width - Inset * u;
            for (int i = 0; i < next.Length; i++)
            {
                float x = right - (next.Length - i) * PieceSize * u - (next.Length - 1 - i) * PieceGap * u;
                Place(next[i].rectTransform, x, 0, PieceSize * u, PieceSize * u);
                // The ice cube is 14 % bigger than the mushroom (prototype: inset −7 %).
                Place(nextIce[i].rectTransform, -0.07f * PieceSize * u, -0.07f * PieceSize * u, 1.14f * PieceSize * u, 1.14f * PieceSize * u);
            }
            Place(nextLabel.rectTransform, right - 40 * u, (PieceSize + 1.4f) * u, 40 * u, LabelSize * 1.4f * u);
            nextLabel.fontSize = LabelSize * u;
            AlignScore(); // puts NEXT on the same line as SCORE
            PlaceCrown();

            alert.ApplyLayout(layout.Board.center, u); // messages appear over the middle of the board
            // "New best!" a little above the board's middle (the prototype's 38 % of the screen from the top).
            var nb = newBest.rectTransform;
            nb.pivot = new Vector2(0.5f, 0.5f);
            Place(nb, layout.Board.center.x, layout.Board.center.y + layout.Board.height * 0.12f, 90f * u, NewBestSize * 1.4f * u);
            newBest.fontSize = NewBestSize * u;
            back.Place(layout.BackButton, u);
            settingsButton.Place(layout.SettingsButton, u);

            // Buttons under the board (L16, L18): Undo (Pause in Timed), the round puffball, Reset; 3 units apart.
            var actions = layout.Actions;
            float round = PuffballButton.Size * u, gap = 3f * u;
            float side = (actions.width - round - 2f * gap) / 2f;
            undo.Place(new Rect(actions.x, actions.y, side, actions.height), u);
            pause.Place(new Rect(actions.x, actions.y, side, actions.height), u);
            reset.Place(new Rect(actions.xMax - side, actions.y, side, actions.height), u);
            var puffballCentre = new Vector2(actions.center.x, actions.center.y);
            puffball.Place(puffballCentre, u);
            bubble.ApplyLayout(puffballCentre + new Vector2(0f, round / 2f), u);
            // The banner a little above the board's middle (prototype: 30 % down the screen).
            banner.ApplyLayout(new Vector2(layout.Board.center.x, layout.Board.center.y + layout.Board.height * 0.2f), u);
            PlaceClock(layout);
            dialog.ApplyLayout(u);
            gameOver.ApplyLayout(u);
            settings.ApplyLayout(u);
        }

        /// <summary>Shows or hides the game's HUD (score, next, buttons). Dialogs are not affected.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        void OnDestroy()
        {
            if (popups == null) return;
            if (Application.isPlaying) Destroy(popups);
            else DestroyImmediate(popups);
        }

        /// <summary>Enables or greys out the Undo button.</summary>
        public void SetUndoEnabled(bool enabled) => undo.Interactable = enabled;

        /// <summary>Shows a short message over the board ("No path"); <paramref name="secondLine"/>: a smaller line under it.</summary>
        public void ShowAlert(string text, string secondLine = null) => alert.Show(text, secondLine);

        /// <summary>L16: the puffballs in stock and how far the next one is (0–1).</summary>
        public void SetPuffballs(int ready, float progress) => puffball.Set(ready, progress);

        /// <summary>L18: Pause and, in Timed mode, Settings hide the NEXT mushrooms (the board hides them on its side).</summary>
        public void SetNextHidden(bool hidden)
        {
            foreach (var image in next) image.gameObject.SetActive(!hidden);
        }

        public AlertToast Alert => alert;

        /// <summary>Shows <paramref name="score"/> without the "+N" effect.</summary>
        public void SetScore(int score)
        {
            Score = score;
            scoreText.text = score.ToString();
            AlignScore();
            PlaceGain();
            PlaceCrown();
        }

        // The score number stands on the same line as the 3 Next mushrooms: the middle of its digits (their ink,
        // not the text box, which has empty room above and below the digits) is level with the middle of the
        // mushrooms' pictures, whatever the score and the screen.
        void AlignScore()
        {
            if (unit <= 0f) return;
            var rt = scoreText.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 0f);
            scoreText.ForceMeshUpdate();
            if (!DigitInk(out float inkBottom, out float inkTop)) return;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, PieceSize * unit / 2f - (inkBottom + inkTop) / 2f);
            // The SCORE label stays right above the number, and NEXT right above the pieces; both share one line
            // (the higher of the two), so neither comes closer to what is under it.
            float labelsY = Mathf.Max(rt.anchoredPosition.y + ScoreSize * 1.1f * unit, (PieceSize + 1.4f) * unit);
            var label = scoreLabel.rectTransform;
            label.anchoredPosition = new Vector2(label.anchoredPosition.x, labelsY);
            var nextRect = nextLabel.rectTransform;
            nextRect.anchoredPosition = new Vector2(nextRect.anchoredPosition.x, labelsY);
        }

        // The top and bottom of the drawn digits, in the score text's own pixels (pivot at its bottom-left).
        bool DigitInk(out float bottom, out float top)
        {
            bottom = float.MaxValue; top = float.MinValue;
            var info = scoreText.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                bottom = Mathf.Min(bottom, c.bottomLeft.y);
                top = Mathf.Max(top, c.topRight.y);
            }
            return top > bottom;
        }

        /// <summary>The middle of the score's digits and of the Next mushrooms, in the HUD's pixels (tests).</summary>
        public float ScoreMiddleY => DigitInk(out float bottom, out float top) ? scoreText.rectTransform.anchoredPosition.y + (bottom + top) / 2f : 0f;
        public float NextMiddleY => next[0].rectTransform.anchoredPosition.y + next[0].rectTransform.sizeDelta.y / 2f;

        /// <summary>Where the SCORE and NEXT labels sit (their bottom edge, in the HUD's pixels; tests).</summary>
        public float ScoreLabelY => scoreLabel.rectTransform.anchoredPosition.y;
        public float NextLabelY => nextLabel.rectTransform.anchoredPosition.y;

        /// <summary>Adds <paramref name="points"/> and floats "+points" up next to the score (prototype: 0.9 s).</summary>
        public void AddScore(int points)
        {
            SetScore(Score + points);
            gainText.text = "+" + points;
            gainTween?.Kill();
            var start = gainText.rectTransform.anchoredPosition;
            float rise = 6f * unit;
            gainTween = Tween.To(gainText, 0.9f, p =>
            {
                gainText.alpha = p < 0.2f ? p / 0.2f : 1f - (p - 0.2f) / 0.8f;
                gainText.rectTransform.anchoredPosition = start + new Vector2(0, -2f * unit + rise * p);
            }).OnComplete(() => gainText.rectTransform.anchoredPosition = start);
        }

        /// <summary>Shows the colors of the next pieces, drawn with <paramref name="pieces"/>; <paramref name="frozen"/>: L15.</summary>
        public void SetPreview(IReadOnlyList<BallColor> colors, IReadOnlyList<bool> frozen, PieceSet pieces)
        {
            for (int i = 0; i < next.Length; i++)
            {
                bool show = i < colors.Count;
                next[i].enabled = show;
                if (show) next[i].sprite = pieces.SpriteFor(colors[i]);
                bool ice = show && frozen != null && i < frozen.Count && frozen[i];
                nextIce[i].enabled = ice;
                next[i].color = ice ? new Color(0.86f, 0.95f, 1f) : Color.white;
            }
        }

        public void SetPreview(IReadOnlyList<BallColor> colors, PieceSet pieces) => SetPreview(colors, null, pieces);

        /// <summary>Which NEXT pieces show ice (tests).</summary>
        public bool NextFrozen(int index) => nextIce[index].enabled;

        /// <summary>Whether the crown is shown next to the score (tests).</summary>
        public bool CrownShowing => crown.enabled;
        /// <summary>The crown's middle and size, in the HUD's pixels (tests).</summary>
        public Vector2 CrownCentre => ((RectTransform)crown.transform).anchoredPosition;
        public float CrownHeight => ((RectTransform)crown.transform).sizeDelta.y;
        /// <summary>The "New best!" text and whether it is on screen now (tests).</summary>
        public string NewBestText => newBest.text;
        public bool NewBestShowing => newBest.alpha > 0.01f;

        /// <summary>Shows or hides the crown; <paramref name="pop"/> makes it pop up with a little twist.</summary>
        public void SetCrown(bool show, bool pop = false)
        {
            crown.enabled = crownGlow.enabled = show;
            Tween.Kill(crown.transform);
            crown.transform.localScale = Vector3.one;
            crown.transform.localRotation = Quaternion.identity;
            if (!show || !pop) return;
            var t = crown.transform;
            Tween.To(t, CrownPopSeconds, p =>
            {
                t.localScale = Vector3.one * p; // with OutBack: a little too big, then settles
                t.localRotation = Quaternion.Euler(0f, 0f, 25f * (1f - p));
            }, Ease.OutBack);
        }

        // ---- L09: the clock ----

        /// <summary>Timed mode on the game screen: the clock shows, and Pause takes Undo's place (L18).</summary>
        public void SetTimed(bool on)
        {
            timed = on;
            clockText.enabled = on;
            ring.gameObject.SetActive(on);
            undo.gameObject.SetActive(!on);
            pause.gameObject.SetActive(on);
            if (!on) { timeGainTween?.Kill(); timeGain.alpha = 0f; }
            if (hasLayout) ApplyLayout(lastLayout);
        }

        public bool ClockShowing => clockText.enabled;
        public string ClockReading => MenuView.ClockText(clockSeconds); // the plain time, e.g. "2:50"
        public Color ClockColor => clockText.color;
        /// <summary>
        /// Where the time's last digit slot ends (tests: the same for every time with the same number of digits,
        /// so nothing shifts as the numbers change).
        /// </summary>
        public float ClockTextWidth
        {
            get
            {
                clockText.ForceMeshUpdate();
                var info = clockText.textInfo;
                return info.characterCount > 0 ? info.characterInfo[info.characterCount - 1].xAdvance : 0f;
            }
        }
        public bool UndoShowing => undo.gameObject.activeSelf;
        /// <summary>The middle of the clock's digits and of Reset, in the HUD's pixels (tests).</summary>
        public Vector2 ClockCentre { get; private set; }
        public Vector2 ResetCentre => ((RectTransform)reset.transform).anchoredPosition;

        /// <summary>
        /// Shows <paramref name="seconds"/> left: cool white, amber from 0:30, red from 0:10 (while the game runs).
        /// The spores go dark one by one, one every 15 s (all lit at 3:00 and above); the last one still lit breathes.
        /// </summary>
        public void SetClock(float seconds, bool running)
        {
            clockSeconds = seconds;
            clockText.text = ClockMarkup(seconds);
            clockLive = running && seconds > 0f;
            var colour = !clockLive ? Palette.Clock
                : seconds <= Core.GameConfig.TimeLow ? ClockLow
                : seconds <= Core.GameConfig.TimeWarn ? ClockWarn : Palette.Clock;
            clockText.color = colour;
            clockIcon.color = colour;
            litSpores = SporesLit(seconds);
            PaintSpores();
        }

        /// <summary>How many of the 12 spores still glow with <paramref name="seconds"/> left (prototype: ceil(share × 12)).</summary>
        public static int SporesLit(float seconds) =>
            Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(1f, Mathf.Max(0f, seconds) / Core.GameConfig.TimedStart) * Spores - 1e-4f), 0, Spores);

        /// <summary>The spores lit now (tests).</summary>
        public int SporesLitNow => litSpores;
        /// <summary>The ring flashes mint after time is added (tests).</summary>
        public bool RingFlashing => Time.unscaledTime < ringGotUntil;
        /// <summary>The middle of the spore ring, in the HUD's pixels (tests).</summary>
        public Vector2 RingCentre => ring.anchoredPosition;

        void PaintSpores()
        {
            bool got = RingFlashing;
            var colour = got ? Palette.Bonus : clockText.color;
            for (int k = 0; k < Spores; k++)
            {
                bool on = k < litSpores;
                spores[k].color = new Color(colour.r, colour.g, colour.b, on ? 1f : 0.16f);
                sporeGlows[k].enabled = on;
                sporeGlows[k].color = new Color(colour.r, colour.g, colour.b, 0.55f);
            }
        }

        // The last spore still lit breathes while the clock runs; the ring pops after time is added (no garbage).
        void Update()
        {
            if (!timed || ring == null || !ring.gameObject.activeInHierarchy) return;
            float now = Time.unscaledTime;
            float popLeft = ringGotUntil - now;
            float pop = popLeft > 0f ? 1f + 0.2f * popLeft / RingGotSeconds : 1f; // 1.2 → 1
            ring.localScale = Vector3.one * pop;
            if (popLeft > -0.1f) PaintSpores(); // the mint flash ends with the pop
            int last = litSpores - 1;
            for (int k = 0; k < Spores; k++)
            {
                float s = 1f, a = 1f;
                if (k == last && clockLive)
                {
                    float p = Mathf.PingPong(now / BreatheSeconds, 1f);
                    p = p * p * (3f - 2f * p); // ease in and out
                    s = Mathf.Lerp(BreatheBig, BreatheSmall, p);
                    a = Mathf.Lerp(1f, BreatheDim, p);
                }
                spores[k].rectTransform.localScale = sporeGlows[k].rectTransform.localScale = Vector3.one * s;
                if (k < litSpores)
                {
                    var c = spores[k].color; c.a = a; spores[k].color = c;
                    var g = sporeGlows[k].color; g.a = 0.55f * a; sporeGlows[k].color = g; // its glow breathes too
                }
            }
        }

        /// <summary>The digits give a small pulse (each second in the last 30).</summary>
        public void PulseClock()
        {
            var t = clockText.rectTransform;
            Tween.Kill(t);
            Tween.To(t, 0.5f, p => t.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, p), Ease.OutQuad)
                .OnKilled = () => { if (t != null) t.localScale = Vector3.one; };
        }

        /// <summary>"+5s" floats up to the right of the clock in mint, and the spores flash mint (prototype: 0.9 s).</summary>
        public void ShowTimeAdded(float seconds)
        {
            ringGotUntil = Time.unscaledTime + RingGotSeconds;
            PaintSpores();
            timeGain.text = "+" + Mathf.RoundToInt(seconds) + Texts.Get("secondsShort");
            timeGainTween?.Kill();
            var rt = timeGain.rectTransform;
            var start = rt.anchoredPosition;
            float rise = 6f * unit;
            timeGainTween = Tween.To(timeGain, 0.9f, p =>
            {
                timeGain.alpha = p < 0.2f ? p / 0.2f : 1f - (p - 0.2f) / 0.8f;
                rt.anchoredPosition = start + new Vector2(0, -2f * unit + rise * p);
            }).OnComplete(() => rt.anchoredPosition = start);
            timeGainTween.OnKilled = () => { rt.anchoredPosition = start; timeGain.alpha = 0f; };
        }

        public bool TimeAddedShowing => timeGain.alpha > 0.01f;

        // Every digit gets the width of the widest one (like a stopwatch's display), so the time doesn't
        // shift as the numbers change: the font's own digits have different widths ("1" is narrow).
        // The strings are made once per layout and reused, so the clock makes no garbage while it runs.
        float clockSeconds = Core.GameConfig.TimedStart;
        string[] clockStrings;
        string digitSpace;

        string ClockMarkup(float seconds)
        {
            int s = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, seconds) - 1e-4f), 0, 5999);
            if (clockStrings == null) clockStrings = new string[600];
            if (s >= clockStrings.Length || digitSpace == null) return MenuView.ClockText(seconds);
            return clockStrings[s] ?? (clockStrings[s] = Monospaced(MenuView.ClockText(s)));
        }

        string Monospaced(string time)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in time)
                b.Append(char.IsDigit(c) ? "<mspace=" + digitSpace + ">" + c + "</mspace>" : c.ToString());
            return b.ToString();
        }

        // The clock sits just above the SCORE / NEXT labels with its digits centred on the screen (the eye centres
        // on the digits); the spore ring hangs to their left and "+5s" floats up to their right. It never goes
        // above the house and gear's line, so on the shortest screens it shares their line, between them.
        void PlaceClock(BoardLayout layout)
        {
            float u = layout.Unit;
            clockText.fontSize = ClockSize * u;
            float widest = 0f;
            for (char c = '0'; c <= '9'; c++) widest = Mathf.Max(widest, clockText.GetPreferredValues(c.ToString()).x);
            string space = (widest / clockText.fontSize).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "em";
            if (space != digitSpace) { digitSpace = space; clockStrings = null; }
            float textW = clockText.GetPreferredValues(Monospaced("0:00")).x;
            float labelsTop = layout.Hud.y + scoreLabel.rectTransform.anchoredPosition.y + LabelSize * 1.4f * u;
            float y = Mathf.Min(labelsTop + ClockAboveLabels * u, layout.BackButton.center.y);
            var centre = new Vector2(layout.Board.center.x, y);
            ClockCentre = centre;
            float left = centre.x - textW / 2f;
            var ct = clockText.rectTransform;
            ct.anchoredPosition = centre;
            ct.sizeDelta = new Vector2(textW * 1.4f, ClockSize * 1.3f * u);
            if (clockText.enabled) clockText.text = ClockMarkup(clockSeconds);
            // The ring: 12 spores clockwise from the top around a small stopwatch.
            ring.anchoredPosition = new Vector2(left - RingGap * u - RingSize * u / 2f, centre.y);
            ring.sizeDelta = Vector2.one * RingSize * u;
            ((RectTransform)clockIcon.transform).anchoredPosition = Vector2.one * RingSize * u / 2f;
            ((RectTransform)clockIcon.transform).sizeDelta = Vector2.one * ClockIconSize * u;
            for (int k = 0; k < Spores; k++)
            {
                float a = k * Mathf.PI * 2f / Spores;
                var at = Vector2.one * RingSize * u / 2f + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * SporeRadius * u;
                var s = (RectTransform)spores[k].transform;
                s.anchoredPosition = at;
                s.sizeDelta = Vector2.one * SporeSize * u;
                UiShapes.MakeDisc(spores[k], SporeSize * u);
                var g = (RectTransform)sporeGlows[k].transform;
                g.anchoredPosition = at;
                g.sizeDelta = Vector2.one * SporeGlowSize * u;
            }
            timeGain.fontSize = TimeGainSize * u;
            Place(timeGain.rectTransform, left + textW + 2f * u, centre.y - TimeGainSize * 0.65f * u, 20f * u, TimeGainSize * 1.3f * u);
        }

        /// <summary>"Time's up!" over the board, the same way as "New best!".</summary>
        public void ShowTimeUp() => ShowBanner(Texts.Get("timeUp"));

        /// <summary>"New best!" pops up over the board, stays a moment, then rises a little and fades (2 s).</summary>
        public void ShowNewBest() => ShowBanner(Texts.Get("newBestShort"));

        void ShowBanner(string text)
        {
            newBest.text = text;
            newBestTween?.Kill();
            var rt = newBest.rectTransform;
            var home = rt.anchoredPosition;
            float rise = NewBestSize * 0.1f * 1.4f * unit;
            newBestTween = Tween.To(newBest, NewBestSeconds, p =>
            {
                newBest.alpha = p < 0.15f ? p / 0.15f : p < 0.75f ? 1f : 1f - (p - 0.75f) / 0.25f;
                float size = p < 0.15f ? Mathf.Lerp(0.6f, 1.08f, p / 0.15f) : p < 0.25f ? Mathf.Lerp(1.08f, 1f, (p - 0.15f) / 0.1f) : 1f;
                rt.localScale = Vector3.one * size;
                rt.anchoredPosition = home + Vector2.up * (p > 0.75f ? rise * (p - 0.75f) / 0.25f : 0f);
            }).OnComplete(() => { rt.anchoredPosition = home; newBest.alpha = 0f; });
            newBestTween.OnKilled = () => { rt.anchoredPosition = home; rt.localScale = Vector3.one; };
        }

        /// <summary>Hides "New best!" at once (a new game starts while it still shows).</summary>
        public void StopNewBest()
        {
            newBestTween?.Kill();
            newBest.alpha = 0f;
        }

        // The crown sits just right of the digits, its middle level with the middle of the digits.
        void PlaceCrown()
        {
            if (crown == null || unit <= 0f || !DigitInk(out float inkBottom, out float inkTop)) return;
            var score = scoreText.rectTransform;
            float x = score.anchoredPosition.x + scoreText.GetPreferredValues(scoreText.text).x + CrownGap * unit + CrownSize * unit / 2f;
            var centre = new Vector2(x, score.anchoredPosition.y + (inkBottom + inkTop) / 2f);
            Place((RectTransform)crown.transform, centre.x, centre.y, CrownSize * unit, CrownSize * unit);
            Place((RectTransform)crownGlow.transform, centre.x, centre.y, CrownSize * 2f * unit, CrownSize * 2f * unit);
        }

        void PlaceGain()
        {
            if (gainText == null) return;
            float x = Inset * unit + scoreText.GetPreferredValues(scoreText.text).x + 2f * unit;
            Place(gainText.rectTransform, x, 1.5f * unit, 20 * unit, GainSize * 1.3f * unit);
        }

        // ---- building blocks ----

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; // measured from the bottom-left corner
            return rt;
        }

        TextMeshProUGUI NewText(string name, string text, Color color, TextAlignmentOptions alignment, bool bold)
        {
            var tmp = NewRect(name, area).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            UiFonts.UseBody(tmp);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            if (name.EndsWith("label")) { UiFonts.UseBold(tmp); tmp.characterSpacing = 14f; } // letter-spaced Nunito ExtraBold labels (0.14em)
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
