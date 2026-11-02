using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using Godot;

namespace CreatureTactics.UI;

/// <summary>One piece of shorthand: a glyph and a few characters ("sword x1.5", "hourglass 3").</summary>
public readonly record struct Chip(string Glyph, string Text = "", Color? Tint = null, string Tip = null);

/// <summary>
/// Shared readable widgets: outlined labels, glyph chips and the shorthand for skills and stats.
/// The rules (from WizardSurvivors and DrawFight): a glyph before every stat, text outlined so it
/// reads over any background, colour only as a second cue, and full sentences only in tooltips.
/// </summary>
public static class Ui
{
    public static readonly Color Ink = Color.FromHtml("#ece7f6"), Dim = Color.FromHtml("#a49cbc"), Gold = Color.FromHtml("#f2c14e"),
        Cyan = Color.FromHtml("#4fe3ff"), Good = Color.FromHtml("#6ff0a0"), Bad = Color.FromHtml("#ff7f8f"), Pink = Color.FromHtml("#ff8ac0");

    public static Label Text(string text, int size, Color color, int outline = 5)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.03f, 0.07f));
        l.AddThemeConstantOverride("outline_size", outline);
        return l;
    }

    public static TextureRect Picture(Texture2D tex, string tip = null) => new()
    {
        Texture = tex,
        StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        TooltipText = tip ?? "",
        MouseFilter = tip == null ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop,
    };

    /// <summary>A glyph's texture; "mini:name" picks a 5x5 mark, drawn to the same height.</summary>
    public static Texture2D GlyphTexture(string glyph, int scale) =>
        glyph.StartsWith("mini:") ? Glyphs.Mini(glyph[5..], scale * 2) : Glyphs.Get(glyph, scale);

    public static Control ChipView(Chip chip, int size = 17, int scale = 2)
    {
        var row = new HBoxContainer { TooltipText = chip.Tip ?? "", MouseFilter = chip.Tip == null ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop };
        row.AddThemeConstantOverride("separation", 1);
        row.AddChild(Picture(GlyphTexture(chip.Glyph, scale)));
        if (chip.Text != "") row.AddChild(Text(chip.Text, size, chip.Tint ?? Ink));
        return row;
    }

    public static HFlowContainer Chips(IEnumerable<Chip> chips, int size = 17, int scale = 2, int gap = 10)
    {
        // Fills the rest of its row: a flow left at its minimum width puts every chip on its own line.
        var flow = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        flow.AddThemeConstantOverride("h_separation", gap);
        flow.AddThemeConstantOverride("v_separation", 2);
        foreach (var c in chips) flow.AddChild(ChipView(c, size, scale));
        return flow;
    }

    static string Signed(int n) => n > 0 ? "+" + n : n.ToString();
    static string Pct(float f) => Mathf.RoundToInt(f * 100) + "%";
    static string Mult(float f) => "x" + f.ToString("0.#");

    /// <summary>What a skill does, as chips: power, reach, shape, effects, how long, cooldown.</summary>
    public static List<Chip> SkillChips(SkillDef s)
    {
        var chips = new List<Chip>();
        if (s.Power > 0)
        {
            chips.Add(new("atk", Mult(s.Power), null, "damage, as a multiple of a basic attack"));
            if (s.Flank > 0) chips.Add(new("paw", Mult(s.Power + s.Flank), null, "with an ally beside the target"));
            if (s.HighGround > 0) chips.Add(new("mountain", Mult(s.Power + s.HighGround), null, "striking down from higher ground"));
            if (s.Desperate > 0) chips.Add(new("hp", Mult(s.Power + s.Desperate), Bad, "while below half HP"));
            if (s.Execute > 0) chips.Add(new("skull", Mult(s.Power + s.Execute), null, "against a foe below 40% HP"));
        }
        if (s.Heal > 0) chips.Add(new("heal", Pct(s.Heal), Good, "of max HP recovered"));
        if (s.Target == TargetKind.Tile) chips.Add(new("swirl", "", null, "teleport to a free tile"));
        if (s.Range > 1 && !s.AroundSelf || s.Range == 1 && s.Target == TargetKind.Ally) chips.Add(new("range", s.Range.ToString(), null, s.Straight ? "range, straight lines only" : "range"));
        if (s.Straight) chips.Add(new("mini:line", "", null, "straight lines only"));
        if (s.AroundSelf) chips.Add(new("mini:ring", s.Radius.ToString(), null, $"everything within {s.Radius} of the user"));
        else if (s.Radius > 0) chips.Add(new("area", s.Radius.ToString(), null, $"everything within {s.Radius} of the aimed tile"));
        if (s.Leap) chips.Add(new("dive", "", null, "leaps next to the target first"));
        if (s.HitBonus != 0) chips.Add(new("hit", Signed(s.HitBonus), s.HitBonus > 0 ? Good : Bad, "to hit"));
        foreach (var (stat, amount, _) in s.Buffs)
            chips.Add(new(SkillIcons.StatGlyph(stat), Signed(amount) + (stat == StatKind.Evade ? "%" : ""), amount > 0 ? Good : Bad,
                stat == StatKind.Evade ? (amount > 0 ? "harder to hit" : "easier to hit") : stat.ToString().ToUpperInvariant()));
        if (s.Poison > 0) chips.Add(new("poison", $"{s.Poison}x{s.PoisonTurns}", Good, $"poison: {s.Poison} a turn for {s.PoisonTurns} turns"));
        if (s.StunChance > 0) chips.Add(new("stun", Pct(s.StunChance), null, "chance to stun (loses its next turn)"));
        if (s.Root) chips.Add(new("root", "", null, "rooted: can't move next turn"));
        if (s.Taunt) chips.Add(new("taunt", "", null, "taunted: must come for the user"));
        if (s.Knock > 0) chips.Add(new("knock", s.Knock.ToString(), null, "knockback, tiles"));
        if (s.Pull > 0) chips.Add(new("hook", s.Pull.ToString(), null, "pulls toward the user, tiles"));
        if (s.Drain > 0) chips.Add(new("fang", Pct(s.Drain), Good, "of the damage dealt is healed"));
        if (s.HealOnKill > 0) chips.Add(new("hp", "+" + Pct(s.HealOnKill), Good, "of max HP back if it kills"));
        if (s.Steal) chips.Add(new("hand", "", null, "steals every boost the target has"));
        if (s.Cleanse) chips.Add(new("sparkle", "", null, "cleanses poison, roots, stuns and curses"));
        if (s.CooldownCut > 0) chips.Add(new("cooldown", "-" + s.CooldownCut, Good, "the target's skills cool down sooner"));
        if (s.RandomBuff) chips.Add(new("question", "", null, "a random strong boost"));
        if (s.HitAndRun) chips.Add(new("move", "again", null, "may move again after using it"));
        int turns = s.Buffs.Length > 0 ? s.Buffs.Max(b => b.turns) : 0;
        if (turns > 0) chips.Add(new("clock", turns.ToString(), Dim, "turns the effect lasts"));
        chips.Add(new("cooldown", s.Cooldown.ToString(), Dim, "cooldown, turns"));
        return chips;
    }

    /// <summary>A passive's effect as chips (its stat bonuses); rule changes keep their short text.</summary>
    public static List<Chip> PassiveChips(PassiveDef p)
    {
        var chips = new List<Chip>();
        void Stat(int v, string glyph, string suffix = "") { if (v != 0) chips.Add(new(glyph, Signed(v) + suffix, v > 0 ? Good : Bad)); }
        Stat(p.Atk, "atk"); Stat(p.Def, "def"); Stat(p.Spd, "spd"); Stat(p.Move, "move"); Stat(p.Evade, "evade", "%"); Stat(p.Hit, "hit"); Stat(p.Thorns, "thorns");
        if (p.HpPct > 0) chips.Add(new("hp", "+" + Pct(p.HpPct), Good));
        if (p.Regen > 0) chips.Add(new("heal", Pct(p.Regen) + "/turn", Good));
        if (p.Drain > 0) chips.Add(new("fang", Pct(p.Drain), Good));
        if (p.SkillPower > 0) chips.Add(new("rune", "+" + Pct(p.SkillPower), Good));
        if (p.Predator > 0) chips.Add(new("skull", "+" + Pct(p.Predator), Good));
        if (p.RangeBonus > 0) chips.Add(new("range", "+" + p.RangeBonus, Good));
        if (p.CooldownCut > 0) chips.Add(new("cooldown", "-" + p.CooldownCut, Good));
        if (p.Climb > 0) chips.Add(new("climb", p.Climb.ToString(), Good));
        if (p.ChemBonus > 0) chips.Add(new("flask", "+" + p.ChemBonus, Good));
        return chips;
    }
}
