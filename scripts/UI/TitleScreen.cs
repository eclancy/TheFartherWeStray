using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Ranching;
using TheFartherWeStray.Story;
using TheFartherWeStray.Tactics;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The title (the user's pick, 2026-11-02): the home base drifting by behind the name, creatures
/// wandering it, and Continue, New game, Load, Settings and Quit. New game and Load open the three
/// save slots, each with where that game stands; starting over a used slot or deleting one asks twice.
/// </summary>
public partial class TitleScreen : Control
{
    public Action<int, bool> Start;  // a slot, and whether it's a new game there
    public string Open;              // --title=new|load|settings: open on that panel (screenshots)

    const int Scale = 3;
    BattleMap _map;
    IsoArt _art;
    Node2D _board;
    Vector2 _home;
    double _t;
    CanvasLayer _ui;
    VBoxContainer _menu;
    Control _panel;
    Label _hint;
    readonly List<SceneActor> _herd = new();
    readonly Dictionary<SceneActor, double> _next = new();
    readonly SeededRng _rng = new(0x717E);
    (int slot, string what)? _armed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Audio.Music.Play("map");
        // A base with everything built, to show off (never saved).
        var show = Ranch.New();
        show.ContractsWon = 5;
        show.Story.Add("main_foundry");
        show.Encounter = 12;
        _map = HomeBase.Map(show);
        _art = new IsoArt(_map, Tactics.BattleScreen.Skirt);
        var bg = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, 11, "Ranch")),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        _board = new Node2D { Scale = new Vector2(Scale, Scale), TextureFilter = TextureFilterEnum.Nearest };
        AddChild(_board);
        _board.AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(_art.PaintBoard()), Centered = false });
        PropArt.Place(_board, _map, _art);
        var yard = new[] { new Vector2I(8, 8), new Vector2I(9, 7), new Vector2I(7, 9), new Vector2I(10, 9) };
        foreach (var (creature, i) in Encounter.StarterSquad().Select((x, i) => (x.Item1, i)).Take(yard.Length))
        {
            var a = new SceneActor { Id = creature.Name };
            a.Setup(FeetAt, (v, ph, bl, po) => CreatureSculptor.Render(creature, false, v, ph, bl, po), creature.Seed, 1, Unit.From(creature, Tactics.Side.Player).MoveType, false);
            _board.AddChild(a);
            a.Place(yard[i]);
            a.Face(new Vector2(_rng.Range(-1f, 1f), _rng.Range(-1f, 1f)));
            _herd.Add(a);
            _next[a] = _rng.Range(0.5f, 2.5f);
        }
        var vp = GetViewportRect().Size;
        _home = new Vector2(vp.X * 0.72f, vp.Y / 2) - _art.TileCenter(new Vector2(8, 8), 0) * Scale;
        _board.Position = _home;

        // A wash over the left, where the name and the menu stand.
        var wash = new TextureRect { Texture = Wash(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        wash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(wash);

        _ui = new CanvasLayer { Layer = 2 };
        AddChild(_ui);
        // Laid out for a desktop's 1080 rows; a phone's 720 pulls everything up.
        _short = vp.Y < 900;
        var logo = Ui.Text(GameRoot.Title.ToUpperInvariant(), 80, Ui.Gold, 14);
        logo.Position = new Vector2(Left - 10, _short ? 40 : 150);
        _ui.AddChild(logo);
        var tag = Ui.Text("breed  ·  train  ·  lead", 24, Ui.Dim, 6);
        tag.Position = new Vector2(Left - 2, _short ? 146 : 262);
        _ui.AddChild(tag);
        _menu = new VBoxContainer { Position = new Vector2(Left, _short ? 230 : 380) };
        _menu.AddThemeConstantOverride("separation", 14);
        _ui.AddChild(_menu);
        _hint = Ui.Text("", 18, Ui.Gold, 5);
        _hint.Position = new Vector2(Left, vp.Y - 80);
        _ui.AddChild(_hint);
        if (!Phone.On)
        {
            var keys = Ui.Text("Arrows and Enter, or click    N: sound", 16, Ui.Dim, 4);
            keys.Position = new Vector2(vp.X - 420, vp.Y - 40);
            _ui.AddChild(keys);
        }
        ShowMenu();
        switch (Open)
        {
            case "new": ShowSlots(true); break;
            case "load": ShowSlots(false); break;
            case "settings": ShowSettings(); break;
        }

        // The version, small in the corner; and a downloaded copy's update check (docs/releasing.md):
        // Windows updates itself over the title before it starts, Android offers the new APK in a strip.
        var version = Ui.Text("v" + UpdateScreen.CurrentVersion, 14, Ui.Dim, 4);
        version.Position = new Vector2(16, vp.Y - 30);
        _ui.AddChild(version);
        if (UpdateScreen.ShouldCheck())
        {
            var over = new CanvasLayer { Layer = 50 };
            AddChild(over);
            over.AddChild(new UpdateScreen { Finished = over.QueueFree });
        }
        UpdateBanner.AttachTo(this);
    }

    bool _short;
    float Left => _short ? 60 : 120;
    float PanelTop => _short ? 190 : 330;

    Vector2 FeetAt(Vector2I tile, int size) => _art.TileCenter(new Vector2(tile.X, tile.Y), _map.HeightAt(tile));

    /// <summary>A dark wash from the left edge, fading out by the middle of the screen.</summary>
    static Texture2D Wash()
    {
        var img = Image.CreateEmpty(64, 4, false, Image.Format.Rgba8);
        for (int x = 0; x < 64; x++)
        for (int y = 0; y < 4; y++)
            img.SetPixel(x, y, new Color(0.03f, 0.02f, 0.07f, Mathf.Clamp(1.25f - x / 30f, 0, 0.94f)));
        return ImageTexture.CreateFromImage(img);
    }

    static int? Latest() => Enumerable.Range(1, Ranch.Slots).Where(Ranch.SlotUsed).OrderByDescending(s => Ranch.SlotSaved(s)).Cast<int?>().FirstOrDefault();

    Button MenuButton(string text, Action pressed, bool enabled = true)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(380, 62), Disabled = !enabled, Alignment = HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size", 26);
        b.Pressed += pressed;
        _menu.AddChild(b);
        return b;
    }

    void ShowMenu()
    {
        ClosePanel();
        foreach (var child in _menu.GetChildren()) child.QueueFree();
        _menu.Visible = true;
        bool any = Latest() != null;
        int resume = Ranch.SlotUsed(Settings.LastSlot) ? Settings.LastSlot : Latest() ?? 1;
        var first = any ? MenuButton($"   Continue   (slot {resume})", () => Start?.Invoke(resume, false)) : null;
        var fresh = MenuButton("   New game", () => ShowSlots(true));
        MenuButton("   Load", () => ShowSlots(false), any);
        MenuButton("   Settings", ShowSettings);
        MenuButton("   Quit", () => GetTree().Quit());
        (first ?? fresh).CallDeferred(Control.MethodName.GrabFocus);
        _hint.Text = "";
    }

    void ClosePanel()
    {
        _panel?.QueueFree();
        _panel = null;
        _armed = null;
    }

    void ShowSettings()
    {
        ClosePanel();
        _menu.Visible = false;
        var settings = new SettingsPanel { Position = new Vector2(Left, PanelTop) };
        settings.Closed = ShowMenu;
        _ui.AddChild(settings);
        _panel = settings;
    }

    /// <summary>
    /// The three save slots. Starting a new game: an empty slot starts at once; a used one asks again
    /// first. Loading: used slots load. Delete asks again too.
    /// </summary>
    void ShowSlots(bool fresh)
    {
        ClosePanel();
        _menu.Visible = false;
        var box = new PanelContainer { Position = new Vector2(Left, PanelTop) };
        box.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        _ui.AddChild(box);
        _panel = box;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        box.AddChild(col);
        col.AddChild(Ui.Text(fresh ? "NEW GAME: PICK A SLOT" : "LOAD A GAME", 24, Ui.Gold, 6));
        Button firstUsable = null;
        for (int slot = 1; slot <= Ranch.Slots; slot++)
        {
            int s = slot;
            var ranch = Ranch.Peek(s);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            col.AddChild(row);
            string text = ranch == null ? $"Slot {s}    (empty)"
                : $"Slot {s}    {ranch.Summary()}\n           saved {Ranch.SlotSaved(s):yyyy-MM-dd HH:mm}";
            float wide = Mathf.Min(940, GetViewportRect().Size.X - Left * 2 - 180);
            var pick = new Button { Text = text, CustomMinimumSize = new Vector2(wide, 84), Alignment = HorizontalAlignment.Left, Disabled = !fresh && ranch == null };
            pick.AddThemeFontSizeOverride("font_size", 19);
            pick.Pressed += () =>
            {
                if (!fresh) { Start?.Invoke(s, false); return; }
                if (ranch == null || _armed == (s, "overwrite")) { Start?.Invoke(s, true); return; }
                _armed = (s, "overwrite");
                _hint.Text = $"Slot {s} has a game in it. Press it again to start over there; that game will be gone.";
                Audio.Sound.Play("cancel");
            };
            row.AddChild(pick);
            firstUsable ??= pick.Disabled ? null : pick;
            if (ranch != null)
            {
                var del = new Button { Text = "Delete", CustomMinimumSize = new Vector2(110, 84) };
                del.AddThemeFontSizeOverride("font_size", 17);
                del.Pressed += () =>
                {
                    if (_armed == (s, "delete"))
                    {
                        Ranch.DeleteSlot(s);
                        _hint.Text = $"Slot {s} deleted.";
                        ShowSlots(fresh);
                        return;
                    }
                    _armed = (s, "delete");
                    _hint.Text = $"Press Delete again to delete slot {s} for good.";
                    Audio.Sound.Play("cancel");
                };
                row.AddChild(del);
            }
        }
        var back = new Button { Text = "Back", CustomMinimumSize = new Vector2(200, 50) };
        back.AddThemeFontSizeOverride("font_size", 19);
        back.Pressed += ShowMenu;
        col.AddChild(back);
        (firstUsable ?? back).CallDeferred(Control.MethodName.GrabFocus);
        _hint.Text = fresh ? "A new game starts at your home base with four beasts." : "";
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _panel != null)
        {
            ShowMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _t += delta;
        // The base drifts slowly by.
        _board.Position = _home + new Vector2(Mathf.Sin((float)_t * 0.07f) * 140, Mathf.Sin((float)_t * 0.05f + 1) * 60);
        foreach (var a in _herd)
        {
            if (_t < _next[a]) continue;
            _next[a] = _t + _rng.Range(1.5f, 4f);
            var dirs = new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up };
            var to = a.Tile + dirs[_rng.Int(4)];
            if (_map.InBounds(to) && !_map.Blocked(to) && _map.TerrainAt(to) != Terrain.Roof && _herd.All(o => o.Tile != to)
                && Math.Abs(to.X - 8) + Math.Abs(to.Y - 8) <= 3)
                _ = a.Walk(new List<Vector2I> { a.Tile, to }, 0.4f);
        }
    }
}
