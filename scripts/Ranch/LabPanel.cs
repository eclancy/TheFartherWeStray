using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// The lab at home: a graft chamber showing the selected beast, and the work it can do as tabs, least change first
/// (the tiers): Infuse (tune its stats), Blood (a strain for a natural creature), Splice (another species' ability),
/// Augment (fit one from the bag, bought at undercity shops) and Graft (a part slot swapped for one in your codex;
/// the chamber shows it before you pay). Each tab has a plain one-line header; what the work makes the beast, and
/// whether its line carries it, is in the tab's tooltip (docs/menus-plan.md, part B step 5).
/// </summary>
public partial class LabPanel : Control
{
    public Ranch Ranch;
    public Action<string> Done; // after an operation: the message to show
    public static string StartTab; // --labtab=ID, for screenshots: the tab to open on

    static float ChamberHeight => Phone.On ? 230 : 290;
    static Color Ink => Ui.Ink;
    static Color Dim => Ui.Dim;
    static Color Cyan => Ui.Cyan;

    /// <summary>The tabs, least change first as the tiers go; the jargon (what it makes the beast) lives in the tooltip.</summary>
    static readonly (string Id, string Label, string Tip)[] Tabs =
    {
        ("infuse", "Infuse", "Infusions tune its stats: an upside and a trade-off each, two at a time (a third washes out the oldest). Size draughts make it a size bigger or smaller, one either way of the size it was born.\nMakes it infused · not inherited"),
        ("blood", "Blood", "New blood gives a natural creature a strain of its own.\nMakes it infused · its line carries it"),
        ("splice", "Splice", "A splice adds the ability of another kind of creature you've met in battle, two at most (a third replaces the oldest).\nMakes it a chimera · its line carries it"),
        ("augment", "Augment", "Augments come from undercity shops and are fitted here, two to a beast: abilities no natural creature has.\nRigs it: chem upkeep after each battle, and it learns slower · not inherited"),
        ("graft", "Graft", "A graft swaps one of its parts for one in your codex (the parts you've met in battle); the chamber shows it before you pay.\nMakes it a chimera · not inherited"),
    };

    int? _id;
    readonly Dictionary<string, int> _candidate = new();
    Texture2D _preview;
    HBoxContainer _tabBar;
    Label _header;
    VBoxContainer _rows;
    string _tab = "infuse";
    string _keep, _keepNow; // the pad: the button pressed, to put the focus back on once the panel is rebuilt

    public override void _Ready()
    {
        if (Tabs.Any(t => t.Id == StartTab)) _tab = StartTab;
        StartTab = null;
        // Under the chamber: the tabs, the tab's one line, then its work in a list that scrolls.
        float y = ChamberHeight + 6;
        _tabBar = new HBoxContainer { Position = new Vector2(0, y), Size = new Vector2(Size.X, 34) };
        _tabBar.AddThemeConstantOverride("separation", 6);
        AddChild(_tabBar);
        _header = new Label { Position = new Vector2(0, y + 42), Size = new Vector2(Size.X, 24), ClipText = true, MouseFilter = MouseFilterEnum.Pass };
        _header.AddThemeFontSizeOverride("font_size", 16);
        AddChild(_header);
        y += 72;
        var scroll = new ScrollContainer
        {
            Position = new Vector2(0, y), Size = new Vector2(Size.X, Size.Y - y),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_rows);
        AddChild(scroll);
    }

    public void ShowCreature(int id)
    {
        _id = id;
        _candidate.Clear();
        var rec = Ranch.Record(id);
        foreach (var s in Lab.Slots) _candidate[s.Name] = Lab.Get(rec.Parts, s.Name);
        Build();
    }

    static Label Text(string text, int width, Color color)
    {
        var l = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), VerticalAlignment = VerticalAlignment.Center, ClipText = true };
        l.AddThemeFontSizeOverride("font_size", 15);
        l.AddThemeColorOverride("font_color", color);
        if (UiSkin.PixelText) Ui.Dress(l, 15);
        return l;
    }

    Button Btn(string text, bool enabled, Action onPress, int width, string key)
    {
        var b = new Button { Text = text, Disabled = !enabled, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(width, 34) };
        b.AddThemeFontSizeOverride("font_size", 14);
        b.Pressed += () => { _keep = key; onPress(); };
        // On a phone, the button just pressed keeps the pad's focus when the panel is rebuilt round it.
        if (Phone.On && _keepNow == key) { _keepNow = null; b.CallDeferred(Control.MethodName.GrabFocus); }
        return b;
    }

    static HFlowContainer Flow()
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 6);
        flow.AddThemeConstantOverride("v_separation", 6);
        return flow;
    }

    static Color TierColour(Classes.Tier t) => Ui.Tone(Classes.TierBook.Hex(t));

    void Header(string text, Color color)
    {
        _header.Text = text;
        _header.AddThemeColorOverride("font_color", color);
        _header.TooltipText = Tabs.First(t => t.Id == _tab).Tip;
    }

    void Build()
    {
        if (_rows == null || _id == null) return;
        (_keepNow, _keep) = (_keep, null);
        foreach (var child in _tabBar.GetChildren()) { _tabBar.RemoveChild(child); child.QueueFree(); }
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        var rec = Ranch.Record(_id.Value);

        // The tabs: the one showing is lit.
        foreach (var (tid, label, tip) in Tabs)
        {
            string t = tid;
            var b = Btn(label, true, () => { _tab = t; Build(); }, 0, "tab:" + t);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            b.TooltipText = tip;
            if (t == _tab)
                foreach (var state in new[] { "normal", "hover", "pressed" }) b.AddThemeStyleboxOverride(state, UiSkin.Lit(UiPart.Small, Cyan, state != "normal"));
            _tabBar.AddChild(b);
        }

        switch (_tab)
        {
            case "blood": Blood(rec); break;
            case "splice": Splice(rec); break;
            case "augment": Augment(rec); break;
            case "graft": Graft(rec); break;
            default: Infuse(rec); break;
        }

        // The chamber shows the beast, with the parts you're considering on the Graft tab.
        var preview = rec.ToCreature();
        if (_tab == "graft") foreach (var s in Lab.Slots) preview.Parts = Lab.Set(preview.Parts, s.Name, _candidate[s.Name]);
        _preview = ImageTexture.CreateFromImage(CreaturePainter.Paint(preview, false));
        QueueRedraw();
    }

    // Every cost is the flask glyph and a number; the buttons say what they fit.
    static Texture2D Flask => Glyphs.Get("flask", 1);

    /// <summary>Infusions: tune its stats, an upside and a trade-off each, two at a time.</summary>
    void Infuse(CreatureRecord rec)
    {
        string tuned = rec.Infusions.Count == 0 ? "none" : string.Join(", ", rec.Infusions.Select(i => Classes.InfusionBook.Get(i)?.Name));
        Header($"Tune its stats, two infusions at a time · {rec.Infusions.Count}/{Classes.InfusionBook.Slots}: {tuned}", TierColour(Classes.Tier.Infused));
        var draughts = Flow();
        int id = _id.Value;
        foreach (var p in Classes.InfusionBook.All)
        {
            string pid = p.Id;
            bool has = rec.Infusions.Contains(p.Id), full = rec.Infusions.Count >= Classes.InfusionBook.Slots;
            int brew = Farmyard.LabPrice(Ranch, Classes.InfusionBook.Cost);
            var b = Btn($"{p.Name}  {brew}", !has && Ranch.Chem >= brew, () => Finish(Lab.Brew(Ranch, id, pid)), 0, "brew:" + pid);
            b.Icon = Flask;
            b.TooltipText = p.Text + (has ? "\nIt has this one." : full ? $"\nThe {Classes.InfusionBook.Get(rec.Infusions[0])?.Name} washes out." : "");
            draughts.AddChild(b);
        }
        _rows.AddChild(draughts);

        // Size draughts: a size bigger or smaller, one either way of the size it was born; no slot.
        var sizes = Flow();
        var born = rec.BirthSize ?? rec.Size;
        var now = Text(SizeInfo.Word(rec.Size) + (born != rec.Size ? $" · born {SizeInfo.Word(born)}" : ""), 0, Ink);
        now.ClipText = false;
        now.MouseFilter = MouseFilterEnum.Pass;
        now.TooltipText = SizeInfo.Describe(rec.Size);
        sizes.AddChild(now);
        foreach (var d in Classes.InfusionBook.Sizes)
        {
            string did = d.Id;
            var (to, why) = Lab.Resized(rec, d);
            int brew = Farmyard.LabPrice(Ranch, d.Cost);
            var b = Btn($"{d.Name}  {brew}", to != null && Ranch.Chem >= brew, () => Finish(Lab.Resize(Ranch, id, did)), 0, "size:" + did);
            b.Icon = Flask;
            b.TooltipText = to is { } size ? $"{SizeInfo.Word(rec.Size)} to {SizeInfo.Word(size)}. {SizeInfo.Describe(size)}\nUses no infusion slot; its line breeds from the size it was born." : why;
            sizes.AddChild(b);
        }
        _rows.AddChild(sizes);
    }

    /// <summary>New blood: a strain of its own for a natural creature; its line carries it.</summary>
    void Blood(CreatureRecord rec)
    {
        if (rec.Species.Strain != Strain.Natural)
        {
            Header($"It's {rec.Species.Strain.Label().ToLowerInvariant()} already: only natural creatures take new blood", Dim);
            return;
        }
        Header("Give it a strain of its own", TierColour(Classes.Tier.Infused));
        var bloods = Flow();
        int id = _id.Value;
        foreach (var (strain, _, cost) in Lab.Infusions)
        {
            var s = strain;
            int price = Farmyard.LabPrice(Ranch, cost);
            var b = Btn($"{strain.Label()}  {price}", Ranch.Chem >= price, () => Finish(Lab.Infuse(Ranch, id, s)), 0, "blood:" + strain);
            b.Icon = Flask;
            bloods.AddChild(b);
        }
        _rows.AddChild(bloods);
    }

    /// <summary>Splices: another species' ability, from a kind of creature you've met; its line carries it.</summary>
    void Splice(CreatureRecord rec)
    {
        var own = Species.FormOf(rec.Species);
        var carried = rec.Species.Spliced ?? Array.Empty<Form>();
        string abilities = carried.Length == 0 ? "none" : string.Join(", ", carried.Select(Classes.TierBook.Ability));
        Header($"Splice in another kind's ability · {carried.Length}/{Classes.TierBook.MaxSpliced}: {abilities}", TierColour(Classes.Tier.Chimera));
        var kinds = Ranch.Forms.Where(f => f != own && !carried.Contains(f)).OrderBy(f => f.ToString()).ToList();
        if (kinds.Count == 0)
        {
            _rows.AddChild(Text("Meet other kinds of creature in battle to splice in their abilities.", 700, Dim));
            return;
        }
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var pick = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(Phone.On ? 300 : 340, 34) };
        pick.AddThemeFontSizeOverride("font_size", 14);
        foreach (var f in kinds) pick.AddItem($"{Classes.TierBook.Ability(f)} ({f.ToString().ToLowerInvariant()})", (int)f);
        int id = _id.Value, cost = Farmyard.LabPrice(Ranch, Classes.TierBook.SpliceCost);
        var splice = Btn($"Splice  {cost}", Ranch.Chem >= cost, () => Finish(Lab.Splice(Ranch, id, (Form)pick.GetItemId(pick.Selected))), 130, "splice");
        splice.Icon = Flask;
        if (carried.Length >= Classes.TierBook.MaxSpliced) splice.TooltipText = $"It loses {Classes.TierBook.Ability(carried[0])}.";
        row.AddChild(pick);
        row.AddChild(splice);
        _rows.AddChild(row);
        // What the picked ability does, in full under the row.
        var what = new Label { Text = Classes.TierBook.AbilityText(kinds[0]), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Size.X - 16, 0) };
        what.AddThemeFontSizeOverride("font_size", 15);
        what.AddThemeColorOverride("font_color", Ink);
        pick.ItemSelected += i => what.Text = Classes.TierBook.AbilityText((Form)pick.GetItemId((int)i));
        _rows.AddChild(what);
    }

    /// <summary>Augments: bought at undercity shops, fitted here, two to a beast, never inherited.</summary>
    void Augment(CreatureRecord rec)
    {
        string fitted = rec.Augments.Count == 0 ? "none fitted" : string.Join(", ", rec.Augments.Select(a => Classes.AugmentBook.Get(a)?.Name));
        Header($"Fit an augment from the bag · {rec.Augments.Count}/{Classes.AugmentBook.Slots}: {fitted}", TierColour(Classes.Tier.Rigged));
        var inBag = Ranch.Items.Where(kv => Classes.AugmentBook.IsAugment(kv.Key) && kv.Value > 0).ToList();
        if (inBag.Count == 0)
        {
            _rows.AddChild(Text("None in the bag: the Foundry, Lantern Row and the Glowmire sell them.", 700, Dim));
            return;
        }
        var augments = Flow();
        int id = _id.Value;
        bool full = rec.Augments.Count >= Classes.AugmentBook.Slots;
        foreach (var (augId, count) in inBag)
        {
            var aug = Classes.AugmentBook.Get(augId);
            string a = augId;
            var b = Btn($"Fit {aug.Name}{(count > 1 ? $" ({count})" : "")}", !rec.Augments.Contains(augId), () => Finish(Ranch.Install(id, a)), 0, "aug:" + augId);
            b.Icon = SkillIcons.Skill(Classes.ClassBook.Skill(aug.Skill), 1);
            b.TooltipText = Classes.AugmentBook.Text(aug) + (full ? $"\nReplaces the {Classes.AugmentBook.Get(rec.Augments[0])?.Name} (scrapped)." : "");
            augments.AddChild(b);
        }
        _rows.AddChild(augments);
    }

    /// <summary>Part grafts: a slot swapped for a part in your codex; the chamber shows the parts being considered.</summary>
    void Graft(CreatureRecord rec)
    {
        Header($"Swap a part for one in your codex · {Ranch.Codex.Count} parts known", TierColour(Classes.Tier.Chimera));
        int id = _id.Value;
        foreach (var slot in Lab.SlotsFor(rec.Species.Plan))
        {
            int current = Lab.Get(rec.Parts, slot.Name), pick = _candidate[slot.Name];
            var options = Lab.Options(Ranch, slot.Name);
            if (!options.Contains(current)) options.Add(current);
            options.Sort();
            string name = slot.Name;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(Text(slot.Name, 70, Dim));
            row.AddChild(Btn("<", options.Count > 1, () => Cycle(name, options, -1), 34, "<:" + name));
            row.AddChild(Text(pick == current ? Lab.Words(slot.Name, current) : $"{Lab.Words(slot.Name, current)} -> {Lab.Words(slot.Name, pick)}", 300, pick == current ? Ink : Ui.Gold));
            row.AddChild(Btn(">", options.Count > 1, () => Cycle(name, options, 1), 34, ">:" + name));
            int price = Farmyard.LabPrice(Ranch, slot.Cost);
            var graft = Btn($"Graft  {price}", pick != current && Ranch.Chem >= price, () => Finish(Lab.GraftPart(Ranch, id, name, _candidate[name])), 120, "graft:" + name);
            graft.Icon = Flask;
            row.AddChild(graft);
            _rows.AddChild(row);
        }
    }

    void Cycle(string slot, List<int> options, int step)
    {
        int i = options.IndexOf(_candidate[slot]);
        _candidate[slot] = options[(i + step + options.Count) % options.Count];
        Build();
    }

    void Finish(string message)
    {
        Ranch.Save();
        Done?.Invoke(message);
        if (_id is { } id) ShowCreature(id);
    }

    public override void _Draw()
    {
        if (_id == null || _preview == null) return;
        var rec = Ranch.Record(_id.Value);
        var font = GetThemeDefaultFont();
        var glow = Ui.Good;
        var brass = Color.FromHtml("#b2873a");
        float h = ChamberHeight;
        // Its name, and its tier in its colour: what the work here would change.
        var tier = Classes.TierBook.Of(Ranch.Get(_id.Value));
        string title = rec.Name, tierText = $"  ·  {tier}";
        float titleW = font.GetStringSize(title, HorizontalAlignment.Left, -1, 17).X, tierW = font.GetStringSize(tierText, HorizontalAlignment.Left, -1, 17).X;
        float x0 = (Size.X - titleW - tierW) / 2;
        Ui.Draw(this, new Vector2(x0, 18), title, HorizontalAlignment.Left, -1, 17, glow);
        Ui.Draw(this, new Vector2(x0 + titleW, 18), tierText, HorizontalAlignment.Left, -1, 17, Ui.Tone(Classes.TierBook.Hex(tier)));

        // A wide tank of chem with brass bands, the beast suspended inside.
        var glass = new Rect2(40, 34, Size.X - 80, h - 54);
        DrawRect(glass, new Color(0.06f, 0.1f, 0.1f, 0.9f));
        var chem = new Rect2(glass.Position.X, glass.Position.Y + glass.Size.Y * 0.35f, glass.Size.X, glass.Size.Y * 0.65f);
        DrawRect(chem, new Color(glow, 0.16f));
        DrawRect(new Rect2(chem.Position, new Vector2(chem.Size.X, 2)), new Color(glow, 0.6f));
        for (int i = 0; i < 10; i++)
        {
            float t = (float)((Time.GetTicksMsec() / 1000.0 * (0.25 + i * 0.05) + i * 0.29) % 1.0);
            DrawCircle(new Vector2(glass.Position.X + 30 + i * (glass.Size.X - 60) / 9, chem.End.Y - t * chem.Size.Y), 2 + i % 3, new Color(glow, 0.45f * (1 - t)));
        }
        DrawRect(glass, new Color(0.7f, 1f, 0.85f, 0.35f), false, 2);
        DrawRect(new Rect2(28, 24, Size.X - 56, 14), brass);
        DrawRect(new Rect2(28, glass.End.Y - 4, Size.X - 56, 14), brass);

        float z = Mathf.Max(2, CreatureSculptor.FitZoom(_preview.GetSize(), glass.Size - new Vector2(20, 16), 3));
        var ts = _preview.GetSize() * z;
        DrawTextureRect(_preview, new Rect2(new Vector2(Mathf.Round(Size.X / 2 - ts.X / 2), glass.End.Y - 10 - ts.Y), ts), false);
    }

    public override void _Process(double delta) { if (Visible) QueueRedraw(); }
}
