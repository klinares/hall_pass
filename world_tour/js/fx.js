/* Confetti made of tiny outlined stickers (stars, hearts, bolts), drawn on one canvas. */
import { reduced } from './motion.js';

let cv, cx, bits = [], running = false;
const shapes = {
  star: (() => { const p = new Path2D(); for (let i = 0; i < 10; i++) { const a = (-90 + i * 36) * Math.PI / 180, r = i % 2 ? .42 : 1; p[i ? 'lineTo' : 'moveTo'](Math.cos(a) * r, Math.sin(a) * r); } p.closePath(); return p; })(),
  heart: new Path2D('M0 .9C-.9 .3-1.2-.3-.8-.75-.45-1.05-.1-.9 0-.55.1-.9.45-1.05.8-.75 1.2-.3.9.3 0 .9z'),
  bolt: new Path2D('M.2-1-.6.15h.5L-.25 1 .65-.2h-.5z')
};

function ensure() {
  if (cv) return;
  cv = document.createElement('canvas'); cv.className = 'fx'; cv.setAttribute('aria-hidden', 'true');
  document.body.appendChild(cv); cx = cv.getContext('2d');
  const fit = () => { const r = devicePixelRatio || 1; cv.width = innerWidth * r; cv.height = innerHeight * r; cx.setTransform(r, 0, 0, r, 0, 0); };
  fit(); addEventListener('resize', fit);
}

function colors() {
  const cs = getComputedStyle(document.documentElement);
  return ['--accent', '--card', '--paper-deep', '--accent', '--card'].map(v => cs.getPropertyValue(v).trim() || '#fff');
}

export function burst(n = 34, { x = innerWidth / 2, y = innerHeight * .38 } = {}) {
  if (reduced()) return;
  ensure();
  const cols = colors(), ink = getComputedStyle(document.documentElement).getPropertyValue('--ink').trim() || '#000';
  const kinds = Object.keys(shapes);
  for (let i = 0; i < n; i++) bits.push({
    x, y, vx: (Math.random() - .5) * 14, vy: -Math.random() * 12 - 4, r: Math.random() * 6.28, vr: (Math.random() - .5) * .3,
    s: 7 + Math.random() * 7, c: cols[i % cols.length], ink, k: kinds[i % kinds.length], life: 80 + Math.random() * 30
  });
  if (!running) { running = true; requestAnimationFrame(loop); }
}

function loop() {
  cx.clearRect(0, 0, innerWidth, innerHeight);
  for (let i = bits.length - 1; i >= 0; i--) {
    const p = bits[i];
    p.x += p.vx; p.y += p.vy; p.vy += .38; p.vx *= .99; p.r += p.vr; p.life--;
    cx.save(); cx.translate(p.x, p.y); cx.rotate(p.r); cx.scale(p.s, p.s);
    cx.lineWidth = 2.6 / p.s; cx.lineJoin = 'round'; cx.fillStyle = p.c; cx.strokeStyle = p.ink;
    cx.fill(shapes[p.k]); cx.stroke(shapes[p.k]); cx.restore();
    if (p.life <= 0 || p.y > innerHeight + 40) bits.splice(i, 1);
  }
  if (bits.length) requestAnimationFrame(loop); else { running = false; cx.clearRect(0, 0, innerWidth, innerHeight); }
}

export function toast(text) {
  const d = document.createElement('div');
  d.className = 'toast shout'; d.textContent = text; d.setAttribute('aria-hidden', 'true');
  document.body.appendChild(d);
  const a = d.animate(reduced()
    ? [{ opacity: 0 }, { opacity: 1, offset: .2 }, { opacity: 1, offset: .8 }, { opacity: 0 }]
    : [{ opacity: 0, transform: 'translate(-50%,24px) rotate(-5deg) scale(.6)' },
       { opacity: 1, transform: 'translate(-50%,0) rotate(-5deg) scale(1.08)', offset: .22 },
       { opacity: 1, transform: 'translate(-50%,-10px) rotate(-5deg) scale(1)', offset: .7 },
       { opacity: 0, transform: 'translate(-50%,-46px) rotate(-5deg) scale(1)' }],
    { duration: 1100, easing: 'ease-out' });
  a.onfinish = () => d.remove();
}
