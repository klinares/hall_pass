/* MODE: stage designer. The first quadrant as a stage plot.
   place / move: tap the stage, the light snaps to the nearest point, nudge it with the arrows,
   then set it. read: set the two numbers with big − / + pickers.
   The arrows and pickers keep every control at 48px+ even when grid points are close together. */
import { generate, SKILL } from '../../math/gen/coords.js';
import { promptCard, ladder, controls, showNext } from './parts.js';
import { pop, wobble } from '../../js/motion.js';
import * as snd from '../../js/sound.js';
import { esc } from '../../js/util.js';

const PAD = 1.1;   /* room for the axis numbers, in grid units */

function plot(max, { light, ghost, start, path, hl }) {
  const S = max + PAD + .5;
  const X = x => x + PAD, Y = y => max - y + .5;
  const lines = [];
  for (let i = 0; i <= max; i++) {
    lines.push(`<line x1="${X(i)}" y1="${Y(0)}" x2="${X(i)}" y2="${Y(max)}" class="${i === 0 ? 'axis' : 'gl'}"/>`);
    lines.push(`<line x1="${X(0)}" y1="${Y(i)}" x2="${X(max)}" y2="${Y(i)}" class="${i === 0 ? 'axis' : 'gl'}"/>`);
    lines.push(`<text x="${X(i)}" y="${Y(0) + .62}" class="ax ${i === hl ? 'on' : ''}">${i}</text>`);
    if (i) lines.push(`<text x="${X(0) - .45}" y="${Y(i) + .17}" class="ax">${i}</text>`);
  }
  const lamp = (p, cls) => `<g class="lamp ${cls}" transform="translate(${X(p.x)} ${Y(p.y)})"><circle r=".42" class="halo"/><circle r=".24" class="bulb"/></g>`;
  const route = path ? `<path class="route" d="M${X(path.from.x)} ${Y(path.from.y)} H${X(path.x ?? path.from.x)} V${Y(path.y ?? path.from.y)}"/>` : '';
  return `
    <svg class="plot" viewBox="0 0 ${S} ${S}" role="img" aria-label="stage grid, 0 to ${max}">
      <rect x="${X(0)}" y="${Y(max)}" width="${max}" height="${max}" class="floor"/>
      ${lines.join('')}
      <text x="${X(max) + .25}" y="${Y(0) + .2}" class="axl">x</text>
      <text x="${X(0) - .2}" y="${Y(max) - .2}" class="axl">y</text>
      ${route}
      ${start ? lamp(start, 'start') : ''}
      ${light ? lamp(light, 'set') : ''}
      ${ghost ? lamp(ghost, 'ghost') : ''}
    </svg>`;
}

const picker = (id, label, v) => `
  <div class="picker" id="${id}">
    <span class="plbl">${label}</span>
    <button class="key fn" data-d="-1" aria-label="${label} down">−</button>
    <output class="pval" aria-live="polite">${v ?? '?'}</output>
    <button class="key fn" data-d="1" aria-label="${label} up">+</button>
  </div>`;

export default {
  id: 'stage',
  name: 'stage designer',
  sub: 'place the gear on the grid',
  sticker: 'lights',
  kind: 'math',
  skill: SKILL,
  generate,
  render(ctx) {
    const p = ctx.prob, pr = p.params, max = pr.max, read = p.kind === 'read';
    let ghost = null, pick = { x: null, y: null }, path = null, hl = null, done = false;

    ctx.el.innerHTML = `
      <div class="desk stagedesk" data-wobble>
        <span class="desk-tag">stage plot ✶ ${esc(pr.gear)}</span>
        <div class="plotwrap" id="plot"></div>
        ${read ? `
          <div class="pickers">${picker('px', 'across', null)}${picker('py', 'up', null)}</div>
          <div class="pairshow" id="pair" aria-live="polite">( ?, ? )</div>
          <button class="btn" id="set">lock it in</button>` : `
          <div class="nudges" role="group" aria-label="move the light">
            <button class="key fn" data-dx="-1" aria-label="left">←</button>
            <button class="key fn" data-dy="1" aria-label="up">↑</button>
            <button class="key fn" data-dy="-1" aria-label="down">↓</button>
            <button class="key fn" data-dx="1" aria-label="right">→</button>
          </div>
          <div class="pairshow" id="pair" aria-live="polite">tap the stage</div>
          <button class="btn" id="set" disabled>set the ${esc(pr.gear)}</button>`}
      </div>
      ${promptCard(p.prompt, read ? 'where is it' : 'stage plot')}
      ${controls(ctx)}`;

    const $ = s => ctx.el.querySelector(s);
    const wrap = $('#plot');
    const draw = () => {
      wrap.innerHTML = plot(max, {
        light: read ? pr.point : done && !read ? p.answer.value : null,
        ghost: read ? (pick.x != null && pick.y != null && done ? pick : null) : ghost,
        start: pr.start, path, hl
      });
      $('#pair').textContent = read ? `( ${pick.x ?? '?'}, ${pick.y ?? '?'} )` : ghost ? `( ${ghost.x}, ${ghost.y} )` : 'tap the stage';
    };

    if (!read) {
      wrap.addEventListener('pointerdown', e => {
        if (done) return;
        const svg = wrap.querySelector('svg'), pt = svg.createSVGPoint();
        pt.x = e.clientX; pt.y = e.clientY;
        const q = pt.matrixTransform(svg.getScreenCTM().inverse());
        const x = Math.round(q.x - PAD), y = Math.round(max + .5 - q.y);
        if (x < 0 || y < 0 || x > max || y > max) return;
        ghost = { x, y }; snd.tap(); draw();
        $('#set').disabled = false;
      });
      ctx.el.querySelectorAll('.nudges button').forEach(b => b.onclick = () => {
        if (done) return;
        if (!ghost) ghost = pr.start ? { ...pr.start } : { x: 0, y: 0 };
        ghost = { x: Math.max(0, Math.min(max, ghost.x + (+b.dataset.dx || 0))), y: Math.max(0, Math.min(max, ghost.y + (+b.dataset.dy || 0))) };
        snd.tap(); draw(); $('#set').disabled = false;
      });
    } else {
      ['px', 'py'].forEach(id => ctx.el.querySelectorAll(`#${id} button`).forEach(b => b.onclick = () => {
        if (done) return;
        const k = id === 'px' ? 'x' : 'y';
        pick[k] = Math.max(0, Math.min(max, (pick[k] ?? 0) + +b.dataset.d));
        ctx.el.querySelector(`#${id} .pval`).textContent = pick[k];
        snd.tap(); draw();
      }));
    }

    const L = ladder(ctx, p, {
      onStep(n) {
        const a = p.answer.value, from = pr.start || { x: 0, y: 0 };
        if (n === 2) { hl = a.x; path = read ? { from: a, y: 0 } : { from, x: a.x }; }
        if (n >= 3) { hl = a.x; path = { from, x: a.x, y: a.y }; done = true; if (read) pick = { ...a }; else ghost = null; }
        draw();
      }
    });

    $('#set').onclick = () => {
      if (done || L.over) return;
      const got = read ? (pick.x == null || pick.y == null ? null : pick) : ghost;
      if (!got) { ctx.say(read ? 'set both numbers first.' : 'tap a spot on the stage first.', 'think'); return; }
      if (got.x === p.answer.value.x && got.y === p.answer.value.y) {
        done = true; ctx.lockHints();
        if (!read) ghost = null;
        draw(); pop(wrap.querySelector('.lamp.set, .lamp.ghost') || wrap);
        $('#set').disabled = true;
        L.right(); ctx.next(1400);
        return;
      }
      wobble(wrap);
      if (L.wrong(got)) { $('#set').disabled = true; showNext(ctx); }
    };

    draw();
    ctx.say(read ? 'across first, then up.' : pr.start ? 'move it, then set it.' : 'tap the spot. the arrows fine-tune it.', 'idle');
  }
};
