"""Draws the time traveler's minimal pixel sprites (based on MC Concept Art No.1) without any libraries.
Usage: python traveler_sprites.py <out_dir> [preview.png]
Writes run0..run5, jump, fall, slide (+ *_aura outline masks) as PNG."""
import math, os, struct, sys, zlib

OUT = (26, 22, 38)
HAIR, HAIR_D = (240, 140, 86), (196, 94, 58)
SKIN, SKIN_D = (242, 214, 190), (204, 164, 142)
SHIRT, SHIRT_D = (246, 246, 242), (196, 200, 212)
STRAP = (160, 112, 66)
LEATHER, LEATHER_D = (156, 70, 50), (112, 48, 36)
CAPE, CAPE_D = (34, 40, 74), (22, 26, 50)
PANTS, PANTS_D = (180, 130, 82), (138, 96, 58)
METAL, METAL_D = (200, 204, 212), (140, 146, 160)
SOCK = (96, 96, 156)
BOOT, BOOT_L = (34, 38, 62), (64, 70, 104)
GOLD = (236, 196, 84)
GLOVE = (36, 38, 58)
EYE = (60, 36, 30)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [[None] * w for _ in range(h)]

    def put(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                self.put(x, y, c)

    def disc(self, cx, cy, r, c):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.25:
                    self.put(x, y, c)

    def line(self, a, b, r, c):
        n = max(1, int(math.dist(a, b) * 2))
        for i in range(n + 1):
            t = i / n
            self.disc(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, r, c)

    def poly(self, pts, c):
        ys = [p[1] for p in pts]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            yc = y + 0.5
            xs = []
            for i in range(len(pts)):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
                if (y0 <= yc < y1) or (y1 <= yc < y0):
                    xs.append(x0 + (yc - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                for x in range(int(math.ceil(xs[i] - 0.5)), int(math.floor(xs[i + 1] - 0.5)) + 1):
                    self.put(x, y, c)

    def outlined(self):
        """Dark 1px outline around the figure, plus a separate mask of the ring just outside it (the aura)."""
        out = Canvas(self.w, self.h)
        out.px = [row[:] for row in self.px]
        nb = ((1, 0), (-1, 0), (0, 1), (0, -1))
        for y in range(self.h):
            for x in range(self.w):
                if self.px[y][x] is None and any(
                        0 <= x + dx < self.w and 0 <= y + dy < self.h and self.px[y + dy][x + dx] is not None
                        for dx, dy in nb):
                    out.px[y][x] = OUT
        aura = Canvas(self.w, self.h)
        for y in range(self.h):
            for x in range(self.w):
                if out.px[y][x] is None and any(
                        0 <= x + dx < self.w and 0 <= y + dy < self.h and out.px[y + dy][x + dx] is not None
                        for dx, dy in nb):
                    aura.px[y][x] = (255, 255, 255)
        return out, aura

    def png(self, path, scale=1):
        raw = b""
        for y in range(self.h * scale):
            row = b"\x00"
            for x in range(self.w * scale):
                c = self.px[y // scale][x // scale]
                row += bytes(c) + b"\xff" if c else b"\x00\x00\x00\x00"
            raw += row
        def chunk(t, d):
            return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
        data = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", self.w * scale, self.h * scale, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
        open(path, "wb").write(data)


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def perp(a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    l = math.hypot(dx, dy) or 1
    return (-dy / l, dx / l)


def leg(cv, hip, knee, foot, far):
    pants, metal, boot = (PANTS_D, METAL_D, BOOT) if far else (PANTS, METAL, BOOT)
    ankle = (foot[0], foot[1] - 3)
    cv.line(knee, ankle, 1.2, metal)                    # silver shin guard
    cv.put(ankle[0], ankle[1] - 1, SOCK)
    cv.put(ankle[0] - 1, ankle[1] - 1, SOCK)
    cv.rect(foot[0] - 1, foot[1] - 2, foot[0] + 2, foot[1], boot)   # boot, toe forward
    cv.put(foot[0] - 1, foot[1] - 3, boot)
    cv.put(foot[0], foot[1] - 3, boot)
    if not far:
        cv.put(foot[0] + 1, foot[1] - 2, BOOT_L)
    cv.line(hip, knee, 1.8, pants)                      # baggy knee-length breeches
    cv.disc(knee[0], knee[1], 1.4, pants)


def arm(cv, shoulder, hand, far):
    elbow = lerp(shoulder, hand, 0.5)
    elbow = (elbow[0] - 0.5, elbow[1] + 1.5)
    if far:   # left arm: bare, dark fingerless glove
        cv.line(shoulder, elbow, 1.1, SHIRT_D)
        cv.line(elbow, hand, 1.0, SKIN_D)
        cv.disc(hand[0], hand[1], 1.2, GLOVE)
    else:     # right arm: wrapped in brown leather, outlined so it reads over the white shirt
        cv.line(shoulder, elbow, 2.1, OUT)
        cv.line(elbow, hand, 1.9, OUT)
        cv.line(shoulder, elbow, 1.2, SHIRT)
        cv.line(elbow, hand, 1.1, LEATHER)
        cv.put(*lerp(elbow, hand, 0.5), LEATHER_D)
        cv.disc(hand[0], hand[1], 1.2, SKIN)


def head(cv, c):
    x, y = c
    cv.disc(x + 1, y, 4.2, SKIN)                        # face
    cv.put(x + 5, y + 1, SKIN)                          # nose
    cv.put(x + 3, y - 1, OUT)                           # eye
    cv.put(x + 3, y, EYE)
    cv.put(x + 3, y - 2, HAIR_D)                        # brow
    cv.put(x + 3, y + 3, SKIN_D)                        # mouth
    cv.put(x + 2, y + 4, SKIN_D)                        # stubble
    # hair: top, swept fringe and the long back reaching the shoulders
    cv.poly([(x - 4, y - 1), (x - 3, y - 4), (x, y - 5.5), (x + 4, y - 5), (x + 6, y - 3), (x + 4, y - 3),
             (x + 2, y - 2), (x, y - 3), (x - 1, y), (x - 1, y + 2), (x - 2, y + 6), (x - 4.5, y + 5)], HAIR)
    cv.put(x - 3, y + 1, HAIR_D)
    cv.put(x - 3, y + 3, HAIR_D)
    cv.put(x + 2, y - 4, HAIR_D)


def torso(cv, hip, neck, cape_tip):
    p = perp(hip, neck)
    back = (-abs(p[0]) if p[0] else -1, p[1])
    # cape over the far (left) shoulder, flowing behind
    s = lerp(hip, neck, 0.95)
    cv.poly([(s[0] + 1, s[1] - 1), (s[0] - 3, s[1]), (cape_tip[0], cape_tip[1]),
             (cape_tip[0] + 3, cape_tip[1] - 1), lerp(hip, neck, 0.35)], CAPE)
    cv.line((s[0] - 2, s[1] + 1), (cape_tip[0] + 2, cape_tip[1] - 1), 0.5, CAPE_D)
    cv.line(hip, neck, 3.2, SHIRT)                      # white shirt
    # crossed straps
    a, b = lerp(hip, neck, 0.15), lerp(hip, neck, 0.9)
    cv.line((a[0] - 2, a[1]), (b[0] + 2, b[1]), 0.5, STRAP)
    cv.line((a[0] + 2, a[1]), (b[0] - 2, b[1]), 0.5, STRAP)
    # belt with the pocket watch
    w = lerp(hip, neck, 0.02)
    cv.line((w[0] - p[0] * 3.2, w[1] - p[1] * 3.2), (w[0] + p[0] * 3.2, w[1] + p[1] * 3.2), 0.7, LEATHER)
    cv.put(w[0] + 2, w[1], GOLD)


def figure(w, h, hip, neck, head_c, near, far, hands, cape_tip):
    cv = Canvas(w, h)
    arm(cv, lerp(hip, neck, 0.88), hands[1], far=True)
    leg(cv, hip, far[0], far[1], far=True)
    torso(cv, hip, neck, cape_tip)
    leg(cv, hip, near[0], near[1], far=False)
    cv.line(neck, (neck[0] + 0.5, neck[1] - 2), 1.2, SKIN)   # neck
    head(cv, head_c)
    arm(cv, lerp(hip, neck, 0.88), hands[0], far=False)
    return cv


def standing(dy, near, far, hands, cape_tip):
    hip = (15, 29 + dy)
    neck = (16.5, 16 + dy)
    return figure(32, 48, hip, neck, (17, 9 + dy), near, far, hands, cape_tip)


# Run cycle: (body bob, near knee/foot, far knee/foot, near/far hand, cape tip)
RUN = [
    (0, ((19, 34), (21, 46)), ((12, 35), (8, 43)), ((12, 25), (21, 23)), (5, 30)),
    (1, ((18, 35), (18, 47)), ((11, 35), (9, 41)), ((13, 27), (20, 25)), (4, 31)),
    (-1, ((16, 34), (14, 46)), ((17, 35), (14, 41)), ((16, 28), (16, 28)), (5, 29)),
    (0, ((12, 35), (8, 43)), ((19, 34), (21, 46)), ((21, 23), (12, 25)), (5, 30)),
    (1, ((11, 35), (9, 41)), ((18, 35), (18, 47)), ((20, 25), (13, 27)), (4, 31)),
    (-1, ((17, 35), (14, 41)), ((16, 34), (14, 46)), ((16, 28), (16, 28)), (5, 29)),
]


def frames():
    out = {}
    for i, (dy, near, far, hands, cape) in enumerate(RUN):
        out[f"run{i}"] = standing(dy, near, far, hands, cape)
    out["jump"] = standing(-2, ((20, 32), (19, 40)), ((13, 34), (10, 41)), ((22, 13), (11, 19)), (6, 31))
    out["fall"] = standing(-1, ((18, 37), (19, 45)), ((13, 37), (11, 44)), ((23, 19), (9, 19)), (5, 10))
    # Slide: leaning back, legs forward, cape trailing on the ground
    out["slide"] = figure(48, 24, (24, 18), (16, 11), (11, 7),
                          ((31, 16), (38, 23)), ((30, 19), (36, 23)), ((26, 13), (10, 22)), (1, 23))
    return out


if __name__ == "__main__":
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    fs = frames()
    for name, cv in fs.items():
        body, aura = cv.outlined()
        body.png(os.path.join(out_dir, f"gc26_timetraveler_traveler_{name}.png"))
        aura.png(os.path.join(out_dir, f"gc26_timetraveler_traveler_{name}_aura.png"))
    if len(sys.argv) > 2:   # x8 contact sheet with the aura in the past colour, for checking by eye
        names = list(fs)
        sheet = Canvas(sum(fs[n].w for n in names) + 2 * len(names), 48)
        x0 = 0
        for n in names:
            body, aura = fs[n].outlined()
            for y in range(body.h):
                for x in range(body.w):
                    c = body.px[y][x] or ((255, 153, 51) if aura.px[y][x] else (210, 214, 222))
                    sheet.put(x0 + x, 48 - body.h + y, c)
            x0 += body.w + 2
        sheet.png(sys.argv[2], scale=6)
    print("frames:", ", ".join(fs))
