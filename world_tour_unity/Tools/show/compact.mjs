import fs from 'node:fs';
const txt = fs.readFileSync(process.argv[2], 'utf8').split('### ').filter(Boolean);
for (const block of txt) {
  const [name, json] = block.split('\n'); const o = JSON.parse(json);
  const log = o.log.map(x => x === 'encore' ? 'E' : x.join(',')).join('|');
  const t = s => s ? [s.q.join(' '), s.fans, s.first, s.hintOnly, s.best, s.hintTotal, s.hinted.join(' '), s.missed.join(' '), s.pct, s.n, s.show.sold ? 1 : 0, s.show.encore ? 1 : 0, s.show.hinted].join(';') : '';
  const k = o.skill; const sk = [k.level, k.run, k.missRun, k.a, k.c, k.h, [1,2,3].map(i => k.byLevel[i].join('/')).join(' ')].join(';');
  console.log(`            new Pinned("${name}",\n                "${log}",\n                "${t(o.t1)}",\n                "${t(o.t2)}",\n                "${sk}"),`);
}
