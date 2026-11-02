using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using Godot;
using static CreatureTactics.Audio.SfxSynth;

namespace CreatureTactics.Audio;

/// <summary>
/// Every sound in the game, as voices for <see cref="SfxSynth"/> (begun 2026-11-02 at the user's
/// request). Content is data, like skills: a new sound is a new entry. Skill themes have their own
/// ("fx_ember", "fx_frost"...), so a new skill sounds right through its icon, as its effect does.
/// </summary>
public static class SoundBook
{
    static Voice Sq(float at, float len, float f, float vol = 0.16f, float duty = 0.25f) => new() { Wave = Wave.Square, At = at, Len = len, Freq = f, Vol = vol, Duty = duty };
    static Voice Tri(float at, float len, float f, float vol = 0.18f) => new() { Wave = Wave.Triangle, At = at, Len = len, Freq = f, Vol = vol };
    static Voice Sin(float at, float len, float f, float vol = 0.2f) => new() { Wave = Wave.Sine, At = at, Len = len, Freq = f, Vol = vol };

    /// <summary>A run of notes, one after another.</summary>
    static IEnumerable<Voice> Notes(float at, float step, float len, string notes, System.Func<float, float, float, Voice> voice) =>
        notes.Split(' ').Select((n, i) => voice(at + i * step, len, N(n)));

    public static readonly Dictionary<string, Voice[]> All = new()
    {
        // Menus.
        ["click"] = new[] { new Voice { Wave = Wave.Square, Len = 0.035f, Freq = 1320, FreqEnd = 1560, Vol = 0.12f, Duty = 0.25f, Decay = 2 } },
        ["confirm"] = Notes(0, 0.06f, 0.08f, "E6 B6", (a, l, f) => Sq(a, l, f, 0.13f)).ToArray(),
        ["cancel"] = Notes(0, 0.06f, 0.08f, "B5 E5", (a, l, f) => Sq(a, l, f, 0.13f)).ToArray(),
        ["depart"] = Notes(0, 0.07f, 0.12f, "C5 E5 G5 C6", (a, l, f) => Sq(a, l, f, 0.13f)).Append(Tri(0.21f, 0.35f, N("C5"), 0.16f)).ToArray(),
        ["tick"] = new[] { new Voice { Wave = Wave.Triangle, Len = 0.03f, Freq = 2000, Vol = 0.12f, Decay = 2 } },
        ["hatch"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.06f, Freq = 9000, Cut = 0.6f, CutEnd = 0.1f, Vol = 0.3f, Decay = 2.5f },
            new Voice { Wave = Wave.Noise, At = 0.09f, Len = 0.05f, Freq = 9000, Cut = 0.5f, CutEnd = 0.1f, Vol = 0.25f, Decay = 2.5f },
        }.Concat(Notes(0.18f, 0.08f, 0.12f, "G5 C6 E6 G6", (a, l, f) => Tri(a, l, f, 0.16f))).ToArray(),

        // Battle.
        ["turn"] = new[] { Tri(0, 0.12f, N("A5"), 0.14f), Tri(0.05f, 0.14f, N("E6"), 0.1f) },
        ["step"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.045f, Freq = 3000, Cut = 0.25f, CutEnd = 0.05f, Vol = 0.2f, Decay = 2.5f },
            new Voice { Wave = Wave.Sine, Len = 0.04f, Freq = 120, FreqEnd = 70, Vol = 0.2f, Decay = 2 },
        },
        ["flap"] = new[] { new Voice { Wave = Wave.Noise, Len = 0.1f, Freq = 6000, Cut = 0.06f, CutEnd = 0.3f, Vol = 0.4f, Attack = 0.03f, Decay = 1.2f } },
        ["swing"] = new[] { new Voice { Wave = Wave.Noise, Len = 0.14f, Freq = 12000, Cut = 0.05f, CutEnd = 0.45f, Vol = 0.45f, Attack = 0.04f, Decay = 1.4f } },
        ["hit"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.1f, Freq = 9000, Cut = 0.7f, CutEnd = 0.1f, Vol = 0.4f, Decay = 2.2f },
            new Voice { Wave = Wave.Square, Len = 0.12f, Freq = 190, FreqEnd = 55, Vol = 0.3f, Cut = 0.3f, Decay = 2 },
        },
        ["miss"] = new[] { new Voice { Wave = Wave.Noise, Len = 0.2f, Freq = 10000, Cut = 0.03f, CutEnd = 0.25f, Vol = 0.45f, Attack = 0.05f, Decay = 1.2f } },
        ["knock"] = new[]
        {
            new Voice { Wave = Wave.Sine, Len = 0.18f, Freq = 140, FreqEnd = 50, Vol = 0.45f, Decay = 2 },
            new Voice { Wave = Wave.Noise, Len = 0.08f, Freq = 4000, Cut = 0.3f, CutEnd = 0.05f, Vol = 0.25f },
        },
        ["faint"] = new[]
        {
            new Voice { Wave = Wave.Saw, Len = 0.5f, Freq = 420, FreqEnd = 70, Vol = 0.22f, Cut = 0.4f, CutEnd = 0.1f, Vibrato = 0.03f, VibratoHz = 9, Decay = 1.2f },
            new Voice { Wave = Wave.Noise, At = 0.05f, Len = 0.35f, Freq = 3000, Cut = 0.1f, CutEnd = 0.02f, Vol = 0.14f },
        },
        ["recruit"] = Notes(0, 0.07f, 0.1f, "C5 E5 G5 C6", (a, l, f) => Sq(a, l, f, 0.14f))
            .Append(Tri(0.21f, 0.4f, N("C6"), 0.14f))
            .Append(new Voice { Wave = Wave.Sine, At = 0.25f, Len = 0.3f, Freq = N("C7"), Vol = 0.07f, Tremolo = 0.6f, TremoloHz = 20 }).ToArray(),
        ["heal"] = new[]
        {
            new Voice { Wave = Wave.Sine, Len = 0.35f, Freq = 600, FreqEnd = 1200, Vol = 0.2f, Attack = 0.05f, Vibrato = 0.02f, VibratoHz = 8, Decay = 1 },
            new Voice { Wave = Wave.Triangle, At = 0.08f, Len = 0.3f, Freq = 900, FreqEnd = 1800, Vol = 0.1f, Attack = 0.05f },
        },
        ["boost"] = Notes(0, 0.05f, 0.08f, "C5 G5 C6", (a, l, f) => Sq(a, l, f, 0.12f, 0.125f)).ToArray(),
        ["debuff"] = Notes(0, 0.05f, 0.08f, "C6 G5 C5", (a, l, f) => Sq(a, l, f, 0.12f, 0.125f)).ToArray(),
        ["status"] = new[] { new Voice { Wave = Wave.Saw, Len = 0.3f, Freq = 240, FreqEnd = 180, Vol = 0.16f, Vibrato = 0.06f, VibratoHz = 7, Cut = 0.3f } },
        ["stun"] = new[]
        {
            new Voice { Wave = Wave.Square, Len = 0.18f, Freq = 1400, FreqEnd = 260, Vol = 0.16f, Duty = 0.3f, Vibrato = 0.15f, VibratoHz = 40 },
            new Voice { Wave = Wave.Noise, Len = 0.12f, Freq = 8000, Cut = 0.5f, CutEnd = 0.1f, Vol = 0.12f },
        },
        ["berry"] = new[] { new Voice { Wave = Wave.Sine, Len = 0.09f, Freq = 320, FreqEnd = 700, Vol = 0.3f } },
        ["cast"] = new[]
        {
            new Voice { Wave = Wave.Triangle, Len = 0.18f, Freq = 300, FreqEnd = 900, Vol = 0.18f, Attack = 0.05f },
            new Voice { Wave = Wave.Noise, Len = 0.15f, Freq = 8000, Cut = 0.05f, CutEnd = 0.2f, Vol = 0.18f, Attack = 0.05f },
        },
        ["shiny"] = Notes(0, 0.06f, 0.25f, "E7 B7 E8", (a, l, f) => Sin(a, l, f, 0.1f))
            .Append(new Voice { Wave = Wave.Triangle, Len = 0.5f, Freq = N("E6"), Vol = 0.06f, Tremolo = 0.7f, TremoloHz = 18 }).ToArray(),
        ["gong"] = new[]
        {
            new Voice { Wave = Wave.Sine, Len = 1.6f, Freq = 98, Vol = 0.4f, Decay = 1.1f },
            new Voice { Wave = Wave.Sine, Len = 1.3f, Freq = 98 * 2.76f, Vol = 0.18f, Decay = 1.4f },
            new Voice { Wave = Wave.Sine, Len = 1.0f, Freq = 98 * 5.4f, Vol = 0.08f, Decay = 1.8f },
            new Voice { Wave = Wave.Noise, Len = 0.08f, Freq = 6000, Cut = 0.4f, CutEnd = 0.05f, Vol = 0.25f },
        },
        ["victory"] = Notes(0, 0.1f, 0.12f, "G4 C5 E5 G5", (a, l, f) => Sq(a, l, f, 0.14f))
            .Append(Sq(0.4f, 0.6f, N("C6"), 0.14f))
            .Concat(Notes(0, 0.2f, 0.22f, "C3 G3", (a, l, f) => Tri(a, l, f, 0.22f)))
            .Append(Tri(0.4f, 0.6f, N("C4"), 0.22f)).ToArray(),
        ["defeat"] = Notes(0, 0.25f, 0.3f, "E4 D#4 D4", (a, l, f) => Tri(a, l, f, 0.2f))
            .Append(new Voice { Wave = Wave.Triangle, At = 0.75f, Len = 0.8f, Freq = N("C#4"), Vol = 0.2f, Vibrato = 0.01f, VibratoHz = 5, Decay = 1 })
            .Append(new Voice { Wave = Wave.Saw, Len = 1.5f, Freq = N("A2"), Vol = 0.12f, Cut = 0.06f, Decay = 1 }).ToArray(),

        // Skill themes: where the effect lands.
        ["fx_ember"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.45f, Freq = 7000, Cut = 0.08f, CutEnd = 0.4f, Vol = 0.35f, Attack = 0.03f, Decay = 1.3f, Tremolo = 0.5f, TremoloHz = 25 },
            new Voice { Wave = Wave.Saw, Len = 0.4f, Freq = 90, FreqEnd = 60, Vol = 0.18f, Cut = 0.1f },
        },
        ["fx_frost"] = new[]
        {
            Sin(0, 0.25f, N("A6"), 0.14f), Sin(0.04f, 0.3f, N("E7"), 0.11f), Tri(0.08f, 0.35f, N("A7"), 0.06f),
            new Voice { Wave = Wave.Noise, Len = 0.1f, Freq = 11000, Cut = 0.6f, CutEnd = 0.2f, Vol = 0.1f },
        },
        ["fx_tide"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.35f, Freq = 9000, Cut = 0.5f, CutEnd = 0.04f, Vol = 0.35f, Decay = 1.6f },
            new Voice { Wave = Wave.Sine, At = 0.05f, Len = 0.12f, Freq = 250, FreqEnd = 650, Vol = 0.18f },
            new Voice { Wave = Wave.Sine, At = 0.15f, Len = 0.1f, Freq = 300, FreqEnd = 800, Vol = 0.13f },
        },
        ["fx_leaf"] = new[] { new Voice { Wave = Wave.Noise, Len = 0.35f, Freq = 7000, Cut = 0.15f, CutEnd = 0.3f, Vol = 0.45f, Tremolo = 0.8f, TremoloHz = 30, Attack = 0.04f, Decay = 1.2f } },
        ["fx_spark"] = new[]
        {
            new Voice { Wave = Wave.Square, Len = 0.22f, Freq = 1800, FreqEnd = 220, Vol = 0.16f, Duty = 0.3f, Vibrato = 0.25f, VibratoHz = 55 },
            new Voice { Wave = Wave.Noise, Len = 0.18f, Freq = 11000, Cut = 0.9f, CutEnd = 0.2f, Vol = 0.16f, Tremolo = 0.9f, TremoloHz = 45 },
        },
        ["fx_stone"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.5f, Freq = 2500, Cut = 0.06f, CutEnd = 0.02f, Vol = 0.45f, Decay = 1.4f },
            new Voice { Wave = Wave.Sine, Len = 0.3f, Freq = 80, FreqEnd = 40, Vol = 0.45f, Decay = 1.8f },
        },
        ["fx_venom"] = new[]
        {
            new Voice { Wave = Wave.Sine, Len = 0.06f, Freq = 300, FreqEnd = 520, Vol = 0.2f },
            new Voice { Wave = Wave.Sine, At = 0.07f, Len = 0.06f, Freq = 240, FreqEnd = 450, Vol = 0.18f },
            new Voice { Wave = Wave.Sine, At = 0.14f, Len = 0.07f, Freq = 360, FreqEnd = 600, Vol = 0.18f },
            new Voice { Wave = Wave.Sine, At = 0.22f, Len = 0.06f, Freq = 280, FreqEnd = 500, Vol = 0.15f },
            new Voice { Wave = Wave.Noise, Len = 0.3f, Freq = 2000, Cut = 0.05f, Vol = 0.08f },
        },
        ["fx_shade"] = new[]
        {
            new Voice { Wave = Wave.Sine, Len = 0.6f, Freq = 220, Vol = 0.18f, Attack = 0.2f, Vibrato = 0.03f, VibratoHz = 3, Decay = 1 },
            new Voice { Wave = Wave.Sine, Len = 0.6f, Freq = 233, Vol = 0.15f, Attack = 0.2f, Decay = 1 },
            new Voice { Wave = Wave.Noise, Len = 0.5f, Freq = 3000, Cut = 0.03f, Vol = 0.1f, Attack = 0.2f },
        },
        ["fx_holy"] = new[]
        {
            new Voice { Wave = Wave.Triangle, Len = 0.5f, Freq = N("C6"), Vol = 0.12f, Attack = 0.08f },
            new Voice { Wave = Wave.Triangle, Len = 0.5f, Freq = N("E6"), Vol = 0.1f, Attack = 0.08f },
            new Voice { Wave = Wave.Triangle, Len = 0.5f, Freq = N("G6"), Vol = 0.08f, Attack = 0.08f },
            new Voice { Wave = Wave.Sine, At = 0.1f, Len = 0.4f, Freq = N("C8"), Vol = 0.04f, Tremolo = 0.7f, TremoloHz = 16 },
        },
        ["fx_arcane"] = new[]
        {
            new Voice { Wave = Wave.Sine, Len = 0.4f, Freq = 440, FreqEnd = 990, Vol = 0.2f, Vibrato = 0.12f, VibratoHz = 11, Decay = 1.1f },
            new Voice { Wave = Wave.Square, Len = 0.35f, Freq = 220, FreqEnd = 495, Vol = 0.07f, Duty = 0.2f, Cut = 0.2f },
        },
        ["fx_brass"] = new[]
        {
            new Voice { Wave = Wave.Sine, Len = 0.3f, Freq = 523, Vol = 0.18f, Decay = 2.2f },
            new Voice { Wave = Wave.Sine, Len = 0.25f, Freq = 1127, Vol = 0.12f, Decay = 2.5f },
            new Voice { Wave = Wave.Sine, Len = 0.2f, Freq = 1789, Vol = 0.08f, Decay = 3 },
            new Voice { Wave = Wave.Noise, Len = 0.04f, Freq = 9000, Cut = 0.7f, Vol = 0.22f, Decay = 3 },
            new Voice { Wave = Wave.Square, Len = 0.08f, Freq = 300, FreqEnd = 200, Vol = 0.1f, Cut = 0.3f },
        },
        ["fx_chem"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.45f, Freq = 11000, Cut = 0.9f, CutEnd = 0.5f, Vol = 0.18f, Tremolo = 0.9f, TremoloHz = 35, Attack = 0.02f },
            new Voice { Wave = Wave.Sine, At = 0.05f, Len = 0.08f, Freq = 400, FreqEnd = 800, Vol = 0.16f },
            new Voice { Wave = Wave.Sine, At = 0.2f, Len = 0.08f, Freq = 350, FreqEnd = 750, Vol = 0.13f },
        },
        ["fx_sound"] = new[]
        {
            new Voice { Wave = Wave.Saw, Len = 0.5f, Freq = 160, FreqEnd = 95, Vol = 0.26f, Vibrato = 0.05f, VibratoHz = 18, Cut = 0.25f, Attack = 0.03f, Decay = 1.1f },
            new Voice { Wave = Wave.Noise, Len = 0.45f, Freq = 4000, Cut = 0.15f, Vol = 0.12f, Attack = 0.03f, Tremolo = 0.5f, TremoloHz = 18 },
        },
        ["fx_wind"] = new[]
        {
            new Voice { Wave = Wave.Noise, Len = 0.5f, Freq = 12000, Cut = 0.03f, CutEnd = 0.3f, Vol = 0.45f, Attack = 0.15f, Decay = 1.1f },
            new Voice { Wave = Wave.Noise, At = 0.15f, Len = 0.35f, Freq = 12000, Cut = 0.25f, CutEnd = 0.02f, Vol = 0.28f },
        },
    };

    public static float[] Render(string name) => SfxSynth.Render(All[name], Seed(name));
}

/// <summary>
/// Plays the sounds: a pool of players under the game root, every sound made once up front. Pitch
/// varies a little each time so repeats don't grate; that roll is the screen's own, never the rules'.
/// N mutes; --mute starts muted.
/// </summary>
public partial class Sound : Node
{
    static Sound _on;
    static readonly Dictionary<string, AudioStreamWav> Streams = new();
    readonly List<AudioStreamPlayer> _players = new();
    readonly SeededRng _rng = new(0x50D5);

    public static bool Muted;
    public static float VolumeDb = -4;

    public override void _Ready()
    {
        _on = this;
        foreach (var name in SoundBook.All.Keys) Stream(name);
        for (int i = 0; i < 16; i++)
        {
            var p = new AudioStreamPlayer();
            AddChild(p);
            _players.Add(p);
        }
    }

    public override void _ExitTree()
    {
        if (_on == this) _on = null;
    }

    public static AudioStreamWav Stream(string name)
    {
        if (!Streams.TryGetValue(name, out var s)) Streams[name] = s = SfxSynth.ToStream(SoundBook.Render(name));
        return s;
    }

    /// <summary>Plays a sound by name; pitch 1 as made, below 1 lower and slower.</summary>
    public static void Play(string name, float pitch = 1, float db = 0)
    {
        if (_on == null || Muted || !SoundBook.All.ContainsKey(name)) return;
        var p = _on._players.FirstOrDefault(x => !x.Playing) ?? _on._players.OrderByDescending(x => x.GetPlaybackPosition()).First();
        p.Stream = Stream(name);
        p.PitchScale = pitch * _on._rng.Range(0.96f, 1.04f);
        p.VolumeDb = VolumeDb + db;
        p.Play();
    }

    /// <summary>The sound of a skill's effect landing, by its theme.</summary>
    public static void Fx(Tactics.FxTheme theme, float db = 0) =>
        Play(theme switch { Tactics.FxTheme.Strike => "hit", Tactics.FxTheme.Boost => "boost", _ => "fx_" + theme.ToString().ToLowerInvariant() }, 1, db);
}
