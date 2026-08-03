# Dustbowl — how to put it on your phone

The `dist/` folder is the whole app. `index.html` must sit at the root of the
host — everything else sits beside it. No build step on the server, no server
code.

## 1. Host it

**Replit:** new Repl → the plain **HTML/CSS/JS** template → delete the sample
files → upload everything from `dist/` → **Run** to check it → **Deploy →
Static**.

Any static host works the same way (Netlify Drop, GitHub Pages, Cloudflare
Pages).

## 2. Put it on the home screen

On the iPhone, open the deployed URL **in Safari** — only Safari can install to
the home screen on iOS:

1. Tap **Share**
2. **Add to Home Screen**
3. Open it from the new icon — *not* from Safari

Launched from the icon it runs fullscreen, with no address bar. Turn the phone
sideways.

## Controls

- **Left thumb, anywhere on the left half** — a stick appears wherever you
  touch. Left/right steers; in the air, pull back to lift the nose, push
  forward to drop it.
- **Right thumb** — brake. Auto-throttle is on by default, so there's no gas
  pedal.
- Switch to manual throttle on the title screen or in the pause menu.
- Keyboard still works: arrows or WASD, `C` camera, `P` pause, `Q` quit,
  `R` respawn, `M` mute.
