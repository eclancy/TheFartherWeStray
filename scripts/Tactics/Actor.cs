using System.Collections.Generic;
using System.Threading.Tasks;
using TheFartherWeStray.Creatures;
using Godot;

namespace TheFartherWeStray.Tactics;

/// <summary>
/// Anyone standing on the board, sculpted in 3D and animated: a creature in battle (<see cref="UnitView"/>)
/// or anyone in a cutscene, creature or person. It breathes, blinks, walks, turns, strikes, casts,
/// flinches and faints, every frame a re-render of its model, rendered on a worker thread and swapped
/// in as they arrive. Where a tile is on screen is the board's to say.
/// </summary>
public partial class Actor : Node2D
{
    public Texture2D Texture;   // the front view: portraits and the turn strip
    public float SpriteHeight => (_showBack ? _backAnchor : _frontAnchor).Y;
    public Vector2I Tile { get; protected set; }

    /// <summary>Renders one frame: the view, how far through the pose (negative: still), a blink, the pose.</summary>
    public delegate (Image img, Vector2I anchor) Sculpt(View view, float phase, bool blink, Pose pose);

    protected Sprite2D _sprite;
    float _flash;
    Texture2D _back;
    Vector2I _frontAnchor, _backAnchor;
    bool _showBack, _flip;
    float _baseY;

    // Animation frames for every pose and both views: the idle breath (and a blink), then the
    // actions. Until a pose's frames exist the actor breathes, or holds its still pose.
    const int IdleFrames = 8;
    const float IdleFps = 8, WalkFps = 10;
    static readonly (Pose pose, int frames)[] Reel =
        { (Pose.Idle, IdleFrames + 1), (Pose.Walk, 4), (Pose.Strike, 5), (Pose.Cast, 6), (Pose.Hurt, 4), (Pose.Faint, 6) };
    readonly Dictionary<(Pose, int), (Texture2D tex, Vector2I anchor)[]> _frames = new();
    readonly System.Collections.Concurrent.ConcurrentQueue<(Pose pose, int view, int frame, Image img, Vector2I anchor)> _rendered = new();
    double _clock;
    int _beatOffset;
    (Texture2D tex, Vector2I anchor, bool flip) _shown;
    Pose _action = Pose.Idle;
    double _actionStart;
    float _actionLen;
    bool _walking, _fainted;
    readonly System.Threading.CancellationTokenSource _stopRendering = new();
    Task _rendering;

    /// <summary>Where a tile puts this actor's feet on the board.</summary>
    protected virtual Vector2 FeetAt(Vector2I tile) => Vector2.Zero;
    /// <summary>Draw order on that tile: nearer the viewer in front.</summary>
    protected virtual int DepthAt(Vector2I tile) => 100 + (tile.X + tile.Y) * 4;
    /// <summary>Each step of a walk (a footfall or a wingbeat).</summary>
    protected virtual void Stepped(int step) { }

    /// <summary>
    /// Makes the sprite: both still views now, then every pose's frames on a worker thread. Each actor
    /// breathes on its own beat (<paramref name="beat"/>).
    /// </summary>
    protected void Begin(Sculpt sculpt, ulong beat, bool facingBack)
    {
        var (front, fa) = sculpt(View.Front, -1, false, Pose.Idle);
        var (back, ba) = sculpt(View.Back, -1, false, Pose.Idle);
        Texture = ImageTexture.CreateFromImage(front);
        _back = ImageTexture.CreateFromImage(back);
        _frontAnchor = fa;
        _backAnchor = ba;
        foreach (var (pose, n) in Reel)
            for (int view = 0; view < 2; view++) _frames[(pose, view)] = new (Texture2D, Vector2I)[n];
        _sprite = new Sprite2D { Centered = false };
        AddChild(_sprite);
        SetFacing(facingBack, false);

        _beatOffset = (int)(beat % IdleFrames);
        var stop = _stopRendering.Token;
        _rendering = Task.Run(() =>
        {
            foreach (var (pose, n) in Reel)
                foreach (int view in new[] { 0, 1 })
                    for (int f = 0; f < n; f++)
                    {
                        if (stop.IsCancellationRequested) return;
                        bool blink = pose == Pose.Idle && f == IdleFrames;
                        float phase = pose switch
                        {
                            Pose.Idle => blink ? 7f / IdleFrames : f / (float)IdleFrames,
                            Pose.Walk => f / (float)n,
                            _ => f / (float)(n - 1),
                        };
                        var (img, anchor) = sculpt(view == 0 ? View.Front : View.Back, phase, blink, pose);
                        _rendered.Enqueue((pose, view, f, img, anchor));
                    }
        });
    }

    /// <summary>
    /// Leaving the board (a battle ends, the game quits): stop rendering frames and let the one in
    /// hand finish, so no image is left half-made on a worker thread.
    /// </summary>
    public override void _ExitTree()
    {
        _stopRendering.Cancel();
        _rendering?.Wait(2000);
    }

    /// <summary>Plays a pose once over <paramref name="seconds"/>, then goes back to breathing.</summary>
    public async Task Act(Pose pose, float seconds)
    {
        _action = pose;
        _actionStart = _clock;
        _actionLen = seconds;
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    public Task Cast() => Act(Pose.Cast, 0.45f);

    /// <summary>Lies down (the faint pose, held, with no fading) or gets up: a nap at the base.</summary>
    public void Rest(bool down) => _fainted = down;

    /// <summary>A red flash, fading (a hit).</summary>
    protected void Flash() => _flash = 1;

    /// <summary>The shiny reveal: a flash of light, then stars bursting and twinkling out around it.</summary>
    public async Task Glint()
    {
        var flash = CreateTween();
        flash.TweenProperty(_sprite, "modulate", new Color(2.2f, 2.2f, 2.4f), 0.08f);
        flash.TweenProperty(_sprite, "modulate", Colors.White, 0.35f);
        float h = SpriteHeight;
        var spots = new[] { new Vector2(-10, -0.85f), new Vector2(9, -0.6f), new Vector2(-4, -0.3f), new Vector2(12, -0.95f), new Vector2(-13, -0.5f), new Vector2(3, -1.05f) };
        for (int i = 0; i < spots.Length; i++)
        {
            var star = new Sprite2D { Texture = UI.Glyphs.Mini("star", 1), Position = new Vector2(spots[i].X, spots[i].Y * h), Scale = Vector2.Zero, ZIndex = 5 };
            AddChild(star);
            var tw = CreateTween();
            tw.TweenInterval(0.07f * i);
            tw.TweenProperty(star, "scale", new Vector2(1.6f, 1.6f), 0.14f);
            tw.TweenProperty(star, "rotation", Mathf.Pi / 2, 0.3f);
            tw.Parallel().TweenProperty(star, "modulate:a", 0f, 0.3f).SetDelay(0.1f);
            tw.TweenCallback(Callable.From(star.QueueFree));
        }
        await ToSignal(GetTree().CreateTimer(0.07f * spots.Length + 0.5f), SceneTreeTimer.SignalName.Timeout);
    }

    /// <summary>Shows the front or back view, mirrored or not, with its feet on this node.</summary>
    void SetFacing(bool back, bool flip)
    {
        _showBack = back;
        _flip = flip;
        ShowFrame();
    }

    /// <summary>
    /// The frame for now, in the view it faces: the action it's playing, the walk cycle while it
    /// moves, or the idle breath; its still pose until those have rendered.
    /// </summary>
    void ShowFrame()
    {
        int view = _showBack ? 1 : 0;
        (Texture2D tex, Vector2I a) pick = default;
        if (_action != Pose.Idle || _fainted)
        {
            var pose = _fainted ? Pose.Faint : _action;
            var frames = _frames[(pose, view)];
            float progress = _fainted ? 1 : (float)((_clock - _actionStart) / _actionLen);
            if (progress >= 1 && !_fainted && pose != Pose.Faint) _action = Pose.Idle;
            else pick = frames[System.Math.Clamp((int)(progress * frames.Length), 0, frames.Length - 1)];
        }
        else if (_walking) pick = _frames[(Pose.Walk, view)][(int)(_clock * WalkFps) % 4];
        if (pick.tex == null)
        {
            var idle = _frames[(Pose.Idle, view)];
            long beat = (long)(_clock * IdleFps) + _beatOffset;
            int f = (int)(beat % IdleFrames);
            // Every fourth breath ends in a blink.
            if (f == IdleFrames - 1 && beat / IdleFrames % 4 == 3 && idle[IdleFrames].tex != null) f = IdleFrames;
            pick = idle[f];
        }
        var (tex, a) = pick;
        if (tex == null) (tex, a) = _showBack ? (_back, _backAnchor) : (Texture, _frontAnchor);
        if (_shown == (tex, a, _flip)) return;
        _shown = (tex, a, _flip);
        _sprite.Texture = tex;
        _sprite.FlipH = _flip;
        _baseY = -a.Y + 2;
        _sprite.Position = new Vector2(_flip ? -(tex.GetWidth() - a.X) : -a.X, _baseY);
    }

    /// <summary>Turns to face along a screen direction, snapped to the nearest diagonal.</summary>
    public void Face(Vector2 toward)
    {
        if (toward.LengthSquared() < 0.01f) return;
        bool back = Mathf.Abs(toward.Y) < 0.01f ? _showBack : toward.Y < 0;
        bool right = Mathf.Abs(toward.X) < 0.01f ? _flip : toward.X > 0;
        SetFacing(back, right);
    }

    /// <summary>Stands on a tile at once.</summary>
    public void Place(Vector2I tile)
    {
        Tile = tile;
        Position = FeetAt(tile);
        ZIndex = DepthAt(tile);
    }

    public override void _Process(double delta)
    {
        if (_flash > 0) { _flash = Mathf.Max(0, _flash - (float)delta * 4); _sprite.Modulate = Colors.White.Lerp(new Color(1, 0.3f, 0.3f), _flash); }
        while (_rendered.TryDequeue(out var r)) _frames[(r.pose, r.view)][r.frame] = (ImageTexture.CreateFromImage(r.img), r.anchor);
        _clock += delta;
        ShowFrame();
    }

    /// <summary>Walks a path a tile at a time, turning along it and hopping up and down ledges.</summary>
    public async Task Walk(List<Vector2I> path, float secondsPerTile = 0.1f)
    {
        _walking = true;
        var tw = CreateTween();
        for (int i = 1; i < path.Count; i++)
        {
            var to = FeetAt(path[i]);
            var from = FeetAt(path[i - 1]);
            int step = i;
            tw.TweenCallback(Callable.From(() =>
            {
                Face(to - from);
                ZIndex = DepthAt(path[step]);
                Stepped(step);
            }));
            if (Mathf.Abs(to.Y - from.Y) > IsoArt.TH)
            {
                // A hop up or down a ledge.
                var mid = new Vector2((from.X + to.X) / 2, Mathf.Min(from.Y, to.Y) - 8);
                tw.TweenProperty(this, "position", mid, secondsPerTile * 0.7f);
                tw.TweenProperty(this, "position", to, secondsPerTile * 0.7f);
            }
            else tw.TweenProperty(this, "position", to, secondsPerTile);
        }
        await ToSignal(tw, Tween.SignalName.Finished);
        _walking = false;
        Tile = path[^1];
    }

    public async Task Slide(Vector2I to)
    {
        var tw = CreateTween();
        tw.TweenProperty(this, "position", FeetAt(to), 0.12f);
        await ToSignal(tw, Tween.SignalName.Finished);
        Tile = to;
        ZIndex = DepthAt(to);
    }

    public async Task Lunge(Actor target)
    {
        var dir = (target.Position - Position).Normalized();
        Face(target.Position - Position);
        var home = Position;
        // The strike pose winds up, then the body steps in as it lands.
        var strike = Act(Pose.Strike, 0.38f);
        var tw = CreateTween();
        tw.TweenInterval(0.11f);
        tw.TweenProperty(this, "position", home + dir * 6, 0.08f);
        tw.TweenProperty(this, "position", home, 0.14f);
        await ToSignal(tw, Tween.SignalName.Finished);
        await strike;
    }

    public async Task Vanish()
    {
        await Act(Pose.Faint, 0.55f);
        _fainted = true;
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0f, 0.35f);
        await ToSignal(tw, Tween.SignalName.Finished);
        Visible = false;
    }
}
