using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Classes;

public enum StatKind { Atk, Def, Spd, Move, Evade }
public enum TargetKind { Enemy, Ally, Self, Area }

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
    public string Innate; // a skill inherited from a parent, usable in any class

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
    public List<SkillDef> Earn(int jp)
    {
        Jp[Class] = JpIn(Class) + jp;
        var learned = new List<SkillDef>();
        int spent = 0;
        foreach (var id in ClassBook.Class(Class).Skills)
        {
            var s = ClassBook.Skill(id);
            spent += s.Cost;
            if (JpIn(Class) < spent || Skills.Contains(id)) continue;
            Skills.Add(id);
            learned.Add(s);
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

    public Training Copy() => new() { Class = Class, Secondary = Secondary, Jp = new(Jp), Skills = new(Skills), Innate = Innate };
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
    };

    public static readonly ClassDef[] Classes =
    {
        new() { Id = "Brawler", Name = "Brawler", Blurb = "Fights up close.", Atk = 2, Hp = 2, Skills = new[] { "heavy_blow", "rend", "frenzy" } },
        new() { Id = "Skirmisher", Name = "Skirmisher", Blurb = "Hits and runs.", Spd = 2, Move = 1, Skills = new[] { "quick_strike", "pounce", "evasion" } },
        new() { Id = "Guardian", Name = "Guardian", Blurb = "Holds the line.", Def = 2, Hp = 6, Spd = -1, Skills = new[] { "shield_wall", "taunt", "second_wind" } },
        new() { Id = "Mystic", Name = "Mystic", Blurb = "Channels hextech at range.", Atk = 1, Def = -1, Skills = new[] { "hex_bolt", "ward", "arc_storm" } },
        new() { Id = "Alchemist", Name = "Alchemist", Blurb = "Brews chemtech: poison and mending.", Spd = 1, Skills = new[] { "toxic_flask", "mend", "acid_splash" } },
        new() { Id = "Tinker", Name = "Tinker", Blurb = "Clockwork tricks: boosts, snares, shocks.", Def = 1, Spd = 1, Skills = new[] { "overclock", "snare", "shock_prod" } },
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
            Id = "Hexwarden", Name = "Hexwarden", Blurb = "Binds hextech and clockwork.", Atk = 2, Def = 1,
            Skills = new[] { "hex_cage", "overcharge", "nova" }, Requires = new[] { ("Mystic", 2), ("Tinker", 2) },
        },
    };

    static readonly Dictionary<string, SkillDef> _skills = Skills.ToDictionary(s => s.Id);
    static readonly Dictionary<string, ClassDef> _classes = Classes.ToDictionary(c => c.Id);

    public static SkillDef Skill(string id) => _skills[id];
    public static ClassDef Class(string id) => _classes[id];

    public static bool Unlocked(ClassDef c, Creature creature, Training t) =>
        c.Requires.All(r => t.LearnedIn(r.cls) >= r.skills) && (c.NeedsPart == null || c.NeedsPart(creature));

    /// <summary>The hint shown for a locked class, like FFTA's silhouettes.</summary>
    public static string Hint(ClassDef c, Creature creature, Training t)
    {
        var parts = c.Requires.Select(r => $"{r.skills} {r.cls} skills ({Math.Min(t.LearnedIn(r.cls), r.skills)}/{r.skills})").ToList();
        if (c.PartHint != null) parts.Add(c.NeedsPart(creature) ? $"{c.PartHint} (has them)" : $"{c.PartHint} (breed them in)");
        return string.Join(", ", parts);
    }

    public static IEnumerable<ClassDef> Available(Creature creature, Training t) => Classes.Where(c => Unlocked(c, creature, t));

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
