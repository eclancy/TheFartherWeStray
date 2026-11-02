using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Classes;

public enum StatKind { Atk, Def, Spd, Move, Evade, Thorns }
public enum TargetKind { Enemy, Ally, Self, Area, Tile }

/// <summary>
/// One skill, as data: who it targets and how far, what it does to whatever it hits. Battle reads
/// these fields; there is no per-skill code.
/// </summary>
public sealed class SkillDef
{
    public string Id, Name, Class, Text;
    public int Cost = 15, Cooldown = 2;
    public TargetKind Target = TargetKind.Enemy;
    public int Range = 1;            // tiles from the user (footprint distance)
    public int Radius;               // 0: one target; n: everything that many tiles from the target tile
    public bool AroundSelf;          // the area is centred on the user (no aiming)
    public float Power;              // damage, as a multiple of a basic attack
    public int HitBonus;
    public float Heal;               // fraction of the target's max HP
    public (StatKind stat, int amount, int turns)[] Buffs = Array.Empty<(StatKind, int, int)>();
    public int Poison, PoisonTurns;
    public float StunChance;
    public bool Root, Taunt;
    public bool Leap;                // jump next to the target first, over anything
    public int Knock;                // tiles of knockback
    public bool HitAndRun;           // may move again after using it
    public int Pull;                 // drag the target this many tiles toward the user
    public float Drain;              // the user heals this share of the damage dealt
    public bool Steal;               // take the target's boosts for yourself
    public bool Cleanse;             // clear poison, roots, stuns and curses
    public float HighGround;         // extra damage multiplier when striking from higher ground
    public float Flank;              // extra damage multiplier when an ally is next to the target
    public bool Straight;            // only along straight lines
    public int CooldownCut;          // the target's skills cool down this much sooner
    public bool RandomBuff;          // a random strong boost
    public float HealOnKill;         // share of max HP regained if the hit kills
    public float Desperate;          // extra damage multiplier while the user is below half HP
    public float Execute;            // extra damage multiplier against a foe below 40% HP
    public float Catch, CatchChance; // a snare: below this share of its HP, a free wild creature is caught with this chance
}


/// <summary>
/// A passive (FFTA's support abilities): learned as the last lesson of a class, then equipped in
/// the passive slot, from any class the creature has trained in. Stat bonuses and rule changes.
/// </summary>
public enum PassiveKind { Support, Reaction }

/// <summary>What a reaction does when its creature is hit (FFTA's R-abilities).</summary>
public enum ReactionKind { None, Counter, ReturnFire, Retreat, Brace, Protector, Burst, Dodge, Guard, Adrenaline, Shock, RallyCry, DirtyRetort }

public sealed class PassiveDef
{
    public string Id, Name, Class, Text;
    public int Cost = 30;
    public PassiveKind Kind = PassiveKind.Support;
    public ReactionKind React;
    public float ReactPower, ReactChance;
    public int ReactAmount;
    public int Atk, Def, Spd, Move, Evade, Hit, Thorns;
    public float HpPct;              // extra max HP, as a share
    public bool NoPoison, NoStun;    // immunities
    public bool Unstoppable;         // no knockback, no roots
    public bool Amphibious;          // wades through chem; no slowdown on land for swimmers
    public bool LongReach;           // basic attacks reach 2 in a line
    public bool Echo;                // boosts it gives last a turn longer
    public bool PackBond;            // ATK +1 for each ally beside it
    public float Regen;              // share of max HP healed at the start of each turn
    public float Drain;              // share of basic-attack damage healed
    public float SkillPower;         // extra skill damage, as a share
    public float Predator;           // extra damage against foes under half HP
    public int RangeBonus;           // ranged skills reach further
    public int CooldownCut;          // skills cool down sooner
    public int Climb;                // can step up or down this many levels
    public int ChemBonus;            // extra chem after each battle it fights in

    /// <summary>Two support passives in effect at once (a shiny's own on top of the equipped one): numbers add, flags join.</summary>
    public static PassiveDef Merge(PassiveDef a, PassiveDef b)
    {
        if (a == null || b == null) return a ?? b;
        return new PassiveDef
        {
            Id = a.Id + "+" + b.Id, Name = a.Name + " + " + b.Name, Class = a.Class, Text = a.Text + " " + b.Text,
            Atk = a.Atk + b.Atk, Def = a.Def + b.Def, Spd = a.Spd + b.Spd, Move = a.Move + b.Move, Evade = a.Evade + b.Evade,
            Hit = a.Hit + b.Hit, Thorns = a.Thorns + b.Thorns, HpPct = a.HpPct + b.HpPct,
            NoPoison = a.NoPoison || b.NoPoison, NoStun = a.NoStun || b.NoStun, Unstoppable = a.Unstoppable || b.Unstoppable,
            Amphibious = a.Amphibious || b.Amphibious, LongReach = a.LongReach || b.LongReach, Echo = a.Echo || b.Echo,
            PackBond = a.PackBond || b.PackBond, Regen = a.Regen + b.Regen, Drain = a.Drain + b.Drain,
            SkillPower = a.SkillPower + b.SkillPower, Predator = a.Predator + b.Predator, RangeBonus = a.RangeBonus + b.RangeBonus,
            CooldownCut = a.CooldownCut + b.CooldownCut, Climb = Math.Max(a.Climb, b.Climb), ChemBonus = a.ChemBonus + b.ChemBonus,
        };
    }
}

/// <summary>A training discipline (FFTA's jobs): stat leanings and the skills it teaches.</summary>
public sealed class ClassDef
{
    public string Id, Name, Blurb;
    public int Hp, Atk, Def, Spd, Move;
    public string[] Skills;
    public (string cls, int skills)[] Requires = Array.Empty<(string, int)>();
    public Func<Creature, bool> NeedsPart;
    public string PartHint;
    public bool Advanced => Requires.Length > 0;
}

/// <summary>What a creature has trained: its class, a secondary skill set, JP per class, skills learned.</summary>
public sealed class Training
{
    public string Class = "Brawler";
    public string Secondary;
    public Dictionary<string, int> Jp = new();
    public List<string> Skills = new();
    public string Innate;  // a skill inherited from a parent, usable in any class
    public List<string> Passives = new(); // passives learned (one per class mastered)
    public List<string> Masteries = new(); // classes trained to three stars
    public string Passive;  // the support passive equipped, from any class trained
    public string Reaction; // the reaction equipped (FFTA's R-ability slot)
    public string Gear;     // the gear it wears, bought at a shop (one slot)

    public int JpIn(string cls) => Jp.TryGetValue(cls, out int v) ? v : 0;
    public int LearnedIn(string cls) => Skills.Count(id => ClassBook.Skill(id).Class == cls);

    /// <summary>Skills usable in battle: the class's and the secondary's learned skills, plus any innate one.</summary>
    public IEnumerable<SkillDef> Usable()
    {
        var ids = Skills.Where(id => ClassBook.Skill(id).Class == Class || ClassBook.Skill(id).Class == Secondary).ToList();
        if (Innate != null && !ids.Contains(Innate)) ids.Add(Innate);
        return ids.Select(ClassBook.Skill);
    }

    /// <summary>Adds JP to the current class and learns its skills in order as JP passes their costs.</summary>
    public List<string> Earn(int jp)
    {
        Jp[Class] = JpIn(Class) + jp;
        var learned = new List<string>();
        int spent = 0;
        foreach (var id in ClassBook.Class(Class).Skills)
        {
            var s = ClassBook.Skill(id);
            spent += s.Cost;
            if (JpIn(Class) < spent || Skills.Contains(id)) continue;
            Skills.Add(id);
            learned.Add(s.Name);
        }
        // The last lessons of a class are its passives. The first of each kind learned is equipped for you.
        foreach (var passive in ClassBook.PassivesOf(Class))
        {
            spent += passive.Cost;
            if (JpIn(Class) < spent || Passives.Contains(passive.Id)) continue;
            Passives.Add(passive.Id);
            if (passive.Kind == PassiveKind.Support) Passive ??= passive.Id; else Reaction ??= passive.Id;
            learned.Add(passive.Name + (passive.Kind == PassiveKind.Reaction ? " (reaction)" : " (passive)"));
        }
        // Training on past everything masters the class: a permanent bonus in any class.
        if (Stars(Class) == 3 && !Masteries.Contains(Class))
        {
            Masteries.Add(Class);
            learned.Add($"MASTERY of {Class}: {ClassBook.MasteryText(Class)}, for good");
        }
        return learned;
    }

    /// <summary>Mastery stars: 1 for all three skills, 2 for its passives too, 3 for training on past them.</summary>
    public int Stars(string cls)
    {
        var k = ClassBook.Class(cls);
        if (k.Skills.Any(s => !Skills.Contains(s))) return 0;
        if (ClassBook.PassivesOf(cls).Any(p => !Passives.Contains(p.Id))) return 1;
        return JpIn(cls) >= ClassBook.TotalCost(cls) + ClassBook.MasteryExtra ? 3 : 2;
    }

    /// <summary>A starter: in the given class, with its first skill already learned.</summary>
    public static Training Starter(string cls)
    {
        var t = new Training { Class = cls };
        t.Earn(ClassBook.Skill(ClassBook.Class(cls).Skills[0]).Cost);
        return t;
    }

    public Training Copy() => new() { Class = Class, Secondary = Secondary, Jp = new(Jp), Skills = new(Skills), Innate = Innate, Passives = new(Passives), Passive = Passive, Reaction = Reaction, Masteries = new(Masteries), Gear = Gear };
}

/// <summary>Every class and skill. The notes' MVP: six base classes and three unlockable ones, two of which need a part.</summary>
public static class ClassBook
{
    static readonly (StatKind, int, int)[] None = Array.Empty<(StatKind, int, int)>();

    public static readonly SkillDef[] Skills =
    {
        // Brawler: close-in damage.
        new() { Id = "heavy_blow", Name = "Heavy Blow", Class = "Brawler", Cost = 15, Cooldown = 2, Power = 1.5f, HitBonus = -10, Text = "A slow, crushing hit (x1.5)." },
        new() { Id = "rend", Name = "Rend", Class = "Brawler", Cost = 20, Cooldown = 3, Power = 1f, Poison = 3, PoisonTurns = 3, Text = "Tears a wound that bleeds for 3 turns." },
        new() { Id = "frenzy", Name = "Frenzy", Class = "Brawler", Cost = 25, Cooldown = 4, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Atk, 4, 3) }, Text = "ATK +4 for 3 turns." },
        // Skirmisher: mobility.
        new() { Id = "quick_strike", Name = "Quick Strike", Class = "Skirmisher", Cost = 15, Cooldown = 2, Power = 0.8f, HitBonus = 20, HitAndRun = true, Text = "A fast hit (x0.8, +20 hit); you may move again." },
        new() { Id = "pounce", Name = "Pounce", Class = "Skirmisher", Cost = 20, Cooldown = 3, Range = 3, Leap = true, Power = 1f, Text = "Leap up to 3 tiles onto a foe and strike." },
        new() { Id = "evasion", Name = "Evasion", Class = "Skirmisher", Cost = 25, Cooldown = 4, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Evade, 25, 3) }, Text = "25% harder to hit for 3 turns." },
        // Guardian: protection.
        new() { Id = "shield_wall", Name = "Shield Wall", Class = "Guardian", Cost = 15, Cooldown = 3, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 1, Buffs = new[] { (StatKind.Def, 3, 3) }, Text = "You and adjacent allies: DEF +3 for 3 turns." },
        new() { Id = "taunt", Name = "Taunt", Class = "Guardian", Cost = 20, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 3, Taunt = true, Text = "Foes within 3 must come for you next turn." },
        new() { Id = "second_wind", Name = "Second Wind", Class = "Guardian", Cost = 25, Cooldown = 4, Target = TargetKind.Self, Range = 0, Heal = 0.35f, Text = "Recover 35% of max HP." },
        // Mystic: hextech, at range.
        new() { Id = "hex_bolt", Name = "Hex Bolt", Class = "Mystic", Cost = 15, Cooldown = 2, Range = 4, Power = 1.1f, Text = "A bolt of hextech at range 4 (x1.1)." },
        new() { Id = "ward", Name = "Ward", Class = "Mystic", Cost = 20, Cooldown = 3, Target = TargetKind.Ally, Range = 3, Buffs = new[] { (StatKind.Def, 4, 3) }, Text = "An ally within 3: DEF +4 for 3 turns." },
        new() { Id = "arc_storm", Name = "Arc Storm", Class = "Mystic", Cost = 25, Cooldown = 3, Target = TargetKind.Area, Range = 4, Radius = 1, Power = 0.8f, Text = "Lightning across a 3x3 cross at range 4 (x0.8)." },
        // Alchemist: chemtech, poison and healing.
        new() { Id = "toxic_flask", Name = "Toxic Flask", Class = "Alchemist", Cost = 15, Cooldown = 2, Range = 3, Power = 0.6f, Poison = 4, PoisonTurns = 3, Text = "Range 3 (x0.6) and poison: 4 a turn for 3 turns." },
        new() { Id = "mend", Name = "Mend", Class = "Alchemist", Cost = 15, Cooldown = 2, Target = TargetKind.Ally, Range = 3, Heal = 0.4f, Text = "An ally within 3 recovers 40% of max HP." },
        new() { Id = "acid_splash", Name = "Acid Splash", Class = "Alchemist", Cost = 25, Cooldown = 3, Target = TargetKind.Area, Range = 3, Radius = 1, Power = 0.5f, Buffs = new[] { (StatKind.Def, -3, 3) }, Text = "A cross of acid at range 3 (x0.5): DEF -3 for 3 turns." },
        // Tinker: clockwork tricks.
        new() { Id = "overclock", Name = "Overclock", Class = "Tinker", Cost = 15, Cooldown = 3, Target = TargetKind.Ally, Range = 2, Buffs = new[] { (StatKind.Spd, 4, 3), (StatKind.Move, 1, 3) }, Text = "An ally within 2: SPD +4 and MOVE +1 for 3 turns." },
        new() { Id = "snare", Name = "Snare", Class = "Tinker", Cost = 20, Cooldown = 3, Range = 3, Power = 0.5f, Root = true, Text = "Range 3 (x0.5): the foe can't move next turn." },
        new() { Id = "shock_prod", Name = "Shock Prod", Class = "Tinker", Cost = 25, Cooldown = 3, Power = 0.9f, StunChance = 0.5f, Text = "A jolt (x0.9) with a 50% chance to stun." },
        // Skydiver (Brawler + Skirmisher, needs wings).
        new() { Id = "dive_bomb", Name = "Dive Bomb", Class = "Skydiver", Cost = 30, Cooldown = 3, Range = 5, Leap = true, Power = 1.8f, Text = "Dive on a foe up to 5 tiles away (x1.8)." },
        new() { Id = "gale", Name = "Gale", Class = "Skydiver", Cost = 30, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 0.6f, Knock = 1, Text = "Wingbeats batter adjacent foes (x0.6) and push them back." },
        new() { Id = "updraft", Name = "Updraft", Class = "Skydiver", Cost = 35, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 1, Buffs = new[] { (StatKind.Move, 2, 2), (StatKind.Evade, 15, 2) }, Text = "You and adjacent allies: MOVE +2 and harder to hit for 2 turns." },
        // Juggernaut (Brawler + Guardian, needs horns).
        new() { Id = "trample", Name = "Trample", Class = "Juggernaut", Cost = 30, Cooldown = 3, Range = 4, Leap = true, Power = 1.6f, Knock = 2, Text = "Charge a foe up to 4 tiles away (x1.6) and throw it back 2." },
        new() { Id = "iron_hide", Name = "Iron Hide", Class = "Juggernaut", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Def, 6, 3) }, Text = "DEF +6 for 3 turns." },
        new() { Id = "earthshaker", Name = "Earthshaker", Class = "Juggernaut", Cost = 35, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Power = 0.9f, Text = "Stamp the ground: every foe within 2 (x0.9)." },
        // Hexwarden (Mystic + Tinker).
        new() { Id = "hex_cage", Name = "Hex Cage", Class = "Hexwarden", Cost = 30, Cooldown = 3, Range = 4, Power = 0.7f, Root = true, Buffs = new[] { (StatKind.Def, -2, 2) }, Text = "Range 4 (x0.7): root the foe and crack its DEF -2." },
        new() { Id = "overcharge", Name = "Overcharge", Class = "Hexwarden", Cost = 30, Cooldown = 4, Target = TargetKind.Ally, Range = 3, Buffs = new[] { (StatKind.Atk, 4, 3), (StatKind.Spd, 2, 3) }, Text = "An ally within 3: ATK +4 and SPD +2 for 3 turns." },
        new() { Id = "nova", Name = "Nova", Class = "Hexwarden", Cost = 35, Cooldown = 5, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Power = 1.2f, Text = "A burst of hextech: every foe within 2 (x1.2)." },
        // Hunter: ranged physical (quills, spit, thrown stones).
        new() { Id = "quill_shot", Name = "Quill Shot", Class = "Hunter", Cost = 15, Cooldown = 1, Range = 3, Power = 0.9f, Text = "A shot at range 3 (x0.9)." },
        new() { Id = "pin_down", Name = "Pin Down", Class = "Hunter", Cost = 20, Cooldown = 3, Range = 3, Power = 0.6f, Root = true, Text = "Range 3 (x0.6): pins the foe so it can't move next turn." },
        new() { Id = "volley", Name = "Volley", Class = "Hunter", Cost = 25, Cooldown = 3, Target = TargetKind.Area, Range = 4, Radius = 1, Power = 0.7f, Text = "A spray of quills across a cross at range 4 (x0.7)." },
        // Howler: voice and morale.
        new() { Id = "war_cry", Name = "War Cry", Class = "Howler", Cost = 15, Cooldown = 3, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 2, Buffs = new[] { (StatKind.Atk, 3, 3) }, Text = "You and allies within 2: ATK +3 for 3 turns." },
        new() { Id = "screech", Name = "Screech", Class = "Howler", Cost = 20, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, StunChance = 0.3f, Buffs = new[] { (StatKind.Def, -2, 2) }, Text = "Foes within 2: DEF -2, and a 30% chance to stun." },
        new() { Id = "lullaby", Name = "Lullaby", Class = "Howler", Cost = 25, Cooldown = 4, Range = 3, StunChance = 0.65f, HitBonus = 10, Text = "Range 3: a 65% chance the foe sleeps through its next turn." },
        // Rogue: dirty fighting.
        new() { Id = "backstab", Name = "Backstab", Class = "Rogue", Cost = 15, Cooldown = 2, Power = 1.2f, Flank = 0.6f, Text = "x1.2, or x1.8 when an ally is next to the target." },
        new() { Id = "smoke_bomb", Name = "Smoke Bomb", Class = "Rogue", Cost = 20, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 1, Buffs = new[] { (StatKind.Evade, 25, 2) }, Text = "You and adjacent allies: 25% harder to hit for 2 turns." },
        new() { Id = "pilfer", Name = "Pilfer", Class = "Rogue", Cost = 25, Cooldown = 3, Power = 0.5f, Steal = true, HitBonus = 10, Text = "A light hit (x0.5) that steals every boost the foe has." },
        // Bulwark (Guardian + Alchemist, needs a shell).
        new() { Id = "shell_up", Name = "Shell Up", Class = "Bulwark", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Def, 8, 3), (StatKind.Spd, -3, 3) }, Text = "DEF +8 and SPD -3 for 3 turns." },
        new() { Id = "spiked_rampart", Name = "Spiked Rampart", Class = "Bulwark", Cost = 30, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 1, Buffs = new[] { (StatKind.Thorns, 4, 3) }, Text = "You and adjacent allies hurt melee attackers for 4 (3 turns)." },
        new() { Id = "haul", Name = "Haul", Class = "Bulwark", Cost = 35, Cooldown = 3, Target = TargetKind.Ally, Range = 4, Pull = 3, Buffs = new[] { (StatKind.Def, 3, 2) }, Text = "Drag an ally within 4 to your side; it gets DEF +3." },
        // Lasher (Brawler + Tinker, needs a whip tail).
        new() { Id = "crack", Name = "Crack", Class = "Lasher", Cost = 30, Cooldown = 2, Range = 3, Straight = true, Power = 1.1f, Text = "Whip a foe up to 3 tiles away in a straight line (x1.1)." },
        new() { Id = "snare_lash", Name = "Snare Lash", Class = "Lasher", Cost = 30, Cooldown = 3, Range = 3, Straight = true, Power = 0.5f, Pull = 2, Root = true, Text = "Reel a foe in 2 tiles and hold it there." },
        new() { Id = "whirl", Name = "Whirl", Class = "Lasher", Cost = 35, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Power = 0.7f, Text = "Spin: every foe within 2 (x0.7)." },
        // Stalker (Rogue + Alchemist, needs claws).
        new() { Id = "ambush", Name = "Ambush", Class = "Stalker", Cost = 30, Cooldown = 3, Power = 1.2f, HighGround = 0.8f, Text = "x1.2, or x2.0 striking down from higher ground." },
        new() { Id = "venom_claws", Name = "Venom Claws", Class = "Stalker", Cost = 30, Cooldown = 3, Power = 1f, Poison = 5, PoisonTurns = 3, Text = "A clawing (x1) that poisons for 5 a turn, 3 turns." },
        new() { Id = "shadow_step", Name = "Shadow Step", Class = "Stalker", Cost = 35, Cooldown = 3, Range = 5, Leap = true, Power = 0.9f, HitBonus = 15, Text = "Appear beside a foe up to 5 tiles away and strike (x0.9)." },
        // Leviathan (Guardian + Mystic, needs to swim).
        new() { Id = "tidal_crash", Name = "Tidal Crash", Class = "Leviathan", Cost = 30, Cooldown = 3, Target = TargetKind.Area, Range = 3, Radius = 1, Power = 0.8f, Knock = 1, Text = "A wave across a cross at range 3 (x0.8) that knocks foes back." },
        new() { Id = "undertow", Name = "Undertow", Class = "Leviathan", Cost = 30, Cooldown = 3, Range = 4, Power = 0.6f, Pull = 3, Text = "Drag a foe up to 3 tiles toward you (x0.6)." },
        new() { Id = "deep_shell", Name = "Deep Shell", Class = "Leviathan", Cost = 35, Cooldown = 4, Target = TargetKind.Self, Range = 0, Heal = 0.25f, Buffs = new[] { (StatKind.Def, 5, 3) }, Text = "Recover 25% of max HP and DEF +5 for 3 turns." },
        // Titan (Brawler + Guardian, needs to be huge).
        new() { Id = "stomp", Name = "Stomp", Class = "Titan", Cost = 30, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 0.8f, StunChance = 0.4f, Text = "Every adjacent foe (x0.8), 40% chance to stun." },
        new() { Id = "shrug_off", Name = "Shrug Off", Class = "Titan", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Cleanse = true, Buffs = new[] { (StatKind.Def, 4, 3) }, Text = "Throw off poison, roots and curses; DEF +4 for 3 turns." },
        new() { Id = "toss", Name = "Toss", Class = "Titan", Cost = 35, Cooldown = 3, Power = 0.8f, Knock = 3, Text = "Hurl a foe 3 tiles (x0.8, more if it hits a wall)." },
        // Trickster (Skirmisher + Mystic, needs to be tiny).
        new() { Id = "blink", Name = "Blink", Class = "Trickster", Cost = 30, Cooldown = 3, Target = TargetKind.Tile, Range = 4, Text = "Vanish and reappear on any free tile within 4." },
        new() { Id = "vanish", Name = "Vanish", Class = "Trickster", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Evade, 90, 1) }, Text = "Nearly impossible to hit until your next turn." },
        new() { Id = "hex_prank", Name = "Hex Prank", Class = "Trickster", Cost = 35, Cooldown = 3, Range = 3, Buffs = new[] { (StatKind.Atk, -3, 3), (StatKind.Def, -3, 3) }, Text = "Range 3: ATK -3 and DEF -3 for 3 turns." },
        // Packleader (Howler + Brawler, needs a loyal or aggressive temperament).
        new() { Id = "rally", Name = "Rally", Class = "Packleader", Cost = 30, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 2, Buffs = new[] { (StatKind.Move, 1, 2), (StatKind.Spd, 2, 2) }, Text = "You and allies within 2: MOVE +1 and SPD +2 for 2 turns." },
        new() { Id = "pack_tactics", Name = "Pack Tactics", Class = "Packleader", Cost = 30, Cooldown = 2, Power = 1.1f, Flank = 0.7f, HitBonus = 10, Text = "x1.1, or x1.8 when an ally is next to the target." },
        new() { Id = "alpha_howl", Name = "Alpha Howl", Class = "Packleader", Cost = 35, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 3, Taunt = true, Buffs = new[] { (StatKind.Atk, -3, 2) }, Text = "Foes within 3: ATK -3, and they must come for you." },
        // Deadeye (Hunter + Tinker, needs a lens visor or extra eyes).
        new() { Id = "headshot", Name = "Headshot", Class = "Deadeye", Cost = 30, Cooldown = 3, Range = 5, Power = 1.6f, HitBonus = -10, Text = "A careful shot at range 5 (x1.6)." },
        new() { Id = "mark", Name = "Mark", Class = "Deadeye", Cost = 30, Cooldown = 3, Range = 5, Buffs = new[] { (StatKind.Def, -4, 3), (StatKind.Evade, -20, 3) }, Text = "Range 5: DEF -4 and 20% easier to hit for 3 turns." },
        new() { Id = "ricochet", Name = "Ricochet", Class = "Deadeye", Cost = 35, Cooldown = 3, Target = TargetKind.Area, Range = 4, Radius = 1, Power = 0.8f, Text = "A shot that bounces across a cross at range 4 (x0.8)." },
        // Chemist (Alchemist + chemtech blood).
        new() { Id = "shimmer_dose", Name = "Shimmer Dose", Class = "Chemist", Cost = 30, Cooldown = 4, Target = TargetKind.Ally, Range = 2, Buffs = new[] { (StatKind.Atk, 6, 2), (StatKind.Spd, 3, 2) }, Poison = 3, PoisonTurns = 2, Text = "An ally within 2: ATK +6, SPD +3 for 2 turns, but it's poisoned." },
        new() { Id = "caustic_cloud", Name = "Caustic Cloud", Class = "Chemist", Cost = 30, Cooldown = 3, Target = TargetKind.Area, Range = 3, Radius = 1, Power = 0.3f, Poison = 5, PoisonTurns = 3, Text = "A cloud across a cross at range 3: poison 5 a turn for 3 turns." },
        new() { Id = "mutagen", Name = "Mutagen", Class = "Chemist", Cost = 35, Cooldown = 3, Target = TargetKind.Ally, Range = 2, RandomBuff = true, Text = "An ally within 2 gets a random strong boost for 3 turns." },
        // Runeweaver (Mystic + arcane blood).
        new() { Id = "glyph_snare", Name = "Glyph Snare", Class = "Runeweaver", Cost = 30, Cooldown = 3, Range = 4, Power = 0.5f, Root = true, HitBonus = 15, Text = "Range 4 (x0.5): a glyph roots the foe." },
        new() { Id = "glyph_shield", Name = "Glyph Shield", Class = "Runeweaver", Cost = 30, Cooldown = 3, Target = TargetKind.Ally, Range = 3, Buffs = new[] { (StatKind.Def, 6, 2) }, Cleanse = true, Text = "An ally within 3: cleansed, and DEF +6 for 2 turns." },
        new() { Id = "arcane_surge", Name = "Arcane Surge", Class = "Runeweaver", Cost = 35, Cooldown = 5, Target = TargetKind.Ally, Range = 3, CooldownCut = 2, Text = "An ally within 3: its skills cool down 2 turns sooner." },
        // Voidcaller (Brawler or Mystic + abyssal blood).
        new() { Id = "dread", Name = "Dread", Class = "Voidcaller", Cost = 30, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Root = true, Buffs = new[] { (StatKind.Spd, -3, 2) }, Text = "Foes within 2 freeze in terror: rooted, SPD -3." },
        new() { Id = "siphon", Name = "Siphon", Class = "Voidcaller", Cost = 30, Cooldown = 2, Range = 2, Power = 1f, Drain = 0.5f, Text = "Range 2 (x1): heal for half the damage." },
        new() { Id = "maw", Name = "Maw", Class = "Voidcaller", Cost = 35, Cooldown = 3, Power = 1.8f, HitBonus = -10, HealOnKill = 0.3f, Text = "A huge bite (x1.8); a kill heals 30% of max HP." },
        // Artificer (Tinker + two machine grafts).
        new() { Id = "spark_coil", Name = "Spark Coil", Class = "Artificer", Cost = 30, Cooldown = 3, Target = TargetKind.Area, Range = 4, Radius = 1, Power = 0.6f, StunChance = 0.3f, Text = "A coil discharges across a cross at range 4 (x0.6), 30% to stun." },
        new() { Id = "repair", Name = "Repair", Class = "Artificer", Cost = 30, Cooldown = 2, Target = TargetKind.Ally, Range = 2, Heal = 0.45f, Cleanse = true, Text = "An ally within 2: recovers 45% of max HP and is cleansed." },
        new() { Id = "overdrive", Name = "Overdrive", Class = "Artificer", Cost = 35, Cooldown = 4, Target = TargetKind.Ally, Range = 2, Buffs = new[] { (StatKind.Atk, 5, 2), (StatKind.Spd, 5, 2) }, Text = "An ally within 2: ATK +5 and SPD +5 for 2 turns." },
        // Thornstag (Guardian + Skirmisher, needs antlers).
        new() { Id = "antler_sweep", Name = "Antler Sweep", Class = "Thornstag", Cost = 30, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 0.9f, Knock = 1, Text = "Rake every adjacent foe (x0.9) and throw them back." },
        new() { Id = "crown_of_thorns", Name = "Crown of Thorns", Class = "Thornstag", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Thorns, 5, 3), (StatKind.Def, 2, 3) }, Text = "Melee attackers take 5; DEF +2 for 3 turns." },
        new() { Id = "rutting_charge", Name = "Rutting Charge", Class = "Thornstag", Cost = 35, Cooldown = 3, Range = 4, Leap = true, Power = 1.3f, StunChance = 0.3f, Text = "Charge a foe up to 4 tiles away (x1.3), 30% to stun." },
        // Lionheart (Howler + Guardian, needs a mane).
        new() { Id = "roar_of_courage", Name = "Roar of Courage", Class = "Lionheart", Cost = 30, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 2, Cleanse = true, Buffs = new[] { (StatKind.Atk, 2, 3) }, Text = "You and allies within 2: cleansed, ATK +2 for 3 turns." },
        new() { Id = "challenge_roar", Name = "Challenge Roar", Class = "Lionheart", Cost = 30, Cooldown = 3, Range = 3, Taunt = true, Buffs = new[] { (StatKind.Atk, -2, 2) }, Text = "Range 3: the foe must come for you, ATK -2." },
        new() { Id = "last_stand", Name = "Last Stand", Class = "Lionheart", Cost = 35, Cooldown = 3, Power = 1.1f, Desperate = 0.9f, Text = "x1.1, or x2.0 when you're below half HP." },
        // Geomancer (Mystic + Guardian, needs back crystals).
        new() { Id = "crystal_lance", Name = "Crystal Lance", Class = "Geomancer", Cost = 30, Cooldown = 2, Range = 4, Straight = true, Power = 1.3f, Text = "A spear of crystal down a straight line, range 4 (x1.3)." },
        new() { Id = "prism_ward", Name = "Prism Ward", Class = "Geomancer", Cost = 30, Cooldown = 3, Target = TargetKind.Ally, Range = 3, Buffs = new[] { (StatKind.Def, 3, 3), (StatKind.Thorns, 3, 3) }, Text = "An ally within 3: DEF +3, and melee attackers take 3." },
        new() { Id = "shardstorm", Name = "Shardstorm", Class = "Geomancer", Cost = 35, Cooldown = 4, Target = TargetKind.Area, Range = 4, Radius = 2, Power = 0.6f, Text = "Crystal shards over everything within 2 of a tile at range 4 (x0.6)." },
        // Bonebreaker (Brawler + Rogue, needs a club tail).
        new() { Id = "crushing_blow", Name = "Crushing Blow", Class = "Bonebreaker", Cost = 30, Cooldown = 2, Power = 1.3f, Buffs = new[] { (StatKind.Def, -3, 3) }, Text = "x1.3, and DEF -3 for 3 turns." },
        new() { Id = "tail_sweep", Name = "Tail Sweep", Class = "Bonebreaker", Cost = 30, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 0.7f, Knock = 1, Text = "Every adjacent foe (x0.7), knocked back." },
        new() { Id = "skull_crack", Name = "Skull Crack", Class = "Bonebreaker", Cost = 35, Cooldown = 3, Power = 1f, StunChance = 0.5f, Text = "x1.0 with a 50% chance to stun." },
        // Swarmlord (Alchemist + Hunter, needs insect wings).
        new() { Id = "sting_volley", Name = "Sting Volley", Class = "Swarmlord", Cost = 30, Cooldown = 3, Target = TargetKind.Area, Range = 4, Radius = 1, Power = 0.4f, Poison = 3, PoisonTurns = 3, Text = "Stingers across a cross at range 4 (x0.4): poison 3 a turn." },
        new() { Id = "drone", Name = "Drone", Class = "Swarmlord", Cost = 30, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Buffs = new[] { (StatKind.Spd, -3, 2), (StatKind.Evade, -10, 2) }, Text = "A maddening buzz: foes within 2 lose SPD 3 and are easier to hit." },
        new() { Id = "hive_mind", Name = "Hive Mind", Class = "Swarmlord", Cost = 35, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 2, Buffs = new[] { (StatKind.Spd, 3, 2), (StatKind.Evade, 10, 2) }, Text = "You and allies within 2: SPD +3 and harder to hit for 2 turns." },
        // Seer (Mystic + Hunter, needs compound eyes).
        new() { Id = "foresight", Name = "Foresight", Class = "Seer", Cost = 30, Cooldown = 3, Target = TargetKind.Ally, Range = 3, Buffs = new[] { (StatKind.Evade, 30, 2) }, Text = "An ally within 3 sees blows coming: 30% harder to hit for 2 turns." },
        new() { Id = "true_sight", Name = "True Sight", Class = "Seer", Cost = 30, Cooldown = 3, Range = 5, Buffs = new[] { (StatKind.Evade, -30, 3), (StatKind.Def, -2, 3) }, Text = "Range 5: the foe can't hide: 30% easier to hit, DEF -2." },
        new() { Id = "omen_bolt", Name = "Omen Bolt", Class = "Seer", Cost = 35, Cooldown = 2, Range = 5, Power = 1.2f, HitBonus = 40, Text = "A bolt that finds its mark, range 5 (x1.2, +40% to hit)." },
        // Sunsail (Skirmisher + Howler, needs a back sail).
        new() { Id = "tailwind", Name = "Tailwind", Class = "Sunsail", Cost = 30, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 2, Buffs = new[] { (StatKind.Move, 2, 2) }, Text = "You and allies within 2: MOVE +2 for 2 turns." },
        new() { Id = "heat_shimmer", Name = "Heat Shimmer", Class = "Sunsail", Cost = 30, Cooldown = 3, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Evade, 20, 3), (StatKind.Spd, 2, 3) }, Text = "Shimmering air: 20% harder to hit, SPD +2 for 3 turns." },
        new() { Id = "solar_flare", Name = "Solar Flare", Class = "Sunsail", Cost = 35, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Power = 0.6f, StunChance = 0.25f, Text = "A blinding flash: foes within 2 (x0.6), 25% to stun." },
        // Cavalier (Skirmisher + Guardian, needs hooves).
        new() { Id = "lance_charge", Name = "Lance Charge", Class = "Cavalier", Cost = 30, Cooldown = 3, Range = 4, Leap = true, Power = 1.5f, Text = "Gallop onto a foe up to 4 tiles away (x1.5)." },
        new() { Id = "stampede", Name = "Stampede", Class = "Cavalier", Cost = 30, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 0.7f, Knock = 2, Text = "Rear and kick: adjacent foes (x0.7) thrown back 2." },
        new() { Id = "steadfast", Name = "Steadfast", Class = "Cavalier", Cost = 35, Cooldown = 4, Target = TargetKind.Self, Range = 0, Cleanse = true, Buffs = new[] { (StatKind.Def, 4, 3), (StatKind.Thorns, 2, 3) }, Text = "Cleansed; DEF +4 and thorns 2 for 3 turns." },
        // Scavenger (Rogue + Hunter, needs a curious temperament).
        new() { Id = "snatch", Name = "Snatch", Class = "Scavenger", Cost = 30, Cooldown = 3, Range = 2, Power = 0.4f, Steal = true, Pull = 1, Text = "Range 2: grab the foe's boosts (x0.4) and drag it a tile closer." },
        new() { Id = "scrounge", Name = "Scrounge", Class = "Scavenger", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Heal = 0.3f, Cleanse = true, Text = "Eat something off the floor: recover 30% and cleanse." },
        new() { Id = "junk_toss", Name = "Junk Toss", Class = "Scavenger", Cost = 35, Cooldown = 2, Range = 3, Power = 0.7f, Buffs = new[] { (StatKind.Spd, -3, 2) }, Text = "Range 3 (x0.7): SPD -3 for 2 turns." },
        // Phantom (Skirmisher + Rogue, needs a skittish temperament).
        new() { Id = "fade", Name = "Fade", Class = "Phantom", Cost = 30, Cooldown = 3, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Evade, 40, 1), (StatKind.Move, 2, 1) }, Text = "Fade out: 40% harder to hit and MOVE +2 until your next turn." },
        new() { Id = "feint", Name = "Feint", Class = "Phantom", Cost = 30, Cooldown = 3, Power = 0.6f, StunChance = 0.4f, HitAndRun = true, Text = "A false blow (x0.6), 40% to stun; you may move again." },
        new() { Id = "ghost_step", Name = "Ghost Step", Class = "Phantom", Cost = 35, Cooldown = 3, Target = TargetKind.Tile, Range = 5, Text = "Vanish and reappear on any free tile within 5." },
        // Champion (Brawler + Howler, needs a proud temperament).
        new() { Id = "challenge", Name = "Challenge", Class = "Champion", Cost = 30, Cooldown = 3, Range = 3, Taunt = true, Buffs = new[] { (StatKind.Def, -3, 2) }, Text = "Range 3: the foe must face you, DEF -3." },
        new() { Id = "duel_stance", Name = "Duel Stance", Class = "Champion", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Buffs = new[] { (StatKind.Atk, 3, 3), (StatKind.Evade, 15, 3) }, Text = "ATK +3 and 15% harder to hit for 3 turns." },
        new() { Id = "finishing_blow", Name = "Finishing Blow", Class = "Champion", Cost = 35, Cooldown = 3, Power = 1f, Execute = 1.2f, Text = "x1.0, or x2.2 against a foe below 40% HP." },
        // Hydrarch (Brawler + Howler, needs the hydra form).
        new() { Id = "hydra_snap", Name = "Hydra Snap", Class = "Hydrarch", Cost = 30, Cooldown = 2, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 1, Power = 0.8f, Poison = 2, PoisonTurns = 2, Text = "Every head bites: each adjacent foe (x0.8) and poison 2 a turn for 2 turns." },
        new() { Id = "regrow", Name = "Regrow", Class = "Hydrarch", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Heal = 0.3f, Cleanse = true, Text = "Grow back what was lost: recover 30% of max HP and cleanse." },
        new() { Id = "chorus_roar", Name = "Chorus Roar", Class = "Hydrarch", Cost = 35, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, StunChance = 0.2f, Buffs = new[] { (StatKind.Atk, -2, 2), (StatKind.Def, -2, 2) }, Text = "All its heads roar: foes within 2 get ATK -2 and DEF -2, 20% chance to stun." },
        // Stoneguard (Guardian + Mystic, needs the gargoyle form).
        new() { Id = "turn_to_stone", Name = "Turn to Stone", Class = "Stoneguard", Cost = 30, Cooldown = 4, Target = TargetKind.Self, Range = 0, Cleanse = true, Buffs = new[] { (StatKind.Def, 8, 2), (StatKind.Spd, -4, 2) }, Text = "Become stone: cleansed, DEF +8 and SPD -4 for 2 turns." },
        new() { Id = "plunge", Name = "Plunge", Class = "Stoneguard", Cost = 30, Cooldown = 3, Range = 4, Leap = true, Power = 1.3f, HighGround = 0.6f, Text = "Drop on a foe up to 4 tiles away (x1.3, x1.9 from higher ground)." },
        new() { Id = "grotesque_glare", Name = "Grotesque Glare", Class = "Stoneguard", Cost = 35, Cooldown = 3, Range = 3, StunChance = 0.45f, Buffs = new[] { (StatKind.Def, -2, 2) }, Text = "Range 3: a 45% chance the foe freezes in fright, and DEF -2." },
        // Coilbinder (Rogue + Mystic, needs the naga form).
        new() { Id = "constrict", Name = "Constrict", Class = "Coilbinder", Cost = 30, Cooldown = 3, Power = 0.7f, Root = true, Buffs = new[] { (StatKind.Spd, -3, 2) }, Text = "Coil round a foe (x0.7): rooted, and SPD -3 for 2 turns." },
        new() { Id = "hypnotic_sway", Name = "Hypnotic Sway", Class = "Coilbinder", Cost = 30, Cooldown = 3, Range = 3, StunChance = 0.55f, HitBonus = 10, Text = "Range 3: a 55% chance the foe loses its next turn, swaying with it." },
        new() { Id = "serpents_embrace", Name = "Serpent's Embrace", Class = "Coilbinder", Cost = 35, Cooldown = 3, Range = 2, Pull = 1, Power = 0.9f, Drain = 0.4f, Text = "Range 2: pull a foe in (x0.9) and heal for 40% of the damage." },
        // Venomtail (Alchemist + Hunter, needs the scorpion form).
        new() { Id = "sting", Name = "Sting", Class = "Venomtail", Cost = 30, Cooldown = 2, Range = 2, Straight = true, Power = 1f, Poison = 5, PoisonTurns = 3, Text = "Strike over the back, range 2 in a line (x1): poison 5 a turn for 3 turns." },
        new() { Id = "pincer_grip", Name = "Pincer Grip", Class = "Venomtail", Cost = 30, Cooldown = 3, Power = 0.8f, Root = true, Buffs = new[] { (StatKind.Def, -2, 2) }, Text = "Clamp a foe (x0.8): rooted, DEF -2 for 2 turns." },
        new() { Id = "neurotoxin", Name = "Neurotoxin", Class = "Venomtail", Cost = 35, Cooldown = 3, Range = 2, Power = 0.5f, StunChance = 0.4f, Poison = 3, PoisonTurns = 2, Text = "Range 2 (x0.5): 40% to stun, poison 3 a turn for 2 turns." },
        // Lurelight (Hunter + Mystic, needs the angler form).
        new() { Id = "lure", Name = "Lure", Class = "Lurelight", Cost = 30, Cooldown = 3, Range = 4, Pull = 3, Buffs = new[] { (StatKind.Def, -2, 2) }, Text = "Range 4: the light draws a foe 3 tiles closer; DEF -2 for 2 turns." },
        new() { Id = "abyssal_maw", Name = "Abyssal Maw", Class = "Lurelight", Cost = 30, Cooldown = 3, Power = 1.7f, HitBonus = -5, HealOnKill = 0.3f, Text = "A gulping bite (x1.7); a kill heals 30% of max HP." },
        new() { Id = "glow_flash", Name = "Glow Flash", Class = "Lurelight", Cost = 35, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, StunChance = 0.3f, Buffs = new[] { (StatKind.Evade, -15, 2) }, Text = "A flash in the dark: foes within 2 are 15% easier to hit, 30% to stun." },
        // Oculus (Mystic + Hunter, needs the watcher form).
        new() { Id = "gaze_beam", Name = "Gaze Beam", Class = "Oculus", Cost = 30, Cooldown = 2, Range = 5, Straight = true, Power = 1.3f, HitBonus = 20, Text = "A beam from the great eye down a straight line, range 5 (x1.3, +20 hit)." },
        new() { Id = "petrifying_stare", Name = "Petrifying Stare", Class = "Oculus", Cost = 30, Cooldown = 3, Range = 4, Root = true, StunChance = 0.25f, Buffs = new[] { (StatKind.Spd, -4, 2) }, Text = "Range 4: rooted, SPD -4, and a 25% chance to stun." },
        new() { Id = "inner_eye", Name = "Inner Eye", Class = "Oculus", Cost = 35, Cooldown = 4, Target = TargetKind.Ally, Range = 3, Buffs = new[] { (StatKind.Evade, 25, 2), (StatKind.Atk, 2, 2) }, Text = "An ally within 3 sees what's coming: 25% harder to hit and ATK +2 for 2 turns." },
        // Saint (Howler + Mystic, needs a halo).
        new() { Id = "benediction", Name = "Benediction", Class = "Saint", Cost = 30, Cooldown = 3, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 2, Heal = 0.2f, Cleanse = true, Text = "You and allies within 2 recover 20% of max HP and are cleansed." },
        new() { Id = "radiance", Name = "Radiance", Class = "Saint", Cost = 30, Cooldown = 3, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Power = 0.7f, Buffs = new[] { (StatKind.Evade, -10, 2) }, Text = "The halo blazes: foes within 2 (x0.7), 10% easier to hit for 2 turns." },
        new() { Id = "sanctuary", Name = "Sanctuary", Class = "Saint", Cost = 35, Cooldown = 4, Target = TargetKind.Ally, Range = 3, Buffs = new[] { (StatKind.Def, 5, 2), (StatKind.Thorns, 2, 2) }, Text = "An ally within 3: DEF +5, and melee attackers take 2, for 2 turns." },
        // Hexmark (Mystic + Tinker, needs a sigil).
        new() { Id = "brand", Name = "Brand", Class = "Hexmark", Cost = 30, Cooldown = 3, Range = 4, HitBonus = 15, Buffs = new[] { (StatKind.Def, -4, 3), (StatKind.Evade, -15, 3) }, Text = "Range 4: burn the sigil onto a foe: DEF -4 and 15% easier to hit for 3 turns." },
        new() { Id = "sigil_burst", Name = "Sigil Burst", Class = "Hexmark", Cost = 30, Cooldown = 3, Target = TargetKind.Area, Range = 4, Radius = 1, Power = 0.9f, Text = "The sigil detonates across a cross at range 4 (x0.9)." },
        new() { Id = "warding_circle", Name = "Warding Circle", Class = "Hexmark", Cost = 35, Cooldown = 4, Target = TargetKind.Ally, Range = 0, AroundSelf = true, Radius = 1, Buffs = new[] { (StatKind.Def, 4, 2), (StatKind.Evade, 15, 2) }, Text = "You and adjacent allies: DEF +4 and 15% harder to hit for 2 turns." },
        // Basilisk (Brawler + Howler, needs the cockatrice form).
        new() { Id = "stone_peck", Name = "Stone Peck", Class = "Basilisk", Cost = 30, Cooldown = 3, Power = 1f, StunChance = 0.35f, Buffs = new[] { (StatKind.Spd, -2, 2) }, Text = "A peck that turns flesh to stone (x1): 35% to stun, SPD -2 for 2 turns." },
        new() { Id = "serpent_lash", Name = "Serpent Lash", Class = "Basilisk", Cost = 25, Cooldown = 2, Range = 2, Straight = true, Power = 0.9f, Knock = 1, Text = "The serpent tail whips round, range 2 in a line (x0.9): knocked back a tile." },
        new() { Id = "dread_crow", Name = "Dread Crow", Class = "Basilisk", Cost = 35, Cooldown = 4, Target = TargetKind.Enemy, Range = 0, AroundSelf = true, Radius = 2, Buffs = new[] { (StatKind.Atk, -2, 2), (StatKind.Def, -1, 2) }, Text = "A crow that chills the blood: foes within 2 get ATK -2 and DEF -1 for 2 turns." },
    };

    public static readonly ClassDef[] Classes =
    {
        // Base classes: open to every creature its body allows.
        new() { Id = "Brawler", Name = "Brawler", Blurb = "Fights up close.", Atk = 2, Hp = 2, Skills = new[] { "heavy_blow", "rend", "frenzy" } },
        new() { Id = "Skirmisher", Name = "Skirmisher", Blurb = "Hits and runs.", Spd = 2, Move = 1, Skills = new[] { "quick_strike", "pounce", "evasion" } },
        new() { Id = "Guardian", Name = "Guardian", Blurb = "Holds the line.", Def = 2, Hp = 6, Spd = -1, Skills = new[] { "shield_wall", "taunt", "second_wind" } },
        new() { Id = "Mystic", Name = "Mystic", Blurb = "Channels hextech at range.", Atk = 1, Def = -1, Skills = new[] { "hex_bolt", "ward", "arc_storm" } },
        new() { Id = "Alchemist", Name = "Alchemist", Blurb = "Brews chemtech: poison and mending.", Spd = 1, Skills = new[] { "toxic_flask", "mend", "acid_splash" } },
        new()
        {
            Id = "Tinker", Name = "Tinker", Blurb = "Clockwork tricks: boosts, snares, shocks.", Def = 1, Spd = 1, Skills = new[] { "overclock", "snare", "shock_prod" },
            NeedsPart = Hands, PartHint = "hands (a biped, or claws)",
        },
        new() { Id = "Hunter", Name = "Hunter", Blurb = "Quills, spit and stones from a distance.", Spd = 1, Atk = 1, Skills = new[] { "quill_shot", "pin_down", "volley" } },
        new() { Id = "Howler", Name = "Howler", Blurb = "A voice that rallies the pack and rattles foes.", Hp = 3, Spd = 1, Skills = new[] { "war_cry", "screech", "lullaby" } },
        new() { Id = "Rogue", Name = "Rogue", Blurb = "Fights dirty: flanks, smoke, theft.", Spd = 2, Skills = new[] { "backstab", "smoke_bomb", "pilfer" } },

        // Part classes: two base classes mastered, and the body for it.
        new()
        {
            Id = "Skydiver", Name = "Skydiver", Blurb = "Strikes from the sky.", Atk = 2, Spd = 2, Move = 1,
            Skills = new[] { "dive_bomb", "gale", "updraft" }, Requires = new[] { ("Brawler", 2), ("Skirmisher", 2) },
            NeedsPart = c => c.Parts.Wings is Wings.Bat or Wings.Feather or Wings.Insect, PartHint = "wings",
        },
        new()
        {
            Id = "Juggernaut", Name = "Juggernaut", Blurb = "An unstoppable charge.", Atk = 2, Def = 3, Hp = 8, Spd = -1,
            Skills = new[] { "trample", "iron_hide", "earthshaker" }, Requires = new[] { ("Brawler", 2), ("Guardian", 2) },
            NeedsPart = c => c.Weapon == Weapon.Horns, PartHint = "horns",
        },
        new()
        {
            Id = "Bulwark", Name = "Bulwark", Blurb = "An immovable wall of shell.", Def = 4, Hp = 8, Spd = -2,
            Skills = new[] { "shell_up", "spiked_rampart", "haul" }, Requires = new[] { ("Guardian", 2), ("Alchemist", 2) },
            NeedsPart = c => c.Parts.Back == Back.Shell, PartHint = "a shell",
        },
        new()
        {
            Id = "Lasher", Name = "Lasher", Blurb = "Reach and control with a whip of a tail.", Atk = 2, Spd = 1,
            Skills = new[] { "crack", "snare_lash", "whirl" }, Requires = new[] { ("Brawler", 2), ("Tinker", 2) },
            NeedsPart = c => c.Parts.Tail == Tail.Whip, PartHint = "a whip tail",
        },
        new()
        {
            Id = "Stalker", Name = "Stalker", Blurb = "Waits above, drops on prey.", Atk = 3, Spd = 1,
            Skills = new[] { "ambush", "venom_claws", "shadow_step" }, Requires = new[] { ("Rogue", 2), ("Alchemist", 2) },
            NeedsPart = c => c.Parts.Limbs == Limbs.Clawed && c.Species.Plan is not (BodyPlan.Serpent or BodyPlan.Aquatic), PartHint = "claws",
        },
        new()
        {
            Id = "Leviathan", Name = "Leviathan", Blurb = "Owns the water and drags foes into it.", Hp = 8, Def = 2, Atk = 1,
            Skills = new[] { "tidal_crash", "undertow", "deep_shell" }, Requires = new[] { ("Guardian", 2), ("Mystic", 2) },
            NeedsPart = c => c.MoveType == MoveType.Swim, PartHint = "a swimmer's body (serpent or aquatic)",
        },
        new()
        {
            Id = "Titan", Name = "Titan", Blurb = "A living siege engine.", Hp = 12, Atk = 2, Def = 2, Spd = -2,
            Skills = new[] { "stomp", "shrug_off", "toss" }, Requires = new[] { ("Brawler", 2), ("Guardian", 2) },
            NeedsPart = c => c.Size == SizeClass.Huge, PartHint = "huge size",
        },
        new()
        {
            Id = "Trickster", Name = "Trickster", Blurb = "Too small to catch, too strange to trust.", Spd = 3, Move = 1,
            Skills = new[] { "blink", "vanish", "hex_prank" }, Requires = new[] { ("Skirmisher", 2), ("Mystic", 2) },
            NeedsPart = c => c.Size == SizeClass.Tiny, PartHint = "tiny size",
        },
        new()
        {
            Id = "Packleader", Name = "Packleader", Blurb = "The one the rest follow.", Atk = 2, Hp = 4,
            Skills = new[] { "rally", "pack_tactics", "alpha_howl" }, Requires = new[] { ("Howler", 2), ("Brawler", 2) },
            NeedsPart = c => c.Attitude is Attitude.Loyal or Attitude.Aggressive, PartHint = "a loyal or aggressive temperament",
        },
        new()
        {
            Id = "Deadeye", Name = "Deadeye", Blurb = "Never misses what it marks.", Atk = 2, Spd = 1,
            Skills = new[] { "headshot", "mark", "ricochet" }, Requires = new[] { ("Hunter", 2), ("Tinker", 2) },
            NeedsPart = c => c.Species.Grafts.HasFlag(Graft.Visor) || c.Species.Grafts.HasFlag(Graft.ExtraEyes), PartHint = "a lens visor or extra eyes",
        },
        new()
        {
            Id = "Thornstag", Name = "Thornstag", Blurb = "Antlers like a hedge of blades.", Def = 2, Atk = 1, Hp = 4,
            Skills = new[] { "antler_sweep", "crown_of_thorns", "rutting_charge" }, Requires = new[] { ("Guardian", 2), ("Skirmisher", 2) },
            NeedsPart = c => c.Parts.Horns == Horns.Antlers, PartHint = "antlers",
        },
        new()
        {
            Id = "Lionheart", Name = "Lionheart", Blurb = "A great mane, a greater heart.", Hp = 6, Atk = 1, Def = 1,
            Skills = new[] { "roar_of_courage", "challenge_roar", "last_stand" }, Requires = new[] { ("Howler", 2), ("Guardian", 2) },
            NeedsPart = c => c.Parts.Back == Back.Mane, PartHint = "a mane",
        },
        new()
        {
            Id = "Geomancer", Name = "Geomancer", Blurb = "Sings to the crystal growing from its back.", Atk = 2, Def = 1,
            Skills = new[] { "crystal_lance", "prism_ward", "shardstorm" }, Requires = new[] { ("Mystic", 2), ("Guardian", 2) },
            NeedsPart = c => c.Parts.Back == Back.Crystals, PartHint = "back crystals",
        },
        new()
        {
            Id = "Bonebreaker", Name = "Bonebreaker", Blurb = "Swings its tail like a mace.", Atk = 3,
            Skills = new[] { "crushing_blow", "tail_sweep", "skull_crack" }, Requires = new[] { ("Brawler", 2), ("Rogue", 2) },
            NeedsPart = c => c.Parts.Tail == Tail.Club, PartHint = "a club tail",
        },
        new()
        {
            Id = "Swarmlord", Name = "Swarmlord", Blurb = "Commands a cloud of stinging things.", Spd = 2, Atk = 1,
            Skills = new[] { "sting_volley", "drone", "hive_mind" }, Requires = new[] { ("Alchemist", 2), ("Hunter", 2) },
            NeedsPart = c => c.Parts.Wings == Wings.Insect, PartHint = "insect wings",
        },
        new()
        {
            Id = "Seer", Name = "Seer", Blurb = "Sees the blow before it falls.", Atk = 1, Spd = 2,
            Skills = new[] { "foresight", "true_sight", "omen_bolt" }, Requires = new[] { ("Mystic", 2), ("Hunter", 2) },
            NeedsPart = c => c.Parts.Eyes == EyeKind.Compound, PartHint = "compound eyes",
        },
        new()
        {
            Id = "Sunsail", Name = "Sunsail", Blurb = "Catches wind and light in the sail on its back.", Spd = 2, Move = 1,
            Skills = new[] { "tailwind", "heat_shimmer", "solar_flare" }, Requires = new[] { ("Skirmisher", 2), ("Howler", 2) },
            NeedsPart = c => c.Parts.Back == Back.Sail, PartHint = "a back sail",
        },
        new()
        {
            Id = "Cavalier", Name = "Cavalier", Blurb = "Hooves made for the charge.", Atk = 1, Def = 2, Move = 1,
            Skills = new[] { "lance_charge", "stampede", "steadfast" }, Requires = new[] { ("Skirmisher", 2), ("Guardian", 2) },
            NeedsPart = c => c.Parts.Limbs == Limbs.Hooved && c.Species.Plan is BodyPlan.Quadruped or BodyPlan.Biped, PartHint = "hooves",
        },
        new()
        {
            Id = "Scavenger", Name = "Scavenger", Blurb = "Takes anything not nailed down.", Spd = 2,
            Skills = new[] { "snatch", "scrounge", "junk_toss" }, Requires = new[] { ("Rogue", 2), ("Hunter", 2) },
            NeedsPart = c => c.Attitude == Attitude.Curious, PartHint = "a curious temperament",
        },
        new()
        {
            Id = "Phantom", Name = "Phantom", Blurb = "Never where you struck.", Spd = 3,
            Skills = new[] { "fade", "feint", "ghost_step" }, Requires = new[] { ("Skirmisher", 2), ("Rogue", 2) },
            NeedsPart = c => c.Attitude == Attitude.Skittish, PartHint = "a skittish temperament",
        },
        new()
        {
            Id = "Champion", Name = "Champion", Blurb = "Fights alone, and wins.", Atk = 3, Hp = 4,
            Skills = new[] { "challenge", "duel_stance", "finishing_blow" }, Requires = new[] { ("Brawler", 2), ("Howler", 2) },
            NeedsPart = c => c.Attitude == Attitude.Proud, PartHint = "a proud temperament",
        },
        new()
        {
            Id = "Hexwarden", Name = "Hexwarden", Blurb = "Binds hextech and clockwork.", Atk = 2, Def = 1,
            Skills = new[] { "hex_cage", "overcharge", "nova" }, Requires = new[] { ("Mystic", 2), ("Tinker", 2) },
        },

        // Form and aura classes: two base classes mastered, and the shape or the mark for it.
        new()
        {
            Id = "Hydrarch", Name = "Hydrarch", Blurb = "Every head hungry, every head awake.", Atk = 2, Hp = 6,
            Skills = new[] { "hydra_snap", "regrow", "chorus_roar" }, Requires = new[] { ("Brawler", 2), ("Howler", 2) },
            NeedsPart = c => Species.FormOf(c.Species) == Form.Hydra, PartHint = "the hydra's form",
        },
        new()
        {
            Id = "Stoneguard", Name = "Stoneguard", Blurb = "Perched, watching, as still as the stone.", Def = 3, Hp = 4, Spd = -1,
            Skills = new[] { "turn_to_stone", "plunge", "grotesque_glare" }, Requires = new[] { ("Guardian", 2), ("Mystic", 2) },
            NeedsPart = c => Species.FormOf(c.Species) == Form.Gargoyle, PartHint = "the gargoyle's form",
        },
        new()
        {
            Id = "Coilbinder", Name = "Coilbinder", Blurb = "Holds the eye, then holds the body.", Spd = 2, Atk = 1,
            Skills = new[] { "constrict", "hypnotic_sway", "serpents_embrace" }, Requires = new[] { ("Rogue", 2), ("Mystic", 2) },
            NeedsPart = c => Species.FormOf(c.Species) == Form.Naga, PartHint = "the naga's form",
        },
        new()
        {
            Id = "Venomtail", Name = "Venomtail", Blurb = "The tail curls, and waits.", Atk = 2, Spd = 1,
            Skills = new[] { "sting", "pincer_grip", "neurotoxin" }, Requires = new[] { ("Alchemist", 2), ("Hunter", 2) },
            NeedsPart = c => Species.FormOf(c.Species) == Form.Scorpion, PartHint = "the scorpion's form",
        },
        new()
        {
            Id = "Lurelight", Name = "Lurelight", Blurb = "A small light in the dark, and a mouth behind it.", Atk = 2, Hp = 4,
            Skills = new[] { "lure", "abyssal_maw", "glow_flash" }, Requires = new[] { ("Hunter", 2), ("Mystic", 2) },
            NeedsPart = c => Species.FormOf(c.Species) == Form.Angler, PartHint = "the angler's form",
        },
        new()
        {
            Id = "Oculus", Name = "Oculus", Blurb = "One eye, and it misses nothing.", Atk = 2, Spd = 1,
            Skills = new[] { "gaze_beam", "petrifying_stare", "inner_eye" }, Requires = new[] { ("Mystic", 2), ("Hunter", 2) },
            NeedsPart = c => Species.FormOf(c.Species) == Form.Watcher, PartHint = "the watcher's form",
        },
        new()
        {
            Id = "Saint", Name = "Saint", Blurb = "The halo heals what it lights, and burns the rest.", Hp = 4, Def = 1, Atk = 1,
            Skills = new[] { "benediction", "radiance", "sanctuary" }, Requires = new[] { ("Howler", 2), ("Mystic", 2) },
            NeedsPart = c => Species.AuraOf(c.Species) == Aura.Halo, PartHint = "a halo",
        },
        new()
        {
            Id = "Hexmark", Name = "Hexmark", Blurb = "Its sigil brands whatever it looks at.", Atk = 2, Def = 1,
            Skills = new[] { "brand", "sigil_burst", "warding_circle" }, Requires = new[] { ("Mystic", 2), ("Tinker", 2) },
            NeedsPart = c => Species.AuraOf(c.Species) == Aura.Sigil, PartHint = "a sigil",
        },
        new()
        {
            Id = "Basilisk", Name = "Basilisk", Blurb = "Half bird, half serpent, and its peck turns flesh to stone.", Atk = 2, Spd = 1,
            Skills = new[] { "stone_peck", "serpent_lash", "dread_crow" }, Requires = new[] { ("Brawler", 2), ("Howler", 2) },
            NeedsPart = c => Species.FormOf(c.Species) == Form.Cockatrice, PartHint = "the cockatrice's form",
        },

        // Blood classes: one base class mastered, and the strain in its veins.
        new()
        {
            Id = "Chemist", Name = "Chemist", Blurb = "Doses allies with volatile chem.", Spd = 2, Atk = 1,
            Skills = new[] { "shimmer_dose", "caustic_cloud", "mutagen" }, Requires = new[] { ("Alchemist", 2) },
            NeedsPart = c => c.Species.Strain == Strain.Chemtech, PartHint = "chemtech blood",
        },
        new()
        {
            Id = "Runeweaver", Name = "Runeweaver", Blurb = "Writes glyphs that bind and shield.", Def = 1, Atk = 1,
            Skills = new[] { "glyph_snare", "glyph_shield", "arcane_surge" }, Requires = new[] { ("Mystic", 2) },
            NeedsPart = c => c.Species.Strain == Strain.Arcane, PartHint = "arcane blood",
        },
        new()
        {
            Id = "Voidcaller", Name = "Voidcaller", Blurb = "Feeds on fear.", Atk = 3, Hp = 4,
            Skills = new[] { "dread", "siphon", "maw" }, Requires = new[] { ("Brawler", 2) },
            NeedsPart = c => c.Species.Strain == Strain.Abyssal, PartHint = "abyssal blood",
        },
        new()
        {
            Id = "Artificer", Name = "Artificer", Blurb = "A brass engineer, half machine itself.", Def = 2, Spd = 1,
            Skills = new[] { "spark_coil", "repair", "overdrive" }, Requires = new[] { ("Tinker", 2) },
            NeedsPart = c => System.Numerics.BitOperations.PopCount((uint)(c.Species.Grafts & MachineGrafts)) >= 2, PartHint = "two machine grafts",
        },
    };

    const Graft MachineGrafts = Graft.Plating | Graft.MetalLimb | Graft.Visor | Graft.Core;

    /// <summary>Tinkering needs something to work a lever with.</summary>
    static bool Hands(Creature c) => c.Species.Plan == BodyPlan.Biped || c.Parts.Limbs == Limbs.Clawed && c.Species.Plan is not (BodyPlan.Serpent or BodyPlan.Aquatic);

    /// <summary>Each body plan's favoured base classes (+50% JP, stronger stat leanings) and barred ones.</summary>
    public static readonly Dictionary<BodyPlan, (string[] favoured, string[] barred)> Affinity = new()
    {
        [BodyPlan.Quadruped] = (new[] { "Brawler", "Skirmisher", "Howler" }, Array.Empty<string>()),
        [BodyPlan.Biped] = (new[] { "Guardian", "Tinker", "Rogue" }, Array.Empty<string>()),
        [BodyPlan.Serpent] = (new[] { "Mystic", "Alchemist" }, new[] { "Guardian" }),
        [BodyPlan.Avian] = (new[] { "Skirmisher", "Mystic", "Hunter" }, new[] { "Guardian" }),
        [BodyPlan.Insectoid] = (new[] { "Skirmisher", "Alchemist", "Hunter" }, new[] { "Mystic" }),
        [BodyPlan.Aquatic] = (new[] { "Mystic", "Guardian" }, new[] { "Skirmisher", "Rogue" }),
        [BodyPlan.Blob] = (new[] { "Guardian", "Alchemist" }, new[] { "Skirmisher", "Hunter" }),
    };

    public static bool Favoured(Creature c, string cls) => Affinity[c.Species.Plan].favoured.Contains(cls);
    public static bool Barred(Creature c, string cls) => Affinity[c.Species.Plan].barred.Contains(cls);

    public enum Standing { Open, Locked, Never }

    /// <summary>
    /// Where a class stands for a creature: open to train now; locked (skills or a part still to
    /// get, by training, breeding or the lab); or never, because it needs a class this body can't take.
    /// </summary>
    public static Standing StandingOf(ClassDef c, Creature creature, Training t)
    {
        if (Barred(creature, c.Id) || c.Requires.Any(r => Barred(creature, r.cls))) return Standing.Never;
        return Unlocked(c, creature, t) ? Standing.Open : Standing.Locked;
    }

    /// <summary>JP multiplier: a favoured class trains half again as fast.</summary>
    public static float JpRate(Creature c, string cls) => Favoured(c, cls) ? 1.5f : 1f;

    /// <summary>One passive per class, learned after its three skills.</summary>
    public static readonly PassiveDef[] Passives =
    {
        new() { Id = "brute_force", Name = "Brute Force", Class = "Brawler", Atk = 2, Text = "ATK +2." },
        new() { Id = "fleet", Name = "Fleet", Class = "Skirmisher", Move = 1, Text = "MOVE +1." },
        new() { Id = "thick_hide", Name = "Thick Hide", Class = "Guardian", Def = 2, HpPct = 0.1f, Text = "DEF +2, max HP +10%." },
        new() { Id = "focus", Name = "Focus", Class = "Mystic", CooldownCut = 1, Text = "Skills cool down a turn sooner." },
        new() { Id = "antidote", Name = "Antidote", Class = "Alchemist", NoPoison = true, Text = "Immune to poison." },
        new() { Id = "tuned", Name = "Tuned Reflexes", Class = "Tinker", Spd = 2, Hit = 5, Text = "SPD +2, +5% to hit." },
        new() { Id = "eagle_eye", Name = "Eagle Eye", Class = "Hunter", RangeBonus = 1, Hit = 5, Text = "Ranged skills reach 1 further; +5% to hit." },
        new() { Id = "echo", Name = "Echo", Class = "Howler", Echo = true, Text = "Boosts it gives last a turn longer." },
        new() { Id = "pickpocket", Name = "Pickpocket", Class = "Rogue", ChemBonus = 4, Evade = 5, Text = "+4 chem after every battle; 5% harder to hit." },
        new() { Id = "featherfall", Name = "Featherfall", Class = "Skydiver", Climb = 3, Text = "Drops and climbs ledges up to 3 high." },
        new() { Id = "unstoppable", Name = "Unstoppable", Class = "Juggernaut", Unstoppable = true, Text = "Can't be knocked back or rooted." },
        new() { Id = "spiked_shell", Name = "Spiked Shell", Class = "Bulwark", Thorns = 3, Def = 1, Text = "Melee attackers take 3; DEF +1." },
        new() { Id = "long_reach", Name = "Long Reach", Class = "Lasher", LongReach = true, Text = "Basic attacks reach 2 tiles in a line." },
        new() { Id = "predator", Name = "Predator", Class = "Stalker", Predator = 0.3f, Text = "+30% damage to foes under half HP." },
        new() { Id = "amphibious", Name = "Amphibious", Class = "Leviathan", Amphibious = true, Text = "Wades through chem; swimmers aren't slowed on land." },
        new() { Id = "colossal", Name = "Colossal", Class = "Titan", HpPct = 0.2f, Text = "Max HP +20%." },
        new() { Id = "slippery", Name = "Slippery", Class = "Trickster", Evade = 15, Text = "15% harder to hit." },
        new() { Id = "pack_bond", Name = "Pack Bond", Class = "Packleader", PackBond = true, Text = "ATK +1 for each ally beside it." },
        new() { Id = "steady_aim", Name = "Steady Aim", Class = "Deadeye", Hit = 15, Text = "+15% to hit." },
        new() { Id = "spell_mastery", Name = "Spell Mastery", Class = "Hexwarden", SkillPower = 0.15f, Text = "Skills deal 15% more damage." },
        new() { Id = "shimmer_blood", Name = "Shimmer Blood", Class = "Chemist", Regen = 0.08f, NoPoison = true, Text = "Heals 8% of max HP each turn; immune to poison." },
        new() { Id = "runic_skin", Name = "Runic Skin", Class = "Runeweaver", NoStun = true, Def = 1, Text = "Immune to stun; DEF +1." },
        new() { Id = "bloodthirst", Name = "Bloodthirst", Class = "Voidcaller", Drain = 0.25f, Text = "Basic attacks heal 25% of the damage." },
        new() { Id = "brass_frame", Name = "Brass Frame", Class = "Artificer", Def = 2, NoPoison = true, Text = "DEF +2; immune to poison." },
        // Reactions (FFTA's R-abilities): one equipped, set off when the creature is hit.
        new() { Id = "counter", Name = "Counter", Class = "Brawler", Kind = PassiveKind.Reaction, React = ReactionKind.Counter, ReactPower = 0.7f, Text = "Hit in melee: strike back (x0.7)." },
        new() { Id = "sidestep", Name = "Sidestep", Class = "Skirmisher", Kind = PassiveKind.Reaction, React = ReactionKind.Retreat, Text = "Hit: step a tile away from the attacker." },
        new() { Id = "bodyguard", Name = "Bodyguard", Class = "Guardian", Kind = PassiveKind.Reaction, React = ReactionKind.Guard, ReactChance = 0.35f, Text = "An adjacent ally is attacked: 35% chance to take the hit instead." },
        new() { Id = "spell_ward", Name = "Spell Ward", Class = "Mystic", Kind = PassiveKind.Reaction, React = ReactionKind.Brace, ReactAmount = 3, Text = "Hit: DEF +3 until its next turn." },
        new() { Id = "adrenal_gland", Name = "Adrenal Gland", Class = "Alchemist", Kind = PassiveKind.Reaction, React = ReactionKind.Adrenaline, ReactPower = 0.25f, Text = "First time below half HP: recover 25% at once." },
        new() { Id = "shock_plating", Name = "Shock Plating", Class = "Tinker", Kind = PassiveKind.Reaction, React = ReactionKind.Shock, ReactAmount = 3, ReactChance = 0.2f, Text = "Hit in melee: the attacker takes 3, 20% chance it's stunned." },
        new() { Id = "return_fire", Name = "Return Fire", Class = "Hunter", Kind = PassiveKind.Reaction, React = ReactionKind.ReturnFire, ReactPower = 0.6f, Text = "Hit from range (up to 4): shoot back (x0.6)." },
        new() { Id = "rallying_cry", Name = "Rallying Cry", Class = "Howler", Kind = PassiveKind.Reaction, React = ReactionKind.RallyCry, ReactAmount = 2, Text = "Hit: allies beside it get ATK +2 for a turn." },
        new() { Id = "dirty_retort", Name = "Dirty Retort", Class = "Rogue", Kind = PassiveKind.Reaction, React = ReactionKind.DirtyRetort, ReactChance = 0.3f, Text = "Hit in melee: 30% chance to stun the attacker." },
        // New classes' passives.
        new() { Id = "hedge_of_antlers", Name = "Hedge of Antlers", Class = "Thornstag", Thorns = 2, Def = 1, Text = "Melee attackers take 2; DEF +1." },
        new() { Id = "protector", Name = "Protector", Class = "Lionheart", Kind = PassiveKind.Reaction, React = ReactionKind.Protector, ReactAmount = 2, Text = "An ally beside it is hit: ATK +2 for 2 turns." },
        new() { Id = "shard_burst", Name = "Shard Burst", Class = "Geomancer", Kind = PassiveKind.Reaction, React = ReactionKind.Burst, ReactAmount = 3, Text = "Hit in melee: every foe beside it takes 3." },
        new() { Id = "heavy_swing", Name = "Heavy Swing", Class = "Bonebreaker", Atk = 3, Spd = -1, Text = "ATK +3, SPD -1." },
        new() { Id = "swarm", Name = "Swarm", Class = "Swarmlord", Evade = 10, Spd = 1, Text = "10% harder to hit; SPD +1." },
        new() { Id = "premonition", Name = "Premonition", Class = "Seer", Kind = PassiveKind.Reaction, React = ReactionKind.Dodge, ReactChance = 0.25f, Text = "A hit that would land: 25% chance it doesn't." },
        new() { Id = "sunbask", Name = "Sunbask", Class = "Sunsail", Regen = 0.05f, Spd = 1, Text = "Heals 5% of max HP each turn; SPD +1." },
        new() { Id = "brace", Name = "Brace", Class = "Cavalier", Kind = PassiveKind.Reaction, React = ReactionKind.Brace, ReactAmount = 4, Text = "Hit: DEF +4 until its next turn." },
        new() { Id = "magpie", Name = "Magpie", Class = "Scavenger", ChemBonus = 6, Text = "+6 chem after every battle." },
        new() { Id = "blur", Name = "Blur", Class = "Phantom", Kind = PassiveKind.Reaction, React = ReactionKind.Retreat, ReactAmount = 15, Text = "Hit: slip a tile away and become 15% harder to hit." },
        new() { Id = "riposte", Name = "Riposte", Class = "Champion", Kind = PassiveKind.Reaction, React = ReactionKind.Counter, ReactPower = 0.9f, Text = "Hit in melee: strike back (x0.9)." },
        // Form and aura classes.
        new() { Id = "hydra_blood", Name = "Hydra Blood", Class = "Hydrarch", HpPct = 0.15f, Regen = 0.03f, Text = "Max HP +15%; heals 3% of max HP each turn." },
        new() { Id = "fanged_retort", Name = "Fanged Retort", Class = "Hydrarch", Kind = PassiveKind.Reaction, React = ReactionKind.Counter, ReactPower = 0.6f, Text = "Hit in melee: one of its other heads bites back (x0.6)." },
        new() { Id = "stone_skin", Name = "Stone Skin", Class = "Stoneguard", Def = 2, NoStun = true, Text = "DEF +2; can't be stunned." },
        new() { Id = "harden", Name = "Harden", Class = "Stoneguard", Kind = PassiveKind.Reaction, React = ReactionKind.Brace, ReactAmount = 4, Text = "Hit: DEF +4 until its next turn." },
        new() { Id = "scaled_coils", Name = "Scaled Coils", Class = "Coilbinder", Evade = 10, Unstoppable = true, Text = "10% harder to hit; no knockback, no roots." },
        new() { Id = "slither", Name = "Slither", Class = "Coilbinder", Kind = PassiveKind.Reaction, React = ReactionKind.Retreat, ReactAmount = 10, Text = "Hit: slither a tile away and become 10% harder to hit." },
        new() { Id = "venom_proof", Name = "Venom-proof", Class = "Venomtail", NoPoison = true, Atk = 1, Text = "Immune to poison; ATK +1." },
        new() { Id = "tail_flick", Name = "Tail Flick", Class = "Venomtail", Kind = PassiveKind.Reaction, React = ReactionKind.DirtyRetort, ReactChance = 0.35f, Text = "Hit in melee: 35% chance to stun the attacker with a flick of the sting." },
        new() { Id = "deep_hunger", Name = "Deep Hunger", Class = "Lurelight", Predator = 0.25f, Text = "25% more damage to foes under half HP." },
        new() { Id = "into_the_dark", Name = "Into the Dark", Class = "Lurelight", Kind = PassiveKind.Reaction, React = ReactionKind.Dodge, ReactChance = 0.2f, Text = "A hit that would land: 20% chance it finds only darkness." },
        new() { Id = "farsight", Name = "Farsight", Class = "Oculus", Hit = 10, RangeBonus = 1, Text = "+10 to hit; ranged skills reach 1 further." },
        new() { Id = "sees_it_coming", Name = "Sees It Coming", Class = "Oculus", Kind = PassiveKind.Reaction, React = ReactionKind.Dodge, ReactChance = 0.3f, Text = "A hit that would land: 30% chance it doesn't." },
        new() { Id = "blessed", Name = "Blessed", Class = "Saint", Echo = true, Regen = 0.03f, Text = "Boosts it gives last a turn longer; heals 3% of max HP each turn." },
        new() { Id = "hearten", Name = "Hearten", Class = "Saint", Kind = PassiveKind.Reaction, React = ReactionKind.RallyCry, ReactAmount = 2, Text = "Hit: allies beside it get ATK +2 for a turn." },
        new() { Id = "sigil_lore", Name = "Sigil Lore", Class = "Hexmark", SkillPower = 0.15f, Text = "Skills deal 15% more damage." },
        new() { Id = "stone_blood", Name = "Stone Blood", Class = "Basilisk", NoPoison = true, Thorns = 2, Text = "Immune to poison; melee attackers take 2." },
        new() { Id = "petrifying_glare", Name = "Petrifying Glare", Class = "Basilisk", Kind = PassiveKind.Reaction, React = ReactionKind.DirtyRetort, ReactChance = 0.25f, Text = "Hit in melee: 25% chance the attacker is stunned, half turned to stone." },
        new() { Id = "ward_sigil", Name = "Ward Sigil", Class = "Hexmark", Kind = PassiveKind.Reaction, React = ReactionKind.Brace, ReactAmount = 3, Text = "Hit: DEF +3 until its next turn." },
    };

    /// <summary>
    /// A shiny's own passive (the user's call, 2026-11-02): a second support on top of the equipped one,
    /// unique to shinies, picked by the creature's seed so it never changes.
    /// </summary>
    public static readonly PassiveDef[] ShinyPassives =
    {
        new() { Id = "prismatic_hide", Name = "Prismatic Hide", Class = "Shiny", Evade = 10, NoPoison = true, Text = "Light bends around it: 10% harder to hit, and immune to poison." },
        new() { Id = "gleaming_fang", Name = "Gleaming Fang", Class = "Shiny", Predator = 0.3f, Atk = 1, Text = "ATK +1, and 30% more damage to foes under half HP." },
        new() { Id = "radiant_blood", Name = "Radiant Blood", Class = "Shiny", Regen = 0.06f, Text = "Heals 6% of max HP at the start of each turn." },
        new() { Id = "starlit_mind", Name = "Starlit Mind", Class = "Shiny", SkillPower = 0.2f, CooldownCut = 1, Text = "Skills hit 20% harder and cool down a turn sooner." },
        new() { Id = "mirror_scales", Name = "Mirror Scales", Class = "Shiny", Thorns = 3, Def = 1, Text = "DEF +1, and melee attackers take 3." },
        new() { Id = "lucky_glint", Name = "Lucky Glint", Class = "Shiny", Hit = 15, Spd = 1, Text = "SPD +1 and +15% to hit." },
        new() { Id = "unbroken", Name = "Unbroken", Class = "Shiny", NoStun = true, Unstoppable = true, HpPct = 0.1f, Text = "Max HP +10%; can't be stunned, rooted or knocked back." },
        new() { Id = "echoing_light", Name = "Echoing Light", Class = "Shiny", Echo = true, RangeBonus = 1, Text = "Boosts it gives last a turn longer; ranged skills reach 1 further." },
        new() { Id = "moonstep", Name = "Moonstep", Class = "Shiny", Move = 1, Climb = 2, Text = "MOVE +1, and steps up or down two levels." },
        new() { Id = "thirsting_shine", Name = "Thirsting Shine", Class = "Shiny", Drain = 0.3f, Text = "Heals 30% of the damage its attacks deal." },
        new() { Id = "gilded_heart", Name = "Gilded Heart", Class = "Shiny", ChemBonus = 6, HpPct = 0.1f, Text = "Max HP +10%, and 6 extra chem after every battle it fights in." },
    };

    /// <summary>The shiny passive a creature has, or null if it isn't shiny.</summary>
    public static PassiveDef ShinyPassive(Creature c) =>
        c.Shiny ? ShinyPassives[(int)(SeededRng.Derive(c.Seed, 0x5419) % (ulong)ShinyPassives.Length)] : null;

    static readonly Dictionary<string, PassiveDef> _passives = Passives.Concat(ShinyPassives).Concat(GearBook.Gear).Concat(TraitBook.Traits).ToDictionary(p => p.Id);
    public static PassiveDef Passive(string id) => id != null && _passives.TryGetValue(id, out var p) ? p : null;
    public static List<PassiveDef> PassivesOf(string cls) => Passives.Where(p => p.Class == cls).OrderBy(p => p.Kind).ToList();

    public static int TotalCost(string cls) => Class(cls).Skills.Sum(id => Skill(id).Cost) + PassivesOf(cls).Sum(p => p.Cost);

    /// <summary>JP past everything a class teaches before it's mastered.</summary>
    public const int MasteryExtra = 60;

    /// <summary>A mastered class's permanent bonus: its strongest leaning, kept in any class.</summary>
    public static (int atk, int def, int spd, int hp) MasteryBonus(string cls)
    {
        var k = Class(cls);
        int best = Math.Max(Math.Max(k.Atk, k.Def), Math.Max(k.Spd, k.Hp / 3));
        if (best <= 0) return (0, 0, 0, 5);
        if (k.Atk == best) return (1, 0, 0, 0);
        if (k.Def == best) return (0, 1, 0, 0);
        if (k.Spd == best) return (0, 0, 1, 0);
        return (0, 0, 0, 5);
    }

    public static string MasteryText(string cls)
    {
        var (atk, def, spd, hp) = MasteryBonus(cls);
        return atk > 0 ? "ATK +1" : def > 0 ? "DEF +1" : spd > 0 ? "SPD +1" : $"max HP +{hp}";
    }

    static readonly Dictionary<string, SkillDef> _skills = Skills.Concat(GearBook.Items).Concat(AugmentBook.Skills).ToDictionary(s => s.Id);
    static readonly Dictionary<string, ClassDef> _classes = Classes.ToDictionary(c => c.Id);

    public static SkillDef Skill(string id) => _skills[id];
    public static ClassDef Class(string id) => _classes[id];

    public static bool Unlocked(ClassDef c, Creature creature, Training t) =>
        c.Requires.All(r => t.LearnedIn(r.cls) >= r.skills) && (c.NeedsPart == null || c.NeedsPart(creature));

    /// <summary>The hint shown for a locked class, like FFTA's silhouettes.</summary>
    public static string Hint(ClassDef c, Creature creature, Training t)
    {
        string plan = creature.Species.Plan.ToString().ToLowerInvariant();
        if (Barred(creature, c.Id)) return $"never: a {plan}'s body can't train as a {c.Name}";
        if (c.Requires.FirstOrDefault(r => Barred(creature, r.cls)) is { cls: not null } bar) return $"never: needs {bar.cls}, which a {plan} can't train";
        var parts = c.Requires.Select(r => $"{r.skills} {r.cls} skills ({Math.Min(t.LearnedIn(r.cls), r.skills)}/{r.skills})").ToList();
        if (c.PartHint != null) parts.Add(c.NeedsPart(creature) ? $"{c.PartHint} (has it)" : $"{c.PartHint} ({HowToGet(c.PartHint)})");
        return string.Join(", ", parts);
    }

    static string HowToGet(string hint) =>
        hint.Contains("blood") ? "infuse it at the lab, or breed it in"
        : hint.Contains("graft") || hint.Contains("visor") ? "fit it at the lab"
        : hint.Contains("size") || hint.Contains("temperament") || hint.Contains("body") ? "breed for it"
        : "breed or graft it in";

    public static IEnumerable<ClassDef> Available(Creature creature, Training t) => Classes.Where(c => StandingOf(c, creature, t) == Standing.Open);

    /// <summary>A wild creature's natural skill, from its strain (and fury, for aggressive beasts).</summary>
    public static string WildSkill(Creature c) => c.Species.Strain switch
    {
        Strain.Chemtech => "toxic_flask",
        Strain.Arcane => "hex_bolt",
        Strain.Clockwork => "shock_prod",
        Strain.Abyssal => "rend",
        _ => c.Attitude == Attitude.Aggressive ? "heavy_blow" : null,
    };
}
