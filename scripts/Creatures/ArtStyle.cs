using System;
using Godot;

namespace CreatureTactics.Creatures;

public enum OutlineMode { Black, Tinted, Selective }

/// <summary>
/// An art direction, as renderer settings: how big and how finely shaded sprites are, how they're
/// outlined and lit, and a colour grade over everything (creatures, board and sky). The game draws
/// everything procedurally, so a style is just a different way of running the same painters, and
/// options can be compared side by side before one is picked.
/// </summary>
public sealed class ArtStyle
{
    public string Id, Name, Blurb;
    public float SizeScale = 1f;            // sprite resolution: 0.8 is chunkier, 1.3 finer
    public int Tones = 4;                   // shading bands per material: 3, 4 or 5
    public OutlineMode Outline = OutlineMode.Selective;
    public float RimScale = 1f;             // multiplies the neon rim of non-natural strains
    public float NaturalRim;                // a cool backlight on natural creatures too
    public Color NaturalRimColor = Color.FromHtml("#7fd8ff");
    public bool GlowHalo;                   // glowing parts bleed light past the outline
    public bool Dither;                     // checkerboard where shading bands meet
    public float Grain;                     // per-pixel texture, for a painted feel
    public float Saturation = 1f, Value = 1f, Contrast = 1f;
    public Color Tint = Colors.White;
    public float TintAmount;
    public string Sky;                      // a sky look to use instead of the map's (daylight styles)

    /// <summary>The style everything draws with. Changing it and repainting switches the look.</summary>
    public static ArtStyle Current = All()[0];

    public static ArtStyle[] All() => new[]
    {
        new ArtStyle
        {
            Id = "neon", Name = "Neon Undercity",
            Blurb = "What we have now: GBA-era pixel sprites, four tones, selective outlines, neon rims on strained creatures.",
        },
        new ArtStyle
        {
            Id = "ffta", Name = "Classic FFTA",
            Blurb = "Smaller, chunkier sprites with flat three-tone shading and bold black outlines; bright, warm, storybook colour.",
            SizeScale = 0.8f, Tones = 3, Outline = OutlineMode.Black, RimScale = 0f,
            Saturation = 1.2f, Value = 1.1f, Tint = Color.FromHtml("#ffe2b0"), TintAmount = 0.08f, Sky = "Day",
        },
        new ArtStyle
        {
            Id = "painted", Name = "Painted (Fell Seal)",
            Blurb = "Larger sprites, five tones with dithered blends and a painted grain; tinted outlines; muted, earthy colour.",
            SizeScale = 1.3f, Tones = 5, Outline = OutlineMode.Tinted, RimScale = 0.6f, NaturalRim = 0.12f,
            NaturalRimColor = Color.FromHtml("#ffd9a0"), Dither = true, Grain = 0.05f,
            Saturation = 0.85f, Value = 0.98f, Tint = Color.FromHtml("#c9a77a"), TintAmount = 0.06f, Sky = "DayWarm",
        },
        new ArtStyle
        {
            Id = "noir", Name = "Neon Noir (Arcane)",
            Blurb = "Everything backlit in cyan and magenta, glowing parts haloed, a deep purple grade with hard contrast.",
            RimScale = 1.6f, NaturalRim = 0.35f, NaturalRimColor = Color.FromHtml("#4fe3ff"), GlowHalo = true,
            Saturation = 1.1f, Value = 0.86f, Contrast = 1.15f, Tint = Color.FromHtml("#7a5cff"), TintAmount = 0.1f,
        },
        new ArtStyle
        {
            Id = "grim", Name = "Grim Dark",
            Blurb = "Drained colour, heavy shadow, black outlines, a blood-red grade and grit: the dark and scary end.",
            Outline = OutlineMode.Black, RimScale = 0.5f, Dither = true, Grain = 0.06f,
            Saturation = 0.55f, Value = 0.82f, Contrast = 1.25f, Tint = Color.FromHtml("#8a2a2a"), TintAmount = 0.07f,
        },
    };

    public static ArtStyle ById(string id)
    {
        foreach (var s in All()) if (s.Id == id) return s;
        return All()[0];
    }

    public bool Neutral => Saturation == 1f && Value == 1f && Contrast == 1f && TintAmount == 0f;

    /// <summary>The colour grade: saturation, value, contrast, then a tint.</summary>
    public Color Grade(Color c)
    {
        if (Neutral || c.A == 0) return c;
        c.ToHsv(out float h, out float s, out float v);
        var g = Color.FromHsv(h, Mathf.Clamp(s * Saturation, 0, 1), Mathf.Clamp(v * Value, 0, 1), c.A);
        if (Contrast != 1f)
            g = new Color(Mathf.Clamp((g.R - 0.5f) * Contrast + 0.5f, 0, 1), Mathf.Clamp((g.G - 0.5f) * Contrast + 0.5f, 0, 1),
                Mathf.Clamp((g.B - 0.5f) * Contrast + 0.5f, 0, 1), g.A);
        if (TintAmount > 0) g = g.Lerp(new Color(Tint, g.A) * new Color(g.V, g.V, g.V, 1) * 1.6f, TintAmount);
        return g;
    }

    /// <summary>Grades a whole image in place (boards, skies, sprites).</summary>
    public void GradeImage(Image img)
    {
        if (Neutral) return;
        for (int y = 0; y < img.GetHeight(); y++)
        for (int x = 0; x < img.GetWidth(); x++)
            img.SetPixel(x, y, Grade(img.GetPixel(x, y)));
    }

    /// <summary>A stable per-pixel noise value in [-1, 1] for grain and dithering offsets.</summary>
    public static float Noise(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5f - 1f;
    }
}
