#!/usr/bin/env python3
"""
Cat Detective - headless story simulator.

Plays a case as text, straight from the runtime JSON (case_config.json,
room_config.json, room_map.json, case_script.json), mirroring the game's rules:

  - rooms + doors from the Tiled Transfers layer (RequiresVisited / LockedText)
  - interactables present in BOTH the map's Interactables layer and room_config
  - intro keywords unlock on inspect; topic keywords unlock when the topic is chosen
  - topic gates: requiresClue (clue found) AND requiresSolve (room board solved)
  - altText shown once its gates are satisfied
  - local boards: bank = found clues of this room + found case-global clues;
    a slot takes only a chip of its category; wrong submit = "N/M fit"
  - solving a room unlocks its macro clues
  - FINAL SOLVE lock: every room solved AND every requiresSolve topic heard
  - the scripted gathering (case_script.json): sequential small boards with a
    curated bank; every gathering chip is handed over when it starts

Two modes:

  Interactive (one command per invocation; state lives in a JSON file):
      python tools/story_sim/sim.py --state run.json new [case_id]
      python tools/story_sim/sim.py --state run.json look
      python tools/story_sim/sim.py --state run.json inspect <thing>
      python tools/story_sim/sim.py --state run.json topic <thing> <n>
      python tools/story_sim/sim.py --state run.json go <room>
      python tools/story_sim/sim.py --state run.json notebook | clue <chip> | board | notes
      python tools/story_sim/sim.py --state run.json solve <chip> <chip> ...
      python tools/story_sim/sim.py --state run.json final
      python tools/story_sim/sim.py --state run.json gather <chip> <chip> ...
      python tools/story_sim/sim.py --state run.json help
    <thing>/<chip>/<room> accept ids or (partial, case-insensitive) display names.

  Audit (deterministic, no LLM):  python tools/story_sim/sim.py audit [case_id]
    Exhausts every interaction, proves every board + the gathering is solvable
    in the game's own order, and reports structural warnings. Exit code 1 on
    any ERROR. Add --dump <file.md> to write the whole script in play order.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
LEVELS = REPO / "Content" / "Levels"
TIME_RE = re.compile(r"(\d{1,2}):(\d{2})\s*([AP]M)", re.I)
CATS = ["Who", "What", "Why", "WhereWhen"]
CAT_LABEL = {"Who": "WHO", "What": "HOW/WHAT", "Why": "WHY", "WhereWhen": "WHERE/WHEN"}


# ════════════════════════════════════════════════════════════════════════════
# Case data
# ════════════════════════════════════════════════════════════════════════════
class Case:
    def __init__(self, case_id: str):
        self.id = case_id
        root = LEVELS / case_id
        cc = json.loads((root / "case_config.json").read_text(encoding="utf-8"))
        self.clues = {c["id"]: c for c in cc["clues"]}
        self.rooms: list[str] = cc["rooms"]
        self.final_sentence = cc.get("finalSolveSentence", "")
        self.final_answers = cc.get("finalSolveClueIds", [])
        sp = root / "case_script.json"
        self.script = json.loads(sp.read_text(encoding="utf-8")) if sp.exists() else {}
        self.room_cfg, self.doors, self.things = {}, {}, {}
        for r in self.rooms:
            rc = json.loads((root / r / "room_config.json").read_text(encoding="utf-8"))
            self.room_cfg[r] = rc
            m = json.loads((root / r / "room_map.json").read_text(encoding="utf-8"))
            placed, doors = [], []
            for layer in m.get("layers", []):
                if layer.get("type") != "objectgroup":
                    continue
                if layer["name"] == "Interactables":
                    placed += [o.get("name", "") for o in layer["objects"]]
                elif layer["name"] == "Transfers":
                    for o in layer["objects"]:
                        p = {q["name"]: q["value"] for q in o.get("properties", [])}
                        if "TargetRoom" in p:
                            doors.append(p)
            self.doors[r] = doors
            by_id = {i["id"]: i for i in rc.get("interactables", [])}
            self.things[r] = [by_id[n] for n in placed if n in by_id]

    def clue_name(self, cid: str) -> str:
        return self.clues.get(cid, {}).get("name", cid)

    def cat(self, cid: str) -> str:
        return self.clues.get(cid, {}).get("category", "Who")

    def room_name(self, r: str) -> str:
        return r.replace("_", " ").title()

    def thing_name(self, t: dict, st: "State | None" = None) -> str:
        if st and t.get("revealName") and t.get("revealNameOnClue") in st.found:
            return t["revealName"]
        if t.get("name"):
            return t["name"]
        return t["id"].replace("inspect_", "").replace("_", " ").title()

    def solve_topics(self):
        """Every requiresSolve topic: the FINAL SOLVE confrontation lock."""
        for r in self.rooms:
            for t in self.things[r]:
                for i, tp in enumerate(t.get("topics", [])):
                    if tp.get("requiresSolve"):
                        yield r, t, i

    def sentence_slots(self, sentence: str, answers: list[str]):
        parts = re.split(r"(\[[^\]]*\])", sentence)
        slots, out = [], []
        for p in parts:
            if p.startswith("[") and p.endswith("]"):
                a = answers[len(slots)] if len(slots) < len(answers) else ""
                slots.append({"tag": p[1:-1], "answer": a, "cat": self.cat(a) if a else "Who"})
                out.append(None)
            else:
                out.append(p)
        return out, slots


# ════════════════════════════════════════════════════════════════════════════
# Play state
# ════════════════════════════════════════════════════════════════════════════
class State:
    def __init__(self, d: dict | None = None):
        d = d or {}
        self.case_id = d.get("case", "malibu_mansion")
        self.room = d.get("room", "")
        self.found: list[str] = d.get("found", [])
        self.solved: list[str] = d.get("solved", [])
        self.visited_rooms: list[str] = d.get("visitedRooms", [])
        self.heard: list[str] = d.get("heard", [])            # room/thing/i
        self.inspected: list[str] = d.get("inspected", [])    # room/thing
        self.gather_step: int | None = d.get("gatherStep")
        self.done = d.get("done", False)
        self.log: dict = d.get("log", {"wrong": {}, "found_before_gathering": None})

    def dump(self) -> dict:
        return {"case": self.case_id, "room": self.room, "found": self.found,
                "solved": self.solved, "visitedRooms": self.visited_rooms,
                "heard": self.heard, "inspected": self.inspected,
                "gatherStep": self.gather_step, "done": self.done, "log": self.log}

    def unlock(self, case: Case, ids) -> list[str]:
        new = []
        for cid in ids:
            if cid and cid in case.clues and cid not in self.found:
                self.found.append(cid)
                new.append(cid)
        return new


def gate_ok(st: State, clue: str = "", solve: str = "") -> bool:
    return (not clue or clue in st.found) and (not solve or solve in st.solved)


def kw_ids(entries) -> list[str]:
    return [k["id"] if isinstance(k, dict) else k for k in entries or []]


def available_topics(st: State, t: dict):
    for i, tp in enumerate(t.get("topics", [])):
        if gate_ok(st, tp.get("requiresClue", ""), tp.get("requiresSolve", "")):
            yield i, tp


def has_unheard(st: State, room: str, t: dict) -> bool:
    return any(f"{room}/{t['id']}/{i}" not in st.heard for i, _ in available_topics(st, t))


def confrontations_left(case: Case, st: State):
    return [(r, t) for r, t, i in case.solve_topics() if f"{r}/{t['id']}/{i}" not in st.heard]


def final_unlocked(case: Case, st: State) -> bool:
    return all(r in st.solved for r in case.rooms) and not confrontations_left(case, st)


def bank_local(case: Case, st: State, room: str) -> list[str]:
    return [c for c in st.found if case.clues[c].get("roomId", "") in (room, "")]


def time_key(cid: str, case: Case):
    m = TIME_RE.search(case.clue_name(cid))
    if not m:
        return (1, 0)
    h, mi, ap = int(m.group(1)), int(m.group(2)), m.group(3).upper()
    return (0, (h % 12 + (12 if ap == "PM" else 0)) * 60 + mi)


# ════════════════════════════════════════════════════════════════════════════
# Text rendering
# ════════════════════════════════════════════════════════════════════════════
def render_text(text: str) -> str:
    text = text.replace("|", "\n\n")
    return re.sub(r"\[([^\]]+)\]", r"*\1*", text)


def render_beats(case: Case, beats: list[dict]) -> str:
    cast = case.script.get("cast", {})
    out = []
    for b in beats:
        if b.get("card"):
            out.append("=== " + b["text"].replace("\n\n", " - ").replace("\n", " ") + " ===")
            continue
        who = b.get("who")
        speaker = b.get("speaker")
        if speaker is None:
            speaker = cast.get(who, {}).get("name", "") if who else ""
        pre = ""
        if b.get("stamp"):
            pre += f"[{b['stamp']}] "
        if b.get("chyron"):
            pre += f"(news banner: {b['chyron']}) "
        line = b["text"].replace("\n", " ")
        out.append(f"{pre}{speaker + ': ' if speaker else ''}{line}")
    return "\n".join(out)


def match(items: list[tuple[str, str]], query: str):
    """items: (id, display). Exact id, exact name, then substring on either."""
    q = query.lower().strip()
    for i, name in items:
        if q in (i.lower(), name.lower()):
            return i
    hits = [i for i, name in items if q in i.lower() or q in name.lower()]
    return hits[0] if len(hits) == 1 else None


# ════════════════════════════════════════════════════════════════════════════
# Commands
# ════════════════════════════════════════════════════════════════════════════
class Game:
    def __init__(self, case: Case, st: State):
        self.c, self.s = case, st
        self.out: list[str] = []

    def p(self, *lines):
        self.out.extend(lines)

    # ── movement ───────────────────────────────────────────────────────────
    def enter(self, room: str):
        self.s.room = room
        if room not in self.s.visited_rooms:
            self.s.visited_rooms.append(room)

    def cmd_new(self, case_id: str | None = None):
        if self.c.script.get("intro"):
            self.p("--- INTRO ---", render_beats(self.c, self.c.script["intro"]), "")
        self.enter(self.c.rooms[0])
        self.cmd_look()

    def cmd_look(self):
        c, s, r = self.c, self.s, self.s.room
        self.p(f"# {c.room_name(r)}" + ("  [board SOLVED]" if r in s.solved else ""))
        found, total = self.room_counts(r)
        self.p(f"Clues found here: {found}/{total}")
        for t in c.things[r]:
            kind = "person" if t.get("topics") else "object"
            mark = ""
            if kind == "person" and has_unheard(s, r, t):
                mark = "  (speech bubble: has something new to say)"
            elif kind == "object" and f"{r}/{t['id']}" not in s.inspected:
                mark = "  (not inspected yet)"
            self.p(f"- {c.thing_name(t, s)} [{kind}]{mark}")
        self.p("Exits:")
        for d in c.doors[r]:
            req = d.get("RequiresVisited", "")
            lock = "" if not req or req in s.visited_rooms else "  (locked for now)"
            self.p(f"- {c.room_name(d['TargetRoom'])}{lock}")
        if all(x in s.solved for x in c.rooms):
            self.p("FINAL SOLVE: " + ("unlocked - use `final`" if final_unlocked(c, s)
                                      else f"{len(c.rooms)}/{len(c.rooms)} rooms solved, but suspects still owe explanations"))

    def room_counts(self, r: str):
        # Mirrors GetRoomClueCounts(hideWhileLocked): gated-only clues hide until found.
        ungated = set()
        for t in self.c.things[r]:
            ungated |= set(kw_ids(t.get("keywords")))
            for tp in t.get("topics", []):
                if not tp.get("requiresClue") and not tp.get("requiresSolve"):
                    ungated |= set(kw_ids(tp.get("keywords")))
        ids = [k for k, v in self.c.clues.items() if v.get("roomId") == r]
        total = [k for k in ids if k in ungated or k in self.s.found]
        return sum(1 for k in total if k in self.s.found), len(total)

    def cmd_go(self, query: str):
        c, s = self.c, self.s
        doors = c.doors[s.room]
        rid = match([(d["TargetRoom"], c.room_name(d["TargetRoom"])) for d in doors], query)
        if not rid:
            return self.p(f"No exit called '{query}' here.")
        d = next(d for d in doors if d["TargetRoom"] == rid)
        req = d.get("RequiresVisited", "")
        if req and req not in s.visited_rooms:
            return self.p(d.get("LockedText") or "That way is locked for now.")
        self.enter(rid)
        self.cmd_look()

    # ── interaction ────────────────────────────────────────────────────────
    def find_thing(self, query: str):
        r = self.s.room
        names = [(t["id"], self.c.thing_name(t, self.s)) for t in self.c.things[r]]
        names += [(t["id"], t["name"]) for t in self.c.things[r] if t.get("name")]   # pre-reveal alias
        tid = match(names, query)
        return next((t for t in self.c.things[r] if t["id"] == tid), None)

    def announce(self, new: list[str]):
        for cid in new:
            cl = self.c.clues[cid]
            self.p(f"  + NEW CLUE: {cl['name']} ({CAT_LABEL[cl['category']]})")

    def cmd_inspect(self, query: str):
        t = self.find_thing(query)
        if not t:
            return self.p(f"Nothing called '{query}' here.")
        s, r = self.s, self.s.room
        alt = t.get("altText") and gate_ok(s, t.get("altTextRequiresClue", ""), t.get("altTextRequiresSolve", ""))
        self.p(f"## {self.c.thing_name(t, s)}", render_text(t["altText"] if alt else t.get("text", "")))
        if f"{r}/{t['id']}" not in s.inspected:
            s.inspected.append(f"{r}/{t['id']}")
        self.announce(s.unlock(self.c, kw_ids(t.get("keywords"))))
        self.list_topics(t)

    def list_topics(self, t: dict):
        topics = list(available_topics(self.s, t))
        if not topics:
            return
        self.p("", "What does Dikkie do? (use: topic <who> <n>)")
        for i, tp in topics:
            heard = " (already done)" if f"{self.s.room}/{t['id']}/{i}" in self.s.heard else ""
            self.p(f"  {i}. {tp['prompt']}{heard}")
        self.p("  -. Pad away. (Leave)")

    def cmd_topic(self, query: str, n: str):
        t = self.find_thing(query)
        if not t:
            return self.p(f"Nobody called '{query}' here.")
        avail = dict(available_topics(self.s, t))
        try:
            i = int(n)
        except ValueError:
            return self.p("Topic number must be a number.")
        if i not in avail:
            return self.p("That option is not available.")
        tp = avail[i]
        key = f"{self.s.room}/{t['id']}/{i}"
        if key not in self.s.heard:
            self.s.heard.append(key)
        self.p(f"## {self.c.thing_name(t, self.s)} - {tp['prompt']}", render_text(tp["text"]))
        self.announce(self.s.unlock(self.c, kw_ids(tp.get("keywords"))))
        self.list_topics(t)

    # ── notebook ───────────────────────────────────────────────────────────
    def cmd_notebook(self):
        for cat in CATS:
            ids = [c for c in self.s.found if self.c.cat(c) == cat]
            if cat == "WhereWhen":
                ids.sort(key=lambda c: time_key(c, self.c))
            self.p(f"{CAT_LABEL[cat]}:")
            for cid in ids:
                cl = self.c.clues[cid]
                where = self.c.room_name(cl["roomId"]) if cl.get("roomId") else "anywhere"
                self.p(f"  - {cl['name']}  [{where}]: {cl.get('context', '')}")

    def cmd_clue(self, query: str):
        cid = match([(c, self.c.clue_name(c)) for c in self.s.found], query)
        if not cid:
            return self.p(f"No found clue matches '{query}'.")
        cl = self.c.clues[cid]
        self.p(f"{cl['name']} ({CAT_LABEL[cl['category']]})", cl.get("context", ""), cl.get("inspectorDescription", ""))

    def show_board(self, title: str, sentence: str, answers: list[str], bank: list[str]):
        parts, slots = self.c.sentence_slots(sentence, answers)
        text, n = "", 0
        for p in parts:
            if p is None:
                text += f"[{n + 1}: {CAT_LABEL[slots[n]['cat']]}]"
                n += 1
            else:
                text += p
        self.p(f"## {title}", text, "", "Word bank:")
        for cat in CATS:
            ids = [c for c in bank if self.c.cat(c) == cat]
            if cat == "WhereWhen":
                ids.sort(key=lambda c: time_key(c, self.c))
            if ids:
                self.p(f"  {CAT_LABEL[cat]}: " + " | ".join(self.c.clue_name(c) for c in ids))
        self.p(f"Submit with: solve/gather <chip for slot 1> <chip for slot 2> ... ({len(slots)} chips, by name or id; quote names with spaces)")

    def cmd_board(self):
        r = self.s.room
        rc = self.c.room_cfg[r]
        if self.s.gather_step is not None:
            return self.show_gather_board()
        self.show_board(f"{self.c.room_name(r)} - What happened here?" + (" (SOLVED)" if r in self.s.solved else ""),
                        rc.get("localDeductionSentence", ""), rc.get("localDeductionClueIds", []),
                        bank_local(self.c, self.s, r))

    def check(self, sentence, answers, bank, picks):
        _, slots = self.c.sentence_slots(sentence, answers)
        if len(picks) != len(slots):
            return None, f"This board has {len(slots)} slots - give exactly {len(slots)} chips."
        chosen = []
        for i, (q, sl) in enumerate(zip(picks, slots)):
            cid = match([(c, self.c.clue_name(c)) for c in bank], q)
            if not cid:
                return None, f"Slot {i + 1}: no chip in the word bank matches '{q}'."
            if self.c.cat(cid) != sl["cat"]:
                return None, f"Slot {i + 1} takes a {CAT_LABEL[sl['cat']]} chip; '{self.c.clue_name(cid)}' is {CAT_LABEL[self.c.cat(cid)]}."
            chosen.append(cid)
        good = sum(1 for cid, sl in zip(chosen, slots) if cid == sl["answer"])
        return good == len(slots), f"{good}/{len(slots)} fit."

    def cmd_solve(self, picks: list[str]):
        if self.s.gather_step is not None:
            return self.cmd_gather(picks)
        r = self.s.room
        if r in self.s.solved:
            return self.p("This room's board is already solved.")
        rc = self.c.room_cfg[r]
        ok, msg = self.check(rc.get("localDeductionSentence", ""), rc.get("localDeductionClueIds", []),
                             bank_local(self.c, self.s, r), picks)
        if ok is None:
            return self.p(f"[{self.c.room_name(r)} board] {msg}")
        if not ok:
            self.s.log["wrong"][r] = self.s.log["wrong"].get(r, 0) + 1
            if any(a not in self.s.found for a in rc.get("localDeductionClueIds", [])):
                msg = msg.rstrip(".") + " - a piece is still out there."
            return self.p(f"[{self.c.room_name(r)} board] Incorrect logic - {msg}")
        self.s.solved.append(r)
        new = self.s.unlock(self.c, [k for k, v in self.c.clues.items() if v.get("roomId") == r and v.get("isMacroClue")])
        self.p(f"Room solved! ({len(self.s.solved)}/{len(self.c.rooms)})")
        self.announce(new)
        for rr, t, i in self.c.solve_topics():
            if t["topics"][i].get("requiresSolve") == r:
                self.p(f"Your deduction corners {self.c.thing_name(t)} ({self.c.room_name(rr)}) - they have some explaining to do...")

    def cmd_notes(self):
        self.p("## Case Notes")
        for r in self.c.rooms:
            f, tot = self.room_counts(r)
            state = "SOLVED" if r in self.s.solved else ("visited" if r in self.s.visited_rooms else "not visited")
            self.p(f"- {self.c.room_name(r)}: {state}, clues {f}/{tot}")
        left = confrontations_left(self.c, self.s)
        n = len(list(self.c.solve_topics()))
        self.p(f"Confronted: {n - len(left)}/{n}")

    # ── finale ─────────────────────────────────────────────────────────────
    def cmd_final(self):
        c, s = self.c, self.s
        if s.done:
            return self.p("The case is closed.")
        if not all(r in s.solved for r in c.rooms):
            return self.p(f"FINAL SOLVE locked: solve every room first ({len(s.solved)}/{len(c.rooms)}).")
        left = confrontations_left(c, s)
        if left:
            names = sorted({f"{c.thing_name(t)} ({c.room_name(r)})" for r, t in left})
            return self.p("FINAL SOLVE locked: still owing an explanation - " + ", ".join(names))
        g = c.script.get("gathering")
        if not g:
            return self.show_board("The Final Solve", c.final_sentence, c.final_answers,
                                   [x for x in s.found if c.clues[x].get("isMacroClue")])
        if s.log.get("found_before_gathering") is None:
            all_ids = {i for st in g["steps"] for i in st["answers"]}
            s.log["found_before_gathering"] = {"missing_answers": sorted(i for i in all_ids if i not in s.found)}
        for st in g["steps"]:
            s.unlock(c, st["answers"] + st.get("bank", []))
        if s.gather_step is None:
            s.gather_step = 0
            self.p("--- THE GATHERING ---", render_beats(c, g.get("intro", [])))
        self.p(render_beats(c, g["steps"][s.gather_step].get("intro", [])), "")
        self.show_gather_board()

    def show_gather_board(self):
        st = self.c.script["gathering"]["steps"][self.s.gather_step]
        bank = [x for x in self.s.found if x in st.get("bank", [])] or \
               [x for x in self.s.found if self.c.clues[x].get("isMacroClue")]
        self.show_board(f"The Gathering - {st['title']}", st["sentence"], st["answers"], bank)

    def cmd_gather(self, picks: list[str]):
        c, s = self.c, self.s
        if s.gather_step is None:
            return self.p("The gathering has not started - use `final`.")
        g = c.script["gathering"]
        st = g["steps"][s.gather_step]
        bank = [x for x in s.found if x in st.get("bank", [])]
        ok, msg = self.check(st["sentence"], st["answers"], bank, picks)
        if ok is None:
            return self.p(msg)
        if not ok:
            key = f"gather:{st['id']}"
            s.log["wrong"][key] = s.log["wrong"].get(key, 0) + 1
            return self.p(f"{st.get('wrongLine') or 'Incorrect logic'} - {msg}")
        self.p(render_beats(c, st.get("success", [])), "")
        s.gather_step += 1
        if s.gather_step < len(g["steps"]):
            self.p(render_beats(c, g["steps"][s.gather_step].get("intro", [])), "")
            self.show_gather_board()
        else:
            s.done = True
            s.gather_step = None
            self.p("--- ENDING ---", render_beats(c, c.script.get("ending", [])))


HELP = """Commands:
  look                      describe the room (people, objects, exits)
  inspect <thing>           examine an object or greet a person (shows their options)
  topic <person> <n>        do option n with a person
  go <room>                 walk through an exit
  notebook                  every clue chip you have found
  clue <chip>               a chip's full description
  board                     this room's deduction board + your word bank
  solve <chip> <chip> ...   submit this room's board (one chip per slot, in order)
  notes                     progress per room
  final                     open the FINAL SOLVE (once unlocked)
  gather <chip> <chip> ...  submit the current gathering board"""


def run_command(st_path: Path, argv: list[str]):
    if not argv:
        print(HELP)
        return
    cmd, args = argv[0], argv[1:]
    if cmd == "new":
        case_id = args[0] if args else "malibu_mansion"
        st = State({"case": case_id})
        g = Game(Case(case_id), st)
        g.cmd_new()
    else:
        if not st_path.exists():
            print("No game in progress - start one with `new`.")
            return
        st = State(json.loads(st_path.read_text(encoding="utf-8")))
        g = Game(Case(st.case_id), st)
        table = {
            "look": lambda: g.cmd_look(),
            "inspect": lambda: g.cmd_inspect(" ".join(args)),
            "talk": lambda: g.cmd_inspect(" ".join(args)),
            "topic": lambda: g.cmd_topic(" ".join(args[:-1]), args[-1]) if len(args) >= 2 else g.p("usage: topic <person> <n>"),
            "go": lambda: g.cmd_go(" ".join(args)),
            "notebook": lambda: g.cmd_notebook(),
            "clue": lambda: g.cmd_clue(" ".join(args)),
            "board": lambda: g.cmd_board(),
            "solve": lambda: g.cmd_solve(args),
            "notes": lambda: g.cmd_notes(),
            "final": lambda: g.cmd_final(),
            "gather": lambda: g.cmd_gather(args),
            "help": lambda: g.p(HELP),
        }
        if cmd not in table:
            g.p(f"Unknown command '{cmd}'.", HELP)
        else:
            table[cmd]()
    st_path.parent.mkdir(parents=True, exist_ok=True)
    st_path.write_text(json.dumps(g.s.dump(), indent=1), encoding="utf-8")
    print("\n".join(g.out))


# ════════════════════════════════════════════════════════════════════════════
# Audit: exhaust the case with the game's own rules
# ════════════════════════════════════════════════════════════════════════════
def audit(case_id: str, dump: Path | None) -> int:
    c = Case(case_id)
    st = State({"case": case_id})
    g = Game(c, st)
    errors, warns = [], []
    script_md = [f"# {case_id} - full script in play order\n"]
    if c.script.get("intro"):
        script_md += ["## Intro\n", render_beats(c, c.script["intro"]), ""]

    g.enter(c.rooms[0])
    seen_text = set()

    def explore():
        """Inspect everything and hear every available topic in every reachable room until nothing changes."""
        changed = True
        while changed:
            changed = False
            for r in c.rooms:
                if r not in st.visited_rooms:
                    # reachable through an open door from a visited room?
                    if not any(d["TargetRoom"] == r and (not d.get("RequiresVisited") or d["RequiresVisited"] in st.visited_rooms)
                               for vr in st.visited_rooms for d in c.doors[vr]):
                        continue
                    g.enter(r)
                    changed = True
                for t in c.things[r]:
                    key = f"{r}/{t['id']}"
                    if key not in st.inspected:
                        st.inspected.append(key)
                        st.unlock(c, kw_ids(t.get("keywords")))
                        if key not in seen_text:
                            seen_text.add(key)
                            script_md.append(f"### {c.room_name(r)} - {c.thing_name(t)}\n\n{render_text(t.get('text', ''))}\n")
                        changed = True
                    for i, tp in available_topics(st, t):
                        hk = f"{r}/{t['id']}/{i}"
                        if hk not in st.heard:
                            st.heard.append(hk)
                            st.unlock(c, kw_ids(tp.get("keywords")))
                            gate = " / ".join(x for x in [tp.get("requiresClue", ""), ("solve:" + tp["requiresSolve"]) if tp.get("requiresSolve") else ""] if x)
                            script_md.append(f"**{c.thing_name(t)} - {tp['prompt']}**{f' _(gate: {gate})_' if gate else ''}\n\n{render_text(tp['text'])}\n")
                            changed = True
                    if t.get("altText") and gate_ok(st, t.get("altTextRequiresClue", ""), t.get("altTextRequiresSolve", "")) and f"alt:{key}" not in seen_text:
                        seen_text.add(f"alt:{key}")
                        script_md.append(f"_{c.thing_name(t)} (alt intro):_ {render_text(t['altText'])}\n")

    # Phase 1: explore, then solve every board as soon as its answers are in the bank.
    progress = True
    while progress:
        progress = False
        explore()
        for r in c.rooms:
            if r in st.solved or r not in st.visited_rooms:
                continue
            rc = c.room_cfg[r]
            ans = rc.get("localDeductionClueIds", [])
            bank = bank_local(c, st, r)
            if all(a in bank for a in ans):
                _, slots = c.sentence_slots(rc.get("localDeductionSentence", ""), ans)
                for sl in slots:
                    opts = [x for x in bank if c.cat(x) == sl["cat"]]
                    if len(opts) < 2:
                        warns.append(f"{r}: slot [{sl['tag']}] has only {len(opts)} option(s) in the bank - auto-fill, not a deduction")
                st.solved.append(r)
                st.unlock(c, [k for k, v in c.clues.items() if v.get("roomId") == r and v.get("isMacroClue")])
                script_md.append(f"**[{c.room_name(r)} board]** {rc.get('localDeductionSentence', '')}\n")
                progress = True

    for r in c.rooms:
        if r not in st.visited_rooms:
            errors.append(f"room '{r}' is never reachable")
        elif r not in st.solved:
            missing = [a for a in c.room_cfg[r].get("localDeductionClueIds", []) if a not in st.found]
            errors.append(f"room '{r}' board unsolvable - answers never found: {missing}")
    # A macro clue only reachable through a gated topic leaks: solving its room
    # auto-unlocks every macro clue of that room, before the confrontation is heard.
    ungated = set()
    for r in c.rooms:
        for t in c.things[r]:
            ungated |= set(kw_ids(t.get("keywords")))
            for tp in t.get("topics", []):
                if not tp.get("requiresClue") and not tp.get("requiresSolve"):
                    ungated |= set(kw_ids(tp.get("keywords")))
    for k, v in c.clues.items():
        if v.get("isMacroClue") and v.get("roomId") and k not in ungated:
            errors.append(f"macro clue '{k}' only unlocks via a gated topic - the {v['roomId']} solve leaks it early (make it micro)")
    never = [k for k, v in c.clues.items() if k not in st.found]
    for k in never:
        warns.append(f"clue '{k}' ({c.clues[k].get('roomId') or 'global'}) is never unlockable")
    left = confrontations_left(c, st)
    for r, t in left:
        errors.append(f"confrontation never reachable: {c.thing_name(t)} in {r}")

    # Phase 2: the finale.
    gscript = c.script.get("gathering")
    if gscript and not errors:
        ids_all = [i for s in gscript["steps"] for i in s["answers"] + s.get("bank", [])]
        for cid in ids_all:
            if cid not in c.clues:
                errors.append(f"gathering references unknown clue '{cid}'")
        # which answers the player is NOT guaranteed to hold before the gathering
        guaranteed = set()
        for r in c.rooms:
            guaranteed |= set(c.room_cfg[r].get("localDeductionClueIds", []))
            guaranteed |= {k for k, v in c.clues.items() if v.get("roomId") == r and v.get("isMacroClue")}
        for r, t, i in c.solve_topics():
            guaranteed |= set(kw_ids(t["topics"][i].get("keywords")))
        script_md.append("## The Gathering\n")
        script_md.append(render_beats(c, gscript.get("intro", [])) + "\n")
        for s in gscript["steps"]:
            parts, slots = c.sentence_slots(s["sentence"], s["answers"])
            if len(slots) != len(s["answers"]):
                errors.append(f"gathering step '{s['id']}': {len(slots)} slots vs {len(s['answers'])} answers")
            for a in s["answers"]:
                if a not in s.get("bank", []):
                    errors.append(f"gathering step '{s['id']}': answer '{a}' missing from its bank")
                if a not in guaranteed:
                    warns.append(f"gathering step '{s['id']}': answer '{a}' ({c.clue_name(a)}) is not guaranteed found before the finale (handed over silently)")
            for sl in slots:
                opts = [x for x in s.get("bank", []) if x in c.clues and c.cat(x) == sl["cat"]]
                if len(opts) < 2:
                    warns.append(f"gathering step '{s['id']}': slot [{sl['tag']}] has {len(opts)} option(s)")
            script_md.append(render_beats(c, s.get("intro", [])))
            script_md.append(f"\n**[Gathering board - {s['title']}]** {s['sentence']}  \n_answers: {', '.join(c.clue_name(a) for a in s['answers'])}_\n")
            script_md.append(render_beats(c, s.get("success", [])) + "\n")
        script_md += ["## Ending\n", render_beats(c, c.script.get("ending", []))]
    elif not gscript:
        missing = [a for a in c.final_answers if a not in st.found]
        if missing:
            errors.append(f"final board answers never found: {missing}")

    # Non-ASCII guard on every text the player can read.
    def ascii_check(where, text):
        bad = {ch for ch in text if ch != "\n" and not (32 <= ord(ch) <= 126)}
        if bad:
            errors.append(f"non-ASCII {sorted(bad)} in {where}")
    for r in c.rooms:
        for t in c.things[r]:
            ascii_check(f"{r}/{t['id']}", t.get("text", "") + t.get("altText", "") + "".join(tp["text"] + tp["prompt"] for tp in t.get("topics", [])))
    ascii_check("case_script.json", json.dumps(c.script, ensure_ascii=False))

    print(f"== story audit: {case_id} ==")
    print(f"rooms solved: {len(st.solved)}/{len(c.rooms)} | clues found: {len(st.found)}/{len(c.clues)} | "
          f"confrontations: {len(list(c.solve_topics())) - len(left)}/{len(list(c.solve_topics()))}")
    for e in errors:
        print("ERROR:", e)
    for w in warns:
        print("WARN: ", w)
    print(f"{len(errors)} error(s), {len(warns)} warning(s)")
    if dump:
        dump.parent.mkdir(parents=True, exist_ok=True)
        dump.write_text("\n".join(script_md), encoding="utf-8")
        print(f"script dump -> {dump}")
    return 1 if errors else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--state", type=Path, default=REPO / "debug_output" / "story_sim_state.json")
    ap.add_argument("--dump", type=Path, default=None)
    ap.add_argument("command", nargs=argparse.REMAINDER)
    a = ap.parse_args()
    if a.command and a.command[0] == "audit":
        sys.exit(audit(a.command[1] if len(a.command) > 1 else "malibu_mansion", a.dump))
    run_command(a.state, a.command)


if __name__ == "__main__":
    main()
