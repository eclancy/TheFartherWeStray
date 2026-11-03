using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Classes;

/// <summary>What has been done to a creature, from least to most.</summary>
public enum Tier { Stray, Thoroughbred, Infused, Chimera, Rigged, Abomination }

/// <summary>
/// Creature tiers (the user's design, from the voice call, 2026-11-02): a creature goes by the most
/// extreme thing done to it. A stray is uncaught; caught and left natural it's a thoroughbred, with a
/// passive bonus for being kept so. Chemically altered it's infused (a lab infusion or a chemical
/// augment); its DNA altered, a chimera (bred from two blood strains, or a part spliced on at the
/// lab); with machinery bolted on, rigged (a mechanical augment). All three at once make an
/// abomination. Infused blood and chimera blood pass down; splices and augments don't. The
/// undercity goes with altering creatures, the city above with breeding them true.
/// </summary>
public static class TierBook
{
    public static readonly PassiveDef Thoroughbred = new()
    {
        Id = "thoroughbred", Name = "Thoroughbred", Class = "Tier", Atk = 1, Def = 1, HpPct = 0.1f,
        Text = "Kept natural since it was caught: ATK +1, DEF +1, max HP +10%. Infusing, crossing strains, splicing or rigging it ends this.",
    };

    public static bool Infused(Creature c) => c.Species.Infused || c.Augments.Any(a => AugmentBook.Get(a) is { Chem: true });
    public static bool Chimera(Creature c) => c.Species.Chimera || c.BirthParts is { } born && !born.Equals(c.Parts);
    public static bool Rigged(Creature c) => c.Species.Rigged || c.Augments.Any(a => AugmentBook.Get(a) is { Chem: false });

    /// <summary>Its tier: the most extreme thing done to it. Nothing done: a thoroughbred if it's been caught, else a stray.</summary>
    public static Tier Of(Creature c, bool tamed = true)
    {
        bool infused = Infused(c), chimera = Chimera(c), rigged = Rigged(c);
        if (infused && chimera && rigged) return Tier.Abomination;
        if (rigged) return Tier.Rigged;
        if (chimera) return Tier.Chimera;
        if (infused) return Tier.Infused;
        return tamed ? Tier.Thoroughbred : Tier.Stray;
    }

    /// <summary>The tier in words, as a sentence would use it ("a chimera", "rigged").</summary>
    public static string Words(Tier t) => t switch
    {
        Tier.Stray => "a stray", Tier.Thoroughbred => "a thoroughbred", Tier.Chimera => "a chimera", Tier.Abomination => "an abomination",
        _ => t.ToString().ToLowerInvariant(),
    };

    public static string Glyph(Tier t) => t switch
    {
        Tier.Stray => "paw", Tier.Thoroughbred => "crown", Tier.Infused => "flask", Tier.Chimera => "helix", Tier.Rigged => "gear", _ => "skull",
    };

    public static string Hex(Tier t) => t switch
    {
        Tier.Stray => "#b8ad9c", Tier.Thoroughbred => "#9ac25a", Tier.Infused => "#4ff0a0", Tier.Chimera => "#c08cff", Tier.Rigged => "#4fe3ff", _ => "#ff5a6a",
    };

    /// <summary>What its tier means, for a tooltip, with anything else done to it that the tier doesn't name.</summary>
    public static string Describe(Creature c, Tier t)
    {
        string what = t switch
        {
            Tier.Stray => "A stray: nobody has caught it.",
            Tier.Thoroughbred => Thoroughbred.Text,
            Tier.Infused => "Infused: chemically altered (a lab infusion or a chemical augment). No thoroughbred bonus.",
            Tier.Chimera => "A chimera: its DNA altered (bred from two strains, or a part spliced on at the lab). No thoroughbred bonus.",
            Tier.Rigged => "Rigged: machinery bolted on (a mechanical augment). No thoroughbred bonus.",
            _ => "An abomination: infused, a chimera and rigged, as far from natural as a creature goes.",
        };
        var also = new[] { (t is Tier.Chimera or Tier.Rigged && Infused(c), "infused"), (t == Tier.Rigged && Chimera(c), "a chimera") }
            .Where(x => x.Item1).Select(x => x.Item2).ToList();
        return also.Count > 0 ? $"{what} Also {string.Join(" and ", also)}." : what;
    }
}
