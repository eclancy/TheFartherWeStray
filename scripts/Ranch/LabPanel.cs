using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// The lab at the ranch: a graft chamber showing the selected creature as it would look with the
/// parts you're considering, and the work it can do, least change first (the tiers): infusions that
/// tune its stats, new blood for natural creatures, another species' ability spliced in, augments
/// from the bag (bought at undercity shops), and a row per part slot (cycle through your codex, then graft).
/// </summary>
public partial class LabPanel : Control
{
    public Ranch Ranch;
    public Action<string> Done; // after an operation: the message to show

    const float ChamberHeight = 290;
    int? _id;
    readonly Dictionary<string, int> _candidate = new();
    Texture2D _preview;
    VBoxContainer _rows;

    public override void _Ready()
    {
        // Everything the lab does, in a list that scrolls under the chamber.
        var scroll = new ScrollContainer
        {
            Position = new Vector2(0, ChamberHeight + 10), Size = new Vector2(Size.X, Size.Y - ChamberHeight - 10),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 4);
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

    void Build()
    {
        if (_rows == null || _id == null) return;
        foreach (var child in _rows.GetChildren()) child.QueueFree();
        var rec = Ranch.Record(_id.Value);
        var plan = rec.Species.Plan;

        Label Text(string text, int width, Color color)
        {
            var l = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), VerticalAlignment = VerticalAlignment.Center, ClipText = true };
            l.AddThemeFontSizeOverride("font_size", 15);
            l.AddThemeColorOverride("font_color", color);
            return l;
        }
        Button Btn(string text, bool enabled, Action onPress, int width = 0)
        {
            var b = new Button { Text = text, Disabled = !enabled, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(width, 34) };
            b.AddThemeFontSizeOverride("font_size", 14);
            b.Pressed += onPress;
            return b;
        }
        var ink = Color.FromHtml("#e8e2f4");
        var dim = Color.FromHtml("#9a91b4");
        var gold = Color.FromHtml("#f2c14e");

        // Every cost is the flask glyph and a number; the buttons say what they fit. The sections run
        // from the least change to the most, as the tiers do: infusions, new blood, splices, augments,
        // then the part grafts.
        var flask = Glyphs.Get("flask", 1);
        Color TierColour(Classes.Tier t) => Color.FromHtml(Classes.TierBook.Hex(t));

        // Infusions: tune its stats, an upside and a trade-off each, two at a time.
        string tuned = rec.Infusions.Count == 0 ? "none" : string.Join(", ", rec.Infusions.Select(i => Classes.InfusionBook.Get(i)?.Name));
        _rows.AddChild(Text($"INFUSIONS · tune its stats · {rec.Infusions.Count}/{Classes.InfusionBook.Slots}: {tuned} · makes it infused · not inherited", 700, TierColour(Classes.Tier.Infused)));
        var draughts = new HFlowContainer();
        draughts.AddThemeConstantOverride("h_separation", 6);
        foreach (var p in Classes.InfusionBook.All)
        {
            int id = _id.Value;
            string pid = p.Id;
            bool has = rec.Infusions.Contains(p.Id), full = rec.Infusions.Count >= Classes.InfusionBook.Slots;
            int brew = Farmyard.LabPrice(Ranch, Classes.InfusionBook.Cost);
            var b = Btn($"{p.Name}  {brew}", !has && Ranch.Chem >= brew, () => Finish(Lab.Brew(Ranch, id, pid)));
            b.Icon = flask;
            b.TooltipText = p.Text + (has ? "\nIt has this one." : full ? $"\nThe {Classes.InfusionBook.Get(rec.Infusions[0])?.Name} washes out." : "");
            draughts.AddChild(b);
        }
        _rows.AddChild(draughts);

        // New blood: a strain of its own for a natural creature; its line carries it.
        if (rec.Species.Strain == Strain.Natural)
        {
            _rows.AddChild(Text("NEW BLOOD · a strain of its own · makes it infused · its line carries it", 700, gold));
            var bloods = new HBoxContainer();
            bloods.AddThemeConstantOverride("separation", 6);
            foreach (var (strain, _, cost) in Lab.Infusions)
            {
                int id = _id.Value;
                int price = Farmyard.LabPrice(Ranch, cost);
                var b = Btn($"{strain.Label()}  {price}", Ranch.Chem >= price, () => Finish(Lab.Infuse(Ranch, id, strain)));
                b.Icon = flask;
                bloods.AddChild(b);
            }
            _rows.AddChild(bloods);
        }
        else _rows.AddChild(Text($"NEW BLOOD · it's {rec.Species.Strain.Label().ToLowerInvariant()} already: only natural creatures take new blood", 700, dim));

        // Splices: another species' ability, from a kind of creature you've met; its line carries it.
        var own = Species.FormOf(rec.Species);
        var carried = rec.Species.Spliced ?? Array.Empty<Form>();
        string abilities = carried.Length == 0 ? "none" : string.Join(", ", carried.Select(Classes.TierBook.Ability));
        _rows.AddChild(Text($"SPLICES · another species' ability · {carried.Length}/{Classes.TierBook.MaxSpliced}: {abilities} · makes a chimera · its line carries it", 700, TierColour(Classes.Tier.Chimera)));
        var kinds = Ranch.Forms.Where(f => f != own && !carried.Contains(f)).OrderBy(f => f.ToString()).ToList();
        var spliceRow = new HBoxContainer();
        spliceRow.AddThemeConstantOverride("separation", 6);
        if (kinds.Count == 0) spliceRow.AddChild(Text("Meet other kinds of creature in battle to splice in their abilities.", 700, dim));
        else
        {
            var pick = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(260, 34) };
            pick.AddThemeFontSizeOverride("font_size", 14);
            foreach (var f in kinds) pick.AddItem($"{Classes.TierBook.Ability(f)} ({f.ToString().ToLowerInvariant()})", (int)f);
            var what = Text(Classes.TierBook.AbilityText(kinds[0]), 300, dim);
            pick.ItemSelected += i => what.Text = Classes.TierBook.AbilityText((Form)pick.GetItemId((int)i));
            int id = _id.Value;
            var splice = Btn($"Splice  {Farmyard.LabPrice(Ranch, Classes.TierBook.SpliceCost)}", Ranch.Chem >= Farmyard.LabPrice(Ranch, Classes.TierBook.SpliceCost),
                () => Finish(Lab.Splice(Ranch, id, (Form)pick.GetItemId(pick.Selected))), 120);
            splice.Icon = flask;
            if (carried.Length >= Classes.TierBook.MaxSpliced) splice.TooltipText = $"It loses {Classes.TierBook.Ability(carried[0])}.";
            spliceRow.AddChild(pick);
            spliceRow.AddChild(splice);
            spliceRow.AddChild(what);
        }
        _rows.AddChild(spliceRow);

        // Augments: bought at undercity shops, bolted on here, two to a creature, never inherited.
        string fitted = rec.Augments.Count == 0 ? "none fitted" : string.Join(", ", rec.Augments.Select(a => Classes.AugmentBook.Get(a)?.Name));
        _rows.AddChild(Text($"AUGMENTS · {rec.Augments.Count}/{Classes.AugmentBook.Slots}: {fitted} · abilities no natural creature has: rigs it · not inherited", 700, TierColour(Classes.Tier.Rigged)));
        var augments = new HFlowContainer();
        augments.AddThemeConstantOverride("h_separation", 6);
        var inBag = Ranch.Items.Where(kv => Classes.AugmentBook.IsAugment(kv.Key) && kv.Value > 0).ToList();
        foreach (var (augId, count) in inBag)
        {
            var aug = Classes.AugmentBook.Get(augId);
            int id = _id.Value;
            bool full = rec.Augments.Count >= Classes.AugmentBook.Slots;
            var b = Btn($"Fit {aug.Name}{(count > 1 ? $" ({count})" : "")}", !rec.Augments.Contains(augId), () => Finish(Ranch.Install(id, augId)));
            b.Icon = SkillIcons.Skill(Classes.ClassBook.Skill(aug.Skill), 1);
            b.TooltipText = Classes.AugmentBook.Text(aug) + (full ? $"\nReplaces the {Classes.AugmentBook.Get(rec.Augments[0])?.Name} (scrapped)." : "");
            augments.AddChild(b);
        }
        if (inBag.Count == 0) augments.AddChild(Text("None in the bag: the Foundry, Lantern Row and the Glowmire sell them.", 700, dim));
        _rows.AddChild(augments);

        // Part grafts: a slot swapped for a part in your codex.
        _rows.AddChild(Text($"PART GRAFTS · {Ranch.Codex.Count} parts in your codex · a grafted part makes a chimera · not inherited", 700, gold));
        foreach (var slot in Lab.SlotsFor(plan))
        {
            int current = Lab.Get(rec.Parts, slot.Name), pick = _candidate[slot.Name];
            var options = Lab.Options(Ranch, slot.Name);
            if (!options.Contains(current)) options.Add(current);
            options.Sort();
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(Text(slot.Name, 70, dim));
            row.AddChild(Btn("<", options.Count > 1, () => Cycle(slot.Name, options, -1), 34));
            row.AddChild(Text(pick == current ? Lab.Words(slot.Name, current) : $"{Lab.Words(slot.Name, current)} -> {Lab.Words(slot.Name, pick)}", 300, pick == current ? ink : gold));
            row.AddChild(Btn(">", options.Count > 1, () => Cycle(slot.Name, options, 1), 34));
            int id = _id.Value;
            string name = slot.Name;
            var graftButton = Btn($"Graft  {Farmyard.LabPrice(Ranch, slot.Cost)}", pick != current && Ranch.Chem >= Farmyard.LabPrice(Ranch, slot.Cost), () => Finish(Lab.GraftPart(Ranch, id, name, _candidate[name])), 120);
            graftButton.Icon = flask;
            row.AddChild(graftButton);
            _rows.AddChild(row);
        }

        // The chamber shows the creature with every part you're considering.
        var preview = rec.ToCreature();
        foreach (var s in Lab.Slots) preview.Parts = Lab.Set(preview.Parts, s.Name, _candidate[s.Name]);
        _preview = ImageTexture.CreateFromImage(CreaturePainter.Paint(preview, false));
        QueueRedraw();
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
        var glow = Color.FromHtml("#4ff0a0");
        var brass = Color.FromHtml("#b2873a");
        // Its tier, in its colour: what the work here would change.
        var tier = Classes.TierBook.Of(Ranch.Get(_id.Value));
        string title = $"GRAFT CHAMBER - {rec.Name}", tierText = $"  ·  {tier}";
        float titleW = font.GetStringSize(title, HorizontalAlignment.Left, -1, 17).X, tierW = font.GetStringSize(tierText, HorizontalAlignment.Left, -1, 17).X;
        float x0 = (Size.X - titleW - tierW) / 2;
        DrawString(font, new Vector2(x0, 18), title, HorizontalAlignment.Left, -1, 17, glow);
        DrawString(font, new Vector2(x0 + titleW, 18), tierText, HorizontalAlignment.Left, -1, 17, Color.FromHtml(Classes.TierBook.Hex(tier)));

        // A wide tank of chem with brass bands, the creature suspended inside.
        var glass = new Rect2(40, 34, Size.X - 80, ChamberHeight - 54);
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

        int z = _preview.GetWidth() * 3 <= glass.Size.X - 20 && _preview.GetHeight() * 3 <= glass.Size.Y - 16 ? 3 : 2;
        var ts = _preview.GetSize() * z;
        DrawTextureRect(_preview, new Rect2(new Vector2(Mathf.Round(Size.X / 2 - ts.X / 2), glass.End.Y - 10 - ts.Y), ts), false);
    }

    public override void _Process(double delta) { if (Visible) QueueRedraw(); }
}
