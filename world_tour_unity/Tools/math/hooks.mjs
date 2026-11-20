// Loader hooks: swap the browser-only modules the math modes import for no-op stubs. Nothing on disk is changed.
const STUBS = {
  'js/sound.js': 'export const tap=()=>{},strum=()=>{},thud=()=>{},crowd=()=>{},tick=()=>{},pop=()=>{},peel=()=>{},beat=()=>0;',
  'js/motion.js': 'export const pop=()=>{},wobble=()=>{},slap=()=>{},hop=()=>{},peelOn=()=>{},shrink=()=>{},slideIn=()=>{},reduced=()=>true;',
  'art/sticker.js': 'export const sticker=()=>"",icon=()=>"",venueArt=()=>"",scribble=()=>"",journalArt=()=>"";',
};
export async function resolve(spec, ctx, next) {
  const r = await next(spec, ctx);
  for (const [k, src] of Object.entries(STUBS))
    if (r.url.endsWith('/world_tour/' + k)) return { url: 'data:text/javascript,' + encodeURIComponent(src), shortCircuit: true };
  return r;
}
