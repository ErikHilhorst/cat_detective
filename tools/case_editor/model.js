// Data layer: load/save/poll the case files and build the cross-reference index.
// Editing mutates the parsed JSON in place (unknown fields survive untouched),
// then markDirty(fileKey) schedules a debounced save. fileKey "" = case_config,
// otherwise the room id.

const CATEGORIES = ["Who", "What", "Why", "WhereWhen"];
const KW_COLORS = ["misc", "plot", "crime"];
const ANCHORS = ["BottomCenter", "TopLeft", "TopCenter", "TopRight", "LeftCenter",
                 "Center", "RightCenter", "BottomLeft", "BottomRight"];

const S = {
  cases: [],
  caseId: null,
  data: null,          // { id, config, version, rooms: { id: { config, version, map, background, sprites } } }
  idx: null,           // built by buildIndex(); null = stale
  dirty: new Set(),    // file keys with unsaved edits
  saveTimer: null,
  saving: false,
  saveGen: 0,          // bumped per flush so a poll that overlapped a save is discarded
  status: "",
  error: "",
  onExternalReload: null,
};

async function api(path, body) {
  const res = await fetch(path, body === undefined ? {} : {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const json = await res.json();
  return { status: res.status, json };
}

async function loadCases() {
  S.cases = (await api("/api/cases")).json;
}

async function loadCase(caseId) {
  const { json } = await api(`/api/case/${caseId}`);
  if (json.error) throw new Error(json.error);
  S.caseId = caseId;
  S.data = json;
  S.dirty.clear();
  invalidate();
}

function invalidate() { S.idx = null; }
function idx() { return S.idx || (S.idx = buildIndex(S.data)); }

function caseConfig() { return S.data.config; }
function roomIds() { return Object.keys(S.data.rooms); }
function room(roomId) { return S.data.rooms[roomId]; }
function clueById(id) { return idx().clues.get(id); }

// ── saving ───────────────────────────────────────────────────────────────

function markDirty(fileKey, structural = true) {
  S.dirty.add(fileKey);
  if (structural) invalidate();
  setStatus("unsaved");
  clearTimeout(S.saveTimer);
  S.saveTimer = setTimeout(flushSaves, 450);
}

async function flushSaves() {
  // One flush at a time: a second POST carrying the pre-save version would 409.
  if (S.saving) { clearTimeout(S.saveTimer); S.saveTimer = setTimeout(flushSaves, 300); return; }
  S.saving = true;
  S.saveGen++;
  try {
    for (const key of [...S.dirty]) {
      const file = key === "" ? S.data : S.data.rooms[key];
      // Clear before sending: typing during the request re-marks the file and
      // queues another flush instead of being dropped when this one returns.
      S.dirty.delete(key);
      setStatus("saving");
      const { status, json } = await api("/api/save", {
        case: S.caseId, room: key || null, data: file.config, version: file.version,
      });
      if (status === 200) {
        file.version = json.version;
      } else {
        S.dirty.add(key);
        if (status === 409) showConflict(key);
        else setStatus("error", json.error || `save failed (${status})`);
        return;
      }
    }
    setStatus(S.dirty.size ? "unsaved" : "saved");
  } finally {
    S.saving = false;
  }
}

function setStatus(status, error = "") {
  S.status = status;
  S.error = error;
  const el = document.getElementById("save-status");
  if (!el) return;
  el.className = status === "unsaved" || status === "saving" ? "dirty" : status === "error" ? "error" : "";
  el.textContent = { unsaved: "unsaved...", saving: "saving...", saved: "saved", error: "save error: " + error }[status] || "";
}

// ── external change polling ──────────────────────────────────────────────
// Files changed by Claude / skills / a text editor reload automatically when
// we hold no unsaved edits for them; otherwise a conflict banner asks.

async function pollVersions() {
  if (!S.caseId || S.saving || !document.getElementById("banner").hidden) return;
  const gen = S.saveGen;
  let json;
  try { json = (await api(`/api/versions/${S.caseId}`)).json; } catch { return; }
  if (S.saving || gen !== S.saveGen) return;   // a save raced this poll; its answer may be stale
  const changed = [];
  for (const [key, version] of Object.entries(json)) {
    const file = key === "" ? S.data : S.data.rooms[key];
    if (file && file.version !== version) changed.push(key);
  }
  if (!changed.length) return;
  if (changed.some(k => S.dirty.has(k))) return showConflict(changed.find(k => S.dirty.has(k)));
  await reloadKeepingView();
}

async function reloadKeepingView() {
  const scroll = window.scrollY;
  await loadCase(S.caseId);
  S.onExternalReload && S.onExternalReload();
  window.scrollTo(0, scroll);
}

function showConflict(key) {
  clearTimeout(S.saveTimer);
  const banner = document.getElementById("banner");
  banner.hidden = false;
  banner.innerHTML = "";
  const label = key === "" ? "case_config.json" : `${key}/room_config.json`;
  banner.append(
    `${label} changed on disk while you had unsaved edits.`,
    btn("Reload from disk (drop my edits)", async () => { banner.hidden = true; await reloadKeepingView(); }),
    btn("Overwrite with mine", async () => {
      banner.hidden = true;
      const file = key === "" ? S.data : S.data.rooms[key];
      file.version = (await api(`/api/versions/${S.caseId}`)).json[key];
      flushSaves();
    }),
  );
  setStatus("error", "conflict");
}

// ── text helpers ─────────────────────────────────────────────────────────

function brackets(text) {
  const out = [];
  const re = /\[([^\]]*)\]/g;
  let m;
  while ((m = re.exec(text || ""))) out.push(m[1]);
  return out;
}

function findKeyword(keywords, display) {
  const d = display.toLowerCase();
  return (keywords || []).find(k => (k.displayText || "").toLowerCase() === d);
}

function parseTime(name) {
  const m = /(\d{1,2})(?::(\d{2}))?\s*(AM|PM)/i.exec(name || "");
  if (!m) return null;
  let h = parseInt(m[1], 10) % 12;
  if (m[3].toUpperCase() === "PM") h += 12;
  return h * 60 + (m[2] ? parseInt(m[2], 10) : 0);
}

function prettyId(id) {
  return (id || "").replace(/^inspect_/, "").split("_").filter(Boolean)
    .map(w => w[0].toUpperCase() + w.slice(1)).join(" ");
}
function interName(inter) { return inter.name || prettyId(inter.id); }
function isCharacter(inter) { return Array.isArray(inter.topics) && inter.topics.length > 0; }

// ── cross-reference index ────────────────────────────────────────────────
//
// A "segment" is one piece of dialogue that can unlock clues:
//   { room, inter, part: "text" | "altText" | "topic", topicIndex, text, keywords, gates }
// gates = { clue, solve } that must hold before the player can see it.

function segmentsOf(roomId, inter) {
  const segs = [];
  segs.push({ room: roomId, inter, part: "text", text: inter.text || "", keywords: inter.keywords || [], gates: {} });
  if (inter.altText) {
    segs.push({
      room: roomId, inter, part: "altText", text: inter.altText, keywords: inter.keywords || [],
      gates: { clue: inter.altTextRequiresClue || "", solve: inter.altTextRequiresSolve || "" },
    });
  }
  (inter.topics || []).forEach((t, i) => segs.push({
    room: roomId, inter, part: "topic", topicIndex: i, topic: t, text: t.text || "",
    keywords: t.keywords || [], gates: { clue: t.requiresClue || "", solve: t.requiresSolve || "" },
  }));
  return segs;
}

function segLabel(seg) {
  const who = interName(seg.inter);
  if (seg.part === "text") return `${who} - intro`;
  if (seg.part === "altText") return `${who} - alt intro`;
  return `${who} - "${seg.topic.prompt || "topic " + (seg.topicIndex + 1)}"`;
}

function parseSlots(sentence) {
  const out = [];
  const re = /\[([^\]]*)\]/g;
  let m;
  while ((m = re.exec(sentence || ""))) out.push(m[1]);
  return out;
}

function buildIndex(data) {
  const ix = {
    clues: new Map(),       // id -> clue object (live reference)
    sources: new Map(),     // clueId -> [{ seg, kw }]
    gates: new Map(),       // clueId -> [{ seg | inter, kind }]   (things this clue unlocks)
    solveGates: new Map(),  // roomId -> [{ seg, kind }]           (things solving this room unlocks)
    boardUse: new Map(),    // clueId -> [{ board, slot }]
    segments: [],
    boards: [],             // { key: roomId | "", title, sentence, answers, slots: [tag], owner }
    unknown: new Set(),     // referenced but undefined clue ids
  };
  const push = (map, key, v) => { if (!map.has(key)) map.set(key, []); map.get(key).push(v); };
  for (const c of data.config.clues || []) ix.clues.set(c.id, c);
  const ref = id => { if (id && !ix.clues.has(id)) ix.unknown.add(id); };

  for (const [roomId, r] of Object.entries(data.rooms)) {
    if (!r.config) continue;
    for (const inter of r.config.interactables || []) {
      for (const seg of segmentsOf(roomId, inter)) {
        ix.segments.push(seg);
        // altText shares the intro's keyword list, and the game unlocks those on
        // interaction whichever intro shows - so they count once, as intro sources.
        if (seg.part !== "altText") {
          for (const kw of seg.keywords) {
            push(ix.sources, kw.id, { seg, kw });
            ref(kw.id);
          }
        }
        if (seg.gates.clue) {
          push(ix.gates, seg.gates.clue, { seg, kind: seg.part === "altText" ? "alt intro" : "topic" });
          ref(seg.gates.clue);
        }
        if (seg.gates.solve) push(ix.solveGates, seg.gates.solve, { seg, kind: seg.part === "altText" ? "alt intro" : "topic" });
      }
      if (inter.revealNameOnClue) {
        push(ix.gates, inter.revealNameOnClue, { inter, room: roomId, kind: `name -> "${inter.revealName}"` });
        ref(inter.revealNameOnClue);
      }
    }
    ix.boards.push({
      key: roomId, title: roomId, owner: r.config,
      sentenceKey: "localDeductionSentence", answersKey: "localDeductionClueIds",
    });
  }
  ix.boards.push({
    key: "", title: "Final solve", owner: data.config,
    sentenceKey: "finalSolveSentence", answersKey: "finalSolveClueIds",
  });
  for (const b of ix.boards) {
    b.sentence = b.owner[b.sentenceKey] || "";
    b.answers = b.owner[b.answersKey] || [];
    b.slots = parseSlots(b.sentence);
    b.answers.forEach((id, i) => { push(ix.boardUse, id, { board: b, slot: i }); ref(id); });
  }
  return ix;
}

// Clues that can appear in a board's word bank (what the player chooses from).
function bankFor(board) {
  const all = [...idx().clues.values()];
  if (board.key === "") return all.filter(c => c.isMacroClue);
  return all.filter(c => c.roomId === board.key || c.roomId === "");
}

// A clue whose every source is gated is a confrontation clue.
function isGatedOnly(clueId) {
  const src = idx().sources.get(clueId) || [];
  return src.length > 0 && src.every(s => s.seg.gates.clue || s.seg.gates.solve);
}

// ── mutations that touch several places ──────────────────────────────────

function renameClue(oldId, newId) {
  const touched = new Set([""]);
  for (const c of caseConfig().clues) if (c.id === oldId) c.id = newId;
  const fix = arr => arr && arr.forEach((v, i) => { if (v === oldId) arr[i] = newId; });
  fix(caseConfig().finalSolveClueIds);
  for (const [roomId, r] of Object.entries(S.data.rooms)) {
    if (!r.config) continue;
    const before = JSON.stringify(r.config);
    fix(r.config.localDeductionClueIds);
    for (const inter of r.config.interactables || []) {
      for (const k of inter.keywords || []) if (k.id === oldId) k.id = newId;
      if (inter.altTextRequiresClue === oldId) inter.altTextRequiresClue = newId;
      if (inter.revealNameOnClue === oldId) inter.revealNameOnClue = newId;
      for (const t of inter.topics || []) {
        if (t.requiresClue === oldId) t.requiresClue = newId;
        for (const k of t.keywords || []) if (k.id === oldId) k.id = newId;
      }
    }
    if (JSON.stringify(r.config) !== before) touched.add(roomId);
  }
  for (const key of touched) markDirty(key);
}

function addClue(fields) {
  const clue = {
    id: fields.id, roomId: fields.roomId ?? "", isMacroClue: !!fields.isMacroClue,
    category: fields.category || "What", name: fields.name || prettyId(fields.id),
    context: fields.context || "", inspectorDescription: fields.inspectorDescription || "",
  };
  caseConfig().clues.push(clue);
  markDirty("");
  return clue;
}

function deleteClue(id) {
  const list = caseConfig().clues;
  const i = list.findIndex(c => c.id === id);
  if (i >= 0) list.splice(i, 1);
  markDirty("");
}

function slugify(text) {
  return (text || "").toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, "");
}
