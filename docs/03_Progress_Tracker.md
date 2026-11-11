# Minesweeper (Mobile): Progress Tracker

Fill this in during the last 5 minutes of every session.

## Weekly log

| Week | Date | Planned | Done? | Hours | Notes / blockers / next step |
|---|---|---|---|---|---|
| 1 | | Setup, Android toolchain, first phone build | ☐ | | |
| 2 | | Board data model (M1) | ☐ | | |
| 3 | | Sprites, Tile prefab, BoardView, portrait camera | ☐ | | |
| 4 | | Touch input: tap and long press (M2) | ☐ | | |
| 5 | | Flood fill, safe first tap | ☐ | | |
| 6 | | Game states, win/lose, restart | ☐ | | |
| 7 | | Mobile UI: safe area, top bar, flag-mode button (M3) | ☐ | | |
| 8 | | Difficulties, main menu, Back button | ☐ | | |
| 9 | | Pinch zoom, pan, chording, best times, pause | ☐ | | |
| 10 | | Audio, vibration, settings, polish (M4) | ☐ | | |
| 11 | | Unit tests, phone testing, bug fixing | ☐ | | |
| 12 | | Signed Android build, device testing | ☐ | | |
| 13 | | Buffer, README, release (M5) | ☐ | | |

**Total hours:** ___ / 26

## Test devices
| Device | Android version | Screen (resolution, aspect) | Notch? | Tested in week |
|---|---|---|---|---|
| | | | | |
| | | | | |

Device Simulator profiles to check: a 16:9 phone, a tall 20:9 phone with a notch, a 4:3 tablet.

## Manual test checklist (week 11, on the real phone)

### Gameplay
- [ ] The first tap never hits a mine, on all three difficulties
- [ ] The first tap on Beginner usually opens an area
- [ ] Numbers are correct, including at corners and edges
- [ ] Flood fill opens empty regions and stops at numbers
- [ ] Flood fill doesn't open flagged cells
- [ ] Tapping a flagged cell in reveal mode does nothing
- [ ] You can't flag an opened cell
- [ ] Chording works only when the flag count equals the number
- [ ] Chording next to a wrong flag causes a loss
- [ ] On a loss, all mines show, the tapped mine is red, and wrong flags are marked
- [ ] On a win, all mines are auto-flagged
- [ ] The board can't be changed after a win or loss (zoom and pan still work)

### Touch
- [ ] A quick tap always opens the cell under the finger, also near the board edges
- [ ] Long press flags after about 0.4 s, with a vibration
- [ ] In flag mode a tap flags; long press still flags in both modes
- [ ] Dragging pans the view and never opens or flags a cell
- [ ] Pinch zooms in and out and never opens or flags a cell
- [ ] The view can't be panned away from the board, and can't zoom out further than the whole board
- [ ] Expert cells are comfortable to tap when zoomed in
- [ ] The tile under the finger looks pressed and the face shows 😮; both reset on lift or drag
- [ ] Tapping a button never opens the cell behind it

### UI and screens
- [ ] The mine counter goes down and up with flags and can go negative
- [ ] The timer starts on the first reveal, stops at the end, and caps at 999
- [ ] The face changes correctly (normal / pressed / win / lose) and restarts on tap
- [ ] Nothing is hidden behind the notch, camera hole, rounded corners or navigation bar
- [ ] All buttons are easy to hit with a thumb
- [ ] Layout is correct on every device in the Device Simulator list above
- [ ] Rotating the phone doesn't change anything (portrait only)

### Android behaviour
- [ ] Back in the game returns to the menu; Back in the menu closes the app
- [ ] Home button, then return: the round continues and the timer didn't count the time away
- [ ] Incoming phone call or screen lock mid-round: same as above
- [ ] Sound follows the phone's media volume; the Sound setting mutes it
- [ ] The Vibration setting turns vibration off

### Menu and saved data
- [ ] Each difficulty loads the correct size and mine count (Expert = 16×30, 99 mines)
- [ ] The best time is saved only after a win, and only if it's better
- [ ] Best times, Sound and Vibration settings survive closing the app completely
- [ ] Menu → Game → Menu works repeatedly with no errors

### Release build (week 12)
- [ ] The signed APK installs and opens on a phone that isn't connected to Unity
- [ ] The app icon and name look right on the home screen
- [ ] The game runs smoothly (60 fps) and the phone doesn't get noticeably warm
- [ ] No errors in the device log (Android Logcat package in Unity, filtered to the app)
- [ ] The keystore and its passwords are backed up outside the Git repository

## Bug list

| # | Found (week) | Device | Description | Severity (High/Med/Low) | Fixed (week) |
|---|---|---|---|---|---|
| 1 | | | | | |

## Retrospective (week 13)
- **What went well:**
- **What was hard:**
- **What I learned:**
- **What I'd add next:**
