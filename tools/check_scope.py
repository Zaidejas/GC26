"""Fails if branch game/<id> changes files outside Assets/Microgames/<id>/.

Usage: python tools/check_scope.py [--branch game/<id>] [--base origin/main]
Checks committed and uncommitted changes since the branch left the base.
"""
import argparse
import os

from gc26 import BRANCH_PREFIX, GAMES_DIR, Report, git, known_ids

p = argparse.ArgumentParser()
p.add_argument("--branch", default=os.environ.get("GITHUB_HEAD_REF") or None)
p.add_argument("--base", default="origin/main")
args = p.parse_args()

branch = args.branch or git("rev-parse", "--abbrev-ref", "HEAD")
if not branch.startswith(BRANCH_PREFIX):
    print(f"check_scope: '{branch}' is not a game branch, skipped.")
    raise SystemExit(0)

game_id = branch[len(BRANCH_PREFIX):]
r = Report()
if game_id not in known_ids():
    r.error(f"'{game_id}' is not in tools/games.txt (branch must be game/<id>).")

base = git("merge-base", args.base, "HEAD")
changed = set(git("diff", "--name-only", base).splitlines())
changed |= set(git("ls-files", "--others", "--exclude-standard").splitlines())

own = f"{GAMES_DIR}/{game_id}/"
allowed = {f"{GAMES_DIR}/{game_id}.meta"}
for path in sorted(p for p in changed if p):
    if not path.startswith(own) and path not in allowed:
        r.error(f"outside your folder: {path}")

r.finish(f"check_scope: OK, {len(changed)} changed file(s), all inside {own}")
