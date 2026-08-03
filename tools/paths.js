const path = require('path');
const ROOT = path.join(__dirname, '..');
module.exports = {
  ROOT,
  src: (f) => path.join(ROOT, 'src', f),
  vendor: (f) => path.join(ROOT, 'vendor', f),
  assets: (f) => path.join(ROOT, 'assets', f),
  dist: (f) => path.join(ROOT, 'dist', f || '')
};
