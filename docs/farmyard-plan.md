# The farmyard: decorate, upgrade, restyle, and machines (plan, not built)

**The user's words (2026-11-03):** "you should be able to decorate and upgrade and stylize your
farmyard"; "include ways that you can upgrade machines to automatically farm supplies between
missions"; "some of your unused beasts will be walking around your farmyard, you can select
which ones are out and about". Plan first, nothing built yet. The decisions marked **Open** are the user's.

## Built so far

**Phase 1, decorating (built 2026-11-03).**

- **The workbench station** (`j`, by the farmhouse wall at (4,1); `Prop.Workbench`, open from the
  start) turns the base into build mode in place. You get a tile cursor (the mouse, WASD or arrows,
  the D-pad) and the shed along the foot of the screen.
  - With a piece in hand, A or a click puts it down; on a placed piece it picks it up.
  - On an empty tile with nothing in hand, A goes to the shed.
  - B, Esc or a right-click backs out a step.
  - The cursor is green where a piece fits and red where it doesn't, and the reason is shown.
- **Pieces:** 19 of them, all props that already exist (`World/Decor.cs`, `DecorBook`).
  - Barrels, crates, lamp posts and the market stall, bush, boulder and neon sign at Lantern Row,
    where you start.
  - Hedges, banners, columns, statues and the fountain in the city above.
  - The chem tank and the gear heap at the Foundry.
  - Trees, pines, reeds, mushrooms and crystals out in the wilds.
  - The shops sell them as `decor:ID` wares; buying puts them in the shed.
  - A new ranch, and every old save once (`save:decor`), starts with 2 barrels, a crate, a lamp,
    2 bushes and a banner.
- **The rules** (`Ranch/Farmyard.cs`):
  - Pieces go anywhere free, but not on a station, the pens or their gap, the spot you come home
    to, a roof, or under your feet.
  - A blocking piece is refused if it would cut off a station or the pens. A flood fill from the
    spot you come home to checks this.
  - At most 60 pieces.
  - A loaded yard is tidied: unknown pieces are dropped, and pieces on a tile the base has since
    claimed go back in the shed.
- **The save:** `Ranch.Decor` (id, x, y) and `Ranch.Shed` (counts). `HomeBase.Map` puts the
  pieces on the board (`BattleMap.Put`), and `BaseScreen.PaintYard` repaints it after each change.
- **The checks:** `--basetest` (23 checks, headless). `--goto=bench --holding=ID --cursor=X,Y`
  opens build mode for a screenshot.
- **Not yet:**
  - Turning a piece.
  - Crafting, which comes with the machines' goods.
  - Tints.
  - Beast statues and trophies.

**Phase 2, beasts out and about (built 2026-11-03).**

- **Choosing them.** At the pens, "Out in the yard" (Y on a desktop) lets a beast not in the squad
  out, or brings it back in. Up to six can be out (`Farmyard.MostOut`).
  - Their cards light green and say IN THE YARD; the squad stays cyan.
  - Joining the squad, or being let go, brings a beast back in.
  - A new ranch lets out the starters outside the squad. An old save gets up to six once
    (`save:out`).
  - Saved in `Ranch.Out`.
- **Roaming.** The out beasts roam the whole yard (`BaseScreen`, `Roamer`, `Decide`, `Potter`).
  - They never go into the pen or a building; fliers may perch on a roof.
  - You walk through them, and their footfalls are quiet.
  - What they do comes from their temper and what's in the yard:
    - The aggressive spar with the training dummies.
    - The curious go to look at the newest piece.
    - The loyal come and stand by you.
    - The proud stand by a statue, a column, the fountain or the gate, or fliers perch up on a
      roof.
    - The skittish keep to the edges, and move off when you come near.
  - Otherwise they drink at the trough or a fountain, nap in the shade of a tree, bush or hedge
    (the faint pose held, with a moon above them), sit by a lamp or a barrel, or wander.
  - Their emote balloons are the cutscenes'.
- **Saying hello.** Click a beast out in the yard, or press A (or E) beside it.
  - It wakes, turns to you and shows its temper in a line, with how it reacts in battle.
- **The title screen** now shows the last slot's own yard behind the name: its pieces, its
  stations, and its out beasts (or its squad).
- **The checks:** `--basetest` grows to 33 checks. It also writes `user://yard_show.json`, a
  yard with six beasts out and nine pieces, for screenshots (`--save=yard_show`).

**Phase 3, machines (built 2026-11-03).**

- **The machines** (`World/Machines.cs`, `MachineBook`; new props `Still`, `BerryBeds`,
  `ScrapPress`, drawn 1.7 times the size of street furniture; the beds 1.4):

  | Machine | Makes, a day (L1 / L2 / L3) | Holds | Sold at | Price |
  |---|---|---|---|---|
  | Chem still | 3 / 5 / 8 chem | 15 / 30 / 50 | Lantern Row | 90 |
  | Glowberry beds | a berry every other day / 1 / 1, plus a tonic every 4 days at L3 | 4 / 6 / 8 (tonics 2) | Gilded Gardens, the coast, the hollow | 60 |
  | Scrap press | 2 / 3 / 5 scrap | 10 / 20 / 30 | Lantern Row, the Foundry | 70 |

  The still makes chem, the plan's proposal; that question was left open, and it's one line of
  data to change.
- **One of each** per yard. You buy it at the shop into the shed, put it out at the workbench,
  and move it there later; it keeps its level and hopper, and B leaves it where it was. Machines
  never go back in the shed.
- **Filling.** Hoppers fill each game day: a battle, or a road travelled (`Farmyard.PassDay`,
  beside `Roaming.PassDay`). A share under one builds up over the days, and a full hopper stops.
- **At the machine.** Walk up and press A (or click it) for its panel: what it makes and holds,
  the hopper, Collect (first, so A, A takes it), Upgrade and Close.
  - Collecting puts chem and scrap in the ranch, and glowberries and tonics in the bag.
  - Upgrades cost chem and scrap: the still 50 + 10, then 100 + 25; the beds 40 + 8, then 80 + 20;
    the press 40, then 90 + 15.
- **Scrap** is a new count (`Ranch.Scrap`) on the base's top bar.
- **The yard.** A label over each machine says what's in its hopper (gold, "full", when full).
  The still lets off steam and bubbles green, the press puffs steam, and the beds glint pink
  (`PropFx`). Beasts out and about sit by the machines too.
- **The checks:** `--basetest` grows to 52 checks. `--save=yard_show` now has all three
  machines, part full. `--machine=ID` walks to a machine and opens it.
- **Not yet:**
  - The net loom, the draught kettle and the water wheel.
  - 2 x 2 machines.
  - Crafting from scrap.

**Phase 4, station upgrades (built 2026-11-03).**

- **Building one up.** At the workbench, put the cursor on a station and press A (or click it).
  The panel shows its three levels and what each does, the next one's price, and Build.
  - Every station but the workbench goes to level 3.
  - Level 2 costs 120 chem and 15 scrap, level 3 costs 240 and 40 (`HomeBase.UpgradeCosts`).
  - **It's built at once** (the user's call, 2026-11-03, changed from the plan's days in
    scaffolding).
- **What each level does** (`StationDef.Upgrades`, the effects in `Farmyard`; the user picked the
  pens', the yard's and the war table's):

  | Station | Level 2 | Level 3 |
  |---|---|---|
  | Gate | a cart: the first road of every trip takes no day | a second cart: the first two roads |
  | Pens | room for 9 beasts out in the yard | room for 12 |
  | Training yard | beasts at home earn a quarter of the squad's JP each battle | half |
  | Armory | 4 glowberries packed for every battle | 5 |
  | Breeding vats | children are born rested | parents don't need to rest either |
  | War table | dispatches pay a quarter more chem | and come back a day sooner |
  | Lab | grafts, infusions and splices 10% cheaper | 20% cheaper |

  None of them touch battle stats.
- **The save:** `Ranch.Levels` (station id to level). A station's label says its level from 2 up.
- **The checks:** `--basetest` grows to 64 checks:
  - no chem, the top level, the workbench, a station not built yet;
  - the pens' cap, the armory's glowberries in a real battle, the lab's prices;
  - the vats' rested parents and child, the war table's dispatch;
  - the gate's cart on the road, the yard's JP after a played battle;
  - save and load.
- **Not yet:** each level's own look (the bigger barn, more vats, lit windows).

## Where it starts

- The base is `Ranch/HomeBase.cs`: three handmade 16 x 14 string grids (terrain, heights,
  props), two buildings (the farmhouse at x0-3, the lab workshop at x12-15), seven stations found
  by their prop letter. Unbuilt stations show as scaffolding.
- **155 tiles are free.** The biggest spaces: the strip between the buildings (x4-11, y0-1), the
  stone court (x6-10, y6-9), the lawn (x1-6, y4-8), the yard (x11-15, y5-8) and the south-east
  corner (x9-14, y10-13).
- `BaseScreen` bakes the board once, but the game builds a new BaseScreen every time you come
  home. So anything changed away from the base shows on return for free; only live placement
  needs a rebuild.
- Props (`Tactics/Props.cs`) are built in code like creatures. A new one costs an enum entry
  (always at the end), a letter and a 5-15 line `Build` case. Nothing at home animates yet
  (`PropFx` covers only the undercity's lamps, hydrants, vents and signs).
- Time is the game's `Day`: a road travelled or a battle fought (`Ranch.cs:465`, `:708`, where
  `Roaming.PassDay` already runs).
- **Chem, for pricing:** a won contract nets about 25-55; shop gear is 35-90, augments 60-140, an
  infusion 30-45.

## The parts

### 1. Decorate

- **Where:** a new station, **the workbench** (beside the farmhouse), opens *build mode* on
  the base itself. That follows "every menu is a place you walk to". The pause menu can offer
  it too.
- **Build mode:**
  - A tile cursor, as in battle: the mouse, the arrows or the phone's D-pad move it.
  - A tray along the bottom shows what you own, with counts.
  - Placing: A or a click places the item, A on a placed one gives Move, Turn (mirror) and
    Store, and B leaves.
  - Tiles light green where something can go and red where it can't. The tile highlights
    already cut themselves to what shows (`TopMask`).
- **Rules** (placing anywhere free, decided by the user, 2026-11-03):
  - One thing per tile, never on a station or its doorstep, never on a roof (to start).
  - **Every station, the gate, the pen's gap and the spawn must stay reachable.** Each
    placement runs a flood fill (`Paths`) and is refused if it would wall anything off.
  - Banners, reeds and grates can be walked through, as on the board.
- **What there is to place:**
  - The existing props, which already have art: lamps, banners, barrels, crates, trees, bushes,
    hedges, statues, fountains, pillars, stalls, neon signs, tanks, rocks, crystals, mushrooms,
    reeds and fence pieces.
  - New ones: flower beds, topiary, benches, wind chimes, a scarecrow, strung chem lanterns, a
    pipe arch, a sign with your mark, a scrap sculpture, a well, a dovecote, hay bales and a
    weather vane.
- **Beast statues** (unique to this game): a statue or topiary of one of *your* beasts. The
  creature sculptor renders it with a stone, bronze, gold or leaf palette, so every one is a
  real beast you raised. They could also stand as trophies: a gilded statue of the beast that
  won a cup.
- **The trophy plinths** (the open "trophy hall" idea in `design-notes.md`): cups, rival titles
  and main-mission keepsakes appear as things you can put out.
- **Where decorations come from** (**decided by the user, 2026-11-03: both bought and
  crafted**):
  - Shops, by side: the city's sell gilded pieces, the undercity's neon and chem, the wilds
    rustic ones. Prices run 10-30 chem small, 40-90 large.
  - Prizes: rival prize tables gain decorations (uncommon), cups give trophies, main missions
    give keepsakes.
  - Dispatches bring back a souvenir now and then, fitting the pun jobs.
  - **Crafting at the workbench** from what the machines make (scrap, and later timber,
    glass and dye), plus a little chem. Recipes are data. Crafted pieces are the homely and
    salvaged ones (benches, scrap sculptures, pipe arches, lanterns); the gilded and neon
    showpieces stay in the shops.

### 2. Upgrade the stations

- Each station gets **levels 1-3**. An upgrade costs chem and scrap, which the machines make
  (part 3). It's **built at once**: the user's call on 2026-11-03, replacing days in scaffolding.
  Each level should change how it looks, with a bigger barn, more vats, lit windows or a sign,
  but that isn't built yet.
- Prices: 100-250 chem a level.
- **Effects stay on the loop, never on battle stats** (decided by the user, 2026-11-03).
  Online PvP brings your own squad, so a bigger farm must not mean stronger beasts. Proposed,
  to tune with `--country`:

| Station | Level 2 | Level 3 |
|---|---|---|
| Pens | room for more beasts at home | beasts back from an episode or steeping recover a day sooner |
| Training yard | beasts left home earn some JP each battle | more of it |
| Breeding vats | a second pair of vats | parents don't need to rest |
| Lab | grafts and infusions 10% cheaper | 20% cheaper |
| Armory | one more glowberry packed each battle (story battles only) | a second gear rack: swap a saved loadout |
| War table | one more dispatch out at once | dispatches come back a day sooner |
| Gate | a cart: the first road of a trip is free | a second cart |

The pens and the training yard need a check first: the code has no roster cap and no JP for
beasts left at home today, so those rows would add a rule rather than tune one.

### 3. Machines that farm while you're out (the user's ask)

- **Made of clockwork, steam and chemistry, no computers** (the industrial-fantasy rule).
  Machines stand on the yard like stations and make supplies **each game day** (a battle or a
  road). They never run on real time, so saves stay deterministic, and nothing rewards leaving
  the game running or punishes a week away from the phone.
- **Each machine fills a hopper with a cap.** You walk up and press A to collect, so coming home
  matters, and a full hopper stops filling.
- **They run on their own** (the user, 2026-11-03: beasts working them is a note for later,
  at the end).
- **Upgrades** raise the rate and the cap, and at level 3 add a second product.

| Machine | Makes | L1 / L2 / L3 a day | Cap | Look |
|---|---|---|---|---|
| Chem still | chem | 3 / 5 / 8 | 15 / 30 / 50 | glass coils bubbling green, a boiler |
| Glowberry beds | glowberries | 1 per 2 days / 1 / 1 + a tonic | 4 / 6 / 8 | glowing shrubs under a cold frame |
| Net loom | snare nets | 1 per 3 days / per 2 / per day | 2 / 3 / 4 | a clockwork loom, shuttles flying |
| Draught kettle | a stat infusion | 1 per 5 days / per 4 / per 3 | 1 / 2 / 3 | a copper kettle with a dripping spout |
| Scrap press | scrap (for upgrades) | 2 / 3 / 5 | 10 / 20 / 30 | a steam press with a pile of parts |
| Water wheel | power: +1 to the machines beside it | - | - | a wheel turning in a stream |

- At level 3 the still makes about a fifth of a won contract a day: a top-up, never the main
  income. The numbers are a first guess, to check with a `--country` run with machines on.
- **They animate:** the first moving props at home (`PropFx`): bubbling glass, steam puffs,
  turning wheels and shuttles.
- **Machines are bought** at a shop that fits them (the still from the undercity, the beds from
  the wilds, the loom from the city), then placed in build mode like a decoration. The 2 x 2
  ones need a 2 x 2 free space. **Prices:** 60-150 chem to build, and upgrades in scrap and
  chem.

### 3b. Beasts out and about (the user's direction, 2026-11-03)

"Some of your unused beasts will be walking around your farmyard, you can select which ones are
out and about."

- **Today:** up to eight beasts wander inside the pen, picked by the game (`BaseScreen`; the pen
  is x0-4, y10-13).
- **Your pick:**
  - At the pens, every beast not in the squad gets an *out and about* toggle: a chip on its
    card, as the squad has its cyan.
  - Those beasts roam the whole yard; the rest stay in the pen as now.
  - A cap keeps the yard readable and the phone quick: six out at once to start (**Open**).
- **Where they go:**
  - Any tile a person can stand on (`CanStand`), never into a building. They walk with `Paths`
    a tile at a time and idle between walks.
  - Fliers can sit on fences and roofs; swimmers keep near water when there is some.
  - They never block you: you walk through them, and they step off a station's doorstep when
    you come.
- **What they do there:** the decorations give them somewhere to go.
  - They drink at the trough or a fountain, nap under a tree or in the hay, sit by a lamp, watch
    the training dummies, or perch on the weather vane.
  - By temper:
    - The curious go to look at the newest thing you put out.
    - The skittish keep to the edges.
    - The proud stand on the highest spot.
    - The loyal follow you about.
    - The aggressive spar with the dummies.
  - The poses are ones they already have: breathing, walking, the strike and the rear-up.
- **Press A by one, or tap it,** and it shows its name and a line about its temper, as the
  page's pen does. The yard doubles as a way to look at your beasts.
- **The title screen** shows the last slot's out-and-about beasts in its yard, instead of the
  four starters.
- **Data:**
  - `Ranch.Out` holds the chosen creature ids and is saved.
  - `Tidy` fills it for old saves with the beasts the game put in the pen before, none of them
    from the squad.
  - A beast away on a dispatch isn't home, so it isn't out.
- **Checks:**
  - `--basetest` keeps the beasts on standable tiles and never shut in.
  - `--yardlife` is for a screenshot.

### 4. Restyle the yard

You choose the farmhouse and workshop's look instead of it being rolled. `Houses.cs` already
has every part:

- Wall material (plaster, timbered, brick, boards, tin, stone) and colour.
- Roof (shingles, tin, slate, copper, thatch).
- Windows (square, arched, shuttered, portholes), awnings, flower boxes, ivy, wall lamps and a
  neon sign with your mark.

Beyond the buildings:

- Fences: rail, iron, hedge, pipe.
- Paths: dirt, stone, boards, cobbles, painted tile by tile.
- The lamps' light: warm, chem green, neon pink, cold blue.
- Banners in your colours with an emblem.

**Kits by side** (**Open: gated by standing?**):

- **Gilded** (the city): white plaster, gold trim, blue slate, topiary, gilded lamps.
- **Undercity:** brick and tin, neon, chem lamps, pipes, a graffiti wall you choose the tags
  for.
- **Wild:** timber, thatch, lanterns, standing stones.

Mixing kits is allowed; the yard then shows where you lean.

### 5. Grow (later)

Buying more land: the grids grow, and with them the pen, the spawn tile and the title screen's
creature tiles, which are hard-coded today. Interiors, walking into the farmhouse and the lab,
are already an open idea. Both come after the rest.

## How it would be built

- **Data, as the rest of the game is:**
  - `World/Decor.cs` holds `DecorBook` (id, name, prop, size, blocks, side, price, where sold,
    tints).
  - `World/Machines.cs` holds `MachineBook` (product, rate and cap per level, costs, build
    days) and `StationBook` levels (costs, days, effects).
  - New entries are new data, not new code.
- **Save** (`Ranch`, plain JSON fields, defaults for old saves in `Tidy`):
  - `Decor` (placed: id, x, y, flip, tint).
  - `Shed` (counts, kept apart from `Items`, whose lookups throw on an unknown id; built as
    `Ranch.Shed`).
  - `Levels` (station id to level), `Machines` (id, x, y, level, stored, days left building).
  - `Out` (the beasts out and about, part 3b).
  - `BaseStyle` (the chosen house, fence, path and lamp looks).
  - No `Vector2I` keys.
- **The map:**
  - `HomeBase.Map(r)` lays the placed things over the handmade grid. It does this as a list of
    placed props, not new letters: only 17 letters are free, and a decoration must never reuse
    a station's letter.
  - `PropArt` gets a per-thing tint in its cache key.
  - IsoArt gets a hook to take the chosen house looks instead of rolling them.
- **Build mode** lives in `BaseScreen`, not in the creature-centred `RanchScreen`: a preview
  sprite under the cursor, and the board repainted when a piece is put down (the base is small;
  measure it).
- **Each day:** machines fill and upgrades build where the day already turns, beside
  `Roaming.PassDay`. Anything rolled comes from `SeededRng` on the day and the machine.
- **The title screen** shows the last slot's own yard behind the name (it builds a fresh ranch
  today).
- **The phone:** the D-pad moves the cursor, A places or picks up, B backs out, and every tray
  item is a button. Check it with `--phone`.
- **Checks:**
  - `--basetest` (headless): place and remove things; refuse layouts that wall a station off;
    save and load the layout identically; machines make the same thing over N days twice.
  - `--decorate` and `--machines` for screenshots.
  - `--country=N` with machines on, for the chem curve.

## Build order

1. **Groundwork and decorating** (built): the data, the save, the overlay, build mode with its rules, a
   dozen pieces from props that exist, and shop stock.
2. **Beasts out and about** (built): choosing them at the pens, roaming the yard, the things
   they do with the decorations, and the title screen showing them.
3. **Machines** (built): the still, the beds and the scrap press, hoppers and collecting, and upgrades.
4. **Station upgrades** (built): levels, built at once, the effects, and the check with
   `--country`.
5. **Restyle:** house, fence, path and lamp choices, tints, kits and your emblem.
6. **Specials:** beast statues, trophies, more land.

## Decided and open

- [x] What upgrades do: **change the loop, never battle stats** (the user, 2026-11-03).
- [x] Machines **run on their own**. Beasts working them is a note for later (the user,
  2026-11-03).
- [x] **You choose which unused beasts are out and about** in the yard (the user, 2026-11-03).
- [x] Where decorations come from: **both**: shops and prizes, and crafting at the workbench
  from what machines make (the user, 2026-11-03).
- [x] Placing: **anywhere free**, as long as everything stays reachable (the user, 2026-11-03).
- [ ] Are the gilded and undercity kits gated by standing, and does the yard showing where you
  lean matter to the story?
- [ ] Can a machine make chem, or only goods, to keep chem coming from jobs? (Built as proposed:
  the still makes a little chem, 3 to 8 a day.)
- [x] The station upgrade effects (the user, 2026-11-03):
  - the pens make room for more beasts out;
  - the yard trains the beasts at home;
  - the war table pays more, then brings them back sooner;
  - the rest as proposed;
  - built at once, not over days.
- [ ] How many beasts can be out at once (six to start?), and can squad beasts be out too
  between battles?

## For later (notes, not in the plan)

- **Beasts working the machines** (taken out of the plan by the user, 2026-11-03, kept as a note).
  - Beasts left at home are put on a machine. One that suits it doubles the output: a spark
    beast on the still's boiler, a leaf beast on the beds, a spider on the loom.
  - Beasts could get better at farm work, all farm-only and never in battle:
    - experience on a machine, with three stars;
    - work tools crafted at the workbench (a harness, goggles, a bellows collar);
    - work traits bred for (Green Thumb, Strong Back);
    - undercity rigs counting as fitting the machinery;
    - perhaps a work tonic.
  - In the plan, machines run on their own, so this would sit on top of them later.
