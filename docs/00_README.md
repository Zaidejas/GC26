# Red Flags (Minesweeper for Mobile in Unity): Project Documents

A 3-month plan (13 weeks × 2 hours = 26 hours) for building a Minesweeper-style game, **"avoid the red flags"**, for **Android phones (portrait, touch controls)** in Unity 6 with C#.

These documents live in the `docs` folder of the `GC26` repository (branch `gc26_minesweeper`). Unity ignores this folder because it's outside `Assets`.

## How to open these files on any computer
- **Get them:** clone the repository and switch to the branch (see "Where things are" below), or download the branch as a ZIP from GitHub (Code → Download ZIP).
- **Documents (`.md`)** are plain text in Markdown format. They open in any text editor (Notepad, VS Code), and they look best in VS Code (press `Ctrl+Shift+V` for the preview) or on GitHub, which shows them formatted.
- **Task list (`.csv`)** opens in Excel, LibreOffice or Google Sheets.
- **Prototype:** open `prototype/index.html` in any modern browser (double-click it). No internet, installation or server is needed. See [prototype/README.md](prototype/README.md).

Keep the folder structure as it is; the documents link to each other with relative paths.

## Contents
| File | Contents |
|---|---|
| [01_Project_Plan.md](01_Project_Plan.md) | Scope, session routine, milestones, week-by-week tasks, risks, resources |
| [02_Technical_Design.md](02_Technical_Design.md) | Architecture, mobile project settings, C# code skeletons (touch input, camera zoom/pan, safe area, loss lines), unit test example |
| [03_Progress_Tracker.md](03_Progress_Tracker.md) | Weekly log, test devices, manual test checklist for the phone, bug list, retrospective |
| [04_Game_Design_Document.md](04_Game_Design_Document.md) | How the game works: theme, rules, touch controls, user flow, screens, feedback, edge cases |
| [05_Task_List.md](05_Task_List.md) | 66 tasks (MS-01 to MS-66) by week, with estimates, priorities, dependencies and "done when" |
| [05_Task_List.csv](05_Task_List.csv) | The same tasks for importing into Trello, GitHub Projects, Jira or Excel |
| [prototype/](prototype/) | Simple playable browser prototype (HTML, CSS, JavaScript; optional Python server) |

## Summary of the timeline
- **Month 1 (weeks 1–4):** setup and first phone build → board logic → visible grid → tap and long press work on the phone
- **Month 2 (weeks 5–8):** flood fill → win/lose → mobile UI (**playable MVP on the phone in week 7**) → difficulties, menu and Back button
- **Month 3 (weeks 9–13):** zoom, pan, chording and best times → audio, vibration and polish → tests → signed Android build → buffer and release

## Where things are
- **Unity project:** the `GC26` project (Unity 6000.5.6f1, URP), branch `gc26_minesweeper` of [Zaidejas/GC26](https://github.com/Zaidejas/GC26). On a new computer, get it with:
  ```
  git clone https://github.com/Zaidejas/GC26.git
  cd GC26
  git checkout gc26_minesweeper
  ```
  Then open the `GC26` folder in Unity Hub (Add → Add project from disk). These documents are in `GC26/docs`.
- **Task board:** [GitHub project gc26_minesweeper](https://github.com/users/Zaidejas/projects/6), with all 66 tasks and Status, Week, Priority and Estimate fields

## What you need
- A Windows PC with Unity 6000.5.6f1 and the Android Build Support module
- An Android phone and a USB cable (or the Android Emulator if you have no phone)
- iOS is optional and needs a Mac
