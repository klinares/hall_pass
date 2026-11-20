/* Task 4.2/4.3 (builder M2). A fake DOM and show ctx, just enough for the web game's math mode templates
   (world_tour/modes/math/*.js), so the real modes can run in node for the C# cross-check vectors. */
/* ---------------- a fake DOM: just enough for these templates ---------------- */
const VOID = new Set(['input', 'br', 'img']);
export class El {
  constructor(tag, attrs = {}) {
    this.tag = tag; this.attrs = attrs; this.children = []; this.parent = null; this.text = null;
    this.listeners = {}; this.onclick = null;
    this.disabled = 'disabled' in attrs; this.hidden = 'hidden' in attrs;
    const el = this;
    this.classList = {
      add: (...c) => { const s = el.cls(); c.forEach(x => s.add(x)); el.attrs.class = [...s].join(' '); },
      remove: (...c) => { const s = el.cls(); c.forEach(x => s.delete(x)); el.attrs.class = [...s].join(' '); },
      toggle: (c, on) => { const s = el.cls(); const want = on === undefined ? !s.has(c) : !!on; if (want) s.add(c); else s.delete(c); el.attrs.class = [...s].join(' '); return want; },
      contains: c => el.cls().has(c)
    };
    this.style = {};
    if (tag === 'svg') {
      this.createSVGPoint = () => ({ x: 0, y: 0, matrixTransform() { return { x: this.x, y: this.y }; } });
      this.getScreenCTM = () => ({ inverse: () => null });
    }
  }
  cls() { return new Set((this.attrs.class || '').split(/\s+/).filter(Boolean)); }
  get id() { return this.attrs.id; }
  get dataset() { const d = {}; for (const [k, v] of Object.entries(this.attrs)) if (k.startsWith('data-')) d[k.slice(5).replace(/-(\w)/g, (_, c) => c.toUpperCase())] = v; return d; }
  get isConnected() { return true; }
  get parentElement() { return this.parent; }
  get firstElementChild() { return this.children.find(c => c.tag !== '#text') || null; }
  set innerHTML(html) { this.children = []; this.raw = html; parseInto(this, html); }
  get innerHTML() { return this.raw || ''; }
  get textContent() { return this.text != null ? this.text : this.children.map(c => c.textContent).join(''); }
  set textContent(v) { this.children = []; const t = new El('#text'); t.text = String(v); t.parent = this; this.children.push(t); }
  getAttribute(k) { return this.attrs[k]; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  removeAttribute(k) { delete this.attrs[k]; }
  addEventListener(t, f) { (this.listeners[t] ||= []).push(f); }
  fire(t, e = {}) { (this.listeners[t] || []).forEach(f => f({ target: this, preventDefault() {}, ...e })); if (t === 'click' && this.onclick) this.onclick(e); }
  closest(sel) { let n = this; while (n && n.tag !== '#root') { if (matchCompound(n, sel)) return n; n = n.parent; } return null; }
  *walk() { for (const c of this.children) { if (c.tag === '#text') continue; yield c; yield* c.walk(); } }
  querySelectorAll(sel) {
    const parts = sel.split(',').map(s => s.trim().split(/\s+/));
    const out = [];
    for (const n of this.walk()) if (parts.some(chain => matchChain(n, chain, this))) out.push(n);
    return out;
  }
  querySelector(sel) { return this.querySelectorAll(sel)[0] || null; }
}
function matchCompound(n, comp) {
  if (n.tag === '#text') return false;
  const m = comp.match(/^([a-zA-Z][\w-]*)?((?:[#.][\w-]+|\[[^\]]+\])*)$/);
  if (!m) throw new Error('selector ' + comp);
  if (m[1] && m[1] !== n.tag) return false;
  for (const t of m[2].match(/[#.][\w-]+|\[[^\]]+\]/g) || []) {
    if (t[0] === '#' && n.attrs.id !== t.slice(1)) return false;
    if (t[0] === '.' && !n.cls().has(t.slice(1))) return false;
    if (t[0] === '[' && !(t.slice(1, -1) in n.attrs)) return false;
  }
  return true;
}
function matchChain(n, chain, root) {
  if (!matchCompound(n, chain[chain.length - 1])) return false;
  let i = chain.length - 2, p = n.parent;
  while (i >= 0 && p && p !== root.parent) { if (matchCompound(p, chain[i])) i--; p = p.parent; }
  return i < 0;
}
function decode(s) { return s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&'); }
export function parseInto(root, html) {
  const re = /<!--[\s\S]*?-->|<(\/?)([a-zA-Z][\w-]*)((?:\s+[\w:-]+(?:="[^"]*")?)*)\s*(\/?)>|([^<]+)/g;
  let cur = root, m;
  while ((m = re.exec(html))) {
    if (m[0].startsWith('<!--')) continue;
    if (m[5] != null) { if (m[5].trim() === '' && cur.tag !== 'text' && cur.tag !== 'output' && cur.tag !== 'b') { /* keep whitespace out */ } const t = new El('#text'); t.text = decode(m[5]); t.parent = cur; cur.children.push(t); continue; }
    const [, close, tag, attrStr, self] = m;
    if (close) { let n = cur; while (n && n.tag !== tag) n = n.parent; if (n) cur = n.parent; continue; }
    const attrs = {};
    for (const a of attrStr.matchAll(/([\w:-]+)(?:="([^"]*)")?/g)) attrs[a[1]] = a[2] != null ? decode(a[2]) : '';
    const el = new El(tag, attrs); el.parent = cur; cur.children.push(el);
    if (!self && !VOID.has(tag)) cur = el;
  }
}

/* ---------------- a fake show ctx (show.js:207-213) ---------------- */
/* T.EV: what the fake ctx and the stubbed sound/motion record, in order */
export const T = { EV: [] };
globalThis.__ev = null;
export function makeCtx(prob) {
  const root = new El('#root'); const el = new El('div'); el.parent = root; root.children.push(el);
  const st = { tries: 0, hint: null, hintMax: 0 };
  const ctx = {
    prob, el,
    say: (h, pose) => T.EV.push(['say', String(h), pose ?? null]),
    miss: () => { st.tries++; T.EV.push(['miss', st.tries]); return st.tries; },
    mistake: w => T.EV.push(['mistake', w]),
    setupHints: (max, fn) => { st.hintMax = max; st.hint = fn; },
    lockHints: () => T.EV.push(['lockHints']),
    hintBtn: () => '',
    next: ms => T.EV.push(['next', ms]),
    win: (x = 0) => T.EV.push(['win', x]),
    reveal: h => T.EV.push(['reveal', String(h)]),
  };
  return { ctx, st };
}
export function hint(st) { const r = st.hint ? st.hint() : false; T.EV.push(['hint', r !== false]); }
