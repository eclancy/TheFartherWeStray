using System;
using Godot;
using TheFartherWeStray.UI;

namespace TheFartherWeStray.Ranching;

/// <summary>
/// Building up a station (docs/farmyard-plan.md, part 2): its three levels and what each does, the next one's price in
/// chem and scrap, and a button that builds it at once (the user's call). Opened at the workbench, and from the
/// station's own screen (Build up, in its top bar: docs/menus-plan.md, part B). Esc, B or Close shuts it.
/// </summary>
public partial class BuildUpPanel : CanvasLayer
{
    public Ranch Ranch;
    public StationDef Station;
    public Action<string> Built;   // after building: what changed, for the screen to say and show
    public Action Closed;          // the screen forgets it

    static Color Gold => Ui.Gold;
    static Color Ink => Ui.Ink;
    static Color Dim => Ui.Dim;
    PanelContainer _panel;

    public override void _Ready()
    {
        Layer = 5;
        // A shade over the screen: nothing under it takes a click while the panel is up, and a click on it closes.
        var shade = new ColorRect { Color = new Color(0.02f, 0.01f, 0.05f, 0.45f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        shade.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close(); };
        AddChild(shade);
        Fill();
    }

    public void Close()
    {
        if (IsQueuedForDeletion()) return;
        QueueFree();
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        Close();
        GetViewport().SetInputAsHandled();
    }

    void Fill()
    {
        _panel?.QueueFree();
        var vp = GetViewport().GetVisibleRect().Size;
        var panel = _panel = new PanelContainer { CustomMinimumSize = new Vector2(Mathf.Min(760, vp.X - 80), 0) };
        panel.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        panel.AnchorLeft = panel.AnchorRight = 0.5f;
        panel.AnchorTop = panel.AnchorBottom = 0.5f;
        panel.GrowHorizontal = panel.GrowVertical = Control.GrowDirection.Both;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);
        var st = Station;
        int level = Farmyard.Level(Ranch, st.Id);
        col.AddChild(Ui.Text($"{st.Name.ToUpperInvariant()}   ·   LEVEL {level}", 22, Gold));
        var close = new Button { Text = Phone.Keys("Close  (Esc)", "Close  (B)"), FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(140, 44) };
        close.Pressed += Close;
        Control first = close;
        if (st.Upgrades == null || !st.Built(Ranch))
        {
            var why = Ui.Text(st.Upgrades == null ? "There's nothing to build on here." : $"Not built yet: it opens after {st.Needs}.", 17, Dim, 4);
            why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            col.AddChild(why);
            col.AddChild(close);
        }
        else
        {
            col.AddChild(Ui.Text($"Level 1   ·   {st.Blurb}", 17, Ink, 4));
            for (int n = 2; n <= 3; n++)
            {
                var line = Ui.Text($"Level {n}   ·   {st.Upgrades[n - 2]}", 17, n <= level ? Gold : Dim, 4);
                line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                col.AddChild(line);
            }
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            col.AddChild(row);
            Button build;
            if (level >= 3) build = new Button { Text = "Fully built up", Disabled = true, FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(0, 44) };
            else
            {
                var (chem, scrap) = HomeBase.UpgradeCosts[level - 1];
                build = new Button
                {
                    Text = $"Build level {level + 1}:  {chem} chem, {scrap} scrap", Disabled = Ranch.Chem < chem || Ranch.Scrap < scrap,
                    FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(0, 44),
                    TooltipText = $"You have {Ranch.Chem} chem and {Ranch.Scrap} scrap. The scrap press makes scrap.",
                };
                build.Pressed += () =>
                {
                    string said = Farmyard.Upgrade(Ranch, st);
                    Ranch.Save();
                    Audio.Sound.Play("hatch");
                    Built?.Invoke(said);
                    Fill();
                };
            }
            row.AddChild(build);
            row.AddChild(close);
            if (!build.Disabled) first = build;
        }
        AddChild(panel);
        PadNav.First(first);
        if (Phone.On) first.CallDeferred(Control.MethodName.GrabFocus);
    }
}
