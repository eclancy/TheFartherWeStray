# Job trees: every creature its own

_Status: **built** (2026-11-01), still iterating. The user wants many class options, added early.
It grew past this draft: 9 base classes (Hunter, Howler and Rogue were added), 11 part classes
(adding Packleader and Deadeye), 4 blood classes, a passive per class, and FFTA multiclassing (main,
secondary, innate, one equipped passive). The README has the current tables; the code is
`scripts/Classes/Classes.cs` and `scripts/Ranch/JobsPanel.cs`._

## The idea

In FFTA your race decides your job list. Here a creature's **body, parts and blood** decide it, so
no two creatures have the same tree, and the tree changes when you breed, graft or infuse.

Three layers, each answering one question:

| Layer | Decided by | Can you change it? |
|---|---|---|
| **Affinities**: which base classes come easily, which can't be taken | body plan (and hands) | only by breeding a different body plan |
| **Part classes**: advanced jobs gated by a part or a size | wings, horns, shell, whip tail, claws, swimming, size | breed it in, or graft it at the lab |
| **Blood classes**: advanced jobs gated by strain | chemtech / arcane / abyssal blood, clockwork grafts | infuse at the lab, or breed the blood in |

The ranch's training panel shows each creature's whole tree: open, locked with a hint (and how
to get the missing piece), or never possible for this creature.

## 1. Affinities (body plan)

Each body plan has **two favoured** base classes and sometimes **one barred**.
- **Favoured:** +50% JP and a small stat bonus in that class.
- **Barred:** can't be taken. FFTA's race-locked jobs, but only where the body makes it obvious.

| Body plan | Favoured | Barred |
|---|---|---|
| Quadruped | Brawler, Skirmisher | - |
| Biped | Guardian, Tinker | - (the versatile one, like FFTA's humans) |
| Serpent | Mystic, Alchemist | Guardian |
| Avian | Skirmisher, Mystic | Guardian |
| Insectoid | Skirmisher, Alchemist | Mystic |
| Aquatic | Mystic, Guardian | Skirmisher |
| Blob | Guardian, Alchemist | Skirmisher |

Plus one **part rule**: **Tinker needs hands**, meaning a biped, or clawed limbs (claws can work
a lever). A hoofed quadruped can't tinker until claws are bred or grafted in.

## 2. Part classes (advanced, need 2+2 base skills and a part)

| Class | Base skills | Needs | Plays as | Skills (draft) |
|---|---|---|---|---|
| **Skydiver** (built) | Brawler + Skirmisher | wings | strikes from above | Dive Bomb, Gale, Updraft |
| **Juggernaut** (built) | Brawler + Guardian | horns | charging tank | Trample, Iron Hide, Earthshaker |
| **Bulwark** | Guardian + Alchemist | a shell | immovable wall | Shell Up (DEF +8, can't move), Spiked Rampart (thorns aura), Haul (pull an ally to safety) |
| **Lasher** | Brawler + Tinker | a whip tail | reach and control | Crack (hit 3 in a line), Snare Lash (pull a foe 2 tiles), Whirl (hit everything within 2) |
| **Stalker** | Skirmisher + Alchemist | claws | ambusher | Ambush (x2 from higher ground or behind), Venom Claws, Shadow Step (land behind a foe) |
| **Leviathan** | Guardian + Mystic | swims | owns the water | Tidal Crash (area knockback), Undertow (pull a foe into chem), Deep Shell (DEF up while in chem) |
| **Titan** | Brawler + Guardian | huge size | a living siege engine | Stomp (area stun), Shrug Off (cleanse and DEF up), Toss (throw a foe 3 tiles) |
| **Trickster** | Skirmisher + Mystic | tiny size | slips past everything | Blink (teleport 4), Pilfer (steal a buff), Vanish (can't be targeted for a turn) |

Size counts as a part: breed toward huge or tiny on purpose. Swimming comes from the body plan
(serpent, aquatic), so a Leviathan line has to start from one.

## 3. Blood classes (advanced, need 2 base skills and a strain)

| Class | Base skills | Needs | Plays as | Skills (draft) |
|---|---|---|---|---|
| **Hexwarden** (built) | Mystic + Tinker | - | battle mage | Hex Cage, Overcharge, Nova |
| **Chemist** | Alchemist | chemtech blood | volatile support | Shimmer Dose (big buff to an ally, poisons it), Caustic Cloud (area poison), Mutagen (random strong buff) |
| **Runeweaver** | Mystic | arcane blood | traps and wards | Rune Trap (a tile that roots), Glyph Shield (blocks the next hit), Arcane Surge (skills cool down faster) |
| **Voidcaller** | Brawler or Mystic | abyssal blood | terror | Dread (foes nearby lose their move), Siphon (steal HP), Maw (big hit that heals you on a kill) |
| **Artificer** | Tinker | 2+ machine grafts | brass engineer | Deploy Turret, Repair (heal a grafted ally), Overdrive (ally acts twice, then stunned) |

This is where the lab's infusions and machine grafts pay off: they open jobs, not just stats.

## How it plays

**Example 1:** a hoofed natural quadruped starts with Brawler and Skirmisher favoured, and no
Tinker (no hands). Its reachable advanced jobs are Juggernaut if it has horns, and Titan if it's
huge. Breeding in a whip tail would open Lasher only after claws (for Tinker) are bred in too.
That's a two-step breeding goal, which is exactly the kind of goal the notes want.

**Example 2:** a chemtech serpent is good at Mystic and Alchemist and can't be a Guardian, so
Bulwark and Juggernaut are out. It's headed for Chemist (its blood) and Leviathan (it swims, but
that needs Guardian, which is barred). So Leviathan is never possible for it, but its children
with a biped parent might get there.

## Decisions taken (defaults, open to change)

1. Bans are hard (FFTA-style), but kept to the few the body makes obvious.
2. Size-gated classes (Titan, Trickster) are in. Temperament gates one (Packleader).
3. Everything was built in one pass.

## Still open

- Ranks within a class (a mastery bonus after the passive).
- A reaction slot (FFTA's R-abilities), beyond the innate attitude reaction.
- Limits on secondary skills (FFTA lets the whole set through).
- AI: it only reaches for some skills, and never teleports.
- Balance: the JP pace (a class takes about 3-4 battles) and the passives' strength.
