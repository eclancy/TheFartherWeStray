# Job trees: every creature its own

_Status: **built** (2026-11-01), still iterating. The user wants many class options, added early.
It grew past this draft: 9 base classes (Hunter, Howler and Rogue were added), 31 part, form, aura
and temperament classes, 4 blood classes, a support passive and usually a reaction per class,
mastery stars, and FFTA multiclassing (main, secondary, innate, a support slot and a reaction
slot). On 2026-11-02 eight classes were tied to forms and auras: Hydrarch (hydra), Stoneguard
(gargoyle), Coilbinder (naga), Venomtail (scorpion), Lurelight (angler), Oculus (watcher), Saint
(a halo) and Hexmark (a sigil); then Basilisk (cockatrice: a stunning peck, a tail lash, a crow
that saps foes). Later that day (the user's asks): a chimera with a form's ability spliced in
can train that form's class (`ClassBook.HasForm`), and three signature classes after FFTA's
summoner, illusionist and assassin were added (section 4). The code is `scripts/Classes/Classes.cs` and
`scripts/Ranch/JobsPanel.cs`._

## The idea

In FFTA your race decides your job list. Here a creature's **body, parts and blood** decide it, so
no two creatures have the same tree, and the tree changes when you breed, graft or infuse.

Three layers, each answering one question:

| Layer | Decided by | Can you change it? |
|---|---|---|
| **Affinities**: which base classes come easily, which can't be taken | body plan (and hands) | only by breeding a different body plan |
| **Part classes**: advanced jobs gated by a part or a size | wings, horns, shell, whip tail, claws, swimming, size | breed it in, or graft it at the lab |
| **Blood classes**: advanced jobs gated by strain | chemborn / runic / abyssal blood, clockwork grafts | infuse at the lab, or breed the blood in |
| **Form and aura classes**: advanced jobs gated by the kind of beast it is, or its mystical mark | form (hydra, gargoyle, naga, scorpion, angler, watcher), aura (halo, sigil) | breed it in: a child takes its form from the body plan's parent and its aura from either |

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
| **Chemist** | Alchemist | chemborn blood | volatile support | Fury Dose (big buff to an ally, poisons it), Caustic Cloud (area poison), Mutagen (random strong buff) |
| **Runeweaver** | Mystic | runic blood | traps and wards | Rune Trap (a tile that roots), Glyph Shield (blocks the next hit), Rune Surge (skills cool down faster) |
| **Voidcaller** | Brawler or Mystic | abyssal blood | terror | Dread (foes nearby lose their move), Siphon (steal HP), Maw (big hit that heals you on a kill) |
| **Artificer** | Tinker | 2+ machine grafts | brass engineer | Deploy Turret, Repair (heal a grafted ally), Overdrive (ally acts twice, then stunned) |

This is where the lab's infusions and machine grafts pay off: they open jobs, not just stats.

## 4. Signature classes (the user's ask, 2026-11-02; built)

The signature styles of FFTA's summoner, illusionist and assassin, made this game's own. Two
base classes each, no part needed. Each brought a mechanic of its own (`SkillDef` fields that
`Battle` reads), so later skills can use them too:

| Class | Base skills | Plays as | Skills | Mechanic |
|---|---|---|---|---|
| **Conjurer** | Mystic + Howler | summoner | Cinder Drake (fire, x1.2), Storm Roc (lightning, x0.7, 30% stun), Grove Stag (heal 30% and cleanse allies) | `Phantom`: the ghost of a great beast (a form, sculpted huge and glowing) rises over the area. Areas reach only foes (or only friends); radius 2 at range 4, cooldown 5 |
| **Mesmer** | Mystic + Rogue | illusionist | Fever Dream (x0.55), Winter Trance (x0.35, SPD -2), Night Terrors (x0.25, ATK -2) | `AllFoes`: every foe on the field, wherever it stands, no aiming; cooldown 5 |
| **Reaper** | Rogue + Hunter | assassin | Death Mark (range 3), Last Breath (x0.8), Sleep Dart (range 4) | `Doom` (falls once its count of its own turns runs out; cleansing lifts it), `Finish` (a foe left under a quarter of its HP falls at once, 50%), `Sleep` (skips up to 2 turns, a hit wakes it; stun-proof creatures resist) |

Passives: Old Pact (skills cool down a turn sooner) and Spirit Ward (brace) for the Conjurer;
Afterimage (12% harder to hit) and Mirror Image (25% the hit strikes an illusion) for the Mesmer;
Cold Blood (+10% to hit, more damage to the wounded) and Slip Away (retreat) for the Reaper.
`--signatures` proves each mechanic; `--battle=3 --fxdemo=signature` shows the summons and an
illusion.

Four more after FFTA's blue mage, time mage, gadgeteer and morpher (the user's yes, 2026-11-02):

| Class | Base skills | Plays as | Skills | Mechanic |
|---|---|---|---|---|
| **Mimic** | Mystic + Hunter | blue mage | Copycat (x0.9, steals boosts), Mock (ATK and DEF -2), Study (harder to hit, SPD +1) | learns any skill a foe uses on it while it trains as a Mimic (main or secondary), kept after the battle (`Training.Learned`, `Battle.Learned`) and usable as a Mimic |
| **Horologist** | Mystic + Tinker | time mage | Haste (SPD +4), Stop, Quicken | `Stop` (frozen for 2 of its turns; hits don't free it), `Quicken` (the ally goes next) |
| **Saboteur** | Tinker + Rogue | gadgeteer | Scatter Charge (x0.8), Flash Powder (40% stun), Mender Mist (heal 25%) | `Gamble`: a coin flip sends each gadget at every foe or every ally (a heal the other way round); Loaded Dice (`Lucky`) tips it to 3 in 4 |
| **Skinchanger** | Brawler + Mystic | morpher | Mirror Shape, Beast Shape (ATK +3, DEF +2, SPD -1), Wisp Shape (harder to hit, MOVE +1) | `CopyForm`: a foe's form ability is its own for 3 turns (`Unit.Borrowed`) |

**Trials (the user's call, 2026-11-02):** certain classes open only once a certain mission is won,
and that mission moves your standing toward its side. All seven signature classes have one
(`ClassDef.Trial`, `World/Trials.cs`), on the map once its place is open: the Conjurer's in the
Gilded Gardens, the Mesmer's on the Brass Bridges, the Mimic's at the coliseum and the
Horologist's at the Clocktower Ruins (the city above, +5 city standing); the Reaper's on Lantern
Row, the Saboteur's at the Foundry and the Skinchanger's in the Glowmire (the undercity, +5).
A locked class's hint names its trial. `ClassBook.TrialDone` reads the save in play; tests and
rivals have every class open. The trials' titles and words are placeholders for the story.

**Open:** which other classes should need a trial; the trials' story.

## How it plays

**Example 1:** a hoofed natural quadruped starts with Brawler and Skirmisher favoured, and no
Tinker (no hands). Its reachable advanced jobs are Juggernaut if it has horns, and Titan if it's
huge. Breeding in a whip tail would open Lasher only after claws (for Tinker) are bred in too.
That's a two-step breeding goal, which is exactly the kind of goal the notes want.

**Example 2:** a chemborn serpent is good at Mystic and Alchemist and can't be a Guardian, so
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
