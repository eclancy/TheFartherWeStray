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
/// The ranch: your roster, the squad, training, and the breeding vats. Click a creature to select
/// it: its panel sets squad, class and secondary skills. Right-click puts it in a breeding vat;
/// Enter breeds; F goes to battle.
/// </summary>
public partial class RanchScreen : Control
{
    public Ranch Ranch;
    public Action<Mission> Fight;
    public Action OpenMap;                // the country map, where missions are picked
    public List<int> JustJoined = new(); // recruits from the last battle, to point out
    public bool VatDemo;                  // screenshots: fill the vats with the first rested pair and the newest child
    public bool AutoFight;                // --autoplay: head into the next battle on its own (an attract loop)
    public bool StartInLab;               // --labmode: open on the lab (screenshots)
    public bool StartOnJobs;              // --jobsmode: open on the job tree (screenshots)
    public bool StartOnParty;             // --partymode: open on the party (screenshots)
    public string StartOn;                // --select=NAME: open with that creature selected (screenshots)
    public string Station;                // one station of the home base: only its part shows (pens, vats, lab, yard, armory, war)
    public Action Back;                   // back out to the base
    public Action ToTitle;                // the pause menu's way back to the title

    static readonly Color Cyan = Color.FromHtml("#4fe3ff"), Magenta = Color.FromHtml("#ff4f9a"), Gold = Color.FromHtml("#f2c14e");
    static readonly Color Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#9a91b4");

    GridContainer _grid;
    float _right, _top;   // where the right-hand panel starts, and where the screen's body begins
    Button _breedButton;
    Label _header, _status, _notes;
    VBoxContainer _detail, _training;
    Control _headerChips;
    VatPanel _vats;
    Button _squadButton, _classButton, _secondButton, _vatButton, _releaseButton, _labButton, _boardButton, _jobsButton;
    HBoxContainer _buttonsRow;
    Button _nameButton, _gearButton;
    LineEdit _nameBox;
    Label _help;
    VBoxContainer _column;
    int _naming;
    LabPanel _lab;
    enum Panel { Vats, Lab, Jobs, Party }
    Panel _panel = Panel.Vats;
    JobsPanel _jobs;
    PartyPanel _party;
    Button _partyButton;
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
        _header = Text(26, Ink, new Vector2(32, 20), new Vector2(vp.X - 60, 40));
        _status = Text(17, Gold, new Vector2(32, 62), new Vector2(Phone.On ? vp.X - 64 : 1100, 30));
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        var scroll = new ScrollContainer { Position = new Vector2(24, _top + 4), Size = new Vector2(_right - 50, vp.Y - _top - 64), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _grid = new GridContainer { Columns = Math.Max(1, (int)((_right - 48) / 184)) };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_grid);
        _help = Text(16, Dim, new Vector2(32, vp.Y - 44), new Vector2(_right - 50, 30));
        _help.Text =
            "Click: select    Right-click: vat    Enter: breed    J: jobs    L: lab    M: map    S: squad    Delete twice: release    N: sound    Tab: Sprite Lab";

        float vatsH = Phone.On ? 190 : 320, notesH = Phone.On ? 44 : 96;
        _vats = new VatPanel { Position = new Vector2(_right, _top), Size = new Vector2(720, vatsH), Screen = this, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_vats);
        _notes = Text(15, Ink, new Vector2(_right, _top + vatsH + 16), new Vector2(720, notesH));
        _notes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // Hover detail and the selected creature's training, stacked in one column.
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
        _classButton = Make("Class", () => { if (_selected is { } id) CycleClass(id); });
        _secondButton = Make("Secondary", () => { if (_selected is { } id) CycleSecondary(id); });
        _vatButton = Make("Vat", () => { if (_selected is { } id) TogglePen(id); });
        _releaseButton = Make("Release", () => { if (_selected is { } id) Release(id); });
        _nameButton = Make("Name", () => { if (_selected is { } id) StartNaming(id); });
        _gearButton = Make("Gear", () => { if (_selected is { } id) CycleGear(id); });
        // Enter breeds on a desktop; a phone's pad needs a button for it.
        _breedButton = Make("Breed", Breed);
        _breedButton.Visible = false;
        _nameBox = new LineEdit { PlaceholderText = Phone.Keys("A name (Enter keeps, Esc cancels)", "A name (Done keeps it)"), MaxLength = 14, Visible = false, Position = new Vector2(_right, vp.Y - 164), CustomMinimumSize = new Vector2(420, 44) };
        _nameBox.AddThemeFontSizeOverride("font_size", 18);
        _nameBox.TextSubmitted += text => FinishNaming(text);
        _nameBox.GuiInput += e => { if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { _nameBox.Visible = false; _nameBox.AcceptEvent(); } };
        AddChild(_nameBox);

        float panelH = vp.Y - _top - 54;
        _lab = new LabPanel { Ranch = Ranch, Position = new Vector2(_right, _top), Size = new Vector2(720, panelH), Visible = false };
        _lab.Done = msg => { Flash(msg); Rebuild(); if (_selected is { } sid) ShowDetail(sid); };
        AddChild(_lab);
        _jobs = new JobsPanel { Ranch = Ranch, Position = new Vector2(_right, _top), Size = new Vector2(Phone.On ? 720 : 730, panelH), Visible = false };
        _jobs.Changed = msg => { Flash(msg); Rebuild(); if (_selected is { } sid) ShowDetail(sid); };
        AddChild(_jobs);
        _party = new PartyPanel { Ranch = Ranch, Selected = () => _selected, Position = new Vector2(_right, _top), Size = new Vector2(Phone.On ? 720 : 730, panelH), Visible = false };
        _party.Changed = msg => { Flash(msg); Rebuild(); };
        AddChild(_party);
        Button Header(string text, float x, Action onPress)
        {
            var b = new Button { Text = text, FocusMode = FocusModeEnum.None, Position = new Vector2(x, 22), CustomMinimumSize = new Vector2(150, 40) };
            b.AddThemeFontSizeOverride("font_size", 16);
            b.Pressed += onPress;
            AddChild(b);
            return b;
        }
        _partyButton = Header("Party  (P)", _right + 90, () => SetPanel(_panel == Panel.Party ? Panel.Vats : Panel.Party));
        _jobsButton = Header("Jobs  (J)", _right + 250, () => SetPanel(_panel == Panel.Jobs ? Panel.Vats : Panel.Jobs));
        _labButton = Header("Lab  (L)", _right + 410, () => SetPanel(_panel == Panel.Lab ? Panel.Vats : Panel.Lab));
        _boardButton = Header("Map  (M)", _right + 570, () => GoToMap());

        if (VatDemo)
        {
            _pen.AddRange(Ranch.Creatures.Where(r => r.Rested).Take(2).Select(r => r.Id));
            var newest = Ranch.Creatures.LastOrDefault(r => r.Generation > 0);
            if (newest != null) ChildShown = Ranch.Get(newest.Id);
        }
        if (JustJoined.Count > 0)
            Flash($"{string.Join(", ", JustJoined.Select(id => Ranch.Get(id).Name))} joined the ranch.");
        if (Ranch.Report.Count > 0) _notes.Text = string.Join("\n", Ranch.Report);
        Select(Ranch.Creatures.FirstOrDefault(r => r.Name == StartOn)?.Id ?? Ranch.Squad.FirstOrDefault(Ranch.Creatures[0].Id));
        Rebuild();
        if (Station == null) AskChoice(this, Ranch, AutoFight, Flash, Rebuild);
        if (StartInLab) SetPanel(Panel.Lab);
        if (StartOnJobs) SetPanel(Panel.Jobs);
        if (StartOnParty) SetPanel(Panel.Party);
        if (Station != null) ApplyStation();
        if (AutoFight && Station == null) GetTree().CreateTimer(1.5).Timeout += () => { if (IsInsideTree()) AutoBattle(this, Ranch, Fight); };
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
    /// the war table leads, the lab grafts. Esc goes back out.
    /// </summary>
    void ApplyStation()
    {
        foreach (var b in new[] { _partyButton, _jobsButton, _labButton, _boardButton }) b.Visible = false;
        var back = new Button { Text = Phone.Keys("Back to the base  (Esc)", "Back to the base  (B)"), FocusMode = FocusModeEnum.None, Position = new Vector2(GetViewportRect().Size.X - 280, 22), CustomMinimumSize = new Vector2(250, 40) };
        back.AddThemeFontSizeOverride("font_size", 16);
        back.Pressed += () => Back?.Invoke();
        AddChild(back);
        SetPanel(Station switch { "lab" => Panel.Lab, "yard" => Panel.Jobs, "war" => Panel.Party, _ => Panel.Vats });
        var keep = Station switch
        {
            "pens" => new[] { _squadButton, _nameButton, _releaseButton },
            "vats" => Phone.On ? new[] { _vatButton, _breedButton } : new[] { _vatButton },
            "armory" => new[] { _gearButton },
            _ => Array.Empty<Button>(),
        };
        foreach (var b in new[] { _squadButton, _classButton, _secondButton, _vatButton, _releaseButton, _nameButton, _gearButton, _breedButton }) b.Visible = keep.Contains(b);
        if (Station != "vats")
        {
            // No vats here: the creature's detail moves up into their space, and the base keeps the news.
            _vats.Visible = false;
            _notes.Visible = false;
            _column.Position = new Vector2(_right, _top + 14);
        }
        // A phone's vats leave no room under them for the training too: the creature's detail is enough there.
        else if (Phone.On) _training.Visible = false;
        _help.Text = Phone.On ? Station switch
        {
            "pens" => "A or tap: select    the buttons: squad, name, release    B: back to the base    Start: menu",
            "vats" => "A or tap: select    Into vat, twice, then Breed    B: back to the base    Start: menu",
            _ => "A or tap: select    B: back to the base    Start: menu",
        } : Station switch
        {
            "pens" => "Click: select    S: squad    Delete twice: release    Esc: back to the base    N: sound",
            "vats" => "Click: select    Right-click: into a vat    Enter: breed    Esc: back to the base    N: sound",
            "armory" => "Click: select    Gear: next piece from the bag    Esc: back to the base    N: sound",
            _ => "Click: select a beast    Esc: back to the base    N: sound",
        };
        Flash(Station switch
        {
            "pens" => "The pens: pick the squad (S), name your beasts, or let one go.",
            "vats" => Phone.On ? "The vats: put two beasts into the vats (Into vat), then Breed." : "The vats: right-click two beasts into the vats, then Enter to breed.",
            "armory" => "The armory: fit gear from the bag (Gear cycles through it).",
            "yard" => "The training yard: every class this beast could train, and what each still needs.",
            "war" => "The war table: your tamers lead from off the field, up to two, three beasts each.",
            "lab" => "The lab (chem): tune stats with infusions, put in new blood, splice in another species' ability, fit augments from the bag, or graft parts.",
            _ => "",
        });
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
        var box = new StyleBoxFlat { BgColor = new Color(0.08f, 0.06f, 0.13f), BorderColor = Gold };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(8);
        box.SetContentMarginAll(18);
        panel.AddThemeStyleboxOverride("panel", box);
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
        if (_selected is { } sid) { ShowDetail(sid); RefreshSelected(); }
    }

    void Rebuild()
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        foreach (var rec in Ranch.Creatures)
        {
            var card = new RanchCard { Screen = this, Id = rec.Id };
            int id = rec.Id;
            card.Clicked += right => { if (right && Station is null or "vats") TogglePen(id); else Select(id); };
            card.Hovered += () => ShowDetail(id);
            _grid.AddChild(card);
            if (Phone.On && id == _selected) PadNav.First(card);
        }
        _header.Text = Station != null ? HomeBase.Get(Station)?.Name.ToUpperInvariant() ?? "THE RANCH" : "THE RANCH";
        _headerChips?.QueueFree();
        var chips = new List<Chip>
        {
            new Chip("paw", Ranch.Creatures.Count.ToString(), Ink, "your beasts"),
            new Chip("fist", $"{Ranch.Squad.Count}/{Ranch.SquadSize}", Ranch.Squad.Count == 0 ? Ui.Bad : Ink, "squad: three beasts for each tamer leading (P)"),
            new Chip("flask", Ranch.Chem.ToString(), Gold, "chem, for the lab"),
            new Chip("mountain", "#" + Ranch.Encounter, Ink, "next encounter"),
            new Chip("sun", Ranch.CityStanding.ToString(), Gold, "standing with the city above"),
            new Chip("moon", Ranch.UnderStanding.ToString(), Magenta, "standing with the undercity"),
        };
        // Titles won from rival tamers.
        if (Ranch.Titles.Count > 0) chips.Add(new Chip("sparkle", Ranch.Titles.Count.ToString(), Gold, "titles: " + string.Join(", ", Ranch.Titles)));
        _headerChips = Ui.Chips(chips, 22, 2, 22);
        _headerChips.Position = new Vector2(270, 20);
        _headerChips.Size = new Vector2(Math.Min(1100, GetViewportRect().Size.X - 580), 40);
        AddChild(_headerChips);
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
        ShowDetail(id);
        RefreshSelected();
    }

    /// <summary>The selected creature's training: class, stars and JP, skill icons, slots, and the job tree in brief.</summary>
    void RefreshSelected()
    {
        bool any = _selected != null && Ranch.Creatures.Any(r => r.Id == _selected);
        foreach (var b in new[] { _squadButton, _classButton, _secondButton, _vatButton, _releaseButton, _nameButton, _gearButton }) b.Disabled = !any;
        _breedButton.Disabled = _pen.Count < 2;
        Clear(_training);
        if (!any) return;
        int id = _selected.Value;
        var c = Ranch.Get(id);
        var t = Ranch.Record(id).Training;
        _squadButton.Text = InSquad(id) ? "Leave squad" : "Join squad";
        _classButton.Text = $"{t.Class}  >";
        _classButton.Icon = Glyphs.Get(SkillIcons.ClassEmblem(t.Class), 1);
        _classButton.TooltipText = "Cycle the main class";
        _secondButton.Text = $"2nd: {t.Secondary ?? "none"}  >";
        _secondButton.Icon = t.Secondary != null ? Glyphs.Get(SkillIcons.ClassEmblem(t.Secondary), 1) : null;
        _secondButton.TooltipText = "Cycle the secondary skill set";
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

        // Everything else it brings: secondary skills, an inherited skill, the two passives.
        var extras = Row();
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
            extras.AddChild(Ui.Picture(SkillIcons.Passive(p, 2), $"{p.Name} ({(p == shine ? "shiny, its own" : p == gear ? "gear" : p == natural ? (tierNow == Tier.Thoroughbred ? "thoroughbred, its species' own" : "chimera, other species' abilities") : p.Kind == PassiveKind.Reaction ? "reaction" : "support")})\n{p.Text}"));
            extras.AddChild(Ui.Text(p.Name, 14, p == shine || p == gear ? Gold : p == natural ? Color.FromHtml(TierBook.Hex(tierNow == Tier.Thoroughbred ? Tier.Thoroughbred : Tier.Chimera)) : p.Kind == PassiveKind.Reaction ? Ui.Pink : Ui.Good, 4));
        }
        if (extras.GetChildCount() > 0) _training.AddChild(extras);

        var standings = ClassBook.Classes.Select(k => ClassBook.StandingOf(k, c, t)).ToList();
        _training.AddChild(Ui.Chips(new[]
        {
            new Chip("check", $"{standings.Count(s => s == ClassBook.Standing.Open)} open", Ui.Good, "classes it can train now"),
            new Chip("lock", $"{standings.Count(s => s == ClassBook.Standing.Locked)} locked", Dim, "classes it could unlock"),
            new Chip("x", $"{standings.Count(s => s == ClassBook.Standing.Never)} never", Ui.Bad, "classes its body can never take"),
        }.Concat(Station == null ? new[] { new Chip("dot", "J: job tree", Dim) } : Array.Empty<Chip>()), 15));
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
        _partyButton.Text = p == Panel.Party ? "Vats  (P)" : "Party  (P)";
        if (p == Panel.Party) _party.Refresh();
        _jobsButton.Text = p == Panel.Jobs ? "Vats  (J)" : "Jobs  (J)";
        if (p == Panel.Jobs && _selected is { } jid) { _jobs.Reset(); _jobs.ShowCreature(jid); }
        _labButton.Text = p == Panel.Lab ? "Vats  (L)" : "Lab  (L)";
        if (p == Panel.Lab && _selected is { } id) _lab.ShowCreature(id);
        Flash(p switch
        {
            Panel.Lab => "The lab (chem): tune stats with infusions, put in new blood, splice in another species' ability, fit augments from the bag, or graft parts.",
            Panel.Jobs => "The job tree: every class this beast could ever train, and what each still needs.",
            Panel.Party => "Your tamers lead from off the field: up to two, three beasts each.",
            _ => "",
        });
    }

    void GoToMap()
    {
        if (Ranch.Squad.Count == 0) { Flash("Pick at least one beast for the squad (S) first."); return; }
        Ranch.Save();
        OpenMap?.Invoke();
    }

    /// <summary>The next piece of gear from the bag (or none), in name order.</summary>
    void CycleGear(int id)
    {
        var t = Ranch.Record(id).Training;
        var options = new List<string> { null };
        options.AddRange(Ranch.Items.Where(kv => GearBook.IsGear(kv.Key) && kv.Value > 0).Select(kv => kv.Key).Append(t.Gear)
            .Where(g => g != null).Distinct().OrderBy(g => ClassBook.Passive(g).Name));
        if (options.Count == 1) { Flash("No gear in the bag: shops on the map (M) sell it."); return; }
        string next = options[(options.IndexOf(t.Gear) + 1) % options.Count];
        Flash(Ranch.Equip(id, next));
        Ranch.Save();
        Rebuild();
        ShowDetail(id);
        RefreshSelected();
    }

    void CycleClass(int id)
    {
        var c = Ranch.Get(id);
        var t = Ranch.Record(id).Training;
        var open = ClassBook.Available(c, t).Select(k => k.Id).ToList();
        t.Class = open[(open.IndexOf(t.Class) + 1) % open.Count];
        if (t.Secondary == t.Class) t.Secondary = null;
        Ranch.Save();
        RefreshSelected();
    }

    void CycleSecondary(int id)
    {
        // Any other class it has learned something in, or none.
        var t = Ranch.Record(id).Training;
        var options = new List<string> { null };
        options.AddRange(ClassBook.Classes.Select(k => k.Id).Where(k => k != t.Class && t.LearnedIn(k) > 0));
        t.Secondary = options[(options.IndexOf(t.Secondary) + 1) % options.Count];
        if (options.Count == 1) Flash("Learn a skill in another class first; then it can be a secondary skill set.");
        Ranch.Save();
        RefreshSelected();
    }

    void ToggleSquad(int id)
    {
        if (InSquad(id)) Ranch.RemoveFromSquad(id);
        else if (Ranch.Away.TryGetValue(id, out var away)) { Flash($"{Ranch.Get(id).Name} is away on a dispatch until day {away.BackOn}."); return; }
        else if (Ranch.Record(id).Steeping) { Flash($"{Ranch.Get(id).Name} is steeping: a new infusion is taking. It can fight again after the next battle."); return; }
        else if (Ranch.Record(id).Recovering) { Flash($"{Ranch.Get(id).Name} is recovering from an episode. It can fight again after the next battle."); return; }
        else if (Ranch.AddToSquad(id)) Flash($"{Ranch.Get(id).Name} joins {Ranch.LeaderOf(id).Name}'s squad.");
        else { Flash("Every tamer leads three already: take someone out first, or field another tamer (P)."); return; }
        Ranch.Save();
        Rebuild();
        if (_panel == Panel.Party) _party.Refresh();
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
        _notes.Text = _pen.Count == 2 ? "Press Enter to breed." : "";
        Rebuild();
    }

    void Breed()
    {
        if (_pen.Count < 2) { Flash("Put two rested beasts in the vats first (right-click)."); return; }
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

    /// <summary>The hovered creature: what it is, its stats in glyphs, its gear, its parts, its lineage.</summary>
    void ShowDetail(int id)
    {
        Clear(_detail);
        var c = Ranch.Get(id);
        var rec = Ranch.Record(id);
        var u = Unit.From(c, Tactics.Side.Player, rec.Training);
        var s = c.Species;
        string what = (c.Nickname != null ? $"{s.Name} · " : "") + $"{c.Size.ToString().ToLowerInvariant()} {s.Plan.ToString().ToLowerInvariant()}"
            + (s.Strain != Strain.Natural ? $" · {s.Strain.Label().ToLowerInvariant()}" : "")
            + (s.Origin != null ? $" · from {s.Origin}" : "");
        var head = Row(Ui.Text(c.Name, 22, Ink), Ui.Text(what, 15, Dim, 4));
        var tier = TierBook.Of(c);
        head.AddChild(Ui.ChipView(new Chip(TierBook.Glyph(tier), tier.ToString(), Color.FromHtml(TierBook.Hex(tier)), TierBook.Describe(c, tier)), 15));
        if (c.Shiny) head.AddChild(Ui.ChipView(new Chip("sparkle", "shiny", Gold), 15));
        if (c.Mutation != null) head.AddChild(Ui.ChipView(new Chip("swirl", c.Mutation.ToString().ToLowerInvariant(), Ui.Pink, "mutation"), 15));
        _detail.AddChild(head);
        // What it was born with (natural, passed down), and what's bolted on (neon and brass, not).
        var born = c.Traits.Select(ClassBook.Passive).Where(p => p != null).Select(p => new Chip("paw", p.Name, Color.FromHtml("#9ac25a"), $"Trait: {p.Text} Passed down when bred."));
        var fitted = c.Augments.Select(AugmentBook.Get).Where(a => a != null).Select(a => new Chip(a.Chem ? "flask" : "gear", a.Name, Color.FromHtml(a.Chem ? "#4ff0a0" : "#4fe3ff"), AugmentBook.Text(a)));
        var tuned = c.Infusions.Select(InfusionBook.Get).Where(p => p != null).Select(p => new Chip("flask", p.Name, Color.FromHtml(TierBook.Hex(Tier.Infused)), $"Infusion: {p.Text} Not passed down."));
        var spliced = (c.Species.Spliced ?? Array.Empty<Form>()).Select(f => new Chip("helix", TierBook.Ability(f), Color.FromHtml(TierBook.Hex(Tier.Chimera)),
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

        // Parts, for grafting: only the ones it has.
        var p = c.Parts;
        var parts = new (string slot, Enum part)[] { ("head", p.Head), ("ears", p.Ears), ("eyes", p.Eyes), ("horns", p.Horns), ("back", p.Back), ("limbs", p.Limbs), ("tail", p.Tail), ("wings", p.Wings), ("pattern", p.Pattern) }
            .Select(x => (x.slot, word: Creature.Words(x.part))).Where(x => x.word != "none").Select(x => $"{x.slot} {x.word}");
        string grafts = s.Strain != Strain.Natural && s.Grafts != Graft.None ? "   ·   grafts: " + s.Grafts.ToString().ToLowerInvariant() : "";
        var partsLine = Ui.Text(string.Join(" · ", parts) + grafts, 13, Dim, 3);
        partsLine.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detail.AddChild(partsLine);

        var tags = new List<Chip>();
        if (c.Generation > 0) tags.Add(new Chip("paw", $"gen {c.Generation}: {c.ParentA} + {c.ParentB}", Dim, "bred in the vats"));
        if (!rec.Rested) tags.Add(new Chip("clock", "resting", Gold, "can breed again after the next battle"));
        if (rec.Steeping) tags.Add(new Chip("flask", "steeping", Color.FromHtml(TierBook.Hex(Tier.Infused)), "a new infusion is taking: it sits out the next battle"));
        if (rec.Recovering) tags.Add(new Chip("skull", "recovering", Color.FromHtml(TierBook.Hex(Tier.Abomination)), "an abomination's episode: it sits out the next battle"));
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
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (Station != null)
        {
            // At a station only its own keys work; the rest of the ranch is walked to.
            if (k.Keycode == Key.Escape && _nameBox.Visible) { _nameBox.Visible = false; }
            else if (k.Keycode == Key.Escape) Back?.Invoke();
            else if (k.Keycode == Phone.Start) AddChild(new PauseMenu { Save = Ranch.Save, ToTitle = ToTitle });
            else if (k.Keycode is Key.Enter or Key.KpEnter && Station == "vats") Breed();
            else if (k.Keycode == Key.S && Station == "pens" && _selected is { } ps) ToggleSquad(ps);
            else if (k.Keycode == Key.Delete && Station == "pens" && _selected is { } pd) Release(pd);
            else return;
            GetViewport().SetInputAsHandled();
            return;
        }
        switch (k.Keycode)
        {
            case Key.Enter:
            case Key.KpEnter:
                Breed();
                break;
            case Key.F:
            case Key.M:
                GoToMap();
                break;
            case Key.J:
                SetPanel(_panel == Panel.Jobs ? Panel.Vats : Panel.Jobs);
                break;
            case Key.P:
                SetPanel(_panel == Panel.Party ? Panel.Vats : Panel.Party);
                break;
            case Key.L:
                SetPanel(_panel == Panel.Lab ? Panel.Vats : Panel.Lab);
                break;
            case Key.S when _selected is { } sid:
                ToggleSquad(sid);
                break;
            case Key.Delete when _selected is { } did:
                Release(did);
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
        // The pad: a card takes the focus (A selects it), and shows its detail as hovering does.
        if (Phone.On)
        {
            FocusMode = FocusModeEnum.All;
            FocusEntered += () => { QueueRedraw(); Hovered?.Invoke(); };
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
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.08f, 0.16f, 0.92f),
            BorderColor = Screen.IsSelected(Id) ? Color.FromHtml("#f2c14e") : pen >= 0 ? Color.FromHtml("#ff4f9a") : squad ? Color.FromHtml("#4fe3ff") : _hover ? new Color(0.6f, 0.55f, 0.75f) : new Color(0.24f, 0.2f, 0.33f),
        };
        box.SetBorderWidthAll(Screen.IsSelected(Id) || squad || pen >= 0 ? 3 : _hover ? 2 : 1);
        box.SetCornerRadiusAll(8);
        DrawStyleBox(box, new Rect2(Vector2.Zero, Size));
        if (HasFocus())
        {
            // Where the pad is: a white frame outside the card's own.
            var ring = new StyleBoxFlat { DrawCenter = false, BorderColor = Colors.White };
            ring.SetBorderWidthAll(2);
            ring.SetCornerRadiusAll(10);
            DrawStyleBox(ring, new Rect2(new Vector2(-4, -4), Size + new Vector2(8, 8)));
        }

        int z = _tex.GetWidth() * 2 <= Size.X - 8 && _tex.GetHeight() * 2 <= 128 ? 2 : 1;
        var ts = _tex.GetSize() * z;
        float ground = 136;
        DrawSetTransform(new Vector2(Size.X / 2, ground - 2), 0, new Vector2(1, 0.22f));
        DrawCircle(Vector2.Zero, ts.X * 0.42f, new Color(0, 0, 0, 0.4f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var modulate = rec.Rested ? Colors.White : new Color(0.65f, 0.62f, 0.75f);
        DrawTextureRect(_tex, new Rect2(new Vector2(Mathf.Round(Size.X / 2 - ts.X / 2), ground - ts.Y), ts), false, modulate);

        var font = GetThemeDefaultFont();
        DrawString(font, new Vector2(0, ground + 24), c.Name, HorizontalAlignment.Center, Size.X, 17, Color.FromHtml("#e8e2f4"));
        DrawString(font, new Vector2(0, ground + 44), $"{c.Size} {c.Species.Plan}", HorizontalAlignment.Center, Size.X, 13, Color.FromHtml("#9a91b4"));

        // Marks in the top corners.
        if (squad) DrawString(font, new Vector2(8, 20), Screen.LeaderTag(Id), HorizontalAlignment.Left, -1, 13, Color.FromHtml("#4fe3ff"));
        if (Screen.Ranch.Away.TryGetValue(Id, out var away))
            DrawString(font, new Vector2(8, 20), $"AWAY {Math.Max(0, away.BackOn - Screen.Ranch.Day)}d", HorizontalAlignment.Left, -1, 13, Color.FromHtml("#ff4f9a"));
        if (pen >= 0) DrawString(font, new Vector2(8, squad ? 38 : 20), pen == 0 ? "VAT A" : "VAT B", HorizontalAlignment.Left, -1, 13, Color.FromHtml("#ff4f9a"));
        string tag = Screen.IsNew(Id) ? "NEW" : c.Generation > 0 ? $"GEN {c.Generation}" : "";
        if (tag != "") DrawString(font, new Vector2(0, 20), tag, HorizontalAlignment.Right, Size.X - 8, 13, Color.FromHtml("#f2c14e"));
        if (rec.Steeping) DrawString(font, new Vector2(0, 38), "steeping", HorizontalAlignment.Right, Size.X - 8, 12, Color.FromHtml("#4ff0a0"));
        else if (rec.Recovering) DrawString(font, new Vector2(0, 38), "recovering", HorizontalAlignment.Right, Size.X - 8, 12, Color.FromHtml("#ff5a6a"));
        else if (!rec.Rested) DrawString(font, new Vector2(0, 38), "resting", HorizontalAlignment.Right, Size.X - 8, 12, Color.FromHtml("#9a91b4"));
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
        Vat(new Rect2(0, 30, 220, h), "VAT A", A, Color.FromHtml("#ff4f9a"));
        Vat(new Rect2(250, 30, 220, h), "VAT B", B, Color.FromHtml("#ff4f9a"));
        Vat(new Rect2(500, 30, 220, h), "OFFSPRING", Screen.ChildShown, Color.FromHtml("#4fe3ff"));
        DrawString(font, new Vector2(226, 30 + h / 2), "+", HorizontalAlignment.Left, -1, 28, Color.FromHtml("#e8e2f4"));
        DrawString(font, new Vector2(474, 30 + h / 2), "=", HorizontalAlignment.Left, -1, 28, Color.FromHtml("#e8e2f4"));
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
        DrawString(font, new Vector2(r.Position.X, r.End.Y + 20), c.Name, HorizontalAlignment.Center, r.Size.X, 17, Color.FromHtml("#e8e2f4"));
    }

    public override void _Process(double delta) => QueueRedraw();
}
