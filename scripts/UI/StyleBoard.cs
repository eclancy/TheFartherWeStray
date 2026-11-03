using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Tactics;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// One menu style's parts side by side over a real board, for comparing the styles
/// (docs/ui-style.md; --uiboard=NAME --shot=N): a menu's buttons in every state, a window with
/// creature cards and small buttons, a tooltip, a dialogue box, a unit panel and the skill bar.
/// </summary>
public partial class StyleBoard : Control
{
    public Skin Skin;

    CanvasLayer _ui;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var vp = GetViewportRect().Size;
        var map = BattleMap.ByName("Lantern Row");
        var art = new IsoArt(map, BattleScreen.Skirt);
        var bg = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, 7, map.Theme)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var board = new Node2D { Scale = new Vector2(3, 3), TextureFilter = TextureFilterEnum.Nearest };
        AddChild(board);
        board.AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(art.PaintBoard()), Centered = false });
        PropArt.Place(board, map, art);
        board.Position = vp / 2 - art.TileCenter(new Vector2(map.W / 2f, map.H / 2f), 0) * 3;
        var wash = new ColorRect { Color = new Color(0.02f, 0.015f, 0.05f, 0.35f), MouseFilter = MouseFilterEnum.Ignore };
        wash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(wash);

        _ui = new CanvasLayer { Layer = 2 };
        AddChild(_ui);
        Put(Ui.Text(UiSkin.Title(Skin), 44, UiSkin.Heading(Skin), 10), 80, 40);

        // A menu, one button in each state.
        var states = new (string text, UiState state)[]
        {
            ("Continue", UiState.Focus), ("New game", UiState.Hover), ("Load", UiState.Disabled), ("Settings", UiState.Normal), ("Quit", UiState.Pressed),
        };
        for (int i = 0; i < states.Length; i++)
        {
            var (text, state) = states[i];
            Put(Plate(UiPart.Button, state, new Vector2(380, 62), "   " + text, 26), 80, 150 + i * 76);
            Put(Ui.Text(state switch { UiState.Focus => "on it (the pad's focus)", UiState.Hover => "pointer over", UiState.Pressed => "pressed", UiState.Disabled => "can't use", _ => "normal" }, 16, Ui.Dim, 4), 476, 170 + i * 76);
        }

        // A window: the pens, with three cards and small buttons.
        var window = Plate(UiPart.Panel, UiState.Normal, new Vector2(660, 470));
        Put(window, 700, 150);
        var head = Ui.Text("THE PENS", 26, UiSkin.Heading(Skin), 6);
        head.Position = new Vector2(30, 22);
        window.AddChild(head);
        var squad = Encounter.StarterSquad().Select(x => x.Item1).Take(3).ToList();
        for (int i = 0; i < squad.Count; i++)
        {
            var state = i == 1 ? UiState.Focus : i == 2 ? UiState.Hover : UiState.Normal;
            var card = Plate(UiPart.Button, state, new Vector2(186, 250));
            card.Position = new Vector2(30 + i * 204, 72);
            window.AddChild(card);
            var pic = new TextureRect
            {
                Texture = ImageTexture.CreateFromImage(CreaturePainter.Paint(squad[i], false)),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = TextureFilterEnum.Nearest, Size = new Vector2(150, 140), Position = new Vector2(18, 14), MouseFilter = MouseFilterEnum.Ignore,
            };
            card.AddChild(pic);
            var name = Ui.Text(squad[i].Name, 20, UiSkin.TextColor(state, Skin), 5);
            name.Position = new Vector2(20, 158);
            card.AddChild(name);
            var chips = Ui.Chips(new[] { new Chip("hp", (40 + i * 6).ToString()), new Chip("atk", (12 + i).ToString()), new Chip("spd", (6 - i).ToString()) }, 15, 2, 8);
            chips.Position = new Vector2(18, 200);
            chips.Size = new Vector2(160, 30);
            card.AddChild(chips);
        }
        var small = new[] { ("Join squad", UiState.Focus), ("Release", UiState.Normal), ("Name", UiState.Normal) };
        float sx = 30;
        foreach (var (text, state) in small)
        {
            var b = Plate(UiPart.Button, state, new Vector2(text.Length * 9 + 40, 40), text, 16, center: true);
            b.Position = new Vector2(sx, 344);
            window.AddChild(b);
            sx += b.Size.X + 12;
        }
        var note = Ui.Text("Small buttons: the same frame at a smaller size.", 15, Ui.Dim, 4);
        note.Position = new Vector2(30, 404);
        window.AddChild(note);

        // A tooltip, a dialogue box and a unit's panel.
        var tip = Plate(UiPart.Tip, UiState.Normal, new Vector2(470, 84));
        Put(tip, 1400, 150);
        var tipText = Ui.Text("Claws: strikes a foe next to it.\nHighlighted, every control shows its full words here.", 17, Ui.Ink, 4);
        tipText.Position = new Vector2(22, 14);
        tip.AddChild(tipText);

        var talk = Plate(UiPart.Panel, UiState.Normal, new Vector2(470, 190));
        Put(talk, 1400, 262);
        var who = Ui.Text("TAMER", 20, UiSkin.Heading(Skin), 5);
        who.Position = new Vector2(30, 22);
        talk.AddChild(who);
        var line = Ui.Text("A dialogue box: the speaker's name, then their line, typed out a letter at a time.", 19, Ui.Ink, 4);
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.Position = new Vector2(30, 58);
        line.Size = new Vector2(410, 100);
        talk.AddChild(line);

        var unit = Plate(UiPart.Panel, UiState.Normal, new Vector2(470, 150));
        Put(unit, 1400, 480);
        var lead = squad[0];
        var pic2 = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(CreaturePainter.Paint(lead, false)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest, Size = new Vector2(90, 90), Position = new Vector2(24, 26), MouseFilter = MouseFilterEnum.Ignore,
        };
        unit.AddChild(pic2);
        var uname = Ui.Text(lead.Name, 24, UiSkin.Heading(Skin), 6);
        uname.Position = new Vector2(130, 24);
        unit.AddChild(uname);
        var uchips = Ui.Chips(new[] { new Chip("hp", "51/51"), new Chip("atk", "16"), new Chip("def", "9"), new Chip("spd", "4"), new Chip("move", "3") }, 17, 2, 10);
        uchips.Position = new Vector2(130, 70);
        uchips.Size = new Vector2(320, 30);
        unit.AddChild(uchips);

        // The skill bar: attack, three skills (one highlighted, one used up) and an item.
        var icons = new[]
        {
            (SkillIcons.Attack(Weapon.Claws, 3), UiState.Normal), (SkillIcons.Action("flame", 3), UiState.Focus), (SkillIcons.Action("heal", 3), UiState.Hover),
            (SkillIcons.Action("bolt", 3), UiState.Disabled), (SkillIcons.Action("flask", 3), UiState.Normal),
        };
        for (int i = 0; i < icons.Length; i++)
        {
            var slot = Plate(UiPart.Button, icons[i].Item2, new Vector2(78, 78));
            Put(slot, 80 + i * 90, vp.Y - 140);
            var icon = new TextureRect { Texture = icons[i].Item1, StretchMode = TextureRect.StretchModeEnum.KeepCentered, Size = new Vector2(78, 78), MouseFilter = MouseFilterEnum.Ignore, Modulate = icons[i].Item2 == UiState.Disabled ? new Color(1, 1, 1, 0.35f) : Colors.White };
            slot.AddChild(icon);
        }
        Put(Ui.Text("The skill bar: highlighted, pointed at, used up.", 16, Ui.Dim, 4), 80, vp.Y - 52);
    }

    void Put(Control c, float x, float y)
    {
        c.Position = new Vector2(x, y);
        _ui.AddChild(c);
    }

    /// <summary>A part drawn in a state, as a box of a size (a focused one with its focus frame over it), with words in it.</summary>
    Control Plate(UiPart part, UiState state, Vector2 size, string text = null, int fontSize = 20, bool center = false)
    {
        var root = new Control { Size = size, MouseFilter = MouseFilterEnum.Ignore };
        var under = new Panel { Size = size, MouseFilter = MouseFilterEnum.Ignore };
        under.AddThemeStyleboxOverride("panel", UiSkin.Box(part, state == UiState.Focus ? UiState.Normal : state, Skin));
        root.AddChild(under);
        if (state == UiState.Focus)
        {
            var over = new Panel { Size = size, MouseFilter = MouseFilterEnum.Ignore };
            over.AddThemeStyleboxOverride("panel", UiSkin.Box(part, UiState.Focus, Skin));
            root.AddChild(over);
        }
        if (text != null)
        {
            var l = Ui.Text(text, fontSize, UiSkin.TextColor(state, Skin), 4);
            l.AddThemeColorOverride("font_outline_color", UiSkin.Outline(Skin));
            l.Size = size;
            l.HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            root.AddChild(l);
        }
        return root;
    }
}
