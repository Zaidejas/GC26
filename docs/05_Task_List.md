# Minesweeper (Mobile): Task List

All tasks from [01_Project_Plan.md](01_Project_Plan.md), broken into small pieces you can finish in one sitting. Target: **Android phones, portrait**.

**Project:** the existing Unity project `GC26` (Unity 6000.5.6f1, URP), branch `gc26_minesweeper` of [Zaidejas/GC26](https://github.com/Zaidejas/GC26).
**Task board:** all 66 tasks are on the GitHub project [gc26_minesweeper](https://github.com/users/Zaidejas/projects/6) as cards with Status, Week, Priority and Estimate. Track progress there; this file is the reference copy.

**How to read this list**
- **Estimate:** minutes of focused work. Each week's tasks add up to **90 minutes**. The other 30 minutes of the 2-hour session go to the routine: 10 min reviewing notes, 15 min testing (Device Simulator and phone), 5 min committing.
- **Priority:** **Must** = needed for the MVP or release · **Should** = planned feature · **Could** = optional; drop it first if you're behind.
- **Depends on:** tasks that must be finished first.
- **Done when:** how you know the task is finished.
- Tick the box when a task is done. The same list is in [05_Task_List.csv](05_Task_List.csv) if you want to import it into Trello, GitHub Projects, Jira or Excel.

**Totals:** 66 tasks · 19.5 hours of task work + 6.5 hours of session routine = 26 hours.

---

## Month 1: Foundations

### Week 1: Setup, first phone build, C# refresher
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-01 | Install Unity 6000.5.6f1 (the GC26 project's version) with Android Build Support (OpenJDK, Android SDK & NDK Tools) | 20 | Must | — | Unity 6000.5.6f1 with the Android module appears under Installs in Unity Hub |
| ☐ | MS-02 | Install Visual Studio 2022 (Unity workload) or VS Code + Unity extension, and set it as the external script editor | 10 | Must | MS-01 | Double-clicking a script in Unity opens it with autocomplete working |
| ☐ | MS-03 | Prepare the GC26 project on branch `gc26_minesweeper`: switch platform to Android, set Portrait, make the camera orthographic, add the 2D Sprite package, create the folders | 15 | Must | MS-01 | Platform shows Android, orientation is Portrait, camera is orthographic; `Scripts/{Core,View,UI,Managers}`, `Sprites`, `Prefabs`, `Scenes`, `Audio` exist |
| ☐ | MS-04 | Check Git for the `gc26_minesweeper` branch: `.gitignore` excludes `Library`/`Temp`/`Logs`; commit the setup and push | 15 | Must | MS-03 | The setup commit is on GitHub in `gc26_minesweeper` and the `Library` folder is not in it |
| ☐ | MS-05 | Enable Developer options and USB debugging on the phone; Build And Run an empty scene | 20 | Must | MS-03 | The empty app opens on the phone |
| ☐ | MS-06 | C# refresher: classes, `int[,]`, `List<T>`, `Queue<T>`, enums, `Random`, MonoBehaviour basics | 10 | Must | — | You can explain what `Start`, `Update` and `[SerializeField]` do |

### Week 2: Board data model → **Milestone M1**
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-07 | Write the `Cell` class | 10 | Must | MS-03 | The class has `IsMine`, `AdjacentMines`, `IsRevealed`, `IsFlagged` |
| ☐ | MS-08 | Write the `Board` class: constructor, `Cell[,]` grid, `GetCell`, `InBounds`, `Neighbours` | 25 | Must | MS-07 | A board of any size can be created; corners return 3 neighbours |
| ☐ | MS-09 | Implement `PlaceMines(safeX, safeY)` with the 3×3 safe area | 20 | Must | MS-08 | The exact number of red flags (mines in code) is placed and none lands in the safe area |
| ☐ | MS-10 | Implement `CalculateNumbers()` | 15 | Must | MS-09 | Every safe cell stores its correct neighbour mine count |
| ☐ | MS-11 | Write a temporary `BoardDebug` script that prints the board to the Console | 20 | Must | MS-10 | Play mode prints a readable grid of `*` and numbers that you check by hand |

### Week 3: Graphics and the visible grid
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-12 | Find or draw sprites (at least 128×128 px): hidden, empty, 1–8, yellow flag (player marker), rectangular red flag (hazard), hit red flag, crossed-out yellow flag | 25 | Must | — | All 14 sprites are in `Assets/Sprites`; red and yellow flags are easy to tell apart on a phone |
| ☐ | MS-13 | Set the sprite import settings (Pixels Per Unit = sprite size, Compression None/High Quality, mipmaps off) | 10 | Must | MS-12 | Sprites look sharp on a high-resolution screen and each one is 1 world unit wide |
| ☐ | MS-14 | Create the `Tile` prefab (SpriteRenderer + BoxCollider2D) and the `TileView` script | 20 | Must | MS-13 | The prefab exists with all sprites assigned in the Inspector |
| ☐ | MS-15 | Write `BoardView.Build()` to spawn one tile per cell | 20 | Must | MS-08, MS-14 | A 9×9 grid of hidden tiles appears in Play mode |
| ☐ | MS-16 | Write `CameraController.Fit()` for portrait screens, with space for the top and bottom bars | 15 | Must | MS-15 | The whole board is visible and centred in the Device Simulator on a phone |

### Week 4: Touch input → **Milestone M2**
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-17 | Write `InputHandler` with `Pointer.current`: screen position → grid (x, y), tap detection (lift without moving) | 25 | Must | MS-15 | A tap logs the correct cell, on the phone and with the mouse in the editor |
| ☐ | MS-18 | A tap opens a single cell and redraws it | 15 | Must | MS-17 | The tapped tile shows its number, an empty tile or a red flag |
| ☐ | MS-19 | Long press (0.4 s held still) toggles a flag; implement `Board.ToggleFlag` | 25 | Must | MS-17 | Long press places and removes a flag; a flagged cell can't be opened |
| ☐ | MS-20 | Finish `TileView.Refresh()` for every cell state | 10 | Must | MS-14 | Each state shows the right sprite |
| ☐ | MS-21 | Test tap and long press on the phone; tune `longPressTime` | 15 | Must | MS-18, MS-19 | Taps always hit the cell under the finger and long press feels natural |

---

## Month 2: A playable game

### Week 5: Flood fill and the safe first tap
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-22 | Implement flood fill in `Board.Reveal()` with a `Queue` (BFS) | 35 | Must | MS-18 | Tapping a 0 opens the whole empty region and its numbered border |
| ☐ | MS-23 | Make `Reveal()` return the changed cells, and add `BoardView.RefreshCells()` | 15 | Must | MS-22 | Only the changed tiles are redrawn |
| ☐ | MS-24 | Place the red flags (mines in code) on the first tap instead of when the board is created | 20 | Must | MS-09, MS-22 | 20 first taps in a row are all safe and each opens an area |
| ☐ | MS-25 | Test flood fill on a 16×30 board on the phone for speed and correctness | 20 | Must | MS-22 | No lag or errors; flood fill never opens flagged cells |

### Week 6: Game states, win and loss
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-26 | Create the `GameState` enum and the `GameManager` script (`OnTap`, `OnLongPress`); move input handling into it | 25 | Must | MS-24 | `GameManager` owns the Board and switches Ready → Playing on the first reveal |
| ☐ | MS-27 | Handle a loss: show all red flags, highlight the hit one, cross out wrong yellow flags, block input | 25 | Must | MS-26 | Opening a red flag ends the round and the board can't be changed |
| ☐ | MS-28 | Handle a win: detect it, cover the remaining red flags with yellow flags, block input | 20 | Must | MS-26 | Opening the last safe cell ends the round as a win |
| ☐ | MS-29 | Add `NewGame()` restart, with a temporary on-screen button | 20 | Must | MS-26 | The button gives a fresh board in any state with no leftover tiles and every cell hidden |

### Week 7: Mobile user interface → **Milestone M3 (playable MVP)**
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-30 | Add a Canvas with Canvas Scaler (Scale With Screen Size, 1080×1920, Match 0.5), EventSystem and TextMeshPro | 10 | Must | — | Text appears at the same relative size on different phones in the Device Simulator |
| ☐ | MS-31 | Add the `SafeArea` script on the UI root panel | 10 | Must | MS-30 | UI stays clear of the notch on a notched phone in the Device Simulator |
| ☐ | MS-32 | Top bar: red flag counter (red flags − yellow flags) and timer (starts on first reveal, stops at the end, caps at 999) | 15 | Must | MS-29, MS-30 | Both update correctly; the counter can go negative |
| ☐ | MS-33 | Face button with 🙂 / 😮 / 😎 / 😵 states that restarts on tap; remove the temporary button | 15 | Must | MS-30 | The face matches the state and tapping it restarts |
| ☐ | MS-34 | Bottom bar: flag-mode toggle (⛏ / yellow flag) calling `GameManager.ToggleFlagMode()` | 15 | Must | MS-26, MS-30 | In flag mode a tap flags; the button shows the active mode |
| ☐ | MS-35 | Win and loss message panel with Play again and Menu buttons; the loss panel shows a random line from `Assets/Text/loss_lines.txt` (`LossLines` helper; create the file with a few lines) | 15 | Must | MS-30 | The panel appears at the end of a round; each loss shows a line from the file; a line added to the file appears without code changes |
| ☐ | MS-36 | Stop touches on UI from reaching the board; build to the phone; tag `v0.1-mvp` | 10 | Must | MS-33, MS-34 | Tapping buttons never opens a tile; a full round works on the phone; the tag is on GitHub |

### Week 8: Difficulty levels, main menu, Back button
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-37 | Create the `DifficultySettings` ScriptableObject and the Beginner 9×9/10, Intermediate 16×16/40, Expert 16×30/99 assets | 15 | Should | MS-26 | Three assets exist with the correct sizes and mine counts |
| ☐ | MS-38 | Create the static `GameSession` class and read it in `NewGame()` | 5 | Should | MS-37 | The game uses `GameSession.Selected`, or the default if it's empty |
| ☐ | MS-39 | Build the `MainMenu` scene (scene 0, the first screen): title, three large difficulty cards, settings button; no Quit button | 30 | Should | MS-38 | The app opens on this screen; the layout matches the GDD sketch and sits inside the safe area |
| ☐ | MS-40 | Scene loading, Menu button, and Android Back (Escape key): game → menu, menu → quit | 15 | Should | MS-39 | Back behaves correctly on the phone; Menu → Game → Menu works repeatedly with no errors |
| ☐ | MS-41 | Test all screens in the Device Simulator on a 16:9 phone, a 20:9 notched phone and a 4:3 tablet | 25 | Should | MS-40 | Board and UI fit on all three with nothing hidden or overlapping |

---

## Month 3: Polish, testing, release

### Week 9: Zoom, pan and advanced gameplay
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-42 | Pinch to zoom in `CameraController`, limited between the minimum size and "whole board visible" | 25 | Should | MS-16 | Pinching zooms smoothly on the phone and never ends in a tap |
| ☐ | MS-43 | One-finger drag to pan, clamped to the board; a drag never counts as a tap | 15 | Should | MS-42 | The view follows the finger and can't leave the board |
| ☐ | MS-44 | Chording: tapping an opened number calls `Board.Chord()` | 15 | Should | MS-22 | It opens neighbours only when the flag count equals the number; a wrong flag causes a loss |
| ☐ | MS-45 | Save and load the best time per difficulty with `PlayerPrefs` | 15 | Should | MS-28, MS-37 | A best time survives closing and reopening the app |
| ☐ | MS-46 | Show best times on the menu cards and "New record!" on the win panel | 10 | Should | MS-45 | Cards show a time or "—"; the message appears only on a record |
| ☐ | MS-47 | Pause the timer while the app is in the background (`OnApplicationPause`) | 10 | Should | MS-32 | Pressing Home for 30 s doesn't add 30 s to the timer |

### Week 10: Audio, vibration and polish → **Milestone M4**
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-48 | Find sound effects: tap, flag, flood fill, loss (hitting a red flag), win (check the licences) | 15 | Should | — | 5 clips are in `Assets/Audio`, with licences noted |
| ☐ | MS-49 | Write `AudioManager` and play the sounds on game events | 20 | Should | MS-48 | Each event in the GDD feedback table plays its sound |
| ☐ | MS-50 | Settings panel with Sound and Vibration toggles, saved in `PlayerPrefs` | 15 | Should | MS-39, MS-49 | Both settings stay as chosen after reopening the app |
| ☐ | MS-51 | `Haptics` helper: vibrate on long-press flag and on loss | 10 | Should | MS-50 | The phone buzzes on those events only when Vibration is on |
| ☐ | MS-52 | Pressed tile highlight and 😮 face while a finger is held on the board | 10 | Should | MS-33 | The tile under the finger darkens and the face changes; both reset on lift or drag |
| ☐ | MS-53 | Tile "pop" animation when opened | 10 | Could | MS-23 | Opened tiles scale up briefly and back |
| ☐ | MS-54 | Camera shake on loss | 10 | Could | MS-27 | The camera shakes briefly and returns to its exact position |

### Week 11: Automated tests and bug fixing
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-55 | Unity Test Framework (already installed): create a `Core` asmdef and an Edit Mode test asmdef | 15 | Must | MS-08 | An empty test runs green in the Test Runner window |
| ☐ | MS-56 | Write unit tests for `Board` (mine count, safe area, numbers, flood fill, flags, win, chord, Expert 16×30) and `LossLines` (skips empty and `#` lines, empty list) | 35 | Must | MS-55 | At least 9 tests, all passing |
| ☐ | MS-57 | Run through the manual test checklist in the progress tracker **on the phone** | 25 | Must | MS-46 | Every item is checked; failures are added to the bug list |
| ☐ | MS-58 | Fix the bugs found | 15 | Must | MS-57 | No High-severity bugs left open |

### Week 12: Android build and settings
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-59 | Player Settings: package name, version, adaptive icon, splash, portrait only, IL2CPP + ARM64, minimum/target API level | 20 | Must | — | All settings are filled in and a build succeeds with them |
| ☐ | MS-60 | Remove `BoardDebug` and unneeded `Debug.Log` calls; set `Application.targetFrameRate = 60` | 10 | Must | — | The Console is clean during normal play and the game runs at 60 fps |
| ☐ | MS-61 | Create a keystore and make a signed release build (APK; AAB if publishing); back up the keystore outside Git | 20 | Must | MS-59, MS-60 | A signed APK installs on the phone without Unity; the keystore has a backup |
| ☐ | MS-62 | Test the release build on 2+ devices: notch, Back button, phone call / lock screen mid-game, return to app | 25 | Must | MS-61 | Everything behaves as described in the GDD edge-case table |
| ☐ | MS-63 | Upload the AAB to Google Play internal testing | 15 | Could | MS-61 | The app installs from the Play internal-testing link |

### Week 13: Buffer and release → **Milestone M5**
| ✓ | ID | Task | Est. | Priority | Depends on | Done when |
|---|---|---|---|---|---|---|
| ☐ | MS-64 | Finish any tasks that slipped from earlier weeks | 40 | Must | — | No Must tasks left open |
| ☐ | MS-65 | Write the repository `README.md`: description, touch controls, phone screenshots, build steps | 20 | Must | MS-61 | The README is visible on GitHub |
| ☐ | MS-66 | Tag `v1.0`, then fill in the retrospective in the progress tracker. Optional: share the APK on itch.io | 30 | Must | MS-65 | The tag is on GitHub and the retrospective is written |

---

## Critical path
These tasks depend on each other in a chain. If one of them is late, everything after it is late too, so finish them first:

MS-05 (phone build) → MS-07 → MS-08 → MS-14/MS-15 → MS-17 → MS-18 → MS-22 → MS-24 → MS-26 → MS-27/MS-28 → MS-30–MS-36 (MVP) → MS-37–MS-40 → MS-55–MS-58 → MS-61 → MS-66

## If you fall behind
Cut tasks in this order:
1. **Could:** MS-53, MS-54, MS-63, and the itch.io upload in MS-66
2. **Should, polish:** MS-52, MS-51, MS-50
3. **Should, features:** MS-44 (chording), MS-45/MS-46 (best times), MS-47 (pause timer)

**Don't cut zoom and pan (MS-42, MS-43) unless you also drop Expert** or shrink it to 16×24; without zoom, Expert cells are too small for a finger.

Don't cut testing (MS-55–MS-58) or the signed Android build (MS-61, MS-62).
