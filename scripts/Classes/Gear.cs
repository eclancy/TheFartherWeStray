using System.Linq;

namespace CreatureTactics.Classes;

/// <summary>
/// What the shops sell (the user's direction, 2026-11-02): gear for your creatures and items to help
/// catch wild ones. Gear is a passive with a price, worn in one slot whatever the creature trains as
/// (it merges into <see cref="Tactics.Unit.Passive"/> like a shiny's); items are skills with a count
/// instead of a cooldown, aimed like any skill and used up. Data, like the rest: new wares are entries.
/// </summary>
public static class GearBook
{
    public static readonly PassiveDef[] Gear =
    {
        // The city above: fine things.
        new() { Id = "bell_collar", Name = "Brass Bell Collar", Class = "Gear", Def = 1, Evade = 5, Text = "DEF +1; 5% harder to hit." },
        new() { Id = "silk_scarf", Name = "Silk Scarf", Class = "Gear", Spd = 1, Evade = 5, Text = "SPD +1; 5% harder to hit." },
        new() { Id = "lucky_horseshoe", Name = "Lucky Horseshoe", Class = "Gear", Hit = 10, Text = "+10% to hit." },
        new() { Id = "hearty_harness", Name = "Hearty Harness", Class = "Gear", HpPct = 0.15f, Text = "Max HP +15%." },
        new() { Id = "champions_sash", Name = "Champion's Sash", Class = "Gear", Atk = 2, Text = "ATK +2." },
        new() { Id = "spiked_collar", Name = "Spiked Collar", Class = "Gear", Atk = 1, Thorns = 2, Text = "ATK +1; melee attackers take 2." },
        new() { Id = "focus_crystal", Name = "Focus Crystal", Class = "Gear", CooldownCut = 1, Text = "Skills cool down a turn sooner." },
        // The undercity: chem and machinery.
        new() { Id = "neon_tag", Name = "Neon Tag", Class = "Gear", Hit = 5, Spd = 1, Text = "SPD +1; +5% to hit." },
        new() { Id = "vial_bandolier", Name = "Vial Bandolier", Class = "Gear", SkillPower = 0.1f, Text = "Skills hit 10% harder." },
        new() { Id = "knuckle_caps", Name = "Brass Knuckle Caps", Class = "Gear", Atk = 2, Text = "ATK +2." },
        new() { Id = "shock_dampers", Name = "Shock Dampers", Class = "Gear", NoStun = true, Text = "Can't be stunned." },
        new() { Id = "gyro_stabilizer", Name = "Gyro Stabilizer", Class = "Gear", Unstoppable = true, Text = "Can't be rooted or knocked back." },
        new() { Id = "rebreather", Name = "Rebreather Mask", Class = "Gear", NoPoison = true, Amphibious = true, Text = "Immune to poison; wades through chem and water." },
        new() { Id = "glowmoss_poultice", Name = "Glowmoss Poultice", Class = "Gear", Regen = 0.04f, Text = "Heals 4% of max HP each turn." },
        // The far country.
        new() { Id = "fur_cloak", Name = "Fur-Lined Cloak", Class = "Gear", Def = 2, HpPct = 0.05f, Text = "DEF +2; max HP +5%." },
        new() { Id = "sun_goggles", Name = "Sun Goggles", Class = "Gear", Hit = 8, Evade = 5, Text = "+8% to hit; 5% harder to hit." },
        new() { Id = "shell_bracer", Name = "Shell Bracer", Class = "Gear", Def = 2, Thorns = 1, Text = "DEF +2; melee attackers take 1." },
        new() { Id = "ember_charm", Name = "Ember Charm", Class = "Gear", Atk = 1, Regen = 0.05f, Text = "ATK +1; heals 5% of max HP each turn." },
    };

    public static readonly SkillDef[] Items =
    {
        new() { Id = "snare_net", Name = "Snare Net", Class = "Item", Cooldown = 0, Range = 3, Catch = 0.3f, CatchChance = 0.6f,
            Text = "Throw at a wild creature within 3: below 30% of its HP, 60% it's caught and joins you." },
        new() { Id = "soothing_chime", Name = "Soothing Chime", Class = "Item", Cooldown = 0, Range = 3, Root = true,
            Text = "A creature within 3 can't move next turn: a skittish one is easier to corner." },
        new() { Id = "tonic", Name = "Tonic", Class = "Item", Cooldown = 0, Target = TargetKind.Ally, Range = 1, Heal = 0.4f,
            Text = "You or an ally beside you: recover 40% of max HP." },
        new() { Id = "smelling_salts", Name = "Smelling Salts", Class = "Item", Cooldown = 0, Target = TargetKind.Ally, Range = 1, Cleanse = true,
            Text = "You or an ally beside you: clear poison, roots and stuns." },
    };

    public static bool IsItem(SkillDef s) => s.Class == "Item";
    public static bool IsGear(string id) => ClassBook.Passive(id)?.Class == "Gear";
    public static bool IsItem(string id) => Items.Any(i => i.Id == id);
}
