/* The sticker sheet. Every sticker, venue drawing and icon is an SVG <symbol>
   injected once into the page and drawn with <use>. Colors come from CSS
   custom properties, so the art follows the theme. */

const K = 'var(--ink)', W = 'var(--card)', P = 'var(--paper-deep)', PP = 'var(--paper)',
      L = 'var(--accent)', B = 'var(--accent-2)', FUR2 = 'var(--fur-2)', MET = '#d6d6e4', DK = '#3a3a3a';
const f = c => `style="fill:${c}"`;
const fs = (c, s, w) => `style="fill:${c};stroke:${s};stroke-width:${w}"`;
const ln = (w = 4) => `style="fill:none;stroke-width:${w}"`;

/* line style shared by every sticker */
const G = (body, extra = '') => `<g style="stroke:${K};stroke-width:4;stroke-linejoin:round;stroke-linecap:round" ${extra}>${body}</g>`;

export function starPts(cx, cy, R, r, n = 5, rot = -90) {
  const pts = [];
  for (let i = 0; i < n * 2; i++) {
    const a = (rot + i * 180 / n) * Math.PI / 180, rad = i % 2 ? r : R;
    pts.push(`${(cx + rad * Math.cos(a)).toFixed(1)},${(cy + rad * Math.sin(a)).toFixed(1)}`);
  }
  return pts.join(' ');
}

const STICKERS = {
  guitar: G(`
    <g transform="translate(50 50) rotate(32) scale(.9) translate(-50 -50)">
      <path d="M43 1h14l-2 13H45z" ${f(K)}/>
      <circle cx="41" cy="5" r="2.2" ${f(W)} style="stroke-width:2"/><circle cx="59" cy="5" r="2.2" ${f(W)} style="stroke-width:2"/>
      <rect x="45.5" y="13" width="9" height="42" rx="1.5" ${f(W)}/>
      <path d="M46 21h8M46 29h8M46 37h8M46 45h8" style="stroke-width:1.6"/>
      <path d="M50 50c-9-2-17 2-16 11-7 3-11 11-8 20 4 11 18 15 24 11 6 4 20 0 24-11 3-9-1-17-8-20 1-9-7-13-16-11z" ${f('var(--gtr)')}/>
      <path d="M58 58c6 2 9 7 8 12" ${ln(2.5)} style="stroke:${W};fill:none"/>
      <rect x="42" y="63" width="16" height="5" rx="1" ${f(K)}/>
      <rect x="42" y="73" width="16" height="5" rx="1" ${f(K)}/>
      <rect x="43" y="83" width="14" height="3.5" rx="1" ${f(W)} style="stroke-width:2"/>
      <path d="M48 9v77M50 9v77M52 9v77" style="stroke-width:.9"/>
      <circle cx="66" cy="80" r="3" ${f(W)} style="stroke-width:2"/><circle cx="62" cy="87" r="3" ${f(W)} style="stroke-width:2"/>
    </g>`),
  amp: G(`
    <path d="M37 24v-8h26v8" ${ln(5)}/>
    <rect x="10" y="24" width="80" height="62" rx="7" ${f(K)}/>
    <rect x="16" y="30" width="68" height="13" rx="3" ${f(L)} style="stroke-width:2.5"/>
    ${[26, 37, 48, 59].map(x => `<circle cx="${x}" cy="36.5" r="3.6" ${f(W)} style="stroke-width:2"/>`).join('')}
    <circle cx="76" cy="36.5" r="3.2" ${f(P)} style="stroke-width:2"/>
    <rect x="16" y="48" width="68" height="32" rx="3" ${fs(DK, W, 2.5)}/>
    <path d="M24 50v28M32 50v28M40 50v28M48 50v28M56 50v28M64 50v28M72 50v28" style="stroke:#555;stroke-width:2"/>
    <text x="50" y="70" text-anchor="middle" style="font:24px var(--hand);fill:${P};stroke:${K};stroke-width:4;paint-order:stroke">loud</text>
    <rect x="16" y="86" width="12" height="6" rx="1" ${f(K)}/><rect x="72" y="86" width="12" height="6" rx="1" ${f(K)}/>`),
  mic: G(`
    <defs><clipPath id="cp-mic"><circle cx="50" cy="28" r="17"/></clipPath></defs>
    <g transform="rotate(-24 50 50)">
      <path d="M41 51h18l-4 40q-5 6-10 0z" ${f(K)}/>
      <rect x="38" y="43" width="24" height="10" rx="3" ${f(P)}/>
      <circle cx="50" cy="28" r="19" ${f(MET)}/>
      <g clip-path="url(#cp-mic)" style="stroke-width:1.6;stroke:#777">
        <path d="M30 18h40M30 24h40M30 30h40M30 36h40M30 42h40M36 8v40M42 8v40M48 8v40M54 8v40M60 8v40M66 8v40"/>
      </g>
      <circle cx="50" cy="28" r="19" style="fill:none"/>
      <path d="M38 20q4-7 11-8" style="fill:none;stroke:${W};stroke-width:3.5"/>
      <rect x="47" y="62" width="6" height="10" rx="2" ${f(L)} style="stroke-width:2"/>
    </g>`),
  bus: G(`
    <path d="M8 30q0-9 9-9h62q11 0 13 13l2 26q0 13-11 13H15q-7 0-7-8z" ${f(P)}/>
    <path d="M8 50h86v7H8z" ${f(L)} style="stroke-width:3"/>
    ${[15, 30, 45].map(x => `<rect x="${x}" y="29" width="11" height="14" rx="2" ${f(W)} style="stroke-width:3"/>`).join('')}
    <rect x="60" y="29" width="10" height="35" rx="2" ${f(W)} style="stroke-width:3"/>
    <path d="M75 29h7q6 0 7 7l1 9H75z" ${f(L)} style="stroke-width:3"/>
    <polygon points="${starPts(30, 64, 6, 2.6)}" ${f(W)} style="stroke-width:2"/>
    <circle cx="90" cy="64" r="3" ${f(W)} style="stroke-width:2"/>
    <circle cx="26" cy="74" r="10" ${f(K)}/><circle cx="26" cy="74" r="4" ${f(W)} style="stroke-width:2"/>
    <circle cx="76" cy="74" r="10" ${f(K)}/><circle cx="76" cy="74" r="4" ${f(W)} style="stroke-width:2"/>`),
  tee: G(`
    <path d="M30 16l12-6q8 9 16 0l12 6 18 15-10 13-8-6v50H30V38l-8 6-10-13z" ${f(W)}/>
    <path d="M17 26l9 11M83 26l-9 11" style="stroke:${L};stroke-width:5"/>
    <path d="M42 10q8 9 16 0" ${ln(4)}/>
    <polygon points="56,30 38,56 49,56 44,76 62,48 51,48 58,30" ${f(P)} style="stroke-width:3"/>`),
  vinyl: G(`
    <circle cx="50" cy="50" r="43" ${f(K)}/>
    ${[37, 31, 25].map(r => `<circle cx="50" cy="50" r="${r}" style="fill:none;stroke:#3d3d3d;stroke-width:1.6"/>`).join('')}
    <path d="M22 34a32 32 0 0 1 16-14" style="fill:none;stroke:${W};stroke-width:3;opacity:.85"/>
    <circle cx="50" cy="50" r="15" ${f(P)} style="stroke-width:3"/>
    <path d="M50 46c-2-3-6-2-6 1 0 3 6 6 6 6s6-3 6-6c0-3-4-4-6-1z" ${f(W)} style="stroke-width:1.5"/>
    <circle cx="50" cy="50" r="2.2" ${f(PP)} style="stroke-width:1.5"/>`),
  lights: G(`
    <polygon points="22,46 4,96 46,96 34,46" style="fill:${L};opacity:.55;stroke:none"/>
    <polygon points="66,46 54,96 96,96 78,46" style="fill:${P};opacity:.55;stroke:none"/>
    <rect x="4" y="8" width="92" height="12" rx="2" ${f(W)}/>
    <path d="M10 20l8-12 8 12 8-12 8 12 8-12 8 12 8-12 8 12 8-12 8 12" style="fill:none;stroke-width:2.5"/>
    <path d="M28 20v8M72 20v8" style="stroke-width:4"/>
    <g transform="rotate(14 28 30)"><rect x="18" y="28" width="20" height="22" rx="4" ${f(K)}/><ellipse cx="28" cy="50" rx="10" ry="4" ${f(L)} style="stroke-width:3"/></g>
    <g transform="rotate(-14 72 30)"><rect x="62" y="28" width="20" height="22" rx="4" ${f(K)}/><ellipse cx="72" cy="50" rx="10" ry="4" ${f(P)} style="stroke-width:3"/></g>`),
  crate: G(`
    <rect x="16" y="6" width="68" height="88" rx="6" ${f(K)}/>
    <path d="M16 18V6h12M72 6h12v12M84 82v12H72M28 94H16V82" style="fill:none;stroke:${MET};stroke-width:6"/>
    <circle cx="50" cy="28" r="9" ${fs('#4a4a4a', W, 3)}/><circle cx="50" cy="28" r="3.5" ${f(L)} style="stroke-width:2"/>
    <circle cx="50" cy="63" r="20" ${fs('#4a4a4a', W, 3)}/>
    <circle cx="50" cy="63" r="11" ${fs('#2c2c2c', '#666', 2)}/>
    <circle cx="50" cy="63" r="4.5" ${f(P)} style="stroke-width:2"/>`),
  star: G(`
    <polygon points="${starPts(50, 53, 45, 19)}" ${f(L)}/>
    <path d="M34 44l6-4" style="stroke:${W};stroke-width:4"/>`),
  heart: G(`
    <path d="M50 88C20 66 8 47 17 30c8-14 26-13 33 2 7-15 25-16 33-2 9 17-3 36-33 58z" ${f(P)}/>
    <path d="M24 36q3-9 12-9" style="fill:none;stroke:${W};stroke-width:4"/>`),
  bolt: G(`<polygon points="60,4 18,56 44,56 34,96 82,38 56,38 70,4" ${f(B)}/>
    <path d="M57 12l-10 14" style="stroke:${W};stroke-width:3.5"/>`),
  ticket: G(`
    <g transform="rotate(-9 50 50)">
      <path d="M6 28h88v14a8 8 0 0 0 0 16v14H6V58a8 8 0 0 0 0-16z" ${f(W)}/>
      <path d="M70 31v38" style="stroke-dasharray:4 4;stroke-width:3"/>
      <text x="38" y="49" text-anchor="middle" style="font:17px var(--hand);fill:${K};stroke:none">admit</text>
      <text x="38" y="65" text-anchor="middle" style="font:17px var(--hand);fill:${P};stroke:none">one</text>
      <polygon points="${starPts(82, 50, 8, 3.4)}" ${f(L)} style="stroke-width:2"/>
    </g>`),
  cassette: G(`
    <rect x="8" y="20" width="84" height="60" rx="6" ${f(L)}/>
    <rect x="16" y="27" width="68" height="28" rx="2" ${f(W)} style="stroke-width:3"/>
    <path d="M20 33h36" style="stroke:${P};stroke-width:3"/>
    <rect x="30" y="38" width="40" height="13" rx="6.5" ${f(K)} style="stroke-width:3"/>
    <circle cx="38" cy="44.5" r="4" ${f(W)} style="stroke-width:2"/><circle cx="62" cy="44.5" r="4" ${f(W)} style="stroke-width:2"/>
    <path d="M24 80l6-16h40l6 16" ${f(P)} style="stroke-width:3"/>`),
  phones: G(`
    <path d="M18 64V50a32 32 0 0 1 64 0v14" style="fill:none;stroke-width:12"/>
    <path d="M18 64V50a32 32 0 0 1 64 0v14" style="fill:none;stroke:${P};stroke-width:4"/>
    <rect x="8" y="52" width="22" height="34" rx="9" ${f(P)}/><rect x="70" y="52" width="22" height="34" rx="9" ${f(P)}/>
    <rect x="24" y="58" width="9" height="22" rx="4" ${f(K)} style="stroke-width:2"/><rect x="67" y="58" width="9" height="22" rx="4" ${f(K)} style="stroke-width:2"/>`),
  setlist: G(`
    <g transform="rotate(-6 50 50)">
      <rect x="18" y="8" width="64" height="86" ${f(W)}/>
      <text x="50" y="27" text-anchor="middle" style="font:15px var(--hand);fill:${P};stroke:none">setlist</text>
      <path d="M26 38h40M26 48h46M26 58h34M26 68h44M26 78h30" style="stroke-width:2.5"/>
      <rect x="38" y="2" width="26" height="10" style="fill:${L};opacity:.85;stroke:none"/>
    </g>`),
  pass: G(`
    <path d="M30 2l14 32M70 2L56 34" style="stroke:${P};stroke-width:6"/>
    <rect x="42" y="28" width="16" height="9" rx="2" ${f(MET)} style="stroke-width:2.5"/>
    <rect x="24" y="34" width="52" height="60" rx="6" ${f(W)}/>
    <path d="M24 40q0-6 6-6h40q6 0 6 6v8H24z" ${f(K)}/>
    <text x="50" y="46" text-anchor="middle" style="font:700 8px var(--mono);fill:${W};stroke:none">all access</text>
    <polygon points="${starPts(50, 68, 15, 6.5)}" ${f(L)} style="stroke-width:3"/>
    <path d="M32 86h36" style="stroke-dasharray:2 3;stroke-width:3"/>`),
  pick: G(`<path d="M50 92C30 72 12 42 18 22c6-15 58-15 64 0 6 20-12 50-32 70z" ${f(P)}/>
    <polygon points="${starPts(50, 40, 14, 6)}" ${f(W)} style="stroke-width:3"/>`),
  note: G(`
    <polygon points="38,22 82,12 82,26 38,36" ${f(K)}/>
    <path d="M40 30v46M80 20v46" style="stroke-width:6"/>
    <ellipse cx="30" cy="76" rx="12" ry="8.5" transform="rotate(-20 30 76)" ${f(K)}/>
    <ellipse cx="70" cy="66" rx="12" ry="8.5" transform="rotate(-20 70 66)" ${f(K)}/>
    <path d="M24 72q3-3 7-3" style="stroke:${W};stroke-width:2.5"/>`),
  coffee: G(`
    <path d="M38 6q-6 6 0 12t0 12M50 4q-6 6 0 12t0 12M62 6q-6 6 0 12t0 12" style="fill:none;stroke-width:3"/>
    <path d="M24 38h52l-7 54H31z" ${f(W)}/>
    <path d="M26 54h48l-3 22H29z" ${f(P)} style="stroke-width:3"/>
    <path d="M50 61c-2-3-6-2-6 1 0 3 6 7 6 7s6-4 6-7c0-3-4-4-6-1z" ${f(W)} style="stroke-width:2"/>
    <rect x="20" y="30" width="60" height="10" rx="4" ${f(L)}/>`),
  trophy: G(`
    <path d="M30 22H18c-2 16 4 22 14 22M70 22h12c2 16-4 22-14 22" style="fill:none;stroke-width:5"/>
    <path d="M28 10h44v20c0 18-10 28-22 28S28 48 28 30z" ${f(L)}/>
    <rect x="44" y="58" width="12" height="14" ${f(L)}/>
    <rect x="28" y="72" width="44" height="18" rx="3" ${f(K)}/>
    <polygon points="${starPts(50, 31, 11, 4.6)}" ${f(W)} style="stroke-width:2.5"/>
    <path d="M36 16v12" style="stroke:${W};stroke-width:3.5"/>`)
};

/* venue drawings for the tour map (120 x 100) */
const VG = body => `<g style="stroke:${K};stroke-width:3.5;stroke-linejoin:round;stroke-linecap:round">${body}</g>`;
const ground = `<path d="M4 94h112" style="stroke-width:4"/>`;
const VENUES = {
  coffee: VG(`
    <rect x="16" y="34" width="88" height="60" ${f(W)}/>
    ${[0, 1, 2, 3, 4, 5].map(i => `<rect x="${10 + i * 16.67}" y="24" width="16.67" height="14" ${f(i % 2 ? W : P)} style="stroke-width:3"/>`).join('')}
    <path d="M10 38${[0, 1, 2, 3, 4, 5].map(() => 'a8.33 6 0 0 0 16.67 0').join('')}" ${f(P)} style="stroke-width:3"/>
    <rect x="24" y="50" width="42" height="30" ${f(L)} style="stroke-width:3"/>
    <text x="45" y="70" text-anchor="middle" style="font:13px var(--hand);fill:${K};stroke:none">open mic</text>
    <rect x="74" y="50" width="20" height="44" ${f(K)}/>
    <circle cx="90" cy="73" r="1.8" ${f(W)} style="stroke:none"/>
    <path d="M60 10v8M78 10v8" style="stroke-width:2.5"/>
    <rect x="52" y="2" width="36" height="12" rx="3" ${f(K)}/>
    <text x="70" y="11.5" text-anchor="middle" style="font:700 8px var(--mono);fill:${PP};stroke:none">coffee</text>
    ${ground}`),
  records: VG(`
    <rect x="12" y="36" width="96" height="58" ${f(L)}/>
    <circle cx="60" cy="22" r="20" ${f(K)}/><circle cx="60" cy="22" r="13" style="fill:none;stroke:#444;stroke-width:1.5"/>
    <circle cx="60" cy="22" r="7" ${f(P)} style="stroke-width:2.5"/>
    <rect x="20" y="50" width="52" height="34" ${f(W)} style="stroke-width:3"/>
    ${[32, 46, 60].map(x => `<circle cx="${x}" cy="67" r="6" ${f(K)} style="stroke-width:2"/><circle cx="${x}" cy="67" r="2" ${f(P)} style="stroke:none"/>`).join('')}
    <rect x="80" y="50" width="20" height="44" ${f(K)}/>
    <path d="M12 44h96" style="stroke-width:3"/>
    <text x="40" y="44" text-anchor="middle" style="font:700 7px var(--mono);fill:${K};stroke:none">records</text>
    ${ground}`),
  rec: VG(`
    <path d="M6 40h108v54H6z" ${f(W)}/>
    <path d="M2 40l58-18 58 18z" ${f(P)}/>
    <rect x="14" y="48" width="92" height="16" ${f(K)}/>
    <text x="60" y="60" text-anchor="middle" style="font:12px var(--hand);fill:${W};stroke:none">battle of the bands</text>
    <rect x="46" y="70" width="28" height="24" ${f(L)} style="stroke-width:3"/>
    <path d="M60 70v24" style="stroke-width:3"/>
    <rect x="14" y="70" width="22" height="14" ${f(L)} style="stroke-width:3"/><rect x="84" y="70" width="22" height="14" ${f(L)} style="stroke-width:3"/>
    ${ground}`),
  rooftop: VG(`
    <path d="M6 66V44h18v22M96 66V36h18v30" ${f(L)} style="stroke-width:3"/>
    <rect x="4" y="66" width="112" height="28" ${f(W)}/>
    <path d="M4 72h112" style="stroke-width:3"/>
    <path d="M70 66V40M88 66V40M70 52h18" style="stroke-width:3"/>
    <rect x="64" y="20" width="30" height="22" rx="3" ${f(P)}/>
    <path d="M62 20l17-12 17 12z" ${f(K)}/>
    <path d="M8 30q26 22 56 6" style="fill:none;stroke-width:2"/>
    ${[[16, 37], [28, 43], [40, 44], [52, 41]].map(([x, y], i) => `<circle cx="${x}" cy="${y}" r="3.5" ${f(i % 2 ? L : W)} style="stroke-width:2"/>`).join('')}
    <rect x="20" y="78" width="14" height="10" ${f(K)} style="stroke-width:2"/><rect x="46" y="78" width="14" height="10" ${f(K)} style="stroke-width:2"/>
    ${ground}`),
  club: VG(`
    <rect x="10" y="22" width="100" height="72" ${f(K)}/>
    <polygon points="${starPts(60, 40, 15, 6.5)}" style="fill:none;stroke:${P};stroke-width:4"/>
    <text x="60" y="68" text-anchor="middle" style="font:15px var(--hand);fill:${L};stroke:none">the fuzz</text>
    <rect x="48" y="72" width="24" height="22" ${f(P)} style="stroke:${W};stroke-width:2.5"/>
    <path d="M30 94V80M90 94V80M30 82q30 10 60 0" style="fill:none;stroke:${W};stroke-width:2.5"/>
    <circle cx="30" cy="80" r="2.5" ${f(W)} style="stroke:none"/><circle cx="90" cy="80" r="2.5" ${f(W)} style="stroke:none"/>
    ${ground}`),
  theater: VG(`
    <rect x="14" y="40" width="92" height="54" ${f(P)}/>
    <path d="M6 22h108l-10 24H16z" ${f(W)}/>
    ${[16, 28, 40, 52, 64, 76, 88, 100].map(x => `<circle cx="${x + 2}" cy="20" r="2.6" ${f(L)} style="stroke-width:1.5"/>`).join('')}
    <text x="60" y="38" text-anchor="middle" style="font:13px var(--hand);fill:${K};stroke:none">tonight: sold out</text>
    <rect x="52" y="4" width="16" height="16" ${f(K)}/>
    <path d="M28 94V60q0-8 8-8t8 8v34M76 94V60q0-8 8-8t8 8v34" ${f(K)} style="stroke-width:3"/>
    <rect x="50" y="62" width="20" height="32" ${f(L)} style="stroke-width:3"/>
    ${ground}`),
  festival: VG(`
    <path d="M10 94V54L36 26l26 28v40z" ${f(W)}/>
    <path d="M58 94V50l26-32 26 32v44z" ${f(P)}/>
    <path d="M36 26v68M23 40v54M49 40v54" style="stroke:${P};stroke-width:5"/>
    <path d="M84 18v76M71 34v60M97 34v60" style="stroke:${W};stroke-width:5"/>
    <path d="M36 26V8l12 5-12 5M84 18V4l10 4-10 4" style="fill:${L};stroke-width:2.5"/>
    <path d="M4 52q32 14 58-2 26 16 54-2" style="fill:none;stroke-width:2"/>
    ${[[14, 56], [28, 60], [44, 58], [70, 58], [86, 60], [102, 56]].map(([x, y], i) => `<path d="M${x - 4} ${y}l4 7 4-7z" ${f(i % 2 ? L : K)} style="stroke-width:1.5"/>`).join('')}
    ${ground}`),
  arena: VG(`
    <polygon points="22,6 4,48 30,48" style="fill:${L};opacity:.55;stroke:none"/>
    <polygon points="98,6 90,48 116,48" style="fill:${P};opacity:.55;stroke:none"/>
    <path d="M6 94V60q54-40 108 0v34z" ${f(L)}/>
    <path d="M14 66q46-32 92 0M22 76q38-24 76 0" style="fill:none;stroke-width:3"/>
    <rect x="6" y="80" width="108" height="14" ${f(K)}/>
    <text x="60" y="91" text-anchor="middle" style="font:700 9px var(--mono);fill:${W};stroke:none;letter-spacing:2px">the arena</text>
    <polygon points="${starPts(60, 40, 9, 4)}" ${f(W)} style="stroke-width:2.5"/>
    ${ground}`)
};

/* plain line icons for controls */
const IG = body => `<g style="fill:none;stroke:currentColor;stroke-width:2.6;stroke-linecap:round;stroke-linejoin:round">${body}</g>`;
const ICONS = {
  'i-sound': IG(`<path d="M3 9v6h4l5 4V5L7 9z" style="fill:currentColor"/><path d="M16 9q2 3 0 6M19 6q4 6 0 12"/>`),
  'i-mute': IG(`<path d="M3 9v6h4l5 4V5L7 9z" style="fill:currentColor"/><path d="M16 9l6 6M22 9l-6 6"/>`),
  'i-x': IG(`<path d="M6 6l12 12M18 6L6 18"/>`),
  'i-back': IG(`<path d="M15 5l-7 7 7 7"/>`),
  'i-lock': IG(`<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>`),
  'i-undo': IG(`<path d="M9 7L4 12l5 5"/><path d="M4 12h10a6 6 0 0 1 0 12h-2" transform="translate(0 -6)"/>`),
  'i-trash': IG(`<path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13"/>`),
  'i-bulb': IG(`<path d="M9 18h6M10 21h4M12 3a6 6 0 0 0-4 10.5c.8.8 1 1.5 1 2.5h6c0-1 .2-1.7 1-2.5A6 6 0 0 0 12 3z"/>`)
};

export const mergeStyles = s => { let o; do { o = s; s = s.replace(/style="([^"]*)"\s+style="([^"]*)"/g, 'style="$1;$2"'); } while (s !== o); return s; };

export function spriteMarkup() {
  const sym = (id, vb, body) => `<symbol id="${id}" viewBox="${vb}">${mergeStyles(body)}</symbol>`;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="0" height="0" style="position:absolute" aria-hidden="true">
    <defs>
      <!-- sticker look: ink ring, white border, hard offset shadow -->
      <filter id="wt-stk" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">
        <feMorphology in="SourceAlpha" operator="dilate" radius="7" result="ring"/>
        <feMorphology in="SourceAlpha" operator="dilate" radius="5" result="pad"/>
        <feOffset in="ring" dx="3.5" dy="3.5" result="off"/>
        <feFlood style="flood-color:var(--ink)" result="ink"/>
        <feComposite in="ink" in2="off" operator="in" result="shadow"/>
        <feComposite in="ink" in2="ring" operator="in" result="ringc"/>
        <feFlood style="flood-color:var(--card)" result="paper"/>
        <feComposite in="paper" in2="pad" operator="in" result="padc"/>
        <feMerge><feMergeNode in="shadow"/><feMergeNode in="ringc"/><feMergeNode in="padc"/><feMergeNode in="SourceGraphic"/></feMerge>
      </filter>
      <filter id="wt-stk-sm" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">
        <feMorphology in="SourceAlpha" operator="dilate" radius="4.5" result="ring"/>
        <feMorphology in="SourceAlpha" operator="dilate" radius="3" result="pad"/>
        <feOffset in="ring" dx="2.5" dy="2.5" result="off"/>
        <feFlood style="flood-color:var(--ink)" result="ink"/>
        <feComposite in="ink" in2="off" operator="in" result="shadow"/>
        <feComposite in="ink" in2="ring" operator="in" result="ringc"/>
        <feFlood style="flood-color:var(--card)" result="paper"/>
        <feComposite in="paper" in2="pad" operator="in" result="padc"/>
        <feMerge><feMergeNode in="shadow"/><feMergeNode in="ringc"/><feMergeNode in="padc"/><feMergeNode in="SourceGraphic"/></feMerge>
      </filter>
    </defs>
    ${Object.entries(STICKERS).map(([k, v]) => sym('s-' + k, '0 0 100 100', v)).join('')}
    ${Object.entries(VENUES).map(([k, v]) => sym('v-' + k, '0 0 120 100', v)).join('')}
    ${Object.entries(ICONS).map(([k, v]) => sym(k, '0 0 24 24', v)).join('')}
  </svg>`;
}

export const STICKER_IDS = Object.keys(STICKERS);
export const VENUE_ART = Object.keys(VENUES);

export function mountSprite() {
  if (document.getElementById('wt-sprite')) return;
  const d = document.createElement('div');
  d.id = 'wt-sprite';
  d.innerHTML = spriteMarkup();
  document.body.prepend(d);
}
