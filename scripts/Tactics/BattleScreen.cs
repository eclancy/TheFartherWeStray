using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CreatureTactics.Creatures;
using CreatureTactics.UI;
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
    public bool DemoAim;    // on the first turn, aim a skill at the busiest tile (screenshots of the skill panel)
    public bool DemoFx;     // show one skill of every look in turn, without touching the battle (--fxdemo)
    public bool DemoShiny;  // make the first wild creature shiny, to see the reveal (they're 1 in 512)
    public float TurnSeconds; // a turn timer (PvP will use one): your turn ends when it runs out; 0 for none
    double _turnLeft;
    Unit _timedUnit;
    HBoxContainer _timer;
    Label _timerText;
    public List<(Creature, Classes.Training)> Squad; // your creatures and their training; null for the default squad
    public System.Action<Battle> Finished; // set when a ranch is waiting for the result
    public System.Action<Battle> Prepare;  // before the first turn: a ranch puts its creatures under their tamers
    public Mission Mission;               // the contract being played; null for a one-off rout
    public string MapName = "Undercity Foundry";

    // The camera (the user's ask, 2026-11-02: closer in, less background): the board drawn at a
    // zoom, aimed at a point that follows whoever acts; WASD / the arrows or a middle-drag pan it,
    // the wheel zooms. The map is painted with surroundings, so there's town or country to see round it.
    public const int Skirt = 8;
    const float MinZoom = 2, MaxZoom = 3.5f;
    static readonly Vector2 View = new(740, 540); // the middle of the play area, left of the info panel
    float _zoom = 3, _zoomShown = 3;
    Vector2 _look, _lookShown;     // the board point the camera aims at, and where it's got to
    Rect2 _bounds;                 // where the camera may aim: over the battlefield
    UnitView _follow;              // who the camera is following, if anyone
    static readonly Color Cyan = Color.FromHtml("#4fe3ff"), Magenta = Color.FromHtml("#ff4f9a"), Gold = Color.FromHtml("#f2c14e");
    static readonly Color Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#9a91b4");

    Battle _battle;
    TextureRect _backdrop;
    IsoArt _art;
    Node2D _board;
    BoardOverlay _overlay;
    readonly Dictionary<Unit, UnitView> _views = new();
    readonly HashSet<Vector2I> _berries = new();

    Label _log, _banner, _objective;
    PanelContainer _infoPanel;
    VBoxContainer _info;
    HBoxContainer _strip;
    const float PanelW = 430;
    readonly List<string> _logLines = new();

    enum Mode { Busy, Command, Berry, Skill, Over }
    Classes.SkillDef _skill;      // the skill being aimed (Mode.Skill)
    HBoxContainer _skillBar;
    readonly List<(List<Vector2I> area, Color color, double until)> _flashes = new();
    SkillFx _fx;
    Mode _mode = Mode.Busy;
    Unit _active;
    Vector2I? _hoverTile;
    Dictionary<Vector2I, Vector2I> _reach = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = _backdrop = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        _board = new Node2D { Scale = new Vector2(_zoom, _zoom), TextureFilter = TextureFilterEnum.Nearest };
        AddChild(_board);
        // The HUD on a layer of its own: the board's sprites sort by depth in the hundreds and would draw over it.
        var hud = new CanvasLayer { Layer = 1 };
        AddChild(hud);

        _strip = new HBoxContainer { Position = new Vector2(24, 18) };
        _strip.AddThemeConstantOverride("separation", 6);
        hud.AddChild(_strip);

        // The info panel: whoever is under the cursor (or whose turn it is), in glyphs and numbers.
        _infoPanel = new PanelContainer { Position = new Vector2(1470, 100), CustomMinimumSize = new Vector2(PanelW, 0), MouseFilter = MouseFilterEnum.Ignore };
        var backing = new StyleBoxFlat { BgColor = new Color(0.04f, 0.03f, 0.08f, 0.93f), BorderColor = new Color(0.3f, 0.26f, 0.42f) };
        backing.SetBorderWidthAll(1);
        backing.SetCornerRadiusAll(6);
        backing.SetContentMarginAll(14);
        _infoPanel.AddThemeStyleboxOverride("panel", backing);
        _info = new VBoxContainer();
        _info.AddThemeConstantOverride("separation", 8);
        _infoPanel.AddChild(_info);
        hud.AddChild(_infoPanel);

        _timer = new HBoxContainer { Position = new Vector2(150, 1010), Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _timer.AddChild(Ui.Picture(Glyphs.Get("clock", 2), "Turn timer: the turn ends when it runs out"));
        _timerText = Ui.Text("", 24, Ink, 6);
        _timer.AddChild(_timerText);
        hud.AddChild(_timer);

        _skillBar = new HBoxContainer { Position = new Vector2(260, 994) };
        _skillBar.AddThemeConstantOverride("separation", 6);
        hud.AddChild(_skillBar);

        // Dark backings under the objective line and the log, so they read over a busy town.
        foreach (var (pos, size) in new[] { (new Vector2(0, 88), new Vector2(1460, 40)), (new Vector2(1460, 868), new Vector2(460, 212)) })
        {
            var under = new ColorRect { Position = pos, Size = size, Color = new Color(0.03f, 0.02f, 0.06f, 0.72f), MouseFilter = MouseFilterEnum.Ignore };
            hud.AddChild(under);
        }
        _log = MakeLabel(15, Dim);
        _log.Position = new Vector2(1474, 880);
        _log.Size = new Vector2(PanelW - 8, 190);
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _log.VerticalAlignment = VerticalAlignment.Bottom;
        hud.AddChild(_log);

        _objective = MakeLabel(19, Gold);
        _objective.Position = new Vector2(24, 100);
        _objective.Size = new Vector2(1400, 30);
        hud.AddChild(_objective);

        _banner = MakeLabel(54, Gold);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.Position = new Vector2(0, 430);
        _banner.Size = new Vector2(1480, 200);
        _banner.Visible = false;
        hud.AddChild(_banner);

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

        _battle = Mission != null ? Mission.CreateBattle(Squad ?? Encounter.StarterSquad().ToList()) : Encounter.Create(Seed, Squad, MapName);
        Prepare?.Invoke(_battle);
        Audio.Music.Play(Audio.Music.ForBattle(Mission));
        if (DemoShiny && _battle.Units.FirstOrDefault(x => x.Side == Side.Wild) is { } demo && !demo.Creature.Shiny)
        {
            var c = demo.Creature;
            c.Spec = c.Spec.Shiny(new SeededRng(SeededRng.Derive(c.Seed, 0x5414)));
            c.Palette = Creature.BuildPalette(c.Spec, c.Species);
            c.Shiny = true;
            demo.ShinyPassive = Classes.ClassBook.ShinyPassive(c);
        }
        _art = new IsoArt(_battle.Map, Skirt);
        _backdrop.Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, Seed, _battle.Map.Theme));
        // The camera may aim anywhere over the battlefield; it starts on your side of it.
        var corners = new[] { new Vector2I(0, 0), new Vector2I(_battle.Map.W - 1, 0), new Vector2I(0, _battle.Map.H - 1), new Vector2I(_battle.Map.W - 1, _battle.Map.H - 1) }
            .Select(t => _art.TileCenter(new Vector2(t.X, t.Y), 0)).ToList();
        _bounds = new Rect2(corners.Aggregate((a, b) => a.Min(b)), Vector2.Zero).Expand(corners.Aggregate((a, b) => a.Max(b)));
        _follow = null;
        _board.AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(_art.PaintBoard()), Centered = false });
        // Props stand on their tiles, sorted with the creatures so a tree hides what's behind it.
        PropArt.Place(_board, _battle.Map, _art);
        _overlay = new BoardOverlay { Screen = this, ZIndex = 1 };
        _board.AddChild(_overlay);
        _fx = new SkillFx { ZIndex = 300 };
        _board.AddChild(_fx);

        foreach (var u in _battle.Units)
        {
            var v = new UnitView();
            v.Setup(u, this);
            _board.AddChild(v);
            _views[u] = v;
            v.Place(u.Pos);
        }
        var yours = _views.Where(p => p.Key.Side == Side.Player).Select(p => p.Value.Position).ToList();
        _look = _lookShown = Clamped(yours.Count > 0 ? yours.Aggregate((a, b) => a + b) / yours.Count : _bounds.GetCenter());
        PlaceCamera();
        // Everyone starts facing the other side.
        foreach (var (u, v) in _views)
        {
            var foes = _views.Where(o => o.Key.Side != u.Side).Select(o => o.Value.Position).ToList();
            if (foes.Count > 0) v.Face(foes.Aggregate(Vector2.Zero, (a, b) => a + b) / foes.Count - v.Position);
        }
        AddLog(_battle.Rival != null ? $"{_battle.Map.Name}. {_battle.Rival.Name} is waiting." : $"{_battle.Map.Name}. A wild pack blocks the way.");
        RefreshObjective();
        Opening();
    }

    /// <summary>
    /// Before the first turn: a rival tamer announces the duel, then the camera visits every shiny on
    /// the far side, and each one glints.
    /// </summary>
    async void Opening()
    {
        _mode = Mode.Busy;
        var home = _look;
        if (_battle.Rival is { } rival)
        {
            _banner.Text = $"{rival.Name.ToUpperInvariant()}\nchallenges you";
            _banner.Visible = true;
            Audio.Sound.Play("gong");
            AddLog($"{rival.Name} sends out {_battle.Units.Count(u => u.Side == Side.Wild)} creatures (a tamer's can't be recruited). {rival.Specialty}: {rival.Text}");
            await Wait(1.6f);
            _banner.Visible = false;
        }
        foreach (var (u, v) in _views.Where(p => p.Key.Side == Side.Wild && p.Key.Creature.Shiny).ToList())
        {
            _look = Body(v);
            await Wait(0.7f);
            AddLog($"A shiny {u.Name} appears! {u.ShinyPassive?.Name}: {u.ShinyPassive?.Text}");
            Popup(v, "shiny!", Gold, -12, UI.Glyphs.Mini("star", 2));
            Audio.Sound.Play("shiny");
            await v.Glint();
            await Wait(0.3f);
        }
        if (_look != home)
        {
            _look = home;
            await Wait(0.6f);
        }
        if (DemoFx) FxDemo();
        else RunTurns();
    }

    /// <summary>--fxdemo: a creature fires one skill of each look at the nearest wild one, centred on screen.</summary>
    async void FxDemo()
    {
        var user = _battle.Units.First(u => u.Side == Side.Player);
        var foe = _battle.Units.Where(u => u.Side == Side.Wild && u.Size == 1).OrderBy(u => Battle.Dist(user, user.Pos, u)).First();
        // Bring the target three tiles away (a demo: no turns are played after it).
        var near = Enumerable.Range(0, _battle.Map.W * _battle.Map.H).Select(i => new Vector2I(i % _battle.Map.W, i / _battle.Map.W))
            .Where(t => Mathf.Abs(t.X - user.Pos.X) + Mathf.Abs(t.Y - user.Pos.Y) == 3 && _battle.CanStand(foe, t))
            .OrderBy(t => Mathf.Abs(_battle.Map.HeightAt(t) - _battle.Map.HeightAt(user.Pos))).FirstOrDefault(foe.Pos);
        foe.Pos = near;
        _views[foe].Place(near);
        var mid = (_views[user].Position + _views[foe].Position) / 2 + new Vector2(0, -20);
        _look = _lookShown = mid;
        PlaceCamera();
        var picks = new List<Classes.SkillDef>();
        foreach (var theme in System.Enum.GetValues<FxTheme>())
        {
            bool Ranged(Classes.SkillDef s) => s.Range > 1 && !s.Leap && !s.AroundSelf;
            var pick = Classes.ClassBook.Skills.Where(s => SkillFx.ThemeOf(s, user) == theme).OrderByDescending(Ranged).FirstOrDefault();
            if (pick != null) picks.Add(pick);
        }
        picks.Add(Classes.ClassBook.Skills.First(s => s.AroundSelf && s.Power > 0));
        await Wait(0.5f);
        foreach (var s in picks)
        {
            bool onSelf = s.AroundSelf || s.Target is Classes.TargetKind.Self or Classes.TargetKind.Ally;
            var centre = onSelf ? user.Pos : foe.Pos;
            var view = _views[user];
            var target = onSelf ? null : _views[foe];
            Popup(view, s.Name, Gold, -10, SkillIcons.Skill(s, 1));
            AddLog($"{s.Name}: {SkillFx.ThemeOf(s, user)}");
            bool melee = s.Power > 0 && (s.Range <= 1 || s.Leap) && !s.AroundSelf;
            if (target != null) view.Face(target.Position - view.Position);
            await SkillEffect(new SkillEvent(user, s, centre, _battle.SkillArea(user, s, centre)), view, target, melee);
            if (s.Power > 0 && target != null) { _fx.Hit(Body(target), 9, false); _views[foe].Hurt(0, false); }
            if (s.Heal > 0) _fx.Rise(Body(view), view.Position, FxTheme.Leaf);
            await Wait(0.55f);
        }
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
        if (TurnSeconds > 0 && _active != _timedUnit) { _timedUnit = _active; _turnLeft = TurnSeconds; }
        _mode = Mode.Command;
        _skill = null;
        _reach = _active.Moved ? new() : _battle.Reachable(_active);
        RefreshPanel();
        RefreshSkillBar();
        _overlay.QueueRedraw();
        if (DemoAim && _demoHover == null && _active.Side == Side.Player) DemoTurn();
    }

    /// <summary>For screenshots: walk toward the pack, then aim the skill that reaches the most creatures.</summary>
    void DemoTurn()
    {
        var best = _active.Skills.Where(_active.Ready)
            .SelectMany(s => _battle.SkillTiles(_active, s).Select(t => (s, t, n: _battle.SkillTargets(_active, s, t).Count)))
            .OrderByDescending(x => x.n).FirstOrDefault();
        if (best.s != null && best.n > 0)
        {
            SelectSkill(best.s);
            _demoHover = best.t;
        }
        else if (!_active.Moved && _reach.Count > 0)
        {
            var foes = _battle.Alive.Where(f => f.Side != _active.Side).ToList();
            var to = _reach.Keys.OrderBy(t => foes.Min(f => System.Math.Abs(f.Pos.X - t.X) + System.Math.Abs(f.Pos.Y - t.Y))).First();
            PlayerAct(() => _battle.Order(CommandKind.Move, _active, to));
        }
        else EndPlayerTurn();
    }

    Vector2I? _demoHover;

    /// <summary>
    /// The command bar: the basic attack, every skill (keys 1-9) with its cooldown over it, then
    /// glowberry (B), undo (Esc) and end turn (Space). Names and full text are in the tooltips.
    /// </summary>
    void RefreshSkillBar()
    {
        foreach (var child in _skillBar.GetChildren()) child.QueueFree();
        if (_mode is not (Mode.Command or Mode.Skill) || _active == null) return;

        var attack = BarSlot(SkillIcons.Attack(_active.Weapon, 3), "", $"Attack: click a red-marked foe\n{_active.Creature.Loadout().weapon}", !_active.Acted, false, 0, null);
        _skillBar.AddChild(attack);
        for (int i = 0; i < _active.Skills.Count; i++)
        {
            var s = _active.Skills[i];
            int cd = _active.Cooldowns.TryGetValue(s.Id, out int c) ? c : 0;
            _skillBar.AddChild(BarSlot(SkillIcons.Skill(s, 3), (i + 1).ToString(), $"{s.Name} ({s.Class})\n{s.Text}",
                _active.Ready(s) && !_active.Acted, s == _skill, cd, () => SelectSkill(s)));
        }
        _skillBar.AddChild(new Control { CustomMinimumSize = new Vector2(18, 0) });
        if (_mode == Mode.Command && _battle.BerrySpots(_active).Count > 0)
            _skillBar.AddChild(BarSlot(SkillIcons.Action("berry", 3), "B", $"Glowberry: set one down to lure the curious ({_battle.Berries} left)", true, false, 0, BerryMode, _battle.Berries.ToString()));
        foreach (var item in _battle.ItemsFor(_active))
            _skillBar.AddChild(BarSlot(SkillIcons.Skill(item, 3), "", $"{item.Name} (item)\n{item.Text}", !_active.Acted, item == _skill, 0,
                () => SelectSkill(item), _battle.Bag[item.Id].ToString()));
        if (_mode == Mode.Command && _battle.CanUndo(_active))
            _skillBar.AddChild(BarSlot(SkillIcons.Action("undo", 3), "Esc", "Undo the move", true, false, 0, () => PlayerAct(() => _battle.Order(CommandKind.Undo, _active))));
        if (_mode == Mode.Command)
            _skillBar.AddChild(BarSlot(SkillIcons.Action("end", 3), "Spc", "End the turn", true, false, 0, EndPlayerTurn));
    }

    static StyleBoxFlat SlotBox(Color border, int width)
    {
        var box = new StyleBoxFlat { BgColor = new Color(0.05f, 0.04f, 0.09f, 0.88f), BorderColor = border };
        box.SetBorderWidthAll(width);
        box.SetCornerRadiusAll(6);
        return box;
    }

    /// <summary>One slot of the command bar: an icon, its key in the corner, its cooldown or count over it.</summary>
    Control BarSlot(Texture2D icon, string key, string tip, bool enabled, bool selected, int cooldown, System.Action onPress, string count = null)
    {
        Control slot;
        var size = new Vector2(72, 72);
        if (onPress == null)
        {
            var panel = new PanelContainer { CustomMinimumSize = size, TooltipText = tip, Modulate = enabled ? Colors.White : new Color(1, 1, 1, 0.4f) };
            panel.AddThemeStyleboxOverride("panel", SlotBox(new Color(0.3f, 0.27f, 0.4f), 2));
            panel.AddChild(UI.Ui.Picture(icon));
            slot = panel;
        }
        else
        {
            var b = new Button { Icon = icon, Disabled = !enabled, TooltipText = tip, FocusMode = FocusModeEnum.None, CustomMinimumSize = size, IconAlignment = HorizontalAlignment.Center };
            b.AddThemeStyleboxOverride("normal", SlotBox(selected ? Gold : new Color(0.3f, 0.27f, 0.4f), selected ? 3 : 2));
            b.AddThemeStyleboxOverride("hover", SlotBox(selected ? Gold : Ink, selected ? 3 : 2));
            b.AddThemeStyleboxOverride("pressed", SlotBox(Gold, 3));
            b.AddThemeStyleboxOverride("disabled", SlotBox(new Color(0.2f, 0.18f, 0.26f), 2));
            b.Pressed += onPress;
            slot = b;
        }
        if (key != "")
        {
            var k = UI.Ui.Text(key, 14, Ink, 5);
            k.Position = new Vector2(5, 0);
            slot.AddChild(k);
        }
        if (cooldown > 0)
        {
            slot.AddChild(new ColorRect { Color = new Color(0.02f, 0.01f, 0.05f, 0.55f), Size = size, MouseFilter = MouseFilterEnum.Ignore });
            var cd = UI.Ui.Text(cooldown.ToString(), 32, Ink, 8);
            cd.HorizontalAlignment = HorizontalAlignment.Center;
            cd.Size = size;
            slot.AddChild(cd);
        }
        if (count != null)
        {
            var n = UI.Ui.Text(count, 16, Gold, 5);
            n.Position = new Vector2(54, 46);
            slot.AddChild(n);
        }
        return slot;
    }

    void BerryMode()
    {
        if (_mode != Mode.Command || _battle.BerrySpots(_active).Count == 0) return;
        _mode = Mode.Berry;
        _overlay.QueueRedraw();
        RefreshPanel();
        RefreshSkillBar();
    }

    void SelectSkill(Classes.SkillDef s)
    {
        if (_mode is not (Mode.Command or Mode.Skill) || _active.Acted || !_active.Ready(s)) return;
        _mode = Mode.Skill;
        _skill = s;
        _skillTiles = _battle.SkillTiles(_active, s);
        RefreshPanel();
        RefreshSkillBar();
        _overlay.QueueRedraw();
    }

    List<Vector2I> _skillTiles = new();

    /// <summary>Where the aimed skill would land for the hovered tile (self-centred skills ignore the cursor).</summary>
    Vector2I? SkillAim() => _skill == null ? null
        : _skill.AroundSelf || _skill.Target == Classes.TargetKind.Self ? _active.Pos
        : _hoverTile is { } h && _skillTiles.Contains(h) ? h : null;

    async void PlayerAct(System.Action act)
    {
        _mode = Mode.Busy;
        _skill = null;
        RefreshSkillBar();
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
        RefreshSkillBar();
        _battle.Order(CommandKind.EndTurn, _active);
        await PlayEvents();
        if (_battle.Winner != null) { ShowEnd(); return; }
        RunTurns();
    }

    void ShowEnd()
    {
        _mode = Mode.Over;
        Audio.Music.Stop(0.3f); // out of the way of the jingle
        Audio.Sound.Play(_battle.Winner == Side.Player ? "victory" : "defeat");
        _overlay.QueueRedraw();
        int n = _battle.Recruited.Count;
        _banner.Text = _battle.Winner != Side.Player ? "DEFEAT"
            : _battle.Rival is { } rival ? $"VICTORY\n{rival.Name} is beaten"
            : $"VICTORY\n{(n == 0 ? "no creatures recruited" : n == 1 ? "1 creature joins you" : $"{n} creatures join you")}";
        _banner.Visible = true;
        AddLog(Finished != null ? "Enter: back to the ranch" : "R: next encounter    Tab: Sprite Lab");
        if (AutoPlay && Finished != null) GetTree().CreateTimer(2.5).Timeout += () => { if (IsInsideTree()) Finished(_battle); };
        RefreshPanel();
    }

    // ------------------------------------------------------------------ input

    Vector2 Clamped(Vector2 p) => new(Mathf.Clamp(p.X, _bounds.Position.X, _bounds.End.X), Mathf.Clamp(p.Y, _bounds.Position.Y, _bounds.End.Y));

    /// <summary>Puts the board where the camera is: its aim in the middle of the play area, at its zoom.</summary>
    void PlaceCamera()
    {
        _board.Scale = new Vector2(_zoomShown, _zoomShown);
        _board.Position = View - _lookShown * _zoomShown;
    }

    void Camera(double delta)
    {
        // Held keys pan (and stop the camera following); a followed creature keeps the camera on it.
        var pan = new Vector2(
            (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1 : 0) - (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? 1 : 0),
            (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) ? 1 : 0) - (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) ? 1 : 0));
        if (pan != Vector2.Zero) { _follow = null; _look += pan * (float)delta * 320 / _zoom; }
        if (_follow != null && IsInstanceValid(_follow)) _look = Body(_follow);
        _look = Clamped(_look);
        float k = 1 - Mathf.Exp(-(float)delta * 6);
        _lookShown = _lookShown.Lerp(_look, k);
        _zoomShown = Mathf.Lerp(_zoomShown, _zoom, k);
        PlaceCamera();
    }

    public override void _Process(double delta)
    {
        if (_board != null && _art != null) Camera(delta);
        if (_flashes.Count > 0) _overlay?.QueueRedraw();
        // The turn timer runs only while you're choosing.
        bool timing = TurnSeconds > 0 && !AutoPlay && _mode is Mode.Command or Mode.Skill or Mode.Berry && _active?.Side == Side.Player;
        _timer.Visible = timing;
        if (timing)
        {
            int before = Mathf.CeilToInt((float)_turnLeft);
            _turnLeft -= delta;
            if (_turnLeft <= 5 && Mathf.CeilToInt((float)_turnLeft) < before) Audio.Sound.Play("tick");
            _timerText.Text = $"{Mathf.CeilToInt((float)Mathf.Max(0, _turnLeft))}";
            _timerText.AddThemeColorOverride("font_color", _turnLeft <= 5 ? Ui.Bad : Ink);
            if (_turnLeft <= 0) { _timer.Visible = false; EndPlayerTurn(); }
        }
        // The info panel hugs its rows (a lone control doesn't shrink by itself).
        if (_infoPanel != null && _infoPanel.Size.Y != _infoPanel.GetCombinedMinimumSize().Y) _infoPanel.Size = new Vector2(PanelW, 0);
        var tile = _demoHover ?? _art?.Pick(_board.GetLocalMousePosition());
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
            if (_mode == Mode.Over && k.Keycode is Key.R or Key.Enter or Key.KpEnter)
            {
                if (Finished != null) Finished(_battle);
                else { Seed++; StartBattle(); }
                return;
            }
            if (_mode is Mode.Berry or Mode.Skill && k.Keycode == Key.Escape) { EnterCommand(); return; }
            if (_mode is Mode.Command or Mode.Skill && k.Keycode >= Key.Key1 && k.Keycode <= Key.Key9)
            {
                int i = (int)(k.Keycode - Key.Key1);
                if (i < _active.Skills.Count) SelectSkill(_active.Skills[i]);
                return;
            }
            if (_mode == Mode.Skill && k.Keycode is Key.Enter or Key.KpEnter && SkillAim() is { } aimed)
            {
                var s = _skill;
                PlayerAct(() => _battle.Order(CommandKind.Skill, _active, aimed, skill: s));
                return;
            }
            if (_mode != Mode.Command) return;
            switch (k.Keycode)
            {
                case Key.Escape when _battle.CanUndo(_active):
                    PlayerAct(() => _battle.Order(CommandKind.Undo, _active));
                    break;
                case Key.B:
                    BerryMode();
                    break;
                case Key.Space:
                case Key.E:
                    EndPlayerTurn();
                    break;
            }
            return;
        }
        // The camera: the wheel zooms, a middle-button drag pans.
        if (e is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Middle) != 0)
        {
            _follow = null;
            _look = Clamped(_look - motion.Relative / _zoomShown);
            _lookShown = _look;
            return;
        }
        if (e is not InputEventMouseButton { Pressed: true } mb) return;
        if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            _zoom = Mathf.Clamp(_zoom + (mb.ButtonIndex == MouseButton.WheelUp ? 0.5f : -0.5f), MinZoom, MaxZoom);
            return;
        }
        if (mb.ButtonIndex == MouseButton.Right)
        {
            if (_mode is Mode.Berry or Mode.Skill) EnterCommand();
            else if (_mode == Mode.Command && _battle.CanUndo(_active)) PlayerAct(() => _battle.Order(CommandKind.Undo, _active));
            return;
        }
        if (mb.ButtonIndex != MouseButton.Left || _hoverTile == null) return;
        var tile = _hoverTile.Value;
        if (_mode == Mode.Skill)
        {
            // Self-centred skills go off on a click anywhere in their area; aimed ones on a valid tile.
            var aim = SkillAim();
            bool selfish = _skill.AroundSelf || _skill.Target == Classes.TargetKind.Self;
            if (aim != null && (!selfish || _battle.SkillArea(_active, _skill, aim.Value).Contains(tile) || _active.Tiles.Contains(tile)))
            {
                var s = _skill;
                PlayerAct(() => _battle.Order(CommandKind.Skill, _active, aim.Value, skill: s));
            }
            return;
        }
        if (_mode == Mode.Berry)
        {
            if (_battle.BerrySpots(_active).Contains(tile)) PlayerAct(() => _battle.Order(CommandKind.Berry, _active, tile));
            return;
        }
        if (_mode != Mode.Command) return;
        var target = UnitOn(tile);
        if (target != null && !_active.Acted && _battle.Targets(_active).Contains(target))
            PlayerAct(() => _battle.Order(CommandKind.Attack, _active, target: target));
        else if (!_active.Moved && _reach.ContainsKey(tile))
            PlayerAct(() => _battle.Order(CommandKind.Move, _active, tile));
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
        else if (_mode == Mode.Skill)
        {
            foreach (var t in _skillTiles) yield return (t, new Color(1f, 0.55f, 0.15f, 0.3f));
            if (SkillAim() is { } aim)
                foreach (var t in _battle.SkillArea(_active, _skill, aim)) yield return (t, new Color(Gold, 0.55f));
        }
        // Skills going off: their area flashes for a moment.
        double now = Time.GetTicksMsec() / 1000.0;
        _flashes.RemoveAll(f => f.until < now);
        foreach (var (area, color, until) in _flashes)
            foreach (var t in area) yield return (t, new Color(color, (float)Mathf.Clamp((until - now) * 1.6, 0, 0.7)));
    }

    public Vector2I? HoverTile => _mode == Mode.Over ? null : _hoverTile;
    public IEnumerable<Vector2I> Berries => _berries;
    public Unit ActiveUnit => _mode == Mode.Over ? null : _active;

    // ------------------------------------------------------------------ panel

    void RefreshPanel()
    {
        if (_battle == null || _info == null) return;
        foreach (var child in _info.GetChildren()) { _info.RemoveChild(child); child.QueueFree(); }
        var hover = UnitOn(_hoverTile);
        var u = hover ?? _active;
        if (u != null) UnitRows(u);
        if (hover != null && _mode == Mode.Command && hover.Side != _active.Side && _battle.Targets(_active).Contains(hover))
        {
            var (dmg, hit) = _battle.Preview(_active, hover);
            _info.AddChild(new HSeparator());
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(UI.Ui.Picture(SkillIcons.Attack(_active.Weapon, 2)));
            row.AddChild(UI.Ui.Chips(new UI.Chip[] { new("atk", $"~{dmg}", Ink, "damage"), new("hit", $"{hit}%", Ink, "chance to hit") }, 20));
            _info.AddChild(row);
        }
        if (_mode == Mode.Skill) SkillRows();
        if (_mode == Mode.Berry)
        {
            _info.AddChild(new HSeparator());
            _info.AddChild(UI.Ui.Chips(new UI.Chip[] { new("berry", $"{_battle.Berries}", Gold), new("mini:area", "green tile", Ink), new("undo", "Esc", Dim) }));
        }
        else if (_mode == Mode.Command && !_active.Moved || _mode == Mode.Command && !_active.Acted)
        {
            // What the coloured tiles mean, once, instead of instructions.
            var legend = new List<UI.Chip>();
            if (!_active.Moved) legend.Add(new(SkillIcons.MoveGlyph(_active.MoveType), "Move", Cyan, "click a blue tile"));
            if (!_active.Acted) legend.Add(new(SkillIcons.WeaponGlyph(_active.Weapon), "Attack", Magenta, "click a red-marked foe"));
            _info.AddChild(new HSeparator());
            _info.AddChild(UI.Ui.Chips(legend, 16));
        }
    }

    static readonly string[] SizeWords = { "tiny", "small", "medium", "large", "huge" };

    void UnitRows(Unit u)
    {
        var c = u.Creature;
        // Portrait, name, and what it is.
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        if (_views.TryGetValue(u, out var view))
        {
            var portrait = UI.Ui.Picture(view.Texture);
            portrait.CustomMinimumSize = new Vector2(76, 76);
            portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            portrait.FlipH = u.Side == Side.Wild;
            head.AddChild(portrait);
        }
        var names = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        names.AddThemeConstantOverride("separation", 0);
        names.AddChild(UI.Ui.Text(u.Name, 22, u.Side == Side.Player ? Cyan : Magenta));
        string what = $"{SizeWords[(int)c.Size]} {c.Species.Plan.ToString().ToLowerInvariant()}" + (c.Species.Strain != Strain.Natural ? $" · {c.Species.Strain.Label().ToLowerInvariant()}" : "");
        names.AddChild(UI.Ui.Text(what, 15, Dim, 4));
        head.AddChild(names);
        _info.AddChild(head);

        // Stats: a glyph before every number, tinted when a boost or curse moves it.
        int hp = Mathf.Max(0, view?.DisplayHp ?? u.Hp);
        float frac = hp / (float)u.MaxHp;
        Color Moved(int now, int baseValue) => now > baseValue ? UI.Ui.Good : now < baseValue ? UI.Ui.Bad : Ink;
        var stats = new List<UI.Chip>
        {
            new("hp", $"{hp}/{u.MaxHp}", frac > 0.5f ? Ink : frac > 0.25f ? Gold : UI.Ui.Bad, "HP"),
            new("atk", u.AtkNow.ToString(), Moved(u.AtkNow, u.Atk), "ATK: damage"),
            new("def", u.DefNow.ToString(), Moved(u.DefNow, u.Def), "DEF: damage taken"),
            new("spd", u.SpdNow.ToString(), Moved(u.SpdNow, u.Spd), "SPD: turn order and hit chance"),
            new("move", u.MoveNow.ToString(), Moved(u.MoveNow, u.Move), "MOVE: tiles per turn"),
        };
        int evade = u.Mod(Classes.StatKind.Evade);
        if (evade != 0) stats.Add(new("evade", (evade > 0 ? "+" : "") + evade + "%", evade > 0 ? UI.Ui.Good : UI.Ui.Bad, "harder (or easier) to hit"));
        if (u.Thorns > 0) stats.Add(new("thorns", u.Thorns.ToString(), Ink, "melee attackers take this much"));
        if (u.Range > 1) stats.Add(new("range", u.Range.ToString(), Ink, "attack reach"));
        _info.AddChild(UI.Ui.Chips(stats, 19));

        // Statuses.
        var status = new List<UI.Chip>();
        if (u.PoisonTurns > 0) status.Add(new("poison", $"{u.PoisonDmg}x{u.PoisonTurns}", UI.Ui.Good, $"poisoned: {u.PoisonDmg} a turn for {u.PoisonTurns} turns"));
        if (u.Stunned) status.Add(new("stun", "", null, "stunned: loses its next turn"));
        if (u.Rooted) status.Add(new("root", "", null, "rooted: can't move"));
        if (u.TauntedBy != null) status.Add(new("taunt", u.TauntedBy.Name, UI.Ui.Bad, "taunted: must go after this creature"));
        foreach (var (stat, amount, turns) in u.Buffs)
            status.Add(new(SkillIcons.StatGlyph(stat), $"{(amount > 0 ? "+" : "")}{amount}{(stat == Classes.StatKind.Evade ? "%" : "")} ({turns})", amount > 0 ? UI.Ui.Good : UI.Ui.Bad, $"{stat} for {turns} more turns"));
        if (status.Count > 0) _info.AddChild(UI.Ui.Chips(status, 17));

        // Class, then loadout and temper; the long words live in the tooltips.
        var (weapon, armor, move) = c.Loadout();
        var kit = new List<UI.Chip>
        {
            new(Classes.TierBook.Glyph(u.Tier), u.Tier.ToString(), Color.FromHtml(Classes.TierBook.Hex(u.Tier)), Classes.TierBook.Describe(c, u.Tier)),
        };
        if (u.Tamer is { } w) kit.Add(new(w.Glyph, w.Name, u.Boost != null ? Cyan : Dim, u.Boost != null ? $"Led by {w.Name}: {w.Text}" : $"Led by {w.Name}, whose boost reaches only {w.LeadsHint}."));
        if (u.ClassName != null) kit.Add(new(SkillIcons.ClassEmblem(u.ClassName), u.ClassName, Gold, "class"));
        if (u.Training?.Secondary != null) kit.Add(new(SkillIcons.ClassEmblem(u.Training.Secondary), u.Training.Secondary, Ink, "secondary skill set"));
        _info.AddChild(UI.Ui.Chips(kit, 17));
        var gear = new List<UI.Chip>
        {
            new(SkillIcons.WeaponGlyph(u.Weapon), u.Weapon.ToString(), Ink, "weapon: " + weapon),
            new(SkillIcons.ArmorGlyph(u.Armor), u.Armor.ToString(), Ink, "armour: " + armor),
            new(SkillIcons.MoveGlyph(u.MoveType), u.MoveType.ToString(), Ink, "moves: " + move),
            new(SkillIcons.AttitudeGlyph(u.Attitude), u.Attitude.ToString(), Ink, $"{u.Attitude}: {AttitudeInfo.Reaction(u.Attitude)}"),
        };
        _info.AddChild(UI.Ui.Chips(gear, 16));

        // Skills, passive and reaction as icons.
        if (u.Skills.Count > 0 || u.Passive != null || u.Reaction != null || u.Traits.Count > 0)
        {
            var icons = new HFlowContainer();
            icons.AddThemeConstantOverride("h_separation", 4);
            foreach (var s in u.Skills) icons.AddChild(UI.Ui.Picture(SkillIcons.Skill(s, 2), $"{s.Name} ({s.Class})\n{s.Text}"));
            foreach (var p in new[] { u.Equipped, u.ShinyPassive, u.GearPassive, u.TierPassive, u.Reaction }.Concat(u.Traits).Where(p => p != null))
                icons.AddChild(UI.Ui.Picture(SkillIcons.Passive(p, 2), $"{p.Name} ({(p == u.ShinyPassive ? "shiny" : p == u.GearPassive ? "gear" : p == u.TierPassive ? "tier" : p.Class == "Trait" ? "trait" : p.Kind == Classes.PassiveKind.Reaction ? "reaction" : "support")})\n{p.Text}"));
            _info.AddChild(icons);
        }

        if (u.Free)
        {
            var hint = UI.Ui.Text(RecruitShort(u), 16, Gold, 4);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.TooltipText = _battle.RecruitHint(u);
            hint.MouseFilter = MouseFilterEnum.Stop;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            row.AddChild(UI.Ui.Picture(UI.Glyphs.Get("star", 2)));
            hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(hint);
            _info.AddChild(row);
        }
    }

    /// <summary>How to win this one over, in a few words (the full hint is its tooltip).</summary>
    string RecruitShort(Unit u) => u.Attitude switch
    {
        Attitude.Proud => u.HitBy.Count <= 1 ? "recruit: beat it one-on-one" : "too many hands on it: it will flee",
        Attitude.Skittish => "recruit: corner it",
        Attitude.Curious => $"recruit: lure it with a glowberry (B, {_battle.Berries} left)",
        Attitude.Aggressive => $"recruit: survive its attacks {u.Weathered}/3",
        _ => "recruit: win over its packmates first",
    };

    void SkillRows()
    {
        _info.AddChild(new HSeparator());
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(UI.Ui.Picture(SkillIcons.Skill(_skill, 3)));
        var names = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        names.AddThemeConstantOverride("separation", 0);
        names.AddChild(UI.Ui.Text(_skill.Name, 21, Gold));
        names.AddChild(UI.Ui.Text($"{_skill.Class} · {SkillIcons.KindName(SkillIcons.KindOf(_skill))}", 15, Dim, 4));
        head.AddChild(names);
        head.TooltipText = _skill.Text;
        _info.AddChild(head);
        _info.AddChild(UI.Ui.Chips(UI.Ui.SkillChips(_skill), 18));

        if (SkillAim() is not { } aim) { _info.AddChild(UI.Ui.Text("aim at an orange tile", 16, Dim, 4)); return; }
        if (_skill.Target == Classes.TargetKind.Tile) { _info.AddChild(UI.Ui.Chips(new UI.Chip[] { new("swirl", "here", Ink), new("mini:area", "Enter", Dim) })); return; }
        var rows = _battle.SkillOutlook(_active, _skill, aim);
        if (rows.Count == 0) _info.AddChild(UI.Ui.Text("nothing there", 16, Dim, 4));
        foreach (var (t, dmg, hit, heal) in rows.Take(5))
        {
            var chips = new List<UI.Chip>();
            if (dmg != null) chips.Add(new("atk", $"~{dmg}", Ink, "damage"));
            if (hit != null) chips.Add(new("hit", $"{hit}%", Ink, "chance to land"));
            if (heal != null) chips.Add(new("heal", $"+{heal}", UI.Ui.Good, "HP recovered"));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var name = UI.Ui.Text(t.Name, 17, t.Side == Side.Player ? Cyan : Magenta, 4);
            name.CustomMinimumSize = new Vector2(150, 0);
            name.ClipText = true;
            row.AddChild(name);
            row.AddChild(UI.Ui.Chips(chips, 17));
            _info.AddChild(row);
        }
        if (rows.Count > 5) _info.AddChild(UI.Ui.Text($"+{rows.Count - 5} more", 15, Dim, 4));
    }

    void RefreshObjective()
    {
        var target = _battle.Target?.Creature;
        string text = Mission != null ? Mission.Brief(target) : "Rout the pack: drive off or recruit every wild creature.";
        if (_battle.Goal == Objective.Survive) text += $"   Round {System.Math.Max(1, _battle.Round)}/{_battle.SurviveRounds}";
        if (_battle.Goal == Objective.Duel) text += $"   Round {System.Math.Max(1, _battle.Round)}/{Battle.DuelRounds}";
        if (Mission?.Sighting != null) text += $"   Sighting: {Mission.Sighting}";
        _objective.Text = text;
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
                    _follow = _views[t.U];
                    if (t.U.Side == Side.Player && !AutoPlay) Audio.Sound.Play("turn");
                    RefreshObjective();
                    RefreshStrip();
                    foreach (var v in _views.Values) v.QueueRedraw();
                    break;
                case MoveEvent m:
                    _follow = _views[m.U];
                    await _views[m.U].Walk(m.Path);
                    break;
                case StrikeEvent s:
                    _follow = null;
                    _look = Clamped((Body(_views[s.A]) + Body(_views[s.T])) / 2);
                    Audio.Sound.Play("swing", UnitView.Heft(s.A));
                    await _views[s.A].Lunge(_views[s.T]);
                    break;
                case DamageEvent d:
                    _fx.Hit(Body(_views[d.U]), d.Amount, d.Miss);
                    // A heavy blow (a quarter of its HP or more) lands lower.
                    if (d.Miss) Audio.Sound.Play("miss"); else Audio.Sound.Play("hit", d.Amount * 4 >= d.U.MaxHp ? 0.8f : 1f);
                    _views[d.U].Hurt(d.Amount, d.Miss);
                    Popup(_views[d.U], d.Miss ? "miss" : d.Amount.ToString(), d.Miss ? Dim : Ink);
                    await Wait(0.22f);
                    break;
                case PushEvent p:
                    Audio.Sound.Play("knock");
                    await _views[p.U].Slide(p.To);
                    break;
                case FaintEvent f:
                    Audio.Sound.Play("faint", UnitView.Heft(f.U));
                    await _views[f.U].Vanish();
                    break;
                case RecruitEvent r:
                    _views[r.U].Recruited();
                    Audio.Sound.Play("recruit");
                    _fx.Burst(Body(_views[r.U]), _views[r.U].Position, FxTheme.Holy);
                    Popup(_views[r.U], "joins!", Cyan);
                    AddLog($"{r.U.Name} joins you: {r.Why}.");
                    await Wait(0.7f);
                    break;
                case HealEvent h:
                    _views[h.U].Heal(h.Amount);
                    Audio.Sound.Play("heal");
                    _fx.Rise(Body(_views[h.U]), _views[h.U].Position, FxTheme.Leaf);
                    Popup(_views[h.U], $"+{h.Amount}", new Color(0.5f, 1f, 0.6f));
                    await Wait(0.2f);
                    break;
                case BerryEvent b:
                    if (b.Placed) _berries.Add(b.Tile); else _berries.Remove(b.Tile);
                    Audio.Sound.Play("berry", b.Placed ? 1f : 1.3f);
                    _overlay.QueueRedraw();
                    break;
                case LogEvent l:
                    AddLog(l.Text);
                    break;
                case SkillEvent sk:
                {
                    // Call the skill's name over the user and flash the ground it covers.
                    var view = _views[sk.U];
                    _follow = null;
                    _look = Clamped((Body(view) + TileMid(sk.Center)) / 2);
                    Popup(view, sk.Skill.Name, Gold, -10, SkillIcons.Skill(sk.Skill, 1));
                    var color = sk.Skill.Target is Classes.TargetKind.Ally or Classes.TargetKind.Self ? Cyan : Color.FromHtml("#ff8a3d");
                    _flashes.Add((sk.Area, color, Time.GetTicksMsec() / 1000.0 + 0.45));
                    _overlay.QueueRedraw();
                    var target = sk.Center != sk.U.Pos ? _views.Values.FirstOrDefault(x => x.Unit.Tiles.Contains(sk.Center) && x != view) : null;
                    bool melee = sk.Skill.Power > 0 && (sk.Skill.Range <= 1 || sk.Skill.Leap) && !sk.Skill.AroundSelf;
                    if (target != null) view.Face(target.Position - view.Position);
                    else if (sk.Center != sk.U.Pos) view.Face(FeetAt(sk.U, sk.Center) - view.Position);
                    await SkillEffect(sk, view, target, melee);
                    break;
                }
                case StatusEvent st:
                    Audio.Sound.Play(StatusSound(st.Text));
                    _fx.Status(Body(_views[st.U], 0.9f), _views[st.U].Position, st.Text);
                    Popup(_views[st.U], st.Text, Color.FromHtml("#c79bff"), 8);
                    _views[st.U].QueueRedraw();
                    await Wait(0.15f);
                    break;
            }
        }
        RefreshPanel();
    }

    /// <summary>
    /// A skill going off, in its theme: a strike lands with a burst; a ranged skill flies from the
    /// user to its target and bursts over the area; skills around the user ripple out from it;
    /// heals and boosts shine on whoever they reach.
    /// </summary>
    /// <summary>The sound for a status mark, by its words.</summary>
    static string StatusSound(string text)
    {
        string t = text.ToLowerInvariant();
        return t.Contains("stun") ? "stun" : t.Contains("poison") ? "fx_venom" : t.Contains('+') ? "boost" : t.Contains('-') ? "debuff" : "status";
    }

    async Task SkillEffect(SkillEvent sk, UnitView view, UnitView target, bool melee)
    {
        var theme = SkillFx.ThemeOf(sk.Skill, sk.U);
        var element = sk.U.Creature?.Species.Element;
        var kind = SkillIcons.KindOf(sk.Skill);
        var reached = _views.Values.Where(v => !v.Unit.Gone && v.Unit.Tiles.Any(sk.Area.Contains)).ToList();
        if (melee && target != null)
        {
            Audio.Sound.Play("swing", UnitView.Heft(sk.U));
            var lunge = view.Lunge(target);
            await Wait(0.19f);
            Audio.Sound.Fx(theme);
            _fx.Burst(Body(target), target.Position, theme, element);
            foreach (var v in reached.Where(v => v != target && v != view)) _fx.Burst(Body(v), v.Position, theme, element, 0.6f);
            await lunge;
            return;
        }
        var cast = view.Cast();
        Audio.Sound.Play("cast");
        await Wait(0.18f);
        if (kind is SkillIcons.Kind.Heal or SkillIcons.Kind.Boost)
        {
            if (kind == SkillIcons.Kind.Boost) Audio.Sound.Fx(theme == FxTheme.Strike ? FxTheme.Boost : theme);
            if (sk.Skill.AroundSelf) _fx.Wave(view.Position, (sk.Skill.Radius + 0.6f) * IsoArt.TW / 2f, theme, element);
            foreach (var v in reached.Where(v => v.Unit.Side == sk.U.Side))
            {
                if (kind == SkillIcons.Kind.Heal) _fx.Burst(Body(v), v.Position, FxTheme.Holy, element, 0.5f);
                else _fx.Rise(Body(v), v.Position, theme == FxTheme.Strike ? FxTheme.Boost : theme, element);
            }
        }
        else if (kind == SkillIcons.Kind.Move)
        {
            Audio.Sound.Fx(FxTheme.Wind);
            _fx.Burst(Body(view, 0.25f), view.Position, FxTheme.Wind, element, 0.6f);
        }
        else if (sk.Skill.AroundSelf)
        {
            Audio.Sound.Fx(theme);
            _fx.Wave(view.Position, (sk.Skill.Radius + 0.6f) * IsoArt.TW / 2f, theme, element);
            await Wait(0.12f);
            foreach (var v in reached.Where(v => v != view)) _fx.Burst(Body(v), v.Position, theme, element, 0.8f);
        }
        else
        {
            var to = target != null ? Body(target) : TileMid(sk.Center) + new Vector2(0, -6);
            await _fx.Projectile(Body(view, 0.6f), to, theme, element);
            Audio.Sound.Fx(theme);
            if (sk.Area.Count > 1)
            {
                // A blast: one wave over the whole area, debris on every tile, the full hit on whoever's caught.
                _fx.Wave(TileMid(sk.Center), (sk.Skill.Radius + 0.6f) * IsoArt.TW / 2f, theme, element);
                foreach (var t in sk.Area.Where(t => !reached.Any(v => v.Unit.Tiles.Contains(t))))
                    _fx.Burst(TileMid(t) + new Vector2(0, -3), TileMid(t), theme, element, 0.35f, piece: true);
                foreach (var v in reached.Where(v => v != view)) _fx.Burst(Body(v), v.Position, theme, element, 0.8f);
            }
            else if (target != null) _fx.Burst(to, target.Position, theme, element);
            else _fx.Burst(to, TileMid(sk.Center), theme, element, 0.7f);
        }
        await cast;
    }

    /// <summary>A point up a creature's body: 0 its feet, 1 the top of its sprite.</summary>
    static Vector2 Body(UnitView v, float up = 0.5f) => v.Position + new Vector2(0, -v.SpriteHeight * up);

    Vector2 TileMid(Vector2I t) => _art.TileCenter(new Vector2(t.X, t.Y), _battle.Map.HeightAt(t));

    /// <summary>Text rising over a creature (board pixels, so it scales with the board), with an icon above it if given.</summary>
    void Popup(UnitView v, string text, Color color, float dy = 0, Texture2D icon = null)
    {
        var l = Ui.Text(text, 9, color, 3);
        l.ZIndex = 400;
        l.Position = v.Position + new Vector2(-40, -v.SpriteHeight - 14 + dy);
        l.Size = new Vector2(80, 12);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        if (icon != null) l.AddChild(new TextureRect { Texture = icon, Position = new Vector2(40 - icon.GetWidth() / 2f, -icon.GetHeight() - 1), TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore });
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

/// <summary>
/// One creature on the board: sprite, shadow, side ring, HP bar, and its animations. It faces one
/// of the four grid directions, the screen diagonals: its front view facing down-left or (mirrored)
/// down-right, its back view facing up-left or (mirrored) up-right.
/// </summary>
/// <summary>A creature on the battle board: an <see cref="Actor"/> with its ring, HP bar and status marks.</summary>
public partial class UnitView : Actor
{
    public Unit Unit;
    public int DisplayHp;

    BattleScreen _screen;
    Color _ring;

    public void Setup(Unit u, BattleScreen screen)
    {
        Unit = u;
        _screen = screen;
        DisplayHp = u.Hp;
        _ring = u.Side == Side.Player ? Color.FromHtml("#4fe3ff") : Color.FromHtml("#ff4f9a");
        var creature = u.Creature;
        Begin((view, phase, blink, pose) => CreatureSculptor.Render(creature, false, view, phase, blink, pose), creature.Seed, u.Side == Side.Wild);
    }

    protected override Vector2 FeetAt(Vector2I tile) => _screen.FeetAt(Unit, tile);
    protected override int DepthAt(Vector2I tile) => BattleScreen.Depth(Unit, tile);

    protected override void Stepped(int step)
    {
        if (Unit.MoveType != MoveType.Fly) Audio.Sound.Play("step", Heft(Unit), -4);
        else if (step % 2 == 1) Audio.Sound.Play("flap", Heft(Unit), -2);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
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
        float w = 18 * Unit.Size, y = -SpriteHeight - 3;
        float frac = Mathf.Clamp(DisplayHp / (float)Unit.MaxHp, 0, 1);
        DrawRect(new Rect2(-w / 2 - 1, y - 1, w + 2, 4), new Color(0.05f, 0.03f, 0.08f, 0.9f));
        DrawRect(new Rect2(-w / 2, y, w * frac, 2), frac > 0.5f ? new Color(0.4f, 1f, 0.6f) : frac > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.3f, 0.35f));
        // The mission target wears a gold diamond.
        if (_screen.Battle.Target == Unit && Unit.Side == Side.Wild)
        {
            var m = new Vector2(w / 2 + 6, y);
            DrawColoredPolygon(new[] { m + new Vector2(0, -4), m + new Vector2(3, 0), m + new Vector2(0, 4), m + new Vector2(-3, 0) }, Color.FromHtml("#f2c14e"));
        }
        // Status marks above the HP bar, shaped so they read without colour: poison drop, stun star,
        // root, taunt, and arrows for boosts and curses.
        var pips = new List<string>();
        if (Unit.PoisonTurns > 0) pips.Add("poison");
        if (Unit.Stunned) pips.Add("stun");
        if (Unit.Rooted) pips.Add("root");
        if (Unit.TauntedBy != null) pips.Add("taunt");
        if (Unit.Buffs.Any(b => b.amount > 0)) pips.Add("up");
        if (Unit.Buffs.Any(b => b.amount < 0)) pips.Add("down");
        for (int i = 0; i < pips.Count; i++)
            DrawTexture(UI.Glyphs.Mini(pips[i], 1), new Vector2(-w / 2 - 1 + i * 6, y - 9));

        if (active)
        {
            // A marker over the active creature.
            var m = new Vector2(0, y - 6 - (pips.Count > 0 ? 7 : 0) + Mathf.Sin(Time.GetTicksMsec() / 200f) * 1.5f);
            DrawColoredPolygon(new[] { m + new Vector2(-3, -3), m + new Vector2(3, -3), m + new Vector2(0, 1) }, _ring);
        }
    }

    /// <summary>A sound's pitch for a creature's size: big ones lower and heavier.</summary>
    public static float Heft(Unit u) => u.Creature.Size switch
    {
        SizeClass.Tiny => 1.35f, SizeClass.Small => 1.15f, SizeClass.Large => 0.85f, SizeClass.Huge => 0.7f, _ => 1f,
    };

    public void Hurt(int amount, bool miss)
    {
        if (miss) return;
        DisplayHp -= amount;
        Flash();
        _ = Act(Pose.Hurt, 0.32f);
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
}
