#!/usr/bin/env node
'use strict';
/*
 * Generates the placeholder Dustbowl Flats terrain albedo textures used by the
 * Unity National scene. The colour rules are a 2-D port of the web build's
 * groundColor() (src/game.js): the same sand/ochre/dirt/packed-clay palette,
 * the same packed-road and rut profile across the track, and the same mottled
 * shading. Slope and height terms are approximated because a texture has no
 * access to the terrain normal.
 *
 * Usage: node unity/Tools/generate-terrain-textures.js
 * Output: unity/Assets/Dustbowl/Art/Textures/*.png (PNG encoder is built in; no
 * npm dependencies). The .meta files beside the PNGs are hand-maintained.
 */
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const OUT_DIR = path.join(__dirname, '..', 'Assets', 'Dustbowl', 'Art', 'Textures');

/* ---------------------------------------------------------------- palette */
const hex = (v) => [((v >> 16) & 255) / 255, ((v >> 8) & 255) / 255, (v & 255) / 255];
const SAND = hex(0xe3be86);
const OCHRE = hex(0xb4783c);
const DIRT = hex(0x6d4a2c);
const SCRUB = hex(0x8b8a4e);
const PACKED = hex(0x8a4520);

/* Course profile constants shared with DustbowlFlatsReferenceSegment.cs */
const TRACK_HALF = 8.5;
const BERM_W = 7;
const STRIP_HALF = 32;          // CourseSurfaceMeshBuilder.LateralHalfExtent
const RIBBON_HALF = 8.35;       // PlayableParitySetup.BuildRacingLineMesh

/* ------------------------------------------------------------------ maths */
const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
function smoothstep(e0, e1, x) {
  const t = clamp((x - e0) / (e1 - e0), 0, 1);
  return t * t * (3 - 2 * t);
}
const lerp = (a, b, t) => a + (b - a) * t;
const lerp3 = (a, b, t) => [lerp(a[0], b[0], t), lerp(a[1], b[1], t), lerp(a[2], b[2], t)];
const mul3 = (a, s) => [a[0] * s, a[1] * s, a[2] * s];

function hash2(i, j, seed) {
  let n = (i * 374761393 + j * 668265263 + seed) | 0;
  n = (n ^ (n >> 13)) | 0;
  n = Math.imul(n, 1274126177) | 0;
  return ((n ^ (n >> 16)) >>> 0) / 4294967295;
}
/* Value noise on a lattice that wraps every px/py cells so the result tiles. */
function vnoise(x, y, px, py, seed) {
  const i = Math.floor(x), j = Math.floor(y);
  const fx = x - i, fy = y - j;
  const u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  const w = (a, m) => ((a % m) + m) % m;
  const a = hash2(w(i, px), w(j, py), seed), b = hash2(w(i + 1, px), w(j, py), seed);
  const c = hash2(w(i, px), w(j + 1, py), seed), d = hash2(w(i + 1, px), w(j + 1, py), seed);
  return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
}
/* u, v in [0,1) across one tile; cellsU/cellsV = lattice cells per tile at octave 0. */
function fbm(u, v, cellsU, cellsV, oct, seed) {
  let sum = 0, amp = 0.5, norm = 0, cu = cellsU, cv = cellsV;
  for (let o = 0; o < oct; o++) {
    sum += vnoise(u * cu, v * cv, cu, cv, seed + o * 7919) * amp;
    norm += amp;
    amp *= 0.5;
    cu *= 2; cv *= 2;
  }
  return sum / norm;
}

/* ------------------------------------------------------------ colour rules */
/* Natural desert ground: web groundColor() without the road term. `slope`
   approximates 1 - normal.y for the berm faces. */
function groundColor(mott, band, fine, grit, slope) {
  let out = lerp3(SAND, OCHRE, clamp(mott * 1.5 - 0.15, 0, 1) * 0.72 + band * 0.25);
  const veg = smoothstep(0.34, 0.08, slope) * smoothstep(0.5, 0.18, mott);
  out = lerp3(out, SCRUB, veg * 0.12);
  out = lerp3(out, DIRT, smoothstep(0.3, 0.7, slope) * 0.85);
  // web shade term, lifted slightly so sunlit sand stays bright and readable
  let shade = (0.86 + mott * 0.30) * (0.90 + fine * 0.20);
  // sparse darker grit so the ground reads as dirt rather than fog
  shade *= 1 - smoothstep(0.62, 0.78, grit) * 0.22;
  return mul3(out, shade);
}
/* Web road term: packed clay across the racing surface with darker ruts either
   side of the centre line, fading up the berm face. */
function applyRoad(color, d, streak, grit) {
  const road = 1 - smoothstep(TRACK_HALF * 0.9, TRACK_HALF + BERM_W * 0.55, d);
  let out = lerp3(color, PACKED, road * 0.88);
  const rut = Math.exp(-Math.pow((d - TRACK_HALF * 0.45) / (TRACK_HALF * 0.22), 2));
  out = mul3(out, 1 - road * rut * 0.14);
  // tyre lines along the racing direction and loose grit keep the clay reading as dirt
  out = mul3(out, 1 - road * (streak - 0.5) * 0.22);
  out = mul3(out, 1 - road * smoothstep(0.58, 0.8, grit) * 0.16);
  // the centre of the line is polished lighter by traffic
  out = mul3(out, 1 + road * Math.exp(-Math.pow(d / (TRACK_HALF * 0.3), 2)) * 0.06);
  return out;
}
/* Approximate berm slope (1 - normal.y) from the shared course profile. */
function bermSlope(d) {
  if (d <= TRACK_HALF || d >= TRACK_HALF + BERM_W) return 0.04;
  const t = (d - TRACK_HALF) / BERM_W;
  const grade = 6 * t * (1 - t) * (4.2 / BERM_W);   // derivative of smoothstep * height/width
  return 1 - 1 / Math.sqrt(1 + grade * grade);
}

/* ------------------------------------------------------------------ images */
function makeImage(width, height, shade) {
  const data = Buffer.alloc(width * height * 3);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const c = shade((x + 0.5) / width, (y + 0.5) / height);
      const o = (y * width + x) * 3;
      data[o] = Math.round(clamp(c[0], 0, 1) * 255);
      data[o + 1] = Math.round(clamp(c[1], 0, 1) * 255);
      data[o + 2] = Math.round(clamp(c[2], 0, 1) * 255);
    }
  }
  return { width, height, data };
}

/* Broad desert: tiles every 50 m (desert mesh UV = world / 900 * 18). */
function desert(u, v) {
  const mott = fbm(u, v, 4, 4, 6, 11);
  const band = fbm(u, v, 2, 2, 3, 23);
  const fine = fbm(u, v, 64, 64, 3, 37);
  const grit = fbm(u, v, 128, 128, 2, 61);
  const slope = 0.02 + fbm(u, v, 8, 8, 2, 41) * 0.16;
  return groundColor(mott, band, fine, grit, slope);
}

/* Shared strip/ribbon shading keyed on the signed lateral metre offset so the
   two meshes match exactly where the ribbon edge (8.35 m) sits on the strip. */
function courseGround(lateral, v) {
  const d = Math.abs(lateral);
  const su = ((lateral + STRIP_HALF) / (STRIP_HALF * 2)) * 2.667;   // desert-equivalent density
  const mott = fbm(su, v, 4, 2, 6, 11);
  const band = fbm(su, v, 2, 1, 3, 23);
  const fine = fbm(su, v, 64, 32, 3, 37);
  const grit = fbm(su, v, 128, 64, 2, 61);
  const streak = fbm(su, v, 48, 1, 3, 53);
  const color = groundColor(mott, band, fine, grit, bermSlope(d));
  return applyRoad(color, d, streak, grit);
}

/* Authoritative 64 m course strip: u = lateral 0..1 (-32..32 m), v = one along tile (~24 m). */
function shoulder(u, v) {
  return courseGround((u - 0.5) * STRIP_HALF * 2, v);
}

/* Packed racing ribbon: u = lateral 0..1 (-8.35..8.35 m), v = one along tile (~24 m). */
function ribbon(u, v) {
  return courseGround((u - 0.5) * RIBBON_HALF * 2, v);
}

/* ------------------------------------------------------------- PNG writer */
const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();
function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC_TABLE[(c ^ buf[i]) & 255] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function chunk(type, payload) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(payload.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), payload]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}
function encodePng({ width, height, data }) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header[8] = 8;   // bit depth
  header[9] = 2;   // truecolour
  header[10] = 0; header[11] = 0; header[12] = 0;
  const stride = width * 3;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0;
    data.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride);
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0))
  ]);
}

/* ------------------------------------------------------------------- main */
fs.mkdirSync(OUT_DIR, { recursive: true });
const outputs = [
  ['DustbowlFlats_DesertSand_Albedo.png', makeImage(512, 512, desert)],
  ['DustbowlFlats_CourseShoulder_Albedo.png', makeImage(256, 512, shoulder)],
  ['DustbowlFlats_PackedDirt_Albedo.png', makeImage(256, 512, ribbon)]
];
for (const [name, image] of outputs) {
  const file = path.join(OUT_DIR, name);
  fs.writeFileSync(file, encodePng(image));
  console.log(`${name}  ${image.width}x${image.height}`);
}
