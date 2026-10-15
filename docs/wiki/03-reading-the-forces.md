# 3. Reading the forces

## Colors

With the hammer, the X-ray and the placement preview, a piece is colored by how close it is to its
limit:

| Color | Meaning |
|---|---|
| Blue | Relaxed (0%) |
| Green | About a third of its limit (33%) |
| Yellow | Two thirds (66%): getting close |
| Red | At its limit (100%) or past it |
| Light blue | Still being calculated |
| Magenta | No path to the ground at all |
| Grey | A world building, not simulated |

## The hammer

Aim at a piece with the hammer. Next to the crosshair, one fact per line:

1. **Stress**: how close the piece is to its limit and how it is loaded, e.g. *Compression 72%*. Past
   100% it adds *would break* (*would fall* for a stone that would tip or slide off), and *cracking!*
   while it cracks.
2. **Name · material · own weight**, e.g. *Wood floor · wood · 260 kg*. A very big structure adds
   *simplified model* (see **Server admins**).
3. **Holds**: the weight it holds up, that is whatever rests on it and everything above, e.g.
   *Holds 6.5 t* on a post of a two-storey house. Players, carts, chests' contents and snow count.
4. **Snow**: the snow lying on it, in the Deep North.
5. **Squeezed** or **Stretched**: for posts, beams and logs, the force along the piece, e.g.
   *Squeezed 6.7 t*.
6. **Rests on**: what holds the piece from below that Fysik counts as ground: *the ground* (terrain,
   including pieces buried up to 2 m), *rock*, *a tree* or *a world building*, e.g. *Rests on the
   ground, rock*. These pieces are the foundations of the structure: everything else holds through
   them. A piece resting on a tree or a rock that can be mined falls if the tree is cut down or the
   rock mined away.
7. **Damaged**: below half health, the strength the piece has left, e.g. *Damaged · 60% strength*.
   Repair it to get the rest back.

The stress line names what is closest to breaking the piece:

| Stress | The piece is | Typical of |
|---|---|---|
| Compression | squeezed along its length | posts, pillars, arches |
| Tension | pulled along its length | hangers, the tie of an arch, the bottom of a truss |
| Bending | bent across | beams, floors, cantilevers |
| Shear | cut across, close to a support | short beams carrying a lot |
| Torsion | twisted | beams held from one side |
| Tipping | resting on a joint whose force leans towards the edge of the contact (stone) | arches, corbels, stones overhanging the one below |
| Sliding | pushed sideways along a joint, close to slipping (stone) | the feet of an arch, stones leaning on a slope |
| Unstable | free to tip or slide away: it will fall (stone) | a stone hung on the side of a wall, an arch without its keystone |

A piece is much stronger squeezed or pulled than bent, which is why braces, trusses and arches
work. Stone, black marble and grausten rest on each other like masonry: their joints never pull, so
a stone needs something under its weight (see **How it works**). Long, thin pieces buckle, so a slender post carries less in compression than its material
alone would allow.

Aiming at a chest, a torch, a pile or another object that holds nothing up shows its name and
weight, for a chest what is inside it (*Holds 240 kg*), and *Rests on the ground* when it stands on
the ground.

## Placing a piece

The ghost is colored by the stress it would have once placed, with the value next to the crosshair
(*Preview: Bending 85%*) and the same lines as the hammer, so a post slid under a beam says what it
will take, and *Rests on the ground* tells you a foundation post really reaches the ground. If
placing it would push another piece past its limit, a red line names that piece and its value. When
vanilla says the spot is invalid, the ghost stays vanilla red. Objects (chests, piles, furniture) do
not preview their weight; it counts once they are placed.

## X-ray

**F7** turns the X-ray on and off: every piece within 40 m is colored by stress. **Ctrl+F7** widens
the radius by 10 m, up to 80 m, then back to 40 m.

## Cracking

- A piece at 100% or more **cracks**: it pulses red, shakes, creaks and sheds dust.
- It falls after a warning that depends on how far past its limit it is: 5 s up to 110%, 2.5 s at
  155%, none from 200%, where it falls at once. The server sets the 5 s
  (`Failure.CrackWarningSeconds`).
- Shore it up (a post, a brace, finish the arch) or take the load off (the cart rolls on, the snow
  is shoveled off) before the time runs out, and it stops cracking.
- When a piece gives way, everything that loses its support comes down with it, as debris that
  bursts on hitting the ground. For 3 s afterwards, whatever is left overloaded in the same
  structure falls without warning.
- A piece left with no path to the ground at all (you removed what held it) has nothing to shore up
  and falls at once, as in vanilla.
- Badly damaged pieces are weaker: below half health their strength drops, down to a quarter at the
  brink of breaking, and the hammer shows what is left (*Damaged · 60% strength*). Rain wear alone
  never weakens a piece. Repair them.
- Nothing falls while a structure is still loading: pieces that appeared in the last 10 s or lie
  close to the edge of the loaded area wait.

## Saving a structure for a report

**Ctrl+F8** writes the structure under the hammer to `BepInEx/fysik-dump.txt`: every piece, its
weight, stress, joints and what it rests on. Attach it to an issue on GitHub when a build behaves strangely (see
**FAQ**).
