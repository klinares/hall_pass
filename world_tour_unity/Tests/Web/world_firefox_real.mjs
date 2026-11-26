// Drives the INSTALLED Firefox (not Playwright's) through selenium-webdriver + geckodriver, headed, against a world/ URL.
// Records every console message and JS error over WebDriver BiDi, every network response/failure, a WebGL2 probe on a test canvas
// (renderer, failIfMajorPerformanceCaveat, a clear+readPixels draw check), page state, and screenshots at 30 s and 90 s.
// Dev-only; never loaded by the game.
//
//   node world_firefox_real.mjs [--url https://klinares.github.io/hall_pass/world/] [--profile <dir>] [--binary <firefox.exe>] [--out <dir>] [--label fresh]
//
// With no --profile, geckodriver makes a fresh temporary profile. With --profile, that directory is used in place (pass a COPY).
import { Builder } from "selenium-webdriver";
import firefox from "selenium-webdriver/firefox.js";
import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const args = process.argv.slice(2);
const arg = (name, def) => { const i = args.indexOf("--" + name); return i >= 0 ? args[i + 1] : def; };
const url = arg("url", "https://klinares.github.io/hall_pass/world/");
const profile = arg("profile", null);
const label = arg("label", profile ? "profile" : "fresh");
const out = arg("out", path.join(process.cwd(), "firefox_real_out", label));
const waitA = +arg("shot1", 30), waitB = +arg("shot2", 90);
fs.mkdirSync(out, { recursive: true });

function findFirefox() {
  const given = arg("binary", null);
  if (given) return given;
  try {
    // Microsoft Store Firefox lives under WindowsApps; ask for the installed package location.
    const loc = execSync('powershell -NoProfile -Command "(Get-AppxPackage Mozilla.Firefox).InstallLocation"', { encoding: "utf8" }).trim();
    const p = path.join(loc, "VFS", "ProgramFiles", "Firefox Package Root", "firefox.exe");
    if (loc && fs.existsSync(p)) return p;
  } catch {}
  for (const p of ["C:/Program Files/Mozilla Firefox/firefox.exe", "C:/Program Files (x86)/Mozilla Firefox/firefox.exe"]) if (fs.existsSync(p)) return p;
  throw new Error("installed Firefox not found; pass --binary");
}

const PROBE = function () {
  function one(kind, attrs) {
    const c = document.createElement("canvas"); c.width = 64; c.height = 64;
    let creationError = null;
    c.addEventListener("webglcontextcreationerror", (e) => { creationError = e.statusMessage || "(no message)"; }, false);
    let gl = null, threw = null;
    try { gl = c.getContext(kind, attrs); } catch (e) { threw = String(e); }
    const r = { kind, attrs, ok: !!gl, creationError, threw };
    if (!gl) return r;
    const dbg = gl.getExtension("WEBGL_debug_renderer_info");
    r.vendor = gl.getParameter(gl.VENDOR);
    r.renderer = gl.getParameter(gl.RENDERER);
    r.unmaskedVendor = dbg ? gl.getParameter(dbg.UNMASKED_VENDOR_WEBGL) : "(no WEBGL_debug_renderer_info)";
    r.unmaskedRenderer = dbg ? gl.getParameter(dbg.UNMASKED_RENDERER_WEBGL) : "(no WEBGL_debug_renderer_info)";
    r.version = gl.getParameter(gl.VERSION);
    r.glsl = gl.getParameter(gl.SHADING_LANGUAGE_VERSION);
    r.maxTexture = gl.getParameter(gl.MAX_TEXTURE_SIZE);
    r.contextAttributes = gl.getContextAttributes();
    r.extensions = (gl.getSupportedExtensions() || []).length;
    gl.clearColor(1, 0, 0, 1); gl.clear(gl.COLOR_BUFFER_BIT);
    const px = new Uint8Array(4); gl.readPixels(1, 1, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
    r.clearReadback = Array.from(px);
    r.error = gl.getError();
    const lose = gl.getExtension("WEBGL_lose_context"); if (lose) lose.loseContext();
    return r;
  }
  return {
    userAgent: navigator.userAgent,
    devicePixelRatio: window.devicePixelRatio,
    results: [
      one("webgl2", {}),
      one("webgl2", { failIfMajorPerformanceCaveat: true }),
      one("webgl2", { powerPreference: "high-performance" }),
      one("webgl2", { powerPreference: "high-performance", failIfMajorPerformanceCaveat: true }),
      one("webgl2", { preserveDrawingBuffer: false, antialias: true, alpha: true, depth: true, stencil: true }),
      one("webgl", {}),
    ],
  };
};

const PAGE_STATE = function () {
  const q = (s) => document.querySelector(s);
  const canvas = q("#unity-canvas") || q("canvas");
  const vis = (el) => el ? getComputedStyle(el).display + "/" + getComputedStyle(el).visibility : null;
  const ids = Array.from(document.querySelectorAll("[id]")).map((e) => ({ id: e.id, display: getComputedStyle(e).display, text: (e.innerText || "").slice(0, 160) }));
  return {
    t: performance.now(),
    readyState: document.readyState,
    hasInstance: !!window.__wtInstance,
    canvas: canvas ? { w: canvas.width, h: canvas.height, cssW: canvas.clientWidth, cssH: canvas.clientHeight, display: vis(canvas) } : null,
    elements: ids,
    resources: performance.getEntriesByType("resource").map((e) => ({ name: e.name.replace(location.origin, ""), status: e.responseStatus, size: e.transferSize, decoded: e.decodedBodySize, ms: Math.round(e.duration) })),
    memory: performance.memory ? performance.memory.usedJSHeapSize : null,
  };
};

// In-page analysis of a screenshot: share of near-black pixels and the mean colour, over the whole viewport.
const SHOT_STATS = function (b64, done) {
  const img = new Image();
  img.onload = () => {
    const c = document.createElement("canvas"); c.width = img.width; c.height = img.height;
    const x = c.getContext("2d"); x.drawImage(img, 0, 0);
    const d = x.getImageData(0, 0, c.width, c.height).data;
    let black = 0, n = 0, r = 0, g = 0, b = 0;
    for (let i = 0; i < d.length; i += 16) { n++; r += d[i]; g += d[i + 1]; b += d[i + 2]; if (d[i] < 12 && d[i + 1] < 12 && d[i + 2] < 12) black++; }
    done({ w: c.width, h: c.height, blackShare: +(black / n).toFixed(3), mean: [r / n, g / n, b / n].map(Math.round) });
  };
  img.onerror = () => done({ error: "decode failed" });
  img.src = "data:image/png;base64," + b64;
};

const binary = findFirefox();
const opts = new firefox.Options().setBinary(binary).enableBidi().windowSize({ width: 1280, height: 860 });
if (profile) opts.addArguments("-profile", path.resolve(profile));
const t0 = Date.now();
const rel = () => +((Date.now() - t0) / 1000).toFixed(2);
const report = { label, url, binary, profile, startedAt: new Date().toISOString(), events: [], network: [], probeBlank: null, probeOnPage: null, states: {}, shots: {} };

let driver;
try {
  driver = await new Builder().forBrowser("firefox").setFirefoxOptions(opts).build();
  const caps = await driver.getCapabilities();
  report.browserVersion = caps.get("browserVersion");
  report.platform = caps.get("platformName");

  const bidi = await driver.getBidi();
  await bidi.subscribe(["log.entryAdded", "network.responseCompleted", "network.fetchError", "browsingContext.navigationFailed"]);
  bidi.socket.on("message", (raw) => {
    let m; try { m = JSON.parse(raw.toString()); } catch { return; }
    if (!m.method) return;
    const p = m.params || {};
    if (m.method === "log.entryAdded") {
      report.events.push({ t: rel(), type: p.type, level: p.level, method: p.method, text: p.text != null ? p.text : (p.args || []).map((a) => a.value).join(" "), source: p.source?.realm ? undefined : p.source, stack: p.stackTrace?.callFrames?.slice(0, 4) });
    } else if (m.method === "network.responseCompleted") {
      const r = p.response || {};
      const hdr = (n) => (r.headers || []).find((h) => h.name.toLowerCase() === n)?.value?.value;
      report.network.push({ t: rel(), url: p.request?.url, status: r.status, mime: r.mimeType, encoding: hdr("content-encoding"), type: hdr("content-type"), bytes: r.bytesReceived, fromCache: r.fromCache });
    } else {
      report.network.push({ t: rel(), event: m.method, url: p.request?.url || p.url, error: p.errorText });
    }
  });

  await driver.get("about:blank");
  report.probeBlank = await driver.executeScript(PROBE);
  console.log("probe (about:blank):");
  for (const r of report.probeBlank.results) console.log("  ", r.kind, JSON.stringify(r.attrs), r.ok ? `ok renderer=${r.unmaskedRenderer} readback=${r.clearReadback} pp=${r.contextAttributes?.powerPreference}` : `FAILED ${r.creationError || r.threw || ""}`);

  const navStart = Date.now();
  await driver.get(url);
  report.navigatedAt = rel();
  for (const [name, secs] of [["s" + waitA, waitA], ["s" + waitB, waitB]]) {
    const left = secs * 1000 - (Date.now() - navStart);
    if (left > 0) await new Promise((r) => setTimeout(r, left));
    const png = await driver.takeScreenshot();
    const file = path.join(out, `shot_${name}.png`);
    fs.writeFileSync(file, Buffer.from(png, "base64"));
    report.states[name] = await driver.executeScript(PAGE_STATE);
    report.shots[name] = { file, stats: await driver.executeAsyncScript(SHOT_STATS, png) };
    console.log(`${name}: instance=${report.states[name].hasInstance} canvas=${JSON.stringify(report.states[name].canvas)} shot=${JSON.stringify(report.shots[name].stats)}`);
  }
  report.probeOnPage = await driver.executeScript(PROBE);
} catch (e) {
  report.fatal = String(e && e.stack || e);
  console.error("FATAL", report.fatal);
} finally {
  report.endedAt = new Date().toISOString();
  fs.writeFileSync(path.join(out, "report.json"), JSON.stringify(report, null, 2));
  const errs = report.events.filter((e) => e.level === "error" || e.type === "javascript");
  console.log(`console entries: ${report.events.length}, errors: ${errs.length}, network: ${report.network.length}, ready line: ${report.events.some((e) => String(e.text).startsWith("WT_WORLD_GAME_READY"))}`);
  for (const e of report.events) console.log(`  [${e.t}] ${e.level} ${String(e.text).slice(0, 220)}`);
  console.log("report:", path.join(out, "report.json"));
  if (driver) await driver.quit().catch(() => {});
}
