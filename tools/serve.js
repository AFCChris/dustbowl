/* Local preview of dist/ exactly as a static host would serve it.
   node tools/serve.js  →  http://localhost:8099 */
const http = require('http');
const fs = require('fs');
const path = require('path');
const P = require('./paths');

const ROOT = P.dist();
const PORT = process.env.PORT || 8099;
const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.png': 'image/png',
  '.webmanifest': 'application/manifest+json',
  '.md': 'text/plain; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8'
};

http.createServer((req, res) => {
  // dev only: lets an automated browser post a rendered frame back to disk
  if (req.method === 'POST' && req.url.startsWith('/shot')) {
    const name = (new URL(req.url, 'http://x').searchParams.get('n') || 'shot') + '.jpg';
    let b = '';
    req.on('data', (c) => (b += c));
    req.on('end', () => {
      fs.writeFileSync(path.join(P.ROOT, name), Buffer.from(b.replace(/^data:image\/\w+;base64,/, ''), 'base64'));
      res.writeHead(200, { 'Access-Control-Allow-Origin': '*' });
      res.end('ok');
    });
    return;
  }
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p === '/') p = '/index.html';
  const f = path.join(ROOT, p);
  if (!f.startsWith(ROOT)) { res.writeHead(403); res.end('no'); return; }
  fs.readFile(f, (e, d) => {
    if (e) { res.writeHead(404); res.end('not found'); return; }
    res.writeHead(200, { 'Content-Type': TYPES[path.extname(f)] || 'application/octet-stream' });
    res.end(d);
  });
}).listen(PORT, () => console.log('serving dist/ on http://localhost:' + PORT));
