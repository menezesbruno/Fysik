# 8. How it works

## The model

Every piece is a rigid block, and every pair of touching pieces is joined at the centre of the area
where they touch. The joint is elastic: it stretches, shears, bends and twists as much as the two
pieces would between their centres and that point, each treated as a short beam. A thin floor glued
on a thick beam is therefore much softer than the beam, and leaves the load to it, as in a real
building. Pieces resting on the ground, rocks or trees are joined to the ground, once per patch of
ground they touch; pieces buried up to 2 m count as resting on it.

A joint carries its force over the whole area where the pieces touch, not at one point. Where two
pieces meet face to face or along an edge (a wall on a wall, a floor along a beam, a piece on the
ground), the pressure varies in a straight line across that area, stronger on the side the load
leans towards, as in a real bearing; a stack of walls is therefore only squeezed, however tall. Over
a short contact, such as the end of a post, the force is spread evenly.

Joints of wood, iron and every other material are rigid: a rectangle of posts and beams holds on
its own, and triangles and braces relieve it by carrying the load along their length.

![How wood works: squeeze, pull and bend, cantilevers and braces, rectangles and triangles, buckling](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/wood-theory.png)

## Masonry

Stone does not work like that. A mason's wall is stones resting on stones, and the theory masons
and engineers use for it (Jacques Heyman's) takes three things as given: the joints carry no
tension, the stone never runs out of compressive strength, and friction stops the stones sliding.
What matters is where the force goes through each joint: while it stays inside the area where two
stones touch, they press on each other and stay put; when it reaches the edge, the joint opens on the
other side and the stones rock on that edge like a hinge. A few hinges are harmless (an arch whose
supports spread a little cracks open at the crown and stands for centuries), but once there are
enough of them for the stones to move, the structure falls: an arch with four hinges collapses.

![How stone works: where the force passes through a joint, the line of thrust in an arch, bonded courses over an opening, buttress and pinnacle](https://raw.githubusercontent.com/menezesbruno/Fysik/main/docs/images/masonry-theory.png)

Fysik applies this to every joint between two stone, black marble or grausten pieces:

- It solves the structure as if every joint were glued, then looks at each stone joint: a joint
  pulled apart opens, a joint whose force passes outside the contact becomes a hinge on the edge it
  leans on, and a joint pushed sideways more than friction holds (0.6 of the squeeze) slips. It
  solves again, and repeats until no joint changes.
- If some stones can then move freely, they fall (*Unstable*). To tell, Fysik solves once more with
  the open joints ten times softer still: a part that is really held moves the same, a part that is
  loose moves ten times more. Where the stones of a loose part would press into a neighbour as they
  move, that contact is closed again before deciding, since it would catch them.
- The hammer shows, for each stone, how far the force in its worst joint has moved towards the edge
  of the contact (*Tipping*: 0% in the middle, 100% at the edge) and how close the joint is to
  slipping (*Sliding*), when either is higher than the stress inside the stone itself.

Stone resting on wood, iron or the ground is still joined rigidly, and two stones that only touch
corner to corner along an edge do not hold each other at all.

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
  their limits: a balcony of 4 m core wood logs is comfortable 8 m out (43%) and reaches its limit
  12 m out (97%).

## When it calculates

A structure is recalculated only when something changes: a piece placed, removed or damaged, an
object placed on it or taken away, a chest's contents, a player or a cart moving onto another piece,
snow building up or shoveled off, a setting. Only the affected structure is solved, on a worker
thread; reading its pieces takes at most `Performance.FrameBudgetMs` (1.5 ms) of each frame. A base
of 850 pieces takes about half a second. Above
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

Fysik is free software, under the GNU General Public License v3.0:
<https://github.com/menezesbruno/Fysik>. The solver is plain C#, tested against beam theory, and runs
outside the game too.
