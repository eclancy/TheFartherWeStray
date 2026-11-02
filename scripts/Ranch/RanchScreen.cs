using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
using CreatureTactics.SpriteLab;
using CreatureTactics.Tactics;
using CreatureTactics.UI;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>
/// The ranch: your roster, the squad, training, and the breeding vats. Click a creature to select
/// it: its panel sets squad, class and secondary skills. Right-click puts it in a breeding vat;
/// Enter breeds; F goes to battle.
/// </summary>
public partial class RanchScreen : Control
{
    public Ranch Ranch;
    public Action<Mission> Fight;
    public List<int> JustJoined = new(); // recruits from the last battle, to point out
    public bool VatDemo;                  // screenshots: fill the vats with the first rested pair and the newest child
    public bool AutoFight;                // --autoplay: head into the next battle on its own (an attract loop)
    public bool StartInLab;               // --labmode: open on the lab (screenshots)
    public bool StartOnBoard;             // --boardmode: open on the mission board (screenshots)
    public bool StartOnJobs;              // --jobsmode: open on the job tree (screenshots)
    public bool StartOnParty;             // --partymode: open on the party (screenshots)

    static readonly Color Cyan = Color.FromHtml("#4fe3ff"), Magenta = Color.FromHtml("#ff4f9a"), Gold = Color.FromHtml("#f2c14e");
    static readonly Color Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#9a91b4");

    GridContainer _grid;
    Label _header, _status, _notes;
    VBoxContainer _detail, _training;
    Control _headerChips;
    VatPanel _vats;
    Button _squadButton, _classButton, _secondButton, _vatButton, _releaseButton, _labButton, _boardButton, _jobsButton;
    HBoxContainer _buttonsRow;
    LabPanel _lab;
    enum Panel { Vats, Lab, Board, Jobs, Party }
    Panel _panel = Panel.Vats;
    MissionBoardPanel _board;
    JobsPanel _jobs;
    PartyPanel _party;
    Button _partyButton;
    readonly List<int> _pen = new();
    int? _selected, _releaseArmed;
    double _releaseTimer;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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

        _header = Text(26, Ink, new Vector2(32, 20), new Vector2(1860, 40));
        _status = Text(17, Gold, new Vector2(32, 62), new Vector2(1100, 30));

        var scroll = new ScrollContainer { Position = new Vector2(24, 100), Size = new Vector2(1120, 920), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _grid = new GridContainer { Columns = 6 };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_grid);
        Text(16, Dim, new Vector2(32, 1036), new Vector2(1120, 30)).Text =
            "Click: select    Right-click: vat    Enter: breed    J: jobs    L: lab    M or F: missions    S: squad    Delete twice: release    Tab: Sprite Lab";

        _vats = new VatPanel { Position = new Vector2(1170, 96), Size = new Vector2(720, 320), Screen = this, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_vats);
        _notes = Text(15, Ink, new Vector2(1170, 432), new Vector2(720, 96));
        _notes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        // Hover detail and the selected creature's training, stacked in one column.
        var column = new VBoxContainer { Position = new Vector2(1170, 532), Size = new Vector2(720, 430) };
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);
        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 6);
        column.AddChild(_detail);
        _training = new VBoxContainer();
        _training.AddThemeConstantOverride("separation", 6);
        column.AddChild(_training);

        var buttons = _buttonsRow = new HBoxContainer { Position = new Vector2(1170, 970) };
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

        _lab = new LabPanel { Ranch = Ranch, Position = new Vector2(1170, 96), Size = new Vector2(720, 930), Visible = false };
        _lab.Done = msg => { Flash(msg); Rebuild(); if (_selected is { } sid) ShowDetail(sid); };
        AddChild(_lab);
        _board = new MissionBoardPanel { Ranch = Ranch, Position = new Vector2(1170, 96), Size = new Vector2(720, 930), Visible = false };
        _board.Depart = Depart;
        AddChild(_board);
        _jobs = new JobsPanel { Ranch = Ranch, Position = new Vector2(1170, 96), Size = new Vector2(730, 930), Visible = false };
        _jobs.Changed = msg => { Flash(msg); Rebuild(); if (_selected is { } sid) ShowDetail(sid); };
        AddChild(_jobs);
        _party = new PartyPanel { Ranch = Ranch, Selected = () => _selected, Position = new Vector2(1170, 96), Size = new Vector2(730, 930), Visible = false };
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
        _partyButton = Header("Party  (P)", 1260, () => SetPanel(_panel == Panel.Party ? Panel.Vats : Panel.Party));
        _jobsButton = Header("Jobs  (J)", 1420, () => SetPanel(_panel == Panel.Jobs ? Panel.Vats : Panel.Jobs));
        _labButton = Header("Lab  (L)", 1580, () => SetPanel(_panel == Panel.Lab ? Panel.Vats : Panel.Lab));
        _boardButton = Header("Missions  (M)", 1740, () => SetPanel(_panel == Panel.Board ? Panel.Vats : Panel.Board));

        if (VatDemo)
        {
            _pen.AddRange(Ranch.Creatures.Where(r => r.Rested).Take(2).Select(r => r.Id));
            var newest = Ranch.Creatures.LastOrDefault(r => r.Generation > 0);
            if (newest != null) ChildShown = Ranch.Get(newest.Id);
        }
        if (JustJoined.Count > 0)
            Flash($"{string.Join(", ", JustJoined.Select(id => Ranch.Get(id).Species.Name))} joined the ranch.");
        if (Ranch.Report.Count > 0) _notes.Text = string.Join("\n", Ranch.Report);
        Select(Ranch.Squad.FirstOrDefault(Ranch.Creatures[0].Id));
        Rebuild();
        if (StartInLab) SetPanel(Panel.Lab);
        if (StartOnBoard) SetPanel(Panel.Board);
        if (StartOnJobs) SetPanel(Panel.Jobs);
        if (StartOnParty) SetPanel(Panel.Party);
        if (AutoFight) GetTree().CreateTimer(1.5).Timeout += () => { if (IsInsideTree() && Ranch.Squad.Count > 0) Fight?.Invoke(Mission.Board(Ranch.Encounter, Ranch.Codex)[0]); };
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

    void Rebuild()
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        foreach (var rec in Ranch.Creatures)
        {
            var card = new RanchCard { Screen = this, Id = rec.Id };
            int id = rec.Id;
            card.Clicked += right => { if (right) TogglePen(id); else Select(id); };
            card.Hovered += () => ShowDetail(id);
            _grid.AddChild(card);
        }
        _header.Text = "THE RANCH";
        _headerChips?.QueueFree();
        _headerChips = Ui.Chips(new[]
        {
            new Chip("paw", Ranch.Creatures.Count.ToString(), Ink, "creatures"),
            new Chip("fist", $"{Ranch.Squad.Count}/{Ranch.SquadSize}", Ranch.Squad.Count == 0 ? Ui.Bad : Ink, "squad: three creatures for each tamer leading (P)"),
            new Chip("flask", Ranch.Chem.ToString(), Gold, "chem, for the lab"),
            new Chip("mountain", "#" + Ranch.Encounter, Ink, "next encounter"),
        }, 22, 2, 22);
        _headerChips.Position = new Vector2(230, 20);
        _headerChips.Size = new Vector2(1100, 40);
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
        foreach (var b in new[] { _squadButton, _classButton, _secondButton, _vatButton, _releaseButton }) b.Disabled = !any;
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
        foreach (var p in new[] { ClassBook.Passive(t.Passive), ClassBook.Passive(t.Reaction) }.Where(p => p != null))
        {
            extras.AddChild(Ui.Picture(SkillIcons.Passive(p, 2), $"{p.Name} ({(p.Kind == PassiveKind.Reaction ? "reaction" : "support")})\n{p.Text}"));
            extras.AddChild(Ui.Text(p.Name, 14, p.Kind == PassiveKind.Reaction ? Ui.Pink : Ui.Good, 4));
        }
        if (extras.GetChildCount() > 0) _training.AddChild(extras);

        var standings = ClassBook.Classes.Select(k => ClassBook.StandingOf(k, c, t)).ToList();
        _training.AddChild(Ui.Chips(new[]
        {
            new Chip("check", $"{standings.Count(s => s == ClassBook.Standing.Open)} open", Ui.Good, "classes it can train now"),
            new Chip("lock", $"{standings.Count(s => s == ClassBook.Standing.Locked)} locked", Dim, "classes it could unlock"),
            new Chip("x", $"{standings.Count(s => s == ClassBook.Standing.Never)} never", Ui.Bad, "classes its body can never take"),
            new Chip("dot", "J: job tree", Dim),
        }, 15));
    }
    /// <summary>Swaps the right-hand side between breeding and training, and the lab.</summary>
    void SetPanel(Panel p)
    {
        _panel = p;
        foreach (var c in new Control[] { _vats, _notes, _training, _buttonsRow, _detail }) c.Visible = p == Panel.Vats;
        _lab.Visible = p == Panel.Lab;
        _board.Visible = p == Panel.Board;
        _jobs.Visible = p == Panel.Jobs;
        _party.Visible = p == Panel.Party;
        _partyButton.Text = p == Panel.Party ? "Vats  (P)" : "Party  (P)";
        if (p == Panel.Party) _party.Refresh();
        _jobsButton.Text = p == Panel.Jobs ? "Vats  (J)" : "Jobs  (J)";
        if (p == Panel.Jobs && _selected is { } jid) { _jobs.Reset(); _jobs.ShowCreature(jid); }
        _labButton.Text = p == Panel.Lab ? "Vats  (L)" : "Lab  (L)";
        _boardButton.Text = p == Panel.Board ? "Vats  (M)" : "Missions  (M)";
        if (p == Panel.Lab && _selected is { } id) _lab.ShowCreature(id);
        if (p == Panel.Board) _board.Refresh();
        Flash(p switch
        {
            Panel.Lab => "The lab: graft parts from your codex, fit brass, or infuse new blood. It all costs chem.",
            Panel.Board => "Pick a contract, then Depart (F) with your squad.",
            Panel.Jobs => "The job tree: every class this creature could ever train, and what each still needs.",
            Panel.Party => "Your tamers lead from off the field: up to two, three creatures each.",
            _ => "",
        });
    }

    void Depart(Mission m)
    {
        if (Ranch.Squad.Count == 0) { Flash("Pick at least one creature for the squad (S)."); return; }
        Ranch.Save();
        Fight?.Invoke(m);
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
        else if (Ranch.AddToSquad(id)) Flash($"{Ranch.Get(id).Species.Name} joins {Ranch.LeaderOf(id).Name}'s squad.");
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
        else if (!Ranch.Record(id).Rested) { Flash($"{Ranch.Get(id).Species.Name} is resting. Fight a battle and everyone can breed again."); return; }
        else if (_pen.Count >= 2) { Flash("Both vats are full: take one out first."); return; }
        else _pen.Add(id);
        ChildShown = null;
        _notes.Text = _pen.Count == 2 ? "Press Enter to breed." : "";
        Rebuild();
    }

    void Breed()
    {
        if (_pen.Count < 2) { Flash("Put two rested creatures in the vats first (right-click)."); return; }
        var a = Ranch.Get(_pen[0]);
        var b = Ranch.Get(_pen[1]);
        var (id, result) = Ranch.Breed(_pen[0], _pen[1]);
        _pen.Clear();
        ChildShown = Ranch.Get(id);
        Ranch.Save();
        var lines = new List<string> { $"{ChildShown.Species.Name} is born (generation {ChildShown.Generation})." };
        if (result.FromA.Count > 0) lines.Add($"From {a.Species.Name}: {string.Join(", ", result.FromA)}");
        if (result.FromB.Count > 0) lines.Add($"From {b.Species.Name}: {string.Join(", ", result.FromB)}");
        if (result.Notes.Count > 0) lines.Add(string.Join("; ", result.Notes));
        _notes.Text = string.Join("\n", lines);
        Flash($"{a.Species.Name} and {b.Species.Name} rest until the next battle.");
        Select(id);
        Rebuild();
    }

    void Release(int id)
    {
        if (Ranch.Creatures.Count <= 1) { Flash("You can't release your last creature."); return; }
        if (_releaseArmed != id)
        {
            _releaseArmed = id;
            _releaseTimer = 3;
            Flash($"Release {Ranch.Get(id).Species.Name} back to the wild? Press Release (or Delete) again.");
            return;
        }
        Flash($"{Ranch.Get(id).Species.Name} returns to the wild.");
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
        string what = $"{c.Size.ToString().ToLowerInvariant()} {s.Plan.ToString().ToLowerInvariant()}"
            + (s.Strain != Strain.Natural ? $" · {s.Strain.ToString().ToLowerInvariant()}" : "")
            + (s.Origin != null ? $" · from {s.Origin}" : "");
        var head = Row(Ui.Text(s.Name, 22, Ink), Ui.Text(what, 15, Dim, 4));
        if (c.Shiny) head.AddChild(Ui.ChipView(new Chip("sparkle", "shiny", Gold), 15));
        if (c.Mutation != null) head.AddChild(Ui.ChipView(new Chip("swirl", c.Mutation.ToString().ToLowerInvariant(), Ui.Pink, "mutation"), 15));
        _detail.AddChild(head);

        _detail.AddChild(Ui.Chips(new[]
        {
            new Chip("hp", u.MaxHp.ToString(), Ink, "max HP"), new Chip("atk", u.Atk.ToString(), Ink, "ATK"), new Chip("def", u.Def.ToString(), Ink, "DEF"),
            new Chip("spd", u.Spd.ToString(), Ink, "SPD"), new Chip("move", u.Move.ToString(), Ink, "MOVE"),
            new Chip(SkillIcons.ClassEmblem(u.ClassName), "as " + u.ClassName, Gold, "stats as its current class"),
        }, 18));

        var (weapon, armor, move) = c.Loadout();
        _detail.AddChild(Ui.Chips(new[]
        {
            new Chip(SkillIcons.WeaponGlyph(c.Weapon), c.Weapon.ToString().ToLowerInvariant(), Ink, "weapon: " + weapon),
            new Chip(SkillIcons.ArmorGlyph(c.Armor), c.Armor.ToString().ToLowerInvariant(), Ink, "armour: " + armor),
            new Chip(SkillIcons.MoveGlyph(c.MoveType), c.MoveType.ToString().ToLowerInvariant(), Ink, "moves: " + move),
            new Chip(SkillIcons.AttitudeGlyph(c.Attitude), c.Attitude.ToString().ToLowerInvariant(), Ink,
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
        switch (k.Keycode)
        {
            case Key.Enter:
            case Key.KpEnter:
                Breed();
                break;
            case Key.F:
                if (_panel != Panel.Board) SetPanel(Panel.Board);
                else Depart(_board.Missions[_board.Selected]);
                break;
            case Key.J:
                SetPanel(_panel == Panel.Jobs ? Panel.Vats : Panel.Jobs);
                break;
            case Key.P:
                SetPanel(_panel == Panel.Party ? Panel.Vats : Panel.Party);
                break;
            case Key.M:
                SetPanel(_panel == Panel.Board ? Panel.Vats : Panel.Board);
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
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
            Clicked?.Invoke(mb.ButtonIndex == MouseButton.Right);
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

        int z = _tex.GetWidth() * 2 <= Size.X - 8 && _tex.GetHeight() * 2 <= 128 ? 2 : 1;
        var ts = _tex.GetSize() * z;
        float ground = 136;
        DrawSetTransform(new Vector2(Size.X / 2, ground - 2), 0, new Vector2(1, 0.22f));
        DrawCircle(Vector2.Zero, ts.X * 0.42f, new Color(0, 0, 0, 0.4f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        var modulate = rec.Rested ? Colors.White : new Color(0.65f, 0.62f, 0.75f);
        DrawTextureRect(_tex, new Rect2(new Vector2(Mathf.Round(Size.X / 2 - ts.X / 2), ground - ts.Y), ts), false, modulate);

        var font = GetThemeDefaultFont();
        DrawString(font, new Vector2(0, ground + 24), c.Species.Name, HorizontalAlignment.Center, Size.X, 17, Color.FromHtml("#e8e2f4"));
        DrawString(font, new Vector2(0, ground + 44), $"{c.Size} {c.Species.Plan}", HorizontalAlignment.Center, Size.X, 13, Color.FromHtml("#9a91b4"));

        // Marks in the top corners.
        if (squad) DrawString(font, new Vector2(8, 20), Screen.LeaderTag(Id), HorizontalAlignment.Left, -1, 13, Color.FromHtml("#4fe3ff"));
        if (pen >= 0) DrawString(font, new Vector2(8, squad ? 38 : 20), pen == 0 ? "VAT A" : "VAT B", HorizontalAlignment.Left, -1, 13, Color.FromHtml("#ff4f9a"));
        string tag = Screen.IsNew(Id) ? "NEW" : c.Generation > 0 ? $"GEN {c.Generation}" : "";
        if (tag != "") DrawString(font, new Vector2(0, 20), tag, HorizontalAlignment.Right, Size.X - 8, 13, Color.FromHtml("#f2c14e"));
        if (!rec.Rested) DrawString(font, new Vector2(0, 38), "resting", HorizontalAlignment.Right, Size.X - 8, 12, Color.FromHtml("#9a91b4"));
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
        DrawString(font, new Vector2(r.Position.X, r.End.Y + 20), c.Species.Name, HorizontalAlignment.Center, r.Size.X, 17, Color.FromHtml("#e8e2f4"));
    }

    public override void _Process(double delta) => QueueRedraw();
}
