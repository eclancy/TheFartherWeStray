using System;
using System.Collections.Generic;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Tactics;

/// <summary>Things standing on a map tile: foliage, rocks, street furniture, ruins. Most block the tile.</summary>
public enum Prop
{
    Tree, Pine, DeadTree, Bush, Mushroom, Crystals, Rock, Lamp, Neon, Crate, Barrel, Tank, Gears,
    Statue, Fountain, Pillar, Stall, Banner, Reeds, Grave, Hedge, Chimney,
    // The undercity's grime (the user's ask, 2026-11-02): a streetlight that flickers, a hydrant
    // leaking across the street, a grate puffing steam. They move (PropFx).
    BrokenLamp, Hydrant, Vent,
    // The home base's stations (the user's ask for a base you walk around, 2026-11-02), its fences
    // and gate, and the scaffolding that stands where a station hasn't opened yet.
    Vat, LabBench, Dummy, Rack, WarTable, Fence, FenceY, FenceCorner, Gate, Trough, Scaffold,
}

/// <summary>
/// Paints props the way creatures are painted: a small 3D model ray-cast from the board's camera,
/// shaded in four tones, outlined, in a palette chosen for the map's region (autumn trees in the
/// ruins, neon lamps in the undercity, gilded ones in the city above). Each tile's prop gets a
/// variant from its position, so two trees on one map differ.
/// </summary>
public static class PropArt
{
    /// <summary>Props you can walk through: everything else fills its tile.</summary>
    public static bool Blocks(Prop p) => p is not (Prop.Reeds or Prop.Banner or Prop.Vent);

    public static Prop? FromChar(char c) => c switch
    {
        'T' => Prop.Tree, 'P' => Prop.Pine, 'D' => Prop.DeadTree, 'u' => Prop.Bush, 'M' => Prop.Mushroom,
        'C' => Prop.Crystals, 'o' => Prop.Rock, 'L' => Prop.Lamp, 'N' => Prop.Neon, 'c' => Prop.Crate,
        'B' => Prop.Barrel, 'K' => Prop.Tank, 'G' => Prop.Gears, 'S' => Prop.Statue, 'F' => Prop.Fountain,
        'I' => Prop.Pillar, 'm' => Prop.Stall, 'f' => Prop.Banner, 'r' => Prop.Reeds, 'g' => Prop.Grave,
        'e' => Prop.Hedge, 'H' => Prop.Chimney, 'l' => Prop.BrokenLamp, 'y' => Prop.Hydrant, 'v' => Prop.Vent,
        'V' => Prop.Vat, 'Q' => Prop.LabBench, 'd' => Prop.Dummy, 'R' => Prop.Rack, 'W' => Prop.WarTable,
        'n' => Prop.Fence, 'k' => Prop.FenceY, '+' => Prop.FenceCorner, 'O' => Prop.Gate, 'q' => Prop.Trough, 'Z' => Prop.Scaffold,
        _ => null,
    };

    static readonly Dictionary<(Prop, int, string, bool), (Texture2D tex, Vector2I anchor)> Cache = new();

    /// <summary>The prop's sprite; <paramref name="lit"/> false draws its lights dark (a flicker's off beat).</summary>
    public static (Texture2D tex, Vector2I anchor) Texture(Prop p, int variant, string theme, bool lit = true)
    {
        variant = Math.Abs(variant) % 4;
        if (Cache.TryGetValue((p, variant, theme, lit), out var hit)) return hit;
        var (img, anchor) = Paint(p, variant, theme, lit);
        return Cache[(p, variant, theme, lit)] = (ImageTexture.CreateFromImage(img), anchor);
    }

    /// <summary>The size a prop is built at, by variant (a little variation; a tree stands about two tiles tall).</summary>
    static float Scale(int variant) => (0.88f + 0.08f * (Math.Abs(variant) % 4)) * 1.4f;

    /// <summary>Where a point on a prop's model lands on the board, from the tile centre it stands on.</summary>
    public static Vector2 Spot(int variant, Vector3 model) => new Camera3(View.Front, Vector2.Zero).Project(model * Scale(variant));

    /// <summary>
    /// Puts a map's props on a board: each a sprite standing on its tile (sorted with the creatures
    /// so a tree hides what's behind it), and the moving ones their effects. Battles and cutscenes share it.
    /// </summary>
    public static void Place(Node2D board, BattleMap map, IsoArt art)
    {
        foreach (var (tile, prop) in map.Props)
        {
            int variant = tile.X * 7 + tile.Y * 3;
            var (tex, anchor) = Texture(prop, variant, map.Theme);
            var at = art.TileCenter(tile, map.HeightAt(tile));
            int z = 100 + (tile.X + tile.Y) * 4 + 1;
            var sprite = new Sprite2D { Texture = tex, Centered = false, Position = at - anchor + new Vector2(0, 1), ZIndex = z };
            board.AddChild(sprite);
            if (PropFx.Moves(prop, map.Theme))
                board.AddChild(new PropFx { Prop = prop, Variant = variant, Theme = map.Theme, Sprite = sprite, Seed = (ulong)(tile.X * 131 + tile.Y * 17 + 7), Position = at, ZIndex = z + 1 });
        }
    }

    /// <summary>The prop's sprite and where its foot (the tile centre) lands in it.</summary>
    public static (Image img, Vector2I anchor) Paint(Prop p, int variant, string theme, bool lit = true)
    {
        const int size = 120;
        var c = new PixelCanvas(size, size);
        c.Begin3D(new Camera3(View.Front, new Vector2(size / 2f, size * 0.78f)));
        var rng = new SeededRng(SeededRng.Derive((ulong)p * 977 + 13, (ulong)variant));
        float k = Scale(variant);
        Build(c, p, variant, k, rng, theme, lit);
        var img = c.Resolve(PaletteFor(p, variant, theme), false);
        var used = img.GetUsedRect();
        var origin = c.Cam.Origin;
        if (used.Size.X == 0) return (img, new Vector2I((int)origin.X, (int)origin.Y));
        return (img.GetRegion(used), new Vector2I(Mathf.RoundToInt(origin.X) - used.Position.X, Mathf.RoundToInt(origin.Y) - used.Position.Y));
    }

    static readonly Vector3 X = new(1, 0, 0), Y = new(0, 1, 0), Z = new(0, 0, 1);

    // Where the moving props' effects come from, on the model (see Spot).
    public static readonly Vector3 LampBulb = new(4.6f, 1.2f, 18.6f), HydrantLeak = new(0, -3.1f, 5.2f), VentMouth = new(0, 0, 0.8f), NeonPanel = new(0.5f, 0, 18);

    static void Build(PixelCanvas c, Prop p, int variant, float k, SeededRng rng, string theme, bool lit)
    {
        var glow = lit ? Mat.Glow : Mat.Membrane;
        int part = c.NewPart();
        int P() => c.NewPart();
        Vector3 V(float x, float y, float z) => new(x * k, y * k, z * k);
        switch (p)
        {
            case Prop.Tree:
                c.Tube(new[] { V(0, 0, 0), V(0.6f, 0, 10), V(0, 0.4f, 19) }, 2.8f * k, 1.8f * k, Mat.Second, part);
                foreach (var (o, r) in new[] { (V(0, 0, 25), 10f), (V(-4, 3, 21.5f), 7f), (V(4, -3, 22.5f), 7f), (V(-1, -1, 31), 6.5f), (V(3, 3, 27), 6f) })
                    c.Ellipsoid(o, X, r * k, r * k, r * 0.8f * k, Mat.Main, P());
                break;
            case Prop.Pine:
                c.Tube(new[] { V(0, 0, 0), V(0, 0, 9) }, 2.2f * k, 1.6f * k, Mat.Second, part);
                for (int t = 0; t < 4; t++)
                    c.Ellipsoid(V(0, 0, 9 + t * 6), X, (9.5f - t * 2.1f) * k, (9.5f - t * 2.1f) * k, 4.2f * k, Mat.Main, P(), t * 0.04f);
                break;
            case Prop.DeadTree:
                c.Tube(new[] { V(0, 0, 0), V(1, 0, 10), V(-0.5f, 0.5f, 20), V(0.5f, 0, 26) }, 2.6f * k, 0.8f * k, Mat.Second, part);
                foreach (var (from, to) in new[] { (V(0.8f, 0, 12), V(7, -3, 20)), (V(-0.3f, 0.4f, 17), V(-6, 4, 24)), (V(0, 0, 21), V(4, 4, 27)) })
                    c.Tube(new[] { from, from.Lerp(to, 0.5f) + V(0, 0, 1.5f), to }, 1.3f * k, 0.4f * k, Mat.Second, part);
                break;
            case Prop.Bush:
                foreach (var (o, r) in new[] { (V(0, 0, 4), 6f), (V(-3.5f, 2.5f, 3.2f), 4.5f), (V(3.5f, -2, 3.4f), 4.5f) })
                    c.Ellipsoid(o, X, r * k, r * k, r * 0.75f * k, Mat.Main, P());
                for (int b = 0; b < 4; b++)
                    c.Sphere(V(rng.Range(-4f, 4f), rng.Range(-4f, 2f), 6 + rng.Range(-1f, 1.5f)), 0.9f * k, Mat.Accent, part, 0.1f);
                break;
            case Prop.Mushroom:
                foreach (var (o, s) in variant % 2 == 0 ? new[] { (V(0, 0, 0), 1f), (V(5, -4, 0), 0.5f) } : new[] { (V(0, 0, 0), 1.1f) })
                {
                    int m = P();
                    c.Tube(new[] { o, o + V(0.5f, 0, 6 * s), o + V(0, 0, 11 * s) }, 2.4f * s * k, 1.7f * s * k, Mat.Second, m);
                    var cap = o + V(0, 0, 10.5f * s);
                    c.Ellipsoid(cap, X, 8 * s * k, 8 * s * k, 4 * s * k, Mat.Main, m, 0, cap.Z - 0.6f * k);
                    for (int d = 0; d < 5; d++)
                    {
                        float a = d / 5f * Mathf.Tau + 0.4f;
                        c.Sphere(cap + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 5 * s * k + Z * 2.4f * s * k, 1.0f * s * k, Mat.Glow, m, 0.15f, 0, true);
                    }
                }
                break;
            case Prop.Crystals:
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.Tau + variant;
                    var dir = (new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (i == 0 ? 0.1f : 0.45f) + Z).Normalized();
                    float len = (i == 0 ? 17 : rng.Range(8f, 13f)) * k;
                    var b = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (i == 0 ? 0 : 2.5f) * k;
                    c.Tube(new[] { b, b + dir * len }, t => (2.8f - 2.5f * t * t) * k * (i == 0 ? 1.2f : 0.9f), Mat.Accent, P(), 0.08f);
                }
                c.Sphere(V(0, 0, 3), 2.2f * k, Mat.Glow, part, 0.2f, 0, true);
                break;
            case Prop.Rock:
                c.Ellipsoid(V(0, 0, 3), new Vector3(1, 0.3f, 0), 7 * k, 5.5f * k, 4.6f * k, Mat.Accent, part);
                c.Ellipsoid(V(4, 3, 2), new Vector3(0.4f, 1, 0), 4 * k, 3.2f * k, 2.8f * k, Mat.Accent, P(), -0.05f);
                break;
            case Prop.Lamp:
            {
                c.Box(V(0, 0, 1), V(1.8f, 1.8f, 1), Mat.Metal, part);
                c.Tube(new[] { V(0, 0, 1), V(0, 0, 25) }, 0.85f * k, 0.7f * k, Mat.Metal, part);
                c.Tube(new[] { V(0, 0, 24), V(2.5f, 0, 25), V(4, 0, 24) }, 0.6f * k, 0.6f * k, Mat.Metal, part);
                var lamp = V(4, 0, 21.5f);
                c.Sphere(lamp, 2.0f * k, Mat.Glow, P(), 0.2f, 0, true);
                c.Box(lamp + V(0, 0, 2.4f), V(2.2f, 2.2f, 0.6f), Mat.Metal, part);
                break;
            }
            case Prop.Neon:
            {
                c.Tube(new[] { V(0, -3, 0), V(0, -3, 22) }, 0.7f * k, 0.7f * k, Mat.Metal, part);
                c.Tube(new[] { V(0, 3, 0), V(0, 3, 22) }, 0.7f * k, 0.7f * k, Mat.Metal, part);
                c.Box(V(0, 0, 18), V(0.8f, 5.5f, 3.6f), Mat.Metal, P());
                c.Box(V(0.5f, 0, 18), V(0.6f, 4.6f, 2.7f), glow, P(), 0.1f, lit);
                break;
            }
            case Prop.BrokenLamp:
            {
                // Knocked crooked: the pole bends, the arm droops, the hood hangs cracked, a wire dangles.
                c.Box(V(0, 0, 1), V(1.8f, 1.8f, 1), Mat.Metal, part);
                c.Tube(new[] { V(0, 0, 1), V(0, 0, 13), V(1.2f, 0.6f, 20), V(1.6f, 0.8f, 23) }, 0.85f * k, 0.7f * k, Mat.Metal, part);
                c.Tube(new[] { V(1.6f, 0.8f, 22.5f), V(3.4f, 1.0f, 22.8f), V(4.6f, 1.2f, 21.2f) }, 0.6f * k, 0.6f * k, Mat.Metal, part);
                var bulb = LampBulb * k;
                c.Sphere(bulb, 1.6f * k, glow, P(), 0.2f, 0, lit);
                c.Ellipsoid(bulb + V(-0.3f, 0, 2.0f), new Vector3(0.9f, 0.2f, -0.4f), 2.6f * k, 2.3f * k, 0.8f * k, Mat.Metal, part);
                c.Tube(new[] { V(1.2f, 0.6f, 19), V(2.2f, -0.8f, 15.5f), V(1.6f, -1.4f, 12.5f) }, 0.3f * k, 0.3f * k, Mat.Second, P());
                break;
            }
            case Prop.Hydrant:
            {
                // A squat street hydrant, its side cap knocked off (the leak is PropFx's).
                c.Box(V(0, 0, 0.6f), V(2.8f, 2.8f, 0.6f), Mat.Metal, part, -0.1f);
                c.Tube(new[] { V(0, 0, 0.6f), V(0, 0, 7.4f) }, 2.3f * k, 2.1f * k, Mat.Main, part);
                c.Sphere(V(0, 0, 7.4f), 2.15f * k, Mat.Main, part);
                c.Box(V(0, 0, 9.6f), V(0.8f, 0.8f, 0.7f), Mat.Metal, P(), 0.1f);
                c.Tube(new[] { V(1.6f, 0, 5), V(3.4f, 0, 5) }, 1.2f * k, 1.2f * k, Mat.Main, P());
                c.Box(V(3.6f, 0, 5), V(0.4f, 1.3f, 1.3f), Mat.Metal, P(), 0.2f);
                c.Tube(new[] { V(0, 1.6f, 5.2f), V(0, 3.2f, 5.2f) }, 0.9f * k, 0.9f * k, Mat.Main, P());
                c.Tube(new[] { V(0, -1.6f, 5.2f), V(0, -3.0f, 5.2f) }, 0.9f * k, 0.75f * k, Mat.Main, P(), -0.15f);
                c.Tube(new[] { V(1.2f, -1.4f, 6.5f), V(2.2f, -2.4f, 4.4f), V(1.4f, -2.8f, 3.8f) }, 0.35f * k, 0.35f * k, Mat.Metal, P(), 0.2f);
                break;
            }
            case Prop.Vat:
            {
                // A breeding vat: glowing fluid in a brass-ringed glass, a pump on its cap.
                c.Box(V(0, 0, 0.8f), V(3.8f, 3.8f, 0.8f), Mat.Metal, part);
                c.Tube(new[] { V(0, 0, 1.6f), V(0, 0, 13) }, 3.1f * k, 3.1f * k, glow, P(), 0.15f, 0);
                foreach (float z in new[] { 1.8f, 7.3f, 12.8f })
                {
                    var ring = new List<Vector3>();
                    for (int i = 0; i <= 16; i++) ring.Add(V(Mathf.Cos(i / 16f * Mathf.Tau) * 3.2f, Mathf.Sin(i / 16f * Mathf.Tau) * 3.2f, z));
                    c.Tube(ring, 0.5f * k, 0.5f * k, Mat.Metal, part);
                }
                c.Sphere(V(0, 0, 13.4f), 3.2f * k, Mat.Metal, part, 0, 13.2f * k);
                c.Tube(new[] { V(0, 0, 15), V(0, 0, 18), V(2.5f, -1.5f, 18.5f), V(3.6f, -2.4f, 14) }, 0.6f * k, 0.6f * k, Mat.Metal, P());
                break;
            }
            case Prop.LabBench:
            {
                // A workbench of flasks and wires, a glowing crystal, a brass cog.
                c.Box(V(0, 0, 6.2f), V(4.4f, 3.0f, 0.5f), Mat.Second, part);
                foreach (var (x, y) in new[] { (-3.8f, -2.4f), (3.8f, -2.4f), (-3.8f, 2.4f), (3.8f, 2.4f) })
                    c.Tube(new[] { V(x, y, 0), V(x, y, 6) }, 0.5f * k, 0.5f * k, Mat.Second, part);
                c.Sphere(V(-2, -0.8f, 8.2f), 1.5f * k, glow, P(), 0.2f, 0, lit);
                c.Tube(new[] { V(-2, -0.8f, 9.4f), V(-2, -0.8f, 11) }, 0.5f * k, 0.5f * k, Mat.Membrane, P());
                c.Tube(new[] { V(0.6f, 1.0f, 6.7f), V(0.6f, 1.0f, 10.5f) }, 0.9f * k, 0.6f * k, Mat.Membrane, P(), 0.2f);
                c.Ellipsoid(V(2.6f, -0.6f, 8.2f), new Vector3(0.3f, 0.2f, 1), 2.0f * k, 0.9f * k, 0.9f * k, Mat.Accent, P(), 0.2f);
                c.Tube(new[] { V(-3, 1.6f, 6.7f), V(-1, 2.6f, 7.4f), V(2.4f, 1.8f, 6.7f) }, 0.3f * k, 0.3f * k, Mat.Metal, P());
                break;
            }
            case Prop.Dummy:
            {
                // A training dummy: a straw body on a post, a crossbar for arms, a sack head.
                c.Tube(new[] { V(0, 0, 0), V(0, 0, 13) }, 0.7f * k, 0.7f * k, Mat.Second, part);
                c.Ellipsoid(V(0, 0, 9.5f), new Vector3(1, 0, 0), 2.4f * k, 2.6f * k, 3.6f * k, Mat.Main, P());
                c.Tube(new[] { V(0, -4.4f, 11), V(0, 4.4f, 11) }, 0.6f * k, 0.6f * k, Mat.Second, P());
                c.Sphere(V(0.3f, 0, 15), 2.0f * k, Mat.Main, P());
                c.Tube(new[] { V(0, 0, 8), V(1.8f, -1.6f, 9.6f) }, 0.25f * k, 0.25f * k, Mat.Accent, P(), 0.2f);
                break;
            }
            case Prop.Rack:
            {
                // An armory: a weapon rack of blades and a spear, an anvil before it.
                foreach (float y in new[] { -3.6f, 3.6f }) c.Tube(new[] { V(-1.4f, y, 0), V(-1.4f, y, 12) }, 0.6f * k, 0.6f * k, Mat.Second, part);
                c.Tube(new[] { V(-1.4f, -3.8f, 11), V(-1.4f, 3.8f, 11) }, 0.5f * k, 0.5f * k, Mat.Second, part);
                c.Tube(new[] { V(-1.4f, -3.8f, 4), V(-1.4f, 3.8f, 4) }, 0.5f * k, 0.5f * k, Mat.Second, part);
                foreach (float y in new[] { -2.2f, 0f, 2.2f })
                    c.Tube(new[] { V(-1.0f, y, 2.5f), V(-0.7f, y, 13.5f) }, 0.4f * k, 0.3f * k, Mat.Metal, P(), 0.2f);
                c.Box(V(2.4f, 0, 1.6f), V(1.6f, 1.2f, 1.6f), Mat.Accent, P(), -0.1f);
                c.Box(V(2.4f, 0, 3.6f), V(2.6f, 1.4f, 0.6f), Mat.Metal, P(), 0.1f);
                break;
            }
            case Prop.WarTable:
            {
                // A war table: the country map pinned on a table, a lantern, marker pins.
                c.Box(V(0, 0, 5.8f), V(4.6f, 3.4f, 0.5f), Mat.Second, part);
                foreach (var (x, y) in new[] { (-3.8f, -2.6f), (3.8f, -2.6f), (-3.8f, 2.6f), (3.8f, 2.6f) })
                    c.Tube(new[] { V(x, y, 0), V(x, y, 5.6f) }, 0.6f * k, 0.6f * k, Mat.Second, part);
                c.Box(V(0, 0, 6.4f), V(3.8f, 2.7f, 0.15f), Mat.Main, P(), 0.2f, true);
                foreach (var (x, y) in new[] { (-1.6f, -0.8f), (1.2f, 1.0f), (2.4f, -1.2f) })
                    c.Sphere(V(x, y, 7.0f), 0.55f * k, Mat.Metal, P(), 0.3f);
                c.Tube(new[] { V(-3.2f, 2.0f, 6.6f), V(-3.2f, 2.0f, 9.8f) }, 0.9f * k, 0.9f * k, Mat.Metal, P());
                c.Sphere(V(-3.2f, 2.0f, 8.6f), 0.95f * k, glow, P(), 0.2f, 0, lit);
                break;
            }
            case Prop.Fence or Prop.FenceY or Prop.FenceCorner:
            {
                // A paddock fence: a post, and rails running half a tile each way along the fence's line
                // (the board's X, its Y, or both at a corner), so a row of them joins up. On the board's
                // camera a model's Y runs along the board's X and its X along the board's Y.
                const float half = 17f; // half a tile, in model units (not scaled: the rails must meet)
                c.Tube(new[] { V(0, 0, 0), V(0, 0, 8) }, 0.8f * k, 0.7f * k, Mat.Second, part);
                c.Sphere(V(0, 0, 8.2f), 0.8f * k, Mat.Second, part);
                foreach (float z in new[] { 3.2f * k, 6.4f * k })
                {
                    if (p != Prop.FenceY) c.Tube(new[] { new Vector3(0, -half, z), new Vector3(0, half, z) }, 0.5f * k, 0.5f * k, Mat.Main, P());
                    if (p != Prop.Fence) c.Tube(new[] { new Vector3(-half, 0, z), new Vector3(half, 0, z) }, 0.5f * k, 0.5f * k, Mat.Main, P());
                }
                break;
            }
            case Prop.Gate:
            {
                // The way out: two posts, a beam, and a sign hung from it.
                foreach (float y in new[] { -4.8f, 4.8f }) c.Tube(new[] { V(0, y, 0), V(0, y, 16) }, 0.9f * k, 0.8f * k, Mat.Second, part);
                c.Tube(new[] { V(0, -5.6f, 15.4f), V(0, 5.6f, 15.4f) }, 0.8f * k, 0.8f * k, Mat.Second, part);
                c.Box(V(0.4f, 0, 12.4f), V(0.3f, 3.0f, 1.6f), Mat.Main, P(), 0.15f);
                c.Tube(new[] { V(0, -1.8f, 15), V(0.3f, -1.8f, 14) }, 0.2f * k, 0.2f * k, Mat.Metal, P());
                c.Tube(new[] { V(0, 1.8f, 15), V(0.3f, 1.8f, 14) }, 0.2f * k, 0.2f * k, Mat.Metal, P());
                break;
            }
            case Prop.Trough:
            {
                // A feed trough at the pens: a long wooden box, full.
                c.Box(V(0, 0, 1.8f), V(2.0f, 4.2f, 1.8f), Mat.Second, part);
                c.Box(V(0, 0, 3.4f), V(1.5f, 3.7f, 0.3f), Mat.Main, P(), 0.1f);
                break;
            }
            case Prop.Scaffold:
            {
                // A plot not yet built: poles, planks, a tarp over a stack of crates.
                foreach (var (x, y) in new[] { (-3.6f, -3.6f), (3.6f, -3.6f), (-3.6f, 3.6f), (3.6f, 3.6f) })
                    c.Tube(new[] { V(x, y, 0), V(x, y, 12) }, 0.45f * k, 0.45f * k, Mat.Second, part);
                c.Box(V(0, 0, 6), V(4.0f, 4.0f, 0.3f), Mat.Second, P(), -0.1f);
                c.Box(V(0, 0, 11.6f), V(4.0f, 4.0f, 0.3f), Mat.Second, P(), -0.1f);
                c.Box(V(0.6f, -0.6f, 2.0f), V(2.0f, 2.0f, 2.0f), Mat.Accent, P(), -0.15f);
                c.Ellipsoid(V(0.6f, -0.6f, 4.4f), new Vector3(1, 0, 0), 2.6f * k, 2.6f * k, 0.9f * k, Mat.Main, P());
                break;
            }
            case Prop.Vent:
            {
                // A street grate, flush with the ground: slots over the dark (the steam is PropFx's).
                c.Box(V(0, 0, 0.35f), V(4.6f, 4.6f, 0.35f), Mat.Metal, part);
                for (int i = -2; i <= 2; i++) c.Box(V(i * 1.7f, 0, 0.55f), V(0.45f, 3.8f, 0.2f), Mat.Second, P(), -0.4f);
                break;
            }
            case Prop.Crate:
                c.Box(V(0, 0, 4.5f), V(4.5f, 4.5f, 4.5f), Mat.Second, part);
                c.Box(V(0, 0, 4.5f), V(4.7f, 4.7f, 0.6f), Mat.Accent, part, -0.1f);
                if (variant >= 2) c.Box(V(0.5f, -0.5f, 12), V(3.3f, 3.3f, 3.3f), Mat.Second, P(), 0.05f);
                break;
            case Prop.Barrel:
            {
                c.Tube(new[] { V(0, 0, 0), V(0, 0, 5), V(0, 0, 10) }, t => (4 + 0.7f * Mathf.Sin(Mathf.Pi * t)) * k, Mat.Second, part);
                foreach (float z in new[] { 2.2f, 7.8f })
                {
                    var ring = new List<Vector3>();
                    for (int i = 0; i <= 16; i++) ring.Add(V(Mathf.Cos(i / 16f * Mathf.Tau) * 4.55f, Mathf.Sin(i / 16f * Mathf.Tau) * 4.55f, z));
                    c.Tube(ring, 0.55f * k, 0.55f * k, Mat.Metal, part);
                }
                break;
            }
            case Prop.Tank:
                c.Tube(new[] { V(0, 0, 0), V(0, 0, 17) }, 5.5f * k, 5.5f * k, Mat.Metal, part);
                c.Sphere(V(0, 0, 17), 5.5f * k, Mat.Metal, part);
                c.Tube(new[] { V(0, 0, 6), V(0, 0, 12) }, 5.8f * k, 5.8f * k, Mat.Glow, P(), 0.05f);
                c.Tube(new[] { V(4, -3, 15), V(8, -5, 15), V(8.5f, -5.5f, 0) }, 1.1f * k, 1.1f * k, Mat.Metal, P());
                break;
            case Prop.Gears:
            {
                c.Box(V(0, 0, 2), V(5, 3, 2), Mat.Accent, part);
                var hub = V(0, 0, 13);
                var ring = new List<Vector3>();
                for (int i = 0; i <= 24; i++) ring.Add(hub + new Vector3(0, Mathf.Cos(i / 24f * Mathf.Tau), Mathf.Sin(i / 24f * Mathf.Tau)) * 8 * k);
                int g = P();
                c.Tube(ring, 1.4f * k, 1.4f * k, Mat.Metal, g);
                for (int i = 0; i < 10; i++)
                {
                    var d = new Vector3(0, Mathf.Cos(i / 10f * Mathf.Tau), Mathf.Sin(i / 10f * Mathf.Tau));
                    c.Sphere(hub + d * 9.6f * k, 1.4f * k, Mat.Metal, g);
                    if (i % 2 == 0) c.Tube(new[] { hub, hub + d * 8 * k }, 0.8f * k, 0.8f * k, Mat.Metal, g);
                }
                c.Sphere(hub, 2.2f * k, Mat.Glow, g, 0.1f, 0, true);
                break;
            }
            case Prop.Statue:
            {
                c.Box(V(0, 0, 3), V(4.5f, 4.5f, 3), Mat.Accent, part, -0.05f);
                int fig = P();
                c.Ellipsoid(V(0, 0, 12), X, 3.2f * k, 3.4f * k, 6 * k, Mat.Accent, fig, 0.05f);
                c.Sphere(V(0.6f, 0, 19.5f), 2.3f * k, Mat.Accent, fig, 0.05f);
                c.Tube(new[] { V(0.5f, -3, 15), V(3, -4.5f, 19), V(3.5f, -4, 23) }, 1.0f * k, 0.8f * k, Mat.Accent, fig, 0.05f);
                c.Tube(new[] { V(-1.5f, 1.5f, 16), V(-4.5f, 5.5f, 21), V(-6, 7, 18) }, 2.2f * k, 0.3f * k, Mat.Accent, fig, 0.1f);
                break;
            }
            case Prop.Fountain:
            {
                var ring = new List<Vector3>();
                for (int i = 0; i <= 24; i++) ring.Add(V(Mathf.Cos(i / 24f * Mathf.Tau) * 9, Mathf.Sin(i / 24f * Mathf.Tau) * 9, 2.4f));
                c.Tube(ring, 1.8f * k, 1.8f * k, Mat.Accent, part);
                c.Ellipsoid(V(0, 0, 2.2f), X, 8.6f * k, 8.6f * k, 0.8f * k, Mat.Membrane, P(), 0.15f);
                c.Tube(new[] { V(0, 0, 1), V(0, 0, 9) }, 1.6f * k, 1.2f * k, Mat.Accent, part);
                c.Ellipsoid(V(0, 0, 9), X, 4 * k, 4 * k, 1.2f * k, Mat.Accent, part, 0.05f);
                c.Tube(new[] { V(0, 0, 9.5f), V(0, 0, 13) }, 0.8f * k, 0.4f * k, Mat.Membrane, P(), 0.25f);
                break;
            }
            case Prop.Pillar:
            {
                float h = 14 + variant * 3;
                c.Box(V(0, 0, 1.2f), V(4.2f, 4.2f, 1.2f), Mat.Accent, part, -0.05f);
                c.Tube(new[] { V(0, 0, 2), V(0, 0, h) }, 3.3f * k, 3.1f * k, Mat.Accent, part);
                c.Sphere(V(1.5f, -1, h), 2.0f * k, Mat.Accent, part);
                c.Ellipsoid(V(6, 2, 1.2f), X, 2.4f * k, 2 * k, 1.4f * k, Mat.Accent, P(), -0.08f);
                c.Ellipsoid(V(-4, -5, 1.0f), X, 1.8f * k, 1.5f * k, 1.1f * k, Mat.Accent, P(), -0.08f);
                break;
            }
            case Prop.Stall:
            {
                c.Box(V(0, 0, 3.2f), V(5.5f, 3.5f, 3.2f), Mat.Second, part);
                foreach (var (x, y) in new[] { (-5f, -3.2f), (5f, -3.2f), (-5f, 3.2f), (5f, 3.2f) })
                    c.Tube(new[] { V(x, y, 0), V(x, y, 13.5f) }, 0.6f * k, 0.6f * k, Mat.Second, part);
                c.Membrane(new[] { V(-6.5f, -5.5f, 12), V(6.5f, -5.5f, 12), V(6.5f, 4, 15), V(-6.5f, 4, 15) }, Mat.Main, P());
                for (int i = 0; i < 5; i++) c.Sphere(V(-3.5f + i * 1.8f, -1.5f + (i % 2) * 2, 7.2f), 1.1f * k, i % 2 == 0 ? Mat.Accent : Mat.Glow, P(), 0.05f, 0, i % 2 == 1);
                break;
            }
            case Prop.Banner:
                c.Tube(new[] { V(0, 0, 0), V(0, 0, 25) }, 0.7f * k, 0.6f * k, Mat.Metal, part);
                c.Sphere(V(0, 0, 25.5f), 1.1f * k, Mat.Metal, part);
                c.Membrane(new[] { V(0, -0.6f, 23.5f), V(0, -6.5f, 23.5f), V(0, -6.5f, 13), V(0, -3.5f, 15), V(0, -0.6f, 13) }, Mat.Main, P());
                c.Sphere(V(0.3f, -3.5f, 19.5f), 1.3f * k, Mat.Accent, P(), 0.1f);
                break;
            case Prop.Reeds:
                for (int i = 0; i < 8; i++)
                {
                    var b = V(rng.Range(-5f, 5f), rng.Range(-5f, 5f), 0);
                    var top = b + V(rng.Range(-1.5f, 1.5f), rng.Range(-1.5f, 1.5f), rng.Range(7f, 12f));
                    int r = P();
                    c.Tube(new[] { b, b.Lerp(top, 0.6f) + V(0.4f, 0, 0), top }, 0.5f * k, 0.3f * k, Mat.Main, r);
                    if (i % 3 == 0) c.Ellipsoid(top - V(0, 0, 1.5f), Z, 1.6f * k, 0.7f * k, 0.7f * k, Mat.Second, r);
                }
                break;
            case Prop.Grave:
                c.Box(V(0, 0, 3.6f), V(1.3f, 3.2f, 3.6f), Mat.Accent, part);
                c.Ellipsoid(V(0, 0, 7.2f), X, 1.3f * k, 3.2f * k, 1.8f * k, Mat.Accent, part, 0, 6.5f * k);
                c.Box(V(0, 0, 0.5f), V(2.5f, 4.2f, 0.5f), Mat.Accent, P(), -0.1f);
                break;
            case Prop.Hedge:
                c.Box(V(0, 0, 4.5f), V(6.2f, 6.2f, 4.5f), Mat.Main, part);
                for (int i = 0; i < 6; i++)
                    c.Sphere(V(rng.Range(-4f, 4f), rng.Range(-4f, 4f), 9 + rng.Range(-0.5f, 0.8f)), 2.6f * k, Mat.Main, part, 0.05f);
                break;
            case Prop.Chimney:
                c.Box(V(0, 0, 5.5f), V(2.6f, 2.6f, 5.5f), Mat.Accent, part);
                c.Box(V(0, 0, 11.4f), V(3.2f, 3.2f, 0.8f), Mat.Accent, part, -0.05f);
                c.Sphere(V(-1, 1, 15), 2.2f * k, Mat.Membrane, P(), 0.2f, -100);
                c.Sphere(V(-3, 2.5f, 19), 2.8f * k, Mat.Membrane, P(), 0.25f, -100);
                break;
        }
    }

    /// <summary>A region's light colour (lamps, signs, fungus, chem): neon pink or cyan in the undercity.</summary>
    static string GlowHex(int variant, string theme) => theme switch
    {
        "Undercity" => Math.Abs(variant) % 2 == 0 ? "#ff4fb8" : "#4fe3ff",
        "Mire" => "#4ff0a0",
        "Cavern" => "#7fd8ff",
        "Uptown" => "#ffcf6a",
        "Frost" => "#9fe8ff",
        "Volcanic" => "#ff6a2a",
        _ => "#ffb04f",
    };

    public static Color GlowOf(int variant, string theme) => Color.FromHtml(GlowHex(variant, theme));

    /// <summary>
    /// Colours for a prop in a region: Main (leaves, cloth), Second (wood, bark), Accent (stone,
    /// marble, crystal), Membrane (water, smoke), Glow (lamps, fungus, chem).
    /// </summary>
    static CreaturePalette PaletteFor(Prop p, int variant, string theme)
    {
        string leaves = theme switch
        {
            "Ruins" => variant % 2 == 0 ? "#b5632f" : "#9a7a2c", "Mire" => "#4a6b3a", "Uptown" => "#4f9a4a", "Cavern" => "#4a3a7a",
            "Frost" => "#2f5a52", "Desert" => "#8a8a3a", "Volcanic" => "#3a3030", "Coast" => "#4f7a4a", _ => "#3f6e45",
        };
        string bark = theme switch { "Mire" => "#4a3f35", "Cavern" => "#cbbfd8", "Volcanic" => "#2a2224", "Desert" => "#8a6a4a", _ => "#6b4a32" };
        string stone = theme switch
        {
            "Uptown" => "#d8d0bf", "Ruins" => "#9a8c7a", "Cavern" => "#8a6ae0", "Mire" => "#5f6663",
            "Frost" => "#a8bcd0", "Desert" => "#d0a070", "Volcanic" => "#3e3a44", "Coast" => "#9aa4ae", _ => "#7a7f90",
        };
        string water = theme switch { "Mire" => "#5fae8a", "Volcanic" => "#ff8a3a", _ => "#6fb4e0" };
        string glow = GlowHex(variant, theme);
        string cloth = variant switch { 0 => "#b03a4a", 1 => "#2f6fb0", 2 => "#c9a23a", _ => "#5a8a3a" };
        var spec = p switch
        {
            Prop.Mushroom => PaletteSpec.FromHex(theme == "Cavern" ? "#7a4ab8" : "#a8443a", "#e2d6c4", stone, water, glow),
            Prop.Crystals => PaletteSpec.FromHex(leaves, bark, theme switch { "Cavern" => "#9a7aff", "Volcanic" => "#2a2030", "Frost" => "#cfeeff", _ => "#7fd8ff" }, water, glow),
            Prop.Stall or Prop.Banner => PaletteSpec.FromHex(cloth, bark, theme == "Uptown" ? "#e6c26a" : "#d8c49a", water, glow),
            Prop.Chimney => PaletteSpec.FromHex(leaves, bark, theme == "Uptown" ? "#a85a3a" : "#5a4a4a", "#9a96a6", glow),
            Prop.Crate or Prop.Barrel => PaletteSpec.FromHex(leaves, "#8a6238", "#4a3a2a", water, glow),
            Prop.Reeds => PaletteSpec.FromHex(theme == "Mire" ? "#6a8a3a" : "#7a8a4a", "#5a3a22", stone, water, glow),
            Prop.Hydrant => PaletteSpec.FromHex(theme == "Uptown" ? "#c9a23a" : variant % 2 == 0 ? "#b8402e" : "#c98a2a", "#2a2228", stone, water, glow),
            Prop.BrokenLamp or Prop.Vent => PaletteSpec.FromHex(leaves, "#1e1a24", stone, "#5a5866", glow),
            Prop.Vat => PaletteSpec.FromHex(leaves, bark, stone, "#9ad8c8", "#4ff0a0"),
            Prop.LabBench => PaletteSpec.FromHex(leaves, "#6b4a32", "#7fd8ff", "#d8e8f0", "#ff4fb8"),
            Prop.Dummy => PaletteSpec.FromHex("#c8a860", "#6b4a32", "#6a5a4a", water, glow),
            Prop.Rack => PaletteSpec.FromHex(leaves, "#6b4a32", "#3a3640", water, glow),
            Prop.WarTable => PaletteSpec.FromHex("#e8d8a8", "#5a3e2a", stone, water, glow),
            Prop.Gate => PaletteSpec.FromHex("#b03a4a", "#6b4a32", stone, water, glow),
            Prop.Fence or Prop.FenceY or Prop.FenceCorner => PaletteSpec.FromHex("#9a7448", "#6b4a32", stone, water, glow),
            Prop.Trough => PaletteSpec.FromHex("#c8b060", "#7a5a3a", stone, water, glow),
            Prop.Scaffold => PaletteSpec.FromHex("#7a6a5a", "#8a6a44", "#6b4a32", water, glow),
            _ => PaletteSpec.FromHex(leaves, bark, stone, water, glow),
        };
        return spec.Build();
    }
}
