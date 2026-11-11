"""Draws the map's crates, concrete blocks and bushes as pixel art and builds their 3D models.

usage: build_tiles.py <unity output folder> [<browser output folder>]
Needs Pillow. Writes atlas.png (all the textures) and one model per tile. To change the look,
edit the draw_ functions or heights below and run the script again. Models are one map cell wide
and use the same space as build_models.py.
"""
import os
import random
import sys
from PIL import Image

from build_models import write_json, write_obj

ART = 32
SCALE = 2
GUTTER = 4
ATLAS = 256
SLOT = ART * SCALE + GUTTER * 2

CRATE_HEIGHT = 0.86
BROKEN_HEIGHT = 0.52
CONCRETE_HEIGHT = 1.0
BUSH_COLUMNS = 16
BUSH_VARIANTS = 3
HALF = 0.49

# Atlas slots, left to right, top to bottom.
SLOTS = ["crate_top", "crate_side", "broken_top", "broken_side", "concrete_top", "concrete_side", "bush0", "bush1", "bush2"]

OUTLINE = (24, 16, 12)


def rgb(text):
    return tuple(int(text[i:i + 2], 16) for i in (0, 2, 4))


def shade(color, amount):
    return tuple(max(0, min(255, c + amount)) for c in color)


class Art:
    def __init__(self, seed):
        self.image = Image.new("RGB", (ART, ART))
        self.px = self.image.load()
        self.rng = random.Random(seed)

    def rect(self, x0, y0, x1, y1, color):
        for y in range(max(y0, 0), min(y1, ART - 1) + 1):
            for x in range(max(x0, 0), min(x1, ART - 1) + 1):
                self.px[x, y] = color

    def frame(self, color):
        self.rect(0, 0, ART - 1, 0, color)
        self.rect(0, ART - 1, ART - 1, ART - 1, color)
        self.rect(0, 0, 0, ART - 1, color)
        self.rect(ART - 1, 0, ART - 1, ART - 1, color)

    def specks(self, count, amount):
        for _ in range(count):
            x, y = self.rng.randrange(1, ART - 1), self.rng.randrange(1, ART - 1)
            self.px[x, y] = shade(self.px[x, y], self.rng.choice((-amount, amount)))

    def bracket(self, x, y, size=5):
        steel = rgb("6F7478")
        self.rect(x, y, x + size - 1, y + size - 1, steel)
        self.rect(x, y, x + size - 1, y, shade(steel, 26))
        self.rect(x, y + size - 1, x + size - 1, y + size - 1, shade(steel, -28))
        self.px[x + size // 2, y + size // 2] = rgb("C8CDD0")


WOOD = [rgb("A9713C"), rgb("9A6532"), rgb("B57E45"), rgb("8F5C2C")]


def planks(art, vertical):
    """Fills the art with boards, each with its own tone and a little grain."""
    width = 6
    for band in range(0, ART, width):
        tone = art.rng.choice(WOOD)
        for a in range(band, min(band + width, ART)):
            for b in range(ART):
                color = tone if a != band else shade(tone, -46)
                if a == band + 1:
                    color = shade(tone, 14)
                x, y = (a, b) if vertical else (b, a)
                art.px[x, y] = color
        for _ in range(5):
            b = art.rng.randrange(ART)
            a = art.rng.randrange(band + 2, min(band + width, ART)) if band + 2 < ART else band
            for step in range(art.rng.randrange(3, 7)):
                if b + step < ART:
                    x, y = (a, b + step) if vertical else (b + step, a)
                    art.px[x, y] = shade(art.px[x, y], -16)


def draw_crate_top(art):
    planks(art, False)
    edge = rgb("6E4422")
    art.rect(1, 1, ART - 2, 3, edge)
    art.rect(1, ART - 4, ART - 2, ART - 2, edge)
    art.rect(1, 1, 3, ART - 2, edge)
    art.rect(ART - 4, 1, ART - 2, ART - 2, edge)
    art.frame(OUTLINE)
    for x, y in ((1, 1), (ART - 6, 1), (1, ART - 6), (ART - 6, ART - 6)):
        art.bracket(x, y)


def draw_crate_side(art):
    planks(art, True)
    edge = rgb("6E4422")
    # The classic diagonal brace of a shipping crate.
    for i in range(4, ART - 4):
        for t in range(-2, 3):
            y = i + t
            if 4 <= y < ART - 4:
                art.px[i, y] = shade(edge, 16 if t == -2 else 0)
    art.rect(1, 1, ART - 2, 4, edge)
    art.rect(1, ART - 5, ART - 2, ART - 2, edge)
    art.rect(1, 1, 4, ART - 2, edge)
    art.rect(ART - 5, 1, ART - 2, ART - 2, edge)
    art.frame(OUTLINE)
    for x, y in ((1, 1), (ART - 6, 1), (1, ART - 6), (ART - 6, ART - 6)):
        art.bracket(x, y)


def crack(art, x, y, length):
    for _ in range(length):
        art.px[max(1, min(ART - 2, x)), max(1, min(ART - 2, y))] = OUTLINE
        x += art.rng.choice((-1, 0, 1, 1))
        y += art.rng.choice((0, 1, 1))


def scorch(art, cx, cy, radius):
    for y in range(cy - radius, cy + radius + 1):
        for x in range(cx - radius, cx + radius + 1):
            if 0 < x < ART - 1 and 0 < y < ART - 1:
                d = (x - cx) ** 2 + (y - cy) ** 2
                if d <= radius * radius and art.rng.random() < 0.85:
                    art.px[x, y] = shade(art.px[x, y], -70 if d < radius * radius / 3 else -36)


def draw_broken_top(art):
    draw_crate_top(art)
    scorch(art, 16, 15, 8)
    scorch(art, 25, 24, 4)
    # A hole with snapped boards around it.
    art.rect(12, 11, 19, 18, OUTLINE)
    art.rect(13, 12, 18, 17, rgb("120C08"))
    for x, y, n in ((3, 5, 14), (22, 3, 16), (8, 20, 12), (24, 14, 13)):
        crack(art, x, y, n)


def draw_broken_side(art):
    draw_crate_side(art)
    scorch(art, 10, 8, 6)
    scorch(art, 24, 12, 5)
    for x, y, n in ((6, 2, 14), (17, 1, 18), (26, 4, 12)):
        crack(art, x, y, n)
    # Jagged, burned top edge, because the crate is shot down to half height.
    for x in range(1, ART - 1):
        for y in range(0, 2 + art.rng.randrange(3)):
            art.px[x, y] = OUTLINE


CONCRETE = rgb("8B9298")


def draw_concrete_top(art):
    art.rect(0, 0, ART - 1, ART - 1, CONCRETE)
    art.specks(70, 9)
    art.rect(1, 1, ART - 2, 2, shade(CONCRETE, 42))
    art.rect(1, 1, 2, ART - 2, shade(CONCRETE, 30))
    art.rect(1, ART - 3, ART - 2, ART - 2, shade(CONCRETE, -44))
    art.rect(ART - 3, 1, ART - 2, ART - 2, shade(CONCRETE, -34))
    # Recessed armour plate.
    plate = shade(CONCRETE, -18)
    art.rect(7, 7, ART - 8, ART - 8, plate)
    art.rect(7, 7, ART - 8, 7, shade(CONCRETE, -52))
    art.rect(7, 7, 7, ART - 8, shade(CONCRETE, -46))
    art.rect(7, ART - 8, ART - 8, ART - 8, shade(CONCRETE, 26))
    art.rect(ART - 8, 7, ART - 8, ART - 8, shade(CONCRETE, 20))
    art.frame(OUTLINE)
    for x, y in ((4, 4), (ART - 5, 4), (4, ART - 5), (ART - 5, ART - 5)):
        art.px[x, y] = shade(CONCRETE, 70)
        art.px[x + 1, y + 1] = shade(CONCRETE, -60)
    for x, y in ((10, 10), (ART - 11, 10), (10, ART - 11), (ART - 11, ART - 11)):
        art.px[x, y] = shade(CONCRETE, 44)


def draw_concrete_side(art):
    face = shade(CONCRETE, -24)
    art.rect(0, 0, ART - 1, ART - 1, face)
    art.specks(90, 8)
    # Yellow and black warning stripe: this block cannot be shot through early on.
    yellow, black = rgb("E0B21F"), rgb("231E16")
    for y in range(5, 12):
        for x in range(1, ART - 1):
            art.px[x, y] = yellow if ((x + y) // 4) % 2 == 0 else black
    art.rect(1, 4, ART - 2, 4, shade(face, -40))
    art.rect(1, 12, ART - 2, 12, shade(face, -40))
    art.rect(1, 1, ART - 2, 2, shade(face, 40))
    art.rect(1, ART - 3, ART - 2, ART - 2, shade(face, -36))
    art.frame(OUTLINE)
    for x in (5, ART - 6):
        for y in (17, ART - 7):
            art.px[x, y] = shade(face, 56)
            art.px[x + 1, y + 1] = shade(face, -46)


LEAVES = [rgb("3E6E2A"), rgb("4E8632"), rgb("5F9A39"), rgb("35602A")]


def draw_bush(art):
    """Layered clumps of leaves. Also returns the height of each pixel's clump."""
    art.rect(0, 0, ART - 1, ART - 1, rgb("1F3517"))
    height = [[0.0] * ART for _ in range(ART)]
    rng = art.rng
    for layer in range(90):
        cx, cy = rng.randrange(ART), rng.randrange(ART)
        radius = rng.choice((2, 2, 3, 3, 4))
        tone = rng.choice(LEAVES)
        level = rng.random()
        for y in range(cy - radius, cy + radius + 1):
            for x in range(cx - radius, cx + radius + 1):
                if not (0 <= x < ART and 0 <= y < ART):
                    continue
                dx, dy = x - cx, y - cy
                if dx * dx + dy * dy > radius * radius + 1:
                    continue
                # Lit from the upper left, shaded on the lower right of each clump.
                color = tone
                if dx + dy <= -radius:
                    color = shade(tone, 34)
                elif dx + dy >= radius:
                    color = shade(tone, -30)
                art.px[x, y] = color
                height[y][x] = max(height[y][x], level)
    for _ in range(26):
        x, y = rng.randrange(ART), rng.randrange(ART)
        art.px[x, y] = rgb("9AC95C")
    return height


class TileMesh:
    def __init__(self):
        self.positions = []
        self.normals = []
        self.uvs = []
        self.indices = []

    def quad(self, corners, normal):
        """Each corner is (x, y, z, u, v); they go counter-clockwise seen from outside."""
        start = len(self.positions) // 3
        for x, y, z, u, v in corners:
            self.positions += [x, y, z]
            self.normals += normal
            self.uvs += [u, v]
        self.indices += [start, start + 1, start + 2, start, start + 2, start + 3]


def slot_rect(name):
    """The texture rectangle of a slot: u0, v0 (bottom left), u1, v1."""
    index = SLOTS.index(name)
    x = index % 3 * SLOT + GUTTER
    y = index // 3 * SLOT + GUTTER
    size = ART * SCALE
    return x / ATLAS, 1 - (y + size) / ATLAS, (x + size) / ATLAS, 1 - y / ATLAS


def build_box(top_name, side_name, height):
    mesh = TileMesh()
    u0, v0, u1, v1 = slot_rect(top_name)
    h = HALF
    mesh.quad([(-h, height, h, u0, v0), (h, height, h, u1, v0), (h, height, -h, u1, v1), (-h, height, -h, u0, v1)], [0, 1, 0])

    u0, v0, u1, v1 = slot_rect(side_name)
    top_v = v0 + (v1 - v0) * min(height, 1.0)
    sides = [
        ([(-h, 0, h), (h, 0, h), (h, height, h), (-h, height, h)], [0, 0, 1]),
        ([(h, 0, -h), (-h, 0, -h), (-h, height, -h), (h, height, -h)], [0, 0, -1]),
        ([(h, 0, h), (h, 0, -h), (h, height, -h), (h, height, h)], [1, 0, 0]),
        ([(-h, 0, -h), (-h, 0, h), (-h, height, h), (-h, height, -h)], [-1, 0, 0]),
    ]
    for corners, normal in sides:
        uv = [(u0, v0), (u1, v0), (u1, top_v), (u0, top_v)]
        mesh.quad([c + t for c, t in zip(corners, uv)], normal)
    return mesh


def build_bush(name, leaf_height, seed):
    """Columns of uneven height with a ragged outline. The art supplies the leaves."""
    rng = random.Random(seed)
    n = BUSH_COLUMNS
    step_art = ART / n
    columns = [[None] * n for _ in range(n)]
    for row in range(n):
        for col in range(n):
            # A rounded square with a torn edge, so neighbouring bushes do not look tiled.
            ex = abs((col + 0.5) / n * 2 - 1)
            ey = abs((row + 0.5) / n * 2 - 1)
            if ex ** 4 + ey ** 4 > 1.0 + rng.uniform(-0.55, 0.15):
                continue
            x0, y0 = int(col * step_art), int(row * step_art)
            level = max(leaf_height[y][x] for y in range(y0, y0 + int(step_art)) for x in range(x0, x0 + int(step_art)))
            dome = 1 - (ex * ex + ey * ey) / 2
            value = 0.74 + 0.22 * dome + 0.2 * level
            columns[row][col] = round(value / 0.07) * 0.07

    mesh = TileMesh()
    u0, v0, u1, v1 = slot_rect(name)
    size = 1.0 / n

    def px(col):
        return col * size - 0.5

    def uv(col, row):
        return u0 + (u1 - u0) * col / n, v1 - (v1 - v0) * row / n

    def get(row, col):
        return columns[row][col] if 0 <= row < n and 0 <= col < n else None

    for row in range(n):
        for col in range(n):
            top = columns[row][col]
            if top is None:
                continue
            x0, x1, z0, z1 = px(col), px(col + 1), px(row), px(row + 1)
            mesh.quad([
                (x0, top, z1) + uv(col, row + 1), (x1, top, z1) + uv(col + 1, row + 1),
                (x1, top, z0) + uv(col + 1, row), (x0, top, z0) + uv(col, row)
            ], [0, 1, 0])
            # Walls read a strip of leaves running across the art, so they look leafy, not striped.
            def wall(low):
                span = (top - low) * n
                down = row + 0.5 + span <= n - 0.5
                end = row + 0.5 + span if down else row + 0.5 - span
                return uv(col + 0.5, row + 0.5), uv(col + 0.5, max(0.5, end))

            low = get(row, col + 1) or 0
            if low < top:
                high_uv, low_uv = wall(low)
                mesh.quad([(x1, low, z1) + low_uv, (x1, low, z0) + low_uv, (x1, top, z0) + high_uv, (x1, top, z1) + high_uv], [1, 0, 0])
            low = get(row, col - 1) or 0
            if low < top:
                high_uv, low_uv = wall(low)
                mesh.quad([(x0, low, z0) + low_uv, (x0, low, z1) + low_uv, (x0, top, z1) + high_uv, (x0, top, z0) + high_uv], [-1, 0, 0])
            low = get(row + 1, col) or 0
            if low < top:
                high_uv, low_uv = wall(low)
                mesh.quad([(x0, low, z1) + low_uv, (x1, low, z1) + low_uv, (x1, top, z1) + high_uv, (x0, top, z1) + high_uv], [0, 0, 1])
            low = get(row - 1, col) or 0
            if low < top:
                high_uv, low_uv = wall(low)
                mesh.quad([(x1, low, z0) + low_uv, (x0, low, z0) + low_uv, (x0, top, z0) + high_uv, (x1, top, z0) + high_uv], [0, 0, -1])
    return mesh


def main():
    unity_out = sys.argv[1]
    browser_out = sys.argv[2] if len(sys.argv) > 2 else None
    atlas = Image.new("RGB", (ATLAS, ATLAS), (40, 40, 40))
    models = {}
    bush_heights = {}

    for index, name in enumerate(SLOTS):
        art = Art(1000 + index * 37)
        if name.startswith("bush"):
            bush_heights[name] = draw_bush(art)
        else:
            {"crate_top": draw_crate_top, "crate_side": draw_crate_side, "broken_top": draw_broken_top,
             "broken_side": draw_broken_side, "concrete_top": draw_concrete_top,
             "concrete_side": draw_concrete_side}[name](art)
        x, y = index % 3 * SLOT, index // 3 * SLOT
        # The stretched copy underneath becomes the gutter that stops colours bleeding between slots.
        atlas.paste(art.image.resize((SLOT, SLOT), Image.NEAREST), (x, y))
        atlas.paste(art.image.resize((ART * SCALE, ART * SCALE), Image.NEAREST), (x + GUTTER, y + GUTTER))

    models["crate"] = build_box("crate_top", "crate_side", CRATE_HEIGHT)
    models["crate_broken"] = build_box("broken_top", "broken_side", BROKEN_HEIGHT)
    models["concrete"] = build_box("concrete_top", "concrete_side", CONCRETE_HEIGHT)
    for variant in range(BUSH_VARIANTS):
        name = "bush%d" % variant
        models[name] = build_bush(name, bush_heights[name], 50 + variant)

    atlas.save(os.path.join(unity_out, "atlas.png"))
    if browser_out:
        atlas.save(os.path.join(browser_out, "atlas.png"))
    for name, mesh in models.items():
        write_obj(mesh, os.path.join(unity_out, name + ".obj"))
        if browser_out:
            write_json(mesh, os.path.join(browser_out, name + ".json"))
        print(name, len(mesh.indices) // 3, "triangles")


main()
