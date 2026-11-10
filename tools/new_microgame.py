"""Creates the folder skeleton for a microgame.

Usage: python tools/new_microgame.py gc26_tetris
"""
import json
import sys

from gc26 import GAMES_DIR, ROOT, known_ids, namespace, pascal_name

if len(sys.argv) != 2:
    sys.exit(__doc__)
gid = sys.argv[1]
if gid not in known_ids():
    sys.exit(f"'{gid}' is not in tools/games.txt. Ask the organizer to add it.")

folder = ROOT / GAMES_DIR / gid
if folder.exists():
    sys.exit(f"{folder.relative_to(ROOT).as_posix()} already exists.")

ns, name = namespace(gid), pascal_name(gid)
cls = f"Game{name[1:]}" if name.startswith("_") else f"{name}Game"
for sub in ("Scenes", "Scripts", "Prefabs", "Art", "Audio"):
    (folder / sub).mkdir(parents=True)

(folder / "microgame.json").write_text(json.dumps({
    "id": gid,
    "title": name.lstrip("_"),
    "description": "",
    "entry_scene": f"{gid}_Main",
    "authors": [],
}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

(folder / f"{ns}.asmdef").write_text(json.dumps({
    "name": ns,
    "rootNamespace": ns,
    "references": ["GC26.Core"],
    "autoReferenced": False,
}, indent=2) + "\n", encoding="utf-8")

(folder / "Scripts" / f"{cls}.cs").write_text(f"""using System;
using GC26.Core;
using UnityEngine;

namespace {ns}
{{
    public class {cls} : MonoBehaviour, IMicrogame
    {{
        public const string Id = "{gid}";

        Action<int> _exit;
        bool _started;

        public void Begin(Action<int> exit)
        {{
            _started = true;
            _exit = exit;
            // TODO: load the save (MicrogameSave.Folder(Id)) and start the game
        }}

        void Start()
        {{
            // Scene opened on its own (no shell): run for testing.
            if (!_started && !MicrogameRunner.IsRunning)
                Begin(score => Debug.Log($"[{{Id}}] exit, score = {{score}}"));
        }}

        /// <summary>Call from the pause menu ("To menu") or the game-over screen.</summary>
        public void ExitToMenu(int score)
        {{
            // TODO: save progress before leaving
            var exit = _exit;
            _exit = null;
            exit?.Invoke(score);
        }}
    }}
}}
""", encoding="utf-8")

(folder / "README.md").write_text(f"""# {gid}

- Ветка: `game/{gid}`
- Неймспейс: `{ns}`
- Стартовая сцена: `Scenes/{gid}_Main.unity` (создать в Unity, повесить `{cls}`)
- Команда:

Как играть:
""", encoding="utf-8")

print(f"Created {GAMES_DIR}/{gid}/ - open Unity, create Scenes/{gid}_Main.unity and add {cls} to it.")
