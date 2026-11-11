# Стальной рубеж (gc26_tanki): notes for Claude

A portrait (9:16) phone tank game: hold off waves of enemy tanks and protect the reactor.
It is one of the GC26 microgames: a shared Unity 6 project (6000.5.6f1, **URP**, **new Input System only**)
with a menu shell. Read `RULES.md` at the repo root before changing anything.

- **Language:** the owner writes in Russian, informally. Answer in Russian.
- **Originality:** the game is original, with its own maps and a reactor instead of an eagle. Don't turn it into a copy of an existing game.
- **Branch:** `gc26_tanki`. The owner wants each change committed and pushed there.
- **Only this folder:** change only `Assets/Microgames/gc26_tanki/`. Never touch `ProjectSettings`, `Packages`, `Assets/Core`, `Assets/Shared` or `tools/`.
- **APK:** don't build one unless asked.

## Layout (inside `Assets/Microgames/gc26_tanki/`)

- `Scripts/Rules/`: game rules in pure C# with no Unity references (`GC26.Tanki.Rules`, own asmdef)
  - `GameSim.cs`: rules and AI
  - `GameTypes.cs`: types and the `Tuning` numbers
  - `Levels.cs`: 20 levels plus the survival arena; `X` in a wave is the boss
- `Scripts/Game/` (`GC26.Tanki.Game`, asmdef `GC26.Tanki` at the folder root):
  - `TankiGame.cs`: the `IMicrogame` the shell calls; creates `GameRunner` in `Begin`, or in `Start` when the scene runs alone
  - `GameRunner.cs`: pages and flow; `Quit()` calls the shell's exit. Restores frame rate, sleep and orientation in `OnDestroy`
  - `GameHud.cs`: IMGUI HUD and all menus, with panel and button textures made in code
  - `WorldView*.cs`: models and effects; `CameraRig.cs` makes its own camera (depth 10) and sun
  - `Controls.cs`: touch, mouse and keyboard through the new Input System
  - `SaveData.cs`: save, as JSON in PlayerPrefs key `gc26_tanki_save`
  - `AutoCheck.cs`: an automatic play-through with screenshots, for development builds only
- `Scripts/Editor/TankiAssetImport.cs`: import settings, only for this folder
- `Resources/gc26_tanki/`: everything loaded with `Resources.Load("gc26_tanki/...")`; materials use URP Lit (`_BaseMap`, `_BaseColor`, `_Smoothness`, `_EmissionColor`)
- `Tests~/`: rule checks without Unity
- `Web~/steel-line.html`: browser version on three.js r128 with the same rules (save in localStorage `steel-line-save`)
- `Art~/`: Python scripts (Pillow and numpy) that make the game's art; `build_variants.py` draws every other tank from the two hand-made drawings (`player.png`, `enemy.png`), then `build_models.py` raises them into models
- `Tools~/`: `assault_maps.txt` and `sync_levels.py`, which writes the assault maps into `Levels.cs` and copies every map into the page

## Keep in sync

- The rules exist twice: in C# (`Scripts/Rules/`) and in JS (inside `Web~/steel-line.html`). When you change a rule, a number or a level, change both.
- Maps: edit `Levels.cs` (campaign, arena) or `Tools~/assault_maps.txt` (assault), then run `python Tools~/sync_levels.py`.
- Awards are listed twice too: `Scripts/Game/TankiAwards.cs` and `AWARDS` in the page.

## Modes and content

- Campaign (20 levels, bosses on 5/10/15/20), survival, and assault (6 fortresses: break the enemy reactor `C` past turrets `T`; defenders loop).
- Enemies R S G H X plus miner M, fixer F, rammer K, each with its own model.
- Garage tanks (`PlayerTank`, `TankDef`): Guard strike, Squall volley, Bastion dome, Phantom dash and cloak.
- Stars per map (win, no tank lost, par time) and 15 awards paid in parts.

## Checks

- **Rules:** `dotnet run --project "Assets/Microgames/gc26_tanki/Tests~"` must print ALL PASSED.
- **Names:** `python tools/check_names.py gc26_tanki`. Until the organizer adds `gc26_tanki` to `tools/games.txt` it stops at "not a known id"; to check the rest, add the line locally and revert it afterwards.
- **Unity:** Unity fails on long paths, so copy the repo to a short folder (for example `C:\sl_gc26`, leaving out `.git Library Temp Logs`) and build there with a temporary editor script that builds only `gc26_tanki_Main` as a development Windows player. Run the player with `-screen-width 540 -screen-height 1080 -screen-fullscreen 0 -autocheck <folder>` for screenshots, then delete its test save (registry `HKCU\Software\DefaultCompany`, the GC26 product).
- **Browser version:** no errors in the console. `window.__steelLine` exposes `sim`, `save`, `startLevel`, `startSurvival`, `showLevels`, `showWorkshop` and `showSettings`.
