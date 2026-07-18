#!/usr/bin/env python3
"""
Foreground-prop layer generator for Cat Detective.

Turns a flat pre-rendered room background into walk-behind layers. The Gemini
image model ("nano banana") re-renders the WHOLE image on every edit (subtle
global repaint + a few px of drift), so a naive before/after diff cannot
isolate an object. Instead each prop is built from two independent edits:

  1. MASK call - Gemini recolors the prop to a flat magenta (#FF00FF)
     silhouette. Extracting "where is magenta" is immune to global re-render
     noise. The silhouette is aligned back to the original with a local
     translation search, and the prop overlay PNG is cut from the ORIGINAL
     art using that mask - no AI pixels in the prop itself.
  2. REMOVAL call - Gemini removes the prop and inpaints what is behind it.
     Those pixels are composited into the new bg_base only INSIDE the dilated
     mask (soft edge), locally aligned. Everything outside the mask keeps the
     original pixels, so the global repaint never touches the scene.

Both calls validate programmatically and retry up to a per-prop cap under a
global image budget. Saved candidates in debug_output/layering/<room>/ are
reused on reruns before any new API call is made.

Outputs (for case/room from the manifest):
  Content/Levels/<case>/<room>/prop_<id>.png   full-canvas RGBA overlays
  Content/Levels/<case>/<room>/bg_base.jpg     props removed (original backed
                                               up as "full scene.jpg"; reruns
                                               read the backup - idempotent)
  debug_output/layering/<room>/                candidates, masks, report.json

The report suggests room_config props[] entries (sortY = mask bottom) and
Tiled Triggers-layer fade rects - review and paste/tune those by hand.

The Gemini API key is read from GEMINI_API_KEY or .env (same as
generate_object_sprites.py).

Usage (from the repo root):
  python tools/layer_room_props.py --manifest tools/layer_manifest_malibu_entrance.json
  python tools/layer_room_props.py --manifest ... --only prop_newel_post --force
  python tools/layer_room_props.py --manifest ... --reprocess   # no API calls
"""

import argparse
import base64
import io
import json
import shutil
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DEBUG_ROOT = REPO_ROOT / "debug_output" / "layering"
DEFAULT_MODEL = "gemini-2.5-flash-image"
API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"

MASK_PROMPT = (
    "Here is a background painting from a 2D isometric adventure game, with a "
    "red rectangle drawn on it as a marker. I need a selection mask for image "
    "editing. Paint a solid, flat, bright MAGENTA (#FF00FF) overlay over "
    "{description}. The magenta overlay must cover exactly those pixels, "
    "fully opaque, like a selection highlight - no shading, no outline, no "
    "texture. Do NOT draw the red rectangle in your output. Leave every "
    "other pixel of the painting exactly as it is. Keep the exact same "
    "framing and aspect ratio: no zoom, no crop, no color grading, no added "
    "text or watermark."
)

REMOVE_PROMPT = (
    "Here is a background painting from a 2D isometric adventure game, with a "
    "red rectangle drawn on it as a marker. Edit this image: completely "
    "remove the object marked by the red rectangle ({description}). "
    "Reconstruct whatever is behind and beneath it (floor, wall, shadows) so "
    "the area looks like the object was never there, seamlessly matching the "
    "surrounding art style, lighting and textures. Do NOT draw the red "
    "rectangle in your output. CRITICAL: change nothing else in the image. "
    "Keep the exact same framing and aspect ratio: no zoom, no crop, no "
    "color grading, no added text or watermark."
)

MIN_MASK_REGION_FRAC = 0.04    # magenta must cover at least this much of the region
MAX_MASK_STRAY_FRAC = 0.002    # magenta outside the expanded region (per outside px)
MIN_REMOVED_DIFF = 22.0        # mean-abs diff inside mask core = prop really gone
FLOOR_SIMILAR = 18.0           # orig ~= inpaint below this = floor, cut from the mask
REGION_EXPAND = 40             # px of slack around the declared region
ALIGN_SEARCH = 10              # +/- px translation search for local alignment


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


def edit_image(image_png: bytes, prompt: str, model: str, api_key: str) -> bytes:
    """Send an image + edit instruction to Gemini, return the edited image bytes."""
    import requests

    body = {
        "contents": [{
            "parts": [
                {"text": prompt},
                {"inline_data": {"mime_type": "image/png",
                                 "data": base64.b64encode(image_png).decode("ascii")}},
            ]
        }]
    }
    headers = {"x-goog-api-key": api_key, "Content-Type": "application/json"}
    url = API_URL.format(model=model)

    last_err = ""
    for attempt in range(1, 4):
        resp = requests.post(url, headers=headers, json=body, timeout=180)
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


# ── image math ───────────────────────────────────────────────────────────────

def to_array(img):
    import numpy as np
    return np.asarray(img.convert("RGB"), dtype=np.float32)


def png_bytes(img) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def load_candidate(path_or_bytes, size):
    from PIL import Image
    img = (Image.open(io.BytesIO(path_or_bytes)) if isinstance(path_or_bytes, bytes)
           else Image.open(path_or_bytes))
    img = img.convert("RGB")
    if img.size != size:
        img = img.resize(size, Image.LANCZOS)
    return img


def region_slices(region, size, expand=0):
    x, y, w, h = region
    W, H = size
    x0 = max(0, x - expand); y0 = max(0, y - expand)
    x1 = min(W, x + w + expand); y1 = min(H, y + h + expand)
    return slice(y0, y1), slice(x0, x1)


def local_offset(orig_arr, cand_arr, region, size):
    """Best (dx, dy) mapping candidate -> original, judged on a ring of
    unchanged content around the prop's region."""
    import numpy as np
    H, W = orig_arr.shape[:2]
    s = ALIGN_SEARCH
    ys, xs = region_slices(region, size, REGION_EXPAND + 50)
    y0 = max(ys.start, s); y1 = min(ys.stop, H - s)
    x0 = max(xs.start, s); x1 = min(xs.stop, W - s)
    if y1 - y0 < 40 or x1 - x0 < 40:
        return 0, 0

    ring = np.ones((y1 - y0, x1 - x0), dtype=bool)
    rys, rxs = region_slices(region, size, 10)
    iy0 = max(rys.start - y0, 0); iy1 = max(rys.stop - y0, 0)
    ix0 = max(rxs.start - x0, 0); ix1 = max(rxs.stop - x0, 0)
    ring[iy0:iy1, ix0:ix1] = False
    if not ring.any():
        return 0, 0

    og = orig_arr[y0:y1, x0:x1].mean(axis=2)
    best = (1e9, 0, 0)
    for dy in range(-s, s + 1):
        for dx in range(-s, s + 1):
            cg = cand_arr[y0 + dy:y1 + dy, x0 + dx:x1 + dx].mean(axis=2)
            err = float(np.abs(og - cg)[ring].mean())
            if err < best[0]:
                best = (err, dx, dy)
    return best[1], best[2]


def shift_bool(mask, dx, dy):
    """Shift a boolean array by (-dx, -dy): candidate space -> original space."""
    import numpy as np
    out = np.zeros_like(mask)
    H, W = mask.shape
    ys_src = slice(max(0, dy), min(H, H + dy))
    xs_src = slice(max(0, dx), min(W, W + dx))
    ys_dst = slice(max(0, -dy), min(H, H - dy))
    xs_dst = slice(max(0, -dx), min(W, W - dx))
    out[ys_dst, xs_dst] = mask[ys_src, xs_src]
    return out


def shift_image(arr, dx, dy):
    """Shift an RGB array by (-dx, -dy), edge-padding: candidate -> original space."""
    import numpy as np
    return np.roll(np.roll(arr, -dy, axis=0), -dx, axis=1)


def annotated_input(original, region):
    """The original scene with a red marker rectangle around the region."""
    from PIL import ImageDraw
    img = original.copy()
    d = ImageDraw.Draw(img)
    x, y, w, h = region
    pad = 12
    d.rectangle([x - pad, y - pad, x + w + pad, y + h + pad],
                outline=(255, 0, 0), width=4)
    return img


def magenta_mask(cand_arr):
    """Magenta-family silhouette pixels (the model often uses a darker
    purple-magenta than the requested #FF00FF, so match by hue)."""
    r, g, b = cand_arr[..., 0], cand_arr[..., 1], cand_arr[..., 2]
    return (r - g > 55) & (b - g > 35) & (g < 110)


def red_contamination(cand_arr, area_mask):
    """Fraction of saturated marker-red pixels inside a fill area."""
    r, g, b = cand_arr[..., 0], cand_arr[..., 1], cand_arr[..., 2]
    red = (r > 175) & (g < 90) & (b < 90)
    total = float(area_mask.sum())
    return float((red & area_mask).sum()) / max(total, 1.0)


def clean_mask(mask):
    """Despeckle + close internal gaps."""
    import numpy as np
    from PIL import Image, ImageFilter
    m = Image.fromarray(mask.astype(np.uint8) * 255, mode="L")
    m = m.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    m = m.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(9))
    return __import__("numpy").asarray(m) > 127


def evaluate_mask(mask, region, size):
    import numpy as np
    ys, xs = region_slices(region, size)
    inside = float(mask[ys, xs].mean())
    outside = mask.copy()
    ys_e, xs_e = region_slices(region, size, REGION_EXPAND)
    outside[ys_e, xs_e] = False
    outside_area = mask.size - (ys_e.stop - ys_e.start) * (xs_e.stop - xs_e.start)
    return inside, float(outside.sum()) / max(outside_area, 1)


def mask_bbox(mask):
    import numpy as np
    rows = np.any(mask, axis=1).nonzero()[0]
    cols = np.any(mask, axis=0).nonzero()[0]
    return [int(cols[0]), int(rows[0]), int(cols[-1]) + 1, int(rows[-1]) + 1]


def tidy_mask(obj):
    """Close small gaps, then despeckle."""
    import numpy as np
    from PIL import Image, ImageFilter
    m = Image.fromarray(obj.astype(np.uint8) * 255, mode="L")
    m = m.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    m = m.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    return np.asarray(m) > 127


def refine_mask_with_fill(mask, orig_arr, fill_arr):
    """Shrink a baggy magenta mask to the true object: inside the mask, pixels
    where the original and the inpainted fill are nearly identical are floor
    (the model magenta'd surroundings too) - drop them. Close holes, despeckle."""
    import numpy as np
    diff = np.abs(orig_arr - fill_arr).mean(axis=2)
    return tidy_mask(mask & (diff > FLOOR_SIMILAR))


def fill_holes(mask, forbid=None):
    """Fill enclosed holes: flood the complement from the image borders; any
    non-mask pixel unreachable from a border is inside the object. Border
    pixels where `forbid` is True are not used as flood seeds (so a prop that
    touches the image edge still gets its edge-touching nicks filled)."""
    import numpy as np
    from collections import deque
    H, W = mask.shape
    outside = np.zeros_like(mask)
    q = deque()

    def seed(y, x):
        if mask[y, x] or outside[y, x]:
            return
        if forbid is not None and forbid[y, x]:
            return
        outside[y, x] = True
        q.append((y, x))

    for x in range(W):
        seed(0, x)
        seed(H - 1, x)
    for y in range(H):
        seed(y, 0)
        seed(y, W - 1)
    while q:
        y, x = q.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < H and 0 <= nx < W and not mask[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                q.append((ny, nx))
    return mask | ~outside


def save_checker_preview(overlay, path):
    """Composite an RGBA overlay onto a checkerboard for visual review."""
    import numpy as np
    from PIL import Image
    rgba = np.asarray(overlay.convert("RGBA"), dtype=np.float32)
    H, W = rgba.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    checker = (((xx // 16) + (yy // 16)) % 2 * 60 + 160).astype(np.float32)
    a = rgba[..., 3:4] / 255.0
    comp = rgba[..., :3] * a + checker[..., None] * (1 - a)
    Image.fromarray(comp.astype("uint8")).save(path)


def build_overlay(original, mask):
    """Prop overlay: original pixels, feathered alpha from the mask."""
    import numpy as np
    from PIL import Image, ImageFilter
    alpha = Image.fromarray(mask.astype(np.uint8) * 255, mode="L")
    alpha = alpha.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    overlay = original.convert("RGBA")
    overlay.putalpha(alpha)
    return overlay


def fill_alpha(mask):
    """Soft compositing alpha for the bg inpaint. Dilation is kept slightly
    inside the overlay's feathered edge: any wider and re-imagined fill bleeds
    visibly past the prop onto kept content (e.g. tiles pasted over a rug)."""
    import numpy as np
    from PIL import Image, ImageFilter
    a = Image.fromarray(mask.astype(np.uint8) * 255, mode="L")
    a = a.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(2.0))
    return np.asarray(a, dtype=np.float32)[..., None] / 255.0


def suggest_wiring(prop_id, bbox):
    x0, y0, x1, y1 = bbox
    sort_y = y1 - 4
    trig_y = max(0, y0 - 30)
    return {
        "config": {"id": prop_id, "texture": prop_id, "sortY": sort_y,
                   "triggerName": f"{prop_id}_fade"},
        "trigger": {"name": f"{prop_id}_fade", "x": max(0, x0 - 10), "y": trig_y,
                    "width": (x1 - x0) + 20, "height": max(10, (sort_y - 15) - trig_y)},
        "bbox": bbox,
    }


# ── per-prop pipeline ────────────────────────────────────────────────────────

class Budget:
    def __init__(self, limit):
        self.limit, self.used = limit, 0

    def take(self):
        if self.used >= self.limit:
            sys.exit(f"ERROR: global image budget ({self.limit}) exhausted.")
        self.used += 1
        return self.used


def acquire_mask(prop, original, size, debug_dir, ctx):
    """Return the prop mask in original-image space, or None."""
    import numpy as np
    pid, region = prop["id"], prop["region"]
    orig_arr = to_array(original)
    saved = debug_dir / f"{pid}_magenta.png"

    candidates = [saved] if saved.exists() else []
    attempts = ctx.per_prop_cap - len(candidates)

    marked = annotated_input(original, region)
    for source in candidates + list(range(attempts)):
        from_file = isinstance(source, Path)
        if from_file:
            print(f"[mask] {pid}: reusing saved magenta candidate")
            cand = load_candidate(source, size)
        else:
            n = ctx.budget.take()
            print(f"[mask] {pid}: magenta attempt (image {n}/{ctx.budget.limit})...")
            try:
                raw = edit_image(png_bytes(marked),
                                 MASK_PROMPT.format(description=prop["remove"]),
                                 ctx.model, ctx.api_key)
            except Exception as e:
                print(f"       API error: {e}")
                continue
            cand = load_candidate(raw, size)
            cand.save(debug_dir / f"{pid}_magenta_attempt{n}.png")

        cand_arr = to_array(cand)
        mask = magenta_mask(cand_arr)
        inside, stray = evaluate_mask(mask, region, size)
        ok = inside >= MIN_MASK_REGION_FRAC and stray <= MAX_MASK_STRAY_FRAC
        print(f"       magenta inside={inside:.3f} (min {MIN_MASK_REGION_FRAC}), "
              f"stray={stray:.5f} (max {MAX_MASK_STRAY_FRAC}) -> "
              f"{'ACCEPT' if ok else 'reject'}")
        if not ok:
            if from_file:
                saved.unlink()
            continue
        if not from_file:
            cand.save(saved)

        dx, dy = local_offset(orig_arr, cand_arr, region, size)
        print(f"       local alignment offset: dx={dx}, dy={dy}")
        mask = clean_mask(shift_bool(mask, dx, dy))
        # Restrict to the declared region + slack so stray blips can't leak in.
        keep = np.zeros_like(mask)
        ys, xs = region_slices(region, size, REGION_EXPAND)
        keep[ys, xs] = mask[ys, xs]
        if not keep.any():
            continue
        from PIL import Image
        Image.fromarray(keep.astype(np.uint8) * 255, mode="L").save(
            debug_dir / f"{pid}_mask.png")
        return keep
    return None


def acquire_fill(prop, original, mask, size, debug_dir, ctx):
    """Return an inpaint RGB array aligned to original space, or None.
    Reuses any saved removal candidate (incl. old *_attempt*.png) first."""
    import numpy as np
    pid, region = prop["id"], prop["region"]
    orig_arr = to_array(original)
    core = clean_mask(mask)  # judge "prop gone" on the solid core

    from PIL import Image as _Image, ImageFilter as _ImageFilter
    fill_area = np.asarray(
        _Image.fromarray(core.astype(np.uint8) * 255, mode="L")
        .filter(_ImageFilter.MaxFilter(13))) > 127

    marked = annotated_input(original, region)
    saved = sorted(debug_dir.glob(f"{pid}_removal.png")) + \
        sorted(debug_dir.glob(f"{pid}_attempt*.png")) + \
        sorted(debug_dir.glob(f"{pid}_removal_attempt*.png"))
    for source in saved + list(range(ctx.per_prop_cap)):
        from_file = isinstance(source, Path)
        if from_file:
            print(f"[fill] {pid}: trying saved removal {source.name}")
            cand = load_candidate(source, size)
        else:
            n = ctx.budget.take()
            print(f"[fill] {pid}: removal attempt (image {n}/{ctx.budget.limit})...")
            try:
                raw = edit_image(png_bytes(marked),
                                 REMOVE_PROMPT.format(description=prop["remove"]),
                                 ctx.model, ctx.api_key)
            except Exception as e:
                print(f"       API error: {e}")
                continue
            cand = load_candidate(raw, size)
            cand.save(debug_dir / f"{pid}_removal_attempt{n}.png")

        cand_arr = to_array(cand)
        dx, dy = local_offset(orig_arr, cand_arr, region, size)
        aligned = shift_image(cand_arr, dx, dy)
        removed = float(np.abs(orig_arr - aligned)[core].mean(axis=-1).mean()) \
            if core.any() else 0.0
        red = red_contamination(aligned, fill_area)
        ok = removed >= MIN_REMOVED_DIFF and red <= 0.001
        print(f"       dx={dx} dy={dy}, in-mask change={removed:.1f} "
              f"(min {MIN_REMOVED_DIFF}), marker-red={red:.4f} -> "
              f"{'ACCEPT' if ok else 'reject'}")
        if ok:
            if not from_file:
                cand.save(debug_dir / f"{pid}_removal.png")
            return aligned
    return None


# ── main ─────────────────────────────────────────────────────────────────────

class Ctx:
    pass


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--manifest", required=True, help="Path to the layering manifest JSON")
    ap.add_argument("--only", help="Comma-separated prop ids to process (default: all)")
    ap.add_argument("--force", action="store_true",
                    help="Rebuild props whose overlay PNG already exists")
    ap.add_argument("--reprocess", action="store_true",
                    help="Alias for --force (saved candidates are always reused first)")
    ap.add_argument("--model", default=None, help="Gemini model id override")
    ap.add_argument("--max-images", type=int, default=None,
                    help="Global API image budget override")
    ap.add_argument("--dry-run", action="store_true", help="List planned work and exit")
    args = ap.parse_args()

    import numpy as np
    from PIL import Image

    manifest = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
    case_id, room_id = manifest["case"], manifest["room"]
    only = set(args.only.split(",")) if args.only else None

    room_dir = REPO_ROOT / "Content" / "Levels" / case_id / room_id
    bg_path = room_dir / manifest.get("background", "bg_base.jpg")
    backup_path = room_dir / f"full scene{bg_path.suffix}"
    debug_dir = DEBUG_ROOT / room_id
    debug_dir.mkdir(parents=True, exist_ok=True)

    source_path = backup_path if backup_path.exists() else bg_path
    if not source_path.exists():
        sys.exit(f"ERROR: background not found: {source_path}")
    original = Image.open(source_path).convert("RGB")
    W, H = original.size
    size = (W, H)
    print(f"[src ] {source_path.relative_to(REPO_ROOT)} ({W}x{H})")

    props = [p for p in manifest["props"] if not only or p["id"] in only]
    if only:
        unknown = only - {p["id"] for p in manifest["props"]}
        if unknown:
            sys.exit(f"ERROR: ids not in manifest: {', '.join(sorted(unknown))}")
    if args.dry_run:
        for p in props:
            print(f"[dry-run] {p['id']:24s} region={p['region']}")
        return

    ctx = Ctx()
    ctx.model = args.model or manifest.get("model", DEFAULT_MODEL)
    ctx.budget = Budget(args.max_images or int(manifest.get("max_total_images", 50)))
    ctx.per_prop_cap = int(manifest.get("max_attempts_per_prop", 6))
    ctx.api_key = load_api_key()

    report = {"case": case_id, "room": room_id, "size": [W, H], "props": []}
    fills = []   # (mask, aligned_fill_arr)
    done = []

    for prop in props:
        pid = prop["id"]
        out_path = room_dir / f"{pid}.png"
        if out_path.exists() and not (args.force or args.reprocess):
            print(f"[skip] {pid}: overlay exists (use --force)")
            mask_file = debug_dir / f"{pid}_mask.png"
            removal_file = debug_dir / f"{pid}_removal.png"
            if mask_file.exists() and removal_file.exists():
                mask = np.asarray(Image.open(mask_file).convert("L")) > 127
                cand_arr = to_array(load_candidate(removal_file, size))
                dx, dy = local_offset(to_array(original), cand_arr,
                                      prop["region"], size)
                fills.append((mask, shift_image(cand_arr, dx, dy)))
                report["props"].append(suggest_wiring(pid, mask_bbox(mask)))
            continue

        if prop.get("mask") == "region":
            # Large structural props: the model refuses magenta recoloring, so
            # seed with the declared region and rely on refine_mask_with_fill
            # to carve out what the removal actually replaced.
            mask = np.zeros((H, W), dtype=bool)
            ys, xs = region_slices(prop["region"], size)
            mask[ys, xs] = True
            print(f"[mask] {pid}: region-seeded mask (carved by the inpaint diff)")
        else:
            mask = acquire_mask(prop, original, size, debug_dir, ctx)
            if mask is None:
                print(f"[FAIL] {pid}: no usable magenta mask")
                continue
        fill = acquire_fill(prop, original, mask, size, debug_dir, ctx)
        if fill is None:
            print(f"[FAIL] {pid}: no usable removal/inpaint")
            continue

        orig_arr = to_array(original)
        refined = refine_mask_with_fill(mask, orig_arr, fill)
        if refined.any():
            kept = float(refined.sum()) / max(float(mask.sum()), 1.0)
            print(f"       mask refined vs fill: kept {kept:.0%} of seed area")
        elif prop.get("mask") == "region":
            print(f"[FAIL] {pid}: inpaint diff carved nothing out of the region")
            continue
        else:
            print(f"       WARNING: refinement emptied the mask; keeping raw magenta")
            refined = mask

        # must_cover zones the magenta pass tends to miss (e.g. plants on a
        # ledge) are unioned in via the inpaint diff instead.
        must = prop.get("must_cover", [])
        if must:
            diff = np.abs(orig_arr - fill).mean(axis=2)
            added = np.zeros_like(refined)
            for rect in must:
                ys_c, xs_c = region_slices(rect, size, 6)
                added[ys_c, xs_c] = diff[ys_c, xs_c] > FLOOR_SIMILAR
            added &= ~refined
            if added.any():
                print(f"       must_cover augmentation added {int(added.sum())} px")
                refined = tidy_mask(refined | added)
        forbid = np.zeros_like(refined)
        ys_f, xs_f = region_slices(prop["region"], size, REGION_EXPAND)
        forbid[ys_f, xs_f] = True
        mask = fill_holes(refined, forbid)
        Image.fromarray(mask.astype(np.uint8) * 255, mode="L").save(
            debug_dir / f"{pid}_mask.png")

        overlay = build_overlay(original, mask)
        overlay.save(out_path, optimize=True)
        save_checker_preview(overlay, debug_dir / f"preview_{pid}.png")
        fills.append((mask, fill))
        wiring = suggest_wiring(pid, mask_bbox(mask))
        report["props"].append(wiring)
        done.append(pid)
        print(f"[done] {pid}: overlay bbox {wiring['bbox']} -> "
              f"{out_path.relative_to(REPO_ROOT)} "
              f"(suggested sortY {wiring['config']['sortY']})")

    if fills:
        bg_arr = to_array(original)
        for mask, fill in fills:
            a = fill_alpha(mask)
            bg_arr = bg_arr * (1 - a) + fill * a
        bg = Image.fromarray(np.clip(bg_arr, 0, 255).astype("uint8"), mode="RGB")

        if not backup_path.exists():
            shutil.copy2(bg_path, backup_path)
            print(f"[bak ] original scene saved as {backup_path.name}")
        if bg_path.suffix.lower() in (".jpg", ".jpeg"):
            bg.save(bg_path, quality=92, subsampling=0)
        else:
            bg.save(bg_path, optimize=True)
        print(f"[bg  ] props removed -> {bg_path.relative_to(REPO_ROOT)}")

        # Reconstruction check: new bg + overlays must rebuild the original scene.
        recon = bg.convert("RGBA")
        for p in manifest["props"]:
            op = room_dir / f"{p['id']}.png"
            if op.exists():
                recon = Image.alpha_composite(recon, Image.open(op).convert("RGBA"))
        recon.convert("RGB").save(debug_dir / "reconstruction.png")
        err = float(np.abs(to_array(recon) - to_array(original)).mean())
        report["reconstruction_mean_error"] = round(err, 2)
        print(f"[chk ] reconstruction mean error vs original: {err:.2f} "
              f"(see {(debug_dir / 'reconstruction.png').relative_to(REPO_ROOT)})")

    (debug_dir / "report.json").write_text(json.dumps(report, indent=2),
                                           encoding="utf-8")
    print(f"\n[rep ] wiring suggestions -> "
          f"{(debug_dir / 'report.json').relative_to(REPO_ROOT)}")
    if done:
        print("\nNext steps:")
        print(f"  1. Review overlays + new bg in debug_output/layering/{room_id}/")
        print(f"  2. Paste props[] into {room_id}/room_config.json and the fade "
              f"triggers into room_map.json (Triggers layer) - tune sortY/rects.")
        print("  3. Add prop_*.png to Content.mgcb (PremultiplyAlpha=False), "
              "then: dotnet build")
        print(f"  4. Verify: dotnet run --no-build -- --screenshot {case_id} {room_id}")


if __name__ == "__main__":
    main()
