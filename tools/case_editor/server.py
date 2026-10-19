"""Case editor server - stdlib only.

    python tools/case_editor/server.py [--port 8765]

Serves the editor page and a tiny JSON API over Content/Levels:
  GET  /api/cases                      -> case ids (folders with a case_config.json)
  GET  /api/case/<case>                -> case config, every room config + map, asset lists
  GET  /api/versions/<case>            -> {file key: version} for change polling
  POST /api/save                       -> {case, room|null, data, version} -> writes formatted JSON
  GET  /content/<path>                 -> images under Content/ (backgrounds, sprites)

Writes are guarded by a content hash: if the file changed on disk since the
editor loaded it (Claude, a skill, a manual edit), the save is refused with 409
and the editor reloads instead of overwriting. room_map.json is read-only here.
"""

import hashlib
import json
import sys
import webbrowser
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote

sys.path.insert(0, str(Path(__file__).parent))
import jsonfmt  # noqa: E402

EDITOR_DIR = Path(__file__).resolve().parent
REPO = EDITOR_DIR.parents[1]
CONTENT = REPO / "Content"
LEVELS = CONTENT / "Levels"
IMAGE_TYPES = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg"}


def version_of(path):
    return hashlib.sha1(path.read_bytes()).hexdigest() if path.exists() else ""


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def case_rooms(case_dir):
    """Rooms listed in case_config first, then any other room folders on disk."""
    listed = read_json(case_dir / "case_config.json").get("rooms", [])
    on_disk = sorted(p.parent.name for p in case_dir.glob("*/room_config.json"))
    return listed + [r for r in on_disk if r not in listed]


def config_path(case_id, room_id):
    case_dir = LEVELS / case_id
    path = case_dir / room_id / "room_config.json" if room_id else case_dir / "case_config.json"
    # Reject path tricks: the target must stay inside Content/Levels/<case>.
    if case_dir.resolve() not in path.resolve().parents:
        raise ValueError("bad path")
    return path


def find_background(room_dir):
    for name in ("bg_base.jpg", "bg_base.png"):
        if (room_dir / name).exists():
            return f"Levels/{room_dir.parent.name}/{room_dir.name}/{name}"
    return None


def load_case(case_id):
    case_dir = LEVELS / case_id
    case_path = case_dir / "case_config.json"
    rooms = {}
    for room_id in case_rooms(case_dir):
        room_dir = case_dir / room_id
        cfg_path = room_dir / "room_config.json"
        map_path = room_dir / "room_map.json"
        sprite_dir = room_dir / "Interactables"
        rooms[room_id] = {
            "config": read_json(cfg_path) if cfg_path.exists() else None,
            "version": version_of(cfg_path),
            "map": read_json(map_path) if map_path.exists() else None,
            "background": find_background(room_dir),
            "sprites": sorted(p.stem for p in sprite_dir.glob("*.png")) if sprite_dir.exists() else [],
        }
    return {
        "id": case_id,
        "config": read_json(case_path),
        "version": version_of(case_path),
        "rooms": rooms,
    }


def versions(case_id):
    case_dir = LEVELS / case_id
    out = {"": version_of(case_dir / "case_config.json")}
    for room_id in case_rooms(case_dir):
        out[room_id] = version_of(case_dir / room_id / "room_config.json")
    return out


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(EDITOR_DIR), **kwargs)

    def log_message(self, fmt, *args):
        if "/api/versions" not in (args[0] if args else ""):
            super().log_message(fmt, *args)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def send_json(self, obj, status=200):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = unquote(self.path.split("?")[0])
        try:
            if path == "/api/cases":
                cases = sorted(p.parent.name for p in LEVELS.glob("*/case_config.json"))
                return self.send_json(cases)
            if path.startswith("/api/case/"):
                return self.send_json(load_case(path[len("/api/case/"):]))
            if path.startswith("/api/versions/"):
                return self.send_json(versions(path[len("/api/versions/"):]))
            if path.startswith("/content/"):
                return self.send_content(path[len("/content/"):])
        except Exception as ex:  # surface errors to the page instead of a dropped socket
            return self.send_json({"error": str(ex)}, 500)
        return super().do_GET()

    def send_content(self, rel):
        target = (CONTENT / rel).resolve()
        mime = IMAGE_TYPES.get(target.suffix.lower())
        if mime is None or CONTENT.resolve() not in target.parents or not target.exists():
            return self.send_error(404)
        body = target.read_bytes()
        self.send_response(200)
        self.send_header("Content-Type", mime)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        if self.path != "/api/save":
            return self.send_error(404)
        try:
            req = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
            path = config_path(req["case"], req.get("room") or "")
            current = version_of(path)
            if current != req.get("version", ""):
                return self.send_json({"error": "changed on disk", "version": current}, 409)
            text = jsonfmt.dumps(req["data"])
            path.write_text(text, encoding="utf-8", newline="\n")
            return self.send_json({"version": version_of(path)})
        except Exception as ex:
            return self.send_json({"error": str(ex)}, 500)


if __name__ == "__main__":
    port = int(sys.argv[sys.argv.index("--port") + 1]) if "--port" in sys.argv else 8765
    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    url = f"http://127.0.0.1:{port}/"
    print(f"Case editor: {url}  (Ctrl+C to stop)")
    if "--no-browser" not in sys.argv:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
