using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using CreatureTactics.UI;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>
/// The lab at the ranch: a graft chamber showing the selected creature as it would look with the
/// parts you're considering, a row per part slot (cycle through your codex, then graft), machine
/// grafts, and blood infusions for natural creatures.
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
        _rows = new VBoxContainer { Position = new Vector2(0, ChamberHeight + 10), Size = new Vector2(Size.X, Size.Y - ChamberHeight - 10) };
        _rows.AddThemeConstantOverride("separation", 4);
        AddChild(_rows);
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

        // Every cost is the flask glyph and a number; the buttons say what they fit.
        var flask = Glyphs.Get("flask", 1);
        _rows.AddChild(Text($"FLESH GRAFTS · {Ranch.Codex.Count} parts in your codex · not inherited", 700, gold));
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
            var graftButton = Btn($"Graft  {slot.Cost}", pick != current && Ranch.Chem >= slot.Cost, () => Finish(Lab.GraftPart(Ranch, id, name, _candidate[name])), 120);
            graftButton.Icon = flask;
            row.AddChild(graftButton);
            _rows.AddChild(row);
        }

        _rows.AddChild(Text("MACHINE GRAFTS · Piltover brass · not inherited", 700, gold));
        var machines = new HBoxContainer();
        machines.AddThemeConstantOverride("separation", 6);
        foreach (var (graft, name, cost) in Lab.Machines)
        {
            bool has = rec.Species.Grafts.HasFlag(graft);
            int id = _id.Value;
            var b = Btn(has ? name : $"{name}  {cost}", !has && Ranch.Chem >= cost, () => Finish(Lab.AddMachine(Ranch, id, graft)));
            b.Icon = has ? Glyphs.Get("check", 1) : flask;
            machines.AddChild(b);
        }
        _rows.AddChild(machines);

        if (rec.Species.Strain == Strain.Natural)
        {
            _rows.AddChild(Text("INFUSIONS · new blood · inherited by its line", 700, gold));
            var infusions = new HBoxContainer();
            infusions.AddThemeConstantOverride("separation", 6);
            foreach (var (strain, _, cost) in Lab.Infusions)
            {
                int id = _id.Value;
                var b = Btn($"{strain}  {cost}", Ranch.Chem >= cost, () => Finish(Lab.Infuse(Ranch, id, strain)));
                b.Icon = flask;
                infusions.AddChild(b);
            }
            _rows.AddChild(infusions);
        }
        else _rows.AddChild(Text($"Strain: {rec.Species.Strain} - already infused.", 700, dim));

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
        DrawString(font, new Vector2(0, 18), $"GRAFT CHAMBER - {rec.Name}", HorizontalAlignment.Center, Size.X, 17, glow);

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
