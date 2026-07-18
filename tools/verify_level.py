"""Static verification of the malibu_mansion level.

Checks:
1. Interactables: every trigger rect is reachable — there exists a cat position
   whose feet box intersects the trigger while overlapping no Collisions rect.
2. Transfers: TargetRoom folder + room_map exist, TargetSpawn exists in target,
   and the spawn's feet box clears every transfer zone AND collision in target.
3. Configs: local sentence [slot] count == localDeductionClueIds length; every
   local answer id is discoverable via UNGATED keywords in that room (intro or
   ungated topic); EVERY clue is discoverable ungated in its OWN room (gated
   topics never carry unique clues - gates gate story, not completion, so the
   room clue counter always completes without backtracking); final sentence
   slots == finalSolveClueIds; every requiresClue id exists and is discoverable;
   every requiresSolve id is a known room (solve-gated confrontation topics);
   every map Interactable name has a room_config entry and vice versa; all
   content strings are pure ASCII (sprite font limit).
4. Solve balance (WARN): a local board slot whose category has no decoy in the
   room's own clue pool is a single-option auto-fill, not a deduction.
"""
import json, os, re, sys

# Case id comes from argv (default: malibu_mansion); rooms come from the case
# config itself, so any case under Content/Levels can be checked:
#   python tools/verify_level.py [case_id]
CASE = sys.argv[1] if len(sys.argv) > 1 else "malibu_mansion"
LEVELS_ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                           "Content", "Levels")
BASE = os.path.join(LEVELS_ROOT, CASE)
if not os.path.isfile(os.path.join(BASE, "case_config.json")):
    print(f"ERROR: no case_config.json under {BASE}")
    sys.exit(1)
_case_early = json.load(open(os.path.join(BASE, "case_config.json"), encoding="utf-8"))
ROOMS = _case_early["rooms"]

# Cat feet box: from Cat.cs, width = frameWidth*scale*0.5, height = 32.
# Actual widths are ~49 (down) / ~56 (up) at scale 0.28/0.32; keep checking
# with 70 as a deliberate safety margin against future scale bumps.
FEET_W, FEET_H = 70, 32

errors, warnings = [], []

def feet_box(px, py):
    return (px - FEET_W / 2, py - FEET_H, FEET_W, FEET_H)

def intersects(a, b):
    ax, ay, aw, ah = a; bx, by, bw, bh = b
    return ax < bx + bw and bx < ax + aw and ay < by + bh and by < ay + ah

def load(room):
    m = json.load(open(os.path.join(BASE, room, "room_map.json"), encoding="utf-8"))
    layers = {}
    for l in m["layers"]:
        if l.get("type") == "objectgroup":
            ox, oy = l.get("offsetx", 0), l.get("offsety", 0)
            objs = []
            for o in l["objects"]:
                objs.append({**o, "x": o["x"] + ox, "y": o["y"] + oy})
            layers[l["name"]] = objs
    cfg = json.load(open(os.path.join(BASE, room, "room_config.json"), encoding="utf-8"))
    return m, layers, cfg

maps = {r: load(r) for r in ROOMS}
case = _case_early

# ---- 1. interactable reachability ----
for room in ROOMS:
    _, layers, _ = maps[room]
    cols = [(c["x"], c["y"], c["width"], c["height"]) for c in layers.get("Collisions", [])]
    for o in layers.get("Interactables", []):
        trig = (o["x"], o["y"], o["width"], o["height"])
        ok = False
        # sample cat positions on a grid around the trigger
        y = o["y"] - 4
        while y <= o["y"] + o["height"] + FEET_H + 4 and not ok:
            x = o["x"] - FEET_W
            while x <= o["x"] + o["width"] + FEET_W and not ok:
                fb = feet_box(x, y)
                if intersects(fb, trig) and not any(intersects(fb, c) for c in cols):
                    ok = True
                x += 8
            y += 8
        if not ok:
            errors.append(f"{room}: interactable '{o['name']}' UNREACHABLE (trigger fully blocked by collisions)")

# ---- 2. transfers ----
transfer_count = 0
for room in ROOMS:
    _, layers, _ = maps[room]
    for t in layers.get("Transfers", []):
        transfer_count += 1
        props = {p["name"]: p["value"] for p in t.get("properties", [])}
        tgt, spawn = props.get("TargetRoom"), props.get("TargetSpawn")
        if tgt not in maps:
            errors.append(f"{room}: transfer '{t['name']}' → unknown room '{tgt}'"); continue
        _, tlayers, _ = maps[tgt]
        sp = next((s for s in tlayers.get("Spawn", []) if s["name"] == spawn), None)
        if sp is None:
            errors.append(f"{room}: transfer '{t['name']}' → '{tgt}' missing spawn '{spawn}'"); continue
        fb = feet_box(sp["x"], sp["y"])
        for z in tlayers.get("Transfers", []):
            if intersects(fb, (z["x"], z["y"], z["width"], z["height"])):
                errors.append(f"{tgt}: spawn '{spawn}' ({sp['x']},{sp['y']}) re-triggers zone '{z['name']}'")
        for c in tlayers.get("Collisions", []):
            if intersects(fb, (c["x"], c["y"], c["width"], c["height"])):
                errors.append(f"{tgt}: spawn '{spawn}' ({sp['x']},{sp['y']}) inside collision '{c['name']}'")
print(f"transfers checked: {transfer_count}")

# ---- 3. configs ----
db = {c["id"]: c for c in case["clues"]}

def check_ascii(where, obj):
    """All content JSON must be ASCII 32-126 (sprite font coverage)."""
    if isinstance(obj, str):
        bad = [ch for ch in obj if ord(ch) > 126 or (ord(ch) < 32 and ch != "\n")]
        if bad:
            errors.append(f"{where}: non-ASCII character(s) {bad!r} in {obj[:40]!r}...")
    elif isinstance(obj, dict):
        for v in obj.values(): check_ascii(where, v)
    elif isinstance(obj, list):
        for v in obj: check_ascii(where, v)

check_ascii("case_config", case)

discoverable = set()          # ungated keywords, all rooms
ungated_by_room = {}          # room -> set of ungated-unlockable clue ids
gated = []                    # ([requirements], {kw ids}) pairs, all rooms
                              # a requirement is a clue id or "solve:<room>"
for room in ROOMS:
    _, layers, cfg = maps[room]
    check_ascii(f"{room}/room_config", cfg)
    cfg_ids = {i["id"] for i in cfg["interactables"]}
    map_names = {o["name"] for o in layers.get("Interactables", [])}
    for n in map_names - cfg_ids:
        errors.append(f"{room}: map object '{n}' has no room_config entry")
    for n in cfg_ids - map_names:
        errors.append(f"{room}: config interactable '{n}' not placed in map")

    room_kw_ungated = set()
    topic_count = 0
    for i in cfg["interactables"]:
        for k in i.get("keywords", []):
            if k["id"] not in db:
                errors.append(f"{room}: keyword id '{k['id']}' ({i['id']}) not in clueDatabase")
            room_kw_ungated.add(k["id"])
        for ti, t in enumerate(i.get("topics", [])):
            topic_count += 1
            if not t.get("prompt"):
                errors.append(f"{room}: {i['id']} topic {ti} has no prompt")
            kw_ids = set()
            for k in t.get("keywords", []):
                if k["id"] not in db:
                    errors.append(f"{room}: keyword id '{k['id']}' ({i['id']} topic {ti}) not in clueDatabase")
                kw_ids.add(k["id"])
            reqs = []
            req_clue = t.get("requiresClue", "")
            if req_clue:
                if req_clue not in db:
                    errors.append(f"{room}: {i['id']} topic {ti} requiresClue '{req_clue}' not in clueDatabase")
                reqs.append(req_clue)
            req_solve = t.get("requiresSolve", "")
            if req_solve:
                if req_solve not in ROOMS:
                    errors.append(f"{room}: {i['id']} topic {ti} requiresSolve '{req_solve}' is not a room")
                reqs.append(f"solve:{req_solve}")
            if reqs:
                gated.append((reqs, kw_ids))
            else:
                room_kw_ungated |= kw_ids
    ungated_by_room[room] = room_kw_ungated
    discoverable |= room_kw_ungated

    slots = re.findall(r"\[([^\]]+)\]", cfg.get("localDeductionSentence", ""))
    answers = cfg.get("localDeductionClueIds", [])
    if len(slots) != len(answers):
        errors.append(f"{room}: {len(slots)} slots vs {len(answers)} localDeductionClueIds")
    answers_ok = True
    for a in answers:
        if a not in room_kw_ungated:
            errors.append(f"{room}: local answer '{a}' not discoverable via ungated keywords in this room")
            answers_ok = False
    # A room whose answers are all reachable is solvable, so its solve-gated
    # confrontation topics are reachable too.
    if answers_ok and answers:
        discoverable.add(f"solve:{room}")

    # Solve balance: a slot category with no spare clue in the room pool is an
    # auto-fill. Count slots per category vs clues of that category in the room.
    slot_cats = {}
    for a in answers:
        if a in db:
            slot_cats[db[a]["category"]] = slot_cats.get(db[a]["category"], 0) + 1
    for cat, n_slots in slot_cats.items():
        options = sum(1 for c in db.values()
                      if c.get("roomId") == room and c.get("category") == cat)
        if options <= n_slots:
            warnings.append(f"{room}: {cat} slot(s) x{n_slots} but only {options} {cat} "
                            f"clue(s) in the room - single-option auto-fill, add a decoy")

    print(f"{room}: {len(map_names)} interactables, {topic_count} topics, "
          f"{len(room_kw_ungated)} ungated clues, {len(slots)} local slots")

# Gated topic keywords become discoverable once ALL their gates are; iterate to fixpoint.
changed = True
while changed:
    changed = False
    for reqs, kw_ids in gated:
        if all(r in discoverable for r in reqs) and not kw_ids <= discoverable:
            discoverable |= kw_ids
            changed = True
for reqs, _ in gated:
    for req in reqs:
        if req in db and req not in discoverable:
            errors.append(f"gated topic requires '{req}' but that clue is never discoverable")

for cid in db:
    if cid not in discoverable:
        errors.append(f"clue '{cid}' exists in clueDatabase but no keyword unlocks it anywhere")

# Gated topics must never be a clue's only source: every clue needs an ungated
# unlock in its OWN room, or the room clue counter can't complete without
# backtracking behind cross-room gates.
for cid, c in db.items():
    home = c.get("roomId", "")
    if home in ungated_by_room and cid not in ungated_by_room[home]:
        errors.append(f"clue '{cid}' is not ungated-discoverable in its own room "
                      f"'{home}' (gated topics must never carry unique clues)")

fslots = re.findall(r"\[([^\]]+)\]", case["finalSolveSentence"])
fans = case.get("finalSolveClueIds", [])
if len(fslots) != len(fans):
    errors.append(f"final sentence: {len(fslots)} slots vs {len(fans)} finalSolveClueIds")
for a in fans:
    if a not in db:
        errors.append(f"finalSolveClueIds: '{a}' not in clueDatabase")
    elif not db[a].get("isMacroClue"):
        errors.append(f"finalSolveClueIds: '{a}' is not a macro clue (won't be in final word bank)")
if set(case.get("rooms", [])) != set(ROOMS):
    errors.append(f"case rooms list mismatch: {case.get('rooms')}")
print(f"case: {len(db)} clues total, final slots {len(fslots)}/{len(fans)}")

print()
for w in warnings: print("WARN:", w)
for e in errors: print("ERROR:", e)
print("RESULT:", "FAIL" if errors else "ALL CHECKS PASSED")
sys.exit(1 if errors else 0)
