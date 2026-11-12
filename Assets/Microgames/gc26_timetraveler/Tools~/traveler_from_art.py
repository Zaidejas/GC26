"""Builds the traveler's game frames from the artist's own sprite (48x64, facing right, mid-stride).
run0 is her drawing as is (two 1 px holes in the shirt filled). The other frames reuse her head, body,
shorts, boots and sock colours: legs are re-posed, the body bobs, the back arm swings a little.
Writes run0..run5, jump, fall, slide (+ *_aura rings) to <out_dir>.
Usage: python traveler_from_art.py <her_sprite.png> <out_dir> [sheet.png]   (needs Pillow)
"""
import math, os, sys
from PIL import Image

PAL = dict(
    M=(126, 126, 143), N=(194, 194, 209), C=(255, 255, 255), E=(0, 0, 0),
    T=(71, 59, 120), K=(50, 41, 71), Q=(39, 39, 54), L=(99, 40, 40), O=(150, 88, 40),
)


def load(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    px = {}
    for y in range(h):
        for x in range(w):
            p = im.getpixel((x, y))
            if p[3] >= 128:
                px[(x, y)] = p[:3]
    return px, w, h


def fill_holes(px):
    """Single transparent pixels closed in on all four sides (holes in the shirt) take the colour above."""
    xs = [x for x, _ in px]
    ys = [y for _, y in px]
    for y in range(min(ys), max(ys) + 1):
        for x in range(min(xs), max(xs) + 1):
            if (x, y) not in px and all((x + dx, y + dy) in px for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[(x, y)] = px[(x, y - 1)]


def take(px, test):
    out = {}
    for (x, y), c in list(px.items()):
        if test(x, y, c):
            out[(x, y)] = c
            del px[(x, y)]
    return out


def split(px):
    """Her sprite -> body (head, torso, shorts, front arm), back arm, front leg, back leg, boots."""
    px = dict(px)
    sock = {PAL["M"], PAL["N"], PAL["C"], PAL["E"]}
    boot = {PAL["T"], PAL["K"], PAL["Q"]}
    parts = {}
    parts["front_boot"] = take(px, lambda x, y, c: 24 <= x <= 36 and 53 <= y <= 60 and c in boot)
    parts["front_sock"] = take(px, lambda x, y, c: 25 <= x <= 30 and 51 <= y <= 55 and c in sock)
    parts["back_boot"] = take(px, lambda x, y, c: x <= 13 and 44 <= y <= 51 and c in boot)
    parts["back_sock"] = take(px, lambda x, y, c: (13 <= x <= 19 and 46 <= y <= 53 and c in sock)
                              or (17 <= x <= 19 and 52 <= y <= 53))
    parts["arm"] = take(px, lambda x, y, c: x <= 16 and 32 <= y <= 43)
    parts["body"] = px
    return parts


def shift(layer, dx, dy):
    return {(x + dx, y + dy): c for (x, y), c in layer.items()}


def recolor(layer, mapping):
    return {k: mapping.get(c, c) for k, c in layer.items()}


FAR_SOCK = {PAL["C"]: PAL["N"], PAL["N"]: PAL["M"], PAL["M"]: PAL["Q"]}   # leg behind the body: darker
NEAR_SOCK = {PAL["E"]: PAL["M"]}                                         # her far leg brought to the front
FAR_BOOT = {PAL["T"]: PAL["K"], PAL["K"]: PAL["Q"]}


def sock(a, b, near):
    """A 4 px wide sock from a to b in her colours: grey edge, light grey, white shine on the back side."""
    fill, shine, edge = (PAL["N"], PAL["C"], PAL["M"]) if near else (PAL["M"], PAL["N"], PAL["Q"])
    out = {}
    ax, ay = a
    bx, by = b
    length = math.hypot(bx - ax, by - ay) or 1
    ux, uy = (bx - ax) / length, (by - ay) / length
    nx, ny = -uy, ux
    if nx > 0 or (nx == 0 and ny < 0):
        nx, ny = -nx, -ny
    for y in range(int(min(ay, by)) - 3, int(max(ay, by)) + 4):
        for x in range(int(min(ax, bx)) - 3, int(max(ax, bx)) + 4):
            qx, qy = x + 0.5, y + 0.5
            t = max(0.0, min(length, (qx - ax) * ux + (qy - ay) * uy))
            cx, cy = ax + ux * t, ay + uy * t
            d = (qx - cx) * nx + (qy - cy) * ny
            dist = math.hypot(qx - cx, qy - cy)
            if dist <= 2.05:
                out[(x, y)] = edge if dist > 1.25 else (shine if d > 0.3 else fill)
    return out


def leg(hip, knee, ankle, boot, anchor):
    """Sock (hip -> knee -> ankle) and one of her boots placed at the ankle. near = drawn in light colours."""
    near = boot[1]
    out = sock(hip, knee, near)
    out.update(sock(knee, ankle, near))
    out.update(shift(boot[0], round(ankle[0] - anchor[0]), round(ankle[1] - anchor[1])))
    return out


def compose(*layers):
    out = {}
    for layer in layers:
        out.update(layer)
    return out


FB_ANCHOR, BB_ANCHOR = (27.5, 55.0), (13.5, 48.0)   # where the sock meets each boot in her drawing


def frames(src):
    px, w, h = src
    fill_holes(px)
    p = split(px)
    body, arm = p["body"], p["arm"]
    flat_near = (p["front_boot"], True)
    flat_far = (recolor(p["front_boot"], FAR_BOOT), False)
    kick_near = (p["back_boot"], True)
    kick_far = (recolor(p["back_boot"], {PAL["T"]: PAL["K"]}), False)

    def pose(dy, arm_d, near_leg, far_leg):
        return compose(far_leg, shift(body, 0, dy), shift(arm, arm_d[0], dy + arm_d[1]), near_leg)

    out = {}
    # 0 contact: her drawing (forward leg = near, kicked-back leg = far)
    out["run0"] = dict(px)
    # 1 down: near foot under the body, far leg swinging through, boot behind
    out["run1"] = pose(1, (1, 0), leg((26, 51), (26.5, 54), (24.5, 56.5), flat_near, FB_ANCHOR),
                       leg((20, 51), (18, 55), (15, 54), kick_far, BB_ANCHOR))
    # 2 push-off: near leg straight behind on the toes, far knee up in front; highest point
    out["run2"] = pose(-1, (2, -1), leg((21, 49), (18.5, 53), (15.5, 55.5), flat_near, FB_ANCHOR),
                       leg((25, 48), (30, 50.5), (30, 53.5), flat_far, FB_ANCHOR))
    # 3 contact, legs swapped: her two legs with front and back colours exchanged; kicked leg in front
    out["run3"] = compose(recolor(p["front_sock"], FAR_SOCK), flat_far[0], body, shift(arm, 3, -1),
                          recolor(p["back_sock"], NEAR_SOCK), kick_near[0])
    out["run4"] = pose(1, (2, -1), leg((20, 51), (18, 55), (15, 54), kick_near, BB_ANCHOR),
                       leg((26, 51), (26.5, 54), (24.5, 56.5), flat_far, FB_ANCHOR))
    out["run5"] = pose(-1, (1, 0), leg((25, 48), (30, 50.5), (30, 53.5), flat_near, FB_ANCHOR),
                       leg((21, 49), (18.5, 53), (15.5, 55.5), flat_far, FB_ANCHOR))
    # jump: knees tucked, near knee forward, arm up
    out["jump"] = pose(-2, (3, -3), leg((26, 47), (31, 49), (30, 53), flat_near, FB_ANCHOR),
                       leg((20, 48), (17, 52), (14.5, 50), kick_far, BB_ANCHOR))
    # fall: legs hanging down, a little apart
    out["fall"] = pose(0, (0, -2), leg((26, 50), (27.5, 54), (28.5, 58), flat_near, FB_ANCHOR),
                       leg((20, 50), (19, 54), (18.5, 58), flat_far, FB_ANCHOR))
    out["slide"] = slide(body, arm, flat_near, flat_far)
    return out


SLIDE_LEAN = 72          # degrees the upper body leans back
SLIDE_HEAD_TURN = 30     # the head leans back less, face looking ahead
SLIDE_NECK = (26.0, 28.0)
SLIDE_HIP = (23.0, 50.0)  # pivot of the lean in her drawing (seat of the shorts)


def slide(body, arm, flat_near, flat_far):
    """Feet-first slide: upper body leaning far back (RotSprite-style turn keeps her pixels clean),
    near leg straight along the ground, far knee up, back hand on the ground behind."""
    hx, hy = SLIDE_HIP
    # the head turns less than the body, so her face still looks ahead
    is_head = lambda x, y: y <= 27 or (y <= 29 and x <= 19)
    head = {k: c for k, c in body.items() if is_head(*k)}
    torso = {k: c for k, c in body.items() if not is_head(*k)}
    torso = rotsprite(compose(torso, arm), SLIDE_LEAN, SLIDE_HIP)
    a = math.radians(SLIDE_LEAN)
    nx, ny = SLIDE_NECK[0] - hx, SLIDE_NECK[1] - hy
    neck = (hx + nx * math.cos(a) + ny * math.sin(a), hy - nx * math.sin(a) + ny * math.cos(a))
    head = shift(rotsprite(head, SLIDE_HEAD_TURN, SLIDE_NECK),
                 round(neck[0] - SLIDE_NECK[0]), round(neck[1] - SLIDE_NECK[1]))
    upper = compose(torso, head)
    out = compose(
        leg((hx + 2, hy + 1), (hx + 7, hy - 1), (hx + 11, hy + 2.5), flat_far, FB_ANCHOR),   # far knee up
        upper,
        leg((hx + 3, hy + 3), (hx + 8, hy + 3.5), (hx + 13, hy + 3.5), flat_near, FB_ANCHOR),  # near leg out
    )
    return out


def scale2x(grid, w, h):
    """EPX / Scale2x: doubles pixel art without blurring and keeps diagonals smooth."""
    out = [[None] * (w * 2) for _ in range(h * 2)]
    g = lambda x, y: grid[y][x] if 0 <= x < w and 0 <= y < h else None
    for y in range(h):
        for x in range(w):
            p, a, b, c, d = g(x, y), g(x, y - 1), g(x + 1, y), g(x - 1, y), g(x, y + 1)
            e0 = e1 = e2 = e3 = p
            if c == a and c != d and a != b: e0 = a
            if a == b and a != c and b != d: e1 = b
            if d == c and d != b and c != a: e2 = c
            if b == d and b != a and d != c: e3 = d
            out[2 * y][2 * x], out[2 * y][2 * x + 1], out[2 * y + 1][2 * x], out[2 * y + 1][2 * x + 1] = e0, e1, e2, e3
    return out


def rotsprite(px, angle, pivot, steps=3):
    """Turn pixel art by any angle: Scale2x x8, rotate nearest-neighbour, then take each 8x8 block's main colour."""
    minx = min(x for x, _ in px) - 2; miny = min(y for _, y in px) - 2
    maxx = max(x for x, _ in px) + 2; maxy = max(y for _, y in px) + 2
    w, h = maxx - minx + 1, maxy - miny + 1
    grid = [[px.get((x + minx, y + miny)) for x in range(w)] for y in range(h)]
    for _ in range(steps):
        grid = scale2x(grid, w, h)
        w, h = w * 2, h * 2
    s = 2 ** steps
    a = math.radians(angle)
    ca, sa = math.cos(a), math.sin(a)
    px0, py0 = (pivot[0] - minx + 0.5) * s, (pivot[1] - miny + 0.5) * s
    out = {}
    # every target pixel looks at the 8x8 block of rotated big pixels that falls inside it
    R = int(math.hypot(w, h) / s) + 2
    for ty in range(int(pivot[1]) - R, int(pivot[1]) + R):
        for tx in range(int(pivot[0]) - R, int(pivot[0]) + R):
            votes = {}
            empty = 0
            for sy in range(s):
                for sx in range(s):
                    X = (tx - pivot[0] + (sx + 0.5) / s) * s
                    Y = (ty - pivot[1] + (sy + 0.5) / s) * s
                    # inverse rotation (counter-clockwise on screen = top goes back for a right-facing figure)
                    bx = ca * X - sa * Y + px0
                    by = sa * X + ca * Y + py0
                    ix, iy = int(bx), int(by)
                    c = grid[iy][ix] if 0 <= ix < w and 0 <= iy < h else None
                    if c is None:
                        empty += 1
                    else:
                        votes[c] = votes.get(c, 0) + 1
            if votes and sum(votes.values()) > empty:
                out[(tx, ty)] = max(votes, key=votes.get)
    return out


def place(px, W, H, centre_x):
    """Draw onto a W x H canvas with the lowest pixel on the bottom row (the sprite pivot is at the feet)."""
    maxy = max(y for _, y in px)
    if centre_x:
        minx = min(x for x, _ in px)
        maxx = max(x for x, _ in px)
        ox = (W - (maxx - minx + 1)) // 2 - minx
    else:
        ox = 0
    oy = (H - 1) - maxy
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for (x, y), c in px.items():
        if 0 <= x + ox < W and 0 <= y + oy < H:
            img.putpixel((x + ox, y + oy), c + (255,))
    return img


def aura(img):
    w, h = img.size
    a = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    src = img.load()
    for y in range(h):
        for x in range(w):
            if src[x, y][3]:
                continue
            if any(0 <= x + dx < w and 0 <= y + dy < h and src[x + dx, y + dy][3]
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                a.putpixel((x, y), (255, 255, 255, 255))
    return a


def main():
    src = load(sys.argv[1])
    out_dir = sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    fs = frames(src)
    imgs = {}
    for name, px in fs.items():
        img = place(px, 72, 44, True) if name == "slide" else place(px, 48, 64, False)
        imgs[name] = img
        img.save(os.path.join(out_dir, f"gc26_timetraveler_traveler_{name}.png"))
        aura(img).save(os.path.join(out_dir, f"gc26_timetraveler_traveler_{name}_aura.png"))
    if len(sys.argv) > 3:   # contact sheet x6, aura in the PAST colour
        sheet = Image.new("RGBA", (sum(i.width + 4 for i in imgs.values()), 68), (70, 90, 110, 255))
        x = 0
        for i in imgs.values():
            sheet.paste(Image.new("RGBA", i.size, (255, 153, 51, 255)), (x, 66 - i.height), aura(i))
            sheet.alpha_composite(i, (x, 66 - i.height))
            x += i.width + 4
        sheet.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save(sys.argv[3])
    print("frames:", ", ".join(fs))


if __name__ == "__main__":
    main()
