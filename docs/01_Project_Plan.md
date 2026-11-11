# Minesweeper for Mobile in Unity (C#): 3-Month Project Plan

**Time budget:** 13 weeks × 2 hours/week = **26 hours total**
**Start:** week of 26 Oct 2026 → **Finish:** week of 18 Jan 2027 (adjust the dates to your actual start)
**Platform:** **Android phones, portrait orientation**. iOS is optional and needs a Mac to build.
**Tools:** Unity 6000.5.6f1 with Android Build Support, C#, Visual Studio 2022 or VS Code, Git + GitHub, an Android phone with a USB cable
**Project:** the existing Unity project `GC26` (URP, Input System and Test Framework already installed), branch `gc26_minesweeper` of [Zaidejas/GC26](https://github.com/Zaidejas/GC26)
**Task board:** [GitHub project gc26_minesweeper](https://github.com/users/Zaidejas/projects/6) (66 tasks, see `05_Task_List.md`)

---

## 1. Goal and scope

### Theme
Classic Minesweeper rules with an **"avoid the red flags"** theme: the hidden hazards are rectangular **red flags** (classic "mines"), and the player marks suspicious cells with **yellow flags**. In the code and in these documents, "mine" still means a hidden red flag (see GDD 1.1).

### Must have (MVP, done by the end of week 7)
- A grid of cells with randomly placed red flags, shown in portrait mode
- Every new board starts with all cells hidden; nothing is revealed until the player taps
- **Tap** reveals a cell; **long press** places or removes a flag (with a short vibration)
- A **flag-mode toggle button**: while it's on, a tap places a flag instead of revealing
- Numbers show how many mines touch each cell
- Revealing an empty (0) cell automatically opens its neighbours (flood fill)
- The first tap is always safe
- Win when every safe cell is revealed; lose when a red flag is revealed
- On a loss, the pop-up shows one random line from a list under the "lost" message; the list is a text file you can add lines to later
- UI: mine counter, timer, restart button, all inside the phone's **safe area** (clear of the notch and rounded corners)
- Runs on a real Android phone

### Should have (weeks 8–10)
- Three difficulty levels designed for portrait: Beginner 9×9 with 10 mines, Intermediate 16×16 with 40, **Expert 16×30** with 99 (the classic 30×16 board turned upright)
- A main menu scene; the Android **Back** button works
- **Pinch to zoom and drag to pan**, needed so Expert cells are big enough to tap
- Chording: tap a revealed number whose flags are all placed to open its remaining neighbours
- Best times saved with `PlayerPrefs`
- Timer pauses when the app goes to the background
- Sound effects, vibration, settings for sound and vibration on/off

### Nice to have (only if there is time left)
- Small animations (tile pop, camera shake)
- Google Play internal-testing release
- iOS build (needs a Mac and an Apple developer account)
- A custom board size

### Out of scope
Desktop and WebGL versions, landscape orientation, multiplayer, online leaderboards, ads and in-app purchases, level editor.

---

## 2. How to use each 2-hour session

Use the same rhythm every week so no time is lost:

| Minutes | Activity |
|---|---|
| 0–10 | Read last week's notes in `03_Progress_Tracker.md`, open the project, `git pull` |
| 10–100 | Do the week's tasks |
| 100–115 | Test in the **Device Simulator** (Window → General → Device Simulator); from week 4 on, also **Build And Run** to your phone at least once a week |
| 115–120 | `git commit` and `git push`, write 2–3 lines in the tracker: what's done, what's next, blockers |

**Rule:** if a task takes longer than planned, cut a "Nice to have" item. Don't skip testing or committing.

**Why test on a real phone often:** the Device Simulator shows screen size and notches correctly, but touch feel (tap accuracy, long-press timing, pinch) can only be judged with a real finger on a real screen.

---

## 3. Milestones

| Milestone | Week | Result |
|---|---|---|
| M1: Setup and logic | 2 | An empty app runs on the phone; board logic prints to the Console |
| M2: Visible board | 4 | You can tap and long-press cells on the phone |
| M3: Playable MVP | 7 | A complete game on the phone you can win and lose, with UI |
| M4: Feature complete | 10 | Menu, difficulties, zoom/pan, chording, sound, vibration, best times |
| M5: Release | 13 | Tested, signed Android build (APK/AAB) |

---

## 4. Week-by-week plan

### Month 1: Foundations

#### Week 1: Setup, first phone build, C# refresher
**Goal:** a working project that already runs on your phone. Getting the Android toolchain working early removes the biggest technical risk.
- [ ] Install **Unity 6000.5.6f1** (the version the GC26 project uses) in Unity Hub with the **Android Build Support** module, including **OpenJDK** and **Android SDK & NDK Tools**
- [ ] Install Visual Studio 2022 Community with the "Game development with Unity" workload, or VS Code with the Unity extension
- [ ] Open the existing `GC26` project and make sure the branch `gc26_minesweeper` is checked out (`git status`). On a new computer, first run `git clone https://github.com/Zaidejas/GC26.git` and `git checkout gc26_minesweeper`
- [ ] File → Build Profiles → **Switch Platform to Android**
- [ ] Player Settings → Resolution and Presentation → Default Orientation: **Portrait**
- [ ] The project was created from the 3D URP template, so set the Main Camera's **Projection to Orthographic** and add the **2D Sprite** package (Package Manager) for slicing sprites
- [ ] Create the folder structure: `Assets/Scripts/{Core,View,UI,Managers}`, `Assets/Sprites`, `Assets/Prefabs`, `Assets/Audio` (`Assets/Scenes` and `Assets/Settings` already exist)
- [ ] Git is already set up: check that `.gitignore` excludes `Library`, `Temp` and `Logs`, then commit the setup and push to `gc26_minesweeper`
- [ ] On the phone: enable **Developer options** (tap "Build number" 7 times) and **USB debugging**; connect by USB and use **Build And Run** on an empty scene
- [ ] C# refresher: classes, 2D arrays `int[,]`, `List<T>`, `Queue<T>`, enums, `Random`, MonoBehaviour basics (`Start`, `Update`, `[SerializeField]`)

**Done when:** an empty app opens on your phone and the setup commit is on GitHub in `gc26_minesweeper`.

#### Week 2: Board data model (pure C#)
**Goal:** game logic that doesn't depend on graphics or platform.
- [ ] Write the `Cell` class (fields: `IsMine`, `AdjacentMines`, `IsRevealed`, `IsFlagged`)
- [ ] Write the `Board` class: constructor (width, height, mines), `Cell[,]` grid, `InBounds(x, y)`
- [ ] Implement `PlaceMines(safeX, safeY)`, which places mines randomly and skips the 3×3 area around the first tap
- [ ] Implement `CalculateNumbers()`, which counts mines in the 8 neighbours of each cell
- [ ] Add a temporary `BoardDebug` MonoBehaviour that builds a board and prints it to the Console with `Debug.Log`

**Done when:** pressing Play prints a correct grid of mines (`*`) and numbers. ✅ **Milestone M1**

#### Week 3: Graphics and the visible grid
**Goal:** see the board on a portrait screen.
- [ ] Make or download sprites: hidden tile, revealed tile, numbers 1–8, **yellow flag** (player marker), **rectangular red flag** (the hazard), hit red flag (dark tile, bright border), crossed-out yellow flag. Use **at least 128×128 px**, because phone screens are high-resolution and small sprites look blurry. Kenney.nl has free tile packs
- [ ] Import the sprites (Pixels Per Unit = sprite size; Compression: None or High Quality; mipmaps off)
- [ ] Create the `Tile` prefab: `SpriteRenderer` + `BoxCollider2D` + `TileView` script
- [ ] Write `BoardView`, which spawns one tile per cell at position (x, y) and keeps a `TileView[,]` array
- [ ] Write `CameraController.Fit()`, which centres the camera on the board and zooms so the grid fits a **portrait** screen, leaving space for the top bar (counter, face, timer) and the bottom bar (flag-mode button)

**Done when:** a 9×9 grid of hidden tiles appears in the Device Simulator on a typical phone.

#### Week 4: Touch input: tap and long press
**Goal:** the player can interact with the board using a finger.
- [ ] Write `InputHandler` using the Input System's `Pointer.current`. It works with touch on the phone **and** with the mouse in the editor, so you can test quickly
- [ ] Convert the touch position to world coordinates with `Camera.ScreenToWorldPoint` and round to a grid (x, y)
- [ ] **Tap** (finger lifted quickly without moving) → `Board.Reveal(x, y)` → `BoardView` refreshes the tile
- [ ] **Long press** (finger held still for about 0.4 s) → `Board.ToggleFlag(x, y)`. A flagged cell can't be revealed
- [ ] `TileView.Refresh(Cell)` chooses the correct sprite from the cell's state
- [ ] Test on the phone and tune the long-press time until it feels right

**Done when:** you can open cells and place flags with your finger on the phone. ✅ **Milestone M2**

### Month 2: A playable game

#### Week 5: Flood fill and the safe first tap
**Goal:** the core Minesweeper "feel".
- [ ] Implement flood fill in `Board.Reveal` with a `Queue<Vector2Int>` (BFS). When a revealed cell has 0 adjacent mines, add its hidden, unflagged neighbours to the queue
- [ ] Return the list of changed cells so the view only redraws those
- [ ] Generate mines lazily on the **first** tap, so the first tap is always safe and usually opens an area
- [ ] Test on a 16×30 board **on the phone** to make sure it's fast

**Done when:** tapping an empty area opens a whole region the way classic Minesweeper does.

#### Week 6: Game states, win and loss
**Goal:** the game has a beginning and an end.
- [ ] Create a `GameState` enum: `Ready`, `Playing`, `Won`, `Lost`
- [ ] Write the `GameManager` MonoBehaviour, which owns the `Board`, the state and the rules
- [ ] Loss: when a red flag is revealed, show all red flags, highlight the tapped one, mark wrongly placed yellow flags, block input
- [ ] Win: when `revealedCount == width × height − mines`, auto-flag all mines and block input
- [ ] Add `Restart()`, which destroys the old tiles and builds a new board. Use a temporary on-screen button until the real UI exists

**Done when:** you can play a full round, win or lose, and restart.

#### Week 7: Mobile user interface
**Goal:** a touch-friendly top bar and bottom bar.
- [ ] Add a Canvas with a **Canvas Scaler**: "Scale With Screen Size", reference resolution 1080×1920, Match = 0.5. This keeps the UI the same relative size on every phone
- [ ] Add a `SafeArea` script on the UI root panel so nothing is hidden behind the notch, camera hole or rounded corners
- [ ] Top bar with TextMeshPro text: mine counter (mines − flags, can go negative) and timer (starts on the first tap, stops at the end, capped at 999)
- [ ] Restart button with a face that changes: 🙂 normal / 😮 while a finger is held on the board / 😎 win / 😵 loss
- [ ] Bottom bar with a big **flag-mode toggle** (⛏ reveal / 🚩 flag). All buttons at least ~48 dp (about 130 px at the reference resolution) so they're easy to hit
- [ ] Win and lose message panels. The lose panel shows one random line from `Assets/Text/loss_lines.txt` (one line per entry) under the "lost" message, using the `LossLines` helper
- [ ] Use C# events (`event Action`) from `GameManager` so the UI doesn't poll every frame
- [ ] Make sure touches on buttons don't also tap the board

**Done when:** the MVP is fully playable **on the phone**. ✅ **Milestone M3.** Tag it in Git: `git tag v0.1-mvp`

#### Week 8: Difficulty levels, main menu, Back button
**Goal:** replayability and normal Android navigation.
- [ ] Create a `DifficultySettings` ScriptableObject (name, width, height, mines) and three assets: Beginner 9×9/10, Intermediate 16×16/40, Expert **16×30**/99
- [ ] Add a `MainMenu` scene with three big difficulty buttons and make it **scene 0** in Build Profiles, so the game always starts from the difficulty choice. There's **no Quit button**: on Android, players leave with the Back or Home button
- [ ] Pass the chosen difficulty to the game scene with a static `GameSession` class, then load it with `SceneManager.LoadScene`
- [ ] **Android Back button:** in the game it returns to the menu; in the menu it closes the app. In the Input System the Back button arrives as the **Escape** key
- [ ] Test the layout in the Device Simulator on several shapes: a 16:9 phone, a tall 20:9 phone with a notch, and a 4:3 tablet

**Done when:** you can choose any level from the menu, and Back behaves as expected on the phone.

### Month 3: Polish, testing, release

#### Week 9: Zoom, pan and advanced gameplay
**Goal:** big boards are comfortable to play on a small screen.
- [ ] **Pinch to zoom** in `CameraController`: two fingers change `orthographicSize`, limited between a minimum and the "whole board visible" size
- [ ] **Drag to pan** with one finger. The camera can't leave the board, and a drag never counts as a tap
- [ ] Chording: tapping a revealed number whose adjacent flags equal its number reveals all its other neighbours
- [ ] Best time per difficulty: save it with `PlayerPrefs.SetFloat("best_" + name, time)` and show it in the menu, plus a "New record!" message
- [ ] Pause the timer while the app is in the background (`OnApplicationPause`)

**Done when:** Expert is playable on a phone with zoom and pan, and best times survive closing the app.

#### Week 10: Audio, vibration and polish
**Goal:** make it feel good in the hand.
- [ ] Add an `AudioManager` with clips for tap, flag, flood reveal, explosion and win (free sounds from Kenney.nl or freesound.org; check the licence)
- [ ] Add a settings panel with **Sound** and **Vibration** on/off, saved in `PlayerPrefs`
- [ ] Vibration (`Handheld.Vibrate()`) when a flag is placed by long press and when you lose
- [ ] Pressed highlight: the tile under the finger looks pressed while held (this replaces the mouse "hover" effect, which doesn't exist on touch screens)
- [ ] Optional small effects: tile pop on reveal, camera shake on explosion

**Done when:** the game has sound, vibration and visual feedback. ✅ **Milestone M4**

#### Week 11: Automated tests and bug fixing
**Goal:** confidence that the logic is correct.
- [ ] The **Unity Test Framework** is already installed in GC26: create an assembly definition for `Scripts/Core` and an Edit Mode test assembly that references it
- [ ] Write tests for `Board`: the mine count is correct, the first-tap area has no mines, the numbers are correct on a hand-made board, flood fill stops at numbers, a flagged cell can't be revealed, the win is detected, chording works
- [ ] Go through the manual test checklist in `03_Progress_Tracker.md` **on the real phone**
- [ ] Fix the bugs you find (keep a bug list in the tracker)

**Done when:** all tests pass and there are no known critical bugs.

#### Week 12: Android build and settings
**Goal:** an app that installs and runs like a real mobile game.
- [ ] Player Settings: company name, product name, **package name** (e.g. `com.yourname.minesweeper`), version, **adaptive icon**, splash screen, portrait only
- [ ] Other Settings: Scripting Backend **IL2CPP**, Target Architectures **ARM64** (both required by Google Play), Minimum API Level as suggested by Unity 6, Target API Level = latest installed
- [ ] Set `Application.targetFrameRate = 60` so animations are smooth; remove debug scripts and `Debug.Log` spam
- [ ] Create a **keystore** (Publishing Settings) and make a signed release build: **APK** for sharing and testing, **AAB** if you want Google Play. Back up the keystore and passwords safely; you can't update a Play app without them
- [ ] Test on 2 or more devices if possible: notch, Back button, phone call or lock screen mid-game, returning to the app

**Done when:** a signed APK installs and runs correctly on a phone that isn't connected to Unity.

#### Week 13: Buffer, release and review
**Goal:** finish cleanly. This week also covers tasks that slipped earlier.
- [ ] Finish anything still open from weeks 1–12
- [ ] Write the `README.md` in the repository: description, controls, screenshots, how to build
- [ ] Optional: upload the AAB to **Google Play internal testing** (needs a one-time Google Play developer account fee), or share the APK on itch.io
- [ ] Tag the release: `git tag v1.0`
- [ ] Write a retrospective in the tracker: what went well, what was hard, what you would do next

**Done when:** v1.0 is released. ✅ **Milestone M5**

---

## 5. Risks and how to handle them

| Risk | Sign | What to do |
|---|---|---|
| Android build setup fails | Errors about the SDK, NDK or JDK | It's done in week 1 on purpose. Reinstall the Android module from Unity Hub; let Unity use its own bundled SDK/JDK instead of other installs |
| Phone not detected | Build And Run can't find a device | Check USB debugging, accept the "Allow USB debugging" prompt on the phone, try another cable, set the USB mode to "File transfer" |
| No phone available | — | Use the Device Simulator for layout, and the Android Emulator (Android Studio) for running builds |
| Cells too small to tap | Wrong cells get opened on Expert | Pinch zoom (week 9); if zoom is cut, drop Expert or reduce it to 16×24 |
| Taps and drags confused | Panning opens cells | Use a movement threshold (~20 px) before a touch counts as a drag; tune it on the phone |
| Different screen shapes | UI hidden by the notch or overlapping the board | Canvas Scaler + SafeArea; test several devices in the Device Simulator |
| Behind schedule | A milestone is late by more than 1 week | Drop Nice-to-have items first, then Should-have items; week 13 is the buffer |
| Stuck on a bug for more than 30 minutes | No progress in a session | Write it in the tracker, move to the next task, search or ask for help later |
| Lost work or keystore | Broken project, lost signing key | Commit and push every session; keep the keystore **outside** the Git repository with a backup copy |

---

## 6. Learning resources
- Unity Learn: learn.unity.com ("Unity Essentials", "Junior Programmer")
- Unity Manual: "Android" section (setup, building, Player settings) and "Device Simulator"
- Input System package docs: "Touch support" and "Pointer"
- Microsoft C# docs: learn.microsoft.com/dotnet/csharp
- Android design guidelines (touch target sizes): developer.android.com/design
- Free 2D art and sounds: kenney.nl
- Gitignore template: github.com/github/gitignore/blob/main/Unity.gitignore

See `02_Technical_Design.md` for the architecture and code skeletons, `04_Game_Design_Document.md` for the rules and screens, `05_Task_List.md` for the task list, and `03_Progress_Tracker.md` for the weekly log and test checklist.
