# Changelog

## 0.1.3: what things weigh and what they hold

- Furniture, crafting stations, lighting, decor, portals and other pieces the game tags as objects now
  weigh what they are made of: 4 kg per piece of wood, 10 kg per stone, 4 kg per metal bar, 0.1 kg per
  nail and 2 kg per unit of item weight for the rest. Their colliders were counted as solid material
  before: a smelter went from about 50 t to 250 kg, a hearth from 17 t to 150 kg, a bed from 672 kg to
  32 kg. Pieces tagged as structure (building, floor, wall, roof, architecture, stairs, doors, stacks)
  keep the weight of their volume, including fences, stake walls and carved statues.
- Chests, workbenches, forges, torches, stone and wood piles and other pieces that hold nothing up
  used to weigh nothing. They now weigh their materials the same way, as a load on the pieces they
  rest on, shared among them and the ground: a stone pile 500 kg, a forge 120 kg, a chest 40 to 113
  kg, a standing torch about 9 kg. Placing, moving or removing one recalculates what holds it.
- Under the stress, the hammer now shows the piece's name, material and own weight, then a line with
  the weight it holds up, that is whatever rests on it and everything above (*Holds 6.5 t* on a post
  of a two-storey house), and for posts, beams and logs a line with how hard they are squeezed or
  stretched (*Squeezed 6.7 t*). The placement preview shows the same for the piece being placed, so a
  post slid under a beam says what it will take. Aiming at a chest, torch or pile shows its name and
  weight. The dump has two new last columns: the weight of objects resting on each piece and the
  weight it holds up.
- Fysik's text next to the crosshair has its own place now, in the game's font and size, clear of the
  game's piece health bar and hover text instead of mixed into them.
- New server settings in the `Weight` section: kilograms per wood item, stone item, metal bar and
  nail, and per unit of item weight for every other material. Changing them recalculates every
  structure.
- Numbers change: pieces under hearths, beds and other heavy furniture read lower, and pieces holding
  piles, forges or chests read higher. A 500 kg stone pile on a wood floor laid on beams adds about 7
  points. Look over floors and cantilevers that hold stone piles after updating.

## 0.1.2: joints that bend like beams

- The stiffness between a piece's centre and each joint now follows beam theory: it bends, not only
  shears. Thin pieces such as floors are no longer as stiff as logs, so a floor laid on beams leaves
  the load to the beams instead of turning yellow, and beams, posts and braces carry what they really
  carry.
- A joint now sits at the middle of the area where two pieces touch, instead of the point nearest to
  one piece's centre: a floor resting on a beam is held along its edge, not by one corner, and the
  result no longer depends on which piece was placed first.
- Numbers change: floors read lower, beams and posts higher. In the building guide, a balcony 12 m out
  is now just past the limit (101%) and the tied-arch bridge has two posts at each corner.
- New bridges in the building guide: the tied arch with braced ends (no scaffolding), a truss between
  stone abutments, a wooden arch leaving 40 m of water clear for boats, and a 100 m crossing on stone
  piers. Temporary props are drawn dashed, and each picture says whether the bridge needs them.

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
