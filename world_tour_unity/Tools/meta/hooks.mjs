// Loader hooks for Tools/meta/backstage_vectors.mjs: swap the browser-only modules world_tour/screens/backstage.js
// imports for stubs that record what happened. Nothing on disk is changed.
const STUBS = {
  'js/sound.js': 'export const tap=()=>{globalThis.__ev.push("tap")},strum=()=>{globalThis.__ev.push("strum")},thud=()=>{globalThis.__ev.push("thud")},pop=()=>{globalThis.__ev.push("pop")},peel=()=>{},crowd=()=>{},tick=()=>{};',
  'js/motion.js': 'export const pop=()=>{},wobble=()=>{globalThis.__ev.push("wobble")},slap=()=>{},hop=()=>{},peelOn=()=>{},shrink=()=>{},slideIn=()=>{},reduced=()=>true;',
  'js/fx.js': 'export const burst=()=>{},toast=t=>{globalThis.__ev.push("toast:"+t)};',
  'js/look.js': 'export const applyLook=()=>globalThis.__applyLook();',
  'art/sticker.js': 'export const sticker=()=>"",icon=()=>"",venueArt=()=>"",scribble=()=>"",journalArt=()=>"";',
  'art/dog.js': 'export const dog=()=>"",setOutfit=()=>{},setPose=()=>{};',
};
export async function resolve(spec, ctx, next) {
  const r = await next(spec, ctx);
  for (const [k, src] of Object.entries(STUBS))
    if (r.url.endsWith('/world_tour/' + k)) return { url: 'data:text/javascript,' + encodeURIComponent(src), shortCircuit: true };
  return r;
}
