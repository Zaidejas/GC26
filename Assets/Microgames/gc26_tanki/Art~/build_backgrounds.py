"""Paints the pictures behind the menus: pixel-art scenes made from the game's own tank and tile drawings.

Each scene is painted small (WIDTH x HEIGHT) and shown scaled up with crisp pixels, like the rest of the art.
Usage, from the project folder:  python Art/build_backgrounds.py  [extra output folder]
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "..", "Resources", "gc26_tanki")
TANKS = os.path.join(RES, "Tanks")
TILES = os.path.join(RES, "Tiles", "atlas.png")
OUTPUT = os.path.join(RES, "Backgrounds")
PREVIEW = os.path.join(HERE, "backgrounds")

# 1:2, so it covers both a 9:16 frame and tall phones. Keep the important parts away from the edges.
WIDTH, HEIGHT = 360, 720
TANK_WIDTH = 64          # hull width in scene pixels

# The game's palette.
NIGHT = (43, 34, 53)
SAND = (217, 188, 140)
FIRE = (232, 100, 58)
GLOW = (111, 242, 224)
MARK = (53, 242, 208)
SHOT = (255, 233, 168)
RED = (255, 84, 71)

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32) / 16.0 - 0.5


# ---------- basics ----------

def rng(seed):
    return np.random.default_rng(seed)


def noise(seed, cell, octaves=3, w=WIDTH, h=HEIGHT):
    """Smooth value noise in 0..1."""
    r = rng(seed)
    total = np.zeros((h, w), np.float32)
    weight, amount = 0.0, 1.0
    for octave in range(octaves):
        size = max(2, int(cell / (2 ** octave)))
        grid = r.random((h // size + 3, w // size + 3)).astype(np.float32)
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize(((w // size + 3) * size, (h // size + 3) * size), Image.BICUBIC)
        total += np.asarray(img, np.float32)[:h, :w] / 255.0 * amount
        weight += amount
        amount *= 0.5
    total /= weight
    return (total - total.min()) / max(1e-6, total.max() - total.min())


def ramp(values, colours):
    """Maps 0..1 to a list of colours, in steps, with ordered dithering between them."""
    h, w = values.shape
    tiled = np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w]
    steps = len(colours) - 1
    index = np.clip(np.round(values * steps + tiled * 0.9), 0, steps).astype(int)
    return np.array(colours, np.float32)[index]


def canvas(colour_array):
    alpha = np.full(colour_array.shape[:2] + (1,), 255, np.float32)
    return Image.fromarray(np.concatenate([colour_array, alpha], 2).astype(np.uint8), "RGBA")


def as_array(img):
    return np.asarray(img, np.float32).copy()


def from_array(arr):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def blob_mask(cx, cy, rx, ry, seed, rough=0.35, w=WIDTH, h=HEIGHT):
    """A rough-edged ellipse mask in 0..1."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.hypot((xs - cx) / rx, (ys - cy) / ry)
    n = noise(seed, 10, 2, w, h)
    return np.clip((1.0 - d + (n - 0.5) * rough) * 6, 0, 1)


def paint(arr, mask, colour, strength=1.0):
    m = (mask * strength)[..., None]
    arr[..., :3] = arr[..., :3] * (1 - m) + np.array(colour, np.float32) * m


def light(arr, cx, cy, radius, colour, strength=1.0, flat=False):
    """Adds coloured light falling off from a point (screen blend)."""
    ys, xs = np.mgrid[0:arr.shape[0], 0:arr.shape[1]].astype(np.float32)
    d = np.hypot(xs - cx, ys - cy) / radius
    amount = np.clip(1 - d, 0, 1)
    amount = amount if flat else amount ** 2
    add = np.array(colour, np.float32) * (amount * strength)[..., None]
    arr[..., :3] = 255 - (255 - arr[..., :3]) * (1 - np.clip(add / 255.0, 0, 1))


def shade(arr, colour, amount):
    """Multiplies the whole scene towards a colour: the time of day."""
    arr[..., :3] = arr[..., :3] * (1 - amount) + arr[..., :3] * np.array(colour, np.float32) / 255.0 * amount


def vignette(arr, strength=0.55, top=0.0, bottom=0.0):
    h, w = arr.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.hypot((xs - w / 2) / (w * 0.62), (ys - h / 2) / (h * 0.62))
    dark = np.clip(d - 0.45, 0, 1) * strength
    # Extra dark bands where titles and buttons sit, so text stays readable.
    dark += np.clip(1 - ys / (h * 0.28), 0, 1) ** 1.5 * top
    dark += np.clip((ys - h * 0.72) / (h * 0.28), 0, 1) ** 1.5 * bottom
    arr[..., :3] *= (1 - np.clip(dark, 0, 0.92))[..., None]


def finish(arr, levels=24):
    """Posterises with ordered dithering so the light reads as pixel art too."""
    arr = as_array(arr)
    h, w = arr.shape[:2]
    tiled = np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w][..., None]
    step = 255.0 / levels
    arr[..., :3] = np.round(arr[..., :3] / step + tiled * 0.8) * step
    arr[..., 3] = 255
    return from_array(arr)


# ---------- the game's drawings ----------

def clean_player(sprite):
    """The player's tank with a reactor light in place of the turret mark, as on the icon."""
    pixels = np.asarray(sprite, np.float32).copy()
    box = (235, 165, 280, 200)
    area = pixels[box[1]:box[3], box[0]:box[2]]
    ys, xs = np.nonzero(area[..., :3].min(axis=2) > 190)
    if len(xs):
        left, right, top, bottom = xs.min() + box[0], xs.max() + box[0], ys.min() + box[1], ys.max() + box[1]
        paint_colour = pixels[top - 7, (left + right) // 2]
        pixels[top - 3:bottom + 4, left - 3:right + 4] = paint_colour
        mx, my = (left + right) // 2, (top + bottom) // 2
        pixels[my - 5:my + 6, mx - 5:mx + 6] = (*MARK, 255)
    return Image.fromarray(pixels.astype(np.uint8), "RGBA")


def load_tank(name, width=TANK_WIDTH):
    sprite = Image.open(os.path.join(TANKS, name + ".png")).convert("RGBA")
    if name == "player":
        sprite = clean_player(sprite)
    box = sprite.getbbox()
    sprite = sprite.crop(box)
    # Hull width is about the sprite's width; the barrel adds height.
    factor = width / sprite.width
    small = sprite.resize((round(sprite.width * factor), round(sprite.height * factor)), Image.LANCZOS)
    # Hard edges, like pixel art.
    arr = np.asarray(small).copy()
    arr[..., 3] = np.where(arr[..., 3] > 110, 255, 0)
    return Image.fromarray(arr, "RGBA")


def wreck(tank, seed):
    """A burnt-out copy: dark, rusty and with holes."""
    arr = np.asarray(tank, np.float32).copy()
    grey = arr[..., :3].mean(axis=2, keepdims=True)
    arr[..., :3] = grey * np.array([0.55, 0.42, 0.36]) + np.array([14, 8, 6])
    n = noise(seed, 6, 2, arr.shape[1], arr.shape[0])
    arr[..., :3] *= (0.6 + n * 0.6)[..., None]
    holes = (noise(seed + 1, 5, 1, arr.shape[1], arr.shape[0]) > 0.86) & (arr[..., 3] > 0)
    arr[holes, :3] = (20, 14, 14)
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def load_tiles():
    atlas = Image.open(TILES).convert("RGBA")
    cell = 72  # a 3x3 grid in the top-left corner, see build_tiles.py
    names = ["crate", "crate2", "crate_broken", "crate_broken2", "concrete", "hazard"]
    tiles = {}
    for i, name in enumerate(names):
        x, y = i % 3 * cell, i // 3 * cell
        tiles[name] = atlas.crop((x, y, x + cell, y + cell))
    for i in range(3):
        tiles["bush" + str(i)] = bush(40 + i)
    return tiles


def bush(seed, size=48):
    """A round clump of leaves, darker underneath, with pixel highlights."""
    r = rng(seed)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    clumps = [(size / 2 + r.uniform(-9, 9), size / 2 + r.uniform(-9, 9), r.uniform(8, 13)) for _ in range(7)]
    for colour, shift, grow in (((26, 52, 26), 2, 1.0), ((48, 92, 40), 0, 0.9), ((78, 128, 54), -2, 0.6), ((120, 168, 74), -3, 0.25)):
        for (x, y, rad) in clumps:
            rr = rad * grow
            d.ellipse((x - rr + shift * 0.3, y - rr + shift, x + rr + shift * 0.3, y + rr + shift), fill=(*colour, 255))
    return img


def put(img, sprite, x, y, angle=0, size=None, shadow=True):
    """Pastes a drawing centred on x, y, turned clockwise by angle degrees, with a soft shadow below-right."""
    if size:
        sprite = sprite.resize((size, round(sprite.height * size / sprite.width)), Image.NEAREST)
    if angle:
        sprite = sprite.rotate(-angle, Image.NEAREST, expand=True)
    px, py = round(x - sprite.width / 2), round(y - sprite.height / 2)
    if shadow:
        alpha = sprite.split()[3]
        dark = Image.new("RGBA", sprite.size, (8, 5, 12, 0))
        dark.putalpha(alpha.point(lambda a: int(a * 0.55)))
        dark = dark.filter(ImageFilter.GaussianBlur(2))
        img.alpha_composite(dark, (px + 5, py + 6))
    img.alpha_composite(sprite, (px, py))


def overlay(img, arr_fn):
    """Runs an array operation on an image."""
    arr = as_array(img)
    arr_fn(arr)
    return from_array(arr)


# ---------- scene parts ----------

def dirt_ground(seed, colours=None):
    colours = colours or [(46, 36, 34), (62, 48, 40), (80, 62, 48), (99, 78, 58), (118, 94, 68)]
    base = noise(seed, 48) * 0.6 + noise(seed + 7, 9, 2) * 0.4
    arr = ramp(base, colours)
    # Small stones.
    r = rng(seed)
    for _ in range(260):
        x, y = r.integers(0, WIDTH), r.integers(0, HEIGHT)
        c = np.array(colours[r.integers(2, len(colours))], np.float32) * r.uniform(0.9, 1.25)
        arr[y, x] = np.clip(c, 0, 255)
        if x + 1 < WIDTH:
            arr[y, x + 1] = arr[y, x] * 0.7
    return canvas(arr)


def tracks(img, points, width=12, seed=0):
    """Tank tracks: two dark bands of tread marks along a path."""
    draw_layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(draw_layer)
    for i in range(len(points) - 1):
        (x0, y0), (x1, y1) = points[i], points[i + 1]
        length = math.hypot(x1 - x0, y1 - y0)
        nx, ny = -(y1 - y0) / length, (x1 - x0) / length
        steps = int(length / 3)
        for s in range(steps):
            t = s / max(1, steps)
            cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for side in (-1, 1):
                ox, oy = cx + nx * side * width / 2, cy + ny * side * width / 2
                d.line((ox - nx * 3, oy - ny * 3, ox + nx * 3, oy + ny * 3), fill=(20, 14, 16, 110))
    img.alpha_composite(draw_layer)


def crater(img, cx, cy, r, seed):
    arr = as_array(img)
    outer = blob_mask(cx, cy, r * 1.35, r * 1.2, seed, 0.5)
    inner = blob_mask(cx + 1, cy + 1, r, r * 0.88, seed + 1, 0.4)
    paint(arr, outer, (128, 102, 74), 0.45)
    paint(arr, inner, (24, 18, 20), 0.85)
    paint(arr, blob_mask(cx - r * 0.25, cy - r * 0.3, r * 0.45, r * 0.3, seed + 2, 0.3), (52, 40, 38), 0.6)
    return from_array(arr)


def smoke(img, cx, cy, rx, ry, seed, colour=(30, 24, 34), strength=0.75):
    arr = as_array(img)
    ys, xs = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float32)
    d = np.hypot((xs - cx) / rx, (ys - cy) / ry)
    n = noise(seed, 14, 3)
    m = np.clip((1 - d) * 1.6, 0, 1) * np.clip(n * 1.8 - 0.35, 0, 1)
    # Stepped, like painted pixel smoke.
    m = np.floor(m * 4) / 4
    paint(arr, m, colour, strength)
    return from_array(arr)


def fire(img, cx, cy, size, seed):
    arr = as_array(img)
    light(arr, cx, cy, size * 7, FIRE, 0.75)
    for r, colour in ((1.0, (190, 48, 30)), (0.68, FIRE), (0.4, (255, 190, 90)), (0.18, SHOT)):
        paint(arr, blob_mask(cx, cy - size * 0.25 * (1 - r), size * r, size * r * 1.3, seed + int(r * 10), 0.8), colour)
    return from_array(arr)


def tracer(img, x0, y0, x1, y1, colour=SHOT):
    arr = as_array(img)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.line((x0, y0, x1, y1), fill=(*colour, 255), width=2)
    glow = np.asarray(layer.filter(ImageFilter.GaussianBlur(4)), np.float32)
    arr[..., :3] = 255 - (255 - arr[..., :3]) * (1 - glow[..., :3] / 255.0 * glow[..., 3:] / 255.0 * 0.9)
    img2 = from_array(arr)
    img2.alpha_composite(layer)
    return img2


def flash(img, x, y, size, colour=SHOT):
    arr = as_array(img)
    light(arr, x, y, size * 6, colour, 0.9)
    img = from_array(arr)
    d = ImageDraw.Draw(img)
    for a in range(0, 360, 45):
        length = size * (1.6 if a % 90 == 0 else 0.9)
        rad = math.radians(a)
        d.line((x, y, x + math.cos(rad) * length, y + math.sin(rad) * length), fill=(*colour, 255), width=2)
    d.ellipse((x - size * 0.5, y - size * 0.5, x + size * 0.5, y + size * 0.5), fill=(255, 255, 240, 255))
    return img


def reactor(img, cx, cy, r, state="ok"):
    """The base: a round armoured plate with a glowing core and the three-part charge ring."""
    arr = as_array(img)
    core = GLOW if state == "ok" else RED
    light(arr, cx, cy, r * 5.5, core, 0.85 if state == "ok" else 0.7)
    img = from_array(arr)
    d = ImageDraw.Draw(img)
    # Concrete pad.
    d.rectangle((cx - r * 1.6, cy - r * 1.6, cx + r * 1.6, cy + r * 1.6), fill=(74, 72, 80, 255), outline=(36, 32, 42, 255), width=2)
    for i in range(-1, 2):
        d.line((cx - r * 1.6, cy + i * r * 0.8, cx + r * 1.6, cy + i * r * 0.8), fill=(60, 58, 66, 255))
    d.ellipse((cx - r * 1.2, cy - r * 1.2, cx + r * 1.2, cy + r * 1.2), fill=(52, 50, 60, 255), outline=(24, 22, 30, 255), width=2)
    d.ellipse((cx - r * 0.95, cy - r * 0.95, cx + r * 0.95, cy + r * 0.95), fill=(88, 90, 100, 255))
    # Bolts.
    for a in range(0, 360, 45):
        rad = math.radians(a + 22.5)
        bx, by = cx + math.cos(rad) * r * 1.08, cy + math.sin(rad) * r * 1.08
        d.rectangle((bx - 1, by - 1, bx + 1, by + 1), fill=(130, 130, 140, 255))
    gap = 14
    for start in (270, 30, 150):
        if state == "broken" and start == 30:
            continue
        d.arc((cx - r * 0.8, cy - r * 0.8, cx + r * 0.8, cy + r * 0.8), start + gap, start + 120 - gap, fill=(*core, 255), width=max(2, int(r * 0.14)))
    d.ellipse((cx - r * 0.42, cy - r * 0.42, cx + r * 0.42, cy + r * 0.42), fill=(*core, 255))
    d.ellipse((cx - r * 0.22, cy - r * 0.22, cx + r * 0.22, cy + r * 0.22), fill=(240, 255, 250, 255) if state == "ok" else (255, 210, 160, 255))
    if state == "broken":
        for a in (20, 140, 250):
            rad = math.radians(a)
            d.line((cx, cy, cx + math.cos(rad) * r * 1.3, cy + math.sin(rad) * r * 1.3), fill=(20, 14, 18, 255), width=2)
    return img


def sandbags(img, x, y, count, horizontal=True):
    d = ImageDraw.Draw(img)
    for i in range(count):
        for row in range(2):
            ox = i * 11 + (5 if row else 0)
            bx, by = (x + ox, y + row * 6) if horizontal else (x + row * 6, y + ox)
            w, h = (10, 6) if horizontal else (6, 10)
            d.rounded_rectangle((bx, by, bx + w, by + h), 2, fill=(150, 126, 88, 255), outline=(70, 56, 40, 255))
            d.line((bx + 2, by + 2, bx + w - 3, by + 2) if horizontal else (bx + 2, by + 2, bx + 2, by + h - 3), fill=(176, 152, 110, 255))


# ---------- scenes ----------

def scene_menu(t):
    """Main menu: the player's tank holds the line in front of the reactor while enemies come out of the smoke."""
    img = dirt_ground(1)
    tracks(img, [(180, 720), (180, 560), (176, 470)], 46)
    tracks(img, [(60, 0), (90, 140), (110, 240)], 46)
    for (x, y, r, s) in ((70, 330, 13, 2), (290, 410, 16, 3), (230, 250, 11, 4), (40, 520, 10, 5), (320, 610, 12, 6), (150, 180, 9, 7)):
        img = crater(img, x, y, r, s)
    put(img, t["tiles"]["concrete"], 120, 640, size=34)
    put(img, t["tiles"]["concrete"], 240, 640, size=34)
    put(img, t["tiles"]["hazard"], 120, 606, size=34)
    put(img, t["tiles"]["hazard"], 240, 606, size=34)
    put(img, t["tiles"]["crate"], 40, 430, size=34)
    put(img, t["tiles"]["crate_broken"], 74, 446, 12, size=34)
    put(img, t["tiles"]["crate2"], 316, 300, size=34)
    put(img, t["tiles"]["crate_broken2"], 300, 520, -8, size=34)
    put(img, t["tiles"]["bush1"], 20, 640, size=46, shadow=False)
    put(img, t["tiles"]["bush0"], 340, 690, size=50, shadow=False)
    put(img, t["tiles"]["bush2"], 340, 200, size=44, shadow=False)
    sandbags(img, 140, 566, 7)
    img = reactor(img, 180, 668, 30)

    put(img, t["enemy"], 96, 110, 180)
    put(img, t["enemy"], 274, 150, 200)
    put(img, t["wreck"], 200, 262, 160)
    img = fire(img, 202, 258, 9, 11)
    img = smoke(img, 205, 190, 60, 90, 12)
    img = smoke(img, 180, 40, 220, 80, 13, strength=0.6)

    put(img, t["player"], 178, 480, -4)
    img = tracer(img, 172, 410, 124, 150)
    img = flash(img, 173, 404, 6)
    img = tracer(img, 270, 210, 220, 380, (255, 120, 90))
    img = flash(img, 125, 148, 5, (255, 210, 140))

    def grade(arr):
        shade(arr, (120, 96, 170), 0.55)
        light(arr, 180, 470, 110, (255, 200, 150), 0.18)
        vignette(arr, 0.75, top=0.55, bottom=0.1)
    return finish(overlay(img, grade))


def scene_map(t):
    """Level select: a battle map on the commander's table under a lamp."""
    wood = noise(20, 60, 2, WIDTH, HEIGHT) * 0.4 + np.tile(noise(21, 3, 1, 1, HEIGHT), (1, WIDTH)) * 0.0
    grain = noise(22, 4, 1, WIDTH // 6 + 1, HEIGHT)
    grain = np.repeat(grain, 6, axis=1)[:, :WIDTH]
    arr = ramp(np.clip(wood + grain * 0.6, 0, 1), [(40, 24, 18), (58, 36, 24), (76, 48, 30), (92, 60, 38)])
    img = canvas(arr)
    d = ImageDraw.Draw(img)

    # The paper map, a little turned.
    paper = Image.new("RGBA", (300, 560), (0, 0, 0, 0))
    pa = ramp(noise(23, 30, 3, 300, 560), [(176, 150, 108), (196, 170, 124), (212, 188, 140), (224, 202, 156)])
    paper = canvas(pa)
    pd = ImageDraw.Draw(paper)
    for x in range(0, 300, 30):
        pd.line((x, 0, x, 560), fill=(160, 134, 96, 255))
    for y in range(0, 560, 30):
        pd.line((0, y, 300, y), fill=(160, 134, 96, 255))
    # River, roads, forest and hills.
    river = [(0, 150), (60, 170), (110, 230), (160, 240), (220, 300), (300, 320)]
    pd.line(river, fill=(70, 120, 140, 255), width=9, joint="curve")
    pd.line(river, fill=(100, 160, 176, 255), width=4, joint="curve")
    pd.line([(40, 560), (80, 420), (150, 380), (180, 240), (240, 120), (260, 0)], fill=(140, 104, 70, 255), width=4, joint="curve")
    for (x, y) in ((40, 60), (60, 90), (30, 100), (250, 420), (270, 450), (230, 470), (90, 470)):
        pd.ellipse((x - 12, y - 10, x + 12, y + 10), fill=(104, 128, 76, 255), outline=(80, 100, 60, 255))
    for i, r in enumerate((60, 44, 28)):
        pd.ellipse((200 - r, 200 - r * 0.7, 200 + r, 200 + r * 0.7), outline=(150, 110, 76, 255), width=2)
    # Red arrows of the enemy advance, a blue line of defence.
    def arrow(points, colour):
        pd.line(points, fill=(*colour, 255), width=5, joint="curve")
        (x0, y0), (x1, y1) = points[-2], points[-1]
        a = math.atan2(y1 - y0, x1 - x0)
        tip = [(x1 + math.cos(a) * 10, y1 + math.sin(a) * 10),
               (x1 + math.cos(a + 2.4) * 12, y1 + math.sin(a + 2.4) * 12),
               (x1 + math.cos(a - 2.4) * 12, y1 + math.sin(a - 2.4) * 12)]
        pd.polygon(tip, fill=(*colour, 255))
    arrow([(60, 20), (90, 120), (120, 250), (130, 340)], (180, 40, 34))
    arrow([(250, 30), (230, 140), (200, 270), (170, 350)], (180, 40, 34))
    arrow([(290, 230), (240, 300), (190, 380)], (180, 40, 34))
    pd.line([(50, 430), (110, 410), (190, 412), (260, 432)], fill=(40, 90, 150, 255), width=4)
    for x in range(60, 260, 22):
        pd.line((x, 422, x + 6, 412), fill=(40, 90, 150, 255), width=2)
    # Reactor symbol.
    pd.ellipse((130, 470, 170, 510), outline=(20, 120, 110, 255), width=4)
    pd.ellipse((143, 483, 157, 497), fill=(20, 140, 126, 255))
    # Worn edges and folds.
    fold = np.asarray(paper, np.float32)
    fold[:, 148:152, :3] *= 0.86
    fold[278:282, :, :3] *= 0.86
    edge = np.ones((560, 300), np.float32)
    n = noise(24, 6, 2, 300, 560)
    border = np.minimum.reduce([np.arange(300)[None, :].repeat(560, 0), (299 - np.arange(300))[None, :].repeat(560, 0),
                                np.arange(560)[:, None].repeat(300, 1), (559 - np.arange(560))[:, None].repeat(300, 1)])
    fold[..., 3] = np.where(border + n * 6 < 3, 0, 255)
    fold[..., :3] *= np.clip(0.75 + border / 40, 0, 1)[..., None]
    paper = Image.fromarray(np.clip(fold, 0, 255).astype(np.uint8), "RGBA").rotate(3, Image.NEAREST, expand=True)
    put(img, paper, 180, 370)

    # Tokens: small tanks on the map, pins, a compass and a mug ring.
    small_enemy = t["enemy"].resize((22, round(t["enemy"].height * 22 / t["enemy"].width)), Image.LANCZOS)
    small_player = t["player"].resize((22, round(t["player"].height * 22 / t["player"].width)), Image.LANCZOS)
    for (x, y, a) in ((96, 150, 170), (230, 170, 200), (250, 300, 230)):
        put(img, small_enemy, x, y, a)
    put(img, small_player, 180, 440, 0)
    for (x, y, c) in ((70, 110, (200, 40, 40)), (270, 260, (200, 40, 40)), (150, 520, (40, 160, 140))):
        d.ellipse((x - 4, y - 4, x + 4, y + 4), fill=(*c, 255), outline=(30, 20, 20, 255))
        d.point((x - 1, y - 2), fill=(255, 230, 220, 255))
    d.ellipse((260, 560, 320, 620), outline=(70, 46, 30, 255), width=3)
    # Brass compass in the corner and a pencil.
    d.ellipse((24, 600, 84, 660), fill=(150, 120, 60, 255), outline=(90, 70, 30, 255), width=3)
    d.ellipse((32, 608, 76, 652), fill=(220, 210, 180, 255))
    d.polygon([(54, 612), (58, 630), (54, 648), (50, 630)], fill=(180, 40, 34, 255))
    d.line((276, 60, 336, 120), fill=(200, 160, 40, 255), width=6)
    d.line((276, 60, 282, 66), fill=(60, 40, 30, 255), width=6)

    def grade(arr):
        shade(arr, (70, 56, 110), 0.55)
        light(arr, 180, 300, 330, (255, 214, 150), 0.65)
        vignette(arr, 0.95, top=0.35, bottom=0.2)
    return finish(overlay(img, grade))


def scene_workshop(t):
    """Workshop: the player's tank on a repair pad in the hangar, with parts and a welding spark."""
    tiles = ramp(noise(30, 40, 3) * 0.7 + noise(31, 5, 1) * 0.3, [(58, 58, 64), (70, 70, 76), (82, 82, 88), (94, 94, 100)])
    img = canvas(tiles)
    d = ImageDraw.Draw(img)
    for x in range(0, WIDTH, 60):
        d.line((x, 0, x, HEIGHT), fill=(40, 40, 46, 255))
    for y in range(0, HEIGHT, 60):
        d.line((0, y, WIDTH, y), fill=(40, 40, 46, 255))
    # Oil stains.
    arr = as_array(img)
    for i, (x, y, r) in enumerate(((90, 300, 26), (250, 600, 30), (300, 200, 18))):
        paint(arr, blob_mask(x, y, r, r * 0.7, 32 + i), (30, 28, 34), 0.6)
    img = from_array(arr)
    d = ImageDraw.Draw(img)
    # Repair pad with hazard stripes.
    x0, y0, x1, y1 = 90, 380, 270, 650
    d.rectangle((x0, y0, x1, y1), fill=(52, 54, 62, 255))
    stripe = Image.new("RGBA", (x1 - x0 + 16, y1 - y0 + 16), (0, 0, 0, 0))
    sd = ImageDraw.Draw(stripe)
    for k in range(-40, 80):
        sd.polygon([(k * 12, 0), (k * 12 + 6, 0), (k * 12 + 6 - 400, 400 * 1), (k * 12 - 400, 400)], fill=(230, 180, 40, 255))
    sa = np.asarray(stripe, np.float32).copy()
    sw, sh = stripe.size
    frame = np.zeros((sh, sw), bool)
    frame[:8, :] = frame[-8:, :] = True
    frame[:, :8] = frame[:, -8:] = True
    black = (sa[..., 3] == 0) & frame
    sa[black] = (24, 22, 26, 255)
    sa[~frame] = 0
    img.alpha_composite(Image.fromarray(sa.astype(np.uint8), "RGBA"), (x0 - 8, y0 - 8))
    # Lift posts.
    for (px, py) in ((x0 + 6, y0 + 6), (x1 - 6, y0 + 6), (x0 + 6, y1 - 6), (x1 - 6, y1 - 6)):
        d.rectangle((px - 7, py - 7, px + 7, py + 7), fill=(120, 124, 134, 255), outline=(30, 30, 36, 255), width=2)
    # Tool chests and parts.
    for (cx, cy, colour) in ((40, 120, (170, 40, 36)), (40, 190, (170, 40, 36)), (318, 110, (40, 80, 150))):
        d.rectangle((cx - 26, cy - 30, cx + 26, cy + 30), fill=(*colour, 255), outline=(24, 18, 20, 255), width=2)
        for k in range(4):
            d.line((cx - 22, cy - 22 + k * 14, cx + 22, cy - 22 + k * 14), fill=(24, 18, 20, 255))
            d.line((cx - 6, cy - 16 + k * 14, cx + 6, cy - 16 + k * 14), fill=(200, 200, 210, 255))
    put(img, t["tiles"]["crate"], 318, 330, size=40)
    put(img, t["tiles"]["crate2"], 322, 376, 8, size=40)
    put(img, t["tiles"]["concrete"], 40, 470, size=40)
    # Gears (the "parts" currency) spilled near the crates.
    def gear(x, y, r):
        for a in range(0, 360, 45):
            rad = math.radians(a)
            d.rectangle((x + math.cos(rad) * r - 2, y + math.sin(rad) * r - 2, x + math.cos(rad) * r + 2, y + math.sin(rad) * r + 2), fill=(200, 170, 90, 255))
        d.ellipse((x - r, y - r, x + r, y + r), fill=(200, 170, 90, 255), outline=(110, 84, 40, 255))
        d.ellipse((x - r * 0.4, y - r * 0.4, x + r * 0.4, y + r * 0.4), fill=(70, 70, 76, 255))
    for (x, y, r) in ((290, 440, 7), (306, 462, 5), (276, 470, 6), (52, 540, 6), (330, 250, 5)):
        gear(x, y, r)
    put(img, t["player_big"], 180, 512)
    img = flash(img, 210, 470, 4, GLOW)
    d = ImageDraw.Draw(img)
    r = rng(33)
    for _ in range(18):
        a = r.uniform(0, math.tau)
        ln = r.uniform(4, 16)
        d.line((210, 470, 210 + math.cos(a) * ln, 470 + math.sin(a) * ln), fill=(255, 230, 150, 255))

    def grade(arr):
        shade(arr, (90, 84, 130), 0.5)
        light(arr, 180, 500, 230, (255, 226, 170), 0.55)
        light(arr, 60, 120, 120, (255, 200, 140), 0.3)
        light(arr, 210, 470, 70, GLOW, 0.6)
        vignette(arr, 0.85, top=0.6, bottom=0.15)
    return finish(overlay(img, grade))


def scene_arena(t):
    """Survival: night arena with red alarm lights, the player in the middle and enemies closing in from all sides."""
    img = dirt_ground(40, [(40, 30, 34), (54, 40, 40), (70, 52, 46), (86, 64, 52), (100, 76, 58)])
    d = ImageDraw.Draw(img)
    # Arena walls of concrete along the edges.
    for y in range(0, HEIGHT, 34):
        put(img, t["tiles"]["concrete"], 8, y + 17, size=34)
        put(img, t["tiles"]["concrete"], WIDTH - 8, y + 17, size=34)
    for (x, y, r, s) in ((120, 300, 14, 41), (250, 360, 12, 42), (180, 200, 10, 43), (90, 560, 13, 44), (280, 520, 15, 45)):
        img = crater(img, x, y, r, s)
    tracks(img, [(180, 720), (180, 430)], 46)
    put(img, t["enemy"], 180, 96, 180)
    put(img, t["enemy"], 76, 236, 140)
    put(img, t["enemy"], 290, 256, 220)
    put(img, t["enemy"], 64, 640, 60)
    put(img, t["enemy"], 300, 650, -60)
    put(img, t["wreck"], 250, 150, 200)
    img = fire(img, 252, 150, 8, 46)
    img = smoke(img, 250, 90, 70, 70, 47)
    put(img, t["player"], 180, 440)
    img = tracer(img, 180, 372, 180, 170)
    img = flash(img, 180, 366, 6)
    img = tracer(img, 92, 268, 160, 410, (255, 120, 90))
    img = tracer(img, 276, 288, 204, 420, (255, 120, 90))

    def grade(arr):
        shade(arr, (90, 60, 120), 0.65)
        light(arr, 30, 40, 200, RED, 0.6)
        light(arr, 330, 40, 200, RED, 0.6)
        light(arr, 180, 440, 120, (255, 200, 160), 0.25)
        vignette(arr, 0.8, top=0.55, bottom=0.2)
    return finish(overlay(img, grade))


def scene_victory(t):
    """Victory: dawn over the held line. The reactor is safe and the enemy wrecks are still smoking."""
    img = dirt_ground(50, [(70, 54, 44), (92, 70, 52), (116, 90, 64), (138, 108, 76), (160, 128, 90)])
    for (x, y, r, s) in ((80, 260, 14, 51), (280, 330, 13, 52), (150, 150, 11, 53), (300, 140, 10, 54), (60, 470, 12, 55)):
        img = crater(img, x, y, r, s)
    tracks(img, [(180, 720), (180, 480)], 46)
    put(img, t["tiles"]["bush0"], 30, 560, size=50, shadow=False)
    put(img, t["tiles"]["bush2"], 336, 600, size=46, shadow=False)
    put(img, t["tiles"]["crate_broken"], 320, 430, 10, size=34)
    put(img, t["wreck"], 90, 130, 150)
    put(img, t["wreck"], 270, 210, 230)
    put(img, t["wreck"], 200, 70, 190)
    img = smoke(img, 90, 70, 50, 90, 56, (60, 50, 56), 0.55)
    img = smoke(img, 270, 150, 50, 90, 57, (60, 50, 56), 0.55)
    img = fire(img, 272, 212, 6, 58)
    img = reactor(img, 180, 640, 30)
    put(img, t["player"], 180, 470)

    def grade(arr):
        shade(arr, (255, 196, 140), 0.35)
        light(arr, 180, -40, 520, (255, 196, 120), 0.75, flat=True)
        vignette(arr, 0.6, top=0.4, bottom=0.1)
    return finish(overlay(img, grade))


def scene_defeat(t):
    """Defeat: the player's tank burnt out in front of a broken reactor, enemies rolling past."""
    img = dirt_ground(60, [(36, 26, 28), (50, 36, 34), (64, 46, 40), (80, 58, 46), (94, 68, 52)])
    for (x, y, r, s) in ((90, 360, 16, 61), (270, 300, 14, 62), (200, 600, 18, 63), (60, 600, 12, 64), (300, 520, 13, 65)):
        img = crater(img, x, y, r, s)
    put(img, t["tiles"]["crate_broken"], 60, 470, 25, size=34)
    put(img, t["tiles"]["crate_broken2"], 300, 420, -15, size=34)
    put(img, t["tiles"]["concrete"], 120, 640, 8, size=34)
    img = reactor(img, 180, 660, 30, "broken")
    put(img, t["player_wreck"], 176, 470, 18)
    img = fire(img, 170, 460, 11, 66)
    img = fire(img, 196, 492, 6, 67)
    img = smoke(img, 170, 330, 90, 170, 68, (24, 18, 22), 0.8)
    put(img, t["enemy"], 80, 140, 170)
    put(img, t["enemy"], 280, 110, 190)

    def grade(arr):
        shade(arr, (150, 60, 70), 0.6)
        light(arr, 180, 660, 220, RED, 0.5)
        vignette(arr, 0.9, top=0.6, bottom=0.2)
    return finish(overlay(img, grade))


SCENES = (("menu", scene_menu), ("map", scene_map), ("workshop", scene_workshop),
          ("arena", scene_arena), ("victory", scene_victory), ("defeat", scene_defeat))


def main():
    extra = sys.argv[1] if len(sys.argv) > 1 else None
    os.makedirs(OUTPUT, exist_ok=True)
    os.makedirs(PREVIEW, exist_ok=True)
    player, enemy = load_tank("player"), load_tank("enemy")
    parts = {"player": player, "player_big": load_tank("player", 96), "enemy": enemy, "wreck": wreck(enemy, 70), "player_wreck": wreck(player, 71), "tiles": load_tiles()}

    pictures = []
    for name, draw in SCENES:
        picture = draw(parts).convert("RGB")
        picture.save(os.path.join(OUTPUT, name + ".png"), optimize=True)
        if extra:
            os.makedirs(extra, exist_ok=True)
            picture.save(os.path.join(extra, name + ".png"), optimize=True)
        pictures.append(picture)

    # All scenes side by side at 1.5x, the 9:16 part that a frame shows marked with lines.
    scale = 1.5
    w, h = round(WIDTH * scale), round(HEIGHT * scale)
    sheet = Image.new("RGB", (len(pictures) * (w + 12) + 12, h + 24), (20, 18, 24))
    for i, picture in enumerate(pictures):
        sheet.paste(picture.resize((w, h), Image.NEAREST), (12 + i * (w + 12), 12))
        cut = (h - w * 16 / 9) / 2
        draw = ImageDraw.Draw(sheet)
        for y in (12 + cut, 12 + h - cut):
            draw.line((12 + i * (w + 12), y, 12 + i * (w + 12) + w, y), fill=(255, 255, 255))
    sheet.save(os.path.join(PREVIEW, "all.png"))
    print("backgrounds written to", OUTPUT)


if __name__ == "__main__":
    main()
