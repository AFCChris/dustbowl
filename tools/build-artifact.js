/* Builds dustbowl98.html — the single-file version for publishing as a Claude
   artifact. No <head> of its own: the artifact wrapper supplies that, which is
   why this build can't be installed to a home screen. Use dist/ for the phone. */
const fs = require('fs');
const path = require('path');
const P = require('./paths');

const three = fs.readFileSync(P.vendor('three.min.js'), 'utf8');
const shell = fs.readFileSync(P.src('shell.html'), 'utf8');
const game = fs.readFileSync(P.src('game.js'), 'utf8');

const out = shell + '\n<script>' + three + '<\/script>\n<script>' + game + '<\/script>\n';
const file = path.join(P.ROOT, 'dustbowl98.html');
fs.writeFileSync(file, out);
console.log('built dustbowl98.html  ' + (out.length / 1024).toFixed(0) + 'kb');
