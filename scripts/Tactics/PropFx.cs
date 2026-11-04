using System.Collections.Generic;
using TheFartherWeStray.Creatures;
using Godot;

namespace TheFartherWeStray.Tactics;

/// <summary>
/// The moving parts of a prop (the user's ask for a grittier undercity, 2026-11-02): a broken
/// streetlight that stutters off and on and spits sparks as it catches, neon signs that buzz out for a
/// moment, a hydrant spraying an arc of water into a rippling puddle, a grate puffing steam. Pure show:
/// nothing here touches the battle. Timing comes from the prop's tile, so each one keeps its own rhythm.
/// </summary>
public partial class PropFx : Node2D
{
    public Prop Prop;
    public int Variant;
    public string Theme;
    public Sprite2D Sprite;   // the prop's own sprite, swapped dark and lit as it flickers
    public ulong Seed;

    public static bool Moves(Prop p, string theme) => p is Prop.BrokenLamp or Prop.Hydrant or Prop.Vent or Prop.Still or Prop.ScrapPress or Prop.BerryBeds
        || p == Prop.Neon && theme == "Undercity";

    /// <summary>Props that let off steam: a grate, the still's chimney, the press's pipe.</summary>
    static bool Steams(Prop p) => p is Prop.Vent or Prop.Still or Prop.ScrapPress;

    (Texture2D tex, Vector2I anchor) _lit, _dark;
    Color _glow;
    Vector2 _light, _nozzle, _jet, _mouth;
    double _t;
    bool _on = true;
    SeededRng _rng;
    Node2D _halo, _ground;
    readonly List<(Vector2 p, Vector2 v, float life)> _bits = new();   // sparks, drops, steam
    readonly List<(Vector2 p, Vector2 v, float life)> _motes = new();  // bubbles in the still's glass, glints over the beds
    Vector2 _moteFrom;
    Color _moteColour;
    readonly List<(Vector2 p, float age)> _rings = new();             // splashes on the puddle

    public override void _Ready()
    {
        _rng = new SeededRng(Seed);
        _glow = PropArt.GlowOf(Variant, Theme);
        if (Prop is Prop.BrokenLamp or Prop.Neon)
        {
            _lit = PropArt.Texture(Prop, Variant, Theme, true);
            _dark = PropArt.Texture(Prop, Variant, Theme, false);
            _light = PropArt.Spot(Variant, Prop == Prop.Neon ? PropArt.NeonPanel : PropArt.LampBulb);
            // The light's halo adds to whatever is behind it, like the skills' glow.
            _halo = new Node2D { Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
            _halo.Draw += DrawHalo;
            AddChild(_halo);
        }
        if (Prop == Prop.Hydrant)
        {
            _nozzle = PropArt.Spot(Variant, PropArt.HydrantLeak);
            _jet = (PropArt.Spot(Variant, PropArt.HydrantLeak + new Vector3(0, -3, 0)) - _nozzle).Normalized();
            // The puddle lies on the ground, under everyone.
            _ground = new Node2D { ZAsRelative = false, ZIndex = 2 };
            _ground.Draw += DrawPuddle;
            AddChild(_ground);
        }
        if (Prop == Prop.Vent) _mouth = PropArt.Spot(Variant, PropArt.VentMouth);
        // The machines (the farmyard's, docs/farmyard-plan.md): steam from the still's chimney and the
        // press's pipe; bubbles rising in the still's chem; glints drifting up off the glowberries.
        if (Prop == Prop.Still) { _mouth = PropArt.Spot(Variant, PropArt.StillChimney); _moteFrom = PropArt.Spot(Variant, PropArt.StillGlass); _moteColour = new Color("#7fffb8"); }
        if (Prop == Prop.ScrapPress) _mouth = PropArt.Spot(Variant, PropArt.PressPipe);
        if (Prop == Prop.BerryBeds) { _moteFrom = PropArt.Spot(Variant, PropArt.BedsBerries); _moteColour = new Color("#ff8ae0"); }
    }

    static ulong H(ulong a, ulong b) => SeededRng.Derive(a, b);

    /// <summary>
    /// Whether the light is on: steady, then a stutter now and then (a broken lamp often, a sign
    /// rarely), and a broken lamp sometimes stays dark a while.
    /// </summary>
    bool LitAt(double t)
    {
        double window = Prop == Prop.Neon ? 7.0 : 2.4;
        long w = (long)(t / window);
        ulong h = H(Seed, (ulong)w);
        double start = h % 1000 / 1000.0 * window * 0.6, length = 0.2 + h / 1000 % 1000 / 1000.0 * (Prop == Prop.Neon ? 0.3 : 1.0);
        double into = t - w * window;
        if (into < start || into > start + length) return true;
        if (Prop == Prop.BrokenLamp && h % 5 == 0) return false; // out for the whole burst
        return H(Seed ^ 0xF11C, (ulong)(t / 0.05)) % 3 == 0;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += delta;
        if (Sprite != null && Prop is Prop.BrokenLamp or Prop.Neon)
        {
            bool on = LitAt(_t);
            if (on != _on)
            {
                _on = on;
                var (tex, anchor) = on ? _lit : _dark;
                Sprite.Texture = tex;
                Sprite.Position = Position - anchor + new Vector2(0, 1);
                // A broken lamp spits sparks as it catches.
                if (on && Prop == Prop.BrokenLamp && _rng.Chance(0.5f))
                    for (int i = 0; i < 4; i++) _bits.Add((_light, new Vector2(_rng.Range(-14f, 14f), _rng.Range(-18f, 2f)), 0.5f));
            }
        }
        if (Prop == Prop.Hydrant)
        {
            // A steady leak: drops leave the broken cap in an arc and splash down.
            for (int i = 0; i < 2; i++)
                if (_rng.Chance(0.75f)) _bits.Add((_nozzle, _jet * _rng.Range(24f, 34f) + new Vector2(_rng.Range(-3f, 3f), _rng.Range(-20f, -12f)), 2f));
        }
        if (Steams(Prop))
        {
            // A puff every few seconds, a few seconds apart, never in step with the next grate.
            double window = Prop == Prop.Still ? 1.8 : 2.8;
            long w = (long)(_t / window);
            double at = H(Seed, (ulong)w) % 1000 / 1000.0 * 1.4;
            double into = _t - w * window;
            if (into >= at && into < at + 0.6 && _rng.Chance(0.4f))
                _bits.Add((_mouth + new Vector2(_rng.Range(-4f, 4f), _rng.Range(-1f, 1f)), new Vector2(_rng.Range(-3f, 3f), _rng.Range(-16f, -10f)), 1.8f));
        }
        if (Prop is Prop.Still or Prop.BerryBeds && _rng.Chance(dt * (Prop == Prop.Still ? 4f : 2.2f)))
            _motes.Add((_moteFrom + new Vector2(_rng.Range(-3f, 3f), _rng.Range(-1f, 2f)), new Vector2(_rng.Range(-1.5f, 1.5f), _rng.Range(-7f, -3f)), Prop == Prop.Still ? 0.6f : 1.4f));
        for (int i = _motes.Count - 1; i >= 0; i--)
        {
            var (p, v, life) = _motes[i];
            life -= dt;
            p += v * dt;
            if (life <= 0) _motes.RemoveAt(i); else _motes[i] = (p, v, life);
        }
        for (int i = _bits.Count - 1; i >= 0; i--)
        {
            var (p, v, life) = _bits[i];
            life -= dt;
            if (Steams(Prop)) { v *= 1 - dt * 0.6f; }
            else v += new Vector2(0, (Prop == Prop.Hydrant ? 120f : 90f) * dt);
            p += v * dt;
            // Water lands on the street a little below the tile's centre, and rings the puddle.
            if (Prop == Prop.Hydrant && p.Y > 3 && v.Y > 0) { _rings.Add((p, 0)); life = 0; }
            if (life <= 0) _bits.RemoveAt(i); else _bits[i] = (p, v, life);
        }
        for (int i = _rings.Count - 1; i >= 0; i--)
        {
            var (p, age) = _rings[i];
            age += dt;
            if (age > 0.45f) _rings.RemoveAt(i); else _rings[i] = (p, age);
        }
        QueueRedraw();
        _halo?.QueueRedraw();
        _ground?.QueueRedraw();
    }

    public override void _Draw()
    {
        DrawMotes();
        foreach (var (p, v, life) in _bits)
        {
            switch (Prop)
            {
                case Prop.BrokenLamp:
                    DrawRect(new Rect2(p.Round(), Vector2.One), new Color(1f, 0.85f, 0.4f, Mathf.Clamp(life * 3, 0, 1)));
                    break;
                case Prop.Hydrant:
                    DrawRect(new Rect2(p.Round(), new Vector2(1, v.Y > 10 ? 2 : 1)), new Color(0.75f, 0.9f, 1f, 0.9f));
                    break;
                case Prop.Vent or Prop.Still or Prop.ScrapPress:
                {
                    float age = 1.8f - life, r = 1.5f + age * 3.2f;
                    DrawCircle(p, r, new Color(0.8f, 0.78f, 0.88f, Mathf.Clamp(life / 1.8f, 0, 1) * 0.35f));
                    break;
                }
            }
        }
    }

    /// <summary>The motes are drawn over everything else of the prop's: a pixel each, fading out.</summary>
    void DrawMotes()
    {
        foreach (var (p, _, life) in _motes)
            DrawRect(new Rect2(p.Round(), Vector2.One), new Color(_moteColour, Mathf.Clamp(life * 2, 0, 1)));
    }

    /// <summary>The light's glow while it's on: a halo round the bulb or sign, and a pool on the street below.</summary>
    void DrawHalo()
    {
        if (!_on) return;
        float flick = 0.9f + 0.1f * Mathf.Sin((float)_t * 37f);
        foreach (var (r, a) in new[] { (14f, 0.07f), (8f, 0.12f), (4f, 0.22f) })
            _halo.DrawCircle(_light, r, new Color(_glow, a * flick));
        if (Prop == Prop.BrokenLamp)
        {
            var pool = new Vector2(_light.X, 2);
            var pts = new Vector2[18];
            for (int i = 0; i < pts.Length; i++) pts[i] = pool + new Vector2(Mathf.Cos(Mathf.Tau * i / pts.Length) * 16, Mathf.Sin(Mathf.Tau * i / pts.Length) * 7);
            _halo.DrawColoredPolygon(pts, new Color(_glow, 0.09f * flick));
        }
    }

    /// <summary>The leak's puddle: dark water with a neon streak in it, rings where the drops land.</summary>
    void DrawPuddle()
    {
        var centre = new Vector2(_nozzle.X + _jet.X * 10, 5);
        var pts = new Vector2[20];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = Mathf.Tau * i / pts.Length;
            float wob = 1 + 0.12f * Mathf.Sin(a * 3 + Seed % 7);
            pts[i] = centre + new Vector2(Mathf.Cos(a) * 15 * wob, Mathf.Sin(a) * 6 * wob);
        }
        _ground.DrawColoredPolygon(pts, new Color(0.12f, 0.2f, 0.32f, 0.7f));
        _ground.DrawLine(centre + new Vector2(-8, -1), centre + new Vector2(5, 1), new Color(PropArt.GlowOf(Variant + 1, "Undercity"), 0.55f), 1);
        foreach (var (p, age) in _rings)
        {
            float r = 1 + age * 9;
            _ground.DrawArc(p - _ground.Position, r, 0, Mathf.Tau, 10, new Color(0.7f, 0.85f, 1f, 0.6f * (1 - age / 0.45f)), 1);
        }
    }
}
