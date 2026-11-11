# Minesweeper (Mobile): Technical Design

**Target:** Android phones, portrait orientation. Unity 6000.5.6f1, URP, Input System package. The code also runs in the editor with a mouse, so you can test quickly without a phone.

**Project:** the existing Unity project `GC26`, branch `gc26_minesweeper` of [Zaidejas/GC26](https://github.com/Zaidejas/GC26). It was created from the 3D URP template and already has the Input System (1.20) and Test Framework (1.7) packages.

**Theme:** the player avoids hidden **red flags** and marks suspicious cells with **yellow flags** (see GDD 1.1). The code keeps the classic names: "mine" (`IsMine`, `MineCount`) = hidden red flag, "flag" (`IsFlagged`, `ToggleFlag`) = the player's yellow flag. Only sprites and on-screen text use the new names.

## 1. Architecture

The main idea is to **separate the logic from the visuals**. The rules live in plain C# classes (no `MonoBehaviour`), so you can test them without pressing Play and they don't care whether input comes from a finger or a mouse. Unity scripts only display the board and pass on input.

```
                ┌──────────────────────┐   drag / pinch   ┌──────────────────┐
  Finger ─────► │   InputHandler       │ ───────────────► │ CameraController │ fit, zoom, pan
  (or mouse)    │ tap / long press /   │                  └──────────────────┘
                │ drag → grid (x, y)   │
                └─────────┬────────────┘
                          ▼
                ┌──────────────────────┐   events   ┌──────────────┐
                │   GameManager        │ ─────────► │  UIManager   │ timer, counter, face,
                │  (state, rules flow, │            └──────────────┘ flag-mode button
                │   flag mode, pause)  │ ─────────► AudioManager / Haptics
                └───┬─────────────┬────┘
                    ▼             ▼
           ┌──────────────┐  ┌──────────────┐
           │ Board (C#)   │  │ BoardView    │ spawns and refreshes TileViews
           │ Cell[,]      │  │ TileView[,]  │
           └──────────────┘  └──────────────┘
```

| Script | Folder | Type | Responsibility |
|---|---|---|---|
| `Cell` | Core | plain class | State of one cell |
| `Board` | Core | plain class | Mines, numbers, reveal, flood fill, flags, chord, win check |
| `GameState` | Core | enum | Ready / Playing / Won / Lost |
| `DifficultySettings` | Core | ScriptableObject | Width, height, mines |
| `GameSession` | Core | static class | Carries the chosen difficulty between scenes |
| `GameManager` | Managers | MonoBehaviour | Owns the Board, controls the state, flag mode, pause; raises events |
| `InputHandler` | Managers | MonoBehaviour | Turns touches into tap, long press or drag |
| `CameraController` | View | MonoBehaviour | Fits the board to a portrait screen; pinch zoom and pan |
| `BoardView` | View | MonoBehaviour | Creates the tile objects and redraws changed cells |
| `TileView` | View | MonoBehaviour | Chooses the sprite for one cell; pressed highlight |
| `SafeArea` | UI | MonoBehaviour | Keeps the UI clear of notches and rounded corners |
| `UIManager` | UI | MonoBehaviour | Mine counter, timer, face, flag-mode button, panels |
| `MainMenu` | UI | MonoBehaviour | Difficulty buttons, best times, settings, Back = quit |
| `AudioManager` | Managers | MonoBehaviour | Plays sound effects |
| `Haptics` | Managers | static class | Vibration, respecting the Vibration setting |
| `LossLines` | Core | static class | Reads the loss lines text file and picks one line at random |

## 2. Scenes
- `MainMenu`: title, three difficulty buttons with best times, settings button (sound, vibration). No Quit button; Android Back closes the app. **It is scene 0 in the Build Profiles scene list**, so the app always opens on the difficulty choice.
- `Game`: Main Camera (+ `CameraController`), `GameManager`, `BoardView`, `InputHandler`, `AudioManager`, and a Canvas with a `SafeArea` panel holding the top bar (counter, face, timer, menu) and the bottom bar (flag-mode toggle).

## 3. Project settings for mobile
| Setting | Value | Why |
|---|---|---|
| Platform | Android | Switch in File → Build Profiles |
| Default Orientation | Portrait | The whole design is portrait |
| Active Input Handling | Input System Package (New) | Used by `InputHandler`; already set in GC26 |
| Main Camera | Projection: Orthographic | The 3D template's camera is Perspective; `CameraController` needs Orthographic |
| 2D Sprite package | Add in Package Manager | Not in the 3D template; needed to slice sprite sheets |
| Canvas Scaler | Scale With Screen Size, 1080×1920, Match 0.5 | UI keeps the same proportions on every screen |
| Scripting Backend / Architecture | IL2CPP / ARM64 | Required by Google Play |
| Target frame rate | `Application.targetFrameRate = 60` | Android often defaults to 30 fps |
| Sprites | 128×128 px or larger, PPU = sprite size | Sharp on high-resolution screens; 1 tile = 1 world unit |
| Loss lines | `Assets/Text/loss_lines.txt`, UTF-8, one line per entry | Assigned to `UIManager` as a TextAsset; edit the file to add lines |

## 4. Core code skeletons

### Cell.cs (week 2)
```csharp
namespace MineSweeper.Core
{
    // Holds the data for ONE square on the board.
    // It is a plain C# class (not a MonoBehaviour), so it has no graphics;
    // it only remembers the state of the square.
    public class Cell
    {
        public bool IsMine;         // true if this square hides a mine
        public int AdjacentMines;   // how many of the 8 surrounding squares are mines (0–8)
        public bool IsRevealed;     // true once the player has opened this square
        public bool IsFlagged;      // true if the player has marked it with a flag
    }
}
```

### GameState.cs (week 6)
```csharp
namespace MineSweeper.Core
{
    // The possible phases of one game.
    // Ready   = board shown, but no tap yet (timer not running, mines not placed)
    // Playing = the first tap has happened, timer is running
    // Won     = every safe square is opened
    // Lost    = a mine was opened
    public enum GameState { Ready, Playing, Won, Lost }
}
```

### Board.cs (weeks 2, 5 and 9)
```csharp
using System.Collections.Generic;   // List<T>, Queue<T>
using UnityEngine;                  // Vector2Int, Mathf

namespace MineSweeper.Core
{
    // Board contains ALL the rules of Minesweeper.
    // It knows nothing about sprites, touches or the screen. This makes it
    // easy to test and easy to reason about.
    public class Board
    {
        // Size of the board and number of mines. "{ get; }" means the value
        // can be read from outside but only set in the constructor.
        public int Width { get; }
        public int Height { get; }
        public int MineCount { get; }

        // Counters that change during play. "private set" means only Board
        // itself can change them; other scripts can only read them.
        public int FlagCount { get; private set; }      // flags currently placed
        public int RevealedCount { get; private set; }  // safe squares opened so far
        public bool MinesPlaced { get; private set; }   // false until the first tap

        // The grid itself: a 2D array, accessed as cells[x, y].
        private readonly Cell[,] cells;

        // Random number generator used to place mines.
        private readonly System.Random rng;

        // Constructor: runs once when you write "new Board(...)".
        // "seed" is optional. Giving the same seed always produces the same
        // mine layout, which is useful for unit tests.
        public Board(int width, int height, int mines, int? seed = null)
        {
            Width = width; Height = height;

            // Make sure the mine count is sensible: at least 1, and leave
            // at least 9 squares free for the safe 3x3 area around the first tap.
            MineCount = Mathf.Clamp(mines, 1, width * height - 9);

            // Use the seed if one was given, otherwise a truly random generator.
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            // Create the array and fill every position with an empty Cell.
            cells = new Cell[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cells[x, y] = new Cell();
        }

        // Returns the Cell at (x, y). "=>" is a short way to write a one-line method.
        public Cell GetCell(int x, int y) => cells[x, y];

        // True if (x, y) is inside the board. Used to avoid reading outside the array.
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        // Gives back the coordinates of every neighbour of (x, y): up to 8 squares
        // (fewer at edges and corners). "yield return" hands out one result at a
        // time, so it can be used directly in a foreach loop.
        public IEnumerable<Vector2Int> Neighbours(int x, int y)
        {
            // dx and dy go through -1, 0, +1: left/same/right and down/same/up.
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;      // skip the square itself
                    int nx = x + dx, ny = y + dy;
                    if (InBounds(nx, ny)) yield return new Vector2Int(nx, ny);
                }
        }

        // Places the mines. It is called on the FIRST tap, not when the board
        // is created, so we can guarantee the first tap is never a mine.
        public void PlaceMines(int safeX, int safeY)
        {
            int placed = 0;
            while (placed < MineCount)
            {
                // Pick a random square.
                int x = rng.Next(Width), y = rng.Next(Height);

                // Is this square within 1 step of the first tap (the 3x3 safe area)?
                bool inSafeArea = Mathf.Abs(x - safeX) <= 1 && Mathf.Abs(y - safeY) <= 1;

                // Skip it if it already has a mine or is in the safe area, and try again.
                if (cells[x, y].IsMine || inSafeArea) continue;

                cells[x, y].IsMine = true;
                placed++;
            }

            // Now that the mines are known, work out the number for every square.
            CalculateNumbers();
            MinesPlaced = true;
        }

        // For every square, count how many neighbours are mines and store the result.
        private void CalculateNumbers()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int count = 0;
                    foreach (var n in Neighbours(x, y))
                        if (cells[n.x, n.y].IsMine) count++;
                    cells[x, y].AdjacentMines = count;
                }
        }

        // Opens the square at (x, y). If it is a 0 (no mines around it), it also
        // opens all its neighbours, and their neighbours, and so on. This is
        // called "flood fill".
        // Returns a list of every square that was opened, so the view only has
        // to redraw those squares instead of the whole board.
        public List<Vector2Int> Reveal(int x, int y)
        {
            var changed = new List<Vector2Int>();
            Cell start = cells[x, y];

            // Nothing happens when tapping an already-open or flagged square.
            if (start.IsRevealed || start.IsFlagged) return changed;

            // Breadth-first search (BFS): a queue of squares still to open.
            // A queue is used instead of recursion so that very large empty
            // areas cannot cause a stack overflow.
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(x, y));

            while (queue.Count > 0)
            {
                // Take the next square out of the queue.
                Vector2Int p = queue.Dequeue();
                Cell c = cells[p.x, p.y];

                // It may have been opened already through another neighbour,
                // and flagged squares are never opened automatically.
                if (c.IsRevealed || c.IsFlagged) continue;

                c.IsRevealed = true;
                changed.Add(p);

                // A mine was opened: the game is lost. GameManager checks the
                // returned list and handles the loss, so we just stop here.
                if (c.IsMine) break;

                RevealedCount++;    // one more safe square opened (used for the win check)

                // If this square is a 0, all its neighbours are safe:
                // put the hidden ones in the queue to be opened too.
                if (c.AdjacentMines == 0)
                    foreach (var n in Neighbours(p.x, p.y))
                        if (!cells[n.x, n.y].IsRevealed) queue.Enqueue(n);
            }
            return changed;
        }

        // Places a flag, or removes it if one is already there.
        // Returns true if something changed, false if the square was already open.
        public bool ToggleFlag(int x, int y)
        {
            Cell c = cells[x, y];
            if (c.IsRevealed) return false;   // you can't flag an open square

            c.IsFlagged = !c.IsFlagged;       // flip true <-> false
            FlagCount += c.IsFlagged ? 1 : -1; // keep the flag counter up to date
            return true;
        }

        // Week 9: "chording". When the player taps an open number and has
        // already placed that many flags around it, open all the other neighbours
        // in one go. If a flag is in the wrong place, this can open a mine.
        public List<Vector2Int> Chord(int x, int y)
        {
            var changed = new List<Vector2Int>();
            Cell c = cells[x, y];

            // Chording only works on an open square that shows a number (1–8).
            if (!c.IsRevealed || c.AdjacentMines == 0) return changed;

            // Count the flags around this square.
            int flags = 0;
            foreach (var n in Neighbours(x, y))
                if (cells[n.x, n.y].IsFlagged) flags++;

            // Only chord when the flag count exactly matches the number.
            if (flags != c.AdjacentMines) return changed;

            // Open every neighbour. Reveal() skips flagged and already-open squares.
            foreach (var n in Neighbours(x, y))
                changed.AddRange(Reveal(n.x, n.y));
            return changed;
        }

        // The game is won when every square that is NOT a mine has been opened.
        public bool IsWon => RevealedCount == Width * Height - MineCount;
    }
}
```

### DifficultySettings.cs (week 8)
```csharp
using UnityEngine;

namespace MineSweeper.Core
{
    // A ScriptableObject is a data file that lives in your Assets folder.
    // [CreateAssetMenu] adds "Create → MineSweeper → Difficulty" to the
    // right-click menu in the Project window, so you can make one asset per
    // difficulty (Beginner, Intermediate, Expert) and edit it in the Inspector.
    [CreateAssetMenu(menuName = "MineSweeper/Difficulty")]
    public class DifficultySettings : ScriptableObject
    {
        public string displayName = "Beginner";  // text shown in the menu
        public int width = 9;                    // number of columns
        public int height = 9;                   // number of rows
        public int mines = 10;                   // number of mines
    }

    // A static class exists only once and keeps its values when a new scene loads.
    // The main menu stores the player's choice here, and the Game scene reads it.
    public static class GameSession
    {
        public static DifficultySettings Selected;
    }
}
```
Presets for portrait screens: Beginner 9×9 with 10 mines, Intermediate 16×16 with 40, **Expert 16×30** with 99 (classic Expert turned upright so it fits a tall screen).

## 5. Unity-side skeletons

### TileView.cs (weeks 3 and 10)
```csharp
using UnityEngine;
using MineSweeper.Core;

namespace MineSweeper.View
{
    // Attached to the Tile prefab. It shows one square on screen by picking
    // the right sprite for the matching Cell.
    // [RequireComponent] makes Unity add a SpriteRenderer automatically if it is missing.
    [RequireComponent(typeof(SpriteRenderer))]
    public class TileView : MonoBehaviour
    {
        // [SerializeField] keeps the fields private in code but shows them in the
        // Inspector, so you can drag the sprites in.
        // Theme: "mine" sprites show the rectangular RED flag (the hazard),
        // "flag" sprites show the player's YELLOW flag.
        //   flag         = yellow flag on a hidden tile
        //   mine         = red flag on an open tile (shown after a loss)
        //   mineExploded = the red flag the player tapped (dark tile, bright border)
        //   wrongFlag    = crossed-out yellow flag on a safe cell (after a loss)
        [SerializeField] private Sprite hidden, empty, flag, mine, mineExploded, wrongFlag;

        // Sprites for the numbers. numbers[0] is "1", numbers[7] is "8".
        [SerializeField] private Sprite[] numbers = new Sprite[8];

        // Tint used while a finger is held on this tile (week 10).
        [SerializeField] private Color pressedTint = new Color(0.8f, 0.8f, 0.8f);

        private SpriteRenderer sr;

        // Awake runs once when the object is created. Store the SpriteRenderer
        // so we don't have to search for it every time.
        private void Awake() => sr = GetComponent<SpriteRenderer>();

        // Updates the sprite to match the cell's state.
        // gameOver: true after a loss, so hidden mines and wrong flags are shown.
        // exploded: true for the mine the player tapped (drawn in red).
        public void Refresh(Cell c, bool gameOver = false, bool exploded = false)
        {
            // Square is still closed.
            if (!c.IsRevealed)
            {
                if (gameOver && c.IsMine && !c.IsFlagged) sr.sprite = mine;           // show missed mines
                else if (gameOver && c.IsFlagged && !c.IsMine) sr.sprite = wrongFlag; // flag was wrong
                else sr.sprite = c.IsFlagged ? flag : hidden;                         // normal closed square
                return;
            }

            // Square is open.
            if (c.IsMine) sr.sprite = exploded ? mineExploded : mine;
            else sr.sprite = c.AdjacentMines == 0 ? empty : numbers[c.AdjacentMines - 1];
        }

        // Darkens the tile while a finger is held on it. Touch screens have no
        // "hover", so this is the feedback that shows which tile will be hit.
        public void SetPressed(bool pressed) => sr.color = pressed ? pressedTint : Color.white;
    }
}
```

### BoardView.cs (week 3)
```csharp
using System.Collections.Generic;
using UnityEngine;
using MineSweeper.Core;

namespace MineSweeper.View
{
    // Creates a Tile object for every square of the Board, and redraws tiles
    // when their cells change. It contains no game rules.
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private TileView tilePrefab;               // the Tile prefab, set in the Inspector
        [SerializeField] private CameraController cameraController; // fits the camera to the new board

        // One TileView per square, so tiles[x, y] matches board cell (x, y).
        private TileView[,] tiles;

        // Builds the visual board for a new game.
        public void Build(Board board)
        {
            Clear();   // remove tiles left over from the previous game

            tiles = new TileView[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                {
                    // Create a copy of the prefab at world position (x, y).
                    // Each tile is 1 unit wide, so grid coordinates = world coordinates.
                    // "transform" as the last argument makes the tile a child of this object.
                    var t = Instantiate(tilePrefab, new Vector3(x, y, 0), Quaternion.identity, transform);
                    t.Refresh(board.GetCell(x, y));
                    tiles[x, y] = t;
                }

            cameraController.Fit(board.Width, board.Height);
        }

        // Gives access to one tile (used for the pressed highlight).
        public TileView GetTile(int x, int y) => tiles[x, y];

        // Redraws only the listed squares (for example, the result of Board.Reveal).
        public void RefreshCells(Board board, IEnumerable<Vector2Int> cells)
        {
            foreach (var p in cells) tiles[p.x, p.y].Refresh(board.GetCell(p.x, p.y));
        }

        // Redraws every square. Used at the end of the game to show all mines.
        // exploded is the position of the mine that was tapped (if any).
        public void RefreshAll(Board board, bool gameOver, Vector2Int? exploded = null)
        {
            for (int x = 0; x < board.Width; x++)
                for (int y = 0; y < board.Height; y++)
                    tiles[x, y].Refresh(board.GetCell(x, y), gameOver,
                                        exploded.HasValue && exploded.Value == new Vector2Int(x, y));
        }

        // Destroys all child objects (the old tiles).
        private void Clear()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
        }
    }
}
```

### CameraController.cs (weeks 3 and 9)
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace MineSweeper.View
{
    // Controls the orthographic camera:
    //  - Fit():  shows the whole board on a portrait screen (week 3)
    //  - Pan():  moves the camera when a finger drags (week 9)
    //  - Update: pinch with two fingers to zoom (week 9)
    // The camera is never allowed to move away from the board.
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // Space (in world units = tiles) to keep free for the UI bars,
        // so the board is not hidden behind them.
        [SerializeField] private float topBarUnits = 2.5f;     // counter, face, timer
        [SerializeField] private float bottomBarUnits = 1.5f;  // flag-mode button
        [SerializeField] private float minZoomSize = 4f;       // most zoomed-in view (smaller = closer)

        private Camera cam;
        private float maxZoomSize;          // the "whole board visible" size from Fit()
        private int boardW, boardH;

        private void Awake() => cam = GetComponent<Camera>();

        // Centres the board and zooms out just enough to show all of it.
        public void Fit(int w, int h)
        {
            boardW = w; boardH = h;

            // orthographicSize is HALF the visible height, in world units.
            // Height needed: the board plus both UI bars.
            float sizeForHeight = (h + topBarUnits + bottomBarUnits) / 2f;
            // Width needed: the board plus half a tile of margin on each side.
            // The visible width is 2 * size * aspect, so divide by aspect.
            float sizeForWidth = (w + 1f) / 2f / cam.aspect;

            // On a tall phone the WIDTH is usually the limit, so this picks the larger value.
            maxZoomSize = Mathf.Max(sizeForHeight, sizeForWidth);
            cam.orthographicSize = maxZoomSize;

            // Tiles sit at 0..w-1, so the board centre is (w-1)/2. Shift the camera
            // so the board sits between the top and bottom bars. z = -10 keeps it in front.
            float yShift = (topBarUnits - bottomBarUnits) / 2f;
            cam.transform.position = new Vector3((w - 1) / 2f, (h - 1) / 2f + yShift, -10f);
        }

        // Moves the camera so the point under the finger follows the finger.
        public void Pan(Vector2 fromScreen, Vector2 toScreen)
        {
            Vector3 delta = cam.ScreenToWorldPoint(fromScreen) - cam.ScreenToWorldPoint(toScreen);
            cam.transform.position += new Vector3(delta.x, delta.y, 0f);
            ClampPosition();
        }

        // True while two fingers are on the screen. InputHandler checks this
        // so a pinch is never treated as a tap.
        public static bool IsPinching =>
            Touchscreen.current != null && Touchscreen.current.touches[1].isInProgress;

        private void Update()
        {
            if (!IsPinching) return;
            var t0 = Touchscreen.current.touches[0];
            var t1 = Touchscreen.current.touches[1];

            // Current finger positions, and where they were in the last frame.
            Vector2 p0 = t0.position.ReadValue(), p1 = t1.position.ReadValue();
            Vector2 prev0 = p0 - t0.delta.ReadValue(), prev1 = p1 - t1.delta.ReadValue();

            float prevDistance = Vector2.Distance(prev0, prev1);
            float distance = Vector2.Distance(p0, p1);
            if (prevDistance < 1f || distance < 1f) return;   // avoid dividing by ~0

            // Fingers moving apart → distance grows → size shrinks → zoom in.
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * prevDistance / distance,
                                               minZoomSize, maxZoomSize);
            ClampPosition();
        }

        // Keeps the visible area over the board. If the view is larger than the
        // board in one direction, the camera is centred in that direction instead.
        private void ClampPosition()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 p = cam.transform.position;

            p.x = ClampAxis(p.x, halfW, -0.5f, boardW - 0.5f);
            p.y = ClampAxis(p.y, halfH, -0.5f - bottomBarUnits, boardH - 0.5f + topBarUnits);
            cam.transform.position = p;
        }

        // min/max are the edges of the area that may be shown; half is half the view size.
        private static float ClampAxis(float value, float half, float min, float max)
        {
            if (max - min <= half * 2f) return (min + max) / 2f;   // view bigger than area → centre
            return Mathf.Clamp(value, min + half, max - half);
        }
    }
}
```

### GameManager.cs (weeks 6–9)
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using MineSweeper.Core;
using MineSweeper.View;

namespace MineSweeper.Managers
{
    // The "boss" of the Game scene. It creates the Board, receives taps from
    // InputHandler, asks the Board to apply the rules, tells BoardView to redraw,
    // and tells the UI when something changes (through events).
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        // Used when the Game scene is started directly in the editor (without the menu).
        [SerializeField] private DifficultySettings defaultDifficulty;

        public Board Board { get; private set; }
        public GameState State { get; private set; }
        public float ElapsedTime { get; private set; }   // seconds since the first tap

        // Flag mode: when true, a tap places a flag instead of revealing.
        // Toggled by the button in the bottom bar.
        public bool FlagMode { get; private set; }

        private bool appPaused;   // true while the app is in the background

        // Events: other scripts (like UIManager) can subscribe to these and get
        // notified, instead of checking the GameManager every frame.
        public event Action<GameState> StateChanged;   // fires when Ready/Playing/Won/Lost changes
        public event Action<int> MinesLeftChanged;      // fires with "mines minus flags"
        public event Action<bool> FlagModeChanged;      // fires when the flag-mode button is toggled

        // Start runs once when the scene begins: create the first game.
        private void Start() => NewGame();

        // Update runs every frame. Only count time while the game is being played
        // and the app is in the foreground.
        private void Update()
        {
            if (State == GameState.Playing && !appPaused)
                ElapsedTime = Mathf.Min(ElapsedTime + Time.deltaTime, 999f); // cap at 999 like the original
        }

        // Unity calls this when the app goes to the background (Home button,
        // phone call, screen lock) and again when it comes back.
        private void OnApplicationPause(bool paused) => appPaused = paused;

        // Starts a fresh game. Also called by the face (restart) button.
        // A new Board has every cell hidden and no mines yet, and nothing is
        // revealed here: the first cell opens only when the player taps.
        public void NewGame()
        {
            // Use the difficulty chosen in the menu, or the default if there is none.
            var d = GameSession.Selected != null ? GameSession.Selected : defaultDifficulty;

            Board = new Board(d.width, d.height, d.mines);
            ElapsedTime = 0;
            boardView.Build(Board);                      // create the tiles on screen
            SetState(GameState.Ready);
            MinesLeftChanged?.Invoke(Board.MineCount);   // "?." = only call if someone is listening
        }

        // Called by the flag-mode button in the bottom bar.
        public void ToggleFlagMode()
        {
            FlagMode = !FlagMode;
            FlagModeChanged?.Invoke(FlagMode);
        }

        // Called by InputHandler on a short tap.
        public void OnTap(int x, int y)
        {
            if (IsOver) return;

            // Tapping an open number always tries to chord, in both modes.
            if (Board.GetCell(x, y).IsRevealed) { OnChord(x, y); return; }

            if (FlagMode) OnFlag(x, y);
            else OnReveal(x, y);
        }

        // Called by InputHandler on a long press: always toggles a flag.
        public void OnLongPress(int x, int y)
        {
            if (IsOver) return;
            if (OnFlag(x, y)) Haptics.Vibrate();   // short buzz confirms the flag
        }

        private void OnReveal(int x, int y)
        {
            // A flagged cell is protected. Checking this first also stops a tap
            // on a flag from placing the mines and starting the timer.
            if (Board.GetCell(x, y).IsFlagged) return;

            // On the very first tap: place the mines (avoiding this square)
            // and start the timer.
            if (!Board.MinesPlaced)
            {
                Board.PlaceMines(x, y);
                SetState(GameState.Playing);
            }

            var changed = Board.Reveal(x, y);           // apply the rules
            boardView.RefreshCells(Board, changed);     // redraw only what changed
            CheckEnd(changed);                          // did we win or lose?
        }

        // Returns true if a flag was placed or removed.
        private bool OnFlag(int x, int y)
        {
            if (!Board.ToggleFlag(x, y)) return false;

            // Redraw this one tile and update the mine counter.
            boardView.RefreshCells(Board, new[] { new Vector2Int(x, y) });
            MinesLeftChanged?.Invoke(Board.MineCount - Board.FlagCount);
            return true;
        }

        private void OnChord(int x, int y)
        {
            var changed = Board.Chord(x, y);
            boardView.RefreshCells(Board, changed);
            CheckEnd(changed);
        }

        private bool IsOver => State == GameState.Won || State == GameState.Lost;

        // Looks at the squares that were just opened to decide if the game has ended.
        private void CheckEnd(List<Vector2Int> changed)
        {
            // If any opened square is a mine, the player has lost.
            foreach (var p in changed)
                if (Board.GetCell(p.x, p.y).IsMine)
                {
                    boardView.RefreshAll(Board, gameOver: true, exploded: p); // show all mines
                    Haptics.Vibrate();
                    SetState(GameState.Lost);
                    return;
                }

            // Otherwise check whether all safe squares are open.
            if (Board.IsWon) SetState(GameState.Won); // week 6: also auto-flag the mines here
        }

        // Changes the state and notifies any listeners (UI, audio, ...).
        private void SetState(GameState s)
        {
            State = s;
            StateChanged?.Invoke(s);
        }
    }
}
```

### InputHandler.cs (weeks 4 and 9)
Uses the **Input System** package. `Pointer.current` is the touch screen on a phone and the mouse in the editor, so the same code works in both.
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MineSweeper.View;

namespace MineSweeper.Managers
{
    // Turns a finger (or mouse) into one of three gestures:
    //   tap        = finger down and up quickly, without moving  → GameManager.OnTap
    //   long press = finger held still for longPressTime seconds → GameManager.OnLongPress
    //   drag       = finger moved more than dragThreshold pixels → CameraController.Pan
    public class InputHandler : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private GameManager game;
        [SerializeField] private BoardView boardView;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private float longPressTime = 0.4f;     // seconds; tune on a real phone
        [SerializeField] private float dragThreshold = 20f;      // pixels before a touch becomes a drag

        // State of the current touch.
        private bool pressing;          // a finger is down
        private bool startedOverUI;     // the touch began on a button → ignore it here
        private bool dragging;          // finger moved too far → it's a pan, not a tap
        private bool longPressDone;     // long press already fired for this touch
        private float pressStartTime;
        private Vector2 pressStartPos, lastPos;
        private TileView pressedTile;   // tile currently shown as pressed

        // UIManager reads this to show the 😮 face while a finger is on the board.
        public bool IsHoldingOnBoard => pressing && !startedOverUI && !dragging;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            // Two fingers = pinch zoom (handled by CameraController). Cancel the
            // current touch so lifting the fingers doesn't count as a tap.
            if (CameraController.IsPinching) { CancelTouch(); return; }

            Vector2 pos = pointer.position.ReadValue();

            // Finger went down this frame: start tracking a new touch.
            if (pointer.press.wasPressedThisFrame)
            {
                pressing = true;
                dragging = false;
                longPressDone = false;
                pressStartTime = Time.unscaledTime;
                pressStartPos = lastPos = pos;
                startedOverUI = IsOverUI(pos);
                if (!startedOverUI) SetPressedTile(pos);
            }

            if (!pressing) return;

            // Touches that start on a UI button belong to the button, not the board.
            if (startedOverUI)
            {
                if (pointer.press.wasReleasedThisFrame) pressing = false;
                return;
            }

            // Moved far enough from where it started → it's a drag.
            if (!dragging && Vector2.Distance(pos, pressStartPos) > dragThreshold)
            {
                dragging = true;
                ClearPressedTile();
            }

            if (dragging)
            {
                cameraController.Pan(lastPos, pos);   // move the view with the finger
            }
            else if (!longPressDone && Time.unscaledTime - pressStartTime >= longPressTime)
            {
                // Held still long enough → long press (flag).
                longPressDone = true;
                ClearPressedTile();
                if (TryGetCell(pos, out int fx, out int fy)) game.OnLongPress(fx, fy);
            }
            lastPos = pos;

            // Finger lifted: if it wasn't a drag or a long press, it was a tap.
            if (pointer.press.wasReleasedThisFrame)
            {
                if (!dragging && !longPressDone && TryGetCell(pos, out int tx, out int ty))
                    game.OnTap(tx, ty);
                CancelTouch();
            }
        }

        // Converts a screen position (pixels) to a grid cell. Tiles are 1 unit
        // wide and sit at whole-number positions, so rounding gives the cell.
        private bool TryGetCell(Vector2 screenPos, out int x, out int y)
        {
            Vector3 world = cam.ScreenToWorldPoint(screenPos);
            x = Mathf.RoundToInt(world.x);
            y = Mathf.RoundToInt(world.y);
            return game.Board.InBounds(x, y);
        }

        // True if a UI element (button, panel) is under the given screen position.
        // A raycast is used because it works reliably for touches as well as the mouse.
        private static bool IsOverUI(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            return results.Count > 0;
        }

        // Shows the pressed highlight on the hidden tile under the finger.
        private void SetPressedTile(Vector2 screenPos)
        {
            ClearPressedTile();
            if (TryGetCell(screenPos, out int x, out int y) && !game.Board.GetCell(x, y).IsRevealed)
            {
                pressedTile = boardView.GetTile(x, y);
                pressedTile.SetPressed(true);
            }
        }

        private void ClearPressedTile()
        {
            if (pressedTile != null) pressedTile.SetPressed(false);
            pressedTile = null;
        }

        // Forgets the current touch.
        private void CancelTouch()
        {
            pressing = false;
            dragging = false;
            ClearPressedTile();
        }
    }
}
```

### Haptics.cs (week 10)
```csharp
using UnityEngine;

namespace MineSweeper.Managers
{
    // Small helper for vibration. It respects the Vibration setting, and does
    // nothing in the editor (where there is no motor to vibrate).
    public static class Haptics
    {
        // Read from the settings saved by the menu; 1 = on (default), 0 = off.
        public static bool Enabled => PlayerPrefs.GetInt("vibration", 1) == 1;

        public static void Vibrate()
        {
            if (!Enabled) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();   // Unity's built-in vibration (about half a second on Android)
#endif
        }
    }
}
```
> `Handheld.Vibrate()` gives one fixed-length buzz. For shorter, lighter "ticks" you would need a native Android plugin or a haptics package; that's a possible improvement, not part of this plan.

### SafeArea.cs (week 7)
```csharp
using UnityEngine;

namespace MineSweeper.UI
{
    // Put this on a full-screen panel inside the Canvas, and place all UI under it.
    // It shrinks the panel to Screen.safeArea, the part of the screen that is not
    // covered by a notch, camera hole or rounded corners.
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform rt;
        private Rect lastSafeArea;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            Apply();
        }

        // The safe area can change (e.g. when the device rotates), so check each frame.
        private void Update()
        {
            if (Screen.safeArea != lastSafeArea) Apply();
        }

        private void Apply()
        {
            lastSafeArea = Screen.safeArea;

            // Convert the safe area from pixels to 0..1 anchor values.
            Vector2 min = lastSafeArea.position;
            Vector2 max = lastSafeArea.position + lastSafeArea.size;
            min.x /= Screen.width;  min.y /= Screen.height;
            max.x /= Screen.width;  max.y /= Screen.height;

            rt.anchorMin = min;
            rt.anchorMax = max;
        }
    }
}
```

### LossLines.cs (week 7)
```csharp
using System;
using System.Linq;

namespace MineSweeper.Core
{
    // The lines shown under the "lost" message. They come from a plain text
    // file (Assets/Text/loss_lines.txt), one line per entry, so new lines can
    // be added later by editing the file — no code changes needed.
    // It is plain C# (no MonoBehaviour), so it can be unit-tested.
    public static class LossLines
    {
        // Splits the file text into lines. Removes spaces at the ends, skips
        // empty lines and comment lines that start with "#".
        public static string[] Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            return text.Split('\n')
                       .Select(line => line.Trim())          // also removes the '\r' of Windows line endings
                       .Where(line => line.Length > 0 && !line.StartsWith("#"))
                       .ToArray();
        }

        // Returns one random line, or "" if the list is empty.
        // "random" is passed in so tests can use a fixed seed.
        public static string PickRandom(string[] lines, Random random)
        {
            if (lines == null || lines.Length == 0) return "";
            return lines[random.Next(lines.Length)];
        }
    }
}
```
Example `loss_lines.txt`:
```
# One line per entry. Lines starting with # are not shown.
That red flag was not a suggestion.
Next time, trust the numbers.
Red flags are easier to see from a distance.
```

### UIManager.cs (weeks 7–10): outline
- Subscribe to `GameManager.StateChanged`, `MinesLeftChanged` and `FlagModeChanged` in `OnEnable`, and unsubscribe in `OnDisable`
- In `Update`, set `timerText.text = Mathf.FloorToInt(game.ElapsedTime).ToString("000")`
- Face sprite: 😮 when `inputHandler.IsHoldingOnBoard` is true, otherwise based on the state (🙂 / 😎 / 😵); the face button's `onClick` calls `game.NewGame()`
- The flag-mode button's `onClick` calls `game.ToggleFlagMode()`; its icon switches between ⛏ and the yellow flag on `FlagModeChanged`
- **Loss line:** a `[SerializeField] private TextAsset lossLinesFile;` field. In `Awake`, `lines = LossLines.Parse(lossLinesFile != null ? lossLinesFile.text : "")`. When `StateChanged` reports `Lost`, set the loss panel's second text to `LossLines.PickRandom(lines, rng)` and hide that text if it's empty. The text box uses TextMeshPro word wrapping
- Player-facing texts say "red flags", never "mines" (counter, menu cards, panels)
- **Android Back button:** in `Update`, `if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)` load the `MainMenu` scene. In the menu the same check calls `Application.Quit()`

## 6. Unit test example (week 11)
The Test Framework package is already installed in GC26. Create `Assets/Tests/EditMode/` with an assembly definition that references the `Core` assembly. This means `Scripts/Core` also needs its own `.asmdef`. Because `Board` doesn't depend on input, the same tests work for the mobile game.
```csharp
using NUnit.Framework;     // the testing library used by Unity Test Framework
using MineSweeper.Core;

// Edit Mode tests run in the Test Runner window without pressing Play.
// Each method marked [Test] is one test. Assert.* checks a condition and
// makes the test fail if it is not true.
public class BoardTests
{
    [Test]
    public void PlaceMines_PlacesExactCount_AndKeepsFirstTapSafe()
    {
        // Arrange: a 9x9 board with 10 mines. The fixed seed (42) makes the
        // layout the same every run, so the test is repeatable.
        var b = new Board(9, 9, 10, seed: 42);

        // Act: pretend the first tap was in the centre.
        b.PlaceMines(4, 4);

        // Assert 1: count all mines on the board; there must be exactly 10.
        int mines = 0;
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                if (b.GetCell(x, y).IsMine) mines++;
        Assert.AreEqual(10, mines);

        // Assert 2: the 3x3 area around the first tap must have no mines.
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                Assert.IsFalse(b.GetCell(4 + dx, 4 + dy).IsMine);
    }

    [Test]
    public void FlaggedCell_CannotBeRevealed()
    {
        var b = new Board(9, 9, 10, seed: 1);
        b.PlaceMines(0, 0);

        // Flag a square, then try to open it.
        b.ToggleFlag(8, 8);

        // Reveal should do nothing, so the list of changed squares is empty.
        Assert.IsEmpty(b.Reveal(8, 8));
    }
}
```
More tests to write: the numbers match a hand-built board, flood fill stops at numbered cells, `IsWon` becomes true after all safe cells are revealed, chording does nothing when the flag count is wrong, the Expert board (16×30) gets exactly 99 mines, and `LossLines.Parse` skips empty and `#` lines while `PickRandom` returns "" for an empty list.

## 7. Conventions
- Namespaces: `MineSweeper.Core`, `.View`, `.Managers`, `.UI`
- Use `[SerializeField] private` fields instead of `public` fields for Inspector references
- Grid origin is (0, 0) at the bottom left; x increases to the right and y increases upward
- All gesture timings and sizes (`longPressTime`, `dragThreshold`, zoom limits) are Inspector fields so you can tune them on the phone without changing code
- Commit messages in the form `Week 5: flood fill reveal`
