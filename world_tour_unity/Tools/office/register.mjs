import { register } from 'node:module';
register('./hooks.mjs', import.meta.url);
/* store.js, progress.js and the modes touch window, document and addEventListener at load: inert stand-ins
   (no localStorage, so the store runs on its in-memory copy and never writes anywhere) */
globalThis.window = {};
globalThis.addEventListener = () => {};
globalThis.removeEventListener = () => {};
globalThis.document = {
  visibilityState: 'hidden', dispatchEvent() {}, addEventListener() {},
  documentElement: { classList: { contains: () => false, add() {}, remove() {} }, dataset: {}, style: { setProperty() {} } },
  body: { classList: { add() {}, remove() {} } }, querySelector: () => null
};
globalThis.matchMedia = () => ({ matches: false, addEventListener() {} });
globalThis.speechSynthesis = undefined;
