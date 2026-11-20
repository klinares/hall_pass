/* Writes Assets/StreamingAssets/math_config.json from the web game's world_tour/math/config.js
   (imported read-only), with the same fields and values.

   node Tools/config_to_json.mjs            writes the project copy
   node Tools/config_to_json.mjs --stdout   prints it instead

   After the first build, the deployed tour/StreamingAssets/math_config.json is the source of truth
   (BuildWeb copies it back here). Edit that copy, or delete it so the next build recreates it. */
import { writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { ENABLED_SKILLS, BOUNDS } from '../../world_tour/math/config.js';

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, '..', 'Assets', 'StreamingAssets');
const json = JSON.stringify({ ENABLED_SKILLS, BOUNDS }, null, 2) + '\n';
if (process.argv.includes('--stdout')) process.stdout.write(json);
else { mkdirSync(outDir, { recursive: true }); writeFileSync(join(outDir, 'math_config.json'), json); console.log('wrote ' + join(outDir, 'math_config.json')); }
