using System.Linq;
using TheFartherWeStray.Creatures;

namespace TheFartherWeStray.Classes;

/// <summary>An augment: a graft bolted on (its look and its stats) and the attack it brings.</summary>
public sealed class AugmentDef
{
    public string Id, Name, Skill;
    public Graft Graft;
    public bool Chem;            // chemical (neon) rather than mechanical (brass)
    public MoveType? Move;       // a rig that changes how it moves (the user's yes, 2026-11-02): it flies or climbs
}

/// <summary>
/// Augments (the user's direction, 2026-11-02): mechanical and chemical work bought from undercity
/// shops, some only after certain missions, neon and metal to look at, bringing weapon-like and
/// chemical attacks; the undercity side's way of making a creature strong. Bolted on at the lab, two
/// to a creature, never inherited. Each one adds its graft (drawn by the sculptor, with that graft's
/// stats in <see cref="Tactics.Unit.From"/>) and teaches its attack, usable whatever the creature
/// trains as. Data: new augments are entries.
/// </summary>
public static class AugmentBook
{
    public const int Slots = 2;

    public static readonly AugmentDef[] All =
    {
        new() { Id = "piston_fist", Name = "Piston Fist", Graft = Graft.MetalLimb, Skill = "piston_punch" },
        new() { Id = "rivet_gun", Name = "Rivet Gun", Graft = Graft.Plating, Skill = "rivet_volley" },
        new() { Id = "lens_visor", Name = "Lens Visor", Graft = Graft.Visor, Skill = "targeting_beam" },
        new() { Id = "hextech_core", Name = "Crystal Core", Graft = Graft.Core, Skill = "core_discharge" },
        new() { Id = "acid_canister", Name = "Acid Canister", Graft = Graft.Canister, Skill = "acid_spray", Chem = true },
        new() { Id = "chem_veins", Name = "Chem Veins", Graft = Graft.Veins, Skill = "chem_rush", Chem = true },
        new() { Id = "strobe_tips", Name = "Strobe Tips", Graft = Graft.GlowTips, Skill = "strobe_flash", Chem = true },
        new() { Id = "drip_feed", Name = "Drip Feed", Graft = Graft.Drips, Skill = "toxic_drip", Chem = true },
        // Steamwork (2026-11-02): the clockwork's gears, pistons, smokestack and pipes, fitted to anyone.
        new() { Id = "gear_grinder", Name = "Gear Grinder", Graft = Graft.Gears, Skill = "gear_grind" },
        new() { Id = "steam_pistons", Name = "Steam Pistons", Graft = Graft.Pistons, Skill = "piston_charge" },
        new() { Id = "smokestack", Name = "Smokestack", Graft = Graft.Smokestack, Skill = "smoke_screen" },
        new() { Id = "pressure_pipes", Name = "Pressure Pipes", Graft = Graft.Pipework, Skill = "steam_vent" },
        // Movement rigs: frame wings to fly (drawn as wings in its metal), grapple claws to climb.
        new() { Id = "gear_wings", Name = "Gear Wings", Graft = Graft.None, Skill = "swoop", Move = MoveType.Fly },
        new() { Id = "grapple_claws", Name = "Grapple Claws", Graft = Graft.MetalLimb, Skill = "grapple", Move = MoveType.Climb },
    };

    public static readonly SkillDef[] Skills =
    {
        new() { Id = "piston_punch", Name = "Piston Punch", Class = "Augment", Cooldown = 3, Power = 1.5f, Knock = 1,
            Text = "A steam-driven punch (x1.5) that knocks the foe back a tile." },
        new() { Id = "rivet_volley", Name = "Rivet Volley", Class = "Augment", Cooldown = 2, Range = 3, Power = 0.9f,
            Text = "Hot brass rivets at range 3 (x0.9)." },
        new() { Id = "targeting_beam", Name = "Targeting Beam", Class = "Augment", Cooldown = 2, Range = 4, Straight = true, Power = 1f, HitBonus = 25,
            Text = "A focused beam down a straight line, range 4 (x1, +25% to hit)." },
        new() { Id = "core_discharge", Name = "Core Discharge", Class = "Augment", Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 1.2f, StunChance = 0.25f,
            Text = "The core vents: every foe beside it (x1.2), 25% to stun." },
        new() { Id = "acid_spray", Name = "Acid Spray", Class = "Augment", Cooldown = 3, Target = TargetKind.Area, Range = 3, Radius = 1, Power = 0.5f, Poison = 3, PoisonTurns = 3,
            Buffs = new[] { (StatKind.Def, -2, 2) }, Text = "A cross of acid at range 3 (x0.5): poison, and DEF -2 for 2 turns." },
        new() { Id = "chem_rush", Name = "Chem Rush", Class = "Augment", Cooldown = 4, Target = TargetKind.Self, Range = 0,
            Buffs = new[] { (StatKind.Atk, 4, 3), (StatKind.Spd, 3, 3) }, Text = "Floods its veins: ATK +4 and SPD +3 for 3 turns." },
        new() { Id = "strobe_flash", Name = "Strobe Flash", Class = "Augment", Cooldown = 3, Range = 2, Power = 0.3f, StunChance = 0.5f,
            Text = "A blinding neon flash at range 2 (x0.3): 50% to stun." },
        new() { Id = "toxic_drip", Name = "Toxic Drip", Class = "Augment", Cooldown = 2, Power = 0.8f, Poison = 5, PoisonTurns = 3,
            Text = "A bite laced with chem (x0.8): poison, 5 a turn for 3 turns." },
        new() { Id = "gear_grind", Name = "Gear Grind", Class = "Augment", Cooldown = 2, Power = 1.2f, Buffs = new[] { (StatKind.Def, -2, 2) },
            Text = "Grinding cogs (x1.2) that chew through armour: DEF -2 for 2 turns." },
        new() { Id = "piston_charge", Name = "Piston Charge", Class = "Augment", Cooldown = 3, Range = 3, Leap = true, Power = 1.1f, Knock = 1,
            Text = "The pistons fire it up to 3 tiles onto a foe (x1.1), knocking it back." },
        new() { Id = "smoke_screen", Name = "Smoke Screen", Class = "Augment", Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 1,
            Buffs = new[] { (StatKind.Evade, 25, 2) }, Text = "Belches smoke: it and allies beside it are 25% harder to hit for 2 turns." },
        new() { Id = "swoop", Name = "Swoop", Class = "Augment", Cooldown = 3, Range = 3, Leap = true, Power = 1f,
            Text = "On frame wings, it swoops onto a foe up to 3 tiles away (x1)." },
        new() { Id = "grapple", Name = "Grapple", Class = "Augment", Cooldown = 3, Range = 3, Pull = 2, Power = 0.6f,
            Text = "A grappling claw at a foe within 3 (x0.6), hauling it 2 tiles closer." },
        new() { Id = "steam_vent", Name = "Steam Vent", Class = "Augment", Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 0.8f, Knock = 1,
            Text = "Vents scalding steam: every foe beside it (x0.8), knocked back." },
    };

    public static AugmentDef Get(string id) => All.FirstOrDefault(a => a.Id == id);
    public static bool IsAugment(string id) => All.Any(a => a.Id == id);

    /// <summary>What an augment does, in a line: its graft and its attack.</summary>
    public static string Text(AugmentDef a) => $"{(a.Chem ? "Chemical" : "Mechanical")} augment: teaches {ClassBook.Skill(a.Skill).Name}. {ClassBook.Skill(a.Skill).Text}"
        + (a.Move is { } m ? $" It {(m == MoveType.Fly ? "flies" : "climbs")} from now on." : "");
}
