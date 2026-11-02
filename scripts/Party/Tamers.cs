using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Party;

/// <summary>
/// A tamer: a member of your party who leads up to three creatures into battle from off the
/// field (the user's direction, 2026-11-02). Each has one specialty, a passive boost to the
/// creatures they lead, and some only reach certain creatures. Data, like skills and passives.
///
/// "Tamer" is the user's name for them (2026-11-02): friendlier to the creatures, someone who
/// collects and works with them, like a Pokémon trainer. The names here are titles standing in for
/// the characters the story will introduce, and <see cref="JoinsAt"/> stands in for story beats.
/// </summary>
public sealed class TamerDef
{
    public string Id, Name, Specialty, Text, Glyph;
    public int JoinsAt;                        // the encounter they join at (a placeholder for the story)
    public Func<Creature, bool> Leads;         // which creatures the boost reaches; null for every one
    public string LeadsHint;
    public int Atk, Def, Spd, Move, Evade, Hit, Thorns;
    public float HpPct, SkillPower, Regen;

    public bool Boosts(Creature c) => Leads == null || Leads(c);
}

public static class TamerBook
{
    public const string You = "you";
    public const int CreaturesEach = 3, MaxFielded = 2;
    const Graft Brass = Graft.Plating | Graft.MetalLimb | Graft.Visor | Graft.Core;

    public static readonly TamerDef[] All =
    {
        new() { Id = You, Name = "You", Specialty = "Steady Hand", Glyph = "crown", Atk = 1, Def = 1,
            Text = "ATK +1 and DEF +1 for every creature you lead." },
        new() { Id = "beastmaster", Name = "The Beastmaster", Specialty = "Call of the Wild", Glyph = "paw", JoinsAt = 3,
            Leads = c => c.Species.Strain == Strain.Natural, LeadsHint = "natural creatures", Atk = 2, HpPct = 0.1f,
            Text = "Natural creatures: ATK +2 and max HP +10%." },
        new() { Id = "tinker", Name = "The Tinker", Specialty = "Fine Tuning", Glyph = "gear", JoinsAt = 6,
            Leads = c => c.Species.Strain == Strain.Clockwork || (c.Species.Grafts & Brass) != 0, LeadsHint = "clockwork or brass-grafted creatures",
            Def = 3, Spd = 1, Text = "Clockwork and brass-grafted creatures: DEF +3 and SPD +1." },
        new() { Id = "chemist", Name = "The Chemist", Specialty = "Volatile Doses", Glyph = "flask", JoinsAt = 9,
            Leads = c => c.Species.Strain is Strain.Chemtech or Strain.Abyssal, LeadsHint = "chemborn or abyssal creatures",
            SkillPower = 0.15f, Regen = 0.03f, Text = "Chemborn and abyssal creatures: skills hit 15% harder; heal 3% of max HP each turn." },
        new() { Id = "arcanist", Name = "The Arcanist", Specialty = "Ley Sight", Glyph = "rune", JoinsAt = 12,
            Leads = c => c.Species.Strain == Strain.Arcane || Species.AuraOf(c.Species) != Aura.None, LeadsHint = "runic creatures, or any with an aura",
            SkillPower = 0.2f, Evade = 5, Text = "Runic creatures and any with an aura: skills hit 20% harder; 5% harder to hit." },
        new() { Id = "drillmaster", Name = "The Drillmaster", Specialty = "Forced March", Glyph = "move", JoinsAt = 15,
            Move = 1, Spd = 1, Text = "MOVE +1 and SPD +1 for every creature it leads." },
    };

    static readonly Dictionary<string, TamerDef> ById = All.ToDictionary(w => w.Id);
    public static TamerDef Get(string id) => id != null && ById.TryGetValue(id, out var w) ? w : null;

    /// <summary>Everyone in the party by this encounter.</summary>
    public static IEnumerable<TamerDef> Joined(ulong encounter) => All.Where(w => (ulong)w.JoinsAt <= encounter);
}
