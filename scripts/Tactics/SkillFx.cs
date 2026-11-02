using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
using CreatureTactics.UI;
using Godot;

namespace CreatureTactics.Tactics;

/// <summary>What a skill looks like going off: the colours and the way its particles move.</summary>
public enum FxTheme { Strike, Ember, Frost, Tide, Leaf, Spark, Stone, Venom, Shade, Holy, Arcane, Brass, Chem, Sound, Wind, Boost }

/// <summary>
/// Pixel effects for skills and hits: projectiles, impact bursts, rings, rising motes and slashes,
/// drawn as crisp board pixels over the units. Purely cosmetic: the dice here are their own, so
/// effects never touch the battle's rolls.
/// </summary>
public partial class SkillFx : Node2D
{
    enum Shape { Dot, Streak, Shard, Bubble, Puff, Chevron, Star }

    sealed class Particle
    {
        public Vector2 Pos, Vel;
        public float Life, Max, Size, Grav, Drag, Sway, Phase, Grow;
        public Color A, B;
        public Shape Shape;
    }

    sealed class Ring { public Vector2 At; public float R0, R1, Life, Max; public Color Color; public int Thick; }
    sealed class Arc { public Vector2 At; public float R, From, Sweep, Life, Max; public Color A, B; }
    sealed class Bolt { public Vector2 From, To; public float Life, Max; public Color A, B; public int Seed; }
    sealed class Shot
    {
        public Vector2 From, To; public float T, Dur, Lift; public FxTheme Theme; public Color A, B;
        public TaskCompletionSource<bool> Done; public float Emit;
    }

    readonly List<Particle> _parts = new();
    readonly List<Ring> _rings = new();
    readonly List<Arc> _arcs = new();
    readonly List<Bolt> _bolts = new();
    readonly List<Shot> _shots = new();
    readonly SeededRng _rng = new(0x5EED_F00D);
    GlowLayer _glow;

    /// <summary>How big effects are on the board: particles, speeds and rings all scale with it.</summary>
    const float K = 1.8f;

    public override void _Ready()
    {
        // The glow: soft halos added on top in light (additive), so effects shine on the dark board.
        _glow = new GlowLayer { Fx = this, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        AddChild(_glow);
    }

    sealed partial class GlowLayer : Node2D
    {
        public SkillFx Fx;
        public override void _Draw() => Fx.DrawGlow(this);
    }

    // ------------------------------------------------------------------ themes

    /// <summary>The theme of a skill: by its icon first, then the user's element for plain hits.</summary>
    public static FxTheme ThemeOf(SkillDef s, Unit user)
    {
        if (SkillIcons.KindOf(s) == SkillIcons.Kind.Heal) return FxTheme.Holy;
        return SkillIcons.GlyphOf(s) switch
        {
            "flame" => FxTheme.Ember,
            "crystal" => FxTheme.Frost,
            "wave" => FxTheme.Tide,
            "thorns" or "root" or "bee" => FxTheme.Leaf,
            "bolt" or "storm" or "stun" => FxTheme.Spark,
            "mountain" or "shell" or "def" => FxTheme.Stone,
            "poison" => FxTheme.Venom,
            "ghost" or "smoke" or "moon" or "skull" => FxTheme.Shade,
            "heal" or "sparkle" or "sun" or "hp" => FxTheme.Holy,
            "rune" or "eye" or "question" or "cooldown" or "swirl" => FxTheme.Arcane,
            "gear" or "chain" or "hook" => FxTheme.Brass,
            "flask" => FxTheme.Chem,
            "sound" or "taunt" or "horn" => FxTheme.Sound,
            "wind" or "wing" or "dive" or "spd" or "evade" or "move" => FxTheme.Wind,
            _ => SkillIcons.KindOf(s) == SkillIcons.Kind.Boost ? FxTheme.Boost : FxTheme.Strike,
        };
    }

    /// <summary>A bright core and a deeper glow for each theme; plain strikes glow in the user's element.</summary>
    public static (Color core, Color glow) Palette(FxTheme t, Element? element = null) => t switch
    {
        FxTheme.Ember => (C("#ffe08a"), C("#ff5a1f")),
        FxTheme.Frost => (C("#f2fdff"), C("#6fd8ff")),
        FxTheme.Tide => (C("#e6f6ff"), C("#2f8dff")),
        FxTheme.Leaf => (C("#dfff9a"), C("#3fae3a")),
        FxTheme.Spark => (C("#fffbe0"), C("#ffd23a")),
        FxTheme.Stone => (C("#e8d6b0"), C("#7d6648")),
        FxTheme.Venom => (C("#e4ff8a"), C("#7c3fd0")),
        FxTheme.Shade => (C("#c9a6ff"), C("#2a1442")),
        FxTheme.Holy => (C("#fffbe8"), C("#ffc94a")),
        FxTheme.Arcane => (C("#ffd6fb"), C("#e03fd8")),
        FxTheme.Brass => (C("#fff0c0"), C("#c98a2e")),
        FxTheme.Chem => (C("#eaffd0"), C("#39ff6a")),
        FxTheme.Sound => (C("#ffffff"), C("#a8c4ff")),
        FxTheme.Wind => (C("#ffffff"), C("#b8f0e0")),
        FxTheme.Boost => (C("#e8f0ff"), C("#4a8cff")),
        _ => (C("#ffffff"), element is { } e ? ElementGlow(e) : C("#ffd27a")),
    };

    public static Color ElementGlow(Element e) => C(e switch
    {
        Element.Ember => "#ff7a2e",
        Element.Tide => "#3ea8ff",
        Element.Leaf => "#6bd35a",
        Element.Spark => "#ffe14a",
        Element.Stone => "#c4a678",
        Element.Venom => "#a6ff4a",
        Element.Frost => "#9ee8ff",
        Element.Shade => "#9a6cff",
        _ => "#e0f27a",
    });

    static Color C(string hex) => Color.FromHtml(hex);

    // ------------------------------------------------------------------ effects

    /// <summary>A projectile from one point to another, bursting when it lands. Finishes on arrival.</summary>
    public Task Projectile(Vector2 from, Vector2 to, FxTheme theme, Element? element = null)
    {
        var (a, b) = Palette(theme, element);
        float dist = from.DistanceTo(to);
        if (theme == FxTheme.Spark)
        {
            // Lightning doesn't travel: it's there, flickering, then gone.
            _bolts.Add(new Bolt { From = from, To = to, Life = 0.3f, Max = 0.3f, A = a, B = b, Seed = _rng.Int(1 << 20) });
            Flash(to, a, 7);
            return Delay(0.12f);
        }
        float lift = theme switch { FxTheme.Stone => 0.45f, FxTheme.Tide or FxTheme.Chem or FxTheme.Venom => 0.25f, FxTheme.Strike or FxTheme.Wind or FxTheme.Sound => 0.06f, _ => 0.14f } * dist;
        var shot = new Shot { From = from, To = to, Dur = Mathf.Clamp(dist / 260f, 0.16f, 0.45f), Lift = lift, Theme = theme, A = a, B = b, Done = new TaskCompletionSource<bool>() };
        _shots.Add(shot);
        return shot.Done.Task;
    }

    /// <summary>
    /// An impact: a flash, a ring along the ground, and the theme's particles flying off. Pieces of
    /// a wide blast leave the flash and ring to the blast's own wave.
    /// </summary>
    public void Burst(Vector2 at, Vector2 ground, FxTheme theme, Element? element = null, float strength = 1, bool piece = false)
    {
        var (a, b) = Palette(theme, element);
        int n = (int)(12 * strength);
        if (!piece)
        {
            Flash(at, a, theme == FxTheme.Shade ? 5 : 8);
            if (theme != FxTheme.Sound) GroundRing(ground, b, 4, 14 * strength, 0.35f);
        }
        switch (theme)
        {
            case FxTheme.Ember:
                for (int i = 0; i < n + 4; i++) Spray(at, Up(70, 40), 0.35f, 0.7f, a, C("#a0200a"), _rng.Range(1, 2.5f), -60, Shape.Dot, drag: 1.5f);
                break;
            case FxTheme.Frost:
                for (int i = 0; i < n; i++) Spray(at, Around(80, 30), 0.35f, 0.6f, a, b, 2, 160, Shape.Shard);
                break;
            case FxTheme.Tide:
                for (int i = 0; i < n + 2; i++) Spray(at, Up(80, 60), 0.4f, 0.65f, a, b, _rng.Range(1, 2), 300, Shape.Dot);
                GroundRing(ground, a, 6, 20 * strength, 0.5f);
                break;
            case FxTheme.Leaf:
                for (int i = 0; i < n; i++) { var p = Spray(at, Around(45, 25), 0.6f, 1f, a, b, 2, 25, Shape.Shard, drag: 2f); p.Sway = 18; }
                break;
            case FxTheme.Spark:
                for (int i = 0; i < n; i++) Spray(at, Around(150, 60), 0.12f, 0.3f, a, b, 1, 0, Shape.Streak, drag: 4f);
                break;
            case FxTheme.Stone:
                for (int i = 0; i < n; i++) Spray(ground + new Vector2(0, -3), Up(90, 50), 0.4f, 0.7f, a, b, _rng.Range(1.5f, 3f), 340, Shape.Dot);
                for (int i = 0; i < 4; i++) Spray(ground, Around(20, 10), 0.5f, 0.8f, new Color(b, 0.7f), new Color(b, 0), 3, -10, Shape.Puff, drag: 2f, grow: 6);
                break;
            case FxTheme.Venom:
                for (int i = 0; i < n; i++) Spray(at, Up(35, 30), 0.6f, 1.1f, C("#c6ff5a"), b, _rng.Range(1.5f, 3f), -20, Shape.Bubble, drag: 1.5f);
                break;
            case FxTheme.Shade:
                for (int i = 0; i < n; i++) Spray(at, Around(30, 25), 0.6f, 1.1f, new Color(a, 0.9f), new Color(b, 0), 2, -25, Shape.Puff, drag: 1.5f, grow: 7);
                break;
            case FxTheme.Holy:
                for (int i = 0; i < n; i++) Spray(at, Up(50, 30), 0.5f, 0.9f, a, b, 1, -30, i % 3 == 0 ? Shape.Star : Shape.Dot, drag: 2f);
                break;
            case FxTheme.Arcane:
                GroundRing(ground, a, 3, 10 * strength, 0.5f);
                for (int i = 0; i < n; i++) { var p = Spray(at, Around(55, 20), 0.5f, 0.8f, a, b, 1.5f, -15, i % 2 == 0 ? Shape.Star : Shape.Dot, drag: 3f); p.Sway = 10; }
                break;
            case FxTheme.Brass:
                for (int i = 0; i < n; i++) Spray(at, Around(120, 50), 0.15f, 0.35f, C("#fff2a0"), b, 1, 120, Shape.Streak, drag: 2f);
                for (int i = 0; i < 4; i++) Spray(at, Up(25, 15), 0.6f, 0.9f, new Color(0.9f, 0.9f, 0.92f, 0.7f), new Color(0.9f, 0.9f, 0.92f, 0), 2, -30, Shape.Puff, grow: 6);
                break;
            case FxTheme.Chem:
                for (int i = 0; i < n; i++) Spray(at, Up(70, 50), 0.4f, 0.75f, a, b, _rng.Range(1.5f, 2.5f), 260, Shape.Dot);
                for (int i = 0; i < 4; i++) Spray(ground, Up(15, 10), 0.6f, 1f, new Color(b, 0.6f), new Color(b, 0), 2, -15, Shape.Puff, grow: 6);
                break;
            case FxTheme.Sound:
                for (int k = 0; k < 3; k++) _rings.Add(new Ring { At = at, R0 = (3 + k * 3) * K, R1 = (18 + k * 6) * K, Life = 0.35f + k * 0.1f, Max = 0.35f + k * 0.1f, Color = k == 0 ? a : b, Thick = 2 });
                break;
            case FxTheme.Wind:
                for (int i = 0; i < n; i++) { var p = Spray(at, Around(90, 30), 0.3f, 0.5f, a, new Color(b, 0), 1, -10, Shape.Streak, drag: 2f); p.Sway = 25; }
                break;
            case FxTheme.Boost:
                Rise(at, ground, theme, element);
                break;
            default:
                // A plain strike: a slash across the target and white-hot sparks.
                Slash(at, a, b);
                for (int i = 0; i < n - 4; i++) Spray(at, Around(110, 50), 0.12f, 0.3f, a, b, 1, 140, Shape.Streak, drag: 3f);
                break;
        }
    }

    /// <summary>Heals and boosts: motes rising up the body and a ring at the feet.</summary>
    public void Rise(Vector2 body, Vector2 ground, FxTheme theme, Element? element = null, bool down = false)
    {
        var (a, b) = Palette(theme, element);
        GroundRing(ground, b, 5, 12, 0.5f);
        for (int i = 0; i < 12; i++)
        {
            var start = ground + new Vector2(_rng.Range(-9, 9), _rng.Range(-3, 2));
            var shape = theme == FxTheme.Boost ? Shape.Chevron : i % 3 == 0 ? Shape.Star : Shape.Dot;
            var p = Add(start, new Vector2(0, down ? 10 : -_rng.Range(18, 34)), _rng.Range(0.6f, 0.95f), a, b, shape == Shape.Dot ? 1.5f : 1, 0, shape);
            p.Life += i * 0.025f; p.Max = p.Life; // a stream, not one puff
            p.Phase = down ? 1 : 0;
        }
    }

    /// <summary>A ring spreading out over the ground from the user (skills around itself).</summary>
    public void Wave(Vector2 ground, float radius, FxTheme theme, Element? element = null)
    {
        var (a, b) = Palette(theme, element);
        _rings.Add(new Ring { At = ground, R0 = 4, R1 = radius, Life = 0.45f, Max = 0.45f, Color = a, Thick = 3 });
        _rings.Add(new Ring { At = ground, R0 = 2, R1 = radius * 0.8f, Life = 0.55f, Max = 0.55f, Color = b, Thick = 2 });
    }

    /// <summary>Sparks off a creature that's been hit, or a puff of air when the blow misses.</summary>
    public void Hit(Vector2 at, int amount, bool miss, Element? element = null)
    {
        if (miss)
        {
            for (int i = 0; i < 4; i++) Spray(at + new Vector2(8, 0), Around(25, 10), 0.3f, 0.45f, new Color(1, 1, 1, 0.6f), new Color(1, 1, 1, 0), 1, -10, Shape.Puff, grow: 4);
            return;
        }
        var glow = element is { } e ? ElementGlow(e) : C("#ffd27a");
        Flash(at, Colors.White, Mathf.Clamp(4 + amount / 3, 5, 10));
        int n = Mathf.Clamp(4 + amount / 3, 5, 12);
        for (int i = 0; i < n; i++) Spray(at, Around(100, 50), 0.1f, 0.25f, Colors.White, glow, 1, 150, Shape.Streak, drag: 3f);
    }

    /// <summary>A status landing: stars for a stun, bubbles for poison, leaves for roots, chevrons for boosts.</summary>
    public void Status(Vector2 head, Vector2 ground, string text)
    {
        string t = text.ToLowerInvariant();
        if (t.Contains("stun"))
            for (int i = 0; i < 5; i++) { var p = Add(head + new Vector2(_rng.Range(-8, 8), _rng.Range(-4, 2)), new Vector2(_rng.Range(-10, 10), -8), 0.7f, C("#fff3a0"), C("#ffb02e"), 1, 0, Shape.Star); p.Sway = 14; }
        else if (t.Contains("poison"))
            Burst(head, ground, FxTheme.Venom, null, 0.6f);
        else if (t.Contains("root"))
            for (int i = 0; i < 8; i++) Spray(ground, Up(30, 20), 0.5f, 0.8f, C("#b8f070"), C("#2f7a2a"), 2, 40, Shape.Shard);
        else if (t.Contains("+") || t.Contains("braced") || t.Contains("rallied") || t.Contains("protective"))
            Rise(head, ground, FxTheme.Boost);
        else if (t.Contains("-") || t.Contains("cursed") || t.Contains("taunt"))
            Rise(head, ground, FxTheme.Arcane, null, down: true);
    }

    // ------------------------------------------------------------------ pieces

    void Flash(Vector2 at, Color c, float size) => Add(at, Vector2.Zero, 0.18f, c, new Color(c, 0), size / K * 1.4f, 0, Shape.Star);

    void GroundRing(Vector2 at, Color c, float r0, float r1, float life) =>
        _rings.Add(new Ring { At = at, R0 = r0 * K, R1 = r1 * K, Life = life, Max = life, Color = c, Thick = 2 });

    void Slash(Vector2 at, Color a, Color b)
    {
        float from = _rng.Range(-2.6f, -1.9f);
        _arcs.Add(new Arc { At = at + new Vector2(-2, 4), R = 11 * K, From = from, Sweep = 2.2f, Life = 0.24f, Max = 0.24f, A = a, B = b });
    }

    Vector2 Around(float speed, float spread)
    {
        float ang = _rng.Range(0, Mathf.Tau);
        float s = speed + _rng.Range(-spread, spread);
        return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f) * s;
    }

    Vector2 Up(float speed, float spread)
    {
        float ang = _rng.Range(-Mathf.Pi * 0.85f, -Mathf.Pi * 0.15f);
        return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (speed + _rng.Range(-spread, spread));
    }

    Particle Spray(Vector2 at, Vector2 vel, float lifeMin, float lifeMax, Color a, Color b, float size, float grav, Shape shape, float drag = 0, float grow = 0)
    {
        var p = Add(at + new Vector2(_rng.Range(-2, 2), _rng.Range(-2, 2)), vel, _rng.Range(lifeMin, lifeMax), a, b, size, grav, shape);
        p.Drag = drag; p.Grow = grow * K;
        return p;
    }

    Particle Add(Vector2 at, Vector2 vel, float life, Color a, Color b, float size, float grav, Shape shape)
    {
        var p = new Particle { Pos = at, Vel = vel * K, Life = life, Max = life, A = a, B = b, Size = size * K, Grav = grav * K, Shape = shape, Phase = _rng.Range(0, Mathf.Tau) };
        _parts.Add(p);
        return p;
    }

    async Task Delay(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    // ------------------------------------------------------------------ update and draw

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool any = _parts.Count + _rings.Count + _arcs.Count + _bolts.Count + _shots.Count > 0;
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var s = _shots[i];
            s.T += dt / s.Dur;
            var at = ShotAt(s, Mathf.Min(s.T, 1));
            // A trail behind the head, in the theme's way.
            s.Emit += dt;
            while (s.Emit > 0.012f)
            {
                s.Emit -= 0.012f;
                var shape = s.Theme switch { FxTheme.Shade or FxTheme.Venom or FxTheme.Chem => Shape.Puff, FxTheme.Frost => Shape.Shard, FxTheme.Strike or FxTheme.Wind => Shape.Streak, _ => Shape.Dot };
                var p = Add(at + new Vector2(_rng.Range(-1.5f, 1.5f), _rng.Range(-1.5f, 1.5f)), Around(12, 8), _rng.Range(0.15f, 0.35f), s.A, new Color(s.B, 0), shape == Shape.Puff ? 1.5f : 1, s.Theme == FxTheme.Ember ? -40 : 10, shape);
                if (shape == Shape.Puff) p.Grow = 3 * K;
            }
            if (s.T >= 1)
            {
                _shots.RemoveAt(i);
                s.Done.TrySetResult(true);
            }
        }
        for (int i = _parts.Count - 1; i >= 0; i--)
        {
            var p = _parts[i];
            p.Life -= dt;
            if (p.Life <= 0) { _parts.RemoveAt(i); continue; }
            p.Vel += new Vector2(0, p.Grav * dt);
            if (p.Drag > 0) p.Vel *= Mathf.Max(0, 1 - p.Drag * dt);
            p.Pos += p.Vel * dt;
            if (p.Sway > 0) p.Pos.X += Mathf.Sin(p.Phase + (p.Max - p.Life) * 9) * p.Sway * K * dt;
            p.Size += p.Grow * dt;
        }
        for (int i = _rings.Count - 1; i >= 0; i--) if ((_rings[i].Life -= dt) <= 0) _rings.RemoveAt(i);
        for (int i = _arcs.Count - 1; i >= 0; i--) if ((_arcs[i].Life -= dt) <= 0) _arcs.RemoveAt(i);
        for (int i = _bolts.Count - 1; i >= 0; i--) if ((_bolts[i].Life -= dt) <= 0) _bolts.RemoveAt(i);
        if (any) { QueueRedraw(); _glow.QueueRedraw(); }
    }

    static Vector2 ShotAt(Shot s, float t) => s.From.Lerp(s.To, t) + new Vector2(0, -4 * s.Lift * t * (1 - t));

    public override void _Draw()
    {
        foreach (var r in _rings)
        {
            float t = 1 - r.Life / r.Max;
            float rad = Mathf.Lerp(r.R0, r.R1, 1 - (1 - t) * (1 - t));
            var c = new Color(r.Color, r.Color.A * (1 - t));
            int steps = Mathf.Max(12, (int)(rad * 4));
            for (int k = 0; k < steps; k++)
            {
                float ang = k * Mathf.Tau / steps;
                var p = r.At + new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad * 0.5f);
                DrawRect(new Rect2(p.Floor(), new Vector2(r.Thick, r.Thick)), c);
            }
        }
        foreach (var a in _arcs)
        {
            float t = 1 - a.Life / a.Max;
            float head = a.From + a.Sweep * Mathf.Min(1, t * 2.2f);
            for (float ang = a.From; ang < head; ang += 0.07f)
            {
                float along = (ang - a.From) / a.Sweep;
                var c = a.A.Lerp(a.B, along) with { A = (1 - t) * (0.4f + 0.6f * along) };
                var p = a.At + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.75f) * a.R;
                float w = 1 + 2 * Mathf.Sin(along * Mathf.Pi);
                DrawRect(new Rect2(p.Floor(), new Vector2(Mathf.Ceil(w), Mathf.Ceil(w))), c);
            }
        }
        foreach (var b in _bolts)
        {
            float t = 1 - b.Life / b.Max;
            if (t > 0.45f && t < 0.6f) continue; // the flicker
            var pts = BoltPoints(b, t);
            for (int k = 1; k < pts.Count; k++)
            {
                PixelLine(pts[k - 1], pts[k], new Color(b.B, 0.7f * (1 - t)), 4);
                PixelLine(pts[k - 1], pts[k], new Color(b.A, 1 - t), 2);
            }
        }
        foreach (var s in _shots)
        {
            var at = ShotAt(s, Mathf.Min(s.T, 1));
            var dir = (ShotAt(s, Mathf.Min(s.T + 0.05f, 1)) - at).Normalized();
            switch (s.Theme)
            {
                case FxTheme.Strike or FxTheme.Wind:
                    // A quill or arrow: a shaft and a bright head.
                    PixelLine(at - dir * 16, at, new Color(s.B, 0.85f), 2);
                    DrawRect(new Rect2(at.Floor() - new Vector2(2, 2), new Vector2(4, 4)), s.A);
                    break;
                case FxTheme.Sound:
                    for (int k = 0; k < 3; k++) DrawArcPixels(at - dir * k * 6, 5 + k * 3, dir.Angle(), 1.6f, new Color(s.A, 1 - k * 0.3f));
                    break;
                case FxTheme.Stone:
                    DrawRect(new Rect2(at.Floor() - new Vector2(5, 5), new Vector2(10, 9)), s.B);
                    DrawRect(new Rect2(at.Floor() - new Vector2(4, 5), new Vector2(5, 3)), s.A);
                    break;
                default:
                    DrawRect(new Rect2(at.Floor() - new Vector2(5, 5), new Vector2(10, 10)), new Color(s.B, 0.5f));
                    DrawRect(new Rect2(at.Floor() - new Vector2(4, 4), new Vector2(8, 8)), s.B);
                    DrawRect(new Rect2(at.Floor() - new Vector2(2, 2), new Vector2(4, 4)), s.A);
                    break;
            }
        }
        foreach (var p in _parts)
        {
            float t = 1 - p.Life / p.Max;
            var c = p.A.Lerp(p.B, t);
            if (t > 0.6f) c.A *= 1 - (t - 0.6f) / 0.4f;
            var at = p.Pos.Floor();
            float sz = Mathf.Max(1, Mathf.Round(p.Size));
            switch (p.Shape)
            {
                case Shape.Streak:
                    var v = p.Vel.Length() > 1 ? p.Vel.Normalized() : Vector2.Right;
                    PixelLine(p.Pos - v * Mathf.Clamp(p.Vel.Length() * 0.05f, 2, 12), p.Pos, c, 2);
                    break;
                case Shape.Shard:
                    // A small diamond, two pixels a step.
                    DrawRect(new Rect2(at, new Vector2(2, 2)), c);
                    DrawRect(new Rect2(at + new Vector2(-2, 2), new Vector2(6, 2)), c);
                    DrawRect(new Rect2(at + new Vector2(0, 4), new Vector2(2, 2)), c);
                    break;
                case Shape.Bubble:
                    DrawArc(at, sz / 2 + 1, 0, Mathf.Tau, 10, c, 1.5f);
                    break;
                case Shape.Puff:
                    DrawCircle(at, sz / 2, new Color(c, c.A * 0.55f));
                    break;
                case Shape.Chevron:
                    bool down = p.Phase >= 1 && p.Vel.Y > 0;
                    int dy = down ? -2 : 2;
                    for (int k = 0; k < 3; k++)
                    {
                        DrawRect(new Rect2(at + new Vector2(-2 * k, dy * k), new Vector2(2, 2)), c);
                        DrawRect(new Rect2(at + new Vector2(2 * k, dy * k), new Vector2(2, 2)), c);
                    }
                    break;
                case Shape.Star:
                    int arm = (int)Mathf.Max(1, sz * (p.Max < 0.2f ? 1 - t : 1));
                    int w = arm >= 4 ? 2 : 1;
                    DrawRect(new Rect2(at - new Vector2(arm, 0), new Vector2(arm * 2 + w, w)), c);
                    DrawRect(new Rect2(at - new Vector2(0, arm), new Vector2(w, arm * 2 + w)), c);
                    if (arm >= 4) DrawRect(new Rect2(at - Vector2.One * 2, new Vector2(4 + w, 4 + w)), new Color(c, c.A * 0.6f));
                    break;
                default:
                    DrawRect(new Rect2(at, new Vector2(sz, sz)), c);
                    break;
            }
        }
    }

    /// <summary>The bolt's zigzag, jumping to a new shape a few times as it flickers.</summary>
    static List<Vector2> BoltPoints(Bolt b, float t)
    {
        var rng = new SeededRng((ulong)(b.Seed + (int)(t * 6)));
        int segs = Mathf.Max(4, (int)(b.From.DistanceTo(b.To) / 9));
        var normal = (b.To - b.From).Normalized().Orthogonal();
        var pts = new List<Vector2> { b.From };
        for (int k = 1; k <= segs; k++) pts.Add(b.From.Lerp(b.To, k / (float)segs) + (k == segs ? Vector2.Zero : normal * rng.Range(-6, 6)));
        return pts;
    }

    void PixelLine(Vector2 a, Vector2 b, Color c, int w) => PixelLine(this, a, b, c, w);

    static void PixelLine(CanvasItem on, Vector2 a, Vector2 b, Color c, int w)
    {
        int n = Mathf.Max(1, (int)a.DistanceTo(b));
        for (int k = 0; k <= n; k++)
        {
            var p = a.Lerp(b, k / (float)n).Floor() - new Vector2(w / 2, w / 2);
            on.DrawRect(new Rect2(p, new Vector2(w, w)), c);
        }
    }

    void DrawArcPixels(Vector2 at, float r, float facing, float sweep, Color c)
    {
        for (float ang = facing - sweep / 2; ang <= facing + sweep / 2; ang += 0.18f)
            DrawRect(new Rect2((at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r).Floor(), new Vector2(2, 2)), c);
    }

    /// <summary>Halos behind everything bright: blended by adding light, so they glow on the dark board.</summary>
    void DrawGlow(CanvasItem on)
    {
        foreach (var s in _shots)
        {
            var at = ShotAt(s, Mathf.Min(s.T, 1));
            on.DrawCircle(at, 12, new Color(s.B, 0.22f));
            on.DrawCircle(at, 7, new Color(s.B, 0.3f));
        }
        foreach (var b in _bolts)
        {
            float t = 1 - b.Life / b.Max;
            var pts = BoltPoints(b, t);
            for (int k = 1; k < pts.Count; k++) PixelLine(on, pts[k - 1], pts[k], new Color(b.B, 0.16f * (1 - t)), 7);
            on.DrawCircle(b.To, 12 * (1 - t) + 4, new Color(b.B, 0.3f * (1 - t)));
        }
        foreach (var p in _parts)
        {
            if (p.Shape == Shape.Puff && p.B.V < 0.3f) continue; // dark smoke gives no light
            float t = 1 - p.Life / p.Max;
            float r = p.Shape switch { Shape.Star => p.Size * 1.2f, Shape.Puff => p.Size * 0.7f, _ => p.Size + 3 };
            on.DrawCircle(p.Pos, r, new Color(p.B, (p.Shape == Shape.Star ? 0.2f : 0.26f) * (1 - t)));
        }
        foreach (var r in _rings)
        {
            float t = 1 - r.Life / r.Max;
            float rad = Mathf.Lerp(r.R0, r.R1, 1 - (1 - t) * (1 - t));
            int steps = Mathf.Max(10, (int)(rad * 1.5f));
            for (int k = 0; k < steps; k++)
            {
                float ang = k * Mathf.Tau / steps;
                on.DrawCircle(r.At + new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad * 0.5f), 3, new Color(r.Color, 0.12f * (1 - t)));
            }
        }
    }
}
