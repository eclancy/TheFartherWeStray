namespace TheFartherWeStray.Creatures;

// The vocabulary of a creature. See docs/game-ideas.md, sections 1 and 4: body plans are the
// FFTA "race", parts are the equipment slots, size sets the grid footprint and attitude becomes
// the reaction ability and the recruit condition.

public enum BodyPlan { Quadruped, Biped, Serpent, Avian, Insectoid, Aquatic, Blob }

/// <summary>
/// How big a creature is. Wild ones are tiny to huge; a runt or an enormous one only comes from
/// breeding two particularly small or large parents (the user's call, 2026-11-07). Saves store the
/// name, so the order can grow at either end.
/// </summary>
public enum SizeClass { Runt, Tiny, Small, Medium, Large, Huge, Enormous }

/// <summary>
/// The kind of beast within a body plan: its silhouette and proportions (a lean hound, a long-legged
/// stag, a drake, a coiled wyrm, a crane, a mantis, a wisp). Auto means "roll it from the seed", for
/// species saved before forms existed.
/// </summary>
public enum Form
{
    Auto,
    Hound, Stag, Cat, Drake, Behemoth,       // quadrupeds
    Raptor, Sentinel, Wraith, Imp,           // bipeds
    Wyrm, Lung,                              // serpents
    Hawk, Crane, Phoenix,                    // avians
    Mantis, Beetle, Spider,                  // insectoids
    Eel, Ray, Kelpie,                        // aquatics
    Wisp, Jelly, Ooze,                       // blobs
    // Added 2026-11-02; rolled only for some new species, so older ones keep their forms.
    Hydra, Gargoyle, Naga, Scorpion, Angler, Watcher,
    Cockatrice,
}

/// <summary>A mystical flourish on top of the body: a halo, floating shards, a sigil, a third eye, trailing wisps.</summary>
public enum Aura { Auto, None, Halo, Shards, Sigil, ThirdEye, Wisps }

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

public enum Pattern { None, Belly, Stripes, Spots, Saddle, Tips, Markings }

/// <summary>
/// What a species is, beyond its anatomy (the Arcane-style setting): an ordinary beast, a clockwork
/// graft from a Piltover-style lab, a Zaun-style chemtech mutant full of neon liquid, an arcane creature
/// marked with runes, or an abyssal horror.
/// </summary>
public enum Strain { Natural, Clockwork, Chemtech, Arcane, Abyssal }

/// <summary>
/// What the player reads for a strain. Arcane is the look, not the words: no names from it in the game.
/// The enum keeps its old names because saves store them.
/// </summary>
public static class Strains
{
    public static string Label(this Strain s) => s switch { Strain.Chemtech => "Chemborn", Strain.Arcane => "Runic", _ => s.ToString() };
}

/// <summary>Grafts and marks a strain adds on top of the parts.</summary>
[System.Flags]
public enum Graft
{
    None = 0, Plating = 1, MetalLimb = 2, Visor = 4, Core = 8, Canister = 16, Veins = 32, Drips = 64,
    Runes = 128, GlowTips = 256, ExtraEyes = 512, Cracks = 1024,
    // Steampunk work (2026-11-02): exposed gears or copper pipework on the flank, pistons or a smokestack on the back.
    Gears = 2048, Pistons = 4096, Smokestack = 8192, Pipework = 16384,
}

public enum Weapon { Bite, Claws, Horns, Club, Whip, Hooves }

public enum Armor { Hide, Shell, Spines, Crystal, Mane }

public enum MoveType { Walk, Fly, Swim, Climb }

/// <summary>Every part slot of one creature. A species fixes these; a mutation can swap one.</summary>
public record struct PartSet(
    HeadShape Head, Ears Ears, EyeKind Eyes, Horns Horns, Back Back,
    Limbs Limbs, Tail Tail, Wings Wings, Pattern Pattern);

/// <summary>What a size does: its stats in battle and how big it's drawn.</summary>
/// <param name="Hp">Max HP multiplier.</param>
/// <param name="Evade">How much harder it is to hit, in percent.</param>
/// <param name="Pixels">Nominal sprite size; a medium creature fills a Gen 3 sized (64 px) sprite.</param>
/// <param name="Head">Head size, kept close to 1: big heads read as cute, and these should read as dangerous.</param>
/// <param name="Tiles">Its footprint's side in tiles.</param>
public readonly record struct SizeStats(float Hp, int Atk, int Spd, int Move, int Evade, int Pixels, float Head, int Tiles);

public static class SizeInfo
{
    /// <summary>
    /// Each size's stats. The user's calls (2026-11-07): a smaller HP swing than before (tiny was x0.6,
    /// huge x1.6), tiny harder to hit, and the breed-only sizes past each end carry HP, ATK and SPD on
    /// while keeping the end's own extra: a runt evades like a tiny one, an enormous one is slow like a
    /// huge one. Huge and enormous creatures take 2x2 tiles.
    /// </summary>
    public static SizeStats Stats(SizeClass s) => s switch
    {
        SizeClass.Runt => new(0.7f, -3, 3, 0, 10, 30, 1.08f, 1),
        SizeClass.Tiny => new(0.8f, -2, 2, 0, 10, 36, 1.05f, 1),
        SizeClass.Small => new(0.9f, -1, 1, 0, 0, 46, 1.0f, 1),
        SizeClass.Medium => new(1f, 0, 0, 0, 0, 56, 0.94f, 1),
        SizeClass.Large => new(1.12f, 1, -1, 0, 0, 68, 0.88f, 1),
        SizeClass.Huge => new(1.25f, 2, -2, -1, 0, 86, 0.82f, 2),
        _ => new(1.4f, 3, -3, -1, 0, 102, 0.78f, 2),
    };

    public static int Pixels(SizeClass s) => Stats(s).Pixels;
    public static float HeadScale(SizeClass s) => Stats(s).Head;
    /// <summary>How many tiles a side its footprint is: 2 for huge and enormous creatures.</summary>
    public static int Tiles(SizeClass s) => Stats(s).Tiles;

    /// <summary>The sizes past each end, which only breeding makes.</summary>
    public static bool BredOnly(SizeClass s) => s is SizeClass.Runt or SizeClass.Enormous;

    /// <summary>Its size as a word on screen.</summary>
    public static string Word(SizeClass s) => s.ToString().ToLowerInvariant();

    /// <summary>Steps from tiny (0) to huge (4), as breeding has always counted them; a runt is -1, an enormous one 5.</summary>
    public static int Step(SizeClass s) => (int)s - (int)SizeClass.Tiny;
    public static SizeClass FromStep(int step) => (SizeClass)System.Math.Clamp(step + (int)SizeClass.Tiny, (int)SizeClass.Runt, (int)SizeClass.Enormous);

    public static string Footprint(SizeClass s) => Tiles(s) > 1 ? $"{Tiles(s)}x{Tiles(s)} tiles" : "1 tile";

    /// <summary>What it does in battle, in short: for tooltips.</summary>
    public static string Describe(SizeClass s)
    {
        var st = Stats(s);
        var bits = new System.Collections.Generic.List<string>();
        if (st.Hp != 1) bits.Add($"max HP x{st.Hp:0.##}");
        if (st.Atk != 0) bits.Add($"ATK {st.Atk:+0;-0}");
        if (st.Spd != 0) bits.Add($"SPD {st.Spd:+0;-0}");
        if (st.Move != 0) bits.Add($"MOVE {st.Move:+0;-0}");
        if (st.Evade != 0) bits.Add($"{st.Evade}% harder to hit");
        if (st.Tiles > 1) bits.Add($"takes {Footprint(s)}");
        string word = Word(s);
        return $"{char.ToUpperInvariant(word[0])}{word[1..]}: {(bits.Count == 0 ? "no change to its stats" : string.Join(", ", bits))}."
            + (BredOnly(s) ? " Only breeding makes one." : "");
    }
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
