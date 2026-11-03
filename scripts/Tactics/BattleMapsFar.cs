using Godot;
using TheFartherWeStray.Creatures;

namespace TheFartherWeStray.Tactics;

/// <summary>
/// The far regions (the user's direction, 2026-11-02: missions in very different places, whose ground
/// all looks very different): snow and ice, desert sand, a volcanic rift, the coast, and the city's
/// arena. Their looks are in <see cref="Look"/>; ice, sand and lava are terrains of their own.
/// </summary>
public sealed partial class BattleMap
{
    /// <summary>A snowbound pass between blue rock ridges, round a frozen lake with one hole of open water and a trapper's hut.</summary>
    public static BattleMap Frost() => new("Frostfang Pass",
        new[]
        {
            ",,,.....,,....,,",
            ",,...........,,,",
            ".....iiiii......",
            "....iiiiiii.....",
            "...iiiiiiiii....",
            ",..iiii==iii..,,",
            ",,.iiiiiiiii.hh,",
            "...iiiiiiiii.hh.",
            "....iiiiiii.....",
            ",.....iiii....,,",
            ",,............,,",
            "...,,......,,...",
            "..,,,,....,,,,..",
        },
        new[]
        {
            "3321111111112333",
            "3211111111111233",
            "2111100000111123",
            "1111000000011112",
            "1110000000001111",
            "2110000000001122",
            "2210000000001331",
            "1110000000001331",
            "1111000000011111",
            "2111110000111122",
            "2211111111111122",
            "1112211111122111",
            "1122221111222211",
        },
        new[]
        {
            "P..o......P...P.",
            ".P.......o....P.",
            "P..............P",
            "..o..........o..",
            "................",
            "P...........C..P",
            "..............H.",
            "P...............",
            ".o..........o...",
            "P..............P",
            "..P.........P...",
            ".......o........",
            "P.o.........o..P",
        })
    {
        Blurb = "A snowbound pass round a frozen lake. Pines on the ridges, a trapper's hut, thin ice.", Theme = "Frost",
        WildAnchor = new Vector2I(8, 1), PlayerAnchor = new Vector2I(8, 11),
        Plans = { [BodyPlan.Quadruped] = 1.5f, [BodyPlan.Avian] = 1.5f, [BodyPlan.Blob] = 0.5f },
        Strains = { [Strain.Arcane] = 1.3f, [Strain.Chemtech] = 0.5f },
        Elements = { [Element.Frost] = 4f, [Element.Ember] = 0.3f, [Element.Tide] = 1.5f },
    };

    /// <summary>Sand dunes round the ruins of a sandstone plaza, with an oasis and a hard-packed caravan track.</summary>
    public static BattleMap Dunes() => new("Sunscorch Dunes",
        new[]
        {
            "sssss,,,,,,sssss",
            "ssss,,,,,,,,ssss",
            "sss,,,-ss-,,,sss",
            "sssssss--sssssss",
            "ssss..ss--ssssss",
            "sss.==.ss--s,,ss",
            "sss.==..ss-s,,ss",
            "ssss....s--sssss",
            "sssssss--sssssss",
            "ss,,ss--ssss,,ss",
            "ss,,ss-sss.sssss",
            "ssssss--.....sss",
            "sssssss--..sssss",
        },
        new[]
        {
            "1112222222211111",
            "1122222222221111",
            "1122211111221111",
            "1111111111111111",
            "2111001111112222",
            "2110000111112222",
            "2110000111112222",
            "1111000111111111",
            "1111111111111111",
            "1122111111112211",
            "1122111111111111",
            "1111110000011111",
            "1111111000111111",
        },
        new[]
        {
            "D....I....I...o.",
            "....S......S....",
            "o..I........I...",
            "................",
            "..o.......o....D",
            "...r..D.........",
            "...r............",
            "o...........I..o",
            "................",
            "..I.........I...",
            ".o...........o..",
            "D...............",
            "...o........D...",
        })
    {
        Blurb = "Dunes round a ruined sandstone plaza. Sand slows walkers; the caravan track and the oasis don't.", Theme = "Desert",
        WildAnchor = new Vector2I(8, 1), PlayerAnchor = new Vector2I(8, 11),
        Plans = { [BodyPlan.Serpent] = 2f, [BodyPlan.Insectoid] = 2f, [BodyPlan.Aquatic] = 0.3f },
        Strains = { [Strain.Abyssal] = 1.3f },
        Elements = { [Element.Ember] = 2.5f, [Element.Stone] = 2f, [Element.Tide] = 0.4f, [Element.Frost] = 0.2f },
    };

    /// <summary>Basalt ledges over rivers of lava, crossed by stone necks; obsidian juts from the ash. Only fliers cross the lava.</summary>
    public static BattleMap Cinder() => new("Cinder Rift",
        new[]
        {
            ",,,,....,,,,,,,,",
            ",,............,,",
            "lll,,..lll,,,lll",
            "llll,,lllll,,lll",
            "....,,.......,..",
            "..,,,,..,,..,,..",
            "..........lll...",
            "llll...llllll...",
            "lllll..lllll....",
            "..........,,....",
            "..,,......,,....",
            ".....,,,........",
            "...,,,,,,,,.....",
        },
        new[]
        {
            "3333222233333333",
            "3322222222222233",
            "0002222000222000",
            "0000220000022000",
            "2222222222222222",
            "2222222222222222",
            "1111111111000111",
            "0000111000000111",
            "0000011000001111",
            "1111111111111111",
            "1111111111111111",
            "1111122211111111",
            "1112222222211111",
        },
        new[]
        {
            "D......o......D.",
            "..o..........C..",
            "................",
            "................",
            "D..............o",
            "...........C...D",
            ".o..............",
            "................",
            "................",
            "D....o..........",
            "............o...",
            ".C............D.",
            "o..............o",
        })
    {
        Blurb = "Basalt ledges over rivers of lava, crossed by narrow necks of stone. Fliers own the rift.", Theme = "Volcanic",
        WildAnchor = new Vector2I(8, 1), PlayerAnchor = new Vector2I(8, 11),
        Plans = { [BodyPlan.Biped] = 1.5f, [BodyPlan.Quadruped] = 1.3f, [BodyPlan.Aquatic] = 0.2f, [BodyPlan.Avian] = 1.3f },
        Strains = { [Strain.Abyssal] = 2f, [Strain.Clockwork] = 1.3f },
        Elements = { [Element.Ember] = 4f, [Element.Stone] = 2f, [Element.Frost] = 0.1f, [Element.Leaf] = 0.3f },
    };

    /// <summary>A beach under dune grass, with two piers running out into the sea. Swimmers own the water.</summary>
    public static BattleMap Coast() => new("Saltwind Coast",
        new[]
        {
            "....ssss========",
            "...sssss========",
            "..ssssss=wwww===",
            "..sssss=========",
            "...ssss=========",
            ",..ssss=========",
            ",,.ssssswwwww===",
            "...ssss=========",
            "..sssss=========",
            "..ssssss========",
            "...ssss====,,===",
            "....sssss==,,===",
            ".....ssss=======",
        },
        new[]
        {
            "2221111000000000",
            "2211111000000000",
            "2111111011110000",
            "2111110000000000",
            "2211110000000000",
            "3211110000000000",
            "3321111111110000",
            "2211110000000000",
            "2111110000000000",
            "2111111000000000",
            "2211110000110000",
            "2221111000110000",
            "2222111100000000",
        },
        new[]
        {
            "u...............",
            "..o.............",
            ".u.......c..B...",
            "P...............",
            "..r.............",
            "o...............",
            "......L.....B...",
            ".r..............",
            "P...............",
            ".u....o.........",
            "...........o....",
            "P...............",
            ".u..r...........",
        })
    {
        Blurb = "Dune grass, a long beach and two piers into the sea. Swimmers own the water; gulls own the sky.", Theme = "Coast",
        WildAnchor = new Vector2I(4, 1), PlayerAnchor = new Vector2I(4, 11),
        Plans = { [BodyPlan.Aquatic] = 3f, [BodyPlan.Avian] = 2f, [BodyPlan.Serpent] = 1.3f },
        Strains = { [Strain.Clockwork] = 0.6f },
        Elements = { [Element.Tide] = 4f, [Element.Spark] = 1.3f, [Element.Ember] = 0.4f },
    };

    /// <summary>The city's arena: a packed-earth floor ringed by stone tiers, pillars and banners. Cups are fought here.</summary>
    public static BattleMap Coliseum() => new("The Grand Coliseum",
        new[]
        {
            ",,,,,,,,,,,,,,,,",
            ",,,,--------,,,,",
            ",,,----------,,,",
            ",,------------,,",
            ",,------------,,",
            ",,------------,,",
            ",,----,,,,----,,",
            ",,------------,,",
            ",,------------,,",
            ",,------------,,",
            ",,,----------,,,",
            ",,,,--------,,,,",
            ",,,,,,,,,,,,,,,,",
        },
        new[]
        {
            "3333333333333333",
            "3332111111112333",
            "3321000000001233",
            "3210000000000123",
            "3210000000000123",
            "3210000000000123",
            "3210001111000123",
            "3210000000000123",
            "3210000000000123",
            "3210000000000123",
            "3321000000001233",
            "3332111111112333",
            "3333333333333333",
        },
        new[]
        {
            "f..I...ff...I..f",
            "................",
            "I..............I",
            "................",
            "..............S.",
            "................",
            "................",
            "................",
            ".S..............",
            "................",
            "I..............I",
            "................",
            "f..I...ff...I..f",
        })
    {
        Blurb = "The city's arena: packed earth ringed by stone tiers. The crowd wants a show.", Theme = "Uptown",
        WildAnchor = new Vector2I(8, 2), PlayerAnchor = new Vector2I(8, 11),
    };
}
