using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
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
public sealed record SkillEvent(Unit U, SkillDef Skill, Vector2I Center, List<Vector2I> Area) : BattleEvent;
public sealed record StatusEvent(Unit U, string Text) : BattleEvent;

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

    // The mission: what counts as winning. Rout (the default) means no wild creature left standing.
    public Objective Goal = Objective.Rout;
    public Unit Target;          // Capture: recruit it. Hunt: bring it down.
    public int SurviveRounds;    // Survive: still standing after this many rounds
    public Mission Mission;

    readonly SeededRng _rng;
    readonly List<Unit> _queue = new();

    public IEnumerable<Unit> Alive => Units.Where(u => !u.Gone);

    public Battle(BattleMap map, IEnumerable<(Creature creature, Training training)> squad, IEnumerable<Creature> wild, ulong seed)
    {
        Map = map;
        _rng = new SeededRng(seed);
        foreach (var (c, t) in squad) Place(Unit.From(c, Side.Player, t), map.PlayerAnchor);
        foreach (var c in wild) Place(Unit.From(c, Side.Wild), map.WildAnchor);
    }

    /// <summary>
    /// Puts one of your creatures under a warden, before the first turn: the warden's boost goes on
    /// its stats if it reaches this kind of creature. Part of the rules, so a replay or a match sees it too.
    /// </summary>
    public void Lead(Creature c, Party.WardenDef w)
    {
        var u = Units.FirstOrDefault(x => x.Creature == c && x.Side == Side.Player);
        if (u == null || w == null) return;
        u.Warden = w;
        if (u.Boost is not { } b) return;
        u.Atk += b.Atk; u.Def += b.Def; u.Spd += b.Spd; u.Move += b.Move; u.HitBonus += b.Hit;
        u.MaxHp = Mathf.RoundToInt(u.MaxHp * (1 + b.HpPct));
        u.Hp = u.MaxHp;
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

    List<Unit> SpeedOrder() => Alive.OrderByDescending(u => u.SpdNow).ThenBy(u => u.Side).ThenBy(u => Units.IndexOf(u)).ToList();

    public Unit NextTurn()
    {
        Active = null;
        if (Winner != null) return null;
        _queue.RemoveAll(u => u.Gone);
        if (_queue.Count == 0)
        {
            Round++;
            if (Goal == Objective.Survive && Round > SurviveRounds && Alive.Any(u => u.Side == Side.Player))
            {
                Log("You held out!");
                Winner = Side.Player;
                Events.Add(new EndEvent(Side.Player));
                return null;
            }
            _queue.AddRange(SpeedOrder());
            foreach (var u in Units) u.GuardUsed = false;
        }
        var next = _queue[0];
        _queue.RemoveAt(0);
        next.Moved = next.Acted = false;
        next.TurnStart = next.Pos;
        Active = next;
        Events.Add(new TurnEvent(next, next.Side, next.Hp));

        // Start of turn: cooldowns tick, poison bites, a stunned creature loses its turn.
        if (next.Passive is { Regen: > 0 } regen) HealUnit(next, Mathf.RoundToInt(next.MaxHp * regen.Regen));
        if (next.Boost is { Regen: > 0 } led) HealUnit(next, Mathf.RoundToInt(next.MaxHp * led.Regen));
        foreach (var id in next.Cooldowns.Keys.ToList()) next.Cooldowns[id]--;
        if (next.PoisonTurns > 0)
        {
            next.PoisonTurns--;
            Log($"{next.Name} suffers from poison.");
            Hurt(next, next.PoisonDmg, null);
        }
        if (next.Gone || next.Stunned)
        {
            if (next.Stunned) { next.Stunned = false; Events.Add(new StatusEvent(next, "stunned!")); Log($"{next.Name} is stunned and can't act."); }
            if (!next.Gone) EndTurn(next);
            return NextTurn();
        }
        return next;
    }

    // ------------------------------------------------------------------ movement

    /// <summary>How high a unit can step up or down between tiles.</summary>
    static int Climb(Unit u) => Math.Max(u.MoveType switch { MoveType.Fly => 99, MoveType.Climb => 3, _ => 1 }, u.Passive?.Climb ?? 0);

    /// <summary>Cost of a tile for a mover, or -1 if it can't go there.</summary>
    /// <summary>Cost for this unit: amphibious creatures wade chem, and swimmers who are aren't slowed on land.</summary>
    static int TileCost(Terrain t, Unit u) =>
        u.Passive is { Amphibious: true } && t != Terrain.Pit ? 1 : TileCost(t, u.MoveType);

    static int TileCost(Terrain t, MoveType m) => (t, m) switch
    {
        (Terrain.Pit, MoveType.Fly) => 1,
        (Terrain.Pit, _) => -1,
        (Terrain.Chem, MoveType.Fly) => 1,
        (Terrain.Chem, MoveType.Swim) => 1,
        (Terrain.Chem, _) => -1,
        (Terrain.Water, MoveType.Fly) => 1,
        (Terrain.Water, MoveType.Swim) => 1,
        (Terrain.Water, _) => -1,
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
            if (!Map.InBounds(t) || Map.Blocked(t) || TileCost(Map.TerrainAt(t), u) < 0 || OccupantOf(t, u) != null) return false;
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
            if (!Map.InBounds(t) || Map.Blocked(t) && u.MoveType != MoveType.Fly) return -1; // fliers pass over props, but can't land on them
            int c = TileCost(Map.TerrainAt(t), u);
            if (c < 0) return -1;
            var occ = OccupantOf(t, u);
            if (occ != null && occ.Side != u.Side) return -1; // enemies block
            worst = Math.Max(worst, c);
        }
        if (Math.Abs(FootHeight(u, to) - FootHeight(u, from)) > Climb(u)) return -1;
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
                if (c > u.MoveNow || (cost.TryGetValue(nxt, out int old) && old <= c)) continue;
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
        if (a.Passive is { Predator: > 0 } pred && t.Hp * 2 < t.MaxHp) mult *= 1 + pred.Predator;
        int atk = a.AtkNow + (a.Passive is { PackBond: true } ? Alive.Count(o => o != a && o.Side == a.Side && Dist(o, o.Pos, a) == 1) : 0);
        int dmg = Math.Max(1, Mathf.RoundToInt((atk * 1.5f - t.DefNow * 0.75f) * mult));
        int hit = 85 + (a.SpdNow - t.SpdNow) * 3 + a.HitBonus + (heightDiff > 0 ? 5 : 0) - t.Mod(StatKind.Evade);
        if (t.Attitude == Attitude.Skittish) hit -= 15;
        if (t.Creature.Size == SizeClass.Tiny && Map.TerrainAt(t.Pos) == Terrain.Grass) hit -= 10; // hides in the moss
        return (dmg, Math.Clamp(hit, 30, 98));
    }

    public void Attack(Unit a, Unit t) => Attack(a, t, counter: false);

    void Attack(Unit a, Unit t, bool counter, float power = 1f)
    {
        if (!counter) a.Acted = true;
        Events.Add(new StrikeEvent(a, t));

        // Loyal reaction: an adjacent loyal ally steps in and takes the hit.
        var target = t;
        if (!counter)
        {
            var guard = Alive.FirstOrDefault(g => g != t && g.Side == t.Side && g.Attitude == Attitude.Loyal
                && !g.GuardUsed && Dist(g, g.Pos, t) == 1);
            // The Bodyguard reaction does the same, by chance.
            guard ??= Alive.FirstOrDefault(g => g != t && g.Side == t.Side && g.Reaction is { React: ReactionKind.Guard } r
                && Dist(g, g.Pos, t) == 1 && _rng.Chance(r.ReactChance));
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
        if (Dodges(target))
        {
            Events.Add(new DamageEvent(target, 0, Miss: true));
            return;
        }
        int dealt = Math.Max(1, Mathf.RoundToInt(dmg * power * _rng.Range(0.9f, 1.1f)));
        Hurt(target, dealt, a);
        if (a.Passive is { Drain: > 0 } thirst && !a.Gone) HealUnit(a, Mathf.RoundToInt(dealt * thirst.Drain));

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
        if (by != null && by.Side == Side.Player && u.Side == Side.Wild) u.HitBy.Add(by);
        if (by != null) u.LastAttacker = by;
        if (u.Hp <= 0) Defeat(u);
        else if (u.Reaction is { React: ReactionKind.Adrenaline } adr && !u.AdrenalineUsed && u.Hp * 2 < u.MaxHp)
        {
            u.AdrenalineUsed = true;
            Status(u, "adrenaline!");
            HealUnit(u, Mathf.RoundToInt(u.MaxHp * adr.ReactPower));
        }
    }

    /// <summary>The Premonition reaction: a hit that would land might not.</summary>
    bool Dodges(Unit t)
    {
        if (t.Reaction is not { React: ReactionKind.Dodge } r || !_rng.Chance(r.ReactChance)) return false;
        Status(t, "foreseen!");
        return true;
    }

    void Knock(Unit a, Unit t)
    {
        if (t.Passive is { Unstoppable: true }) return;
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
        bool countered = false;
        switch (target.Attitude)
        {
            case Attitude.Aggressive when InRange(target, target.Pos, a):
                Log($"{target.Name} counterattacks!");
                Attack(target, a, counter: true);
                countered = true;
                break;
            case Attitude.Skittish:
                Retreat(target, a);
                break;
        }
        React(a, target, Dist(a, a.Pos, target) == 1, countered);
    }

    /// <summary>
    /// The equipped reaction of a creature that was just hit (FFTA's R-abilities), and the Protector
    /// reaction of any ally beside it. Attitude reactions come first; a creature never counters twice.
    /// </summary>
    void React(Unit a, Unit t, bool melee, bool countered = false)
    {
        if (t.Gone || a.Gone || t.Side == a.Side) return;
        foreach (var p in Alive.Where(p => p != t && p.Side == t.Side && Dist(p, p.Pos, t) == 1 && p.Reaction is { React: ReactionKind.Protector }).ToList())
        {
            p.Buffs.Add((StatKind.Atk, p.Reaction.ReactAmount, 2));
            Status(p, $"protective! ATK +{p.Reaction.ReactAmount}");
        }
        var r = t.Reaction;
        if (r == null) return;
        switch (r.React)
        {
            case ReactionKind.Counter when melee && !countered && InRange(t, t.Pos, a):
                Log($"{t.Name}: {r.Name}!");
                Attack(t, a, counter: true, r.ReactPower);
                break;
            case ReactionKind.ReturnFire when !melee && Dist(t, t.Pos, a) <= 4:
                Log($"{t.Name}: {r.Name}!");
                Attack(t, a, counter: true, r.ReactPower);
                break;
            case ReactionKind.Retreat:
                Retreat(t, a);
                if (r.ReactAmount > 0) t.Buffs.Add((StatKind.Evade, r.ReactAmount, 1));
                break;
            case ReactionKind.Brace:
                t.Buffs.Add((StatKind.Def, r.ReactAmount, 1));
                Status(t, $"braced, DEF +{r.ReactAmount}");
                break;
            case ReactionKind.Burst when melee:
                Log($"{t.Name}: {r.Name}!");
                foreach (var e in Alive.Where(e => e.Side != t.Side && Dist(e, e.Pos, t) == 1).ToList()) Hurt(e, r.ReactAmount, t);
                break;
            case ReactionKind.Shock when melee:
                Log($"{t.Name}: {r.Name}!");
                Hurt(a, r.ReactAmount, t);
                if (!a.Gone && _rng.Chance(r.ReactChance)) { a.Stunned = true; Status(a, "stunned"); }
                break;
            case ReactionKind.RallyCry:
                foreach (var p in Alive.Where(p => p != t && p.Side == t.Side && Dist(p, p.Pos, t) == 1).ToList())
                {
                    p.Buffs.Add((StatKind.Atk, r.ReactAmount, 1));
                    Status(p, $"rallied, ATK +{r.ReactAmount}");
                }
                break;
            case ReactionKind.DirtyRetort when melee && _rng.Chance(r.ReactChance):
                Log($"{t.Name}: {r.Name}!");
                a.Stunned = true;
                Status(a, "stunned");
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
            if (!CanStand(u, p) || Math.Abs(FootHeight(u, p) - FootHeight(u, u.Pos)) > Climb(u)) continue;
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
        if (!Alive.Any(u => u.Side == Side.Player)) Winner = Side.Wild;
        else if (Goal == Objective.Capture && Target != null && Target.Side == Side.Player) Winner = Side.Player;
        else if (Goal == Objective.Capture && Target != null && Target.Gone) { Log($"{Target.Name} got away. The capture failed."); Winner = Side.Wild; }
        else if (Goal == Objective.Hunt && Target != null && (Target.Gone || Target.Side == Side.Player)) Winner = Side.Player;
        else if (!Alive.Any(u => u.Side == Side.Wild)) Winner = Side.Player;
        if (Winner != null) Events.Add(new EndEvent(Winner.Value));
    }

    void Log(string text) => Events.Add(new LogEvent(text));

    // ------------------------------------------------------------------ skills

    static int TileGap(Unit u, Vector2I pos, Vector2I tile)
    {
        var (dx, dy) = Gap(pos, u.Size, tile, 1);
        return dx + dy;
    }

    /// <summary>A skill's reach for this user: ranged skills reach further with Eagle Eye.</summary>
    public static int RangeOf(Unit u, SkillDef s) => s.Range + (s.Range > 1 ? u.Passive?.RangeBonus ?? 0 : 0);

    /// <summary>Tiles a skill can be aimed at from where the unit stands (self-centred skills: its own tile).</summary>
    public List<Vector2I> SkillTiles(Unit u, SkillDef s)
    {
        var tiles = new List<Vector2I>();
        if (s.Target == TargetKind.Self || s.AroundSelf) { tiles.Add(u.Pos); return tiles; }
        int range = RangeOf(u, s);
        for (int y = 0; y < Map.H; y++)
        for (int x = 0; x < Map.W; x++)
        {
            var p = new Vector2I(x, y);
            int d = TileGap(u, u.Pos, p);
            if (d > range || (s.Target != TargetKind.Ally && d < 1)) continue;
            if (s.Straight)
            {
                var (dx, dy) = Gap(u.Pos, u.Size, p, 1);
                if (dx != 0 && dy != 0) continue;
            }
            var occ = OccupantOf(p);
            switch (s.Target)
            {
                case TargetKind.Enemy when occ != null && occ.Side != u.Side:
                    if (s.Leap ? LeapSpot(u, occ, range) != null : range > 1 || InRange(u, u.Pos, occ)) tiles.Add(p);
                    break;
                case TargetKind.Ally when occ != null && occ.Side == u.Side:
                    tiles.Add(p);
                    break;
                case TargetKind.Area when Map.TerrainAt(p) != Terrain.Pit:
                    tiles.Add(p);
                    break;
                case TargetKind.Tile when CanStand(u, p):
                    tiles.Add(p);
                    break;
            }
        }
        return tiles;
    }

    /// <summary>Where a leaping unit lands: the free spot beside the target nearest to it.</summary>
    Vector2I? LeapSpot(Unit u, Unit target, int range)
    {
        Vector2I? best = null;
        int bestD = int.MaxValue;
        for (int y = target.Pos.Y - u.Size; y <= target.Pos.Y + target.Size; y++)
        for (int x = target.Pos.X - u.Size; x <= target.Pos.X + target.Size; x++)
        {
            var p = new Vector2I(x, y);
            var (dx, dy) = Gap(p, u.Size, target.Pos, target.Size);
            if (dx + dy != 1 || (p != u.Pos && !CanStand(u, p))) continue;
            var (mx, my) = Gap(u.Pos, u.Size, p, u.Size);
            int d = mx + my;
            if (d > range || d >= bestD) continue;
            best = p;
            bestD = d;
        }
        return best;
    }

    /// <summary>The tiles a skill's effect covers, for highlighting.</summary>
    public List<Vector2I> SkillArea(Unit u, SkillDef s, Vector2I tile)
    {
        var area = new List<Vector2I>();
        if (s.Radius == 0) { area.Add(s.Target == TargetKind.Self ? u.Pos : tile); return area; }
        for (int y = 0; y < Map.H; y++)
        for (int x = 0; x < Map.W; x++)
        {
            var p = new Vector2I(x, y);
            int d = s.AroundSelf ? TileGap(u, u.Pos, p) : Math.Abs(p.X - tile.X) + Math.Abs(p.Y - tile.Y);
            if (d <= s.Radius) area.Add(p);
        }
        return area;
    }

    /// <summary>Who a skill aimed at a tile would affect.</summary>
    public List<Unit> SkillTargets(Unit u, SkillDef s, Vector2I tile)
    {
        if (s.Target == TargetKind.Tile) return new List<Unit>();
        bool friendly = s.Target is TargetKind.Ally or TargetKind.Self;
        if (s.Target == TargetKind.Self) return new List<Unit> { u };
        if (s.Radius == 0)
        {
            var occ = OccupantOf(tile);
            return occ != null && (occ.Side == u.Side) == friendly ? new List<Unit> { occ } : new List<Unit>();
        }
        var area = SkillArea(u, s, tile);
        return Alive.Where(o => (o.Side == u.Side) == friendly && o.Tiles.Any(area.Contains)).ToList();
    }

    static bool Rolls(SkillDef s) => s.Power > 0 || s.Poison > 0 || s.StunChance > 0 || s.Root;

    /// <summary>The damage multiplier of a skill on a target: its power, plus high-ground and flanking bonuses.</summary>
    float PowerOn(Unit u, SkillDef s, Unit t)
    {
        float p = s.Power;
        if (s.HighGround > 0 && FootHeight(u, u.Pos) > FootHeight(t, t.Pos)) p += s.HighGround;
        if (s.Flank > 0 && Alive.Any(o => o != u && o.Side == u.Side && Dist(o, o.Pos, t) == 1)) p += s.Flank;
        if (s.Desperate > 0 && u.Hp * 2 < u.MaxHp) p += s.Desperate;
        if (s.Execute > 0 && t.Hp * 10 < t.MaxHp * 4) p += s.Execute;
        return p * (1 + (u.Passive?.SkillPower ?? 0) + (u.Boost?.SkillPower ?? 0));
    }

    int HitOn(Unit u, SkillDef s, Unit t) => Math.Clamp(Preview(u, t).hit + s.HitBonus, 5, 99);

    /// <summary>A one-line preview of a skill on a target tile (the notes ask for clear previews).</summary>
    /// <summary>What a skill aimed at a tile would do to each creature it reaches: damage, chance to hit, healing.</summary>
    public List<(Unit t, int? dmg, int? hit, int? heal)> SkillOutlook(Unit u, SkillDef s, Vector2I tile)
    {
        var rows = new List<(Unit, int?, int?, int?)>();
        if (s.Target == TargetKind.Tile) return rows;
        foreach (var t in SkillTargets(u, s, tile))
        {
            if (t.Side != u.Side && Rolls(s))
            {
                int? dmg = s.Power > 0 ? Math.Max(1, Mathf.RoundToInt(Preview(u, t).dmg * PowerOn(u, s, t))) : null;
                rows.Add((t, dmg, HitOn(u, s, t), null));
            }
            else if (s.Heal > 0) rows.Add((t, null, null, Math.Min(t.MaxHp - t.Hp, Mathf.RoundToInt(t.MaxHp * s.Heal))));
            else rows.Add((t, null, null, null));
        }
        return rows;
    }

    public string SkillPreview(Unit u, SkillDef s, Vector2I tile)
    {
        if (s.Target == TargetKind.Tile) return s.Name + ": move to this tile";
        var rows = SkillOutlook(u, s, tile);
        if (rows.Count == 0) return s.Name + ": nothing there";
        var parts = rows.Take(4).Select(r =>
            r.hit != null ? r.t.Name + " " + (r.dmg != null ? "~" + r.dmg + " dmg, " : "") + r.hit + "%"
            : r.heal != null ? r.t.Name + " +" + r.heal + " HP"
            : r.t.Name);
        string more = rows.Count > 4 ? " and " + (rows.Count - 4) + " more" : "";
        return s.Name + ": " + string.Join("; ", parts) + more;
    }

    void HealUnit(Unit t, int amount)
    {
        amount = Math.Min(t.MaxHp - t.Hp, amount);
        if (amount <= 0 || t.Gone) return;
        t.Hp += amount;
        Events.Add(new HealEvent(t, amount));
    }

    /// <summary>A boost or curse; boosts given by an Echo passive last a turn longer.</summary>
    void Apply(Unit from, Unit t, (StatKind stat, int amount, int turns) b)
    {
        if (b.amount > 0 && from.Passive is { Echo: true }) b.turns++;
        t.Buffs.Add(b);
        Status(t, BuffText(b));
    }

    void Poison(Unit t, SkillDef s)
    {
        if (t.Passive is { NoPoison: true }) { Status(t, "immune"); return; }
        t.PoisonDmg = s.Poison;
        t.PoisonTurns = s.PoisonTurns;
        Status(t, "poisoned");
    }

    /// <summary>Drags a unit up to n tiles toward another, stopping at anything in the way.</summary>
    void Pull(Unit toward, Unit t, int n)
    {
        if (t.Passive is { Unstoppable: true }) return;
        for (int k = 0; k < n; k++)
        {
            int now = Dist(t, t.Pos, toward);
            if (now <= 1) break;
            Vector2I? best = null;
            foreach (var d in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
            {
                var p = t.Pos + d;
                if (CanStand(t, p) && Dist(t, p, toward) < now) { best = p; break; }
            }
            if (best == null) break;
            t.Pos = best.Value;
            Events.Add(new PushEvent(t, best.Value));
        }
    }

    void Cleanse(Unit t)
    {
        bool any = t.PoisonTurns > 0 || t.Rooted || t.Stunned || t.TauntedBy != null || t.Buffs.Any(b => b.amount < 0);
        t.PoisonTurns = 0;
        t.Rooted = t.Stunned = false;
        t.TauntedBy = null;
        t.Buffs.RemoveAll(b => b.amount < 0);
        if (any) Status(t, "cleansed");
    }

    public void UseSkill(Unit u, SkillDef s, Vector2I tile)
    {
        u.Acted = true;
        u.Cooldowns[s.Id] = Math.Max(1, s.Cooldown - (u.Passive?.CooldownCut ?? 0));
        int range = RangeOf(u, s);
        if (s.Target == TargetKind.Tile)
        {
            // Teleport.
            Events.Add(new SkillEvent(u, s, tile, new List<Vector2I> { u.Pos, tile }));
            Log(u.Name + " uses " + s.Name + ".");
            Events.Add(new MoveEvent(u, new List<Vector2I> { u.Pos, tile }));
            u.Pos = tile;
            return;
        }
        var primary = s.Radius == 0 && s.Target == TargetKind.Enemy ? OccupantOf(tile) : null;
        if (s.Leap && primary != null && LeapSpot(u, primary, range) is { } spot && spot != u.Pos)
        {
            Events.Add(new MoveEvent(u, new List<Vector2I> { u.Pos, spot }));
            u.Pos = spot;
        }
        Events.Add(new SkillEvent(u, s, s.AroundSelf || s.Target == TargetKind.Self ? u.Pos : tile, SkillArea(u, s, tile)));
        Log(u.Name + " uses " + s.Name + ".");

        foreach (var t in SkillTargets(u, s, tile))
        {
            if (t.Gone) continue;
            if (t.Side != u.Side)
            {
                if (Rolls(s))
                {
                    var (dmg, _) = Preview(u, t);
                    if (_rng.Int(100) >= HitOn(u, s, t))
                    {
                        Events.Add(new DamageEvent(t, 0, Miss: true));
                        continue;
                    }
                    if (s.Power > 0 && Dodges(t))
                    {
                        Events.Add(new DamageEvent(t, 0, Miss: true));
                        continue;
                    }
                    if (s.Power > 0)
                    {
                        int dealt = Math.Max(1, Mathf.RoundToInt(dmg * PowerOn(u, s, t) * _rng.Range(0.9f, 1.1f)));
                        Hurt(t, dealt, u);
                        if (s.Drain > 0) HealUnit(u, Mathf.RoundToInt(dealt * s.Drain));
                        if (s.HealOnKill > 0 && t.Gone) HealUnit(u, Mathf.RoundToInt(u.MaxHp * s.HealOnKill));
                        if (u.Side == Side.Wild && u.Attitude == Attitude.Aggressive) u.Weathered = t.Gone ? 0 : u.Weathered + 1;
                        if (t.Gone || t.Side == u.Side) continue; // fled, or recruited by the blow
                    }
                }
                if (s.Poison > 0) Poison(t, s);
                if (s.StunChance > 0 && _rng.Chance(s.StunChance))
                {
                    if (t.Passive is { NoStun: true }) Status(t, "resists"); else { t.Stunned = true; Status(t, "stunned"); }
                }
                if (s.Root)
                {
                    if (t.Passive is { Unstoppable: true }) Status(t, "unstoppable"); else { t.Rooted = true; Status(t, "rooted"); }
                }
                if (s.Taunt) { t.TauntedBy = u; Status(t, "taunted"); }
                if (s.Steal && t.Buffs.Any(b => b.amount > 0))
                {
                    foreach (var b in t.Buffs.Where(b => b.amount > 0).ToList()) { t.Buffs.Remove(b); u.Buffs.Add(b); }
                    Status(u, "stole boosts");
                }
                foreach (var b in s.Buffs) Apply(u, t, b);
                if (s.Pull > 0) Pull(u, t, s.Pull);
                for (int k = 0; k < s.Knock && !t.Gone; k++) Knock(u, t);
                if (s.Power > 0) React(u, t, Dist(u, u.Pos, t) == 1);
            }
            else
            {
                if (s.Cleanse) Cleanse(t);
                if (s.Heal > 0) HealUnit(t, Mathf.RoundToInt(t.MaxHp * s.Heal));
                foreach (var b in s.Buffs) Apply(u, t, b);
                if (s.RandomBuff)
                {
                    var pick = new[] { (StatKind.Atk, 5, 3), (StatKind.Def, 5, 3), (StatKind.Spd, 5, 3), (StatKind.Evade, 25, 3) }[_rng.Int(4)];
                    Apply(u, t, pick);
                }
                if (s.CooldownCut > 0)
                {
                    foreach (var id in t.Cooldowns.Keys.ToList()) t.Cooldowns[id] -= s.CooldownCut;
                    Status(t, "recharged");
                }
                if (s.Poison > 0) Poison(t, s); // a volatile dose: Shimmer Dose
                if (s.Pull > 0 && t != u) Pull(u, t, s.Pull);
            }
        }
        if (s.HitAndRun && u.Moved && !u.Gone)
        {
            u.Moved = false; // hit and run: a second move (no undo past this point)
            u.TurnStart = u.Pos;
        }
    }

    static string BuffText((StatKind stat, int amount, int turns) b) =>
        (b.stat == StatKind.Evade ? "evasion" : b.stat.ToString().ToUpperInvariant()) + (b.amount > 0 ? " +" : " ") + b.amount;

    void Status(Unit u, string text) => Events.Add(new StatusEvent(u, text));

    /// <summary>How much the AI likes a skill aimed at a tile (0 or less: not worth it).</summary>
    float SkillScore(Unit u, SkillDef s, Vector2I tile)
    {
        if (s.Target == TargetKind.Tile) return 0; // the AI doesn't teleport (yet)
        float score = 0;
        foreach (var t in SkillTargets(u, s, tile))
        {
            if (t.Side != u.Side)
            {
                var (dmg, _) = Preview(u, t);
                float p = HitOn(u, s, t) / 100f;
                float d = dmg * PowerOn(u, s, t);
                score += p * (d + (d >= t.Hp ? 15 : 0) + s.Poison * s.PoisonTurns * 0.6f + s.StunChance * 10 + (s.Root ? 3 : 0)
                    + s.Buffs.Length * 3 + d * s.Drain * 0.5f + (s.Steal ? t.Buffs.Count(b => b.amount > 0) * 4 : 0) + s.Pull);
                if (s.Taunt) score += u.Hp > u.MaxHp / 2 ? 4 : -4;
            }
            else
            {
                bool threatened = Alive.Any(o => o.Side != u.Side && Dist(o, o.Pos, t) <= 4);
                if (s.Heal > 0 && t.Hp < t.MaxHp * 0.6f) score += Math.Min(t.MaxHp - t.Hp, t.MaxHp * s.Heal);
                if ((s.Buffs.Length > 0 || s.RandomBuff) && threatened && !t.Buffs.Any(b => s.Buffs.Any(sb => sb.stat == b.stat))) score += 5;
                if (s.Cleanse) score += (t.PoisonTurns > 0 ? 4 : 0) + (t.Rooted ? 3 : 0) + t.Buffs.Count(b => b.amount < 0) * 3;
                if (s.CooldownCut > 0 && t != u && t.Cooldowns.Values.Any(v => v > 1)) score += 4;
                if (s.Poison > 0) score -= 2;
            }
        }
        return score;
    }

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
            if (Map.InBounds(p) && Map.TerrainAt(p) is not (Terrain.Pit or Terrain.Chem or Terrain.Water) && !Map.Blocked(p) && OccupantOf(p) == null
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
        // Effects that last "for N turns" count the creature's own turns.
        for (int i = u.Buffs.Count - 1; i >= 0; i--)
        {
            var b = u.Buffs[i];
            if (b.turns <= 1) u.Buffs.RemoveAt(i); else u.Buffs[i] = (b.stat, b.amount, b.turns - 1);
        }
        u.Rooted = false;
        u.TauntedBy = null;

        if (!u.Gone && u.Attitude == Attitude.Curious && u.Side == Side.Wild)
        {
            // Curious reaction: a wild one grabs any item within reach. (Your own curious creatures
            // leave the berries you set down for the creature they're meant to lure.)
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
    /// <summary>Movement cost from every spot on the map to the nearest spot where the unit could hit a foe.</summary>
    Dictionary<Vector2I, int> ApproachField(Unit u, List<Unit> foes)
    {
        var dist = new Dictionary<Vector2I, int>();
        var open = new List<Vector2I>();
        for (int y = 0; y < Map.H; y++)
        for (int x = 0; x < Map.W; x++)
        {
            var p = new Vector2I(x, y);
            if (CanStand(u, p) && foes.Any(f => InRange(u, p, f))) { dist[p] = 0; open.Add(p); }
        }
        var dirs = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        while (open.Count > 0)
        {
            open.Sort((a, b) => dist[a].CompareTo(dist[b]));
            var cur = open[0];
            open.RemoveAt(0);
            foreach (var d in dirs)
            {
                var from = cur + d;
                if (!u.TilesAt(from).All(Map.InBounds)) continue;
                int step = StepCost(u, from, cur); // the cost of stepping from there to here
                if (step < 0) continue;
                int c = dist[cur] + step;
                if (dist.TryGetValue(from, out int old) && old <= c) continue;
                dist[from] = c;
                open.Add(from);
            }
        }
        return dist;
    }

    // ------------------------------------------------------------------ your side's plans

    /// <summary>The wild creature this battle is about, while it's still out there.</summary>
    Unit Quarry => Target is { Gone: false, Side: Side.Wild } t ? t : null;

    /// <summary>
    /// Whether the player side may hurt this creature. Capturing means not knocking the target
    /// out (it would flee), except for the one champion a proud target has to lose to.
    /// </summary>
    bool MayHurt(Unit u, Unit t) =>
        u.Side != Side.Player || Goal != Objective.Capture || t != Quarry || t.Attitude == Attitude.Proud && Champion() == u;

    /// <summary>The one creature a proud target may fight: whoever has hit it already, or your strongest.</summary>
    Unit Champion() =>
        Quarry == null ? null
        : Quarry.HitBy.Count > 0 ? Quarry.HitBy.First()
        : Alive.Where(o => o.Side == Side.Player).OrderByDescending(o => o.AtkNow).ThenBy(o => Dist(o, o.Pos, Quarry)).FirstOrDefault();

    /// <summary>Whether hitting this creature hurts the hitter: thorns, a counter, a shock, a burst, or an aggressive temper.</summary>
    static bool BitesBack(Unit u) =>
        u.Thorns > 0 || u.Attitude == Attitude.Aggressive
        || u.Reaction?.React is ReactionKind.Counter or ReactionKind.ReturnFire or ReactionKind.Shock or ReactionKind.Burst or ReactionKind.DirtyRetort;

    /// <summary>For a curious target, the creature nearest it carries the glowberry, while one's left and none is down.</summary>
    bool BerryCarrier(Unit u) =>
        u.Side == Side.Player && Goal == Objective.Capture && Quarry is { Attitude: Attitude.Curious } q && Berries > 0 && BerryTiles.Count == 0
        && Alive.Where(o => o.Side == Side.Player).OrderBy(o => Dist(o, o.Pos, q)).First() == u;

    /// <summary>
    /// Where your side's creature should stand for the objective, or null to fight as usual. Hunts
    /// close on the marked alpha; captures play to the target's temper (a champion duels the proud,
    /// the rest ring in the skittish, the berry carrier walks to within luring range); hold-outs
    /// keep together and out of reach.
    /// </summary>
    Vector2I? PlayerPlan(Unit u, List<Vector2I> tiles, List<Unit> foes, Func<Vector2I, bool> canAttack)
    {
        var q = Quarry;
        Vector2I Toward(Unit goal)
        {
            var field = ApproachField(u, new List<Unit> { goal });
            return tiles.OrderBy(p => field.TryGetValue(p, out int d) ? d : 1000 + Dist(u, p, goal)).First();
        }
        if (Goal == Objective.Hunt && q != null)
        {
            var at = tiles.Where(p => InRange(u, p, q)).ToList();
            return at.Count > 0 ? at.OrderByDescending(p => FootHeight(u, p)).ThenBy(p => p == u.Pos ? 0 : 1).First() : Toward(q);
        }
        if (Goal == Objective.Capture && q != null)
        {
            if (q.Attitude == Attitude.Proud && Champion() == u)
            {
                var at = tiles.Where(p => InRange(u, p, q)).ToList();
                return at.Count > 0 ? at.OrderByDescending(p => FootHeight(u, p)).First() : Toward(q);
            }
            if (q.Attitude == Attitude.Skittish)
            {
                // Take a tile beside it; stay put if already there.
                if (Dist(u, u.Pos, q) == 1) return u.Pos;
                var ring = tiles.Where(p => Dist(u, p, q) == 1).ToList();
                return ring.Count > 0 ? ring.First() : Toward(q);
            }
            if (BerryCarrier(u))
            {
                // Close enough that it will come for the berry, not so close it's cornered.
                var near = tiles.Where(p => Dist(u, p, q) >= 2).ToList();
                return near.Count > 0 ? near.OrderBy(p => Dist(u, p, q)).First() : u.Pos;
            }
        }
        if (Goal == Objective.Survive)
        {
            var allies = Alive.Where(o => o != u && o.Side == u.Side).ToList();
            return tiles.OrderByDescending(p => (canAttack(p) ? 4 : 0) + allies.Count(a => Dist(u, p, a) <= 1) * 2
                    - foes.Count(f => Dist(u, p, f) <= f.MoveNow + f.Range) * 3)
                .ThenBy(p => p == u.Pos ? 0 : 1).First();
        }
        return null;
    }

    public void AiTurn(Unit u)
    {
        var foes = Alive.Where(o => o.Side != u.Side).ToList();
        if (foes.Count == 0) { EndTurn(u); return; }
        // A taunted creature has eyes only for its taunter.
        if (u.TauntedBy is { Gone: false } taunter && taunter.Side != u.Side) foes = new List<Unit> { taunter };
        var tiles = Reachable(u).Keys.Append(u.Pos).ToList();
        var mood = u.Side == Side.Player || u.TauntedBy != null ? Attitude.Aggressive : u.Attitude;

        int Nearest(Vector2I p) => foes.Min(f => Dist(u, p, f));
        Unit BestTarget(Vector2I p)
        {
            var inRange = foes.Where(f => InRange(u, p, f) && MayHurt(u, f)).ToList();
            if (inRange.Count == 0) return null;
            if (mood == Attitude.Proud && u.LastAttacker != null && inRange.Contains(u.LastAttacker)) return u.LastAttacker;
            if (u.Side == Side.Player && Goal == Objective.Hunt && Target != null && inRange.Contains(Target)) return Target;
            return inRange.OrderBy(f => f.Hp).First();
        }

        // A skittish creature left without its pack doesn't hang around to be cornered: it bolts.
        if (u.Side == Side.Wild && mood == Attitude.Skittish && !Alive.Any(o => o != u && o.Side == Side.Wild))
        {
            Log($"{u.Name} bolts from the field!");
            u.Gone = true;
            Events.Add(new FaintEvent(u));
            CheckEnd();
            Active = null;
            return;
        }

        Vector2I dest = u.Pos;
        var plan = u.Side == Side.Player ? PlayerPlan(u, tiles, foes, p => BestTarget(p) != null) : null;
        if (plan is { } planned) dest = planned;
        else switch (mood)
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
                    if (attack.Count > 0)
                        dest = attack.OrderBy(p => BestTarget(p).Hp).ThenByDescending(p => FootHeight(u, p)).First();
                    else
                    {
                        // Head for the foe by walking distance, so cliffs and canals are gone around, not stared at.
                        var field = ApproachField(u, foes);
                        dest = tiles.OrderBy(p => field.TryGetValue(p, out int d) ? d : 1000 + Nearest(p)).First();
                    }
                    break;
                }
        }

        if (dest != u.Pos) Move(u, dest);
        // The berry carrier sets one down where the curious target will come for it.
        if (BerryCarrier(u) && BerrySpots(u) is { Count: > 0 } spots)
        {
            OfferBerry(u, spots.OrderBy(p => Math.Abs(p.X - Target.Pos.X) + Math.Abs(p.Y - Target.Pos.Y)).First());
            EndTurn(u);
            return;
        }
        // Skittish creatures only fight back when there is nowhere left to go.
        bool fights = mood != Attitude.Skittish || Cornered(u);
        var target = BestTarget(u.Pos);

        // Weigh each ready skill against a plain attack. Support skills can be used even by the timid.
        float basic = 0;
        if (fights && target != null)
        {
            var (dmg, hit) = Preview(u, target);
            basic = dmg * hit / 100f + (dmg >= target.Hp ? 15 : 0);
        }
        (SkillDef skill, Vector2I tile, float score) best = (null, default, basic);
        foreach (var s in u.Skills.Where(u.Ready))
        {
            bool hostile = s.Target is TargetKind.Enemy or TargetKind.Area;
            if (hostile && !fights) continue;
            foreach (var t in SkillTiles(u, s))
            {
                var hits = hostile ? SkillTargets(u, s, t) : null;
                if (hits != null && hits.Any(x => x.Side != u.Side && !MayHurt(u, x))) continue;
                float score = SkillScore(u, s, t);
                if (u.Side == Side.Player && Goal == Objective.Hunt && hits != null && hits.Contains(Target)) score += 8;
                if (score > best.score + 0.5f) best = (s, t, score);
            }
        }
        if (best.skill != null) UseSkill(u, best.skill, best.tile);
        else if (fights && target != null) Attack(u, target);
        EndTurn(u);
    }
}
