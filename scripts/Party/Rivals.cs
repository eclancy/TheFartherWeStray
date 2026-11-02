using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;

namespace CreatureTactics.Party;

/// <summary>
/// A rival tamer (the user's pick, 2026-11-02): someone breeding toward their own ideal creature,
/// who challenges you to a duel on the mission board. They lead from off the field like you, with a
/// boost of their own, and field trained creatures that can't be recruited. Data, like tamers.
///
/// The titles stand in for the characters the story will introduce, and <see cref="AppearsAt"/>
/// stands in for story beats, as with your own party.
/// </summary>
public sealed class RivalDef
{
    public TamerDef Tamer;                 // name, glyph and boost, the same as your own tamers'
    public string Ideal;                   // what they breed toward, for the mission card
    public Func<Species, float> Taste;     // how much they want a species in their squad (0: never)
    public Strain Infuse;                  // natural creatures they take get this blood (Natural: none)
    public Graft Grafts;                   // machine grafts they fit: to their ace, and one more creature each time you beat them
    public string[] Classes;               // the jobs they train toward, favourite first
    public string[] Turf;                  // the maps they choose for a duel
    public int AppearsAt;                  // the first encounter they challenge you (a placeholder for the story)

    public string Id => Tamer.Id;
    public string Name => Tamer.Name;
}

public static class RivalBook
{
    public static readonly RivalDef[] All =
    {
        new()
        {
            Tamer = new() { Id = "brass_baron", Name = "The Brass Baron", Specialty = "Precision Gears", Glyph = "chain",
                Leads = c => c.Species.Strain == Strain.Clockwork || (c.Species.Grafts & Ranching.Lab.MachineGrafts) != 0,
                LeadsHint = "clockwork or brass-fitted creatures", Spd = 1, Hit = 10,
                Text = "Clockwork and brass-fitted creatures: SPD +1 and +10 to hit." },
            Ideal = "Breeds armoured clockwork, plated in brass.", AppearsAt = 2,
            Taste = s => s.Strain == Strain.Clockwork ? 6 : s.Strain == Strain.Natural ? 1 : 0.3f,
            Grafts = Graft.Plating | Graft.Visor,
            Classes = new[] { "Artificer", "Hexwarden", "Deadeye", "Bulwark", "Juggernaut", "Tinker", "Guardian", "Mystic", "Hunter" },
            Turf = new[] { "Brass Bridges", "Clocktower Ruins" },
        },
        new()
        {
            Tamer = new() { Id = "chem_witch", Name = "The Chem Witch", Specialty = "Bubbling Vials", Glyph = "poison",
                Leads = c => c.Species.Strain is Strain.Chemtech or Strain.Abyssal, LeadsHint = "chemtech or abyssal creatures",
                SkillPower = 0.2f, Spd = 1, Text = "Chemtech and abyssal creatures: skills hit 20% harder; SPD +1." },
            Ideal = "Breeds creatures brimming with glowing chem.", AppearsAt = 4,
            Taste = s => s.Strain == Strain.Chemtech ? 6 : s.Strain == Strain.Abyssal ? 2 : s.Strain == Strain.Natural ? 1 : 0,
            Infuse = Strain.Chemtech,
            Classes = new[] { "Chemist", "Venomtail", "Swarmlord", "Stalker", "Alchemist", "Rogue", "Hunter" },
            Turf = new[] { "Sump Marsh", "Undercity Foundry" },
        },
        new()
        {
            Tamer = new() { Id = "huntress", Name = "The Huntress", Specialty = "Blood Scent", Glyph = "claw",
                Leads = c => c.Species.Strain == Strain.Natural, LeadsHint = "natural creatures", Atk = 2, Spd = 1,
                Text = "Natural creatures: ATK +2 and SPD +1." },
            Ideal = "Breeds the perfect predator: claws, horns and speed.", AppearsAt = 6,
            Taste = s => (s.Strain == Strain.Natural ? 4 : 0.2f) * (s.Plan is BodyPlan.Quadruped or BodyPlan.Avian ? 2 : 1)
                * (s.Parts.Limbs == Limbs.Clawed || s.Parts.Horns != Horns.None ? 2 : 1),
            Classes = new[] { "Packleader", "Stalker", "Champion", "Skydiver", "Cavalier", "Juggernaut", "Hunter", "Skirmisher", "Brawler", "Howler" },
            Turf = new[] { "Gilded Gardens", "Hexlight Hollow" },
        },
        new()
        {
            Tamer = new() { Id = "occultist", Name = "The Occultist", Specialty = "Old Rites", Glyph = "eye",
                Leads = c => c.Species.Strain is Strain.Arcane or Strain.Abyssal || Species.AuraOf(c.Species) != Aura.None,
                LeadsHint = "arcane or abyssal creatures, or any with an aura", SkillPower = 0.15f, Evade = 8,
                Text = "Arcane and abyssal creatures, and any with an aura: skills hit 15% harder; 8% harder to hit." },
            Ideal = "Breeds beasts touched by old magic: runes, halos, third eyes.", AppearsAt = 8,
            Taste = s => (s.Strain == Strain.Arcane ? 5 : s.Strain == Strain.Abyssal ? 2 : s.Strain == Strain.Natural ? 0.6f : 0.2f)
                * (Species.AuraOf(s) != Aura.None ? 3 : 1),
            Infuse = Strain.Arcane,
            Classes = new[] { "Runeweaver", "Saint", "Hexmark", "Oculus", "Seer", "Lurelight", "Hexwarden", "Mystic", "Howler", "Hunter" },
            Turf = new[] { "Hexlight Hollow", "Lantern Row" },
        },
        new()
        {
            Tamer = new() { Id = "wyrm_lord", Name = "The Wyrm Lord", Specialty = "Giant's Blood", Glyph = "fang",
                Leads = c => c.Size >= SizeClass.Large, LeadsHint = "large and huge creatures", HpPct = 0.1f, Atk = 1,
                Text = "Large and huge creatures: max HP +10% and ATK +1." },
            Ideal = "Breeds the biggest beasts alive: wyrms, drakes and hydras.", AppearsAt = 10,
            Taste = s => (s.BaseSize >= SizeClass.Large ? 4 : s.BaseSize == SizeClass.Medium ? 1 : 0.1f)
                * (Species.FormOf(s) is Form.Wyrm or Form.Lung or Form.Drake or Form.Behemoth or Form.Hydra or Form.Naga ? 3 : 1),
            Classes = new[] { "Titan", "Hydrarch", "Leviathan", "Juggernaut", "Bonebreaker", "Voidcaller", "Brawler", "Guardian" },
            Turf = new[] { "Sump Marsh", "Clocktower Ruins" },
        },
    };

    static readonly Dictionary<string, RivalDef> ById = All.ToDictionary(r => r.Id);
    public static RivalDef Get(string id) => id != null && ById.TryGetValue(id, out var r) ? r : null;

    /// <summary>
    /// Who challenges you on this board, if anyone: a rival on their first encounter, then one of those
    /// you've met on every other board.
    /// </summary>
    public static RivalDef Challenger(ulong encounter)
    {
        if (All.FirstOrDefault(r => (ulong)r.AppearsAt == encounter) is { } debut) return debut;
        var met = All.Where(r => (ulong)r.AppearsAt <= encounter).ToList();
        return met.Count == 0 || encounter % 2 != 0 ? null : met[(int)(encounter / 2 % (ulong)met.Count)];
    }

    /// <summary>As many creatures as you can field by then: three, or six once a second tamer has joined you.</summary>
    public static int SquadSize(ulong encounter) => TamerBook.CreaturesEach * Math.Min(TamerBook.MaxFielded, TamerBook.Joined(encounter).Count());

    // How far a rival trains (eased 2026-11-02, when AI-played squads won about two duels in five):
    // JP per encounter so far, never past the average of the squad you bring; more for their ace and
    // for each time you've beaten them.
    public const int JpPerEncounter = 10, AceJp = 10, JpPerWin = 10;

    /// <summary>
    /// How many creatures fewer a rival brings for the duels you've lost to them in a row: cocky after
    /// beating you, one fewer, then two. Beating them sets it back (and they train harder instead).
    /// </summary>
    public static int Fewer(int losses) => Math.Min(losses, 2);

    /// <summary>
    /// A rival's squad of <paramref name="count"/> for a duel: species to their taste, given their blood
    /// and grafts, and trained a little behind your own creatures by this encounter (no further than
    /// <paramref name="yourJp"/>, your squad's average), further each time you beat them.
    /// </summary>
    public static List<(Creature creature, Training training)> Squad(RivalDef r, ulong encounter, int wins, ulong seed, int count, int yourJp = int.MaxValue)
    {
        int jpBase = Math.Min(JpPerEncounter * (int)encounter, yourJp) + JpPerWin * wins;
        var rng = new SeededRng(seed ^ 0x7A3E);
        var cands = Enumerable.Range(0, 40).Select(i => Species.Generate(SeededRng.Derive(seed, 0x5100 + (ulong)i))).ToList();
        var squad = new List<(Creature, Training)>();
        for (int n = 0; n < count; n++)
        {
            var options = cands.Select((s, i) => (i, r.Taste(s))).Where(o => o.Item2 > 0).ToArray();
            int pick = options.Length > 0 ? rng.Weighted(options) : 0;
            var sp = cands[pick];
            cands.RemoveAt(pick);
            if (r.Infuse != Strain.Natural && sp.Strain == Strain.Natural)
            {
                sp.Strain = r.Infuse;
                sp.Grafts |= Ranching.Lab.Infusions.First(x => x.strain == r.Infuse).grafts;
            }
            if (n <= wins) sp.Grafts |= r.Grafts;
            var c = Creature.Specimen(sp, 0);
            // Their ace, first in line, is trained the furthest.
            int jp = jpBase + (n == 0 ? AceJp : 0);
            squad.Add((c, Train(c, r.Classes, jp)));
        }
        return squad;
    }

    /// <summary>
    /// Spends JP the way a tamer would: the favourite classes first as each one opens, then the body's
    /// favoured classes, moving on once a class's skills and passives are learned.
    /// </summary>
    public static Training Train(Creature c, string[] classes, int jp)
    {
        var t = new Training { Class = Ranching.Ranch.DefaultClass(c), Innate = ClassBook.WildSkill(c) };
        while (jp > 0)
        {
            var open = ClassBook.Available(c, t).Where(k => t.Stars(k.Id) < 2).Select(k => k.Id).ToList();
            if (open.Count == 0) break;
            t.Class = classes.FirstOrDefault(open.Contains) ?? open.FirstOrDefault(k => ClassBook.Favoured(c, k)) ?? open[0];
            int spend = Math.Min(jp, 40);
            t.Earn(spend);
            jp -= spend;
        }
        // They fight as the favourite of the classes trained; the next one lends its skills.
        int Rank(string k) { int i = Array.IndexOf(classes, k); return i < 0 ? 99 : i; }
        var trained = t.Jp.Keys.Where(k => t.LearnedIn(k) > 0 && ClassBook.StandingOf(ClassBook.Class(k), c, t) == ClassBook.Standing.Open)
            .OrderBy(Rank).ThenByDescending(t.JpIn).ToList();
        if (trained.Count > 0) t.Class = trained[0];
        t.Secondary = trained.Skip(1).FirstOrDefault();
        return t;
    }
}
