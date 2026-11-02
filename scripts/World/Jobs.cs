using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
using CreatureTactics.Party;
using CreatureTactics.Ranching;
using CreatureTactics.Tactics;

namespace CreatureTactics.World;

/// <summary>
/// A dispatch (the user's direction, 2026-11-02: funny, pun-filled jobs, like FFTA's dispatch
/// missions): someone around the country needs a hand, you send one creature off for a few days,
/// and it comes back with chem, training and a story. The better it suits the job, the likelier it
/// goes well. Nobody is ever lost; at worst they come back empty-handed. Data: new jobs are entries.
/// </summary>
public sealed class DispatchDef
{
    public string Id, Title, Ask, Success, Failure;
    public string Location;                        // where it's offered; null: anywhere
    public int Days = 2, Reward = 20;
    public (string hint, Func<Creature, Training, bool> fits)[] Suits;
}

public static class DispatchBook
{
    // What a job can ask for.
    static (string, Func<Creature, Training, bool>) Plan(BodyPlan p, string hint) => (hint, (c, _) => c.Species.Plan == p);
    static (string, Func<Creature, Training, bool>) Class(string cls) => (cls.ToLowerInvariant(), (_, t) => t != null && (t.Class == cls || t.Masteries.Contains(cls)));
    static (string, Func<Creature, Training, bool>) Temper(Attitude a) => (a.ToString().ToLowerInvariant(), (c, _) => c.Attitude == a);
    static (string, Func<Creature, Training, bool>) Blood(Strain s) => ($"{s.ToString().ToLowerInvariant()} blood", (c, _) => c.Species.Strain == s);
    static (string, Func<Creature, Training, bool>) Of(Element e) => ($"{e.ToString().ToLowerInvariant()} element", (c, _) => c.Species.Element == e);
    static (string, Func<Creature, Training, bool>) Big => ("large or huge", (c, _) => c.Size >= SizeClass.Large);
    static (string, Func<Creature, Training, bool>) Little => ("small or tiny", (c, _) => c.Size <= SizeClass.Small);
    static (string, Func<Creature, Training, bool>) Wings => ("wings", (c, _) => c.Parts.Wings != Creatures.Wings.None);
    static (string, Func<Creature, Training, bool>) Has(string hint, Func<Creature, bool> f) => (hint, (c, _) => f(c));

    public static readonly DispatchDef[] All =
    {
        new() { Id = "hiss", Title = "Hiss and Make Up", Location = "sump", Days = 2, Reward = 22,
            Ask = "Two serpents in the mire won't stop feuding. Send someone calm to mediate.",
            Suits = new[] { Plan(BodyPlan.Serpent, "a serpent"), Temper(Attitude.Loyal), Of(Element.Frost) },
            Success = "The serpents hugged it out. Awkwardly.", Failure = "Now there are three feuding serpents." },
        new() { Id = "pane", Title = "Pane in the Glass", Location = "gardens", Days = 2, Reward = 20,
            Ask = "The greenhouse's high windows need a wash. Wings would help.",
            Suits = new[] { Wings, Little, Temper(Attitude.Curious) },
            Success = "Spotless. The orchids are blushing.", Failure = "Smudged. Very artistically smudged." },
        new() { Id = "gears", Title = "Gear Today, Gone Tomorrow", Location = "foundry", Days = 3, Reward = 28,
            Ask = "Gears keep vanishing from the foundry overnight. Stand watch.",
            Suits = new[] { Blood(Strain.Clockwork), Class("Guardian"), Has("keen eyes", c => c.Parts.Eyes == EyeKind.Compound || c.Species.Grafts.HasFlag(Graft.Visor)) },
            Success = "Caught a magpie-drake red-handed. Gears returned, mostly.", Failure = "Fell asleep on a conveyor. Woke up in a crate." },
        new() { Id = "fowl", Title = "The Fowl Play", Location = "bridges", Days = 2, Reward = 24,
            Ask = "A theatre on the bridges needs a bird for its matinee.",
            Suits = new[] { Plan(BodyPlan.Avian, "a bird"), Temper(Attitude.Proud), Big },
            Success = "Standing ovation. It took five bows.", Failure = "Ate the props. The show went on, briefly." },
        new() { Id = "parcel", Title = "Special Delivery-ish", Location = "lantern", Days = 1, Reward = 14,
            Ask = "Run a parcel across Lantern Row before the neon shops close.",
            Suits = new[] { Class("Skirmisher"), Wings, Has("long legs", c => c.Parts.Limbs is Limbs.Long or Limbs.Hooved) },
            Success = "Delivered with a flourish and only one dent.", Failure = "Delivered to the wrong neon shop. It's a lamp now." },
        new() { Id = "hoof", Title = "Hoof It", Location = "gardens", Days = 2, Reward = 20,
            Ask = "A doctor needs medicine carried up the hill road, fast.",
            Suits = new[] { Has("hooves", c => c.Parts.Limbs == Limbs.Hooved), Plan(BodyPlan.Quadruped, "four legs"), Temper(Attitude.Loyal) },
            Success = "The patient is fine. The doctor wants to adopt it.", Failure = "It stopped to graze. For a day." },
        new() { Id = "brew", Title = "Brew-haha", Location = "lantern", Days = 2, Reward = 26,
            Ask = "An alchemist needs a taste-tester for new tonics. Strong stomachs only.",
            Suits = new[] { Blood(Strain.Chemtech), Class("Alchemist"), Temper(Attitude.Curious) },
            Success = "Tasted everything. Glowed a little. Loved it.", Failure = "Hiccupped bubbles for two days straight." },
        new() { Id = "vane", Title = "Weather or Not", Location = "clocktower", Days = 3, Reward = 30,
            Ask = "The clocktower's weathervane is stuck. Fly up and fix it.",
            Suits = new[] { Wings, Class("Tinker"), Has("clever claws", c => c.Parts.Limbs == Limbs.Clawed) },
            Success = "Fixed it. Now it points at lunch.", Failure = "Got stuck on the weathervane too. Spun for hours." },
        new() { Id = "drift", Title = "Snow Problem", Location = "frost", Days = 3, Reward = 30,
            Ask = "A merchant's cart is buried in a drift on the pass. Dig it out.",
            Suits = new[] { Big, Of(Element.Frost), Has("claws", c => c.Weapon == Weapon.Claws) },
            Success = "Dug out the cart and a very surprised marmot.", Failure = "Dug a lovely tunnel. Wrong direction." },
        new() { Id = "beetle", Title = "Dune Buggy", Location = "dunes", Days = 2, Reward = 24,
            Ask = "Sand-beetle racers need a pacer for the big race.",
            Suits = new[] { Plan(BodyPlan.Insectoid, "an insect"), Class("Skirmisher"), Of(Element.Ember) },
            Success = "Paced the winner. Got a tiny medal.", Failure = "Won by accident. The beetles are furious." },
        new() { Id = "shore", Title = "Shore Thing", Location = "coast", Days = 2, Reward = 22,
            Ask = "Lifeguard duty on the coast, while the gulls are on strike.",
            Suits = new[] { Has("a swimmer", c => c.MoveType == MoveType.Swim), Plan(BodyPlan.Aquatic, "a sea creature"), Of(Element.Tide) },
            Success = "Saved three swimmers and a hat.", Failure = "Napped in the shallows. Got sunburnt on one side." },
        new() { Id = "fangs", Title = "Fangs for the Memories", Days = 1, Reward = 16,
            Ask = "A retired tamer wants to reminisce with a fierce creature.",
            Suits = new[] { Has("a bite", c => c.Weapon == Weapon.Bite), Temper(Attitude.Aggressive), Has("an old line", c => c.Generation >= 2) },
            Success = "Heard forty stories. Was in three of them.", Failure = "Growled at the scrapbook. Tea was cut short." },
        new() { Id = "lamp", Title = "Light Reading", Days = 2, Reward = 18,
            Ask = "A librarian working late needs something that glows.",
            Suits = new[] { Has("an aura", c => Species.AuraOf(c.Species) != Aura.None), Of(Element.Spark), Blood(Strain.Arcane) },
            Success = "Lit the stacks all night. Read two books.", Failure = "Glowed too bright. The librarian shushed it." },
        new() { Id = "cheese", Title = "Mutt-er of Fact", Location = "lantern", Days = 2, Reward = 24,
            Ask = "A detective needs a keen nose for a missing-cheese case.",
            Suits = new[] { Has("a hound", c => Species.FormOf(c.Species) == Form.Hound), Plan(BodyPlan.Quadruped, "four legs"), Temper(Attitude.Curious) },
            Success = "Found the cheese. Ate the evidence.", Failure = "Followed the wrong cheese. A lovely cheese, though." },
        new() { Id = "crystal", Title = "Crystal Clear", Location = "hollow", Days = 3, Reward = 32,
            Ask = "Miners need someone to spot the cracked crystals before they shatter.",
            Suits = new[] { Has("compound eyes", c => c.Parts.Eyes == EyeKind.Compound), Has("back crystals", c => c.Parts.Back == Back.Crystals), Class("Mystic") },
            Success = "Spotted every crack. The miners carved it a statue.", Failure = "Tapped one to check. It was cracked." },
        new() { Id = "stones", Title = "Rock Bottom Prices", Days = 1, Reward = 15,
            Ask = "Haul stones for a market stall's grand opening.",
            Suits = new[] { Has("huge", c => c.Size == SizeClass.Huge), Class("Brawler"), Has("a shell or a club", c => c.Parts.Back == Back.Shell || c.Parts.Tail == Tail.Club) },
            Success = "Hauled the lot, and the stall, by mistake.", Failure = "Sat on the stones. They're pebbles now." },
        new() { Id = "vents", Title = "Hot Under the Collar", Location = "cinder", Days = 3, Reward = 34,
            Ask = "Prospectors need an escort past the lava vents.",
            Suits = new[] { Of(Element.Ember), Has("a flier", c => c.MoveType == MoveType.Fly), Class("Guardian") },
            Success = "Escorted them there and back, singed only slightly.", Failure = "Got distracted by a nice warm rock." },
        new() { Id = "net", Title = "Pier Pressure", Location = "coast", Days = 1, Reward = 16,
            Ask = "Fishers need help hauling in an enormous net.",
            Suits = new[] { Big, Of(Element.Tide), Temper(Attitude.Loyal) },
            Success = "Hauled in the net and three confused crabs.", Failure = "Jumped in after the fish. Came back with one." },
    };

    public static DispatchDef Get(string id) => All.FirstOrDefault(d => d.Id == id);

    /// <summary>The odds it goes well, in percent: 30, and 22 more for each way the creature suits the job.</summary>
    public static int Chance(DispatchDef d, Creature c, Training t) => 30 + 22 * d.Suits.Count(s => s.fits(c, t));
}

/// <summary>A cup at the Grand Coliseum (FFTA's tournaments): three duels in a row; win all three to take it.</summary>
public sealed class CupDef
{
    public string Id, Name, After;
    public (Milestone kind, int count)[] Needs;
    public int Level;          // the encounter its contenders are trained to: a cup gets easier as you grow
    public int Prize;          // chem for taking the cup
    public RivalDef[] Contenders;
}

public static class CupBook
{
    /// <summary>A contender: a tamer with a little boost and a squad trained in a few classes.</summary>
    static RivalDef Contender(string id, string name, string specialty, string glyph, string text, string[] classes, Func<Species, float> taste,
        int atk = 0, int def = 0, int spd = 0, float hp = 0) => new()
    {
        Tamer = new TamerDef { Id = id, Name = name, Specialty = specialty, Glyph = glyph, Text = text, Atk = atk, Def = def, Spd = spd, HpPct = hp },
        Ideal = text, Taste = taste, Classes = classes, Turf = new[] { "The Grand Coliseum" },
    };

    public static readonly CupDef[] All =
    {
        new()
        {
            Id = "bronze", Name = "Bronze Cup", Level = 6, Prize = 80, Needs = new[] { (Milestone.Contracts, 5) },
            Contenders = new[]
            {
                Contender("kid_kettle", "Kid Kettle", "Full Steam", "gear", "SPD +1 for every creature.", new[] { "Tinker", "Skirmisher" }, s => s.Strain == Strain.Clockwork ? 4 : 1, spd: 1),
                Contender("mama_mudpie", "Mama Mudpie", "Tough Love", "shell", "DEF +1 for every creature.", new[] { "Guardian", "Brawler" }, s => s.Strain == Strain.Natural ? 3 : 1, def: 1),
                Contender("tumble_twins", "The Tumble Twins", "Double Trouble", "wind", "ATK +1 for every creature.", new[] { "Skirmisher", "Hunter" }, s => s.BaseSize <= SizeClass.Small ? 4 : 0.5f, atk: 1),
            },
        },
        new()
        {
            Id = "silver", Name = "Silver Cup", After = "bronze", Level = 12, Prize = 140, Needs = new[] { (Milestone.Contracts, 10), (Milestone.Creatures, 8) },
            Contenders = new[]
            {
                Contender("barb", "Barb the Brisk", "Tailwind", "wing", "SPD +1 and ATK +1 for every creature.", new[] { "Skirmisher", "Skydiver", "Hunter" }, s => s.Plan == BodyPlan.Avian ? 4 : 1, atk: 1, spd: 1),
                Contender("sootcheek", "Dame Sootcheek", "Smoke and Mirrors", "flask", "ATK +1 and DEF +1 for every creature.", new[] { "Alchemist", "Chemist", "Rogue" }, s => s.Strain == Strain.Chemtech ? 4 : 1, atk: 1, def: 1),
                Contender("reginald", "Sir Reginald Fang", "Old Money", "fang", "ATK +2 for every creature.", new[] { "Brawler", "Champion", "Packleader" }, s => s.Plan == BodyPlan.Quadruped ? 3 : 1, atk: 2),
            },
        },
        new()
        {
            Id = "gold", Name = "Gold Cup", After = "silver", Level = 20, Prize = 220, Needs = new[] { (Milestone.Contracts, 15), (Milestone.RivalWins, 2) },
            Contenders = new[]
            {
                Contender("bolt", "Bertram Bolt", "Live Wire", "bolt", "ATK +1 and SPD +2 for every creature.", new[] { "Mystic", "Tinker", "Hexwarden" }, s => s.Element == Element.Spark ? 4 : 1, atk: 1, spd: 2),
                Contender("lumen", "Lady Lumen", "Footlights", "sun", "DEF +2 and max HP +10% for every creature.", new[] { "Mystic", "Saint", "Runeweaver" }, s => s.Strain == Strain.Arcane ? 4 : 1, def: 2, hp: 0.1f),
                Contender("ironjaw", "Old Ironjaw", "Undefeated", "crown", "ATK +2, DEF +2 and max HP +10% for every creature.", new[] { "Titan", "Juggernaut", "Brawler", "Guardian" }, s => s.BaseSize >= SizeClass.Large ? 4 : 1, atk: 2, def: 2, hp: 0.1f),
            },
        },
    };

    public static CupDef Get(string id) => All.FirstOrDefault(c => c.Id == id);

    /// <summary>Cups you may enter: not yet won, the one before won, the coliseum open, the milestones met.</summary>
    public static IEnumerable<CupDef> Ready(Ranch r) => All.Where(c => !r.Cups.Contains(c.Id) && (c.After == null || r.Cups.Contains(c.After))
        && Country.IsOpen(r, Country.Get("coliseum")) && c.Needs.All(n => StoryBook.Progress(r, n.kind) >= n.count));

    /// <summary>Round 1 to 3 of a cup: a duel with that round's contender, in the arena.</summary>
    public static Mission Round(Ranch r, CupDef cup, int round)
    {
        var contender = cup.Contenders[round - 1];
        ulong seed = SeededRng.Derive(SeededRng.Hash(cup.Id), r.Encounter * 3 + (ulong)round);
        var m = Mission.Duel(contender, (ulong)cup.Level, round - 1, seed);
        m.MapName = "The Grand Coliseum";
        m.Location = "coliseum";
        m.Cup = cup.Id;
        m.Round = round;
        m.Reward = round < 3 ? 10 : cup.Prize;
        m.Title = $"{cup.Name}, round {round}: {contender.Name}";
        return m;
    }
}
