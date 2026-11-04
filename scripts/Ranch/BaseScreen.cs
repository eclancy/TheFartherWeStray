using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Story;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.UI;
using TheFartherWeStray.World;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// Home: the base you walk around (the user's direction, 2026-11-02). You're on the board as a person;
/// click a tile to walk there (or step with WASD / the arrows), and walk up to a station and press E
/// (or click it) to use it: the pens, the vats, the lab, the training yard, the armory, the war table,
/// the gate out to the country. Your creatures wander the pens. Stations not yet built stand as
/// scaffolding with a sign saying what opens them; new ones are marked until first used. The workbench
/// turns the base into build mode right here: a cursor on the board, the shed along the foot of the
/// screen, pieces put out and picked up where you can see them (docs/farmyard-plan.md, part 1).
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
    public string DemoHolding;           // --holding=ID with --goto=bench: build mode with that piece in hand
    public Vector2I? DemoCursor;         // --cursor=X,Y: and the cursor there
    public static Vector2I? Standing;    // where you were, for coming back from a station

    const int Scale = 3;
    Vector2 Focus => GetViewportRect().Size * new Vector2(0.5f, 0.463f);
    static readonly Color Gold = Ui.Gold, Ink = Ui.Ink, Dim = Ui.Dim;

    BattleMap _map;
    IsoArt _art;
    Node2D _board;
    Sprite2D _ground;
    readonly List<Node> _propNodes = new();
    SceneActor _you;
    readonly List<SceneActor> _herd = new();
    readonly Dictionary<StationDef, Label> _tags = new();
    Label _status, _notes, _prompt, _hint;
    bool _walking;
    readonly SeededRng _rng = new(0xBA5E); // the herd's wandering: show only
    readonly Dictionary<SceneActor, double> _nextWander = new();
    double _clock;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Audio.Music.Play("ranch");
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
        _ground = new Sprite2D { Centered = false };
        _board.AddChild(_ground);
        PaintYard();

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

    /// <summary>Paints the yard as it stands (the ground and every prop), again after a piece is put out or picked up.</summary>
    void PaintYard()
    {
        _map = HomeBase.Map(Ranch);
        _art = new IsoArt(_map, Tactics.BattleScreen.Skirt);
        _ground.Texture = ImageTexture.CreateFromImage(_art.PaintBoard());
        foreach (var n in _propNodes) n.QueueFree();
        _propNodes.Clear();
        int before = _board.GetChildCount();
        PropArt.Place(_board, _map, _art);
        for (int i = before; i < _board.GetChildCount(); i++) _propNodes.Add(_board.GetChild(i));
    }

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
        if (s.Id == "bench") { StartBuild(); return; }
        Use?.Invoke(s.Id);
    }

    // ------------------------------------------------------------------ build mode (the workbench)

    bool _building;
    string _holding;            // the piece in hand: put out where the cursor is
    Vector2I _cursor;
    YardCursor _mark;
    Sprite2D _ghost;
    CanvasLayer _shedLayer;
    PanelContainer _shed;
    readonly List<Button> _shedButtons = new();

    /// <summary>The workbench: a cursor on the yard and the shed along the foot of the screen.</summary>
    void StartBuild()
    {
        _building = true;
        _mark = new YardCursor();
        _board.AddChild(_mark);
        _ghost = new Sprite2D { Centered = false, Visible = false };
        _board.AddChild(_ghost);
        _shedLayer = new CanvasLayer { Layer = 3 };
        AddChild(_shedLayer);
        _hint.Visible = false;
        _holding = DemoHolding != null && Ranch.Shed.GetValueOrDefault(DemoHolding) > 0 ? DemoHolding : null;
        DemoHolding = null;
        RefreshShed();
        MoveCursor(DemoCursor ?? _you.Tile + new Vector2I(1, 0));
        DemoCursor = null;
        Flash(Ranch.Shed.Count == 0 && Ranch.Decor.Count == 0
            ? "The shed's empty: the shops sell pieces for the yard."
            : "The workbench: pick something from the shed, then where it goes.");
    }

    void EndBuild()
    {
        _building = false;
        _holding = null;
        _mark.QueueFree();
        _ghost.QueueFree();
        _shedLayer.QueueFree();
        _shed = null;
        _shedButtons.Clear();
        _hint.Visible = true;
        _prompt.Position = new Vector2(20, GetViewportRect().Size.Y - 120);
        Ranch.Save();
        Audio.Sound.Play("cancel");
        Flash($"The yard's as you left it: {Ranch.Decor.Count} {(Ranch.Decor.Count == 1 ? "piece" : "pieces")} out.");
    }

    /// <summary>The shed: a button for every piece in it, the one in hand lit gold, and Done.</summary>
    void RefreshShed()
    {
        _shed?.QueueFree();
        _shedButtons.Clear();
        var vp = GetViewportRect().Size;
        _shed = new PanelContainer();
        _shed.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        _shed.AddChild(col);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        head.AddChild(Ui.Text("THE SHED", 18, Gold));
        head.AddChild(Ui.Chips(new List<Chip>
        {
            new("hand", $"{Ranch.Decor.Count}/{Farmyard.Most} out", Ink, "pieces out in the yard"),
            new("flask", Ranch.Chem.ToString(), Gold, "chem: the shops sell more pieces"),
        }, 16));
        var help = Ui.Text(Phone.Keys("Click a piece, then a tile    Click one in the yard: pick it up    Esc or right-click: back",
            "D-pad: move    A: put down or pick up (on an empty tile: the shed)    B: back"), 14, Dim, 4);
        help.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        help.HorizontalAlignment = HorizontalAlignment.Right;
        head.AddChild(help);
        var done = new Button { Text = Phone.Keys("Done  (Esc)", "Done  (B)"), FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(140, 36) };
        done.Pressed += EndBuild;
        head.AddChild(done);
        col.AddChild(head);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, Phone.On ? 92 : 110), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        scroll.AddChild(row);
        col.AddChild(scroll);
        foreach (var def in DecorBook.All)
        {
            int count = Ranch.Shed.GetValueOrDefault(def.Id);
            if (count == 0) continue;
            var (tex, _) = PropArt.Texture(def.Prop, 1, _map.Theme);
            var b = new Button
            {
                Text = $"{def.Name}  x{count}", Icon = tex, ExpandIcon = true, TooltipText = def.Text,
                IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top,
                CustomMinimumSize = new Vector2(Phone.On ? 120 : 140, Phone.On ? 84 : 100), TextureFilter = TextureFilterEnum.Nearest,
                FocusMode = FocusModeEnum.All,
            };
            if (def.Id == _holding) b.AddThemeStyleboxOverride("normal", UiSkin.Lit(UiPart.Button, Gold, true));
            var id = def.Id;
            b.Pressed += () => Hold(id);
            row.AddChild(b);
            _shedButtons.Add(b);
        }
        if (_shedButtons.Count == 0) row.AddChild(Ui.Text("Nothing in the shed. The shops sell pieces for the yard: barrels and lamps at Lantern Row, finer things up in the city.", 16, Dim, 4));
        float wide = Mathf.Min(vp.X - 48, 1500);
        _shed.Position = new Vector2((vp.X - wide) / 2, vp.Y - (Phone.On ? 160 : 190));
        _shed.Size = new Vector2(wide, 0);
        _shedLayer.AddChild(_shed);
        _prompt.Position = new Vector2(20, _shed.Position.Y - 44);
    }

    /// <summary>Takes a piece in hand from the shed; the cursor goes back to the yard.</summary>
    void Hold(string id)
    {
        _holding = id;
        RefreshShed();
        GetViewport().GuiReleaseFocus();
        MoveCursor(_cursor);
        Flash($"{DecorBook.Get(id).Name}: {Phone.Keys("click a tile", "move to a tile and press A")} to put it there. {Phone.Keys("Esc", "B")} puts it back.");
        Audio.Sound.Play("click");
    }

    void MoveCursor(Vector2I t)
    {
        _cursor = new Vector2I(Mathf.Clamp(t.X, 0, _map.W - 1), Mathf.Clamp(t.Y, 0, _map.H - 1));
        int h = _map.HeightAt(_cursor);
        string why = _holding != null ? Farmyard.Why(Ranch, _holding, _cursor, _you.Tile) : null;
        var piece = Farmyard.At(Ranch, _cursor);
        _mark.Top = _art.TileTop(_cursor, h);
        _mark.Colour = _holding != null ? (why == null ? new Color("#5fe08a") : new Color("#ff6f7d")) : piece != null ? Gold : new Color(1, 1, 1, 0.7f);
        _mark.ZIndex = 100 + (_cursor.X + _cursor.Y) * 4;
        _mark.QueueRedraw();
        _ghost.Visible = _holding != null;
        if (_holding != null)
        {
            var def = DecorBook.Get(_holding);
            var (tex, anchor) = PropArt.Texture(def.Prop, _cursor.X * 7 + _cursor.Y * 3, _map.Theme);
            _ghost.Texture = tex;
            _ghost.Position = _art.TileCenter(_cursor, h) - anchor + new Vector2(0, 1);
            _ghost.ZIndex = 100 + (_cursor.X + _cursor.Y) * 4 + 2;
            _ghost.Modulate = why == null ? new Color(1, 1, 1, 0.8f) : new Color(1, 0.45f, 0.45f, 0.7f);
        }
        _buildPrompt = _holding != null
            ? why ?? $"[{Phone.Keys("click", "A")}]  put the {DecorBook.Get(_holding).Name} here"
            : piece != null ? $"[{Phone.Keys("click", "A")}]  pick up the {DecorBook.Get(piece.Id)?.Name}"
            : Phone.Keys("Pick something from the shed below", "[A]  open the shed");
        _buildPromptOk = _holding == null || why == null;
    }

    string _buildPrompt = "";
    bool _buildPromptOk;

    /// <summary>A or a click on the cursor: put the piece in hand down, or pick up the one there, or (by key) go to the shed.</summary>
    void Act(bool byKey)
    {
        if (_holding != null)
        {
            string name = DecorBook.Get(_holding).Name;
            if (Farmyard.Place(Ranch, _holding, _cursor, _you.Tile) is { } why) { Flash(why); Audio.Sound.Play("cancel"); return; }
            int left = Ranch.Shed.GetValueOrDefault(_holding);
            if (left == 0) _holding = null;
            PaintYard();
            RefreshShed();
            MoveCursor(_cursor);
            Flash($"The {name} is out. {(left > 0 ? $"{left} more in the shed." : "That was the last one in the shed.")}");
            Audio.Sound.Play("confirm");
            return;
        }
        if (Farmyard.PickUp(Ranch, _cursor) is { } id)
        {
            _holding = id;
            PaintYard();
            RefreshShed();
            MoveCursor(_cursor);
            Flash($"Picked up the {DecorBook.Get(id).Name}: put it somewhere else, or {Phone.Keys("Esc", "B")} leaves it in the shed.");
            Audio.Sound.Play("click");
            return;
        }
        if (byKey && _shedButtons.Count > 0) _shedButtons[0].GrabFocus();
        Flash(_shedButtons.Count > 0 ? "Pick something from the shed." : "The shed's empty: the shops sell pieces for the yard.");
    }

    /// <summary>B, Esc or a right-click: back out of the shed, put down what's in hand, or leave the bench.</summary>
    void Back()
    {
        if (GetViewport().GuiGetFocusOwner() is { } f && _shed.IsAncestorOf(f)) { GetViewport().GuiReleaseFocus(); return; }
        if (_holding != null)
        {
            Flash($"The {DecorBook.Get(_holding).Name} stays in the shed.");
            _holding = null;
            RefreshShed();
            MoveCursor(_cursor);
            Audio.Sound.Play("cancel");
            return;
        }
        EndBuild();
    }

    void BuildInput(InputEvent e)
    {
        if (e is InputEventMouseMotion m)
        {
            if (TileAt(m.Position) is { } over && over != _cursor) MoveCursor(over);
            return;
        }
        if (e is InputEventMouseButton { Pressed: true } click)
        {
            if (click.ButtonIndex == MouseButton.Right) Back();
            else if (click.ButtonIndex == MouseButton.Left && TileAt(click.Position) is { } t) { MoveCursor(t); Act(false); }
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
        if (step is { } d) MoveCursor(_cursor + d);
        else if (!k.Echo && k.Keycode is Key.E or Key.Enter or Key.KpEnter or Key.Space) Act(true);
        else if (!k.Echo && k.Keycode == Key.Escape) Back();
        else return;
        GetViewport().SetInputAsHandled();
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
        var vp = GetViewportRect().Size;
        _status.Position = new Vector2(32, 64);
        _status.Size = new Vector2(Mathf.Min(1200, vp.X - 64), 30);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ui.AddChild(_status);
        _notes = Ui.Text(Ranch.Report.Count > 0 ? string.Join("\n", Ranch.Report.TakeLast(Phone.On ? 4 : 6)) : "", 15, Ink, 4);
        _notes.Position = new Vector2(vp.X - (Phone.On ? 400 : 520), Phone.On ? 120 : 70);
        _notes.Size = new Vector2(Phone.On ? 380 : 490, 300);
        _notes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        ui.AddChild(_notes);
        _prompt = Ui.Text("", 22, Ink, 6);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _prompt.Position = new Vector2(20, vp.Y - 120);
        _prompt.Size = new Vector2(vp.X - 40, 40);
        ui.AddChild(_prompt);
        _hint = Ui.Text(HintWords, 15, Dim, 4);
        _hint.Position = new Vector2(32, vp.Y - 44);
        ui.AddChild(_hint);
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

    static string HintWords => Phone.Keys("Click: walk (or WASD / arrows)    E or click a station: use it    Esc: menu    N: sound",
        "D-pad or tap: walk    A or tap a station: use it    Start: menu");

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
        // The camera keeps you in view (at the workbench, the cursor).
        var want = Focus - ((_building ? FeetAt(_cursor, 1) : _you.Position) - new Vector2(0, 20)) * Scale;
        _board.Position = _board.Position.Lerp(want, (float)Math.Min(1, delta * 4));
        foreach (var (s, tag) in _tags)
        {
            var first = HomeBase.Tiles(s).First();
            tag.Position = _board.Position + (FeetAt(first, 1) + new Vector2(0, -38)) * Scale - new Vector2(160, 14);
        }
        if (_building)
        {
            _prompt.Text = _buildPrompt;
            _prompt.AddThemeColorOverride("font_color", _buildPromptOk ? Ink : new Color("#ff8a96"));
        }
        else
        {
            var near = _walking ? null : Near();
            _prompt.Text = near == null ? "" : near.Built(Ranch) ? $"[{Phone.Keys("E", "A")}]  {near.Name}: {near.Blurb}" : $"{near.Name}: being built. Opens after {near.Needs}.";
            _prompt.AddThemeColorOverride("font_color", near != null && near.Built(Ranch) ? Ink : Dim);
        }
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

    PauseMenu _pause;

    /// <summary>Esc at the base (Start on a phone): resume, the settings, save and go back to the title, or quit.</summary>
    void Pause()
    {
        if (_pause != null) return;
        _pause = new PauseMenu { Save = () => { Standing = _you.Tile; Ranch.Save(); }, ToTitle = ToTitle, Closed = () => _pause = null };
        AddChild(_pause);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (Ranch.Pending != null && !AutoFight) return; // the choice after a main mission stands over everything until it's made
        // Esc opens the menu on a desktop; on a phone Start does (B is cancel, and there's nothing here to cancel).
        if (_pause == null && e is InputEventKey { Pressed: true, Echo: false } open && (open.Keycode == Phone.Start || open.Keycode == Key.Escape && !Phone.On && !_building))
        {
            Pause();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_pause != null) return;
        if (_building) { BuildInput(e); return; }
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

/// <summary>The workbench's cursor: the tile it's on, outlined and washed in a colour (green: it fits; red: it doesn't).</summary>
public partial class YardCursor : Node2D
{
    public Vector2 Top;   // the tile's top corner on the board
    public Color Colour = Colors.White;

    public override void _Draw()
    {
        float w = IsoArt.TW / 2f, h = IsoArt.TH / 2f;
        var pts = new[] { Top, Top + new Vector2(w, h), Top + new Vector2(0, 2 * h), Top + new Vector2(-w, h) };
        DrawColoredPolygon(pts, new Color(Colour, 0.28f));
        DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Colour, 1);
    }
}
