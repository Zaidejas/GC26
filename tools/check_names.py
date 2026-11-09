"""Checks every microgame folder for naming rules (see RULES.md).

Usage: python tools/check_names.py [<id> ...]   (no ids = all games)
"""
import json
import re
import sys

from gc26 import GAMES_DIR, ID_RE, ROOT, Report, known_ids, namespace

GENERIC = {
    "gamemanager", "manager", "game", "player", "enemy", "main", "ui", "hud",
    "level", "level1", "menu", "mainmenu", "audiomanager", "soundmanager",
    "inputmanager", "gamecontroller", "controller", "settings", "constants",
}
FIELDS = {"id": str, "title": str, "description": str, "entry_scene": str, "authors": list}
NS_RE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)
PREFS_RE = re.compile(r"PlayerPrefs\.\w+\(\s*\"([^\"]*)\"")

r = Report()
games_root = ROOT / GAMES_DIR
ids = known_ids()
only = set(sys.argv[1:])

for gid in ids:
    if not ID_RE.match(gid):
        r.error(f"tools/games.txt: bad id '{gid}' (gc26_ + a-z 0-9 _)")

folders = sorted(d for d in games_root.iterdir() if d.is_dir()) if games_root.is_dir() else []
for folder in folders:
    gid = folder.name
    if only and gid not in only:
        continue
    where = f"{GAMES_DIR}/{gid}"
    if gid not in ids:
        r.error(f"{where}: folder is not a known id in tools/games.txt")
        continue

    # passport
    manifest = folder / "microgame.json"
    data = {}
    if not manifest.is_file():
        r.error(f"{where}: missing microgame.json")
    else:
        try:
            data = json.loads(manifest.read_text(encoding="utf-8-sig"))
        except ValueError as e:
            r.error(f"{where}/microgame.json: invalid JSON ({e})")
        for key, typ in FIELDS.items():
            if not isinstance(data.get(key), typ):
                r.error(f"{where}/microgame.json: field '{key}' missing or wrong type")
        if data.get("id") not in (None, gid):
            r.error(f"{where}/microgame.json: id '{data.get('id')}' != folder '{gid}'")
        entry = data.get("entry_scene")
        if isinstance(entry, str):
            if not entry.startswith(gid + "_"):
                r.error(f"{where}/microgame.json: entry_scene '{entry}' must start with '{gid}_'")
            elif not (folder / "Scenes" / f"{entry}.unity").is_file():
                r.warn(f"{where}: entry scene Scenes/{entry}.unity not created yet")

    # assembly definition
    ns = namespace(gid)
    asmdef = folder / f"{ns}.asmdef"
    if not asmdef.is_file():
        r.error(f"{where}: missing {ns}.asmdef")
    else:
        try:
            a = json.loads(asmdef.read_text(encoding="utf-8-sig"))
            if a.get("name") != ns:
                r.error(f"{where}/{asmdef.name}: name must be '{ns}'")
            if a.get("rootNamespace") not in (None, "", ns):
                r.error(f"{where}/{asmdef.name}: rootNamespace must be '{ns}'")
        except ValueError as e:
            r.error(f"{where}/{asmdef.name}: invalid JSON ({e})")
    for extra in folder.rglob("*.asmdef"):
        if extra != asmdef and "Editor" not in extra.parts and "Tests" not in extra.parts:
            r.warn(f"{extra.relative_to(ROOT).as_posix()}: extra asmdef, name it {ns}.<Something>")

    for f in folder.rglob("*"):
        rel = f.relative_to(ROOT).as_posix()
        if f.is_dir():
            if f.name == "Resources":
                subs = [c.name for c in f.iterdir() if not c.name.endswith(".meta")]
                if subs != [gid] and subs:
                    r.error(f"{rel}: may contain only the folder '{gid}/' (found: {', '.join(subs)})")
            continue
        stem, ext = f.stem.lower(), f.suffix.lower()
        if ext == ".meta":
            continue
        if ext == ".unity" and not f.stem.startswith(gid + "_"):
            r.error(f"{rel}: scene name must start with '{gid}_'")
        if ext in (".cs", ".prefab", ".unity", ".asset") and stem in GENERIC:
            r.error(f"{rel}: generic name '{f.stem}', add the game name (e.g. {ns.split('.')[1]}{f.stem})")
        if ext == ".cs":
            code = f.read_text(encoding="utf-8-sig", errors="replace")
            for key in PREFS_RE.findall(code):
                if not key.startswith(gid + "_"):
                    r.error(f"{rel}: PlayerPrefs key '{key}' must start with '{gid}_' (or use MicrogameSave.Key)")
            if "Application.persistentDataPath" in code:
                r.warn(f"{rel}: use MicrogameSave.Folder(Id) instead of Application.persistentDataPath")
            spaces = NS_RE.findall(code)
            if not spaces:
                r.error(f"{rel}: no namespace, wrap code in 'namespace {ns}'")
            for s in spaces:
                if s != ns and not s.startswith(ns + "."):
                    r.error(f"{rel}: namespace '{s}' must be '{ns}' or '{ns}.*'")

r.finish(f"check_names: OK, {len(folders)} game folder(s) checked")
