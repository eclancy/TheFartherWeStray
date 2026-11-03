# Design notes: decided directions and things to think on

_Systems the user has asked for: tamers (first called generals) and the party, online PvP, shinies. Each
says what's decided (the user's words), what was defaulted to build a first version, and what's
still open. Story themes live in `story-notes.md`; the user will come back to the story later._

## Ideas from the voice call (the user's, 2026-11-02)

_Imported from the user's "Game name ideas" doc and the call's transcript (the game part only).
The title ideas are still ideas; the creature tiers have since been decided and built (below). The
doc's Wizard Survivors names belong to another game and stay there._

**Title ideas** (the game is still called Creature Tactics):
- Favourites: **Lanternkin** and **Brassbound Tamers** (liked on the call), and **Above and Below**
  (it fits the city and the undercity).
- Built on those: *Lanternkin: Brassbound Tamers* (the creatures are Lanternkin, you're a
  Brassbound Tamer), *Brassbound Lanternkin*, *Lanternkin Tactics*, *Lanternkin of Lantern Row*
  (one of the maps), *Brassbound: Tamers of the Undercity*, *Cogkin* (more clockwork in it).
- A possible theme or name: **The Farther We Stray**. It needs workshopping so it feels more like
  the game's story.
- **Strays:** any creature that hasn't been captured is a stray. (Built as its tier: below.)

**The creature tiers** from the call are decided and built: see Creature tiers, below. The call's
first draft also had a set bonus for a squad of one strain (the user replaced it with a bonus per
creature), an infusion that rests a mission, a chimera that uses up a second creature, and a rig's
upkeep or a cap on growth; none of those is built.

**Names and theme (the user's, 2026-11-02):**
- **Names shouldn't lean steampunk**, and not every one needs "lantern" in it. On the call the
  user called the game more neon punk; afterwards (2026-11-02) they asked to lean away from neon
  punk too if it means hacking, and it usually reads that way (cyberpunk: screens, hackers,
  cyberware). **The theme is industrial fantasy** (the user chose it, 2026-11-02): Arcane's own
  mix. That means a gilded city above in its golden age, and an undercity of smog, pipes, alchemy
  and glowing chemicals, with neon signs but no computers.

**Tasks from the call:**
- [x] Name the DNA tier: **Chimera** (the user, 2026-11-02).
- [x] More on the chemical path: the user set it (2026-11-02): infusions adjust its natural stats.
  Built as the lab's infusions (Creature tiers, below).
- [ ] Find a title that fits the look (not steampunk, not hacking), perhaps by naming the
  creatures as a whole first.

**Open:**
- [ ] A title: one of the above, or keep Creature Tactics?

## Creature tiers (the user's design, 2026-11-02; built)

**Decided by the user** (on the voice call and after):
- **A creature goes by the most extreme thing done to it.** From least to most: **Infused**,
  **Chimera** (an infused creature that becomes one goes by Chimera), **Rigged** (the most
  unnatural). Only all three at once make an **Abomination**.
- **Thoroughbred:** caught and left natural, the heroic word for purebred instead of "feral". It
  gets **a passive bonus of its own for staying natural**, not a squad bonus, and **the bonus is
  based on its species**.
- **Infusion adjusts its natural stats** (it changes less of its natural state, enhancing it or
  with small trade-offs; maybe through something like an equipment panel).
- **A chimera can have the abilities of different species.**
- **Rigging gives abilities no natural creature can have.**
- **Stray:** a creature nobody has caught.
- The undercity goes with rigs, chemicals and experimentation; the city above with breeding and
  keeping creatures natural and purebred.

**Built** (`Classes/Tiers.cs`: `TierBook`, `InfusionBook`), on the lab and breeding as they were:
- **The thoroughbred bonus, from its species** (`TierBook.ThoroughbredOf`): its form's ability (each
  of the 30 forms has one: a hound's Pack Blood, ATK +1 for each ally beside it and SPD +1; a
  drake's Dragon Hide, DEF +2; a wraith's Unseen, harder to hit and healing from its bites; a
  kelpie's Tide Runner, MOVE +1...) and a touch of its element's blood (ember ATK +1, stone DEF +1,
  spark SPD +1, leaf healing...). Counted like a trait whatever it trains as. Your creatures and
  rivals' count as caught; wild packs are strays and get none.
- **Infused:** infusions brewed at the lab (`InfusionBook`, 30 chem each, two slots, a third washes
  out the oldest, not passed down), each an upside and a trade-off: Rage Serum (ATK +2, DEF -1),
  Iron Tincture (DEF +2, SPD -1), Quickblood (SPD +2, max HP -10%), Bulk Tonic (max HP +20%, SPD -1),
  Keen Drops (+12% to hit, 4% easier to hit), Ghost Vapour (10% harder to hit, DEF -1), Stride
  Elixir (MOVE +1, max HP -10%), Mending Draught (heals 4% a turn, ATK -1). New blood (the lab's
  chemborn, runic or abyssal for a natural creature) counts too, and its line carries it.
- **Chimera:** it carries the abilities of other species (`TierBook.ChimeraOf`): its own form's,
  plus up to two more (`Species.Spliced`). Bred across bloods (or from a chimera), it takes the
  other parent's kind and what its parents carried; at the lab (50 chem) you splice in the ability
  of any kind of creature you've met (`Ranch.Forms`); a grafted part makes one too. Its line
  carries the abilities. It trades the thoroughbred's element touch for them. **A spliced ability
  opens that form's class** (the user's pick, 2026-11-02: a chimera with the hydra's Many Heads can
  train as a Hydrarch).
- **Rigged:** any augment, mechanical or chemical: each teaches an attack no natural creature can
  learn (Piston Punch, Acid Spray...). A rival's machine grafts count.
- **Passing down.** New blood and chimera DNA (crossed bloods, spliced abilities) pass to the child;
  infusions, grafted parts and augments don't. Two natural creatures bred together make a
  thoroughbred.
- **Standing.** Breeding a chimera moves the undercity instead of the city; new blood moves it as
  an augment does; each infusion a little (`Standing.Infusion`); the first splice that makes a
  chimera a little more (`Standing.Splice`).
- **On screen.** A tier chip (crown, flask, helix, gear, skull; a paw for strays) on the pens'
  detail and the battle panel; the pens list a creature's spliced abilities, infusions and
  augments as chips and its species bonus or chimera abilities among its passives. The lab runs
  from the least change to the most (infusions, new blood, splices, augments, part grafts), shows
  the tier on the chamber, and says when it changes ("It's a chimera now, no longer a
  thoroughbred.").
- **Old saves.** Creatures caught wild and given new blood at the lab are found and marked infused
  (their species, regenerated from its seed, has other blood). Ones bred before tiers can't be told
  apart and stay as they are.
- `--tiers` checks it all headless (every form has an ability of its own).

**Defaulted, open to change:**
- [ ] The numbers: each form's ability, each element's touch, each infusion's trade-off.
- [ ] Does an abomination get something of its own (a passive, a look), or is the name enough?
- [ ] Rigs that change how it moves (wings to fly, climbing claws), from the call's first draft?
- [ ] Should the tiers bring costs of their own, like the first draft's (an infusion resting a
  mission, a rig's upkeep, a cap on growth)? None are built.
- [ ] Call wild creatures strays everywhere? (Now only their tier chip says so.)

## Tamers and the party (the user's direction, 2026-11-02)

**Name:** **Tamer**, the user's pick (2026-11-02): "something friendlier to the creatures, something
that suggests you collect and work with them, like trainer for Pokemon". It replaced "general" and the
working name "warden", and matches the rival tamers planned for later.

**Decided by the user:**
- The player is a tamer. A tamer leads up to **three creatures**.
- Tamers are **not on the field**. They give **passive boosts** to the creatures they lead.
- A **second party member** joins in the story, so two tamers field up to **six creatures**.
- More party members join later, **each with their own specialties and passives**.
- At most **two tamers and six creatures** go into a battle. Tamers and creatures can be swapped
  between battles.

**Defaulted for the first version (2026-11-02), open to change:**
- Creatures belong to the ranch and are assigned to a tamer before each battle.
- Each tamer has one specialty: a passive boost to the creatures they lead, as data
  (`Tamers.cs`), like skills and passives.
- Until the story exists, other tamers join on a schedule of encounters (a placeholder for the
  story beats that will bring them).

**Built (first version, 2026-11-02):** six tamers as data in `scripts/Party/Tamers.cs`: You
(Steady Hand: ATK and DEF +1 for all), the Beastmaster (natural creatures), the Tinker (clockwork
and brass), the Chemist (chemborn and abyssal), the Arcanist (runic or aura) and the Drillmaster
(MOVE and SPD for all), joining at encounters 3, 6, 9, 12 and 15. The ranch's Party panel (P)
fields up to two and fills their three slots each; battles apply the boosts before the first turn.

**Open:**
- [x] The name: **Tamer**.
- [ ] Do tamers grow (levels, more boosts, a tree of their own)?
- [ ] Who each party member is, and how they join (the story).

## The world, missions and the shop (the user's direction, 2026-11-02; the map is built)

**Decided by the user:**
- **Many kinds of missions:** funny, pun-filled **dispatch** missions (send creatures off; they
  come back later with a result, as in FFTA), **repeatable** missions, and missions in **very
  different locations** whose terrain all looks very different.
- **A country map** that slowly unlocks as main story missions are completed. **Main story
  missions** become available as the player hits other milestones, so the story can't be rushed.
- **Coliseum missions,** like FFTA's tournament.
- **Wild encounters that wander the map,** like FFTA's: packs roaming between locations, met by
  travelling into them.
- **A shop:** equipment for your team, and items that help lure and catch wild creatures.
- **Sound** (begun 2026-11-02; effects built). **Music** (built 2026-11-02, the user's pick):
  six looping tracks made in code like the sound effects (`Audio/Songs.cs`, `Audio/Music.cs`):
  Home Pasture (the ranch: a flute over a harp, F major), The Open Road (the map: a marching horn
  call), Wild Clash (battles in the wilds: driving E minor), Brass and Banners (the city above: a
  brass fanfare with bells, timpani and clockwork ticks), Neon Mire (the undercity: a dark
  phrygian groove on saw bass with echoing arpeggios) and Rival's Gambit (duels and the coliseum:
  fast harmonic minor). The two sides are heard apart, as they're seen apart.
  **Open:** [ ] the user's ear on all of it (made without listening: levels and balance were
  measured, not heard); [ ] more tracks (main missions, the endings, the lab, a title screen);
  [ ] a separate music volume or mute key; [ ] tracks per region (the far regions borrow the
  wilds' battle track).

**Built (2026-11-02): the country map** (`scripts/World/`, the user's pick to build first).
- `Country.cs`: eight places as data (the ranch, three in the city above, three in the undercity,
  the cavern in the wilds), each with a battle map and a side, and nine roads (a day each; a lift
  joins the Brass Bridges to the Foundry). `CountryArt` paints the map in code.
- `CountryScreen` (M at the ranch): pick a place to see what's on offer there; Depart travels
  along the roads (the crown walks them, days pass) and starts the battle. Locked places show as
  "???" and say which main mission opens them and what it waits for.
- What's on offer: one or two contracts at every open place, a rival's duel (on their home ground
  if it's open), and the main missions that are ready.
- `Story.cs`: five main missions, each at a place, each opening the next place, each waiting for
  the one before and for milestones: contracts won, creatures at the ranch, codex parts, rival
  duels won. They say only what they open; the story is still to come.
- Saved: where you are, the day, contracts won, main missions won. Old saves start at the ranch.
- `--country=N` plays the loop headless (main missions when ready, a duel when one is needed,
  else the easiest nearby contract) and prints when each road opens. On 2026-11-02 four of the
  five main missions were won in 40 battles, the third waiting on a rival duel won.

**Suggested order** (each builds on the one before):
1. ~~**The country map**~~ (built, above).
2. **Story milestones and flags:** a data-driven list of story missions, each with conditions
   (milestones met, flags, standing with the city or undercity) and outcomes (flags, unlocked
   locations, narrative-only creatures). Cutscenes come after.
3. ~~**Mission variety**~~ (built 2026-11-02, the user's pick):
   - **New regions** with ground that looks nothing alike: Frostfang Pass (snow, blue rock, a
     frozen lake), Sunscorch Dunes (sand that slows walkers, sandstone ruins, an oasis), Cinder
     Rift (ash and basalt over lava only fliers cross), Saltwind Coast (beach, piers, the sea) and
     the Grand Coliseum. New terrains: ice, sand, lava; new looks: Frost, Desert, Volcanic, Coast.
     Four new main missions open them, branching off the road (so the chain is now a tree).
   - **Dispatches** (`World/Jobs.cs`, 18 so far, pun titles): send one creature off for 1 to 3
     days; the better it suits the job (three things each job likes), the likelier it goes well
     (30% plus 22 for each). It comes back with chem and JP if it went well, a little JP and a
     funny line if not. Nobody is ever lost (defaulted). A battle now takes a day.
   - **Regular jobs:** every place has one, always on offer, a fresh pack each time, three
     quarters of the pay.
   - **The Grand Coliseum** (opens with the Brass Bridges): Bronze, Silver and Gold Cups, each
     three duels in a row against named contenders; your own squad; prizes 80, 140 and 220 chem.
     Contenders are trained to a set level per cup, so a cup gets easier as you grow.
4. ~~**Wandering encounters**~~ (built 2026-11-02, the user's pick): up to three packs roam the
   open places, moving on most days; travel is a day a road, and arriving where a pack is starts
   a rout against it (recruiting allowed). You can also go after one from its place's list. They
   show on the map as their leader.
5. ~~**The shop**~~ (built 2026-11-02, the user's pick): **chem** is the currency (defaulted: one
   currency, so the lab and the shops compete for it). Each place sells its own stock; some wares
   appear only after a main mission or a cup; you buy where you stand. **Gear**: one slot per
   creature, 18 pieces (fine things in the city above, chem and machinery in the undercity, gear
   for the weather in the far country), each a small passive. **Items**, used in battle from the
   command bar: snare nets (catch a worn-down wild creature, 60%), soothing chimes (root one, to
   corner the skittish), tonics, smelling salts, and extra glowberries.

**Open:**
- [ ] Equipment: one accessory slot per creature, or more? Does it show on the sprite?
  (Defaulted: one slot, not drawn yet.)
- [x] Shop currency: **chem** (defaulted; easy to split into a coin later).
- [ ] Dispatch: can a dispatched creature be lost, or only come back empty-handed? (Defaulted:
  never lost.)
- [ ] Coliseum: your own squad only, or loaner squads; prizes? (Defaulted: your own squad; chem.
  A trophy or a unique creature would be good prizes.)
- [x] Rival balance (eased 2026-11-02, the user's pick): rivals train a little behind you and
  never past your squad's average JP, and each duel lost to one in a row makes them cocky (one
  creature fewer, then two). Against a trained squad you win 43% straight, 69% after a loss, 91%
  after two. In 70-battle headless runs every path (city, undercity, peace) wins its two rival
  duels and reaches its ending. Before, the headless squad never changed class (a test harness
  gap, fixed) and won about one duel in ten.

## Title, save slots and settings (the user's pick, 2026-11-02; built)

A title screen (the base drifting behind the name) with Continue, New game, Load, Settings and
Quit; three save slots, each summarised (place, day, roster, chem, main missions, when saved),
with overwriting and deleting asking twice; the old save is slot 1. Settings: music and effects
volume, mute, cutscene text speed, a battle turn timer (off by default), fullscreen; kept in
`user://settings.cfg`. Esc at the base opens a pause menu.

**Open:**
- [ ] More settings: key rebinding, a colour-blind palette for the side rings, screen shake?
- [ ] Autosave on every return home (now: saves on station use, battles, and leaving)?
- [ ] Naming a save, or a portrait of your squad on each slot?

## The home base (the user's direction, 2026-11-02; built)

**The user's words:** all menus and systems grounded in a home base, where systems and areas
unlock through the campaign; the player goes there to breed, switch equipment, graft and so on,
walking around the base to the crafting areas.

**Built:** a farmyard you walk around as your character (click or WASD); seven stations, each
opening one part of the old ranch screen: the gate (the country map), the pens (roster, squad,
names), the training yard (jobs), the armory (gear), the breeding vats, the war table (tamers),
the lab. Your creatures wander the pens. Unbuilt stations stand in scaffolding with a sign.

**Decided by the user (2026-11-02):** keep the unlock order: gate, pens and yard from the start;
the armory after a contract; the vats after two; the war table when a second tamer joins; the lab
after the road to the Foundry. (Defaulted: the farmhouse and the lab are closed buildings for now.)

**Open:**
- [x] The unlock order (kept by the user, 2026-11-02). Story beats may replace the counts once there is a story.
- [ ] More stations: a dispatch board, a trophy hall for cups and rival titles, a shop or trader
  at home, a codex wall, the Sprite Lab as a breeder's studio?
- [ ] Interiors (walking into the farmhouse and the lab), and the base growing visibly (upgrades).
- [ ] Should tamers stand around the base (and talk)?

## Story flags and standing (the user's pick, 2026-11-02; built as machinery)

Built: standing with the two sides, choices after main missions, story flags, the three endings
gated by where standing leans, rumoured creatures per path, and shop discounts. The details and
the numbers are in `story-notes.md` ("Built so far"). All wording is placeholder for the story.

**Open:**
- [ ] Standing rises quickly (a side reaches 100 within about 40 battles if you favour it): fine,
  or halve the per-contract amount?
- [ ] Should the far side react to low standing (dearer shops, a side's contracts drying up,
  rival tamers taking sides)?

## Traits, augments and names (the user's direction, 2026-11-02; built)

**Decided by the user:**
- **Players can name their creatures.**
- **More mech and steampunk aspects for creatures** (more brass, gears, pistons and machine parts
  in the sculptor).
- **Breedable traits:** sensible, biological traits passed on by breeding, coloured as such
  (natural tones). They lean toward the city-above side of the campaign.
- **Augments:** mechanical and chemical, bought from certain shops and unlocked by certain missions,
  neon and metal in colour, bringing more chemical and weapon-like attacks. They lean toward the
  undercity side.

**Built (2026-11-02):**
- **Names:** Name on the ranch.
- **Traits** (`Classes/Traits.cs`): 14 biological quirks (Leathery Skin, Keen Nose, Swift,
  Strong Jaw, Iron Stomach, Hard Head, Night Eyes, Quick Healer, Big Heart, Hunter's Instinct,
  Herd-Born, Sure-Footed, Strong Swimmer, Stout), each a small passive, up to two a creature,
  shown in natural green. Wild creatures are born with them (about half have one, one in eight
  two), so recruits bring theirs. Bred, each parent's trait passes down half the time; a child
  that gets none sometimes shows a new one (about one in six).
- **Augments** (`Classes/Augments.cs`): 8, four mechanical (Piston Fist, Rivet Gun, Lens Visor,
  Crystal Core) and four chemical (Acid Canister, Chem Veins, Strobe Tips, Drip Feed). Each bolts
  on a graft (its look, drawn by the sculptor, and its stats) and teaches an attack (Piston
  Punch, Rivet Volley, Targeting Beam, Core Discharge, Acid Spray, Chem Rush, Strobe Flash,
  Toxic Drip). Sold only at undercity shops (the Foundry, Lantern Row, the Glowmire), some after a
  main mission or a cup; fitted at the lab, which no longer sells brass directly. Never
  inherited: breeding reads the parents as they were born.
- **Steamwork** (the sculptor's new steampunk parts): gears and pipework with a pressure gauge on
  the flank, twin pistons and a steaming smokestack on the back, all moving with the idle. New
  clockwork species carry up to two (one each place); four more augments fit them to anyone:
  Gear Grinder (Gear Grind: chews armour), Steam Pistons (Piston Charge: a leaping, knocking
  charge), Smokestack (Smoke Screen: it and allies beside it harder to hit), Pressure Pipes
  (Steam Vent: scalds and knocks back every foe beside it).

**Defaulted, open to change:**
- [x] Augment slots: **two** per creature; a third scraps the oldest (no removing them back).
- [x] Traits: pass on, and **sometimes a new one appears** in a child that inherited none.
- [ ] Does using augments (and breeding traits) shift your standing with the two sides? Waiting on
  standing itself (story flags and alignment, not built).

## Rival tamers (the user's pick, 2026-11-02)

**Decided by the user:** build rival tamers next (2026-11-02). The rest was defaulted to build a
first version, and is open to change.

**Built (first version, 2026-11-02):** `scripts/Party/Rivals.cs`.
- Five rivals as data, each breeding toward an ideal: the Brass Baron (armoured clockwork, from
  encounter 2), the Chem Witch (chem-filled creatures, 4), the Huntress (natural predators, 6),
  the Occultist (arcane beasts and auras, 8) and the Wyrm Lord (the biggest beasts, 10). The titles
  are placeholders for characters the story will bring, like your own party's.
- Each leads from off the field with a boost of their own (a `TamerDef`, like yours), and fields
  species to their taste, given their blood (infusions) and machine grafts, trained in their
  favourite classes.
- A **duel** contract takes the board's last card on a rival's debut and on every other board
  after (they take turns). They field as many creatures as you bring, three at least, six at most.
- A tamer's creatures fight as trained (no temper-driven fleeing) and **can't be recruited**.
- A duel still undecided after **20 rounds** goes to the side with more of its HP left (a tie to the
  rival), so two walls of shields can't stall forever. PvP will want the same rule.
- Winning pays more chem than a contract, puts their parts in your codex, and counts: each win
  makes their squad train harder and fits their grafts to one more creature.
- **Eased (2026-11-02):** they train a little behind you (10 JP per encounter, never past your
  squad's average; their ace +10, +10 per win), and losing makes them cocky: each duel you lose
  to a rival in a row, they bring one creature fewer next time, then two (never under two). Your
  first win resets it. The card shows "cocky: 1 fewer". Coliseum contenders are built the same
  way, so cups train at 10 JP per level too (they don't get cocky: a cup is three rounds and out).
- **Prizes (the user's pick, 2026-11-02):** asked whether the prize should be an egg, a graft or a
  title, the user chose all of them in a list with rarity, and a random prize on every win (repeat
  wins still pay chem as before). Each rival has a hoard (`RivalDef.Prizes`); a win draws a rarity
  (common 57%, uncommon 30%, rare 12%, very rare 1%: the user found 3% too common for a shiny),
  then one of its prizes:
  - common: items for the bag (tonics, smelling salts, snare nets, chimes, glowberries) or a purse
    of chem;
  - uncommon: their signature gear (Brass Gorget, Witch's Phial, Fang Necklace, Occult Sigil,
    Wyrmscale Mantle: a little better than shop gear, never sold), one of their augments (the
    Baron's Rivet Gun or Lens Visor, the Witch's Acid Canister or Drip Feed, the Occultist's
    Crystal Core or Strobe Tips; the Huntress and the Wyrm Lord give items instead), or a title
    (won once, shown on the ranch: Gearbreaker, Witchbane, Apex Tamer, Sigilbreaker, Wyrmtamer,
    placeholders for the story);
  - rare: an egg from their line: two species to their taste, given their blood, bred together,
    born with a trait or two (machine grafts never pass down, so the Baron's eggs aren't
    clockwork);
  - very rare: the same egg, shiny.
  The duel card lists what's still to be won.

**Open:**
- [ ] Who the rivals are (names, looks, why they breed what they breed): the story.
- [x] Should beating a rival give something unique? Yes: a random prize every win, by rarity (above).
- [x] The shiny egg: 1 in 100 a win (the user's call, 2026-11-02).
- [ ] The rest of the odds and hoards are defaulted: is the egg (12%) too rare or too common?
- [ ] How often they come back (now: every other board, taking turns), and whether you can refuse.
- [ ] The round limit and its tie-break (defaulted, for duels and later PvP).

## Online PvP battles (a goal, the user's direction, 2026-11-02)

**Decided by the user:** casual matches, turn timers, each player using their own squad (tamers
and creatures from their ranch), **only one shiny fielded at a time** (2026-11-02), and
**matchmaking, kept basic for now** (2026-11-02). More to come later.

**What already helps:** the rules live in `Battle`, apart from any drawing, and every roll comes
from `SeededRng`. Two machines that start from the same seed and apply the same commands reach
the same state, so a match can send just the commands (move here, use this skill there) and let
each side replay them: lockstep, the usual way turn-based games go online.

**Built (2026-11-02):**
- Commands as data: `Command` in `Tactics/Commands.cs` (move, attack, skill, berry, undo, flee,
  end turn). The screen and the AI both act only through `Battle.Order`; `Battle.History` keeps
  them.
- A determinism check: `Battle.StateHash` after every turn, and `--replaytest`, which rebuilds 14
  battles from their commands and compares hashes turn by turn (all match). Floating point is
  still the risk across machines; the rules use little of it, and what they use should be made
  integer before matches go online.
- A turn timer: `BattleScreen.TurnSeconds` (a clock chip counts down; the turn ends at zero),
  `--timer=N` to try it.

**Matchmaking plan (basic, 2026-11-02; not built):**
- One "Quick match" button. The game connects to a small matchmaking server over WebSocket and
  waits in a single queue; the server pairs the first two players who are waiting. No ranks,
  ratings, regions or lobbies yet.
- Once paired, the server sends both players a match seed and who goes first, then relays each
  side's commands to the other (lockstep, as above). Relaying through the server avoids NAT and
  port-forwarding trouble and lets it check squads (one shiny, six creatures, two tamers).
- Each side sends its squad (tamers, creatures, training) when the match starts; the battle is
  built from both squads and the seed, then only commands travel.
- After every turn both sides send `StateHash`; a mismatch ends the match as a draw and logs it.
- Later, not now: friend codes or private rooms, a rating, reconnecting after a drop, spectating.

**What it will still need:**
- The server itself (a small one: a queue and a relay) and Godot's `WebSocketPeer` on the client.
- PvP rules: two tamers and six creatures a side, turn timers, disconnects, no recruiting.
- Balance: classes, skills and grafts tuned for fairness, not only against wild packs.

**Open:**
- [x] Ranked or casual? **Casual.**
- [x] Squads? **Your own** tamers and creatures.
- [x] Pace? **Turn timers.**
- [x] Matchmaking or friend codes? **Matchmaking**, basic: one queue, first two paired.
- [x] Shinies? **One** fielded per side.
- [ ] Timer length? What happens on a disconnect?

## Shinies (the user's direction, 2026-11-02)

**Decided by the user:**
- **Much rarer:** 1 in 512 for now (was 1 in 24), easy to change.
- **The reveal:** when a battle starts, the camera pans to each shiny and shows a glint animation.

- **They matter:** a shiny has a special unique skill, a second passive on top of its equipped one
  (2026-11-02).
- **PvP:** only one shiny fielded at a time (2026-11-02).

**Built (2026-11-02):** a palette swap (`PaletteSpec.Shiny`), the reveal above, a "shiny" tag in the
ranch detail, and a **shiny passive**: one of eleven (`ClassBook.ShinyPassives`: Prismatic Hide,
Gleaming Fang, Radiant Blood, Starlit Mind, Mirror Scales, Lucky Glint, Unbroken, Echoing Light,
Moonstep, Thirsting Shine, Gilded Heart), picked by the creature's seed so it never changes, in
effect alongside the equipped passive whatever it trains as, with a gold-framed icon. Wild and
rival shinies have theirs too. The one-shiny PvP rule is a squad check (`PvpRules.Problems`) for
the matchmaking to call.

**Open:**
- [x] Only cosmetic, or do they matter? **A unique second passive.**
- [ ] Inheritance: can breeding pass it on, or raise the odds?
- [ ] Hunting: sightings of shinies on the mission board, a codex of shiny species.
