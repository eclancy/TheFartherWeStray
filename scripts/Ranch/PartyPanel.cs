using System;
using System.Linq;
using TheFartherWeStray.Party;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// The party: every tamer, who's joined, who's going into the next battle (at most two), and the
/// three creatures each one leads. Tamers stay off the field; their specialty boosts the creatures
/// they lead. Click a creature to send it back to the ranch; "+ selected" puts the creature selected
/// on the left under that tamer.
/// </summary>
public partial class PartyPanel : Control
{
    public Ranch Ranch;
    public Func<int?> Selected;
    public Action<string> Changed;
    VBoxContainer _box;

    static readonly Color Ink = Ui.Ink, Dim = Ui.Dim, Gold = Ui.Gold;

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

    public void Refresh()
    {
        if (_box == null) return;
        foreach (var child in _box.GetChildren()) { _box.RemoveChild(child); child.QueueFree(); }
        _box.AddChild(Ui.Text($"PARTY · {Ranch.Fielded.Count}/{TamerBook.MaxFielded} tamers leading · {Ranch.Squad.Count}/{Ranch.SquadSize} beasts", 17, Gold));
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
                head.AddChild(Btn(fielded ? "Stand down" : "Lead", () => Toggle(id), fielded ? Ranch.Fielded.Count > 1 : Ranch.Fielded.Count < TamerBook.MaxFielded));
            }
            rows.AddChild(head);
            var text = Ui.Text(w.Text, 14, isIn ? Ink : Dim, 4);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            rows.AddChild(text);

            if (fielded)
            {
                // Their three places in the squad.
                var slots = new HFlowContainer();
                slots.AddThemeConstantOverride("h_separation", 6);
                foreach (var cid in Ranch.Squad.Where(c => Ranch.LeaderOf(c)?.Id == w.Id).ToList())
                {
                    var c = Ranch.Get(cid);
                    var b = Btn(c.Name, () => Remove(cid));
                    b.Icon = Glyphs.Get(w.Boosts(c) ? "up" : "dot", 1);
                    b.TooltipText = w.Boosts(c) ? $"Boosted: {w.Text}\nClick to take it out of the squad." : $"Not boosted: {w.Name} leads {w.LeadsHint}.\nClick to take it out of the squad.";
                    slots.AddChild(b);
                }
                int room = TamerBook.CreaturesEach - Ranch.Led(w.Id);
                for (int k = 0; k < room; k++) slots.AddChild(Ui.Text("· empty ·", 14, Dim, 3));
                var wid = w.Id;
                var sel = Selected?.Invoke();
                if (room > 0 && sel is { } sid && Ranch.LeaderOf(sid)?.Id != w.Id)
                    slots.AddChild(Btn($"+ {Ranch.Get(sid).Name}", () => Add(sid, wid)));
                rows.AddChild(slots);
            }
            _box.AddChild(card);
        }
    }

    void Toggle(string tamer)
    {
        bool was = Ranch.Fielded.Contains(tamer);
        if (!Ranch.ToggleField(tamer)) return;
        Done(was ? $"{TamerBook.Get(tamer).Name} stands down; their beasts stay home." : $"{TamerBook.Get(tamer).Name} will lead in the next battle.");
    }

    void Remove(int id)
    {
        Ranch.RemoveFromSquad(id);
        Done($"{Ranch.Get(id).Name} stays home.");
    }

    void Add(int id, string tamer)
    {
        if (!Ranch.AddToSquad(id, tamer)) return;
        Done($"{Ranch.Get(id).Name} joins {TamerBook.Get(tamer).Name}'s squad.");
    }

    void Done(string message)
    {
        Ranch.Save();
        Changed?.Invoke(message);
        Refresh();
    }
}
