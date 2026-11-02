# Design notes: decided directions and things to think on

_Systems the user has asked for: tamers (first called generals) and the party, online PvP, shinies. Each
says what's decided (the user's words), what was defaulted to build a first version, and what's
still open. Story themes live in `story-notes.md`; the user will come back to the story later._

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
and brass), the Chemist (chemtech and abyssal), the Arcanist (arcane or aura) and the Drillmaster
(MOVE and SPD for all), joining at encounters 3, 6, 9, 12 and 15. The ranch's Party panel (P)
fields up to two and fills their three slots each; battles apply the boosts before the first turn.

**Open:**
- [x] The name: **Tamer**.
- [ ] Do tamers grow (levels, more boosts, a tree of their own)?
- [ ] Who each party member is, and how they join (the story).

## The world, missions and the shop (the user's direction, 2026-11-02; not built yet)

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
- **Sound** (begun 2026-11-02, see below).

**Suggested order** (each builds on the one before):
1. **The country map:** locations (each a map and a region), roads between them, travel taking a
   day, unlocking by story flags. The ranch becomes one location; the mission board moves onto
   the map (missions sit at locations).
2. **Story milestones and flags:** a data-driven list of story missions, each with conditions
   (milestones met, flags, standing with the city or undercity) and outcomes (flags, unlocked
   locations, narrative-only creatures). Cutscenes come after.
3. **Mission variety:** dispatch missions (data: a pun title, the ask, which creatures suit it,
   days away, rewards), repeatable contracts at locations, new regions with new tile sets
   (snow, desert, forest, coast, volcanic...), and a coliseum with ranked brackets of rival
   squads.
4. **Wandering encounters:** wild packs moving along roads each day; walking into one starts a
   battle on that location's map.
5. **The shop:** a currency (chem, or coin earned alongside it), equipment (a new slot on each
   creature, on top of parts as gear), and catching items (lures for each temper, nets, calming
   incense), all as data.

**Open:**
- [ ] Equipment: one accessory slot per creature, or more? Does it show on the sprite?
- [ ] Shop currency: chem (already earned), or a separate coin?
- [ ] Dispatch: can a dispatched creature be lost, or only come back empty-handed?
- [ ] Coliseum: your own squad only, or loaner squads; prizes?

## Traits, augments and names (the user's direction, 2026-11-02; not built yet)

**Decided by the user:**
- **Players can name their creatures.**
- **More mech and steampunk aspects for creatures** (more brass, gears, pistons and machine parts
  in the sculptor).
- **Breedable traits:** sensible, biological traits passed on by breeding, coloured as such
  (natural tones). They lean toward the city-above side of the campaign.
- **Augments:** mechanical and chemical, bought from certain shops and unlocked by certain missions,
  neon and metal in colour, bringing more chemical and weapon-like attacks. They lean toward the
  undercity side.

**How it maps onto what exists:** the lab already has flesh grafts (parts from the codex), machine
grafts (brass, not inherited) and infusions (blood strains, inherited). Machine grafts and
chem infusions become the first **augments** (moved to undercity shops, gated by missions, with
skills of their own); breedable **traits** are a new inherited layer (a few per creature, from
its parents, e.g. thick hide, keen nose, venom glands), each with a small stat or a passive.

**Open:**
- [ ] Do augments take a slot each (how many), and can they be removed?
- [ ] Do traits mutate when bred, or only pass on?
- [ ] Does using augments shift your standing with the two sides?

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

**Open:**
- [ ] Who the rivals are (names, looks, why they breed what they breed): the story.
- [ ] Should beating a rival give something unique (an egg from their line, a graft, a title)?
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
