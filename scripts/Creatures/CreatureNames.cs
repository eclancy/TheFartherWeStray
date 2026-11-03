using System;
using System.Collections.Generic;

namespace TheFartherWeStray.Creatures;

/// <summary>
/// Species names built from syllables: an element-flavoured front and a back taken from the
/// creature's most striking part ("horn" + "fin" pieces, as the design notes suggest).
/// </summary>
public static class CreatureNames
{
    static readonly Dictionary<Element, string[]> Fronts = new()
    {
        [Element.Ember] = new[] { "Pyr", "Cind", "Emb", "Scor", "Flar", "Ash" },
        [Element.Tide] = new[] { "Aqu", "Mar", "Ripp", "Tid", "Brin", "Cor" },
        [Element.Leaf] = new[] { "Fern", "Bram", "Sprou", "Moss", "Thist", "Bud" },
        [Element.Spark] = new[] { "Volt", "Zap", "Fizz", "Jolt", "Stat", "Ampi" },
        [Element.Stone] = new[] { "Pebb", "Grav", "Bould", "Crag", "Slat", "Rubb" },
        [Element.Venom] = new[] { "Tox", "Vil", "Mire", "Nox", "Blight", "Sluj" },
        [Element.Frost] = new[] { "Fros", "Glac", "Rime", "Shiv", "Sleet", "Brr" },
        [Element.Shade] = new[] { "Gloo", "Wisp", "Murk", "Dusk", "Shad", "Umbr" },
        [Element.Meadow] = new[] { "Wyld", "Thorn", "Brak", "Fen", "Hollo", "Grim" },
    };

    public static string Make(Species s, SeededRng rng)
    {
        string front = rng.Pick(Fronts[s.Element]);
        string back = rng.Pick(Backs(s));
        bool consonantJoin = !IsVowel(front[^1]) && !IsVowel(back[0]);
        string glue = consonantJoin && rng.Chance(0.6f) ? rng.Pick(new[] { "a", "o", "i", "u", "e" }) : "";
        return front + glue + back;
    }

    static string[] Backs(Species s)
    {
        var p = s.Parts;
        var options = new List<string>();
        switch (p.Horns)
        {
            case Horns.NoseHorn: case Horns.BrowHorn: case Horns.TwinHorns: options.AddRange(new[] { "horn", "ceros", "tusk", "prong" }); break;
            case Horns.Antlers: options.AddRange(new[] { "stag", "antle", "rack" }); break;
            case Horns.Crest: options.AddRange(new[] { "crest", "plume" }); break;
        }
        switch (p.Back)
        {
            case Back.Shell: options.AddRange(new[] { "shell", "carap", "dome" }); break;
            case Back.Spikes: options.AddRange(new[] { "spike", "quill", "barb" }); break;
            case Back.Sail: options.AddRange(new[] { "fin", "sail" }); break;
            case Back.Crystals: options.AddRange(new[] { "gem", "lith", "shard" }); break;
            case Back.Mane: options.AddRange(new[] { "mane", "fluff" }); break;
        }
        if (p.Wings is Wings.Bat or Wings.Feather) options.AddRange(new[] { "wing", "ptera", "flit" });
        if (p.Tail == Tail.Fluffy) options.AddRange(new[] { "puff", "tuft" });
        options.AddRange(s.Strain switch
        {
            Strain.Clockwork => new[] { "cog", "gear", "bolt", "rivet" },
            Strain.Chemtech => new[] { "chem", "tox", "ooze", "vat" },
            Strain.Arcane => new[] { "rune", "hex", "glyph", "mage" },
            Strain.Abyssal => new[] { "dread", "maw", "gloom", "wraith" },
            _ => System.Array.Empty<string>(),
        });
        options.AddRange(s.Plan switch
        {
            BodyPlan.Quadruped => new[] { "fang", "hound", "prowl", "claw" },
            BodyPlan.Biped => new[] { "brute", "golem", "ogre", "kin" },
            BodyPlan.Serpent => new[] { "coil", "ssa", "wyrm", "sly" },
            BodyPlan.Avian => new[] { "talon", "raptor", "hawk", "crow" },
            BodyPlan.Insectoid => new[] { "pede", "bug", "mite", "wasp" },
            BodyPlan.Aquatic => new[] { "fin", "gill", "carp", "ray" },
            _ => new[] { "ooze", "slime", "mire", "flan" },
        });
        return options.ToArray();
    }

    /// <summary>A bred name: the front of one parent's name and the back of the other's.</summary>
    public static string Splice(string a, string b, SeededRng rng)
    {
        if (rng.Chance(0.5f)) (a, b) = (b, a);
        // Front: through the first vowel run that ends at least three letters in.
        int i = 0;
        while (i < a.Length && !(i >= 2 && IsVowel(a[i - 1]) && !IsVowel(a[i]))) i++;
        string front = a[..Math.Max(3, Math.Min(i, a.Length))];
        // Back: from the last consonant before its final vowel run.
        int j = b.Length - 1;
        while (j > 0 && !IsVowel(b[j])) j--;
        while (j > 0 && IsVowel(b[j])) j--;
        string back = b[Math.Max(1, j)..].ToLowerInvariant();
        if (front.Length + back.Length < 5) back = b[(b.Length / 2)..].ToLowerInvariant();
        string name = front + back;
        return name.Length > 11 ? name[..11] : name;
    }

    static bool IsVowel(char c) => "aeiouyAEIOUY".IndexOf(c) >= 0;
}
