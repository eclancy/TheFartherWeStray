using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using CreatureTactics.Ranching;
using CreatureTactics.Tactics;

namespace CreatureTactics.World;

/// <summary>A wild pack roaming the country (saved): where it is, and the map whose fauna it is.</summary>
public sealed class Wanderer
{
    public int Id;
    public string At, Home;
    public ulong Seed;
    public int Size;
}

/// <summary>
/// Wandering encounters (the user's direction, 2026-11-02, after FFTA's): wild packs turn up out in
/// the country and move along the roads, a day at a time. Arrive where one is and it attacks before
/// anything else; or go after one on purpose. Beaten, it's gone (and its creatures can be recruited,
/// as in any rout). Everything rolls from the day, so a replayed run meets the same packs.
/// </summary>
public static class Roaming
{
    public const int MaxPacks = 3;

    /// <summary>Where packs roam: open places with a map, but never the coliseum (or the ranch, which has none).</summary>
    public static bool Roams(Ranch r, string id) => Country.Get(id) is { MapName: not null } l && l.Id != "coliseum" && Country.IsOpen(r, l);

    /// <summary>A day passes: each pack moves on two days in three, and a new one turns up now and then.</summary>
    public static void PassDay(Ranch r)
    {
        var rng = new SeededRng(SeededRng.Derive(0x60A3, (ulong)r.Day));
        foreach (var w in r.Wanderers)
        {
            if (!rng.Chance(0.66f)) continue;
            var next = Country.Roads.Select(road => road.a == w.At ? road.b : road.b == w.At ? road.a : null)
                .Where(id => id != null && Roams(r, id)).ToList();
            if (next.Count > 0) w.At = next[rng.Int(next.Count)];
        }
        if (r.Wanderers.Count < MaxPacks && rng.Chance(0.35f)) Spawn(r, rng);
    }

    /// <summary>A new pack at some open place other than where you are.</summary>
    public static void Spawn(Ranch r, SeededRng rng)
    {
        var places = Country.Locations.Where(l => Roams(r, l.Id) && l.Id != r.Location).ToList();
        if (places.Count == 0) return;
        var at = places[rng.Int(places.Count)];
        r.Wanderers.Add(new Wanderer
        {
            Id = r.NextWanderer, At = at.Id, Home = at.MapName, Size = rng.Int(3, 5),
            Seed = SeededRng.Derive(0x60A3 ^ (ulong)r.Day, (ulong)r.NextWanderer),
        });
        r.NextWanderer++;
    }

    /// <summary>The fight with a pack, on the map of wherever it is now; its creatures come from its home.</summary>
    public static Mission Battle(Wanderer w)
    {
        var at = Country.Get(w.At);
        var m = new Mission
        {
            Seed = w.Seed, MapName = at.MapName, FaunaMap = w.Home, Location = at.Id, Goal = Objective.Rout, Pack = w.Size, Roamer = w.Id,
            Stars = System.Math.Clamp(w.Size - 2, 1, 5), Reward = 10 + w.Size * 3,
        };
        var (pack, _) = m.Wild();
        m.Title = $"Roaming: {pack[0].Species.Name}{(pack.Count > 1 ? $" and {pack.Count - 1} more" : "")}";
        return m;
    }
}
