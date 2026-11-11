"""Builds the bonus pickups: 16x16 pixel drawings puffed up into rounded voxel models.

usage: build_bonuses.py <unity output folder> [<browser output folder>]
Needs Pillow. To change a bonus, edit its drawing below and run the script again.
Each letter is a colour from PALETTE; a dot is empty.
"""
import os
import sys
from PIL import Image

from build_models import write_json, write_obj

ART = 16
SCALE = 4
GUTTER = 4
ATLAS = 256
SLOT = ART * SCALE + GUTTER * 2
SIZE = 0.85
FLAT_DEPTH = 0.05
DEPTH_STEP = 0.035
MAX_ROUNDING = 3

PALETTE = {
    "o": "15130F",  # outline
    "g": "4C6B2E", "G": "6E9440", "d": "2F441D",  # grenade body
    "m": "B4B9BD", "M": "6B7075", "y": "E3C14A",  # lever, cap and pull ring
    "Y": "F7C92E", "A": "FFE98A", "a": "C9920F",  # star
    "B": "2E8FE0", "C": "8CCBFF", "N": "155A9E", "W": "F2F6F8",  # shield
    "S": "A5ABAE", "T": "C9CED0", "D": "6D7377", "K": "1D1A18",  # fort
    "I": "8CD8FF", "J": "E9F8FF", "n": "2C7FB8",  # snowflake
    "R": "EE2F45", "L": "FF9AA6", "E": "A5142A",  # heart
}

ICONS = {
    "bomb": [
        "........mmmm....",
        ".....yy.MMMmm...",
        "....y..yMMM.mm..",
        ".....yy.MMM..m..",
        "......ooooo.....",
        "....oogGgGgoo...",
        "...ogGGdGGdGGo..",
        "..ogGGgdGgdGGgo.",
        "..odddddddddddo.",
        "..ogGGgdGgdGgdo.",
        "..ogGgdGgdGgdgo.",
        "..odddddddddddo.",
        "...ogGgdGgdGdo..",
        "...ogGgdGgdgdo..",
        "....ooddddoo....",
        "......oooo......",
    ],
    "star": [
        ".......oo.......",
        "......oAAo......",
        "......oAYo......",
        ".....oAYYYo.....",
        "oooooAAYYYYooooo",
        "oAAAAYYYYYYYYaao",
        ".oAYYYYYYYYYYao.",
        "..oAYYYYYYYYao..",
        "...oYYYYYYYao...",
        "...oYYYYYYYao...",
        "..oAYYYooYYYao..",
        "..oAYYo..oYYao..",
        ".oAYoo....ooao..",
        ".ooo........ooo.",
        "................",
        "................",
    ],
    "shield": [
        "...oooooooooo...",
        "..oCCCCBBBBBBo..",
        "..oCBBBBBBBBNo..",
        "..oCBBBWWBBBNo..",
        "..oCBBBWWBBBNo..",
        "..oCBWWWWWWBNo..",
        "..oCBWWWWWWBNo..",
        "..oCBBBWWBBBNo..",
        "..oCBBBWWBBBNo..",
        "...oCBBWWBBNo...",
        "...oCBBBBBBNo...",
        "....oCBBBBNo....",
        ".....oCBBNo.....",
        "......oCNo......",
        ".......oo.......",
        "................",
    ],
    "fort": [
        "..ooo.oooo.ooo..",
        "..oTo.oTTo.oTo..",
        "..oSoooSSoooSo..",
        "..oTTTTTTTTTTo..",
        "..oDDDDDDDDDDo..",
        "..oSSDSSSDSSSo..",
        "..oSSDSSSDSSSo..",
        "..oDDDDDDDDDDo..",
        "..oSDSSSDSSDSo..",
        "..oSDSSSDSSDSo..",
        "..oDDDDooDDDDo..",
        "..oSSSoKKoSSSo..",
        "..oSSSoKKoSSSo..",
        "..oDDDoKKoDDDo..",
        "..oooooooooooo..",
        "................",
    ],
    "freeze": [
        ".......II.......",
        "..n...nIIn...n..",
        "...I...II...I...",
        "....I..II..I....",
        ".....I.II.I.....",
        "..n...IJJI...n..",
        ".nIIIIJJJJIIIIn.",
        ".nIIIIJJJJIIIIn.",
        "..n...IJJI...n..",
        ".....I.II.I.....",
        "....I..II..I....",
        "...I...II...I...",
        "..n...nIIn...n..",
        ".......II.......",
        "................",
        "................",
    ],
    "life": [
        "................",
        "..ooo......ooo..",
        ".oRRRo....oRRRo.",
        "oRLLRRo..oRRRRRo",
        "oRLRRRRooRRRRRRo",
        "oRRRRRRRRRRRRRRo",
        "oRRRRRRRRRRRRREo",
        ".oRRRRRRRRRRREo.",
        "..oRRRRRRRRREo..",
        "...oRRRRRRREo...",
        "....oRRRRREo....",
        ".....oRRREo.....",
        "......oREo......",
        ".......oo.......",
        "................",
        "................",
    ],
}


def rgb(text):
    return tuple(int(text[i:i + 2], 16) for i in (0, 2, 4))


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


def rounding_depths(rows):
    """Half thickness of each pixel: thicker further from the edge, so drawings look puffed up."""
    distance = {}
    frontier = []
    for r in range(ART):
        for c in range(ART):
            if rows[r][c] == ".":
                continue
            edge = any(
                not (0 <= r + dr < ART and 0 <= c + dc < ART) or rows[r + dr][c + dc] == "."
                for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1))
            )
            if edge:
                distance[(r, c)] = 0
                frontier.append((r, c))
    while frontier:
        next_frontier = []
        for r, c in frontier:
            for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                cell = (r + dr, c + dc)
                if 0 <= cell[0] < ART and 0 <= cell[1] < ART and rows[cell[0]][cell[1]] != "." and cell not in distance:
                    distance[cell] = distance[(r, c)] + 1
                    next_frontier.append(cell)
        frontier = next_frontier
    return {cell: FLAT_DEPTH + DEPTH_STEP * min(d, MAX_ROUNDING) for cell, d in distance.items()}


def build_icon(rows, slot):
    x_slot = slot % 3 * SLOT + GUTTER
    y_slot = slot // 3 * SLOT + GUTTER
    depths = rounding_depths(rows)
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
    atlas = Image.new("RGB", (ATLAS, ATLAS), (21, 19, 15))
    for slot, (name, rows) in enumerate(ICONS.items()):
        assert len(rows) == ART and all(len(r) == ART for r in rows), name
        art = Image.new("RGB", (ART, ART), (21, 19, 15))
        for r, row in enumerate(rows):
            for c, letter in enumerate(row):
                if letter != ".":
                    art.putpixel((c, r), rgb(PALETTE[letter]))
        x, y = slot % 3 * SLOT + GUTTER, slot // 3 * SLOT + GUTTER
        atlas.paste(art.resize((ART * SCALE, ART * SCALE), Image.NEAREST), (x, y))

        mesh = build_icon(rows, slot)
        write_obj(mesh, os.path.join(unity_out, name + ".obj"))
        if browser_out:
            write_json(mesh, os.path.join(browser_out, name + ".json"))
        print(name, len(mesh.indices) // 3, "triangles")

    atlas.save(os.path.join(unity_out, "atlas.png"))
    if browser_out:
        atlas.save(os.path.join(browser_out, "atlas.png"))


main()
