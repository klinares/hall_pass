/* Hash router: #/map, #/venue/coffee, #/show/remix?v=coffee&w=2026-10-26, ...
   Works from any GitHub Pages subfolder because nothing depends on server paths. */
import { slideIn } from './motion.js';

const routes = [];
let cleanup = null;

export function route(pattern, loader) { routes.push({ pattern, loader }); }

export function go(hash, { replace = false } = {}) {
  const h = hash.startsWith('#') ? hash : '#' + hash;
  if (location.hash === h) return render();
  if (replace) { history.replaceState(null, '', h); render(); }
  else location.hash = h;
}

function parse() {
  const raw = location.hash.replace(/^#\/?/, '') || 'map';
  const [path, qs = ''] = raw.split('?');
  const parts = path.split('/').filter(Boolean);
  const query = Object.fromEntries(new URLSearchParams(qs));
  return { parts, query };
}

export async function render() {
  const { parts, query } = parse();
  const r = routes.find(x => x.pattern === parts[0]) || routes.find(x => x.pattern === 'map');
  const app = document.getElementById('app');
  if (typeof cleanup === 'function') { try { cleanup(); } catch (e) { console.warn(e); } }
  cleanup = null;
  document.body.classList.remove('in-show');
  const mod = await r.loader();
  const sec = document.createElement('div');
  sec.className = 'screen';
  app.replaceChildren(sec);
  cleanup = mod.mount(sec, { id: parts[1], query }) || null;
  window.scrollTo(0, 0);
  slideIn(sec);
}

export function start() {
  addEventListener('hashchange', render);
  render();
}
