using System;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The pause menu: resume, the settings, save and go back to the title, or save and quit. Esc opens
/// it at the base; on a phone, Start opens it anywhere out of battle (B closes it).
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    public Action Save;      // what to save first (the host's ranch, where you stand)
    public Action ToTitle;
    public Action Closed;

    VBoxContainer _col;

    public override void _Ready()
    {
        Layer = 4;
        var vp = GetViewport().GetVisibleRect().Size;
        var shade = new ColorRect { Color = new Color(0.02f, 0.01f, 0.05f, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);
        _col = new VBoxContainer { Position = new Vector2(vp.X / 2 - 200, vp.Y * 0.3f) };
        _col.AddThemeConstantOverride("separation", 14);
        AddChild(_col);
        _col.AddChild(Ui.Text("PAUSED", 30, Ui.Gold, 8));
        Item("Resume", Close).CallDeferred(Control.MethodName.GrabFocus);
        Item("Settings", () =>
        {
            _col.Visible = false;
            var settings = new SettingsPanel { Position = new Vector2(vp.X / 2 - 320, Mathf.Max(20, vp.Y / 2 - 300)) };
            settings.Closed = () =>
            {
                settings.QueueFree();
                _col.Visible = true;
                (_col.GetChild(1) as Control)?.GrabFocus();
            };
            AddChild(settings);
        });
        Item("Save and return to title", () => { Save?.Invoke(); ToTitle?.Invoke(); });
        Item("Save and quit", () => { Save?.Invoke(); GetTree().Quit(); });
    }

    Button Item(string text, Action pressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(400, 58) };
        b.AddThemeFontSizeOverride("font_size", 22);
        b.Pressed += pressed;
        _col.AddChild(b);
        return b;
    }

    public void Close()
    {
        QueueFree();
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Esc, B or Start again closes it (the settings first, if they're open).
        if (e is not InputEventKey { Pressed: true, Echo: false } k || k.Keycode is not (Key.Escape or Phone.Start)) return;
        GetViewport().SetInputAsHandled();
        if (!_col.Visible) { foreach (var child in GetChildren()) if (child is SettingsPanel s) s.Closed?.Invoke(); return; }
        Close();
    }
}
