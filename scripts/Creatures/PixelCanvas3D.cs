using System;
using System.Collections.Generic;
using Godot;

namespace CreatureTactics.Creatures;

/// <summary>Which way a creature faces on screen. The other two diagonals are these two, mirrored.</summary>
public enum View { Front, Back }

/// <summary>What a creature is doing: breathing, walking, striking, casting, flinching, falling.</summary>
public enum Pose { Idle, Walk, Strike, Cast, Hurt, Faint }

/// <summary>
/// FFTA's camera: 30 degrees above the board, looking down a grid diagonal. Creature space is X
/// forward, Y to its left, Z up, in sprite pixels, with the ground at Z = 0. In the front view the
/// creature faces down-left on screen; in the back view, up-left. Both show its right flank.
/// </summary>
public readonly struct Camera3
{
    public readonly Vector3 R, U, D, L;   // screen right, screen up, view direction, toward the light
    public readonly Vector2 Origin;       // where creature-space (0, 0, 0) lands on the canvas
    const float K = 0.70710678f, Sn = 0.5f, Cs = 0.8660254f;

    public Camera3(View view, Vector2 origin)
    {
        Origin = origin;
        if (view == View.Front) { R = new(-K, -K, 0); U = new(-Sn * K, Sn * K, Cs); D = new(-Cs * K, Cs * K, -Sn); }
        else { R = new(-K, K, 0); U = new(Sn * K, Sn * K, Cs); D = new(Cs * K, Cs * K, -Sn); }
        // Light from the upper left of the screen, a little in front: the same light as the flat painter.
        L = (-0.55f * R + 0.75f * U - 0.45f * D).Normalized();
    }

    public Vector2 Project(Vector3 p) => Origin + new Vector2(p.Dot(R), -p.Dot(U));
    public float Depth(Vector3 p) => p.Dot(D);
    /// <summary>The point at depth 0 under a canvas position; the view ray runs along D from it.</summary>
    public Vector3 Ray(float x, float y) => (x - Origin.X) * R - (y - Origin.Y) * U;
}

/// <summary>
/// Painting in 3D: shapes are ray-cast from the camera into the same pixel grid, keeping the nearest
/// surface per pixel (a depth buffer) with its creature-space position and normal. Shading, tones,
/// outlines and rim light then resolve exactly as for flat painting; seams fall where one part sits
/// clearly in front of another.
/// </summary>
public sealed partial class PixelCanvas
{
    float[] _depth;
    Vector3[] _pos, _norm;
    public Camera3 Cam { get; private set; }
    const float SeamDepth = 2.2f;

    /// <summary>
    /// Moves shapes for one frame of an animation (a breath, a sway, a wingbeat): applied to every
    /// ellipsoid's centre, so to every sphere of a tube, and to every corner of a membrane. Positions
    /// kept for patterns are moved back, so markings ride the body instead of swimming over it.
    /// </summary>
    public Func<Vector3, Vector3> Warp = NoWarp;
    public static Vector3 NoWarp(Vector3 p) => p;

    public void Begin3D(Camera3 cam)
    {
        Cam = cam;
        _depth = new float[W * H];
        Array.Fill(_depth, float.MaxValue);
        _pos = new Vector3[W * H];
        _norm = new Vector3[W * H];
    }

    public Vector3 PosAt(int x, int y) => Inside(x, y) && _pos != null ? _pos[y * W + x] : Vector3.Zero;
    public Vector3 Normal3At(int x, int y) => Inside(x, y) && _norm != null ? _norm[y * W + x] : Vector3.Zero;

    void Set3(int i, Mat m, float t, Vector3 p, Vector3 n, int part, float bias, bool flat)
    {
        _depth[i] = t; _pos[i] = p * 2 - Warp(p); _norm[i] = n;
        _mat[i] = m; _part[i] = part;
        _shade[i] = (flat ? 0.55f : n.Dot(Cam.L)) + bias;
        _nx[i] = n.Dot(Cam.R); _ny[i] = -n.Dot(Cam.U);
    }

    /// <summary>An ellipsoid with axes ax, ay, az (unit, perpendicular) and radii r. Nothing below <paramref name="floor"/> shows.</summary>
    public void Ellipsoid(Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, Vector3 r, Mat m, int part, float bias = 0, float floor = 0, bool flat = false)
    {
        r = new Vector3(Mathf.Max(r.X, 0.45f), Mathf.Max(r.Y, 0.45f), Mathf.Max(r.Z, 0.45f));
        var shift = Warp(c) - c;
        c += shift;
        floor += shift.Z;
        float ext = Mathf.Max(r.X, Mathf.Max(r.Y, r.Z)) + 1;
        var sc = Cam.Project(c);
        var d = Cam.D;
        var ld = new Vector3(d.Dot(ax) / r.X, d.Dot(ay) / r.Y, d.Dot(az) / r.Z);
        float a = ld.Dot(ld);
        for (int y = Mathf.FloorToInt(sc.Y - ext); y <= Mathf.CeilToInt(sc.Y + ext); y++)
        for (int x = Mathf.FloorToInt(sc.X - ext); x <= Mathf.CeilToInt(sc.X + ext); x++)
        {
            if (!Inside(x, y)) continue;
            var ray = Cam.Ray(x + 0.5f, y + 0.5f);
            var o = ray - c;
            var lo = new Vector3(o.Dot(ax) / r.X, o.Dot(ay) / r.Y, o.Dot(az) / r.Z);
            float b = lo.Dot(ld), cc = lo.Dot(lo) - 1, disc = b * b - a * cc;
            if (disc < 0) continue;
            float t = (-b - Mathf.Sqrt(disc)) / a;
            int i = y * W + x;
            if (t >= _depth[i]) continue;
            var p = ray + d * t;
            if (p.Z < floor) continue;
            var q = lo + ld * t;
            var n = (ax * (q.X / r.X) + ay * (q.Y / r.Y) + az * (q.Z / r.Z)).Normalized();
            Set3(i, m, t, p, n, part, bias, flat);
        }
    }

    /// <summary>An ellipsoid stretched along <paramref name="fwd"/>: rl along it, rw across, rh up.</summary>
    public void Ellipsoid(Vector3 c, Vector3 fwd, float rl, float rw, float rh, Mat m, int part, float bias = 0, float floor = 0, bool flat = false)
    {
        var (ax, ay, az) = Frame(fwd);
        Ellipsoid(c, ax, ay, az, new Vector3(rl, rw, rh), m, part, bias, floor, flat);
    }

    public void Sphere(Vector3 c, float r, Mat m, int part, float bias = 0, float floor = 0, bool flat = false) =>
        Ellipsoid(c, new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1), new Vector3(r, r, r), m, part, bias, floor, flat);

    /// <summary>A right-handed frame whose first axis is <paramref name="fwd"/> and whose third leans up.</summary>
    public static (Vector3 ax, Vector3 ay, Vector3 az) Frame(Vector3 fwd)
    {
        var ax = fwd.Normalized();
        var up = new Vector3(0, 0, 1);
        var ay = up.Cross(ax);
        ay = ay.LengthSquared() < 1e-4f ? new Vector3(0, 1, 0) : ay.Normalized();
        return (ax, ay, ax.Cross(ay));
    }

    /// <summary>
    /// A tube through points, swept as overlapping spheres every half pixel, with the radius given
    /// along its length (0 at the first point, 1 at the last): limbs, necks, tails, horns.
    /// </summary>
    public void Tube(IReadOnlyList<Vector3> pts, Func<float, float> radius, Mat m, int part, float bias = 0, float floor = 0, bool flat = false)
    {
        float total = 0;
        for (int i = 1; i < pts.Count; i++) total += (pts[i] - pts[i - 1]).Length();
        if (total < 1e-3f) { Sphere(pts[0], radius(0), m, part, bias, floor, flat); return; }
        float run = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1];
            var ab = pts[i] - a;
            float len = ab.Length();
            int n = Math.Max(1, Mathf.CeilToInt(len / 0.45f));
            for (int k = i == 1 ? 0 : 1; k <= n; k++)
            {
                float f = k / (float)n;
                Sphere(a + ab * f, Mathf.Max(0.45f, radius((run + len * f) / total)), m, part, bias, floor, flat);
            }
            run += len;
        }
    }

    public void Tube(IReadOnlyList<Vector3> pts, float r0, float r1, Mat m, int part, float bias = 0, float floor = 0) =>
        Tube(pts, t => Mathf.Lerp(r0, r1, t), m, part, bias, floor);

    /// <summary>
    /// A flat polygon in space (wing membranes, fins, sails, crests), lit by its own facing and a
    /// little flatter than a solid. Edge-on, it shrinks to its rim.
    /// </summary>
    public void Membrane(IReadOnlyList<Vector3> pts, Mat m, int part, float bias = 0, float floor = 0)
    {
        if (pts.Count < 3) return;
        var flatPts = pts;
        var moved = new Vector3[pts.Count];
        for (int k = 0; k < pts.Count; k++) moved[k] = Warp(pts[k]);
        pts = moved;
        // Newell's normal copes with polygons that aren't quite flat.
        var nrm = Vector3.Zero;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Count];
            nrm += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }
        if (nrm.LengthSquared() < 1e-6f) return;
        nrm = nrm.Normalized();
        var v0 = pts[0];
        float denom = Cam.D.Dot(nrm);
        if (Mathf.Abs(denom) < 0.12f)
        {
            var rim = new List<Vector3>(flatPts) { flatPts[0] };
            Tube(rim, 0.5f, 0.5f, m, part, bias, floor);
            return;
        }
        var e1 = (pts[1] - v0).Normalized();
        var e2 = nrm.Cross(e1);
        var poly = new Vector2[pts.Count];
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < pts.Count; i++)
        {
            poly[i] = new Vector2((pts[i] - v0).Dot(e1), (pts[i] - v0).Dot(e2));
            var s = Cam.Project(pts[i]);
            minX = Mathf.Min(minX, s.X); maxX = Mathf.Max(maxX, s.X);
            minY = Mathf.Min(minY, s.Y); maxY = Mathf.Max(maxY, s.Y);
        }
        var facing = denom > 0 ? -nrm : nrm;
        for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
        for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
        {
            if (!Inside(x, y)) continue;
            var ray = Cam.Ray(x + 0.5f, y + 0.5f);
            float t = (v0 - ray).Dot(nrm) / denom;
            int i = y * W + x;
            if (t >= _depth[i]) continue;
            var p = ray + Cam.D * t;
            if (p.Z < floor) continue;
            var l = new Vector2((p - v0).Dot(e1), (p - v0).Dot(e2));
            bool inside = false;
            for (int a = 0, b = poly.Length - 1; a < poly.Length; b = a++)
                if ((poly[a].Y > l.Y) != (poly[b].Y > l.Y) && l.X < (poly[b].X - poly[a].X) * (l.Y - poly[a].Y) / (poly[b].Y - poly[a].Y) + poly[a].X)
                    inside = !inside;
            if (!inside) continue;
            Set3(i, m, t, p, facing, part, bias, false);
            _shade[i] = 0.3f + 0.55f * Mathf.Max(0, facing.Dot(Cam.L)) + bias;
        }
    }

    /// <summary>
    /// Where a point on a surface lands on the canvas, if it isn't hidden: the nearest surface at
    /// that pixel is no more than <paramref name="tolerance"/> in front of it.
    /// </summary>
    public bool Visible(Vector3 p, out int x, out int y, float tolerance = 1.6f)
    {
        p = Warp(p);
        var s = Cam.Project(p);
        x = Mathf.FloorToInt(s.X); y = Mathf.FloorToInt(s.Y);
        if (!Inside(x, y) || _depth == null) return false;
        int i = y * W + x;
        return _mat[i] != Mat.None && Cam.Depth(p) <= _depth[i] + tolerance;
    }

    /// <summary>The bounding box of everything painted, or an empty rect.</summary>
    public Rect2I Used()
    {
        int x0 = W, y0 = H, x1 = -1, y1 = -1;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            if (_mat[y * W + x] == Mat.None) continue;
            x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
        }
        return x1 < 0 ? new Rect2I() : new Rect2I(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>Seams: in 3D, a neighbouring pixel of another part that sits clearly nearer the camera.</summary>
    bool InFront(int j, int i, int part) =>
        _depth == null ? _part[j] > part : _part[j] != part && _depth[j] < _depth[i] - SeamDepth;
}
