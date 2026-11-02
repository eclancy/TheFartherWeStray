# Creature Tactics

An FFTA-style isometric tactics game where your units are procedurally generated creatures you
recruit, breed and train. A creature's body plan works like an FFTA race, its parts are its
equipment (horns are the weapon, a shell is the armour, wings are the movement accessory), and
its attitude is both its reaction ability and how you recruit it in battle.

The design lives in [docs/game-ideas.md](docs/game-ideas.md). Section 4, "Creature Tactics",
is the game, and sections 1-2 add detail.

Godot 4.5 (.NET), C#.

## Art direction

Medieval high fantasy with steampunk and neonpunk, after Arcane (Piltover brass above, Zaun neon
below). Monsters are drawn in the spirit of FFTA and Fell Seal: lean and predatory, with small
glowing eyes, fangs, earthy palettes and dark outlines. The tone can be dark and scary, balanced
by plenty of natural beasts. Every species has a **strain**:

| Strain | Share | Look |
|---|---|---|
| Natural | about half | earthy beasts, no glow |
| Clockwork | | brass plating, a metal limb, a lens visor, a hextech core |
| Chemtech | | a canister of neon chem on its back, glowing veins, dripping ooze |
| Arcane | | glowing runes, burning horn and spike tips |
| Abyssal | | near-black body, extra eyes, glowing cracks |

Every strain except natural gets a neon rim light and glow spill.

## Run it

```bash
"$GODOT_BIN" --path .
```

`GODOT_BIN` is set in `.claude/settings.json` (Godot 4.5.1 mono). Build C# with
`dotnet build CreatureTactics.sln`. On a fresh checkout, run `"$GODOT_BIN" --headless --path . --import` once.

The game opens on the **ranch**. **Tab** switches to the **Sprite Lab** and back.

### Ranch and breeding

Your roster, saved to `user://ranch.json`. A new ranch starts with four creatures from the
DrawPets cast. Creatures you recruit in battle move in.

| Input | Does |
|---|---|
| Click a creature | select it. Its panel sets squad, class and secondary skills |
| S | add the selected creature to or take it out of the squad (up to 4) |
| Right-click a creature | put it in a breeding vat (two vats) |
| Enter | breed the pair in the vats |
| F | take the squad into the next battle |
| Delete twice | release the hovered creature |

Each breeding gives one child, a new species of its own. Parts are the gear, so you breed toward
the creature you want:
- **Parts:** the body plan and each part slot come whole from one parent or the other, including
  a parent's own mutations, with a small chance of a fresh mutation.
- **Colour:** it comes from either parent, sometimes blended.
- **Size and proportions:** they land between the parents'.
- **Strain:** blood strains (arcane, chemtech, abyssal) are inherited. Clockwork grafts are
  machinery, so they aren't.
- **Names:** spliced from both parents.

Parents and newborns rest until the next battle, and a battle rests everyone. Hover a creature
for its lineage, parts, gear and stats.

### Classes and skills

Classes are training disciplines, as FFTA jobs are: classes teach skills, and parts (the gear)
modify them. Each class leans the stats a little and teaches three skills. Creatures earn JP
for their class in every battle (more if they're still standing, and more for a win) and learn
the class's skills in order. In battle a creature can use its class's learned skills, the learned
skills of one **secondary** class, and its **innate** skill.

| Class | Teaches |
|---|---|
| Brawler | Heavy Blow, Rend (bleed), Frenzy (ATK up) |
| Skirmisher | Quick Strike (hit and run), Pounce (leap), Evasion |
| Guardian | Shield Wall, Taunt, Second Wind |
| Mystic (hextech) | Hex Bolt (range 4), Ward, Arc Storm (area) |
| Alchemist (chemtech) | Toxic Flask (poison), Mend (heal), Acid Splash (area, DEF down) |
| Tinker (clockwork) | Overclock (SPD and MOVE up), Snare (root), Shock Prod (stun) |
| **Skydiver** | 2 Brawler and 2 Skirmisher skills, **and wings**: Dive Bomb, Gale, Updraft |
| **Juggernaut** | 2 Brawler and 2 Guardian skills, **and horns**: Trample, Iron Hide, Earthshaker |
| **Hexwarden** | 2 Mystic and 2 Tinker skills: Hex Cage, Overcharge, Nova |

Locked classes show as hints with progress, like FFTA's silhouettes. The part-gated ones are
why you breed: a creature without wings can't become a Skydiver until wings are bred into its
line. A bred child is born knowing one skill from a parent (its innate skill), and recruits keep
the skill they had in the wild.

Wild creatures fight with a natural skill from their strain: chemtech beasts throw toxic flasks,
arcane ones cast hex bolts, clockwork ones carry shock prods, abyssal ones rend, and aggressive
natural ones swing heavy blows.

### Battle

Your squad faces a wild pack of four on an isometric foundry yard with height, brass platforms,
a chem canal and a pit. Each encounter number brings a different pack. Turns go in Speed order
(FFTA style). When it ends, Enter takes you back to the ranch, where your recruits are waiting.

| Input | Does |
|---|---|
| Click a blue tile | move (passes allies, climbs 1 level; flyers, climbers and swimmers differ) |
| Click a marked enemy | attack. Hover it first for damage and hit chance |
| 1-9 or the skill buttons | aim a skill: orange tiles are in range, gold shows the area, the panel previews the result |
| B, then a green tile | set down a glowberry (lures curious creatures) |
| Esc / right-click | undo the move, if you haven't acted yet |
| Space | end the turn |
| Enter | after a battle: back to the ranch |

Parts become stats: whip tails reach 2 tiles, horns and hooves knock back, spines hurt melee
attackers, shells add defence but cost movement, high ground hits harder, and huge creatures
take 2x2 tiles.

Attitude is a reaction: aggressive creatures counterattack, skittish ones retreat a tile when
hit, and a loyal one shields an adjacent ally once a round. Curious creatures grab items in
reach, and proud ones go after whoever hit them. It also sets how you **recruit** a wild one:

| Attitude | To recruit it |
|---|---|
| Proud | defeat it using only one of your creatures |
| Skittish | corner it so it can't step anywhere |
| Curious | lure it with a glowberry |
| Aggressive | survive three of its attacks without anyone falling |
| Loyal | recruit its packmates first (a placeholder) |

### Sprite Lab

A sheet of seeded creatures to judge the generator. Space rerolls, Left/Right steps through
numbered sheets, clicking a creature shows its species' individuals, D shows the DrawPets cast,
C toggles 4-colour mode, Z zooms, and P saves a PNG to `.shots/`. Hover a creature for its parts,
gear, strain and attitude.

### Command line

```bash
"$GODOT_BIN" --headless --path . -- --autobattle=3
"$GODOT_BIN" --headless --path . -- --ranchtest
"$GODOT_BIN" --headless --path . -- --breeding=1
"$GODOT_BIN" --path . -- --save=demo --newranch --autoplay
"$GODOT_BIN" --headless --path . -- --export=.shots/sheet42.png --sheet=42
```

- `--autobattle=N` plays encounter N AI against AI and prints the log.
- `--ranchtest` runs fights, recruiting and breeding headless, then checks the save file round trip.
- `--breeding=N` writes `.shots/breeding.png`: pairs of parents and four possible children each.
- `--autoplay` loops ranch and battle on its own (an attract mode).
- `--save=NAME` uses a separate save file.
- `--battle=N` runs a one-off battle without the ranch.
- `--shot=N` saves frame N to `.shots/shot.png`.
- `--export` renders a sheet without a window.

The full list is in `scripts/GameRoot.cs`.

## Layout

- `scripts/Creatures/`: the generator, with no UI. `Creature.cs` (species, individuals,
  strains and grafts, gear), `CreaturePainter.cs` (body plans, parts, faces, grafts),
  `PixelCanvas.cs` (shading, seams, outlines, rim light), `CreaturePalette.cs`,
  `CreatureNames.cs`, `DrawPetsCast.cs`.
- `scripts/Tactics/`: battles. `Battle.cs` (rules, recruiting, attitude AI, with no drawing),
  `Unit.cs` (stats from parts), `BattleMap.cs`, `Encounter.cs`, `IsoArt.cs` (pixel-art board and
  backdrop), `BattleScreen.cs`.
- `scripts/Creatures/Breeding.cs`: inheritance rules.
- `scripts/Classes/Classes.cs`: every class and skill as data, training (JP, learning, unlocks).
- `scripts/Ranch/`: the roster and save file (`Ranch.cs`) and the ranch screen with its vats.
- `scripts/SpriteLab/`: the sheet screen and the PNG composer.
- `docs/game-ideas.md`: the design notes.
