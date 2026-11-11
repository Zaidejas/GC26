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
- `Scripts/Core`, `Scripts/Runner`, `Scripts/UI` - game code, namespace `GC26.Timetraveler`.

## Как запустить / How to run

1. `git switch game/gc26_timetraveler` (the game is not in `main` until the final merge).
2. Open the project in Unity 6000.5.6f1 and open the scene
   `Assets/Microgames/gc26_timetraveler/Scenes/gc26_timetraveler_Main.unity`
   (or menu **Tools > Time Traveler > Open Main Scene**).
   Before Play the scene shows a small preview (ground, traveler, rock, log); the real track is built at runtime.
3. Press **Play**. Optional: **Tools > Time Traveler > View as Phone (Simulator)**.
4. Console: every log line starts with `[gc26_timetraveler]` (start, each run, world switch, time slow,
   pause, death with its cause, exit). Type `timetraveler` in the Console search box to filter.

## Как играть / How to play

Run as far as you can. Red is always deadly. The traveler is orange in the PAST and blue in the FUTURE:
orange or blue pieces are solid only when he has the same colour.

| Action | Touch | Keyboard |
| --- | --- | --- |
| Jump (hold = higher, again in the air = double jump) | Tap | Space / W |
| Slide (hold to keep sliding) / fast fall in the air | Swipe down | S / Down |
| Slow time (watch energy) | Swipe up | E / Shift |
| Switch world (Past / Future) | Swipe left or right | Q |
| Pause, then "To menu" | II button (top right) | Esc / P |

Score sent to the menu: best distance in metres.
