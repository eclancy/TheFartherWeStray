using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.World;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>A piece put out in the farmyard: which one, and on what tile.</summary>
public sealed class PlacedDecor
{
    public string Id;
    public int X, Y;
    [JsonIgnore] public Vector2I Tile => new(X, Y);
}

/// <summary>A machine out in the farmyard: which, where, its level, and what's in its hopper (and part-made) for each thing it makes.</summary>
public sealed class PlacedMachine
{
    public string Id;
    public int X, Y;
    public int Level = 1;
    public List<int> Stored = new();
    public List<float> Part = new();
    [JsonIgnore] public Vector2I Tile => new(X, Y);
}

/// <summary>
/// Decorating the farmyard (the user's ask, 2026-11-03; docs/farmyard-plan.md, part 1). Pieces live in
/// the shed until you put them out at the workbench, on any free tile (the user's pick), so long as
/// every station, the gate, the way into the pens and the spot you come home to can still be reached.
/// The rules are here, away from the screen, so the headless check (--basetest) tests the real thing.
/// </summary>
public static class Farmyard
{
    /// <summary>The way into the pens: the gap in their fence, kept clear.</summary>
    public static readonly Vector2I PenGap = new(5, 11);

    /// <summary>How many pieces the yard holds: room to make it yours, and the board stays quick on a phone.</summary>
    public const int Most = 60;

    /// <summary>How many beasts can be out and about at once to start: the pens' upgrades make room for more.</summary>
    public const int MostOut = 6;

    // ------------------------------------------------------------------ station levels (part 2)

    public static int Level(Ranch r, string station) => r.Levels.GetValueOrDefault(station, 1);

    /// <summary>Beasts that can be out in the yard: 6, 9 or 12 by the pens' level.</summary>
    public static int OutCap(Ranch r) => MostOut + 3 * (Level(r, "pens") - 1);

    /// <summary>The share of the squad's JP the beasts at home earn each battle, in percent: the training yard's.</summary>
    public static int HomeShare(Ranch r) => (Level(r, "yard") - 1) * 25;

    /// <summary>Glowberries every battle brings: 3, 4 or 5 by the armory's level.</summary>
    public static int BattleBerries(Ranch r) => 2 + Level(r, "armory");

    /// <summary>A dispatch's pay with the war table's level: a quarter more from level 2.</summary>
    public static int DispatchPay(Ranch r, int reward) => Level(r, "war") >= 2 ? reward * 5 / 4 : reward;

    /// <summary>A dispatch's days with the war table's level: a day sooner at level 3, never under one.</summary>
    public static int DispatchDays(Ranch r, int days) => Level(r, "war") >= 3 ? System.Math.Max(1, days - 1) : days;

    /// <summary>Carts at the gate: how many of a trip's first roads take no day.</summary>
    public static int Carts(Ranch r) => Level(r, "gate") - 1;

    /// <summary>A lab price with the lab's level: 10% off at level 2, 20% at level 3.</summary>
    public static int LabPrice(Ranch r, int cost) => cost * (100 - 10 * (Level(r, "lab") - 1)) / 100;

    /// <summary>Raises a station a level for chem and scrap, built at once (the user's call). Returns what happened.</summary>
    public static string Upgrade(Ranch r, StationDef s)
    {
        if (s.Upgrades == null) return $"{s.Name} is as it is: there's nothing to build on.";
        if (!s.Built(r)) return $"{s.Name} isn't built yet: it opens after {s.Needs}.";
        int level = Level(r, s.Id);
        if (level >= 3) return $"{s.Name} is as good as it gets.";
        var (chem, scrap) = HomeBase.UpgradeCosts[level - 1];
        if (r.Chem < chem || r.Scrap < scrap) return $"Level {level + 1} takes {chem} chem and {scrap} scrap: you have {r.Chem} and {r.Scrap}.";
        r.Chem -= chem;
        r.Scrap -= scrap;
        r.Levels[s.Id] = level + 1;
        return $"{s.Name} is level {level + 1}: {s.Upgrades[level - 1]}";
    }

    /// <summary>A new farmyard's first pieces, given once (old saves get them too).</summary>
    public static void Starter(Ranch r)
    {
        if (!r.Flags.Add(Ranch.DecorFlag)) return;
        foreach (var (id, count) in DecorBook.Starter) r.Shed[id] = r.Shed.GetValueOrDefault(id) + count;
    }

    public static PlacedDecor At(Ranch r, Vector2I t) => r.Decor.FirstOrDefault(d => d.X == t.X && d.Y == t.Y);

    /// <summary>The prop a piece or a machine stands as, or null if the id is neither.</summary>
    public static Prop? PropOf(string id) => DecorBook.Get(id)?.Prop ?? MachineBook.Get(id)?.Prop;

    public static string NameOf(string id) => DecorBook.Get(id)?.Name ?? MachineBook.Get(id)?.Name ?? id;

    /// <summary>
    /// Beasts out and about (docs/farmyard-plan.md, part 3b): once per save, the ones not in the squad
    /// go out, as many as fit, so the yard has life in it from the first day.
    /// </summary>
    public static void FirstOut(Ranch r)
    {
        if (!r.Flags.Add(Ranch.OutFlag)) return;
        foreach (var rec in r.Creatures)
            if (r.Out.Count < OutCap(r) && !r.Squad.Contains(rec.Id) && !r.Out.Contains(rec.Id)) r.Out.Add(rec.Id);
    }

    /// <summary>Lets a beast out into the yard, or brings it back to the pen. Returns what happened.</summary>
    public static string ToggleOut(Ranch r, int id)
    {
        string name = r.Get(id).Name;
        if (r.Out.Remove(id)) return $"{name} goes back in the pen.";
        if (r.Squad.Contains(id)) return $"{name} is in the squad: the squad waits in the pen between battles.";
        if (r.Out.Count >= OutCap(r)) return $"{OutCap(r)} are out already: bring one back in first{(Level(r, "pens") < 3 ? ", or build up the pens" : "")}.";
        r.Out.Add(id);
        return r.Away.ContainsKey(id) ? $"{name} will be out in the yard when it's back from its dispatch." : $"{name} is out and about in the yard.";
    }

    /// <summary>Why a piece can't go on a tile, or null if it can. <paramref name="you"/> is where you stand.</summary>
    public static string Why(Ranch r, string id, Vector2I t, Vector2I? you = null)
    {
        if (PropOf(id) is not { } prop) return "That isn't something for the yard.";
        var map = HomeBase.Map(r);
        if (!map.InBounds(t)) return "That's outside the yard.";
        if (map.TerrainAt(t) is Terrain.Roof) return "Not on a roof.";
        if (map.TerrainAt(t) is Terrain.Pit or Terrain.Water or Terrain.Chem or Terrain.Lava) return "Not there.";
        if (HomeBase.At(t) is { } station) return $"That's {station.Name}.";
        if (map.Props.Any(p => p.tile == t)) return "Something's already there.";
        if (HomeBase.Pen().Contains(t) || t == PenGap) return "The pens are the beasts': keep them and the way in clear.";
        if (t == HomeBase.Spawn) return "That's where you come home to: keep it clear.";
        if (you == t) return "You're standing there.";
        if (DecorBook.Get(id) != null && r.Decor.Count >= Most) return $"The yard holds {Most} pieces: put one away first.";
        if (PropArt.Blocks(prop) && CutOff(map, t) is { } cut) return $"That would wall off {cut}.";
        return null;
    }

    /// <summary>
    /// What a blocking piece on this tile would shut you out of, or null: everywhere you can walk from
    /// the spot you come home to, then a station with no way to stand beside it, or the pens.
    /// </summary>
    static string CutOff(BattleMap map, Vector2I blocked)
    {
        bool Open(Vector2I t) => t != blocked && map.InBounds(t) && !map.Blocked(t)
            && map.TerrainAt(t) is not (Terrain.Pit or Terrain.Water or Terrain.Chem or Terrain.Lava or Terrain.Roof);
        var reached = new HashSet<Vector2I> { HomeBase.Spawn };
        var todo = new Queue<Vector2I>(reached);
        var steps = new[] { Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up };
        while (todo.Count > 0)
        {
            var at = todo.Dequeue();
            foreach (var d in steps)
            {
                var next = at + d;
                if (reached.Contains(next) || !Open(next) || Mathf.Abs(map.HeightAt(next) - map.HeightAt(at)) > 2) continue;
                reached.Add(next);
                todo.Enqueue(next);
            }
        }
        foreach (var s in HomeBase.Stations)
            if (!HomeBase.Tiles(s).Any(t => steps.Any(d => reached.Contains(t + d)))) return s.Name;
        return reached.Contains(PenGap) ? null : "the pens";
    }

    /// <summary>Puts a piece from the shed out on a tile. Returns why not, or null when it's out.</summary>
    public static string Place(Ranch r, string id, Vector2I t, Vector2I? you = null)
    {
        if (r.Shed.GetValueOrDefault(id) < 1) return $"There's no {NameOf(id)} left in the shed.";
        if (Why(r, id, t, you) is { } why) return why;
        if (--r.Shed[id] == 0) r.Shed.Remove(id);
        if (MachineBook.Get(id) is { } machine)
        {
            var m = new PlacedMachine { Id = id, X = t.X, Y = t.Y };
            Fit(m, machine);
            r.Machines.Add(m);
        }
        else r.Decor.Add(new PlacedDecor { Id = id, X = t.X, Y = t.Y });
        return null;
    }

    /// <summary>Takes the piece on a tile back into the shed. Returns its id, or null if there's none there.</summary>
    public static string PickUp(Ranch r, Vector2I t)
    {
        if (At(r, t) is not { } piece) return null;
        r.Decor.Remove(piece);
        r.Shed[piece.Id] = r.Shed.GetValueOrDefault(piece.Id) + 1;
        return piece.Id;
    }

    /// <summary>
    /// Keeps a loaded yard sound: a piece that's unknown now, or on a tile the base has since claimed
    /// (a new station, say), goes back in the shed; the beasts out are ones you have, none in the squad.
    /// </summary>
    public static void Tidy(Ranch r)
    {
        r.Out.RemoveAll(id => r.Squad.Contains(id) || r.Creatures.All(c => c.Id != id));
        if (r.Out.Count > OutCap(r)) r.Out.RemoveRange(OutCap(r), r.Out.Count - OutCap(r));
        // Station levels that make sense: known stations with levels, 1 to 3.
        foreach (var id in r.Levels.Keys.ToList())
            if (HomeBase.Get(id)?.Upgrades == null) r.Levels.Remove(id); else r.Levels[id] = Math.Clamp(r.Levels[id], 1, 3);
        var all = r.Decor.ToList();
        r.Decor.Clear();
        foreach (var d in all)
        {
            if (DecorBook.Get(d.Id) == null) continue;
            if (Why(r, d.Id, d.Tile) == null) r.Decor.Add(d);
            else r.Shed[d.Id] = r.Shed.GetValueOrDefault(d.Id) + 1;
        }
        // Machines: one of each, known ones, at a level that exists, with a hopper slot for everything they make.
        var machines = r.Machines.ToList();
        r.Machines.Clear();
        foreach (var m in machines)
        {
            if (MachineBook.Get(m.Id) is not { } def || r.Machines.Any(o => o.Id == m.Id)) continue;
            m.Level = Math.Clamp(m.Level, 1, MachineBook.Levels);
            Fit(m, def);
            if (Why(r, m.Id, m.Tile) == null) r.Machines.Add(m);
            else r.Shed[m.Id] = r.Shed.GetValueOrDefault(m.Id) + 1;
        }
    }

    // ------------------------------------------------------------------ machines (part 3)

    /// <summary>A hopper slot, and its part-made share, for each thing the machine makes.</summary>
    static void Fit(PlacedMachine m, MachineDef def)
    {
        m.Stored ??= new();
        m.Part ??= new();
        while (m.Stored.Count < def.Makes.Length) m.Stored.Add(0);
        while (m.Part.Count < def.Makes.Length) m.Part.Add(0);
    }

    public static PlacedMachine MachineAt(Ranch r, Vector2I t) => r.Machines.FirstOrDefault(m => m.X == t.X && m.Y == t.Y);

    /// <summary>Whether the yard has this machine already, out or in the shed: one of each.</summary>
    public static bool Owns(Ranch r, string id) => r.Shed.ContainsKey(id) || r.Machines.Any(m => m.Id == id);

    /// <summary>Moves a machine (its level and hopper go with it). Returns why not, or null when it's moved.</summary>
    public static string Move(Ranch r, PlacedMachine m, Vector2I t, Vector2I? you = null)
    {
        r.Machines.Remove(m);
        string why = Why(r, m.Id, t, you);
        if (why == null) { m.X = t.X; m.Y = t.Y; }
        r.Machines.Add(m);
        return why;
    }

    /// <summary>
    /// A day goes by (a battle, or a road travelled): every machine adds its day's share to its hopper.
    /// Shares under one build up over days; a full hopper stops filling until it's emptied.
    /// </summary>
    public static void PassDay(Ranch r)
    {
        foreach (var m in r.Machines)
        {
            if (MachineBook.Get(m.Id) is not { } def) continue;
            Fit(m, def);
            for (int i = 0; i < def.Makes.Length; i++)
            {
                float rate = def.Makes[i].PerDay[m.Level - 1];
                int cap = def.Makes[i].Holds[m.Level - 1];
                if (rate <= 0 || m.Stored[i] >= cap) { m.Part[i] = 0; continue; }
                m.Part[i] += rate;
                while (m.Part[i] >= 0.999f && m.Stored[i] < cap) { m.Stored[i]++; m.Part[i] -= 1; }
                if (m.Stored[i] >= cap || m.Part[i] < 0) m.Part[i] = 0;
            }
        }
    }

    /// <summary>Empties a machine's hopper into the ranch: chem, scrap, or into the bag. Returns what came out.</summary>
    public static string Collect(Ranch r, PlacedMachine m)
    {
        var def = MachineBook.Get(m.Id);
        Fit(m, def);
        var got = new List<string>();
        for (int i = 0; i < def.Makes.Length; i++)
        {
            int n = m.Stored[i];
            if (n == 0) continue;
            string thing = def.Makes[i].Thing;
            if (thing == MachineBook.Chem) r.Chem += n;
            else if (thing == MachineBook.Scrap) r.Scrap += n;
            else r.Items[thing] = r.Items.GetValueOrDefault(thing) + n;
            got.Add(MachineBook.Words(thing, n));
            m.Stored[i] = 0;
        }
        return got.Count == 0 ? $"The {def.Name}'s hopper is empty: it fills a little each day (a battle, or a road travelled)."
            : $"The {def.Name} gives up {string.Join(" and ", got)}.";
    }

    /// <summary>Raises a machine a level for chem and scrap. Returns what happened.</summary>
    public static string Upgrade(Ranch r, PlacedMachine m)
    {
        var def = MachineBook.Get(m.Id);
        if (m.Level >= MachineBook.Levels) return $"The {def.Name} is as good as it gets.";
        var (chem, scrap) = def.Upgrades[m.Level - 1];
        if (r.Chem < chem || r.Scrap < scrap) return $"Level {m.Level + 1} takes {chem} chem and {scrap} scrap: you have {r.Chem} and {r.Scrap}.";
        r.Chem -= chem;
        r.Scrap -= scrap;
        m.Level++;
        return $"The {def.Name} is level {m.Level}: {MachineBook.Rate(def, m.Level)}.";
    }
}
