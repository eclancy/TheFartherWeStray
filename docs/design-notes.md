# Design notes: decided directions and things to think on

_Systems the user has asked for: wardens (the generals) and the party, online PvP, shinies. Each
says what's decided (the user's words), what was defaulted to build a first version, and what's
still open. Story themes live in `story-notes.md`; the user will come back to the story later._

## Wardens and the party (the user's direction, 2026-11-02)

**Name:** the user wants a different name than "general". **Warden** is the working name in the
code and UI (other ideas offered: Handler, Keeper, Beastcaller, Marshal); rename when the user picks.

**Decided by the user:**
- The player is a warden. A warden leads up to **three creatures**.
- Wardens are **not on the field**. They give **passive boosts** to the creatures they lead.
- A **second party member** joins in the story, so two wardens field up to **six creatures**.
- More party members join later, **each with their own specialties and passives**.
- At most **two wardens and six creatures** go into a battle. Wardens and creatures can be swapped
  between battles.

**Defaulted for the first version (2026-11-02), open to change:**
- Creatures belong to the ranch and are assigned to a warden before each battle.
- Each warden has one specialty: a passive boost to the creatures they lead, as data
  (`Wardens.cs`), like skills and passives.
- Until the story exists, other wardens join on a schedule of encounters (a placeholder for the
  story beats that will bring them).

**Built (first version, 2026-11-02):** six wardens as data in `scripts/Party/Wardens.cs`: You
(Steady Hand: ATK and DEF +1 for all), the Beastmaster (natural creatures), the Tinker (clockwork
and brass), the Chemist (chemtech and abyssal), the Arcanist (arcane or aura) and the Drillmaster
(MOVE and SPD for all), joining at encounters 3, 6, 9, 12 and 15. The ranch's Party panel (P)
fields up to two and fills their three slots each; battles apply the boosts before the first turn.

**Open:**
- [ ] The name.
- [ ] Do wardens grow (levels, more boosts, a tree of their own)?
- [ ] Who each party member is, and how they join (the story).

## Online PvP battles (a goal, the user's direction, 2026-11-02)

**Decided by the user:** casual matches, turn timers, each player using their own squad (wardens
and creatures from their ranch). More to come later.

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

**What it will still need:**
- Matchmaking and transport: Godot's high-level multiplayer (ENet or WebSocket) for direct or
  relayed matches, or a small server. A server also stops cheating on squads.
- PvP rules: two wardens and six creatures a side, turn timers, disconnects, no recruiting.
- Balance: classes, skills and grafts tuned for fairness, not only against wild packs.

**Open:**
- [x] Ranked or casual? **Casual.**
- [x] Squads? **Your own** wardens and creatures.
- [x] Pace? **Turn timers.**
- [ ] Matchmaking or friend codes? Timer length? What happens on a disconnect?

## Shinies (the user's direction, 2026-11-02)

**Decided by the user:**
- **Much rarer:** 1 in 512 for now (was 1 in 24), easy to change.
- **The reveal:** when a battle starts, the camera pans to each shiny and shows a glint animation.

**Today:** a shiny is a palette swap (`PaletteSpec.Shiny`), the reveal above, and a "shiny" tag in
the ranch detail.

**Open:**
- [ ] Only cosmetic, or do they matter (a stat edge, a unique skill, a rarer form)?
- [ ] Inheritance: can breeding pass it on, or raise the odds?
- [ ] Hunting: sightings of shinies on the mission board, a codex of shiny species.
