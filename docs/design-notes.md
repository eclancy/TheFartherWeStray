# Design notes: decided directions and things to think on

_Systems the user has asked for that aren't built yet (generals and the party, online PvP,
shinies). Each says what's decided (the user's
words) and what's still open. Story themes live in `story-notes.md`._

## Generals and the party (the user's direction, 2026-11-02)

**Decided:**
- The player is a **general**. A general leads up to **three creatures**.
- A general **may or may not be on the field**, but always shapes the battle: boosting the team's
  effects or giving creatures altered abilities.
- A **second party member** joins in the story, so two generals field up to **six creatures**.
- More party members join later, **each with their own specialties and passives**.
- At most **two characters and six creatures** take the field. Characters and creatures can be
  swapped between battles.

**Open:**
- [ ] On the field or not: is it per general, per mission, or a choice? If on the field, can they
      be hit, and what happens if they fall?
- [ ] What a general does in battle: passive auras only, or active commands too (FFTA's Judge-like
      calls, a once-per-battle order, a cooldown skill)? Does using one cost the general's turn?
- [ ] "Altered abilities": swapping a creature's skill for a variant, adding an effect to its
      skills, or unlocking skills it couldn't otherwise use?
- [ ] Do creatures belong to a general (three each) or to the ranch, assigned before battle?
- [ ] Do generals level up or have their own job tree, the way creatures do?
- [ ] Specialties to start with, e.g. a beastmaster (boosts natural creatures), a tinker (boosts
      clockwork grafts), a chemist (chem and poisons), an arcanist (runes and auras).

**What changes in the current game:** the squad is one group of four today (`Ranch.SquadSize`).
It becomes three per general, six with two. Mission rewards and recruiting stay the same.

## Online PvP battles (a goal, the user's direction, 2026-11-02)

**Decided:** online player-versus-player battles are a goal: your generals and creatures against
another player's.

**What already helps:** the rules live in `Battle`, apart from any drawing, and every roll comes
from `SeededRng`. Two machines that start from the same seed and apply the same commands reach
the same state, so a match can send just the commands (move here, use this skill there) and let
each side replay them: lockstep, the usual way turn-based games go online.

**What it will need:**
- Commands as data: every player action a small serialisable record, and `Battle` driven only by
  those (today the screen calls `Battle` methods directly).
- A determinism check: both sides hash the battle state after each turn and compare. Floating
  point is the risk; the rules use little of it, and what they use should be rounded or made
  integer before matches go online.
- Matchmaking and transport: Godot's high-level multiplayer (ENet or WebSocket) for direct or
  relayed matches, or a small server. A server also stops cheating on squads.
- PvP rules: squad limits (two generals, six creatures, as decided above), turn timers,
  disconnects, and no recruiting.
- Balance: classes, skills and grafts tuned for fairness, not only against wild packs.

**Open:**
- [ ] Ranked or casual? Matchmaking or friend codes?
- [ ] Squads from your own ranch, or a draft or rental mode so bred creatures don't decide every
      match?
- [ ] Real-time-ish (turn timers) or asynchronous (play your turn when you like)?

## Shinies (to think on, 2026-11-02)

**Today:** a creature is shiny 1 time in 512 (`Creature.Generate`; the user asked for "much
rarer" than the first 1 in 24 on 2026-11-02). A shiny is only a palette swap
(`PaletteSpec.Shiny`) and a "shiny" tag in the ranch detail.

**The user wants shinies as a real feature.** Open questions:
- [x] How rare? **Much rarer** (the user, 2026-11-02): 1 in 512 for now, easy to change.
- [ ] How they show: a sparkle when one appears in battle, an animated glint in the idle, a mark
      on the card and in the codex.
- [ ] Are they only cosmetic, or do they matter (a stat edge, a unique skill, a rarer form)?
- [ ] Inheritance: can breeding pass it on, or raise the odds?
- [ ] Hunting: sightings of shinies on the mission board, a codex of shiny species.
