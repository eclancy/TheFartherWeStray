# CLAUDE.md - The Farther We Stray

**The game is called The Farther We Stray** (the user, 2026-11-02): `GameRoot.Title`, the project's
`config/name`, the solution and project (`TheFartherWeStray.sln`, `.csproj`), the assembly and the
root namespace (`TheFartherWeStray`). The old working name is gone everywhere but one place:
`GameRoot.BringOverOldSaves` copies saves and settings once from the old `user://` folder (user://
follows `config/name`, so renaming the game moved it; never rename it again without a copy like
that). The local folder may still have the old name until the user renames it. The story's theme: the city above is
just as corrupt as the city below (`docs/story-notes.md`). **Words on screen** (the user, for now):
your tamed creatures (and a tamer's) are **beasts** ("your beasts"); uncaught ones are **strays**;
general rules text and the code say creature. No automatic nicknames: players name their beasts.

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
- **Arcane is the look, not the words** (the user, 2026-11-02): no names or terms from the show or
  League anywhere a player reads (no Piltover, Zaun, the Lanes, lamplighters, hextech, shimmer, the
  Sump, chemtech). Strains show as Chemborn and Runic (`Strain.Label()`); the `Strain` enum names and
  the `hextech_core` and `shimmer_*` ids stay because saves store them. The marsh is the Glowmire;
  the city above's look is `Uptown`.
- **Theme: industrial fantasy** (the user, 2026-11-02): a magic world in its industrial age, as in
  Arcane. The city above is a gilded golden age (royal, spires, old money); the undercity is smog,
  pipes, alchemy and chemicals glowing in glass, with neon signs but **no computers**. Lean away
  from neon punk's hacking side (screens, code, cyberware) everywhere, and from brass-and-gears
  steampunk in names. Machines are clockwork, steam and chemistry; the creature tiers fit it
  (the undercity's rigs and chemicals, the city above's thoroughbreds and breeding).
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
- **Menu style: Gilt and neon** (the user's pick, 2026-11-03; research, rules and the other three
  candidates in `docs/ui-style.md`). The city's gold line with stepped Art Deco corners round smoked
  violet glass, an undercity neon tube inside it: unlit, glowing faintly under the pointer, lit cyan
  for the pad's focus, pink when pressed. Frames are pixel nine-slices painted in code
  (`UI/UiSkin.cs`; a frame pixel is the board's 3x, small buttons' 2x) and go on Godot's default
  theme at start, so every button, panel, tooltip and slider takes them. Screens that draw their own
  boxes use `UiSkin.Box(part, state)`; a box whose light means something uses `UiSkin.Lit` (pens
  cards: gold chosen, pink in a vat, cyan in the squad; the turn order by side; the skill being
  aimed); `UiSkin.Tight` gives an icon or portrait less room. A button whose words are 16 points or
  less takes the small frame (`UiSkin.Adapt`). The phone pad is drawn in it too (`PaintRings`). New
  boxes go through `UiSkin`, never a fresh `StyleBoxFlat`. The game icon (`icon/icon.png`, `--icon`)
  is Blaze in the frame. `--ui=plain|gilded|neon|iron` puts another style on for comparing;
  `--uiboard=NAME --shot=30` shows a style's parts side by side (`UI/StyleBoard.cs`).

## Status

- Sprite Lab (`scripts/SpriteLab/`): the generator prototype.
- Battle slice (`scripts/Tactics/`): one handmade map, your squad against a pack seeded by the
  encounter number, speed-order turns, parts as gear, attitude reactions and AI, and recruiting.
- **The home base (2026-11-02, the user's direction, built):** every menu and system grounded in a
  place you walk to. `Ranch/HomeBase.cs` holds the base map (a farmyard: `Ranch` look) and its
  stations as data (`StationDef`: id, name, glyph, prop letter, `Built` rule, what opens it): the
  gate (the country map), the pens (roster and squad), the training yard (jobs), the armory
  (gear, after a contract), the breeding vats (after two), the war table (party, when a second
  tamer joins), the lab (after `main_foundry`); the user kept this order (2026-11-02). Unlocks come from progress (no save change); an
  unbuilt station is `Prop.Scaffold`; `used:ID` flags mark the NEW ones. `Ranch/BaseScreen.cs`
  is home: you as a person (`SceneActor`), click to walk (`Paths.Find`) or WASD, E or click a
  station to use it, the herd wandering the pens, the camera following; it asks the pending
  choice and runs the attract loop (`RanchScreen.AskChoice`, `AutoBattle`). A station opens
  `RanchScreen` with `Station` set: only that part shows (pens: the squad, name, release; vats: the
  vats; armory: gear; yard: jobs; war: tamers; lab: lab), its Back button goes home; the old
  shortcuts are off there. `BaseScreen.Standing` keeps where you stood.
  **One top bar** (`UI/TopBar.cs`, 2026-11-07): home, every station and the map show the same
  bar: the screen's name, then beasts, squad, chem, scrap, the day, both standings (and titles
  once there are any), and Back home on the right. It refreshes itself; screens only place it
  and start below `TopBar.Bottom`. A station says what it is once (its label at home, its name
  in the bar): no intro line, no key line. **One beast in the column** (2026-11-07): the selected
  one; the pointer only lights a card. Its parts show only at the lab, open and locked classes
  only at the yard. **The squad is picked only at the pens** (`Ranch.AddToSquad` puts a beast
  under a fielded tamer whose specialty boosts it when one has room); the war table fields
  tamers and shows who leads whom, and with two leading moves a beast across (`MoveLeader`, or
  pick one and one of the other's: `SwapLeaders`). **The job tree** (`JobsPanel`) lists the classes
  a beast can train first (its main, its secondary, the rest); Show locked adds the others, and
  the three groups (base; part and form; blood) are a filter.
  **Home's name** (the user's call, 2026-11-07): "Home" until the player renames it (Name your
  home, in home's pause menu). Saved as `Ranch.HomeName` (`Ranch.Home` to show, `NameHome` to
  change); `Country.HomeName` is the save in play's, so home's place on the map, the bar's
  title, `Country.BackHome` ("Back home", or "Back to Thornfield") and the slot summary use it.
  On screen, home is "home" (never the ranch or the base); the code still says ranch and base. `--goto=ID` walks to a station and uses
  it; `--station=ID` (and `--labmode`, `--jobsmode`, `--partymode`, `--vatdemo`) opens one
  directly. New stations: a `StationDef` entry, a prop letter on the map, and a `Station` case.
  **Decorating the farmyard** (the user's ask, 2026-11-03; phase 1 of `docs/farmyard-plan.md`,
  built):
  - The workbench (`bench`, `j`, by the farmhouse) turns the base into build mode in place: a
    tile cursor, and the shed along the foot of the screen.
  - The pieces are props (`World/Decor.cs`, `DecorBook`, 19 of them). Each side's shops sell its
    own look as `decor:ID` wares.
  - They're saved in `Ranch.Decor` (placed) and `Ranch.Shed` (owned). `HomeBase.Map` stands them
    on the board.
  - The rules are in `Ranch/Farmyard.cs`: any free tile, never a station, the pens or the spot
    you come home to, and never walling anything off (a flood fill).
  - New pieces are `DecorBook` entries; a new kind of prop goes last in the `Prop` enum.
  - `--basetest` checks it all headless; `--goto=bench --holding=ID --cursor=X,Y` is for
    screenshots.
  **Beasts out and about** (phase 2, built):
  - At the pens, up to six beasts not in the squad are let out into the yard (`Ranch.Out`;
    `Farmyard.ToggleOut`, Y; their cards lit green).
  - They roam the yard by temper and go to what's in it (`BaseScreen.Decide` / `Potter`): the
    aggressive spar with the dummies, the curious look at the newest piece, the loyal follow you,
    the proud stand somewhere grand, and the skittish keep to the edges. Otherwise they drink,
    nap or sit by things.
  - Click one, or press A beside it, to say hello.
  - The title screen shows the last slot's own yard and its out beasts.
  - `--save=yard_show` (written by `--basetest`) is a yard to look at.
  **Machines** (phase 3, built):
  - A chem still, glowberry beds and a scrap press (`World/Machines.cs`; props `Still`,
    `BerryBeds`, `ScrapPress`, which move in `PropFx`). Each fills a hopper per game day:
    `Farmyard.PassDay`, called where `Ranch.Day` goes up, never in real time.
  - One of each per yard. You buy it at a shop into the shed, put it out and move it at the
    workbench; it keeps its level and hopper.
  - A or a click opens its panel: Collect, or Upgrade for chem and scrap (`Ranch.Scrap`, made by
    the press).
  - New machines are `MachineBook` entries and a prop.
  - `--machine=ID` opens one for screenshots.
  **Station upgrades** (phase 4, built):
  - Build up opens a station's levels (`Ranch/BuildUpPanel.cs`): at the workbench (the cursor on
    a station and A) and in the station's own top bar (`TopBar.Station`; the gate's is on the map;
    `--station=ID --buildup` or `--mapmode --buildup` opens it for screenshots). Every station but the
    workbench goes to level 3, built at once (the user's call) for chem and scrap
    (`HomeBase.UpgradeCosts`; saved in `Ranch.Levels`).
  - The effects stay on the loop, never battle stats. They're read through `Farmyard`:
    - `Carts`: the gate's first roads take no day;
    - `OutCap`: the pens hold 6, 9 or 12 beasts out;
    - `HomeShare`: the yard trains the beasts at home on the squad's JP;
    - `BattleBerries`: the armory packs 3, 4 or 5 glowberries;
    - the vats' rest rules;
    - `DispatchPay` and `DispatchDays`: the war table;
    - `LabPrice`: the lab's discounts.
  - New effects are an `Upgrades` line on the station and a rule that reads its level.
- **Title, save slots and settings (2026-11-02, the user's pick, built):** the game starts on
  `UI/TitleScreen.cs` (the home base drifting behind the name; Continue, New game, Load, Settings,
  Quit). Three slots (`Ranch.Slots`, `SlotPath`, `Peek`, `Summary`, `DeleteSlot`): slot 1 is the
  old `user://ranch.json`, so saves from before slots are slot 1; overwriting or deleting asks
  twice. `UI/Settings.cs` keeps music and effects volume, mute, cutscene text speed, the battle
  turn timer, fullscreen and the last slot in `user://settings.cfg`, applied at start
  (`Settings.Apply`); `SettingsPanel` edits them on the title and in the pause menu. N saves the
  mute. Test and screenshot flags (`--save`, `--newranch`, `--autoplay`, `--station`, `--scene`,
  `--mapmode`...) skip the title; `--title[=new|load|settings]` opens it anyway, `--slots=NAME`
  keeps slots in test files, `--pause` opens the base's pause menu (`--pause=controls` on its
  Controls page), `--slotstest` checks slots and settings headless.
- **The pause menu and the keys (2026-11-07, the user's call; `docs/menus-plan.md`):**
  - **Esc pauses on every screen on a desktop**: the base, the stations, the map, battle and
    scenes (`UI/PauseMenu.cs`; `PauseMenu.Opens`). Esc still closes an open panel or stops aiming
    first. Going back is the Back button; on a phone B goes back and Start is the menu.
  - **The game stands still while it's open** (`Engine.TimeScale` 0): walks, turns, the turn
    timer and scenes wait. Timers that must run anyway take `ignoreTimeScale` (the `--pad` tool).
  - Its items: Resume, the screen's own (a scene's Skip the scene, home's Name your home),
    Controls, Settings, and where there's a game to save, save and return to title, save and quit.
  - **Controls** (`UI/Controls.cs`, `ControlsBook`): every key, screen by screen, the screen
    you're on first; a desktop's keys, or the phone's pad and taps. New keys go there.
  - **Key hints show the first three times a screen opens** (`Hints.Show(id)`, counted in
    `settings.cfg`), each ending with where the rest are (`Hints.Menu`). A run with flags shows
    every hint and counts none; `--nohints` shows none.
- Ranch and breeding (`scripts/Ranch/`, `Creatures/Breeding.cs`): a saved roster, squad picking,
  two-vat breeding (one child per pair; parents rest until the next battle), and the
  ranch-battle-ranch loop. Bred species exist only in the save, so `CreatureRecord` stores the
  whole `Species`.
- Jobs (`scripts/Classes/Classes.cs`, `docs/classes-design.md`): 51 classes (9 base, 31 part,
  form, aura or temperament, 7 signature, 4 blood; the form and aura classes need a `Form` such as
  the hydra, or that form's ability spliced into a chimera (`ClassBook.HasForm`), or an `Aura` such
  as a halo). **Signature classes** (the user's ask, 2026-11-02: FFTA's summoner, illusionist and
  assassin, skinned): Conjurer (summons: `SkillDef.Phantom`, a great beast's ghost rising over the
  area, drawn in `BattleScreen.Phantom`), Mesmer (`AllFoes`: every foe on the field at once), Reaper
  (`Doom`, `Sleep`, `Finish`; `Unit.DoomTurns`, `SleepTurns` are in the state hash), and after FFTA's
  blue mage, time mage, gadgeteer and morpher: Mimic (learns foes' skills used on it:
  `Battle.Learned`, `Training.Learned`), Horologist (`Stop`, `Quicken`), Saboteur (traps: `SkillDef.Trap`,
  `Battle.Traps`, hidden from the other side, sprung by the first foe to walk on one), Skinchanger (`CopyForm`: `Unit.Borrowed`).
  **Trials** (the user's call): the seven signature classes open only after their trial mission
  (`ClassDef.Trial`, `World/Trials.cs`, flags `trial:CLASS`), which also moves standing toward its
  side; `ClassBook.TrialDone` reads the save in play (set in `Ranch.Tidy`/`New`; open by default).
  `--signatures` proves every mechanic, `--battle=3 --fxdemo=signature` shows the summons. Each class has three skills and passives (a support, often a reaction too),
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
- **Story (content not started; the cutscene player is built):** the user wants FFTA-depth story,
  cutscenes and sprite animation. The user set the themes on 2026-11-02 (lighthearted to start; a
  glorious city and a nefarious undercity that hate each other; branching by alignment, solutions
  and dialogue; three endings: justice for the undercity, chaos for the city, or peace).
  `docs/story-notes.md` holds them and the open questions. Don't invent beyond them; ask.
- **Cutscenes (2026-11-02, the user's pick, built):** `scripts/Story/`. `SceneBook` holds scenes as
  data: a map, music, and a script of plain lines (the format is in `Scenes.cs`: `cast` people and
  creatures at tiles, `ID: dialogue`, `walk`, `face`, `emote`, `pose`, `appear`, `leave`, `pan`,
  `title`, `narrate`, `fade`, `wait`, `sound`, `music`, `shake`; a leading `&` runs a step alongside
  the next; tiles can be `player` / `wild` plus an offset, so a scene fits any map). Creatures are
  `squadN` (yours), `packN` (the battle's pack), a cast species or `wild:SEED`; people are looks by id
  and side. `CutsceneScreen` plays one on its map at 3x: the cast as animated sprites, a dialogue box
  with the speaker's portrait, a typewriter and a marker over them, emote balloons, captions, camera
  pans, fades; Enter / Space / click goes on, Esc skips. Board sprites share `Tactics/Actor.cs` with
  battle (`UnitView` is an `Actor` with its ring and HP bar). People (`Story/People.cs`): a
  `PersonSculptor` makes them like creatures (a 3D figure ray-cast to pixels, every pose animated),
  looks rolled per id and side (`People.Look`). **Stylized and punk, not rigid** (the user,
  2026-11-02): long legs, a V torso, chunky boots, the whole body leaning into a stance (swagger,
  crossed, slouch, ready, showoff, regal); clothes coloured by the creatures' own palette roller
  (`PaletteSpec.Roll` by element); the undercity neon-punk (crests, liberty spikes, dreads, hair horns,
  neon tips; vests showing glowing tattoos; mechanical arms, chem tanks, eye implants, glowing veins,
  piercings, spine spikes); the city above royal (silver, gold and blues: breastplates, paired
  pauldrons, capes with gold hems, sashes, ruffs, crowns and circlets); the wilds scavengers. A main
  mission's `Scene` plays once before its battle (flag `seen:ID`); the only scene, `road_to_foundry`
  before `main_foundry`, is a placeholder showing what scenes can do, to be replaced with the
  user's story. `--scene=ID [--autoplay]` plays one; `--scenetest` reads them all (cast, tiles,
  sounds, tracks); `--people` writes `.shots/people.png`; `--recordwhole` records the whole screen
  at half size (cutscene dialogue sits low).
- Missions (`Tactics/Mission.cs`): a board of four contracts per encounter (Rout, Capture, Hunt,
  Hold out) on twelve handmade maps (`BattleMap.cs`, `BattleMapsFar.cs`, about 16x13 since 2026-11-02 when the user
  asked for larger maps; the five town maps, Lantern Row, the Foundry, the Brass Bridges, the Gilded
  Gardens and the Clocktower Ruins, grow by `BattleMap.Grow` to about 22x19: the handmade map whole
  in the middle, a street round it, then house blocks cut by alleys, stepping up a storey at a time
  so every roof can be climbed to; anchors move with the middle, so scenes' `player+DX,DY` tiles
  still fit), each with a look (`Look` in `IsoArt.cs`) and fauna odds. Maps have three
  grids: terrain (now with roofs, boards, dirt and water), heights, and props. Buildings are roof
  tiles on house-front walls (FFTA towns: fliers and climbers stand on roofs). Props
  (`Props.cs`: 25 kinds, trees to neon signs, built like creatures and coloured per region) block
  their tile except reeds, banners and grates; fliers pass over them. **Surroundings and the
  camera (the user's ask, 2026-11-02: closer in, a proper city):** `IsoArt(map, skirt)` paints
  tiles round the map (`Surround`, from the map's name, by region: house blocks with alleys behind,
  tall; low plazas, stalls and lamps in front so nothing hides the fight; cliffs, dunes, lava, sea,
  crystal or fields in the wilds), darkened toward the edge; `AroundProps` stand on them
  (`PropArt.Place` places both). They're scenery, never part of the battle (`Pick` is map-only).
  Battles, cutscenes, the base and the title use `BattleScreen.Skirt` (8). The battle camera
  (`BattleScreen.Camera`) draws the board at 3x (wheel: 2 to 3.5), follows whoever acts, pans with
  WASD / arrows / a middle-drag, and keeps over the battlefield. **Tile highlights keep to what
  can be seen** (the user's call, 2026-11-03): `IsoArt.KeepTops` records which tile's top shows at
  each board pixel as it paints, `TopMask` cuts a highlight to it, and `BoardOverlay` draws a tile
  hidden behind a block in front only where it shows, or whole while the cursor is on it. The HUD sits on its own canvas
  layer above the board's depth-sorted sprites. Small ground details (tufts,
  cracks, fungus, neon puddles) are painted per tile. **The undercity's grime** (the user's ask,
  2026-11-02): broken streetlights (`l`) that stutter and spark, leaking hydrants (`y`) spraying
  into a rippling puddle, steam grates (`v`), and neon signs buzzing out now and then, animated by
  `PropFx` (show only, never the rules); graffiti tags, bubble letters, sprayed symbols and torn
  posters on undercity walls (`IsoArt.WallArt`, from the wall's tile). **Houses** (the user's ask
  for a lot more variation, 2026-11-03; `Tactics/Houses.cs`): each run of roof tiles of one height
  is a `House` rolled from its first tile and its region's `Street`: wall material (plaster,
  timbered, brick, boards, tin, stone) and colour, roof (shingles, tin, a flat roof with a parapet,
  vents and lit skylights, slate, copper, thatch, snow), windows (square, arched, portholes,
  shuttered, boarded, barred; lit in the house's colours with a glow on the wall), doors (lit
  fanlights, roller shutters), awnings, flower boxes, wall lamps, neon signs with a mark, drainpipes,
  ivy, icicles; lamps and lit doorways pool light on the street in front (`Spill`). The undercity is
  grimy brick, tin and boards lit in every colour; the city above pastel plaster and stone, arched
  and shuttered windows, awnings and flowers. `WallArt` has 17 kinds (tags, arrows, two-tone
  bubbles, 13 marks, stencils, throw-ups, stickers, pasted posters, painted-over patches with fresh
  tags, chem stains, two-storey murals) in the undercity, the Glowmire (chem colours) and the ruins
  (faded), and posters alone on the coast and at the ranch. All of it is painting, never the rules. `PropArt.Place` puts a map's
  props on any board (battles and cutscenes). `--props` writes every prop in every region's colours. Sightings promise a codex-missing part. After touching maps or AI, run
  `--autoboard=6`: no mission may stall. Your side's AI plays the objective (`PlayerPlan` in
  `Battle.cs`): hunts focus the alpha, hold-outs keep together and out of reach, captures never
  land the knockout and play to the target's temper (a champion duels the proud, the squad rings
  the skittish, a glowberry lures the curious, the sturdy stand up to the aggressive while anyone
  its blow could fell keeps clear; for a loyal target, `Mark` first wins over its easiest
  packmate). Creatures whose thorns or counters could fell the target keep out of its reach
  (`Avoid`). `--capturetest=10` plays a capture with the target set to each temper: on
  2026-11-02 proud 8/10, skittish 7, curious 9, aggressive 6, loyal 2, no stalls; on the grown
  town maps proud 8, skittish 7, curious 10, aggressive 7, loyal 6. Loyal stays lowest because the
  target fights at full strength while you may not touch it (the rule is a placeholder). After
  `Battle.CaptureGiveUp` (20) rounds your side's AI gives a capture up and fights it out: on the
  bigger maps a last creature could otherwise keep clear of a loyal pair for ever. `--trace=TITLE` prints the full log of matching missions (`--trace=8Loyal` in
  `--capturetest`).
- **Tamers and the party (the user's direction, 2026-11-02, built):** the player leads creatures
  as a tamer (the user's name, 2026-11-02: friendlier than "general" or the working name "warden";
  someone who collects and works with creatures, like a Pokémon trainer).
  Tamers stay off the field and give passive boosts; each leads three creatures; at most two
  tamers and six creatures per battle, swapped between battles. `scripts/Party/Tamers.cs` holds
  them as data (a specialty boost, which creatures it reaches, the encounter they join at: a
  placeholder for the story); `Ranch.Fielded`/`LedBy` save who leads whom; `Battle.Lead` applies
  the boost before the first turn; the war table at the home base (the Party panel) fields tamers and moves beasts between them (the squad itself is picked at the pens). Details, defaults and open
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
  (`RivalBook.Fewer`, `Ranch.RivalLosses`, saved; never under two; a win resets it). **Prizes
  (the user's pick, 2026-11-02): every win draws one by rarity** from `RivalDef.Prizes`
  (`RivalBook.Draw`: common 57%, uncommon 30, rare 12, very rare 1 (the user's call: 3% shiny
  was too common), then a prize of that rarity):
  items or chem (common); their signature gear (in `GearBook.Gear`, never sold), their augments,
  a title (once, `Ranch.Titles`, saved; names are placeholders) (uncommon); an egg from their line
  (rare; shiny, very rare): `RivalBook.Egg` breeds two species to their taste, given their blood,
  and adds a trait if none passed (no grafts). `Ranch.Award` grants and reports it; the duel card
  hints at it (`PrizeHint`). New prizes are entries. Their
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
  rushed. **Two boards** (the user's direction, 2026-11-03): the undercity's work and the city
  above's are separate boards, and the city's offers nothing (no contracts, regular jobs, duels,
  trials, rumours or main missions) until someone with connections brings you in: `Country.Hires`,
  opened by `CityBoardOpenedBy` (`main_foundry` until the story's scene; story-notes, act one).
  `CountryScreen` (M at the ranch, `--mapmode`) shows it all, names each place's board, and travels before a battle.
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
- **Creature tiers (2026-11-02, the user's design, built):** `Classes/Tiers.cs` (`TierBook`,
  `InfusionBook`). A creature goes by the most extreme thing done to it: a **stray** (uncaught), a
  **thoroughbred** (caught and left natural: a bonus **from its species**, its form's ability plus a
  touch of its element, `TierBook.ThoroughbredOf`; new forms want a `FormBonus` entry), **infused**
  (its stats tuned by lab infusions, `CreatureRecord.Infusions`, two slots, or new blood), a
  **chimera** (**the abilities of other species**: its form's plus up to two in `Species.Spliced`,
  `TierBook.ChimeraOf`; bred across bloods, clockwork counting as natural, or from a chimera, or
  spliced at the lab from `Ranch.Forms`, or a grafted part), **rigged** (any augment: **abilities
  no natural creature has**; or a rival's machine grafts); all three make an **abomination**. The
  tier passive (`TierBook.PassiveOf`) and infusions merge into `Unit.Passive` like traits.
  `Species.Infused`, `Chimera` and `Spliced` pass down; infusions, grafted parts and augments
  don't; `Species.Rigged` is only for rivals. `Unit.From` counts a creature as caught if it's yours
  or trained (a rival's); wild ones are strays, and the game calls them strays. An abomination
  also gets `TierBook.Unstable` (big stats; its instability is between missions, never in battle:
  after a battle, 1 in 4 an episode in `Ranch.Episodes`: `CreatureRecord.Recovering`, a part shifts,
  or an infusion sours). Costs: a new infusion steeps (`CreatureRecord.Steeping`: out of
  the next battle), rigs cost `TierBook.UpkeepPerRig` chem after each battle, rigged creatures learn
  slower (`TierBook.GrowthRate`). Movement rigs: Gear Wings (fly), Grapple Claws (climb)
  (`AugmentDef.Move`). Breeding a chimera, new blood, infusions and the
  first splice move the undercity. Tier chips on the pens and the battle panel; lab messages say
  when a tier changes (`Ranch.TierNote`). `--tiers` checks it all; old saves are brought up once
  (`Ranch.TiersFlag`).
  With species bonuses and chimera abilities (2026-11-02): rival duels 47% straight, 65% after a
  loss, 87% after two; captures proud 10/10, skittish 9, curious 10, aggressive 8, loyal 8; battles
  3 to 9 rounds; no stalls.
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
- **Ideas from the voice call (2026-11-02, the user's; not decided, not built):** title ideas
  (Lanternkin, Brassbound Tamers, Above and Below), "strays" for uncaught creatures and The Farther
  We Stray as a theme, with the tasks the call left, in `docs/design-notes.md`. Ask before building
  any of it. (The call's creature tiers are built: below.)
- **The phone build (2026-11-03, the user's ask: Android, held sideways).** `UI/Phone.cs`: on
  Android, or with `--phone` on a desktop (add `--resolution 2400x1080` for a phone's shape), the
  game runs in a SubViewport between two fixed pad columns that never cover it and never come or go:
  a D-pad on the left; A (select), B (cancel) and Start (menu) on the right, Game Boy fashion. The
  game is laid out 720 tall there (`Phone.GameHeight`; a desktop's is 1080) so text reads on a
  phone, and about 1000 to 1200 wide: **screens size themselves from `GetViewportRect()`, never
  1920x1080.** The pad sends keys (the arrows, Enter for A, Escape for B, `Phone.Start` = Key.Menu);
  Android's back gesture is B. `UI/PadNav.cs`: every button takes the focus, scroll boxes follow
  it, a gold frame shows it, the first arrow or A lands on the frontmost layer's first control
  (`PadNav.First` marks one), and the focused or tapped control's tooltip shows beside it (no
  hover on a phone). Per screen: the base walks on the D-pad and A uses a station; Start opens the
  pause menu (`UI/PauseMenu.cs`) on every screen (B closes it). Stations
  and the map get every action as a button (the vats' Breed, the map's Shop and Depart); the map's
  D-pad picks a place, A goes into its missions (A on a card moves to Depart), B comes back out.
  Battles have a cursor: the arrows step it along the board's rows turned a quarter (up is
  up-right), A acts on it (on your own creature it opens the command bar), B cancels or undoes;
  on a phone a tap moves the cursor, a second tap on it acts, a drag pans. Cutscenes: A or B goes
  on; Start is the menu, which can skip the scene. On the desktop the arrows move the battle
  cursor (WASD pans) and Backspace or right-click undoes a move. **New screens and actions:** lay out from the viewport, give
  every action a button, and look with `--phone`. `--pad=down,down,a,...` presses the pad's
  buttons after the screen settles (`--padwait=S`), logging where the focus went.
- **Android export (2026-11-03):** `tools/android-build.sh` writes a release-signed
  `build/android/TheFartherWeStray.apk` (`--install` puts it on a phone adb sees) from the
  `Android` preset in `export_presets.cfg` (arm64, no Gradle). **The package id
  `com.ecec.thefartherwestray` is the app's identity: never change it**, or it's a second app that
  can't update the first. The version code must climb with every APK (major*10000 + minor*100 +
  patch, from `config/version`). The key is `%APPDATA%/Godot/keystores/thefartherwestray-release.keystore`
  with its password beside it (`.secret`), never in the repo; Android only installs an update
  signed with the same key, so it must be backed up off this machine. `--icon` paints the launcher
  icons into `icon/` (Blaze on the night colour).
- **Windows export, releases and updates (2026-11-03; `docs/releasing.md`):** the `Windows Desktop`
  preset (x86_64; `icon/icon.ico` from `--icon`, `config/version`); not code-signed, so SmartScreen
  warns. **Release with `bash tools/release/release.sh X.Y.Z "What changed"`** (`--dry-run` first): it
  bumps the version and the APK's version code, exports, packages (`package.py`: the portable zip,
  the patch zip, `version.json`), compiles the installer (`installer.iss`, Inno Setup; its `AppId`
  never changes), exports and checks the signed APK, commits, tags, pushes and makes the GitHub
  release on eclancy/TheFartherWeStray. **Downloaded copies update** the way The One He Missed's do
  (the user's ask): `UI/UpdateScreen.cs` over the title of an exported Windows build fetches
  `ecec.dev/the-farther-we-stray/version.json` (a redirect to the newest release's), downloads the
  patch (or the full zip on another engine) and restarts through `apply.cmd`; `UI/UpdateBanner.cs`
  on Android shows a strip offering the APK. Every failure just goes to the title. `--no-update`,
  `--update-screen=9.9.9`, `--update-banner=9.9.9`, `THEFARTHERWESTRAY_UPDATE_URL`. A release is
  built from what's committed (docs aside). `tools/windows-build.sh` and `android-build.sh` stay
  for one-off builds.
- **The game's page on ecec.dev** (`ecec.dev/games/the-farther-we-stray`, the user's ask 2026-11-03:
  its own style, like the other games' pages) is built from the game's own art: `--webkit[=DIR]`
  (`SpriteLab/WebKit.cs`, headless) writes a stray of every form (a strip of idle, strike, hurt and
  cast frames and what the battle panel says about it, and the tier it becomes once caught), the
  Gilt-and-neon frames as nine-slices for CSS border-image, a board tile and every glyph. The page
  lives in the website's repo (`src/pages/games/the-farther-we-stray/`, the art in
  `public/games/the-farther-we-stray/`); after changing forms, glyphs or the menu style, export
  again and copy it there.
- **The shader cache is off** (`rendering/shader_compiler/shader_cache/enabled=false`): with it on,
  an exported build crashed on every second launch (the cache the first run wrote; found
  2026-11-03, `docs/releasing.md`). Don't turn it back on without testing two launches of an export.
- Not built yet: the story itself (themes and three endings set by the user, in
  `docs/story-notes.md`; the flags, standing and endings machinery and the cutscene player are
  built) and the network side of PvP. The
  user's direction and a suggested build order are in `docs/design-notes.md`.

## Build and check

- `dotnet build TheFartherWeStray.sln` is the fast check.
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
- **Screens:** `"$GODOT_BIN" --path . -- --shot=40` shows the title (`--save=X --newranch` for the base; add `--autoplay` for mid-battle, `--save=X --newranch --mapmode`
  for the country map, `--battle=3 --aim --shot=900`
  for the aimed-skill panel, `--lab` for
  the Lab) writes `.shots/shot.png`. It opens a window briefly.
- **The phone:** add `--phone` and run with `--resolution 2400x1080` to see any screen as a phone
  shows it, pad and all (the shot is the whole window).
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
