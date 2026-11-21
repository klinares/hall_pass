// Loader hooks for office_vectors.mjs: swap the browser-only modules that screens/office.js and modes/index.js
// import for no-op stubs. Nothing on disk is changed.
const STUBS = {
  'js/sound.js': 'export const setEnabled=()=>{},enabled=()=>false,unlock=()=>{},strum=()=>{},thud=()=>{},tap=()=>{},pop=()=>{},peel=()=>{},crowd=()=>{},beat=()=>0,tick=()=>{};',
  'js/motion.js': 'export const pop=()=>{},wobble=()=>{},slap=()=>{},hop=()=>{},peelOn=()=>{},shrink=()=>{},slideIn=()=>{},reduced=()=>true;',
  'js/speech.js': 'export const canSpeak=()=>false,warmUp=()=>{},say=()=>false,hush=()=>{};',
  'art/sticker.js': 'export const sticker=()=>"",icon=()=>"",venueArt=()=>"",scribble=()=>"",journalArt=()=>"";',
  'art/dog.js': 'export const setOutfit=()=>{},dog=()=>"",setPose=()=>{};',
  'js/look.js': 'export const applyLook=()=>({});',
  'js/fx.js': 'export const burst=()=>{},toast=()=>{};',
};
export async function resolve(spec, ctx, next) {
  const r = await next(spec, ctx);
  for (const [k, src] of Object.entries(STUBS))
    if (r.url.endsWith('/world_tour/' + k)) return { url: 'data:text/javascript,' + encodeURIComponent(src), shortCircuit: true };
  return r;
}
