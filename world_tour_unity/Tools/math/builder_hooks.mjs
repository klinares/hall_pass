// Loader hooks: swap the UI-only modules the math modes import for no-op stubs. Nothing on disk is changed.
const STUBS = {
  'js/sound.js': 'export const tap=()=>{globalThis.__ev&&globalThis.__ev.push(["tap"])},strum=()=>{},thud=()=>{},crowd=()=>{},tick=()=>{globalThis.__ev&&globalThis.__ev.push(["tick"])};',
  'js/motion.js': 'export const pop=()=>{globalThis.__ev&&globalThis.__ev.push(["pop"])},wobble=()=>{globalThis.__ev&&globalThis.__ev.push(["wobble"])},slap=()=>{},hop=()=>{},reduced=()=>true;',
};
export async function resolve(spec, ctx, next) {
  const r = await next(spec, ctx);
  for (const [k, src] of Object.entries(STUBS))
    if (r.url.endsWith('/world_tour/' + k)) return { url: 'data:text/javascript,' + encodeURIComponent(src), shortCircuit: true };
  return r;
}
