using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.World;
using Godot;

namespace TheFartherWeStray.Ranching;

/// <summary>A place at the home base where one of the ranch's systems lives, built as the campaign goes on.</summary>
public sealed class StationDef
{
    public string Id, Name, Glyph, Blurb;
    public char Prop;                 // its prop on the base map (every tile with this prop is the station)
    public Func<Ranch, bool> Built;   // whether it's open yet
    public string Needs;              // what opens it, said to the player while it's scaffolding
}

/// <summary>
/// The home base (the user's direction, 2026-11-02): every menu and system grounded in a place you
/// walk to. The pens hold the roster and the squad, the vats breed, the lab grafts and infuses, the
/// training yard trains classes, the armory fits gear, the war table leads the party, the gate goes
/// out to the country. Stations open as the campaign goes on; until then their plot stands in
/// scaffolding with a sign. Unlocks are worked out from progress, so the save needs nothing new.
/// </summary>
public static class HomeBase
{
    public static readonly StationDef[] Stations =
    {
        new() { Id = "gate", Name = "The Gate", Glyph = "mountain", Prop = 'O', Built = _ => true,
            Blurb = "out to the country: contracts, the story, the shops" },
        new() { Id = "pens", Name = "The Pens", Glyph = "paw", Prop = 'q', Built = _ => true,
            Blurb = "your beasts: pick the squad, name them, let them go" },
        new() { Id = "yard", Name = "Training Yard", Glyph = "fist", Prop = 'd', Built = _ => true,
            Blurb = "classes and the job tree" },
        // The farmyard plan's first part (docs/farmyard-plan.md): decorating happens here, on the base itself.
        new() { Id = "bench", Name = "The Workbench", Glyph = "hand", Prop = 'j', Built = _ => true,
            Blurb = "put things out in the yard, and move them about" },
        new() { Id = "armory", Name = "The Armory", Glyph = "club", Prop = 'R', Built = r => r.ContractsWon >= 1,
            Blurb = "fit the gear you've bought", Needs = "your first contract" },
        new() { Id = "vats", Name = "Breeding Vats", Glyph = "sparkle", Prop = 'V', Built = r => r.ContractsWon >= 2,
            Blurb = "breed two beasts into a new one", Needs = "two contracts won" },
        new() { Id = "war", Name = "War Table", Glyph = "crown", Prop = 'W', Built = r => Party.TamerBook.Joined(r.Encounter).Count() >= 2,
            Blurb = "your tamers and who each leads", Needs = "a second tamer joining you" },
        new() { Id = "lab", Name = "The Lab", Glyph = "flask", Prop = 'Q', Built = r => r.Story.Contains("main_foundry"),
            Blurb = "infuse, splice, fit augments, graft parts", Needs = "the road to the Foundry" },
    };

    public static StationDef Get(string id) => Stations.FirstOrDefault(s => s.Id == id);

    // The base: a farmhouse with the war table on its porch and the workbench by its wall, the lab's
    // workshop, a vat shed, the training yard's dummies, the pens behind their fence, the armory's
    // rack, and the gate south.
    static readonly string[] Terrain =
    {
        "hhhh........hhhh",
        "hhhh........hhhh",
        ",,,,...--...,,,,",
        ",,,,...--...,,,,",
        "..,,...--...,,..",
        ".......--..-----",
        "ww.....,,,,-----",
        "ww----,,,,,-----",
        "ww.....,,,,-----",
        ".......,,,,.....",
        "......,--,......",
        "......,--,......",
        "......,--,......",
        "......,--,......",
    };
    static readonly string[] Heights =
    {
        "2222000000002222",
        "2222000000002222",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
        "0000000000000000",
    };
    static readonly string[] Props =
    {
        "................",
        "....j...........",
        "..W.........Q...",
        "L....f....f....L",
        "u....T....T.....",
        ".............d..",
        "V....u.........u",
        "...............d",
        "V...........d...",
        "nnnnn+.........T",
        ".....kq.........",
        "...............R",
        ".....k.....c.B..",
        ".....k..O...u..L",
    };

    /// <summary>Where you stand when you first come home.</summary>
    public static readonly Vector2I Spawn = new(8, 8);

    /// <summary>Inside the pens' fence, where the creatures wander.</summary>
    public static IEnumerable<Vector2I> Pen()
    {
        for (int y = 10; y <= 13; y++)
        for (int x = 0; x <= 4; x++) yield return new Vector2I(x, y);
    }

    /// <summary>
    /// The base as it stands for this ranch: a station not yet built is a plot of scaffolding, and the
    /// pieces you've put out stand where you put them.
    /// </summary>
    public static BattleMap Map(Ranch r)
    {
        var closed = Stations.Where(s => !s.Built(r)).Select(s => s.Prop).ToHashSet();
        var props = Props.Select(row => new string(row.Select(ch => closed.Contains(ch) ? 'Z' : ch).ToArray())).ToArray();
        var map = BattleMap.Custom("Home Base", Terrain, Heights, props, "Ranch");
        foreach (var d in r.Decor)
            if (DecorBook.Get(d.Id) is { } def) map.Put(d.Tile, def.Prop);
        foreach (var m in r.Machines)
            if (MachineBook.Get(m.Id) is { } def) map.Put(m.Tile, def.Prop);
        return map;
    }

    /// <summary>The station a tile belongs to, if any (scaffolding included).</summary>
    public static StationDef At(Vector2I t) =>
        t.Y >= 0 && t.Y < Props.Length && t.X >= 0 && t.X < Props[t.Y].Length ? Stations.FirstOrDefault(s => s.Prop == Props[t.Y][t.X]) : null;

    public static IEnumerable<Vector2I> Tiles(StationDef s)
    {
        for (int y = 0; y < Props.Length; y++)
        for (int x = 0; x < Props[y].Length; x++)
            if (Props[y][x] == s.Prop) yield return new Vector2I(x, y);
    }

    /// <summary>Built and not yet used: shown as new.</summary>
    public static bool Fresh(Ranch r, StationDef s) => s.Built(r) && !r.Flags.Contains("used:" + s.Id);
}
