# Red Flags: browser prototype

A simple HTML/CSS/JS version of the game described in [../Minesweeper_GDD.md](../Minesweeper_GDD.md). Use it to try the rules and the controls before building the Unity version.

## Open it
**Double-click `index.html`** (or drag it into a browser window). It works offline in any modern browser (Chrome, Edge, Firefox, Safari) on any computer: nothing to install, no internet, no server. The whole prototype is this **one file**, so it also works in previews that only show a single file (for example the Claude app's side browser) and can be copied or emailed on its own.

**Phone-sized view on a computer:** press `F12` in Chrome or Edge, then `Ctrl+Shift+M`, and pick a phone. Long press = hold the mouse button for about half a second.

**On a phone (optional):** if the computer has Python 3.7 or newer, run
```
python serve.py --lan
```
and open the address it prints on a phone that's on the same Wi-Fi. `python serve.py` alone serves it at http://127.0.0.1:8000 on the same computer.

## Add loss lines
Open `index.html` in any text editor and find the block marked **"Loss lines: edit here"** (search for `LOSS_LINES_TEXT`). Add lines between the two backticks (`` ` ``), one line per entry. Save and reload the page. Empty lines and lines starting with `#` are ignored; don't use the `` ` `` character inside a line. (The Unity game will use a plain `loss_lines.txt` file; the prototype keeps the lines inside the page so it works without loading other files.)

## What's in it
- Starts on the difficulty choice: Beginner 9×9/10, Intermediate 16×16/40, Expert 16×30/99, with best times
- Every new board starts with all cells hidden
- Tap to open, long press (or right-click) for a yellow flag, a Reveal/Flag mode toggle
- Safe first tap, flood fill, chording on numbers, win and loss, counter, timer, face
- First-game tutorial: asks for a first tap, then highlights the opened cell with the biggest number ("This cell has N red flags around it — be careful!"). It shows in the first game every time the page is opened; reload the page to see it again
- **?** button at the right of the top bar: a "How to play" pop-up; OK or a tap/click anywhere closes it, and the timer pauses while it's open
- Loss panel with a random line from the loss lines block
- Back button / Escape returns to the menu; the timer pauses when the page is hidden

Left out to keep it simple: pinch zoom and pan (Expert scrolls instead), sounds, settings, animations. Best times are saved in the browser on that computer only.

## Files
| File | What it does |
|---|---|
| `index.html` | The whole prototype, open this one. Inside, in order: styles (portrait layout, CSS flags), the screens, the **loss lines** block, and the game code (`Board` with the same rules as `Board.cs`, `LossLines`, tutorial, UI and touch handling) |
| `serve.py` | Optional small web server for testing on a phone |
