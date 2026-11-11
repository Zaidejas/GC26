# Colours — Technical Design Document (gc26_2048_nastya)

**Language / Язык:** English (below) · [Русский](#русская-версия)

Companion to [Colours_GDD.md](Colours_GDD.md). This document describes **how** the game is built inside the GC26 microgames project.
The repository rules are in [RULES.md](../../../../RULES.md) and take priority over anything here.

| | |
| --- | --- |
| Unity | 6000.5.6f1 |
| Render pipeline | URP 17.5 (the project's existing PC/Mobile assets; nothing changed) |
| UI | uGUI 2.5 + TextMeshPro (LiberationSans SDF) |
| Input | Input System 1.20 |
| Tests | Unity Test Framework 1.7 (EditMode + PlayMode) |
| Microgame ID | `gc26_2048_nastya` |
| Namespace / assembly | `GC26._2048Nastya` / `GC26._2048Nastya.asmdef` |
| Entry scene | `gc26_2048_nastya_Main` |
| New packages | **None.** Everything uses packages already in `Packages/manifest.json` (rule 5) |

---

## 1. Project constraints (from RULES.md)

| Rule | What it means for this game |
| --- | --- |
| Only our folder | All game files live in `Assets/Microgames/gc26_2048_nastya/` (plus `Assets/Microgames/gc26_2048_nastya.meta`) |
| Own asmdef + namespace | All code is in `namespace GC26._2048Nastya` (sub-namespaces allowed); it references only `GC26.Core` and packages |
| Prefixed scene names | `gc26_2048_nastya_Main.unity`, `gc26_2048_nastya_Test.unity` |
| Resources in an ID subfolder | `Resources/gc26_2048_nastya/...`, loaded as `Resources.Load("gc26_2048_nastya/...")`. Prefer direct serialized references and avoid Resources entirely |
| Saves | Files only in `MicrogameSave.Folder(Id)`; PlayerPrefs keys only via `MicrogameSave.Key(Id, "...")` |
| No global state | No `DontDestroyOnLoad`, no static singletons, no `Time.timeScale` changes (pause blocks input instead) |
| No generic names | No `GameManager.cs`, `Player.cs`, `UI.cs`, `Menu.cs`, etc. (`check_names.py` rejects them). Classes get a `2048` suffix or a specific name |
| Input actions | Our own asset `gc26_2048_nastya_Controls.inputactions`, with the action map `gc26_2048_nastya_Gameplay`. The shared `Assets/InputSystem_Actions.inputactions` stays untouched |

---

### 1.1 Two 2048 games in one repository

This repository has two separate 2048 games by different people. They share nothing except `GC26.Core`.

| | This game | The other game |
| --- | --- | --- |
| ID | `gc26_2048_nastya` | `gc26_2048` |
| Design | Colours, "night glass" (the GDD) | Cheese theme |
| Branch | `game/gc26_2048_nastya` | `game/gc26_2048` |
| Folder | `Assets/Microgames/gc26_2048_nastya/` | `Assets/Microgames/gc26_2048/` |
| Namespace / asmdef | `GC26._2048Nastya` | `GC26._2048` |
| Entry scene | `gc26_2048_nastya_Main` | its own scene with the `gc26_2048_` prefix |
| Local clone on this PC | `C:\Users\20260047\Unity\GC26_Nastya` | `C:\Users\20260047\Unity\2048` |

Never copy assets between the two folders together with their `.meta` files (duplicate GUIDs). To reuse something, duplicate it in Unity with Ctrl+D.

## 2. Folder layout

```
Assets/Microgames/gc26_2048_nastya/
    microgame.json
    GC26._2048Nastya.asmdef                 references: GC26.Core, Unity.InputSystem, Unity.TextMeshPro, UnityEngine.UI
    README.md
    Docs~/                           Colours_GDD.md, Colours_TDD.md, Colours_prototype.html (Unity ignores folders ending in ~)
    Scenes/
        gc26_2048_nastya_Main.unity          entry scene (one IMicrogame component)
    Scripts/
        Game2048Nastya.cs                   IMicrogame entry point, owns the game loop
        Model/                        pure C#, no UnityEngine dependency
            Board2048.cs
            Direction2048.cs
            MoveResult2048.cs
            TileSpawner2048.cs
            GameState2048.cs
        View/
            BoardView2048.cs
            TileView2048.cs
            TilePool2048.cs
            Hud2048.cs
            Overlays2048.cs
            Theme2048.cs              ScriptableObject: colors, font sizes, timings
        Input/
            Input2048.cs
        Persistence/
            SaveStore2048.cs
        Audio/
            Sfx2048.cs
    Input/
        gc26_2048_nastya_Controls.inputactions
    Prefabs/
        gc26_2048_nastya_Tile.prefab
        gc26_2048_nastya_Cell.prefab
    Art/
        gc26_2048_nastya_Theme.asset         Theme2048 instance
        gc26_2048_nastya_RoundedRect.png     9-sliced rounded sprite
    Audio/
        *.wav / *.ogg, CREDITS.md
    Tests/
        EditMode/GC26._2048Nastya.Tests.asmdef
        EditMode/Board2048Tests.cs
        EditMode/SaveStore2048Tests.cs
```

The test assembly lives in a `Tests` folder, so `check_names.py` accepts the extra asmdef.

`microgame.json`:

```json
{
  "id": "gc26_2048_nastya",
  "title": "Colours",
  "description": "Slide and merge number tiles to reach 2048",
  "entry_scene": "gc26_2048_nastya_Main",
  "authors": ["<team names>"]
}
```

---

## 3. Architecture

Three layers with one-way dependencies:

```
          ┌────────────────────────────────────────────┐
 Input ──►│ Game2048Nastya (MonoBehaviour, IMicrogame) │──► SaveStore2048 ──► MicrogameSave
          │  - state machine, move queue, exit()       │
          └──────┬───────────────────────┬─────────────┘
                 │ commands              │ results (MoveResult2048)
                 ▼                       ▼
          Model (pure C#)          View (MonoBehaviours)
          Board2048, Spawner      BoardView2048, TileView2048, Hud2048, Overlays2048, Sfx2048
```

- **Model** knows nothing about Unity objects. It is deterministic when given a seeded random, so it is fully covered by EditMode tests.
- **View** only displays what the model reports. It never changes game state.
- **Game2048Nastya** is the only class that talks to both, and the only `IMicrogame` in the scene.

There are no static mutable fields anywhere. Everything is wired through serialized references in the scene.

---

## 4. Model

### 4.1 Data

```csharp
namespace GC26._2048Nastya
{
    public enum Direction2048 { Up, Down, Left, Right }

    /// One tile on the board. Id is stable across moves, so the view can animate it.
    public readonly struct Tile2048
    {
        public readonly int Id;     // unique within a game, > 0
        public readonly int Value;  // 2, 4, 8, ...
    }

    public sealed class Board2048
    {
        public int Size { get; }                  // 4 (3..6 if board sizes are added)
        Tile2048?[,] _cells;                      // [x, y], x to the right, y down from the top row
        int _nextId;

        public Tile2048? Get(int x, int y);
        public IEnumerable<(int x, int y)> EmptyCells();
        public bool HasMoves();                   // any empty cell or any equal neighbors
        public MoveResult2048 Move(Direction2048 dir);
        public Tile2048 Place(int x, int y, int value);  // used by spawner, loading and tests
        public Board2048 Clone();                 // for undo
    }
}
```

### 4.2 Move result (input for animation)

```csharp
public sealed class MoveResult2048
{
    public bool Moved;                    // false → invalid move
    public int ScoreGained;               // sum of merged tile values
    public List<TileMove> Moves;          // (tileId, from, to): every tile that changed position
    public List<TileMerge> Merges;        // (sourceIdA, sourceIdB, newTile, cell)
    public int MaxValue;                  // largest tile after the move
}
```

The spawn is **not** part of `Move`. `Game2048Nastya` calls `TileSpawner2048` afterwards, so tests can check moves without randomness.

### 4.3 Move algorithm

Each move is processed as `Size` independent **lines**. A line is read in the order **from the wall the tiles move toward**:

| Direction | Lines | Read order inside a line |
| --- | --- | --- |
| Left | each row y | x = 0 → Size-1 |
| Right | each row y | x = Size-1 → 0 |
| Up | each column x | y = 0 → Size-1 |
| Down | each column x | y = Size-1 → 0 |

For one line (positions `p[0]` is at the wall):

```
tiles   = non-empty tiles of the line, in read order
out     = empty list
i = 0
while i < tiles.Count:
    if i + 1 < tiles.Count and tiles[i].Value == tiles[i+1].Value:
        merged = new tile (new Id, Value * 2)
        out.Add(merged); record merge(tiles[i], tiles[i+1] → merged at p[out.Count-1])
        record moves of tiles[i] and tiles[i+1] to p[out.Count-1]
        score += merged.Value
        i += 2                      // merged tile is never compared again → "one merge per move"
    else:
        out.Add(tiles[i]); record move if its position changed
        i += 1
write out to p[0..out.Count-1], clear the rest
```

This gives the GDD rules automatically: `2 2 2 2` → `4 4`, `2 2 2 _` Left → `4 2`, Right → `_ _ 2 4`.
`Moved` is true if any tile changed position or any merge happened.

Complexity: O(Size²) per move with no allocations except the result lists, which are reused (cleared, not re-created) by `Game2048Nastya`.

### 4.4 Spawner

```csharp
public sealed class TileSpawner2048
{
    public TileSpawner2048(System.Random rng, float fourChance = 0.1f);
    public Tile2048? Spawn(Board2048 board);   // null if the board is full
}
```

- Uses `System.Random`, not `UnityEngine.Random`, so it shares no global random state with other games and accepts a seed.
- Picks a uniformly random empty cell; value 4 with `fourChance`, otherwise 2.

### 4.5 Game state

```csharp
public sealed class GameState2048
{
    public Board2048 Board;
    public int Score;
    public int Moves;
    public bool WonShown;        // "Keep going" already chosen, never show the win overlay again
    public bool IsOver;
    public UndoSnapshot? Undo;   // board clone + score + moves before the last valid move (Should have)
}
```

Win check: `!WonShown && result.MaxValue >= 2048`. Lose check: `!Board.HasMoves()` after the spawn.

---

## 5. Game2048Nastya: entry point and control flow

Based on the template from `tools/new_microgame.py`.

```csharp
public class Game2048Nastya : MonoBehaviour, IMicrogame
{
    public const string Id = "gc26_2048_nastya";

    [SerializeField] BoardView2048 boardView;
    [SerializeField] Hud2048 hud;
    [SerializeField] Overlays2048 overlays;
    [SerializeField] Input2048 input;
    [SerializeField] Sfx2048 sfx;
    [SerializeField] Theme2048 theme;

    Action<int> _exit;
    bool _started;
    GameState2048 _state;
    SaveStore2048 _save;
    Direction2048? _buffered;

    public void Begin(Action<int> exit) { ... }   // load save or new game, enable input
    void Start() { /* standalone test: Begin itself if !MicrogameRunner.IsRunning */ }
    public void ExitToMenu() { _save.SaveGame(_state); var e = _exit; _exit = null; e?.Invoke(_state.Score); }
}
```

### 5.1 State machine

```
            Begin()
               │
               ▼
 ┌────────► Playing ──move valid──► Animating ──done──► (check win / lose) ──► Playing
 │             │ ▲                     │ input → buffer one direction
 │      Esc    │ │ Resume              │
 │             ▼ │                     ├──► WinOverlay ──Keep going──► Playing
 │           Paused ──To menu──► Exiting (save, exit(score))
 │                                     └──► GameOver ──Try again──► (new game) ──► Playing
 └─────────────────────────────────────────────── To menu ──► Exiting
```

- Input is processed only in `Playing`. In `Animating`, a direction is stored in `_buffered`; when a new one arrives, the current animation is **completed instantly** (`boardView.FinishNow()`) and the buffered move runs.
- `Paused` and overlays disable `Input2048`'s gameplay map. **`Time.timeScale` is never touched.**

### 5.2 One valid move, step by step

1. `result = _state.Board.Move(dir)`. If `!result.Moved`: `boardView.Bump(dir)`, `sfx.Invalid()`, stop.
2. Store the undo snapshot (taken before step 1 with `Board.Clone()`).
3. `_state.Score += result.ScoreGained; _state.Moves++`.
4. `spawned = spawner.Spawn(_state.Board)`.
5. `boardView.Play(result, spawned)` starts the slide → merge/spawn animation (state = `Animating`).
6. `hud.SetScore(...)`, update best score, and `_save.SaveGame(_state)` (a small file, written synchronously; see 8.2).
7. When the animation finishes: win check → overlay; `!HasMoves()` → game over, `_save.ClearGame()`.

---

## 6. View

### 6.1 Layout
- One **Screen Space - Camera** canvas in the game scene (not Overlay, so it is unloaded together with the scene and never draws over the shell). A `CanvasScaler` with *Scale With Screen Size*, reference 1080×1920, match 0.5.
- The board is a square `RectTransform` sized by an `AspectRatioFitter` (Fit In Parent). Cell positions are **calculated** from the board size, padding and gap, not by a `GridLayoutGroup`, because tiles must move freely between cells during animation.
- Background cells (`gc26_2048_nastya_Cell.prefab`) are created once per board size. Tiles live in a separate container above them.

### 6.2 Tiles
- `gc26_2048_nastya_Tile.prefab`: `Image` (9-sliced rounded sprite) + `TextMeshProUGUI` + an optional glow `Image`.
- `TileView2048.Set(value, theme)` applies the color, text color and font size from `Theme2048`. Values above the theme table use the last entry.
- **Pooling:** `TilePool2048` keeps inactive tile views and reuses them, so there is no `Instantiate`/`Destroy` during play. The pool is a field of `BoardView2048`, so it lives and dies with the scene.
- `BoardView2048` keeps `Dictionary<int, TileView2048>` from the model tile Id to its view.

### 6.3 Animation
- No tween package is available (rule 5), so animations use a small coroutine helper: `IEnumerator Tween(float duration, Action<float> step)` with ease-out `1 - (1 - t)^3`.
- The time base is `Time.unscaledDeltaTime`, so animations are correct regardless of `timeScale` set by anyone.
- Order per move: all slides in parallel (0.10 s) → merge pops and spawn grows in parallel (0.12 s). The two tiles of a merge slide to the target cell, then both return to the pool and the new tile pops in.
- `FinishNow()` stops the coroutines and snaps every tile to its final state. It is used for buffered input, pausing and exiting.

### 6.4 Theme
`Theme2048` (ScriptableObject, asset `Art/gc26_2048_nastya_Theme.asset`) holds:
- background, board, empty cell colors;
- a list of `(value, tileColor, textColor, glow)` entries from the GDD palette;
- font sizes for 1–2, 3, 4 and 5+ digits;
- animation durations (slide, pop, spawn, bump, score popup).

---

## 7. Input

### 7.1 Actions (`gc26_2048_nastya_Controls.inputactions`, map `gc26_2048_nastya_Gameplay`)

| Action | Type | Bindings |
| --- | --- | --- |
| `Move` | Value, Vector2 | 2D composite: Arrows, WASD; gamepad left stick/D-pad as a bonus |
| `PointerPress` | Button | `<Pointer>/press` |
| `PointerPosition` | Value, Vector2 | `<Pointer>/position` |
| `Pause` | Button | Escape, gamepad Start |
| `NewGame` | Button | N |

The asset is used through a serialized `InputActionAsset` reference and enabled in `OnEnable`, disabled in `OnDisable`, so nothing stays active after the scene unloads.

### 7.2 Direction detection
- **Keys / stick:** on `Move` performed, take the dominant axis of the vector (|x| vs |y|). The stick needs a 0.5 deadzone and must return to center before the next move.
- **Swipe / drag:** remember the position on press; on release, `delta = end - start`. If `delta.magnitude < 0.05 * min(Screen.width, Screen.height)`, ignore it; otherwise use the dominant axis. Screen y points up, so positive y means **Up**.
- Presses that start on a UI button are ignored (`EventSystem.current.IsPointerOverGameObject()`), so tapping Pause does not trigger a move.
- `Input2048` exposes `event Action<Direction2048> Moved`, `event Action PausePressed`, `event Action NewGamePressed`.

### 7.3 EventSystem
The shell may already have an `EventSystem`. The game scene includes one with an `InputSystemUIInputModule` **only if** none exists: a small component checks `EventSystem.current` in `Awake` and disables the scene's own EventSystem when another is active.

---

## 8. Persistence

### 8.1 Where
| Data | Storage | Key / file |
| --- | --- | --- |
| Best score | PlayerPrefs | `MicrogameSave.Key(Id, "best")` → `gc26_2048_nastya_best` |
| Sound on/off | PlayerPrefs | `gc26_2048_nastya_sound` (via `MicrogameSave.Key`) |
| Hint shown | PlayerPrefs | `gc26_2048_nastya_hint` (via `MicrogameSave.Key`) |
| Game in progress | JSON file | `MicrogameSave.Folder(Id)/game.json` |

### 8.2 Format of `game.json`

```json
{
  "version": 1,
  "size": 4,
  "cells": [0, 2, 0, 0,  4, 8, 0, 0,  0, 0, 0, 0,  2, 0, 0, 0],
  "score": 120,
  "moves": 37,
  "wonShown": false
}
```

- `cells` is row by row from the top-left; 0 = empty. Tile Ids are re-created on load.
- Serialized with `JsonUtility` through a `[Serializable] class SaveData2048`.
- **Safe write:** write to `game.json.tmp`, then replace `game.json`, so a crash during a write cannot corrupt the save.
- **Validation on load:** wrong `version`, size, cell count, or a value that is not a power of two → the save is ignored and a new game starts (logged as a warning, never an exception).
- Undo history is not saved; after a resume, undo is unavailable until the next move.

---

## 9. Shell integration

- The entry scene has **exactly one** `IMicrogame`: `Game2048Nastya` on the root object `gc26_2048_nastya_Root`.
- `Begin(exit)` is the only start path. The `Start()` fallback runs the game standalone only when `MicrogameRunner.IsRunning` is false (opening the scene directly in the editor).
- `exit(score)` is called exactly once (guarded by setting `_exit = null`), from **To menu** in the pause menu or on the game over overlay. The score passed is the current game's score.
- Everything the game creates (tile views, pooled objects, audio sources) is a child of objects in the game scene, so unloading the scene cleans it up completely.
- The camera: the scene has its own camera only for standalone testing. With the shell present, it is disabled in `Begin` if the shell provides one. Agree on this with the organizer when the shell scene appears in `main`.

---

## 10. Audio

- `Sfx2048` has one `AudioSource` (2D, `playOnAwake` off) and `PlayOneShot` clips from serialized fields.
- Merge pitch: `1 + 0.05 * log2(value) clamped to 1.6`.
- The sound toggle sets `AudioSource.mute`; it never changes `AudioListener.volume` (that is global).
- Import settings: short SFX as *Decompress On Load*, mono, Vorbis quality 70.

---

## 11. Performance targets

| Target | Value |
| --- | --- |
| Frame rate | 60 FPS on a mid-range phone and any PC |
| GC allocations during a move | 0 B after warm-up (pooled tiles, reused lists, cached strings for values) |
| Scene load time inside the shell | < 0.5 s |
| Draw calls | < 30 (one atlas for UI sprites, one TMP font material) |

Cache number strings (`"2"`, `"4"`, … `"131072"`) in a dictionary to avoid `int.ToString()` allocations; the score text uses `TMP_Text.SetText("{0}", score)`, which does not allocate.

---

## 12. Testing

### 12.1 EditMode (`Tests/EditMode`, assembly `GC26._2048Nastya.Tests`)

| Test | Checks |
| --- | --- |
| `Move_Left_FourEqual_MergesPairs` | `2 2 2 2` → `4 4 _ _`, score +8 |
| `Move_Left_ThreeEqual_MergesNearWall` | `2 2 2 _` → `4 2 _ _` |
| `Move_Right_ThreeEqual_MergesNearWall` | `2 2 2 _` → `_ _ 2 4` |
| `Move_NoMergeTwiceInOneMove` | `4 4 8 _` Left → `8 8 _ _`, not `16` |
| `Move_Invalid_ReturnsMovedFalse` | a full row that cannot move or merge |
| `Move_AllDirections_Symmetric` | the same pattern rotated gives the same result for Up/Down/Left/Right |
| `Move_ReportsTileMovesAndMerges` | Ids and positions in `MoveResult2048` match the board |
| `HasMoves_FullBoardWithPair_True` / `_NoPair_False` | lose condition |
| `Spawner_SeededDistribution` | with a fixed seed and 10 000 spawns, about 10% are 4s (±1.5%) and only empty cells are used |
| `Spawner_FullBoard_ReturnsNull` | no spawn on a full board |
| `Save_RoundTrip` | save → load gives the same board, score, moves, flag |
| `Save_Corrupted_StartsNewGame` | broken JSON, wrong size, value 3 → ignored without exceptions |

### 12.2 PlayMode / manual
- Open `gc26_2048_nastya_Main` standalone, play to 2048 using a debug "fill board" context menu on `Game2048Nastya` (editor only, `[ContextMenu]`, stripped from builds with `#if UNITY_EDITOR`).
- Launch from the shell: play, pause, **To menu**, check the score arrives, no objects remain in the hierarchy, `Time.timeScale == 1`.
- Key mashing and very fast swipes: the board and the view never disagree.
- Device Simulator (package already installed): small phone, tablet, notch, portrait and landscape.

### 12.3 Before every push
```bash
python tools/check_scope.py
```
```bash
python tools/check_names.py gc26_2048_nastya
```
Enable the hook once per clone:
```bash
git config core.hooksPath tools/hooks
```

---

## 13. Risks and open questions

| Risk / question | Mitigation |
| --- | --- |
| Mixing this game up with the other 2048 in the same repository (`gc26_2048`, cheese design, branch `game/gc26_2048`, clone `C:\Users\20260047\Unity\2048`) | Separate ID, folder, branch, namespace, scene names and save keys (section 1.1); separate clone `C:\Users\20260047\Unity\GC26_Nastya`; never copy files between the two folders |
| `gc26_2048_nastya` is not in `tools/games.txt` on `main` yet | PR from branch `core/add-gc26_2048_nastya` to the organizer; until it is merged, `check_scope.py` also reports `tools/games.txt` |
| `Assets/Microgames.meta` is not in `main` yet; Unity creates it on first open, and it is outside our folder | Do not commit it on the game branch; ask the organizer to add it to `main` |
| The shell's camera and EventSystem setup is not in `main` yet | The fallback in sections 7.3 and 9; re-check when the shell scene lands |
| Animation desync under very fast input | A single source of truth (model); `FinishNow()` before every new move; PlayMode test with rapid input |

---
---

# Русская версия

# Colours — технический дизайн-документ (TDD, gc26_2048_nastya)

Дополняет [Colours_GDD.md](Colours_GDD.md). Этот документ описывает, **как** игра устроена внутри проекта микроигр GC26.
Правила репозитория — в [RULES.md](../../../../RULES.md); они важнее всего, что написано здесь.

| | |
| --- | --- |
| Unity | 6000.5.6f1 |
| Рендер-пайплайн | URP 17.5 (существующие ассеты PC/Mobile проекта; ничего не меняем) |
| UI | uGUI 2.5 + TextMeshPro (LiberationSans SDF) |
| Ввод | Input System 1.20 |
| Тесты | Unity Test Framework 1.7 (EditMode + PlayMode) |
| ID микроигры | `gc26_2048_nastya` |
| Неймспейс / сборка | `GC26._2048Nastya` / `GC26._2048Nastya.asmdef` |
| Стартовая сцена | `gc26_2048_nastya_Main` |
| Новые пакеты | **Нет.** Используются только пакеты, которые уже есть в `Packages/manifest.json` (правило 5) |

---

## 1. Ограничения проекта (из RULES.md)

| Правило | Что это значит для этой игры |
| --- | --- |
| Только своя папка | Все файлы игры лежат в `Assets/Microgames/gc26_2048_nastya/` (плюс `Assets/Microgames/gc26_2048_nastya.meta`) |
| Свой asmdef и неймспейс | Весь код в `namespace GC26._2048Nastya` (вложенные неймспейсы можно); ссылки только на `GC26.Core` и пакеты |
| Сцены с префиксом | `gc26_2048_nastya_Main.unity`, `gc26_2048_nastya_Test.unity` |
| Resources в подпапке ID | `Resources/gc26_2048_nastya/...`, загрузка `Resources.Load("gc26_2048_nastya/...")`. Лучше использовать прямые сериализованные ссылки и обходиться без Resources |
| Сохранения | Файлы только в `MicrogameSave.Folder(Id)`; ключи PlayerPrefs только через `MicrogameSave.Key(Id, "...")` |
| Без глобального состояния | Никаких `DontDestroyOnLoad`, статических синглтонов, изменений `Time.timeScale` (пауза вместо этого блокирует ввод) |
| Без общих имён | Никаких `GameManager.cs`, `Player.cs`, `UI.cs`, `Menu.cs` и т. п. (`check_names.py` их отклоняет). У классов суффикс `2048` или конкретное имя |
| Input actions | Свой ассет `gc26_2048_nastya_Controls.inputactions` с картой действий `gc26_2048_nastya_Gameplay`. Общий `Assets/InputSystem_Actions.inputactions` не трогаем |

---

### 1.1 Две игры 2048 в одном репозитории

В репозитории две отдельные игры 2048 от разных людей. Общего у них только `GC26.Core`.

| | Эта игра | Другая игра |
| --- | --- | --- |
| ID | `gc26_2048_nastya` | `gc26_2048` |
| Дизайн | Colours, «ночное стекло» (см. GDD) | Сырная тема |
| Ветка | `game/gc26_2048_nastya` | `game/gc26_2048` |
| Папка | `Assets/Microgames/gc26_2048_nastya/` | `Assets/Microgames/gc26_2048/` |
| Неймспейс / asmdef | `GC26._2048Nastya` | `GC26._2048` |
| Стартовая сцена | `gc26_2048_nastya_Main` | своя сцена с префиксом `gc26_2048_` |
| Локальный клон на этом ПК | `C:\Users\20260047\Unity\GC26_Nastya` | `C:\Users\20260047\Unity\2048` |

Никогда не копируйте ассеты между этими двумя папками вместе с `.meta`-файлами (будут одинаковые GUID). Чтобы переиспользовать что-то, дублируйте в Unity через Ctrl+D.

## 2. Структура папок

```
Assets/Microgames/gc26_2048_nastya/
    microgame.json
    GC26._2048Nastya.asmdef                 ссылки: GC26.Core, Unity.InputSystem, Unity.TextMeshPro, UnityEngine.UI
    README.md
    Docs~/                           Colours_GDD.md, Colours_TDD.md, Colours_prototype.html (Unity игнорирует папки, оканчивающиеся на ~)
    Scenes/
        gc26_2048_nastya_Main.unity          стартовая сцена (один компонент IMicrogame)
    Scripts/
        Game2048Nastya.cs                    точка входа IMicrogame, управляет игровым циклом
        Model/                               чистый C#, без зависимости от UnityEngine
            Board2048.cs
            Direction2048.cs
            MoveResult2048.cs
            TileSpawner2048.cs
            GameState2048.cs
        View/
            BoardView2048.cs
            TileView2048.cs
            TilePool2048.cs
            Hud2048.cs
            Overlays2048.cs
            Theme2048.cs                     ScriptableObject: цвета, размеры шрифта, тайминги
        Input/
            Input2048.cs
        Persistence/
            SaveStore2048.cs
        Audio/
            Sfx2048.cs
    Input/
        gc26_2048_nastya_Controls.inputactions
    Prefabs/
        gc26_2048_nastya_Tile.prefab
        gc26_2048_nastya_Cell.prefab
    Art/
        gc26_2048_nastya_Theme.asset         экземпляр Theme2048
        gc26_2048_nastya_RoundedRect.png     скруглённый спрайт с 9-slice
    Audio/
        *.wav / *.ogg, CREDITS.md
    Tests/
        EditMode/GC26._2048Nastya.Tests.asmdef
        EditMode/Board2048Tests.cs
        EditMode/SaveStore2048Tests.cs
```

Тестовая сборка лежит в папке `Tests`, поэтому `check_names.py` разрешает дополнительный asmdef.

`microgame.json`:

```json
{
  "id": "gc26_2048_nastya",
  "title": "Colours",
  "description": "Slide and merge number tiles to reach 2048",
  "entry_scene": "gc26_2048_nastya_Main",
  "authors": ["<имена команды>"]
}
```

---

## 3. Архитектура

Три слоя с односторонними зависимостями:

```
          ┌────────────────────────────────────────────┐
 Ввод ───►│ Game2048Nastya (MonoBehaviour, IMicrogame) │──► SaveStore2048 ──► MicrogameSave
          │  - машина состояний, очередь ходов, exit() │
          └──────┬───────────────────────┬─────────────┘
                 │ команды               │ результаты (MoveResult2048)
                 ▼                       ▼
          Модель (чистый C#)       Представление (MonoBehaviour)
          Board2048, Spawner       BoardView2048, TileView2048, Hud2048, Overlays2048, Sfx2048
```

- **Модель** ничего не знает об объектах Unity. С заданным сидом случайности она детерминирована, поэтому полностью покрывается EditMode-тестами.
- **Представление** только показывает то, что сообщает модель. Оно никогда не меняет состояние игры.
- **Game2048Nastya** — единственный класс, который общается с обоими слоями, и единственный `IMicrogame` в сцене.

Нигде нет изменяемых статических полей. Всё связано через сериализованные ссылки в сцене.

---

## 4. Модель

### 4.1 Данные

```csharp
namespace GC26._2048Nastya
{
    public enum Direction2048 { Up, Down, Left, Right }

    /// Одна плитка на поле. Id не меняется между ходами, чтобы представление могло её анимировать.
    public readonly struct Tile2048
    {
        public readonly int Id;     // уникален в пределах партии, > 0
        public readonly int Value;  // 2, 4, 8, ...
    }

    public sealed class Board2048
    {
        public int Size { get; }                  // 4 (3..6, если добавят размеры поля)
        Tile2048?[,] _cells;                      // [x, y], x вправо, y вниз от верхнего ряда
        int _nextId;

        public Tile2048? Get(int x, int y);
        public IEnumerable<(int x, int y)> EmptyCells();
        public bool HasMoves();                   // есть пустая клетка или равные соседи
        public MoveResult2048 Move(Direction2048 dir);
        public Tile2048 Place(int x, int y, int value);  // для спавнера, загрузки и тестов
        public Board2048 Clone();                 // для отмены хода
    }
}
```

### 4.2 Результат хода (данные для анимации)

```csharp
public sealed class MoveResult2048
{
    public bool Moved;                    // false → недействительный ход
    public int ScoreGained;               // сумма значений слитых плиток
    public List<TileMove> Moves;          // (tileId, from, to): каждая плитка, сменившая позицию
    public List<TileMerge> Merges;        // (sourceIdA, sourceIdB, newTile, cell)
    public int MaxValue;                  // самая большая плитка после хода
}
```

Появление новой плитки **не** входит в `Move`. `Game2048Nastya` вызывает `TileSpawner2048` после него, поэтому тесты могут проверять ходы без случайности.

### 4.3 Алгоритм хода

Каждый ход обрабатывается как `Size` независимых **линий**. Линия читается **от стенки, к которой движутся плитки**:

| Направление | Линии | Порядок чтения внутри линии |
| --- | --- | --- |
| Влево | каждый ряд y | x = 0 → Size-1 |
| Вправо | каждый ряд y | x = Size-1 → 0 |
| Вверх | каждый столбец x | y = 0 → Size-1 |
| Вниз | каждый столбец x | y = Size-1 → 0 |

Для одной линии (позиция `p[0]` — у стенки):

```
tiles   = непустые плитки линии в порядке чтения
out     = пустой список
i = 0
while i < tiles.Count:
    if i + 1 < tiles.Count and tiles[i].Value == tiles[i+1].Value:
        merged = новая плитка (новый Id, Value * 2)
        out.Add(merged); записать слияние(tiles[i], tiles[i+1] → merged в p[out.Count-1])
        записать перемещения tiles[i] и tiles[i+1] в p[out.Count-1]
        score += merged.Value
        i += 2                      // слитая плитка больше не сравнивается → «одно слияние за ход»
    else:
        out.Add(tiles[i]); записать перемещение, если позиция изменилась
        i += 1
записать out в p[0..out.Count-1], остальное очистить
```

Так правила из GDD выполняются автоматически: `2 2 2 2` → `4 4`, `2 2 2 _` влево → `4 2`, вправо → `_ _ 2 4`.
`Moved` равен true, если хоть одна плитка сменила позицию или произошло хоть одно слияние.

Сложность: O(Size²) за ход, без выделений памяти, кроме списков результата, которые `Game2048Nastya` переиспользует (очищает, а не создаёт заново).

### 4.4 Спавнер

```csharp
public sealed class TileSpawner2048
{
    public TileSpawner2048(System.Random rng, float fourChance = 0.1f);
    public Tile2048? Spawn(Board2048 board);   // null, если поле заполнено
}
```

- Использует `System.Random`, а не `UnityEngine.Random`, поэтому не делит глобальное состояние случайности с другими играми и принимает сид.
- Выбирает равновероятно случайную пустую клетку; значение 4 с вероятностью `fourChance`, иначе 2.

### 4.5 Состояние игры

```csharp
public sealed class GameState2048
{
    public Board2048 Board;
    public int Score;
    public int Moves;
    public bool WonShown;        // «Играть дальше» уже выбрано, окно победы больше не показывать
    public bool IsOver;
    public UndoSnapshot? Undo;   // копия поля + счёт + ходы до последнего действительного хода (Желательно)
}
```

Проверка победы: `!WonShown && result.MaxValue >= 2048`. Проверка поражения: `!Board.HasMoves()` после появления плитки.

---

## 5. Game2048Nastya: точка входа и управление потоком

Основан на шаблоне из `tools/new_microgame.py`.

```csharp
public class Game2048Nastya : MonoBehaviour, IMicrogame
{
    public const string Id = "gc26_2048_nastya";

    [SerializeField] BoardView2048 boardView;
    [SerializeField] Hud2048 hud;
    [SerializeField] Overlays2048 overlays;
    [SerializeField] Input2048 input;
    [SerializeField] Sfx2048 sfx;
    [SerializeField] Theme2048 theme;

    Action<int> _exit;
    bool _started;
    GameState2048 _state;
    SaveStore2048 _save;
    Direction2048? _buffered;

    public void Begin(Action<int> exit) { ... }   // загрузить сохранение или начать новую игру, включить ввод
    void Start() { /* отдельный запуск для теста: сам вызывает Begin, если !MicrogameRunner.IsRunning */ }
    public void ExitToMenu() { _save.SaveGame(_state); var e = _exit; _exit = null; e?.Invoke(_state.Score); }
}
```

### 5.1 Машина состояний

```
            Begin()
               │
               ▼
 ┌────────► Playing ──ход действителен──► Animating ──готово──► (проверка победы / поражения) ──► Playing
 │             │ ▲                           │ ввод → буфер на одно направление
 │      Esc    │ │ Продолжить                │
 │             ▼ │                           ├──► WinOverlay ──Играть дальше──► Playing
 │           Paused ──В меню──► Exiting (сохранить, exit(score))
 │                                           └──► GameOver ──Ещё раз──► (новая игра) ──► Playing
 └───────────────────────────────────────────────────── В меню ──► Exiting
```

- Ввод обрабатывается только в `Playing`. В `Animating` направление сохраняется в `_buffered`; когда приходит новое, текущая анимация **мгновенно завершается** (`boardView.FinishNow()`), и выполняется отложенный ход.
- `Paused` и окна отключают игровую карту действий `Input2048`. **`Time.timeScale` никогда не меняется.**

### 5.2 Один действительный ход по шагам

1. `result = _state.Board.Move(dir)`. Если `!result.Moved`: `boardView.Bump(dir)`, `sfx.Invalid()`, стоп.
2. Сохранить снимок для отмены (сделан до шага 1 через `Board.Clone()`).
3. `_state.Score += result.ScoreGained; _state.Moves++`.
4. `spawned = spawner.Spawn(_state.Board)`.
5. `boardView.Play(result, spawned)` запускает анимацию сдвиг → слияние/появление (состояние `Animating`).
6. `hud.SetScore(...)`, обновить лучший счёт и `_save.SaveGame(_state)` (маленький файл, пишется синхронно; см. 8.2).
7. Когда анимация закончилась: проверка победы → окно; `!HasMoves()` → конец игры, `_save.ClearGame()`.

---

## 6. Представление

### 6.1 Раскладка
- Один canvas **Screen Space - Camera** в сцене игры (не Overlay, чтобы он выгружался вместе со сценой и никогда не рисовался поверх оболочки). `CanvasScaler` с *Scale With Screen Size*, эталон 1080×1920, match 0.5.
- Поле — квадратный `RectTransform`, размер задаёт `AspectRatioFitter` (Fit In Parent). Позиции клеток **вычисляются** из размера поля, отступов и промежутков, а не через `GridLayoutGroup`, потому что плитки должны свободно двигаться между клетками во время анимации.
- Фоновые клетки (`gc26_2048_nastya_Cell.prefab`) создаются один раз на размер поля. Плитки лежат в отдельном контейнере над ними.

### 6.2 Плитки
- `gc26_2048_nastya_Tile.prefab`: `Image` (скруглённый спрайт с 9-slice) + `TextMeshProUGUI` + необязательный `Image` свечения.
- `TileView2048.Set(value, theme)` применяет цвет, цвет текста и размер шрифта из `Theme2048`. Значения больше, чем в таблице темы, берут последнюю запись.
- **Пул:** `TilePool2048` хранит неактивные плитки и переиспользует их, поэтому во время игры нет `Instantiate`/`Destroy`. Пул — поле `BoardView2048`, поэтому живёт и умирает вместе со сценой.
- `BoardView2048` хранит `Dictionary<int, TileView2048>` от Id плитки в модели к её представлению.

### 6.3 Анимация
- Пакетов твинов нет (правило 5), поэтому анимации сделаны маленьким помощником на корутинах: `IEnumerator Tween(float duration, Action<float> step)` с ease-out `1 - (1 - t)^3`.
- Время берётся из `Time.unscaledDeltaTime`, поэтому анимации работают правильно, кто бы ни поменял `timeScale`.
- Порядок за ход: все сдвиги параллельно (0,10 с) → «поп» слияний и появление новых плиток параллельно (0,12 с). Две плитки слияния едут в целевую клетку, затем обе возвращаются в пул, а новая плитка «выпрыгивает».
- `FinishNow()` останавливает корутины и мгновенно ставит каждую плитку в конечное состояние. Используется для буфера ввода, паузы и выхода.

### 6.4 Тема
`Theme2048` (ScriptableObject, ассет `Art/gc26_2048_nastya_Theme.asset`) хранит:
- цвета фона, поля и пустой клетки;
- список записей `(value, tileColor, textColor, glow)` из палитры GDD;
- размеры шрифта для 1–2, 3, 4 и 5+ цифр;
- длительности анимаций (сдвиг, поп, появление, вздрагивание, всплывающий счёт).

---

## 7. Ввод

### 7.1 Действия (`gc26_2048_nastya_Controls.inputactions`, карта `gc26_2048_nastya_Gameplay`)

| Действие | Тип | Привязки |
| --- | --- | --- |
| `Move` | Value, Vector2 | 2D composite: стрелки, WASD; бонусом — левый стик/крестовина геймпада |
| `PointerPress` | Button | `<Pointer>/press` |
| `PointerPosition` | Value, Vector2 | `<Pointer>/position` |
| `Pause` | Button | Escape, Start на геймпаде |
| `NewGame` | Button | N |

Ассет подключается через сериализованную ссылку `InputActionAsset`, включается в `OnEnable` и выключается в `OnDisable`, поэтому после выгрузки сцены ничего не остаётся активным.

### 7.2 Определение направления
- **Клавиши / стик:** при срабатывании `Move` берётся преобладающая ось вектора (|x| против |y|). У стика мёртвая зона 0,5, и перед следующим ходом он должен вернуться в центр.
- **Свайп / перетаскивание:** при нажатии запоминается позиция; при отпускании `delta = end - start`. Если `delta.magnitude < 0.05 * min(Screen.width, Screen.height)`, свайп игнорируется; иначе используется преобладающая ось. Ось y экрана направлена вверх, поэтому положительный y означает **вверх**.
- Нажатия, начатые на кнопке UI, игнорируются (`EventSystem.current.IsPointerOverGameObject()`), чтобы нажатие на паузу не делало ход.
- `Input2048` предоставляет события `event Action<Direction2048> Moved`, `event Action PausePressed`, `event Action NewGamePressed`.

### 7.3 EventSystem
В оболочке уже может быть `EventSystem`. Сцена игры содержит свой с `InputSystemUIInputModule`, но он работает **только если** другого нет: маленький компонент проверяет `EventSystem.current` в `Awake` и отключает EventSystem сцены, если уже активен другой.

---

## 8. Сохранения

### 8.1 Где
| Данные | Хранилище | Ключ / файл |
| --- | --- | --- |
| Лучший счёт | PlayerPrefs | `MicrogameSave.Key(Id, "best")` → `gc26_2048_nastya_best` |
| Звук вкл/выкл | PlayerPrefs | `gc26_2048_nastya_sound` (через `MicrogameSave.Key`) |
| Подсказка показана | PlayerPrefs | `gc26_2048_nastya_hint` (через `MicrogameSave.Key`) |
| Текущая партия | JSON-файл | `MicrogameSave.Folder(Id)/game.json` |

### 8.2 Формат `game.json`

```json
{
  "version": 1,
  "size": 4,
  "cells": [0, 2, 0, 0,  4, 8, 0, 0,  0, 0, 0, 0,  2, 0, 0, 0],
  "score": 120,
  "moves": 37,
  "wonShown": false
}
```

- `cells` — построчно от верхнего левого угла; 0 = пусто. Id плиток создаются заново при загрузке.
- Сериализуется через `JsonUtility` и класс `[Serializable] class SaveData2048`.
- **Безопасная запись:** пишем в `game.json.tmp`, затем заменяем им `game.json`, чтобы сбой во время записи не испортил сохранение.
- **Проверка при загрузке:** неверная `version`, размер, число клеток или значение не степень двойки → сохранение игнорируется, начинается новая игра (с предупреждением в лог, без исключений).
- История отмены не сохраняется; после продолжения партии отмена недоступна до следующего хода.

---

## 9. Интеграция с оболочкой

- В стартовой сцене **ровно один** `IMicrogame`: `Game2048Nastya` на корневом объекте `gc26_2048_nastya_Root`.
- `Begin(exit)` — единственный путь запуска. Запасной вариант в `Start()` запускает игру отдельно, только когда `MicrogameRunner.IsRunning` равен false (сцена открыта напрямую в редакторе).
- `exit(score)` вызывается ровно один раз (защищено через `_exit = null`) по кнопке **«В меню»** в паузе или в окне конца игры. Передаётся счёт текущей партии.
- Всё, что создаёт игра (плитки, объекты пула, источники звука), — дочерние объекты сцены игры, поэтому выгрузка сцены всё полностью убирает.
- Камера: своя камера в сцене есть только для отдельного теста. Если оболочка даёт свою, камера игры отключается в `Begin`. Договориться об этом с организатором, когда сцена оболочки появится в `main`.

---

## 10. Звук

- У `Sfx2048` один `AudioSource` (2D, `playOnAwake` выключен), клипы из сериализованных полей проигрываются через `PlayOneShot`.
- Высота тона при слиянии: `1 + 0.05 * log2(value)`, не больше 1,6.
- Переключатель звука меняет `AudioSource.mute`; `AudioListener.volume` не трогаем (он глобальный).
- Настройки импорта: короткие звуки — *Decompress On Load*, моно, Vorbis качество 70.

---

## 11. Цели по производительности

| Цель | Значение |
| --- | --- |
| Частота кадров | 60 FPS на телефоне среднего уровня и на любом ПК |
| Выделения памяти (GC) за ход | 0 байт после прогрева (пул плиток, переиспользуемые списки, закэшированные строки чисел) |
| Время загрузки сцены в оболочке | < 0,5 с |
| Draw calls | < 30 (один атлас для спрайтов UI, один материал шрифта TMP) |

Строки чисел (`"2"`, `"4"`, … `"131072"`) кэшируются в словаре, чтобы не выделять память на `int.ToString()`; текст счёта выводится через `TMP_Text.SetText("{0}", score)`, который не выделяет память.

---

## 12. Тестирование

### 12.1 EditMode (`Tests/EditMode`, сборка `GC26._2048Nastya.Tests`)

| Тест | Что проверяет |
| --- | --- |
| `Move_Left_FourEqual_MergesPairs` | `2 2 2 2` → `4 4 _ _`, счёт +8 |
| `Move_Left_ThreeEqual_MergesNearWall` | `2 2 2 _` → `4 2 _ _` |
| `Move_Right_ThreeEqual_MergesNearWall` | `2 2 2 _` → `_ _ 2 4` |
| `Move_NoMergeTwiceInOneMove` | `4 4 8 _` влево → `8 8 _ _`, а не `16` |
| `Move_Invalid_ReturnsMovedFalse` | заполненный ряд, который не может сдвинуться или слиться |
| `Move_AllDirections_Symmetric` | один и тот же повёрнутый шаблон даёт одинаковый результат для всех направлений |
| `Move_ReportsTileMovesAndMerges` | Id и позиции в `MoveResult2048` совпадают с полем |
| `HasMoves_FullBoardWithPair_True` / `_NoPair_False` | условие поражения |
| `Spawner_SeededDistribution` | с фиксированным сидом и 10 000 появлений около 10% — четвёрки (±1,5%), используются только пустые клетки |
| `Spawner_FullBoard_ReturnsNull` | на заполненном поле плитка не появляется |
| `Save_RoundTrip` | сохранение → загрузка дают то же поле, счёт, ходы и флаг |
| `Save_Corrupted_StartsNewGame` | битый JSON, неверный размер, значение 3 → игнорируется без исключений |

### 12.2 PlayMode / вручную
- Открыть `gc26_2048_nastya_Main` отдельно и дойти до 2048 с помощью отладочного пункта контекстного меню «заполнить поле» на `Game2048Nastya` (только в редакторе, `[ContextMenu]`, убирается из сборок через `#if UNITY_EDITOR`).
- Запуск из оболочки: играть, пауза, **«В меню»**, проверить, что счёт пришёл, в иерархии не осталось объектов, `Time.timeScale == 1`.
- Быстрое нажатие клавиш и очень быстрые свайпы: поле и представление никогда не расходятся.
- Device Simulator (пакет уже установлен): маленький телефон, планшет, вырез экрана, портрет и альбом.

### 12.3 Перед каждым пушем
```bash
python tools/check_scope.py
```
```bash
python tools/check_names.py gc26_2048_nastya
```
Один раз на клон включить хук:
```bash
git config core.hooksPath tools/hooks
```

---

## 13. Риски и открытые вопросы

| Риск / вопрос | Как снизить |
| --- | --- |
| Перепутать эту игру с другой 2048 в том же репозитории (`gc26_2048`, сырный дизайн, ветка `game/gc26_2048`, клон `C:\Users\20260047\Unity\2048`) | Отдельные ID, папка, ветка, неймспейс, имена сцен и ключи сохранений (раздел 1.1); отдельный клон `C:\Users\20260047\Unity\GC26_Nastya`; никогда не копировать файлы между двумя папками |
| `gc26_2048_nastya` ещё нет в `tools/games.txt` в `main` | PR из ветки `core/add-gc26_2048_nastya` организатору; пока он не влит, `check_scope.py` также ругается на `tools/games.txt` |
| `Assets/Microgames.meta` ещё нет в `main`; Unity создаст его при первом открытии, и он вне нашей папки | Не коммитить его в игровую ветку; попросить организатора добавить его в `main` |
| Настройка камеры и EventSystem в оболочке ещё не попала в `main` | Запасные варианты из разделов 7.3 и 9; перепроверить, когда появится сцена оболочки |
| Рассинхрон анимации при очень быстром вводе | Единый источник правды (модель); `FinishNow()` перед каждым новым ходом; PlayMode-тест с быстрым вводом |
