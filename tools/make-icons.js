/* Generates the home-screen icons as real PNGs (no image libraries available,
   so this hand-rolls a minimal RGB PNG: IHDR + deflated IDAT + IEND). */
const fs = require('fs');
const zlib = require('zlib');

const crcTable = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();
function crc32(buf) {
  let c = -1;
  for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
}
function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(td));
  return Buffer.concat([len, td, crc]);
}
function png(w, h, rgb) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8;    // bit depth
  ihdr[9] = 2;    // colour type: truecolour
  const raw = Buffer.alloc(h * (w * 3 + 1));
  for (let y = 0; y < h; y++) {
    raw[y * (w * 3 + 1)] = 0;   // filter: none
    rgb.copy(raw, y * (w * 3 + 1) + 1, y * w * 3, (y + 1) * w * 3);
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0))
  ]);
}

const hex = (s) => [parseInt(s.slice(1, 3), 16), parseInt(s.slice(3, 5), 16), parseInt(s.slice(5, 7), 16)];
const mix = (a, b, t) => a.map((v, i) => Math.round(v + (b[i] - v) * Math.max(0, Math.min(1, t))));

const SKY = hex('#1d3a63'), ROSE = hex('#c07a72'), HAZE = hex('#f3a45c');
const SAND = hex('#e3be86'), OCHRE = hex('#a9682e'), DIRT = hex('#4a3320'), AMBER = hex('#ffb020');

function draw(N) {
  const buf = Buffer.alloc(N * N * 3);
  const put = (x, y, c) => {
    if (x < 0 || y < 0 || x >= N || y >= N) return;
    const i = (y * N + x) * 3;
    buf[i] = c[0]; buf[i + 1] = c[1]; buf[i + 2] = c[2];
  };
  const disc = (cx, cy, r, c) => {
    for (let y = Math.floor(cy - r); y <= cy + r; y++)
      for (let x = Math.floor(cx - r); x <= cx + r; x++)
        if ((x - cx) ** 2 + (y - cy) ** 2 <= r * r) put(x, y, c);
  };

  const horizon = 0.60 * N;
  for (let y = 0; y < N; y++) {
    let c;
    if (y < horizon * 0.72) c = mix(SKY, ROSE, y / (horizon * 0.72));
    else if (y < horizon) c = mix(ROSE, HAZE, (y - horizon * 0.72) / (horizon * 0.28));
    else c = mix(SAND, OCHRE, (y - horizon) / (N - horizon));
    for (let x = 0; x < N; x++) put(x, y, c);
  }

  // the ramp, lower left
  const rx0 = 0.10 * N, rx1 = 0.44 * N, ry0 = 0.88 * N, ry1 = 0.60 * N;
  for (let x = Math.floor(rx0); x <= rx1; x++) {
    const t = (x - rx0) / (rx1 - rx0);
    const top = ry0 + (ry1 - ry0) * Math.pow(t, 1.5);
    for (let y = Math.floor(top); y < 0.92 * N; y++) put(x, y, DIRT);
  }

  // the flight path off the lip — a flat, fast arc rather than a tent
  const ax = rx1, ay = ry1, bx = 0.97 * N, by = 0.78 * N, apex = 0.34 * N;
  const arcAt = (s) => [
    ax + (bx - ax) * s,
    ay + (by - ay) * s - Math.sin(s * Math.PI) * (ay - apex)
  ];
  const thick = Math.max(1.6, N * 0.02);
  for (let s = 0; s <= 1; s += 0.0012) {
    const [x, y] = arcAt(s);
    disc(x, y, thick / 2, AMBER);
  }

  // small bike silhouette at the top of the arc, nose up
  const [bxp, byp] = arcAt(0.46);
  const wr = N * 0.040;
  const lift = wr * 1.9;
  disc(bxp - wr * 1.5, byp - lift + wr * 0.30, wr, DIRT);        // rear wheel
  disc(bxp + wr * 1.5, byp - lift - wr * 0.25, wr, DIRT);        // front wheel, raised
  for (let t = -1; t <= 1; t += 0.02) {                          // frame
    const fx = bxp + t * wr * 1.5, fy = byp - lift + t * -wr * 0.28;
    disc(fx, fy - wr * 0.5, wr * 0.52, DIRT);
  }
  disc(bxp - wr * 0.15, byp - lift - wr * 1.75, wr * 0.55, DIRT); // rider

  return png(N, N, buf);
}

for (const n of [180, 192, 512]) {
  fs.writeFileSync(require('./paths').assets('icon-' + n + '.png'), draw(n));
  console.log('icon-' + n + '.png', fs.statSync(require('./paths').assets('icon-' + n + '.png')).size + ' bytes');
}
