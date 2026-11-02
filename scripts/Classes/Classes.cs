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
}


/// <summary>
/// A passive (FFTA's support abilities): learned as the last lesson of a class, then equipped in
/// the passive slot, from any class the creature has trained in. Stat bonuses and rule changes.
/// </summary>
public sealed class PassiveDef
{
    public string Id, Name, Class, Text;
    public int Cost = 30;
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
    public string Passive; // the one equipped (FFTA's support slot), from any class trained

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
        // The last lesson of a class is its passive. The first passive learned is equipped for you.
        var passive = ClassBook.PassiveOf(Class);
        if (JpIn(Class) >= spent + passive.Cost && !Passives.Contains(passive.Id))
        {
            Passives.Add(passive.Id);
            Passive ??= passive.Id;
            learned.Add(passive.Name + " (passive)");
        }
        return learned;
    }

    /// <summary>A starter: in the given class, with its first skill already learned.</summary>
    public static Training Starter(string cls)
    {
        var t = new Training { Class = cls };
        t.Earn(ClassBook.Skill(ClassBook.Class(cls).Skills[0]).Cost);
        return t;
    }

    public Training Copy() => new() { Class = Class, Secondary = Secondary, Jp = new(Jp), Skills = new(Skills), Innate = Innate, Passives = new(Passives), Passive = Passive };
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
            Id = "Hexwarden", Name = "Hexwarden", Blurb = "Binds hextech and clockwork.", Atk = 2, Def = 1,
            Skills = new[] { "hex_cage", "overcharge", "nova" }, Requires = new[] { ("Mystic", 2), ("Tinker", 2) },
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
    };

    static readonly Dictionary<string, PassiveDef> _passives = Passives.ToDictionary(p => p.Id);
    public static PassiveDef Passive(string id) => id != null && _passives.TryGetValue(id, out var p) ? p : null;
    public static PassiveDef PassiveOf(string cls) => Passives.First(p => p.Class == cls);

    static readonly Dictionary<string, SkillDef> _skills = Skills.ToDictionary(s => s.Id);
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
