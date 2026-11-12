"""Cute chibi base for the traveler (bald, black line pixels only), bigger than the old 32x48 frames.
Frames are 48x64 px (slide 64x32): at 16 px per unit the drawing is 3 x 4 units around the 1.3 x 3 hitbox,
so he reads about a third bigger on a phone without changing the gameplay.
About 2.4 heads tall: big round head, small bean body, short chubby limbs, big boots, big eye.
Usage: python chibi_base.py <out_dir> [sheet.png] [phone_mock.png]
"""
import os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from base_mannequin import Mask, lerp, png, FAR_ARM, FAR_LEG, TORSO, NEAR_LEG, NEAR_ARM  # noqa: E402

HEAD_LAYER = NEAR_LEG + 0.5   # head in front of the body and near leg, behind the near arm


def leg(m, hip, knee, foot, part):
    m.limb(hip, knee, 3.4, 3.0, part)                  # chubby thigh
    ankle = (foot[0] - 1.0, foot[1] - 3.2)
    m.limb(knee, ankle, 3.0, 2.4, part)                # short shin
    planted = foot[1] >= m.h - 1.6
    if planted:                                        # big round boot flat on the ground
        m.poly([(ankle[0] - 2.6, ankle[1] - 1.2), (ankle[0] + 2.2, ankle[1] - 1.6),
                (foot[0] + 5.2, foot[1] - 2.4), (foot[0] + 5.6, foot[1] + 0.5),
                (foot[0] - 3.2, foot[1] + 0.5)], part)
        m.disc(foot[0] + 3.6, foot[1] - 1.4, 2.0, part)
    else:                                              # lifted boot follows the shin
        sx, sy = ankle[0] - knee[0], ankle[1] - knee[1]
        l = (sx * sx + sy * sy) ** 0.5 or 1
        tx, ty = sy / l, -sx / l
        if tx < 0:
            tx, ty = -tx, -ty
        toe = (ankle[0] + tx * 5.2, ankle[1] + ty * 5.2 + 1.2)
        m.limb(ankle, toe, 2.8, 2.3, part)


def arm(m, shoulder, elbow, hand, part):
    m.limb(shoulder, elbow, 2.6, 2.2, part)            # short upper arm
    m.limb(elbow, hand, 2.2, 2.0, part)                # forearm
    m.disc(hand[0], hand[1], 2.7, part)                # round mitten hand


def body(m, hip, chest):
    m.limb(hip, chest, 6.2, 6.0, TORSO)                # bean-shaped little body
    m.disc(hip[0] - 0.5, hip[1] + 1, 5.6, TORSO)       # round bottom


def head(m, c):
    part = HEAD_LAYER
    m.ellipse(c[0], c[1], 11.5, 12.0, part)            # big round head
    m.ellipse(c[0] + 3.5, c[1] + 6.5, 7.0, 4.6, part)  # soft cheeks and chin, a bit forward
    # big eye: 3 x 5 with a 1 px shine (left transparent)
    ex, ey = int(round(c[0] + 6)), int(round(c[1] + 1))
    for y in range(ey - 2, ey + 3):
        for x in range(ex - 1, ex + 2):
            if (x, y) != (ex, ey - 1):
                m.extra.add((x, y))
    m.extra.update({(ex - 1, ey - 4), (ex, ey - 4), (ex + 1, ey - 5)})          # brow
    m.extra.update({(ex + 2, ey + 6), (ex + 3, ey + 6)})                        # small smile
    m.extra.update({(ex + 3, ey + 5)})
    cx, cy = int(round(c[0] - 3)), int(round(c[1] + 2))                         # ear
    m.extra.update({(cx, cy - 2), (cx - 1, cy - 1), (cx - 1, cy), (cx - 1, cy + 1), (cx, cy + 2)})


def figure(w, h, hip, chest, head_c, near, far, near_arm, far_arm, shoulder=None):
    m = Mask(w, h)
    shoulder = shoulder or lerp(hip, chest, 0.75)
    arm(m, shoulder, far_arm[0], far_arm[1], FAR_ARM)
    leg(m, hip, far[0], far[1], FAR_LEG)
    body(m, hip, chest)
    leg(m, hip, near[0], near[1], NEAR_LEG)
    head(m, head_c)
    arm(m, shoulder, near_arm[0], near_arm[1], NEAR_ARM)
    return m


def standing(dy, near, far, near_arm, far_arm, lean=3.0):
    hip = (21, 47 + dy)
    chest = (21 + lean * 0.5, 37 + dy)
    return figure(48, 64, hip, chest, (23 + lean, 21 + dy), near, far, near_arm, far_arm)


# (bob, near (knee, foot), far (knee, foot), near arm (elbow, hand), far arm (elbow, hand))
RUN = [
    # 0 contact: near boot lands in front, far leg kicked back
    (0, ((27, 52), (30, 62.5)), ((15.5, 53), (10.5, 55.5)), ((18.5, 41), (17.5, 45)), ((28.5, 41), (32, 37.5))),
    # 1 down: weight on the near leg, lowest point
    (1, ((25.5, 53), (24, 63)), ((16.5, 51.5), (13, 53.5)), ((19.5, 42), (19.5, 46)), ((28, 42), (31, 39.5))),
    # 2 push-off: near leg pushes from behind, far knee drives up; highest point
    (-2, ((19, 54), (15, 62)), ((27.5, 49), (26, 56)), ((27, 43), (31, 40)), ((19.5, 42), (17.5, 45.5))),
]
RUN += [(dy, far, near, fa, na) for (dy, near, far, na, fa) in RUN]


def frames():
    out = {f"run{i}": standing(*f) for i, f in enumerate(RUN)}
    out["jump"] = standing(-3, ((28, 48), (27, 56)), ((18.5, 51), (14.5, 56)),
                           ((30, 35), (33, 30)), ((17, 36), (14, 31)), lean=2)
    out["fall"] = standing(-1, ((25, 54), (27, 62)), ((18, 54), (16, 61)),
                           ((30.5, 38), (35.5, 35)), ((16, 38), (11.5, 35)), lean=1)
    # Slide (64 x 32): leaning back on one hand, boots first
    out["slide"] = figure(64, 32, (33, 24), (24, 19), (16, 13),
                          ((42, 24.5), (51, 31)), ((40, 27), (47, 31)),
                          ((31, 18), (36, 17)), ((20, 25), (17, 29.5)), shoulder=(25, 19))
    return out


def phone_mock(path, new_frame, old_path):
    """The real 270x480 game screen (x2 here) with the old 32x48 size and the new chibi size side by side."""
    W, H, ground = 270, 480, 380
    sky, grass, dirt, rock = (40, 34, 28), (110, 150, 80), (92, 77, 61), (217, 64, 51)
    px = [[sky] * W for _ in range(H)]
    for y in range(ground, H):
        for x in range(W):
            px[y][x] = grass if y < ground + 3 else dirt
    for y in range(ground - 24, ground):            # a rock (24 px) for scale
        for x in range(200, 224):
            px[y][x] = rock
    def stamp(art, x0, colour=(255, 255, 255)):
        h = len(art)
        for y in range(h):
            for x in range(len(art[0])):
                if art[y][x]:
                    px[ground - h + y][x0 + x] = art[y][x] if colour is None else colour
    old = read_png(old_path) if old_path and os.path.exists(old_path) else None
    if old:
        stamp(old, 40, None)
    stamp(new_frame, 110)
    png(px, path, scale=2)


def read_png(path):
    """Tiny PNG reader for our own 8-bit RGBA files (no filters other than 0)."""
    import struct, zlib
    data = open(path, "rb").read()
    pos, w, h, idat = 8, 0, 0, b""
    while pos < len(data):
        n = struct.unpack(">I", data[pos:pos + 4])[0]
        t = data[pos + 4:pos + 8]
        d = data[pos + 8:pos + 8 + n]
        if t == b"IHDR":
            w, h = struct.unpack(">II", d[:8])
        elif t == b"IDAT":
            idat += d
        pos += 12 + n
    raw = zlib.decompress(idat)
    out, i = [], 0
    for y in range(h):
        i += 1
        row = []
        for x in range(w):
            r, g, b, a = raw[i:i + 4]
            row.append((r, g, b) if a else None)
            i += 4
        out.append(row)
    return out


if __name__ == "__main__":
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    fs = frames()
    arts = {n: m.lineart() for n, m in fs.items()}
    for n, a in arts.items():
        png(a, os.path.join(out_dir, f"chibi_{n}.png"))
    if len(sys.argv) > 2:
        names = list(arts)
        width = sum(len(arts[n][0]) + 2 for n in names)
        sheet = [[(232, 232, 236)] * width for _ in range(64)]
        x0 = 0
        for n in names:
            a = arts[n]
            for y in range(len(a)):
                for x in range(len(a[0])):
                    if a[y][x]:
                        sheet[64 - len(a) + y][x0 + x] = a[y][x]
            for y in range(64):
                sheet[y][x0 + len(a[0]) + 1] = (190, 190, 200)
            x0 += len(a[0]) + 2
        png(sheet, sys.argv[2], scale=5)
    if len(sys.argv) > 3:
        phone_mock(sys.argv[3], arts["run0"], sys.argv[4] if len(sys.argv) > 4 else None)
    print("frames:", ", ".join(fs))
