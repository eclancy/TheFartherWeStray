using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Tactics;

public enum Side { Player, Wild }

/// <summary>
/// A creature on the battlefield. Its stats come from its parts the way FFTA stats come from race
/// and equipment: body plan and size set the base, the weapon part sets reach and knockback, the
/// armour part sets defence, wings or fins set how it moves, and strain grafts add on top.
/// </summary>
public sealed class Unit
{
    public Creature Creature;
    public Side Side;
    public Vector2I Pos;     // top-left tile of its footprint
    public int Size;         // footprint width: 2 for huge creatures (2x2), else 1

    public int MaxHp, Hp, Atk, Def, Spd, Move, HitBonus;
    public MoveType MoveType;
    public Weapon Weapon;
    public Armor Armor;

    public bool Gone;        // fainted, or fled the field
    public bool Moved, Acted;
    public Vector2I TurnStart;

    // Recruiting and reactions.
    public readonly HashSet<Unit> HitBy = new();
    public Unit LastAttacker;
    public int Weathered;    // aggressive: attacks it made that didn't drop anyone
    public bool GuardUsed;   // loyal: has shielded an ally this round

    // Class, skills and statuses.
    public Training Training;                        // null for wild creatures
    public List<SkillDef> Skills = new();
    public readonly Dictionary<string, int> Cooldowns = new();
    public readonly List<(StatKind stat, int amount, int turns)> Buffs = new();
    public int PoisonDmg, PoisonTurns;
    public bool Stunned, Rooted;
    public Unit TauntedBy;
    public PassiveDef ShinyPassive;                  // a shiny's own second passive, on top of the equipped one
    public PassiveDef GearPassive;                   // what its gear does
    public readonly List<PassiveDef> Traits = new(); // what it was born with
    /// <summary>The support passive equipped in its training.</summary>
    public PassiveDef Equipped => ClassBook.Passive(Training?.Passive);
    /// <summary>Every support passive in effect, as one: the equipped one, plus a shiny's own, its gear's and its traits.</summary>
    public PassiveDef Passive
    {
        get
        {
            if (ShinyPassive == null && GearPassive == null && Traits.Count == 0) return Equipped;
            if (_merged == null || _mergedFor != Training?.Passive)
            {
                _merged = new[] { ShinyPassive, GearPassive }.Concat(Traits).Aggregate(Equipped, PassiveDef.Merge);
                _mergedFor = Training?.Passive;
            }
            return _merged;
        }
    }
    PassiveDef _merged;
    string _mergedFor;
    public Party.TamerDef Tamer;                   // who leads it from off the field (yours, or a rival's), if anyone
    /// <summary>The tamer's boost, if it reaches this creature (some tamers only lead certain kinds).</summary>
    public Party.TamerDef Boost => Tamer != null && Tamer.Boosts(Creature) ? Tamer : null;
    /// <summary>A wild creature no tamer leads: it acts on its temper and can be won over (a rival's can't).</summary>
    public bool Free => Side == Side.Wild && Tamer == null;
    public PassiveDef Reaction => ClassBook.Passive(Training?.Reaction);
    public bool AdrenalineUsed;

    public int Mod(StatKind s) => Buffs.Where(b => b.stat == s).Sum(b => b.amount) + (s == StatKind.Evade ? (Passive?.Evade ?? 0) + (Boost?.Evade ?? 0) : 0);
    public int AtkNow => System.Math.Max(1, Atk + Mod(StatKind.Atk));
    public int DefNow => System.Math.Max(0, Def + Mod(StatKind.Def));
    public int SpdNow => System.Math.Max(1, Spd + Mod(StatKind.Spd));
    public int MoveNow => Rooted ? 0 : System.Math.Max(1, Move + Mod(StatKind.Move));
    public bool Ready(SkillDef s) => !Cooldowns.TryGetValue(s.Id, out int cd) || cd <= 0;
    public string ClassName => Training?.Class;

    public Attitude Attitude => Creature.Attitude;
    public string Name => Creature.Name;
    public int Range => Weapon == Weapon.Whip || Passive is { LongReach: true } ? 2 : 1;
    public bool Knockback => Weapon is Weapon.Horns or Weapon.Hooves;
    public int Thorns => (Armor == Armor.Spines ? 3 : 0) + (Passive?.Thorns ?? 0) + (Boost?.Thorns ?? 0) + Mod(StatKind.Thorns);

    public IEnumerable<Vector2I> TilesAt(Vector2I p)
    {
        for (int dy = 0; dy < Size; dy++)
        for (int dx = 0; dx < Size; dx++)
            yield return p + new Vector2I(dx, dy);
    }

    public IEnumerable<Vector2I> Tiles => TilesAt(Pos);

    public static Unit From(Creature c, Side side, Training training = null)
    {
        var sp = c.Species;
        (float hp, float atk, float def, float spd, int move) = sp.Plan switch
        {
            BodyPlan.Quadruped => (30f, 9f, 6f, 6f, 4),
            BodyPlan.Biped => (32f, 9f, 6f, 5f, 4),
            BodyPlan.Serpent => (28f, 8f, 5f, 6f, 4),
            BodyPlan.Avian => (24f, 8f, 4f, 8f, 5),
            BodyPlan.Insectoid => (24f, 9f, 6f, 7f, 4),
            BodyPlan.Aquatic => (28f, 8f, 5f, 6f, 4),
            _ => (36f, 7f, 8f, 4f, 3),
        };
        switch (c.Size)
        {
            case SizeClass.Tiny: hp *= 0.6f; atk -= 2; spd += 2; break;
            case SizeClass.Small: hp *= 0.8f; atk -= 1; spd += 1; break;
            case SizeClass.Large: hp *= 1.25f; atk += 1; spd -= 1; break;
            case SizeClass.Huge: hp *= 1.6f; atk += 2; spd -= 2; move -= 1; break;
        }
        int hit = 0;
        switch (c.Weapon)
        {
            case Weapon.Claws: atk += 2; break;
            case Weapon.Horns: atk += 2; break;
            case Weapon.Club: atk += 3; hit -= 10; break;
            case Weapon.Hooves: atk += 1; break;
        }
        switch (c.Armor)
        {
            case Armor.Shell: def += 4; move -= 1; break;
            case Armor.Spines: def += 1; break;
            case Armor.Crystal: def += 3; break;
            case Armor.Mane: hp += 6; break;
        }
        // The class leans the stats (FFTA jobs do the same).
        if (training != null)
        {
            var cls = ClassBook.Class(training.Class);
            float lean = ClassBook.Favoured(c, cls.Id) ? 1.5f : 1f; // a favoured class suits the body better
            hp += cls.Hp * lean; atk += cls.Atk * lean; def += cls.Def * lean; spd += cls.Spd * lean; move += cls.Move;
            if (ClassBook.Passive(training.Passive) is { } p)
            {
                atk += p.Atk; def += p.Def; spd += p.Spd; move += p.Move; hit += p.Hit; hp *= 1 + p.HpPct;
            }
            // Mastered classes keep paying out, whatever the creature trains as now.
            foreach (var m in training.Masteries)
            {
                var (ma, md, ms, mh) = ClassBook.MasteryBonus(m);
                atk += ma; def += md; spd += ms; hp += mh;
            }
        }
        // A shiny's own passive, its gear and its traits count whatever it trains as.
        var shine = ClassBook.ShinyPassive(c);
        var gear = ClassBook.Passive(training?.Gear);
        var traits = c.Traits.Select(ClassBook.Passive).Where(p => p != null).ToList();
        foreach (var extra in traits.Prepend(gear).Prepend(shine))
        {
            if (extra == null) continue;
            atk += extra.Atk; def += extra.Def; spd += extra.Spd; move += extra.Move; hit += extra.Hit; hp *= 1 + extra.HpPct;
        }
        var g = sp.Grafts;
        if (g.HasFlag(Graft.Plating)) def += 3;
        if (g.HasFlag(Graft.MetalLimb)) atk += 1;
        if (g.HasFlag(Graft.Visor)) hit += 10;
        if (g.HasFlag(Graft.Core)) atk += 2;
        if (g.HasFlag(Graft.Canister) || g.HasFlag(Graft.Drips)) atk += 1;
        if (g.HasFlag(Graft.Veins)) spd += 1;
        if (g.HasFlag(Graft.Runes)) { def += 1; hit += 5; }
        if (g.HasFlag(Graft.GlowTips)) atk += 1;
        if (g.HasFlag(Graft.ExtraEyes)) hit += 10;
        if (g.HasFlag(Graft.Cracks)) { atk += 2; def -= 1; }

        // Individuals roll a little either side of their species.
        var rng = new SeededRng(c.Seed ^ 0xBA77_1E5UL);
        int Roll(float v) => Mathf.Max(1, Mathf.RoundToInt(v * rng.Range(0.9f, 1.1f)));
        var u = new Unit
        {
            Creature = c, Side = side, Size = c.Size == SizeClass.Huge ? 2 : 1,
            MaxHp = Roll(hp), Atk = Roll(atk), Def = Roll(def), Spd = Roll(spd), Move = Mathf.Max(2, move),
            HitBonus = hit, MoveType = c.MoveType, Weapon = c.Weapon, Armor = c.Armor,
        };
        u.Hp = u.MaxHp;
        u.Training = training;
        u.ShinyPassive = shine;
        u.GearPassive = gear;
        u.Traits.AddRange(traits);
        if (training != null) u.Skills = training.Usable().ToList();
        else if (ClassBook.WildSkill(c) is { } wild) u.Skills.Add(ClassBook.Skill(wild));
        // Augments teach their attacks whatever it trains as.
        foreach (var a in c.Augments)
            if (AugmentBook.Get(a) is { } aug && u.Skills.All(s => s.Id != aug.Skill)) u.Skills.Add(ClassBook.Skill(aug.Skill));
        return u;
    }
}
