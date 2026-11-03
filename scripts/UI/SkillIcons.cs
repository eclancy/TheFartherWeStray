using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.UI;

/// <summary>
/// An icon for every skill, class, passive and piece of loadout. A skill icon is a framed glyph:
/// the glyph says what it does (a fist, a bolt, a heal cross), the frame colour says what kind of
/// skill it is (attack, heal, boost, control, move), and corner marks say its shape (an area, a ring
/// around the user, a straight line, a leap) and whether it raises or lowers stats.
/// </summary>
public static class SkillIcons
{
    public const int SkillSize = 20, BadgeSize = 16;

    /// <summary>The glyph for each skill, by id. Within one class, every skill looks different.</summary>
    static readonly Dictionary<string, string> SkillGlyph = new()
    {
        ["heavy_blow"] = "fist", ["rend"] = "claw", ["frenzy"] = "atk",
        ["quick_strike"] = "spd", ["pounce"] = "paw", ["evasion"] = "evade",
        ["shield_wall"] = "def", ["taunt"] = "taunt", ["second_wind"] = "hp",
        ["hex_bolt"] = "bolt", ["ward"] = "rune", ["arc_storm"] = "storm",
        ["toxic_flask"] = "flask", ["mend"] = "heal", ["acid_splash"] = "poison",
        ["overclock"] = "gear", ["snare"] = "chain", ["shock_prod"] = "stun",
        ["dive_bomb"] = "dive", ["gale"] = "wind", ["updraft"] = "wing",
        ["trample"] = "horn", ["iron_hide"] = "def", ["earthshaker"] = "mountain",
        ["hex_cage"] = "chain", ["overcharge"] = "atk", ["nova"] = "sun",
        ["quill_shot"] = "arrow", ["pin_down"] = "root", ["volley"] = "arrow",
        ["war_cry"] = "sound", ["screech"] = "sound", ["lullaby"] = "moon",
        ["backstab"] = "slash", ["smoke_bomb"] = "smoke", ["pilfer"] = "hand",
        ["shell_up"] = "shell", ["spiked_rampart"] = "thorns", ["haul"] = "hook",
        ["crack"] = "whip", ["snare_lash"] = "hook", ["whirl"] = "swirl",
        ["ambush"] = "slash", ["venom_claws"] = "poison", ["shadow_step"] = "ghost",
        ["tidal_crash"] = "wave", ["undertow"] = "hook", ["deep_shell"] = "shell",
        ["stomp"] = "mountain", ["shrug_off"] = "sparkle", ["toss"] = "knock",
        ["blink"] = "swirl", ["vanish"] = "ghost", ["hex_prank"] = "rune",
        ["rally"] = "move", ["pack_tactics"] = "paw", ["alpha_howl"] = "sound",
        ["headshot"] = "hit", ["mark"] = "eye", ["ricochet"] = "arrow",
        ["shimmer_dose"] = "flask", ["caustic_cloud"] = "smoke", ["mutagen"] = "question",
        ["glyph_snare"] = "rune", ["glyph_shield"] = "def", ["arcane_surge"] = "cooldown",
        ["dread"] = "skull", ["siphon"] = "swirl", ["maw"] = "fang",
        ["spark_coil"] = "stun", ["repair"] = "gear", ["overdrive"] = "spd",
        ["antler_sweep"] = "slash", ["crown_of_thorns"] = "thorns", ["rutting_charge"] = "horn",
        ["roar_of_courage"] = "sound", ["challenge_roar"] = "taunt", ["last_stand"] = "flame",
        ["crystal_lance"] = "spear", ["prism_ward"] = "crystal", ["shardstorm"] = "crystal",
        ["crushing_blow"] = "club", ["tail_sweep"] = "swirl", ["skull_crack"] = "skull",
        ["sting_volley"] = "bee", ["drone"] = "sound", ["hive_mind"] = "bee",
        ["foresight"] = "eye", ["true_sight"] = "hit", ["omen_bolt"] = "bolt",
        ["tailwind"] = "wind", ["heat_shimmer"] = "flame", ["solar_flare"] = "sun",
        ["lance_charge"] = "spear", ["stampede"] = "knock", ["steadfast"] = "def",
        ["snatch"] = "hand", ["scrounge"] = "heal", ["junk_toss"] = "gear",
        ["fade"] = "ghost", ["feint"] = "spd", ["ghost_step"] = "swirl",
        ["challenge"] = "taunt", ["duel_stance"] = "atk", ["finishing_blow"] = "slash",
        ["hydra_snap"] = "fang", ["regrow"] = "heal", ["chorus_roar"] = "sound",
        ["turn_to_stone"] = "mountain", ["plunge"] = "dive", ["grotesque_glare"] = "eye",
        ["constrict"] = "chain", ["hypnotic_sway"] = "swirl", ["serpents_embrace"] = "hook",
        ["sting"] = "poison", ["pincer_grip"] = "claw", ["neurotoxin"] = "stun",
        ["lure"] = "hook", ["abyssal_maw"] = "fang", ["glow_flash"] = "sun",
        ["gaze_beam"] = "bolt", ["petrifying_stare"] = "eye", ["inner_eye"] = "evade",
        ["benediction"] = "heal", ["radiance"] = "sun", ["sanctuary"] = "def",
        ["brand"] = "flame", ["sigil_burst"] = "rune", ["warding_circle"] = "def",
        ["stone_peck"] = "mountain", ["serpent_lash"] = "whip", ["dread_crow"] = "sound",
        // Items from the shops.
        ["snare_net"] = "chain", ["soothing_chime"] = "sound", ["tonic"] = "heal", ["smelling_salts"] = "sparkle",
        // Augments' attacks.
        ["piston_punch"] = "fist", ["rivet_volley"] = "gear", ["targeting_beam"] = "eye", ["core_discharge"] = "bolt",
        ["acid_spray"] = "flask", ["chem_rush"] = "flask", ["strobe_flash"] = "stun", ["toxic_drip"] = "poison",
        ["gear_grind"] = "gear", ["piston_charge"] = "knock", ["smoke_screen"] = "smoke", ["steam_vent"] = "wind",
    };

    /// <summary>Each class's emblem.</summary>
    static readonly Dictionary<string, string> ClassGlyph = new()
    {
        ["Brawler"] = "fist", ["Skirmisher"] = "spd", ["Guardian"] = "def", ["Mystic"] = "rune", ["Alchemist"] = "flask",
        ["Tinker"] = "gear", ["Hunter"] = "arrow", ["Howler"] = "sound", ["Rogue"] = "smoke",
        ["Skydiver"] = "wing", ["Juggernaut"] = "horn", ["Hexwarden"] = "chain", ["Bulwark"] = "shell", ["Lasher"] = "whip",
        ["Stalker"] = "claw", ["Leviathan"] = "wave", ["Titan"] = "mountain", ["Trickster"] = "swirl", ["Packleader"] = "paw",
        ["Deadeye"] = "hit", ["Chemist"] = "poison", ["Runeweaver"] = "crystal", ["Voidcaller"] = "skull", ["Artificer"] = "storm",
        ["Thornstag"] = "thorns", ["Lionheart"] = "crown", ["Geomancer"] = "spear", ["Bonebreaker"] = "club", ["Swarmlord"] = "bee",
        ["Seer"] = "eye", ["Sunsail"] = "sun", ["Cavalier"] = "move", ["Scavenger"] = "hand", ["Phantom"] = "ghost", ["Champion"] = "star",
        ["Hydrarch"] = "fang", ["Stoneguard"] = "mountain", ["Coilbinder"] = "swirl", ["Venomtail"] = "poison", ["Lurelight"] = "sun",
        ["Oculus"] = "eye", ["Saint"] = "sparkle", ["Hexmark"] = "rune", ["Basilisk"] = "stun",
    };

    public enum Kind { Attack, Heal, Boost, Control, Move }

    public static Kind KindOf(SkillDef s) =>
        s.Target == TargetKind.Tile ? Kind.Move
        : s.Power > 0 ? Kind.Attack
        : s.Heal > 0 ? Kind.Heal
        : s.Target is TargetKind.Ally or TargetKind.Self ? Kind.Boost
        : Kind.Control;

    public static Color KindColor(Kind k) => Color.FromHtml(k switch
    {
        Kind.Attack => "#d4483a",
        Kind.Heal => "#35b062",
        Kind.Boost => "#4a7fe0",
        Kind.Control => "#9a52d8",
        _ => "#2fb2c8",
    });

    public static string KindName(Kind k) => k switch
    {
        Kind.Attack => "attack", Kind.Heal => "heal", Kind.Boost => "boost", Kind.Control => "control", _ => "move",
    };

    public static string GlyphOf(SkillDef s) => SkillGlyph.TryGetValue(s.Id, out var g) ? g : s.Power > 0 ? "atk" : "star";
    public static string ClassEmblem(string cls) => ClassGlyph.TryGetValue(cls, out var g) ? g : "star";

    static readonly Dictionary<(string, int), Texture2D> Cache = new();

    static Texture2D Cached(string key, int scale, System.Func<Image> paint)
    {
        if (Cache.TryGetValue((key, scale), out var t)) return t;
        var img = paint();
        img.Resize(img.GetWidth() * scale, img.GetHeight() * scale, Image.Interpolation.Nearest);
        return Cache[(key, scale)] = ImageTexture.CreateFromImage(img);
    }

    /// <summary>A skill's icon: 20x20 art pixels at the given scale.</summary>
    public static Texture2D Skill(SkillDef s, int scale = 2) => Cached("skill:" + s.Id, scale, () => SkillImage(s));

    public static Image SkillImage(SkillDef s)
    {
        var img = Image.CreateEmpty(SkillSize, SkillSize, false, Image.Format.Rgba8);
        Frame(img, 0, 0, SkillSize, KindColor(KindOf(s)));
        Glyphs.Draw(img, GlyphOf(s), 4, 4);
        // Raises stats (green up) or lowers them (red down): top right.
        bool up = s.Buffs.Any(b => b.amount > 0) || s.RandomBuff || s.CooldownCut > 0;
        bool down = s.Buffs.Any(b => b.amount < 0);
        if (down && (s.Target is TargetKind.Enemy or TargetKind.Area || !up)) Glyphs.DrawMini(img, "down", 14, 1);
        else if (up) Glyphs.DrawMini(img, "up", 14, 1);
        // Its shape: bottom right.
        string shape = s.AroundSelf ? "ring" : s.Radius > 0 ? "area" : s.Leap ? "leap" : s.Straight ? "line" : null;
        if (shape != null) Glyphs.DrawMini(img, shape, 14, 14);
        return img;
    }

    /// <summary>The basic attack, by weapon, in an attack frame.</summary>
    public static Texture2D Attack(Weapon w, int scale = 2) => Cached("attack:" + w, scale, () =>
    {
        var img = Image.CreateEmpty(SkillSize, SkillSize, false, Image.Format.Rgba8);
        Frame(img, 0, 0, SkillSize, Color.FromHtml("#8a8296"));
        Glyphs.Draw(img, WeaponGlyph(w), 4, 4);
        return img;
    });

    /// <summary>An action in a plain frame (glowberry, undo, end turn).</summary>
    public static Texture2D Action(string glyph, int scale = 2) => Cached("action:" + glyph, scale, () =>
    {
        var img = Image.CreateEmpty(SkillSize, SkillSize, false, Image.Format.Rgba8);
        Frame(img, 0, 0, SkillSize, Color.FromHtml("#6a6280"));
        Glyphs.Draw(img, glyph, 4, 4);
        return img;
    });

    /// <summary>A class's emblem in a 16x16 frame: gold for its main class, grey and locked when shut.</summary>
    public static Texture2D Class(string cls, ClassBook.Standing standing, int scale = 2) => Cached($"class:{cls}:{standing}", scale, () =>
    {
        var img = Image.CreateEmpty(BadgeSize, BadgeSize, false, Image.Format.Rgba8);
        var tint = standing switch
        {
            ClassBook.Standing.Open => Color.FromHtml("#b0a6c8"),
            ClassBook.Standing.Locked => Color.FromHtml("#4a4360"),
            _ => Color.FromHtml("#6a2a3a"),
        };
        Frame(img, 0, 0, BadgeSize, tint);
        if (standing == ClassBook.Standing.Open) Glyphs.Draw(img, ClassEmblem(cls), 2, 2);
        else Glyphs.Draw(img, ClassEmblem(cls), 2, 2, tint.Lightened(0.25f));
        return img;
    });

    /// <summary>Mastery stars, three slots, filled as earned.</summary>
    public static Texture2D Stars(int stars, int scale = 2) => Cached("stars:" + stars, scale, () =>
    {
        var img = Image.CreateEmpty(19, 7, false, Image.Format.Rgba8);
        for (int i = 0; i < 3; i++) Glyphs.DrawMini(img, i < stars ? "star" : "nostar", 1 + i * 6, 1);
        return img;
    });

    /// <summary>A passive's icon: green frame for support, pink for reactions.</summary>
    public static Texture2D Passive(PassiveDef p, int scale = 2) => Cached("passive:" + p.Id, scale, () =>
    {
        var img = Image.CreateEmpty(BadgeSize, BadgeSize, false, Image.Format.Rgba8);
        Frame(img, 0, 0, BadgeSize, Color.FromHtml(p.Class switch { "Shiny" => "#f2c14e", "Gear" => "#c9a24a", "Trait" => "#8a9a4a", "Tier" => p.Id.StartsWith("chimera") ? "#8a5ac8" : "#9ac25a", "Infusion" => "#3fbf8a", _ => p.Kind == PassiveKind.Reaction ? "#d84f8f" : "#3faa6a" }));
        Glyphs.Draw(img, PassiveGlyph(p), 2, 2);
        return img;
    });

    public static string PassiveGlyph(PassiveDef p)
    {
        if (p.Class == "Tier") return TierBook.Glyph(p.Id.StartsWith("chimera") ? Tier.Chimera : Tier.Thoroughbred);
        if (p.Class == "Infusion") return "flask";
        if (p.Kind == PassiveKind.Reaction)
            return p.React switch
            {
                ReactionKind.Counter => "fist", ReactionKind.ReturnFire => "arrow", ReactionKind.Retreat => "evade",
                ReactionKind.Brace => "def", ReactionKind.Protector => "atk", ReactionKind.Burst => "crystal",
                ReactionKind.Dodge => "eye", ReactionKind.Guard => "shell", ReactionKind.Adrenaline => "hp",
                ReactionKind.Shock => "stun", ReactionKind.RallyCry => "sound", ReactionKind.DirtyRetort => "hand",
                _ => "star",
            };
        if (p.NoPoison) return "poison";
        if (p.NoStun) return "stun";
        if (p.Unstoppable) return "mountain";
        if (p.Amphibious) return "wave";
        if (p.LongReach) return "whip";
        if (p.Echo) return "sound";
        if (p.PackBond) return "paw";
        if (p.Regen > 0) return "heal";
        if (p.Drain > 0) return "fang";
        if (p.SkillPower > 0) return "rune";
        if (p.Predator > 0) return "skull";
        if (p.RangeBonus > 0) return "range";
        if (p.CooldownCut > 0) return "cooldown";
        if (p.Climb > 0) return "climb";
        if (p.ChemBonus > 0) return "flask";
        if (p.Thorns > 0) return "thorns";
        if (p.HpPct > 0) return "hp";
        if (p.Evade > 0) return "evade";
        if (p.Hit > 0) return "hit";
        // The biggest plain stat bonus.
        var stats = new[] { (p.Atk, "atk"), (p.Def, "def"), (p.Spd, "spd"), (p.Move, "move") };
        var best = stats.OrderByDescending(s => s.Item1).First();
        return best.Item1 > 0 ? best.Item2 : "star";
    }

    public static string WeaponGlyph(Weapon w) => w switch
    {
        Weapon.Claws => "claw", Weapon.Horns => "horn", Weapon.Club => "club", Weapon.Whip => "whip", Weapon.Hooves => "move", _ => "fang",
    };

    public static string ArmorGlyph(Armor a) => a switch
    {
        Armor.Shell => "shell", Armor.Spines => "thorns", Armor.Crystal => "crystal", Armor.Mane => "hp", _ => "def",
    };

    public static string MoveGlyph(MoveType m) => m switch
    {
        MoveType.Fly => "wing", MoveType.Swim => "wave", MoveType.Climb => "climb", _ => "move",
    };

    public static string AttitudeGlyph(Attitude a) => a switch
    {
        Attitude.Aggressive => "fist", Attitude.Skittish => "evade", Attitude.Loyal => "hp", Attitude.Curious => "question", _ => "crown",
    };

    public static string StatGlyph(StatKind k) => k switch
    {
        StatKind.Atk => "atk", StatKind.Def => "def", StatKind.Spd => "spd", StatKind.Move => "move", StatKind.Evade => "evade", _ => "thorns",
    };

    /// <summary>A chamfered, bevelled frame filled with a dark shade of the colour.</summary>
    public static void Frame(Image img, int x0, int y0, int size, Color c)
    {
        var top = c.Darkened(0.5f);
        var bottom = c.Darkened(0.78f);
        var light = c.Lightened(0.2f);
        var shade = c.Darkened(0.3f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cx = System.Math.Min(x, size - 1 - x), cy = System.Math.Min(y, size - 1 - y);
            if (cx + cy < 2) continue; // chamfered corners
            Color px;
            if (cx == 0 || cy == 0 || cx + cy == 2) px = x + y < size ? light : shade; // the rim, lit from top left
            else px = top.Lerp(bottom, y / (float)size);
            img.SetPixel(x0 + x, y0 + y, px);
        }
    }

    /// <summary>Every skill icon on one sheet, for checking them at a glance.</summary>
    public static Image Sheet(int columns = 15)
    {
        var skills = ClassBook.Skills;
        int rows = (skills.Length + columns - 1) / columns;
        var img = Image.CreateEmpty(columns * (SkillSize + 4) + 4, rows * (SkillSize + 4) + 4, false, Image.Format.Rgba8);
        img.Fill(Color.FromHtml("#16121f"));
        for (int i = 0; i < skills.Length; i++)
        {
            var icon = SkillImage(skills[i]);
            img.BlendRect(icon, new Rect2I(0, 0, SkillSize, SkillSize), new Vector2I(4 + i % columns * (SkillSize + 4), 4 + i / columns * (SkillSize + 4)));
        }
        return img;
    }
}
