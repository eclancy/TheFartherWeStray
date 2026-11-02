using Godot;

namespace CreatureTactics.Creatures;

/// <summary>
/// The front door to creature drawing. Everything is drawn by <see cref="CreatureSculptor"/> in
/// FFTA's view; this keeps the old entry points: a sprite facing down-left, and the per-species
/// choice of which palette colour each part is painted in.
/// </summary>
public static class CreaturePainter
{
    /// <summary>The creature facing down-left (its front view), trimmed to what was painted.</summary>
    public static Image Paint(Creature creature, bool fourColor) => CreatureSculptor.Render(creature, fourColor, View.Front).img;

    /// <summary>
    /// Which palette material each of a species' parts is coloured in: pinned on the species, or
    /// rolled from its seed. Breeding copies these so a child's spikes keep their parent's colour.
    /// </summary>
    public static (Mat spike, Mat shell, Mat feather, Mat sail, Mat tuft, Mat crest) StyleOf(Species sp)
    {
        var rng = new SeededRng(SeededRng.Derive(sp.Seed, 0x5EED));
        return (
            sp.SpikeMat ?? rng.Pick(new[] { Mat.Accent, Mat.Second, Mat.Accent }),
            sp.ShellMat ?? rng.Pick(new[] { Mat.Second, Mat.Accent }),
            sp.FeatherMat ?? rng.Pick(new[] { Mat.Main, Mat.Second }),
            sp.SailMat ?? rng.Pick(new[] { Mat.Second, Mat.Membrane }),
            sp.TuftMat ?? rng.Pick(new[] { Mat.Second, Mat.Main }),
            sp.CrestMat ?? rng.Pick(new[] { Mat.Second, Mat.Accent, Mat.Main }));
    }
}
