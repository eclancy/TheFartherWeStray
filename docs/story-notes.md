# Story: notes to start iterating on

_Status: **not started**. Opened 2026-11-01 at the user's request. The user sets the themes;
nothing beyond what's marked as the user's direction is decided._

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

## The user's direction (2026-11-02): the player is a general

The story revolves around **the player, who leads creatures as a general**. This replaces the
earlier "no human unit" assumption. See `design-notes.md` for the mechanics as they stand:

- The player starts with **three creatures**.
- The player **might or might not be on the field**, but always acts as a general: boosting the
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
- [x] Who is the player? **A general who leads creatures** (2026-11-02). Still open: their
      background (a ranger, a lab-runner, a rancher from the undercity?).
- [ ] Who are the other party members, and how does each join?
- [ ] Who opposes them? Rival ranches or generals, a guild, the people behind the chem, something
      abyssal?
- [ ] Do the creatures have voices or personalities in cutscenes beyond their attitude?
- [ ] Linear campaign, mission board with story missions, or a mix? (Section 2 asks this too.)
- [ ] Does the story explain the procedural species, as the "frontier" and "mutating world"
      seeds both do?

## Tech the story will need (when it's time)

- **Cutscene format:** a data-driven script (actors, moves, facings, emotes, lines,
  camera, waits) played on any battle map. Same rule as skills: content is data, not code.
- **Sprite acting:** walk, hop, turn and idle exist; action poses are being built. Still needed:
  emote icons (! ? ...), celebrate, and portraits for speakers. Human generals need their own
  sprites (the creature sculptor could build them).
- **Dialogue box and portraits:** generated creatures can use a crop of their own sprite.
- **Mission board:** a list of contracts, each a map plus a pack plus an objective, with an
  optional scene before and after.
- **Story state:** flags in the ranch save.

## Suggested first iteration

1. The user writes a one-page premise and two or three themes.
2. Turn that into a cast list (the player and the first party members) and three story missions.
3. Build the cutscene player on the foundry map with one short scene: an opening where a wild
   pack is spotted, so the first battle has a reason.
