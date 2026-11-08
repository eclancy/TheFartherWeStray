using System;
using System.Linq;
using TheFartherWeStray.Party;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// The war table: every tamer, who's joined, who's going into the next battle (at most two), and who leads whom.
/// Tamers stay off the field; their specialty boosts the beasts they lead. The squad itself is picked at the pens
/// (docs/menus-plan.md, part B); here, with two tamers leading, a beast can go to the other one: to an empty place,
/// or swapped with one of theirs (pick it, then one of the other's).
/// </summary>
public partial class PartyPanel : Control
{
    public Ranch Ranch;
    public Action<string> Changed;
    VBoxContainer _box;
    int? _picked;   // a beast picked to swap with one of the other tamer's
    string _keep;   // the pad: the button to put the focus back on after the panel is rebuilt ("beast:ID", "tamer:ID")

    static Color Ink => Ui.Ink;
    static Color Dim => Ui.Dim;
    static Color Gold => Ui.Gold;
    static Color Cyan => Ui.Cyan;

    public override void _Ready()
    {
        var scroll = new ScrollContainer { Size = Size, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _box = new VBoxContainer { CustomMinimumSize = new Vector2(Size.X - 16, 0) };
        _box.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_box);
    }

    Button Btn(string text, Action onPress, bool enabled = true)
    {
        var b = new Button { Text = text, Disabled = !enabled, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
        b.AddThemeFontSizeOverride("font_size", 14);
        b.Pressed += onPress;
        return b;
    }

    /// <summary>On a phone, the button pressed keeps the pad's focus when the panel is rebuilt round it.</summary>
    void Keep(Button b, string key)
    {
        if (!Phone.On || _keep != key) return;
        _keep = null;
        b.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Refresh()
    {
        if (_box == null) return;
        foreach (var child in _box.GetChildren()) { _box.RemoveChild(child); child.QueueFree(); }
        if (_picked is { } p && !Ranch.Squad.Contains(p)) _picked = null;
        _box.AddChild(Ui.Text($"{Ranch.Fielded.Count}/{TamerBook.MaxFielded} tamers leading · {Ranch.Squad.Count}/{Ranch.SquadSize} beasts", 17, Gold));
        _box.AddChild(Ui.Text("The squad is picked at the pens.", 14, Dim, 4));
        var joined = Ranch.Party.Select(w => w.Id).ToHashSet();
        foreach (var w in TamerBook.All)
        {
            bool isIn = joined.Contains(w.Id), fielded = Ranch.Fielded.Contains(w.Id);
            var card = new PanelContainer();
            // A fielded tamer's card is lit gold; one who hasn't joined yet is dim.
            var box = fielded ? UiSkin.Lit(UiPart.Small, Gold, true) : UiSkin.Box(UiPart.Small, isIn ? UiState.Normal : UiState.Disabled);
            card.AddThemeStyleboxOverride("panel", UiSkin.Tight(box, 16));
            var rows = new VBoxContainer();
            rows.AddThemeConstantOverride("separation", 6);
            card.AddChild(rows);

            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 10);
            head.AddChild(Ui.Picture(Glyphs.Get(isIn ? w.Glyph : "lock", 2)));
            var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            names.AddThemeConstantOverride("separation", 0);
            names.AddChild(Ui.Text(w.Name, 19, fielded ? Gold : isIn ? Ink : Dim));
            names.AddChild(Ui.Text(isIn ? $"{w.Specialty} · leads {w.LeadsHint ?? "any beast"}" : $"joins at encounter {w.JoinsAt}", 14, Dim, 4));
            head.AddChild(names);
            if (isIn)
            {
                var id = w.Id;
                string key = $"tamer:{id}";
                var lead = Btn(fielded ? "Stand down" : "Lead", () => { _keep = key; Toggle(id); }, fielded ? Ranch.Fielded.Count > 1 : Ranch.Fielded.Count < TamerBook.MaxFielded);
                Keep(lead, key);
                head.AddChild(lead);
            }
            rows.AddChild(head);
            var text = Ui.Text(w.Text, 14, isIn ? Ink : Dim, 4);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            rows.AddChild(text);
            if (fielded) rows.AddChild(Led(w));
            _box.AddChild(card);
        }
    }

    /// <summary>The three places a fielded tamer leads: who's in them, and, with another tamer leading, a way across.</summary>
    Control Led(TamerDef w)
    {
        var slots = new HFlowContainer();
        slots.AddThemeConstantOverride("h_separation", 6);
        var other = Ranch.Fielded.Where(f => f != w.Id).Select(TamerBook.Get).FirstOrDefault();
        foreach (var cid in Ranch.Squad.Where(c => Ranch.LeaderOf(c)?.Id == w.Id).ToList())
        {
            var c = Ranch.Get(cid);
            string boost = w.Boosts(c) ? $"Boosted: {w.Text}" : $"Not boosted: {w.Name} leads {w.LeadsHint}.";
            if (other == null)
            {
                slots.AddChild(Ui.ChipView(new Chip(w.Boosts(c) ? "up" : "dot", c.Name, Ink, boost), 15));
                continue;
            }
            bool room = Ranch.Led(other.Id) < TamerBook.CreaturesEach, picked = _picked == cid;
            string key = $"beast:{cid}";
            var b = Btn(c.Name, () => { _keep = key; Across(cid, w, other); });
            Keep(b, key);
            b.Icon = Glyphs.Get(w.Boosts(c) ? "up" : "dot", 1);
            if (picked) b.AddThemeColorOverride("font_color", Cyan);
            b.TooltipText = boost + "\n" + (room ? $"Moves it to {other.Name}."
                : picked ? $"Picked: now one of {other.Name}'s to swap with (or this one again to put it down)."
                : _picked != null && Ranch.LeaderOf(_picked.Value)?.Id == other.Id ? $"Swaps it with {Ranch.Get(_picked.Value).Name}."
                : $"Pick it, then one of {other.Name}'s, to swap them.");
            slots.AddChild(b);
        }
        for (int k = Ranch.Led(w.Id); k < TamerBook.CreaturesEach; k++) slots.AddChild(Ui.Text("· empty ·", 14, Dim, 3));
        return slots;
    }

    /// <summary>A beast to the other tamer: straight there if they've room, else picked, then swapped with one of theirs.</summary>
    void Across(int id, TamerDef from, TamerDef to)
    {
        if (_picked == id) { _picked = null; Refresh(); return; }
        if (_picked is { } p && Ranch.LeaderOf(p)?.Id != from.Id && Ranch.SwapLeaders(p, id))
        {
            _picked = null;
            Done($"{Ranch.Get(p).Name} and {Ranch.Get(id).Name} swap: {Ranch.Get(p).Name} goes to {Ranch.LeaderOf(p).Name}, {Ranch.Get(id).Name} to {Ranch.LeaderOf(id).Name}.");
            return;
        }
        if (Ranch.MoveLeader(id, to.Id)) { _picked = null; Done($"{Ranch.Get(id).Name} goes to {to.Name}."); return; }
        _picked = id;
        Changed?.Invoke($"{Ranch.Get(id).Name} picked: now one of {to.Name}'s to swap with.");
        Refresh();
    }

    void Toggle(string tamer)
    {
        bool was = Ranch.Fielded.Contains(tamer);
        if (!Ranch.ToggleField(tamer)) return;
        _picked = null;
        Done(was ? $"{TamerBook.Get(tamer).Name} stands down; their beasts stay home." : $"{TamerBook.Get(tamer).Name} will lead in the next battle: pick their beasts at the pens.");
    }

    void Done(string message)
    {
        Ranch.Save();
        Changed?.Invoke(message);
        Refresh();
    }
}
