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
- Classes and skills (`scripts/Classes/Classes.cs`): six base classes and three advanced ones
  (Skydiver needs wings, Juggernaut horns). JP from battles learns skills in order; a primary,
  a secondary and an innate skill are usable. Skills are pure data (`SkillDef`) read by
  `Battle.UseSkill`, so a new skill should be a new entry, not new code. Wild creatures get a
  strain-based natural skill.
- The lab (`scripts/Ranch/Lab.cs`, `LabPanel.cs`): chem from battles pays for flesh grafts
  (swap a part for one in the codex, the parts met in battle), machine grafts (brass) and
  infusions (blood strains for natural creatures). Flesh and machine grafts are **not inherited**:
  breeding reads `BirthParts`. Infusions are.
- **Story (planned, not started):** the user wants FFTA-depth story, cutscenes and sprite
  animation, and will set the themes first. `docs/story-notes.md` holds the goal, the open
  questions and a suggested first iteration. Don't invent the story's themes; ask.
- Missions (`Tactics/Mission.cs`): a board of four contracts per encounter (Rout, Capture, Hunt,
  Hold out) on five handmade regions (`BattleMap.cs`), each with a look (`Look` in `IsoArt.cs`) and
  fauna odds. Sightings promise a codex-missing part. After touching maps or AI, run
  `--autoboard=6`: no mission may stall.
- Not built yet: story and cutscenes, rival tamers, the AI playing objectives (your side's AI only
  routs, so it fails most captures in headless runs; that's the AI, not the rules). There's no human
  player character on the field (the notes' Option A).

## Build and check

- `dotnet build CreatureTactics.sln` is the fast check.
- **Battle rules:** `"$GODOT_BIN" --headless --path . -- --autobattle=N` prints an AI-vs-AI log.
  After any rules change, run seeds 1-20: every one should end in under 30 rounds. Stalemates have
  come from kiting skittish fliers and from units stuck behind cliffs.
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
