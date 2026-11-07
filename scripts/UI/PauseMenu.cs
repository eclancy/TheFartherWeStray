using System;
using System.Collections.Generic;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The pause menu, on every screen: Esc opens it on a desktop (once nothing is open that Esc closes first: a
/// panel, an aimed skill), Start on a phone's pad (where B goes back instead). The game stands still while it's
/// open: time stops, so walks, turns, the turn timer and scenes wait. Resume, the screen's own items (a scene's
/// Skip), Controls, Settings, and where there's a game to save, save and go back to the title, or save and quit.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    public Action Save;      // what to save first (the host's ranch, where you stand); unset in a battle or a scene
    public Action ToTitle;
    public Action Closed;
    public string Screen;    // the screen it's over: its controls come first on the Controls page (ControlsBook ids)
    public List<(string Label, Action Act)> Extra = new(); // the screen's own items, after Resume
    public static string DemoPage;                         // --pause=controls: open on the Controls page (screenshots)

    /// <summary>Esc on a desktop, or the pad's Start: the menu. (On a phone the pad's B sends Esc, and means back.)</summary>
    public static bool Opens(InputEventKey k) => k.Keycode == Phone.Start || k.Keycode == Key.Escape && !Phone.On;

    /// <summary>The pad's B on a phone: back, where there's somewhere to go back to. A desktop has the Back button.</summary>
    public static bool GoesBack(InputEventKey k) => k.Keycode == Key.Escape && Phone.On;

    VBoxContainer _col;
    Control _page;
    Button _opener;
    double _was = -1;

    public override void _Ready()
    {
        Layer = 20;
        _was = Engine.TimeScale;
        Engine.TimeScale = 0;
        var vp = GetViewport().GetVisibleRect().Size;
        var shade = new ColorRect { Color = new Color(0.02f, 0.01f, 0.05f, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);
        _col = new VBoxContainer { Position = new Vector2(vp.X / 2 - 200, vp.Y * 0.22f) };
        _col.AddThemeConstantOverride("separation", 14);
        AddChild(_col);
        _col.AddChild(Ui.Text("PAUSED", 30, Ui.Gold, 8));
        var resume = Item("Resume", Close);
        foreach (var (label, act) in Extra) Item(label, () => { Close(); act(); });
        Button controls = null;
        controls = Item("Controls", () => Open(controls, new ControlsPanel { First = Screen, Closed = ClosePage }, ControlsPanel.Frame(vp)));
        Button settings = null;
        settings = Item("Settings", () => Open(settings, new SettingsPanel { Closed = ClosePage },
            new Rect2(vp.X / 2 - 320, Mathf.Max(20, vp.Y / 2 - 300), 0, 0)));
        if (Save != null && ToTitle != null) Item("Save and return to title", () => { Save(); ToTitle(); });
        if (Save != null) Item("Save and quit", () => { Save(); GetTree().Quit(); });
        if (DemoPage == "controls") { DemoPage = null; controls.EmitSignal(BaseButton.SignalName.Pressed); }
        else resume.CallDeferred(Control.MethodName.GrabFocus);
    }

    Button Item(string text, Action pressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(400, 58) };
        b.AddThemeFontSizeOverride("font_size", 22);
        b.Pressed += pressed;
        _col.AddChild(b);
        return b;
    }

    /// <summary>A page over the menu (the controls, the settings); Esc, B or Start closes it and the menu's back.</summary>
    void Open(Button from, Control page, Rect2 at)
    {
        _opener = from;
        _col.Visible = false;
        _page = page;
        page.Position = at.Position;
        if (at.Size != Vector2.Zero) page.Size = at.Size;
        AddChild(page);
    }

    void ClosePage()
    {
        _page?.QueueFree();
        _page = null;
        _col.Visible = true;
        _opener?.GrabFocus();
    }

    public void Close()
    {
        Thaw();
        QueueFree();
        Closed?.Invoke();
    }

    /// <summary>Time runs again, at the speed it ran before.</summary>
    void Thaw()
    {
        if (_was < 0) return;
        Engine.TimeScale = _was;
        _was = -1;
    }

    public override void _ExitTree() => Thaw();

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true } k || k.Keycode == Key.N) return; // N still turns the sound off
        // Nothing reaches the screen under the menu. Esc, B or Start closes the page that's open, then the menu.
        GetViewport().SetInputAsHandled();
        if (k.Echo || k.Keycode is not (Key.Escape or Phone.Start)) return;
        if (_page != null) ClosePage();
        else Close();
    }
}
