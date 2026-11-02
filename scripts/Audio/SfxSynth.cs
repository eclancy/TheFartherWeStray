using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Audio;

public enum Wave { Square, Triangle, Saw, Sine, Noise }

/// <summary>One voice of a sound: a waveform with a pitch slide, an envelope and a filter.</summary>
public sealed class Voice
{
    public Wave Wave = Wave.Square;
    public float At, Len = 0.1f;              // start and length, in seconds
    public float Freq = 440, FreqEnd;         // pitch in Hz, sliding to FreqEnd (0: no slide); for noise, how often it changes
    public float Vol = 0.3f;
    public float Attack = 0.004f, Decay = 1.5f; // attack time; decay curve over the length (higher: punchier)
    public float Vibrato, VibratoHz = 6;      // pitch wobble, as a share of the pitch
    public float Duty = 0.5f;                 // square pulse width (0.125 and 0.25 sound thinner)
    public float Cut = 1, CutEnd = -1;        // one-pole low-pass, 1 open to near 0 muffled, sliding to CutEnd
    public float Tremolo, TremoloHz = 12;     // volume wobble, as a share
}

/// <summary>
/// Sound effects made in code, like the sprites: voices summed into 16-bit mono at 22 kHz, retro
/// (sfxr-style) to suit the pixel art. Noise comes from <see cref="SeededRng"/>, so a sound is the
/// same every time it's made.
/// </summary>
public static class SfxSynth
{
    public const int Rate = 22050;

    public static float[] Render(IReadOnlyList<Voice> voices, ulong seed)
    {
        float total = voices.Max(v => v.At + v.Len) + 0.01f;
        var buf = new float[(int)(total * Rate)];
        var rng = new SeededRng(seed);
        foreach (var v in voices)
        {
            int start = (int)(v.At * Rate), n = Math.Max(1, (int)(v.Len * Rate));
            double phase = 0;
            float held = 0, lp = 0;
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                float t = i / (float)Rate, k = i / (float)n;
                float f = v.FreqEnd > 0 ? v.Freq * MathF.Pow(v.FreqEnd / v.Freq, k) : v.Freq;
                if (v.Vibrato > 0) f *= 1 + v.Vibrato * MathF.Sin(MathF.Tau * v.VibratoHz * t);
                double before = phase;
                phase += f / Rate;
                float x = (float)(phase - Math.Floor(phase));
                if (v.Wave == Wave.Noise && (i == 0 || Math.Floor(phase) != Math.Floor(before))) held = rng.Range(-1f, 1f);
                float s = v.Wave switch
                {
                    Wave.Square => x < v.Duty ? 1 : -1,
                    Wave.Triangle => 4 * MathF.Abs(x - 0.5f) - 1,
                    Wave.Saw => 2 * x - 1,
                    Wave.Sine => MathF.Sin(MathF.Tau * x),
                    _ => held,
                };
                float cut = v.CutEnd >= 0 ? v.Cut + (v.CutEnd - v.Cut) * k : v.Cut;
                lp += (s - lp) * Math.Clamp(cut, 0.002f, 1f);
                float env = Math.Min(1f, t / Math.Max(v.Attack, 0.0005f)) * MathF.Pow(1 - k, v.Decay);
                if (v.Tremolo > 0) env *= 1 - v.Tremolo * (0.5f + 0.5f * MathF.Sin(MathF.Tau * v.TremoloHz * t));
                buf[start + i] += lp * env * v.Vol;
            }
        }
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Tanh(buf[i] * 1.1f); // a soft limit, never a hard clip
        return buf;
    }

    public static AudioStreamWav ToStream(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f);
            bytes[i * 2] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = bytes };
    }

    /// <summary>A note's pitch: "C5", "F#4", "Bb3".</summary>
    public static float N(string note)
    {
        int idx = "C D EF G A B".IndexOf(char.ToUpperInvariant(note[0]));
        int pos = 1;
        if (note[pos] == '#') { idx++; pos++; } else if (note[pos] == 'b') { idx--; pos++; }
        int octave = int.Parse(note[pos..]);
        return 440f * MathF.Pow(2, (12 * (octave + 1) + idx - 69) / 12f);
    }

    /// <summary>A stable seed from a sound's name.</summary>
    public static ulong Seed(string name) => SeededRng.Hash(name);
}
