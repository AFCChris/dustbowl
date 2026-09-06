#!/usr/bin/env node
'use strict';
/*
 * Generates the soft dust puff sprite sheet used by the National bikes' roost
 * particle systems (NationalDust material). Four 128x128 puffs are stacked in
 * one column; the particle systems pick a random row per particle so the trail
 * does not read as repeated copies of one blob.
 *
 * Each puff is a radial density falloff whose silhouette is broken up by value
 * noise, with a mild top-lit shade baked into RGB so the unlit particle shader
 * still reads as a soft volume. Alpha is straight (not premultiplied).
 *
 * Usage: node unity/Tools/generate-dust-sprite.js
 * Output: unity/Assets/Dustbowl/Art/Textures/DustbowlFlats_DustPuff_Sheet.png
 * (the .meta beside it is hand-maintained; no npm dependencies).
 */
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const OUT = path.join(__dirname, '..', 'Assets', 'Dustbowl', 'Art', 'Textures', 'DustbowlFlats_DustPuff_Sheet.png');
const TILE = 128;
const ROWS = 4;

const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
function smoothstep(e0, e1, x) {
  const t = clamp((x - e0) / (e1 - e0), 0, 1);
  return t * t * (3 - 2 * t);
}

function hash2(i, j, seed) {
  let n = (i * 374761393 + j * 668265263 + seed) | 0;
  n = (n ^ (n >> 13)) | 0;
  n = Math.imul(n, 1274126177) | 0;
  return ((n ^ (n >> 16)) >>> 0) / 4294967295;
}
function vnoise(x, y, seed) {
  const i = Math.floor(x), j = Math.floor(y);
  const fx = x - i, fy = y - j;
  const u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
  const a = hash2(i, j, seed), b = hash2(i + 1, j, seed);
  const c = hash2(i, j + 1, seed), d = hash2(i + 1, j + 1, seed);
  return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
}
function fbm(x, y, oct, seed) {
  let sum = 0, amp = 0.5, norm = 0, f = 1;
  for (let o = 0; o < oct; o++) {
    sum += vnoise(x * f, y * f, seed + o * 7919) * amp;
    norm += amp;
    amp *= 0.5;
    f *= 2.03;
  }
  return sum / norm;
}

/* Returns [r, g, b, a] in 0..1 for one puff pixel. */
function puff(u, v, seed) {
  const dx = u - 0.5, dy = v - 0.5;
  const r = Math.sqrt(dx * dx + dy * dy) * 2;             // 0 centre .. 1 tile edge
  const angle = Math.atan2(dy, dx);
  // Low-frequency silhouette wobble plus finer internal billows.
  const wobble = (fbm(Math.cos(angle) * 1.6 + 3, Math.sin(angle) * 1.6 + 3, 2, seed) - 0.5)
    + (fbm(Math.cos(angle) * 3.1 + 9, Math.sin(angle) * 3.1 + 9, 1, seed + 53) - 0.5) * 0.35;
  const billow = fbm(u * 4.5, v * 4.5, 3, seed + 101);
  const radius = 0.84 + wobble * 0.42;                    // irregular outer edge
  let density = smoothstep(radius, radius * 0.42, r);     // broad soft core, feathered rim
  density *= 0.55 + 0.45 * Math.pow(billow, 1.3);         // cloudy interior
  density *= 1 - smoothstep(0.9, 0.99, r);                // never touch the tile edge
  const alpha = clamp(density * 1.15, 0, 1);

  // Soft top light: brighter towards the upper-left, darker underneath.
  const light = (-dy * 0.8 - dx * 0.35) + (billow - 0.5) * 0.35;
  const shade = 0.80 + 0.20 * smoothstep(-0.6, 0.6, light);
  return [shade, shade * 0.985, shade * 0.96, alpha];
}

function makeSheet() {
  const width = TILE, height = TILE * ROWS;
  const data = Buffer.alloc(width * height * 4);
  for (let row = 0; row < ROWS; row++) {
    for (let y = 0; y < TILE; y++) {
      for (let x = 0; x < TILE; x++) {
        const [r, g, b, a] = puff((x + 0.5) / TILE, (y + 0.5) / TILE, 1000 + row * 37);
        const o = ((row * TILE + y) * width + x) * 4;
        data[o] = Math.round(clamp(r, 0, 1) * 255);
        data[o + 1] = Math.round(clamp(g, 0, 1) * 255);
        data[o + 2] = Math.round(clamp(b, 0, 1) * 255);
        data[o + 3] = Math.round(clamp(a, 0, 1) * 255);
      }
    }
  }
  return { width, height, data };
}

/* ------------------------------------------------------------ PNG encoder */
const CRC_TABLE = new Int32Array(256).map((_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c;
});
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
  header[9] = 6;   // colour type RGBA
  const stride = width * 4;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) {
    raw[y * (stride + 1)] = 0;
    data.copy(raw, y * (stride + 1) + 1, y * stride, (y + 1) * stride);
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, encodePng(makeSheet()));
console.log(`wrote ${path.relative(process.cwd(), OUT)} (${TILE}x${TILE * ROWS}, ${ROWS} puffs)`);
