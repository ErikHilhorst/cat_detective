// Shared UI building blocks. Plain DOM; components that need to redraw
// themselves are built with mount(render) and call refresh().

function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (v === undefined || v === null || v === false) continue;
    if (k.startsWith("on")) el.addEventListener(k.slice(2), v);
    else if (k === "class") el.className = v;
    else if (k === "style" && typeof v === "object") Object.assign(el.style, v);
    else if (k in el && k !== "list") el[k] = v;
    else el.setAttribute(k, v === true ? "" : v);
  }
  append(el, children);
  return el;
}

function append(el, children) {
  for (const c of children.flat(Infinity)) {
    if (c === undefined || c === null || c === false) continue;
    el.append(c instanceof Node ? c : String(c));
  }
  return el;
}

function btn(label, onclick, cls) {
  return h("button", { class: cls, onclick: e => { e.stopPropagation(); onclick(e); } }, label);
}

function mount(render) {
  let el;
  const refresh = () => { const next = render(refresh); el.replaceWith(next); el = next; };
  el = render(refresh);
  return el;
}

function go(hash) { location.hash = hash; }
function routeTo(view, ...parts) { return `#/${S.caseId}/${[view, ...parts].filter(p => p !== undefined && p !== "").join("/")}`; }

// ── clue chips & pickers ─────────────────────────────────────────────────

function clueChip(id, opts = {}) {
  const clue = id ? clueById(id) : null;
  if (!id) return h("span", { class: "muted" }, "-");
  if (!clue) {
    return h("span", { class: "chip unknown", title: "No clue with this id in case_config.json" },
      "? " + id, opts.onCreate ? btn("create", opts.onCreate) : null);
  }
  const cls = ["chip", "cat-" + clue.category, opts.answer && "answer", opts.dim && "dim"].filter(Boolean).join(" ");
  return h("span", { class: cls, title: `${clue.id}\n${clue.category}${clue.isMacroClue ? " / macro" : ""}\n\n${clue.context}`,
    onclick: e => { e.stopPropagation(); openClue(id); } },
    clue.name, opts.showId ? h("span", { class: "muted mono" }, clue.id) : null);
}

function refreshClueDatalist() {
  const dl = document.getElementById("clue-ids");
  dl.innerHTML = "";
  for (const c of idx().clues.values()) dl.append(h("option", { value: c.id }, `${c.name} (${c.category})`));
}

// Text input bound to a clue id, with a live chip that shows what it resolves to.
// onCreateDefaults(id) -> fields for addClue when the id is unknown.
function cluePicker(value, onChange, opts = {}) {
  const chipHolder = h("span");
  const input = h("input", {
    value: value || "", placeholder: opts.placeholder || "clue id", class: "mono",
    style: { width: opts.width || "190px" },
  });
  input.setAttribute("list", "clue-ids");
  const showChip = () => {
    chipHolder.replaceChildren(input.value ? clueChip(input.value.trim(), {
      onCreate: opts.onCreateDefaults ? () => {
        addClue({ id: input.value.trim(), ...opts.onCreateDefaults(input.value.trim()) });
        refreshClueDatalist();
        showChip();
        openClue(input.value.trim());
      } : null,
    }) : "");
  };
  input.addEventListener("input", () => { onChange(input.value.trim()); showChip(); });
  showChip();
  return h("span", { class: "row", style: { gap: "4px", flexWrap: "nowrap" } }, input, chipHolder);
}

function roomSelect(value, onChange, opts = {}) {
  const sel = h("select", { onchange: () => onChange(sel.value) },
    opts.emptyLabel !== undefined ? h("option", { value: "" }, opts.emptyLabel) : null,
    roomIds().map(r => h("option", { value: r, selected: r === value }, r)));
  sel.value = value || "";
  return sel;
}

function select(options, value, onChange) {
  const sel = h("select", { onchange: () => onChange(sel.value) },
    options.map(o => h("option", { value: o, selected: o === value }, o)));
  return sel;
}

// ── rich text preview ────────────────────────────────────────────────────
// Mirrors the game: "|" = page break, "\n" = line break, [x] = keyword
// (case-insensitive match on displayText). Unmatched brackets show red.

function richPreview(text, keywords) {
  const pages = (text || "").split("|");
  return h("div", { class: "preview" }, pages.map((page, i) => {
    const el = h("div", { class: "page", "data-n": pages.length > 1 ? `page ${i + 1}/${pages.length}` : "" });
    const parts = page.split(/(\[[^\]]*\])/);
    for (const part of parts) {
      if (/^\[[^\]]*\]$/.test(part)) {
        const disp = part.slice(1, -1);
        const kw = findKeyword(keywords, disp);
        el.append(kw
          ? h("span", { class: "kw " + (kw.color || "misc"), title: kw.id, onclick: () => openClue(kw.id) }, disp)
          : h("span", { class: "kw bad", title: "No keyword entry for this bracket" }, disp));
      } else {
        part.split("\n").forEach((line, j) => { if (j) el.append(h("br")); el.append(line); });
      }
    }
    return el;
  }));
}

// ── dialogue segment editor ──────────────────────────────────────────────
// Edits one text field (intro / alt intro / topic) side by side with its
// rendered preview. The keyword table is rendered separately because the
// intro and alt intro share one list.

function textEditor(obj, field, keywordsOf, fileKey, onTextChange) {
  const preview = h("div");
  const ta = h("textarea", { rows: 3, value: obj[field] || "", spellcheck: true });
  const fit = () => { ta.style.height = "auto"; ta.style.height = ta.scrollHeight + 4 + "px"; };
  const redraw = () => preview.replaceChildren(richPreview(obj[field], keywordsOf()));
  ta.addEventListener("input", () => {
    obj[field] = ta.value;
    markDirty(fileKey);
    redraw();
    fit();
    onTextChange && onTextChange();
  });
  redraw();
  requestAnimationFrame(fit);
  const el = h("div", { class: "seg-cols" }, ta, preview);
  el.redrawPreview = redraw;
  return el;
}

// Keyword table for a keyword list, checked against the texts that use it.
function keywordTable(owner, textsOf, fileKey, roomId, onChange) {
  return mount(refresh => {
    owner.keywords = owner.keywords || [];
    const kws = owner.keywords;
    const allBrackets = textsOf().flatMap(brackets);
    const used = d => allBrackets.some(b => b.toLowerCase() === (d || "").toLowerCase());
    const missing = [...new Set(allBrackets.filter(b => !findKeyword(kws, b)))];
    const changed = () => { markDirty(fileKey); onChange && onChange(); };

    const rows = kws.map((kw, i) => h("tr", { class: used(kw.displayText) ? "" : "unused", title: used(kw.displayText) ? "" : "No [bracket] with this text" },
      h("td", {}, h("input", { value: kw.displayText, oninput: e => { kw.displayText = e.target.value; changed(); }, onchange: refresh })),
      h("td", {}, cluePicker(kw.id, v => { kw.id = v; changed(); }, {
        onCreateDefaults: id => ({ name: kw.displayText, roomId }),
      })),
      h("td", { class: "c-color" }, select(KW_COLORS, kw.color || "misc", v => { kw.color = v; changed(); })),
      h("td", { class: "c-x" }, btn("x", () => { kws.splice(i, 1); changed(); refresh(); }, "danger")),
    ));

    return h("div", {},
      kws.length ? h("table", { class: "kw-table" }, rows) : null,
      h("div", { class: "missing-brackets" },
        missing.length ? h("span", { class: "badge bad" }, "brackets without keyword:") : null,
        missing.map(b => btn(`+ [${b}]`, () => {
          const guess = [...idx().clues.values()].find(c => c.name.toLowerCase() === b.toLowerCase());
          kws.push({ displayText: b, id: guess ? guess.id : slugify(b), color: "misc" });
          changed();
          refresh();
        })),
        btn("+ keyword", () => { kws.push({ displayText: "", id: "", color: "misc" }); changed(); refresh(); }),
      ),
    );
  });
}

function gateEditor(obj, clueKey, solveKey, fileKey, onChange) {
  return h("span", { class: "row", style: { gap: "6px" } },
    h("span", { class: "badge gate" }, clueKey),
    cluePicker(obj[clueKey], v => { setOrDelete(obj, clueKey, v); markDirty(fileKey); onChange && onChange(); }, { placeholder: "(none)", width: "160px" }),
    h("span", { class: "badge solve" }, solveKey),
    roomSelect(obj[solveKey], v => { setOrDelete(obj, solveKey, v); markDirty(fileKey); onChange && onChange(); }, { emptyLabel: "(none)" }),
  );
}

function setOrDelete(obj, key, value) {
  if (value === "" || value === undefined || value === null) delete obj[key];
  else obj[key] = value;
}

function gateBadges(gates) {
  return [
    gates.clue ? h("span", { class: "badge gate", title: "requires clue" }, "needs ", clueChip(gates.clue)) : null,
    gates.solve ? h("span", { class: "badge solve", title: "requires solve" }, "after solving " + gates.solve) : null,
  ];
}

// Link to the room view, scrolled to an interactable card.
function segLink(seg) {
  return h("a", { href: routeTo("room", seg.room, seg.inter.id) },
    h("span", { class: "muted" }, seg.room + " > "), segLabel(seg));
}

// ── clue drawer ──────────────────────────────────────────────────────────

let drawerClueId = null;

function openClue(id) {
  drawerClueId = id;
  renderDrawer();
}

function closeDrawer() {
  drawerClueId = null;
  document.getElementById("drawer").hidden = true;
  render();
}

function renderDrawer() {
  const drawer = document.getElementById("drawer");
  const clue = drawerClueId && clueById(drawerClueId);
  if (!clue) { drawer.hidden = true; return; }
  drawer.hidden = false;
  const ix = idx();
  const edited = () => markDirty("");

  const field = (key, el) => [h("label", {}, key), el];
  const textInput = (key, rows) => {
    const ta = h("textarea", { rows, value: clue[key] || "", oninput: e => { clue[key] = e.target.value; edited(); } });
    return ta;
  };

  const sources = ix.sources.get(clue.id) || [];
  const gates = ix.gates.get(clue.id) || [];
  const boards = ix.boardUse.get(clue.id) || [];
  const banks = ix.boards.filter(b => b.slots.length && bankFor(b).includes(clue)
    && !b.answers.includes(clue.id)
    && b.answers.some(a => clueById(a)?.category === clue.category));

  const idInput = h("input", { value: clue.id, class: "mono", style: { width: "100%" } });

  drawer.replaceChildren(
    h("div", { class: "row" },
      h("h2", { class: "grow", style: { margin: 0 } }, h("span", { class: "dot cat-" + clue.category }), " ", clue.name),
      btn("close", closeDrawer)),
    h("div", { class: "field-grid" },
      field("id", h("span", { class: "row", style: { flexWrap: "nowrap" } }, idInput, btn("rename", () => {
        const next = idInput.value.trim();
        if (!next || next === clue.id) return;
        if (clueById(next)) return alert(`A clue "${next}" already exists.`);
        if (!confirm(`Rename "${clue.id}" -> "${next}" everywhere?\nExisting save files that found this clue will lose it.`)) return;
        renameClue(clue.id, next);
        drawerClueId = next;
        refreshClueDatalist();
        renderDrawer();
        render();
      }))),
      field("name", h("input", { value: clue.name, style: { width: "100%" }, oninput: e => { clue.name = e.target.value; edited(); } })),
      field("category", select(CATEGORIES, clue.category, v => { clue.category = v; edited(); renderDrawer(); render(); })),
      field("roomId", roomSelect(clue.roomId, v => { clue.roomId = v; edited(); render(); }, { emptyLabel: "(case-global)" })),
      field("isMacroClue", h("input", { type: "checkbox", checked: !!clue.isMacroClue, onchange: e => { clue.isMacroClue = e.target.checked; edited(); render(); } })),
      field("context", textInput("context", 3)),
      field("inspector", textInput("inspectorDescription", 3)),
    ),

    h("h3", {}, `Unlocked by (${sources.length})`),
    sources.length ? h("ul", {}, sources.map(s => h("li", {}, segLink(s.seg), " ", h("span", { class: "muted" }, `[${s.kw.displayText}]`), " ", gateBadges(s.seg.gates))))
      : h("p", { class: "badge bad" }, "Nothing unlocks this clue"),

    gates.length ? [h("h3", {}, `Opens (${gates.length})`),
      h("ul", {}, gates.map(g => h("li", {}, g.seg ? segLink(g.seg) : h("a", { href: routeTo("room", g.room, g.inter.id) }, `${g.room} > ${interName(g.inter)}`), " ", h("span", { class: "badge gate" }, g.kind))))] : null,

    h("h3", {}, "Boards"),
    boards.length || banks.length ? h("ul", {},
      boards.map(u => h("li", {}, h("a", { href: routeTo("boards") }, u.board.title), ` - answer for slot ${u.slot + 1} `, h("span", { class: "muted" }, `[${u.board.slots[u.slot] ?? "?"}]`))),
      banks.map(b => h("li", { class: "muted" }, h("a", { href: routeTo("boards") }, b.title), " - decoy option (same category in word bank)")),
    ) : h("p", { class: "muted" }, "Not used on any board."),

    h("h3", {}, ""),
    btn("delete clue", () => {
      const refs = sources.length + gates.length + boards.length;
      if (!confirm(refs ? `"${clue.id}" is still referenced ${refs} time(s). Delete anyway? References will show as unknown ids.` : `Delete "${clue.id}"?`)) return;
      deleteClue(clue.id);
      refreshClueDatalist();
      closeDrawer();
    }, "danger"),
  );
}

// ── room thumbnail ───────────────────────────────────────────────────────
// Background image with the Tiled Interactables and Transfers rects drawn on top.

function mapObjects(map, layerName) {
  const layer = (map?.layers || []).find(l => l.name === layerName && l.type === "objectgroup");
  if (!layer) return [];
  const ox = layer.offsetx || 0, oy = layer.offsety || 0;
  return (layer.objects || []).map(o => ({
    ...o, x: o.x + ox, y: o.y + oy,
    props: Object.fromEntries((o.properties || []).map(p => [p.name, p.value])),
  }));
}

function roomThumb(roomId, opts = {}) {
  const r = room(roomId);
  if (!r.background) return h("div", { class: "muted" }, "(no background)");
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  const img = h("img", { src: "/content/" + r.background, onload: () => {
    svg.setAttribute("viewBox", `0 0 ${img.naturalWidth} ${img.naturalHeight}`);
  } });
  const ns = (tag, attrs, text) => {
    const el = document.createElementNS("http://www.w3.org/2000/svg", tag);
    for (const [k, v] of Object.entries(attrs)) {
      if (k.startsWith("on")) el.addEventListener(k.slice(2), v); else el.setAttribute(k, v);
    }
    if (text) el.textContent = text;
    return el;
  };
  const configured = new Map((r.config?.interactables || []).map(i => [i.id, i]));
  for (const t of mapObjects(r.map, "Transfers")) {
    const title = ns("title", {}, `-> ${t.props.TargetRoom}${t.props.RequiresVisited ? ` (requires visiting ${t.props.RequiresVisited})` : ""}`);
    const rect = ns("rect", { x: t.x, y: t.y, width: t.width, height: t.height, class: "r-transfer",
      onclick: () => go(routeTo(opts.view || "room", t.props.TargetRoom)) });
    rect.append(title);
    svg.append(rect);
    if (opts.labels !== false) svg.append(ns("text", { x: t.x + 6, y: t.y + t.height / 2 }, "-> " + t.props.TargetRoom));
  }
  for (const o of mapObjects(r.map, "Interactables")) {
    const inter = configured.get(o.name);
    const cls = ["r-inter", inter && isCharacter(inter) && "character", !inter && "missing", opts.selected === o.name && "sel"].filter(Boolean).join(" ");
    const rect = ns("rect", { x: o.x, y: o.y, width: o.width, height: o.height, class: cls,
      onclick: () => opts.onPick ? opts.onPick(o.name) : go(routeTo("room", roomId, o.name)) });
    rect.append(ns("title", {}, inter ? interName(inter) : `${o.name} (in map, no room_config entry)`));
    svg.append(rect);
    if (opts.labels !== false) svg.append(ns("text", { x: o.x, y: o.y - 6 }, inter ? interName(inter) : o.name));
  }
  return h("div", { class: "thumb" + (opts.small ? " small" : "") }, img, svg);
}
