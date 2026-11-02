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
    public Prize[] Prizes;                 // what beating them can win you, one drawn each time (RivalBook.Draw)

    public string Id => Tamer.Id;
    public string Name => Tamer.Name;
}

/// <summary>How often a prize comes up: a rarity is drawn first (<see cref="RivalBook.Odds"/>), then one of its prizes.</summary>
public enum Rarity { Common, Uncommon, Rare, VeryRare }

/// <summary>
/// One prize a rival can give up when you beat them (the user's pick, 2026-11-02: a random prize every
/// win, by rarity): things for the bag, a purse of chem, a title (once), or an egg from their line.
/// </summary>
public sealed class Prize
{
    public Rarity Rarity;
    public string Item;      // into the bag: gear, an augment or an item, Count of them
    public int Count = 1;
    public int Chem;         // a purse of chem
    public string Title;     // a title to keep (won once; a placeholder name for the story)
    public bool Egg, Shiny;  // a young creature bred toward their ideal (shiny: the very rare one)
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
            Prizes = Hoard(
                new() { Rarity = Rarity.Common, Item = "tonic", Count = 2 },
                new() { Rarity = Rarity.Common, Chem = 40 },
                new() { Rarity = Rarity.Uncommon, Item = "brass_gorget" },
                new() { Rarity = Rarity.Uncommon, Item = "rivet_gun" },
                new() { Rarity = Rarity.Uncommon, Item = "lens_visor" },
                new() { Rarity = Rarity.Uncommon, Title = "Gearbreaker" }),
        },
        new()
        {
            Tamer = new() { Id = "chem_witch", Name = "The Chem Witch", Specialty = "Bubbling Vials", Glyph = "poison",
                Leads = c => c.Species.Strain is Strain.Chemtech or Strain.Abyssal, LeadsHint = "chemborn or abyssal creatures",
                SkillPower = 0.2f, Spd = 1, Text = "Chemborn and abyssal creatures: skills hit 20% harder; SPD +1." },
            Ideal = "Breeds creatures brimming with glowing chem.", AppearsAt = 4,
            Taste = s => s.Strain == Strain.Chemtech ? 6 : s.Strain == Strain.Abyssal ? 2 : s.Strain == Strain.Natural ? 1 : 0,
            Infuse = Strain.Chemtech,
            Classes = new[] { "Chemist", "Venomtail", "Swarmlord", "Stalker", "Alchemist", "Rogue", "Hunter" },
            Turf = new[] { "Glowmire", "Undercity Foundry" },
            Prizes = Hoard(
                new() { Rarity = Rarity.Common, Item = "smelling_salts", Count = 2 },
                new() { Rarity = Rarity.Common, Item = "tonic", Count = 2 },
                new() { Rarity = Rarity.Uncommon, Item = "witchs_phial" },
                new() { Rarity = Rarity.Uncommon, Item = "acid_canister" },
                new() { Rarity = Rarity.Uncommon, Item = "drip_feed" },
                new() { Rarity = Rarity.Uncommon, Title = "Witchbane" }),
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
            // A hunter of natural beasts: snares and berries rather than machinery.
            Prizes = Hoard(
                new() { Rarity = Rarity.Common, Item = "snare_net", Count = 2 },
                new() { Rarity = Rarity.Common, Item = World.ShopBook.Berry, Count = 3 },
                new() { Rarity = Rarity.Uncommon, Item = "fang_necklace" },
                new() { Rarity = Rarity.Uncommon, Item = "soothing_chime", Count = 2 },
                new() { Rarity = Rarity.Uncommon, Title = "Apex Tamer" }),
        },
        new()
        {
            Tamer = new() { Id = "occultist", Name = "The Occultist", Specialty = "Old Rites", Glyph = "eye",
                Leads = c => c.Species.Strain is Strain.Arcane or Strain.Abyssal || Species.AuraOf(c.Species) != Aura.None,
                LeadsHint = "runic or abyssal creatures, or any with an aura", SkillPower = 0.15f, Evade = 8,
                Text = "Runic and abyssal creatures, and any with an aura: skills hit 15% harder; 8% harder to hit." },
            Ideal = "Breeds beasts touched by old magic: runes, halos, third eyes.", AppearsAt = 8,
            Taste = s => (s.Strain == Strain.Arcane ? 5 : s.Strain == Strain.Abyssal ? 2 : s.Strain == Strain.Natural ? 0.6f : 0.2f)
                * (Species.AuraOf(s) != Aura.None ? 3 : 1),
            Infuse = Strain.Arcane,
            Classes = new[] { "Runeweaver", "Saint", "Hexmark", "Oculus", "Seer", "Lurelight", "Hexwarden", "Mystic", "Howler", "Hunter" },
            Turf = new[] { "Hexlight Hollow", "Lantern Row" },
            Prizes = Hoard(
                new() { Rarity = Rarity.Common, Item = "soothing_chime", Count = 2 },
                new() { Rarity = Rarity.Common, Item = "smelling_salts", Count = 2 },
                new() { Rarity = Rarity.Uncommon, Item = "occult_sigil" },
                new() { Rarity = Rarity.Uncommon, Item = "hextech_core" },
                new() { Rarity = Rarity.Uncommon, Item = "strobe_tips" },
                new() { Rarity = Rarity.Uncommon, Title = "Sigilbreaker" }),
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
            Turf = new[] { "Glowmire", "Clocktower Ruins" },
            // Big beasts, no machinery: provisions and a fat purse.
            Prizes = Hoard(
                new() { Rarity = Rarity.Common, Item = "tonic", Count = 3 },
                new() { Rarity = Rarity.Common, Chem = 50 },
                new() { Rarity = Rarity.Uncommon, Item = "wyrmscale_mantle" },
                new() { Rarity = Rarity.Uncommon, Item = World.ShopBook.Berry, Count = 4 },
                new() { Rarity = Rarity.Uncommon, Title = "Wyrmtamer" }),
        },
    };

    /// <summary>A rival's own prizes, then the egg from their line every rival can give up (rare; shiny, very rare).</summary>
    static Prize[] Hoard(params Prize[] own) => own.Concat(new Prize[]
    {
        new() { Rarity = Rarity.Rare, Egg = true },
        new() { Rarity = Rarity.VeryRare, Egg = true, Shiny = true },
    }).ToArray();

    /// <summary>
    /// How often each rarity is drawn, in percent (among those with a prize left to draw). The shiny
    /// egg is 1 in 100 (the user's call, 2026-11-02: 3% was too common).
    /// </summary>
    public static int Odds(Rarity r) => r switch { Rarity.Common => 57, Rarity.Uncommon => 30, Rarity.Rare => 12, _ => 1 };

    public static string Word(Rarity r) => r == Rarity.VeryRare ? "very rare" : r.ToString().ToLowerInvariant();

    /// <summary>
    /// The prize for a win: a rarity by its odds, then one of that rarity's prizes. A title you
    /// already hold is out of the draw.
    /// </summary>
    public static Prize Draw(RivalDef r, ICollection<string> titles, ulong seed)
    {
        var rng = new SeededRng(seed);
        var open = r.Prizes.Where(p => p.Title == null || !titles.Contains(p.Title)).ToList();
        var rarity = rng.Weighted(open.Select(p => p.Rarity).Distinct().Select(x => (x, (float)Odds(x))).ToArray());
        var pool = open.Where(p => p.Rarity == rarity).ToList();
        return pool[rng.Int(pool.Count)];
    }

    /// <summary>
    /// An egg from a rival's line: two species to their taste, given their blood, bred together (so
    /// it's one of a kind), born with a trait or two. Machine grafts never pass down, so it has none.
    /// </summary>
    public static Creature Egg(RivalDef r, ulong seed, bool shiny)
    {
        var rng = new SeededRng(seed ^ 0xE66);
        var cands = Enumerable.Range(0, 24).Select(i => Species.Generate(SeededRng.Derive(seed, 0xE600 + (ulong)i))).ToList();
        Creature Parent()
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
            return Creature.Specimen(sp, 0);
        }
        var c = Breeding.Breed(Parent(), Parent(), SeededRng.Derive(seed, 0xE667)).Child;
        if (c.Traits.Count == 0) c.Traits.Add(Classes.TraitBook.Traits[rng.Int(Classes.TraitBook.Traits.Length)].Id);
        if (shiny)
        {
            c.Shiny = true;
            c.Spec = c.Spec.Shiny(rng);
            c.Palette = Creature.BuildPalette(c.Spec, c.Species);
        }
        return c;
    }

    /// <summary>The best prizes still to be won from a rival, in a few words for the duel card (items go without saying).</summary>
    public static string PrizeHint(RivalDef r, ICollection<string> titles)
    {
        var words = r.Prizes.Where(p => p.Item != null && GearBook.IsGear(p.Item)).Select(p => World.ShopBook.Name(p.Item)).ToList();
        if (r.Prizes.Any(p => p.Item != null && AugmentBook.Get(p.Item) != null)) words.Add("augments");
        if (r.Prizes.FirstOrDefault(p => p.Title != null) is { } t && !titles.Contains(t.Title)) words.Add("a title");
        words.Add("rare eggs");
        return "Prizes: " + string.Join(", ", words);
    }

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
