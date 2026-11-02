using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Ranching;
using CreatureTactics.Tactics;
using CreatureTactics.UI;
using Godot;

namespace CreatureTactics.World;

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

    const int Scale = 4;
    static readonly Vector2 MapAt = new(40, 112);
    static readonly Color Gold = Color.FromHtml("#f2c14e"), Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#9a91b4");
    static readonly Color Pink = Color.FromHtml("#ff4f9a"), Green = Color.FromHtml("#4ff0a0"), Cyan = Color.FromHtml("#4fe3ff");

    string _selected, _hover;
    Vector2 _token;
    bool _travelling;
    List<Mission> _board = new();
    MissionBoardPanel _missions;
    PanelContainer _chooser;
    VBoxContainer _place;
    Label _status;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        TextureFilter = TextureFilterEnum.Nearest;
        _board = Country.Board(Ranch);
        _token = Screen(Country.Get(Ranch.Location));

        var back = new Button { Text = "Ranch  (Esc)", FocusMode = FocusModeEnum.None, Position = new Vector2(1740, 22), CustomMinimumSize = new Vector2(150, 40) };
        back.AddThemeFontSizeOverride("font_size", 16);
        back.Pressed += () => Back?.Invoke();
        AddChild(back);

        _place = new VBoxContainer { Position = new Vector2(1170, 96), Size = new Vector2(720, 160) };
        _place.AddThemeConstantOverride("separation", 6);
        AddChild(_place);
        _missions = new MissionBoardPanel { Ranch = Ranch, Position = new Vector2(1170, 262), Size = new Vector2(720, 770), Depart = Depart };
        AddChild(_missions);
        _status = new Label { Position = new Vector2(40, 1046), Size = new Vector2(1100, 24) };
        _status.AddThemeFontSizeOverride("font_size", 16);
        _status.AddThemeColorOverride("font_color", Gold);
        AddChild(_status);

        // Start on wherever there's something to do: a main mission first, then here, then the nearest contract.
        var first = _board.FirstOrDefault(m => m.Story != null) ?? _board.FirstOrDefault(m => m.Location == Ranch.Location)
            ?? _board.OrderBy(m => Country.Days(Ranch, m.Location)).FirstOrDefault();
        Select(first?.Location ?? Ranch.Location);
        _status.Text = "Pick a place, then a mission; Depart (F) travels there and starts it. Esc: back to the ranch.";
        if (DemoDispatch && _board.FirstOrDefault(m => m.Dispatch != null) is { } job) { Select(job.Location); Choose(job.Dispatch); }
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

        var here = _board.Where(m => m.Location == id).ToList();
        _missions.Visible = l.MapName != null;
        if (l.MapName == null)
            _place.AddChild(Wrap("Home. Back at the ranch (Esc) your creatures rest, breed and train.", 16, Ink));
        else
        {
            _missions.Source = () => here;
            _missions.Heading = here.Count == 0 ? "NOTHING ON OFFER HERE TODAY" : $"ON OFFER AT {l.Name.ToUpperInvariant()}";
            _missions.DepartLabelFor = m => m.Dispatch != null ? "Send a creature  (F)" : id == Ranch.Location ? "Depart  (F)" : $"Travel {days}d & depart  (F)";
            _missions.Selected = 0;
            _missions.Refresh();
            _missions.QueueRedraw();
        }
        QueueRedraw();
    }

    static Label Wrap(string text, int size, Color color)
    {
        var label = Ui.Text(text, size, color, 4);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(700, 0);
        return label;
    }

    async void Depart(Mission m)
    {
        if (_travelling || _chooser != null) return;
        if (m.Dispatch != null) { Choose(m.Dispatch); return; }
        if (Ranch.Squad.Count == 0) { _status.Text = "Pick at least one creature for the squad at the ranch (S)."; return; }
        _travelling = true;
        Audio.Sound.Play("depart");
        // Walk the token along the roads, a stop at every place on the way.
        var route = Country.Route(Ranch, Ranch.Location, m.Location) ?? new List<string> { m.Location };
        foreach (var stop in route.Skip(1))
        {
            var tween = CreateTween();
            tween.TweenMethod(Callable.From<Vector2>(p => { _token = p; QueueRedraw(); }), _token, Screen(Country.Get(stop)), 0.35f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            await ToSignal(tween, Tween.SignalName.Finished);
            Audio.Sound.Play("step", 1.2f);
        }
        Ranch.TravelTo(m.Location);
        Ranch.Save();
        Fight?.Invoke(m);
    }

    /// <summary>Who goes on a dispatch: everyone at home, best suited first, with their odds.</summary>
    void Choose(DispatchDef job)
    {
        _chooser = new PanelContainer { Position = new Vector2(1170, 262), Size = new Vector2(720, 0) };
        var box = new StyleBoxFlat { BgColor = new Color(0.07f, 0.05f, 0.12f, 0.97f), BorderColor = Gold };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(8);
        box.SetContentMarginAll(14);
        _chooser.AddThemeStyleboxOverride("panel", box);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        _chooser.AddChild(col);
        col.AddChild(Wrap($"Who goes on \"{job.Title}\"? Away {job.Days} {(job.Days == 1 ? "day" : "days")}.", 18, Gold));
        col.AddChild(Wrap("Suits: " + string.Join(", ", job.Suits.Select(s => s.hint)) + ". The better it suits, the likelier it goes well.", 15, Dim));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(690, 520), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
                Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(680, 38),
            };
            b.AddThemeFontSizeOverride("font_size", 16);
            int id = rec.Id;
            b.Pressed += () => Send(id, job);
            rows.AddChild(b);
        }
        var cancel = new Button { Text = "Cancel  (Esc)", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(160, 40), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        cancel.Pressed += CloseChooser;
        col.AddChild(cancel);
        _missions.Visible = false;
        AddChild(_chooser);
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

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.Escape && _chooser != null) { CloseChooser(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode is Key.Escape or Key.M) { Back?.Invoke(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.F && _missions.Visible && _missions.Missions.Count > 0) Depart(_missions.Missions[_missions.Selected]);
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        DrawRect(new Rect2(Vector2.Zero, Size), Color.FromHtml("#0b0913"));
        DrawString(font, new Vector2(40, 52), "THE COUNTRY", HorizontalAlignment.Left, 400, 30, Gold);
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
            float r = 22 + (sel ? 3 + MathF.Sin(t * 4) * 1.5f : hover ? 2 : 0);
            DrawCircle(p, r + 3, new Color(0, 0, 0, 0.7f));
            DrawCircle(p, r, Color.FromHtml("#120e1c"));
            DrawArc(p, r, 0, MathF.Tau, 40, sel ? Ink : ring, sel ? 4 : 3);
            var glyph = Glyphs.Get(open ? l.Glyph : "lock", 2);
            DrawTexture(glyph, p - glyph.GetSize() / 2, open ? Colors.White : new Color(1, 1, 1, 0.5f));
            string name = open ? l.Name : "???";
            DrawString(font, p + new Vector2(-91, 45), name, HorizontalAlignment.Center, 180, 17, new Color(0, 0, 0, 0.9f));
            DrawString(font, p + new Vector2(-92, 44), name, HorizontalAlignment.Center, 180, 17, open ? Ink : Dim);

            // What's on offer here: a main mission, a duel, how many contracts.
            var here = _board.Where(m => m.Location == l.Id).ToList();
            float bx = p.X + 20;
            if (here.Any(m => m.Story != null)) { Badge(p + new Vector2(-30, -30), "star", Gold); }
            if (here.Any(m => m.Cup != null)) { Badge(new Vector2(bx, p.Y - 30), "crown", Gold); bx += 24; }
            else if (here.Any(m => m.Rival != null)) { Badge(new Vector2(bx, p.Y - 30), "slash", Pink); bx += 24; }
            if (here.Any(m => m.Dispatch != null)) { Badge(new Vector2(bx, p.Y - 30), "hand", Cyan); bx += 24; }
            int contracts = here.Count(m => m.Story == null && m.Rival == null && m.Dispatch == null);
            if (contracts > 0)
            {
                DrawCircle(new Vector2(bx, p.Y - 26), 11, Color.FromHtml("#ff8a3d"));
                DrawString(font, new Vector2(bx - 10, p.Y - 20), contracts.ToString(), HorizontalAlignment.Center, 20, 15, Color.FromHtml("#1a0f08"));
            }
        }

        // Your party: a crown over where it stands, bobbing.
        var crown = Glyphs.Get("crown", 3);
        DrawTexture(crown, _token + new Vector2(-crown.GetWidth() / 2f, -64 + MathF.Sin(t * 3) * 3));
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
        string away = Ranch.Away.Count > 0 ? $"   ·   {Ranch.Away.Count} away on dispatches" : "";
        if (ready.Count > 0) return $"Main mission ready at {Country.Get(ready[0].Location).Name}: the road to {Country.Get(ready[0].Opens).Name}{(ready.Count > 1 ? $" (and {ready.Count - 1} more)" : "")}.{away}";
        if (StoryBook.Next(Ranch) is not { } next) return "Every road is open. (The story is still to come.)" + away;
        var at = Country.Get(next.Location);
        return $"Next main mission, at {at.Name}, waits for: " + string.Join(", ", StoryBook.Needs(Ranch, next).Where(n => !n.met).Select(n => n.text)) + "." + away;
    }
}
