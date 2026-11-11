# Lines — project rules for Claude

A study project: a phone remake of the classic Lines puzzle (move balls on a 9×9 board, make lines of 5+). Built in Unity 6 with C#, for iOS first and Android second. It is installed directly on our own phones and never published to a store.

Lines is also one of the 11 GC26 microgames in one shared Unity project (team rules: `RULES.md` and `GETTING_STARTED.md` in the repository root, from `main`). Everything of ours lives in **`Assets/Microgames/gc26_lines/`** (called "the game folder" below; paths in this file are relative to it unless they start at the project root). For now Lines is built alone as a phone app; later all games are merged into one app with a shared menu.

This file lives in the game folder because our branch may only change that folder. Each machine keeps a local, uncommitted `CLAUDE.md` in the project root containing the single line `@Assets/Microgames/gc26_lines/CLAUDE.md`, so Claude reads this file when it starts in the project root (it is listed in `.git/info/exclude`).

## Read first

- `Documentation~/Lines_Game_Design_Document.md`: the rules, controls, screens, and what is MVP vs later. **The GDD is the source of truth for game behavior.**
- `Documentation~/prototype/lines-prototype.html`: a playable browser prototype (open it in a browser). **The Unity game should look and play the same, as closely as possible**: rules, animation timings, layout, screens and menu flow. When the GDD and the prototype disagree, ask the user. Its JavaScript rule functions (`findPath`, `linesThrough`, `spawn`, `clearLines`, `addScore`) are a useful cross-check for the C# Core. It is also published at https://claude.ai/artifact/6fQXEJVoA8iEaHxpHKtgFL (republish after changing it).
- `Documentation~/Lines_Plan_EN_RU.md`: the 8-week plan and architecture (English and Russian versions; keep both in sync when editing).
- Folders whose names end in `~` (`Documentation~`, `Tools~`) are skipped by Unity, so they don't clutter the project.
- Tasks: the [gc26_lines project board](https://github.com/users/Zaidejas/projects/14). Tasks 01–41 are in work order (Part 1, tasks 16–25: every prototype feature in Unity; Part 2, tasks 26–41: simulators and phones; 32a moved the game into the team layout); L01–L13 are later features. Each task says what to do, when it is done, and which doc section it comes from.

## Working with the user

- The user is new to Unity. They use the editor only to look at the result. Explain in plain words, and say clearly when a step needs them (passwords, their phone, their accounts, judging the look).
- Work one board task at a time. When asked to start a task: set its Status to **In progress**, do it, check it, commit, then set it to **Done**.
- If the GDD doesn't answer a question about game behavior, ask the user, then write the answer into the GDD.

## Team rules (RULES.md) in short

- **Only the game folder.** Our branch changes only `Assets/Microgames/gc26_lines/` and `Assets/Microgames/gc26_lines.meta`. Never commit `ProjectSettings/`, `Packages/`, `Assets/Settings/`, `Assets/Core/`, `Assets/Shared/`, Build Settings, `.gitignore`, `GC26.slnx` or other root files: Unity often changes them by itself, so stage only the game folder (`git add Assets/Microgames/gc26_lines Assets/Microgames/gc26_lines.meta`, never `git add .`) and undo stray changes (`git checkout -- GC26.slnx`). Anything project-wide (a package, a setting, a layer) is a request to the organiser.
- **Names.** Namespaces `GC26.Lines.*`; code packages `GC26.Lines` (the game folder root) and `GC26.Lines.<Something>`; the scene name starts with `gc26_lines_`; Resources only under `Resources/gc26_lines/` and loaded as `Resources.Load("gc26_lines/...")`; no generic file names such as `GameController`, `Game`, `Menu`, `Settings`, `Player` (prefix them with `Lines`).
- **Saves** only in `MicrogameSave.Folder(LinesMicrogame.Id)` (persistentDataPath/gc26_lines); PlayerPrefs keys start with `gc26_lines_`.
- **No global state:** no `DontDestroyOnLoad`, no session-long static singletons, `Time.timeScale` and `Physics.gravity` put back in `OnDisable`. Everything the game makes lives in its scene.
- **Checks** before pushing (both must show no ERROR; our branch is still named `gc26_lines`, so pass the team name explicitly):

```bash
python3 tools/check_scope.py --branch game/gc26_lines
python3 tools/check_names.py gc26_lines
```

- **Merge `main` into our branch** about once a week: `git fetch && git merge origin/main`.
- **The shared menu** (later): it loads `gc26_lines_Main`, finds the one `IMicrogame` component (`LinesMicrogame`) and calls `Begin(exit)`. The game starts by itself without the menu, so phone builds work alone. Still to do when the games are merged: a "Back to games" button that calls `LinesMicrogame.ExitToMenu(score)`, a pause, and the team's branch name `game/gc26_lines`.

## Code-first: no hand-built scenes

- The game has one scene, `Scenes/gc26_lines_Main.unity`, containing only a camera and the `GameBootstrap` object (with `GameBootstrap` and `LinesMicrogame`). Everything else (board, balls, UI) is created from code at runtime.
- Never hand-edit `.unity`, `.prefab` or `.asset` files as text. If the scene must change, change `Scripts/Editor/ProjectSetup.cs` and run it. ProjectSetup changes nothing outside the game folder.
- Project-wide phone-app settings (name "Lines", app id `com.gc26.lines`, portrait only, icon, splash) are **not** committed: `Scripts/Editor/PhoneAppSettings.cs` applies them during a build, and `Tools~/build.sh` puts `ProjectSettings`, `Assets/Settings`, `Packages/packages-lock.json` and `GC26.slnx` back exactly as they were afterwards.
- No third-party packages from the Asset Store. Animations use our own small helper (no DOTween).
- MVP art: the dark magic-forest look from the prototype (GDD → Art), exported from it as PNGs: 7 glowing mushrooms (`Resources/gc26_lines/Mushrooms/mushroom_0…6.png`, the same in every game; `PieceSet.Mushrooms()`, with generated glossy balls as a fallback), two forest backgrounds (`Resources/gc26_lines/Backgrounds/`; `ForestBackground`: the menu's has tall mushrooms), the Mystery Quest font (`Resources/gc26_lines/Fonts/`) and the app icon and splash logo (`Art/AppIcon/`). Colours live in `Palette`.

## Code layout (inside the game folder)

```
GC26.Lines.asmdef          GC26.Lines            code package for everything below that has no own asmdef
Scripts/Core/              GC26.Lines.Core       pure C# game rules, no UnityEngine (unit-tested)
Scripts/Game/              GC26.Lines            GameBootstrap, LinesGameController, input, LinesMicrogame (link to the shared menu)
Scripts/View/              GC26.Lines            board/ball views, sprite generation, animation helper
Scripts/UI/                GC26.Lines            screens (menu, game HUD, how to play, leaderboard), pop-ups (uGUI + TextMeshPro, built from code)
Scripts/Services/          GC26.Lines            saving (game in progress, leaderboard)
Scripts/Editor/            GC26.Lines.Editor     project setup, phone-app settings, build scripts, layout screenshots
Tests/EditMode/            GC26.Lines.Tests.EditMode   (Core rules, Tween driven by Tween.Tick)
Tests/PlayMode/            GC26.Lines.Tests.PlayMode   (things that need real frames)
Resources/gc26_lines/      mushrooms, backgrounds, fonts
Art/AppIcon/               app icon and splash logo (used only by builds)
Scenes/                    gc26_lines_Main.unity
Documentation~/  Tools~/   docs and the build script (skipped by Unity)
microgame.json  README.md  the game's card for the shared menu, and the team README
```

- Namespaces follow the folders: `GC26.Lines.Core`, `GC26.Lines.Game`, `GC26.Lines.View`, `GC26.Lines.UI`, `GC26.Lines.Services`, `GC26.Lines.EditorTools`, `GC26.Lines.Tests`.
- Core raises C# events (`OnBallMoved`, `OnMoveBlocked`, `OnLinesCleared`, `OnBallsSpawned`, `OnGameOver`); the view layer listens. Game rules never live in views.
- Every Core rule gets unit tests. Use a seeded random generator so tests are repeatable.
- Play-mode test classes inherit `PlayModeTestBase`, which points saving at a temporary folder, so tests start without a saved game and never touch the real one. They start the game with `LoadGameScene()`, which loads the scene by its path (it isn't in the shared Build Settings).
- Touch input is read as events (`EnhancedTouch` `Touch.onFingerDown`), not checked once per frame: a quick tap can go down and up inside one frame and would be missed.

## Running Unity from the command line

Run these from the project root. Unity must be **closed** while they run: an open editor locks the project. Check first: if `Temp/UnityLockfile` exists or `pgrep -f "Unity.app/Contents/MacOS/Unity"` finds a process, ask the user to close Unity.

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity

# EditMode tests (do not pass -quit with -runTests)
$UNITY -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testResults <scratchpad>/results.xml -logFile <scratchpad>/tests.log

# PlayMode tests (real frame loop, e.g. animations); no -nographics, no -quit
$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode \
  -testResults <scratchpad>/results-play.xml -logFile <scratchpad>/tests-play.log

# Layout screenshots at 16:9, 20:9 (with notch) and real iPhone sizes, without building; no -nographics
$UNITY -batchmode -projectPath . -executeMethod GC26.Lines.EditorTools.LayoutScreenshots.Run \
  -quit -shotsDir <scratchpad>/shots -logFile <scratchpad>/shots.log

# Rebuild the game's scene
$UNITY -batchmode -nographics -projectPath . -executeMethod GC26.Lines.EditorTools.ProjectSetup.Run \
  -quit -logFile <scratchpad>/setup.log
```

- Unity version is pinned to **6000.5.6f1** (`ProjectSettings/ProjectVersion.txt`). Don't upgrade it.
- Check results in the XML (`result="Passed"`) and look for `error CS` in the log; the exit code alone isn't enough.
- Unity rewrites `GC26.slnx` and sometimes shared settings during these runs; undo those changes afterwards (`git checkout -- GC26.slnx`, or the file in question) instead of committing them.
- To import a `.unitypackage`, use `-importPackage <path> -quit`. `AssetDatabase.ImportPackage` does not finish before a batch-mode run quits.

Builds (Unity closed; output in `Builds/` at the project root, which is excluded locally; logs in `Builds/logs/`):

```bash
B="Assets/Microgames/gc26_lines/Tools~/build.sh"
"$B" ios-sim      # Unity → Xcode → installs and starts the game in the iOS Simulator (~2 min)
"$B" ios-device   # Xcode project for a real iPhone: Builds/iOS-Device/Unity-iPhone.xcodeproj
"$B" android      # Builds/Android/Lines.apk; installs and starts it on every running emulator (~3 min)
```

- `LINES_SIM=<name or UDID>` picks the simulated iPhone (default: the running one, else "iPhone 18 Pro"); `LINES_APPLE_TEAM=<team id>` signs the iPhone build with the user's free Apple account.
- This Xcode has no separate Simulator app window; watch the game with `mcp__Claude_Code_iOS_Simulator__control` (attach, screenshot).
- The app id is `com.gc26.lines` (set in `PhoneAppSettings`, only during builds). Builds switch Unity back to the Mac platform at the end.
- Saves on a phone are in the app's data folder under `gc26_lines/` (`game.json`, `leaderboard.json`). In the iOS Simulator: `$(xcrun simctl get_app_container booted com.gc26.lines data)/Documents/gc26_lines/`.
- Android tools come with Unity's Android module (installed through Unity Hub). The Android SDK, with the emulator and an Android 16 arm64 phone image, lives in `/Applications/Unity/Hub/Editor/6000.5.6f1/PlaybackEngines/AndroidPlayer/SDK`, and its Java in `…/AndroidPlayer/OpenJDK`. There are two emulator phones, `Lines_16x9` (1080×1920) and `Lines_20x9` (1080×2400):

```bash
SDK=/Applications/Unity/Hub/Editor/6000.5.6f1/PlaybackEngines/AndroidPlayer/SDK
$SDK/emulator/emulator -avd Lines_16x9 -no-snapshot-save -no-audio &   # opens a phone window
$SDK/platform-tools/adb install -r Builds/Android/Lines.apk
$SDK/platform-tools/adb shell monkey -p com.gc26.lines 1             # starts the game
$SDK/platform-tools/adb exec-out screencap -p > shot.png
```

- The emulator phones run the game full screen with no notch. Their save files are in `/storage/emulated/0/Android/data/com.gc26.lines/files/gc26_lines/` (`adb shell cat …/game.json`). Android's one-time "Viewing full screen" tip is switched off with `adb shell settings put secure immersive_mode_confirmations confirmed`.

## How each task is checked

1. Unit tests pass (for anything in Core).
2. Part 1 (tasks 16–25): play-mode tests, and layout screenshots (`LayoutScreenshots`) shown to the user for anything visible; the user tries it in Unity's Play mode.
   Part 2 (from task 27): the game builds and runs in the iOS Simulator (`mcp__Claude_Code_iOS_Simulator__control`) or Android Emulator, with screenshots.
3. The team checks (`check_scope.py`, `check_names.py`) show no ERROR.
4. The task's "Done when" on the board is met.

## Git and the board

- Commit to the `gc26_lines` branch only, and only the game folder (see Team rules). Don't push unless the user asks.
- Commit messages start with the task: `Task 05: Pathfinder`. Changes outside a task get a plain descriptive message.
- `.png`, `.ttf` and other binaries go through Git LFS (see `.gitattributes`).
- Board status changes use `gh` (signed in as `natlaur`, with the `project` scope):

```bash
# find a task's item id
gh project item-list 14 --owner Zaidejas --limit 100 --format json \
  --jq '.items[] | select(.title|startswith("05 ·")) | .id'

# set its Status
gh project item-edit --project-id PVT_kwHOFIAi-M4BpIgp --id <item-id> \
  --field-id PVTSSF_lAHOFIAi-M4BpIgpzhnlegE --single-select-option-id <option>
# options: To do f1a96cd1 · In progress 6ff8e781 · Done e4a0383e · Cancelled 69caede6
```
