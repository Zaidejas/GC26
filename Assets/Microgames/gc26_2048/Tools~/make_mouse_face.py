"""Draws the mouse sprites in Resources/gc26_2048/:

- mouse_face.png: head and paws for the idle "peek", cut off flat at the bottom (that edge stays behind the
  screen border while the mouse peeks out);
- mouse_run.png: the whole mouse (body, head, paws, tail) that runs over the field and eats cheese.

Shapes follow the mouse from the HTML prototype (HERO svg, viewBox 0 0 120 100). Python 3.8+, no libraries.
Run from anywhere: python make_mouse_face.py
"""
import os
import struct
import zlib

INK = (0x3a, 0x2a, 0x1c)
FUR = (0xa9, 0xab, 0xb6)
BELLY = (0xd9, 0xdb, 0xe3)
EAR = (0xf6, 0xb6, 0xc2)
NOSE = (0xf0, 0x8a, 0xa0)
TAIL = (0xc9, 0x8b, 0x96)
WHITE = (0xff, 0xff, 0xff)

SS = 3  # supersampling per axis


def ellipse(cx, cy, rx, ry, color, stroke=0.0):
    """Filled ellipse with an outline of width `stroke` (painted as an outer ellipse, then the fill)."""
    shapes = []
    if stroke:
        shapes.append(("e", cx, cy, rx + stroke / 2, ry + stroke / 2, INK))
    shapes.append(("e", cx, cy, rx - stroke / 2, ry - stroke / 2, color))
    return shapes


def line(x1, y1, x2, y2, width, color=INK):
    return [("l", x1, y1, x2, y2, width / 2, color)]


def bezier(points, width, color, steps=16):
    """Cubic bezier chain (M p0 C p1 p2 p3 C ...) as short line segments."""
    shapes = []
    p0 = points[0]
    for i in range(1, len(points), 3):
        p1, p2, p3 = points[i], points[i + 1], points[i + 2]
        prev = p0
        for s in range(1, steps + 1):
            t = s / steps
            u = 1 - t
            x = u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0]
            y = u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]
            shapes += line(prev[0], prev[1], x, y, width, color)
            prev = (x, y)
        p0 = p3
    return shapes


HEAD = (
    ellipse(44, 40, 10, 10, FUR, 2.5) + ellipse(44, 40, 6, 6, EAR)
    + ellipse(76, 40, 10, 10, FUR, 2.5) + ellipse(76, 40, 6, 6, EAR)
    + ellipse(60, 52, 15, 13, FUR, 2.5)
    + ellipse(54, 50, 2.6, 2.6, INK) + ellipse(66, 50, 2.6, 2.6, INK)
    + ellipse(54.8, 49.2, 0.9, 0.9, WHITE) + ellipse(66.8, 49.2, 0.9, 0.9, WHITE)
    + ellipse(60, 57.5, 3, 2.3, NOSE, 1.5)
    + line(52, 58, 40, 58, 1.2) + line(52, 60.5, 41, 63, 1.2)
    + line(68, 58, 80, 58, 1.2) + line(68, 60.5, 79, 63, 1.2)
)

FACE = (
    ellipse(60, 74, 17, 15, FUR, 2.5)          # body, mostly below the cut
    + ellipse(60, 78, 10, 9, BELLY)
    + HEAD
    + ellipse(47, 67.5, 5, 3.6, FUR, 2) + ellipse(73, 67.5, 5, 3.6, FUR, 2)  # paws holding the edge
)

RUN = (
    bezier([(72, 84), (88, 86), (96, 78), (92, 70), (89, 64), (82, 68), (86, 72)], 2.6, TAIL)
    + ellipse(60, 74, 17, 15, FUR, 2.5)
    + ellipse(60, 78, 10, 9, BELLY)
    + HEAD
    + ellipse(50, 74, 4.5, 3.5, FUR, 2) + ellipse(70, 74, 4.5, 3.5, FUR, 2)  # front paws
    + ellipse(50, 88, 5, 3, FUR, 2) + ellipse(70, 88, 5, 3, FUR, 2)          # feet
)

SPRITES = {
    # name: (shapes, crop box in prototype units x0, y0, x1, y1, pixels per unit)
    "mouse_face.png": (FACE, (28.0, 27.0, 92.0, 70.0), 8),
    "mouse_run.png": (RUN, (28.0, 27.0, 98.0, 93.0), 6),
}


def inside(shape, x, y):
    if shape[0] == "e":
        _, cx, cy, rx, ry, _ = shape
        if rx <= 0 or ry <= 0:
            return False
        dx, dy = (x - cx) / rx, (y - cy) / ry
        return dx * dx + dy * dy <= 1.0
    _, x1, y1, x2, y2, r, _ = shape
    vx, vy = x2 - x1, y2 - y1
    length = vx * vx + vy * vy
    t = 0.0 if length == 0 else max(0.0, min(1.0, ((x - x1) * vx + (y - y1) * vy) / length))
    px, py = x1 + t * vx - x, y1 + t * vy - y
    return px * px + py * py <= r * r


def bounds(shape):
    if shape[0] == "e":
        _, cx, cy, rx, ry, _ = shape
        return cx - rx, cy - ry, cx + rx, cy + ry
    _, x1, y1, x2, y2, r, _ = shape
    return min(x1, x2) - r, min(y1, y2) - r, max(x1, x2) + r, max(y1, y2) + r


def render(shapes, box, scale):
    x0, y0, x1, y1 = box
    w, h = int((x1 - x0) * scale), int((y1 - y0) * scale)
    boxes = [bounds(s) for s in shapes]
    rows = []
    n = SS * SS
    for py in range(h):
        row = bytearray([0])  # PNG filter: none
        for px in range(w):
            r = g = b = a = 0
            for sy in range(SS):
                y = y0 + (py + (sy + 0.5) / SS) / scale
                for sx in range(SS):
                    x = x0 + (px + (sx + 0.5) / SS) / scale
                    color = None
                    for shape, (bx0, by0, bx1, by1) in zip(shapes, boxes):
                        if bx0 <= x <= bx1 and by0 <= y <= by1 and inside(shape, x, y):
                            color = shape[-1]
                    if color:
                        r += color[0]; g += color[1]; b += color[2]; a += 1
            if a:
                row += bytes((r // a, g // a, b // a, 255 * a // n))
            else:
                row += bytes((0, 0, 0, 0))
        rows.append(bytes(row))
    return w, h, b"".join(rows)


def png(w, h, raw):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


if __name__ == "__main__":
    folder = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Resources", "gc26_2048")
    for name, (shapes, box, scale) in SPRITES.items():
        out = os.path.join(folder, name)
        w, h, raw = render(shapes, box, scale)
        with open(out, "wb") as f:
            f.write(png(w, h, raw))
        print(f"{os.path.normpath(out)}: {w}x{h}")
