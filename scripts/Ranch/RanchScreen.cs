using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Classes;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.SpriteLab;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// A station of the home base, walked to and opened: the roster down the left, and on the right only
/// that station's part (the pens' squad and names, the vats, the armory's gear, the training yard's
/// job tree, the war table's tamers, the lab). Esc goes back to the base.
/// </summary>
public partial class RanchScreen : Control
{
    public Ranch Ranch;
    public List<int> JustJoined = new(); // recruits from the last battle, to point out
    public bool VatDemo;                  // screenshots: fill the vats with the first rested pair and the newest child
    public string StartOn;                // --select=NAME: open with that creature selected (screenshots)
    public string Station;                // one station of the home base: only its part shows (pens, vats, lab, yard, armory, war)
    public Action Back;                   // back out to the base
    public Action ToTitle;                // the pause menu's way back to the title
    public bool DemoBuildUp;              // --buildup: open on the station's Build up panel (screenshots)

    static Color Cyan => Ui.Cyan;
    static Color Magenta => Ui.Magenta;
    static Color Gold => Ui.Gold;
    static Color Ink => Ui.Ink;
    static Color Dim => Ui.Dim;

    GridContainer _grid;
    float _right, _top;   // where the right-hand panel starts, and where the screen's body begins
    Button _breedButton;
    Label _status, _notes;
    TopBar _bar;   // the station's name, the counts, and the way back home
    VBoxContainer _detail, _training;
    VatPanel _vats;
    Button _squadButton, _vatButton, _releaseButton, _yardButton;
    HBoxContainer _buttonsRow;
    Button _nameButton, _gearButton;
    LineEdit _nameBox;
    VBoxContainer _column;
    int _naming;
    LabPanel _lab;
    enum Panel { Vats, Lab, Jobs, Party }
    Panel _panel = Panel.Vats;
    JobsPanel _jobs;
    PartyPanel _party;
    readonly List<int> _pen = new();
    int? _selected, _releaseArmed;
    double _releaseTimer;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Audio.Music.Play("ranch");
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

        // Laid out from the screen's size: the roster on the left, the panel (720 wide) on the right.
        // A desktop's 1920x1080 gives six columns of creatures; a phone's narrower screen two.
        var vp = GetViewportRect().Size;
        _right = vp.X - 720 - (Phone.On ? 16 : 30);
        _top = Phone.On ? 112 : 96;
        _bar = new TopBar { Ranch = Ranch, Title = HomeBase.Get(Station)?.Name.ToUpperInvariant() ?? "", Back = () => Back?.Invoke(), Station = Station, Built = said => { Flash(said); Rebuild(); } };
        AddChild(_bar);
        _status = Text(17, Gold, new Vector2(32, TopBar.Bottom), new Vector2(Phone.On ? vp.X - 64 : 1100, 30));
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var scroll = new ScrollContainer { Position = new Vector2(24, _top + 4), Size = new Vector2(_right - 50, vp.Y - _top - 64), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _grid = new GridContainer { Columns = Math.Max(1, (int)((_right - 48) / 184)) };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_grid);

        float vatsH = Phone.On ? 190 : 320, notesH = Phone.On ? 44 : 96;
        _vats = new VatPanel { Position = new Vector2(_right, _top), Size = new Vector2(720, vatsH), Screen = this, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_vats);
        _notes = Text(15, Ink, new Vector2(_right, _top + vatsH + 16), new Vector2(720, notesH));
        _notes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // The selected beast, and only it: what it is, then its training (the pointer only lights the cards).
        var column = _column = new VBoxContainer { Position = new Vector2(_right, _top + vatsH + notesH + 20), Size = new Vector2(720, 430) };
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);
        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 6);
        column.AddChild(_detail);
        _training = new VBoxContainer();
        _training.AddThemeConstantOverride("separation", 6);
        column.AddChild(_training);

        var buttons = _buttonsRow = new HBoxContainer { Position = new Vector2(_right, vp.Y - 110) };
        buttons.AddThemeConstantOverride("separation", 8);
        AddChild(buttons);
        Button Make(string text, Action onPress)
        {
            var b = new Button { Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 44) };
            b.AddThemeFontSizeOverride("font_size", 15);
            b.Pressed += onPress;
            buttons.AddChild(b);
            return b;
        }
        _squadButton = Make("Squad", () => { if (_selected is { } id) ToggleSquad(id); });
        _vatButton = Make("Vat", () => { if (_selected is { } id) TogglePen(id); });
        _releaseButton = Make("Release", () => { if (_selected is { } id) Release(id); });
        _nameButton = Make("Name", () => { if (_selected is { } id) StartNaming(id); });
        _yardButton = Make("Out in the yard", () => { if (_selected is { } id) ToggleOut(id); });
        _gearButton = Make("Gear", () => { if (_selected is { } id) CycleGear(id); });
        // Breed is a button everywhere (Enter does it too on a desktop).
        _breedButton = Make("Breed", Breed);
        _nameBox = new LineEdit { PlaceholderText = Phone.Keys("A name (Enter keeps, Esc cancels)", "A name (Done keeps it)"), MaxLength = 14, Visible = false, Position = new Vector2(_right, vp.Y - 164), CustomMinimumSize = new Vector2(420, 44) };
        _nameBox.AddThemeFontSizeOverride("font_size", 18);
        _nameBox.TextSubmitted += text => FinishNaming(text);
        _nameBox.GuiInput += e => { if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { _nameBox.Visible = false; _nameBox.AcceptEvent(); } };
        AddChild(_nameBox);

        float panelH = vp.Y - _top - 54;
        _lab = new LabPanel { Ranch = Ranch, Position = new Vector2(_right, _top), Size = new Vector2(720, panelH), Visible = false };
        _lab.Done = msg => { Flash(msg); Rebuild(); };
        AddChild(_lab);
        _jobs = new JobsPanel { Ranch = Ranch, Position = new Vector2(_right, _top), Size = new Vector2(Phone.On ? 720 : 730, panelH), Visible = false };
        _jobs.Changed = msg => { Flash(msg); Rebuild(); };
        AddChild(_jobs);
        _party = new PartyPanel { Ranch = Ranch, Position = new Vector2(_right, _top), Size = new Vector2(Phone.On ? 720 : 730, panelH), Visible = false };
        _party.Changed = msg => { Flash(msg); Rebuild(); };
        AddChild(_party);

        if (VatDemo)
        {
            _pen.AddRange(Ranch.Creatures.Where(r => r.Rested).Take(2).Select(r => r.Id));
            var newest = Ranch.Creatures.LastOrDefault(r => r.Generation > 0);
            if (newest != null) ChildShown = Ranch.Get(newest.Id);
        }
        if (JustJoined.Count > 0)
            Flash($"{string.Join(", ", JustJoined.Select(id => Ranch.Get(id).Name))} joined you.");
        if (Ranch.Report.Count > 0) _notes.Text = string.Join("\n", Ranch.Report);
        Select(Ranch.Creatures.FirstOrDefault(r => r.Name == StartOn)?.Id ?? Ranch.Squad.FirstOrDefault(Ranch.Creatures[0].Id));
        Rebuild();
        ApplyStation();
        if (DemoBuildUp) _bar.OpenLevels();
    }

    /// <summary>The attract loop's next battle: the story when it can, else the nearest contract.</summary>
    public static void AutoBattle(Node host, Ranch ranch, Action<Mission> fight)
    {
        if (!host.IsInsideTree() || ranch.Squad.Count == 0) return;
        var all = World.Country.Board(ranch).Where(x => x.Dispatch == null).ToList();
        var m = all.FirstOrDefault(x => x.Story != null) ?? all.OrderBy(x => World.Country.Days(ranch, x.Location)).First();
        var met = ranch.TravelTo(m.Location);
        ranch.Save();
        if (met != null) fight?.Invoke(World.Roaming.Battle(met));
        else if (m.Roamer == 0) fight?.Invoke(m);
        else if (all.FirstOrDefault(x => x.Roamer == 0) is { } other) fight?.Invoke(other); // the pack moved on
    }

    /// <summary>
    /// At one station of the home base, only that station's part shows (the other systems are walked
    /// to): the pens are the roster and squad, the vats breed, the armory fits gear, the yard trains,
    /// the war table leads, the lab grafts. The top bar names it, once (no intro, no key line: the Controls page in the
    /// menu has the keys), and its Back button goes home (B on a phone).
    /// </summary>
    void ApplyStation()
    {
        SetPanel(Station switch { "lab" => Panel.Lab, "yard" => Panel.Jobs, "war" => Panel.Party, _ => Panel.Vats });
        var keep = Station switch
        {
            "pens" => new[] { _squadButton, _yardButton, _nameButton, _releaseButton },
            "vats" => new[] { _vatButton, _breedButton },
            "armory" => new[] { _gearButton },
            _ => Array.Empty<Button>(),
        };
        foreach (var b in new[] { _squadButton, _vatButton, _releaseButton, _nameButton, _gearButton, _breedButton, _yardButton }) b.Visible = keep.Contains(b);
        if (Station != "vats")
        {
            // No vats here: the creature's detail moves up into their space, and the base keeps the news.
            _vats.Visible = false;
            _notes.Visible = false;
            _column.Position = new Vector2(_right, _top + 14);
        }
        // A phone's vats leave no room under them for the training too: the creature's detail is enough there.
        else if (Phone.On) _training.Visible = false;
    }

    Label Text(int size, Color color, Vector2 pos, Vector2 boxSize)
    {
        var l = new Label { Position = pos, Size = boxSize, MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", Ui.Fit(size));
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        l.AddThemeConstantOverride("shadow_offset_x", 2);
        l.AddThemeConstantOverride("shadow_offset_y", 2);
        if (UiSkin.PixelText) Ui.Dress(l, size);
        AddChild(l);
        return l;
    }

    void Flash(string text) => _status.Text = text;

    /// <summary>The choice after a main mission, over everything until it's made: who hears first and gets the credit.</summary>
    public static void AskChoice(Control host, Ranch ranch, bool auto, Action<string> flash, Action after)
    {
        if (ranch.Pending == null || World.StoryBook.Get(ranch.Pending) is not { } main) return;
        if (auto) { flash(ranch.Decide("both")); return; } // the attract loop shares the credit and plays on
        var shade = new ColorRect { Color = new Color(0.02f, 0.01f, 0.05f, 0.75f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var layer = new CanvasLayer { Layer = 5 };
        host.AddChild(layer);
        layer.AddChild(shade);
        var vp = host.GetViewportRect().Size;
        var panel = new PanelContainer { Position = new Vector2((vp.X - 800) / 2, vp.Y * 0.3f), CustomMinimumSize = new Vector2(800, 0) };
        panel.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        shade.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);
        col.AddChild(Ui.Text(main.Title.ToUpperInvariant(), 18, Gold));
        var prompt = Ui.Text(World.StoryBook.Prompt(main), 18, Ink, 4);
        prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        prompt.CustomMinimumSize = new Vector2(760, 0);
        col.AddChild(prompt);
        foreach (var choice in World.StoryBook.ChoicesAfter(main))
        {
            var b = new Button { Text = $"{choice.Text}   ({(choice.City >= 0 ? "+" : "")}{choice.City} city, {(choice.Under >= 0 ? "+" : "")}{choice.Under} undercity)", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(760, 44), Alignment = HorizontalAlignment.Left };
            b.AddThemeFontSizeOverride("font_size", 16);
            string id = choice.Id;
            b.Pressed += () =>
            {
                flash(ranch.Decide(id));
                ranch.Save();
                layer.QueueFree();
                after?.Invoke();
            };
            col.AddChild(b);
        }
    }

    void StartNaming(int id)
    {
        _naming = id;
        _nameBox.Text = Ranch.Record(id).Nickname ?? "";
        _nameBox.Visible = true;
        _nameBox.GrabFocus();
        _nameBox.SelectAll();
    }

    void FinishNaming(string text)
    {
        _nameBox.Visible = false;
        Flash(Ranch.Rename(_naming, text));
        Ranch.Save();
        Rebuild();
    }

    void Rebuild()
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        foreach (var rec in Ranch.Creatures)
        {
            var card = new RanchCard { Screen = this, Id = rec.Id };
            int id = rec.Id;
            card.Clicked += right => { if (right && Station == "vats") TogglePen(id); else Select(id); };
            _grid.AddChild(card);
            if (Phone.On && id == _selected) PadNav.First(card);
        }
        _vats.QueueRedraw();
        RefreshSelected();
    }

    public bool InSquad(int id) => Ranch.Squad.Contains(id);
    public int PenSlot(int id) => _pen.IndexOf(id);
    public bool IsNew(int id) => JustJoined.Contains(id);
    public bool IsSelected(int id) => _selected == id;
    public Creature ChildShown;
    public IReadOnlyList<int> Pen => _pen;

    void Select(int id)
    {
        _selected = id;
        if (_panel == Panel.Lab) _lab.ShowCreature(id);
        if (_panel == Panel.Jobs) { _jobs.Reset(); _jobs.ShowCreature(id); }
        if (_panel == Panel.Party) _party.Refresh();
        foreach (var card in _grid.GetChildren().OfType<RanchCard>()) card.QueueRedraw();
        RefreshSelected();
    }

    /// <summary>
    /// The column: the selected beast (the only one it shows), then its training: class, stars and JP, skill icons and
    /// slots. What only one station needs stays there: the parts at the lab, open and locked classes at the yard.
    /// </summary>
    void RefreshSelected()
    {
        bool any = _selected != null && Ranch.Creatures.Any(r => r.Id == _selected);
        foreach (var b in new[] { _squadButton, _vatButton, _releaseButton, _nameButton, _gearButton, _yardButton }) b.Disabled = !any;
        _breedButton.Disabled = _pen.Count < 2;
        Clear(_detail);
        Clear(_training);
        if (!any) return;
        int id = _selected.Value;
        ShowDetail(id);
        var c = Ranch.Get(id);
        var t = Ranch.Record(id).Training;
        _squadButton.Text = InSquad(id) ? "Leave squad" : "Join squad";
        _yardButton.Text = Ranch.Out.Contains(id) ? "Back to the pen" : "Out in the yard";
        _yardButton.Disabled = InSquad(id);
        _yardButton.TooltipText = InSquad(id) ? "The squad waits in the pen between battles" : $"Up to {Farmyard.OutCap(Ranch)} beasts roam the yard (more as the pens are built up); the rest stay in the pen";
        _vatButton.Text = PenSlot(id) >= 0 ? "Out of vat" : "Into vat";
        _gearButton.Text = $"Gear: {ClassBook.Passive(t.Gear)?.Name ?? "none"}  >";
        var bag = Ranch.Items.Where(kv => GearBook.IsGear(kv.Key)).Select(kv => $"{kv.Value} {ClassBook.Passive(kv.Key).Name}").ToList();
        _gearButton.TooltipText = "Cycle the gear it wears (bought at shops on the map)\nIn the bag: " + (bag.Count > 0 ? string.Join(", ", bag) : "no gear");

        var cls = ClassBook.Class(t.Class);
        var head = Row(Ui.Picture(SkillIcons.Class(cls.Id, ClassBook.Standing.Open, 2), cls.Blurb), Ui.Text(cls.Name, 18, Gold),
            Ui.Picture(SkillIcons.Stars(t.Stars(cls.Id), 2), "mastery stars"));
        // Its skills in order, dimmed until learned, then how much JP the next one needs.
        int spent = 0;
        int? next = null;
        foreach (var sid in cls.Skills)
        {
            var s = ClassBook.Skill(sid);
            spent += s.Cost;
            bool known = t.Skills.Contains(sid);
            var icon = Ui.Picture(SkillIcons.Skill(s, 2), $"{s.Name}\n{s.Text}" + (known ? "" : $"\n{spent - t.JpIn(cls.Id)} JP to go"));
            if (!known) { icon.Modulate = new Color(1, 1, 1, 0.4f); next ??= spent - t.JpIn(cls.Id); }
            head.AddChild(icon);
        }
        head.AddChild(Ui.Chips(new[] { new Chip("star", $"{t.JpIn(cls.Id)} JP", Ink, "JP earned in this class") }
            .Concat(next != null ? new[] { new Chip("lock", $"{next}", Dim, "JP to the next skill") } : Array.Empty<Chip>()), 15));
        _training.AddChild(head);

        // Everything else it brings: secondary skills, an inherited skill, the two passives. It wraps, so a long
        // list (or the wider pixel type) takes a second line rather than running off the screen.
        var extras = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        extras.AddThemeConstantOverride("h_separation", 8);
        extras.AddThemeConstantOverride("v_separation", 4);
        if (t.Secondary != null)
        {
            extras.AddChild(Ui.ChipView(new Chip(SkillIcons.ClassEmblem(t.Secondary), t.Secondary, Ink, "secondary skill set"), 15));
            foreach (var s in t.Skills.Select(ClassBook.Skill).Where(s => s.Class == t.Secondary))
                extras.AddChild(Ui.Picture(SkillIcons.Skill(s, 2), $"{s.Name} ({s.Class})\n{s.Text}"));
        }
        if (t.Innate != null)
        {
            var s = ClassBook.Skill(t.Innate);
            extras.AddChild(Ui.Picture(SkillIcons.Skill(s, 2), $"Innate, from a parent: {s.Name}\n{s.Text}"));
        }
        var shine = ClassBook.ShinyPassive(c);
        var gear = ClassBook.Passive(t.Gear);
        var tierNow = TierBook.Of(c);
        var natural = TierBook.PassiveOf(c, tierNow);
        foreach (var p in new[] { ClassBook.Passive(t.Passive), shine, gear, natural, ClassBook.Passive(t.Reaction) }.Where(p => p != null))
        {
            // Each passive's icon and name together, so the wrap never parts them.
            extras.AddChild(Row(Ui.Picture(SkillIcons.Passive(p, 2), $"{p.Name} ({(p == shine ? "shiny, its own" : p == gear ? "gear" : p == natural ? (tierNow == Tier.Thoroughbred ? "thoroughbred, its species' own" : "chimera, other species' abilities") : p.Kind == PassiveKind.Reaction ? "reaction" : "support")})\n{p.Text}"),
                Ui.Text(p.Name, 14, p == shine || p == gear ? Gold : p == natural ? Ui.Tone(TierBook.Hex(tierNow == Tier.Thoroughbred ? Tier.Thoroughbred : Tier.Chimera)) : p.Kind == PassiveKind.Reaction ? Ui.Pink : Ui.Good, 4)));
        }
        if (extras.GetChildCount() > 0) _training.AddChild(extras);
    }
    /// <summary>Swaps the right-hand side between breeding and training, and the lab.</summary>
    void SetPanel(Panel p)
    {
        _panel = p;
        foreach (var c in new Control[] { _vats, _notes, _training, _buttonsRow, _detail }) c.Visible = p == Panel.Vats;
        if (Phone.On && Station == "vats") _training.Visible = false;
        _lab.Visible = p == Panel.Lab;
        _jobs.Visible = p == Panel.Jobs;
        _party.Visible = p == Panel.Party;
        if (p == Panel.Party) _party.Refresh();
        if (p == Panel.Jobs && _selected is { } jid) { _jobs.Reset(); _jobs.ShowCreature(jid); }
        if (p == Panel.Lab && _selected is { } id) _lab.ShowCreature(id);
    }

    /// <summary>The next piece of gear from the bag (or none), in name order.</summary>
    void CycleGear(int id)
    {
        var t = Ranch.Record(id).Training;
        var options = new List<string> { null };
        options.AddRange(Ranch.Items.Where(kv => GearBook.IsGear(kv.Key) && kv.Value > 0).Select(kv => kv.Key).Append(t.Gear)
            .Where(g => g != null).Distinct().OrderBy(g => ClassBook.Passive(g).Name));
        if (options.Count == 1) { Flash("No gear in the bag: the shops sell it (out through the gate)."); return; }
        string next = options[(options.IndexOf(t.Gear) + 1) % options.Count];
        Flash(Ranch.Equip(id, next));
        Ranch.Save();
        Rebuild();
    }

    void ToggleSquad(int id)
    {
        if (InSquad(id)) Ranch.RemoveFromSquad(id);
        else if (Ranch.Away.TryGetValue(id, out var away)) { Flash($"{Ranch.Get(id).Name} is away on a dispatch until day {away.BackOn}."); return; }
        else if (Ranch.Record(id).Steeping) { Flash($"{Ranch.Get(id).Name} is steeping: a new infusion is taking. It can fight again after the next battle."); return; }
        else if (Ranch.Record(id).Recovering) { Flash($"{Ranch.Get(id).Name} is recovering from an episode. It can fight again after the next battle."); return; }
        else if (Ranch.AddToSquad(id)) Flash($"{Ranch.Get(id).Name} joins {Ranch.LeaderOf(id).Name}'s squad.");
        else { Flash("Every tamer leads three already: take someone out first, or field another tamer at the war table."); return; }
        Ranch.Save();
        Rebuild();
        if (_panel == Panel.Party) _party.Refresh();
    }

    void ToggleOut(int id)
    {
        Flash(Farmyard.ToggleOut(Ranch, id));
        Ranch.Save();
        Rebuild();
    }

    /// <summary>The short name on a squad creature's card: who leads it.</summary>
    public string LeaderTag(int id) => Ranch.LeaderOf(id) is { } w ? (w.Id == Party.TamerBook.You ? "YOU" : w.Name.Replace("The ", "").ToUpperInvariant()) : "SQUAD";

    void TogglePen(int id)
    {
        if (_pen.Contains(id)) _pen.Remove(id);
        else if (Ranch.Away.ContainsKey(id)) { Flash($"{Ranch.Get(id).Name} is away on a dispatch."); return; }
        else if (!Ranch.Record(id).Rested) { Flash($"{Ranch.Get(id).Name} is resting. Fight a battle and everyone can breed again."); return; }
        else if (_pen.Count >= 2) { Flash("Both vats are full: take one out first."); return; }
        else _pen.Add(id);
        ChildShown = null;
        _notes.Text = _pen.Count == 2 ? Phone.Keys("Ready: Breed (or Enter) makes one child.", "Ready: Breed makes one child.") : "";
        Rebuild();
    }

    void Breed()
    {
        if (_pen.Count < 2) { Flash(Phone.Keys("Put two rested beasts in the vats first (Into vat, or right-click).", "Put two rested beasts in the vats first (Into vat).")); return; }
        var a = Ranch.Get(_pen[0]);
        var b = Ranch.Get(_pen[1]);
        var (id, result) = Ranch.Breed(_pen[0], _pen[1]);
        Audio.Sound.Play("hatch");
        _pen.Clear();
        ChildShown = Ranch.Get(id);
        Ranch.Save();
        var lines = new List<string> { $"{ChildShown.Name} is born (generation {ChildShown.Generation})." };
        if (result.FromA.Count > 0) lines.Add($"From {a.Name}: {string.Join(", ", result.FromA)}");
        if (result.FromB.Count > 0) lines.Add($"From {b.Name}: {string.Join(", ", result.FromB)}");
        if (result.Notes.Count > 0) lines.Add(string.Join("; ", result.Notes));
        _notes.Text = string.Join("\n", lines);
        Flash($"{a.Name} and {b.Name} rest until the next battle.");
        Select(id);
        Rebuild();
    }

    void Release(int id)
    {
        if (Ranch.Creatures.Count <= 1) { Flash("You can't release your last beast."); return; }
        if (_releaseArmed != id)
        {
            _releaseArmed = id;
            _releaseTimer = 3;
            Flash($"Release {Ranch.Get(id).Name} to live as a stray? Press Release (or Delete) again.");
            return;
        }
        Flash($"{Ranch.Get(id).Name} goes back to being a stray.");
        _releaseArmed = null;
        _pen.Remove(id);
        Ranch.Release(id);
        Ranch.Save();
        _selected = Ranch.Creatures[0].Id;
        Rebuild();
    }

    static void Clear(Control box)
    {
        foreach (var child in box.GetChildren()) { box.RemoveChild(child); child.QueueFree(); }
    }

    static HBoxContainer Row(params Control[] items)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        foreach (var i in items) row.AddChild(i);
        return row;
    }

    /// <summary>The selected beast: what it is, its stats in glyphs, its gear, its lineage.</summary>
    void ShowDetail(int id)
    {
        var c = Ranch.Get(id);
        var rec = Ranch.Record(id);
        var u = Unit.From(c, Tactics.Side.Player, rec.Training);
        var s = c.Species;
        string what = (c.Nickname != null ? $"{s.Name} · " : "") + $"{SizeInfo.Word(c.Size)} {s.Plan.ToString().ToLowerInvariant()}"
            + (s.Strain != Strain.Natural ? $" · {s.Strain.Label().ToLowerInvariant()}" : "")
            + (s.Origin != null ? $" · from {s.Origin}" : "");
        // Its size's tooltip: what the size does in battle, and the size it was born if a draught changed it.
        var whatLabel = Ui.Text(what, 15, Dim, 4);
        whatLabel.MouseFilter = MouseFilterEnum.Stop;
        whatLabel.TooltipText = SizeInfo.Describe(c.Size) + (c.BirthSize is { } bornSize && bornSize != c.Size ? $"\nBorn {SizeInfo.Word(bornSize)}: its line breeds from that." : "");
        var head = Row(Ui.Text(c.Name, 22, Ink), whatLabel);
        var tier = TierBook.Of(c);
        head.AddChild(Ui.ChipView(new Chip(TierBook.Glyph(tier), tier.ToString(), Ui.Tone(TierBook.Hex(tier)), TierBook.Describe(c, tier)), 15));
        if (c.Shiny) head.AddChild(Ui.ChipView(new Chip("sparkle", "shiny", Gold), 15));
        if (c.Mutation != null) head.AddChild(Ui.ChipView(new Chip("swirl", c.Mutation.ToString().ToLowerInvariant(), Ui.Pink, "mutation"), 15));
        _detail.AddChild(head);
        // What it was born with (natural, passed down), and what's bolted on (neon and brass, not).
        var born = c.Traits.Select(ClassBook.Passive).Where(p => p != null).Select(p => new Chip("paw", p.Name, Ui.Tone("#9ac25a"), $"Trait: {p.Text} Passed down when bred."));
        var fitted = c.Augments.Select(AugmentBook.Get).Where(a => a != null).Select(a => new Chip(a.Chem ? "flask" : "gear", a.Name, Color.FromHtml(a.Chem ? "#4ff0a0" : "#4fe3ff"), AugmentBook.Text(a)));
        var tuned = c.Infusions.Select(InfusionBook.Get).Where(p => p != null).Select(p => new Chip("flask", p.Name, Ui.Tone(TierBook.Hex(Tier.Infused)), $"Infusion: {p.Text} Not passed down."));
        var spliced = (c.Species.Spliced ?? Array.Empty<Form>()).Select(f => new Chip("helix", TierBook.Ability(f), Ui.Tone(TierBook.Hex(Tier.Chimera)),
            $"Spliced in from a {f.ToString().ToLowerInvariant()}: {TierBook.AbilityText(f)}. Passed down."));
        var marks = born.Concat(spliced).Concat(tuned).Concat(fitted).ToList();
        if (marks.Count > 0) _detail.AddChild(Ui.Chips(marks, 15));

        _detail.AddChild(Ui.Chips(new[]
        {
            new Chip("hp", u.MaxHp.ToString(), Ink, "max HP"), new Chip("atk", u.Atk.ToString(), Ink, "ATK"), new Chip("def", u.Def.ToString(), Ink, "DEF"),
            new Chip("spd", u.Spd.ToString(), Ink, "SPD"), new Chip("move", u.Move.ToString(), Ink, "MOVE"),
            new Chip(SkillIcons.ClassEmblem(u.ClassName), "as " + u.ClassName, Gold, "stats as its current class"),
        }, 18));

        var (weapon, armor, move) = c.Loadout();
        _detail.AddChild(Ui.Chips(new[]
        {
            new Chip(SkillIcons.WeaponGlyph(c.Weapon), c.Weapon.ToString(), Ink, "weapon: " + weapon),
            new Chip(SkillIcons.ArmorGlyph(c.Armor), c.Armor.ToString(), Ink, "armour: " + armor),
            new Chip(SkillIcons.MoveGlyph(c.MoveType), c.MoveType.ToString(), Ink, "moves: " + move),
            new Chip(SkillIcons.AttitudeGlyph(c.Attitude), c.Attitude.ToString(), Ink,
                $"reacts: {AttitudeInfo.Reaction(c.Attitude)}\nrecruit: {AttitudeInfo.Recruit(c.Attitude)}"),
        }, 16));

        var tags = new List<Chip>();
        if (c.Generation > 0) tags.Add(new Chip("paw", $"gen {c.Generation}: {c.ParentA} + {c.ParentB}", Dim, "bred in the vats"));
        if (!rec.Rested) tags.Add(new Chip("clock", "resting", Gold, "can breed again after the next battle"));
        if (rec.Steeping) tags.Add(new Chip("flask", "steeping", Ui.Tone(TierBook.Hex(Tier.Infused)), "a new infusion is taking: it sits out the next battle"));
        if (rec.Recovering) tags.Add(new Chip("skull", "recovering", Ui.Tone(TierBook.Hex(Tier.Abomination)), "an abomination's episode: it sits out the next battle"));
        if (TierBook.GrowthRate(c) < 1) tags.Add(new Chip("down", $"learns at {TierBook.GrowthRate(c) * 100:0}%", Dim, "its rigs slow how fast it learns (JP)"));
        if (tags.Count > 0) _detail.AddChild(Ui.Chips(tags, 14));
    }
    public override void _Process(double delta)
    {
        if (_releaseArmed == null) return;
        _releaseTimer -= delta;
        if (_releaseTimer <= 0) { _releaseArmed = null; Flash(""); }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || _bar.Building) return;
        // Only the station's own keys work here; the rest of the base is walked to. Esc is the menu on a desktop
        // (the Back button goes back); on a phone B goes back and Start is the menu.
        if (k.Keycode == Key.Escape && _nameBox.Visible) { _nameBox.Visible = false; }
        else if (PauseMenu.Opens(k)) AddChild(new PauseMenu { Save = Ranch.Save, ToTitle = ToTitle, Screen = "station" });
        else if (PauseMenu.GoesBack(k)) Back?.Invoke();
        else if (k.Keycode is Key.Enter or Key.KpEnter && Station == "vats") Breed();
        else if (k.Keycode == Key.S && Station == "pens" && _selected is { } ps) ToggleSquad(ps);
        else if (k.Keycode == Key.Y && Station == "pens" && _selected is { } py) ToggleOut(py);
        else if (k.Keycode == Key.Delete && Station == "pens" && _selected is { } pd) Release(pd);
        else return;
        GetViewport().SetInputAsHandled();
    }
}

/// <summary>A creature on the roster, with its squad, vat and resting marks.</summary>
public partial class RanchCard : Control
{
    public RanchScreen Screen;
    public int Id;
    public event Action<bool> Clicked;

    Texture2D _tex;
    bool _hover;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(176, 200);
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        _tex = ImageTexture.CreateFromImage(CreaturePainter.Paint(Screen.Ranch.Get(Id), false));
        // The pointer only lights the card; a click (or A) selects it, and the column shows the selected one.
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
        if (Phone.On)
        {
            FocusMode = FocusModeEnum.All;
            FocusEntered += QueueRedraw;
            FocusExited += QueueRedraw;
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
            Clicked?.Invoke(mb.ButtonIndex == MouseButton.Right);
        else if (e.IsActionPressed("ui_accept")) { Clicked?.Invoke(false); AcceptEvent(); }
    }

    public override void _Draw()
    {
        var c = Screen.Ranch.Get(Id);
        var rec = Screen.Ranch.Record(Id);
        bool squad = Screen.InSquad(Id);
        int pen = Screen.PenSlot(Id);
        bool yard = !squad && Screen.Ranch.Out.Contains(Id) && !Screen.Ranch.Away.ContainsKey(Id);
        // The card's tube says where it stands: gold when chosen, pink in a vat, cyan in the squad, green out in the yard.
        var box = Screen.IsSelected(Id) ? UiSkin.Lit(UiPart.Button, Color.FromHtml("#f2c14e"), true)
            : pen >= 0 ? UiSkin.Lit(UiPart.Button, Color.FromHtml("#ff4f9a"), _hover)
            : squad ? UiSkin.Lit(UiPart.Button, Color.FromHtml("#4fe3ff"), _hover)
            : yard ? UiSkin.Lit(UiPart.Button, Color.FromHtml("#5fe08a"), _hover)
            : UiSkin.Box(UiPart.Button, _hover ? UiState.Hover : UiState.Normal);
        DrawStyleBox(box, new Rect2(Vector2.Zero, Size));
        if (HasFocus()) DrawStyleBox(UiSkin.Box(UiPart.Button, UiState.Focus), new Rect2(Vector2.Zero, Size)); // where the pad is

        float z = CreatureSculptor.FitZoom(_tex.GetSize(), new Vector2(Size.X - 8, 128), 2);
        var ts = _tex.GetSize() * z;
        float ground = 136;
        DrawSetTransform(new Vector2(Size.X / 2, ground - 2), 0, new Vector2(1, 0.22f));
        DrawCircle(Vector2.Zero, ts.X * 0.42f, new Color(0, 0, 0, 0.4f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var modulate = rec.Rested ? Colors.White : new Color(0.65f, 0.62f, 0.75f);
        DrawTextureRect(_tex, new Rect2(new Vector2(Mathf.Round(Size.X / 2 - ts.X / 2), ground - ts.Y), ts), false, modulate);

        var font = GetThemeDefaultFont();
        Ui.Draw(this, new Vector2(0, ground + 24), c.Name, HorizontalAlignment.Center, Size.X, 17, Ui.Ink);
        // Its size and body; the size cut short ("Med.") when the two don't fit the card.
        string what = $"{c.Size} {c.Species.Plan}";
        if (font.GetStringSize(what, HorizontalAlignment.Left, -1, Ui.Fit(13)).X > Size.X - 2 * UiSkin.Px * 4) what = $"{c.Size.ToString()[..3]}. {c.Species.Plan}";
        Ui.Draw(this, new Vector2(0, ground + 44), what, HorizontalAlignment.Center, Size.X, Ui.Fit(13), Ui.Dim);

        // Marks in the top corners.
        if (squad) Ui.Draw(this, new Vector2(18, 30), Screen.LeaderTag(Id), HorizontalAlignment.Left, -1, Ui.Fit(13), Ui.Cyan);
        if (yard) Ui.Draw(this, new Vector2(18, 30), "IN THE YARD", HorizontalAlignment.Left, -1, Ui.Fit(13), Ui.Good);
        if (Screen.Ranch.Away.TryGetValue(Id, out var away))
            Ui.Draw(this, new Vector2(18, 30), $"AWAY {Math.Max(0, away.BackOn - Screen.Ranch.Day)}d", HorizontalAlignment.Left, -1, Ui.Fit(13), Ui.Magenta);
        if (pen >= 0) Ui.Draw(this, new Vector2(18, squad || yard ? 48 : 30), pen == 0 ? "VAT A" : "VAT B", HorizontalAlignment.Left, -1, Ui.Fit(13), Ui.Magenta);
        string tag = Screen.IsNew(Id) ? "NEW" : c.Generation > 0 ? $"GEN {c.Generation}" : "";
        if (tag != "") Ui.Draw(this, new Vector2(0, 30), tag, HorizontalAlignment.Right, Size.X - 18, Ui.Fit(13), Ui.Gold);
        if (rec.Steeping) Ui.Draw(this, new Vector2(0, 48), "steeping", HorizontalAlignment.Right, Size.X - 18, Ui.Fit(12), Ui.Good);
        else if (rec.Recovering) Ui.Draw(this, new Vector2(0, 38), "recovering", HorizontalAlignment.Right, Size.X - 8, Ui.Fit(12), Ui.Bad);
        else if (!rec.Rested) Ui.Draw(this, new Vector2(0, 38), "resting", HorizontalAlignment.Right, Size.X - 8, Ui.Fit(12), Ui.Dim);
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
        float h = Size.Y - 50;
        Vat(new Rect2(0, 30, 220, h), "VAT A", A, Ui.Magenta);
        Vat(new Rect2(250, 30, 220, h), "VAT B", B, Ui.Magenta);
        Vat(new Rect2(500, 30, 220, h), "OFFSPRING", Screen.ChildShown, Ui.Cyan);
        Ui.Draw(this, new Vector2(226, 30 + h / 2), "+", HorizontalAlignment.Left, -1, 28, Ui.Ink);
        Ui.Draw(this, new Vector2(474, 30 + h / 2), "=", HorizontalAlignment.Left, -1, 28, Ui.Ink);
    }

    void Vat(Rect2 r, string label, Creature c, Color glow)
    {
        var font = GetThemeDefaultFont();
        var brass = Color.FromHtml("#b2873a");
        Ui.Draw(this, r.Position + new Vector2(0, -8), label, HorizontalAlignment.Center, r.Size.X, 15, glow);
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
        Ui.Draw(this, new Vector2(r.Position.X, r.End.Y + 20), c.Name, HorizontalAlignment.Center, r.Size.X, 17, Ui.Ink);
    }

    public override void _Process(double delta) => QueueRedraw();
}
