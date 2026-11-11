# Red Flags: Game Design Document (GDD)

| | |
|---|---|
| **Title** | Red Flags (a Minesweeper-style game) |
| **Genre** | Single-player logic / puzzle game |
| **Engine** | Unity 6000.5.6f1 (URP), C#; project GC26, branch `gc26_minesweeper` |
| **Platform** | Android phones (main); Android tablets supported; iOS optional later |
| **Orientation** | Portrait only |
| **Input** | Touch: tap, long press, drag, pinch; Android Back button |
| **Target player** | Anyone who likes short logic puzzles on the go; one round lasts from 10 seconds to a few minutes, playable one-handed on small boards |
| **Related documents** | [Technical_Design_Document.md](Technical_Design_Document.md) (architecture, code, development plan and tasks), [prototype/](prototype/README.md) (playable browser prototype), [task board](https://github.com/users/Zaidejas/projects/6) |

> **How to open:** this is a Markdown file; it opens in any text editor and looks best in VS Code (`Ctrl+Shift+V`) or on GitHub. The prototype opens by double-clicking `prototype/index.html`.

---

## 1. Game concept

The player sees a rectangular field of closed squares. Some squares hide **red flags**. The player taps squares to open them one at a time. Each opened square shows how many red flags touch it, and the player uses those numbers to work out where the red flags are. The player **wins** by opening every square that doesn't hide a red flag and **loses** by opening a red flag. In short: **avoid the red flags**.

**Design pillars**
1. **Pure logic:** almost every situation can be solved by thinking, not guessing. The first tap is always safe.
2. **Made for fingers:** every action has a clear touch gesture, a mistaken tap is hard to make, and the board can be zoomed so cells are never too small to hit.
3. **Quick sessions:** a round can be started, paused (by leaving the app) and finished in moments; restarting takes one tap.
4. **Faithful to the classic:** the same rules as the original Minesweeper, adapted only where touch requires it.

### 1.1 Theme and terminology
The rules are classic Minesweeper, but the theme is **"avoid the red flags"**:

| In the game (what the player sees) | In this document and in the code |
|---|---|
| **Red flag**: a rectangular red flag hidden under a square; the hazard to avoid | "mine" (`IsMine`, `MineCount`) |
| **Yellow flag**: the marker the player places on suspicious squares | "flag" (`IsFlagged`, `ToggleFlag`) |

All text the player sees says "red flags", never "mines". In the rest of this document, "mine" means a hidden red flag and "flag" means the player's yellow flag.

### 1.2 Scope

#### Must have (MVP, done by the end of week 7)

- A grid of cells with randomly placed red flags, shown in portrait mode
- Every new board starts with all cells hidden; nothing is revealed until the player taps
- **Tap** reveals a cell; **long press** places or removes a flag (with a short vibration)
- A **flag-mode toggle button**: while it's on, a tap places a flag instead of revealing
- Numbers show how many mines touch each cell
- Revealing an empty (0) cell automatically opens its neighbours (flood fill)
- The first tap is always safe
- Win when every safe cell is revealed; lose when a red flag is revealed
- On a loss, the pop-up shows one random line from a list under the "lost" message; the list is a text file you can add lines to later
- UI: mine counter, timer, restart button, all inside the phone's **safe area** (clear of the notch and rounded corners)
- Runs on a real Android phone

#### Should have (weeks 8–10)

- Three difficulty levels designed for portrait: Beginner 9×9 with 10 mines, Intermediate 16×16 with 40, **Expert 16×30** with 99 (the classic 30×16 board turned upright)
- A main menu scene; the Android **Back** button works
- **Pinch to zoom and drag to pan**, needed so Expert cells are big enough to tap
- Chording: tap a revealed number whose flags are all placed to open its remaining neighbours
- Best times saved with `PlayerPrefs`
- Timer pauses when the app goes to the background
- Sound effects, vibration, settings for sound and vibration on/off

#### Nice to have (only if there is time left)

- Small animations (tile pop, camera shake)
- Google Play internal-testing release
- iOS build (needs a Mac and an Apple developer account)
- A custom board size

What is left out of the game is listed in section 10.

## 2. Game elements

### 2.1 The board
- A grid of squares (cells). Its size and number of mines depend on the difficulty.
- Each cell either hides a **mine** or is **safe**.
- Each safe cell has a **number** from 0 to 8: how many of its up to 8 neighbours (horizontal, vertical and diagonal) contain mines.

```
 Neighbours of X:
 ┌───┬───┬───┐
 │ ↖ │ ↑ │ ↗ │
 ├───┼───┼───┤
 │ ← │ X │ → │
 ├───┼───┼───┤
 │ ↙ │ ↓ │ ↘ │
 └───┴───┴───┘
 Corner cells have 3 neighbours, edge cells have 5, all others have 8.
```

### 2.2 Cell states

| State | Looks like | Meaning |
|---|---|---|
| Hidden | Raised grey tile | Not opened yet |
| Pressed | Hidden tile, slightly darker | A finger is currently on it |
| Flagged | Hidden tile with a **yellow** flag | The player thinks a red flag is hidden here |
| Revealed, empty (0) | Flat, blank tile | No mines nearby |
| Revealed, number 1–8 | Flat tile with a coloured number | That many mines nearby |
| Red flag (after a loss) | **Rectangular red flag** on a flat tile | A red flag the player did not find |
| Hit red flag | Red flag on a dark tile with a bright border | The red flag the player tapped |
| Wrong flag (after a loss) | Yellow flag with a cross | A yellow flag was placed on a safe cell |

**Number colours** (classic): 1 blue, 2 green, 3 red, 4 dark blue, 5 dark red, 6 teal, 7 black, 8 grey. Numbers are bold and large so they're readable on a small screen.

### 2.3 Difficulty levels
Boards are arranged for a **tall, portrait** screen.

| Level | Size (columns × rows) | Mines | Mine density | On a typical phone |
|---|---|---|---|---|
| Beginner | 9 × 9 | 10 | 12% | Whole board fits with large cells; no zoom needed |
| Intermediate | 16 × 16 | 40 | 16% | Whole board fits; zoom helps on small phones |
| Expert | 16 × 30 | 99 | 21% | Whole board fits when zoomed out; zoom in to play comfortably |
| Custom *(optional)* | Chosen by the player | Chosen by the player (at most cells − 9) | — | — |

Expert is the classic 30×16 board turned upright, so it has the same difficulty as the original.

## 3. Rules

### 3.1 Starting a round
1. A round always starts from the **difficulty choice**: the app opens on it, and the player picks Beginner, Intermediate or Expert before any board is shown.
2. The round starts in the **Ready** state: **every cell is hidden**, the timer shows `000`, and the counter shows the total number of red flags. The game never opens or marks any cell by itself before the player's first action; this is true for every new board (from the menu, the face button or Play again).
3. Mines are **not** placed until the first reveal.
4. On the first tap that reveals a cell, mines are placed randomly, but **never on the tapped cell or its 8 neighbours**. So the first tap always opens a 0, which opens an area of the board.
5. The timer starts on that first reveal and the round enters the **Playing** state.

### 3.2 Opening a cell (tap)
- **Mine:** the round is lost immediately.
- **Number 1–8:** only that cell opens.
- **0 (empty):** the cell opens, and all its neighbours open automatically. Any neighbour that is also a 0 opens its neighbours too, and so on. This chain stops at numbered cells, so an empty region opens along with its numbered border ("flood fill").
- Tapping a flagged cell does nothing (in normal mode).
- A cell opens when the finger is **lifted**, not when it touches. If the finger moves away (a drag), nothing opens. This prevents accidental opens while scrolling.

### 3.3 Flagging (long press, or tap in flag mode)
There are two ways to flag, so players can choose what's comfortable:
- **Long press** (hold about 0.4 s) on a hidden cell places a flag, or removes one. The phone gives a short vibration to confirm.
- **Flag mode:** the button in the bottom bar switches between ⛏ **Reveal mode** (default) and 🚩 **Flag mode**. In flag mode a normal tap places or removes a flag. Long press still flags in both modes.

Rules for flags:
- A flagged cell is protected: it can't be opened by a tap or by flood fill.
- Opened cells can't be flagged.
- Flags are only a note for the player. The game doesn't check whether they're correct until the round ends.

### 3.4 Chording (open a number's remaining neighbours)
- Tapping an **opened number** whose neighbouring flags **equal that number** opens all of its other hidden, unflagged neighbours at once. This works in both reveal and flag mode.
- If the number of flags doesn't match, nothing happens.
- If one of those flags is wrong, chording opens a mine and the round is lost.

### 3.5 Zoom and pan
- **Pinch** with two fingers to zoom in and out. Zooming out stops when the whole board is visible; zooming in stops at about 4 cells across.
- **Drag** with one finger to move around the board. The view can't move away from the board.
- Zooming and panning never open or flag cells.

### 3.6 Mine counter
- Shows **total red flags − yellow flags placed**.
- It can go below zero if the player places more flags than there are mines. That's a hint that some flags are wrong.

### 3.7 Timer
- Counts whole seconds from the first reveal.
- **Pauses** while the app is in the background (Home button, phone call, screen locked) and continues on return.
- Stops when the round is won or lost.
- Stops at 999.

### 3.8 Winning
- The round is won as soon as **every safe cell is opened**. Flags aren't needed to win.
- On a win: the timer stops, every remaining mine is flagged automatically, the mine counter shows `000`, the face shows 😎, and the win sound plays.
- If the time is lower than the saved best time for that difficulty, it becomes the new **best time** and the player sees a "New record!" message.

### 3.9 Losing
- The round is lost as soon as a mine is opened, by a normal tap or by chording.
- On a loss: the timer stops, the tapped red flag is highlighted, all other red flags are shown, wrongly placed yellow flags are crossed out, the face shows 😵, the loss sound plays, the phone vibrates, and the camera shakes briefly.
- The loss panel shows **one randomly picked line** from the loss lines list under the "lost" message (see 6.4).

### 3.10 After the round
- The board can't be changed any more after a win or loss, but the player can still zoom and pan to look at it.
- The player can restart (face button or **Play again**) or return to the menu (**Menu** button or Android **Back**).

## 4. Controls

| Gesture | Where | Action |
|---|---|---|
| Tap | Hidden cell, reveal mode | Open it |
| Tap | Hidden cell, flag mode | Place or remove a flag |
| Long press (~0.4 s) | Hidden cell, any mode | Place or remove a flag (with vibration) |
| Tap | Opened number | Chord |
| Finger held on the board | — | Tile looks pressed; face changes to 😮 |
| One-finger drag | Board | Pan the view |
| Two-finger pinch | Board | Zoom in or out |
| Tap face button | Top bar | Restart with the same difficulty |
| Tap ⛏/🚩 button | Bottom bar | Switch between reveal and flag mode |
| Tap Menu button / Android **Back** | Game screen | Back to the main menu |
| Android **Back** | Main menu | Close the app |

All buttons are at least 48 dp (about 9 mm) so they're easy to hit with a thumb. The flag-mode button is at the bottom, within thumb reach.

## 5. User flow

### 5.1 Screen flow

```
          ┌───────────────┐   Back
 Launch ─►│   Main Menu   │──────────► app closes
          └──┬─────────┬──┘
   choose    │         │ ⚙ Settings
   difficulty│         ▼
             │   ┌───────────────┐
             │   │ Settings panel│ sound / vibration on-off
             │   └───────────────┘
             ▼                                    Menu button / Back
          ┌───────────────┐                       from any state
          │  Game: Ready  │◄─────────────┐        ──► Main Menu
          └──────┬────────┘              │
                 │ first reveal tap      │ Restart
                 ▼                       │ (face / Play again)
          ┌───────────────┐              │ from any state
          │ Game: Playing │──────────────┤
          └───┬───────┬───┘              │
   all safe   │       │ mine opened      │
   cells open ▼       ▼                  │
      ┌──────────┐ ┌──────────┐          │
      │   Won    │ │   Lost   │──────────┘
      │ (record?)│ │          │
      └──────────┘ └──────────┘

 The app always opens on the Main Menu (difficulty choice); there is no
 splash or intro screen. Every new board starts with all cells hidden.

 Leaving the app (Home, call, lock) at any time pauses the timer;
 returning continues the same round.
```

### 5.2 Step-by-step: first-time player
1. The player opens the app and the **difficulty choice (Main Menu)** appears straight away.
2. The player taps **Beginner**. The **Game** screen loads with a 9×9 board filling the width of the screen, **all cells hidden**; the counter shows `010` and the timer `000`.
3. The player taps any cell. It is always safe, an area opens, and the timer starts.
4. The player studies the numbers, long-presses cells they're sure are mines (the phone buzzes and a flag appears), and taps cells they're sure are safe.
5. **Either:**
   - **a.** All safe cells are open → **Win**: 😎, the time is shown and maybe saved as a record. **Or:**
   - **b.** The player opens a mine → **Lose**: the phone vibrates and all mines are shown, 😵.
6. The player taps **Play again**, or presses **Back** to return to the menu and choose another difficulty.

### 5.3 Step-by-step: experienced player
1. Menu → **Expert**. The 16×30 board appears fully zoomed out.
2. First tap → a large area opens.
3. The player pinches to zoom into one area and drags to move around.
4. The player switches to 🚩 flag mode for quick flagging, then taps numbers to **chord** and clear cells fast.
5. A phone call interrupts; the timer pauses. After the call the player returns and continues.
6. Wins in 142 s → "New record!" → the best time for Expert is updated in the menu.
7. Taps **Play again** at once to try for a better time.

## 6. Screens

All layouts are **portrait** and stay inside the phone's **safe area** (clear of the notch, camera hole and rounded corners).

### 6.1 Main Menu (difficulty choice, the first screen)
```
┌───────────────────────────┐
│       (notch / status)    │
│                       ⚙   │
│                           │
│      M I N E S W E E P E R│
│                           │
│  ┌─────────────────────┐  │
│  │ Beginner            │  │
│  │ 9×9 · 10 red flags  │  │
│  │ Best: 12 s          │  │
│  └─────────────────────┘  │
│  ┌─────────────────────┐  │
│  │ Intermediate        │  │
│  │ 16×16 · 40 red flags│  │
│  │ Best: 68 s          │  │
│  └─────────────────────┘  │
│  ┌─────────────────────┐  │
│  │ Expert              │  │
│  │ 16×30 · 99 red flags│  │
│  │ Best: —             │  │
│  └─────────────────────┘  │
│                           │
└───────────────────────────┘
```
- This is the first screen after launch; a game is only started by tapping one of the cards.
- Large difficulty cards, each showing the board size, the number of red flags and the best time ("—" if there is none yet).
- ⚙ opens the settings panel: **Sound** on/off and **Vibration** on/off.
- There is **no Quit button**; on Android the Back or Home button leaves the app.

### 6.2 Game screen
```
┌───────────────────────────┐
│       (notch / status)    │
│ [☰]  010    🙂    000     │  ← top bar: menu, mine counter, face, timer
├───────────────────────────┤
│ ┌─┬─┬─┬─┬─┬─┬─┬─┬─┐       │
│ │ │ │ │1│■│■│■│■│■│       │
│ ├─┼─┼─┼─┼─┼─┼─┼─┼─┤       │
│ │ │ │1│2│■│🚩│■│■│■│      │  ← board: fills the width; pinch to zoom,
│ ├─┼─┼─┼─┼─┼─┼─┼─┼─┤          drag to pan
│ │  ...                    │
│ └─┴─┴─┴─┴─┴─┴─┴─┴─┘       │
│                           │
├───────────────────────────┤
│      [ ⛏  |  🚩 ]         │  ← bottom bar: reveal/flag mode toggle
│       (home indicator)    │
└───────────────────────────┘
```
- In the sketches, 🚩 stands for the player's **yellow** flag (on the board and on the mode toggle).
- **Menu button** (☰, top left): back to the main menu.
- **Red flag counter**: red flags minus yellow flags, three digits.
- **Face button** (centre): shows the state and restarts the round when tapped.

  | Face | When |
  |---|---|
  | 🙂 | Ready or Playing |
  | 😮 | A finger is held on the board |
  | 😎 | Won |
  | 😵 | Lost |

- **Timer**: seconds, three digits.
- **Mode toggle** (bottom): the active mode is highlighted. It sits at the bottom so the thumb can reach it easily.
- The board starts zoomed so it fills the space between the bars.

### 6.3 End-of-round message
A panel that slides up from the bottom, so the board stays visible above it and can still be zoomed and panned to study it.
- Win: "You won! Time: 37 s" (+ "New record!" if applicable) — [Play again] [Menu]
- Loss: "You hit a red flag!", with **one random line from the loss lines list** below it in smaller text — [Play again] [Menu]

### 6.4 Loss lines
- A list of short lines, one of which is picked at random each time the player loses and shown under the "lost" message.
- The list is a plain text file in the project (`Assets/Text/loss_lines.txt`), **one line per entry**. To add lines later, add new lines to the file and rebuild the app; no code changes are needed.
- Empty lines are ignored, and lines starting with `#` are comments (not shown).
- The panel's text box wraps long lines, but lines should be short (about 80 characters or less) to read well on a phone.
- If the list is empty, only the "lost" message is shown.

## 7. Feedback: audio, vibration and visuals

| Event | Visual | Sound | Vibration |
|---|---|---|---|
| Finger down on a hidden cell | Tile looks pressed, face 😮 | — | — |
| Open one cell | Tile "pops" (short scale animation) | Soft click | — |
| Flood fill | Opened tiles pop | Sweep / whoosh | — |
| Place or remove a yellow flag | Yellow flag appears or disappears | Short tick | Short buzz (long press only) |
| Chord | Neighbours pop | Click | — |
| Lose | Hit red flag highlighted, all red flags shown, camera shake, random loss line | Loss sound | Yes |
| Win | Remaining red flags covered with yellow flags, 😎 | Short victory jingle | — |
| New record | "New record!" text | — (the jingle is enough) | — |

Sound and vibration can each be turned off in the settings; the choices are remembered. Sound volume follows the phone's media volume.

## 8. Saved data

Stored locally on the phone with `PlayerPrefs`:

| Key | Value |
|---|---|
| `best_Beginner`, `best_Intermediate`, `best_Expert` | Best winning time in seconds |
| `sound` | 1 = on, 0 = off |
| `vibration` | 1 = on, 0 = off |

No account, internet connection or special permissions are needed. Uninstalling the app deletes this data.

## 9. Edge cases

| Situation | Expected behaviour |
|---|---|
| First tap near a corner or edge | The safe area is clipped to the board; the tap is still safe |
| Long press before the first reveal | The flag is placed; the timer doesn't start |
| Finger moves while pressed | Becomes a drag (pan); no cell opens or is flagged |
| Second finger touches during a tap | Becomes a pinch; the tap is cancelled |
| Touch starts on a button | Only the button reacts, never the cell behind it |
| Touch outside the board (empty margin) | Ignored |
| More flags than mines | Counter goes negative (e.g. `-02`) |
| Chord with the wrong number of flags | Nothing happens |
| App goes to background mid-round (call, Home, lock) | Timer pauses; the round is still there on return |
| Android closes the app while it's in the background | The round is lost (not saved); best times and settings are kept |
| Restart in the middle of a round | New board, timer reset; no best time saved |
| Timer reaches 999 | Stays at 999; the round continues |
| Phone with a notch or rounded corners | UI stays inside the safe area |
| Tablet or very tall phone | Board and UI scale; the board stays centred |
| Phone rotated | Nothing happens; the game stays in portrait |
| App reopened, or Back to the menu and a level chosen again | A brand-new board with all cells hidden; nothing from the previous board is shown |
| Loss lines list is empty | Only the "lost" message is shown |
| Very long loss line | The text wraps inside the panel |

## 10. Out of scope

Landscape mode, desktop and web versions, multiplayer, online leaderboards, ads and in-app purchases, saving an unfinished round, guaranteed "no-guess" boards, and replays.

## 11. Possible future ideas

- iOS version
- Save and resume an unfinished round
- Lighter, shorter haptic "ticks" using a native haptics plugin
- A "no-guess" mode that only generates boards solvable by logic
- Dark theme
- Daily puzzle with a shared seed
- Statistics: games played, win rate, longest streak
- Google Play Games achievements

## 12. Prototype

A simple playable browser version of these rules is in [prototype/](prototype/README.md). Open `prototype/index.html` in any browser (no installation or internet needed) to try the rules, touch controls and loss lines before building the Unity version. It leaves out zoom/pan, sounds and settings.
