using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Tactics;

namespace TheFartherWeStray.World;

/// <summary>One thing a machine makes: chem, scrap or an item for the bag; how much a day at each level; what its hopper holds.</summary>
public sealed class MachineOutput
{
    public string Thing;     // MachineBook.Chem, MachineBook.Scrap, or an item id (a glowberry, a tonic)
    public float[] PerDay;   // at levels 1, 2 and 3 (0: not at that level)
    public int[] Holds;      // the hopper, at levels 1, 2 and 3
}

/// <summary>A machine for the farmyard: the prop it stands as, its price and shops, what it makes, what upgrading costs.</summary>
public sealed class MachineDef
{
    public string Id, Name, Text;
    public Prop Prop;
    public int Price;
    public string[] At;                    // the places that sell it
    public MachineOutput[] Makes;
    public (int chem, int scrap)[] Upgrades; // to level 2, then to level 3
}

/// <summary>
/// Machines that farm while you're out (the user's ask, 2026-11-03; docs/farmyard-plan.md, part 3):
/// clockwork, steam and chemistry, never computers. Each fills a hopper a little every game day (a battle
/// or a road), never in real time; you walk up and collect, and a full hopper stops. Upgrading costs chem
/// and scrap, which the press makes. One of each per yard. New machines are entries (and a prop).
/// </summary>
public static class MachineBook
{
    public const string Chem = "chem", Scrap = "scrap";
    public const int Levels = 3;
    /// <summary>A machine's id as a shop sells it.</summary>
    public const string WarePrefix = "machine:";

    public static readonly MachineDef[] All =
    {
        new()
        {
            Id = "still", Name = "Chem Still", Prop = Prop.Still, Price = 90, At = new[] { "lantern" },
            Text = "Copper and glass bubbling green: a little chem a day. A top-up, never a living.",
            Makes = new[] { new MachineOutput { Thing = Chem, PerDay = new[] { 3f, 5f, 8f }, Holds = new[] { 15, 30, 50 } } },
            Upgrades = new[] { (50, 10), (100, 25) },
        },
        new()
        {
            Id = "beds", Name = "Glowberry Beds", Prop = Prop.BerryBeds, Price = 60, At = new[] { "gardens", "coast", "hollow" },
            Text = "Raised beds of glowberries under a cold frame: a berry every other day, and tonics once they're tended.",
            Makes = new[]
            {
                new MachineOutput { Thing = ShopBook.Berry, PerDay = new[] { 0.5f, 1f, 1f }, Holds = new[] { 4, 6, 8 } },
                new MachineOutput { Thing = "tonic", PerDay = new[] { 0f, 0f, 0.25f }, Holds = new[] { 0, 0, 2 } },
            },
            Upgrades = new[] { (40, 8), (80, 20) },
        },
        new()
        {
            Id = "press", Name = "Scrap Press", Prop = Prop.ScrapPress, Price = 70, At = new[] { "lantern", "foundry" },
            Text = "A steam press that crushes salvage into scrap, for building the machines up.",
            Makes = new[] { new MachineOutput { Thing = Scrap, PerDay = new[] { 2f, 3f, 5f }, Holds = new[] { 10, 20, 30 } } },
            Upgrades = new[] { (40, 0), (90, 15) },
        },
    };

    public static MachineDef Get(string id) => All.FirstOrDefault(m => m.Id == id);

    public static bool IsWare(string wareId) => wareId.StartsWith(WarePrefix);

    public static MachineDef OfWare(string wareId) => IsWare(wareId) ? Get(wareId[WarePrefix.Length..]) : null;

    /// <summary>Every machine as the shops sell it.</summary>
    public static IEnumerable<Ware> Wares => All.Select(m => new Ware { Id = WarePrefix + m.Id, Price = m.Price, At = m.At });

    /// <summary>An amount of a thing in words: "12 chem", "3 scrap", "2 glowberries", "1 tonic".</summary>
    public static string Words(string thing, int n) => thing switch
    {
        Chem => $"{n} chem",
        Scrap => $"{n} scrap",
        ShopBook.Berry => n == 1 ? "1 glowberry" : $"{n} glowberries",
        _ => $"{n} {ShopBook.Name(thing).ToLowerInvariant()}{(n == 1 ? "" : "s")}",
    };

    /// <summary>The glyph for a thing a machine makes.</summary>
    public static string Glyph(string thing) => thing switch { Chem => "flask", Scrap => "gear", ShopBook.Berry => "berry", _ => "heal" };

    /// <summary>What a machine makes at a level, in a line: "5 chem a day, holds 30".</summary>
    public static string Rate(MachineDef def, int level) => string.Join("; ", def.Makes.Where(o => o.PerDay[level - 1] > 0).Select(o =>
        (o.PerDay[level - 1] >= 1 ? $"{Words(o.Thing, (int)o.PerDay[level - 1])} a day" : $"{Words(o.Thing, 1)} every {System.Math.Round(1 / o.PerDay[level - 1])} days")
        + $", holds {o.Holds[level - 1]}"));
}
