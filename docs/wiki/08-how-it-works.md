# 8. How it works

## The model

Every piece is a rigid block, and every pair of touching pieces is joined at the centre of the area
where they touch. The joint is elastic: it stretches, shears, bends and twists as much as the two
pieces would between their centres and that point, each treated as a short beam. A thin floor glued
on a thick beam is therefore much softer than the beam, and leaves the load to it, as in a real
building. Pieces resting on the ground, rocks or trees are joined to the ground; pieces buried up to
2 m count as resting on it.

All joints are rigid: a rectangle of posts and beams holds on its own, and triangles and braces
relieve it by carrying the load along their length.

## The calculation

Each connected structure is solved as a 3D frame. The weight of every piece, and of whatever rests on
it, pushes down; the solver finds how far every joint moves so that every piece is in balance, which
gives the force and the twist in every joint. Each piece is then cut across along its three
directions, at its centre and next to every joint, and each cut is checked for squeeze, pull, bending,
shear and twist against the strength of its material. The worst cut is the stress shown by the hammer,
as a percentage of what the piece can take.

- Floors, walls and roofs are checked as plates, so twisting between uneven supports reads as
  bending.
- Long, thin pieces buckle: their limit in compression drops with their slenderness.
- Damaged pieces are weaker below half health.
- Materials are about a tenth as strong as the real ones, so that builds at Valheim's scale reach
  their limits: a balcony of 4 m core wood logs is comfortable 8 m out (46%) and breaks 12 m out
  (101%).

## When it calculates

A structure is recalculated only when something changes: a piece placed, removed or damaged, an
object placed on it or taken away, a chest's contents, a player or a cart moving onto another piece,
snow building up or shoveled off, a setting. Only the affected structure is solved, on a worker
thread, so it takes no frame time: about half a second for 2000 pieces. Above
`Structure.MaxFrameBodies` pieces a simpler model passes the weight down to the ground without
recognising arches.

When only the loads change (someone walks onto another piece, snow builds up), the structure keeps
its last result while the new one is calculated, so it never goes back to light blue.

## Multiplayer

The player who owns a piece calculates its whole structure and decides only that piece's fate. The
calculation is deterministic, so players owning different parts of a structure get the same result.
Moving loads come from the player or cart itself: its owner, the only one who knows what it stands
on, shares the pieces it rests on and its weight, rounded to 5 kg. Snow comes from the game's own
snow buildup, in four steps. Everyone therefore solves the same structure with the same loads.

## Source

Fysik is open source, under the MIT license: <https://github.com/menezesbruno/Fysik>. The solver is
plain C#, tested against beam theory, and runs outside the game too.
