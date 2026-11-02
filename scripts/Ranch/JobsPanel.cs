using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
using CreatureTactics.UI;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>
/// The selected creature's job tree (FFTA's job menu, per creature): every class, marked open,
/// locked (with what it still needs) or never possible for this body, with mastery stars. Click a
/// class to see it, then train it as the main class or take its skills as the secondary. Below,
/// the two passive slots: one support and one reaction, each from any class trained.
/// </summary>
public partial class JobsPanel : Control
{
    public Ranch Ranch;
    public Action<string> Changed;

    int? _id;
    string _shown;
    VBoxContainer _box;

    static readonly Color Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#6f6788"), Gold = Color.FromHtml("#f2c14e"),
        Cyan = Color.FromHtml("#4fe3ff"), Red = Color.FromHtml("#b0546a"), Green = Color.FromHtml("#4ff0a0"), Pink = Color.FromHtml("#ff8ac0");

    public override void _Ready()
    {
        var scroll = new ScrollContainer { Size = Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _box = new VBoxContainer { CustomMinimumSize = new Vector2(Size.X - 16, 0) };
        _box.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(_box);
    }

    public void ShowCreature(int id)
    {
        _id = id;
        _shown ??= Ranch.Record(id).Training.Class;
        Build();
    }

    /// <summary>Forget the class being looked at (a different creature was selected).</summary>
    public void Reset() => _shown = null;

    Button Btn(string text, Action onPress, bool enabled = true, int width = 0)
    {
        var b = new Button { Text = text, Disabled = !enabled, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(width, 32), ClipText = width > 0 };
        b.AddThemeFontSizeOverride("font_size", 14);
        b.Pressed += onPress;
        return b;
    }

    static Control Row(params Control[] items)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        foreach (var i in items) row.AddChild(i);
        return row;
    }

    static Label Named(string text, int size, Color color, float width)
    {
        var l = Ui.Text(text, size, color, 4);
        l.CustomMinimumSize = new Vector2(width, 0);
        l.ClipText = true;
        return l;
    }

    /// <summary>A class button: its emblem, its name, and its mastery stars at the right.</summary>
    Button ClassButton(ClassDef k, ClassBook.Standing standing, Creature c, Training t)
    {
        var id = k.Id;
        var b = Btn(k.Name, () => { _shown = id; Build(); }, true, 168);
        b.Icon = SkillIcons.Class(k.Id, standing, 2);
        b.Alignment = HorizontalAlignment.Left;
        b.CustomMinimumSize = new Vector2(168, 40);
        b.TooltipText = $"{k.Name}: {k.Blurb}" + (standing == ClassBook.Standing.Open ? "" : "\nNeeds: " + ClassBook.Hint(k, c, t));
        b.Modulate = k.Id == t.Class ? Gold : k.Id == t.Secondary ? Cyan : standing switch
        {
            ClassBook.Standing.Never => new Color(0.75f, 0.5f, 0.58f),
            ClassBook.Standing.Locked => new Color(0.62f, 0.6f, 0.72f),
            _ => ClassBook.Favoured(c, k.Id) ? Green : Colors.White,
        };
        if (k.Id == _shown) b.AddThemeColorOverride("font_color", Cyan);
        int stars = t.Stars(k.Id);
        if (stars > 0) b.AddChild(new TextureRect { Texture = SkillIcons.Stars(stars, 2), Position = new Vector2(126, 26), MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest });
        return b;
    }

    /// <summary>JP still to earn, or a tick once learned.</summary>
    static Control Cost(bool learned, int toGo) => learned
        ? Ui.ChipView(new Chip("check"), 14)
        : Ui.ChipView(new Chip("lock", $"{toGo} JP", Dim, "JP still to earn in this class"), 14);

    void Build()
    {
        if (_box == null || _id == null) return;
        foreach (var child in _box.GetChildren()) { _box.RemoveChild(child); child.QueueFree(); }
        var c = Ranch.Get(_id.Value);
        var t = Ranch.Record(_id.Value).Training;
        var aff = ClassBook.Affinity[c.Species.Plan];

        _box.AddChild(Ui.Text($"JOB TREE · {c.Name}", 18, Gold));
        // Which classes this body takes to (+50% JP) and which it never can.
        var leanings = aff.favoured.Select(f => new Chip(SkillIcons.ClassEmblem(f), f, Green, "favoured: +50% JP"))
            .Concat(aff.barred.Select(f => new Chip("x", f, Red, "barred: never, for this body"))).ToList();
        if (leanings.Count > 0) _box.AddChild(Ui.Chips(leanings, 14, 2, 12));

        void Section(string title, IEnumerable<ClassDef> classes, string tip)
        {
            var head = Ui.Text(title, 13, Dim, 4);
            head.TooltipText = tip;
            head.MouseFilter = MouseFilterEnum.Stop;
            _box.AddChild(head);
            var grid = new GridContainer { Columns = 4 };
            grid.AddThemeConstantOverride("h_separation", 5);
            grid.AddThemeConstantOverride("v_separation", 5);
            foreach (var k in classes) grid.AddChild(ClassButton(k, ClassBook.StandingOf(k, c, t), c, t));
            _box.AddChild(grid);
        }
        Section("BASE", ClassBook.Classes.Where(k => !k.Advanced), "Open to any body that allows them.");
        Section("PART, FORM & TEMPERAMENT", ClassBook.Classes.Where(k => k.Advanced && k.Requires.Length == 2), "Two base classes mastered, and the body, form, aura or nature for it.");
        Section("BLOOD", ClassBook.Classes.Where(k => k.Advanced && k.Requires.Length == 1), "One base class mastered, and the strain.");

        // The class being looked at: its emblem, stars, JP and mastery bonus, then each lesson in order.
        var shown = ClassBook.Class(_shown ?? t.Class);
        var st = ClassBook.StandingOf(shown, c, t);
        _box.AddChild(new HSeparator());
        var (ma, md, ms, mh) = ClassBook.MasteryBonus(shown.Id);
        var mastery = ma > 0 ? new Chip("atk", "+1", Gold) : md > 0 ? new Chip("def", "+1", Gold) : ms > 0 ? new Chip("spd", "+1", Gold) : new Chip("hp", "+" + mh, Gold);
        var title = Ui.Text(shown.Name, 20, Gold);
        title.TooltipText = shown.Blurb;
        title.MouseFilter = MouseFilterEnum.Stop;
        _box.AddChild(Row(
            Ui.Picture(SkillIcons.Class(shown.Id, st, 3), shown.Blurb),
            title,
            Ui.Picture(SkillIcons.Stars(t.Stars(shown.Id), 3), "mastery: * skills, ** passives, *** mastered"),
            Ui.Chips(new[] { new Chip("star", $"{t.JpIn(shown.Id)} JP", Ink, "JP earned in this class"), mastery with { Tip = "mastery bonus, kept in every class" } }, 16)));

        int spent = 0;
        foreach (var sid in shown.Skills)
        {
            var s = ClassBook.Skill(sid);
            spent += s.Cost;
            bool known = t.Skills.Contains(sid);
            var icon = Ui.Picture(SkillIcons.Skill(s, 2), $"{s.Name}\n{s.Text}");
            if (!known) icon.Modulate = new Color(1, 1, 1, 0.45f);
            _box.AddChild(Row(icon, Named(s.Name, 15, known ? Ink : Dim, 140), Cost(known, Math.Max(0, spent - t.JpIn(shown.Id))), Ui.Chips(Ui.SkillChips(s), 14, 2, 8)));
        }
        foreach (var p in ClassBook.PassivesOf(shown.Id))
        {
            spent += p.Cost;
            bool has = t.Passives.Contains(p.Id);
            var icon = Ui.Picture(SkillIcons.Passive(p, 2), $"{p.Name} ({(p.Kind == PassiveKind.Reaction ? "reaction" : "support")})\n{p.Text}");
            if (!has) icon.Modulate = new Color(1, 1, 1, 0.45f);
            var chips = Ui.PassiveChips(p);
            Control what = chips.Count > 0 && p.Kind == PassiveKind.Support ? Ui.Chips(chips, 14, 2, 8) : Ui.Text(p.Text, 13, Dim, 3);
            if (what is Label l) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; }
            _box.AddChild(Row(icon, Named(p.Name, 15, has ? (p.Kind == PassiveKind.Reaction ? Pink : Green) : Dim, 140), Cost(has, Math.Max(0, spent - t.JpIn(shown.Id))), what));
        }
        if (!t.Masteries.Contains(shown.Id) && t.Stars(shown.Id) >= 2)
            _box.AddChild(Row(Ui.Picture(SkillIcons.Stars(3, 2)), Named("Mastery", 15, Dim, 140), Cost(false, Math.Max(0, spent + ClassBook.MasteryExtra - t.JpIn(shown.Id)))));
        if (st != ClassBook.Standing.Open)
        {
            var needs = Ui.Text(ClassBook.Hint(shown, c, t), 14, st == ClassBook.Standing.Never ? Red : Gold, 4);
            needs.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            needs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _box.AddChild(Row(Ui.Picture(Glyphs.Get(st == ClassBook.Standing.Never ? "x" : "lock", 2)), needs));
        }

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        var main = Btn($"Train as {shown.Name}", () => SetMain(shown.Id), st == ClassBook.Standing.Open && t.Class != shown.Id);
        main.Icon = Glyphs.Get(SkillIcons.ClassEmblem(shown.Id), 1);
        actions.AddChild(main);
        var second = Btn("As secondary", () => SetSecondary(shown.Id), t.Class != shown.Id && t.LearnedIn(shown.Id) > 0 && t.Secondary != shown.Id);
        second.TooltipText = $"Use its learned {shown.Name} skills alongside the main class";
        actions.AddChild(second);
        if (t.Secondary != null) actions.AddChild(Btn("No secondary", () => SetSecondary(null)));
        _box.AddChild(actions);

        // The two slots, each a row of every passive of that kind it has learned.
        void Slot(PassiveKind kind, string current, string title, Color color)
        {
            var learned = t.Passives.Select(ClassBook.Passive).Where(p => p.Kind == kind).ToList();
            var head = Ui.Text(title, 13, color, 4);
            head.TooltipText = kind == PassiveKind.Reaction ? "One reaction, from any class trained: it goes off when hit." : "One support passive, from any class trained.";
            head.MouseFilter = MouseFilterEnum.Stop;
            _box.AddChild(head);
            if (learned.Count == 0) { _box.AddChild(Ui.Text("none learned yet: train past a class's skills", 13, Dim, 3)); return; }
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("h_separation", 5);
            row.AddThemeConstantOverride("v_separation", 5);
            foreach (var p in learned)
            {
                var b = Btn(p.Name, () => Equip(p), current != p.Id);
                b.Icon = SkillIcons.Passive(p, 2);
                b.TooltipText = $"{p.Name} ({p.Class})\n{p.Text}";
                if (current == p.Id) b.Modulate = color;
                row.AddChild(b);
            }
            _box.AddChild(row);
        }
        _box.AddChild(new HSeparator());
        Slot(PassiveKind.Support, t.Passive, "SUPPORT", Green);
        Slot(PassiveKind.Reaction, t.Reaction, "REACTION", Pink);

        // What it takes into battle.
        _box.AddChild(new HSeparator());
        var kit = new HFlowContainer();
        kit.AddThemeConstantOverride("h_separation", 4);
        kit.AddChild(Ui.Text("IN BATTLE", 13, Dim, 4));
        foreach (var s in t.Usable()) kit.AddChild(Ui.Picture(SkillIcons.Skill(s, 2), $"{s.Name} ({s.Class})\n{s.Text}"));
        foreach (var p in new[] { ClassBook.Passive(t.Passive), ClassBook.ShinyPassive(c), ClassBook.Passive(t.Gear), ClassBook.Passive(t.Reaction) }.Where(p => p != null))
            kit.AddChild(Ui.Picture(SkillIcons.Passive(p, 2), $"{p.Name}{(p.Class == "Shiny" ? " (shiny)" : "")}\n{p.Text}"));
        foreach (var m in t.Masteries)
        {
            var (a, d, sp, h) = ClassBook.MasteryBonus(m);
            kit.AddChild(Ui.ChipView(a > 0 ? new Chip("atk", "+1", Gold, $"{m} mastered") : d > 0 ? new Chip("def", "+1", Gold, $"{m} mastered") : sp > 0 ? new Chip("spd", "+1", Gold, $"{m} mastered") : new Chip("hp", "+" + h, Gold, $"{m} mastered"), 15));
        }
        _box.AddChild(kit);
    }

    void SetMain(string cls)
    {
        var t = Ranch.Record(_id.Value).Training;
        t.Class = cls;
        if (t.Secondary == cls) t.Secondary = null;
        Done($"{Ranch.Get(_id.Value).Name} now trains as a {cls}.");
    }

    void SetSecondary(string cls)
    {
        Ranch.Record(_id.Value).Training.Secondary = cls;
        Done(cls == null ? "No secondary skill set." : $"{Ranch.Get(_id.Value).Name} can use its {cls} skills too.");
    }

    void Equip(PassiveDef p)
    {
        var t = Ranch.Record(_id.Value).Training;
        if (p.Kind == PassiveKind.Reaction) t.Reaction = p.Id; else t.Passive = p.Id;
        Done($"{Ranch.Get(_id.Value).Name} equips {p.Name}.");
    }

    void Done(string message)
    {
        Ranch.Save();
        Changed?.Invoke(message);
        Build();
    }
}
