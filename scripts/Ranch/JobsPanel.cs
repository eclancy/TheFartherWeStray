using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>
/// The selected creature's job tree (FFTA's job menu, per creature): every class, marked open,
/// locked (with what it still needs) or never possible for this body; favoured classes starred.
/// Click a class to see it, then train it as the main class or take its skills as the secondary.
/// Learned passives are equipped below: one slot, from any class trained.
/// </summary>
public partial class JobsPanel : Control
{
    public Ranch Ranch;
    public Action<string> Changed;

    int? _id;
    string _shown;
    VBoxContainer _box;

    static readonly Color Ink = Color.FromHtml("#e8e2f4"), Dim = Color.FromHtml("#6f6788"), Gold = Color.FromHtml("#f2c14e"),
        Cyan = Color.FromHtml("#4fe3ff"), Red = Color.FromHtml("#b0546a"), Green = Color.FromHtml("#4ff0a0");

    public override void _Ready()
    {
        var scroll = new ScrollContainer { Size = Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _box = new VBoxContainer { CustomMinimumSize = new Vector2(Size.X - 16, 0) };
        _box.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_box);
    }

    public void ShowCreature(int id)
    {
        _id = id;
        _shown ??= Ranch.Record(id).Training.Class;
        Build();
    }

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
        var b = new Button { Text = text, Disabled = !enabled, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(width, 34), ClipText = width > 0 };
        b.AddThemeFontSizeOverride("font_size", 14);
        b.Pressed += onPress;
        return b;
    }

    void Build()
    {
        if (_box == null || _id == null) return;
        foreach (var child in _box.GetChildren()) child.QueueFree();
        var c = Ranch.Get(_id.Value);
        var t = Ranch.Record(_id.Value).Training;
        var aff = ClassBook.Affinity[c.Species.Plan];

        _box.AddChild(Text($"JOB TREE - {c.Species.Name}, a {c.Species.Plan.ToString().ToLowerInvariant()}", 17, Gold));
        _box.AddChild(Text($"Favoured (*): {string.Join(", ", aff.favoured)}  (+50% JP, stronger leanings)"
            + (aff.barred.Length > 0 ? $"    Barred: {string.Join(", ", aff.barred)}" : ""), 14, Ink, wrap: true));

        void Section(string title, IEnumerable<ClassDef> classes)
        {
            _box.AddChild(Text(title, 14, Dim));
            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            foreach (var k in classes)
            {
                var standing = ClassBook.StandingOf(k, c, t);
                string mark = k.Id == t.Class ? "> " : k.Id == t.Secondary ? "+ " : "";
                string fav = ClassBook.Favoured(c, k.Id) ? " *" : "";
                string state = standing == ClassBook.Standing.Never ? "  (never)" : standing == ClassBook.Standing.Locked ? "  (locked)" : "";
                int learned = t.LearnedIn(k.Id);
                string progress = learned > 0 ? $"  {learned}/3" : "";
                var id = k.Id;
                var b = Btn($"{mark}{k.Name}{fav}{progress}{state}", () => { _shown = id; Build(); }, true, 222);
                b.Modulate = k.Id == t.Class ? Gold : standing switch { ClassBook.Standing.Never => Red, ClassBook.Standing.Locked => new Color(0.65f, 0.62f, 0.75f), _ => Colors.White };
                if (k.Id == _shown) b.AddThemeColorOverride("font_color", Cyan);
                grid.AddChild(b);
            }
            _box.AddChild(grid);
        }
        Section("BASE CLASSES", ClassBook.Classes.Where(k => !k.Advanced));
        Section("PART CLASSES - master two base classes, and have the body for it", ClassBook.Classes.Where(k => k.Advanced && k.Requires.Length == 2));
        Section("BLOOD CLASSES - master one base class, and carry the strain", ClassBook.Classes.Where(k => k.Advanced && k.Requires.Length == 1));

        // The class being looked at.
        var shown = ClassBook.Class(_shown ?? t.Class);
        var st = ClassBook.StandingOf(shown, c, t);
        _box.AddChild(Text($"{shown.Name}: {shown.Blurb}   JP {t.JpIn(shown.Id)}", 16, Gold, wrap: true));
        int spent = 0;
        foreach (var sid in shown.Skills)
        {
            var s = ClassBook.Skill(sid);
            spent += s.Cost;
            bool known = t.Skills.Contains(sid);
            _box.AddChild(Text((known ? "   [learned] " : $"   [{Math.Max(0, spent - t.JpIn(shown.Id))} JP] ") + $"{s.Name}: {s.Text}", 14, known ? Ink : Dim, wrap: true));
        }
        var passive = ClassBook.PassiveOf(shown.Id);
        bool hasPassive = t.Passives.Contains(passive.Id);
        _box.AddChild(Text((hasPassive ? "   [learned] " : $"   [{Math.Max(0, spent + passive.Cost - t.JpIn(shown.Id))} JP] ") + $"Passive - {passive.Name}: {passive.Text}", 14, hasPassive ? Green : Dim, wrap: true));
        if (st != ClassBook.Standing.Open) _box.AddChild(Text("Needs: " + ClassBook.Hint(shown, c, t), 14, st == ClassBook.Standing.Never ? Red : Gold, wrap: true));

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        actions.AddChild(Btn($"Train as {shown.Name}", () => SetMain(shown.Id), st == ClassBook.Standing.Open && t.Class != shown.Id));
        actions.AddChild(Btn($"Secondary: {shown.Name}", () => SetSecondary(shown.Id),
            t.Class != shown.Id && t.LearnedIn(shown.Id) > 0 && t.Secondary != shown.Id));
        if (t.Secondary != null) actions.AddChild(Btn("No secondary", () => SetSecondary(null)));
        _box.AddChild(actions);

        // The passive slot: any passive learned in any class.
        _box.AddChild(Text($"PASSIVE SLOT - equipped: {ClassBook.Passive(t.Passive)?.Name ?? "none"}"
            + (t.Passives.Count == 0 ? "   (master a class's three skills, then earn its passive)" : ""), 14, Dim, wrap: true));
        if (t.Passives.Count > 0)
        {
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("h_separation", 6);
            foreach (var pid in t.Passives)
            {
                var p = ClassBook.Passive(pid);
                var b = Btn($"{p.Name} ({p.Class})", () => Equip(pid), t.Passive != pid);
                b.TooltipText = p.Text;
                row.AddChild(b);
            }
            _box.AddChild(row);
        }
        var usable = t.Usable().Select(s => s.Name).ToList();
        _box.AddChild(Text($"In battle: {(usable.Count == 0 ? "basic attacks only so far" : string.Join(", ", usable))}", 14, Ink, wrap: true));
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

    void Equip(string pid)
    {
        Ranch.Record(_id.Value).Training.Passive = pid;
        Done($"{Ranch.Get(_id.Value).Species.Name} equips {ClassBook.Passive(pid).Name}.");
    }

    void Done(string message)
    {
        Ranch.Save();
        Changed?.Invoke(message);
        Build();
    }

    /// <summary>Forget the class being looked at (a different creature was selected).</summary>
    public void Reset() => _shown = null;
}
