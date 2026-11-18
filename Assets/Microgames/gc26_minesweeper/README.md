# gc26_minesweeper — Red Flags

- Ветка: `game/gc26_minesweeper`
- Неймспейс: `GC26.Minesweeper`
- Стартовая сцена: `Scenes/gc26_minesweeper_Main.unity` (компонент `MinesweeperGame` на объекте `MinesweeperGame`)
- Команда: Tvareena
- Дизайн: [`Docs~/Minesweeper_GDD.md`](Docs~/Minesweeper_GDD.md), техническое описание: [`Docs~/Minesweeper_TDD.md`](Docs~/Minesweeper_TDD.md), браузерный прототип: [`Docs~/Minesweeper_Prototype/`](Docs~/Minesweeper_Prototype/README.md)
- `Docs~` ends with `~`, so Unity doesn't import it; it lives in the game folder because of rule 1 (only your own folder).

Как играть (How to play):
- Tap cells to reveal them. **Avoid the red flags!**
- A number shows how many red flags touch that cell, including diagonally.
- Long press (or right-click) a cell to mark it with a yellow flag where you suspect a red flag. The Flag button at the bottom does the same with a normal tap.
- Tap an opened number when all its red flags are marked to open the cells around it.
- Open every cell without a red flag to win. Your first tap is always safe.
- Top bar: ☰ pause (Resume / Menu / Exit), red flag counter, face = restart, timer, ? = How to play.
- **Menu** always means Red Flags' own main menu (the level screen); **Exit** always means leaving Red Flags (closes the app, or back to the GC26 menu).

## Structure
| Folder | What's inside |
|---|---|
| `Scripts/Core/` | The rules in plain C# (`MinesweeperBoard`, `MinesweeperCell`, `MinesweeperState`, `MinesweeperDifficulty`, `MinesweeperLossLines`) and `MinesweeperSolver`, which checks that a board can be solved without guessing |
| `Scripts/` | `MinesweeperGame` (the `IMicrogame`; builds the whole UI from code), `MinesweeperCellView` (tap / long press / right click), `MinesweeperSprites` (graphics drawn in code), `MinesweeperSafeArea` |
| `Resources/gc26_minesweeper/loss_lines.txt` | Lines shown after a loss, one per line; `#` lines are comments. Add lines here, no code changes needed |
| `Editor/` | `GC26 → Minesweeper → Create Main Scene` and `Build Windows Player` |
| `Tests/EditMode/` | Unit tests for the rules (Test Runner → EditMode) |

## Version
The version is shown at the bottom of every screen. It lives in `Scripts/MinesweeperVersion.cs`: bump it with every change (MAJOR.MINOR.PATCH: PATCH = fixes and small tweaks, MINOR = new features, MAJOR = big redesigns) and add a line below.

In Unity: **GC26 → Minesweeper → Version → Bump Version...** — describe the change, press Patch, Minor or Major; it updates the number and adds the row here. **Show Current Version** shows the number and what the three parts mean.

| Version | Changes |
|---|---|
| 0.3.1 | Button words match their action: pause is now Resume / Menu (level screen) / Exit (leave Red Flags); questions and Back say "go to the menu" or "exit" |
| 0.3.0 | "Are you sure you want to leave and lose your progress?" before restart, Change level, Exit to menu or Back during a game in progress |
| 0.2.2 | Class rules (tools/): tutorial state is no longer static (rule 6), docs moved into the game folder as `Docs~/` (rule 1), branch `game/gc26_minesweeper` |
| 0.2.1 | About 4% of boards need a guess again, on every level (MinesweeperBoard.GuessChance); measured 3.9% |
| 0.2.0 | No-guess boards: every board can be solved by logic from the first tap (MinesweeperSolver). Before: 11.5% / 33.8% / 89.3% of Beginner / Intermediate / Expert boards needed a guess |
| 0.1.2 | Editor: GC26 → Minesweeper → Version menu to bump the version and add the changelog row |
| 0.1.1 | Version number at the bottom of the screen; used for the Windows build |
| 0.1.0 | First playable version: levels, tutorial, How to play, pause, loss lines, Windows build |

## Windows build for other PCs
`GC26 → Minesweeper → Build Windows Player` makes `Builds/RedFlags_Windows/` (not committed). Copy the whole folder (or its zip) to any Windows PC and run `RedFlags.exe` — nothing to install.
