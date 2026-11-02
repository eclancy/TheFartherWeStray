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
        if (s.Strain != Strain.Natural)
            sb.Append($"\n{s.Strain.Label()} strain, grafted with: {(s.Grafts == Graft.None ? "nothing" : s.Grafts.ToString().ToLowerInvariant())}");
        return sb.ToString();
    }

    static readonly Color Paper = new(0.075f, 0.06f, 0.13f);
    static readonly Color Card = new(0.12f, 0.1f, 0.19f);
    static readonly Color Shadow = new(0f, 0f, 0f);

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

    /// <summary>
    /// Each creature twice, as it stands on the board: facing down-left (front) and up-left (back),
    /// feet on a diamond tile, so every sprite in the sheet shares one ground line per row.
    /// </summary>
    public static Image ComposeViews(List<Creature> creatures, bool fourColor, int scale, int columns)
    {
        var shots = new List<(Image img, Vector2I anchor)>();
        foreach (var c in creatures)
            foreach (var view in new[] { View.Front, View.Back })
                shots.Add(CreatureSculptor.Render(c, fourColor, view));
        return Sheets(new() { shots }, creatures.Count, scale, columns)[0];
    }

    /// <summary>
    /// The idle loop, frame by frame, laid out like <see cref="ComposeViews"/> on one fixed grid so
    /// the frames line up: <paramref name="frames"/> frames over two breaths, the last one a blink.
    /// </summary>
    public static List<Image> ComposeIdle(List<Creature> creatures, int scale, int columns, int frames = 16)
    {
        var steps = new List<(Pose, float, bool)>();
        for (int f = 0; f < frames; f++) steps.Add((Pose.Idle, (f % (frames / 2)) / (frames / 2f), f == frames - 1));
        return ComposeAnimation(creatures, scale, columns, steps);
    }

    /// <summary>
    /// Every action in turn, at 8 frames a second: a breath, two walk cycles, a strike, a cast, a
    /// flinch and a faint (held), then back to breathing.
    /// </summary>
    public static List<(Pose pose, float phase, bool blink)> ActionReel()
    {
        var steps = new List<(Pose, float, bool)>();
        for (int f = 0; f < 8; f++) steps.Add((Pose.Idle, f / 8f, false));
        for (int f = 0; f < 8; f++) steps.Add((Pose.Walk, (f % 4) / 4f, false));
        for (int f = 0; f < 5; f++) steps.Add((Pose.Strike, f / 5f, false));
        for (int f = 0; f < 3; f++) steps.Add((Pose.Idle, f / 8f, false));
        for (int f = 0; f < 6; f++) steps.Add((Pose.Cast, f / 6f, false));
        for (int f = 0; f < 4; f++) steps.Add((Pose.Hurt, f / 4f, false));
        for (int f = 0; f < 2; f++) steps.Add((Pose.Idle, f / 8f, false));
        for (int f = 0; f < 6; f++) steps.Add((Pose.Faint, f / 5f, false));
        for (int f = 0; f < 4; f++) steps.Add((Pose.Faint, 1, false));
        return steps;
    }

    public static List<Image> ComposeAnimation(List<Creature> creatures, int scale, int columns, List<(Pose pose, float phase, bool blink)> steps)
    {
        var all = new List<List<(Image img, Vector2I anchor)>>();
        foreach (var (pose, phase, blink) in steps)
        {
            var shots = new List<(Image img, Vector2I anchor)>();
            foreach (var c in creatures)
                foreach (var view in new[] { View.Front, View.Back })
                    shots.Add(CreatureSculptor.Render(c, false, view, phase, blink, pose));
            all.Add(shots);
        }
        return Sheets(all, creatures.Count, scale, columns);
    }

    /// <summary>One sheet per frame, every frame sized to fit all of them, each creature on its tiles.</summary>
    static List<Image> Sheets(List<List<(Image img, Vector2I anchor)>> frames, int count, int scale, int columns)
    {
        int left = 24, right = 24, up = 0, down = 14;
        foreach (var shots in frames)
        foreach (var (img, a) in shots)
        {
            left = Mathf.Max(left, a.X); right = Mathf.Max(right, img.GetWidth() - a.X);
            up = Mathf.Max(up, a.Y); down = Mathf.Max(down, img.GetHeight() - a.Y);
        }
        int pad = 4 * scale, gap = 4 * scale;
        int spriteW = (left + right) * scale, cellW = spriteW * 2 + 3 * pad, cellH = (up + down) * scale + 2 * pad;
        int rows = (count + columns - 1) / columns;
        var sheets = new List<Image>();
        foreach (var shots in frames)
        {
            var sheet = Image.CreateEmpty(columns * cellW + (columns + 1) * gap, rows * cellH + (rows + 1) * gap, false, Image.Format.Rgba8);
            sheet.Fill(Paper);
            for (int i = 0; i < count; i++)
            {
                int cx = gap + (i % columns) * (cellW + gap), cy = gap + (i / columns) * (cellH + gap);
                sheet.FillRect(new Rect2I(cx, cy, cellW, cellH), Card);
                for (int v = 0; v < 2; v++)
                {
                    var (img, a) = shots[i * 2 + v];
                    int gx = cx + pad + v * (spriteW + pad) + left * scale, gy = cy + pad + up * scale;
                    // The tile it stands on: a 48 x 24 diamond, as on the board.
                    for (int y = -12; y <= 12; y++)
                    for (int x = -24; x <= 24; x++)
                    {
                        if (Mathf.Abs(x) / 24f + Mathf.Abs(y) / 12f > 1) continue;
                        for (int k = 0; k < scale * scale; k++)
                        {
                            int px = gx + x * scale + k % scale, py = gy + y * scale + k / scale;
                            if (px >= 0 && py >= 0 && px < sheet.GetWidth() && py < sheet.GetHeight()) sheet.SetPixel(px, py, Card.Lightened(0.12f));
                        }
                    }
                    var sprite = (Image)img.Duplicate();
                    sprite.Resize(sprite.GetWidth() * scale, sprite.GetHeight() * scale, Image.Interpolation.Nearest);
                    sheet.BlendRect(sprite, new Rect2I(0, 0, sprite.GetWidth(), sprite.GetHeight()), new Vector2I(gx - a.X * scale, gy - a.Y * scale));
                }
            }
            sheets.Add(sheet);
        }
        return sheets;
    }

    static void ShadowEllipse(Image img, int cx, int cy, float rx, float ry)
    {
        for (int y = (int)(cy - ry); y <= cy + ry; y++)
        for (int x = (int)(cx - rx); x <= cx + rx; x++)
        {
            if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight()) continue;
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            if (dx * dx + dy * dy > 1) continue;
            img.SetPixel(x, y, img.GetPixel(x, y).Lerp(Shadow, 0.35f));
        }
    }
}
