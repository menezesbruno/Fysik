# 6. Server admins

## Versions

Fysik must be on the server and on every player, with exactly the same version: every player who
owns part of a structure must calculate it exactly as the others do. Players without it, or with
another version, cannot join. Update the server and tell your players to update at the same time.

Fysik adds pieces of its own: arch stones, tower stones, and thin stone walls and floors. A world
keeps them only while Fysik is installed; removing it removes them, and what they held may fall.

## Who calculates

Only players calculate. Whoever owns a piece (the player near it, as in vanilla) solves the whole
structure and decides that piece's fate; the calculation gives the same result on every machine, so
owners of different parts of one structure agree, and a crack is synced to everyone through the
piece. Each player and cart tells everyone which pieces it rests on, so moving loads are the same for
all.

To check that two players agree, both aim the hammer at the same structure and type `fysik hash` in
the console: it prints the number of pieces, a fingerprint of the result and the most loaded piece.
With both players close enough to have the whole structure loaded, and nobody building or moving on
it, both must see the same fingerprint. If they differ, send both players' dumps (**Ctrl+F8**) in an
issue.

The dedicated server calculates nothing. It relays, syncs the settings and hands the pieces near the
centre of the world, which it would otherwise keep, to the nearest player. It needs no extra CPU.

## Settings

Settings marked *server* in the configuration table on the mod page are enforced by the server and
synced to every player. An admin can change them in game with a configuration manager (F1); changing
a structure, weight or material setting recalculates every structure. The ones worth knowing:

- `Structure.Mode`: Physics, Sandbox or DisplayOnly (see **Getting started**).
- `Structure.Difficulty`: Relaxed makes materials 50% stronger, Strict 30% weaker.
- `Structure.MasonryJoints`: off, stone, black marble and grausten joints are glued instead of
  resting on each other like masonry (as up to 0.1.5): stone can hang off a wall or overhang freely.
  Turn it off for a while on a world whose old stone builds were made for 0.1.x, if they need time to
  be shored up.
- `Failure.CrackWarningSeconds`: the warning before a piece just past its limit falls (5 s).
- `Failure.DamageWeakens`: off, damage never changes a piece's strength.
- `Structure.WorldBuildings`: whether world buildings are simulated (below).
- `Weight.LiveLoads`: off, chest contents, players, carts and snow weigh nothing.
- `Weight.Contents`, `Weight.Player`, `Weight.Snow`: how much stored items, players and a full layer
  of snow weigh. `Weight.Snow = 0` turns only the snow off.
- `Weight.Wood`, `Stone`, `MetalBar`, `Nail`, `PerItemWeight`: what objects weigh per material.
- `Material.<type>`: density and strengths of each material, for fine calibration.

## World buildings

By default, stone towers, abandoned houses, villages, ruins, fortresses and every other piece no
player placed keep vanilla support, show in grey and are never brought down by Fysik; a player build
resting on one is held as if on rock. With `Structure.WorldBuildings = Physics` they are simulated
like player builds. **This changes the world for good:** many of them were not built for real forces
and crack and fall as soon as someone comes near. Pieces spawned with console commands, or by mods
that do not record a builder, count as world buildings.

## Big bases

Structures of up to `Structure.MaxFrameBodies` pieces (2000 by default, 500 to 5000) get the full
calculation, on a worker thread: a base of 850 pieces takes about half a second. Larger ones use a
simpler model that does not recognise arches; the hammer then adds *simplified model*. Raise the limit
if your players build arches into big bases. A structure that reaches beyond every player's loaded
area is never brought down.

## Worlds and modifiers

- A world where buildings never fall (`NoBuildingFall`) keeps that rule: Fysik shows the forces but
  brings nothing down.
- A world without heavy snow (`NoHeavySnow`) has no snow weight. A world with heavy snow on every
  piece (`AllHeavySnow`) puts snow on tarred roofs and the stave church pieces too.

## Moving an existing world to Fysik

1. Back up the world.
2. Start with `Structure.Mode = Sandbox`, so nothing falls, and let players look over their bases
   with the X-ray (F7) and shore up what is red.
3. Switch to `Physics`.

## Other mods

Mods that change structural integrity (NoBuildIntegrity, Forever Build, Balrond Better Build and the
like) conflict with Fysik. Pieces added by other mods get conservative default materials, with a
warning in the log for each unknown material; their objects weigh their recipe.

## Console

With the game started with `-console`, F5 opens the console: `fysik stats` shows solver statistics
and who owns the pieces around you, `fysik recalc` recalculates everything, `fysik dump` writes
the structure under the hammer to `BepInEx/fysik-dump.txt` and `fysik hash` prints the fingerprint of
its result (see **Who calculates**).
