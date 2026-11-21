/* The tour map: venue positions for two layouts, the marker route between them,
   and the doodles scattered in the empty spaces. */

/* portrait: winds down the page. landscape: two rows, out and back. */
export const LAYOUTS = {
  portrait: {
    w: 360, h: 1225,
    pts: [105, 250, 395, 540, 685, 830, 975, 1120].map((y, i) => [i % 2 ? 262 : 98, y]),
    via: {},
    decor: [['note', 262, 118, 12], ['heart', 96, 262, -10], ['bolt', 268, 405, 8], ['vinyl', 92, 548, -6],
            ['pick', 270, 690, 14], ['star', 92, 838, -12], ['phones', 270, 982, 6], ['star', 90, 1130, 10]]
  },
  landscape: {
    w: 1000, h: 610,
    pts: [[120, 150], [370, 128], [628, 158], [880, 136], [880, 446], [628, 470], [370, 446], [120, 462]],
    via: { 3: [985, 291] },     /* swing wide between the rows */
    decor: [['note', 248, 300, 12], ['heart', 505, 340, -8], ['bolt', 752, 302, 10], ['vinyl', 40, 300, -6],
            ['star', 496, 40, 8], ['pick', 246, 568, -14], ['phones', 754, 580, 6]]
  }
};

/* smooth curve through points (Catmull-Rom → cubic Bézier) */
export function curve(points) {
  if (points.length < 2) return '';
  let d = `M${points[0][0]} ${points[0][1]}`;
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[i - 1] || points[i], p1 = points[i], p2 = points[i + 1], p3 = points[i + 2] || p2;
    const c1 = [p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6];
    const c2 = [p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6];
    d += ` C${c1[0].toFixed(1)} ${c1[1].toFixed(1)} ${c2[0].toFixed(1)} ${c2[1].toFixed(1)} ${p2[0]} ${p2[1]}`;
  }
  return d;
}

/* route points including the swing-wide waypoints, cut off after venue index `upto` */
export function routePoints(L, upto = L.pts.length - 1) {
  const out = [];
  L.pts.forEach((p, i) => { if (i <= upto) { out.push(p); if (L.via[i] && i < upto) out.push(L.via[i]); } });
  return out;
}

export function mapSVG(L, reached) {
  const all = curve(routePoints(L));
  const done = reached > 0 ? curve(routePoints(L, reached)) : '';
  return `<svg class="route" viewBox="0 0 ${L.w} ${L.h}" preserveAspectRatio="none" aria-hidden="true">
    <path d="${all}" style="fill:none;stroke:var(--card);stroke-width:18;stroke-linecap:round;opacity:.9"/>
    ${done ? `<path d="${done}" style="fill:none;stroke:var(--accent);stroke-width:12;stroke-linecap:round"/>` : ''}
    <path d="${all}" style="fill:none;stroke:var(--ink);stroke-width:3.5;stroke-linecap:round;stroke-dasharray:10 11"/>
  </svg>`;
}
