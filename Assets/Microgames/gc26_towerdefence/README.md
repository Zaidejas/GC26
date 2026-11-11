# gc26_towerdefence — Last Bastion

- Ветка: `game/gc26_towerdefence`
- Неймспейс: `GC26.Towerdefence`
- Стартовая сцена: `Scenes/gc26_towerdefence_Main.unity` (создать в Unity, повесить `TowerdefenceGame`)
- Команда:
- Доска задач: https://github.com/users/Zaidejas/projects/13

Как играть: строй башни на свободных местах у дороги и не дай врагам дойти до замка. 10 волн, 20 жизней.

A mobile tower defence game, rebuilt in Unity/C# from the browser prototype in [`Docs~/towerdefence_prototype/`](Docs~/towerdefence_prototype/).
It is one of the GC26 microgames: it runs from the shared GC26 menu, so follow [`RULES.md`](../../../RULES.md)
(work only in this folder, everything in `namespace GC26.Towerdefence`, no generic names like `GameManager`).

## Open the project on another computer

### 1. Install the tools (once per computer)
- **Unity Hub**, then **Unity 6000.5.6f1** with **Android Build Support**. If the version is missing, Unity Hub offers to install it when you add the project.
- **Git**, **Git LFS** and **Python 3**. Run this once after installing Git LFS, so images, sounds and models download correctly:
  ```
  git lfs install
  ```

### 2. Download the project
Put it in a short folder path, for example `C:\Unity`. Unity's package files are nested deeply, and Windows can fail to find them if the full path gets longer than 260 characters.
```
cd C:\Unity
git clone --branch game/gc26_towerdefence https://github.com/Zaidejas/GC26.git
cd GC26
git config core.hooksPath tools/hooks
```
If you already have the repository, switch to the branch instead:
```
git fetch origin
git switch game/gc26_towerdefence
git config core.hooksPath tools/hooks
```
The last line turns on the check that runs before every `git push` (see `tools/README.md`).

### 3. Open it in Unity
1. In Unity Hub, click **Add** → **Add project from disk** and choose the `GC26` folder (the one with `Assets`, `Packages` and `ProjectSettings` in it).
2. Open it with **6000.5.6f1**. The first time takes a few minutes, because Unity rebuilds the `Library` folder and downloads the packages.
3. Open the scene `Assets/Microgames/gc26_towerdefence/Scenes/gc26_towerdefence_Main.unity`.

### Good to know
- Change files only inside `Assets/Microgames/gc26_towerdefence/`. Packages, ProjectSettings, `Assets/Core` and `Assets/Shared` are changed only by the organizer through `main`.
- Once a week, bring in `main`: `git fetch && git merge origin/main`.
- If Unity asks to upgrade the project to a different version, check with the team first.
- Pull before you start working and push when you finish, so the team stays in sync.

## Browser prototype
The prototype in [`Docs~/towerdefence_prototype/`](Docs~/towerdefence_prototype/) needs no install: double-click `index.html` to play it in a browser.
To try it on a phone, run this from the repository root, then open the address it prints on a phone on the same Wi-Fi:
```
python "Assets/Microgames/gc26_towerdefence/Docs~/towerdefence_prototype/serve.py" --lan
```
See [`Docs~/towerdefence_prototype/README.md`](Docs~/towerdefence_prototype/README.md) for the controls and what's in it.
The folder name ends with `~`, so Unity ignores it.

## Docs
| File | What it is |
|---|---|
| [`Docs~/TowerDefense_Plan_EN_RU.md`](Docs~/TowerDefense_Plan_EN_RU.md) | Development plan (English and Russian) |
| [`Docs~/TowerDefense_GDD_EN_RU.md`](Docs~/TowerDefense_GDD_EN_RU.md) | Game design document (English and Russian) |
| [`Docs~/TowerDefense_Tasks.json`](Docs~/TowerDefense_Tasks.json) | The original 57 board tasks (the board has since been updated for the Unity remake) |
