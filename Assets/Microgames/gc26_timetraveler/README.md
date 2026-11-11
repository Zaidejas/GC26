# gc26_timetraveler

- Ветка: `game/gc26_timetraveler`
- Неймспейс: `GC26.Timetraveler`
- Стартовая сцена: `Scenes/gc26_timetraveler_Main.unity` (`TimetravelerGame` на объекте `TimetravelerRoot`)
- Команда:
- Game Design Document: https://claude.ai/artifact/FmYnjxVwHUhJNiAVmtr1rZ

Time Traveler Runner: a portrait 2D story runner. The game locks the screen to portrait while it runs
and restores the orientation on exit.

## Folders
- `Art/` - images get pixel-art import settings automatically (16 px per unit, sharp pixels).
  `Art/Portraits/` keeps smooth anime-style portraits.
- `Editor/` - editor tools: menu **Tools > Time Traveler** (phone view, open main scene).
- `Scripts/Core`, `Scripts/Runner`, `Scripts/UI` - game code, namespace `GC26.Timetraveler`.

How to play: tap to jump (hold for higher), tap in the air to double jump, swipe down to slide.
