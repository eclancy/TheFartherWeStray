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
  Layers: `Form` (29 silhouettes, a few per body plan: hound, stag, drake, hydra, sentinel,
  gargoyle, wraith, wyrm, naga, crane, mantis, scorpion, spider, ray, angler, wisp, jelly,
  watcher...; `LaterFormsFor` holds forms added after the first set, rolled only for a third of
  newly generated species so nothing already seen changes form), element themes (ember flames, frost shards, leaf
  fronds, tide fins, venom frills, shade smoke, spark crackle, meadow tufts), strain grafts, and an
  `Aura` (halo, floating shards, sigil, third eye, wisps). New forms go in `CreatureForms.cs`: build
  the body, set the anchors (head, spine, tail root, wing root); the shared parts do the rest.
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
- Jobs (`scripts/Classes/Classes.cs`, `docs/classes-design.md`): 43 classes (9 base, 30 part,
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
  (swap a part for one in the codex, the parts met in battle), machine grafts (brass) and
  infusions (blood strains for natural creatures). Flesh and machine grafts are **not inherited**:
  breeding reads `BirthParts`. Infusions are.
- **Story (planned, not started):** the user wants FFTA-depth story, cutscenes and sprite
  animation, and will set the themes first. `docs/story-notes.md` holds the goal, the open
  questions and a suggested first iteration. Don't invent the story's themes; ask.
- Missions (`Tactics/Mission.cs`): a board of four contracts per encounter (Rout, Capture, Hunt,
  Hold out) on seven handmade maps (`BattleMap.cs`, about 16x13 since 2026-11-02 when the user
  asked for larger maps), each with a look (`Look` in `IsoArt.cs`) and fauna odds. Maps have three
  grids: terrain (now with roofs, boards, dirt and water), heights, and props. Buildings are roof
  tiles on house-front walls (FFTA towns: fliers and climbers stand on roofs). Props
  (`Props.cs`: 22 kinds, trees to neon signs, built like creatures and coloured per region) block
  their tile except reeds and banners; fliers pass over them. Small ground details (tufts, cracks,
  fungus) are painted per tile. `--props` writes every prop in every region's colours. Sightings promise a codex-missing part. After touching maps or AI, run
  `--autoboard=6`: no mission may stall. Your side's AI plays the objective (`PlayerPlan` in
  `Battle.cs`): hunts focus the alpha, hold-outs keep together and out of reach, captures never
  land the knockout and play to the target's temper (a champion duels the proud, the squad rings
  the skittish, a glowberry lures the curious). `--autoboard=12` on 2026-11-02: rout 12/14,
  capture 5/11 (was 3), hunt 12/12, hold out 11/11 (was 9). Aggressive and loyal captures are the
  weak spot. `--trace=TITLE` prints the full log of matching missions.
- **Wardens and the party (the user's direction, 2026-11-02, built):** the player leads creatures
  as a warden (working name; the user wants something other than "general" and hasn't picked yet).
  Wardens stay off the field and give passive boosts; each leads three creatures; at most two
  wardens and six creatures per battle, swapped between battles. `scripts/Party/Wardens.cs` holds
  them as data (a specialty boost, which creatures it reaches, the encounter they join at: a
  placeholder for the story); `Ranch.Fielded`/`LedBy` save who leads whom; `Battle.Lead` applies
  the boost before the first turn; the Party panel (P) on the ranch fields wardens and fills slots. Details, defaults and open
  questions are in `docs/design-notes.md`, with PvP (casual, turn timers, your own squad) and
  shinies (1 in 512; the battle opens with a camera pan and a glint on each one).
- **Online PvP is a goal** (2026-11-02, groundwork built): see `docs/design-notes.md`. Every
  action, the player's, the AI's or a replay's, goes through `Battle.Order(Command)`
  (`Tactics/Commands.cs`): small records a network can send. `Battle.StateHash` fingerprints the
  state after each turn. Never change battle state from a screen except through `Order`, or a
  match can't be replayed from its commands. `BattleScreen.TurnSeconds` is the turn timer
  (`--timer=N`).
- Not built yet: story and cutscenes, rival tamers, the network side of PvP.

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
- **Screens:** `"$GODOT_BIN" --path . -- --shot=40` (add `--autoplay` for mid-battle, `--battle=3 --aim --shot=900`
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
