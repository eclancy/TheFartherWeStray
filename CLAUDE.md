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
- **Chosen look: "Neon Undercity"** (2026-11-01), the default `ArtStyle`. The user compared five
  styles (`--style=`, V in the Sprite Lab) and picked this one, as is. Keep the other presets for
  reference, but don't switch the default.
- **Perspective: FFTA's** (2026-11-02, built). Creatures are small 3D models ray-cast into pixels
  (`CreatureSculptor`, `PixelCanvas3D`): camera 30 degrees up a grid diagonal, two views (front
  facing down-left, back facing up-left), mirrored for the other two diagonals. `UnitView` turns
  units as they walk and strike. Sprites come with an anchor (the feet), not a bottom edge.
- **Mystical, fantastical, varied, themed; not plump** (2026-11-02). Lean, distinct silhouettes
  over round bodies; each creature should read as a kind of fantasy beast, not a palette swap.
  Layers: `Form` (30 silhouettes, a few per body plan: hound, stag, drake, hydra, sentinel,
  gargoyle, wraith, wyrm, naga, crane, cockatrice, mantis, scorpion, spider, ray, angler, wisp,
  jelly, watcher...; `LaterFormsFor` holds forms added after the first set, rolled only for a third of
  newly generated species so nothing already seen changes form), element themes (ember flames, frost shards, leaf
  fronds, tide fins, venom frills, shade smoke, spark crackle, meadow tufts), strain grafts, and an
  `Aura` (halo, floating shards, sigil, third eye, wisps). New forms go in `CreatureForms.cs`: build
  the body, set the anchors (head, spine, tail root, wing root); the shared parts do the rest.
  A form that should stay one clean shape sets `_sparse` (the watcher: no ears, back plates or
  tail, smaller grafts kept off its eye). `--forms --only=Watcher --many=6` shows six of a form.
  **Steamwork (2026-11-02, the user's ask for more mech and steampunk):** grafts `Gears` (meshing
  cogs on the flank, turning), `Pipework` (copper pipes and a glowing pressure gauge, its needle
  wavering), `Pistons` (twin pumping pistons at the shoulders) and `Smokestack` (a flared stack
  puffing steam), sculpted in `Steamwork()`. New clockwork species roll up to one flank piece and
  one back piece (`Species.SteamworkFor`, a stream of its own, so older rolls don't change; saved
  species keep their grafts); chemtech ones sometimes pipework. They're machine grafts (not
  inherited) and augments too. `--steampunk` writes `.shots/steampunk*.png` (clockwork creatures,
  each piece's idle with and without it, the starters fitted).
  **The user approved this look as built ("the creatures all look great", 2026-11-02): refine
  and extend it, don't replace it.** Of the eight fixes on 2026-11-02 the user kept seven and
  preferred the original shell: smooth overlapping plates, no dark rims or knobs.
- **Idle animation** (2026-11-02): every frame is a re-render of the model with `PixelCanvas.Warp`
  moving shapes: a breath on planted feet (floaters bob), a head nod a beat behind, tails and
  serpent bodies swaying in a wave, wingbeats, floating auras, flickering flames, a blink every
  fourth breath. 8 frames at 8 fps; `UnitView` renders them on a worker thread and swaps them in.
  New forms' trailing parts (tatters, plumes, tendrils) want a `Trail` warp.
- **Action poses** (2026-11-02): `Pose` Walk, Strike, Cast, Hurt, Faint, made the same way
  (`BodyWarp` per pose, `LegWarp` for steps). `UnitView` renders every pose for both views in the
  background and plays them: walk cycles while moving, a wind-up and lunge for attacks and melee
  skills, a rear-up for other skills, a flinch on a hit, a collapse before fading.
  `--forms --idle=NAME --actions` writes the whole reel as frames.
- **Readability (2026-11-01):** icons and shorthand over sentences, after WizardSurvivors'
  stat glyphs. `scripts/UI/`: `Glyphs` (12x12 art, outlined, drawn at 2x+), `SkillIcons` (every
  skill gets a framed glyph: frame colour = attack/heal/boost/control/move, corner marks = shape and
  stat up/down), `Ui.Chips` (glyph + number). A glyph before every stat; full text only in
  tooltips; colour only as a second cue. New skills need an entry in `SkillIcons.SkillGlyph`;
  check `--icons` (writes `.shots/icons.png`).

## Status

- Sprite Lab (`scripts/SpriteLab/`): the generator prototype.
- Battle slice (`scripts/Tactics/`): one handmade map, your squad against a pack seeded by the
  encounter number, speed-order turns, parts as gear, attitude reactions and AI, and recruiting.
- Ranch and breeding (`scripts/Ranch/`, `Creatures/Breeding.cs`): a saved roster, squad picking,
  two-vat breeding (one child per pair; parents rest until the next battle), and the
  ranch-battle-ranch loop. Bred species exist only in the save, so `CreatureRecord` stores the
  whole `Species`.
- Jobs (`scripts/Classes/Classes.cs`, `docs/classes-design.md`): 44 classes (9 base, 31 part,
  form, aura or temperament, 4 blood; the form and aura classes need a `Form` such as the hydra or
  an `Aura` such as a halo), each with three skills and passives (a support, often a reaction too),
  mastery stars, and support + reaction slots. Every creature has its own job tree from its body
  plan (favoured and barred classes), parts, size, temperament and strain. FFTA multiclassing: main
  class skills, one secondary class's learned skills, an innate skill, one equipped passive.
  Skills and passives are pure data (`SkillDef`, `PassiveDef`) read by `Battle`, so new ones
  should be new entries, not new code. **The user wants many class options: keep adding.**
  After changing skills or reactions, run `--skilltest`: every skill must fire and every reaction be proven. The AI uses only some of them;
  that's AI tuning, not a bug.
- The lab (`scripts/Ranch/Lab.cs`, `LabPanel.cs`): chem from battles pays for flesh grafts
  (swap a part for one in the codex, the parts met in battle), augments from the bag (below) and
  infusions (blood strains for natural creatures). Flesh grafts and augments are **not inherited**:
  breeding reads `BirthParts`. Infusions are.
- **Story (planned, not started):** the user wants FFTA-depth story, cutscenes and sprite
  animation. The user set the themes on 2026-11-02 (lighthearted to start; a glorious city and a
  nefarious undercity that hate each other; branching by alignment, solutions and dialogue;
  three endings: justice for the undercity, chaos for the city, or peace). `docs/story-notes.md`
  holds them and the open questions. Don't invent beyond them; ask.
- Missions (`Tactics/Mission.cs`): a board of four contracts per encounter (Rout, Capture, Hunt,
  Hold out) on twelve handmade maps (`BattleMap.cs`, `BattleMapsFar.cs`, about 16x13 since 2026-11-02 when the user
  asked for larger maps), each with a look (`Look` in `IsoArt.cs`) and fauna odds. Maps have three
  grids: terrain (now with roofs, boards, dirt and water), heights, and props. Buildings are roof
  tiles on house-front walls (FFTA towns: fliers and climbers stand on roofs). Props
  (`Props.cs`: 22 kinds, trees to neon signs, built like creatures and coloured per region) block
  their tile except reeds and banners; fliers pass over them. Small ground details (tufts, cracks,
  fungus) are painted per tile. `--props` writes every prop in every region's colours. Sightings promise a codex-missing part. After touching maps or AI, run
  `--autoboard=6`: no mission may stall. Your side's AI plays the objective (`PlayerPlan` in
  `Battle.cs`): hunts focus the alpha, hold-outs keep together and out of reach, captures never
  land the knockout and play to the target's temper (a champion duels the proud, the squad rings
  the skittish, a glowberry lures the curious, the sturdy stand up to the aggressive while anyone
  its blow could fell keeps clear; for a loyal target, `Mark` first wins over its easiest
  packmate). Creatures whose thorns or counters could fell the target keep out of its reach
  (`Avoid`). `--capturetest=10` plays a capture with the target set to each temper: on
  2026-11-02 proud 8/10, skittish 7, curious 9, aggressive 6, loyal 2, no stalls. Loyal stays low
  because the target fights at full strength while you may not touch it (the rule is a
  placeholder). `--trace=TITLE` prints the full log of matching missions (`--trace=8Loyal` in
  `--capturetest`).
- **Tamers and the party (the user's direction, 2026-11-02, built):** the player leads creatures
  as a tamer (the user's name, 2026-11-02: friendlier than "general" or the working name "warden";
  someone who collects and works with creatures, like a Pokémon trainer).
  Tamers stay off the field and give passive boosts; each leads three creatures; at most two
  tamers and six creatures per battle, swapped between battles. `scripts/Party/Tamers.cs` holds
  them as data (a specialty boost, which creatures it reaches, the encounter they join at: a
  placeholder for the story); `Ranch.Fielded`/`LedBy` save who leads whom; `Battle.Lead` applies
  the boost before the first turn; the Party panel (P) on the ranch fields tamers and fills slots. Details, defaults and open
  questions are in `docs/design-notes.md`, with PvP (casual, turn timers, your own squad, one
  shiny, basic matchmaking planned) and shinies (1 in 512; the battle opens with a camera pan and
  a glint on each one; each has a unique second passive, `ClassBook.ShinyPassives`, picked by its
  seed and merged into `Unit.Passive`; `--shinies` checks them all).
- **Rival tamers (2026-11-02, built):** `scripts/Party/Rivals.cs` holds five rivals as data (a
  `TamerDef` boost, an ideal they breed toward, a taste in species, a blood infusion, machine
  grafts, a class order, home maps; the titles are placeholders for the story). A duel
  (`Objective.Duel`, `Mission.Duel`) takes the board's last card on a rival's debut and on every
  other board after. They field as many creatures as you bring (three, up to six once a second
  tamer joins), trained by `RivalBook.Train` a little behind you (`JpPerEncounter`, never past
  your squad's average JP); each win you have against them (`Ranch.Rivals`, saved) trains them
  harder and fits their grafts to one more creature. **Eased 2026-11-02 (the user's pick):** each
  duel lost to a rival in a row makes them cocky: they bring one creature fewer, then two
  (`RivalBook.Fewer`, `Ranch.RivalLosses`, saved; never under two; a win resets it). Their
  creatures are `Side.Wild` with a `Tamer`, so they fight as trained and can't be recruited: use
  `Unit.Free` for anything about wild tempers or recruiting. A duel still undecided after
  `Battle.DuelRounds` (20) goes to the side with the larger share of its HP left. `--rivals=N`
  plays each rival at their first three board appearances against starters trained to match,
  straight and after one and two losses: on 2026-11-02, after easing, you win 43% straight, 69%
  after a loss, 91% after two (the Wyrm Lord hardest, its third meeting 2/10 straight), no
  stalls. Rivals win by build (casters with Hex Bolt, hunters with Volley and Return Fire), not
  numbers. `--battle=S --duel=N [--enc=E --wins=W --losses=L]` opens one on screen;
  `--save=X --newranch --encounter=6 [--losses=L] --boardmode --shot=40` shows a board with a duel
  card (`--encounter` jumps a ranch ahead).
- `Battle.Place` only puts a unit where it can walk toward the far side (`Reaches`): a huge creature
  used to spawn shut in a pit on the Undercity Foundry. Capture berries go only where the lured
  creature can get next to them (`CanFetch`), and the squad stands aside off its way.
- **The country map (2026-11-02, built):** `scripts/World/`. `Country` holds the places (each a
  battle map and a side: city above, undercity, wilds; the ranch is one), the roads (a day each)
  and what's on offer (`Country.Board`: contracts at every open place, a rival's duel, ready main
  missions). `StoryBook` holds the main missions: each opens a place and waits for the one before
  plus milestones (contracts won, creatures, codex parts, rival wins), so the story can't be
  rushed. `CountryScreen` (M at the ranch, `--mapmode`) shows it all and travels before a battle.
  `Ranch` saves `Location`, `Day`, `ContractsWon`, `Story`. `Mission.Board` is still the test
  board for `--autoboard`. `--country=N` plays the loop headless (it retrains its squad as a player
  would, tries a cup at most every fourth battle, and takes `--save`); run it after touching missions,
  milestones or the AI: no battle may stall.
- **Mission variety (2026-11-02, built):** twelve maps now. The far regions (`BattleMapsFar.cs`:
  Frostfang Pass, Sunscorch Dunes, Cinder Rift, Saltwind Coast, the Grand Coliseum) bring terrains
  `Ice`, `Sand` (walkers pay 2) and `Lava` (fliers only), and looks Frost, Desert, Volcanic, Coast
  (ground details per theme in `IsoArt`, prop colours in `Props`). Main missions branch (`After`).
  `World/Jobs.cs` holds the dispatches (pun jobs as data: who suits them, days, chem, the funny
  line either way; `Ranch.Send`/`Away`/`ReturnDispatches`) and the coliseum's cups (`CupBook`:
  three duels in a row with named contenders; `GameRoot` chains the rounds; `Ranch.Cups`). Every
  place has a regular job (`LocationDef.Regular`, `Mission.Regular`). A battle takes a day.
  `--dispatchdemo` opens the map on a dispatch's chooser.
- **Wandering encounters (2026-11-02, built):** `World/Roaming.cs`. Up to three wild packs roam
  the open places (never the ranch or the coliseum), saved in `Ranch.Wanderers`; each day
  (`Roaming.PassDay`, rolled from the day) a pack moves on two days in three and a new one turns
  up a day in three or so. Travel goes a road at a time (`Ranch.TravelStep`, a day each): arrive
  where a pack is and it attacks first (`TravelTo` returns it). Packs are on the board as fights
  to seek out, drawn on the map as their leader's sprite; their creatures come from where they
  first turned up (`Mission.FaunaMap`). Beaten packs are gone; they don't count as contracts.
- **The shop (2026-11-02, built):** `Classes/Gear.cs` holds the wares as data: gear is a
  `PassiveDef` with `Class = "Gear"` (one slot, `Training.Gear`, merged into `Unit.Passive` and
  baked into stats like a shiny's), items are `SkillDef`s with `Class = "Item"` (a count in
  `Battle.Bag` instead of a cooldown; aimed like skills; `Catch`/`CatchChance` for the snare net).
  `World/Shop.cs` says which place sells what for how much chem, some only after a main mission
  or a cup. `Ranch.Items` is the bag (saved); `Ranch.Buy` only where you stand; `Ranch.Equip`;
  `LeadUnits` packs the bag into a battle and `AfterBattle` takes out what was used (glowberries
  only past the three every battle brings). The AI never uses items. Shop button (S) on the map's
  place panel (it can travel you there); Gear button on the ranch. `--shoptest` checks it all;
  `--shopdemo` and `--battle=N --bag` are for screenshots.
- **Traits and augments (2026-11-02, built; the user's city/undercity split):** traits
  (`Classes/Traits.cs`) are biological, born with (`Creature.Traits`, rolled from the seed for
  wild ones) and passed down (`TraitBook.Inherit` in `Breeding`), each a passive merged into
  `Unit.Passive` (frame colour natural green). Augments (`Classes/Augments.cs`) are mechanical or
  chemical, bought at undercity shops and fitted at the lab (`Ranch.Install`, two slots,
  `CreatureRecord.Augments`): each adds its graft when the record becomes a creature
  (`ToCreature`, a species copy) and teaches its attack (`Unit.From`). `Ranch.Breed` reads the
  parents with `ToCreature(augmented: false)`, so augments never pass down. Old saves get their
  traits rolled in `Tidy`. `--traits` checks the odds, the inheritance and the save;
  `--select=NAME` opens the ranch on a creature.
- **Story flags and standing (2026-11-02, built as machinery; wording is placeholder):**
  `World/Standing.cs` holds standing with the city and the undercity (`Ranch.CityStanding`,
  `UnderStanding`) and every amount that moves it (work for a place's side, augments, breeding,
  cups) plus shop discounts. `StoryBook` adds `ChoicesAfter` (who gets the credit after a main
  mission: `Ranch.Pending`, `Ranch.Decide`, a flag in `Ranch.Flags`), the three endings
  (`MainMission.Ending` and `Leans`, gated on standing; `Ranch.Ending`) and `Rumours` (rare
  creatures per path, `Mission.Special`). The ranch shows a pending choice over everything; the
  map shows both standings. `--endings` checks the gates and the save; `--country=N --path=city|
  undercity|peace` plays a path; `--choicedemo` shows a choice. Story content is the user's: the
  prompts, rumours and ending missions are placeholders to replace, not to extend.
- **Sound (begun 2026-11-02):** `scripts/Audio/`. Every sound is made in code like the sprites:
  `SfxSynth` sums voices (square, triangle, saw, sine, noise; pitch slides, envelopes, low-pass,
  vibrato, tremolo) into 22 kHz mono, and `SoundBook` holds the 40 sounds as data (menus, steps,
  hits, faints, recruits, jingles, the duel gong, the shiny chime, one per skill theme:
  `Sound.Fx(theme)`). `Sound.Play(name, pitch)` plays one; `UnitView.Heft` lowers big creatures.
  Every button clicks (hooked in `GameRoot`); N mutes, `--mute` starts muted. `--sfx` writes each
  sound and `reel.wav` to `.shots/sfx/` with peak and RMS levels. Sound never touches the rules.
- **Music (2026-11-02, the user's pick):** made in code too. `Audio/Songs.cs`: `SongBook` holds six
  songs as data (a tempo, a chord progression a bar at a time, and parts: an instrument and a
  pattern of note tokens, "-" hold, "." rest, "|" bar lines, drums as letters), `Theory` draws
  arpeggios, bass lines and pads from the progression so only melodies and drums are written by
  hand. `Audio/Music.cs`: `MusicSynth` renders a song through `SfxSynth.Mix` into a seamless loop
  (notes and echoes wrap round the seam, a low cut, levelled to one loudness); `Instruments` are the
  voices (flute, horn, lead, chip, harp, lute, pulse, bell, bass, saw bass, pad, drums). The `Music`
  node renders every song on a worker thread at start (about 2.5 s; not in headless runs) and fades
  between tracks: `ranch` (ranch and lab), `map`, and in battle `Music.ForBattle`: `rival` for
  duels and cups, else by the place's side, `battle_city` (brass fanfare), `battle_under` (neon
  saw bass) or `battle` (the wilds). It stops for the result jingle and in the Sprite Lab. Quieter
  than effects (`Music.Db`); N mutes both, `--nomusic` turns it off. `--music` writes each song
  twice through to `.shots/music/`, with levels, the seam, each part's share of the mix (weighted
  roughly as the ear hears) and any bar that doesn't add up. Titles are working names; the user
  hasn't heard it yet.
- **Names:** players can name creatures (Name on the ranch); `Creature.Nickname`, saved on the
  record. Show `Creature.Name` (nickname or species) on screen, never `Species.Name`, which
  breeding still uses for species.
- **Online PvP is a goal** (2026-11-02, groundwork built): see `docs/design-notes.md`. Every
  action, the player's, the AI's or a replay's, goes through `Battle.Order(Command)`
  (`Tactics/Commands.cs`): small records a network can send. `Battle.StateHash` fingerprints the
  state after each turn. Never change battle state from a screen except through `Order`, or a
  match can't be replayed from its commands. `BattleScreen.TurnSeconds` is the turn timer
  (`--timer=N`).
- Not built yet: story and cutscenes (themes and three endings set by the user, in
  `docs/story-notes.md`; the flags, standing and endings machinery is built) and the network
  side of PvP. The
  user's direction and a suggested build order are in `docs/design-notes.md`.

## Build and check

- `dotnet build CreatureTactics.sln` is the fast check.
- **Battle rules:** `"$GODOT_BIN" --headless --path . -- --autobattle=N` prints an AI-vs-AI log.
  After any rules change, run seeds 1-20: every one should end in under 30 rounds. Stalemates have
  come from kiting skittish fliers and from units stuck behind cliffs.
- **Determinism:** `--replaytest` (headless) replays 14 AI battles from their commands alone and
  compares the state hash after every turn. Run it after any rules change: all must match.
- **Ranch and save file:** `--ranchtest` (headless) must report every creature identical after a
  save and load. Run it after touching `Species`, `Creature`, `PaletteSpec` or the painter's
  inputs. `--breeding=N` writes a sheet of parents and children. Use `--save=NAME` for anything
  that saves, so the user's `user://ranch.json` is never touched.
- **Sprites:** `"$GODOT_BIN" --headless --path . -- --forms --medium` (every form, front and back,
  on board tiles; `--scale=4` for close-ups) or `--views=N` (sheet N), then look at `.shots/`.
  `--export=.shots/x.png --sheet=1` still writes the front views. `--only=Lung,Jelly` narrows
  `--forms`; adding `--idle=NAME` writes the idle loop as frames to `.shots/idle/NAME/` (make a GIF
  with ffmpeg: `-framerate 8 -i f%02d.png`).
- **Skill effects** (2026-11-02): `Tactics/SkillFx.cs` draws them as board pixels with an additive
  glow layer: projectiles from user to target, themed bursts, one wave per blast, rising motes for
  heals and boosts, hit sparks, status marks. A skill's look (`FxTheme`: ember, frost, tide, leaf,
  spark, stone, venom, shade, holy, arcane, brass, chem, sound, wind, boost, plain strike) comes
  from its icon in `SkillIcons`, so new skills get one for free. `--battle=3 --fxdemo` fires one of
  each; `"$GODOT_BIN" --fixed-fps 30 --path . -- --battle=3 --fxdemo --shot=10 --record=560` saves
  its frames to `.shots/rec/` for a GIF.
- **Screens:** `"$GODOT_BIN" --path . -- --shot=40` (add `--autoplay` for mid-battle, `--save=X --newranch --mapmode`
  for the country map, `--battle=3 --aim --shot=900`
  for the aimed-skill panel, `--lab` for
  the Lab) writes `.shots/shot.png`. It opens a window briefly.
- `$GODOT_BIN` is in `.claude/settings.json`. If it is unset, say so rather than guessing.
  `.shots/` is ignored by git.

## Rules worth keeping

- Everything is seeded through `SeededRng` (SplitMix64), never `System.Random` or `GD.Randi`.
- `Battle` holds the rules and emits `BattleEvent`s; `BattleScreen` only animates them. Keep
  rules out of the screen so `--autobattle` stays a real test.
- Every roll should mean something in battle. Parts map to `Weapon`/`Armor`/`MoveType`
  (`Creature.cs`), and `Unit.From` turns those and grafts into stats.
- 3D painting keeps the nearest surface per pixel; a seam falls where another part sits more than
  `SeamDepth` in front. Shapes in one part merge without seams, so give a part its own id where a
  line should show (shell plates, feathers). Shape choices must come from the species' seed, never
  the view, or the front and back views won't match.
- Root Controls need `SetAnchorsAndOffsetsPreset(FullRect)`. Plain `SetAnchorsPreset` leaves them
  zero-sized under the `Node` root.
- The loyal recruit condition is a placeholder; the notes list only four.
- The DrawPets cast (`DrawPetsCast.cs`) is hand-authored from `../DrawPets/scripts/adoptables.gd`.
