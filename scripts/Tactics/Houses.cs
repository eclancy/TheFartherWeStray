using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using Godot;

namespace TheFartherWeStray.Tactics;

/// <summary>What a house's walls are made of.</summary>
public enum WallKind { Plaster, Timbered, Brick, Boards, Tin, Stone }

/// <summary>What a house's roof is: shingles, ridged tin, a flat tar roof with a parapet, slate scales, copper, thatch, snow.</summary>
public enum RoofKind { Shingles, Tin, Flat, Slate, Copper, Thatch, Snow }

/// <summary>A house's windows: square panes, tall arches, portholes, shuttered, boarded up, barred.</summary>
public enum WindowKind { Square, Arched, Round, Shuttered, Boarded, Barred }

/// <summary>
/// One house: a run of roof tiles of one height, painted as one building with its own walls, roof,
/// windows, colours and lights. Rolled from the run's first tile, so a map's houses are the same
/// every time and never touch the rules.
/// </summary>
public sealed class House
{
    public Color Wall, Trim, Door, Roof, Lamp, Sign, Shutter, AwningA, AwningB;
    public Color[] Lights;           // the colours its lit windows glow, the main one most often
    public float Lit;                // the share of its windows lit
    public WallKind Walls;
    public RoofKind RoofKind;
    public WindowKind Windows;
    public bool Pair;                // two windows a storey on each wall, not one
    public bool HasLamp, HasSign, HasPipe, HasIvy, HasAwning, HasFlowers, HasIcicles, Skylights;
    public Vector2I SignTile, PipeTile; // the one tile of the run that carries its sign, and its drainpipe
    public int SignFace, PipeFace;
    public int Symbol;               // the mark in its neon sign
}

/// <summary>What a region's houses are made of and lit by. Each house picks its own from these.</summary>
public sealed class Street
{
    public Color[] Walls, Roofs, Lights, Trims, Doors, Shutters, Awnings, Lamps, Signs;
    public WallKind[] WallKinds = { WallKind.Plaster };
    public RoofKind[] RoofKinds = { RoofKind.Shingles };
    public WindowKind[] WindowKinds = { WindowKind.Square };
    public float Lit = 0.7f, Pair = 0.4f, Lamp, Sign, Pipe, Ivy, Awning, Flowers, Icicles, Skylights;
}

public sealed partial class IsoArt
{
    const int WallPx = TW / 2; // a wall face is this many pixels across

    readonly Dictionary<Vector2I, House> _houses = new();
    Street _street;

    static Color[] Cs(params string[] hex) => hex.Select(C).ToArray();

    /// <summary>
    /// Each region's houses (the user's ask for more variety, 2026-11-03). The undercity: soot brick,
    /// tin and boards in grimy colours, tin and tar roofs, portholes, boarded and barred windows lit in
    /// every colour, neon signs, drainpipes and wall lamps. The city above: pastel plaster and dressed
    /// stone, terracotta, slate and copper roofs, arched and shuttered windows in warm light, striped
    /// awnings and flower boxes. The rest by their own looks.
    /// </summary>
    static readonly Dictionary<string, Street> Streets = new()
    {
        ["Undercity"] = new Street
        {
            Walls = Cs("#5a5068", "#6a3e36", "#3e5a4e", "#7a5a34", "#2f5a66", "#5a5a62", "#4e3a5a", "#7a3a3a", "#2e3550", "#6a4a5a", "#4a4a3a"),
            Roofs = Cs("#3d4a5a", "#5a5a62", "#7a4a32", "#2e2e36", "#4a3a4a", "#3a4a40", "#5a4a3a"),
            Lights = Cs("#ffd27a", "#ff4fb8", "#4fe3ff", "#6fff8a", "#b07aff", "#ffb04f", "#ffe3a8"),
            Trims = Cs("#241c2c", "#3a2a2a", "#1e2430", "#4a3a2a", "#2a2a30"),
            Doors = Cs("#3a2418", "#1e2a3a", "#3a1e2a", "#2a3a2a", "#5a2a1e", "#2a2a2a"),
            Shutters = Cs("#3a4a3a", "#4a3a3a"), Awnings = Cs("#6a3a3a", "#3a3a4a", "#5a4a2a", "#2a4a4a"),
            Lamps = Cs("#ffd27a", "#ffb04f", "#4fe3ff", "#ff4fb8", "#6fff8a"),
            Signs = Cs("#ff4fb8", "#4fe3ff", "#6fff8a", "#ffd23a", "#b07aff", "#ff6a3a"),
            WallKinds = new[] { WallKind.Brick, WallKind.Brick, WallKind.Tin, WallKind.Boards, WallKind.Plaster, WallKind.Stone },
            RoofKinds = new[] { RoofKind.Tin, RoofKind.Tin, RoofKind.Flat, RoofKind.Flat, RoofKind.Shingles },
            WindowKinds = new[] { WindowKind.Square, WindowKind.Square, WindowKind.Round, WindowKind.Boarded, WindowKind.Barred },
            Lit = 0.68f, Lamp = 0.4f, Sign = 0.35f, Pipe = 0.5f, Ivy = 0.04f, Awning = 0.12f, Skylights = 0.35f,
        },
        ["Uptown"] = new Street
        {
            Walls = Cs("#d9c9a8", "#d8a8a0", "#e2cf8f", "#e8e2d4", "#a8bcd0", "#b8c8a8", "#c8a878", "#e0c0a0", "#c8b8d0"),
            Roofs = Cs("#a8553a", "#8a4a3a", "#4a5a6a", "#5f9f8a", "#3e4a5e", "#b8923a", "#7a3a4a"),
            Lights = Cs("#ffe3a8", "#ffd27a", "#fff0c8", "#9fd8e8"),
            Trims = Cs("#6a4a32", "#e8d8b0", "#b8923a", "#8a7a6a", "#f0ead8"),
            Doors = Cs("#4a2e1e", "#2a3e5a", "#5a1e2a", "#2e4a3a", "#6a4a2a"),
            Shutters = Cs("#3e6a4a", "#2e4a7a", "#7a2e2e", "#6a5a3a", "#4a6a7a"),
            Awnings = Cs("#b83a3a", "#2e5a8a", "#3e7a4a", "#c8923a", "#f0ead8", "#7a3a6a"),
            Lamps = Cs("#ffe3a8", "#ffd27a"), Signs = Cs("#ffd27a"),
            WallKinds = new[] { WallKind.Plaster, WallKind.Plaster, WallKind.Stone, WallKind.Stone, WallKind.Timbered, WallKind.Brick },
            RoofKinds = new[] { RoofKind.Shingles, RoofKind.Shingles, RoofKind.Slate, RoofKind.Copper },
            WindowKinds = new[] { WindowKind.Arched, WindowKind.Arched, WindowKind.Shuttered, WindowKind.Square },
            Lit = 0.75f, Pair = 0.55f, Lamp = 0.45f, Pipe = 0.1f, Ivy = 0.15f, Awning = 0.4f, Flowers = 0.45f,
        },
        ["Ruins"] = new Street
        {
            Walls = Cs("#8a7f72", "#7a7064", "#9a8a78", "#6e6458", "#8a7a6a", "#7a6a5a"),
            Roofs = Cs("#5a5048", "#4a4440", "#6a5a4a", "#4f5a5a"),
            Lights = Cs("#ffb04f", "#ff8a3a"),
            Trims = Cs("#4f483f", "#6a5e50"), Doors = Cs("#2e2620", "#3a2e24"),
            Shutters = Cs("#4a4a3a"), Awnings = Cs("#5a4a3a"), Lamps = Cs("#ffb04f"), Signs = Cs("#ffb04f"),
            WallKinds = new[] { WallKind.Stone, WallKind.Stone, WallKind.Brick, WallKind.Plaster },
            RoofKinds = new[] { RoofKind.Slate, RoofKind.Shingles, RoofKind.Flat },
            WindowKinds = new[] { WindowKind.Arched, WindowKind.Boarded, WindowKind.Square },
            Lit = 0.22f, Lamp = 0.12f, Ivy = 0.6f,
        },
        ["Mire"] = new Street
        {
            Walls = Cs("#5a4632", "#4a3a2a", "#3e4a3a", "#5a5040", "#3a3028", "#4a4038"),
            Roofs = Cs("#7a5a3a", "#5a4a3a", "#4a5a4a", "#6a4a2a"),
            Lights = Cs("#4ff0a0", "#9fffc8", "#ffd27a", "#c8ff4f"),
            Trims = Cs("#2a2018", "#3a2e22"), Doors = Cs("#1e1610", "#2a2418"),
            Shutters = Cs("#3a4a2a"), Awnings = Cs("#4a5a3a"), Lamps = Cs("#4ff0a0", "#ffd27a"), Signs = Cs("#4ff0a0", "#c8ff4f"),
            WallKinds = new[] { WallKind.Boards, WallKind.Boards, WallKind.Tin },
            RoofKinds = new[] { RoofKind.Tin, RoofKind.Tin, RoofKind.Thatch },
            WindowKinds = new[] { WindowKind.Square, WindowKind.Boarded, WindowKind.Round },
            Lit = 0.55f, Lamp = 0.35f, Sign = 0.12f, Pipe = 0.3f, Ivy = 0.35f,
        },
        ["Frost"] = new Street
        {
            Walls = Cs("#7a5a3e", "#6a4a32", "#8a6a48", "#5a4a3a"),
            Roofs = Cs("#5a3e2a", "#3e3a3a", "#6a2e2a"),
            Lights = Cs("#ffd27a", "#ffb04f"),
            Trims = Cs("#3a2a1e"), Doors = Cs("#2a1e14", "#4a2e1e"),
            Shutters = Cs("#7a2e2e", "#2e4a6a"), Awnings = Cs("#7a2e2e"), Lamps = Cs("#ffd27a"), Signs = Cs("#ffd27a"),
            WallKinds = new[] { WallKind.Boards },
            RoofKinds = new[] { RoofKind.Snow },
            WindowKinds = new[] { WindowKind.Square, WindowKind.Shuttered },
            Lit = 0.8f, Lamp = 0.4f, Icicles = 0.75f,
        },
        ["Ranch"] = new Street
        {
            Walls = Cs("#e0d4b8", "#a84a3a", "#8a6a4a", "#d8c8a0", "#b8aa8e"),
            Roofs = Cs("#8a3a2e", "#c8a050", "#5a4a3a", "#6a5a4a"),
            Lights = Cs("#ffd27a", "#ffe3a8"),
            Trims = Cs("#5a3e2a", "#e8dcc0"), Doors = Cs("#3e2a1c", "#6a3a2a", "#2e4a3a"),
            Shutters = Cs("#3e6a4a", "#7a2e2e", "#2e4a7a"), Awnings = Cs("#a84a3a", "#e8dcc0"), Lamps = Cs("#ffd27a"), Signs = Cs("#ffd27a"),
            WallKinds = new[] { WallKind.Plaster, WallKind.Timbered, WallKind.Boards, WallKind.Boards },
            RoofKinds = new[] { RoofKind.Shingles, RoofKind.Thatch, RoofKind.Shingles },
            WindowKinds = new[] { WindowKind.Shuttered, WindowKind.Square },
            Lit = 0.7f, Lamp = 0.35f, Ivy = 0.3f, Flowers = 0.35f, Awning = 0.1f,
        },
        ["Coast"] = new Street
        {
            Walls = Cs("#e8e0d0", "#9ab8d8", "#e8d890", "#e8b0b0", "#a8d8c0", "#f0ece0"),
            Roofs = Cs("#4a6a8a", "#3a5a7a", "#a8553a", "#5a6a7a"),
            Lights = Cs("#ffe3a8", "#ffd27a"),
            Trims = Cs("#f0ece0", "#4a5a6a"), Doors = Cs("#2e4a6a", "#3e6a5a", "#6a3a2a"),
            Shutters = Cs("#2e5a8a", "#3e7a6a", "#e8e0d0"), Awnings = Cs("#2e5a8a", "#e8e0d0", "#c84a3a"), Lamps = Cs("#ffe3a8"), Signs = Cs("#ffd27a"),
            WallKinds = new[] { WallKind.Plaster, WallKind.Plaster, WallKind.Boards },
            RoofKinds = new[] { RoofKind.Slate, RoofKind.Shingles },
            WindowKinds = new[] { WindowKind.Shuttered, WindowKind.Square, WindowKind.Round },
            Lit = 0.7f, Lamp = 0.4f, Flowers = 0.3f, Awning = 0.3f,
        },
    };

    /// <summary>This map's street: its region's, or one made from its look for the regions without houses of their own.</summary>
    Street StreetHere => _street ??= Streets.TryGetValue(_map.Theme, out var s) ? s : new Street
    {
        Walls = new[] { _look.WallL }, Roofs = new[] { _look.Roof }, Lights = _look.Windows, Trims = new[] { _look.Timber },
        Doors = new[] { _look.Door }, Shutters = Cs("#4a3a2a"), Awnings = Cs("#7a3a3a"), Lamps = Cs("#ffd27a"), Signs = Cs("#ffd27a"),
        Lamp = 0.25f,
    };

    /// <summary>The house a roof tile belongs to: the run of roof tiles of its height it joins, rolled once.</summary>
    House HouseAt(Vector2I t)
    {
        if (_houses.TryGetValue(t, out var known)) return known;
        int h = HeightOf(t);
        var run = new List<Vector2I> { t };
        var seen = new HashSet<Vector2I> { t };
        for (int i = 0; i < run.Count && run.Count < 48; i++)
            foreach (var d in new[] { Vector2I.Right, Vector2I.Left, Vector2I.Up, Vector2I.Down })
            {
                var n = run[i] + d;
                if (seen.Add(n) && Painted(n) && TerrainOf(n) == Terrain.Roof && HeightOf(n) == h) run.Add(n);
            }
        var first = run.OrderBy(r => r.Y).ThenBy(r => r.X).First();
        var house = MakeHouse(new SeededRng(Hash(first.X + 4000, first.Y + 4000, 77 + h * 13 + (int)(SeededRng.Hash(_map.Name) % 997))), run);
        foreach (var r in run) _houses[r] = house;
        return house;
    }

    bool Painted(Vector2I t) => t.X >= -_skirt && t.Y >= -_skirt && t.X < _map.W + _skirt && t.Y < _map.H + _skirt;

    House MakeHouse(SeededRng rng, List<Vector2I> run)
    {
        var s = StreetHere;
        var main = rng.Pick(s.Lights);
        var house = new House
        {
            Wall = Vary(rng.Pick(s.Walls), rng), Roof = Vary(rng.Pick(s.Roofs), rng), Trim = rng.Pick(s.Trims), Door = rng.Pick(s.Doors),
            Shutter = rng.Pick(s.Shutters), AwningA = rng.Pick(s.Awnings), Lamp = rng.Pick(s.Lamps), Sign = rng.Pick(s.Signs),
            Walls = rng.Pick(s.WallKinds), RoofKind = rng.Pick(s.RoofKinds), Windows = rng.Pick(s.WindowKinds),
            // Mostly one colour of light, now and then another.
            Lights = new[] { main, main, main, rng.Pick(s.Lights) },
            Lit = Math.Clamp(s.Lit + rng.Range(-0.2f, 0.2f), 0.05f, 0.95f),
            Pair = rng.Chance(s.Pair), HasLamp = rng.Chance(s.Lamp), HasSign = rng.Chance(s.Sign), HasPipe = rng.Chance(s.Pipe),
            HasIvy = rng.Chance(s.Ivy), HasAwning = rng.Chance(s.Awning), HasFlowers = rng.Chance(s.Flowers),
            HasIcicles = rng.Chance(s.Icicles), Skylights = rng.Chance(s.Skylights),
            SignTile = rng.Pick(run), PipeTile = rng.Pick(run), SignFace = rng.Int(2), PipeFace = rng.Int(2), Symbol = rng.Int(Symbols.Length),
        };
        var awnings = s.Awnings.Where(a => a != house.AwningA).ToArray();
        house.AwningB = awnings.Length > 0 ? rng.Pick(awnings) : house.AwningA.Lightened(0.4f);
        // A dark trim on a dark wall vanishes: give it some contrast.
        if (Mathf.Abs(house.Trim.Luminance - house.Wall.Luminance) < 0.08f) house.Trim = house.Wall.Luminance > 0.4f ? house.Wall.Darkened(0.45f) : house.Wall.Lightened(0.3f);
        return house;
    }

    /// <summary>A colour nudged a little, so no two houses picking it match exactly.</summary>
    static Color Vary(Color c, SeededRng rng) =>
        Color.FromHsv((c.H + rng.Range(-0.015f, 0.015f) + 1) % 1, Mathf.Clamp(c.S * rng.Range(0.85f, 1.12f), 0, 1), Mathf.Clamp(c.V * rng.Range(0.9f, 1.08f), 0, 1));

    static bool DoorAt(Vector2I t, int face) => Hash(t.X, t.Y, 40 + face) % 3 == 0;
    static bool DoorLit(Vector2I t, int face) => Hash(t.X, t.Y, 48 + face) % 2 == 0;
    static bool LampAt(Vector2I t, int face, House house) => house.HasLamp && (DoorAt(t, face) || Hash(t.X, t.Y, 49 + face) % 3 == 0);

    static Color Mortar(Color wall) => wall.Luminance > 0.45f ? wall.Darkened(0.25f) : wall.Lightened(0.18f);

    // Window spaces on a wall, in its pixels across: one in the middle, or a pair.
    static readonly (int lo, int hi)[] One = { (9, 14) }, Two = { (4, 8), (15, 19) };

    /// <summary>
    /// A house front on one wall of a building tile (<paramref name="face"/> 0 the left wall, 1 the
    /// right, in shadow; a and b are the wall's top corners): the house's material with a ledge at each
    /// storey, its windows (lit or dark, with a glow round the lit ones), a door at street level with an
    /// awning or a lamp, a neon sign up top, a drainpipe, ivy, icicles, graffiti.
    /// </summary>
    Color Facade(Vector2I t, int x, int y, Vector2 a, Vector2 b, int h, int face)
    {
        var house = HouseAt(t);
        float along = (x + 0.5f - a.X) / (b.X - a.X);
        float below = y + 0.5f - (a.Y + along * (b.Y - a.Y));
        int storey = (int)(below / HS), inStorey = (int)below % HS, row = (int)below, storeys = h;
        int px = Math.Clamp((int)(along * WallPx), 0, WallPx - 1);
        Color S(Color c) => face == 1 ? c.Darkened(0.3f) : c; // the right wall is in shadow
        if (below < 1.5f) return S(house.Trim); // the eave
        if (house.HasIcicles && row <= 4 && px % 2 == 0 && row - 1 <= (int)(Hash(px, t.X * 31 + t.Y, 52 + face) % 5) - 1) return S(C("#e8f4ff"));
        if (storey >= storeys) return S(house.Wall.Darkened(0.25f)); // the foundation below street level
        if (px == 0 || px == WallPx - 1)
            return S(house.Walls is WallKind.Stone or WallKind.Brick ? (row / 3 % 2 == 0 ? house.Wall.Lightened(0.15f) : house.Wall.Darkened(0.1f)) : house.Trim);
        if (GraffitiChance > 0 && WallArt(t, face, h) is { } art)
        {
            int col = Math.Clamp((int)(along * WallCols), 0, WallCols - 1), r = (int)below - art.top;
            if (r >= 0 && r < art.mask.GetLength(1) && art.mask[col, r] is var m and > 0) return S(art.colors[m]);
        }
        bool bottom = storey == storeys - 1, door = bottom && DoorAt(t, face), lamp = bottom && LampAt(t, face, house);
        if (door)
        {
            if (house.HasAwning && inStorey is 1 or 2 && px >= 6 && px <= 17) return S(px / 2 % 2 == 0 ? house.AwningA : house.AwningB);
            if (px >= 9 && px <= 14 && inStorey >= 3) return DoorPixel(house, px, inStorey, DoorLit(t, face), face);
        }
        if (lamp && px is 5 or 6 && inStorey is >= 4 and <= 6)
            return inStorey == 4 ? S(C("#1a1620")) : px == 5 && inStorey == 5 ? house.Lamp.Lightened(0.5f) : house.Lamp;
        if (house.HasSign && t == house.SignTile && face == house.SignFace && storey == 0 && storeys >= 2 && SignPixel(house, px, inStorey) is { } sign) return sign;
        if (!(door && px >= 7 && px <= 16) && WindowPixel(t, house, face, storey, px, inStorey) is { } window) return window;
        if (house.HasPipe && t == house.PipeTile && face == house.PipeFace && px == 2) return S(inStorey == 6 ? C("#8a8494") : C("#4a4652"));

        var c = Material(house, t, face, x, y, px, row);
        if (house.HasIvy && IvyAt(t, face, px, row, storeys) is { } ivy) c = ivy;
        // Soot and grime gather toward the street.
        float grime = Mathf.Clamp((row - (storeys - 1) * HS) / (float)HS, 0, 1);
        if (_map.Theme is not ("Uptown" or "Coast")) c = c.Darkened(0.12f * grime);
        c = S(c);
        return Lighting(c, t, house, face, px, row, storey, inStorey, storeys, door, lamp);
    }

    /// <summary>The wall's own surface: plaster, timber framing, brick courses, boards, ridged tin, dressed stone.</summary>
    Color Material(House house, Vector2I t, int face, int x, int y, int px, int row)
    {
        var w = house.Wall;
        uint n = Hash(x, y, 42);
        switch (house.Walls)
        {
            case WallKind.Brick:
            {
                int course = row / 3, shift = course % 2 * 3;
                if (row % 3 == 2 || (px + shift) % 6 == 5) return Mortar(w);
                uint k = Hash((px + shift) / 6 + t.X * 17 + face * 5, course + t.Y * 13, 44);
                return k % 6 == 0 ? w.Darkened(0.14f) : k % 7 == 0 ? w.Lightened(0.08f) : w;
            }
            case WallKind.Boards:
            {
                int plank = row / 3;
                if (row % 3 == 0) return w.Darkened(0.3f);
                uint k = Hash(plank + t.X * 7, face + t.Y * 11, 45);
                if ((px + plank * 7 + (int)(k % 5)) % 13 == 0) return w.Darkened(0.3f); // where one board ends and the next begins
                var p = k % 3 == 0 ? w.Darkened(0.08f) : k % 3 == 1 ? w.Lightened(0.05f) : w;
                return n % 19 == 0 ? p.Darkened(0.12f) : p;
            }
            case WallKind.Tin:
            {
                var c = px % 2 == 0 ? w.Lightened(0.08f) : w.Darkened(0.06f);
                uint k = Hash(px + t.X * 5, face + t.Y * 3, 46);
                return k % 6 == 0 && row > (int)(k / 6 % 12) ? c.Lerp(C("#8a4a2a"), 0.35f) : c; // rust running down
            }
            case WallKind.Stone:
            {
                int course = row / 4, shift = course % 2 * 3;
                if (row % 4 == 3 || (px + shift) % 6 == 5) return w.Darkened(0.25f);
                uint k = Hash((px + shift) / 6 + t.X * 23 + face * 7, course + t.Y * 19, 47);
                return k % 5 == 0 ? w.Darkened(0.1f) : k % 4 == 0 ? w.Lightened(0.06f) : w;
            }
            case WallKind.Timbered:
            {
                // Diagonal braces in the panels either side of the windows.
                int s = row % HS;
                if (s == 0) return house.Trim;
                if ((px <= 6 && (px - 1 == s - 2 || px - 1 == 9 - s)) || (px >= 17 && (22 - px == s - 2 || 22 - px == 9 - s))) return house.Trim;
                break;
            }
        }
        if (row % HS == 0) return house.Trim; // a ledge at each storey
        return n % 23 == 0 ? w.Darkened(0.12f) : n % 29 == 0 ? w.Lightened(0.06f) : w;
    }

    /// <summary>A window's pixel, if this is one: its frame, glass lit or dark, shutters, boards or bars, a flower box below.</summary>
    Color? WindowPixel(Vector2I t, House house, int face, int storey, int px, int inStorey)
    {
        Color S(Color c) => face == 1 ? c.Darkened(0.3f) : c;
        var slots = house.Pair ? Two : One;
        for (int i = 0; i < slots.Length; i++)
        {
            var (lo, hi) = slots[i];
            if (px < lo - 2 || px > hi + 2 || inStorey < 2 || inStorey > 10) continue;
            uint key = Hash(t.X * 5 + face * 3 + i, t.Y * 7 + storey, 41);
            bool lit = key % 100 < house.Lit * 100 && !(house.Windows == WindowKind.Boarded && key % 4 != 0);
            var light = house.Lights[key / 100 % (uint)house.Lights.Length];
            if (house.HasFlowers && inStorey is 9 or 10 && px >= lo - 1 && px <= hi + 1)
                return inStorey == 10 ? S(C("#6a4a2e")) : S(Hash(px, t.X + t.Y * 7, 55) % 3 == 0 ? C("#3e6a3a") : ((px + i) % 3) switch { 0 => C("#ff6f9f"), 1 => C("#ffffff"), _ => C("#ffd25a") });
            if (house.Windows == WindowKind.Shuttered && inStorey >= 3 && inStorey <= 8 && (px == lo - 2 || px == lo - 1 || px == hi + 1 || px == hi + 2))
                return S(inStorey % 2 == 0 ? house.Shutter.Darkened(0.2f) : house.Shutter);
            if (px < lo || px > hi) continue;
            Color Glass() => lit ? (face == 1 ? light.Darkened(0.1f) : light).Lightened(inStorey == 4 && px < (lo + hi) / 2 ? 0.35f : 0)
                : inStorey == 4 && px == lo + 1 ? S(C("#3a3a52")) : S(C("#1a1a2a"));
            var frame = S(house.Trim);
            switch (house.Windows)
            {
                case WindowKind.Arched:
                    if (inStorey == 2) return px > lo + 1 && px < hi - 1 ? frame : null;
                    if (inStorey == 3 && (px == lo || px == hi)) return null;
                    if (inStorey > 8) return null;
                    if (px == lo || px == hi || inStorey == 8 || (inStorey == 3 && (px == lo + 1 || px == hi - 1))) return frame;
                    return Glass();
                case WindowKind.Round:
                {
                    float cx = (lo + hi + 1) / 2f, cy = 6f, r = Math.Min((hi - lo + 1) / 2f, 3.2f);
                    float d = new Vector2(px + 0.5f - cx, inStorey + 0.5f - cy).Length();
                    if (d > r + 0.4f) return null;
                    return d > r - 0.8f ? S(C("#8a7a5a")) : Glass();
                }
            }
            if (inStorey < 3 || inStorey > 8) return null;
            if (px == lo || px == hi || inStorey == 3 || inStorey == 8) return frame;
            switch (house.Windows)
            {
                case WindowKind.Boarded:
                    if (px - lo == inStorey - 3 || px - lo == 8 - inStorey || inStorey == 5) return S(C("#6a4a2e"));
                    break;
                case WindowKind.Barred:
                    if ((px - lo) % 2 == 1) return S(C("#141218"));
                    break;
                default:
                    if (hi - lo >= 5 && (px == (lo + hi) / 2 || inStorey == 5)) return frame; // the mullions
                    break;
            }
            return Glass();
        }
        return null;
    }

    Color DoorPixel(House house, int px, int inStorey, bool lit, int face)
    {
        Color S(Color c) => face == 1 ? c.Darkened(0.3f) : c;
        if (house.Walls == WallKind.Tin) return S(inStorey % 2 == 0 ? C("#6a6a72") : C("#4a4a52")); // a roller shutter
        if (house.Windows == WindowKind.Arched && inStorey == 3 && (px == 9 || px == 14)) return S(house.Wall);
        if (inStorey == 3) return lit ? house.Lights[0] : S(house.Door.Darkened(0.3f)); // the fanlight
        if (px == 9 || px == 14) return S(house.Door.Darkened(0.3f));
        if (px == 13 && inStorey == 7) return S(C("#e8c060"));
        return S(px is 11 or 12 && inStorey is 5 or 9 ? house.Door.Darkened(0.18f) : house.Door);
    }

    /// <summary>A neon sign across a top storey: a tube round a dark board, a mark inside lit the same colour.</summary>
    Color? SignPixel(House house, int px, int inStorey)
    {
        if (px < 3 || px > 20 || inStorey < 2 || inStorey > 9) return null;
        if (px == 3 || px == 20 || inStorey == 2 || inStorey == 9) return house.Sign;
        var mark = Symbols[house.Symbol];
        int w = mark.Max(r => r.Length), x0 = 3 + (18 - w) / 2, y0 = 2 + (8 - mark.Length) / 2;
        int mx = px - x0, my = inStorey - y0;
        if (my >= 0 && my < mark.Length && mx >= 0 && mx < mark[my].Length && mark[my][mx] == 'a') return house.Sign.Lightened(0.35f);
        return C("#140c1a");
    }

    /// <summary>Ivy creeping up from the street in patches.</summary>
    static Color? IvyAt(Vector2I t, int face, int px, int row, int storeys)
    {
        uint k = Hash(t.X, t.Y, 53 + face);
        int centre = (int)(k % WallPx), half = 4 + (int)(k / 24 % 5);
        float off = Mathf.Abs(px - centre) / (float)half;
        if (off > 1) return null;
        float tall = storeys * HS * (0.3f + 0.5f * (k / 120 % 100) / 100f) * (1 - off * off);
        if (storeys * HS - row > tall || Hash(px, row, 54 + t.X) % 3 == 0) return null;
        return Hash(px, row, 56) % 2 == 0 ? C("#3e6a3a") : C("#5a8a4a");
    }

    /// <summary>What lights fall on a wall: its lit windows' glow, the lamp by its door, its neon sign.</summary>
    Color Lighting(Color c, Vector2I t, House house, int face, int px, int row, int storey, int inStorey, int storeys, bool door, bool lamp)
    {
        if (lamp)
        {
            float d = new Vector2(px - 5.5f, row - ((storeys - 1) * HS + 5.5f)).Length();
            if (d < 7) c = c.Lerp(house.Lamp, 0.45f * Sq(1 - d / 7));
        }
        if (house.HasSign && t == house.SignTile && face == house.SignFace && storey <= 1 && storeys >= 2)
        {
            float dx = Math.Max(0, Math.Max(3 - px, px - 20)), dy = Math.Max(0, Math.Max(2 - row, row - 9));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 4) c = c.Lerp(house.Sign, 0.4f * Sq(1 - d / 4));
        }
        var slots = house.Pair ? Two : One;
        for (int i = 0; i < slots.Length; i++)
        {
            var (lo, hi) = slots[i];
            uint key = Hash(t.X * 5 + face * 3 + i, t.Y * 7 + storey, 41);
            if (key % 100 >= house.Lit * 100 || house.Windows == WindowKind.Boarded || (door && storey == storeys - 1 && !house.Pair)) continue;
            float dx = Math.Max(0, Math.Max(lo - px, px - hi)), dy = Math.Max(0, Math.Max(3 - inStorey, inStorey - 8));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 3) c = c.Lerp(house.Lights[key / 100 % (uint)house.Lights.Length], 0.22f * Sq(1 - d / 3));
        }
        return c;
    }

    /// <summary>A house's roof: shingles, ridged tin, a flat tar roof with a parapet, vents and lit skylights, slate scales, copper, thatch or snow.</summary>
    Color RoofTop(Vector2I t, int x, int y, float u, float v)
    {
        var house = HouseAt(t);
        uint n = Hash(x, y, 71);
        Color roof = house.Roof, light = roof.Lightened(0.2f), dark = roof.Darkened(0.32f);
        switch (house.RoofKind)
        {
            case RoofKind.Tin:
            {
                float r = u * 9 % 1f;
                var c = r < 0.25f ? light : r < 0.5f ? roof : r < 0.75f ? roof.Darkened(0.12f) : dark.Lightened(0.1f);
                return Hash(x / 3, y / 2, 72) % 23 == 0 ? c.Lerp(C("#8a4a2a"), 0.5f) : c;
            }
            case RoofKind.Flat:
            {
                if (u < 0.07f || u > 0.93f || v < 0.07f || v > 0.93f) return light; // the parapet
                uint k = Hash(t.X, t.Y, 73);
                if (house.Skylights && k % 3 == 0 && u > 0.36f && u < 0.64f && v > 0.36f && v < 0.64f)
                    return u < 0.41f || u > 0.59f || v < 0.41f || v > 0.59f ? dark : house.Lights[0];
                if (k % 4 == 1 && u > 0.62f && u < 0.78f && v > 0.18f && v < 0.34f) return u < 0.66f || v < 0.22f ? light : dark; // a vent box
                return n % 5 == 0 ? roof.Lightened(0.1f) : n % 7 == 0 ? dark : roof;
            }
            case RoofKind.Slate:
            {
                float rows = v * 5, a = u * 5 + Mathf.FloorToInt(rows) % 2 * 0.5f;
                float fr = rows - Mathf.Floor(rows), fa = a - Mathf.Floor(a);
                if (fr > 0.72f + 0.25f * Mathf.Abs(fa - 0.5f) * 2) return dark; // scalloped edges
                return n % 13 == 0 ? light : fr < 0.3f ? roof.Lightened(0.08f) : roof;
            }
            case RoofKind.Copper:
                if (u * 4 % 1f < 0.1f) return roof.Lightened(0.25f); // raised seams
                if (Hash(x, y / 3, 74) % 11 == 0) return roof.Lightened(0.15f); // streaks of patina
                return n % 9 == 0 ? roof.Darkened(0.1f) : roof;
            case RoofKind.Thatch:
                return (v * 12 + n % 3 * 0.06f) % 1f < 0.2f ? dark : n % 4 == 0 ? light : roof;
            case RoofKind.Snow:
            {
                float fr = v * 4 % 1f;
                if (fr < 0.12f && n % 3 == 0) return roof; // the shingles peeking through
                return n % 11 == 0 ? C("#ffffff") : n % 7 == 0 ? C("#c8d8e8") : C("#dce8f2");
            }
            default:
            {
                float rows = v * 4, a = u * 4 + Mathf.FloorToInt(rows) % 2 * 0.5f;
                float fr = rows - Mathf.Floor(rows);
                if (fr < 0.16f || a - Mathf.Floor(a) < 0.07f) return dark;
                if (fr > 0.8f) return light;
                uint k = Hash(Mathf.FloorToInt(a), Mathf.FloorToInt(rows) + t.X * 31 + t.Y * 17, 75);
                return n % 17 == 0 ? light : k % 5 == 0 ? roof.Darkened(0.1f) : k % 7 == 0 ? roof.Lightened(0.06f) : roof;
            }
        }
    }

    /// <summary>
    /// Light from a house's lamp or lit doorway pooling on the street in front of it: the house up and
    /// to the right stands on this tile's top-right edge with its left wall, the one up and to the left
    /// on its top-left edge with its right wall.
    /// </summary>
    void Spill(Image img, Vector2I t, Vector2 top)
    {
        if (HeightOf(t) != 0) return;
        var centre = top + new Vector2(0, TH / 2f);
        foreach (var (nb, face) in new[] { (t + new Vector2I(0, -1), 0), (t + new Vector2I(-1, 0), 1) })
        {
            if (!Painted(nb) || TerrainOf(nb) != Terrain.Roof || HeightOf(nb) < 1) continue;
            var house = HouseAt(nb);
            Vector2 e0 = face == 0 ? top : top + new Vector2(-TW / 2f, TH / 2f), e1 = face == 0 ? top + new Vector2(TW / 2f, TH / 2f) : top;
            if (LampAt(nb, face, house)) Pool(e0.Lerp(e1, 0.24f), house.Lamp, 0.42f);
            if (DoorAt(nb, face) && DoorLit(nb, face)) Pool(e0.Lerp(e1, 0.5f), house.Lights[0], 0.3f);
        }

        void Pool(Vector2 at, Color light, float strength)
        {
            var c = at.Lerp(centre, 0.35f);
            float rx = TW * 0.34f, ry = TH * 0.36f;
            for (int y = Mathf.FloorToInt(c.Y - ry); y <= Mathf.CeilToInt(c.Y + ry); y++)
            for (int x = Mathf.FloorToInt(c.X - rx); x <= Mathf.CeilToInt(c.X + rx); x++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                if (Mathf.Abs(x + 0.5f - centre.X) / (TW / 2f) + Mathf.Abs(y + 0.5f - centre.Y) / (TH / 2f) > 1) continue;
                float d = Mathf.Sqrt(Sq((x + 0.5f - c.X) / rx) + Sq((y + 0.5f - c.Y) / ry));
                if (d >= 1) continue;
                var p = img.GetPixel(x, y);
                if (p.A > 0) img.SetPixel(x, y, p.Lerp(light, strength * Sq(1 - d)));
            }
        }
    }
}
