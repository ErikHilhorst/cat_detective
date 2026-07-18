#!/usr/bin/env python3
"""
Decluttered base-layer generator for Cat Detective.

For each room in the manifest, asks the Gemini image API for N variants of the
room background with its free-standing furniture/objects removed and the floor
continued underneath - the "flat floor" base layer used to build walk-behind
props. EVERY generated image is kept (including bad ones) for human review;
nothing in Content/ is touched.

Outputs: debug_output/layering/<room>/declutter_v<N>.png
         (resized to the room's exact canvas size, ready for Photoshop)

Usage (from the repo root):
  python tools/declutter_rooms.py --manifest tools/declutter_manifest_malibu.json
  python tools/declutter_rooms.py --manifest ... --only kitchen,garden
  python tools/declutter_rooms.py --manifest ... --variants 2
"""

import argparse
import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from layer_room_props import (  # noqa: E402
    REPO_ROOT, DEBUG_ROOT, load_api_key, edit_image, png_bytes, load_candidate)

PROMPT = (
    "Here is a background painting from a 2D isometric adventure game. Edit "
    "this image: completely remove {remove}. {keep_note}"
)

REFINE_PROMPT = (
    "Here is a background painting from a 2D isometric adventure game. An "
    "earlier edit was supposed to remove several objects but some are still "
    "present. Remove ALL of the following that still appear in the image, "
    "leaving none of them: {remove}. {keep_note}"
)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--manifest", required=True)
    ap.add_argument("--only", help="Comma-separated room ids (default: all)")
    ap.add_argument("--variants", type=int, default=None,
                    help="Variants per room (overrides manifest)")
    ap.add_argument("--refine", metavar="V",
                    help="Chained pass: use declutter_v<V>.png as the source and "
                         "ask for the remaining objects; writes declutter_v<V>_pass2.png")
    ap.add_argument("--model", default=None)
    args = ap.parse_args()

    from PIL import Image

    manifest = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
    case_id = manifest["case"]
    model = args.model or manifest.get("model", "gemini-2.5-flash-image")
    variants = args.variants or int(manifest.get("variants", 3))
    keep_note = manifest["keep_note"]
    only = set(args.only.split(",")) if args.only else None

    api_key = load_api_key()
    total = failed = 0

    for entry in manifest["rooms"]:
        room = entry["room"]
        if only and room not in only:
            continue
        out_dir = DEBUG_ROOT / room
        out_dir.mkdir(parents=True, exist_ok=True)
        if args.refine:
            src = out_dir / f"declutter_v{args.refine}.png"
        else:
            src = (REPO_ROOT / "Content" / "Levels" / case_id / room /
                   entry.get("source", "bg_base.jpg"))
        if not src.exists():
            print(f"[FAIL] {room}: source not found: {src}")
            failed += 1
            continue
        original = Image.open(src).convert("RGB")
        tpl = REFINE_PROMPT if args.refine else PROMPT
        prompt = tpl.format(remove=entry["remove"], keep_note=keep_note)

        for v in ([f"{args.refine}_pass2"] if args.refine
                  else range(1, variants + 1)):
            out = out_dir / f"declutter_v{v}.png"
            if out.exists():
                print(f"[skip] {room} v{v}: exists")
                continue
            total += 1
            print(f"[gen ] {room} v{v}/{variants} ({original.size[0]}x{original.size[1]})...")
            try:
                raw = edit_image(png_bytes(original), prompt, model, api_key)
                load_candidate(raw, original.size).save(out)
                print(f"[done] -> {out.relative_to(REPO_ROOT)}")
            except Exception as e:
                print(f"[FAIL] {room} v{v}: {e}")
                failed += 1
            time.sleep(1)

    print(f"\nSummary: {total} images requested, {failed} failed. "
          f"Review under debug_output/layering/<room>/declutter_v*.png")


if __name__ == "__main__":
    main()
