# gc26_findepair — Emo Party

- Branch: `game/gc26_findepair`
- Namespace: `GC26.Findepair`
- Entry scene: `Scenes/gc26_findepair_Main.unity` (one object, `Findepair`, with `FindepairGame`)
- Team:

How to play: tap two cards to turn them over. Matching pictures burst into pink sparkles and vanish after 1 second; otherwise they turn back. Find all 6 pairs in as few moves as you can.

## Running

Open `Scenes/gc26_findepair_Main.unity` and press Play: the game starts on its own (splash → menu). Inside the GC26 shell it starts when the shell calls `Begin`, and **Exit** returns to the shell with the score (stars of the best round, 0–3).

## Layout

| Folder | What |
|---|---|
| `Scripts/` | Runtime code. The screens are built from code at startup by `FindepairUI` (sizes in 1080 × 1920 reference pixels). |
| `Data/Cards/` | 6 `CardData` assets: id, name, face sprite, frame color. |
| `Art/Cards`, `Art/Backgrounds` | Card faces and back (900 × 1200), menu and board backgrounds (900 × 1950). Exported from `docs/prototype_findepair/images/`. |
| `Art/FX`, `Art/UI` | Sparkle, flash, heart, star, bolt; white UI shapes (rounded panel, frame, glow, circle) tinted in code. |
| `Editor/` | **GC26 → Findepair → Rebuild Scene and Assets**: import settings, CardData assets, scene. |
| `Tests/` | Rule tests and Play Mode tests that play the real scene. |

## Replacing art

Overwrite a PNG in `Art/` with the same name and size, then run **GC26 → Findepair → Rebuild Scene and Assets**. The full list of pictures is in `docs/prototype_findepair/images/ARTWORK.md`.

## Tests

Window → General → Test Runner → PlayMode → Run All. With the environment variable `FINDEPAIR_SHOTS` set to a folder, the Play Mode tests also save screenshots there.
