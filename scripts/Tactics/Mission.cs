using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;

namespace CreatureTactics.Tactics;

public enum Objective { Rout, Capture, Hunt, Survive, Duel }

/// <summary>
/// A contract on the mission board (FFTA's board): a region, a wild pack drawn from that region's
/// fauna, an objective and a chem reward. Some carry a sighting: a part your codex doesn't have yet,
/// guaranteed on one creature in the pack. A duel puts a rival tamer's squad there instead.
/// </summary>
public sealed class Mission
{
    public ulong Seed;
    public string MapName;
    public Objective Goal;
    public int Pack, Rounds, Reward, Stars;
    public string Title, Sighting;
    public Party.RivalDef Rival;  // a duel: the rival tamer whose squad you face
    public int RivalWins;         // times you've beaten them before (their squad trains harder)
    public ulong Encounter;
    public string Location;       // where on the country map it is
    public string Story;          // a main story mission: its id in the StoryBook
    public bool Repeatable;       // a place's regular job, always on offer
    public string Flavor;          // a line of colour for the card (a regular job's ask)
    public World.DispatchDef Dispatch; // not a battle: send a creature off on this job
    public string Cup;            // a coliseum cup's round...
    public int Round;             // ...and which (1 to 3)
    public int Roamer;            // a roaming pack (its id; 0: none)
    public string FaunaMap;       // whose region the pack comes from, if not the map it's fought on
    int _sightingIndex = -1;

    const int CandidateCount = 24;

    /// <summary>The species that could turn up for this mission, fixed by its seed.</summary>
    List<Species> Candidates() =>
        Enumerable.Range(0, CandidateCount).Select(i => Species.Generate(SeededRng.Derive(Seed, 0xC0 + (ulong)i))).ToList();

    public string Brief(Creature target = null) => Dispatch != null ? Dispatch.Ask : Goal switch
    {
        Objective.Capture => $"Capture {target?.Species.Name ?? "the marked creature"}: recruit it before it gets away.",
        Objective.Hunt => $"Hunt {target?.Species.Name ?? "the alpha"}: bring down the marked alpha.",
        Objective.Survive => $"Survive {Rounds} rounds against a large pack.",
        Objective.Duel => $"Beat {Rival.Name}'s squad in {Battle.DuelRounds} rounds.",
        _ => "Rout the pack: drive off or recruit every wild creature.",
    };

    /// <summary>A board of missions. It changes after every battle (the seed is the encounter number).
    /// On some boards a rival tamer's duel takes the last place; <paramref name="rivalWins"/> counts your wins against each.</summary>
    public static List<Mission> Board(ulong encounter, ICollection<string> codex, int count = 4, IReadOnlyDictionary<string, int> rivalWins = null)
    {
        var list = new List<Mission>();
        var sighted = new HashSet<string>(); // no two cards promise the same part
        for (int i = 0; i < count; i++)
        {
            ulong seed = SeededRng.Derive(encounter * 977UL, (ulong)i + 1);
            if (i == count - 1 && Party.RivalBook.Challenger(encounter) is { } rival)
            {
                list.Add(Duel(rival, encounter, rivalWins?.GetValueOrDefault(rival.Id) ?? 0, seed));
                continue;
            }
            var rng = new SeededRng(seed);
            // The first card always uses a different region from the second, so the board varies.
            string mapName = BattleMap.Names[(i == 0 ? (int)(encounter % (ulong)BattleMap.Names.Length) : rng.Int(BattleMap.Names.Length))];
            list.Add(Contract(seed, rng, mapName, encounter, codex, sighted));
        }
        return list;
    }

    /// <summary>
    /// A contract on a map: an objective, a pack, maybe a sighting of a part your codex lacks (none
    /// already in <paramref name="sighted"/>). It draws from <paramref name="rng"/>, carrying on the caller's rolls.
    /// </summary>
    public static Mission Contract(ulong seed, SeededRng rng, string mapName, ulong encounter, ICollection<string> codex, HashSet<string> sighted)
    {
        var m = new Mission { Seed = seed, Encounter = encounter, MapName = mapName };
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
        return m;
    }

    /// <summary>A place's regular job: the same ask every time, a fresh pack, a little less pay.</summary>
    public static Mission Regular(ulong seed, World.LocationDef at)
    {
        var rng = new SeededRng(seed);
        var m = new Mission { Seed = seed, MapName = at.MapName, Location = at.Id, Goal = at.RegularGoal, Repeatable = true, Flavor = at.RegularAsk };
        m.Pack = m.Goal == Objective.Survive ? 5 : rng.Int(3, 4);
        m.Rounds = m.Goal == Objective.Survive ? 4 : 0;
        m.Stars = Math.Clamp(m.Pack - 2 + (m.Goal is Objective.Hunt or Objective.Survive ? 1 : 0), 1, 5);
        m.Reward = (6 + m.Pack * 3 + m.Goal switch { Objective.Hunt => 8, Objective.Survive => 10, _ => 0 }) * 3 / 4;
        m.Title = at.Regular;
        return m;
    }

    /// <summary>A dispatch on the board: no battle, a creature sent off for a few days.</summary>
    public static Mission Dispatching(World.DispatchDef job, World.LocationDef at) => new()
    {
        Seed = SeededRng.Hash(job.Id), MapName = at.MapName, Location = at.Id, Dispatch = job, Title = job.Title, Reward = job.Reward,
    };

    /// <summary>A rival tamer's challenge: their squad on one of their maps, stronger each time you win.</summary>
    public static Mission Duel(Party.RivalDef rival, ulong encounter, int wins, ulong seed)
    {
        var rng = new SeededRng(seed);
        var m = new Mission { Seed = seed, Encounter = encounter, Goal = Objective.Duel, Rival = rival, RivalWins = wins };
        m.MapName = rival.Turf[rng.Int(rival.Turf.Length)];
        m.Pack = Party.RivalBook.SquadSize(encounter);
        m.Stars = Math.Clamp(m.Pack / 2 + wins, 1, 5);
        m.Reward = 20 + m.Pack * 4 + wins * 6;
        m.Title = $"Duel: {rival.Name}";
        return m;
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
        var map = BattleMap.ByName(FaunaMap ?? MapName);
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
        if (Rival != null)
        {
            // A fair fight: they field as many creatures as you bring, at least three and at most Pack.
            var yours = squad.ToList();
            var duel = new Battle(BattleMap.ByName(MapName), yours, Array.Empty<Creature>(), Seed) { Goal = Goal, Mission = this };
            int count = Math.Clamp(yours.Count, Party.TamerBook.CreaturesEach, Pack);
            duel.Challenge(Rival.Tamer, Party.RivalBook.Squad(Rival, Encounter, RivalWins, Seed, count));
            return duel;
        }
        var (pack, target) = Wild();
        var battle = new Battle(BattleMap.ByName(MapName), squad, pack, Seed) { Goal = Goal, SurviveRounds = Rounds };
        battle.Target = target == null ? null : battle.Units.FirstOrDefault(u => u.Creature == target);
        battle.Mission = this;
        return battle;
    }
}
