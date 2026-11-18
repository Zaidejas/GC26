# gc26_minesweeper — Red Flags

- Ветка: `gc26_minesweeper`
- Неймспейс: `GC26.Minesweeper`
- Стартовая сцена: `Scenes/gc26_minesweeper_Main.unity` (компонент `MinesweeperGame` на объекте `MinesweeperGame`)
- Команда: Tvareena
- Дизайн: `docs/Minesweeper_GDD.md`, техническое описание: `docs/Minesweeper_TDD.md`

Как играть (How to play):
- Tap cells to reveal them. **Avoid the red flags!**
- A number shows how many red flags touch that cell, including diagonally.
- Long press (or right-click) a cell to mark it with a yellow flag where you suspect a red flag. The Flag button at the bottom does the same with a normal tap.
- Tap an opened number when all its red flags are marked to open the cells around it.
- Open every cell without a red flag to win. Your first tap is always safe.
- Top bar: ☰ pause (Resume / Change level / Exit to menu), red flag counter, face = restart, timer, ? = How to play.

## Structure
| Folder | What's inside |
|---|---|
| `Scripts/Core/` | The rules in plain C# (`MinesweeperBoard`, `MinesweeperCell`, `MinesweeperState`, `MinesweeperDifficulty`, `MinesweeperLossLines`) |
| `Scripts/` | `MinesweeperGame` (the `IMicrogame`; builds the whole UI from code), `MinesweeperCellView` (tap / long press / right click), `MinesweeperSprites` (graphics drawn in code), `MinesweeperSafeArea` |
| `Resources/gc26_minesweeper/loss_lines.txt` | Lines shown after a loss, one per line; `#` lines are comments. Add lines here, no code changes needed |
| `Editor/` | `GC26 → Minesweeper → Create Main Scene` and `Build Windows Player` |
| `Tests/EditMode/` | Unit tests for the rules (Test Runner → EditMode) |

## Windows build for other PCs
`GC26 → Minesweeper → Build Windows Player` makes `Builds/RedFlags_Windows/` (not committed). Copy the whole folder (or its zip) to any Windows PC and run `RedFlags.exe` — nothing to install.
