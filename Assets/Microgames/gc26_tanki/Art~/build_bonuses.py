"""Builds the bonus pickups: 32x32 shaded pixel drawings puffed up into rounded voxel models.

usage: build_bonuses.py <unity output folder> [<browser output folder>]
Needs Pillow and numpy. Each bonus is made of shapes (drawn big, then shrunk to 32x32):
"body" shapes give the model its outline and are shaded like a rounded surface lit from the top left,
"paint" shapes only recolour what is under them (grooves, bricks, the cross on the shield).
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

from build_models import write_json, write_obj

ART = 32
BIG = 8  # shapes are drawn this many times larger, then shrunk, for clean edges
SCALE = 2
GUTTER = 4
ATLAS = 256
SLOT = ART * SCALE + GUTTER * 2
SIZE = 0.85
FLAT_DEPTH = 0.05
DOME = 0.12  # how much thicker the middle is than the edge
DOME_PIXELS = 6  # how far from the edge the dome reaches full height
LIGHT = np.array([-0.55, 0.6, 0.58])
LIGHT = LIGHT / np.linalg.norm(LIGHT)
BACKGROUND = (21, 19, 15)


def hexes(*colours):
    return [tuple(int(c[i:i + 2], 16) for i in (0, 2, 4)) for c in colours]


# Tones from shadow to highlight, then the outline colour.
RED = hexes("7A0E1E", "B8182F", "E8304A", "FF6B7E", "FFD3D9") + hexes("3A0710")
GOLD = hexes("8A5A06", "C98E10", "F5C42C", "FFE27A", "FFF8D6") + hexes("4A2E04")
BLUE = hexes("0E3F7A", "1B66B8", "2E8FE0", "79C1FF", "E0F2FF") + hexes("08223F")
STEEL = hexes("6F7880", "98A1A8", "C3CACE", "E4E9EC", "FFFFFF") + hexes("2A2F33")
STONE = hexes("4E5357", "6D7377", "989EA1", "C3C8CA", "E6E9EA") + hexes("1F2224")
ICE = hexes("2A78B0", "4FA6DA", "8CD8FF", "C8EEFF", "FFFFFF") + hexes("1F5F92")
GREEN = hexes("26381A", "3E5A26", "587E35", "7FA850", "BFD99A") + hexes("141F0D")
BRASS = hexes("7A5A10", "B8891E", "E3C14A", "F7E08A", "FFF6D0") + hexes("3A2A06")


def star_points(cx, cy, outer, inner, points=5, turn=-90):
    result = []
    for i in range(points * 2):
        r = outer if i % 2 == 0 else inner
        a = math.radians(turn + i * 180 / points)
        result.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return result


def heart_points(cx, cy, size):
    result = []
    for i in range(120):
        t = i / 120 * 2 * math.pi
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        result.append((cx + x * size / 34, cy - y * size / 34))
    return result


def shield_points(cx, top, width, height):
    """Straight sides for the top 40%, then curving in to a point."""
    half = width / 2
    right = [(cx, top + height * 0.06), (cx + half * 0.55, top), (cx + half, top + height * 0.05),
             (cx + half, top + height * 0.4)]
    for i in range(1, 21):
        t = i / 20
        right.append((cx + half * math.cos(t * math.pi / 2) ** 0.9, top + height * (0.4 + 0.6 * math.sin(t * math.pi / 2))))
    left = [(2 * cx - x, y) for x, y in reversed(right[1:-1])]
    return right + left


def line(draw, a, b, width):
    draw.line([a, b], fill=255, width=width)
    for p in (a, b):
        draw.ellipse([p[0] - width / 2, p[1] - width / 2, p[0] + width / 2, p[1] + width / 2], fill=255)


# Each shape is (kind, palette or tone, drawing function); coordinates are in 32x32 pixels.
ICONS = {
    "bomb": [
        ("body", GREEN, lambda d: d.ellipse([7, 9, 25, 30], fill=255)),
        ("paint", 0, lambda d: [d.rectangle([7, 15.2, 25, 16.6], fill=255), d.rectangle([7, 21.6, 25, 23], fill=255),
                                d.rectangle([12.3, 9, 13.7, 30], fill=255), d.rectangle([18.3, 9, 19.7, 30], fill=255)]),
        ("body", STEEL, lambda d: d.rounded_rectangle([12, 5, 20, 11], radius=1.5, fill=255)),
        ("body", STEEL, lambda d: d.polygon([(18, 5), (27, 9), (28, 12), (25, 11), (19, 8)], fill=255)),
        ("body", BRASS, lambda d: [d.ellipse([5, 1, 13, 9], fill=255)]),
        ("hole", None, lambda d: d.ellipse([7.4, 3.4, 10.6, 6.6], fill=255)),
    ],
    "star": [
        ("body", GOLD, lambda d: d.polygon(star_points(16, 17, 15.5, 6.6), fill=255)),
    ],
    "shield": [
        ("body", STEEL, lambda d: d.polygon(shield_points(16, 1.5, 27, 29.5), fill=255)),
        ("body", BLUE, lambda d: d.polygon(shield_points(16, 4.4, 20.6, 23.8), fill=255)),
        ("body", STEEL, lambda d: [d.rectangle([14.2, 8.5, 17.8, 22.5], fill=255), d.rectangle([9.3, 13.4, 22.7, 17], fill=255)]),
    ],
    "fort": [
        ("body", STONE, lambda d: [d.rectangle([5, 9, 27, 30], fill=255)] +
         [d.rectangle([x, 3, x + 5, 10], fill=255) for x in (5, 13.5, 22)]),
        ("paint", 0, lambda d: [d.rectangle([5, y, 27, y + 0.9], fill=255) for y in (13, 18, 23)] +
         [d.rectangle([x, y0, x + 0.9, y0 + 5], fill=255) for x, y0 in ((11, 13), (19, 13), (15, 18), (23, 18), (9, 23))]),
        ("hole", None, lambda d: [d.rectangle([13, 22, 19, 30], fill=255), d.ellipse([13, 19, 19, 25], fill=255)]),
    ],
    "freeze": [
        ("body", ICE, lambda d: [line(d, (16 + 13.5 * math.cos(math.radians(a)), 16 + 13.5 * math.sin(math.radians(a))),
                                     (16 - 13.5 * math.cos(math.radians(a)), 16 - 13.5 * math.sin(math.radians(a))), 3.4)
                                 for a in (90, 30, 150)] +
         [line(d, (16 + 8.6 * math.cos(math.radians(a)), 16 + 8.6 * math.sin(math.radians(a))),
               (16 + 8.6 * math.cos(math.radians(a)) + 3.6 * math.cos(math.radians(a + s)),
                16 + 8.6 * math.sin(math.radians(a)) + 3.6 * math.sin(math.radians(a + s))), 2.6)
          for a in range(0, 360, 60) for s in (-38, 38)] +
         [d.regular_polygon((16, 16, 4.6), 6, fill=255)]),
    ],
    "life": [
        ("body", RED, lambda d: d.polygon(heart_points(16, 15.5, 29), fill=255)),
    ],
}


def mask_of(draw_shape):
    big = Image.new("L", (ART * BIG, ART * BIG), 0)

    class Scaled:
        """Passes 32x32 coordinates and widths to a drawing BIG times larger."""
        def __init__(self, draw):
            self.d = draw

        def _s(self, value):
            if isinstance(value, (int, float)):
                return value * BIG
            if isinstance(value, tuple) and len(value) == 3:
                return (value[0] * BIG, value[1] * BIG, value[2] * BIG)
            if isinstance(value, (list, tuple)):
                return [self._s(v) for v in value] if not all(isinstance(v, (int, float)) for v in value) \
                    else [v * BIG for v in value]
            return value

        def __getattr__(self, name):
            method = getattr(self.d, name)

            def call(xy, *args, **kwargs):
                if "width" in kwargs:
                    kwargs["width"] = int(round(kwargs["width"] * BIG))
                if "radius" in kwargs:
                    kwargs["radius"] = kwargs["radius"] * BIG
                scaled = self._s(xy)
                if name == "regular_polygon":
                    scaled = tuple(scaled)
                elif name == "polygon" or name == "line":
                    scaled = [tuple(p) for p in scaled]
                return method(scaled, *args, **kwargs)
            return call

    draw_shape(Scaled(ImageDraw.Draw(big)))
    small = np.asarray(big.resize((ART, ART), Image.BOX), dtype=np.float32) / 255
    return small >= 0.5


def distance_inside(mask):
    """Pixels from the nearest empty pixel, counting the edge of the picture as empty."""
    dist = np.where(mask, 1e6, 0.0)
    for _ in range(2):
        for y in range(ART):
            for x in range(ART):
                if not mask[y, x]:
                    continue
                best = dist[y, x]
                for dy, dx, step in ((-1, 0, 1), (0, -1, 1), (-1, -1, 1.414), (-1, 1, 1.414)):
                    ny, nx = y + dy, x + dx
                    near = dist[ny, nx] if 0 <= ny < ART and 0 <= nx < ART else 0.0
                    best = min(best, near + step)
                dist[y, x] = best
        for y in range(ART - 1, -1, -1):
            for x in range(ART - 1, -1, -1):
                if not mask[y, x]:
                    continue
                best = dist[y, x]
                for dy, dx, step in ((1, 0, 1), (0, 1, 1), (1, 1, 1.414), (1, -1, 1.414)):
                    ny, nx = y + dy, x + dx
                    near = dist[ny, nx] if 0 <= ny < ART and 0 <= nx < ART else 0.0
                    best = min(best, near + step)
                dist[y, x] = best
    return dist


def dome(dist, reach):
    t = np.clip(dist / reach, 0, 1)
    return np.sqrt(1 - (1 - t) ** 2)


def shade_layer(mask, palette):
    """Colours a shape as a rounded bump lit from the top left: five tones plus a darker rim."""
    height = dome(distance_inside(mask), 7.0) * 4.0
    gy, gx = np.gradient(height)
    normal = np.dstack([-gx, gy, np.ones_like(height)])
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    light = normal @ LIGHT
    yy, xx = np.mgrid[0:ART, 0:ART]
    cy, cx = np.argwhere(mask).mean(axis=0) if mask.any() else (16, 16)
    # A soft fall-off towards the bottom right, so flat middles are not one flat colour.
    rows, cols = np.ptp(np.argwhere(mask), axis=0) + 1 if mask.any() else (ART, ART)
    light = light - 0.35 * ((xx - cx) / cols + (yy - cy) / rows)
    # A shine near the top left of the shape.
    spot = ((xx - (cx - cols * 0.22)) / (cols * 0.14)) ** 2 + ((yy - (cy - rows * 0.22)) / (rows * 0.11)) ** 2
    light = np.where(spot < 1, 1.0, light)
    tone = np.digitize(light, [0.3, 0.5, 0.7, 0.95])
    colours = np.array(palette[:5], dtype=np.uint8)[tone]
    return colours


def draw_icon(shapes):
    picture = np.zeros((ART, ART, 3), dtype=np.uint8)
    picture[:] = BACKGROUND
    solid = np.zeros((ART, ART), dtype=bool)
    rims = []
    last_palette = None
    for kind, palette, draw_shape in shapes:
        mask = mask_of(draw_shape)
        if kind == "body":
            picture[mask] = shade_layer(mask, palette)[mask]
            solid |= mask
            rims.append((mask, palette[5]))
            last_palette = palette
        elif kind == "paint":
            picture[mask & solid] = last_palette[palette]
        elif kind == "hole":
            solid &= ~mask
            picture[~solid] = BACKGROUND
    # Each body gets a one-pixel rim where it borders empty space or a body under it.
    outline = np.zeros((ART, ART), dtype=bool)
    for y in range(ART):
        for x in range(ART):
            if not solid[y, x]:
                continue
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if not (0 <= ny < ART and 0 <= nx < ART) or not solid[ny, nx]:
                    outline[y, x] = True
    for i, (mask, rim) in enumerate(rims):
        above = np.zeros((ART, ART), dtype=bool)
        for later, _ in rims[i + 1:]:
            above |= later
        visible = mask & ~above & solid
        edge = outline & visible
        if i > 0:
            # where this body meets the one under it, draw its own rim too
            under = np.zeros((ART, ART), dtype=bool)
            for earlier, _ in rims[:i]:
                under |= earlier
            for y, x in np.argwhere(visible):
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < ART and 0 <= nx < ART and solid[ny, nx] and not mask[ny, nx]:
                        edge[y, x] = True
        picture[edge] = rim
    picture[~solid] = BACKGROUND
    return picture, solid


class Mesh:
    def __init__(self):
        self.positions = []
        self.normals = []
        self.uvs = []
        self.indices = []

    def quad(self, corners, normal, uv):
        """Corners go counter-clockwise seen from outside; uv is either one point or a corner list."""
        start = len(self.positions) // 3
        for i, corner in enumerate(corners):
            self.positions += list(corner)
            self.normals += normal
            self.uvs += list(uv[i] if isinstance(uv, list) else uv)
        self.indices += [start, start + 1, start + 2, start, start + 2, start + 3]


def rounding_depths(solid):
    """Half thickness of each pixel: a dome, thicker further from the edge, in a few steps."""
    dist = distance_inside(solid)
    depths = {}
    for r, c in np.argwhere(solid):
        level = round(float(dome(np.array(dist[r, c] - 1), DOME_PIXELS)) * 4) / 4
        depths[(int(r), int(c))] = FLAT_DEPTH + DOME * level
    return depths


def build_icon(solid, slot):
    x_slot = slot % 3 * SLOT + GUTTER
    y_slot = slot // 3 * SLOT + GUTTER
    depths = rounding_depths(solid)
    step = SIZE / ART
    half = SIZE / 2
    mesh = Mesh()

    def u(col):
        return (x_slot + col * SCALE) / ATLAS

    def v(row):
        return 1 - (y_slot + row * SCALE) / ATLAS

    for (r, c), d in depths.items():
        x0, x1 = c * step - half, (c + 1) * step - half
        y1, y0 = half - r * step, half - (r + 1) * step
        centre = (u(c + 0.5), v(r + 0.5))
        rect = [(u(c), v(r + 1)), (u(c + 1), v(r + 1)), (u(c + 1), v(r)), (u(c), v(r))]

        mesh.quad([(x0, y0, d), (x1, y0, d), (x1, y1, d), (x0, y1, d)], [0, 0, 1], rect)
        mesh.quad([(x1, y0, -d), (x0, y0, -d), (x0, y1, -d), (x1, y1, -d)], [0, 0, -1], [rect[1], rect[0], rect[3], rect[2]])

        def spans(neighbour):
            other = depths.get(neighbour)
            if other is None:
                return [(-d, d)]
            return [(other, d), (-d, -other)] if other < d else []

        for z0, z1 in spans((r, c + 1)):
            mesh.quad([(x1, y0, z1), (x1, y0, z0), (x1, y1, z0), (x1, y1, z1)], [1, 0, 0], centre)
        for z0, z1 in spans((r, c - 1)):
            mesh.quad([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], [-1, 0, 0], centre)
        for z0, z1 in spans((r - 1, c)):
            mesh.quad([(x0, y1, z1), (x1, y1, z1), (x1, y1, z0), (x0, y1, z0)], [0, 1, 0], centre)
        for z0, z1 in spans((r + 1, c)):
            mesh.quad([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], [0, -1, 0], centre)
    return mesh


def main():
    unity_out = sys.argv[1]
    browser_out = sys.argv[2] if len(sys.argv) > 2 else None
    atlas = Image.new("RGB", (ATLAS, ATLAS), BACKGROUND)
    for slot, (name, shapes) in enumerate(ICONS.items()):
        picture, solid = draw_icon(shapes)
        x, y = slot % 3 * SLOT + GUTTER, slot // 3 * SLOT + GUTTER
        art = Image.fromarray(picture, "RGB")
        atlas.paste(art.resize((ART * SCALE, ART * SCALE), Image.NEAREST), (x, y))

        mesh = build_icon(solid, slot)
        write_obj(mesh, os.path.join(unity_out, name + ".obj"))
        if browser_out:
            write_json(mesh, os.path.join(browser_out, name + ".json"))
        print(name, len(mesh.indices) // 3, "triangles")

    atlas.save(os.path.join(unity_out, "atlas.png"))
    if browser_out:
        atlas.save(os.path.join(browser_out, "atlas.png"))


main()
