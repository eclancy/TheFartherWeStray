using System.Collections.Generic;
using System.Text;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.SpriteLab;

/// <summary>Which creatures a sheet shows, and composing a sheet into one PNG.</summary>
public static class CreatureSheet
{
    /// <summary>A sheet of different species: slot i is species Derive(sheet, i), shown by its first individual.</summary>
    public static List<Creature> SpeciesSheet(ulong sheetSeed, int count)
    {
        var list = new List<Creature>();
        for (int i = 0; i < count; i++) list.Add(Creature.Specimen(SeededRng.Derive(sheetSeed, (ulong)i)));
        return list;
    }

    /// <summary>Individuals of one species. The first is the one the species sheet shows.</summary>
    public static List<Creature> Siblings(Species species, int count)
    {
        var list = new List<Creature>();
        for (int i = 0; i < count; i++) list.Add(Creature.Specimen(species, i));
        return list;
    }

    /// <summary>The DrawPets cast, each exactly as designed.</summary>
    public static List<Creature> DrawPetsSheet(int count)
    {
        var list = new List<Creature>();
        foreach (var s in DrawPetsCast.All)
        {
            if (list.Count == count) break;
            list.Add(Creature.Specimen(s, 0));
        }
        return list;
    }

    public static string Describe(Creature c)
    {
        var s = c.Species;
        var p = c.Parts;
        var (weapon, armor, move) = c.Loadout();
        var sb = new StringBuilder();
        sb.Append($"{s.Name}{(c.Shiny ? "  (shiny)" : "")}{(c.Mutation != null ? $"  ({c.Mutation})" : "")}");
        sb.Append(s.Origin != null
            ? $"   ·   {s.Plan}   ·   from {s.Origin}\n"
            : $"   ·   {s.Plan}, {s.Element} palette   ·   species {s.Seed:X16}\n");
        sb.Append($"Size {c.Size} ({c.Footprint})   ·   {c.Attitude}: reacts - {AttitudeInfo.Reaction(c.Attitude)}; recruit - {AttitudeInfo.Recruit(c.Attitude)}\n");
        sb.Append($"Parts: head {Creature.Words(p.Head)}, ears {Creature.Words(p.Ears)}, eyes {Creature.Words(p.Eyes)}, horns {Creature.Words(p.Horns)}, ");
        sb.Append($"back {Creature.Words(p.Back)}, limbs {Creature.Words(p.Limbs)}, tail {Creature.Words(p.Tail)}, wings {Creature.Words(p.Wings)}, pattern {Creature.Words(p.Pattern)}\n");
        sb.Append($"As gear: weapon {weapon}   ·   armour {armor}   ·   movement {move}");
        return sb.ToString();
    }

    static readonly Color Paper = new(0.957f, 0.937f, 0.886f);
    static readonly Color Card = new(0.988f, 0.976f, 0.945f);
    static readonly Color Shadow = new(0.2f, 0.18f, 0.25f);

    /// <summary>Lays the creatures out on a grid, each standing on a soft shadow, at an integer scale.</summary>
    public static Image Compose(List<Creature> creatures, bool fourColor, int scale, int columns)
    {
        var sprites = new List<Image>();
        int maxW = 0, maxH = 0;
        foreach (var c in creatures)
        {
            var img = CreaturePainter.Paint(c, fourColor);
            sprites.Add(img);
            maxW = Mathf.Max(maxW, img.GetWidth());
            maxH = Mathf.Max(maxH, img.GetHeight());
        }
        int pad = 6 * scale, gap = 4 * scale;
        int cellW = maxW * scale + 2 * pad, cellH = maxH * scale + 2 * pad;
        int rows = (sprites.Count + columns - 1) / columns;
        var sheet = Image.CreateEmpty(columns * cellW + (columns + 1) * gap, rows * cellH + (rows + 1) * gap, false, Image.Format.Rgba8);
        sheet.Fill(Paper);

        for (int i = 0; i < sprites.Count; i++)
        {
            int cx = gap + (i % columns) * (cellW + gap), cy = gap + (i / columns) * (cellH + gap);
            sheet.FillRect(new Rect2I(cx, cy, cellW, cellH), Card);
            var sprite = sprites[i];
            sprite.Resize(sprite.GetWidth() * scale, sprite.GetHeight() * scale, Image.Interpolation.Nearest);
            int groundY = cy + cellH - pad;
            int sx = cx + (cellW - sprite.GetWidth()) / 2;
            ShadowEllipse(sheet, cx + cellW / 2, groundY - scale, sprite.GetWidth() * 0.42f, Mathf.Max(2, 3 * scale));
            sheet.BlendRect(sprite, new Rect2I(0, 0, sprite.GetWidth(), sprite.GetHeight()), new Vector2I(sx, groundY - sprite.GetHeight()));
        }
        return sheet;
    }

    static void ShadowEllipse(Image img, int cx, int cy, float rx, float ry)
    {
        for (int y = (int)(cy - ry); y <= cy + ry; y++)
        for (int x = (int)(cx - rx); x <= cx + rx; x++)
        {
            if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight()) continue;
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            if (dx * dx + dy * dy > 1) continue;
            img.SetPixel(x, y, img.GetPixel(x, y).Lerp(Shadow, 0.16f));
        }
    }
}
