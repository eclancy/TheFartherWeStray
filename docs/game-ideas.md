# Future Game Ideas

_Started 2026-10-20_

---

## 1. Procedural Creature Collector

**The pitch:** A creature collector where every creature is built from randomized parts, stats, sizes, attitudes and colors, aiming for No Man's Sky levels of variety, drawn in the style of classic Pokémon (Gen 1–3).

### Core pieces
- **Body plan** (template): quadruped, biped, serpent, avian, insectoid, aquatic, blob
- **Part slots:** head, eyes, horns/crest, back (fins, shell, spikes), limbs, tail, wings, pattern/markings
- **Coloring:** palette families tied to biome or element, plus rare palettes (the "shiny" equivalent)
- **Size:** tiny to huge, with an effect on stats and how it looks in the world
- **Attitude/temperament:** skittish, aggressive, curious, lazy, proud...
- **Stats:** base stats come from parts, and individuals roll variation on top

### The big risk: "1000 bowls of oatmeal"
Procedural variety goes flat when the differences don't *matter*. No Man's Sky's creatures famously blur together. The fix is to give every random roll a gameplay meaning:
- Wings → can fly, gets flying moves, can reach other areas
- Size → HP/speed tradeoff, plus how hard it is to catch
- Attitude → how it acts in the overworld, how you catch it, how well it obeys
- Parts → decide the move pool (horns give charge moves, a shell gives defensive moves)

### Art direction note
Classic Pokémon's limits (small sprites, about 4 colors per sprite) are a big help here. Tight palettes and low resolution make procedurally assembled parts look like they belong together. **Prototype the sprite generator first.** If generated creatures don't look charming, nothing else matters.

### Species vs. individuals (design decision)
Suggested hybrid:
- Each **species** is generated from a seed: a fixed body plan, a set of parts, a palette range, a move family and a name. That keeps the Dex meaningful ("I found a new species!").
- **Individuals** vary within their species: size, shade, attitude, stat rolls, and rare **mutations** (an odd part or palette).
- This gives you two layers of discovery, *new species* and *rare individuals*.

### What should the game loop be?
Options, from safest to most distinctive:

| Loop | Description | Strength |
|---|---|---|
| **A. Classic** | Route → encounter → battle/catch → train → gym/boss | Proven, but variety can feel cosmetic |
| **B. Naturalist / field researcher** | Each biome generates its own ecosystem. You watch behavior (skittish ones flee, aggressive ones charge, curious ones follow you), catalog it and catch. The Dex *is* the game. | Makes attitude and parts readable in the world |
| **C. Breeding / lineage** | Parts and traits pass to offspring. Breed toward the creature you picture. | Gives the randomness *purpose*: the player steers it |
| **D. Expedition roguelite** | Head out to a generated island with a team, bring back finds, upgrade your base | Endless replay, fits procedural generation |

**Recommendation:** **B + C**, with battles as the *testing ground*, not the whole point.
- Explore → watch → catch (attitude decides *how*) → breed toward goals → prove your creatures in battles, tournaments or contests
- The long-term hook: "I want a huge, calm, crimson serpent with crystal horns", and the player chases that through breeding

### Open questions
- [ ] Turn-based battles (Pokémon) or something simpler (auto-battle, contests)?
- [ ] One handcrafted world with procedural fauna, or a fully procedural world?
- [ ] Are elements/types tied to parts, palettes or biomes?
- [ ] How are names generated? (Syllables built from parts, e.g. "horn" + "fin" pieces)

---

## 2. FFTA-Style Tactics Game

**The pitch:** Grid tactics like Final Fantasy Tactics Advance. Units have a **species**, a **class**, and full sets of weapons and armor. Learn skills from several classes to unlock new classes.

### Systems to decide
- **Where do skills come from?** FFTA: equipment teaches abilities through AP. Alternative: classes teach skills and gear only modifies them. *Suggestion: classes teach and gear modifies.* It keeps the unlock tree as the main progression and gear as tuning.
- **Class unlock tree:** e.g. Soldier (2 skills) + White Mage (2 skills) → Paladin. Show locked classes as silhouettes with hints so players chase them.
- **Species:** innate traits, plus classes only some species can take (like FFTA's race-locked jobs). Species could also cap or shift stat growth.
- **Gear:** weapon type decides the attack pattern (spear reaches 2 tiles, bow arcs, axe hits in a cleave). Armor weight vs. move range.
- **Party size and battle length:** FFTA battles are short (5–10 min). Keep maps small, around 4–6 units per side.
- **Permadeath?** Off by default, with a toggle.

### Theming / story options
1. **Mercenary guild in a fractured world.** Take contracts from a mission board, grow your clan, compete with rival clans. This is FFTA's structure. Flexible and low-risk.
2. **Tabletop diorama.** The game *is* a painted-miniature wargame on a table: units are minis, maps are dioramas, a "game master" narrates. A strong look, and it fits a miniatures hobby.
3. **Creature-world crossover.** Set it in the creature collector's world. Species are generated with the same part system, and wild "monster" enemies come from the same generator, so tech is shared between projects.
4. **Wizard world.** Ties into existing wizard projects, with classes as schools of magic and martial disciplines.

### How should battles feel? What's unique?
Pick **one** signature hook and build around it:
- **Telegraphed enemy intent (FFTA × Into the Breach).** Enemies show what they'll do next turn, so battles play like readable puzzles: dodge, block, shove, interrupt. *Strongest candidate for a unique feel.*
- **Laws / battle modifiers.** FFTA's Judges, reimagined as weather, terrain or a "match rule" that changes each fight (no fire magic, healing doubled, height bonus tripled).
- **Formation combos.** Adjacent allies in certain shapes (line, triangle, flank) unlock combo skills. This rewards positioning over raw stats.
- **Destructible / reactive terrain.** Burn grass, freeze water into walkways, knock units off ledges.

Baseline feel to aim for: snappy animations, fast enemy turns, clear previews of damage and hit chance, and an undo for movement before you act.

### Open questions
- [ ] Which signature hook?
- [ ] Linear story campaign or mission board with light story?
- [ ] How many species and classes at launch? (Rough MVP: 3 species, 6 base classes, 4 unlockable)
- [ ] Facing and height mechanics (FFT-style) or flat?

---

## 3. Deckbuilding Roguelike

**The pitch:** No unique angle yet. Just want to take a spin at the genre.

### Plan: build the classic core first, find the twist later
Clone the Slay the Spire foundation as a learning project:
- Energy per turn, draw 5, attack and block cards
- Enemy **intents** shown above their heads
- Branching map: fights, elites, rest sites, shops, events, boss
- Card reward after each fight (pick 1 of 3 or skip)
- Relics (passive run modifiers)
- Upgrading and removing cards

**MVP scope:** 1 character, around 20 cards, 5 normal enemies, 1 elite, 1 boss, 1 act.

### Twist ideas for later (if one sticks)
- **Creature parts as cards.** Your deck is a creature you build. Gaining a "horn" part adds horn cards. Shares tech with project 1.
- **Hand order matters.** Cards get bonuses based on where they sit in your hand or which card was played before.
- **Grid + cards.** A small tactics board where cards are the actions. Shares ideas with project 2.
- **Deck is your health.** Damage burns cards, and healing restores them.
- **Card fusion.** Combine two cards into a new one instead of upgrading.

---

## 4. Combined: Creature Tactics (Collector × FFTA)

**The pitch:** An FFTA-style tactics game where your units are procedurally generated creatures you recruit, breed and train. Each half fixes the other's weak spot. The tactics grid gives random parts and sizes a real job, and the collector gives the tactics game endless unit variety and a long-term hook.

### Games to study
- **Pokémon Conquest:** Pokémon on a tactics grid. The closest existing game.
- **Dragon Quest Monsters:** breeding where offspring inherit skills.
- **Shin Megami Tensei:** demon fusion and skill inheritance.
- **Disgaea:** recruiting monsters onto your squad.
- **Spore:** parts that grant abilities.

### How the FFTA systems map onto creatures
| FFTA system | Creature version |
|---|---|
| Race / species | **Body plan + generated species** (quadruped, serpent, avian...) |
| Equipment | **Parts.** Horns or claws are the *weapon* (they set the attack pattern), a shell or hide is the *armor*, wings or fins are the *accessory* (movement type) |
| Job / class | **Training discipline** a creature learns at the ranch (Brawler, Mystic, Skirmisher, Guardian...) |
| Learn skills → unlock classes | Same, **but some classes also need certain parts**: e.g. Brawler skills + Glider skills + *has wings* → **Skydiver** |
| Recruiting | **Recruit wild creatures in battle** instead of only defeating them |

**The core synergy:** class unlocks can require parts, and parts come from breeding. So a goal like "I want a Skydiver" sends you to the breeding pen to get wings into a bloodline. Collecting, breeding and tactics all feed each other.

**Swapping "gear":** you can't take off a horn, so parts change through breeding, mutation, or grafting at the ranch (a costly ritual or lab). That keeps parts meaningful and makes breeding the gear grind.

### Unique tactical hooks that only this combo gets
- **Size on the grid:** huge creatures take up 2×2 tiles (they can't fit through gaps, but they block and hit in a wide area). Tiny ones can share a tile, hide in grass, or ride on a large ally.
- **Body plan sets movement:** serpents swim, avians fly over gaps, climbers ignore height, burrowers tunnel under the map.
- **Attitude becomes a reaction ability:**
  - Aggressive: counterattacks
  - Skittish: dodges and retreats a tile after being hit
  - Loyal: takes hits for adjacent allies
  - Curious: grabs items within reach
  - Proud: gets stronger when fighting alone
- **Recruiting depends on attitude** (fights become varied puzzles instead of "KO everything"):
  - Proud: beat it with a single unit
  - Skittish: corner it so it can't flee
  - Curious: lure it with an item
  - Aggressive: survive its attacks for X turns
- **Telegraphed enemy intent** (from project 2) still fits well. Wild creatures could even telegraph by attitude: skittish ones flee and aggressive ones charge the nearest unit.

### Game loop
1. **Mission board / expedition:** pick a region (a generated biome map with its own fauna)
2. **Battle:** complete the objective and try to recruit wild creatures along the way
3. **Ranch:** breed (parts, palettes and *some learned skills* pass down), train classes, graft parts
4. **Unlock:** new classes from skill and part combos, and new regions with new species
5. Repeat with stronger, more specialized creatures

### Player character?
- **Option A:** No human on the field. You're the rancher or commander. Simplest.
- **Option B:** Human tamers with FFTA classes fight next to the creatures, Pokémon Conquest-style. Richer, but it doubles the systems.
- *Suggestion: start with A. Humans can arrive later as story units or rivals.*

### Theme / story seeds
- **Frontier rangers:** an unexplored land where new species keep appearing, which explains the procedural generation in-story. You run a ranger outpost taking contracts.
- **Rival ranches:** clans like FFTA's, each breeding toward its own ideal creature. Rival creatures show what's possible.
- **Mutating world:** a spreading phenomenon keeps rewriting local ecosystems, so regions change between visits.

### MVP scope
- 1 biome, 3 body plans, about 20 parts
- 6 base classes, 3 unlockable (at least one needs a part)
- Simple breeding: pick 2 parents → the child inherits a part mix plus 1 skill
- 5–8 handcrafted maps with generated fauna
- **Still prototype the sprite generator first.** It's the biggest risk in this version too.

### Open questions
- [ ] Do creatures keep learned classes when bred, or only pass down a skill?
- [ ] Permadeath? (It hurts more when every creature is unique, which might be good)
- [ ] Squad size per battle: 4? 6?
- [ ] Is battle-only recruiting enough, or should there also be overworld catching?

---

## Cross-project synergies
- A **procedural creature/part generator** could power all three: collector creatures, tactics species and enemies, and deckbuilder cards.
- The deckbuilder is the smallest scope and makes a good warm-up. The creature generator is the riskiest piece, so prototype it early to see if it's fun.
