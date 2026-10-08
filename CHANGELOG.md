# Changelog

## 0.1.1: world buildings

- Buildings that come with the world (stone towers, abandoned houses, villages, ruins) no longer
  crack and fall when you come near: they keep vanilla support, are not simulated and show in neutral
  grey. Player builds resting on them are held as if on rock, and are recalculated if they are
  destroyed.
- New server setting `Structure.WorldBuildings`: `Vanilla` (default) or `Physics`, which simulates
  world buildings like player builds; many of them will then crack and fall.

## 0.1.0: first release (early access)

Fysik replaces Valheim's structural support with a force calculation. **Back up your worlds.**

- Each connected structure is solved as a 3D frame, up to `Structure.MaxFrameBodies` pieces (2000 by
  default, 500 to 5000); larger ones use a simplified model that does not recognise arches. Arches
  work in compression, braces and triangles carry load along their length, cantilevers bend.
- Stress is shown on the piece under the hammer (color and value, e.g. *Compression 72%*), on the
  placement ghost before you place it, and on every piece nearby with the X-ray (F7; Ctrl+F7 widens
  the radius from 40 m up to 80 m).
- An overloaded piece cracks (pulses red, shakes, creaks and sheds dust) for
  `Failure.CrackWarningSeconds` (5 s) before it falls, leaving time to shore it up. A piece with no
  path to the ground falls at once. When a piece gives way, everything it held comes down with it,
  animated like an imploded building, and whatever is left overloaded follows without warning.
- Chests, workbenches, torches and other pieces that hold nothing up add no weight, but need
  something to rest on and fall without it.
- Rocks, trees and terrain count as ground and are watched: cut the tree or mine the rock a
  structure rests on, and it is recalculated.
- Pieces below half health are weaker, down to 25% at the brink of breaking
  (`Failure.DamageWeakens`); rain wear alone never weakens a structure.
- Server settings: `Structure.Mode` (Physics, DisplayOnly or Sandbox), `Structure.Difficulty`
  (Relaxed, Normal or Strict) and the weight and strength of each material.
- Multiplayer: the player near a structure calculates it, and the server calculates nothing. The
  server and every client must run exactly the same Fysik version.
- Nothing falls while a structure is still loading, nor (with Voxheim) before its terrain is ready.
- Structures are solved on a worker thread, without taking frame time.
- Console command `fysik stats | recalc | dump`; Ctrl+F8 writes the structure under the hammer to
  `BepInEx/fysik-dump.txt`.
