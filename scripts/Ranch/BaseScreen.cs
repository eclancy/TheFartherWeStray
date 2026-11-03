using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Story;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// Home: the base you walk around (the user's direction, 2026-11-02). You're on the board as a person;
/// click a tile to walk there (or step with WASD / the arrows), and walk up to a station and press E
/// (or click it) to use it: the pens, the vats, the lab, the training yard, the armory, the war table,
/// the gate out to the country. Your creatures wander the pens. Stations not yet built stand as
/// scaffolding with a sign saying what opens them; new ones are marked until first used.
/// </summary>
public partial class BaseScreen : Control
{
    public Ranch Ranch;
    public Action<string> Use;          // a station's id ("gate" means the country map): the host opens it
    public Action<Mission> Fight;       // the attract loop's battles
    public List<int> JustJoined = new(); // recruits from the last battle, to point out
    public bool AutoFight;               // --autoplay: head out on its own
    public string DemoGoTo;              // --goto=ID: walk to that station and use it (checking the walk and the stations)
    public Action ToTitle;               // the pause menu's way back to the title
    public bool DemoPause;               // --pause: open on the pause menu (screenshots)
    public static Vector2I? Standing;    // where you were, for coming back from a station

    const int Scale = 3;
    static readonly Vector2 Focus = new(960, 500);
    static readonly Color Gold = Ui.Gold, Ink = Ui.Ink, Dim = Ui.Dim;

    BattleMap _map;
    IsoArt _art;
    Node2D _board;
    SceneActor _you;
    readonly List<SceneActor> _herd = new();
    readonly Dictionary<StationDef, Label> _tags = new();
    Label _status, _notes, _prompt;
    bool _walking;
    readonly SeededRng _rng = new(0xBA5E); // the herd's wandering: show only
    readonly Dictionary<SceneActor, double> _nextWander = new();
    double _clock;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Audio.Music.Play("ranch");
        _map = HomeBase.Map(Ranch);
        _art = new IsoArt(_map, Tactics.BattleScreen.Skirt);
        var bg = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, 7, "Ranch")),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        _board = new Node2D { Scale = new Vector2(Scale, Scale), TextureFilter = TextureFilterEnum.Nearest };
        AddChild(_board);
        _board.AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(_art.PaintBoard()), Centered = false });
        PropArt.Place(_board, _map, _art);

        // You, where you left off (or at the yard's centre), and the herd in the pens.
        var look = People.Look("you");
        _you = new SceneActor { Id = "you", Shown = "You" };
        _you.Setup(FeetAt, (v, ph, bl, po) => PersonSculptor.Render(look, v, ph, bl, po), SeededRng.Hash("you"), 1, MoveType.Walk, true);
        _board.AddChild(_you);
        _you.Place(Standing is { } s && CanStand(s) ? s : HomeBase.Spawn);
        _you.Face(new Vector2(-1, 1));
        var pen = HomeBase.Pen().Where(CanStand).ToList();
        foreach (var rec in Ranch.Creatures.Where(r => !Ranch.Away.ContainsKey(r.Id)).Take(Math.Min(8, pen.Count)))
        {
            var creature = Ranch.Get(rec.Id);
            var unit = Unit.From(creature, Tactics.Side.Player);
            var a = new SceneActor { Id = rec.Id.ToString(), Shown = creature.Name };
            a.Setup(FeetAt, (v, ph, bl, po) => CreatureSculptor.Render(creature, false, v, ph, bl, po), creature.Seed, 1, unit.MoveType, false);
            _board.AddChild(a);
            var spot = pen[_rng.Int(pen.Count)];
            pen.Remove(spot);
            a.Place(spot);
            a.Face(new Vector2(_rng.Range(-1f, 1f), _rng.Range(-1f, 1f)));
            _herd.Add(a);
            _nextWander[a] = _rng.Range(0.5f, 3f);
        }
        _board.Position = Focus - (_you.Position - new Vector2(0, 20)) * Scale;

        BuildUi();
        if (JustJoined.Count > 0) Flash($"{string.Join(", ", JustJoined.Select(id => Ranch.Get(id).Name))} joined the ranch: they're in the pens.");
        RanchScreen.AskChoice(this, Ranch, AutoFight, Flash, Refresh);
        if (AutoFight) GetTree().CreateTimer(1.5).Timeout += () => { if (IsInsideTree()) RanchScreen.AutoBattle(this, Ranch, Fight); };
        if (HomeBase.Get(DemoGoTo) is { } demo) GetTree().CreateTimer(0.6).Timeout += () => { if (IsInsideTree()) GoTo(demo); };
        if (DemoPause) Pause();
    }

    // ------------------------------------------------------------------ the board

    Vector2 FeetAt(Vector2I tile, int size) => _art.TileCenter(new Vector2(tile.X + (size - 1) / 2f, tile.Y + (size - 1) / 2f), _map.InBounds(tile) ? _map.HeightAt(tile) : 0);

    bool CanStand(Vector2I t) => _map.InBounds(t) && !_map.Blocked(t) && _map.TerrainAt(t) is not (Terrain.Pit or Terrain.Water or Terrain.Chem or Terrain.Lava or Terrain.Roof);

    /// <summary>The tile under a point on the screen: the front-most tile whose top face holds it.</summary>
    Vector2I? TileAt(Vector2 screen)
    {
        var p = (screen - _board.Position) / Scale;
        Vector2I? best = null;
        int bestDepth = int.MinValue;
        for (int y = 0; y < _map.H; y++)
        for (int x = 0; x < _map.W; x++)
        {
            var t = new Vector2I(x, y);
            var top = _art.TileTop(t, _map.HeightAt(t));
            float dx = Mathf.Abs(p.X - top.X) / (IsoArt.TW / 2f), dy = Mathf.Abs(p.Y - top.Y - IsoArt.TH / 2f) / (IsoArt.TH / 2f);
            int depth = (x + y) * 10 + _map.HeightAt(t);
            if (dx + dy <= 1 && depth > bestDepth) { best = t; bestDepth = depth; }
        }
        return best;
    }

    /// <summary>A station whose prop stands at this point on the screen (props are tall: their sprite counts, not just the tile).</summary>
    StationDef StationAt(Vector2 screen)
    {
        var p = (screen - _board.Position) / Scale;
        foreach (var s in HomeBase.Stations)
            foreach (var t in HomeBase.Tiles(s))
            {
                var feet = FeetAt(t, 1);
                if (Mathf.Abs(p.X - feet.X) < 14 && p.Y < feet.Y + 6 && p.Y > feet.Y - 34) return s;
            }
        return null;
    }

    /// <summary>The station you're standing next to, if any.</summary>
    StationDef Near()
    {
        foreach (var s in HomeBase.Stations)
            foreach (var t in HomeBase.Tiles(s))
                if (Mathf.Abs(t.X - _you.Tile.X) + Mathf.Abs(t.Y - _you.Tile.Y) == 1) return s;
        return null;
    }

    async void WalkTo(Vector2I to, StationDef then = null)
    {
        if (_walking) return;
        var path = Paths.Find(_map, _you.Tile, to, CanStand, false);
        if (path == null) { Flash("You can't get there from here."); return; }
        _walking = true;
        if (path.Count > 1) await _you.Walk(path, 0.18f);
        _walking = false;
        if (then != null)
        {
            var at = HomeBase.Tiles(then).OrderBy(t => (t - _you.Tile).LengthSquared()).First();
            _you.Face(FeetAt(at, 1) - _you.Position);
            UseStation(then);
        }
    }

    /// <summary>Walks to the nearest spot beside a station, then uses it.</summary>
    void GoTo(StationDef s)
    {
        var beside = HomeBase.Tiles(s).SelectMany(t => new[] { t + Vector2I.Right, t + Vector2I.Left, t + Vector2I.Down, t + Vector2I.Up }).Where(CanStand).Distinct()
            .Select(t => (t, path: Paths.Find(_map, _you.Tile, t, CanStand, false))).Where(x => x.path != null).OrderBy(x => x.path.Count).ToList();
        if (beside.Count == 0) { Flash($"There's no way to {s.Name} from here."); return; }
        WalkTo(beside[0].t, s);
    }

    void UseStation(StationDef s)
    {
        if (!s.Built(Ranch)) { Flash($"{s.Name}: still being built. It opens after {s.Needs}."); Audio.Sound.Play("cancel"); return; }
        if (s.Id == "gate" && Ranch.Squad.Count == 0) { Flash("Pick a squad at the pens before heading out."); Audio.Sound.Play("cancel"); return; }
        Standing = _you.Tile;
        Ranch.Flags.Add("used:" + s.Id);
        Ranch.Save();
        Audio.Sound.Play("confirm");
        Use?.Invoke(s.Id);
    }

    // ------------------------------------------------------------------ the screen

    void BuildUi()
    {
        var ui = new CanvasLayer { Layer = 2 };
        AddChild(ui);
        var title = Ui.Text("HOME BASE", 26, Ink, 6);
        title.Position = new Vector2(32, 20);
        ui.AddChild(title);
        var chips = Ui.Chips(new List<Chip>
        {
            new("paw", Ranch.Creatures.Count.ToString(), Ink, "your beasts"),
            new("fist", $"{Ranch.Squad.Count}/{Ranch.SquadSize}", Ranch.Squad.Count == 0 ? Ui.Bad : Ink, "squad, picked at the pens"),
            new("flask", Ranch.Chem.ToString(), Gold, "chem"),
            new("clock", "day " + Ranch.Day, Ink, "the day"),
            new("sun", Ranch.CityStanding.ToString(), Gold, "standing with the city above"),
            new("moon", Ranch.UnderStanding.ToString(), Color.FromHtml("#ff4f9a"), "standing with the undercity"),
        }, 22, 2, 22);
        chips.Position = new Vector2(250, 20);
        chips.Size = new Vector2(1100, 40);
        ui.AddChild(chips);
        _status = Ui.Text("", 18, Gold, 5);
        _status.Position = new Vector2(32, 64);
        _status.Size = new Vector2(1200, 30);
        ui.AddChild(_status);
        _notes = Ui.Text(Ranch.Report.Count > 0 ? string.Join("\n", Ranch.Report.TakeLast(6)) : "", 15, Ink, 4);
        _notes.Position = new Vector2(1400, 70);
        _notes.Size = new Vector2(490, 300);
        _notes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ui.AddChild(_notes);
        _prompt = Ui.Text("", 22, Ink, 6);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.Position = new Vector2(0, 960);
        _prompt.Size = new Vector2(1920, 40);
        ui.AddChild(_prompt);
        var hint = Ui.Text("Click: walk (or WASD / arrows)    E or click a station: use it    Esc: menu    N: sound", 15, Dim, 4);
        hint.Position = new Vector2(32, 1036);
        ui.AddChild(hint);
        foreach (var s in HomeBase.Stations)
        {
            var tag = Ui.Text("", 19, Ink, 7);
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            tag.Size = new Vector2(320, 28);
            ui.AddChild(tag);
            _tags[s] = tag;
        }
        Refresh();
    }

    void Refresh()
    {
        foreach (var (s, tag) in _tags)
        {
            bool built = s.Built(Ranch);
            tag.Text = !built ? $"{s.Name} (scaffolding)" : HomeBase.Fresh(Ranch, s) ? $"NEW  {s.Name}" : s.Name;
            tag.AddThemeColorOverride("font_color", !built ? Dim : HomeBase.Fresh(Ranch, s) ? Gold : Ink);
        }
    }

    void Flash(string text) => _status.Text = text;

    public override void _Process(double delta)
    {
        _clock += delta;
        // The camera keeps you in view.
        var want = Focus - (_you.Position - new Vector2(0, 20)) * Scale;
        _board.Position = _board.Position.Lerp(want, (float)Math.Min(1, delta * 4));
        foreach (var (s, tag) in _tags)
        {
            var first = HomeBase.Tiles(s).First();
            tag.Position = _board.Position + (FeetAt(first, 1) + new Vector2(0, -38)) * Scale - new Vector2(160, 14);
        }
        var near = _walking ? null : Near();
        _prompt.Text = near == null ? "" : near.Built(Ranch) ? $"[E]  {near.Name}: {near.Blurb}" : $"{near.Name}: being built. Opens after {near.Needs}.";
        _prompt.AddThemeColorOverride("font_color", near != null && near.Built(Ranch) ? Ink : Dim);
        // The herd wanders the pens, a step now and then.
        foreach (var a in _herd)
        {
            if (_clock < _nextWander[a]) continue;
            _nextWander[a] = _clock + _rng.Range(1.5f, 4.5f);
            var dirs = new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up };
            var to = a.Tile + dirs[_rng.Int(4)];
            if (HomeBase.Pen().Contains(to) && CanStand(to) && _herd.All(o => o.Tile != to)) _ = a.Walk(new List<Vector2I> { a.Tile, to }, 0.4f);
        }
    }

    // ------------------------------------------------------------------ the pause menu

    CanvasLayer _pause;

    /// <summary>Esc at the base: resume, the settings, save and go back to the title, or quit.</summary>
    void Pause()
    {
        if (_pause != null) return;
        _pause = new CanvasLayer { Layer = 4 };
        AddChild(_pause);
        var shade = new ColorRect { Color = new Color(0.02f, 0.01f, 0.05f, 0.7f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pause.AddChild(shade);
        var col = new VBoxContainer { Position = new Vector2(760, 330) };
        col.AddThemeConstantOverride("separation", 14);
        _pause.AddChild(col);
        col.AddChild(Ui.Text("PAUSED", 30, Gold, 8));
        Button Item(string text, Action pressed)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(400, 58) };
            b.AddThemeFontSizeOverride("font_size", 22);
            b.Pressed += pressed;
            col.AddChild(b);
            return b;
        }
        Item("Resume", Unpause).CallDeferred(Control.MethodName.GrabFocus);
        Item("Settings", () =>
        {
            col.Visible = false;
            var settings = new SettingsPanel { Position = new Vector2(640, 280) };
            settings.Closed = () => { settings.QueueFree(); col.Visible = true; };
            _pause.AddChild(settings);
        });
        Item("Save and return to title", () => { Standing = _you.Tile; Ranch.Save(); ToTitle?.Invoke(); });
        Item("Save and quit", () => { Standing = _you.Tile; Ranch.Save(); GetTree().Quit(); });
    }

    void Unpause()
    {
        _pause?.QueueFree();
        _pause = null;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            if (_pause == null) Pause(); else Unpause();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_pause != null) return;
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            if (StationAt(click.Position) is { } s) GoTo(s);
            else if (TileAt(click.Position) is { } t && CanStand(t)) WalkTo(t);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is not InputEventKey { Pressed: true } k) return;
        Vector2I? step = k.Keycode switch
        {
            Key.W or Key.Up => Vector2I.Up,
            Key.D or Key.Right => Vector2I.Right,
            Key.S or Key.Down => Vector2I.Down,
            Key.A or Key.Left => Vector2I.Left,
            _ => null,
        };
        if (step is { } d)
        {
            if (!_walking && CanStand(_you.Tile + d)) WalkTo(_you.Tile + d);
            else _you.Face(FeetAt(_you.Tile + d, 1) - _you.Position);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!k.Echo && k.Keycode is Key.E or Key.Enter or Key.KpEnter or Key.Space && !_walking && Near() is { } n)
        {
            _you.Face(FeetAt(HomeBase.Tiles(n).OrderBy(t => (t - _you.Tile).LengthSquared()).First(), 1) - _you.Position);
            UseStation(n);
            GetViewport().SetInputAsHandled();
        }
    }
}
