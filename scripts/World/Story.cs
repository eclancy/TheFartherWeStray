using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using CreatureTactics.Ranching;
using CreatureTactics.Tactics;

namespace CreatureTactics.World;

/// <summary>What the rest of the game asks of you before a main mission appears.</summary>
public enum Milestone { Contracts, Creatures, CodexParts, RivalWins }

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

    public string Title => $"Main: the road to {Country.Get(Opens).Name}";
}

public static class StoryBook
{
    public static readonly MainMission[] All =
    {
        new() { Id = "main_foundry", Location = "lantern", Opens = "foundry", Needs = new[] { (Milestone.Contracts, 2) } },
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
    };

    public static MainMission Get(string id) => All.FirstOrDefault(m => m.Id == id);

    public static int Progress(Ranch r, Milestone m) => m switch
    {
        Milestone.Contracts => r.ContractsWon,
        Milestone.Creatures => r.Creatures.Count,
        Milestone.CodexParts => r.Codex.Count(k => !k.EndsWith(".None")), // real parts, not the empty slots
        _ => r.Rivals.Values.Sum(),
    };

    static string Words(Milestone m) => m switch
    {
        Milestone.Contracts => "contracts won",
        Milestone.Creatures => "creatures at the ranch",
        Milestone.CodexParts => "parts in the codex",
        _ => "rival duels won",
    };

    /// <summary>The next main mission not yet won whose mission before it is won, if any.</summary>
    public static MainMission Next(Ranch r) => All.FirstOrDefault(m => !r.Story.Contains(m.Id) && (m.After == null || r.Story.Contains(m.After)));

    /// <summary>Main missions on offer now: the one before won, its place open, its milestones met.</summary>
    public static IEnumerable<MainMission> Ready(Ranch r) => All.Where(m => !r.Story.Contains(m.Id)
        && (m.After == null || r.Story.Contains(m.After)) && Country.IsOpen(r, Country.Get(m.Location))
        && m.Needs.All(n => Progress(r, n.kind) >= n.count));

    /// <summary>Each milestone with your progress, and whether it's met: "2/4 contracts won".</summary>
    public static IEnumerable<(string text, bool met)> Needs(Ranch r, MainMission m) =>
        m.Needs.Select(n => ($"{System.Math.Min(Progress(r, n.kind), n.count)}/{n.count} {Words(n.kind)}", Progress(r, n.kind) >= n.count));

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
