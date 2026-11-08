using System;
using System.Collections.Generic;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The game's own type (docs/ui-style.md): a pixel font drawn in code like the glyphs, after FFTA's small
/// proportional pixel type with a one-pixel shadow. Capitals and figures are 7 pixels tall, lower case 5
/// with 2 below the line; most capitals are 5 wide and most lower case 4, a pixel apart. A bitmap font at
/// <see cref="Native"/>, scaled by whole pixels only (Godot's integer-only fixed size rounds the size
/// over Native): 14-22 point words draw at 2x, 23-31 at 3x and so on (so, as in FFTA, nearly all words are
/// one size), and its pixels stay square and match the board's. Characters it doesn't draw (a name
/// typed with an accent it lacks) come from the plain sans.
/// </summary>
public static class PixelFont
{
    /// <summary>The size the font is drawn at 1:1; a label's font size over this, rounded, is its scale.</summary>
    public const int Native = 9;

    /// <summary>Rows above the line (a blank row over the capitals, for line spacing), the capitals' height, rows below.</summary>
    public const int Ascent = 8, Cap = 7, Descent = 2;

    const int Rows = Cap + Descent;

    /// <summary>How many screen pixels a font pixel is at a font size.</summary>
    public static int Scale(int size) => Math.Max(1, (int)Math.Round((double)size / Native, MidpointRounding.AwayFromZero));

    static FontFile _font;

    /// <summary>The font, built once.</summary>
    public static FontFile Font => _font ??= Build(Shapes);

    static FontFile Build(Dictionary<int, string[]> shapes)
    {
        var sans = ThemeDB.FallbackFont;
        int atlasW = 1;
        foreach (var rows in shapes.Values) atlasW += rows[0].Length + 1;
        var img = Image.CreateEmpty(atlasW, Rows + 2, false, Image.Format.La8);
        var font = new FontFile
        {
            FixedSize = Native,
            FixedSizeScaleMode = TextServer.FixedSizeScaleMode.IntegerOnly,
            Antialiasing = TextServer.FontAntialiasing.None,
            Hinting = TextServer.Hinting.None,
            SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
            AllowSystemFallback = true,
        };
        var key = new Vector2I(Native, 0);
        int x = 1;
        foreach (var (ch, rows) in shapes)
        {
            int w = rows[0].Length;
            for (int y = 0; y < Rows; y++)
            for (int i = 0; i < w; i++)
                if (rows[y][i] == '#') img.SetPixel(x + i, y + 1, Colors.White);
            font.SetGlyphAdvance(0, Native, ch, new Vector2(w + 1, 0));
            font.SetGlyphOffset(0, key, ch, new Vector2(0, -Cap));
            font.SetGlyphSize(0, key, ch, new Vector2(w, Rows));
            font.SetGlyphUVRect(0, key, ch, new Rect2(x, 1, w, Rows));
            font.SetGlyphTextureIdx(0, key, ch, 0);
            x += w + 1;
        }
        foreach (int space in new[] { ' ', 0xA0 })
        {
            font.SetGlyphAdvance(0, Native, space, new Vector2(3, 0));
            font.SetGlyphOffset(0, key, space, Vector2.Zero);
            font.SetGlyphSize(0, key, space, Vector2.Zero);
            font.SetGlyphUVRect(0, key, space, new Rect2(0, 0, 0, 0));
            font.SetGlyphTextureIdx(0, key, space, -1);
        }
        font.SetTextureImage(0, key, 0, img);
        font.SetCacheAscent(0, Native, Ascent);
        font.SetCacheDescent(0, Native, Descent);
        font.SetCacheUnderlinePosition(0, Native, 1);
        font.SetCacheUnderlineThickness(0, Native, 1);
        if (sans != null) font.Fallbacks = new Godot.Collections.Array<Font> { sans };
        return font;
    }

    static Dictionary<int, string[]> _shapes;

    /// <summary>Every glyph as rows of '#' (ink) and '.', from the capitals' top down through the two rows below the line.</summary>
    static Dictionary<int, string[]> Shapes => _shapes ??= Parse();

    static Dictionary<int, string[]> Parse()
    {
        var shapes = new Dictionary<int, string[]>();
        foreach (var (ch, art) in Art)
        {
            var rows = art.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (rows.Length != Cap && rows.Length != Rows) throw new Exception($"PixelFont: '{ch}' has {rows.Length} rows");
            int w = rows[0].Length;
            var all = new string[Rows];
            for (int y = 0; y < Rows; y++)
            {
                all[y] = y < rows.Length ? rows[y] : new string('.', w);
                if (all[y].Length != w) throw new Exception($"PixelFont: '{ch}' row {y} is {all[y].Length} wide, not {w}");
            }
            shapes[ch] = all;
        }
        // Accented vowels: the letter with its accent in the two rows over the lower case.
        var accents = new Dictionary<char, string[]>
        {
            ['´'] = new[] { "..#.", ".#.." }, ['`'] = new[] { ".#..", "..#." }, ['^'] = new[] { ".##.", "#..#" },
            ['¨'] = new[] { "#..#", "...." }, ['~'] = new[] { ".#.#", "#.#." },
        };
        foreach (var (accented, letter, accent) in new[]
        {
            ('á', 'a', '´'), ('à', 'a', '`'), ('â', 'a', '^'), ('ä', 'a', '¨'), ('ã', 'a', '~'),
            ('é', 'e', '´'), ('è', 'e', '`'), ('ê', 'e', '^'), ('ë', 'e', '¨'),
            ('ó', 'o', '´'), ('ò', 'o', '`'), ('ô', 'o', '^'), ('ö', 'o', '¨'), ('õ', 'o', '~'),
            ('ú', 'u', '´'), ('ù', 'u', '`'), ('û', 'u', '^'), ('ü', 'u', '¨'), ('ñ', 'n', '~'),
        })
        {
            var rows = (string[])shapes[letter].Clone();
            rows[0] = accents[accent][0];
            rows[1] = accents[accent][1];
            shapes[accented] = rows;
        }
        return shapes;
    }

    /// <summary>
    /// The glyphs, 7 rows (capital height, on the line) or 9 (with the two below it), rows split by spaces.
    /// Original shapes for this game: plain and upright, a flat-topped A, a tailed l, a single-storey g.
    /// </summary>
    static readonly (int ch, string art)[] Art =
    {
        ('A', ".###. #...# #...# ##### #...# #...# #...#"),
        ('B', "####. #...# #...# ####. #...# #...# ####."),
        ('C', ".###. #...# #.... #.... #.... #...# .###."),
        ('D', "####. #...# #...# #...# #...# #...# ####."),
        ('E', "#### #... #... ###. #... #... ####"),
        ('F', "#### #... #... ###. #... #... #..."),
        ('G', ".###. #...# #.... #.### #...# #...# .###."),
        ('H', "#...# #...# #...# ##### #...# #...# #...#"),
        ('I', "### .#. .#. .#. .#. .#. ###"),
        ('J', "...# ...# ...# ...# #..# #..# .##."),
        ('K', "#...# #..#. #.#.. ##... #.#.. #..#. #...#"),
        ('L', "#... #... #... #... #... #... ####"),
        ('M', "#...# ##.## #.#.# #.#.# #...# #...# #...#"),
        ('N', "#...# #...# ##..# #.#.# #..## #...# #...#"),
        ('O', ".###. #...# #...# #...# #...# #...# .###."),
        ('P', "####. #...# #...# ####. #.... #.... #...."),
        ('Q', ".###. #...# #...# #...# #.#.# #..#. .##.#"),
        ('R', "####. #...# #...# ####. #.#.. #..#. #...#"),
        ('S', ".###. #...# #.... .###. ....# #...# .###."),
        ('T', "##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#.."),
        ('U', "#...# #...# #...# #...# #...# #...# .###."),
        ('V', "#...# #...# #...# #...# .#.#. .#.#. ..#.."),
        ('W', "#...# #...# #...# #.#.# #.#.# #.#.# .#.#."),
        ('X', "#...# #...# .#.#. ..#.. .#.#. #...# #...#"),
        ('Y', "#...# #...# .#.#. ..#.. ..#.. ..#.. ..#.."),
        ('Z', "##### ....# ...#. ..#.. .#... #.... #####"),

        ('a', ".... .... .##. ...# .### #..# .###"),
        ('b', "#... #... ###. #..# #..# #..# ###."),
        ('c', ".... .... .### #... #... #... .###"),
        ('d', "...# ...# .### #..# #..# #..# .###"),
        ('e', ".... .... .##. #..# #### #... .###"),
        ('f', ".## #.. ### #.. #.. #.. #.."),
        ('g', ".... .... .### #..# #..# #..# .### ...# ###."),
        ('h', "#... #... ###. #..# #..# #..# #..#"),
        ('i', "# . # # # # #"),
        ('j', ".# .. .# .# .# .# .# .# #."),
        ('k', "#... #... #..# #.#. ##.. #.#. #..#"),
        ('l', "#. #. #. #. #. #. .#"),
        ('m', "..... ..... ####. #.#.# #.#.# #.#.# #.#.#"),
        ('n', ".... .... ###. #..# #..# #..# #..#"),
        ('o', ".... .... .##. #..# #..# #..# .##."),
        ('p', ".... .... ###. #..# #..# #..# ###. #... #..."),
        ('q', ".... .... .### #..# #..# #..# .### ...# ...#"),
        ('r', ".... .... #.## ##.. #... #... #..."),
        ('s', ".... .... .### #... .##. ...# ###."),
        ('t', ".#. .#. ### .#. .#. .#. ..#"),
        ('u', ".... .... #..# #..# #..# #..# .###"),
        ('v', "..... ..... #...# #...# .#.#. .#.#. ..#.."),
        ('w', "..... ..... #...# #...# #.#.# #.#.# .#.#."),
        ('x', ".... .... #..# #..# .##. #..# #..#"),
        ('y', ".... .... #..# #..# #..# #..# .### ...# .##."),
        ('z', ".... .... #### ...# .##. #... ####"),

        ('0', ".##. #..# #..# #..# #..# #..# .##."),
        ('1', ".#. ##. .#. .#. .#. .#. ###"),
        ('2', ".##. #..# ...# ..#. .#.. #... ####"),
        ('3', "###. ...# ...# .##. ...# ...# ###."),
        ('4', "#..# #..# #..# #### ...# ...# ...#"),
        ('5', "#### #... #... ###. ...# ...# ###."),
        ('6', ".##. #... #... ###. #..# #..# .##."),
        ('7', "#### ...# ...# ..#. ..#. .#.. .#.."),
        ('8', ".##. #..# #..# .##. #..# #..# .##."),
        ('9', ".##. #..# #..# .### ...# ...# .##."),

        ('!', "# # # # # . #"),
        ('"', "#.# #.# ... ... ... ... ..."),
        ('#', "..... .#.#. ##### .#.#. ##### .#.#. ....."),
        ('$', "..#.. .#### #.#.. .###. ..#.# ####. ..#.."),
        ('%', "..... ##..# ##.#. ..#.. .#.## #..## ....."),
        ('&', ".##.. #..#. #.#.. .#... #.#.# #..#. .##.#"),
        ('\'', "# # . . . . ."),
        ('(', ".# #. #. #. #. #. .#"),
        (')', "#. .# .# .# .# .# #."),
        ('*', "... #.# .#. #.# ... ... ..."),
        ('+', "... ... .#. ### .#. ... ..."),
        (',', ".. .. .. .. .. .. .# #. .."),
        ('-', "... ... ... ### ... ... ..."),
        ('.', ". . . . . . #"),
        ('/', "..# ..# .#. .#. .#. #.. #.."),
        (':', ". . . # . . #"),
        (';', ".. .. .. .# .. .. .# #. .."),
        ('<', "... ..# .#. #.. .#. ..# ..."),
        ('=', ".... .... #### .... #### .... ...."),
        ('>', "... #.. .#. ..# .#. #.. ..."),
        ('?', ".##. #..# ...# ..#. .#.. .... .#.."),
        ('@', ".###. #...# #.### #.#.# #.### #.... .####"),
        ('[', "## #. #. #. #. #. ##"),
        ('\\', "#.. #.. .#. .#. .#. ..# ..#"),
        (']', "## .# .# .# .# .# ##"),
        ('^', ".#. #.# ... ... ... ... ..."),
        ('_', ".... .... .... .... .... .... .... #### ...."),
        ('`', "#. .# .. .. .. .. .."),
        ('{', "..# .#. .#. #.. .#. .#. ..#"),
        ('|', "# # # # # # # # #"),
        ('}', "#.. .#. .#. ..# .#. .#. #.."),
        ('~', "..... ..... .##.# #.##. ..... ..... ....."),

        ('·', ". . . # . . ."),
        ('•', ".. .. ## ## .. .. .."),
        ('▼', "..... ..... ##### .###. ..#.. ..... ....."),
        ('▲', "..... ..... ..#.. .###. ##### ..... ....."),
        ('—', "....... ....... ....... ####### ....... ....... ......."),
        ('–', "..... ..... ..... ##### ..... ..... ....."),
        ('’', ".# #. .. .. .. .. .."),
        ('‘', ".# #. .. .. .. .. .."),
        ('“', ".#.# #.#. .... .... .... .... ...."),
        ('”', ".#.# #.#. .... .... .... .... ...."),
        ('…', "..... ..... ..... ..... ..... ..... #.#.#"),
        ('×', "... ... #.# .#. #.# ... ..."),
        ('°', ".#. #.# .#. ... ... ... ..."),
        ('→', "..... ..#.. ...#. ##### ...#. ..#.. ....."),
        ('←', "..... ..#.. .#... ##### .#... ..#.. ....."),
        ('↑', "..#.. .###. #.#.# ..#.. ..#.. ..#.. ....."),
        ('↓', "..#.. ..#.. ..#.. #.#.# .###. ..#.. ....."),
        ('★', "..#.. ..#.. ##### .###. .#.#. #...# ....."),
    };
}
