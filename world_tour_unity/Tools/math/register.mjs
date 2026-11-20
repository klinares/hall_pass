import { register } from 'node:module';
register('./hooks.mjs', import.meta.url);
globalThis.window = {};
globalThis.__keys = [];
globalThis.addEventListener = (type, fn) => { if (type === 'keydown') globalThis.__keys.push(fn); };
globalThis.removeEventListener = (type, fn) => { globalThis.__keys = globalThis.__keys.filter(f => f !== fn); };
globalThis.document = { documentElement: { classList: { contains: () => false }, dataset: {} }, body: { classList: { add() {}, remove() {} } } };
globalThis.matchMedia = () => ({ matches: true });
