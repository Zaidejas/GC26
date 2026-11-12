"""Cuts the artist's sprite sheet (Tools~/source/traveler_sheet.webp) into the traveler's game frames.
The sheet is pixel art scaled by 2.56 and saved lossy on a baked checkerboard, so this script
1) samples it back to the art pixel grid, 2) removes the checkerboard / grey panels by flood fill
from each frame's border, 3) snaps every frame to one shared palette, 4) puts each frame on a canvas
with the feet on the bottom row and the body centred (sprite pivot = bottom centre).
Writes run0..7, jump0..5, slide0..3, death0..5 (+ *_aura rings).
Usage: python traveler_from_sheet.py <sheet> <out_dir> [preview.png]   (needs Pillow, numpy, scipy)
"""
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage as nd
from scipy.cluster.vq import kmeans2

PIXEL = 2.56                 # sheet pixels per art pixel
PHASE = (0.16, 1.12)         # grid offset (x, y) in sheet pixels, found by search

# Frame boxes in sheet pixels (top, left, bottom, right), in play order.
FRAMES = {
    "run": [(33, 6, 192, 128), (39, 129, 194, 251), (36, 255, 192, 379), (34, 381, 194, 503),
            (34, 505, 194, 628), (39, 628, 194, 746), (39, 754, 194, 877), (39, 882, 194, 1010)],
    "jump": [(227, 22, 407, 142), (220, 186, 398, 305), (210, 362, 384, 489),
             (237, 539, 407, 668), (251, 700, 407, 840), (221, 868, 405, 1002)],
    "slide": [(444, 19, 571, 186), (448, 222, 571, 395), (445, 421, 571, 603), (449, 634, 571, 796)],
    "death": [(609, 27, 763, 144), (605, 184, 763, 342), (606, 354, 765, 511),
              (625, 529, 763, 674), (653, 682, 764, 846), (702, 848, 761, 1018)],
}
CANVAS = {"run": (64, 64), "jump": (72, 72), "slide": (72, 56), "death": (72, 64)}
COLOURS = 30


def to_grid(path):
    a = np.asarray(Image.open(path).convert("RGB")).astype(float)
    h, w, _ = a.shape
    ox, oy = PHASE
    W, H = int((w - ox) / PIXEL), int((h - oy) / PIXEL)
    g = np.zeros((H, W, 3))
    for j in range(H):
        for i in range(W):
            x0, y0 = ox + i * PIXEL, oy + j * PIXEL
            blk = a[int(y0 + 0.5):int(y0 + PIXEL - 0.5) + 1, int(x0 + 0.5):int(x0 + PIXEL - 0.5) + 1]
            g[j, i] = np.median(blk.reshape(-1, 3), 0)
    return g


def background(c):
    """Checkerboard white / light grey and the grey panels: no colour, fairly light."""
    sat = c.max(-1) - c.min(-1)
    return (sat < 16) & (c.mean(-1) > 150)


def cut(g, box):
    t, l, b, r = [int(round(v / PIXEL)) for v in box]
    t, l, b, r = t - 2, l - 2, b + 2, r + 2
    c = g[t:b, l:r]
    bg = background(c)
    lab, _ = nd.label(bg)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    alpha = ~np.isin(lab, list(border))
    # checkerboard seen through gaps (between cape and legs): enclosed, made of pure white AND neutral grey
    sat = c.max(-1) - c.min(-1)
    br = c.mean(-1)
    for k in set(np.unique(lab)) - border - {0}:
        m = lab == k
        if m.sum() < 6:
            continue
        white = (m & (br > 245)).sum()
        grey = (m & (sat < 7) & (br > 210) & (br < 234)).sum()
        if white >= 0.2 * m.sum() and grey >= 0.2 * m.sum():
            alpha &= ~m
    # drop specks: keep the biggest piece and anything touching it
    lab2, n = nd.label(alpha)
    if n > 1:
        sizes = nd.sum(alpha, lab2, range(1, n + 1))
        keep = 1 + int(np.argmax(sizes))
        alpha = (lab2 == keep) | np.isin(lab2, [i + 1 for i, s in enumerate(sizes) if s >= 12])
    # light, colourless pixels on the very edge are leftover background blur
    for _ in range(2):
        edge = alpha & ~nd.binary_erosion(alpha)
        alpha &= ~(edge & (c.max(-1) - c.min(-1) < 22) & (c.mean(-1) > 170))
    return c, alpha


def main():
    sheet, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    g = to_grid(sheet)
    cuts = {k: [cut(g, b) for b in boxes] for k, boxes in FRAMES.items()}

    # one shared palette for every frame
    pix = np.concatenate([c[a] for frames in cuts.values() for c, a in frames])
    np.random.seed(7)
    pal, _ = kmeans2(pix, COLOURS, minit="++", seed=7)
    pal = np.clip(pal, 0, 255)

    imgs = {}
    for kind, frames in cuts.items():
        W, H = CANVAS[kind]
        # one horizontal anchor per animation: the middle of the upper body in the first frame
        anchors = []
        for c, a in frames:
            ys, xs = np.nonzero(a)
            top = ys.min() + (ys.max() - ys.min()) * 0.55
            anchors.append(np.median(xs[ys <= top]))
        for i, (c, a) in enumerate(frames):
            idx = np.argmin(((c[..., None, :] - pal[None, None]) ** 2).sum(-1), -1)
            ys, xs = np.nonzero(a)
            img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            dx = W // 2 - int(round(anchors[i]))
            dy = (H - 1) - ys.max()
            for y, x in zip(ys, xs):
                X, Y = x + dx, y + dy
                if 0 <= X < W and 0 <= Y < H:
                    img.putpixel((X, Y), tuple(int(v) for v in pal[idx[y, x]]) + (255,))
            name = f"{kind}{i}"
            imgs[name] = img
            img.save(os.path.join(out_dir, f"gc26_timetraveler_traveler_{name}.png"))
            aura(img).save(os.path.join(out_dir, f"gc26_timetraveler_traveler_{name}_aura.png"))

    if len(sys.argv) > 3:
        rows = [[n for n in imgs if n.startswith(k)] for k in FRAMES]
        sw = max(sum(imgs[n].width + 4 for n in r) for r in rows)
        sh = sum(max(imgs[n].height for n in r) + 4 for r in rows)
        prev = Image.new("RGBA", (sw, sh), (70, 90, 110, 255))
        y = 0
        for r in rows:
            x = 0
            rh = max(imgs[n].height for n in r)
            for n in r:
                im = imgs[n]
                prev.paste(Image.new("RGBA", im.size, (255, 153, 51, 255)), (x, y + rh - im.height), aura(im))
                prev.alpha_composite(im, (x, y + rh - im.height))
                x += im.width + 4
            y += rh + 4
        prev.resize((sw * 3, sh * 3), Image.NEAREST).save(sys.argv[3])
    print("frames:", ", ".join(imgs))


def aura(img):
    a = np.asarray(img)[..., 3] > 0
    ring = nd.binary_dilation(a, structure=nd.generate_binary_structure(2, 1)) & ~a
    out = np.zeros(a.shape + (4,), np.uint8)
    out[ring] = (255, 255, 255, 255)
    return Image.fromarray(out, "RGBA")


if __name__ == "__main__":
    main()
