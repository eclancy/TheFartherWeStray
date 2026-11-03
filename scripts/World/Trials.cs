using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Ranching;
using TheFartherWeStray.Tactics;

namespace TheFartherWeStray.World;

/// <summary>A trial: a mission at one place that opens a class, and moves your standing toward that place's side.</summary>
public sealed class TrialDef
{
    public string Class, Location, Title, Words;
    public Side Side;
    public Objective Goal;
    public int Pack;
}

/// <summary>
/// Class trials (the user's call, 2026-11-02): certain classes open only once a certain mission is
/// won, and winning it moves your standing toward that mission's side. The signature classes each
/// have one, at a place of their side, on the map once that place is open. The titles and words are
/// placeholders for the story to replace.
/// </summary>
public static class TrialBook
{
    public const int Standing = 5;

    public static readonly TrialDef[] All =
    {
        new() { Class = "Conjurer", Location = "gardens", Side = Side.City, Goal = Objective.Hunt, Pack = 4, Title = "Trial: the Old Pact",
            Words = "The Gardens' keepers will teach a creature to call the old beasts, once it proves itself." },
        new() { Class = "Mesmer", Location = "bridges", Side = Side.City, Goal = Objective.Rout, Pack = 4, Title = "Trial: the Salon",
            Words = "A mesmerist of the city above wants an audience that can hold its nerve." },
        new() { Class = "Mimic", Location = "coliseum", Side = Side.City, Goal = Objective.Rout, Pack = 4, Title = "Trial: the Mirror Match",
            Words = "The coliseum's scholars of the fight will train a mimic, for a winner to watch." },
        new() { Class = "Horologist", Location = "clocktower", Side = Side.City, Goal = Objective.Survive, Pack = 4, Title = "Trial: the Last Hour",
            Words = "Hold the old clocktower until its bell strikes, and its keeper will share the clock's secret." },
        new() { Class = "Reaper", Location = "lantern", Side = Side.Undercity, Goal = Objective.Hunt, Pack = 4, Title = "Trial: the Quiet Contract",
            Words = "Someone on Lantern Row teaches the quiet work, to those who finish a job cleanly." },
        new() { Class = "Saboteur", Location = "foundry", Side = Side.Undercity, Goal = Objective.Rout, Pack = 4, Title = "Trial: the Scrap Heap",
            Words = "The Foundry's scrappers share their tricks with anyone who survives their testing ground." },
        new() { Class = "Skinchanger", Location = "sump", Side = Side.Undercity, Goal = Objective.Hunt, Pack = 4, Title = "Trial: the Shifting Pool",
            Words = "Deep in the Glowmire, a pool changes whatever drinks from it. Its guardian decides who may." },
    };

    public static string Flag(string cls) => "trial:" + cls;
    public static TrialDef Get(string cls) => All.FirstOrDefault(t => t.Class == cls);

    /// <summary>Trials on offer: not yet won, at a place that's open.</summary>
    public static IEnumerable<TrialDef> Open(Ranch r) =>
        All.Where(t => !r.Flags.Contains(Flag(t.Class)) && Country.Get(t.Location) is { } at && Country.IsOpen(r, at));

    public static Mission ToMission(TrialDef t)
    {
        var at = Country.Get(t.Location);
        return new Mission
        {
            Seed = SeededRng.Hash(Flag(t.Class)), MapName = at.MapName, Location = at.Id, Goal = t.Goal, Pack = t.Pack,
            Trial = t.Class, Stars = 3, Reward = 40, Title = t.Title, Flavor = $"{t.Words} Wins open the {t.Class} class.",
        };
    }
}
