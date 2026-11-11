using System;
using System.Collections;
using System.Collections.Generic;
using GC26.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace GC26._2048
{
    /// <summary>
    /// «Мышкин сыр» — the single IMicrogame in gc26_2048_Main. Builds its whole UI from code under this object,
    /// so everything it creates lives in the game scene and goes away when the shell unloads it.
    /// </summary>
    public class Game2048 : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_2048";

        const string Pause = "pause", Settings = "settings", Rules = "rules", Confirm = "confirm", Results = "results";

        Action<int> _exit;
        bool _started;

        CheeseArt art;
        CheeseStorage storage;
        CheeseSession session;
        CheeseAudio sound;
        BoardView boardView;

        RectTransform frame, titleScreen, gameScreen, overlay;
        readonly Dictionary<string, RectTransform> sheets = new Dictionary<string, RectTransform>();

        TextMeshProUGUI scoreText, bestText, movesText, holeText;
        Image movesCard;
        RectTransform scoreCard, holeCard;

        Button continueButton, playButton, exitButton, soundButton;
        TextMeshProUGUI menuBestText, menuFindText;
        Image menuFindIcon;

        TextMeshProUGUI resultTitle, resultScore, resultBest, resultFind, resultHole, resultMoves;
        Image resultFindIcon;
        GameObject resultRecord;

        string settingsReturn, rulesReturn, confirmReturn;
        bool playing;
        Direction? queued;
        int recordAtStart;
        int lastScore;

        // ---------------- lifecycle and shell contract ----------------

        void Awake()
        {
            art = new CheeseArt(Id);
            storage = new CheeseStorage(Id);
            session = new CheeseSession(Environment.TickCount);
            sound = gameObject.AddComponent<CheeseAudio>();
            sound.Enabled = storage.Sound;
            EnsureEventSystem();
            Build();
            ShowTitle();
        }

        void OnDestroy() => art?.Dispose();

        public void Begin(Action<int> exit)
        {
            _started = true;
            _exit = exit;
            exitButton.gameObject.SetActive(MicrogameRunner.IsRunning);
        }

        void Start()
        {
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
            {
                EnsureCamera();
                Begin(score => Debug.Log($"[{Id}] exit, score = {score}"));
            }
        }

        /// <summary>Saves and hands control back to the shell menu.</summary>
        public void ExitToMenu(int score)
        {
            if (playing) storage.SaveGame(session);
            var exit = _exit;
            _exit = null;
            exit?.Invoke(score);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && playing) storage.SaveGame(session);
        }

        void OnApplicationQuit()
        {
            if (playing) storage.SaveGame(session);
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Camera");
            go.transform.SetParent(transform, false);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CheeseArt.Page;
            cam.orthographic = true;
        }

        // ---------------- input ----------------

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Back();
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) TryMove(Direction.Left);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) TryMove(Direction.Right);
            else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) TryMove(Direction.Up);
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) TryMove(Direction.Down);
#endif
        }

        /// <summary>Esc / Android back: opens the game menu, closes a window, or leaves from the title screen.</summary>
        void Back()
        {
            if (overlay.gameObject.activeSelf)
            {
                if (sheets[Pause].gameObject.activeSelf) Show(null);
                else if (sheets[Settings].gameObject.activeSelf) Show(settingsReturn);
                else if (sheets[Rules].gameObject.activeSelf) Show(rulesReturn);
                else if (sheets[Confirm].gameObject.activeSelf) Show(confirmReturn);
                return;
            }
            if (gameScreen.gameObject.activeSelf && playing) Show(Pause);
            else if (titleScreen.gameObject.activeSelf && MicrogameRunner.IsRunning) ExitToMenu(lastScore);
        }

        void TryMove(Direction dir)
        {
            if (!playing || session.IsOver || overlay.gameObject.activeSelf || !gameScreen.gameObject.activeSelf) return;
            if (boardView.Busy)
            {
                queued = dir;
                return;
            }
            var result = session.Move(dir);
            if (!result.Changed) return;
            lastScore = session.Score;
            sound.Play(CheeseSound.Rustle);
            boardView.Play(result, () => AfterMove(result));
        }

        void AfterMove(MoveResult r)
        {
            if (r.Cheeses > 0)
            {
                sound.Play(CheeseSound.Fanfare);
                StartCoroutine(Bump(holeCard));
            }
            else if (r.TopLevel == 3) sound.Play(CheeseSound.Moo);
            else if (r.TopLevel > 0) sound.Play(CheeseSound.Chime, r.TopLevel);

            if (r.FirstCheese) StartCoroutine(Toast("Сыр готов!"));
            if (r.Points > 0) StartCoroutine(FloatPoints(r.Points));
            if (session.Score > storage.Best) storage.Best = session.Score;
            if (session.BestLevel > storage.BestLevel) storage.BestLevel = session.BestLevel;
            if (session.MovesLeft > 0 && session.MovesLeft <= CheeseRules.WarnMovesLeft) sound.Play(CheeseSound.Beep);

            RenderHud();
            storage.SaveGame(session);

            if (session.IsOver)
            {
                playing = false;
                queued = null;
                StartCoroutine(ShowResultsLater());
                return;
            }
            if (queued.HasValue)
            {
                var q = queued.Value;
                queued = null;
                TryMove(q);
            }
        }

        // ---------------- game flow ----------------

        void ShowTitle()
        {
            if (playing) storage.SaveGame(session);
            playing = false;
            queued = null;
            Show(null);
            gameScreen.gameObject.SetActive(false);
            titleScreen.gameObject.SetActive(true);

            bool hasGame = storage.HasGame;
            continueButton.gameObject.SetActive(hasGame);
            playButton.SetText(hasGame ? "Новая игра" : "Играть");
            playButton.SetPrimary(!hasGame);
            menuBestText.text = storage.Best.ToString();
            int bestLevel = storage.BestLevel;
            menuFindIcon.gameObject.SetActive(bestLevel > 0);
            menuFindIcon.sprite = art.Item(bestLevel);
            menuFindText.text = bestLevel > 0 ? CheeseRules.ShortNames[bestLevel] : "—";
        }

        void ShowGame()
        {
            titleScreen.gameObject.SetActive(false);
            gameScreen.gameObject.SetActive(true);
            Show(null);
            Canvas.ForceUpdateCanvases();
            boardView.ShowBoard(session.Board);
            RenderHud();
        }

        void NewGame()
        {
            storage.DeleteGame();
            session.StartNew();
            recordAtStart = storage.Best;
            lastScore = 0;
            playing = true;
            queued = null;
            storage.SaveGame(session);
            ShowGame();
        }

        void ContinueGame()
        {
            var d = storage.LoadGame();
            if (d == null)
            {
                NewGame();
                return;
            }
            session.Restore(d.cells, d.score, d.moves, d.cheeses, d.cheeseShown, d.bestLevel);
            recordAtStart = storage.Best;
            lastScore = session.Score;
            playing = true;
            queued = null;
            ShowGame();
        }

        IEnumerator ShowResultsLater()
        {
            yield return new WaitForSecondsRealtime(0.45f);
            resultTitle.text = session.Ended == EndReason.OutOfMoves ? "Ходы закончились" : "Поле заполнено";
            resultScore.text = session.Score.ToString();
            resultBest.text = storage.Best.ToString();
            resultRecord.SetActive(session.Score > recordAtStart && session.Score > 0);
            int level = session.BestLevel;
            resultFindIcon.gameObject.SetActive(level > 0);
            resultFindIcon.sprite = art.Item(level);
            resultFind.text = level > 0 ? CheeseRules.Names[level] : "—";
            resultHole.text = session.Cheeses.ToString();
            resultMoves.text = session.MovesMade.ToString();
            Show(Results);
        }

        void RenderHud()
        {
            scoreText.text = session.Score.ToString();
            bestText.text = storage.Best.ToString();
            movesText.text = session.MovesLeft.ToString();
            bool warn = session.MovesLeft <= CheeseRules.WarnMovesLeft;
            movesText.color = warn ? (Color)CheeseArt.Warn : CheeseArt.Ink;
            movesCard.color = warn ? new Color(1f, 0.86f, 0.82f) : (Color)CheeseArt.Panel;
            holeText.text = session.Cheeses.ToString();
        }

        void Show(string sheet)
        {
            foreach (var kv in sheets) kv.Value.gameObject.SetActive(kv.Key == sheet);
            overlay.gameObject.SetActive(sheet != null);
            if (sheet != null) LayoutRebuilder.ForceRebuildLayoutImmediate(sheets[sheet]);
        }

        void OpenSettings(string from)
        {
            settingsReturn = from;
            soundButton.SetText(sound.Enabled ? "Вкл" : "Выкл");
            soundButton.SetPrimary(sound.Enabled);
            Show(Settings);
        }

        void OpenRules(string from)
        {
            rulesReturn = from;
            Show(Rules);
        }

        void ToggleSound()
        {
            sound.Enabled = !sound.Enabled;
            storage.Sound = sound.Enabled;
            soundButton.SetText(sound.Enabled ? "Вкл" : "Выкл");
            soundButton.SetPrimary(sound.Enabled);
            if (sound.Enabled) sound.Play(CheeseSound.Chime, 3);
        }

        // ---------------- small effects ----------------

        IEnumerator Bump(RectTransform card)
        {
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                card.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t / 0.35f * Mathf.PI));
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        IEnumerator FloatPoints(int points)
        {
            StartCoroutine(Bump(scoreCard));
            var label = CheeseUi.Label(scoreCard, "+" + points, 44f, CheeseArt.CheeseDeep, TextAlignmentOptions.Right, true);
            label.outlineWidth = 0.2f;
            label.outlineColor = CheeseArt.Ink;
            var rt = label.rectTransform;
            rt.Place(0f, -0.45f, 0.95f, 0.1f);
            var start = rt.anchoredPosition;
            for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = start + new Vector2(0, 60f * t / 0.8f);
                label.alpha = 1f - t / 0.8f;
                yield return null;
            }
            Destroy(label.gameObject);
        }

        IEnumerator Toast(string text)
        {
            var box = CheeseUi.Box(gameScreen, "Toast", art.Box, CheeseArt.Cheese, true);
            var rt = box.rectTransform;
            rt.Place(0.14f, 0.47f, 0.86f, 0.56f);
            var icon = CheeseUi.Picture(rt, "Cheese", art.Item(CheeseRules.CheeseLevel));
            icon.rectTransform.Place(0.04f, 0.1f, 0.26f, 0.9f);
            var label = CheeseUi.Label(rt, text, 70f, CheeseArt.Ink, TextAlignmentOptions.Center, true);
            label.rectTransform.Place(0.24f, 0f, 0.98f, 1f);
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            for (float t = 0; t < 1.8f; t += Time.unscaledDeltaTime)
            {
                float k = t / 1.8f;
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.05f, k / 0.15f) : 1f);
                group.alpha = k < 0.15f ? k / 0.15f : k > 0.8f ? 1f - (k - 0.8f) / 0.2f : 1f;
                yield return null;
            }
            Destroy(box.gameObject);
        }

        // ---------------- building the UI ----------------

        void Build()
        {
            var canvasRect = CheeseUi.Rect("Canvas", transform);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();

            var page = CheeseUi.Box(canvasRect, "Page", null, CheeseArt.Page);
            page.rectTransform.Fill();
            page.raycastTarget = true; // the shell menu behind stays untouched while we play

            var safe = CheeseUi.Rect("SafeArea", canvasRect).Fill();
            frame = CheeseUi.Rect("Frame", safe);
            frame.gameObject.AddComponent<CheeseFrame>().Init(safe);
            frame.gameObject.AddComponent<RectMask2D>();

            CheeseUi.Box(frame, "Sky", null, CheeseArt.Sky).rectTransform.Place(0, 0, 1, 0.152f);
            CheeseUi.Box(frame, "Cloud", art.Soft, Color.white).rectTransform.Place(0.1f, 0.03f, 0.34f, 0.06f);
            CheeseUi.Box(frame, "Cloud", art.Soft, Color.white).rectTransform.Place(0.64f, 0.015f, 0.84f, 0.04f);
            CheeseUi.Box(frame, "Meadow", null, CheeseArt.Meadow).rectTransform.Place(0, 0.15f, 1, 1);
            var deep = CheeseUi.Box(frame, "MeadowDeep", null, CheeseArt.MeadowDeep);
            deep.rectTransform.Place(0, 0.7f, 1, 1);
            deep.color = new Color(deep.color.r, deep.color.g, deep.color.b, 0.6f);

            titleScreen = BuildTitle();
            gameScreen = BuildGame();
            overlay = BuildOverlay();
        }

        RectTransform BuildTitle()
        {
            var s = CheeseUi.Rect("TitleScreen", frame).Fill();

            var eyebrow = CheeseUi.Label(s, "ФЕРМА · ГОЛОВОЛОМКА", 34f, CheeseArt.InkDim, TextAlignmentOptions.Center, true);
            eyebrow.characterSpacing = 12f;
            eyebrow.rectTransform.Place(0.05f, 0.028f, 0.95f, 0.058f);

            var logo = CheeseUi.Label(s, "Мышкин сыр", 150f, CheeseArt.Cheese, TextAlignmentOptions.Center, true);
            logo.outlineWidth = 0.22f;
            logo.outlineColor = CheeseArt.Ink;
            logo.textWrappingMode = TextWrappingModes.NoWrap;
            logo.rectTransform.Place(0.02f, 0.058f, 0.98f, 0.15f);

            CheeseUi.Picture(s, "MouseHole", art.Mouse).rectTransform.Place(0.05f, 0.165f, 0.95f, 0.535f);

            var stats = CheeseUi.Rect("Stats", s).Place(0.06f, 0.555f, 0.94f, 0.63f);
            CheeseUi.RowGroup(stats.gameObject, 24f);
            menuBestText = Card(stats, "РЕКОРД", out _, out _);
            menuFindText = Card(stats, "ЛУЧШИЙ ПРЕДМЕТ", out _, out menuFindIcon, withIcon: true);

            continueButton = CheeseUi.Button(s, "Продолжить", true, art, () => { sound.Play(CheeseSound.Chime, 2); ContinueGame(); }, 56f);
            ((RectTransform)continueButton.transform).Place(0.06f, 0.655f, 0.94f, 0.72f);
            playButton = CheeseUi.Button(s, "Играть", true, art, () => { sound.Play(CheeseSound.Chime, 2); NewGame(); }, 56f);
            ((RectTransform)playButton.transform).Place(0.06f, 0.737f, 0.94f, 0.802f);

            var row = CheeseUi.Rect("Row", s).Place(0.06f, 0.82f, 0.94f, 0.872f);
            CheeseUi.RowGroup(row.gameObject, 24f);
            CheeseUi.Button(row, "Правила", false, art, () => OpenRules(null));
            CheeseUi.Button(row, "Настройки", false, art, () => OpenSettings(null));
            exitButton = CheeseUi.Button(row, "Выход", false, art, () => ExitToMenu(lastScore));
            exitButton.gameObject.SetActive(false);

            CheeseUi.Label(s, "Поле 4×4 · 8 предметов · 150 ходов", 36f, CheeseArt.Light, TextAlignmentOptions.Center, true)
                .rectTransform.Place(0.05f, 0.905f, 0.95f, 0.945f);
            return s;
        }

        RectTransform BuildGame()
        {
            var s = CheeseUi.Rect("GameScreen", frame).Fill();

            var title = CheeseUi.Label(s, "Мышкин сыр", 80f, CheeseArt.Cheese, TextAlignmentOptions.Left, true);
            title.outlineWidth = 0.2f;
            title.outlineColor = CheeseArt.Ink;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.rectTransform.Place(0.05f, 0.022f, 0.64f, 0.075f);
            var menu = CheeseUi.Button(s, "Меню", false, art, () => { if (playing) Show(Pause); });
            ((RectTransform)menu.transform).Place(0.66f, 0.02f, 0.95f, 0.074f);

            var hud = CheeseUi.Rect("Hud", s).Place(0.04f, 0.09f, 0.96f, 0.165f);
            CheeseUi.RowGroup(hud.gameObject, 20f);
            scoreText = Card(hud, "ОЧКИ", out scoreCard, out _);
            bestText = Card(hud, "РЕКОРД", out _, out _);
            movesText = Card(hud, "ХОДЫ", out var movesRect, out _);
            movesCard = movesRect.GetComponent<Image>();
            holeText = Card(hud, "НОРКА", out holeCard, out var holeIcon, withIcon: true);
            holeIcon.sprite = art.Item(CheeseRules.CheeseLevel);

            var area = CheeseUi.Rect("FieldArea", s).Place(0.04f, 0.18f, 0.96f, 0.875f);
            var hit = area.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // catches swipes over the whole area
            area.gameObject.AddComponent<CheeseSwipe>().Swiped += TryMove;
            var field = CheeseUi.Rect("Field", area);
            var fit = field.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            boardView = area.gameObject.AddComponent<BoardView>();
            boardView.Init(field, art);

            CheeseUi.Label(s, "Свайп по полю или стрелки", 36f, CheeseArt.Light, TextAlignmentOptions.Left, true)
                .rectTransform.Place(0.05f, 0.9f, 0.64f, 0.95f);
            var restart = CheeseUi.Button(s, "Заново", false, art, () =>
            {
                confirmReturn = null;
                if (playing) Show(Confirm);
                else NewGame();
            });
            ((RectTransform)restart.transform).Place(0.66f, 0.893f, 0.95f, 0.952f);
            return s;
        }

        RectTransform BuildOverlay()
        {
            var o = CheeseUi.Rect("Overlay", frame).Fill();
            var dim = o.gameObject.AddComponent<Image>();
            dim.color = CheeseArt.Shade;

            var pause = Sheet(o, Pause, "Меню");
            Btn(pause, "Продолжить", true, () => Show(null));
            Btn(pause, "Заново", false, () => { confirmReturn = Pause; Show(Confirm); });
            Btn(pause, "Правила", false, () => OpenRules(Pause));
            Btn(pause, "Настройки", false, () => OpenSettings(Pause));
            Btn(pause, "В главное меню", false, ShowTitle);

            var settings = Sheet(o, Settings, "Настройки");
            var soundRow = CheeseUi.Rect("SoundRow", settings);
            var h = CheeseUi.RowGroup(soundRow.gameObject, 20f);
            h.childForceExpandWidth = false;
            soundRow.Size(120f);
            CheeseUi.Label(soundRow, "Звук", 48f, CheeseArt.Ink, TextAlignmentOptions.Left, true).Size(120f, -1f, 1f);
            soundButton = CheeseUi.Button(soundRow, "Вкл", true, art, ToggleSound);
            soundButton.Size(120f, 240f, 0f);
            Btn(settings, "Правила", false, () => OpenRules(Settings));
            Btn(settings, "Закрыть", true, () => Show(settingsReturn));

            var rules = Sheet(o, Rules, "Правила");
            var text = CheeseUi.Label(rules,
                "1. Проведите пальцем вверх, вниз, влево или вправо — все предметы на поле сдвинутся.\n" +
                "2. Два одинаковых предмета объединяются в следующий.\n" +
                "3. За каждое объединение — очки: чем ценнее предмет, тем больше, от 32 до 2048.\n" +
                "4. Готовый сыр мышка сразу уносит в норку, и место освобождается.\n" +
                "5. После каждого хода на поле появляется новая трава или сено.\n" +
                "6. У вас 150 ходов. Игра заканчивается, когда ходы кончатся или поле заполнится.",
                32f, CheeseArt.Ink, TextAlignmentOptions.TopLeft);
            text.lineSpacing = 4f;
            text.paragraphSpacing = 10f;
            var chain = CheeseUi.Rect("Chain", rules);
            CheeseUi.Column(chain.gameObject, 6f);
            for (int level = 1; level <= CheeseRules.CheeseLevel; level++)
            {
                var line = CheeseUi.Rect("Item" + level, chain);
                var lh = CheeseUi.RowGroup(line.gameObject, 18f);
                lh.childForceExpandWidth = false;
                line.Size(84f);
                CheeseUi.Picture(line, "Icon", art.Item(level)).Size(84f, 84f, 0f);
                string from = level == 1 ? "появляется на поле"
                    : $"2 × {CheeseRules.ShortNames[level - 1].ToLower()}{(level == CheeseRules.CheeseLevel ? " → мышка уносит в норку" : "")}";
                CheeseUi.Label(line, $"<b>{CheeseRules.Names[level]}</b>\n<size=24><color=#6e5a45>{from}</color></size>",
                    32f, CheeseArt.Ink, TextAlignmentOptions.Left).Size(84f, -1f, 1f);
                CheeseUi.Label(line, level > 1 ? "+" + CheeseRules.PointsForMerge(level - 1) : "", 34f,
                    CheeseArt.CheeseDeep, TextAlignmentOptions.Right, true).Size(84f, 150f, 0f);
            }
            Btn(rules, "Понятно", true, () => Show(rulesReturn));

            var confirm = Sheet(o, Confirm, "Начать заново?");
            CheeseUi.Label(confirm, "Текущая партия будет потеряна.", 40f, CheeseArt.InkDim, TextAlignmentOptions.Left);
            Btn(confirm, "Начать новую партию", true, NewGame);
            Btn(confirm, "Отмена", false, () => Show(confirmReturn));

            var results = Sheet(o, Results, "Ходы закончились");
            resultTitle = results.GetChild(0).GetComponent<TextMeshProUGUI>();
            var chip = CheeseUi.Box(results, "Record", art.Box, CheeseArt.Cheese);
            chip.Size(70f);
            var chipText = CheeseUi.Label(chip.transform, "Новый рекорд!", 34f, CheeseArt.Ink, TextAlignmentOptions.Center, true);
            chipText.rectTransform.Fill();
            resultRecord = chip.gameObject;
            CheeseUi.Label(results, "Очки", 36f, CheeseArt.InkDim, TextAlignmentOptions.Left, true);
            resultScore = CheeseUi.Label(results, "0", 110f, CheeseArt.Ink, TextAlignmentOptions.Left, true);
            resultBest = ResultRow(results, "Рекорд", out _);
            resultFind = ResultRow(results, "Лучший предмет", out resultFindIcon);
            resultHole = ResultRow(results, "Сыров в норке", out _);
            resultMoves = ResultRow(results, "Сделано ходов", out _);
            Btn(results, "Заново", true, NewGame);
            Btn(results, "В главное меню", false, ShowTitle);

            o.gameObject.SetActive(false);
            return o;
        }

        /// <summary>HUD / menu card: small caption on top, value (with optional icon) below.</summary>
        TextMeshProUGUI Card(Transform parent, string caption, out RectTransform card, out Image icon, bool withIcon = false)
        {
            var box = CheeseUi.Box(parent, caption, art.Box, CheeseArt.Panel, true);
            card = box.rectTransform;
            CheeseUi.Label(card, caption, 26f, CheeseArt.InkDim, TextAlignmentOptions.Center, true)
                .rectTransform.Place(0.05f, 0.1f, 0.95f, 0.42f);
            icon = null;
            float textLeft = 0.05f;
            if (withIcon)
            {
                icon = CheeseUi.Picture(card, "Icon", null);
                icon.rectTransform.Place(0.08f, 0.42f, 0.4f, 0.92f);
                textLeft = 0.38f;
            }
            var value = CheeseUi.Label(card, "0", 52f, CheeseArt.Ink, withIcon ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, true);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.enableAutoSizing = true;
            value.fontSizeMin = 24f;
            value.fontSizeMax = 52f;
            value.rectTransform.Place(textLeft, 0.4f, 0.95f, 0.95f);
            return value;
        }

        RectTransform Sheet(RectTransform parent, string key, string title)
        {
            var panel = CheeseUi.Box(parent, key, art.Box, CheeseArt.Panel, true);
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0.07f, 0.5f);
            rt.anchorMax = new Vector2(0.93f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 600f);
            CheeseUi.Column(panel.gameObject, 22f, 56);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            CheeseUi.Label(rt, title, 72f, CheeseArt.Ink, TextAlignmentOptions.Left, true);
            sheets[key] = rt;
            return rt;
        }

        void Btn(Transform sheet, string text, bool primary, Action onClick) =>
            CheeseUi.Button(sheet, text, primary, art, onClick).Size(124f);

        TextMeshProUGUI ResultRow(Transform sheet, string caption, out Image icon)
        {
            var row = CheeseUi.Rect(caption, sheet);
            var h = CheeseUi.RowGroup(row.gameObject, 14f);
            h.childForceExpandWidth = false;
            row.Size(76f);
            CheeseUi.Label(row, caption, 40f, CheeseArt.InkDim, TextAlignmentOptions.Left).Size(76f, -1f, 1f);
            icon = CheeseUi.Picture(row, "Icon", null);
            icon.Size(70f, 70f, 0f);
            icon.gameObject.SetActive(false);
            var value = CheeseUi.Label(row, "0", 42f, CheeseArt.Ink, TextAlignmentOptions.Right, true);
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.Size(76f);
            return value;
        }
    }
}
