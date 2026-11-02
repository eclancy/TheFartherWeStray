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
        Hsv light = new(TowardHue(c.H, 1f / 6f, 0.04f), c.S * 0.75f, Mathf.Min(1, c.V + 0.15f));
        Hsv dark = new(TowardHue(c.H, 2f / 3f, 0.04f), Mathf.Min(1, c.S * 1.06f + 0.05f), c.V * 0.72f);
        Hsv deep = new(TowardHue(c.H, 2f / 3f, 0.07f), Mathf.Min(1, c.S * 1.1f + 0.1f), c.V * 0.48f);
        // FFTA and Fell Seal outline their monsters in a near-black tinted with the body's hue.
        Hsv line = new(TowardHue(c.H, 2f / 3f, 0.1f), Mathf.Min(1, c.S * 0.7f + 0.25f), Mathf.Min(0.2f, c.V * 0.22f));
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
    public Ramp Main, Second, Accent, Membrane, Metal;
    /// <summary>The neon: glowing eyes, cores and the rim light (the Arcane look).</summary>
    public Color Glow, GlowHot;
    /// <summary>How strongly the glow rims the silhouette: 0 for natural creatures.</summary>
    public float RimStrength;
    public Color EyeWhite = new(0.95f, 0.93f, 0.86f); // fangs
    public Color EyeShade = new(0.72f, 0.68f, 0.62f);
    public Color Pupil = new(0.07f, 0.05f, 0.09f);
    public Color Mouth = new(0.42f, 0.08f, 0.12f);

    // The four colours of "4-colour mode", the Game Boy Color rule: white, light, dark, black.
    public Color GbWhite = new(0.97f, 0.97f, 0.93f);
    public Color GbLight, GbDark;
    public Color GbBlack = new(0.1f, 0.09f, 0.13f);

    public CreaturePalette(Hsv main, Hsv second, Hsv accent, Hsv membrane, Hsv glow, Hsv metal)
    {
        Main = Ramp.From(main);
        Second = Ramp.From(second);
        Accent = Ramp.From(accent);
        Membrane = Ramp.From(membrane);
        Metal = Ramp.From(metal);
        Glow = glow.ToColor();
        GlowHot = new Hsv(glow.H, glow.S * 0.3f, 1f).ToColor();
        GbLight = main.Shift(0, -0.05f, 0.08f).ToColor();
        GbDark = new Hsv(main.H, Mathf.Min(1, main.S + 0.1f), main.V * 0.5f).ToColor();
    }
}

/// <summary>The HSV choices behind a palette, rolled per species.</summary>
public record struct PaletteSpec(Hsv Main, Hsv Second, Hsv Accent, Hsv Membrane, Hsv Glow, Hsv Metal)
{
    // Neon: hextech blue, chemtech green, shimmer pink, forge amber, a Zaun red.
    static readonly Hsv Amber = new(0.11f, 0.85f, 1f), Hextech = new(0.52f, 0.75f, 1f), Chem = new(0.3f, 0.9f, 1f),
        Shimmer = new(0.87f, 0.65f, 1f), Ember = new(0.02f, 0.85f, 1f);
    // Metals: brass, copper, gunmetal.
    static readonly Hsv Brass = new(0.11f, 0.62f, 0.82f), Copper = new(0.06f, 0.62f, 0.72f), Gunmetal = new(0.6f, 0.15f, 0.5f);

    /// <summary>A palette from hex colours (hand-authored species).</summary>
    public static PaletteSpec FromHex(string main, string second, string accent, string membrane = null, string glow = null)
    {
        static Hsv H(string hex) { var c = Color.FromHtml(hex); return new Hsv(c.H, c.S, c.V); }
        var s = H(second);
        return new PaletteSpec(H(main), s, H(accent),
            membrane != null ? H(membrane) : new Hsv(s.H, s.S * 0.6f, Mathf.Min(1, s.V + 0.05f)),
            glow != null ? H(glow) : Amber, Brass);
    }

    public static PaletteSpec Roll(Element element, SeededRng rng)
    {
        Hsv main = RollMain(element, rng);

        // Bellies and markings: bone and tan, a darker shade of the body (saddles and stripes read
        // as menace), or a contrasting colour. No pastel creams.
        Hsv second = rng.Weighted(
            (new Hsv(Mathf.Lerp(main.H, 0.1f, 0.5f), rng.Range(0.18f, 0.35f), rng.Range(0.78f, 0.9f)), 3f), // bone belly
            (new Hsv(Mathf.PosMod(main.H + 0.02f, 1f), Mathf.Min(1, main.S * 1.1f), main.V * 0.5f), 3f),      // dark self
            (main.Shift(rng.Range(0.42f, 0.58f), -0.1f, -0.15f), 1.2f),                                      // contrast
            (main.Shift(rng.Chance(0.5f) ? 0.08f : -0.08f, 0.05f, -0.1f), 1.5f));                             // neighbour

        Hsv accent = rng.Weighted(
            (new Hsv(0.11f, 0.2f, 0.88f), 4f),   // bone
            (new Hsv(0.72f, 0.22f, 0.24f), 2.5f), // obsidian
            (new Hsv(0.11f, 0.65f, 0.85f), 1.2f), // brass
            (new Hsv(0.99f, 0.75f, 0.7f), 1f),    // blood red
            (new Hsv(0.6f, 0.12f, 0.72f), 1.5f)); // steel

        Hsv membrane = new(second.H, Mathf.Min(1, second.S * 0.9f + 0.1f), Mathf.Clamp(second.V * 0.9f, 0.35f, 0.85f));

        Hsv glow = element switch
        {
            Element.Ember => rng.Pick(new[] { Amber, Ember }),
            Element.Spark or Element.Frost => rng.Pick(new[] { Hextech, Amber }),
            Element.Venom or Element.Leaf => rng.Pick(new[] { Chem, Shimmer }),
            Element.Shade => rng.Pick(new[] { Shimmer, Hextech, Ember }),
            _ => rng.Pick(new[] { Amber, Hextech, Chem, Shimmer }),
        };
        Hsv metal = rng.Weighted((Brass, 3f), (Copper, 2f), (Gunmetal, 1.5f));
        // The glow has to stand out from the body: a green glow on a green body turns pink.
        if (Mathf.Abs(Mathf.PosMod(glow.H - main.H + 0.5f, 1f) - 0.5f) < 0.08f) glow = glow.H is > 0.4f and < 0.7f ? Amber : Hextech;
        return new PaletteSpec(main, second, accent, membrane, glow, metal);
    }

    static Hsv RollMain(Element e, SeededRng rng)
    {
        // Earthy, saturated, mid-dark: the FFTA / Fell Seal monster range rather than pastels.
        (float h0, float h1, float s0, float s1, float v0, float v1) = e switch
        {
            Element.Ember => (-0.02f, 0.06f, 0.72f, 0.92f, 0.68f, 0.88f),
            Element.Tide => (0.55f, 0.64f, 0.55f, 0.8f, 0.5f, 0.75f),
            Element.Leaf => (0.22f, 0.36f, 0.5f, 0.75f, 0.42f, 0.66f),
            Element.Spark => (0.11f, 0.15f, 0.75f, 0.92f, 0.82f, 0.94f),
            Element.Stone => (0.06f, 0.12f, 0.18f, 0.38f, 0.42f, 0.62f),
            Element.Venom => (0.74f, 0.86f, 0.42f, 0.68f, 0.4f, 0.62f),
            Element.Frost => (0.52f, 0.58f, 0.22f, 0.42f, 0.72f, 0.88f),
            Element.Shade => (0.68f, 0.78f, 0.22f, 0.42f, 0.26f, 0.4f),
            _ => rng.Chance(0.5f) ? (0.06f, 0.11f, 0.4f, 0.62f, 0.5f, 0.72f) : (0.58f, 0.64f, 0.06f, 0.16f, 0.45f, 0.62f),
        };
        return new Hsv(Mathf.PosMod(rng.Range(h0, h1), 1f), rng.Range(s0, s1), rng.Range(v0, v1));
    }

    /// <summary>The abyssal strain: the body sinks toward black so the glow does the talking.</summary>
    public PaletteSpec Abyssal(SeededRng rng) => this with
    {
        Main = new Hsv(Main.H, Main.S * 0.6f, Mathf.Max(0.2f, Main.V * 0.42f)),
        Second = rng.Chance(0.5f) ? new Hsv(0.1f, 0.15f, 0.78f) : new Hsv(Main.H, Main.S * 0.7f, Mathf.Max(0.12f, Main.V * 0.22f)),
        Glow = rng.Pick(new[] { new Hsv(0.0f, 0.9f, 1f), new Hsv(0.78f, 0.7f, 1f), Chem }),
    };

    /// <summary>Small per-individual drift in shade, so siblings aren't identical.</summary>
    public PaletteSpec Jitter(SeededRng rng)
    {
        float dh = rng.Range(-0.015f, 0.015f), dv = rng.Range(-0.05f, 0.05f);
        return this with { Main = Main.Shift(dh, 0, dv), Second = Second.Shift(dh, 0, dv * 0.5f), Membrane = Membrane.Shift(dh) };
    }

    /// <summary>The rare "shiny" palette: the main hue rotates, the rest follows.</summary>
    public PaletteSpec Shiny(SeededRng rng)
    {
        float dh = rng.Range(0.3f, 0.55f);
        return this with { Main = Main.Shift(dh, 0.05f, 0.05f), Second = Second.Shift(dh), Accent = Accent.Shift(dh * 0.5f), Membrane = Membrane.Shift(dh), Glow = Glow.Shift(dh) };
    }

    public CreaturePalette Build() => new(Main, Second, Accent, Membrane, Glow, Metal);
}
