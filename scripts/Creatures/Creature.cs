using System;
using System.Collections.Generic;
using System.Linq;

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

    public Strain Strain;
    public Graft Grafts;

    /// <summary>Its silhouette within the body plan, and its mystical flourish (see <see cref="FormOf"/>).</summary>
    public Form Form;
    public Aura Aura;

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
        s.HornCurve = rng.Range(-0.4f, 1f); // mostly swept back
        s.WingsFolded = rng.Chance(0.5f);
        s.MuzzleSecond = rng.Chance(0.4f);

        s.Parts = RollParts(s.Plan, rng);
        s.Palette = PaletteSpec.Roll(s.Element, rng);
        s.Strain = rng.Weighted((Strain.Natural, 4.5f), (Strain.Clockwork, 1.8f), (Strain.Chemtech, 1.5f), (Strain.Arcane, 1.2f), (Strain.Abyssal, 1f));
        s.Grafts = RollGrafts(s.Strain, rng);
        if (s.Strain == Strain.Abyssal) s.Palette = s.Palette.Abyssal(rng);
        s.Name = CreatureNames.Make(s, rng);
        // Rolled from their own stream, so every older roll above stays as it was.
        s.Form = FormOf(s);
        // A third of new species take one of the later forms instead, from a stream of their own.
        var later = LaterFormsFor(s.Plan);
        var lrng = new SeededRng(SeededRng.Derive(seed, 0xF0E4));
        if (later.Length > 0 && lrng.Chance(0.33f)) s.Form = later[lrng.Int(later.Length)];
        s.Aura = AuraOf(s);
        return s;
    }

    /// <summary>The forms each body plan can take.</summary>
    public static Form[] FormsFor(BodyPlan plan) => plan switch
    {
        BodyPlan.Quadruped => new[] { Form.Hound, Form.Stag, Form.Cat, Form.Drake, Form.Behemoth },
        BodyPlan.Biped => new[] { Form.Raptor, Form.Sentinel, Form.Wraith, Form.Imp },
        BodyPlan.Serpent => new[] { Form.Wyrm, Form.Lung },
        BodyPlan.Avian => new[] { Form.Hawk, Form.Crane, Form.Phoenix },
        BodyPlan.Insectoid => new[] { Form.Mantis, Form.Beetle, Form.Spider },
        BodyPlan.Aquatic => new[] { Form.Eel, Form.Ray, Form.Kelpie },
        _ => new[] { Form.Wisp, Form.Jelly, Form.Ooze },
    };

    /// <summary>Forms added after the first set. Only rolled for newly generated species.</summary>
    public static Form[] LaterFormsFor(BodyPlan plan) => plan switch
    {
        BodyPlan.Quadruped => new[] { Form.Hydra },
        BodyPlan.Biped => new[] { Form.Gargoyle },
        BodyPlan.Serpent => new[] { Form.Naga },
        BodyPlan.Insectoid => new[] { Form.Scorpion },
        BodyPlan.Aquatic => new[] { Form.Angler },
        BodyPlan.Blob => new[] { Form.Watcher },
        _ => System.Array.Empty<Form>(),
    };

    public static Form[] AllFormsFor(BodyPlan plan) => FormsFor(plan).Concat(LaterFormsFor(plan)).ToArray();

    /// <summary>Its form: the one it has, or one rolled from its seed (species saved before forms existed).</summary>
    public static Form FormOf(Species s)
    {
        var forms = FormsFor(s.Plan);
        if (s.Form != Form.Auto && System.Array.IndexOf(AllFormsFor(s.Plan), s.Form) >= 0) return s.Form;
        var rng = new SeededRng(SeededRng.Derive(s.Seed, 0xF0E3));
        // Behemoths are for big beasts; imps for small ones.
        return rng.Weighted(forms.Select(f => (f, f switch
        {
            Form.Behemoth => s.BaseSize >= SizeClass.Large ? 2.5f : 0.15f,
            Form.Imp => s.BaseSize <= SizeClass.Small ? 2.5f : 0.3f,
            Form.Sentinel => s.BaseSize >= SizeClass.Medium ? 1.5f : 0.6f,
            _ => 1f,
        })).ToArray());
    }

    /// <summary>Its aura: most strained creatures have one, and about half the natural ones.</summary>
    public static Aura AuraOf(Species s)
    {
        if (s.Aura != Aura.Auto) return s.Aura;
        var rng = new SeededRng(SeededRng.Derive(s.Seed, 0xA0A0));
        float none = s.Strain == Strain.Natural ? 4f : 1.5f;
        return rng.Weighted(
            (Aura.None, none),
            (Aura.Halo, s.Strain is Strain.Arcane or Strain.Clockwork ? 2f : 0.8f),
            (Aura.Shards, s.Element is Element.Frost or Element.Stone or Element.Spark ? 2f : 0.7f),
            (Aura.Sigil, s.Strain == Strain.Arcane ? 2f : 0.7f),
            (Aura.ThirdEye, s.Strain == Strain.Abyssal || s.Element == Element.Shade ? 2f : 0.6f),
            (Aura.Wisps, s.Element is Element.Ember or Element.Shade or Element.Venom || s.Strain == Strain.Chemtech ? 2f : 0.7f));
    }

    /// <summary>A copy to change (grafts) without touching other creatures that share this species.</summary>
    public Species Clone() => (Species)MemberwiseClone();

    /// <summary>How strongly the neon rims this species: natural beasts get none.</summary>
    public float RimStrength => Strain switch { Strain.Natural => 0, Strain.Abyssal => 0.6f, _ => 0.5f };

    /// <summary>Each strain's grafts: two or three of its own, so two clockwork beasts still differ.</summary>
    public static Graft RollGrafts(Strain strain, SeededRng rng)
    {
        Graft[] pool = strain switch
        {
            Strain.Clockwork => new[] { Graft.Plating, Graft.MetalLimb, Graft.Visor, Graft.Core },
            Strain.Chemtech => new[] { Graft.Canister, Graft.Veins, Graft.Drips, Graft.MetalLimb },
            Strain.Arcane => new[] { Graft.Runes, Graft.GlowTips, Graft.Core },
            Strain.Abyssal => new[] { Graft.ExtraEyes, Graft.Cracks, Graft.Drips, Graft.GlowTips },
            _ => System.Array.Empty<Graft>(),
        };
        Graft g = Graft.None;
        int want = pool.Length == 0 ? 0 : rng.Int(2, System.Math.Min(3, pool.Length));
        while (want > 0)
        {
            var pick = pool[rng.Int(pool.Length)];
            if ((g & pick) != 0) continue;
            g |= pick;
            want--;
        }
        return g;
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
                           (Pattern.Saddle, 1f), (Pattern.Tips, 1.2f), (Pattern.Markings, 1f));

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
    public PartSet? BirthParts;       // the parts it was born with, if the lab has grafted any (breeding uses these)
    public PaletteSpec Spec;          // this individual's colours (species colours plus drift)
    public CreaturePalette Palette;   // built from Spec
    public bool Shiny;
    public string Mutation; // null, or a short note like "mutant tail"
    public float BodyJitter, LegJitter, HeadJitter;

    // Lineage: 0 for wild-caught, parents' generation + 1 when bred at the ranch.
    public int Generation;
    public string ParentA, ParentB;

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
        c.Spec = palette;
        c.Palette = BuildPalette(palette, species);

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
                Attitude = species.CommonAttitude, Spec = species.Palette, Palette = BuildPalette(species.Palette, species),
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

    /// <summary>The part that is this creature's weapon (FFTA's weapon slot).</summary>
    public Weapon Weapon
    {
        get
        {
            var p = Parts;
            bool legs = Species.Plan is not (BodyPlan.Serpent or BodyPlan.Aquatic);
            return p.Horns is Horns.NoseHorn or Horns.BrowHorn or Horns.TwinHorns or Horns.Antlers ? Weapon.Horns
                : legs && p.Limbs == Limbs.Clawed ? Weapon.Claws
                : p.Tail == Tail.Club ? Weapon.Club
                : p.Tail == Tail.Whip ? Weapon.Whip
                : legs && p.Limbs == Limbs.Hooved ? Weapon.Hooves
                : Weapon.Bite;
        }
    }

    /// <summary>The part that is its armour.</summary>
    public Armor Armor => Parts.Back switch
    {
        Back.Shell => Armor.Shell,
        Back.Spikes => Armor.Spines,
        Back.Crystals => Armor.Crystal,
        Back.Mane => Armor.Mane,
        _ => Armor.Hide,
    };

    /// <summary>How it moves, from its wings or body plan (the accessory slot).</summary>
    public MoveType MoveType =>
        Parts.Wings is Wings.Bat or Wings.Feather or Wings.Insect ? MoveType.Fly
        : Species.Plan is BodyPlan.Aquatic or BodyPlan.Serpent ? MoveType.Swim
        : Species.Plan == BodyPlan.Insectoid ? MoveType.Climb
        : MoveType.Walk;

    /// <summary>What the parts mean as FFTA equipment: weapon, armour, movement.</summary>
    public (string weapon, string armor, string move) Loadout()
    {
        string weapon = Weapon switch
        {
            Weapon.Horns => $"{Words(Parts.Horns)} (charge, knockback)",
            Weapon.Claws => "claws (slash)",
            Weapon.Club => "tail club (smash)",
            Weapon.Whip => "whip tail (reach 2)",
            Weapon.Hooves => "hooves (kick, knockback)",
            _ => "fangs (bite)",
        };
        string armor = Armor switch
        {
            Armor.Shell => "shell (heavy)",
            Armor.Spines => "spines (thorns)",
            Armor.Crystal => "crystal hide",
            Armor.Mane => "thick mane",
            _ => "bare hide",
        };
        string move = MoveType switch
        {
            MoveType.Fly => "flies over gaps and water",
            MoveType.Swim => "swims",
            MoveType.Climb => "climbs cliffs",
            _ => "walks",
        };
        return (weapon, armor, move);
    }

    public static CreaturePalette BuildPalette(PaletteSpec spec, Species species)
    {
        var p = spec.Build();
        p.RimStrength = species.RimStrength;
        return p;
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
