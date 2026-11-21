import { mergeStyles } from './sprite.js';

/* riff, the tour dog. One silhouette, five poses: idle, cheer, think, sleepy, oops.
   Outfit layers draw on top: bandana, beanie, shades, headphones, spiked collar.
   setOutfit() sets what riff wears everywhere (backstage equips it). */
let worn = '';
export const setOutfit = o => { worn = o || ''; };

const B = 'var(--accent-2)', K = 'var(--ink)', FUR = 'var(--fur)', FUR2 = 'var(--fur-2)', TEE = 'var(--tee)', W = 'var(--card)', P = 'var(--paper-deep)', L = 'var(--accent)';
const f = c => `style="fill:${c}"`;
const none = 'style="fill:none"';

/* outlined limb: thick ink stroke with a fur stroke on top */
const limb = (d, w = 13) => `<path d="${d}" style="fill:none;stroke:${K};stroke-width:${w}"/><path d="${d}" style="fill:none;stroke:${FUR};stroke-width:${w - 7}"/>`;

const star = (cx, cy, r) => {
  const p = []; for (let i = 0; i < 10; i++) { const a = (-90 + i * 36) * Math.PI / 180, q = i % 2 ? r * .44 : r; p.push(`${(cx + q * Math.cos(a)).toFixed(1)},${(cy + q * Math.sin(a)).toFixed(1)}`); }
  return `<polygon points="${p.join(' ')}" ${f(L)} style="stroke-width:2.5"/>`;
};

function ears(pose) {
  if (pose === 'cheer') return `
    <path d="M56 44C44 32 40 16 48 10c8 2 14 16 16 28z" ${f(FUR2)}/>
    <path d="M104 44c12-12 16-28 8-34-8 2-14 16-16 28z" ${f(FUR2)}/>`;
  if (pose === 'oops') return `
    <path d="M52 46C38 50 26 52 22 62c6 6 22 2 32-6z" ${f(FUR2)}/>
    <path d="M108 46c14 4 26 6 30 16-6 6-22 2-32-6z" ${f(FUR2)}/>`;
  return `
    <path d="M53 40C39 40 31 58 37 80c4 9 15 5 17-6z" ${f(FUR2)}/>
    <path d="M107 40c14 0 22 18 16 40-4 9-15 5-17-6z" ${f(FUR2)}/>`;
}

function face(pose) {
  const glint = (x, y) => `<circle cx="${x}" cy="${y}" r="1.6" ${f(W)} style="stroke:none"/>`;
  let eyes, mouth, extra = '';
  switch (pose) {
    case 'cheer':
      eyes = `<path d="M60 61q7-9 14 0M86 61q7-9 14 0" ${none} style="fill:none;stroke-width:4"/>`;
      mouth = `<path d="M70 79q10 16 20 0z" ${f(K)}/><path d="M74 85q6 6 12 0q-6-5-12 0z" ${f(P)} style="stroke:none"/>`;
      break;
    case 'think':
      eyes = `<circle cx="67" cy="59" r="6.5" ${f(W)} style="stroke-width:3"/><circle cx="69" cy="56" r="3.6" ${f(K)} style="stroke:none"/>${glint(70, 55)}
              <circle cx="93" cy="59" r="6.5" ${f(W)} style="stroke-width:3"/><circle cx="95" cy="56" r="3.6" ${f(K)} style="stroke:none"/>${glint(96, 55)}`;
      mouth = `<path d="M74 81q6-3 12 1" ${none} style="fill:none;stroke-width:3.5"/>`;
      extra = `<path d="M84 46q8-6 16-2" style="fill:none;stroke-width:3.5"/>`;
      break;
    case 'sleepy':
      eyes = `<path d="M61 61q6 5 12 0M87 61q6 5 12 0" style="fill:none;stroke-width:3.5"/>`;
      mouth = `<path d="M76 80q4 3 8 0" style="fill:none;stroke-width:3"/>`;
      break;
    case 'oops':
      eyes = `<path d="M61 55l10 5-10 5M99 55l-10 5 10 5" style="fill:none;stroke-width:3.8"/>`;
      mouth = `<path d="M71 82q4-4 9 0t9 0" style="fill:none;stroke-width:3.5"/>`;
      extra = `<path d="M122 34q6 9 0 14q-6-5 0-14z" ${f(L)} style="stroke-width:2.5"/>`;
      break;
    default:
      eyes = `<circle cx="67" cy="59" r="6.5" ${f(W)} style="stroke-width:3"/><circle cx="68" cy="60" r="3.8" ${f(K)} style="stroke:none"/>${glint(69.5, 58.5)}
              <circle cx="93" cy="59" r="6.5" ${f(W)} style="stroke-width:3"/><circle cx="94" cy="60" r="3.8" ${f(K)} style="stroke:none"/>${glint(95.5, 58.5)}
              <ellipse class="lid" cx="67" cy="59" rx="7.5" ry="7.5" ${f(FUR)} style="stroke:none"/>
              <ellipse class="lid" cx="93" cy="59" rx="7.5" ry="7.5" ${f(FUR2)} style="stroke:none"/>`;
      mouth = `<path d="M80 75v3M73 79q3.5 4 7-1q3.5 5 7 1" style="fill:none;stroke-width:3"/>`;
  }
  return `
    <ellipse cx="94" cy="58" rx="12" ry="12.5" ${f(FUR2)} style="stroke:none"/>
    ${eyes}
    <ellipse cx="59" cy="72" rx="6" ry="3.4" ${f(P)} style="stroke:none;opacity:.85"/>
    <ellipse cx="101" cy="72" rx="6" ry="3.4" ${f(P)} style="stroke:none;opacity:.85"/>
    <ellipse cx="80" cy="76" rx="13" ry="9" ${f(FUR)} style="stroke-width:3"/>
    <path d="M74 69q6-4 12 0q-1 6-6 7q-5-1-6-7z" ${f(K)} style="stroke-width:2"/>
    ${mouth}${extra}`;
}

function head(pose) {
  return `
    <path d="M64 37l4-12 6 8 6-13 6 13 6-8 4 12z" ${f(FUR)}/>
    <ellipse cx="80" cy="60" rx="33" ry="29" ${f(FUR)}/>
    ${ears(pose)}
    ${face(pose)}`;
}

function outfitLayer(outfit, pose) {
  if (pose === 'sleepy') return '';
  switch (outfit) {
    case 'bandana': return `<path d="M60 98q20 10 40 0l-6 16q-14 8-28 0z" ${f(P)} style="stroke-width:3.5"/><circle cx="72" cy="104" r="1.6" ${f(W)} style="stroke:none"/><circle cx="86" cy="106" r="1.6" ${f(W)} style="stroke:none"/>`;
    case 'beanie': return `<path d="M50 44q30-34 60 0z" ${f(L)} style="stroke-width:3.5"/><rect x="47" y="40" width="66" height="10" rx="5" ${f(P)} style="stroke-width:3.5"/><circle cx="80" cy="13" r="6" ${f(W)} style="stroke-width:3"/>`;
    case 'shades': return `<path d="M54 54h52" style="stroke-width:3"/><rect x="55" y="51" width="22" height="14" rx="5" ${f(K)}/><rect x="83" y="51" width="22" height="14" rx="5" ${f(K)}/><path d="M59 55l5-1M87 55l5-1" style="stroke:${W};stroke-width:2"/>`;
    case 'phones': return `<path d="M47 58Q46 18 80 18T113 58" style="fill:none;stroke:${K};stroke-width:10"/><path d="M47 58Q46 18 80 18T113 58" style="fill:none;stroke:${L};stroke-width:4"/><rect x="36" y="50" width="16" height="24" rx="7" ${f(B)} style="stroke-width:3.5"/><rect x="108" y="50" width="16" height="24" rx="7" ${f(B)} style="stroke-width:3.5"/>`;
    case 'collar': return `<path d="M60 98q20 10 40 0" style="fill:none;stroke:${K};stroke-width:9"/>${[[64, 101], [72, 104], [80, 105], [88, 104], [96, 101]].map(([x, y]) => `<path d="M${x - 3.5} ${y}l3.5 7 3.5-7z" ${f(W)} style="stroke-width:2"/>`).join('')}`;
    default: return '';
  }
}

const NECK = ['bandana', 'collar'];

function sitting(pose, outfit) {
  const arms = pose === 'cheer'
    ? limb('M64 106C56 96 50 86 46 76') + limb('M96 106c8-10 14-20 18-30') +
      `<circle cx="45" cy="73" r="7" ${f(FUR)} style="stroke-width:3.5"/><circle cx="115" cy="73" r="7" ${f(FUR)} style="stroke-width:3.5"/>`
    : pose === 'think'
      ? limb('M96 108c-2-8-6-14-12-18') + `<circle cx="84" cy="88" r="7" ${f(FUR)} style="stroke-width:3.5"/>`
      : '';
  const sparkle = pose === 'cheer' ? star(30, 46, 8) + star(134, 34, 6) + star(128, 104, 5) : '';
  const thought = pose === 'think'
    ? `<circle cx="120" cy="38" r="3" ${f(W)} style="stroke-width:2.5"/><circle cx="130" cy="28" r="4.5" ${f(W)} style="stroke-width:2.5"/><circle cx="143" cy="15" r="7" ${f(W)} style="stroke-width:2.5"/>`
    : '';
  const tilt = pose === 'think' ? 'rotate(-10 80 70)' : pose === 'oops' ? 'translate(0 3)' : '';
  return `
    <g class="tail"><path d="M103 128c17-4 25-18 21-32" style="fill:none;stroke:${K};stroke-width:13"/><path d="M103 128c17-4 25-18 21-32" style="fill:none;stroke:${FUR};stroke-width:6"/></g>
    <ellipse cx="58" cy="136" rx="15" ry="13" ${f(FUR)}/>
    <ellipse cx="102" cy="136" rx="15" ry="13" ${f(FUR)}/>
    <path d="M56 146c-6-28 4-50 24-50s30 22 24 50z" ${f(TEE)}/>
    <path d="M68 99q12 9 24 0" ${none} style="fill:none;stroke-width:3"/>
    ${star(80, 124, 9)}
    <ellipse cx="68" cy="147" rx="11" ry="7" ${f(FUR)}/>
    <ellipse cx="92" cy="147" rx="11" ry="7" ${f(FUR)}/>
    ${NECK.includes(outfit) ? outfitLayer(outfit, pose) : ''}
    ${pose === 'think' ? '' : arms}
    <g class="head" transform="${tilt}">${head(pose)}${outfit && !NECK.includes(outfit) ? outfitLayer(outfit, pose) : ''}</g>
    ${pose === 'think' ? arms : ''}
    ${sparkle}${thought}`;
}

function lying() {
  return `
    <g class="tail"><path d="M126 132c14 0 20-10 14-20" style="fill:none;stroke:${K};stroke-width:13"/><path d="M126 132c14 0 20-10 14-20" style="fill:none;stroke:${FUR};stroke-width:6"/></g>
    <path d="M40 146c0-26 26-36 56-36s38 14 34 36z" ${f(TEE)}/>
    ${star(98, 128, 8)}
    <ellipse cx="122" cy="146" rx="15" ry="7" ${f(FUR)}/>
    <g transform="translate(-22 58) rotate(-8 80 60) scale(.92)">${head('sleepy')}</g>
    <ellipse cx="44" cy="147" rx="16" ry="7" ${f(FUR)}/>
    <ellipse cx="70" cy="148" rx="14" ry="6.5" ${f(FUR)}/>
    <g class="zzz" style="stroke:none">
      <text x="98" y="78" style="font:18px var(--hand);fill:${K}">z</text>
      <text x="112" y="58" style="font:24px var(--hand);fill:${K}">z</text>
      <text x="128" y="34" style="font:32px var(--hand);fill:${K}">z</text>
    </g>`;
}

const LABELS = { idle: 'riff the tour dog', cheer: 'riff cheering', think: 'riff thinking', sleepy: 'riff napping', oops: 'riff wincing' };

export function dog(pose = 'idle', { outfit = worn, cls = '' } = {}) {
  const body = mergeStyles(pose === 'sleepy' ? lying() : sitting(pose, outfit));
  return `<svg class="dog pose-${pose} ${cls}" viewBox="0 0 160 160" role="img" aria-label="${LABELS[pose] || LABELS.idle}">
    <g filter="url(#wt-stk)"><g class="body" style="stroke:${K};stroke-width:4;stroke-linejoin:round;stroke-linecap:round">${body}</g></g>
  </svg>`;
}

/* swap pose in place (keeps the element, retriggers the pose animation) */
export function setPose(host, pose, opts) {
  if (!host) return;
  host.innerHTML = dog(pose, opts);
}
