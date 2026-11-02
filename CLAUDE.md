# CLAUDE.md - Creature Tactics

FFTA-style isometric tactics with procedurally generated creature units. Godot 4.5 .NET, **C#**,
2D, GL Compatibility, built in code (`scenes/Main.tscn` only attaches `GameRoot`). The design is
`docs/game-ideas.md` (section 4; sections 1-2 for detail). Read it before design decisions.

## Art direction (the user's calls, 2026-11-01)

- **Cool, not charming.** No big eyes, blush or baby-schema heads. Faces are small glowing almond
  eyes under attitude-shaped brows, with fanged jaws.
- **FFTA and Fell Seal: Arbiter's Mark** for monsters and the isometric board.
- **Arcane (League of Legends)**: medieval high fantasy with steampunk (Piltover brass) and
  neonpunk (Zaun chem glow). Creatures can be magical, mechanical, full of glowing liquid, or
  dark and scary, balanced by plenty of natural ones. This is the `Strain` system.
- The current sprite fidelity (medium creature about Gen 3 size, four tones, selective outlines)
  is what the user wants. Don't shrink it.

## Status

- Sprite Lab (`scripts/SpriteLab/`): the generator prototype.
- Battle slice (`scripts/Tactics/`): one handmade map, your squad against a pack seeded by the
  encounter number, speed-order turns, parts as gear, attitude reactions and AI, and recruiting.
- Ranch and breeding (`scripts/Ranch/`, `Creatures/Breeding.cs`): a saved roster, squad picking,
  two-vat breeding (one child per pair; parents rest until the next battle), and the
  ranch-battle-ranch loop. Bred species exist only in the save, so `CreatureRecord` stores the
  whole `Species`.
- Not built yet: classes and skills (the notes' training disciplines, with part-gated unlocks),
  and grafting at the lab. There's no human player character on the field (the notes' Option A).

## Build and check

- `dotnet build CreatureTactics.sln` is the fast check.
- **Battle rules:** `"$GODOT_BIN" --headless --path . -- --autobattle=N` prints an AI-vs-AI log.
  Run a few seeds after any rules change.
- **Ranch and save file:** `--ranchtest` (headless) must report every creature identical after a
  save and load. Run it after touching `Species`, `Creature`, `PaletteSpec` or the painter's
  inputs. `--breeding=N` writes a sheet of parents and children. Use `--save=NAME` for anything
  that saves, so the user's `user://ranch.json` is never touched.
- **Sprites:** `"$GODOT_BIN" --headless --path . -- --export=.shots/x.png --sheet=1`, then look at
  the PNG (`--scale=4 --columns=4 --count=12` for close-ups).
- **Screens:** `"$GODOT_BIN" --path . -- --shot=40` (add `--autoplay` for mid-battle, `--lab` for
  the Lab) writes `.shots/shot.png`. It opens a window briefly.
- `$GODOT_BIN` is in `.claude/settings.json`. If it is unset, say so rather than guessing.
  `.shots/` is ignored by git.

## Rules worth keeping

- Everything is seeded through `SeededRng` (SplitMix64), never `System.Random` or `GD.Randi`.
- `Battle` holds the rules and emits `BattleEvent`s; `BattleScreen` only animates them. Keep
  rules out of the screen so `--autobattle` stays a real test.
- Every roll should mean something in battle. Parts map to `Weapon`/`Armor`/`MoveType`
  (`Creature.cs`), and `Unit.From` turns those and grafts into stats.
- Painting is back to front; part ids increase in paint order, and the seam rule depends on it.
- Root Controls need `SetAnchorsAndOffsetsPreset(FullRect)`. Plain `SetAnchorsPreset` leaves them
  zero-sized under the `Node` root.
- The loyal recruit condition is a placeholder; the notes list only four.
- The DrawPets cast (`DrawPetsCast.cs`) is hand-authored from `../DrawPets/scripts/adoptables.gd`.
