# GC26 – Tower Defence (Last Bastion)

A mobile tower defense game made with Unity and C#. This is the `gc26_towerdefence` branch of the GC26 repository.

- Task board: https://github.com/users/Zaidejas/projects/13
- Plan, game design and a playable browser prototype: [`Docs/`](Docs/)

## Open the project on another computer

### 1. Install the tools (once per computer)
- **Unity Hub**, then **Unity 6000.5.6f1** with **Android Build Support** (add **iOS Build Support** on a Mac if you build for iPhone). If the version is missing, Unity Hub offers to install it when you add the project.
- **Git** and **Git LFS**. Run this once after installing Git LFS, so images, sounds and models download correctly:
  ```
  git lfs install
  ```

### 2. Download the project
Put it in a short folder path, for example `C:\Unity`. Unity's package files are nested deeply, and Windows can fail to find them if the full path gets longer than 260 characters.
```
cd C:\Unity
git clone --branch gc26_towerdefence https://github.com/Zaidejas/GC26.git
```
If you already have the repository, switch to the branch instead:
```
git fetch origin
git switch gc26_towerdefence
```

### 3. Open it in Unity
1. In Unity Hub, click **Add** → **Add project from disk** and choose the `GC26` folder (the one with `Assets`, `Packages` and `ProjectSettings` in it).
2. Open it with **6000.5.6f1**. The first time takes a few minutes, because Unity rebuilds the `Library` folder and downloads the packages.
3. Open the scene `Assets/Scenes/SampleScene.unity`.

### Good to know
- `Library`, `Temp`, `Logs`, `UserSettings` and the `.csproj`/`.sln`/`.slnx` files are not in Git. Unity creates them on each computer, so don't copy them between computers.
- If Unity asks to upgrade the project to a different version, check with the team first. Opening it with another version changes files in `ProjectSettings` and `Packages` for everyone.
- Pull before you start working and push when you finish, so the team stays in sync.

## Browser prototype
The prototype in [`Docs/prototype/`](Docs/prototype/) needs no install: double-click `Docs/prototype/index.html` to play it in a browser.

To try it on a phone, install Python 3 and run this, then open the address it prints on a phone on the same Wi-Fi:
```
python Docs/prototype/serve.py --lan
```
See [`Docs/prototype/README.md`](Docs/prototype/README.md) for the controls and what's in it.

## Docs
| File | What it is |
|---|---|
| [`Docs/TowerDefense_Plan_EN_RU.md`](Docs/TowerDefense_Plan_EN_RU.md) | 2-month development plan (English and Russian) |
| [`Docs/TowerDefense_GDD_EN_RU.md`](Docs/TowerDefense_GDD_EN_RU.md) | Game design document (English and Russian) |
| [`Docs/TowerDefense_Tasks.json`](Docs/TowerDefense_Tasks.json) | The 57 tasks on the project board |
