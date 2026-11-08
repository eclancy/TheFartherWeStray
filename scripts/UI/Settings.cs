using System;
using System.Collections.Generic;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The player's settings (the title screen's and the pause menu's), kept in user://settings.cfg and
/// applied as the game starts: music and effects volume, mute, how fast cutscene lines type out, the
/// battle turn timer, fullscreen, the menus' look, the save slot played last, and how often each key hint has shown
/// (<see cref="Hints"/>).
/// </summary>
public static class Settings
{
    public static string Path = "user://settings.cfg";
    public static float Music = 0.8f, Effects = 0.9f;
    public static bool Muted, Fullscreen;
    public static int TextSpeed = 1;          // index into Speeds
    public static int TurnTimer;              // seconds a turn in battle; 0 for none
    public static int LastSlot = 1;
    public static Skin Look = Skin.Dusk;      // the menus' look (UiSkin): Dusk, or another of Looks

    public static readonly string[] Speeds = { "Slow", "Normal", "Fast", "Instant" };

    /// <summary>The menu looks a player can pick (the user's call, 2026-11-07: Dusk, the others as options).</summary>
    public static readonly Skin[] Looks = { Skin.Dusk, Skin.Lamplight, Skin.Verdigris, Skin.GiltNeon };

    /// <summary>A look's name in the settings.</summary>
    public static string LookName(Skin s) => s switch
    {
        Skin.Lamplight => "Lamplight",
        Skin.Verdigris => "Verdigris",
        Skin.GiltNeon => "Gilt and neon",
        _ => "Dusk",
    };
    public static readonly int[] Timers = { 0, 15, 30, 60 };

    /// <summary>Cutscene lines type out at this many characters a second.</summary>
    public static float CharsPerSecond => TextSpeed switch { 0 => 30, 1 => 55, 2 => 110, _ => 100000 };

    public static void Load()
    {
        var cf = new ConfigFile();
        if (cf.Load(Path) == Error.Ok)
        {
            Music = (float)cf.GetValue("audio", "music", Music);
            Effects = (float)cf.GetValue("audio", "effects", Effects);
            Muted = (bool)cf.GetValue("audio", "muted", Muted);
            TextSpeed = Math.Clamp((int)cf.GetValue("game", "text_speed", TextSpeed), 0, Speeds.Length - 1);
            TurnTimer = (int)cf.GetValue("game", "turn_timer", TurnTimer);
            Fullscreen = (bool)cf.GetValue("display", "fullscreen", Fullscreen);
            if (UiSkin.TryParse((string)cf.GetValue("display", "look", Look.ToString()), out var look) && Array.IndexOf(Looks, look) >= 0) Look = look;
            LastSlot = Math.Clamp((int)cf.GetValue("game", "last_slot", LastSlot), 1, Ranching.Ranch.Slots);
            Hints.Seen.Clear();
            if (cf.HasSection("hints"))
                foreach (var id in cf.GetSectionKeys("hints")) Hints.Seen[id] = (int)cf.GetValue("hints", id, 0);
        }
        Apply();
    }

    public static void Save()
    {
        var cf = new ConfigFile();
        cf.SetValue("audio", "music", Music);
        cf.SetValue("audio", "effects", Effects);
        cf.SetValue("audio", "muted", Muted);
        cf.SetValue("game", "text_speed", TextSpeed);
        cf.SetValue("game", "turn_timer", TurnTimer);
        cf.SetValue("game", "last_slot", LastSlot);
        cf.SetValue("display", "fullscreen", Fullscreen);
        cf.SetValue("display", "look", Look.ToString());
        foreach (var (id, n) in Hints.Seen) cf.SetValue("hints", id, n);
        cf.Save(Path);
    }

    /// <summary>Puts the settings into effect: volumes (music sits under the effects), mute, the window.</summary>
    public static void Apply()
    {
        Audio.Sound.VolumeDb = Effects <= 0.001f ? -80 : -4 + Mathf.LinearToDb(Effects);
        Audio.Music.Db = Music <= 0.001f ? -80 : -10 + Mathf.LinearToDb(Music);
        Audio.Sound.Muted = Muted;
        if (DisplayServer.GetName() != "headless" && !Phone.On)
            DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
    }
}

/// <summary>
/// The settings, as a panel: sliders and switches that take effect and save as they change. A new menu look
/// goes on at once: the panel paints itself again in it, and the screen behind is made again as the panel
/// closes (<see cref="UiSkin.Restyle"/>; a battle or a scene keeps its own panels until it ends).
/// </summary>
public partial class SettingsPanel : PanelContainer
{
    public Action Closed;

    bool _restyled;

    public override void _Ready() => Build(false);

    void Build(bool onLook)
    {
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        AddThemeStyleboxOverride("panel", UiSkin.Box(UiPart.Panel, UiState.Normal));
        CustomMinimumSize = new Vector2(640, 0);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        AddChild(col);
        col.AddChild(Ui.Text("SETTINGS", 24, Ui.Gold, 6));

        Slider(col, "sound", "Music", Settings.Music, v => Settings.Music = v);
        Slider(col, "sound", "Effects", Settings.Effects, v => { Settings.Effects = v; Audio.Sound.Play("tick"); });
        Toggle(col, "x", Phone.On ? "Mute everything" : "Mute everything (N)", Settings.Muted, v => Settings.Muted = v);
        Cycle(col, "clock", "Cutscene text", () => Settings.Speeds[Settings.TextSpeed], () => Settings.TextSpeed = (Settings.TextSpeed + 1) % Settings.Speeds.Length);
        Cycle(col, "cooldown", "Battle turn timer", () => Settings.TurnTimer == 0 ? "Off" : $"{Settings.TurnTimer} s",
            () => Settings.TurnTimer = Settings.Timers[(Array.IndexOf(Settings.Timers, Settings.TurnTimer) + 1) % Settings.Timers.Length]);
        var look = Cycle(col, "sparkle", "Menu look", () => Settings.LookName(Settings.Look), NextLook,
            "How the menus look: Dusk, Lamplight (warm), Verdigris (teal) or Gilt and neon (the first look). A battle or a scene takes it once it ends.");
        if (!Phone.On) Toggle(col, "area", "Fullscreen", Settings.Fullscreen, v => Settings.Fullscreen = v);

        var done = new Button { Text = "Done", CustomMinimumSize = new Vector2(200, 46) };
        done.AddThemeFontSizeOverride("font_size", 18);
        done.Pressed += () => Closed?.Invoke();
        col.AddChild(done);
        (onLook ? look : done).CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>However the panel closes (Done, Esc, B), a new look makes the screen behind again once it's gone.</summary>
    public override void _ExitTree()
    {
        if (!_restyled) return;
        _restyled = false;
        Callable.From(() => UiSkin.Restyle?.Invoke()).CallDeferred();
    }

    /// <summary>The next menu look: on at once, the panel painted again in it.</summary>
    void NextLook()
    {
        Settings.Look = Settings.Looks[(Array.IndexOf(Settings.Looks, Settings.Look) + 1) % Settings.Looks.Length];
        UiSkin.Use(Settings.Look);
        _restyled = true;
        Settings.Save();
        Callable.From(() => Build(true)).CallDeferred();
    }

    static void Changed()
    {
        Settings.Apply();
        Settings.Save();
    }

    HBoxContainer Row(VBoxContainer col, string glyph, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        row.AddChild(Ui.Picture(Glyphs.Get(glyph, 2), label));
        var l = Ui.Text(label, 19, Ui.Ink, 4);
        l.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(l);
        col.AddChild(row);
        return row;
    }

    void Slider(VBoxContainer col, string glyph, string label, float value, Action<float> set)
    {
        var row = Row(col, glyph, label);
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, Value = value * 100, CustomMinimumSize = new Vector2(260, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var shown = Ui.Text($"{Mathf.RoundToInt(value * 100)}%", 18, Ui.Dim, 4);
        slider.ValueChanged += v => { set((float)v / 100f); shown.Text = $"{Mathf.RoundToInt((float)v)}%"; Changed(); };
        row.AddChild(slider);
        row.AddChild(shown);
    }

    void Toggle(VBoxContainer col, string glyph, string label, bool value, Action<bool> set)
    {
        var row = Row(col, glyph, label);
        var check = new CheckButton { ButtonPressed = value };
        check.Toggled += on => { set(on); Changed(); };
        row.AddChild(check);
    }

    Button Cycle(VBoxContainer col, string glyph, string label, Func<string> shown, Action next, string tip = null)
    {
        var row = Row(col, glyph, label);
        var b = new Button { Text = shown(), CustomMinimumSize = new Vector2(200, 40), TooltipText = tip ?? "" };
        b.AddThemeFontSizeOverride("font_size", 17);
        b.Pressed += () => { next(); b.Text = shown(); Changed(); };
        row.AddChild(b);
        return b;
    }
}
