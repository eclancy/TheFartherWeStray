# Creature Tactics

An FFTA-style grid tactics game where your units are procedurally generated creatures you
recruit, breed and train. A creature's body plan works like an FFTA race, its parts are its
equipment (horns are the weapon, a shell is the armour, wings are the movement accessory), and
its attitude is both its reaction ability and how you recruit it in battle.

The design lives in [docs/game-ideas.md](docs/game-ideas.md). Section 4, "Creature Tactics",
is the game, and sections 1-2 add detail.

Godot 4.5 (.NET), C#.

## Status: sprite generator prototype

The notes call the sprite generator the biggest risk ("if generated creatures don't look
charming, nothing else matters"), so it comes first. The game is currently the **Sprite Lab**,
a sheet of seeded creatures to judge.

Each creature is built from a seed:
- **Species**: body plan (quadruped, biped, serpent, avian, insectoid, aquatic, blob), the part
  in every slot (head, ears, eyes, horns, back, limbs, tail, wings, pattern), a palette family,
  proportions and a syllable name.
- **Individual**: size (tiny to huge, where huge is a 2x2 footprint), attitude (aggressive,
  skittish, loyal, curious, proud: it shapes the eyes, mouth and pose), a slight shade drift,
  and rare twists: a shiny palette (1 in 24) or a mutated part (1 in 14).

Sprites are drawn as lit shapes on a small pixel grid: four tones per material with hue-shifted
ramps, part seams, and selective outlines. A medium creature is roughly Gen 3 sized. **C**
switches to a strict 4-colour Game Boy Color palette for comparison.

The **DrawPets cast** (press **D**) is 24 pets from the DrawPets adopt shop, rebuilt as
hand-authored species in the same part system (`scripts/Creatures/DrawPetsCast.cs`).

## Run it

```bash
"$GODOT_BIN" --path .
```

`GODOT_BIN` is set in `.claude/settings.json` (Godot 4.5.1 mono). Build C# with
`dotnet build CreatureTactics.sln`. On a fresh checkout, run `"$GODOT_BIN" --headless --path . --import` once.

| Key | Does |
|---|---|
| Space / R | new random sheet |
| Left / Right | previous / next sheet (sheets are numbered and repeatable) |
| Click a creature | its species: 24 individuals side by side |
| Esc / right-click | back to the sheet |
| D | the DrawPets cast |
| C | full palette / 4-colour |
| Z | zoom 2x / 3x |
| P | save the sheet as a PNG in `.shots/` |
| F11 | fullscreen |

Hover a creature to see its parts and what they mean in battle (weapon, armour, movement,
reaction, recruit condition).

Export without opening a window:

```bash
"$GODOT_BIN" --headless --path . -- --export=.shots/sheet42.png --sheet=42
"$GODOT_BIN" --headless --path . -- --export=.shots/cast.png --drawpets --four
"$GODOT_BIN" --headless --path . -- --export=.shots/pip.png --species=Pip
```

Other options: `--scale=N`, `--columns=N`, `--from=N`, `--count=N`. See `scripts/GameRoot.cs`.

## Layout

- `scripts/Creatures/`: the generator, with no UI. `Creature.cs` (species and individuals),
  `CreaturePainter.cs` (body plans and part shapes), `PixelCanvas.cs` (shading, seams,
  outlines, palettes), `CreaturePalette.cs`, `CreatureNames.cs`, `DrawPetsCast.cs`.
- `scripts/SpriteLab/`: the sheet screen and the PNG composer.
- `docs/game-ideas.md`: the design notes.
