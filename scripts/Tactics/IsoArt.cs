using System;
using System.Collections.Generic;
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
    public readonly int Width, Height;
    readonly int _ox, _oy;

    public IsoArt(BattleMap map)
    {
        _map = map;
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

    static (Color left, Color right) Sides(Terrain t) => t switch
    {
        Terrain.Grass => (C("#4a3b33"), C("#30262a")),
        Terrain.Stone => (C("#535869"), C("#383b4b")),
        Terrain.Brass => (C("#8d6a2a"), C("#5c4320")),
        Terrain.Chem => (C("#1e4a3e"), C("#14302c")),
        _ => (C("#222"), C("#111")),
    };

    /// <summary>The top face's texture, in the tile's own (u, v) coordinates (0..1 along each edge).</summary>
    static Color TopColor(Terrain t, int x, int y, float u, float v)
    {
        uint n = Hash(x, y, (int)t);
        switch (t)
        {
            case Terrain.Grass:
                return n % 9 == 0 ? C("#5c8550") : n % 7 == 0 ? C("#2b4630") : n % 31 == 0 ? C("#8a9f5a") : C("#3e6140");
            case Terrain.Stone:
            {
                // Cobbles: staggered rows of stones with dark joints.
                float a = u * 3, b = v * 3 + (Mathf.FloorToInt(u * 3) % 2) * 0.5f;
                bool joint = a - Mathf.Floor(a) < 0.12f || b - Mathf.Floor(b) < 0.12f;
                if (joint) return C("#363a48");
                return n % 11 == 0 ? C("#7b8194") : n % 5 == 0 ? C("#5f6577") : C("#6a7083");
            }
            case Terrain.Brass:
            {
                bool seam = Mathf.Abs(u - 0.5f) < 0.035f || Mathf.Abs(v - 0.5f) < 0.035f;
                if (seam) return C("#6e5220");
                foreach (var (ru, rv) in new[] { (0.15f, 0.15f), (0.85f, 0.15f), (0.15f, 0.85f), (0.85f, 0.85f), (0.35f, 0.35f), (0.65f, 0.65f) })
                    if (Mathf.Abs(u - ru) < 0.04f && Mathf.Abs(v - rv) < 0.04f) return C("#f0cf72");
                return (u + v) % 0.5f < 0.08f ? C("#c79a45") : C("#b2873a");
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
    public static Image PaintBackdrop(int w, int h, ulong seed)
    {
        var rng = new Creatures.SeededRng(seed);
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        Color top = C("#0d0b1e"), mid = C("#251537"), bottom = C("#120c1a");
        for (int y = 0; y < h; y++)
        {
            float k = y / (float)h;
            var c = k < 0.6f ? top.Lerp(mid, k / 0.6f) : mid.Lerp(bottom, (k - 0.6f) / 0.4f);
            for (int x = 0; x < w; x++) img.SetPixel(x, y, c);
        }
        // Far towers (the city above), then near slums (the city below), each with lit windows.
        Skyline(img, rng, h * 0.55f, 0.13f, C("#1c1a33"), new[] { C("#f2b950"), C("#f7d58a") }, 14, 34);
        Skyline(img, rng, h * 0.78f, 0.08f, C("#160f22"), new[] { C("#ff4fb8"), C("#4ff0a0"), C("#5fd0ff") }, 10, 22);
        // Haze of chem-glow along the bottom.
        for (int y = (int)(h * 0.8f); y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float k = (y - h * 0.8f) / (h * 0.2f);
            if (Hash(x, y, 77) % 3 == 0) img.SetPixel(x, y, img.GetPixel(x, y).Lerp(C("#2f6a55"), 0.12f * k));
        }
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
