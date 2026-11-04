# Lines — project rules for Claude

A study project: a phone remake of the classic Lines puzzle (move balls on a 9×9 board, make lines of 5+). Built in Unity 6 with C#, for iOS first and Android second. It is installed directly on our own phones and never published to a store.

## Read first

- `docs/Lines_Game_Design_Document.md`: the rules, controls, screens, and what is MVP vs later. **The GDD is the source of truth for game behavior.**
- `docs/Lines_Plan_EN_RU.md`: the 8-week plan and architecture (English and Russian versions; keep both in sync when editing).
- Tasks: the [gc26_lines project board](https://github.com/users/Zaidejas/projects/14). Tasks 01–42 are in work order; L01–L13 are later features. Each task says what to do, when it is done, and which doc section it comes from.

## Working with the user

- The user is new to Unity. They use the editor only to look at the result. Explain in plain words, and say clearly when a step needs them (passwords, their phone, their accounts, judging the look).
- Work one board task at a time. When asked to start a task: set its Status to **In progress**, do it, check it, commit, then set it to **Done**.
- If the GDD doesn't answer a question about game behavior, ask the user, then write the answer into the GDD.

## Code-first: no hand-built scenes

- The project has one scene, `Assets/Scenes/Main.unity`, containing only a camera and `GameBootstrap`. Everything else (board, balls, UI) is created from code at runtime.
- Never hand-edit `.unity`, `.prefab` or `.asset` files as text. If a scene or setting must change, change `Assets/Scripts/Editor/ProjectSetup.cs` and run it.
- No third-party packages from the Asset Store. Animations use our own small helper (no DOTween).
- Ball and board images are generated in code (MVP).

## Code layout

```
Assets/Scripts/Core/      Lines.Core   pure C# game rules, no UnityEngine (unit-tested)
Assets/Scripts/Game/      Lines        GameBootstrap, GameController, input
Assets/Scripts/View/      Lines        board/ball views, sprite generation, animation helper
Assets/Scripts/UI/        Lines        screens (menu, game HUD, how to play, leaderboard), pop-ups (uGUI + TextMeshPro, built from code)
Assets/Scripts/Services/  Lines        saving (game in progress, leaderboard)
Assets/Scripts/Editor/    Lines.Editor project setup and build scripts
Assets/Tests/EditMode/    Lines.Tests.EditMode
```

- Namespaces follow the folders: `Lines.Core`, `Lines.Game`, `Lines.View`, `Lines.UI`, `Lines.Services`, `Lines.EditorTools`, `Lines.Tests`.
- Core raises C# events (`OnBallMoved`, `OnMoveBlocked`, `OnLinesCleared`, `OnBallsSpawned`, `OnGameOver`); the view layer listens. Game rules never live in views.
- Every Core rule gets unit tests. Use a seeded random generator so tests are repeatable.

## Running Unity from the command line

Unity must be **closed** while these run: an open editor locks the project. Check first: if `Temp/UnityLockfile` exists or `pgrep -f "Unity.app/Contents/MacOS/Unity"` finds a process, ask the user to close Unity.

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity

# EditMode tests (do not pass -quit with -runTests)
$UNITY -batchmode -nographics -projectPath . -runTests -testPlatform EditMode \
  -testResults <scratchpad>/results.xml -logFile <scratchpad>/tests.log

# Rebuild the Main scene and project settings
$UNITY -batchmode -nographics -projectPath . -executeMethod Lines.EditorTools.ProjectSetup.Run \
  -quit -logFile <scratchpad>/setup.log
```

- Unity version is pinned to **6000.5.6f1** (`ProjectSettings/ProjectVersion.txt`). Don't upgrade it.
- Check results in the XML (`result="Passed"`) and look for `error CS` in the log; the exit code alone isn't enough.
- To import a `.unitypackage`, use `-importPackage <path> -quit`. `AssetDatabase.ImportPackage` does not finish before a batch-mode run quits.
- Build scripts (iOS Simulator, iPhone, Android APK) arrive in task 16; document their commands here when they exist.

## How each task is checked

1. Unit tests pass (for anything in Core).
2. From task 17 on: the game builds and runs in the iOS Simulator (`mcp__Claude_Code_iOS_Simulator__control`), and a screenshot is shown to the user for anything visible.
3. The task's "Done when" on the board is met.

## Git and the board

- Commit to the `gc26_lines` branch only. Don't push unless the user asks.
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
