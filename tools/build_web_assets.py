"""
Web asset step, run by the CatDetectiveWeb publish (ItchIoFix target) on the
patched publish/wwwroot. Stdlib + Pillow (with WebP) only.

1. TEXTURES. Every TextureImporter entry in Content/Content.Web.mgcb is
   re-encoded from its source image to a small browser-native file next to
   its .xnb:  Content/<asset>.jpg  (fully opaque)  or  .webp  (has alpha).
   The page's texture worker (wwwroot/js/gametex.js) downloads these in the
   background and decodes them with createImageBitmap OFF the main thread;
   the game builds its Texture2D from the finished RGBA (Systems/WebTextures.cs).
   Mirrors the pipeline: ColorKey (exact match -> transparent black),
   straight alpha (every entry is PremultiplyAlpha=False), no resizing - the
   game sizes sprites from texture dimensions, so pixels must match the xnb.
   The .xnb files stay as a fallback (manifest missing / decode failure).
   Writes Content/textures.json: [{"a": asset, "f": file, "w", "h", "s": bytes}]
   in load-priority order (Shared first, then .mgcb order).

2. FRAMEWORK. gzips every framework/*.wasm|*.dat|*.dll to <file>.gz. The boot
   loader in index.html fetches the .gz and inflates it with the browser's
   native DecompressionStream (itch.io serves files without compression).

3. js/bootinfo.js: window.BOOT_TOTAL_BYTES = bytes the boot loader downloads,
   so the HTML progress bar is a real percentage instead of racing to 100%.

usage: python tools/build_web_assets.py <publish/wwwroot> [Content.Web.mgcb]
"""
import gzip
import hashlib
import json
import os
import shutil
import sys
import time

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# Encoded textures are cached here (outside publish/, which every publish
# wipes), keyed on source bytes + processor params + encoder version.
CACHE = os.path.join(REPO, "CatDetectiveWeb", "obj", "web_assets_cache")
ENCODER_VERSION = "1"


def parse_mgcb(path):
    """Yields dicts: {src, asset, params} for each TextureImporter entry."""
    entries, cur = [], None
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.strip()
            if line.startswith("#begin "):
                cur = {"importer": "", "params": {}}
            elif cur is None:
                continue
            elif line.startswith("/importer:"):
                cur["importer"] = line[len("/importer:"):]
            elif line.startswith("/processorParam:"):
                k, _, v = line[len("/processorParam:"):].partition("=")
                cur["params"][k] = v
            elif line.startswith("/build:"):
                spec = line[len("/build:"):]
                src, _, out = spec.partition(";")
                out = out or src
                if cur["importer"] == "TextureImporter":
                    entries.append({
                        "src": src,
                        "asset": os.path.splitext(out)[0].replace("\\", "/"),
                        "params": cur["params"],
                    })
                cur = None
    return entries


def encode_texture(src_path, params, out_base):
    """Encodes (or copies from cache) one texture; returns (path, (w, h))."""
    with open(src_path, "rb") as f:
        digest = hashlib.sha1(f.read() + json.dumps(params, sort_keys=True).encode()
                              + ENCODER_VERSION.encode()).hexdigest()
    for ext in (".jpg", ".webp"):
        cached = os.path.join(CACHE, digest + ext)
        if os.path.exists(cached):
            with Image.open(cached) as im:
                size = im.size
            shutil.copyfile(cached, out_base + ext)
            return out_base + ext, size

    import numpy as np
    with Image.open(src_path) as im:
        arr = np.array(im.convert("RGBA"))
    if params.get("ColorKeyEnabled", "False") == "True":
        key = [int(c) for c in params.get("ColorKeyColor", "255,0,255,255").split(",")][:3]
        hit = np.all(arr[:, :, :3] == key, axis=2)
        arr[hit] = 0                                   # transparent black, like the pipeline
    im = Image.fromarray(arr, "RGBA")
    if int(arr[:, :, 3].min()) == 255:
        ext = ".jpg"
        im.convert("RGB").save(out_base + ext, "JPEG", quality=88, optimize=True)
    else:
        ext = ".webp"
        im.save(out_base + ext, "WEBP", quality=90, alpha_quality=100, method=4)
    os.makedirs(CACHE, exist_ok=True)
    shutil.copyfile(out_base + ext, os.path.join(CACHE, digest + ext))
    return out_base + ext, im.size


def build_textures(wwwroot, mgcb):
    content_src = os.path.dirname(mgcb)
    content_out = os.path.join(wwwroot, "Content")
    entries = parse_mgcb(mgcb)
    # Shared first (boot + menu), then the .mgcb order (cases/rooms).
    entries.sort(key=lambda e: 0 if e["asset"].startswith("Shared/") else 1)
    manifest, total_in, total_out = [], 0, 0
    for e in entries:
        src = os.path.join(content_src, e["src"])
        if not os.path.exists(src):
            print(f"  WARN missing source {e['src']} - asset {e['asset']} stays xnb-only")
            continue
        out_base = os.path.join(content_out, *e["asset"].split("/"))
        os.makedirs(os.path.dirname(out_base), exist_ok=True)
        path, (w, h) = encode_texture(src, e["params"], out_base)
        size = os.path.getsize(path)
        xnb = out_base + ".xnb"
        total_in += os.path.getsize(xnb) if os.path.exists(xnb) else 0
        total_out += size
        manifest.append({"a": e["asset"], "f": os.path.relpath(path, content_out).replace("\\", "/"),
                         "w": w, "h": h, "s": size})
    with open(os.path.join(content_out, "textures.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, separators=(",", ":"))
    print(f"  textures: {len(manifest)} files, {total_in / 1e6:.1f} MB xnb -> {total_out / 1e6:.1f} MB jpg/webp")


def gzip_framework(wwwroot):
    fw = os.path.join(wwwroot, "framework")
    raw_total = gz_total = boot_total = 0
    for name in sorted(os.listdir(fw)):
        p = os.path.join(fw, name)
        if not os.path.isfile(p) or name.endswith((".gz", ".br")):
            continue
        size = os.path.getsize(p)
        if name.endswith((".wasm", ".dat", ".dll")):
            with open(p, "rb") as fi:
                data = fi.read()
            with open(p + ".gz", "wb") as fo:
                fo.write(gzip.compress(data, compresslevel=9, mtime=0))
            gz = os.path.getsize(p + ".gz")
            raw_total += size
            gz_total += gz
            # icudt*.dat: only one variant is ever fetched (none with invariant
            # globalization); keep them out of the progress total.
            if not name.startswith("icudt"):
                boot_total += gz
        elif name.endswith((".js", ".json")):
            boot_total += size
    with open(os.path.join(wwwroot, "js", "bootinfo.js"), "w", encoding="utf-8") as f:
        f.write(f"window.BOOT_TOTAL_BYTES = {boot_total};\n")
    print(f"  framework: {raw_total / 1e6:.1f} MB -> {gz_total / 1e6:.1f} MB gzipped "
          f"(boot download ~{boot_total / 1e6:.1f} MB)")


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    wwwroot = os.path.abspath(sys.argv[1])
    mgcb = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(REPO, "Content", "Content.Web.mgcb")
    t = time.time()
    print(f"[build_web_assets] {wwwroot}")
    build_textures(wwwroot, mgcb)
    gzip_framework(wwwroot)
    print(f"[build_web_assets] done in {time.time() - t:.1f} s")


if __name__ == "__main__":
    main()
