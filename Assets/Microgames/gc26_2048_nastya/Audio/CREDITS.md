# Sound credits: Colours (gc26_2048_nastya)

All sound effects in this folder were **generated from scratch** for this game by
[`Docs~/generate_sfx.py`](../Docs~/generate_sfx.py): sine tones, filtered noise and envelopes.
No recordings, samples or third-party sounds are used, so there is nothing to license or attribute.

| File | Sound | Used for |
| --- | --- | --- |
| `gc26_2048_nastya_Slide.wav` | Short soft swish | A move without merges |
| `gc26_2048_nastya_Merge.wav` | Round pluck (C5); pitch rises with the tile value | A move with merges |
| `gc26_2048_nastya_Invalid.wav` | Low falling bump | A move that changes nothing |
| `gc26_2048_nastya_Win.wav` | Rising arpeggio C–E–G–C | The first 2048 tile |
| `gc26_2048_nastya_Lose.wav` | Falling line G–E–C–A | No more moves |
| `gc26_2048_nastya_Click.wav` | Tiny click | Buttons |

To change a sound, edit the script and run it from the game folder: `python Docs~/generate_sfx.py`.
