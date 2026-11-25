# Emo Party — Artwork list

Every picture in the game, the placeholder file it uses now, and what the final art should be.
Paths are relative to this `images/` folder. Style and palette rules are in the [GDD](../../GDD_findepair.md#4-cards-and-illustrations).

**How to replace:** edit the placeholder SVG, or put a PNG with the **same name** next to it (for example `cards/emo_cat.png`). The game uses the PNG first, then the SVG. Refresh the page to see it. No code changes are needed.

## Cards

All card images: **900 × 1200 px (3:4)**. Card faces need a **transparent background**: the game draws the dark card color, the neon frame and the glow around them, so don't draw your own frame.

| # | Title | Placeholder file | Format | Size | Main color | Description |
|---|---|---|---|---|---|---|
| 1 | Emo kitty | `cards/emo_cat.svg` | SVG (PNG to replace) | 900 × 1200 px, transparent | Black + neon pink `#FF2FB3` | Black cat with a side-swept fringe over one eye, a pink streak in the fringe, eyeliner, one green eye showing |
| 2 | Emo bear | `cards/emo_bear.svg` | SVG (PNG to replace) | 900 × 1200 px, transparent | Light pink `#FF7AD9` | Teddy bear in an emo look: side fringe, heart patch, striped scarf |
| 3 | Polaroid | `cards/polaroid.svg` | SVG (PNG to replace) | 900 × 1200 px, transparent | Fuchsia `#FF2FB3` | Instant camera for "from above" selfies, with a photo sliding out that has a heart on it |
| 4 | MP3 player | `cards/mp3_player.svg` | SVG (PNG to replace) | 900 × 1200 px, transparent | Neon cyan `#2DE2E6` | Player with a playlist on its screen and a wired earphone cable bent into a heart |
| 5 | Energy drink | `cards/energy_drink.svg` | SVG (PNG to replace) | 900 × 1200 px, transparent | Neon violet `#9B5CFF` | Energy drink can with an acid-green lightning bolt, **no brand logo** |
| 6 | Bow skull | `cards/bow_skull.svg` | SVG (PNG to replace) | 900 × 1200 px, transparent | White + pink | Cute skull with a big pink bow and heart-shaped eyes |
| 7 | Card back | `cards/card_back.svg` | SVG (PNG to replace) | 900 × 1200 px, **opaque**, fills the whole card | Black + neon pink | Checkerboard with a heart and a lightning bolt in the center. The same on every card, and it must give no hint of the face |

Rules for the 6 faces: each card must be recognizable by **silhouette and main color**, not by small details (on a phone a card is about 3 cm tall). **No names or text** on the cards.

## Backgrounds

Both backgrounds: **900 × 1950 px (9:19.5, portrait phone)**, opaque. They are scaled to cover the screen, so keep important details away from the edges. The game darkens them, so the neon cards and buttons stay readable.

| # | Title | Placeholder file | Format | Size | Used on | Description |
|---|---|---|---|---|---|---|
| 8 | Brick wall | `backgrounds/menu_wall.svg` | SVG (PNG to replace) | 900 × 1950 px | Main menu, settings | Dark brick wall. Final art: neon graffiti (Emo Party lettering, hearts, stars) and posted flyers |
| 9 | Cork board | `backgrounds/board_cork.svg` | SVG (PNG to replace) | 900 × 1950 px | Game board | Cork notice board. Final art: flyers for games and gigs, stickers and Polaroids; the cards look like notes pinned to it |

## Decorations and effects

These used to be drawn only in code (HTML/CSS in `index.html`). Each one now also has its own image file, in `decor/` and `fx/`, matching the prototype's look, so an artist can redraw it. `index.html` still draws them in code: the files are not wired into the prototype yet. All files are transparent SVG (PNG to replace), except the opaque flyers, notes and Polaroid. Text uses Segoe UI, so on a computer without it the text looks slightly different.

| # | Title | File | Size | Used on | Description |
|---|---|---|---|---|---|
| 10 | Card frame | `decor/card_frame.svg` | 900 × 1200 px | Every card | Neon pink rounded border with a soft glow. In the game it's tinted to the card's main color |
| 11 | Push pin | `decor/push_pin.svg` | 128 × 128 px | Every card, top center | Glossy pink pin with a white highlight and a small shadow |
| 12 | Graffiti star | `decor/graffiti_star.svg` | 256 × 256 px | Main menu | Neon cyan `#2DE2E6` star with a glow |
| 13 | Graffiti heart | `decor/graffiti_heart.svg` | 256 × 256 px | Main menu | Neon pink `#FF2FB3` heart with a glow |
| 14 | Graffiti bolt | `decor/graffiti_bolt.svg` | 256 × 256 px | Main menu | Acid green `#39FF88` lightning bolt with a glow |
| 15 | "XOXO 2006" | `decor/graffiti_xoxo_2006.svg` | 1024 × 220 px | Main menu | Violet `#9B5CFF` italic spray-paint lettering |
| 16 | "never grow up" | `decor/graffiti_never_grow_up.svg` | 1024 × 220 px | Main menu | Light pink `#FF7AD9` italic spray-paint lettering |
| 17 | Flyer "Band wanted" | `decor/flyer_band_wanted.svg` | 540 × 380 px | Main menu | Paper flyer with tape: "BAND WANTED / drummer, own fringe a must" |
| 18 | Flyer "Gig" | `decor/flyer_gig.svg` | 540 × 380 px | Main menu | Dark flyer with a violet border: "GIG / Friday, garage #3" |
| 19 | Flyer "Mixtape" | `decor/flyer_mixtape.svg` | 540 × 380 px | Main menu | Paper flyer with tape: "MIXTAPE / will swap CD for a tape" |
| 20 | Note "Memory tournament" | `decor/note_memory_tournament.svg` | 370 × 370 px | Game board | Light pink sticky note with a folded corner |
| 21 | Note "Game of the week" | `decor/note_game_of_the_week.svg` | 370 × 370 px | Game board | Cyan sticky note |
| 22 | Note "MP3 charger" | `decor/note_mp3_charger.svg` | 370 × 370 px | Game board | Acid green sticky note: "Don't forget the MP3 charger" |
| 23 | Polaroid | `decor/polaroid.svg` | 320 × 380 px | Game board | Small Polaroid frame with a violet-to-pink photo; the game shows it half transparent |
| 24 | Logo (splash) | `decor/logo_splash.svg` | 1200 × 420 px | Splash | "EMO PARTY" in white italic with a pink neon glow, tilted −6°. The flicker stays an animation |
| 25 | Logo (menu) | `decor/logo_menu.svg` | 1000 × 700 px | Main menu | "EMO / PARTY" on two lines plus "find the pair" in cyan |
| 26 | Sparkle | `fx/sparkle.svg` | 128 × 128 px | Pair vanish | White four-pointed star with a pink glow |
| 27 | Confetti star | `fx/confetti_star.svg` | 128 × 128 px | Win screen | Acid green star; the game tints confetti in the palette colors |
| 28 | Confetti heart | `fx/confetti_heart.svg` | 128 × 128 px | Win screen | Pink heart |

## Unity

For the Unity version, the same 9 images go to `Assets/Microgames/gc26_findepair/Art/` as PNG 900 × 1200 (cards) — see the [TDD](../../TDD_findepair.md), sections 2 and 9. Unity also needs a sparkle sprite (`Art/FX/sparkle.png`, 64 × 64, four-pointed star with a soft glow), UI buttons and icons, and an app icon; these don't exist yet.
