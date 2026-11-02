using System;
using System.Collections.Generic;
using System.Linq;

namespace CreatureTactics.Audio;

/// <summary>
/// A little music theory for writing songs as data: chord tones by name, and whole lines (an
/// arpeggio, a bass, a held pad) drawn from a progression so only the melody and drums are written by hand.
/// </summary>
public static class Theory
{
    public const int Hold = -2, Rest = -1;
    static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static string Name(int midi) => Names[midi % 12] + (midi / 12 - 1);

    /// <summary>A chord's root (0 = C) and its tones above it: "F", "Dm", "Bb", "C#m", "G7", "Dsus".</summary>
    static (int root, int[] tones) Parse(string chord)
    {
        int root = "C D EF G A B".IndexOf(chord[0]), pos = 1;
        if (pos < chord.Length && chord[pos] == '#') { root++; pos++; }
        else if (pos < chord.Length && chord[pos] == 'b') { root--; pos++; }
        int[] tones = chord[pos..] switch
        {
            "" => new[] { 0, 4, 7 },
            "m" => new[] { 0, 3, 7 },
            "7" => new[] { 0, 4, 7, 10 },
            "m7" => new[] { 0, 3, 7, 10 },
            "dim" => new[] { 0, 3, 6 },
            "sus" => new[] { 0, 5, 7 },
            _ => throw new ArgumentException($"no chord called {chord}"),
        };
        return ((root + 12) % 12, tones);
    }

    /// <summary>Chord tone n from the root in this octave: 0 root, 1 third, 2 fifth, 3 the root an octave up...</summary>
    public static int Tone(string chord, int octave, int n)
    {
        var (root, tones) = Parse(chord);
        return 12 * (octave + 1) + root + tones[n % tones.Length] + 12 * (n / tones.Length);
    }

    /// <summary>
    /// A line over a progression, one chord a bar ("Dm,C" splits a bar): each step plays the chord tone
    /// the shape gives (cycling), or holds (<see cref="Hold"/>) or rests (<see cref="Rest"/>).
    /// </summary>
    public static string Line(string chords, int octave, int div, params int[] shape) =>
        Bars(chords, div, (chord, step, _) => shape[step % shape.Length] switch
        {
            Rest => ".",
            Hold => "-",
            var n => Name(Tone(chord, octave, n)),
        });

    /// <summary>Each chord held for as long as it lasts.</summary>
    public static string Pad(string chords, int octave, int div) =>
        Bars(chords, div, (chord, step, _) => step == 0 ? string.Join("+", Enumerable.Range(0, 3).Select(n => Name(Tone(chord, octave, n)))) : "-");

    static string Bars(string chords, int div, Func<string, int, int, string> token)
    {
        var bars = new List<string>();
        foreach (var bar in chords.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var each = bar.Split(',');
            int per = 4 * div / each.Length;
            bars.Add(string.Join(" ", each.SelectMany(c => Enumerable.Range(0, per).Select(s => token(c, s, per)))));
        }
        return string.Join(" | ", bars);
    }
}

/// <summary>
/// Every song (begun 2026-11-02 at the user's pick): FFTA-style marches and fanfares played on the
/// sprites' retro voices, with the two sides of the country heard apart, brass for the city above and
/// neon saw bass for the undercity. A new track is a new entry; <see cref="Music.ForBattle"/> picks
/// the battle tracks. The titles are working names.
/// </summary>
public static class SongBook
{
    const int H = Theory.Hold, R = Theory.Rest;

    static Song Make(string id, string name, string mood, int bpm, string chords, params Part[] parts) =>
        new() { Id = id, Name = name, Mood = mood, Bpm = bpm, Bars = chords.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, Parts = parts };

    // The progressions, one chord a bar.
    const string RanchChords = "F C Dm Bb F C Bb C Dm Bb F C Dm Bb Gm C";
    const string MapChords = "D D C G D D C A Bm G D A Bm G Em A";
    const string BattleChords = "Em Em C D Em Em C B Am C G D Am C B B";
    const string CityChords = "C D C D Am G F G C D C D F G C G";
    const string UnderChords = "F#m F#m G F#m F#m F#m E G Bm Bm G A F#m G E F#m";
    const string RivalChords = "Cm Cm Ab G Cm Cm Ab G Fm Fm Cm Cm Ab Bb G G";

    public static readonly Song[] All =
    {
        Make("ranch", "Home Pasture", "the ranch and the lab: warm, unhurried, a flute over a harp", 88, RanchChords,
            new Part { Inst = "flute", Echo = 0.75f, Feedback = 0.25f, Notes =
                "A4 - - C5 F5 - E5 - | E5 - D5 C5 G4 - - - | A4 - - D5 F5 - E5 D5 | D5 - - - C5 - Bb4 - | " +
                "A4 - - C5 F5 - G5 A5 | G5 - - E5 C5 - - - | D5 - F5 - Bb5 - A5 G5 | E5 - - - - - . . | " +
                "F5 - E5 - D5 - A4 - | Bb4 - C5 - D5 - - - | C5 - A4 - F4 - A4 C5 | G5 - - - E5 - - - | " +
                "F5 - - E5 D5 - - A4 | Bb4 - D5 - F5 - - - | G5 - F5 - D5 - Bb4 - | E5 - - - C5 - . ." },
            new Part { Inst = "harp", Notes = Theory.Line(RanchChords, 3, 2, 0, 2, 3, 4, 3, 2, 3, 2) },
            new Part { Inst = "bass", Notes = Theory.Line(RanchChords, 2, 2, 0, H, H, H, 2, H, H, H) },
            new Part { Inst = "pad", Notes = Theory.Pad(RanchChords, 4, 2) }),

        Make("map", "The Open Road", "the country map: a marching horn call, setting out", 108, MapChords,
            new Part { Inst = "horn", Echo = 0.75f, Feedback = 0.15f, Notes =
                "D5 - - A4 D5 - E5 - | F#5 - - - E5 - D5 - | E5 - - C5 G5 - - E5 | D5 - - - - - B4 - | " +
                "A4 - - D5 F#5 - A5 - | A5 - G5 - F#5 - E5 - | G5 - - E5 C5 - E5 - | C#5 - - - E5 - - - | " +
                "D5 - - C#5 B4 - F#4 - | G4 - A4 - B4 - D5 - | F#5 - - - E5 - D5 - | E5 - - - - - . . | " +
                "F#5 - - E5 D5 - B4 - | D5 - - - B4 - G4 - | E5 - - - G5 - - - | A5 - G5 - E5 - C#5 -" },
            new Part { Inst = "lute", Notes = Theory.Line(MapChords, 3, 2, 0, 1, 2, 3, 2, 1, 2, 1) },
            new Part { Inst = "bass", Notes = Theory.Line(MapChords, 2, 2, 0, H, 0, 2, 0, H, 2, H) },
            new Part { Inst = "drums", Div = 4, Notes =
                "k+c . h . s . h . k . h . s . h h | k . h . s . h . k . h . s . h h | k . h . s . h . k . h . s . h h | k . h . s . h . k . h . s s s s | " +
                "k . h . s . h . k . h . s . h h | k . h . s . h . k . h . s . h h | k . h . s . h . k . h . s . h h | k . h . s . h . s . s . s s s s" }),

        Make("battle", "Wild Clash", "battles in the wilds: driving, minor, a pulse lead", 140, BattleChords,
            new Part { Inst = "chip", Echo = 0.75f, Feedback = 0.25f, Notes =
                "E5 - B4 - E5 - G5 - | F#5 - E5 - D5 - B4 - | C5 - - - E5 - G5 - | F#5 - - - A5 - - - | " +
                "B5 - - - G5 - E5 - | F#5 - G5 - A5 - G5 - | E5 - - - C5 - E5 - | D#5 - - - F#5 - - - | " +
                "C6 - - - B5 - A5 - | G5 - - - E5 - - - | D5 - G5 - B5 - - - | A5 - - - F#5 - D5 - | " +
                "E5 - A5 - C6 - - - | B5 - A5 - G5 - E5 - | F#5 - - - D#5 - - - | F#5 - E5 - D#5 - B4 -" },
            new Part { Inst = "bass", Notes = Theory.Line(BattleChords, 2, 2, 0, 0, 3, 0, 0, 0, 3, 0) },
            new Part { Inst = "pulse", Div = 4, Notes = Theory.Line(BattleChords, 4, 4, 0, 1, 2, 3, 2, 1, 2, 1) },
            new Part { Inst = "drums", Div = 4, Notes =
                "k+c . h . s . h k k . h . s . h . | k . h . s . h k k . h . s . h . | k . h . s . h k k . h . s . h . | k . h . s . h k k . s . s s s s | " +
                "k . h . s . h k k . h . s . h . | k . h . s . h k k . h . s . h . | k . h . s . h k k . h . s . h . | k . h . s . h k k . s . s s s s" }),

        Make("battle_city", "Brass and Banners", "battles in the city above: a glorious brass fanfare, bells and timpani, clockwork ticks", 128, CityChords,
            new Part { Inst = "horn", Echo = 0.75f, Feedback = 0.2f, Notes =
                "G4 - C5 - E5 - - G5 | F#5 - - - E5 - D5 - | E5 - - - G5 - - E5 | A5 - - - F#5 - - - | " +
                "A5 - G5 - E5 - C5 - | D5 - - - B4 - G4 - | A4 - C5 - F5 - A5 - | G5 - - - - - . . | " +
                "C6 - - - G5 - E5 - | F#5 - - - A5 - - - | G5 - E5 - C5 - E5 - | D5 - - - F#5 - A5 - | " +
                "C6 - - - A5 - F5 - | B5 - - - D6 - - - | C6 - - - G5 - E5 - | D5 - - - B4 - D5 -" },
            new Part { Inst = "bell", Notes = Theory.Line(CityChords, 4, 2, R, 0, R, 1, R, 2, R, 1) },
            new Part { Inst = "bass", Notes = Theory.Line(CityChords, 2, 2, 0, H, H, 0, 2, H, 0, H) },
            new Part { Inst = "pad", Notes = Theory.Pad(CityChords, 4, 2) },
            new Part { Inst = "drums", Div = 4, Notes =
                "k+t+c . r . s . r . k . r k s . r . | k+t . r . s . r . k . r k s . r . | k+t . r . s . r . k . r k s . r . | k+t . r . s . r . t . t . T . T T" }),

        Make("battle_under", "Neon Mire", "battles in the undercity: a dark phrygian groove, saw bass and echoing chem-glow arpeggios", 124, UnderChords,
            new Part { Inst = "chip", Echo = 0.75f, Feedback = 0.35f, Notes =
                "F#5 - - C#5 - - F#5 - | A5 - G5 - F#5 - E5 - | D5 - - - B4 - D5 - | C#5 - - - - - . . | " +
                "F#5 - - A5 - - C#6 - | B5 - A5 - G5 - F#5 - | E5 - - G#5 - - B5 - | B5 - - - D6 - - - | " +
                "D6 - - - C#6 - B5 - | F#5 - - - D5 - - - | G5 - - - B5 - D6 - | C#6 - - - A5 - E5 - | " +
                "F#5 - A5 - C#6 - A5 - | B5 - - - G5 - - - | G#5 - - - E5 - B4 - | F#5 - - - - - . ." },
            new Part { Inst = "sawbass", Notes = Theory.Line(UnderChords, 2, 2, 0, 0, 3, 0, 0, 3, 0, 3) },
            new Part { Inst = "pulse", Div = 4, Echo = 0.75f, Feedback = 0.2f, Notes = Theory.Line(UnderChords, 4, 4, 0, 1, 2, 3) },
            new Part { Inst = "pad", Notes = Theory.Pad(UnderChords, 4, 2) },
            new Part { Inst = "drums", Div = 4, Notes =
                "k . o . p . o . k . o k p . o . | k . o . p . o . k . o k p . o . | k . o . p . o . k . o k p . o . | k . o . p . o . k . o k p . p p" }),

        Make("rival", "Rival's Gambit", "duels with rival tamers and the coliseum: fast, harmonic minor, a saw lead", 150, RivalChords,
            new Part { Inst = "lead", Echo = 0.75f, Feedback = 0.15f, Notes =
                "C5 - G4 - C5 - D5 Eb5 | G5 - - - F5 - Eb5 - | Eb5 - - C5 Ab4 - C5 - | B4 - - - D5 - G5 - | " +
                "C6 - - - G5 - Eb5 - | F5 - Eb5 - D5 - C5 - | Eb5 - - - Ab5 - - - | G5 - - - B4 - D5 - | " +
                "F5 - - - Ab5 - C6 - | Bb5 - Ab5 - G5 - F5 - | G5 - - - Eb5 - C5 - | D5 - Eb5 - G5 - - - | " +
                "Ab5 - - - G5 - F5 - | F5 - - - D5 - Bb4 - | B4 - D5 - G5 - B5 - | D6 - C6 - B5 - G5 -" },
            new Part { Inst = "sawbass", Notes = Theory.Line(RivalChords, 2, 2, 0, 0, 3, 0, 0, 0, 3, 0) },
            new Part { Inst = "pulse", Div = 4, Notes = Theory.Line(RivalChords, 4, 4, 0, 1, 2, 1, 3, 1, 2, 1) },
            new Part { Inst = "drums", Div = 4, Notes =
                "k+c . h k s . h . k . h k s . h . | k . h k s . h . k . h k s . h . | k . h k s . h . k . h k s . h . | k . h k s . s . k . s . s s s s" }),
    };

    public static Song Get(string id) => All.FirstOrDefault(s => s.Id == id);
}
