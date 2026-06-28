# Cat Detective — CLAUDE.md

## Project overview

A 2.5D isometric detective adventure game where a cat solves human crimes.
Cozy Studio Ghibli aesthetic, pre-rendered backgrounds, point-and-click/free-roam gameplay.
Built in C# using **MonoGame (KNI fork)**, targeting **Web/WASM** as the final platform.

Current state: prototype — movement, Y-sorting, prop fading, and the render pipeline are implemented.
Real art assets are not yet present; placeholder PNGs in `Content/` are needed to build.

---

## Tech stack

| Layer | Choice |
|-------|--------|
| Language | C# (.NET 8) |
| Framework | MonoGame.Framework.DesktopGL (dev) → KNI WASM (ship) |
| Map editor | Tiled — JSON export only |
| JSON parsing | System.Text.Json (built-in, no extra packages) |
| Content pipeline | MonoGame Content Builder (`.mgcb`) |

---

## File structure

```
CatDetective.csproj       — Project file; swap package for KNI to target WASM
Program.cs                — Entry point ([STAThread])
Game1.cs                  — Main loop; owns the 4-pass render pipeline

Entities/
  GameObject.cs           — Abstract base: Position (bottom-center), LayerDepth, CollisionBox
  Prop.cs                 — Foreground furniture; fades when cat walks behind it
  Cat.cs                  — Player character; sprite-sheet animator, AABB movement

Map/
  MapParser.cs            — Reads Tiled JSON; extracts "Collisions" and "Triggers" layers

Content/
  Content.mgcb            — Asset pipeline config (ALL sprites: PremultiplyAlpha=False)
  room_map.json           — Tiled map; copied verbatim to output, not pipeline-processed

  bg_base.png             — Full-room pre-rendered background (no desk/cabinet)
  prop_desk.png           — Desk cutout sprite
  prop_cabinet.png        — Cabinet cutout sprite
  mask_sunbeams.png       — Sunbeam VFX: black BG + warm-white beams (used additively)
  shadow_blob.png         — Blurred black oval for the blob shadow under the cat
  spr_cat_walk_down.png   — Horizontal sprite sheet, 4 frames, walk toward camera
  spr_cat_walk_up.png     — Horizontal sprite sheet, 4 frames, walk away from camera
```

---

## Architecture rules

### Pivot / origin — NEVER change this
All entities use **bottom-center** as the draw origin. `Position` = the floor contact point.
Changing this to top-left breaks Y-sorting for every entity in the scene.
`TextureOrigin` in `GameObject` computes `(width/2, height)` automatically.

### Layer depth formula — keep it normalised
```csharp
layerDepth = Math.Clamp(Position.Y / screenHeight, 0f, 1f);
```
`SpriteSortMode.FrontToBack` → higher depth = drawn in front. Do not invert this.

### Render pipeline — keep passes in order
| Pass | Blend | Content |
|------|-------|---------|
| 1 | AlphaBlend | `bg_base` at (0,0) |
| 2 | AlphaBlend | Blob shadow (before cat, so it sits under) |
| 3 | **NonPremultiplied**, FrontToBack | Cat + all props |
| 4 | **Additive** | `mask_sunbeams` at (0,0) |

Inserting a new entity: decide which pass it belongs to and draw it there.
New lighting overlays → Pass 4 (additive). New floor decals → Pass 1 or 2. New Y-sortable objects → Pass 3.

### Content pipeline — PremultiplyAlpha=False
Every sprite used in Pass 3 (NonPremultiplied) must be built with `PremultiplyAlpha=False` in `Content.mgcb`.
If a new sprite is added, add its entry to the `.mgcb` with this flag.
The background and sunbeam mask can use either setting; keep them False for consistency.

### Cat update — two-phase
`Cat.Update()` reads input and advances animation. It does NOT move the cat.
`Cat.MoveWithCollision()` applies velocity with per-axis AABB resolution.
Call them in that order from `Game1.Update()`. Do not merge them.

---

## Map / Tiled conventions

- Only **object layers** are parsed. Tile layers are ignored.
- Layer named `Collisions` → feeds `_solidBoundaries` (blocks cat movement).
- Layer named `Triggers` → fade zones; matched to props by name substring (`"desk"`, `"cabinet"`).
- Layer named `Transfers` → doorways; each object needs two custom properties: `TargetRoom` (folder name) and `TargetSpawn` (spawn point name in the target room).
- Layer named `Spawn` → named point objects. `spawn_default` is used on first entry; return spawns are named `spawn_from_<roomId>`.
- The JSON file is read at runtime with `File.ReadAllText`. Edit it without rebuilding `.mgcb`.
- Object rectangles only. Tiled polygons and ellipses are not supported yet.
- A layer-level `offsetx`/`offsety` on any layer shifts all its objects at parse time. The entrance Transfers layer uses offset `(-142, +192)` to align with the background art.

### Spawn point placement rule — critical
The cat collision box is **87 px wide (±43 px from center) and 45 px tall** (from `Position.Y - 45` up to `Position.Y`). A spawn point that lands inside a transfer zone causes an instant re-trigger loop. When placing a `spawn_from_X` point, ensure the entire collision box clears the transfer zone:

- **Y clearance**: `spawnY > zoneBottom + 45` (spawn below a zone) or `spawnY - 45 > zoneTop` adjusted accordingly
- **X clearance**: `spawnX - 43 > zoneRight` or `spawnX + 43 < zoneLeft` (spawn beside a zone)

A safe margin of ~15 px beyond the minimum is recommended.

### Malibu Mansion — room wiring

```
entrance ──► living_room   (transfer right-side; spawn_from_entrance)
         └─► bedroom       (transfer right-side; spawn_from_entrance)

living_room ──► entrance   (transfer bottom-left; spawn_from_living_room)
            ├─► library    (transfer left-side;   spawn_from_living_room)
            ├─► kitchen    (transfer right-side;  spawn_from_living_room)
            └─► garden     (transfer top-center;  spawn_from_living_room)

bedroom ──► entrance       (transfer bottom-center; spawn_from_bedroom)

library ──► living_room    (transfer right-side; spawn_from_library)

kitchen ──► living_room    (transfer right-side; spawn_from_kitchen)

garden ──► living_room     (transfer right-side; spawn_from_garden)
       └─► pool_area       (transfer bottom-left; spawn_from_garden)

pool_area ──► garden       (transfer bottom-center; spawn_from_pool_area)
```

All connections are bidirectional. Background dimensions per room:
- `entrance`, `living_room`, `kitchen`: 1370 × 768
- `bedroom`, `garden`, `library`, `pool_area`: 1456 × 816

The canvas resizes automatically when switching between room sizes.

---

## Build & run

```bash
dotnet restore
dotnet run
```

Controls: WASD or arrow keys. Escape to quit.

To rebuild content assets after changing `.mgcb`:
```bash
dotnet build   # MonoGame.Content.Builder.Task runs mgcb automatically
```

---

## Switching to KNI / WASM

In `CatDetective.csproj`, replace:
```xml
<PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.1.303" />
<PackageReference Include="MonoGame.Content.Builder.Task"  Version="3.8.1.303" />
```
with the appropriate KNI platform packages. No C# source changes are needed.

---

## What NOT to do

- Do not use `SpriteSortMode.BackToFront` — the entire depth system assumes `FrontToBack`.
- Do not set `PremultiplyAlpha=True` for Pass 3 sprites — it will cause darkening when props fade.
- Do not parse Tiled tilesets — the GDD uses pre-rendered PNGs, not tile-based rendering.
- Do not add collision logic inside `Cat.Update()` — keep movement and collision in `MoveWithCollision()`.
- Do not commit art assets to git as large binaries — use Git LFS or keep them out of the repo.
