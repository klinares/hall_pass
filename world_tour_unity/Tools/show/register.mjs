import { register } from 'node:module';
register('./hooks.mjs', import.meta.url);
globalThis.window = {};
globalThis.addEventListener = () => {};
globalThis.document = { visibilityState: 'visible', body: { classList: { add() {}, remove() {} } }, dispatchEvent() {} };
