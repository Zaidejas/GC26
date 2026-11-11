# gc26_lines

- Branch: `gc26_lines` (the team rules ask for `game/gc26_lines`; renaming is still to do)
- Namespace: `GC26.Lines`
- Start scene: `Scenes/gc26_lines_Main.unity` (made from code by `Lines → Run Project Setup`; holds `GameBootstrap` and `LinesMicrogame`)
- Team:

How to play: tap a mushroom, then an empty cell. It moves there if there is a free path up, down, left or right. Line up five or more of the same mushroom in a row, column or diagonal to clear them and score. After each move that clears nothing, three new mushrooms appear. The game ends when the board is full.

Docs (Unity skips folders ending in `~`):

- `Documentation~/Lines_Game_Design_Document.md`: rules, screens, art (the source of truth)
- `Documentation~/prototype/lines-prototype.html`: playable browser prototype
- `Documentation~/Lines_Plan_EN_RU.md`: the 8-week plan
- `Tools~/build.sh`: builds Lines on its own as a phone app (iOS Simulator, iPhone, Android)
- `CLAUDE.md`: working notes for Claude
