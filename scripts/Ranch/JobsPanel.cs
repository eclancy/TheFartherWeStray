using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
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

    Label Text(string text, int size, Color color, bool wrap = false)
    {
        var l = new Label { Text = text, CustomMinimumSize = new Vector2(Size.X - 20, 0) };
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    Button Btn(string text, Action onPress, bool enabled = true, int width = 0)
    {
        var b = new Button { Text = text, Disabled = !enabled, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(width, 32), ClipText = width > 0 };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.Pressed += onPress;
        return b;
    }

    static string StarText(int stars) => stars == 0 ? "" : " " + new string('*', stars);

    void Build()
    {
        if (_box == null || _id == null) return;
        foreach (var child in _box.GetChildren()) child.QueueFree();
        var c = Ranch.Get(_id.Value);
        var t = Ranch.Record(_id.Value).Training;
        var aff = ClassBook.Affinity[c.Species.Plan];

        _box.AddChild(Text($"JOB TREE - {c.Species.Name}, a {c.Species.Plan.ToString().ToLowerInvariant()}", 17, Gold));
        _box.AddChild(Text($"Favoured (green): {string.Join(", ", aff.favoured)}    "
            + (aff.barred.Length > 0 ? $"Barred: {string.Join(", ", aff.barred)}    " : "")
            + "Stars: * skills, ** passives, *** mastered", 13, Ink, wrap: true));

        void Section(string title, IEnumerable<ClassDef> classes)
        {
            _box.AddChild(Text(title, 13, Dim));
            var grid = new GridContainer { Columns = 4 };
            grid.AddThemeConstantOverride("h_separation", 5);
            grid.AddThemeConstantOverride("v_separation", 5);
            foreach (var k in classes)
            {
                var standing = ClassBook.StandingOf(k, c, t);
                string mark = k.Id == t.Class ? "> " : k.Id == t.Secondary ? "+ " : "";
                string state = standing == ClassBook.Standing.Never ? " x" : standing == ClassBook.Standing.Locked ? " ?" : "";
                var id = k.Id;
                var b = Btn($"{mark}{k.Name}{StarText(t.Stars(k.Id))}{state}", () => { _shown = id; Build(); }, true, 168);
                b.Modulate = k.Id == t.Class ? Gold : standing switch
                {
                    ClassBook.Standing.Never => Red,
                    ClassBook.Standing.Locked => new Color(0.62f, 0.6f, 0.72f),
                    _ => ClassBook.Favoured(c, k.Id) ? Green : Colors.White,
                };
                if (k.Id == _shown) b.AddThemeColorOverride("font_color", Cyan);
                grid.AddChild(b);
            }
            _box.AddChild(grid);
        }
        Section("BASE CLASSES", ClassBook.Classes.Where(k => !k.Advanced));
        Section("PART AND TEMPERAMENT CLASSES - two base classes mastered, and the body or nature for it", ClassBook.Classes.Where(k => k.Advanced && k.Requires.Length == 2));
        Section("BLOOD CLASSES - one base class mastered, and the strain", ClassBook.Classes.Where(k => k.Advanced && k.Requires.Length == 1));

        // The class being looked at.
        var shown = ClassBook.Class(_shown ?? t.Class);
        var st = ClassBook.StandingOf(shown, c, t);
        _box.AddChild(Text($"{shown.Name}{StarText(t.Stars(shown.Id))}: {shown.Blurb}   JP {t.JpIn(shown.Id)}   Mastery: {ClassBook.MasteryText(shown.Id)} for good", 15, Gold, wrap: true));
        int spent = 0;
        foreach (var sid in shown.Skills)
        {
            var s = ClassBook.Skill(sid);
            spent += s.Cost;
            bool known = t.Skills.Contains(sid);
            _box.AddChild(Text((known ? "   [learned] " : $"   [{Math.Max(0, spent - t.JpIn(shown.Id))} JP] ") + $"{s.Name}: {s.Text}", 13, known ? Ink : Dim, wrap: true));
        }
        foreach (var p in ClassBook.PassivesOf(shown.Id))
        {
            spent += p.Cost;
            bool has = t.Passives.Contains(p.Id);
            string kind = p.Kind == PassiveKind.Reaction ? "Reaction" : "Support";
            _box.AddChild(Text((has ? "   [learned] " : $"   [{Math.Max(0, spent - t.JpIn(shown.Id))} JP] ") + $"{kind} - {p.Name}: {p.Text}", 13,
                has ? (p.Kind == PassiveKind.Reaction ? Pink : Green) : Dim, wrap: true));
        }
        if (!t.Masteries.Contains(shown.Id) && t.Stars(shown.Id) >= 2)
            _box.AddChild(Text($"   [{Math.Max(0, spent + ClassBook.MasteryExtra - t.JpIn(shown.Id))} JP] Mastery", 13, Dim));
        if (st != ClassBook.Standing.Open) _box.AddChild(Text("Needs: " + ClassBook.Hint(shown, c, t), 13, st == ClassBook.Standing.Never ? Red : Gold, wrap: true));

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        actions.AddChild(Btn($"Train as {shown.Name}", () => SetMain(shown.Id), st == ClassBook.Standing.Open && t.Class != shown.Id));
        actions.AddChild(Btn($"Secondary: {shown.Name}", () => SetSecondary(shown.Id), t.Class != shown.Id && t.LearnedIn(shown.Id) > 0 && t.Secondary != shown.Id));
        if (t.Secondary != null) actions.AddChild(Btn("No secondary", () => SetSecondary(null)));
        _box.AddChild(actions);

        // The two slots.
        void Slot(PassiveKind kind, string current, string title, Color color)
        {
            var learned = t.Passives.Select(ClassBook.Passive).Where(p => p.Kind == kind).ToList();
            _box.AddChild(Text($"{title}: {ClassBook.Passive(current)?.Name ?? "none"}" + (learned.Count == 0 ? "   (learn one by training past a class's skills)" : ""), 13, color, wrap: true));
            if (learned.Count == 0) return;
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("h_separation", 5);
            row.AddThemeConstantOverride("v_separation", 5);
            foreach (var p in learned)
            {
                var b = Btn($"{p.Name} ({p.Class})", () => Equip(p), current != p.Id);
                b.TooltipText = p.Text;
                row.AddChild(b);
            }
            _box.AddChild(row);
        }
        Slot(PassiveKind.Support, t.Passive, "SUPPORT SLOT", Green);
        Slot(PassiveKind.Reaction, t.Reaction, "REACTION SLOT", Pink);

        var usable = t.Usable().Select(s => s.Name).ToList();
        string masteries = t.Masteries.Count == 0 ? "" : $"   Mastered: {string.Join(", ", t.Masteries.Select(m => $"{m} ({ClassBook.MasteryText(m)})"))}";
        _box.AddChild(Text($"In battle: {(usable.Count == 0 ? "basic attacks only so far" : string.Join(", ", usable))}.{masteries}", 13, Ink, wrap: true));
    }

    void SetMain(string cls)
    {
        var t = Ranch.Record(_id.Value).Training;
        t.Class = cls;
        if (t.Secondary == cls) t.Secondary = null;
        Done($"{Ranch.Get(_id.Value).Species.Name} now trains as a {cls}.");
    }

    void SetSecondary(string cls)
    {
        Ranch.Record(_id.Value).Training.Secondary = cls;
        Done(cls == null ? "No secondary skill set." : $"{Ranch.Get(_id.Value).Species.Name} can use its {cls} skills too.");
    }

    void Equip(PassiveDef p)
    {
        var t = Ranch.Record(_id.Value).Training;
        if (p.Kind == PassiveKind.Reaction) t.Reaction = p.Id; else t.Passive = p.Id;
        Done($"{Ranch.Get(_id.Value).Species.Name} equips {p.Name}.");
    }

    void Done(string message)
    {
        Ranch.Save();
        Changed?.Invoke(message);
        Build();
    }
}
