using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CreatureTactics.Creatures;

/// <summary>
/// Breeding at the ranch: two parents make one child, a new species of its own. Parts are the
/// gear here (game-ideas.md: "breeding is the gear grind"), so every part slot comes whole from one
/// parent or the other, and you can breed toward the creature you have in mind:
/// <list type="bullet">
/// <item>body plan, and each part slot, from one parent (a parent's own mutations count)</item>
/// <item>a small chance of a fresh mutation in one slot</item>
/// <item>colours from either parent, sometimes blended</item>
/// <item>size and proportions in between</item>
/// <item>blood strains (arcane, chemtech, abyssal) are inherited; clockwork is machinery and isn't</item>
/// </list>
/// </summary>
public static class Breeding
{
    public sealed class Result
    {
        public Creature Child;
        public readonly List<string> FromA = new(), FromB = new(), Notes = new();
    }

    public static Result Breed(Creature a, Creature b, ulong seed)
    {
        var rng = new SeededRng(seed);
        var r = new Result();
        Species A = a.Species, B = b.Species;

        // Each trait comes from one side; the result notes which, so the screen can show it.
        T From<T>(string trait, T fromA, T fromB)
        {
            bool takeA = rng.Chance(0.5f);
            if (!EqualityComparer<T>.Default.Equals(fromA, fromB)) (takeA ? r.FromA : r.FromB).Add(trait);
            return takeA ? fromA : fromB;
        }

        var plan = From("body plan", A.Plan, B.Plan);
        var planParent = plan == A.Plan ? A : B;
        // Birth parts: lab grafts are surgery, not blood, so they don't pass down.
        PartSet pa = a.BirthParts ?? a.Parts, pb = b.BirthParts ?? b.Parts;
        var parts = new PartSet(
            From("head", pa.Head, pb.Head), From("ears", pa.Ears, pb.Ears), From("eyes", pa.Eyes, pb.Eyes),
            From("horns", pa.Horns, pb.Horns), From("back", pa.Back, pb.Back), From("limbs", pa.Limbs, pb.Limbs),
            From("tail", pa.Tail, pb.Tail), From("wings", pa.Wings, pb.Wings), From("pattern", pa.Pattern, pb.Pattern));

        // A fresh mutation now and then, so lines don't only ever reshuffle what they started with.
        if (rng.Chance(0.12f))
        {
            var donor = Species.RollParts(plan, rng);
            switch (rng.Int(5))
            {
                case 0 when donor.Horns != parts.Horns: parts.Horns = donor.Horns; r.Notes.Add($"mutation: {Creature.Words(donor.Horns)} horns"); break;
                case 1 when donor.Back != parts.Back: parts.Back = donor.Back; r.Notes.Add($"mutation: {Creature.Words(donor.Back)} back"); break;
                case 2 when donor.Tail != parts.Tail: parts.Tail = donor.Tail; r.Notes.Add($"mutation: {Creature.Words(donor.Tail)} tail"); break;
                case 3 when donor.Wings != parts.Wings: parts.Wings = donor.Wings; r.Notes.Add($"mutation: {Creature.Words(donor.Wings)} wings"); break;
                case 4 when donor.Pattern != parts.Pattern: parts.Pattern = donor.Pattern; r.Notes.Add($"mutation: {Creature.Words(donor.Pattern)} pattern"); break;
            }
        }
        // Fish can't carry wings (the plan has nowhere to draw them).
        if (plan == BodyPlan.Aquatic && parts.Wings != Wings.None) { parts.Wings = Wings.None; r.Notes.Add("the wings didn't take"); }

        // Colours: the main colour from one parent, sometimes blended toward the other.
        PaletteSpec ca = a.Spec, cb = b.Spec;
        var main = From("colour", ca.Main, cb.Main);
        if (rng.Chance(0.3f)) { main = Blend(ca.Main, cb.Main, rng.Range(0.35f, 0.65f)); r.Notes.Add("blended colour"); }
        var palette = new PaletteSpec(main, rng.Chance(0.5f) ? ca.Second : cb.Second, rng.Chance(0.5f) ? ca.Accent : cb.Accent,
            rng.Chance(0.5f) ? ca.Membrane : cb.Membrane, rng.Chance(0.5f) ? ca.Glow : cb.Glow, rng.Chance(0.5f) ? ca.Metal : cb.Metal);

        // Strain: blood strains are inherited, clockwork is not.
        static bool Blood(Strain s) => s is Strain.Arcane or Strain.Chemtech or Strain.Abyssal;
        Strain strain = Strain.Natural;
        Species strainParent = null;
        if (Blood(A.Strain) && Blood(B.Strain)) strainParent = rng.Chance(0.5f) ? A : B;
        else if (Blood(A.Strain)) strainParent = rng.Chance(0.75f) ? A : null;
        else if (Blood(B.Strain)) strainParent = rng.Chance(0.75f) ? B : null;
        Graft grafts = Graft.None;
        if (strainParent != null)
        {
            strain = strainParent.Strain;
            (strainParent == A ? r.FromA : r.FromB).Add($"{strain.ToString().ToLowerInvariant()} blood");
            // Most of the parent's blood traits come through; at least one always does.
            foreach (Graft g in Enum.GetValues<Graft>())
                if (g != Graft.None && (g & Ranching.Lab.MachineGrafts) == 0 && strainParent.Grafts.HasFlag(g) && rng.Chance(0.7f)) grafts |= g;
            if (grafts == Graft.None) grafts = Species.RollGrafts(strain, rng) & ~Ranching.Lab.MachineGrafts;
            if (grafts == Graft.None) grafts = strain switch { Strain.Arcane => Graft.Runes, Strain.Chemtech => Graft.Veins, _ => Graft.Cracks };
        }
        if (A.Strain == Strain.Clockwork || B.Strain == Strain.Clockwork) r.Notes.Add("clockwork grafts aren't inherited");

        float Mix(float x, float y) => Mathf.Lerp(x, y, rng.Range(0.3f, 0.7f)) * rng.Range(0.96f, 1.04f);
        var style = CreaturePainter.StyleOf(planParent);
        var child = new Species
        {
            Seed = seed,
            Plan = plan,
            Element = main == ca.Main ? A.Element : B.Element,
            BaseSize = (SizeClass)Math.Clamp((int)Math.Round(((int)a.Size + (int)b.Size) / 2f + (rng.Chance(0.25f) ? rng.Pick(new[] { -1, 1 }) : 0)), 0, 4),
            Parts = parts,
            Palette = palette,
            CommonAttitude = rng.Chance(0.5f) ? a.Attitude : b.Attitude,
            BodyLen = Mix(A.BodyLen, B.BodyLen), Thickness = Mix(A.Thickness, B.Thickness), LegLen = Mix(A.LegLen, B.LegLen),
            HeadSize = Mix(A.HeadSize, B.HeadSize), NeckLen = Mix(A.NeckLen, B.NeckLen), SnoutLen = Mix(A.SnoutLen, B.SnoutLen),
            EyeSize = Mix(A.EyeSize, B.EyeSize), WingSpread = Mix(A.WingSpread, B.WingSpread), HornCurve = Mix(A.HornCurve, B.HornCurve),
            WingsFolded = planParent.WingsFolded, MuzzleSecond = rng.Chance(0.5f) ? A.MuzzleSecond : B.MuzzleSecond,
            Strain = strain, Grafts = grafts,
            // The body plan's parent passes on its silhouette; either parent its aura.
            Form = Species.FormOf(planParent), Aura = Species.AuraOf(rng.Chance(0.5f) ? A : B),
            SpikeMat = style.spike, ShellMat = style.shell, FeatherMat = style.feather, SailMat = style.sail, TuftMat = style.tuft, CrestMat = style.crest,
        };
        child.Name = CreatureNames.Splice(A.Name, B.Name, rng);

        var c = new Creature
        {
            Species = child, Seed = SeededRng.Derive(seed, 1), Size = child.BaseSize, Parts = parts,
            Attitude = rng.Chance(0.85f) ? child.CommonAttitude : (Attitude)rng.Int(5),
            Spec = palette, BodyJitter = 1, LegJitter = 1, HeadJitter = 1,
            Generation = Math.Max(a.Generation, b.Generation) + 1, ParentA = A.Name, ParentB = B.Name,
        };
        c.Palette = Creature.BuildPalette(palette, child);
        r.Child = c;
        return r;
    }

    static Hsv Blend(Hsv x, Hsv y, float t)
    {
        float dh = Mathf.PosMod(y.H - x.H + 0.5f, 1f) - 0.5f; // the short way round the hue wheel
        return new Hsv(Mathf.PosMod(x.H + dh * t, 1f), Mathf.Lerp(x.S, y.S, t), Mathf.Lerp(x.V, y.V, t));
    }
}
