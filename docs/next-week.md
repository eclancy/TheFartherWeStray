# Next week

Notes for what we build next week (started 2026-11-04). Tell Claude a note in any session and it
goes in **Your notes**. The list under it is what this week left open, to pick from.

## Your notes

**Priorities** (the user, 2026-11-04):

1. **Simplify the menus, first thing next week.**
   - An audit of every screen, and a proposed plan, are already written:
     `docs/menus-plan.md`.
   - **Part A (the cleanup) built on 2026-11-07** and merged.
   - **Part B, step 1 (the pause everywhere, Controls, first-time hints) built on 2026-11-07.**
   - **Part B, step 2 (one top bar, a station named once, home's name) built on 2026-11-07.**
   - **Part B, step 3 (one beast in the column, the squad picked at the pens, Build up at every
     station) built on 2026-11-07.**
   - **Part B, step 4 (the job tree: open classes first, Show locked, a filter) built on 2026-11-07.**
   - **Decided by the user on 2026-11-07: all of Part B goes in.**
     - Key help: hints the first few times, plus a Controls page in the pause menu.
     - Jobs: the open classes first, with "Show locked". The lab: one section at a time as tabs.
     - The squad: chosen at the pens only; the war table just fields tamers.
     - Pause on a desktop: Esc pauses everywhere; Back is a button.
     - One top bar everywhere; a station says what it is once; one beast in the column;
       "Build up" at each station too.
     - Home: "Home" by default, and the player can rename it.
2. **Real story cinematics, starting with a much longer introduction.** A new game can't just
   start at the ranch with no idea how you got there or what the ranch is.
   - The introduction tells who you are, how you came to the farmyard, and what the ranch is.
   - The premise already set (`docs/story-notes.md`): you grew up in the undercity, got out
     with a few beasts and a grudge, and now work a farmyard out past both cities.
   - The cutscene player is built (`scripts/Story/`). The scenes' words are the user's: write
     them together.
3. **A tutorial on how to battle**: taught in play, not a wall of text.
4. **How you get your first beasts**: shown in the story, not just handed over. Today a new game
   starts with four starters already in the pens (`Encounter.StarterSquad`).

**Anything else:**

-

## Left over from this week

### The farmyard (`docs/farmyard-plan.md`)

- **Phase 5, restyle the yard** (not started): choose the farmhouse's and the workshop's look
  (walls, roofs, windows, awnings, flowers, a sign with your mark), fences, path tiles, the lamps'
  colour, banners with your emblem; gilded, undercity and wild kits.
- **Phase 6, specials**: statues and topiary of your own beasts, trophies for cups and rival
  titles, more land.
- **Each station level's own look**: levels show only on the label now. The plan wants a bigger
  barn, more vats, lit windows.
- **More machines**: the net loom, the draught kettle, the water wheel (power for the machines
  beside it), and 2 x 2 machines.
- **Crafting** at the workbench from the press's scrap.
- **Small things**: turning a placed piece; tints.
- **Still open, for you to decide**:
  - Are the gilded and undercity kits gated by standing?
  - Can squad beasts be out in the yard between battles too?
  - Should the still keep making chem? (Built as the plan proposed: 3 to 8 a day.)
- **For later, not planned**: beasts working the machines, and getting better at it.

### The story (`docs/story-notes.md`)

- **Drawn portraits with expressions** for the story's characters, a line picking the one that
  suits it: decided on 2026-11-03, not built. Still open: which expressions.
- The first act's scenes, from the beats in the story notes.

### To check

- The first real update: a 0.3.0 copy updating itself to 0.4.0, on Windows and on a phone.
- Saying hello to a beast out in the yard, on screen (built and compiled, not tried).

### Small, offered

- Stat draughts could leave a visible mark on a beast (a faint glow in the draught's colour).
- `docs/game-ideas.md` has a status note from the story session that was never committed: keep
  it or drop it.
