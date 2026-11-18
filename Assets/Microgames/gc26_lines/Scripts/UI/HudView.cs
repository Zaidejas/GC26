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
        const float LabelSize = 3.2f, ScoreSize = 10f, GainSize = 5f, PieceSize = 8.5f, PieceGap = 1.6f, Inset = 1f;
        static readonly Color GainColor = Palette.Gain;

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
        // L09 Timed mode: the clock at the top, between the house and the gear; Reset alone in the middle.
        public const float ClockSize = 7.5f, ClockIconSize = 5.4f, ClockIconGap = 1.6f, TimeGainSize = 4.6f;
        public static readonly Color ClockWarn = new Color(1f, 0.835f, 0.42f), ClockLow = new Color(1f, 0.42f, 0.42f);
        Image clockIcon;
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
            scoreText = NewText("Score", "0", Palette.Text, TextAlignmentOptions.BottomLeft, bold: true);
            gainText = NewText("Gain", "", GainColor, TextAlignmentOptions.BottomLeft, bold: true);
            gainText.alpha = 0f;
            UiFonts.UseDisplay(scoreText);
            UiFonts.UseDisplay(gainText);
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
            clockIcon = NewRect("Clock icon", (RectTransform)transform).gameObject.AddComponent<Image>();
            clockIcon.sprite = SpriteFactory.StopwatchIcon();
            clockIcon.preserveAspect = true;
            clockIcon.raycastTarget = false;
            clockText = NewText("Clock", "3:00", Palette.Text, TextAlignmentOptions.MidlineLeft, bold: true);
            clockText.rectTransform.SetParent(transform, false);
            UiFonts.UseDisplay(clockText);
            clockText.rectTransform.pivot = new Vector2(0f, 0.5f);
            timeGain = NewText("Time gain", "", GainColor, TextAlignmentOptions.MidlineLeft, bold: true);
            timeGain.rectTransform.SetParent(transform, false);
            UiFonts.UseDisplay(timeGain);
            timeGain.alpha = 0f;
            clockIcon.enabled = clockText.enabled = false; // Classic until SetTimed
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
            back.MakePlain(); // just the house, no ring or disc (teacher's feedback, prototype)
            back.Clicked += () => BackClicked?.Invoke();
            settingsButton = PillButton.CreateIcon((RectTransform)transform, SpriteFactory.GearIcon(), "Settings");
            settingsButton.MakePlain(); // just the white gear, opposite the house
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
            clockIcon.enabled = clockText.enabled = on;
            undo.gameObject.SetActive(!on);
            pause.gameObject.SetActive(on);
            if (!on) { timeGainTween?.Kill(); timeGain.alpha = 0f; }
            if (hasLayout) ApplyLayout(lastLayout);
        }

        public bool ClockShowing => clockText.enabled;
        public string ClockReading => clockText.text;
        public Color ClockColor => clockText.color;
        public bool UndoShowing => undo.gameObject.activeSelf;
        /// <summary>The middle of the clock (icon and time together) and of Reset, in the HUD's pixels (tests).</summary>
        public Vector2 ClockCentre { get; private set; }
        public Vector2 ResetCentre => ((RectTransform)reset.transform).anchoredPosition;

        /// <summary>Shows <paramref name="seconds"/> left: yellow from 0:30, red from 0:10 (while the game runs).</summary>
        public void SetClock(float seconds, bool running)
        {
            clockText.text = MenuView.ClockText(seconds);
            var colour = !running || seconds <= 0f ? Palette.Text
                : seconds <= Core.GameConfig.TimeLow ? ClockLow
                : seconds <= Core.GameConfig.TimeWarn ? ClockWarn : Palette.Text;
            clockText.color = colour;
            clockIcon.color = colour;
        }

        /// <summary>The digits give a small pulse (each second in the last 30).</summary>
        public void PulseClock()
        {
            var t = clockText.rectTransform;
            Tween.Kill(t);
            Tween.To(t, 0.5f, p => t.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, p), Ease.OutQuad)
                .OnKilled = () => { if (t != null) t.localScale = Vector3.one; };
        }

        /// <summary>"+5s" floats up to the right of the clock (prototype: like the points, 0.9 s).</summary>
        public void ShowTimeAdded(float seconds)
        {
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

        // The stopwatch icon and the time, centred together between the house and the gear.
        void PlaceClock(BoardLayout layout)
        {
            float u = layout.Unit;
            clockText.fontSize = ClockSize * u;
            float textW = clockText.GetPreferredValues("0:00").x;
            float total = ClockIconSize * u + ClockIconGap * u + textW;
            var centre = new Vector2(layout.Board.center.x, layout.BackButton.center.y);
            ClockCentre = centre;
            float left = centre.x - total / 2f;
            Place((RectTransform)clockIcon.transform, left, centre.y - ClockIconSize * u / 2f, ClockIconSize * u, ClockIconSize * u);
            var ct = clockText.rectTransform;
            ct.anchoredPosition = new Vector2(left + (ClockIconSize + ClockIconGap) * u, centre.y);
            ct.sizeDelta = new Vector2(textW * 1.4f, ClockSize * 1.3f * u);
            timeGain.fontSize = TimeGainSize * u;
            Place(timeGain.rectTransform, left + total + 2f * u, centre.y - TimeGainSize * 0.65f * u, 20f * u, TimeGainSize * 1.3f * u);
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
            if (name.EndsWith("label")) { UiFonts.UseDisplay(tmp); tmp.characterSpacing = 6f; } // letter-spaced Mystery Quest labels
            return tmp;
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
