"""Makes the other tanks from the two hand-made drawings, so every tank keeps their pixel style.

usage: build_variants.py <folder with player.png, enemy.png> <unity output folder> [<browser output folder>]
Needs Pillow and numpy. Writes one 512x512 sprite per variant next to the originals;
build_models.py then raises each of them into a model (their parts are in VARIANT_PARTS below).

Enemies come from enemy.png (green), the player's tanks from player.png (grey camo with glowing lines).
The art is drawn in blocks of PX pixels with a black outline, like the originals.
"""
import colorsys
import os
import sys

import numpy as np
from PIL import Image

PX = 4
INK = (16, 14, 18, 255)


# ---------------------------------------------------------------- pixel helpers

def arr(image):
    return np.array(image.convert("RGBA")).astype(np.float32)


def img(a):
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def hsv(a):
    """Hue (0..1), saturation and value of every pixel."""
    rgb = a[..., :3] / 255.0
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    rc = np.where(m, (mx - r) / np.where(m, d, 1), 0)
    gc = np.where(m, (mx - g) / np.where(m, d, 1), 0)
    bc = np.where(m, (mx - b) / np.where(m, d, 1), 0)
    h = np.where(r == mx, bc - gc, np.where(g == mx, 2.0 + rc - bc, 4.0 + gc - rc))
    h = (h / 6.0) % 1.0
    s = np.where(mx > 0, d / np.where(mx > 0, mx, 1), 0)
    return h, s, mx


def from_hsv(h, s, v):
    i = np.floor(h * 6).astype(int) % 6
    f = h * 6 - np.floor(h * 6)
    p = v * (1 - s)
    q = v * (1 - s * f)
    t = v * (1 - s * (1 - f))
    r = np.choose(i, [v, q, p, p, t, v])
    g = np.choose(i, [t, v, v, q, p, p])
    b = np.choose(i, [p, p, t, v, v, q])
    return np.stack([r, g, b], -1) * 255.0


def paint_body(a, colour, mask, keep_dark=0.16, strength=1.0):
    """Recolours the painted metal: keeps each pixel's brightness, swaps its colour.
    Very dark pixels (outlines, tracks' gaps) keep their own colour."""
    h, s, v = hsv(a)
    lum = (a[..., 0] * 0.3 + a[..., 1] * 0.59 + a[..., 2] * 0.11) / 255.0
    target = np.array(colour, np.float32) / 255.0
    t_lum = target @ np.array([0.3, 0.59, 0.11], np.float32)
    new = np.clip(target[None, None, :] * (lum / max(t_lum, 1e-3))[..., None] * 0.9, 0, 1) * 255.0
    use = mask & (a[..., 3] > 0) & (v > keep_dark)
    w = strength
    out = a.copy()
    out[..., :3] = np.where(use[..., None], a[..., :3] * (1 - w) + new * w, a[..., :3])
    return out


def glow_mask(a):
    """The cyan glowing lines of the player's drawing."""
    h, s, v = hsv(a)
    return (a[..., 3] > 0) & (s > 0.35) & (v > 0.45) & (h > 0.42) & (h < 0.56)


def recolour_glow(a, hue, sat=1.0):
    h, s, v = hsv(a)
    m = glow_mask(a)
    rgb = from_hsv(np.full_like(h, hue), np.clip(s * sat, 0, 1), np.clip(v * 1.05, 0, 1))
    out = a.copy()
    out[..., :3] = np.where(m[..., None], rgb, a[..., :3])
    return out


def region(x0, y0, x1, y1, shape=(512, 512)):
    m = np.zeros(shape, bool)
    m[y0:y1, x0:x1] = True
    return m


def erase(a, x0, y0, x1, y1):
    a[y0:y1, x0:x1, 3] = 0
    return a


def remove_barrel(a, x0, x1, hull_top, turret_top, dx=40):
    """Takes the drawn barrel away: above the hull it becomes empty, over the deck it is
    painted over with the deck next to it, so no hole is left."""
    a[0:hull_top, x0:x1, 3] = 0
    a[hull_top:turret_top, x0:x1] = a[hull_top:turret_top, x0 + dx:x1 + dx]
    return a


def fill(a, x0, y0, x1, y1, colour, outline=True):
    """A pixel-art block: a filled box on the PX grid with a black rim."""
    x0, y0, x1, y1 = (int(round(c / PX)) * PX for c in (x0, y0, x1, y1))
    if outline:
        a[y0:y1, x0:x1] = INK
        x0, y0, x1, y1 = x0 + PX, y0 + PX, x1 - PX, y1 - PX
    if x1 > x0 and y1 > y0:
        a[y0:y1, x0:x1] = list(colour) + [255]
    return a


def shade(colour, k):
    return tuple(int(max(0, min(255, c * k))) for c in colour)


def plate(a, x0, y0, x1, y1, colour, rivets=True):
    """An armour plate: lit top edge, dark bottom edge, rivets in the corners."""
    fill(a, x0, y0, x1, y1, colour)
    fill(a, x0 + PX, y0 + PX, x1 - PX, y0 + 2 * PX, shade(colour, 1.35), outline=False)
    fill(a, x0 + PX, y1 - 2 * PX, x1 - PX, y1 - PX, shade(colour, 0.65), outline=False)
    if rivets:
        for x, y in ((x0 + 2 * PX, y0 + 3 * PX), (x1 - 3 * PX, y0 + 3 * PX), (x0 + 2 * PX, y1 - 4 * PX), (x1 - 3 * PX, y1 - 4 * PX)):
            fill(a, x, y, x + PX, y + PX, shade(colour, 0.5), outline=False)
    return a


def barrel(a, cx, y0, y1, width, colour, muzzle=True):
    """A gun barrel pointing up (to y0), with a lit stripe and a wider muzzle."""
    half = width // 2
    fill(a, cx - half, y0, cx + half, y1, colour)
    fill(a, cx - half + PX, y0 + PX, cx - half + 2 * PX, y1 - PX, shade(colour, 1.4), outline=False)
    if muzzle:
        fill(a, cx - half - PX, y0, cx + half + PX, y0 + 4 * PX, shade(colour, 0.9))
    return a


def disc(a, cx, cy, r, colour, rim=INK):
    yy, xx = np.mgrid[0:512, 0:512]
    # snapped to the pixel grid, so the circle looks drawn, not smooth
    sx = (xx // PX) * PX + PX / 2
    sy = (yy // PX) * PX + PX / 2
    d = (sx - cx) ** 2 + (sy - cy) ** 2
    a[d <= r * r] = rim
    a[d <= (r - PX) ** 2] = list(colour) + [255]
    return a


def hazard(a, x0, y0, x1, y1, c1, c2):
    """Diagonal warning stripes."""
    for y in range(y0, y1, PX):
        for x in range(x0, x1, PX):
            c = c1 if ((x + y) // (PX * 3)) % 2 == 0 else c2
            a[y:y + PX, x:x + PX] = list(c) + [255]
    a[y0:y0 + PX, x0:x1] = INK
    a[y1 - PX:y1, x0:x1] = INK
    return a


# ---------------------------------------------------------------- enemies (from enemy.png)
# enemy.png: hull x 160-352, y 60-448, deck from y 96; turret octagon 192-322 x 174-318; barrel 247-266 x 0-150.

ENEMY_BARREL = (244, 272, 96, 176)


def enemy_scout(src):
    a = paint_body(arr(src), (236, 200, 84), np.ones((512, 512), bool))
    remove_barrel(a, *ENEMY_BARREL)
    barrel(a, 257, 64, 156, 20, (120, 104, 64))
    # light rails on the sides, for speed
    hazard(a, 216, 330, 296, 342, (40, 36, 30), (226, 196, 120))
    return a


def enemy_gunner(src):
    a = paint_body(arr(src), (104, 128, 160), np.ones((512, 512), bool))
    remove_barrel(a, *ENEMY_BARREL)
    for cx in (243, 271):
        barrel(a, cx, 0, 156, 16, (70, 86, 110))
    fill(a, 228, 140, 288, 176, (84, 102, 130))   # wider mantlet for two guns
    return a


def enemy_heavy(src):
    a = paint_body(arr(src), (112, 96, 84), np.ones((512, 512), bool))
    remove_barrel(a, *ENEMY_BARREL)
    barrel(a, 257, 28, 160, 32, (80, 70, 62))
    # side skirts over the tracks
    for x0, x1 in ((156, 212), (300, 356)):
        plate(a, x0, 120, x1, 220, (98, 86, 76))
        plate(a, x0, 228, x1, 328, (98, 86, 76))
    # an extra plate on the front of the turret
    plate(a, 216, 176, 300, 204, (124, 108, 96), rivets=False)
    return a


def enemy_boss(src):
    a = paint_body(arr(src), (150, 52, 44), np.ones((512, 512), bool))
    remove_barrel(a, *ENEMY_BARREL)
    # forward gun and the two side guns it fires with
    barrel(a, 257, 0, 170, 28, (60, 30, 30))
    side = (60, 30, 30)
    fill(a, 96, 232, 200, 256, side)
    fill(a, 316, 232, 420, 256, side)
    fill(a, 92, 226, 108, 262, shade(side, 0.9))
    fill(a, 408, 226, 424, 262, shade(side, 0.9))
    plate(a, 200, 196, 316, 300, (170, 64, 52))
    disc(a, 258, 248, 22, (255, 120, 60))
    disc(a, 258, 248, 10, (255, 220, 140), rim=(255, 160, 80, 255))
    return a


def enemy_miner(src):
    a = paint_body(arr(src), (118, 104, 62), np.ones((512, 512), bool))
    remove_barrel(a, *ENEMY_BARREL)
    barrel(a, 257, 80, 156, 20, (90, 80, 46))
    # a rack of mines on the engine deck
    plate(a, 204, 336, 312, 420, (88, 78, 50), rivets=False)
    for cx in (226, 258, 290):
        for cy in (358, 396):
            disc(a, cx, cy, 13, (52, 50, 46))
            fill(a, cx - PX, cy - PX, cx + PX, cy + PX, (230, 60, 40), outline=False)
    return a


def enemy_fixer(src):
    a = paint_body(arr(src), (186, 186, 176), np.ones((512, 512), bool))
    remove_barrel(a, *ENEMY_BARREL)
    barrel(a, 257, 96, 156, 16, (110, 110, 104))
    hazard(a, 168, 96, 348, 112, (40, 36, 30), (240, 170, 40))
    hazard(a, 168, 420, 348, 436, (40, 36, 30), (240, 170, 40))
    # a crane arm over the turret, with a hook
    fill(a, 284, 204, 380, 220, (240, 170, 40))
    fill(a, 368, 216, 384, 248, (70, 66, 60))
    # wrench sign on the turret
    disc(a, 236, 248, 20, (60, 170, 90))
    fill(a, 232, 236, 240, 264, (235, 240, 230), outline=False)
    fill(a, 224, 236, 248, 244, (235, 240, 230), outline=False)
    return a


def enemy_rammer(src):
    a = paint_body(arr(src), (168, 84, 44), np.ones((512, 512), bool))
    remove_barrel(a, *ENEMY_BARREL)
    # a wedge ram across the front, with spikes
    steel = (96, 92, 90)
    fill(a, 152, 40, 360, 76, steel)
    fill(a, 168, 28, 344, 44, shade(steel, 1.2))
    for x in range(168, 344, 28):
        fill(a, x, 8, x + 16, 32, shade(steel, 1.3))
        fill(a, x + 4, 0, x + 12, 12, shade(steel, 1.5), outline=False)
    # exhaust stacks: it does not shoot, it only drives
    for cx in (228, 288):
        disc(a, cx, 380, 14, (60, 50, 44))
        disc(a, cx, 380, 7, (255, 140, 50), rim=(30, 24, 20, 255))
    return a


# ---------------------------------------------------------------- player tanks (from player.png)
# player.png: tracks x 150-365, y 115-395, deck from y 120; turret octagon 192-326 x 156-288; barrel 245-271 x 0-132.

PLAYER_BARREL = (240, 278, 120, 158)


def player_squall(src):
    a = arr(src)
    a = paint_body(a, (176, 150, 110), ~glow_mask(a), strength=0.85)
    a = recolour_glow(a, 0.07)
    remove_barrel(a, *PLAYER_BARREL)
    for cx in (244, 272):
        barrel(a, cx, 8, 160, 16, (90, 78, 60))
        fill(a, cx - 4, 8, cx + 4, 16, (255, 150, 60), outline=False)
    return a


def player_bastion(src):
    a = arr(src)
    a = paint_body(a, (92, 108, 80), ~glow_mask(a), strength=0.85)
    a = recolour_glow(a, 0.30)
    remove_barrel(a, *PLAYER_BARREL)
    barrel(a, 258, 40, 160, 36, (60, 70, 54))
    fill(a, 250, 40, 266, 48, (150, 255, 90), outline=False)
    # extra armour on both sides
    for x0, x1 in (148, 196), (320, 368):
        plate(a, x0, 172, x1, 260, (78, 92, 68))
        plate(a, x0, 268, x1, 356, (78, 92, 68))
    return a


def player_phantom(src):
    a = arr(src)
    a = paint_body(a, (70, 60, 92), ~glow_mask(a), strength=0.9)
    a = recolour_glow(a, 0.86)
    remove_barrel(a, *PLAYER_BARREL)
    barrel(a, 258, 0, 160, 16, (40, 34, 56))
    for y in range(24, 150, 24):
        fill(a, 254, y, 262, y + 8, (255, 90, 230), outline=False)
    return a


VARIANTS = {
    "enemy": [("enemy_scout", enemy_scout), ("enemy_gunner", enemy_gunner), ("enemy_heavy", enemy_heavy),
              ("enemy_boss", enemy_boss), ("enemy_miner", enemy_miner), ("enemy_fixer", enemy_fixer),
              ("enemy_rammer", enemy_rammer)],
    "player": [("player_squall", player_squall), ("player_bastion", player_bastion), ("player_phantom", player_phantom)],
}


def main():
    sprites, unity_out = sys.argv[1], sys.argv[2]
    browser_out = sys.argv[3] if len(sys.argv) > 3 else None
    for base, items in VARIANTS.items():
        src = Image.open(os.path.join(sprites, base + ".png")).convert("RGBA")
        for name, make in items:
            out = img(make(src))
            out.save(os.path.join(unity_out, name + ".png"))
            if browser_out:
                out.save(os.path.join(browser_out, name + ".png"))
            print("sprite", name)


if __name__ == "__main__":
    main()
