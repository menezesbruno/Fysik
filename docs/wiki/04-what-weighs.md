# 4. What weighs

Every weight below loads the pieces under it and everything that holds those up, down to the
ground. Aim at a piece with the hammer to see what it holds (*Holds*).

## Building pieces

Floors, walls, beams, posts, roofs, stairs, doors and other structural pieces weigh their volume
times their material's density:

| Material | kg per m³ |
|---|---|
| Wood | 500 |
| Core wood | 600 |
| Timberwood | 650 |
| Ancient | 700 |
| Iron | 1200 |
| Ice | 920 |
| Grausten | 2300 |
| Stone | 2400 |
| Black marble | 2700 |

For example a 2×2 wood floor weighs about 260 kg, a 2 m wood beam 160 kg, a 4 m core wood log 600 kg
and a 26° wood roof piece 320 kg. Stone is much heavier: a 2×2 stone floor is 9.6 t and a 2×1 stone
wall 4.8 t, their thin versions half that, and a stone of a 6 m tower about 2.4 t (1.3 t thin).
Fences, stake walls and carved statues count as structure too.

## Furniture and other objects

Beds, tables, chairs, crafting stations, chests, torches, lights, decorations and other pieces the
game tags as objects are not solid blocks, so they weigh the materials they are built from:

| Material | kg each |
|---|---|
| Wood (any kind) | 4 |
| Stone, grausten, black marble, obsidian | 10 |
| Metal bar | 4 |
| Nail | 0.1 |
| Anything else | 2 per unit of item weight |

A smelter weighs about 250 kg, a forge 120 kg, a bed 32 kg, a chair 16 kg, an empty chest 40 to
113 kg and a standing torch about 9 kg. Objects that hold nothing up (chests, workbenches, torches,
some benches) share their weight among the pieces they rest on, the ground taking its share when
they also touch it.

## Piles and stacks

Piles of stone, wood, coal and the like, and stacks of logs or metal bars, weigh what their items
weigh in a chest (next section):

| Pile or stack | Weight |
|---|---|
| Stone, grausten, black marble, coal, flint or skull pile (50 items) | 100 kg |
| Wood, core wood, fine wood, frost wood, blackwood or yggdrasil stack (50 logs) | 100 kg |
| Stack of 30 iron, copper, bronze, black metal or flametal bars | 360 kg |
| Stack of 30 silver or gold bars | 420 kg |
| Stack of 30 tin bars | 240 kg |
| Bone stack | 25 kg |
| Treasure pile | 100 kg |

## Chests

What is inside a chest weighs 1 kg per unit of the game's item weight, so the weights in your
inventory read as kilograms. A full slot weighs:

| In the slot | Weight |
|---|---|
| 50 wood, core wood, stone, coal, grausten and the like | 100 kg |
| 30 ore or scrap | 300 kg (tin 240 kg, silver 420 kg) |
| 30 metal bars | 360 kg (tin 240 kg, silver and gold 420 kg) |

A wood chest full of stone is about 1 t, a reinforced chest 2.4 t and a black metal chest 3.2 t. The
pieces under a chest are recalculated when you put things in or take them out.

## Players

A player weighs 80 kg plus their inventory, counted like a chest's contents, on the piece they stand,
sit or lie on. A jump does not lift the load for the moment you are in the air.

## Carts

A cart weighs its materials (about 80 kg) plus its cargo, counted like a chest's contents, shared
among the pieces under its wheels:

| Full cart (18 slots) of | Weight |
|---|---|
| Wood, core wood, stone or coal | about 1.9 t |
| Ore | about 5.5 t |
| Metal bars | about 6.6 t (silver 7.6 t) |

A cart moving across a bridge is recalculated each time a wheel rolls onto another piece. See
**Building guide** for which bridges carry which carts.

## Snow

In the Deep North, snow builds up on pieces left in the open, in four steps from a thin layer to full
depth, and it weighs. A full layer is 150 kg per square metre of ground, and roofs hold less of it
the steeper they are:

| Roof slope | Share of the snow it holds |
|---|---|
| Flat up to 30° | 80% |
| 45° | 40% |
| 60° and steeper | none, it slides off |

Under full snow a flat 2×2 floor carries about 480 kg, a 26° wood roof piece about 510 kg (more than
its own weight) and a 45° roof piece about 250 kg. Walls carry only what lies on their top edge.

- Shovel a roof, or walk across it, to clear the snow and its weight.
- Pieces the game already protects from heavy snow hold none: tarred (darkwood) roofs and the stave
  church pieces. Neither does a world with the no heavy snow modifier.
- Vanilla's snow damage, which wore down exposed pieces by their distance to the ground whatever the
  build, is replaced by this weight.

## Turning loads off

The server can turn chest contents, players, carts and snow off together with
`Weight.LiveLoads = false`; the pieces, the objects built on them and piles still weigh. Each weight
above can also be changed in the `Weight` settings (see **Server admins**).
