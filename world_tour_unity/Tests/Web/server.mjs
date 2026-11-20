// Plain static file server, no dependencies. Behaves like GitHub Pages without its extras:
// it sends Content-Type and Content-Length only (no Content-Encoding, no cache headers).
//
//   node Tests/Web/server.mjs <folder> [port]      (port 0 or omitted picks a free port)
//
// Also imported by run.mjs: startServer(folder) -> { url, port, bytesServed, files, resetCounters(), close() }
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.json': 'application/json',
  '.txt': 'text/plain; charset=utf-8',
  '.css': 'text/css',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.wasm': 'application/wasm',
};

export function startServer(folder, port = 0) {
  const root = path.resolve(folder);
  const state = { bytesServed: 0, files: [] };
  const server = http.createServer((req, res) => {
    let rel;
    try { rel = decodeURIComponent(new URL(req.url, 'http://x').pathname); } catch { res.writeHead(400); return res.end(); }
    if (rel.endsWith('/')) rel += 'index.html';
    const file = path.join(root, rel);
    if (file !== root && !file.startsWith(root + path.sep)) { res.writeHead(403); return res.end(); }
    fs.readFile(file, (err, buf) => {
      if (err) { res.writeHead(404, { 'Content-Type': 'text/plain' }); return res.end('not found'); }
      res.writeHead(200, {
        'Content-Type': TYPES[path.extname(file).toLowerCase()] || 'application/octet-stream',
        'Content-Length': buf.length,
      });
      res.end(buf);
      state.bytesServed += buf.length;
      state.files.push({ path: rel, bytes: buf.length });
    });
  });
  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, '127.0.0.1', () => {
      const p = server.address().port;
      resolve({
        url: `http://127.0.0.1:${p}/`,
        port: p,
        get bytesServed() { return state.bytesServed; },
        get files() { return state.files.slice(); },
        resetCounters() { state.bytesServed = 0; state.files = []; },
        close() { return new Promise(r => { server.closeAllConnections?.(); server.close(() => r()); }); },
      });
    });
  });
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const folder = process.argv[2];
  if (!folder) { console.error('usage: node server.mjs <folder> [port]'); process.exit(2); }
  const s = await startServer(folder, Number(process.argv[3] || 0));
  console.log(`serving ${path.resolve(folder)} at ${s.url} (ctrl+c to stop)`);
}
