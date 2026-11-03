using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Tactics;

/// <summary>
/// The squad rules for online matches (the user's calls, 2026-11-02): your own tamers and creatures,
/// at most two tamers and six creatures, and only one shiny fielded at a time. To be checked before a
/// match is queued, and again by the matchmaking server once there is one (docs/design-notes.md).
/// </summary>
public static class PvpRules
{
    public const int MaxShinies = 1;

    /// <summary>What's wrong with a squad for a match, or nothing if it may play.</summary>
    public static List<string> Problems(IReadOnlyCollection<Creature> squad, int tamers)
    {
        var problems = new List<string>();
        if (squad.Count == 0) problems.Add("Bring at least one beast.");
        if (tamers > Party.TamerBook.MaxFielded) problems.Add($"At most {Party.TamerBook.MaxFielded} tamers.");
        if (squad.Count > Party.TamerBook.CreaturesEach * tamers) problems.Add($"Each tamer leads at most {Party.TamerBook.CreaturesEach} beasts.");
        int shinies = squad.Count(c => c.Shiny);
        if (shinies > MaxShinies) problems.Add($"Only {MaxShinies} shiny may be fielded at a time ({shinies} in the squad).");
        return problems;
    }
}
