/* =======================================================================
   DUSTBOWL '98  —  arcade dirt-bike riding over procedural desert
   ======================================================================= */
(function () {
'use strict';

const $ = (s) => document.querySelector(s);

/* The artifact wrapper owns <head>, so claim the viewport from here. Without
   viewport-fit=cover, env(safe-area-inset-*) reports zero and the HUD ends up
   under the Dynamic Island. */
{
  let vp = document.querySelector('meta[name="viewport"]');
  if (!vp) {
    vp = document.createElement('meta');
    vp.name = 'viewport';
    document.head.appendChild(vp);
  }
  vp.content = 'width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover';
}
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
const lerp = (a, b, t) => a + (b - a) * t;
function smoothstep(e0, e1, x) {
  const t = clamp((x - e0) / (e1 - e0), 0, 1);
  return t * t * (3 - 2 * t);
}

/* ---------------------------------------------------------------- noise */
function hash2(i, j) {
  let n = (i * 374761393 + j * 668265263) | 0;
  n = (n ^ (n >> 13)) | 0;
  n = Math.imul(n, 1274126177) | 0;
  return ((n ^ (n >> 16)) >>> 0) / 4294967295;
}
function vnoise(x, y) {
  const i = Math.floor(x), j = Math.floor(y);
  const fx = x - i, fy = y - j;
  const u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  const a = hash2(i, j), b = hash2(i + 1, j), c = hash2(i, j + 1), d = hash2(i + 1, j + 1);
  return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
}
function fbm(x, y, oct) {
  let sum = 0, amp = 0.5, norm = 0, fx = x, fy = y;
  for (let o = 0; o < oct; o++) {
    sum += vnoise(fx, fy) * amp;
    norm += amp;
    amp *= 0.5;
    fx *= 2.03; fy *= 1.97;
  }
  return sum / norm;
}

/* ------------------------------------------------------------ constants */
const BUILD = 'v0.10 · 3 Aug';  // shown on the title screen, so you can tell
                                 // at a glance whether a deploy actually landed
/* Phones get a lighter build of the world. Decided once, up front, because the
   terrain mesh is baked at load. */
const MOBILE = matchMedia('(pointer: coarse)').matches || Math.min(screen.width, screen.height) < 820;

/* A modern phone held a locked 60fps at the cautious settings, so this dials
   the detail back up. The FPS readout is the test: if it still never leaves 60,
   there was headroom going spare; if it dips, these come back down. */
const WORLD = 1100;          // terrain extent (metres, centred on origin)
const SEG = MOBILE ? 240 : 320;   // terrain grid resolution
const PLAY_R = 340;          // rideable radius before the rim wall
const GRAV = 22;             // arcade gravity
const MAX_SPEED = 42;        // m/s — the engine's ceiling, not the achieved top speed
const RIDE_H = 0.42;         // wheel contact offset

/* ---------------------------------------------------------------- ramps */
const ramps = [];
function rampHeight(x, z) {
  let add = 0;
  for (let i = 0; i < ramps.length; i++) {
    const r = ramps[i];
    const dx = x - r.x, dz = z - r.z;
    if (dx * dx + dz * dz > r.r2) continue;
    const f = dx * r.cos + dz * r.sin;
    if (f < 0 || f > r.len) continue;
    const s = -dx * r.sin + dz * r.cos;
    const as = Math.abs(s);
    if (as > r.wid) continue;
    const t = f / r.len;
    const lat = 1 - smoothstep(r.wid * 0.5, r.wid, as);
    const h = r.h * Math.pow(t, 1.65) * lat;
    if (h > add) add = h;
  }
  return add;
}

function baseHeight(x, z) {
  let h = (fbm(x * 0.0016, z * 0.0016, 5) - 0.5) * 88;
  h += (fbm(x * 0.0062 + 91, z * 0.0062 - 37, 3) - 0.5) * 26;
  h += (fbm(x * 0.017 + 43, z * 0.017 - 12, 2) - 0.5) * 7;
  h += (fbm(x * 0.028 + 11, z * 0.028 + 5, 2) - 0.5) * 1.4;
  const r = Math.hypot(x, z);
  // low canyon rim keeps the rider in the bowl without walling off the sky
  const rim = smoothstep(PLAY_R + 10, PLAY_R + 190, r);
  h += rim * rim * 78;
  return h;
}

/* ------------------------------------------------------------ the track */
/* Not a road painted on the desert — a channel cut into it. The racing surface
   sits below the surrounding land with raised sides, corners are banked, and
   there are whoops and table-tops on the racing line. The landform is what
   keeps you on the track; there's no invisible penalty doing that job. */
const TRACK_HALF = 8.5;      // racing surface, centre to edge
const BERM_W = 7;            // raised side wall
const BERM_H = 4.2;          // how high the sides stand above the surface
const TRACK_CUT = 1.6;       // how far the surface sits below the natural land
const BLEND_W = 12;          // sides fall back into natural terrain over this
const MAX_BANK = 0.42;       // rise per metre across a banked corner

const FIELD_CELL = 4;
const FIELD_MIN = -520, FIELD_N = Math.ceil(1040 / FIELD_CELL) + 1;
const fieldDist = new Float32Array(FIELD_N * FIELD_N).fill(9999);
const fieldIdx = new Int32Array(FIELD_N * FIELD_N).fill(-1);
let trackPts = [];
let trackLen = 0;

/* Walking the polyline per query is far too slow — the terrain build alone asks
   a few hundred thousand times. Bake the nearest-point index into a grid once,
   then refine against that point's own segments, which stays exact and smooth. */
function bakeTrackField() {
  const REACH = TRACK_HALF + BERM_W + BLEND_W + 6;
  const cells = Math.ceil(REACH / FIELD_CELL);
  for (let n = 0; n < trackPts.length; n++) {
    const p = trackPts[n];
    const ci = Math.round((p.x - FIELD_MIN) / FIELD_CELL);
    const cj = Math.round((p.z - FIELD_MIN) / FIELD_CELL);
    for (let i = ci - cells; i <= ci + cells; i++) {
      if (i < 0 || i >= FIELD_N) continue;
      for (let j = cj - cells; j <= cj + cells; j++) {
        if (j < 0 || j >= FIELD_N) continue;
        const wx = FIELD_MIN + i * FIELD_CELL, wz = FIELD_MIN + j * FIELD_CELL;
        const d = Math.hypot(wx - p.x, wz - p.z);
        const k = j * FIELD_N + i;
        if (d < fieldDist[k]) { fieldDist[k] = d; fieldIdx[k] = n; }
      }
    }
  }
}

/* Signed lateral offset, centreline height, bank and distance along the lap.
   Returns null when the query is nowhere near the track. */
const _p = { s: 0, y: 0, bank: 0, along: 0, off: 9999 };
function trackProfile(x, z) {
  const i = Math.round((x - FIELD_MIN) / FIELD_CELL);
  const j = Math.round((z - FIELD_MIN) / FIELD_CELL);
  if (i < 0 || j < 0 || i >= FIELD_N || j >= FIELD_N) return null;
  const seed = fieldIdx[j * FIELD_N + i];
  if (seed < 0) return null;

  const N = trackPts.length;
  let bestT = 0, bestD2 = Infinity, bestSeg = seed;
  for (let o = -2; o <= 1; o++) {
    const a = trackPts[((seed + o) % N + N) % N];
    const b = trackPts[((seed + o + 1) % N + N) % N];
    const ex = b.x - a.x, ez = b.z - a.z;
    const len2 = ex * ex + ez * ez;
    let t = len2 > 0 ? ((x - a.x) * ex + (z - a.z) * ez) / len2 : 0;
    t = clamp(t, 0, 1);
    const px = a.x + ex * t, pz = a.z + ez * t;
    const d2 = (x - px) * (x - px) + (z - pz) * (z - pz);
    if (d2 < bestD2) { bestD2 = d2; bestT = t; bestSeg = ((seed + o) % N + N) % N; }
  }
  const a = trackPts[bestSeg], b = trackPts[(bestSeg + 1) % N];
  const ex = b.x - a.x, ez = b.z - a.z;
  const el = Math.hypot(ex, ez) || 1;
  // signed side: positive to the right of travel
  _p.s = ((x - a.x) * ez - (z - a.z) * ex) / el;
  _p.off = Math.sqrt(bestD2);
  _p.y = a.y + (b.y - a.y) * bestT;
  _p.bank = a.bank + (b.bank - a.bank) * bestT;
  // The closing segment wraps to point 0, whose `along` is 0 — interpolating to
  // that runs the lap distance backwards through the whole track. Close the
  // loop at trackLen instead so distance stays monotonic all the way round.
  const bAlong = (bestSeg === N - 1) ? trackLen : b.along;
  _p.along = a.along + (bAlong - a.along) * bestT;
  return _p;
}

/* 1 on the racing surface, 0 once you're past the berms */
function onTrack(x, z) {
  const p = trackProfile(x, z);
  if (!p) return 0;
  return 1 - smoothstep(TRACK_HALF, TRACK_HALF + BERM_W * 0.8, Math.abs(p.s));
}

/* Bumps that live on the racing line: rhythm-section whoops and table-tops.
   You're in a channel, so these can't be ridden around — which is the whole
   point, and what was wrong with ramps sitting out in the open. */
const features = [];
function trackFeature(along, s) {
  let add = 0;
  for (let i = 0; i < features.length; i++) {
    const f = features[i];
    let d = along - f.at;
    if (d < -trackLen / 2) d += trackLen;
    else if (d > trackLen / 2) d -= trackLen;
    if (d < 0 || d > f.len) continue;
    const lateral = 1 - smoothstep(TRACK_HALF * 0.6, TRACK_HALF * 1.1, Math.abs(s));
    if (f.kind === 'whoops' || f.kind === 'ripples') {
      const n = Math.round(f.len / f.wave);
      add += (0.5 - 0.5 * Math.cos((d / f.len) * n * Math.PI * 2)) * f.h * lateral
           * Math.sin((d / f.len) * Math.PI);           // fade in and out
    } else if (f.kind === 'tabletop') {
      // steep face, flat deck, steep landing — clear it or case the edge
      const t = d / f.len;
      const ramp = smoothstep(0, 0.26, t) * (1 - smoothstep(0.74, 1, t));
      add += ramp * f.h * lateral;
    } else {
      /* A jump needs a crest to be thrown off. A flat top means you ride up it
         and along — no launch, and a taller one gives *less* air than a shorter
         one. A hump has curvature at the top, and the curvature is what puts
         you in the sky. */
      const t = d / f.len;
      add += Math.pow(Math.sin(Math.PI * t), 1.35) * f.h * lateral;
    }
  }
  return add;
}

function terrainH(x, z) {
  const natural = baseHeight(x, z);
  const p = trackProfile(x, z);
  if (!p) return natural + rampHeight(x, z);

  const as = Math.abs(p.s);
  const surface = p.y - TRACK_CUT + p.bank * p.s + trackFeature(p.along, p.s);
  let h;
  if (as <= TRACK_HALF) {
    h = surface;
  } else if (as <= TRACK_HALF + BERM_W) {
    // the raised side that stops you simply riding out of the corner
    const t = (as - TRACK_HALF) / BERM_W;
    h = surface + BERM_H * t * t * (3 - 2 * t);
  } else {
    const t = clamp((as - TRACK_HALF - BERM_W) / BLEND_W, 0, 1);
    const top = surface + BERM_H;
    h = lerp(top, natural, t * t * (3 - 2 * t));
  }
  return h + rampHeight(x, z);
}

const _n = new THREE.Vector3();
function terrainNormal(x, z, out) {
  const e = 0.7;
  const hL = terrainH(x - e, z), hR = terrainH(x + e, z);
  const hD = terrainH(x, z - e), hU = terrainH(x, z + e);
  (out || _n).set(hL - hR, 2 * e, hD - hU).normalize();
  return out || _n;
}

/* ------------------------------------------------------------- renderer */
const canvas = $('#scene');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
// soft shadows are a per-pixel cost the whole terrain pays; phones get the cheap one
renderer.shadowMap.type = MOBILE ? THREE.PCFShadowMap : THREE.PCFSoftShadowMap;

const scene = new THREE.Scene();
const SKY_TOP = 0x1d3a63, SKY_HAZE = 0xf6b077, FOG_COL = 0xe8ac78;
scene.fog = new THREE.Fog(FOG_COL, MOBILE ? 300 : 320, MOBILE ? 1300 : 1500);

const CAM_FAR = MOBILE ? 2200 : 2600;
// the sky shell has to sit comfortably inside the far plane, or it gets clipped
// and you see straight through to the clear colour — a black hole in the sky
const SKY_R = CAM_FAR * 0.82;
const camera = new THREE.PerspectiveCamera(62, 1, 0.4, CAM_FAR);
camera.position.set(0, 8, -14);

/* ------------------------------------------------------------------ sky */
{
  const g = new THREE.SphereGeometry(SKY_R, 32, 20);
  const pos = g.attributes.position;
  const col = new Float32Array(pos.count * 3);
  const cTop = new THREE.Color().setHex(SKY_TOP);
  const cMid = new THREE.Color().setHex(0xc07a72);
  const cHaze = new THREE.Color().setHex(SKY_HAZE);
  const tmp = new THREE.Color();
  for (let i = 0; i < pos.count; i++) {
    const y = pos.getY(i) / SKY_R;
    if (y > 0.02) tmp.copy(cMid).lerp(cTop, smoothstep(0.02, 0.34, y));
    else tmp.copy(cHaze).lerp(cMid, smoothstep(-0.14, 0.02, y));
    col[i * 3] = tmp.r; col[i * 3 + 1] = tmp.g; col[i * 3 + 2] = tmp.b;
  }
  g.setAttribute('color', new THREE.BufferAttribute(col, 3));
  const sky = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.BackSide, fog: false, depthWrite: false }));
  scene.add(sky);
}

/* --------------------------------------------------------------- lights */
const sunDir = new THREE.Vector3(-0.55, 0.52, 0.65).normalize();
const sun = new THREE.DirectionalLight(0xffd6a0, 2.5);
sun.castShadow = true;
sun.shadow.mapSize.set(1024, 1024);
sun.shadow.camera.left = -22; sun.shadow.camera.right = 22;
sun.shadow.camera.top = 22; sun.shadow.camera.bottom = -22;
sun.shadow.camera.near = 1; sun.shadow.camera.far = 120;
sun.shadow.bias = -0.0016;
scene.add(sun, sun.target);
scene.add(new THREE.HemisphereLight(0x9dbbe8, 0x7d5230, 0.6));

/* sun disc */
{
  const s = new THREE.Mesh(
    new THREE.CircleGeometry(46, 32),
    new THREE.MeshBasicMaterial({ color: 0xfff0c8, fog: false, transparent: true, opacity: 0.92 })
  );
  s.position.copy(sunDir).multiplyScalar(SKY_R * 0.92);
  s.lookAt(0, 0, 0);
  scene.add(s);
}

/* -------------------------------------------------------- course layout */
const CP_COUNT = 8;
const checkpoints = [];
{
  let seedA = 0.4;
  for (let i = 0; i < CP_COUNT; i++) {
    const a = (i / CP_COUNT) * Math.PI * 2 + (hash2(i * 7 + 3, 11) - 0.5) * 0.38;
    const rad = 165 + hash2(i * 13 + 5, 29) * 95;
    const x = Math.cos(a) * rad, z = Math.sin(a) * rad;
    checkpoints.push({ x, z, y: 0, a });
    seedA = a;
  }
  /* No ramps on the Baja course. They sat beside the racing line, so going
     round them was always faster — they were an obstacle, not a reward. The
     ramp system stays in the code for the stunt mode; the air out here comes
     from the land itself. */

  // lay the road: a closed Catmull-Rom spline through the gates
  const SPACING = 2.2;
  const n = CP_COUNT;
  const cm = (p0, p1, p2, p3, t, k) => {
    const t2 = t * t, t3 = t2 * t;
    return 0.5 * ((2 * p1[k]) + (-p0[k] + p2[k]) * t +
      (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t2 +
      (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t3);
  };
  for (let i = 0; i < n; i++) {
    const p0 = checkpoints[(i - 1 + n) % n], p1 = checkpoints[i];
    const p2 = checkpoints[(i + 1) % n], p3 = checkpoints[(i + 2) % n];
    const steps = Math.max(6, Math.round(Math.hypot(p2.x - p1.x, p2.z - p1.z) / SPACING));
    if (i === 0) checkpoints[0].idx = trackPts.length;
    else checkpoints[i].idx = trackPts.length;
    for (let s = 0; s < steps; s++) {
      const t = s / steps;
      trackPts.push({ x: cm(p0, p1, p2, p3, t, 'x'), z: cm(p0, p1, p2, p3, t, 'z'), y: 0 });
    }
  }
  // Grade it: follow the land, then smooth along its length so there are no
  // steps to trip over. Smooth it too hard and the road goes billiard-flat and
  // the bike never leaves the ground — this is tuned to keep the roll that
  // throws you into the air over a crest.
  for (const p of trackPts) p.y = baseHeight(p.x, p.z);
  const N = trackPts.length;
  for (let pass = 0; pass < 9; pass++) {
    const prev = trackPts.map((p) => p.y);
    for (let i = 0; i < N; i++) {
      trackPts[i].y = prev[(i - 1 + N) % N] * 0.25 + prev[i] * 0.5 + prev[(i + 1) % N] * 0.25;
    }
  }

  // distance along the lap
  trackLen = 0;
  for (let i = 0; i < N; i++) {
    trackPts[i].along = trackLen;
    const b = trackPts[(i + 1) % N];
    trackLen += Math.hypot(b.x - trackPts[i].x, b.z - trackPts[i].z);
  }

  // Bank the corners: how sharply the centreline turns decides how much the
  // outside edge lifts. Smoothed so the banking eases in and out of a corner
  // rather than snapping on.
  for (let i = 0; i < N; i++) {
    const a = trackPts[(i - 1 + N) % N], b = trackPts[i], c = trackPts[(i + 1) % N];
    const t1x = b.x - a.x, t1z = b.z - a.z;
    const t2x = c.x - b.x, t2z = c.z - b.z;
    const l1 = Math.hypot(t1x, t1z) || 1, l2 = Math.hypot(t2x, t2z) || 1;
    const crossZ = (t1x / l1) * (t2z / l2) - (t1z / l1) * (t2x / l2);
    b.bank = clamp(crossZ * 9, -MAX_BANK, MAX_BANK);
  }
  for (let pass = 0; pass < 12; pass++) {
    const prev = trackPts.map((p) => p.bank);
    for (let i = 0; i < N; i++) {
      trackPts[i].bank = prev[(i - 1 + N) % N] * 0.25 + prev[i] * 0.5 + prev[(i + 1) % N] * 0.25;
    }
  }

  /* Fill the lap with work. A track you can hold flat out is a boring track, so
     features are packed nose-to-tail with only short breathers between them:
       ripples  — fast, shallow chatter that shakes the suspension. Fine in
                  corners, since braking bumps live on corner entry anyway.
       whoops   — big rolling swells you either double or get bucked by.
       table    — a proper jump, on the racing line so it can't be avoided.  */
  const worstBank = (at, len) => {
    let worst = 0;
    for (let d = 0; d < len; d += 4) {
      const i = Math.round((((at + d) % trackLen) / trackLen) * N) % N;
      worst = Math.max(worst, Math.abs(trackPts[i].bank));
    }
    return worst;
  };
  /* Laid out deliberately rather than left to chance: six jumps that build in
     size, four sets of whoops between them, and exactly one chatter section.
     Spaced evenly round the lap and nudged clear of the banked corners, which
     stay clean so they can actually be raced. */
  const plan = [
    /* Jump size is set by height over a roughly fixed length — that ratio is
       what decides how hard you get launched. Stretching the length as well
       just flattens the crest out again. */
    /* Two different jobs, kept clearly apart:
         rollers — long, shallow undulation as a run-up to a jump. Should flow.
         ripples — one short, sharp, chewed-up braking-bump section that
                   genuinely rattles the bike. Exactly one per lap. */
    { kind: 'table', len: 30, h: 1.9 },      // gentle opener
    { kind: 'whoops', len: 46, wave: 16, h: 1.3 },
    { kind: 'table', len: 29, h: 2.6 },
    { kind: 'whoops', len: 50, wave: 17, h: 1.4 },
    { kind: 'tabletop', len: 36, h: 3.6 },   // flat deck — clear it or case it
    { kind: 'ripples', len: 46, wave: 4.2, h: 0.62 },   // the only rough patch
    { kind: 'table', len: 30, h: 2.2 },
    { kind: 'whoops', len: 46, wave: 16, h: 1.35 },
    { kind: 'table', len: 27, h: 3.0 },
    { kind: 'whoops', len: 48, wave: 17, h: 1.45 },
    { kind: 'table', len: 27, h: 4.2 }       // the big one
  ];
  const slot = trackLen / plan.length;
  for (let i = 0; i < plan.length; i++) {
    let at = 24 + i * slot;
    for (let tries = 0; tries < 14 && worstBank(at, plan[i].len + 24) > MAX_BANK * 0.34; tries++) {
      at += 5;
    }
    features.push(Object.assign({ at: at % trackLen }, plan[i]));
  }

  bakeTrackField();
  for (const c of checkpoints) c.y = terrainH(c.x, c.z);
}

/* ------------------------------------------------------------- terrain */
const SAND = new THREE.Color().setHex(0xe3be86);
const OCHRE = new THREE.Color().setHex(0xb4783c);
const DIRT = new THREE.Color().setHex(0x6d4a2c);
const SCRUB = new THREE.Color().setHex(0x8b8a4e);
const ROCK = new THREE.Color().setHex(0x9a8a7c);

// a redder, darker clay than the surrounding sand — the racing line has to read
// as a different surface from a distance, not just a slightly darker patch
const PACKED = new THREE.Color().setHex(0x8a4520);
const CHEWED = new THREE.Color().setHex(0x5c3018);   // churned-up braking bumps

/* How strongly a given point along the lap sits inside a feature of some kind —
   used to shade the rough sections so they're visible on the approach. */
function featureBlend(along, kind) {
  let best = 0;
  for (let i = 0; i < features.length; i++) {
    const f = features[i];
    if (f.kind !== kind) continue;
    let d = along - f.at;
    if (d < -trackLen / 2) d += trackLen;
    else if (d > trackLen / 2) d -= trackLen;
    if (d < -6 || d > f.len + 6) continue;
    best = Math.max(best, smoothstep(-6, 4, d) * (1 - smoothstep(f.len - 4, f.len + 6, d)));
  }
  return best;
}

function groundColor(x, z, h, slope, out) {
  const mott = fbm(x * 0.02, z * 0.02, 2);
  const band = fbm(x * 0.0045 + 7, z * 0.0045 - 3, 2);
  out.copy(SAND).lerp(OCHRE, clamp(mott * 1.5 - 0.15, 0, 1) * 0.85 + band * 0.3);
  const veg = smoothstep(0.34, 0.08, slope) * smoothstep(0.5, 0.18, mott);
  out.lerp(SCRUB, veg * 0.5);
  out.lerp(DIRT, smoothstep(0.3, 0.7, slope) * 0.85);
  out.lerp(ROCK, smoothstep(45, 120, h) * 0.7);
  const shade = 0.8 + mott * 0.36;
  out.multiplyScalar(shade);
  // worn road surface — the thing you're meant to be following
  const p = trackProfile(x, z);
  if (p) {
    const as = Math.abs(p.s);
    // the racing surface, plus a scuffed band up the face of each berm
    const road = 1 - smoothstep(TRACK_HALF * 0.9, TRACK_HALF + BERM_W * 0.55, as);
    out.lerp(PACKED, road * 0.88);
    // darker ruts either side of the centre line
    const rut = Math.exp(-Math.pow((as - TRACK_HALF * 0.45) / (TRACK_HALF * 0.22), 2));
    out.multiplyScalar(1 - road * rut * 0.12);
    // chewed-up braking bumps read darker, so you can see them coming
    const chew = featureBlend(p.along, 'ripples');
    if (chew > 0) out.lerp(CHEWED, chew * road * 0.7);
  }
  return out;
}

const groundMat = new THREE.MeshLambertMaterial({ vertexColors: true });
{
  const geo = new THREE.PlaneGeometry(WORLD, WORLD, SEG, SEG);
  geo.rotateX(-Math.PI / 2);
  const pos = geo.attributes.position;
  const col = new Float32Array(pos.count * 3);
  const tmp = new THREE.Color();
  const nrm = new THREE.Vector3();
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), z = pos.getZ(i);
    const h = terrainH(x, z);
    pos.setY(i, h);
    terrainNormal(x, z, nrm);
    groundColor(x, z, h, 1 - nrm.y, tmp);
    col[i * 3] = tmp.r; col[i * 3 + 1] = tmp.g; col[i * 3 + 2] = tmp.b;
  }
  geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
  geo.computeVertexNormals();
  const ground = new THREE.Mesh(geo, groundMat);
  ground.receiveShadow = true;
  scene.add(ground);
}

/* ramp patches — high-res so the lips stay sharp */
{
  const RC = 26, RS = 14;                   // grid along / across
  const positions = [], colors = [], indices = [];
  const tmp = new THREE.Color();
  const nrm = new THREE.Vector3();
  const PACKED = new THREE.Color().setHex(0x7c5431);
  let base = 0;
  for (const r of ramps) {
    for (let a = 0; a <= RC; a++) {
      const f = (a / RC) * (r.len + 2) - 2;
      for (let b = 0; b <= RS; b++) {
        const s = (b / RS) * 2 * r.wid - r.wid;
        const x = r.x + r.cos * f - r.sin * s;
        const z = r.z + r.sin * f + r.cos * s;
        const y = terrainH(x, z) + 0.05;
        positions.push(x, y, z);
        terrainNormal(x, z, nrm);
        groundColor(x, z, y, 1 - nrm.y, tmp);
        const packed = smoothstep(r.wid * 0.95, r.wid * 0.45, Math.abs(s)) * smoothstep(-1, 3, f);
        tmp.lerp(PACKED, packed * 0.65);
        colors.push(tmp.r, tmp.g, tmp.b);
      }
    }
    for (let a = 0; a < RC; a++) {
      for (let b = 0; b < RS; b++) {
        const i0 = base + a * (RS + 1) + b;
        const i1 = i0 + 1, i2 = i0 + RS + 1, i3 = i2 + 1;
        indices.push(i0, i2, i1, i1, i2, i3);
      }
    }
    base += (RC + 1) * (RS + 1);
    // end skirt so the lip reads as a solid edge
    const skirtStart = base;
    for (let b = 0; b <= RS; b++) {
      const s = (b / RS) * 2 * r.wid - r.wid;
      const x = r.x + r.cos * r.len - r.sin * s;
      const z = r.z + r.sin * r.len + r.cos * s;
      const top = terrainH(x, z) + 0.05;
      const bot = baseHeight(x, z) - 0.4;
      positions.push(x, top, z, x, bot, z);
      colors.push(DIRT.r * 0.8, DIRT.g * 0.8, DIRT.b * 0.8);
      colors.push(DIRT.r * 0.5, DIRT.g * 0.5, DIRT.b * 0.5);
    }
    for (let b = 0; b < RS; b++) {
      const i0 = skirtStart + b * 2;
      indices.push(i0, i0 + 1, i0 + 2, i0 + 2, i0 + 1, i0 + 3);
    }
    base += (RS + 1) * 2;
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  g.setIndex(indices);
  g.computeVertexNormals();
  const m = new THREE.Mesh(g, new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide }));
  m.receiveShadow = true;
  scene.add(m);
}

/* ------------------------------------------------- detail over features */
/* The main terrain grid has ~4.5m cells, which cannot draw a 4m chatter bump —
   the physics samples the height function directly and feels every ripple, but
   the mesh has nowhere near enough vertices to show one. Riders would be
   bounced by bumps they can't see. So each feature section gets its own strip
   of high-resolution geometry laid over the top. */
{
  const positions = [], colors = [], indices = [];
  const tmp = new THREE.Color();
  const nrm = new THREE.Vector3();
  const N = trackPts.length;
  const HALF = TRACK_HALF + 2.5;
  let base = 0;

  for (const f of features) {
    const pad = 8;
    const step = f.kind === 'ripples' ? 0.7 : 1.4;     // chatter needs the most
    const cols = Math.max(6, Math.round((f.len + pad * 2) / step));
    const rows = 14;
    for (let a = 0; a <= cols; a++) {
      const along = f.at - pad + (a / cols) * (f.len + pad * 2);
      const i = ((Math.round((along / trackLen) * N)) % N + N) % N;
      const p = trackPts[i], q = trackPts[(i + 1) % N];
      const ex = q.x - p.x, ez = q.z - p.z;
      const el = Math.hypot(ex, ez) || 1;
      const nx = ez / el, nz = -ex / el;
      for (let b = 0; b <= rows; b++) {
        const s = (b / rows) * 2 * HALF - HALF;
        const x = p.x + nx * s, z = p.z + nz * s;
        const y = terrainH(x, z);
        positions.push(x, y, z);
        terrainNormal(x, z, nrm);
        groundColor(x, z, y, 1 - nrm.y, tmp);
        colors.push(tmp.r, tmp.g, tmp.b);
      }
    }
    for (let a = 0; a < cols; a++) {
      for (let b = 0; b < rows; b++) {
        const i0 = base + a * (rows + 1) + b;
        const i1 = i0 + 1, i2 = i0 + rows + 1, i3 = i2 + 1;
        indices.push(i0, i2, i1, i1, i2, i3);
      }
    }
    base += (cols + 1) * (rows + 1);
  }

  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  g.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  g.setIndex(indices);
  g.computeVertexNormals();
  const m = new THREE.Mesh(g, new THREE.MeshLambertMaterial({
    vertexColors: true,
    // sits exactly on the base terrain, so bias it forward rather than lifting it
    polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2
  }));
  m.receiveShadow = true;
  scene.add(m);
}

/* -------------------------------------------------------------- scenery */
function scatter(geo, mat, count, scaleMin, scaleMax, maxSlope, clearance) {
  const mesh = new THREE.InstancedMesh(geo, mat, count);
  const d = new THREE.Object3D();
  const nrm = new THREE.Vector3();
  let placed = 0, tries = 0;
  while (placed < count && tries < count * 12) {
    tries++;
    const a = hash2(tries * 3 + 1, 97) * Math.PI * 2;
    const rr = Math.sqrt(hash2(tries * 5 + 9, 61)) * (PLAY_R + 60);
    const x = Math.cos(a) * rr, z = Math.sin(a) * rr;
    if (rr < 26) continue;
    if (rampHeight(x, z) > 0.05) continue;
    const tp = trackProfile(x, z);
    if (tp && Math.abs(tp.s) < clearance) continue;   // keep the track clear
    terrainNormal(x, z, nrm);
    if (1 - nrm.y > maxSlope) continue;
    d.position.set(x, terrainH(x, z) - 0.15, z);
    d.rotation.set(0, hash2(tries, 7) * 6.28, 0);
    const s = lerp(scaleMin, scaleMax, hash2(tries * 11, 23));
    d.scale.set(s, s * lerp(0.8, 1.4, hash2(tries * 17, 3)), s);
    d.updateMatrix();
    mesh.setMatrixAt(placed++, d.matrix);
  }
  mesh.count = placed;
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  scene.add(mesh);
  return mesh;
}
// far fewer rocks than before, and none of them anywhere near the road
scatter(new THREE.DodecahedronGeometry(1, 0), new THREE.MeshLambertMaterial({ color: 0x9a8977 }), MOBILE ? 70 : 110, 0.7, 3.2, 0.55, 22);
scatter(new THREE.ConeGeometry(0.8, 1.8, 5), new THREE.MeshLambertMaterial({ color: 0x7d8348 }), MOBILE ? 170 : 300, 0.5, 1.3, 0.4, 13);

/* ------------------------------------------------- start / finish gantry */
{
  const N = trackPts.length;
  const p = trackPts[0], q = trackPts[3 % N];
  const dirA = Math.atan2(q.z - p.z, q.x - p.x);
  const g = new THREE.Group();
  const span = TRACK_HALF + BERM_W * 0.5;

  const postMat = new THREE.MeshLambertMaterial({ color: 0xe9e4da });
  const barMat = new THREE.MeshLambertMaterial({ color: 0xe4453a });
  for (const sx of [-span, span]) {
    const post = new THREE.Mesh(new THREE.BoxGeometry(0.7, 9, 0.7), postMat);
    post.position.set(sx, 4.5, 0);
    post.castShadow = true;
    g.add(post);
  }
  const beam = new THREE.Mesh(new THREE.BoxGeometry(span * 2 + 1.4, 1.9, 0.9), barMat);
  beam.position.y = 9.4;
  beam.castShadow = true;
  g.add(beam);
  const banner = new THREE.Mesh(
    new THREE.PlaneGeometry(span * 2, 1.3),
    new THREE.MeshBasicMaterial({ color: 0x0b0d10, side: THREE.DoubleSide })
  );
  banner.position.set(0, 7.9, 0.5);
  g.add(banner);

  // chequered strip painted across the track
  const strip = new THREE.Group();
  const SQ = 12;
  for (let i = 0; i < SQ; i++) {
    for (let j = 0; j < 2; j++) {
      const q2 = new THREE.Mesh(
        new THREE.PlaneGeometry(span * 2 / SQ, 1.5),
        new THREE.MeshBasicMaterial({ color: (i + j) % 2 ? 0xf2ede3 : 0x1b1d22 })
      );
      q2.rotation.x = -Math.PI / 2;
      q2.position.set(-span + (i + 0.5) * (span * 2 / SQ), 0, (j - 0.5) * 1.5);
      strip.add(q2);
    }
  }
  strip.position.y = 0.12;
  g.add(strip);

  g.rotation.y = -dirA + Math.PI / 2;
  g.position.set(p.x, p.y - TRACK_CUT, p.z);
  scene.add(g);
  // the strip follows the banked surface rather than floating over it
  strip.rotation.z = Math.atan(trackPts[0].bank);
}

/* ---------------------------------------------------------------- bike */
const bike = new THREE.Group();
scene.add(bike);
const bikeBody = new THREE.Group();
bike.add(bikeBody);

const matRed = new THREE.MeshLambertMaterial({ color: 0xd8352a });
const matDark = new THREE.MeshLambertMaterial({ color: 0x22242a });
const matChrome = new THREE.MeshLambertMaterial({ color: 0xb9bdc4 });
const matWhite = new THREE.MeshLambertMaterial({ color: 0xeee9df });
const matSkin = new THREE.MeshLambertMaterial({ color: 0xd7a077 });
const matSuit = new THREE.MeshLambertMaterial({ color: 0x2f6fd0 });

function box(w, h, d, mat, x, y, z, rx) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
  m.position.set(x, y, z);
  if (rx) m.rotation.x = rx;
  m.castShadow = true;
  return m;
}
function cyl(rt, rb, h, mat, x, y, z, rx, rz) {
  const m = new THREE.Mesh(new THREE.CylinderGeometry(rt, rb, h, 10), mat);
  m.position.set(x, y, z);
  if (rx) m.rotation.x = rx;
  if (rz) m.rotation.z = rz;
  m.castShadow = true;
  return m;
}

const WHEEL_R = 0.4;
function makeWheel() {
  const g = new THREE.Group();
  const tyre = new THREE.Mesh(new THREE.TorusGeometry(WHEEL_R, 0.115, 8, 22), matDark);
  const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.1, 0.16, 8), matChrome);
  hub.rotation.x = Math.PI / 2;
  const spoke = new THREE.Mesh(new THREE.BoxGeometry(WHEEL_R * 1.8, 0.05, 0.03), matChrome);
  const spoke2 = spoke.clone(); spoke2.rotation.z = Math.PI / 2;
  tyre.castShadow = true;
  g.add(tyre, hub, spoke, spoke2);
  g.rotation.y = Math.PI / 2;
  return g;
}
const wheelR = makeWheel(), wheelF = makeWheel();
wheelR.position.set(0, WHEEL_R, -0.72);
wheelF.position.set(0, WHEEL_R, 0.78);
bikeBody.add(wheelR, wheelF);

bikeBody.add(box(0.3, 0.34, 1.05, matDark, 0, 0.72, -0.1));
bikeBody.add(box(0.34, 0.3, 0.52, matRed, 0, 0.94, 0.18));
bikeBody.add(box(0.3, 0.12, 0.56, matDark, 0, 0.99, -0.36));
bikeBody.add(box(0.42, 0.34, 0.06, matWhite, 0, 1.02, 0.52));
bikeBody.add(box(0.36, 0.28, 0.08, matRed, 0, 0.5, -0.95));
bikeBody.add(cyl(0.07, 0.07, 0.9, matChrome, -0.18, 0.72, 0.62, 0.28));
bikeBody.add(cyl(0.07, 0.07, 0.9, matChrome, 0.18, 0.72, 0.62, 0.28));
bikeBody.add(cyl(0.05, 0.05, 0.66, matDark, 0, 1.16, 0.5, 0, Math.PI / 2));
bikeBody.add(cyl(0.06, 0.09, 0.7, matChrome, 0.2, 0.62, -0.6, Math.PI / 2 - 0.15));
bikeBody.add(box(0.5, 0.05, 0.24, matDark, 0, 0.45, -0.05));

/* rider */
const rider = new THREE.Group();
rider.position.set(0, 0.98, -0.16);
bikeBody.add(rider);
const torso = box(0.36, 0.52, 0.26, matSuit, 0, 0.24, 0.06, -0.5);
rider.add(torso);
const head = new THREE.Mesh(new THREE.SphereGeometry(0.15, 12, 10), matWhite);
head.position.set(0, 0.55, 0.3);
head.castShadow = true;
rider.add(head);
const visor = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.08, 0.05), matDark);
visor.position.set(0, 0.54, 0.44);
rider.add(visor);
for (const sx of [-1, 1]) {
  const arm = box(0.1, 0.44, 0.1, matSuit, sx * 0.2, 0.28, 0.32, 0.85);
  rider.add(arm);
  const leg = box(0.14, 0.44, 0.16, matDark, sx * 0.15, -0.1, -0.1, 0.5);
  rider.add(leg);
  const boot = box(0.13, 0.12, 0.24, matDark, sx * 0.17, -0.34, -0.24);
  rider.add(boot);
}

/* ---------------------------------------------------------------- dust */
const DUST_N = MOBILE ? 200 : 260;
const dustPos = new Float32Array(DUST_N * 3);
const dustLife = new Float32Array(DUST_N);
const dustVel = new Float32Array(DUST_N * 3);
let dustCursor = 0;
{
  for (let i = 0; i < DUST_N; i++) { dustPos[i * 3 + 1] = -9999; dustLife[i] = 0; }
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const ctx = c.getContext('2d');
  const grd = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
  grd.addColorStop(0, 'rgba(246,224,190,0.7)');
  grd.addColorStop(0.45, 'rgba(214,178,130,0.28)');
  grd.addColorStop(1, 'rgba(226,190,140,0)');
  ctx.fillStyle = grd;
  ctx.fillRect(0, 0, 64, 64);
  const tex = new THREE.CanvasTexture(c);
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(dustPos, 3));
  const pts = new THREE.Points(g, new THREE.PointsMaterial({
    size: 1.15, map: tex, transparent: true, depthWrite: false,
    opacity: 0.34, sizeAttenuation: true, blending: THREE.NormalBlending
  }));
  pts.frustumCulled = false;
  scene.add(pts);
  var dustGeo = g;
}
function emitDust(x, y, z, power) {
  const i = dustCursor = (dustCursor + 1) % DUST_N;
  dustPos[i * 3] = x + (Math.random() - 0.5) * 0.5;
  dustPos[i * 3 + 1] = y + 0.1;
  dustPos[i * 3 + 2] = z + (Math.random() - 0.5) * 0.5;
  dustVel[i * 3] = (Math.random() - 0.5) * 2;
  dustVel[i * 3 + 1] = 0.6 + Math.random() * 1.6 * power;
  dustVel[i * 3 + 2] = (Math.random() - 0.5) * 2;
  dustLife[i] = 0.9 + Math.random() * 0.6;
}

/* ------------------------------------------------------------- controls */
const keys = Object.create(null);
const keyAt = Object.create(null);   // when each key was last pressed
const HELD = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space', 'KeyW', 'KeyA', 'KeyS', 'KeyD', 'ShiftLeft'];
addEventListener('keydown', (e) => {
  if (HELD.includes(e.code)) e.preventDefault();
  if (!keys[e.code]) keyAt[e.code] = clock;
  keys[e.code] = true;
  if (e.code === 'KeyR') respawn(true);
  if (e.code === 'KeyM') toggleAudio();
  if (e.code === 'KeyC') cycleCam();
  if (e.code === 'Escape' || e.code === 'KeyP') togglePause();
  if (e.code === 'KeyQ' && started) quitToTitle();
  if (!started && (e.code === 'Enter' || e.code === 'Space')) startGame();
});
addEventListener('keyup', (e) => { keys[e.code] = false; });
addEventListener('blur', () => { for (const k in keys) keys[k] = false; if (started) setPaused(true); });

/* ---------------------------------------------------------- touch input */
/* One thumb on a floating stick does what the original's joystick did: left and
   right steers on the ground and whips in the air, back and forward lifts or
   drops the nose. The other thumb only has to worry about speed. */
const touch = { steer: 0, gas: 0, brake: 0, pitch: 0 };
const STICK_R = 62;                       // px of travel for full deflection
let autoThrottle = loadPref('autoThrottle', true);
let touchMode = matchMedia('(pointer: coarse)').matches;

function loadPref(k, dflt) {
  try {
    const v = localStorage.getItem('dustbowl.' + k);
    return v === null ? dflt : JSON.parse(v);
  } catch (e) { return dflt; }
}
function savePref(k, v) {
  try { localStorage.setItem('dustbowl.' + k, JSON.stringify(v)); } catch (e) { /* private mode */ }
}

function setTouchMode(on) {
  touchMode = on;
  document.body.classList.toggle('touch', on);
}
setTouchMode(touchMode);
document.body.classList.add('mode-race');   // stunt mode will clear this
// insurance: if the media query is wrong inside an embedded frame, the first
// real touch still brings the controls up rather than leaving you with none
addEventListener('touchstart', () => setTouchMode(true), { once: true, passive: true });

function setThrottleMode(auto) {
  autoThrottle = auto;
  savePref('autoThrottle', auto);
  const label = auto ? 'AUTO' : 'MANUAL';
  for (const el of document.querySelectorAll('.throttleState')) el.textContent = label;
  document.body.classList.toggle('manual-throttle', !auto);
}

{
  const zone = $('#stickZone'), base = $('#stickBase'), knob = $('#stickKnob');
  let stickId = null, sx = 0, sy = 0;

  const showStick = (x, y) => {
    base.style.left = x + 'px';
    base.style.top = y + 'px';
    base.classList.add('on');
  };
  const moveKnob = (dx, dy) => {
    const d = Math.hypot(dx, dy);
    const s = d > STICK_R ? STICK_R / d : 1;
    knob.style.transform = 'translate(-50%,-50%) translate(' + dx * s + 'px,' + dy * s + 'px)';
  };

  zone.addEventListener('pointerdown', (e) => {
    if (stickId !== null) return;
    stickId = e.pointerId;
    zone.setPointerCapture(e.pointerId);
    sx = e.clientX; sy = e.clientY;
    showStick(sx, sy);
    moveKnob(0, 0);
    e.preventDefault();
  });
  zone.addEventListener('pointermove', (e) => {
    if (e.pointerId !== stickId) return;
    const dx = e.clientX - sx, dy = e.clientY - sy;
    touch.steer = clamp(dx / STICK_R, -1, 1);
    touch.pitch = clamp(-dy / STICK_R, -1, 1);   // pull back = nose up
    moveKnob(dx, dy);
    e.preventDefault();
  });
  const release = (e) => {
    if (e.pointerId !== stickId) return;
    stickId = null;
    touch.steer = 0;
    touch.pitch = 0;
    base.classList.remove('on');
    moveKnob(0, 0);
  };
  zone.addEventListener('pointerup', release);
  zone.addEventListener('pointercancel', release);

  const hold = (el, set) => {
    const on = (e) => { set(1); el.classList.add('on'); el.setPointerCapture(e.pointerId); e.preventDefault(); };
    const off = (e) => { set(0); el.classList.remove('on'); };
    el.addEventListener('pointerdown', on);
    el.addEventListener('pointerup', off);
    el.addEventListener('pointercancel', off);
  };
  hold($('#tGas'), (v) => touch.gas = v);
  hold($('#tBrake'), (v) => touch.brake = v);
}

/* ---------------------------------------------------------------- audio */
let actx = null, engOsc = null, engGain = null, engFilt = null, windGain = null, audioOn = true;
function initAudio() {
  if (actx) return;
  const AC = window.AudioContext || window.webkitAudioContext;
  if (!AC) return;
  actx = new AC();
  engGain = actx.createGain();
  engGain.gain.value = 0;
  engFilt = actx.createBiquadFilter();
  engFilt.type = 'lowpass';
  engFilt.frequency.value = 900;
  engOsc = actx.createOscillator();
  engOsc.type = 'sawtooth';
  engOsc.frequency.value = 60;
  const sub = actx.createOscillator();
  sub.type = 'square';
  sub.frequency.value = 30;
  const subG = actx.createGain();
  subG.gain.value = 0.35;
  engOsc.connect(engFilt);
  sub.connect(subG).connect(engFilt);
  engFilt.connect(engGain).connect(actx.destination);
  engOsc.start(); sub.start();
  engOsc.subOsc = sub;

  // wind
  const buf = actx.createBuffer(1, actx.sampleRate * 2, actx.sampleRate);
  const d = buf.getChannelData(0);
  for (let i = 0; i < d.length; i++) d[i] = (Math.random() * 2 - 1) * 0.4;
  const src = actx.createBufferSource();
  src.buffer = buf; src.loop = true;
  const wf = actx.createBiquadFilter();
  wf.type = 'bandpass'; wf.frequency.value = 700; wf.Q.value = 0.6;
  windGain = actx.createGain();
  windGain.gain.value = 0;
  src.connect(wf).connect(windGain).connect(actx.destination);
  src.start();
}
function silenceAudio() {
  if (engGain) engGain.gain.value = 0;
  if (windGain) windGain.gain.value = 0;
}
function toggleAudio() {
  audioOn = !audioOn;
  const label = audioOn ? 'ON' : 'OFF';
  $('#sndState').textContent = label;
  $('#sndBtnState').textContent = label;
  if (!audioOn) silenceAudio();
}

/* The engine note is written from the render loop, so if the loop ever stops —
   the artifact panel is closed, the tab is hidden, the iframe is detached — the
   gain would freeze at its last value and drone on. Nothing below relies on the
   render loop still running. */
function killAudio() {
  silenceAudio();
  if (actx && actx.state === 'running') actx.suspend();
}
function wakeAudio() {
  if (actx && actx.state === 'suspended') actx.resume();
}
addEventListener('pagehide', killAudio);
addEventListener('beforeunload', killAudio);
document.addEventListener('visibilitychange', () => {
  if (document.hidden) { killAudio(); if (started) setPaused(true); }
});
// Backstop: a hidden iframe stops getting frames but keeps running timers, and
// it may never fire visibilitychange (that follows the *top* document). If the
// loop has gone quiet, cut the sound.
setInterval(() => {
  if (!actx) return;
  if (performance.now() - lastFrameAt > 800) {
    killAudio();
    if (started && !paused) setPaused(true);
  }
}, 400);
function thud(vol) {
  if (!actx || !audioOn) return;
  const t = actx.currentTime;
  const o = actx.createOscillator();
  o.type = 'sine';
  o.frequency.setValueAtTime(150, t);
  o.frequency.exponentialRampToValueAtTime(40, t + 0.22);
  const g = actx.createGain();
  g.gain.setValueAtTime(Math.min(0.5, vol), t);
  g.gain.exponentialRampToValueAtTime(0.001, t + 0.3);
  o.connect(g).connect(actx.destination);
  o.start(t); o.stop(t + 0.32);
}
function chime(freq) {
  if (!actx || !audioOn) return;
  const t = actx.currentTime;
  const o = actx.createOscillator();
  o.type = 'triangle';
  o.frequency.setValueAtTime(freq, t);
  const g = actx.createGain();
  g.gain.setValueAtTime(0.22, t);
  g.gain.exponentialRampToValueAtTime(0.001, t + 0.4);
  o.connect(g).connect(actx.destination);
  o.start(t); o.stop(t + 0.42);
}

/* ----------------------------------------------------------- bike state */
const S = {
  pos: new THREE.Vector3(),
  vel: new THREE.Vector3(),
  quat: new THREE.Quaternion(),
  yaw: 0,
  grounded: true,
  airTime: 0,
  lastGoodPos: new THREE.Vector3(),
  lastGoodYaw: 0,
  crashed: false,
  crashT: 0,
  tumble: new THREE.Vector3(),
  wheelSpin: 0,
  flipAccum: 0,
  lean: 0,
  airStart: -1,
  climb: 0,       // smoothed rate the ground is rising under the wheels (m/s)
  surf: 1,        // 1 on the road, 0 out in the sand
  groundSmooth: null,   // suspended chassis line
  jolt: 0         // how much bump the suspension is currently eating
};
const SUSP_TRAVEL = 0.42;   // metres of give before the chassis is pushed
function takeoff() {
  // Carry the ramp's launch into the air. The suspension hugs the surface so
  // tightly that vel.y is ~0 at the lip, and the terrain normal is unusable
  // there (it straddles the vertical drop), so use the rate the ground has
  // been rising under the wheels instead.
  S.vel.y = Math.max(S.vel.y, Math.min(S.climb, 26));
  S.airTime = 0;
  S.flipAccum = 0;
  S.airStart = clock;
}
const G = {
  score: 0, nextCP: 0, lap: 1, lapStart: 0, best: null,
  bigAir: 0, combo: 0, totalAir: 0,
  finished: false, raceStart: 0, raceTime: 0, lapTimes: []
};
let started = false, paused = false;

const upV = new THREE.Vector3(0, 1, 0);
const fwdV = new THREE.Vector3();
const rightV = new THREE.Vector3();
const bikeUp = new THREE.Vector3();
const gN = new THREE.Vector3();
const tmpV = new THREE.Vector3();
const tmpV2 = new THREE.Vector3();
const tmpQ = new THREE.Quaternion();
const leanQ = new THREE.Quaternion();
const tmpM = new THREE.Matrix4();
const tmpE = new THREE.Euler();
const fwdAxis = new THREE.Vector3(0, 0, 1);

/* Drop the bike back on the track, pointing the right way round. */
function trackSpawn(atAlong) {
  const N = trackPts.length;
  const i = ((Math.round((atAlong / trackLen) * N)) % N + N) % N;
  const p = trackPts[i], q = trackPts[(i + 3) % N];
  return { x: p.x, z: p.z, yaw: Math.atan2(q.x - p.x, q.z - p.z) };
}

function respawn(manual) {
  let x, z, yaw;
  if (manual) {
    // on the grid, just behind the start line
    const s = trackSpawn(trackLen - 18);
    x = s.x; z = s.z; yaw = s.yaw;
  } else {
    // rejoin facing forwards wherever you fell off
    const p = trackProfile(S.lastGoodPos.x, S.lastGoodPos.z);
    const s = trackSpawn(p ? p.along : 0);
    x = s.x; z = s.z; yaw = s.yaw;
  }
  S.pos.set(x, terrainH(x, z) + RIDE_H + 0.6, z);
  S.vel.set(0, 0, 0);
  S.yaw = yaw;
  S.quat.setFromAxisAngle(upV, yaw);
  S.grounded = true;
  S.airTime = 0;
  S.crashed = false;
  S.crashT = 0;
  S.flipAccum = 0;
  S.lean = 0;
  S.climb = 0;
  S.groundSmooth = null;
  S.jolt = 0;
  G.combo = 0;
  resetGateTracking();
  hideBanner();
}

function resetRun() {
  G.score = 0; G.nextCP = 0; G.lap = 1; G.lapStart = clock; G.totalAir = 0; G.bigAir = 0;
  G.finished = false; G.raceStart = clock; G.raceTime = 0; G.lapTimes = [];
  respawn(true);
  S.lastGoodPos.copy(S.pos);
  S.lastGoodYaw = S.yaw;
}

/* -------------------------------------------------------------- physics */
function step(dt) {
  const steerIn = clamp((keys.ArrowRight || keys.KeyD ? 1 : 0) - (keys.ArrowLeft || keys.KeyA ? 1 : 0) + touch.steer, -1, 1);
  const brakeIn = (keys.ArrowDown || keys.KeyS ? 1 : 0) + touch.brake + (keys.Space ? 1 : 0);
  // auto-throttle: the bike drives itself unless you're on the brake
  const gasIn = autoThrottle && !S.crashed
    ? (brakeIn > 0 ? 0 : 1)
    : (keys.ArrowUp || keys.KeyW ? 1 : 0) + touch.gas;
  // In the air the throttle keys double as pitch control — but only if the rider
  // presses them *after* leaving the ground. Holding the gas through a jump
  // shouldn't quietly tip you onto your face.
  // (the stick is analogue and self-centring, so it needs no such guard)
  const fresh = (c) => keys[c] && keyAt[c] > S.airStart;
  const pitchIn = clamp(
    (fresh('ArrowDown') || fresh('KeyS') ? 1 : 0) - (fresh('ArrowUp') || fresh('KeyW') ? 1 : 0) - touch.pitch,
    -1, 1);

  const ctrl = S.crashed ? 0 : 1;

  // ------- crash tumble
  if (S.crashed) {
    S.crashT += dt;
    S.vel.y -= GRAV * dt;
    S.pos.addScaledVector(S.vel, dt);
    const gh = terrainH(S.pos.x, S.pos.z);
    if (S.pos.y < gh + 0.5) {
      S.pos.y = gh + 0.5;
      S.vel.multiplyScalar(0.55);
      S.vel.y = Math.abs(S.vel.y) * 0.25;
      S.tumble.multiplyScalar(0.6);
      if (Math.random() < 0.6) emitDust(S.pos.x, gh, S.pos.z, 1.4);
    }
    tmpE.set(S.tumble.x * dt, S.tumble.y * dt, S.tumble.z * dt);
    tmpQ.setFromEuler(tmpE);
    S.quat.multiply(tmpQ);
    if (S.crashT > 1.7) respawn(false);
    return;
  }

  fwdV.set(Math.sin(S.yaw), 0, Math.cos(S.yaw));

  /* The wheels follow the ground directly. An earlier version ran the chassis
     through a low-pass filter to model suspension travel, but it swallowed the
     chatter section whole *and* softened the jump faces — the bike felt better
     without it. The filtered line survives for the rider-shake animation only,
     where it can't affect how anything handles. */
  const gh = terrainH(S.pos.x, S.pos.z);
  if (S.groundSmooth === null) S.groundSmooth = gh;
  S.groundSmooth = lerp(S.groundSmooth, gh, 1 - Math.exp(-26 * dt));
  S.jolt = Math.min(Math.abs(gh - S.groundSmooth), SUSP_TRAVEL);   // visual only
  terrainNormal(S.pos.x, S.pos.z, gN);
  const wasGrounded = S.grounded;
  S.grounded = S.pos.y <= gh + RIDE_H + 0.06;
  if (wasGrounded && !S.grounded) takeoff();

  if (S.grounded) {
    // -- landing
    if (!wasGrounded) onLand(gN);

    S.pos.y = Math.max(S.pos.y, gh + RIDE_H);
    // slope-aligned basis
    tmpV.copy(fwdV).addScaledVector(gN, -fwdV.dot(gN));
    if (tmpV.lengthSq() < 1e-5) tmpV.copy(fwdV);
    tmpV.normalize();
    rightV.crossVectors(gN, tmpV).normalize();

    const speedF = S.vel.dot(tmpV);
    const absSpeed = S.vel.length();

    // engine
    if (gasIn > 0) {
      const power = 32 * (1 - clamp(speedF / MAX_SPEED, 0, 1));
      S.vel.addScaledVector(tmpV, power * dt * ctrl);
    }
    if (brakeIn > 0) {
      const b = speedF > 0 ? -24 : 12;
      S.vel.addScaledVector(tmpV, b * dt * clamp(Math.abs(speedF) / 3, 0, 1) * ctrl);
    }
    // gravity along slope
    tmpV.set(0, -GRAV, 0);
    // arcade cheat: only part of gravity fights you up a slope, so you keep
    // enough speed climbing a ramp face to actually launch off the lip
    tmpV2.copy(tmpV).addScaledVector(gN, -tmpV.dot(gN));
    S.vel.addScaledVector(tmpV2, dt * 0.55);

    // steering
    const steerAuth = clamp(absSpeed / 6, 0, 1) * (1 - clamp(absSpeed / (MAX_SPEED * 1.5), 0, 0.55));
    S.yaw -= steerIn * 2.2 * dt * steerAuth * Math.sign(speedF || 1) * ctrl;

    // lateral grip
    const lat = S.vel.dot(rightV);
    const gripLoss = clamp(Math.abs(lat) / 12, 0, 1);
    const grip = 1 - Math.exp(-(14 - gripLoss * 7) * dt);
    S.vel.addScaledVector(rightV, -lat * grip);
    if (Math.abs(lat) > 4 && Math.random() < 0.6) emitDust(S.pos.x, gh, S.pos.z, 1.1);

    // Rolling drag, air resistance, and the surface you're on. Loose sand off
    // the road drags badly — that's what makes cutting the corner cost you.
    S.surf = onTrack(S.pos.x, S.pos.z);
    const loose = (1 - S.surf) * 0.62;
    S.vel.multiplyScalar(Math.exp(-((gasIn > 0 ? 0.1 : 0.85) + loose) * dt));
    S.vel.addScaledVector(S.vel, -0.004 * absSpeed * dt);

    // suspension follow
    const target = gh + RIDE_H;
    S.vel.y += (target - S.pos.y) * 90 * dt;
    S.vel.y -= S.vel.y * clamp(16 * dt, 0, 1);
    S.pos.addScaledVector(S.vel, dt);

    // track how fast the ground is climbing under the wheels; one bad sample at
    // a lip barely moves it, so it still reads the ramp face at takeoff
    const nextH = terrainH(S.pos.x, S.pos.z);
    const groundRate = clamp((nextH - gh) / dt, -40, 40);
    S.climb = lerp(S.climb, groundRate, 1 - Math.exp(-11 * dt));

    /* Two ways to leave the ground:
       1. the surface drops away instantly — a ramp lip;
       2. you crest a rise carrying more upward speed than gravity can cancel
          before the ground falls away beneath you. Without (2) the bike is
          glued to the terrain and only artificial ramps ever produce air,
          which is exactly what made the open desert feel dead. */
    const carried = S.climb - GRAV * dt;
    if (S.pos.y > nextH + RIDE_H + 0.25 || carried > groundRate + 0.6) {
      S.grounded = false;
      takeoff();
    } else {
      S.pos.y = nextH + RIDE_H;
      if (S.vel.y < 0) S.vel.y = 0;
    }

    // orientation: align to slope, add lean
    S.lean = lerp(S.lean, -steerIn * clamp(absSpeed / 16, 0, 1) * 0.55, 1 - Math.exp(-8 * dt));
    fwdV.set(Math.sin(S.yaw), 0, Math.cos(S.yaw));
    tmpV.copy(fwdV).addScaledVector(gN, -fwdV.dot(gN)).normalize();
    rightV.crossVectors(gN, tmpV).normalize();
    // right-handed basis: X = up x forward, Y = up, Z = forward (the bike model
    // faces local +Z). A mirrored basis here silently yaws the whole bike.
    tmpM.makeBasis(rightV, gN, tmpV);
    tmpQ.setFromRotationMatrix(tmpM);
    leanQ.setFromAxisAngle(fwdAxis, S.lean);
    tmpQ.multiply(leanQ);
    S.quat.slerp(tmpQ, 1 - Math.exp(-11 * dt));

    S.wheelSpin += speedF / WHEEL_R * dt;

    // dust from the rear wheel
    if (absSpeed > 4 && Math.random() < clamp(absSpeed / 22, 0, 1)) {
      tmpV.set(0, 0, -0.8).applyQuaternion(S.quat);
      emitDust(S.pos.x + tmpV.x, gh, S.pos.z + tmpV.z, 0.7 + absSpeed / 30);
    }

    S.lastGoodPos.copy(S.pos);
    S.lastGoodYaw = S.yaw;
    if (S.airTime > 0.05) S.airTime = 0;
  } else {
    // -------- airborne
    S.airTime += dt;
    S.vel.y -= GRAV * dt;
    S.vel.multiplyScalar(Math.exp(-0.06 * dt));
    S.pos.addScaledVector(S.vel, dt);

    const pr = pitchIn * 2.1 * dt * ctrl;
    const yr = -steerIn * 1.5 * dt * ctrl;
    S.flipAccum += pr;
    tmpE.set(pr, yr, -steerIn * 0.8 * dt * ctrl, 'XYZ');
    tmpQ.setFromEuler(tmpE);
    S.quat.multiply(tmpQ);
    S.yaw += yr;
    S.wheelSpin += 6 * dt;

    // hands off = the bike settles back towards level, so a clean jump lands clean
    if (!pitchIn && !steerIn && S.airTime > 0.2) {
      tmpQ.setFromAxisAngle(upV, S.yaw);
      S.quat.slerp(tmpQ, 1 - Math.exp(-1.5 * dt));
    }

    if (S.pos.y < terrainH(S.pos.x, S.pos.z) + RIDE_H) {
      S.grounded = true;
      terrainNormal(S.pos.x, S.pos.z, gN);
      onLand(gN);
    }
  }

  // rim guard
  const r = Math.hypot(S.pos.x, S.pos.z);
  if (r > PLAY_R + 90) {
    S.pos.multiplyScalar((PLAY_R + 88) / r);
    S.vel.multiplyScalar(0.3);
  }
  if (S.pos.y < -60) respawn(false);
}

function onLand(normal) {
  const at = S.airTime;
  S.airTime = 0;
  S.climb = 0;
  bikeUp.set(0, 1, 0).applyQuaternion(S.quat);
  const align = bikeUp.dot(normal);
  const impact = Math.max(0, -S.vel.y);

  if (at > 0.3) {
    const flips = Math.floor(Math.abs(S.flipAccum) / (Math.PI * 2) + 0.25);
    const badLanding = align < 0.35 || (align < 0.62 && impact > 20);
    if (badLanding) {
      crash();
      return;
    }
    const pts = Math.round(Math.pow(at, 1.6) * 120 + flips * 750);
    G.combo++;
    G.score += pts * (1 + Math.min(G.combo, 5) * 0.15);
    G.totalAir += at;
    if (at > G.bigAir) G.bigAir = at;
    /* Keep scoring live underneath race mode so the stunt park can reuse it,
       but do not surface air points or trick callouts during a race. */
    if (!document.body.classList.contains('mode-race')) {
      let label = at > 2.6 ? 'MASSIVE AIR' : at > 1.6 ? 'BIG AIR' : at > 0.9 ? 'NICE AIR' : 'AIR';
      if (flips >= 1) label = flips > 1 ? flips + 'x FLIP!' : (S.flipAccum > 0 ? 'FRONT FLIP!' : 'BACKFLIP!');
      banner(label, at.toFixed(2) + 's  ·  +' + Math.round(pts * (1 + Math.min(G.combo, 5) * 0.15)) + (G.combo > 1 ? '  ·  x' + G.combo : ''));
      chime(flips ? 880 : 620);
    }
  }

  // absorb the hit, scrub speed on a sloppy landing
  const scrub = lerp(0.5, 1.0, clamp((align - 0.35) / 0.5, 0, 1));
  const horiz = tmpV.set(S.vel.x, 0, S.vel.z);
  const sp = horiz.length();
  horiz.normalize().multiplyScalar(sp * scrub);
  S.vel.set(horiz.x, Math.max(S.vel.y * -0.12, 0), horiz.z);
  // re-derive heading from where the bike actually points
  tmpV.set(0, 0, 1).applyQuaternion(S.quat);
  if (Math.abs(tmpV.x) + Math.abs(tmpV.z) > 0.05) S.yaw = Math.atan2(tmpV.x, tmpV.z);
  S.flipAccum = 0;
  if (impact > 5) thud(impact / 55);
  for (let i = 0; i < 6; i++) emitDust(S.pos.x, S.pos.y - RIDE_H, S.pos.z, 1 + impact / 20);
}

function crash() {
  S.crashed = true;
  S.crashT = 0;
  G.combo = 0;
  S.tumble.set((Math.random() - 0.5) * 9, (Math.random() - 0.5) * 6, (Math.random() - 0.5) * 9);
  S.vel.multiplyScalar(0.5);
  S.vel.y = 4;
  banner('WIPEOUT', 'shake it off', true);
  thud(0.5);
}

/* ------------------------------------------------------------ HUD & UI */
const elSpeed = $('#speedVal'), elArc = $('#speedArc'), elScore = $('#scoreVal');
const elCP = $('#cpVal'), elLap = $('#lapVal'), elTime = $('#timeVal'), elBest = $('#bestVal');
const elAir = $('#airFill'), elAirVal = $('#airVal'), elDist = $('#distVal');
const elBanner = $('#banner'), elBanTitle = $('#banTitle'), elBanSub = $('#banSub');
const elFps = $('#perf');
const ARC_LEN = 236;
let bannerT = 0;

function banner(title, sub, bad) {
  elBanTitle.textContent = title;
  elBanSub.textContent = sub || '';
  elBanner.classList.toggle('bad', !!bad);
  elBanner.classList.add('show');
  bannerT = bad ? 1.6 : 1.3;
}
function hideBanner() { elBanner.classList.remove('show'); bannerT = 0; }

function fmtTime(t) {
  const m = Math.floor(t / 60);
  const s = t - m * 60;
  return m + ':' + (s < 10 ? '0' : '') + s.toFixed(2);
}

/* minimap */
const mm = $('#minimap');
const mmx = mm.getContext('2d');
function drawMap() {
  const w = mm.width, h = mm.height, cx = w / 2, cy = h / 2;
  const scale = (w / 2 - 8) / (PLAY_R + 20);
  mmx.clearRect(0, 0, w, h);
  mmx.strokeStyle = 'rgba(255,176,32,0.22)';
  mmx.lineWidth = 1;
  mmx.beginPath();
  mmx.arc(cx, cy, PLAY_R * scale, 0, 6.283);
  mmx.stroke();
  // the circuit itself
  mmx.beginPath();
  for (let i = 0; i <= trackPts.length; i += 2) {
    const p = trackPts[i % trackPts.length];
    const px = cx + p.x * scale, py = cy + p.z * scale;
    i === 0 ? mmx.moveTo(px, py) : mmx.lineTo(px, py);
  }
  mmx.closePath();
  mmx.strokeStyle = 'rgba(233,228,218,0.55)';
  mmx.lineWidth = 3;
  mmx.stroke();
  // start / finish
  const sp = trackPts[0];
  mmx.beginPath();
  mmx.arc(cx + sp.x * scale, cy + sp.z * scale, 3.5, 0, 6.283);
  mmx.fillStyle = '#ffb020';
  mmx.fill();
  const bx = cx + S.pos.x * scale, by = cy + S.pos.z * scale;
  mmx.save();
  mmx.translate(bx, by);
  mmx.rotate(-S.yaw + Math.PI);
  mmx.beginPath();
  mmx.moveTo(0, -6); mmx.lineTo(4, 5); mmx.lineTo(0, 2.5); mmx.lineTo(-4, 5);
  mmx.closePath();
  mmx.fillStyle = '#e4453a';
  mmx.fill();
  mmx.restore();
}

/* --------------------------------------------------------------- camera */
let camMode = 0;
const CAM_NAMES = ['CHASE', 'CLOSE', 'HELI'];
function cycleCam() {
  camMode = (camMode + 1) % 3;
  $('#camName').textContent = CAM_NAMES[camMode];
}
const camTarget = new THREE.Vector3();
const camWanted = new THREE.Vector3();

function updateCamera(dt) {
  const sp = S.vel.length();
  fwdV.set(Math.sin(S.yaw), 0, Math.cos(S.yaw));
  if (camMode === 2) {
    camWanted.set(S.pos.x - fwdV.x * 6, S.pos.y + 22, S.pos.z - fwdV.z * 6);
  } else {
    const back = camMode === 0 ? 6.4 + sp * 0.1 : 4.2;
    const up = camMode === 0 ? 2.5 : 1.8;
    camWanted.set(S.pos.x - fwdV.x * back, S.pos.y + up, S.pos.z - fwdV.z * back);
    const ch = terrainH(camWanted.x, camWanted.z) + 1.4;
    if (camWanted.y < ch) camWanted.y = ch;
  }
  camera.position.lerp(camWanted, 1 - Math.exp(-(S.crashed ? 3 : 7) * dt));
  camTarget.lerp(tmpV.set(S.pos.x + fwdV.x * 4, S.pos.y + 1.2, S.pos.z + fwdV.z * 4), 1 - Math.exp(-9 * dt));
  camera.lookAt(camTarget);
  const wantFov = 60 + clamp(sp / MAX_SPEED, 0, 1) * 9;
  camera.fov += (wantFov - camera.fov) * (1 - Math.exp(-4 * dt));
  camera.updateProjectionMatrix();
}

/* ----------------------------------------------------------------- loop */
let clock = 0, last = performance.now(), lastFrameAt = performance.now();
const arrowMat = new THREE.MeshBasicMaterial({ color: 0xffb020, transparent: true, opacity: 0.6, fog: false });
const dirArrow = new THREE.Group();
{
  const head = new THREE.Mesh(new THREE.ConeGeometry(0.34, 0.9, 4), arrowMat);
  head.rotation.z = -Math.PI / 2;
  head.rotation.y = Math.PI / 4;
  head.position.x = 0.45;
  const shaft = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.16, 0.16), arrowMat);
  shaft.position.x = -0.1;
  dirArrow.add(head, shaft);
}
const arrowPivot = new THREE.Group();
arrowPivot.add(dirArrow);
arrowPivot.visible = false;
scene.add(arrowPivot);

/* A race is start line to finish line, not a scavenger hunt for gates. Laps are
   counted by crossing the start/finish plane going forwards, with the sector
   check stopping you from reversing over it to farm laps. */
const RACE_LAPS = 3;
let lastAlong = 0, sectorSeen = false;

function resetGateTracking() {
  const p = trackProfile(S.pos.x, S.pos.z);
  lastAlong = p ? p.along : 0;
  sectorSeen = false;
}

function checkCheckpoint() {
  const p = trackProfile(S.pos.x, S.pos.z);
  if (!p) return;
  const along = p.along;
  const prev = lastAlong;
  lastAlong = along;
  if (G.finished) return;

  // must have been round the far side before a crossing counts
  if (along > trackLen * 0.45 && along < trackLen * 0.75) sectorSeen = true;

  const wrapped = prev > trackLen * 0.8 && along < trackLen * 0.2;
  if (!wrapped || !sectorSeen) return;
  sectorSeen = false;

  const t = clock - G.lapStart;
  G.lapStart = clock;
  G.lapTimes.push(t);
  if (G.best === null || t < G.best) G.best = t;

  if (G.lap >= RACE_LAPS) {
    G.finished = true;
    G.raceTime = clock - G.raceStart;
    banner('FINISH', fmtTime(G.raceTime) + '  ·  best ' + fmtTime(G.best));
    chime(1320);
    setTimeout(() => chime(1760), 180);
    setTimeout(showResults, 1400);
  } else {
    G.lap++;
    banner('LAP ' + G.lap + ' / ' + RACE_LAPS, t === G.best ? 'best ' + fmtTime(t) : fmtTime(t));
    chime(1040);
  }
}

function frame(now) {
  requestAnimationFrame(frame);
  lastFrameAt = now;
  let dt = (now - last) / 1000;
  last = now;
  if (dt > 0.25) dt = 0.25;        // came back from the background; don't warp
  trackFps(dt);
  if (innerWidth !== viewW || innerHeight !== viewH) resize();
  if (!started) { titleCam(now / 1000); renderer.render(scene, camera); return; }
  if (paused || resultsUp) { renderer.render(scene, camera); return; }
  const t0 = performance.now();
  update(dt);
  renderer.render(scene, camera);
  workAcc += performance.now() - t0;
}

/* Frame rate readout. I can't profile your phone from here, so the game has to
   tell you what it's actually managing. */
let fpsAcc = 0, fpsFrames = 0, fpsShown = 0, workAcc = 0, workShown = 0;
function trackFps(dt) {
  fpsAcc += dt;
  fpsFrames++;
  if (fpsAcc >= 0.5) {
    fpsShown = Math.round(fpsFrames / fpsAcc);
    workShown = workAcc / fpsFrames;
    fpsAcc = 0;
    fpsFrames = 0;
    workAcc = 0;
    // 60fps is capped by the display, so it can't show spare capacity. The work
    // figure can: it's how much of each 16.7ms frame the game actually uses.
    elFps.textContent = fpsShown + ' FPS · ' + workShown.toFixed(1) + 'ms';
    elFps.classList.toggle('bad', fpsShown < 50 || workShown > 12);
  }
}

function titleCam(t) {
  camera.position.set(Math.sin(t * 0.12) * 20, terrainH(0, 0) + 6.5, Math.cos(t * 0.12) * 20);
  camera.lookAt(S.pos.x, S.pos.y + 1, S.pos.z);
  camera.fov = 62;
  camera.updateProjectionMatrix();
  bike.position.copy(S.pos);
  bike.position.y -= RIDE_H;
  bike.quaternion.copy(S.quat);
}

function update(dt) {
  clock += dt;

  /* Substep to keep the suspension stable, but always cover the *whole* frame.
     The old code clamped dt to 50ms, so below 20fps the bike and the race clock
     both ran slower than real time — a second on the clock took longer than a
     second. Now a slow frame costs smoothness, never speed. */
  const steps = Math.min(6, Math.max(1, Math.ceil(dt / 0.02)));
  for (let i = 0; i < steps; i++) step(dt / steps);
  if (!S.crashed) checkCheckpoint();

  // ---- transforms
  bike.position.copy(S.pos);
  bike.position.y -= RIDE_H;
  bike.quaternion.copy(S.quat);
  wheelR.rotation.x = wheelF.rotation.x = 0;
  wheelR.children[0].rotation.z = wheelF.children[0].rotation.z = 0;
  wheelR.rotation.x = -S.wheelSpin;
  wheelF.rotation.x = -S.wheelSpin;
  const sp = S.vel.length();
  rider.rotation.x = lerp(rider.rotation.x, S.grounded ? clamp(-sp / 90, -0.3, 0) : 0.22, 1 - Math.exp(-6 * dt));
  // the suspension eats the chatter, but the rider still gets shaken by it
  const shake = clamp(S.jolt / SUSP_TRAVEL, 0, 1);
  rider.position.y = 0.98 - shake * 0.055 * (0.6 + 0.4 * Math.sin(clock * 47));
  rider.position.z = lerp(rider.position.z, S.grounded ? -0.16 : -0.3, 1 - Math.exp(-5 * dt))
    + shake * 0.02 * Math.sin(clock * 39);

  // ---- dust
  for (let i = 0; i < DUST_N; i++) {
    if (dustLife[i] <= 0) continue;
    dustLife[i] -= dt;
    if (dustLife[i] <= 0) { dustPos[i * 3 + 1] = -9999; continue; }
    dustPos[i * 3] += dustVel[i * 3] * dt;
    dustPos[i * 3 + 1] += dustVel[i * 3 + 1] * dt;
    dustPos[i * 3 + 2] += dustVel[i * 3 + 2] * dt;
    dustVel[i * 3 + 1] -= 0.6 * dt;
    dustVel[i * 3] *= 0.98; dustVel[i * 3 + 2] *= 0.98;
  }
  dustGeo.attributes.position.needsUpdate = true;

  // ---- how far round the lap you are (the track itself is the guidance now)
  const prof = trackProfile(S.pos.x, S.pos.z);
  const lapPct = prof ? Math.round((prof.along / trackLen) * 100) : 0;
  arrowPivot.visible = false;

  // ---- shadow follows the bike
  sun.position.copy(S.pos).addScaledVector(sunDir, 60);
  sun.target.position.copy(S.pos);
  sun.target.updateMatrixWorld();

  updateCamera(dt);

  // ---- audio
  if (actx && audioOn) {
    const rpm = clamp(sp / MAX_SPEED, 0, 1);
    const gas = (keys.ArrowUp || keys.KeyW || touch.gas) ? 1 : 0;
    const f = 58 + rpm * 190 + (S.grounded ? 0 : 20) + gas * 26;
    engOsc.frequency.setTargetAtTime(f, actx.currentTime, 0.06);
    engOsc.subOsc.frequency.setTargetAtTime(f / 2, actx.currentTime, 0.06);
    engFilt.frequency.setTargetAtTime(500 + rpm * 1600, actx.currentTime, 0.1);
    engGain.gain.setTargetAtTime(S.crashed ? 0.02 : 0.05 + rpm * 0.09 + gas * 0.03, actx.currentTime, 0.08);
    windGain.gain.setTargetAtTime(rpm * rpm * 0.05, actx.currentTime, 0.15);
  }

  // ---- HUD
  const mph = sp * 2.2369;
  elSpeed.textContent = Math.round(mph);
  elArc.style.strokeDashoffset = (ARC_LEN * (1 - clamp(mph / 110, 0, 1))).toFixed(1);
  elScore.textContent = Math.round(G.score).toLocaleString('en-US');
  elCP.textContent = G.finished ? 'DONE' : lapPct + '%';
  elLap.textContent = G.lap + '/' + RACE_LAPS;
  elTime.textContent = G.finished ? fmtTime(G.raceTime) : fmtTime(clock - G.lapStart);
  elBest.textContent = G.best === null ? '—:——' : fmtTime(G.best);
  elDist.textContent = S.surf > 0.5 ? 'ON' : 'OFF';
  const airPct = clamp(S.airTime / 3, 0, 1) * 100;
  elAir.style.height = airPct + '%';
  elAirVal.textContent = (S.grounded ? 0 : S.airTime).toFixed(2);
  document.body.classList.toggle('airborne', !S.grounded && S.airTime > 0.4);

  if (bannerT > 0) {
    bannerT -= dt;
    if (bannerT <= 0) hideBanner();
  }
  drawMap();
}

/* ------------------------------------------------------------ lifecycle */
let viewW = 0, viewH = 0;
function resize() {
  const w = viewW = innerWidth, h = viewH = innerHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  const mmSize = 132;
  mm.width = mm.height = mmSize * Math.min(devicePixelRatio, 2);
  mm.style.width = mm.style.height = mmSize + 'px';
  mmx.setTransform(Math.min(devicePixelRatio, 2), 0, 0, Math.min(devicePixelRatio, 2), 0, 0);
}
addEventListener('resize', resize);
resize();

let resultsUp = false;

function showResults() {
  resultsUp = true;
  killAudio();
  $('#rTotal').textContent = fmtTime(G.raceTime);
  $('#rBest').textContent = G.best === null ? '—:——' : fmtTime(G.best);
  const rows = G.lapTimes.map((t, i) =>
    '<tr class="' + (t === G.best ? 'best' : '') + '"><td>Lap ' + (i + 1) + '</td><td>' + fmtTime(t) + '</td></tr>'
  ).join('');
  $('#lapRows').innerHTML = rows;
  $('#results').classList.add('show');
  hideBanner();
}
function hideResults() {
  resultsUp = false;
  $('#results').classList.remove('show');
  if (audioOn) wakeAudio();
  last = performance.now();
}

function setPaused(p) {
  paused = p;
  $('#pause').classList.toggle('show', p);
  if (p) killAudio(); else if (audioOn) wakeAudio();
  last = performance.now();
}
function togglePause() { if (started) setPaused(!paused); }

function startGame() {
  if (started) return;
  started = true;
  initAudio();
  if (audioOn) wakeAudio();
  $('#title').classList.add('gone');
  document.body.classList.add('playing');
  resetRun();
  last = performance.now();
}

function quitToTitle() {
  started = false;
  resultsUp = false;
  $('#results').classList.remove('show');
  setPaused(false);
  killAudio();
  hideBanner();
  $('#pause').classList.remove('show');
  $('#title').classList.remove('gone');
  document.body.classList.remove('playing', 'airborne');
  resetRun();
  S.pos.set(0, terrainH(0, 0) + RIDE_H, 0);
  S.vel.set(0, 0, 0);
  last = performance.now();
}

$('#startBtn').addEventListener('click', startGame);
$('#menuBtn').addEventListener('click', () => { if (started && !resultsUp) setPaused(true); });
$('#againBtn').addEventListener('click', () => { hideResults(); resetRun(); });
$('#resultsQuit').addEventListener('click', () => { hideResults(); quitToTitle(); });
$('#resumeBtn').addEventListener('click', () => setPaused(false));
$('#restartBtn').addEventListener('click', () => { G.best = null; resetRun(); setPaused(false); });
$('#quitBtn').addEventListener('click', quitToTitle);
for (const id of ['#throttleBtn', '#throttleBtn2']) {
  $(id).addEventListener('click', () => setThrottleMode(!autoThrottle));
}
setThrottleMode(autoThrottle);
$('#sndBtn').addEventListener('click', () => { initAudio(); toggleAudio(); if (audioOn) wakeAudio(); });

// the title screen idles on the same loop as the game
resetRun();
camTarget.copy(S.pos);
$('#buildStamp').textContent = BUILD;
requestAnimationFrame(frame);

$('#loading').classList.add('gone');

// diagnostics hook (used to drive the sim in tests)
window.__dbg = {
  S, G, keys, step, update, resize, startGame, respawn, terrainH, terrainNormal,
  checkpoints, ramps, renderer, scene, camera,
  get trackPts() { return trackPts; }, trackProfile, onTrack, features,
  get trackLen() { return trackLen; },
  get clock() { return clock; }, get fps() { return fpsShown; }, MOBILE, SEG,
  quitToTitle, setPaused, killAudio, titleCam, checkCheckpoint, resetGateTracking,
  showResults, hideResults,
  touch, setTouchMode, setThrottleMode,
  get started() { return started; },
  get paused() { return paused; },
  get audio() { return { actx, engGain, windGain, audioOn }; }
};
})();
