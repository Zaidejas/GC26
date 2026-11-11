# Red Flags: browser prototype

A simple HTML/CSS/JS version of the game described in `../04_Game_Design_Document.md`. Use it to try the rules and the controls before building the Unity version.

## Open it
**Double-click `index.html`** (or drag it into a browser window). It works offline in any modern browser (Chrome, Edge, Firefox, Safari) on any computer: nothing to install, no internet, no server. Keep all files in this folder together.

**Phone-sized view on a computer:** press `F12` in Chrome or Edge, then `Ctrl+Shift+M`, and pick a phone. Long press = hold the mouse button for about half a second.

**On a phone (optional):** if the computer has Python 3.7 or newer, run
```
python serve.py --lan
```
and open the address it prints on a phone that's on the same Wi-Fi. `python serve.py` alone serves it at http://127.0.0.1:8000 on the same computer.

## Add loss lines
Open `loss_lines.js` in any text editor and add lines between the two backticks (`` ` ``), one line per entry. Save and reload the page. Empty lines and lines starting with `#` are ignored; don't use the `` ` `` character inside a line. (The Unity game will use a plain `loss_lines.txt` file; the prototype uses a `.js` file because browsers don't let a page opened from disk read `.txt` files.)

## What's in it
- Starts on the difficulty choice: Beginner 9×9/10, Intermediate 16×16/40, Expert 16×30/99, with best times
- Every new board starts with all cells hidden
- Tap to open, long press (or right-click) for a yellow flag, a Reveal/Flag mode toggle
- Safe first tap, flood fill, chording on numbers, win and loss, counter, timer, face
- Loss panel with a random line from `loss_lines.js`
- Back button / Escape returns to the menu; the timer pauses when the page is hidden

Left out to keep it simple: pinch zoom and pan (Expert scrolls instead), sounds, settings, animations. Best times are saved in the browser on that computer only.

## Files
| File | What it does |
|---|---|
| `index.html` | Menu and game screens; open this one |
| `style.css` | Portrait layout; flags drawn with plain CSS (pole + rectangle) |
| `game.js` | `Board` (same rules as `Board.cs`), `LossLines`, UI and touch handling |
| `loss_lines.js` | Lines shown after a loss, one per line; `#` lines are comments |
| `serve.py` | Optional small web server for testing on a phone |
