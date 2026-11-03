using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Classes;

/// <summary>What has been done to a creature, from least to most.</summary>
public enum Tier { Stray, Thoroughbred, Infused, Chimera, Rigged, Abomination }

/// <summary>
/// Creature tiers (the user's design, from the voice call, 2026-11-02): a creature goes by the most
/// extreme thing done to it, and each step does what the user set out (2026-11-02). A stray is
/// uncaught; caught and left natural it's a thoroughbred, with a bonus from its species (its form's
/// ability and its element's blood). Infused: its natural stats tuned at the lab (InfusionBook), or
/// new blood put in. A chimera carries the abilities of other species (bred across strains, an
/// ability spliced in at the lab, or a grafted part). Rigged: augments, abilities no natural creature
/// has. All three at once make an abomination. New blood and chimera DNA pass down; infusions,
/// grafted parts and augments don't. The undercity goes with altering creatures, the city above with
/// breeding them true.
/// </summary>
public static class TierBook
{
    /// <summary>
    /// A thoroughbred's bonus comes from its species (the user's call, 2026-11-02): its form, the kind
    /// of beast it is, at its best, and a touch of its element's blood. Two halves, so species of one
    /// form still differ. New forms want an entry here.
    /// </summary>
    static readonly Dictionary<Form, PassiveDef> FormBonus = new()
    {
        [Form.Hound] = new() { Name = "Pack Blood", PackBond = true, Spd = 1, Text = "ATK +1 for each ally beside it, SPD +1" },
        [Form.Stag] = new() { Name = "Antlered Pride", Def = 1, Climb = 2, Text = "DEF +1, steps up or down two levels" },
        [Form.Cat] = new() { Name = "Night Stalker", Evade = 8, Predator = 0.1f, Text = "8% harder to hit, 10% more damage to foes under half HP" },
        [Form.Drake] = new() { Name = "Dragon Hide", Def = 2, Text = "DEF +2" },
        [Form.Behemoth] = new() { Name = "Mountain-Born", HpPct = 0.1f, Unstoppable = true, Text = "max HP +10%, can't be rooted or knocked back" },
        [Form.Hydra] = new() { Name = "Many Heads", Atk = 1, Regen = 0.03f, Text = "ATK +1, heals 3% of max HP each turn" },
        [Form.Raptor] = new() { Name = "Hunter's Stride", Spd = 1, Predator = 0.1f, Text = "SPD +1, 10% more damage to foes under half HP" },
        [Form.Sentinel] = new() { Name = "Stone Watch", Def = 1, NoStun = true, Text = "DEF +1, can't be stunned" },
        [Form.Wraith] = new() { Name = "Unseen", Evade = 8, Drain = 0.1f, Text = "8% harder to hit, heals 10% of its basic attacks' damage" },
        [Form.Imp] = new() { Name = "Quick Wits", Spd = 1, Hit = 8, Text = "SPD +1, +8% to hit" },
        [Form.Gargoyle] = new() { Name = "Living Stone", Def = 1, Thorns = 2, Text = "DEF +1, melee attackers take 2" },
        [Form.Wyrm] = new() { Name = "Coiled Strength", Atk = 1, HpPct = 0.05f, Text = "ATK +1, max HP +5%" },
        [Form.Lung] = new() { Name = "Sky Dancer", Spd = 1, SkillPower = 0.1f, Text = "SPD +1, skills hit 10% harder" },
        [Form.Naga] = new() { Name = "Old Blood", Def = 1, SkillPower = 0.1f, Text = "DEF +1, skills hit 10% harder" },
        [Form.Hawk] = new() { Name = "Keen Eye", Hit = 10, RangeBonus = 1, Text = "+10% to hit, ranged skills reach 1 further" },
        [Form.Crane] = new() { Name = "Grace", Spd = 1, Evade = 8, Text = "SPD +1, 8% harder to hit" },
        [Form.Phoenix] = new() { Name = "Undying Ember", Regen = 0.05f, Text = "heals 5% of max HP each turn" },
        [Form.Cockatrice] = new() { Name = "Basilisk Blood", Atk = 1, NoPoison = true, Text = "ATK +1, immune to poison" },
        [Form.Mantis] = new() { Name = "Blade Arms", Atk = 1, Predator = 0.1f, Text = "ATK +1, 10% more damage to foes under half HP" },
        [Form.Beetle] = new() { Name = "Iron Carapace", Def = 1, Unstoppable = true, Text = "DEF +1, can't be rooted or knocked back" },
        [Form.Spider] = new() { Name = "Web-Walker", Hit = 8, Climb = 2, Text = "+8% to hit, steps up or down two levels" },
        [Form.Scorpion] = new() { Name = "Stinger", Atk = 1, Thorns = 2, Text = "ATK +1, melee attackers take 2" },
        [Form.Eel] = new() { Name = "Slippery", Evade = 8, Amphibious = true, Text = "8% harder to hit, wades through water and chem" },
        [Form.Ray] = new() { Name = "Glide", Spd = 1, Amphibious = true, Text = "SPD +1, wades through water and chem" },
        [Form.Kelpie] = new() { Name = "Tide Runner", Move = 1, Text = "MOVE +1" },
        [Form.Angler] = new() { Name = "Lure", Hit = 8, Drain = 0.1f, Text = "+8% to hit, heals 10% of its basic attacks' damage" },
        [Form.Wisp] = new() { Name = "Flicker", Evade = 12, Text = "12% harder to hit" },
        [Form.Jelly] = new() { Name = "Stinging Bell", Thorns = 2, HpPct = 0.05f, Text = "melee attackers take 2, max HP +5%" },
        [Form.Ooze] = new() { Name = "Formless", HpPct = 0.1f, NoPoison = true, Text = "max HP +10%, immune to poison" },
        [Form.Watcher] = new() { Name = "All-Seeing", Hit = 10, Evade = 5, Text = "+10% to hit, 5% harder to hit" },
    };

    static readonly PassiveDef Hardy = new() { Name = "Hardy", Atk = 1, Def = 1, Text = "ATK +1, DEF +1" };

    /// <summary>Each element's half: a touch of what its blood is.</summary>
    static readonly Dictionary<Element, PassiveDef> ElementBonus = new()
    {
        [Element.Ember] = new() { Atk = 1, Text = "ATK +1" },
        [Element.Tide] = new() { HpPct = 0.05f, Amphibious = true, Text = "max HP +5%, wades through water and chem" },
        [Element.Leaf] = new() { Regen = 0.03f, Text = "heals 3% of max HP each turn" },
        [Element.Spark] = new() { Spd = 1, Text = "SPD +1" },
        [Element.Stone] = new() { Def = 1, Text = "DEF +1" },
        [Element.Venom] = new() { Hit = 5, NoPoison = true, Text = "+5% to hit, immune to poison" },
        [Element.Frost] = new() { Evade = 5, HpPct = 0.05f, Text = "5% harder to hit, max HP +5%" },
        [Element.Shade] = new() { Evade = 8, Text = "8% harder to hit" },
        [Element.Meadow] = new() { HpPct = 0.1f, Text = "max HP +10%" },
    };

    static readonly Dictionary<(Form, Element), PassiveDef> _made = new();

    /// <summary>A thoroughbred's bonus for its species: its form's strength and its element's blood, as one passive.</summary>
    public static PassiveDef ThoroughbredOf(Species sp)
    {
        var form = Species.FormOf(sp);
        lock (_made)
        {
            if (_made.TryGetValue((form, sp.Element), out var made)) return made;
            var f = FormBonus.GetValueOrDefault(form, Hardy);
            var e = ElementBonus[sp.Element];
            var p = PassiveDef.Merge(f, e);
            p.Id = $"thoroughbred_{form}_{sp.Element}".ToLowerInvariant();
            p.Name = $"Thoroughbred: {f.Name}";
            p.Class = "Tier";
            p.Text = $"Kept natural since it was caught. {f.Name}: {f.Text}. {sp.Element} blood: {e.Text}. "
                + "Infusing, crossing strains, splicing or rigging it ends this.";
            return _made[(form, sp.Element)] = p;
        }
    }

    /// <summary>A form's ability, by name and in words: what a thoroughbred has at its best, and what a chimera can carry.</summary>
    public static string Ability(Form f) => FormBonus.GetValueOrDefault(f, Hardy).Name;
    public static PassiveDef AbilityOf(Form f) => FormBonus.GetValueOrDefault(f, Hardy);
    public static string AbilityText(Form f) => FormBonus.GetValueOrDefault(f, Hardy).Text;

    /// <summary>How many other species' abilities a chimera can carry, and what the lab charges to splice one in.</summary>
    public const int MaxSpliced = 2, SpliceCost = 50;

    static readonly Dictionary<string, PassiveDef> _chimeras = new();

    /// <summary>
    /// A chimera's abilities (the user's call, 2026-11-02: "chimera can give the abilities of different
    /// species"): its own form's and each spliced-in species' (bred in from a parent of another kind, or
    /// spliced at the lab), without a thoroughbred's touch of element blood.
    /// </summary>
    public static PassiveDef ChimeraOf(Species sp)
    {
        var forms = new[] { Species.FormOf(sp) }.Concat(sp.Spliced ?? System.Array.Empty<Form>()).Distinct().ToList();
        string key = string.Join("_", forms).ToLowerInvariant();
        lock (_chimeras)
        {
            if (_chimeras.TryGetValue(key, out var made)) return made;
            var parts = forms.Select(f => FormBonus.GetValueOrDefault(f, Hardy)).ToList();
            var p = PassiveDef.Merge(parts.Aggregate(PassiveDef.Merge), new PassiveDef()); // a copy, never one of the table's own
            p.Id = "chimera_" + key;
            p.Name = "Chimera: " + string.Join(" + ", parts.Select(x => x.Name));
            p.Class = "Tier";
            p.Text = $"The abilities of {(forms.Count == 1 ? "its own species only" : $"{forms.Count} species")}: "
                + string.Join("; ", forms.Zip(parts, (f, x) => $"{x.Name} ({f.ToString().ToLowerInvariant()}): {x.Text}")) + ".";
            return _chimeras[key] = p;
        }
    }

    // What has been done to it. Infused: its stats tuned (InfusionBook) or new blood (Lab.Infusions).
    // Chimera: other species' DNA (crossed strains, spliced abilities, a grafted part). Rigged: augments,
    // which give it abilities no natural creature has (and a rival's machine grafts).
    public static bool Infused(Creature c) => c.Species.Infused || c.Infusions.Any(i => InfusionBook.Get(i) != null);
    public static bool Chimera(Creature c) => c.Species.Chimera || c.Species.Spliced is { Length: > 0 } || c.BirthParts is { } born && !born.Equals(c.Parts);
    public static bool Rigged(Creature c) => c.Species.Rigged || c.Augments.Any(a => AugmentBook.Get(a) != null);

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

    /// <summary>
    /// An abomination's perk (the user's calls, 2026-11-02: a perk of its own with an unstable cost,
    /// the instability between missions rather than losing control in battle): big stats, and after
    /// each battle it fights, a chance of an episode (<see cref="Ranching.Ranch.AfterBattle"/>).
    /// </summary>
    public static readonly PassiveDef Unstable = new()
    {
        Id = "unstable", Name = "Unstable", Atk = 3, Def = 2, Spd = 1, HpPct = 0.1f,
        Text = "ATK +3, DEF +2, SPD +1, max HP +10%. Unstable: after a battle it fought, 1 in 4 it has an episode: it needs the next battle to recover, its body shifts, or an infusion sours (or its rigs seize).",
    };

    /// <summary>The chance of an abomination's episode after each battle it fights.</summary>
    public const float EpisodeChance = 0.25f;

    static readonly Dictionary<string, PassiveDef> _abominations = new();

    /// <summary>An abomination's passive: its chimera abilities and the Unstable perk, as one.</summary>
    public static PassiveDef AbominationOf(Species sp)
    {
        var chimera = ChimeraOf(sp);
        lock (_abominations)
        {
            if (_abominations.TryGetValue(chimera.Id, out var made)) return made;
            var p = PassiveDef.Merge(chimera, Unstable);
            p.Id = "abomination_" + chimera.Id.Substring("chimera_".Length);
            p.Name = "Abomination: Unstable";
            p.Class = "Tier";
            p.Text = $"{Unstable.Text} {chimera.Text}";
            return _abominations[chimera.Id] = p;
        }
    }

    /// <summary>The passive its tier brings: a thoroughbred's species bonus, a chimera's abilities, an abomination's perk on top (null for the rest).</summary>
    public static PassiveDef PassiveOf(Creature c, Tier t) => t switch
    {
        Tier.Thoroughbred => ThoroughbredOf(c.Species),
        Tier.Abomination => AbominationOf(c.Species),
        _ => Chimera(c) ? ChimeraOf(c.Species) : null,
    };

    // The costs of altering a creature (the user's yes to the call's first draft, 2026-11-02): a new
    // infusion steeps (it sits out the next battle), rigs need upkeep after every battle they fight,
    // and rigged creatures grow slower (less JP), an abomination slowest.
    public const int UpkeepPerRig = 3;
    public static float GrowthRate(Creature c) => Of(c) switch { Tier.Abomination => 0.6f, Tier.Rigged => 0.75f, _ => 1f };

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
            Tier.Thoroughbred => ThoroughbredOf(c.Species).Text,
            Tier.Infused => "Infused: its natural stats tuned at the lab, or new blood put in. No thoroughbred bonus.",
            Tier.Chimera => $"A chimera. {ChimeraOf(c.Species).Text} No thoroughbred bonus.",
            Tier.Rigged => $"Rigged: augments bolted on, with abilities no natural creature has. Upkeep {UpkeepPerRig} chem a rig after each battle; learns at 3/4 pace. No thoroughbred bonus.",
            _ => $"An abomination: infused, a chimera and rigged, as far from natural as a creature goes. {AbominationOf(c.Species).Text}",
        };
        var also = new[] { (t is Tier.Chimera or Tier.Rigged && Infused(c), "infused"), (t == Tier.Rigged && Chimera(c), "a chimera") }
            .Where(x => x.Item1).Select(x => x.Item2).ToList();
        return also.Count > 0 ? $"{what} Also {string.Join(" and ", also)}." : what;
    }
}

/// <summary>
/// Infusions (the user's direction, 2026-11-02: "infusion can adjust its natural stats"): draughts
/// brewed at the lab that tune a creature's stats, each an upside with a small trade-off, two to a
/// creature like an equipment panel; a third washes out the oldest. They make it infused and don't
/// pass down (new blood, the lab's other infusion, does). Data: new ones are entries.
/// </summary>
public static class InfusionBook
{
    public const int Slots = 2, Cost = 30;

    public static readonly PassiveDef[] All =
    {
        new() { Id = "rage_serum", Name = "Rage Serum", Class = "Infusion", Atk = 2, Def = -1, Text = "ATK +2, DEF -1." },
        new() { Id = "iron_tincture", Name = "Iron Tincture", Class = "Infusion", Def = 2, Spd = -1, Text = "DEF +2, SPD -1." },
        new() { Id = "quickblood", Name = "Quickblood", Class = "Infusion", Spd = 2, HpPct = -0.1f, Text = "SPD +2, max HP -10%." },
        new() { Id = "bulk_tonic", Name = "Bulk Tonic", Class = "Infusion", HpPct = 0.2f, Spd = -1, Text = "Max HP +20%, SPD -1." },
        new() { Id = "keen_drops", Name = "Keen Drops", Class = "Infusion", Hit = 12, Evade = -4, Text = "+12% to hit, 4% easier to hit." },
        new() { Id = "ghost_vapour", Name = "Ghost Vapour", Class = "Infusion", Evade = 10, Def = -1, Text = "10% harder to hit, DEF -1." },
        new() { Id = "stride_elixir", Name = "Stride Elixir", Class = "Infusion", Move = 1, HpPct = -0.1f, Text = "MOVE +1, max HP -10%." },
        new() { Id = "mending_draught", Name = "Mending Draught", Class = "Infusion", Regen = 0.04f, Atk = -1, Text = "Heals 4% of max HP each turn, ATK -1." },
    };

    public static PassiveDef Get(string id) => All.FirstOrDefault(p => p.Id == id);
}
