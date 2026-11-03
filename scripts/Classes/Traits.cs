using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;

namespace TheFartherWeStray.Classes;

/// <summary>
/// Traits (the user's direction, 2026-11-02): sensible, biological quirks a creature is born with
/// and passes down when bred, natural in colour, the city side's way of making a creature strong.
/// Up to two each, each a small passive merged into <see cref="Tactics.Unit.Passive"/>. Wild
/// creatures are born with them (rolled from their seed), so recruits bring theirs.
/// </summary>
public static class TraitBook
{
    public const int Max = 2;

    public static readonly PassiveDef[] Traits =
    {
        new() { Id = "leathery_skin", Name = "Leathery Skin", Class = "Trait", Def = 1, HpPct = 0.05f, Text = "DEF +1; max HP +5%." },
        new() { Id = "keen_nose", Name = "Keen Nose", Class = "Trait", Hit = 8, Text = "+8% to hit." },
        new() { Id = "swift", Name = "Swift", Class = "Trait", Spd = 1, Text = "SPD +1." },
        new() { Id = "strong_jaw", Name = "Strong Jaw", Class = "Trait", Atk = 1, Text = "ATK +1." },
        new() { Id = "iron_stomach", Name = "Iron Stomach", Class = "Trait", NoPoison = true, Text = "Immune to poison." },
        new() { Id = "hard_head", Name = "Hard Head", Class = "Trait", NoStun = true, Text = "Can't be stunned." },
        new() { Id = "night_eyes", Name = "Night Eyes", Class = "Trait", Evade = 5, Text = "5% harder to hit." },
        new() { Id = "quick_healer", Name = "Quick Healer", Class = "Trait", Regen = 0.03f, Text = "Heals 3% of max HP each turn." },
        new() { Id = "big_heart", Name = "Big Heart", Class = "Trait", HpPct = 0.1f, Text = "Max HP +10%." },
        new() { Id = "hunters_instinct", Name = "Hunter's Instinct", Class = "Trait", Predator = 0.15f, Text = "15% more damage to foes under half HP." },
        new() { Id = "herd_born", Name = "Herd-Born", Class = "Trait", PackBond = true, Text = "ATK +1 for each ally beside it." },
        new() { Id = "sure_footed", Name = "Sure-Footed", Class = "Trait", Climb = 2, Text = "Steps up or down two levels." },
        new() { Id = "strong_swimmer", Name = "Strong Swimmer", Class = "Trait", Amphibious = true, Text = "Wades through water and chem." },
        new() { Id = "stout", Name = "Stout", Class = "Trait", Unstoppable = true, Text = "Can't be rooted or knocked back." },
    };

    /// <summary>A creature's birth traits, from its seed: about half have one, some two.</summary>
    public static List<string> Roll(ulong seed)
    {
        var rng = new SeededRng(SeededRng.Derive(seed, 0x7EA1));
        int count = rng.Chance(0.12f) ? 2 : rng.Chance(0.5f) ? 1 : 0;
        var picks = new List<string>();
        while (picks.Count < count)
        {
            string id = Traits[rng.Int(Traits.Length)].Id;
            if (!picks.Contains(id)) picks.Add(id);
        }
        return picks;
    }

    /// <summary>
    /// A child's traits: each of its parents' passes down half the time, up to two; a child left
    /// with none sometimes shows a new one.
    /// </summary>
    public static List<string> Inherit(IReadOnlyList<string> a, IReadOnlyList<string> b, SeededRng rng, Action<bool, string> passed, Action<string> mutated)
    {
        var child = new List<string>();
        foreach (var (id, fromA) in a.Select(t => (t, true)).Concat(b.Select(t => (t, false))))
        {
            if (child.Count >= Max || child.Contains(id) || !rng.Chance(0.5f)) continue;
            child.Add(id);
            passed(fromA, id);
        }
        if (child.Count == 0 && rng.Chance(0.15f))
        {
            string id = Traits[rng.Int(Traits.Length)].Id;
            child.Add(id);
            mutated(id);
        }
        return child;
    }
}
