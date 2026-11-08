# Menu and button style

**Picked: D, Gilt and neon** (the user, 2026-11-03), and built: it is the game's default look, on
every button, panel, tooltip, slider, card and the phone pad. The other three stay as `--ui=NAME`
to compare against. What follows is the research and the comparison the pick was made from.

**Picked again 2026-11-07: E, Dusk**, with the game's own pixel font everywhere (the user, after "we
need a more signature text style, and the menu contrast is too drastic... take inspiration from ffta
in terms of using color, menus, and text style"). Lamplight, Verdigris and Gilt and neon stay as the
player's options in Settings (Menu look). The FFTA notes, the font and the candidates are in
[Softer gilt and the game's own type](#softer-gilt-and-the-games-own-type) at the end.

The menus and buttons didn't look like this game. They used Godot's default grey buttons, flat
dark boxes with round corners and thin cyan or gold borders, and the default sans font. Next to the
pixel-art board they could have belonged to any game. This doc collects the research, sets the
rules any style has to keep, and compares four candidate styles, each built in the game
(`scripts/UI/UiSkin.cs`).

To look at the candidates:

- `--uiboard=gilded|neon|iron|giltneon|plain --shot=30` shows one style's parts side by side over
  Lantern Row: a menu with a button in each state, the pens window with cards and small buttons, a
  tooltip, a dialogue box, a unit panel and the skill bar.
- `--ui=NAME` puts a style on every button, panel and tooltip in the game (try it with the title,
  `--phone`, or any screen flag).
- Renders are in `.shots/ui/`: `styles_compared.png`, `titles_compared.png`, `board_NAME.png`,
  `title_NAME.png` and `phone_title_giltneon.png`.

## What we're after

The game's art direction (CLAUDE.md) sets the target:

- Cool, not charming.
- Industrial fantasy: a magic world in its industrial age.
- A gilded city above (royal, spires, old money) and an undercity of smog, pipes, alchemy and
  glowing chemicals, with neon signs. No computers.
- The board is FFTA's and Fell Seal's isometric pixel art in the "Neon Undercity" look.

It also sets the readability rules: a glyph before every stat, icons over sentences, full words
only in tooltips, and colour only as a second cue. On a phone, the pad's focus has to be easy to
see on every control.

## Research

**Arcane (the look, never the words).** Riot's and Fortiche's teams built the upper city on Art
Deco and Art Nouveau: gilded symmetry and sleek geometry, drawing on Streamline Moderne and Mucha.
They built the undercity as its opposite: industrial and neon-drenched, after early 20th-century
underground engineering ([80.lv](https://80.lv/articles/riot-games-on-designing-arcane-s-piltover-zaun/)).
*Take:* the city's language is geometry and gold; the undercity's is light on grime. The rivalry
between the two cities can live in the frames themselves.

**Art Deco ornament.** The motifs are stepped forms (a ziggurat's setbacks), chevrons and sunbursts,
always symmetrical and repeated in a rhythm
([illustrarch](https://illustrarch.com/articles/34588-art-deco-geometric-designs.html)). *Take:*
stepped corners are cheap in pixel art and still read when small. Sunbursts suit headings and the
title.

**BioShock Infinite.** The team began with Art Nouveau but found it too costly. They moved to a
Beaux-Arts look and kept Nouveau for decorative frames and signs
([Engadget](https://www.engadget.com/2011-06-10-lead-artist-shawn-robertson-talks-the-aesthetic-evolution-of-bio.html)).
*Take:* ornament belongs in frames and signs, not everywhere.

**Frostpunk.** Its UI is built from circles, echoing the generator at the centre of its city
([Xbox Wire](https://news.xbox.com/en-us/2019/09/20/frostpunk-console-edition-radial-driven-design-of-gameplay-and-controls/)).
Heavy stamped lettering is kept for the logo; the working UI uses plain type
([Made Good Designs](https://madegooddesigns.com/frostpunk-font/)). *Take:* a UI can echo the
setting's central object. Ornament goes on headings and frames; numbers and rules stay plain.

**Persona 5.** A strong identity from a few fixed rules: red, black and white, with jagged angles
([Siliconera](https://www.siliconera.com/?p=668266)). Critics note how hard it can be to parse,
and Persona 5 Tactica moved toward clarity
([Inverse](https://inverse.com/gaming/persona-5-tactica-user-interface)). *Take:* identity comes
from a small set of rules, never at the cost of reading.

**FFTA and Fell Seal.** Both use framed windows over the board. Fell Seal's UI splits reviewers:
GameBanshee calls it unwieldy ([GameBanshee](https://www.gamebanshee.com/38wh8)), RPGFan smooth
([RPGFan](https://www.rpgfan.com/review/fell-seal-arbiters-mark/)). *Take:* panels have to hold
up over busy pixel art, and the cursor has to be obvious.

**Pixel UI practice.** Nine-slice frames keep their corners and stretch their edges and middle,
so ornament goes in the corners and the edges stay plain
([Phaser](https://phaser.io/packs/nine-slice)). Dim what's behind a modal and always offer a way
out ([Generalist Programmer](https://generalistprogrammer.com/tutorials/game-ui-design-best-practices)).
Segmented bars read faster than smooth ones ([Wayline](https://www.wayline.io/learn/game-ui-art/3)).

## Rules any style keeps

1. **Frames are pixel art at the board's pixel size.** A frame pixel is 3 screen pixels, painted
   in code like the sprites (`UiSkin.Painted`). Each frame is a nine-slice with its ornament in the
   corners.
2. **Focus changes light or shape, not only colour.** The pad has to be findable at a glance, so a
   tube lights, a lamp comes on, or a bar of gold appears.
3. **Words stay in the plain sans.** Numbers, rules and tooltips keep today's font. Only headings
   might get a display face (open question 2). *(Replaced 2026-11-07: every word is in the game's
   own pixel font, below.)*
4. **States differ in brightness too**, so they still read without colour.
5. **It sits over every board:** the farmyard, the gilded streets and the undercity.
6. **No screen look:** no thin sci-fi lines, hex grids, scanlines or terminal type.

## The candidates

### A. Gilded (the city above)

Deep navy glass inside a gold line with stepped Art Deco corners, and a fainter gold line inside
that, like a gilded picture frame. Panels have a gold jewel in each corner. Hovering brightens the
gold. Focus makes it pale and runs a bar of gold leaf down the left side. Pressed darkens the
glass; disabled turns the gold to grey.

- **For:** elegant, clearly fantasy, and calm over a busy board.
- **Against:** it is only the city's look, so the undercity, the game's default board, has no
  voice in it. Gold on navy is also close to a very well-known game client's look. Focus and hover
  differ only a little.

### B. Neon sign (the undercity)

Sooty glass with a neon tube round it. A button's tube is unlit glass until you're on it, then it
lights cyan with a glow, and pink as it's pressed. Panels glow pink and tooltips amber.

- **For:** the most striking of the four, and it matches the Neon Undercity board. Focus is the
  clearest of all: a sign switching on.
- **Against:** every menu becomes the undercity's, including the farm and the city above. Panels
  glowing everywhere tire the eye and compete with the board's own neon. The style is the closest
  to the screen look the art direction wants to avoid.

### C. Foundry iron (industry)

An iron band, lit from the top left, around a sunken plate. Buttons are green enamel; panels are
bare iron with a rivet in each corner. Every button has a small lamp in its top-left corner. Focus
lights the lamp amber and warms the rim to copper. Pressed, the bevel turns over.

- **For:** the most tactile, with a diegetic focus (a lamp) and a colour of its own (enamel green).
- **Against:** the heaviest look; it takes the most room on a phone and leans furthest toward
  steampunk. The lamp is small at button size, and the enamel can look like a flat green block.

### D. Gilt and neon (both cities) — recommended

The city's gold line with stepped corners, round smoked violet glass. Inside the gold, the
undercity's neon: an unlit tube that lights cyan when you're on it and pink as it's pressed.
Panels keep their tube dimly lit violet, with a cyan pip in each corner.

- **For:** the story in one frame: the city's gilt holding the undercity's light. Focus is nearly
  as clear as B's (a tube lights up inside the gold), and it sits over the farm, the city and the
  undercity alike. It's one system to keep, not three.
- **Against:** two ideas in one frame need care, or it gets busy. Hover and focus are close
  (both light the tube); hover should only half-light it.

|                                | A Gilded       | B Neon sign       | C Foundry iron  | D Gilt and neon |
| ------------------------------ | -------------- | ----------------- | --------------- | --------------- |
| Says "this game"               | city only      | undercity only    | industry        | both cities     |
| Pad focus at a glance          | good           | best              | fair (small)    | very good       |
| Calm over a busy board         | best           | weakest           | good            | good            |
| Avoids the screen look         | yes            | at risk           | yes             | yes             |
| Room on a phone                | good           | good              | most frame      | good            |

## The recommendation

**D, Gilt and neon.** It's the only one that holds the game's theme, the two cities, in every
frame. Its focus works for the pad, and it doesn't fight the board. If one city should own the
menus, B is the striking choice and A the calm one; C is the one to pick for a heavier, hands-on
industrial feel.

## After the pick

- Every button, panel, tooltip and text field takes the style through the theme (`UiSkin.Use`, as
  the mockups do now).
- Every screen's own boxes go through `UiSkin.Box`: the pens' cards, the mission cards, the lab,
  the war table, the battle HUD panels and skill bar, the dialogue and choice boxes, the slot,
  settings and pause panels. That's about 45 hand-made boxes today (`StyleBoxFlat`s, `ColorRect`s and drawn rectangles), most of them in
  `RanchScreen`, `BattleScreen`, `CountryScreen` and `LabPanel`.
- A compact frame for small buttons (at 2 pixels, not 3), so the lab's and yard's buttons don't
  grow.
- Hover and focus made distinct, and a disabled state that still reads.
- The phone pad drawn in the same style: the D-pad as a gilt plate, A and B as rings that light
  when pressed.
- Optional: a soft click or hum as the focus moves; headings in a display face.
- Plain stays as `--ui=plain` until the change is done, then goes.

## Open questions

1. ~~Which style?~~ D, Gilt and neon.
2. ~~Headings: keep the plain sans in capitals, or draw a display face in code?~~ Every word is in
   the game's pixel font now (2026-11-07); headings are set apart by colour, as in FFTA.
3. A soft click or hum as the pad's focus moves?

## Softer gilt and the game's own type

The user's note on 2026-11-07: the menus need a signature text style, their contrast is too
drastic, and FFTA is the model for colour, menus and text. The current look is near-black glass
(`#120e22`) inside a bright gold line and a lit cyan tube, with white words in the default sans and
a heavy black outline round them. Each step jumps from near black to near full brightness.

### What FFTA does (principles, not its assets)

Final Fantasy Tactics Advance (2003) was made for the Game Boy Advance, a small screen with no
backlight, and for a younger audience ([Wikipedia](https://en.wikipedia.org/wiki/Final_Fantasy_Tactics_Advance)).
Its colour is lifted to suit that. What we take from it, from playing it and from screenshots (no
FFTA fonts, art or layouts are used):

1. **Windows aren't black.** A window is a mid tone of one hue: light enough that its frame and its
   words sit a step or two away from it, not at opposite ends of the scale.
2. **Short ramps.** On the GBA a window, its frame and its text come from a handful of neighbouring
   tones, so the frame reads as a lit edge of the window rather than a line drawn round it.
3. **The frame is the window's edge.** A soft lighter rim, not a hard line in another colour, so a
   window reads as one object over the board. Ours go a step further and light the gold from the
   top left, like a raised plate.
4. **One small pixel type, two tones.** Every word is in one small proportional pixel font, nearly
   all of it at one size. Each letter is drawn in its colour with a one-pixel shadow down and
   right in a dark of the window's own hue (a GBA font is main colour, shadow and background). No
   outlines.
5. **Colour points, it doesn't shout.** Most words are one warm light colour; names, numbers and
   the cursor carry the few accents, and the accents are muted enough to sit on the window.
6. **Hierarchy by place and colour, not size.** Headings are the same size as body words, set
   apart by colour, by a band or by their position in the window.
7. **The cursor is a thing, not a glow.** Focus is a pointer and a lit edge, easy to find on a
   phone.

### The game's own type

`UI/PixelFont.cs` draws a pixel font in code, as the glyphs and sprites are, so it's the game's and
nobody else's. Capitals and figures are 7 pixels tall and lower case 5, with 2 below the line; most
capitals are 5 wide and lower case 4, a pixel apart.
Godot scales it by whole pixels only, so its pixels stay square like the board's: 14-22
point words draw at 2x, 23-31 at 3x and so on, which puts nearly every word at one size, as FFTA
does. Words in a battle are drawn at 1x on the board, a font pixel per board pixel. A character it
lacks comes from the plain sans.

Outlines don't draw on a bitmap font, so the words take FFTA's treatment: a shadow one font pixel
down and right in the window's own dark (`Ui.Dress`). Words smaller than 14 points come up to 14
(`Ui.Fit`), since 1x is too small to read.

### Three softer versions of the gilt frame

All three keep gilt and neon's frame (the stepped Art Deco corners, the gold line, the tube inside
it, the panel's corner pips) and its rules: focus still lights the tube, pressed still turns it
pink. What changes is the steps between glass, frame and words (`UI/Looks.cs`):

- the glass is lifted off black to a dusky mid tone, nearly opaque;
- the outer edge is the glass's own dark, not black;
- the gold is a step quieter and lit from the top left;
- the lights are softened: a screen's cyan, gold, pink and green become the look's own
  (`Look.Soften`), so the pens' cards still say chosen, squad, vat and yard, more quietly;
- the words are ivory or cream in the pixel font with the shadow above.

**E, Dusk** keeps gilt and neon's violet: plum glass, antique gold, sea-glass and rose lights, ivory
words. The closest to what the user picked on 2026-11-03.

**F, Lamplight** is the city's gilt by gaslight: umber glass like an old ledger, a brass frame,
verdigris and coral lights, cream words. The warmest, and the nearest FFTA's own warm windows.

**G, Verdigris** is the undercity's: smoked teal glass, brass gone green on its shaded side,
chem-green and rose lights, mint words.

|                              | D Gilt and neon | E Dusk          | F Lamplight     | G Verdigris     |
| ---------------------------- | --------------- | --------------- | --------------- | --------------- |
| Glass                        | near black      | dusky plum      | umber           | smoked teal     |
| Frame                        | bright gold     | antique gold    | brass           | brass, patina   |
| Focus                        | cyan            | sea glass       | verdigris       | chem green      |
| Words                        | sans, outlined  | pixel, shadowed | pixel, shadowed | pixel, shadowed |
| Leans                        | both cities     | both cities     | the city above  | the undercity   |

To look at them: `--ui=dusk|lamplight|verdigris` on any screen (`--phone` too), `--uiboard=NAME
--shot=30` for the parts side by side, `--font=pixel|sans` to swap the words alone, and
`--typesheet --shot=10` for the type at every size.

**Picked:** E, Dusk, as the default (the user, 2026-11-07), with the pixel font everywhere. The
others are the player's to pick in Settings (Menu look: Dusk, Lamplight, Verdigris, Gilt and neon),
and switch at once: the theme changes, and the screen in view is made again as the settings close.
