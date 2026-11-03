using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The pad's way round the menus, on a phone. Godot's own focus does most of it: the arrows move it
/// between controls and A presses the focused one. This adds what that lacks: every button takes the
/// focus (some refuse it on a desktop, where Enter and Space are shortcuts), scroll boxes follow it, a
/// gold frame shows it, and when nothing has it the first arrow or A puts it on the frontmost layer's
/// first control (or one a screen marked with <see cref="First"/>). The arrows move the focus to the
/// nearest control that way on the whole screen (on the same layer): Godot's own search stays inside a
/// scroll box, which left the pad unable to get from a roster to the buttons beside it. And since a
/// phone has no hover and the full words live in tooltips, whatever has the focus, or is tapped,
/// shows its tooltip beside it.
/// </summary>
public partial class PadNav : Node
{
    /// <summary>Marks the control the pad should land on first when nothing has the focus.</summary>
    public static void First(Control c) => c.SetMeta("pad_first", true);

    CanvasLayer _layer;
    PanelContainer _tip;
    Label _text;
    Control _for;
    double _hideAt;

    /// <summary>A meta on a button that should stay tap-only on the phone (the update strip's): set before it joins the tree.</summary>
    public const string Skip = "pad_skip";

    /// <summary>Phone-only touches to the whole UI, as each control joins the tree.</summary>
    public static void Adapt(Node node)
    {
        if (node.HasMeta(Skip)) return;
        if (node is BaseButton b && b.FocusMode == Control.FocusModeEnum.None) b.FocusMode = Control.FocusModeEnum.All;
        if (node is ScrollContainer s) s.FollowFocus = true;
    }

    /// <summary>A gold frame round whatever the pad is on.</summary>
    public static void Theme()
    {
        var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = Ui.Gold };
        focus.SetBorderWidthAll(3);
        focus.SetCornerRadiusAll(6);
        focus.SetExpandMarginAll(2);
        var theme = ThemeDB.GetDefaultTheme();
        foreach (var type in new[] { "Button", "CheckButton", "CheckBox", "OptionButton", "MenuButton", "LineEdit", "LinkButton" })
            theme.SetStylebox("focus", type, focus);
    }

    public override void _Ready()
    {
        _layer = new CanvasLayer { Layer = 60 };
        AddChild(_layer);
        _tip = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _tip.AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Tip, UiState.Normal));
        _text = Ui.Text("", 17, Ui.Ink, 4);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tip.AddChild(_text);
        _layer.AddChild(_tip);
        GetViewport().GuiFocusChanged += Show;
    }

    void Show(Control c)
    {
        string words = c?.TooltipText;
        if (string.IsNullOrEmpty(words)) { Hide(); return; }
        _for = c;
        _text.Text = words;
        var font = _text.GetThemeFont("font");
        float wide = font.GetMultilineStringSize(words, HorizontalAlignment.Left, -1, 17).X + 6;
        _text.CustomMinimumSize = new Vector2(Mathf.Min(420, wide), 0);
        _tip.Size = Vector2.Zero;
        _tip.Visible = true;
        _hideAt = Time.GetTicksMsec() / 1000.0 + 6;
        Place();
    }

    void Hide()
    {
        _tip.Visible = false;
        _for = null;
    }

    /// <summary>Beside the control: above it near the foot of the screen, below it near the top, else to the side with room.</summary>
    void Place()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        var r = _for.GetGlobalRect();
        var size = _tip.GetCombinedMinimumSize();
        Vector2 at;
        if (r.Position.Y > vp.Y * 0.6f) at = new Vector2(r.GetCenter().X - size.X / 2, r.Position.Y - size.Y - 8);
        else if (r.End.Y < vp.Y * 0.3f) at = new Vector2(r.GetCenter().X - size.X / 2, r.End.Y + 8);
        else at = new Vector2(r.GetCenter().X < vp.X / 2 ? r.End.X + 10 : r.Position.X - size.X - 10, r.Position.Y);
        _tip.Position = new Vector2(Mathf.Clamp(at.X, 6, Mathf.Max(6, vp.X - size.X - 6)), Mathf.Clamp(at.Y, 6, Mathf.Max(6, vp.Y - size.Y - 6)));
        _tip.Size = size;
    }

    public override void _Process(double delta)
    {
        if (!_tip.Visible) return;
        if (_for == null || !IsInstanceValid(_for) || !_for.IsVisibleInTree() || Time.GetTicksMsec() / 1000.0 > _hideAt) { Hide(); return; }
        Place();
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } arrow && arrow.Keycode is Key.Up or Key.Down or Key.Left or Key.Right
            && GetViewport().GuiGetFocusOwner() is { } from && from.IsVisibleInTree()
            // A slider takes left and right, and a text box moves its caret with them.
            && !(from is Godot.Range or LineEdit && arrow.Keycode is Key.Left or Key.Right)
            && Toward(from, arrow.Keycode) is { } to)
        {
            to.GrabFocus();
            GetViewport().SetInputAsHandled();
            return;
        }
        // A tap on something with a tooltip shows it (a focused button's already shows).
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb && Under(mb.Position) is { } c && c != _for) Show(c);
        else if (e is InputEventKey { Pressed: true } && _for != null && !_for.HasFocus()) Hide();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Nothing focused and the screen didn't want the key: put the focus somewhere to start from.
        if (e is not InputEventKey { Pressed: true } k || k.Keycode is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter)) return;
        var vp = GetViewport();
        if (vp.GuiGetFocusOwner() is { } f && f.IsVisibleInTree()) return;
        if (Start() is not { } first) return;
        first.GrabFocus();
        vp.SetInputAsHandled();
    }

    static int Layer(Control c) => c.GetCanvasLayerNode()?.Layer ?? 0;

    IEnumerable<Control> Controls() => All(GetViewport()).Where(c => c.IsVisibleInTree() && !_tip.IsAncestorOf(c) && c != _tip);

    static IEnumerable<Control> All(Node n)
    {
        foreach (var child in n.GetChildren())
        {
            if (child is Control c) yield return c;
            foreach (var d in All(child)) yield return d;
        }
    }

    /// <summary>The control to start from: on the frontmost layer that has any, the one marked first, else the top-left one.</summary>
    Control Start()
    {
        var view = GetViewport().GetVisibleRect();
        var open = Controls().Where(c => c.FocusMode == Control.FocusModeEnum.All && !(c is BaseButton { Disabled: true }) && view.Intersects(c.GetGlobalRect())).ToList();
        if (open.Count == 0) return null;
        int top = open.Max(Layer);
        var front = open.Where(c => Layer(c) == top).ToList();
        return front.FirstOrDefault(c => c.HasMeta("pad_first"))
            ?? front.OrderBy(c => Mathf.Round(c.GetGlobalRect().Position.Y / 12)).ThenBy(c => c.GetGlobalRect().Position.X).First();
    }

    /// <summary>
    /// The control the pad moves to from one, that way: on the same layer, on the screen (or further
    /// along the same scroll box), nearest by the gap that way plus twice the sideways offset.
    /// </summary>
    Control Toward(Control from, Key key)
    {
        var dir = key switch { Key.Up => Vector2.Up, Key.Down => Vector2.Down, Key.Left => Vector2.Left, _ => Vector2.Right };
        var view = GetViewport().GetVisibleRect();
        var a = from.GetGlobalRect();
        int layer = Layer(from);
        Control best = null;
        float bestScore = float.MaxValue;
        foreach (var c in Controls())
        {
            if (c == from || c.FocusMode != Control.FocusModeEnum.All || c is BaseButton { Disabled: true } || Layer(c) != layer || from.IsAncestorOf(c) || c.IsAncestorOf(from)) continue;
            var b = c.GetGlobalRect();
            if (!Shown(c, b, from, view)) continue;
            // How far it lies that way (it must start past the middle of where the focus is), and how far off to the side.
            float ahead = dir.X != 0 ? (dir.X > 0 ? b.Position.X - a.End.X : a.Position.X - b.End.X) : (dir.Y > 0 ? b.Position.Y - a.End.Y : a.Position.Y - b.End.Y);
            bool past = dir.X > 0 ? b.GetCenter().X > a.GetCenter().X + 1 : dir.X < 0 ? b.GetCenter().X < a.GetCenter().X - 1
                : dir.Y > 0 ? b.GetCenter().Y > a.GetCenter().Y + 1 : b.GetCenter().Y < a.GetCenter().Y - 1;
            if (!past) continue;
            float side = dir.X != 0 ? Mathf.Max(0, Mathf.Max(b.Position.Y - a.End.Y, a.Position.Y - b.End.Y)) : Mathf.Max(0, Mathf.Max(b.Position.X - a.End.X, a.Position.X - b.End.X));
            float score = Mathf.Max(0, ahead) + 2 * side;
            if (score < bestScore) { best = c; bestScore = score; }
        }
        return best;
    }

    /// <summary>On the screen, and not scrolled out of sight in a scroll box (unless it's further along the focused control's own).</summary>
    static bool Shown(Control c, Rect2 rect, Control from, Rect2 view)
    {
        bool sameList = false;
        for (Node n = c.GetParent(); n != null; n = n.GetParent())
            if (n is ScrollContainer s)
            {
                if (s.IsAncestorOf(from)) { sameList = true; break; }
                if (!s.GetGlobalRect().Intersects(rect)) return false;
            }
        return sameList || view.Intersects(rect);
    }

    /// <summary>The frontmost control with a tooltip under a point.</summary>
    Control Under(Vector2 p)
    {
        Control best = null;
        int bestLayer = int.MinValue;
        foreach (var c in Controls())
        {
            if (c.MouseFilter == Control.MouseFilterEnum.Ignore || string.IsNullOrEmpty(c.TooltipText) || !c.GetGlobalRect().HasPoint(p)) continue;
            int layer = Layer(c);
            if (layer >= bestLayer) { best = c; bestLayer = layer; } // later in the tree draws on top
        }
        return best;
    }
}
