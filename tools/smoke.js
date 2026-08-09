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
  // sanity: terrain is finite everywhere near the track
  for (let i = 0; i < dbg.trackPts.length; i += 7) {
    const p = dbg.trackPts[i];
    const h = dbg.terrainH(p.x, p.z);
    if (!isFinite(h)) throw new Error(courseId + ': non-finite terrain at ' + i);
  }
  // drive the sim: start, hold gas, update 20 simulated seconds
  dbg.startGame();
  dbg.keys.KeyW = true;
  for (let i = 0; i < 20 * 60; i++) dbg.update(1 / 60);
  const prof = dbg.trackProfile(dbg.S.pos.x, dbg.S.pos.z);
  return {
    id: courseId, trackLen: Math.round(dbg.trackLen),
    laps: dbg.RACE_LAPS, features: dbg.features.length,
    riderMoved: dbg.S.pos.length() > 1, onCourse: !!prof,
    clock: dbg.clock.toFixed(1),
  };
}

for (const id of ['flats', 'rimrock', 'mesa', 'noon', 'not-a-course']) {
  const r = runCourse(id);
  console.log(JSON.stringify(r));
}
console.log('smoke OK');
process.exit(0);   // the game's audio watchdog setInterval would keep node alive

