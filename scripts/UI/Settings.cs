using System;
using System.Collections.Generic;
using Godot;

namespace TheFartherWeStray.UI;

/// <summary>
/// The player's settings (the title screen's and the base's pause menu), kept in user://settings.cfg
/// and applied as the game starts: music and effects volume, mute, how fast cutscene lines type out,
/// the battle turn timer, fullscreen, and the save slot played last.
/// </summary>
public static class Settings
{
    public static string Path = "user://settings.cfg";
    public static float Music = 0.8f, Effects = 0.9f;
    public static bool Muted, Fullscreen;
    public static int TextSpeed = 1;          // index into Speeds
    public static int TurnTimer;              // seconds a turn in battle; 0 for none
    public static int LastSlot = 1;

    public static readonly string[] Speeds = { "Slow", "Normal", "Fast", "Instant" };
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
            LastSlot = Math.Clamp((int)cf.GetValue("game", "last_slot", LastSlot), 1, Ranching.Ranch.Slots);
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

/// <summary>The settings, as a panel: sliders and switches that take effect and save as they change.</summary>
public partial class SettingsPanel : PanelContainer
{
    public Action Closed;

    public override void _Ready()
    {
        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.05f, 0.1f, 0.96f), BorderColor = Ui.Gold };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(24);
        AddThemeStyleboxOverride("panel", style);
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
        if (!Phone.On) Toggle(col, "area", "Fullscreen", Settings.Fullscreen, v => Settings.Fullscreen = v);

        var done = new Button { Text = "Done", CustomMinimumSize = new Vector2(200, 46) };
        done.AddThemeFontSizeOverride("font_size", 18);
        done.Pressed += () => Closed?.Invoke();
        col.AddChild(done);
        done.GrabFocus();
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

    void Cycle(VBoxContainer col, string glyph, string label, Func<string> shown, Action next)
    {
        var row = Row(col, glyph, label);
        var b = new Button { Text = shown(), CustomMinimumSize = new Vector2(160, 40) };
        b.AddThemeFontSizeOverride("font_size", 17);
        b.Pressed += () => { next(); b.Text = shown(); Changed(); };
        row.AddChild(b);
    }
}
