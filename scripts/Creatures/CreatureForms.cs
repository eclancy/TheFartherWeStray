using System.Collections.Generic;
using System.Linq;
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
    static readonly Quad HydraQ = new(10, 30, 5.8f, 5.6f, 5.2f, 5f, 6.6f, 7f, 1f, 15, 56, 2.8f, 4.5f, -18, 3.5f, 3.0f, 1.5f);

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
            case Form.Hydra: Hydra(); break;
            case Form.Gargoyle: Gargoyle(); break;
            case Form.Naga: Naga(); break;
            case Form.Scorpion: Scorpion(); break;
            case Form.Angler: Angler(); break;
            case Form.Watcher: Watcher(); break;
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

    /// <summary>
    /// A many-headed drake, low and heavy: three thick necks rise together from the chest in S-curves,
    /// every head facing forward, snarling, at a different height.
    /// </summary>
    void Hydra()
    {
        Quadruped(HydraQ);
        float lh = HydraQ.LegH * LegMul, chestX = HydraQ.Len * BodyMul * 0.4f;
        foreach (int side in Sides)
        {
            var nb = V(chestX + 1.5f, side * 2.6f, lh + HydraQ.ChestH * 0.95f + HydraQ.Hunch);
            // The far head rides higher, the near one lower, so all three show.
            var hc = _headC + V(-3, side * 6.5f, side > 0 ? 3 : -3.5f);
            var fwd = (X + Y * side * 0.15f - Z * 0.25f).Normalized();
            var neck = Smooth(new[] { nb, nb + V(1, side * 3, 4.5f), nb.Lerp(hc, 0.65f) + V(-1.6f, side * 1, 1.8f), hc - fwd * _headR * 0.55f }, 5);
            _c.Tube(neck, 3.0f * u, 2.2f * u, Mat.Main, _bodyPart);
            RisingNeck(neck[0], neck[^1], 3.0f * u);
            LesserHead(hc, _headR * 0.9f, fwd);
        }
    }

    /// <summary>A second head without a face of its own: skull, muzzle, jaw and two glowing eyes.</summary>
    void LesserHead(Vector3 c, float r, Vector3 fwd)
    {
        int part = Part(Zone.Head);
        var (hf, hs, hu) = PixelCanvas.Frame(fwd);
        _c.Ellipsoid(c, hf, 0.9f * r, 0.72f * r, 0.72f * r, Mat.Main, part);
        _c.Ellipsoid(c + (hf * 0.38f + hu * 0.4f) * r, hf, 0.42f * r, 0.75f * r, 0.24f * r, Mat.Main, part);
        _c.Ellipsoid(c + (hf * 0.95f - hu * 0.15f) * r, hf - hu * 0.15f, 0.6f * r, 0.38f * r, 0.34f * r, _sp.MuzzleSecond ? Mat.Second : Mat.Main, part);
        _c.Ellipsoid(c + (hf * 0.75f - hu * 0.42f) * r, hf - hu * 0.15f, 0.5f * r, 0.32f * r, 0.18f * r, _sp.MuzzleSecond ? Mat.Second : Mat.Main, part);
        foreach (int side in Sides)
        {
            _c.Sphere(c + (hf * 0.55f + hu * 0.25f + hs * side * 0.6f) * r, Mathf.Max(0.55f, 0.15f * r), Mat.Glow, part, 0.2f, 0, true);
            // If the species is horned, every head is: short spikes swept back.
            if (_p.Horns != Horns.None)
            {
                var hb = c + (hu * 0.6f + hs * side * 0.38f) * r;
                _c.Tube(new[] { hb, hb + (hu * 0.45f - hf * 0.7f + hs * side * 0.15f) * r }, 0.18f * r, 0.03f * r, Mat.Accent, Part(Zone.Tip));
            }
        }
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
        _hover = hv * u;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-1, 0, hv + 8), 3.2f * g * u, 2.8f * u),
            (V(0.5f, 0, hv + 12.5f), 3.6f * g * u, 3.0f * u),
            (V(2.2f, 0, hv + 16.5f), 4.8f * g * u, 3.6f * u),
            (V(4.2f, 0, hv + 19f), 4.0f * g * u, 3.0f * u),
        }, Mat.Main, _bodyPart);
        _c.Warp = Trail(V(-1, 0, hv + 8), 0.08f);
        _c.Tube(Smooth(new[] { V(-1, 0, hv + 8), V(-2.5f, 0, hv + 3.5f), V(-6, 0, hv + 0.5f), V(-11, 0, hv + 1.5f) }, 5), 3.0f * u, 0.3f * u, Mat.Main, _bodyPart);
        foreach (int side in Sides)
            _c.Tube(Smooth(new[] { V(-1, side * 1.5f, hv + 7), V(-3, side * 3, hv + 2.5f), V(-7.5f, side * 4.5f, hv + 0.5f), V(-10, side * 6, hv + 2.5f) }, 5), 1.9f * u, 0.25f * u, Mat.Main, _bodyPart);
        _c.Warp = BodyWarp;
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

    /// <summary>A gargoyle crouched on its haunches, knuckles on the stone, head jutting forward.</summary>
    void Gargoyle()
    {
        _upright = true;
        _wingSize = 1.2f;
        float g = Girth, k = LegMul;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-1.5f, 0, 7.5f * k), 4.5f * g * u, 3.8f * u),
            (V(0.5f, 0, 7.5f * k + 5), 4.6f * g * u, 3.6f * u),
            (V(2.3f, 0, 7.5f * k + 9.5f), 6.2f * g * u, 4.4f * u),
            (V(3.5f, 0, 7.5f * k + 12), 4.6f * g * u, 3.2f * u),
        }, Mat.Main, _bodyPart);
        float top = 7.5f * k + 12;
        SetHead(V(6.4f, 0, top + 3.2f), 4.8f, -16);
        Neck(V(3.6f, 0, top), _headC - _headFwd * _headR * 0.55f, 3.0f, 2.6f, V(0.5f, 0, 0.5f));
        foreach (int side in Sides)
        {
            float y = side * 3.6f * g;
            // Haunches: knees drawn up high beside the body, feet flat.
            Leg(new[] { V(-1.5f, y, 7.5f * k), V(3.5f, y * 1.3f, 9 * k), V(-2.5f, y * 1.2f, 2 * k), V(0.5f, y * 1.2f, 0) }, 2.7f);
            _c.Sphere(V(2.8f, side * 6.4f, top - 1), 2.6f * u * LimbThick, Mat.Main, _bodyPart);
            Leg(new[] { V(2.8f, side * 6.8f, top - 1), V(3.4f, side * 8.6f, top - 7.5f), V(6.6f, side * 7, 0.6f) }, 2.0f, Metal(side), hand: true);
        }
        _tailRoot = V(-3.5f, 0, 7.5f * k);
        _tailDir = new Vector3(-1, 0, -0.4f);
        _tailR = 2.2f * g * u;
        _wingRoot = V(0.5f, 0, top - 1);
    }

    // ---------------------------------------------------------------- serpents

    /// <summary>A body as one tube along points, its radius profile given, its points kept as the spine.</summary>
    void SerpentBody(Vector3[] pts, System.Func<float, float> radius)
    {
        _serpentFront = pts[^1];
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
        float hv = 6, g = Girth, B = BodyMul, rMax = 3.3f * g * u;
        _hover = hv * u;
        // Great arches through the air, the way it's painted on scrolls: the rise and fall reads from above.
        SerpentBody(new[] { V(-30 * B, 2.5f, hv + 1), V(-25 * B, -1, hv + 6), V(-19 * B, 1, hv + 12), V(-13 * B, -1.5f, hv + 8), V(-7 * B, 1.5f, hv + 2.5f), V(-1, -1, hv + 5), V(3.5f, 1, hv + 10), V(7.5f, 0, hv + 12) },
            t => t < 0.22f ? Mathf.Lerp(0.7f * u, rMax, Mathf.SmoothStep(0, 1, t / 0.22f)) : Mathf.Lerp(rMax, rMax * 0.88f, (t - 0.22f) / 0.78f));
        SetHead(V(10.8f, 0, hv + 12.5f), 5.2f, -8);
        // A soft mane down the whole spine, so the long body reads as one line.
        int mane = Part(Zone.Tip);
        for (int k = 0; k < 14; k++)
        {
            float t = Mathf.Lerp(0.12f, 0.96f, k / 13f);
            var (top, _, _, along) = SpineAt(t);
            _c.Tube(new[] { top - Z * 0.4f * u, top + Z * (1.6f + 0.8f * (k % 2)) * u - along * 1.8f * u }, 1.0f * u, 0.2f * u, _tuftMat, mane);
        }
        // Four small clawed legs, held up under the body as it swims.
        foreach (float t in new[] { 0.48f, 0.84f })
        {
            var (top, w, h, along) = SpineAt(t);
            var hip = top - Z * h * 1.4f;
            foreach (int side in Sides)
                Leg(new[] { hip + Y * side * w * 0.6f, hip + Y * side * (w + 2.5f * u) - Z * 2 * u + along * u, hip + Y * side * (w + 2.2f * u) - Z * 4 * u + along * 2.5f * u }, 0.9f, false, hand: true);
        }
        int whisker = Part(Zone.Tip);
        foreach (int side in Sides)
        {
            var b = H(1.1f, side * 0.35f, -0.25f);
            _c.Tube(Smooth(new[] { b, b + V(-2, side * 3, -1), b + V(-7, side * 5, 0.5f), b + V(-12, side * 4, -1.5f) }, 4), 0.5f * u, 0.2f * u, Mat.Accent, whisker);
        }
        _wingRoot = V(2, 0, hv + 8);
    }

    /// <summary>A naga: a serpent's coils on the ground, rising into a torso with arms and a head.</summary>
    void Naga()
    {
        float g = Girth, B = BodyMul, rMax = 4.2f * g * u;
        SerpentBody(new[] { V(-14 * B, 4, 1.2f), V(-9 * B, -4, 2.4f), V(-2, -5.5f, 3.6f), V(3, -0.5f, 4.2f), V(0, 4.5f, 4.4f), V(-3, 1, 6), V(-1.5f, 0, 11), V(0, 0, 16) },
            t => t < 0.4f ? Mathf.Lerp(0.6f * u, rMax, Mathf.SmoothStep(0, 1, t / 0.4f)) : Mathf.Lerp(rMax, rMax * 0.82f, (t - 0.4f) / 0.6f));
        _c.Warp = BodyWarp;
        Loft(new List<(Vector3, float, float)>
        {
            (V(0, 0, 16), 3.6f * g * u, 3.0f * u),
            (V(1, 0, 20), 5.4f * g * u, 3.6f * u),
            (V(1.6f, 0, 23), 4.4f * g * u, 3.0f * u),
        }, Mat.Main, _bodyPart, 0, false);
        SetHead(V(3.6f, 0, 27), 4.6f, -8);
        Neck(V(1.8f, 0, 23.5f), _headC - _headFwd * _headR * 0.55f, 2.6f, 2.3f, Vector3.Zero);
        foreach (int side in Sides)
        {
            _c.Sphere(V(1.4f, side * 5.4f, 22.5f), 2.1f * u * LimbThick, Mat.Main, _bodyPart);
            Leg(new[] { V(1.4f, side * 5.8f, 22.5f), V(3, side * 7.6f, 17), V(6.2f, side * 6.4f, 15.5f) }, 1.6f, Metal(side), hand: true);
        }
        _wingRoot = V(0, 0, 22);
    }

    // ---------------------------------------------------------------- avians

    /// <summary>An upright bird of prey on short legs.</summary>
    void Hawk()
    {
        _upright = true;
        _wingSize = 1.15f;
        float lh = 8 * LegMul, g = Girth;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-4.6f, 0, lh + 3.5f), 3.9f * g * u, 3.4f * u),
            (V(-0.6f, 0, lh + 9), 5.4f * g * u, 5.2f * u),
            (V(2.7f, 0, lh + 14), 4.8f * g * u, 4.5f * u),
            (V(3.6f, 0, lh + 17), 3.6f * g * u, 3.4f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(5.8f, 0, lh + 20.5f), 5.1f, -8);
        Neck(V(3.6f, 0, lh + 16.5f), _headC - _headFwd * _headR * 0.55f, 3.1f, 2.8f, Vector3.Zero);
        foreach (int side in Sides)
        {
            float y = side * 2.7f * g;
            // Feathered thighs over scaled shanks.
            _c.Ellipsoid(V(0.2f, y, lh + 4.5f), new Vector3(0.3f, 0, -1), 3.2f * u, 2.3f * u, 2.3f * u, Mat.Main, _bodyPart);
            Leg(new[] { V(0.5f, y, lh + 3), V(1.6f, y, lh * 0.45f), V(2, y, 0) }, 1.4f, Metal(side), hand: true);
        }
        _tailRoot = V(-5.2f, 0, lh + 3.2f);
        _tailDir = new Vector3(-0.7f, 0, -0.7f);
        _tailR = 2.4f * u;
        _wingRoot = V(2.0f, 0, lh + 15);
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
        _c.Warp = Trail(V(-4, 0, lh + 4), 0.05f);
        for (int k = -1; k <= 1; k++)
        {
            var pts = Smooth(new[] { V(-4, k * 0.8f, lh + 4), V(-10, k * 3, lh + 1.5f), V(-17, k * 5, 1.5f), V(-22, k * 6.5f, 3.5f) }, 5);
            _c.Tube(pts, 1.2f * u, 0.6f * u, _featherMat, plume);
            _c.Sphere(pts[^1], 1.4f * u, Mat.Glow, plume, 0.1f, 0, true);
        }
        _c.Warp = BodyWarp;
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

    /// <summary>A scorpion: flat and wide, pincers forward, the stinger curled high over its back.</summary>
    void Scorpion()
    {
        float g = Girth, B = BodyMul;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-9 * B, 0, 6), 4.0f * g * u, 2.4f * u),
            (V(-3, 0, 5.8f), 5.2f * g * u, 2.8f * u),
            (V(2, 0, 5.5f), 4.6f * g * u, 2.6f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(5, 0, 6), 2.9f, -5);
        // The tail: segments rising up and over, ending in a venom bulb and a sting.
        int tailPart = Part(Zone.Tail);
        var tail = Smooth(new[] { V(-10 * B, 0, 6.5f), V(-14 * B, 0, 10), V(-14.5f * B, 0, 16), V(-11 * B, 0, 20), V(-7 * B, 0, 20.5f), V(-5 * B, 0, 18.5f) }, 5);
        _c.Tube(tail, 2.0f * u, 1.3f * u, Mat.Main, tailPart);
        var bulb = tail[^1];
        _c.Sphere(bulb, 1.7f * u, Mat.Glow, tailPart, 0.05f, 0, true);
        _c.Tube(new[] { bulb + V(0.6f, 0, -0.6f), bulb + V(1.6f, 0, -3.2f) }, 0.9f * u, 0.1f * u, Mat.Accent, Part(Zone.Tip));
        foreach (int side in Sides)
        {
            // Pincers.
            int claw = Part(Zone.Limb);
            var arm = new[] { V(3.5f, side * 3, 5), V(7, side * 6.5f, 6.2f), V(10, side * 5.5f, 6.2f) };
            _c.Tube(Smooth(arm, 4), 1.4f * u * LimbThick, 1.1f * u, Metal(side) ? Mat.Metal : Mat.Main, claw);
            var hand = arm[^1];
            _c.Ellipsoid(hand, X + Y * side * 0.2f, 2.4f * u, 1.6f * u, 1.3f * u, Mat.Main, claw);
            int jaws = Part(Zone.Tip);
            foreach (float s2 in new[] { -0.6f, 0.6f })
                _c.Tube(Smooth(new[] { hand + X * 1.5f * u + Y * s2 * u, hand + X * 3.5f * u + Y * s2 * 1.4f * u, hand + X * 5 * u + Y * s2 * 0.3f * u }, 3), 0.8f * u, 0.15f * u, Mat.Accent, jaws);
            for (int k = 0; k < 4; k++)
            {
                var root = V(1 - k * 2.4f, side * 4, 5.5f);
                Leg(new[] { root, root + V(0.5f - k * 0.8f, side * 5, 3.5f), root + V(1.5f - k * 2, side * 8.5f, -5.5f) }, 0.85f);
            }
        }
        _tailRoot = bulb;
        _tailDir = new Vector3(1, 0, -0.3f);
        _tailR = 1.2f * u;
        _bodyIsTail = true;
        _wingRoot = V(-2, 0, 9);
    }

    // ---------------------------------------------------------------- aquatics

    /// <summary>A long fish, swimming in the air over the board.</summary>
    void Eel()
    {
        float hv = 4.5f, g = Girth, B = BodyMul, rMax = 4.3f * g * u;
        _hover = hv * u;
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
        float hv = 8f, g = Girth;
        _hover = hv * u;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-9, 0, hv + 4), 2.8f * g * u, 2.0f * u),
            (V(-2, 0, hv + 4.8f), 5.8f * g * u, 3.1f * u),
            (V(5.5f, 0, hv + 4.8f), 5.0f * g * u, 2.8f * u),
        }, Mat.Main, _bodyPart);
        int wing = Part(Zone.Body);
        // The wings ripple: tips rise and fall a beat behind the body.
        _c.Warp = p => BodyWarp(p) + Z * Wave(0.15f) * Mathf.Abs(p.Y) * 0.13f;
        foreach (int side in Sides)
        {
            var front = V(5.5f, side * 2.8f, hv + 5.4f);
            var tip = V(-3.5f, side * 23, hv + 11);
            _c.Membrane(new[] { front, V(1, side * 14, hv + 7.5f), tip, V(-7.5f, side * 15, hv + 6), V(-9, side * 3, hv + 4.6f) }, Mat.Main, wing);
            // A pale leading edge and a line of glowing spots, so the wings read from above.
            _c.Tube(Smooth(new[] { front, V(1, side * 14, hv + 7.5f), tip }, 4), 1.0f * u, 0.35f * u, Mat.Second, wing, 0.05f);
            for (int k = 0; k < 3; k++)
                _c.Sphere(V(-1.5f - k * 1.2f, side * (7.5f + 3.8f * k), hv + 6.6f + k * 1.2f), (0.95f - 0.15f * k) * u, Mat.Glow, Part(Zone.Aura), 0.1f, 0, true);
        }
        _c.Warp = BodyWarp;
        SetHead(V(7.2f, 0, hv + 4.8f), 3.8f, 0);
        int horns = Part(Zone.Tip);
        foreach (int side in Sides)
            _c.Tube(Smooth(new[] { V(8.5f, side * 2.2f, hv + 4.5f), V(11.5f, side * 3, hv + 4.8f), V(11.8f, side * 2, hv + 6.2f) }, 4), 0.9f * u, 0.3f * u, Mat.Main, horns);
        var tail = Smooth(new[] { V(-8, 0, hv + 4), V(-15, 1, hv + 3.5f), V(-22, -1, hv + 5) }, 5);
        _c.Warp = Trail(V(-8, 0, hv + 4), 0.07f);
        _c.Tube(tail, 0.9f * u, 0.25f * u, Mat.Main, Part(Zone.Tail));
        _c.Warp = BodyWarp;
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
        _hover = hv * u;
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

    /// <summary>An angler from the deep: a great head and underbite, a tapering body, a glowing lure on a stalk.</summary>
    void Angler()
    {
        float hv = 5, g = Girth, B = BodyMul;
        _hover = hv * u;
        SerpentBody(new[] { V(-17 * B, 1.5f, hv + 5), V(-11 * B, -1, hv + 5.5f), V(-5, 0.5f, hv + 6), V(0, 0, hv + 6.5f) },
            t => Mathf.Lerp(0.8f * u, 5.0f * g * u, Mathf.SmoothStep(0, 1, t)));
        SetHead(V(4.8f, 0, hv + 7), 6.0f, -4);
        var (hf, hs, hu) = HeadFrame;
        // The underbite, lined with needle teeth.
        int jaw = Part(Zone.Head);
        _c.Ellipsoid(H(0.55f, 0, -0.55f), hf + hu * 0.2f, 0.85f * _headR, 0.8f * _headR, 0.32f * _headR, Mat.Main, jaw);
        int teeth = Part(Zone.Tip);
        for (int k = -2; k <= 2; k++)
        {
            var b = H(1.05f - Mathf.Abs(k) * 0.12f, k * 0.28f, -0.4f);
            _c.Tube(new[] { b, b + hu * _headR * 0.38f + hf * _headR * 0.05f }, 0.3f * u, 0.08f * u, Mat.EyeWhite, teeth);
        }
        // The lure: a stalk arching out over the mouth, ending in a light.
        int lure = Part(Zone.Tip);
        var s0 = H(0.2f, 0, 0.85f);
        var tip = H(1.6f, 0, 1.0f);
        _c.Tube(Smooth(new[] { s0, H(0.6f, 0, 1.7f), H(1.3f, 0, 1.75f), tip }, 4), 0.5f * u, 0.35f * u, Mat.Main, lure);
        _c.Sphere(tip - hu * 0.3f * u, 1.4f * u, Mat.Glow, lure, 0.15f, 0, true);
        int fins = Part(Zone.Tip);
        foreach (int side in Sides)
        {
            var p = V(-1, side * 4.4f, hv + 5);
            _c.Membrane(new[] { p, p + V(-4, side * 4, -2), p + V(-2, side * 1.5f, -3.5f) }, _sailMat, fins);
        }
        _wingRoot = V(-2, 0, hv + 10);
    }

    /// <summary>A floating eye: one great lidded eye in an orb, a mouth beneath, tendrils hanging.</summary>
    void Watcher()
    {
        float g = Girth, oz = 19;
        _hover = 1;
        _faceOnBody = true;
        _bigEye = true;
        float r = 6.6f * g;
        _c.Sphere(V(0, 0, oz), r * u, Mat.Main, _bodyPart);
        // The eye: sclera, a glowing iris, a slit pupil, under a heavy lid.
        int eye = Part(Zone.Head);
        var front = (X - Y * 0.35f + Z * 0.12f).Normalized();
        var ec = V(0, 0, oz) + front * (r - 0.7f) * u;
        _c.Ellipsoid(ec, front, 1.8f * u, 4.6f * u, 4.3f * u, Mat.EyeWhite, eye);
        _c.Ellipsoid(ec + front * 1.1f * u, front, 1.0f * u, 2.9f * u, 2.9f * u, Mat.Glow, eye, 0.05f);
        _c.Ellipsoid(ec + front * 1.8f * u, front, 0.6f * u, 0.8f * u, 2.3f * u, Mat.Pupil, eye);
        var lid = new List<Vector3>();
        for (int k = 0; k <= 10; k++)
        {
            float a = Mathf.Lerp(-1.3f, 1.3f, k / 10f);
            lid.Add(V(0, 0, oz) + (front * Mathf.Cos(a) * 0.75f + Z * 0.55f + front.Cross(Z) * Mathf.Sin(a) * 0.62f).Normalized() * r * u);
        }
        _c.Tube(lid, 1.1f * u, 1.1f * u, Mat.Main, _bodyPart, -0.1f);
        _c.Warp = Trail(V(0, 0, oz), 0.07f);
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * Mathf.Tau + 0.3f;
            var b = V(Mathf.Cos(a) * 4 * g, Mathf.Sin(a) * 4 * g, oz - 4.5f);
            var pts = Smooth(new[] { b, b + V(-1, Mathf.Cos(k * 1.3f), -5), b + V(-2.5f, Mathf.Sin(k) * 2, -10), b + V(-3, Mathf.Cos(k * 2.1f) * 2, -oz + 5.5f) }, 4);
            int tendril = Part(Zone.Tail);
            _c.Tube(pts, 0.9f * u, 0.3f * u, Mat.Main, tendril);
            _c.Sphere(pts[^1], 0.6f * u, Mat.Glow, tendril, 0.1f, 0, true);
        }
        _c.Warp = BodyWarp;
        _spine.Add((V(-3, 0, oz), r * 0.85f * u, r * 0.85f * u));
        _spine.Add((V(0, 0, oz), r * u, r * u));
        _spine.Add((V(3, 0, oz), r * 0.85f * u, r * 0.85f * u));
        SetHead(V(0.5f, 0, oz - 1), 7.4f, -20);
        _tailRoot = V(-5, 0, oz - 2);
        _tailDir = new Vector3(-1, 0, -0.4f);
        _tailR = 1 * u;
        _wingRoot = V(0, 0, oz + 3);
    }

    // ---------------------------------------------------------------- blobs

    /// <summary>A spirit of light and smoke: a hovering teardrop crowned with a streaming flame.</summary>
    void Wisp()
    {
        _upright = true;
        float hv = 4, g = Girth;
        _hover = hv * u;
        Loft(new List<(Vector3, float, float)>
        {
            (V(-0.5f, 0, hv + 5), 4.2f * g * u, 4.0f * u),
            (V(0.5f, 0, hv + 10), 5.4f * g * u, 5.0f * u),
            (V(1.2f, 0, hv + 14), 4.4f * g * u, 4.0f * u),
        }, Mat.Main, _bodyPart);
        // The crown burns: the body's colour at the root, light from halfway up.
        _c.Warp = Trail(V(0.5f, 0, hv + 14), 0.1f);
        var crown = Smooth(new[] { V(0.5f, 0, hv + 14), V(-3.5f, 0, hv + 19), V(-3, 0, hv + 24), V(-6.5f, 0, hv + 27.5f) }, 6);
        _c.Tube(crown.Take(crown.Count / 2 + 1).ToList(), t => (4.2f - 1.6f * t) * u * g, Mat.Main, _bodyPart);
        _c.Tube(crown.Skip(crown.Count / 2 - 1).ToList(), t => (2.7f - 2.4f * t) * u * g, Mat.Glow, Part(Zone.Aura), 0.1f, 0, true);
        _c.Warp = Trail(V(-0.5f, 0, hv + 5), 0.1f);
        var wisp = Smooth(new[] { V(-0.5f, 0, hv + 5), V(-2.5f, 0, hv + 1.5f), V(-6.5f, 0, hv) }, 6);
        _c.Tube(wisp, t => (3.6f - 3.0f * t) * g * u, Mat.Main, _bodyPart);
        _c.Sphere(wisp[^1], 0.9f * u, Mat.Glow, Part(Zone.Aura), 0.1f, 0, true);
        _c.Warp = BodyWarp;
        SetHead(V(3.4f, 0, hv + 12.5f), 4.6f, -4);
        foreach (int side in Sides)
            Leg(new[] { V(0.8f, side * 4.8f, hv + 10.5f), V(2.6f, side * 7.6f, hv + 7.5f), V(5.6f, side * 7.0f, hv + 6) }, 0.95f, Metal(side), hand: true);
        _c.Sphere(V(3.6f, -2.2f, hv + 7.6f), 2.0f * u, Mat.Glow, _bodyPart, 0.15f, 0, true);
        _tailRoot = V(-6, 0, hv);
        _tailDir = new Vector3(-1, 0, 0);
        _tailR = 1 * u;
        _wingRoot = V(0, 0, hv + 13);
    }

    /// <summary>A drifting bell with a face, trailing tendrils to the ground.</summary>
    void Jelly()
    {
        float g = Girth, bz = 25;
        _hover = 1;
        _faceOnBody = true;
        _c.Ellipsoid(V(0, 0, bz), X, 8.2f * g * u, 8.2f * g * u, 7.0f * u, Mat.Main, _bodyPart, 0, (bz - 1.5f) * u);
        var rim = new List<Vector3>();
        for (int k = 0; k <= 24; k++) rim.Add(V(Mathf.Cos(k / 24f * Mathf.Tau) * 7.9f * g, Mathf.Sin(k / 24f * Mathf.Tau) * 7.9f * g, bz - 1.5f));
        _c.Tube(rim, 0.8f * u, 0.8f * u, Mat.Second, _bodyPart);
        _c.Warp = Trail(V(0, 0, bz), 0.06f);
        for (int k = 0; k < 7; k++)
        {
            float a = k / 7f * Mathf.Tau;
            var b = V(Mathf.Cos(a) * 6.8f * g, Mathf.Sin(a) * 6.8f * g, bz - 1.8f);
            var pts = Smooth(new[] { b, b + V(-1.5f + Mathf.Sin(k) * 1, Mathf.Cos(k * 1.7f) * 1.5f, -7), b + V(-3, Mathf.Cos(k) * 2, -14), b + V(-4, Mathf.Sin(k * 2.3f) * 2.5f, -bz + 3.5f) }, 4);
            int tendril = Part(Zone.Tail);
            _c.Tube(pts, 0.8f * u, 0.3f * u, k % 2 == 0 ? Mat.Membrane : Mat.Second, tendril);
            _c.Sphere(pts[^1], 0.7f * u, Mat.Glow, tendril, 0.1f, 0, true);
        }
        foreach (int side in Sides)
            _c.Tube(Smooth(new[] { V(1, side * 1.5f, bz - 2), V(0.5f, side * 2.5f, bz - 9), V(-1, side * 2, bz - 16) }, 4), 1.6f * u, 0.6f * u, Mat.Second, Part(Zone.Tail));
        _c.Warp = BodyWarp;
        _spine.Add((V(-3.5f, 0, bz), 6.5f * g * u, 6 * u));
        _spine.Add((V(0, 0, bz), 7.5f * g * u, 6.8f * u));
        _spine.Add((V(3.5f, 0, bz), 6.5f * g * u, 6 * u));
        SetHead(V(0.5f, 0, bz + 0.5f), 7.6f, -12);
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
        _c.Ellipsoid(V(-1, 0, 0), X, 10.5f * g * u, 9.5f * g * u, 1.8f * u, Mat.Main, _bodyPart);
        Loft(new List<(Vector3, float, float)>
        {
            (V(-1.5f, 0, 3), 6.2f * g * u, 5.8f * u),
            (V(-0.8f, 0, 9), 4.6f * g * u, 4.3f * u),
            (V(0.8f, 0, 15), 4.2f * g * u, 3.8f * u),
            (V(2.6f, 0, 20.5f), 3.8f * g * u, 3.4f * u),
        }, Mat.Main, _bodyPart);
        SetHead(V(5.2f, 0, 24.5f), 5.6f, -14);
        Neck(V(2.6f, 0, 20.5f), _headC - _headFwd * _headR * 0.55f, 3.3f, 3.0f, Vector3.Zero);
        foreach (int side in Sides)
        {
            // Long arms melting off the shoulders, a drip hanging from each hand.
            var hand = V(5.2f, side * 7.2f, 7);
            Leg(new[] { V(1.8f, side * 4.4f, 18), V(3.4f, side * 7.6f, 13), hand }, 1.5f, Metal(side), hand: true);
            _c.Tube(new[] { hand - Z * u, hand - Z * 3.5f * u }, 0.7f * u, 0.45f * u, Mat.Main, _bodyPart);
        }
        _c.Sphere(V(2.0f, -3.2f, 12), 2.0f * u, Mat.Glow, _bodyPart, 0.15f, 0, true);
        _tailRoot = V(-4, 0, 4);
        _tailDir = new Vector3(-1, 0, 0.3f);
        _tailR = 1.6f * u;
        _wingRoot = V(0, 0, 15);
    }
}
