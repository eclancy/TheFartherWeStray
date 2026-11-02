using System;
using System.Collections.Generic;

namespace CreatureTactics.Creatures;

/// <summary>
/// Pets from the DrawPets adopt shop (../DrawPets/scripts/adoptables.gd), re-made as species in
/// this game's part system: the same body plans, part slots and palettes the generator uses, set
/// by hand instead of rolled. They're a check that the part vocabulary can express designed
/// creatures, and a cast of familiar faces. DrawPets draws them facing right in marker; here
/// they're pixel sprites facing left.
///
/// Personality words map to attitudes: shy - skittish; brave, sporty - aggressive; cuddly - loyal;
/// curious, clever, playful - curious; calm, fancy, sleepy - proud.
/// </summary>
public static class DrawPetsCast
{
    static List<Species> _all;

    public static IReadOnlyList<Species> All => _all ??= Build();

    static Species Pet(string name, BodyPlan plan, SizeClass size, Attitude attitude, Element element,
        PartSet parts, PaletteSpec palette, Action<Species> tweak = null)
    {
        var s = new Species
        {
            Seed = SeededRng.Derive(0xD8A3_F7E7, Fnv(name)),
            Name = name, Plan = plan, BaseSize = size, CommonAttitude = attitude, Element = element,
            Parts = parts, Palette = palette, Origin = "DrawPets",
        };
        tweak?.Invoke(s);
        return s;
    }

    /// <summary>A string hash that is the same on every run (string.GetHashCode is not).</summary>
    static ulong Fnv(string s)
    {
        ulong h = 0xCBF29CE484222325UL;
        foreach (char c in s) { h ^= c; h *= 0x100000001B3UL; }
        return h;
    }

    static List<Species> Build() => new()
    {
        // Meadow & Forest
        Pet("Pip", BodyPlan.Quadruped, SizeClass.Tiny, Attitude.Skittish, Element.Meadow,
            new(HeadShape.Snout, Ears.Round, EyeKind.Beady, Horns.None, Back.None, Limbs.Stubby, Tail.Whip, Wings.None, Pattern.Markings),
            PaletteSpec.FromHex("#f2dcb5", "#f7a8c4", "#f7a8c4"),
            s => { s.SnoutLen = 0.85f; s.NeckLen = 0.6f; s.HeadSize = 1.1f; s.TuftMat = Mat.Main; }),
        Pet("Bramble", BodyPlan.Quadruped, SizeClass.Small, Attitude.Skittish, Element.Leaf,
            new(HeadShape.Snout, Ears.None, EyeKind.Beady, Horns.None, Back.Spikes, Limbs.Stubby, Tail.None, Wings.None, Pattern.Belly),
            PaletteSpec.FromHex("#c9965f", "#f2dcb4", "#6e4a2a"),
            s => { s.SnoutLen = 1.4f; s.NeckLen = 0.6f; s.MuzzleSecond = true; s.BodyLen = 1.1f; }),
        Pet("Moss", BodyPlan.Quadruped, SizeClass.Medium, Attitude.Proud, Element.Leaf,
            new(HeadShape.Round, Ears.None, EyeKind.Round, Horns.None, Back.Shell, Limbs.Stubby, Tail.Thick, Wings.None, Pattern.Belly),
            PaletteSpec.FromHex("#a8d672", "#f3eaa0", "#c9a25e"),
            s => { s.NeckLen = 1.3f; s.ShellMat = Mat.Accent; s.Thickness = 1.1f; }),
        Pet("Clover", BodyPlan.Quadruped, SizeClass.Small, Attitude.Curious, Element.Meadow,
            new(HeadShape.Snout, Ears.Pointy, EyeKind.Round, Horns.Antlers, Back.None, Limbs.Hooved, Tail.None, Wings.None, Pattern.Spots),
            PaletteSpec.FromHex("#e9a05a", "#fff6e8", "#8a5a3c"),
            s => { s.LegLen = 1.35f; s.NeckLen = 1.45f; s.Thickness = 0.9f; s.EyeSize = 1.2f; }),
        Pet("Tank", BodyPlan.Quadruped, SizeClass.Large, Attitude.Aggressive, Element.Stone,
            new(HeadShape.Snout, Ears.Pointy, EyeKind.Beady, Horns.NoseHorn, Back.None, Limbs.Stubby, Tail.Whip, Wings.None, Pattern.Stripes),
            PaletteSpec.FromHex("#9fb1d6", "#c6d4ee", "#f2e6c8"),
            s => { s.NeckLen = 0.6f; s.Thickness = 1.15f; s.TuftMat = Mat.Main; }),
        Pet("Sprocket", BodyPlan.Quadruped, SizeClass.Small, Attitude.Curious, Element.Ember,
            new(HeadShape.Snout, Ears.Pointy, EyeKind.Round, Horns.None, Back.None, Limbs.Long, Tail.Fluffy, Wings.None, Pattern.Tips),
            PaletteSpec.FromHex("#f08a3c", "#fff4e6", "#3a2a2a"),
            s => { s.SnoutLen = 1.3f; s.MuzzleSecond = true; s.Thickness = 0.9f; }),
        Pet("Pudding", BodyPlan.Quadruped, SizeClass.Small, Attitude.Loyal, Element.Meadow,
            new(HeadShape.Snout, Ears.Pointy, EyeKind.Beady, Horns.None, Back.None, Limbs.Stubby, Tail.Curl, Wings.None, Pattern.Markings),
            PaletteSpec.FromHex("#f6a8c0", "#fbd0dc", "#c86a8a"),
            s => { s.SnoutLen = 0.75f; s.MuzzleSecond = true; s.Thickness = 1.15f; s.NeckLen = 0.6f; }),
        Pet("Boulder", BodyPlan.Quadruped, SizeClass.Huge, Attitude.Proud, Element.Stone,
            new(HeadShape.Snout, Ears.None, EyeKind.Round, Horns.TwinHorns, Back.Mane, Limbs.Hooved, Tail.Whip, Wings.None, Pattern.None),
            PaletteSpec.FromHex("#9c6b4a", "#c89a74", "#f0e6cc"),
            s => { s.SnoutLen = 0.8f; s.NeckLen = 0.6f; s.HornCurve = -0.4f; s.TuftMat = Mat.Second; s.Thickness = 1.15f; }),
        Pet("Dottie", BodyPlan.Insectoid, SizeClass.Small, Attitude.Curious, Element.Ember,
            new(HeadShape.Round, Ears.None, EyeKind.Round, Horns.Antennae, Back.Shell, Limbs.Stubby, Tail.None, Wings.None, Pattern.Spots),
            PaletteSpec.FromHex("#ef6a60", "#2e2b36", "#ef6a60"),
            s => { s.ShellMat = Mat.Main; s.BodyLen = 1.1f; s.Thickness = 1.2f; }),

        // Fliers
        Pet("Hoot", BodyPlan.Avian, SizeClass.Small, Attitude.Curious, Element.Leaf,
            new(HeadShape.Round, Ears.Pointy, EyeKind.Round, Horns.None, Back.None, Limbs.Clawed, Tail.Fan, Wings.Feather, Pattern.Belly),
            PaletteSpec.FromHex("#b8875a", "#f5dfb0", "#f2b632"),
            s => { s.EyeSize = 1.35f; s.HeadSize = 1.15f; s.SnoutLen = 0.5f; s.WingsFolded = true; s.NeckLen = 0.6f; }),
        Pet("Zephyr", BodyPlan.Quadruped, SizeClass.Small, Attitude.Curious, Element.Tide,
            new(HeadShape.Wide, Ears.None, EyeKind.Round, Horns.None, Back.None, Limbs.Long, Tail.Whip, Wings.Insect, Pattern.Stripes),
            PaletteSpec.FromHex("#4cc4b4", "#2f9488", "#9be06a", "#cfe6ff"),
            s => { s.BodyLen = 1.15f; s.Thickness = 0.85f; s.NeckLen = 0.6f; s.TuftMat = Mat.Main; s.EyeSize = 1.2f; }),
        Pet("Blaze", BodyPlan.Biped, SizeClass.Large, Attitude.Aggressive, Element.Ember,
            new(HeadShape.Snout, Ears.None, EyeKind.Round, Horns.TwinHorns, Back.Spikes, Limbs.Clawed, Tail.Thick, Wings.Bat, Pattern.Belly),
            PaletteSpec.FromHex("#f27a45", "#ffd36b", "#f0e6c8", "#ffc070"),
            s => { s.HornCurve = 0.6f; s.SpikeMat = Mat.Second; s.WingSpread = 0.8f; }),
        Pet("Petal", BodyPlan.Quadruped, SizeClass.Small, Attitude.Proud, Element.Shade,
            new(HeadShape.Round, Ears.Pointy, EyeKind.Round, Horns.None, Back.None, Limbs.Long, Tail.Curl, Wings.Insect, Pattern.Stripes),
            PaletteSpec.FromHex("#b9b6cc", "#8e8aa6", "#f6a6d0", "#f6a6d0"),
            s => { s.NeckLen = 0.8f; s.EyeSize = 1.2f; }),
        Pet("Pelly", BodyPlan.Avian, SizeClass.Medium, Attitude.Curious, Element.Tide,
            new(HeadShape.Round, Ears.None, EyeKind.Beady, Horns.Crest, Back.None, Limbs.Long, Tail.Fan, Wings.Feather, Pattern.None),
            PaletteSpec.FromHex("#f4f1ea", "#f9cfb0", "#f5a24b"),
            s => { s.SnoutLen = 1.6f; s.NeckLen = 1.5f; s.WingsFolded = true; s.CrestMat = Mat.Accent; s.FeatherMat = Mat.Second; }),
        Pet("Gizmo", BodyPlan.Blob, SizeClass.Tiny, Attitude.Skittish, Element.Shade,
            new(HeadShape.Round, Ears.Pointy, EyeKind.Beady, Horns.None, Back.None, Limbs.Stubby, Tail.None, Wings.Bat, Pattern.Belly),
            PaletteSpec.FromHex("#a98be8", "#e2d6ff", "#4a3a6a", "#b79cf0"),
            s => { s.EyeSize = 1.3f; s.WingSpread = 0.9f; }),

        // Water & Oddballs
        Pet("Finnegan", BodyPlan.Aquatic, SizeClass.Medium, Attitude.Loyal, Element.Frost,
            new(HeadShape.Round, Ears.None, EyeKind.Round, Horns.None, Back.None, Limbs.Long, Tail.Fan, Wings.None, Pattern.Belly),
            PaletteSpec.FromHex("#aab4c8", "#e3e8f0", "#5a6070"),
            s => { s.EyeSize = 1.3f; s.Thickness = 0.95f; }),
        Pet("Gloop", BodyPlan.Blob, SizeClass.Small, Attitude.Proud, Element.Venom,
            new(HeadShape.Round, Ears.None, EyeKind.Round, Horns.None, Back.None, Limbs.Stubby, Tail.None, Wings.None, Pattern.Spots),
            PaletteSpec.FromHex("#b8e05a", "#e4f5a0", "#8a5ad0"),
            s => { s.EyeSize = 1.3f; s.BodyLen = 1.1f; }),
        Pet("Nessie", BodyPlan.Serpent, SizeClass.Huge, Attitude.Skittish, Element.Tide,
            new(HeadShape.Snout, Ears.None, EyeKind.Round, Horns.Antennae, Back.Sail, Limbs.Stubby, Tail.None, Wings.None, Pattern.Belly),
            PaletteSpec.FromHex("#6fd0a8", "#e6f5c0", "#e8d9a8"),
            s => { s.NeckLen = 1.4f; s.Thickness = 1.25f; s.SailMat = Mat.Second; }),

        // Dinos & Monsters
        Pet("Rumble", BodyPlan.Biped, SizeClass.Huge, Attitude.Aggressive, Element.Leaf,
            new(HeadShape.Snout, Ears.None, EyeKind.Round, Horns.None, Back.Spikes, Limbs.Clawed, Tail.Thick, Wings.None, Pattern.Belly),
            PaletteSpec.FromHex("#8fd162", "#eef7b0", "#f5a43c"),
            s => { s.SnoutLen = 1.3f; s.SpikeMat = Mat.Accent; s.HeadSize = 1.1f; }),
        Pet("Trixie", BodyPlan.Quadruped, SizeClass.Huge, Attitude.Proud, Element.Venom,
            new(HeadShape.Snout, Ears.Round, EyeKind.Round, Horns.TwinHorns, Back.None, Limbs.Stubby, Tail.Thick, Wings.None, Pattern.Spots),
            PaletteSpec.FromHex("#b49ae8", "#f5a3c7", "#f0e6c8"),
            s => { s.HornCurve = -0.7f; s.NeckLen = 0.6f; s.BodyLen = 1.1f; }),
        Pet("Gummy", BodyPlan.Biped, SizeClass.Small, Attitude.Loyal, Element.Ember,
            new(HeadShape.Snout, Ears.Round, EyeKind.Round, Horns.None, Back.None, Limbs.Stubby, Tail.None, Wings.None, Pattern.Markings),
            PaletteSpec.FromHex("#ff7070", "#ffb0b0", "#c04050"),
            s => { s.SnoutLen = 0.7f; s.MuzzleSecond = true; s.Thickness = 1.15f; }),
        Pet("Sparkle", BodyPlan.Quadruped, SizeClass.Large, Attitude.Proud, Element.Meadow,
            new(HeadShape.Snout, Ears.Pointy, EyeKind.Round, Horns.BrowHorn, Back.Mane, Limbs.Hooved, Tail.Fluffy, Wings.None, Pattern.Markings),
            PaletteSpec.FromHex("#fbd3ea", "#c7a6f5", "#ffe27a"),
            s => { s.NeckLen = 1.35f; s.SnoutLen = 1.2f; s.LegLen = 1.15f; s.TuftMat = Mat.Second; s.Thickness = 0.9f; }),
        Pet("Ember", BodyPlan.Quadruped, SizeClass.Small, Attitude.Aggressive, Element.Ember,
            new(HeadShape.Snout, Ears.None, EyeKind.Round, Horns.None, Back.Spikes, Limbs.Stubby, Tail.Whip, Wings.None, Pattern.Spots),
            PaletteSpec.FromHex("#f5a04a", "#ffe07a", "#ff5a2a"),
            s => { s.BodyLen = 1.25f; s.LegLen = 0.8f; s.NeckLen = 0.6f; s.SpikeMat = Mat.Accent; s.TuftMat = Mat.Accent; }),
        Pet("Crystal", BodyPlan.Biped, SizeClass.Large, Attitude.Loyal, Element.Stone,
            new(HeadShape.Wide, Ears.None, EyeKind.Beady, Horns.Crest, Back.Crystals, Limbs.Stubby, Tail.None, Wings.None, Pattern.None),
            PaletteSpec.FromHex("#a99ad0", "#8a7ab8", "#8fe6e0"),
            s => { s.CrestMat = Mat.Accent; s.Thickness = 1.25f; }),
    };
}
