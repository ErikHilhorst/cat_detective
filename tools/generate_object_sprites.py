#!/usr/bin/env python3
"""
Object sprite generator for Cat Detective.

Pipeline per object (driven by a sprite manifest JSON):
  1. Generate a single-object image on a white background via the Gemini
     image API ("nano banana"). Raw output is kept in debug_output/sprite_raw/
     for review and reprocessing.
  2. Remove the white background with a flood fill that starts at the image
     borders (so white/cream areas INSIDE an object are preserved).
  3. Trim to the object's bounding box, resize to the manifest's target size,
     and save as an optimized RGBA PNG under
     Content/Levels/<case>/<room>/Interactables/<id>.png
  4. Optionally (--write-mgcb) append the standard PremultiplyAlpha=False
     entry for each new sprite to Content/Content.mgcb.

The Gemini API key is read from the GEMINI_API_KEY environment variable,
falling back to a KEY=VALUE line in .env at the repo root.

Usage (from the repo root):
  python tools/generate_object_sprites.py --manifest tools/sprite_manifest_malibu.json
  python tools/generate_object_sprites.py --manifest ... --only inspect_birdcage,inspect_treat_jar
  python tools/generate_object_sprites.py --manifest ... --reprocess --tolerance 215
  python tools/generate_object_sprites.py --manifest ... --write-mgcb
  python tools/generate_object_sprites.py --scan malibu_mansion   # print a manifest skeleton
"""

import argparse
import base64
import json
import sys
import time
from collections import deque
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
RAW_DIR = REPO_ROOT / "debug_output" / "sprite_raw"
MGCB_PATH = REPO_ROOT / "Content" / "Content.mgcb"
DEFAULT_MODEL = "gemini-2.5-flash-image"
API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"

MGCB_BLOCK = """
#begin {rel}
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyEnabled=False
/processorParam:GenerateMipmaps=False
/processorParam:PremultiplyAlpha=False
/processorParam:ResizeToPowerOfTwo=False
/processorParam:MakeSquare=False
/processorParam:TextureFormat=Color
/build:{rel}
"""


# ── environment ─────────────────────────────────────────────────────────────

def load_api_key() -> str:
    import os
    key = os.environ.get("GEMINI_API_KEY", "")
    if not key:
        env_file = REPO_ROOT / ".env"
        if env_file.exists():
            for line in env_file.read_text(encoding="utf-8").splitlines():
                line = line.strip()
                if line.startswith("GEMINI_API_KEY") and "=" in line:
                    key = line.split("=", 1)[1].strip().strip('"').strip("'")
                    break
    if not key:
        sys.exit("ERROR: GEMINI_API_KEY not found in environment or .env at repo root.")
    return key


# ── Gemini call ──────────────────────────────────────────────────────────────

def generate_image(prompt: str, model: str, api_key: str) -> bytes:
    """Call Gemini and return the first inline image as raw bytes."""
    import requests

    body = {"contents": [{"parts": [{"text": prompt}]}]}
    headers = {"x-goog-api-key": api_key, "Content-Type": "application/json"}
    url = API_URL.format(model=model)

    last_err = ""
    for attempt in range(1, 4):
        resp = requests.post(url, headers=headers, json=body, timeout=120)
        if resp.status_code in (429, 500, 503):
            wait = 10 * (2 ** (attempt - 1))
            print(f"    HTTP {resp.status_code}, retrying in {wait}s ({attempt}/3)...")
            time.sleep(wait)
            last_err = f"HTTP {resp.status_code}: {resp.text[:300]}"
            continue
        if resp.status_code != 200:
            raise RuntimeError(f"HTTP {resp.status_code}: {resp.text[:500]}")

        data = resp.json()
        candidates = data.get("candidates") or []
        if not candidates:
            reason = data.get("promptFeedback", {}).get("blockReason", "no candidates")
            raise RuntimeError(f"No image returned ({reason})")
        for part in candidates[0].get("content", {}).get("parts", []):
            inline = part.get("inlineData") or part.get("inline_data")
            if inline and inline.get("data"):
                return base64.b64decode(inline["data"])
        raise RuntimeError("Response contained no inline image data")

    raise RuntimeError(f"Gave up after retries. Last error: {last_err}")


# ── background removal ───────────────────────────────────────────────────────

def cutout_white_background(img, tolerance: int):
    """
    Flood-fill near-white pixels reachable from the image borders and make
    them transparent. White areas enclosed inside the object are untouched.
    Boundary pixels get a soft alpha based on how white they are.
    """
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    removed = bytearray(w * h)
    q = deque()

    def is_bg(x, y):
        r, g, b, _ = px[x, y]
        return r >= tolerance and g >= tolerance and b >= tolerance

    for x in range(w):
        for y in (0, h - 1):
            if not removed[y * w + x] and is_bg(x, y):
                removed[y * w + x] = 1
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if not removed[y * w + x] and is_bg(x, y):
                removed[y * w + x] = 1
                q.append((x, y))

    while q:
        x, y = q.popleft()
        if x > 0 and not removed[y * w + x - 1] and is_bg(x - 1, y):
            removed[y * w + x - 1] = 1
            q.append((x - 1, y))
        if x < w - 1 and not removed[y * w + x + 1] and is_bg(x + 1, y):
            removed[y * w + x + 1] = 1
            q.append((x + 1, y))
        if y > 0 and not removed[(y - 1) * w + x] and is_bg(x, y - 1):
            removed[(y - 1) * w + x] = 1
            q.append((x, y - 1))
        if y < h - 1 and not removed[(y + 1) * w + x] and is_bg(x, y + 1):
            removed[(y + 1) * w + x] = 1
            q.append((x, y + 1))

    # Apply transparency; feather kept pixels that border the removed region.
    feather_floor = 180
    for y in range(h):
        row = y * w
        for x in range(w):
            if removed[row + x]:
                r, g, b, _ = px[x, y]
                px[x, y] = (r, g, b, 0)
                continue
            touches_bg = (
                (x > 0 and removed[row + x - 1])
                or (x < w - 1 and removed[row + x + 1])
                or (y > 0 and removed[row - w + x])
                or (y < h - 1 and removed[row + w + x])
            )
            if touches_bg:
                r, g, b, a = px[x, y]
                whiteness = min(r, g, b)
                if whiteness >= feather_floor:
                    soft = int(255 * (255 - whiteness) / (255 - feather_floor))
                    px[x, y] = (r, g, b, min(a, max(soft, 0)))
    return img


def trim_and_resize(img, target_size: int, padding: int = 2):
    """Trim to the alpha bounding box, pad, and scale so max(w,h) == target."""
    from PIL import Image

    bbox = img.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError("Cutout produced a fully transparent image")
    img = img.crop(bbox)

    padded = Image.new("RGBA", (img.width + padding * 2, img.height + padding * 2), (0, 0, 0, 0))
    padded.paste(img, (padding, padding))
    img = padded

    factor = target_size / max(img.width, img.height)
    if factor < 1.0:
        img = img.resize(
            (max(1, round(img.width * factor)), max(1, round(img.height * factor))),
            Image.LANCZOS,
        )
    return img


# ── mgcb ─────────────────────────────────────────────────────────────────────

def write_mgcb_entries(case_id: str, rel_paths: list) -> int:
    text = MGCB_PATH.read_text(encoding="utf-8")
    missing = [rel for rel in rel_paths if f"#begin {rel}" not in text]
    if not missing:
        return 0
    header = f"# ── Case: {case_id} — object sprites (generated) ────────────────────────────"
    additions = ""
    if header not in text:
        additions += f"\n{header}\n"
    for rel in missing:
        additions += MGCB_BLOCK.format(rel=rel)
    # The mgcb file uses LF line endings; suppress newline translation on Windows.
    with open(MGCB_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write(text.rstrip("\n") + "\n" + additions)
    return len(missing)


# ── manifest scan helper ─────────────────────────────────────────────────────

def scan_case(case_id: str):
    """Print a manifest skeleton for every placeholder-textured interactable.

    placeholder_object entries become plain objects; placeholder_person entries
    are tagged "character": true so they use character_prompt_template.
    """
    case_dir = REPO_ROOT / "Content" / "Levels" / case_id
    if not case_dir.is_dir():
        sys.exit(f"ERROR: no such case folder: {case_dir}")
    objects = []
    for cfg_path in sorted(case_dir.glob("*/room_config.json")):
        room = cfg_path.parent.name
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
        for it in cfg.get("interactables", []):
            tex = it.get("texture", "")
            is_character = "placeholder_person" in tex
            if "placeholder_object" not in tex and not is_character:
                continue
            scale = float(it.get("scale", 0.1))
            entry = {
                "id": it["id"],
                "room": room,
                "size": round(1024 * scale),
                "prompt": "TODO - one-sentence description",
            }
            if is_character:
                entry["character"] = True
            objects.append(entry)
    manifest = {
        "case": case_id,
        "prompt_template": (
            "Create an object that I can use for a videogame sprite, in the style of "
            "Studio Ghibli, soft hand-painted cel shading, cozy warm palette. Draw exactly "
            "one object, centered and filling most of the frame: {description} Create a "
            "full white background so the object can be easily cut out - no drop shadow, "
            "no ground plane, no text, no watermark."
        ),
        "character_prompt_template": (
            "Create a character that I can use for a videogame sprite, in the style of "
            "Studio Ghibli, soft hand-painted cel shading, cozy warm palette. Draw exactly "
            "one character, full body visible, centered and filling most of the frame: "
            "{description} The head must be in the upper part of the image (it is cropped "
            "for a dialogue portrait). Create a full white background so the character can "
            "be easily cut out - no drop shadow, no ground plane, no text, no watermark."
        ),
        "objects": objects,
    }
    print(json.dumps(manifest, indent=2))


# ── main ─────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--manifest", help="Path to the sprite manifest JSON")
    ap.add_argument("--scan", metavar="CASE_ID", help="Print a manifest skeleton for a case and exit")
    ap.add_argument("--only", help="Comma-separated object ids to process (default: all)")
    ap.add_argument("--force", action="store_true", help="Regenerate/overwrite existing outputs")
    ap.add_argument("--reprocess", action="store_true",
                    help="Skip the API; re-run cutout/resize on existing raw images")
    ap.add_argument("--model", default=DEFAULT_MODEL, help=f"Gemini model id (default: {DEFAULT_MODEL})")
    ap.add_argument("--tolerance", type=int, default=235,
                    help="Whiteness floor (0-255) for background flood fill; lower eats faint shadows (default: 235)")
    ap.add_argument("--write-mgcb", action="store_true",
                    help="Append missing Content.mgcb entries for the manifest's sprites")
    ap.add_argument("--dry-run", action="store_true", help="List planned work without doing it")
    args = ap.parse_args()

    if args.scan:
        scan_case(args.scan)
        return
    if not args.manifest:
        ap.error("--manifest is required (or use --scan CASE_ID)")

    from PIL import Image

    manifest = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
    case_id = manifest["case"]
    template = manifest["prompt_template"]
    only = set(args.only.split(",")) if args.only else None

    jobs = []
    for obj in manifest["objects"]:
        if obj.get("skip"):
            continue
        if only and obj["id"] not in only:
            continue
        jobs.append(obj)
    if only:
        unknown = only - {o["id"] for o in manifest["objects"]}
        if unknown:
            sys.exit(f"ERROR: ids not in manifest: {', '.join(sorted(unknown))}")

    api_key = None
    ok, failed, skipped = [], [], []

    for obj in jobs:
        oid, room, size = obj["id"], obj["room"], int(obj["size"])
        out_path = REPO_ROOT / "Content" / "Levels" / case_id / room / "Interactables" / f"{oid}.png"
        raw_path = RAW_DIR / f"{oid}.png"

        if out_path.exists() and not args.force:
            skipped.append(oid)
            continue
        if args.dry_run:
            print(f"[dry-run] {oid:32s} room={room:12s} size={size}px -> {out_path.relative_to(REPO_ROOT)}")
            continue

        try:
            if args.reprocess:
                if not raw_path.exists():
                    raise RuntimeError(f"--reprocess set but no raw image at {raw_path}")
            elif not raw_path.exists() or args.force:
                if api_key is None:
                    api_key = load_api_key()
                # Characters get their own template (portrait-safe head placement);
                # falls back to the object template for older manifests.
                tpl = template
                if obj.get("character"):
                    tpl = manifest.get("character_prompt_template", template)
                prompt = tpl.format(description=obj["prompt"])
                print(f"[gen ] {oid} ({room})...")
                raw_bytes = generate_image(prompt, args.model, api_key)
                RAW_DIR.mkdir(parents=True, exist_ok=True)
                raw_path.write_bytes(raw_bytes)
                time.sleep(1)  # be polite to the rate limiter

            print(f"[cut ] {oid}: flood-fill cutout (tolerance {args.tolerance})...")
            img = Image.open(raw_path)
            img = cutout_white_background(img, args.tolerance)
            img = trim_and_resize(img, size)
            out_path.parent.mkdir(parents=True, exist_ok=True)
            img.save(out_path, optimize=True)
            print(f"[done] {oid}: {img.width}x{img.height} -> {out_path.relative_to(REPO_ROOT)}")
            ok.append(obj)
        except Exception as e:
            print(f"[FAIL] {oid}: {e}")
            failed.append(oid)

    if args.write_mgcb and not args.dry_run:
        rels = [
            f"Levels/{case_id}/{o['room']}/Interactables/{o['id']}.png"
            for o in manifest["objects"]
            if not o.get("skip")
            and (REPO_ROOT / "Content" / "Levels" / case_id / o["room"] / "Interactables" / f"{o['id']}.png").exists()
        ]
        added = write_mgcb_entries(case_id, rels)
        print(f"[mgcb] {added} new entries appended to Content.mgcb")

    print(f"\nSummary: {len(ok)} generated, {len(skipped)} skipped (exists; use --force), {len(failed)} failed.")
    if failed:
        print("Failed: " + ", ".join(failed))
    if ok:
        print("\nNext steps:")
        print("  1. Set \"scale\": 1.0 for these objects in their room_config.json (sprites are pre-sized).")
        print("  2. Ensure Content.mgcb has entries (--write-mgcb does this), then: dotnet build")
        print("  3. Verify: dotnet run --no-build -- --screenshot " + case_id + " <room_id>")
    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()
