using System;
using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Ranching;

/// <summary>
/// The lab, where parts change without breeding (game-ideas.md: "you can't take off a horn, so
/// parts change through breeding, mutation, or grafting at the ranch, a costly ritual or lab").
/// Three operations, all paid in chem:
/// <list type="bullet">
/// <item>flesh grafts swap one part slot for a variant in your codex (parts you've met in battle)</item>
/// <item>machine grafts bolt on Piltover brass: plating, a metal limb, a lens visor, a hextech core</item>
/// <item>infusions put chemtech, arcane or abyssal blood into a natural creature</item>
/// </list>
/// Each changes its tier (Classes.TierBook): a splice makes a chimera, an infusion infuses, a chemical
/// augment infuses and a mechanical one rigs; any of them ends a thoroughbred's bonus.
/// Flesh and machine grafts aren't inherited (breeding reads birth parts), so the lab is a costly
/// shortcut for one creature while breeding stays the way to build a line. Infusions are blood, and
/// they do pass down.
/// </summary>
public static class Lab
{
    public sealed record Slot(string Name, Type Type, int Cost);

    public static readonly Slot[] Slots =
    {
        new("Head", typeof(HeadShape), 10), new("Ears", typeof(Ears), 8), new("Eyes", typeof(EyeKind), 8),
        new("Horns", typeof(Horns), 25), new("Back", typeof(Back), 20), new("Limbs", typeof(Limbs), 15),
        new("Tail", typeof(Tail), 15), new("Wings", typeof(Wings), 40), new("Pattern", typeof(Pattern), 6),
    };

    /// <summary>Machine grafts: bolted on, never inherited.</summary>
    public const Graft MachineGrafts = Graft.Plating | Graft.MetalLimb | Graft.Visor | Graft.Core
        | Graft.Gears | Graft.Pistons | Graft.Smokestack | Graft.Pipework;

    public static readonly (Strain strain, Graft grafts, int cost)[] Infusions =
    {
        (Strain.Chemtech, Graft.Veins | Graft.Drips, 45),
        (Strain.Arcane, Graft.Runes | Graft.GlowTips, 45),
        (Strain.Abyssal, Graft.ExtraEyes | Graft.Cracks, 45),
    };

    public static int Get(PartSet p, string slot) => slot switch
    {
        "Head" => (int)p.Head, "Ears" => (int)p.Ears, "Eyes" => (int)p.Eyes, "Horns" => (int)p.Horns, "Back" => (int)p.Back,
        "Limbs" => (int)p.Limbs, "Tail" => (int)p.Tail, "Wings" => (int)p.Wings, _ => (int)p.Pattern,
    };

    public static PartSet Set(PartSet p, string slot, int v) => slot switch
    {
        "Head" => p with { Head = (HeadShape)v }, "Ears" => p with { Ears = (Ears)v }, "Eyes" => p with { Eyes = (EyeKind)v },
        "Horns" => p with { Horns = (Horns)v }, "Back" => p with { Back = (Back)v }, "Limbs" => p with { Limbs = (Limbs)v },
        "Tail" => p with { Tail = (Tail)v }, "Wings" => p with { Wings = (Wings)v }, _ => p with { Pattern = (Pattern)v },
    };

    public static string Key(string slot, int v) => $"{slot}.{Enum.GetName(SlotOf(slot).Type, v)}";
    public static Slot SlotOf(string name) => Slots.First(s => s.Name == name);
    public static string Words(string slot, int v) => Creature.Words((Enum)Enum.ToObject(SlotOf(slot).Type, v));

    /// <summary>Slots a body plan can show (fish have nowhere to put wings or legs, serpents no legs).</summary>
    public static IEnumerable<Slot> SlotsFor(BodyPlan plan) => Slots.Where(s =>
        !(s.Name == "Wings" && plan == BodyPlan.Aquatic) && !(s.Name == "Limbs" && plan is BodyPlan.Serpent or BodyPlan.Aquatic));

    /// <summary>Variants of a slot you can graft: every part in your codex, plus none where "none" exists.</summary>
    public static List<int> Options(Ranch ranch, string slot)
    {
        var type = SlotOf(slot).Type;
        return Enum.GetValues(type).Cast<int>()
            .Where(v => Enum.GetName(type, v) == "None" || ranch.Codex.Contains(Key(slot, v))).ToList();
    }

    /// <summary>Adds every part a creature carries to the codex.</summary>
    public static int Learn(Ranch ranch, Creature c)
    {
        int added = 0;
        foreach (var s in Slots)
            if (ranch.Codex.Add(Key(s.Name, Get(c.Parts, s.Name)))) added++;
        return added;
    }

    public static string GraftPart(Ranch ranch, int id, string slot, int value)
    {
        var rec = ranch.Record(id);
        var cost = SlotOf(slot).Cost;
        if (Get(rec.Parts, slot) == value) return "It already has that.";
        if (ranch.Chem < cost) return $"Not enough chem: a {slot.ToLowerInvariant()} graft costs {cost}.";
        var before = Classes.TierBook.Of(ranch.Get(id));
        bool spliced = Classes.TierBook.Chimera(ranch.Get(id));
        ranch.Chem -= cost;
        rec.BirthParts ??= rec.Parts;
        rec.Parts = Set(rec.Parts, slot, value);
        ranch.Refresh(id);
        // Splicing a creature into a chimera is undercity work: the undercity takes note the first time.
        string moved = !spliced && Classes.TierBook.Chimera(ranch.Get(id)) ? $" ({World.Standing.Shift(ranch, 0, World.Standing.Splice)})" : "";
        return $"{rec.Name}: {slot.ToLowerInvariant()} grafted to {Words(slot, value)} (-{cost} chem).{ranch.TierNote(id, before)}{moved}";
    }

    public static string Infuse(Ranch ranch, int id, Strain strain)
    {
        var rec = ranch.Record(id);
        var (_, _, cost) = Infusions.First(i => i.strain == strain);
        if (rec.Species.Strain != Strain.Natural) return "Only a natural creature can take an infusion.";
        if (ranch.Chem < cost) return $"Not enough chem: an infusion costs {cost}.";
        var before = Classes.TierBook.Of(ranch.Get(id));
        ranch.Chem -= cost;
        rec.Species = rec.Species.Clone();
        Infusion(rec.Species, strain);
        if (strain == Strain.Abyssal) rec.Spec = rec.Spec.Abyssal(new SeededRng(rec.Seed ^ 0xAB15));
        ranch.Refresh(id);
        // Chemical work: the undercity takes note, as it does of augments.
        string moved = World.Standing.Shift(ranch, 0, World.Standing.Augment);
        return $"{rec.Name} takes the {strain.Label().ToLowerInvariant()} infusion (-{cost} chem). Its line will carry it.{ranch.TierNote(id, before)} ({moved})";
    }

    /// <summary>Puts a blood strain into a species (a copy of it): the strain, its grafts, and the mark of an infusion, which its line carries.</summary>
    public static void Infusion(Species sp, Strain strain)
    {
        sp.Strain = strain;
        sp.Grafts |= Infusions.First(i => i.strain == strain).grafts;
        sp.Infused = true;
    }
}
