"""Cuts a tank out of a flat dark background and centres it on the hull.

usage: cut_sprite.py input.jpg output.png bg_threshold preview.png
Needs Pillow. The sprite must point up and have a near-black outline.
"""
import sys
from collections import deque
from PIL import Image, ImageDraw, ImageFilter

OUTPUT_SIZE = 512
MIN_HULL_ROW_WIDTH = 200
CLOSE_SIZE = 9


def background_mask(im, threshold):
    w, h = im.size
    px = im.load()
    seen = bytearray(w * h)
    queue = deque()

    def is_background(x, y):
        r, g, b = px[x, y]
        neutral = max(r, g, b) - min(r, g, b) <= 8
        return neutral and min(r, g, b) >= threshold

    for x in range(w):
        for y in (0, h - 1):
            queue.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            queue.append((x, y))

    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y * w + x]:
            continue
        if not is_background(x, y):
            continue
        seen[y * w + x] = 1
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return seen


def solid_silhouette(mask, size):
    """Closes small gaps in the outline, fills everything inside it, drops stray specks."""
    w, h = size
    tank = Image.new("L", size)
    tank.putdata([0 if mask[i] else 255 for i in range(w * h)])

    closed = tank.filter(ImageFilter.MaxFilter(CLOSE_SIZE))
    ImageDraw.floodfill(closed, (0, 0), 128)
    solid = closed.point(lambda v: 0 if v == 128 else 255).filter(ImageFilter.MinFilter(CLOSE_SIZE))

    ImageDraw.floodfill(solid, (w // 2, h // 2), 200)
    data = solid.getdata()
    return bytearray(0 if data[i] == 200 else 1 for i in range(w * h))


def main():
    source, target, threshold, preview = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
    im = Image.open(source).convert("RGB")
    w, h = im.size
    mask = solid_silhouette(background_mask(im, threshold), im.size)

    cut = Image.new("RGBA", im.size)
    out = cut.load()
    src = im.load()
    for y in range(h):
        for x in range(w):
            if not mask[y * w + x]:
                out[x, y] = src[x, y] + (255,)

    # Rows wider than the barrel are the hull; its middle becomes the turning point.
    rows = []
    for y in range(h):
        xs = [x for x in range(w) if not mask[y * w + x]]
        if xs:
            rows.append((y, min(xs), max(xs)))
    top = rows[0][0]
    bottom = rows[-1][0]
    hull = [r for r in rows if r[2] - r[1] >= MIN_HULL_ROW_WIDTH]
    hull_top, hull_bottom = hull[0][0], hull[-1][0]
    left = min(r[1] for r in hull)
    right = max(r[2] for r in hull)
    pivot_x = (left + right) // 2
    pivot_y = (hull_top + hull_bottom) // 2

    half = max(pivot_y - top, bottom - pivot_y, pivot_x - left, right - pivot_x) + 4
    canvas = Image.new("RGBA", (half * 2, half * 2))
    canvas.paste(cut.crop((left - 8, top, right + 9, bottom + 1)), (half - (pivot_x - left) - 8, half - (pivot_y - top)))
    final = canvas.resize((OUTPUT_SIZE, OUTPUT_SIZE), Image.LANCZOS)
    final.save(target)

    print(target.rsplit("\\", 1)[-1], "hull", right - left + 1, "x", hull_bottom - hull_top + 1,
          "side", half * 2, "barrel_above_hull", hull_top - top)

    sand = Image.new("RGBA", final.size, (217, 188, 140, 255))
    sand.alpha_composite(final)
    sand.convert("RGB").save(preview)


main()
