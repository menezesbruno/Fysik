# Fysik

**Structural physics for Valheim building.** In vanilla Valheim, a piece only cares about how far it is
from the ground. Fysik replaces that with a force calculation in which the **shape** of your build
matters, in the spirit of Poly Bridge and Bridge Constructor:

- **Arches** carry their load in compression and can span gaps that a flat beam cannot.
- **Braces and triangles** carry load along their length instead of bending: a diagonal under a
  balcony halves its stress.
- **Cantilevers** bend, and the longer they reach, the more they strain.
- An overloaded piece **cracks before it falls**: it turns red, shakes, creaks and sheds dust, giving
  you a few seconds to shore it up or take the load off (fewer the more it is overloaded). Once
  something gives way, the collapse follows instantly.
- Building an arch works like the real thing: until the last stone closes it, an unfinished arch is
  just two cantilevers and needs scaffolding. Close it, remove the scaffolding, and it stands.
- **Everything weighs**: furniture, full chests, players walking across, a loaded cart rolling over a
  bridge, and the snow piling up on roofs in the Deep North. A weak bridge creaks under a cart, and
  gives way if the cart is too heavy or stops on it.

> ### ⚠ Before you install
>
> Fysik is **on from the start**. As soon as a world loads, every building made by players is checked
> against the physics Fysik applies, and **buildings that do not respect it will crack and fall**,
> including bases you built before installing it. **Back up your world first.** We recommend
> **starting a new world** with Fysik, so everything you build is designed for it from the first post.
>
> Buildings that come with the world (stone towers, abandoned houses, villages, ruins) are **not**
> simulated: they keep vanilla support, unless the server sets `Structure.WorldBuildings = Physics`
> (see [World buildings](#world-buildings)).

## Status

> **Early access.** Material values are still being calibrated. **Back up your worlds before trying
> it.**

## Installation

Install with a mod manager (r2modman, Thunderstore Mod Manager) or manually:

1. Install [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
   and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Copy `plugins/Fysik.dll` into `BepInEx/plugins/Fysik/`.

**Fysik must be installed on the server and on every client**, with exactly the same version (every
player who owns part of a structure must calculate it exactly as the others do).
Players without it cannot join a server that has it, and vice versa.

Existing player buildings follow the new rules as soon as the mod is enabled: **back up your world**,
and preferably start a new one. To look at an old base before letting it settle, set
`Structure.Mode = Sandbox` (nothing falls for lack of support) or `DisplayOnly` (vanilla decides).

## Seeing the forces

- **Hammer:** aim at a piece. It is colored by stress, from blue (relaxed) through green and yellow
  to red (at its limit), and next to the crosshair you get its stress, e.g. *Compression 72%*, then
  its name, material and own weight, the weight it holds up (*Holds 6.5 t*), the snow on it
  (*Snow 340 kg*) and, for posts, beams and logs, how hard they are squeezed or stretched
  (*Squeezed 6.7 t*). Aiming at a chest, torch or pile shows its name and weight, and for a chest
  what is inside it (*Holds 240 kg*). A player standing, sitting or lying on a piece counts in that
  piece's *Holds*. Light blue means the structure is still being calculated; magenta means it has no
  path to the ground at all; neutral grey means a world building that Fysik does not simulate
  (*World building · not simulated*).
- **Placing a piece:** the ghost is colored by the stress it would have once placed, with the value
  next to the crosshair (*Preview: Bending 85%*) and the same lines as the hammer, including what it
  would hold up. If it would push another piece past its limit, a red line names that piece. When
  vanilla says the spot is invalid, the ghost stays vanilla red.
- **X-ray (F7):** colors every piece within 40 m. **Ctrl+F7** widens the radius by 10 m, up to 80 m,
  then back to 40 m.
- **Cracking:** a piece above 100% pulses red, shakes, creaks and sheds dust. Just past its limit (up
  to 110%) you have `CrackWarningSeconds` (5 s) to shore it up (add a post, a brace, finish the arch)
  or take the load off before it falls; the further past, the shorter the warning: half of it at 155%,
  none at twice its limit. If the load goes away in time (the cart rolls on, you shovel the snow off),
  the piece stops cracking. Once something gives way, everything that lost its support comes down with
  it at once, as debris that bursts on hitting the ground, like a building being imploded; whatever is
  left overloaded follows without warning. A piece left with no path to the ground at all (you removed
  its support) has nothing to shore up and falls at once, as in vanilla.
- **Furniture:** beds, tables, smelters, hearths, portals and other pieces the game tags as objects
  rather than structure weigh what they are made of (4 kg per piece of wood, 10 kg per stone, 4 kg per
  metal bar, adjustable in the `Weight` settings): a smelter about 250 kg, a bed 32 kg, a chair 16 kg.
  Chests, workbenches, forges, torches and similar pieces that hold nothing up weigh the same way and
  load the pieces they rest on: a forge 120 kg, an empty chest 40 to 113 kg, a standing torch about
  9 kg. Their load counts once they are placed; the ghost does not show it yet. They still need
  something to rest on and fall when the piece under them goes, as in vanilla. Fences, stake walls and
  carved statues count as structure and weigh their full volume.
- **Piles and stacks:** wood, stone, coal and other piles and stacks weigh what their items weigh in
  a chest (see below): a stone pile or a wood stack 100 kg, a stack of iron bars 360 kg.

## What weighs on a structure

Besides the pieces themselves and the objects on them:

- **Chests:** what is inside weighs 1 kg per unit of the game's item weight (`Weight.Contents`), so
  the inventory weights read as kilograms: 50 stones weigh 100 kg, 30 iron bars 360 kg, a reinforced
  chest full of stone about 2.4 t. The pieces under a chest are recalculated when you put things in or
  take them out.
- **Players:** 80 kg (`Weight.Player`) plus their inventory, counted like a chest's contents, on the
  piece they stand, sit or lie on.
- **Carts:** their own weight (about 80 kg) plus their cargo, counted like a chest's contents and
  shared among the pieces under their wheels. A full cart of wood, core wood or stone is about 1.9 t,
  of ore about 5.5 t, of metal bars about 6.6 t.
- **Snow:** in the Deep North, snow builds up on pieces left in the open, and it weighs: a full layer
  is 150 kg per square metre of ground (`Weight.Snow`). A roof up to 30° steep holds 80% of it, a 45°
  roof 40%, and from 60° the snow slides off. Shovel a roof, or walk across it, to clear the snow and
  its weight. Pieces the game already protects from heavy snow (tarred roofs, the stave church pieces)
  hold none, and neither does a world with the no heavy snow modifier. In Physics and Sandbox modes,
  Fysik replaces vanilla's snow damage, which wore down exposed pieces by their distance to the
  ground.

All of these are on by default; the server turns them off together with `Weight.LiveLoads = false`,
leaving only the pieces and the objects built on them. Vanilla's snow damage stays off either way in
Physics and Sandbox modes.

Moving loads are recalculated each time someone steps onto another piece. A structure keeps its last
result while the new one is calculated, so a big base with people walking around it does not flicker.

## World buildings

Stone towers, abandoned houses, villages, ruins, fortresses and everything else the game places in
the world were designed for vanilla support, not for real forces. By default Fysik leaves them alone:

- **Only what players build is simulated.** World buildings keep vanilla support: they stand as the
  game built them, and if you break their base, the rest crumbles as in vanilla. Fysik never brings
  them down.
- They show in **neutral grey** with the hammer and the X-ray.
- A player build resting on a world building is held as if on rock. If that world building is
  destroyed, your build is recalculated and may fall.

To simulate world buildings too, the server sets `Structure.WorldBuildings = Physics`. They are then
calculated, colored and brought down like any player build. **This changes the game:** many of them do
not respect the physics and will crack and fall, for good, as soon as someone comes near.

A piece counts as a world building when no player placed it. Pieces spawned with console commands, or
by mods that do not record a builder, count as world buildings too.

## Building guide

The pictures below were computed with Fysik's own solver and the default materials, using the same
pieces you build with. In game, pieces snap a little differently, so expect similar, not identical,
numbers.

### Squeeze, don't bend

A piece is far stronger squeezed or pulled along its length than bent across it. A post under a roof
is squeezed and barely notices; a beam reaching out is bent, and the joint where it leaves its support
carries the whole reach. The stress grows with the square of the length: half again as far out means
more than twice the stress.

- With 4 m core wood logs and floors on top, a balcony 8 m out is comfortable (46%), 12 m out is just
  past the limit (101%) and 16 m out falls.
- A **diagonal brace** turns the bending into a squeeze: two 45° logs from the ground to the first
  joint bring the 12 m balcony back to 45%.
- The brace pushes back against whatever it stands on. Give the house a diagonal (or a solid wall),
  or the push bends its posts instead.

### Arches

An arch turns the weight into a squeeze that follows its curve, so it spans far more than a flat
beam. This bridge is 24 m long with 20 m clear underneath; hanging the deck from the arch takes it
from 67% down to 43%.

![Tied-arch bridge, 24 m: side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-tied-arch.png)

- **Prop it while you build.** Until the arches close, the hangers and half arches hang on the deck
  and the corner posts crack (118%). One plain pole under the middle of each side keeps everything
  below 38%; remove the poles once both arches are closed.
- **Hold its feet.** An arch pushes outwards where it lands. Here the deck beams tie the two feet
  together (in tension, about 46 kN); otherwise set the feet against the ground or a heavy wall.
- **Hang the deck from it.** Poles from the arch down to the deck (hangers) carry the deck in tension.
- **Two posts per corner.** A deck flexes, and a lone post glued under its end gets bent (87%); two
  posts 2 m apart hold it straight (24%).

![Tied-arch bridge with braced ends, 24 m: side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-tied-arch-braced.png)

- **Or brace the ends.** Two 45° logs from the foot of each post up to the deck close a triangle, and
  the same bridge needs no scaffolding: nothing goes above 59% while it is built, as long as all the
  beams meet before the floors go on. The deck then holds on its own (41%), so the arch is for looks.
  A short brace from halfway up the post would bend the post instead (118%).

### Wider gaps and boats

To leave more water free, put the structure under the deck and let stone take the ends. In the
pictures, temporary props are dashed: build with them, then take them away.

![Truss bridge, 24 m between stone abutments: side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-truss.png)

- **Truss.** A zigzag of 45° logs between the deck and a bottom chord 4 m below turns the weight into
  squeeze and pull along the logs: worst piece 49% between stone abutments 24 m apart, where the deck
  alone would reach 118% and fall. Build it out from both abutments, panel by panel: nothing goes above
  54% on the way, so it needs no scaffolding.

![Arch bridge, 40 m clear between stone abutments: side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-arch-bridge.png)

- **Arch under the deck.** With heavy stone abutments to push against, a wooden arch leaves 40 m of
  water clear, with about 11 m of headroom for boats in the middle: worst piece 45%. Without props
  the arch cracks once it is 12 m out; a plain pole standing in the water under every joint from 10 m
  out keeps it below 48%. Once closed it stands on its own (25%) and the props come out.

![Long crossing, three 32 m arches on stone piers: side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-viaduct.png)

- **Farther islands.** Repeat the span on stone piers standing in the water. Between two arches a
  pier is pushed from both sides and only carries weight: three 32 m arches cross 100 m at 62%. Prop
  each arch from 10 m out as it is built (it reaches 96% without), then move the props to the next span.

### Stone bridges

Stone is strong squeezed and weak pulled. A pair of stone arches (`stone_arch`) closes 4 m; wider
arches are built out from the piers in steps and closed at the top with a pair of stone arches.

![Stone arcade bridge: pillars every 4 m with pairs of stone arches; side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-stone-arcade.png)

- Pillars every 4 m, a pair of stone arches between each two, stone floors on top: worst piece 23%.
  At this span the arches are mostly for looks: the same floors laid straight on the pillars hold too
  (22%). No scaffolding: the first stone arch of each pair stands on its pillar (17%) until the second
  one closes it.

![Roman bridge: three 8 m arches on 2 m piers; side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-roman-bridge.png)

- With 8 m between the piers the arch does the work: worst piece 48%, while the same piers with a
  flat deck reach 122% and the deck falls.
- Each arch steps out 1 m and then 2 m from the piers and closes with two stone arches. Before the
  arches close nothing goes above 39%, so no scaffolding is needed.

### Snow

Snow weighs on whatever is left in the open, mostly roofs. With full snow at the default weight and
26° roofs of wood:

- A house up to 8 m wide with posts every 2 to 4 m under the eaves and the ridge stays below 40%.
- A 12 m hall with posts every 4 m reaches about 75%. A 16 m hall with posts every 4 m under the eaves
  and every 8 m under the ridge is already at 63% with no snow and gives way when the snow is half deep.
- **Steep roofs shed snow:** the same 16 m hall with 45° roofs holds full snow at about 85%.
- A roof sticking out from a wall with nothing under it holds one roof piece out (37%), not two (136%).
- Shovel the roof when something starts to crack, or prop it from below.

### Materials

- **Stone** is very strong squeezed and weak pulled: use it for walls, pillars and arches, not for
  beams or overhangs. Span an opening in a stone wall with an arch or a wooden beam.
- **Core wood** (logs) is stronger than wood; **iron** is strong in every direction.
- Long, thin pieces buckle: a slender post carries less than its material alone would allow.

### Tips

- Watch the ghost's color before placing each piece, and check the whole building with the X-ray
  (F7) after big changes.
- When something cracks, you have a few seconds: put a post under it, add a brace, or remove what it
  carries (a stack of iron bars is 360 kg, a cart full of ore about 5.5 t).
- Before taking a loaded cart across a bridge, look at the bridge with the X-ray, and keep moving.
- Badly damaged pieces (below half health) are weaker; repair them.
- Big bases: structures up to `Structure.MaxFrameBodies` pieces (2000) get the full calculation.
  Larger ones use a simpler model that does not recognise arches; raise the limit (up to 5000) if you
  build arches into a big base.
- Too hard or too easy? Ask your server admin about `Structure.Difficulty`.

## Roadmap

Next:

- Masonry: stone blocks that only hold by pressing on each other, interlocking walls, arches built on wooden centering

Later:

- Creatures' weight (tamed lox, trolls) on bridges and floors
- Calibration with player builds (feedback welcome)
- Faster recalculation for very large bases
- Translations into every language Valheim supports
- A website to plan and test a build in the browser with the same solver

## Configuration

`BepInEx/config/menezesbruno.fysik.cfg`. Settings marked *server* are enforced by the server and
synced to every client; they can also be edited in game with a configuration manager (F1).

| Section | Setting | Default | Range | Description |
|---|---|---|---|---|
| Structure | Mode (*server*) | Physics | Physics, DisplayOnly, Sandbox | Physics: Fysik decides what falls. DisplayOnly: vanilla decides, Fysik only shows. Sandbox: nothing falls for lack of support. |
| Structure | WorldBuildings (*server*) | Vanilla | Vanilla, Physics | Buildings that come with the world (stone towers, abandoned houses, villages, ruins). Vanilla: they keep vanilla support, are not simulated and show in grey; player builds rest on them as on rock. Physics: simulated like player builds. **Warning:** many were not built for real forces and will crack and fall when players come near, changing the game. |
| Failure | CrackWarningSeconds (*server*) | 5 | 0–60 | Seconds an overloaded piece stays cracked before it falls, when it is just past its limit (up to 110%); shorter the further past, none at twice its limit. |
| Failure | DamageWeakens (*server*) | on | | Pieces below half health are weaker, down to 25% strength at the brink of breaking; rain wear alone never weakens them. Off: damage never changes strength. |
| Structure | Difficulty (*server*) | Normal | Relaxed, Normal, Strict | Relaxed: materials 50% stronger (longer spans and cantilevers). Strict: 30% weaker. |
| Structure | MaxFrameBodies (*server*) | 2000 | 500–5000 | Largest structure solved with the full frame analysis; bigger ones use a simplified model that does not recognise arches. Higher values take longer to update (about 0.5 s for 2000 pieces, 2 s for 5000). |
| Material.\<type\> | Density, CompressiveStrength, TensileStrength, ShearStrength, BucklingSlenderness (*server*) | per material | | Density (kg/m³) gives structural pieces their weight; strengths in MPa. Stone is strong in compression and weak in tension. Objects weigh their materials instead (see Weight). |
| Weight | Wood, Stone, MetalBar, Nail (*server*) | 4, 10, 4, 0.1 | 0–1000 | Kilograms per item used to build an object (furniture, crafting stations, chests, torches); objects weigh their materials, not their volume. |
| Weight | PerItemWeight (*server*) | 2 | 0–100 | Kilograms per unit of the game's item weight, for every other material in an object's recipe. |
| Weight | LiveLoads (*server*) | on | | Chest contents, players, carts and Deep North snow weigh on the structure. Off: only the pieces and the objects built on them (piles and stacks included) weigh. |
| Weight | Contents (*server*) | 1 | 0–20 | Kilograms per unit of the game's item weight for stored items: inside chests, carts and players' inventories, and in piles and stacks (at 1, the game's weights read as kilograms). |
| Weight | Player (*server*) | 80 | 0–500 | Body weight of a player, kilograms; their inventory adds to it. |
| Weight | Snow (*server*) | 150 | 0–1000 | Weight of a full layer of Deep North snow, kilograms per square metre of ground; roofs hold less of it the steeper they are. 0: snow weighs nothing. |
| Display | HammerInfo | on | | Stress color, value, weight and weight held on the piece under the hammer. |
| Display | PlacementPreview | on | | Predicted stress and weight held on the piece being placed. |
| Display | CollapseAnimation | on | | Falling pieces come down as debris and burst on impact (off: they shatter in place, as in vanilla). |
| Display | XRayKey | F7 | | Toggles the X-ray view. |
| Display | XRayRadius | 40 | 40–80 | X-ray radius, metres. |
| Display | XRayRadiusKey | Ctrl+F7 | | Widens the X-ray radius by 10 m (after 80 m, back to 40 m). |
| Performance | FrameBudgetMs | 1.5 | 0.25–10 | Time per frame spent on calculations. |
| Performance | BackgroundSolver | on | | Solve structures on a worker thread, without taking frame time. |
| Debug | LogSolves | on | | One log line per structure solved. |
| Debug | LogGeometry | on | | One log line per kind of piece: colliders, size, weight. |
| Debug | DumpKey | Ctrl+F8 | | Writes the structure under the hammer to `BepInEx/fysik-dump.txt`. |

The console command `fysik stats`, `fysik recalc` or `fysik dump` (F5; the game must be started with
`-console`) shows solver statistics, recalculates everything or writes the structure under the hammer
to `BepInEx/fysik-dump.txt`.

## Compatibility

Fysik takes over structural integrity, so it is expected to conflict with other mods that change it,
such as NoBuildIntegrity, Forever Build or Balrond Better Build. Pieces added by other mods will use
conservative default material properties, with a warning in the log for each unknown material.
Their furniture and other objects weigh their recipe: Fysik knows the vanilla wood, stone, metal bars
and nails, and counts any other resource by its weight in the inventory (`Weight.PerItemWeight`).

## Links

- Source code and issues: <https://github.com/menezesbruno/Fysik>
- License: MIT
