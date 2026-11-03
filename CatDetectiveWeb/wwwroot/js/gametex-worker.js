// Texture worker: downloads the game's jpg/webp textures and decodes them to
// straight-alpha RGBA OFF the main thread, in priority order. The main page
// (gametex.js) hands the pixels to the game, which builds Texture2Ds from them.
//
// Messages in : {t:'init', base, items:[{a,f,w,h,s}]}
//               {t:'prio', list:[prefix...]}   (front of the queue, in order)
//               {t:'credit', bytes}            (main freed decoded bytes)
// Messages out: {t:'loaded', a}                (downloaded, not decoded yet)
//               {t:'pixels', a, w, h, buf}     (transferable RGBA)
//               {t:'fail', a, err}
'use strict';

const CONCURRENT_DOWNLOADS = 4;
let items = [];           // manifest order = default priority
let base = '';
let prio = [];            // prefixes, most urgent first
// Until the game sends its first priority list (right after the .NET runtime
// boots), only the menu/UI art downloads - on a slow connection the runtime
// download must not share its bandwidth with art for rooms nobody is in yet.
let bootOnly = true;
let credit = 0;           // decoded bytes the main thread can still hold
let decoding = false;
let active = 0;

function rank(it) {
  for (let i = 0; i < prio.length; i++)
    if (it.a.startsWith(prio[i])) return i;
  return prio.length + it.order / 100000;
}

function pick(filter) {
  let best = null, br = Infinity;
  for (const it of items) {
    if (!filter(it)) continue;
    const r = rank(it);
    if (r < br) { br = r; best = it; }
  }
  return best;
}

function pumpDownloads() {
  while (active < CONCURRENT_DOWNLOADS) {
    const it = pick(x => x.state === 'new' && (!bootOnly || x.a.startsWith('Shared/')));
    if (!it) return;
    it.state = 'downloading';
    active++;
    fetch(base + it.f)
      .then(r => { if (!r.ok) throw new Error('HTTP ' + r.status); return r.blob(); })
      .then(b => { it.blob = b; it.state = 'downloaded'; postMessage({ t: 'loaded', a: it.a }); })
      .catch(e => { it.state = 'failed'; postMessage({ t: 'fail', a: it.a, err: String(e) }); })
      .finally(() => { active--; pumpDownloads(); pumpDecode(); });
  }
}

async function pumpDecode() {
  if (decoding) return;
  decoding = true;
  try {
    for (;;) {
      // Decode strictly in priority order: never let a low-priority texture
      // that happens to be downloaded jump ahead of an urgent one in flight.
      const next = pick(x => x.state !== 'decoded' && x.state !== 'failed');
      if (!next || next.state !== 'downloaded') break;
      const bytes = next.w * next.h * 4;
      if (bytes > credit && credit < MAX_CREDIT) break;   // main thread is full; wait for credit
      try {
        const bmp = await createImageBitmap(next.blob, { premultiplyAlpha: 'none', colorSpaceConversion: 'none' });
        const cv = new OffscreenCanvas(bmp.width, bmp.height);
        const cx = cv.getContext('2d', { willReadFrequently: true });
        cx.drawImage(bmp, 0, 0);
        bmp.close();
        const data = cx.getImageData(0, 0, cv.width, cv.height).data;
        next.state = 'decoded';
        next.blob = null;
        credit -= data.byteLength;
        postMessage({ t: 'pixels', a: next.a, w: cv.width, h: cv.height, buf: data.buffer }, [data.buffer]);
      } catch (e) {
        next.state = 'failed';
        postMessage({ t: 'fail', a: next.a, err: String(e) });
      }
    }
  } finally {
    decoding = false;
  }
}

let MAX_CREDIT = 0;
onmessage = (ev) => {
  const m = ev.data;
  if (m.t === 'init') {
    base = m.base;
    items = m.items.map((x, i) => ({ ...x, order: i, state: 'new', blob: null }));
    credit = MAX_CREDIT = m.credit;
    pumpDownloads();
  } else if (m.t === 'prio') {
    bootOnly = false;
    prio = m.list.concat(prio.filter(p => !m.list.includes(p)));
    pumpDownloads();
    pumpDecode();
  } else if (m.t === 'credit') {
    credit = Math.min(MAX_CREDIT, credit + m.bytes);
    pumpDecode();
  }
};
