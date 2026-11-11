# Last Bastion: browser prototype

A simple HTML/CSS/JS version of the game in `TowerDefense_GDD_EN_RU.md`. Use it to try the rules, controls and balance before building the Unity version.

## Run it
```
python serve.py
```
This opens http://127.0.0.1:8001. To try it on your phone, run `python serve.py --lan` and open the address it prints on a phone that's on the same Wi-Fi. You can also just open `index.html` directly.

Add `?gold=5000` to the address to start with more gold for testing.

## Screen size
The game has two directions:
- **Horizontal (16:9):** the road goes from left to right, and the castle is on the right.
- **Vertical (9:16):** the same map turned 90°, so the road comes in at the top and the castle is at the bottom. The distances are the same, so the balance is identical.

Where it decides the direction:
- **On a phone** the game fills the whole screen and follows the phone: held sideways it's horizontal, held upright it's vertical. You can turn the phone in the middle of a level without losing anything. Longer phones (like 19.5:9 or 20:9) get extra grass at the ends, so nothing is stretched or cut off. Pressing **Play** switches to full screen where the browser allows it.
- **On a computer** the game is shown inside a phone frame. The bar above it picks **Horizontal** or **Vertical** and the screen shape (**16:9**, **19.5:9** or **20:9**), and remembers your choice.
- `?frame=0` shows the game without the frame on a computer, and `?frame=1` shows the frame on a phone.

## What's in it
- Main menu with **Play** and **How to play** (rules, towers, monsters and buttons, in English or Russian)
- Pause menu with **Resume**, **Restart level** and **Main menu**; the win/lose screen also has **Main menu**
- One level: a winding path, 11 build slots, the castle, and 10 waves
- All 5 towers (Archer, Cannon, Frost, Tesla, Sniper) and all 6 enemies (Grunt, Runner, Brute, Bat, Shaman, Warlord boss) with the stats from the GDD
- Radial menu: tap a slot to build; tap a tower to upgrade (2 tiers), sell (70% refund) or change target mode (First, Last, Strong, Close)
- Range circle for the selected tower; hover a menu button on desktop to preview its range
- Armor, shield, slow, splash (ground only), chain lightning, flying enemies, shaman healing
- Gold per kill, wave-clear bonus (20 + 5 × wave), early-call bonus (seconds left on a 20-second countdown)
- 20 lives, win/lose screen with 1–3 stars, pause (Esc), 1×/2× speed, Space to start a wave

Left out to keep it simple: level select, saving, camera pan and zoom, sounds, tutorial, tower unlocks (all towers are available).

## Tuning
All numbers are at the top of `game.js`: `CONFIG`, `TOWERS`, `ENEMIES`, `WAVES`, `PATH` and `SLOTS`. Edit them and reload the page.

The boss in wave 10 has 35% of the GDD's 2000 HP, like the weaker bosses the GDD plans for the World 1 and 2 finales. In simulated runs, a reasonable mix of towers and upgrades wins with about 12 lives left (2 stars), and two Archers alone lose on wave 4.

## Files
| File | What it does |
|---|---|
| `index.html` | Game area, HUD, radial menu, main menu, How to play (EN/RU), pause and result screens |
| `style.css` | Landscape layout and HUD styling |
| `game.js` | Game data, simulation, drawing and touch handling |
| `serve.py` | Small local web server with caching turned off |
