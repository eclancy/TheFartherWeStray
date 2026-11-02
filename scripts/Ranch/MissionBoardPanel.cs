using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Tactics;
using CreatureTactics.UI;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>
/// The mission board: this round's contracts, each with a thumbnail of its map, the objective,
/// difficulty, chem reward and any sighting. Click one to pick it; Depart (or F) takes the squad.
/// </summary>
public partial class MissionBoardPanel : Control
{
    public Ranch Ranch;
    public Action<Mission> Depart;

    public List<Mission> Missions = new();
    public int Selected;
    static readonly Dictionary<string, Texture2D> Thumbs = new();

    public void Refresh()
    {
        Missions = Mission.Board(Ranch.Encounter, Ranch.Codex);
        Selected = Math.Clamp(Selected, 0, Missions.Count - 1);
        foreach (var child in GetChildren()) child.QueueFree();
        var list = new VBoxContainer { Position = new Vector2(0, 30), Size = new Vector2(Size.X, Size.Y - 100) };
        list.AddThemeConstantOverride("separation", 10);
        AddChild(list);
        for (int i = 0; i < Missions.Count; i++)
        {
            int index = i;
            var card = new MissionCard { Board = this, Index = i, CustomMinimumSize = new Vector2(Size.X, 190) };
            card.Clicked += () => { Selected = index; foreach (var c in list.GetChildren().OfType<MissionCard>()) c.QueueRedraw(); };
            list.AddChild(card);
        }
        var depart = new Button { Text = "Depart  (F)", FocusMode = FocusModeEnum.None, Position = new Vector2(Size.X - 200, Size.Y - 56), CustomMinimumSize = new Vector2(200, 48) };
        depart.AddThemeFontSizeOverride("font_size", 18);
        depart.Pressed += () => Depart?.Invoke(Missions[Selected]);
        AddChild(depart);
    }

    public override void _Draw()
    {
        DrawString(GetThemeDefaultFont(), new Vector2(0, 18), $"MISSION BOARD · encounter {Ranch.Encounter}",
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
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Clicked?.Invoke();
    }

    static string Goal(Objective o) => o switch { Objective.Survive => "clock", Objective.Hunt => "hit", Objective.Capture => "chain", _ => "fist" };

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
        DrawTexture(Glyphs.Get(Goal(m.Goal), 2), new Vector2(x - 4, 8));
        DrawString(font, new Vector2(x + 26, 32), m.Title, HorizontalAlignment.Left, w - 26, 20, ink);
        DrawTexture(SkillIcons.Stars(m.Stars > 3 ? 3 : m.Stars, 2), new Vector2(x, 44));
        if (m.Stars > 3) DrawTexture(SkillIcons.Stars(m.Stars - 3, 2), new Vector2(x + 38, 44));
        float cx = x + (m.Stars > 3 ? 84 : 48);
        foreach (var (glyph, text) in new[] { ("paw", m.Pack.ToString()), ("flask", m.Reward.ToString()) })
        {
            DrawTexture(Glyphs.Get(glyph, 2), new Vector2(cx, 40));
            DrawString(font, new Vector2(cx + 28, 61), text, HorizontalAlignment.Left, 60, 17, gold);
            cx += 72;
        }
        DrawString(font, new Vector2(x, 90), m.Brief(), HorizontalAlignment.Left, w, 15, ink);
        DrawString(font, new Vector2(x, 114), BattleMap.ByName(m.MapName).Blurb, HorizontalAlignment.Left, w, 14, dim);
        if (m.Sighting != null)
        {
            DrawTexture(Glyphs.Get("eye", 2), new Vector2(x - 4, 128));
            DrawString(font, new Vector2(x + 26, 150), $"{m.Sighting}  ·  new to your codex", HorizontalAlignment.Left, w - 26, 15, Color.FromHtml("#4ff0a0"));
        }
    }
}
