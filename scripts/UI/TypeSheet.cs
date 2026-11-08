using Godot;
using TheFartherWeStray.Tactics;

namespace TheFartherWeStray.UI;

/// <summary>
/// The game's type (PixelFont) at a glance (--typesheet --shot=N, any --ui=NAME): every character it draws,
/// each size band (14-22 point words at 2x, 23-31 at 3x...), the look's word colours on its glass, and words
/// straight over a board, where only their shadow keeps them apart from the art.
/// </summary>
public partial class TypeSheet : Control
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var vp = GetViewportRect().Size;
        var bg = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, 7, BattleMap.ByName("Lantern Row").Theme)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var panel = new Panel { Position = new Vector2(40, 40), Size = new Vector2(vp.X * 0.62f, Mathf.Min(vp.Y - 80, 620)) };
        panel.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        AddChild(panel);
        float y = 30;
        void Line(string text, int size, Color color)
        {
            var l = Ui.Text(text, size, color);
            l.Position = new Vector2(30, y);
            panel.AddChild(l);
            y += PixelFont.Scale(Ui.Fit(size)) * (PixelFont.Ascent + PixelFont.Descent + 2) + 4;
        }
        Line("THE GAME'S TYPE", 26, UiSkin.Heading());
        Line("ABCDEFGHIJKLMNOPQRSTUVWXYZ  0123456789", 17, Ui.Ink);
        Line("abcdefghijklmnopqrstuvwxyz  éèêëáàâäöüñ", 17, Ui.Ink);
        Line("!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~  · • — – … × ° → ← ↑ ↓ ★ ▼", 17, Ui.Ink);
        y += 10;
        foreach (int size in new[] { 14, 17, 22, 24, 30, 40 })
            Line($"{size} pt (x{PixelFont.Scale(size)}): Blaze, 45/45 HP", size, Ui.Ink);
        y += 10;
        Line("Secondary words: day 99, 2693 chem", 17, Ui.Dim);
        Line("Faint words: locked, resting", 17, Ui.Faint);
        Line("Heading gold  ·  Thoroughbred", 17, Ui.Gold);
        Line("Cyan  ·  in the squad  ·  +3 DEF", 17, Ui.Cyan);
        Line("Magenta  ·  wild pack  ·  VAT A", 17, Ui.Magenta);
        Line("Good  ·  healed 12   Bad  ·  fainted -18", 17, Ui.Good);

        var over = new VBoxContainer { Position = new Vector2(vp.X * 0.62f + 80, 60) };
        over.AddThemeConstantOverride("separation", 10);
        AddChild(over);
        over.AddChild(Ui.Text("Over the board", 22, UiSkin.Heading()));
        over.AddChild(Ui.Text("NEW  The Pens", 17, Ui.Gold));
        over.AddChild(Ui.Text("Lantern Row, day 99", 17, Ui.Ink));
        over.AddChild(Ui.Text("A pack of strays blocks the way.", 16, Ui.Dim));
        var tip = new PanelContainer();
        tip.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Tip, UiState.Normal));
        tip.AddChild(Ui.Text("A tooltip: the full words.", 17, Ui.Ink));
        over.AddChild(tip);
        foreach (var (text, state) in new[] { ("A button", UiState.Normal), ("Pointed at", UiState.Hover), ("Can't use", UiState.Disabled) })
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(320, 50), Disabled = state == UiState.Disabled, FocusMode = FocusModeEnum.None };
            b.AddThemeFontSizeOverride("font_size", 20);
            if (state == UiState.Hover) b.AddThemeStyleboxOverride("normal", UiSkin.Box(UiPart.Button, UiState.Hover));
            over.AddChild(b);
        }
    }
}
