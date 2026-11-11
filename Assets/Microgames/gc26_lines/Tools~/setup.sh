#!/bin/bash
# One-time setup of Lines on a new machine (or after cloning again). Safe to run more than once.
# Run it from anywhere in the project, before opening Unity:
#   Assets/Microgames/gc26_lines/Tools~/setup.sh
# It makes sure the pictures and fonts are real files (Git LFS), turns on the team's pre-push check,
# keeps local-only files out of git, and adds the root CLAUDE.md pointer for Claude.
set -euo pipefail
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
GAME=Assets/Microgames/gc26_lines
ok() { echo "  ✓ $1"; }
problem() { echo "  ✗ $1" >&2; FAILED=1; }
FAILED=0

echo "Lines setup in $(pwd)"

# 1. Pictures and fonts are stored with Git LFS. Without it they arrive as small text placeholders,
#    and the game shows no mushrooms, no forest and the wrong font.
if git lfs version >/dev/null 2>&1; then
  git config --unset core.hooksPath 2>/dev/null || true   # so Git LFS can install its hooks
  git lfs install --local --force >/dev/null
  git lfs pull
  if file "$GAME/Resources/gc26_lines/Mushrooms/mushroom_0.png" | grep -q "PNG image"; then
    ok "pictures and fonts downloaded (Git LFS)"
  else
    problem "the mushroom pictures are still placeholders; run 'git lfs pull' and check for errors"
  fi
else
  problem "Git LFS is not installed. Install it (macOS: 'brew install git-lfs'; Sourcetree has it built in:
      Repository → Git LFS → Initialize), then run this script again."
fi

# 2. Before every push: the team's check (RULES.md) and the Git LFS upload of pictures and fonts.
#    RULES.md says 'git config core.hooksPath tools/hooks', but that switches off Git LFS's own push
#    step, so pushed pictures would never reach GitHub. Instead, one hook in .git/hooks runs both.
git config --unset core.hooksPath 2>/dev/null || true
HOOK="$(git rev-parse --git-path hooks)/pre-push"
mkdir -p "$(dirname "$HOOK")"
cat > "$HOOK" <<'HOOK_END'
#!/bin/sh
# Made by Assets/Microgames/gc26_lines/Tools~/setup.sh: the team's check, then the Git LFS upload.
sh "$(git rev-parse --show-toplevel)/tools/hooks/pre-push" "$@" < /dev/null || exit 1
command -v git-lfs >/dev/null 2>&1 || { echo >&2 "Git LFS is not installed: pictures and fonts can't be pushed."; exit 2; }
git lfs pre-push "$@"
HOOK_END
chmod +x "$HOOK"
ok "before each push: team check and picture upload (Git LFS)"

# 3. Files that are made on each machine and must not be committed (outside our folder).
EXCLUDE=.git/info/exclude
mkdir -p .git/info; touch "$EXCLUDE"
for line in "/Builds/" ".vscode/" "/Assets/Microgames.meta" "/CLAUDE.md"; do
  grep -qxF "$line" "$EXCLUDE" || echo "$line" >> "$EXCLUDE"
done
ok "build output, editor settings and the root CLAUDE.md kept out of git"

# 4. Claude reads CLAUDE.md from the folder it starts in; ours lives in the game folder.
if [ ! -f CLAUDE.md ]; then
  echo "@$GAME/CLAUDE.md" > CLAUDE.md
  ok "root CLAUDE.md created (points to $GAME/CLAUDE.md)"
else
  ok "root CLAUDE.md already there"
fi

# 5. The Unity version the project needs.
VERSION=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
if [ -d "/Applications/Unity/Hub/Editor/$VERSION" ]; then
  ok "Unity $VERSION is installed"
else
  echo "  ! Unity $VERSION was not found in /Applications/Unity/Hub/Editor. Install exactly this version through Unity Hub."
fi

echo
if [ "$FAILED" = 0 ]; then
  echo "Done. Open the project in Unity Hub (Add → this folder). The game's scene, Scenes/gc26_lines_Main,"
  echo "opens by itself; press Play to start Lines."
else
  echo "Some steps need attention (see ✗ above)." >&2; exit 1
fi
