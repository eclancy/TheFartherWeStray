using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The phone build (the user's ask, 2026-11-03): held sideways, the game runs in the middle of the
/// screen and a fixed pad stands in a column either side, never over the game and never coming or
/// going: a D-pad on the left; A (select), B (cancel) and Start (menu) on the right, like a Game
/// Boy's. On Android always; on a desktop, --phone shows the same (run it with --resolution 1200x540
/// or so for a phone's shape). The pad sends keys into the game: the arrows, Enter (A), Escape (B)
/// and Menu (Start), so every screen reads it as a keyboard; Android's back gesture is B.
/// </summary>
public static class Phone
{
    /// <summary>The phone layout is on: Android, or --phone.</summary>
    public static bool On { get; private set; }
    /// <summary>The game is laid out this tall in its own pixels (a desktop's is 1080), so text reads at a phone's size.</summary>
    public const float GameHeight = 720;
    /// <summary>The pad's Start button.</summary>
    public const Key Start = Key.Menu;
    /// <summary>The phone screen, while it's on.</summary>
    public static PhoneFrame Frame;

    public static bool Wanted() => OS.HasFeature("android") || OS.GetCmdlineUserArgs().Any(a => a == "--phone");
    public static void Begin() => On = true;

    /// <summary>The words for a control in the hints at the foot of a screen: the pad's name on a phone, the key's on a desktop.</summary>
    public static string Keys(string desktop, string phone) => On ? phone : desktop;
}

/// <summary>
/// The whole phone screen: the pad's two columns and, between them, the game, drawn from a
/// SubViewport at the screen's own resolution (crisp) but laid out <see cref="Phone.GameHeight"/>
/// pixels tall. Taps on the game go in as mouse clicks (Godot makes them from touches); keys, the
/// pad's included, go in as keys.
/// </summary>
public partial class PhoneFrame : Control
{
    public Node Game;
    public GameView View { get; private set; }
    Pad _pad;

    public override void _Ready()
    {
        Phone.Frame = this;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var win = GetWindow();
        win.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        win.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        win.ContentScaleSize = new Vector2I(960, 540);
        var bg = new ColorRect { Color = Color.FromHtml("#0b0914"), MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        View = new GameView();
        AddChild(View);
        _pad = new Pad { Send = View.Key };
        _pad.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_pad);
        Resized += Layout;
        // The game starts once its viewport has its size: screens lay themselves out from it.
        Callable.From(() =>
        {
            Layout();
            View.Sub.AddChild(Game);
        }).CallDeferred();
    }

    /// <summary>The columns, a third of the screen's height wide (about the width of a thumb's reach), inside any notch.</summary>
    void Layout()
    {
        var size = Size;
        if (size.X <= 0 || size.Y <= 0) return;
        float scale = GetWindow().Size.Y / size.Y; // screen pixels to this frame's units
        float left = 0, right = 0;
        if (OS.HasFeature("android"))
        {
            var safe = DisplayServer.GetDisplaySafeArea();
            var screen = DisplayServer.ScreenGetSize();
            left = Mathf.Max(0, safe.Position.X / scale);
            right = Mathf.Max(0, (screen.X - safe.End.X) / scale);
        }
        float col = Mathf.Clamp(size.Y * 0.3f, 120, 200);
        _pad.Columns = (new Rect2(left, 0, col, size.Y), new Rect2(size.X - right - col, 0, col, size.Y));
        _pad.QueueRedraw();
        View.Position = new Vector2(left + col, 0);
        View.Size = new Vector2(size.X - left - right - 2 * col, size.Y);
        var px = (View.Size * scale).Round();
        View.Sub.Size = new Vector2I((int)px.X, (int)px.Y);
        float zoom = px.Y / Phone.GameHeight;
        View.Sub.Size2DOverride = new Vector2I(Mathf.RoundToInt(px.X / zoom), (int)Phone.GameHeight);
        View.QueueRedraw();
    }

    public override void _Notification(int what)
    {
        // Android's back gesture is B, not a way out of the game.
        if (what == NotificationWMGoBackRequest) View?.Key(Key.Escape, true, false);
        if (what == NotificationWMGoBackRequest) View?.Key(Key.Escape, false, false);
    }
}

/// <summary>The game's SubViewport, drawn to fill this control, with input passed through.</summary>
public partial class GameView : Control
{
    public readonly SubViewport Sub = new()
    {
        RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest,
        Size2DOverrideStretch = true,
        HandleInputLocally = true,
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        AddChild(Sub);
    }

    public override void _Draw() => DrawTextureRect(Sub.GetTexture(), new Rect2(Vector2.Zero, Size), false);

    /// <summary>A key from the pad, straight into the game.</summary>
    public void Key(Key key, bool pressed, bool echo) =>
        Sub.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed, Echo = echo });

    static bool Positional(InputEvent e) => e is InputEventMouse or InputEventScreenTouch or InputEventScreenDrag or InputEventGesture;

    public override void _GuiInput(InputEvent e)
    {
        // The game reads the mouse, which Godot makes from the first finger; the touches themselves stay out.
        if (e is not InputEventMouse) return;
        var ratio = new Vector2(Sub.Size.X / Mathf.Max(1, Size.X), Sub.Size.Y / Mathf.Max(1, Size.Y));
        Sub.PushInput(e.XformedBy(new Transform2D(0, ratio, 0, Vector2.Zero)));
        AcceptEvent();
    }

    public override void _Input(InputEvent e)
    {
        if (Positional(e)) return;
        if (e is InputEventKey { Keycode: Godot.Key.Back } back) { Key(Godot.Key.Escape, back.Pressed, back.Echo); GetViewport().SetInputAsHandled(); return; }
        Sub.PushInput(e);
        GetViewport().SetInputAsHandled();
    }
}

/// <summary>
/// The pad, drawn in the two columns: the D-pad on the left; A, B and Start on the right. Each finger
/// is followed on its own, so a thumb can hold a direction while the other presses A; sliding along
/// the D-pad changes direction. A held direction repeats like a held key. On a desktop the mouse
/// presses it too.
/// </summary>
public partial class Pad : Control
{
    public Action<Key, bool, bool> Send;
    public (Rect2 left, Rect2 right) Columns;

    static readonly Color Ink = Ui.Ink, Dim = Ui.Dim, Gold = Ui.Gold, Edge = new(0.95f, 0.76f, 0.31f, 0.35f);
    static readonly Color Body = Color.FromHtml("#221a36"), Rim = Color.FromHtml("#3d3158"), Lit = Color.FromHtml("#3b2c5c");
    const double Delay = 0.3, Every = 0.085;

    readonly Dictionary<long, Key?> _fingers = new();  // a finger (the mouse is -1) and what it holds
    readonly Dictionary<Key, int> _held = new();
    Key? _repeating;
    double _repeatAt;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    Vector2 DpadCentre => Columns.left.Position + new Vector2(Columns.left.Size.X / 2, Columns.left.Size.Y * 0.6f);
    float Arm => Columns.left.Size.X * 0.42f;
    Vector2 ACentre => Columns.right.Position + new Vector2(Columns.right.Size.X * 0.7f, Columns.right.Size.Y * 0.55f);
    Vector2 BCentre => Columns.right.Position + new Vector2(Columns.right.Size.X * 0.3f, Columns.right.Size.Y * 0.55f + Columns.right.Size.X * 0.3f);
    float ButtonR => Columns.right.Size.X * 0.2f;
    Rect2 StartRect => new(Columns.right.Position + new Vector2(Columns.right.Size.X * 0.2f, Columns.right.Size.Y * 0.2f), new Vector2(Columns.right.Size.X * 0.6f, Columns.right.Size.X * 0.2f));

    /// <summary>What a press at this point holds: a direction anywhere in the left column (by the angle from the D-pad's middle), or a button.</summary>
    Key? KeyAt(Vector2 p, Key? was = null)
    {
        if (Columns.left.Grow(8).HasPoint(p) || was is Key.Up or Key.Down or Key.Left or Key.Right && p.X < Columns.left.End.X + 40)
        {
            var d = p - DpadCentre;
            if (d.Length() < Arm * 0.18f) return was is Key.Up or Key.Down or Key.Left or Key.Right ? was : null;
            if (d.Length() > Arm * 2.2f && was == null) return null;
            return Mathf.Abs(d.X) > Mathf.Abs(d.Y) ? (d.X > 0 ? Key.Right : Key.Left) : (d.Y > 0 ? Key.Down : Key.Up);
        }
        if (!Columns.right.Grow(8).HasPoint(p)) return null;
        if (StartRect.Grow(14).HasPoint(p)) return Phone.Start;
        float da = p.DistanceTo(ACentre), db = p.DistanceTo(BCentre);
        if (Mathf.Min(da, db) > ButtonR * 1.7f) return null;
        return da <= db ? Key.Enter : Key.Escape;
    }

    void Hold(long finger, Key? key)
    {
        _fingers.TryGetValue(finger, out var old);
        if (old == key) return;
        if (old is { } o) Up(o);
        _fingers[finger] = key;
        if (key is { } k) Down(k);
        QueueRedraw();
    }

    void Let(long finger)
    {
        if (_fingers.Remove(finger, out var old) && old is { } o) Up(o);
        QueueRedraw();
    }

    void Down(Key k)
    {
        _held[k] = _held.GetValueOrDefault(k) + 1;
        if (_held[k] > 1) return;
        Send?.Invoke(k, true, false);
        if (k is Key.Up or Key.Down or Key.Left or Key.Right) { _repeating = k; _repeatAt = Time.GetTicksMsec() / 1000.0 + Delay; }
    }

    void Up(Key k)
    {
        if (!_held.TryGetValue(k, out int n)) return;
        if (n > 1) { _held[k] = n - 1; return; }
        _held.Remove(k);
        if (_repeating == k) _repeating = null;
        Send?.Invoke(k, false, false);
    }

    public override void _Process(double delta)
    {
        if (_repeating is not { } k) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _repeatAt) return;
        _repeatAt = now + Every;
        Send?.Invoke(k, true, true);
    }

    public override void _Input(InputEvent e)
    {
        switch (e)
        {
            case InputEventScreenTouch touch when touch.Pressed:
                if (KeyAt(touch.Position) is { } k) { Hold(touch.Index, k); GetViewport().SetInputAsHandled(); }
                break;
            case InputEventScreenTouch touch:
                if (_fingers.ContainsKey(touch.Index)) { Let(touch.Index); GetViewport().SetInputAsHandled(); }
                break;
            case InputEventScreenDrag drag when _fingers.TryGetValue(drag.Index, out var held):
                Hold(drag.Index, KeyAt(drag.Position, held));
                GetViewport().SetInputAsHandled();
                break;
            // The mouse presses it on a desktop (Android's own mouse events are made from touches, already counted).
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb when mb.Device != InputEvent.DeviceIdEmulation:
                if (mb.Pressed && KeyAt(mb.Position) is { } mk) { Hold(-1, mk); GetViewport().SetInputAsHandled(); }
                else if (!mb.Pressed && _fingers.ContainsKey(-1)) { Let(-1); GetViewport().SetInputAsHandled(); }
                break;
            case InputEventMouseMotion mm when mm.Device != InputEvent.DeviceIdEmulation && _fingers.TryGetValue(-1, out var mheld):
                Hold(-1, KeyAt(mm.Position, mheld));
                break;
        }
    }

    bool Pressed(Key k) => _held.ContainsKey(k);

    public override void _Draw()
    {
        if (Columns.left.Size.X <= 0) return; // not laid out yet
        var font = GetThemeDefaultFont();
        foreach (var (col, edgeX) in new[] { (Columns.left, Columns.left.End.X), (Columns.right, Columns.right.Position.X) })
        {
            DrawRect(col, Color.FromHtml("#0b0914"));
            DrawLine(new Vector2(edgeX, 0), new Vector2(edgeX, col.Size.Y), Edge, 1);
        }

        // The D-pad: a cross with a dent in the middle and an arrow on each arm.
        var c = DpadCentre;
        float arm = Arm, w = arm * 0.62f;
        DrawCircle(c + new Vector2(0, 3), arm * 1.12f, new Color(0, 0, 0, 0.35f));
        foreach (var r in new[] { new Rect2(c.X - w / 2, c.Y - arm, w, arm * 2), new Rect2(c.X - arm, c.Y - w / 2, arm * 2, w) })
        {
            var box = new StyleBoxFlat { BgColor = Body, BorderColor = Rim };
            box.SetBorderWidthAll(2);
            box.SetCornerRadiusAll(6);
            DrawStyleBox(box, r);
        }
        DrawRect(new Rect2(c.X - w / 2 + 2, c.Y - w / 2 + 2, w - 4, w - 4), Body);
        foreach (var (key, dir) in new[] { (Key.Up, Vector2.Up), (Key.Down, Vector2.Down), (Key.Left, Vector2.Left), (Key.Right, Vector2.Right) })
        {
            var tip = c + dir * arm * 0.78f;
            var side = new Vector2(-dir.Y, dir.X) * w * 0.26f;
            var back = tip - dir * w * 0.36f;
            if (Pressed(key))
            {
                var lit = new Rect2(c + dir * w * 0.5f - new Vector2(Mathf.Abs(dir.Y), Mathf.Abs(dir.X)) * (w / 2 - 3), Vector2.Zero)
                    .Expand(c + dir * (arm - 3) + new Vector2(Mathf.Abs(dir.Y), Mathf.Abs(dir.X)) * (w / 2 - 3));
                DrawRect(lit, Lit);
            }
            DrawColoredPolygon(new[] { tip, back + side, back - side }, Pressed(key) ? Gold : Dim);
        }
        DrawCircle(c, w * 0.18f, new Color(0, 0, 0, 0.3f));

        // A and B, Game Boy fashion (B low on the left, A high on the right), with what each does under them.
        void Round(Vector2 at, string letter, string word, Key key, Color tint)
        {
            float r = ButtonR;
            DrawCircle(at + new Vector2(0, 3), r, new Color(0, 0, 0, 0.35f));
            DrawCircle(at, r, Pressed(key) ? tint : Body);
            DrawArc(at, r, 0, Mathf.Tau, 40, Pressed(key) ? Ink : tint, 2, true);
            int size = Mathf.RoundToInt(r * 0.95f);
            DrawString(font, at + new Vector2(-r, size * 0.36f), letter, HorizontalAlignment.Center, r * 2, size, Pressed(key) ? Color.FromHtml("#1a1030") : Ink);
            DrawString(font, at + new Vector2(-r * 1.6f, r + 16), word, HorizontalAlignment.Center, r * 3.2f, 11, Dim);
        }
        Round(ACentre, "A", "select", Key.Enter, Gold);
        Round(BCentre, "B", "cancel", Key.Escape, Color.FromHtml("#a98bff"));

        // Start: a pill above them.
        var s = StartRect;
        var pill = new StyleBoxFlat { BgColor = Pressed(Phone.Start) ? Gold : Body, BorderColor = Pressed(Phone.Start) ? Ink : Rim };
        pill.SetBorderWidthAll(2);
        pill.SetCornerRadiusAll((int)(s.Size.Y / 2));
        DrawStyleBox(pill, s);
        DrawString(font, new Vector2(s.Position.X, s.Position.Y + s.Size.Y * 0.68f), "START", HorizontalAlignment.Center, s.Size.X, Mathf.RoundToInt(s.Size.Y * 0.5f),
            Pressed(Phone.Start) ? Color.FromHtml("#1a1030") : Ink);
        DrawString(font, new Vector2(s.Position.X - 10, s.End.Y + 16), "menu", HorizontalAlignment.Center, s.Size.X + 20, 11, Dim);
    }
}
