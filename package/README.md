# Fysik

**Structural physics for Valheim building.** In vanilla Valheim, a piece only cares about how far it is
from the ground. With Fysik, the **shape** of your build matters, in the spirit of Poly Bridge and
Bridge Constructor.

**Found a bug or have a suggestion?** Open an issue on
[GitHub](https://github.com/menezesbruno/Fysik/issues).

![A 24 m tied-arch bridge with braced ends, every piece colored by its computed stress: side view, top view, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-tied-arch-braced.png)

*A 24 m bridge from the building guide, every piece colored by the stress Fysik computes: the worst
is at 42%, and it goes up without scaffolding. The parts list and building order are in the picture.*

## How it works

- **Everything weighs, and the weight has to reach the ground** through the pieces below. Fysik
  calculates the forces in every piece as you build.
- **Shape matters.** Arches carry their load in compression and span gaps a flat beam cannot. Braces
  and triangles carry load along their length instead of bending. Cantilevers bend, more the further
  they reach.
- **Stone is masonry.** Stones rest on each other and cannot pull: each needs something under its
  weight, and an arch of the wedge-shaped arch stones stands once its keystone is in.
- **New stone pieces**: arch stones, curved tower stones for round towers and rounded corners, and
  stone walls and floors half as thick. Stone walls and floors are cut to fit around arches and
  towers, and wooden floors and walls around towers.
- **Colors show the stress**: blue is relaxed, green is fine, yellow is getting close, red is at the
  limit.
- **Overloaded pieces crack before they fall**: they turn red, shake and creak for a few seconds,
  time to shore them up. Then whatever they held comes down with them.
- **Not only the pieces weigh**: furniture, full chests, players walking across, a loaded cart
  rolling over a bridge and the snow piling up on roofs in the Deep North.

## Quick start

1. **Back up your world**, or better, start a new one (see the warning below).
2. **Install** with r2modman or Thunderstore Mod Manager. On a server, the server and every player
   need Fysik, with the same version.
3. **Watch the ghost** while building: before you place a piece, it is colored by the stress it would
   have. Red would break.
4. **Aim the hammer** at a piece to read its stress (*Bending 72%*), its weight, the weight it holds
   up (*Holds 6.5 t*) and whether it stands on the ground (*Rests on the ground*).
5. **Press F7** for the X-ray: every piece within 40 m colored by stress (**Ctrl+F7** widens it).
6. **When something cracks**, put a post or a brace under it, or take the load off, before it falls.
7. **Build like a carpenter**: posts every 2 to 4 m, a diagonal brace under anything that reaches out,
   an arch or a truss to cross a wide gap.

Worked examples, every number explained and answers to common questions are in the
[wiki](https://thunderstore.io/c/valheim/p/menezesbruno/Fysik/wiki/).

![A castle with round towers of tower stones, 1 m stone walls cut to fit them, an arched gate and a wooden wall walk on knee braces: front view, top view, wall section, parts list and building order](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/guide-castle-wooden-walk.png)

*A castle from the building guide: round towers of Fysik's tower stones, stone walls cut to fit
them, an arched gate whose stones are all cut to fit, and a wooden wall walk on knee braces behind
the merlons. Worst piece 67%; the guide also has one with walls 2 m thick and a stone wall walk.*

> ### ⚠ Before you install
>
> Fysik is **on from the start**. As soon as a world loads, every building made by players is checked
> against the physics Fysik applies, and **buildings that do not respect it will crack and fall**,
> including bases you built before installing it. **Back up your world first.** We recommend
> **starting a new world** with Fysik, so everything you build is designed for it from the first post.
>
> Buildings that come with the world (stone towers, abandoned houses, villages, ruins) are **not**
> simulated: they keep vanilla support, unless the server sets `Structure.WorldBuildings = Physics`
> (see *Server admins* in the [wiki](https://thunderstore.io/c/valheim/p/menezesbruno/Fysik/wiki/)).
>
> **Updating from 0.1.x?** Since 0.2.0 stone, black marble and grausten rest on each other like real
> masonry instead of being glued. Stone that only held because it was glued **falls**: overhangs past
> the stone below, slabs held only by the side of a wall, blocks over a doorway with nothing under
> them, half-built arches. Back up your world before updating and look over your stone builds with
> the X-ray (F7), or have the server set `Structure.MasonryJoints = false` to keep stone glued as before.
>
> The arch stones, tower stones and thin stone walls and floors are Fysik's own pieces: a world
> keeps them only while Fysik is installed.

## The physics

Wood and iron follow beam theory: a piece is strong squeezed or pulled along its length and much
weaker bent across it, so braces, trusses and arches beat long beams. Stone follows masonry theory:
blocks rest on each other, and what counts is where the force passes through the joints between them.

![How wood works: squeeze, pull and bend, cantilevers and braces, rectangles and triangles, buckling](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/wood-theory.png)

![How stone works: where the force passes through a joint, the line of thrust in an arch, bonded courses over an opening, buttress and pinnacle](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/masonry-theory.png)

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

## Wiki

Everything else is in the [wiki](https://thunderstore.io/c/valheim/p/menezesbruno/Fysik/wiki/):

- **Getting started**: installing, the three modes, old bases and your first builds.
- **Reading the forces**: the colors, every line next to the crosshair, the X-ray, the placement
  preview and how cracking works.
- **What weighs**: pieces, furniture, piles, chests, players, carts and snow, with typical weights.
- **Building guide**: balconies, arches, trusses, stone masonry, round towers, castles and stone
  bridges, snow on roofs and carts on bridges, with worked examples.
- **Server admins**: multiplayer, versions, world buildings, big bases and the settings worth knowing.
- **FAQ**: common questions, and how to send a build that behaves strangely.
- **How it works**: the calculation behind it.

## Roadmap

Next:

- Conical roofs for round towers
- More arch stones (cut from the 2×1 stone), and grausten and black marble blocks cut around arches
  and towers
- Creatures' weight (tamed lox, trolls) on bridges and floors

Later:

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
| Structure | MasonryJoints (*server*) | on | | Stone, black marble and grausten rest on each other like real masonry: their joints cannot pull, they open or rock on an edge when the weight above is off-centre and slip when pushed sideways harder than friction holds; a stone needs something under its weight. Off: these joints are glued, as up to 0.1.5. |
| Structure | MaxFrameBodies (*server*) | 2000 | 500–5000 | Largest structure solved with the full frame analysis; bigger ones use a simplified model that does not recognise arches or masonry. Higher values take longer to update (a base of 850 pieces takes about half a second). |
| Material.\<type\> | Density, CompressiveStrength, TensileStrength, ShearStrength, BucklingSlenderness (*server*) | per material | 10–20000, 0.01–1000, 5–500 | One section per material: Wood, HardWood (core wood), Timberwood, Ancient, Stone, Marble (black marble), Ashstone (grausten), Iron, Ice, and Unknown for pieces of other mods. Density (kg/m³) gives structural pieces their weight; strengths in MPa; buckling slenderness is where a slender piece's compressive limit halves. Stone is strong in compression and weak in tension. Objects weigh their materials instead (see Weight). |
| Weight | Wood, Stone, MetalBar, Nail (*server*) | 4, 10, 4, 0.1 | 0–1000 | Kilograms per item used to build an object (furniture, crafting stations, chests, torches); objects weigh their materials, not their volume. |
| Weight | PerItemWeight (*server*) | 2 | 0–100 | Kilograms per unit of the game's item weight, for every other material in an object's recipe. |
| Weight | LiveLoads (*server*) | on | | Chest contents, players, carts and Deep North snow weigh on the structure. Off: only the pieces and the objects built on them (piles and stacks included) weigh. |
| Weight | Contents (*server*) | 1 | 0–20 | Kilograms per unit of the game's item weight for stored items: inside chests, carts and players' inventories, and in piles and stacks (at 1, the game's weights read as kilograms). |
| Weight | Player (*server*) | 80 | 0–500 | Body weight of a player, kilograms; their inventory adds to it. |
| Weight | Snow (*server*) | 150 | 0–1000 | Weight of a full layer of Deep North snow, kilograms per square metre of ground; roofs hold less of it the steeper they are. 0: snow weighs nothing. |
| Display | HammerInfo | on | | Stress color and the lines next to the crosshair for the piece under the hammer: stress, weight, weight held, what it rests on, damage. |
| Display | PlacementPreview | on | | Predicted stress and weight held on the piece being placed. |
| Display | CollapseAnimation | on | | Falling pieces come down as debris and burst on impact (off: they shatter in place, as in vanilla). |
| Display | XRayKey | F7 | | Toggles the X-ray view. |
| Display | XRayRadius | 40 | 40–80 | X-ray radius, metres. |
| Display | XRayRadiusKey | Ctrl+F7 | | Widens the X-ray radius by 10 m (after 80 m, back to 40 m). |
| Performance | FrameBudgetMs | 1.5 | 0.25–10 | Time per frame spent on calculations. |
| Performance | BackgroundSolver | on | | Solve structures on a worker thread, without taking frame time. |
| Debug | DetailedLog | off | | Writes every structure solved, every crack and fall, each kind of piece (colliders, size, weight) and each ownership handover to `BepInEx/LogOutput.log`. For bug reports and development; off keeps the log clean. |
| Debug | DumpKey | Ctrl+F8 | | Writes the structure under the hammer to `BepInEx/fysik-dump.txt`. |

The console command `fysik stats`, `fysik recalc`, `fysik dump` or `fysik hash` (F5; the game must be
started with `-console`) shows solver statistics, recalculates everything, writes the structure under
the hammer to `BepInEx/fysik-dump.txt` or prints a fingerprint of its result, the same on every
player's game when they calculate it alike.

## Compatibility

Fysik takes over structural integrity, so it is expected to conflict with other mods that change it,
such as NoBuildIntegrity, Forever Build or Balrond Better Build. Pieces added by other mods will use
conservative default material properties, with a warning in the log for each unknown material.
Their furniture and other objects weigh their recipe: Fysik knows the vanilla wood, stone, metal bars
and nails, and counts any other resource by its weight in the inventory (`Weight.PerItemWeight`).

## Links

- Source code and issues: <https://github.com/menezesbruno/Fysik>
- License: GNU General Public License v3.0
