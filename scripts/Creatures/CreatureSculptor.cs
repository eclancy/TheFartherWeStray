using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheFartherWeStray.Creatures;

/// <summary>
/// Draws a creature the way FFTA shows its units: from above and to the side, facing a screen
/// diagonal. The creature is a small 3D model (ellipsoids, tubes, membranes) ray-cast into pixels,
/// then shaded, outlined and rim-lit by <see cref="PixelCanvas.Resolve"/> like before.
///
/// Its <see cref="Form"/> (a lean hound, a long-legged stag, a coiled wyrm, a crane, a wisp) builds
/// the body and leaves anchors: head, spine, tail root, wing root, feet. Every part slot attaches at
/// those anchors, so any part fits any form. The element adds themed touches (flames, frost, fronds,
/// fins), the strain its grafts, the aura a mystical flourish. Faces, patterns and surface grafts go
/// on last, onto whatever is visible from this side.
/// </summary>
public sealed partial class CreatureSculptor
{
    enum Zone { Body, Head, Tail, Tip, Limb, Ear, Wing, Aura }

    readonly Creature _cr;
    readonly Species _sp;
    readonly PartSet _p;
    readonly PixelCanvas _c;
    readonly Form _form;
    readonly Aura _aura;
    readonly float S, u;            // sprite size, and its scale against a medium creature
    readonly SeededRng _rng;        // shape choices: the same for every sibling and both views
    readonly Dictionary<int, Zone> _zones = new();
    readonly Mat _spikeMat, _shellMat, _featherMat, _sailMat, _tuftMat, _crestMat;

    // Anchors the form leaves for the shared parts.
    Vector3 _headC, _headFwd = new(1, 0, 0);
    float _headR;
    readonly List<(Vector3 p, float w, float h)> _spine = new();   // the torso, rear to front
    Vector3 _tailRoot, _tailDir = new(-1, 0, 0);
    float _tailR;
    bool _bodyIsTail;                // serpents and fish: the body already is the tail
    Vector3 _wingRoot;
    float _wingSize = 1;
    bool _upright;                   // the torso stands up (bellies face forward)
    readonly List<(Vector3 p, Vector3 fwd, float r, bool hand)> _feet = new();
    int _bodyPart, _headPart;
    (Vector3 front, Vector3 back)? _mouth;
    Vector3 _tailEnd;                // the far end of whatever tail was drawn
    readonly List<(Vector3 root, float r)> _necks = new(); // where each rising neck leaves the body, and how thick
    float _hover;                    // floaters: how high it hangs (they bob instead of breathing)
    bool _faceOnBody;                // no head: the face is on the body (jellies)
    bool _bigEye;                    // one great eye is the face: no small eyes painted on
    bool _sparse;                    // a lone shape (the watcher): no ears, back plates or tail to clutter it
    Vector3? _serpentFront;          // bodies that are all tail: where the wave down them starts

    // The animation: which pose, and how far through it (0 to 1); a negative phase is a still pose.
    readonly Pose _pose;
    readonly float _phase;
    readonly bool _blink;
    bool _tailSway;
    Vector3 _tailAxis, _tailSide;

    static readonly Vector3 X = new(1, 0, 0), Y = new(0, 1, 0), Z = new(0, 0, 1);

    /// <summary>The sprite for one view, and where the creature's ground point (its feet) lands in it.</summary>
    public static (Image img, Vector2I anchor) Render(Creature creature, bool fourColor, View view, float phase = -1, bool blink = false, Pose pose = Pose.Idle)
    {
        var s = new CreatureSculptor(creature, view, phase, blink, pose);
        s.Build();
        var img = s._c.Resolve(creature.Palette, fourColor);
        var used = img.GetUsedRect();
        var origin = s._c.Cam.Origin;
        if (used.Size.X == 0) return (img, new Vector2I(Mathf.RoundToInt(origin.X), Mathf.RoundToInt(origin.Y)));
        return (img.GetRegion(used), new Vector2I(Mathf.RoundToInt(origin.X) - used.Position.X, Mathf.RoundToInt(origin.Y) - used.Position.Y));
    }

    CreatureSculptor(Creature cr, View view, float phase, bool blink, Pose pose)
    {
        _phase = phase;
        _pose = pose;
        // Eyes shut when hurt, and once a faint takes hold.
        _blink = blink || phase >= 0 && (pose == Pose.Hurt && phase < 0.6f || pose == Pose.Faint && phase > 0.3f);
        _cr = cr;
        _sp = cr.Species;
        _p = cr.Parts;
        _form = Species.FormOf(_sp);
        _aura = Species.AuraOf(_sp);
        S = Mathf.Round(SizeInfo.Pixels(cr.Size) * ArtStyle.Current.SizeScale);
        u = S / 56f * 1.1f;
        int w = Mathf.CeilToInt(S * 2.5f), h = Mathf.CeilToInt(S * 2.5f);
        _c = new PixelCanvas(w, h);
        _c.Begin3D(new Camera3(view, new Vector2(w / 2f, h * 0.7f)));
        _rng = new SeededRng(SeededRng.Derive(_sp.Seed, 0x3D3D));
        (_spikeMat, _shellMat, _featherMat, _sailMat, _tuftMat, _crestMat) = CreaturePainter.StyleOf(_sp);
    }

    void Build()
    {
        _c.Warp = BodyWarp;
        BuildForm();
        // From here on, anything behind the tail root sways with the tail.
        _tailAxis = _tailDir.Normalized();
        _tailSide = Z.Cross(_tailAxis).Normalized();
        _tailSway = !_bodyIsTail && !_sparse && _p.Tail != Tail.None;
        _c.Warp = BodyWarp;
        Head();
        if (!_sparse) EarsPart();
        HornsPart();
        if (!_sparse) BackPart();
        if (_sparse) _tailEnd = _tailRoot; else TailPart();
        _c.Warp = WingWarp;
        WingsPart();
        _c.Warp = BodyWarp;
        Grafts();
        Motif();
        _c.Warp = AuraWarp;
        AuraPart();
        _c.Warp = BodyWarp;
        ApplyPattern();
        Face();
        SurfaceGrafts();
    }

    // ---------------------------------------------------------------- idle motion

    bool Animate => _phase >= 0;
    float Wave(float lag = 0, float speed = 1) => Animate ? Mathf.Sin(Mathf.Tau * (_phase * speed - lag)) : 0;
    /// <summary>0 at rest, 1 at the top of the breath.</summary>
    float Rise => Animate ? 0.5f - 0.5f * Mathf.Cos(Mathf.Tau * _phase) : 0;

    /// <summary>
    /// The body's idle: it lifts a pixel on its legs while the feet stay planted (floaters drift
    /// instead), the head nods a beat behind, a tail sways in a wave running to its tip, and a
    /// serpent's whole length ripples.
    /// </summary>
    Vector3 BodyWarp(Vector3 p)
    {
        if (!Animate) return p;
        float lift = _hover > 0 ? 1 : Mathf.Clamp((p.Z - 2 * u) / (9 * u), 0, 1);
        float head = _headR > 0 ? Mathf.Clamp(1 - ((p - _headC).Length() - _headR * 1.1f) / _headR, 0, 1) : 0;
        float along = 0, tail = 0;
        if (_tailSway)
        {
            var rel = p - _tailRoot;
            along = rel.Dot(_tailAxis);
            if (along > 0) tail = Mathf.Clamp(1 - ((rel - _tailAxis * along).Length() - 7 * u) / (5 * u), 0, 1);
            else along = 0;
        }
        float serpent = 0, sd = 0;
        if (_serpentFront is { } front)
        {
            sd = (p - front).Length();
            serpent = Mathf.Clamp((sd - 3 * u) / (10 * u), 0, 1);
        }

        var o = p;
        switch (_pose)
        {
            case Pose.Idle:
                o += Z * (_hover > 0 ? 1.6f : 1.0f) * u * Rise * lift;
                o += Z * 0.7f * u * Wave(0.2f) * head;
                o += _tailSide * Wave(0.1f + along / (28 * u)) * along * 0.075f * tail;
                o += Y * 1.2f * u * Wave(sd / (24 * u)) * serpent;
                break;
            case Pose.Walk:
            {
                // Two steps a cycle: the body rides up on each, the head bobs, the tail swings.
                float bob = 0.5f - 0.5f * Mathf.Cos(Mathf.Tau * 2 * _phase);
                o += Z * 1.0f * u * bob * lift + X * 0.5f * u * bob * lift;
                o += Z * 0.6f * u * Wave(0.15f, 2) * head;
                o += _tailSide * Wave(0.2f + along / (24 * u)) * along * 0.09f * tail;
                o += Y * 2.0f * u * Wave(sd / (18 * u), 2) * serpent;
                break;
            }
            case Pose.Strike:
            {
                // Wind up, lunge, recover: the body draws back, then throws itself and its head forward.
                float t = _phase;
                float f = t < 0.3f ? -t / 0.3f : t < 0.55f ? Mathf.Lerp(-1, 1, (t - 0.3f) / 0.25f) : Mathf.Lerp(1, 0, (t - 0.55f) / 0.45f);
                o += X * f * 2.4f * u * lift + Z * -Mathf.Max(f, 0) * 0.8f * u * lift;
                o += (X * f * 1.8f * u - Z * Mathf.Max(f, 0) * 1.2f * u + Z * Mathf.Max(-f, 0) * 1.0f * u) * head;
                o += _tailSide * f * along * 0.12f * tail + Z * Mathf.Max(-f, 0) * along * 0.06f * tail;
                o += Y * 1.6f * u * f * Mathf.Sin(sd / (6 * u)) * serpent;
                break;
            }
            case Pose.Cast:
            {
                // Rear up and lift the head: the gathering before a skill goes off.
                float c = Mathf.Sin(Mathf.Pi * Mathf.Clamp(_phase * 1.15f, 0, 1));
                o += Z * c * 2.2f * u * lift - X * c * 1.0f * u * lift;
                o += (Z * c * 1.6f * u - X * c * 0.6f * u) * head;
                o += Z * c * along * 0.12f * tail;
                o += Z * c * 1.2f * u * Mathf.Sin(sd / (7 * u)) * serpent;
                break;
            }
            case Pose.Hurt:
            {
                // Knocked back: the body flinches away and the head snaps up.
                float h = _phase < 0.25f ? _phase / 0.25f : 1 - (_phase - 0.25f) / 0.75f;
                o += (-X * 2.2f * u - Z * 0.7f * u) * h * lift;
                o += (-X * 1.2f * u + Z * 1.0f * u) * h * head;
                o += _tailSide * h * along * 0.06f * tail;
                break;
            }
            case Pose.Faint:
            {
                // It folds onto the ground and rolls onto its side; floaters drop out of the air.
                float fall = Mathf.SmoothStep(0, 1, _phase);
                if (_hover > 0) o.Z -= Mathf.Min(_hover, p.Z) * fall * 0.9f;
                float z = o.Z;
                o.Z = z * (1 - 0.5f * fall);
                o.Y += z * 0.4f * fall;
                o += -Z * 1.6f * u * fall * head;
                break;
            }
        }
        return o;
    }

    /// <summary>
    /// A leg's own motion on top of the body's: when walking, diagonal pairs (or alternate sides,
    /// on two legs) lift and swing forward in turn, the foot moving most and the hip not at all.
    /// Arms swing against the legs.
    /// </summary>
    Func<Vector3, Vector3> LegWarp(Vector3 hip, Vector3 foot, bool hand) => p =>
    {
        var b = BodyWarp(p);
        if (!Animate || _pose != Pose.Walk) return b;
        float span = Mathf.Max(0.5f, hip.Z - foot.Z);
        float w = Mathf.Clamp((hip.Z - p.Z) / span, 0, 1);
        float offset = (foot.Y > 0) ^ (foot.X > 0) ? 0 : 0.5f;
        float ph = Mathf.Tau * (_phase + offset);
        if (hand) return b + X * -Mathf.Cos(ph) * 1.6f * u * w;
        return b + Z * Mathf.Max(0, Mathf.Sin(ph)) * 2.6f * u * w + X * Mathf.Cos(ph) * 2.2f * u * w;
    };

    /// <summary>Trailing things (tatters, plumes, tendrils, flame crowns) sway more the further they reach.</summary>
    Func<Vector3, Vector3> Trail(Vector3 root, float amount = 0.07f) => p =>
    {
        var b = BodyWarp(p);
        if (!Animate) return b;
        var rel = p - root;
        float d = new Vector2(rel.X, rel.Y).Length() + Mathf.Abs(rel.Z) * 0.6f;
        return b + Y * Wave(0.05f + d / (26 * u)) * d * amount;
    };

    /// <summary>Wings beat about the shoulders: a slow fold-and-lift when folded, a full stroke when spread.</summary>
    Vector3 WingWarp(Vector3 p)
    {
        var b = BodyWarp(p);
        if (!Animate) return b;
        var rel = p - _wingRoot;
        float side = rel.Y >= 0 ? 1 : -1;
        float amp = (_p.Wings == Wings.Insect ? 0.32f : _sp.WingsFolded ? 0.05f : 0.2f)
            * (_pose == Pose.Cast ? 2.2f : _pose == Pose.Walk ? 1.4f : _pose == Pose.Faint ? 0 : 1);
        float th = amp * Wave(0.1f, _p.Wings == Wings.Insect ? 2 : 1) * side;
        float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
        var turned = new Vector3(rel.X, rel.Y * cs - rel.Z * sn, rel.Y * sn + rel.Z * cs);
        return b + (turned - rel);
    }

    /// <summary>Halos, shards and wisps float on their own slower beat.</summary>
    Vector3 AuraWarp(Vector3 p) => BodyWarp(p) + Z * 1.2f * u * Wave(0.35f);

    // ---------------------------------------------------------------- helpers

    Vector3 V(float x, float y, float z) => new(x * u, y * u, z * u);
    int Part(Zone z) { int id = _c.NewPart(); _zones[id] = z; return id; }
    bool Has(Graft g) => (_sp.Grafts & g) != 0;
    Zone? ZoneAt(int x, int y) => _zones.TryGetValue(_c.PartAt(x, y), out var z) ? z : null;

    float LegMul => (_p.Limbs switch { Limbs.Stubby => 0.8f, Limbs.Long => 1.18f, _ => 1f }) * _sp.LegLen * _cr.LegJitter;
    float LimbThick => (_p.Limbs switch { Limbs.Stubby => 1.2f, Limbs.Long => 0.85f, _ => 1f }) * Mathf.Lerp(0.9f, 1.08f, (_sp.Thickness - 0.85f) / 0.35f);
    float BodyMul => _sp.BodyLen * _cr.BodyJitter;
    /// <summary>Torso girth: lean by default, never round.</summary>
    float Girth => Mathf.Lerp(0.82f, 1.0f, Mathf.Clamp((_sp.Thickness - 0.85f) / 0.35f, 0, 1));
    float HeadMul => SizeInfo.HeadScale(_cr.Size) * _sp.HeadSize * _cr.HeadJitter;

    static List<Vector3> Smooth(IReadOnlyList<Vector3> c, int per = 5)
    {
        if (c.Count < 3) return c.ToList();
        var o = new List<Vector3>();
        for (int i = 0; i < c.Count - 1; i++)
        {
            Vector3 p0 = c[Math.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Math.Min(i + 2, c.Count - 1)];
            for (int k = 0; k < per; k++) o.Add(p1.CubicInterpolate(p2, p0, p3, k / (float)per));
        }
        o.Add(c[^1]);
        return o;
    }

    /// <summary>A torso: ellipsoids swept along a smoothed spine, half-width w and half-height h at each point.</summary>
    void Loft(IReadOnlyList<(Vector3 p, float w, float h)> spine, Mat m, int part, float floor = 0, bool anchor = true)
    {
        const int per = 8;
        var dense = Smooth(spine.Select(s => s.p).ToList(), per);
        if (spine.Count == 2) dense = Enumerable.Range(0, per + 1).Select(k => spine[0].p.Lerp(spine[1].p, k / (float)per)).ToList();
        for (int i = 0; i < dense.Count; i++)
        {
            float f = spine.Count == 1 ? 0 : i / (float)per;
            int a = Math.Min((int)f, spine.Count - 1), b = Math.Min(a + 1, spine.Count - 1);
            float t = Mathf.SmoothStep(0, 1, f - a);
            float w = Mathf.Lerp(spine[a].w, spine[b].w, t), h = Mathf.Lerp(spine[a].h, spine[b].h, t);
            var dir = dense[Math.Min(i + 1, dense.Count - 1)] - dense[Math.Max(i - 1, 0)];
            if (dir.LengthSquared() < 1e-5f) dir = X;
            _c.Ellipsoid(dense[i], dir, Mathf.Min(w, h) * 0.75f + 0.5f, w, h, m, part, 0, floor);
        }
        if (anchor) _spine.AddRange(spine);
    }

    /// <summary>A point along the torso (0 rump, 1 chest) on its top surface, with the local half-width.</summary>
    (Vector3 top, float w, float h, Vector3 along) SpineAt(float t)
    {
        if (_spine.Count == 0) return (_headC, _headR, _headR, X);
        if (_spine.Count == 1) return (_spine[0].p + Z * _spine[0].h, _spine[0].w, _spine[0].h, X);
        float f = Mathf.Clamp(t, 0, 1) * (_spine.Count - 1);
        int a = Math.Min((int)f, _spine.Count - 2);
        float k = f - a;
        var p = _spine[a].p.Lerp(_spine[a + 1].p, k);
        float w = Mathf.Lerp(_spine[a].w, _spine[a + 1].w, k), h = Mathf.Lerp(_spine[a].h, _spine[a + 1].h, k);
        var along = (_spine[a + 1].p - _spine[a].p).Normalized();
        var up = BackOf(along);
        return (p + up * h * 0.92f, w, h, along);
    }

    /// <summary>Which way the back faces where the spine runs <paramref name="along"/>: up, or behind for upright bodies.</summary>
    Vector3 BackOf(Vector3 along)
    {
        var (_, _, up) = PixelCanvas.Frame(along);
        return (_upright ? up.X > 0 : up.Z < 0) ? -up : up;
    }

    Vector3 UpAt(float t)
    {
        if (_spine.Count < 2) return Z;
        float f = Mathf.Clamp(t, 0, 1) * (_spine.Count - 1);
        int a = Math.Min((int)f, _spine.Count - 2);
        return BackOf((_spine[a + 1].p - _spine[a].p).Normalized());
    }

    Vector3 TorsoMid => _spine.Count == 0 ? _headC : _spine[_spine.Count / 2].p;

    /// <summary>A leg through its joints, hip first: a muscled upper part tapering to the foot.</summary>
    void Leg(Vector3[] j, float r, bool metal = false, bool hand = false)
    {
        var before = _c.Warp;
        _c.Warp = LegWarp(j[0], j[^1], hand);
        LegShape(j, r, metal, hand);
        _c.Warp = before;
    }

    void LegShape(Vector3[] j, float r, bool metal, bool hand)
    {
        int part = Part(Zone.Limb);
        var mat = metal ? Mat.Metal : Mat.Main;
        r *= LimbThick * u;
        var upper = j[1] - j[0];
        if (!hand) _c.Ellipsoid(j[0] + upper * 0.38f, upper, upper.Length() * 0.52f, r * 1.3f, r * 1.3f, mat, part);
        _c.Tube(Smooth(j, 4), t => r * Mathf.Lerp(1f, 0.5f, Mathf.Sqrt(t)), mat, part);
        if (metal)
            for (int k = 1; k < j.Length - 1; k++) _c.Sphere(j[k], r * 0.78f, Mat.Glow, part, 0.1f, 0, true);
        var fwd = new Vector3(j[^1].X - j[^2].X, j[^1].Y - j[^2].Y, 0);
        if (hand || fwd.LengthSquared() < 0.01f || fwd.Normalized().X < 0.3f) fwd = X;
        Foot(j[^1], fwd.Normalized(), r * 0.62f, part, hand);
    }

    void Foot(Vector3 at, Vector3 fwd, float r, int part, bool hand)
    {
        var left = Z.Cross(fwd).Normalized();
        if (_p.Limbs == Limbs.Hooved && !hand)
            _c.Ellipsoid(at + Z * r * 0.75f + fwd * r * 0.15f, fwd, r * 1.05f, r * 0.95f, r * 0.85f, Mat.Accent, part, -0.2f);
        else if (_p.Limbs == Limbs.Clawed || hand)
        {
            _c.Ellipsoid(at + Z * r * 0.55f + fwd * r * 0.35f, fwd, r * 1.2f, r * 0.95f, r * 0.6f, Mat.Main, part);
            int claws = Part(Zone.Tip);
            float len = _p.Limbs == Limbs.Clawed ? 1.1f : 0.7f;
            for (int k = -1; k <= 1; k++)
            {
                var b = at + fwd * r * 1.2f + left * k * r * 0.55f + Z * r * 0.5f;
                _c.Tube(new[] { b, b + fwd * r * len - Z * r * 0.5f }, r * 0.32f, 0.12f * u, Mat.Accent, claws);
            }
        }
        else
        {
            float big = _p.Limbs == Limbs.Stubby ? 1.3f : 1f;
            _c.Ellipsoid(at + Z * r * 0.55f + fwd * r * 0.35f, fwd, r * 1.25f * big, r * big, r * 0.62f * big, Mat.Main, part);
        }
        _feet.Add((at, fwd, r, hand));
    }

    /// <summary>A neck from the shoulders to the head, bowed by <paramref name="bow"/>.</summary>
    void Neck(Vector3 from, Vector3 to, float r0, float r1, Vector3 bow)
    {
        var mid = from.Lerp(to, 0.5f) + bow;
        _c.Tube(Smooth(new[] { from, mid, to }, 6), r0 * u, r1 * u, Mat.Main, _bodyPart);
        RisingNeck(from, to, r0 * u);
    }

    /// <summary>Notes a neck that climbs out of the body's top, which shells must stop short of. Necks
    /// that jut forward or low (a behemoth's) leave the shell alone.</summary>
    void RisingNeck(Vector3 from, Vector3 to, float r)
    {
        if ((to - from).Normalized().Z > 0.35f) _necks.Add((from, r));
    }

    /// <summary>Where the head sits, how big, and which way it points (pitch in degrees, negative is nose down).</summary>
    void SetHead(Vector3 c, float r, float pitch)
    {
        // Temper shows in the carriage: predators hold their heads low and forward, pride lifts them.
        var lean = _cr.Attitude switch
        {
            Attitude.Aggressive => V(1.2f, 0, -1.6f),
            Attitude.Proud => V(-0.4f, 0, 1.4f),
            Attitude.Skittish => V(-0.6f, 0, 0.6f),
            _ => Vector3.Zero,
        };
        if (_upright || _sp.Plan is BodyPlan.Blob) lean *= 0.5f;
        _headC = c + lean;
        _headR = r * u * HeadMul;
        float p = Mathf.DegToRad(pitch + (_cr.Attitude == Attitude.Aggressive ? -8 : _cr.Attitude == Attitude.Proud ? 6 : 0));
        _headFwd = new Vector3(Mathf.Cos(p), 0, Mathf.Sin(p));
    }

    (Vector3 f, Vector3 s, Vector3 up) HeadFrame => PixelCanvas.Frame(_headFwd);
    Vector3 H(float f, float s, float up)
    {
        var (hf, hs, hu) = HeadFrame;
        return _headC + (hf * f + hs * s + hu * up) * _headR;
    }

    // ---------------------------------------------------------------- head

    void Head()
    {
        if (_faceOnBody)
        {
            // No skull: the eyes and mouth sit on the body itself.
            _headPart = _bodyPart;
            _mouth = (H(0.92f, -0.22f, -0.42f), H(0.45f, -0.72f, -0.45f));
            return;
        }
        int part = _headPart = Part(Zone.Head);
        float r = _headR;
        var (hf, hs, hu) = HeadFrame;
        float sl = _sp.SnoutLen;
        bool avian = _sp.Plan == BodyPlan.Avian, insect = _sp.Plan == BodyPlan.Insectoid;
        var shape = _p.Head;
        float wide = shape == HeadShape.Wide ? 1.22f : 1f;
        var muzzle = _sp.MuzzleSecond ? Mat.Second : Mat.Main;

        // An angular skull: the dome, a hard brow over the eyes, and the jaw's corners.
        _c.Ellipsoid(_headC, hf, 0.92f * r, 0.74f * r * wide, 0.74f * r * (shape == HeadShape.Wide ? 0.82f : 1f), Mat.Main, part);
        _c.Ellipsoid(H(0.38f, 0, 0.4f), hf, 0.45f * r, 0.78f * r * wide, 0.26f * r, Mat.Main, part);
        _c.Ellipsoid(H(0.05f, 0, -0.36f), hf, 0.62f * r, 0.64f * r * wide, 0.34f * r, Mat.Main, part);

        if (avian)
        {
            // A hooked beak; cranes get a long straight dagger.
            bool crane = _form == Form.Crane;
            float len = (crane ? 2.0f : 1.05f) * sl;
            var b0 = H(0.62f, 0, -0.05f);
            var tip = H(0.62f + len, 0, crane ? -0.35f : -0.55f);
            var pts = crane ? new[] { b0, tip } : new[] { b0, H(0.62f + len * 0.6f, 0, 0.0f), H(0.62f + len * 0.95f, 0, -0.25f), tip };
            _c.Tube(Smooth(pts, 4), 0.36f * r, 0.05f * r, Mat.Accent, Part(Zone.Tip));
            _mouth = null;
        }
        else if (insect)
        {
            // Mandibles, curving in to meet.
            int mand = Part(Zone.Tip);
            float big = _form == Form.Beetle ? 1.8f : 1f;
            foreach (int side in new[] { -1, 1 })
                _c.Tube(Smooth(new[] { H(0.65f, side * 0.35f, -0.3f), H(0.65f + 0.6f * big, side * 0.55f * big, -0.35f), H(0.7f + 0.95f * big, side * 0.12f, -0.4f) }, 4),
                    0.17f * r * Mathf.Sqrt(big), 0.04f * r, Mat.Accent, mand);
            _mouth = null;
        }
        else switch (shape)
        {
            case HeadShape.Snout:
            {
                var down = (hf - hu * 0.16f).Normalized();
                _c.Ellipsoid(H(0.55f + 0.3f * sl, 0, -0.14f), down, 0.58f * sl * r, 0.4f * r, 0.36f * r, muzzle, part);
                _c.Ellipsoid(H(0.5f + 0.27f * sl, 0, -0.42f), down, 0.5f * sl * r, 0.32f * r, 0.2f * r, muzzle, part);
                _mouth = (H(0.5f + 0.75f * sl, -0.2f, -0.32f), H(0.15f, -0.52f, -0.34f));
                break;
            }
            case HeadShape.Wide:
                _c.Ellipsoid(H(0.5f, 0, -0.32f), hf, 0.62f * r, 0.95f * r, 0.32f * r, muzzle, part);
                _mouth = (H(1.05f, -0.3f, -0.36f), H(0.2f, -0.9f, -0.34f));
                break;
            default:
                _c.Ellipsoid(H(0.62f, 0, -0.2f), hf, 0.45f * r, 0.5f * r, 0.36f * r, muzzle, part);
                _mouth = (H(0.98f, -0.22f, -0.3f), H(0.35f, -0.55f, -0.36f));
                break;
        }

        if (_p.Eyes == EyeKind.Compound)
            foreach (int side in new[] { -1, 1 })
                _c.Ellipsoid(H(0.4f, side * 0.5f, 0.22f), hf + hs * side * 0.6f, 0.34f * r * _sp.EyeSize, 0.3f * r * _sp.EyeSize, 0.36f * r * _sp.EyeSize, Mat.Pupil, part, 0.15f);

        if (Has(Graft.Visor))
        {
            // A brass-rimmed lens over the right eye, strapped round the skull.
            int visor = Part(Zone.Tip);
            var at = H(0.52f, -0.62f, 0.2f);
            _c.Sphere(at, 0.34f * r, Mat.Metal, visor);
            _c.Sphere(at + (hf * 0.25f - hs * 0.55f) * r * 0.3f, 0.24f * r, Mat.Glow, visor, 0.15f, 0, true);
            _c.Tube(Smooth(new[] { H(0.5f, -0.75f, 0.26f), H(-0.25f, -0.8f, 0.34f), H(-0.85f, 0, 0.38f), H(-0.25f, 0.8f, 0.34f), H(0.45f, 0.72f, 0.3f) }, 4), 0.1f * r, 0.1f * r, Mat.Metal, visor);
        }
    }

    // ---------------------------------------------------------------- ears and horns

    void EarsPart()
    {
        if (_p.Ears == Ears.None || _sp.Plan == BodyPlan.Insectoid) return;
        var (hf, hs, hu) = HeadFrame;
        float r = _headR * (_form == Form.Imp ? 1.35f : 1f);
        foreach (int side in new[] { -1, 1 })
        {
            int part = Part(Zone.Ear);
            var b = H(-0.22f, side * 0.5f, 0.55f);
            switch (_p.Ears)
            {
                case Ears.Pointy:
                    // Long and swept back: a jackal's, not a kitten's.
                    _c.Tube(Smooth(new[] { b, b + (hu * 0.6f - hf * 0.18f + hs * side * 0.16f) * r, b + (hu * 1.15f - hf * 0.55f + hs * side * 0.32f) * r }, 4),
                        0.24f * r, 0.03f * r, Mat.Main, part);
                    break;
                case Ears.Round:
                    _c.Ellipsoid(b + (hu * 0.25f + hs * side * 0.12f) * r, hf + hs * side * 0.3f, 0.12f * r, 0.32f * r, 0.36f * r, Mat.Main, part);
                    break;
                default:
                    _c.Tube(Smooth(new[] { b, b + (hs * side * 0.45f - hu * 0.05f) * r, b + (hs * side * 0.6f - hu * 0.6f - hf * 0.15f) * r }, 4),
                        0.22f * r, 0.15f * r, Mat.Main, part);
                    break;
            }
        }
    }

    void HornsPart()
    {
        if (_p.Horns == Horns.None) return;
        var (hf, hs, hu) = HeadFrame;
        float r = _headR;
        float c = Mathf.Clamp(_sp.HornCurve, -0.4f, 1f);
        switch (_p.Horns)
        {
            case Horns.NoseHorn:
            {
                var b = _mouth is { } m ? m.front.Lerp(_headC, 0.35f) + hu * 0.5f * r : H(0.8f, 0, 0.3f);
                _c.Tube(Smooth(new[] { b, b + (hu * 0.5f + hf * 0.1f) * r, b + (hu * 0.95f + hf * 0.3f) * r }, 4), 0.22f * r, 0.02f * r, Mat.Accent, Part(Zone.Tip));
                break;
            }
            case Horns.BrowHorn:
            {
                var b = H(0.42f, 0, 0.58f);
                _c.Tube(Smooth(new[] { b, b + (hu * 0.55f + hf * 0.2f) * r, b + (hu * 0.9f + hf * 0.75f) * r }, 4), 0.24f * r, 0.02f * r, Mat.Accent, Part(Zone.Tip));
                break;
            }
            case Horns.TwinHorns:
                foreach (int side in new[] { -1, 1 })
                {
                    var b = H(0.02f, side * 0.42f, 0.6f);
                    var pts = new[]
                    {
                        b,
                        b + (hu * 0.5f + hs * side * 0.22f - hf * 0.25f * c) * r,
                        b + (hu * (0.9f - 0.3f * c) + hs * side * 0.42f - hf * 0.85f * c + hf * 0.2f * (1 - c)) * r,
                        b + (hu * (1.15f - 0.75f * c) + hs * side * 0.55f - hf * 1.5f * c + hf * 0.5f * (1 - c)) * r,
                    };
                    _c.Tube(Smooth(pts, 4), 0.26f * r, 0.03f * r, Mat.Accent, Part(Zone.Tip));
                }
                break;
            case Horns.Antlers:
                foreach (int side in new[] { -1, 1 })
                {
                    int part = Part(Zone.Tip);
                    var b = H(-0.08f, side * 0.34f, 0.62f);
                    var beam = new[] { b, b + (hu * 0.7f + hs * side * 0.45f - hf * 0.2f) * r, b + (hu * 1.4f + hs * side * 0.8f - hf * 0.45f) * r, b + (hu * 2.05f + hs * side * 0.95f - hf * 0.55f) * r };
                    _c.Tube(Smooth(beam, 4), 0.14f * r, 0.04f * r, Mat.Accent, part);
                    for (int k = 1; k <= 2; k++)
                        _c.Tube(new[] { beam[k], beam[k] + (hu * 0.6f + hf * 0.4f + hs * side * 0.05f) * r }, 0.09f * r, 0.03f * r, Mat.Accent, part);
                }
                break;
            case Horns.Crest:
                if (_sp.Plan == BodyPlan.Avian)
                {
                    int plume = Part(Zone.Tip);
                    for (int k = 0; k < 4; k++)
                    {
                        var b = H(0.25f - k * 0.18f, 0, 0.72f);
                        _c.Tube(Smooth(new[] { b, b + (hu * (0.55f - 0.08f * k) - hf * 0.3f) * r, b + (hu * (0.95f - 0.15f * k) - hf * (0.8f + 0.2f * k)) * r }, 3), 0.14f * r, 0.04f * r, _crestMat, plume);
                    }
                }
                else
                {
                    // A jagged fin from brow to nape.
                    var fin = new[] { H(0.45f, 0, 0.5f), H(0.35f, 0, 1.0f), H(0.05f, 0, 0.78f), H(-0.15f, 0, 1.3f), H(-0.45f, 0, 0.88f), H(-0.7f, 0, 1.45f), H(-0.95f, 0, 0.5f) };
                    _c.Membrane(fin, _crestMat, Part(Zone.Tip));
                }
                break;
            case Horns.Antennae:
                foreach (int side in new[] { -1, 1 })
                {
                    int part = Part(Zone.Tip);
                    var b = H(0.42f, side * 0.28f, 0.66f);
                    var tip = b + (hu * 1.35f + hf * 0.65f + hs * side * 0.6f) * r;
                    _c.Tube(Smooth(new[] { b, b + (hu * 0.85f + hf * 0.15f + hs * side * 0.25f) * r, tip }, 4), 0.08f * r, 0.06f * r, Mat.Main, part);
                    _c.Sphere(tip, 0.17f * r, Mat.Glow, part, 0.1f, 0, true);
                }
                break;
        }
    }

    // ---------------------------------------------------------------- back

    void BackPart()
    {
        if (_spine.Count == 0) return;
        switch (_p.Back)
        {
            case Back.Spikes:
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(5 * BodyMul * Mathf.Sqrt(u)), 4, 8);
                for (int k = 0; k < n; k++)
                {
                    float t = Mathf.Lerp(0.08f, 0.95f, k / (float)(n - 1));
                    var (top, w, _, along) = SpineAt(t);
                    var up = UpAt(t);
                    float tall = (3f + 3.5f * Mathf.Sin(Mathf.Pi * (0.25f + 0.6f * t))) * u;
                    _c.Tube(new[] { top - up * 0.6f * u, top + up * tall - along * tall * 0.4f }, 1.7f * u, 0.12f * u, _spikeMat, Part(Zone.Tip));
                }
                break;
            }
            case Back.Shell:
            {
                var (top, w, h, along) = SpineAt(0.45f);
                var mid = TorsoMid;
                float len = _spine.Count < 2 ? w * 2 : (_spine[^1].p - _spine[0].p).Length();
                // Overlapping plates from the rump forward, each its own part so the seams between them
                // show. They stop short of the shoulders: necks rise clear of the shell, never through it.
                var shoulders = _spine[^1].p - (_spine.Count < 2 ? X : (_spine[^1].p - _spine[^2].p).Normalized()) * (_spine[^1].w * 0.6f + 1.5f * u);
                for (int k = 0; k < 3; k++)
                {
                    float t = 0.18f + 0.24f * k;
                    var (pt, pw, ph, pa) = SpineAt(t);
                    var up = UpAt(t);
                    var c0 = pt - up * ph * (_upright ? 0.4f : 0.62f) + pa * u * (k - 1);
                    float floor = _upright ? 0 : c0.Z - ph * 0.15f;
                    float rl = len * 0.26f + 1.5f * u, full = rl;
                    float over = (c0 - shoulders).Dot(pa) + rl;
                    if (over > 0) rl = Mathf.Max(rl * 0.45f, rl - over);
                    // And clear of every rising neck's base, however thick: a plate with no room is left off.
                    foreach (var (root, nr) in _necks) rl = Mathf.Min(rl, (root - c0).Dot(pa) - nr - 1.2f * u);
                    if (rl < full * 0.35f) continue;
                    // Smooth plates, no rims or knobs: the user preferred this to an armoured version.
                    _c.Ellipsoid(c0, pa, rl, pw * 1.28f, ph * 0.9f, _shellMat, Part(Zone.Body), 0.04f * k, floor);
                }
                break;
            }
            case Back.Sail:
            {
                int n = 9;
                var bottom = new List<Vector3>();
                var tops = new List<Vector3>();
                for (int k = 0; k < n; k++)
                {
                    float t = Mathf.Lerp(0.05f, 0.95f, k / (float)(n - 1));
                    var (top, _, _, along) = SpineAt(t);
                    var up = UpAt(t);
                    bottom.Add(top - up * 0.8f * u);
                    tops.Add(top + up * (2f + 7.5f * Mathf.Sin(Mathf.Pi * t)) * u * (k % 2 == 0 ? 1f : 0.86f) - along * 1.5f * u);
                }
                int part = Part(Zone.Tip);
                _c.Membrane(bottom.Concat(Enumerable.Reverse(tops)).ToList(), _sailMat, part);
                for (int k = 0; k < n; k += 2) _c.Tube(new[] { bottom[k], tops[k] }, 0.55f * u, 0.3f * u, _spikeMat, part);
                break;
            }
            case Back.Mane:
            {
                int part = Part(Zone.Body);
                var (top, w, h, along) = SpineAt(0.92f);
                if (_sp.Plan == BodyPlan.Serpent)
                {
                    // On a serpent, a hood flaring behind the head.
                    var nb = _headC - _headFwd * _headR * 1.2f - Z * _headR * 0.6f;
                    foreach (int side in new[] { -1, 1 })
                        _c.Ellipsoid(nb + Y * side * _headR * 0.75f, _headFwd + Z * 0.4f, _headR * 1.3f, _headR * 0.25f, _headR * 1.0f, _tuftMat, part);
                    break;
                }
                // A ruff of spiky tufts around the neck, swept back down it.
                var neck = top.Lerp(_headC, 0.25f);
                var nd = (_headC - neck).Normalized();
                var (_, ra, rb) = PixelCanvas.Frame(nd);
                if (rb.Dot(BackOf(along)) < 0) rb = -rb;
                for (int k = 0; k < 9; k++)
                {
                    float a = Mathf.Lerp(-1.9f, 1.9f, k / 8f);
                    var radial = (rb * Mathf.Cos(a) + ra * Mathf.Sin(a)).Normalized();
                    var b = neck + radial * w * 0.75f;
                    var tip = b + (radial * 0.55f - nd * 0.9f).Normalized() * (5.5f + (k % 2) * 1.5f) * u;
                    _c.Tube(new[] { b, tip }, 2.4f * u, 0.3f * u, _tuftMat, part);
                }
                break;
            }
            case Back.Crystals:
            {
                int n = Mathf.Clamp(Mathf.RoundToInt(4 * Mathf.Sqrt(u)), 3, 6);
                for (int k = 0; k < n; k++)
                {
                    float t = Mathf.Lerp(0.15f, 0.9f, k / (float)(n - 1));
                    var (top, w, _, along) = SpineAt(t);
                    float side = (k % 2 == 0 ? 1 : -1) * 0.35f;
                    float len = (5f + 3f * Mathf.Sin(Mathf.Pi * t) + _rng.Range(-1f, 1f)) * u;
                    var dir = (UpAt(t) + Y * side - along * 0.25f).Normalized();
                    var b = top + Y * side * w * 0.6f;
                    _c.Tube(new[] { b - dir * u, b + dir * len * 0.7f, b + dir * len }, t2 => (2.0f - 1.7f * t2 * t2) * u, Mat.Accent, Part(Zone.Tip));
                }
                break;
            }
        }
    }

    // ---------------------------------------------------------------- tail

    void TailPart()
    {
        if (_bodyIsTail)
        {
            // The body already ends in a tail; the slot decorates its end.
            TailTip(_tailRoot, _tailDir, Mathf.Max(_tailR, 0.8f * u));
            return;
        }
        if (_p.Tail == Tail.None) { _tailEnd = _tailRoot; return; }
        var dir = _tailDir.Normalized();
        var side = Z.Cross(dir).Normalized();
        float r = _tailR;
        float sway = _rng.Range(-1f, 1f);
        int part = Part(Zone.Tail);
        List<Vector3> Curve(float len, float[] rise, float[] swing)
        {
            var pts = new List<Vector3>();
            for (int k = 0; k < rise.Length; k++)
            {
                float f = k / (float)(rise.Length - 1);
                pts.Add(_tailRoot + dir * len * f + Z * rise[k] * u + side * swing[k] * sway * u);
            }
            return Smooth(pts, 5);
        }
        float L = BodyMul * (_upright ? 0.85f : 1f);
        switch (_p.Tail)
        {
            case Tail.Whip:
            {
                var pts = Curve(32 * L * u, new[] { 0f, -2.5f, -2f, 1f, 4f }, new[] { 0f, 2f, 4.5f, 3f, -1f });
                _c.Tube(pts, r, 0.35f * u, Mat.Main, part);
                _tailEnd = pts[^1];
                // A barbed spade on the end.
                var end = pts[^1]; var back = (pts[^1] - pts[^3]).Normalized();
                _c.Membrane(new[] { end - back * 0.5f * u, end + Z.Cross(back) * 2f * u - back * 1.2f * u, end + back * 3f * u, end - Z.Cross(back) * 2f * u - back * 1.2f * u }, Mat.Accent, Part(Zone.Tip));
                break;
            }
            case Tail.Thick:
            {
                var pts = Curve(22 * L * u, new[] { 0f, -2f, -4.5f, -6f }, new[] { 0f, 1.5f, 3f, 2f });
                _c.Tube(pts, r * 1.3f, 0.9f * u, Mat.Main, part);
                _tailEnd = pts[^1];
                break;
            }
            case Tail.Fluffy:
            {
                var pts = Curve(20 * L * u, new[] { 0f, 3f, 7f, 9.5f, 10f }, new[] { 0f, 1f, 2.5f, 3f, 2f });
                _c.Tube(pts, t => Mathf.Lerp(r, 0.4f * u, t) + 3.4f * u * Mathf.Sin(Mathf.Pi * Mathf.Pow(t, 0.8f)), Mat.Main, part);
                _tailEnd = pts[^1];
                break;
            }
            case Tail.Fan:
            {
                if (_sp.Plan == BodyPlan.Aquatic) { TailTip(_tailRoot, dir, r); break; }
                // Long feathers fanned out, bowing to the ground.
                var stub = _tailRoot + dir * 3 * u;
                _c.Tube(new[] { _tailRoot, stub }, r, r * 0.8f, Mat.Main, part);
                int fan = Part(Zone.Tail);
                for (int k = -2; k <= 2; k++)
                {
                    var tip = stub + dir * (14 - Mathf.Abs(k) * 1.5f) * L * u + side * k * 3.2f * u + Z * (k == 0 ? 1f : -0.5f) * u;
                    _c.Tube(new[] { stub, stub.Lerp(tip, 0.5f) + Z * 1.5f * u, tip }, 1.3f * u, 0.5f * u, _featherMat, fan);
                }
                _tailEnd = stub + dir * 14 * L * u;
                break;
            }
            case Tail.Club:
            {
                var pts = Curve(18 * L * u, new[] { 0f, -1.5f, -2.5f, -2f }, new[] { 0f, 1f, 2f, 1.5f });
                _c.Tube(pts, r * 1.2f, 1.2f * u, Mat.Main, part);
                var end = pts[^1];
                int club = Part(Zone.Tip);
                _c.Ellipsoid(end, (pts[^1] - pts[^3]), 3.8f * u, 3.1f * u, 2.8f * u, _shellMat, club);
                foreach (var d in new[] { Z, side, -side, Z + side, Z - side })
                    _c.Tube(new[] { end + d.Normalized() * 2.4f * u, end + d.Normalized() * 4.6f * u }, 0.9f * u, 0.1f * u, Mat.Accent, club);
                _tailEnd = end;
                break;
            }
            case Tail.Curl:
            {
                var pts = new List<Vector3>();
                var center = _tailRoot + dir * 4.5f * u + Z * 6 * u;
                for (int k = 0; k <= 16; k++)
                {
                    float a = Mathf.Lerp(-Mathf.Pi * 0.55f, Mathf.Pi * 1.35f, k / 16f);
                    float rad = Mathf.Lerp(6f, 2.5f, k / 16f) * u;
                    pts.Add(center + dir * Mathf.Cos(a) * rad + Z * Mathf.Sin(a) * rad + side * k * 0.12f * u);
                }
                _c.Tube(pts, r, 0.7f * u, Mat.Main, part);
                _tailEnd = pts[^1];
                break;
            }
        }
    }

    /// <summary>The end of a body that is its own tail (serpents, fish): a fin, a club, a tuft or a lash.</summary>
    void TailTip(Vector3 end, Vector3 dir, float r)
    {
        dir = dir.Normalized();
        var side = Z.Cross(dir).Normalized();
        _tailEnd = end;
        switch (_p.Tail)
        {
            case Tail.Fan:
            case Tail.None when _sp.Plan == BodyPlan.Aquatic:
            {
                // A forked fin, standing upright.
                var fin = new[] { end - dir * u, end + dir * 6 * u + Z * 6.5f * u, end + dir * 4 * u + Z * 1.5f * u, end + dir * 4.5f * u - Z * 1.5f * u, end + dir * 6 * u - Z * 5.5f * u };
                _c.Membrane(fin, _sailMat, Part(Zone.Tail));
                _tailEnd = end + dir * 6 * u;
                break;
            }
            case Tail.Club:
            {
                int club = Part(Zone.Tip);
                _c.Ellipsoid(end, dir, 3.4f * u, 2.8f * u, 2.6f * u, _shellMat, club);
                foreach (var d in new[] { Z, side, -side })
                    _c.Tube(new[] { end + d * 2.2f * u, end + d * 4.2f * u }, 0.8f * u, 0.1f * u, Mat.Accent, club);
                break;
            }
            case Tail.Fluffy:
                _c.Tube(new[] { end, end + dir * 4 * u + Z * 2 * u, end + dir * 7 * u + Z * 1 * u }, t => (1.5f + 2.5f * Mathf.Sin(Mathf.Pi * t)) * u, _tuftMat, Part(Zone.Tail));
                _tailEnd = end + dir * 7 * u;
                break;
            case Tail.Whip:
            {
                var pts = Smooth(new[] { end, end + dir * 5 * u + Z * 1.5f * u + side * 2 * u, end + dir * 10 * u + Z * 3 * u }, 4);
                _c.Tube(pts, r, 0.3f * u, Mat.Main, Part(Zone.Tail));
                _tailEnd = pts[^1];
                break;
            }
        }
    }

    // ---------------------------------------------------------------- wings

    void WingsPart()
    {
        if (_p.Wings == Wings.None) return;
        bool folded = _sp.WingsFolded && _p.Wings != Wings.Insect;
        float span = (_p.Wings == Wings.Tiny ? 7f : 15f + 9f * _sp.WingSpread) * u * _wingSize;
        foreach (int side in new[] { 1, -1 })
        {
            var (_, w, _, _) = SpineAt(0.8f);
            var b = _wingRoot + Y * side * w * 0.7f;
            var o = Y * side; // outward
            // Folded wings lie along the back: level on a horizontal body, down it on an upright one.
            var X = _upright ? new Vector3(0.35f, 0, 0.94f) : CreatureSculptor.X;
            var Z = _upright ? new Vector3(-0.94f, 0, 0.35f) : CreatureSculptor.Z;
            if (!folded) { X = CreatureSculptor.X; Z = CreatureSculptor.Z; }
            int bone = Part(Zone.Wing), skin = Part(Zone.Wing);
            switch (_p.Wings)
            {
                case Wings.Bat:
                case Wings.Tiny when _sp.Plan != BodyPlan.Avian:
                    if (folded)
                    {
                        var fold = new[] { b, b - X * span * 0.55f + Z * span * 0.3f + o * span * 0.12f, b - X * span * 0.85f - Z * span * 0.05f + o * span * 0.15f, b - X * span * 0.4f - Z * span * 0.3f + o * span * 0.16f };
                        _c.Membrane(fold, Mat.Membrane, skin);
                        _c.Tube(new[] { fold[0], fold[1], fold[2] }, 0.9f * u, 0.4f * u, Mat.Main, bone);
                        break;
                    }
                    var elbow = b + o * span * 0.3f + Z * span * 0.38f - X * span * 0.08f;
                    var wrist = b + o * span * 0.52f + Z * span * 0.72f + X * span * 0.04f;
                    var f1 = wrist + o * span * 0.45f + Z * span * 0.18f - X * span * 0.12f;
                    var f2 = wrist + o * span * 0.38f - Z * span * 0.2f - X * span * 0.38f;
                    var f3 = wrist + o * span * 0.1f - Z * span * 0.5f - X * span * 0.5f;
                    var root = b - X * span * 0.42f - Z * span * 0.06f;
                    _c.Membrane(new[] { wrist, f1, f2 }, Mat.Membrane, skin);
                    _c.Membrane(new[] { wrist, f2, f3 }, Mat.Membrane, skin);
                    _c.Membrane(new[] { b, elbow, wrist, f3, root }, Mat.Membrane, skin);
                    _c.Tube(new[] { b, elbow, wrist }, 1.3f * u, 0.8f * u, Mat.Main, bone);
                    foreach (var f in new[] { f1, f2, f3 }) _c.Tube(new[] { wrist, f }, 0.6f * u, 0.25f * u, Mat.Main, bone);
                    _c.Tube(new[] { wrist, wrist + Z * 2 * u + X * 0.8f * u }, 0.5f * u, 0.1f * u, Mat.Accent, Part(Zone.Tip));
                    break;
                case Wings.Feather:
                case Wings.Tiny:
                    if (folded)
                    {
                        // Three tracts laid over each other, each its own part so the edges show:
                        // short coverts on the shoulder, the secondaries, and the long primaries to a point.
                        float f = span * 0.72f;
                        _c.Membrane(new[] { b + o * 0.6f * u, b - X * f * 0.3f + Z * f * 0.08f + o * f * 0.15f, b - X * f * 0.45f - Z * f * 0.04f + o * f * 0.16f, b - X * f * 0.12f - Z * f * 0.16f + o * f * 0.16f }, Mat.Main, Part(Zone.Wing), 0.06f);
                        _c.Membrane(new[] { b - X * f * 0.12f + o * f * 0.1f, b - X * f * 0.58f + Z * f * 0.05f + o * f * 0.12f, b - X * f * 0.78f - Z * f * 0.07f + o * f * 0.11f, b - X * f * 0.32f - Z * f * 0.2f + o * f * 0.13f }, _featherMat, skin);
                        _c.Membrane(new[] { b - X * f * 0.42f + o * f * 0.06f, b - X * f * 0.85f + Z * f * 0.02f + o * f * 0.07f, b - X * f * 1.08f - Z * f * 0.06f + o * f * 0.04f, b - X * f * 0.72f - Z * f * 0.12f + o * f * 0.09f }, _featherMat, Part(Zone.Wing), -0.12f);
                        break;
                    }
                    {
                        // Spread: an arm of bone and coverts, then every flight feather on its own,
                        // secondaries trailing back from the arm and primaries fanned from the wrist.
                        var elbowF = b + o * span * 0.22f + Z * span * 0.3f - X * span * 0.04f;
                        var wr = b + o * span * 0.46f + Z * span * 0.55f + X * span * 0.02f;
                        _c.Tube(Smooth(new[] { b, elbowF, wr }, 3), 1.1f * u, 0.8f * u, Mat.Main, bone);
                        var plane = (wr - b).Cross(-X).Normalized();
                        void Feather(Vector3 root, Vector3 dir, float len, float wid, float bias)
                        {
                            dir = dir.Normalized();
                            var side = dir.Cross(plane).Normalized();
                            _c.Membrane(new[] { root, root + dir * len * 0.35f + side * wid, root + dir * len, root + dir * len * 0.4f - side * wid * 0.55f }, _featherMat, Part(Zone.Wing), bias);
                        }
                        for (int k = 0; k < 5; k++)
                        {
                            var quillAt = b.Lerp(wr, 0.12f + 0.19f * k);
                            Feather(quillAt, -X + o * 0.12f * k - Z * 0.1f, span * (0.36f + 0.025f * k), span * 0.07f, k % 2 == 0 ? 0.04f : -0.04f);
                        }
                        for (int k = 0; k < 6; k++)
                        {
                            float a = Mathf.Lerp(0.55f, 1.45f, k / 5f);
                            var quillAt = wr + o * span * 0.03f * k - X * span * 0.01f * k;
                            Feather(quillAt, -X * Mathf.Cos(a) + o * Mathf.Sin(a) + Z * 0.08f, span * (0.42f + 0.05f * k), span * 0.06f, k % 2 == 0 ? -0.02f : 0.06f);
                        }
                        _c.Membrane(new[] { b + Z * 0.4f * u, elbowF + Z * 0.4f * u, wr + Z * 0.4f * u, wr - X * span * 0.16f + Z * 0.2f * u, b - X * span * 0.2f }, Mat.Main, bone, 0.08f);
                    }
                    break;
                case Wings.Insect:
                    for (int k = 0; k < 2; k++)
                    {
                        float len = span * (k == 0 ? 0.95f : 0.75f), wid = span * 0.28f;
                        var axis = (o * 0.55f + Z * (0.62f - 0.25f * k) - X * (0.55f + 0.3f * k)).Normalized();
                        var across = axis.Cross(o).Normalized();
                        var c0 = b - X * k * 2 * u;
                        var pts = new List<Vector3>();
                        for (int a = 0; a < 12; a++)
                        {
                            float t = a / 12f * Mathf.Tau;
                            pts.Add(c0 + axis * len * 0.5f * (1 + Mathf.Cos(t)) + across * wid * 0.5f * Mathf.Sin(t));
                        }
                        _c.Membrane(pts, Mat.Membrane, skin, 0.15f);
                        _c.Tube(new[] { c0, c0 + axis * len * 0.9f }, 0.35f * u, 0.25f * u, Mat.Glow, bone, 0, 0);
                    }
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- grafts (volumes)

    void Grafts()
    {
        if (_spine.Count == 0) return;
        var (front, fw, fh, along) = SpineAt(0.85f);
        var (mid, mw, mh, _) = SpineAt(0.5f);
        if (Has(Graft.Plating))
        {
            // A pauldron over the right shoulder and a plate down the flank, riveted. A sparse form
            // wears them smaller and on its far side, clear of the eye.
            int plate = Part(Zone.Body);
            float k = _sparse ? 0.55f : 1;
            var away = _sparse ? Y : -Y;
            var shoulder = front - Z * fh * 0.35f + away * fw * 0.75f;
            _c.Ellipsoid(shoulder, along, fh * 0.9f * k, fw * 0.45f * k, fh * 0.75f * k, Mat.Metal, plate);
            var flank = mid - Z * mh * 0.6f + away * mw * 0.85f;
            _c.Ellipsoid(flank, along, mh * 1.2f * k, mw * 0.3f * k, mh * 0.6f * k, Mat.Metal, plate, -0.05f);
        }
        if (Has(Graft.Core))
        {
            // A hextech core set in the chest, burning through.
            int core = Part(Zone.Body);
            var last = _spine[^1];
            var chest = _upright
                ? last.p + X * last.h * 0.85f - Z * last.h * 0.3f - Y * last.w * 0.25f
                : last.p + along * (Mathf.Min(last.w, last.h) * 0.75f + 0.5f) - Z * last.h * 0.15f - Y * last.w * 0.25f;
            _c.Sphere(chest, 2.5f * u, Mat.Metal, core);
            _c.Sphere(chest + ((_upright ? X : along) - Y * 0.6f).Normalized() * 1.1f * u, 1.7f * u, Mat.Glow, core, 0.1f, 0, true);
        }
        if (Has(Graft.Canister))
        {
            // A glass tank of chem strapped to the back, piped forward.
            int tank = Part(Zone.Body);
            var up = UpAt(0.5f);
            var a = mid + up * 2.0f * u - along * 3 * u;
            var b = mid + up * 2.0f * u + along * 4 * u;
            _c.Tube(new[] { a, b }, 2.3f * u, 2.3f * u, Mat.Glow, tank, 0.05f);
            _c.Sphere(a, 2.6f * u, Mat.Metal, tank);
            _c.Sphere(b, 2.6f * u, Mat.Metal, tank);
            _c.Tube(Smooth(new[] { b, b + along * 3 * u + up * 1.5f * u, front + along * u }, 4), 0.6f * u, 0.6f * u, Mat.Metal, Part(Zone.Body));
        }
        Steamwork();
    }

    /// <summary>
    /// Steampunk work (2026-11-02): meshing cogs or copper pipework with a pressure gauge on the flank;
    /// twin pistons or a smokestack on the back. The cogs turn, the pistons pump, the soot rises and
    /// the gauge's needle wavers, all with the breath.
    /// </summary>
    void Steamwork()
    {
        float phase = Animate ? _phase : 0;
        var side = -Y;
        if (Has(Graft.Gears))
        {
            var (top, w, h, along) = SpineAt(0.3f);
            var hub = top - Z * h * 1.1f + side * (w + 0.7f * u);
            float r = Mathf.Max(2.2f * u, h * 0.62f), turn = phase * Mathf.Tau / 2;
            Cog(hub, along, side, r, 8, turn);
            Cog(hub + along * r * 1.5f + Z * r * 0.6f, along, side, r * 0.62f, 6, -turn * 8 / 6f + 0.3f);
        }
        if (Has(Graft.Pipework))
        {
            var (fore, fw, fh, along) = SpineAt(0.78f);
            var (aft, aw, ah, _) = SpineAt(0.22f);
            int pipes = Part(Zone.Body);
            for (int k = 0; k < 2; k++)
            {
                float drop = 0.45f + k * 0.35f;
                var a = fore - Z * fh * drop + side * fw * 0.95f;
                var b = aft - Z * ah * drop + side * aw * 0.95f;
                _c.Tube(Smooth(new[] { a, a.Lerp(b, 0.5f) + side * 0.7f * u - Z * 0.5f * u, b }, 4), 0.45f * u, 0.45f * u, Mat.Metal, pipes);
            }
            // The gauge at the back end of the pipes: a glowing dial, its needle wavering.
            var g = aft - Z * ah * 0.6f + side * aw * 1.1f;
            _c.Ellipsoid(g, along, Z, side, new Vector3(1.6f * u, 1.6f * u, 0.55f * u), Mat.Metal, Part(Zone.Body));
            _c.Ellipsoid(g + side * 0.4f * u, along, Z, side, new Vector3(1.15f * u, 1.15f * u, 0.3f * u), Mat.Glow, Part(Zone.Body), 0.1f, 0, true);
            float swing = 0.9f + Mathf.Sin(Mathf.Tau * phase * 2) * 0.35f;
            var pivot = g + side * 0.7f * u;
            _c.Tube(new[] { pivot, pivot + (along * Mathf.Cos(swing) + Z * Mathf.Sin(swing)) * 1.0f * u }, 0.22f * u, 0.14f * u, Mat.Line, Part(Zone.Body), 0.15f);
        }
        if (Has(Graft.Pistons))
        {
            // Twin pistons astride the spine, their rods pumping.
            var (top, w, _, _) = SpineAt(0.72f);
            var up = UpAt(0.72f);
            float stroke = 0.5f + 0.5f * Mathf.Sin(Mathf.Tau * phase);
            foreach (int s in new[] { -1, 1 })
            {
                var b = top + Y * s * w * 0.6f - up * 0.6f * u;
                var mouth = b + up * 3.2f * u;
                int cylinder = Part(Zone.Body);
                _c.Tube(new[] { b, mouth }, 1.45f * u, 1.3f * u, Mat.Metal, cylinder, 0.1f);
                _c.Sphere(mouth, 1.0f * u, Mat.Glow, cylinder, 0.2f, 0, true);
                var head = mouth + up * (1.0f + stroke * 1.6f) * u;
                int rod = Part(Zone.Body);
                _c.Tube(new[] { mouth, head }, 0.55f * u, 0.55f * u, Mat.Metal, rod, 0.1f);
                _c.Sphere(head, 1.05f * u, Mat.Metal, rod, 0.1f);
            }
        }
        if (Has(Graft.Smokestack))
        {
            // A flared stack toward the rear, puffing soot that rises and drifts back.
            var (top, _, _, along) = SpineAt(0.3f);
            var up = UpAt(0.3f);
            int stack = Part(Zone.Body);
            var b = top - up * 0.6f * u;
            var lip = b + up * 5.0f * u;
            _c.Tube(new[] { b, lip }, 1.5f * u, 1.2f * u, Mat.Metal, stack, 0.1f);
            _c.Tube(new[] { lip, lip + up * 1.1f * u }, 1.9f * u, 2.0f * u, Mat.Metal, stack, 0.1f);
            _c.Sphere(lip + up * 1.2f * u, 1.3f * u, Mat.Line, stack, 0.2f, 0, true);
            // Steam: pale puffs rising and drifting back, swelling as they go.
            for (int k = 0; k < 3; k++)
            {
                float f = (phase + k / 3f) % 1f;
                var puff = lip + up * (2.8f + f * 6f) * u - along * f * 4f * u;
                _c.Sphere(puff, (1.0f + f * 1.4f) * u, Mat.EyeWhite, Part(Zone.Aura), -0.4f);
            }
        }
    }

    /// <summary>A brass cog standing on <paramref name="normal"/>: a disc, its teeth turned by <paramref name="turn"/>, a glowing hub.</summary>
    void Cog(Vector3 hub, Vector3 along, Vector3 normal, float r, int teeth, float turn)
    {
        int part = Part(Zone.Body);
        var up = normal.Cross(along).Normalized();
        _c.Ellipsoid(hub, along, up, normal, new Vector3(r, r, 0.8f * u), Mat.Metal, part, 0.15f);
        for (int k = 0; k < teeth; k++)
        {
            float a = turn + k * Mathf.Tau / teeth;
            _c.Sphere(hub + (along * Mathf.Cos(a) + up * Mathf.Sin(a)) * r * 1.12f, Mathf.Max(0.6f * u, r * 0.22f), Mat.Metal, part, 0.15f);
        }
        _c.Sphere(hub + normal * 0.7f * u, Mathf.Max(0.6f * u, r * 0.3f), Mat.Glow, Part(Zone.Body), 0.2f, 0, true);
    }

    // ---------------------------------------------------------------- element themes

    /// <summary>
    /// The element as a theme, worn: embers burn at the tail, frost and stone grow shards, leaf
    /// sprouts fronds, tide grows fins, venom a frill, shade trails smoke, spark crackles, meadow tufts.
    /// </summary>
    void Motif()
    {
        if (_spine.Count == 0 && _sp.Plan != BodyPlan.Blob) return;
        var (front, fw, fh, along) = SpineAt(0.85f);
        var (rear, rw, rh, _) = SpineAt(0.15f);
        int part = Part(Zone.Tip);
        switch (_sp.Element)
        {
            case Element.Ember:
                Flame(_tailEnd == default ? rear : _tailEnd, 1f);
                if (_p.Back is Back.None or Back.Mane) { Flame(front + Z * 0.5f * u, 0.7f); Flame(SpineAt(0.5f).top + Z * 0.3f * u, 0.55f); }
                break;
            case Element.Frost:
            case Element.Stone:
                foreach (int side in new[] { -1, 1 })
                {
                    var b = front - Z * fh * 0.1f + Y * side * fw * 0.8f;
                    var dir = (Y * side * 0.55f + Z * 0.7f - along * 0.55f).Normalized();
                    if (_sp.Element == Element.Frost)
                        _c.Tube(new[] { b, b + dir * 6 * u }, t => (1.4f - 1.3f * t) * u, Mat.Accent, part, 0.1f);
                    else
                        _c.Ellipsoid(b + dir * 1.5f * u, dir + along, 3.2f * u, 2.2f * u, 1.3f * u, Mat.Accent, part, -0.05f);
                }
                break;
            case Element.Leaf:
                foreach (var (at, dir) in new[] { (_headC - _headFwd * _headR * 0.6f + Z * _headR * 0.6f, (Z - _headFwd * 0.8f).Normalized()), (_tailEnd == default ? rear : _tailEnd, (Z * 0.6f - along).Normalized()) })
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var d = (dir + Y * k * 0.5f).Normalized();
                        var side = d.Cross(Z).Normalized();
                        _c.Membrane(new[] { at, at + d * 3.2f * u + side * 1.6f * u, at + d * 6.5f * u, at + d * 3.2f * u - side * 1.6f * u }, Mat.Second, part, 0.05f);
                    }
                break;
            case Element.Tide:
                foreach (var f in _feet.Where(f => !f.hand).Take(2))
                {
                    var knee = f.p + Z * 7 * u;
                    _c.Membrane(new[] { knee, knee - X * 4 * u + Z * 1.5f * u, knee - X * 3 * u - Z * 2.5f * u }, _sailMat, part);
                }
                if (_p.Back == Back.None)
                {
                    var (top, _, _, a2) = SpineAt(0.55f);
                    _c.Membrane(new[] { top - a2 * 4 * u, top + Z * 5 * u - a2 * 3 * u, top + a2 * 3 * u }, _sailMat, part);
                }
                break;
            case Element.Venom:
            {
                // A frill flaring behind the head.
                var (hf, hs, hu) = HeadFrame;
                var ring = new List<Vector3>();
                for (int k = 0; k <= 10; k++)
                {
                    float a = Mathf.Lerp(-2.2f, 2.2f, k / 10f);
                    float rad = _headR * (k % 2 == 0 ? 1.55f : 1.25f);
                    ring.Add(H(-0.55f, 0, -0.1f) + (hu * Mathf.Cos(a) + hs * Mathf.Sin(a)) * rad - hf * _headR * 0.25f);
                }
                ring.Insert(0, H(-0.55f, 0, -0.2f));
                _c.Membrane(ring, Mat.Second, part, 0.05f);
                break;
            }
            case Element.Shade:
            {
                // Smoke trailing off the back and the tail.
                var src = _tailEnd == default ? rear : _tailEnd;
                for (int k = 0; k < 3; k++)
                {
                    var b = src.Lerp(front, k * 0.3f) + Z * rh * 0.5f;
                    var pts = Smooth(new[] { b, b - along * 4 * u + Z * 3 * u + Y * (k - 1) * 2 * u, b - along * 8 * u + Z * 4 * u + Y * (k - 1) * 4 * u }, 4);
                    _c.Tube(pts, 1.6f * u, 0.3f * u, Mat.Main, Part(Zone.Aura), -0.35f);
                }
                break;
            }
            case Element.Spark:
            {
                var at = _tailEnd == default ? rear : _tailEnd;
                for (int k = 0; k < 3; k++)
                {
                    var dir = (Z + Y * (k - 1) * 0.7f - along * 0.4f).Normalized();
                    var side = dir.Cross(along).Normalized();
                    _c.Tube(new[] { at, at + dir * 2 * u + side * u, at + dir * 3.5f * u - side * u, at + dir * 5.5f * u }, 0.5f * u, 0.3f * u, Mat.Glow, Part(Zone.Aura), 0.1f, -100);
                }
                break;
            }
            case Element.Meadow:
                foreach (int side in new[] { -1, 1 })
                {
                    var b = H(-0.1f, side * 0.65f, -0.3f);
                    _c.Tube(new[] { b, b + (Y * side * 0.8f - _headFwd * 0.5f - Z * 0.2f).Normalized() * 3.5f * u }, 1.2f * u, 0.2f * u, _tuftMat, part);
                }
                foreach (var f in _feet.Where(f => !f.hand))
                    _c.Tube(new[] { f.p + Z * 4 * u, f.p + Z * 3 * u - X * 2.5f * u }, 1.1f * u, 0.2f * u, _tuftMat, part);
                break;
        }
    }

    /// <summary>A tongue of fire rising from a point.</summary>
    void Flame(Vector3 at, float size)
    {
        int part = Part(Zone.Aura);
        var before = _c.Warp;
        _c.Warp = p => before(p) + Y * 0.8f * size * u * Wave(0, 2) * Mathf.Clamp((p.Z - at.Z) / (7.5f * size * u), 0, 1);
        var pts = Smooth(new[] { at, at + Z * 2.5f * size * u + X * 0.6f * u, at + Z * 5f * size * u - X * 1.2f * u, at + Z * 7.5f * size * u - X * 0.4f * u }, 4);
        _c.Tube(pts, t => (2.2f - 2.0f * t) * size * u, Mat.Glow, part, 0.1f, -100, true);
        _c.Tube(pts.Take(pts.Count / 2).ToList(), t => (1.1f - 0.8f * t) * size * u, Mat.Glow, part, 0.5f, -100, true);
        _c.Warp = before;
    }

    // ---------------------------------------------------------------- aura

    void AuraPart()
    {
        int part = Part(Zone.Aura);
        var (hf, hs, hu) = HeadFrame;
        switch (_aura)
        {
            case Aura.Halo:
            {
                // A ring of light floating over and behind the head.
                var c = _headC - _headFwd * _headR * 0.35f + Z * _headR * 1.45f;
                var n = (Z * 0.85f - _headFwd * 0.5f).Normalized();
                var a = n.Cross(Y).Normalized();
                var b = n.Cross(a).Normalized();
                var ring = new List<Vector3>();
                for (int k = 0; k <= 24; k++) ring.Add(c + (a * Mathf.Cos(k / 24f * Mathf.Tau) + b * Mathf.Sin(k / 24f * Mathf.Tau)) * _headR * 0.95f);
                _c.Tube(ring, 0.5f * u, 0.5f * u, _sp.Strain == Strain.Clockwork ? Mat.Metal : Mat.Glow, part, 0.1f, -100);
                if (_sp.Strain == Strain.Clockwork)
                    for (int k = 0; k < 8; k++) _c.Sphere(ring[k * 3], 0.8f * u, Mat.Metal, part, 0, -100);
                break;
            }
            case Aura.Shards:
                for (int k = 0; k < 3; k++)
                {
                    var (top, _, _, along) = SpineAt(0.25f + 0.3f * k);
                    var c = top + Z * (5 + 2 * (k % 2)) * u + Y * (k - 1) * 3 * u;
                    var dir = (Z + along * 0.25f * (k - 1)).Normalized();
                    _c.Tube(new[] { c - dir * 2.4f * u, c, c + dir * 2.8f * u }, t => (0.25f + 1.25f * Mathf.Sin(Mathf.Pi * t)) * u, k == 1 ? Mat.Glow : Mat.Accent, part, 0.1f, -100);
                }
                break;
            case Aura.ThirdEye:
                _c.Ellipsoid(H(0.62f, 0, 0.42f), _headFwd, 0.14f * _headR, 0.2f * _headR, 0.26f * _headR, Mat.Glow, _headPart, 0.2f, -100, true);
                break;
            case Aura.Wisps:
            {
                var src = _tailEnd == default ? SpineAt(0.1f).top : _tailEnd;
                for (int k = 0; k < 4; k++)
                {
                    var p = src - X * (2 + 3.5f * k) * u + Z * (4 + 2.5f * k + _rng.Range(-1f, 1f)) * u + Y * _rng.Range(-4f, 4f) * u;
                    _c.Sphere(p, (1.5f - 0.25f * k) * u, Mat.Glow, part, 0.15f, -100, true);
                }
                break;
            }
            // The sigil is painted onto the flank with the other surface marks.
        }
    }

    // ---------------------------------------------------------------- patterns

    void ApplyPattern()
    {
        if (_form == Form.Jelly)
        {
            // Four glowing lobes in the bell, seen through the top.
            var top = _spine.Count > 1 ? _spine[1].p : _headC;
            float rad = _spine.Count > 1 ? _spine[1].w : _headR;
            _c.Recolor((x, y) =>
            {
                if (_c.PartAt(x, y) != _bodyPart || _c.MatAt(x, y) != Mat.Main || _c.Normal3At(x, y).Z < 0.45f) return false;
                var rel = _c.PosAt(x, y) - top;
                float d = new Vector2(rel.X, rel.Y).Length() / rad, a = Mathf.Atan2(rel.Y, rel.X);
                return d > 0.12f && d < 0.62f && Mathf.Cos(4 * a + 0.4f) > 0.3f;
            }, Mat.Glow, -0.1f);
        }
        bool Main(int x, int y) => _c.MatAt(x, y) == Mat.Main;
        float unit = u;
        switch (_p.Pattern)
        {
            case Pattern.Belly:
                _c.Recolor((x, y) =>
                {
                    if (!Main(x, y) || ZoneAt(x, y) is not (Zone.Body or Zone.Tail)) return false;
                    var n = _c.Normal3At(x, y);
                    return _upright ? n.X > 0.35f && n.Z < 0.6f : n.Z < -0.22f;
                }, Mat.Second);
                break;
            case Pattern.Saddle:
                _c.Recolor((x, y) => Main(x, y) && ZoneAt(x, y) == Zone.Body && _c.Normal3At(x, y).Z > 0.55f, Mat.Second);
                break;
            case Pattern.Stripes:
            {
                float w = 1.7f * unit, period = w * 2.7f;
                _c.Recolor((x, y) =>
                {
                    if (!Main(x, y) || ZoneAt(x, y) is not (Zone.Body or Zone.Tail or Zone.Limb) || _c.Normal3At(x, y).Z < -0.3f) return false;
                    var p = _c.PosAt(x, y);
                    float along = _upright ? p.Z : p.X;
                    return Mathf.PosMod(along + (_upright ? p.X : p.Z) * 0.35f, period) < w;
                }, Mat.Second);
                break;
            }
            case Pattern.Spots:
            {
                float cell = 3.6f * unit;
                _c.Recolor((x, y) =>
                {
                    if (!Main(x, y) || ZoneAt(x, y) is not (Zone.Body or Zone.Tail or Zone.Head) || _c.Normal3At(x, y).Z < -0.3f) return false;
                    var p = _c.PosAt(x, y);
                    int gx = Mathf.FloorToInt(p.X / cell), gy = Mathf.FloorToInt(p.Y / cell), gz = Mathf.FloorToInt(p.Z / cell);
                    var h = new SeededRng(SeededRng.Derive(_sp.Seed, (ulong)(uint)(gx * 73856093 ^ gy * 19349663 ^ gz * 83492791)));
                    if (h.Chance(0.4f)) return false;
                    var sc = new Vector3((gx + h.Range(0.3f, 0.7f)) * cell, (gy + h.Range(0.3f, 0.7f)) * cell, (gz + h.Range(0.3f, 0.7f)) * cell);
                    return (p - sc).Length() < cell * h.Range(0.28f, 0.42f);
                }, Mat.Second);
                break;
            }
            case Pattern.Tips:
            {
                // Ear tips, socks, and the end of the tail.
                var tops = new Dictionary<int, float>();
                for (int y = 0; y < _c.H; y++)
                for (int x = 0; x < _c.W; x++)
                    if (ZoneAt(x, y) == Zone.Ear) { int pt = _c.PartAt(x, y); tops[pt] = Mathf.Max(tops.TryGetValue(pt, out var t) ? t : float.MinValue, _c.PosAt(x, y).Z); }
                float tailLen = (_tailEnd - _tailRoot).Length();
                _c.Recolor((x, y) => Main(x, y) && ZoneAt(x, y) switch
                {
                    Zone.Ear => _c.PosAt(x, y).Z > tops[_c.PartAt(x, y)] - _headR * 0.4f,
                    Zone.Limb => _c.PosAt(x, y).Z < 3.2f * unit + _feet.Select(f => f.p.Z).DefaultIfEmpty(0).Min(),
                    Zone.Tail => tailLen > 1 && (_c.PosAt(x, y) - _tailRoot).Length() > tailLen * 0.68f,
                    _ => false,
                }, Mat.Second);
                break;
            }
        }
    }

    // ---------------------------------------------------------------- face

    /// <summary>Eyes, brows and jaw, painted onto the head where this view can see them.</summary>
    void Face()
    {
        var (hf, hs, hu) = HeadFrame;
        // Which way "toward the snout" runs on screen, so eyes and jaws point the right way.
        int front = _c.Cam.Project(_headC + hf * _headR).X < _c.Cam.Project(_headC).X ? -1 : 1;
        int e = _p.Eyes == EyeKind.Compound ? 0 : Mathf.Clamp(Mathf.RoundToInt(_headR * 0.2f * _sp.EyeSize), 1, 3);

        (int x, int y)? near = null;
        foreach (int side in _bigEye ? Array.Empty<int>() : new[] { 1, -1 })
        {
            var dir = (hf * 0.62f + hu * 0.3f + hs * side * 0.72f).Normalized();
            float k = 1f / Mathf.Sqrt(Sq(dir.Dot(hf) / 0.92f) + Sq(dir.Dot(hs) / 0.74f) + Sq(dir.Dot(hu) / 0.74f));
            var p = _headC + dir * k * _headR;
            if (!_c.Visible(p, out int x, out int y, 2.2f) || _c.PartAt(x, y) != _headPart) continue;
            // The eye nearer the camera is the full almond; the other shows as a glint.
            bool far = dir.Dot(-_c.Cam.D) < 0.35f;
            if (_p.Eyes == EyeKind.Compound) { Glint(x, y); near = (x, y); continue; }
            Eye(x, y, far ? Mathf.Max(1, e - 1) : e, front, far);
            if (!far) near = (x, y);
        }

        if (near is { } n)
        {
            if (Has(Graft.ExtraEyes))
                foreach (var (dx, dy) in new[] { (-front * (e + 2), -e - 1), (-front * 2, -e - 3), (-front * (e + 4), 1) })
                    Glint(n.x + dx, n.y + dy);
            if (_p.Pattern == Pattern.Markings)
                for (int k = 0; k < 2; k++)
                for (int i = 0; i < 3 + e; i++)
                {
                    int x = n.x - front * (e + 2 + i), y = n.y + k * 2 + i / 2;
                    if (_c.MatAt(x, y) == Mat.Main && _c.PartAt(x, y) == _headPart) _c.Plot(x, y, Mat.Second, _headPart);
                }
        }
        if (_aura == Aura.ThirdEye && _c.Visible(H(0.75f, 0, 0.42f), out int tx, out int ty, 2.5f)) _c.Plot(tx, ty, Mat.Pupil, _headPart);
        Jaw(front);
    }

    static float Sq(float v) => v * v;

    bool Skin(int x, int y) => _c.MatAt(x, y) is Mat.Main or Mat.Second or Mat.Glow or Mat.Pupil && _c.PartAt(x, y) == _headPart;

    void Glint(int x, int y)
    {
        if (!Skin(x, y)) return;
        _c.Plot(x, y, Mat.Glow, _headPart, 2);
        if (Skin(x, y - 1) && _c.MatAt(x, y - 1) != Mat.Glow) _c.Plot(x, y - 1, Mat.Line, _headPart);
    }

    void Eye(int ex, int ey, int e, int front, bool far)
    {
        var att = _cr.Attitude;
        void Px(int x, int y, Mat m, int tone = 1) { if (Skin(x, y)) _c.Plot(x, y, m, _headPart, tone); }
        if (_blink)
        {
            // Lids shut: a line where the eye was.
            for (int x = ex - Math.Max(1, e); x <= ex + Math.Max(1, e); x++) Px(x, ey, Mat.Line);
            return;
        }
        int top, half;
        if (far || e <= 1)
        {
            Px(ex, ey, Mat.Glow, 2);
            if (!far) Px(ex + front, ey, Mat.Glow);
            top = ey; half = 1;
        }
        else
        {
            // Almond: widest in the middle, a slit pupil toward the snout.
            int h = att == Attitude.Skittish ? e + 1 : Mathf.Max(2, e);
            half = e;
            top = ey - h / 2;
            for (int r = 0; r < h; r++)
            {
                int w = (r == 0 || r == h - 1) ? half - 1 : half;
                for (int x = ex - w; x <= ex + w; x++) Px(x, top + r, Mat.Glow, r == 0 && (x - ex) * front > 0 ? 2 : 1);
            }
            int px = ex + front * Mathf.Max(1, e / 2);
            if (_p.Eyes == EyeKind.Beady || att == Attitude.Skittish) Px(px, top + h / 2, Mat.Pupil);
            else for (int r = 0; r < h; r++) Px(px, top + r, Mat.Pupil);
        }
        // The brow, shaped by temper. "back" runs 0 at the snout side of the eye to 1 behind it.
        int b0 = top - 1;
        for (int i = -half - 1; i <= half + 1; i++)
        {
            int x = ex + i;
            float back = (half + 1 - i * front) / (2f * half + 2);
            int y = att switch
            {
                Attitude.Aggressive => b0 + Mathf.RoundToInt((1 - back) * (half + 0.5f)),
                Attitude.Proud => b0 + 1,
                Attitude.Skittish => b0 - Mathf.RoundToInt((1 - back) * 1.2f),
                Attitude.Curious => b0 - (Mathf.Abs(i) <= half / 2 ? 1 : 0),
                _ => b0,
            };
            Px(x, y, Mat.Line);
            for (int yy = top - 1; yy < y; yy++)
                if (_c.MatAt(x, yy) is Mat.Glow or Mat.Pupil) _c.Plot(x, yy, Mat.Main, _headPart, 0);
        }
    }

    /// <summary>The jaw line along the near side of the muzzle, with fangs; aggression opens it.</summary>
    void Jaw(int front)
    {
        if (_mouth is not { } m) return;
        var att = _cr.Attitude;
        bool striking = Animate && _pose == Pose.Strike && _phase > 0.35f && _phase < 0.8f;
        int open = att == Attitude.Aggressive || striking ? (_headR >= 5 * u ? 2 : 1) : 0;
        var seen = new List<(int x, int y)>();
        for (int k = 0; k <= 12; k++)
        {
            var p = m.front.Lerp(m.back, k / 12f);
            if (!_c.Visible(p, out int x, out int y, 2.6f) || _c.PartAt(x, y) != _headPart) continue;
            if (seen.Count > 0 && seen[^1] == (x, y)) continue;
            seen.Add((x, y));
        }
        if (seen.Count < 2) return;
        for (int i = 0; i < seen.Count; i++)
        {
            var (x, y) = seen[i];
            if (_c.MatAt(x, y) is Mat.Main or Mat.Second) _c.Plot(x, y, Mat.Line, _headPart);
            if (i == 0 || i >= seen.Count - 1) continue;
            for (int o = 1; o <= open; o++) if (_c.MatAt(x, y + o) is Mat.Main or Mat.Second) _c.Plot(x, y + o, Mat.Mouth, _headPart);
            if (open > 0 && _c.MatAt(x, y + open + 1) is Mat.Main or Mat.Second) _c.Plot(x, y + open + 1, Mat.Line, _headPart);
        }
        // Fangs hang from the front of the upper jaw.
        var (fx, fy) = seen[Math.Min(1, seen.Count - 1)];
        if (_c.MatAt(fx, fy + 1) is not Mat.None) _c.Plot(fx, fy + 1, Mat.EyeWhite, _headPart);
        if (open > 0 && seen.Count > 3) { var (gx, gy) = seen[3]; if (_c.MatAt(gx, gy + 1) is not Mat.None) _c.Plot(gx, gy + 1, Mat.EyeWhite, _headPart); }
        if (Has(Graft.Drips))
        {
            foreach (var (i, n) in new[] { (1, 2), (Math.Min(3, seen.Count - 1), 3) })
            {
                var (x, y) = seen[i];
                for (int k = 0; k < n; k++) _c.Plot(x, y + open + 2 + k, Mat.Glow, _headPart, k == n - 1 ? 2 : 1);
            }
        }
    }

    // ---------------------------------------------------------------- surface grafts

    /// <summary>Grafts worked into the visible surface: veins, cracks, runes, burning tips, and the sigil aura.</summary>
    void SurfaceGrafts()
    {
        var rng = new SeededRng(SeededRng.Derive(_sp.Seed, 0x6EAF));
        bool Body(int x, int y) => _c.MatAt(x, y) is Mat.Main or Mat.Second && ZoneAt(x, y) is Zone.Body or Zone.Tail or Zone.Limb;
        var mid = _c.Cam.Project(TorsoMid);
        float span = Mathf.Max(6, S * 0.2f);

        if (Has(Graft.Veins) || Has(Graft.Cracks))
        {
            bool cracks = Has(Graft.Cracks);
            for (int k = 0; k < (cracks ? 3 : 4); k++)
            {
                float x = mid.X + rng.Range(-0.6f, 0.6f) * span, y = mid.Y + rng.Range(-0.4f, 0.5f) * span * 0.6f;
                int steps = Mathf.RoundToInt(S * (cracks ? 0.18f : 0.22f));
                int dir = rng.Chance(0.5f) ? 1 : -1;
                for (int s = 0; s < steps; s++)
                {
                    int px = Mathf.FloorToInt(x), py = Mathf.FloorToInt(y);
                    if (Body(px, py)) _c.Plot(px, py, Mat.Glow, _c.PartAt(px, py), cracks && s % 3 == 0 ? 2 : 1);
                    x += dir * (rng.Chance(0.75f) ? 1 : 0);
                    y += cracks ? (s % 2 == 0 ? 1 : -1) * (rng.Chance(0.6f) ? 1 : 0) : rng.Range(-0.8f, 0.8f);
                }
            }
        }
        if (Has(Graft.Runes))
        {
            var glyphs = new (int, int)[][]
            {
                new[] { (0, -1), (0, 0), (0, 1), (-1, 0), (1, 0) },
                new[] { (-1, -1), (0, 0), (1, 1), (1, -1), (-1, 1) },
                new[] { (0, -1), (-1, 1), (0, 1), (1, 1) },
                new[] { (-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1) },
            };
            int count = S >= 50 ? 3 : 2;
            for (int k = 0; k < count; k++)
            {
                var (top, _, h, _) = SpineAt(0.25f + 0.5f * k / Math.Max(1, count - 1));
                var at = _c.Cam.Project(top - Z * h * 0.9f - Y * SpineAt(0.5f).w * 0.7f);
                int gx = Mathf.FloorToInt(at.X), gy = Mathf.FloorToInt(at.Y);
                foreach (var (dx, dy) in glyphs[(k + (int)(_sp.Seed % 4)) % glyphs.Length])
                    if (Body(gx + dx, gy + dy)) _c.Plot(gx + dx, gy + dy, Mat.Glow, _c.PartAt(gx + dx, gy + dy));
            }
        }
        if (_aura == Aura.Sigil && _spine.Count > 0)
        {
            // A glowing circle and cross on the near flank, wrapped onto the body.
            var (top, w, h, along) = SpineAt(0.5f);
            var c = top - Z * h * 0.95f - Y * w * 0.8f;
            float rad = Mathf.Max(2.2f, Mathf.Min(w, h) * 0.65f);
            for (int k = 0; k < 28; k++)
            {
                float a = k / 28f * Mathf.Tau;
                var p = c + (along * Mathf.Cos(a) + Z * Mathf.Sin(a)) * rad - Y * 0.6f * u;
                if (_c.Visible(p, out int x, out int y, 3f) && Body(x, y)) _c.Plot(x, y, Mat.Glow, _c.PartAt(x, y), 2);
            }
            for (int k = -3; k <= 3; k++)
                foreach (var d in new[] { along, Z })
                {
                    var p = c + d * rad * k / 4.5f - Y * 0.6f * u;
                    if (_c.Visible(p, out int x, out int y, 3f) && Body(x, y)) _c.Plot(x, y, Mat.Glow, _c.PartAt(x, y));
                }
        }
        if (Has(Graft.GlowTips))
        {
            // The top third of every horn, spike, claw and crystal burns.
            var span3 = new Dictionary<int, (float min, float max)>();
            for (int y = 0; y < _c.H; y++)
            for (int x = 0; x < _c.W; x++)
            {
                if (_c.MatAt(x, y) == Mat.None || ZoneAt(x, y) != Zone.Tip) continue;
                int part = _c.PartAt(x, y);
                float z = _c.PosAt(x, y).Z;
                span3[part] = span3.TryGetValue(part, out var t) ? (Mathf.Min(t.min, z), Mathf.Max(t.max, z)) : (z, z);
            }
            _c.Recolor((x, y) => _c.MatAt(x, y) is Mat.Accent or Mat.Second && span3.TryGetValue(_c.PartAt(x, y), out var t)
                && _c.PosAt(x, y).Z >= t.max - (t.max - t.min) * 0.35f, Mat.Glow);
        }
    }
}
