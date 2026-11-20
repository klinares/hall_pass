// Loader hooks: swap the UI-only modules show.js imports for no-op stubs. Nothing on disk is changed.
const STUBS = {
  'js/sound.js': 'export const tap=()=>{},strum=()=>{},thud=()=>{},crowd=()=>{},tick=()=>{};',
  'js/fx.js': 'export const burst=()=>{},toast=()=>{};',
  'art/dog.js': 'export const dog=()=>"";',
  'art/sticker.js': 'export const icon=()=>"";',
  'js/motion.js': 'export const wobble=()=>{},hop=()=>{},reduced=()=>true;',
  'screens/ticket.js': 'export const renderTicket=(root,a)=>globalThis.__ticket(a);',
};
export async function resolve(spec, ctx, next) {
  const r = await next(spec, ctx);
  for (const [k, src] of Object.entries(STUBS))
    if (r.url.endsWith('/world_tour/' + k)) return { url: 'data:text/javascript,' + encodeURIComponent(src), shortCircuit: true };
  return r;
}
