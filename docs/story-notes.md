# Story: notes to start iterating on

_Status: **not started**, but the user has set the themes (2026-11-02, below). Opened 2026-11-01
at the user's request. Nothing beyond what's marked as the user's direction is decided._

## The user's themes (2026-11-02)

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
- **The two sides in the creatures** (2026-11-02): biological, breedable traits belong with the
  city above; mechanical and chemical augments (neon and metal, chemical and weapon-like attacks)
  belong with the undercity. See `design-notes.md`, Traits and augments.

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
- **Strains need a reason in the lore:** why clockwork grafts, chemtech mutants, arcane-marked
  beasts and abyssal horrors exist, and who makes them. The lab and infusions are the player's
  side of that.
- **Story seeds** from `game-ideas.md` section 4: frontier rangers (new species keep appearing),
  rival ranches (clans breeding toward an ideal), a mutating world (regions change between
  visits).

## Open questions for the user

- [ ] The themes. What is the story *about*? (Ambition vs. nature? Who owns a creature? What the
      upper city takes from the lower?)
- [x] Who is the player? **A tamer who leads creatures** (2026-11-02). Still open: their
      background (a ranger, a lab-runner, a rancher from the undercity?).
- [ ] Who are the other party members, and how does each join?
- [ ] Who opposes them? Rival ranches or tamers (five placeholder rival tamers are built, in
      `Rivals.cs`), a guild, the people behind the chem, something abyssal?
- [ ] Do the creatures have voices or personalities in cutscenes beyond their attitude?
- [x] Linear campaign, mission board with story missions, or a mix? **A mix:** story missions
      appear on the map as milestones are met, among contracts, dispatches, duels and more.
- [ ] Does the story explain the procedural species, as the "frontier" and "mutating world"
      seeds both do?

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

1. The user writes a one-page premise and two or three themes.
2. Turn that into a cast list (the player and the first party members) and three story missions.
3. Build the cutscene player on the foundry map with one short scene: an opening where a wild
   pack is spotted, so the first battle has a reason.
