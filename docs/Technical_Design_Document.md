# Red Flags: Technical Design Document

**Target:** Android phones, portrait orientation. Unity 6000.5.6f1, URP, Input System package. The code also runs in the editor with a mouse, so you can test quickly without a phone.

**Project:** the existing Unity project `GC26`, branch `gc26_minesweeper` of [Zaidejas/GC26](https://github.com/Zaidejas/GC26). It was created from the 3D URP template and already has the Input System (1.20) and Test Framework (1.7) packages.

**Theme:** the player avoids hidden **red flags** and marks suspicious cells with **yellow flags** (see Game_Design_Document.md, section 1.1). The code keeps the classic names: "mine" (`IsMine`, `MineCount`) = hidden red flag, "flag" (`IsFlagged`, `ToggleFlag`) = the player's yellow flag. Only sprites and on-screen text use the new names.

**Related documents:** [Game_Design_Document.md](Game_Design_Document.md) (rules, screens, controls), [prototype/](prototype/README.md), [task board](https://github.com/users/Zaidejas/projects/6) (all tasks from section 9 as cards).

**Get the project on a new computer:** `git clone -b gc26_minesweeper https://github.com/Zaidejas/GC26.git`, then open the `GC26` folder in Unity Hub (Add → Add project from disk). These documents are in `GC26/docs`.

**Contents:** 1–7 architecture and code · 8 testing · 9 development plan and tasks · 10 progress tracking

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
- `Game`: Main Camera (+ `CameraController`), `GameManager`, `BoardView`, `InputHandler`, `AudioManager`, and a Canvas with a `SafeArea` panel holding the top bar (menu, counter, face, timer, **?** help button), the bottom bar (flag-mode toggle), and the hidden "How to play" pop-up.

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
- **"How to play" pop-up** (Game_Design_Document.md, section 6.6): a full-screen, semi-transparent `Image` with a centred panel (title, text, OK button) as its child, last in the Canvas so it draws on top. The **?** button's `onClick` shows it. A `Button` on the full-screen image (covering the panel too) and the OK button both hide it, so a tap anywhere closes it; because the image blocks raycasts, `InputHandler`'s `IsOverUI` check stops that tap from reaching the board. While it's open, `GameManager` pauses the timer (like `appPaused`), and the Back button closes it before going to the menu
- **First-game tutorial** (Game_Design_Document.md, section 6.5): a bubble panel (TextMeshPro text + "Got it" button) anchored directly **under the top bar, above the board**. While it's visible, `CameraController.Fit` uses a larger top margin (top bar + bubble height), so the board sits below the bubble and no cell is covered; when it closes, fit again with the normal margin. In `Start`, if `PlayerPrefs.GetInt("tutorial_done", 0) == 0`, show step 1. When `StateChanged` reports `Playing` (the first reveal), find the revealed non-mine cell with the biggest `AdjacentMines` (first found on a tie), call `TileView.SetTutorialHighlight(true)` on it (a pulsing yellow outline sprite), show step 2 with that number, and save `tutorial_done = 1`. Close it on "Got it", on the next `OnTap`/`OnLongPress`, or on leaving the scene
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

## 8. Testing

Automated tests for `Board` and `LossLines` are in section 6. Everything below is checked by hand **on a real phone** (week 11) and on the release build (week 12).

### 8.1 Test devices

| Device | Android version | Screen (resolution, aspect) | Notch? | Tested in week |
|---|---|---|---|---|
| | | | | |
| | | | | |

Device Simulator profiles to check: a 16:9 phone, a tall 20:9 phone with a notch, a 4:3 tablet.

### 8.2 Manual test checklist

#### Gameplay
- [ ] The first tap never hits a mine, on all three difficulties
- [ ] The first tap on Beginner usually opens an area
- [ ] Numbers are correct, including at corners and edges
- [ ] Flood fill opens empty regions and stops at numbers
- [ ] Flood fill doesn't open flagged cells
- [ ] Tapping a flagged cell in reveal mode does nothing
- [ ] You can't flag an opened cell
- [ ] Chording works only when the flag count equals the number
- [ ] Chording next to a wrong flag causes a loss
- [ ] On a loss, all mines show, the tapped mine is red, and wrong flags are marked
- [ ] On a win, all mines are auto-flagged
- [ ] The board can't be changed after a win or loss (zoom and pan still work)

#### Touch
- [ ] A quick tap always opens the cell under the finger, also near the board edges
- [ ] Long press flags after about 0.4 s, with a vibration
- [ ] In flag mode a tap flags; long press still flags in both modes
- [ ] Dragging pans the view and never opens or flags a cell
- [ ] Pinch zooms in and out and never opens or flags a cell
- [ ] The view can't be panned away from the board, and can't zoom out further than the whole board
- [ ] Expert cells are comfortable to tap when zoomed in
- [ ] The tile under the finger looks pressed and the face shows 😮; both reset on lift or drag
- [ ] Tapping a button never opens the cell behind it

#### UI and screens
- [ ] The mine counter goes down and up with flags and can go negative
- [ ] The timer starts on the first reveal, stops at the end, and caps at 999
- [ ] The face changes correctly (normal / pressed / win / lose) and restarts on tap
- [ ] Nothing is hidden behind the notch, camera hole, rounded corners or navigation bar
- [ ] All buttons are easy to hit with a thumb
- [ ] Layout is correct on every device in the Device Simulator list above
- [ ] Rotating the phone doesn't change anything (portrait only)
- [ ] The menu shows no instructions text
- [ ] First game after installing (any difficulty): the tutorial asks for a tap; after it, the opened cell with the biggest number is highlighted and the message shows that number
- [ ] Both tutorial messages appear above the board (never below it or over it), and taps on the board still work while they're shown
- [ ] "Got it" or the next move closes the tutorial; it doesn't appear in later games
- [ ] The **?** button (top right) opens "How to play" with the four rules and an OK button
- [ ] OK, or a tap anywhere on the screen, closes it; the closing tap never opens or flags a cell
- [ ] The timer doesn't count while "How to play" is open
- [ ] All five top-bar items fit on the narrowest test phone

#### Android behaviour
- [ ] Back in the game returns to the menu; Back in the menu closes the app; Back with "How to play" open only closes the pop-up
- [ ] Home button, then return: the round continues and the timer didn't count the time away
- [ ] Incoming phone call or screen lock mid-round: same as above
- [ ] Sound follows the phone's media volume; the Sound setting mutes it
- [ ] The Vibration setting turns vibration off

#### Menu and saved data
- [ ] Each difficulty loads the correct size and mine count (Expert = 16×30, 99 mines)
- [ ] The best time is saved only after a win, and only if it's better
- [ ] Best times, Sound and Vibration settings and "tutorial done" survive closing the app completely
- [ ] Menu → Game → Menu works repeatedly with no errors

#### Release build (week 12)
- [ ] The signed APK installs and opens on a phone that isn't connected to Unity
- [ ] The app icon and name look right on the home screen
- [ ] The game runs smoothly (60 fps) and the phone doesn't get noticeably warm
- [ ] No errors in the device log (Android Logcat package in Unity, filtered to the app)
- [ ] The keystore and its passwords are backed up outside the Git repository

## 9. Development plan and tasks

**Time budget:** 13 weeks × 2 hours/week = **26 hours total**
**Start:** week of 26 Oct 2026 → **Finish:** week of 18 Jan 2027 (adjust the dates to your actual start)

### 9.1 Session routine

Use the same rhythm every week so no time is lost:

| Minutes | Activity |
|---|---|
| 0–10 | Read last week's notes (section 10.1), open the project, `git pull` |
| 10–100 | Do the week's tasks |
| 100–115 | Test in the **Device Simulator** (Window → General → Device Simulator); from week 4 on, also **Build And Run** to your phone at least once a week |
| 115–120 | `git commit` and `git push`, write 2–3 lines in the weekly log (section 10.1): what's done, what's next, blockers |

**Rule:** if a task takes longer than planned, cut a "Nice to have" item. Don't skip testing or committing.

**Why test on a real phone often:** the Device Simulator shows screen size and notches correctly, but touch feel (tap accuracy, long-press timing, pinch) can only be judged with a real finger on a real screen.

### 9.2 Milestones

| Milestone | Week | Result |
|---|---|---|
| M1: Setup and logic | 2 | An empty app runs on the phone; board logic prints to the Console |
| M2: Visible board | 4 | You can tap and long-press cells on the phone |
| M3: Playable MVP | 7 | A complete game on the phone you can win and lose, with UI |
| M4: Feature complete | 10 | Menu, difficulties, zoom/pan, chording, sound, vibration, best times |
| M5: Release | 13 | Tested, signed Android build (APK/AAB) |

### 9.3 Week-by-week plan and tasks

**How to read this list**
- **Estimate:** minutes of focused work. Each week's tasks add up to **90 minutes**. The other 30 minutes of the 2-hour session go to the routine: 10 min reviewing notes, 15 min testing (Device Simulator and phone), 5 min committing.
- **Priority:** **Must** = needed for the MVP or release · **Should** = planned feature · **Could** = optional; drop it first if you're behind.
- **Depends on:** tasks that must be finished first.
- **Done when:** how you know the task is finished.
- Track progress on the [task board](https://github.com/users/Zaidejas/projects/6); the IDs (MS-01 …) are the same.

**Totals:** 66 tasks · 19.5 hours of task work + 6.5 hours of session routine = 26 hours.

#### Month 1: Foundations

##### Week 1: Setup, first phone build, C# refresher

**Goal:** a working project that already runs on your phone. Getting the Android toolchain working early removes the biggest technical risk.
- Install **Unity 6000.5.6f1** (the version the GC26 project uses) in Unity Hub with the **Android Build Support** module, including **OpenJDK** and **Android SDK & NDK Tools**
- Install Visual Studio 2022 Community with the "Game development with Unity" workload, or VS Code with the Unity extension
- Open the existing `GC26` project and make sure the branch `gc26_minesweeper` is checked out (`git status`). On a new computer, first run `git clone https://github.com/Zaidejas/GC26.git` and `git checkout gc26_minesweeper`
- File → Build Profiles → **Switch Platform to Android**
- Player Settings → Resolution and Presentation → Default Orientation: **Portrait**
- The project was created from the 3D URP template, so set the Main Camera's **Projection to Orthographic** and add the **2D Sprite** package (Package Manager) for slicing sprites
- Create the folder structure: `Assets/Scripts/{Core,View,UI,Managers}`, `Assets/Sprites`, `Assets/Prefabs`, `Assets/Audio` (`Assets/Scenes` and `Assets/Settings` already exist)
- Git is already set up: check that `.gitignore` excludes `Library`, `Temp` and `Logs`, then commit the setup and push to `gc26_minesweeper`
- On the phone: enable **Developer options** (tap "Build number" 7 times) and **USB debugging**; connect by USB and use **Build And Run** on an empty scene
- C# refresher: classes, 2D arrays `int[,]`, `List<T>`, `Queue<T>`, enums, `Random`, MonoBehaviour basics (`Start`, `Update`, `[SerializeField]`)

**Done when:** an empty app opens on your phone and the setup commit is on GitHub in `gc26_minesweeper`.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-01 | Install Unity 6000.5.6f1 (the GC26 project's version) with Android Build Support (OpenJDK, Android SDK & NDK Tools) | 20 | Must | — | Unity 6000.5.6f1 with the Android module appears under Installs in Unity Hub |
| ☐ | MS-02 | Install Visual Studio 2022 (Unity workload) or VS Code + Unity extension, and set it as the external script editor | 10 | Must | MS-01 | Double-clicking a script in Unity opens it with autocomplete working |
| ☐ | MS-03 | Prepare the GC26 project on branch `gc26_minesweeper`: switch platform to Android, set Portrait, make the camera orthographic, add the 2D Sprite package, create the folders | 15 | Must | MS-01 | Platform shows Android, orientation is Portrait, camera is orthographic; `Scripts/{Core,View,UI,Managers}`, `Sprites`, `Prefabs`, `Scenes`, `Audio` exist |
| ☐ | MS-04 | Check Git for the `gc26_minesweeper` branch: `.gitignore` excludes `Library`/`Temp`/`Logs`; commit the setup and push | 15 | Must | MS-03 | The setup commit is on GitHub in `gc26_minesweeper` and the `Library` folder is not in it |
| ☐ | MS-05 | Enable Developer options and USB debugging on the phone; Build And Run an empty scene | 20 | Must | MS-03 | The empty app opens on the phone |
| ☐ | MS-06 | C# refresher: classes, `int[,]`, `List<T>`, `Queue<T>`, enums, `Random`, MonoBehaviour basics | 10 | Must | — | You can explain what `Start`, `Update` and `[SerializeField]` do |

##### Week 2: Board data model (pure C#)

**Goal:** game logic that doesn't depend on graphics or platform.
- Write the `Cell` class (fields: `IsMine`, `AdjacentMines`, `IsRevealed`, `IsFlagged`)
- Write the `Board` class: constructor (width, height, mines), `Cell[,]` grid, `InBounds(x, y)`
- Implement `PlaceMines(safeX, safeY)`, which places mines randomly and skips the 3×3 area around the first tap
- Implement `CalculateNumbers()`, which counts mines in the 8 neighbours of each cell
- Add a temporary `BoardDebug` MonoBehaviour that builds a board and prints it to the Console with `Debug.Log`

**Done when:** pressing Play prints a correct grid of mines (`*`) and numbers. ✅ **Milestone M1**

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-07 | Write the `Cell` class | 10 | Must | MS-03 | The class has `IsMine`, `AdjacentMines`, `IsRevealed`, `IsFlagged` |
| ☐ | MS-08 | Write the `Board` class: constructor, `Cell[,]` grid, `GetCell`, `InBounds`, `Neighbours` | 25 | Must | MS-07 | A board of any size can be created; corners return 3 neighbours |
| ☐ | MS-09 | Implement `PlaceMines(safeX, safeY)` with the 3×3 safe area | 20 | Must | MS-08 | The exact number of red flags (mines in code) is placed and none lands in the safe area |
| ☐ | MS-10 | Implement `CalculateNumbers()` | 15 | Must | MS-09 | Every safe cell stores its correct neighbour mine count |
| ☐ | MS-11 | Write a temporary `BoardDebug` script that prints the board to the Console | 20 | Must | MS-10 | Play mode prints a readable grid of `*` and numbers that you check by hand |

##### Week 3: Graphics and the visible grid

**Goal:** see the board on a portrait screen.
- Make or download sprites: hidden tile, revealed tile, numbers 1–8, **yellow flag** (player marker), **rectangular red flag** (the hazard), hit red flag (dark tile, bright border), crossed-out yellow flag. Use **at least 128×128 px**, because phone screens are high-resolution and small sprites look blurry. Kenney.nl has free tile packs
- Import the sprites (Pixels Per Unit = sprite size; Compression: None or High Quality; mipmaps off)
- Create the `Tile` prefab: `SpriteRenderer` + `BoxCollider2D` + `TileView` script
- Write `BoardView`, which spawns one tile per cell at position (x, y) and keeps a `TileView[,]` array
- Write `CameraController.Fit()`, which centres the camera on the board and zooms so the grid fits a **portrait** screen, leaving space for the top bar (counter, face, timer) and the bottom bar (flag-mode button)

**Done when:** a 9×9 grid of hidden tiles appears in the Device Simulator on a typical phone.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-12 | Find or draw sprites (at least 128×128 px): hidden, empty, 1–8, yellow flag (player marker), rectangular red flag (hazard), hit red flag, crossed-out yellow flag | 25 | Must | — | All 14 sprites are in `Assets/Sprites`; red and yellow flags are easy to tell apart on a phone |
| ☐ | MS-13 | Set the sprite import settings (Pixels Per Unit = sprite size, Compression None/High Quality, mipmaps off) | 10 | Must | MS-12 | Sprites look sharp on a high-resolution screen and each one is 1 world unit wide |
| ☐ | MS-14 | Create the `Tile` prefab (SpriteRenderer + BoxCollider2D) and the `TileView` script | 20 | Must | MS-13 | The prefab exists with all sprites assigned in the Inspector |
| ☐ | MS-15 | Write `BoardView.Build()` to spawn one tile per cell | 20 | Must | MS-08, MS-14 | A 9×9 grid of hidden tiles appears in Play mode |
| ☐ | MS-16 | Write `CameraController.Fit()` for portrait screens, with space for the top and bottom bars | 15 | Must | MS-15 | The whole board is visible and centred in the Device Simulator on a phone |

##### Week 4: Touch input: tap and long press

**Goal:** the player can interact with the board using a finger.
- Write `InputHandler` using the Input System's `Pointer.current`. It works with touch on the phone **and** with the mouse in the editor, so you can test quickly
- Convert the touch position to world coordinates with `Camera.ScreenToWorldPoint` and round to a grid (x, y)
- **Tap** (finger lifted quickly without moving) → `Board.Reveal(x, y)` → `BoardView` refreshes the tile
- **Long press** (finger held still for about 0.4 s) → `Board.ToggleFlag(x, y)`. A flagged cell can't be revealed
- `TileView.Refresh(Cell)` chooses the correct sprite from the cell's state
- Test on the phone and tune the long-press time until it feels right

**Done when:** you can open cells and place flags with your finger on the phone. ✅ **Milestone M2**

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-17 | Write `InputHandler` with `Pointer.current`: screen position → grid (x, y), tap detection (lift without moving) | 25 | Must | MS-15 | A tap logs the correct cell, on the phone and with the mouse in the editor |
| ☐ | MS-18 | A tap opens a single cell and redraws it | 15 | Must | MS-17 | The tapped tile shows its number, an empty tile or a red flag |
| ☐ | MS-19 | Long press (0.4 s held still) toggles a flag; implement `Board.ToggleFlag` | 25 | Must | MS-17 | Long press places and removes a flag; a flagged cell can't be opened |
| ☐ | MS-20 | Finish `TileView.Refresh()` for every cell state | 10 | Must | MS-14 | Each state shows the right sprite |
| ☐ | MS-21 | Test tap and long press on the phone; tune `longPressTime` | 15 | Must | MS-18, MS-19 | Taps always hit the cell under the finger and long press feels natural |

#### Month 2: A playable game

##### Week 5: Flood fill and the safe first tap

**Goal:** the core Minesweeper "feel".
- Implement flood fill in `Board.Reveal` with a `Queue<Vector2Int>` (BFS). When a revealed cell has 0 adjacent mines, add its hidden, unflagged neighbours to the queue
- Return the list of changed cells so the view only redraws those
- Generate mines lazily on the **first** tap, so the first tap is always safe and usually opens an area
- Test on a 16×30 board **on the phone** to make sure it's fast

**Done when:** tapping an empty area opens a whole region the way classic Minesweeper does.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-22 | Implement flood fill in `Board.Reveal()` with a `Queue` (BFS) | 35 | Must | MS-18 | Tapping a 0 opens the whole empty region and its numbered border |
| ☐ | MS-23 | Make `Reveal()` return the changed cells, and add `BoardView.RefreshCells()` | 15 | Must | MS-22 | Only the changed tiles are redrawn |
| ☐ | MS-24 | Place the red flags (mines in code) on the first tap instead of when the board is created | 20 | Must | MS-09, MS-22 | 20 first taps in a row are all safe and each opens an area |
| ☐ | MS-25 | Test flood fill on a 16×30 board on the phone for speed and correctness | 20 | Must | MS-22 | No lag or errors; flood fill never opens flagged cells |

##### Week 6: Game states, win and loss

**Goal:** the game has a beginning and an end.
- Create a `GameState` enum: `Ready`, `Playing`, `Won`, `Lost`
- Write the `GameManager` MonoBehaviour, which owns the `Board`, the state and the rules
- Loss: when a red flag is revealed, show all red flags, highlight the tapped one, mark wrongly placed yellow flags, block input
- Win: when `revealedCount == width × height − mines`, auto-flag all mines and block input
- Add `Restart()`, which destroys the old tiles and builds a new board. Use a temporary on-screen button until the real UI exists

**Done when:** you can play a full round, win or lose, and restart.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-26 | Create the `GameState` enum and the `GameManager` script (`OnTap`, `OnLongPress`); move input handling into it | 25 | Must | MS-24 | `GameManager` owns the Board and switches Ready → Playing on the first reveal |
| ☐ | MS-27 | Handle a loss: show all red flags, highlight the hit one, cross out wrong yellow flags, block input | 25 | Must | MS-26 | Opening a red flag ends the round and the board can't be changed |
| ☐ | MS-28 | Handle a win: detect it, cover the remaining red flags with yellow flags, block input | 20 | Must | MS-26 | Opening the last safe cell ends the round as a win |
| ☐ | MS-29 | Add `NewGame()` restart, with a temporary on-screen button | 20 | Must | MS-26 | The button gives a fresh board in any state with no leftover tiles and every cell hidden |

##### Week 7: Mobile user interface

**Goal:** a touch-friendly top bar and bottom bar.
- Add a Canvas with a **Canvas Scaler**: "Scale With Screen Size", reference resolution 1080×1920, Match = 0.5. This keeps the UI the same relative size on every phone
- Add a `SafeArea` script on the UI root panel so nothing is hidden behind the notch, camera hole or rounded corners
- Top bar with TextMeshPro text: mine counter (mines − flags, can go negative) and timer (starts on the first tap, stops at the end, capped at 999)
- Restart button with a face that changes: 🙂 normal / 😮 while a finger is held on the board / 😎 win / 😵 loss
- Bottom bar with a big **flag-mode toggle** (⛏ reveal / 🚩 flag). All buttons at least ~48 dp (about 130 px at the reference resolution) so they're easy to hit
- Win and lose message panels. The lose panel shows one random line from `Assets/Text/loss_lines.txt` (one line per entry) under the "lost" message, using the `LossLines` helper
- Use C# events (`event Action`) from `GameManager` so the UI doesn't poll every frame
- Make sure touches on buttons don't also tap the board

**Done when:** the MVP is fully playable **on the phone**. ✅ **Milestone M3.** Tag it in Git: `git tag v0.1-mvp`

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-30 | Add a Canvas with Canvas Scaler (Scale With Screen Size, 1080×1920, Match 0.5), EventSystem and TextMeshPro | 10 | Must | — | Text appears at the same relative size on different phones in the Device Simulator |
| ☐ | MS-31 | Add the `SafeArea` script on the UI root panel | 10 | Must | MS-30 | UI stays clear of the notch on a notched phone in the Device Simulator |
| ☐ | MS-32 | Top bar: red flag counter (red flags − yellow flags) and timer (starts on first reveal, stops at the end, caps at 999) | 15 | Must | MS-29, MS-30 | Both update correctly; the counter can go negative |
| ☐ | MS-33 | Face button with 🙂 / 😮 / 😎 / 😵 states that restarts on tap; remove the temporary button | 15 | Must | MS-30 | The face matches the state and tapping it restarts |
| ☐ | MS-34 | Bottom bar: flag-mode toggle (⛏ / yellow flag) calling `GameManager.ToggleFlagMode()` | 15 | Must | MS-26, MS-30 | In flag mode a tap flags; the button shows the active mode |
| ☐ | MS-35 | Win and loss message panel with Play again and Menu buttons; the loss panel shows a random line from `Assets/Text/loss_lines.txt` (`LossLines` helper; create the file with a few lines) | 15 | Must | MS-30 | The panel appears at the end of a round; each loss shows a line from the file; a line added to the file appears without code changes |
| ☐ | MS-36 | Stop touches on UI from reaching the board; build to the phone; tag `v0.1-mvp` | 10 | Must | MS-33, MS-34 | Tapping buttons never opens a tile; a full round works on the phone; the tag is on GitHub |

##### Week 8: Difficulty levels, main menu, Back button

**Goal:** replayability and normal Android navigation.
- Create a `DifficultySettings` ScriptableObject (name, width, height, mines) and three assets: Beginner 9×9/10, Intermediate 16×16/40, Expert **16×30**/99
- Add a `MainMenu` scene with three big difficulty buttons and make it **scene 0** in Build Profiles, so the game always starts from the difficulty choice. There's **no Quit button**: on Android, players leave with the Back or Home button
- Pass the chosen difficulty to the game scene with a static `GameSession` class, then load it with `SceneManager.LoadScene`
- **Android Back button:** in the game it returns to the menu; in the menu it closes the app. In the Input System the Back button arrives as the **Escape** key
- Test the layout in the Device Simulator on several shapes: a 16:9 phone, a tall 20:9 phone with a notch, and a 4:3 tablet

**Done when:** you can choose any level from the menu, and Back behaves as expected on the phone.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-37 | Create the `DifficultySettings` ScriptableObject and the Beginner 9×9/10, Intermediate 16×16/40, Expert 16×30/99 assets | 15 | Should | MS-26 | Three assets exist with the correct sizes and mine counts |
| ☐ | MS-38 | Create the static `GameSession` class and read it in `NewGame()` | 5 | Should | MS-37 | The game uses `GameSession.Selected`, or the default if it's empty |
| ☐ | MS-39 | Build the `MainMenu` scene (scene 0, the first screen): title, three large difficulty cards, settings button; no Quit button | 30 | Should | MS-38 | The app opens on this screen; the layout matches the sketch in Game_Design_Document.md and sits inside the safe area |
| ☐ | MS-40 | Scene loading, Menu button, and Android Back (Escape key): game → menu, menu → quit | 15 | Should | MS-39 | Back behaves correctly on the phone; Menu → Game → Menu works repeatedly with no errors |
| ☐ | MS-41 | Test all screens in the Device Simulator on a 16:9 phone, a 20:9 notched phone and a 4:3 tablet | 25 | Should | MS-40 | Board and UI fit on all three with nothing hidden or overlapping |

#### Month 3: Polish, testing, release

##### Week 9: Zoom, pan and advanced gameplay

**Goal:** big boards are comfortable to play on a small screen.
- **Pinch to zoom** in `CameraController`: two fingers change `orthographicSize`, limited between a minimum and the "whole board visible" size
- **Drag to pan** with one finger. The camera can't leave the board, and a drag never counts as a tap
- Chording: tapping a revealed number whose adjacent flags equal its number reveals all its other neighbours
- Best time per difficulty: save it with `PlayerPrefs.SetFloat("best_" + name, time)` and show it in the menu, plus a "New record!" message
- Pause the timer while the app is in the background (`OnApplicationPause`)

**Done when:** Expert is playable on a phone with zoom and pan, and best times survive closing the app.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-42 | Pinch to zoom in `CameraController`, limited between the minimum size and "whole board visible" | 25 | Should | MS-16 | Pinching zooms smoothly on the phone and never ends in a tap |
| ☐ | MS-43 | One-finger drag to pan, clamped to the board; a drag never counts as a tap | 15 | Should | MS-42 | The view follows the finger and can't leave the board |
| ☐ | MS-44 | Chording: tapping an opened number calls `Board.Chord()` | 15 | Should | MS-22 | It opens neighbours only when the flag count equals the number; a wrong flag causes a loss |
| ☐ | MS-45 | Save and load the best time per difficulty with `PlayerPrefs` | 15 | Should | MS-28, MS-37 | A best time survives closing and reopening the app |
| ☐ | MS-46 | Show best times on the menu cards and "New record!" on the win panel | 10 | Should | MS-45 | Cards show a time or "—"; the message appears only on a record |
| ☐ | MS-47 | Pause the timer while the app is in the background (`OnApplicationPause`) | 10 | Should | MS-32 | Pressing Home for 30 s doesn't add 30 s to the timer |

##### Week 10: Audio, vibration and polish

**Goal:** make it feel good in the hand.
- Add an `AudioManager` with clips for tap, flag, flood reveal, explosion and win (free sounds from Kenney.nl or freesound.org; check the licence)
- Add a settings panel with **Sound** and **Vibration** on/off, saved in `PlayerPrefs`
- Vibration (`Handheld.Vibrate()`) when a flag is placed by long press and when you lose
- Pressed highlight: the tile under the finger looks pressed while held (this replaces the mouse "hover" effect, which doesn't exist on touch screens)
- Optional small effects: tile pop on reveal, camera shake on explosion

**Done when:** the game has sound, vibration and visual feedback. ✅ **Milestone M4**

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-48 | Find sound effects: tap, flag, flood fill, loss (hitting a red flag), win (check the licences) | 15 | Should | — | 5 clips are in `Assets/Audio`, with licences noted |
| ☐ | MS-49 | Write `AudioManager` and play the sounds on game events | 20 | Should | MS-48 | Each event in the feedback table in Game_Design_Document.md plays its sound |
| ☐ | MS-50 | Settings panel with Sound and Vibration toggles, saved in `PlayerPrefs` | 15 | Should | MS-39, MS-49 | Both settings stay as chosen after reopening the app |
| ☐ | MS-51 | `Haptics` helper: vibrate on long-press flag and on loss | 10 | Should | MS-50 | The phone buzzes on those events only when Vibration is on |
| ☐ | MS-52 | Pressed tile highlight and 😮 face while a finger is held on the board | 10 | Should | MS-33 | The tile under the finger darkens and the face changes; both reset on lift or drag |
| ☐ | MS-53 | Tile "pop" animation when opened | 10 | Could | MS-23 | Opened tiles scale up briefly and back |
| ☐ | MS-54 | Camera shake on loss | 10 | Could | MS-27 | The camera shakes briefly and returns to its exact position |

##### Week 11: Automated tests and bug fixing

**Goal:** confidence that the logic is correct.
- The **Unity Test Framework** is already installed in GC26: create an assembly definition for `Scripts/Core` and an Edit Mode test assembly that references it
- Write tests for `Board`: the mine count is correct, the first-tap area has no mines, the numbers are correct on a hand-made board, flood fill stops at numbers, a flagged cell can't be revealed, the win is detected, chording works
- Go through the manual test checklist (section 8.2) **on the real phone**
- Fix the bugs you find (keep a bug list in section 10.2)

**Done when:** all tests pass and there are no known critical bugs.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-55 | Unity Test Framework (already installed): create a `Core` asmdef and an Edit Mode test asmdef | 15 | Must | MS-08 | An empty test runs green in the Test Runner window |
| ☐ | MS-56 | Write unit tests for `Board` (mine count, safe area, numbers, flood fill, flags, win, chord, Expert 16×30) and `LossLines` (skips empty and `#` lines, empty list) | 35 | Must | MS-55 | At least 9 tests, all passing |
| ☐ | MS-57 | Run through the manual test checklist (section 8.2) **on the phone** | 25 | Must | MS-46 | Every item is checked; failures are added to the bug list |
| ☐ | MS-58 | Fix the bugs found | 15 | Must | MS-57 | No High-severity bugs left open |

##### Week 12: Android build and settings

**Goal:** an app that installs and runs like a real mobile game.
- Player Settings: company name, product name, **package name** (e.g. `com.yourname.minesweeper`), version, **adaptive icon**, splash screen, portrait only
- Other Settings: Scripting Backend **IL2CPP**, Target Architectures **ARM64** (both required by Google Play), Minimum API Level as suggested by Unity 6, Target API Level = latest installed
- Set `Application.targetFrameRate = 60` so animations are smooth; remove debug scripts and `Debug.Log` spam
- Create a **keystore** (Publishing Settings) and make a signed release build: **APK** for sharing and testing, **AAB** if you want Google Play. Back up the keystore and passwords safely; you can't update a Play app without them
- Test on 2 or more devices if possible: notch, Back button, phone call or lock screen mid-game, returning to the app

**Done when:** a signed APK installs and runs correctly on a phone that isn't connected to Unity.

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-59 | Player Settings: package name, version, adaptive icon, splash, portrait only, IL2CPP + ARM64, minimum/target API level | 20 | Must | — | All settings are filled in and a build succeeds with them |
| ☐ | MS-60 | Remove `BoardDebug` and unneeded `Debug.Log` calls; set `Application.targetFrameRate = 60` | 10 | Must | — | The Console is clean during normal play and the game runs at 60 fps |
| ☐ | MS-61 | Create a keystore and make a signed release build (APK; AAB if publishing); back up the keystore outside Git | 20 | Must | MS-59, MS-60 | A signed APK installs on the phone without Unity; the keystore has a backup |
| ☐ | MS-62 | Test the release build on 2+ devices: notch, Back button, phone call / lock screen mid-game, return to app | 25 | Must | MS-61 | Everything behaves as described in the edge-case table in Game_Design_Document.md |
| ☐ | MS-63 | Upload the AAB to Google Play internal testing | 15 | Could | MS-61 | The app installs from the Play internal-testing link |

##### Week 13: Buffer, release and review

**Goal:** finish cleanly. This week also covers tasks that slipped earlier.
- Finish anything still open from weeks 1–12
- Write the `README.md` in the repository: description, controls, screenshots, how to build
- Optional: upload the AAB to **Google Play internal testing** (needs a one-time Google Play developer account fee), or share the APK on itch.io
- Tag the release: `git tag v1.0`
- Write a retrospective (section 10.3): what went well, what was hard, what you would do next

**Done when:** v1.0 is released. ✅ **Milestone M5**

**Tasks**

| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-64 | Finish any tasks that slipped from earlier weeks | 40 | Must | — | No Must tasks left open |
| ☐ | MS-65 | Write the repository `README.md`: description, touch controls, phone screenshots, build steps | 20 | Must | MS-61 | The README is visible on GitHub |
| ☐ | MS-66 | Tag `v1.0`, then fill in the retrospective (section 10.3). Optional: share the APK on itch.io | 30 | Must | MS-65 | The tag is on GitHub and the retrospective is written |

### 9.4 Critical path

These tasks depend on each other in a chain. If one of them is late, everything after it is late too, so finish them first:

MS-05 (phone build) → MS-07 → MS-08 → MS-14/MS-15 → MS-17 → MS-18 → MS-22 → MS-24 → MS-26 → MS-27/MS-28 → MS-30–MS-36 (MVP) → MS-37–MS-40 → MS-55–MS-58 → MS-61 → MS-66

### 9.5 If you fall behind

Cut tasks in this order:
1. **Could:** MS-53, MS-54, MS-63, and the itch.io upload in MS-66
2. **Should, polish:** MS-52, MS-51, MS-50
3. **Should, features:** MS-44 (chording), MS-45/MS-46 (best times), MS-47 (pause timer)

**Don't cut zoom and pan (MS-42, MS-43) unless you also drop Expert** or shrink it to 16×24; without zoom, Expert cells are too small for a finger.

Don't cut testing (MS-55–MS-58) or the signed Android build (MS-61, MS-62).

### 9.6 Risks and how to handle them

| Risk | Sign | What to do |
|---|---|---|
| Android build setup fails | Errors about the SDK, NDK or JDK | It's done in week 1 on purpose. Reinstall the Android module from Unity Hub; let Unity use its own bundled SDK/JDK instead of other installs |
| Phone not detected | Build And Run can't find a device | Check USB debugging, accept the "Allow USB debugging" prompt on the phone, try another cable, set the USB mode to "File transfer" |
| No phone available | — | Use the Device Simulator for layout, and the Android Emulator (Android Studio) for running builds |
| Cells too small to tap | Wrong cells get opened on Expert | Pinch zoom (week 9); if zoom is cut, drop Expert or reduce it to 16×24 |
| Taps and drags confused | Panning opens cells | Use a movement threshold (~20 px) before a touch counts as a drag; tune it on the phone |
| Different screen shapes | UI hidden by the notch or overlapping the board | Canvas Scaler + SafeArea; test several devices in the Device Simulator |
| Behind schedule | A milestone is late by more than 1 week | Drop Nice-to-have items first, then Should-have items; week 13 is the buffer |
| Stuck on a bug for more than 30 minutes | No progress in a session | Write it in the weekly log (section 10.1), move to the next task, search or ask for help later |
| Lost work or keystore | Broken project, lost signing key | Commit and push every session; keep the keystore **outside** the Git repository with a backup copy |

### 9.7 Learning resources

- Unity Learn: learn.unity.com ("Unity Essentials", "Junior Programmer")
- Unity Manual: "Android" section (setup, building, Player settings) and "Device Simulator"
- Input System package docs: "Touch support" and "Pointer"
- Microsoft C# docs: learn.microsoft.com/dotnet/csharp
- Android design guidelines (touch target sizes): developer.android.com/design
- Free 2D art and sounds: kenney.nl
- Gitignore template: github.com/github/gitignore/blob/main/Unity.gitignore

## 10. Progress tracking

Task status is tracked on the [task board](https://github.com/users/Zaidejas/projects/6) (Todo → In Progress → Done). Use the tables below for notes, bugs and the final review.

### 10.1 Weekly log

| Week | Date | Planned | Done? | Hours | Notes / blockers / next step |
|---|---|---|---|---|---|
| 1 | | Setup, Android toolchain, first phone build | ☐ | | |
| 2 | | Board data model (M1) | ☐ | | |
| 3 | | Sprites, Tile prefab, BoardView, portrait camera | ☐ | | |
| 4 | | Touch input: tap and long press (M2) | ☐ | | |
| 5 | | Flood fill, safe first tap | ☐ | | |
| 6 | | Game states, win/lose, restart | ☐ | | |
| 7 | | Mobile UI: safe area, top bar, flag-mode button (M3) | ☐ | | |
| 8 | | Difficulties, main menu, Back button | ☐ | | |
| 9 | | Pinch zoom, pan, chording, best times, pause | ☐ | | |
| 10 | | Audio, vibration, settings, polish (M4) | ☐ | | |
| 11 | | Unit tests, phone testing, bug fixing | ☐ | | |
| 12 | | Signed Android build, device testing | ☐ | | |
| 13 | | Buffer, README, release (M5) | ☐ | | |

**Total hours:** ___ / 26

### 10.2 Bug list

| # | Found (week) | Device | Description | Severity (High/Med/Low) | Fixed (week) |
|---|---|---|---|---|---|
| 1 | | | | | |

### 10.3 Retrospective (week 13)

- **What went well:**
- **What was hard:**
- **What I learned:**
- **What I'd add next:**
