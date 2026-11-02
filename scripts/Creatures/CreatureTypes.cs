namespace CreatureTactics.Creatures;

// The vocabulary of a creature. See docs/game-ideas.md, sections 1 and 4: body plans are the
// FFTA "race", parts are the equipment slots, size sets the grid footprint and attitude becomes
// the reaction ability and the recruit condition.

public enum BodyPlan { Quadruped, Biped, Serpent, Avian, Insectoid, Aquatic, Blob }

public enum SizeClass { Tiny, Small, Medium, Large, Huge }

public enum Attitude { Aggressive, Skittish, Loyal, Curious, Proud }

/// <summary>Palette family. Doubles as the element until elements are designed.</summary>
public enum Element { Ember, Tide, Leaf, Spark, Stone, Venom, Frost, Shade, Meadow }

public enum HeadShape { Round, Snout, Wide }

public enum Ears { None, Pointy, Round, Floppy }

public enum EyeKind { Round, Beady, Compound }

public enum Horns { None, NoseHorn, BrowHorn, TwinHorns, Antlers, Crest, Antennae }

public enum Back { None, Spikes, Shell, Sail, Mane, Crystals }

public enum Limbs { Stubby, Long, Clawed, Hooved }

public enum Tail { None, Whip, Thick, Fluffy, Fan, Club, Curl }

public enum Wings { None, Bat, Feather, Insect, Tiny }

public enum Pattern { None, Belly, Stripes, Spots, Saddle, Tips, Cheeks }

/// <summary>Every part slot of one creature. A species fixes these; a mutation can swap one.</summary>
public record struct PartSet(
    HeadShape Head, Ears Ears, EyeKind Eyes, Horns Horns, Back Back,
    Limbs Limbs, Tail Tail, Wings Wings, Pattern Pattern);

public static class SizeInfo
{
    /// <summary>Nominal sprite size in pixels; a medium creature fills a Gen 3 sized (64 px) sprite.</summary>
    public static int Pixels(SizeClass s) => s switch
    {
        SizeClass.Tiny => 36,
        SizeClass.Small => 46,
        SizeClass.Medium => 56,
        SizeClass.Large => 68,
        _ => 86,
    };

    /// <summary>Small creatures get bigger heads and eyes: the baby schema that makes them cute.</summary>
    public static float HeadScale(SizeClass s) => s switch
    {
        SizeClass.Tiny => 1.3f,
        SizeClass.Small => 1.15f,
        SizeClass.Medium => 1.0f,
        SizeClass.Large => 0.92f,
        _ => 0.85f,
    };

    public static string Footprint(SizeClass s) => s switch
    {
        SizeClass.Tiny => "shares a tile",
        SizeClass.Huge => "2x2 tiles",
        _ => "1 tile",
    };
}

public static class AttitudeInfo
{
    /// <summary>The battle reaction ability (game-ideas.md section 4).</summary>
    public static string Reaction(Attitude a) => a switch
    {
        Attitude.Aggressive => "counterattacks",
        Attitude.Skittish => "dodges and retreats a tile",
        Attitude.Loyal => "takes hits for adjacent allies",
        Attitude.Curious => "grabs items within reach",
        _ => "stronger when fighting alone",
    };

    /// <summary>How to recruit it in battle.</summary>
    public static string Recruit(Attitude a) => a switch
    {
        Attitude.Aggressive => "survive its attacks for 3 turns",
        Attitude.Skittish => "corner it so it can't flee",
        Attitude.Loyal => "befriend its packmates first",
        Attitude.Curious => "lure it with an item",
        _ => "beat it with a single unit",
    };
}
