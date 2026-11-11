"""Raises the flat tank drawings into 3D voxel models. The drawing stays the texture.

usage: build_models.py <folder with player.png, enemy.png> <unity output folder> [<browser output folder>]
Needs Pillow. Each part of a tank is listed below with a height, in map cells; to change
a model, edit the shapes and run the script again. Shape coordinates are pixels of the
512x512 sprite, later shapes paint over earlier ones.

Space used here: x right, y up, z towards the back of the tank (three.js convention),
so the barrel points to -z.
"""
import json
import os
import sys
from PIL import Image

GRID = 96
WALL_UV_INSET = 0.3


def rect(x0, y0, x1, y1):
    return lambda x, y: x0 <= x <= x1 and y0 <= y <= y1


def circle(cx, cy, r):
    return lambda x, y: (x - cx) ** 2 + (y - cy) ** 2 <= r * r


def octagon(x0, y0, x1, y1, cut):
    def inside(x, y):
        if not (x0 <= x <= x1 and y0 <= y <= y1):
            return False
        dx = min(x - x0, x1 - x)
        dy = min(y - y0, y1 - y)
        return dx + dy >= cut
    return inside


# Each part: (shape, bottom, top). The first entry of "parts" covers the whole silhouette.
TANKS = {
    "player": {
        "cells": 1.765,
        "parts": [
            (rect(0, 0, 512, 512), 0.0, 0.3),                # hull
            (rect(0, 0, 168, 512), 0.0, 0.17),               # left track
            (rect(346, 0, 512, 512), 0.0, 0.17),             # right track
            (rect(155, 118, 198, 168), 0.0, 0.36),           # front left fender
            (rect(316, 118, 360, 168), 0.0, 0.36),           # front right fender
            (rect(155, 365, 198, 396), 0.0, 0.26),           # rear left fender
            (rect(316, 365, 360, 396), 0.0, 0.26),           # rear right fender
            (rect(208, 285, 306, 372), 0.0, 0.36),           # engine deck
            (circle(231, 306, 15), 0.0, 0.33),               # left fan
            (circle(284, 306, 15), 0.0, 0.33),               # right fan
            (octagon(192, 156, 326, 288, 26), 0.0, 0.56),    # turret
            (circle(229, 218, 18), 0.0, 0.64),               # left hatch
            (circle(286, 218, 18), 0.0, 0.64),               # right hatch
            (rect(240, 132, 274, 158), 0.0, 0.54),           # gun mantlet
            (rect(245, 0, 271, 132), 0.42, 0.52),            # barrel
        ],
    },
    "enemy": {
        "cells": 1.31,
        "parts": [
            (rect(0, 0, 512, 512), 0.0, 0.3),                # hull
            (rect(0, 0, 207, 512), 0.0, 0.2),                # left track and skirt
            (rect(305, 0, 512, 512), 0.0, 0.2),              # right track and skirt
            (rect(166, 84, 208, 136), 0.0, 0.34),            # front left fender
            (rect(304, 84, 348, 136), 0.0, 0.34),            # front right fender
            (rect(166, 335, 210, 428), 0.0, 0.34),           # rear left fender
            (rect(302, 335, 348, 428), 0.0, 0.34),           # rear right fender
            (rect(0, 288, 170, 372), 0.0, 0.26),             # left side skirt
            (rect(342, 288, 512, 372), 0.0, 0.26),           # right side skirt
            (octagon(192, 174, 322, 318, 28), 0.0, 0.56),    # turret
            (circle(233, 247, 22), 0.0, 0.65),               # commander's cupola
            (circle(286, 240, 15), 0.0, 0.62),               # loader's hatch
            (rect(244, 150, 268, 182), 0.0, 0.5),            # gun mantlet
            (rect(247, 0, 266, 150), 0.38, 0.48),            # barrel
        ],
    },
}


def build_columns(image, parts):
    """Returns a GRID x GRID table of (bottom, top), or None where the sprite is empty."""
    step = image.size[0] / GRID
    alpha = image.getchannel("A").load()
    columns = [[None] * GRID for _ in range(GRID)]
    for row in range(GRID):
        for col in range(GRID):
            x = (col + 0.5) * step
            y = (row + 0.5) * step
            if alpha[int(x), int(y)] < 128:
                continue
            for shape, bottom, top in parts:
                if shape(x, y):
                    columns[row][col] = (bottom, top)
    return columns


class Mesh:
    def __init__(self, cells):
        self.scale = cells / GRID
        self.half = cells / 2
        self.positions = []
        self.normals = []
        self.uvs = []
        self.indices = []

    def corner(self, gx, height, gz, normal, cell):
        self.positions += [gx * self.scale - self.half, height, gz * self.scale - self.half]
        self.normals += normal
        # A wall on the silhouette's edge must not read the empty pixels next to it,
        # so its texture coordinates stay inside its own column.
        u, v = gx, gz
        if cell:
            u = min(max(u, cell[0] + WALL_UV_INSET), cell[0] + 1 - WALL_UV_INSET)
            v = min(max(v, cell[1] + WALL_UV_INSET), cell[1] + 1 - WALL_UV_INSET)
        self.uvs += [u / GRID, 1 - v / GRID]

    def quad(self, corners, normal, cell=None):
        """Corners go counter-clockwise seen from outside."""
        start = len(self.positions) // 3
        for gx, height, gz in corners:
            self.corner(gx, height, gz, normal, cell)
        self.indices += [start, start + 1, start + 2, start, start + 2, start + 3]


def merge_rectangles(columns, value_of):
    """Greedily merges cells with an equal value. The texture carries the detail, so this is lossless."""
    used = [[False] * GRID for _ in range(GRID)]
    for row in range(GRID):
        for col in range(GRID):
            value = value_of(columns[row][col])
            if used[row][col] or value is None:
                continue
            width = 1
            while col + width < GRID and not used[row][col + width] and value_of(columns[row][col + width]) == value:
                width += 1
            height = 1
            while row + height < GRID and all(
                not used[row + height][c] and value_of(columns[row + height][c]) == value
                for c in range(col, col + width)
            ):
                height += 1
            for r in range(row, row + height):
                for c in range(col, col + width):
                    used[r][c] = True
            yield col, row, width, height, value


def exposed_spans(span, neighbour):
    """Parts of a column's side that the neighbouring column does not cover."""
    bottom, top = span
    if neighbour is None:
        return [(bottom, top)]
    other_bottom, other_top = neighbour
    result = []
    if other_top < top:
        result.append((max(bottom, other_top), top))
    if other_bottom > bottom:
        result.append((bottom, min(top, other_bottom)))
    return [s for s in result if s[1] > s[0]]


def build_mesh(columns, cells):
    mesh = Mesh(cells)

    def get(row, col):
        return columns[row][col] if 0 <= row < GRID and 0 <= col < GRID else None

    for col, row, width, depth, top in merge_rectangles(columns, lambda c: c and c[1]):
        mesh.quad([(col, top, row + depth), (col + width, top, row + depth), (col + width, top, row), (col, top, row)], [0, 1, 0])

    for col, row, width, depth, bottom in merge_rectangles(columns, lambda c: c[0] if c and c[0] > 0 else None):
        mesh.quad([(col, bottom, row), (col + width, bottom, row), (col + width, bottom, row + depth), (col, bottom, row + depth)], [0, -1, 0])

    for row in range(GRID):
        for col in range(GRID):
            span = columns[row][col]
            if span is None:
                continue
            for bottom, top in exposed_spans(span, get(row, col + 1)):  # right
                x = col + 1
                mesh.quad([(x, bottom, row + 1), (x, bottom, row), (x, top, row), (x, top, row + 1)], [1, 0, 0], (col, row))
            for bottom, top in exposed_spans(span, get(row, col - 1)):  # left
                x = col
                mesh.quad([(x, bottom, row), (x, bottom, row + 1), (x, top, row + 1), (x, top, row)], [-1, 0, 0], (col, row))
            for bottom, top in exposed_spans(span, get(row + 1, col)):  # back
                z = row + 1
                mesh.quad([(col, bottom, z), (col + 1, bottom, z), (col + 1, top, z), (col, top, z)], [0, 0, 1], (col, row))
            for bottom, top in exposed_spans(span, get(row - 1, col)):  # front
                z = row
                mesh.quad([(col + 1, bottom, z), (col, bottom, z), (col, top, z), (col + 1, top, z)], [0, 0, -1], (col, row))
    return mesh


def write_obj(mesh, path):
    """Turned 180 degrees around y: Unity negates x on import, which leaves the barrel on +z."""
    lines = []
    count = len(mesh.positions) // 3
    for i in range(count):
        x, y, z = mesh.positions[i * 3:i * 3 + 3]
        lines.append("v %.4f %.4f %.4f" % (-x, y, -z))
    for i in range(count):
        lines.append("vt %.4f %.4f" % (mesh.uvs[i * 2], mesh.uvs[i * 2 + 1]))
    for i in range(count):
        x, y, z = mesh.normals[i * 3:i * 3 + 3]
        lines.append("vn %d %d %d" % (-x, y, -z))
    for i in range(0, len(mesh.indices), 3):
        a, b, c = (mesh.indices[i + k] + 1 for k in range(3))
        lines.append("f %d/%d/%d %d/%d/%d %d/%d/%d" % (a, a, a, b, b, b, c, c, c))
    with open(path, "w", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")


def write_json(mesh, path):
    data = {
        "positions": [round(v, 4) for v in mesh.positions],
        "normals": mesh.normals,
        "uvs": [round(v, 4) for v in mesh.uvs],
        "indices": mesh.indices,
    }
    with open(path, "w") as handle:
        json.dump(data, handle, separators=(",", ":"))


def main():
    sprites, unity_out = sys.argv[1], sys.argv[2]
    browser_out = sys.argv[3] if len(sys.argv) > 3 else None
    for name, tank in TANKS.items():
        image = Image.open(os.path.join(sprites, name + ".png")).convert("RGBA")
        mesh = build_mesh(build_columns(image, tank["parts"]), tank["cells"])
        write_obj(mesh, os.path.join(unity_out, name + "_model.obj"))
        if browser_out:
            write_json(mesh, os.path.join(browser_out, name + "_model.json"))
        print(name, len(mesh.indices) // 3, "triangles")


if __name__ == "__main__":
    main()
