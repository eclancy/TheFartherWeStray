using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using CreatureTactics.SpriteLab;
using CreatureTactics.Tactics;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>
/// The ranch: your roster, the squad for the next fight, and the breeding vats. Left-click a
/// creature to put it in (or take it out of) the squad; right-click to put it in a breeding vat;
/// Enter breeds; F goes to battle.
/// </summary>
public partial class RanchScreen : Control
{
    public Ranch Ranch;
    public Action Fight;
    public List<int> JustJoined = new(); // recruits from the last battle, to point out
    public bool VatDemo;                  // screenshots: fill the vats with the first rested pair and the newest child
    public bool AutoFight;                // --autoplay: head into the next battle on its own (an attract loop)

    static readonly Color Cyan = Color.FromHtml("#4fe3ff"), Magenta = Color.FromHtml("#ff4f9a"), Gold = Color.FromHtml("#f2c14e");
    static readonly Color Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#9a91b4");

    GridContainer _grid;
    Label _header, _status, _notes, _detail;
    VatPanel _vats;
    readonly List<int> _pen = new();
    int? _hover, _releaseArmed;
    double _releaseTimer;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, 99)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var shade = new ColorRect { Color = new Color(0.03f, 0.02f, 0.06f, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _header = Text(26, Ink, new Vector2(32, 20), new Vector2(1860, 40));
        _status = Text(17, Gold, new Vector2(32, 62), new Vector2(1100, 30));

        var scroll = new ScrollContainer { Position = new Vector2(24, 100), Size = new Vector2(1120, 960), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _grid = new GridContainer { Columns = 6 };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_grid);

        _vats = new VatPanel { Position = new Vector2(1170, 100), Size = new Vector2(720, 380), Screen = this, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_vats);
        _notes = Text(16, Ink, new Vector2(1170, 490), new Vector2(720, 120));
        _notes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detail = Text(16, Dim, new Vector2(1170, 620), new Vector2(720, 330));
        _detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Text(17, Ink, new Vector2(1170, 960), new Vector2(720, 80)).Text =
            "Left-click: squad (up to 4)    Right-click: breeding vat    Enter: breed\nF: to battle    Delete twice: release    Tab: Sprite Lab";

        if (VatDemo)
        {
            _pen.AddRange(Ranch.Creatures.Where(r => r.Rested).Take(2).Select(r => r.Id));
            var newest = Ranch.Creatures.LastOrDefault(r => r.Generation > 0);
            if (newest != null) ChildShown = Ranch.Get(newest.Id);
        }
        if (JustJoined.Count > 0)
            Flash($"{string.Join(", ", JustJoined.Select(id => Ranch.Get(id).Species.Name))} joined the ranch.");
        Rebuild();
        if (AutoFight) GetTree().CreateTimer(1.5).Timeout += () => { if (IsInsideTree() && Ranch.Squad.Count > 0) Fight?.Invoke(); };
    }

    Label Text(int size, Color color, Vector2 pos, Vector2 boxSize)
    {
        var l = new Label { Position = pos, Size = boxSize, MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        l.AddThemeConstantOverride("shadow_offset_x", 2);
        l.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(l);
        return l;
    }

    void Flash(string text) => _status.Text = text;

    void Rebuild()
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        foreach (var rec in Ranch.Creatures)
        {
            var card = new RanchCard { Screen = this, Id = rec.Id };
            int id = rec.Id;
            card.Clicked += right => { if (right) TogglePen(id); else ToggleSquad(id); };
            card.Hovered += () => { _hover = id; ShowDetail(id); };
            _grid.AddChild(card);
        }
        _header.Text = $"THE RANCH      {Ranch.Creatures.Count} creatures      squad {Ranch.Squad.Count}/{Ranch.SquadSize}      next: encounter {Ranch.Encounter}";
        _vats.QueueRedraw();
    }

    public bool InSquad(int id) => Ranch.Squad.Contains(id);
    public int PenSlot(int id) => _pen.IndexOf(id);
    public bool IsNew(int id) => JustJoined.Contains(id);
    public Creature ChildShown;
    public IReadOnlyList<int> Pen => _pen;

    void ToggleSquad(int id)
    {
        if (InSquad(id)) Ranch.Squad.Remove(id);
        else if (Ranch.Squad.Count < Ranch.SquadSize) Ranch.Squad.Add(id);
        else { Flash("The squad is full: take someone out first."); return; }
        Ranch.Save();
        Rebuild();
    }

    void TogglePen(int id)
    {
        if (_pen.Contains(id)) _pen.Remove(id);
        else if (!Ranch.Record(id).Rested) { Flash($"{Ranch.Get(id).Species.Name} is resting. Fight a battle and everyone can breed again."); return; }
        else if (_pen.Count >= 2) { Flash("Both vats are full: right-click one of them to take it out."); return; }
        else _pen.Add(id);
        ChildShown = null;
        _notes.Text = _pen.Count == 2 ? "Press Enter to breed." : "";
        Rebuild();
    }

    void Breed()
    {
        if (_pen.Count < 2) { Flash("Put two rested creatures in the vats first (right-click)."); return; }
        var a = Ranch.Get(_pen[0]);
        var b = Ranch.Get(_pen[1]);
        var (id, result) = Ranch.Breed(_pen[0], _pen[1]);
        _pen.Clear();
        ChildShown = Ranch.Get(id);
        Ranch.Save();
        var lines = new List<string> { $"{ChildShown.Species.Name} is born (generation {ChildShown.Generation})." };
        if (result.FromA.Count > 0) lines.Add($"From {a.Species.Name}: {string.Join(", ", result.FromA)}");
        if (result.FromB.Count > 0) lines.Add($"From {b.Species.Name}: {string.Join(", ", result.FromB)}");
        if (result.Notes.Count > 0) lines.Add(string.Join("; ", result.Notes));
        _notes.Text = string.Join("\n", lines);
        Flash($"{a.Species.Name} and {b.Species.Name} rest until the next battle.");
        Rebuild();
        ShowDetail(id);
    }

    void ShowDetail(int id)
    {
        var c = Ranch.Get(id);
        var u = Unit.From(c, Tactics.Side.Player);
        string lineage = c.Generation > 0 ? $"\nBred: generation {c.Generation}, of {c.ParentA} and {c.ParentB}" : "";
        _detail.Text = CreatureSheet.Describe(c) + lineage
            + $"\nHP {u.MaxHp}   ATK {u.Atk}   DEF {u.Def}   SPD {u.Spd}   MOVE {u.Move}"
            + (Ranch.Record(id).Rested ? "" : "\nResting: can breed again after the next battle.");
    }

    public override void _Process(double delta)
    {
        if (_releaseArmed == null) return;
        _releaseTimer -= delta;
        if (_releaseTimer <= 0) { _releaseArmed = null; Flash(""); }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Enter:
            case Key.KpEnter:
                Breed();
                break;
            case Key.F:
                if (Ranch.Squad.Count == 0) { Flash("Pick at least one creature for the squad (left-click)."); return; }
                Ranch.Save();
                Fight?.Invoke();
                break;
            case Key.Delete when _hover is { } id:
                if (Ranch.Creatures.Count <= 1) { Flash("You can't release your last creature."); return; }
                if (_releaseArmed != id)
                {
                    _releaseArmed = id;
                    _releaseTimer = 3;
                    Flash($"Press Delete again to release {Ranch.Get(id).Species.Name} back to the wild.");
                    return;
                }
                Flash($"{Ranch.Get(id).Species.Name} returns to the wild.");
                _releaseArmed = null;
                _pen.Remove(id);
                Ranch.Release(id);
                Ranch.Save();
                _hover = null;
                Rebuild();
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }
}

/// <summary>A creature on the roster, with its squad, vat and resting marks.</summary>
public partial class RanchCard : Control
{
    public RanchScreen Screen;
    public int Id;
    public event Action<bool> Clicked;
    public event Action Hovered;

    Texture2D _tex;
    bool _hover;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(176, 200);
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        _tex = ImageTexture.CreateFromImage(CreaturePainter.Paint(Screen.Ranch.Get(Id), false));
        MouseEntered += () => { _hover = true; QueueRedraw(); Hovered?.Invoke(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
            Clicked?.Invoke(mb.ButtonIndex == MouseButton.Right);
    }

    public override void _Draw()
    {
        var c = Screen.Ranch.Get(Id);
        var rec = Screen.Ranch.Record(Id);
        bool squad = Screen.InSquad(Id);
        int pen = Screen.PenSlot(Id);
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.08f, 0.16f, 0.92f),
            BorderColor = pen >= 0 ? Color.FromHtml("#ff4f9a") : squad ? Color.FromHtml("#4fe3ff") : _hover ? new Color(0.6f, 0.55f, 0.75f) : new Color(0.24f, 0.2f, 0.33f),
        };
        box.SetBorderWidthAll(squad || pen >= 0 ? 3 : _hover ? 2 : 1);
        box.SetCornerRadiusAll(8);
        DrawStyleBox(box, new Rect2(Vector2.Zero, Size));

        int z = _tex.GetWidth() * 2 <= Size.X - 12 && _tex.GetHeight() * 2 <= 112 ? 2 : 1;
        var ts = _tex.GetSize() * z;
        float ground = 136;
        DrawSetTransform(new Vector2(Size.X / 2, ground - 2), 0, new Vector2(1, 0.22f));
        DrawCircle(Vector2.Zero, ts.X * 0.42f, new Color(0, 0, 0, 0.4f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var modulate = rec.Rested ? Colors.White : new Color(0.65f, 0.62f, 0.75f);
        DrawTextureRect(_tex, new Rect2(new Vector2(Mathf.Round(Size.X / 2 - ts.X / 2), ground - ts.Y), ts), false, modulate);

        var font = GetThemeDefaultFont();
        DrawString(font, new Vector2(0, ground + 24), c.Species.Name, HorizontalAlignment.Center, Size.X, 17, Color.FromHtml("#e8e2f4"));
        DrawString(font, new Vector2(0, ground + 44), $"{c.Size} {c.Species.Plan}", HorizontalAlignment.Center, Size.X, 13, Color.FromHtml("#9a91b4"));

        // Marks in the top corners.
        if (squad) DrawString(font, new Vector2(8, 20), "SQUAD", HorizontalAlignment.Left, -1, 13, Color.FromHtml("#4fe3ff"));
        if (pen >= 0) DrawString(font, new Vector2(8, squad ? 38 : 20), pen == 0 ? "VAT A" : "VAT B", HorizontalAlignment.Left, -1, 13, Color.FromHtml("#ff4f9a"));
        string tag = Screen.IsNew(Id) ? "NEW" : c.Generation > 0 ? $"GEN {c.Generation}" : "";
        if (tag != "") DrawString(font, new Vector2(0, 20), tag, HorizontalAlignment.Right, Size.X - 8, 13, Color.FromHtml("#f2c14e"));
        if (!rec.Rested) DrawString(font, new Vector2(0, 38), "resting", HorizontalAlignment.Right, Size.X - 8, 12, Color.FromHtml("#9a91b4"));
    }
}

/// <summary>Three glass vats of glowing chem: parent A, parent B, and the newborn.</summary>
public partial class VatPanel : Control
{
    public RanchScreen Screen;
    readonly Dictionary<Creature, Texture2D> _textures = new();

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var pen = Screen.Pen;
        Creature A = pen.Count > 0 ? Screen.Ranch.Get(pen[0]) : null;
        Creature B = pen.Count > 1 ? Screen.Ranch.Get(pen[1]) : null;
        Vat(new Rect2(0, 30, 220, 330), "VAT A", A, Color.FromHtml("#ff4f9a"));
        Vat(new Rect2(250, 30, 220, 330), "VAT B", B, Color.FromHtml("#ff4f9a"));
        Vat(new Rect2(500, 30, 220, 330), "OFFSPRING", Screen.ChildShown, Color.FromHtml("#4fe3ff"));
        DrawString(font, new Vector2(226, 200), "+", HorizontalAlignment.Left, -1, 28, Color.FromHtml("#e8e2f4"));
        DrawString(font, new Vector2(474, 200), "=", HorizontalAlignment.Left, -1, 28, Color.FromHtml("#e8e2f4"));
    }

    void Vat(Rect2 r, string label, Creature c, Color glow)
    {
        var font = GetThemeDefaultFont();
        var brass = Color.FromHtml("#b2873a");
        DrawString(font, r.Position + new Vector2(0, -8), label, HorizontalAlignment.Center, r.Size.X, 15, glow);
        // Glass, chem, brass caps.
        var glass = new Rect2(r.Position + new Vector2(14, 24), r.Size - new Vector2(28, 48));
        DrawRect(glass, new Color(0.08f, 0.1f, 0.14f, 0.85f));
        float level = c != null ? 0.55f : 0.25f;
        var chem = new Rect2(glass.Position.X, glass.End.Y - glass.Size.Y * level, glass.Size.X, glass.Size.Y * level);
        DrawRect(chem, new Color(glow, 0.22f));
        DrawRect(new Rect2(chem.Position, new Vector2(chem.Size.X, 2)), new Color(glow, 0.7f));
        for (int i = 0; i < 6; i++)
        {
            float t = (float)((Time.GetTicksMsec() / 1000.0 * (0.3 + i * 0.07) + i * 0.37) % 1.0);
            var p = new Vector2(glass.Position.X + 20 + i * 27 % (glass.Size.X - 30), chem.End.Y - t * chem.Size.Y);
            DrawCircle(p, 2 + i % 2, new Color(glow, 0.5f * (1 - t)));
        }
        DrawRect(glass, new Color(0.7f, 0.85f, 1f, 0.35f), false, 2);
        DrawRect(new Rect2(r.Position + new Vector2(4, 8), new Vector2(r.Size.X - 8, 18)), brass);
        DrawRect(new Rect2(new Vector2(r.Position.X + 4, r.End.Y - 26), new Vector2(r.Size.X - 8, 18)), brass);
        for (int i = 0; i < 5; i++)
        {
            DrawCircle(new Vector2(r.Position.X + 20 + i * (r.Size.X - 40) / 4, r.Position.Y + 17), 2, Color.FromHtml("#f0cf72"));
            DrawCircle(new Vector2(r.Position.X + 20 + i * (r.Size.X - 40) / 4, r.End.Y - 17), 2, Color.FromHtml("#f0cf72"));
        }
        if (c == null) return;
        if (!_textures.TryGetValue(c, out var tex)) _textures[c] = tex = ImageTexture.CreateFromImage(CreaturePainter.Paint(c, false));
        int z = tex.GetWidth() * 2 <= glass.Size.X - 10 && tex.GetHeight() * 2 <= glass.Size.Y - 40 ? 2 : 1;
        var ts = tex.GetSize() * z;
        var pos = new Vector2(glass.Position.X + glass.Size.X / 2 - ts.X / 2, glass.End.Y - 20 - ts.Y);
        DrawTextureRect(tex, new Rect2(pos.Round(), ts), false);
        DrawString(font, new Vector2(r.Position.X, r.End.Y + 20), c.Species.Name, HorizontalAlignment.Center, r.Size.X, 17, Color.FromHtml("#e8e2f4"));
    }

    public override void _Process(double delta) => QueueRedraw();
}
