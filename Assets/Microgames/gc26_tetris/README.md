# gc26_tetris — Block Drop

- Ветка: `game/gc26_tetris`
- Неймспейс: `GC26.Tetris` (логика — `GC26.Tetris.Logic`, отрисовка — `GC26.Tetris.View`)
- Стартовая сцена: `Scenes/gc26_tetris_Main.unity`, компонент `TetrisGame` (реализует `IMicrogame`)
- Команда: Golemch1k

Как играть: ← → двигать, ↑ / X повернуть, Z повернуть назад, ↓ быстрее вниз, пробел сбросить,
C отложить в запас, P или Esc пауза, M звук. В меню — стрелки и Enter или мышь.

Режимы:
- **Марафон** — играй, пока хватит места; комбо до ×4, бомбы и молнии.
- **Бой с ботом** — линии и комбо бьют бота, из него падают монетки, побеждённого сменяет более крепкий.

В меню GC26 пункт «В меню GC26» выходит в оболочку с лучшим счётом за этот заход.

Для разработки (меню **GC26 Tetris**): Create Scene — пересоздать сцену, Run Rules Tests — 46 тестов правил,
Build Windows — отдельная сборка `Build/BlockDrop.exe`. Из командной строки:
`Unity -batchmode -projectPath . -executeMethod GC26.Tetris.EditorTools.TetrisProjectSetup.RunAll`.
