using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Ranching;

namespace CreatureTactics.World;

/// <summary>Something a shop sells: what, for how much chem, where, and what has to happen first.</summary>
public sealed class Ware
{
    public string Id;            // a piece of gear, an item, or a glowberry
    public int Price;
    public string[] At;          // the places that sell it
    public string OpenedBy;      // a main mission won first, if any
    public string Cup;           // a coliseum cup won first, if any
}

/// <summary>
/// The shops (the user's direction, 2026-11-02): each place sells its own stock, priced in chem, and
/// some wares only appear once a main mission or a cup is won. Fine things in the city above,
/// chem and machinery in the undercity, gear for the weather out in the far country. You buy where
/// you stand.
/// </summary>
public static class ShopBook
{
    public const string Berry = "glowberry";

    public static readonly Ware[] Stock =
    {
        new() { Id = Berry, Price = 8, At = new[] { "gardens", "lantern", "dunes" } },
        new() { Id = "tonic", Price = 12, At = new[] { "gardens", "lantern", "bridges", "coliseum" } },
        new() { Id = "smelling_salts", Price = 10, At = new[] { "lantern", "sump" } },
        new() { Id = "soothing_chime", Price = 18, At = new[] { "gardens", "bridges", "frost" } },
        new() { Id = "snare_net", Price = 25, At = new[] { "lantern", "foundry", "sump", "coast" } },
        new() { Id = "bell_collar", Price = 40, At = new[] { "gardens" } },
        new() { Id = "silk_scarf", Price = 45, At = new[] { "gardens" } },
        new() { Id = "lucky_horseshoe", Price = 35, At = new[] { "bridges" } },
        new() { Id = "hearty_harness", Price = 60, At = new[] { "bridges" } },
        new() { Id = "spiked_collar", Price = 55, At = new[] { "coliseum" } },
        new() { Id = "champions_sash", Price = 70, At = new[] { "coliseum" }, Cup = "bronze" },
        new() { Id = "focus_crystal", Price = 90, At = new[] { "hollow", "clocktower" } },
        new() { Id = "neon_tag", Price = 40, At = new[] { "lantern" } },
        new() { Id = "vial_bandolier", Price = 65, At = new[] { "lantern" }, OpenedBy = "main_foundry" },
        new() { Id = "knuckle_caps", Price = 50, At = new[] { "foundry" } },
        new() { Id = "shock_dampers", Price = 60, At = new[] { "foundry" } },
        new() { Id = "gyro_stabilizer", Price = 75, At = new[] { "foundry" }, OpenedBy = "main_sump" },
        new() { Id = "rebreather", Price = 55, At = new[] { "sump" } },
        new() { Id = "glowmoss_poultice", Price = 70, At = new[] { "sump" } },
        new() { Id = "fur_cloak", Price = 55, At = new[] { "frost" } },
        new() { Id = "sun_goggles", Price = 50, At = new[] { "dunes" } },
        new() { Id = "shell_bracer", Price = 50, At = new[] { "coast" } },
        new() { Id = "ember_charm", Price = 80, At = new[] { "cinder" } },
        // Augments: undercity work only, some once its roads are open.
        new() { Id = "piston_fist", Price = 70, At = new[] { "foundry" } },
        new() { Id = "rivet_gun", Price = 80, At = new[] { "foundry" } },
        new() { Id = "lens_visor", Price = 60, At = new[] { "foundry", "lantern" } },
        new() { Id = "hextech_core", Price = 110, At = new[] { "foundry" }, OpenedBy = "main_sump" },
        new() { Id = "acid_canister", Price = 80, At = new[] { "lantern", "sump" } },
        new() { Id = "drip_feed", Price = 70, At = new[] { "sump" } },
        new() { Id = "chem_veins", Price = 90, At = new[] { "lantern" }, OpenedBy = "main_coast" },
        new() { Id = "strobe_tips", Price = 85, At = new[] { "lantern" }, Cup = "bronze" },
    };

    /// <summary>What a place sells now.</summary>
    public static List<Ware> At(Ranch r, string place) => Stock.Where(w => w.At.Contains(place)
        && (w.OpenedBy == null || r.Story.Contains(w.OpenedBy)) && (w.Cup == null || r.Cups.Contains(w.Cup))).ToList();

    public static string Name(string id) => id == Berry ? "Glowberry" : AugmentBook.Get(id)?.Name ?? ClassBook.Passive(id)?.Name ?? ClassBook.Skill(id).Name;

    public static string Text(string id) => id == Berry
        ? "A lure for the curious: set it down where a curious creature will come for it. Every battle brings 3; these are extra."
        : AugmentBook.Get(id) is { } aug ? AugmentBook.Text(aug) + " Fitted at the lab; two to a creature; not inherited."
        : ClassBook.Passive(id)?.Text ?? ClassBook.Skill(id).Text;
}
