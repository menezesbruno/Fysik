# 7. FAQ

**My base fell as soon as I loaded the world.**
It was built for vanilla support, where a piece only needs to be close enough to the ground. Restore
the backup, set `Structure.Mode = Sandbox` to see the forces without anything falling, shore up what
is red, then switch back to `Physics`.

**Why is a piece light blue?**
Its structure is still being calculated. A big base takes up to a couple of seconds after a change.

**Why is a piece magenta?**
It has no path to the ground at all. In Physics mode it falls at once.

**Why are some buildings grey?**
They come with the world (towers, villages, ruins). They keep vanilla support and Fysik does not
simulate them; see **Server admins** to change that.

**A piece is over 100% and did not fall.**
It cracks first and falls after a few seconds; take the load off or shore it up and it stops. It may
also be waiting for its structure to finish loading, or the world or the server may not let buildings
fall (Sandbox or DisplayOnly mode, or a world where buildings never fall).

**A piece fell without any warning.**
Either it was twice its limit or more, it had no path to the ground left, or something in the same
structure had just given way (for 3 s after a fall, overloaded pieces fall at once).

**My stone fell, but nothing was overloaded.**
Stone, black marble and grausten rest on each other like real masonry: their joints cannot pull. A
stone that only touches the side of another, sticks out past the one below it with its weight beyond
the edge, or sits over a doorway with nothing under it tips or slides off (*Unstable · would fall*).
Put something under its weight, weigh down its back end with the stones above, span the opening with
a lintel or an arch, and prop arches until the keystone is in. The server can glue stone joints again
with `Structure.MasonryJoints = false`.

**Why does my roof crack in the Deep North?**
Snow piles up on it and weighs. Shovel it off, prop the roof from below, or build steeper roofs:
from 30° snow starts to slide off, from 60° none stays. Tarred roofs are protected from snow.

**My bridge broke under a cart.**
A full cart weighs from about 1.9 t (wood) to 6.6 t (metal bars). Long spans resting only on their
ends need a post in the middle, a truss or an arch; see **Building guide**.

**Can I turn off the weight of chests, players, carts and snow?**
The server can, with `Weight.LiveLoads = false`.

**Does it cost frame rate?**
Little: structures are only recalculated when something changes, the heavy part runs on a worker
thread, and the rest is limited to 1.5 ms per frame (`Performance.FrameBudgetMs`).

**Does it work in multiplayer and on dedicated servers?**
Yes. The server and every player need Fysik, the same version; see **Server admins**, which also
shows how to check with `fysik hash` that two players calculate a structure the same way.

**Which mods conflict with it?**
Mods that change structural integrity, such as NoBuildIntegrity, Forever Build or Balrond Better
Build.

**A build behaves strangely. How do I report it?**
Aim at a piece of it with the hammer and press **Ctrl+F8**: it writes the whole structure to
`BepInEx/fysik-dump.txt`. Open an issue at <https://github.com/menezesbruno/Fysik/issues> with that
file, a screenshot and what you expected. Builds from players are how the materials get calibrated,
so they are very welcome.
