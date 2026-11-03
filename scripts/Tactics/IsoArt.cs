using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheFartherWeStray.Tactics;

/// <summary>
/// Paints the isometric board as pixel art, FFTA-style: diamond tiles raised on blocks with lit and
/// shaded sides, in an undercity palette (cobbles, moss, brass platforms, glowing chem). Also the
/// backdrop: a skyline of brass towers above and neon-lit slums below.
/// </summary>
public sealed class IsoArt
{
    public const int TW = 48, TH = 24, HS = 12; // tile width, tile height, pixels per height level
    const int BaseDepth = 8, Margin = 8;

    readonly BattleMap _map;
    readonly Look _look;
    public readonly int Width, Height;
    readonly int _ox, _oy;

    // The surroundings painted round the battlefield (the user's ask, 2026-11-02: less background, a
    // proper city): tiles outside the map, never part of the battle, and the props standing on them.
    readonly int _skirt;
    readonly Dictionary<Vector2I, (Terrain terrain, int height)> _around = new();
    readonly HashSet<Vector2I> _aroundBlocked = new();
    public readonly List<(Vector2I tile, Prop prop)> AroundProps = new();
    float _fade; // how far the tile being painted is darkened (the surroundings fade out at their edge)

    /// <param name="skirt">How many tiles of surroundings to paint round the map (0: the map alone, an island).</param>
    public IsoArt(BattleMap map, int skirt = 0)
    {
        _map = map;
        _look = Look.Get(map.Theme);
        _skirt = skirt;
        if (skirt > 0) Surround();
        int maxH = 0;
        foreach (var t in PaintOrder()) maxH = Math.Max(maxH, HeightOf(t));
        _ox = (map.H + 2 * skirt) * TW / 2 + Margin;
        _oy = skirt * TH + maxH * HS + Margin;
        Width = (map.W + map.H + 4 * skirt) * TW / 2 + Margin * 2;
        Height = (map.W + map.H + 4 * skirt) * TH / 2 + maxH * HS + BaseDepth + Margin * 2;
    }

    public Terrain TerrainOf(Vector2I t) => _map.InBounds(t) ? _map.TerrainAt(t) : _around.TryGetValue(t, out var a) ? a.terrain : Terrain.Grass;
    public int HeightOf(Vector2I t) => _map.InBounds(t) ? _map.HeightAt(t) : _around.TryGetValue(t, out var a) ? a.height : 0;
    bool BlockedAt(Vector2I t) => _map.InBounds(t) ? _map.Blocked(t) : _aroundBlocked.Contains(t);

    /// <summary>Every tile painted, the surroundings included, back to front.</summary>
    IEnumerable<Vector2I> PaintOrder()
    {
        int w = _map.W + 2 * _skirt, h = _map.H + 2 * _skirt;
        for (int s = 0; s < w + h - 1; s++)
        for (int x = 0; x < w; x++)
        {
            int y = s - x;
            if (y >= 0 && y < h) yield return new Vector2I(x - _skirt, y - _skirt);
        }
    }

    /// <summary>How many tiles outside the map a tile is (0 inside it).</summary>
    int Outside(Vector2I t) => Math.Max(t.X < 0 ? -t.X : t.X >= _map.W ? t.X - _map.W + 1 : 0, t.Y < 0 ? -t.Y : t.Y >= _map.H ? t.Y - _map.H + 1 : 0);

    /// <summary>
    /// Fills the surroundings, the same every time for a map. Next to the map, the ground carries on
    /// (a sidewalk, the shore, the field) at the edge's own height. Beyond that, by region: in a town,
    /// blocks of houses with alleys between, tall behind the battlefield, low in front so nothing hides
    /// the fight; out in the wilds, cliffs and rock behind, open ground in front, the region's own
    /// water, lava, crystal or fields.
    /// </summary>
    void Surround()
    {
        uint salt = (uint)Creatures.SeededRng.Hash(_map.Name);
        uint R(Vector2I t, int k) => Hash(t.X + 1000, t.Y + 1000, (int)(salt % 100000) + k);
        string theme = _map.Theme;
        foreach (var t in PaintOrder())
        {
            int d = Outside(t);
            if (d == 0) continue;
            var near = new Vector2I(Math.Clamp(t.X, 0, _map.W - 1), Math.Clamp(t.Y, 0, _map.H - 1));
            var nearT = _map.TerrainAt(near);
            int nearH = _map.HeightAt(near);
            bool behind = (t.X < 0 || t.Y < 0) && !(t.X >= _map.W || t.Y >= _map.H); // up the screen: may stand tall
            bool front = (t.X >= _map.W || t.Y >= _map.H) && !(t.X < 0 || t.Y < 0);  // down the screen: kept low
            uint r = R(t, 0);
            bool alley = (t.X + 1000) % 5 == 0 || (t.Y + 1000) % 5 == 0;
            Terrain terrain;
            int height;
            Prop? prop = null;
            int Tall(int lo, int hi) => behind ? lo + (int)(r % (uint)(hi - lo + 1)) + (d >= 4 ? 1 : 0) : front ? Math.Min(1, lo) : Math.Min(2, lo + (int)(r % 2));
            switch (theme)
            {
                case "Undercity" or "Uptown" or "Ruins":
                {
                    bool ruins = theme == "Ruins";
                    if (d == 1)
                    {
                        // The street carries on past the map's edge (a building at the edge carries on too).
                        (terrain, height) = nearT == Terrain.Roof ? (Terrain.Roof, nearH) : (theme == "Uptown" && nearT == Terrain.Grass ? Terrain.Grass : Terrain.Stone, Math.Min(nearH, 3));
                        if (terrain != Terrain.Roof && r % 7 == 0) prop = theme == "Undercity" ? (r % 3 == 0 ? Prop.BrokenLamp : Prop.Lamp) : theme == "Uptown" ? Prop.Lamp : Prop.Rock;
                        break;
                    }
                    if (alley || (front && r % 3 != 0) || (ruins && r % 3 == 0))
                    {
                        terrain = theme == "Uptown" && !alley && r % 2 == 0 ? Terrain.Grass : ruins && r % 2 == 0 ? Terrain.Grass : Terrain.Stone;
                        height = 0;
                        uint q = R(t, 7) % 24; // about one tile in five has something on it
                        prop = q switch
                        {
                            0 => theme == "Uptown" ? Prop.Tree : theme == "Ruins" ? Prop.DeadTree : Prop.Barrel,
                            1 => theme == "Undercity" ? Prop.Neon : theme == "Uptown" ? Prop.Hedge : Prop.Rock,
                            2 => theme == "Undercity" ? Prop.Crate : theme == "Uptown" ? Prop.Statue : Prop.Pillar,
                            3 => theme == "Undercity" ? (r % 5 == 0 ? Prop.Hydrant : r % 2 == 0 ? Prop.Vent : Prop.Barrel) : theme == "Uptown" ? Prop.Lamp : Prop.Grave,
                            4 => front ? Prop.Stall : (Prop?)null,
                            _ => null,
                        };
                        break;
                    }
                    terrain = Terrain.Roof;
                    height = ruins ? Math.Max(1, Tall(1, 3) - (int)(r % 2)) : Tall(2, 4);
                    if (r % 6 == 0 && height >= 2) prop = Prop.Chimney;
                    break;
                }
                case "Mire":
                    if (d == 1) { (terrain, height) = (nearT is Terrain.Chem or Terrain.Water ? Terrain.Chem : Terrain.Wood, Math.Min(nearH, 1)); break; }
                    if (alley) { (terrain, height) = (Terrain.Wood, 0); prop = r % 5 == 0 ? Prop.Lamp : null; break; }
                    if (r % 3 == 0 && !front) { (terrain, height) = (Terrain.Roof, Tall(1, 2)); break; }
                    (terrain, height) = (r % 2 == 0 ? Terrain.Chem : Terrain.Grass, 0);
                    if (terrain == Terrain.Grass && r % 4 == 1) prop = Prop.Reeds;
                    break;
                case "Frost":
                    if (behind && d >= 2) { (terrain, height) = (Terrain.Stone, Tall(1, 3)); if (r % 5 == 0) prop = Prop.Pine; break; }
                    (terrain, height) = (r % 6 == 0 ? Terrain.Ice : Terrain.Grass, d == 1 ? Math.Min(nearH, 2) : 0);
                    if (terrain == Terrain.Grass && r % 4 == 0) prop = r % 3 == 0 ? Prop.Rock : Prop.Pine;
                    break;
                case "Desert":
                    if (behind && d >= 2) { (terrain, height) = (Terrain.Stone, Tall(1, 3)); if (r % 7 == 0) prop = Prop.Pillar; break; }
                    (terrain, height) = (Terrain.Sand, d == 1 ? Math.Min(nearH, 2) : (int)(r % 2) * (front ? 0 : 1));
                    if (r % 9 == 0) prop = r % 2 == 0 ? Prop.Rock : Prop.DeadTree;
                    break;
                case "Volcanic":
                    if (behind && d >= 2) { (terrain, height) = (Terrain.Stone, Tall(1, 4)); break; }
                    (terrain, height) = (d >= 2 && r % 4 == 0 ? Terrain.Lava : Terrain.Stone, d == 1 ? Math.Min(nearH, 2) : 0);
                    if (terrain == Terrain.Stone && r % 8 == 0) prop = Prop.Rock;
                    break;
                case "Coast":
                    if (front && d >= 2) { (terrain, height) = (Terrain.Water, 0); break; }
                    if (behind && d >= 3) { (terrain, height) = (Terrain.Stone, Tall(1, 3)); break; }
                    (terrain, height) = (d == 1 ? (nearT == Terrain.Water ? Terrain.Water : Terrain.Sand) : Terrain.Grass, d == 1 ? Math.Min(nearH, 1) : (int)(r % 2));
                    if (terrain == Terrain.Grass && r % 5 == 0) prop = Prop.Reeds;
                    break;
                case "Cavern":
                    if (behind && d >= 2) { (terrain, height) = (Terrain.Crystal, Tall(2, 4)); break; }
                    (terrain, height) = (r % 3 == 0 ? Terrain.Grass : Terrain.Stone, d == 1 ? Math.Min(nearH, 2) : 0);
                    if (r % 6 == 0) prop = r % 2 == 0 ? Prop.Crystals : Prop.Mushroom;
                    break;
                default: // the ranch and anywhere else: fields and orchards, a barn or two behind
                    if (behind && d >= 3 && !alley && r % 3 == 0) { (terrain, height) = (Terrain.Roof, Tall(1, 2)); break; }
                    (terrain, height) = (alley ? Terrain.Dirt : (t.X + t.Y) % 2 == 0 && d >= 2 && r % 3 != 0 ? Terrain.Dirt : Terrain.Grass, 0);
                    if (terrain == Terrain.Grass && r % 6 == 0) prop = r % 3 == 0 ? Prop.Tree : Prop.Bush;
                    break;
            }
            _around[t] = (terrain, height);
            if (prop is { } pr && terrain is not (Terrain.Roof or Terrain.Water or Terrain.Chem or Terrain.Lava))
            {
                AroundProps.Add((t, pr));
                _aroundBlocked.Add(t);
            }
        }
    }

    /// <summary>Board-pixel position of a tile's top corner at a height. Tile coords may be fractional.</summary>
    public Vector2 TileTop(Vector2 t, int h) => new(_ox + (t.X - t.Y) * TW / 2f, _oy + (t.X + t.Y) * TH / 2f - h * HS);

    /// <summary>The centre of a tile's top face.</summary>
    public Vector2 TileCenter(Vector2 t, int h) => TileTop(t, h) + new Vector2(0, TH / 2f);

    public Vector2[] Diamond(Vector2I t, float inset = 0)
    {
        var top = TileTop(t, _map.HeightAt(t));
        float hw = TW / 2f - inset * 2, hh = TH / 2f - inset;
        var c = top + new Vector2(0, TH / 2f);
        return new[] { c + new Vector2(0, -hh), c + new Vector2(hw, 0), c + new Vector2(0, hh), c + new Vector2(-hw, 0) };
    }

    /// <summary>Tiles in painter's order, back to front.</summary>
    public IEnumerable<Vector2I> DrawOrder()
    {
        for (int s = 0; s < _map.W + _map.H - 1; s++)
        for (int x = 0; x < _map.W; x++)
        {
            int y = s - x;
            if (y >= 0 && y < _map.H) yield return new Vector2I(x, y);
        }
    }

    /// <summary>The tile whose top face is under a board-pixel point, front-most first.</summary>
    public Vector2I? Pick(Vector2 p)
    {
        Vector2I? hit = null;
        foreach (var t in DrawOrder())
        {
            var c = TileCenter(t, _map.HeightAt(t));
            if (Mathf.Abs(p.X - c.X) / (TW / 2f) + Mathf.Abs(p.Y - c.Y) / (TH / 2f) <= 1) hit = t;
        }
        return hit;
    }

    // ------------------------------------------------------------------ board

    static Color C(string hex) => Color.FromHtml(hex);

    static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + salt * 2246822519);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    public Image PaintBoard(double time = 0)
    {
        var img = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        foreach (var t in PaintOrder())
        {
            // The surroundings darken toward their edge, so the battlefield stands out.
            int d = Outside(t);
            _fade = d == 0 ? 0 : Math.Clamp(0.22f + (d - 1) / (float)_skirt * 0.45f, 0, 0.62f);
            PaintTile(img, t);
        }
        _fade = 0;
        Creatures.ArtStyle.Current.GradeImage(img);
        return img;
    }

    void PaintTile(Image img, Vector2I t)
    {
        var terrain = TerrainOf(t);
        int h = HeightOf(t);
        var top = TileTop(t, h);
        int depth = h * HS + BaseDepth;
        float hw = TW / 2f, hh = TH / 2f;
        var L = top + new Vector2(-hw, hh);
        var B = top + new Vector2(0, TH);
        var R = top + new Vector2(hw, hh);
        var down = new Vector2(0, depth);

        if (terrain == Terrain.Pit)
        {
            // A hole: just darkness with a faint glow far below.
            Fill(img, new[] { top, R, B, L }, (x, y, u, v) =>
            {
                float d = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                return d > 0.44f ? C("#1b1426") : (Hash(x, y, 3) % 23 == 0 ? C("#3b1f4a") : C("#0a0710"));
            });
            return;
        }

        var (sideL, sideR) = Sides(terrain);
        if (terrain == Terrain.Roof)
        {
            // A building: its walls are house fronts, with timbers, windows and sometimes a door.
            Fill(img, new[] { L, B, B + down, L + down }, (x, y, u, v) => Facade(t, x, y, L, B, h, sideL, 0));
            Fill(img, new[] { B, R, R + down, B + down }, (x, y, u, v) => Facade(t, x, y, B, R, h, sideR, 1));
            Fill(img, new[] { top, R, B, L }, (x, y, u, v) => TopColor(terrain, x, y, u, v));
            Line(img, top, L, TopColor(terrain, 0, 0, 0.5f, 0.5f).Lightened(0.28f));
            Line(img, top, R, TopColor(terrain, 0, 0, 0.5f, 0.5f).Lightened(0.15f));
            Line(img, L, B, sideL.Darkened(0.45f));
            Line(img, B, R, sideR.Darkened(0.45f));
            Line(img, B, B + down, sideR.Darkened(0.55f));
            return;
        }
        // Left face is lit, right face in shadow; a darker band where block meets block.
        Fill(img, new[] { L, B, B + down, L + down }, (x, y, u, v) =>
        {
            Color c = sideL;
            if (terrain is Terrain.Stone or Terrain.Grass && ((y - (int)top.Y) % 6 == 0)) c = sideL.Darkened(0.18f);
            if (terrain == Terrain.Brass && Hash(x, y, 9) % 17 == 0) c = sideL.Lightened(0.2f);
            if (terrain == Terrain.Chem && (x + y) % 9 == 0) c = C("#2f8f5e");
            return c;
        });
        Fill(img, new[] { B, R, R + down, B + down }, (x, y, u, v) =>
        {
            Color c = sideR;
            if (terrain is Terrain.Stone or Terrain.Grass && ((y - (int)top.Y + 3) % 6 == 0)) c = sideR.Darkened(0.18f);
            return c;
        });
        Fill(img, new[] { top, R, B, L }, (x, y, u, v) => TopColor(terrain, x, y, u, v));

        // Crisp FFTA edges: lit back rims, dark front rims.
        Line(img, top, L, TopColor(terrain, 0, 0, 0.5f, 0.5f).Lightened(0.28f));
        Line(img, top, R, TopColor(terrain, 0, 0, 0.5f, 0.5f).Lightened(0.15f));
        Line(img, L, B, sideL.Darkened(0.35f));
        Line(img, B, R, sideR.Darkened(0.35f));
        Line(img, B, B + down, sideR.Darkened(0.45f));
        Decorate(img, t, top, terrain);
    }

    /// <summary>
    /// A house front on one wall of a building tile: plaster, a timber at each storey, a window a
    /// storey (most lit), and on some, a door at street level. <paramref name="face"/> 0 is the left
    /// wall, 1 the right; a and b are the wall's top corners.
    /// </summary>
    Color Facade(Vector2I t, int x, int y, Vector2 a, Vector2 b, int h, Color wall, int face)
    {
        float along = (x + 0.5f - a.X) / (b.X - a.X);
        float below = y + 0.5f - (a.Y + along * (b.Y - a.Y));
        int storey = (int)(below / HS), inStorey = (int)below % HS;
        int storeys = h; // walls rise h storeys above the ground; the rest is foundation
        var timber = _look.Timber;
        if (below < 1.5f || inStorey == 0) return timber;
        if (along < 0.09f || along > 0.91f) return timber;
        if (storey >= storeys) return wall.Darkened(0.25f); // the foundation below street level
        if (_map.Theme == "Undercity" && WallArt(t, face, h) is { } art)
        {
            int col = (int)(along * WallCols), row = (int)below - art.top;
            if (row >= 0 && row < art.mask.GetLength(1) && art.mask[col, row] is var m and > 0)
                return face == 1 ? art.colors[m].Darkened(0.22f) : art.colors[m];
        }
        bool door = storey == storeys - 1 && Hash(t.X, t.Y, 40 + face) % 3 == 0;
        if (door)
        {
            if (along > 0.36f && along < 0.64f && inStorey >= 3)
                return along > 0.56f && inStorey == 7 ? C("#e8c060") : _look.Door;
            return wall;
        }
        if (along > 0.32f && along < 0.68f && inStorey >= 3 && inStorey <= 8)
        {
            bool edge = along < 0.36f || along > 0.64f || inStorey == 3 || inStorey == 8;
            if (edge) return timber;
            uint lit = Hash(t.X * 3 + face, t.Y * 5 + storey, 41);
            if (lit % 4 == 0) return C("#1a1a2a");
            var glass = _look.Windows[lit % (uint)_look.Windows.Length];
            return inStorey == 4 && along < 0.5f ? glass.Lightened(0.35f) : glass;
        }
        return Hash(x, y, 42) % 23 == 0 ? wall.Darkened(0.12f) : wall;
    }

    /// <summary>
    /// Small things painted on a tile's top: tufts and flowers on grass, cracks, pebbles and moss
    /// on stone, scrap in the foundry, fallen leaves in the ruins, glowing fungus in the cavern.
    /// They don't block anything; the props that do stand on tiles are drawn as sprites.
    /// </summary>
    void Decorate(Image img, Vector2I t, Vector2 top, Terrain terrain)
    {
        if (BlockedAt(t)) return;
        uint seed = Hash(t.X, t.Y, 99);
        int count = (int)(seed % 4);
        for (int k = 0; k < count; k++)
        {
            uint r = Hash(t.X * 7 + k, t.Y * 13 + k, 101);
            float u = 0.22f + (r % 100) / 100f * 0.56f, v = 0.22f + (r / 100 % 100) / 100f * 0.56f;
            var at = top + new Vector2((u - v) * TW / 2f, (u + v) * TH / 2f);
            string[] stamp = null;
            Color a = default, b = default, c = default;
            switch (terrain)
            {
                case Terrain.Grass when _map.Theme == "Frost":
                    if (r % 3 == 0) { stamp = Glint; a = C("#ffffff"); c = C("#9fe8ff"); }
                    else { stamp = Drift; a = _look.GrassLight; b = _look.GrassDark; }
                    break;
                case Terrain.Grass when _map.Theme == "Desert":
                    if (r % 3 == 0) { stamp = Tuft; a = C("#a8a050"); b = C("#6a6a32"); }
                    else { stamp = Pebble; a = _look.GrassLight; b = _look.GrassDark; }
                    break;
                case Terrain.Grass when _map.Theme == "Volcanic":
                    if (r % 3 == 0) { stamp = Ember; a = C("#ff8a3a"); b = C("#ffd27a"); }
                    else { stamp = Crack; b = C("#1e1a1c"); }
                    break;
                case Terrain.Stone when _map.Theme == "Volcanic":
                    if (r % 3 == 0) { stamp = Crack; b = C("#ff6a2a"); } else { stamp = Pebble; a = _look.StoneLight; b = _look.StoneJoint; }
                    break;
                case Terrain.Sand:
                    if (r % 4 == 0 && _map.Theme == "Coast") { stamp = Shell; a = C("#fff4e8"); b = C("#e89aa0"); }
                    else if (r % 4 == 1) { stamp = Pebble; a = C("#f0d8a0"); b = C("#a8824e"); }
                    break;
                case Terrain.Grass:
                    if (r % 5 == 0 && _map.Theme != "Cavern") { stamp = Flower; a = C("#f2e07a"); c = _look.Flowers[r / 7 % (uint)_look.Flowers.Length]; b = _look.GrassDark; }
                    else if (r % 5 == 1 && _map.Theme == "Ruins") { stamp = Leaves; c = C("#c4682f"); }
                    else if (r % 5 == 1 && _map.Theme == "Cavern") { stamp = Fungus; c = C("#7fd8ff"); b = _look.GrassDark; }
                    else { stamp = Tuft; a = _look.GrassLight; b = _look.GrassDark; }
                    break;
                case Terrain.Stone:
                    if (r % 4 == 0) { stamp = Crack; b = _look.StoneJoint; }
                    else if (r % 4 == 1 && _map.Theme is "Undercity" or "Mire") { stamp = Moss; a = _look.GrassLight; }
                    else if (r % 4 == 1 && _map.Theme == "Cavern") { stamp = Fungus; c = C("#b9a2ff"); b = _look.StoneJoint; }
                    else if (r % 4 == 2 && _map.Theme == "Undercity") { stamp = Scrap; a = C("#c79a45"); b = C("#6e5220"); }
                    else if (r % 4 == 3 && _map.Theme == "Undercity" && r % 3 == 0) { stamp = NeonPuddle; a = C("#1c2638"); c = r / 3 % 2 == 0 ? C("#ff4fb8") : C("#4fe3ff"); }
                    else { stamp = Pebble; a = _look.StoneLight; b = _look.StoneJoint; }
                    break;
                case Terrain.Dirt:
                    stamp = r % 3 == 0 && _map.Theme is "Undercity" or "Mire" or "Ruins" or "Coast" ? Puddle : Pebble;
                    a = r % 3 == 0 ? C("#5f8fb0") : C("#9a8064"); b = C("#4a3828"); c = a;
                    break;
                case Terrain.Wood:
                    if (r % 3 == 0) { stamp = Nail; a = C("#b8b0a0"); }
                    break;
            }
            if (stamp != null) Stamp(img, stamp, at, a, b, c);
        }
    }

    // ------------------------------------------------------------------ the undercity's walls

    const int WallCols = 24, ArtRows = 11;
    readonly Dictionary<(Vector2I, int), (byte[,] mask, Color[] colors, int top)?> _wallArt = new();
    static readonly Color[] Neon = { C("#ff4fb8"), C("#4fe3ff"), C("#6fff8a"), C("#ffd23a"), C("#f4f0ff"), C("#ff6a3a"), C("#b07aff") };

    // Symbols sprayed on walls ('a' paint): no words, just marks.
    static readonly string[][] Symbols =
    {
        new[] { "a..a..a", "aa.a.aa", "aaaaaaa", "aaaaaaa" },                       // a crown
        new[] { "...a...", "..aaa..", "aaaaaaa", ".aaaaa.", "..a.a..", ".a...a." },  // a star
        new[] { "...a...", "....a..", "aaaaaaa", "....a..", "...a..." },             // an arrow
        new[] { "..aaa", ".aa..", "aaaa.", "..aa.", ".aa..", "a...." },              // a bolt
        new[] { "a...a", ".a.a.", "..a..", ".a.a.", "a...a" },                       // an X
    };

    /// <summary>
    /// Graffiti and posters on one undercity wall, or none (the user's ask for a grittier undercity,
    /// 2026-11-02): a neon tag scrawled at street level with a drop shadow and drips, bubble letters,
    /// a sprayed symbol, or a torn poster pasted up a storey. Painted in the wall's own skewed grid,
    /// so it lies on the wall. Mask values: 1 paint, 2 its outline, 3 drips, 4 paper, 5 print, 6 shine.
    /// </summary>
    (byte[,] mask, Color[] colors, int top)? WallArt(Vector2I t, int face, int h)
    {
        if (_wallArt.TryGetValue((t, face), out var cached)) return cached;
        var rng = new Creatures.SeededRng(Hash(t.X, t.Y, 70 + face));
        (byte[,], Color[], int)? art = null;
        if (rng.Chance(0.6f))
        {
            var mask = new byte[WallCols, ArtRows];
            var paint = rng.Pick(Neon);
            var colors = new Color[7];
            colors[1] = paint;
            colors[2] = rng.Chance(0.6f) ? C("#120a18") : rng.Pick(Neon);
            colors[3] = paint.Darkened(0.25f);
            colors[4] = rng.Pick(new[] { C("#e8dcc0"), C("#c84a3a"), C("#e8c040"), C("#7ab0d8") });
            colors[5] = C("#2a1e2e");
            colors[6] = C("#ffffff");
            int top = (h - 1) * HS + 1; // the street-level storey
            int kind = rng.Int(10);
            void Set(int x, int y, byte v, bool over = true)
            {
                if (x < 2 || x >= WallCols - 2 || y < 0 || y >= ArtRows) return;
                if (over || mask[x, y] == 0) mask[x, y] = v;
            }
            if (kind < 4)
            {
                // A tag: two or three scrawled strokes, a drop shadow, a few drips.
                int strokes = 2 + rng.Int(2), x = 3 + rng.Int(3);
                for (int k = 0; k < strokes; k++)
                {
                    int y = 2 + rng.Int(5);
                    for (int step = 0; step < 5; step++)
                    {
                        int nx = x + 1 + rng.Int(2), ny = Math.Clamp(y + rng.Int(5) - 2, 1, 8);
                        for (int i = 0; i <= 8; i++) Set((int)Mathf.Round(Mathf.Lerp(x, nx, i / 8f)), (int)Mathf.Round(Mathf.Lerp(y, ny, i / 8f)), 1);
                        x = nx; y = ny;
                    }
                    x = Math.Max(3, x - 6 + rng.Int(3));
                }
                Outline(mask, Set);
                Drips(mask, rng, Set);
            }
            else if (kind < 6)
            {
                // Bubble letters: fat round shapes side by side, outlined, with a shine.
                int n = 2 + rng.Int(2), x0 = 4 + rng.Int(2);
                for (int i = 0; i < n; i++)
                {
                    float cx = x0 + i * 5.2f, cy = 5 + rng.Range(-1f, 1f), rx = 2.6f, ry = 3.3f;
                    for (int y = 0; y < ArtRows; y++)
                    for (int x = 0; x < WallCols; x++)
                        if (Sq((x - cx) / rx) + Sq((y - cy) / ry) <= 1) Set(x, y, 1);
                    Set((int)cx - 1, (int)cy - 2, 6);
                }
                Outline(mask, Set, all: true);
                Drips(mask, rng, Set);
            }
            else if (kind < 8)
            {
                var sym = rng.Pick(Symbols);
                int x0 = 4 + rng.Int(WallCols - 10), y0 = 1 + rng.Int(Math.Max(1, ArtRows - sym.Length - 2));
                for (int y = 0; y < sym.Length; y++)
                for (int x = 0; x < sym[y].Length; x++)
                    if (sym[y][x] == 'a') Set(x0 + x, y0 + y, 1);
                Outline(mask, Set);
                Drips(mask, rng, Set);
            }
            else
            {
                // A poster, pasted up and torn at a corner, a storey higher if there is one.
                if (h > 1) top = (h - 2) * HS + 1;
                int x0 = 4 + rng.Int(WallCols - 11), y0 = 2 + rng.Int(3);
                for (int y = 0; y < 7; y++)
                for (int x = 0; x < 6; x++)
                    if (!(x == 5 && y == 0) && !(x == 0 && y == 6 && rng.Chance(0.5f))) Set(x0 + x, y0 + y, 4);
                for (int x = 1; x < 5; x++) Set(x0 + x, y0 + 2, 5);
                Set(x0 + 2, y0 + 4, 5); Set(x0 + 3, y0 + 4, 5); Set(x0 + 1, y0 + 5, 5);
            }
            art = (mask, colors, top);
        }
        return _wallArt[(t, face)] = art;
    }

    static float Sq(float v) => v * v;

    /// <summary>Paint's edge: a shadow below and to the right (or a full outline for bubble letters).</summary>
    static void Outline(byte[,] mask, Action<int, int, byte, bool> set, bool all = false)
    {
        var paint = new List<(int x, int y)>();
        for (int y = 0; y < ArtRows; y++)
        for (int x = 0; x < WallCols; x++)
            if (mask[x, y] is 1 or 6) paint.Add((x, y));
        foreach (var (x, y) in paint)
            foreach (var (dx, dy) in all ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1) } : new[] { (1, 1), (0, 1) })
                set(x + dx, y + dy, 2, false);
    }

    /// <summary>Runs of paint down the wall from the lowest strokes.</summary>
    static void Drips(byte[,] mask, Creatures.SeededRng rng, Action<int, int, byte, bool> set)
    {
        for (int d = 0, tries = 0; d < 2 + rng.Int(2) && tries < 40; tries++)
        {
            int x = 2 + rng.Int(WallCols - 4), y = ArtRows - 1;
            while (y > 0 && mask[x, y] != 1) y--;
            if (mask[x, y] != 1) continue;
            for (int k = 1; k <= 1 + rng.Int(3); k++) set(x, y + k, 3, false);
            d++;
        }
    }

    // Tiny stamps: 'a' light, 'b' dark, 'c' accent; '.' empty.
    static readonly string[] Tuft = { "a..a", ".ab.", ".bb." };
    static readonly string[] Flower = { ".c.", "cac", ".b." };
    static readonly string[] Leaves = { "c..", ".c.", "..c" };
    static readonly string[] Fungus = { "c.c", "b.b" };
    static readonly string[] Crack = { "b...", ".b..", ".bb.", "...b" };
    static readonly string[] Moss = { "a.a", ".aa" };
    static readonly string[] Scrap = { ".a.", "aba", ".a." };
    static readonly string[] Pebble = { "a.", "bb" };
    static readonly string[] Puddle = { ".aa.", "aaaa", ".aa." };
    static readonly string[] Nail = { "a" };
    static readonly string[] Glint = { ".c.", "cac", ".c." };
    static readonly string[] Drift = { ".aa.", "abba" };
    static readonly string[] Ember = { "a.", ".b" };
    static readonly string[] Shell = { "aaa", ".b." };
    static readonly string[] NeonPuddle = { ".aaaa.", "aacaaa", "aaaaca", ".aaaa." };

    void Stamp(Image img, string[] stamp, Vector2 at, Color a, Color b, Color c)
    {
        int x0 = Mathf.FloorToInt(at.X) - stamp[0].Length / 2, y0 = Mathf.FloorToInt(at.Y) - stamp.Length / 2;
        for (int y = 0; y < stamp.Length; y++)
        for (int x = 0; x < stamp[y].Length; x++)
        {
            Color? px = stamp[y][x] switch { 'a' => a, 'b' => b, 'c' => c, _ => null };
            int ix = x0 + x, iy = y0 + y;
            if (px == null || ix < 0 || iy < 0 || ix >= Width || iy >= Height || img.GetPixel(ix, iy).A == 0) continue;
            img.SetPixel(ix, iy, _fade > 0 ? px.Value.Darkened(_fade) : px.Value);
        }
    }

    (Color left, Color right) Sides(Terrain t) => t switch
    {
        Terrain.Grass => (_look.GrassSideL, _look.GrassSideR),
        Terrain.Stone => (_look.StoneSideL, _look.StoneSideR),
        Terrain.Crystal => (C("#3b2a73"), C("#281c52")),
        Terrain.Brass => (C("#8d6a2a"), C("#5c4320")),
        Terrain.Chem => (C("#1e4a3e"), C("#14302c")),
        Terrain.Roof => (_look.WallL, _look.WallR),
        Terrain.Wood => (C("#6b4a2e"), C("#4a3220")),
        Terrain.Dirt => (C("#6a5038"), C("#4a3828")),
        Terrain.Water => (C("#1e3a5a"), C("#142a40")),
        Terrain.Ice => (C("#6f9ab8"), C("#4f7896")),
        Terrain.Sand => (C("#b08a52"), C("#8a6a3e")),
        Terrain.Lava => (C("#3a1a14"), C("#26100c")),
        _ => (C("#222"), C("#111")),
    };

    /// <summary>The top face's texture, in the tile's own (u, v) coordinates (0..1 along each edge).</summary>
    Color TopColor(Terrain t, int x, int y, float u, float v)
    {
        uint n = Hash(x, y, (int)t);
        switch (t)
        {
            case Terrain.Grass:
                return n % 9 == 0 ? _look.GrassLight : n % 7 == 0 ? _look.GrassDark : n % 31 == 0 ? _look.GrassSpeck : _look.Grass;
            case Terrain.Stone:
            {
                // Cobbles: staggered rows of stones with dark joints.
                float a = u * 3, b = v * 3 + (Mathf.FloorToInt(u * 3) % 2) * 0.5f;
                bool joint = a - Mathf.Floor(a) < 0.12f || b - Mathf.Floor(b) < 0.12f;
                if (joint) return _look.StoneJoint;
                return n % 11 == 0 ? _look.StoneLight : n % 5 == 0 ? _look.StoneMid : _look.Stone;
            }
            case Terrain.Brass:
            {
                bool seam = Mathf.Abs(u - 0.5f) < 0.035f || Mathf.Abs(v - 0.5f) < 0.035f;
                if (seam) return C("#6e5220");
                foreach (var (ru, rv) in new[] { (0.15f, 0.15f), (0.85f, 0.15f), (0.15f, 0.85f), (0.85f, 0.85f), (0.35f, 0.35f), (0.65f, 0.65f) })
                    if (Mathf.Abs(u - ru) < 0.04f && Mathf.Abs(v - rv) < 0.04f) return C("#f0cf72");
                return (u + v) % 0.5f < 0.08f ? C("#c79a45") : C("#b2873a");
            }
            case Terrain.Crystal:
            {
                // Raw hextech crystal: faceted bands with glints.
                float band = (u * 3 - v * 2 + 10) % 1f;
                if (n % 29 == 0) return C("#f0e8ff");
                return band < 0.18f ? C("#b9a2ff") : band < 0.45f ? C("#8466e8") : C("#6a4bd1");
            }
            case Terrain.Roof:
            {
                // Shingles in staggered rows, lit along their lower edges.
                float rows = v * 4, off = (Mathf.FloorToInt(rows) % 2) * 0.5f, a = u * 4 + off;
                float fr = rows - Mathf.Floor(rows);
                if (fr < 0.16f || a - Mathf.Floor(a) < 0.07f) return _look.RoofDark;
                if (fr > 0.8f) return _look.RoofLight;
                return n % 17 == 0 ? _look.RoofLight : _look.Roof;
            }
            case Terrain.Wood:
            {
                // Planks with dark joints and a little grain.
                float p = v * 4;
                if (p - Mathf.Floor(p) < 0.12f) return C("#4a3220");
                if ((u * 2 + Mathf.Floor(p) * 0.37f) % 1f < 0.04f) return C("#5a3d26");
                return n % 7 == 0 ? C("#9a7048") : n % 5 == 0 ? C("#7a5636") : C("#8a6240");
            }
            case Terrain.Dirt:
                return n % 9 == 0 ? C("#9a8064") : n % 5 == 0 ? C("#6a5038") : n % 23 == 0 ? C("#b8a080") : C("#7f6246");
            case Terrain.Water:
            {
                float r = (u * 2.2f + v * 1.4f + ((x * 5 + y * 3) % 5) * 0.01f) % 1f;
                if (r < 0.07f) return C("#cfe8ff");
                if (r < 0.18f) return C("#7fb8e8");
                return n % 13 == 0 ? C("#5fa0d8") : C("#3a78b8");
            }
            case Terrain.Ice:
            {
                // A frozen lake: pale and glassy, long glints and a few cracks.
                float g = (u * 1.7f - v * 0.9f + ((x * 3 + y * 5) % 7) * 0.05f + 4) % 1f;
                if (n % 23 == 0 && Mathf.Abs(u - v) < 0.05f) return C("#8cc4dc");
                if (g < 0.05f) return C("#f4fcff");
                return g < 0.3f ? C("#d4eef8") : C("#b8e0f0");
            }
            case Terrain.Sand:
            {
                // Dune ripples, lit on one side.
                float w = (u * 3.5f + v * 1.2f + Mathf.Sin(v * 6 + x) * 0.15f + 4) % 1f;
                if (w < 0.12f) return _look.GrassDark.Lerp(C("#b08a52"), 0.5f);
                if (w < 0.24f) return C("#f0d8a0");
                return n % 17 == 0 ? C("#c8a468") : C("#e0c088");
            }
            case Terrain.Lava:
            {
                // Molten rock under a cracking crust.
                float r = (u * 2.1f + v * 1.6f + ((x * 7 + y * 3) % 5) * 0.03f) % 1f;
                float crust = (u * 4.3f - v * 3.1f + x * 0.37f + 10) % 1f;
                if (crust < 0.18f) return C("#3a1a14");
                if (r < 0.08f) return C("#ffe08a");
                if (r < 0.25f) return C("#ffa13a");
                return n % 11 == 0 ? C("#ff7a2a") : C("#e8531c");
            }
            case Terrain.Chem:
            {
                // Glowing liquid with drifting ripples.
                float r = (u * 2.5f + v * 1.3f + ((x * 7 + y * 3) % 5) * 0.01f) % 1f;
                if (r < 0.08f) return C("#c8ffd9");
                if (r < 0.2f) return C("#7ff0a6");
                return n % 13 == 0 ? C("#8cf5b3") : C("#43c97c");
            }
        }
        return C("#000");
    }

    delegate Color Shader(int x, int y, float u, float v);

    void Fill(Image img, Vector2[] poly, Shader shade)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in poly) { minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X); minY = Mathf.Min(minY, p.Y); maxY = Mathf.Max(maxY, p.Y); }
        var top = poly[0];
        for (int y = Mathf.FloorToInt(minY); y < Mathf.CeilToInt(maxY); y++)
        for (int x = Mathf.FloorToInt(minX); x < Mathf.CeilToInt(maxX); x++)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
            float px = x + 0.5f, py = y + 0.5f;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].Y > py) != (poly[j].Y > py) &&
                    px < (poly[j].X - poly[i].X) * (py - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            if (!inside) continue;
            // (u, v): position along the diamond's two edges from its top corner.
            float dx = px - top.X, dy = py - top.Y;
            float u = dy / TH + dx / TW, v = dy / TH - dx / TW;
            var c = shade(x, y, u, v);
            img.SetPixel(x, y, _fade > 0 ? c.Darkened(_fade) : c);
        }
    }

    void Line(Image img, Vector2 a, Vector2 b, Color c)
    {
        int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.X - a.X), Mathf.Abs(b.Y - a.Y)));
        for (int i = 0; i <= n; i++)
        {
            var p = a + (b - a) * (i / (float)Math.Max(1, n));
            int x = Mathf.Clamp(Mathf.FloorToInt(p.X), 0, Width - 1), y = Mathf.Clamp(Mathf.FloorToInt(p.Y), 0, Height - 1);
            if (img.GetPixel(x, y).A > 0) img.SetPixel(x, y, _fade > 0 ? c.Darkened(_fade) : c);
        }
    }

    // ------------------------------------------------------------------ backdrop

    /// <summary>A low-res skyline: brass towers high up, neon slums below, haze between.</summary>
    public static Image PaintBackdrop(int w, int h, ulong seed, string theme = "Undercity")
    {
        var look = Look.Get(theme);
        // Some art styles fight in daylight: the sky comes from the style, the ground from the map.
        if (Creatures.ArtStyle.Current.Sky is { } sky) look = Look.Get(sky);
        var rng = new Creatures.SeededRng(seed);
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        Color top = look.SkyTop, mid = look.SkyMid, bottom = look.SkyBottom;
        for (int y = 0; y < h; y++)
        {
            float k = y / (float)h;
            var c = k < 0.6f ? top.Lerp(mid, k / 0.6f) : mid.Lerp(bottom, (k - 0.6f) / 0.4f);
            for (int x = 0; x < w; x++) img.SetPixel(x, y, c);
        }
        // Far towers (the city above), then near slums (the city below), each with lit windows.
        Skyline(img, rng, h * 0.55f, 0.13f, look.FarBody, look.FarLights, 14, 34);
        Skyline(img, rng, h * 0.78f, 0.08f, look.NearBody, look.NearLights, 10, 22);
        // Haze of chem-glow along the bottom.
        for (int y = (int)(h * 0.8f); y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float k = (y - h * 0.8f) / (h * 0.2f);
            if (Hash(x, y, 77) % 3 == 0) img.SetPixel(x, y, img.GetPixel(x, y).Lerp(look.Haze, 0.12f * k));
        }
        Creatures.ArtStyle.Current.GradeImage(img);
        return img;
    }

    static void Skyline(Image img, Creatures.SeededRng rng, float baseY, float windowChance, Color body, Color[] lights, int minW, int maxW)
    {
        int w = img.GetWidth(), h = img.GetHeight();
        int x = -rng.Int(10);
        while (x < w)
        {
            int bw = rng.Int(minW, maxW), bh = rng.Int(20, (int)(baseY * 0.7f));
            int top = (int)baseY - bh;
            bool spire = rng.Chance(0.3f);
            for (int yy = Math.Max(0, top); yy < h; yy++)
            for (int xx = Math.Max(0, x); xx < Math.Min(w, x + bw); xx++) img.SetPixel(xx, yy, body);
            if (spire)
                for (int k = 0; k < bh / 3; k++)
                {
                    int sx = x + bw / 2, sy = top - k;
                    if (sx >= 0 && sx < w && sy >= 0) img.SetPixel(sx, sy, body);
                }
            for (int yy = top + 3; yy < h - 2; yy += 4)
            for (int xx = x + 2; xx < x + bw - 2; xx += 3)
                if (xx >= 0 && xx < w && yy >= 0 && rng.Chance(windowChance * (1 - (float)(yy - top) / (h - top + 1) * 0.5f)))
                    img.SetPixel(xx, yy, lights[rng.Int(lights.Length)]);
            x += bw + rng.Int(0, 4);
        }
    }
}

/// <summary>A region's colours: its ground and its sky.</summary>
public sealed class Look
{
    public Color Grass, GrassLight, GrassDark, GrassSpeck, GrassSideL, GrassSideR;
    public Color Stone, StoneLight, StoneMid, StoneJoint, StoneSideL, StoneSideR;
    public Color SkyTop, SkyMid, SkyBottom, FarBody, NearBody, Haze;
    public Color[] FarLights, NearLights;
    // Buildings: roofs, walls (lit and shaded), timbers, doors, window glass; and garden flowers.
    public Color Roof = C("#3d4a5a"), RoofLight = C("#55667a"), RoofDark = C("#26303c");
    public Color WallL = C("#5a5068"), WallR = C("#3e3850"), Timber = C("#241c2c"), Door = C("#3a2418");
    public Color[] Windows = { C("#ffd27a"), C("#ff4fb8"), C("#4fe3ff") };
    public Color[] Flowers = { C("#ff6f9f"), C("#ffd25a"), C("#b98cff") };

    static Color C(string hex) => Color.FromHtml(hex);

    static Look Make(string[] grass, string[] stone, string[] sky, string[] far, string[] near, string haze) => new()
    {
        Grass = C(grass[0]), GrassLight = C(grass[1]), GrassDark = C(grass[2]), GrassSpeck = C(grass[3]), GrassSideL = C(grass[4]), GrassSideR = C(grass[5]),
        Stone = C(stone[0]), StoneLight = C(stone[1]), StoneMid = C(stone[2]), StoneJoint = C(stone[3]), StoneSideL = C(stone[4]), StoneSideR = C(stone[5]),
        SkyTop = C(sky[0]), SkyMid = C(sky[1]), SkyBottom = C(sky[2]),
        FarBody = C(far[0]), FarLights = far[1..].Select(C).ToArray(),
        NearBody = C(near[0]), NearLights = near[1..].Select(C).ToArray(),
        Haze = C(haze),
    };

    static readonly Dictionary<string, Look> Looks = new()
    {
        // The undercity: moss on cobbles under a night of neon windows.
        ["Undercity"] = Make(
            new[] { "#3e6140", "#5c8550", "#2b4630", "#8a9f5a", "#4a3b33", "#30262a" },
            new[] { "#6a7083", "#7b8194", "#5f6577", "#363a48", "#535869", "#383b4b" },
            new[] { "#0d0b1e", "#251537", "#120c1a" },
            new[] { "#1c1a33", "#f2b950", "#f7d58a" }, new[] { "#160f22", "#ff4fb8", "#4ff0a0", "#5fd0ff" }, "#2f6a55"),
        // The city above: marble and gardens at sunset, brass towers lit gold.
        ["Uptown"] = Make(
            new[] { "#4f7a3e", "#6fa04f", "#3a5c30", "#e8c86a", "#6b5a48", "#4a3d33" },
            new[] { "#cfc7b8", "#e6dfd2", "#b9b0a1", "#8f8678", "#a39a8c", "#7c7468" },
            new[] { "#2b3f6b", "#d98a5a", "#4a3a5a" },
            new[] { "#6b5a7a", "#ffd27a", "#ffe3a8" }, new[] { "#3b2f4a", "#ffc25a", "#7fe0e0" }, "#f2b57a"),
        // The sump: black moss, slick slate, a green chem haze.
        ["Mire"] = Make(
            new[] { "#2f4a2c", "#46663a", "#22361f", "#7fbf5a", "#2f2a24", "#1f1b18" },
            new[] { "#4d5250", "#5e6461", "#444947", "#2a2d2c", "#3a3e3d", "#2a2d2c" },
            new[] { "#08130f", "#123326", "#0a1a14" },
            new[] { "#102a20", "#4ff0a0", "#ff4fb8" }, new[] { "#0b1c16", "#4ff0a0", "#9fffc8" }, "#4ff0a0"),
        // The ruins: dry grass and weathered stone in a red dusk.
        ["Ruins"] = Make(
            new[] { "#5a6b3a", "#7a8c4a", "#43512d", "#b8a25a", "#5a4636", "#3d2f26" },
            new[] { "#8a7f72", "#a09484", "#7a7064", "#4f483f", "#6e6458", "#544c43" },
            new[] { "#1d1830", "#6b3b4a", "#2a1a26" },
            new[] { "#3a2a3e", "#f2b950" }, new[] { "#24182a", "#ffb04f", "#ff7a4f" }, "#c46a4a"),
        // Daylight skies for the daytime styles (only the sky colours are used).
        ["Day"] = Make(
            new[] { "#3e6140", "#5c8550", "#2b4630", "#8a9f5a", "#4a3b33", "#30262a" },
            new[] { "#6a7083", "#7b8194", "#5f6577", "#363a48", "#535869", "#383b4b" },
            new[] { "#5d93d6", "#a9d0ec", "#e8e4cf" },
            new[] { "#8fa4c4", "#dfe7f2" }, new[] { "#6f7f9c", "#cfd8e6" }, "#f0e6c8"),
        ["DayWarm"] = Make(
            new[] { "#3e6140", "#5c8550", "#2b4630", "#8a9f5a", "#4a3b33", "#30262a" },
            new[] { "#6a7083", "#7b8194", "#5f6577", "#363a48", "#535869", "#383b4b" },
            new[] { "#7f9cb8", "#d9c9a8", "#b89a76" },
            new[] { "#8c8a96", "#efe2c4" }, new[] { "#6f6a70", "#e8d2a8" }, "#e6c89a"),
        // The pass: snow over blue rock under a cold night with an aurora.
        ["Frost"] = Make(
            new[] { "#d8e6f0", "#f4fbff", "#a9bfd4", "#8fb0d0", "#8a9db4", "#66788f" },
            new[] { "#6f7f96", "#8a9ab0", "#62718a", "#3e4a5e", "#55627a", "#3c465a" },
            new[] { "#0b1530", "#2a5a7a", "#16243f" },
            new[] { "#22304e", "#9fe8ff", "#7fffc8" }, new[] { "#16203a", "#bff4ff", "#7fd8ff" }, "#9fd0ff"),
        // The dunes: scrub and sandstone under a burning dusk.
        ["Desert"] = Make(
            new[] { "#c9a868", "#e0c48a", "#a8864e", "#7a8a4a", "#a07a4a", "#7a5a36" },
            new[] { "#c08a5a", "#d8a878", "#b07c50", "#7a5234", "#9a6a42", "#744e30" },
            new[] { "#2a1f4a", "#d8704a", "#5a3048" },
            new[] { "#5a3a4a", "#ffb04f" }, new[] { "#3a2430", "#ffd27a", "#ff8a4f" }, "#f2a65a"),
        // The rift: ash and basalt under a red sky.
        ["Volcanic"] = Make(
            new[] { "#4a4446", "#5e5658", "#363032", "#ff7a3a", "#3a3234", "#262022" },
            new[] { "#3a3640", "#4e4a56", "#34303a", "#1c1a20", "#2c2830", "#1e1b22" },
            new[] { "#120608", "#4a1410", "#1e0a0a" },
            new[] { "#2a0e0e", "#ff6a2a", "#ffb04f" }, new[] { "#1a0808", "#ff4a1a", "#ffd27a" }, "#ff6a2a"),
        // The coast: dune grass and sea rock at twilight.
        ["Coast"] = Make(
            new[] { "#7a9a5a", "#9aba6a", "#5a7a46", "#e8e0b0", "#8a7a5a", "#6a5a42" },
            new[] { "#7a8490", "#949eaa", "#6c7682", "#454c56", "#5e6672", "#454c56" },
            new[] { "#18284a", "#5a7ab0", "#e8a87a" },
            new[] { "#2a3a5a", "#ffd27a" }, new[] { "#1a2640", "#ffe3a8", "#7fe0e0" }, "#9fc8e8"),
        // The home base: deep grass and warm cobbles at dusk, the city's lights far off.
        ["Ranch"] = Make(
            new[] { "#4a7a3a", "#6a9a4a", "#365c2c", "#e8d06a", "#6b5038", "#4a3828" },
            new[] { "#8a7e70", "#a09484", "#7a6e62", "#4f463e", "#6e6458", "#544a40" },
            new[] { "#1e2148", "#c8785a", "#3a2a4a" },
            new[] { "#3a3050", "#ffd27a", "#ffe3a8" }, new[] { "#2a2238", "#ffc25a", "#ff9a6a" }, "#e8a87a"),
        // The hollow: fungal moss and dark rock, lit by crystal.
        ["Cavern"] = Make(
            new[] { "#3a2f5a", "#57458a", "#2a2242", "#7fd8ff", "#2b2236", "#1d1726" },
            new[] { "#3f3d4f", "#514e64", "#383647", "#232130", "#302e3d", "#22202c" },
            new[] { "#07060d", "#120d22", "#06050b" },
            new[] { "#151022", "#b9a2ff", "#7fd8ff" }, new[] { "#0e0a18", "#b9a2ff", "#e6dcff" }, "#6a4bd1"),
    };

    static Look()
    {
        // The city above: cream plaster, terracotta roofs, warm windows.
        var p = Looks["Uptown"];
        (p.Roof, p.RoofLight, p.RoofDark) = (C("#a8553a"), C("#c46c4a"), C("#74382a"));
        (p.WallL, p.WallR, p.Timber, p.Door) = (C("#d9c9a8"), C("#b0a080"), C("#6a4a32"), C("#4a2e1e"));
        p.Windows = new[] { C("#ffe3a8"), C("#ffd27a"), C("#9fd8e8") };
        p.Flowers = new[] { C("#ff5f6f"), C("#ffffff"), C("#ffd25a"), C("#c08aff") };
        // The sump: rusted tin roofs on plank shacks, chem-lit windows.
        var s = Looks["Mire"];
        (s.Roof, s.RoofLight, s.RoofDark) = (C("#7a5a3a"), C("#8f6a45"), C("#4f3a26"));
        (s.WallL, s.WallR, s.Timber, s.Door) = (C("#5a4632"), C("#3e3024"), C("#2a2018"), C("#1e1610"));
        s.Windows = new[] { C("#4ff0a0"), C("#9fffc8") };
        // The ruins: weathered stone walls, broken slate.
        var r = Looks["Ruins"];
        (r.Roof, r.RoofLight, r.RoofDark) = (C("#5a5048"), C("#706458"), C("#3a332e"));
        (r.WallL, r.WallR, r.Timber, r.Door) = (C("#8a7f72"), C("#6e6458"), C("#4f483f"), C("#2e2620"));
        r.Windows = new[] { C("#ffb04f"), C("#1a1a2a") };
        r.Flowers = new[] { C("#ffb04f"), C("#ff7a4f") };
        // The pass: snow-laden roofs on log walls.
        var f = Looks["Frost"];
        (f.Roof, f.RoofLight, f.RoofDark) = (C("#dce8f2"), C("#f4fbff"), C("#9fb4c8"));
        (f.WallL, f.WallR, f.Timber, f.Door) = (C("#7a5a3e"), C("#5a422e"), C("#3a2a1e"), C("#2a1e14"));
        f.Windows = new[] { C("#ffd27a") };
        // The home base: barn-red roofs over timber and whitewash, lamplit windows.
        var h = Looks["Ranch"];
        (h.Roof, h.RoofLight, h.RoofDark) = (C("#8a3a2e"), C("#a8503e"), C("#5a2620"));
        (h.WallL, h.WallR, h.Timber, h.Door) = (C("#e0d4b8"), C("#b8aa8e"), C("#5a3e2a"), C("#3e2a1c"));
        h.Windows = new[] { C("#ffd27a"), C("#ffe3a8") };
        h.Flowers = new[] { C("#ffd25a"), C("#ff7a8a"), C("#ffffff"), C("#c08aff") };
        // The coast: white plaster and blue-grey slate.
        var co = Looks["Coast"];
        (co.Roof, co.RoofLight, co.RoofDark) = (C("#4a6a8a"), C("#6a8aaa"), C("#2e4660"));
        (co.WallL, co.WallR) = (C("#e8e0d0"), C("#c0b8a8"));
        co.Flowers = new[] { C("#ffffff"), C("#ffd25a"), C("#c08aff") };
    }

    public static Look Get(string name) => Looks.TryGetValue(name ?? "", out var l) ? l : Looks["Undercity"];
}
