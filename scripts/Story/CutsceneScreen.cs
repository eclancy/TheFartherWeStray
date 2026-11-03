using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.Tactics;
using TheFartherWeStray.UI;
using Godot;

namespace TheFartherWeStray.Story;

/// <summary>
/// Plays a <see cref="SceneDef"/> on its map, FFTA-style: the cast stand on the board as animated
/// sprites (creatures and people alike), walk, turn, strike and emote, and speak in a box with their
/// portrait while a marker bobs over them. Enter, Space or a click goes on (finishing a line first);
/// Esc skips the scene. With <see cref="Auto"/> the lines move on by themselves.
/// </summary>
public partial class CutsceneScreen : Control
{
    public SceneDef Scene;
    public Ranching.Ranch Ranch;     // your squad (squad1, squad2...), else the starters
    public Mission Mission;          // the pack about to be fought (pack1, pack2...), if any
    public bool Auto;                // lines advance by themselves (--autoplay, recordings)
    public System.Action Done;

    const int Scale = 3;  // closer in than a battle's 2x: scenes are about who's in them
    static readonly Color Gold = Ui.Gold, Ink = Ui.Ink, Dim = Ui.Dim;
    static readonly Vector2 Focus = new(960, 400); // where the camera puts what it looks at

    BattleMap _map;
    IsoArt _art;
    Node2D _board;
    Script _script;
    readonly Dictionary<string, SceneActor> _cast = new();
    PanelContainer _box;
    TextureRect _portrait;
    Label _name, _text, _caption, _more;
    ColorRect _fade;
    SpeakerMarker _marker;
    bool _advance, _skipped, _finished;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _map = BattleMap.ByName(Scene.Map);
        _art = new IsoArt(_map, Tactics.BattleScreen.Skirt);
        _script = Script.Parse(Scene.Script);
        if (Scene.Music != null) Audio.Music.Play(Scene.Music); else Audio.Music.Stop();

        var bg = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(IsoArt.PaintBackdrop(480, 270, SeededRng.Hash(Scene.Id), _map.Theme)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        _board = new Node2D { Scale = new Vector2(Scale, Scale), TextureFilter = TextureFilterEnum.Nearest };
        AddChild(_board);
        _board.AddChild(new Sprite2D { Texture = ImageTexture.CreateFromImage(_art.PaintBoard()), Centered = false });
        PropArt.Place(_board, _map, _art);
        _board.Position = Focus - new Vector2(_art.Width / 2f, _art.Height / 2f) * Scale;

        foreach (var c in _script.Cast) Enter(c);
        _marker = new SpeakerMarker { ZIndex = 450, Visible = false };
        _board.AddChild(_marker);
        BuildUi();
        _ = Run();
    }

    // ------------------------------------------------------------------ the cast

    /// <summary>Puts one of the cast on the board: who they are, where they stand, which way they face.</summary>
    void Enter(CastLine c)
    {
        var actor = new SceneActor { Id = c.Id };
        if (c.Kind == "person")
        {
            var look = People.Look(c.Ref, c.Side);
            actor.Shown = c.Name ?? (c.Ref == "you" ? "You" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(c.Ref.Replace('_', ' ')));
            actor.Setup(FeetAt, (v, ph, bl, po) => PersonSculptor.Render(look, v, ph, bl, po), SeededRng.Hash(c.Ref), 1, MoveType.Walk, true);
        }
        else
        {
            var creature = Creature(c.Ref);
            var unit = Unit.From(creature, Tactics.Side.Wild);
            actor.Shown = c.Name ?? creature.Name;
            actor.Setup(FeetAt, (v, ph, bl, po) => CreatureSculptor.Render(creature, false, v, ph, bl, po), creature.Seed, unit.Size, unit.MoveType, false);
        }
        _board.AddChild(actor);
        actor.Place(Free(Tiles.Resolve(c.At, _map) ?? _map.PlayerAnchor, actor));
        if (c.Face != null) FaceToward(actor, c.Face);
        if (c.Hidden) { actor.Visible = false; actor.Modulate = new Color(1, 1, 1, 0); }
        _cast[c.Id] = actor;
    }

    /// <summary>A creature by its script name: squadN, packN, a cast species ("Blaze"), or wild:SEED.</summary>
    Creature Creature(string who)
    {
        if (who.StartsWith("squad") && int.TryParse(who[5..], out int s))
        {
            var squad = Ranch?.SquadCreatures().Select(x => x.Item1).ToList() ?? Encounter.StarterSquad().Select(x => x.Item1).ToList();
            if (s >= 1 && s <= squad.Count) return squad[s - 1];
        }
        if (who.StartsWith("pack") && int.TryParse(who[4..], out int p))
        {
            var pack = Mission?.Wild().pack;
            if (pack != null && p >= 1 && p <= pack.Count) return pack[p - 1];
            return Creatures.Creature.Specimen(SeededRng.Derive(SeededRng.Hash(Scene.Id), (ulong)p));
        }
        if (who.StartsWith("wild:") && ulong.TryParse(who[5..], out ulong seed)) return Creatures.Creature.Specimen(seed);
        var cast = DrawPetsCast.All.FirstOrDefault(sp => sp.Name == who);
        return cast != null ? Creatures.Creature.Specimen(cast, 0) : Creatures.Creature.Specimen(SeededRng.Hash(who));
    }

    /// <summary>Where a footprint's feet go: the middle of its top face, as in battle.</summary>
    public Vector2 FeetAt(Vector2I tile, int size)
    {
        int h = 0;
        for (int dx = 0; dx < size; dx++)
        for (int dy = 0; dy < size; dy++)
        {
            var t = tile + new Vector2I(dx, dy);
            if (_map.InBounds(t)) h = Mathf.Max(h, _map.HeightAt(t));
        }
        return _art.TileCenter(new Vector2(tile.X + (size - 1) / 2f, tile.Y + (size - 1) / 2f), h);
    }

    bool CanStand(SceneActor a, Vector2I tile, bool others = true)
    {
        for (int dx = 0; dx < a.Size; dx++)
        for (int dy = 0; dy < a.Size; dy++)
        {
            var t = tile + new Vector2I(dx, dy);
            if (!_map.InBounds(t) || _map.Blocked(t)) return false;
            if (a.Move != MoveType.Fly && _map.TerrainAt(t) is Terrain.Pit or Terrain.Chem or Terrain.Water or Terrain.Lava && !(a.Move == MoveType.Swim && _map.TerrainAt(t) is Terrain.Water or Terrain.Chem)) return false;
            if (others && _cast.Values.Any(o => o != a && o.Visible && Overlaps(o, o.Tile, t))) return false;
        }
        return true;
    }

    static bool Overlaps(SceneActor o, Vector2I at, Vector2I t) => t.X >= at.X && t.X < at.X + o.Size && t.Y >= at.Y && t.Y < at.Y + o.Size;

    /// <summary>The nearest tile to this one where the actor can stand, so a script can't put anyone in a wall.</summary>
    Vector2I Free(Vector2I want, SceneActor a)
    {
        for (int r = 0; r < _map.W + _map.H; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (Mathf.Abs(dx) + Mathf.Abs(dy) != r) continue;
            var t = want + new Vector2I(dx, dy);
            if (CanStand(a, t)) return t;
        }
        return want;
    }

    List<Vector2I> PathTo(SceneActor a, Vector2I to) => Paths.Find(_map, a.Tile, to, t => CanStand(a, t, false), a.Move == MoveType.Fly) ?? new List<Vector2I> { a.Tile, to };

    void FaceToward(SceneActor a, string toward)
    {
        if (_cast.TryGetValue(toward, out var other)) { a.Face(other.Position - a.Position); return; }
        if (Tiles.Facing(toward) is { } step) { a.Face(FeetAt(a.Tile + step, 1) - FeetAt(a.Tile, 1)); return; }
        if (Tiles.Resolve(toward, _map) is { } tile) a.Face(FeetAt(tile, 1) - a.Position);
    }

    // ------------------------------------------------------------------ the screen

    void BuildUi()
    {
        // On a layer of its own, above the board (whose sprites sort by depth in the hundreds).
        var ui = new CanvasLayer { Layer = 2 };
        AddChild(ui);
        _caption = Ui.Text("", 44, Gold, 8);
        _caption.HorizontalAlignment = HorizontalAlignment.Center;
        _caption.Position = new Vector2(0, 120);
        _caption.Size = new Vector2(1920, 80);
        _caption.Modulate = new Color(1, 1, 1, 0);
        ui.AddChild(_caption);

        _box = new PanelContainer { Position = new Vector2(260, 806), CustomMinimumSize = new Vector2(1400, 230), Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.04f, 0.09f, 0.92f), BorderColor = new Color(0.55f, 0.47f, 0.25f) };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(18);
        _box.AddThemeStyleboxOverride("panel", style);
        ui.AddChild(_box);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 22);
        _box.AddChild(row);
        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(190, 190), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest,
        };
        row.AddChild(_portrait);
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 10);
        row.AddChild(col);
        _name = Ui.Text("", 28, Gold, 5);
        col.AddChild(_name);
        _text = Ui.Text("", 26, Ink, 4);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(1100, 0);
        col.AddChild(_text);
        _more = Ui.Text("▼", 22, Gold, 4);
        _more.Position = new Vector2(1620, 1000);
        _more.Visible = false;
        ui.AddChild(_more);

        var hint = Ui.Text("Enter: next   Esc: skip", 16, Dim, 4);
        hint.Position = new Vector2(1660, 20);
        ui.AddChild(hint);
        _fade = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        _fade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ui.AddChild(_fade);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.Escape) { _skipped = true; Finish(); }
            else if (k.Keycode is Key.Enter or Key.KpEnter or Key.Space or Key.Z) _advance = true;
            else return;
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _advance = true;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_more.Visible) _more.Position = new Vector2(1620, 996 + Mathf.Sin(Time.GetTicksMsec() / 180f) * 3);
    }

    // ------------------------------------------------------------------ running the script

    async Task Run()
    {
        var alongside = new List<Task>();
        for (int i = 0; i < _script.Steps.Count && !_skipped; i++)
        {
            var step = _script.Steps[i];
            var next = i + 1 < _script.Steps.Count ? _script.Steps[i + 1] : null;
            var task = Do(step, next);
            if (step.Alongside) { alongside.Add(task); continue; }
            await task;
            foreach (var t in alongside) await t;
            alongside.Clear();
        }
        foreach (var t in alongside) await t;
        Finish();
    }

    async Task Do(Step s, Step next)
    {
        _cast.TryGetValue(s.Who ?? "", out var a);
        switch (s.Verb)
        {
            case "say":
                await Speak(a, s.Text, next?.Verb is "say" or "narrate");
                break;
            case "narrate":
                await Speak(null, s.Text, next?.Verb is "say" or "narrate");
                break;
            case "title":
                await Caption(s.Text);
                break;
            case "walk" when a != null && Tiles.Resolve(s.Args[0], _map) is { } to:
                await a.Walk(PathTo(a, Free(to, a)), a.Person ? 0.24f : 0.2f);
                break;
            case "leave" when a != null:
                if (s.Args.Length == 1 && Tiles.Resolve(s.Args[0], _map) is { } off) await a.Walk(PathTo(a, off), 0.22f);
                await TweenTo(a, "modulate:a", 0f, 0.35f);
                a.Visible = false;
                break;
            case "appear" when a != null:
                a.Place(Free(a.Tile, a));
                a.Visible = true;
                await TweenTo(a, "modulate:a", 1f, 0.4f);
                break;
            case "face" when a != null:
                FaceToward(a, s.Args[0]);
                await Wait(0.15f);
                break;
            case "emote" when a != null:
                await Emote(a, s.Args[0]);
                break;
            case "pose" when a != null:
                Audio.Sound.Play(s.Args[0] switch { "strike" => "swing", "cast" => "cast", "hurt" => "hit", _ => "faint" });
                await a.Act(s.Args[0] switch { "strike" => Pose.Strike, "cast" => Pose.Cast, "hurt" => Pose.Hurt, _ => Pose.Faint },
                    s.Args[0] switch { "strike" => 0.38f, "cast" => 0.6f, "hurt" => 0.32f, _ => 0.6f });
                break;
            case "pan":
                var at = a != null ? (Vector2?)(a.Position - new Vector2(0, a.SpriteHeight / 2)) : Tiles.Resolve(s.Args[0], _map) is { } tile ? FeetAt(tile, 1) : null;
                if (at is { } spot) await TweenTo(_board, "position", Focus - spot * Scale, 0.7f);
                break;
            case "wait":
                await Wait(float.Parse(s.Args[0], CultureInfo.InvariantCulture));
                break;
            case "fade":
                await TweenTo(_fade, "color:a", s.Args[0] == "out" ? 1f : 0f, 0.6f);
                break;
            case "sound":
                Audio.Sound.Play(s.Args[0]);
                break;
            case "music":
                if (s.Args[0] == "stop") Audio.Music.Stop(); else Audio.Music.Play(s.Args[0]);
                break;
            case "shake":
                var home = _board.Position;
                var tw = CreateTween();
                foreach (var d in new[] { 6, -5, 4, -3, 2, 0 }) tw.TweenProperty(_board, "position", home + new Vector2(d, d / 2f), 0.04f);
                await ToSignal(tw, Godot.Tween.SignalName.Finished);
                break;
        }
    }

    /// <summary>A line in the box: who says it (their portrait, their name, a marker over them), typed out, then wait to go on.</summary>
    async Task Speak(SceneActor who, string text, bool keepOpen)
    {
        _box.Visible = true;
        _portrait.Visible = who != null;
        if (who != null)
        {
            // People show from the chest up; creatures whole.
            var tex = who.Texture;
            _portrait.Texture = who.Person
                ? new AtlasTexture { Atlas = tex, Region = new Rect2(0, 0, tex.GetWidth(), Mathf.Ceil(tex.GetHeight() * 0.55f)) }
                : tex;
            _marker.Visible = true;
            _marker.Position = who.Position + new Vector2(0, -who.SpriteHeight - 8);
            _marker.ZIndex = 450;
        }
        else _marker.Visible = false;
        _name.Text = who?.Shown ?? "";
        _name.Visible = who != null;
        _text.Text = text;
        _text.Modulate = who == null ? Dim.Lightened(0.3f) : Colors.White;
        _text.VisibleCharacters = 0;
        _advance = false;
        Audio.Sound.Play("tick");
        // Typed out; a press shows the rest at once.
        for (float shown = 0; shown < text.Length && !_skipped; shown += (float)GetProcessDeltaTime() * Settings.CharsPerSecond)
        {
            if (_advance) { _advance = false; break; }
            _text.VisibleCharacters = (int)shown;
            await NextFrame();
        }
        _text.VisibleCharacters = -1;
        _more.Visible = true;
        if (Auto) await Wait(0.9f + text.Length * 0.035f);
        else
            while (!_advance && !_skipped) await NextFrame();
        _advance = false;
        _more.Visible = false;
        _marker.Visible = false;
        if (!keepOpen) _box.Visible = false;
        Audio.Sound.Play("click", 1.1f, -6);
    }

    async Task Caption(string text)
    {
        _caption.Text = text;
        await TweenTo(_caption, "modulate:a", 1f, 0.4f);
        await Wait(1.4f);
        await TweenTo(_caption, "modulate:a", 0f, 0.4f);
    }

    async Task Emote(SceneActor a, string kind)
    {
        var balloon = new EmoteBalloon { Kind = kind, Position = a.Position + new Vector2(0, -a.SpriteHeight - 6), ZIndex = 460, Scale = new Vector2(0.2f, 0.2f) };
        _board.AddChild(balloon);
        Audio.Sound.Play(kind == "!" ? "turn" : "tick", kind == "?" ? 0.8f : 1f);
        var tw = CreateTween();
        tw.TweenProperty(balloon, "scale", Vector2.One, 0.12f).SetTrans(Godot.Tween.TransitionType.Back).SetEase(Godot.Tween.EaseType.Out);
        tw.TweenInterval(0.75f);
        tw.TweenProperty(balloon, "modulate:a", 0f, 0.2f);
        tw.TweenCallback(Callable.From(balloon.QueueFree));
        await ToSignal(tw, Godot.Tween.SignalName.Finished);
    }

    /// <summary>The end: fade to black, then whatever comes next (the battle, or home).</summary>
    async void Finish()
    {
        if (_finished) return;
        _finished = true;
        _box.Visible = false;
        if (_fade.Color.A < 1) await TweenTo(_fade, "color:a", 1f, 0.35f);
        Done?.Invoke();
    }

    async Task TweenTo(GodotObject target, string property, Variant to, float seconds)
    {
        var tw = CreateTween();
        tw.TweenProperty(target, property, to, seconds).SetTrans(Godot.Tween.TransitionType.Sine).SetEase(Godot.Tween.EaseType.InOut);
        await ToSignal(tw, Godot.Tween.SignalName.Finished);
    }

    async Task Wait(float seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
}

/// <summary>The marker bobbing over whoever is speaking.</summary>
public partial class SpeakerMarker : Node2D
{
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        float y = Mathf.Sin(Time.GetTicksMsec() / 180f) * 1.5f;
        DrawColoredPolygon(new[] { new Vector2(-4, y - 5), new Vector2(4, y - 5), new Vector2(0, y) }, Ui.Gold);
        DrawPolyline(new[] { new Vector2(-4, y - 5), new Vector2(4, y - 5), new Vector2(0, y), new Vector2(-4, y - 5) }, new Color(0.1f, 0.07f, 0.02f), 1);
    }
}

/// <summary>A speech balloon over someone: !, ?, ... or a glyph.</summary>
public partial class EmoteBalloon : Node2D
{
    public string Kind;
    public override void _Draw()
    {
        var rect = new Rect2(-8, -13, 16, 12);
        var box = new StyleBoxFlat { BgColor = new Color(0.97f, 0.95f, 0.9f), BorderColor = new Color(0.1f, 0.08f, 0.14f) };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(4);
        DrawStyleBox(box, rect);
        DrawColoredPolygon(new[] { new Vector2(-2, -1.5f), new Vector2(2, -1.5f), new Vector2(0, 2) }, new Color(0.97f, 0.95f, 0.9f));
        if (Kind is "!" or "?" or "...")
        {
            var font = ThemeDB.FallbackFont;
            var color = Kind == "!" ? new Color(0.85f, 0.15f, 0.2f) : new Color(0.15f, 0.12f, 0.25f);
            DrawString(font, new Vector2(-8, -3.5f), Kind, HorizontalAlignment.Center, 16, 10, color);
        }
        else DrawTexture(Glyphs.Get(Kind, 1), new Vector2(-6, -13));
    }
}

/// <summary>Walks over a map: the shortest way across ground one can stand on, ledges of two levels at most (fliers any).</summary>
public static class Paths
{
    public static List<Vector2I> Find(BattleMap map, Vector2I from, Vector2I to, System.Func<Vector2I, bool> canStand, bool fly)
    {
        var back = new Dictionary<Vector2I, Vector2I> { [from] = from };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(from);
        var dirs = new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) };
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (t == to) break;
            foreach (var d in dirs)
            {
                var n = t + d;
                if (back.ContainsKey(n) || !canStand(n)) continue;
                if (!fly && Mathf.Abs(map.HeightAt(n) - map.HeightAt(t)) > 2) continue;
                back[n] = t;
                queue.Enqueue(n);
            }
        }
        if (!back.ContainsKey(to)) return null;
        var path = new List<Vector2I> { to };
        while (path[^1] != from) path.Add(back[path[^1]]);
        path.Reverse();
        return path;
    }
}

/// <summary>Someone in a cutscene or walking the base: an <see cref="Actor"/> with a shadow, a name, and how it gets about.</summary>
public partial class SceneActor : Actor
{
    public string Id;
    public string Shown;      // the name in the dialogue box
    public int Size = 1;
    public MoveType Move;
    public bool Person;
    System.Func<Vector2I, int, Vector2> _feet;

    /// <summary>Makes the sprite; <paramref name="feet"/> says where a tile (and footprint size) puts its feet on the board.</summary>
    public void Setup(System.Func<Vector2I, int, Vector2> feet, Sculpt sculpt, ulong beat, int size, MoveType move, bool person)
    {
        _feet = feet;
        Size = size;
        Move = move;
        Person = person;
        Begin(sculpt, beat, false);
    }

    protected override Vector2 FeetAt(Vector2I tile) => _feet(tile, Size);
    protected override int DepthAt(Vector2I tile) => 100 + (tile.X + tile.Y + 2 * (Size - 1)) * 4;

    protected override void Stepped(int step)
    {
        if (Move == MoveType.Fly) { if (step % 2 == 1) Audio.Sound.Play("flap", 1, -6); }
        else Audio.Sound.Play("step", Person ? 1.25f : 1, -8);
    }

    public override void _Draw()
    {
        float rx = IsoArt.TW * 0.3f * Size, ry = IsoArt.TH * 0.3f * Size;
        var pts = new Vector2[20];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = Mathf.Tau * i / pts.Length;
            pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        DrawColoredPolygon(pts, new Color(0, 0, 0, 0.32f));
    }
}
