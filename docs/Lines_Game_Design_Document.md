# Lines — Game Design Document

Oct 21, 2026 · revised Nov 2, 2026 · hmmd2

Earlier online version (before the Nov 2 revision): [https\://claude.ai/artifact/MbLgFyYVPGJvoRkVvUrSSQ](https://claude.ai/artifact/MbLgFyYVPGJvoRkVvUrSSQ)

## Overview

Lines remakes the classic 1998 Windows puzzle game for phones, with a few custom changes. Players move balls on a 9×9 board to form lines of five and try to last as long as possible while new balls keep arriving.

This is a study project. The game is built for iOS and Android and installed directly on our own phones; it is **not published** to the App Store or Google Play.

The document separates the **MVP** (the first complete version) from **later** features. Anything marked *Later* is out of scope for the MVP.

| Item | Value |
| :---- | :---- |
| Genre | Casual turn-based puzzle, endless high-score |
| Platforms | iOS first, Android second. Day-to-day testing in the iOS Simulator and the Android Emulator, with regular checks on real phones |
| Distribution | None. Builds are installed directly on our own devices |
| Engine | Unity 6 (6000.5.6f1), C\# |
| Orientation | Portrait, one-handed play |
| Input | Touch (mouse clicks also work in the Unity editor, for testing) |
| Session length | 3–10 minutes. The game in progress is saved automatically, so the player can close it at any time and continue later |
| Audience | Players aged 25–55 who remember the original, plus casual puzzle fans |
| Development time | 8 weeks, one developer working with Claude |

**Selling points**

- The original rules people remember, plus a few small changes that make it easier to play.
- Fast, clean controls: two taps per move and no timers.
- The game in progress is saved automatically and picks up where the player left off.
- Seven ball colors chosen so they are easy to tell apart.

**Custom changes compared to the original**

- **Undo:** take back the last move (one step).
- **Reset:** start a new game from the game screen at any time.
- **Blocked-move alert:** when a ball can't reach the tapped cell, the player sees a message instead of nothing happening.

## Core gameplay and rules

Each turn the player makes one move. A move that completes a line removes it and costs nothing. A move that doesn't brings 3 new balls onto the board.

![Turn loop](Lines_turn_loop.png)

Every turn ends either with a cleared line, which skips the spawn, or with 3 new balls arriving. The game ends only after a spawn fills the board.

| Rule | Value |
| :---- | :---- |
| Board | 9×9 cells (81) |
| Colors | 7 |
| Balls at the start | 3, in random cells, as in the classic game |
| New balls per turn | 3. Their colors **and the cells where they will appear** are shown in advance (the cells show small preview dots) |
| Movement | One ball per turn to any empty cell that has a path to it |
| Path | Up, down, left and right through empty cells. No diagonals and no jumping over balls |
| Line | 5 or more balls of one color in a row, column or diagonal |
| After a line clears | No new balls spawn that turn. The preview stays the same for the next turn |
| Spawned balls | Can also complete a line, which clears and scores |
| Game over | No empty cells are left |

**Edge cases**

- A move that forms two crossing lines clears both. The shared ball counts once.
- If the player moves a ball onto a cell with a preview dot, the ball for that dot appears in another random empty cell instead.
- If fewer than 3 cells are free when balls spawn, the board fills and the game ends.
- Game over is final: Undo is not available after the board fills. The only option is to start a new game.
- Tapping a ball that has no reachable empty cell still selects it. The blocked move is reported when the player taps a target (see Controls). The original game shows nothing here; this is one of the custom changes.

## Controls and feedback

The whole game is played with two taps: tap a ball, then tap an empty cell.

| Player action | What happens |
| :---- | :---- |
| Tap a ball | The ball is selected and bounces in place |
| Tap another ball | Selection moves to that ball |
| Tap a reachable empty cell | The ball moves along the shortest path, cell by cell (about 40 ms per cell) |
| Tap an unreachable cell | The ball shakes, a short "No path" alert appears near the board, and the ball stays selected |
| Tap the selected ball again | Nothing happens. The ball stays selected. To change the selection, tap another ball |
| Undo button | Restores the board, the score and the preview to how they were before the last move. Only one step, as in the classic game: after an undo, the button is disabled until the next move. Also disabled at the start of a game |
| Reset button | Asks for confirmation, then clears the board and starts a new game |

Input is ignored while a move, clear or spawn animation is playing.

- Cells are at least 9 mm wide on a 6-inch phone, so the board fills the screen width.
- *Later:* a movement effect closer to the original, where the ball visibly steps from cell to cell along its path.
- *Later:* light vibration on a line clear, with an off switch in Settings.

## Scoring

Longer lines are worth much more than short ones, which rewards the player for taking risks. The values below are starting points to tune during testing in Week 7.

| Balls cleared in one line | Points |
| :---- | :---- |
| 5 | 10 |
| 6 | 14 |
| 7 | 20 |
| 8 | 28 |
| 9 | 36 |

- **Several lines in one move:** the total for the move is multiplied by the number of lines (two lines \= ×2).
- **Lines completed by spawned balls:** score normally, with no multiplier.
- **Undo:** removes the points scored by the move it takes back.
- *Later:* best score, saved on the device and shown on the game screen and the game-over screen.

## Preview modes

The original game had three difficulty levels that differ only in how much the player knows about the next balls. The MVP ships with the first one. The other two are later features.

| Mode | What the player sees before the spawn | Release |
| :---- | :---- | :---- |
| Full preview | The colors of the next 3 balls and the cells where they will appear | MVP |
| Colors only | The colors of the next 3 balls, but not where they will appear | Later |
| No preview | Nothing about the next balls | Later |

## Later features (not in the MVP)

These are planned for after the MVP and are not scheduled in the 8 weeks unless time is left over.

| Feature | Notes |
| :---- | :---- |
| Preview modes | "Colors only" and "No preview" (see above), chosen when starting a new game |
| Main menu | Play / Continue, Modes, Leaderboard, Settings, How to play. In the MVP the app opens straight into the game |
| Pause menu | Not needed: nothing happens until the player acts |
| Best score and "New record" banner | Saved on the device |
| Settings screen | Sound, music, vibration, theme |
| How to play | 3 short illustrated cards explaining the rules, shown automatically on first launch |
| Audio | See the sound list below |
| Vibration | Light vibration on a line clear |
| Light and dark themes | The MVP has one theme |
| Custom art packs | Replace the generated balls and board with your own art |
| Other game modes | Daily Challenge (the same seed for every player that day, one attempt) and Timed (3 minutes, +5 seconds per cleared line) |
| Leaderboard | Local top 10 on the device |
| Achievements and statistics | For example: first 7-ball line, two lines in one move, 1,000 points in one game. Statistics: games played, best score, longest line, average score |
| Unlockable themes | Ball and board themes unlocked by reaching score milestones |
| Web version | A browser build of the same game |

**Planned sounds (later)**

| Event | Sound |
| :---- | :---- |
| Select a ball | Soft click |
| Move | Short rolling sound |
| Line cleared | Bright chime, higher pitch for longer lines |
| Balls spawn | Quiet pop |
| Blocked move | Low buzz, together with the "No path" alert |
| Game over | Short descending melody |

A calm background music loop, with each sound switchable in Settings.

## Screens and UI (MVP)

The MVP has one screen and two pop-ups. The app opens straight into the game: the saved game if there is one, otherwise a new game.

| Screen | Contents | Opens from |
| :---- | :---- | :---- |
| Game | Board, score, preview of the next 3 balls, Undo, Reset | App launch |
| Reset confirmation | "Start a new game?" with Yes and Cancel | Reset button |
| Game over | Final score and a "Start new game" button | Game, when the board is full |

**Game screen layout (portrait, top to bottom)**

1. Score and the preview of the next 3 ball colors.
2. The board, filling the screen width, with preview dots in the cells where the next balls will appear.
3. Undo and Reset buttons, within easy reach of the thumb.

**Saving the game in progress (MVP)**

- The game is saved after every move and when the app is closed or goes to the background.
- The save holds the board, the score, the preview (colors and cells), the preview mode and the Undo step, so Undo still works after a restart.
- On launch, a saved game is restored automatically. There is no Continue button, because the MVP has no main menu.
- When a game ends, the save is cleared, so the next launch starts a new game. Reset also starts a new save.

## Art

The look is a clean, modern take on the original: glossy balls on a soft, light board, with nothing cluttering the play area.

- **Generated in code for the MVP:** the balls are drawn by the game's own code as glossy circles with shading and a highlight, so no image files or art tools are needed. Custom art packs are a later feature.
- 7 fixed ball colors that stay easy to tell apart: red, orange, yellow, green, cyan, blue, purple. The exact color values are set once in code and used everywhere.
- A flat board with subtle cell borders. One theme in the MVP.
- Preview dots: small versions of the next balls, drawn in their target cells.
- Animations: the selected ball bounces, balls grow in when they spawn, cleared balls shrink and burst into particles, and a blocked move shakes the ball.
- No audio in the MVP (see Later features).

## Technical summary

The game logic is plain C\# kept separate from Unity, so it can be unit-tested. The Unity layer only draws the board and takes input.

**Code-first approach.** The project has a single, nearly empty `Main` scene. Everything else — the board, the balls, the UI — is created by code when the game starts. No scenes, prefabs or layouts are built by hand in the Unity editor; the editor is used only to look at the result. This lets the whole game be written and changed as code.

| Area | Choice |
| :---- | :---- |
| Engine | Unity 6 (6000.5.6f1), 2D, URP |
| Logic | Plain C\#: `Board`, `Pathfinder` (BFS), `LineDetector`, `Spawner` (seeded RNG, picks colors and cells in advance), `ScoreCalculator` |
| Turn flow | State machine: Idle → Ball selected → Moving → Resolving → Spawning → Check game over |
| Rendering | Pooled ball objects. Ball and board images generated in code. Animations with a small animation helper written in our own code (no third-party animation library) |
| Input | New Input System, touch (mouse in the editor) |
| UI | uGUI with TextMeshPro, created from code, safe-area aware |
| Saving | `SaveService`: the game in progress as JSON in `Application.persistentDataPath`, saved after each move and when the app goes to the background |
| Testing | EditMode unit tests for all game logic, run from the command line. Visual checks in the iOS Simulator and Android Emulator, then on real phones |
| Builds | iOS: Unity → Xcode project → iOS Simulator, and your own iPhone with a free Apple account (the app must be reinstalled every 7 days). Android: APK installed directly on the emulator and your phone |
| Targets | 60 FPS on older phones. iOS 15+, Android 8.0+ |

The week-by-week schedule is in the separate 8-week development plan (`Lines_Plan_EN_RU.md`).
