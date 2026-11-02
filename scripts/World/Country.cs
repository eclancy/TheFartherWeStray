using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;
using CreatureTactics.Party;
using CreatureTactics.Ranching;
using CreatureTactics.Tactics;
using Godot;

namespace CreatureTactics.World;

public enum Side { City, Undercity, Wilds }

/// <summary>A place on the country map: a battle map to fight on (the ranch has none), and which side it's on.</summary>
public sealed class LocationDef
{
    public string Id, Name, MapName, Glyph, Blurb;
    public Side Side;
    public Vector2I At;           // on the painted map, in its pixels
    public string OpenedBy;       // the main mission that opens it; null: open from the start
}

/// <summary>
/// The country (the user's direction, 2026-11-02): the glorious city above, the undercity below,
/// the wilds around them, and your ranch on the outskirts between. Places open one by one as main
/// story missions are won; roads join them, and each road travelled takes a day. Data, like the
/// rest: new places are new entries (and need a map in <see cref="BattleMap"/>).
/// </summary>
public static class Country
{
    public const string Home = "ranch";

    public static readonly LocationDef[] Locations =
    {
        new() { Id = Home, Name = "The Ranch", Glyph = "paw", Side = Side.Wilds, At = new(36, 128),
            Blurb = "Your ranch on the outskirts, between the city and the undercity. Creatures rest and breed here." },
        new() { Id = "gardens", Name = "Gilded Gardens", MapName = "Gilded Gardens", Glyph = "sun", Side = Side.City, At = new(74, 52) },
        new() { Id = "lantern", Name = "Lantern Row", MapName = "Lantern Row", Glyph = "flame", Side = Side.Undercity, At = new(98, 168) },
        new() { Id = "foundry", Name = "Undercity Foundry", MapName = "Undercity Foundry", Glyph = "gear", Side = Side.Undercity, At = new(156, 186), OpenedBy = "main_foundry" },
        new() { Id = "bridges", Name = "Brass Bridges", MapName = "Brass Bridges", Glyph = "crown", Side = Side.City, At = new(140, 40), OpenedBy = "main_bridges" },
        new() { Id = "sump", Name = "Sump Marsh", MapName = "Sump Marsh", Glyph = "flask", Side = Side.Undercity, At = new(216, 204), OpenedBy = "main_sump" },
        new() { Id = "clocktower", Name = "Clocktower Ruins", MapName = "Clocktower Ruins", Glyph = "clock", Side = Side.City, At = new(210, 64), OpenedBy = "main_clocktower" },
        new() { Id = "hollow", Name = "Hexlight Hollow", MapName = "Hexlight Hollow", Glyph = "crystal", Side = Side.Wilds, At = new(244, 130), OpenedBy = "main_hollow" },
    };

    /// <summary>The roads, each a day's travel. The lift joins the Brass Bridges to the Foundry below.</summary>
    public static readonly (string a, string b)[] Roads =
    {
        (Home, "gardens"), (Home, "lantern"), ("gardens", "bridges"), ("bridges", "clocktower"), ("lantern", "foundry"),
        ("foundry", "sump"), ("clocktower", "hollow"), ("sump", "hollow"), ("bridges", "foundry"),
    };

    static readonly Dictionary<string, LocationDef> ById = Locations.ToDictionary(l => l.Id);
    public static LocationDef Get(string id) => id != null && ById.TryGetValue(id, out var l) ? l : null;
    public static LocationDef ByMap(string map) => Locations.FirstOrDefault(l => l.MapName == map);

    public static bool IsOpen(Ranch r, LocationDef l) => l.OpenedBy == null || r.Story.Contains(l.OpenedBy);
    public static IEnumerable<LocationDef> Open(Ranch r) => Locations.Where(l => IsOpen(r, l));

    /// <summary>The way from one place to another along open roads (both ends included), or null.</summary>
    public static List<string> Route(Ranch r, string from, string to)
    {
        var prev = new Dictionary<string, string> { [from] = null };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == to) break;
            foreach (var (a, b) in Roads)
            {
                string next = a == cur ? b : b == cur ? a : null;
                if (next == null || prev.ContainsKey(next) || !IsOpen(r, Get(next))) continue;
                prev[next] = cur;
                queue.Enqueue(next);
            }
        }
        if (!prev.ContainsKey(to)) return null;
        var route = new List<string>();
        for (var at = to; at != null; at = prev[at]) route.Add(at);
        route.Reverse();
        return route;
    }

    /// <summary>Days of travel from where you are.</summary>
    public static int Days(Ranch r, string to) => Math.Max(0, (Route(r, r.Location, to)?.Count ?? 1) - 1);

    /// <summary>
    /// What's on offer everywhere: one or two contracts at each open place with a map, a rival's duel
    /// (on their home ground if it's open), and the main missions whose milestones are met.
    /// </summary>
    public static List<Mission> Board(Ranch r)
    {
        ulong enc = r.Encounter;
        var list = new List<Mission>();
        var sighted = new HashSet<string>(); // no two contracts promise the same part
        foreach (var l in Open(r).Where(l => l.MapName != null))
        {
            ulong place = (ulong)Array.IndexOf(Locations, l);
            int count = new SeededRng(SeededRng.Derive(enc * 977UL + 0x10C, place)).Chance(0.5f) ? 2 : 1;
            for (int k = 0; k < count; k++)
            {
                ulong seed = SeededRng.Derive(enc * 977UL + place * 31, (ulong)k + 1);
                var m = Mission.Contract(seed, new SeededRng(seed), l.MapName, enc, r.Codex, sighted);
                m.Location = l.Id;
                list.Add(m);
            }
        }
        if (RivalBook.Challenger(enc) is { } rival)
        {
            var duel = Mission.Duel(rival, enc, r.Rivals.GetValueOrDefault(rival.Id), SeededRng.Derive(enc * 977UL, 0xD0E1));
            var at = rival.Turf.Select(ByMap).FirstOrDefault(l => l != null && IsOpen(r, l))
                ?? Open(r).Where(l => l.MapName != null).OrderBy(l => Days(r, l.Id)).First();
            duel.MapName = at.MapName;
            duel.Location = at.Id;
            list.Add(duel);
        }
        list.AddRange(StoryBook.Ready(r).Select(StoryBook.ToMission));
        return list;
    }

    public static List<Mission> At(Ranch r, string location) => Board(r).Where(m => m.Location == location).ToList();
}
