// Phase 8.4: time to the first playable place (WT_WORLD_GAME_READY) on a cold and then a warm cache, same browser context.
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { startServer } from './server.mjs';
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..', '..');
const { chromium } = createRequire(path.join(here, 'package.json'))('playwright');
const server = await startServer(path.join(repo, 'world'));
const browser = await chromium.launch({ headless: true, args: ['--use-gl=angle', '--use-angle=d3d11', '--ignore-gpu-blocklist'] });
const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
for (const label of ['cold', 'warm1', 'warm2']) {
  const page = await ctx.newPage(); let t0 = Date.now(), ready = null; server.resetCounters();
  page.on('console', m => { if (m.text().startsWith('WT_WORLD_GAME_READY') && !ready) ready = Date.now() - t0; });
  await page.goto(server.url, { waitUntil: 'load' });
  while (!ready && Date.now() - t0 < 90000) await new Promise(r => setTimeout(r, 50));
  console.log(label, 'ready ms', ready, 'bytes fetched', server.bytesServed);
  await page.close();
}
await browser.close(); await server.close();
