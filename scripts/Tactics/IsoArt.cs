using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CreatureTactics.Tactics;

/// <summary>
/// Paints the isometric board as pixel art, FFTA-style: diamond tiles raised on blocks with lit and
/// shaded sides, in an undercity palette (cobbles, moss, brass platforms, glowing chem). Also the
/// backdrop: a skyline of brass towers above and neon-lit slums below.
/// </summary>
public sealed class IsoArt
{
    public const int TW = 48, TH = 24, HS = 12; // tile width, tile height, pixels per height level
    const int BaseDepth = 8, Margin = 8;

    readonly BattleMap _map;
    readonly Look _look;
    public readonly int Width, Height;
    readonly int _ox, _oy;

    public IsoArt(BattleMap map)
    {
        _map = map;
        _look = Look.Get(map.Theme);
        int maxH = 0;
        for (int y = 0; y < map.H; y++)
        for (int x = 0; x < map.W; x++) maxH = Math.Max(maxH, map.HeightAt(new Vector2I(x, y)));
        _ox = map.H * TW / 2 + Margin;
        _oy = maxH * HS + Margin;
        Width = (map.W + map.H) * TW / 2 + Margin * 2;
        Height = (map.W + map.H) * TH / 2 + maxH * HS + BaseDepth + Margin * 2;
    }

    /// <summary>Board-pixel position of a tile's top corner at a height. Tile coords may be fractional.</summary>
    public Vector2 TileTop(Vector2 t, int h) => new(_ox + (t.X - t.Y) * TW / 2f, _oy + (t.X + t.Y) * TH / 2f - h * HS);

    /// <summary>The centre of a tile's top face.</summary>
    public Vector2 TileCenter(Vector2 t, int h) => TileTop(t, h) + new Vector2(0, TH / 2f);

    public Vector2[] Diamond(Vector2I t, float inset = 0)
    {
        var top = TileTop(t, _map.HeightAt(t));
        float hw = TW / 2f - inset * 2, hh = TH / 2f - inset;
        var c = top + new Vector2(0, TH / 2f);
        return new[] { c + new Vector2(0, -hh), c + new Vector2(hw, 0), c + new Vector2(0, hh), c + new Vector2(-hw, 0) };
    }

    /// <summary>Tiles in painter's order, back to front.</summary>
    public IEnumerable<Vector2I> DrawOrder()
    {
        for (int s = 0; s < _map.W + _map.H - 1; s++)
        for (int x = 0; x < _map.W; x++)
        {
            int y = s - x;
            if (y >= 0 && y < _map.H) yield return new Vector2I(x, y);
        }
    }

    /// <summary>The tile whose top face is under a board-pixel point, front-most first.</summary>
    public Vector2I? Pick(Vector2 p)
    {
        Vector2I? hit = null;
        foreach (var t in DrawOrder())
        {
            var c = TileCenter(t, _map.HeightAt(t));
            if (Mathf.Abs(p.X - c.X) / (TW / 2f) + Mathf.Abs(p.Y - c.Y) / (TH / 2f) <= 1) hit = t;
        }
        return hit;
    }

    // ------------------------------------------------------------------ board

    static Color C(string hex) => Color.FromHtml(hex);

    static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + salt * 2246822519);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    public Image PaintBoard(double time = 0)
    {
        var img = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        foreach (var t in DrawOrder()) PaintTile(img, t);
        Creatures.ArtStyle.Current.GradeImage(img);
        return img;
    }

    void PaintTile(Image img, Vector2I t)
    {
        var terrain = _map.TerrainAt(t);
        int h = _map.HeightAt(t);
        var top = TileTop(t, h);
        int depth = h * HS + BaseDepth;
        float hw = TW / 2f, hh = TH / 2f;
        var L = top + new Vector2(-hw, hh);
        var B = top + new Vector2(0, TH);
        var R = top + new Vector2(hw, hh);
        var down = new Vector2(0, depth);

        if (terrain == Terrain.Pit)
        {
            // A hole: just darkness with a faint glow far below.
            Fill(img, new[] { top, R, B, L }, (x, y, u, v) =>
            {
                float d = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                return d > 0.44f ? C("#1b1426") : (Hash(x, y, 3) % 23 == 0 ? C("#3b1f4a") : C("#0a0710"));
            });
            return;
        }

        var (sideL, sideR) = Sides(terrain);
        // Left face is lit, right face in shadow; a darker band where block meets block.
        Fill(img, new[] { L, B, B + down, L + down }, (x, y, u, v) =>
        {
            Color c = sideL;
            if (terrain is Terrain.Stone or Terrain.Grass && ((y - (int)top.Y) % 6 == 0)) c = sideL.Darkened(0.18f);
            if (terrain == Terrain.Brass && Hash(x, y, 9) % 17 == 0) c = sideL.Lightened(0.2f);
            if (terrain == Terrain.Chem && (x + y) % 9 == 0) c = C("#2f8f5e");
            return c;
        });
        Fill(img, new[] { B, R, R + down, B + down }, (x, y, u, v) =>
        {
            Color c = sideR;
            if (terrain is Terrain.Stone or Terrain.Grass && ((y - (int)top.Y + 3) % 6 == 0)) c = sideR.Darkened(0.18f);
            return c;
        });
        Fill(img, new[] { top, R, B, L }, (x, y, u, v) => TopColor(terrain, x, y, u, v));

        // Crisp FFTA edges: lit back rims, dark front rims.
        Line(img, top, L, TopColor(terrain, 0, 0, 0.5f, 0.5f).Lightened(0.28f));
        Line(img, top, R, TopColor(terrain, 0, 0, 0.5f, 0.5f).Lightened(0.15f));
        Line(img, L, B, sideL.Darkened(0.35f));
        Line(img, B, R, sideR.Darkened(0.35f));
        Line(img, B, B + down, sideR.Darkened(0.45f));
    }

    (Color left, Color right) Sides(Terrain t) => t switch
    {
        Terrain.Grass => (_look.GrassSideL, _look.GrassSideR),
        Terrain.Stone => (_look.StoneSideL, _look.StoneSideR),
        Terrain.Crystal => (C("#3b2a73"), C("#281c52")),
        Terrain.Brass => (C("#8d6a2a"), C("#5c4320")),
        Terrain.Chem => (C("#1e4a3e"), C("#14302c")),
        _ => (C("#222"), C("#111")),
    };

    /// <summary>The top face's texture, in the tile's own (u, v) coordinates (0..1 along each edge).</summary>
    Color TopColor(Terrain t, int x, int y, float u, float v)
    {
        uint n = Hash(x, y, (int)t);
        switch (t)
        {
            case Terrain.Grass:
                return n % 9 == 0 ? _look.GrassLight : n % 7 == 0 ? _look.GrassDark : n % 31 == 0 ? _look.GrassSpeck : _look.Grass;
            case Terrain.Stone:
            {
                // Cobbles: staggered rows of stones with dark joints.
                float a = u * 3, b = v * 3 + (Mathf.FloorToInt(u * 3) % 2) * 0.5f;
                bool joint = a - Mathf.Floor(a) < 0.12f || b - Mathf.Floor(b) < 0.12f;
                if (joint) return _look.StoneJoint;
                return n % 11 == 0 ? _look.StoneLight : n % 5 == 0 ? _look.StoneMid : _look.Stone;
            }
            case Terrain.Brass:
            {
                bool seam = Mathf.Abs(u - 0.5f) < 0.035f || Mathf.Abs(v - 0.5f) < 0.035f;
                if (seam) return C("#6e5220");
                foreach (var (ru, rv) in new[] { (0.15f, 0.15f), (0.85f, 0.15f), (0.15f, 0.85f), (0.85f, 0.85f), (0.35f, 0.35f), (0.65f, 0.65f) })
                    if (Mathf.Abs(u - ru) < 0.04f && Mathf.Abs(v - rv) < 0.04f) return C("#f0cf72");
                return (u + v) % 0.5f < 0.08f ? C("#c79a45") : C("#b2873a");
            }
            case Terrain.Crystal:
            {
                // Raw hextech crystal: faceted bands with glints.
                float band = (u * 3 - v * 2 + 10) % 1f;
                if (n % 29 == 0) return C("#f0e8ff");
                return band < 0.18f ? C("#b9a2ff") : band < 0.45f ? C("#8466e8") : C("#6a4bd1");
            }
            case Terrain.Chem:
            {
                // Glowing liquid with drifting ripples.
                float r = (u * 2.5f + v * 1.3f + ((x * 7 + y * 3) % 5) * 0.01f) % 1f;
                if (r < 0.08f) return C("#c8ffd9");
                if (r < 0.2f) return C("#7ff0a6");
                return n % 13 == 0 ? C("#8cf5b3") : C("#43c97c");
            }
        }
        return C("#000");
    }

    delegate Color Shader(int x, int y, float u, float v);

    void Fill(Image img, Vector2[] poly, Shader shade)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in poly) { minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X); minY = Mathf.Min(minY, p.Y); maxY = Mathf.Max(maxY, p.Y); }
        var top = poly[0];
        for (int y = Mathf.FloorToInt(minY); y < Mathf.CeilToInt(maxY); y++)
        for (int x = Mathf.FloorToInt(minX); x < Mathf.CeilToInt(maxX); x++)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
            float px = x + 0.5f, py = y + 0.5f;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].Y > py) != (poly[j].Y > py) &&
                    px < (poly[j].X - poly[i].X) * (py - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            if (!inside) continue;
            // (u, v): position along the diamond's two edges from its top corner.
            float dx = px - top.X, dy = py - top.Y;
            float u = dy / TH + dx / TW, v = dy / TH - dx / TW;
            img.SetPixel(x, y, shade(x, y, u, v));
        }
    }

    void Line(Image img, Vector2 a, Vector2 b, Color c)
    {
        int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.X - a.X), Mathf.Abs(b.Y - a.Y)));
        for (int i = 0; i <= n; i++)
        {
            var p = a + (b - a) * (i / (float)Math.Max(1, n));
            int x = Mathf.Clamp(Mathf.FloorToInt(p.X), 0, Width - 1), y = Mathf.Clamp(Mathf.FloorToInt(p.Y), 0, Height - 1);
            if (img.GetPixel(x, y).A > 0) img.SetPixel(x, y, c);
        }
    }

    // ------------------------------------------------------------------ backdrop

    /// <summary>A low-res skyline: brass towers high up, neon slums below, haze between.</summary>
    public static Image PaintBackdrop(int w, int h, ulong seed, string theme = "Undercity")
    {
        var look = Look.Get(theme);
        // Some art styles fight in daylight: the sky comes from the style, the ground from the map.
        if (Creatures.ArtStyle.Current.Sky is { } sky) look = Look.Get(sky);
        var rng = new Creatures.SeededRng(seed);
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        Color top = look.SkyTop, mid = look.SkyMid, bottom = look.SkyBottom;
        for (int y = 0; y < h; y++)
        {
            float k = y / (float)h;
            var c = k < 0.6f ? top.Lerp(mid, k / 0.6f) : mid.Lerp(bottom, (k - 0.6f) / 0.4f);
            for (int x = 0; x < w; x++) img.SetPixel(x, y, c);
        }
        // Far towers (the city above), then near slums (the city below), each with lit windows.
        Skyline(img, rng, h * 0.55f, 0.13f, look.FarBody, look.FarLights, 14, 34);
        Skyline(img, rng, h * 0.78f, 0.08f, look.NearBody, look.NearLights, 10, 22);
        // Haze of chem-glow along the bottom.
        for (int y = (int)(h * 0.8f); y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float k = (y - h * 0.8f) / (h * 0.2f);
            if (Hash(x, y, 77) % 3 == 0) img.SetPixel(x, y, img.GetPixel(x, y).Lerp(look.Haze, 0.12f * k));
        }
        Creatures.ArtStyle.Current.GradeImage(img);
        return img;
    }

    static void Skyline(Image img, Creatures.SeededRng rng, float baseY, float windowChance, Color body, Color[] lights, int minW, int maxW)
    {
        int w = img.GetWidth(), h = img.GetHeight();
        int x = -rng.Int(10);
        while (x < w)
        {
            int bw = rng.Int(minW, maxW), bh = rng.Int(20, (int)(baseY * 0.7f));
            int top = (int)baseY - bh;
            bool spire = rng.Chance(0.3f);
            for (int yy = Math.Max(0, top); yy < h; yy++)
            for (int xx = Math.Max(0, x); xx < Math.Min(w, x + bw); xx++) img.SetPixel(xx, yy, body);
            if (spire)
                for (int k = 0; k < bh / 3; k++)
                {
                    int sx = x + bw / 2, sy = top - k;
                    if (sx >= 0 && sx < w && sy >= 0) img.SetPixel(sx, sy, body);
                }
            for (int yy = top + 3; yy < h - 2; yy += 4)
            for (int xx = x + 2; xx < x + bw - 2; xx += 3)
                if (xx >= 0 && xx < w && yy >= 0 && rng.Chance(windowChance * (1 - (float)(yy - top) / (h - top + 1) * 0.5f)))
                    img.SetPixel(xx, yy, lights[rng.Int(lights.Length)]);
            x += bw + rng.Int(0, 4);
        }
    }
}

/// <summary>A region's colours: its ground and its sky.</summary>
public sealed class Look
{
    public Color Grass, GrassLight, GrassDark, GrassSpeck, GrassSideL, GrassSideR;
    public Color Stone, StoneLight, StoneMid, StoneJoint, StoneSideL, StoneSideR;
    public Color SkyTop, SkyMid, SkyBottom, FarBody, NearBody, Haze;
    public Color[] FarLights, NearLights;

    static Color C(string hex) => Color.FromHtml(hex);

    static Look Make(string[] grass, string[] stone, string[] sky, string[] far, string[] near, string haze) => new()
    {
        Grass = C(grass[0]), GrassLight = C(grass[1]), GrassDark = C(grass[2]), GrassSpeck = C(grass[3]), GrassSideL = C(grass[4]), GrassSideR = C(grass[5]),
        Stone = C(stone[0]), StoneLight = C(stone[1]), StoneMid = C(stone[2]), StoneJoint = C(stone[3]), StoneSideL = C(stone[4]), StoneSideR = C(stone[5]),
        SkyTop = C(sky[0]), SkyMid = C(sky[1]), SkyBottom = C(sky[2]),
        FarBody = C(far[0]), FarLights = far[1..].Select(C).ToArray(),
        NearBody = C(near[0]), NearLights = near[1..].Select(C).ToArray(),
        Haze = C(haze),
    };

    static readonly Dictionary<string, Look> Looks = new()
    {
        // The undercity: moss on cobbles under a night of neon windows.
        ["Undercity"] = Make(
            new[] { "#3e6140", "#5c8550", "#2b4630", "#8a9f5a", "#4a3b33", "#30262a" },
            new[] { "#6a7083", "#7b8194", "#5f6577", "#363a48", "#535869", "#383b4b" },
            new[] { "#0d0b1e", "#251537", "#120c1a" },
            new[] { "#1c1a33", "#f2b950", "#f7d58a" }, new[] { "#160f22", "#ff4fb8", "#4ff0a0", "#5fd0ff" }, "#2f6a55"),
        // The city above: marble and gardens at sunset, brass towers lit gold.
        ["Piltover"] = Make(
            new[] { "#4f7a3e", "#6fa04f", "#3a5c30", "#e8c86a", "#6b5a48", "#4a3d33" },
            new[] { "#cfc7b8", "#e6dfd2", "#b9b0a1", "#8f8678", "#a39a8c", "#7c7468" },
            new[] { "#2b3f6b", "#d98a5a", "#4a3a5a" },
            new[] { "#6b5a7a", "#ffd27a", "#ffe3a8" }, new[] { "#3b2f4a", "#ffc25a", "#7fe0e0" }, "#f2b57a"),
        // The sump: black moss, slick slate, a green chem haze.
        ["Sump"] = Make(
            new[] { "#2f4a2c", "#46663a", "#22361f", "#7fbf5a", "#2f2a24", "#1f1b18" },
            new[] { "#4d5250", "#5e6461", "#444947", "#2a2d2c", "#3a3e3d", "#2a2d2c" },
            new[] { "#08130f", "#123326", "#0a1a14" },
            new[] { "#102a20", "#4ff0a0", "#ff4fb8" }, new[] { "#0b1c16", "#4ff0a0", "#9fffc8" }, "#4ff0a0"),
        // The ruins: dry grass and weathered stone in a red dusk.
        ["Ruins"] = Make(
            new[] { "#5a6b3a", "#7a8c4a", "#43512d", "#b8a25a", "#5a4636", "#3d2f26" },
            new[] { "#8a7f72", "#a09484", "#7a7064", "#4f483f", "#6e6458", "#544c43" },
            new[] { "#1d1830", "#6b3b4a", "#2a1a26" },
            new[] { "#3a2a3e", "#f2b950" }, new[] { "#24182a", "#ffb04f", "#ff7a4f" }, "#c46a4a"),
        // Daylight skies for the daytime styles (only the sky colours are used).
        ["Day"] = Make(
            new[] { "#3e6140", "#5c8550", "#2b4630", "#8a9f5a", "#4a3b33", "#30262a" },
            new[] { "#6a7083", "#7b8194", "#5f6577", "#363a48", "#535869", "#383b4b" },
            new[] { "#5d93d6", "#a9d0ec", "#e8e4cf" },
            new[] { "#8fa4c4", "#dfe7f2" }, new[] { "#6f7f9c", "#cfd8e6" }, "#f0e6c8"),
        ["DayWarm"] = Make(
            new[] { "#3e6140", "#5c8550", "#2b4630", "#8a9f5a", "#4a3b33", "#30262a" },
            new[] { "#6a7083", "#7b8194", "#5f6577", "#363a48", "#535869", "#383b4b" },
            new[] { "#7f9cb8", "#d9c9a8", "#b89a76" },
            new[] { "#8c8a96", "#efe2c4" }, new[] { "#6f6a70", "#e8d2a8" }, "#e6c89a"),
        // The hollow: fungal moss and dark rock, lit by crystal.
        ["Cavern"] = Make(
            new[] { "#3a2f5a", "#57458a", "#2a2242", "#7fd8ff", "#2b2236", "#1d1726" },
            new[] { "#3f3d4f", "#514e64", "#383647", "#232130", "#302e3d", "#22202c" },
            new[] { "#07060d", "#120d22", "#06050b" },
            new[] { "#151022", "#b9a2ff", "#7fd8ff" }, new[] { "#0e0a18", "#b9a2ff", "#e6dcff" }, "#6a4bd1"),
    };

    public static Look Get(string name) => Looks.TryGetValue(name ?? "", out var l) ? l : Looks["Undercity"];
}
