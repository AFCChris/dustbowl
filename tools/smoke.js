/* Headless smoke test: bake every National course and run the sim for a few
   simulated seconds. No browser — DOM and WebGLRenderer are stubbed, THREE is
   the real vendored build. Run: node tools/smoke.js */
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const ROOT = path.join(__dirname, '..');
const threeSrc = fs.readFileSync(path.join(ROOT, 'vendor', 'three.min.js'), 'utf8');
const gameSrc = fs.readFileSync(path.join(ROOT, 'src', 'game.js'), 'utf8');

/* A 2D-context stand-in where every property is callable, every call returns
   the same stand-in (so gradient objects etc. keep working), and property
   writes are accepted. */
const anyCtx = new Proxy(function () {}, {
  get: (t, k) => (k === Symbol.toPrimitive ? () => 0 : anyCtx),
  set: () => true,
  apply: () => anyCtx,
});

function makeEl() {
  const el = {
    style: {},
    classList: { add() {}, remove() {}, toggle() {}, contains: () => true },
    textContent: '', innerHTML: '', content: '',
    addEventListener() {}, setPointerCapture() {},
    appendChild() {}, setAttribute() {},
    getContext: () => anyCtx,
  };
  return el;
}

function runCourse(courseId) {
  const els = new Map();
  const sandbox = {
    console, performance: { now: () => Date.now() },
    setTimeout, setInterval, clearTimeout, clearInterval,
    localStorage: {
      _s: { 'dustbowl.course': JSON.stringify(courseId) },
      getItem(k) { return this._s[k] || null; },
      setItem(k, v) { this._s[k] = v; },
    },
    matchMedia: () => ({ matches: false }),
    screen: { width: 1920, height: 1080 },
    devicePixelRatio: 1, innerWidth: 1280, innerHeight: 720,
    addEventListener() {}, requestAnimationFrame() {},
    Math, Date, JSON, Object, Array, Float32Array, Int32Array, Uint8Array, Uint16Array, Uint32Array,
  };
  sandbox.window = sandbox;
  sandbox.self = sandbox;
  sandbox.document = {
    querySelector(s) { if (!els.has(s)) els.set(s, makeEl()); return els.get(s); },
    querySelectorAll: () => [],
    createElement: () => makeEl(),
    createElementNS: () => makeEl(),
    head: { appendChild() {} },
    body: { classList: { add() {}, remove() {}, toggle() {}, contains: () => true } },
    addEventListener() {},
  };
  const ctx = vm.createContext(sandbox);
  vm.runInContext(threeSrc, ctx);
  vm.runInContext(`
    THREE.WebGLRenderer = function () {
      return { setPixelRatio(){}, setSize(){}, render(){},
        shadowMap: {}, domElement: {} };
    };
  `, ctx);
  vm.runInContext(gameSrc, ctx);

  const dbg = sandbox.window.__dbg;
  if (!dbg) throw new Error(courseId + ': __dbg missing');
  if (!(dbg.trackLen > 500)) throw new Error(courseId + ': bad trackLen ' + dbg.trackLen);
  if (!dbg.features.length) throw new Error(courseId + ': no features');
  if (dbg.checkpoints.length < 8) throw new Error(courseId + ': too few authored points');
  if (dbg.checkpoints.length !== dbg.COURSE.sections.length) {
    throw new Error(courseId + ': layout/section count mismatch');
  }
  for (const p of dbg.trackPts) {
    const radius = Math.hypot(p.x, p.z);
    if (radius > 340) {
      throw new Error(courseId + ': centreline leaves play bowl at radius ' + radius.toFixed(1));
    }
  }
  for (const sec of dbg.COURSE.sections) {
    if (!(sec.cornerRadius > 0 && sec.straightLength > 0 && isFinite(sec.elevation) &&
      sec.braking >= 0 && sec.braking <= 1 && sec.aiSpeed > 0 && Array.isArray(sec.jumps))) {
      throw new Error(courseId + ': invalid authored section metadata');
    }
  }
  // The authored centreline must remain a simple loop. An infield may fold
  // close beside itself, but road geometry may never actually intersect.
  const pts = dbg.trackPts;
  // Lap zero is also the visible grid/gantry location. It must sit on a calm
  // straight and clear of jump geometry, rather than inheriting layout point 0.
  const sb = pts[pts.length - 6], ss = pts[0], sa = pts[6];
  const inX = ss.x - sb.x, inZ = ss.z - sb.z;
  const outX = sa.x - ss.x, outZ = sa.z - ss.z;
  const inL = Math.hypot(inX, inZ) || 1, outL = Math.hypot(outX, outZ) || 1;
  const startBend = Math.acos(Math.max(-1, Math.min(1, (inX * outX + inZ * outZ) / (inL * outL))));
  const nearestFeature = Math.min(...dbg.features.map((f) => {
    const d = Math.abs(f.at);
    return Math.min(d, dbg.trackLen - d);
  }));
  if (startBend > 0.14 || Math.abs(ss.bank) > 0.1 || nearestFeature < 18) {
    throw new Error(courseId + ': unsafe start straight (bend=' + startBend.toFixed(3) +
      ', bank=' + ss.bank.toFixed(3) + ', feature=' + nearestFeature.toFixed(1) + ')');
  }
  function crosses(a, b, c, d) {
    const orient = (p, q, r) => (q.x - p.x) * (r.z - p.z) - (q.z - p.z) * (r.x - p.x);
    const abC = orient(a, b, c), abD = orient(a, b, d);
    const cdA = orient(c, d, a), cdB = orient(c, d, b);
    return abC * abD < -1e-7 && cdA * cdB < -1e-7;
  }
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i], b = pts[(i + 1) % pts.length];
    for (let j = i + 2; j < pts.length; j++) {
      if (i === 0 && j === pts.length - 1) continue;
      const c = pts[j], d = pts[(j + 1) % pts.length];
      if (crosses(a, b, c, d)) {
        throw new Error(courseId + ': self-intersection at segments ' + i + '/' + j);
      }
    }
  }
  // sanity: terrain is finite everywhere near the track
  for (let i = 0; i < dbg.trackPts.length; i += 7) {
    const p = dbg.trackPts[i];
    const h = dbg.terrainH(p.x, p.z);
    if (!isFinite(h)) throw new Error(courseId + ': non-finite terrain at ' + i);
  }
  // Validate a complete synthetic lap through the baked profile. Sampling just
  // off-centre catches field holes while the sector/wrap assertions exercise
  // the same progress contract used by player and AI lap gating.
  let sectorSeen = false, completedLaps = 0, prevAlong = 0;
  for (let lap = 0; lap < dbg.RACE_LAPS; lap++) {
    for (let i = 1; i <= dbg.trackPts.length; i++) {
      const p = dbg.trackPts[i % dbg.trackPts.length];
      const prof = dbg.trackProfile(p.x, p.z);
      if (!prof || prof.off > 3) {
        throw new Error(courseId + ': profile gap at ' + i + ' (' +
          p.x.toFixed(1) + ',' + p.z.toFixed(1) + '; off=' + (prof ? prof.off.toFixed(1) : 'null') + ')');
      }
      // The exact start point is shared by the opening and closing segments;
      // trackProfile may correctly describe it as trackLen. For the synthetic
      // forward crossing, normalize that final sample to the new lap's zero.
      const along = i === dbg.trackPts.length ? 0 : prof.along;
      if (along > dbg.trackLen * 0.45 && along < dbg.trackLen * 0.75) sectorSeen = true;
      if (prevAlong > dbg.trackLen * 0.8 && along < dbg.trackLen * 0.2 && sectorSeen) {
        completedLaps++;
        sectorSeen = false;
      }
      prevAlong = along;
    }
  }
  if (completedLaps !== dbg.RACE_LAPS) {
    throw new Error(courseId + ': synthetic lap count ' + completedLaps);
  }

  // drive the live sim: start, hold gas, update 20 simulated seconds
  dbg.startGame();
  dbg.keys.KeyW = true;
  for (let i = 0; i < 20 * 60; i++) dbg.update(1 / 60);
  const prof = dbg.trackProfile(dbg.S.pos.x, dbg.S.pos.z);
  return {
    id: courseId, trackLen: Math.round(dbg.trackLen),
    laps: dbg.RACE_LAPS, features: dbg.features.length,
    riderMoved: dbg.S.pos.length() > 1, onCourse: !!prof,
    clock: dbg.clock.toFixed(1), completedLaps,
    startBend: +startBend.toFixed(3), startBank: +ss.bank.toFixed(3),
  };
}

const silhouettes = new Set();
for (const id of ['flats', 'rimrock', 'mesa', 'noon', 'not-a-course']) {
  const r = runCourse(id);
  if (id !== 'not-a-course') {
    const dbgShape = r.trackLen + ':' + r.features;
    silhouettes.add(dbgShape);
  }
  console.log(JSON.stringify(r));
}
if (silhouettes.size !== 4) throw new Error('course signatures are not distinct');
console.log('smoke OK');
process.exit(0);   // the game's audio watchdog setInterval would keep node alive

