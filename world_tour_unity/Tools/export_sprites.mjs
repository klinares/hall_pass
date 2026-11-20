// Sprite export: renders the web game's SVG art (world_tour/art/*.js, read only) to transparent PNGs
// for Unity, plus a manifest. Plain Node plus the Playwright install already in
// world_tour/tests/e2e/node_modules (nothing is installed or changed there).
//
//   node Tools/export_sprites.mjs [--out <dir>] [--fonts <dir>] [--sheet <png>] [--theme <id>] [--dry]
//
//   --out    sprite root, default <project>/Assets/Art/Sprites (groups go in <out>/<group>/)
//   --fonts  folder holding CaveatBrush/CaveatBrush-Regular.ttf and CourierPrime/CourierPrime-{Regular,Bold}.ttf,
//            default <project>/Assets/Fonts. Sticker and venue text is drawn in these faces, so they must exist.
//   --sheet  also write a contact sheet of every sprite to this PNG (review aid, not used by the game)
//   --theme  tokens.css theme: tour-pink (default) or peach-navy (needs --out, so it can't overwrite the default set)
//   --dry    render and check everything, write nothing (prints the summary)
//
// Sizes: every piece is rendered at 2x the largest size it is shown at in a 1920x1080 window
// (measured in the web game; the table is SIZES below). The same inputs give byte-identical PNGs and
// manifest on the same machine and Playwright build. PNGs whose bytes did not change are not rewritten.
//
// Glyphs: symbols Caveat Brush and Courier Prime lack (six-point star, stars, heart, arrows, play triangle,
// rounded square) are drawn here as plain geometry sized to a Courier Prime line, into glyphs/, for a TMP
// sprite asset.
//
// Decor: the scrapbook pieces the web draws in CSS (torn paper, washi tape, ticket stub, stamp, stage desk,
// speech bubble, halftone corners) are rendered from world_tour/css at device scale 2 into decor/, with
// Unity slice borders where a piece stretches. The map route is not exported: the manifest carries its curves.
//
// The dog is exported in layers (see DOG below). The web draws its white sticker border and hard
// shadow with one SVG filter over the whole dog, so each layer comes as three sprites: <name> (the art),
// <name>_ink (black ring + offset shadow) and <name>_pad (white border). Stack all _ink, then all _pad,
// then the art layers in order (manifest dog.stacks lists the full order for every pose and outfit).
// A composite check rebuilds every pose and outfit from the layers and compares it with the web render.
//
// Exit codes: 0 ok, 1 a check failed (nothing written), 2 the runner broke. Browser and server are
// always closed.
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const project = path.resolve(here, '..');                  // world_tour_unity/
const repo = path.resolve(project, '..');                  // repo root
const webRoot = path.join(repo, 'world_tour');
const requireFromE2E = createRequire(path.join(webRoot, 'tests', 'e2e', 'package.json'));
const { chromium } = requireFromE2E('playwright');
const pwVersion = requireFromE2E('playwright/package.json').version;

// ---- options ----
const argv = process.argv.slice(2);
const opt = (name, def) => { const i = argv.indexOf(name); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : def; };
const outDir = path.resolve(opt('--out', path.join(project, 'Assets', 'Art', 'Sprites')));
const fontsDir = path.resolve(opt('--fonts', path.join(project, 'Assets', 'Fonts')));
const sheetPath = opt('--sheet', '') ? path.resolve(opt('--sheet', '')) : '';
const theme = opt('--theme', 'tour-pink');
const dry = argv.includes('--dry');
if (!['tour-pink', 'peach-navy'].includes(theme)) { console.error(`unknown --theme ${theme}`); process.exit(2); }
if (theme !== 'tour-pink' && !argv.includes('--out') && !dry) { console.error('a theme other than tour-pink needs its own --out, so it never overwrites the default sprites'); process.exit(2); }
const outReal = path.resolve(outDir);
if (outReal === webRoot || outReal.startsWith(webRoot + path.sep) || outReal.startsWith(path.join(repo, 'spelling_4_U'))) {
  console.error('refusing to write inside world_tour/ or spelling_4_U/'); process.exit(2);
}

// ---- sizes: largest CSS size at 1920x1080 (measured in the web game), rendered at 2x ----
// box: the web svg viewBox in art units; px: the rendered size of that box.
const SIZES = {
  sticker:    { box: [-10, -10, 124, 124], css: 170, px: [340, 340], where: 'studio record (vinyl) 170px; backstage mirror 120px; journal 104px' },
  sticker_sm: { box: [-10, -10, 124, 124], css: 84,  px: [168, 168], where: 'tour bus on the map 84px; map decor 78px; desk lights 70px' },
  venue:      { box: [-6, -6, 132, 112],   css: 272, px: [561, 476], where: 'venue page poster 272x231px; map 190px (4.25 px per unit)' },
  icon:       { box: [0, 0, 24, 24],       css: 24,  px: [48, 48],   where: 'sound and quit buttons 24px' },
  scribble:   { box: [0, 0, 200, 20],      css: 0,   px: [400, 40],  where: 'stretched under text (about 80x7px); exported at its viewBox x2' },
  dog:        { box: [0, 0, 160, 160],     css: 190, px: [380, 380], where: 'show coach and backstage mirror 190px' }
};
// glyphs: 128 px per em; an 80 x 92 px box whose baseline is 84 px below its top (0.656 em up, 0.06 em down)
const GLYPH_EM = 128, GLYPH_PX = [80, 92], GLYPH_TOP_PX = 84;
const PAD = 64;               // transparent room around the box while measuring (px)
const VIEW = { width: 2400, height: 1600 };

// ---- tiny static server: world_tour/ (read only) and the fonts folder ----
const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.ttf': 'font/ttf' };
function harnessHtml() {
  return `<!doctype html><html><head><meta charset="utf-8">
<link rel="stylesheet" href="/world_tour/css/tokens.css">
<style>
@font-face{font-family:'Caveat Brush';src:url('/__fonts/CaveatBrush/CaveatBrush-Regular.ttf') format('truetype');font-weight:400}
@font-face{font-family:'Courier Prime';src:url('/__fonts/CourierPrime/CourierPrime-Regular.ttf') format('truetype');font-weight:400}
@font-face{font-family:'Courier Prime';src:url('/__fonts/CourierPrime/CourierPrime-Bold.ttf') format('truetype');font-weight:700}
html,body{margin:0;padding:0;background:transparent}
svg{display:block;overflow:visible}
#stage{position:absolute;left:${PAD}px;top:${PAD}px}
#stage>svg{position:absolute;left:0;top:0}
.scribble>svg path{fill:none;stroke:#fff;stroke-width:5;stroke-linecap:round}
</style></head><body><div id="stage"></div>
<script type="module" src="/__export/harness.js"></script></body></html>`;
}

// The decor page: the web game's own stylesheets, nothing else on it.
function decorHtml() {
  const css = ['tokens', 'base', 'paper', 'components', 'screens'].map(n => `<link rel="stylesheet" href="/world_tour/css/${n}.css">`).join('');
  return `<!doctype html><html class="b21-nohalf"><head><meta charset="utf-8">${css}
<style>
html,body{background:transparent!important}
html.b21-nohalf body::before,html.b21-nohalf body::after{display:none!important}
#dstage{position:absolute;left:${PAD}px;top:${PAD}px}
.b21-notail::before{display:none!important}
</style></head><body><div id="dstage"></div></body></html>`;
}

// Runs in the page. Exposes window.B21 for the Node side.
function harnessJs() {
  return `
import { mountSprite, spriteMarkup } from '/world_tour/art/sprite.js';
import { sticker, icon, venueArt, scribble } from '/world_tour/art/sticker.js';
import { dog } from '/world_tour/art/dog.js';
import { LAYOUTS, mapSVG, curve, routePoints } from '/world_tour/art/map.js';
import { STICKER_IDS, VENUE_ART } from '/world_tour/art/sprite.js';
import { SHOP } from '/world_tour/data/shop.js';
import { VENUES } from '/world_tour/data/venues.js';

const stage = document.getElementById('stage');
const NECK = ['bandana', 'collar'];

function addLayerFilters() {
  const defs = document.querySelector('#wt-sprite defs');
  for (const base of ['wt-stk', 'wt-stk-sm']) {
    const src = document.getElementById(base);
    for (const role of ['ink', 'pad']) {
      const f = src.cloneNode(true);
      f.id = 'b21-' + role + '-' + base;
      // the layer is a fraction of the dog, so the default -30% region would clip its ring
      f.setAttribute('filterUnits', 'userSpaceOnUse');
      f.setAttribute('x', '-200'); f.setAttribute('y', '-200'); f.setAttribute('width', '2000'); f.setAttribute('height', '2000');
      const keep = role === 'ink' ? ['shadow', 'ringc'] : ['padc'];
      for (const n of [...f.querySelectorAll('feMergeNode')]) if (!keep.includes(n.getAttribute('in'))) n.remove();
      defs.appendChild(f);
    }
  }
}

function size(svg, w, h) { svg.style.width = w + 'px'; svg.style.height = h + 'px'; }

function put(html, w, h, wrapStyle) {
  stage.innerHTML = '';
  const host = document.createElement('div');
  if (wrapStyle) host.setAttribute('style', wrapStyle);
  host.innerHTML = html;
  stage.appendChild(host);
  const svg = host.querySelector('svg');
  size(svg, w, h);
  return svg;
}

// ---- dog: split one render into the parts dog.js draws, in its own order ----
function dogParts(svg, pose, outfit) {
  const body = svg.querySelector('g.body');
  const kids = [...body.children];
  const need = (ok, msg) => { if (!ok) throw new Error('dog.js structure changed: ' + msg + ' (' + pose + '/' + outfit + ')'); };
  if (pose === 'sleepy') {
    need(kids.length === 8 && kids[0].classList.contains('tail') && kids[7].classList.contains('zzz'), 'lying children');
    const z = [...kids[7].children];
    need(z.length === 3, 'three z');
    return { order: ['tail', 'body', 'head', 'paws', 'z1', 'z2', 'z3'],
      parts: { tail: [kids[0]], body: kids.slice(1, 4), head: [kids[4]], paws: kids.slice(5, 7), z1: [z[0]], z2: [z[1]], z3: [z[2]] } };
  }
  need(kids[0].classList.contains('tail'), 'tail first');
  const hi = kids.findIndex(k => k.classList.contains('head'));
  need(hi >= 8, 'head index');
  const between = kids.slice(8, hi), after = kids.slice(hi + 1);
  const preArms = pose === 'cheer' ? 6 : 0;
  const neckN = NECK.includes(outfit) ? between.length - preArms : 0;
  need(neckN === (outfit === 'bandana' ? 3 : outfit === 'collar' ? 6 : 0), 'neck outfit nodes ' + neckN);
  need(between.length - neckN === preArms, 'arms before head');
  let arms = between.slice(neckN), fx = after;
  if (pose === 'think') { need(after.length === 6, 'think arms + thought'); arms = after.slice(0, 3); fx = after.slice(3); }
  else need(after.length === (pose === 'cheer' ? 3 : 0), 'fx after head');
  const head = kids[hi];
  const plain = document.createElement('div');
  plain.innerHTML = dog(pose, { outfit: '' });
  const baseN = plain.querySelector('g.head').children.length;
  const hk = [...head.children];
  const headOutfit = hk.slice(baseN);
  need((NECK.includes(outfit) || !outfit) ? headOutfit.length === 0 : headOutfit.length > 0, 'head outfit nodes');
  const lids = hk.slice(0, baseN).filter(e => e.classList.contains('lid'));
  const headArt = hk.slice(0, baseN).filter(e => !e.classList.contains('lid'));
  const parts = { tail: [kids[0]], body: kids.slice(1, 8), arms, head: headArt, lids, fx, outfit: NECK.includes(outfit) ? between.slice(0, neckN) : headOutfit };
  const order = ['tail', 'body'];
  if (NECK.includes(outfit)) order.push('outfit');
  if (pose !== 'think' && arms.length) order.push('arms');
  order.push('head');
  if (lids.length) order.push('lids');
  if (outfit && !NECK.includes(outfit)) order.push('outfit');
  if (pose === 'think') order.push('arms');
  if (fx.length) order.push('fx');
  return { order, parts };
}

function keepOnly(root, keep) {
  const inside = n => { for (const k of keep) if (n === k || n.contains(k)) return true; return false; };
  const walk = el => { for (const c of [...el.children]) { if (keep.has(c)) continue; if (inside(c)) walk(c); else c.remove(); } };
  walk(root);
}

window.B21 = {
  async init(themeId) {
    if (themeId !== 'tour-pink') document.documentElement.dataset.theme = themeId;
    mountSprite();
    addLayerFilters();
    const faces = ['24px "Caveat Brush"', '400 8px "Courier Prime"', '700 8px "Courier Prime"'];
    await Promise.all(faces.map(f => document.fonts.load(f)));
    // document.fonts.check() is true for a family that has no face at all, so look at each face instead
    const loaded = [...document.fonts].filter(ff => ff.status === 'loaded').map(ff => ff.family.replace(/["']/g, '') + ' ' + ff.weight);
    const want = ['Caveat Brush 400', 'Courier Prime 400', 'Courier Prime 700'];
    const missing = want.filter(w => !loaded.includes(w));
    if (missing.length) throw new Error('fonts not loaded: ' + missing.join(', '));
    const icons = [...new DOMParser().parseFromString(spriteMarkup(), 'image/svg+xml').querySelectorAll('symbol')].map(s => s.id).filter(id => id.startsWith('i-'));
    const skins = SHOP.filter(i => i.slot === 'guitar').map(i => ({ id: i.id.split(':')[1], color: i.color }));
    const outfits = SHOP.filter(i => i.slot === 'dog').map(i => i.id.split(':')[1]).filter(o => o !== 'none');
    const L = LAYOUTS.landscape;
    const styles = [...new DOMParser().parseFromString(mapSVG(L, 1), 'image/svg+xml').querySelectorAll('path')].map(p => p.getAttribute('style'));
    const route = { layout: L, curveAll: curve(routePoints(L)), curveDone: L.pts.map((p, i) => i > 0 ? curve(routePoints(L, i)) : ''),
      strokes: { under: styles[0], done: styles[1], dash: styles[2] } };
    return { stickers: STICKER_IDS, venues: VENUE_ART, venueOrder: VENUES.map(v => ({ id: v.id, art: v.art })), icons, skins, outfits, route };
  },
  sticker(id, small, w, h, gtr) { put(sticker(id, { small }), w, h, gtr ? '--gtr:' + gtr : ''); },
  venue(id, w, h) { put(venueArt(id), w, h); },
  icon(id, w, h) { put(icon(id), w, h, 'color:#fff'); },
  raw(html, w, h) { put(html, w, h); },
  scribble(w, h) {
    stage.innerHTML = scribble('');
    const s = stage.querySelector('.scribble'); s.style.position = 'absolute'; s.style.left = '0'; s.style.top = '0';
    const svg = s.querySelector('svg'); svg.style.left = '0'; svg.style.bottom = 'auto'; svg.style.top = '0'; size(svg, w, h);
  },
  // part '' renders the whole dog as the web does (lids hidden, as the CSS keeps them at scaleY(0))
  dog(pose, outfit, part, role, w, h) {
    const svg = put(dog(pose, { outfit }), w, h);
    const g = svg.querySelector('g[filter]');
    if (!part) { svg.querySelectorAll('.lid').forEach(e => e.remove()); return null; }
    const { order, parts } = dogParts(svg, pose, outfit);
    if (!parts[part] || !parts[part].length) throw new Error('no dog part ' + part + ' in ' + pose + '/' + outfit);
    keepOnly(svg.querySelector('g.body'), new Set(parts[part]));
    if (role === 'art') g.removeAttribute('filter'); else g.setAttribute('filter', 'url(#b21-' + role + '-wt-stk)');
    return order;
  },
  dogOrder(pose, outfit) { const host = document.createElement('div'); host.innerHTML = dog(pose, { outfit }); return dogParts(host.firstElementChild, pose, outfit).order; },
  // alpha bounds of a PNG (base64); returns [x0, y0, x1, y1] exclusive, or null when empty
  async bounds(b64) {
    const img = await createImageBitmap(new Blob([Uint8Array.from(atob(b64), c => c.charCodeAt(0))], { type: 'image/png' }), { premultiplyAlpha: 'none', colorSpaceConversion: 'none' });
    const c = new OffscreenCanvas(img.width, img.height), x = c.getContext('2d');
    x.drawImage(img, 0, 0);
    const d = x.getImageData(0, 0, img.width, img.height).data;
    let x0 = img.width, y0 = img.height, x1 = -1, y1 = -1;
    for (let y = 0; y < img.height; y++) for (let i = 0; i < img.width; i++) if (d[(y * img.width + i) * 4 + 3]) { if (i < x0) x0 = i; if (i > x1) x1 = i; if (y < y0) y0 = y; if (y > y1) y1 = y; }
    return x1 < 0 ? null : [x0, y0, x1 + 1, y1 + 1];
  },
  // stack layer PNGs at offsets and compare with a reference PNG; both in the same frame
  async compare(refB64, frame, layers) {
    const load = b64 => createImageBitmap(new Blob([Uint8Array.from(atob(b64), c => c.charCodeAt(0))], { type: 'image/png' }), { premultiplyAlpha: 'none', colorSpaceConversion: 'none' });
    const W = frame[0], H = frame[1];
    const a = new OffscreenCanvas(W, H), ax = a.getContext('2d');
    for (const l of layers) ax.drawImage(await load(l.b64), l.x, l.y);
    const b = new OffscreenCanvas(W, H), bx = b.getContext('2d');
    bx.drawImage(await load(refB64), 0, 0);
    const da = ax.getImageData(0, 0, W, H).data, db = bx.getImageData(0, 0, W, H).data;
    let maxd = 0, over8 = 0, over32 = 0, lit = 0;
    for (let i = 0; i < da.length; i += 4) {
      // compare premultiplied colour so fully transparent pixels count as equal
      const aa = da[i + 3] / 255, ab = db[i + 3] / 255;
      let m = Math.abs(da[i + 3] - db[i + 3]);
      for (let k = 0; k < 3; k++) m = Math.max(m, Math.abs(da[i + k] * aa - db[i + k] * ab));
      if (db[i + 3] || da[i + 3]) lit++;
      if (m > maxd) maxd = m; if (m > 8) over8++; if (m > 32) over32++;
    }
    return { maxd: Math.round(maxd), over8, over32, lit };
  },
  async sheet(items, cols, cell) {
    const rows = Math.ceil(items.length / cols), W = cols * cell, H = rows * (cell + 18);
    const c = new OffscreenCanvas(W, H), x = c.getContext('2d');
    for (let r = 0; r < rows; r++) for (let q = 0; q < cols; q++) {
      for (let yy = 0; yy < cell; yy += 12) for (let xx = 0; xx < cell; xx += 12) {
        x.fillStyle = ((xx + yy) / 12) % 2 ? '#8c8c8c' : '#b4b4b4'; x.fillRect(q * cell + xx, r * (cell + 18) + yy, 12, 12);
      }
    }
    x.fillStyle = '#fff'; for (let r = 0; r < rows; r++) x.fillRect(0, r * (cell + 18) + cell, W, 18);
    x.font = '11px monospace'; x.fillStyle = '#000';
    for (let i = 0; i < items.length; i++) {
      const it = items[i], q = i % cols, r = Math.floor(i / cols);
      const img = await createImageBitmap(new Blob([Uint8Array.from(atob(it.b64), ch => ch.charCodeAt(0))], { type: 'image/png' }));
      const s = Math.min(1, (cell - 8) / Math.max(img.width, img.height));
      const w = img.width * s, h = img.height * s, ox = q * cell + (cell - w) / 2, oy = r * (cell + 18) + (cell - h) / 2;
      x.drawImage(img, ox, oy, w, h);
      x.strokeStyle = 'rgba(255,0,0,.55)'; x.lineWidth = 1; x.strokeRect(ox - .5, oy - .5, w + 1, h + 1);
      x.fillStyle = '#000'; x.fillText(it.label.slice(0, Math.floor(cell / 6.6)), q * cell + 2, r * (cell + 18) + cell + 13);
    }
    const blob = await c.convertToBlob({ type: 'image/png' });
    const buf = new Uint8Array(await blob.arrayBuffer());
    let s = ''; for (let i = 0; i < buf.length; i += 0x8000) s += String.fromCharCode.apply(null, buf.subarray(i, i + 0x8000));
    return btoa(s);
  }
};
window.B21_READY = true;
`;
}

function startServer() {
  const server = http.createServer((req, res) => {
    try {
      if (req.method !== 'GET') { res.writeHead(405); return res.end(); }
      const p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
      if (p === '/__export/harness.html') { res.writeHead(200, { 'content-type': 'text/html' }); return res.end(harnessHtml()); }
      if (p === '/__export/decor.html') { res.writeHead(200, { 'content-type': 'text/html' }); return res.end(decorHtml()); }
      if (p === '/__export/harness.js') { res.writeHead(200, { 'content-type': 'text/javascript' }); return res.end(harnessJs()); }
      let f = null;
      if (p.startsWith('/__fonts/')) f = path.join(fontsDir, p.slice('/__fonts/'.length));
      else if (p.startsWith('/world_tour/')) f = path.join(repo, p);
      const okRoot = f && (f.startsWith(fontsDir + path.sep) || f.startsWith(webRoot + path.sep));
      if (!okRoot || !fs.existsSync(f) || !fs.statSync(f).isFile()) { res.writeHead(404); return res.end(); }
      res.writeHead(200, { 'content-type': TYPES[path.extname(f)] || 'application/octet-stream' });
      fs.createReadStream(f).pipe(res);
    } catch (e) { res.writeHead(500); res.end(); }
  });
  return new Promise(r => server.listen(0, '127.0.0.1', () => r(server)));
}

// ---- helpers ----
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
const up4 = n => Math.ceil(n / 4) * 4;
const r6 = n => Math.round(n * 1e6) / 1e6;
const fail = [];
const log = (...a) => console.log(...a);

async function main() {
  for (const f of ['CaveatBrush/CaveatBrush-Regular.ttf', 'CourierPrime/CourierPrime-Regular.ttf', 'CourierPrime/CourierPrime-Bold.ttf']) {
    if (!fs.existsSync(path.join(fontsDir, f))) { console.error(`missing font ${path.join(fontsDir, f)} (use --fonts)`); process.exit(2); }
  }
  const server = await startServer();
  const base = `http://127.0.0.1:${server.address().port}`;
  let browser;
  try {
    browser = await chromium.launch();
    const page = await browser.newPage({ viewport: VIEW, deviceScaleFactor: 1 });
    const pageErrors = [];
    page.on('pageerror', e => pageErrors.push(e.message));
    page.on('console', m => { if (m.type() === 'error') pageErrors.push(m.text()); });
    page.on('request', r => { if (!r.url().startsWith(base)) pageErrors.push('outside request ' + r.url()); });
    await page.goto(base + '/__export/harness.html');
    await page.waitForFunction(() => window.B21_READY === true);
    const info = await page.evaluate(t => window.B21.init(t), theme);
    const chromeVersion = browser.version();

    const shot = async (clip) => page.screenshot({ clip, omitBackground: true, type: 'png' });
    const padded = (w, h) => ({ x: 0, y: 0, width: w + 2 * PAD, height: h + 2 * PAD });
    const boundsOf = async (buf) => page.evaluate(b => window.B21.bounds(b), buf.toString('base64'));

    // Render one staged piece: measure against the padded area, then crop.
    // Measure in the padded area, then the caller crops: tight around the content (multiple of 4 px).
    const measure = async (w, h, label) => {
      const buf = await shot(padded(w, h));
      const b = await boundsOf(buf);
      if (!b) { fail.push(`${label}: blank`); return null; }
      if (b[0] === 0 || b[1] === 0 || b[2] === w + 2 * PAD || b[3] === h + 2 * PAD) fail.push(`${label}: touches the measuring edge, raise PAD`);
      return { x0: b[0] - PAD, y0: b[1] - PAD, x1: b[2] - PAD, y1: b[3] - PAD };
    };

    const sprites = [];             // { name, group, kind, w, h, rect:[x,y,w,h] in box px, source, png, ... }
    const add = (s) => { if (sprites.some(o => o.name === s.name)) throw new Error('duplicate sprite ' + s.name); sprites.push(s); };

    // ---- box pieces: stickers, venues, icons, scribble ----
    const boxJobs = [];
    for (const id of info.stickers) {
      for (const small of [false, true]) boxJobs.push({ group: 'stickers', size: small ? 'sticker_sm' : 'sticker', name: id + (small ? '_sm' : ''), stage: () => page.evaluate(([i, s, w, h]) => window.B21.sticker(i, s, w, h, ''), [id, small, ...SIZES[small ? 'sticker_sm' : 'sticker'].px]),
        source: { module: 'world_tour/art/sticker.js', fn: 'sticker', args: [id, { small }] } });
    }
    for (const sk of info.skins) {
      if (sk.id === 'classic') continue;     // classic is var(--paper-deep), the default --gtr: same as plain 'guitar'
      for (const small of [false, true]) boxJobs.push({ group: 'stickers', size: small ? 'sticker_sm' : 'sticker', name: `guitar_${sk.id}` + (small ? '_sm' : ''), stage: () => page.evaluate(([s, w, h, c]) => window.B21.sticker('guitar', s, w, h, c), [small, ...SIZES[small ? 'sticker_sm' : 'sticker'].px, sk.color]),
        source: { module: 'world_tour/art/sticker.js', fn: 'sticker', args: ['guitar', { small }], css: { '--gtr': sk.color }, from: `world_tour/data/shop.js guitar:${sk.id}` } });
    }
    for (const id of info.venues) boxJobs.push({ group: 'map', size: 'venue', name: `venue_${id}`, stage: () => page.evaluate(([i, w, h]) => window.B21.venue(i, w, h), [id, ...SIZES.venue.px]),
      source: { module: 'world_tour/art/sticker.js', fn: 'venueArt', args: [id] } });
    for (const id of info.icons) boxJobs.push({ group: 'ui', size: 'icon', name: `icon_${id.slice(2)}`, tint: true, stage: () => page.evaluate(([i, w, h]) => window.B21.icon(i, w, h), [id, ...SIZES.icon.px]),
      source: { module: 'world_tour/art/sticker.js', fn: 'icon', args: [id], css: { color: '#fff' } } });
    boxJobs.push({ group: 'ui', size: 'scribble', name: 'scribble', tint: true, stage: () => page.evaluate(([w, h]) => window.B21.scribble(w, h), SIZES.scribble.px),
      source: { module: 'world_tour/art/sticker.js', fn: 'scribble', args: [''], css: { stroke: '#fff' }, note: 'preserveAspectRatio none: stretch to the text width' } });

    // Tight crop. The web sizes and rotates the svg box (CSS transform-origin is its centre), and the art
    // overflows that box (sticker() uses a 100-unit symbol in a 124-unit viewBox, so it draws 1.24x and
    // toward the bottom right; svg overflow is visible). So the pivot is the web box centre and 'box' says
    // where that box lies inside the sprite.
    for (const j of boxJobs) {
      await j.stage();
      const [w, h] = SIZES[j.size].px;
      const m = await measure(w, h, j.name);
      if (!m) continue;
      const cw = up4(m.x1 - m.x0), ch = up4(m.y1 - m.y0);
      await j.stage();
      const png = await shot({ x: PAD + m.x0, y: PAD + m.y0, width: cw, height: ch });
      add({ name: j.name, group: j.group, kind: j.size, w: cw, h: ch, png, tint: !!j.tint,
        box: [-m.x0, -m.y0, w, h], pivot: [r6((-m.x0 + w / 2) / cw), r6(1 - (-m.y0 + h / 2) / ch)], source: j.source });
    }

    // ---- dog layers ----
    const [DW, DH] = SIZES.dog.px;
    const POSES = ['idle', 'cheer', 'think', 'oops', 'sleepy'];
    const HEAD_OUTFITS = info.outfits.filter(o => !['bandana', 'collar'].includes(o));
    const dogName = (pose, outfit, part) => {
      if (pose === 'sleepy') return `dog_sleepy_${part}`;
      if (part === 'tail' || part === 'body') return `dog_${part}`;
      if (part === 'outfit') return HEAD_OUTFITS.includes(outfit) && (pose === 'think' || pose === 'oops') ? `dog_outfit_${outfit}_${pose}` : `dog_outfit_${outfit}`;
      return `dog_${part}_${pose}`;
    };
    const anchor = [DW / 2, DH];             // bottom centre of the web box: (80,160) in dog units
    const dogLayers = new Map();             // name+role -> sprite
    const renderDog = async (pose, outfit, part, role) => {
      const name = dogName(pose, outfit, part) + (role === 'art' ? '' : '_' + role);
      await page.evaluate(a => window.B21.dog(...a), [pose, outfit, part, role, DW, DH]);
      const m = await measure(DW, DH, name);
      if (!m) return null;
      const w = up4(m.x1 - m.x0), h = up4(m.y1 - m.y0);
      const png = await shot({ x: PAD + m.x0, y: PAD + m.y0, width: w, height: h });
      const prev = dogLayers.get(name);
      if (prev) {
        if (prev.offset[0] !== m.x0 || prev.offset[1] !== m.y0 || sha(prev.png) !== sha(png)) fail.push(`${name}: differs between poses (${prev.source.args[0]} vs ${pose})`);
        return prev;
      }
      const s = { name, group: 'dog', kind: 'dog', w, h, png, offset: [m.x0, m.y0],
        pivot: [r6((anchor[0] - m.x0) / w), r6((m.y0 + h - anchor[1]) / h)], role, part,
        source: { module: 'world_tour/art/dog.js', fn: 'dog', args: [pose, { outfit }], part, layer: role } };
      dogLayers.set(name, s); add(s);
      return s;
    };
    const stacks = {};
    for (const pose of POSES) {
      stacks[pose] = {};
      const outfits = pose === 'sleepy' ? [''] : ['', ...info.outfits];
      for (const outfit of outfits) {
        const order = await page.evaluate(a => window.B21.dogOrder(...a), [pose, outfit]);
        const ink = [], pad = [], art = [];
        for (const part of order) {
          if (part !== 'outfit' && outfit !== '') {
            // parts that don't depend on the outfit come from the plain render; just reuse the names
            const n = dogName(pose, '', part);
            if (!dogLayers.has(n)) fail.push(`missing ${n}`);
            art.push(n); if (part !== 'lids') { ink.push(n + '_ink'); pad.push(n + '_pad'); }
            continue;
          }
          const a = await renderDog(pose, outfit, part, 'art');
          if (a) art.push(a.name);
          if (part === 'lids') continue;          // inside the head: no ring of its own
          const i = await renderDog(pose, outfit, part, 'ink'), p = await renderDog(pose, outfit, part, 'pad');
          if (i) ink.push(i.name); if (p) pad.push(p.name);
        }
        stacks[pose][outfit || 'none'] = [...ink, ...pad, ...art];
      }
    }
    // extra check: shared names really are shared (body and tail from every sitting pose, neck outfits per pose)
    for (const pose of ['cheer', 'think', 'oops']) { await renderDog(pose, '', 'body', 'art'); await renderDog(pose, '', 'tail', 'ink'); }
    for (const o of ['bandana', 'collar']) for (const pose of ['cheer', 'think', 'oops']) await renderDog(pose, o, 'outfit', 'art');
    for (const o of HEAD_OUTFITS) await renderDog('cheer', o, 'outfit', 'pad');

    // composite check: every pose and outfit rebuilt from layers vs the web's own render (lids hidden)
    const composite = [];
    const frameClip = { x: PAD - 40, y: PAD - 40, width: DW + 80, height: DH + 80 };
    const byName = new Map(sprites.map(s => [s.name, s]));
    for (const pose of POSES) for (const [outfit, stack] of Object.entries(stacks[pose])) {
      await page.evaluate(a => window.B21.dog(...a), [pose, outfit === 'none' ? '' : outfit, '', '', DW, DH]);
      const ref = await shot(frameClip);
      const layers = stack.filter(n => !n.startsWith('dog_lids')).map(n => { const s = byName.get(n); return { b64: s.png.toString('base64'), x: s.offset[0] + 40, y: s.offset[1] + 40 }; });
      const r = await page.evaluate(a => window.B21.compare(...a), [ref.toString('base64'), [frameClip.width, frameClip.height], layers]);
      composite.push({ pose, outfit, ...r });
    }
    const worst = composite.reduce((a, b) => (b.over32 / b.lit > a.over32 / a.lit ? b : a));
    for (const c of composite) if (c.over32 / c.lit > 0.01) fail.push(`dog composite ${c.pose}/${c.outfit}: ${c.over32} of ${c.lit} pixels differ by more than 32`);

    // ---- glyphs: symbols the bundled fonts lack, drawn by the team as plain geometry (not from any font) ----
    // Em units, baseline at y = 0, y up, on Courier Prime's line: advance .6 em (its monospace advance),
    // cap height .58, maths axis (centre of its minus) .284, stroke .07 em (between its .066 minus bar and
    // .074 stem). Rendered white, so TextMeshPro tints them with the text colour.
    {
      const EM = GLYPH_EM;
      const stars = (cx, cy, R, r, n) => {
        const p = [];
        for (let i = 0; i < 2 * n; i++) { const a = Math.PI / 2 + i * Math.PI / n, q = i % 2 ? r : R; p.push(`${(cx + q * Math.cos(a)).toFixed(4)},${(cy + q * Math.sin(a)).toFixed(4)}`); }
        return p.join(' ');
      };
      const S = .07, line = `fill:none;stroke:#fff;stroke-width:${S};stroke-linecap:round;stroke-linejoin:round`, solid = 'fill:#fff;stroke:none';
      const arrowH = (tipX, tailX, y) => { const d = tipX > tailX ? -1 : 1; return `<path style="${line}" d="M${tailX} ${y}H${tipX}M${tipX + d * .16} ${y + .16}L${tipX} ${y}L${tipX + d * .16} ${y - .16}"/>`; };
      const arrowV = (tipY, tailY, x) => { const d = tipY > tailY ? -1 : 1; return `<path style="${line}" d="M${x} ${tailY}V${tipY}M${x - .16} ${tipY + d * .16}L${x} ${tipY}L${x + .16} ${tipY + d * .16}"/>`; };
      const GLYPHS = [
        [0x2736, 'six-pointed star', `<polygon style="${solid}" points="${stars(.3, .29, .25, .13, 6)}"/>`],
        [0x2605, 'star', `<polygon style="${solid};stroke:#fff;stroke-width:.02;stroke-linejoin:round" points="${stars(.3, .265, .275, .275 * .382, 5)}"/>`],
        [0x2606, 'outline star', `<polygon style="${line};stroke-width:${S * .85}" points="${stars(.3, .27, .255, .255 * .382, 5)}"/>`],
        [0x2665, 'heart', `<path style="${solid}" d="M.3 .03C.14 .16 .05 .27 .05 .39C.05 .5 .13 .57 .21 .57C.26 .57 .29 .54 .3 .5C.31 .54 .34 .57 .39 .57C.47 .57 .55 .5 .55 .39C.55 .27 .46 .16 .3 .03Z"/>`],
        [0x2190, 'left arrow', arrowH(.083 + S / 2, .517 - S / 2, .284)],
        [0x2191, 'up arrow', arrowV(.58 - S / 2, S / 2, .3)],
        [0x2192, 'right arrow', arrowH(.517 - S / 2, .083 + S / 2, .284)],
        [0x2193, 'down arrow', arrowV(S / 2, .58 - S / 2, .3)],
        [0x25B6, 'play triangle', `<polygon style="${solid};stroke:#fff;stroke-width:.03;stroke-linejoin:round" points=".1,.045 .1,.523 .53,.284"/>`],
        [0x25A2, 'rounded hollow square', `<rect style="${line}" x=".07" y=".054" width=".46" height=".46" rx=".07"/>`]
      ];
      const [GW, GH] = GLYPH_PX, top = GLYPH_TOP_PX;
      for (const [code, what, body] of GLYPHS) {
        const name = 'u' + code.toString(16).padStart(4, '0');
        const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${GW / EM} ${GH / EM}"><g transform="matrix(1 0 0 -1 0 ${top / EM})">${body}</g></svg>`;
        await page.evaluate(([h, w, hh]) => window.B21.raw(h, w, hh), [svg, GW, GH]);
        const m = await measure(GW, GH, name);
        if (!m) continue;
        if (m.x0 < 1 || m.y0 < 1 || m.x1 > GW - 1 || m.y1 > GH - 1) fail.push(`${name}: touches its glyph box`);
        const png = await shot({ x: PAD, y: PAD, width: GW, height: GH });
        add({ name, group: 'glyphs', kind: 'glyph', w: GW, h: GH, png, tint: true, pivot: [0, r6((GH - top) / GH)],
          glyph: { unicode: code, emPx: EM, width: GW, height: GH, bearingX: 0, bearingY: top, advance: r6(.6 * EM) },
          source: { drawn: 'team geometry in Tools/export_sprites.mjs (GLYPHS)', what, svg: body } });
      }
    }

    // ---- decor: the scrapbook pieces the web draws with CSS (world_tour/css/*.css), rendered from the
    // game's own classes at device scale 2 ----
    const dpage = await browser.newPage({ viewport: VIEW, deviceScaleFactor: 2 });
    dpage.on('pageerror', e => pageErrors.push('decor: ' + e.message));
    dpage.on('console', m => { if (m.type() === 'error') pageErrors.push('decor: ' + m.text()); });
    dpage.on('request', r => { if (!r.url().startsWith(base)) pageErrors.push('outside request ' + r.url()); });
    await dpage.goto(base + '/__export/decor.html');
    if (theme !== 'tour-pink') await dpage.evaluate(t => { document.documentElement.dataset.theme = t; }, theme);
    const dstage = (html, half = false) => dpage.evaluate(([h, f]) => {
      document.documentElement.classList.toggle('b21-nohalf', !f);
      const st = document.getElementById('dstage'); st.innerHTML = h;
      const el = st.querySelector('[data-b21]');
      if (!el) return null;
      const r = el.getBoundingClientRect(), o = st.getBoundingClientRect();
      return [r.left - o.left, r.top - o.top, r.width, r.height];
    }, [html, half]);
    const dshot = clip => dpage.screenshot({ clip, omitBackground: true, type: 'png' });
    const dbounds = async buf => page.evaluate(b => window.B21.bounds(b), buf.toString('base64'));
    // A piece: html with one [data-b21] element (the web box). crop = that box grown by 'grow' css px
    // [left, top, right, bottom] (room for shadows and bites). 'inset' = slice borders in css px measured
    // inward from the web box edges [left, top, right, bottom]; the shadow or bite overhang is added on top.
    const T = 'style="position:absolute;left:0;top:0;right:auto;bottom:auto;margin:0;transform:none';
    const torn = (cls, L, tileX, tileY) => {
      const W = L + tileX + 8, H = 20 + tileY + 12;
      return { html: `<div class="scrap" style="width:${W}px"><div data-b21 class="${cls}" style="width:${W}px;height:${H}px;padding:0"></div></div>`,
        grow: [0, 0, 6, 6], inset: [L, 20, 8, 12], mode: 'tiled', tile: [tileX, tileY] };
    };
    const D = [
      { name: 'paper_torn', ...torn('torn', 8, 60, 20), note: '.scrap > .torn: white paper, torn top and bottom, 6px hard shadow. Draw Tiled: the edge repeats every 60 css px' },
      { name: 'paper_lined', ...torn('torn lined', 40, 60, 32), note: '.torn.lined: notebook lines every 32 css px and the margin line at 34-36px. Draw Tiled' },
      { name: 'paper_map', ...torn('torn map', 8, 420, 28), note: '.torn.map: the map paper with its 28px grid (420 = lcm of 28 and the 60px edge). Draw Tiled' },
      { name: 'tape_a', html: `<span data-b21 class="tape" ${T}"></span>`, grow: [0, 0, 0, 0], note: '.tape: 96x26 fixed in the web, placed with .tl (top -10, left 14, rotate -8), .tr (top -10, right 14, rotate 7), .tc (top -12, centred, rotate -3). The web multiplies it over what is below (mix-blend-mode multiply); this PNG is the plain colour' },
      { name: 'tape_b', html: `<span data-b21 class="tape b" ${T}"></span>`, grow: [0, 0, 0, 0], note: '.tape.b: opacity .9 is baked in' },
      { name: 'tape_c', html: `<span data-b21 class="tape c" ${T}"></span>`, grow: [0, 0, 0, 0], note: '.tape.c: dotted pink tape' },
      { name: 'ticket_stub', html: `<div class="scrap ticketwrap" style="margin:0;width:320px"><div data-b21 class="ticket" style="height:200px"><div class="tk-main"></div><div class="tk-stub perf"></div></div></div>`,
        grow: [0, 16, 6, 22], inset: [12, 20, 106, 20], mode: 'sliced', note: '.ticket with .tk-stub.perf: the 88px stub with its dashed seam and paper-coloured bites; 6px hard shadow. Draw Sliced. Text and the per-show barcode (ticket.js barcode()) are not in it' },
      { name: 'stamp', html: `<span data-b21 class="stamp" ${T}"></span>`, grow: [0, 0, 0, 0], note: '.stamp without its text ("sold out" in the hand font, accent-2); the web rotates it -14 deg' },
      { name: 'desk', html: `<div data-b21 class="desk" style="margin:0;transform:none;width:752px;height:274px"></div>`, grow: [0, 0, 6, 6], inset: [22, 22, 22, 22], mode: 'sliced',
        note: '.desk at its remix size (752x274 css at 1920). Other heights stretch its dots and gradient; for those use desk_plain plus desk_dots tiled inside it. The web tilts it -.5 deg' },
      { name: 'desk_plain', html: `<div data-b21 class="desk" style="margin:0;transform:none;width:752px;height:274px;background-size:0 0,auto"></div>`, grow: [0, 0, 6, 6], inset: [22, 22, 22, 22], mode: 'sliced',
        note: '.desk with only its gradient layer (the dot layer sized to 0). Sliced; the gradient runs top to bottom, ink from 60%' },
      { name: 'desk_dots', html: `<div data-b21 class="desk" style="margin:0;transform:none;width:9px;height:9px;padding:0;border:0;border-radius:0;box-shadow:none;background-size:9px 9px,0 0"></div>`, grow: [0, 0, 0, 0], mode: 'tiled-whole',
        note: 'the .desk dot layer as one 9px tile (white at 13%); tile it inside desk_plain, clipped to its rounded inside' },
      { name: 'bubble', html: `<div class="stagewrap" style="display:block"><div class="coach" style="display:block;margin:0"><div data-b21 class="bubble b21-notail" style="width:120px;height:80px;min-height:0;padding:0"></div></div></div>`,
        grow: [0, 0, 3, 3], inset: [26, 26, 26, 26], mode: 'sliced', note: 'the coach speech bubble at 900px and wider (both coach bubbles at 1920 share this shape). Sliced; add bubble_tail on top' },
      { name: 'bubble_tail', html: `<div class="stagewrap" style="display:block"><div class="coach" style="display:block;margin:0"><div data-b21 class="bubble" style="width:120px;height:80px;min-height:0;padding:0;background:transparent;border-color:transparent;box-shadow:none"></div></div></div>`,
        tight: true, note: 'the .bubble::before tail; offset is from the bubble bottom centre (show coach: left 50%; home coach: left 48% of the bubble width). Draw after bubble: it covers the bubble border where they meet' }
    ];
    for (const d of D) {
      const box = await dstage(d.html);
      if (!box) { fail.push(`${d.name}: no element`); continue; }
      const [bx, by, bw, bh] = box;
      const all = await dshot({ x: 0, y: 0, width: Math.ceil(bx + bw) + 3 * PAD, height: Math.ceil(by + bh) + 3 * PAD });
      const b = await dbounds(all);
      if (!b) { fail.push(`${d.name}: blank`); continue; }
      let crop;
      if (d.tight) crop = [Math.floor(b[0] / 2) - PAD, Math.floor(b[1] / 2) - PAD, Math.ceil(b[2] / 2) - PAD, Math.ceil(b[3] / 2) - PAD];
      else crop = [bx - d.grow[0], by - d.grow[1], bx + bw + d.grow[2], by + bh + d.grow[3]].map(Math.round);
      if (b[0] < (PAD + crop[0]) * 2 || b[1] < (PAD + crop[1]) * 2 || b[2] > (PAD + crop[2]) * 2 || b[3] > (PAD + crop[3]) * 2)
        fail.push(`${d.name}: content outside its crop (${b.join(',')} vs ${crop.join(',')})`);
      const png = await dshot({ x: PAD + crop[0], y: PAD + crop[1], width: crop[2] - crop[0], height: crop[3] - crop[1] });
      const w = (crop[2] - crop[0]) * 2, h = (crop[3] - crop[1]) * 2;
      const bxPx = (bx - crop[0]) * 2, byPx = (by - crop[1]) * 2;
      const s = { name: d.name, group: 'decor', kind: 'decor', w, h, png, box: [bxPx, byPx, bw * 2, bh * 2],
        pivot: [r6((bxPx + bw) / w), r6(1 - (byPx + bh) / h)],
        source: { css: 'world_tour/css (tokens, base, paper, components, screens)', html: d.html, note: d.note } };
      if (d.inset) {
        const l = bxPx + d.inset[0] * 2, t = byPx + d.inset[1] * 2;
        const r = (w - bxPx - bw * 2) + d.inset[2] * 2, bo = (h - byPx - bh * 2) + d.inset[3] * 2;
        s.border = [l, bo, r, t];
        if (d.tile) s.tilePx = [d.tile[0] * 2, d.tile[1] * 2];
      }
      if (d.mode) s.drawMode = d.mode;
      if (d.tight) {
        // the tail: offset of its top-left from the bubble's bottom centre, px; pivot on that point
        s.offset = [r6((crop[0] - (bx + bw / 2)) * 2), r6((crop[1] - (by + bh)) * 2)];
        s.pivot = [r6(-s.offset[0] / w), r6((s.offset[1] + h) / h)];
        delete s.box;
      }
      add(s);
    }
    // halftone dot patches: body::before (top right) and body::after (bottom left), fixed to the window
    // corners; exported as the part a window shows (the rest hangs off screen)
    await dstage('', true);
    for (const [name, clip, pivot, note] of [
      ['halftone_tr', { x: VIEW.width - 480, y: 0, width: 480, height: 480 }, [1, 1], 'body::before: 560px dot patch at top -80, right -80, fading out from the top-right window corner; opacity .75 baked in'],
      ['halftone_bl', { x: 0, y: VIEW.height - 460, width: 460, height: 460 }, [0, 0], 'body::after: 560px dot patch at bottom -100, left -100, fading out from the bottom-left window corner; opacity .75 baked in']]) {
      const png = await dshot(clip);
      if (!(await dbounds(png))) { fail.push(`${name}: blank`); continue; }
      add({ name, group: 'decor', kind: 'decor', w: clip.width * 2, h: clip.height * 2, png, pivot, source: { css: 'world_tour/css/base.css', note } });
    }
    await dstage('', false);
    await dpage.close();

    if (pageErrors.length) fail.push(...pageErrors.map(e => 'page: ' + e));

    // ---- manifest ----
    const L = info.route.layout;
    const manifest = {
      format: 1,
      generator: 'world_tour_unity/Tools/export_sprites.mjs',
      renderer: { playwright: pwVersion, chromium: chromeVersion },
      theme,
      reference: '2x the largest CSS size in a 1920x1080 window; Sprite pixels per unit 200 makes native size equal that CSS size',
      conventions: {
        pivot: 'Unity normalized pivot, origin bottom-left',
        box: 'where the web svg box lies in the sprite, px from the sprite top-left [x, y, w, h] (x, y can be negative); the web sizes and rotates that box, and the art may overflow it',
        offset: 'px from the frame top-left (dog: web box; bubble_tail: bubble bottom centre) to the sprite top-left; y down',
        border: 'Unity sprite border in px [left, bottom, right, top] (Vector4 order); drawMode says sliced, tiled, or tiled-whole (tile the whole sprite)',
        dogStack: 'draw every _ink sprite, then every _pad sprite, then the art sprites, in the listed order',
        tint: 'white art meant to be tinted with a palette colour'
      },
      groups: {
        stickers: { size: { regular: SIZES.sticker, small: SIZES.sticker_sm }, note: 'regular uses the web wt-stk filter (thicker border), _sm uses wt-stk-sm; guitar_<skin> are the shop guitar skins, plain guitar is classic' },
        map: { size: SIZES.venue, note: 'locked venues are a CSS filter in the web (grayscale, contrast .7, brightness 1.08, opacity .5), not separate art' },
        ui: { size: { icon: SIZES.icon, scribble: SIZES.scribble } },
        dog: { size: SIZES.dog, framePx: [DW, DH], anchorPx: anchor, unitsToPx: DW / 160 },
        glyphs: { note: 'symbols the bundled fonts lack (BRIEF: drawn by the team in the same style), white for tinting; glyph = TMP sprite metrics in px at emPx per em (Editor/GlyphSetup builds the TMP sprite asset from them)' },
        decor: { note: 'CSS scrapbook pieces from the web stylesheets at 2x CSS px (device scale 2). Tiled pieces have a centre exactly tilePx wide and high, so the pattern continues; a partial last tile can leave a small seam before the right or bottom border' }
      },
      sprites: sprites.map(s => {
        const o = { name: s.name, group: s.group, file: `${s.group}/${s.name}.png`, w: s.w, h: s.h, pivot: s.pivot };
        if (s.box) o.box = s.box;
        if (s.offset) o.offset = s.offset;
        if (s.role) { o.part = s.part; o.layer = s.role; }
        if (s.tint) o.tint = true;
        if (s.border) o.border = s.border;
        if (s.drawMode) o.drawMode = s.drawMode;
        if (s.tilePx) o.tilePx = s.tilePx;
        if (s.glyph) o.glyph = s.glyph;
        o.source = s.source;
        o.sha256 = sha(s.png);
        return o;
      }),
      dog: {
        poses: POSES,
        outfits: info.outfits,
        stacks,
        // the same stacks as a list, for readers without dictionaries (Unity JsonUtility)
        stackList: POSES.flatMap(p => Object.entries(stacks[p]).map(([o, layers]) => ({ pose: p, outfit: o, layers }))),
        animation: {
          tail: { sprites: ['dog_tail', 'dog_tail_ink', 'dog_tail_pad'], rotateAboutPx: [103 * DW / 160, 128 * DH / 160], wag: 'rotate 0 -> 14 deg -> 0 over 1.1 s, ease-in-out, loop; not in sleepy' },
          lids: { sprites: ['dog_lids_idle'], scaleAboutPx: [80 * DW / 160, 59 * DH / 160], blink: 'scaleY 0 (hidden) normally; 0 at 92%, 1 at 95%, 0 at 100% of 4.2 s, loop; idle only' },
          zzz: { sprites: ['dog_sleepy_z1', 'dog_sleepy_z2', 'dog_sleepy_z3'], float: 'translate (3, -5) units and opacity .55 at 50% of 2.4 s, ease-in-out, loop; delays 0, .4, .8 s (with their _ink and _pad)' },
          head: 'think and oops tilts are baked into dog_head_think / dog_head_oops and their outfit layers'
        },
        compositeCheck: composite
      },
      map: {
        layout: { name: 'landscape', w: L.w, h: L.h, pts: L.pts, via: L.via, decor: L.decor },
        venueOrder: info.venueOrder,
        route: 'not exported as sprites: draw it from these curves (SVG path data in layout units, stretched to the map rect, preserveAspectRatio none). Web order: under (curveAll), then done (curveDone[reached], none when reached is 0), then dash (curveAll)',
        routeStrokes: info.route.strokes,
        curveAll: info.route.curveAll,
        curveDone: info.route.curveDone
      }
    };

    // ---- summary ----
    const groups = {};
    for (const s of sprites) { const g = groups[s.group] || (groups[s.group] = { n: 0, bytes: 0, px: 0 }); g.n++; g.bytes += s.png.length; g.px += s.w * s.h; }
    const total = sprites.reduce((a, s) => a + s.png.length, 0);
    const largest = sprites.reduce((a, s) => (s.png.length > a.png.length ? s : a));
    log(`sprites: ${sprites.length}, PNG total ${(total / 1e6).toFixed(2)} MB, largest ${largest.group}/${largest.name}.png ${(largest.png.length / 1e3).toFixed(1)} KB (${largest.w}x${largest.h})`);
    for (const [g, v] of Object.entries(groups)) log(`  ${g.padEnd(9)} ${String(v.n).padStart(3)} sprites  ${(v.bytes / 1e3).toFixed(0).padStart(6)} KB  ${(v.px / 1e6).toFixed(2)} Mpx`);
    log(`dog composite: worst ${worst.pose}/${worst.outfit} max diff ${worst.maxd}, ${worst.over32} px > 32, ${worst.over8} px > 8 of ${worst.lit} lit`);

    if (fail.length) { console.error('FAILED:\n  ' + fail.join('\n  ')); process.exitCode = 1; return; }

    // ---- sheet ----
    if (sheetPath) {
      const items = sprites.map(s => ({ label: `${s.group}/${s.name} ${s.w}x${s.h}`, b64: s.png.toString('base64') }));
      const b64 = await page.evaluate(a => window.B21.sheet(...a), [items, 12, 200]);
      fs.mkdirSync(path.dirname(sheetPath), { recursive: true });
      fs.writeFileSync(sheetPath, Buffer.from(b64, 'base64'));
      log(`contact sheet: ${sheetPath}`);
    }

    if (dry) { log('dry run: nothing written'); return; }

    // ---- write ----
    const mfPath = path.join(outDir, 'manifest.json');
    const old = fs.existsSync(mfPath) ? JSON.parse(fs.readFileSync(mfPath, 'utf8')) : null;
    let written = 0, same = 0, removed = 0;
    for (const s of sprites) {
      const f = path.join(outDir, s.group, s.name + '.png');
      fs.mkdirSync(path.dirname(f), { recursive: true });
      if (fs.existsSync(f) && fs.readFileSync(f).equals(s.png)) { same++; continue; }
      fs.writeFileSync(f, s.png); written++;
    }
    // remove PNGs an earlier run wrote that this run no longer makes (only files the old manifest listed)
    if (old && Array.isArray(old.sprites)) {
      const now = new Set(sprites.map(s => `${s.group}/${s.name}.png`));
      for (const o of old.sprites) {
        if (typeof o.file !== 'string' || now.has(o.file) || !/^[a-z_]+\/[a-z0-9_-]+\.png$/.test(o.file)) continue;
        const f = path.join(outDir, o.file);
        if (fs.existsSync(f)) { fs.unlinkSync(f); removed++; }
        const dir = path.dirname(f);
        if (fs.existsSync(dir) && fs.readdirSync(dir).length === 0) fs.rmdirSync(dir);
      }
    }
    const text = JSON.stringify(manifest, null, 2) + '\n';
    const mfSame = fs.existsSync(mfPath) && fs.readFileSync(mfPath, 'utf8') === text;
    if (!mfSame) fs.writeFileSync(mfPath, text);
    log(`wrote ${written} PNGs, ${same} unchanged, ${removed} removed; manifest ${mfSame ? 'unchanged' : 'written'} (sha256 ${sha(Buffer.from(text))})`);
    log(`out: ${outDir}`);
  } finally {
    if (browser) await browser.close();
    await new Promise(r => server.close(r));
  }
}

main().catch(e => { console.error('runner error:', e && e.stack || e); process.exitCode = 2; });
