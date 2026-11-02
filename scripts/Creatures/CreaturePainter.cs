using System;
using System.Collections.Generic;
using Godot;

namespace CreatureTactics.Creatures;

/// <summary>
/// Draws a creature: the body plan lays out a skeleton (body, hips, head, tail root, wing root) and
/// each part slot paints its shape at those anchors, back to front. The view is a three-quarter view
/// facing left, standing on the bottom row, like a Gen 2-3 front sprite.
/// </summary>
public sealed class CreaturePainter
{
    enum Zone { Body, Head, Tail, Tip, Limb, Ear }

    readonly Creature _cr;
    readonly Species _sp;
    readonly PartSet _p;
    readonly PixelCanvas _c;
    readonly Dictionary<int, Zone> _zones = new();

    readonly float S;      // nominal sprite size for this size class
    readonly float Floor;  // y of the ground the creature stands on
    readonly float Lx;     // left edge of the nominal S x S box inside the larger canvas
    readonly float _hs;    // head scale
    readonly Vector2 _pose;

    // Per-species style choices, rolled once so every sibling matches.
    readonly Mat _spikeMat, _shellMat, _featherMat, _sailMat, _tuftMat, _crestMat;

    public static Image Paint(Creature creature, bool fourColor)
    {
        var painter = new CreaturePainter(creature);
        painter.PaintBody();
        painter.ApplyPattern();
        var img = painter._c.Resolve(creature.Palette, fourColor);
        // Trim the empty margin but keep the bottom row, so the sprite still stands on its ground.
        var used = img.GetUsedRect();
        if (used.Size.X == 0) return img;
        return img.GetRegion(new Rect2I(used.Position.X, used.Position.Y, used.Size.X, img.GetHeight() - used.Position.Y));
    }

    CreaturePainter(Creature cr)
    {
        _cr = cr;
        _sp = cr.Species;
        _p = cr.Parts;
        S = SizeInfo.Pixels(cr.Size);
        int w = Mathf.CeilToInt(S * 1.7f), h = Mathf.CeilToInt(S * 1.55f);
        _c = new PixelCanvas(w, h);
        Floor = h - 1;
        Lx = (w - S) / 2f;
        _hs = SizeInfo.HeadScale(cr.Size) * _sp.HeadSize * cr.HeadJitter;
        _pose = cr.Attitude switch
        {
            Attitude.Aggressive => new Vector2(-0.2f, 0.2f),
            Attitude.Skittish => new Vector2(0.15f, 0.2f),
            Attitude.Proud => new Vector2(0.08f, -0.25f),
            Attitude.Curious => new Vector2(-0.12f, -0.12f),
            _ => Vector2.Zero,
        };

        var rng = new SeededRng(SeededRng.Derive(_sp.Seed, 0x5EED));
        _spikeMat = _sp.SpikeMat ?? rng.Pick(new[] { Mat.Accent, Mat.Second, Mat.Accent });
        _shellMat = _sp.ShellMat ?? rng.Pick(new[] { Mat.Second, Mat.Accent });
        _featherMat = _sp.FeatherMat ?? rng.Pick(new[] { Mat.Main, Mat.Second });
        _sailMat = _sp.SailMat ?? rng.Pick(new[] { Mat.Second, Mat.Membrane });
        _tuftMat = _sp.TuftMat ?? rng.Pick(new[] { Mat.Second, Mat.Main });
        _crestMat = _sp.CrestMat ?? rng.Pick(new[] { Mat.Second, Mat.Accent, Mat.Main });
    }

    float Hx(float f) => Lx + S * f;

    /// <summary>Dark-pupil beady eyes vanish on a dark body, so those get eye whites.</summary>
    bool DarkBody => _cr.Palette.Main.Base.Luminance < 0.4f;
    float BodyMul => _sp.BodyLen * _cr.BodyJitter;

    float LimbLenMul => (_p.Limbs switch { Limbs.Stubby => 0.7f, Limbs.Long => 1.3f, _ => 1f })
        * _sp.LegLen * _cr.LegJitter * (_cr.Size == SizeClass.Tiny ? 0.85f : 1f);

    float LimbThickMul => (_p.Limbs switch { Limbs.Stubby => 1.25f, Limbs.Long => 0.85f, _ => 1f }) * _sp.Thickness;

    int Part(Zone zone)
    {
        int id = _c.NewPart();
        _zones[id] = zone;
        return id;
    }

    void PaintBody()
    {
        switch (_sp.Plan)
        {
            case BodyPlan.Quadruped: Quadruped(); break;
            case BodyPlan.Biped: Biped(); break;
            case BodyPlan.Serpent: Serpent(); break;
            case BodyPlan.Avian: Avian(); break;
            case BodyPlan.Insectoid: Insectoid(); break;
            case BodyPlan.Aquatic: Aquatic(); break;
            default: Blob(); break;
        }
    }

    // ---------------------------------------------------------------- body plans

    void Quadruped()
    {
        float rx = S * 0.25f * BodyMul, ry = S * 0.15f * _sp.Thickness;
        float legLen = S * 0.2f * LimbLenMul, legR = S * 0.05f * LimbThickMul;
        if (_cr.Attitude == Attitude.Skittish) legLen *= 0.88f;
        float cx = Hx(0.56f), cy = Floor - legLen - ry * 0.5f;
        float hr = S * 0.14f * _hs;
        var hc = new Vector2(cx - rx * 0.92f - hr * 0.25f, cy - ry * 0.75f - hr * 0.6f * _sp.NeckLen) + _pose * hr;
        var wingRoot = new Vector2(cx + rx * 0.05f, cy - ry * 0.75f);

        if (_p.Wings != Wings.None) Wing(wingRoot + new Vector2(S * 0.05f, -S * 0.02f), S * 0.36f, far: true);
        BackBehind(new Vector2(cx, cy), rx, ry, -155, -35);
        Leg(new Vector2(cx - rx * 0.5f + S * 0.05f, cy + ry * 0.2f), legR, far: true, hind: false);
        Leg(new Vector2(cx + rx * 0.55f + S * 0.05f, cy + ry * 0.2f), legR, far: true, hind: true);
        TailPaint(new Vector2(cx + rx * 0.9f, cy - ry * 0.25f), ry * 0.7f, S * 0.32f);

        int body = Part(Zone.Body);
        _c.Ellipse(cx, cy, rx, ry, Mat.Main, body);
        _c.Capsule(new Vector2(cx - rx * 0.6f, cy - ry * 0.2f), hc + new Vector2(hr * 0.2f, hr * 0.3f), ry * 0.6f, hr * 0.55f, Mat.Main, body);
        BackOver(new Vector2(cx, cy), rx, ry);

        Leg(new Vector2(cx - rx * 0.58f, cy + ry * 0.3f), legR, far: false, hind: false);
        Leg(new Vector2(cx + rx * 0.5f, cy + ry * 0.3f), legR, far: false, hind: true);
        if (_p.Wings != Wings.None) Wing(wingRoot, S * 0.36f, far: false);
        HeadPaint(hc, hr);
    }

    void Biped()
    {
        float rx = S * 0.165f * _sp.Thickness, ry = S * 0.2f * BodyMul;
        float legLen = S * 0.12f * LimbLenMul, legR = S * 0.06f * LimbThickMul;
        float cx = Hx(0.52f), cy = Floor - legLen - ry * 0.7f;
        float hr = S * 0.17f * _hs;
        var hc = new Vector2(cx - rx * 0.2f, cy - ry * 0.95f - hr * 0.5f) + _pose * hr;
        var wingRoot = new Vector2(cx + rx * 0.3f, cy - ry * 0.6f);

        if (_p.Wings != Wings.None) Wing(wingRoot + new Vector2(S * 0.04f, -S * 0.02f), S * 0.34f, far: true);
        BackBehind(new Vector2(cx, cy), rx, ry, -80, 35);
        Arm(new Vector2(cx - rx * 0.55f, cy - ry * 0.45f), new Vector2(cx - rx * 1.15f, cy - ry * 0.05f), legR * 0.75f, far: true);
        Leg(new Vector2(cx + rx * 0.2f, cy + ry * 0.6f), legR, far: true, hind: false);
        TailPaint(new Vector2(cx + rx * 0.8f, cy + ry * 0.45f), rx * 0.55f, S * 0.3f);

        int body = Part(Zone.Body);
        _c.Ellipse(cx, cy, rx, ry, Mat.Main, body);
        BackOver(new Vector2(cx, cy), rx, ry);

        Leg(new Vector2(cx - rx * 0.35f, cy + ry * 0.65f), legR, far: false, hind: false);
        Arm(new Vector2(cx + rx * 0.2f, cy - ry * 0.4f), new Vector2(cx - rx * 0.3f, cy + ry * 0.3f), legR * 0.75f, far: false);
        if (_p.Wings != Wings.None) Wing(wingRoot, S * 0.34f, far: false);
        HeadPaint(hc, hr);
    }

    void Serpent()
    {
        float r = S * 0.085f * _sp.Thickness;
        float hr = S * 0.145f * _hs;
        float rise = S * 0.4f * Mathf.Lerp(1, _sp.NeckLen, 0.5f)
            * (_cr.Attitude == Attitude.Proud ? 1.1f : _cr.Attitude == Attitude.Skittish ? 0.85f : 1f);
        // Tail on the ground at the right, a coil, then the neck rears up in an S like a cobra.
        var ctrl = new[]
        {
            new Vector2(Hx(0.95f), Floor - r * 0.45f),
            new Vector2(Hx(0.82f), Floor - r - S * 0.07f),
            new Vector2(Hx(0.66f), Floor - r * 0.95f),
            new Vector2(Hx(0.48f), Floor - r - S * 0.02f),
            new Vector2(Hx(0.36f), Floor - r - rise * 0.45f),
            new Vector2(Hx(0.44f), Floor - r - rise * 0.82f),
            new Vector2(Hx(0.38f), Floor - r - rise),
        };
        var pts = Spline(ctrl, 6);
        float Radius(float t) => t < 0.4f ? Mathf.Lerp(r * 0.22f, r, Mathf.SmoothStep(0, 0.4f, t)) : Mathf.Lerp(r, r * 0.7f, (t - 0.4f) / 0.6f);
        var hc = ctrl[^1] + new Vector2(-hr * 0.35f, -hr * 0.45f) + _pose * hr;

        if (_p.Wings != Wings.None) Wing(Along(pts, 0.7f) + new Vector2(S * 0.05f, -r * 0.5f), S * 0.34f, far: true);
        BackAlong(pts, Radius, behind: true);

        int body = Part(Zone.Body);
        _c.Tube(pts, Radius, Mat.Main, body);
        _c.Capsule(pts[^1], hc + new Vector2(hr * 0.2f, hr * 0.3f), Radius(1), hr * 0.6f, Mat.Main, body);
        BackAlong(pts, Radius, behind: false);
        TailTip(pts[0], pts[0] - pts[2], r);

        if (_p.Wings != Wings.None) Wing(Along(pts, 0.7f) + new Vector2(0, -r * 0.5f), S * 0.34f, far: false);
        HeadPaint(hc, hr);
    }

    void Avian()
    {
        float rx = S * 0.19f * BodyMul, ry = S * 0.16f * _sp.Thickness;
        float legLen = S * 0.14f * LimbLenMul, legR = Mathf.Max(0.6f, S * 0.022f);
        float cx = Hx(0.55f), cy = Floor - legLen - ry * 0.8f;
        float hr = S * 0.13f * _hs;
        var hc = new Vector2(cx - rx * 0.78f, cy - ry * 0.85f - hr * 0.45f * _sp.NeckLen) + _pose * hr;
        var wingRoot = new Vector2(cx + rx * 0.1f, cy - ry * 0.6f);
        bool folded = _sp.WingsFolded && _p.Wings == Wings.Feather;

        if (!folded && _p.Wings != Wings.None) Wing(wingRoot + new Vector2(S * 0.05f, -S * 0.02f), S * 0.42f, far: true);
        BirdLeg(new Vector2(cx + rx * 0.2f + S * 0.03f, cy + ry * 0.6f), legR, far: true);
        TailPaint(new Vector2(cx + rx * 0.85f, cy - ry * 0.1f), ry * 0.6f, S * 0.3f);

        int body = Part(Zone.Body);
        _c.Ellipse(cx, cy, rx, ry, Mat.Main, body, rot: -0.15f);
        if (_sp.NeckLen > 1.1f)
            _c.Capsule(new Vector2(cx - rx * 0.6f, cy - ry * 0.3f), hc + new Vector2(hr * 0.2f, hr * 0.3f), ry * 0.45f, hr * 0.5f, Mat.Main, body);
        BackOver(new Vector2(cx, cy), rx, ry);

        BirdLeg(new Vector2(cx - rx * 0.15f, cy + ry * 0.65f), legR, far: false);
        if (folded) FoldedWing(new Vector2(cx, cy), rx, ry);
        else if (_p.Wings != Wings.None) Wing(wingRoot, S * 0.42f, far: false);
        HeadPaint(hc, hr);
    }

    void Insectoid()
    {
        float legLen = S * 0.13f * LimbLenMul;
        float rt = S * 0.085f * _sp.Thickness;
        float tx = Hx(0.46f), ty = Floor - legLen - rt * 0.4f;
        var thorax = new Vector2(tx, ty);
        var ac = new Vector2(tx + rt * 0.9f + S * 0.13f * BodyMul, ty - S * 0.03f);
        float arx = S * 0.17f * BodyMul, ary = S * 0.12f * _sp.Thickness, arot = -0.3f;
        float hr = S * 0.1f * _hs;
        var hc = new Vector2(tx - rt - hr * 0.6f, ty - rt * 0.35f) + _pose * hr;
        var wingRoot = new Vector2(tx + rt * 0.2f, ty - rt * 0.8f);
        float legR = Mathf.Max(0.6f, S * 0.02f * LimbThickMul);

        if (_p.Wings != Wings.None) Wing(wingRoot + new Vector2(S * 0.04f, -S * 0.02f), S * 0.38f, far: true);
        for (int k = 0; k < 3; k++) InsectLeg(thorax, rt, k, legLen, legR, far: true);
        var tailRoot = ac + new Vector2(arx, 0).Rotated(arot) * 0.9f;
        TailPaint(tailRoot, ary * 0.5f, S * 0.16f);
        BackBehind(ac, arx, ary, -150, -40);

        int abdomen = Part(Zone.Body);
        _c.Ellipse(ac.X, ac.Y, arx, ary, Mat.Main, abdomen, rot: arot);
        if (_p.Back == Back.Shell)
        {
            int shell = Part(Zone.Body);
            _c.Ellipse(ac.X - arx * 0.05f, ac.Y - ary * 0.2f, arx * 1.04f, ary * 0.85f, _shellMat, shell, rot: arot, clipBelow: ac.Y + ary * 0.25f);
        }
        int th = Part(Zone.Body);
        _c.Ellipse(tx, ty, rt * 1.1f, rt, Mat.Main, th);

        for (int k = 0; k < 3; k++) InsectLeg(thorax, rt, k, legLen, legR, far: false);
        if (_p.Wings != Wings.None) Wing(wingRoot, S * 0.38f, far: false);
        HeadPaint(hc, hr);
    }

    void Aquatic()
    {
        float rx = S * 0.28f * BodyMul, ry = S * 0.18f * _sp.Thickness;
        float cx = Hx(0.52f), cy = Floor - ry - S * 0.14f; // it floats
        float hr = ry * 0.85f * Mathf.Sqrt(_hs);
        var hc = new Vector2(cx - rx * 0.5f, cy - ry * 0.12f) + _pose * hr * 0.4f;
        float fin = _p.Limbs == Limbs.Long ? 1.4f : _p.Limbs == Limbs.Stubby ? 0.75f : 1f;

        int farFin = Part(Zone.Limb);
        _c.Polygon(new[]
        {
            new Vector2(cx + S * 0.02f, cy + ry * 0.3f),
            new Vector2(cx + S * 0.08f, cy + ry * (0.95f + 0.4f * fin)),
            new Vector2(cx + S * 0.12f + rx * 0.2f, cy + ry * 0.35f),
        }, Mat.Second, farFin, -0.35f);
        TailPaint(new Vector2(cx + rx * 0.88f, cy), ry * 0.7f, S * 0.3f);
        BackBehind(new Vector2(cx, cy), rx, ry, -150, -40);

        int body = Part(Zone.Body);
        _c.Ellipse(cx, cy, rx, ry, Mat.Main, body);
        BackOver(new Vector2(cx, cy), rx, ry);

        int nearFin = Part(Zone.Limb);
        _c.Polygon(new[]
        {
            new Vector2(cx - rx * 0.25f, cy + ry * 0.2f),
            new Vector2(cx - rx * 0.05f, cy + ry * (0.85f + 0.45f * fin)),
            new Vector2(cx + rx * 0.22f, cy + ry * 0.35f),
        }, Mat.Second, nearFin);
        HeadPaint(hc, hr, facePart: body);
    }

    void Blob()
    {
        float rx = S * 0.29f * BodyMul, ry = S * 0.25f * _sp.Thickness;
        float cx = Hx(0.5f), cy = Floor - ry * 0.88f;
        float hr = ry * 0.62f * Mathf.Sqrt(_hs);
        var hc = new Vector2(cx - rx * 0.32f, cy - ry * 0.22f) + _pose * hr * 0.4f;
        var wingRoot = new Vector2(cx + rx * 0.2f, cy - ry * 0.7f);

        if (_p.Wings != Wings.None) Wing(wingRoot + new Vector2(S * 0.04f, -S * 0.02f), S * 0.3f, far: true);
        TailPaint(new Vector2(cx + rx * 0.85f, cy + ry * 0.3f), ry * 0.45f, S * 0.28f);
        BackBehind(new Vector2(cx, cy), rx, ry, -160, -20);

        int body = Part(Zone.Body);
        _c.Ellipse(cx, cy, rx, ry, Mat.Main, body, clipBelow: Floor);
        // A blob slumps: a few drips puddle out along its bottom edge.
        for (int i = -1; i <= 1; i += 2)
            _c.Ellipse(cx + rx * 0.62f * i, Floor - ry * 0.14f, ry * 0.22f, ry * 0.16f, Mat.Main, body, clipBelow: Floor);

        if (_p.Limbs == Limbs.Long)
        {
            Arm(new Vector2(cx - rx * 0.7f, cy), new Vector2(cx - rx * 1.15f, cy + ry * 0.35f), S * 0.045f, far: false);
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                int nub = Part(Zone.Limb);
                float fx = cx - rx * 0.45f + i * rx * 0.55f, fr = S * 0.06f * LimbThickMul;
                _c.Ellipse(fx, Floor - fr * 0.6f, fr * 1.1f, fr * 0.65f, Mat.Main, nub, clipBelow: Floor);
                if (_p.Limbs == Limbs.Clawed) Claws(fx - fr * 1.1f, Floor, fr, nub);
            }
        }
        BackOver(new Vector2(cx, cy), rx, ry);
        if (_p.Wings != Wings.None) Wing(wingRoot, S * 0.3f, far: false);
        HeadPaint(hc, hr, facePart: body);
    }

    // ---------------------------------------------------------------- head and face

    /// <summary>Paints a head (or, with facePart, only the face on an existing body) with its ears and horns.</summary>
    void HeadPaint(Vector2 hc, float hr, int facePart = -1)
    {
        EarsPaint(hc, hr, behind: true);
        HornsPaint(hc, hr, behind: true, null);

        int head = facePart;
        Vector2? snout = null;
        float snoutRx = 0, snoutRy = 0;
        if (facePart < 0)
        {
            head = Part(Zone.Head);
            float rx = hr, ry = hr * 0.92f;
            if (_p.Head == HeadShape.Wide) { rx *= 1.15f; ry *= 0.85f; }
            _c.Ellipse(hc.X, hc.Y, rx, ry, Mat.Main, head);
            if (_p.Head == HeadShape.Snout && _sp.Plan is not (BodyPlan.Avian or BodyPlan.Insectoid))
            {
                snoutRx = hr * 0.55f * _sp.SnoutLen;
                snoutRy = hr * 0.42f;
                var sc = new Vector2(hc.X - rx * 0.72f - snoutRx * 0.3f, hc.Y + hr * 0.32f);
                _c.Ellipse(sc.X, sc.Y, snoutRx, snoutRy, _sp.MuzzleSecond ? Mat.Second : Mat.Main, head);
                snout = sc;
            }
        }

        EarsPaint(hc, hr, behind: false);
        HornsPaint(hc, hr, behind: false, snout.HasValue ? (snout.Value, snoutRx, snoutRy) : null);
        if (_sp.Plan == BodyPlan.Avian) Beak(hc, hr);
        Face(hc, hr, head, snout, snoutRx, snoutRy);
    }

    void EarsPaint(Vector2 hc, float hr, bool behind)
    {
        switch (_p.Ears)
        {
            case Ears.Pointy when behind:
                foreach (var (deg, lean, bias) in new[] { (-55f, 0.35f, -0.2f), (-118f, -0.3f, 0f) })
                {
                    int ear = Part(Zone.Ear);
                    var b = hc + Vector2.FromAngle(Mathf.DegToRad(deg)) * hr * 0.75f;
                    var dir = new Vector2(0, -1).Rotated(lean);
                    var tip = b + dir * hr * 1.0f;
                    _c.Capsule(b, tip, hr * 0.34f, 0.5f, Mat.Main, ear, bias);
                    _c.Capsule(b + dir * hr * 0.2f, b + dir * hr * 0.7f, hr * 0.15f, 0.5f, Mat.Second, ear, bias);
                }
                break;
            case Ears.Round when behind:
                foreach (var (deg, bias) in new[] { (-50f, -0.2f), (-120f, 0f) })
                {
                    int ear = Part(Zone.Ear);
                    var ec = hc + Vector2.FromAngle(Mathf.DegToRad(deg)) * hr * 0.95f;
                    _c.Ellipse(ec.X, ec.Y, hr * 0.4f, hr * 0.4f, Mat.Main, ear, bias);
                    _c.Ellipse(ec.X - hr * 0.04f, ec.Y + hr * 0.02f, hr * 0.2f, hr * 0.2f, Mat.Second, ear, bias - 0.15f);
                }
                break;
            case Ears.Floppy:
                {
                    int ear = Part(Zone.Ear);
                    if (behind)
                        _c.Capsule(hc + new Vector2(hr * 0.45f, -hr * 0.6f), hc + new Vector2(hr * 1.0f, hr * 0.35f), hr * 0.22f, hr * 0.3f, Mat.Main, ear, -0.3f);
                    else
                        _c.Capsule(hc + new Vector2(hr * 0.3f, -hr * 0.75f), hc + new Vector2(hr * 0.62f, hr * 0.45f), hr * 0.22f, hr * 0.32f, Mat.Main, ear);
                    break;
                }
        }
    }

    void HornsPaint(Vector2 hc, float hr, bool behind, (Vector2 c, float rx, float ry)? snout)
    {
        float curve = _sp.HornCurve;
        switch (_p.Horns)
        {
            case Horns.NoseHorn when !behind:
                {
                    int horn = Part(Zone.Tip);
                    if (_sp.Plan == BodyPlan.Aquatic)
                    {
                        var b = hc + new Vector2(-hr * 0.85f, -hr * 0.05f);
                        _c.Capsule(b, b + new Vector2(-hr * 1.4f, -hr * 0.35f), hr * 0.17f, 0.5f, Mat.Accent, horn);
                        break;
                    }
                    var nb = snout.HasValue
                        ? snout.Value.c + new Vector2(snout.Value.rx * 0.05f, -snout.Value.ry * 0.7f)
                        : hc + new Vector2(-hr * 0.55f, -hr * 0.55f);
                    float len = _sp.Plan == BodyPlan.Insectoid ? 1.3f : 0.9f;
                    _c.Chain(new[] { nb, nb + new Vector2(-hr * 0.2f, -hr * 0.5f * len), nb + new Vector2(-hr * (0.15f - curve * 0.2f), -hr * 0.95f * len) },
                        hr * 0.26f, 0.5f, Mat.Accent, horn);
                    break;
                }
            case Horns.BrowHorn when !behind:
                {
                    // One horn from the forehead, a unicorn's.
                    int horn = Part(Zone.Tip);
                    var b = hc + new Vector2(-hr * 0.3f, -hr * 0.78f);
                    _c.Chain(new[] { b, b + new Vector2(-hr * 0.22f, -hr * 0.55f), b + new Vector2(-hr * (0.4f - curve * 0.15f), -hr * 1.15f) },
                        hr * 0.2f, 0.5f, Mat.Accent, horn);
                    break;
                }
            case Horns.TwinHorns when _sp.Plan == BodyPlan.Insectoid:
                if (!behind)
                {
                    foreach (float dy in new[] { -0.05f, 0.45f })
                    {
                        int mandible = Part(Zone.Tip);
                        var b = hc + new Vector2(-hr * 0.75f, hr * dy);
                        _c.Chain(new[] { b, b + new Vector2(-hr * 0.55f, -hr * 0.1f), b + new Vector2(-hr * 0.75f, hr * 0.25f) }, hr * 0.2f, 0.5f, Mat.Accent, mandible);
                    }
                }
                break;
            case Horns.TwinHorns:
                {
                    int horn = Part(Zone.Tip);
                    var b = behind ? hc + new Vector2(hr * 0.35f, -hr * 0.72f) : hc + new Vector2(-hr * 0.2f, -hr * 0.82f);
                    var p1 = b + new Vector2(curve * hr * 0.25f, -hr * 0.55f);
                    var p2 = p1 + new Vector2(curve * hr * 0.55f, -hr * 0.35f + Mathf.Abs(curve) * hr * 0.15f);
                    _c.Chain(new[] { b, p1, p2 }, hr * 0.24f, 0.5f, Mat.Accent, horn, behind ? -0.3f : 0);
                    break;
                }
            case Horns.Antlers:
                {
                    int antler = Part(Zone.Tip);
                    float bias = behind ? -0.3f : 0;
                    float r = Mathf.Max(0.6f, hr * 0.12f);
                    var b = behind ? hc + new Vector2(hr * 0.3f, -hr * 0.75f) : hc + new Vector2(-hr * 0.2f, -hr * 0.82f);
                    var mid = b + new Vector2(hr * 0.2f, -hr * 0.7f);
                    var top = b + new Vector2(hr * 0.6f, -hr * 1.25f);
                    _c.Chain(new[] { b, mid, top }, r * 1.3f, r, Mat.Accent, antler, bias);
                    _c.Capsule(mid, mid + new Vector2(-hr * 0.3f, -hr * 0.45f), r, 0.5f, Mat.Accent, antler, bias);
                    _c.Capsule(Lerp(mid, top, 0.6f), Lerp(mid, top, 0.6f) + new Vector2(-hr * 0.05f, -hr * 0.45f), r, 0.5f, Mat.Accent, antler, bias);
                    break;
                }
            case Horns.Crest when behind:
                {
                    int crest = Part(Zone.Tip);
                    float len = _sp.Plan == BodyPlan.Avian ? 1.75f : 1.45f;
                    for (int i = 0; i < 3; i++)
                    {
                        var dir = Vector2.FromAngle(Mathf.DegToRad(-105 + i * 32));
                        float l = len * (i == 1 ? 1.1f : 0.85f);
                        _c.Capsule(hc + dir * hr * 0.6f, hc + dir * hr * l, hr * 0.24f, 0.5f, _crestMat, crest);
                    }
                    break;
                }
            case Horns.Antennae when behind:
                {
                    var tipMat = Mat.Accent;
                    if (_sp.Plan == BodyPlan.Aquatic)
                    {
                        // Anglerfish lure: one stalk from the forehead, a glowing bulb out front.
                        int lure = Part(Zone.Tip);
                        var b = hc + new Vector2(-hr * 0.3f, -hr * 0.8f);
                        var p1 = b + new Vector2(-hr * 0.1f, -hr * 0.9f);
                        var p2 = p1 + new Vector2(-hr * 0.7f, hr * 0.2f);
                        _c.Chain(new[] { b, p1, p2 }, 0.6f, 0.5f, Mat.Main, lure, -0.2f);
                        float br = Mathf.Max(1.2f, hr * 0.2f);
                        _c.Ellipse(p2.X, p2.Y + br * 0.6f, br, br, tipMat, lure, 0.35f);
                        break;
                    }
                    foreach (float dx in new[] { 0.15f, -0.3f })
                    {
                        int ant = Part(Zone.Tip);
                        var b = hc + new Vector2(hr * dx, -hr * 0.8f);
                        var p1 = b + new Vector2(-hr * 0.15f, -hr * 0.65f);
                        var p2 = p1 + new Vector2(-hr * 0.45f, -hr * 0.25f);
                        _c.Chain(new[] { b, p1, p2 }, 0.6f, 0.5f, Mat.Main, ant, dx > 0 ? -0.3f : -0.1f);
                        float br = Mathf.Max(1f, hr * 0.15f);
                        _c.Ellipse(p2.X, p2.Y, br, br, tipMat, ant);
                    }
                    break;
                }
        }
    }

    void Beak(Vector2 hc, float hr)
    {
        int beak = Part(Zone.Tip);
        float len = hr * 0.8f * _sp.SnoutLen;
        _c.Polygon(new[]
        {
            new Vector2(hc.X - hr * 0.6f, hc.Y - hr * 0.08f),
            new Vector2(hc.X - hr * 0.6f - len, hc.Y + hr * 0.22f),
            new Vector2(hc.X - hr * 0.5f, hc.Y + hr * 0.48f),
        }, Mat.Accent, beak);
    }

    void Face(Vector2 hc, float hr, int part, Vector2? snout, float snoutRx, float snoutRy)
    {
        float frac = _cr.Size switch
        {
            SizeClass.Tiny => 0.34f, SizeClass.Small => 0.3f, SizeClass.Medium => 0.27f, SizeClass.Large => 0.24f, _ => 0.22f,
        } * _sp.EyeSize;
        int e = _p.Eyes == EyeKind.Compound
            ? Mathf.Clamp(Mathf.RoundToInt(hr * 0.36f), 1, 6)
            : Mathf.Clamp(Mathf.RoundToInt(hr * frac), 1, 5);
        bool avian = _sp.Plan == BodyPlan.Avian;

        float eyeY = hc.Y - hr * (avian ? 0.22f : snout.HasValue ? 0.15f : 0.02f);
        float farX = Mathf.Max(hc.X - hr * (avian ? 0.45f : 0.62f), hc.X - hr + 0.9f * e + 0.5f);
        float nearX = farX + Mathf.Max(2 * e + 1.5f, hr * 0.6f);
        int fy = Mathf.FloorToInt(eyeY);
        Eye(Mathf.FloorToInt(farX), fy, e, part, far: true);
        Eye(Mathf.FloorToInt(nearX), fy, e, part, far: false);

        if (_p.Pattern == Pattern.Cheeks)
        {
            int cy = fy + e + 1;
            int cx = Mathf.FloorToInt(nearX + e * 0.6f);
            foreach (int x in new[] { cx, cx + 1 })
                if (_c.MatAt(x, cy) is Mat.Main or Mat.Second) _c.Plot(x, cy, Mat.Blush, part);
        }

        if (avian || _sp.Plan == BodyPlan.Insectoid) return;
        Vector2 m = snout.HasValue
            ? new Vector2(snout.Value.X - snoutRx * 0.25f, snout.Value.Y + snoutRy * 0.4f)
            : new Vector2((farX + nearX) / 2f, hc.Y + hr * 0.5f);
        Mouth(Mathf.FloorToInt(m.X), Mathf.FloorToInt(m.Y), part, hr);
    }

    /// <summary>Eyes carry most of the charm, so they're drawn pixel by pixel; attitude shapes the lids.</summary>
    void Eye(int ex, int ey, int e, int part, bool far)
    {
        var att = _cr.Attitude;
        bool lidded = false;
        if (_p.Eyes == EyeKind.Compound)
        {
            _c.Ellipse(ex + 0.5f, ey + 0.5f, e * (far ? 0.75f : 0.95f), e * 1.1f, Mat.Pupil, part, flat: true);
            _c.Plot(ex - e / 2, ey - e / 2, Mat.Pupil, part, 2);
            if (e >= 3) _c.Plot(ex - e / 2 + 1, ey - e / 2, Mat.Pupil, part, 2);
        }
        else if ((_p.Eyes == EyeKind.Beady && !DarkBody) || e <= 1)
        {
            if (e <= 2)
            {
                _c.Plot(ex, ey, Mat.Pupil, part);
                _c.Plot(ex, ey + 1, Mat.Pupil, part);
                if (e == 2 && !far)
                {
                    _c.Plot(ex - 1, ey, Mat.Pupil, part, 2);
                    _c.Plot(ex - 1, ey + 1, Mat.Pupil, part);
                }
            }
            else
            {
                _c.Ellipse(ex + 0.5f, ey + 0.5f, e * 0.5f, e * 0.65f, Mat.Pupil, part, flat: true);
                _c.Plot(ex - 1, ey - 1, Mat.Pupil, part, 2);
            }
        }
        else
        {
            float rx = e * (far ? 0.7f : 0.85f), ry = e;
            _c.Ellipse(ex + 0.5f, ey + 0.5f, rx, ry, Mat.EyeWhite, part, flat: true);
            float pr = att switch { Attitude.Skittish => 0.4f, Attitude.Loyal => 0.72f, _ => 0.56f };
            float px = ex + 0.5f - rx * 0.28f;
            float py = ey + 0.5f + (att == Attitude.Curious ? -ry * 0.2f : ry * 0.12f);
            _c.Ellipse(px, py, Mathf.Max(rx * pr, 0.5f), Mathf.Max(ry * pr * 1.15f, 0.6f), Mat.Pupil, part, flat: true);
            _c.Plot(Mathf.FloorToInt(px - rx * pr * 0.3f), Mathf.FloorToInt(py - ry * pr * 0.5f), Mat.Pupil, part, 2);
            if (att == Attitude.Loyal && e >= 3)
                _c.Plot(Mathf.FloorToInt(px + rx * pr * 0.4f), Mathf.FloorToInt(py + ry * pr * 0.45f), Mat.Pupil, part, 2);
            lidded = true;
        }

        if (att is not (Attitude.Aggressive or Attitude.Proud)) return;
        // Aggressive: lids slope down toward the middle of the face. Proud: flat, half-closed lids.
        int inward = far ? 1 : -1;
        for (int x = ex - e - 1; x <= ex + e + 1; x++)
        {
            float lid = att == Attitude.Aggressive
                ? ey - e + 0.5f + (x - ex) * inward * 0.6f + e * 0.3f
                : ey - e * 0.1f;
            if (!lidded)
            {
                // Beady and compound eyes get a brow line above instead of a lid.
                int by = Mathf.FloorToInt(lid) - 1;
                if (Mathf.Abs(x - ex) <= e && _c.MatAt(x, by) is Mat.Main or Mat.Second) _c.Plot(x, by, Mat.Line, part);
                continue;
            }
            for (int y = ey - e - 1; y <= ey + e + 1; y++)
            {
                if (_c.MatAt(x, y) is not (Mat.EyeWhite or Mat.Pupil) || _c.PartAt(x, y) != part) continue;
                if (y < Mathf.FloorToInt(lid)) _c.Plot(x, y, Mat.Main, part);
                else if (y == Mathf.FloorToInt(lid)) _c.Plot(x, y, Mat.Line, part);
            }
        }
    }

    void Mouth(int mx, int my, int part, float hr)
    {
        int w = hr >= 9 ? 2 : 1;
        var px = new List<(int, int, Mat)>();
        switch (_cr.Attitude)
        {
            case Attitude.Aggressive:
                for (int x = mx - w; x <= mx + w; x++) px.Add((x, my, Mat.Line));
                px.Add((mx - w, my + 1, Mat.EyeWhite));
                if (w == 2) px.Add((mx + 1, my + 1, Mat.EyeWhite));
                break;
            case Attitude.Loyal:
                px.Add((mx - w, my, Mat.Line));
                for (int x = mx - w + 1; x <= mx + w - 1; x++) px.Add((x, my + 1, Mat.Line));
                px.Add((mx + w, my, Mat.Line));
                if (w == 2) px.Add((mx, my, Mat.Mouth));
                break;
            case Attitude.Curious:
                px.Add((mx, my, Mat.Mouth));
                if (w == 2) { px.Add((mx + 1, my, Mat.Mouth)); px.Add((mx, my + 1, Mat.Mouth)); px.Add((mx + 1, my + 1, Mat.Mouth)); }
                break;
            case Attitude.Proud:
                for (int x = mx - w; x < mx + w; x++) px.Add((x, my + 1, Mat.Line));
                px.Add((mx + w, my, Mat.Line));
                break;
            default: // skittish: a wobbly line
                for (int i = 0; i <= 2 * w + 1; i++) px.Add((mx - w + i, my + (i % 2 == 0 ? 1 : 0), Mat.Line));
                break;
        }
        foreach (var (x, y, m) in px)
            if (_c.MatAt(x, y) is Mat.Main or Mat.Second) _c.Plot(x, y, m, part);
    }

    // ---------------------------------------------------------------- limbs

    void Leg(Vector2 hip, float r, bool far, bool hind)
    {
        int leg = Part(Zone.Limb);
        float bias = far ? -0.32f : 0;
        // Far feet stand a little higher on screen: that's what sells the three-quarter view.
        float ground = far ? Floor - Mathf.Max(1, S * 0.035f) : Floor;
        var foot = new Vector2(hip.X + (hind ? r * 0.2f : -r * 0.2f), ground - r * 0.6f);
        bool bend = hind && _p.Limbs != Limbs.Stubby && foot.Y - hip.Y > r * 2.5f;
        if (bend)
        {
            // Hind legs bend back at the hock, like a dog's or a deer's.
            var hock = new Vector2(hip.X + r * 0.9f, Mathf.Lerp(hip.Y, foot.Y, 0.55f));
            _c.Capsule(hip, hock, r * 1.35f, r * 0.8f, Mat.Main, leg, bias);
            _c.Capsule(hock, foot, r * 0.8f, r * 0.7f, Mat.Main, leg, bias);
        }
        else
        {
            _c.Capsule(hip, foot, r * 1.3f, r * 0.78f, Mat.Main, leg, bias);
        }

        if (_p.Limbs == Limbs.Hooved)
        {
            _c.Ellipse(foot.X - r * 0.1f, ground - r * 0.45f, r * 0.95f, r * 0.55f, Mat.Accent, leg, bias - 0.15f, clipBelow: ground);
            return;
        }
        float fx = foot.X - r * 0.3f;
        _c.Ellipse(fx, ground - r * 0.6f, r * 1.15f, r * 0.62f, Mat.Main, leg, bias, clipBelow: ground);
        if (_p.Limbs == Limbs.Clawed) Claws(fx - r * 1.15f, ground, r, leg);
    }

    void Arm(Vector2 shoulder, Vector2 hand, float r, bool far)
    {
        int arm = Part(Zone.Limb);
        float bias = far ? -0.32f : 0;
        _c.Capsule(shoulder, hand, r * 1.0f, r * 0.8f, Mat.Main, arm, bias);
        _c.Ellipse(hand.X, hand.Y, r * 1.05f, r * 0.95f, Mat.Main, arm, bias);
        if (_p.Limbs == Limbs.Clawed)
        {
            int hx = Mathf.FloorToInt(hand.X - r), hy = Mathf.FloorToInt(hand.Y + r * 0.5f);
            _c.Plot(hx - 1, hy, Mat.Accent, arm, 2);
            _c.Plot(hx, hy + 1, Mat.Accent, arm, 2);
        }
    }

    /// <summary>Claw tips poking out of the front of a foot, on the ground row.</summary>
    void Claws(float frontX, float ground, float r, int part)
    {
        int y = Mathf.FloorToInt(ground) - 1;
        int x = Mathf.FloorToInt(frontX);
        int n = r >= 2.5f ? 3 : 2;
        for (int i = 0; i < n; i++) _c.Plot(x - 1 + i * 2, y, Mat.Accent, part, 2);
    }

    void BirdLeg(Vector2 hip, float r, bool far)
    {
        int leg = Part(Zone.Limb);
        float bias = far ? -0.35f : 0;
        var foot = new Vector2(hip.X + r, Floor - 0.5f);
        var knee = new Vector2(hip.X + r * 2.5f, Mathf.Lerp(hip.Y, foot.Y, 0.45f));
        _c.Capsule(hip, knee, r * 1.2f, r, Mat.Accent, leg, bias);
        _c.Capsule(knee, foot, r, r, Mat.Accent, leg, bias);
        float toe = Mathf.Max(2.5f, S * 0.06f);
        _c.Capsule(foot, foot + new Vector2(-toe, 0), r, 0.5f, Mat.Accent, leg, bias);
        _c.Capsule(foot, foot + new Vector2(toe * 0.5f, 0), r, 0.5f, Mat.Accent, leg, bias);
        if (_p.Limbs == Limbs.Clawed) _c.Plot(Mathf.FloorToInt(foot.X - toe) - 1, Mathf.FloorToInt(Floor) - 1, Mat.Pupil, leg);
    }

    void InsectLeg(Vector2 thorax, float rt, int k, float legLen, float r, bool far)
    {
        int leg = Part(Zone.Limb);
        float bias = far ? -0.35f : 0;
        float off = far ? S * 0.03f : 0;
        float dx = (k switch { 0 => -1f, 1 => 0.15f, _ => 1f }) * legLen * 0.6f;
        var hip = new Vector2(thorax.X + (k - 1) * rt * 0.6f + off, thorax.Y + rt * 0.45f);
        var knee = hip + new Vector2(dx, -legLen * 0.45f);
        var foot = new Vector2(hip.X + dx * 1.5f, Floor - 0.5f);
        _c.Capsule(hip, knee, r, r * 0.8f, Mat.Main, leg, bias);
        _c.Capsule(knee, foot, r * 0.8f, 0.5f, Mat.Main, leg, bias);
    }

    // ---------------------------------------------------------------- wings

    void Wing(Vector2 root, float span, bool far)
    {
        float bias = far ? -0.3f : 0;
        float a = Mathf.DegToRad(-62 + (_sp.WingSpread - 0.5f) * 30) + (far ? -0.3f : 0);
        var dir = Vector2.FromAngle(a);
        switch (_p.Wings)
        {
            case Wings.Bat:
                {
                    int wing = Part(Zone.Limb);
                    var wrist = root + dir * span * 0.55f;
                    var tips = new Vector2[3];
                    for (int k = 0; k < 3; k++)
                        tips[k] = wrist + Vector2.FromAngle(a + 0.25f + k * 0.5f) * span * (0.75f - k * 0.15f);
                    var poly = new List<Vector2> { root, wrist, tips[0] };
                    for (int k = 1; k < 3; k++)
                    {
                        poly.Add(Lerp(Lerp(tips[k - 1], tips[k], 0.5f), wrist, 0.3f)); // scallop between fingers
                        poly.Add(tips[k]);
                    }
                    poly.Add(root + new Vector2(span * 0.3f, span * 0.1f));
                    _c.Polygon(poly, Mat.Membrane, wing, bias);
                    float br = Mathf.Max(0.6f, span * 0.06f);
                    _c.Capsule(root, wrist, br * 1.3f, br, Mat.Main, wing, bias - 0.1f);
                    foreach (var t in tips) _c.Capsule(wrist, t, br * 0.8f, 0.5f, Mat.Main, wing, bias - 0.1f);
                    break;
                }
            case Wings.Feather:
            case Wings.Tiny:
                {
                    bool tiny = _p.Wings == Wings.Tiny;
                    float sp = tiny ? span * 0.45f : span;
                    var wrist = root + dir * sp * 0.5f;
                    int n = tiny ? 3 : 5;
                    for (int k = n - 1; k >= 0; k--)
                    {
                        int feather = Part(Zone.Limb);
                        var b = Lerp(wrist, root, k / (float)n);
                        float len = sp * (0.72f - k * 0.08f);
                        _c.Capsule(b, b + Vector2.FromAngle(a + 0.15f + k * 0.3f) * len, sp * 0.1f, 0.5f, _featherMat, feather, bias + 0.05f);
                    }
                    int covert = Part(Zone.Limb);
                    var cc = Lerp(root, wrist, 0.55f);
                    _c.Ellipse(cc.X, cc.Y, sp * 0.32f, sp * 0.16f, Mat.Main, covert, bias, rot: a);
                    break;
                }
            case Wings.Insect:
                {
                    for (int k = 1; k >= 0; k--)
                    {
                        int wing = Part(Zone.Limb);
                        float ang = a + 0.15f + k * 0.55f;
                        float len = span * (k == 0 ? 1f : 0.75f);
                        var c = root + Vector2.FromAngle(ang) * len * 0.5f;
                        _c.Ellipse(c.X, c.Y, len * 0.5f, len * 0.17f, Mat.Membrane, wing, bias + 0.2f, rot: ang);
                        _c.Capsule(root, root + Vector2.FromAngle(ang) * len * 0.85f, 0.5f, 0.5f, Mat.Membrane, wing, bias - 0.8f);
                    }
                    break;
                }
        }
    }

    void FoldedWing(Vector2 c, float rx, float ry)
    {
        int tips = Part(Zone.Limb);
        for (int k = 0; k < 3; k++)
        {
            var b = new Vector2(c.X + rx * 0.4f, c.Y + ry * (0.0f + k * 0.18f));
            _c.Capsule(b, new Vector2(c.X + rx * 1.15f, c.Y + ry * (-0.05f + k * 0.22f)), ry * 0.18f, 0.5f, _featherMat, tips);
        }
        int wing = Part(Zone.Limb);
        _c.Ellipse(c.X + rx * 0.12f, c.Y - ry * 0.05f, rx * 0.72f, ry * 0.55f, Mat.Main, wing, 0.05f, rot: 0.25f);
    }

    // ---------------------------------------------------------------- tails

    void TailPaint(Vector2 root, float thick, float len)
    {
        if (_sp.Plan == BodyPlan.Serpent) return; // a serpent's tail is its body; TailTip decorates the end
        int tail = Part(Zone.Tail);
        switch (_p.Tail)
        {
            case Tail.None:
                if (_sp.Plan is BodyPlan.Quadruped or BodyPlan.Biped)
                    _c.Ellipse(root.X + thick * 0.3f, root.Y - thick * 0.2f, thick * 0.4f, thick * 0.35f, Mat.Main, tail);
                break;
            case Tail.Whip:
                {
                    if (_sp.Plan == BodyPlan.Insectoid)
                    {
                        _c.Capsule(root, root + new Vector2(len * 0.5f, len * 0.1f), thick * 0.45f, 0.5f, Mat.Accent, tail);
                        break;
                    }
                    var pts = new[] { root, root + new Vector2(len * 0.45f, -len * 0.05f), root + new Vector2(len * 0.8f, -len * 0.35f), root + new Vector2(len * 0.95f, -len * 0.7f) };
                    _c.Chain(pts, thick * 0.3f, 0.5f, Mat.Main, tail);
                    int tuft = Part(Zone.Tip);
                    _c.Ellipse(pts[^1].X, pts[^1].Y, thick * 0.38f, thick * 0.5f, _tuftMat, tuft);
                    break;
                }
            case Tail.Thick:
                _c.Chain(new[] { root, root + new Vector2(len * 0.5f, thick * 0.3f), root + new Vector2(len * 0.95f, thick * 0.2f) },
                    thick * 0.75f, 0.6f, Mat.Main, tail);
                break;
            case Tail.Fluffy:
                {
                    _c.Capsule(root, root + new Vector2(len * 0.3f, -len * 0.2f), thick * 0.35f, thick * 0.3f, Mat.Main, tail);
                    int puff = Part(Zone.Tail);
                    var pc = root + new Vector2(len * 0.5f, -len * 0.55f);
                    _c.Ellipse(pc.X, pc.Y, thick * 0.8f, thick * 1.1f, Mat.Main, puff, rot: 0.4f);
                    var tip = pc + new Vector2(thick * 0.45f, -thick * 0.65f);
                    _c.Ellipse(tip.X, tip.Y, thick * 0.4f, thick * 0.45f, Mat.Main, puff);
                    break;
                }
            case Tail.Fan:
                if (_sp.Plan == BodyPlan.Aquatic)
                {
                    var up = root + new Vector2(len * 0.55f, -len * 0.5f);
                    var low = root + new Vector2(len * 0.55f, len * 0.42f);
                    _c.Polygon(new[] { root + new Vector2(0, -thick * 0.4f), up, root + new Vector2(len * 0.32f, 0), low, root + new Vector2(0, thick * 0.4f) }, Mat.Second, tail);
                    _c.Capsule(root, up, 0.6f, 0.5f, Mat.Second, tail, -0.5f);
                    _c.Capsule(root, low, 0.6f, 0.5f, Mat.Second, tail, -0.5f);
                }
                else
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int f = Part(Zone.Tail);
                        var d = Vector2.FromAngle(-0.75f + k * 0.32f);
                        _c.Capsule(root, root + d * len * (0.85f - Mathf.Abs(k - 1.5f) * 0.1f), thick * 0.35f, 0.5f, k % 2 == 0 ? _featherMat : Mat.Main, f);
                    }
                }
                break;
            case Tail.Club:
                {
                    var end = root + new Vector2(len * 0.8f, -len * 0.08f);
                    _c.Chain(new[] { root, root + new Vector2(len * 0.4f, -len * 0.12f), end }, thick * 0.4f, thick * 0.28f, Mat.Main, tail);
                    int club = Part(Zone.Tip);
                    _c.Ellipse(end.X, end.Y, thick * 0.6f, thick * 0.55f, Mat.Accent, club);
                    break;
                }
            case Tail.Curl:
                {
                    var center = root + new Vector2(len * 0.42f, -len * 0.3f);
                    var off = root - center;
                    float r0 = off.Length(), a0 = off.Angle();
                    var pts = new List<Vector2>();
                    for (int k = 0; k < 12; k++) pts.Add(center + Vector2.FromAngle(a0 + k * 0.5f) * r0 * (1 - k * 0.055f));
                    _c.Chain(pts, thick * 0.38f, 0.6f, Mat.Main, tail);
                    break;
                }
        }
    }

    /// <summary>A serpent's tail decoration: club, fin, tuft, or a rattle.</summary>
    void TailTip(Vector2 tip, Vector2 dirBack, float r)
    {
        var d = dirBack.Normalized();
        int part = Part(Zone.Tip);
        switch (_p.Tail)
        {
            case Tail.Club:
                _c.Ellipse(tip.X, tip.Y - r * 0.2f, r * 0.75f, r * 0.7f, Mat.Accent, part);
                break;
            case Tail.Fan:
                _c.Polygon(new[] { tip, tip + d * r * 1.6f + new Vector2(0, -r * 1.5f), tip + d * r * 0.9f, tip + d * r * 1.6f + new Vector2(0, r * 0.4f) }, Mat.Second, part);
                break;
            case Tail.Fluffy:
                _c.Ellipse(tip.X + d.X * r * 0.5f, tip.Y - r * 0.5f, r * 0.8f, r * 0.9f, _tuftMat, part, rot: 0.5f);
                break;
            case Tail.Whip:
                for (int k = 0; k < 3; k++)
                {
                    var c = tip + d * (k * r * 0.55f) + new Vector2(0, -k * r * 0.25f);
                    _c.Ellipse(c.X, c.Y, r * 0.38f, r * 0.42f, Mat.Accent, part);
                }
                break;
        }
    }

    // ---------------------------------------------------------------- back

    Vector2 OnEllipse(Vector2 c, float rx, float ry, float deg, float scale = 1) =>
        c + new Vector2(Mathf.Cos(Mathf.DegToRad(deg)) * rx, Mathf.Sin(Mathf.DegToRad(deg)) * ry) * scale;

    Vector2 EllipseNormal(float rx, float ry, float deg) =>
        new Vector2(Mathf.Cos(Mathf.DegToRad(deg)) / rx, Mathf.Sin(Mathf.DegToRad(deg)) / ry).Normalized();

    /// <summary>Back parts that grow from behind the body's outline: spikes, a sail, crystals.</summary>
    void BackBehind(Vector2 c, float rx, float ry, float deg0, float deg1)
    {
        switch (_p.Back)
        {
            case Back.Spikes:
                {
                    int spikes = Part(Zone.Tip);
                    int n = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(rx, ry) / (S * 0.07f)), 3, 6);
                    for (int i = 0; i < n; i++)
                    {
                        float deg = Mathf.Lerp(deg0, deg1, (i + 0.5f) / n);
                        var b = OnEllipse(c, rx, ry, deg, 0.85f);
                        var dir = EllipseNormal(rx, ry, deg).Rotated(0.35f);
                        float len = S * 0.085f * (0.7f + 0.5f * Mathf.Sin(Mathf.Pi * (i + 0.5f) / n));
                        _c.Capsule(b, b + dir * (len + ry * 0.15f), S * 0.04f, 0.5f, _spikeMat, spikes);
                    }
                    break;
                }
            case Back.Sail:
                {
                    int sail = Part(Zone.Tip);
                    var poly = new List<Vector2>();
                    var tops = new List<(Vector2 b, Vector2 t)>();
                    float a0 = Mathf.Lerp(deg0, deg1, 0.08f), a1 = Mathf.Lerp(deg0, deg1, 0.85f);
                    poly.Add(OnEllipse(c, rx, ry, a0, 0.7f));
                    for (int k = 0; k <= 4; k++)
                    {
                        float deg = Mathf.Lerp(a0, a1, k / 4f);
                        var b = OnEllipse(c, rx, ry, deg, 0.9f);
                        float h = S * 0.15f * (0.6f + 0.4f * Mathf.Sin(Mathf.Pi * (k + 0.5f) / 5f));
                        var t = b + EllipseNormal(rx, ry, deg).Rotated(0.3f) * h;
                        tops.Add((b, t));
                        poly.Add(t);
                        if (k < 4) poly.Add(Lerp(t, OnEllipse(c, rx, ry, Mathf.Lerp(a0, a1, (k + 0.5f) / 4f), 1.0f), 0.3f));
                    }
                    poly.Add(OnEllipse(c, rx, ry, a1, 0.7f));
                    _c.Polygon(poly, _sailMat, sail);
                    foreach (var (b, t) in tops) _c.Capsule(b, t, 0.6f, 0.5f, _sailMat, sail, -0.55f);
                    break;
                }
            case Back.Crystals:
                {
                    int n = rx > S * 0.2f ? 3 : 2;
                    for (int i = 0; i < n; i++)
                    {
                        int gem = Part(Zone.Tip);
                        float deg = Mathf.Lerp(deg0, deg1, (i + 0.6f) / (n + 0.2f));
                        var b = OnEllipse(c, rx, ry, deg, 0.8f);
                        var dir = EllipseNormal(rx, ry, deg).Rotated(0.2f);
                        var perp = new Vector2(-dir.Y, dir.X);
                        float len = S * (i == n / 2 ? 0.2f : 0.14f), w = S * 0.045f;
                        _c.Polygon(new[] { b - perp * w, b + dir * len * 0.65f - perp * w, b + dir * len, b + dir * len * 0.65f + perp * w, b + perp * w }, Mat.Accent, gem, 0.1f);
                        _c.Capsule(b + dir * len * 0.2f, b + dir * len * 0.85f, 0.5f, 0.5f, Mat.Accent, gem, 0.6f);
                    }
                    break;
                }
        }
    }

    /// <summary>Back parts that sit on top of the body: a shell or a mane.</summary>
    void BackOver(Vector2 c, float rx, float ry)
    {
        switch (_p.Back)
        {
            case Back.Shell when _sp.Plan != BodyPlan.Insectoid:
                {
                    int shell = Part(Zone.Body);
                    float clip = c.Y + ry * 0.3f;
                    _c.Ellipse(c.X + rx * 0.05f, c.Y - ry * 0.2f, rx * 1.02f, ry * 0.95f, _shellMat, shell, clipBelow: clip);
                    _c.Capsule(new Vector2(c.X - rx * 0.92f, clip - 0.8f), new Vector2(c.X + rx * 0.98f, clip - 0.8f), Mathf.Max(0.7f, ry * 0.12f), Mathf.Max(0.7f, ry * 0.12f), _shellMat, shell, -0.25f);
                    if (rx > 9)
                    {
                        float top = c.Y - ry * 1.15f;
                        foreach (float k in new[] { -0.4f, 0.35f })
                        {
                            float sx = c.X + rx * k;
                            _c.Recolor((x, y) => _c.PartAt(x, y) == shell && Mathf.Abs(x + 0.5f - sx) < 0.5f && y > top + ry * 0.4f && y < clip - 2, Mat.Line);
                        }
                    }
                    break;
                }
            case Back.Mane:
                {
                    int mane = Part(Zone.Body);
                    float r = S * 0.06f;
                    for (int i = 0; i < 6; i++)
                    {
                        float deg = Mathf.Lerp(-175, -95, i / 5f);
                        var p = OnEllipse(c, rx, ry, deg, 0.92f);
                        float rr = r * (i % 2 == 0 ? 1.1f : 0.9f);
                        _c.Ellipse(p.X, p.Y, rr, rr, _tuftMat == Mat.Main ? Mat.Second : _tuftMat, mane, 0.1f);
                    }
                    break;
                }
        }
    }

    /// <summary>A serpent's back parts, placed along its body curve instead of an ellipse.</summary>
    void BackAlong(List<Vector2> pts, Func<float, float> radius, bool behind)
    {
        if (behind == (_p.Back == Back.Mane)) return;
        int part = Part(_p.Back == Back.Mane ? Zone.Body : Zone.Tip);
        int n = _p.Back == Back.Mane ? 6 : 5;
        for (int i = 0; i < n; i++)
        {
            float t = _p.Back == Back.Mane ? Mathf.Lerp(0.72f, 0.95f, i / (n - 1f)) : Mathf.Lerp(0.3f, 0.75f, i / (n - 1f));
            var p = Along(pts, t);
            var tangent = (Along(pts, Mathf.Min(1, t + 0.02f)) - Along(pts, Mathf.Max(0, t - 0.02f))).Normalized();
            var up = new Vector2(tangent.Y, -tangent.X);
            if (up.Y > 0) up = -up;
            float r = radius(t);
            var b = p + up * r * 0.6f;
            switch (_p.Back)
            {
                case Back.Spikes:
                    _c.Capsule(b, b + up.Rotated(0.35f) * (r * 0.4f + S * 0.08f), S * 0.035f, 0.5f, _spikeMat, part);
                    break;
                case Back.Sail:
                    _c.Polygon(new[] { b - tangent * r * 0.9f, b + up * (S * 0.12f) + tangent * r * 0.3f, b + tangent * r * 0.9f }, _sailMat, part);
                    break;
                case Back.Crystals when i % 2 == 0:
                    {
                        var perp = tangent * S * 0.04f;
                        _c.Polygon(new[] { b - perp, b + up * S * 0.1f - perp, b + up * S * 0.15f, b + up * S * 0.1f + perp, b + perp }, Mat.Accent, part, 0.1f);
                        break;
                    }
                case Back.Mane:
                    float rr = S * 0.055f * (i % 2 == 0 ? 1.1f : 0.9f);
                    var mc = p + new Vector2(Mathf.Abs(up.X) > 0.5f ? r * 0.7f : 0, -r * 0.5f);
                    _c.Ellipse(mc.X, mc.Y, rr, rr, _tuftMat == Mat.Main ? Mat.Second : _tuftMat, part, 0.1f);
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- patterns

    void ApplyPattern()
    {
        Zone? ZoneAt(int x, int y) => _zones.TryGetValue(_c.PartAt(x, y), out var z) ? z : null;
        bool IsMain(int x, int y) => _c.MatAt(x, y) == Mat.Main;

        switch (_p.Pattern)
        {
            case Pattern.Belly:
                if (_sp.Plan == BodyPlan.Biped)
                    _c.Recolor((x, y) => IsMain(x, y) && ZoneAt(x, y) == Zone.Body && _c.NormalAt(x, y).X < 0.1f && _c.NormalAt(x, y).Y > -0.4f, Mat.Second);
                else
                    _c.Recolor((x, y) => IsMain(x, y) && ZoneAt(x, y) == Zone.Body && _c.NormalAt(x, y).Y > 0.3f, Mat.Second);
                break;
            case Pattern.Saddle:
                _c.Recolor((x, y) => IsMain(x, y) && ZoneAt(x, y) == Zone.Body && _c.NormalAt(x, y).Y < -0.5f, Mat.Second);
                break;
            case Pattern.Stripes:
                {
                    float w = Mathf.Max(1.6f, S * 0.05f), period = w * 2.6f;
                    _c.Recolor((x, y) => IsMain(x, y) && ZoneAt(x, y) is Zone.Body or Zone.Tail && _c.NormalAt(x, y).Y < 0.5f
                        && Mathf.PosMod(x + y * 0.35f, period) < w, Mat.Second);
                    break;
                }
            case Pattern.Spots:
                {
                    float cell = Mathf.Max(4f, S * 0.11f);
                    _c.Recolor((x, y) =>
                    {
                        if (!IsMain(x, y) || ZoneAt(x, y) is not (Zone.Body or Zone.Tail or Zone.Head) || _c.NormalAt(x, y).Y > 0.35f) return false;
                        int gx = Mathf.FloorToInt(x / cell), gy = Mathf.FloorToInt(y / cell);
                        var h = new SeededRng(SeededRng.Derive(_sp.Seed, (ulong)(uint)(gx * 73856093 ^ gy * 19349663)));
                        if (h.Chance(0.35f)) return false;
                        var sc = new Vector2((gx + h.Range(0.25f, 0.75f)) * cell, (gy + h.Range(0.25f, 0.75f)) * cell);
                        return (new Vector2(x + 0.5f, y + 0.5f) - sc).Length() < cell * h.Range(0.2f, 0.32f);
                    }, Mat.Second);
                    break;
                }
            case Pattern.Tips:
                {
                    var tops = new Dictionary<int, float>();
                    for (int y = 0; y < _c.H; y++)
                    for (int x = 0; x < _c.W; x++)
                    {
                        int part = _c.PartAt(x, y);
                        if (_c.MatAt(x, y) != Mat.None && !tops.ContainsKey(part)) tops[part] = y;
                    }
                    float foot = Floor - Mathf.Max(2, S * 0.07f);
                    _c.Recolor((x, y) => IsMain(x, y) && ZoneAt(x, y) switch
                    {
                        Zone.Ear => y < tops[_c.PartAt(x, y)] + S * 0.06f,
                        Zone.Tip => true,
                        Zone.Limb => y >= foot,
                        _ => false,
                    }, Mat.Second);
                    break;
                }
        }
    }

    // ---------------------------------------------------------------- geometry helpers

    static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * t;

    static List<Vector2> Spline(IReadOnlyList<Vector2> c, int perSegment)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < c.Count - 1; i++)
        {
            Vector2 p0 = c[Math.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Math.Min(i + 2, c.Count - 1)];
            for (int k = 0; k < perSegment; k++) o.Add(p1.CubicInterpolate(p2, p0, p3, k / (float)perSegment));
        }
        o.Add(c[^1]);
        return o;
    }

    static Vector2 Along(List<Vector2> pts, float t)
    {
        float f = Mathf.Clamp(t, 0, 1) * (pts.Count - 1);
        int i = Math.Min((int)f, pts.Count - 2);
        return Lerp(pts[i], pts[i + 1], f - i);
    }
}
