using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Tactics;

namespace TheFartherWeStray.World;

/// <summary>A piece for the farmyard: the prop it stands as, its price, where it's sold, whose look it is.</summary>
public sealed class DecorDef
{
    public string Id, Name, Text;
    public Prop Prop;
    public int Price;
    public string[] At;   // the places that sell it
    public Side Side;     // whose look it is: the city above's, the undercity's, or the country's own
}

/// <summary>
/// Decorating the farmyard (the user's ask, 2026-11-03; docs/farmyard-plan.md, part 1): pieces bought
/// at the shops, kept in the shed, put out at the workbench. Each side's shops sell its own look, gilded
/// in the city above, neon and chem below, the country's own out in the wilds; the everyday things are
/// sold where you start. New pieces are entries. A piece is a prop, so it's drawn like the board's.
/// </summary>
public static class DecorBook
{
    /// <summary>A piece's id as a shop sells it, so it never meets an item's id in the bag.</summary>
    public const string WarePrefix = "decor:";

    public static readonly DecorDef[] All =
    {
        // Everyday: sold where you start.
        new() { Id = "barrel", Name = "Barrel", Prop = Prop.Barrel, Price = 10, At = new[] { "lantern", "gardens" }, Side = Side.Wilds, Text = "A rain barrel, for by a wall." },
        new() { Id = "crate", Name = "Crate", Prop = Prop.Crate, Price = 10, At = new[] { "lantern", "gardens" }, Side = Side.Wilds, Text = "A stout crate. Stack a few and it's a yard." },
        new() { Id = "lamp", Name = "Lamp Post", Prop = Prop.Lamp, Price = 25, At = new[] { "lantern", "gardens" }, Side = Side.Wilds, Text = "A lamp on a post, lit at dusk." },
        new() { Id = "stall", Name = "Market Stall", Prop = Prop.Stall, Price = 50, At = new[] { "lantern", "gardens" }, Side = Side.Wilds, Text = "A striped awning over a counter, for selling nothing in particular." },
        new() { Id = "bush", Name = "Bush", Prop = Prop.Bush, Price = 12, At = new[] { "lantern", "gardens", "coast", "hollow" }, Side = Side.Wilds, Text = "Something green." },
        // The country's own.
        new() { Id = "tree", Name = "Shade Tree", Prop = Prop.Tree, Price = 30, At = new[] { "gardens", "coast", "hollow" }, Side = Side.Wilds, Text = "A broad tree, grown in a tub and planted out." },
        new() { Id = "pine", Name = "Pine", Prop = Prop.Pine, Price = 30, At = new[] { "frost", "hollow" }, Side = Side.Wilds, Text = "A pine from the pass, still smelling of snow." },
        new() { Id = "rock", Name = "Boulder", Prop = Prop.Rock, Price = 12, At = new[] { "lantern", "hollow", "dunes", "cinder", "frost" }, Side = Side.Wilds, Text = "A good sitting rock." },
        new() { Id = "reeds", Name = "Reeds", Prop = Prop.Reeds, Price = 8, At = new[] { "sump", "coast" }, Side = Side.Wilds, Text = "A clump of reeds. You can walk through them." },
        new() { Id = "mushroom", Name = "Giant Mushroom", Prop = Prop.Mushroom, Price = 25, At = new[] { "sump", "hollow" }, Side = Side.Wilds, Text = "Grown in the dark, glowing faintly. Don't eat it." },
        new() { Id = "crystals", Name = "Crystal Cluster", Prop = Prop.Crystals, Price = 45, At = new[] { "hollow", "frost" }, Side = Side.Wilds, Text = "Hexlight crystal, prised out whole." },
        // The city above.
        new() { Id = "hedge", Name = "Hedge", Prop = Prop.Hedge, Price = 18, At = new[] { "gardens", "bridges" }, Side = Side.City, Text = "Clipped square, the way they like it up there." },
        new() { Id = "banner", Name = "Banner", Prop = Prop.Banner, Price = 15, At = new[] { "gardens", "bridges", "coliseum" }, Side = Side.City, Text = "A banner on a pole. You can walk under it." },
        new() { Id = "pillar", Name = "Column", Prop = Prop.Pillar, Price = 45, At = new[] { "gardens", "clocktower" }, Side = Side.City, Text = "A marble column holding up nothing at all." },
        new() { Id = "statue", Name = "Statue", Prop = Prop.Statue, Price = 70, At = new[] { "gardens", "bridges" }, Side = Side.City, Text = "Somebody important, in stone." },
        new() { Id = "fountain", Name = "Fountain", Prop = Prop.Fountain, Price = 90, At = new[] { "gardens" }, Side = Side.City, Text = "A gilded age, in a basin." },
        // The undercity.
        new() { Id = "neon", Name = "Neon Sign", Prop = Prop.Neon, Price = 60, At = new[] { "lantern" }, Side = Side.Undercity, Text = "A sign in glowing glass tube, lit at all hours." },
        new() { Id = "tank", Name = "Chem Tank", Prop = Prop.Tank, Price = 45, At = new[] { "foundry", "sump" }, Side = Side.Undercity, Text = "A glass tank of something green. Probably empty." },
        new() { Id = "gears", Name = "Gear Heap", Prop = Prop.Gears, Price = 35, At = new[] { "foundry" }, Side = Side.Undercity, Text = "Cogs off a dead machine, piled up to look like art." },
    };

    /// <summary>What a new farmyard has in the shed: enough to start decorating before the first shop.</summary>
    public static readonly (string id, int count)[] Starter =
    {
        ("lamp", 1), ("barrel", 2), ("crate", 1), ("bush", 2), ("banner", 1),
    };

    public static DecorDef Get(string id) => All.FirstOrDefault(d => d.Id == id);

    public static bool IsWare(string wareId) => wareId.StartsWith(WarePrefix);

    public static DecorDef OfWare(string wareId) => IsWare(wareId) ? Get(wareId[WarePrefix.Length..]) : null;

    /// <summary>Every piece as the shops sell it.</summary>
    public static IEnumerable<Ware> Wares => All.Select(d => new Ware { Id = WarePrefix + d.Id, Price = d.Price, At = d.At });
}
