using System.Collections.Generic;

namespace CreatureTactics.Creatures;

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
        [Element.Meadow] = new[] { "Bun", "Pip", "Mallo", "Dew", "Clov", "Hopp" },
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
        options.AddRange(s.Plan switch
        {
            BodyPlan.Quadruped => new[] { "paw", "hound", "lope", "pup" },
            BodyPlan.Biped => new[] { "kin", "bo", "mon", "ling" },
            BodyPlan.Serpent => new[] { "coil", "ssa", "wyrm", "sly" },
            BodyPlan.Avian => new[] { "beak", "chirp", "wren", "hawk" },
            BodyPlan.Insectoid => new[] { "pede", "bug", "mite", "wasp" },
            BodyPlan.Aquatic => new[] { "fin", "gill", "carp", "ray" },
            _ => new[] { "goo", "blob", "jel", "mo" },
        });
        return options.ToArray();
    }

    static bool IsVowel(char c) => "aeiouyAEIOUY".IndexOf(c) >= 0;
}
