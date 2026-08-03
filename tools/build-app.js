/* Builds dist/ — the standalone, hostable app.
   This is the only build with its own <head>, which is what lets iOS install it
   to the home screen and run it fullscreen. */
const fs = require('fs');
const path = require('path');
const P = require('./paths');

const OUT = P.dist();
fs.mkdirSync(OUT, { recursive: true });

const three = fs.readFileSync(P.vendor('three.min.js'), 'utf8');
const shell = fs.readFileSync(P.src('shell.html'), 'utf8');
const game = fs.readFileSync(P.src('game.js'), 'utf8');

/* A syntax error in game.js kills the whole script silently — the page loads,
   Three.js is there, and nothing runs. Refuse to build rather than ship that. */
try {
  new Function(game);
} catch (e) {
  console.error('\n  game.js failed to parse: ' + e.message + '\n  Build aborted.\n');
  process.exit(1);
}

// the shell carries its own <title>; the document head below supplies the rest
const body = shell.replace(/<title>.*?<\/title>\s*/, '');

const html = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover">
<title>Dustbowl '98</title>
<meta name="description" content="Arcade dirt-bike riding over procedural desert.">
<meta name="theme-color" content="#14161c">
<link rel="manifest" href="manifest.webmanifest">
<link rel="icon" href="icon-192.png" sizes="192x192">
<!-- iOS: these are what make "Add to Home Screen" launch without Safari's chrome -->
<meta name="mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<meta name="apple-mobile-web-app-title" content="Dustbowl">
<link rel="apple-touch-icon" href="icon-180.png">
<style>*{box-sizing:border-box}html,body{margin:0;padding:0}</style>
</head>
<body>
${body}
<script>${three}<\/script>
<script>${game}<\/script>
</body>
</html>
`;

const manifest = {
  name: "Dustbowl '98",
  short_name: 'Dustbowl',
  description: 'Arcade dirt-bike riding over procedural desert.',
  start_url: './',
  scope: './',
  display: 'standalone',
  orientation: 'landscape',
  background_color: '#14161c',
  theme_color: '#14161c',
  icons: [
    { src: 'icon-192.png', sizes: '192x192', type: 'image/png' },
    { src: 'icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' }
  ]
};

fs.writeFileSync(path.join(OUT, 'index.html'), html);
fs.writeFileSync(path.join(OUT, 'manifest.webmanifest'), JSON.stringify(manifest, null, 2));
for (const n of [180, 192, 512]) {
  fs.copyFileSync(P.assets('icon-' + n + '.png'), path.join(OUT, 'icon-' + n + '.png'));
}
fs.copyFileSync(path.join(P.ROOT, 'DEPLOY.md'), path.join(OUT, 'README.md'));

console.log('built dist/');
for (const f of fs.readdirSync(OUT)) {
  console.log('  ' + f + '  ' + (fs.statSync(path.join(OUT, f)).size / 1024).toFixed(0) + 'kb');
}
