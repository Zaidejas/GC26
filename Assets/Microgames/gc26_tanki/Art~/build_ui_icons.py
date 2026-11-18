"""Renders the browser version's button icons (ICON_PATHS in Web~/steel-line.html) to PNGs for the Unity HUD.

Single-colour icons come out white, so the HUD can tint them; coloured ones keep their colours.
Each icon is drawn by headless Edge or Chrome, so no SVG library is needed.
usage: python build_ui_icons.py [output folder]
"""
import os
import re
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
PAGE = os.path.join(HERE, "..", "Web~", "steel-line.html")
OUTPUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "Resources", "gc26_tanki", "Icons")
NAMES = ["play", "assault", "wave", "tank", "wrench", "medal", "star", "gear", "part", "trophy",
         "back", "home", "retry", "next", "lock"]
SIZE = 96
BROWSERS = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]


def icon_paths():
    text = open(PAGE, encoding="utf-8").read()
    block = text[text.index("const ICON_PATHS = {"):]
    block = block[:block.index("\n};")]
    return dict(re.findall(r"^\s*(\w+): '(.*)',?$", block, re.M))


def main():
    browser = next((b for b in BROWSERS if os.path.exists(b)), None)
    if browser is None:
        sys.exit("Edge or Chrome is needed to draw the icons")
    paths = icon_paths()
    os.makedirs(OUTPUT, exist_ok=True)
    with tempfile.TemporaryDirectory() as work:
        for name in NAMES:
            body = paths[name].replace("currentColor", "#ffffff")
            page = os.path.join(work, name + ".html")
            with open(page, "w", encoding="utf-8") as f:
                f.write('<html><body style="margin:0;background:transparent">'
                        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="{SIZE}" height="{SIZE}" '
                        f'style="display:block">{body}</svg></body></html>')
            target = os.path.abspath(os.path.join(OUTPUT, name + ".png"))
            subprocess.run([browser, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                            "--user-data-dir=" + os.path.join(work, "profile"),
                            "--default-background-color=00000000", f"--window-size={SIZE},{SIZE}",
                            "--screenshot=" + target, "file:///" + page.replace("\\", "/")],
                           check=True, capture_output=True, timeout=60)
            print(name, os.path.getsize(target))


if __name__ == "__main__":
    main()
