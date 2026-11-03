using System;
using System.Collections.Generic;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The menu styles (docs/ui-style.md). Gilt and neon is the game's (the user's pick, 2026-11-03); the
/// others are kept to compare against (--ui=NAME), and Plain is Godot's own look, as the game had it.
/// </summary>
public enum Skin { Plain, Gilded, Neon, Iron, GiltNeon }

/// <summary>What a frame is for: a button, a small one (14-16 point words), a panel or window, a tooltip.</summary>
public enum UiPart { Button, Small, Panel, Tip }

public enum UiState { Normal, Hover, Focus, Pressed, Disabled }

/// <summary>
/// The menus' look, painted in code like everything else. Each frame is a small piece of pixel art
/// (four corners, edges one colour along their length, a flat middle) scaled by <see cref="Px"/>, so
/// a frame's pixels are the board's, and drawn as a nine-slice that fits any size. <see cref="Use"/>
/// puts a skin's frames and text colours on Godot's default theme, so every button, panel and tooltip
/// takes it; screens that draw their own boxes ask <see cref="Box"/>, or <see cref="Lit"/> for one whose
/// light means something (a creature in the squad, a side in the turn order). Godot draws a focused
/// control's focus frame over its normal one, so focus frames are overlays with an empty middle.
/// </summary>
public static class UiSkin
{
    /// <summary>A frame pixel is this many screen pixels: the board's 3x (small buttons' frames are 2x).</summary>
    public const int Px = 3;

    public static Skin Current = Skin.GiltNeon;

    public static bool TryParse(string name, out Skin skin) => Enum.TryParse(name.Replace("-", "").Replace("_", ""), true, out skin);

    public static string Title(Skin s) => s switch
    {
        Skin.Gilded => "A  Gilded",
        Skin.Neon => "B  Neon sign",
        Skin.Iron => "C  Foundry iron",
        Skin.GiltNeon => "D  Gilt and neon",
        _ => "Plain (as it was)",
    };

    /// <summary>Puts a skin on the default theme. Plain leaves Godot's own look (and the phone's gold focus frame) alone.</summary>
    public static void Use(Skin skin)
    {
        Current = skin;
        if (skin == Skin.Plain) return;
        var theme = ThemeDB.GetDefaultTheme();
        theme.SetTypeVariation("SmallButton", "Button");
        foreach (var (type, part) in new[] { ("Button", UiPart.Button), ("OptionButton", UiPart.Button), ("MenuButton", UiPart.Button), ("CheckButton", UiPart.Button), ("CheckBox", UiPart.Button), ("SmallButton", UiPart.Small) })
        {
            theme.SetStylebox("normal", type, Box(part, UiState.Normal, skin));
            theme.SetStylebox("hover", type, Box(part, UiState.Hover, skin));
            theme.SetStylebox("pressed", type, Box(part, UiState.Pressed, skin));
            theme.SetStylebox("hover_pressed", type, Box(part, UiState.Pressed, skin));
            theme.SetStylebox("disabled", type, Box(part, UiState.Disabled, skin));
            theme.SetStylebox("focus", type, Box(part, UiState.Focus, skin));
            theme.SetColor("font_color", type, TextColor(UiState.Normal, skin));
            theme.SetColor("font_hover_color", type, TextColor(UiState.Hover, skin));
            theme.SetColor("font_focus_color", type, TextColor(UiState.Focus, skin));
            theme.SetColor("font_pressed_color", type, TextColor(UiState.Pressed, skin));
            theme.SetColor("font_hover_pressed_color", type, TextColor(UiState.Pressed, skin));
            theme.SetColor("font_disabled_color", type, TextColor(UiState.Disabled, skin));
            theme.SetColor("font_outline_color", type, Outline(skin));
            theme.SetConstant("outline_size", type, 4);
        }
        theme.SetStylebox("normal", "LineEdit", Box(UiPart.Button, UiState.Normal, skin));
        theme.SetStylebox("focus", "LineEdit", Box(UiPart.Button, UiState.Focus, skin));
        theme.SetStylebox("panel", "PanelContainer", Box(UiPart.Panel, UiState.Normal, skin));
        theme.SetStylebox("panel", "Panel", Box(UiPart.Panel, UiState.Normal, skin));
        theme.SetStylebox("panel", "TooltipPanel", Box(UiPart.Tip, UiState.Normal, skin));

        // Sliders: a dark groove, a tube lit cyan as far as the value goes, and a gold diamond to drag.
        StyleBoxFlat Groove(string fill, string edge)
        {
            var g = new StyleBoxFlat { BgColor = C(fill), BorderColor = C(edge) };
            g.SetBorderWidthAll(Px);
            g.ContentMarginTop = g.ContentMarginBottom = Px;
            return g;
        }
        theme.SetStylebox("slider", "HSlider", Groove("#1c1826", "#05040a"));
        theme.SetStylebox("grabber_area", "HSlider", Groove("#4fe3ff", "#05040a"));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Groove("#d8fdff", "#05040a"));
        theme.SetIcon("grabber", "HSlider", Diamond("#f2c14e", "#ffe7a3"));
        theme.SetIcon("grabber_highlight", "HSlider", Diamond("#ffd77a", "#ffffff"));
        theme.SetIcon("grabber_disabled", "HSlider", Diamond("#3a3644", "#5f5870"));
    }

    /// <summary>A slider's handle: a small diamond, dark-edged, its middle lighter.</summary>
    static Texture2D Diamond(string gold, string core)
    {
        var img = Image.CreateEmpty(7, 7, false, Image.Format.Rgba8);
        for (int y = 0; y < 7; y++)
        for (int x = 0; x < 7; x++)
        {
            int r = 3 - (Math.Abs(x - 3) + Math.Abs(y - 3));
            if (r >= 0) img.SetPixel(x, y, r == 0 ? C("#05040a") : r == 1 ? C(gold) : C(core));
        }
        img.Resize(7 * Px, 7 * Px, Image.Interpolation.Nearest);
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>
    /// As each control joins the tree: a button with small words (16 points or less, set before it's
    /// added) takes the small frame, so a row of them doesn't grow round a frame made for the big menus.
    /// </summary>
    public static void Adapt(Node node)
    {
        if (Current == Skin.Plain || node is not Button b || b.ThemeTypeVariation != "" || b.Icon != null) return;
        if (b.HasThemeFontSizeOverride("font_size") && b.GetThemeFontSize("font_size") <= 16) b.ThemeTypeVariation = "SmallButton";
    }

    /// <summary>The colour of a control's words in each state.</summary>
    public static Color TextColor(UiState state, Skin? skin = null) => Color.FromHtml((skin ?? Current) switch
    {
        Skin.Gilded => state switch { UiState.Hover or UiState.Focus => "#ffe7a3", UiState.Pressed => "#ffffff", UiState.Disabled => "#5d5868", _ => "#e8dcbc" },
        Skin.Neon => state switch { UiState.Hover => "#d8fbff", UiState.Focus => "#ffffff", UiState.Pressed => "#ffe0f0", UiState.Disabled => "#4a4458", _ => "#8fb0ba" },
        Skin.Iron => state switch { UiState.Hover => "#ffe2b8", UiState.Focus => "#ffc06a", UiState.Pressed => "#ffffff", UiState.Disabled => "#5c5562", _ => "#d9d2c5" },
        Skin.GiltNeon => state switch { UiState.Hover or UiState.Focus => "#ffffff", UiState.Pressed => "#ffd6ec", UiState.Disabled => "#5f5870", _ => "#e6e0f2" },
        _ => state == UiState.Disabled ? "#6a6478" : "#ece7f6",
    });

    /// <summary>The dark edge round a skin's words.</summary>
    public static Color Outline(Skin? skin = null) => Color.FromHtml((skin ?? Current) switch
    {
        Skin.Gilded => "#05060c",
        Skin.Neon => "#04222b",
        Skin.Iron => "#08070a",
        _ => "#0b0914",
    });

    /// <summary>The skin's headings: its gold, or its light.</summary>
    public static Color Heading(Skin? skin = null) => Color.FromHtml((skin ?? Current) switch
    {
        Skin.Neon => "#ff8ac8",
        Skin.Iron => "#e8a25c",
        _ => "#f2c14e",
    });

    static readonly Dictionary<(Skin, UiPart, UiState, Color?), StyleBox> _boxes = new();

    /// <summary>A frame for a part in a state, in a skin (the one in use by default).</summary>
    public static StyleBox Box(UiPart part, UiState state, Skin? which = null) => Make(which ?? Current, part, state, null);

    /// <summary>
    /// A frame whose light means something: its tube lit in that colour (a creature in the squad, a side
    /// in the turn order, the card that's chosen), and its gold bright when strong.
    /// </summary>
    public static StyleBox Lit(UiPart part, Color light, bool strong = false, Skin? which = null) => Make(which ?? Current, part, strong ? UiState.Hover : UiState.Normal, light);

    /// <summary>A copy of a frame with other room inside it: a skill slot's icon or a portrait wants less than words do.</summary>
    public static StyleBox Tight(StyleBox box, float pad)
    {
        var copy = (StyleBox)box.Duplicate();
        copy.ContentMarginLeft = copy.ContentMarginRight = copy.ContentMarginTop = copy.ContentMarginBottom = pad;
        return copy;
    }

    static StyleBox Make(Skin skin, UiPart part, UiState state, Color? light)
    {
        if (_boxes.TryGetValue((skin, part, state, light), out var known)) return known;
        StyleBox box = skin == Skin.Plain ? PlainBox(part, state, light) : Painted(skin, part, state, light);
        _boxes[(skin, part, state, light)] = box;
        return box;
    }

    /// <summary>The look the game had: flat dark boxes, a thin border, round corners.</summary>
    static StyleBox PlainBox(UiPart part, UiState state, Color? light)
    {
        if (part is UiPart.Button or UiPart.Small)
        {
            var b = new StyleBoxFlat
            {
                BgColor = state switch { UiState.Hover => new Color(0.2f, 0.2f, 0.24f, 0.75f), UiState.Pressed => new Color(0.08f, 0.08f, 0.1f, 0.75f), UiState.Disabled => new Color(0.1f, 0.1f, 0.12f, 0.5f), UiState.Focus => Colors.Transparent, _ => new Color(0.1f, 0.1f, 0.12f, 0.6f) },
                BorderColor = light ?? (state == UiState.Focus ? new Color(1, 1, 1, 0.75f) : Colors.Transparent),
                DrawCenter = state != UiState.Focus,
            };
            b.SetBorderWidthAll(state == UiState.Focus || light != null ? 2 : 0);
            b.SetCornerRadiusAll(3);
            b.SetContentMarginAll(8);
            return b;
        }
        var p = new StyleBoxFlat { BgColor = new Color(0.06f, 0.05f, 0.1f, 0.96f), BorderColor = light ?? (part == UiPart.Tip ? Ui.Dim : Ui.Gold) };
        p.SetBorderWidthAll(2);
        p.SetCornerRadiusAll(8);
        p.SetContentMarginAll(part == UiPart.Tip ? 10 : 22);
        return p;
    }

    /// <summary>Corner size in frame pixels: room for the corner's art, and what the nine-slice keeps unstretched.</summary>
    static int Corner(Skin skin, UiPart part) => part == UiPart.Panel || skin is Skin.Gilded or Skin.GiltNeon ? 6 : skin == Skin.Neon ? 5 : 4;

    /// <summary>
    /// A whole frame at its own size in frame pixels (unscaled), for art that isn't a nine-slice, such as
    /// the app's icon. Lit draws the focus frame over it.
    /// </summary>
    public static Image Paint(Skin skin, UiPart part, UiState state, int w, int h, bool lit = false, Color? light = null)
    {
        var shape = part == UiPart.Small ? UiPart.Button : part;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var f = new Frag(x, y, w, h);
            var c = Pixel(skin, shape, state, f, light);
            if (lit && Pixel(skin, shape, UiState.Focus, f, light) is { A: > 0 } over) c = c.A > 0 ? c.Blend(over) : over;
            if (c.A > 0) img.SetPixel(x, y, c);
        }
        return img;
    }

    /// <summary>One pixel of a skin's frame. The older skins have no coloured light: a lit frame is their hover.</summary>
    static Color Pixel(Skin skin, UiPart part, UiState state, Frag f, Color? light)
    {
        var st = light != null && state == UiState.Normal ? UiState.Hover : state;
        return skin switch
        {
            Skin.Gilded => Gilded(part, st, f),
            Skin.Neon => Neon(part, st, f),
            Skin.Iron => Iron(part, st, f),
            _ => GiltNeon(part, state, f, light),
        };
    }

    static StyleBox Painted(Skin skin, UiPart part, UiState state, Color? light)
    {
        int px = part == UiPart.Small ? 2 : Px;
        int m = Corner(skin, part == UiPart.Small ? UiPart.Button : part), size = m * 2 + 2;
        var img = Paint(skin, part, state, size, size, light: light);
        img.Resize(size * px, size * px, Image.Interpolation.Nearest);
        int margin = m * px;
        var box = new StyleBoxTexture
        {
            Texture = ImageTexture.CreateFromImage(img),
            TextureMarginLeft = margin, TextureMarginRight = margin, TextureMarginTop = margin, TextureMarginBottom = margin,
            DrawCenter = state != UiState.Focus,
        };
        (int padX, int padY) = part switch
        {
            UiPart.Panel => (margin + 4, margin + 2),
            UiPart.Tip => (margin + 2, margin - 4),
            _ => (margin + 2, px * 2 + 2),
        };
        box.ContentMarginLeft = padX;
        box.ContentMarginRight = padX;
        box.ContentMarginTop = padY;
        box.ContentMarginBottom = padY;
        return box;
    }

    /// <summary>
    /// A pixel of a frame, as how far it is in from the frame's edge on each axis (dx, dy: the corner
    /// art is the same at every corner) and which side it's on, for frames lit from the top left.
    /// </summary>
    readonly struct Frag
    {
        public readonly int Dx, Dy;
        public readonly bool Left, Top;
        public Frag(int x, int y, int w, int h)
        {
            Dx = Math.Min(x, w - 1 - x);
            Dy = Math.Min(y, h - 1 - y);
            Left = x < w / 2;
            Top = y < h / 2;
        }
    }

    /// <summary>How many pixels in from the edge of a frame with stepped (Art Deco) corners, each step a pixels; -1 outside.</summary>
    static int Stepped(Frag f, int a)
    {
        int d1 = Math.Max(f.Dx - 2 * a, f.Dy - a), d2 = Math.Max(f.Dx - a, f.Dy - 2 * a);
        if (d1 < 0 || d2 < 0) return -1;
        return Math.Min(Math.Min(f.Dx, f.Dy), Math.Min(d1, d2));
    }

    /// <summary>
    /// The stepped outline for the outer two rings, then plain straight rings inside: a nine-slice's
    /// corner is too small to step every ring in turn, and the middle must come out as the fill.
    /// </summary>
    static int Deco(Frag f, int a)
    {
        int r = Stepped(f, a);
        return r < 2 ? r : Math.Min(f.Dx, f.Dy);
    }

    /// <summary>How many pixels in from the edge of a frame with round corners of radius r; -1 outside.</summary>
    static int Rounded(Frag f, int r)
    {
        if (f.Dx >= r || f.Dy >= r) return Math.Min(f.Dx, f.Dy);
        float d = r + 0.5f - Mathf.Sqrt((r - f.Dx) * (r - f.Dx) + (r - f.Dy) * (r - f.Dy));
        return d < 0 ? -1 : (int)d;
    }

    /// <summary>How many pixels in from the edge of a frame with its corners cut at 45 degrees, c pixels; -1 outside.</summary>
    static int Cut(Frag f, int c)
    {
        int d = f.Dx + f.Dy - c;
        return d < 0 ? -1 : Math.Min(Math.Min(f.Dx, f.Dy), d);
    }

    static Color C(string hex, float a = 1) => new(Color.FromHtml(hex), a);

    static readonly Color None = new(0, 0, 0, 0);

    /// <summary>
    /// A: the city above. Deep navy, a gold line with stepped corners and a fainter line inside it, like
    /// a gilded picture frame; a gold jewel in each panel corner. Focus: the gold brightens and a bar of
    /// gold leaf runs down the left side.
    /// </summary>
    static Color Gilded(UiPart part, UiState st, Frag f)
    {
        int a = part == UiPart.Tip ? 1 : 2;
        int r = Deco(f, a);
        if (r < 0) return None;
        bool off = st == UiState.Disabled;
        string gold = off ? "#3d3a44" : st switch { UiState.Hover => "#e9c46a", UiState.Pressed or UiState.Focus => "#ffe7a3", _ => part == UiPart.Tip ? "#8a6c32" : "#b48a3c" };
        string inner = off ? "#24222c" : st switch { UiState.Hover => "#8a6c32", UiState.Pressed => "#b48a3c", UiState.Focus => "#e9c46a", _ => "#5e4a26" };
        var fill = off ? C("#0e1020", 0.8f) : st switch { UiState.Hover => C("#182040", 0.96f), UiState.Pressed => C("#080b18", 0.97f), _ => C(part == UiPart.Panel ? "#0c1022" : "#0f1428", 0.95f) };
        if (st == UiState.Focus)
        {
            // An overlay: the bright lines and the gold leaf down the left.
            if (r == 1) return C(gold);
            if (r == 3) return C(inner);
            if (f.Left && f.Dx == 2 && f.Dy >= 2) return C("#f2c14e");
            return None;
        }
        if (part == UiPart.Panel && !off && Math.Abs(f.Dx - 4) + Math.Abs(f.Dy - 4) <= 1) return C("#f2c14e");
        return r switch { 0 => C("#05060c", 0.9f), 1 => C(gold), 3 => C(inner), _ => fill };
    }

    /// <summary>
    /// B: the undercity. Sooty glass with a neon tube round it, rounded at the corners. A button's tube
    /// is unlit glass until you're on it; then it lights cyan with a glow, and pink as it's pressed.
    /// Panels are lit pink, tooltips amber.
    /// </summary>
    static Color Neon(UiPart part, UiState st, Frag f)
    {
        int r = Rounded(f, part == UiPart.Panel ? 5 : 4);
        if (r < 0) return None;
        bool lit = st is UiState.Hover or UiState.Focus or UiState.Pressed || part != UiPart.Button;
        string light = part == UiPart.Panel ? "#ff5fae" : part == UiPart.Tip ? "#ffb347" : st == UiState.Pressed ? "#ff5fae" : "#4fe3ff";
        string core = part == UiPart.Panel ? "#ffc4e2" : part == UiPart.Tip ? "#ffe3b8" : st == UiState.Pressed ? "#ffd6ec" : "#d8fdff";
        float glow = part == UiPart.Panel ? 0.7f : st == UiState.Focus ? 1.2f : 1;
        var fill = st == UiState.Disabled ? C("#0b0912", 0.7f) : C("#0b0912", 0.93f);
        Color tube = st == UiState.Disabled ? C("#241f2e") : lit ? C(light) : C("#1d3a42");
        Color center = st == UiState.Disabled ? C("#2c2638") : lit ? C(core) : C("#2b5560");
        if (st == UiState.Focus)
            return r switch { 0 => C(light, 0.22f * glow), 1 => C(light, 0.45f * glow), 2 => tube, 3 => center, 4 => C(light, 0.28f), _ => None };
        return r switch
        {
            0 => lit ? C(light, 0.16f * glow) : None,
            1 => lit ? C(light, 0.36f * glow) : C("#000000", 0.35f),
            2 => tube,
            3 => center,
            4 => lit ? fill.Lerp(C(light), 0.22f) : fill,
            _ => fill,
        };
    }

    /// <summary>
    /// C: the foundry. An iron band, lit from the top left, round a sunken plate: green enamel on a
    /// button, bare iron with a rivet in each corner on a panel. Every button has a little lamp in its
    /// top-left corner; focus lights it amber and warms the rim to copper. Pressed, the bevel turns over.
    /// </summary>
    static Color Iron(UiPart part, UiState st, Frag f)
    {
        int r = Cut(f, 1);
        if (r < 0) return None;
        bool off = st == UiState.Disabled, down = st == UiState.Pressed, button = part == UiPart.Button;
        int band = button ? 2 : 3; // the inner bevel's ring
        bool lamp = button && f.Left && f.Top && f.Dx is 2 or 3 && f.Dy is 2 or 3;
        if (st == UiState.Focus)
        {
            if (r == 1) return C("#d07a3e");
            if (lamp) return f.Dx == 2 && f.Dy == 2 ? C("#fff0c0") : C("#ffb347");
            if (button && f.Left && f.Top && (f.Dx, f.Dy) is (4, 2) or (2, 4) or (4, 3) or (3, 4)) return C("#ff9a3c", 0.45f);
            return None;
        }
        // Lit from the top left: an edge is light on those sides, dark on the others (and the other way round sunk in).
        bool lightSide = (f.Top && f.Dy == r) || (f.Left && f.Dx == r);
        if (r == 0) return C("#08070a");
        if (r == 1) return off ? C("#2a2730") : (lightSide != down) ? C(st == UiState.Hover ? "#7a7280" : "#5f5868") : C("#121015");
        if (lamp) return off ? C("#24212a") : C("#3a2a22");
        if (!button && f.Dx is 2 or 3 && f.Dy is 2 or 3)
            return (f.Dx, f.Dy) switch { (2, 2) => C("#b8aeb8"), (3, 3) => C("#121015"), _ => C("#6a6270") }; // a rivet
        if (r < band) return C("#1a181e");
        if (r == band) return off ? C("#141217") : (lightSide != down) ? C("#0c0b0f") : C("#4a4450");
        return off ? C("#1a1c20", 0.92f) : button
            ? st switch { UiState.Hover => C("#1f4744"), UiState.Pressed => C("#0f2422"), _ => C("#163633") }
            : C("#211e26", 0.97f);
    }

    /// <summary>
    /// D: both cities at once. The city's gold line with stepped corners round smoked violet glass, and
    /// inside it the undercity's neon: an unlit tube that glows faintly when you point at it, lights cyan
    /// when you're on it (its outer edge turning pale gold too, to stand out from a frame lit for a reason)
    /// and pink as it's pressed. A frame given a light keeps its tube lit in that colour.
    /// Panels keep their tube dimly lit violet; a cyan pip sits in each panel corner.
    /// </summary>
    static Color GiltNeon(UiPart part, UiState st, Frag f, Color? tint)
    {
        int a = part == UiPart.Tip ? 1 : 2;
        int r = Deco(f, a);
        if (r < 0) return None;
        if (st != UiState.Focus && part == UiPart.Panel && f.Dx == 4 && f.Dy == 4) return tint ?? C("#4fe3ff");
        return GiltNeonRing(part, st, r, tint);
    }

    /// <summary>
    /// Any shape in gilt and neon at its own size in frame pixels, given how far each pixel is in from the
    /// shape's edge (-1 outside): the phone pad's cross and round buttons. A focus state paints the lit
    /// overlay alone, to draw over the shape.
    /// </summary>
    public static Image PaintRings(int w, int h, Func<int, int, int> ring, UiState state = UiState.Normal, Color? light = null)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int r = ring(x, y);
            if (r >= 0 && GiltNeonRing(UiPart.Button, state, r, light) is { A: > 0 } c) img.SetPixel(x, y, c);
        }
        return img;
    }

    /// <summary>How far a pixel of a w by h box is in from its edge, its corners stepped a pixels: for building shapes from boxes.</summary>
    public static int Inset(int x, int y, int w, int h, int a) => x < 0 || y < 0 || x >= w || y >= h ? -1 : Deco(new Frag(x, y, w, h), a);

    /// <summary>A gilt-and-neon frame's colour on its ring r (0 the dark edge, 1 the gold, 3 the tube, more the glass).</summary>
    static Color GiltNeonRing(UiPart part, UiState st, int r, Color? tint)
    {
        bool off = st == UiState.Disabled;
        bool on = st is UiState.Focus or UiState.Pressed || tint != null, half = st == UiState.Hover && tint == null;
        string gold = off ? "#3a3644" : st is UiState.Hover or UiState.Focus or UiState.Pressed ? "#ffd77a" : part == UiPart.Tip ? "#9a7a3a" : "#c99a40";
        Color light = tint ?? C(st == UiState.Pressed ? "#ff5fae" : "#4fe3ff");
        Color core = tint is { } t ? t.Lerp(Colors.White, 0.55f) : C(st == UiState.Pressed ? "#ffd6ec" : "#d8fdff");
        var fill = off ? C("#100c1a", 0.75f) : st switch { UiState.Hover => C("#1a1430", 0.95f), UiState.Pressed => C("#0c0916", 0.96f), _ => C("#120e22", 0.94f) };
        if (st == UiState.Focus)
            return r switch { 0 => C("#fff0c0"), 1 => C(gold), 2 => new Color(light, 0.5f), 3 => core, 4 => new Color(light, 0.35f), 5 => new Color(light, 0.12f), _ => None };
        return r switch
        {
            0 => C("#05040a", 0.9f),
            1 => C(gold),
            2 => on ? fill.Lerp(light, 0.35f) : half ? fill.Lerp(light, 0.12f) : fill,
            3 => off ? C("#1c1826") : on ? light : half ? light.Darkened(0.35f) : part == UiPart.Panel ? C("#3a2a5a") : C("#24384a"),
            4 => on ? fill.Lerp(light, 0.18f) : fill,
            _ => fill,
        };
    }
}
