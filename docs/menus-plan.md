# Simplifying the menus (plan, 2026-11-04)

**The user's words (2026-11-04):** "we need to immediately start focusing on simplifying menus."

The audit (2026-11-04) read every screen's code and screenshotted each one, on a desktop and as a
phone. **Part A, cleanup with no decisions in it, was built on 2026-11-07.** **Part B was decided
by the user on 2026-11-07: every proposal goes in**, as written below, with one change: home is
called "Home" by default and the player can rename it.

## What's wrong now

- **Every screen explains its keys, all the time.** The base, the stations, the map, the title,
  the shed and the cutscenes each have an always-on help line, and "N: sound" alone appears
  about seven times. On a phone, the status lines name keyboard letters ("pick the squad (S),
  let up to 9 others out (Y)").
- **A station is described five times:**
  - its label on the base;
  - the prompt with its blurb;
  - the header at the station;
  - an intro on the status line (written twice in the code);
  - a help line.
- **Three different top bars.** The base, the stations and the map each show a different set of
  counts. The stations' set includes "#1", the next encounter, which means nothing to a player.
  Chem and the day repeat again in the map's place panel and the shop.
- **A dead screen still in the code.** `RanchScreen`'s old all-in-one mode, from before the home
  base, can't be reached any more. Its messages still name keys that do nothing there: P, M, J,
  S, right-click.
- **The job tree shows all 51 classes at once**, in three sections on one scroll. Most are locked
  for any given beast ("9 open, 42 locked").
- **The lab shows five sections at once.** Each is headed by a line of jargon ("makes it infused
  · not inherited", "makes a chimera · its line carries it", "rigs it"), and part grafts alone
  can be 27 controls.
- **The beast column shows two beasts.** Its detail follows the pointer, its training follows
  the selection. It carries a parts line only the lab needs.
- **The squad is edited in two places:** the pens and the war table.
- **Hidden or uneven verbs:**
  - Station upgrades are only at the workbench, while machines upgrade from their own panel.
  - Breeding on a desktop is right-click twice, then Enter, with no button; the phone has one.
  - In battle, the Attack slot looks like a button but isn't, and both E and Space end the turn.
  - One place is called the ranch, the base and home.
- **No pause on a desktop at a station, on the map or in battle.** Esc there means "back".
- **Tab opens the Sprite Lab**, a development tool, in the shipped game.

## Principles

1. **One way to do each thing, called the same everywhere.**
2. **Controls when they're needed, not always.** A key hint shows the first few times, and the
   rest lives in a Controls page.
3. **Show what you can do now.** What's locked or rare sits behind "show all".
4. **Words only where an icon can't carry it.** Jargon goes in tooltips.
5. **Phone first.** Never name a keyboard key on a phone.
6. **The same top bar on every screen.**

## Part A: cleanup (no decisions; built 2026-11-07)

1. **Remove the dead old-ranch mode:**
   - in `RanchScreen`: the header buttons, the Class and Secondary buttons, the old key switch
     and help, `StartIn*`, `GoToMap`, the "J: job tree" chip;
   - in `MissionBoardPanel`: the leftover defaults.
2. **Fix the messages that name dead keys,** or keyboard keys on a phone. These are "(P)",
   "(M)", "(S)", "(Y)", "(right-click)" and "Press Enter to breed" (all of them on a phone),
   and the map's line about "(S)" at the ranch.
3. **Tab to the Sprite Lab only in the editor,** never in an exported game.
4. **The station intro written once,** not twice.
5. **The map's M no longer goes home** (a leftover from the old ranch screen). Esc and the Home
   button do.
6. **In battle, Space alone ends the turn** (not E as well), and the Attack slot works as a
   button. It already looks like one. (Built: the first press puts the cursor on the weakest foe
   in reach, the second strikes; it's dimmed when nothing is in reach, and drops an aimed skill.)
7. **A Breed button on a desktop too,** not only on a phone.

## Part B: decided (the user, 2026-11-07: all of it)

1. **Help on demand.** Drop the always-on key lines. A screen's hint shows the first few times
   it opens, and the pause menu gets a Controls page with every key, desktop and phone.
2. **One top bar:** beasts, squad, chem, scrap, day and the two standings, the same on the base,
   the stations and the map. "#encounter" goes.
3. **A station says what it is once:** its label on the base, its header inside. The status
   intro and the help line go.
4. **Jobs: the open classes first.** The classes this beast can train show; "Show locked (42)"
   reveals the rest. The three sections become a filter.
5. **The lab: one section at a time**, as tabs (Infuse, Blood, Splice, Augment, Graft), each with
   a plain one-line header. The jargon moves into tooltips.
6. **One beast in the column, the selected one.** The pointer only highlights. The parts line
   shows only at the lab, and open/locked/never only at the training yard.
7. **The squad in one place.** The pens pick it; the war table fields tamers and shows who leads
   whom.
8. **A pause everywhere on a desktop: Esc pauses on every screen.** Going back is the Back
   button (B on a phone), the way the phone already works.
9. **Upgrades where the thing is.** A station's own screen gets "Build up" too, as machines
   have; the workbench keeps it as well.
10. **One name for home, the player's to choose.** It's "Home" by default, and the player can
    rename it (a new feature: a saved name, used on the screen's title, the Back buttons and the
    map). Until it's renamed: "Home" is the screen's title, "Back home" the button, "Home" on
    the map.

## Order

Part A first. Then Part B, a screen at a time so each can be checked on a phone:

1. The pause and the key help, together. Esc pauses everywhere, and the Controls page takes over
   from the help lines. **Built 2026-11-07:** Esc (Start on a phone) opens the menu on every
   screen once nothing's open for it to close; time stops while it's open; Controls lists every
   key, the screen you're on first; each screen's key line shows its first three times. In battle
   Backspace or right-click undoes a move on a desktop, and on a phone Start is the menu there too
   (A on your own beast opens the command bar). A scene's menu has Skip the scene.
2. One top bar, a station saying what it is once, and home's name. **Built 2026-11-07:**
   `UI/TopBar.cs` on home, every station and the map: the screen's name, then beasts, squad,
   chem, scrap, the day and both standings (titles once there are any), Back home on the right.
   "#encounter", the map's drawn bars and the repeated day and chem in its place panel and shop
   are gone; so are the stations' intro and key lines. Home is "Home" until the player picks
   Name your home in its pause menu; the name is saved and shows in the bar, on the Back buttons
   ("Back to Thornfield") and on the map. On screen the ranch and the base are now "home".
3. The pens: one beast in the column, the squad chosen only there, and Build up at each station.
4. The job tree, with the open classes first.
5. The lab as tabs.
