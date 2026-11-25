// 8.4 tester probe: a fresh save at 1366x768, what the player sees first. Not part of the suites.
import path from 'node:path'; import fs from 'node:fs';
import { createRequire } from 'node:module'; import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
const here = path.dirname(fileURLToPath(import.meta.url)); const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(repo, 'world_tour', 'tests', 'e2e', 'package.json'))('playwright');
const out = process.env.OUT || '.'; const sleep = ms => new Promise(r => setTimeout(r, ms));
const server = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl'] });
const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } }); const page = await ctx.newPage();
const lines = []; page.on('console', m => { lines.push(m.text()); if (/shader/i.test(m.text())) console.log('SHADERMSG type=' + m.type() + ' ' + JSON.stringify(m.text()).slice(0, 400)); }); page.on('pageerror', e => lines.push('PAGEERROR ' + e.message));
const t0 = Date.now();
await page.goto(server.url + (process.env.Q || '?wt_test=1'), { waitUntil: 'load' });
for (let i = 0; i < 3; i++) { await sleep(2500); await page.screenshot({ path: path.join(out, `fresh_${String(i).padStart(2, '0')}.png`) }); }
console.log('load->12 shots', Date.now() - t0, 'ms');
console.log(lines.filter(l => /^WT_|error|PAGEERROR/i.test(l)).map(l => l.replace(/\s+/g, ' ').slice(0, 200)).join('\n'));
await browser.close(); server.close();
