# Story: notes to start iterating on

_Status: **being set by the user**: the themes (2026-11-02) and the first questions answered
(2026-11-03, below). Opened 2026-11-01 at the user's request. Nothing beyond what's marked as the
user's direction is decided._

## The user's themes (2026-11-02)

- **The game is called The Farther We Stray.**
- **The city above is just as corrupt as the city below.** The story explores it: the gilded city's
  rot is as real as the undercity's, only better dressed.

- **Lighthearted to start.** Problems are going on all over the map, and tamers step in: some to
  help the public, some for nefarious purposes.
- **A glorious city and a nefarious undercity that hate each other**, like Arcane's Piltover and
  Zaun.
- **Branching plotlines.** Which plotlines advance depends on who the player aligns with, how they
  solve story missions, and the decisions they make in dialogue.
- **Narrative-gated creatures.** Some monsters can only be found and caught by taking certain
  narrative paths.
- **Story pacing by milestones.** Main story missions become available as the player hits other
  milestones (contracts done, creatures caught, rivals beaten...), so the story can't be blazed
  through while skipping the rest of the game.
- **A country map** that slowly unlocks as main story missions are completed (see
  `design-notes.md`, World map).
- **Three endings** (2026-11-02): **bringing justice to the undercity**, **bringing chaos to the
  main city**, or **finding peace between them**. Alignment, how story missions are solved and
  dialogue choices steer toward one.
- **Theme: industrial fantasy** (2026-11-02): a magic world in its industrial age, as in Arcane.
  The city above is a gilded golden age (royal, spires, old money, showy progress); the undercity
  is smog, pipes, alchemy and chemicals glowing in glass, with neon signs but no computers. Not
  neon punk's hacking side, and names not steampunk.
- **The two sides in the creatures** (2026-11-02): biological, breedable traits belong with the
  city above; mechanical and chemical augments (neon and metal, chemical and weapon-like attacks)
  belong with the undercity. See `design-notes.md`, Traits and augments.

## The user's answers (2026-11-03, first round)

Picked from options offered in a question round; the user's choices, not yet written out as story.

- **What it's about: what the city takes from below.** The city's golden age runs on the
  undercity's chem, labour and creatures. The player finds out the price, and who in both cities
  profits from it.
- **The player: raised in the undercity.** A chem runner or lab hand who got out with a few beasts
  and a grudge.
- **The opposition: someone in both cities.** A hidden power that profits from the feud and keeps
  it going, with hands in the city's council and the undercity's chem trade.
- **The player's voice: only through choices.** Silent in scenes (as the stand-in scene already
  is); the player speaks when picking a line at a choice.

Second round (2026-11-03):

- **The party: a mix.** Some people from your past, people from each side, and rivals won over.
  In the user's words: "They should be varied in appearance and goals and styles, helping push the
  story forward. They should each have aligned goals with the player, while having very different
  perspectives." The five placeholder tamers in `Party/Tamers.cs` keep their specialties until the
  story gives them people.
- **The beasts: emotes and gestures only.** Beasts never talk; in scenes they react with emote
  balloons and poses that fit their temperament.
- **The species: the world is changing.** Chem runoff and old magic change the wilds between
  visits, which is why new species keep turning up.
- **Looks: designed, with drawn portraits.** The story's characters get designed looks (not rolled
  ones) and a bigger portrait drawn for the dialogue box. The user added (2026-11-03): "The other
  characters also should have a few extra portraits showing expressions to suit the dialog", so
  each character has a set of portraits by expression, and a line picks the one that suits it.

Third round (2026-11-03):

- **The city connection is the friend from your past.** She stayed below but runs work up to the
  city, so she has contacts there, and after the Lantern Row job she brings you onto the city's
  board.
- **The grudge: the city's buyers.** Nobody below in particular: the city above, whose orders kept
  the labs running.
- **The rival won over: the Brass Baron** (at the Foundry, the end of act one).
- **The hidden power: an old-money house.** A family with members on the city's council and in the
  chem trade, each side passing for honest.
- **The reveal: the middle of the story.** Act two names the house, and the rest is about what you
  do with it.

## Built so far: the machinery, not the story (2026-11-02)

Everything below is placeholder wording for the story to replace; the mechanics are real.
- **Standing** with the city above and the undercity (`World/Standing.cs`, -100 to 100 each):
  contracts, regular jobs, main missions and dispatches raise standing with whoever the place's
  work is for (the wilds are nobody's); fitting an augment raises the undercity's, breeding the
  city's; coliseum cups the city's. At 50 a side's shops take 15% off.
- **Choices**: after each main mission, a choice of who hears first and gets the credit (the city,
  the undercity, or both across one table), shifting standing and setting a flag
  (`main_foundry:city`). The prompt is generic: the story will give each its own.
- **Story flags** (`Ranch.Flags`): choices, catches and scenes seen; nothing reads them yet but
  the rumours.
- **The cutscene player** (built 2026-11-02): scenes are written as plain lines in
  `scripts/Story/Scenes.cs` (`SceneBook`; the format is at the top of the file) and played on the
  battle maps with the cast as animated sprites, people included, dialogue with portraits, emotes,
  camera pans and fades. A main mission can play one before its battle. The one scene so far
  (`road_to_foundry`, before the first main mission) is a stand-in: a lookout tells you a wild
  pack blocks the road. Replace it, and every person's look, with the story's own.
  **Open:** [ ] how people look (now rolled per id and side: neon-punk in the undercity, royal in
  the city above, after the user's notes of 2026-11-02; the story's characters want designed looks); [x] does the player speak, or stay silent as many collectors do?
  **Only through choices** (2026-11-03): silent in scenes, a line picked at a choice; [x] portraits drawn for the box, or the sprite as now?
  **Drawn portraits, and designed looks** for the story's characters (2026-11-03), **with a few
  extra portraits each for expressions** to suit the dialogue (the user, 2026-11-03). Building it
  touches `Story/People.cs` (the portraits) and `CutsceneScreen.cs` (the dialogue box), and a
  scene line would name an expression. Still open: which expressions (for example calm, angry,
  smiling, shocked).
- **The three endings** (`end_justice` at the Brass Bridges, `end_chaos` at the Foundry,
  `end_peace` at Hexlight Hollow): once the last road is open and 15 contracts are won, the one
  your standing leans to appears. Justice wants 60 with the city and 30 over the undercity; chaos
  the same the other way; peace 40 with both and neither 30 ahead. Winning one records the ending
  (`Ranch.Ending`); you can play on.
- **Rumoured creatures** (the user's narrative-gated monsters): a rare creature per path, heard of
  at a place once standing allows and gone once caught: a prize-bred beast in the Gardens (city
  50), a lab escapee in the Glowmire (undercity 50), a creature of both worlds in the Hollow
  (balanced, 30 with both).

## The goal

A story with the animation, cutscenes and depth of FFTA. Battles and the ranch should feel like
they happen *somewhere*, to someone, for a reason.

What FFTA does that we want the equivalent of:
- **Cutscenes acted out by the sprites on the map:** characters walk, turn, emote, jump and
  gesture, with speech bubbles and portraits. They're cheap to make and say a lot.
- **A mission board with story missions threaded through it.** Optional contracts flesh out
  the world, and story beats gate progress.
- **A world with rules that shape battles:** FFTA's Judges and Laws. Our version could be the
  undercity's own rules.
- **Personal stakes:** a small cast whose relationships change over the campaign.

## The user's direction (2026-11-02): the player is a tamer

The story revolves around **the player, who leads creatures as a tamer** (first called a general;
the user named it Tamer on 2026-11-02, like a Pok\u00e9mon trainer). This replaces the
earlier "no human unit" assumption. See `design-notes.md` for the mechanics as they stand:

- The player starts with **three creatures**.
- The player **stays off the field** (the user's call, 2026-11-02) and acts as a tamer: boosting the
  team's effects or giving creatures altered abilities.
- Later in the story a **second party member** joins, so up to **six creatures** can fight.
- More party members join over time, **each with their own specialties and passives**.
- At most **two characters and six creatures** take the field; characters and creatures can be
  switched between battles.

## Already fixed by earlier decisions

- **Setting tone:** medieval high fantasy with steampunk and neonpunk, after Arcane. A brass city
  above (Piltover-like) and a chem-lit undercity below (Zaun-like). It can be dark and scary,
  balanced by natural wonder.
- **Strains need a reason in the lore:** why clockwork grafts, chemborn mutants, runic-marked
  beasts and abyssal horrors exist, and who makes them. The lab and infusions are the player's
  side of that.
- **Story seeds** from `game-ideas.md` section 4: frontier rangers (new species keep appearing),
  rival ranches (clans breeding toward an ideal), a mutating world (regions change between
  visits).

## Open questions for the user

- [x] The themes. What is the story *about*? **What the city takes from below** (2026-11-03).
- [x] Who is the player? **A tamer who leads creatures** (2026-11-02), **raised in the undercity**,
      a chem runner or lab hand who got out with a few beasts and a grudge (2026-11-03).
- [x] Who are the other party members, and how does each join? **A mix of people from your past,
      people from each side and rivals won over**, varied, with aims aligned to the player's and
      very different perspectives (2026-11-03). Still open: who each one is.
- [x] Who opposes them? **Someone in both cities**, a hidden power profiting from the feud
      (2026-11-03): **an old-money house** with members on the council and in the chem trade,
      named in the middle of the story. Still open: what the other rival tamers are to it.
- [x] Do the creatures have voices or personalities in cutscenes beyond their attitude? **Emotes
      and gestures only** (2026-11-03).
- [x] Linear campaign, mission board with story missions, or a mix? **A mix:** story missions
      appear on the map as milestones are met, among contracts, dispatches, duels and more.
- [x] Does the story explain the procedural species, as the "frontier" and "mutating world"
      seeds both do? **The world is changing:** chem runoff and old magic change the wilds
      between visits (2026-11-03).
- [ ] From the voice call (2026-11-02): **The Farther We Stray** as a theme or name (the stronger a
      creature gets, the farther it strays from what it was; uncaught creatures are strays). The
      user says it needs workshopping to feel more like the game's story. See `design-notes.md`,
      Ideas from the voice call.

## Act one (2026-11-03, the user approved these beats as a start)

_Drafted from the user's answers above and approved by the user as a start ("They work",
2026-11-03). Every person here is still a role, not yet a character: names, looks and lines are the
user's to give, and scenes wait for them (the questions at the end of this section)._

**The user's direction on it (2026-11-03): connections build up over time, and it all makes
logical sense.** In the user's words: "there should be a reason the city hires you. Like that one
of the other tamers mentions taking jobs from the city because she has connections. Then you can
use the gilded city as well as the undercity's mission boards, as they are two separate boards."
So every new place, board, ally and clue comes through someone you already know, for a reason
the story shows:

- **The undercity's board is yours from the start:** you're from there, and people below know you.
- **The city's board is a separate board, and it opens through a connection:** one of the tamers
  who joins you (in the user's example, "she") takes jobs from the city because she has
  connections there, and brings you in. Until then the city has no reason to hire you.
- **Each ally comes through the last link:** the friend from your past on an undercity job, the
  city-side ally on the first city job, the rival on the trail you're all following.
- **Each clue leads to the next place:** the changed strays near the Foundry lead to the city's
  gardens, the gardens' glow leads back down to the Foundry's chem, and its waste to the Glowmire.

**Premise.** You grew up in the undercity, running chem or working a lab, and got out with a few
beasts and a grudge. Now you work a farmyard out past both cities, taking the only jobs open to
you, the undercity's, until the people you meet open more. It starts light: odd jobs, rowdy strays, rival tamers showing off. But the strays are changing
faster than they should, and every trail you follow runs the same way. The city's golden age runs
on what it takes from below, and someone with a hand in both cities profits from keeping them at
each other's throats.

**Tone.** Light at the start (funny jobs, allies who see everything differently arguing it out),
darker by the end of the act, when the trail first points at someone in both cities.

**The beats, on the main missions that exist now** (`World/Story.cs`, in the order they open):

1. **A new game.** At the ranch: who you are and why you left, told by the people round you or a
   caption, since you speak only through choices.
2. **Lantern Row, `main_foundry`** (opens the Undercity Foundry; replaces the stand-in scene). A
   job brings you back to the streets you grew up in: strays near the Foundry are changing, and
   the locals blame its runoff. **Someone from your past** is there. She stayed when you left and
   wants the undercity fixed from inside. The same aim as yours (stop what's changing the strays),
   the opposite view (she doesn't trust anyone who left, or anyone from above). She joins as the
   second tamer about here (today's placeholder join is at encounter 3). After the battle, the
   credit choice becomes who you tell about the changed strays.
3. **The city's board opens.** The friend from your past runs work up to the city, so she has
   contacts there. After the Lantern Row job she mentions she takes jobs from up there, and brings
   you in: from now on the city's board is open to you beside the undercity's. With your grudge
   against the city's buyers, taking their money is a choice of its own.
4. **Gilded Gardens, `main_bridges`** (a hunt; opens the Brass Bridges and the coliseum). Through
   her connections, the city above hires you to hunt a changed beast loose in its prize gardens.
   **Someone from the city side** works the same case: they believe the city is what it says it is, and that the
   undercity's labs are to blame. The same aim, the other side. The first glimpse of the price:
   what makes the gardens glow was shipped up from below.
5. **Undercity Foundry, `main_sump`** (opens the Glowmire; it already waits for a rival win). **A
   rival you've beaten**, the Brass Baron, turns up on the same trail and, beaten again or talked round, joins you.
   The trail leads down into the Glowmire, where the Foundry's waste runs and the strays change
   fastest. The act ends when the trail shows one hand on both ends, for example the same mark on
   the city's orders and on the undercity's chem. Someone profits from both, and from the feud:
   an old-money house, though you don't learn which until the middle of the story.

**The side roads that exist** (`main_coast` after the Foundry, `main_frost` after the Bridges)
could carry one ally's own story, or stay trips out where the world's changes show: old magic in
the wilds, chem runoff downstream.

**How it fits what's built.**
- Each beat is a main mission already on the map, and its milestones stay as they are, so the act
  can't be rushed.
- **Two boards (built 2026-11-03):** the undercity's (Lantern Row, the Foundry, the Glowmire) and
  work in the wilds are open from the start; the city's (Gilded Gardens, the Brass Bridges, the
  Clocktower Ruins, the coliseum) offers nothing until beat 3 (`Country.Hires`,
  `CityBoardOpenedBy`). Until her scene exists, winning the Lantern Row job (`main_foundry`) opens
  it. The map names each place's board, and a city place shows that its board is shut to you and
  what comes first. The city's places stay on the map to travel to and shop at.
- The credit choice after each mission becomes the story's own choice of who you tell and what,
  moving standing toward an ending.
- The three allies take three of the five tamer slots. Their specialties could match who they
  are (the friend from below as the Tinker or the Chemist, the city one as the Beastmaster), or
  the story gives them new ones.
- One of the five rival tamers becomes the ally; the others stay rivals for now.
- The rumoured creatures already on two of these paths (a prize-bred beast in the Gardens, a lab
  escapee in the Glowmire) could be the changed strays of beats 4 and 5, or their kin.

**For the user, before any scene is written:**
- [x] Do these beats work? **Yes, as a start** (2026-11-03).
- [x] The grudge: **the city's buyers**, whose orders kept the labs running. Still open: what
      happened when you got out.
- [ ] Each ally: name, look, what they want, how they see the world, and which specialty they take.
- [x] Which rival is won over? **The Brass Baron.** Still open: why the Baron is on the same trail.
- [x] Which tamer has the city connections that open its board? **The friend from your past,**
      who runs work up to the city. Still open: what work, and for whom.
- [x] The hidden power: **an old-money house** with members on the council and in the chem
      trade, **named in the middle of the story**. Still open: the house itself.
- [ ] What the credit choice means at each beat: who hears, and what follows.

## Tech the story will need (when it's time)

- **Cutscene format:** a data-driven script (actors, moves, facings, emotes, lines,
  camera, waits) played on any battle map. Same rule as skills: content is data, not code.
- **Sprite acting:** walk, hop, turn and idle exist; action poses are being built. Still needed:
  emote icons (! ? ...), celebrate, and portraits for speakers. Human tamers need their own
  sprites (the creature sculptor could build them).
- **Dialogue box and portraits:** generated creatures can use a crop of their own sprite.
- **Mission board:** a list of contracts, each a map plus a pack plus an objective, with an
  optional scene before and after.
- **Story state:** flags in the ranch save, plus standing with each side (city and undercity),
  the choices made in dialogue and how each story mission was solved, all read by data-driven
  conditions on missions, dialogue lines and where narrative-only creatures can appear.

## Suggested first iteration

1. ~~The user writes a one-page premise and two or three themes.~~ The user answered two rounds of
   questions instead (2026-11-03, above).
2. Turn that into a cast list (the player and the first party members) and three story missions:
   Act one (above), approved as a start (2026-11-03); its characters still need the user's details.
3. ~~Build the cutscene player~~ (built 2026-11-02, with a stand-in scene before the first main
   mission: a wild pack blocks the road to the Foundry).
