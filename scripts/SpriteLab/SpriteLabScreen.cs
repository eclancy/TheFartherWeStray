using System;
using System.Collections.Generic;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.SpriteLab;

/// <summary>
/// The sprite generator prototype: a sheet of seeded creatures to judge for charm. Each card is a
/// different species; click one to see individuals of that species side by side.
/// </summary>
public partial class SpriteLabScreen : Control
{
    public ulong SheetSeed = 1;
    public Species SpeciesView; // null: the sheet; set: individuals of this species
    public bool DrawPets;       // show the DrawPets cast instead of a generated sheet
    public bool FourColor;
    public int Zoom = 2;

    Label _header, _detail;
    GridContainer _grid;
    string _flash;
    double _flashLeft;

    int Columns => Zoom == 2 ? 8 : 5;
    int Count => Zoom == 2 ? 24 : 10;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        var bg = new ColorRect { Color = new Color(0.957f, 0.937f, 0.886f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 24);
        AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        margin.AddChild(col);

        _header = new Label();
        _header.AddThemeFontSizeOverride("font_size", 22);
        _header.AddThemeColorOverride("font_color", new Color(0.22f, 0.2f, 0.27f));
        col.AddChild(_header);

        var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(center);
        _grid = new GridContainer();
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        center.AddChild(_grid);

        _detail = new Label { CustomMinimumSize = new Vector2(0, 118) };
        _detail.AddThemeFontSizeOverride("font_size", 17);
        _detail.AddThemeColorOverride("font_color", new Color(0.25f, 0.23f, 0.3f));
        col.AddChild(_detail);

        Rebuild();
    }

    void Rebuild()
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        _grid.Columns = Columns;
        var creatures = Creatures(Count);
        var cardSize = Zoom == 2 ? new Vector2(226, 246) : new Vector2(360, 340);
        for (int i = 0; i < creatures.Count; i++)
        {
            var card = new CreatureCard();
            card.Setup(creatures[i], i, FourColor, Zoom, cardSize);
            var c = creatures[i];
            card.Hovered += () => _detail.Text = CreatureSheet.Describe(c);
            card.Clicked += () => { if (SpeciesView == null) { SpeciesView = c.Species; Rebuild(); } };
            _grid.AddChild(card);
        }
        _detail.Text = "Hover a creature to see its parts, attitude and what they mean in battle.";
        UpdateHeader();
    }

    List<Creature> Creatures(int count) =>
        SpeciesView != null ? CreatureSheet.Siblings(SpeciesView, count)
        : DrawPets ? CreatureSheet.DrawPetsSheet(count)
        : CreatureSheet.SpeciesSheet(SheetSeed, count);

    void UpdateHeader()
    {
        string sheet = DrawPets ? "the DrawPets cast" : $"sheet {SheetSeed}";
        string where = SpeciesView != null
            ? $"Individuals of {SpeciesView.Name}   [Esc] back to {sheet}"
            : $"{(DrawPets ? "DrawPets cast" : $"Sheet {SheetSeed}")}   [Space] reroll   [Left/Right] prev/next   [D] DrawPets   [Click] see the species";
        string mode = FourColor ? "4-colour (GBC)" : "full palette (GBA)";
        _header.Text = $"Creature Tactics  -  Sprite Lab      {where}   [C] {mode}   [Z] zoom   [P] save PNG"
            + (_flash != null ? $"      {_flash}" : "");
    }

    public override void _Process(double delta)
    {
        if (_flash == null) return;
        _flashLeft -= delta;
        if (_flashLeft <= 0) { _flash = null; UpdateHeader(); }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && SpeciesView != null)
        {
            SpeciesView = null;
            Rebuild();
            return;
        }
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.Space:
            case Key.R:
                SheetSeed = (ulong)Random.Shared.Next(1, 1_000_000);
                SpeciesView = null;
                DrawPets = false;
                break;
            case Key.Right: SheetSeed++; SpeciesView = null; DrawPets = false; break;
            case Key.Left: SheetSeed = Math.Max(1, SheetSeed - 1); SpeciesView = null; DrawPets = false; break;
            case Key.D: DrawPets = !DrawPets; SpeciesView = null; break;
            case Key.Escape:
            case Key.Backspace:
                if (SpeciesView == null) return;
                SpeciesView = null;
                break;
            case Key.C: FourColor = !FourColor; break;
            case Key.Z: Zoom = Zoom == 2 ? 3 : 2; break;
            case Key.P: SavePng(); return;
            case Key.F11:
                DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
                    ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
                return;
            default: return;
        }
        GetViewport().SetInputAsHandled();
        Rebuild();
    }

    void SavePng()
    {
        string dir = ProjectSettings.GlobalizePath("res://.shots");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        string name = SpeciesView != null ? $"species_{SpeciesView.Name}" : DrawPets ? "drawpets" : $"sheet_{SheetSeed}";
        string path = $"{dir}/{name}{(FourColor ? "_4col" : "")}.png";
        CreatureSheet.Compose(Creatures(24), FourColor, 3, 8).SavePng(path);
        _flash = $"saved {path}";
        _flashLeft = 4;
        UpdateHeader();
    }
}

/// <summary>One creature on the sheet, standing on its shadow, with its name and the basics under it.</summary>
public partial class CreatureCard : Control
{
    public event Action Hovered, Clicked;

    Creature _creature;
    Texture2D _tex;
    int _index, _zoom;
    bool _hover;

    public void Setup(Creature creature, int index, bool fourColor, int zoom, Vector2 size)
    {
        _creature = creature;
        _index = index;
        _zoom = zoom;
        _tex = ImageTexture.CreateFromImage(CreaturePainter.Paint(creature, fourColor));
        CustomMinimumSize = size;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "";
        MouseEntered += () => { _hover = true; QueueRedraw(); Hovered?.Invoke(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Clicked?.Invoke();
    }

    public override void _Draw()
    {
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.988f, 0.976f, 0.945f),
            BorderColor = _hover ? new Color(0.45f, 0.4f, 0.6f) : new Color(0.88f, 0.85f, 0.78f),
        };
        box.SetBorderWidthAll(_hover ? 3 : 2);
        box.SetCornerRadiusAll(10);
        DrawStyleBox(box, new Rect2(Vector2.Zero, Size));

        const float textH = 58;
        float ground = Size.Y - textH - 4;
        // The rare creature too big for its card at this zoom drops a step rather than spill over.
        int z = _zoom;
        while (z > 1 && (_tex.GetWidth() * z > Size.X - 12 || _tex.GetHeight() * z > ground - 8)) z--;
        var ts = _tex.GetSize() * z;
        var center = new Vector2(Size.X / 2, ground - _zoom);
        DrawSetTransform(center, 0, new Vector2(1, 0.22f));
        DrawCircle(Vector2.Zero, ts.X * 0.42f, new Color(0.2f, 0.18f, 0.25f, 0.16f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawTextureRect(_tex, new Rect2(new Vector2(Mathf.Round(Size.X / 2 - ts.X / 2), ground - ts.Y), ts), false);

        var font = GetThemeDefaultFont();
        var ink = new Color(0.22f, 0.2f, 0.27f);
        var soft = new Color(0.45f, 0.42f, 0.5f);
        string name = _creature.Species.Name + (_creature.Shiny ? " *" : "");
        DrawString(font, new Vector2(0, ground + 24), name, HorizontalAlignment.Center, Size.X, 19, _creature.Shiny ? new Color(0.75f, 0.5f, 0.05f) : ink);
        string line = $"{_creature.Size} {_creature.Species.Plan} · {_creature.Attitude}";
        DrawString(font, new Vector2(0, ground + 44), line, HorizontalAlignment.Center, Size.X, 14, soft);
        if (_creature.Mutation != null)
            DrawString(font, new Vector2(0, ground + 59), _creature.Mutation, HorizontalAlignment.Center, Size.X, 13, new Color(0.6f, 0.25f, 0.5f));
        DrawString(font, new Vector2(10, 22), $"#{_index + 1}", HorizontalAlignment.Left, -1, 13, soft);
    }
}
