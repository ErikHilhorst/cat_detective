// Router + header. Routes: #/<case>/<view>/<arg>/<arg>
//   overview | room/<room>/<interactable> | clues | boards | timeline | flow/<room> | search/<query>

function parseRoute() {
  const [caseId, view, ...args] = location.hash.replace(/^#\/?/, "").split("/").map(decodeURIComponent);
  return { caseId, view: view || "overview", args };
}

async function render() {
  const route = parseRoute();
  if (!route.caseId || !S.cases.includes(route.caseId)) {
    return go(`#/${S.cases.includes("malibu_mansion") ? "malibu_mansion" : S.cases[0]}/overview`);
  }
  if (route.caseId !== S.caseId) {
    if (S.dirty.size) await flushSaves();
    await loadCase(route.caseId);
    drawerClueId = null;
  }
  refreshClueDatalist();
  renderHeader(route);

  const view = document.getElementById("view");
  const node = route.view === "search" ? viewSearch(route.args.join("/"))
    : (VIEWS[route.view] || VIEWS.overview).render(...route.args);
  view.replaceChildren(node);
  renderDrawer();
}

function renderHeader(route) {
  const sel = document.getElementById("case-select");
  sel.replaceChildren(...S.cases.map(c => h("option", { value: c, selected: c === S.caseId }, c)));
  sel.onchange = () => go(`#/${sel.value}/overview`);

  const lastRoom = route.view === "room" || route.view === "flow" ? route.args[0] : undefined;
  document.getElementById("tabs").replaceChildren(...Object.entries(VIEWS).map(([key, v]) =>
    h("a", { href: routeTo(key, (key === "room" || key === "flow") ? lastRoom : undefined), class: key === route.view ? "active" : "" }, v.label)));
  setStatus(S.status);
}

let searchTimer;
document.getElementById("search").addEventListener("input", e => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => go(routeTo("search", encodeURIComponent(e.target.value))), 250);
});
document.addEventListener("keydown", e => {
  if (e.key === "Escape" && drawerClueId) closeDrawer();
});
window.addEventListener("hashchange", render);
window.addEventListener("beforeunload", e => { if (S.dirty.size) { flushSaves(); e.preventDefault(); } });

// Always re-render: the DOM is bound to the old objects, so leaving it up would
// send further typing into data that is no longer saved.
S.onExternalReload = () => render();

(async () => {
  await loadCases();
  await render();
  setInterval(pollVersions, 1500);
})();
