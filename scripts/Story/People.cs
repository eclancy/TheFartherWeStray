using System;
using System.Collections.Generic;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Story;

public enum Hat { Bare, Hood, TopHat, WideBrim, Cap, Pointed }
public enum Garb { Coat, Jacket, Robe }

/// <summary>
/// How a person looks: colours (as a creature palette: coat, trim, skin, hair, brass, glow), a hat,
/// a garb and a few extras. Rolled from an id so the same person always looks the same; the story will
/// give its characters their own looks (these are placeholders, like the rivals' titles).
/// </summary>
public sealed class PersonLook
{
    public Hsv Coat, Trim, Skin, Hair, Metal, Glow;
    public Hat Hat;
    public Garb Garb;
    public bool Goggles, Cape, Satchel, Pauldron;
    public float Height = 1, Build = 1;

    public CreaturePalette Palette() => new(Coat, Trim, Skin, Hair, Glow, Metal);
}

/// <summary>The people of the country, for cutscenes: tamers, rivals, townsfolk. Looks by id.</summary>
public static class People
{
    /// <summary>A person's look, the same every time for the same id; tinted by the side they're from.</summary>
    public static PersonLook Look(string id, World.Side side = World.Side.Wilds)
    {
        var rng = new SeededRng(SeededRng.Hash("person:" + id));
        var look = new PersonLook
        {
            Skin = new Hsv(rng.Range(0.04f, 0.09f), rng.Range(0.25f, 0.6f), rng.Range(0.38f, 0.95f)),
            Hair = rng.Weighted(
                (new Hsv(0.75f, 0.2f, 0.12f), 3f),                  // black
                (new Hsv(0.07f, 0.55f, rng.Range(0.25f, 0.45f)), 3f), // brown
                (new Hsv(0.03f, 0.7f, 0.55f), 1.2f),                // auburn
                (new Hsv(0.12f, 0.45f, 0.85f), 1.2f),               // fair
                (new Hsv(0.6f, 0.05f, 0.85f), 0.8f),                // white
                (side == World.Side.Undercity ? new Hsv(rng.Pick(new[] { 0.87f, 0.3f, 0.52f }), 0.75f, 0.9f) : new Hsv(0.07f, 0.55f, 0.35f), side == World.Side.Undercity ? 2.5f : 0.1f)),
            Height = rng.Range(0.92f, 1.08f),
            Build = rng.Range(0.9f, 1.15f),
        };
        switch (side)
        {
            case World.Side.City: // the glorious city above: white, navy, crimson and gold
                look.Coat = rng.Pick(new[] { new Hsv(0.62f, 0.55f, 0.42f), new Hsv(0.1f, 0.08f, 0.92f), new Hsv(0.99f, 0.65f, 0.55f), new Hsv(0.6f, 0.65f, 0.7f) });
                look.Trim = rng.Pick(new[] { new Hsv(0.62f, 0.4f, 0.25f), new Hsv(0.08f, 0.12f, 0.85f), new Hsv(0.75f, 0.15f, 0.18f), new Hsv(0.98f, 0.5f, 0.3f) });
                look.Metal = new Hsv(0.11f, 0.62f, 0.85f);
                look.Glow = new Hsv(0.52f, 0.6f, 1f);
                look.Hat = rng.Weighted((Hat.Bare, 3f), (Hat.TopHat, 2f), (Hat.WideBrim, 1.2f), (Hat.Cap, 0.6f));
                look.Garb = rng.Weighted((Garb.Coat, 3f), (Garb.Jacket, 2f), (Garb.Robe, 0.6f));
                break;
            case World.Side.Undercity: // the nefarious undercity: leather, soot, and chem glow
                look.Coat = rng.Pick(new[] { new Hsv(0.06f, 0.5f, 0.32f), new Hsv(0.72f, 0.2f, 0.22f), new Hsv(0.48f, 0.45f, 0.3f), new Hsv(0.8f, 0.4f, 0.3f) });
                look.Trim = rng.Pick(new[] { new Hsv(0.75f, 0.15f, 0.14f), new Hsv(0.07f, 0.4f, 0.22f), new Hsv(0.6f, 0.15f, 0.4f) });
                look.Metal = rng.Pick(new[] { new Hsv(0.06f, 0.62f, 0.72f), new Hsv(0.6f, 0.15f, 0.5f) });
                look.Glow = rng.Pick(new[] { new Hsv(0.3f, 0.9f, 1f), new Hsv(0.87f, 0.65f, 1f), new Hsv(0.52f, 0.75f, 1f) });
                look.Hat = rng.Weighted((Hat.Bare, 2.5f), (Hat.Hood, 2f), (Hat.Cap, 2f), (Hat.Pointed, 0.6f));
                look.Garb = rng.Weighted((Garb.Coat, 2.5f), (Garb.Jacket, 2.5f), (Garb.Robe, 0.8f));
                look.Goggles = rng.Chance(0.45f);
                break;
            default: // the wilds and the ranch: moss, oilcloth and leather
                look.Coat = rng.Pick(new[] { new Hsv(0.25f, 0.4f, 0.4f), new Hsv(0.08f, 0.5f, 0.45f), new Hsv(0.15f, 0.35f, 0.55f), new Hsv(0.45f, 0.35f, 0.38f) });
                look.Trim = rng.Pick(new[] { new Hsv(0.07f, 0.45f, 0.28f), new Hsv(0.1f, 0.2f, 0.75f), new Hsv(0.3f, 0.3f, 0.25f) });
                look.Metal = new Hsv(0.09f, 0.55f, 0.72f);
                look.Glow = new Hsv(0.11f, 0.85f, 1f);
                look.Hat = rng.Weighted((Hat.Bare, 3f), (Hat.Hood, 2f), (Hat.WideBrim, 2f), (Hat.Cap, 1f));
                look.Garb = rng.Weighted((Garb.Coat, 2f), (Garb.Jacket, 3f), (Garb.Robe, 0.5f));
                break;
        }
        look.Cape = rng.Chance(look.Garb == Garb.Robe ? 0.1f : 0.3f);
        look.Satchel = rng.Chance(0.35f);
        look.Pauldron = rng.Chance(side == World.Side.City ? 0.35f : 0.2f);
        return look;
    }
}

/// <summary>
/// Draws a person the way <see cref="CreatureSculptor"/> draws a creature: a small 3D figure (boots,
/// legs, coat, arms, head, hat) ray-cast into pixels from FFTA's camera, shaded in four tones and
/// outlined, facing the screen diagonal. Poses move the same way a creature's do: a breath, a walk
/// with swinging legs and arms, a lunge, arms raised to cast, a flinch, a fall to one knee.
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
    float _headR;
    static readonly Vector3 X = new(1, 0, 0), Y = new(0, 1, 0), Z = new(0, 0, 1);

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
        u = S / 56f * 1.45f * look.Height;
        int w = Mathf.CeilToInt(S * 1.9f), h = Mathf.CeilToInt(S * 1.9f);
        _c = new PixelCanvas(w, h);
        _c.Begin3D(new Camera3(view, new Vector2(w / 2f, h * 0.85f)));
    }

    bool Animate => _phase >= 0;
    float Wave(float lag = 0, float speed = 1) => Animate ? Mathf.Sin(Mathf.Tau * (_phase * speed - lag)) : 0;
    float Rise => Animate ? 0.5f - 0.5f * Mathf.Cos(Mathf.Tau * _phase) : 0;

    /// <summary>The pose's sway of the whole figure: more the higher up (feet stay planted).</summary>
    Vector3 Warp(Vector3 p)
    {
        if (!Animate) return p;
        float up = Mathf.Clamp((p.Z - 3 * u) / (24 * u), 0, 1);
        float head = _headR > 0 ? Mathf.Clamp(1 - ((p - _headC).Length() - _headR * 1.2f) / _headR, 0, 1) : 0;
        var o = p;
        switch (_pose)
        {
            case Pose.Idle:
                o += Z * 0.8f * u * Rise * Mathf.Clamp((p.Z - 10 * u) / (8 * u), 0, 1);
                o += Z * 0.4f * u * Wave(0.2f) * head;
                break;
            case Pose.Walk:
                o += Z * 0.9f * u * (0.5f - 0.5f * Mathf.Cos(Mathf.Tau * 2 * _phase)) * up + X * 0.6f * u * up;
                break;
            case Pose.Strike:
            {
                float t = _phase, f = t < 0.3f ? -t / 0.3f : t < 0.55f ? Mathf.Lerp(-1, 1, (t - 0.3f) / 0.25f) : Mathf.Lerp(1, 0, (t - 0.55f) / 0.45f);
                o += X * f * 2.6f * u * up - Z * Mathf.Max(f, 0) * 0.8f * u * up;
                break;
            }
            case Pose.Cast:
            {
                float c = Mathf.Sin(Mathf.Pi * Mathf.Clamp(_phase * 1.15f, 0, 1));
                o += Z * c * 1.2f * u * up - X * c * 1.0f * u * up;
                break;
            }
            case Pose.Hurt:
            {
                float h = _phase < 0.25f ? _phase / 0.25f : 1 - (_phase - 0.25f) / 0.75f;
                o += (-X * 2.4f * u - Z * 0.6f * u) * h * up;
                break;
            }
            case Pose.Faint:
            {
                // Down on one knee, then slumped forward.
                float k = Mathf.Clamp(_phase * 1.4f, 0, 1);
                o.Z = p.Z * (1 - 0.32f * k * up);
                o += X * 2.2f * u * k * up * up;
                break;
            }
        }
        return o;
    }

    int Part() => ++_part;

    void Build()
    {
        _c.Warp = Warp;
        float b = _l.Build;
        // How the limbs move: a stride for the walk, a thrown arm for a strike, both raised to cast.
        float stride = _pose == Pose.Walk && Animate ? Mathf.Sin(Mathf.Tau * _phase) : 0;
        float lift(float s) => _pose == Pose.Walk && Animate ? Mathf.Max(0, s) * 1.2f * u : 0;
        float strike = _pose == Pose.Strike && Animate ? (_phase < 0.3f ? 0 : _phase < 0.55f ? (_phase - 0.3f) / 0.25f : 1 - (_phase - 0.55f) / 0.45f) : 0;
        float cast = _pose == Pose.Cast && Animate ? Mathf.Sin(Mathf.Pi * Mathf.Clamp(_phase * 1.15f, 0, 1)) : 0;
        float hurt = _pose == Pose.Hurt && Animate ? (_phase < 0.25f ? _phase / 0.25f : 1 - (_phase - 0.25f) / 0.75f) : 0;

        // The far side first (the canvas keeps whatever is nearest anyway; parts number in paint order).
        foreach (int side in new[] { -1, 1 })
        {
            float swing = side * stride;
            var hip = new Vector3(0, side * 1.5f * u * b, 12.5f * u);
            var ankle = new Vector3(2.2f * u * swing, side * 1.7f * u * b, 2f * u + lift(swing));
            var knee = (hip + ankle) / 2 + X * 0.7f * u;
            int leg = Part();
            _c.Tube(new[] { hip, knee, ankle }, 1.5f * u * b, 1.15f * u, Mat.Second, leg);
            _c.Ellipsoid(ankle + new Vector3(0.7f * u, 0, -0.9f * u), X, 1.9f * u, 1.15f * u, 1.1f * u, Mat.Second, leg, -0.15f);
        }
        if (_l.Garb != Garb.Jacket)
        {
            // A coat's skirt to the knee, or a robe to the ground.
            float floor = _l.Garb == Garb.Robe ? 0.6f * u : 5f * u;
            _c.Ellipsoid(new Vector3(-0.3f * u, 0, 10.5f * u), X, 3.4f * u * b, 3.7f * u * b, 6.2f * u, Mat.Main, Part(), 0, floor);
        }
        if (_l.Cape)
        {
            var top = new Vector3(-1.6f * u, 0, 21.5f * u);
            _c.Membrane(new[] { top + Y * 3.4f * u * b, top - Y * 3.4f * u * b, new Vector3(-3.6f * u, -4.4f * u * b, 4f * u), new Vector3(-3.6f * u, 4.4f * u * b, 4f * u) }, Mat.Second, Part());
        }
        int torso = Part();
        _c.Ellipsoid(new Vector3(0, 0, 18f * u), X, 2.6f * u * b, 3.4f * u * b, 5.4f * u, Mat.Main, torso);
        _c.Ellipsoid(new Vector3(0.1f * u, 0, 13.4f * u), X, 2.75f * u * b, 3.45f * u * b, 0.8f * u, Mat.Second, Part(), 0.1f);
        _c.Sphere(new Vector3(2.5f * u * b, 0, 13.4f * u), 0.75f * u, Mat.Metal, Part(), 0.3f);
        if (_l.Satchel) _c.Box(new Vector3(-0.4f * u, 3.9f * u * b, 11.8f * u), new Vector3(1.5f * u, 0.9f * u, 1.6f * u), Mat.Second, Part());

        foreach (int side in new[] { -1, 1 })
        {
            float swing = -side * stride;
            var shoulder = new Vector3(0, side * 3.7f * u * b, 21.2f * u);
            var elbow = shoulder + new Vector3(0.6f * u + 1.2f * u * swing, side * 0.7f * u, -4.8f * u);
            var hand = elbow + new Vector3(0.9f * u + 1.4f * u * swing, -side * 0.2f * u, -3.6f * u);
            // The near arm (side 1 faces the camera in both views once mirrored) strikes; both rise to cast.
            if (strike > 0 && side == 1) { elbow = shoulder + new Vector3(2.6f * u, 0.4f * u, -2.2f * u) * strike + (elbow - shoulder) * (1 - strike); hand = shoulder + new Vector3(5.8f * u, 0, -1.2f * u) * strike + (hand - shoulder) * (1 - strike); }
            if (cast > 0) { elbow = elbow.Lerp(shoulder + new Vector3(0.8f * u, side * 1.6f * u, 3.2f * u), cast); hand = hand.Lerp(shoulder + new Vector3(1.6f * u, side * 1.4f * u, 7.4f * u), cast); }
            if (hurt > 0) { elbow += new Vector3(-0.8f * u, side * 0.8f * u, 1.0f * u) * hurt; hand += new Vector3(-1.2f * u, side * 1.4f * u, 2.4f * u) * hurt; }
            int arm = Part();
            _c.Tube(new[] { shoulder, elbow, hand }, 1.35f * u * b, 1.05f * u, Mat.Main, arm);
            _c.Sphere(hand + (hand - elbow).Normalized() * 0.7f * u, 1.05f * u, Mat.Accent, arm, 0.1f);
            if (_l.Pauldron && side == 1) _c.Ellipsoid(shoulder + Z * 0.6f * u, X, 1.9f * u, 1.7f * u, 1.3f * u, Mat.Metal, Part(), 0.15f);
        }

        // Neck and head.
        int skin = Part();
        _c.Tube(new[] { new Vector3(0, 0, 22.4f * u), new Vector3(0.2f * u, 0, 24.4f * u) }, 1.1f * u, 1.0f * u, Mat.Accent, skin);
        _headC = new Vector3(0.4f * u, 0, 26.8f * u);
        _headR = 3.1f * u;
        _c.Ellipsoid(_headC, X, 3.0f * u, 2.85f * u, 3.3f * u, Mat.Accent, skin);
        Hair();
        Headwear();
        Face(skin);
    }

    void Hair()
    {
        if (_l.Hat == Hat.Hood) return;
        // Over the crown and down the back, leaving the face.
        _c.Ellipsoid(_headC + new Vector3(-1.1f * u, 0, 1.0f * u), X, 3.0f * u, 3.15f * u, 3.0f * u, Mat.Membrane, Part());
    }

    void Headwear()
    {
        var crown = _headC + Z * 2.6f * u;
        switch (_l.Hat)
        {
            case Hat.Hood:
            {
                int hood = Part();
                _c.Ellipsoid(_headC + new Vector3(-1.5f * u, 0, 0.5f * u), X, 3.6f * u, 3.8f * u, 3.9f * u, Mat.Main, hood);
                _c.Tube(new[] { _headC + new Vector3(-3.4f * u, 0, -1.5f * u), new Vector3(-2.6f * u, 0, 21f * u) }, 2.2f * u, 3.2f * u, Mat.Main, hood);
                break;
            }
            case Hat.TopHat:
            {
                _c.Ellipsoid(crown + Z * 0.4f * u, X, 3.9f * u, 3.9f * u, 0.55f * u, Mat.Second, Part(), 0.1f);
                int top = Part();
                _c.Tube(new[] { crown + Z * 0.6f * u, crown + Z * 5.2f * u }, 2.4f * u, 2.6f * u, Mat.Second, top);
                _c.Tube(new[] { crown + Z * 0.8f * u, crown + Z * 1.6f * u }, 2.55f * u, 2.55f * u, Mat.Metal, Part(), 0.2f);
                break;
            }
            case Hat.WideBrim:
                _c.Ellipsoid(crown + Z * 0.2f * u, X, 5.2f * u, 5.2f * u, 0.5f * u, Mat.Second, Part(), 0.1f);
                _c.Ellipsoid(crown + Z * 1.2f * u, X, 2.7f * u, 2.7f * u, 2.0f * u, Mat.Second, Part());
                break;
            case Hat.Cap:
                _c.Ellipsoid(_headC + new Vector3(-0.3f * u, 0, 1.6f * u), X, 3.2f * u, 3.25f * u, 2.2f * u, Mat.Second, Part(), 0, _headC.Z + 0.9f * u);
                _c.Ellipsoid(_headC + new Vector3(2.4f * u, 0, 1.4f * u), X, 1.6f * u, 2.0f * u, 0.4f * u, Mat.Second, Part(), 0.2f);
                break;
            case Hat.Pointed:
            {
                _c.Ellipsoid(crown, X, 4.4f * u, 4.4f * u, 0.5f * u, Mat.Second, Part(), 0.1f);
                var tip = crown + new Vector3(-2.2f * u, 0, 7.5f * u);
                _c.Tube(new[] { crown + Z * 0.4f * u, crown + new Vector3(-0.6f * u, 0, 4f * u), tip }, t => Mathf.Lerp(2.7f, 0.5f, t) * u, Mat.Second, Part());
                break;
            }
        }
        if (_l.Goggles)
        {
            int g = Part();
            foreach (int side in new[] { -1, 1 })
            {
                var at = _headC + new Vector3(2.0f * u, side * 1.3f * u, 1.7f * u);
                _c.Sphere(at, 1.05f * u, Mat.Metal, g, 0.2f);
                _c.Sphere(at + X * 0.6f * u, 0.6f * u, Mat.Glow, g, 0.5f, 0, true);
            }
        }
    }

    /// <summary>Two dark eyes on the side of the face toward the camera (shut in a blink, a flinch, a faint).</summary>
    void Face(int skin)
    {
        foreach (int side in new[] { 1, -1 })
        {
            var dir = (X * 0.82f + Y * side * 0.5f + Z * 0.12f).Normalized();
            var p = _headC + dir * _headR * 0.98f;
            if (!_c.Visible(p, out int x, out int y, 2.0f) || _c.PartAt(x, y) != skin) continue;
            if (_blink) { _c.Plot(x, y, Mat.Line, skin); continue; }
            _c.Plot(x, y, Mat.Pupil, skin);
            _c.Plot(x, y - 1, Mat.Pupil, skin);
        }
    }
}
