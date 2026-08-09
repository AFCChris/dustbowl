---
name: Headless smoke testing without WebGL
description: How to verify the game when the screenshot browser cannot create a WebGL context
---

The screenshot/preview capture environment fails to create a WebGL context at the GPU level ("BindToCurrentSequence failed"), so the game renders as a stuck loading screen there — that is an environment limitation, not an app bug.

**How to verify instead:** `node tools/smoke.js` — a vm sandbox that stubs the DOM (a Proxy-based "anything goes" 2D context) and `THREE.WebGLRenderer`, loads the real vendored Three.js, then drives every course through `window.__dbg` (bake, startGame, 20s of update()). The game's own `setInterval` audio watchdog keeps node alive, hence the `process.exit(0)` at the end.

**Why:** the game is one load-time IIFE; the build's `new Function` parse guard catches syntax only, so runtime bake errors need this harness.

**How to apply:** run it after any change to terrain, course layout, the COURSES catalog, or race lifecycle; add new invariants as cheap asserts there.
