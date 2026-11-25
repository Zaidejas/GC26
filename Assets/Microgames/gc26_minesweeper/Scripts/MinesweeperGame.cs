using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GC26.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GC26.Minesweeper
{
    /// <summary>
    /// Red Flags (Minesweeper): the only IMicrogame in gc26_minesweeper_Main.
    /// Builds its whole UI from code when it begins, so the scene needs only this component
    /// (plus a camera for running the scene on its own). Rules: see Docs~/Minesweeper_GDD.md.
    /// </summary>
    public class MinesweeperGame : MonoBehaviour, IMicrogame
    {
        public const string Id = "gc26_minesweeper";

        [SerializeField] float longPressSeconds = 0.4f;
        public float LongPressSeconds => longPressSeconds;

        // Colours, the same as the browser prototype.
        static readonly Color BgColor = Hex(0x1F2430);
        static readonly Color OuterColor = Hex(0x12151C);
        static readonly Color PanelColor = Hex(0x2B3242);
        static readonly Color ButtonColor = Hex(0x3A4256);
        static readonly Color TextColor = Hex(0xF1F3F7);
        static readonly Color RedColor = Hex(0xE0302B);
        static readonly Color YellowColor = Hex(0xF2C418);
        static readonly Color DarkText = Hex(0x222222);

        // The tutorial runs in the first game each time Red Flags is opened (GDD 6.5). A normal field,
        // not static and not saved: class rule 6 forbids state that outlives the game's scene.
        bool tutorialSeen;

        Action<int> exit;
        bool started;
        MinesweeperSprites sprites;
        Font font;
        readonly System.Random random = new System.Random();
        string[] lossLines;

        // ---- UI references
        Canvas canvas;
        GameObject menuScreen, gameScreen, tutorialBox, endPanel, helpOverlay, pauseOverlay, confirmOverlay;
        RectTransform tutorialRect;
        Text confirmTitle, confirmYesLabel;
        Action confirmYes;                      // what "Yes" does in the "Are you sure?" dialog
        readonly List<Text> levelLabels = new List<Text>();
        RectTransform boardArea, boardRect, highlightFrame;
        GridLayoutGroup grid;
        Text counterText, timerText, tutorialText, endTitle, endLine;
        Image faceImage;
        GameObject tutorialOkButton;
        Text tutorialOkLabel;
        MinesweeperHowToPlayDemo helpDemo;
        Text helpButtonLabel;
        Button helpCloseButton;
        Outline revealOutline, flagOutline;

        // ---- game state
        MinesweeperBoard board;
        MinesweeperDifficulty level;
        MinesweeperCellView[,] cells;
        MinesweeperState state;
        bool flagMode, appPaused;
        float elapsed;
        int tutorialStep;                       // 0 = off, 1 = first tap, 2 = number, 3 = mark a red flag, 4 = good luck
        MinesweeperCellView tutorialTarget, pressedCell;
        Vector2 lastAreaSize;
        RectTransform loupe;                    // the magnifier
        Image[,] loupeTiles, loupeIcons;
        Text[,] loupeLabels;

        bool IsOver => state == MinesweeperState.Won || state == MinesweeperState.Lost;
        bool InGame => gameScreen != null && gameScreen.activeSelf;
        bool OverlayOpen => (helpOverlay != null && helpOverlay.activeSelf) || (pauseOverlay != null && pauseOverlay.activeSelf)
                            || (confirmOverlay != null && confirmOverlay.activeSelf) || (zoomHintOverlay != null && zoomHintOverlay.activeSelf);

        // ---- zoom (GDD 3.5): pinch / mouse wheel; the view never leaves the board and has no scroll bars
        const float ZoomTargetCell = 130f;      // zooming in stops once cells are this big (canvas units)
        const float MaxZoom = 4f;
        float zoom = 1f;
        Vector2 pan;                            // board centre's offset from the board area's centre
        bool pinching, middleDragging;
        float pinchStartDistance, pinchStartZoom;
        Vector2 pinchLastMid, lastMousePos;
        GameObject zoomHintOverlay;
        string ZoomHintKey => MicrogameSave.Key(Id, "zoom_hint_seen");

        // =====================================================================
        // Connection to the GC26 shell
        // =====================================================================

        public void Begin(Action<int> exitCallback)
        {
            started = true;
            exit = exitCallback;
            if (canvas == null) BuildUI();
            ShowMenu();
        }

        void Start()
        {
            // Scene opened on its own (no shell): run it, e.g. in the Editor or in the Windows build.
            if (started || MicrogameRunner.IsRunning) return;
#if UNITY_STANDALONE && !UNITY_EDITOR
            // Portrait window that fits the screen.
            int h = Mathf.Min(960, Screen.currentResolution.height - 120);
            Screen.SetResolution(h * 9 / 16, h, FullScreenMode.Windowed);
#endif
            Begin(score =>
            {
                Debug.Log($"[{Id}] exit, score = {score}");
#if !UNITY_EDITOR
                Application.Quit();       // standalone build: "Exit" closes the app
#endif
            });

            string testDir = SelfTestFolder();
            if (testDir != null) StartCoroutine(SelfTest(testDir));
        }

        /// <summary>Called by "Exit to menu" (pause) and by "Exit" on the level screen.</summary>
        public void ExitToMenu(int score)
        {
            var e = exit;
            exit = null;
            e?.Invoke(score);
        }

        void OnApplicationPause(bool paused) => appPaused = paused;

        // =====================================================================
        // Per-frame: timer, face, Back button, board layout, tutorial highlight
        // =====================================================================

        void Update()
        {
            if (canvas == null) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) OnBack();

            if (!InGame) return;
            if (!OverlayOpen) UpdateZoomInput();
            if (pressing) UpdatePress();
            if (state == MinesweeperState.Playing && !appPaused && !OverlayOpen)
                elapsed = Mathf.Min(999f, elapsed + Time.unscaledDeltaTime);
            UpdateHud();
        }

        // =====================================================================
        // Pressing a cell: aim by sliding, release = tap, hold still = yellow flag.
        // On small cells (Expert, and Intermediate on phones) a magnifier above the finger shows the
        // cells around it, enlarged, with the one that will be hit framed in yellow.
        // =====================================================================

        const float LoupeBelowCellSize = 80f;   // canvas units; below this cells count as "small"
        const float LoupeDelay = 0.12f;         // seconds before it appears, so quick taps don't flash it
        const float LoupeCell = 120f;           // size of one enlarged cell
        const float LoupeOffset = 300f;         // how far above the finger it floats

        bool pressing, pressLongDone, pressIsTouch;
        float pressStart, pressBegan;
        MinesweeperCellView pressTarget;

        bool SmallCells => grid != null && grid.cellSize.x * zoom < LoupeBelowCellSize;   // as big as they look now
        // Only fingers need the magnifier: a finger hides the cell, a mouse pointer doesn't.
        bool LoupeFor(bool touch) => touch && SmallCells;

        public void BeginPress(MinesweeperCellView cell)
        {
            if (IsOver || OverlayOpen || pinching) return;
            pressing = true;
            pressIsTouch = Pointer.current is Touchscreen;
            pressLongDone = false;
            pressTarget = cell;
            pressStart = pressBegan = Time.unscaledTime;
            SetPressedCell(cell);
        }

        void UpdatePress()
        {
            Pointer p = Pointer.current;
            if (p == null || IsOver || OverlayOpen) { EndPress(false); return; }

            bool released = p.press.wasReleasedThisFrame || !p.press.isPressed;
            if (released) { EndPress(!pressLongDone); return; }

            Vector2 pos = p.position.ReadValue();
            MinesweeperCellView cell = CellAt(pos);
            if (cell != pressTarget)
            {
                // Slid to another cell: aim there instead, and start the long-press wait again.
                pressTarget = cell;
                pressStart = Time.unscaledTime;
                SetPressedCell(cell);
            }
            if (pressLongDone) return;

            if (cell != null && Time.unscaledTime - pressStart >= longPressSeconds)
            {
                pressLongDone = true;                 // held still long enough: yellow flag
                SetPressedCell(null);
                HideLoupe();
                OnLongPress(cell.X, cell.Y);
                return;
            }

            if (cell != null && LoupeFor(pressIsTouch) && Time.unscaledTime - pressBegan >= LoupeDelay) ShowLoupe(cell, pos);
            else HideLoupe();
        }

        // Forget a press without acting on it (a new board or the menu replaced the cells, or a pinch began).
        void CancelPress()
        {
            pressing = false;
            pressTarget = null;
            HideLoupe();
            if (pressedCell != null) SetPressedCell(null);
        }

        // =====================================================================
        // Zoom (GDD 3.5). Phones: pinch with two fingers to zoom, drag with two fingers to move.
        // Computers: mouse wheel zooms around the pointer, drag with the wheel held to move.
        // One finger / the left button still aims and taps as before. The board is scaled inside the
        // board area (which cuts off what's outside it); there are no scroll bars, and the view is kept
        // on the board: zoomed out the whole board fits, zoomed in its edges can't come off the screen's edges.
        // =====================================================================

        float MaxZoomNow => grid == null || grid.cellSize.x <= 0f ? 1f : Mathf.Clamp(ZoomTargetCell / grid.cellSize.x, 1f, MaxZoom);

        void UpdateZoomInput()
        {
            // Two fingers down: pinch zoom and move. A press that had started is cancelled, not tapped.
            int down = 0;
            Vector2 a = default, b = default;
            Touchscreen ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (down == 0) a = t.position.ReadValue(); else if (down == 1) b = t.position.ReadValue();
                    down++;
                }
            if (down >= 2)
            {
                Vector2 mid = (a + b) * 0.5f;
                float dist = Mathf.Max((a - b).magnitude, 1f);
                if (!pinching)
                {
                    pinching = true;
                    CancelPress();
                    pinchStartDistance = dist;
                    pinchStartZoom = zoom;
                }
                else
                {
                    PanBy(mid - pinchLastMid);
                    ZoomAround(mid, pinchStartZoom * dist / pinchStartDistance);
                }
                pinchLastMid = mid;
                return;
            }
            // Stays "pinching" until every finger is up, so the finger lifted last doesn't open a cell.
            if (pinching && down == 0) pinching = false;

            Mouse m = Mouse.current;
            if (m == null) return;
            Vector2 pos = m.position.ReadValue();
            float wheel = m.scroll.ReadValue().y;
            if (wheel != 0f && RectTransformUtility.RectangleContainsScreenPoint(boardArea, pos, null))
                ZoomAround(pos, zoom * (wheel > 0f ? 1.2f : 1f / 1.2f));
            if (m.middleButton.isPressed)
            {
                if (middleDragging) PanBy(pos - lastMousePos);
                middleDragging = true;
            }
            else middleDragging = false;
            lastMousePos = pos;
        }

        // Zooms so the board point under "screenPos" stays under it.
        void ZoomAround(Vector2 screenPos, float newZoom)
        {
            newZoom = Mathf.Clamp(newZoom, 1f, MaxZoomNow);
            if (Mathf.Approximately(newZoom, zoom)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boardArea, screenPos, null, out Vector2 local);
            Vector2 p = local - boardArea.rect.center;
            pan = p - (p - pan) * (newZoom / zoom);
            zoom = newZoom;
            ApplyZoom();
        }

        void PanBy(Vector2 screenDelta)
        {
            if (zoom <= 1f) return;
            pan += screenDelta / canvas.scaleFactor;
            ApplyZoom();
        }

        // Scale and position the board, keeping the view on it.
        void ApplyZoom()
        {
            zoom = Mathf.Clamp(zoom, 1f, MaxZoomNow);
            Vector2 shown = boardRect.rect.size * zoom;
            Vector2 room = boardArea.rect.size - new Vector2(BoardMargin, BoardMargin);
            Vector2 limit = new Vector2(Mathf.Max(0f, (shown.x - room.x) / 2f), Mathf.Max(0f, (shown.y - room.y) / 2f));
            pan = new Vector2(Mathf.Clamp(pan.x, -limit.x, limit.x), Mathf.Clamp(pan.y, -limit.y, limit.y));
            boardRect.localScale = new Vector3(zoom, zoom, 1f);
            boardRect.anchoredPosition = pan;     // anchored at the area's centre
            if (pressing && !SmallCells) HideLoupe();
        }

        void ResetZoom()
        {
            zoom = 1f;
            pan = Vector2.zero;
            pinching = middleDragging = false;
            if (boardRect != null) ApplyZoom();
        }

        // ---- "Zoom in and out" hint: once, the first time Expert opens (after the tutorial if it's running)

        void MaybeShowZoomHint()
        {
            if (level != MinesweeperDifficulty.Expert || tutorialStep != 0 || OverlayOpen) return;
            if (PlayerPrefs.GetInt(ZoomHintKey, 0) == 1) return;
            ShowZoomHint();
        }

        void ShowZoomHint()
        {
            CancelPress();
            PlayerPrefs.SetInt(ZoomHintKey, 1);
            PlayerPrefs.Save();
            zoomHintOverlay.SetActive(true);    // the animation starts again (OnEnable)
        }

        void HideZoomHint() => zoomHintOverlay.SetActive(false);

        void EndPress(bool tap)
        {
            pressing = false;
            HideLoupe();
            MinesweeperCellView target = pressTarget;
            pressTarget = null;
            SetPressedCell(null);
            if (tap && target != null) OnTap(target.X, target.Y);
        }

        // The cell under a screen position, or null when it's off the board.
        MinesweeperCellView CellAt(Vector2 screenPos)
        {
            if (cells == null) return null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(boardArea, screenPos, null)) return null;   // cut off when zoomed in
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boardRect, screenPos, null, out Vector2 local)) return null;
            float size = grid.cellSize.x;
            Rect r = boardRect.rect;                 // y grows upwards; row 0 is at the top
            int x = Mathf.FloorToInt((local.x - r.xMin) / size);
            int y = Mathf.FloorToInt((r.yMax - local.y) / size);
            return board.InBounds(x, y) ? cells[x, y] : null;
        }

        // The magnifier: 3×3 enlarged copies of the cells around "target", floating above the finger
        // (below it if there's no room above), kept inside the screen.
        void ShowLoupe(MinesweeperCellView target, Vector2 screenPos)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = target.X + dx, y = target.Y + dy;
                    int lx = dx + 1, ly = dy + 1;
                    bool inside = board.InBounds(x, y);
                    loupeTiles[lx, ly].enabled = inside;
                    loupeIcons[lx, ly].enabled = false;
                    loupeLabels[lx, ly].text = "";
                    if (!inside) continue;
                    MinesweeperCellView c = cells[x, y];
                    loupeTiles[lx, ly].sprite = c.TileSprite;
                    loupeTiles[lx, ly].color = dx == 0 && dy == 0 ? new Color(0.8f, 0.8f, 0.8f, 1f) : Color.white;
                    if (c.IconSprite != null) { loupeIcons[lx, ly].sprite = c.IconSprite; loupeIcons[lx, ly].enabled = true; }
                    loupeLabels[lx, ly].text = c.NumberText;
                    loupeLabels[lx, ly].color = c.NumberColor;
                }

            var screen = (RectTransform)gameScreen.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(screen, screenPos, null, out Vector2 local);
            Vector2 half = loupe.rect.size / 2f;
            Rect area = screen.rect;
            float y0 = local.y + LoupeOffset;
            if (y0 + half.y > area.yMax - 170f) y0 = local.y - LoupeOffset;          // no room above: below
            float x0 = Mathf.Clamp(local.x, area.xMin + half.x + 10f, area.xMax - half.x - 10f);
            y0 = Mathf.Clamp(y0, area.yMin + half.y + 10f, area.yMax - half.y - 10f);
            loupe.anchoredPosition = new Vector2(x0, y0);
            loupe.gameObject.SetActive(true);
        }

        void HideLoupe()
        {
            if (loupe != null && loupe.gameObject.activeSelf) loupe.gameObject.SetActive(false);
        }

        void BuildLoupe(RectTransform gameRoot)
        {
            loupe = NewRect("Magnifier", gameRoot);
            loupe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            loupe.anchorMin = loupe.anchorMax = loupe.pivot = new Vector2(0.5f, 0.5f);
            float side = 3 * LoupeCell + 30f;
            loupe.sizeDelta = new Vector2(side, side);
            Image bg = AddImage(loupe, PanelColor, sprites.Rounded);
            bg.raycastTarget = false;
            var outline = loupe.gameObject.AddComponent<Outline>();
            outline.effectColor = YellowColor;
            outline.effectDistance = new Vector2(4, -4);
            loupe.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -12);

            loupeTiles = new Image[3, 3];
            loupeIcons = new Image[3, 3];
            loupeLabels = new Text[3, 3];
            for (int ly = 0; ly < 3; ly++)
                for (int lx = 0; lx < 3; lx++)
                {
                    RectTransform c = NewRect($"Cell {lx},{ly}", loupe);
                    c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f);
                    c.sizeDelta = new Vector2(LoupeCell - 4f, LoupeCell - 4f);
                    c.anchoredPosition = new Vector2((lx - 1) * LoupeCell, (1 - ly) * LoupeCell);
                    loupeTiles[lx, ly] = c.gameObject.AddComponent<Image>();
                    loupeTiles[lx, ly].type = Image.Type.Sliced;
                    loupeTiles[lx, ly].raycastTarget = false;

                    RectTransform i = NewRect("Icon", c);
                    Stretch(i, 0.12f);
                    loupeIcons[lx, ly] = i.gameObject.AddComponent<Image>();
                    loupeIcons[lx, ly].preserveAspect = true;
                    loupeIcons[lx, ly].raycastTarget = false;

                    RectTransform t = NewRect("Number", c);
                    Stretch(t);
                    loupeLabels[lx, ly] = AddText(t, "", 74, DarkText, FontStyle.Bold);
                }

            // Yellow frame around the middle cell: the one that will be tapped.
            RectTransform f = NewRect("Aim", loupe);
            f.anchorMin = f.anchorMax = new Vector2(0.5f, 0.5f);
            f.sizeDelta = new Vector2(LoupeCell + 14f, LoupeCell + 14f);
            AddImage(f, Color.white, sprites.Frame).raycastTarget = false;
            loupe.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!InGame || board == null) return;
            if (boardArea.rect.size != lastAreaSize) LayoutBoard();   // e.g. the window was resized

            if (tutorialTarget != null)
            {
                // Keep the pulsing frame on top of the highlighted cell.
                RectTransform t = tutorialTarget.Rect;
                highlightFrame.anchorMin = t.anchorMin;
                highlightFrame.anchorMax = t.anchorMax;
                highlightFrame.pivot = t.pivot;
                highlightFrame.anchoredPosition = t.anchoredPosition;
                float pad = 8f + 6f * Mathf.PingPong(Time.unscaledTime * 2f, 1f);
                highlightFrame.sizeDelta = t.rect.size + Vector2.one * (pad * 2f);
            }
            if (tutorialBox.activeSelf) PlaceTutorialBubble();
        }

        // The tutorial bubble sits in the middle of the screen and may cover the board, but never the
        // highlighted cell: if it would, it moves just above the cell, or just below it if there's no room above.
        void PlaceTutorialBubble()
        {
            var screen = (RectTransform)gameScreen.transform;
            float half = tutorialRect.rect.height / 2f;
            float y = 0f;                                          // 0 = middle of the game screen
            if (tutorialTarget != null)
            {
                const float gap = 30f;
                var corners = new Vector3[4];
                tutorialTarget.Rect.GetWorldCorners(corners);      // 0 = bottom-left, 2 = top-right
                float cellBottom = screen.InverseTransformPoint(corners[0]).y - gap;
                float cellTop = screen.InverseTransformPoint(corners[2]).y + gap;
                bool covers = y - half < cellTop && y + half > cellBottom;
                if (covers)
                {
                    float roomTop = screen.rect.yMax - 170f;      // below the top bar
                    y = cellTop + half <= roomTop - half ? cellTop + half : cellBottom - half;
                }
            }
            tutorialRect.anchoredPosition = new Vector2(0, y);
        }

        // True if the tutorial bubble and the highlighted cell overlap on screen (used by the self-test).
        bool TutorialBubbleCoversTarget()
        {
            if (tutorialTarget == null || !tutorialBox.activeSelf) return false;
            var a = new Vector3[4]; var b = new Vector3[4];
            tutorialRect.GetWorldCorners(a);
            tutorialTarget.Rect.GetWorldCorners(b);
            return a[0].x < b[2].x && a[2].x > b[0].x && a[0].y < b[2].y && a[2].y > b[0].y;
        }

        // Android Back / Escape: close a pop-up first, then game -> level screen -> exit.
        void OnBack()
        {
            if (confirmOverlay.activeSelf) { ConfirmNo(); return; }       // Back = "Keep playing"
            if (helpOverlay.activeSelf) { HideHelp(); return; }
            if (zoomHintOverlay.activeSelf) { HideZoomHint(); return; }
            if (pauseOverlay.activeSelf) { pauseOverlay.SetActive(false); return; }
            if (InGame) { RequestMenu(); return; }
            ExitToMenu(0);
        }

        // =====================================================================
        // "Are you sure?" (GDD 6.7): before anything that throws away a game in progress
        // =====================================================================

        // A game is in progress once the first cell is opened and until it is won or lost.
        // Without one (no tap yet, or the round is over) there is nothing to lose, so the action runs at once.
        void ConfirmIfGameInProgress(string question, string yesLabel, Action onYes)
        {
            pauseOverlay.SetActive(false);
            if (!InGame || state != MinesweeperState.Playing) { onYes(); return; }
            SetPressedCell(null);
            confirmTitle.text = question;
            confirmYesLabel.text = yesLabel;
            confirmYes = onYes;
            confirmOverlay.SetActive(true);
        }

        void ConfirmYes()
        {
            Action action = confirmYes;
            confirmYes = null;
            confirmOverlay.SetActive(false);
            action?.Invoke();
        }

        void ConfirmNo()
        {
            confirmYes = null;
            confirmOverlay.SetActive(false);     // back to the same game; the timer continues
        }

        // Button words mean the same everywhere:
        //   Menu = Red Flags' own main menu (the level screen)
        //   Exit = leave Red Flags (closes the app, or returns to the GC26 menu when started from there)
        void RequestNewRound() =>
            ConfirmIfGameInProgress("Are you sure you want to start a new game and lose your progress?", "New game", NewRound);

        void RequestMenu() =>
            ConfirmIfGameInProgress("Are you sure you want to go to the menu and lose your progress?", "Menu", ShowMenu);

        void RequestExit() =>
            ConfirmIfGameInProgress("Are you sure you want to exit and lose your progress?", "Exit", () => ExitToMenu(0));

        // =====================================================================
        // Screens
        // =====================================================================

        void ShowMenu()
        {
            CancelPress();
            EndTutorial();
            helpOverlay.SetActive(false);
            pauseOverlay.SetActive(false);
            confirmOverlay.SetActive(false);
            endPanel.SetActive(false);
            gameScreen.SetActive(false);
            ClearBoard();
            board = null;

            for (int i = 0; i < MinesweeperDifficulty.All.Length; i++)
            {
                var lv = MinesweeperDifficulty.All[i];
                int best = PlayerPrefs.GetInt(BestKey(lv), -1);
                levelLabels[i].text = $"<b>{lv.Name}</b>\n<size=40>{lv.Width}×{lv.Height} · {lv.Mines} red flags · Best: {(best >= 0 ? best + " s" : "—")}</size>";
            }
            menuScreen.SetActive(true);
        }

        void StartGame(MinesweeperDifficulty difficulty)
        {
            level = difficulty;
            menuScreen.SetActive(false);
            gameScreen.SetActive(true);
            NewRound();
        }

        // A new board: every cell hidden, nothing opened until the player taps (GDD 3.1).
        void NewRound()
        {
            CancelPress();
            var shape = ShapeForScreen();
            board = new MinesweeperBoard(shape.cols, shape.rows, level.Mines);
            state = MinesweeperState.Ready;
            elapsed = 0f;
            SetFlagMode(false);
            endPanel.SetActive(false);
            pauseOverlay.SetActive(false);
            EndTutorial();
            BuildCells();
            RefreshAll(false, null);
            UpdateHud();
            StartTutorialIfNeeded();
            MaybeShowZoomHint();                 // Expert, first time, when no tutorial is running
        }

        // =====================================================================
        // Player actions (called by MinesweeperCellView)
        // =====================================================================

        public void OnTap(int x, int y)
        {
            if (IsOver || OverlayOpen) return;

            MinesweeperCell c = board.GetCell(x, y);
            if (c.IsRevealed) AfterOpen(board.Chord(x, y));          // tap a number = chord
            else if (flagMode) ToggleFlag(x, y);
            else if (!c.IsFlagged)
            {
                bool firstReveal = !board.MinesPlaced;
                if (firstReveal)
                {
                    // The first tap is always safe. About 4% of boards need a guess somewhere; the rest are
                    // re-rolled until they can be solved from here by logic alone (MinesweeperSolver).
                    board.PlaceMinesFair(x, y);
                    state = MinesweeperState.Playing;
                }
                AfterOpen(board.Reveal(x, y));
                if (firstReveal) { TutorialAfterFirstReveal(); return; }
            }
            TutorialAfterAction();
        }

        public void OnLongPress(int x, int y)
        {
            if (IsOver || OverlayOpen) return;
            if (!board.GetCell(x, y).IsRevealed && ToggleFlag(x, y)) Vibrate();
            TutorialAfterAction();
        }

        public void OnSecondaryClick(int x, int y)
        {
            if (IsOver || OverlayOpen) return;
            ToggleFlag(x, y);
            TutorialAfterAction();
        }

        public void SetPressedCell(MinesweeperCellView cell)
        {
            if (pressedCell != null) pressedCell.SetPressed(false);
            pressedCell = null;
            if (cell == null || IsOver || OverlayOpen || board.GetCell(cell.X, cell.Y).IsRevealed) return;
            pressedCell = cell;
            cell.SetPressed(true);
        }

        bool ToggleFlag(int x, int y)
        {
            if (!board.ToggleFlag(x, y)) return false;
            cells[x, y].Show(board.GetCell(x, y), false, false, sprites);
            UpdateHud();
            return true;
        }

        void AfterOpen(List<Vector2Int> changed)
        {
            foreach (var p in changed) cells[p.x, p.y].Show(board.GetCell(p.x, p.y), false, false, sprites);
            foreach (var p in changed)
                if (board.GetCell(p.x, p.y).IsMine) { Lose(p); return; }
            if (board.IsWon) Win();
            UpdateHud();
        }

        void Lose(Vector2Int hit)
        {
            state = MinesweeperState.Lost;
            RefreshAll(true, hit);
            Vibrate();
            UpdateHud();
            ShowEnd("You hit a red flag!", MinesweeperLossLines.PickRandom(lossLines, random));
        }

        void Win()
        {
            state = MinesweeperState.Won;
            // Cover the remaining red flags with yellow flags.
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    MinesweeperCell c = board.GetCell(x, y);
                    if (c.IsMine && !c.IsFlagged) c.IsFlagged = true;
                }
            RefreshAll(false, null);

            int time = Mathf.FloorToInt(elapsed);
            int best = PlayerPrefs.GetInt(BestKey(level), -1);
            bool record = best < 0 || time < best;
            if (record) { PlayerPrefs.SetInt(BestKey(level), time); PlayerPrefs.Save(); }
            UpdateHud();
            ShowEnd("You won!", $"Time: {time} s{(record ? " · New record!" : "")}");
        }

        void RefreshAll(bool gameOver, Vector2Int? hit)
        {
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    cells[x, y].Show(board.GetCell(x, y), gameOver, hit.HasValue && hit.Value == new Vector2Int(x, y), sprites);
        }

        void ShowEnd(string title, string line)
        {
            endTitle.text = title;
            endLine.text = line;
            endLine.gameObject.SetActive(!string.IsNullOrEmpty(line));
            endPanel.SetActive(true);
        }

        void SetFlagMode(bool on)
        {
            flagMode = on;
            revealOutline.enabled = !on;
            flagOutline.enabled = on;
        }

        void UpdateHud()
        {
            if (board == null) return;
            counterText.text = state == MinesweeperState.Won ? "000" : Digits(board.MineCount - board.FlagCount);
            timerText.text = Digits(Mathf.FloorToInt(elapsed));
            faceImage.sprite = state == MinesweeperState.Won ? sprites.FaceCool
                             : state == MinesweeperState.Lost ? sprites.FaceDead
                             : pressedCell != null ? sprites.FaceOh
                             : sprites.FaceSmile;
        }

        static string Digits(int n) =>
            n < 0 ? "-" + Mathf.Min(99, -n).ToString("00") : Mathf.Min(999, n).ToString("000");

        static string BestKey(MinesweeperDifficulty lv) => MicrogameSave.Key(Id, "best_" + lv.Name);

        static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (PlayerPrefs.GetInt(MicrogameSave.Key(Id, "vibration"), 1) == 1) Handheld.Vibrate();
#endif
        }

        // =====================================================================
        // First-game tutorial (GDD 6.5), in the first game each time Red Flags is opened:
        //   1. "Tap any cell to start"                (waits for the first tap)
        //   2. the biggest opened number is explained (Got it, or any move)
        //   3. a cell that must hide a red flag is highlighted: mark it with a yellow flag (or Skip)
        //   4. "Avoid the red flags! Good luck!"      (Let's go!, or any move)
        // =====================================================================

        void StartTutorialIfNeeded()
        {
            if (tutorialSeen) return;
            tutorialStep = 1;
            ShowTutorial("Tap any cell to start. Your first tap is always safe.", null);
        }

        // Called after every move during the tutorial.
        void TutorialAfterAction()
        {
            if (tutorialStep == 0) return;
            if (IsOver) { EndTutorial(); return; }
            switch (tutorialStep)
            {
                case 2: TutorialMarkStep(); break;                          // any move after step 2 moves on
                case 3:
                    if (board.GetCell(tutorialTarget.X, tutorialTarget.Y).IsFlagged) TutorialGoodLuck();
                    break;
                case 4: FinishTutorial(); break;
            }
        }

        // The button in the tutorial bubble: Got it / Skip / Let's go!
        void OnTutorialButton()
        {
            if (tutorialStep == 2) TutorialMarkStep();
            else if (tutorialStep == 3) TutorialGoodLuck();
            else FinishTutorial();
        }

        // The tutorial is done; on Expert the zoom hint comes next (the first time only).
        void FinishTutorial()
        {
            EndTutorial();
            MaybeShowZoomHint();
        }

        // Step 3: a cell the numbers prove is a red flag; the player marks it with a yellow flag.
        void TutorialMarkStep()
        {
            MinesweeperCellView target = FindObviousRedFlag();
            if (target == null) { TutorialGoodLuck(); return; }      // none on this board yet
            tutorialStep = 3;
            SetTutorialTarget(target);
            // Phones and tablets: long press. Computers: right-click (long press also works there).
            string how = Application.isMobilePlatform ? "Long press" : "Right-click";
            ShowTutorial($"The numbers show this cell must hide a red flag. {how} it to mark it with a yellow flag.", "Skip");
        }

        // Step 4: the send-off.
        void TutorialGoodLuck()
        {
            tutorialStep = 4;
            SetTutorialTarget(null);
            ShowTutorial("Avoid the red flags! Good luck!", "Let's go!");
        }

        // A hidden cell that must be a red flag: next to an opened number whose hidden, unflagged
        // neighbours are exactly as many as its missing red flags. Null if there is none.
        MinesweeperCellView FindObviousRedFlag()
        {
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    MinesweeperCell c = board.GetCell(x, y);
                    if (!c.IsRevealed || c.AdjacentMines == 0) continue;
                    int flagged = 0, hidden = 0;
                    Vector2Int first = default;
                    foreach (var n in board.Neighbours(x, y))
                    {
                        MinesweeperCell nc = board.GetCell(n.x, n.y);
                        if (nc.IsFlagged) flagged++;
                        else if (!nc.IsRevealed) { if (hidden == 0) first = n; hidden++; }
                    }
                    if (hidden > 0 && hidden == c.AdjacentMines - flagged && board.GetCell(first.x, first.y).IsMine)
                        return cells[first.x, first.y];
                }
            return null;
        }

        void SetTutorialTarget(MinesweeperCellView cell)
        {
            tutorialTarget = cell;
            highlightFrame.gameObject.SetActive(cell != null);
            if (cell != null) highlightFrame.SetAsLastSibling();
        }

        void TutorialAfterFirstReveal()
        {
            if (tutorialStep != 1 || IsOver) { EndTutorial(); return; }

            // The opened cell with the biggest number (the first one found if several are equal).
            MinesweeperCellView best = null;
            int bestNumber = 0;
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    MinesweeperCell c = board.GetCell(x, y);
                    if (c.IsRevealed && !c.IsMine && c.AdjacentMines > bestNumber) { best = cells[x, y]; bestNumber = c.AdjacentMines; }
                }
            if (best == null) { EndTutorial(); return; }

            tutorialStep = 2;
            tutorialSeen = true;
            SetTutorialTarget(best);
            ShowTutorial($"This cell has {bestNumber} red flag{(bestNumber == 1 ? "" : "s")} around it — be careful!", "Got it");
        }

        // buttonLabel null = no button (step 1 waits for the first tap).
        void ShowTutorial(string text, string buttonLabel)
        {
            tutorialText.text = text;
            tutorialOkButton.SetActive(buttonLabel != null);
            if (buttonLabel != null) tutorialOkLabel.text = buttonLabel;
            tutorialBox.SetActive(true);
        }

        void EndTutorial()
        {
            tutorialStep = 0;
            tutorialTarget = null;
            if (tutorialBox != null) tutorialBox.SetActive(false);
            if (highlightFrame != null) highlightFrame.gameObject.SetActive(false);
        }

        // =====================================================================
        // Pop-ups
        // =====================================================================

        void ShowHelp()
        {
            SetPressedCell(null);
            helpOverlay.SetActive(true);         // the demo starts again at page 1 (OnEnable)
            UpdateHelpButton();
        }
        void HideHelp() => helpOverlay.SetActive(false);
        void ShowPause() { SetPressedCell(null); pauseOverlay.SetActive(true); }

        // =====================================================================
        // Board cells
        // =====================================================================

        void ClearBoard()
        {
            for (int i = boardRect.childCount - 1; i >= 0; i--)
            {
                Transform child = boardRect.GetChild(i);
                if (child == highlightFrame) continue;
                child.SetParent(null);
                Destroy(child.gameObject);
            }
            cells = null;
            pressedCell = null;
        }

        void BuildCells()
        {
            ClearBoard();
            grid.constraintCount = board.Width;
            cells = new MinesweeperCellView[board.Width, board.Height];

            // Row by row from the top, so the grid fills left to right, top to bottom.
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    RectTransform rt = NewRect($"Cell {x},{y}", boardRect);
                    var bg = rt.gameObject.AddComponent<Image>();
                    bg.type = Image.Type.Sliced;

                    RectTransform iconRt = NewRect("Icon", rt);
                    Stretch(iconRt, 0.12f);
                    var icon = iconRt.gameObject.AddComponent<Image>();
                    icon.raycastTarget = false;
                    icon.preserveAspect = true;

                    RectTransform labelRt = NewRect("Number", rt);
                    Stretch(labelRt);
                    Text label = AddText(labelRt, "", 30, DarkText, FontStyle.Bold);

                    var view = rt.gameObject.AddComponent<MinesweeperCellView>();
                    view.Init(this, x, y, bg, icon, label);
                    cells[x, y] = view;
                }
            highlightFrame.SetAsLastSibling();
            ResetZoom();                          // a new board starts zoomed out
            lastAreaSize = Vector2.zero;          // lay out again next frame
        }

        // Only a thin margin (12 units): on phones the board's width decides the cell size, so every
        // unit counts. Expert on a phone can't get much bigger than screen width / 16 (about 4 mm);
        // the magnifier (ShowLoupe) makes those small cells easy to hit.
        const float BoardMargin = 12f;
        const float MaxCellSize = 130f;

        // The level's cells arranged to fit the free space best (e.g. Expert 16×30 on a tall phone,
        // 20×24 on a wider portrait window, 30×16 on a landscape PC screen). GDD 3.5.
        (int cols, int rows) ShapeForScreen()
        {
            Vector2 area = boardArea != null ? boardArea.rect.size : Vector2.zero;
            if (area.x <= 1f || area.y <= 1f) return (level.Width, level.Height);
            return level.ShapeFor(area.x - BoardMargin, area.y - BoardMargin, MaxCellSize);
        }

        // Fits the board into the free space between the bars.
        void LayoutBoard()
        {
            Vector2 area = boardArea.rect.size;
            lastAreaSize = area;
            if (cells == null || area.x <= 1f || area.y <= 1f) return;

            // Until the first move the board is empty, so it can still be rearranged for this screen
            // (the window was resized, or the free space wasn't known yet). After that it keeps its shape.
            if (state == MinesweeperState.Ready && board.FlagCount == 0)
            {
                var shape = ShapeForScreen();
                if (shape.cols != board.Width || shape.rows != board.Height)
                {
                    CancelPress();
                    board = new MinesweeperBoard(shape.cols, shape.rows, level.Mines);
                    BuildCells();
                    RefreshAll(false, null);
                    lastAreaSize = area;
                }
            }

            const float margin = BoardMargin;
            float size = Mathf.Floor(Mathf.Min((area.x - margin) / board.Width, (area.y - margin) / board.Height, MaxCellSize));
            size = Mathf.Max(size, 20f);
            grid.cellSize = new Vector2(size, size);
            boardRect.sizeDelta = new Vector2(size * board.Width, size * board.Height);
            int fontSize = Mathf.RoundToInt(size * 0.62f);
            foreach (var c in cells) c.SetFontSize(fontSize);
            ApplyZoom();                          // keep the zoomed view on the board after a resize
        }

        // =====================================================================
        // Building the UI from code
        // =====================================================================

        void BuildUI()
        {
            sprites = MinesweeperSprites.Create();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var linesAsset = Resources.Load<TextAsset>(Id + "/loss_lines");
            lossLines = MinesweeperLossLines.Parse(linesAsset != null ? linesAsset.text : "");

            // Input for UI clicks and touches (the shell may already have one).
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("MinesweeperEventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var canvasGo = new GameObject("MinesweeperCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform outer = NewRect("Background", canvasGo.transform);
            Stretch(outer);
            AddImage(outer, OuterColor, null);

            RectTransform safe = NewRect("SafeArea", canvasGo.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<MinesweeperSafeArea>();

            RectTransform app = NewRect("App", safe);
            Stretch(app);
            AddImage(app, BgColor, null);

            BuildMenu(app);
            BuildGame(app);
            BuildVersionLabel(app);       // before the pop-ups, so they dim it too
            BuildHelp(app);
            BuildPause(app);
            BuildZoomHint(app);
            BuildConfirm(app);            // last, so it draws on top of everything
        }

        // Level screen (the first screen): title, three levels with best times, Exit.
        void BuildMenu(RectTransform app)
        {
            RectTransform menu = NewRect("Levels", app);
            Stretch(menu);
            menuScreen = menu.gameObject;
            var col = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(80, 80, 180, 80);
            col.spacing = 40;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            RectTransform title = NewRect("Title", menu);
            AddText(title, $"Avoid the\n<color=#{ColorUtility.ToHtmlStringRGB(RedColor)}>Red Flags</color>", 120, TextColor, FontStyle.Bold);
            Layout(title, h: 340);

            levelLabels.Clear();
            foreach (var lv in MinesweeperDifficulty.All)
            {
                var chosen = lv;
                Button b = MakeButton(menu, "Level " + lv.Name, "", 60, PanelColor, TextColor, () => StartGame(chosen), out Text label);
                label.alignment = TextAnchor.MiddleLeft;
                label.fontStyle = FontStyle.Normal;
                RectTransform lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(50, 0);
                lrt.offsetMax = new Vector2(-30, 0);
                levelLabels.Add(label);
                Layout(b, h: 230);
            }

            RectTransform spacer = NewRect("Spacer", menu);
            Layout(spacer, h: 20, fh: 1);

            Button exitButton = MakeButton(menu, "Exit", "Exit", 50, ButtonColor, TextColor, () => ExitToMenu(0), out _);
            Layout(exitButton, h: 140);
        }

        // Game screen: top bar, tutorial, board, bottom bar, end-of-round panel.
        void BuildGame(RectTransform app)
        {
            RectTransform g = NewRect("Game", app);
            Stretch(g);
            gameScreen = g.gameObject;
            var v = g.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            // ---- top bar: menu, red flag counter, face, timer, help
            RectTransform top = NewRect("TopBar", g);
            AddImage(top, PanelColor, null);
            Layout(top, h: 170, fh: 0);       // fh 0: fixed height, all spare height goes to the board
            var h = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(30, 30, 25, 25);
            h.spacing = 24;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            Button menuButton = MakeIconButton(top, "Pause", sprites.MenuIcon, ShowPause, out _);
            Layout(menuButton, w: 130);
            counterText = MakeDigits(top, "Counter");
            Button faceButton = MakeIconButton(top, "Face", sprites.FaceSmile, RequestNewRound, out faceImage);
            Layout(faceButton, w: 150);
            timerText = MakeDigits(top, "Timer");
            Button helpButton = MakeButton(top, "Help", "?", 72, ButtonColor, TextColor, ShowHelp, out _);
            Layout(helpButton, w: 130);

            // ---- tutorial: a bubble in the middle of the screen, on top of the board (outside the column
            // layout). LateUpdate moves it up or down so it never covers the highlighted cell.
            RectTransform tut = NewRect("Tutorial", g);
            tutorialRect = tut;
            tut.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tut.anchorMin = new Vector2(0, 0.5f);
            tut.anchorMax = new Vector2(1, 0.5f);
            tut.pivot = new Vector2(0.5f, 0.5f);
            tut.offsetMin = new Vector2(40, 0);
            tut.offsetMax = new Vector2(-40, 0);
            tut.anchoredPosition = Vector2.zero;
            tut.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            tutorialBox = tut.gameObject;
            AddImage(tut, PanelColor, sprites.Rounded);
            tut.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -10);
            var outline = tut.gameObject.AddComponent<Outline>();
            outline.effectColor = YellowColor;
            outline.effectDistance = new Vector2(4, -4);
            var tv = tut.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.padding = new RectOffset(40, 40, 30, 30);
            tv.spacing = 20;
            tv.childAlignment = TextAnchor.UpperCenter;
            tv.childControlWidth = tv.childControlHeight = true;
            tv.childForceExpandWidth = false;
            tv.childForceExpandHeight = false;

            RectTransform tutTextRt = NewRect("Text", tut);
            tutorialText = AddText(tutTextRt, "", 46, TextColor, FontStyle.Normal);
            Layout(tutTextRt, fw: 1);
            Button ok = MakeButton(tut, "TutorialButton", "Got it", 46, YellowColor, DarkText, OnTutorialButton, out tutorialOkLabel);
            Layout(ok, w: 340, h: 110);
            tutorialOkButton = ok.gameObject;
            tutorialBox.SetActive(false);

            // ---- board area (takes all remaining height)
            boardArea = NewRect("BoardArea", g);
            Layout(boardArea, fh: 1);
            boardArea.gameObject.AddComponent<RectMask2D>();   // a zoomed-in board is cut off at the bars
            boardRect = NewRect("Board", boardArea);
            boardRect.anchorMin = boardRect.anchorMax = boardRect.pivot = new Vector2(0.5f, 0.5f);
            grid = boardRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.spacing = Vector2.zero;
            grid.childAlignment = TextAnchor.UpperLeft;

            highlightFrame = NewRect("TutorialHighlight", boardRect);
            highlightFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image frame = AddImage(highlightFrame, Color.white, sprites.Frame);
            frame.raycastTarget = false;
            highlightFrame.gameObject.SetActive(false);

            // ---- bottom bar: Reveal / Flag mode
            RectTransform bottom = NewRect("BottomBar", g);
            AddImage(bottom, PanelColor, null);
            Layout(bottom, h: 190, fh: 0);
            var bh = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            bh.padding = new RectOffset(30, 30, 30, 40);
            bh.spacing = 24;
            bh.childControlWidth = bh.childControlHeight = true;
            bh.childForceExpandWidth = bh.childForceExpandHeight = true;

            Button reveal = MakeButton(bottom, "RevealMode", "Reveal", 52, ButtonColor, TextColor, () => SetFlagMode(false), out _);
            revealOutline = AddModeOutline(reveal);
            Button flag = MakeButton(bottom, "FlagMode", "    Flag", 52, ButtonColor, TextColor, () => SetFlagMode(true), out Text flagLabel);
            flagOutline = AddModeOutline(flag);
            RectTransform flagIcon = NewRect("Icon", flag.transform);
            flagIcon.anchorMin = flagIcon.anchorMax = new Vector2(0.5f, 0.5f);
            flagIcon.sizeDelta = new Vector2(70, 70);
            flagIcon.anchoredPosition = new Vector2(-70, 0);
            AddImage(flagIcon, Color.white, sprites.YellowFlag).raycastTarget = false;
            flagLabel.alignment = TextAnchor.MiddleCenter;

            // ---- end-of-round panel, above the bottom bar (outside the column layout)
            RectTransform end = NewRect("EndPanel", g);
            end.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            end.anchorMin = new Vector2(0, 0);
            end.anchorMax = new Vector2(1, 0);
            end.pivot = new Vector2(0.5f, 0);
            end.offsetMin = new Vector2(30, 0);
            end.offsetMax = new Vector2(-30, 0);
            end.anchoredPosition = new Vector2(0, 220);
            AddImage(end, PanelColor, sprites.Rounded);
            end.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -10);
            var ev = end.gameObject.AddComponent<VerticalLayoutGroup>();
            ev.padding = new RectOffset(40, 40, 40, 40);
            ev.spacing = 24;
            ev.childControlWidth = ev.childControlHeight = true;
            ev.childForceExpandWidth = true;
            ev.childForceExpandHeight = false;
            end.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            endTitle = AddText(NewRect("Title", end), "", 64, TextColor, FontStyle.Bold);
            endLine = AddText(NewRect("Line", end), "", 44, TextColor, FontStyle.Normal);
            RectTransform row = NewRect("Buttons", end);
            Layout(row, h: 120);
            var rh = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rh.spacing = 24;
            rh.childControlWidth = rh.childControlHeight = true;
            rh.childForceExpandWidth = rh.childForceExpandHeight = true;
            MakeButton(row, "PlayAgain", "Play again", 46, YellowColor, DarkText, NewRound, out _);
            MakeButton(row, "Levels", "Menu", 46, ButtonColor, TextColor, ShowMenu, out _);
            endPanel = end.gameObject;
            endPanel.SetActive(false);

            tutorialRect.SetAsLastSibling();      // the tutorial bubble draws on top of the board
            BuildLoupe(g);                        // and the magnifier on top of everything in the game screen
            gameScreen.SetActive(false);
        }

        // Small version number at the very bottom of every screen. It sits in the bottom padding of the
        // level screen and the bottom bar, so it never covers a button.
        void BuildVersionLabel(RectTransform app)
        {
            RectTransform v = NewRect("Version", app);
            v.anchorMin = new Vector2(0, 0);
            v.anchorMax = new Vector2(1, 0);
            v.pivot = new Vector2(0.5f, 0);
            v.sizeDelta = new Vector2(0, 40);
            v.anchoredPosition = Vector2.zero;
            AddText(v, "v" + MinesweeperVersion.Number, 26, new Color(TextColor.r, TextColor.g, TextColor.b, 0.5f), FontStyle.Normal);
        }

        // "How to play" (GDD 6.6): OK or a tap anywhere closes it; the tap never reaches the board.
        void BuildHelp(RectTransform app)
        {
            RectTransform dialog = MakeOverlay(app, "HowToPlay", out helpOverlay, closeOnTap: true);
            AddText(NewRect("Title", dialog), "How to play", 64, TextColor, FontStyle.Bold);

            // Four pages, one per tap: an animation with one short caption each.
            RectTransform demo = NewRect("Demo", dialog);
            RectTransform captionRt = NewRect("Caption", dialog);
            Text caption = AddText(captionRt, "", 48, TextColor, FontStyle.Bold);
            Layout(captionRt, h: 130);
            RectTransform pageRt = NewRect("Page", dialog);
            Text page = AddText(pageRt, "", 34, new Color(TextColor.r, TextColor.g, TextColor.b, 0.6f), FontStyle.Normal);
            Layout(pageRt, h: 44);
            helpDemo = demo.gameObject.AddComponent<MinesweeperHowToPlayDemo>();
            helpDemo.Init(sprites, font, caption, page, RedColor, YellowColor);

            Button next = MakeButton(dialog, "Next", "Next", 50, YellowColor, DarkText, HelpNext, out helpButtonLabel);
            Layout(next, h: 120);

            // × in the top-right corner closes How to play from any page (a tap elsewhere goes to the next page).
            Button close = MakeButton(dialog, "Close", "×", 72, ButtonColor, TextColor, HideHelp, out _);
            var closeRt = (RectTransform)close.transform;
            closeRt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(1, 1);
            closeRt.sizeDelta = new Vector2(100, 100);
            closeRt.anchoredPosition = new Vector2(-24, -24);
            helpCloseButton = close;
            helpOverlay.SetActive(false);
        }

        // A tap anywhere on How to play (or its button) shows the next page; after the last one it closes.
        void HelpNext()
        {
            if (!helpDemo.Next()) { HideHelp(); return; }
            UpdateHelpButton();
        }

        void UpdateHelpButton() =>
            helpButtonLabel.text = helpDemo.Page == MinesweeperHowToPlayDemo.PageCount - 1 ? "Got it" : "Next";

        // "Zoom in and out" (GDD 3.5): an animation and one short caption; only "Got it" closes it.
        void BuildZoomHint(RectTransform app)
        {
            RectTransform dialog = MakeOverlay(app, "ZoomHint", out zoomHintOverlay, closeOnTap: false);
            AddText(NewRect("Title", dialog), "Zoom in and out", 64, TextColor, FontStyle.Bold);
            bool touch = Application.isMobilePlatform;
            RectTransform demo = NewRect("Demo", dialog);
            demo.gameObject.AddComponent<MinesweeperZoomHintDemo>().Init(sprites, font, touch);
            RectTransform captionRt = NewRect("Caption", dialog);
            AddText(captionRt, touch
                ? "Pinch with two fingers to zoom.\nDrag with two fingers to move."
                : "Mouse wheel to zoom.\nHold the wheel and drag to move.", 44, TextColor, FontStyle.Normal);
            Layout(captionRt, h: 130);
            Layout(MakeButton(dialog, "GotIt", "Got it", 50, YellowColor, DarkText, HideZoomHint, out _), h: 120);
            zoomHintOverlay.SetActive(false);
        }

        // Pause (class rule: every game has a pause and "Exit to menu").
        void BuildPause(RectTransform app)
        {
            RectTransform dialog = MakeOverlay(app, "Pause", out pauseOverlay, closeOnTap: false);
            AddText(NewRect("Title", dialog), "Paused", 64, TextColor, FontStyle.Bold);
            Layout(MakeButton(dialog, "Resume", "Resume", 50, YellowColor, DarkText, () => pauseOverlay.SetActive(false), out _), h: 130);
            Layout(MakeButton(dialog, "Menu", "Menu", 50, ButtonColor, TextColor, RequestMenu, out _), h: 130);   // level screen
            Layout(MakeButton(dialog, "Exit", "Exit", 50, ButtonColor, TextColor, RequestExit, out _), h: 130);   // leave Red Flags
            pauseOverlay.SetActive(false);
        }

        // "Are you sure?" dialog. Only its buttons close it, so a stray tap can't decide for the player.
        // "Keep playing" is the big yellow button: the safe choice is the easy one.
        void BuildConfirm(RectTransform app)
        {
            RectTransform dialog = MakeOverlay(app, "Confirm", out confirmOverlay, closeOnTap: false);
            confirmTitle = AddText(NewRect("Question", dialog), "", 52, TextColor, FontStyle.Bold);
            Layout(MakeButton(dialog, "KeepPlaying", "Keep playing", 50, YellowColor, DarkText, ConfirmNo, out _), h: 130);
            Layout(MakeButton(dialog, "Yes", "", 50, RedColor, Color.white, ConfirmYes, out confirmYesLabel), h: 130);
            confirmOverlay.SetActive(false);
        }

        // ---- small UI helpers

        RectTransform MakeOverlay(RectTransform parent, string name, out GameObject overlay, bool closeOnTap)
        {
            RectTransform o = NewRect(name, parent);
            Stretch(o);
            Image dim = AddImage(o, new Color(0, 0, 0, 0.6f), null);
            overlay = o.gameObject;
            if (closeOnTap)
            {
                var close = o.gameObject.AddComponent<Button>();
                close.transition = Selectable.Transition.None;
                close.targetGraphic = dim;
                close.onClick.AddListener(HelpNext);
            }

            RectTransform d = NewRect("Dialog", o);
            d.anchorMin = d.anchorMax = d.pivot = new Vector2(0.5f, 0.5f);
            d.sizeDelta = new Vector2(920, 0);
            Image bg = AddImage(d, PanelColor, sprites.Rounded);
            bg.raycastTarget = !closeOnTap;      // for "tap anywhere closes", taps on the dialog go to the overlay
            var dv = d.gameObject.AddComponent<VerticalLayoutGroup>();
            dv.padding = new RectOffset(50, 50, 50, 50);
            dv.spacing = 30;
            dv.childControlWidth = dv.childControlHeight = true;
            dv.childForceExpandWidth = true;
            dv.childForceExpandHeight = false;
            d.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return d;
        }

        Text MakeDigits(RectTransform parent, string name)
        {
            RectTransform rt = NewRect(name, parent);
            AddImage(rt, Color.black, sprites.Rounded);
            Layout(rt, fw: 1);
            RectTransform t = NewRect("Text", rt);
            Stretch(t);
            Text text = AddText(t, "000", 72, RedColor, FontStyle.Bold);
            return text;
        }

        Button MakeButton(Transform parent, string name, string label, int fontSize, Color bg, Color fg, UnityAction onClick, out Text text)
        {
            RectTransform rt = NewRect(name, parent);
            Image img = AddImage(rt, bg, sprites.Rounded);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock colors = b.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            b.colors = colors;
            b.onClick.AddListener(onClick);

            RectTransform t = NewRect("Label", rt);
            Stretch(t);
            text = AddText(t, label, fontSize, fg, FontStyle.Bold);
            return b;
        }

        Button MakeIconButton(Transform parent, string name, Sprite icon, UnityAction onClick, out Image iconImage)
        {
            Button b = MakeButton(parent, name, "", 10, ButtonColor, TextColor, onClick, out _);
            RectTransform i = NewRect("Icon", b.transform);
            Stretch(i, 0.15f);
            iconImage = AddImage(i, Color.white, icon);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            return b;
        }

        Outline AddModeOutline(Button b)
        {
            var o = b.gameObject.AddComponent<Outline>();
            o.effectColor = YellowColor;
            o.effectDistance = new Vector2(5, -5);
            return o;
        }

        Text AddText(RectTransform rt, string s, int size, Color color, FontStyle style)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = s;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Image AddImage(RectTransform rt, Color color, Sprite sprite)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = new Vector2(inset, inset);
            rt.anchorMax = new Vector2(1f - inset, 1f - inset);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Layout(Component c, float w = -1, float h = -1, float fw = -1, float fh = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.flexibleWidth = fw;
            le.flexibleHeight = fh;
        }

        static Color Hex(int rgb) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        // =====================================================================
        // Self-test for the standalone build: RedFlags.exe -minesweeperSelfTest <folder>
        // Plays a short scripted round, saves screenshots and results, then quits.
        // =====================================================================

        static string SelfTestFolder()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-minesweeperSelfTest") return args[i + 1];
            return null;
        }

        IEnumerator SelfTest(string folder)
        {
            Directory.CreateDirectory(folder);
            var log = new List<string>();
            int hintSeenBefore = PlayerPrefs.GetInt(ZoomHintKey, 0);   // put back at the end
            PlayerPrefs.DeleteKey(ZoomHintKey);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Screenshot(folder, "1_levels");

            StartGame(MinesweeperDifficulty.Beginner);
            yield return null; yield return null; yield return null;
            log.Add($"tutorial step 1 shown: {tutorialBox.activeSelf && tutorialStep == 1} ({tutorialText.text})");
            log.Add($"Beginner {board.Width}×{board.Height} (expected 9×9), cell size {grid.cellSize.x:F0} units, magnifier for a finger: {LoupeFor(true)} (expected False)");
            yield return Screenshot(folder, "2_tutorial_step1");

            OnTap(4, 4);
            yield return null; yield return null; yield return null;
            log.Add($"tutorial step 2 shown: {tutorialBox.activeSelf && tutorialStep == 2} ({tutorialText.text})");
            log.Add($"tutorial bubble covers the highlighted cell: {TutorialBubbleCoversTarget()} (expected False), bubble y {tutorialRect.anchoredPosition.y:F0}");
            yield return Screenshot(folder, "3_tutorial_step2");

            // Worst case: highlight the cell in the very middle, right where the bubble would be.
            MinesweeperCellView realTarget = tutorialTarget;
            tutorialTarget = cells[board.Width / 2, board.Height / 2];
            yield return null; yield return null;
            log.Add($"middle cell highlighted: bubble covers it {TutorialBubbleCoversTarget()} (expected False), bubble moved to y {tutorialRect.anchoredPosition.y:F0}");
            yield return Screenshot(folder, "3b_tutorial_moved");
            tutorialTarget = realTarget;

            // Step 3: mark the obvious red flag. Step 4: good luck.
            OnTutorialButton();                                  // "Got it"
            yield return null; yield return null;
            MinesweeperCellView markTarget = tutorialTarget;
            log.Add($"tutorial step 3 shown: {tutorialStep == 3} ('{tutorialText.text}', button '{tutorialOkLabel.text}')");
            if (markTarget != null)
            {
                log.Add($"step 3 highlights a real red flag: {board.GetCell(markTarget.X, markTarget.Y).IsMine}, bubble covers it {TutorialBubbleCoversTarget()} (expected False)");
                yield return Screenshot(folder, "3c_tutorial_mark");
                OnSecondaryClick(markTarget.X, markTarget.Y);    // right-click = yellow flag
                yield return null; yield return null;
            }
            log.Add($"tutorial step 4 shown: {tutorialStep == 4} ('{tutorialText.text}', button '{tutorialOkLabel.text}')");
            yield return Screenshot(folder, "3d_tutorial_good_luck");
            OnTutorialButton();                                  // "Let's go!"
            log.Add($"tutorial closed after 'Let's go!': {!tutorialBox.activeSelf && tutorialStep == 0}");

            // How to play: one page per tap; a screenshot of each page mid-animation.
            ShowHelp();
            string[] pageShots = { "4a_help_tap", "4b_help_number", "4c_help_red_flag", "4d_help_yellow_flag" };
            float[] waits = { 1.6f, 1.6f, 1.2f, 1.6f };
            for (int p = 0; p < pageShots.Length; p++)
            {
                yield return new WaitForSecondsRealtime(waits[p]);
                log.Add($"help page {helpDemo.Page + 1}: '{helpDemo.GetComponentInParent<VerticalLayoutGroup>().transform.Find("Caption").GetComponent<Text>().text}', button '{helpButtonLabel.text}'");
                yield return Screenshot(folder, pageShots[p]);
                HelpNext();                                       // = a tap
            }
            log.Add($"help closed after the last page: {!helpOverlay.activeSelf}");

            // × closes it from any page.
            ShowHelp();
            HelpNext();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Screenshot(folder, "4e_help_close_button");
            int pageBefore = helpDemo.Page;
            helpCloseButton.onClick.Invoke();
            log.Add($"× on page {pageBefore + 1} closes How to play: {!helpOverlay.activeSelf}");

            Vector2Int? mine = null, safe = null;
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    MinesweeperCell c = board.GetCell(x, y);
                    if (c.IsMine && !c.IsFlagged && mine == null) mine = new Vector2Int(x, y);
                    if (!c.IsMine && !c.IsRevealed && !c.IsFlagged && safe == null) safe = new Vector2Int(x, y);
                }
            if (safe.HasValue)
            {
                OnLongPress(safe.Value.x, safe.Value.y);
                log.Add($"long press placed a yellow flag: {board.GetCell(safe.Value.x, safe.Value.y).IsFlagged}, counter {counterText.text}");
            }

            // Restart during a game must ask first; "Keep playing" keeps everything.
            RequestNewRound();
            yield return null;
            log.Add($"restart during a game asks first: {confirmOverlay.activeSelf} ('{confirmTitle.text}')");
            yield return Screenshot(folder, "5a_confirm");
            ConfirmNo();
            log.Add($"after 'Keep playing': state {state}, yellow flag kept {(safe.HasValue && board.GetCell(safe.Value.x, safe.Value.y).IsFlagged)}");
            ShowPause();
            RequestMenu();
            log.Add($"pause -> Menu asks first: {confirmOverlay.activeSelf} ('{confirmTitle.text}', button '{confirmYesLabel.text}'), pause closed: {!pauseOverlay.activeSelf}");
            ConfirmNo();
            ShowPause();
            RequestExit();
            log.Add($"pause -> Exit asks first: {confirmOverlay.activeSelf} ('{confirmTitle.text}', button '{confirmYesLabel.text}')");
            ConfirmNo();
            log.Add($"still in the same game after both 'Keep playing': {InGame && state == MinesweeperState.Playing}");
            OnTap(mine.Value.x, mine.Value.y);
            yield return null; yield return null;
            log.Add($"after tapping a red flag: state {state}, panel '{endTitle.text}', line '{endLine.text}'");
            yield return Screenshot(folder, "5_loss");
            bool ranAtOnce = false;
            ConfirmIfGameInProgress("test", "Yes", () => ranAtOnce = true);
            log.Add($"after the round is over, leaving doesn't ask: {ranAtOnce && !confirmOverlay.activeSelf}");
            RequestMenu();
            yield return null;
            log.Add($"after the round, pause Menu goes straight to the level screen: {menuScreen.activeSelf && !InGame}");
            StartGame(MinesweeperDifficulty.Beginner);
            yield return null;

            ShowPause();
            yield return null;
            yield return Screenshot(folder, "6_pause");
            pauseOverlay.SetActive(false);

            StartGame(MinesweeperDifficulty.Expert);
            yield return null; yield return null;
            log.Add($"second game, tutorial shown again: {tutorialBox.activeSelf} (expected False)");
            log.Add($"first Expert board: zoom hint shown {zoomHintOverlay.activeSelf} (expected True), counts as an open pop-up {OverlayOpen}");
            yield return new WaitForSecondsRealtime(1.3f);
            yield return Screenshot(folder, "7a_zoom_hint");
            OnBack();                                            // Back / Esc closes it
            log.Add($"Back closes the zoom hint: {!zoomHintOverlay.activeSelf}, still in the game {InGame}");
            NewRound();
            log.Add($"next Expert board: zoom hint shown again {zoomHintOverlay.activeSelf} (expected False)");
            float tallSize = Mathf.Floor(Mathf.Min((boardArea.rect.width - BoardMargin) / 16f, (boardArea.rect.height - BoardMargin) / 30f));
            log.Add($"Expert on {Screen.width}×{Screen.height}: arranged {board.Width}×{board.Height} = {board.Width * board.Height} cells (expected 480), cell size {grid.cellSize.x:F0} units, as 16×30 it would be {tallSize:F0}");
            OnTap(8, 15);
            yield return null; yield return null;
            yield return Screenshot(folder, "7_expert");

            // Aiming: the screen centre of a cell must map back to that same cell.
            log.Add($"Expert small cells: {SmallCells}; magnifier with a finger {LoupeFor(true)} (expected True), with a mouse {LoupeFor(false)} (expected False)");
            int W = board.Width, H = board.Height;
            Vector2Int[] probes = { new Vector2Int(0, 0), new Vector2Int(W - 1, H - 1), new Vector2Int(W / 2, H / 2), new Vector2Int(3, H - 8), new Vector2Int(W - 4, 4) };
            int hits = 0;
            foreach (var pr in probes)
            {
                RectTransform rt = cells[pr.x, pr.y].Rect;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                if (CellAt(screen) == cells[pr.x, pr.y]) hits++;
            }
            log.Add($"finger position -> cell is right for {hits}/{probes.Length} cells");

            // The magnifier over a cell in the middle and one at the top edge (it must stay on screen).
            foreach (var pr in new[] { new Vector2Int(8, 15), new Vector2Int(2, 0) })
            {
                RectTransform rt = cells[pr.x, pr.y].Rect;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                ShowLoupe(cells[pr.x, pr.y], screen);
                yield return null;
                var lc = new Vector3[4]; loupe.GetWorldCorners(lc);
                bool onScreen = lc[0].x >= 0 && lc[0].y >= 0 && lc[2].x <= Screen.width && lc[2].y <= Screen.height;
                log.Add($"magnifier for cell {pr.x},{pr.y}: shown {loupe.gameObject.activeSelf}, fully on screen {onScreen}, middle shows '{loupeLabels[1, 1].text}' = cell '{cells[pr.x, pr.y].NumberText}'");
                yield return Screenshot(folder, $"8_magnifier_{pr.x}_{pr.y}");
            }
            HideLoupe();

            // Zoom: in around a cell, it stays under the same spot; the view can't leave the board.
            {
                RectTransform rt = cells[W / 2, H / 2].Rect;
                Vector2 spot = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                ZoomAround(spot, 100f);                          // asks for far too much: clamped
                yield return null;
                Vector2 after = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                log.Add($"zoom in: {zoom:F2}× (max {MaxZoomNow:F2}), cells look {grid.cellSize.x * zoom:F0} units, same cell under the spot {CellAt(spot) == cells[W / 2, H / 2]} (moved {(after - spot).magnitude:F1} px), magnifier with a finger now {LoupeFor(true)} (expected False)");
                yield return Screenshot(folder, "10a_zoomed_in");
                PanBy(new Vector2(100000f, -100000f));           // try to drag far past the corner
                yield return null;
                var bc = new Vector3[4]; boardRect.GetWorldCorners(bc);
                var ac = new Vector3[4]; boardArea.GetWorldCorners(ac);
                bool covers = bc[0].x <= ac[0].x + 8f && bc[2].y >= ac[2].y - 8f;   // board's left and top edges still reach the area's
                log.Add($"dragged far past the top-left corner: board edge stops at the screen edge {covers}");
                yield return Screenshot(folder, "10b_zoomed_corner");
                RectTransform c0 = cells[0, 0].Rect;
                Vector2 s0 = RectTransformUtility.WorldToScreenPoint(null, c0.TransformPoint(c0.rect.center));
                log.Add($"zoomed in, cell 0,0 is hit by a tap on it: {CellAt(s0) == cells[0, 0]}");
                ZoomAround(spot, 0.01f);
                yield return null;
                log.Add($"zoom out: back to {zoom:F2}× with offset {pan} (expected 1.00×, (0.00, 0.00))");
            }

            // A wide (landscape) window: a new Expert board lies on its side and gets much bigger cells.
            int oldW = Screen.width, oldH = Screen.height;
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);
            StartGame(MinesweeperDifficulty.Expert);
            yield return null; yield return null; yield return null;
            log.Add($"Expert on {Screen.width}×{Screen.height}: arranged {board.Width}×{board.Height} = {board.Width * board.Height} cells, {board.MineCount} red flags (expected 30×16, 99), cell size {grid.cellSize.x:F0} units");
            yield return Screenshot(folder, "9a_expert_wide");

            // Once the game has started the shape stays, even if the window changes.
            OnTap(board.Width / 2, board.Height / 2);
            Screen.SetResolution(oldW, oldH, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
            bool fits = boardRect.rect.width <= boardArea.rect.width + 0.5f && boardRect.rect.height <= boardArea.rect.height + 0.5f;
            log.Add($"window made tall again mid-game: still {board.Width}×{board.Height} (expected 30×16), state {state}, board fits {fits}, cell size {grid.cellSize.x:F0}");
            yield return Screenshot(folder, "9b_expert_wide_then_tall");

            if (hintSeenBefore == 1) PlayerPrefs.SetInt(ZoomHintKey, 1); else PlayerPrefs.DeleteKey(ZoomHintKey);
            PlayerPrefs.Save();

            File.WriteAllLines(Path.Combine(folder, "selftest.txt"), log);
            Application.Quit();
        }

        static IEnumerator Screenshot(string folder, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
