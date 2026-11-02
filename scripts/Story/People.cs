using System;
using System.Collections.Generic;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Story;

public enum Hat { None, Hood, Cap, Band, TopHat, WideBrim, Crown, Circlet }
public enum Hair { Mohawk, Crest, Liberty, Spikes, Undercut, HalfShaved, Long, Twintails, Dreads, Buns, Horns, Pompadour, Shaved }
public enum Garb { Jacket, Vest, Coat, Robe }
/// <summary>How someone stands: attitude before anything is said (the user wanted people stylized and punk, not rigid).</summary>
public enum Stance { Swagger, Crossed, Slouch, Ready, Showoff, Regal }

/// <summary>
/// How a person looks: colours (as a creature palette: coat, trim, skin, hair, metal, glow, rolled the
/// way creatures' are so people and creatures belong together), hair, a hat, a garb, a stance, and the
/// details: punk gear and body mods in the undercity, armour and finery in the city above. Rolled from
/// an id so the same person always looks the same; the story's characters will want designed looks.
/// </summary>
public sealed class PersonLook
{
    public Hsv Coat, Trim, Skin, HairColor, Metal, Glow;
    public Hair Hair;
    public bool HairTips;          // neon-dipped ends
    public Hat Hat;
    public Garb Garb;
    public Stance Stance;
    // Street gear.
    public bool Collar, Studs, Spaulder, Gauntlet, Chain, Mask, Scarf, Tails, Goggles;
    // Body mods and ink (the neon-punk undercity).
    public bool Tattoo, Veins, MechArm, ChemTank, EyeImplant, Piercings, Ridge;
    // Finery (the city above).
    public bool Breastplate, Pauldrons, Cape, Sash, Ruff, Gloves, Buttons;
    public float Height = 1, Build = 1;

    public CreaturePalette Palette() => new(Coat, Trim, Skin, HairColor, Glow, Metal);
}

/// <summary>The people of the country, for cutscenes: tamers, rivals, townsfolk. Looks by id, styled by side.</summary>
public static class People
{
    /// <summary>
    /// A person's look, the same every time for the same id. The undercity is neon-punk (wild hair,
    /// sleeveless vests, glowing tattoos, mechanical arms, chem tanks, eye implants); the city above is
    /// royal (silver, gold and blues, breastplates, capes, crowns); the wilds are scavengers (hoods,
    /// scarves, ragged tails, war paint). Colours come from the creatures' own palette roller.
    /// </summary>
    public static PersonLook Look(string id, World.Side side = World.Side.Wilds)
    {
        var rng = new SeededRng(SeededRng.Hash("person:" + id));
        bool under = side == World.Side.Undercity, city = side == World.Side.City;
        var look = new PersonLook
        {
            Skin = new Hsv(rng.Range(0.03f, 0.09f), rng.Range(0.25f, 0.6f), rng.Range(0.36f, 0.95f)),
            Height = rng.Range(0.94f, 1.08f),
            Build = rng.Range(0.92f, 1.15f),
        };
        var natural = rng.Weighted(
            (new Hsv(0.75f, 0.2f, 0.12f), 3f), (new Hsv(0.07f, 0.55f, rng.Range(0.25f, 0.45f)), 3f),
            (new Hsv(0.03f, 0.7f, 0.55f), 1.2f), (new Hsv(0.12f, 0.4f, 0.88f), 1.2f), (new Hsv(0.6f, 0.05f, 0.9f), 1f));

        if (city)
        {
            // Royal: blues, white, violet and crimson, trimmed in gold or silver.
            look.Coat = rng.Pick(new[] { new Hsv(0.62f, 0.7f, 0.52f), new Hsv(0.64f, 0.6f, 0.32f), new Hsv(0.6f, 0.06f, 0.94f), new Hsv(0.75f, 0.5f, 0.45f), new Hsv(0.98f, 0.7f, 0.48f), new Hsv(0.55f, 0.55f, 0.6f) });
            look.Trim = rng.Pick(new[] { new Hsv(0.1f, 0.08f, 0.93f), new Hsv(0.64f, 0.5f, 0.24f), new Hsv(0.75f, 0.12f, 0.14f) });
            bool gold = rng.Chance(0.6f);
            look.Metal = gold ? new Hsv(0.12f, 0.66f, 0.92f) : new Hsv(0.6f, 0.08f, 0.88f);
            look.Glow = gold ? new Hsv(0.13f, 0.4f, 1f) : new Hsv(0.55f, 0.55f, 1f);
            look.HairColor = natural;
            look.Hair = rng.Weighted((Hair.Pompadour, 1.5f), (Hair.Long, 2.5f), (Hair.Undercut, 2f), (Hair.Buns, 1.5f), (Hair.Twintails, 0.8f), (Hair.Crest, 0.4f));
            look.Hat = rng.Weighted((Hat.None, 3f), (Hat.Circlet, 1.5f), (Hat.Crown, 0.5f), (Hat.TopHat, 1.5f));
            look.Garb = rng.Weighted((Garb.Coat, 3f), (Garb.Jacket, 2f), (Garb.Robe, 0.6f));
            look.Stance = rng.Weighted((Stance.Regal, 3f), (Stance.Swagger, 1.5f), (Stance.Crossed, 1.5f), (Stance.Showoff, 0.6f));
            look.Breastplate = rng.Chance(0.45f); look.Pauldrons = rng.Chance(0.55f); look.Cape = rng.Chance(0.55f);
            look.Sash = rng.Chance(0.4f); look.Ruff = rng.Chance(0.3f); look.Gloves = rng.Chance(0.6f); look.Buttons = rng.Chance(0.7f);
            look.Collar = !look.Ruff && rng.Chance(0.5f); look.Goggles = rng.Chance(0.15f); look.Tails = look.Garb == Garb.Coat && rng.Chance(0.4f);
        }
        else
        {
            // The creatures' palettes: an element's colours for the clothes, and its neon for the glow.
            var element = under ? rng.Pick(new[] { Element.Venom, Element.Spark, Element.Shade, Element.Ember, Element.Tide })
                                : rng.Pick(new[] { Element.Leaf, Element.Meadow, Element.Stone, Element.Frost, Element.Tide });
            var spec = PaletteSpec.Roll(element, rng);
            look.Coat = new Hsv(spec.Main.H, Mathf.Clamp(spec.Main.S + 0.1f, 0, 1), Mathf.Clamp(spec.Main.V, 0.36f, 0.68f));
            look.Trim = new Hsv(spec.Second.H, spec.Second.S * 0.7f, Mathf.Min(spec.Second.V * 0.45f, 0.3f));
            look.Metal = spec.Metal;
            look.Glow = spec.Glow;
            var neon = new Hsv(rng.Chance(0.5f) ? spec.Glow.H : rng.Pick(new[] { 0.87f, 0.3f, 0.52f, 0.78f, 0.02f, 0.14f }), 0.85f, 0.95f);
            look.HairColor = rng.Chance(under ? 0.7f : 0.15f) ? neon : rng.Chance(0.3f) ? spec.Membrane : natural;
            look.HairTips = rng.Chance(under ? 0.45f : 0.1f);
            if (under)
            {
                look.Hair = rng.Weighted((Hair.Mohawk, 2f), (Hair.Crest, 2f), (Hair.Liberty, 2f), (Hair.Spikes, 1.5f), (Hair.HalfShaved, 1.5f), (Hair.Dreads, 1.2f), (Hair.Twintails, 1.2f), (Hair.Horns, 1f), (Hair.Pompadour, 0.8f), (Hair.Shaved, 0.6f));
                look.Hat = rng.Weighted((Hat.None, 6f), (Hat.Hood, 1f), (Hat.Cap, 0.8f), (Hat.Band, 1.4f));
                look.Garb = rng.Weighted((Garb.Vest, 2.5f), (Garb.Jacket, 2.5f), (Garb.Coat, 1.5f));
                look.Stance = rng.Weighted((Stance.Swagger, 2.5f), (Stance.Slouch, 2.5f), (Stance.Ready, 2f), (Stance.Crossed, 1.5f), (Stance.Showoff, 1.5f));
                look.Collar = rng.Chance(0.5f); look.Studs = rng.Chance(0.55f); look.Spaulder = rng.Chance(0.4f);
                look.Gauntlet = rng.Chance(0.25f); look.Chain = rng.Chance(0.55f); look.Mask = rng.Chance(0.3f);
                look.Scarf = rng.Chance(0.2f); look.Goggles = rng.Chance(0.3f);
                look.Tattoo = rng.Chance(0.75f); look.Veins = rng.Chance(0.4f); look.MechArm = rng.Chance(0.3f);
                look.ChemTank = rng.Chance(0.3f); look.EyeImplant = rng.Chance(0.35f); look.Piercings = rng.Chance(0.65f); look.Ridge = rng.Chance(0.3f);
                look.Tails = look.Garb == Garb.Coat && rng.Chance(0.7f);
            }
            else
            {
                look.Hair = rng.Weighted((Hair.Long, 2f), (Hair.Dreads, 1.5f), (Hair.Spikes, 1.5f), (Hair.HalfShaved, 1f), (Hair.Mohawk, 1f), (Hair.Buns, 1f), (Hair.Twintails, 0.8f), (Hair.Shaved, 0.8f));
                look.Hat = rng.Weighted((Hat.None, 3f), (Hat.Hood, 2.5f), (Hat.WideBrim, 1.2f), (Hat.Band, 1f));
                look.Garb = rng.Weighted((Garb.Jacket, 2f), (Garb.Coat, 2.5f), (Garb.Vest, 1f), (Garb.Robe, 0.6f));
                look.Stance = rng.Weighted((Stance.Swagger, 2f), (Stance.Crossed, 2f), (Stance.Slouch, 1.5f), (Stance.Ready, 1f), (Stance.Showoff, 0.8f));
                look.Collar = rng.Chance(0.35f); look.Studs = rng.Chance(0.25f); look.Spaulder = rng.Chance(0.25f);
                look.Mask = rng.Chance(0.3f); look.Scarf = rng.Chance(0.5f); look.Tattoo = rng.Chance(0.35f);
                look.Chain = rng.Chance(0.2f); look.Piercings = rng.Chance(0.3f);
                look.Tails = look.Garb == Garb.Coat && rng.Chance(0.5f);
            }
        }
        if (look.Hat is Hat.Hood or Hat.Cap && look.Hair is not (Hair.Long or Hair.Dreads or Hair.Shaved)) look.Hair = Hair.Long;
        return look;
    }
}

/// <summary>
/// Draws a person the way <see cref="CreatureSculptor"/> draws a creature: a small 3D figure ray-cast
/// into pixels from FFTA's camera, shaded in four tones and outlined. Stylized, not lifelike: long legs,
/// a V of a torso, chunky boots, a big head and loud hair, the whole body leaning into its stance
/// (weight on one leg, hips against shoulders). Poses move it as they move creatures; the idle shifts
/// weight foot to foot while hair, scarves, capes and coat tails sway.
/// </summary>
public sealed class PersonSculptor
{
    readonly PersonLook _l;
    readonly PixelCanvas _c;
    readonly float u, _phase;
    readonly Pose _pose;
    readonly bool _blink;
    int _part;
    Vector3 _headC;
    float _headR, _hipZ, _lean, _side;
    readonly List<Vector3> _ink = new();    // points on visible skin where tattoos and veins glow
    Vector3? _trailRoot;                     // a hanging part being drawn: where it hangs from
    static readonly Vector3 X = new(1, 0, 0), Y = new(0, 1, 0), Z = new(0, 0, 1);
    const int Near = -1; // -Y faces the camera in both views (the other diagonals are mirrors)

    public static (Image img, Vector2I anchor) Render(PersonLook look, View view, float phase = -1, bool blink = false, Pose pose = Pose.Idle)
    {
        var s = new PersonSculptor(look, view, phase, blink, pose);
        s.Build();
        var img = s._c.Resolve(look.Palette(), false);
        var used = img.GetUsedRect();
        var origin = s._c.Cam.Origin;
        if (used.Size.X == 0) return (img, new Vector2I(Mathf.RoundToInt(origin.X), Mathf.RoundToInt(origin.Y)));
        return (img.GetRegion(used), new Vector2I(Mathf.RoundToInt(origin.X) - used.Position.X, Mathf.RoundToInt(origin.Y) - used.Position.Y));
    }

    PersonSculptor(PersonLook look, View view, float phase, bool blink, Pose pose)
    {
        _l = look;
        _phase = phase;
        _pose = pose;
        _blink = blink || phase >= 0 && (pose == Pose.Hurt && phase < 0.6f || pose == Pose.Faint && phase > 0.3f);
        float S = Mathf.Round(SizeInfo.Pixels(SizeClass.Medium) * ArtStyle.Current.SizeScale);
        u = S / 56f * 1.4f * look.Height;
        int w = Mathf.CeilToInt(S * 2.2f), h = Mathf.CeilToInt(S * 2.2f);
        _c = new PixelCanvas(w, h);
        _c.Begin3D(new Camera3(view, new Vector2(w / 2f, h * 0.84f)));
        // How the stance carries the body: leaning back with swagger, hunched forward in a slouch or a crouch.
        _lean = look.Stance switch { Stance.Swagger => -0.14f, Stance.Showoff => -0.12f, Stance.Crossed => -0.08f, Stance.Regal => -0.06f, Stance.Slouch => 0.2f, Stance.Ready => 0.16f, _ => 0 };
        _side = look.Stance switch { Stance.Swagger => 0.08f, Stance.Showoff => -0.06f, Stance.Crossed => 0.05f, _ => 0 };
    }

    bool Animate => _phase >= 0;
    float Wave(float lag = 0, float speed = 1) => Animate ? Mathf.Sin(Mathf.Tau * (_phase * speed - lag)) : 0;
    float Rise => Animate ? 0.5f - 0.5f * Mathf.Cos(Mathf.Tau * _phase) : 0;

    // How far through each action, 0 when not in it (a strike winds back below 0 before it lands).
    float Strike => _pose == Pose.Strike && Animate ? (_phase < 0.3f ? -_phase / 0.3f * 0.5f : _phase < 0.55f ? Mathf.Lerp(-0.5f, 1, (_phase - 0.3f) / 0.25f) : 1 - (_phase - 0.55f) / 0.45f) : 0;
    float Cast => _pose == Pose.Cast && Animate ? Mathf.Sin(Mathf.Pi * Mathf.Clamp(_phase * 1.15f, 0, 1)) : 0;
    float Hurt => _pose == Pose.Hurt && Animate ? (_phase < 0.25f ? _phase / 0.25f : 1 - (_phase - 0.25f) / 0.75f) : 0;
    float Faint => _pose == Pose.Faint && Animate ? Mathf.Clamp(_phase * 1.4f, 0, 1) : 0;
    float Stride => _pose == Pose.Walk && Animate ? Mathf.Sin(Mathf.Tau * _phase) : 0;

    /// <summary>
    /// The stance's lean (everything above the hips tips forward or back, and a little sideways), the
    /// pose's sway on top (more the higher up, feet planted), and trailing parts swinging behind.
    /// </summary>
    Vector3 Warp(Vector3 p)
    {
        var o = p;
        float above = p.Z - _hipZ;
        if (above > 0)
        {
            float lean = _pose == Pose.Walk ? _lean * 0.4f + 0.08f : _lean;
            o.X += above * Mathf.Sin(lean);
            o.Z -= above * (1 - Mathf.Cos(lean));
            o.Y += above * Mathf.Sin(_side);
        }
        if (Animate)
        {
            float up = Mathf.Clamp((p.Z - 3 * u) / (24 * u), 0, 1);
            float head = _headR > 0 ? Mathf.Clamp(1 - ((p - _headC).Length() - _headR * 1.3f) / _headR, 0, 1) : 0;
            switch (_pose)
            {
                case Pose.Idle:
                    o += Z * 0.7f * u * Rise * Mathf.Clamp((p.Z - 12 * u) / (8 * u), 0, 1);
                    o += Y * 0.55f * u * Wave() * up;                            // weight shifting foot to foot
                    o += (Z * 0.4f * u * Wave(0.25f) + X * 0.35f * u * Wave(0.3f)) * head;
                    break;
                case Pose.Walk:
                    o += Z * 1.0f * u * (0.5f - 0.5f * Mathf.Cos(Mathf.Tau * 2 * _phase)) * up;
                    o += Y * 0.6f * u * Mathf.Sin(Mathf.Tau * _phase) * up;      // a swagger in the walk
                    break;
                case Pose.Strike:
                    o += X * Strike * 3f * u * up - Z * Mathf.Max(Strike, 0) * 0.9f * u * up;
                    break;
                case Pose.Cast:
                    o += Z * Cast * 1.3f * u * up - X * Cast * 1.2f * u * up;
                    break;
                case Pose.Hurt:
                    o += (-X * 2.6f * u - Z * 0.6f * u) * Hurt * up + Y * 0.8f * u * Hurt * up;
                    break;
                case Pose.Faint:
                    o.Z = o.Z * (1 - 0.34f * Faint * up);
                    o += X * 2.4f * u * Faint * up * up;
                    break;
            }
        }
        if (_trailRoot is { } root)
        {
            float d = Mathf.Max(0, (root - p).Length() / (8 * u));
            float swing = Animate ? (_pose == Pose.Walk ? 1.6f : 0.8f) * Mathf.Sin(Mathf.Tau * (_phase - d * 0.25f)) : 0;
            o += (Y * swing * u + X * (_pose == Pose.Walk ? -1.2f : 0) * u) * d;
        }
        return o;
    }

    int Part() => ++_part;

    void Build()
    {
        _c.Warp = Warp;
        float b = _l.Build;
        // Weight on one leg: the hips shift toward it and the shoulders tilt the other way.
        float shift = _l.Stance switch { Stance.Swagger => 1.6f, Stance.Showoff => 1.3f, Stance.Slouch => 1.0f, Stance.Crossed => 0.8f, Stance.Regal => 0.3f, _ => 0 } * u;
        bool crouch = _l.Stance == Stance.Ready && _pose is Pose.Idle;
        bool wide = _l.Stance == Stance.Ready || _pose == Pose.Strike;
        _hipZ = (crouch ? 12.6f : 14.6f) * u;
        var hip = new Vector3(0, shift, _hipZ);
        var chest = hip + new Vector3(_l.Stance == Stance.Regal ? 0.4f * u : 0, -shift * 1.5f, 6.0f * u);
        float tilt = shift * 0.6f;

        if (_l.Cape) Cape(chest, b);
        if (_l.ChemTank) ChemTank(chest);
        Legs(hip, wide, crouch, b);
        if (_l.Garb == Garb.Robe) _c.Ellipsoid(new Vector3(-0.2f * u, hip.Y * 0.5f, 9.5f * u), X, 3.6f * u * b, 3.9f * u * b, 8.6f * u, Mat.Main, Part(), 0, 0.6f * u);
        if (_l.Garb == Garb.Coat) CoatTails(hip, b);

        // Torso: a narrow waist under a broad chest, the belt and its buckle.
        int torso = Part();
        _c.Ellipsoid(hip + new Vector3(0, 0, 1.6f * u), X, 2.2f * u * b, 2.7f * u * b, 2.8f * u, Mat.Main, torso);
        _c.Ellipsoid(chest, X, 2.6f * u * b, 4.1f * u * b, 3.6f * u, Mat.Main, torso);
        _c.Ellipsoid(hip + new Vector3(0.1f * u, 0, 0.4f * u), X, 2.35f * u * b, 2.85f * u * b, 0.75f * u, Mat.Second, Part(), -0.1f);
        _c.Box(hip + new Vector3(2.25f * u * b, 0, 0.4f * u), new Vector3(0.35f * u, 0.8f * u, 0.6f * u), Mat.Metal, Part(), 0.3f);
        Finery(hip, chest, b);
        if (_l.Chain)
        {
            var a = hip + new Vector3(1.4f * u, Near * 2.5f * u * b, 0.3f * u);
            var c = hip + new Vector3(-1.2f * u, Near * 2.8f * u * b, 0.2f * u);
            _c.Tube(new[] { a, (a + c) / 2 + new Vector3(0, Near * 0.5f * u, -2.2f * u), c }, 0.45f * u, 0.45f * u, Mat.Metal, Part(), 0.3f);
        }
        if (_l.Studs && !_l.Breastplate)
            for (int i = 0; i < 3; i++)
                _c.Sphere(chest + new Vector3(1.7f * u * b, Near * (1.2f + i * 0.9f) * u, (1.4f - i * 0.6f) * u), 0.45f * u, Mat.Metal, Part(), 0.4f);
        if (_l.Ridge)
            for (int i = 0; i < 3; i++)
            {
                var at = chest + new Vector3(-2.4f * u * b, 0, (1.8f - i * 1.8f) * u);
                _c.Tube(new[] { at, at + new Vector3(-1.8f * u, 0, 1.2f * u) }, 0.75f * u, 0.45f * u, Mat.Metal, Part(), 0.25f);
            }

        Arms(chest, tilt, b);
        HeadAndHair(chest);
    }

    void Legs(Vector3 hip, bool wide, bool crouch, float b)
    {
        foreach (int side in new[] { -Near, Near })
        {
            float swing = side * Stride;
            bool weight = (side == Near) == (hip.Y < 0) || _l.Stance is Stance.Ready or Stance.Regal;
            float spread = (wide ? 3.8f : weight ? 1.4f : 3.0f) * u;
            var top = hip + new Vector3(0, side * 1.6f * u * b, -0.4f * u);
            var ankle = new Vector3(2.4f * u * swing + (weight ? 0 : 0.9f * u), side * spread + hip.Y * (weight ? 0.8f : 0.2f), 1.7f * u + (Stride != 0 ? Mathf.Max(0, swing) * 1.4f * u : 0));
            if (_pose == Pose.Faint) ankle += X * (side == Near ? 2.2f : -0.6f) * u * Faint;
            // Knees bend out in a crouch, forward on the free leg.
            var knee = (top + ankle) / 2 + new Vector3((crouch ? 1.8f : weight ? 0.5f : 1.3f) * u, side * (crouch ? 1.2f : 0.3f) * u, 0);
            int leg = Part();
            _c.Tube(new[] { top, knee, ankle }, 1.5f * u * b, 1.15f * u, Mat.Second, leg);
            int boot = Part();
            var bootMat = _l.Breastplate || _l.Pauldrons && _l.Metal.S < 0.3f ? Mat.Metal : Mat.Second;
            _c.Tube(new[] { ankle + Z * 1.8f * u, ankle }, 1.5f * u, 1.35f * u, bootMat, boot, -0.25f);
            _c.Ellipsoid(ankle + new Vector3(0.9f * u, 0, -0.7f * u), X, 2.3f * u, 1.45f * u, 1.25f * u, bootMat, boot, -0.25f);
            _c.Sphere(ankle + new Vector3(2.6f * u, 0, -0.6f * u), 0.6f * u, Mat.Metal, boot, 0.2f);
        }
    }

    /// <summary>A long coat's tails, hanging open behind the legs, ragged at the hem if they're tails.</summary>
    void CoatTails(Vector3 hip, float b)
    {
        var top = hip + new Vector3(-1.2f * u, 0, 1.4f * u);
        _trailRoot = top;
        int teeth = _l.Tails ? 5 : 1;
        var outline = new List<Vector3> { top + Y * 2.8f * u * b };
        for (int i = 0; i <= teeth; i++)
        {
            float f = i / (float)teeth;
            float z = 4.2f * u + (_l.Tails && i % 2 == 1 ? 1.6f * u : 0);
            outline.Add(new Vector3(-2.6f * u, Mathf.Lerp(3.4f, -3.4f, f) * u * b, z));
        }
        outline.Add(top - Y * 2.8f * u * b);
        _c.Membrane(outline, Mat.Main, Part());
        _trailRoot = null;
    }

    /// <summary>A royal cape from the shoulders nearly to the ground, its hem trimmed in gold or silver.</summary>
    void Cape(Vector3 chest, float b)
    {
        var top = chest + new Vector3(-2.2f * u, 0, 2.0f * u);
        _trailRoot = top;
        var hem = new List<Vector3>();
        for (int i = 0; i <= 6; i++) hem.Add(new Vector3(-4.6f * u, Mathf.Lerp(5.0f, -5.0f, i / 6f) * u * b, 1.4f * u + (i % 2) * 0.6f * u));
        var outline = new List<Vector3> { top + Y * 3.8f * u * b };
        outline.AddRange(hem);
        outline.Add(top - Y * 3.8f * u * b);
        _c.Membrane(outline, Mat.Main, Part(), -0.15f);
        _c.Tube(hem, 0.55f * u, 0.55f * u, Mat.Metal, Part(), 0.2f);
        _trailRoot = null;
    }

    /// <summary>A tank of glowing chem on the back, piped into the neck.</summary>
    void ChemTank(Vector3 chest)
    {
        int tank = Part();
        var bottom = chest + new Vector3(-3.4f * u, 0, -3.4f * u);
        _c.Tube(new[] { bottom, bottom + Z * 6.4f * u }, 1.9f * u, 1.9f * u, Mat.Metal, tank);
        _c.Tube(new[] { bottom + Z * 1.6f * u, bottom + Z * 4.8f * u }, 1.95f * u, 1.95f * u, Mat.Glow, Part(), 0.1f, 0);
        var neck = chest + new Vector3(-1.2f * u, Near * 1.2f * u, 3.4f * u);
        _c.Tube(new[] { bottom + Z * 6.4f * u, bottom + new Vector3(0.4f * u, Near * 1.0f * u, 8.2f * u), neck }, 0.55f * u, 0.5f * u, Mat.Glow, Part(), 0.2f);
    }

    /// <summary>The city's finery: a breastplate with a gem, a sash, gold buttons.</summary>
    void Finery(Vector3 hip, Vector3 chest, float b)
    {
        if (_l.Breastplate)
        {
            int plate = Part();
            _c.Ellipsoid(chest + new Vector3(0.9f * u, 0, 0.2f * u), X, 2.0f * u * b, 3.7f * u * b, 3.2f * u, Mat.Metal, plate, 0.15f);
            _c.Sphere(chest + new Vector3(2.8f * u * b, 0, 0.6f * u), 0.7f * u, Mat.Glow, Part(), 0.4f, 0, true);
        }
        else if (_l.Buttons)
            for (int i = 0; i < 3; i++)
                _c.Sphere(chest + new Vector3(2.5f * u * b, Near * 0.6f * u, (1.6f - i * 1.6f) * u), 0.45f * u, Mat.Metal, Part(), 0.4f);
        if (_l.Sash)
            _c.Tube(new[] { chest + new Vector3(1.2f * u, -Near * 3.4f * u * b, 2.4f * u), chest + new Vector3(2.4f * u * b, 0, -0.6f * u), hip + new Vector3(1.4f * u, Near * 2.6f * u * b, 0.6f * u) }, 0.75f * u, 0.75f * u, Mat.Second, Part(), 0.1f);
    }

    void Arms(Vector3 chest, float tilt, float b)
    {
        foreach (int side in new[] { -Near, Near })
        {
            var shoulder = chest + new Vector3(0, side * 4.0f * u * b, 1.6f * u + (side == Near ? tilt : -tilt));
            if (_l.Stance == Stance.Slouch) shoulder += Z * 0.7f * u + X * 0.4f * u;
            Vector3 elbow, hand;
            bool onHip = _l.Stance == Stance.Swagger && side == Near || _l.Stance == Stance.Showoff && side != Near;
            switch (_l.Stance)
            {
                case Stance.Swagger or Stance.Showoff when onHip: // fist on the hip, elbow out
                    elbow = shoulder + new Vector3(-0.8f * u, side * 3.4f * u, -3.4f * u);
                    hand = chest + new Vector3(0.4f * u, side * 3.6f * u * b, -5.8f * u);
                    break;
                case Stance.Showoff: // a fist punched at the sky
                    elbow = shoulder + new Vector3(0.6f * u, side * 1.8f * u, 3.2f * u);
                    hand = shoulder + new Vector3(1.2f * u, side * 1.0f * u, 7.6f * u);
                    break;
                case Stance.Crossed: // arms folded across the chest
                    elbow = shoulder + new Vector3(1.6f * u, side * 0.5f * u, -4.4f * u);
                    hand = chest + new Vector3(2.9f * u * b, -side * 1.8f * u, -1.6f * u + (side == Near ? 0.6f * u : 0));
                    break;
                case Stance.Slouch: // hands jammed in pockets
                    elbow = shoulder + new Vector3(-0.9f * u, side * 1.0f * u, -4.4f * u);
                    hand = chest + new Vector3(0.8f * u, side * 3.0f * u * b, -6.4f * u);
                    break;
                case Stance.Ready: // fists up
                    elbow = shoulder + new Vector3(1.2f * u, side * 1.8f * u, -3.6f * u);
                    hand = shoulder + new Vector3(4.0f * u, -side * 0.6f * u, -0.6f * u);
                    break;
                case Stance.Regal: // hands clasped behind the back
                    elbow = shoulder + new Vector3(-1.4f * u, side * 0.8f * u, -4.0f * u);
                    hand = chest + new Vector3(-2.8f * u, side * 1.0f * u, -5.4f * u);
                    break;
                default: // hanging loose
                    elbow = shoulder + new Vector3(0.5f * u, side * 0.8f * u, -4.6f * u);
                    hand = elbow + new Vector3(1.0f * u, -side * 0.2f * u, -3.8f * u);
                    break;
            }
            // Actions take the arms over: a swing to walk, a punch, both raised to cast, flung back when hit.
            if (_pose == Pose.Walk)
            {
                float swing = -side * Stride;
                elbow = shoulder + new Vector3(0.5f * u + 1.2f * u * swing, side * 0.9f * u, -4.4f * u);
                hand = elbow + new Vector3(1.0f * u + 1.6f * u * swing, -side * 0.2f * u, -3.6f * u);
            }
            if (Strike != 0 && side == Near)
            {
                float s = Mathf.Max(0, Strike), back = Mathf.Max(0, -Strike) * 2;
                elbow = elbow.Lerp(shoulder + new Vector3(2.8f * u, side * 0.3f * u, -1.6f * u), s).Lerp(shoulder + new Vector3(-2.2f * u, side * 1.2f * u, -2.8f * u), back);
                hand = hand.Lerp(shoulder + new Vector3(6.4f * u, -side * 0.3f * u, -0.8f * u), s).Lerp(shoulder + new Vector3(-1.6f * u, side * 1.0f * u, -5.4f * u), back);
            }
            if (Cast > 0)
            {
                elbow = elbow.Lerp(shoulder + new Vector3(0.9f * u, side * 1.8f * u, 3.2f * u), Cast);
                hand = hand.Lerp(shoulder + new Vector3(1.8f * u, side * 1.2f * u, 7.6f * u), Cast);
            }
            if (Hurt > 0) { elbow += new Vector3(-1f * u, side * 1f * u, 1.2f * u) * Hurt; hand += new Vector3(-1.4f * u, side * 1.8f * u, 2.8f * u) * Hurt; }

            int arm = Part();
            bool mech = _l.MechArm && side == Near;
            bool gauntlet = _l.Gauntlet && side == Near && !mech;
            bool bare = _l.Garb == Garb.Vest && !mech;
            var upper = mech ? Mat.Metal : bare ? Mat.Accent : Mat.Main;
            var lower = mech || gauntlet ? Mat.Metal : bare ? Mat.Accent : Mat.Main;
            _c.Tube(new[] { shoulder, elbow }, (mech ? 1.25f : 1.4f) * u * b, 1.2f * u, upper, arm, mech ? 0.15f : 0);
            _c.Tube(new[] { elbow, hand }, 1.2f * u, gauntlet ? 1.7f * u : mech ? 1.3f * u : 1.05f * u, lower, arm, mech || gauntlet ? 0.15f : 0);
            var fist = hand + (hand - elbow).Normalized() * 0.8f * u;
            var fistMat = mech || gauntlet ? Mat.Metal : _l.Gloves ? Mat.Second : Mat.Accent;
            _c.Sphere(fist, (gauntlet ? 1.6f : 1.15f) * u, fistMat, arm, 0.1f);
            if (mech)
            {
                // Glowing joints and a piston along the forearm.
                int joints = Part();
                _c.Sphere(shoulder, 1.0f * u, Mat.Glow, joints, 0.3f, 0, true);
                _c.Sphere(elbow, 0.85f * u, Mat.Glow, joints, 0.3f, 0, true);
                _c.Tube(new[] { elbow + new Vector3(0, side * 0.9f * u, 0), hand + new Vector3(0, side * 0.8f * u, 0) }, 0.4f * u, 0.4f * u, Mat.Metal, Part(), 0.3f);
            }
            if (side == Near && bare)
                for (int i = 1; i <= 4; i++) _ink.Add(shoulder.Lerp(elbow, i / 5f) + new Vector3(0, side * 1.2f * u, 0));
            if (side == Near && bare)
                for (int i = 1; i <= 3; i++) _ink.Add(elbow.Lerp(hand, i / 4f) + new Vector3(0, side * 1.0f * u, 0));

            // Shoulder armour: paired pauldrons in the city, a spiked one on the near side in the street.
            if (_l.Pauldrons || side == Near && _l.Spaulder)
            {
                int pad = Part();
                _c.Ellipsoid(shoulder + Z * 0.7f * u, X, 2.2f * u, 2.1f * u, 1.5f * u, Mat.Metal, pad, 0.15f);
                if (_l.Pauldrons) _c.Ellipsoid(shoulder + new Vector3(0, side * 0.6f * u, -0.3f * u), X, 1.9f * u, 1.9f * u, 1.0f * u, Mat.Metal, pad, 0.05f);
                if (_l.Studs && !_l.Pauldrons)
                    foreach (float f in new[] { -0.9f, 0.4f })
                    {
                        var at = shoulder + new Vector3(f * u, side * 0.9f * u, 1.6f * u);
                        _c.Tube(new[] { at, at + new Vector3(0, side * 0.9f * u, 2.4f * u) }, 0.8f * u, 0.45f * u, Mat.Metal, pad, 0.25f);
                    }
            }
        }
    }

    void HeadAndHair(Vector3 chest)
    {
        bool slouch = _l.Stance == Stance.Slouch;
        var neck = chest + new Vector3(slouch ? 0.6f * u : 0.1f * u, 0, 3.1f * u);
        _headC = neck + new Vector3(slouch ? 1.0f * u : 0.4f * u, 0, 3.7f * u - (slouch ? 0.4f * u : 0));
        if (_l.Stance is Stance.Swagger or Stance.Regal or Stance.Showoff) _headC += Z * 0.4f * u - X * 0.5f * u; // chin up
        if (_l.Stance == Stance.Crossed) _headC += Y * -Near * 0.7f * u;                                         // head cocked
        _headR = 3.3f * u;

        if (_l.Ruff)
            _c.Ellipsoid(neck + new Vector3(0, 0, 0.4f * u), X, 2.6f * u, 3.4f * u, 1.0f * u, Mat.Second, Part(), 0.15f);
        else if (_l.Collar)
        {
            int collar = Part();
            _c.Ellipsoid(neck + new Vector3(-0.9f * u, 0, 0.6f * u), X, 1.9f * u, 3.0f * u, 1.9f * u, Mat.Main, collar);
            if (_l.Studs)
                foreach (float y in new[] { -2.2f, 0f, 2.2f })
                {
                    var at = neck + new Vector3(-1.4f * u, y * u, 2.0f * u);
                    _c.Tube(new[] { at, at + new Vector3(-0.8f * u, y * 0.3f * u, 1.6f * u) }, 0.6f * u, 0.45f * u, Mat.Metal, collar, 0.25f);
                }
        }
        int skin = Part();
        _c.Tube(new[] { neck - Z * 0.6f * u, neck + Z * 1.2f * u }, 1.15f * u, 1.05f * u, Mat.Accent, skin);
        _c.Ellipsoid(_headC, X, 3.15f * u, 2.95f * u, 3.4f * u, Mat.Accent, skin);
        _c.Ellipsoid(_headC + new Vector3(1.6f * u, 0, -1.9f * u), X, 1.7f * u, 2.0f * u, 1.3f * u, Mat.Accent, skin); // a strong jaw
        if (_l.Veins || _l.Tattoo)
            for (int i = 0; i < 3; i++) _ink.Add(neck + new Vector3(0.4f * u, Near * 1.0f * u, (i - 0.5f) * 0.8f * u));
        if (_l.Scarf)
        {
            int scarf = Part();
            _c.Tube(new[] { neck + new Vector3(0.6f * u, 2.0f * u, 0.4f * u), neck + new Vector3(1.4f * u, 0, 0.2f * u), neck + new Vector3(0.6f * u, -2.0f * u, 0.4f * u), neck + new Vector3(-1.4f * u, -1.0f * u, 0.6f * u) }, 1.1f * u, 1.1f * u, Mat.Glow, scarf);
            var end = neck + new Vector3(-1.6f * u, 0.8f * u, 0.2f * u);
            _trailRoot = end;
            _c.Tube(new[] { end, end + new Vector3(-2.4f * u, 1.0f * u, -2.6f * u), end + new Vector3(-4.2f * u, 2.0f * u, -4.4f * u) }, 1.0f * u, 0.7f * u, Mat.Glow, scarf);
            _trailRoot = null;
        }
        if (_l.Mask) _c.Ellipsoid(_headC + new Vector3(2.0f * u, 0, -1.5f * u), X, 1.6f * u, 2.8f * u, 1.4f * u, Mat.Second, Part(), -0.1f);

        HairPart();
        Headwear();
        Face(skin);
    }

    /// <summary>A spike of hair from root to tip; dipped in neon at the end if the look says so.</summary>
    void Spike(Vector3 root, Vector3 tip, float r0, int part)
    {
        _trailRoot = root;
        if (_l.HairTips)
        {
            var mid = root.Lerp(tip, 0.65f);
            _c.Tube(new[] { root, mid }, r0, Mathf.Lerp(r0, 0.45f * u, 0.65f), Mat.Membrane, part);
            _c.Tube(new[] { mid, tip }, Mathf.Lerp(r0, 0.45f * u, 0.65f), 0.45f * u, Mat.Glow, part, 0.1f);
        }
        else _c.Tube(new[] { root, tip }, r0, 0.45f * u, Mat.Membrane, part);
        _trailRoot = null;
    }

    void HairPart()
    {
        if (_l.Hat == Hat.Hood) return;
        int hair = Part();
        var crown = _headC + Z * 2.4f * u;
        switch (_l.Hair)
        {
            case Hair.Mohawk:
                for (int i = 0; i < 6; i++)
                {
                    float f = i / 5f;
                    var root = _headC + new Vector3(Mathf.Lerp(2.0f, -2.8f, f) * u, 0, Mathf.Lerp(2.6f, 1.6f, f) * u + Mathf.Sin(f * Mathf.Pi) * 0.7f * u);
                    Spike(root, root + new Vector3((-0.8f - f * 2.8f) * u, 0, (4.8f + Mathf.Sin(f * Mathf.Pi) * 2.2f) * u), 1.15f * u, hair);
                }
                break;
            case Hair.Crest:
                // A huge fanned crest, taller at the back, sweeping back over the head.
                for (int i = 0; i < 7; i++)
                {
                    float f = i / 6f;
                    var root = _headC + new Vector3(Mathf.Lerp(2.2f, -3.0f, f) * u, 0, Mathf.Lerp(2.8f, 1.4f, f) * u);
                    Spike(root, root + new Vector3((-1.5f - f * 4.5f) * u, 0, (4.0f + f * 4.2f) * u), 1.3f * u, hair);
                }
                break;
            case Hair.Liberty:
                // Tall liberty spikes all round the head.
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.Tau * i / 9f;
                    var dir = new Vector3(Mathf.Cos(a) * 0.55f - 0.25f, Mathf.Sin(a) * 0.7f, 1.0f).Normalized();
                    var root = _headC + new Vector3(-0.4f * u, 0, 1.4f * u) + dir * 2.6f * u;
                    Spike(root, root + dir * 6.5f * u, 1.0f * u, hair);
                }
                break;
            case Hair.Spikes:
                _c.Ellipsoid(_headC + new Vector3(-0.8f * u, 0, 1.3f * u), X, 3.0f * u, 3.1f * u, 2.6f * u, Mat.Membrane, hair);
                for (int i = 0; i < 8; i++)
                {
                    float a = Mathf.Tau * i / 8f;
                    var dir = new Vector3(Mathf.Cos(a) * 0.8f - 0.4f, Mathf.Sin(a), 0.9f).Normalized();
                    var root = _headC + new Vector3(-0.6f * u, 0, 1.6f * u) + dir * 2.4f * u;
                    Spike(root, root + dir * 4.0f * u, 1.05f * u, hair);
                }
                break;
            case Hair.Undercut:
                _c.Ellipsoid(_headC + new Vector3(-0.2f * u, -Near * 0.8f * u, 2.4f * u), X, 3.2f * u, 2.4f * u, 1.6f * u, Mat.Membrane, hair);
                _c.Ellipsoid(_headC + new Vector3(1.6f * u, -Near * 2.4f * u, 0.8f * u), X, 1.6f * u, 1.2f * u, 2.2f * u, Mat.Membrane, hair);
                break;
            case Hair.HalfShaved:
                // One side shaved to the skin; the other a long sheet falling past the jaw.
                _c.Ellipsoid(_headC + new Vector3(-0.6f * u, -Near * 1.0f * u, 1.8f * u), X, 3.2f * u, 2.4f * u, 2.2f * u, Mat.Membrane, hair);
                _trailRoot = _headC + new Vector3(0, -Near * 2.6f * u, 1.0f * u);
                _c.Tube(new[] { _headC + new Vector3(0.8f * u, -Near * 2.8f * u, 1.6f * u), _headC + new Vector3(1.4f * u, -Near * 3.2f * u, -2.2f * u), _headC + new Vector3(1.0f * u, -Near * 3.2f * u, -5.0f * u) }, 1.5f * u, 0.8f * u, Mat.Membrane, hair);
                _trailRoot = null;
                break;
            case Hair.Long:
            {
                _c.Ellipsoid(_headC + new Vector3(-1.0f * u, 0, 1.0f * u), X, 3.1f * u, 3.3f * u, 3.0f * u, Mat.Membrane, hair);
                var nape = _headC + new Vector3(-2.8f * u, 0, -0.4f * u);
                Spike(nape, nape + new Vector3(-1.8f * u, 0.4f * u, -7.6f * u), 1.8f * u, hair);
                break;
            }
            case Hair.Twintails:
                _c.Ellipsoid(_headC + new Vector3(-0.8f * u, 0, 1.4f * u), X, 3.0f * u, 3.1f * u, 2.4f * u, Mat.Membrane, hair);
                foreach (int side in new[] { -1, 1 })
                {
                    var root = _headC + new Vector3(-1.2f * u, side * 2.8f * u, 2.0f * u);
                    Spike(root, root + new Vector3(-2.4f * u, side * 2.4f * u, -7.0f * u), 1.3f * u, hair);
                }
                break;
            case Hair.Dreads:
                _c.Ellipsoid(_headC + new Vector3(-0.8f * u, 0, 1.2f * u), X, 3.1f * u, 3.2f * u, 2.6f * u, Mat.Membrane, hair);
                for (int i = 0; i < 7; i++)
                {
                    float a = Mathf.Lerp(-1.9f, 1.9f, i / 6f);
                    var root = _headC + new Vector3(-2.0f * u + Mathf.Cos(a) * -0.6f * u, Mathf.Sin(a) * 2.8f * u, 1.4f * u);
                    Spike(root, root + new Vector3(-1.4f * u, Mathf.Sin(a) * 1.0f * u, -6.0f * u - (i % 2) * 1.2f * u), 0.8f * u, hair);
                }
                break;
            case Hair.Buns:
                _c.Ellipsoid(_headC + new Vector3(-0.8f * u, 0, 1.4f * u), X, 3.0f * u, 3.1f * u, 2.4f * u, Mat.Membrane, hair);
                foreach (int side in new[] { -1, 1 }) _c.Sphere(_headC + new Vector3(-0.6f * u, side * 2.2f * u, 3.4f * u), 1.6f * u, Mat.Membrane, hair);
                break;
            case Hair.Horns:
                // Hair shaped into two horns curling back.
                _c.Ellipsoid(_headC + new Vector3(-0.8f * u, 0, 1.4f * u), X, 3.0f * u, 3.1f * u, 2.4f * u, Mat.Membrane, hair);
                foreach (int side in new[] { -1, 1 })
                {
                    var root = _headC + new Vector3(0.2f * u, side * 2.0f * u, 2.6f * u);
                    _trailRoot = root;
                    _c.Tube(new[] { root, root + new Vector3(-0.6f * u, side * 1.2f * u, 3.2f * u), root + new Vector3(-2.6f * u, side * 1.6f * u, 4.4f * u) }, 1.2f * u, 0.45f * u, _l.HairTips ? Mat.Glow : Mat.Membrane, hair);
                    _trailRoot = null;
                }
                break;
            case Hair.Pompadour:
                // A towering quiff out over the brow.
                _c.Ellipsoid(_headC + new Vector3(-0.6f * u, 0, 1.4f * u), X, 3.0f * u, 3.0f * u, 2.4f * u, Mat.Membrane, hair);
                _c.Ellipsoid(crown + new Vector3(1.2f * u, 0, 1.4f * u), new Vector3(1, 0, 0.5f), 3.6f * u, 2.4f * u, 2.0f * u, Mat.Membrane, hair);
                break;
            case Hair.Shaved:
                _c.Ellipsoid(_headC + new Vector3(-0.6f * u, 0, 1.4f * u), X, 3.0f * u, 3.0f * u, 2.3f * u, Mat.Membrane, hair, -0.2f, _headC.Z + 1.4f * u);
                break;
        }
    }

    void Headwear()
    {
        var crown = _headC + Z * 2.8f * u;
        switch (_l.Hat)
        {
            case Hat.Hood:
            {
                int hood = Part();
                _c.Ellipsoid(_headC + new Vector3(-1.5f * u, 0, 0.6f * u), X, 3.8f * u, 4.0f * u, 4.1f * u, Mat.Main, hood);
                _c.Tube(new[] { _headC + new Vector3(-1.4f * u, 0, 4.0f * u), _headC + new Vector3(-4.0f * u, 0, 3.4f * u) }, 1.6f * u, 0.5f * u, Mat.Main, hood);
                break;
            }
            case Hat.Cap: // worn backwards
                _c.Ellipsoid(_headC + new Vector3(-0.3f * u, 0, 1.8f * u), X, 3.3f * u, 3.35f * u, 2.3f * u, Mat.Second, Part(), 0, _headC.Z + 1.0f * u);
                _c.Ellipsoid(_headC + new Vector3(-3.0f * u, 0, 1.6f * u), X, 1.7f * u, 2.0f * u, 0.4f * u, Mat.Second, Part(), 0.2f);
                break;
            case Hat.Band:
                _c.Ellipsoid(_headC + Z * 1.4f * u, X, 3.25f * u, 3.05f * u, 0.7f * u, Mat.Glow, Part(), 0, 0, true);
                break;
            case Hat.TopHat:
            {
                // In the darker of the clothes: a white top hat reads as a chef's.
                var felt = _l.Trim.V > 0.6f ? Mat.Main : Mat.Second;
                _c.Ellipsoid(crown + Z * 0.4f * u, X, 4.0f * u, 4.0f * u, 0.55f * u, felt, Part(), 0.1f);
                var lean = new Vector3(-0.6f * u, 0, 0);
                _c.Tube(new[] { crown + Z * 0.6f * u, crown + Z * 5.4f * u + lean }, 2.5f * u, 2.7f * u, felt, Part());
                _c.Tube(new[] { crown + Z * 0.8f * u, crown + Z * 1.7f * u + lean * 0.2f }, 2.65f * u, 2.65f * u, Mat.Metal, Part(), 0.2f);
                break;
            }
            case Hat.WideBrim:
                _c.Ellipsoid(crown + Z * 0.2f * u, X, 5.4f * u, 5.4f * u, 0.5f * u, Mat.Second, Part(), 0.1f);
                _c.Ellipsoid(crown + Z * 1.3f * u, X, 2.8f * u, 2.8f * u, 2.1f * u, Mat.Second, Part());
                break;
            case Hat.Crown:
            {
                int c = Part();
                _c.Ellipsoid(crown, X, 2.8f * u, 2.8f * u, 0.9f * u, Mat.Metal, c, 0.15f);
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.Tau * i / 5f + 0.3f;
                    var at = crown + new Vector3(Mathf.Cos(a) * 2.5f * u, Mathf.Sin(a) * 2.5f * u, 0.4f * u);
                    _c.Tube(new[] { at, at + Z * 1.8f * u }, 0.7f * u, 0.45f * u, Mat.Metal, c, 0.2f);
                }
                _c.Sphere(crown + new Vector3(2.8f * u, 0, 0.2f * u), 0.65f * u, Mat.Glow, Part(), 0.4f, 0, true);
                break;
            }
            case Hat.Circlet:
                _c.Ellipsoid(_headC + Z * 1.5f * u, X, 3.25f * u, 3.05f * u, 0.5f * u, Mat.Metal, Part(), 0.2f);
                _c.Sphere(_headC + new Vector3(3.1f * u, 0, 1.6f * u), 0.6f * u, Mat.Glow, Part(), 0.4f, 0, true);
                break;
        }
        if (_l.Goggles)
        {
            int g = Part();
            foreach (int side in new[] { -1, 1 })
            {
                var at = _headC + new Vector3(2.1f * u, side * 1.4f * u, 1.9f * u);
                _c.Sphere(at, 1.1f * u, Mat.Metal, g, 0.2f);
                _c.Sphere(at + X * 0.6f * u, 0.65f * u, Mat.Glow, g, 0.5f, 0, true);
            }
        }
    }

    /// <summary>
    /// The face toward the camera: a dark eye under a hard brow (a glowing implant in a metal socket for
    /// some; shut in a blink, a flinch, a faint), piercings, an earring, and glowing ink and veins on
    /// whatever skin shows.
    /// </summary>
    void Face(int skin)
    {
        foreach (int side in new[] { Near, -Near })
        {
            var dir = (X * 0.84f + Y * side * 0.48f + Z * 0.1f).Normalized();
            var p = _headC + dir * _headR * 0.97f;
            if (!_c.Visible(p, out int x, out int y, 2.0f) || _c.PartAt(x, y) != skin) continue;
            int front = _c.Cam.Project(_headC + X * _headR).X < _c.Cam.Project(_headC).X ? -1 : 1;
            if (_l.EyeImplant)
            {
                foreach (var (dx, dy) in new[] { (-1, -1), (0, -2), (1, -1), (1, 0), (0, 1), (-1, 0) })
                    if (_c.PartAt(x + dx, y + dy) == skin) _c.Plot(x + dx, y + dy, Mat.Metal, skin, 2);
                _c.Plot(x, y, Mat.Glow, skin, 2);
                _c.Plot(x, y - 1, Mat.Glow, skin, 2);
            }
            else
            {
                if (_blink) _c.Plot(x, y, Mat.Line, skin);
                else { _c.Plot(x, y, Mat.Pupil, skin); _c.Plot(x, y - 1, Mat.Pupil, skin); }
                _c.Plot(x - front, y - 2, Mat.Line, skin);
                _c.Plot(x, y - 2, Mat.Line, skin);
                if (_c.PartAt(x + front, y - 1) == skin) _c.Plot(x + front, y - 1, Mat.Line, skin);
            }
            if (_l.Piercings)
            {
                if (_c.PartAt(x - front, y - 3) == skin) _c.Plot(x - front, y - 3, Mat.Metal, skin, 3);
                if (_c.PartAt(x + front, y + 3) == skin) _c.Plot(x + front, y + 3, Mat.Metal, skin, 3);
            }
            if (_l.Tattoo && _c.PartAt(x - front * 2, y + 2) == skin)
            {
                // A mark under the eye, down the cheek.
                _c.Plot(x - front, y + 1, Mat.Glow, skin, 2);
                _c.Plot(x - front * 2, y + 2, Mat.Glow, skin, 2);
                if (_c.PartAt(x - front * 2, y + 3) == skin) _c.Plot(x - front * 2, y + 3, Mat.Glow, skin, 2);
            }
            break;
        }
        if ((_l.Piercings || _l.Tattoo) && _c.Visible(_headC + new Vector3(-0.4f * u, Near * 2.9f * u, -1.2f * u), out int ex, out int ey, 2.0f) && _c.PartAt(ex, ey) == skin)
            _c.Plot(ex, ey + 1, Mat.Metal, skin, 3);
        // Ink and veins along the visible skin: dashes of glow.
        if (_l.Tattoo || _l.Veins)
            for (int i = 0; i < _ink.Count; i++)
                if ((i % 2 == 0 || _l.Veins) && _c.Visible(_ink[i], out int tx, out int ty, 2.5f) && _c.MatAt(tx, ty) == Mat.Accent)
                    _c.Plot(tx, ty, Mat.Glow, _c.PartAt(tx, ty), 2);
    }
}
