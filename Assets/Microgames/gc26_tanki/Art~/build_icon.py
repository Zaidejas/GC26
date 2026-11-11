"""Draws the app icon variants from the player's tank sprite, plus a sheet comparing them at phone sizes."""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SPRITE_PATH = os.path.join(HERE, "..", "Resources", "gc26_tanki", "Tanks", "player.png")
OUTPUT_DIR = os.path.join(HERE, "icon")
UNITY_ICON_DIR = os.path.join(HERE, "app_icon")

SIZE = 1024
CENTRE = SIZE / 2

# Same colours as the game: reactor glow, player marker, shot and the loading screen's floor.
GLOW = (111, 242, 224)
MARK = (53, 242, 208)
SHOT = (255, 233, 168)
BACK = (22, 17, 26)
BACK_LIGHT = (38, 66, 74)

# Where the turret mark is in the sprite: left, top, right, bottom.
MARK_AREA = (235, 165, 280, 200)
BRIGHTNESS = 1.35


def load_sprite():
    """The player's sprite made easier to read when small: lighter, with a reactor light in place of the turret mark."""
    pixels = np.asarray(Image.open(SPRITE_PATH).convert("RGBA"), np.float32).copy()

    # The mark is the only near-white part of the turret. Its dark outline is covered by the padding.
    area = pixels[MARK_AREA[1]:MARK_AREA[3], MARK_AREA[0]:MARK_AREA[2]]
    ys, xs = np.nonzero(area[..., :3].min(axis=2) > 190)
    left, right = xs.min() + MARK_AREA[0], xs.max() + MARK_AREA[0]
    top, bottom = ys.min() + MARK_AREA[1], ys.max() + MARK_AREA[1]
    pad = 3
    paint = pixels[top - pad - 4, (left + right) // 2]
    pixels[top - pad:bottom + pad + 1, left - pad:right + pad + 1] = paint

    pixels[..., :3] = np.clip(pixels[..., :3] * BRIGHTNESS, 0, 255)

    middle_x, middle_y = (left + right) // 2, (top + bottom) // 2
    pixels[middle_y - 4:middle_y + 5, middle_x - 4:middle_x + 5] = (*MARK, 255)
    return Image.fromarray(pixels.astype(np.uint8), "RGBA")


def scaled(sprite, box, factor):
    """Scales pixel art with crisp pixels: blown up without smoothing, then reduced to the exact size."""
    part = sprite.crop(box)
    width, height = round(part.width * factor), round(part.height * factor)
    big = part.resize((part.width * 8, part.height * 8), Image.NEAREST)
    return big.resize((width, height), Image.LANCZOS)


def radial_background(centre, radius, inner, outer):
    ys, xs = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    distance = np.hypot(xs - centre[0], ys - centre[1]) / radius
    amount = np.clip(1.0 - distance, 0.0, 1.0)[..., None] ** 1.6
    colour = np.array(outer, np.float32) + (np.array(inner, np.float32) - np.array(outer, np.float32)) * amount
    alpha = np.full((SIZE, SIZE, 1), 255, np.float32)
    return Image.fromarray(np.concatenate([colour, alpha], axis=2).astype(np.uint8), "RGBA")


def add_light(image, layer, blur, strength=1.0):
    """Adds a blurred copy of the layer's colour on top of the image, the way light does."""
    soft = np.asarray(layer.filter(ImageFilter.GaussianBlur(blur)), np.float32)
    amount = soft[..., 3:] / 255.0 * strength
    base = np.asarray(image, np.float32)
    alpha = base[..., 3:] / 255.0

    # Works on a transparent layer too: the light itself becomes visible there.
    light = base[..., :3] * alpha + soft[..., :3] * amount
    new_alpha = np.clip(alpha + np.clip(amount, 0, 1) * (1 - alpha), 1e-4, 1)
    colour = np.clip(light / new_alpha, 0, 255)
    result = np.concatenate([colour, new_alpha * 255], axis=2)
    return Image.fromarray(result.astype(np.uint8), "RGBA")


def lit_lines(sprite):
    """Only the sprite's bright cyan lines, used to make them glow."""
    pixels = np.asarray(sprite, np.int16)
    keep = (pixels[..., 1] > 130) & (pixels[..., 2] > 130) & (pixels[..., 0] < 130) & (pixels[..., 3] > 0)
    lines = np.zeros(pixels.shape, np.uint8)
    lines[keep] = (*MARK, 255)
    return Image.fromarray(lines, "RGBA")


def on_canvas(sprite, position):
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.paste(sprite, (round(position[0]), round(position[1])))
    return canvas


def silhouette(layer):
    """The layer's whole shape in the marker colour, used as a halo that lifts the tank off the background."""
    pixels = np.asarray(layer).copy()
    pixels[..., :3] = MARK
    return Image.fromarray(pixels, "RGBA")


def draw_lit(image, layer, glow):
    lines = lit_lines(layer)
    image = add_light(image, silhouette(layer), 16, 0.55)
    image = add_light(image, lines, 12, glow)
    image.alpha_composite(layer)
    return add_light(image, lines, 5, glow)


def place_tank(image, sprite, position, glow=1.3):
    return draw_lit(image, on_canvas(sprite, position), glow)


def charge_ring(radius, width):
    """The three-part ring from the loading screen, with a gap at the top for the barrel."""
    scale = 2
    layer = Image.new("RGBA", (SIZE * scale, SIZE * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    r = radius * scale
    c = CENTRE * scale
    gap = 11
    for start in (270, 30, 150):
        draw.arc((c - r, c - r, c + r, c + r), start + gap, start + 120 - gap, fill=(*GLOW, 255), width=width * scale)
    return layer.resize((SIZE, SIZE), Image.LANCZOS)


def reactor_background():
    return radial_background((CENTRE, CENTRE), 620, BACK_LIGHT, BACK)


def variant_reactor(sprite, image=None):
    """The tank stands in the charged reactor ring, its barrel going out through the gap."""
    if image is None:
        image = reactor_background()
    ring = charge_ring(452, 40)
    image = add_light(image, ring, 26, 0.9)
    image.alpha_composite(ring)

    # The hull sits in the middle of the ring and the barrel runs off the top edge.
    factor = 2.1
    tank = scaled(sprite, (145, 1, 368, 397), factor)
    hull_middle = (256 - 1) * factor
    return place_tank(image, tank, (CENTRE - tank.width / 2, CENTRE - hull_middle))


def variant_shot(sprite):
    """The tank fires towards the top right corner."""
    # Far enough from the corner that a round icon does not cut the flash.
    tip = (748, 276)
    image = radial_background(tip, 860, (120, 84, 44), BACK)

    factor = 2.35
    tank = scaled(sprite, (145, 1, 368, 397), factor)
    frame = Image.new("RGBA", (SIZE * 2, SIZE * 2), (0, 0, 0, 0))
    frame.paste(tank, (SIZE - tank.width // 2, SIZE - tank.height // 2))
    turned = frame.rotate(-45, Image.BICUBIC)

    # Half the sprite's length back along the barrel from its tip.
    back = tank.height / 2 * math.sqrt(0.5)
    centre = (tip[0] - back, tip[1] + back)
    corner = (centre[0] - SIZE, centre[1] - SIZE)

    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(turned, (max(0, round(corner[0])), max(0, round(corner[1]))),
                           (max(0, -round(corner[0])), max(0, -round(corner[1]))))
    image = draw_lit(image, canvas, 1.3)

    flash = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(flash)
    for length, width in ((150, 34), (92, 60)):
        for turn in (0, 90):
            angle = math.radians(-45 + turn)
            dx, dy = math.cos(angle) * length, math.sin(angle) * length
            nx, ny = -math.sin(angle) * width / 2, math.cos(angle) * width / 2
            draw.polygon([(tip[0] - dx, tip[1] - dy), (tip[0] + nx, tip[1] + ny), (tip[0] + dx, tip[1] + dy),
                          (tip[0] - nx, tip[1] - ny)], fill=(*SHOT, 255))
    image = add_light(image, flash, 40, 1.2)
    image.alpha_composite(flash.filter(ImageFilter.GaussianBlur(2)))
    return image


def variant_close(sprite):
    """A close-up: the tank fills the whole icon, with the reactor's light behind the barrel."""
    image = radial_background((CENTRE, 60), 640, (74, 190, 180), BACK)

    tank = scaled(sprite, (128, 40, 384, 296), 4)
    return place_tank(image, tank, (0, 0), 1.5)


VARIANTS = (("reactor", variant_reactor), ("shot", variant_shot), ("close", variant_close))


def masked(image, size, corner):
    """The icon as a phone would show it. A corner of .5 is a circle."""
    scale = 4
    mask = Image.new("L", (size * scale, size * scale), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size * scale - 1, size * scale - 1), corner * size * scale, fill=255)
    mask = mask.resize((size, size), Image.LANCZOS)
    small = image.resize((size, size), Image.LANCZOS).convert("RGBA")
    small.putalpha(mask)
    return small


def comparison_sheet(icons):
    big, medium, small, gap = 360, 120, 64, 40
    rows = [(26, 28, 34), (236, 238, 242)]
    width = gap + (big + gap) * len(icons)
    height = gap + big + sum(gap + medium for _ in rows)
    sheet = Image.new("RGBA", (width, height), rows[0] + (255,))
    y = gap + big + gap // 2
    ImageDraw.Draw(sheet).rectangle((0, y + medium // 2 + gap, width, height), fill=rows[1] + (255,))

    for index, icon in enumerate(icons):
        x = gap + index * (big + gap)
        sheet.alpha_composite(masked(icon, big, 0.2), (x, gap))
        for row in range(len(rows)):
            top = gap + big + gap + row * (medium + gap)
            sheet.alpha_composite(masked(icon, medium, 0.2), (x, top))
            sheet.alpha_composite(masked(icon, medium, 0.5), (x + medium + 16, top))
            sheet.alpha_composite(masked(icon, small, 0.5), (x + medium * 2 + 32, top + (medium - small) // 2))
    return sheet


def android_layers(sprite):
    """Android's adaptive icon: a background and a foreground the phone masks to its own shape.

    Only the middle two thirds of a layer is ever shown, so the drawing is shrunk into it.
    """
    visible = round(SIZE * 72 / 108)
    offset = (SIZE - visible) // 2

    clear = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    drawing = variant_reactor(sprite, clear)
    foreground = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    foreground.alpha_composite(drawing.resize((visible, visible), Image.LANCZOS), (offset, offset))

    # The light behind the ring is part of the background, scaled the same way.
    background = Image.new("RGBA", (SIZE, SIZE), BACK + (255,))
    background.alpha_composite(reactor_background().resize((visible, visible), Image.LANCZOS), (offset, offset))
    background = background.filter(ImageFilter.GaussianBlur(24))
    return background.convert("RGB"), foreground


def export_for_unity(sprite):
    os.makedirs(UNITY_ICON_DIR, exist_ok=True)
    variant_reactor(sprite).convert("RGB").save(os.path.join(UNITY_ICON_DIR, "icon.png"))
    background, foreground = android_layers(sprite)
    background.save(os.path.join(UNITY_ICON_DIR, "icon_background.png"))
    foreground.save(os.path.join(UNITY_ICON_DIR, "icon_foreground.png"))

    # How a round and a square launcher would show the adaptive icon.
    joined = background.convert("RGBA")
    joined.alpha_composite(foreground)
    visible = round(SIZE * 72 / 108)
    offset = (SIZE - visible) // 2
    shown = joined.crop((offset, offset, offset + visible, offset + visible))
    preview = Image.new("RGBA", (560, 280), (236, 238, 242, 255))
    preview.alpha_composite(masked(shown, 240, 0.5), (20, 20))
    preview.alpha_composite(masked(shown, 240, 0.12), (300, 20))
    preview.convert("RGB").save(os.path.join(OUTPUT_DIR, "android_preview.png"))


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    sprite = load_sprite()
    export_for_unity(sprite)
    icons = []
    for name, draw in VARIANTS:
        icon = draw(sprite).convert("RGB")
        icon.save(os.path.join(OUTPUT_DIR, "icon_" + name + ".png"))
        icons.append(icon)
    comparison_sheet(icons).convert("RGB").save(os.path.join(OUTPUT_DIR, "comparison.png"))
    print("icons written to", OUTPUT_DIR, file=sys.stdout)


if __name__ == "__main__":
    main()
