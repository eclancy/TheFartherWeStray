using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Tactics;
using CreatureTactics.UI;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>
/// A list of missions as cards, each with a thumbnail of its map, the objective, difficulty, chem
/// reward and any sighting, duel or main-mission note. Click one to pick it; Depart (or F) takes
/// the squad. The country map shows each place's missions in one.
/// </summary>
public partial class MissionBoardPanel : Control
{
    public Ranch Ranch;
    public Action<Mission> Depart;
    public Func<List<Mission>> Source;       // the missions to show (the encounter's whole board if unset)
    public string Heading;                   // the title over them
    public string DepartLabel = "Depart  (F)";
    public Func<Mission, string> DepartLabelFor; // the Depart button's words for the picked mission, if they vary
    Button _depart;

    public List<Mission> Missions = new();
    public int Selected;
    static readonly Dictionary<string, Texture2D> Thumbs = new();

    public void Refresh()
    {
        Missions = Source?.Invoke() ?? Mission.Board(Ranch.Encounter, Ranch.Codex, rivalWins: Ranch.Rivals);
        Selected = Math.Clamp(Selected, 0, Math.Max(0, Missions.Count - 1));
        foreach (var child in GetChildren()) child.QueueFree();
        if (Missions.Count == 0) return;
        // More cards than fit scroll.
        var scroll = new ScrollContainer { Position = new Vector2(0, 30), Size = new Vector2(Size.X, Size.Y - 100), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(list);
        for (int i = 0; i < Missions.Count; i++)
        {
            int index = i;
            var card = new MissionCard { Board = this, Index = i, CustomMinimumSize = new Vector2(Size.X - 16, 190) };
            card.Clicked += () =>
            {
                Selected = index;
                Audio.Sound.Play("click");
                _depart.Text = LabelFor(Missions[Selected]);
                foreach (var c in list.GetChildren().OfType<MissionCard>()) c.QueueRedraw();
            };
            list.AddChild(card);
        }
        _depart = new Button { Text = LabelFor(Missions[Selected]), FocusMode = FocusModeEnum.None, Position = new Vector2(Size.X - 240, Size.Y - 56), CustomMinimumSize = new Vector2(240, 48) };
        _depart.AddThemeFontSizeOverride("font_size", 18);
        _depart.Pressed += () => Depart?.Invoke(Missions[Selected]);
        AddChild(_depart);
    }

    string LabelFor(Mission m) => DepartLabelFor?.Invoke(m) ?? DepartLabel;

    public override void _Draw()
    {
        DrawString(GetThemeDefaultFont(), new Vector2(0, 18), Heading ?? $"MISSION BOARD · encounter {Ranch.Encounter}",
            HorizontalAlignment.Left, Size.X, 16, Color.FromHtml("#f2c14e"));
    }

    /// <summary>A small picture of a map, painted once.</summary>
    public static Texture2D Thumb(string map)
    {
        if (!Thumbs.TryGetValue(map, out var t))
            Thumbs[map] = t = ImageTexture.CreateFromImage(new IsoArt(BattleMap.ByName(map)).PaintBoard());
        return t;
    }
}

/// <summary>One contract on the board.</summary>
public partial class MissionCard : Control
{
    public MissionBoardPanel Board;
    public int Index;
    public event Action Clicked;
    bool _hover;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass; // the wheel still scrolls the list
        TextureFilter = TextureFilterEnum.Nearest;
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Clicked?.Invoke();
    }

    static string Goal(Mission m) => m.Dispatch != null ? "hand" : m.Cup != null ? "crown" : m.Goal switch
    {
        Objective.Survive => "clock", Objective.Hunt => "hit", Objective.Capture => "chain", Objective.Duel => "slash", _ => "fist",
    };

    public override void _Draw()
    {
        var m = Board.Missions[Index];
        bool selected = Board.Selected == Index;
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.08f, 0.16f, 0.92f),
            BorderColor = selected ? Color.FromHtml("#f2c14e") : _hover ? new Color(0.6f, 0.55f, 0.75f) : new Color(0.24f, 0.2f, 0.33f),
        };
        box.SetBorderWidthAll(selected ? 3 : 1);
        box.SetCornerRadiusAll(8);
        DrawStyleBox(box, new Rect2(Vector2.Zero, Size));

        // The map, shrunk to fit on the left.
        var thumb = MissionBoardPanel.Thumb(m.MapName);
        float scale = Mathf.Min(260f / thumb.GetWidth(), (Size.Y - 20) / thumb.GetHeight());
        var ts = thumb.GetSize() * scale;
        DrawTextureRect(thumb, new Rect2(new Vector2(12, (Size.Y - ts.Y) / 2), ts), false);

        var font = GetThemeDefaultFont();
        var ink = Color.FromHtml("#e8e2f4");
        var dim = Color.FromHtml("#9a91b4");
        var gold = Color.FromHtml("#f2c14e");
        float x = 290, w = Size.X - x - 12;
        // Title behind the objective's glyph; then difficulty stars, pack size and reward as glyphs and numbers.
        DrawTexture(Glyphs.Get(Goal(m), 2), new Vector2(x - 4, 8));
        DrawString(font, new Vector2(x + 26, 32), m.Title, HorizontalAlignment.Left, w - 26, 20, ink);
        float cx = x;
        if (m.Dispatch == null)
        {
            DrawTexture(SkillIcons.Stars(m.Stars > 3 ? 3 : m.Stars, 2), new Vector2(x, 44));
            if (m.Stars > 3) DrawTexture(SkillIcons.Stars(m.Stars - 3, 2), new Vector2(x + 38, 44));
            cx = x + (m.Stars > 3 ? 84 : 48);
        }
        // A dispatch shows how long it takes; a battle, how many it's against.
        var counts = m.Dispatch != null ? new[] { ("clock", $"{m.Dispatch.Days}d"), ("flask", m.Reward.ToString()) } : new[] { ("paw", m.Pack.ToString()), ("flask", m.Reward.ToString()) };
        foreach (var (glyph, text) in counts)
        {
            DrawTexture(Glyphs.Get(glyph, 2), new Vector2(cx, 40));
            DrawString(font, new Vector2(cx + 28, 61), text, HorizontalAlignment.Left, 60, 17, gold);
            cx += 72;
        }
        DrawString(font, new Vector2(x, 90), m.Brief(), HorizontalAlignment.Left, w, 15, ink);
        string line = m.Dispatch != null ? $"No battle: one creature goes, and is away {m.Dispatch.Days} {(m.Dispatch.Days == 1 ? "day" : "days")}."
            : m.Flavor ?? m.Rival?.Ideal ?? BattleMap.ByName(m.MapName).Blurb;
        DrawString(font, new Vector2(x, 114), line, HorizontalAlignment.Left, w, 14, dim);
        if (m.Story is { } story)
        {
            // A main mission: what it opens on the country map.
            DrawTexture(Glyphs.Get("star", 2), new Vector2(x - 4, 128));
            DrawString(font, new Vector2(x + 26, 150), $"Main mission  ·  opens {World.Country.Get(World.StoryBook.Get(story).Opens).Name}", HorizontalAlignment.Left, w - 26, 15, gold);
        }
        else if (m.Roamer != 0)
        {
            DrawTexture(Glyphs.Get("paw", 2), new Vector2(x - 4, 128));
            DrawString(font, new Vector2(x + 26, 150), "Roaming pack  ·  moves on each day; cross its path and it attacks", HorizontalAlignment.Left, w - 26, 15, Color.FromHtml("#ff6a5a"));
        }
        else if (m.Cup is { } cup)
        {
            DrawTexture(Glyphs.Get("crown", 2), new Vector2(x - 4, 128));
            DrawString(font, new Vector2(x + 26, 150), $"Three duels in a row  ·  the cup pays {World.CupBook.Get(cup).Prize} chem", HorizontalAlignment.Left, w - 26, 15, gold);
        }
        else if (m.Dispatch is { } job)
        {
            DrawTexture(Glyphs.Get("check", 2), new Vector2(x - 4, 128));
            DrawString(font, new Vector2(x + 26, 150), "Suits: " + string.Join(", ", job.Suits.Select(s => s.hint)), HorizontalAlignment.Left, w - 26, 15, Color.FromHtml("#4fe3ff"));
        }
        else if (m.Rival is { } rival)
        {
            // The rival's own boost, and how often you've beaten them.
            DrawTexture(Glyphs.Get(rival.Tamer.Glyph, 2), new Vector2(x - 4, 128));
            string record = m.RivalWins > 0 ? $"beaten {m.RivalWins}x  ·  " : "";
            DrawString(font, new Vector2(x + 26, 150), $"{record}{rival.Tamer.Text}", HorizontalAlignment.Left, w - 26, 15, Color.FromHtml("#ff7aa8"));
        }
        else if (m.Repeatable)
        {
            DrawTexture(Glyphs.Get("cooldown", 2), new Vector2(x - 4, 128));
            DrawString(font, new Vector2(x + 26, 150), "Regular job  ·  always on offer here", HorizontalAlignment.Left, w - 26, 15, Color.FromHtml("#4ff0a0"));
        }
        else if (m.Sighting != null)
        {
            DrawTexture(Glyphs.Get("eye", 2), new Vector2(x - 4, 128));
            DrawString(font, new Vector2(x + 26, 150), $"{m.Sighting}  ·  new to your codex", HorizontalAlignment.Left, w - 26, 15, Color.FromHtml("#4ff0a0"));
        }
    }
}
