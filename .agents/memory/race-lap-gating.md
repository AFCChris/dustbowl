---
name: Race lap gating & countdown timers
description: Pitfalls when adding riders that start behind the start/finish line, and countdown timer lifecycle
---
- Any rider (player or AI) spawning just *behind* the start/finish line will register a spurious lap on its first crossing unless it has the same mid-lap `sectorSeen` qualification gate as the player. Apply the gate to every lap-detection path, including analytical/LOD advance paths.
**Why:** The staggered AI grid shipped one lap short until gated; the review caught it.
**How to apply:** When adding new spawn points, race modes, or riders, verify lap detection needs a mid-track sector visit first, and start race/lap clocks at GO, not at reset.
- Countdown/start sequences using setTimeout chains must be tokenized (`_cdToken`) so quit/restart invalidates stale callbacks.
