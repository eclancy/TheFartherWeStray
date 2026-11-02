using System;
using CreatureTactics.Creatures;
using Godot;

namespace CreatureTactics.World;

/// <summary>
/// The country map's picture, painted in code like the boards: the glorious city on its plateau
/// above (brass, gold and gardens), the cliff and its lifts, the neon undercity below with its chem
/// canal and the sump, the cavern in the east, and the ranch's fields in the western wilds. Roads
/// and places are drawn over it by the screen, so they can show what's open.
/// </summary>
public static class CountryArt
{
    public const int W = 272, H = 232;

    static readonly Color Void = C("#0b0913"), Rim = C("#2a2140");
    static readonly Color Wild = C("#1c3a2c"), WildLight = C("#28513a"), Tree = C("#14291f"), TreeLight = C("#3a7350");
    static readonly Color Stone = C("#5b4a39"), StoneDark = C("#4d3f31"), Street = C("#7a6447"), Roof = C("#9b7446"), RoofLight = C("#c39a58");
    static readonly Color Patina = C("#4f8a7a"), Copper = C("#b5653a"), StoneLight = C("#7a6447");
    static readonly Color Gold = C("#f2c14e"), Hex = C("#4fe3ff"), Lawn = C("#3f7a4f"), LawnLight = C("#5a9a5e"), Pond = C("#3b6fd0");
    static readonly Color Cliff = C("#3a3046"), CliffLight = C("#5a4b6c"), CliffDark = C("#1c1626"), Brass = C("#c9a24a");
    static readonly Color Under = C("#18131f"), Block = C("#2a2238"), BlockLight = C("#3a3050");
    static readonly Color Pink = C("#ff4f9a"), Chem = C("#3dffb0"), ChemDeep = C("#1d8a63"), Smog = C("#3b2a4d");
    static readonly Color Field = C("#6b5a2e"), FieldGreen = C("#4f7a35"), Barn = C("#a03b2e"), Fence = C("#8a6a45");
    static readonly Color Crystal = C("#b06cff"), Cave = C("#0c0a12");

    static Color C(string hex) => Color.FromHtml(hex);

    static Texture2D _texture;
    public static Texture2D Texture => _texture ??= ImageTexture.CreateFromImage(Paint());

    public static Image Paint()
    {
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        var rng = new SeededRng(0xC0047);
        float N(float x, float y, float scale, int salt) => Noise(x / scale, y / scale, salt);

        // The land: an island of country in the dark, its edge wobbling.
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float dx = (x - 136) / 150f, dy = (y - 116) / 126f;
            float edge = 1 - MathF.Sqrt(dx * dx + dy * dy) + (N(x, y, 14, 1) - 0.5f) * 0.18f;
            if (edge < 0) { img.SetPixel(x, y, rng.Chance(0.004f) ? C("#3a3358") : Void); continue; }
            if (edge < 0.025f) { img.SetPixel(x, y, Rim); continue; }
            img.SetPixel(x, y, N(x, y, 6, 2) > 0.62f ? WildLight : Wild);
        }

        // The city above: a plateau laid out in streets, its blocks built up under brass, copper and patina roofs.
        for (int y = 6; y < 100; y++)
        for (int x = 34; x < 252; x++)
        {
            float top = 96 + (N(x, 0, 10, 3) - 0.5f) * 10;
            if (y > top || !Land(img, x, y)) continue;
            img.SetPixel(x, y, x % 11 == 0 || y % 9 == 0 ? Street : N(x, y, 5, 13) > 0.6f ? StoneDark : Stone);
        }
        for (int gy = 0; gy < 90; gy += 9)
        for (int gx = 33; gx < 250; gx += 11)
        {
            if (Near(gx + 5, gy + 4, 74, 52, 19)) continue; // the gardens stay green
            int count = rng.Int(1, 3);
            for (int k = 0; k < count; k++)
            {
                int bw = rng.Int(3, 6), bh = rng.Int(3, 5);
                int bx = gx + 1 + rng.Int(0, 10 - bw), by = gy + 1 + rng.Int(0, 8 - bh);
                if (img.GetPixel(Math.Clamp(bx, 0, W - 1), Math.Clamp(by, 0, H - 1)) == Wild) continue;
                var roof = rng.Weighted((Roof, 4f), (RoofLight, 2f), (Copper, 1.5f), (Patina, 1.5f));
                Rect(img, bx, by, bw, bh, roof);
                Rect(img, bx, by + bh - 1, bw, 1, roof.Darkened(0.35f));
                if (rng.Chance(0.6f)) Set(img, bx + rng.Int(0, bw - 1), by + bh - 1, Gold);
            }
        }
        // Towers with hextech tips.
        for (int i = 0; i < 9; i++)
        {
            int tx = rng.Int(46, 236), ty = rng.Int(14, 70), th = rng.Int(8, 16);
            if (Near(tx, ty, 74, 52, 20)) continue;
            Rect(img, tx, ty - th, 2, th, StoneLight);
            Set(img, tx, ty - th - 1, Hex); Set(img, tx + 1, ty - th - 1, Hex);
        }
        // Gilded Gardens: lawns, hedges and ponds.
        Blob(img, 74, 52, 17, 13, Lawn, 4);
        for (int i = 0; i < 40; i++) Set(img, 74 + rng.Int(-14, 14), 52 + rng.Int(-10, 10), rng.Chance(0.5f) ? LawnLight : C("#2c5a38"));
        Blob(img, 66, 58, 4, 3, Pond, 5); Blob(img, 83, 46, 3, 2, Pond, 6);
        // Brass Bridges: a chasm through the city, bridged in gold.
        for (int y = 18; y < 62; y++) { int cx = 140 + (int)((N(0, y, 8, 7) - 0.5f) * 6); Rect(img, cx - 3, y, 6, 1, CliffDark); }
        for (int k = 0; k < 3; k++) { int by = 28 + k * 12; Rect(img, 134, by, 13, 1, Brass); Set(img, 137, by - 1, Gold); Set(img, 143, by - 1, Gold); }
        // Clocktower Ruins: a broken tower with a gold face.
        Rect(img, 207, 44, 7, 22, C("#6a6470"));
        Rect(img, 208, 40, 5, 4, C("#57525e"));
        Blob(img, 210, 50, 2, 2, Gold, 8);
        for (int i = 0; i < 14; i++) Set(img, 200 + rng.Int(0, 22), 62 + rng.Int(0, 6), C("#57525e"));

        // The cliff between, with lifts.
        for (int x = 30; x < 256; x++)
        {
            int top = 96 + (int)((N(x, 0, 10, 3) - 0.5f) * 10);
            for (int y = top; y < top + 14; y++)
            {
                if (!Land(img, x, y)) continue;
                img.SetPixel(x, y, y < top + 2 ? CliffLight : y > top + 10 ? CliffDark : (x * 3 + y) % 11 == 0 ? CliffLight : Cliff);
            }
        }
        foreach (int lx in new[] { 150, 151, 118, 196 }) for (int y = 92; y < 124; y++) if (Land(img, lx, y)) Set(img, lx, y, (y % 4 == 0) ? Gold : Brass);

        // The undercity: dark blocks, neon windows, smog, and the chem canal.
        for (int y = 112; y < 226; y++)
        for (int x = 70; x < 262; x++)
        {
            if (!Land(img, x, y) || y < 108 + (N(x, 1, 10, 3) - 0.5f) * 10 + 14 || x < 78 + (N(0, y, 9, 14) - 0.5f) * 22) continue;
            if (Near(x, y, 216, 204, 22) || Near(x, y, 244, 130, 20)) continue;
            img.SetPixel(x, y, Under);
        }
        for (int i = 0; i < 300; i++)
        {
            int bx = rng.Int(76, 252), by = rng.Int(124, 218), bw = rng.Int(3, 7), bh = rng.Int(3, 6);
            if (img.GetPixel(bx, by) != Under || Near(bx, by, 216, 204, 22)) continue;
            Rect(img, bx, by, bw, bh, rng.Chance(0.3f) ? BlockLight : Block);
            if (rng.Chance(0.7f)) Set(img, bx + rng.Int(0, bw - 1), by + rng.Int(0, bh - 1), rng.Pick(new[] { Pink, Hex, Chem }));
        }
        for (int x = 84; x < 238; x++)
        {
            int cy = 196 + (int)(MathF.Sin(x * 0.09f) * 3);
            if (!Land(img, x, cy)) continue;
            Set(img, x, cy - 1, ChemDeep); Set(img, x, cy, Chem); Set(img, x, cy + 1, ChemDeep);
        }
        for (int i = 0; i < 18; i++) Blend(img, rng.Int(80, 250), rng.Int(126, 210), rng.Int(6, 12), Smog, 0.35f);
        // Sump Marsh: pools of glowing chem and reeds.
        Blob(img, 216, 204, 20, 13, C("#173a2c"), 9);
        for (int i = 0; i < 9; i++) Blob(img, 216 + rng.Int(-14, 14), 204 + rng.Int(-8, 8), rng.Int(2, 5), rng.Int(1, 3), rng.Chance(0.5f) ? Chem : ChemDeep, 10 + i);
        for (int i = 0; i < 30; i++) Set(img, 216 + rng.Int(-18, 18), 204 + rng.Int(-11, 11), C("#4f7a35"));

        // The western wilds: forest, and the ranch's fields, barn and fence.
        for (int i = 0; i < 260; i++)
        {
            int tx = rng.Int(8, 76), ty = rng.Int(100, 226);
            if (!Land(img, tx, ty) || Near(tx, ty, 36, 128, 16) || img.GetPixel(tx, ty) != Wild && img.GetPixel(tx, ty) != WildLight) continue;
            Set(img, tx, ty, Tree); Set(img, tx + 1, ty, Tree); Set(img, tx, ty - 1, TreeLight);
        }
        for (int y = 118; y < 138; y++)
        for (int x = 22; x < 52; x++)
            if (Land(img, x, y)) Set(img, x, y, (y / 2) % 2 == 0 ? Field : FieldGreen);
        for (int x = 21; x < 53; x += 2) { Set(img, x, 117, Fence); Set(img, x, 138, Fence); }
        Rect(img, 40, 120, 7, 5, Barn); Rect(img, 41, 119, 5, 1, C("#c4523e")); Set(img, 43, 124, C("#2a1a12"));

        // Hexlight Hollow: a cavern mouth in the eastern cliffs, ringed with crystals.
        Blob(img, 244, 130, 16, 12, CliffDark, 11);
        Blob(img, 244, 132, 9, 6, Cave, 12);
        for (int i = 0; i < 16; i++)
        {
            int cx = 244 + rng.Int(-14, 14), cy = 130 + rng.Int(-10, 10);
            if (Near(cx, cy, 244, 132, 6)) continue;
            var col = rng.Chance(0.5f) ? Crystal : Hex;
            Set(img, cx, cy, col); Set(img, cx, cy - 1, col.Lightened(0.3f));
        }
        return img;
    }

    static bool Land(Image img, int x, int y) => x >= 0 && y >= 0 && x < W && y < H && img.GetPixel(x, y) != Void && img.GetPixel(x, y) != Rim;
    static bool Near(int x, int y, int cx, int cy, int r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r;

    static void Set(Image img, int x, int y, Color c)
    {
        if (Land(img, x, y)) img.SetPixel(x, y, c);
    }

    static void Rect(Image img, int x, int y, int w, int h, Color c)
    {
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(img, x + i, y + j, c);
    }

    static void Blob(Image img, int cx, int cy, int rx, int ry, Color c, int salt)
    {
        for (int y = cy - ry - 2; y <= cy + ry + 2; y++)
        for (int x = cx - rx - 2; x <= cx + rx + 2; x++)
        {
            float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
            if (dx * dx + dy * dy + (Noise(x / 3f, y / 3f, salt) - 0.5f) * 0.6f < 1) Set(img, x, y, c);
        }
    }

    static void Blend(Image img, int cx, int cy, int r, Color c, float amount)
    {
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
            if (Near(x, y, cx, cy, r) && Land(img, x, y)) img.SetPixel(x, y, img.GetPixel(x, y).Lerp(c, amount * (1 - MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r)));
    }

    /// <summary>Smooth value noise in [0, 1].</summary>
    static float Noise(float x, float y, int salt)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy, salt), b = Hash(ix + 1, iy, salt), c = Hash(ix, iy + 1, salt), d = Hash(ix + 1, iy + 1, salt);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    static float Hash(int x, int y, int salt) =>
        new SeededRng(SeededRng.Derive((ulong)(uint)x * 73856093UL ^ (ulong)(uint)y * 19349663UL, (ulong)salt)).Float();
}
