"""Canonical JSON formatter for case_config.json / room_config.json.

Mirrors the hand-written house style so editor saves produce small diffs:
  - 2-space indent, key order preserved
  - arrays of primitives on one line:      "rooms": ["entrance", "bedroom"]
  - short all-primitive objects inside arrays on one line:
        { "displayText": "Rosa", "id": "the_maid", "color": "misc" }
  - everything else expanded one key per line

Run directly to reformat every config in place (the one-time reformat):
    python tools/case_editor/jsonfmt.py            # rewrite
    python tools/case_editor/jsonfmt.py --check    # list files that would change
"""

import json
import sys
from pathlib import Path

INLINE_OBJECT_MAX = 150   # chars; clue objects are always longer, keywords/props shorter


def _is_primitive(v):
    return v is None or isinstance(v, (str, int, float, bool))


def _scalar(v):
    return json.dumps(v, ensure_ascii=False)


def _inline_object(obj):
    if not obj:
        return "{}"
    return "{ " + ", ".join(f"{_scalar(k)}: {_scalar(v)}" for k, v in obj.items()) + " }"


def _fmt(v, indent, in_array):
    pad = "  " * indent
    inner = "  " * (indent + 1)
    if isinstance(v, dict):
        if not v:
            return "{}"
        if in_array and all(_is_primitive(x) for x in v.values()):
            line = _inline_object(v)
            if len(line) <= INLINE_OBJECT_MAX:
                return line
        items = [f"{inner}{_scalar(k)}: {_fmt(x, indent + 1, False)}" for k, x in v.items()]
        return "{\n" + ",\n".join(items) + "\n" + pad + "}"
    if isinstance(v, list):
        if not v:
            return "[]"
        if all(_is_primitive(x) for x in v):
            return "[" + ", ".join(_scalar(x) for x in v) + "]"
        items = [f"{inner}{_fmt(x, indent + 1, True)}" for x in v]
        return "[\n" + ",\n".join(items) + "\n" + pad + "]"
    return _scalar(v)


def dumps(data):
    return _fmt(data, 0, False) + "\n"


def config_files(levels_dir):
    yield from sorted(levels_dir.glob("*/case_config.json"))
    yield from sorted(levels_dir.glob("*/*/room_config.json"))


if __name__ == "__main__":
    repo = Path(__file__).resolve().parents[2]
    check = "--check" in sys.argv
    for path in config_files(repo / "Content" / "Levels"):
        raw = path.read_text(encoding="utf-8")
        out = dumps(json.loads(raw))
        if out != raw:
            print(("would change: " if check else "reformatted: ") + str(path.relative_to(repo)))
            if not check:
                path.write_text(out, encoding="utf-8", newline="\n")
