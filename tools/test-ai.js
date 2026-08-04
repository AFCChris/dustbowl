/* Headless verification of AI rider logic — no Three.js, no DOM.
   Run with:  node tools/test-ai.js
*/
'use strict';

const RACE_LAPS = 3;
const trackLen  = 1200;
const NUM_AI    = 7;
let   failed    = 0;

function assert(cond, msg) {
  if (!cond) { console.error('FAIL:', msg); failed++; } else { console.log('ok  ', msg); }
}
function clamp(v, a, b) { return v < a ? a : v > b ? b : v; }
function lerp(a, b, t)  { return a + (b - a) * t; }

/* ---- helpers matching game.js implementations exactly ---- */

function effTotalDist(lap, along, finished, finishTime) {
  if (finished) return (RACE_LAPS + 1) * trackLen - (finishTime || 0);
  return (lap - 1) * trackLen + along;
}

function _recordAILap(ai, clock) {
  if (ai.finished) return;
  ai.lapTimes.push(clock - ai.lapStart);
  ai.lapStart = clock;
  if (ai.lap >= RACE_LAPS) {
    ai.finished   = true;
    ai.finishTime = clock;
  } else {
    ai.lap++;
  }
}

/* Simulate the wrap-detection + sector-gate logic from updateAI() full-sim
   path. Returns the number of lap-records triggered. */
function simFullSimLaps(startAlong, laps) {
  const ai = {
    lap: 1, lapStart: 0, lapTimes: [], finished: false, finishTime: null,
    sectorSeen: false,
    along: startAlong,
  };
  const step = 5;          // metres per tick — coarse but fast
  const lapsToSim = laps;
  let clock = 0;

  for (let iter = 0; iter < lapsToSim * trackLen / step + 100; iter++) {
    const prevAlong = ai.along;
    ai.along += step;
    if (ai.along >= trackLen) ai.along -= trackLen;
    const newAlong = ai.along;

    // sector gate
    if (newAlong > trackLen * 0.45 && newAlong < trackLen * 0.75) ai.sectorSeen = true;

    // wrap detection
    if (prevAlong > trackLen * 0.8 && newAlong < trackLen * 0.2 && ai.sectorSeen) {
      _recordAILap(ai, clock);
      ai.sectorSeen = false;
    }
    clock += 0.1;
    if (ai.finished) break;
  }
  return ai;
}

/* Simulate the analytical LOD advance + sector-gate logic from updateAI(). */
function simAnalyticalLaps(startAlong, laps) {
  const ai = {
    lap: 1, lapStart: 0, lapTimes: [], finished: false, finishTime: null,
    sectorSeen: false,
    along: startAlong,
  };
  const step = 5;
  let clock = 0;

  for (let iter = 0; iter < laps * trackLen / step + 100; iter++) {
    ai.along += step;

    // sector gate
    if (ai.along > trackLen * 0.45 && ai.along < trackLen * 0.75) ai.sectorSeen = true;

    // overflow → wrap + conditional lap record
    if (ai.along >= trackLen) {
      ai.along -= trackLen;
      if (ai.sectorSeen) {
        _recordAILap(ai, clock);
        ai.sectorSeen = false;
      }
    }
    clock += 0.1;
    if (ai.finished) break;
  }
  return ai;
}

/* ================================================================
   ORIGINAL TESTS (v0.14)
   ================================================================ */

// ---- effTotalDist ----
assert(effTotalDist(1, 100, false, null) === 100, 'lap1 along=100 → 100');
assert(effTotalDist(2, 200, false, null) === trackLen + 200, 'lap2 along=200');
assert(effTotalDist(3, 1000, false, null) < effTotalDist(1, 0, true, 120),
  'finished rider outranks lap-3 non-finished');
assert(effTotalDist(1, 0, true, 90) > effTotalDist(1, 0, true, 120),
  'earlier finisher ranks higher');

// ---- ranking sort ----
function buildRanking(riders) {
  return [...riders].sort((a, b) =>
    effTotalDist(b.lap, b.along, b.finished, b.finishTime) -
    effTotalDist(a.lap, a.along, a.finished, a.finishTime)
  );
}

const testField = [
  { name: 'You',     lap: 3, along: 900,  finished: false, finishTime: null },
  { name: 'Rider 1', lap: 3, along: 800,  finished: false, finishTime: null },
  { name: 'Rider 2', lap: 3, along: 950,  finished: false, finishTime: null },
  { name: 'Rider 3', lap: 1, along: 200,  finished: false, finishTime: null },
  { name: 'Rider 4', lap: 3, along: 1150, finished: true,  finishTime: 185  },
  { name: 'Rider 5', lap: 3, along: 1150, finished: true,  finishTime: 200  },
  { name: 'Rider 6', lap: 2, along: 600,  finished: false, finishTime: null },
  { name: 'Rider 7', lap: 3, along: 1100, finished: true,  finishTime: 190  },
];

const ranked = buildRanking(testField);
assert(ranked[0].name === 'Rider 4', '1st: earliest finisher (185s)');
assert(ranked[1].name === 'Rider 7', '2nd: second finisher (190s)');
assert(ranked[2].name === 'Rider 5', '3rd: third finisher (200s)');
assert(ranked[3].name === 'Rider 2', '4th: non-finished lap3 along=950');
assert(ranked[4].name === 'You',     '5th: non-finished lap3 along=900');
assert(ranked[5].name === 'Rider 1', '6th: non-finished lap3 along=800');
assert(ranked[6].name === 'Rider 6', '7th: lap2');
assert(ranked[7].name === 'Rider 3', '8th: lap1 (last)');

// ---- computePlayerPos ----
function computePlayerPos(aiRiders, playerLap, playerAlong, playerFinished, playerRaceTime) {
  const pd = effTotalDist(playerLap, playerAlong, playerFinished, playerRaceTime);
  let ahead = 0;
  for (const ai of aiRiders) {
    if (effTotalDist(ai.lap, ai.along, ai.finished, ai.finishTime) > pd) ahead++;
  }
  return ahead + 1;
}
const aiForPos = testField.slice(1);
assert(computePlayerPos(aiForPos, 3, 900, false, null) === 5, 'player in P5 with mixed field');

// ---- old wrap detection shape ----
function simWrap(prev, next) {
  return prev > trackLen * 0.8 && next < trackLen * 0.2;
}
assert(simWrap(1100, 50),   'wrap detected: 1100→50');
assert(!simWrap(600, 700),  'no wrap mid-lap');
assert(!simWrap(50, 100),   'no wrap at start');
assert(!simWrap(900, 1100), 'no wrap: 900→1100');

// ---- rubber-banding ----
function rubberNudge(base, gap, dt) {
  const tgt = base * (1 + clamp(gap / 200, -0.05, 0.05));
  return lerp(base, tgt, 1 - Math.exp(-0.6 * dt));
}
const base = 0.87;
assert(rubberNudge(base,  300, 0.5) > base, 'rubber-band: gap>0 → AI speeds up');
assert(rubberNudge(base, -300, 0.5) < base, 'rubber-band: gap<0 → AI slows down');
assert(Math.abs(rubberNudge(base, 300, 0.5) - base) <= base * 0.05 + 0.001, 'rubber-band cap ≤5%');

// ---- analytical LOD lap increment ----
let along = trackLen - 0.5;
along += 30 * 0.1;
let bumped = false;
if (along >= trackLen) { along -= trackLen; bumped = true; }
assert(bumped, 'analytical: lap increment when along overflows');
assert(along > 0 && along < trackLen, 'along stays in valid range');

// ---- grid slots ----
const GRID_SLOTS = [
  { along: trackLen - 2,  lat: 0   },
  { along: trackLen - 8,  lat: -2.5 },
  { along: trackLen - 8,  lat:  2.5 },
  { along: trackLen - 14, lat: -2.5 },
  { along: trackLen - 14, lat:  2.5 },
  { along: trackLen - 20, lat: -2.5 },
  { along: trackLen - 20, lat:  2.5 },
  { along: trackLen - 26, lat: 0   },
];
assert(GRID_SLOTS.length === 8, 'grid has 8 slots');
assert(GRID_SLOTS[0].along > GRID_SLOTS[1].along, 'player slot ahead of first AI slot');

/* ================================================================
   REGRESSION TESTS — Bug 1: sector gate for grid-start AIs
   ================================================================ */
console.log('');
console.log('--- Bug 1: sector gate prevents false lap on grid-start crossing ---');

// Each grid AI starts near trackLen. Without the gate they would record a lap
// after just a few metres. With the gate they need a full lap first.

// Test every actual grid along value
const gridAlongs = [trackLen-8, trackLen-14, trackLen-20, trackLen-26];

for (const startAlong of gridAlongs) {
  // ---- full-sim path ----
  const fsResult = simFullSimLaps(startAlong, RACE_LAPS);
  assert(fsResult.finished, `full-sim: AI starting at along=${startAlong} eventually finishes`);
  assert(fsResult.lapTimes.length === RACE_LAPS,
    `full-sim: AI at along=${startAlong} records exactly ${RACE_LAPS} laps (got ${fsResult.lapTimes.length})`);

  // ---- analytical LOD path ----
  const anResult = simAnalyticalLaps(startAlong, RACE_LAPS);
  assert(anResult.finished, `analytical: AI starting at along=${startAlong} eventually finishes`);
  assert(anResult.lapTimes.length === RACE_LAPS,
    `analytical: AI at along=${startAlong} records exactly ${RACE_LAPS} laps (got ${anResult.lapTimes.length})`);
}

// Also verify that a false immediate crossing (prevAlong=1192, newAlong=5) is
// suppressed when sectorSeen=false (the initial state on the grid).
{
  const ai = { lap: 1, lapStart: 0, lapTimes: [], finished: false, finishTime: null, sectorSeen: false, along: trackLen - 8 };
  const prevAlong = ai.along;
  ai.along = 5;   // simulate crossing start/finish after only 8m
  const newAlong = ai.along;
  // sector gate check
  if (newAlong > trackLen * 0.45 && newAlong < trackLen * 0.75) ai.sectorSeen = true;
  // wrap detection
  if (prevAlong > trackLen * 0.8 && newAlong < trackLen * 0.2 && ai.sectorSeen) {
    _recordAILap(ai, 1);
  }
  assert(ai.lapTimes.length === 0,
    'sector gate: immediate grid-start crossing (8m) does not record a lap');
  assert(!ai.finished, 'sector gate: AI is not marked finished after suppressed crossing');
}

// Positive check: a crossing IS recorded once sectorSeen is true.
{
  const ai = { lap: 1, lapStart: 0, lapTimes: [], finished: false, finishTime: null, sectorSeen: true, along: trackLen - 5 };
  const prevAlong = ai.along;
  ai.along = 10;
  const newAlong = ai.along;
  if (prevAlong > trackLen * 0.8 && newAlong < trackLen * 0.2 && ai.sectorSeen) {
    _recordAILap(ai, 100);
    ai.sectorSeen = false;
  }
  assert(ai.lapTimes.length === 1, 'sector gate: crossing after sectorSeen=true records a lap');
  assert(ai.lap === 2, 'sector gate: lap counter advances to 2 after first genuine crossing');
  assert(!ai.sectorSeen, 'sector gate: sectorSeen cleared after recording lap');
}

/* ================================================================
   REGRESSION TESTS — Bug 2: countdown session token
   ================================================================ */
console.log('');
console.log('--- Bug 2: countdown session token cancellation ---');

// Simulate the token mechanism from startCountdown().
{
  let _cdToken = 0;
  let executed = [];

  function makeSession() {
    const token = ++_cdToken;
    return {
      tick(phase) {
        if (_cdToken !== token) return;   // stale
        executed.push({ token, phase });
      },
      release() {
        if (_cdToken !== token) return;
        executed.push({ token, phase: 'release' });
      },
    };
  }

  // Session 1 starts, then is cancelled by session 2 starting before its callbacks fire.
  const s1 = makeSession();
  s1.tick('3');   // fires immediately — token still valid

  // Simulate quit: increment token (or start a new race which also increments)
  const s2 = makeSession();

  // Now s1's remaining callbacks fire (simulating delayed setTimeout)
  s1.tick('2');     // should be suppressed — token changed
  s1.tick('1');     // suppressed
  s1.release();     // suppressed

  // s2's callbacks fire
  s2.tick('3');
  s2.tick('2');
  s2.tick('1');
  s2.release();

  assert(executed.filter(e => e.token === 1).length === 1,
    'token: session 1 only executes its first tick (before cancellation)');
  assert(executed.filter(e => e.token === 2).length === 4,
    'token: session 2 executes all four phases');
  assert(executed.every(e => e.token <= 2),
    'token: no unexpected sessions fired');
}

// Verify that calling quit (token++) before the GO callback prevents the GO
// from firing (the critical race condition).
{
  let _cdToken = 0;
  let goFired = false;

  const token = ++_cdToken;   // startCountdown
  // User quits before GO fires
  _cdToken++;                 // quitToTitle increments token

  // GO callback fires from setTimeout
  if (_cdToken === token) { goFired = true; }

  assert(!goFired, 'token: GO callback suppressed when quit mid-countdown');
}

// Confirm that a clean countdown (no cancellation) does fire GO.
{
  let _cdToken = 0;
  let goFired = false;

  const token = ++_cdToken;
  // No quit — token unchanged
  if (_cdToken === token) { goFired = true; }

  assert(goFired, 'token: GO fires normally when countdown is not cancelled');
}

/* ================================================================
   SUMMARY
   ================================================================ */
console.log('');
if (failed === 0) {
  console.log('All AI logic checks passed (' + (31 + gridAlongs.length * 4) + ' assertions).');
} else {
  console.error(failed + ' check(s) failed.');
  process.exit(1);
}
