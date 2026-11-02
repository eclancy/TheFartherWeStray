using Godot;

namespace CreatureTactics.Tactics;

public enum Terrain { Grass, Stone, Brass, Chem, Pit }

/// <summary>
/// An isometric battle map: a grid of tiles, each with a terrain and a height (FFTA-style). Maps
/// are handmade; the fauna on them is generated (game-ideas.md, MVP scope).
/// </summary>
public sealed class BattleMap
{
    public readonly int W, H;
    public readonly string Name;
    readonly Terrain[] _terrain;
    readonly int[] _height;

    public Vector2I PlayerAnchor, WildAnchor;

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
                _ => Terrain.Grass,
            };
            _height[y * W + x] = heights[y][x] - '0';
        }
    }

    public bool InBounds(Vector2I p) => p.X >= 0 && p.Y >= 0 && p.X < W && p.Y < H;
    public Terrain TerrainAt(Vector2I p) => _terrain[p.Y * W + p.X];
    public int HeightAt(Vector2I p) => _height[p.Y * W + p.X];

    /// <summary>
    /// A foundry yard in the undercity: cobbles, moss, raised brass platforms, a canal of glowing
    /// chem, and a pit. x runs down-right on screen, y down-left; the wild pack starts at the back.
    /// </summary>
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
        WildAnchor = new Vector2I(2, 0),
        PlayerAnchor = new Vector2I(7, 7),
    };
}
