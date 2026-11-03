using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Ranching;
using TheFartherWeStray.Tactics;

namespace TheFartherWeStray.World;

/// <summary>What the rest of the game asks of you before a main mission appears.</summary>
public enum Milestone { Contracts, Creatures, CodexParts, RivalWins, CityStanding, UndercityStanding }

/// <summary>
/// A main story mission (the user's direction, 2026-11-02): it appears at its place once the one
/// before it is won and its milestones are met, so the story can't be rushed past the rest of the
/// game, and winning it opens a new place on the map. The story itself is still to come: these
/// say only what they open.
/// </summary>
public sealed class MainMission
{
    public string Id, Location, Opens, After;
    public (Milestone kind, int count)[] Needs;
    public Objective Goal = Objective.Rout;
    public int Pack = 4;
    // An ending (the user's three): its name, and where standing must lean for it ("city", "undercity", "balanced").
    public string Ending, Leans;
    public string Scene;          // a cutscene played once before its battle (Story.SceneBook)

    public string Title => Ending != null ? $"Ending: {Ending}" : $"Main: the road to {Country.Get(Opens).Name}";
}

/// <summary>A choice after a main mission: what it says, how it moves standing, the flag it sets.</summary>
public sealed record Choice(string Id, string Text, int City, int Under);

/// <summary>
/// A rumoured creature (the user's direction: some monsters can only be found on certain paths): a
/// rare one heard of at a place once your standing allows, until you catch it. Placeholders for the
/// story's own: their words are a line each.
/// </summary>
public sealed class Rumour
{
    public string Id, Location, Words, Gate;
    public Func<Ranch, bool> Open;
    public Func<Creature> Make;
}

public static class StoryBook
{
    public static readonly MainMission[] All =
    {
        new() { Id = "main_foundry", Location = "lantern", Opens = "foundry", Needs = new[] { (Milestone.Contracts, 2) }, Scene = "road_to_foundry" },
        new() { Id = "main_bridges", Location = "gardens", Opens = "bridges", After = "main_foundry", Goal = Objective.Hunt,
            Needs = new[] { (Milestone.Contracts, 4), (Milestone.Creatures, 6) } },
        new() { Id = "main_sump", Location = "foundry", Opens = "sump", After = "main_bridges", Pack = 5,
            Needs = new[] { (Milestone.Contracts, 6), (Milestone.RivalWins, 1) } },
        new() { Id = "main_clocktower", Location = "bridges", Opens = "clocktower", After = "main_sump", Goal = Objective.Hunt, Pack = 5,
            Needs = new[] { (Milestone.Contracts, 9), (Milestone.CodexParts, 34) } },
        new() { Id = "main_hollow", Location = "clocktower", Opens = "hollow", After = "main_clocktower", Pack = 6,
            Needs = new[] { (Milestone.Contracts, 12), (Milestone.Creatures, 10), (Milestone.RivalWins, 2) } },
        // The far regions branch off the road (the user's direction: very different places to fight).
        new() { Id = "main_coast", Location = "lantern", Opens = "coast", After = "main_foundry",
            Needs = new[] { (Milestone.Contracts, 4), (Milestone.Creatures, 6) } },
        new() { Id = "main_dunes", Location = "coast", Opens = "dunes", After = "main_coast", Goal = Objective.Hunt, Pack = 5,
            Needs = new[] { (Milestone.Contracts, 8), (Milestone.CodexParts, 30) } },
        new() { Id = "main_frost", Location = "gardens", Opens = "frost", After = "main_bridges", Pack = 5,
            Needs = new[] { (Milestone.Contracts, 7), (Milestone.Creatures, 8) } },
        new() { Id = "main_cinder", Location = "clocktower", Opens = "cinder", After = "main_clocktower", Goal = Objective.Hunt, Pack = 6,
            Needs = new[] { (Milestone.Contracts, 14), (Milestone.RivalWins, 2) } },
        // The three endings, once the last road is open: whichever your standing leans toward.
        new() { Id = "end_justice", Location = "bridges", After = "main_hollow", Ending = "justice for the undercity", Leans = "city",
            Goal = Objective.Hunt, Pack = 6, Needs = new[] { (Milestone.Contracts, 15), (Milestone.CityStanding, 60) } },
        new() { Id = "end_chaos", Location = "foundry", After = "main_hollow", Ending = "chaos in the city above", Leans = "undercity",
            Goal = Objective.Hunt, Pack = 6, Needs = new[] { (Milestone.Contracts, 15), (Milestone.UndercityStanding, 60) } },
        new() { Id = "end_peace", Location = "hollow", After = "main_hollow", Ending = "peace between them", Leans = "balanced",
            Pack = 6, Needs = new[] { (Milestone.Contracts, 15), (Milestone.CityStanding, 40), (Milestone.UndercityStanding, 40) } },
    };

    /// <summary>
    /// The choice after a main mission (placeholder words until the story is written): who hears first
    /// that a new place is open, and gets the credit. The city, the undercity, or both across one table.
    /// </summary>
    public static Choice[] ChoicesAfter(MainMission m) => m.Ending != null ? Array.Empty<Choice>() : new[]
    {
        new Choice("city", "The city above: let its watch take the credit.", 8, -3),
        new Choice("undercity", "The undercity: let its crews take the credit.", -3, 8),
        new Choice("both", "Both, across one table: share it.", 3, 3),
    };

    public static string Prompt(MainMission m) => $"{Country.Get(m.Opens).Name} is open, and word will get out. Who hears it first, and gets the credit?";

    public static readonly Rumour[] Rumours =
    {
        new()
        {
            Id = "rumour_city", Location = "gardens", Gate = "50 standing with the city",
            Words = "A prize-bred beast from the city's finest stables has slipped its keepers in the Gardens.",
            Open = r => r.CityStanding >= 50,
            Make = () => Rumoured(0xC17A, sp => { sp.Strain = Strain.Natural; sp.Grafts = Graft.None; sp.Aura = Aura.Halo; }, "big_heart", "swift"),
        },
        new()
        {
            Id = "rumour_under", Location = "sump", Gate = "50 standing with the undercity",
            Words = "Something that escaped an undercity lab is loose in the Glowmire, dripping chem.",
            Open = r => r.UnderStanding >= 50,
            Make = () => Rumoured(0x5E7A, sp => { sp.Strain = Strain.Chemtech; sp.Grafts = Graft.Canister | Graft.Drips | Graft.Pipework; }, "iron_stomach", "quick_healer"),
        },
        new()
        {
            Id = "rumour_peace", Location = "hollow", Gate = "30 standing with both sides, neither far ahead",
            Words = "A creature of both worlds haunts the Hollow, seen only by those trusted on either side.",
            Open = r => r.CityStanding >= 30 && r.UnderStanding >= 30 && Standing.Lean(r) == "balanced",
            Make = () => Rumoured(0x9EAC, sp => { sp.Strain = Strain.Arcane; sp.Grafts = Graft.Runes | Graft.GlowTips; sp.Aura = Aura.Sigil; }, "night_eyes", "hard_head"),
        },
    };

    /// <summary>A rumoured creature: a species of its own seed, reshaped, large, born with two traits.</summary>
    static Creature Rumoured(ulong seed, Action<Species> shape, params string[] traits)
    {
        var sp = Species.Generate(seed);
        shape(sp);
        var c = Creature.Specimen(sp, 0);
        c.Size = SizeClass.Large;
        c.Traits = traits.ToList();
        return c;
    }

    /// <summary>Rumours heard now: open to your standing, at an open place, not yet caught.</summary>
    public static IEnumerable<Rumour> Heard(Ranch r) => Rumours.Where(x => x.Open(r) && !r.Flags.Contains($"caught:{x.Id}") && Country.IsOpen(r, Country.Get(x.Location)));

    public static Mission RumourMission(Rumour x)
    {
        var at = Country.Get(x.Location);
        var special = x.Make();
        return new Mission
        {
            Seed = SeededRng.Hash(x.Id), MapName = at.MapName, Location = at.Id, Goal = Objective.Capture, Pack = 3, Special = special,
            Rumour = x.Id, Stars = 3, Reward = 40, Title = $"Rumour: {special.Species.Name}", Flavor = x.Words,
        };
    }

    public static MainMission Get(string id) => All.FirstOrDefault(m => m.Id == id);

    public static int Progress(Ranch r, Milestone m) => m switch
    {
        Milestone.Contracts => r.ContractsWon,
        Milestone.Creatures => r.Creatures.Count,
        Milestone.CodexParts => r.Codex.Count(k => !k.EndsWith(".None")), // real parts, not the empty slots
        Milestone.CityStanding => r.CityStanding,
        Milestone.UndercityStanding => r.UnderStanding,
        _ => r.Rivals.Values.Sum(),
    };

    static string Words(Milestone m) => m switch
    {
        Milestone.Contracts => "contracts won",
        Milestone.Creatures => "beasts at the ranch",
        Milestone.CodexParts => "parts in the codex",
        Milestone.CityStanding => "standing with the city",
        Milestone.UndercityStanding => "standing with the undercity",
        _ => "rival duels won",
    };

    /// <summary>The next main mission not yet won whose mission before it is won, if any (the ending your standing leans to, at the end).</summary>
    public static MainMission Next(Ranch r) => r.Ending != null ? null
        : All.Where(m => !r.Story.Contains(m.Id) && (m.After == null || r.Story.Contains(m.After)))
            .OrderBy(m => m.Ending != null && m.Leans != Standing.Lean(r) ? 1 : 0).FirstOrDefault();

    /// <summary>Main missions on offer now: the one before won, its place open, its milestones met.</summary>
    public static IEnumerable<MainMission> Ready(Ranch r) => All.Where(m => !r.Story.Contains(m.Id)
        && (m.After == null || r.Story.Contains(m.After)) && Country.IsOpen(r, Country.Get(m.Location))
        && (m.Ending == null || r.Ending == null && Standing.Lean(r) == m.Leans)
        && m.Needs.All(n => Progress(r, n.kind) >= n.count));

    /// <summary>Each milestone with your progress, and whether it's met: "2/4 contracts won".</summary>
    public static IEnumerable<(string text, bool met)> Needs(Ranch r, MainMission m) =>
        m.Needs.Select(n => ($"{System.Math.Min(Progress(r, n.kind), n.count)}/{n.count} {Words(n.kind)}", Progress(r, n.kind) >= n.count))
            .Concat(m.Leans == null ? Enumerable.Empty<(string, bool)>() : new[] { (m.Leans switch
            {
                "city" => "leaning to the city (30 over the undercity)",
                "undercity" => "leaning to the undercity (30 over the city)",
                _ => "balanced (neither side 30 ahead)",
            }, Standing.Lean(r) == m.Leans) });

    /// <summary>The battle for a main mission: the same fight every attempt, on its place's map.</summary>
    public static Mission ToMission(MainMission m)
    {
        var at = Country.Get(m.Location);
        ulong seed = SeededRng.Hash(m.Id);
        return new Mission
        {
            Seed = seed, MapName = at.MapName, Goal = m.Goal, Pack = m.Pack, Location = at.Id, Story = m.Id,
            Stars = System.Math.Clamp(m.Pack - 1, 1, 5), Reward = 30 + m.Pack * 5, Title = m.Title,
        };
    }
}
