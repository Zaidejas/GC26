# Lines — Game Design Document

Oct 21, 2026 · revised Nov 2, 2026 · hmmd2

Earlier online version (before the Nov 2 revision): [https\://claude.ai/artifact/MbLgFyYVPGJvoRkVvUrSSQ](https://claude.ai/artifact/MbLgFyYVPGJvoRkVvUrSSQ)

**Playable prototype:** [`prototype/lines-prototype.html`](prototype/lines-prototype.html) (open in a browser). The Unity game should look and play like it.

## Overview

Lines remakes the classic 1998 Windows puzzle game for phones, with a few custom changes. Players move balls on a 9×9 board to form lines of five and try to last as long as possible while new balls keep arriving.

This is a study project. The game is built for iOS and Android and installed directly on our own phones; it is **not published** to the App Store or Google Play.

The document separates the **MVP** (the first complete version) from **later** features. Anything marked *Later* is out of scope for the MVP.

| Item | Value |
| :---- | :---- |
| Genre | Casual turn-based puzzle, endless high-score |
| Platforms | iOS first, Android second. Day-to-day testing in the iOS Simulator and the Android Emulator, with regular checks on real phones |
| Distribution | None. Builds are installed directly on our own devices |
| Engine | Unity 6 (6000.5.6f1), C\# |
| Orientation | Portrait, one-handed play |
| Target screens | Two portrait shapes, as required by the course: **16:9** (e.g. 1080×1920) and **20:9** (e.g. 1080×2400) |
| Input | Touch (mouse clicks also work in the Unity editor, for testing) |
| Session length | 3–10 minutes. The game in progress is saved automatically, so the player can close it at any time and continue later |
| Audience | Players aged 25–55 who remember the original, plus casual puzzle fans |
| Development time | 8 weeks, one developer working with Claude |

**Selling points**

- The original rules people remember, plus a few small changes that make it easier to play.
- Fast, clean controls: two taps per move and no timers.
- The game in progress is saved automatically and picks up where the player left off.
- A dark, magical mushroom forest: 7 glowing mushrooms instead of plain balls, on a dark glass board.

**Custom changes compared to the original**

- **Undo:** take back the last move (one step).
- **Reset:** start a new game from the game screen at any time.
- **Blocked-move alert:** when a ball can't reach the tapped cell, the player sees a message instead of nothing happening.

## Core gameplay and rules

Each turn the player makes one move. A move that completes a line removes it and costs nothing. A move that doesn't brings 3 new balls onto the board.

![Turn loop](Lines_turn_loop.png)

Every turn ends either with a cleared line, which skips the spawn, or with 3 new balls arriving. The game ends only after a spawn fills the board.

| Rule | Value |
| :---- | :---- |
| Board | 9×9 cells (81) |
| Kinds of balls | 7: the 7 glowing mushrooms, the same in every game (see Art) |
| Balls at the start | 3, in random cells, as in the classic game |
| New balls per turn | 3. Their colors **and the cells where they will appear** are shown in advance (the cells show the next mushrooms as small see-through "ghost" mushrooms) |
| Movement | One ball per turn to any empty cell that has a path to it |
| Path | Up, down, left and right through empty cells. No diagonals and no jumping over balls |
| Line | 5 or more balls of one color in a row, column or diagonal |
| After a line clears | No new balls spawn that turn. The preview stays the same for the next turn |
| Spawned balls | Can also complete a line, which clears and scores |
| Game over | No empty cells are left |

**Edge cases**

- A move that forms two crossing lines clears both. The shared ball is removed once, but for scoring it counts in the length of **both** lines (two crossing 5-lines: (10 + 10) × 2 = 40).
- If the player moves a ball onto a cell with a preview dot, the ball for that dot appears in another random empty cell instead.
- If fewer than 3 cells are free when balls spawn, the board fills and the game ends.
- If the spawn fills the last free cells but the new balls complete a line, the line clears first, so the board is no longer full and the game goes on.
- Game over is final: Undo is not available after the board fills. The only option is to start a new game.
- Tapping a ball that has no reachable empty cell still selects it. The blocked move is reported when the player taps a target (see Controls). The original game shows nothing here; this is one of the custom changes.

## Controls and feedback

The whole game is played with two taps: tap a ball, then tap an empty cell.

| Player action | What happens |
| :---- | :---- |
| Tap a ball | The ball is selected and bounces in place |
| Tap another ball | Selection moves to that ball |
| Tap a reachable empty cell | The ball moves along the shortest path, cell by cell (about 40 ms per cell) |
| Tap an unreachable cell | The ball shakes, a short "No path" alert appears near the board, and the ball stays selected |
| Tap the selected ball again | Nothing happens. The ball stays selected. To change the selection, tap another ball |
| Undo button | Restores the board, the score and the preview to how they were before the last move. Only one step, as in the classic game: after an undo, the button is disabled until the next move. Also disabled at the start of a game |
| Reset button | Asks for confirmation, then clears the board and starts a new game |

Input is ignored while a move, clear or spawn animation is playing.

- Cells are at least 9 mm wide on a 6-inch phone, so the board fills the screen width.
- *Later:* a movement effect closer to the original, where the ball visibly steps from cell to cell along its path.
- *Later:* light vibration on a line clear, with an off switch in Settings.

## Scoring

Longer lines are worth much more than short ones, which rewards the player for taking risks. The values below are starting points to tune during testing in Week 7.

| Balls cleared in one line | Points |
| :---- | :---- |
| 5 | 10 |
| 6 | 14 |
| 7 | 20 |
| 8 | 28 |
| 9 | 36 |

- **Several lines in one move:** the total for the move is multiplied by the number of lines (two lines \= ×2).
- **Lines completed by spawned balls:** score normally, with no multiplier.
- **Undo:** removes the points scored by the move it takes back.
- **Leaderboard (MVP):** a local top 10 of finished games, each with a player name, score and date (see Screens and UI).
  - Only finished games count: a game abandoned with Reset or New game never reaches the leaderboard.
  - **Ties:** the older score keeps its place. A new score must be higher than the 10th place to get in.
  - After saving, the leaderboard says "New best score, Anna!", "You're #3, Anna!" or "120 points didn't make the top 10".
- *Later:* the best score shown on the game screen during play.

## Preview modes

The original game had three difficulty levels that differ only in how much the player knows about the next balls. The MVP ships with the first one. The other two are later features.

| Mode | What the player sees before the spawn | Release |
| :---- | :---- | :---- |
| Full preview | The colors of the next 3 balls and the cells where they will appear | MVP |
| Colors only | The colors of the next 3 balls, but not where they will appear | Later |
| No preview | Nothing about the next balls | Later |

## Later features (not in the MVP)

These are planned for after the MVP and are not scheduled in the 8 weeks unless time is left over.

| Feature | Notes |
| :---- | :---- |
| Preview modes | "Colors only" and "No preview" (see above), chosen when starting a new game |
| More menu entries | Modes on the main menu |
| Pause menu | Not needed: nothing happens until the player acts |
| Best score during play | The best score shown on the game screen next to the current score |
| More settings | A theme choice (Settings itself is now in the MVP) |
| How to play on first launch | Open the How to play cards automatically the first time the app starts |
| Audio | The sounds and music below: in the prototype now, in Unity next (board task L03) |
| Vibration | Light vibration on a line clear |
| More themes | The MVP has one theme: the dark magic forest |
| Custom art packs | Replace the MVP mushrooms (and board) with other art |
| Other game modes | Daily Challenge (the same seed for every player that day, one attempt) and Timed (3 minutes, +5 seconds per cleared line) |
| Achievements and statistics | For example: first 7-ball line, two lines in one move, 1,000 points in one game. Statistics: games played, best score, longest line, average score |
| Unlockable themes | Ball and board themes unlocked by reaching score milestones |
| Web version | A browser build of the same game |

**Sounds and music** (picked on the Forest Sounds listening page, `prototype/forest-sounds.html`; all made from code, no recordings, so nothing to license). They use the D major pentatonic scale, so every note fits with the music and with each other.

| Event | Sound |
| :---- | :---- |
| Select a mushroom | Fairy tick: a short rising "tick" with a tiny bell on top |
| Move | Sparkle run: one soft bell note per cell, rising, so a longer path is a longer run |
| Landing | Dew drop: a quick "plip" rising in pitch, with a tiny bell, together with the landing pulse |
| Line cleared | Harp: one plucked note per mushroom in the line, rising; a longer line starts higher |
| New mushrooms appear | Bubbles: three quick rising bubbles |
| No path | Low buzz: two short, soft, low buzzes, together with the "No path" alert |
| Game over | Music box: six bell notes falling, the last one long |
| New record (the score makes the top 10) | Warm swell: a soft warm chord with three bells on top |
| Button tap | Soft click (every button except the board's cells, which have their own sounds) |

**Music:** "Firefly dance", a calm endless loop: soft warm chords (D, Bm, G, A, about 6 s each), a low note at each chord change and a gentle kalimba pattern on top, slightly varied each time. It plays on every screen while Music is on, and fades in and out when switched. Music and Sounds each have an on/off switch in Settings. In a browser, sound can start only after the first tap.

## Screens and UI (MVP)

The MVP has four screens and three pop-ups. The app opens on the main menu.

| Screen | Contents | Opens from |
| :---- | :---- | :---- |
| Main menu | Game title, Continue (with the saved game's score), New game, How to play, Leaderboard; a gear icon (Settings) in the top-right corner | App launch; back buttons |
| Game | House button (main menu) top-left and gear (Settings) top-right, score, preview of the next 3 balls, board, Undo, Reset | Continue or New game |
| How to play | Back button, then 3 short cards with a small board picture each: move a ball along a free path; make lines of five; the next balls, game over and Undo. Back / Next, and "Got it" on the last card. The picture and title stay in the same place on every card | Main menu |
| Leaderboard | Back button, then the local top 10 finished games, each with rank, name, date and score; the game just finished is highlighted, with a result line above the list and a Play again button below it. Shows a short explanation while it's empty | Main menu; Save on the game-over pop-up |
| New game confirmation | "Start a new game?" with Yes and Cancel; the current game is lost | Reset button, or New game on the menu while a game is unfinished |
| Settings | A pop-up card with on/off switches for Music, Sounds and Vibration, the Language (English / Русский), and a How to play button. Closes with ✕ or a tap outside the card | The gear on the main menu or the game screen |
| Game over | Final score and a name field with Save, nothing else. Save adds the score to the leaderboard and opens the Leaderboard screen | Game, when the board is full |

**Main menu**

- **Continue** resumes the saved game and shows its score. It is disabled when there's no unfinished game.
- **New game** starts right away when there's no unfinished game; otherwise it asks for confirmation first.
- The game screen, How to play and Leaderboard all have the same house icon in the top-left corner (just the white icon, no ring or disc behind it). It returns to the main menu, except How to play opened from Settings during a game, which returns to the game. Leaving the game saves it, so Continue picks it up exactly where it was (Undo included).
- How to play and Leaderboard have no title at the top, only the back button.

**Settings** (teacher's feedback, pulled forward from the later features)

- The gear is a plain white icon like the house (no ring or disc). The pop-up is the same dark card as the other pop-ups; the game stays visible behind it.
- **Music, Sounds, Vibration:** on/off switches (gold when on), all on by default, remembered on the device. Music and Sounds work in the prototype (see Later features → Sounds and music); Unity doesn't play them yet, and there is no vibration yet, so there those switches only remember the choice.
- **Language:** English or Русский. Everything changes at once (menu, game screen, pop-ups, How to play, leaderboard messages and dates); the game's name "Lines" stays the same. The first time, the game uses Russian on a phone set to Russian, English otherwise; the choice is remembered.
- **How to play** closes Settings and opens the How to play cards; their back button and "Got it" return to where Settings was opened (the game or the menu). How to play also stays on the main menu.
- Later, in the shared GC26 app, this pop-up can also be the game's pause, with a "Back to games" button.

**Player name on game over**

- The name field is pre-filled with the last name the player typed. If they have never typed one, it's a placeholder **PlayerN**, where N counts the placeholder names used so far (Player1, Player2, …).
- The player can keep it or type their own (up to 16 characters). An empty field uses the placeholder.

**Game screen layout (portrait, top to bottom)**

1. House button (main menu) in the top-left corner: only the white house icon, no ring or disc.
2. Score and the preview of the next 3 ball colors.
3. The board, filling the screen width, with "ghost" mushrooms (preview dots) in the cells where the next balls will appear.
4. Undo and Reset buttons, within easy reach of the thumb.

**Layout on the two target screens**

- The board takes the full screen width minus small side margins, on both screen shapes.
- **16:9 is the tight case:** the score and preview, the board and the buttons must all fit inside the safe area with no overlap.
- **20:9 is the tall case:** the extra height is split evenly above and below the content, so the board stays in the middle of the screen and nothing sticks to the top.
- Text and buttons keep the same size on both shapes; only the empty space changes.
- Testing: iPhone SE (3rd generation) in the iOS Simulator is exactly 16:9; modern iPhones (19.5:9) are close to 20:9. The Android Emulator gets two custom phones with exactly 1080×1920 and 1080×2400.

**Saving the game in progress (MVP)**

- The game is saved after every move and when the app is closed or goes to the background.
- The save holds the board, the score, the preview (colors and cells), the preview mode and the Undo step, so Undo still works after a restart.
- The saved game is resumed with Continue on the main menu.
- Closing the app during a game keeps it for Continue.
- When a game ends, the score waits on the game-over pop-up and is kept in the save. If the app is closed before Save is tapped, the score goes to the leaderboard under the pre-filled name at the next launch.
- After a game ends, Continue is disabled until a new game starts. Reset also starts a new save.
- The leaderboard (top 10 scores with dates) is saved on the device separately from the game in progress.

## Art

The look is a dark, magical mushroom forest at night: glowing mushrooms on a dark glass board, with soft light and fireflies in the background. (The rest of this document calls the mushrooms "balls".) The browser prototype (`Documentation~/prototype/lines-prototype.html`) is the reference for every detail.

- **Pieces:** 7 glowing mushrooms, the same 7 in every game. Each has its own colour and cap shape, so they stay easy to tell apart even as small preview dots:

  | Colour | Mushroom | Stem |
  | :---- | :---- | :---- |
  | Red | Round cap with pale blue bumps | Blue |
  | Orange | Tall flame-shaped cone | Turquoise |
  | Yellow | Golden cap with drips along the rim | Purple with yellow dots |
  | Green | Pointy hat with a curled tip | Glowing blue |
  | Pink | Flat "jellyfish" cap with a yellow fringe | Pale lilac |
  | Blue | Cone with a curly tip | Red |
  | Purple | Round cap with yellow spots | Blue |

  Every stem contrasts with its cap; each cap is glossy, glows softly in its own colour and has a few tiny sparkles around it. The shapes stay simple because the phone screen is small.
- **Where the art comes from:** the mushrooms and backgrounds are drawn as vector pictures in the prototype and exported from it as PNGs for Unity (mushrooms with a transparent background, 256 px or more). Glossy balls generated in code remain only as a fallback if the images are missing.
- **Backgrounds:** a dim magic forest (misty trees, soft light beams, vines with glowing buds, ferns, fireflies) that fills the whole screen on both 16:9 and 20:9 and never competes with the board. There are two versions:
  - **Start screen:** tall glowing mushrooms on long stems at the sides and smaller ones along the bottom, placed in the free space around the title and buttons.
  - **Game, How to play and Leaderboard:** the same forest without mushrooms, and with nothing glowing near the bottom, so nothing sits behind the board, the buttons or the page dots.
- **Board:** dark purple glass with a soft violet-and-turquoise glow around the edge; each cell glows faintly from inside.
- **Buttons and pop-ups:** dark glass with a warm outline; main buttons (Continue, Yes, Save, Play again) are glowing gold. Text is light cream; the "Lines" title is a glowing gold gradient.
- **Fonts:** Mystery Quest (playful, witchy) for the title, numbers, labels, buttons and headings; it has only Latin letters, so Russian text in those places uses Kurale (a fairy-tale serif with Cyrillic letters, also a free Google Font); Nunito for longer text (How to play explanations, pop-up messages, leaderboard names). Both are free Google Fonts under the SIL Open Font License, so they can be included in the game.
- **App icon:** the red spotted mushroom, large and glowing, on the dark forest with a few fireflies. On Android 8+ it is made of two layers (the forest and the mushroom), so each phone can cut it into its own shape.
- **Splash screen:** while the game loads: the same glowing mushroom and the gold "Lines" title over the plain forest background, for about 2 seconds, with no Unity logo. Before that, iOS shows a plain screen in the game's dark colour.
- One theme (the dark magic forest) in the MVP.
- Preview dots: "ghost" versions of the next mushrooms in their target cells: about half the size of a real one, see-through and a little paler, with no glow, so they read as "a mushroom will grow here", not as real pieces (teacher's feedback; glowing coloured dots, a breathing animation and a magic circle were tried and dropped).
- Game screen life (teacher's feedback, so the board isn't still before the first move): about 12 fireflies (warm gold and soft turquoise) drift slowly behind the board and pulse brighter and dimmer; every mushroom on the board sways gently on its foot (about 3° each way, 3 s per sway, each at its own rhythm, steady across moves; not while selected or moving; previews don't sway). When the game screen opens (New game, Continue, Reset) the board fades in and grows slightly into place, then the mushrooms pop up one after another from top-left to bottom-right, the previews last (about 0.6–1 s in all). All of these are off when the device asks for reduced motion.
- Animations: a moving ball leaves a trail of magic dust (3 tiny sparkles in its colour on each cell it leaves, drifting down and fading in about half a second; teacher's feedback, a glowing comet tail was tried and dropped); when it arrives it pulses once: contracts to about 80%, expands to about 110% and settles, in about 0.4 s (teacher's feedback); the selected ball bounces, balls grow in when they spawn, cleared balls shrink and burst into particles, and a blocked move shakes the ball.
- Sounds and music: in the prototype; in Unity they come with board task L03 (see Later features → Sounds and music).

## Technical summary

The game logic is plain C\# kept separate from Unity, so it can be unit-tested. The Unity layer only draws the board and takes input.

**Code-first approach.** The project has a single, nearly empty `Main` scene. Everything else — the board, the balls, the UI — is created by code when the game starts. No scenes, prefabs or layouts are built by hand in the Unity editor; the editor is used only to look at the result. This lets the whole game be written and changed as code.

| Area | Choice |
| :---- | :---- |
| Engine | Unity 6 (6000.5.6f1), 2D, URP |
| Logic | Plain C\#: `Board`, `Pathfinder` (BFS), `LineDetector`, `Spawner` (seeded RNG, picks colors and cells in advance), `ScoreCalculator` |
| Turn flow | State machine: Idle → Ball selected → Moving → Resolving → Spawning → Check game over |
| Rendering | Pooled ball objects. The mushroom images and the two forest backgrounds are PNGs exported from the browser prototype; the board is generated in code. Animations with a small animation helper written in our own code (no third-party animation library) |
| Input | New Input System, touch (mouse in the editor) |
| UI | uGUI with TextMeshPro, created from code, safe-area aware. Screens: main menu, game, how to play, leaderboard; switched by a small screen manager |
| Saving | `SaveService`: the game in progress as JSON in the game's own save folder (`MicrogameSave.Folder("gc26_lines")`, the team rule), saved after each move and when the app goes to the background. The leaderboard (top 10) is a separate JSON file; its ranking rules live in Core and are unit-tested |
| Testing | EditMode unit tests for all game logic, run from the command line. Visual checks in the iOS Simulator and Android Emulator, then on real phones |
| Builds | iOS: Unity → Xcode project → iOS Simulator, and your own iPhone with a free Apple account (the app must be reinstalled every 7 days). Android: APK installed directly on the emulator and your phone |
| Targets | Portrait 16:9 and 20:9 screens. 60 FPS on older phones. iOS 15+, Android 8.0+ |
| Team project | Lines is one of the GC26 microgames (RULES.md in the repository root): everything lives in `Assets/Microgames/gc26_lines/`, namespaces `GC26.Lines.*`, scene `gc26_lines_Main`. It is built alone as a phone app for now; the link to the shared GC26 menu (`LinesMicrogame`, `microgame.json`) is in place but the "Back to games" button and pause come when all games are merged |

The week-by-week schedule is in the separate 8-week development plan (`Lines_Plan_EN_RU.md`).
