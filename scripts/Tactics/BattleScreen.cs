using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Tactics;

/// <summary>
/// A battle on an isometric board. On your creature's turn: click a blue tile to move, a marked
/// enemy to attack, B to set down a glowberry, Esc to undo the move, Space to end the turn. Enemy
/// turns play out quickly; the rules live in <see cref="Battle"/>, this only shows them.
/// </summary>
public partial class BattleScreen : Control
{
    public ulong Seed = 1;
    public bool AutoPlay;   // the AI plays your side too (demos and screenshots)

    const int Scale = 2;
    static readonly Color Cyan = Color.FromHtml("#4fe3ff"), Magenta = Color.FromHtml("#ff4f9a"), Gold = Color.FromHtml("#f2c14e");
    static readonly Color Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#9a91b4");

    Battle _battle;
    IsoArt _art;
    Node2D _board;
    BoardOverlay _overlay;
    readonly Dictionary<Unit, UnitView> _views = new();
    readonly HashSet<Vector2I> _berries = new();

    Label _panel, _log, _banner;
    HBoxContainer _strip;
    readonly List<string> _logLines = new();

    enum Mode { Busy, Command, Berry, Over }
    Mode _mode = Mode.Busy;
    Unit _active;
    Vector2I? _hoverTile;
    Dictionary<Vector2I, Vector2I> _reach = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, Seed)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        _board = new Node2D { Scale = new Vector2(Scale, Scale), TextureFilter = TextureFilterEnum.Nearest };
        AddChild(_board);

        _strip = new HBoxContainer { Position = new Vector2(24, 18) };
        _strip.AddThemeConstantOverride("separation", 6);
        AddChild(_strip);

        var backing = new ColorRect { Color = new Color(0.04f, 0.03f, 0.08f, 0.72f), Position = new Vector2(1470, 100), Size = new Vector2(430, 560), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(backing);
        _panel = MakeLabel(17, Ink);
        _panel.Position = new Vector2(1490, 120);
        _panel.Size = new Vector2(410, 760);
        _panel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_panel);

        _log = MakeLabel(16, Dim);
        _log.Position = new Vector2(24, 930);
        AddChild(_log);

        _banner = MakeLabel(54, Gold);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.Position = new Vector2(0, 430);
        _banner.Size = new Vector2(1480, 200);
        _banner.Visible = false;
        AddChild(_banner);

        StartBattle();
    }

    static Label MakeLabel(int size, Color color)
    {
        var l = new Label { MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        l.AddThemeConstantOverride("shadow_offset_x", 2);
        l.AddThemeConstantOverride("shadow_offset_y", 2);
        return l;
    }

    void StartBattle()
    {
        foreach (var child in _board.GetChildren()) child.QueueFree();
        _views.Clear();
        _berries.Clear();
        _logLines.Clear();
        _banner.Visible = false;

        _battle = Encounter.Create(Seed);
        _art = new IsoArt(_battle.Map);
        _board.Position = new Vector2((1480 - _art.Width * Scale) / 2f, (1080 - _art.Height * Scale) / 2f + 60);
        _board.AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(_art.PaintBoard()), Centered = false });
        _overlay = new BoardOverlay { Screen = this, ZIndex = 1 };
        _board.AddChild(_overlay);

        foreach (var u in _battle.Units)
        {
            var v = new UnitView();
            v.Setup(u, this);
            _board.AddChild(v);
            _views[u] = v;
            v.Place(u.Pos);
        }
        AddLog($"{_battle.Map.Name}. A wild pack blocks the way.");
        RunTurns();
    }

    // ------------------------------------------------------------------ geometry

    public IsoArt Art => _art;
    public Battle Battle => _battle;

    public int HeightUnder(Unit u, Vector2I pos) => u.TilesAt(pos).Max(t => _battle.Map.HeightAt(t));

    /// <summary>Where a unit's feet go: the middle of its footprint's top face.</summary>
    public Vector2 FeetAt(Unit u, Vector2I pos)
    {
        var c = new Vector2(pos.X + (u.Size - 1) / 2f, pos.Y + (u.Size - 1) / 2f);
        return _art.TileCenter(c, HeightUnder(u, pos));
    }

    public static int Depth(Unit u, Vector2I pos) => 100 + (pos.X + pos.Y + 2 * (u.Size - 1)) * 4;

    // ------------------------------------------------------------------ turn loop

    async void RunTurns()
    {
        _mode = Mode.Busy;
        while (true)
        {
            var u = _battle.NextTurn();
            await PlayEvents();
            if (u == null || _battle.Winner != null) { ShowEnd(); return; }
            _active = u;
            if (u.Side == Side.Player && !AutoPlay)
            {
                EnterCommand();
                return; // input drives the rest of this turn
            }
            await Wait(0.2f);
            _battle.AiTurn(u);
            await PlayEvents();
            if (_battle.Winner != null) { ShowEnd(); return; }
        }
    }

    void EnterCommand()
    {
        _mode = Mode.Command;
        _reach = _active.Moved ? new() : _battle.Reachable(_active);
        RefreshPanel();
        _overlay.QueueRedraw();
    }

    async void PlayerAct(System.Action act)
    {
        _mode = Mode.Busy;
        _overlay.QueueRedraw();
        act();
        await PlayEvents();
        if (_battle.Winner != null) { ShowEnd(); return; }
        if (_active.Gone || _active.Side != Side.Player || (_active.Moved && _active.Acted)) { EndPlayerTurn(); return; }
        EnterCommand();
    }

    async void EndPlayerTurn()
    {
        _mode = Mode.Busy;
        _battle.EndTurn(_active);
        await PlayEvents();
        if (_battle.Winner != null) { ShowEnd(); return; }
        RunTurns();
    }

    void ShowEnd()
    {
        _mode = Mode.Over;
        _overlay.QueueRedraw();
        int n = _battle.Recruited.Count;
        _banner.Text = _battle.Winner == Side.Player
            ? $"VICTORY\n{(n == 0 ? "no creatures recruited" : n == 1 ? "1 creature joins you" : $"{n} creatures join you")}"
            : "DEFEAT";
        _banner.Visible = true;
        AddLog("R: next encounter    Tab: Sprite Lab");
        RefreshPanel();
    }

    // ------------------------------------------------------------------ input

    public override void _Process(double delta)
    {
        var tile = _art?.Pick(_board.GetLocalMousePosition());
        if (tile != _hoverTile)
        {
            _hoverTile = tile;
            RefreshPanel();
            _overlay?.QueueRedraw();
        }
    }

    Unit UnitOn(Vector2I? tile) => tile == null ? null : _battle.Alive.FirstOrDefault(u => u.Tiles.Contains(tile.Value));

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.R && _mode == Mode.Over) { Seed++; StartBattle(); return; }
            if (_mode == Mode.Berry && k.Keycode == Key.Escape) { _mode = Mode.Command; _overlay.QueueRedraw(); RefreshPanel(); return; }
            if (_mode != Mode.Command) return;
            switch (k.Keycode)
            {
                case Key.Escape when _battle.CanUndo(_active):
                    PlayerAct(() => _battle.UndoMove(_active));
                    break;
                case Key.B when _battle.BerrySpots(_active).Count > 0:
                    _mode = Mode.Berry;
                    _overlay.QueueRedraw();
                    RefreshPanel();
                    break;
                case Key.Space:
                case Key.E:
                    EndPlayerTurn();
                    break;
            }
            return;
        }
        if (e is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex == MouseButton.Right)
        {
            if (_mode == Mode.Berry) { _mode = Mode.Command; _overlay.QueueRedraw(); RefreshPanel(); }
            else if (_mode == Mode.Command && _battle.CanUndo(_active)) PlayerAct(() => _battle.UndoMove(_active));
            return;
        }
        if (mb.ButtonIndex != MouseButton.Left || _hoverTile == null) return;
        var tile = _hoverTile.Value;
        if (_mode == Mode.Berry)
        {
            if (_battle.BerrySpots(_active).Contains(tile)) PlayerAct(() => _battle.OfferBerry(_active, tile));
            return;
        }
        if (_mode != Mode.Command) return;
        var target = UnitOn(tile);
        if (target != null && !_active.Acted && _battle.Targets(_active).Contains(target))
            PlayerAct(() => _battle.Attack(_active, target));
        else if (!_active.Moved && _reach.ContainsKey(tile))
            PlayerAct(() => _battle.Move(_active, tile));
    }

    // ------------------------------------------------------------------ overlay data

    public IEnumerable<(Vector2I tile, Color color)> Highlights()
    {
        if (_mode == Mode.Command)
        {
            if (!_active.Moved)
                foreach (var t in _reach.Keys) yield return (t, new Color(Cyan, 0.32f));
            if (!_active.Acted)
                foreach (var target in _battle.Targets(_active))
                    foreach (var t in target.Tiles) yield return (t, new Color(Magenta, 0.45f));
        }
        else if (_mode == Mode.Berry)
        {
            foreach (var t in _battle.BerrySpots(_active)) yield return (t, new Color(0.4f, 1f, 0.55f, 0.4f));
        }
    }

    public Vector2I? HoverTile => _mode == Mode.Over ? null : _hoverTile;
    public IEnumerable<Vector2I> Berries => _berries;
    public Unit ActiveUnit => _mode == Mode.Over ? null : _active;

    // ------------------------------------------------------------------ panel

    void RefreshPanel()
    {
        if (_battle == null) return;
        var hover = UnitOn(_hoverTile);
        var u = hover ?? _active;
        var lines = new List<string>();
        if (u != null)
        {
            var c = u.Creature;
            var (weapon, armor, move) = c.Loadout();
            lines.Add($"{u.Name}{(u.Side == Side.Wild ? "  (wild)" : "")}");
            lines.Add($"{c.Size} {c.Species.Plan}{(c.Species.Strain != Strain.Natural ? $", {c.Species.Strain}" : "")}");
            lines.Add($"HP {Mathf.Max(0, _views.TryGetValue(u, out var v) ? v.DisplayHp : u.Hp)}/{u.MaxHp}   ATK {u.Atk}   DEF {u.Def}   SPD {u.Spd}   MOVE {u.Move}");
            lines.Add($"Weapon: {weapon}");
            lines.Add($"Armour: {armor}");
            lines.Add($"Moves: {move}");
            lines.Add($"{u.Attitude}: {AttitudeInfo.Reaction(u.Attitude)}");
            if (u.Side == Side.Wild) lines.Add(_battle.RecruitHint(u));
            if (hover != null && _mode == Mode.Command && hover.Side != _active.Side && _battle.Targets(_active).Contains(hover))
            {
                var (dmg, hit) = _battle.Preview(_active, hover);
                lines.Add("");
                lines.Add($">> {_active.Name} attacks: about {dmg} damage, {hit}% to hit");
            }
        }
        if (_mode is Mode.Command or Mode.Berry)
        {
            lines.Add("");
            lines.Add($"{_active.Name}'s turn");
            lines.Add(_mode == Mode.Berry
                ? "Click a green tile to set down a glowberry.   Esc: cancel"
                : $"{(_active.Moved ? "" : "Click a blue tile to move.   ")}{(_active.Acted ? "" : "Click a marked enemy to attack.   ")}"
                  + $"{(_battle.BerrySpots(_active).Count > 0 ? $"B: glowberry ({_battle.Berries}).   " : "")}"
                  + $"{(_battle.CanUndo(_active) ? "Esc: undo move.   " : "")}Space: end turn");
        }
        _panel.Text = string.Join("\n", lines);
    }

    void RefreshStrip()
    {
        foreach (var child in _strip.GetChildren()) child.QueueFree();
        foreach (var u in _battle.Upcoming().Take(10))
        {
            var frame = new PanelContainer { CustomMinimumSize = new Vector2(72, 72) };
            var box = new StyleBoxFlat { BgColor = new Color(0.06f, 0.05f, 0.1f, 0.85f), BorderColor = u.Side == Side.Player ? Cyan : Magenta };
            box.SetBorderWidthAll(u == _active ? 3 : 1);
            box.SetCornerRadiusAll(4);
            frame.AddThemeStyleboxOverride("panel", box);
            frame.AddChild(new TextureRect
            {
                Texture = _views[u].Texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = TextureFilterEnum.Nearest,
                FlipH = u.Side == Side.Wild,
            });
            _strip.AddChild(frame);
        }
    }

    void AddLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > 5) _logLines.RemoveAt(0);
        _log.Text = string.Join("\n", _logLines);
    }

    // ------------------------------------------------------------------ animation

    async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    async Task PlayEvents()
    {
        var events = _battle.Events.ToList();
        _battle.Events.Clear();
        foreach (var e in events)
        {
            switch (e)
            {
                case TurnEvent t:
                    _active = t.U;
                    RefreshStrip();
                    foreach (var v in _views.Values) v.QueueRedraw();
                    break;
                case MoveEvent m:
                    await _views[m.U].Walk(m.Path);
                    break;
                case StrikeEvent s:
                    await _views[s.A].Lunge(_views[s.T]);
                    break;
                case DamageEvent d:
                    _views[d.U].Hurt(d.Amount, d.Miss);
                    Popup(_views[d.U], d.Miss ? "miss" : d.Amount.ToString(), d.Miss ? Dim : Ink);
                    await Wait(0.22f);
                    break;
                case PushEvent p:
                    await _views[p.U].Slide(p.To);
                    break;
                case FaintEvent f:
                    await _views[f.U].Vanish();
                    break;
                case RecruitEvent r:
                    _views[r.U].Recruited();
                    Popup(_views[r.U], "joins!", Cyan);
                    AddLog($"{r.U.Name} joins you: {r.Why}.");
                    await Wait(0.7f);
                    break;
                case HealEvent h:
                    _views[h.U].Heal(h.Amount);
                    Popup(_views[h.U], $"+{h.Amount}", new Color(0.5f, 1f, 0.6f));
                    await Wait(0.2f);
                    break;
                case BerryEvent b:
                    if (b.Placed) _berries.Add(b.Tile); else _berries.Remove(b.Tile);
                    _overlay.QueueRedraw();
                    break;
                case LogEvent l:
                    AddLog(l.Text);
                    break;
            }
        }
        RefreshPanel();
    }

    void Popup(UnitView v, string text, Color color)
    {
        var l = MakeLabel(9, color);
        l.Text = text;
        l.ZIndex = 400;
        l.Position = v.Position + new Vector2(-20, -v.SpriteHeight - 14);
        l.Size = new Vector2(40, 12);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        _board.AddChild(l);
        var tw = CreateTween();
        tw.TweenProperty(l, "position:y", l.Position.Y - 10, 0.6f);
        tw.Parallel().TweenProperty(l, "modulate:a", 0f, 0.6f).SetDelay(0.25f);
        tw.TweenCallback(Callable.From(l.QueueFree));
    }
}

/// <summary>Tile highlights, berries and the hover cursor, drawn over the board.</summary>
public partial class BoardOverlay : Node2D
{
    public BattleScreen Screen;

    public override void _Draw()
    {
        var art = Screen.Art;
        foreach (var (t, c) in Screen.Highlights())
        {
            var d = art.Diamond(t, 1);
            DrawColoredPolygon(d, c);
            DrawPolyline(new[] { d[0], d[1], d[2], d[3], d[0] }, new Color(c, 0.9f), 1);
        }
        foreach (var b in Screen.Berries)
        {
            var c = art.TileCenter(b, Screen.Battle.Map.HeightAt(b));
            DrawCircle(c + new Vector2(0, -3), 3.5f, Color.FromHtml("#1b0f24"));
            DrawCircle(c + new Vector2(0, -3), 2.5f, Color.FromHtml("#ff5fd2"));
            DrawCircle(c + new Vector2(-1, -4), 0.9f, Colors.White);
        }
        if (Screen.HoverTile is { } h)
        {
            var d = Screen.Art.Diamond(h);
            DrawPolyline(new[] { d[0], d[1], d[2], d[3], d[0] }, new Color(1, 1, 1, 0.85f), 1);
        }
    }
}

/// <summary>One creature on the board: sprite, shadow, side ring, HP bar, and its animations.</summary>
public partial class UnitView : Node2D
{
    public Unit Unit;
    public Texture2D Texture;
    public int DisplayHp;
    public float SpriteHeight => Texture.GetHeight();

    BattleScreen _screen;
    Sprite2D _sprite;
    Vector2I _tile;
    Color _ring;
    float _flash;

    public void Setup(Unit u, BattleScreen screen)
    {
        Unit = u;
        _screen = screen;
        DisplayHp = u.Hp;
        _ring = u.Side == Side.Player ? Color.FromHtml("#4fe3ff") : Color.FromHtml("#ff4f9a");
        Texture = ImageTexture.CreateFromImage(CreaturePainter.Paint(u.Creature, false));
        _sprite = new Sprite2D { Texture = Texture, Centered = false };
        _sprite.Position = new Vector2(-Texture.GetWidth() / 2f, -Texture.GetHeight() + 3);
        // Sprites face left; the wild pack starts at the back and faces the player's corner.
        _sprite.FlipH = u.Side == Side.Wild;
        AddChild(_sprite);
    }

    public void Place(Vector2I tile)
    {
        _tile = tile;
        Position = _screen.FeetAt(Unit, tile);
        ZIndex = BattleScreen.Depth(Unit, tile);
    }

    public override void _Process(double delta)
    {
        if (_flash > 0) { _flash = Mathf.Max(0, _flash - (float)delta * 4); _sprite.Modulate = Colors.White.Lerp(new Color(1, 0.3f, 0.3f), _flash); }
        bool active = _screen.ActiveUnit == Unit;
        _sprite.Position = new Vector2(_sprite.Position.X, -Texture.GetHeight() + 3 + (active ? Mathf.Round(Mathf.Sin(Time.GetTicksMsec() / 160f)) : 0));
        QueueRedraw();
    }

    public override void _Draw()
    {
        float rx = IsoArt.TW * 0.32f * Unit.Size, ry = IsoArt.TH * 0.32f * Unit.Size;
        bool active = _screen.ActiveUnit == Unit;
        var pts = new Vector2[24];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = Mathf.Tau * i / pts.Length;
            pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        DrawColoredPolygon(pts, new Color(0, 0, 0, 0.35f));
        var ring = pts.Append(pts[0]).ToArray();
        DrawPolyline(ring, new Color(_ring, active ? 1f : 0.65f), active ? 1.5f : 1f);

        // HP bar above the head.
        float w = 18 * Unit.Size, y = -Texture.GetHeight() - 3;
        float frac = Mathf.Clamp(DisplayHp / (float)Unit.MaxHp, 0, 1);
        DrawRect(new Rect2(-w / 2 - 1, y - 1, w + 2, 4), new Color(0.05f, 0.03f, 0.08f, 0.9f));
        DrawRect(new Rect2(-w / 2, y, w * frac, 2), frac > 0.5f ? new Color(0.4f, 1f, 0.6f) : frac > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.3f, 0.35f));
        if (active)
        {
            // A marker over the active creature.
            var m = new Vector2(0, y - 6 + Mathf.Sin(Time.GetTicksMsec() / 200f) * 1.5f);
            DrawColoredPolygon(new[] { m + new Vector2(-3, -3), m + new Vector2(3, -3), m + new Vector2(0, 1) }, _ring);
        }
    }

    void Face(Vector2 toward) { if (Mathf.Abs(toward.X) > 0.5f) _sprite.FlipH = toward.X > 0; }

    public async Task Walk(List<Vector2I> path)
    {
        var tw = CreateTween();
        for (int i = 1; i < path.Count; i++)
        {
            var to = _screen.FeetAt(Unit, path[i]);
            var from = _screen.FeetAt(Unit, path[i - 1]);
            int step = i;
            tw.TweenCallback(Callable.From(() => { Face(to - from); ZIndex = BattleScreen.Depth(Unit, path[step]); }));
            if (Mathf.Abs(to.Y - from.Y) > IsoArt.TH)
            {
                // A hop up or down a ledge.
                var mid = new Vector2((from.X + to.X) / 2, Mathf.Min(from.Y, to.Y) - 8);
                tw.TweenProperty(this, "position", mid, 0.07f);
                tw.TweenProperty(this, "position", to, 0.07f);
            }
            else tw.TweenProperty(this, "position", to, 0.1f);
        }
        await ToSignal(tw, Tween.SignalName.Finished);
        _tile = path[^1];
    }

    public async Task Slide(Vector2I to)
    {
        var tw = CreateTween();
        tw.TweenProperty(this, "position", _screen.FeetAt(Unit, to), 0.12f);
        await ToSignal(tw, Tween.SignalName.Finished);
        _tile = to;
        ZIndex = BattleScreen.Depth(Unit, to);
    }

    public async Task Lunge(UnitView target)
    {
        var dir = (target.Position - Position).Normalized();
        Face(target.Position - Position);
        var home = Position;
        var tw = CreateTween();
        tw.TweenProperty(this, "position", home + dir * 7, 0.08f);
        tw.TweenProperty(this, "position", home, 0.1f);
        await ToSignal(tw, Tween.SignalName.Finished);
    }

    public void Hurt(int amount, bool miss)
    {
        if (miss) return;
        DisplayHp -= amount;
        _flash = 1;
    }

    public void Heal(int amount) => DisplayHp += amount;

    public void Recruited()
    {
        _ring = Color.FromHtml("#4fe3ff");
        DisplayHp = Mathf.Max(DisplayHp, Unit.MaxHp / 2);
        var tw = CreateTween();
        tw.TweenProperty(_sprite, "modulate", new Color(0.6f, 1.6f, 2f), 0.15f);
        tw.TweenProperty(_sprite, "modulate", Colors.White, 0.4f);
    }

    public async Task Vanish()
    {
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0f, 0.35f);
        await ToSignal(tw, Tween.SignalName.Finished);
        Visible = false;
    }
}
