using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;

namespace CreatureTactics.Tactics;

public enum Objective { Rout, Capture, Hunt, Survive }

/// <summary>
/// A contract on the mission board (FFTA's board): a region, a wild pack drawn from that region's
/// fauna, an objective and a chem reward. Some carry a sighting: a part your codex doesn't have yet,
/// guaranteed on one creature in the pack.
/// </summary>
public sealed class Mission
{
    public ulong Seed;
    public string MapName;
    public Objective Goal;
    public int Pack, Rounds, Reward, Stars;
    public string Title, Sighting;
    int _sightingIndex = -1;

    const int CandidateCount = 24;

    /// <summary>The species that could turn up for this mission, fixed by its seed.</summary>
    List<Species> Candidates() =>
        Enumerable.Range(0, CandidateCount).Select(i => Species.Generate(SeededRng.Derive(Seed, 0xC0 + (ulong)i))).ToList();

    public string Brief(Creature target = null) => Goal switch
    {
        Objective.Capture => $"Capture {target?.Species.Name ?? "the marked creature"}: recruit it before it gets away.",
        Objective.Hunt => $"Hunt {target?.Species.Name ?? "the alpha"}: bring down the marked alpha.",
        Objective.Survive => $"Survive {Rounds} rounds against a large pack.",
        _ => "Rout the pack: drive off or recruit every wild creature.",
    };

    /// <summary>A board of missions. It changes after every battle (the seed is the encounter number).</summary>
    public static List<Mission> Board(ulong encounter, ICollection<string> codex, int count = 4)
    {
        var list = new List<Mission>();
        var sighted = new HashSet<string>(); // no two cards promise the same part
        for (int i = 0; i < count; i++)
        {
            ulong seed = SeededRng.Derive(encounter * 977UL, (ulong)i + 1);
            var rng = new SeededRng(seed);
            var m = new Mission { Seed = seed };
            // The first card always uses a different region from the second, so the board varies.
            m.MapName = BattleMap.Names[(i == 0 ? (int)(encounter % (ulong)BattleMap.Names.Length) : rng.Int(BattleMap.Names.Length))];
            m.Goal = rng.Weighted((Objective.Rout, 3f), (Objective.Capture, 2f), (Objective.Hunt, 2f), (Objective.Survive, 1.5f));
            m.Pack = m.Goal == Objective.Survive ? rng.Int(5, 6) : rng.Int(3, 5);
            m.Rounds = m.Goal == Objective.Survive ? 4 + (m.Pack - 5) : 0;

            // A sighting: some part of a likely species your codex lacks, wings and horns first.
            var map = BattleMap.ByName(m.MapName);
            var cands = m.Candidates();
            if (rng.Chance(0.6f))
            {
                // Gear slots first (wings and horns open classes), in a shuffled order so boards vary.
                var slots = new List<string> { "Wings", "Horns", "Back", "Tail" }.OrderBy(_ => rng.Float()).Concat(new[] { "Limbs", "Head" }).ToList();
                foreach (var slot in slots)
                {
                    string KeyOf(Species s) => Ranching.Lab.Key(slot, Ranching.Lab.Get(s.Parts, slot));
                    int idx = cands.FindIndex(s => map.Fit(s) >= 1f && !codex.Contains(KeyOf(s)) && !KeyOf(s).EndsWith("None") && !sighted.Contains(KeyOf(s)));
                    if (idx < 0) continue;
                    m._sightingIndex = idx;
                    sighted.Add(Ranching.Lab.Key(slot, Ranching.Lab.Get(cands[idx].Parts, slot)));
                    string words = Ranching.Lab.Words(slot, Ranching.Lab.Get(cands[idx].Parts, slot));
                    string slotWord = slot.ToLowerInvariant();
                    m.Sighting = words.Contains(slotWord.TrimEnd('s')) ? words : $"{words} {slotWord}";
                    break;
                }
            }
            m.Stars = Math.Clamp(m.Pack - 2 + (m.Goal is Objective.Hunt or Objective.Survive ? 1 : 0), 1, 5);
            m.Reward = 6 + m.Pack * 3 + m.Goal switch { Objective.Capture => 6, Objective.Hunt => 8, Objective.Survive => 10, _ => 0 };
            m.Title = $"{GoalWord(m.Goal)}: {m.MapName}";
            list.Add(m);
        }
        return list;
    }

    static string GoalWord(Objective o) => o switch
    {
        Objective.Capture => "Capture",
        Objective.Hunt => "Hunt",
        Objective.Survive => "Hold out",
        _ => "Rout",
    };

    /// <summary>The wild pack: drawn from the candidates by how well each fits the region.</summary>
    public (List<Creature> pack, Creature target) Wild()
    {
        var map = BattleMap.ByName(MapName);
        var rng = new SeededRng(Seed ^ 0x9AC4);
        var cands = Candidates();
        var chosen = new List<int>();
        if (_sightingIndex >= 0) chosen.Add(_sightingIndex);
        while (chosen.Count < Pack)
        {
            var options = Enumerable.Range(0, cands.Count).Where(i => !chosen.Contains(i)).Select(i => (i, map.Fit(cands[i]))).ToArray();
            chosen.Add(rng.Weighted(options));
        }
        var pack = chosen.Select(i => Creature.Specimen(cands[i], 0)).ToList();

        Creature target = null;
        if (Goal == Objective.Capture) target = _sightingIndex >= 0 ? pack[0] : pack[rng.Int(pack.Count)];
        if (Goal == Objective.Hunt)
        {
            // The alpha: the biggest of the pack, grown a size bigger still.
            target = pack.OrderByDescending(c => c.Size).First();
            target.Size = (SizeClass)Math.Min((int)SizeClass.Huge, (int)target.Size + 1);
        }
        return (pack, target);
    }

    public Battle CreateBattle(IEnumerable<(Creature, Training)> squad)
    {
        var (pack, target) = Wild();
        var battle = new Battle(BattleMap.ByName(MapName), squad, pack, Seed) { Goal = Goal, SurviveRounds = Rounds };
        battle.Target = target == null ? null : battle.Units.FirstOrDefault(u => u.Creature == target);
        battle.Mission = this;
        return battle;
    }
}
