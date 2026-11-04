using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheFartherWeStray.Classes;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Ranching;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.World;

/// <summary>
/// The country map (the user's direction, 2026-11-02): every place, open or not, the roads between
/// them, and your party where it stands. Pick a place to see its contracts, duels and main missions;
/// Depart travels there along the roads (a day each) and starts the battle. A locked place says which
/// main mission opens it and what that mission is waiting for.
/// </summary>
public partial class CountryScreen : Control
{
    public Ranch Ranch;
    public Action<Mission> Fight;
    public Action Back;
    public bool DemoDispatch;   // --dispatchdemo: open on the first dispatch's chooser (screenshots)
    public bool DemoShop;       // --shopdemo: open on the shop where you stand (screenshots)
    public Action ToTitle;      // the pause menu's way back to the title

    // The map at 4x on a desktop with the panel on the right; on a phone at 2x, the panel beside it.
    static int Scale => Phone.On ? 2 : 4;
    static Vector2 MapAt => Phone.On ? new(24, 112) : new(40, 112);
    float _panelX, _panelW, _overY;   // the right-hand panel, and where the shop and the chooser open
    static readonly Color Gold = Color.FromHtml("#f2c14e"), Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#9a91b4");
    static readonly Color Pink = Color.FromHtml("#ff4f9a"), Green = Color.FromHtml("#4ff0a0"), Cyan = Color.FromHtml("#4fe3ff");

    string _selected, _hover;
    Vector2 _token;
    bool _travelling;
    List<Mission> _board = new();
    MissionBoardPanel _missions;
    PanelContainer _chooser, _shop;
    readonly Dictionary<int, (Texture2D tex, Vector2I anchor)> _packs = new(); // each roaming pack's leader, painted once
    VBoxContainer _place;
    Label _status;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        TextureFilter = TextureFilterEnum.Nearest;
        Audio.Music.Play("map");
        _board = Country.Board(Ranch);
        _token = Screen(Country.Get(Ranch.Location));

        var vp = GetViewportRect().Size;
        _panelX = Phone.On ? MapAt.X + CountryArt.W * Scale + 24 : vp.X - 750;
        _panelW = Phone.On ? vp.X - _panelX - 16 : 720;
        _overY = Phone.On ? 96 : 262;
        var back = new Button { Text = Phone.Keys("Home  (Esc)", "Home  (B)"), FocusMode = FocusModeEnum.None, Position = new Vector2(vp.X - 180, 22), CustomMinimumSize = new Vector2(150, 40) };
        back.AddThemeFontSizeOverride("font_size", 16);
        back.Pressed += () => Back?.Invoke();
        AddChild(back);

        _place = new VBoxContainer { Position = new Vector2(_panelX, 96), Size = new Vector2(_panelW, 160) };
        _place.AddThemeConstantOverride("separation", 6);
        AddChild(_place);
        _missions = new MissionBoardPanel { Ranch = Ranch, Position = new Vector2(_panelX, 262), Size = new Vector2(_panelW, vp.Y - 310), Depart = Depart };
        AddChild(_missions);
        // Under the map on a phone (two lines), along the foot on a desktop.
        _status = Phone.On ? new Label { Position = new Vector2(MapAt.X, MapAt.Y + CountryArt.H * Scale + 14), Size = new Vector2(_panelX - MapAt.X - 20, 60) }
            : new Label { Position = new Vector2(40, vp.Y - 34), Size = new Vector2(1100, 24) };
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.AddThemeFontSizeOverride("font_size", 16);
        _status.AddThemeColorOverride("font_color", Gold);
        AddChild(_status);

        // Start on wherever there's something to do: a main mission first, then here, then the nearest contract.
        var first = _board.FirstOrDefault(m => m.Story != null) ?? _board.FirstOrDefault(m => m.Location == Ranch.Location)
            ?? _board.OrderBy(m => Country.Days(Ranch, m.Location)).FirstOrDefault();
        Select(first?.Location ?? Ranch.Location);
        _status.Text = Phone.Keys("Pick a place, then a mission; Depart (F) travels there and starts it. Esc: back to the ranch.",
            "D-pad: pick a place.  A: its missions, then Depart.  B: back home.  Start: menu.");
        if (DemoDispatch && _board.FirstOrDefault(m => m.Dispatch != null) is { } job) { Select(job.Location); Choose(job.Dispatch); }
        if (DemoShop && ShopBook.At(Ranch, Ranch.Location).Count > 0) { Select(Ranch.Location); OpenShop(Ranch.Location); }
    }

    static Vector2 Screen(LocationDef l) => MapAt + new Vector2(l.At.X, l.At.Y) * Scale;

    void Select(string id)
    {
        _selected = id;
        var l = Country.Get(id);
        foreach (var child in _place.GetChildren()) child.QueueFree();
        bool open = Country.IsOpen(Ranch, l);
        var side = l.Side switch { Side.City => ("the city above", Gold), Side.Undercity => ("the undercity", Pink), _ => ("the wilds", Green) };
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(Ui.Picture(Glyphs.Get(open ? l.Glyph : "lock", 2)));
        head.AddChild(Ui.Text(open ? l.Name : "Unknown place", 24, open ? Ink : Dim));
        head.AddChild(Ui.Text(side.Item1, 16, side.Item2, 6));
        _place.AddChild(head);

        if (!open)
        {
            var opener = StoryBook.Get(l.OpenedBy);
            _place.AddChild(Wrap($"Locked. The main mission at {Country.Get(opener.Location).Name} opens the road here.", 16, Dim));
            if (opener.After != null && !Ranch.Story.Contains(opener.After))
                _place.AddChild(Wrap($"First: {StoryBook.Get(opener.After).Title}.", 16, Dim));
            else
                _place.AddChild(Ui.Chips(StoryBook.Needs(Ranch, opener).Select(n => new Chip(n.met ? "check" : "dot", n.text, n.met ? Ui.Good : Ink)).ToArray(), 16));
            _missions.Visible = false;
            QueueRedraw();
            return;
        }
        string blurb = l.Blurb ?? BattleMap.ByName(l.MapName).Blurb;
        _place.AddChild(Wrap(blurb, 15, Dim));
        int days = Country.Days(Ranch, id);
        _place.AddChild(Ui.Chips(new[]
        {
            new Chip("move", id == Ranch.Location ? "you are here" : $"{days} {(days == 1 ? "day" : "days")} away", id == Ranch.Location ? Green : Ink, "each road takes a day"),
            new Chip("clock", $"day {Ranch.Day}", Dim, "days travelled so far"),
        }, 16));
        CloseShop();
        if (ShopBook.At(Ranch, id).Count > 0)
        {
            var shop = new Button { Text = Phone.Keys("Shop  (S)", "Shop"), FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(140, 36), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
            shop.Pressed += () => OpenShop(id);
            _place.AddChild(shop);
        }

        var here = _board.Where(m => m.Location == id).ToList();
        bool hires = Country.Hires(Ranch, l);
        _missions.Visible = l.MapName != null && hires;
        if (l.MapName == null)
            _place.AddChild(Wrap("Home. Back at the ranch (Esc) your beasts rest, breed and train.", 16, Ink));
        else if (!hires)
        {
            // The city's board, shut until someone with connections up here brings you in (wording is a placeholder for the story's).
            _place.AddChild(Wrap("The city's board is shut to you: nobody up here hires from below without someone to vouch for them.", 16, Gold));
            _place.AddChild(Wrap($"First: {StoryBook.Get(Country.CityBoardOpenedBy).Title}.", 16, Dim));
        }
        else
        {
            _missions.Source = () => here;
            _missions.Heading = here.Count == 0 ? "NOTHING ON OFFER HERE TODAY" : $"{Country.BoardOf(l.Side)} · {l.Name}".ToUpperInvariant();
            string f = Phone.On ? "" : "  (F)";
            _missions.DepartLabelFor = m => m.Dispatch != null ? $"Send a beast{f}" : id == Ranch.Location ? $"Depart{f}" : $"Travel {days}d & depart{f}";
            _missions.Selected = 0;
            _missions.Refresh();
            _missions.QueueRedraw();
        }
        QueueRedraw();
    }

    Label Wrap(string text, int size, Color color)
    {
        var label = Ui.Text(text, size, color, 4);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(_panelW - 20, 0);
        return label;
    }

    async void Depart(Mission m)
    {
        if (_travelling || _chooser != null) return;
        if (m.Dispatch != null) { Choose(m.Dispatch); return; }
        if (Ranch.Squad.Count == 0) { _status.Text = "Pick at least one beast for the squad at the ranch (S)."; return; }
        _travelling = true;
        Audio.Sound.Play("depart");
        if (await WalkTo(m.Location, m.Roamer)) return;
        // Going after a pack that moved on while you travelled finds only its tracks.
        if (m.Roamer != 0)
        {
            _status.Text = "The pack has moved on.";
            _travelling = false;
            _board = Country.Board(Ranch);
            Select(Ranch.Location);
            return;
        }
        Fight?.Invoke(m);
    }

    /// <summary>
    /// Walks the token along the roads, a day at every place on the way. A roaming pack where you
    /// arrive stops you and the fight with it starts (true); otherwise you're there (false).
    /// </summary>
    async Task<bool> WalkTo(string dest, int hunting = 0)
    {
        var route = Country.Route(Ranch, Ranch.Location, dest) ?? new List<string> { dest };
        int road = 0;
        foreach (var stop in route.Skip(1))
        {
            var tween = CreateTween();
            tween.TweenMethod(Callable.From<Vector2>(p => { _token = p; QueueRedraw(); }), _token, Screen(Country.Get(stop)), 0.35f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            await ToSignal(tween, Tween.SignalName.Finished);
            Audio.Sound.Play("step", 1.2f);
            if (Ranch.TravelStep(stop, road++ < Ranching.Farmyard.Carts(Ranch)) is { } met)
            {
                Ranch.Save();
                if (met.Id != hunting)
                {
                    _status.Text = $"A roaming pack at {Country.Get(stop).Name} blocks the road!";
                    Audio.Sound.Play("gong", 1.4f);
                    await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
                }
                Fight?.Invoke(Roaming.Battle(met));
                return true;
            }
        }
        Ranch.Save();
        return false;
    }

    /// <summary>The shop where a place's stock is: what it sells, what you have, buy where you stand.</summary>
    void OpenShop(string place)
    {
        CloseShop();
        _shop = new PanelContainer { Position = new Vector2(_panelX, _overY), Size = new Vector2(_panelW, 0) };
        _shop.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        _shop.AddChild(col);
        bool here = place == Ranch.Location;
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        head.AddChild(Ui.Text($"SHOP  ·  {Country.Get(place).Name}", 18, Gold));
        head.AddChild(Ui.ChipView(new Chip("flask", $"{Ranch.Chem} chem", Gold, "chem to spend"), 16));
        col.AddChild(head);
        if (!here)
        {
            int days = Country.Days(Ranch, place);
            col.AddChild(Wrap($"You're {days} {(days == 1 ? "day" : "days")} away: travel here to buy (roaming packs may be on the road).", 15, Dim));
            var go = new Button { Text = $"Travel here  ({days}d)", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(200, 40), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
            go.Pressed += () => TravelToShop(place);
            col.AddChild(go);
        }
        float tall = GetViewportRect().Size.Y - _overY - (here ? 140 : 200);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(_panelW - 30, Math.Min(here ? 560 : 500, tall)), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(rows);
        col.AddChild(scroll);
        foreach (var ware in ShopBook.At(Ranch, place))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            // A piece for the farmyard shows as itself, drawn as the yard draws it.
            var decor = DecorBook.OfWare(ware.Id);
            var machine = MachineBook.OfWare(ware.Id);
            var icon = ware.Id == ShopBook.Berry ? SkillIcons.Action("berry", 2)
                : decor != null ? Tactics.PropArt.Texture(decor.Prop, 1, "Ranch").tex
                : machine != null ? Tactics.PropArt.Texture(machine.Prop, 1, "Ranch").tex
                : AugmentBook.Get(ware.Id) is { } aug ? SkillIcons.Skill(Classes.ClassBook.Skill(aug.Skill), 2)
                : GearBook.IsGear(ware.Id) ? SkillIcons.Passive(Classes.ClassBook.Passive(ware.Id), 2) : SkillIcons.Skill(Classes.ClassBook.Skill(ware.Id), 2);
            var picture = Ui.Picture(icon, ShopBook.Text(ware.Id));
            if (decor != null || machine != null)
            {
                picture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                picture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                picture.CustomMinimumSize = new Vector2(44, 44);
                picture.TextureFilter = TextureFilterEnum.Nearest;
            }
            row.AddChild(picture);
            var words = new VBoxContainer { CustomMinimumSize = new Vector2(_panelW - 280, 0) };
            int owned = decor != null ? Ranch.Shed.GetValueOrDefault(decor.Id) : machine != null ? 0 : Ranch.Items.GetValueOrDefault(ware.Id);
            bool have = machine != null && Ranching.Farmyard.Owns(Ranch, machine.Id);
            string kind = machine != null ? (have ? "  (a machine: yours already)" : "  (a machine for the yard)") : decor != null ? "  (for the yard)" : AugmentBook.IsAugment(ware.Id) ? "  (augment)" : GearBook.IsGear(ware.Id) ? "  (gear)" : "";
            words.AddChild(Ui.Text($"{ShopBook.Name(ware.Id)}{kind}{(owned > 0 ? $"   ·   {owned} in the {(decor != null ? "shed" : "bag")}" : "")}", 16, decor != null || machine != null ? Gold : AugmentBook.IsAugment(ware.Id) ? Cyan : Ink));
            var text = Wrap(ShopBook.Text(ware.Id), 13, Dim);
            text.CustomMinimumSize = new Vector2(_panelW - 280, 0);
            words.AddChild(text);
            row.AddChild(words);
            int price = ShopBook.PriceAt(Ranch, ware, place);
            var buy = new Button { Text = price < ware.Price ? $"Buy  {price} (-15%)" : $"Buy  {price}", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 40), Disabled = !here || Ranch.Chem < price || have };
            var w = ware;
            buy.Pressed += () => { _status.Text = Ranch.Buy(w); Ranch.Save(); Audio.Sound.Play("confirm"); OpenShop(place); };
            row.AddChild(buy);
            rows.AddChild(row);
        }
        var close = new Button { Text = Phone.Keys("Close  (Esc)", "Close  (B)"), FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(160, 40), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        close.Pressed += CloseShop;
        col.AddChild(close);
        _missions.Visible = false;
        AddChild(_shop);
        if (Phone.On) FocusFirst(_shop);
    }

    void CloseShop()
    {
        if (_shop == null) return;
        _shop.QueueFree();
        _shop = null;
        _missions.Visible = Country.Get(_selected)?.MapName != null;
    }

    async void TravelToShop(string place)
    {
        if (_travelling) return;
        CloseShop();
        _travelling = true;
        if (await WalkTo(place)) return;
        _travelling = false;
        _board = Country.Board(Ranch);
        Select(place);
        OpenShop(place);
    }

    /// <summary>Who goes on a dispatch: everyone at home, best suited first, with their odds.</summary>
    void Choose(DispatchDef job)
    {
        _chooser = new PanelContainer { Position = new Vector2(_panelX, _overY), Size = new Vector2(_panelW, 0) };
        _chooser.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        _chooser.AddChild(col);
        col.AddChild(Wrap($"Who goes on \"{job.Title}\"? Away {job.Days} {(job.Days == 1 ? "day" : "days")}.", 18, Gold));
        col.AddChild(Wrap("Suits: " + string.Join(", ", job.Suits.Select(s => s.hint)) + ". The better it suits, the likelier it goes well.", 15, Dim));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(_panelW - 30, Math.Min(520, GetViewportRect().Size.Y - _overY - 220)), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(rows);
        col.AddChild(scroll);
        var home = Ranch.Creatures.Where(r => !Ranch.Away.ContainsKey(r.Id))
            .Select(r => (r, chance: Math.Min(96, DispatchBook.Chance(job, Ranch.Get(r.Id), r.Training))))
            .OrderByDescending(x => x.chance).ThenBy(x => x.r.Id);
        foreach (var (rec, chance) in home)
        {
            var c = Ranch.Get(rec.Id);
            var fits = job.Suits.Where(s => s.fits(c, rec.Training)).Select(s => s.hint).ToList();
            var b = new Button
            {
                Text = $"{rec.Name}  ·  {chance}%{(fits.Count > 0 ? "  ·  " + string.Join(", ", fits) : "")}{(Ranch.Squad.Contains(rec.Id) ? "  (leaves the squad)" : "")}",
                Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(_panelW - 40, 38), ClipText = true,
            };
            b.AddThemeFontSizeOverride("font_size", 16);
            int id = rec.Id;
            b.Pressed += () => Send(id, job);
            rows.AddChild(b);
        }
        var cancel = new Button { Text = Phone.Keys("Cancel  (Esc)", "Cancel  (B)"), FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(160, 40), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        cancel.Pressed += CloseChooser;
        col.AddChild(cancel);
        _missions.Visible = false;
        AddChild(_chooser);
        if (Phone.On) FocusFirst(_chooser);
    }

    void CloseChooser()
    {
        _chooser?.QueueFree();
        _chooser = null;
        _missions.Visible = true;
    }

    void Send(int id, DispatchDef job)
    {
        CloseChooser();
        _status.Text = Ranch.Send(id, job);
        Ranch.Save();
        Audio.Sound.Play("depart");
        _board = Country.Board(Ranch);
        Select(_selected);
    }

    public override void _Process(double delta) => QueueRedraw(); // the token bobs and the selection pulses

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion)
        {
            string over = Pick(motion.Position);
            if (over != _hover) { _hover = over; QueueRedraw(); }
        }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && Pick(click.Position) is { } id && !_travelling)
        {
            Audio.Sound.Play("click");
            Select(id);
        }
    }

    string Pick(Vector2 at) => Country.Locations.FirstOrDefault(l => at.DistanceTo(Screen(l)) < 28)?.Id;

    /// <summary>The pad lands on a panel's first button that can be pressed.</summary>
    static void FocusFirst(Control panel) =>
        Callable.From(() => All(panel).OfType<BaseButton>().FirstOrDefault(b => !b.Disabled && b.IsVisibleInTree())?.GrabFocus()).CallDeferred();

    static IEnumerable<Control> All(Node n)
    {
        foreach (var child in n.GetChildren())
        {
            if (child is Control c) yield return c;
            foreach (var d in All(child)) yield return d;
        }
    }

    /// <summary>The nearest place in an arrow's direction (on the screen), for the pad.</summary>
    void StepPlace(Key key)
    {
        var dir = key switch { Key.Up => Vector2.Up, Key.Down => Vector2.Down, Key.Left => Vector2.Left, _ => Vector2.Right };
        var from = Screen(Country.Get(_selected ?? Ranch.Location));
        var next = Country.Locations.Where(l => l.Id != _selected)
            .Select(l => (l, d: Screen(l) - from)).Where(x => x.d.Dot(dir) > 0.3f * x.d.Length())
            .OrderBy(x => x.d.Dot(dir) + 2 * Mathf.Abs(x.d.Cross(dir))).Select(x => x.l).FirstOrDefault();
        if (next == null) return;
        Audio.Sound.Play("tick");
        Select(next.Id);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } pad && !_travelling && _chooser == null && _shop == null)
        {
            bool inPanel = GetViewport().GuiGetFocusOwner() is { } owner && IsAncestorOf(owner);
            // Arrows with nothing focused pick a place; A goes into its missions (or its shop); B in there comes back out.
            if (!inPanel && pad.Keycode is Key.Up or Key.Down or Key.Left or Key.Right) { StepPlace(pad.Keycode); GetViewport().SetInputAsHandled(); return; }
            if (!inPanel && !pad.Echo && pad.Keycode is Key.Enter or Key.KpEnter && Phone.On)
            {
                if (_missions.Visible && _missions.Missions.Count > 0) _missions.FocusSelected(); else FocusFirst(_place);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (inPanel && !pad.Echo && pad.Keycode == Key.Escape) { GetViewport().GuiReleaseFocus(); GetViewport().SetInputAsHandled(); return; }
        }
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Phone.Start) { AddChild(new PauseMenu { Save = Ranch.Save, ToTitle = ToTitle }); GetViewport().SetInputAsHandled(); return; }
        if (k.Keycode == Key.Escape && _chooser != null) { CloseChooser(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.Escape && _shop != null) { CloseShop(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.S && _shop == null && _selected != null && ShopBook.At(Ranch, _selected).Count > 0) OpenShop(_selected);
        else if (k.Keycode is Key.Escape or Key.M) { Back?.Invoke(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.F && _missions.Visible && _missions.Missions.Count > 0) Depart(_missions.Missions[_missions.Selected]);
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        DrawRect(new Rect2(Vector2.Zero, Size), Color.FromHtml("#0b0913"));
        DrawString(font, new Vector2(40, 52), "THE COUNTRY", HorizontalAlignment.Left, 400, 30, Gold);
        DrawTexture(Glyphs.Get("flask", 2), new Vector2(290, 30));
        DrawString(font, new Vector2(318, 50), $"{Ranch.Chem}", HorizontalAlignment.Left, 120, 20, Gold);
        // Standing with each side: a bar from -100 to 100, and the number.
        void Bar(float x0, string glyph, string label, int value, Color color)
        {
            DrawTexture(Glyphs.Get(glyph, 2), new Vector2(x0, 30));
            DrawRect(new Rect2(x0 + 30, 38, 120, 10), new Color(0.15f, 0.12f, 0.22f));
            DrawRect(new Rect2(x0 + 90, 36, 1, 14), Dim);
            float f = value / 100f;
            DrawRect(f >= 0 ? new Rect2(x0 + 90, 38, 60 * f, 10) : new Rect2(x0 + 90 + 60 * f, 38, -60 * f, 10), color);
            DrawString(font, new Vector2(x0 + 158, 50), $"{label} {value}", HorizontalAlignment.Left, 160, 16, color);
        }
        Bar(400, "sun", "city", Ranch.CityStanding, Gold);
        Bar(640, "moon", "undercity", Ranch.UnderStanding, Pink);
        DrawString(font, new Vector2(40, 84), NextLine(), HorizontalAlignment.Left, 1100, 16, Dim);
        var tex = CountryArt.Texture;
        DrawTextureRect(tex, new Rect2(MapAt, tex.GetSize() * Scale), false);

        // Roads: brass where both ends are open, faint dashes where they wait.
        foreach (var (a, b) in Country.Roads)
        {
            LocationDef la = Country.Get(a), lb = Country.Get(b);
            Vector2 pa = Screen(la), pb = Screen(lb);
            bool open = Country.IsOpen(Ranch, la) && Country.IsOpen(Ranch, lb);
            bool lift = la.Side != lb.Side && la.Side != Side.Wilds && lb.Side != Side.Wilds;
            if (open)
            {
                DrawLine(pa, pb, new Color(0, 0, 0, 0.6f), 9);
                DrawLine(pa, pb, lift ? Cyan : Color.FromHtml("#c9a24a"), 4);
            }
            else DrawDashedLine(pa, pb, new Color(0.6f, 0.55f, 0.75f, 0.35f), 3, 10);
        }

        float t = Time.GetTicksMsec() / 1000f;
        foreach (var l in Country.Locations)
        {
            var p = Screen(l);
            bool open = Country.IsOpen(Ranch, l), sel = l.Id == _selected, hover = l.Id == _hover;
            var ring = !open ? Color.FromHtml("#4a4060") : l.Side switch { Side.City => Gold, Side.Undercity => Pink, _ => Green };
            float r = (Phone.On ? 15 : 22) + (sel ? 3 + MathF.Sin(t * 4) * 1.5f : hover ? 2 : 0);
            DrawCircle(p, r + 3, new Color(0, 0, 0, 0.7f));
            DrawCircle(p, r, Color.FromHtml("#120e1c"));
            DrawArc(p, r, 0, MathF.Tau, 40, sel ? Ink : ring, sel ? 4 : 3);
            var glyph = Glyphs.Get(open ? l.Glyph : "lock", Phone.On ? 1 : 2);
            DrawTexture(glyph, p - glyph.GetSize() / 2, open ? Colors.White : new Color(1, 1, 1, 0.5f));
            string name = open ? l.Name : "???";
            int nameSize = Phone.On ? 14 : 17;
            float below = Phone.On ? 32 : 44;
            DrawString(font, p + new Vector2(-91, below + 1), name, HorizontalAlignment.Center, 180, nameSize, new Color(0, 0, 0, 0.9f));
            DrawString(font, p + new Vector2(-92, below), name, HorizontalAlignment.Center, 180, nameSize, open ? Ink : Dim);

            // What's on offer here: a main mission, a duel, how many contracts.
            var here = _board.Where(m => m.Location == l.Id).ToList();
            float off = Phone.On ? 22 : 30, bx = p.X + off * 0.67f;
            if (here.Any(m => m.Story != null)) { Badge(p + new Vector2(-off, -off), "star", Gold); }
            if (here.Any(m => m.Cup != null)) { Badge(new Vector2(bx, p.Y - off), "crown", Gold); bx += 24; }
            else if (here.Any(m => m.Rival != null)) { Badge(new Vector2(bx, p.Y - off), "slash", Pink); bx += 24; }
            if (here.Any(m => m.Dispatch != null)) { Badge(new Vector2(bx, p.Y - off), "hand", Cyan); bx += 24; }
            int contracts = here.Count(m => m.Story == null && m.Rival == null && m.Dispatch == null && m.Roamer == 0);
            if (contracts > 0)
            {
                DrawCircle(new Vector2(bx, p.Y - off + 4), 11, Color.FromHtml("#ff8a3d"));
                DrawString(font, new Vector2(bx - 10, p.Y - off + 10), contracts.ToString(), HorizontalAlignment.Center, 20, 15, Color.FromHtml("#1a0f08"));
            }
        }

        // Roaming packs: each one's leader beside the place it's at, on a red ring.
        foreach (var group in Ranch.Wanderers.GroupBy(w => w.At))
        {
            int k = 0;
            foreach (var w in group)
            {
                var (sprite, anchor) = PackSprite(w);
                var feet = Screen(Country.Get(w.At)) + new Vector2(-46 - k++ * 40, 24);
                float pulse = 0.6f + 0.4f * MathF.Sin(t * 3 + w.Id);
                DrawCircle(feet + new Vector2(0, -16), 24, new Color(0.05f, 0.03f, 0.08f, 0.7f));
                DrawArc(feet + new Vector2(0, -16), 24, 0, MathF.Tau, 36, new Color(1f, 0.35f, 0.3f, pulse), 3);
                DrawTextureRect(sprite, new Rect2(feet - new Vector2(anchor.X, anchor.Y), sprite.GetSize()), false);
            }
        }

        // Your party: a crown over where it stands, bobbing.
        var crown = Glyphs.Get("crown", 3);
        DrawTexture(crown, _token + new Vector2(-crown.GetWidth() / 2f, -64 + MathF.Sin(t * 3) * 3));
    }

    (Texture2D tex, Vector2I anchor) PackSprite(Wanderer w)
    {
        if (_packs.TryGetValue(w.Id, out var hit)) return hit;
        var leader = Roaming.Battle(w).Wild().pack[0];
        var (img, anchor) = CreatureSculptor.Render(leader, false, View.Front);
        return _packs[w.Id] = (ImageTexture.CreateFromImage(img), anchor);
    }

    void Badge(Vector2 at, string glyph, Color color)
    {
        DrawCircle(at, 12, new Color(0, 0, 0, 0.75f));
        DrawArc(at, 12, 0, MathF.Tau, 24, color, 2);
        var g = Glyphs.Get(glyph, 1);
        DrawTexture(g, at - g.GetSize() / 2);
    }

    /// <summary>The next main mission and what it waits for.</summary>
    string NextLine()
    {
        var ready = StoryBook.Ready(Ranch).ToList();
        string away = (Ranch.Away.Count > 0 ? $"   ·   {Ranch.Away.Count} away on dispatches" : "")
            + (Ranch.Wanderers.Count > 0 ? $"   ·   {Ranch.Wanderers.Count} {(Ranch.Wanderers.Count == 1 ? "pack" : "packs")} roaming" : "");
        string what(MainMission m) => m.Ending != null ? $"the ending, {m.Ending}" : $"the road to {Country.Get(m.Opens).Name}";
        if (Ranch.Ending is { } ended) return $"Ending reached: {StoryBook.Get(ended).Ending}. Play on as long as you like.{away}";
        if (ready.Count > 0) return $"Main mission ready at {Country.Get(ready[0].Location).Name}: {what(ready[0])}{(ready.Count > 1 ? $" (and {ready.Count - 1} more)" : "")}.{away}";
        if (StoryBook.Next(Ranch) is not { } next) return "Every road is open. (The story is still to come.)" + away;
        var at = Country.Get(next.Location);
        return $"Next main mission, at {at.Name}, waits for: " + string.Join(", ", StoryBook.Needs(Ranch, next).Where(n => !n.met).Select(n => n.text)) + "." + away;
    }
}
