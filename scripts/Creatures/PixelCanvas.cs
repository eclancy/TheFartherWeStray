using System;
using System.Collections.Generic;
using Godot;

namespace CreatureTactics.Creatures;

/// <summary>What a pixel is made of. Colours are only picked at the end, from the palette.</summary>
public enum Mat : byte { None, Main, Second, Accent, Membrane, Metal, EyeWhite, Pupil, Glow, Mouth, Line }

/// <summary>
/// A small pixel grid that creature parts are painted onto as shaded shapes, then resolved into a
/// sprite: each shape is lit like a ball or a tube from the upper left and quantised to four
/// tones, then every part gets an outline. That is most of what makes a Gen 2-3 sprite read.
///
/// Parts are numbered in paint order. Where a later (nearer) part meets an earlier one, the earlier
/// part's edge pixel becomes a line, so overlapping parts stay readable. Shapes painted under the
/// same part number merge with no line (a snout on a head, the segments of a serpent).
/// </summary>
public sealed class PixelCanvas
{
    public readonly int W, H;
    readonly Mat[] _mat;
    readonly float[] _shade;
    readonly float[] _nx, _ny; // surface normal, so patterns can find the belly or the back
    readonly int[] _part;
    int _nextPart = 1;

    // Light from the upper left, a little in front.
    static readonly Vector3 Light = new Vector3(-0.45f, -0.7f, 0.55f).Normalized();
    // Shade thresholds for the four tones: deep shadow, shadow, base, highlight.
    const float HighlightAbove = 0.86f, BaseAbove = 0.3f, ShadowAbove = 0.02f;

    static int ToneOf(float shade) =>
        shade >= HighlightAbove ? 3 : shade >= BaseAbove ? 2 : shade >= ShadowAbove ? 1 : 0;

    public PixelCanvas(int w, int h)
    {
        W = w; H = h;
        _mat = new Mat[w * h];
        _shade = new float[w * h];
        _nx = new float[w * h];
        _ny = new float[w * h];
        _part = new int[w * h];
    }

    public int NewPart() => _nextPart++;

    public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
    public Mat MatAt(int x, int y) => Inside(x, y) ? _mat[y * W + x] : Mat.None;
    public int PartAt(int x, int y) => Inside(x, y) ? _part[y * W + x] : 0;
    public Vector2 NormalAt(int x, int y) => Inside(x, y) ? new Vector2(_nx[y * W + x], _ny[y * W + x]) : Vector2.Zero;

    static float Lambert(float nx, float ny)
    {
        float d2 = nx * nx + ny * ny;
        float nz = d2 < 1 ? Mathf.Sqrt(1 - d2) : 0;
        return nx * Light.X + ny * Light.Y + nz * Light.Z;
    }

    void Set(int x, int y, Mat m, float shade, int part, float nx, float ny)
    {
        int i = y * W + x;
        _mat[i] = m; _shade[i] = shade; _part[i] = part; _nx[i] = nx; _ny[i] = ny;
    }

    /// <summary>A lit ellipse. <paramref name="bias"/> darkens (far-side limbs) or lightens it.</summary>
    public void Ellipse(float cx, float cy, float rx, float ry, Mat m, int part,
        float bias = 0, float rot = 0, bool flat = false, float clipBelow = float.MaxValue)
    {
        rx = Mathf.Max(rx, 0.5f); ry = Mathf.Max(ry, 0.5f);
        float ext = Mathf.Max(rx, ry) + 1;
        float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
        bool any = false;
        for (int y = Mathf.FloorToInt(cy - ext); y <= Mathf.CeilToInt(cy + ext); y++)
        for (int x = Mathf.FloorToInt(cx - ext); x <= Mathf.CeilToInt(cx + ext); x++)
        {
            if (!Inside(x, y) || y + 0.5f > clipBelow) continue;
            float px = x + 0.5f - cx, py = y + 0.5f - cy;
            float lx = (px * cs + py * sn) / rx, ly = (-px * sn + py * cs) / ry;
            if (lx * lx + ly * ly > 1) continue;
            float wx = lx * cs - ly * sn, wy = lx * sn + ly * cs;
            Set(x, y, m, flat ? 0.55f + bias : Lambert(wx, wy) + bias, part, wx, wy);
            any = true;
        }
        int ix = Mathf.FloorToInt(cx), iy = Mathf.FloorToInt(cy);
        if (!any && Inside(ix, iy)) Set(ix, iy, m, 0.55f + bias, part, 0, 0);
    }

    /// <summary>A lit tube from a to b whose radius tapers from ra to rb (limbs, necks, horns, tails).</summary>
    public void Capsule(Vector2 a, Vector2 b, float ra, float rb, Mat m, int part, float bias = 0)
    {
        ra = Mathf.Max(ra, 0.5f); rb = Mathf.Max(rb, 0.5f);
        float ext = Mathf.Max(ra, rb) + 1;
        int x0 = Mathf.FloorToInt(Mathf.Min(a.X, b.X) - ext), x1 = Mathf.CeilToInt(Mathf.Max(a.X, b.X) + ext);
        int y0 = Mathf.FloorToInt(Mathf.Min(a.Y, b.Y) - ext), y1 = Mathf.CeilToInt(Mathf.Max(a.Y, b.Y) + ext);
        Vector2 ab = b - a;
        float len2 = Mathf.Max(ab.LengthSquared(), 1e-4f);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            if (!Inside(x, y)) continue;
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float t = Mathf.Clamp((p - a).Dot(ab) / len2, 0, 1);
            Vector2 d = p - (a + ab * t);
            float r = Mathf.Lerp(ra, rb, t);
            if (d.LengthSquared() > r * r) continue;
            Vector2 n = d / r;
            Set(x, y, m, Lambert(n.X, n.Y) + bias, part, n.X, n.Y);
        }
    }

    /// <summary>A tube through several points (curved horns, tails), tapering from ra to rb.</summary>
    public void Chain(IReadOnlyList<Vector2> pts, float ra, float rb, Mat m, int part, float bias = 0) =>
        Tube(pts, t => Mathf.Lerp(ra, rb, t), m, part, bias);

    /// <summary>
    /// One continuous tube along a polyline, lit from the nearest point of the whole line, so the
    /// shading flows round bends instead of breaking at every joint (serpent bodies).
    /// </summary>
    public void Tube(IReadOnlyList<Vector2> pts, Func<float, float> radius, Mat m, int part, float bias = 0)
    {
        int n = pts.Count - 1;
        float maxR = 0, minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i <= n; i++)
        {
            maxR = Mathf.Max(maxR, Mathf.Max(radius(i / (float)n), 0.5f));
            minX = Mathf.Min(minX, pts[i].X); maxX = Mathf.Max(maxX, pts[i].X);
            minY = Mathf.Min(minY, pts[i].Y); maxY = Mathf.Max(maxY, pts[i].Y);
        }
        for (int y = Mathf.FloorToInt(minY - maxR - 1); y <= Mathf.CeilToInt(maxY + maxR + 1); y++)
        for (int x = Mathf.FloorToInt(minX - maxR - 1); x <= Mathf.CeilToInt(maxX + maxR + 1); x++)
        {
            if (!Inside(x, y)) continue;
            var p = new Vector2(x + 0.5f, y + 0.5f);
            // The segment whose surface this pixel is deepest inside wins.
            float best = float.MaxValue;
            Vector2 bestN = Vector2.Zero;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], ab = pts[i + 1] - a;
                float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 1e-4f), 0, 1);
                Vector2 d = p - (a + ab * t);
                float r = Mathf.Max(radius((i + t) / n), 0.5f);
                float depth = d.Length() / r;
                if (depth < best) { best = depth; bestN = d / r; }
            }
            if (best > 1) continue;
            Set(x, y, m, Lambert(bestN.X, bestN.Y) + bias, part, bestN.X, bestN.Y);
        }
    }

    /// <summary>A filled polygon, shaded top-light to bottom-dark (wing membranes, fins, sails).</summary>
    public void Polygon(IReadOnlyList<Vector2> pts, Mat m, int part, float bias = 0)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts)
        {
            minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X);
            minY = Mathf.Min(minY, p.Y); maxY = Mathf.Max(maxY, p.Y);
        }
        float h = Mathf.Max(maxY - minY, 1);
        for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
        for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
        {
            if (!Inside(x, y)) continue;
            float px = x + 0.5f, py = y + 0.5f;
            bool inside = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                if ((pts[i].Y > py) != (pts[j].Y > py) &&
                    px < (pts[j].X - pts[i].X) * (py - pts[i].Y) / (pts[j].Y - pts[i].Y) + pts[i].X)
                    inside = !inside;
            }
            if (!inside) continue;
            float k = (py - minY) / h;
            Set(x, y, m, 0.8f - 0.55f * k + bias, part, 0, k * 2 - 1);
        }
    }

    /// <summary>One pixel of detail (eyes, mouths, claws). Tone: 0 shadow, 1 base, 2 highlight.</summary>
    public void Plot(int x, int y, Mat m, int part, int tone = 1)
    {
        if (!Inside(x, y)) return;
        float shade = tone switch { 0 => 0.15f, 2 => 1, _ => 0.55f };
        int i = y * W + x;
        Set(x, y, m, shade, part, _nx[i], _ny[i]);
    }

    /// <summary>Changes the material of pixels that pass a test, keeping their shading (patterns).</summary>
    public void Recolor(Func<int, int, bool> where, Mat to, float bias = 0)
    {
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int i = y * W + x;
            if (_mat[i] == Mat.None || !where(x, y)) continue;
            _mat[i] = to;
            _shade[i] += bias;
        }
    }

    static bool IsDetail(Mat m) => m is Mat.EyeWhite or Mat.Pupil or Mat.Glow or Mat.Mouth or Mat.Line;

    /// <summary>
    /// Turns the painted grid into a sprite: tones, seams between parts, and the outer outline.
    /// <paramref name="fourColor"/> limits the whole sprite to four colours, the Game Boy Color rule.
    /// </summary>
    public Image Resolve(CreaturePalette pal, bool fourColor)
    {
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        var accentTones = AccentTonesFor(pal);
        var style = ArtStyle.Current;
        var black = new Color(0.07f, 0.06f, 0.09f);
        float rim = pal.RimStrength * style.RimScale;
        Color rimColor = pal.Glow;
        if (rim <= 0 && style.NaturalRim > 0) { rim = style.NaturalRim; rimColor = style.NaturalRimColor; }

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int i = y * W + x;
            Mat m = _mat[i];
            if (m == Mat.None)
            {
                // Outer outline: an empty pixel touching the creature.
                Mat edge = Neighbour4(x, y, j => _mat[j] != Mat.None);
                if (edge == Mat.None) continue;
                Color line = fourColor ? pal.GbBlack : style.Outline == OutlineMode.Black ? black : LineOf(pal, edge);
                if (!fourColor && style.Outline == OutlineMode.Selective && LitEdge(x, y, out int lit) && !IsDetail(_mat[lit]))
                {
                    // Selective outline: where the light hits the edge, the line softens toward the
                    // colour it borders (the GBA-era trick that stops sprites looking cut out).
                    line = line.Lerp(RampOf(pal, _mat[lit]).Dark, 0.5f);
                }
                // Glow halo: glowing parts bleed light past the outline.
                if (!fourColor && style.GlowHalo && Neighbour4(x, y, j => _mat[j] == Mat.Glow) == Mat.Glow) line = pal.Glow.Lerp(line, 0.35f);
                img.SetPixel(x, y, fourColor ? line : style.Grade(line));
                continue;
            }

            // Dithering nudges the shade either way in a checkerboard, so bands interleave where they meet.
            float shade = _shade[i] + (style.Dither ? ((x + y) % 2 == 0 ? 0.05f : -0.05f) : 0);
            int tone = ToneOf(shade);
            if (style.Tones == 3 && tone == 0) tone = 1;
            Color col = fourColor ? FourColor(pal, m, tone, accentTones) : FullColor(pal, m, tone);
            bool ramp = m is Mat.Main or Mat.Second or Mat.Accent or Mat.Membrane or Mat.Metal;
            if (!fourColor && ramp && style.Tones == 5 && tone == 2 && shade >= 0.62f) col = RampOf(pal, m).Base.Lerp(RampOf(pal, m).Light, 0.5f);
            if (!fourColor && ramp && style.Grain > 0) col = col.Lightened(ArtStyle.Noise(x, y) * style.Grain).Darkened(Mathf.Max(0, -ArtStyle.Noise(x, y)) * style.Grain);

            // Seam: this pixel borders a part painted after (in front of) it.
            bool seam = false;
            if (!IsDetail(m))
            {
                int part = _part[i];
                Mat front = Neighbour4(x, y, j => _mat[j] != Mat.None && _part[j] > part && !IsDetail(_mat[j]));
                if (front != Mat.None) { col = fourColor ? pal.GbBlack : style.Outline == OutlineMode.Black ? black : LineOf(pal, front); seam = true; }
            }
            if (!fourColor && !seam && !IsDetail(m))
            {
                // Glow spill: skin next to something glowing picks up its light.
                if (Neighbour4(x, y, j => _mat[j] == Mat.Glow) == Mat.Glow) col = col.Lerp(pal.Glow, 0.4f);
                // Rim light: a neon backlight along the shadow-side silhouette, the Arcane look.
                else if (rim > 0 && tone <= 2 && OnShadowEdge(x, y)) col = col.Lerp(rimColor, Mathf.Min(0.85f, rim));
            }
            img.SetPixel(x, y, fourColor ? col : style.Grade(col));
        }
        return img;
    }

    /// <summary>A filled pixel on the lower-right edge of the silhouette, away from the light (not the feet).</summary>
    bool OnShadowEdge(int x, int y)
    {
        bool Empty(int px, int py) => !Inside(px, py) || _mat[py * W + px] == Mat.None;
        return Empty(x + 1, y) || (y < H - 3 && Empty(x, y + 1) && Empty(x + 1, y + 1));
    }

    /// <summary>
    /// Whether this empty pixel sits on the creature's lit (upper-left) side: the pixel to its
    /// right or below is filled and in base tone or brighter.
    /// </summary>
    bool LitEdge(int x, int y, out int lit)
    {
        lit = -1;
        foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
        {
            int nx = x + dx, ny = y + dy;
            if (!Inside(nx, ny)) continue;
            int j = ny * W + nx;
            if (_mat[j] == Mat.None || ToneOf(_shade[j]) < 2) continue;
            // Only where nothing borders it from the shadow side, so the outline's far side stays dark.
            if (Inside(x - 1, y) && _mat[y * W + x - 1] != Mat.None) continue;
            if (Inside(x, y - 1) && _mat[(y - 1) * W + x] != Mat.None) continue;
            lit = j;
            return true;
        }
        return false;
    }

    Mat Neighbour4(int x, int y, Func<int, bool> test)
    {
        ReadOnlySpan<(int dx, int dy)> dirs = stackalloc (int, int)[] { (-1, 0), (1, 0), (0, -1), (0, 1) };
        Mat found = Mat.None;
        foreach (var (dx, dy) in dirs)
        {
            int nx = x + dx, ny = y + dy;
            if (!Inside(nx, ny)) continue;
            int j = ny * W + nx;
            if (!test(j)) continue;
            if (_mat[j] == Mat.Main) return Mat.Main; // the body's line wins, so outlines stay one colour
            found = _mat[j];
        }
        return found;
    }

    static Ramp RampOf(CreaturePalette pal, Mat m) => m switch
    {
        Mat.Second => pal.Second,
        Mat.Accent => pal.Accent,
        Mat.Membrane => pal.Membrane,
        Mat.Metal => pal.Metal,
        _ => pal.Main,
    };

    static Color LineOf(CreaturePalette pal, Mat m) =>
        m is Mat.Pupil or Mat.Glow or Mat.Mouth or Mat.Line or Mat.EyeWhite ? pal.Main.Line : RampOf(pal, m).Line;

    // Tones: 0 deep shadow, 1 shadow, 2 base, 3 highlight.
    static Color FullColor(CreaturePalette pal, Mat m, int tone)
    {
        switch (m)
        {
            case Mat.EyeWhite: return tone <= 1 ? pal.EyeShade : pal.EyeWhite;
            case Mat.Pupil: return tone == 3 ? pal.EyeWhite : pal.Pupil;
            case Mat.Mouth: return pal.Mouth;
            case Mat.Glow: return tone == 3 ? pal.GlowHot : pal.Glow;
            case Mat.Line: return pal.Main.Line;
        }
        return RampOf(pal, m)[tone];
    }

    // Four-colour mode maps every material onto white / light / dark / black.
    enum Gb { White, Light, Dark, Black }

    static Gb[] AccentTonesFor(CreaturePalette pal)
    {
        float lum = pal.Accent.Base.Luminance;
        return lum > 0.72f ? new[] { Gb.Light, Gb.Light, Gb.White, Gb.White }
             : lum < 0.35f ? new[] { Gb.Black, Gb.Black, Gb.Dark, Gb.Light }
             : new[] { Gb.Dark, Gb.Dark, Gb.Light, Gb.White };
    }

    static Color FourColor(CreaturePalette pal, Mat m, int tone, Gb[] accentTones)
    {
        Gb g = m switch
        {
            Mat.Main => tone <= 1 ? Gb.Dark : tone == 3 ? Gb.White : Gb.Light,
            Mat.Second or Mat.Membrane => tone <= 1 ? Gb.Light : Gb.White,
            Mat.Accent or Mat.Metal => accentTones[tone],
            Mat.EyeWhite => Gb.White,
            Mat.Pupil => tone == 3 ? Gb.White : Gb.Black,
            Mat.Glow => Gb.White,
            Mat.Mouth => Gb.Dark,
            _ => Gb.Black,
        };
        return g switch { Gb.White => pal.GbWhite, Gb.Light => pal.GbLight, Gb.Dark => pal.GbDark, _ => pal.GbBlack };
    }
}
