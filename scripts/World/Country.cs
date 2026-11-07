using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Party;
using TheFartherWeStray.Ranching;
using TheFartherWeStray.Tactics;
using Godot;

namespace TheFartherWeStray.World;

public enum Side { City, Undercity, Wilds }

/// <summary>A place on the country map: a battle map to fight on (home has none), and which side it's on.</summary>
public sealed class LocationDef
{
    public string Id, MapName, Glyph, Blurb;
    string _name;
    /// <summary>Its name on screen. Home is called whatever the player calls it (<see cref="Country.HomeName"/>).</summary>
    public string Name { get => Id == Country.Home ? Country.HomeName : _name; init => _name = value; }
    public Side Side;
    public Vector2I At;           // on the painted map, in its pixels
    public string OpenedBy;       // the main mission that opens it; null: open from the start
    // Its regular job, always on offer (the user's direction: repeatable missions): a name, the ask, the objective.
    public string Regular, RegularAsk;
    public Objective RegularGoal = Objective.Rout;
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

    /// <summary>
    /// What the player calls home (the user's call, 2026-11-07): "Home" until they rename it. It's the save in play's
    /// (set where a ranch is made or loaded, and on renaming); the title, the Back buttons and the map read it.
    /// </summary>
    public static string HomeName = DefaultHome;
    public const string DefaultHome = "Home";

    /// <summary>The way back, in words: "Back home", or "Back to Thornfield" once it's renamed.</summary>
    public static string BackHome => HomeName == DefaultHome ? "Back home" : $"Back to {HomeName}";
    public static string BackHomeLow => HomeName == DefaultHome ? "back home" : $"back to {HomeName}";

    public static readonly LocationDef[] Locations =
    {
        new() { Id = Home, Glyph = "paw", Side = Side.Wilds, At = new(36, 128),
            Blurb = "Your home on the outskirts, between the city and the undercity. Your beasts rest, breed and train here." },
        new() { Id = "gardens", Name = "Gilded Gardens", MapName = "Gilded Gardens", Glyph = "sun", Side = Side.City, At = new(78, 52),
            Regular = "Weed Whacking", RegularAsk = "Pests are at the prize roses again. Shoo them off." },
        new() { Id = "lantern", Name = "Lantern Row", MapName = "Lantern Row", Glyph = "flame", Side = Side.Undercity, At = new(98, 168),
            Regular = "Night Market Watch", RegularAsk = "Keep the stalls standing till dawn.", RegularGoal = Objective.Survive },
        new() { Id = "foundry", Name = "Undercity Foundry", MapName = "Undercity Foundry", Glyph = "gear", Side = Side.Undercity, At = new(156, 186), OpenedBy = "main_foundry",
            Regular = "Rust Busters", RegularAsk = "Something keeps nesting in the furnaces. Clear it out." },
        new() { Id = "bridges", Name = "Brass Bridges", MapName = "Brass Bridges", Glyph = "crown", Side = Side.City, At = new(140, 40), OpenedBy = "main_bridges",
            Regular = "Toll Troll Patrol", RegularAsk = "Something big keeps charging a toll on the bridges.", RegularGoal = Objective.Hunt },
        new() { Id = "coliseum", Name = "The Grand Coliseum", MapName = "The Grand Coliseum", Glyph = "star", Side = Side.City, At = new(112, 78), OpenedBy = "main_bridges",
            Blurb = "The city's arena. Cups of three duels in a row; win all three to take one.",
            Regular = "Crowd Pleaser", RegularAsk = "The crowd paid for monsters. Give them a show." },
        new() { Id = "sump", Name = "Glowmire", MapName = "Glowmire", Glyph = "flask", Side = Side.Undercity, At = new(216, 204), OpenedBy = "main_sump",
            Regular = "Sludge Duty", RegularAsk = "The chem pools are crawling again. Thin them out." },
        new() { Id = "clocktower", Name = "Clocktower Ruins", MapName = "Clocktower Ruins", Glyph = "clock", Side = Side.City, At = new(196, 64), OpenedBy = "main_clocktower",
            Regular = "Bats in the Belfry", RegularAsk = "The bells won't ring with all that up there." },
        new() { Id = "hollow", Name = "Hexlight Hollow", MapName = "Hexlight Hollow", Glyph = "crystal", Side = Side.Wilds, At = new(244, 130), OpenedBy = "main_hollow",
            Regular = "Crystal Clear-out", RegularAsk = "A big one keeps eating the miners' crystals.", RegularGoal = Objective.Hunt },
        new() { Id = "coast", Name = "Saltwind Coast", MapName = "Saltwind Coast", Glyph = "wave", Side = Side.Wilds, At = new(16, 176), OpenedBy = "main_coast",
            Regular = "Beach Sweep", RegularAsk = "Tidy the beach before the tourists see what washed up." },
        new() { Id = "dunes", Name = "Sunscorch Dunes", MapName = "Sunscorch Dunes", Glyph = "sun", Side = Side.Wilds, At = new(54, 212), OpenedBy = "main_dunes",
            Regular = "Sand Trap", RegularAsk = "Guard the caravan's camp through a long hot night.", RegularGoal = Objective.Survive },
        new() { Id = "frost", Name = "Frostfang Pass", MapName = "Frostfang Pass", Glyph = "crystal", Side = Side.Wilds, At = new(30, 40), OpenedBy = "main_frost",
            Regular = "Snow Day", RegularAsk = "Clear the pass so the post can get through." },
        new() { Id = "cinder", Name = "Cinder Rift", MapName = "Cinder Rift", Glyph = "flame", Side = Side.Wilds, At = new(242, 34), OpenedBy = "main_cinder",
            Regular = "Hot Potato", RegularAsk = "Something fiery keeps raiding the prospectors' camp.", RegularGoal = Objective.Hunt },
    };

    /// <summary>The roads, each a day's travel. The lift joins the Brass Bridges to the Foundry below.</summary>
    public static readonly (string a, string b)[] Roads =
    {
        (Home, "gardens"), (Home, "lantern"), ("gardens", "bridges"), ("bridges", "clocktower"), ("lantern", "foundry"),
        ("foundry", "sump"), ("clocktower", "hollow"), ("sump", "hollow"), ("bridges", "foundry"),
        ("gardens", "coliseum"), ("bridges", "coliseum"), (Home, "coast"), ("lantern", "coast"), ("coast", "dunes"), ("dunes", "lantern"),
        ("gardens", "frost"), (Home, "frost"), ("clocktower", "cinder"), ("hollow", "cinder"),
    };

    static readonly Dictionary<string, LocationDef> ById = Locations.ToDictionary(l => l.Id);
    public static LocationDef Get(string id) => id != null && ById.TryGetValue(id, out var l) ? l : null;
    public static LocationDef ByMap(string map) => Locations.FirstOrDefault(l => l.MapName == map);

    public static bool IsOpen(Ranch r, LocationDef l) => l.OpenedBy == null || r.Story.Contains(l.OpenedBy);
    public static IEnumerable<LocationDef> Open(Ranch r) => Locations.Where(l => IsOpen(r, l));

    /// <summary>
    /// The two boards (the user's direction, 2026-11-03): the undercity's work and the city above's
    /// are separate boards, and the city's is shut to you until someone with connections up there
    /// brings you in. In the story that's the friend from your past, after the Lantern Row job
    /// (docs/story-notes.md, act one); until her scene exists, winning that job opens it. The
    /// undercity's board and work in the wilds are yours from the start.
    /// </summary>
    public const string CityBoardOpenedBy = "main_foundry";

    public static bool CityBoardOpen(Ranch r) => r.Story.Contains(CityBoardOpenedBy);

    /// <summary>Whether a place gives you work: it's open, and its board is open to you.</summary>
    public static bool Hires(Ranch r, LocationDef l) => IsOpen(r, l) && (l.Side != Side.City || CityBoardOpen(r));

    /// <summary>Whose board a place's work is on, as the map names it.</summary>
    public static string BoardOf(Side side) => side switch { Side.City => "the city's board", Side.Undercity => "the undercity's board", _ => "work in the wilds" };

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
    /// <summary>Days to get somewhere: a road each, less the gate's carts (the first roads of a trip take none).</summary>
    public static int Days(Ranch r, string to) => Math.Max(0, (Route(r, r.Location, to)?.Count ?? 1) - 1 - Ranching.Farmyard.Carts(r));

    /// <summary>
    /// What's on offer everywhere: at each open place with a map, its regular job, one or two
    /// contracts and sometimes a dispatch; a rival's duel (on their home ground if it's open); the
    /// coliseum's cups; the main missions whose milestones are met; and the roaming packs.
    /// </summary>
    public static List<Mission> Board(Ranch r)
    {
        ulong enc = r.Encounter;
        var list = new List<Mission>();
        var sighted = new HashSet<string>(); // no two contracts promise the same part
        var underway = r.Away.Values.Select(a => a.Job).ToHashSet();
        var open = Open(r).Where(l => l.MapName != null && Hires(r, l)).ToList();
        foreach (var l in open)
        {
            ulong place = (ulong)Array.IndexOf(Locations, l);
            if (l.Regular != null) list.Add(Mission.Regular(SeededRng.Derive(enc * 977UL + place * 31, 0x5E6), l));
            if (l.Id == "coliseum") { list.AddRange(CupBook.Ready(r).Select(c => CupBook.Round(r, c, 1))); continue; }
            var pick = new SeededRng(SeededRng.Derive(enc * 977UL + 0x10C, place));
            int count = pick.Chance(0.5f) ? 2 : 1;
            for (int k = 0; k < count; k++)
            {
                ulong seed = SeededRng.Derive(enc * 977UL + place * 31, (ulong)k + 1);
                var m = Mission.Contract(seed, new SeededRng(seed), l.MapName, enc, r.Codex, sighted);
                m.Location = l.Id;
                list.Add(m);
            }
            // A dispatch, some days: one of this place's own jobs, or one that could be anywhere.
            var jobs = DispatchBook.All.Where(d => (d.Location == l.Id || d.Location == null && (int)(SeededRng.Hash(d.Id) % (ulong)open.Count) == open.IndexOf(l))
                && !underway.Contains(d.Id)).ToList();
            if (jobs.Count > 0 && pick.Chance(0.6f)) list.Add(Mission.Dispatching(jobs[pick.Int(jobs.Count)], l));
        }
        if (RivalBook.Challenger(enc) is { } rival)
        {
            var duel = Mission.Duel(rival, enc, r.Rivals.GetValueOrDefault(rival.Id), SeededRng.Derive(enc * 977UL, 0xD0E1), r.RivalLosses.GetValueOrDefault(rival.Id));
            var at = rival.Turf.Select(ByMap).FirstOrDefault(l => l != null && Hires(r, l))
                ?? open.OrderBy(l => Days(r, l.Id)).First();
            duel.MapName = at.MapName;
            duel.Location = at.Id;
            list.Add(duel);
        }
        list.AddRange(StoryBook.Ready(r).Select(StoryBook.ToMission));
        list.AddRange(StoryBook.Heard(r).Select(StoryBook.RumourMission));
        list.AddRange(TrialBook.Open(r).Select(TrialBook.ToMission));
        list.RemoveAll(m => Get(m.Location) is { } here && !Hires(r, here)); // nothing from a board that's shut to you
        list.AddRange(r.Wanderers.Where(w => Roaming.Roams(r, w.At)).Select(Roaming.Battle));
        return list;
    }

    public static List<Mission> At(Ranch r, string location) => Board(r).Where(m => m.Location == location).ToList();
}
