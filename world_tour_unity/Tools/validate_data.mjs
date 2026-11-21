/* Checks weeks.json and math_config.json the way the game does when it loads them, so a week typed by hand
   can be checked before pushing (ADVISOR 00:12 #4).

   node world_tour_unity/Tools/validate_data.mjs [weeks.json] [math_config.json]

   With no paths it checks the deployed copies in tour/StreamingAssets/, and for a file that isn't deployed yet,
   the project copy in world_tour_unity/Assets/StreamingAssets/. Prints "ok", or a numbered list of problems.
   Exit code 1 when there is an error (the game would refuse the file); warnings alone exit 0.

   Same three steps and the same messages as the C# loader (Assets/Core/Data: StrictJson.cs, DataFiles.cs,
   Weeks.cs, WeekRules.cs): strict parse, then raw keys and types, then the meaning rules.
   Keep the two in step: after changing a rule here or there, run
     node world_tour_unity/Tools/validate_data.mjs --parity <samples dir>
   which rewrites <samples dir>/expected_findings.json from these rules; the C# test DataParityTests then
   checks the C# rules give exactly the same findings for every sample file. --parity-check only compares. */
import { readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';

/* ---------------- strict JSON reader (mirror of StrictJson.cs) ---------------- */

class Fail extends Error { constructor(line, column, message) { super(message); this.line = line; this.column = column; } }
const MAX_DEPTH = 64;   /* mirror of StrictJson.MaxDepth: deeper text is refused, never a stack overflow */
const NUMBER_SHAPE = /^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$/;
const isWordChar = c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c === '_';
const hexDigit = c => (c >= '0' && c <= '9') ? c.charCodeAt(0) - 48 : (c >= 'a' && c <= 'f') ? c.charCodeAt(0) - 87 : (c >= 'A' && c <= 'F') ? c.charCodeAt(0) - 55 : -1;

export function parseStrict(text) {
  const s = text ?? '';
  let i = 0, line = 1, lineStart = 0;
  const open = [];
  if (s.length > 0 && s.charCodeAt(0) === 0xfeff) { i = 1; lineStart = 1; }
  const col = at => at - lineStart + 1;
  const at = (pos, msg) => new Fail(line, col(pos), msg);
  const skipWs = () => {
    while (i < s.length) {
      const c = s[i];
      if (c === '\n') { i++; line++; lineStart = i; }
      else if (c === ' ' || c === '\t' || c === '\r') i++;
      else break;
    }
  };
  const eof = () => {
    if (open.length === 0) return new Fail(0, 0, 'the file is empty');
    const o = open[open.length - 1];
    return new Fail(o.line, o.column, `this ${o.c} is never closed`);
  };
  const word = () => { const start = i; while (i < s.length && isWordChar(s[i])) i++; return s.slice(start, i); };
  const getField = (node, key) => node.fields.find(f => f.key === key);

  function value() {
    skipWs();
    if (i >= s.length) throw eof();
    const c = s[i];
    if (c === '{') return obj();
    if (c === '[') return arr();
    if (c === '"') { const l = line, cl = col(i); return { kind: 'string', line: l, column: cl, text: str() }; }
    if (c === '-' || (c >= '0' && c <= '9')) return num();
    if (isWordChar(c)) {
      const p = i, w = word();
      if (w === 'true' || w === 'false') return { kind: 'bool', line, column: col(p), bool: w === 'true' };
      if (w === 'null') return { kind: 'null', line, column: col(p) };
      throw at(p, `"${w}" is not a value: text needs double quotes, and true, false and null are lowercase`);
    }
    if (c === "'") throw at(i, 'use double quotes (") around text, not single quotes');
    if (c === '/') throw at(i, "JSON files can't have comments");
    throw at(i, `expected a value here, but found "${c}"`);
  }
  function obj() {
    const node = { kind: 'object', line, column: col(i), fields: [] };
    if (open.length >= MAX_DEPTH) throw at(i, `the data is too deeply nested (more than ${MAX_DEPTH} levels of { } and [ ])`);
    open.push({ c: '{', line, column: col(i) });
    i++;
    skipWs();
    if (i >= s.length) throw eof();
    if (s[i] === '}') { i++; open.pop(); return node; }
    let commaLine = 0, commaCol = 0;
    for (;;) {
      skipWs();
      if (i >= s.length) throw eof();
      let c = s[i];
      if (c !== '"') {
        if (c === '}' && commaLine > 0) throw new Fail(commaLine, commaCol, 'there is an extra comma here, before }');
        if (c === "'") throw at(i, 'use double quotes (") around text, not single quotes');
        if (isWordChar(c)) { const p = i; throw at(p, `field names need double quotes, like "${word()}"`); }
        if (c === '/') throw at(i, "JSON files can't have comments");
        throw at(i, `expected a field name in double quotes, but found "${c}"`);
      }
      const keyLine = line, keyCol = col(i);
      const key = str();
      if (getField(node, key)) throw new Fail(keyLine, keyCol, `"${key}" appears twice in the same { }`);
      skipWs();
      if (i >= s.length) throw eof();
      if (s[i] !== ':') throw at(i, `expected : after "${key}"`);
      i++;
      const v = value();
      node.fields.push({ key, line: keyLine, column: keyCol, value: v });
      skipWs();
      if (i >= s.length) throw eof();
      c = s[i];
      if (c === ',') { commaLine = line; commaCol = col(i); i++; continue; }
      if (c === '}') { i++; open.pop(); return node; }
      if (c === '"') throw at(i, 'a comma is missing before this field');
      if (c === ']') throw at(i, `found ] where } was expected; the { on line ${node.line} is not closed`);
      if (isWordChar(c)) throw at(i, `expected , or } here, but found "${c}"; check the quotes just before it`);
      throw at(i, `expected , or } here, but found "${c}"`);
    }
  }
  function arr() {
    const node = { kind: 'array', line, column: col(i), items: [] };
    if (open.length >= MAX_DEPTH) throw at(i, `the data is too deeply nested (more than ${MAX_DEPTH} levels of { } and [ ])`);
    open.push({ c: '[', line, column: col(i) });
    i++;
    skipWs();
    if (i >= s.length) throw eof();
    if (s[i] === ']') { i++; open.pop(); return node; }
    let commaLine = 0, commaCol = 0;
    for (;;) {
      skipWs();
      if (i >= s.length) throw eof();
      if (s[i] === ']' && commaLine > 0) throw new Fail(commaLine, commaCol, 'there is an extra comma here, before ]');
      node.items.push(value());
      skipWs();
      if (i >= s.length) throw eof();
      const c = s[i];
      if (c === ',') { commaLine = line; commaCol = col(i); i++; continue; }
      if (c === ']') { i++; open.pop(); return node; }
      if (c === '{' || c === '[' || c === '"' || c === '-' || isWordChar(c)) throw at(i, 'a comma is missing before this item');
      if (c === '}') throw at(i, `found } where ] was expected; the [ on line ${node.line} is not closed`);
      throw at(i, `expected , or ] here, but found "${c}"`);
    }
  }
  function str() {
    const quote = i;
    i++;
    let out = '';
    for (;;) {
      if (i >= s.length || s[i] === '\n' || s[i] === '\r') throw at(quote, 'this text has no closing quote');
      const c = s[i];
      if (c === '"') { i++; return out; }
      if (c < ' ') throw at(i, "a tab or other control character can't go inside quotes");
      if (c !== '\\') { out += c; i++; continue; }
      if (i + 1 >= s.length) throw at(quote, 'this text has no closing quote');
      const e = s[i + 1];
      const simple = { '"': '"', '\\': '\\', '/': '/', b: '\b', f: '\f', n: '\n', r: '\r', t: '\t' };
      if (e in simple) out += simple[e];
      else if (e === 'u') {
        let code = 0;
        for (let k = 0; k < 4; k++) {
          const d = i + 2 + k < s.length ? hexDigit(s[i + 2 + k]) : -1;
          if (d < 0) throw at(i, 'a backslash-u code inside quotes needs 4 hex digits');
          code = code * 16 + d;
        }
        out += String.fromCharCode(code);
        i += 4;
      } else throw at(i, `"\\${e}" is not allowed inside quotes; to write a backslash, type two of them`);
      i += 2;
    }
  }
  function num() {
    const start = i;
    while (i < s.length && ((s[i] >= '0' && s[i] <= '9') || s[i] === '-' || s[i] === '+' || s[i] === '.' || s[i] === 'e' || s[i] === 'E')) i++;
    const raw = s.slice(start, i);
    if (!NUMBER_SHAPE.test(raw)) throw at(start, `"${raw}" is not a valid number`);
    return { kind: 'number', line, column: col(start), text: raw };
  }

  try {
    skipWs();
    if (i >= s.length) throw eof();
    const root = value();
    skipWs();
    if (i < s.length) throw at(i, 'there is extra text after the end of the data');
    return { root, error: null };
  } catch (e) {
    if (!(e instanceof Fail)) throw e;
    return { root: null, error: { line: e.line, column: e.column, message: e.message } };
  }
}

/* ---------------- findings ---------------- */

/* ---------------- text rules (mirror of TextRules.cs: explicit character sets, no locale lowercasing, no trim()) ---------------- */

const isSpace = c => { const n = c.charCodeAt(0); return (n >= 0x09 && n <= 0x0d) || n === 0x20 || n === 0xa0 || n === 0x1680 || (n >= 0x2000 && n <= 0x200a)
  || n === 0x2028 || n === 0x2029 || n === 0x202f || n === 0x205f || n === 0x3000 || n === 0xfeff; };
const hex4 = n => 'U+' + n.toString(16).toUpperCase().padStart(4, '0');
const firstNonLetter = w => { for (let i = 0; i < w.length; i++) if (w[i] < 'a' || w[i] > 'z') return i; return -1; };
const nameChar = c => { const n = c.charCodeAt(0); return c === ' ' ? 'a space' : (n > 0x20 && n < 0x7f) ? `"${c}"` : hex4(n); };
const asciiLower = s => s.replace(/[A-Z]/g, c => String.fromCharCode(c.charCodeAt(0) + 32));
const isInvisible = n => n < 0x20 || (n >= 0x7f && n <= 0x9f) || n === 0xa0 || n === 0xad || n === 0x1680 || n === 0x180e
  || (n >= 0x2000 && n <= 0x200f) || (n >= 0x2028 && n <= 0x202f) || (n >= 0x205f && n <= 0x2064) || n === 0x3000 || n === 0xfeff;
const visible = s => { let o = ''; for (const c of s.split('')) o += isInvisible(c.charCodeAt(0)) ? hex4(c.charCodeAt(0)) : c; return o; };

const finding = (severity, file, line, column, message) => ({ severity, file, line, column, message: visible(message) });
const err = (file, line, msg) => finding('E', file, line, 0, msg);
const warn = (file, line, msg) => finding('W', file, line, 0, msg);
export const parityKey = f => `${f.severity}|${f.line}|${f.column}|${f.message}`;
export const show = f => {
  let where = f.file || '';
  if (f.line > 0) where += (where ? ', ' : '') + `line ${f.line}` + (f.column > 0 ? `, column ${f.column}` : '');
  return (where ? where + ': ' : '') + f.message;
};

/* ---------------- shared raw checks (mirror of DataFiles.cs) ---------------- */

const get = (node, key) => { const f = node.fields.find(x => x.key === key); return f ? f.value : null; };
const blank = s => s == null || s.split('').every(isSpace);
const EXPECTED = { string: 'text in double quotes', bool: 'true or false (without quotes)', number: 'a number', array: 'a list in [ ]', object: 'a { } block' };
function describe(v) {
  switch (v.kind) {
    case 'string': return `the text "${v.text.length > 40 ? v.text.slice(0, 40) + '...' : v.text}"`;
    case 'number': return v.text;
    case 'bool': return v.bool ? 'true' : 'false';
    case 'null': return 'null';
    case 'array': return 'a list [ ]';
    default: return 'a { } block';
  }
}
function unknown(f, file, node, at, known) {
  for (const fld of node.fields) {
    if (known.includes(fld.key)) continue;
    let near = null;
    for (const k of known) if (asciiLower(k) === asciiLower(fld.key)) near = k;
    f.push(err(file, fld.line, `${at}: "${fld.key}" is not a field the game knows` +
      (near !== null ? ` (did you mean "${near}"? names are case-sensitive)` : ` (fields here: ${known.join(', ')})`)));
  }
}
function expect(f, file, at, key, v, kind) {
  if (v.kind === kind) return true;
  f.push(err(file, v.line, `${at}: "${key}" should be ${EXPECTED[kind]}, but it is ${describe(v)}`));
  return false;
}
function expectOptional(f, file, at, key, node, kind) { const v = get(node, key); if (v) expect(f, file, at, key, v, kind); }
const isWhole = v => v.kind === 'number' && !/[.eE]/.test(v.text);
function expectInt(f, file, at, key, node) {
  const v = get(node, key);
  if (!v) return;
  if (!isWhole(v)) { f.push(err(file, v.line, `${at}: "${key}" should be a whole number, but it is ${describe(v)}`)); return; }
  const neg = v.text.startsWith('-'), digits = v.text.replace(/^-/, '');
  const inRange = digits.length <= 10 && (neg ? -Number(digits) >= -2147483648 : Number(digits) <= 2147483647);
  if (!inRange) f.push(err(file, v.line, `${at}: "${key}" is ${v.text}, which is too ` + (neg ? 'small (the smallest allowed is -2147483648' : 'big (the largest allowed is 2147483647') + ')'));
  /* every bound must be above 0; checked on what was typed, since MathConfig keeps -1 as "not set" */
  else if (neg || Number(digits) === 0) f.push(err(file, v.line, `${at}: "${key}" should be a whole number above 0, but it is ${v.text}`));
}
const hasErrors = f => f.some(x => x.severity === 'E');
function parseOrReport(text, file, f) {
  const { root, error } = parseStrict(text);
  if (error) f.push(finding('E', file, error.line, error.column, error.message));
  return root;
}

/* ---------------- weeks.json (mirror of DataFiles.LoadWeeks, Weeks.cs, WeekRules.cs) ---------------- */

const MIN_WORDS_FOR_MODE = 4;   /* lyric.js:28, prefix.js:27 */
const isDateShape = id => typeof id === 'string' && /^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(id);
function isRealDate(id) {
  if (!isDateShape(id)) return false;
  const y = +id.slice(0, 4), m = +id.slice(5, 7), d = +id.slice(8, 10);
  if (y < 1 || m < 1 || m > 12 || d < 1) return false;
  const days = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  const leap = (y % 4 === 0 && y % 100 !== 0) || y % 400 === 0;
  return d <= (m === 2 && leap ? 29 : days[m - 1]);
}
const withSentence = wk => (wk.words || []).filter(v => v && v.x != null && v.x.includes('___'));          /* lyric.js:9 */
const withPrefix = wk => (wk.words || []).filter(v => v && v.prefix && v.prefix.p && v.prefix.root && v.prefix.means);   /* prefix.js:10 */
const weekName = (wk, i) => wk && !blank(wk.id) ? `week ${wk.id}` : `week #${i + 1}`;
const wordName = (v, j) => v && !blank(v.w) ? `word "${v.w}"` : `word #${j + 1}`;
const count = n => n === 1 ? '1 word has' : `${n} words have`;
const countBlanks = x => (x.match(/___/g) || []).length;

/* Week by week (mirror of WeeksFile.Check / CheckWeek): a week with an error is skipped and the rest still load.
   rawSkipped[i], when set, names a week the key and type checks already refused. A repeated id is checked against the
   weeks already kept, so the copy higher in the file wins, as WEEKS.find(w => w.id === id) does (show.js:13). */
export function checkWeeks(model, file = 'weeks.json', rawSkipped = null) {
  const f = [], kept = [], skipped = [];
  const weeks = model.weeks;
  if (!weeks || weeks.length === 0) { f.push(err(file, 0, 'there are no weeks in the file')); return { findings: f, kept, skipped }; }
  const keptIds = new Set();
  weeks.forEach((wk, i) => {
    if (rawSkipped && rawSkipped[i] != null) { skipped.push(rawSkipped[i]); return; }
    if (!wk) { f.push(err(file, 0, `week #${i + 1} is empty`)); skipped.push(skipName(`week #${i + 1}`, 0)); return; }
    if (checkWeek(f, file, wk, i, keptIds)) { kept.push(wk); keptIds.add(wk.id); }
    else skipped.push(skipName(weekName(wk, i), wk.line || 0));
  });
  if (kept.length === 0) f.push(err(file, 0, 'none of the weeks can be used, so the game has no words to show'));
  else if (skipped.length > 0) f.push(warn(file, 0, `the game skips ${skipped.join(', ')} until ${skipped.length === 1 ? 'it is' : 'they are'} fixed; the other weeks load`));
  return { findings: f, kept, skipped };
}
export const validateWeeksModel = (model, file = 'weeks.json') => checkWeeks(model, file).findings;

function checkWeek(f, file, wk, i, keptIds) {
  const before = errorCount(f);
  const at = weekName(wk, i), line = wk.line || 0;
  if (blank(wk.id)) f.push(err(file, line, `${at}: the id is missing; it should be the test date, like 2026-11-23`));
  else if (!isDateShape(wk.id)) f.push(err(file, line, `${at}: the id "${wk.id}" should be the test date written as YYYY-MM-DD, like 2026-11-23`));
  else if (!isRealDate(wk.id)) f.push(err(file, line, `${at}: the id "${wk.id}" is not a real date`));
  else if (keptIds.has(wk.id)) f.push(err(file, line, `${at}: a week higher in the file already uses the id ${wk.id}; each week needs its own date`));
  if (blank(wk.label)) f.push(err(file, line, `${at}: the label is missing; it is the name shown in the week menu`));
  if (!wk.words || wk.words.length === 0) { f.push(err(file, line, `${at}: there are no words in this week`)); return false; }
  const seen = new Set();
  wk.words.forEach((v, j) => {
    if (!v) { f.push(err(file, line, `${at}, word #${j + 1} is empty`)); return; }
    checkWord(f, file, `${at}, ${wordName(v, j)}`, v, seen);
  });
  const nx = withSentence(wk).length, np = withPrefix(wk).length;
  if (nx > 0 && nx < MIN_WORDS_FOR_MODE) f.push(warn(file, line, `${at}: only ${count(nx)} a sentence (x), so lyric writer is hidden that week (it needs ${MIN_WORDS_FOR_MODE})`));
  if (np > 0 && np < MIN_WORDS_FOR_MODE) f.push(warn(file, line, `${at}: only ${count(np)} a prefix, so prefix mixer is hidden that week (it needs ${MIN_WORDS_FOR_MODE})`));
  return errorCount(f) === before;
}
const skipName = (name, line) => line > 0 ? `${name} (line ${line})` : name;   /* tells two weeks with one id apart */
const errorCount = f => f.filter(x => x.severity === 'E').length;

function checkWord(f, file, at, v, seen) {
  const line = v.line || 0;
  const hasW = !blank(v.w);
  if (!hasW) f.push(err(file, line, `${at}: the word (w) is missing`));
  else {
    const bad = firstNonLetter(v.w);   /* parts.js:43 */
    if (bad >= 0) f.push(err(file, line, `${at}: the word must be lowercase letters a to z only, or a typed answer can never match it (it has ${nameChar(v.w[bad])})`));
    if (seen.has(v.w)) f.push(err(file, line, `${at}: this word is already in this week`));
    else seen.add(v.w);
  }
  if (blank(v.s)) f.push(err(file, line, `${at}: the syllables (s) are missing; write them like ex-am-ple`));
  if (blank(v.h)) f.push(err(file, line, `${at}: the clue (h) is missing`));
  if (v.x != null) {   /* content_review.mjs:53, lyric.js:9 */
    const n = countBlanks(v.x);
    if (n === 0) f.push(err(file, line, `${at}: the sentence (x) has no ___ to show where the word goes`));
    else if (n > 1) f.push(err(file, line, `${at}: the sentence (x) has ${n} blanks (___); it needs exactly one`));
  }
  if (v.prefix != null) {   /* prefix.js:10, :96, :109 */
    const p = !blank(v.prefix.p), root = !blank(v.prefix.root), means = !blank(v.prefix.means);
    if (!p) f.push(err(file, line, `${at}: the prefix is missing "p" (the prefix itself, like ir)`));
    if (!root) f.push(err(file, line, `${at}: the prefix is missing "root" (the word it is added to)`));
    if (!means) f.push(err(file, line, `${at}: the prefix is missing "means" (what the prefix adds, like not)`));
    if (p && root && hasW && v.prefix.p + v.prefix.root !== v.w)
      f.push(err(file, line, `${at}: the prefix "${v.prefix.p}" plus the root "${v.prefix.root}" makes "${v.prefix.p + v.prefix.root}", not "${v.w}"`));
  }
  if (hasW) {   /* content_review.mjs:56, 54, 55 */
    if (!blank(v.s) && v.s.replace(/-/g, '') !== v.w) f.push(warn(file, line, `${at}: the syllables "${v.s}" don't spell the word`));
    const lw = asciiLower(v.w);
    if (v.x != null && asciiLower(v.x).includes(lw)) f.push(warn(file, line, `${at}: the sentence (x) gives the word away`));
    if (!blank(v.h) && asciiLower(v.h).includes(lw)) f.push(warn(file, line, `${at}: the clue (h) gives the word away`));
    /* Unity only (QUESTIONS 11; C# SpellingHints.ShortWord = 3): once the first letter is shown, nothing is left to hide */
    if (firstNonLetter(v.w) < 0 && v.w.length <= 3)
      f.push(warn(file, line, `${at}: it is only ${v.w.length} letter${v.w.length === 1 ? '' : 's'} long, so the hints have almost nothing to hide (words of 4 or more letters work best)`));
  }
}

/* Mirror of DataFiles.LoadWeeks: refused (model null) when it can't be parsed, has no weeks list, or no week is valid;
   otherwise model.weeks holds only the usable weeks and skipped names the others. */
export function loadWeeks(text, file = 'weeks.json') {
  const f = [];
  const refused = () => ({ model: null, skipped: [], findings: f });
  const root = parseOrReport(text, file, f);
  if (!root) return refused();
  if (root.kind !== 'object') { f.push(err(file, root.line, 'the file should be a { } block holding "weeks": [ ... ]')); return refused(); }
  unknown(f, file, root, 'the top level', ['weeks']);
  const weeks = get(root, 'weeks');
  if (!weeks) { f.push(err(file, root.line, 'the top level: "weeks" is missing')); return refused(); }
  if (!expect(f, file, 'the top level', 'weeks', weeks, 'array')) return refused();
  const rawSkipped = weeks.items.map((wk, i) => {
    const before = errorCount(f);
    const at = rawWeek(f, file, wk, i);
    return errorCount(f) > before ? skipName(at, wk.line) : null;
  });
  const text_ = (node, key) => { const v = get(node, key); return v ? v.text : null; };
  const model = {
    weeks: weeks.items.map((wk, i) => {
      if (rawSkipped[i] != null) return null;
      const words = get(wk, 'words');
      return {
        id: text_(wk, 'id'), label: text_(wk, 'label'), line: wk.line,
        words: words ? words.items.map(v => {
          const p = get(v, 'prefix');
          return { w: text_(v, 'w'), s: text_(v, 's'), h: text_(v, 'h'), x: text_(v, 'x'), line: v.line,
            prefix: p ? { p: text_(p, 'p'), root: text_(p, 'root'), means: text_(p, 'means') } : null };
        }) : null
      };
    })
  };
  const r = checkWeeks(model, file, rawSkipped);
  f.push(...r.findings);
  if (r.kept.length === 0) return refused();
  return { model: { weeks: r.kept }, skipped: r.skipped, findings: f };
}

function rawWeek(f, file, wk, i) {
  let at = `week #${i + 1}`;
  if (wk.kind !== 'object') { f.push(err(file, wk.line, `${at} should be a { } block, but it is ${describe(wk)}`)); return at; }
  const id = get(wk, 'id');
  if (id && id.kind === 'string' && !blank(id.text)) at = `week ${id.text}`;
  unknown(f, file, wk, at, ['id', 'label', 'words']);
  expectOptional(f, file, at, 'id', wk, 'string');
  expectOptional(f, file, at, 'label', wk, 'string');
  const words = get(wk, 'words');
  if (!words || !expect(f, file, at, 'words', words, 'array')) return at;
  words.items.forEach((v, j) => {
    let wat = `${at}, word #${j + 1}`;
    if (v.kind !== 'object') { f.push(err(file, v.line, `${wat} should be a { } block, but it is ${describe(v)}`)); return; }
    const w = get(v, 'w');
    if (w && w.kind === 'string' && !blank(w.text)) wat = `${at}, word "${w.text}"`;
    unknown(f, file, v, wat, ['w', 's', 'h', 'x', 'prefix']);
    for (const k of ['w', 's', 'h', 'x']) expectOptional(f, file, wat, k, v, 'string');
    const p = get(v, 'prefix');
    if (p && expect(f, file, wat, 'prefix', p, 'object')) {
      const pat = `${wat}, prefix`;
      unknown(f, file, p, pat, ['p', 'root', 'means']);
      for (const k of ['p', 'root', 'means']) expectOptional(f, file, pat, k, p, 'string');
    }
  });
  return at;
}

/* ---------------- math_config.json (mirror of DataFiles.LoadMathConfig and MathConfig.Validate) ---------------- */

const SKILLS = ['dec', 'md', 'frac', 'xy', 'vol', 'ops', 'flu', 'fm'];
const LEVEL_KEYS = ['MAX_LEVEL', 'START_LEVEL'];   /* Unity only, phase 7.1: see MATH_CONFIG.md */
const MAX_AFTER = 20, LOWEST_LEVEL = 0, HIGHEST_LEVEL = 3;   /* MathConfig.MaxAfter, LowestLevel, HighestLevel */
const BOUND_KEYS = ['moneyCents', 'maxDenominator', 'coordMax', 'maxVolume', 'storyDollars'];   /* storyDollars: Unity only, QUESTIONS 9.4 */

/* a level: a whole number from 0 to 3 (0 is the warm-up); mirror of DataFiles.ExpectLevel */
function expectLevel(f, file, at, key, node) {
  const v = get(node, key);
  if (!v) return;
  if (!isWhole(v)) { f.push(err(file, v.line, `${at}: "${key}" should be a whole number, but it is ${describe(v)}`)); return; }
  const neg = v.text.startsWith('-'), digits = v.text.replace(/^-/, '').replace(/^0+/, '');
  const bad = digits.length > 1 || (digits.length === 1 && (neg || Number(digits) > HIGHEST_LEVEL));
  if (bad) f.push(err(file, v.line, `${at}: "${key}" should be a whole number from ${LOWEST_LEVEL} to ${HIGHEST_LEVEL}, but it is ${v.text}`));
}

export function loadMathConfig(text, file = 'math_config.json') {
  const f = [];
  const root = parseOrReport(text, file, f);
  if (root) {
    if (root.kind !== 'object') f.push(err(file, root.line, 'the file should be a { } block holding "ENABLED_SKILLS" and "BOUNDS"'));
    else {
      unknown(f, file, root, 'the top level', ['ENABLED_SKILLS', 'BOUNDS', 'LEVEL_UP_AFTER', 'LEVEL_DOWN_AFTER', 'LEVELS', 'fmDivision']);
      const sk = get(root, 'ENABLED_SKILLS');
      if (sk && expect(f, file, 'the top level', 'ENABLED_SKILLS', sk, 'object')) {
        unknown(f, file, sk, 'ENABLED_SKILLS', SKILLS);
        for (const k of SKILLS) expectOptional(f, file, 'ENABLED_SKILLS', k, sk, 'bool');
      }
      const b = get(root, 'BOUNDS');
      if (b && expect(f, file, 'the top level', 'BOUNDS', b, 'object')) {
        unknown(f, file, b, 'BOUNDS', BOUND_KEYS);
        for (const k of BOUND_KEYS) expectInt(f, file, 'BOUNDS', k, b);
      }
      for (const k of ['LEVEL_UP_AFTER', 'LEVEL_DOWN_AFTER']) expectInt(f, file, 'the top level', k, root);
      expectOptional(f, file, 'the top level', 'fmDivision', root, 'bool');
      const lv = get(root, 'LEVELS');
      if (lv && expect(f, file, 'the top level', 'LEVELS', lv, 'object')) {
        unknown(f, file, lv, 'LEVELS', SKILLS);
        for (const sk of SKILLS) {
          const one = get(lv, sk);
          if (!one || !expect(f, file, 'LEVELS', sk, one, 'object')) continue;
          unknown(f, file, one, `LEVELS.${sk}`, LEVEL_KEYS);
          for (const k of LEVEL_KEYS) expectLevel(f, file, `LEVELS.${sk}`, k, one);
        }
      }
      if (!hasErrors(f)) {
        /* MathConfig.Validate(): a missing bound stays -1 and is reported; others must be above 0 */
        for (const k of BOUND_KEYS) {
          const v = b && get(b, k) ? Number(get(b, k).text) : -1;
          if (v === -1) f.push(err(file, 0, `BOUNDS.${k} is missing.`));
          else if (v <= 0) f.push(err(file, 0, `BOUNDS.${k} must be a whole number above 0 (it is ${v}).`));
        }
        for (const k of ['LEVEL_UP_AFTER', 'LEVEL_DOWN_AFTER']) {
          const v = get(root, k) ? Number(get(root, k).text) : null;
          if (v !== null && v > MAX_AFTER) f.push(err(file, 0, `${k} must be ${MAX_AFTER} or less (it is ${v}).`));
        }
        /* a start above the cap is not wrong enough to refuse the file: the game starts at the cap */
        for (const sk of SKILLS) {
          const one = lv && get(lv, sk);
          const num = (key, dflt) => one && get(one, key) ? Number(get(one, key).text) : dflt;
          const max = num('MAX_LEVEL', HIGHEST_LEVEL), start = num('START_LEVEL', LOWEST_LEVEL);
          if (start > max) f.push(warn(file, 0, `LEVELS.${sk}: START_LEVEL (${start}) is above MAX_LEVEL (${max}), so the game starts it at ${max}`));
        }
      }
    }
  }
  return { usedDefaults: hasErrors(f), findings: f };
}

/* ---------------- command line ---------------- */

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, '..', '..');

function samples(dir) {
  const out = {};
  for (const name of readdirSync(dir).filter(n => n.endsWith('.json') && n !== 'expected_findings.json').sort()) {
    const text = readFileSync(join(dir, name), 'utf8');
    const r = name.startsWith('weeks') ? loadWeeks(text) : name.startsWith('config') ? loadMathConfig(text) : null;
    if (!r) throw new Error(`sample ${name}: name must start with "weeks" or "config"`);
    out[name] = r.findings.map(parityKey);
  }
  return JSON.stringify({ note: 'written by Tools/validate_data.mjs --parity; the C# loader must give the same findings', samples: out }, null, 1)
    .replace(/[^\x00-\x7f]/g, c => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')) + '\n';
}

function main(argv) {
  const pi = argv.findIndex(a => a === '--parity' || a === '--parity-check');
  if (pi >= 0) {
    const dir = resolve(argv[pi + 1] || join(here, '..', 'Assets', 'Tests', 'EditMode', 'DataSamples'));
    const file = join(dir, 'expected_findings.json'), json = samples(dir);
    if (argv[pi] === '--parity') { writeFileSync(file, json); console.log(`wrote ${file}`); return 0; }
    const same = existsSync(file) && readFileSync(file, 'utf8') === json;
    console.log(same ? 'parity file is up to date' : `parity file is out of date: run --parity ${dir}`);
    return same ? 0 : 1;
  }
  const pick = (arg, name) => {
    if (arg) return resolve(arg);
    const deployed = join(repo, 'tour', 'StreamingAssets', name);
    return existsSync(deployed) ? deployed : join(repo, 'world_tour_unity', 'Assets', 'StreamingAssets', name);
  };
  /* a path named weeks.json or math_config.json is checked as that file wherever it is given; any other name goes by position */
  let weeksArg, configArg;
  argv.filter(a => !a.startsWith('--')).forEach((a, i) => {
    const base = a.split(/[\\/]/).pop();
    if (base === 'math_config.json') configArg = a;
    else if (base === 'weeks.json') weeksArg = a;
    else if (i === 0) weeksArg = a;
    else configArg = a;
  });
  const weeksPath = pick(weeksArg, 'weeks.json'), configPath = pick(configArg, 'math_config.json');
  const findings = [], notes = [];
  for (const [name, path, load] of [['weeks.json', weeksPath, loadWeeks], ['math_config.json', configPath, loadMathConfig]]) {
    console.log(`checking ${path}`);
    if (!existsSync(path)) { findings.push(err(name, 0, `there is no file at ${path}`)); continue; }
    const r = load(readFileSync(path, 'utf8'), name);
    findings.push(...r.findings);
    if (name === 'weeks.json' && r.model === null) notes.push('until weeks.json is fixed, the game shows these problems instead of the words.');
    else if (name !== 'weeks.json' && hasErrors(r.findings)) notes.push('until math_config.json is fixed, the game uses its built-in math settings.');
  }
  const errors = findings.filter(x => x.severity === 'E'), warnings = findings.filter(x => x.severity === 'W');
  if (!findings.length) { console.log('ok'); return 0; }
  let n = 0;
  if (errors.length) {
    console.log(`\n${errors.length} problem${errors.length > 1 ? 's' : ''} to fix:`);
    for (const x of errors) console.log(`${++n}. ${show(x)}`);
    for (const note of notes) console.log(note);
  }
  if (warnings.length) {
    console.log(`\n${warnings.length} thing${warnings.length > 1 ? 's' : ''} to look at (the game still loads):`);
    for (const x of warnings) console.log(`${++n}. ${show(x)}`);
  }
  if (!errors.length) console.log('\nok to load');
  return errors.length ? 1 : 0;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = main(process.argv.slice(2));
