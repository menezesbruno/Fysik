# 2. Getting started

## Installing

Install Fysik with a mod manager (r2modman, Thunderstore Mod Manager); it brings
[BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and
[Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) with it. To install it by hand,
install those two first, then copy `plugins/Fysik.dll` into `BepInEx/plugins/Fysik/`.

On a dedicated server, Fysik must be on the server and on every player, with exactly the same
version. Players without it, or with another version, cannot join.

## Before you play

- **Back up your world.** As soon as it loads, every building made by players is checked, and what
  does not respect the physics cracks and falls.
- **Look at an old base first.** Set `Structure.Mode` to `Sandbox`: Fysik shows the forces, but
  nothing falls for lack of support. Shore up whatever is red, then set it back to `Physics`.
- **World buildings are left alone.** Stone towers, abandoned houses, villages and ruins keep vanilla
  support and show in grey; Fysik never brings them down.

## The three modes

`Structure.Mode` is set by the server:

| Mode | What it does |
|---|---|
| Physics (default) | Fysik decides what falls: overloaded pieces crack, then fall. Vanilla support is off. |
| Sandbox | Fysik shows the forces, and nothing falls for lack of support. For test worlds and for checking old bases. |
| DisplayOnly | Vanilla decides what falls, as without the mod; Fysik only shows the forces. |

## Your first builds

- **Watch the ghost** before placing a piece: it is colored by the stress it would have. Blue and
  green are fine, yellow is getting close, red would break.
- **Press F7** after big changes: the X-ray colors every piece within 40 m.
- **Posts every 2 to 4 m** under beams and floors keep most houses well below their limit.
- **Check the foundations.** Aim the hammer at a post or wall at the bottom of a build: *Rests on the
  ground* means it carries its load into the world. A piece without that line only holds through the
  pieces around it.
- **A beam reaching out bends**, and the longer it reaches, the worse. A diagonal brace under it
  turns the bending into a squeeze.
- **Stone** is strong squeezed and weak pulled: use it for walls, pillars and arches, not for beams
  or overhangs.
- **When something cracks** you have a few seconds, fewer the more it is overloaded: add a post or a
  brace under it, or take the load off.

The **Building guide** page has worked examples, and **Reading the forces** explains every number the
hammer shows.
