using System;
using System.Collections.Generic;
using System.Linq;
using TheFartherWeStray.Creatures;
using Godot;

namespace TheFartherWeStray.Tactics;

public enum Terrain { Grass, Stone, Brass, Chem, Pit, Crystal, Roof, Wood, Dirt, Water, Ice, Sand, Lava }

/// <summary>
/// An isometric battle map: a grid of tiles, each with a terrain and a height (FFTA-style), the props
/// standing on it (trees, lamps, crates, statues), and the region it belongs to: its look and the
/// fauna that lives there. Maps are handmade; the creatures on them are generated (game-ideas.md,
/// MVP scope). Buildings are tiles too: roofs raised on walls, which walkers go round and fliers
/// and climbers can stand on, as in FFTA's towns.
/// </summary>
public sealed partial class BattleMap
{
    public readonly int W, H;
    public readonly string Name;
    readonly Terrain[] _terrain;
    readonly int[] _height;

    public string Blurb;
    public string Theme = "Undercity";
    public Vector2I PlayerAnchor, WildAnchor;

    /// <summary>What stands on the map, tile by tile, and which tiles that fills.</summary>
    public readonly List<(Vector2I tile, Prop prop)> Props = new();
    readonly HashSet<Vector2I> _blocked = new();

    // Region fauna: multipliers on the odds of each body plan, strain and element turning up here.
    public Dictionary<BodyPlan, float> Plans = new();
    public Dictionary<Strain, float> Strains = new();
    public Dictionary<Element, float> Elements = new();

    BattleMap(string name, string[] terrain, string[] heights, string[] props = null)
    {
        Name = name;
        H = terrain.Length;
        W = terrain[0].Length;
        _terrain = new Terrain[W * H];
        _height = new int[W * H];
        for (int y = 0; y < H; y++)
        {
            if (terrain[y].Length != W || heights[y].Length != W || props != null && props[y].Length != W)
                throw new System.ArgumentException($"{name}: row {y} isn't {W} wide");
            for (int x = 0; x < W; x++)
            {
                _terrain[y * W + x] = terrain[y][x] switch
                {
                    ',' => Terrain.Stone,
                    'b' => Terrain.Brass,
                    '~' => Terrain.Chem,
                    ' ' => Terrain.Pit,
                    'x' => Terrain.Crystal,
                    'h' => Terrain.Roof,
                    'w' => Terrain.Wood,
                    '-' => Terrain.Dirt,
                    '=' => Terrain.Water,
                    'i' => Terrain.Ice,
                    's' => Terrain.Sand,
                    'l' => Terrain.Lava,
                    _ => Terrain.Grass,
                };
                _height[y * W + x] = heights[y][x] - '0';
                if (props != null && PropArt.FromChar(props[y][x]) is { } prop)
                {
                    var t = new Vector2I(x, y);
                    Props.Add((t, prop));
                    if (PropArt.Blocks(prop)) _blocked.Add(t);
                }
            }
        }
    }

    public bool InBounds(Vector2I p) => p.X >= 0 && p.Y >= 0 && p.X < W && p.Y < H;
    public Terrain TerrainAt(Vector2I p) => _terrain[p.Y * W + p.X];
    public int HeightAt(Vector2I p) => _height[p.Y * W + p.X];
    /// <summary>A tree, a crate, a statue: nothing can stand here.</summary>
    public bool Blocked(Vector2I p) => _blocked.Contains(p);

    /// <summary>How well a species fits this region (the odds multiplier for it turning up here).</summary>
    public float Fit(Species s) =>
        (Plans.TryGetValue(s.Plan, out float p) ? p : 1f) * (Strains.TryGetValue(s.Strain, out float st) ? st : 1f)
        * (Elements.TryGetValue(s.Element, out float e) ? e : 1f);

    public static readonly string[] Names =
    {
        "Undercity Foundry", "Brass Bridges", "Glowmire", "Clocktower Ruins", "Hexlight Hollow", "Lantern Row", "Gilded Gardens",
        "Frostfang Pass", "Sunscorch Dunes", "Cinder Rift", "Saltwind Coast", "The Grand Coliseum",
    };

    /// <summary>A map laid out elsewhere (the home base), from the same three grids.</summary>
    public static BattleMap Custom(string name, string[] terrain, string[] heights, string[] props, string theme) => new(name, terrain, heights, props) { Theme = theme };

    public static BattleMap ByName(string name) => name switch
    {
        "Brass Bridges" => Grow(Bridges(), TownRing),
        "Glowmire" => Sump(),
        "Clocktower Ruins" => Grow(Clocktower(), TownRing),
        "Hexlight Hollow" => Hollow(),
        "Lantern Row" => Grow(LanternRow(), TownRing),
        "Gilded Gardens" => Grow(Gardens(), TownRing),
        "Frostfang Pass" => Frost(),
        "Sunscorch Dunes" => Dunes(),
        "Cinder Rift" => Cinder(),
        "Saltwind Coast" => Coast(),
        "The Grand Coliseum" => Coliseum(),
        _ => Grow(Foundry(), TownRing),
    };

    /// <summary>How many tiles of streets and houses the town maps grow by on every side.</summary>
    public const int TownRing = 3;

    BattleMap(string name, int w, int h)
    {
        Name = name;
        W = w;
        H = h;
        _terrain = new Terrain[w * h];
        _height = new int[w * h];
    }

    /// <summary>
    /// A town map grown by a ring of city on every side (the user's ask, 2026-11-02: bigger maps, a
    /// proper city): the handmade map whole in the middle, then a street round its edge, then blocks
    /// of houses cut by alleys. The blocks step up a storey at a time (a row of sheds, then the houses
    /// behind), so every roof can be climbed to: nothing can perch where nobody can follow. Lamps,
    /// signs and crates stand along the street now and then. The anchors move with the middle.
    /// </summary>
    static BattleMap Grow(BattleMap core, int ring)
    {
        var m = new BattleMap(core.Name, core.W + 2 * ring, core.H + 2 * ring)
        {
            Blurb = core.Blurb, Theme = core.Theme, Plans = core.Plans, Strains = core.Strains, Elements = core.Elements,
            PlayerAnchor = core.PlayerAnchor + new Vector2I(ring, ring), WildAnchor = core.WildAnchor + new Vector2I(ring, ring),
        };
        uint salt = (uint)SeededRng.Hash(core.Name);
        uint R(int x, int y, int k) { uint h = (uint)(x * 374761393 + y * 668265263 + (int)(salt % 100000) * 31 + k * 2246822519); h = (h ^ (h >> 13)) * 1274126177; return h ^ (h >> 16); }
        bool uptown = core.Theme == "Uptown", ruins = core.Theme == "Ruins";
        for (int y = 0; y < m.H; y++)
        for (int x = 0; x < m.W; x++)
        {
            var t = new Vector2I(x, y);
            var c = t - new Vector2I(ring, ring); // in the core's own tiles
            int i = y * m.W + x;
            if (core.InBounds(c))
            {
                m._terrain[i] = core.TerrainAt(c);
                m._height[i] = core.HeightAt(c);
                continue;
            }
            // How far out of the middle, and which way the ring runs here (its blocks are cut along it).
            int dx = c.X < 0 ? -c.X : c.X >= core.W ? c.X - core.W + 1 : 0, dy = c.Y < 0 ? -c.Y : c.Y >= core.H ? c.Y - core.H + 1 : 0;
            int d = Math.Max(dx, dy);
            int along = dx >= dy ? c.Y : c.X;
            bool alley = along % 4 == 0 || dx > 0 && dy > 0 && (dx + dy) % 3 == 0;
            uint r = R(x, y, 0);
            Terrain terrain;
            int height;
            if (d == 1 || alley)
            {
                // The street round the old edge, and the alleys out through the blocks.
                terrain = uptown && r % 4 == 0 ? Terrain.Grass : ruins && r % 3 == 0 ? Terrain.Grass : Terrain.Stone;
                height = 0;
            }
            else
            {
                // Sheds a storey high along the street, houses two storeys behind them (ruins lower).
                terrain = ruins && r % 3 == 0 ? Terrain.Grass : Terrain.Roof;
                height = terrain == Terrain.Grass ? 0 : ruins ? 1 : Math.Min(2, d - 1);
            }
            m._terrain[i] = terrain;
            m._height[i] = height;
        }
        // The middle's props, moved with it; a few more along the new street.
        foreach (var (tile, prop) in core.Props) m.Add(tile + new Vector2I(ring, ring), prop);
        for (int y = 0; y < m.H; y++)
        for (int x = 0; x < m.W; x++)
        {
            var t = new Vector2I(x, y);
            if (core.InBounds(t - new Vector2I(ring, ring)) || m.TerrainAt(t) == Terrain.Roof || m.HeightAt(t) != 0) continue;
            uint r = R(x, y, 9);
            if (r % 11 != 0) continue;
            // Never next to another prop, so the street stays open.
            if (m.Props.Any(p => Math.Abs(p.tile.X - x) + Math.Abs(p.tile.Y - y) <= 2)) continue;
            m.Add(t, core.Theme switch
            {
                "Uptown" => r % 3 == 0 ? Prop.Tree : r % 3 == 1 ? Prop.Lamp : Prop.Statue,
                "Ruins" => r % 2 == 0 ? Prop.Rock : Prop.DeadTree,
                _ => r % 4 == 0 ? Prop.Neon : r % 4 == 1 ? Prop.BrokenLamp : r % 4 == 2 ? Prop.Crate : Prop.Lamp,
            });
        }
        return m;
    }

    void Add(Vector2I t, Prop prop)
    {
        Props.Add((t, prop));
        if (PropArt.Blocks(prop)) _blocked.Add(t);
    }

    // Terrain: '.' moss/grass, ',' stone, 'b' brass, '~' chem, ' ' pit, 'x' crystal, 'h' roof,
    //          'w' wooden boards, '-' dirt, '=' water.
    // Props:   'T' tree, 'P' pine, 'D' dead tree, 'u' bush, 'M' mushroom, 'C' crystals, 'o' rock,
    //          'L' lamp, 'N' neon sign, 'c' crate, 'B' barrel, 'K' chem tank, 'G' gear works,
    //          'S' statue, 'F' fountain, 'I' pillar, 'm' market stall, 'f' banner, 'r' reeds,
    //          'g' grave, 'e' hedge, 'H' chimney (on a roof). Reeds and banners don't block.
    // x runs down-right on screen, y down-left; the wild pack starts at the back.

    /// <summary>A foundry yard in the undercity: works and sheds, a chem canal with a footbridge, a pit.</summary>
    public static BattleMap Foundry() => new("Undercity Foundry",
        new[]
        {
            ",,,,hhhh,,bb..,,",
            ",..,hhhh,,bb..,,",
            ",..,hhhh,,,,..,,",
            "..,,,,,,,,..,,,,",
            "~~~~~~ww~~~~~~,,",
            "~~~~~~ww~~~~~~,,",
            ",,..,,,,..,,,,..",
            ",,..,,  ,,..bb..",
            ".bb.,,  ,,..bb..",
            ".bb.,,,,,,,,,,hh",
            "..,,,,..,,,,,,hh",
            "..,,hhh,,,..,,hh",
            "..,,hhh,,,..,,,,",
        },
        new[]
        {
            "1111333311220011",
            "1001333311220011",
            "1001333311110011",
            "0011111111000011",
            "0000000000000011",
            "0000000000000011",
            "0000000000000000",
            "0000000000000000",
            "0220000000001100",
            "0220000000001133",
            "0000000000000033",
            "0000333000000033",
            "0000333000000000",
        },
        new[]
        {
            "....H...c.....L.",
            ".c............B.",
            ".B.......K......",
            "l..v..N.....yo..",
            "................",
            "................",
            ".L....c....G....",
            "...........B.L..",
            "........c.......",
            "....l..v......H.",
            ".c...B.....N....",
            ".....H..........",
            "L.........c.....",
        })
    {
        Blurb = "A foundry yard in the undercity. Brass platforms, works and sheds, a canal of chem.",
        WildAnchor = new Vector2I(12, 1), PlayerAnchor = new Vector2I(9, 11),
    };

    /// <summary>Plazas of the city above, joined by brass bridges over a chasm: fliers rule here.</summary>
    public static BattleMap Bridges() => new("Brass Bridges",
        new[]
        {
            ",,,,,,    ,,,,,,",
            ",hhh,,    ,..,,,",
            ",hhh,,    ,..,,,",
            ",,,,,,bbbb,,,,,,",
            ",..,,,    ,,,hh,",
            ",..,,,    ,,,hh,",
            ",,,,,,bbbb,,,,,,",
            ",,,,,,    ,,,,,,",
            ",hh,..    ..,,,,",
            ",hh,..    ..,hhh",
            ",,,,,,bbbb,,,hhh",
            ",..,,,    ,,,,,,",
            ",,,,,,    ,,,,,,",
        },
        new[]
        {
            "1111110000111111",
            "1333110000111111",
            "1333110000111111",
            "1111111111111111",
            "1111110000111441",
            "1111110000111441",
            "1111111111111111",
            "1111110000111111",
            "1331110000111111",
            "1331110000111444",
            "1111111111111444",
            "1111110000111111",
            "1111110000111111",
        },
        new[]
        {
            "L....L....L....L",
            "..H...........S.",
            ".....e....F.....",
            "................",
            ".T...f..........",
            "..u.............",
            "................",
            "S..............e",
            "....T.......c...",
            ".H..........m...",
            "e...............",
            ".u..L.....L.....",
            "................",
        })
    {
        Blurb = "Marble plazas of the city above, bridged over a chasm. Fliers own the gaps.",
        Theme = "Uptown",
        WildAnchor = new Vector2I(2, 0), PlayerAnchor = new Vector2I(12, 12),
        Plans = { [BodyPlan.Avian] = 3f, [BodyPlan.Insectoid] = 1.5f, [BodyPlan.Aquatic] = 0.2f },
        Strains = { [Strain.Clockwork] = 3f, [Strain.Abyssal] = 0.4f },
        Elements = { [Element.Spark] = 2f, [Element.Stone] = 1.5f },
    };

    /// <summary>A sump below the city: islands, boardwalks and stilt shacks over water and chem.</summary>
    public static BattleMap Sump() => new("Glowmire",
        new[]
        {
            "..==..,,..~~~~..",
            "..==..,,..~~~~..",
            "..,,wwww..==..hh",
            "==,,..==..==..hh",
            "==..........ww..",
            "..~~~~,,~~==ww..",
            "..~~~~,,~~==....",
            "hh..==..ww....==",
            "hh..==..ww..,,==",
            "....~~~~....,,..",
            "..ww~~~~ww..~~..",
            "..ww....ww..~~..",
            "........==,,....",
            "..b.....==,,....",
        },
        new[]
        {
            "0000001100000000",
            "0000001100000000",
            "0011000000000022",
            "0011000000000022",
            "0000000000000000",
            "0000001100000000",
            "0000001100000000",
            "2200000000000000",
            "2200000000110000",
            "0000000000110000",
            "0000000000000000",
            "0000000000000000",
            "0000000000110000",
            "0010000000110000",
        },
        new[]
        {
            ".r..D.....r.....",
            "....r..........u",
            "D...........r...",
            "..B............H",
            "...r....u.......",
            "................",
            "..............D.",
            "......r.........",
            ".H..........L...",
            ".D............r.",
            "................",
            "....r.....u.....",
            ".u..D...........",
            "............L...",
        })
    {
        Blurb = "Islands in a sump of glowing chem, strung with boardwalks. Swimmers and mutants thrive.",
        Theme = "Mire",
        WildAnchor = new Vector2I(8, 1), PlayerAnchor = new Vector2I(2, 12),
        Plans = { [BodyPlan.Aquatic] = 3f, [BodyPlan.Serpent] = 2f, [BodyPlan.Blob] = 2f, [BodyPlan.Avian] = 0.5f },
        Strains = { [Strain.Chemtech] = 3f, [Strain.Clockwork] = 0.5f },
        Elements = { [Element.Venom] = 2.5f, [Element.Tide] = 2f, [Element.Leaf] = 1.5f },
    };

    /// <summary>A fallen clocktower: steep tiers of stone and brass among graves. The high ground decides it.</summary>
    public static BattleMap Clocktower() => new("Clocktower Ruins",
        new[]
        {
            ",,,,,,bbbb,,,,,,",
            ",,..,,bbbb,,..,,",
            ",,..,,,,,,,,..,,",
            "..,,,,,,,,,,,,..",
            "..,,,,  ,,,,,,..",
            "..,,,,  ,,hh,,..",
            "....,,,,,,hh,...",
            ",,..,,..,,,,,,,,",
            ",,..,,..,,bb,,,,",
            "......,,,,bb....",
            "hh....,,,,,,....",
            "hh..,,,,..,,..,,",
            "....,,,,..,,..,,",
            ",,,,,,,,,,,,,,,,",
        },
        new[]
        {
            "3333334444333333",
            "3322334444332233",
            "2222333333332222",
            "1122222222222211",
            "1122220022222211",
            "1122220022442211",
            "1111222222441111",
            "1111111111111111",
            "0011111111221100",
            "0000001111220000",
            "3300000111100000",
            "3300000000000000",
            "0000000000000000",
            "0000000000000000",
        },
        new[]
        {
            "I..........G...I",
            "..D..........D..",
            "....I......o....",
            ".g............g.",
            "...o......I.....",
            "................",
            "..g.........u...",
            "......I.........",
            "..o...........I.",
            "T.............T.",
            "..........o.....",
            ".H....g.........",
            "..u..........o..",
            "I..............I",
        })
    {
        Blurb = "The tiers of a fallen clocktower, broken pillars and old graves. Whoever holds the top hits hardest.",
        Theme = "Ruins",
        WildAnchor = new Vector2I(8, 1), PlayerAnchor = new Vector2I(8, 13),
        Plans = { [BodyPlan.Biped] = 2f, [BodyPlan.Quadruped] = 2f, [BodyPlan.Aquatic] = 0.3f },
        Strains = { [Strain.Clockwork] = 1.5f, [Strain.Arcane] = 1.5f },
        Elements = { [Element.Stone] = 2f, [Element.Ember] = 1.5f },
    };

    /// <summary>A cavern lit by hextech crystals and glowing fungus. Arcane things nest here.</summary>
    public static BattleMap Hollow() => new("Hexlight Hollow",
        new[]
        {
            ",,xxx,,,,..,,,,,",
            ",xxxx,,..,,,,xx,",
            ",,,,,,~~~,,,,xx,",
            "..,,,,~~~,,..,,,",
            "..,,,,,,,,,..,,,",
            ",,,,..,,,,,,,,xx",
            ",xx,..,,~~,,,,xx",
            ",xx,,,,,~~,,..,,",
            ",,,,..,,,,,,..,,",
            "..,,..,,xx,,,,,,",
            "..,,,,,,xx,,~~,,",
            ",,,,..,,,,,,~~,,",
            ",,..,,,,..,,,,,,",
        },
        new[]
        {
            "1122211111001111",
            "1222211001111221",
            "1111110001111221",
            "0011110001100111",
            "0011111111100111",
            "0000001111111122",
            "0220001100111122",
            "0220000000110000",
            "0000000000110000",
            "0000000022000000",
            "0000000022000000",
            "0000000000000000",
            "0000000000000000",
        },
        new[]
        {
            "C......M.....o..",
            ".....M.....o....",
            "..o..........C..",
            "M..........M....",
            "....C..........M",
            "..M.....o.......",
            "......M.....C...",
            "....o.....M.....",
            "..C.........M...",
            ".M.....o......C.",
            "....M...........",
            "..o.......M.....",
            "......C.....M...",
        })
    {
        Blurb = "A cavern lit by raw glowing crystal and fungus. Runic and abyssal things nest in the dark.",
        Theme = "Cavern",
        WildAnchor = new Vector2I(8, 1), PlayerAnchor = new Vector2I(8, 12),
        Plans = { [BodyPlan.Insectoid] = 2f, [BodyPlan.Blob] = 1.5f, [BodyPlan.Serpent] = 1.5f, [BodyPlan.Avian] = 0.5f },
        Strains = { [Strain.Arcane] = 3f, [Strain.Abyssal] = 2.5f, [Strain.Natural] = 0.6f },
        Elements = { [Element.Shade] = 2f, [Element.Frost] = 1.5f, [Element.Venom] = 1.5f },
    };

    /// <summary>An undercity street of narrow houses, market stalls and neon, round a little green.</summary>
    public static BattleMap LanternRow() => new("Lantern Row",
        new[]
        {
            "hhh,,hhhh,,hhh,,",
            "hhh,,hhhh,,hhh,,",
            ",,,,,,,,,,,,,,,,",
            ",,,,--------,,,,",
            "hh,,--------,,hh",
            "hh,,,,,,,,,,,,hh",
            ",,,,,,..,,,,,,,,",
            "hhh,,,..,,,,hhhh",
            "hhh,,,,,,,,,hhhh",
            ",,,,,,,,,,,,,,,,",
            ",,----,,----,,,,",
            "hh,,,,,,,,,,,hhh",
            "hh,,,,hhh,,,,hhh",
            ",,,,,,hhh,,,,,,,",
        },
        new[]
        {
            "3330033330033300",
            "3330033330033300",
            "0000000000000000",
            "0000000000000000",
            "2200000000000022",
            "2200000000000022",
            "0000000000000000",
            "3330000000003333",
            "3330000000003333",
            "0000000000000000",
            "0000000000000000",
            "2200000000000222",
            "2200003330000222",
            "0000003330000000",
        },
        new[]
        {
            ".H......H.....L.",
            "...c.......H...B",
            "l....N....L....N",
            "..m..........m..",
            "...B........c...",
            "................",
            ".......T..v.....",
            "...L.....F......",
            "..H.......c...H.",
            "N....v.........l",
            "......c......y..",
            "...........B....",
            "...l....H.......",
            "L....B.........L",
        })
    {
        Blurb = "A lamplit undercity street: tall narrow houses, market stalls, neon. Fliers take the rooftops.",
        WildAnchor = new Vector2I(8, 2), PlayerAnchor = new Vector2I(11, 13),
        Plans = { [BodyPlan.Biped] = 2f, [BodyPlan.Quadruped] = 1.5f, [BodyPlan.Avian] = 1.5f },
        Strains = { [Strain.Chemtech] = 1.5f, [Strain.Clockwork] = 1.5f },
        Elements = { [Element.Spark] = 1.5f, [Element.Shade] = 1.5f },
    };

    /// <summary>A walled garden of the city above: hedges, ponds, statues and a gazebo on the lawn.</summary>
    public static BattleMap Gardens() => new("Gilded Gardens",
        new[]
        {
            "....,,,,,,,,....",
            "..==..,,,,..==..",
            "..==..,,,,..==..",
            "......,,,,......",
            ",,,,,,,,,,,,,,,,",
            "..,,..,,,,..,,..",
            "..,,..,hh,..,,..",
            "..,,..,hh,..,,..",
            "..,,..,,,,..,,..",
            ",,,,,,,,,,,,,,,,",
            "......,,,,......",
            "..====,,,,......",
            "..====,,,,......",
        },
        new[]
        {
            "1111111111111111",
            "1100111111110011",
            "1100111111110011",
            "1111111111111111",
            "1111111111111111",
            "0000000000000000",
            "0000000220000000",
            "0000000220000000",
            "0000000000000000",
            "0000000000000000",
            "0000000000000000",
            "0000000000000000",
            "0000000000000000",
        },
        new[]
        {
            "T..e........e..T",
            "................",
            "................",
            ".u..S......S..u.",
            "................",
            "e.....L..L.....e",
            "..........T.....",
            ".T..............",
            "....u.....u.....",
            "................",
            "e..T......F...e.",
            "..............T.",
            "u.........S.....",
        })
    {
        Blurb = "A gilded garden of the city above: lawns, hedges, ponds and statues. Insects and birds everywhere.",
        Theme = "Uptown",
        WildAnchor = new Vector2I(7, 0), PlayerAnchor = new Vector2I(8, 12),
        Plans = { [BodyPlan.Avian] = 2f, [BodyPlan.Insectoid] = 2f, [BodyPlan.Quadruped] = 1.5f },
        Strains = { [Strain.Natural] = 1.5f, [Strain.Clockwork] = 1.5f },
        Elements = { [Element.Leaf] = 2.5f, [Element.Meadow] = 2f },
    };
}
