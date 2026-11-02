# CLAUDE.md - Creature Tactics

FFTA-style tactics with procedurally generated creature units. Godot 4.5 .NET, **C#**, 2D, GL
Compatibility, built in code (`scenes/Main.tscn` only attaches `GameRoot`). The design is
`docs/game-ideas.md` (section 4; sections 1-2 for detail). Read it before design decisions.

## Status

The first prototype is the **Sprite Lab** (`scripts/SpriteLab/`), a sheet of seeded creatures,
because the notes name the sprite generator as the biggest risk. No tactics code exists yet.
There is no human player character on the field (the notes' Option A).

## Build and check

- `dotnet build CreatureTactics.sln` is the fast check.
- `"$GODOT_BIN" --headless --path . -- --export=.shots/x.png --sheet=1` renders a sheet to PNG
  with no window. **This is how sprite changes get verified**: export, then look at the PNG
  (use `--scale=5 --columns=4 --count=8` for close-ups). `$GODOT_BIN` is in
  `.claude/settings.json`. If it is unset, say so rather than guessing a path.
- `.shots/` is ignored by git.

## Generator rules

- Everything comes from a seed through `SeededRng` (SplitMix64), never `System.Random` or
  `GD.Randi`, so a sheet number or species seed always draws the same creature.
- Species vs individual (design notes): the species fixes the plan, parts, palette and
  proportions. An individual varies size, attitude, shade, and rarely shiny or mutation.
- Every roll should mean something in battle. `Creature.Loadout()` maps parts to
  weapon/armour/movement; `AttitudeInfo` gives reactions and recruit conditions. The loyal
  recruit condition is a placeholder; the notes list only four.
- Painting is back to front. Part ids increase in paint order, and the seam rule depends on it.
- The DrawPets cast (`DrawPetsCast.cs`) is hand-authored from `../DrawPets/scripts/adoptables.gd`
  and the art in `../DrawPets/.shots/art_adopt_*_assembled.png`.
