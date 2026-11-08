using System;
using System.Collections.Generic;
using Godot;
using TheFartherWeStray.Ranching;

namespace TheFartherWeStray.UI;

/// <summary>
/// The bar along the top of home, its stations and the map (docs/menus-plan.md, part B): the screen's name, the same
/// counts everywhere (your beasts, the squad, chem, scrap, the day, standing with each side, and titles once there
/// are any), and the way back home wherever there's one. It keeps itself up to date: a screen only places it.
/// At a station with levels, Build up opens them (as the workbench does).
/// </summary>
public partial class TopBar : HBoxContainer
{
    public Ranch Ranch;
    public string Title;
    public Action Back;            // unset at home itself
    public string Station;         // the station this screen is, if it has levels to build: Build up shows
    public Action<string> Built;   // after building a level: what changed, for the screen to say

    static Color Moon => Ui.Magenta;
    Label _title;
    Control _chips;
    Button _back, _build;
    BuildUpPanel _levels;
    string _shown;

    /// <summary>Whether its Build up panel is open (it takes Esc and B itself).</summary>
    public bool Building => _levels != null;

    /// <summary>Where a screen's own things can start below it.</summary>
    public static float Bottom => Phone.On ? 56 : 62;

    public override void _Ready()
    {
        var vp = GetViewportRect().Size;
        float x = Phone.On ? 20 : 32;
        Position = new Vector2(x, Phone.On ? 10 : 16);
        Size = new Vector2(vp.X - 2 * x, 42);
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", Phone.On ? 16 : 24);
        // The title takes the same room on every screen (unless it's longer), so the counts stand still between them.
        _title = Ui.Text("", Phone.On ? 21 : 26, Ui.Ink, 6);
        _title.CustomMinimumSize = new Vector2(Phone.On ? 170 : 290, 0);
        AddChild(_title);
        if (HomeBase.Get(Station) is { Upgrades: not null } st && st.Built(Ranch))
        {
            _build = new Button { Text = "Build up", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(Phone.On ? 110 : 140, 40), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            _build.AddThemeFontSizeOverride("font_size", 16);
            _build.Pressed += () => OpenLevels(st);
            AddChild(_build);
        }
        if (Back != null)
        {
            _back = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(Phone.On ? 150 : 200, 40), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            _back.AddThemeFontSizeOverride("font_size", 16);
            _back.Pressed += () => Back?.Invoke();
            AddChild(_back);
        }
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    /// <summary>Opens the station's Build up (the button, or `--buildup` for screenshots).</summary>
    public void OpenLevels() { if (HomeBase.Get(Station) is { Upgrades: not null } st && st.Built(Ranch)) OpenLevels(st); }

    void OpenLevels(StationDef st)
    {
        if (_levels != null) return;
        _levels = new BuildUpPanel { Ranch = Ranch, Station = st, Built = said => Built?.Invoke(said), Closed = () => _levels = null };
        AddChild(_levels);
    }

    /// <summary>Redraws the counts when one has changed (a purchase, a rename, a new beast).</summary>
    void Refresh()
    {
        int level = Station != null ? Farmyard.Level(Ranch, Station) : 0;
        string now = $"{Title}|{Ranch.Creatures.Count}|{Ranch.Squad.Count}/{Ranch.SquadSize}|{Ranch.Chem}|{Ranch.Scrap}|{Ranch.Day}|"
            + $"{Ranch.CityStanding}|{Ranch.UnderStanding}|{Ranch.Titles.Count}|{World.Country.BackHome}|{level}";
        if (now == _shown) return;
        _shown = now;
        _title.Text = Title ?? "";
        if (_build != null) _build.TooltipText = level >= 3 ? $"Level 3: built up all the way" : $"Level {level} of 3: build it up with chem and scrap";
        if (_back != null) _back.Text = Phone.Keys(World.Country.BackHome, World.Country.BackHome + "  (B)");
        _chips?.QueueFree();
        _chips = Ui.Chips(Counts(Ranch), Phone.On ? 18 : 22, 2, Phone.On ? 14 : 22);
        AddChild(_chips);
        MoveChild(_chips, 1);
    }

    /// <summary>The counts, in order: the same on every screen.</summary>
    public static List<Chip> Counts(Ranch r)
    {
        var chips = new List<Chip>
        {
            new("paw", r.Creatures.Count.ToString(), Ui.Ink, "your beasts"),
            new("fist", $"{r.Squad.Count}/{r.SquadSize}", r.Squad.Count == 0 ? Ui.Bad : Ui.Ink, "the squad, picked at the pens: three beasts for each tamer leading"),
            new("flask", r.Chem.ToString(), Ui.Gold, "chem: won in battle, spent at the shops, the lab and on building up"),
            new("gear", r.Scrap.ToString(), Ui.Ink, "scrap: the scrap press makes it, and building up takes it"),
            new("clock", "day " + r.Day, Ui.Ink, "the day: a battle, or a road travelled, takes one"),
            new("sun", r.CityStanding.ToString(), Ui.Gold, "standing with the city above"),
            new("moon", r.UnderStanding.ToString(), Moon, "standing with the undercity"),
        };
        if (r.Titles.Count > 0) chips.Add(new("sparkle", r.Titles.Count.ToString(), Ui.Gold, "titles won from rival tamers: " + string.Join(", ", r.Titles)));
        return chips;
    }
}
