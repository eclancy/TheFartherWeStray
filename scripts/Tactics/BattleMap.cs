using System.Collections.Generic;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.Tactics;

public enum Terrain { Grass, Stone, Brass, Chem, Pit, Crystal }

/// <summary>
/// An isometric battle map: a grid of tiles, each with a terrain and a height (FFTA-style), plus the
/// region it belongs to: its look and the fauna that lives there. Maps are handmade; the creatures
/// on them are generated (game-ideas.md, MVP scope).
/// </summary>
public sealed class BattleMap
{
    public readonly int W, H;
    public readonly string Name;
    readonly Terrain[] _terrain;
    readonly int[] _height;

    public string Blurb;
    public string Theme = "Undercity";
    public Vector2I PlayerAnchor, WildAnchor;

    // Region fauna: multipliers on the odds of each body plan, strain and element turning up here.
    public Dictionary<BodyPlan, float> Plans = new();
    public Dictionary<Strain, float> Strains = new();
    public Dictionary<Element, float> Elements = new();

    BattleMap(string name, string[] terrain, string[] heights)
    {
        Name = name;
        H = terrain.Length;
        W = terrain[0].Length;
        _terrain = new Terrain[W * H];
        _height = new int[W * H];
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            _terrain[y * W + x] = terrain[y][x] switch
            {
                ',' => Terrain.Stone,
                'b' => Terrain.Brass,
                '~' => Terrain.Chem,
                ' ' => Terrain.Pit,
                'x' => Terrain.Crystal,
                _ => Terrain.Grass,
            };
            _height[y * W + x] = heights[y][x] - '0';
        }
    }

    public bool InBounds(Vector2I p) => p.X >= 0 && p.Y >= 0 && p.X < W && p.Y < H;
    public Terrain TerrainAt(Vector2I p) => _terrain[p.Y * W + p.X];
    public int HeightAt(Vector2I p) => _height[p.Y * W + p.X];

    /// <summary>How well a species fits this region (the odds multiplier for it turning up here).</summary>
    public float Fit(Species s) =>
        (Plans.TryGetValue(s.Plan, out float p) ? p : 1f) * (Strains.TryGetValue(s.Strain, out float st) ? st : 1f)
        * (Elements.TryGetValue(s.Element, out float e) ? e : 1f);

    public static readonly string[] Names = { "Undercity Foundry", "Brass Bridges", "Sump Marsh", "Clocktower Ruins", "Hexlight Hollow" };

    public static BattleMap ByName(string name) => name switch
    {
        "Brass Bridges" => Bridges(),
        "Sump Marsh" => Sump(),
        "Clocktower Ruins" => Clocktower(),
        "Hexlight Hollow" => Hollow(),
        _ => Foundry(),
    };

    // Map legend: '.' moss/grass, ',' stone, 'b' brass, '~' chem, ' ' pit, 'x' crystal.
    // x runs down-right on screen, y down-left; the wild pack starts at the back.

    /// <summary>A foundry yard in the undercity: cobbles, moss, raised brass, a chem canal, a pit.</summary>
    public static BattleMap Foundry() => new("Undercity Foundry",
        new[]
        {
            ",,,,bb..,,",
            ",..,bb..,,",
            "..~~~~....",
            "..~~~~..,,",
            ",,..,,..,,",
            "..,,  ,,..",
            ".bb,  ,,..",
            ".bb,,,....",
            "..,,,,..,.",
        },
        new[]
        {
            "1111220011",
            "1001220011",
            "0000000000",
            "0000000011",
            "0000110011",
            "0011001100",
            "0220001100",
            "0220000000",
            "0000000010",
        })
    {
        Blurb = "A foundry yard in the undercity. Brass platforms, a canal of chem.",
        WildAnchor = new Vector2I(2, 0), PlayerAnchor = new Vector2I(7, 7),
    };

    /// <summary>Plazas of the city above, joined by brass bridges over a chasm: fliers rule here.</summary>
    public static BattleMap Bridges() => new("Brass Bridges",
        new[]
        {
            ",,,,  ,,,,",
            ",bb,  ,..,",
            ",bb,  ,..,",
            ",,,,bb,,,,",
            ",,..bb..,,",
            ",,,,bb,,,,",
            ",..,  ,bb,",
            ",..,  ,bb,",
            ",,,,  ,,,,",
        },
        new[]
        {
            "1111001111",
            "1221001111",
            "1221001111",
            "1111111111",
            "1111111111",
            "1111111111",
            "1111001221",
            "1111001221",
            "1111001111",
        })
    {
        Blurb = "Marble plazas of the city above, bridged over a chasm. Fliers own the gaps.",
        Theme = "Piltover",
        WildAnchor = new Vector2I(1, 0), PlayerAnchor = new Vector2I(8, 8),
        Plans = { [BodyPlan.Avian] = 3f, [BodyPlan.Insectoid] = 1.5f, [BodyPlan.Aquatic] = 0.2f },
        Strains = { [Strain.Clockwork] = 3f, [Strain.Abyssal] = 0.4f },
        Elements = { [Element.Spark] = 2f, [Element.Stone] = 1.5f },
    };

    /// <summary>A chem-flooded sump below the city: islands and causeways, swimmers' ground.</summary>
    public static BattleMap Sump() => new("Sump Marsh",
        new[]
        {
            "..~~..,,..",
            "..~~..,,..",
            "..,,..~~~~",
            "~~,,~~~~..",
            "..,,......",
            "..~~~~,,~~",
            "~~..~~,,..",
            "......,,..",
            "..b...~~..",
        },
        new[]
        {
            "0000001100",
            "0000001100",
            "0011000000",
            "0011000011",
            "0000000011",
            "0000001100",
            "0011001100",
            "0000000000",
            "0100000000",
        })
    {
        Blurb = "Islands in a sump of glowing chem. Swimmers and mutants thrive.",
        Theme = "Sump",
        WildAnchor = new Vector2I(7, 0), PlayerAnchor = new Vector2I(1, 8),
        Plans = { [BodyPlan.Aquatic] = 3f, [BodyPlan.Serpent] = 2f, [BodyPlan.Blob] = 2f, [BodyPlan.Avian] = 0.5f },
        Strains = { [Strain.Chemtech] = 3f, [Strain.Clockwork] = 0.5f },
        Elements = { [Element.Venom] = 2.5f, [Element.Tide] = 2f, [Element.Leaf] = 1.5f },
    };

    /// <summary>A fallen clocktower: steep tiers of stone and brass. The high ground decides it.</summary>
    public static BattleMap Clocktower() => new("Clocktower Ruins",
        new[]
        {
            ",,,,bbb,,,",
            ",,..bbb..,",
            "..,,,,,,..",
            "..,,  ,,..",
            "..,,  ,,..",
            "..,,,,,,..",
            ",..,bb,..,",
            ",..,bb,...",
            ",,,,,,,,,,",
        },
        new[]
        {
            "2222333222",
            "2211333112",
            "1111222111",
            "0011002110",
            "0011002110",
            "0011111100",
            "0001221000",
            "0001221000",
            "0000000000",
        })
    {
        Blurb = "The tiers of a fallen clocktower. Whoever holds the top hits hardest.",
        Theme = "Ruins",
        WildAnchor = new Vector2I(5, 0), PlayerAnchor = new Vector2I(4, 8),
        Plans = { [BodyPlan.Biped] = 2f, [BodyPlan.Quadruped] = 2f, [BodyPlan.Aquatic] = 0.3f },
        Strains = { [Strain.Clockwork] = 1.5f, [Strain.Arcane] = 1.5f },
        Elements = { [Element.Stone] = 2f, [Element.Ember] = 1.5f },
    };

    /// <summary>A cavern lit by hextech crystals growing out of the rock. Arcane things nest here.</summary>
    public static BattleMap Hollow() => new("Hexlight Hollow",
        new[]
        {
            ",,xx,,,,..",
            ",xxx,,..,,",
            ",,,,~~,,,,",
            "..,,~~,xx,",
            "..,,,,,xx,",
            ",,,,..,,,,",
            ",xx,..~~,,",
            ",xx,,,~~,.",
            ",,,,..,,..",
        },
        new[]
        {
            "1122111100",
            "1222110011",
            "1111001111",
            "0011001221",
            "0011111221",
            "0000110000",
            "0220110000",
            "0220000000",
            "0000000000",
        })
    {
        Blurb = "A cavern lit by raw hextech crystal. Arcane and abyssal things nest in the dark.",
        Theme = "Cavern",
        WildAnchor = new Vector2I(2, 0), PlayerAnchor = new Vector2I(7, 8),
        Plans = { [BodyPlan.Insectoid] = 2f, [BodyPlan.Blob] = 1.5f, [BodyPlan.Serpent] = 1.5f, [BodyPlan.Avian] = 0.5f },
        Strains = { [Strain.Arcane] = 3f, [Strain.Abyssal] = 2.5f, [Strain.Natural] = 0.6f },
        Elements = { [Element.Shade] = 2f, [Element.Frost] = 1.5f, [Element.Venom] = 1.5f },
    };
}
