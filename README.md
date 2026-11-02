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

The game opens on the **ranch**. **Tab** switches to the **Sprite Lab** and back. **N** mutes the
sound and music (`--mute` starts muted, `--nomusic` plays without music).

**Music** is made in code like everything else: a flute and harp at the ranch, a marching horn on
the map, and battle music by whose ground you fight on (a brass fanfare in the city above, neon saw
bass in the undercity, a driving pulse in the wilds, something faster for rival tamers and the
coliseum). `--music` writes every track to `.shots/music/`.

### Ranch and breeding

Your roster, saved to `user://ranch.json`. A new ranch starts with four creatures from the
DrawPets cast. Creatures you recruit in battle move in.

| Input | Does |
|---|---|
| Click a creature | select it. Its panel sets squad, class and secondary skills |
| J | the selected creature's job tree: classes, secondary, passive |
| S | add the selected creature to or take it out of the squad (up to 4) |
| Right-click a creature | put it in a breeding vat (two vats) |
| Enter | breed the pair in the vats |
| L | open the lab (grafting) |
| M (or F) | the mission board; F again departs with the selected contract |
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

### Jobs: classes, skills and multiclassing

Classes are training disciplines, like FFTA's jobs. There are **35**, and every creature has its
own **job tree**, shaped by its body, its parts and its blood. Press **J** at the ranch to see the
selected creature's tree: every class is marked open, locked (with what it still needs and how to
get it), or never possible for that body.

**Learning.** Creatures earn JP for their current class in each battle, more if they're still
standing and more for a win. They learn the class's three skills in order, then its **passives**: a support passive, and for every base class and several advanced ones a **reaction**. Training on past everything earns **mastery** (stars: * skills, ** passives, *** mastered): a permanent +1 to the class's strongest stat (or +5 HP), kept whatever the creature trains as next. Mastering many classes, FFTA-style, makes a creature strong.

**Multiclassing (FFTA-style).** In battle a creature can use:
- its **class's** learned skills,
- the learned skills of one **secondary** class it has trained in,
- its **innate** skill (inherited from a parent, or kept from the wild),
- one equipped **support** passive and one equipped **reaction**, each from any class it has trained.

Its attitude stays as its innate reaction.

**Affinities.** Each body plan has favoured classes (marked with \*, +50% JP and stronger stat
leanings) and a few barred ones. **Tinker** needs hands: a biped, or claws.

| Body plan | Favoured | Barred |
|---|---|---|
| Quadruped | Brawler, Skirmisher, Howler | - |
| Biped | Guardian, Tinker, Rogue | - |
| Serpent | Mystic, Alchemist | Guardian |
| Avian | Skirmisher, Mystic, Hunter | Guardian |
| Insectoid | Skirmisher, Alchemist, Hunter | Mystic |
| Aquatic | Mystic, Guardian | Skirmisher, Rogue |
| Blob | Guardian, Alchemist | Skirmisher, Hunter |

**Base classes** (open to any body that allows them):

| Class | Skills | Passive |
|---|---|---|
| Brawler | Heavy Blow, Rend, Frenzy | Brute Force (ATK +2) |
| Skirmisher | Quick Strike, Pounce, Evasion | Fleet (MOVE +1) |
| Guardian | Shield Wall, Taunt, Second Wind | Thick Hide |
| Mystic | Hex Bolt, Ward, Arc Storm | Focus (faster cooldowns) |
| Alchemist | Toxic Flask, Mend, Acid Splash | Antidote (no poison) |
| Tinker | Overclock, Snare, Shock Prod | Tuned Reflexes |
| Hunter | Quill Shot, Pin Down, Volley | Eagle Eye (+1 range) |
| Howler | War Cry, Screech, Lullaby | Echo (boosts last longer) |
| Rogue | Backstab, Smoke Bomb, Pilfer | Pickpocket (+chem) |

**Part classes** (master two base classes, and have the body for it):

| Class | Needs | Skills |
|---|---|---|
| Skydiver | Brawler + Skirmisher, wings | Dive Bomb, Gale, Updraft |
| Juggernaut | Brawler + Guardian, horns | Trample, Iron Hide, Earthshaker |
| Bulwark | Guardian + Alchemist, a shell | Shell Up, Spiked Rampart, Haul |
| Lasher | Brawler + Tinker, a whip tail | Crack, Snare Lash, Whirl |
| Stalker | Rogue + Alchemist, claws | Ambush, Venom Claws, Shadow Step |
| Leviathan | Guardian + Mystic, a swimmer's body | Tidal Crash, Undertow, Deep Shell |
| Titan | Brawler + Guardian, huge size | Stomp, Shrug Off, Toss |
| Trickster | Skirmisher + Mystic, tiny size | Blink, Vanish, Hex Prank |
| Packleader | Howler + Brawler, loyal or aggressive | Rally, Pack Tactics, Alpha Howl |
| Deadeye | Hunter + Tinker, a lens visor or extra eyes | Headshot, Mark, Ricochet |
| Thornstag | Guardian + Skirmisher, antlers | Antler Sweep, Crown of Thorns, Rutting Charge |
| Lionheart | Howler + Guardian, a mane | Roar of Courage, Challenge Roar, Last Stand |
| Geomancer | Mystic + Guardian, back crystals | Crystal Lance, Prism Ward, Shardstorm |
| Bonebreaker | Brawler + Rogue, a club tail | Crushing Blow, Tail Sweep, Skull Crack |
| Swarmlord | Alchemist + Hunter, insect wings | Sting Volley, Drone, Hive Mind |
| Seer | Mystic + Hunter, compound eyes | Foresight, True Sight, Omen Bolt |
| Sunsail | Skirmisher + Howler, a back sail | Tailwind, Heat Shimmer, Solar Flare |
| Cavalier | Skirmisher + Guardian, hooves | Lance Charge, Stampede, Steadfast |
| Scavenger | Rogue + Hunter, curious | Snatch, Scrounge, Junk Toss |
| Phantom | Skirmisher + Rogue, skittish | Fade, Feint, Ghost Step |
| Champion | Brawler + Howler, proud | Challenge, Duel Stance, Finishing Blow |
| Hexwarden | Mystic + Tinker | Hex Cage, Overcharge, Nova |

**Reactions** (FFTA's R-abilities) fire when the creature is attacked: Counter (Brawler), Sidestep (Skirmisher), Bodyguard (Guardian), Spell Ward (Mystic), Adrenal Gland (Alchemist), Shock Plating (Tinker), Return Fire (Hunter), Rallying Cry (Howler), Dirty Retort (Rogue), Protector (Lionheart), Shard Burst (Geomancer), Premonition (Seer), Brace (Cavalier), Blur (Phantom), Riposte (Champion).

**Blood classes** (master one base class, and carry the strain):

| Class | Needs | Skills |
|---|---|---|
| Chemist | Alchemist, chemtech blood | Shimmer Dose, Caustic Cloud, Mutagen |
| Runeweaver | Mystic, arcane blood | Glyph Snare, Glyph Shield, Arcane Surge |
| Voidcaller | Brawler, abyssal blood | Dread, Siphon, Maw |
| Artificer | Tinker, two machine grafts (augments count) | Spark Coil, Repair, Overdrive |

The gates are why you breed and visit the lab:
- Breed wings, a shell or huge size into a line.
- Graft claws to open Tinker, or fit a visor to open Deadeye.
- Infuse blood to open a blood class.

Wild creatures fight with a natural skill from their strain: chemtech beasts throw toxic flasks,
arcane ones cast hex bolts, clockwork ones carry shock prods, abyssal ones rend, and aggressive
natural ones swing heavy blows.

### The lab

Press **L** at the ranch (or the Lab button). The lab changes parts without breeding, at a price
in **chem**. You earn chem each battle: 8 for a win, 2 for a loss, 2 for every wild creature driven
off, and 5 for each creature you release.
- **Flesh grafts:** swap a part slot (horns, wings, shell...) for any variant in your **codex**,
  the parts you've met on creatures you fought or recruited. Wings cost the most (40), since they
  give flight and open Skydiver.
- **Augments:** bought at undercity shops and fitted here, two to a creature, never inherited.
  Four mechanical (Piston Fist, Rivet Gun, Lens Visor, Hextech Core), four chemical (Acid
  Canister, Chem Veins, Strobe Tips, Drip Feed) and four steamwork (Gear Grinder, Steam Pistons,
  Smokestack, Pressure Pipes); each bolts on its brass, neon or steam work and teaches an attack.
  Clockwork creatures are often born with steamwork of their own: turning cogs, pumping pistons,
  a steaming stack, copper pipes and a pressure gauge.
- **Traits** aren't made here: creatures are born with them (Keen Nose, Quick Healer, Stout...) and
  pass them down when bred.
- **Infusions:** chemtech, arcane or abyssal blood for a natural creature.

The graft chamber previews the creature with the parts you're considering. Flesh and machine
grafts belong to that one creature: breeding passes on the parts it was born with. Infusions are
blood, so the line carries them.

### Mission board and regions

Press **M** at the ranch for the **country map**: the city above, the undercity below, the
wilds, and your ranch between them. Pick a place to see what's on offer there, then **Depart
(F)**: your party travels the roads (a day each) and the battle starts. Contracts change after
every battle. Locked places open through **main missions**, which only appear once you've done
enough elsewhere (contracts won, creatures raised, codex parts found, rivals beaten). Each card
shows its map, difficulty, chem reward and objective:

| Objective | To succeed |
|---|---|
| Rout | drive off or recruit every wild creature |
| Capture | recruit the marked creature before it gets away |
| Hunt | bring down the marked alpha (one size bigger than the rest of its pack) |
| Hold out | still have someone standing after N rounds against a big pack |
| Duel | beat a rival tamer's trained squad, matched to yours, within 20 rounds (no recruiting); lose and they grow cocky, bringing one creature fewer next time, then two |
| Regular job | each place's standing job, always on offer (a rout, a hunt or a hold-out), for a little less pay |
| Cup | at the Grand Coliseum: three duels in a row against named contenders; take the cup for a big prize |
| Dispatch | no battle: send one creature off on a pun-titled job for a few days; the better it suits, the likelier it comes back with chem |

**Shops** at most places sell **gear** (one piece per creature: a collar, a scarf, knuckle caps,
a rebreather...) and **items** for battle: snare nets to catch a worn-down wild creature,
soothing chimes to stop one moving, tonics, smelling salts and extra glowberries. Each place
sells its own stock, priced in chem; you buy where you stand (S on the map). Put gear on at the
ranch (Gear); items appear in the battle's command bar with their counts.

**Standing.** The city above and the undercity hate each other, and both notice who you work
for: contracts at a city place raise your standing with the city, at an undercity place with the
undercity; fitting augments pleases the undercity, breeding the city. After each main mission you
choose who gets the credit. Where your standing leans decides which of three endings opens:
justice for the undercity, chaos in the city above, or peace between them. Each path also has a
rare creature that's only rumoured to those who walk it. Both standings show on the map and the
ranch; at 50 a side's shops take 15% off.

**Roaming packs** wander the country, a few at a time, moving on most days. Each shows on the
map as its leader. Travel goes a road a day, and arriving where a pack is means fighting it first;
or go after one on purpose for its creatures.

**Rival tamers** challenge you on some boards: the Brass Baron, the Chem Witch, the Huntress,
the Occultist and the Wyrm Lord, each breeding toward their own ideal creature and leading it with
a boost of their own. Every win against one makes their next squad train harder.

Some cards carry a **sighting**: a part your codex lacks (wings, horns, a shell...), promised on a
creature in that pack. Meeting it adds the part to the codex for the lab, and recruiting it
brings the part into your breeding lines.

Twelve places, each with its own look and its own creatures, among them:
- **Undercity Foundry:** cobbles, brass platforms, a chem canal. Anything turns up.
- **Brass Bridges:** marble plazas over a chasm in the city above. Birds and clockwork things.
- **Sump Marsh:** islands in glowing chem. Swimmers, serpents, blobs, chemtech mutants.
- **Clocktower Ruins:** steep tiers where the high ground decides fights. Bipeds and beasts.
- **Hexlight Hollow:** a crystal-lit cavern. Insects, arcane and abyssal creatures.
- **Frostfang Pass:** snow and blue rock round a frozen lake. Frost creatures, birds and beasts.
- **Sunscorch Dunes:** sand that slows walkers, sandstone ruins, an oasis. Serpents and insects.
- **Cinder Rift:** ash and basalt over lava that only fliers cross. Ember and abyssal things.
- **Saltwind Coast:** a beach, two piers and the sea. Swimmers and gulls.
- **The Grand Coliseum:** the city's arena, where the cups are fought.

### Battle

Your squad plays out a contract on an isometric map with height. Turns go in Speed order
(FFTA style). The objective and any target (a gold diamond) show at the top. When it ends, Enter
takes you back to the ranch, where your recruits, chem and JP are waiting.

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
- `--autoboard=N` plays every mission on boards 1-N AI against AI, one line each (catches stalls).
- `--skilltest` fires every skill once by hand, proves every reaction directly, and lets the AI use all skills for 20 battles.
- `--ranchtest` runs fights, recruiting and breeding headless, then checks the save file round trip.
- `--breeding=N` writes `.shots/breeding.png`: pairs of parents and four possible children each.
- `--autoplay` loops ranch and battle on its own (an attract mode).
- `--save=NAME` uses a separate save file.
- `--battle=N --map="Sump Marsh"` runs a one-off battle without the ranch.
- `--shot=N` saves frame N to `.shots/shot.png`.
- `--props` writes every map prop in every region's colours.
- `--export` renders a sheet without a window; `--forms` one creature of every form, front and
  back on board tiles (`--only=Lung,Jelly` for some, `--idle=NAME` for their idle loop as
  frames); `--views=N` sheet N the same way; `--icons` every skill icon.

The full list is in `scripts/GameRoot.cs`.

## Layout

- `scripts/Creatures/`: the generator, with no UI. `Creature.cs` (species, individuals,
  strains and grafts, gear, forms and auras), `CreatureSculptor.cs` and `CreatureForms.cs`
  (each creature as a small 3D model seen from FFTA's camera: forms, parts, faces, grafts,
  element themes, auras), `PixelCanvas.cs` and `PixelCanvas3D.cs` (ray-casting, shading,
  seams, outlines, rim light), `CreaturePalette.cs`,
  `CreatureNames.cs`, `DrawPetsCast.cs`.
- `scripts/Tactics/`: battles. `Battle.cs` (rules, recruiting, attitude AI, with no drawing),
  `Unit.cs` (stats from parts), `BattleMap.cs`, `Encounter.cs`, `IsoArt.cs` (pixel-art board and
  backdrop), `BattleScreen.cs`.
- `scripts/Creatures/Breeding.cs`: inheritance rules.
- `scripts/Classes/Classes.cs`: every class, skill and passive as data; affinities; training (JP, learning, unlocks).
- `scripts/Ranch/JobsPanel.cs`: the job tree screen.
- `scripts/UI/`: glyphs, skill icons and the glyph-and-number shorthand used on every screen.
- `docs/classes-design.md`: the job-tree design and its open questions.
- `scripts/Ranch/`: the roster and save file (`Ranch.cs`) and the ranch screen with its vats.
- `scripts/SpriteLab/`: the sheet screen and the PNG composer.
- `scripts/Ranch/Lab.cs`, `LabPanel.cs`: grafting rules and the lab panel.
- `scripts/Tactics/Mission.cs`, `scripts/Ranch/MissionBoardPanel.cs`: contracts and the board.
- `docs/game-ideas.md`: the design notes.
- `docs/story-notes.md`: the planned story track (FFTA-style story, cutscenes, animation), with
  open questions. Not started.
