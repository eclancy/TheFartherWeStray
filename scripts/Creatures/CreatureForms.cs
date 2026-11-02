using System.Collections.Generic;
using Godot;

namespace CreatureTactics.Creatures;

/// <summary>
/// The forms: each lays out a body in creature space (X forward, Y left, Z up, in pixels for a medium
/// creature; <c>V</c> scales them) and sets the anchors the shared parts hang from. Lean by design:
/// deep chests and tucked waists, long limbs and necks, nothing round unless it's meant to be.
/// </summary>
public sealed partial class CreatureSculptor
{
    /// <summary>A four-legged body, from a handful of proportions.</summary>
    readonly record struct Quad(float LegH, float Len, float HipW, float HipH, float WaistW, float WaistH, float ChestW, float ChestH,
        float Drop, float NeckLen, float NeckRise, float NeckR, float HeadR, float Pitch, float Sprawl, float LegR, float Hunch);

    static readonly Quad HoundQ = new(17, 30, 4.6f, 5.4f, 3.5f, 4.2f, 5.2f, 7.4f, 2.4f, 9, 52, 3.1f, 5.5f, -14, 0, 2.5f, 0);
    static readonly Quad StagQ = new(24, 26, 4.2f, 5f, 3.5f, 4.4f, 4.6f, 6.4f, 1.5f, 13, 64, 2.5f, 4.7f, -18, 0, 1.9f, 0);
    static readonly Quad CatQ = new(13, 32, 4.8f, 5.3f, 3.9f, 4.4f, 5.2f, 6.2f, 1.5f, 6, 28, 3.3f, 5.4f, -6, 0, 2.6f, 1.2f);
    static readonly Quad DrakeQ = new(9, 30, 5.4f, 5.1f, 5.0f, 4.8f, 5.7f, 5.8f, 0.5f, 14, 56, 3.1f, 5.1f, -10, 4f, 2.7f, 0);
    static readonly Quad BehemothQ = new(16, 30, 6.4f, 7f, 5.8f, 6.6f, 7.8f, 9.6f, 1f, 6, -8, 4.8f, 6.0f, -12, 1f, 4.1f, 4f);

    void BuildForm()
    {
        _bodyPart = Part(Zone.Body);
        switch (_form)
        {
            case Form.Hound: Quadruped(HoundQ); break;
            case Form.Stag: Quadruped(StagQ); break;
            case Form.Cat: Quadruped(CatQ); break;
            case Form.Drake: Quadruped(DrakeQ); break;
            case Form.Behemoth: Quadruped(BehemothQ); break;
            case Form.Raptor: Raptor(); break;
            case Form.Sentinel: Sentinel(); break;
            case Form.Wraith: Wraith(); break;
            case Form.Imp: Imp(); break;
            case Form.Wyrm: Wyrm(); break;
            case Form.Lung: Lung(); break;
            case Form.Hawk: Hawk(); break;
            case Form.Crane: Crane(); break;
            case Form.Phoenix: Phoenix(); break;
            case Form.Mantis: Mantis(); break;
            case Form.Beetle: Beetle(); break;
            case Form.Spider: Spider(); break;
            case Form.Eel: Eel(); break;
            case Form.Ray: Ray(); break;
            case Form.Kelpie: Kelpie(); break;
            case Form.Wisp: Wisp(); break;
            case Form.Jelly: Jelly(); break;
            default: Ooze(); break;
        }
    }

    bool Metal(int side) => side == -1 && Has(Graft.MetalLimb);
    static readonly int[] Sides = { 1, -1 }; // far side first, then the side facing the camera

    // ---------------------------------------------------------------- quadrupeds

    void Quadruped(Quad q)
    {
        float L = q.Len * BodyMul, lh = q.LegH * LegMul, g = Girth;
        float hipX = -L * 0.45f, chestX = L * 0.4f;
        Loft(new List<(Vector3, float, float)>
        {
            (V(hipX, 0, lh + q.HipH * 0.55f), q.HipW * g * u, q.HipH * u),
            (V(-L * 0.05f, 0, lh + q.WaistH * 0.7f + q.Hunch * 0.3f), q.WaistW * g * u, q.WaistH * u),
            (V(chestX - 3, 0, lh + q.ChestH * 0.55f - q.Drop + q.Hunch), q.ChestW * g * u, q.ChestH * u),
            (V(chestX + 1.5f, 0, lh + q.ChestH * 0.6f + q.Hunch), q.ChestW * 0.82f * g * u, q.ChestH * 0.78f * u),
        }, Mat.Main, _bodyPart);

        float nl = q.NeckLen * Mathf.Lerp(0.75f, 1.25f, Mathf.Clamp((_sp.NeckLen - 0.6f) / 0.8f, 0, 1));
        float rise = Mathf.DegToRad(q.NeckRise);
        var nb = V(chestX + 1.5f, 0, lh + q.ChestH * 0.95f + q.Hunch);
        SetHead(nb + V(Mathf.Cos(rise) * nl + q.HeadR * 0.5f, 0, Mathf.Sin(rise) * nl + q.HeadR * 0.2f), q.HeadR, q.Pitch);
        Neck(nb - V(1.5f, 0, 1.5f), _headC - _headFwd * _headR * 0.55f, q.NeckR * 1.25f, q.NeckR, V(0, 0, 1f));

        float fx = chestX - 1.5f, hx = hipX + 1.5f;
        foreach (int side in Sides)
        {
            float yf = side * q.ChestW * g * 0.62f, yh = side * q.HipW * g * 0.62f, sp = side * q.Sprawl;
            Leg(new[] { V(fx, yf, lh + 2), V(fx - 1.6f, yf + sp, lh * 0.55f), V(fx + 0.4f, yf + sp * 0.6f, lh * 0.17f), V(fx + 1.4f, yf + sp * 0.6f, 0) }, q.LegR, Metal(side));
            Leg(new[] { V(hx, yh, lh + 3), V(hx + 3.2f, yh + sp, lh * 0.58f), V(hx - 2.4f, yh + sp * 0.6f, lh * 0.24f), V(hx - 1f, yh + sp * 0.6f, 0) }, q.LegR * 1.05f);
        }
        _tailRoot = V(hipX - q.HipW * 0.55f, 0, lh + q.HipH * 0.7f);
        _tailDir = new Vector3(-1, 0, -0.25f);
        _tailR = q.HipW * 0.5f * g * u;
        _wingRoot = V(chestX - 2, 0, lh + q.ChestH + q.Hunch);
    }

    // ---------------------------------------------------------------- bipeds

    /// <summary>A hunting biped: a level spine balanced on long digitigrade legs, small clawed arms.</summary>
    void Raptor()
    {
        float lh = 19 * LegMul, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-8, 0, lh + 2.5f), 4.0f * g * u, 4.6f * u),
            (V(-1, 0, lh + 3.2f), 4.2f * g * u, 5.2f * u),
            (V(5.5f, 0, lh + 5f), 4.4f * g * u, 5.8f * u),
            (V(8.5f, 0, lh + 7f), 3.4f * g * u, 4.2f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(14.5f, 0, lh + 14.5f), 5f, -10);
        Neck(V(9, 0, lh + 8.5f), _headC - _headFwd * _headR * 0.55f, 3.2f, 2.6f, V(-1, 0, 0.5f));
        foreach (int side in Sides)
        {
            float y = side * 3.6f * g;
            Leg(new[] { V(-1.5f, y, lh + 2), V(3.5f, y * 1.15f, lh * 0.55f), V(-3, y, lh * 0.2f), V(0.5f, y, 0) }, 2.7f);
            Leg(new[] { V(7.5f, y * 1.2f, lh + 5.5f), V(8.5f, y * 1.6f, lh + 1f), V(12f, y * 1.4f, lh - 0.5f) }, 1.3f, Metal(side), hand: true);
        }
        _tailRoot = V(-11, 0, lh + 2.5f);
        _tailDir = new Vector3(-1, 0, 0.05f);
        _tailR = 3.2f * g * u;
        _wingRoot = V(5, 0, lh + 9);
    }

    /// <summary>An upright beast-knight: V-shaped torso, long arms, digitigrade legs.</summary>
    void Sentinel()
    {
        _upright = true;
        float lh = 17 * LegMul, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-0.5f, 0, lh + 1), 4.4f * g * u, 3.4f * u),
            (V(0.3f, 0, lh + 5.5f), 3.4f * g * u, 3.0f * u),
            (V(1.4f, 0, lh + 10.5f), 6.0f * g * u, 4.6f * u),
            (V(2.0f, 0, lh + 14f), 5.2f * g * u, 3.6f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(4.5f, 0, lh + 20.5f), 5.0f, -6);
        Neck(V(2.4f, 0, lh + 15.5f), _headC - _headFwd * _headR * 0.55f, 2.8f, 2.4f, V(0.5f, 0, 0));
        foreach (int side in Sides)
        {
            _c.Sphere(V(1.8f, side * 6.2f, lh + 13.5f), 2.6f * u * LimbThick, Mat.Main, _bodyPart);
            Leg(new[] { V(1.8f, side * 6.6f, lh + 13.5f), V(-0.5f, side * 8.4f, lh + 7.5f), V(2.2f, side * 8.2f, lh + 1.5f) }, 2.0f, Metal(side), hand: true);
            float y = side * 3.3f * g;
            Leg(new[] { V(0, y, lh + 1.5f), V(3.6f, y * 1.15f, lh * 0.55f), V(-2.4f, y * 1.1f, lh * 0.2f), V(0.6f, y * 1.1f, 0) }, 2.6f);
        }
        _tailRoot = V(-3.2f, 0, lh + 1.5f);
        _tailDir = new Vector3(-1, 0, -0.6f);
        _tailR = 2.4f * g * u;
        _wingRoot = V(-1.5f, 0, lh + 14);
    }

    /// <summary>A gaunt, hovering thing: a ribcage, long reaching arms, and tatters where legs should be.</summary>
    void Wraith()
    {
        _upright = true;
        float hv = 5, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-1, 0, hv + 8), 3.2f * g * u, 2.8f * u),
            (V(0.5f, 0, hv + 12.5f), 3.6f * g * u, 3.0f * u),
            (V(2.2f, 0, hv + 16.5f), 4.8f * g * u, 3.6f * u),
            (V(4.2f, 0, hv + 19f), 4.0f * g * u, 3.0f * u),
        }, Mat.Main, _bodyPart);
        _c.Tube(Smooth(new[] { V(-1, 0, hv + 8), V(-2.5f, 0, hv + 3.5f), V(-6, 0, hv + 0.5f), V(-11, 0, hv + 1.5f) }, 5), 3.0f * u, 0.3f * u, Mat.Main, _bodyPart);
        foreach (int side in Sides)
            _c.Tube(Smooth(new[] { V(-1, side * 1.5f, hv + 7), V(-3, side * 3, hv + 2.5f), V(-7.5f, side * 4.5f, hv + 0.5f), V(-10, side * 6, hv + 2.5f) }, 5), 1.9f * u, 0.25f * u, Mat.Main, _bodyPart);
        SetHead(V(9.2f, 0, hv + 21), 4.6f, -22);
        Neck(V(5, 0, hv + 19.5f), _headC - _headFwd * _headR * 0.55f, 2.4f, 2.0f, V(0, 0, 1));
        foreach (int side in Sides)
        {
            _c.Sphere(V(3.5f, side * 5.2f, hv + 18.5f), 2.2f * u, Mat.Main, _bodyPart);
            Leg(new[] { V(3.5f, side * 5.6f, hv + 18.5f), V(1.2f, side * 8.4f, hv + 11.5f), V(6.5f, side * 7.4f, hv + 4.5f) }, 1.5f, Metal(side), hand: true);
        }
        _tailRoot = V(-3, 0, hv + 7);
        _tailDir = new Vector3(-1, 0, -0.3f);
        _tailR = 2 * u;
        _wingRoot = V(2, 0, hv + 18);
    }

    /// <summary>A small crouching biped with long limbs and a sharp face.</summary>
    void Imp()
    {
        _upright = true;
        float lh = 9.5f * LegMul, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-2, 0, lh + 1.5f), 3.7f * g * u, 3.4f * u),
            (V(-0.2f, 0, lh + 4.5f), 3.3f * g * u, 3.0f * u),
            (V(1.8f, 0, lh + 7.5f), 4.3f * g * u, 3.8f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(4.8f, 0, lh + 12.8f), 5.2f, -4);
        Neck(V(2.6f, 0, lh + 9), _headC - _headFwd * _headR * 0.55f, 2.4f, 2.1f, Vector3.Zero);
        foreach (int side in Sides)
        {
            float y = side * 3.1f * g;
            Leg(new[] { V(-1.5f, y, lh + 1.5f), V(3.4f, y * 1.35f, lh * 0.8f), V(-2.4f, y * 1.2f, lh * 0.28f), V(0.2f, y * 1.2f, 0) }, 2.2f);
            Leg(new[] { V(2.2f, side * 4.6f, lh + 7f), V(1.2f, side * 6.8f, lh + 2.2f), V(5.2f, side * 5.8f, lh - 1.5f) }, 1.4f, Metal(side), hand: true);
        }
        _tailRoot = V(-3, 0, lh + 1.5f);
        _tailDir = new Vector3(-1, 0, 0.3f);
        _tailR = 1.4f * u;
        _wingRoot = V(0, 0, lh + 8);
    }

    // ---------------------------------------------------------------- serpents

    /// <summary>A body as one tube along points, its radius profile given, its points kept as the spine.</summary>
    void SerpentBody(Vector3[] pts, System.Func<float, float> radius)
    {
        _c.Tube(Smooth(pts, 6), radius, Mat.Main, _bodyPart);
        for (int k = 0; k < pts.Length; k++)
        {
            float r = radius(k / (float)(pts.Length - 1));
            _spine.Add((pts[k], r, r));
        }
        _bodyIsTail = true;
        _tailRoot = pts[0];
        _tailDir = pts[0] - pts[1];
        _tailR = radius(0);
    }

    /// <summary>Coiled on the ground, the front reared up into an S.</summary>
    void Wyrm()
    {
        float g = Girth, B = BodyMul, rMax = 4.6f * g * u;
        SerpentBody(new[] { V(-16 * B, -3, 1.2f), V(-11 * B, 3, 2.4f), V(-5 * B, 5.5f, 3.8f), V(0, 1.5f, 4.4f), V(-2, -4, 4.5f), V(3, -6.5f, 4.5f), V(8, -3, 4.8f), V(9.5f, 0, 9.5f), V(8.5f, 0, 16), V(10.5f, 0, 21) },
            t => t < 0.45f ? Mathf.Lerp(0.6f * u, rMax, Mathf.SmoothStep(0, 1, t / 0.45f)) : Mathf.Lerp(rMax, rMax * 0.72f, (t - 0.45f) / 0.55f));
        SetHead(V(13, 0, 22.5f), 4.8f, -18);
        _wingRoot = V(9, 0, 11);
    }

    /// <summary>An eastern dragon, swimming through the air in long waves, trailing whiskers.</summary>
    void Lung()
    {
        float hv = 5, g = Girth, B = BodyMul, rMax = 3.0f * g * u;
        SerpentBody(new[] { V(-27 * B, 2, hv + 7), V(-21 * B, -2.5f, hv + 2.5f), V(-15 * B, 2, hv + 8), V(-9 * B, -2, hv + 3), V(-3 * B, 1.5f, hv + 8.5f), V(3, -1, hv + 5), V(7.5f, 0, hv + 10) },
            t => t < 0.25f ? Mathf.Lerp(0.7f * u, rMax, Mathf.SmoothStep(0, 1, t / 0.25f)) : Mathf.Lerp(rMax, rMax * 0.9f, (t - 0.25f) / 0.75f));
        SetHead(V(10.5f, 0, hv + 11.5f), 4.6f, -6);
        int whisker = Part(Zone.Tip);
        foreach (int side in Sides)
        {
            var b = H(1.1f, side * 0.35f, -0.25f);
            _c.Tube(Smooth(new[] { b, b + V(-2, side * 3, -1), b + V(-7, side * 5, 0.5f), b + V(-12, side * 4, -1.5f) }, 4), 0.5f * u, 0.2f * u, Mat.Accent, whisker);
        }
        _wingRoot = V(2, 0, hv + 8);
    }

    // ---------------------------------------------------------------- avians

    /// <summary>An upright bird of prey on short legs.</summary>
    void Hawk()
    {
        _upright = true;
        _wingSize = 1.15f;
        float lh = 6.5f * LegMul, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-4, 0, lh + 3.2f), 3.4f * g * u, 3.0f * u),
            (V(-0.5f, 0, lh + 8), 4.8f * g * u, 4.6f * u),
            (V(2.4f, 0, lh + 12.5f), 4.3f * g * u, 4.0f * u),
            (V(3.2f, 0, lh + 15), 3.2f * g * u, 3.0f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(5.2f, 0, lh + 18.2f), 4.6f, -8);
        Neck(V(3.2f, 0, lh + 14.5f), _headC - _headFwd * _headR * 0.55f, 2.8f, 2.5f, Vector3.Zero);
        foreach (int side in Sides)
        {
            float y = side * 2.4f * g;
            Leg(new[] { V(0, y, lh + 5), V(1.6f, y, lh + 0.5f), V(2, y, 0) }, 1.5f, Metal(side), hand: true);
        }
        _tailRoot = V(-4.6f, 0, lh + 3);
        _tailDir = new Vector3(-0.7f, 0, -0.7f);
        _tailR = 2.2f * u;
        _wingRoot = V(1.8f, 0, lh + 13);
    }

    /// <summary>A crane: stilt legs, a long S of a neck, a dagger beak.</summary>
    void Crane()
    {
        _wingSize = 1.2f;
        float lh = 19 * LegMul, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-7.5f, 0, lh + 5.5f), 3.0f * g * u, 3.0f * u),
            (V(-1, 0, lh + 5), 4.2f * g * u, 4.2f * u),
            (V(4.5f, 0, lh + 6), 3.6f * g * u, 3.8f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(10.5f, 0, lh + 22), 3.6f, -6);
        _c.Tube(Smooth(new[] { V(5.5f, 0, lh + 7.5f), V(8, 0, lh + 12), V(6, 0, lh + 17), _headC - _headFwd * _headR * 0.6f }, 5), 2.3f * u, 1.7f * u, Mat.Main, _bodyPart);
        foreach (int side in Sides)
        {
            float y = side * 2f * g;
            Leg(new[] { V(-0.5f, y, lh + 3.5f), V(1.5f, y, lh * 0.75f), V(-1.2f, y, lh * 0.48f), V(0.8f, y, 0) }, 1.0f, Metal(side), hand: true);
        }
        _tailRoot = V(-8, 0, lh + 5.5f);
        _tailDir = new Vector3(-1, 0, -0.25f);
        _tailR = 2.2f * u;
        _wingRoot = V(1.5f, 0, lh + 8.5f);
    }

    /// <summary>A proud upright firebird whose plumes sweep back along the ground.</summary>
    void Phoenix()
    {
        _upright = true;
        _wingSize = 1.25f;
        float lh = 8 * LegMul, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-3.5f, 0, lh + 4), 3.4f * g * u, 3.2f * u),
            (V(0, 0, lh + 9), 4.6f * g * u, 4.4f * u),
            (V(2.6f, 0, lh + 14), 4.0f * g * u, 3.6f * u),
            (V(3.4f, 0, lh + 17), 2.9f * g * u, 2.8f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(5.4f, 0, lh + 21.5f), 4.3f, 2);
        Neck(V(3.4f, 0, lh + 16.5f), _headC - _headFwd * _headR * 0.55f, 2.6f, 2.2f, V(-0.6f, 0, 0));
        foreach (int side in Sides)
        {
            float y = side * 2.4f * g;
            Leg(new[] { V(0, y, lh + 5), V(1.6f, y, lh + 0.5f), V(2, y, 0) }, 1.4f, Metal(side), hand: true);
        }
        int plume = Part(Zone.Tail);
        for (int k = -1; k <= 1; k++)
        {
            var pts = Smooth(new[] { V(-4, k * 0.8f, lh + 4), V(-10, k * 3, lh + 1.5f), V(-17, k * 5, 1.5f), V(-22, k * 6.5f, 3.5f) }, 5);
            _c.Tube(pts, 1.2f * u, 0.6f * u, _featherMat, plume);
            _c.Sphere(pts[^1], 1.4f * u, Mat.Glow, plume, 0.1f, 0, true);
        }
        _tailRoot = V(-4.2f, 0, lh + 3.6f);
        _tailDir = new Vector3(-0.8f, 0, -0.6f);
        _tailR = 2 * u;
        _wingRoot = V(1.6f, 0, lh + 14.5f);
    }

    // ---------------------------------------------------------------- insectoids

    /// <summary>A mantis: long abdomen, a rising thorax, spined forelegs folded to strike.</summary>
    void Mantis()
    {
        float g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-14, 0, 9.5f), 2.4f * g * u, 2.4f * u),
            (V(-10, 0, 10.5f), 3.8f * g * u, 3.6f * u),
            (V(-5, 0, 11), 3.4f * g * u, 3.2f * u),
            (V(-1.5f, 0, 11.5f), 2.2f * g * u, 2.2f * u),
        }, Mat.Main, _bodyPart);
        _c.Tube(Smooth(new[] { V(-1.5f, 0, 11.5f), V(1.5f, 0, 15.5f), V(3.5f, 0, 20.5f) }, 5), 1.9f * u, 1.5f * u, Mat.Main, _bodyPart);
        SetHead(V(5.4f, 0, 22.2f), 3.7f, -25);
        foreach (int side in Sides)
        {
            int arm = Part(Zone.Limb), spines = Part(Zone.Tip);
            var a = new[] { V(3.2f, side * 1.8f, 19), V(6.2f, side * 3.4f, 13.5f), V(9.8f, side * 2.6f, 19), V(8.5f, side * 2.2f, 22.5f) };
            _c.Tube(Smooth(a, 4), 1.25f * u * LimbThick, 0.5f * u, Metal(side) ? Mat.Metal : Mat.Main, arm);
            for (int k = 0; k < 3; k++)
            {
                var p = a[1].Lerp(a[2], 0.2f + 0.25f * k);
                _c.Tube(new[] { p, p + (Z * 0.7f - X * 0.7f).Normalized() * 1.7f * u }, 0.4f * u, 0.08f * u, Mat.Accent, spines);
            }
            for (int k = 0; k < 2; k++)
            {
                var root = V(0.5f - k * 3.5f, side * 1.6f, 11.5f);
                Leg(new[] { root, root + V(1 - k * 2, side * 6, 4.5f), root + V(2 - k * 4, side * 9, -11.5f) }, 0.8f);
            }
        }
        _tailRoot = V(-16, 0, 9.5f);
        _tailDir = new Vector3(-1, 0, 0.1f);
        _tailR = 1.2f * u;
        _wingRoot = V(-2, 0, 13);
    }

    /// <summary>A stag beetle: a long armoured back, great mandibles, legs splayed wide.</summary>
    void Beetle()
    {
        float g = Girth, B = BodyMul;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-11 * B, 0, 7.5f), 3.6f * g * u, 3.6f * u),
            (V(-6 * B, 0, 8.4f), 5.6f * g * u, 4.8f * u),
            (V(0, 0, 8.6f), 5.8f * g * u, 4.8f * u),
            (V(4.5f, 0, 8.4f), 4.6f * g * u, 3.8f * u),
        }, Mat.Main, _bodyPart);
        _c.Ellipsoid(V(6.5f, 0, 8.6f), X, 2.6f * u, 4.6f * g * u, 3.6f * u, Mat.Main, _bodyPart);
        SetHead(V(9.6f, 0, 8.4f), 3.8f, 0);
        foreach (int side in Sides)
            for (int k = 0; k < 3; k++)
            {
                var root = V(5 - k * 4, side * 3.8f, 7);
                Leg(new[] { root, root + V(1.5f - k * 1.5f, side * 6.5f, 5), root + V(3 - k * 3.5f, side * 10.5f, -7) }, 1.0f, k == 0 && Metal(side));
            }
        _tailRoot = V(-14, 0, 7.5f);
        _tailDir = new Vector3(-1, 0, 0.2f);
        _tailR = 1.4f * u;
        _wingRoot = V(-1, 0, 12);
    }

    /// <summary>A spider: a heavy abdomen and eight legs arching high above it.</summary>
    void Spider()
    {
        float g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-12, 0, 11.5f), 3.2f * g * u, 3.2f * u),
            (V(-7, 0, 11), 5.6f * g * u, 5.4f * u),
            (V(-3, 0, 9.5f), 3.6f * g * u, 3.4f * u),
        }, Mat.Main, _bodyPart);
        Loft(new List<(Vector3, float, float)> { (V(-1, 0, 8.5f), 3.4f * g * u, 2.8f * u), (V(3, 0, 8.8f), 3.8f * g * u, 2.9f * u) }, Mat.Main, _bodyPart, 0, false);
        SetHead(V(5.8f, 0, 9), 3.2f, -12);
        foreach (int side in Sides)
            for (int k = 0; k < 4; k++)
            {
                var root = V(3 - k * 1.6f, side * 2.6f, 8.5f);
                Leg(new[] { root, root + V((1.5f - k) * 3.5f, side * 7.5f, 9.5f - k * 0.8f), root + V((1.5f - k) * 6.5f, side * 13.5f, -8.5f) }, 1.0f, k == 0 && Metal(side));
            }
        _tailRoot = V(-14.5f, 0, 12);
        _tailDir = new Vector3(-1, 0, 0.4f);
        _tailR = 1.3f * u;
        _wingRoot = V(-6, 0, 16);
    }

    // ---------------------------------------------------------------- aquatics

    /// <summary>A long fish, swimming in the air over the board.</summary>
    void Eel()
    {
        float hv = 4.5f, g = Girth, B = BodyMul, rMax = 4.3f * g * u;
        SerpentBody(new[] { V(-22 * B, 3, hv + 3.5f), V(-15 * B, -3, hv + 5.5f), V(-7 * B, 2.5f, hv + 4.5f), V(0, -1, hv + 6.5f), V(6, 0, hv + 7.2f) },
            t => Mathf.Lerp(0.9f * u, rMax, Mathf.SmoothStep(0, 1, Mathf.Min(1, t / 0.6f))));
        SetHead(V(9.8f, 0, hv + 7.6f), 4.8f, -2);
        int fin = Part(Zone.Tip);
        foreach (int side in Sides)
        {
            var p = V(6, side * 3.6f, hv + 5.5f);
            _c.Membrane(new[] { p, p + V(-5, side * 4, -1.5f), p + V(-3.5f, side * 1.5f, -3) }, _sailMat, fin);
        }
        _wingRoot = V(2, 0, hv + 10);
    }

    /// <summary>A manta, gliding: wide flat wings, a whip of a tail.</summary>
    void Ray()
    {
        float hv = 7.5f, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-8, 0, hv + 4), 2.6f * g * u, 1.8f * u),
            (V(-2, 0, hv + 4.5f), 5.4f * g * u, 2.8f * u),
            (V(5, 0, hv + 4.6f), 4.6f * g * u, 2.6f * u),
        }, Mat.Main, _bodyPart);
        int wing = Part(Zone.Body);
        foreach (int side in Sides)
            _c.Membrane(new[] { V(5, side * 2.5f, hv + 5), V(1, side * 13, hv + 7), V(-3, side * 21, hv + 10), V(-7, side * 14, hv + 5.5f), V(-8, side * 3, hv + 4.5f) }, Mat.Main, wing);
        SetHead(V(7.2f, 0, hv + 4.8f), 3.8f, 0);
        int horns = Part(Zone.Tip);
        foreach (int side in Sides)
            _c.Tube(Smooth(new[] { V(8.5f, side * 2.2f, hv + 4.5f), V(11.5f, side * 3, hv + 4.8f), V(11.8f, side * 2, hv + 6.2f) }, 4), 0.9f * u, 0.3f * u, Mat.Main, horns);
        var tail = Smooth(new[] { V(-8, 0, hv + 4), V(-15, 1, hv + 3.5f), V(-22, -1, hv + 5) }, 5);
        _c.Tube(tail, 0.9f * u, 0.25f * u, Mat.Main, Part(Zone.Tail));
        _bodyIsTail = true;
        _tailRoot = tail[^1];
        _tailDir = new Vector3(-1, 0, 0.3f);
        _tailR = 0.3f * u;
        _wingRoot = V(0, 0, hv + 7);
    }

    /// <summary>A sea-dragon: an upright S of a body, curled tail below, fronds streaming off its back.</summary>
    void Kelpie()
    {
        _upright = true;
        float hv = 2.5f, g = Girth;
        var pts = new[] { V(2.5f, 0, hv + 2.5f), V(-1.5f, 0, hv + 0.6f), V(-4, 0, hv + 3.5f), V(-3.2f, 0, hv + 9.5f), V(-0.8f, 0, hv + 15.5f), V(1.8f, 0, hv + 19.5f) };
        SerpentBody(pts, t => t < 0.35f ? Mathf.Lerp(0.5f, 2.2f, t / 0.35f) * u : t < 0.6f ? Mathf.Lerp(2.2f * u, 4.3f * g * u, (t - 0.35f) / 0.25f) : Mathf.Lerp(4.3f * g * u, 2.8f * g * u, (t - 0.6f) / 0.4f));
        SetHead(V(4.4f, 0, hv + 22.5f), 4.2f, -28);
        int fronds = Part(Zone.Tip);
        for (int k = 2; k < pts.Length; k++)
        {
            var p = pts[k] - X * 3 * u;
            var d = (-X * 0.8f + Z * 0.3f).Normalized();
            _c.Membrane(new[] { p, p + d * 3 * u + Z * 1.8f * u, p + d * 6 * u, p + d * 3 * u - Z * 1.8f * u }, Mat.Second, fronds, 0.05f);
        }
        _wingRoot = V(-2, 0, hv + 15);
    }

    // ---------------------------------------------------------------- blobs

    /// <summary>A spirit of light and smoke: a hovering teardrop crowned with a streaming flame.</summary>
    void Wisp()
    {
        _upright = true;
        float hv = 4, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-0.5f, 0, hv + 5), 4.2f * g * u, 4.0f * u),
            (V(0.5f, 0, hv + 10), 5.4f * g * u, 5.0f * u),
            (V(1.2f, 0, hv + 14), 4.4f * g * u, 4.0f * u),
        }, Mat.Main, _bodyPart);
        _c.Tube(Smooth(new[] { V(0.5f, 0, hv + 14), V(-3.5f, 0, hv + 19), V(-3, 0, hv + 24), V(-6.5f, 0, hv + 27) }, 5), t => (4.2f - 3.9f * t) * u * g, Mat.Main, _bodyPart);
        _c.Tube(Smooth(new[] { V(-0.5f, 0, hv + 5), V(-2.5f, 0, hv + 1.5f), V(-6, 0, hv) }, 5), 3.6f * g * u, 0.3f * u, Mat.Main, _bodyPart);
        SetHead(V(3.4f, 0, hv + 12.5f), 4.6f, -4);
        foreach (int side in Sides)
            Leg(new[] { V(1, side * 4.6f, hv + 10), V(3.2f, side * 6.6f, hv + 7), V(5.6f, side * 5.6f, hv + 6) }, 1.2f, Metal(side), hand: true);
        _c.Sphere(V(3.6f, -2.4f, hv + 7.5f), 1.4f * u, Mat.Glow, _bodyPart, 0.1f, 0, true);
        _tailRoot = V(-6, 0, hv);
        _tailDir = new Vector3(-1, 0, 0);
        _tailR = 1 * u;
        _wingRoot = V(0, 0, hv + 13);
    }

    /// <summary>A drifting bell with a face, trailing tendrils to the ground.</summary>
    void Jelly()
    {
        float g = Girth, bz = 23;
        _c.Ellipsoid(V(0, 0, bz), X, 7.5f * g * u, 7.5f * g * u, 6.2f * u, Mat.Main, _bodyPart, 0, (bz - 1.5f) * u);
        var rim = new List<Vector3>();
        for (int k = 0; k <= 24; k++) rim.Add(V(Mathf.Cos(k / 24f * Mathf.Tau) * 7.2f * g, Mathf.Sin(k / 24f * Mathf.Tau) * 7.2f * g, bz - 1.5f));
        _c.Tube(rim, 0.8f * u, 0.8f * u, Mat.Second, _bodyPart);
        for (int k = 0; k < 7; k++)
        {
            float a = k / 7f * Mathf.Tau;
            var b = V(Mathf.Cos(a) * 6.2f * g, Mathf.Sin(a) * 6.2f * g, bz - 1.8f);
            var pts = new[] { b, b + V(-1.5f + Mathf.Sin(k) * 1, Mathf.Cos(k * 1.7f) * 1.5f, -7), b + V(-3, Mathf.Cos(k) * 2, -14), b + V(-4, Mathf.Sin(k * 2.3f) * 2.5f, -bz + 3.5f) };
            _c.Tube(Smooth(pts, 4), 0.75f * u, 0.3f * u, k % 2 == 0 ? Mat.Membrane : Mat.Second, Part(Zone.Tail));
        }
        foreach (int side in Sides)
            _c.Tube(Smooth(new[] { V(1, side * 1.5f, bz - 2), V(0.5f, side * 2.5f, bz - 9), V(-1, side * 2, bz - 16) }, 4), 1.6f * u, 0.6f * u, Mat.Second, Part(Zone.Tail));
        _spine.Add((V(-3.5f, 0, bz), 6 * g * u, 5 * u));
        _spine.Add((V(0, 0, bz), 7 * g * u, 6 * u));
        _spine.Add((V(3.5f, 0, bz), 6 * g * u, 5 * u));
        SetHead(V(4.2f, 0, bz + 1.2f), 4.6f, -10);
        _tailRoot = V(-6, 0, bz - 1);
        _tailDir = new Vector3(-1, 0, -0.5f);
        _tailR = 1 * u;
        _wingRoot = V(0, 0, bz + 4);
    }

    /// <summary>A tall, melting thing: a column of ooze over its own puddle, dripping arms, a glowing core.</summary>
    void Ooze()
    {
        _upright = true;
        float g = Girth;
        _c.Ellipsoid(V(-1, 0, 0), X, 9.5f * g * u, 8.5f * g * u, 2.4f * u, Mat.Main, _bodyPart);
        Loft(new List<(Vector3, float, float)>
        {
            (V(-1.5f, 0, 3.5f), 6.4f * g * u, 6.0f * u),
            (V(-0.8f, 0, 8), 4.8f * g * u, 4.4f * u),
            (V(0.8f, 0, 13), 4.0f * g * u, 3.6f * u),
            (V(2.4f, 0, 17), 3.6f * g * u, 3.2f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(4.8f, 0, 20.5f), 5f, -12);
        Neck(V(2.4f, 0, 17), _headC - _headFwd * _headR * 0.55f, 3.2f, 2.8f, Vector3.Zero);
        foreach (int side in Sides)
        {
            var hand = V(4.6f, side * 6.4f, 6);
            Leg(new[] { V(1.4f, side * 4.2f, 14), V(3.2f, side * 6.8f, 10), hand }, 1.7f, Metal(side), hand: true);
            _c.Tube(new[] { hand - Z * u, hand - Z * 4 * u }, 0.8f * u, 0.4f * u, Mat.Main, _bodyPart);
        }
        _c.Sphere(V(1.2f, -3.6f, 9), 1.5f * u, Mat.Glow, _bodyPart, 0.1f, 0, true);
        _tailRoot = V(-4, 0, 4);
        _tailDir = new Vector3(-1, 0, 0.3f);
        _tailR = 1.6f * u;
        _wingRoot = V(0, 0, 15);
    }
}
