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

    /// <summary>How many beasts can be out and about at once (the plan's six, to start).</summary>
    public const int MostOut = 6;

    /// <summary>A new farmyard's first pieces, given once (old saves get them too).</summary>
    public static void Starter(Ranch r)
    {
        if (!r.Flags.Add(Ranch.DecorFlag)) return;
        foreach (var (id, count) in DecorBook.Starter) r.Shed[id] = r.Shed.GetValueOrDefault(id) + count;
    }

    public static PlacedDecor At(Ranch r, Vector2I t) => r.Decor.FirstOrDefault(d => d.X == t.X && d.Y == t.Y);

    /// <summary>
    /// Beasts out and about (docs/farmyard-plan.md, part 3b): once per save, the ones not in the squad
    /// go out, as many as fit, so the yard has life in it from the first day.
    /// </summary>
    public static void FirstOut(Ranch r)
    {
        if (!r.Flags.Add(Ranch.OutFlag)) return;
        foreach (var rec in r.Creatures)
            if (r.Out.Count < MostOut && !r.Squad.Contains(rec.Id) && !r.Out.Contains(rec.Id)) r.Out.Add(rec.Id);
    }

    /// <summary>Lets a beast out into the yard, or brings it back to the pen. Returns what happened.</summary>
    public static string ToggleOut(Ranch r, int id)
    {
        string name = r.Get(id).Name;
        if (r.Out.Remove(id)) return $"{name} goes back in the pen.";
        if (r.Squad.Contains(id)) return $"{name} is in the squad: the squad waits in the pen between battles.";
        if (r.Out.Count >= MostOut) return $"{MostOut} are out already: bring one back in first.";
        r.Out.Add(id);
        return r.Away.ContainsKey(id) ? $"{name} will be out in the yard when it's back from its dispatch." : $"{name} is out and about in the yard.";
    }

    /// <summary>Why a piece can't go on a tile, or null if it can. <paramref name="you"/> is where you stand.</summary>
    public static string Why(Ranch r, string id, Vector2I t, Vector2I? you = null)
    {
        var def = DecorBook.Get(id);
        if (def == null) return "That isn't something for the yard.";
        var map = HomeBase.Map(r);
        if (!map.InBounds(t)) return "That's outside the yard.";
        if (map.TerrainAt(t) is Terrain.Roof) return "Not on a roof.";
        if (map.TerrainAt(t) is Terrain.Pit or Terrain.Water or Terrain.Chem or Terrain.Lava) return "Not there.";
        if (HomeBase.At(t) is { } station) return $"That's {station.Name}.";
        if (map.Props.Any(p => p.tile == t)) return "Something's already there.";
        if (HomeBase.Pen().Contains(t) || t == PenGap) return "The pens are the beasts': keep them and the way in clear.";
        if (t == HomeBase.Spawn) return "That's where you come home to: keep it clear.";
        if (you == t) return "You're standing there.";
        if (r.Decor.Count >= Most) return $"The yard holds {Most} pieces: put one away first.";
        if (PropArt.Blocks(def.Prop) && CutOff(map, t) is { } cut) return $"That would wall off {cut}.";
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
        if (r.Shed.GetValueOrDefault(id) < 1) return $"There's no {DecorBook.Get(id)?.Name ?? id} left in the shed.";
        if (Why(r, id, t, you) is { } why) return why;
        if (--r.Shed[id] == 0) r.Shed.Remove(id);
        r.Decor.Add(new PlacedDecor { Id = id, X = t.X, Y = t.Y });
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
        if (r.Out.Count > MostOut) r.Out.RemoveRange(MostOut, r.Out.Count - MostOut);
        var all = r.Decor.ToList();
        r.Decor.Clear();
        foreach (var d in all)
        {
            if (DecorBook.Get(d.Id) == null) continue;
            if (Why(r, d.Id, d.Tile) == null) r.Decor.Add(d);
            else r.Shed[d.Id] = r.Shed.GetValueOrDefault(d.Id) + 1;
        }
    }
}
