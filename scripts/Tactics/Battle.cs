using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Tactics;

// What happened, in order, for the screen to animate. The rules finish instantly; the screen catches up.
public abstract record BattleEvent;
public sealed record TurnEvent(Unit U, Side Side, int Hp) : BattleEvent;
public sealed record MoveEvent(Unit U, List<Vector2I> Path) : BattleEvent;
public sealed record StrikeEvent(Unit A, Unit T) : BattleEvent;
public sealed record DamageEvent(Unit U, int Amount, bool Miss) : BattleEvent;
public sealed record PushEvent(Unit U, Vector2I To) : BattleEvent;
public sealed record FaintEvent(Unit U) : BattleEvent;
public sealed record RecruitEvent(Unit U, string Why) : BattleEvent;
public sealed record HealEvent(Unit U, int Amount) : BattleEvent;
public sealed record BerryEvent(Vector2I Tile, bool Placed) : BattleEvent;
public sealed record LogEvent(string Text) : BattleEvent;
public sealed record EndEvent(Side Winner) : BattleEvent;

/// <summary>
/// The rules of one battle, with no drawing: FFTA-style individual turns in speed order, move then
/// act (in either order, with the move undoable until you act), attitude reactions, and recruiting
/// wild creatures by meeting their attitude's condition (game-ideas.md, section 4).
/// </summary>
public sealed class Battle
{
    public readonly BattleMap Map;
    public readonly List<Unit> Units = new();
    public readonly List<BattleEvent> Events = new();
    public readonly HashSet<Vector2I> BerryTiles = new();
    public readonly List<Unit> Recruited = new();
    public int Berries = 3;
    public int Round;
    public Unit Active;
    public Side? Winner;

    readonly SeededRng _rng;
    readonly List<Unit> _queue = new();

    public IEnumerable<Unit> Alive => Units.Where(u => !u.Gone);

    public Battle(BattleMap map, IEnumerable<Creature> squad, IEnumerable<Creature> wild, ulong seed)
    {
        Map = map;
        _rng = new SeededRng(seed);
        foreach (var c in squad) Place(Unit.From(c, Side.Player), map.PlayerAnchor);
        foreach (var c in wild) Place(Unit.From(c, Side.Wild), map.WildAnchor);
    }

    void Place(Unit u, Vector2I anchor)
    {
        // The nearest free spot to the anchor, searching outward.
        for (int r = 0; r < Map.W + Map.H; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (Math.Abs(dx) + Math.Abs(dy) != r) continue;
            var p = anchor + new Vector2I(dx, dy);
            if (!CanStand(u, p)) continue;
            u.Pos = u.TurnStart = p;
            Units.Add(u);
            return;
        }
    }

    // ------------------------------------------------------------------ turns

    /// <summary>Upcoming turns, active unit first (for the turn-order strip).</summary>
    public IEnumerable<Unit> Upcoming()
    {
        if (Active != null && !Active.Gone) yield return Active;
        foreach (var u in _queue) if (!u.Gone) yield return u;
        foreach (var u in SpeedOrder()) yield return u; // the round after
    }

    List<Unit> SpeedOrder() => Alive.OrderByDescending(u => u.Spd).ThenBy(u => u.Side).ThenBy(u => Units.IndexOf(u)).ToList();

    public Unit NextTurn()
    {
        Active = null;
        if (Winner != null) return null;
        _queue.RemoveAll(u => u.Gone);
        if (_queue.Count == 0)
        {
            Round++;
            _queue.AddRange(SpeedOrder());
            foreach (var u in Units) u.GuardUsed = false;
        }
        var next = _queue[0];
        _queue.RemoveAt(0);
        next.Moved = next.Acted = false;
        next.TurnStart = next.Pos;
        Active = next;
        Events.Add(new TurnEvent(next, next.Side, next.Hp));
        return next;
    }

    // ------------------------------------------------------------------ movement

    /// <summary>How high a unit can step up or down between tiles.</summary>
    static int Climb(MoveType m) => m switch { MoveType.Fly => 99, MoveType.Climb => 3, _ => 1 };

    /// <summary>Cost of a tile for a mover, or -1 if it can't go there.</summary>
    static int TileCost(Terrain t, MoveType m) => (t, m) switch
    {
        (Terrain.Pit, MoveType.Fly) => 1,
        (Terrain.Pit, _) => -1,
        (Terrain.Chem, MoveType.Fly) => 1,
        (Terrain.Chem, MoveType.Swim) => 1,
        (Terrain.Chem, _) => -1,
        (_, MoveType.Swim) => 2, // swimmers are slow on land
        _ => 1,
    };

    int FootHeight(Unit u, Vector2I p) => u.TilesAt(p).Max(Map.HeightAt);

    Unit OccupantOf(Vector2I tile, Unit except = null) =>
        Alive.FirstOrDefault(o => o != except && o.Tiles.Contains(tile));

    /// <summary>Whether the unit could stand with its footprint at p (huge creatures need flat ground).</summary>
    public bool CanStand(Unit u, Vector2I p)
    {
        int lo = int.MaxValue, hi = int.MinValue;
        foreach (var t in u.TilesAt(p))
        {
            if (!Map.InBounds(t) || TileCost(Map.TerrainAt(t), u.MoveType) < 0 || OccupantOf(t, u) != null) return false;
            lo = Math.Min(lo, Map.HeightAt(t)); hi = Math.Max(hi, Map.HeightAt(t));
        }
        return hi - lo <= 1;
    }

    /// <summary>Every tile the unit can reach this turn, with the step it came from.</summary>
    public Dictionary<Vector2I, Vector2I> Reachable(Unit u)
    {
        // You can pass through allies but not stop on them.
        var all = ReachableAllSteps(u);
        foreach (var p in all.Keys.ToList())
            if (!CanStand(u, p)) all.Remove(p);
        return all;
    }

    int StepCost(Unit u, Vector2I from, Vector2I to)
    {
        int worst = 0;
        foreach (var t in u.TilesAt(to))
        {
            if (!Map.InBounds(t)) return -1;
            int c = TileCost(Map.TerrainAt(t), u.MoveType);
            if (c < 0) return -1;
            var occ = OccupantOf(t, u);
            if (occ != null && occ.Side != u.Side) return -1; // enemies block
            worst = Math.Max(worst, c);
        }
        if (Math.Abs(FootHeight(u, to) - FootHeight(u, from)) > Climb(u.MoveType)) return -1;
        return worst;
    }

    public List<Vector2I> PathTo(Unit u, Vector2I dest)
    {
        // The path may pass through allies it could not stop on.
        var all = ReachableAllSteps(u);
        if (!all.ContainsKey(dest) || !CanStand(u, dest)) return null;
        var path = new List<Vector2I> { dest };
        while (path[^1] != u.Pos) path.Add(all[path[^1]]);
        path.Reverse();
        return path;
    }

    Dictionary<Vector2I, Vector2I> ReachableAllSteps(Unit u)
    {
        var cost = new Dictionary<Vector2I, int> { [u.Pos] = 0 };
        var prev = new Dictionary<Vector2I, Vector2I>();
        var open = new List<Vector2I> { u.Pos };
        var dirs = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        while (open.Count > 0)
        {
            open.Sort((a, b) => cost[a].CompareTo(cost[b]));
            var cur = open[0];
            open.RemoveAt(0);
            foreach (var d in dirs)
            {
                var nxt = cur + d;
                int step = StepCost(u, cur, nxt);
                if (step < 0) continue;
                int c = cost[cur] + step;
                if (c > u.Move || (cost.TryGetValue(nxt, out int old) && old <= c)) continue;
                cost[nxt] = c;
                prev[nxt] = cur;
                open.Add(nxt);
            }
        }
        return prev;
    }

    public bool Move(Unit u, Vector2I dest)
    {
        if (u.Moved || dest == u.Pos) return false;
        var path = PathTo(u, dest);
        if (path == null) return false;
        u.Pos = dest;
        u.Moved = true;
        Events.Add(new MoveEvent(u, path));
        return true;
    }

    public bool CanUndo(Unit u) => u.Moved && !u.Acted;

    public void UndoMove(Unit u)
    {
        if (!CanUndo(u)) return;
        Events.Add(new MoveEvent(u, new List<Vector2I> { u.Pos, u.TurnStart }));
        u.Pos = u.TurnStart;
        u.Moved = false;
    }

    // ------------------------------------------------------------------ combat

    /// <summary>Tile distance between two footprints (a huge creature is close from any side).</summary>
    public static (int dx, int dy) Gap(Vector2I aPos, int aSize, Vector2I bPos, int bSize)
    {
        int dx = Math.Max(0, Math.Max(aPos.X - (bPos.X + bSize - 1), bPos.X - (aPos.X + aSize - 1)));
        int dy = Math.Max(0, Math.Max(aPos.Y - (bPos.Y + bSize - 1), bPos.Y - (aPos.Y + aSize - 1)));
        return (dx, dy);
    }

    public static int Dist(Unit a, Vector2I aPos, Unit b)
    {
        var (dx, dy) = Gap(aPos, a.Size, b.Pos, b.Size);
        return dx + dy;
    }

    public bool InRange(Unit a, Vector2I aPos, Unit t)
    {
        var (dx, dy) = Gap(aPos, a.Size, t.Pos, t.Size);
        int d = dx + dy;
        if (d == 1) return Math.Abs(FootHeight(a, aPos) - FootHeight(t, t.Pos)) <= 2;
        return a.Range == 2 && d == 2 && (dx == 0 || dy == 0); // a whip tail reaches two tiles in a line
    }

    public List<Unit> Targets(Unit a) => Alive.Where(t => t.Side != a.Side && InRange(a, a.Pos, t)).ToList();

    /// <summary>Damage and hit chance, shown before you commit (the notes ask for clear previews).</summary>
    public (int dmg, int hit) Preview(Unit a, Unit t)
    {
        int heightDiff = FootHeight(a, a.Pos) - FootHeight(t, t.Pos);
        float mult = heightDiff > 0 ? 1.15f : heightDiff < 0 ? 0.9f : 1f; // high ground hits harder
        int dmg = Math.Max(1, Mathf.RoundToInt((a.Atk * 1.5f - t.Def * 0.75f) * mult));
        int hit = 85 + (a.Spd - t.Spd) * 3 + a.HitBonus + (heightDiff > 0 ? 5 : 0);
        if (t.Attitude == Attitude.Skittish) hit -= 15;
        if (t.Creature.Size == SizeClass.Tiny && Map.TerrainAt(t.Pos) == Terrain.Grass) hit -= 10; // hides in the moss
        return (dmg, Math.Clamp(hit, 30, 98));
    }

    public void Attack(Unit a, Unit t) => Attack(a, t, counter: false);

    void Attack(Unit a, Unit t, bool counter)
    {
        if (!counter) a.Acted = true;
        Events.Add(new StrikeEvent(a, t));

        // Loyal reaction: an adjacent loyal ally steps in and takes the hit.
        var target = t;
        if (!counter)
        {
            var guard = Alive.FirstOrDefault(g => g != t && g.Side == t.Side && g.Attitude == Attitude.Loyal
                && !g.GuardUsed && Dist(g, g.Pos, t) == 1);
            if (guard != null)
            {
                guard.GuardUsed = true;
                target = guard;
                Log($"{guard.Name} shields {t.Name}!");
            }
        }

        var (dmg, hit) = Preview(a, target);
        if (_rng.Int(100) >= hit)
        {
            Events.Add(new DamageEvent(target, 0, Miss: true));
            AfterHit(a, target, survived: true, counter);
            return;
        }
        int dealt = Math.Max(1, Mathf.RoundToInt(dmg * _rng.Range(0.9f, 1.1f)));
        Hurt(target, dealt, a);

        if (target.Thorns > 0 && !a.Gone && Dist(a, a.Pos, target) == 1)
        {
            Log($"{a.Name} is cut on {target.Name}'s spines.");
            Hurt(a, target.Thorns, target);
        }
        if (!target.Gone && a.Knockback && !counter) Knock(a, target);
        AfterHit(a, target, survived: !target.Gone && target.Side != a.Side, counter);
    }

    void Hurt(Unit u, int amount, Unit by)
    {
        u.Hp -= amount;
        Events.Add(new DamageEvent(u, amount, Miss: false));
        if (by.Side == Side.Player && u.Side == Side.Wild) u.HitBy.Add(by);
        u.LastAttacker = by;
        if (u.Hp <= 0) Defeat(u);
    }

    void Knock(Unit a, Unit t)
    {
        var d = (t.Pos + new Vector2(t.Size - 1, t.Size - 1) / 2f) - (a.Pos + new Vector2(a.Size - 1, a.Size - 1) / 2f);
        var dir = Math.Abs(d.X) >= Math.Abs(d.Y) ? new Vector2I(Math.Sign(d.X), 0) : new Vector2I(0, Math.Sign(d.Y));
        var dest = t.Pos + dir;
        if (CanStand(t, dest) && FootHeight(t, dest) <= FootHeight(t, t.Pos))
        {
            t.Pos = dest;
            Events.Add(new PushEvent(t, dest));
        }
        else
        {
            Log($"{t.Name} slams into the wall!");
            Hurt(t, 2, a);
        }
    }

    void AfterHit(Unit a, Unit target, bool survived, bool counter)
    {
        // An aggressive wild creature is won over by enduring it (counted when it attacks).
        if (!counter && a.Side == Side.Wild && a.Attitude == Attitude.Aggressive && target.Side == Side.Player)
            a.Weathered = target.Gone ? 0 : a.Weathered + 1;

        if (!survived || counter || a.Gone) return;
        switch (target.Attitude)
        {
            case Attitude.Aggressive when InRange(target, target.Pos, a):
                Log($"{target.Name} counterattacks!");
                Attack(target, a, counter: true);
                break;
            case Attitude.Skittish:
                Retreat(target, a);
                break;
        }
    }

    /// <summary>Skittish reaction: after being hit, scramble one tile away from the attacker.</summary>
    void Retreat(Unit u, Unit from)
    {
        Vector2I best = u.Pos;
        int bestD = Dist(u, u.Pos, from);
        foreach (var d in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
        {
            var p = u.Pos + d;
            if (!CanStand(u, p) || Math.Abs(FootHeight(u, p) - FootHeight(u, u.Pos)) > Climb(u.MoveType)) continue;
            int dist = Dist(u, p, from);
            if (dist > bestD) { best = p; bestD = dist; }
        }
        if (best == u.Pos) return;
        Log($"{u.Name} scrambles away.");
        Events.Add(new MoveEvent(u, new List<Vector2I> { u.Pos, best }));
        u.Pos = best;
    }

    void Defeat(Unit u)
    {
        if (u.Gone) return;
        if (u.Side == Side.Wild && u.Attitude == Attitude.Proud && u.HitBy.Count == 1)
        {
            Recruit(u, $"bested one on one by {u.HitBy.First().Name}");
            return;
        }
        u.Gone = true;
        u.Hp = 0;
        Events.Add(new FaintEvent(u));
        Log(u.Side == Side.Wild ? $"{u.Name} flees!" : $"{u.Name} is knocked out.");
        CheckEnd();
    }

    void Recruit(Unit u, string why)
    {
        u.Side = Side.Player;
        u.Hp = Math.Max(u.Hp, u.MaxHp / 2);
        Recruited.Add(u);
        Events.Add(new RecruitEvent(u, why));
        Log($"{u.Name} joins you: {why}.");
        CheckEnd();
    }

    void CheckEnd()
    {
        if (Winner != null) return;
        if (!Alive.Any(u => u.Side == Side.Wild)) Winner = Side.Player;
        else if (!Alive.Any(u => u.Side == Side.Player)) Winner = Side.Wild;
        if (Winner != null) Events.Add(new EndEvent(Winner.Value));
    }

    void Log(string text) => Events.Add(new LogEvent(text));

    // ------------------------------------------------------------------ items and turn end

    /// <summary>Tiles next to the unit where a berry could be set down (the curious recruit lure).</summary>
    public List<Vector2I> BerrySpots(Unit u)
    {
        var spots = new List<Vector2I>();
        if (Berries <= 0 || u.Acted) return spots;
        foreach (var t in u.Tiles)
        foreach (var d in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
        {
            var p = t + d;
            if (Map.InBounds(p) && Map.TerrainAt(p) is not (Terrain.Pit or Terrain.Chem) && OccupantOf(p) == null
                && !BerryTiles.Contains(p) && !spots.Contains(p)) spots.Add(p);
        }
        return spots;
    }

    public void OfferBerry(Unit u, Vector2I tile)
    {
        Berries--;
        u.Acted = true;
        BerryTiles.Add(tile);
        Events.Add(new BerryEvent(tile, Placed: true));
        Log($"{u.Name} sets down a glowberry.");
    }

    public void EndTurn(Unit u)
    {
        if (!u.Gone && u.Attitude == Attitude.Curious)
        {
            // Curious reaction: grabs any item within reach.
            var berry = BerryTiles.FirstOrDefault(b => u.Tiles.Any(t => Math.Abs(t.X - b.X) + Math.Abs(t.Y - b.Y) <= 1), new Vector2I(-1, -1));
            if (berry.X >= 0)
            {
                BerryTiles.Remove(berry);
                Events.Add(new BerryEvent(berry, Placed: false));
                int heal = Math.Min(10, u.MaxHp - u.Hp);
                u.Hp += heal;
                Events.Add(new HealEvent(u, heal));
                Log($"{u.Name} snatches the glowberry.");
                if (u.Side == Side.Wild) Recruit(u, "lured with a glowberry");
            }
        }
        if (!u.Gone && u.Side == Side.Wild && u.Attitude == Attitude.Aggressive && u.Weathered >= 3)
            Recruit(u, "its fury weathered for three attacks");

        foreach (var w in Alive.Where(w => w.Side == Side.Wild).ToList())
        {
            if (w.Attitude == Attitude.Skittish && Cornered(w)) Recruit(w, "cornered with nowhere to run");
            else if (w.Attitude == Attitude.Loyal && Recruited.Count > 0 && !Alive.Any(o => o != w && o.Side == Side.Wild))
                Recruit(w, "its packmates went with you");
        }
        Active = null;
    }

    /// <summary>Skittish recruit condition: no tile it could step to.</summary>
    public bool Cornered(Unit u) => !new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down }
        .Any(d => StepCost(u, u.Pos, u.Pos + d) >= 0 && CanStand(u, u.Pos + d));

    /// <summary>A short note on what it takes to recruit a wild unit, with progress.</summary>
    public string RecruitHint(Unit u) => u.Attitude switch
    {
        Attitude.Proud => u.HitBy.Count <= 1 ? "Proud: defeat it using only one of your creatures" : "Proud: too many hands on it - it will flee",
        Attitude.Skittish => "Skittish: corner it so it has nowhere to step",
        Attitude.Curious => $"Curious: lure it with a glowberry ({Berries} left, B to set one down)",
        Attitude.Aggressive => $"Aggressive: survive its attacks without falling ({u.Weathered}/3)",
        _ => "Loyal: recruit its packmates, then it follows",
    };

    // ------------------------------------------------------------------ AI

    /// <summary>
    /// A computer turn. Wild creatures act out their attitude, so they telegraph by temperament:
    /// aggressive ones charge, skittish ones keep away, curious ones go for items, loyal ones stay
    /// with the pack, proud ones go after whoever hit them.
    /// </summary>
    public void AiTurn(Unit u)
    {
        var foes = Alive.Where(o => o.Side != u.Side).ToList();
        if (foes.Count == 0) { EndTurn(u); return; }
        var tiles = Reachable(u).Keys.Append(u.Pos).ToList();
        var mood = u.Side == Side.Player ? Attitude.Aggressive : u.Attitude;

        int Nearest(Vector2I p) => foes.Min(f => Dist(u, p, f));
        Unit BestTarget(Vector2I p)
        {
            var inRange = foes.Where(f => InRange(u, p, f)).ToList();
            if (inRange.Count == 0) return null;
            if (mood == Attitude.Proud && u.LastAttacker != null && inRange.Contains(u.LastAttacker)) return u.LastAttacker;
            return inRange.OrderBy(f => f.Hp).First();
        }

        Vector2I dest = u.Pos;
        switch (mood)
        {
            case Attitude.Skittish:
                if (Nearest(u.Pos) <= 4) dest = tiles.OrderByDescending(Nearest).ThenBy(p => p == u.Pos ? 0 : 1).First();
                break;
            case Attitude.Curious when BerryTiles.Count > 0:
                dest = tiles.OrderBy(p => BerryTiles.Min(b => Math.Abs(b.X - p.X) + Math.Abs(b.Y - p.Y))).First();
                break;
            case Attitude.Loyal:
                {
                    var pack = Alive.Where(o => o != u && o.Side == u.Side).ToList();
                    var attack = tiles.Where(p => BestTarget(p) != null).ToList();
                    if (attack.Count > 0) dest = attack.OrderBy(p => pack.Count == 0 ? 0 : pack.Min(o => Dist(u, p, o))).First();
                    else if (pack.Count > 0) dest = tiles.OrderBy(p => pack.Min(o => Dist(u, p, o))).ThenBy(Nearest).First();
                    break;
                }
            default:
                {
                    var attack = tiles.Where(p => BestTarget(p) != null).ToList();
                    dest = attack.Count > 0
                        ? attack.OrderBy(p => BestTarget(p).Hp).ThenByDescending(p => FootHeight(u, p)).First()
                        : tiles.OrderBy(Nearest).First();
                    break;
                }
        }

        if (dest != u.Pos) Move(u, dest);
        // Skittish creatures only fight back when there is nowhere left to go.
        bool fights = mood != Attitude.Skittish || Cornered(u);
        var target = BestTarget(u.Pos);
        if (fights && target != null) Attack(u, target);
        EndTurn(u);
    }
}
