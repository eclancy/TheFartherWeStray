using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// Every key and button in the game, screen by screen, for the Controls page in the pause menu: a desktop's keys
/// and mouse, and a phone's pad and touch (a phone never shows a keyboard key). New screens and keys go here.
/// </summary>
public static class ControlsBook
{
    public record Section(string Id, string Title, (string Key, string What)[] Desk, (string Key, string What)[] Pad);

    public static readonly Section[] Sections =
    {
        new("all", "Everywhere",
            new[]
            {
                ("Esc", "the menu (it closes an open panel, or stops aiming, first)"),
                ("Click", "buttons, beasts, places and tiles"),
                ("N", "sound off or on"),
            },
            new[]
            {
                ("Start", "the menu"),
                ("A or tap", "select"),
                ("B", "back, or cancel"),
                ("D-pad", "move round a screen's buttons"),
            }),
        new("base", "Home",
            new[]
            {
                ("Click a tile", "walk there"),
                ("WASD or arrows", "walk"),
                ("Click a station", "walk there and use it"),
                ("E beside a station", "use it (Enter and Space too)"),
                ("E beside a machine", "open it: collect, or upgrade"),
                ("Click a beast, or E beside it", "say hello"),
            },
            new[]
            {
                ("D-pad or tap", "walk"),
                ("Tap a station", "walk there and use it"),
                ("A beside a station", "use it"),
                ("A beside a machine", "open it: collect, or upgrade"),
                ("A beside a beast, or tap it", "say hello"),
            }),
        new("build", "The workbench",
            new[]
            {
                ("Click a piece, then a tile", "put it there"),
                ("Click a piece in the yard", "pick it up"),
                ("WASD or arrows", "move the cursor"),
                ("E or Enter", "put down or pick up; on a station, its levels"),
                ("Esc or right-click", "put the piece back, then leave the workbench"),
            },
            new[]
            {
                ("D-pad", "move the cursor"),
                ("A", "put down or pick up; on an empty tile, the shed; on a station, its levels"),
                ("B", "put the piece back, then leave the workbench"),
            }),
        new("station", "Stations",
            new[]
            {
                ("Click a beast", "select it"),
                ("S", "in or out of the squad (the pens)"),
                ("Y", "out in the yard, or back in (the pens)"),
                ("Delete, twice", "let a beast go (the pens)"),
                ("Right-click a beast", "into a vat, or out (the vats)"),
                ("Enter", "breed (the vats)"),
                ("Build up", "the button, top right: the station's levels"),
                ("Back home", "the button, top right"),
            },
            new[]
            {
                ("A or tap a beast", "select it"),
                ("The buttons", "squad, the yard, name, release, vats, breed, gear; Build up, at the top"),
                ("B", "back home"),
            }),
        new("map", "The map",
            new[]
            {
                ("Click a place, or arrows", "pick it"),
                ("Click a mission", "pick it"),
                ("F", "depart: travel there and start it"),
                ("S", "the place's shop"),
                ("Back home", "the button, top right"),
            },
            new[]
            {
                ("D-pad", "pick a place"),
                ("A", "into its missions; on a mission, to Depart"),
                ("B", "back out, then home"),
            }),
        new("battle", "Battle",
            new[]
            {
                ("Click a tile", "move there"),
                ("Click a red-marked foe", "attack it (or the Attack slot: once to aim, again to strike)"),
                ("1 to 9", "aim a skill; click a tile to use it"),
                ("B", "set down a glowberry"),
                ("Right-click or Backspace", "undo the move"),
                ("Right-click or Esc", "stop aiming"),
                ("Space", "end the turn"),
                ("Arrows, Enter", "move the cursor, act on it"),
                ("WASD, middle-drag", "look round"),
                ("Wheel", "zoom"),
            },
            new[]
            {
                ("D-pad", "move the cursor"),
                ("A", "act on it; on your own beast, the command bar"),
                ("Tap", "move the cursor; tap it again to act"),
                ("Drag", "look round"),
                ("B", "stop aiming, or undo the move"),
            }),
        new("scene", "Scenes",
            new[]
            {
                ("Enter, Space or click", "the next line"),
                ("Esc", "the menu: skip the scene there"),
            },
            new[]
            {
                ("A, B or tap", "the next line"),
                ("Start", "the menu: skip the scene there"),
            }),
    };

    /// <summary>The screen you're on first, then what works everywhere, then the rest.</summary>
    public static IEnumerable<Section> For(string first) =>
        Sections.OrderBy(s => s.Id == first ? 0 : s.Id == "all" ? 1 : 2);
}

/// <summary>The Controls page: every screen's keys (or the pad's buttons), the one you're on first. Up and down scroll it.</summary>
public partial class ControlsPanel : PanelContainer
{
    public string First;
    public Action Closed;
    ScrollContainer _scroll;

    /// <summary>Where it sits: centred, as tall as the screen allows.</summary>
    public static Rect2 Frame(Vector2 vp)
    {
        float w = Mathf.Min(900, vp.X - 40), h = vp.Y - 40;
        return new Rect2(vp.X / 2 - w / 2, 20, w, h);
    }

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        AddChild(col);
        col.AddChild(Ui.Text("CONTROLS", 24, Ui.Gold, 6));
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(_scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(list);
        float keyW = Phone.On ? 250 : 300, whatW = Size.X - keyW - 90;
        bool firstSection = true;
        foreach (var s in ControlsBook.For(First))
        {
            if (!firstSection) list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
            firstSection = false;
            list.AddChild(Ui.Text(s.Title.ToUpperInvariant(), 18, Ui.Gold, 5));
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 18);
            grid.AddThemeConstantOverride("v_separation", 4);
            foreach (var (key, what) in Phone.On ? s.Pad : s.Desk)
            {
                var k = Ui.Text(key, 17, Ui.Ink, 4);
                k.CustomMinimumSize = new Vector2(keyW, 0);
                k.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                var w = Ui.Text(what, 17, Ui.Dim, 4);
                w.CustomMinimumSize = new Vector2(whatW, 0);
                w.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                grid.AddChild(k);
                grid.AddChild(w);
            }
            list.AddChild(grid);
        }
        var done = new Button { Text = Phone.Keys("Done  (Esc)", "Done  (B)"), CustomMinimumSize = new Vector2(200, 46), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        done.AddThemeFontSizeOverride("font_size", 18);
        done.Pressed += () => Closed?.Invoke();
        col.AddChild(done);
        done.GrabFocus();
    }

    public override void _Input(InputEvent e)
    {
        // Up and down scroll the list (the pad's D-pad too: the only button here is Done).
        if (e is not InputEventKey { Pressed: true } k || k.Keycode is not (Key.Up or Key.Down)) return;
        _scroll.ScrollVertical += k.Keycode == Key.Down ? 90 : -90;
        GetViewport().SetInputAsHandled();
    }
}

/// <summary>
/// A screen's key hint shows the first few times the screen opens (Times); after that the Controls page in the
/// menu has them. Counted for the player in settings.cfg, never by a run with flags: tests and screenshots see
/// every hint and count none, and --nohints shows none.
/// </summary>
public static class Hints
{
    public const int Times = 3;
    public static bool Counting = true, Off;
    public static readonly Dictionary<string, int> Seen = new();

    /// <summary>Whether the hint with this id shows this time (and counts it).</summary>
    public static bool Show(string id)
    {
        if (Off) return false;
        int n = Seen.GetValueOrDefault(id);
        if (n >= Times) return false;
        if (Counting)
        {
            Seen[id] = n + 1;
            Settings.Save();
        }
        return true;
    }

    /// <summary>How every hint ends: where the rest of the keys are.</summary>
    public static string Menu => Phone.Keys("Esc: menu and controls", "Start: menu and controls");
}
