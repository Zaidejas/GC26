# gc26_timetraveler

- Ветка: `game/gc26_timetraveler`
- Неймспейс: `GC26.Timetraveler`
- Стартовая сцена: `Scenes/gc26_timetraveler_Main.unity` (`TimetravelerGame` на объекте `TimetravelerRoot`)
- Команда: Ilona Kostiuk
- Game Design Document: https://claude.ai/artifact/FmYnjxVwHUhJNiAVmtr1rZ

Time Traveler Runner: a portrait 2D story runner. The game locks the screen to portrait while it runs
and restores the orientation on exit.

## Folders
- `Art/` - images get pixel-art import settings automatically (16 px per unit, sharp pixels).
  `Art/Portraits/` keeps smooth anime-style portraits.
- `Editor/` - editor tools: menu **Tools > Time Traveler** (phone view, open main scene).
- `Scripts/` - game code, namespace `GC26.Timetraveler`: `TimetravelerFlow` (menu -> map -> level),
  `Menu/`, `Map/` (world map), `Runner/` (the runner), `TimetravelerLevels` (level list: edit lengths/order here),
  `TimetravelerSave` (coins, best distance, finished levels).
- `Resources/gc26_timetraveler/Slots/` - **art slots**: drop a PNG with the right name and it replaces the
  coloured box. Names and sizes: see `Slots/README.md` there.
- `Resources/gc26_timetraveler/Traveler/` - the traveler's runner frames.
- `Tools~/` - helper scripts (Unity ignores this folder).

## Как запустить / How to run

1. `git switch game/gc26_timetraveler` (the game is not in `main` until the final merge).
2. Open the project in Unity 6000.5.6f1 and open the scene
   `Assets/Microgames/gc26_timetraveler/Scenes/gc26_timetraveler_Main.unity`
   (or menu **Tools > Time Traveler > Open Main Scene**).
   Before Play the scene shows a small preview (ground, traveler, rock, log); the real track is built at runtime.
3. Press **Play**: main menu -> **Play** -> world map -> walk to level 1-1 -> **Play**. Optional: **Tools > Time Traveler > View as Phone (Simulator)**.
4. Console: every log line starts with `[gc26_timetraveler]` (start, each run, world switch, time slow,
   pause, death with its cause, exit). Type `timetraveler` in the Console search box to filter.

## Как играть / How to play

**Menu**: Play (world map), Endless run (opens after the Age of Dinosaurs; always open in the editor), Exit.

**World map**: hold a finger / the mouse where to walk, or WASD / arrows. Stand on a level node and press
Play (or Enter). Levels open one after another; later timelines are locked behind the gate.

**Levels**: the same track every attempt, reach the finish line. Coins go to your bank even if you die.

**Runner**: run to the finish (levels) or as far as you can (endless). Red is always deadly. The traveler is orange in the PAST and blue in the FUTURE:
orange or blue pieces are solid only when he has the same colour.

| Action | Touch | Keyboard |
| --- | --- | --- |
| Jump (again in the air = double jump) | Tap | Space / W |
| Slide (hold to keep sliding) / fast fall in the air | Swipe down | S / Down |
| Slow time (watch energy) | Swipe up | E / Shift |
| Switch world (Past / Future) | Swipe left or right | Q |
| Pause, then "To map" / "To menu" | II button (top right) | Esc / P |

Score sent to the GC26 menu: best endless distance in metres.

Testing: progress is in PlayerPrefs (keys start with `gc26_timetraveler_`). To start over, delete them with
**Edit > Clear All PlayerPrefs** (this clears every game's PlayerPrefs in the project).
