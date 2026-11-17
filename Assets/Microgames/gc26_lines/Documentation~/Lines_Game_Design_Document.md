# Lines — Game Design Document

Oct 21, 2026 · revised Nov 2, 2026 · updated Nov 14, 2026 to match the finished game (version 0.1.0) · Nov 17, 2026: Pause in Timed mode, the special mushrooms and their tutorials (tasks L14–L19), agreed in the prototype · hmmd2

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
- Fast, clean controls: two taps per move. A relaxed Classic game with no timer, and a Timed mode for a faster one.
- A little forest magic on top of the classic rules: a rare rainbow mushroom, frozen mushrooms and a puffball bomb.
- The game in progress is saved automatically and picks up where the player left off.
- A dark, magical mushroom forest: 7 glowing mushrooms instead of plain balls, on a dark glass board.

**Custom changes compared to the original**

- **Undo:** take back the last move (one step).
- **Reset:** start a new game from the game screen at any time.
- **Blocked-move alert:** when a ball can't reach the tapped cell, the player sees a message instead of nothing happening.
- **Timed mode** (task L09): a second way to play, against the clock (see Timed mode).
- **Special mushrooms** (tasks L14–L16): a rainbow mushroom that fits any line, frozen mushrooms that can't be moved, and a puffball bomb earned with points (see Special mushrooms), each introduced on the board the first time it appears (task L17).

## Core gameplay and rules

Each turn the player makes one move. A move that completes a line removes it and costs nothing. A move that doesn't brings 3 new balls onto the board.

![Turn loop](Lines_turn_loop.png)

Every turn ends either with a cleared line, which skips the spawn, or with 3 new balls arriving. The game ends when no ball can move any more: usually because a spawn filled the board, or (rarely) because the only free cells are walled in by frozen mushrooms. With a puffball in stock, the game first offers it as a rescue (see Special mushrooms).

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
| Game over | No ball can move: no empty cells are left, or every free cell is walled in by frozen mushrooms (then the game-over pop-up says "No moves left") |
| Special mushrooms | Rainbow (fits any line), frozen (can't be moved) and the puffball bomb: see Special mushrooms |

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
| Tap a frozen mushroom | It shakes with a small ice crack, and "Frozen — can't move" appears with "Make a line through it to break the ice" under it. A ball already selected stays selected |
| Puffball button | See Special mushrooms → Puffball bomb |
| Undo button | Restores the board, the score and the preview to how they were before the last move. Only one step, as in the classic game: after an undo, the button is disabled until the next move. Also disabled at the start of a game |
| Reset button | Asks for confirmation, then clears the board and starts a new game |

Input is ignored while a move, clear or spawn animation is playing.

- Cells are at least 9 mm wide on a 6-inch phone, so the board fills the screen width.
- *Later:* a movement effect closer to the original, where the ball visibly steps from cell to cell along its path.
- **Vibration** (task L02; always together with a sound or picture, and off with the Vibration switch in Settings). Only moments that matter vibrate, so the player keeps noticing it:

  | Moment | Feel |
  | :---- | :---- |
  | Line cleared | A firm tap; a stronger one for a line of 7 or more; two taps (about 0.12 s apart) when one move clears two or more lines |
  | No path | Two quick soft knocks: the phone's own "error" feel |
  | New record (the score makes the top 10) | The phone's own "success" feel |
  | Game over | One heavy thump |
  | Vibration switched on in Settings | One light tick, so the player feels what they turned on |
  | Tap on a frozen mushroom | One light tick |
  | A puffball earned | A "reward" double tap |
  | The puffball button (getting ready to aim) | One light tick |
  | A puffball bursts | One heavy thump |

  Nothing vibrates for the cells of a move, selecting, new mushrooms or button taps. The game uses the phone's own light, firm and heavy feels (iPhone haptics; on Android the matching vibration effects), which work on the iPhone even with the silent switch on. In the browser prototype only Android phones can vibrate.

## Scoring

Longer lines are worth much more than short ones, which rewards the player for taking risks. The values below were re-checked in task 38 and kept as they are.

| Balls cleared in one line | Points |
| :---- | :---- |
| 5 | 10 |
| 6 | 14 |
| 7 | 20 |
| 8 | 28 |
| 9 | 36 |

- **Several lines in one move:** the total for the move is multiplied by the number of lines (two lines \= ×2).
- **Rainbow mushrooms** count in a line's length like any other ball. A rainbow that finishes two lines at once counts in both, like a crossing ball.
- **A puffball burst** scores nothing.
- **Lines completed by spawned balls:** score normally, with no multiplier.
- **Undo:** removes the points scored by the move it takes back.
- **Leaderboard (MVP):** a local top 10 of finished games, each with a player name, score and date (see Screens and UI).
  - Only finished games count: a game abandoned with Reset or New game never reaches the leaderboard.
  - A game that ends with 0 points never goes on the leaderboard: the game-over pop-up then has no name field, only "Clear a line to get on the leaderboard." and two buttons, Menu and Play again.
  - **Ties:** the older score keeps its place. A new score must be higher than the 10th place to get in.
  - After saving, the leaderboard says "New best score, Anna!", "You're #3, Anna!" or "120 points didn't make the top 10".
- **Passing the best score during play** (task L04): the best is the top of the leaderboard (finished games only). When the score of the game in progress passes it, fireworks burst over the game screen (about 1.5 s of gold, turquoise, pink and lilac sparks), a glowing "New best!" appears in the middle of the board, and the "New record" sound and the "success" vibration play. Then a small glowing gold crown sits next to the score number, its middle level with the middle of the digits, for the rest of the game. It happens once per game; if Undo takes the score back below the best, the crown quietly disappears. A continued game that is already above the best shows the crown without fireworks. With no finished games yet there is no best, so nothing happens. (A plain "Best 412" number next to the score was tried and dropped.) If the same move also earns a puffball, "New best!" and the fireworks play first, and the puffball's message or tutorial follows about 2 seconds later, so they never cover each other.

## Preview modes

The original game had three difficulty levels that differ only in how much the player knows about the next balls: Full preview (the colors of the next 3 balls and the cells where they will appear), Colors only, and No preview. Lines has **only Full preview**. The other two were planned as a later feature (L03) and dropped on Nov 14, 2026: scores from different modes can't be compared, so they would need separate leaderboards, and the work across every screen was too much for what they add. The full preview is what makes this version friendly.

## Timed mode (task L09)

A second, faster way to play next to the normal game (called **Classic** from now on). Same board, same rules, same points; the difference is a clock.

| Rule | Value |
| :---- | :---- |
| Time | 3:00 at the start. Every cleared line adds 5 seconds (two lines in one move: +10 s; lines made by new mushrooms count too) |
| End | When the clock reaches 0:00 ("Time's up!"), or as usual when the board is full |
| What counts | Only the player's thinking time: the clock stops while a mushroom moves, lines clear, new mushrooms appear or a puffball bursts, while a tutorial is showing on the board, and whenever the game screen isn't in front (menu, Settings, Pause, the Reset question, How to play) or the app is in the background |
| Undo | None (it would give free thinking time). **Pause** takes Undo's place in the bottom row: Pause, the puffball, Reset |
| Pause (task L18) | Stops the clock and opens a "Paused" card ("The clock is stopped and the board is hidden.") with Resume. While paused, the board and NEXT are hidden, so a pause gives no free thinking time. Settings hides them the same way in Timed mode, and coming back from another app the game is paused |
| Points | The same table as Classic |

**Choosing the mode.** Two round badges sit on the main menu right above Continue: a mushroom (**Classic**) and a stopwatch (**Timed**), each with its name under it; the chosen one glows gold. The choice is remembered. Continue, New game and Leaderboard all follow the chosen mode.

**Each mode has its own game in progress.** A Classic game and a Timed game are saved separately and never replace each other. Leaving a timed game or closing the app stops its clock; Continue (with Timed chosen) picks it up with the time that was left, and shows it next to the score with the clock's stopwatch icon ("· score 40 · ⏱ 1:20"), so it reads as time, all on one line.

**The clock** sits at the top of the game screen, centred between the house and the gear (above SCORE and NEXT), inside the safe area, so the iPhone's camera cut-out never covers it: a stopwatch icon and the time left as m:ss in the score's font. "+5s" (or "+10s") floats up to its right when time is added. From 0:30 it turns yellow and ticks softly each second, its digits giving a small pulse; from 0:10 it turns red and the tick is sharper. At 0:00 a glowing gold "Time's up!" shows over the board, and a moment later the game-over pop-up opens as in Classic.

**Leaderboard.** The same two badges, smaller and side by side next to the house, are the Leaderboard's tabs: **Classic** and **Timed**, each a top 10 with the same rules (finished games only, older scores win ties). It opens on the tab of the game just finished, or of the mode chosen on the menu. The "New best" celebration (fireworks and the crown, see Scoring) compares with the best of the mode being played.

**How to play** has a card "Race the clock" (the last one): "Choose Timed on the menu to play against the clock: 3 minutes, and every cleared line adds 5 seconds. The clock stops while mushrooms move."

The special mushrooms work the same in both modes.

## Special mushrooms (tasks L14–L16)

Three bits of forest magic on top of the classic rules, the same in Classic and Timed. Their numbers were checked with a robot player (250 games per setting, about 170–200 points a game).

### Rainbow mushroom (task L14)

| Rule | Value |
| :---- | :---- |
| What it does | Fits any line: it counts as every colour at once, so four green balls and a rainbow make a line (`G G 🌈 G G`); one rainbow can even finish two lines of different colours in one move (each line scores, and the two-line ×2 applies) |
| How often | Each new mushroom has a 1 in 40 chance of being a rainbow (about one every 12–15 turns) |
| Not at the start | The first NEXT of a new game (New game or Reset) never has a rainbow: the player is still getting used to the board |
| Other | It moves like any mushroom, shows in NEXT and as a ghost like the others, and is never frozen |
| Look | A rainbow-striped cap (red, orange, yellow, green, cyan, blue, violet) with a white star and a glossy shine, a white stem and a white glow. Its sparkles when it clears are rainbow-coloured |

### Frozen mushrooms (task L15)

| Rule | Value |
| :---- | :---- |
| What it does | Arrives frozen: it can't be selected or moved, but it still counts for its colour. A line through it clears it with the line, and the ice shatters |
| How often | From 100 points, each new mushroom has a 1 in 20 chance of arriving frozen; never more than 4 frozen mushrooms on the board at once. (The robot lost only about 3% of its score to ice: lines, rainbows and puffballs keep it under control) |
| Shown in advance | NEXT and the ghost on the board show the ice too |
| Tapping one | A shake, a small ice crack and "Frozen — can't move" / "Make a line through it to break the ice" (see Controls) |
| Look | The mushroom sits in a frosted ice cube: an uneven pale-blue outline with a frosty white edge, cut faces, a crack, a few snowflake sparkles and a soft blue glow; the mushroom shows through a little less colourful, and it doesn't sway. When it breaks, pale ice shards fly out |

### Puffball bomb (task L16)

| Rule | Value |
| :---- | :---- |
| Earning | One puffball every 200 points; up to 2 can be kept |
| A full pocket | With 2 in stock the ring stays full and nothing more is earned; points scored meanwhile don't count. After one is used, counting starts again from the current score (the next comes 200 points later). Nothing is ever silently lost |
| The button | A round button in the middle of the bottom row (Classic: Undo, puffball, Reset; Timed: Pause, puffball, Reset), showing a cream puffball with a fuse. A gold ring around it fills towards the next puffball; a small gold number shows how many are ready, and the fuse sparkles while one is |
| Not ready yet | Tapping it shows a bubble above it: "Puffball in 40 points", "You get one every 200 points and can keep 2", and a gold progress bar |
| Earned | The button pops, a gold banner over the board says "A puffball for you!" / "Tap it below the board to use it" (the first one of a game shows the tutorial instead, see Tutorials on the board) |
| Aiming | Tap the button: it pulses gold, and a gold dashed 3×3 square lights up on the spot with the most mushrooms; the mushrooms in it glow and tremble. Tap another cell to move the square there (sliding a finger works too); tap the lit spot to burst. Tap the button again to cancel. A bubble says "Where to burst? Tap another cell to move it, tap the lit spot to burst. The puffball button cancels." |
| The burst | The 3×3 square around the lit cell bursts in a cloud of golden-green spores, frozen mushrooms included. It scores nothing and is **not a move**: like a cleared line, no new mushrooms come after it and NEXT stays the same. Undo can take it back (and gives the puffball back). An empty square can't be burst ("Nothing to burst here") |
| Rescue | When no ball can move (the board is full, or the free cells are walled in by ice) and a puffball is in stock, the game doesn't end: the screen dims, a hand points at the puffball, "The board is full!" (or "No moves left!") / "Use your puffball to keep going", with an End game button. Cancelling the aim brings the offer back |
| In Pause or Settings | Pause while aiming cancels the aim and hides its bubble |

**How to play** has a card for each: "Rainbow mushroom", "Frozen mushrooms" and "Puffball bomb", after "Watch what's coming" and before "Race the clock". The order is: move a piece; make lines of five; watch what's coming; rainbow mushroom; frozen mushrooms; puffball bomb; race the clock. The puffball card: "Every 200 points earn a puffball (you can keep 2). Tap it below the board: a 3×3 square lights up. Tap another cell to move it, and tap the lit spot to burst. No new mushrooms come after a burst."

## Tutorials on the board (task L17)

The first time each special mushroom appears **in a game**, the game introduces it right on the board (coach marks): the screen dims except one spot with a gold outline, an animated hand taps on it, and a short caption explains. Each one shows once per game: Continue doesn't repeat them (they are saved with the game), but a new game or Reset shows them again. The clock stops while one is showing.

| When | What |
| :---- | :---- |
| The first rainbow of a game (in NEXT, or already on the board) | Its spot lights up: "A rainbow mushroom: it fits any line", with Got it |
| The first frozen mushroom of a game | Its spot lights up: "Frozen: it can't move. Make a line through it to break the ice", with Got it |
| The first puffball of a game | 1) The hand points at the puffball button: "You earned a puffball!" / "Tap it to use it". 2) The whole board lights up with the burst already on the busiest spot: "Where to burst?" / "Tap another cell to move it, tap the lit spot to burst". Taps elsewhere do nothing; both steps have Skip (the puffball stays for later) |

When celebrations meet (see Scoring), "New best!" comes first and the tutorial follows.

## Later features (not in the MVP)

These are planned for after the MVP and are not scheduled in the 8 weeks unless time is left over.

| Feature | Notes |
| :---- | :---- |
| Pause menu | Not needed: nothing happens until the player acts |
| More settings | A theme choice (Settings itself is now in the MVP) |
| How to play on first launch | Open the How to play cards automatically the first time the app starts |
| More themes | The MVP has one theme: the dark magic forest |
| Custom art packs | Replace the MVP mushrooms (and board) with other art |
| Timed mode | *Done (task L09), see Timed mode.* (A Daily Challenge, the same seed for every player that day with one attempt, was dropped on Nov 15, 2026: with no server, daily scores can't be compared between players, and it would need its own results list, streak and save.) |
| Achievements and statistics | *Dropped on Nov 16, 2026 (task L10):* the leaderboards and the New best crown already give the player goals, and a new screen, menu icon, save file and unlock pop-ups would add little. The idea was: first 7-ball line, two lines in one move, 1,000 points in one game; statistics: games played, best score, longest line, average score |
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
| The clock's last 30 seconds (Timed) | A soft tick each second; sharper from 0:10 |
| A rainbow mushroom arrives | Shimmer: a quick rising run of seven bells |
| A frozen mushroom arrives | A soft frosty chime with a breath of air |
| Tap on a frozen mushroom | A small ice crack |
| A line breaks the ice | A glassy shatter on top of the harp |
| A puffball earned | A rising bell arpeggio |
| Puffball button: ready to aim / cancelled | A rising swoosh / a falling swoosh |
| A puffball bursts | A deep boom with a puff of air and three bells |
| A tutorial on the board appears | A two-note chime |

**Music:** "Firefly dance", a calm endless loop: soft warm chords (D, Bm, G, A, about 6 s each), a low note at each chord change and a gentle kalimba pattern on top, slightly varied each time. It plays on every screen while Music is on, and fades in and out when switched. Music and Sounds each have an on/off switch in Settings. In a browser, sound can start only after the first tap.

## Screens and UI (MVP)

The game has four screens and four pop-ups (Pause, only in Timed mode, is the fourth; it's described in Timed mode). The app opens on the main menu.

| Screen | Contents | Opens from |
| :---- | :---- | :---- |
| Main menu | Game title, the Classic / Timed badges, Continue (with the saved game's score), New game, How to play, Leaderboard; a gear icon (Settings) in the top-right corner | App launch; back buttons |
| Game | House button (main menu) top-left and gear (Settings) top-right, score, preview of the next 3 balls, board, and the bottom row: Undo (Pause in Timed), the round puffball button, Reset. In Timed the clock sits at the top between the house and the gear. The SCORE and NEXT labels share one line, and the score number sits on the same line as the 3 next mushrooms (the middle of its digits level with their middle) | Continue or New game |
| How to play | House button (a back arrow when opened from the game, see below), then 7 short cards with a small board picture each: move a ball along a free path; make lines of five; the next balls, game over and Undo; the rainbow mushroom; frozen mushrooms; the puffball bomb; race the clock (Timed). Back / Next, and "Got it" on the last card; a swipe left or right also changes the card (the card follows the finger, slides out and the next one slides in; a short swipe springs back; the first and last card only stretch a little, and a swipe never closes How to play). The buttons use the same slide. The picture and title stay in the same place on every card | Main menu |
| Leaderboard | House button and the Classic / Timed tabs, then the local top 10 finished games, each with rank, name, date and score; the game just finished is highlighted, with a result line above the list and a Play again button below it. Shows a short explanation while it's empty | Main menu; Save on the game-over pop-up |
| New game confirmation | "Start a new game?" with Yes and Cancel; the current game is lost | Reset button, or New game on the menu while a game is unfinished |
| Settings | A pop-up card with on/off switches for Music, Sounds and Vibration, the Language (English / Русский), and a How to play button. Closes with ✕ or a tap outside the card | The gear on the main menu or the game screen |
| Game over | Final score and a name field with Save, nothing else (no label above the field; it's still named for screen readers). When the game ended because the free cells are walled in by ice, the line above the score says "No moves left. Final score". Save adds the score to the leaderboard and opens the Leaderboard screen | Game, when no ball can move (or Time's up) |

**Main menu**

- **Continue** resumes the saved game and shows its score. It is disabled when there's no unfinished game.
- **New game** starts right away when there's no unfinished game; otherwise it asks for confirmation first.
- The game screen, How to play and Leaderboard all have the same house icon in the top-left corner (just the white icon, no ring or disc behind it). It returns to the main menu, except How to play opened from Settings during a game: there the button is a back arrow (←, "Back to the game") and returns to the game, so the icon always says where it goes (task L19). Leaving the game saves it, so Continue picks it up exactly where it was (Undo included).
- How to play and Leaderboard have no title at the top, only the house button.
- On shorter screens (16:9) the leaderboard rows get a little shorter, so all 10 places fit without scrolling when it's opened from the menu. Right after a game the result line and Play again take some room, so the list may scroll a little; it opens on the game just finished.

**Settings** (teacher's feedback, pulled forward from the later features)

- The gear is a plain white icon like the house (no ring or disc). The pop-up is the same dark card as the other pop-ups; the game stays visible behind it (in Timed mode the board and NEXT are hidden behind it, like in Pause, since the clock is stopped).
- **Music, Sounds, Vibration:** on/off switches (gold when on), all on by default, remembered on the device. Music and Sounds switch the game's sounds and music (see Sounds and music) on and off at once; Vibration switches the vibration (see Controls and feedback).
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
4. Undo (Pause in Timed), the round puffball button and Reset, within easy reach of the thumb.

**Layout on the two target screens**

- The board takes the full screen width minus small side margins, on both screen shapes.
- **16:9 is the tight case:** the score and preview, the board and the buttons must all fit inside the safe area with no overlap.
- **20:9 is the tall case:** the extra height is split evenly above and below the content, so the board stays in the middle of the screen and nothing sticks to the top.
- Text and buttons keep the same size on both shapes; only the empty space changes.
- Testing: iPhone SE (3rd generation) in the iOS Simulator is exactly 16:9; modern iPhones (19.5:9) are close to 20:9. The Android Emulator gets two custom phones with exactly 1080×1920 and 1080×2400.

**Saving the game in progress (MVP)**

- The game is saved after every move and when the app is closed or goes to the background.
- The save holds the board, the score, the preview (colors and cells) and the Undo step, so Undo still works after a restart. It also holds which balls are frozen (on the board and in NEXT), the puffballs in stock and the score of the next one, a rescue waiting for the player, and which tutorials this game has already shown. Older saves without these load as a game with no specials.
- The saved game is resumed with Continue on the main menu.
- Closing the app during a game keeps it for Continue.
- When a game ends, the score waits on the game-over pop-up and is kept in the save. If the app is closed before Save is tapped, the score goes to the leaderboard under the pre-filled name at the next launch.
- After a game ends, Continue is disabled until a new game starts. Reset also starts a new save.
- The leaderboards (top 10 scores with dates, one for each mode) are saved on the device separately from the games in progress (one for each mode, see Timed mode).

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
- **Special pieces** (see Special mushrooms): the rainbow mushroom (an 8th mushroom picture), the frosted ice cube drawn over frozen mushrooms, and the puffball bomb (the button's picture, also shown in How to play and the banner). The burst is a flash and a cloud of golden-green spores; breaking ice throws pale shards.
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
- Game screen life (teacher's feedback, so the board isn't still before the first move): about 12 fireflies (warm gold and soft turquoise) drift slowly behind the board and pulse brighter and dimmer; every mushroom on the board sways gently on its foot (about 3° each way, 3 s per sway, each at its own rhythm, steady across moves; not while selected or moving; previews don't sway). When the game screen opens (New game, Continue, Reset) the board fades in and grows slightly into place, then the mushrooms pop up one after another from top-left to bottom-right, the previews last (about 0.6–1 s in all). In the browser prototype all of these are off when the device asks for reduced motion; the Unity game doesn't read that phone setting.
- Animations: a moving ball leaves a trail of magic dust (3 tiny sparkles in its colour on each cell it leaves, drifting down and fading in about half a second; teacher's feedback, a glowing comet tail was tried and dropped); when it arrives it pulses once: contracts to about 80%, expands to about 110% and settles, in about 0.4 s (teacher's feedback); the selected ball bounces, balls grow in when they spawn, cleared balls shrink and burst into particles, and a blocked move shakes the ball.
- Sounds and music: see Sounds and music. In the game they are made from code when it starts (no sound files), the same recipes as the prototype.

## Technical summary

The game logic is plain C\# kept separate from Unity, so it can be unit-tested. The Unity layer only draws the board and takes input.

**Code-first approach.** The project has a single, nearly empty scene, `gc26_lines_Main` (a camera and the `GameBootstrap` object). Everything else — the board, the balls, the UI — is created by code when the game starts. No scenes, prefabs or layouts are built by hand in the Unity editor; the editor is used only to look at the result. This lets the whole game be written and changed as code.

| Area | Choice |
| :---- | :---- |
| Engine | Unity 6 (6000.5.6f1), 2D, URP |
| Logic | Plain C\#: `Board`, `Pathfinder` (BFS), `LineDetector`, `Spawner` (seeded RNG, picks colors and cells in advance), `ScoreCalculator`, `GameState` (one turn, Undo), `Leaderboard` |
| Turn flow | State machine in `LinesGameController`: Idle → Ball selected → Moving → Resolving → Spawning → Check game over; `BoardPresenter` plays the animations and sounds |
| Rendering | Pooled ball objects. The mushroom images and the two forest backgrounds are PNGs exported from the browser prototype; the board is generated in code. Animations with a small animation helper written in our own code (no third-party animation library) |
| Input | New Input System, touch (mouse in the editor) |
| UI | uGUI with TextMeshPro, created from code, safe-area aware. Screens: main menu, game, How to play, leaderboard; pop-ups: Settings, new game confirmation, game over, and the "No path" alert. `GameBootstrap` creates them and switches between them. Every text is in `Texts` (English and Russian) |
| Sound | Music and sounds made from code when the game starts (`Synth`, `SoundBank`, `LinesAudio`), no sound files |
| Saving | `SaveService`: the game in progress as JSON in the game's own save folder (`MicrogameSave.Folder("gc26_lines")`, the team rule), saved after each move and when the app goes to the background. The leaderboard (top 10, `LeaderboardService`) and the settings (`SettingsService`) are separate JSON files; the ranking rules live in Core and are unit-tested |
| Testing | EditMode unit tests for all game logic and the views, and play-mode tests in the real scene (including a robot that plays whole games and checks the screen against the board after every turn and that every-frame code makes no garbage), all run from the command line. Layout screenshots at 16:9, 20:9 and real iPhone sizes. Then the iOS Simulator, the Android Emulator and real phones (an iPhone 14 Pro and an Android phone, both at 60 FPS) |
| Builds | iOS: Unity → Xcode project → iOS Simulator, and your own iPhone with a free Apple account (the app must be reinstalled every 7 days). Android: APK installed directly on the emulator and your phone |
| Targets | Portrait 16:9 and 20:9 screens. 60 FPS on older phones. iOS 15+, Android 8.0+ |
| Team project | Lines is one of the GC26 microgames (RULES.md in the repository root): everything lives in `Assets/Microgames/gc26_lines/`, namespaces `GC26.Lines.*`, scene `gc26_lines_Main`. It is built alone as a phone app for now; the link to the shared GC26 menu (`LinesMicrogame`, `microgame.json`) is in place but the "Back to games" button and pause come when all games are merged |

The week-by-week schedule is in the separate 8-week development plan (`Lines_Plan_EN_RU.md`).
