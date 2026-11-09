"""Shared helpers for the GC26 microgame tools."""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAMES_DIR = "Assets/Microgames"
ID_RE = re.compile(r"^gc26_[a-z0-9]+(_[a-z0-9]+)*$")
BRANCH_PREFIX = "game/"


def known_ids():
    lines = (ROOT / "tools" / "games.txt").read_text(encoding="utf-8").splitlines()
    return [ln.strip() for ln in lines if ln.strip() and not ln.startswith("#")]


def pascal_name(game_id):
    """gc26_science_snake -> ScienceSnake, gc26_2048 -> _2048."""
    name = "".join(part[:1].upper() + part[1:] for part in game_id.split("_")[1:])
    return "_" + name if name[:1].isdigit() else name


def namespace(game_id):
    return "GC26." + pascal_name(game_id)


def git(*args):
    out = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    if out.returncode != 0:
        sys.exit(f"git {' '.join(args)} failed:\n{out.stderr.strip()}")
    return out.stdout.strip()


class Report:
    def __init__(self):
        self.errors = []
        self.warnings = []

    def error(self, msg):
        self.errors.append(msg)

    def warn(self, msg):
        self.warnings.append(msg)

    def finish(self, ok_msg):
        for w in self.warnings:
            print(f"  WARN  {w}")
        for e in self.errors:
            print(f"  ERROR {e}")
        if self.errors:
            print(f"\n{len(self.errors)} error(s). See RULES.md.")
            sys.exit(1)
        print(ok_msg)
