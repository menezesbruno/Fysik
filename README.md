![Fysik: structural physics for Valheim](art/banner.png)

# Fysik

**Structural physics for Valheim building.** Fysik ("physics" in Swedish and Danish) replaces Valheim's
vanilla structural integrity, where a piece only cares about its distance to the ground, with a force
calculation in which the **shape** of a build matters: arches work in compression, triangulated
trusses stay rigid, cantilevers bend. Overloaded pieces crack, shake and creak for a few seconds before
they fall, so there is time to shore them up.

Gameplay references: Poly Bridge, Bridge Constructor.

Players: installation, settings and a **building guide** with worked examples are in the
[package README](package/README.md#building-guide).

> **Status: early access.** Fysik computes the forces, shows them (hammer colors, X-ray view,
> placement preview) and decides what falls: overloaded pieces crack, then fall. Material values are
> still being calibrated.

## How it works

- **Solver:** linear 3D frame analysis (K·u = F, sparse stiffness matrix, conjugate gradient) on each
  connected island of up to `Structure.MaxFrameBodies` pieces (2000 by default, up to 5000); a
  simplified load-path model above that. Each piece is a
  rigid body with 6 degrees of freedom; each joint is an elastic connection whose stiffness comes
  from the two pieces' cross-sections between their centres and the contact point, each segment
  treated as a Timoshenko cantilever (stretching, shear, bending and twisting, with the rotation a
  sideways push causes). The contact point is the centre of the overlap between the two pieces.
  Stresses are then checked on cuts across every piece, so a beam resting on a post in the middle
  still sees its own bending.
- **Connections:** every pair of touching pieces is a rigid joint. Walls and floors are approximated
  as equivalent beams. Loads: self-weight, plus what rests on a piece. Pieces the game tags as
  structure (building, floor, wall, roof, architecture, stairs, doors, stacks) weigh their volume
  times their material's density; pieces it tags as objects (furniture, crafting, lighting, decor,
  storage, transport, defense, food) weigh what they are made of, since their colliders are not solid
  material: 4 kg per piece of wood, 10 kg per stone, 4 kg per metal bar, 0.1 kg per nail, and 2 kg per
  unit of item weight for anything else (server settings in the `Weight` section). Simple buckling:
  slender pieces get a lower compression limit. Pieces that hold nothing up (chests, workbenches,
  torches, stone and wood piles) weigh their materials too, as a load shared among the pieces they
  rest on (the ground takes its share when they also touch it) and spread over each of those pieces;
  as in vanilla, they need a structural piece or the ground to rest on, and fall without one.
- **World buildings:** buildings that come with the world (stone towers, abandoned houses, villages,
  ruins: every piece no player placed) do not get Fysik's physics. They keep vanilla support, are
  shown in neutral grey, and a player build resting on them treats them as fixed ground; only player
  builds are simulated directly. The server setting `Structure.WorldBuildings = Physics` simulates
  them too, with the normal stress colors; many were not designed for it and will fall.
- **Materials:** wood < core wood < stone / iron. Stone is strong in compression with moderate tension.
  Unknown materials from other mods get safe defaults and a log warning.
- **Multiplayer:** only players calculate. Whoever owns a piece (the player near it, as in vanilla)
  solves the whole structure and decides its fate; the calculation is deterministic, so owners of
  different parts of one structure agree, and the crack is synced through the piece's ZDO. The
  dedicated server calculates nothing: it relays, syncs the settings (Jötunn) and hands the pieces
  near the world centre, which it would otherwise keep, to the nearest player. Every player must have
  the mod (`CompatibilityLevel.EveryoneMustHaveMod`).
- **Performance:** recalculation only on events (piece placed, removed or damaged, an object placed on
  it or taken away, a weight or material setting changed), only on the affected island. Reading
  contacts and building the model stay within ~1–2 ms per frame; the solve itself runs on a worker
  thread (a 2000-piece island in about half a second).

## Roadmap

Next:

- Masonry: stone blocks that only hold by pressing on each other, interlocking walls, arches built on wooden centering
- Loads beyond self-weight: chest contents, snow on roofs

Later:

- Calibration with player builds (feedback welcome)
- Faster recalculation for very large bases
- Translations into every language Valheim supports
- A website to plan and test a build in the browser with the same solver

## Building from source

Requirements: Windows, Valheim, [r2modman](https://thunderstore.io/c/valheim/p/ebkr/r2modman/) with a
profile that has BepInExPack_Valheim and Jötunn, and the .NET SDK (any recent version; the project
targets .NET Framework 4.6.2 through reference assemblies).

1. Copy `Environment.props.example` to `Environment.props` and set:
   - `ValheimDir`: the game folder (contains `valheim_Data\Managed`).
   - `DevProfileDir`: an r2modman profile **dedicated to testing** (e.g. `Fysik-Dev`). It provides the
     BepInEx and Jötunn references and receives Debug builds. The build refuses to deploy to a profile
     named `Default`.
2. Build:

   ```sh
   dotnet build -c Debug                                   # builds and copies Fysik.dll + .pdb to <profile>/BepInEx/plugins/Fysik
   dotnet test tests/Fysik.Tests                           # solver tests (no game needed)
   dotnet build src/Fysik/Fysik.csproj -c Release -t:Package   # creates dist/Fysik-<version>.zip for Thunderstore
   ```

3. Start the game through the test profile (r2modman: select the profile, then *Start modded*) and
   look for `Fysik <version> loaded` in `<profile>/BepInEx/LogOutput.log`.

The version lives only in `src/Fysik/Fysik.csproj` (`<Version>`); it feeds the plugin attribute
(through `BepInEx.PluginInfoProps`) and the Thunderstore `manifest.json`. `assembly_valheim` is
publicized at build time (`BepInEx.AssemblyPublicizer.MSBuild`), so private game members can be used
directly. Game and third-party DLLs are referenced in place and never committed.

### Repository layout

| Path | Contents |
|---|---|
| `src/Fysik/Structure/` | The solver: pure C#, no Unity types, shared with the tests |
| `src/Fysik/Game/` | Valheim side: piece graph, contacts, display, patches |
| `tests/Fysik.Tests/` | Solver tests against beam theory (cantilever, fixed beam, column, arch, brace, floor on a beam, contact overlap, weight held, squeeze along a piece, performance) |
| `package/` | Thunderstore `manifest.json` template and the package README |
| `art/` | `icon.png` and `banner.png` |
| `docs/images/` | The building guide's pictures |

### Releasing on Thunderstore

Run the `Package` target and upload `dist/Fysik-<version>.zip`. Select the categories **Building** and
**Mods**. Update `CHANGELOG.md` before each release.

## License

[MIT](LICENSE)
