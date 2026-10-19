// The six views. Each returns a DOM node for #view.

const VIEWS = {
  overview: { label: "Overview", render: viewOverview },
  room: { label: "Rooms", render: viewRoom },
  clues: { label: "Clues", render: viewClues },
  boards: { label: "Boards", render: viewBoards },
  timeline: { label: "Timeline", render: viewTimeline },
  flow: { label: "Flow", render: viewFlow },
};

function catCounts(clues) {
  return h("span", { class: "row", style: { gap: "10px" } }, CATEGORIES.map(cat => {
    const n = clues.filter(c => c.category === cat).length;
    return h("span", { class: "cat-" + cat, title: cat }, h("span", { class: "dot" }), " ", cat, " ", h("b", {}, n));
  }));
}

function spriteImg(roomId, inter) {
  const local = room(roomId).sprites.includes(inter.id);
  const base = local ? `/content/Levels/${S.caseId}/${roomId}/Interactables/${inter.id}` : inter.texture ? `/content/${inter.texture}` : null;
  if (!base) return h("img", { alt: "" });
  return h("img", { src: base + ".png", alt: "", onerror: e => { if (!e.target.src.endsWith(".jpg")) e.target.src = base + ".jpg"; } });
}

// ── overview ─────────────────────────────────────────────────────────────

function viewOverview() {
  const ix = idx();
  const clues = [...ix.clues.values()];
  const global = clues.filter(c => c.roomId === "");
  const final = ix.boards.find(b => b.key === "");
  return h("div", {},
    h("h2", {}, S.caseId),
    h("div", { class: "panel", style: { marginBottom: "14px" } },
      h("div", { class: "stat-row" },
        h("span", {}, h("b", {}, clues.length), " clues"),
        h("span", {}, h("b", {}, clues.filter(c => c.isMacroClue).length), " macro"),
        h("span", {}, h("b", {}, global.length), " case-global"),
        h("span", {}, h("b", {}, ix.segments.length), " dialogue segments"),
        catCounts(clues)),
      ix.unknown.size ? h("p", {}, h("span", { class: "badge bad" }, "unknown clue ids referenced:"), " ", [...ix.unknown].map(id => h("span", { class: "mono" }, id + " "))) : null,
      h("p", { style: { marginBottom: 0 } }, h("b", {}, "Final board: "), h("a", { href: routeTo("boards") }, `${final.slots.length} slots`), " - ",
        final.answers.map(id => clueChip(id, { answer: true }))),
    ),
    h("div", { class: "room-grid" }, roomIds().map(roomCard)),
  );
}

function roomCard(roomId) {
  const r = room(roomId);
  const ix = idx();
  const inters = r.config?.interactables || [];
  const chars = inters.filter(isCharacter);
  const roomClues = [...ix.clues.values()].filter(c => c.roomId === roomId);
  const board = ix.boards.find(b => b.key === roomId);
  const exits = mapObjects(r.map, "Transfers");
  const solveOpens = ix.solveGates.get(roomId) || [];
  const listed = (caseConfig().rooms || []).includes(roomId);
  return h("div", { class: "room-card" },
    h("a", { href: routeTo("room", roomId) }, roomThumb(roomId, { labels: false })),
    h("div", { class: "body" },
      h("div", { class: "row" }, h("h3", { class: "grow" }, h("a", { href: routeTo("room", roomId) }, roomId)),
        listed ? null : h("span", { class: "badge bad" }, "not in case rooms[]"),
        h("a", { href: routeTo("flow", roomId) }, "flow")),
      h("div", {}, h("span", { class: "muted" }, "Characters: "),
        chars.length ? chars.map((c, i) => [i ? ", " : "", h("a", { href: routeTo("room", roomId, c.id) }, interName(c))]) : "-"),
      h("div", {}, h("span", { class: "muted" }, `Objects (${inters.length - chars.length}): `),
        inters.filter(i => !isCharacter(i)).map((c, i) => [i ? ", " : "", h("a", { href: routeTo("room", roomId, c.id) }, interName(c))])),
      h("div", { class: "stat-row" }, h("span", {}, h("b", {}, roomClues.length), " room clues"), catCounts(roomClues)),
      h("div", {}, h("span", { class: "muted" }, "Board: "), board.slots.length ? `${board.slots.length} slots` : "none",
        solveOpens.length ? [" - solving opens ", solveOpens.map(g => h("span", { class: "badge solve" }, interName(g.seg.inter)))] : null),
      h("div", {}, h("span", { class: "muted" }, "Exits: "), exits.map((t, i) => [i ? ", " : "",
        h("a", { href: routeTo("room", t.props.TargetRoom) }, t.props.TargetRoom),
        t.props.RequiresVisited ? h("span", { class: "badge", title: "RequiresVisited" }, " after " + t.props.RequiresVisited) : null])),
    ),
  );
}

// ── room ─────────────────────────────────────────────────────────────────

function viewRoom(roomId, focusId) {
  roomId = roomId || roomIds()[0];
  const r = room(roomId);
  if (!r) return h("p", {}, `No room "${roomId}".`);
  if (!r.config) return h("p", {}, `${roomId} has no room_config.json.`);
  const cfg = r.config;
  cfg.interactables = cfg.interactables || [];
  const mapIds = new Set(mapObjects(r.map, "Interactables").map(o => o.name));
  const unplaced = cfg.interactables.filter(i => !mapIds.has(i.id));

  const roomTabs = h("div", { class: "row", style: { marginBottom: "10px" } },
    roomIds().map(id => h("a", { href: routeTo("room", id), class: "badge", style: id === roomId ? { fontWeight: 700, color: "var(--ink)" } : {} }, id)));

  const side = h("div", { class: "room-side" },
    roomThumb(roomId, { selected: focusId, onPick: id => {
      const card = document.getElementById("card-" + id);
      if (card) { card.scrollIntoView({ behavior: "smooth", block: "start" }); flash(card); }
      else if (confirm(`"${id}" is placed in the map but has no room_config entry. Add one?`)) {
        cfg.interactables.push({ id, name: prettyId(id), text: "", keywords: [], texture: "Shared/placeholder_object", scale: 0.1 });
        markDirty(roomId);
        render();
      }
    } }),
    h("div", { class: "panel" },
      h("h4", {}, "Interactables"),
      h("div", { class: "links" }, cfg.interactables.map(i => h("a", { class: "chip", href: routeTo("room", roomId, i.id) },
        isCharacter(i) ? "* " : "", interName(i)))),
      unplaced.length ? h("p", {}, h("span", { class: "badge bad" }, "not placed in room_map.json:"), " ", unplaced.map(i => i.id).join(", ")) : null,
      h("p", { class: "muted", style: { margin: "6px 0 0", fontSize: "12px" } }, "* = character. Click a rect on the map to jump; dashed red = in map without config."),
    ),
    boardPanel(idx().boards.find(b => b.key === roomId)),
  );

  const cards = h("div", { class: "cards" },
    cfg.interactables.map((inter, i) => interactableCard(roomId, inter, i)),
    h("div", {}, btn("+ interactable", () => {
      const id = prompt("Interactable id (must match an object name in the Tiled Interactables layer):", "inspect_");
      if (!id) return;
      cfg.interactables.push({ id, name: prettyId(id), text: "", keywords: [], texture: "Shared/placeholder_object", scale: 0.1 });
      markDirty(roomId);
      go(routeTo("room", roomId, id));
      render();
    })),
  );

  const el = h("div", {}, roomTabs, h("div", { class: "room-layout" }, side, cards));
  if (focusId) requestAnimationFrame(() => {
    const card = document.getElementById("card-" + focusId);
    if (card) { card.scrollIntoView({ block: "start" }); flash(card); }
  });
  return el;
}

function flash(el) { el.classList.remove("flash"); void el.offsetWidth; el.classList.add("flash"); }

const INTER_FIELDS = [
  ["texture", "text"], ["scale", "number"], ["spriteAnchor", "anchor"], ["offsetX", "number"], ["offsetY", "number"],
  ["interactPadding", "number"], ["alwaysOnTop", "bool"], ["revealName", "text"], ["revealNameOnClue", "clue"],
  ["portraitCrop", "json"],
];
const INTER_HANDLED = new Set(["id", "name", "text", "keywords", "topics", "altText", "altTextRequiresClue", "altTextRequiresSolve",
  ...INTER_FIELDS.map(f => f[0])]);

function interactableCard(roomId, inter, index) {
  const list = room(roomId).config.interactables;
  return mount(refresh => {
    const dirty = () => markDirty(roomId);
    const texts = () => [inter.text, inter.altText].filter(Boolean);

    // intro + alt intro share one keyword list; both previews redraw when it changes
    const editors = [];
    const redrawAll = () => editors.forEach(e => e.redrawPreview());
    let kwTable;
    const rebuildKw = () => { const n = keywordTable(inter, texts, roomId, roomId, redrawAll); kwTable.replaceWith(n); kwTable = n; };
    const intro = textEditor(inter, "text", () => inter.keywords, roomId, rebuildKw);
    editors.push(intro);
    kwTable = keywordTable(inter, texts, roomId, roomId, redrawAll);

    let altSeg = null;
    if (inter.altText !== undefined) {
      const alt = textEditor(inter, "altText", () => inter.keywords, roomId, rebuildKw);
      editors.push(alt);
      altSeg = h("div", { class: "segment alt" },
        h("div", { class: "segment-head" }, h("span", { class: "label" }, "Alt intro"),
          gateEditor(inter, "altTextRequiresClue", "altTextRequiresSolve", roomId),
          h("span", { class: "grow" }),
          btn("remove alt intro", () => { delete inter.altText; delete inter.altTextRequiresClue; delete inter.altTextRequiresSolve; dirty(); refresh(); }, "danger")),
        alt,
        h("div", { class: "muted", style: { fontSize: "12px" } }, "Uses the intro's keyword list."));
    }

    inter.topics = inter.topics || [];
    const topics = inter.topics.map((t, ti) => topicEditor(roomId, inter, t, ti, refresh));
    if (!inter.topics.length) delete inter.topics;   // keep objects free of an empty topics array

    const fields = h("details", { class: "fields" },
      h("summary", {}, "sprite & placement fields"),
      h("div", { class: "field-grid" },
        INTER_FIELDS.map(([key, type]) => [h("label", {}, key), fieldInput(inter, key, type, dirty)]),
        Object.keys(inter).filter(k => !INTER_HANDLED.has(k)).map(k => [h("label", {}, k), fieldInput(inter, k, "json", dirty)]),
      ));

    return h("div", { class: "card", id: "card-" + inter.id },
      h("div", { class: "card-head" },
        spriteImg(roomId, inter),
        h("input", { class: "name", value: inter.name || "", placeholder: prettyId(inter.id), oninput: e => { setOrDelete(inter, "name", e.target.value); dirty(); } }),
        h("span", { class: "mono muted" }, inter.id),
        isCharacter(inter) ? h("span", { class: "badge" }, "character") : null,
        h("span", { class: "grow" }),
        index > 0 ? btn("up", () => { list.splice(index - 1, 0, list.splice(index, 1)[0]); dirty(); render(); }) : null,
        btn("delete", () => { if (confirm(`Delete ${inter.id} from room_config? (The Tiled rect stays.)`)) { list.splice(index, 1); dirty(); render(); } }, "danger"),
      ),
      h("div", { class: "card-body" },
        fields,
        h("div", { class: "segment" },
          h("div", { class: "segment-head" }, h("span", { class: "label" }, "Intro"),
            h("span", { class: "grow" }),
            inter.altText === undefined ? btn("+ alt intro", () => { inter.altText = ""; dirty(); refresh(); }) : null),
          intro, kwTable),
        altSeg,
        topics,
        h("div", {}, btn("+ topic", () => {
          inter.topics = inter.topics || [];
          inter.topics.push({ prompt: "", text: "", keywords: [] });
          dirty(); refresh();
        })),
      ),
    );
  });
}

function fieldInput(obj, key, type, dirty) {
  if (type === "number") return h("input", { type: "number", step: "any", value: obj[key] ?? "", style: { width: "110px" },
    oninput: e => { setOrDelete(obj, key, e.target.value === "" ? "" : Number(e.target.value)); dirty(); } });
  if (type === "bool") return h("input", { type: "checkbox", checked: !!obj[key], onchange: e => { setOrDelete(obj, key, e.target.checked || ""); dirty(); } });
  if (type === "anchor") {
    const sel = h("select", { onchange: () => { setOrDelete(obj, key, sel.value); dirty(); } },
      h("option", { value: "" }, "(default BottomCenter)"), ANCHORS.map(a => h("option", { value: a, selected: obj[key] === a }, a)));
    return sel;
  }
  if (type === "clue") return cluePicker(obj[key], v => { setOrDelete(obj, key, v); dirty(); }, { placeholder: "(none)" });
  if (type === "json") {
    const input = h("input", { class: "mono", value: obj[key] === undefined ? "" : JSON.stringify(obj[key]), style: { width: "100%" } });
    input.addEventListener("input", () => {
      if (input.value.trim() === "") { delete obj[key]; dirty(); input.style.outlineColor = ""; return; }
      try { obj[key] = JSON.parse(input.value); dirty(); input.style.outline = ""; }
      catch { input.style.outline = "2px solid var(--bad)"; }
    });
    return input;
  }
  return h("input", { value: obj[key] ?? "", style: { width: "100%" }, oninput: e => { setOrDelete(obj, key, e.target.value); dirty(); } });
}

function topicEditor(roomId, inter, topic, ti, refreshCard) {
  const dirty = () => markDirty(roomId);
  let kwTable;
  const editor = textEditor(topic, "text", () => topic.keywords, roomId, () => {
    const n = keywordTable(topic, () => [topic.text], roomId, roomId, () => editor.redrawPreview());
    kwTable.replaceWith(n); kwTable = n;
  });
  kwTable = keywordTable(topic, () => [topic.text], roomId, roomId, () => editor.redrawPreview());
  const topics = inter.topics;
  return h("div", { class: "segment topic" },
    h("div", { class: "segment-head" },
      h("span", { class: "label" }, `Topic ${ti + 1}`),
      gateEditor(topic, "requiresClue", "requiresSolve", roomId),
      h("span", { class: "grow" }),
      ti > 0 ? btn("up", () => { topics.splice(ti - 1, 0, topics.splice(ti, 1)[0]); dirty(); refreshCard(); }) : null,
      ti < topics.length - 1 ? btn("down", () => { topics.splice(ti + 1, 0, topics.splice(ti, 1)[0]); dirty(); refreshCard(); }) : null,
      btn("x", () => { if (confirm("Delete this topic?")) { topics.splice(ti, 1); dirty(); refreshCard(); } }, "danger")),
    h("input", { class: "prompt-input", value: topic.prompt || "", placeholder: "Cat action prompt", oninput: e => { topic.prompt = e.target.value; dirty(); } }),
    editor, kwTable);
}

// ── board panel (room side + boards view) ────────────────────────────────

function boardPanel(board, opts = {}) {
  return mount(refresh => {
    const owner = board.owner;
    const fileKey = board.key;
    const sentence = owner[board.sentenceKey] || "";
    const slots = parseSlots(sentence);
    owner[board.answersKey] = owner[board.answersKey] || [];
    const answers = owner[board.answersKey];
    const bank = bankFor(board);

    // rendered sentence: each [tag] replaced by its answer chip
    let n = 0;
    const rendered = h("div", { class: "sentence" }, sentence.split(/(\[[^\]]*\])/).map(part =>
      /^\[[^\]]*\]$/.test(part) ? clueChip(answers[n++] || "", { answer: true }) : part));

    const ta = h("textarea", { rows: 3, value: sentence });
    ta.addEventListener("change", () => { owner[board.sentenceKey] = ta.value; markDirty(fileKey); refresh(); });
    ta.addEventListener("input", () => { owner[board.sentenceKey] = ta.value; markDirty(fileKey); });

    const rows = slots.map((tag, i) => {
      const ans = clueById(answers[i]);
      const cands = ans ? bank.filter(c => c.category === ans.category && c.id !== ans.id) : [];
      return h("tr", {},
        h("td", { class: "mono" }, i + 1),
        h("td", { class: "muted" }, `[${tag}]`),
        h("td", {}, cluePicker(answers[i], v => { answers[i] = v; markDirty(fileKey); }, { width: "150px" })),
        opts.full ? h("td", {}, h("div", { class: "cand" },
          cands.length ? cands.map(c => clueChip(c.id, { dim: isGatedOnly(c.id) })) : h("span", { class: "badge bad" }, "no other option"))) : null,
      );
    });
    const mismatch = slots.length !== answers.length;

    return h("div", { class: opts.full ? "board panel" : "panel" },
      h("h4", {}, opts.full ? null : "Board - ", board.title),
      rendered,
      h("details", { open: opts.full || !sentence, style: { marginTop: "6px" } },
        h("summary", { class: "muted" }, "edit sentence & answers"),
        ta,
        mismatch ? h("p", {}, h("span", { class: "badge bad" }, `${slots.length} slots but ${answers.length} answer ids`), " ",
          answers.length > slots.length ? btn("trim answers", () => { answers.length = slots.length; markDirty(fileKey); refresh(); }) : null) : null,
        h("table", { class: "grid slot-table" },
          h("tr", {}, h("th", {}, "#"), h("th", {}, "tag"), h("th", {}, "answer"), opts.full ? h("th", {}, "other options in the word bank (same category; faded = confrontation-only)") : null),
          rows),
      ),
    );
  });
}

// ── clues ────────────────────────────────────────────────────────────────

const clueFilter = { q: "", room: "*", cat: "*", macro: "*" };

function viewClues() {
  const ix = idx();
  const body = h("tbody");
  const draw = () => {
    const q = clueFilter.q.toLowerCase();
    const list = [...ix.clues.values()].filter(c =>
      (clueFilter.room === "*" || c.roomId === clueFilter.room) &&
      (clueFilter.cat === "*" || c.category === clueFilter.cat) &&
      (clueFilter.macro === "*" || String(!!c.isMacroClue) === clueFilter.macro) &&
      (!q || [c.id, c.name, c.context, c.inspectorDescription].join(" ").toLowerCase().includes(q)));
    body.replaceChildren(...list.map(c => {
      const src = ix.sources.get(c.id) || [];
      const use = ix.boardUse.get(c.id) || [];
      const gates = ix.gates.get(c.id) || [];
      return h("tr", { class: "click" + (c.id === drawerClueId ? " sel" : ""), onclick: () => { openClue(c.id); draw(); } },
        h("td", {}, h("span", { class: "dot cat-" + c.category }), " ", h("b", {}, c.name), h("div", { class: "mono muted" }, c.id)),
        h("td", {}, c.roomId || h("span", { class: "muted" }, "(global)")),
        h("td", {}, c.isMacroClue ? h("span", { class: "badge macro" }, "macro") : ""),
        h("td", {}, src.length ? h("div", { class: "links" }, src.map(s => h("span", {}, segLink(s.seg), " ", gateBadges(s.seg.gates)))) : h("span", { class: "badge bad" }, "none")),
        h("td", {}, gates.map(g => h("div", { class: "muted" }, g.seg ? segLabel(g.seg) : interName(g.inter)))),
        h("td", {}, use.map(u => h("div", {}, h("span", { class: "badge" }, `${u.board.title} #${u.slot + 1}`)))),
      );
    }));
  };
  const opt = (v, label) => h("option", { value: v }, label ?? v);
  const filterSel = (key, options) => {
    const sel = h("select", { onchange: () => { clueFilter[key] = sel.value; draw(); } }, options);
    sel.value = clueFilter[key];
    return sel;
  };
  draw();
  return h("div", {},
    h("div", { class: "filters" },
      h("input", { type: "search", placeholder: "filter...", value: clueFilter.q, oninput: e => { clueFilter.q = e.target.value; draw(); } }),
      filterSel("room", [opt("*", "all rooms"), opt("", "(global)"), roomIds().map(r => opt(r))]),
      filterSel("cat", [opt("*", "all categories"), CATEGORIES.map(c => opt(c))]),
      filterSel("macro", [opt("*", "macro + micro"), opt("true", "macro only"), opt("false", "micro only")]),
      h("span", { class: "grow" }),
      btn("+ new clue", () => {
        const id = prompt("New clue id:");
        if (!id) return;
        if (clueById(id)) return openClue(id);
        addClue({ id, roomId: clueFilter.room !== "*" ? clueFilter.room : "" });
        refreshClueDatalist();
        openClue(id);
        render();
      })),
    h("table", { class: "grid" },
      h("thead", {}, h("tr", {}, ["clue", "room", "", "unlocked by", "opens", "boards"].map(t => h("th", {}, t)))),
      body),
  );
}

// ── boards ───────────────────────────────────────────────────────────────

function viewBoards() {
  const boards = idx().boards;
  return h("div", {},
    h("p", { class: "muted" }, "Each slot shows its answer and the other clues of the same category the player can pick from that board's word bank (local: room + case-global clues; final: macro clues)."),
    [boards.find(b => b.key === ""), ...boards.filter(b => b.key !== "")].map(b => boardPanel(b, { full: true })));
}

// ── timeline ─────────────────────────────────────────────────────────────

function viewTimeline() {
  const ix = idx();
  const timed = [...ix.clues.values()].map(c => ({ c, t: parseTime(c.name) })).filter(x => x.t !== null).sort((a, b) => a.t - b.t);
  return h("div", {},
    h("p", { class: "muted" }, "Every clue whose name is a time, in order. Each source shows the other clues unlocked in the same dialogue - the event the time is paired with."),
    h("div", { class: "tl" }, timed.map(({ c }) => {
      const src = ix.sources.get(c.id) || [];
      const use = ix.boardUse.get(c.id) || [];
      return h("div", { class: "tl-item" },
        h("div", { class: "row" }, h("span", { class: "tl-time" }, c.name), clueChip(c.id, { showId: true }),
          c.isMacroClue ? h("span", { class: "badge macro" }, "macro") : h("span", { class: "badge" }, "micro"),
          use.map(u => h("span", { class: "badge" }, `answer: ${u.board.title} #${u.slot + 1}`))),
        h("div", { class: "muted", style: { fontSize: "12px" } }, c.context),
        src.length ? src.map(s => h("div", { class: "tl-src" },
          h("div", { class: "row" }, segLink(s.seg), gateBadges(s.seg.gates)),
          h("div", { class: "row", style: { marginTop: "3px" } }, h("span", { class: "muted" }, "paired with:"),
            s.seg.keywords.filter(k => k.id !== c.id).map(k => clueChip(k.id))),
          h("div", { class: "snip" }, snippet(s.seg.text, s.kw.displayText, s.seg.keywords)),
        )) : h("div", { class: "tl-src" }, h("span", { class: "badge bad" }, "no source")),
      );
    })),
  );
}

// The sentence(s) around a bracket, rendered with keyword colors.
function snippet(text, display, keywords) {
  const i = (text || "").toLowerCase().indexOf("[" + display.toLowerCase() + "]");
  if (i < 0) return richPreview(text.slice(0, 200), keywords);
  let start = Math.max(0, text.lastIndexOf("|", i) + 1, text.lastIndexOf("\n", i) + 1);
  let end = text.length;
  for (const sep of ["|", "\n"]) { const j = text.indexOf(sep, i); if (j >= 0) end = Math.min(end, j); }
  if (i - start > 220) start = i - 220;
  if (end - i > 220) end = i + 220;
  return richPreview(text.slice(start, end), keywords);
}

// ── flow ─────────────────────────────────────────────────────────────────
// Three columns per room: dialogue sources -> clues -> what the clues feed
// (board slots, gated topics anywhere). Wires drawn in SVG; hover lights a path.

function viewFlow(roomId) {
  roomId = roomId || roomIds()[0];
  const r = room(roomId);
  if (!r?.config) return h("p", {}, `No room "${roomId}".`);
  const ix = idx();
  const edges = [];   // [fromKey, toKey, gated]
  const nodes = new Map();

  const node = (key, label, cls, onclick) => {
    const el = h("div", { class: "fnode " + (cls || ""), "data-key": key, onclick }, label);
    nodes.set(key, el);
    return el;
  };

  // left: sources in this room
  const left = [h("h4", {}, "Dialogue in " + roomId)];
  const clueKeys = new Set();
  const segs = ix.segments.filter(s => s.room === roomId && s.part !== "altText");
  let lastInter = null;
  for (const seg of segs) {
    if (seg.inter !== lastInter) {
      left.push(h("div", { class: "fnode group" }, h("a", { href: routeTo("room", roomId, seg.inter.id) }, interName(seg.inter))));
      lastInter = seg.inter;
    }
    const key = `seg:${seg.inter.id}:${seg.part}:${seg.topicIndex ?? ""}`;
    const gated = !!(seg.gates.clue || seg.gates.solve);
    left.push(node(key, [seg.part === "text" ? "intro" : `"${seg.topic.prompt}"`, " ", gateBadges(seg.gates)], "",
      () => go(routeTo("room", roomId, seg.inter.id))));
    for (const kw of seg.keywords) { edges.push([key, "clue:" + kw.id, gated]); clueKeys.add(kw.id); }
    if (seg.gates.clue) edges.push(["clue:" + seg.gates.clue, key, true, "gate"]);
  }
  // also show this room's clues even if unlocked elsewhere
  for (const c of ix.clues.values()) if (c.roomId === roomId) clueKeys.add(c.id);

  // middle: clues
  const clueList = [...clueKeys].map(id => ix.clues.get(id) || { id, name: id, category: "", roomId: "?" })
    .sort((a, b) => CATEGORIES.indexOf(a.category) - CATEGORIES.indexOf(b.category) || (parseTime(a.name) ?? 1e9) - (parseTime(b.name) ?? 1e9));
  const middle = [h("h4", {}, "Clues")].concat(clueList.map(c => node("clue:" + c.id,
    [c.name, c.roomId !== roomId ? h("span", { class: "muted" }, c.roomId === "" ? " (global)" : ` (${c.roomId})`) : null,
      c.isMacroClue ? h("span", { class: "badge macro" }, "M") : null],
    "cat-" + c.category, () => openClue(c.id))));

  // right: consumers
  const right = [h("h4", {}, "Feeds")];
  for (const b of ix.boards.filter(b => b.key === roomId || b.key === "")) {
    right.push(node(`board:${b.key}`, h("a", { href: routeTo("boards") }, b.key ? "Local board" : "Final board"), "group"));
    b.answers.forEach((id, i) => {
      if (!clueKeys.has(id)) return;
      const key = `slot:${b.key}:${i}`;
      right.push(node(key, `#${i + 1} [${b.slots[i] ?? "?"}]`));
      edges.push(["clue:" + id, key, false]);
    });
  }
  const solveOpens = ix.solveGates.get(roomId) || [];
  if (solveOpens.length) {
    right.push(h("div", { class: "fnode group" }, "Solving the local board opens"));
    const boardKey = `board:${roomId}`;
    solveOpens.forEach((g, i) => {
      const key = `solve:${i}`;
      right.push(node(key, segLabel(g.seg) + (g.seg.room !== roomId ? ` (${g.seg.room})` : ""), "", () => go(routeTo("room", g.seg.room, g.seg.inter.id))));
      if (nodes.has(boardKey)) edges.push([boardKey, key, true]);
    });
  }
  const crossGates = [...clueKeys].flatMap(id => (ix.gates.get(id) || []).filter(g => g.seg && g.seg.room !== roomId).map(g => [id, g]));
  if (crossGates.length) {
    right.push(h("div", { class: "fnode group" }, "Opens in other rooms"));
    crossGates.forEach(([id, g], i) => {
      const key = `xgate:${i}`;
      right.push(node(key, `${g.seg.room} > ${segLabel(g.seg)}`, "", () => go(routeTo("room", g.seg.room, g.seg.inter.id))));
      edges.push(["clue:" + id, key, true]);
    });
  }

  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("class", "wires");
  const wrap = h("div", { class: "flow" }, svg,
    h("div", { class: "flow-col" }, left), h("div", { class: "flow-col" }, middle), h("div", { class: "flow-col" }, right));

  const paths = [];
  const draw = () => {
    svg.replaceChildren();
    paths.length = 0;
    const box = wrap.getBoundingClientRect();
    for (const [from, to, gated] of edges) {
      const a = nodes.get(from), b = nodes.get(to);
      if (!a || !b) continue;
      const ra = a.getBoundingClientRect(), rb = b.getBoundingClientRect();
      // back-edges (clue -> gated source on the left) leave from the clue's left side;
      // same-column edges (board -> topics it opens) loop out of the left edge
      const same = Math.abs(rb.left - ra.left) < 2;
      const back = !same && rb.left < ra.left;
      const y1 = ra.top + ra.height / 2 - box.top, y2 = rb.top + rb.height / 2 - box.top;
      const p = document.createElementNS("http://www.w3.org/2000/svg", "path");
      if (same) {
        const x = ra.left - box.left, bulge = Math.min(70, 20 + Math.abs(y2 - y1) / 6);
        p.setAttribute("d", `M${x},${y1} C${x - bulge},${y1} ${x - bulge},${y2} ${x},${y2}`);
      } else {
        const x1 = (back ? ra.left : ra.right) - box.left;
        const x2 = (back ? rb.right : rb.left) - box.left;
        const dx = Math.max(40, Math.abs(x2 - x1) / 2) * (back ? -1 : 1);
        p.setAttribute("d", `M${x1},${y1} C${x1 + dx},${y1} ${x2 - dx},${y2} ${x2},${y2}`);
      }
      if (gated) p.classList.add("gated");
      p.dataset.from = from; p.dataset.to = to;
      svg.append(p);
      paths.push(p);
    }
  };
  const highlight = key => {
    const hot = new Set(key ? [key] : []);
    if (key) for (const p of paths) {
      if (p.dataset.from === key) hot.add(p.dataset.to);
      if (p.dataset.to === key) hot.add(p.dataset.from);
    }
    for (const p of paths) p.classList.toggle("hot", !!key && (p.dataset.from === key || p.dataset.to === key));
    for (const [k, el] of nodes) { el.classList.toggle("hot", hot.has(k) && !!key); el.classList.toggle("faded", !!key && !hot.has(k)); }
  };
  for (const [k, el] of nodes) {
    el.addEventListener("mouseenter", () => highlight(k));
    el.addEventListener("mouseleave", () => highlight(null));
  }
  requestAnimationFrame(draw);
  new ResizeObserver(draw).observe(wrap);

  return h("div", {},
    h("div", { class: "row", style: { marginBottom: "10px" } },
      roomIds().map(id => h("a", { href: routeTo("flow", id), class: "badge", style: id === roomId ? { fontWeight: 700, color: "var(--ink)" } : {} }, id))),
    h("p", { class: "muted" }, "Hover a node to trace it. Dashed wires = gated (topic needs a clue or a solve)."),
    wrap);
}

// ── search ───────────────────────────────────────────────────────────────

function viewSearch(q) {
  const needle = (q || "").toLowerCase();
  if (!needle) return h("p", { class: "muted" }, "Type in the search box.");
  const ix = idx();
  const segHits = ix.segments.filter(s => (s.text + " " + (s.topic?.prompt || "")).toLowerCase().includes(needle));
  const clueHits = [...ix.clues.values()].filter(c => [c.id, c.name, c.context, c.inspectorDescription].join(" ").toLowerCase().includes(needle));
  const boardHits = ix.boards.filter(b => b.sentence.toLowerCase().includes(needle));
  return h("div", {},
    h("h2", {}, `"${q}"`),
    h("h3", {}, `Clues (${clueHits.length})`),
    h("div", { class: "links" }, clueHits.map(c => clueChip(c.id, { showId: true }))),
    h("h3", {}, `Dialogue (${segHits.length})`),
    segHits.map(s => h("div", { class: "tl-src" }, segLink(s), h("div", { class: "snip" }, highlightSnippet(s.text, needle, s.keywords)))),
    h("h3", {}, `Boards (${boardHits.length})`),
    boardHits.map(b => h("div", { class: "tl-src" }, h("a", { href: routeTo("boards") }, b.title), ": ", b.sentence)),
  );
}

function highlightSnippet(text, needle, keywords) {
  const i = text.toLowerCase().indexOf(needle);
  if (i < 0) return richPreview(text.slice(0, 200), keywords);
  const start = Math.max(0, i - 120), end = Math.min(text.length, i + needle.length + 120);
  // keep brackets whole so the preview still colors them
  const s = text.lastIndexOf("[", start) > text.lastIndexOf("]", start) ? text.lastIndexOf("[", start) : start;
  const e = text.indexOf("]", end) >= 0 && text.lastIndexOf("[", end) > text.lastIndexOf("]", end) ? text.indexOf("]", end) + 1 : end;
  return h("div", {}, s > 0 ? "... " : "", richPreview(text.slice(s, e).replace(/\|/g, " / "), keywords), e < text.length ? " ..." : "");
}
