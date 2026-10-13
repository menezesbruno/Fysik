# 5. Building guide

The pictures and numbers below were computed with Fysik's own solver and the default materials,
using the same pieces you build with. In game, pieces snap a little differently, so expect similar,
not identical, numbers.

## Squeeze, don't bend

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

## Arches

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

## Wider gaps and boats

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

## Stone bridges

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

## Snow

Snow weighs on whatever is left in the open, mostly roofs. With full snow at the default weight and
26° roofs of wood:

- A house up to 8 m wide with posts every 2 to 4 m under the eaves and the ridge stays below 40%.
- A 12 m hall with posts every 4 m reaches about 75%. A 16 m hall with posts every 4 m under the eaves
  and every 8 m under the ridge is already at 63% with no snow and gives way when the snow is half deep.
- **Steep roofs shed snow:** the same 16 m hall with 45° roofs holds full snow at about 85%.
- A roof sticking out from a wall with nothing under it holds one roof piece out (37%), not two (136%).
- Shovel the roof when something starts to crack, or prop it from below.

## Carts on bridges

A cart weighs about 1.9 t full of wood, core wood or stone, 5.5 t full of ore and 6.6 t full of metal
bars (see **What weighs**). With the cart on the worst spot of a 4 m wide deck, core wood logs with
floors on top unless the row says otherwise:

| Bridge | Empty | Wood | Ore | Bars |
|---|---|---|---|---|
| Posts every 4 m | 8% | 17% | 42% | 50% |
| Posts every 8 m | 20% | 32% | 56% | 64% |
| Floors on 2 m wood beams, posts every 4 m | 9% | 28% | 64% | 75% |
| 12 m span, posts only at the ends | 43% | 60% | 93% | 103% |
| 16 m span, posts only at the ends | 76% | 98% | 141% | 154% |
| Tied arch, 24 m (above) | 16% | 20% | 50% | 59% |
| Truss, 24 m (above) | 49% | 53% | 70% | 76% |
| Arch under the deck, 40 m (above) | 45% | 54% | 79% | 87% |

- A bridge with posts every 4 to 8 m, or any of the bridges above, carries any cart.
- Long spans resting only on their ends are where carts matter: wood gets across a 16 m span just
  under the limit, ore and metal bring it down. For heavy loads, put a post in the middle, a truss
  under the deck or an arch.
- A cart that stops on an overloaded bridge brings it down. Moving, it may make it across a bridge
  that is only just too weak, while the pieces under it crack and stop again behind it.

## Materials

- **Stone** is very strong squeezed and weak pulled: use it for walls, pillars and arches, not for
  beams or overhangs. Span an opening in a stone wall with an arch or a wooden beam.
- **Core wood** (logs) is stronger than wood; **iron** is strong in every direction.
- Long, thin pieces buckle: a slender post carries less than its material alone would allow.

## Tips

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
