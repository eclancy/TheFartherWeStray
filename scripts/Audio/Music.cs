using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheFartherWeStray.Creatures;
using Godot;

namespace TheFartherWeStray.Audio;

/// <summary>
/// A song as data (music begun 2026-11-02 at the user's pick): a tempo, a length in bars and parts,
/// each an instrument playing a pattern. Rendered once into a loop by <see cref="MusicSynth"/>.
/// </summary>
public sealed class Song
{
    public string Id, Name, Mood;
    public int Bpm, Bars;
    public Part[] Parts;
}

/// <summary>
/// One instrument's line. Notes is a pattern, one token per step: a note ("C5", "F#4", "Bb3", or a
/// chord "D4+F4+A4"), "-" to hold the one before, "." for silence; drums use letters (k kick, s snare,
/// h hat, o open hat, c crash, t and T toms, p clap, r rim tick), "k+h" for two at once. "|" marks a
/// bar, which must hold 4 x Div steps. A pattern shorter than the song repeats.
/// </summary>
public sealed class Part
{
    public string Inst, Notes;
    public int Div = 2;              // steps per beat: 2 eighths, 4 sixteenths
    public float Echo, Feedback = 0.3f; // an echo this many beats behind (0: none), and how much comes back
}

/// <summary>
/// Turns a <see cref="Song"/> into a seamless loop: every note is a <see cref="Voice"/> mixed by
/// <see cref="SfxSynth"/>, notes ringing past the end wrap round to the start, and the echo runs
/// round the loop too, so the seam can't be heard. Same input, same samples (noise is seeded).
/// </summary>
public static class MusicSynth
{
    const float Tail = 6; // seconds a note may ring past the loop's end before it wraps

    public static float BeatSeconds(Song s) => 60f / s.Bpm;
    public static int LoopSamples(Song s) => (int)Math.Round(s.Bars * 4 * BeatSeconds(s) * SfxSynth.Rate);

    /// <summary>The bars of a part, as token lists (a pattern without bar marks is one long bar).</summary>
    public static List<string[]> Bars(Part p) =>
        p.Notes.Split('|').Select(b => b.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(b => b.Length > 0).ToList();

    /// <summary>Bars that don't hold 4 x Div steps: mistakes in the song's data.</summary>
    public static IEnumerable<string> Problems(Song s)
    {
        foreach (var p in s.Parts)
        {
            var bars = Bars(p);
            for (int i = 0; i < bars.Count; i++)
                if (bars.Count > 1 && bars[i].Length != 4 * p.Div) yield return $"{s.Id} {p.Inst} bar {i + 1}: {bars[i].Length} steps, not {4 * p.Div}";
            int steps = bars.Sum(b => b.Length);
            if (steps > s.Bars * 4 * p.Div) yield return $"{s.Id} {p.Inst}: {steps} steps, longer than the song's {s.Bars * 4 * p.Div}";
        }
    }

    /// <summary>The song as one loop; <paramref name="master"/> false leaves the parts' raw levels (for checking the balance).</summary>
    public static float[] Render(Song s, bool master = true)
    {
        int loop = LoopSamples(s);
        var mix = new float[loop];
        var rng = new SeededRng(SfxSynth.Seed("music_" + s.Id));
        foreach (var p in s.Parts)
        {
            var buf = new float[loop + (int)(Tail * SfxSynth.Rate)];
            var tokens = Bars(p).SelectMany(b => b).ToArray();
            int total = s.Bars * 4 * p.Div;
            float step = BeatSeconds(s) / p.Div;
            for (int i = 0; i < total; i++)
            {
                string tok = tokens[i % tokens.Length];
                if (tok is "-" or ".") continue;
                int held = 1;
                while (i + held < total && tokens[(i + held) % tokens.Length] == "-") held++;
                foreach (var one in tok.Split('+'))
                foreach (var v in Instruments.Play(p.Inst, one, i * step, held * step))
                    SfxSynth.Mix(buf, v, rng);
            }
            // What rings past the end comes back in at the start.
            for (int i = loop; i < buf.Length; i++) buf[i % loop] += buf[i];
            if (p.Echo > 0)
            {
                // A feedback echo, twice round the loop so the tail at the seam is the settled one.
                int d = (int)(p.Echo * BeatSeconds(s) * SfxSynth.Rate);
                var wet = new float[loop];
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < loop; i++)
                        wet[i] = (buf[(i - d + loop) % loop] + wet[(i - d + loop) % loop]) * p.Feedback;
                for (int i = 0; i < loop; i++) mix[i] += buf[i] + wet[i];
            }
            else for (int i = 0; i < loop; i++) mix[i] += buf[i];
        }
        if (!master) return mix;
        // A gentle cut below about 40 Hz, round the loop twice so the seam is settled: rumble no speaker
        // plays would only eat the headroom.
        float rc = 1 / (MathF.Tau * 40), a = rc / (rc + 1f / SfxSynth.Rate), y = 0, prev = mix[^1];
        var cut = new float[loop];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < loop; i++) { y = a * (y + mix[i] - prev); prev = mix[i]; cut[i] = y; }
        mix = cut;
        // Every song at about the same loudness, peaks rounded off rather than clipped.
        float peak = mix.Max(Math.Abs), rms = MathF.Sqrt(mix.Average(x => x * x));
        float gain = Math.Min(0.13f / Math.Max(rms, 1e-5f), 0.95f / Math.Max(peak, 1e-5f) * 1.25f);
        for (int i = 0; i < loop; i++) mix[i] = MathF.Tanh(mix[i] * gain);
        return mix;
    }

    /// <summary>A loop ready to play: it repeats from the start forever.</summary>
    public static AudioStreamWav ToStream(float[] samples)
    {
        var stream = SfxSynth.ToStream(samples);
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        stream.LoopBegin = 0;
        stream.LoopEnd = samples.Length;
        return stream;
    }
}

/// <summary>The instruments: how a note on each becomes voices for the synth.</summary>
public static class Instruments
{
    public static IEnumerable<Voice> Play(string inst, string token, float at, float len)
    {
        if (inst == "drums")
        {
            foreach (var v in Drum(token, at)) yield return v;
            yield break;
        }
        float f = SfxSynth.N(token);
        switch (inst)
        {
            case "flute": // a breathy wooden flute, a little vibrato
                yield return new Voice { Wave = Wave.Triangle, At = at, Len = len, Freq = f, Vol = 0.17f, Attack = 0.03f, Decay = 0.45f, Vibrato = 0.006f, VibratoHz = 5.2f };
                yield return new Voice { Wave = Wave.Noise, At = at, Len = Math.Min(len, 0.08f), Freq = 9000, Cut = 0.15f, Vol = 0.025f, Decay = 2 };
                break;
            case "horn": // brass: a saw through a closing filter (the city above)
                yield return new Voice { Wave = Wave.Saw, At = at, Len = len, Freq = f, Vol = 0.17f, Attack = 0.025f, Decay = 0.55f, Cut = 0.22f, CutEnd = 0.1f, Vibrato = 0.004f, VibratoHz = 5.5f };
                yield return new Voice { Wave = Wave.Square, At = at, Len = len, Freq = f / 2, Vol = 0.04f, Duty = 0.5f, Attack = 0.03f, Decay = 0.6f, Cut = 0.08f };
                break;
            case "lead": // a bright saw lead for duels
                yield return new Voice { Wave = Wave.Saw, At = at, Len = len, Freq = f, Vol = 0.14f, Attack = 0.01f, Decay = 0.5f, Cut = 0.35f, CutEnd = 0.2f, Vibrato = 0.005f, VibratoHz = 6 };
                break;
            case "chip": // a thin pulse lead
                yield return new Voice { Wave = Wave.Square, At = at, Len = len, Freq = f, Vol = 0.1f, Duty = 0.125f, Attack = 0.005f, Decay = 0.7f, Vibrato = 0.006f, VibratoHz = 6, Cut = 0.6f };
                break;
            case "harp": // plucked strings that ring
                yield return new Voice { Wave = Wave.Triangle, At = at, Len = Math.Max(len, 0.9f), Freq = f, Vol = 0.12f, Attack = 0.002f, Decay = 2.6f };
                yield return new Voice { Wave = Wave.Sine, At = at, Len = Math.Max(len, 0.5f), Freq = f * 2, Vol = 0.025f, Attack = 0.002f, Decay = 3 };
                break;
            case "lute": // a dry plucked lute
                yield return new Voice { Wave = Wave.Square, At = at, Len = Math.Max(len, 0.35f), Freq = f, Vol = 0.07f, Duty = 0.25f, Attack = 0.002f, Decay = 2.8f, Cut = 0.3f, CutEnd = 0.08f };
                break;
            case "pulse": // a quick arpeggio blip
                yield return new Voice { Wave = Wave.Square, At = at, Len = Math.Max(len, 0.12f), Freq = f, Vol = 0.05f, Duty = 0.25f, Attack = 0.002f, Decay = 2, Cut = 0.5f };
                break;
            case "bell": // chimes: brass gears and glass (two partials)
                yield return new Voice { Wave = Wave.Sine, At = at, Len = Math.Max(len, 1.1f), Freq = f, Vol = 0.055f, Attack = 0.002f, Decay = 3 };
                yield return new Voice { Wave = Wave.Sine, At = at, Len = 0.5f, Freq = f * 2.76f, Vol = 0.018f, Attack = 0.002f, Decay = 3.5f };
                break;
            case "bass": // a round, plain bass
                yield return new Voice { Wave = Wave.Triangle, At = at, Len = len, Freq = f, Vol = 0.2f, Attack = 0.006f, Decay = 0.6f };
                break;
            case "sawbass": // a gritty neon bass, its filter snapping shut (the undercity)
                yield return new Voice { Wave = Wave.Saw, At = at, Len = len, Freq = f, Vol = 0.2f, Attack = 0.004f, Decay = 0.9f, Cut = 0.16f, CutEnd = 0.035f };
                yield return new Voice { Wave = Wave.Sine, At = at, Len = len, Freq = f, Vol = 0.14f, Attack = 0.004f, Decay = 0.7f };
                break;
            case "pad": // a soft held chord
                yield return new Voice { Wave = Wave.Square, At = at, Len = len, Freq = f, Vol = 0.045f, Duty = 0.5f, Attack = 0.18f, Decay = 0.35f, Cut = 0.05f, Tremolo = 0.15f, TremoloHz = 3.5f };
                break;
            default:
                throw new ArgumentException($"no instrument called {inst}");
        }
    }

    static IEnumerable<Voice> Drum(string token, float at)
    {
        foreach (char d in token)
            switch (d)
            {
                case 'k':
                    yield return new Voice { Wave = Wave.Sine, At = at, Len = 0.16f, Freq = 150, FreqEnd = 52, Vol = 0.45f, Attack = 0.001f, Decay = 1.6f };
                    yield return new Voice { Wave = Wave.Noise, At = at, Len = 0.012f, Freq = 6000, Cut = 0.4f, Vol = 0.12f, Decay = 2 };
                    break;
                case 's':
                    yield return new Voice { Wave = Wave.Noise, At = at, Len = 0.16f, Freq = 9000, Cut = 0.55f, CutEnd = 0.12f, Vol = 0.21f, Attack = 0.001f, Decay = 2.2f };
                    yield return new Voice { Wave = Wave.Triangle, At = at, Len = 0.08f, Freq = 200, FreqEnd = 160, Vol = 0.18f, Attack = 0.001f, Decay = 2 };
                    break;
                case 'h':
                    yield return new Voice { Wave = Wave.Noise, At = at, Len = 0.03f, Freq = 15000, Cut = 0.95f, Vol = 0.05f, Attack = 0.001f, Decay = 2.5f };
                    break;
                case 'o':
                    yield return new Voice { Wave = Wave.Noise, At = at, Len = 0.16f, Freq = 15000, Cut = 0.9f, Vol = 0.04f, Attack = 0.001f, Decay = 1.8f };
                    break;
                case 'c':
                    yield return new Voice { Wave = Wave.Noise, At = at, Len = 1.3f, Freq = 14000, Cut = 0.9f, CutEnd = 0.35f, Vol = 0.1f, Attack = 0.002f, Decay = 1.7f };
                    break;
                case 't':
                    yield return new Voice { Wave = Wave.Sine, At = at, Len = 0.26f, Freq = 120, FreqEnd = 78, Vol = 0.4f, Attack = 0.001f, Decay = 1.5f };
                    break;
                case 'T':
                    yield return new Voice { Wave = Wave.Sine, At = at, Len = 0.2f, Freq = 185, FreqEnd = 125, Vol = 0.34f, Attack = 0.001f, Decay = 1.5f };
                    break;
                case 'p':
                    for (int i = 0; i < 3; i++)
                        yield return new Voice { Wave = Wave.Noise, At = at + i * 0.011f, Len = i == 2 ? 0.13f : 0.02f, Freq = 7000, Cut = 0.45f, Vol = 0.18f, Attack = 0.001f, Decay = 2.4f };
                    break;
                case 'r': // a clockwork tick
                    yield return new Voice { Wave = Wave.Triangle, At = at, Len = 0.025f, Freq = 2300, Vol = 0.07f, Attack = 0.001f, Decay = 3 };
                    break;
                case '+':
                    break;
                default:
                    throw new ArgumentException($"no drum called {d}");
            }
    }
}

/// <summary>
/// Plays the music: one track at a time, faded between screens, looping. Every song is rendered once
/// on a worker thread when the game starts; a track asked for before it's ready starts when it is.
/// Quieter than the sound effects; N mutes both, --nomusic turns music off.
/// </summary>
public partial class Music : Node
{
    static Music _on;
    public static float Db = -10;
    public static bool Off;

    static readonly ConcurrentDictionary<string, float[]> Rendered = new();
    readonly Dictionary<string, AudioStreamWav> _streams = new();
    AudioStreamPlayer _deck;
    string _current, _want;
    float _level, _fade = 0.6f;

    public override void _Ready()
    {
        _on = this;
        _deck = new AudioStreamPlayer();
        AddChild(_deck);
        Task.Run(() =>
        {
            foreach (var song in SongBook.All.Where(s => !Rendered.ContainsKey(s.Id))) Rendered[song.Id] = MusicSynth.Render(song);
        });
    }

    public override void _ExitTree()
    {
        if (_on == this) _on = null;
    }

    /// <summary>Fades to this track (null: silence) over <paramref name="fade"/> seconds; the same track keeps playing.</summary>
    public static void Play(string id, float fade = 0.6f)
    {
        if (_on == null) return;
        _on._want = Off ? null : id;
        _on._fade = Math.Max(0.05f, fade);
    }

    public static void Stop(float fade = 0.4f) => Play(null, fade);

    /// <summary>The track for a battle: rivals and the coliseum have their own; otherwise, whose ground it's on.</summary>
    public static string ForBattle(Tactics.Mission m) =>
        m == null ? "battle"
        : m.Rival != null ? "rival"
        : World.Country.Get(m.Location ?? "")?.Side switch
        {
            World.Side.City => "battle_city",
            World.Side.Undercity => "battle_under",
            _ => "battle",
        };

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_current != _want)
        {
            _level -= dt / _fade;
            if (_level <= 0 || !_deck.Playing)
            {
                _level = 0;
                _deck.Stop();
                _current = null;
                if (_want != null && Rendered.TryGetValue(_want, out var samples))
                {
                    if (!_streams.TryGetValue(_want, out var stream)) _streams[_want] = stream = MusicSynth.ToStream(samples);
                    _deck.Stream = stream;
                    _deck.Play();
                    _current = _want;
                }
            }
        }
        else if (_current != null) _level = Math.Min(1, _level + dt / 0.8f);
        _deck.VolumeDb = Sound.Muted || _level <= 0.001f ? -80 : Db + Mathf.LinearToDb(_level);
    }
}
