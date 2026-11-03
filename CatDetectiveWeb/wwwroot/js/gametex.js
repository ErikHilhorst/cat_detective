// Main-thread side of the texture pipeline (see gametex-worker.js).
// Starts at page load - in parallel with the .NET runtime download - so the
// menu art is usually decoded before the game even boots.
//
// The game (Systems/WebTextures.cs) calls these synchronously via JSImport:
//   state()            'pending' | 'active' | 'none' (no manifest / unsupported -> .xnb)
//   prioritize(csv)    asset-name prefixes to fetch + decode first, in order
//   progress(csv)      "done,total" - done = handed to the game (or failed)
//   readyCount(csv)    "done,total" - done = decoded and waiting (or taken/failed)
//   info(name)         "w,h" when decoded pixels are waiting, else ""
//   take(name, view)   copies the RGBA into the game's buffer, frees ours
//   nextReady()        any decoded asset waiting to be taken, or ""
//   drop(name)         the game loaded it another way; discard ours
'use strict';
(function () {
  const MAX_HELD_BYTES = 160 * 1024 * 1024;   // decoded RGBA waiting for the game
  const status = new Map();    // name -> new | loaded | ready | taken | failed
  const pixels = new Map();    // name -> Uint8ClampedArray
  let names = [];              // manifest order
  let worker = null;
  let state = 'pending';

  const api = {
    state: () => state,          // pending (manifest in flight) | active | none
    prioritize(csv) {
      const list = csv.split(',').filter(Boolean);
      if (worker && list.length) worker.postMessage({ t: 'prio', list });
    },
    progress: (csv) => count(csv, s => s === 'taken' || s === 'failed'),
    readyCount: (csv) => count(csv, s => s === 'ready' || s === 'taken' || s === 'failed'),
    info(name) {
      const p = pixels.get(name);
      return p ? `${p.w},${p.h}` : '';
    },
    take(name, view) {
      const p = pixels.get(name);
      if (!p) return;
      view.set(p.data);
      release(name, p);
    },
    nextReady() {
      for (const k of pixels.keys()) return k;
      return '';
    },
    drop(name) {
      const p = pixels.get(name);
      if (p) release(name, p);
      else if (status.has(name)) status.set(name, 'taken');
    },
  };

  function release(name, p) {
    pixels.delete(name);
    status.set(name, 'taken');
    worker.postMessage({ t: 'credit', bytes: p.data.byteLength });
  }

  function count(csv, done) {
    const pre = csv.split(',').filter(Boolean);
    let d = 0, t = 0;
    for (const n of names) {
      if (pre.length && !pre.some(p => n.startsWith(p))) continue;
      t++;
      if (done(status.get(n))) d++;
    }
    return `${d},${t}`;
  }

  globalThis.gameTex = api;

  if (typeof Worker === 'undefined' || typeof OffscreenCanvas === 'undefined' ||
      typeof createImageBitmap === 'undefined') {
    state = 'none';
    console.warn('[gameTex] OffscreenCanvas/createImageBitmap unavailable - falling back to .xnb loading');
    return;
  }

  const base = new URL('Content/', document.baseURI).href;
  fetch(base + 'textures.json')
    .then(r => { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
    .then(items => {
      names = items.map(i => i.a);
      for (const n of names) status.set(n, 'new');
      worker = new Worker('js/gametex-worker.js');
      worker.onmessage = (ev) => {
        const m = ev.data;
        if (m.t === 'loaded') status.set(m.a, 'loaded');
        else if (m.t === 'pixels') {
          if (status.get(m.a) === 'taken') {          // game already has it (xnb fallback)
            worker.postMessage({ t: 'credit', bytes: m.buf.byteLength });
            return;
          }
          pixels.set(m.a, { w: m.w, h: m.h, data: new Uint8Array(m.buf) });
          status.set(m.a, 'ready');
        } else if (m.t === 'fail') {
          status.set(m.a, 'failed');
          console.warn('[gameTex] ' + m.a + ': ' + m.err);
        }
      };
      worker.onerror = (e) => console.error('[gameTex] worker error', e.message);
      worker.postMessage({ t: 'init', base, items, credit: MAX_HELD_BYTES });
      state = 'active';
      console.log(`[gameTex] streaming ${items.length} textures`);
    })
    .catch(e => {
      state = 'none';
      console.warn('[gameTex] no texture manifest (' + e + ') - falling back to .xnb loading');
    });
})();
