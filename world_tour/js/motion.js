/* Squishy motion via the Web Animations API. Every animation is under 400ms,
   never awaited by input handlers, and skipped when reduced motion is on. */
const mq = matchMedia('(prefers-reduced-motion: reduce)');
export const reduced = () => mq.matches || document.documentElement.dataset.motion === 'reduced';

function run(el, frames, opts) {
  if (!el || !el.animate) return null;
  if (reduced()) return el.animate([{ opacity: .6 }, { opacity: 1 }], { duration: 120 });
  return el.animate(frames, { duration: 300, easing: 'cubic-bezier(.34,1.56,.64,1)', ...opts });
}

const rot = el => getComputedStyle(el).getPropertyValue('--r').trim() || '0deg';

/* correct answer: pop with overshoot */
export const pop = el => run(el, [
  { transform: `rotate(${rot(el)}) scale(1)` },
  { transform: `rotate(${rot(el)}) scale(1.22)`, offset: .45 },
  { transform: `rotate(${rot(el)}) scale(1)` }], { duration: 340 });

/* wrong answer: short wobble, no red flash */
export const wobble = el => run(el, [
  { transform: 'translateX(0) rotate(0)' }, { transform: 'translateX(-7px) rotate(-2deg)' },
  { transform: 'translateX(6px) rotate(1.5deg)' }, { transform: 'translateX(-3px) rotate(-.5deg)' },
  { transform: 'translateX(0) rotate(0)' }], { duration: 340, easing: 'ease-out' });

/* letter or sticker landing: drops in, squashes, settles */
export const slap = el => run(el, [
  { transform: 'translateY(-14px) scale(1.25) rotate(-6deg)', opacity: 0 },
  { transform: 'translateY(0) scale(.9,1.08) rotate(2deg)', opacity: 1, offset: .55 },
  { transform: 'translateY(0) scale(1) rotate(0)' }], { duration: 260 });

/* sticker peeling on: lifts from a corner then slaps down */
export const peelOn = el => run(el, [
  { transform: `rotate(${rot(el)}) perspective(300px) rotateX(55deg) rotateY(-25deg) scale(1.15)`, opacity: 0 },
  { transform: `rotate(${rot(el)}) perspective(300px) rotateX(-6deg) rotateY(4deg) scale(1.05)`, opacity: 1, offset: .6 },
  { transform: `rotate(${rot(el)}) scale(1)` }], { duration: 380 });

/* tile leaving the rack */
export const shrink = el => run(el, [
  { transform: `rotate(${rot(el)}) scale(1)` }, { transform: `rotate(${rot(el)}) scale(0)`, opacity: 0 }], { duration: 160, easing: 'ease-in' });

/* new screen: a quick paper slide */
export const slideIn = el => run(el, [
  { transform: 'translateX(28px) rotate(.8deg)', opacity: 0 },
  { transform: 'translateX(0) rotate(0)', opacity: 1 }], { duration: 280, easing: 'cubic-bezier(.2,.8,.2,1)' });

export const hop = el => run(el, [
  { transform: 'translateY(0)' }, { transform: 'translateY(-16px) scale(1.04,.96)', offset: .4 }, { transform: 'translateY(0)' }], { duration: 380 });
