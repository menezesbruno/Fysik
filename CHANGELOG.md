# Changelog

## 0.2.0

- Pieces now press on each other over the whole area where they touch, not at one point. A wall
  standing on another wall bears on its full length, a floor resting on a beam along its whole edge,
  and a piece standing on the ground on all of the ground under it, with the pressure stronger on the
  side the load leans towards, as in a real bearing. Pieces read lower where their supports are wide:
  stacked stone no longer cracks at its base (a stack of stone walls or blocks stands far beyond 100 m
  and is only squeezed), a stone foundation under two storeys of walls went from about 70% to 12%,
  the stone abutments of the arch bridges from 45% to 18%, and houses read a few points lower.
- The ground under a piece is one support per patch of ground it touches, instead of up to sixteen
  separate points: a log lying across a ditch still rests on both banks.
- Arch stones: seven new stones at the stonecutter, next to the stone arch, cut as wedges from the 1x1
  stone, so arches can be built from stones that touch face to face, sized to the building grid: half
  circles 4, 6, 8, 10, 12, 14 and 16 m wide inside and half as high, from 9 to 29 stones, all
  standing on walls or pillars 1 m wide. Beyond 10 m the arch pushes its supports hard: give it heavy
  piers or abutments. Aim at the top of a wall or pillar and the first stone
  appears tilted as the foot of the arch, snapped to it; then aim at a stone and the next one carries
  on the curve, up to the keystone. Worlds keep them only while Fysik is installed.
- The dump lists the area of every joint and ground contact.
- Fysik is now under the GNU General Public License v3.0 (it was MIT up to 0.1.5): anyone may use,
  change and share it, and whatever is built on it and distributed must stay open under the same
  license.

## 0.1.5: what holds a piece up

- The hammer says what a piece rests on: a new line, *Rests on the ground*, on every piece that
  touches something Fysik counts as ground, naming it: the ground, rock, a tree or a world building.
  These pieces are the foundations of a structure; a piece without the line holds only through
  others. The placement preview shows it too, so a foundation post tells you it really reaches the
  ground, and so do chests and other objects standing on the ground. A piece resting on a tree or a
  rock that can be mined falls if the tree is cut down or the rock mined away.
- Badly damaged pieces say how weak they are: below half health the hammer adds *Damaged · 60%
  strength*. Repairing them brings their strength back.
- Fixed a false shear in pieces where loads met exactly at their middle, such as a wall carrying the
  wall above it straight down to the ground: whether the two forces fell on the same side of the cut
  at the centre came down to rounding. A stack of 4×2 stone walls cracked at its base at about 32 m
  and now stands to about 100 m. The building guide's numbers do not change.
- New console command `fysik hash`: aim at a structure and it prints the number of pieces, a
  fingerprint of the result and the most loaded piece. Two players looking at the same structure
  must see the same fingerprint; if they do not, their dumps show why. The dump shows the same line,
  gains a last column with what each piece rests on and lists every point where the structure touches
  the ground.
- The mod page is shorter: how it works and a quick start come first, and the building guide, what
  weighs, server notes and the FAQ moved to the
  [wiki](https://thunderstore.io/c/valheim/p/menezesbruno/Fysik/wiki/).

## 0.1.4: players, carts, chests and snow

- Players weigh on what they stand, sit or lie on: 80 kg plus their inventory. A loaded cart weighs
  its materials plus its cargo, shared among the pieces under its wheels. A weak bridge creaks under
  a cart and gives way if the cart is too heavy or stops on it; keep moving and a bridge that is only
  just too weak lets you through. Whoever owns a player or a cart tells everyone which pieces it rests
  on, so every player calculates the same.
- What is inside a chest weighs 1 kg per unit of the game's item weight: 50 stones weigh 100 kg, 30
  iron bars 360 kg, a reinforced chest full of stone about 2.4 t. Putting things in or taking them
  out recalculates the pieces under it, and aiming at a chest shows what it holds.
- Deep North snow weighs: a full layer is 150 kg per square metre of ground, on pieces left in the
  open. Roofs up to 30° steep hold 80% of it, 45° roofs 40%, and from 60° it slides off. Shovel it
  off, or walk across it, and its weight goes with it. Tarred roofs and the stave church pieces, which
  the game protects from heavy snow, hold none, nor does a world with the no heavy snow modifier.
  Vanilla's snow damage, which wore down exposed pieces by their distance to the ground whatever the
  build, is gone in Physics and Sandbox modes. The hammer shows the snow on a piece (*Snow 340 kg*).
- The crack warning now depends on how far past its limit a piece is: the full
  `CrackWarningSeconds` up to 110%, half of it at 155%, none from twice the limit. Taking the load
  off in time (the cart rolls on, the snow is shoveled) still stops the crack.
- Piles and stacks weigh what their items weigh in a chest: a stone pile went from 500 kg to 100 kg,
  a wood stack from 200 kg to 100 kg, a fine wood stack from 1.6 t to 100 kg, and a stack of iron bars
  from 125 kg to 360 kg.
- New server settings in the `Weight` section: `LiveLoads` (on by default; off turns chest contents,
  players, carts and snow off together), `Contents` (kilograms per unit of item weight for stored
  items: inside chests, carts and inventories, and in piles and stacks), `Player` (body weight) and
  `Snow` (kilograms per square metre of a full layer; 0 turns snow weight off).
- When only the loads on a structure change, it keeps its last result while the new one is
  calculated, so people walking around a big base do not leave it light blue.
- The dump has two new last columns: snow and moving loads on each piece, in kilograms.
- Numbers change: floors under full chests read higher (a reinforced chest full of stone adds 2.4 t),
  pieces under piles of stone and stacks of wood lower, pieces under stacks of metal bars higher, and
  in the Deep North roofs read higher as snow builds up. Look over storage rooms and wide roofs after
  updating.

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
