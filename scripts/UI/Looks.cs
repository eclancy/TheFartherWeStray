using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// A gilt-framed menu look's colours (docs/ui-style.md): the frame, the glass inside it, its lights and its
/// words. Dusk is the game's (the user's pick, 2026-11-07); Lamplight, Verdigris and Gilt and neon are the
/// others the player can pick in Settings. The softer three, after FFTA's windows, lift the glass off black,
/// take the gold down a step and light it from the top left and quiet the lights. Every look draws its words
/// in the pixel font with a shadow in the glass's own dark (the user's pick, 2026-11-07).
/// </summary>
public sealed class Look
{
    /// <summary>The frame's outer edge; the gold line lit (top and left) and shaded (bottom and right); brighter when you're on it; a tooltip's; a disabled control's.</summary>
    public Color Edge, Gold, GoldShade, GoldLit, GoldLitShade, TipGold, TipGoldShade, GoldOff;

    /// <summary>The glass, normal, pointed at, pressed and disabled; the ring inside the gold, mixed this much toward Inner.</summary>
    public Color Glass, GlassHover, GlassPressed, GlassOff, Inner;
    public float InnerMix;

    /// <summary>The tube inside the gold: unlit on a button and on a panel, disabled, lit for the focus and for a press, their bright cores, the focus frame's edge, a panel's corner pips.</summary>
    public Color Tube, TubePanel, TubeOff, Focus, FocusCore, Press, PressCore, FocusEdge, Pip;

    /// <summary>How far a light given by a screen (a squad card's cyan) goes toward white at its core.</summary>
    public float CoreLift = 0.55f;

    /// <summary>The most saturated and the brightest a screen's own colours may be in this look.</summary>
    public float Saturation = 1, Brightness = 1;

    /// <summary>The words: normal, on a control that's lit, pressed, disabled; secondary, faint, headings, and the colours words take.</summary>
    public Color Ink, InkLit, InkPressed, InkOff, Dim, Faint, Heading, Cyan, Pink, Magenta, Good, Bad;

    /// <summary>The dark under the words: a shadow (pixel font) or an outline (the sans).</summary>
    public Color Shadow;

    /// <summary>Words in the game's pixel font (<see cref="PixelFont"/>) rather than the plain sans.</summary>
    public bool Pixel;

    /// <summary>
    /// A screen's own bright colour in this look: one near the hue of the look's own (the cyan of a squad card,
    /// the gold of a chosen one) becomes that one; any other is held to the look's saturation and brightness.
    /// Softening a softened colour changes nothing.
    /// </summary>
    public Color Soften(Color c)
    {
        if (Saturation >= 1 && Brightness >= 1) return c;
        if (c.S > 0.3f)
            foreach (var own in new[] { Heading, Cyan, Magenta, Good, Bad })
            {
                float d = Mathf.Abs(c.H - own.H);
                if (Mathf.Min(d, 1 - d) < 0.045f) return new Color(own, c.A);
            }
        return Color.FromHsv(c.H, Mathf.Min(c.S, Saturation), Mathf.Min(c.V, Brightness), c.A);
    }

    static Color C(string hex, float a = 1) => new(Color.FromHtml(hex), a);

    /// <summary>D, Gilt and neon (the default from 2026-11-03 to 11-07): near-black violet glass, bright gold, cyan and pink neon.</summary>
    public static readonly Look GiltNeon = new()
    {
        Edge = C("#05040a", 0.9f), Gold = C("#c99a40"), GoldShade = C("#c99a40"), GoldLit = C("#ffd77a"), GoldLitShade = C("#ffd77a"),
        TipGold = C("#9a7a3a"), TipGoldShade = C("#9a7a3a"), GoldOff = C("#3a3644"),
        Glass = C("#120e22", 0.94f), GlassHover = C("#1a1430", 0.95f), GlassPressed = C("#0c0916", 0.96f), GlassOff = C("#100c1a", 0.75f),
        Inner = C("#120e22"), InnerMix = 0,
        Tube = C("#24384a"), TubePanel = C("#3a2a5a"), TubeOff = C("#1c1826"), Focus = C("#4fe3ff"), FocusCore = C("#d8fdff"),
        Press = C("#ff5fae"), PressCore = C("#ffd6ec"), FocusEdge = C("#fff0c0"), Pip = C("#4fe3ff"),
        Ink = C("#ece7f6"), InkLit = C("#ffffff"), InkPressed = C("#ffd6ec"), InkOff = C("#5f5870"),
        Dim = C("#a49cbc"), Faint = C("#6f6788"), Heading = C("#f2c14e"),
        Cyan = C("#4fe3ff"), Pink = C("#ff8ac0"), Magenta = C("#ff4f9a"), Good = C("#6ff0a0"), Bad = C("#ff7f8f"),
        Shadow = C("#0b0914"), Pixel = true,
    };

    /// <summary>
    /// E, Dusk, the game's look (the user's pick, 2026-11-07): gilt and neon at dusk. The same violet glass
    /// lifted to a dusky mid tone, the gold antique and lit from the top left, the neon turned down to sea
    /// glass and rose; ivory words in the pixel font with a plum shadow.
    /// </summary>
    public static readonly Look Dusk = new()
    {
        Edge = C("#1a1428", 0.85f), Gold = C("#d6b676"), GoldShade = C("#9a7a4a"), GoldLit = C("#f0d89e"), GoldLitShade = C("#c4a26a"),
        TipGold = C("#b39462"), TipGoldShade = C("#8a7048"), GoldOff = C("#4c4560"),
        Glass = C("#2c2442", 0.96f), GlassHover = C("#362c50", 0.97f), GlassPressed = C("#241d37", 0.97f), GlassOff = C("#262035", 0.85f),
        Inner = C("#1f1931"), InnerMix = 0.6f,
        Tube = C("#41375c"), TubePanel = C("#463a66"), TubeOff = C("#2f2843"), Focus = C("#8ed6d2"), FocusCore = C("#cfeee9"),
        Press = C("#e396ba"), PressCore = C("#f3d3e2"), FocusEdge = C("#e9d29b", 0.7f), Pip = C("#8ed6d2"),
        CoreLift = 0.3f, Saturation = 0.45f, Brightness = 0.9f,
        Ink = C("#f0e7d6"), InkLit = C("#fff8ea"), InkPressed = C("#f6d9e4"), InkOff = C("#71688a"),
        Dim = C("#b2a8c5"), Faint = C("#7e7496"), Heading = C("#e8c983"),
        Cyan = C("#8ed6d2"), Pink = C("#e7a3c3"), Magenta = C("#e090b6"), Good = C("#9fdbac"), Bad = C("#ee9c9c"),
        Shadow = C("#140f20", 0.9f), Pixel = true,
    };

    /// <summary>
    /// F, Lamplight: the city's gilt by gaslight. Warm umber glass like an old leather ledger, a brass frame,
    /// lights in verdigris and coral; cream words in the pixel font with a brown shadow. The warmest, and the
    /// nearest FFTA's own warm windows.
    /// </summary>
    public static readonly Look Lamplight = new()
    {
        Edge = C("#1c130f", 0.85f), Gold = C("#e2c385"), GoldShade = C("#a5814d"), GoldLit = C("#f6e0ac"), GoldLitShade = C("#cda86c"),
        TipGold = C("#c4a36a"), TipGoldShade = C("#93744a"), GoldOff = C("#56493d"),
        Glass = C("#3a2d27", 0.96f), GlassHover = C("#46372f", 0.97f), GlassPressed = C("#30251f", 0.97f), GlassOff = C("#30261f", 0.85f),
        Inner = C("#2a201b"), InnerMix = 0.6f,
        Tube = C("#55443a"), TubePanel = C("#5a4839"), TubeOff = C("#3d322b"), Focus = C("#93d1c0"), FocusCore = C("#d6efe6"),
        Press = C("#e8988a"), PressCore = C("#f7d8cf"), FocusEdge = C("#f2dca6", 0.7f), Pip = C("#93d1c0"),
        CoreLift = 0.3f, Saturation = 0.48f, Brightness = 0.93f,
        Ink = C("#f6ebd5"), InkLit = C("#fff9ec"), InkPressed = C("#f8ddd3"), InkOff = C("#7d6c5d"),
        Dim = C("#c8b59d"), Faint = C("#8f7d6a"), Heading = C("#efcb7f"),
        Cyan = C("#93d1c0"), Pink = C("#e9a8ad"), Magenta = C("#e3929a"), Good = C("#b2d68e"), Bad = C("#ec9d88"),
        Shadow = C("#1a110d", 0.9f), Pixel = true,
    };

    /// <summary>
    /// G, Verdigris: the undercity's take. Smoked teal glass, a brass frame gone green where the light doesn't
    /// reach, chem-green and rose lights; pale mint words in the pixel font with a deep teal shadow.
    /// </summary>
    public static readonly Look Verdigris = new()
    {
        Edge = C("#0c1819", 0.85f), Gold = C("#d2bb7e"), GoldShade = C("#6e977f"), GoldLit = C("#ead79f"), GoldLitShade = C("#8fbba0"),
        TipGold = C("#ad9c68"), TipGoldShade = C("#5f8573"), GoldOff = C("#3f4f4d"),
        Glass = C("#203236", 0.96f), GlassHover = C("#283e41", 0.97f), GlassPressed = C("#1a292c", 0.97f), GlassOff = C("#1d2a2c", 0.85f),
        Inner = C("#162427"), InnerMix = 0.6f,
        Tube = C("#2f4948", 0.95f), TubePanel = C("#33504c"), TubeOff = C("#253535"), Focus = C("#a5e3a0"), FocusCore = C("#dcf5d6"),
        Press = C("#e79db5"), PressCore = C("#f6d8e2"), FocusEdge = C("#e6d6a0", 0.7f), Pip = C("#a5e3a0"),
        CoreLift = 0.3f, Saturation = 0.45f, Brightness = 0.9f,
        Ink = C("#e6f0e8"), InkLit = C("#f6fff7"), InkPressed = C("#f6dde6"), InkOff = C("#5f7774"),
        Dim = C("#a3bab3"), Faint = C("#6d8682"), Heading = C("#dcc887"),
        Cyan = C("#8fd8d2"), Pink = C("#e8a6bd"), Magenta = C("#e197b2"), Good = C("#a5e3a0"), Bad = C("#ef9f95"),
        Shadow = C("#0a1416", 0.9f), Pixel = true,
    };
}
