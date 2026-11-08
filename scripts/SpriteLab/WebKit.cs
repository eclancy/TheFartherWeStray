using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TheFartherWeStray.Classes;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.SpriteLab;

/// <summary>
/// What the game's page on ecec.dev shows (the user's ask, 2026-11-03: a page in the game's own style):
/// a stray of every form, each a strip of frames (breathing, then a strike, a flinch and a cast) with
/// what the battle panel says about it; the menu frames as nine-slices for CSS border-image; and every
/// glyph. `--webkit=DIR` writes them, headless. The page's copy lives in the website's repo.
/// </summary>
public static class WebKit
{
    // The reels the page plays, in order along each strip: (pose, frames).
    static readonly (string name, Pose pose, int frames)[] Reels =
    {
        ("idle", Pose.Idle, 8), ("strike", Pose.Strike, 5), ("hurt", Pose.Hurt, 4), ("cast", Pose.Cast, 6),
    };

    public static void Write(string dir, ulong start)
    {
        Directory.CreateDirectory(Path.Combine(dir, "strays"));
        var strays = new List<object>();
        int n = 0;
        foreach (var form in Enum.GetValues<Form>().Where(f => f != Form.Auto))
        {
            var c = StrayOf(form, ref start);
            if (c == null) continue;
            string id = $"s{n++:00}";
            var (strip, w, h, ax, ay) = Strip(c);
            strip.SavePng(Path.Combine(dir, "strays", id + ".png"));
            strays.Add(Describe(c, id, w, h, ax, ay));
        }
        File.WriteAllText(Path.Combine(dir, "strays.json"), JsonSerializer.Serialize(new
        {
            reels = Reels.Select(r => new { r.name, r.frames }),
            fps = 8,
            strays,
        }, new JsonSerializerOptions { WriteIndented = true }));

        Frames(Path.Combine(dir, "frames"));
        Tile().SavePng(Path.Combine(dir, "tile.png"));
        GlyphAtlas(dir);
        GD.Print($"wrote {strays.Count} strays, the frames and {Glyphs.Names.Count()} glyphs to {dir}");
    }

    /// <summary>The first creature of this form from seed <paramref name="start"/> on, as a stray: no bigger than large, so it fits its tile.</summary>
    static Creature StrayOf(Form form, ref ulong start)
    {
        var plan = Enum.GetValues<BodyPlan>().First(p => Species.AllFormsFor(p).Contains(form));
        for (ulong seed = start; seed < start + 4000; seed++)
        {
            var sp = Species.Generate(seed);
            if (sp.Plan != plan) continue;
            sp.Form = form;
            var c = Creature.Generate(sp, SeededRng.Derive(seed, 1));
            if (c.Size > SizeClass.Large) c.Size = SizeClass.Large;
            start = seed + 1;
            return c;
        }
        return null;
    }

    /// <summary>Every reel's frames side by side, each the same size with the feet at the same point.</summary>
    static (Image strip, int w, int h, int ax, int ay) Strip(Creature c)
    {
        var shots = new List<(Image img, Vector2I a)>();
        foreach (var (_, pose, frames) in Reels)
            for (int f = 0; f < frames; f++)
                shots.Add(CreatureSculptor.Render(c, false, View.Front, pose == Pose.Idle ? f / (float)frames : f / (float)(frames - 1),
                    pose == Pose.Idle && f == frames - 1, pose));
        int left = shots.Max(s => s.a.X), right = shots.Max(s => s.img.GetWidth() - s.a.X);
        int up = shots.Max(s => s.a.Y), down = shots.Max(s => s.img.GetHeight() - s.a.Y);
        int w = left + right, h = up + down;
        var strip = Image.CreateEmpty(w * shots.Count, h, false, Image.Format.Rgba8);
        for (int i = 0; i < shots.Count; i++)
        {
            var (img, a) = shots[i];
            strip.BlendRect(img, new Rect2I(0, 0, img.GetWidth(), img.GetHeight()), new Vector2I(i * w + left - a.X, up - a.Y));
        }
        return (strip, w, h, left, up);
    }

    /// <summary>What the battle panel says about a stray, as data.</summary>
    static object Describe(Creature c, string id, int w, int h, int ax, int ay)
    {
        var u = Unit.From(c, Tactics.Side.Wild);
        var tier = Unit.From(c, Tactics.Side.Player).Tier; // what it becomes once caught: a thoroughbred, if it's natural
        var sp = c.Species;
        var (weapon, armor, move) = c.Loadout();
        var favoured = ClassBook.Affinity[sp.Plan].favoured
            .Select(name => ClassBook.Classes.FirstOrDefault(k => k.Name == name))
            .Where(k => k != null)
            .Select(k => new { name = k.Name, glyph = SkillIcons.ClassEmblem(k.Name), blurb = k.Blurb });
        return new
        {
            id,
            name = c.Name,
            form = sp.Form.ToString(),
            plan = sp.Plan.ToString().ToLowerInvariant(),
            size = c.Size.ToString().ToLowerInvariant(),
            element = sp.Element.ToString().ToLowerInvariant(),
            strain = sp.Strain == Strain.Natural ? null : sp.Strain.Label(),
            shiny = c.Shiny,
            stats = new { hp = u.MaxHp, atk = u.Atk, def = u.Def, spd = u.Spd, move = u.Move },
            weapon = new { name = u.Weapon.ToString(), glyph = SkillIcons.WeaponGlyph(u.Weapon), text = weapon },
            armor = new { name = u.Armor.ToString(), glyph = SkillIcons.ArmorGlyph(u.Armor), text = armor },
            moves = new { name = u.MoveType.ToString(), glyph = SkillIcons.MoveGlyph(u.MoveType), text = move },
            temper = new
            {
                name = c.Attitude.ToString(),
                glyph = SkillIcons.AttitudeGlyph(c.Attitude),
                reacts = AttitudeInfo.Reaction(c.Attitude),
                recruit = AttitudeInfo.Recruit(c.Attitude),
            },
            traits = u.Traits.Select(t => new { name = t.Name, text = t.Text }),
            favoured,
            caught = new { tier = tier.ToString(), glyph = TierBook.Glyph(tier), hex = TierBook.Hex(tier), text = TierBook.Describe(c, tier) },
            frame = new { w, h, ax, ay },
        };
    }

    /// <summary>The menu frames as the game draws them, for CSS border-image: a slice of `slice` pixels.</summary>
    static void Frames(string dir)
    {
        Directory.CreateDirectory(dir);
        var gold = Color.FromHtml("#f2c14e");
        var cyan = Color.FromHtml("#4fe3ff");
        var pink = Color.FromHtml("#ff4f9a");
        var list = new (string name, UiPart part, UiState state, bool lit, Color? light)[]
        {
            ("button", UiPart.Button, UiState.Normal, false, null),
            ("button-hover", UiPart.Button, UiState.Hover, false, null),
            ("button-pressed", UiPart.Button, UiState.Pressed, false, null),
            ("button-focus", UiPart.Button, UiState.Normal, true, null),
            ("button-gold", UiPart.Button, UiState.Hover, false, gold),
            ("button-cyan", UiPart.Button, UiState.Normal, false, cyan),
            ("button-pink", UiPart.Button, UiState.Normal, false, pink),
            ("small", UiPart.Small, UiState.Normal, false, null),
            ("small-hover", UiPart.Small, UiState.Hover, false, null),
            ("small-cyan", UiPart.Small, UiState.Normal, false, cyan),
            ("small-pink", UiPart.Small, UiState.Normal, false, pink),
            ("small-gold", UiPart.Small, UiState.Hover, false, gold),
            ("panel", UiPart.Panel, UiState.Normal, false, null),
            ("panel-cyan", UiPart.Panel, UiState.Normal, false, cyan),
            ("panel-gold", UiPart.Panel, UiState.Hover, false, gold),
            ("tip", UiPart.Tip, UiState.Normal, false, null),
        };
        foreach (var (name, part, state, lit, light) in list)
        {
            // As the game makes its nine-slices (UiSkin.Painted): corner 6 frame pixels, at 3x (2x for small).
            int px = part == UiPart.Small ? 2 : UiSkin.Px, m = 6, size = m * 2 + 2;
            var img = UiSkin.Paint(UI.Skin.Dusk, part, state, size, size, lit, light); // the game's look (Dusk, 2026-11-07)
            img.Resize(size * px, size * px, Image.Interpolation.Nearest);
            img.SavePng(Path.Combine(dir, name + ".png"));
        }
    }

    /// <summary>
    /// A board tile for a stray to stand on: the 48 x 24 diamond the board is built from, in the
    /// undercity's cobble colours, lit along its upper edges, with its two sides below.
    /// </summary>
    static Image Tile()
    {
        const int w = 48, half = 24, depth = 8;
        Color top = Color.FromHtml("#3b3552"), rim = Color.FromHtml("#5d557f"), left = Color.FromHtml("#262038"), right = Color.FromHtml("#1b1729");
        var img = Image.CreateEmpty(w + 1, half + depth + 1, false, Image.Format.Rgba8);
        for (int x = 0; x <= w; x++)
        {
            float lean = 1 - Mathf.Abs(x - half) / (float)half;
            int up = Mathf.RoundToInt(12 - 12 * lean), down = Mathf.RoundToInt(12 + 12 * lean);
            for (int y = up; y <= down; y++) img.SetPixel(x, y, y <= up + 1 ? rim : ((x + y) % 7 == 0 ? top.Lightened(0.06f) : top));
            for (int y = down + 1; y <= down + depth; y++) img.SetPixel(x, y, x < half ? left : right);
        }
        return img;
    }

    /// <summary>Every glyph in one sheet, 14 pixels a cell (the 12x12 art and its outline), with an index.</summary>
    static void GlyphAtlas(string dir)
    {
        var names = Glyphs.Names.OrderBy(n => n).ToList();
        const int cols = 16, cell = 14;
        int rows = (names.Count + cols - 1) / cols;
        var img = Image.CreateEmpty(cols * cell, rows * cell, false, Image.Format.Rgba8);
        for (int i = 0; i < names.Count; i++) Glyphs.Draw(img, names[i], (i % cols) * cell + 1, (i / cols) * cell + 1);
        img.SavePng(Path.Combine(dir, "glyphs.png"));
        File.WriteAllText(Path.Combine(dir, "glyphs.json"), JsonSerializer.Serialize(new
        {
            cell, cols,
            glyphs = names.Select((n, i) => new { n, i }).ToDictionary(g => g.n, g => g.i),
        }));
    }
}
