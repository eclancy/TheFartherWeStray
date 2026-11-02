using System;
using System.Collections.Generic;

namespace CreatureTactics.Creatures;

/// <summary>
/// A species, generated whole from one seed: body plan, parts, palette family, proportions and a
/// name. Every individual of the species shares these (game-ideas.md, "Species vs. individuals").
/// </summary>
public sealed class Species
{
    public ulong Seed;
    public string Name;
    public BodyPlan Plan;
    public Element Element;
    public SizeClass BaseSize;
    public PartSet Parts;
    public PaletteSpec Palette;
    public Attitude CommonAttitude;

    // Proportions, all around 1.
    public float BodyLen = 1, Thickness = 1, LegLen = 1, HeadSize = 1, NeckLen = 1, SnoutLen = 1, EyeSize = 1;
    public float WingSpread = 0.5f, HornCurve;
    public bool WingsFolded, MuzzleSecond;

    /// <summary>Set for hand-authored species (the DrawPets cast): who designed it.</summary>
    public string Origin;

    // Which palette material each part is coloured in. Generated species leave these null and the
    // painter rolls them; authored species can pin them (a ladybug's shell must be red).
    public Mat? SpikeMat, ShellMat, FeatherMat, SailMat, TuftMat, CrestMat;

    public static Species Generate(ulong seed)
    {
        var rng = new SeededRng(seed);
        var s = new Species { Seed = seed };
        s.Plan = rng.Weighted(
            (BodyPlan.Quadruped, 3f), (BodyPlan.Biped, 2.5f), (BodyPlan.Serpent, 1.5f), (BodyPlan.Avian, 1.5f),
            (BodyPlan.Insectoid, 1.2f), (BodyPlan.Aquatic, 1.2f), (BodyPlan.Blob, 1.5f));
        s.Element = (Element)rng.Int(Enum.GetValues<Element>().Length);
        s.BaseSize = rng.Weighted(
            (SizeClass.Tiny, 1.5f), (SizeClass.Small, 2.5f), (SizeClass.Medium, 3f), (SizeClass.Large, 2f), (SizeClass.Huge, 1f));
        s.CommonAttitude = (Attitude)rng.Int(Enum.GetValues<Attitude>().Length);

        s.BodyLen = rng.Range(0.85f, 1.18f);
        s.Thickness = rng.Range(0.85f, 1.2f);
        s.LegLen = rng.Range(0.8f, 1.2f);
        s.HeadSize = rng.Range(0.88f, 1.18f);
        s.NeckLen = rng.Range(0.6f, 1.4f);
        s.SnoutLen = rng.Range(0.7f, 1.3f);
        s.EyeSize = rng.Range(0.85f, 1.25f);
        s.WingSpread = rng.Float();
        s.HornCurve = rng.Range(-1f, 1f);
        s.WingsFolded = rng.Chance(0.5f);
        s.MuzzleSecond = rng.Chance(0.4f);

        s.Parts = RollParts(s.Plan, rng);
        s.Palette = PaletteSpec.Roll(s.Element, rng);
        s.Name = CreatureNames.Make(s, rng);
        return s;
    }

    /// <summary>Rolls every slot, with each body plan's own odds (fish don't get antlers often).</summary>
    public static PartSet RollParts(BodyPlan plan, SeededRng rng)
    {
        var head = rng.Weighted((HeadShape.Round, 3f), (HeadShape.Snout, plan == BodyPlan.Quadruped ? 4f : 1.5f), (HeadShape.Wide, 1.5f));
        var eyes = plan == BodyPlan.Insectoid
            ? rng.Weighted((EyeKind.Compound, 4f), (EyeKind.Round, 1f), (EyeKind.Beady, 1f))
            : rng.Weighted((EyeKind.Round, 5f), (EyeKind.Beady, 2f), (EyeKind.Compound, 0.3f));
        var ears = plan is BodyPlan.Avian or BodyPlan.Insectoid or BodyPlan.Aquatic or BodyPlan.Serpent
            ? rng.Weighted((Ears.None, 6f), (Ears.Pointy, 1f), (Ears.Round, 0.3f))
            : rng.Weighted((Ears.None, 2f), (Ears.Pointy, 3f), (Ears.Round, 2f), (Ears.Floppy, 1.5f));

        var horns = plan switch
        {
            BodyPlan.Insectoid => rng.Weighted((Horns.Antennae, 4f), (Horns.NoseHorn, 1.5f), (Horns.TwinHorns, 1.5f), (Horns.None, 1f)),
            BodyPlan.Avian => rng.Weighted((Horns.None, 3f), (Horns.Crest, 4f), (Horns.TwinHorns, 0.5f), (Horns.Antennae, 0.4f)),
            BodyPlan.Aquatic => rng.Weighted((Horns.None, 4f), (Horns.NoseHorn, 1.5f), (Horns.Antennae, 1.5f), (Horns.Crest, 1f)),
            _ => rng.Weighted((Horns.None, 3f), (Horns.NoseHorn, 1f), (Horns.BrowHorn, 0.7f), (Horns.TwinHorns, 2f), (Horns.Antlers, 1f), (Horns.Crest, 1.2f), (Horns.Antennae, 0.4f)),
        };

        var back = plan switch
        {
            BodyPlan.Aquatic => rng.Weighted((Back.Sail, 5f), (Back.Spikes, 1.5f), (Back.Shell, 0.7f), (Back.None, 1f)),
            BodyPlan.Insectoid => rng.Weighted((Back.Shell, 3f), (Back.Spikes, 1f), (Back.None, 3f)),
            BodyPlan.Serpent => rng.Weighted((Back.None, 3f), (Back.Spikes, 2f), (Back.Sail, 1.5f), (Back.Mane, 1f), (Back.Crystals, 1f)),
            BodyPlan.Avian => rng.Weighted((Back.None, 5f), (Back.Mane, 2f), (Back.Spikes, 0.6f), (Back.Crystals, 0.6f)),
            _ => rng.Weighted((Back.None, 3f), (Back.Spikes, 2f), (Back.Shell, 1.3f), (Back.Sail, 1f), (Back.Mane, 1.5f), (Back.Crystals, 1f)),
        };

        var limbs = plan switch
        {
            BodyPlan.Quadruped => rng.Weighted((Limbs.Stubby, 2f), (Limbs.Long, 2f), (Limbs.Clawed, 2f), (Limbs.Hooved, 1.5f)),
            BodyPlan.Blob => rng.Weighted((Limbs.Stubby, 2f), (Limbs.Long, 1f), (Limbs.Clawed, 1f)),
            _ => rng.Weighted((Limbs.Stubby, 2f), (Limbs.Long, 2f), (Limbs.Clawed, 2f)),
        };

        var tail = plan switch
        {
            BodyPlan.Aquatic => rng.Weighted((Tail.Fan, 5f), (Tail.Fluffy, 1.5f), (Tail.Whip, 1f)),
            BodyPlan.Avian => rng.Weighted((Tail.Fan, 5f), (Tail.Fluffy, 1f), (Tail.Whip, 1f)),
            BodyPlan.Insectoid => rng.Weighted((Tail.None, 3f), (Tail.Whip, 2f)),
            BodyPlan.Serpent => rng.Weighted((Tail.None, 3f), (Tail.Whip, 1.5f), (Tail.Club, 1f), (Tail.Fan, 1f), (Tail.Fluffy, 1f)),
            BodyPlan.Blob => rng.Weighted((Tail.None, 4f), (Tail.Curl, 1.5f), (Tail.Fluffy, 1f), (Tail.Whip, 1f)),
            _ => rng.Weighted((Tail.None, 1f), (Tail.Whip, 2f), (Tail.Thick, 2f), (Tail.Fluffy, 2f), (Tail.Club, 1f), (Tail.Curl, 1f)),
        };

        var wings = plan switch
        {
            BodyPlan.Avian => rng.Weighted((Wings.Feather, 6f), (Wings.Tiny, 2f), (Wings.Bat, 1f)),
            BodyPlan.Insectoid => rng.Weighted((Wings.Insect, 4f), (Wings.None, 3f)),
            BodyPlan.Aquatic => Wings.None,
            _ => rng.Weighted((Wings.None, 7f), (Wings.Bat, 1f), (Wings.Feather, 1f), (Wings.Tiny, 1f), (Wings.Insect, 0.3f)),
        };

        var pattern = plan == BodyPlan.Insectoid
            ? rng.Weighted((Pattern.Stripes, 3f), (Pattern.None, 1f), (Pattern.Spots, 1f), (Pattern.Saddle, 1f))
            : rng.Weighted((Pattern.None, 2f), (Pattern.Belly, 4f), (Pattern.Stripes, 1.2f), (Pattern.Spots, 1.2f),
                           (Pattern.Saddle, 1f), (Pattern.Tips, 1.2f), (Pattern.Cheeks, 1f));

        return new PartSet(head, ears, eyes, horns, back, limbs, tail, wings, pattern);
    }
}

/// <summary>
/// One individual: its species plus what varies inside a species (size, shade, attitude, proportion
/// wiggle) and the rare twists, a shiny palette or a mutated part.
/// </summary>
public sealed class Creature
{
    public Species Species;
    public ulong Seed;
    public SizeClass Size;
    public Attitude Attitude;
    public PartSet Parts;
    public CreaturePalette Palette;
    public bool Shiny;
    public string Mutation; // null, or a short note like "mutant tail"
    public float BodyJitter, LegJitter, HeadJitter;

    public static Creature Generate(Species species, ulong seed)
    {
        var rng = new SeededRng(seed);
        var c = new Creature { Species = species, Seed = seed, Parts = species.Parts };

        int size = (int)species.BaseSize + rng.Weighted((0, 7f), (-1, 1.5f), (1, 1.5f));
        c.Size = (SizeClass)Math.Clamp(size, 0, (int)SizeClass.Huge);
        c.Attitude = rng.Chance(0.55f) ? species.CommonAttitude : (Attitude)rng.Int(Enum.GetValues<Attitude>().Length);
        c.BodyJitter = rng.Range(0.95f, 1.05f);
        c.LegJitter = rng.Range(0.94f, 1.06f);
        c.HeadJitter = rng.Range(0.95f, 1.05f);

        var palette = species.Palette.Jitter(rng);
        c.Shiny = rng.Chance(1f / 24f);
        if (c.Shiny) palette = palette.Shiny(rng);
        c.Palette = palette.Build();

        if (rng.Chance(1f / 14f)) c.Mutate(rng);
        return c;
    }

    /// <summary>The individual of a species shown first: its "type specimen".</summary>
    public static Creature Specimen(ulong speciesSeed, int sibling = 0) => Specimen(Species.Generate(speciesSeed), sibling);

    /// <summary>
    /// Individual number <paramref name="sibling"/> of a species. An authored species' first individual
    /// is exactly as designed; its siblings vary like any other species'.
    /// </summary>
    public static Creature Specimen(Species species, int sibling)
    {
        if (species.Origin != null && sibling == 0)
        {
            return new Creature
            {
                Species = species, Seed = species.Seed, Parts = species.Parts, Size = species.BaseSize,
                Attitude = species.CommonAttitude, Palette = species.Palette.Build(),
                BodyJitter = 1, LegJitter = 1, HeadJitter = 1,
            };
        }
        return Generate(species, SeededRng.Derive(species.Seed, (ulong)sibling + 1));
    }

    void Mutate(SeededRng rng)
    {
        var donor = Species.RollParts(Species.Plan, rng);
        var p = Parts;
        var swaps = new List<(string, Func<PartSet, bool>, Action)>
        {
            ("horns", q => q.Horns != p.Horns, () => p.Horns = donor.Horns),
            ("back", q => q.Back != p.Back, () => p.Back = donor.Back),
            ("tail", q => q.Tail != p.Tail, () => p.Tail = donor.Tail),
            ("wings", q => q.Wings != p.Wings, () => p.Wings = donor.Wings),
            ("ears", q => q.Ears != p.Ears, () => p.Ears = donor.Ears),
            ("pattern", q => q.Pattern != p.Pattern, () => p.Pattern = donor.Pattern),
        };
        swaps.RemoveAll(s => !s.Item2(donor));
        if (swaps.Count == 0) return;
        var (name, _, apply) = swaps[rng.Int(swaps.Count)];
        apply();
        Parts = p;
        Mutation = "mutant " + name;
    }

    public string Footprint => SizeInfo.Footprint(Size);

    /// <summary>What the parts mean as FFTA equipment: weapon, armour, movement.</summary>
    public (string weapon, string armor, string move) Loadout()
    {
        var p = Parts;
        bool legs = Species.Plan is not (BodyPlan.Serpent or BodyPlan.Aquatic);
        string weapon =
            p.Horns is Horns.NoseHorn or Horns.BrowHorn or Horns.TwinHorns or Horns.Antlers ? $"{Words(p.Horns)} (charge)" :
            legs && p.Limbs == Limbs.Clawed ? "claws (slash)" :
            p.Tail == Tail.Club ? "tail club (smash)" :
            p.Tail == Tail.Whip ? "whip tail (reach 2)" :
            legs && p.Limbs == Limbs.Hooved ? "hooves (kick)" : "bite";
        string armor = p.Back switch
        {
            Back.Shell => "shell (heavy)",
            Back.Spikes => "spines (thorns)",
            Back.Crystals => "crystal hide",
            Back.Mane => "thick mane",
            _ => "bare hide",
        };
        string move =
            p.Wings is Wings.Bat or Wings.Feather or Wings.Insect ? "flies over gaps" :
            Species.Plan is BodyPlan.Aquatic or BodyPlan.Serpent ? "swims" :
            Species.Plan == BodyPlan.Insectoid ? "climbs" :
            p.Wings == Wings.Tiny ? "glides down ledges" : "walks";
        return (weapon, armor, move);
    }

    public static string Words(Enum e)
    {
        string s = e.ToString();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
            sb.Append(char.ToLowerInvariant(s[i]));
        }
        return sb.ToString();
    }
}
