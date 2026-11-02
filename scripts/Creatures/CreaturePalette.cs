using Godot;

namespace CreatureTactics.Creatures;

/// <summary>A colour in HSV, kept as numbers so individuals can jitter and shinies can rotate it.</summary>
public record struct Hsv(float H, float S, float V)
{
    public Hsv Shift(float dh, float ds = 0, float dv = 0) =>
        new(Mathf.PosMod(H + dh, 1f), Mathf.Clamp(S + ds, 0, 1), Mathf.Clamp(V + dv, 0, 1));

    public Color ToColor() => Color.FromHsv(H, S, V);
}

/// <summary>
/// The tones of one material, darkest first: deep shadow, shadow, base, highlight, and the line
/// colour drawn around it. Indexed 0-3 by tone.
/// </summary>
public readonly struct Ramp
{
    public readonly Color Deep, Dark, Base, Light, Line;

    public Ramp(Color deep, Color dark, Color @base, Color light, Color line)
    {
        Deep = deep; Dark = dark; Base = @base; Light = light; Line = line;
    }

    public Color this[int tone] => tone switch { 0 => Deep, 1 => Dark, 3 => Light, _ => Base };

    /// <summary>
    /// Hue-shifted ramp, the pixel-art habit that keeps shading from looking muddy: highlights lean
    /// toward yellow, shadows and lines toward blue-purple.
    /// </summary>
    public static Ramp From(Hsv c)
    {
        Hsv light = new(TowardHue(c.H, 1f / 6f, 0.04f), c.S * 0.7f, Mathf.Min(1, c.V + 0.16f));
        Hsv dark = new(TowardHue(c.H, 2f / 3f, 0.04f), Mathf.Min(1, c.S * 1.06f + 0.05f), c.V * 0.76f);
        Hsv deep = new(TowardHue(c.H, 2f / 3f, 0.07f), Mathf.Min(1, c.S * 1.1f + 0.1f), c.V * 0.56f);
        Hsv line = new(TowardHue(c.H, 2f / 3f, 0.09f), Mathf.Min(1, c.S * 0.9f + 0.2f), c.V * 0.28f);
        return new Ramp(deep.ToColor(), dark.ToColor(), c.ToColor(), light.ToColor(), line.ToColor());
    }

    static float TowardHue(float h, float target, float amount)
    {
        float d = Mathf.PosMod(target - h + 0.5f, 1f) - 0.5f;
        return Mathf.PosMod(h + Mathf.Clamp(d, -amount, amount), 1f);
    }
}

/// <summary>The handful of colours one creature is drawn with.</summary>
public sealed class CreaturePalette
{
    public Ramp Main, Second, Accent, Membrane;
    public Color EyeWhite = new(0.98f, 0.98f, 0.96f);
    public Color EyeShade = new(0.78f, 0.8f, 0.86f);
    public Color Pupil = new(0.1f, 0.09f, 0.16f);
    public Color Mouth = new(0.6f, 0.16f, 0.22f);
    public Color Blush = new(0.97f, 0.5f, 0.52f);

    // The four colours of "4-colour mode", the Game Boy Color rule: white, light, dark, black.
    public Color GbWhite = new(0.97f, 0.97f, 0.93f);
    public Color GbLight, GbDark;
    public Color GbBlack = new(0.1f, 0.09f, 0.13f);

    public CreaturePalette(Hsv main, Hsv second, Hsv accent, Hsv membrane)
    {
        Main = Ramp.From(main);
        Second = Ramp.From(second);
        Accent = Ramp.From(accent);
        Membrane = Ramp.From(membrane);
        GbLight = main.Shift(0, -0.05f, 0.08f).ToColor();
        GbDark = new Hsv(main.H, Mathf.Min(1, main.S + 0.1f), main.V * 0.5f).ToColor();
    }
}

/// <summary>The HSV choices behind a palette, rolled per species.</summary>
public record struct PaletteSpec(Hsv Main, Hsv Second, Hsv Accent, Hsv Membrane)
{
    /// <summary>A palette from four hex colours (hand-authored species).</summary>
    public static PaletteSpec FromHex(string main, string second, string accent, string membrane = null)
    {
        static Hsv H(string hex) { var c = Color.FromHtml(hex); return new Hsv(c.H, c.S, c.V); }
        var s = H(second);
        return new PaletteSpec(H(main), s, H(accent), membrane != null ? H(membrane) : new Hsv(s.H, s.S * 0.6f, Mathf.Min(1, s.V + 0.05f)));
    }

    public static PaletteSpec Roll(Element element, SeededRng rng)
    {
        Hsv main = RollMain(element, rng);

        Hsv second = rng.Weighted(
            (new Hsv(Mathf.Lerp(main.H, 0.12f, 0.6f), rng.Range(0.1f, 0.22f), rng.Range(0.92f, 1f)), 4f), // cream
            (new Hsv(main.H, main.S * 0.45f, Mathf.Min(1, main.V + 0.15f)), 3f),                           // paler self
            (main.Shift(rng.Range(0.42f, 0.58f), 0, -0.05f), 1.5f),                                       // complement
            (main.Shift(rng.Chance(0.5f) ? 0.09f : -0.09f, 0.05f, 0), 1.5f));                               // neighbour

        Hsv accent = rng.Weighted(
            (new Hsv(0.12f, 0.14f, 0.96f), 4f),                       // ivory horn and claw
            (new Hsv(0.7f, 0.12f, 0.32f), 2f),                        // charcoal
            (new Hsv(0.12f, 0.7f, 0.98f), 1.5f),                      // gold
            (new Hsv(0.07f, 0.35f, 0.75f), 1.5f),                     // bone tan
            (main.Shift(0.5f, 0.2f, 0.1f), 1.5f));                     // contrasting

        Hsv membrane = new(second.H, second.S * 0.6f, Mathf.Min(1, second.V + 0.05f));
        return new PaletteSpec(main, second, accent, membrane);
    }

    static Hsv RollMain(Element e, SeededRng rng)
    {
        (float h0, float h1, float s0, float s1, float v0, float v1) = e switch
        {
            Element.Ember => (-0.02f, 0.08f, 0.7f, 0.9f, 0.85f, 1f),
            Element.Tide => (0.53f, 0.62f, 0.55f, 0.8f, 0.8f, 0.95f),
            Element.Leaf => (0.22f, 0.38f, 0.5f, 0.75f, 0.7f, 0.9f),
            Element.Spark => (0.12f, 0.16f, 0.7f, 0.9f, 0.95f, 1f),
            Element.Stone => (0.05f, 0.11f, 0.2f, 0.4f, 0.55f, 0.75f),
            Element.Venom => (0.74f, 0.84f, 0.4f, 0.65f, 0.6f, 0.82f),
            Element.Frost => (0.5f, 0.56f, 0.2f, 0.4f, 0.9f, 1f),
            Element.Shade => (0.68f, 0.76f, 0.25f, 0.45f, 0.45f, 0.6f),
            _ => rng.Chance(0.5f) ? (0.05f, 0.12f, 0.2f, 0.4f, 0.85f, 1f) : (0.92f, 0.98f, 0.22f, 0.4f, 0.9f, 1f),
        };
        return new Hsv(Mathf.PosMod(rng.Range(h0, h1), 1f), rng.Range(s0, s1), rng.Range(v0, v1));
    }

    /// <summary>Small per-individual drift in shade, so siblings aren't identical.</summary>
    public PaletteSpec Jitter(SeededRng rng)
    {
        float dh = rng.Range(-0.015f, 0.015f), dv = rng.Range(-0.05f, 0.05f);
        return new PaletteSpec(Main.Shift(dh, 0, dv), Second.Shift(dh, 0, dv * 0.5f), Accent, Membrane.Shift(dh));
    }

    /// <summary>The rare "shiny" palette: the main hue rotates, the rest follows.</summary>
    public PaletteSpec Shiny(SeededRng rng)
    {
        float dh = rng.Range(0.3f, 0.55f);
        return new PaletteSpec(Main.Shift(dh, 0.05f, 0.05f), Second.Shift(dh), Accent.Shift(dh * 0.5f), Membrane.Shift(dh));
    }

    public CreaturePalette Build() => new(Main, Second, Accent, Membrane);
}
