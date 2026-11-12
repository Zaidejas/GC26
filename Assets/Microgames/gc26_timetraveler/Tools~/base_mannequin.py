"""Bald mannequin base for drawing the traveler's frames over (black line pixels only, transparent inside).
Same canvas, poses and feet line as the game's frames: 32x48 (slide 48x24), facing right, soles on the bottom row.
Usage: python base_mannequin.py <out_dir> [sheet.png]
"""
import math, os, struct, sys, zlib

BLACK = (0, 0, 0)

# Draw order = depth: later parts are in front. Overlaps get a black line on the part behind.
FAR_ARM, FAR_LEG, TORSO, NECK, HEAD, NEAR_LEG, NEAR_ARM = range(1, 8)


class Mask:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.id = [[0] * w for _ in range(h)]
        self.extra = set()   # interior detail pixels (eye, ear, knee...)

    def put(self, x, y, part):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.id[y][x] = part

    def disc(self, cx, cy, r, part):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.3:
                    self.put(x, y, part)

    def ellipse(self, cx, cy, rx, ry, part):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.05:
                    self.put(x, y, part)

    def limb(self, a, b, r0, r1, part):
        """Tapered capsule from a (radius r0) to b (radius r1)."""
        n = max(2, int(math.dist(a, b) * 3))
        for i in range(n + 1):
            t = i / n
            self.disc(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, r0 + (r1 - r0) * t, part)

    def poly(self, pts, part):
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
                    self.put(x, y, part)

    def lineart(self):
        """Black pixels: the silhouette edge, edges where a front part overlaps a back part, and details."""
        out = [[None] * self.w for _ in range(self.h)]
        for y in range(self.h):
            for x in range(self.w):
                p = self.id[y][x]
                if not p:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    q = self.id[ny][nx] if 0 <= nx < self.w and 0 <= ny < self.h else 0
                    if q == 0 or q > p and not (p in (TORSO, NECK, HEAD) and q in (NECK, HEAD)):
                        out[y][x] = BLACK
                        break
        for (x, y) in self.extra:
            if 0 <= x < self.w and 0 <= y < self.h:
                out[y][x] = BLACK
        return out


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def leg(m, hip, knee, foot, part, toe_dir=1):
    # foot points along the ground when planted, tilts with the shin when lifted
    planted = foot[1] >= 45.5 or (part in (NEAR_LEG, FAR_LEG) and foot[1] >= 21.5 and foot[1] <= 23.5)
    ankle = (foot[0] - 0.8, foot[1] - 2.4)
    m.limb(hip, knee, 3.6, 2.7, part)                 # thigh
    m.disc(knee[0] + 0.2, knee[1], 2.5, part)         # knee
    calf = lerp(knee, ankle, 0.35)
    m.limb(knee, calf, 2.5, 2.7, part)                # calf muscle
    m.limb(calf, ankle, 2.7, 1.6, part)               # shin to ankle
    if planted:
        m.poly([(ankle[0] - 1.8, ankle[1] - 1.0), (ankle[0] + 1.6, ankle[1] - 1.0),
                (foot[0] + 4.2, foot[1] - 1.4), (foot[0] + 4.4, foot[1] + 0.5),
                (foot[0] - 2.2, foot[1] + 0.5)], part)
    else:
        sx, sy = ankle[0] - knee[0], ankle[1] - knee[1]
        l = math.hypot(sx, sy) or 1
        tx, ty = sy / l, -sx / l                      # toes point "forward" of the shin
        if tx < 0: tx, ty = -tx, -ty
        m.limb(ankle, (ankle[0] + tx * 4.0, ankle[1] + ty * 4.0 + 0.8), 1.7, 1.3, part)


def arm(m, shoulder, elbow, hand, part):
    m.disc(shoulder[0], shoulder[1] + 0.5, 2.8, part)  # shoulder (deltoid)
    m.limb(shoulder, elbow, 2.5, 1.9, part)           # upper arm
    fore = lerp(elbow, hand, 0.4)
    m.limb(elbow, fore, 1.9, 2.0, part)               # forearm muscle
    m.limb(fore, hand, 2.0, 1.4, part)                # wrist
    m.disc(hand[0], hand[1], 1.9, part)               # fist


def torso(m, hip, neck_base):
    # chest wider than the waist, slight lean follows the hip->neck line
    dx, dy = neck_base[0] - hip[0], neck_base[1] - hip[1]
    l = math.hypot(dx, dy)
    px, py = -dy / l, dx / l   # perpendicular (points to the back for a right-facing figure)
    def at(t, half):
        c = lerp(hip, neck_base, t)
        return [(c[0] + px * half, c[1] + py * half), (c[0] - px * half, c[1] - py * half)]
    pelvis, waist, chest, shoulders = at(0.0, 4.4), at(0.38, 4.0), at(0.72, 5.4), at(1.0, 4.4)
    m.poly([pelvis[0], waist[0], chest[0], shoulders[0], shoulders[1], chest[1], waist[1], pelvis[1]], TORSO)
    m.disc(hip[0] - 0.6, hip[1] + 0.4, 4.3, TORSO)    # buttocks / pelvis volume
    c = lerp(hip, neck_base, 0.7)
    m.disc(c[0] + 0.8, c[1], 5.3, TORSO)              # ribcage, chest pushed forward


def head(m, c, neck_base):
    m.limb(neck_base, (c[0] - 0.5, c[1] + 3.5), 2.2, 2.0, NECK)
    m.ellipse(c[0], c[1] - 0.3, 4.6, 5.0, HEAD)       # skull
    m.poly([(c[0] - 1, c[1] + 2), (c[0] + 4.4, c[1] + 0.5), (c[0] + 4.2, c[1] + 3.6),
            (c[0] + 2.0, c[1] + 5.2), (c[0] - 1.5, c[1] + 4.6)], HEAD)   # strong jaw and chin
    m.put(c[0] + 5.2, c[1] + 0.5, HEAD)               # nose
    m.extra.add((int(round(c[0] + 3.0)), int(round(c[1] - 0.6))))     # eye
    m.extra.add((int(round(c[0] + 3.0)), int(round(c[1] - 1.6))))     # brow line hint
    ex, ey = int(round(c[0] - 0.8)), int(round(c[1] + 0.4))           # ear
    m.extra.update({(ex, ey), (ex, ey + 1), (ex - 1, ey), (ex - 1, ey + 1)})


def figure(w, h, hip, neck_base, head_c, near, far, near_arm, far_arm):
    m = Mask(w, h)
    shoulder = lerp(hip, neck_base, 0.9)
    arm(m, shoulder, far_arm[0], far_arm[1], FAR_ARM)
    leg(m, hip, far[0], far[1], FAR_LEG)
    torso(m, hip, neck_base)
    head(m, head_c, neck_base)
    leg(m, hip, near[0], near[1], NEAR_LEG)
    arm(m, shoulder, near_arm[0], near_arm[1], NEAR_ARM)
    return m


def standing(dy, near, far, near_arm, far_arm, lean=2.5):
    # Running lean: shoulders and head ahead of the hips.
    hip = (14, 27 + dy)
    neck = (14 + lean, 15 + dy)
    return figure(32, 48, hip, neck, (16 + lean, 8 + dy), near, far, near_arm, far_arm)


# Six-frame run: contact, down (lowest), passing (highest), then the same with the legs swapped.
# Each: body bob, near (knee, foot), far (knee, foot), near arm (elbow, hand), far arm (elbow, hand)
RUN = [
    # 0 contact: near leg reaching forward, far leg kicked back; arms opposite
    (0, ((20, 33.5), (22.5, 46)), ((10.5, 33), (5.5, 36)), ((12.5, 21.5), (12, 25.5)), ((20.5, 22.5), (23.5, 19))),
    # 1 down: near foot under the body, knee bent (lowest); far heel tucked up behind
    (1, ((19, 34.5), (17, 47)), ((11, 32.5), (9, 36)), ((13.5, 22.5), (14, 26.5)), ((20, 23), (22.5, 20))),
    # 2 push-off: near leg behind on its toes, far knee driving up (highest)
    (-1, ((13, 35.5), (9.5, 45.5)), ((20, 30), (17.5, 37)), ((18.5, 23.5), (21.5, 21)), ((13.5, 22.5), (12.5, 26.5))),
]
RUN += [(dy, far, near, fa, na) for (dy, near, far, na, fa) in RUN]   # 3-5: the same with the legs swapped


def frames():
    out = {}
    for i, f in enumerate(RUN):
        out[f"run{i}"] = standing(*f)
    # Jump: knees tucked, arms up and forward
    out["jump"] = standing(-2, ((20, 30), (18.5, 38)), ((13, 33), (9, 39)),
                           ((19.5, 14.5), (22, 10.5)), ((11.5, 16.5), (9.5, 12.5)), lean=1.5)
    # Fall: legs reaching down, arms out for balance
    out["fall"] = standing(-1, ((18, 35), (19.5, 44.5)), ((12.5, 35), (10.5, 43.5)),
                           ((21, 18.5), (24.5, 16)), ((10.5, 18.5), (7.5, 16)), lean=1)
    # Slide: leaning back on one hand, legs forward (48 x 24)
    s = Mask(48, 24)
    hip, neck = (24, 18), (15, 11)
    shoulder = lerp(hip, neck, 0.9)
    arm(s, shoulder, (12, 17), (10, 21.5), FAR_ARM)
    leg(s, hip, (31, 19.5), (37, 22.5), FAR_LEG)
    torso(s, hip, neck)
    head(s, (11, 6.5), neck)
    leg(s, hip, (31, 16.5), (39, 22.5), NEAR_LEG)
    arm(s, shoulder, (21, 12), (26, 11), NEAR_ARM)
    out["slide"] = s
    return out


def png(pixels, path, scale=1, background=None):
    h, w = len(pixels), len(pixels[0])
    raw = b""
    for y in range(h * scale):
        row = b"\x00"
        for x in range(w * scale):
            c = pixels[y // scale][x // scale]
            if c is None and background:
                c = background
            row += bytes(c) + b"\xff" if c else b"\x00\x00\x00\x00"
        raw += row
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w * scale, h * scale, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


if __name__ == "__main__":
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    fs = frames()
    arts = {n: m.lineart() for n, m in fs.items()}
    for n, a in arts.items():
        png(a, os.path.join(out_dir, f"base_{n}.png"))
    if len(sys.argv) > 2:
        names = list(arts)
        width = sum(len(arts[n][0]) + 2 for n in names)
        sheet = [[(232, 232, 236)] * width for _ in range(48)]
        x0 = 0
        for n in names:
            a = arts[n]
            for y in range(len(a)):
                for x in range(len(a[0])):
                    if a[y][x]:
                        sheet[48 - len(a) + y][x0 + x] = a[y][x]
            for y in range(48):          # separator
                sheet[y][x0 + len(a[0]) + 1] = (190, 190, 200)
            x0 += len(a[0]) + 2
        png(sheet, sys.argv[2], scale=6)
    print("frames:", ", ".join(fs))
