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

## Not image files (drawn in code)

These are made with HTML/CSS in `index.html`, not files. They stay as they are unless you ask for them to become images too.

| Title | Where | Description |
|---|---|---|
| Card frame, glow, push pin | Every card | Neon border in the card's main color and a pink pin at the top |
| Graffiti and flyers | Main menu | ★ ♥ ⚡, "XOXO 2006", "never grow up", 3 paper flyers |
| Notes and Polaroids | Game board | 3 sticky notes and 2 small Polaroid frames |
| Logo | Splash, main menu | Flickering neon "EMO PARTY" text |
| Sparkles and confetti | Pair vanish, win screen | ✦ ★ ♥ symbols with a pink glow |

## Unity

For the Unity version, the same 9 images go to `Assets/Microgames/gc26_findepair/Art/` as PNG 900 × 1200 (cards) — see the [TDD](../../TDD_findepair.md), sections 2 and 9. Unity also needs a sparkle sprite (`Art/FX/sparkle.png`, 64 × 64, four-pointed star with a soft glow), UI buttons and icons, and an app icon; these don't exist yet.
