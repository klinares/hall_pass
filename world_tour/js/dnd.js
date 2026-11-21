/* Drag and drop that also works as tap-to-select, then tap-to-place.
   Nothing depends on hover. Pointer events cover mouse, pen, and touch.

   makeDnD({ items, zones, onDrop(item, zone) })
     items: elements she can pick up; zones: elements she can drop on.
     A press that moves less than 8px is a tap: it selects the item (tap again to deselect),
     and the next tap on a zone places it.
     onTap(item), if given, replaces select-then-place: a tap acts at once (beat maker:
     tapping a note block adds it), while dragging still works. */
export function makeDnD({ items, zones, onDrop, onTap = null }) {
  let selected = null, drag = null;

  const select = el => {
    if (selected) selected.classList.remove('selected');
    selected = el && el !== selected ? el : null;
    if (selected) selected.classList.add('selected');
    zones.forEach(z => z.classList.toggle('armed', !!selected));
  };
  const zoneAt = (x, y) => zones.find(z => { const r = z.getBoundingClientRect(); return x >= r.left - 12 && x <= r.right + 12 && y >= r.top - 12 && y <= r.bottom + 12; });

  items.forEach(el => {
    el.addEventListener('pointerdown', e => {
      if (el.disabled || e.button > 0) return;
      const r = el.getBoundingClientRect();
      drag = { el, id: e.pointerId, x0: e.clientX, y0: e.clientY, dx: e.clientX - r.left, dy: e.clientY - r.top, ghost: null, w: r.width, h: r.height };
      try { el.setPointerCapture(e.pointerId); } catch (err) { /* older Safari */ }
    });
    el.addEventListener('pointermove', e => {
      if (!drag || drag.el !== el || e.pointerId !== drag.id) return;
      const moved = Math.hypot(e.clientX - drag.x0, e.clientY - drag.y0);
      if (!drag.ghost && moved > 8) {
        const g = el.cloneNode(true);
        g.classList.add('ghost'); g.removeAttribute('id'); g.setAttribute('aria-hidden', 'true');
        Object.assign(g.style, { position: 'fixed', left: 0, top: 0, width: drag.w + 'px', height: drag.h + 'px', margin: 0, zIndex: 30, pointerEvents: 'none' });
        document.body.appendChild(g);
        drag.ghost = g; el.classList.add('lifted'); select(null);
        zones.forEach(z => z.classList.add('armed'));
      }
      if (drag.ghost) {
        e.preventDefault();
        drag.ghost.style.transform = `translate(${e.clientX - drag.dx}px, ${e.clientY - drag.dy}px) rotate(-4deg) scale(1.08)`;
        const z = zoneAt(e.clientX, e.clientY);
        zones.forEach(q => q.classList.toggle('over', q === z));
      }
    });
    const end = e => {
      if (!drag || drag.el !== el) return;
      const d = drag; drag = null;
      if (d.ghost) {
        d.ghost.remove(); el.classList.remove('lifted');
        zones.forEach(q => q.classList.remove('over', 'armed'));
        const z = e.type === 'pointerup' ? zoneAt(e.clientX, e.clientY) : null;
        if (z) onDrop(el, z);
      } else if (e.type === 'pointerup') {
        if (onTap) onTap(el); else select(el);
      }
    };
    el.addEventListener('pointerup', end);
    el.addEventListener('pointercancel', end);
    /* keyboard: enter/space selects like a tap */
    el.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); if (onTap) onTap(el); else select(el); } });
    el.addEventListener('click', e => e.preventDefault());
  });

  zones.forEach(z => {
    const place = () => { if (selected) { const s = selected; select(null); onDrop(s, z); } };
    z.addEventListener('click', place);
    z.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); place(); } });
  });

  return { clear: () => select(null), get selected() { return selected; } };
}
