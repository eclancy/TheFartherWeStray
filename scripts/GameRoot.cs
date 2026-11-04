using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TheFartherWeStray.Creatures;
using TheFartherWeStray.SpriteLab;
using Godot;

namespace TheFartherWeStray;

/// <summary>
/// Entry point: the ranch by default, or a one-off battle, or the Sprite Lab.
/// Command-line options come after "--", e.g. `--path . -- --sheet=42 --four`:
///   --sheet=N         start on sheet N
///   --species=X       open the species view: a species seed (16 hex digits, shown on hover) or a DrawPets name
///   --drawpets        start on the DrawPets cast
///   --four            start in 4-colour mode
///   (no options)      the ranch: your roster, breeding, and battles from it (saved to user://ranch.json)
///   --save=NAME       use the ranch saved as user://NAME.json
///   --newranch        start a fresh ranch (overwrites the save once anything is saved)
///   --style=ID        art style: neon (default), ffta, painted, noir, grim (V cycles them in the Sprite Lab)
///   --lab             open the Sprite Lab (Tab switches)
///   --battle=N        a one-off battle with the default squad; --autoplay lets the AI play your side,
///                     --aim aims the first skill at the busiest tile (for screenshots of the skill panel)
///   --autobattle=N    run a battle AI-vs-AI headless and print the log
///   --shot=N          save frame N to .shots/shot.png and quit
///   --webkit[=DIR]    write the strays, menu frames and glyphs the game's ecec.dev page shows (headless)
///   --ui=NAME         another menu style on every button and panel: plain, gilded, neon, iron (giltneon is
///                     the game's; UI/UiSkin.cs, docs/ui-style.md); --uiboard=NAME shows a style's parts side by side
///   --export=FILE     write the sheet (or species) as a PNG and quit; works with --headless
///   --scale=N         export pixel scale (default 3)
///   --columns=N --from=N --count=N   export layout: columns, first slot (1-based) and how many (8, 1, 24)
/// </summary>
public partial class GameRoot : Node
{
    public override void _Ready()
    {
        // On a phone (or with --phone) the game runs inside the phone frame, between the pad's columns.
        if (UI.Phone.Wanted() && !_framed)
        {
            _framed = true;
            UI.Phone.Begin();
            // No hover tooltips (a finger left resting would bring one up over the pad's own): a
            // viewport reads the delay when it's made, so before the game's is.
            ProjectSettings.SetSetting("gui/timers/tooltip_delay_sec", 3600f);
            AddChild(new UI.PhoneFrame { Game = new GameRoot() });
            return;
        }
        // A crash during setup quits instead of leaving a headless run hanging until its timeout.
        try { Start(); }
        catch (Exception e) { GD.PrintErr(e.ToString()); GetTree().Quit(1); }
    }

    static bool _framed;

    /// <summary>The game's name (the user's, 2026-11-02), as the project's config/name has it too.</summary>
    public const string Title = "The Farther We Stray";

    /// <summary>
    /// Saves and settings from before the game had its name: user:// follows the project's name, so
    /// when the game was renamed they were left in the old folder. Copied over once, never overwriting.
    /// </summary>
    static void BringOverOldSaves()
    {
        try
        {
            string now = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
            string old = System.IO.Path.Combine(System.IO.Directory.GetParent(now.TrimEnd('/', System.IO.Path.DirectorySeparatorChar))?.FullName ?? "", "Creature Tactics");
            if (!System.IO.Directory.Exists(old) || System.IO.Path.GetFullPath(old) == now) return;
            foreach (var file in System.IO.Directory.GetFiles(old))
            {
                string to = System.IO.Path.Combine(now, System.IO.Path.GetFileName(file));
                if (!System.IO.File.Exists(to) && (file.EndsWith(".json") || file.EndsWith(".cfg"))) System.IO.File.Copy(file, to);
            }
        }
        catch (Exception e) { GD.PrintErr($"Couldn't bring over old saves: {e.Message}"); }
    }

    void Start()
    {
        var args = ParseArgs(OS.GetCmdlineUserArgs());
        BringOverOldSaves();
        // Sound: every button clicks; N mutes, --mute starts muted.
        AddChild(new Audio.Sound());
        // The player's settings (volumes, mute, text speed, turn timer, fullscreen); --mute mutes on top.
        UI.Settings.Load();
        if (args.ContainsKey("mute")) Audio.Sound.Muted = true;
        // Music: rendered on a worker thread once the game starts (not for headless tests); --nomusic turns it off.
        Audio.Music.Off = args.ContainsKey("nomusic");
        if (DisplayServer.GetName() != "headless") AddChild(new Audio.Music());
        GetTree().NodeAdded += node => { if (node is BaseButton button) button.Pressed += () => Audio.Sound.Play("click"); };
        // The phone's pad finds its way round every menu (UI/PadNav.cs).
        if (UI.Phone.On)
        {
            UI.PadNav.Theme();
            GetTree().NodeAdded += UI.PadNav.Adapt;
            AddChild(new UI.PadNav());
            // --pad=down,down,a,...: presses the pad's buttons one by one, after the screen settles (checking it with --shot).
            if (args.TryGetValue("pad", out var presses)) PressPad(presses.Split(','), args.TryGetValue("padwait", out var pw) && float.TryParse(pw, CultureInfo.InvariantCulture, out var w) ? w : 1.5f);
        }
        // The menus' look (UI/UiSkin.cs, docs/ui-style.md): gilt and neon, or --ui=NAME for one of the styles it was
        // picked from. After the phone's focus frame, so the style's own wins.
        UI.UiSkin.Use(args.TryGetValue("ui", out var uiName) && UI.UiSkin.TryParse(uiName, out var uiSkin) ? uiSkin : UI.Skin.GiltNeon);
        GetTree().NodeAdded += UI.UiSkin.Adapt;

        if (args.ContainsKey("slotstest"))
        {
            // Save slots and settings without a screen, in test files (--slots=NAME, default slots_test):
            // two games saved, their summaries read back, one deleted and made again; the settings kept
            // after a save and load. Leaves slots 2 and 3 for a look at the title (--slots=NAME --title=load).
            Ranching.Ranch.SlotPrefix = "user://" + args.GetValueOrDefault("slots", "slots_test");
            for (int sl = 1; sl <= Ranching.Ranch.Slots; sl++) Ranching.Ranch.DeleteSlot(sl);
            Ranching.Ranch MakeSlot(int sl, int day, int chem, string at, params string[] story)
            {
                var r = Ranching.Ranch.New();
                (r.Day, r.Chem, r.Location) = (day, chem, at);
                foreach (var m in story) r.Story.Add(m);
                Ranching.Ranch.SavePath = Ranching.Ranch.SlotPath(sl);
                r.Save();
                return r;
            }
            var two = MakeSlot(2, 12, 340, "lantern", "main_foundry");
            var three = MakeSlot(3, 94, 2029, "foundry", "main_foundry", "main_bridges", "main_coast");
            bool kept = Ranching.Ranch.Peek(2)?.Day == 12 && Ranching.Ranch.Peek(3)?.Chem == 2029 && Ranching.Ranch.Peek(1) == null;
            GD.Print($"slots used: {string.Join(", ", Enumerable.Range(1, Ranching.Ranch.Slots).Where(Ranching.Ranch.SlotUsed))}; read back {(kept ? "identical" : "DIFFERENT")}");
            for (int sl = 1; sl <= Ranching.Ranch.Slots; sl++)
                GD.Print($"  slot {sl}: {(Ranching.Ranch.Peek(sl) is { } r ? r.Summary() + $" (saved {Ranching.Ranch.SlotSaved(sl):HH:mm})" : "empty")}");
            Ranching.Ranch.DeleteSlot(3);
            bool gone = !Ranching.Ranch.SlotUsed(3);
            MakeSlot(3, 94, 2029, "foundry", "main_foundry", "main_bridges", "main_coast");
            GD.Print($"delete: slot 3 {(gone ? "gone" : "STILL THERE")}, then made again");
            UI.Settings.Path = "user://settings_test.cfg";
            (UI.Settings.Music, UI.Settings.Effects, UI.Settings.TextSpeed, UI.Settings.TurnTimer, UI.Settings.LastSlot) = (0.35f, 0.6f, 2, 30, 3);
            UI.Settings.Save();
            (UI.Settings.Music, UI.Settings.Effects, UI.Settings.TextSpeed, UI.Settings.TurnTimer, UI.Settings.LastSlot) = (1, 1, 0, 0, 1);
            UI.Settings.Load();
            bool same = Mathf.IsEqualApprox(UI.Settings.Music, 0.35f) && Mathf.IsEqualApprox(UI.Settings.Effects, 0.6f) && UI.Settings.TextSpeed == 2 && UI.Settings.TurnTimer == 30 && UI.Settings.LastSlot == 3;
            GD.Print($"settings: {(same ? "kept" : "DIFFERENT")} after save and load (music {UI.Settings.Music:0.00}, effects {UI.Settings.Effects:0.00}, text {UI.Settings.Speeds[UI.Settings.TextSpeed]}, timer {UI.Settings.TurnTimer}s, last slot {UI.Settings.LastSlot})");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("scenetest"))
        {
            // Every cutscene read and checked: lines understood, everyone named is cast, every tile on
            // the map and somewhere to stand, every sound and track real.
            int problems = 0;
            foreach (var sc in Story.SceneBook.All)
            {
                var found = Story.SceneBook.Problems(sc).ToList();
                var parsed = Story.Script.Parse(sc.Script);
                GD.Print($"{sc.Id,-18} on {sc.Map,-18} {parsed.Cast.Count} cast, {parsed.Steps.Count} steps ({parsed.Steps.Count(x => x.Verb == "say")} lines){(sc.Placeholder ? ", placeholder" : "")}: {(found.Count == 0 ? "reads" : $"{found.Count} problems")}");
                foreach (var line in found) GD.Print("  " + line);
                problems += found.Count;
            }
            var mains = World.StoryBook.All.Where(m => m.Scene != null).ToList();
            foreach (var m in mains.Where(m => Story.SceneBook.Get(m.Scene) == null)) { GD.Print($"  {m.Id} names a scene that doesn't exist: {m.Scene}"); problems++; }
            GD.Print($"scenes: {Story.SceneBook.All.Length}, played before {mains.Count} main missions; problems: {problems}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("icon"))
        {
            // The app's icons (icon/): Blaze, the first starter, on the night colour with a chem glow. The
            // Android launcher's three (named in export_presets.cfg: the adaptive layers 432 square, the
            // old square 192) and the window's.
            var sprite = CreaturePainter.Paint(Tactics.Encounter.StarterSquad().First().Item1, false);
            sprite = sprite.GetRegion(sprite.GetUsedRect());
            Image Back(int n)
            {
                var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
                Color top = Color.FromHtml("#2a1a48"), bottom = Color.FromHtml("#0b0914"), glow = Color.FromHtml("#4fe3ff");
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float g = Mathf.Clamp(1 - new Vector2(x - n / 2f, y - n * 0.56f).Length() / (n * 0.4f), 0, 1);
                    img.SetPixel(x, y, top.Lerp(bottom, y / (n - 1f)).Lerp(glow, g * g * 0.4f));
                }
                return img;
            }
            Image Front(int n, float fill)
            {
                var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
                int k = Math.Max(1, (int)(n * fill / Math.Max(sprite.GetWidth(), sprite.GetHeight())));
                var big = (Image)sprite.Duplicate();
                big.Resize(sprite.GetWidth() * k, sprite.GetHeight() * k, Image.Interpolation.Nearest);
                img.BlendRect(big, new Rect2I(Vector2I.Zero, big.GetSize()), new Vector2I((n - big.GetWidth()) / 2, (n - big.GetHeight()) / 2));
                return img;
            }
            Image Whole(int n)
            {
                var img = Back(n);
                var front = Front(n, 0.8f);
                img.BlendRect(front, new Rect2I(Vector2I.Zero, front.GetSize()), Vector2I.Zero);
                return img;
            }
            string iconDir = ProjectSettings.GlobalizePath("res://icon");
            DirAccess.MakeDirRecursiveAbsolute(iconDir);
            Back(432).SavePng(iconDir + "/android-background.png");
            Front(432, 0.56f).SavePng(iconDir + "/android-foreground.png");
            Whole(192).SavePng(iconDir + "/android-main.png");
            // The project's and the window's: Blaze in the menus' gilt-and-neon frame (UI/UiSkin.cs), its
            // tube lit, on glass with a chem glow, painted 64 pixels square to read in Godot's project
            // list, then made 256. Blaze at 3x (its size on the board) fills the glass like a portrait, cut off
            // by the frame where it reaches it.
            var frame = UI.UiSkin.Paint(UI.Skin.GiltNeon, UI.UiPart.Panel, UI.UiState.Normal, 64, 64, lit: true);
            for (int y = 8; y < 56; y++)
            for (int x = 8; x < 56; x++)
            {
                // A glow behind the creature in three bands, stepped like the board's light.
                float d = new Vector2(x - 31.5f, y - 35).Length();
                float band = d < 12 ? 0.3f : d < 18 ? 0.18f : d < 24 ? 0.08f : 0;
                if (band > 0) frame.SetPixel(x, y, frame.GetPixel(x, y).Lerp(Color.FromHtml("#4fe3ff"), band));
            }
            frame.Resize(256, 256, Image.Interpolation.Nearest);
            var blaze = (Image)sprite.Duplicate();
            blaze.Resize(sprite.GetWidth() * 3, sprite.GetHeight() * 3, Image.Interpolation.Nearest);
            var at = new Vector2I((256 - blaze.GetWidth()) / 2, (256 - blaze.GetHeight()) / 2 + 6);
            var glass = new Rect2I(26, 26, 204, 204); // inside the frame's tube
            var shown = new Rect2I(at, blaze.GetSize()).Intersection(glass);
            frame.BlendRect(blaze, new Rect2I(shown.Position - at, shown.Size), shown.Position);
            frame.SavePng(iconDir + "/icon.png");
            // The same for Windows (the exe and its installer): an .ico holding it at every size the
            // shell asks for, the small ones as plain bitmaps, the 256 as a PNG.
            using (var ico = new System.IO.BinaryWriter(System.IO.File.Create(iconDir + "/icon.ico")))
            {
                int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
                var images = sizes.Select(n =>
                {
                    var img = (Image)frame.Duplicate();
                    img.Resize(n, n, Image.Interpolation.Lanczos);
                    if (n == 256) return img.SavePngToBuffer();
                    using var bmp = new System.IO.MemoryStream();
                    using var w = new System.IO.BinaryWriter(bmp);
                    w.Write(40); w.Write(n); w.Write(n * 2); w.Write((short)1); w.Write((short)32); w.Write(0); w.Write(n * n * 4 + n * ((n + 31) / 32 * 4)); w.Write(0L); w.Write(0L);
                    for (int y = n - 1; y >= 0; y--)
                    for (int x = 0; x < n; x++)
                    {
                        var c = img.GetPixel(x, y);
                        w.Write((byte)(c.B8)); w.Write((byte)(c.G8)); w.Write((byte)(c.R8)); w.Write((byte)(c.A8));
                    }
                    w.Write(new byte[n * ((n + 31) / 32 * 4)]); // the mask: unused, the alpha says it all
                    w.Flush();
                    return bmp.ToArray();
                }).ToList();
                ico.Write((short)0); ico.Write((short)1); ico.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    ico.Write((byte)(sizes[i] % 256)); ico.Write((byte)(sizes[i] % 256)); ico.Write((byte)0); ico.Write((byte)0);
                    ico.Write((short)1); ico.Write((short)32); ico.Write(images[i].Length); ico.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var data in images) ico.Write(data);
            }
            GD.Print($"icons written to {iconDir}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("people"))
        {
            // People for cutscenes: twelve from each side (every fourth from behind), then one person through
            // every pose, drawn at 3x on a dark ground, to .shots/people.png.
            var folk = new List<(Image img, Vector2I anchor)>();
            foreach (var side in new[] { World.Side.City, World.Side.Undercity, World.Side.Wilds })
                for (int i = 0; i < 12; i++) folk.Add(Story.PersonSculptor.Render(Story.People.Look($"{side}{i}", side), i % 4 == 3 ? View.Back : View.Front));
            var you = Story.People.Look("Undercity0", World.Side.Undercity);
            foreach (var (pose, frames) in new[] { (Pose.Idle, 4), (Pose.Walk, 4), (Pose.Strike, 5), (Pose.Cast, 6), (Pose.Hurt, 4), (Pose.Faint, 6) })
                for (int fr = 0; fr < frames; fr++) folk.Add(Story.PersonSculptor.Render(you, View.Front, fr / (float)(pose == Pose.Walk || pose == Pose.Idle ? frames : frames - 1), false, pose));
            int scale = 3, cellW = 72, cellH = 84, cols = 12, rows = (folk.Count + cols - 1) / cols;
            var outImg = Image.CreateEmpty(cols * cellW * scale, rows * cellH * scale, false, Image.Format.Rgba8);
            outImg.Fill(new Color(0.1f, 0.08f, 0.16f));
            for (int i = 0; i < folk.Count; i++)
            {
                var (img, anchor) = folk[i];
                int ox = (i % cols) * cellW + cellW / 2 - anchor.X, oy = (i / cols) * cellH + cellH - 6 - anchor.Y;
                for (int y = 0; y < img.GetHeight(); y++)
                for (int x = 0; x < img.GetWidth(); x++)
                {
                    var c = img.GetPixel(x, y);
                    if (c.A < 0.5f) continue;
                    outImg.FillRect(new Rect2I((ox + x) * scale, (oy + y) * scale, scale, scale), c);
                }
            }
            outImg.SavePng(ProjectSettings.GlobalizePath("res://.shots/people.png"));
            GD.Print($"wrote {folk.Count} people sprites to .shots/people.png");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("music"))
        {
            // Every song as a .wav in .shots/music/, played twice through so the loop's seam can be heard,
            // with its length, levels, how long it took to make, and any bar that doesn't add up.
            string dir = ProjectSettings.GlobalizePath("res://.shots/music");
            System.IO.Directory.CreateDirectory(dir);
            foreach (var problem in Audio.SongBook.All.SelectMany(Audio.MusicSynth.Problems)) GD.Print("PROBLEM " + problem);
            foreach (var song in Audio.SongBook.All)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var loop = Audio.MusicSynth.Render(song);
                long ms = clock.ElapsedMilliseconds;
                Audio.SfxSynth.ToStream(loop.Concat(loop).ToArray()).SaveToWav($"{dir}/{song.Id}.wav");
                float peak = loop.Max(System.Math.Abs), rms = (float)System.Math.Sqrt(loop.Average(x => x * x));
                float seam = System.Math.Abs(loop[0] - loop[^1]), step = loop.Zip(loop.Skip(1), (a, b) => System.Math.Abs(a - b)).Average();
                GD.Print($"{song.Id,-13} {song.Name,-18} {song.Bpm,3} bpm  {song.Bars} bars  {loop.Length / (float)Audio.SfxSynth.Rate,5:0.0}s loop  peak {peak:0.00}  rms {rms:0.000}  seam jump {seam:0.000} (a step averages {step:0.000})  made in {ms} ms");
                // The balance: each part's share of the mix's loudness, weighted roughly as the ear hears
                // (a gentle cut below about 300 Hz, so a booming bass doesn't count for more than it sounds).
                float Level(Audio.Part[] parts)
                {
                    var raw = Audio.MusicSynth.Render(new Audio.Song { Id = song.Id, Bpm = song.Bpm, Bars = song.Bars, Parts = parts }, false);
                    float rc = 1 / (System.MathF.Tau * 300), a = rc / (rc + 1f / Audio.SfxSynth.Rate), y = 0, prev = 0;
                    double sum = 0;
                    foreach (var x in raw) { y = a * (y + x - prev); prev = x; sum += y * y; }
                    return (float)System.Math.Sqrt(sum / raw.Length);
                }
                float all = Level(song.Parts);
                GD.Print("    " + string.Join("  ", song.Parts.Select(part => $"{part.Inst} {Level(new[] { part }) / all * 100:0}%")));
            }
            GD.Print($"wrote {Audio.SongBook.All.Length} songs to {dir}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("sfx"))
        {
            // Every sound as a .wav in .shots/sfx/, and all of them in a row in reel.wav, with levels.
            string dir = ProjectSettings.GlobalizePath("res://.shots/sfx");
            System.IO.Directory.CreateDirectory(dir);
            var reel = new List<float>();
            foreach (var name in Audio.SoundBook.All.Keys)
            {
                var samples = Audio.SoundBook.Render(name);
                Audio.SfxSynth.ToStream(samples).SaveToWav($"{dir}/{name}.wav");
                reel.AddRange(samples);
                reel.AddRange(new float[Audio.SfxSynth.Rate * 2 / 5]);
                float peak = samples.Max(System.Math.Abs), rms = (float)System.Math.Sqrt(samples.Average(x => x * x));
                GD.Print($"{name,-10} {samples.Length / (float)Audio.SfxSynth.Rate:0.00}s  peak {peak:0.00}  rms {rms:0.000}");
            }
            Audio.SfxSynth.ToStream(reel.ToArray()).SaveToWav($"{dir}/reel.wav");
            GD.Print($"wrote {Audio.SoundBook.All.Count} sounds and reel.wav ({reel.Count / (float)Audio.SfxSynth.Rate:0.0}s) to {dir}");
            GetTree().Quit();
            return;
        }
        if (args.TryGetValue("style", out var styleId)) ArtStyle.Current = ArtStyle.ById(styleId);
        ulong sheet = args.TryGetValue("sheet", out var s) && ulong.TryParse(s, out var n) ? n : 1;
        bool four = args.ContainsKey("four"), drawPets = args.ContainsKey("drawpets");
        Species species = null;
        if (args.TryGetValue("species", out var sp))
        {
            if (ulong.TryParse(sp, NumberStyles.HexNumber, null, out var h)) species = Species.Generate(h);
            else foreach (var pet in DrawPetsCast.All)
                if (string.Equals(pet.Name, sp, StringComparison.OrdinalIgnoreCase)) species = pet;
        }

        if (args.TryGetValue("autobattle", out var ab))
        {
            // Both sides played by the AI, printed as text: the quick check on battle rules.
            var battle = Tactics.Encounter.Create(ulong.TryParse(ab, out var bs) ? bs : 1);
            foreach (var u in battle.Units)
                GD.Print($"{u.Side,-6} {u.Name,-12} {u.Creature.Size,-6} {u.Attitude,-10} HP {u.MaxHp} ATK {u.Atk} DEF {u.Def} SPD {u.Spd} MOV {u.Move} {u.MoveType} {u.Weapon} at {u.Pos}");
            while (battle.Winner == null && battle.Round <= 30)
            {
                var u = battle.NextTurn();
                if (u == null) break;
                battle.AiTurn(u);
            }
            foreach (var e in battle.Events) GD.Print(Tactics.Encounter.Describe(e));
            GD.Print($"rounds {battle.Round}, winner {battle.Winner?.ToString() ?? "none"}, recruited {battle.Recruited.Count}");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("autoboard", out var boards))
        {
            // Every mission on boards 1..N, AI against AI: catches maps where packs can't reach each other.
            int boardCount = int.TryParse(boards, out var nb) ? nb : 3;
            int stalls = 0;
            for (ulong enc = 1; enc <= (ulong)boardCount; enc++)
            foreach (var m in Tactics.Mission.Board(enc, new HashSet<string>()))
            {
                var battle = m.CreateBattle(Tactics.Encounter.StarterSquad());
                while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                if (battle.Winner == null) stalls++;
                // --trace=TITLE prints the whole log of the missions with that title (debugging the AI).
                if (args.TryGetValue("trace", out var trace) && m.Title.Contains(trace))
                    foreach (var e in battle.Events) GD.Print("    " + Tactics.Encounter.Describe(e));
                GD.Print($"board {enc}: {m.Title,-34} pack {m.Pack} {new string('*', m.Stars),-5} reward {m.Reward,2}  -> {(battle.Winner == Tactics.Side.Player ? "complete" : battle.Winner == null ? "STALLED" : "failed")} in {battle.Round} rounds, recruited {battle.Recruited.Count}{(m.Sighting != null ? $", sighting: {m.Sighting}" : "")}{(battle.Target != null ? $", target {battle.Target.Name} ({battle.Target.Attitude}{(battle.Winner != Tactics.Side.Player ? battle.Target.Gone ? ", got away" : battle.Target.Side == Tactics.Side.Player ? ", recruited" : ", still wild" : "")})" : "")}");
            }
            GD.Print($"stalled: {stalls}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("shinies"))
        {
            // Every shiny passive in battle: Blaze carries each one in turn against the same pack, and
            // the PvP squad check turns away a second shiny.
            foreach (var shine in Classes.ClassBook.ShinyPassives)
            {
                var battle = Tactics.Encounter.Create(5, null, "Gilded Gardens");
                var blaze = battle.Units.First(u => u.Name == "Blaze");
                blaze.ShinyPassive = shine;
                int dealt = 0, healed = 0;
                while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                foreach (var e in battle.Events)
                {
                    if (e is Tactics.DamageEvent { Miss: false } d && battle.Events.TakeWhile(x => x != e).OfType<Tactics.StrikeEvent>().LastOrDefault()?.A == blaze) dealt += d.Amount;
                    if (e is Tactics.HealEvent h && h.U == blaze) healed += h.Amount;
                }
                GD.Print($"{shine.Name,-16} in effect: {blaze.Passive.Name,-30} evade {blaze.Mod(Classes.StatKind.Evade),3}  thorns {blaze.Thorns}  dealt {dealt,3}  healed {healed,3}  -> {battle.Winner?.ToString() ?? "STALLED"} in {battle.Round} rounds");
            }
            var pair = Tactics.Encounter.StarterSquad().Take(2).Select(x => x.Item1).ToList();
            foreach (var c in pair) c.Shiny = true;
            GD.Print($"pvp check, two shinies: {string.Join(" ", Tactics.PvpRules.Problems(pair, 1))}");
            pair[1].Shiny = false;
            GD.Print($"pvp check, one shiny: {(Tactics.PvpRules.Problems(pair, 1).Count == 0 ? "allowed" : "refused")}");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("country", out var countryArg))
        {
            // The country loop without a screen: each battle is the next main mission when it's ready,
            // a duel when the next one wants a rival beaten, a coliseum cup (every round), else the
            // easiest nearby contract; recruits fill the squad, and creatures left at home go on the
            // dispatches on offer. Shows when each road opens, whether any battle stalls, and that the
            // save keeps where you are. --trace=N prints battle N's log.
            Ranching.Ranch.SavePath = args.TryGetValue("save", out var countrySave) && countrySave != "" ? $"user://{countrySave}.json" : "user://country_test.json";
            int battles = int.TryParse(countryArg, out var cbn) ? cbn : 30, countryStalls = 0, lastCup = -99;
            // --path=city|undercity|peace: the choices made after main missions, and whose contracts come first.
            string path = args.GetValueOrDefault("path", "peace");
            var ranch = Ranching.Ranch.New();
            int codexAll = Ranching.Lab.Slots.Sum(sl => System.Enum.GetNames(sl.Type).Count(n => n != "None"));
            GD.Print($"codex: {World.StoryBook.Progress(ranch, World.Milestone.CodexParts)} of {codexAll} parts at the start");
            for (int i = 0; i < battles; i++)
            {
                var board = World.Country.Board(ranch);
                foreach (var job in board.Where(x => x.Dispatch != null))
                    if (ranch.Creatures.Where(rec => !ranch.Squad.Contains(rec.Id) && !ranch.Away.ContainsKey(rec.Id))
                        .OrderByDescending(rec => World.DispatchBook.Chance(job.Dispatch, ranch.Get(rec.Id), rec.Training)).FirstOrDefault() is { } idle)
                        GD.Print("    " + ranch.Send(idle.Id, job.Dispatch));
                var fights = board.Where(x => x.Dispatch == null).ToList();
                bool wantsDuel = World.StoryBook.Next(ranch) is { } next && next.Needs.Any(n => n.kind == World.Milestone.RivalWins && World.StoryBook.Progress(ranch, n.kind) < n.count);
                var m = fights.FirstOrDefault(x => x.Story != null) ?? (wantsDuel ? fights.FirstOrDefault(x => x.Rival != null && x.Cup == null) : null)
                    ?? (i - lastCup >= 4 ? fights.FirstOrDefault(x => x.Cup != null) : null)
                    ?? fights.Where(x => x.Rival == null && x.Cup == null).OrderBy(x => Favour(x)).ThenBy(x => World.Country.Days(ranch, x.Location)).ThenBy(x => x.Stars).First();
                int Favour(Tactics.Mission x) => World.Standing.ClientOf(x.Location ?? "") switch
                {
                    World.Side.City => path == "city" || path == "peace" && i % 2 == 0 ? 0 : 1,
                    World.Side.Undercity => path == "undercity" || path == "peace" && i % 2 == 1 ? 0 : 1,
                    _ => 2,
                };
                if (ranch.TravelTo(m.Location) is { } met)
                {
                    if (met.Id != m.Roamer) GD.Print($"    ambushed at {met.At} by a roaming pack of {met.Size}");
                    m = World.Roaming.Battle(met);
                }
                else if (m.Roamer != 0) m = World.Country.Board(ranch).First(x => x.Dispatch == null && x.Roamer == 0 && x.Location == ranch.Location && x.Cup == null);
                Tactics.Battle battle;
                while (true)
                {
                    battle = m.CreateBattle(ranch.SquadCreatures());
                    ranch.LeadUnits(battle);
                    while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                    // A cup goes on to its next round while you win, as on screen.
                    if (m.Cup == null || battle.Winner != Tactics.Side.Player || m.Round == 3) break;
                    ranch.AfterBattle(battle, m);
                    GD.Print($"    {m.Title}: won");
                    m = World.CupBook.Round(ranch, World.CupBook.Get(m.Cup), m.Round + 1);
                }
                if (battle.Winner == null) countryStalls++;
                if (m.Cup != null) lastCup = i;
                if (args.TryGetValue("trace", out var countryTrace) && countryTrace == (i + 1).ToString())
                {
                    foreach (var u in battle.Units)
                        GD.Print($"    {u.Side,-6} {u.Name,-14} {u.Creature.Size,-6} {u.ClassName}/{u.Training?.Secondary ?? "-"}  HP {u.MaxHp} ATK {u.Atk} DEF {u.Def} SPD {u.Spd}  {string.Join(", ", u.Skills.Select(sk => sk.Name))}{(u.Passive != null ? $"; {u.Passive.Name}" : "")}{(u.Reaction != null ? $"; {u.Reaction.Name}" : "")}");
                    foreach (var e in battle.Events) GD.Print("    " + Tactics.Encounter.Describe(e));
                    if (battle.Target is { } target) GD.Print($"    target {target.Name}: {battle.RecruitHint(target)}, {target.Hp} HP, gone {target.Gone}");
                }
                ranch.AfterBattle(battle, m);
                foreach (var line in ranch.Report.Where(l => l.Contains("is back from") || l.Contains("Champion") || l.Contains("Out of the") || l.Contains("Ending") || l.Contains("rumour"))) GD.Print("    " + line);
                if (ranch.Pending != null) GD.Print("    chose: " + ranch.Decide(path == "peace" ? "both" : path));
                foreach (var rec in ranch.Creatures) if (!ranch.Squad.Contains(rec.Id)) ranch.AddToSquad(rec.Id);
                foreach (var id in ranch.Squad) Retrain(ranch.Get(id), ranch.Record(id).Training);
                string result = battle.Winner == Tactics.Side.Player ? "won" : battle.Winner == null ? "STALLED" : "lost";
                GD.Print($"{i + 1,2}. day {ranch.Day,3}  {m.Location,-10} {m.Title,-36} {result,-7} | contracts {ranch.ContractsWon,2}  creatures {ranch.Creatures.Count,2}  rival wins {ranch.Rivals.Values.Sum()}  city {ranch.CityStanding,3} under {ranch.UnderStanding,3} | open: {string.Join(", ", World.Country.Open(ranch).Select(l => l.Id))}");
            }
            ranch.Save();
            var back = Ranching.Ranch.Load();
            bool kept = back.Location == ranch.Location && back.Day == ranch.Day && back.ContractsWon == ranch.ContractsWon && back.Story.SetEquals(ranch.Story)
                && back.Cups.SetEquals(ranch.Cups) && back.Wanderers.Count == ranch.Wanderers.Count
                && back.Wanderers.All(w => ranch.Wanderers.Any(x => x.Id == w.Id && x.At == w.At && x.Seed == w.Seed)) && back.Away.Count == ranch.Away.Count && back.Away.All(kv => ranch.Away.TryGetValue(kv.Key, out var a) && a.Job == kv.Value.Job && a.BackOn == kv.Value.BackOn);
            GD.Print($"country: {(kept ? "kept" : "DIFFERENT")} after save and load (at {back.Location}, day {back.Day}, main missions won: {string.Join(", ", back.Story)}; cups: {string.Join(", ", back.Cups)}; away: {back.Away.Count}; packs roaming: {string.Join(", ", back.Wanderers.Select(w => w.At))}); stalled: {countryStalls}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("endings"))
        {
            // Standing and the endings without a screen: a choice after a main mission sets a flag and
            // shifts standing; with the last road open, each way standing can lean opens exactly one
            // ending (and its path's rumoured creature); an ending won is the ending reached; all saved.
            Ranching.Ranch.SavePath = "user://endings_test.json";
            var ranch = Ranching.Ranch.New();
            ranch.Pending = "main_foundry";
            GD.Print("choice: " + ranch.Decide("city") + $"; flags {string.Join(", ", ranch.Flags)}");
            foreach (var m in World.StoryBook.All.Where(m => m.Ending == null)) ranch.Story.Add(m.Id);
            ranch.ContractsWon = 15;
            foreach (var (city, under) in new[] { (70, 10), (10, 70), (50, 45), (20, 20) })
            {
                ranch.CityStanding = city;
                ranch.UnderStanding = under;
                var ready = World.StoryBook.Ready(ranch).Where(m => m.Ending != null).Select(m => m.Ending).ToList();
                var heard = World.StoryBook.Heard(ranch).Select(x => x.Id).ToList();
                GD.Print($"city {city,3}, undercity {under,3} (leans {World.Standing.Lean(ranch),-9}): endings open: {(ready.Count == 0 ? "none" : string.Join(", ", ready))}; rumours: {(heard.Count == 0 ? "none" : string.Join(", ", heard))}");
            }
            ranch.CityStanding = 50; ranch.UnderStanding = 45;
            var peace = World.StoryBook.ToMission(World.StoryBook.Get("end_peace"));
            var rumour = World.StoryBook.RumourMission(World.StoryBook.Rumours.First(x => x.Id == "rumour_peace"));
            var rb = rumour.CreateBattle(ranch.SquadCreatures());
            GD.Print($"rumour battle: target {rb.Target?.Name} ({rb.Target?.Creature.Species.Strain}, {rb.Target?.Creature.Size}, traits {string.Join(", ", rb.Target?.Creature.Traits ?? new List<string>())})");
            ranch.TravelTo("hollow");
            var battle = peace.CreateBattle(ranch.SquadCreatures());
            ranch.LeadUnits(battle);
            foreach (var u in battle.Units.Where(u => u.Side == Tactics.Side.Wild)) u.Hp = 1; // a sure win, to see the ending land
            while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
            ranch.AfterBattle(battle, peace);
            foreach (var line in ranch.Report.Take(3)) GD.Print("  " + line);
            GD.Print($"after: ending {ranch.Ending}; endings still open: {World.StoryBook.Ready(ranch).Count(m => m.Ending != null)}");
            var back = Ranching.Ranch.Load();
            bool kept = back.CityStanding == ranch.CityStanding && back.UnderStanding == ranch.UnderStanding && back.Flags.SetEquals(ranch.Flags) && back.Ending == ranch.Ending && back.Pending == ranch.Pending;
            GD.Print($"story: {(kept ? "kept" : "DIFFERENT")} after save and load (city {back.CityStanding}, undercity {back.UnderStanding}, flags {string.Join(", ", back.Flags)}, ending {back.Ending})");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("steampunk"))
        {
            // The steampunk work: clockwork creatures of every body plan, front and back, then one of
            // each piece through its idle (cogs turning, pistons pumping, soot rising, the gauge's needle).
            var picks = new List<Creature>();
            var plans = new Dictionary<BodyPlan, int>();
            for (ulong k = 0; picks.Count < 8 && k < 20000; k++)
            {
                var steam = Species.Generate(SeededRng.Derive(0x57EA, k));
                if (steam.Strain != Strain.Clockwork || (steam.Grafts & (Graft.Gears | Graft.Pistons | Graft.Smokestack | Graft.Pipework)) == 0) continue;
                if (plans.GetValueOrDefault(steam.Plan) >= 2) continue;
                plans[steam.Plan] = plans.GetValueOrDefault(steam.Plan) + 1;
                picks.Add(Creature.Specimen(steam, 0));
            }
            string sheetPath = ProjectSettings.GlobalizePath("res://.shots/steampunk.png");
            CreatureSheet.ComposeViews(picks, false, 4, 4).SavePng(sheetPath);
            GD.Print($"wrote {picks.Count} clockwork creatures: " + string.Join("; ", picks.Select(c => $"{c.Species.Name} ({c.Species.Plan}, {c.Species.Grafts})")));
            // The four starters, each fitted with one piece of steamwork as an augment would.
            var fitted = new List<Creature>();
            var pieces = new[] { Graft.Gears, Graft.Pistons, Graft.Smokestack, Graft.Pipework };
            foreach (var (name, i) in new[] { "Blaze", "Sprocket", "Tank", "Hoot" }.Select((n, i) => (n, i)))
            {
                var starter = Creature.Specimen(DrawPetsCast.All.First(x => x.Name == name), 0);
                starter.Species = starter.Species.Clone();
                starter.Species.Grafts |= pieces[i];
                fitted.Add(starter);
            }
            CreatureSheet.ComposeViews(fitted, false, 4, 4).SavePng(ProjectSettings.GlobalizePath("res://.shots/steampunk_fitted.png"));
            // One creature per piece, close up: a four-legged one of fair size where there is one, four frames of its idle in a row.
            var rows = new List<Image>();
            foreach (var piece in new[] { Graft.Gears, Graft.Pistons, Graft.Smokestack, Graft.Pipework })
            {
                Creature who = null;
                for (ulong k = 0; who == null && k < 40000; k++)
                {
                    var steam = Species.Generate(SeededRng.Derive(0x57EB, k));
                    if (steam.Strain == Strain.Clockwork && steam.Grafts.HasFlag(piece) && steam.Plan == BodyPlan.Quadruped && steam.BaseSize >= (args["steampunk"] == "large" ? SizeClass.Large : SizeClass.Medium))
                        who = Creature.Specimen(steam, 0);
                }
                who ??= picks.FirstOrDefault(c => c.Species.Grafts.HasFlag(piece));
                if (who == null) continue;
                // Without its steampunk work first, to see what the work adds; then the idle with it.
                var bare = Creature.Specimen(who.Species.Clone(), 0);
                bare.Species.Grafts &= ~(Graft.Gears | Graft.Pistons | Graft.Smokestack | Graft.Pipework);
                var frames = new[] { CreatureSculptor.Render(bare, false, View.Front, 0).img }
                    .Concat(Enumerable.Range(0, 3).Select(fr => CreatureSculptor.Render(who, false, View.Front, fr / 3f).img)).ToList();
                int fw = frames.Max(i => i.GetWidth()), fh = frames.Max(i => i.GetHeight());
                var row = Image.CreateEmpty(fw * 4, fh, false, Image.Format.Rgba8);
                row.Fill(Color.FromHtml("#16121f"));
                for (int fr = 0; fr < 4; fr++) row.BlendRect(frames[fr], new Rect2I(Vector2I.Zero, frames[fr].GetSize()), new Vector2I(fr * fw + (fw - frames[fr].GetWidth()) / 2, fh - frames[fr].GetHeight()));
                row.Resize(row.GetWidth() * 5, row.GetHeight() * 5, Image.Interpolation.Nearest);
                GD.Print($"  {piece}: {who.Species.Name} ({who.Species.Plan}, {who.Size}, {who.Species.Grafts})");
                rows.Add(row);
            }
            // The showcase's idle as frames (.shots/steamidle/f00.png...), for a GIF: one creature per piece, side by side.
            var shown = new HashSet<ulong>();
            var showcase = new[] { Graft.Smokestack, Graft.Pistons, Graft.Pipework, Graft.Gears }.Select(piece =>
            {
                for (ulong k = 0; k < 40000; k++)
                {
                    var steam = Species.Generate(SeededRng.Derive(0x57EC, k));
                    if (steam.Strain == Strain.Clockwork && steam.Grafts.HasFlag(piece) && steam.Plan == BodyPlan.Quadruped && steam.BaseSize >= SizeClass.Large
                        && steam.Parts.Wings == Wings.None && shown.Add(steam.Seed)) return Creature.Specimen(steam, 0);
                }
                return null;
            }).Where(c => c != null).ToList();
            string frameDir = ProjectSettings.GlobalizePath("res://.shots/steamidle");
            System.IO.Directory.CreateDirectory(frameDir);
            for (int fr = 0; fr < 8; fr++)
            {
                var shots = showcase.Select(c => CreatureSculptor.Render(c, false, View.Front, fr / 8f)).ToList();
                int cw = shots.Max(x => x.img.GetWidth()) + 8, ch = shots.Max(x => x.anchor.Y) + 6;
                var frame = Image.CreateEmpty(cw * shots.Count, ch, false, Image.Format.Rgba8);
                frame.Fill(Color.FromHtml("#16121f"));
                for (int i = 0; i < shots.Count; i++)
                    frame.BlendRect(shots[i].img, new Rect2I(Vector2I.Zero, shots[i].img.GetSize()), new Vector2I(i * cw + (cw - shots[i].img.GetWidth()) / 2, ch - 4 - shots[i].anchor.Y));
                frame.Resize(frame.GetWidth() * 4, frame.GetHeight() * 4, Image.Interpolation.Nearest);
                frame.SavePng($"{frameDir}/f{fr:00}.png");
            }
            GD.Print("showcase: " + string.Join(", ", showcase.Select(c => $"{c.Species.Name} ({c.Species.Grafts})")));
            int w = rows.Max(r => r.GetWidth()), h = rows.Sum(r => r.GetHeight());
            var strip = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            strip.Fill(Color.FromHtml("#16121f"));
            int y = 0;
            foreach (var r in rows) { strip.BlitRect(r, new Rect2I(Vector2I.Zero, r.GetSize()), new Vector2I(0, y)); y += r.GetHeight(); }
            strip.SavePng(ProjectSettings.GlobalizePath("res://.shots/steampunk_idle.png"));
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("traits"))
        {
            // Traits and augments without a screen: how often wild creatures are born with traits, how
            // often a parent's trait passes down, that an augment fits (graft, stats, attack) but never
            // passes down, and that both survive a save (old saves get their traits rolled).
            var counts = new int[3];
            var seen = new Dictionary<string, int>();
            for (ulong k = 0; k < 600; k++)
            {
                var wild = Creature.Specimen(Species.Generate(SeededRng.Derive(0x7EA7, k)), 0);
                counts[wild.Traits.Count]++;
                foreach (var t in wild.Traits) seen[t] = seen.GetValueOrDefault(t) + 1;
            }
            GD.Print($"wild: {counts[0] / 6f:0}% none, {counts[1] / 6f:0}% one, {counts[2] / 6f:0}% two; every trait seen: {seen.Count}/{Classes.TraitBook.Traits.Length} (rarest {seen.Values.Min()}, commonest {seen.Values.Max()})");
            int offered = 0, passed = 0, bare = 0, fresh = 0;
            for (ulong k = 0; k < 400; k++)
            {
                var pa = Creature.Specimen(Species.Generate(SeededRng.Derive(0xB4ED, k * 2)), 0);
                var pb = Creature.Specimen(Species.Generate(SeededRng.Derive(0xB4ED, k * 2 + 1)), 0);
                if (pa.Traits.Count + pb.Traits.Count == 0) continue;
                var child = Breeding.Breed(pa, pb, SeededRng.Derive(0xC417, k)).Child;
                var parentTraits = pa.Traits.Concat(pb.Traits).Distinct().ToList();
                offered += parentTraits.Count;
                passed += child.Traits.Count(parentTraits.Contains);
                if (child.Traits.All(t => !parentTraits.Contains(t))) { bare++; if (child.Traits.Count > 0) fresh++; }
            }
            GD.Print($"breeding: {passed}/{offered} parent traits passed down ({passed * 100f / offered:0}%, about half, up to two a child); {fresh} of {bare} children with none passed showed a new one");

            Ranching.Ranch.SavePath = "user://traits_test.json";
            var ranch = Ranching.Ranch.New();
            int tank = ranch.Creatures.First(r => r.Species.Name == "Tank").Id, sprocket = ranch.Creatures.First(r => r.Species.Name == "Sprocket").Id;
            var plain = Tactics.Unit.From(ranch.Get(tank), Tactics.Side.Player, ranch.Record(tank).Training);
            ranch.Items["piston_fist"] = 1;
            ranch.Items["acid_canister"] = 1;
            GD.Print("  " + ranch.Install(tank, "piston_fist"));
            GD.Print("  " + ranch.Install(tank, "acid_canister"));
            var fitted = Tactics.Unit.From(ranch.Get(tank), Tactics.Side.Player, ranch.Record(tank).Training);
            GD.Print($"augments: Tank ATK {plain.Atk} -> {fitted.Atk}, grafts {ranch.Get(tank).Species.Grafts}, skills now {string.Join(", ", fitted.Skills.Select(sk => sk.Name))}");
            foreach (var r in ranch.Creatures) r.Rested = true;
            var (pup, result) = ranch.Breed(tank, sprocket);
            var pupC = ranch.Get(pup);
            GD.Print($"  bred Tank x Sprocket -> {pupC.Name}: grafts {pupC.Species.Grafts}, augments {pupC.Augments.Count}, traits {string.Join(", ", pupC.Traits)} ({string.Join("; ", result.FromA.Concat(result.FromB).Where(n => n.StartsWith("trait")))})");
            ranch.Save();
            var back = Ranching.Ranch.Load();
            bool kept = ranch.Creatures.All(r => back.Record(r.Id).Traits.SequenceEqual(r.Traits) && back.Record(r.Id).Augments.SequenceEqual(r.Augments));
            back.Record(tank).Traits = null;
            back.Tidy();
            GD.Print($"traits: {(kept ? "kept" : "DIFFERENT")} after save and load; an old save's creature gets {string.Join(", ", back.Record(tank).Traits)} (it was born with {string.Join(", ", ranch.Record(tank).Traits)})");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("tiers"))
        {
            // Creature tiers without a screen: what each piece of lab work makes a creature, what
            // breeding makes, the thoroughbred bonus, strays in battle, rivals' squads, that tiers
            // survive a save, and that an old save's infused creatures are found again.
            Ranching.Ranch.SavePath = "user://tiers_test.json";
            var ranch = Ranching.Ranch.New();
            ranch.Chem = 2000;
            Classes.Tier TierOf(int id) => Classes.TierBook.Of(ranch.Get(id));
            Creature Caught(Strain strain, ulong from)
            {
                for (ulong k = from; ; k++)
                    if (Species.Generate(SeededRng.Derive(0x71E5, k)) is { } sp && sp.Strain == strain) return Creature.Specimen(sp, 0);
            }
            int n1 = ranch.Add(Caught(Strain.Natural, 0)), n2 = ranch.Add(Caught(Strain.Natural, 50)), n3 = ranch.Add(Caught(Strain.Natural, 100));
            int n4 = ranch.Add(Caught(Strain.Natural, 150)), chem = ranch.Add(Caught(Strain.Chemtech, 0));
            foreach (var r in ranch.Creatures) Ranching.Lab.Learn(ranch, ranch.Get(r.Id));
            GD.Print($"caught: {string.Join(", ", ranch.Creatures.Select(r => $"{r.Name} ({r.Species.Strain.Label()}) {TierOf(r.Id)}"))}");

            var plain = Tactics.Unit.From(ranch.Get(n1), Tactics.Side.Player);
            var wild = Tactics.Unit.From(ranch.Get(n1), Tactics.Side.Wild);
            GD.Print($"thoroughbred bonus: {ranch.Record(n1).Name} caught ATK {plain.Atk} DEF {plain.Def} HP {plain.MaxHp} ({plain.Tier}); wild ATK {wild.Atk} DEF {wild.Def} HP {wild.MaxHp} ({wild.Tier})");
            foreach (var r in ranch.Creatures)
                GD.Print($"  {r.Name} ({Species.FormOf(r.Species)}, {r.Species.Element}): {Classes.TierBook.ThoroughbredOf(r.Species).Text}");
            var forms = Enum.GetValues<Form>().Where(f => f != Form.Auto).ToList();
            var bare = forms.Where(f => Classes.TierBook.ThoroughbredOf(new Species { Form = f, Plan = Enum.GetValues<BodyPlan>().First(pl => Species.AllFormsFor(pl).Contains(f)) }).Name == "Thoroughbred: Hardy").ToList();
            GD.Print($"forms with a bonus of their own: {forms.Count - bare.Count}/{forms.Count}{(bare.Count > 0 ? $"; missing: {string.Join(", ", bare)}" : "")}");

            GD.Print("  " + Ranching.Lab.Infuse(ranch, n1, Strain.Chemtech));
            var rec1 = ranch.Record(n1);
            foreach (var slot in Ranching.Lab.SlotsFor(rec1.Species.Plan))
            {
                int now = Ranching.Lab.Get(rec1.Parts, slot.Name);
                var other = Ranching.Lab.Options(ranch, slot.Name).Where(v => v != now).ToList();
                if (other.Count == 0) continue;
                GD.Print("  " + Ranching.Lab.GraftPart(ranch, n1, slot.Name, other[0]));
                break;
            }
            var other1 = ranch.Forms.First(f => f != Species.FormOf(rec1.Species));
            GD.Print("  " + Ranching.Lab.Splice(ranch, n1, other1));
            GD.Print($"  {rec1.Name}: {TierOf(n1)}: {Classes.TierBook.Describe(ranch.Get(n1), TierOf(n1))}");
            foreach (var aug in new[] { "piston_fist", "rivet_gun", "acid_canister" }) ranch.Items[aug] = 1;
            GD.Print("  " + ranch.Install(n1, "piston_fist"));
            GD.Print("  " + ranch.Install(n2, "rivet_gun"));
            var tunedBefore = Tactics.Unit.From(ranch.Get(n3), Tactics.Side.Player);
            GD.Print("  " + Ranching.Lab.Brew(ranch, n3, "rage_serum"));
            GD.Print("  " + Ranching.Lab.Brew(ranch, n3, "quickblood"));
            GD.Print("  " + Ranching.Lab.Brew(ranch, n3, "bulk_tonic"));
            var tunedAfter = Tactics.Unit.From(ranch.Get(n3), Tactics.Side.Player);
            GD.Print($"  infusions: {ranch.Record(n3).Name} ATK {tunedBefore.Atk} -> {tunedAfter.Atk}, SPD {tunedBefore.Spd} -> {tunedAfter.Spd}, HP {tunedBefore.MaxHp} -> {tunedAfter.MaxHp}, DEF {tunedBefore.Def} -> {tunedAfter.Def} (its {tunedBefore.TierPassive?.Name} gone: {tunedAfter.TierPassive?.Name ?? "none"})");
            var after = Tactics.Unit.From(ranch.Get(n2), Tactics.Side.Player);
            var abom = Tactics.Unit.From(ranch.Get(n1), Tactics.Side.Player);
            GD.Print($"lab: {ranch.Record(n1).Name} {TierOf(n1)} ({abom.TierPassive?.Name}; skills {string.Join(", ", abom.Skills.Select(sk => sk.Name))}), {ranch.Record(n2).Name} {TierOf(n2)} (passive {after.TierPassive?.Name ?? "none"}), {ranch.Record(n3).Name} {TierOf(n3)}");

            int city = ranch.CityStanding, under = ranch.UnderStanding;
            foreach (var r in ranch.Creatures) r.Rested = true;
            var (true1, res1) = ranch.Breed(n3, n4);
            GD.Print($"bred natural x natural -> {ranch.Record(true1).Name}: {TierOf(true1)} ({string.Join("; ", res1.Notes)}); standing city {city} -> {ranch.CityStanding}, undercity {under} -> {ranch.UnderStanding}");
            city = ranch.CityStanding; under = ranch.UnderStanding;
            var (cross, res2) = ranch.Breed(n2, chem);
            GD.Print($"bred natural x chemborn -> {ranch.Record(cross).Name}: {TierOf(cross)}, {Classes.TierBook.PassiveOf(ranch.Get(cross), TierOf(cross))?.Name} ({string.Join("; ", res2.Notes)}); standing city {city} -> {ranch.CityStanding}, undercity {under} -> {ranch.UnderStanding}");
            GD.Print("  " + ranch.Install(n4, "acid_canister") + $" -> {TierOf(n4)}");
            var (same, res3) = ranch.Breed(n1, chem);
            GD.Print($"bred infused chemborn x chemborn -> {ranch.Record(same).Name}: {TierOf(same)} ({string.Join("; ", res3.Notes)})");
            foreach (var r in ranch.Creatures) r.Rested = true;
            var (grand, res4) = ranch.Breed(cross, true1);
            GD.Print($"bred chimera x thoroughbred -> {ranch.Record(grand).Name}: {TierOf(grand)}, {Classes.TierBook.PassiveOf(ranch.Get(grand), TierOf(grand))?.Name} ({string.Join("; ", res4.Notes)})");

            // The costs: a new infusion steeps (out of the squad until a battle is over), rigs cost upkeep, rigged creatures learn slower.
            int sq = ranch.Squad[0];
            ranch.Chem = 500;
            GD.Print("  " + Ranching.Lab.Brew(ranch, sq, "iron_tincture"));
            bool steeping = ranch.Record(sq).Steeping, rejoined = ranch.AddToSquad(sq);
            ranch.Items["gear_wings"] = 1;
            ranch.Items["grapple_claws"] = 1;
            int flier = ranch.Squad[0];
            GD.Print("  " + ranch.Install(flier, "gear_wings"));
            int climber = ranch.Creatures.First(r => !ranch.Squad.Contains(r.Id) && r.Id != sq && r.Augments.Count == 0).Id;
            GD.Print("  " + ranch.Install(climber, "grapple_claws"));
            GD.Print($"rigs: {ranch.Record(flier).Name} now moves {ranch.Get(flier).MoveType} (wings {ranch.Get(flier).Parts.Wings}), {ranch.Record(climber).Name} {ranch.Get(climber).MoveType}");
            int chemBefore = ranch.Chem;
            var costBattle = new Tactics.Mission { Seed = 11, MapName = "Lantern Row", Goal = Tactics.Objective.Rout, Pack = 3 }.CreateBattle(ranch.SquadCreatures());
            ranch.LeadUnits(costBattle);
            while (costBattle.Winner == null && costBattle.Round <= 30) { var u = costBattle.NextTurn(); if (u == null) break; costBattle.AiTurn(u); }
            ranch.AfterBattle(costBattle);
            GD.Print($"costs: steeping {steeping}, could rejoin the squad {rejoined}, steeping after the battle {ranch.Record(sq).Steeping}; {string.Join(" ", ranch.Report.Where(l => l.StartsWith("Rig upkeep")))}; {ranch.Record(flier).Name} learns at {Classes.TierBook.GrowthRate(ranch.Get(flier)) * 100:0}%");

            // An abomination's episodes come between missions: about 1 in 4 battles it fights.
            ranch.Record(n1).Steeping = ranch.Record(n1).Recovering = false;
            foreach (var id in ranch.Squad.ToList()) ranch.RemoveFromSquad(id);
            int fought = 0, episodes = 0;
            var kinds = new HashSet<string>();
            for (int k = 0; k < 24; k++)
            {
                if (!ranch.Squad.Contains(n1) && !ranch.AddToSquad(n1)) { ranch.AfterBattle(new Tactics.Mission { Seed = 900 + (ulong)k, MapName = "Lantern Row", Goal = Tactics.Objective.Rout, Pack = 2 }.CreateBattle(ranch.SquadCreatures())); continue; }
                foreach (var r in ranch.Creatures.Where(r => r.Id != n1 && ranch.Squad.Count < 3)) ranch.AddToSquad(r.Id);
                var eb = new Tactics.Mission { Seed = 900 + (ulong)k, MapName = "Lantern Row", Goal = Tactics.Objective.Rout, Pack = 2 }.CreateBattle(ranch.SquadCreatures());
                ranch.LeadUnits(eb);
                while (eb.Winner == null && eb.Round <= 30) { var u = eb.NextTurn(); if (u == null) break; eb.AiTurn(u); }
                ranch.AfterBattle(eb);
                fought++;
                foreach (var line in ranch.Report.Where(l => l.Contains("has an episode")))
                {
                    episodes++;
                    kinds.Add(line.Contains("recover") ? "recovers" : line.Contains("body shifts") ? "body shifts" : line.Contains("sours") ? "infusion sours" : "rigs seize");
                }
            }
            GD.Print($"episodes: {ranch.Record(n1).Name} had {episodes} in {fought} battles (about 1 in 4): {string.Join(", ", kinds)}; it's {TierOf(n1)} still");

            ranch.Save();
            var back = Ranching.Ranch.Load();
            bool kept = ranch.Creatures.All(r => Classes.TierBook.Of(back.Get(r.Id)) == TierOf(r.Id));
            back.Flags.Remove(Ranching.Ranch.TiersFlag);
            back.Record(n1).Species.Infused = false;
            back.Record(n1).Species.Chimera = false;
            back.Record(n1).Species.Spliced = null;
            back.Record(n1).Augments.Clear();
            back.Record(n1).BirthParts = null;
            back.Refresh(n1);
            var before = Classes.TierBook.Of(back.Get(n1));
            back.Tidy();
            GD.Print($"save: tiers {(kept ? "kept" : "DIFFERENT")} after save and load; an old save's infused {back.Record(n1).Name} reads {before}, then {Classes.TierBook.Of(back.Get(n1))} once tidied; the wild-born chemborn stays {Classes.TierBook.Of(back.Get(chem))}");

            foreach (var (id, wins) in new[] { ("chem_witch", 0), ("brass_baron", 2) })
            {
                var squad = Party.RivalBook.Squad(Party.RivalBook.Get(id), 4, wins, 77, 3);
                GD.Print($"rival {id} (wins {wins}): {string.Join(", ", squad.Select(p => $"{p.creature.Name} {Tactics.Unit.From(p.creature, Tactics.Side.Wild, p.training).Tier}"))}");
            }
            var battle = new Tactics.Mission { Seed = 5, MapName = "Lantern Row", Goal = Tactics.Objective.Rout, Pack = 3 }.CreateBattle(ranch.SquadCreatures());
            GD.Print($"battle: yours {string.Join(", ", battle.Units.Where(u => u.Side == Tactics.Side.Player).Select(u => $"{u.Name} {u.Tier}"))}; the pack {string.Join(", ", battle.Units.Where(u => u.Side == Tactics.Side.Wild).Select(u => $"{u.Name} {u.Tier}"))}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("shoptest"))
        {
            // The shops without a screen: buy where you stand (and not elsewhere), wear gear, use items
            // in a battle (a snare net on a worn-down wild creature, a tonic), then the bag after it.
            Ranching.Ranch.SavePath = "user://shop_test.json";
            var ranch = Ranching.Ranch.New();
            ranch.Chem = 400;
            ranch.Story.Add("main_foundry");
            ranch.TravelTo("gardens");
            foreach (var id in new[] { "bell_collar", "tonic", "tonic", "soothing_chime", World.ShopBook.Berry, "snare_net" })
                GD.Print("  " + ranch.Buy(World.ShopBook.Stock.First(w => w.Id == id)));
            ranch.TravelTo("lantern");
            for (int k = 0; k < 6; k++) GD.Print("  " + ranch.Buy(World.ShopBook.Stock.First(w => w.Id == "snare_net")));
            GD.Print($"  at {ranch.Location}: {string.Join(", ", World.ShopBook.At(ranch, ranch.Location).Select(w => $"{World.ShopBook.Name(w.Id)} {w.Price}"))}");
            int blaze = ranch.Creatures.First(r => r.Species.Name == "Blaze").Id;
            var before = Tactics.Unit.From(ranch.Get(blaze), Tactics.Side.Player, ranch.Record(blaze).Training);
            GD.Print("  " + ranch.Equip(blaze, "bell_collar"));
            var after = Tactics.Unit.From(ranch.Get(blaze), Tactics.Side.Player, ranch.Record(blaze).Training);
            GD.Print($"gear: Blaze DEF {before.Def} -> {after.Def}, evade {before.Mod(Classes.StatKind.Evade)} -> {after.Mod(Classes.StatKind.Evade)}");

            var mission = new Tactics.Mission { Seed = 9, MapName = "Lantern Row", Goal = Tactics.Objective.Rout, Pack = 3 };
            var battle = mission.CreateBattle(ranch.SquadCreatures());
            ranch.LeadUnits(battle);
            GD.Print($"bag in battle: {string.Join(", ", battle.Bag.Select(kv => $"{kv.Key} x{kv.Value}"))}; berries {battle.Berries}");
            var thrower = battle.Units.First(u => u.Side == Tactics.Side.Player);
            var net = Classes.ClassBook.Skill("snare_net");
            int caught = 0, throws = 0;
            foreach (var wild in battle.Units.Where(u => u.Side == Tactics.Side.Wild).ToList())
            {
                wild.Hp = 1; // worn right down
                var spot = Enumerable.Range(0, battle.Map.W * battle.Map.H).Select(i => new Godot.Vector2I(i % battle.Map.W, i / battle.Map.W))
                    .FirstOrDefault(p => battle.CanStand(thrower, p) && Tactics.Battle.Dist(thrower, p, wild) <= 3, new Godot.Vector2I(-1, -1));
                if (spot.X < 0 || battle.Bag.GetValueOrDefault("snare_net") == 0) continue;
                thrower.Pos = spot;
                // Throw until it's caught or the nets run out.
                while (wild.Side == Tactics.Side.Wild && !wild.Gone && battle.Bag.GetValueOrDefault("snare_net") > 0)
                {
                    throws++;
                    int mark = battle.Events.Count;
                    battle.Order(Tactics.CommandKind.Skill, thrower, wild.Pos, skill: net);
                    GD.Print("    " + string.Join(" / ", battle.Events.Skip(mark).Select(Tactics.Encounter.Describe).Where(d => !d.StartsWith("--"))));
                }
                if (wild.Side == Tactics.Side.Player) caught++;
            }
            GD.Print($"nets: {throws} thrown at worn-down wild creatures, {caught} caught (60% a throw); recruited {battle.Recruited.Count}");
            thrower.Hp = thrower.MaxHp / 2;
            int hp = thrower.Hp;
            battle.Order(Tactics.CommandKind.Skill, thrower, thrower.Pos, skill: Classes.ClassBook.Skill("tonic"));
            GD.Print($"tonic: {hp} -> {thrower.Hp} HP; used {string.Join(", ", battle.Used.Select(kv => $"{kv.Key} x{kv.Value}"))}");
            while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
            ranch.AfterBattle(battle, mission);
            GD.Print($"bag after: {string.Join(", ", ranch.Items.Select(kv => $"{kv.Key} x{kv.Value}"))}; chem {ranch.Chem}");
            var back = Ranching.Ranch.Load();
            bool kept = back.Items.Count == ranch.Items.Count && back.Items.All(kv => ranch.Items.GetValueOrDefault(kv.Key) == kv.Value) && back.Record(blaze).Training.Gear == "bell_collar";
            GD.Print($"shop: {(kept ? "kept" : "DIFFERENT")} after save and load (Blaze wears {back.Record(blaze).Training.Gear})");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("rivals", out var rivalTries))
        {
            // Every rival tamer at their first three turns on the board (beaten each time before), against
            // the starters trained about as far as a player's would be by then and led by your tamers.
            // Prints the first squad of each (what they field and train), then plays N duels (--rivals=N,
            // different squads and maps) and reports wins.
            int tries = int.TryParse(rivalTries, out var rt) ? rt : 6, duelStalls = 0;
            var tally = new int[3];
            foreach (var rival in Party.RivalBook.All)
            foreach (var (enc, wins) in Enumerable.Range(1, 60).Select(e => (ulong)e).Where(e => Party.RivalBook.Challenger(e) == rival).Take(3).Select((e, i) => (e, i)))
            for (int losses = 0; losses <= 2; losses++)
            {
                int youWin = 0, roundSum = 0;
                string runs = "";
                for (int k = 0; k < tries; k++)
                {
                    var m = Tactics.Mission.Duel(rival, enc, wins, SeededRng.Derive(enc, 0xD0E1 + (ulong)k), losses);
                    // Your side: the starters and two more of the cast, as many as you could field, trained the
                    // way a tamer would to about this encounter, and led by the tamers who've joined.
                    var leaders = Party.TamerBook.Joined(enc).Take(Party.TamerBook.MaxFielded).ToList();
                    var squad = new[] { "Blaze", "Sprocket", "Tank", "Hoot", "Boulder", "Zephyr" }.Take(Party.RivalBook.SquadSize(enc))
                        .Select(name => Creature.Specimen(Creatures.DrawPetsCast.All.First(sp => sp.Name == name), 0))
                        .Select(c => (c, Party.RivalBook.Train(c, new[] { Ranching.Ranch.DefaultClass(c) }, 15 + 18 * ((int)enc - 1)))).ToList();
                    var battle = m.CreateBattle(squad.Select(x => (x.c, x.Item2)));
                    for (int i = 0; i < squad.Count; i++) battle.Lead(squad[i].c, leaders[i / Party.TamerBook.CreaturesEach]);
                    if (k == 0 && losses == 0)
                    {
                        GD.Print($"{rival.Name} at encounter {enc}, beaten {wins}x:");
                        foreach (var u in battle.Units)
                            GD.Print($"  {(u.Side == Tactics.Side.Wild ? "rival" : "yours")} {u.Name,-14} {u.Creature.Species.Strain,-9} {Creatures.Species.FormOf(u.Creature.Species),-10} {u.Creature.Size,-6} {u.ClassName}/{u.Training?.Secondary ?? "-"}  HP {u.MaxHp} ATK {u.Atk} DEF {u.Def} SPD {u.Spd}{(u.Boost != null ? " (boosted)" : "")}  {string.Join(", ", u.Skills.Select(s => s.Name))}{(u.Passive != null ? $"; {u.Passive.Name}" : "")}{(u.Reaction != null ? $"; {u.Reaction.Name}" : "")}");
                    }
                    while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                    if (battle.Winner == null) duelStalls++;
                    if (battle.Winner == Tactics.Side.Player) youWin++;
                    roundSum += battle.Round;
                    runs += battle.Winner == Tactics.Side.Player ? "W" : battle.Winner == null ? "S" : "l";
                    if (args.TryGetValue("trace", out var trace) && trace == $"{rival.Id}{wins}{k}" && losses == 0)
                    {
                        GD.Print($"    on {m.MapName}; still standing: {string.Join(", ", battle.Alive.Select(u => $"{u.Name} {u.Creature.Size} {u.MoveType} at {u.Pos}"))}");
                        foreach (var e in battle.Events) GD.Print("    " + Tactics.Encounter.Describe(e));
                    }
                }
                tally[losses] += youWin;
                GD.Print($"  -> {(losses == 0 ? "straight" : $"after {losses} lost")}: you win {youWin}/{tries} ({runs}: W you, l the rival, S stalled), {roundSum / (float)tries:0.0} rounds on average");
            }
            int all = tries * Party.RivalBook.All.Length * 3;
            GD.Print($"overall: you win {tally[0]}/{all} straight, {tally[1]}/{all} after a loss, {tally[2]}/{all} after two; stalled: {duelStalls}");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("capturetest", out var captureBoards))
        {
            // Captures by temper: on boards 1..N, each board's first mission made a capture, played once
            // with the target set to each temper. Your side's AI should win most of them.
            int captureCount = int.TryParse(captureBoards, out var cb) ? cb : 8;
            var tempers = new[] { Attitude.Proud, Attitude.Skittish, Attitude.Curious, Attitude.Aggressive, Attitude.Loyal };
            var won = new Dictionary<Attitude, int>();
            var rounds = new Dictionary<Attitude, int>();
            int captureStalls = 0;
            for (ulong enc = 1; enc <= (ulong)captureCount; enc++)
            {
                var m = Tactics.Mission.Board(enc, new HashSet<string>()).First(x => x.Pack >= 2);
                m.Goal = Tactics.Objective.Capture;
                foreach (var temper in tempers)
                {
                    var battle = m.CreateBattle(Tactics.Encounter.StarterSquad());
                    battle.Target.Creature.Attitude = temper;
                    while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                    if (battle.Winner == null) captureStalls++;
                    if (battle.Winner == Tactics.Side.Player) won[temper] = won.GetValueOrDefault(temper) + 1;
                    rounds[temper] = rounds.GetValueOrDefault(temper) + battle.Round;
                    string fate = battle.Winner == Tactics.Side.Player ? "recruited" : battle.Winner == null ? "STALLED" : battle.Target.Gone ? "got away" : "you lost";
                    GD.Print($"board {enc} {m.MapName,-18} pack {m.Pack} {temper,-10} -> {fate} in {battle.Round} rounds, recruited {battle.Recruited.Count}");
                    if (args.TryGetValue("trace", out var trace) && trace == $"{enc}{temper}")
                        foreach (var e in battle.Events) GD.Print("    " + Tactics.Encounter.Describe(e));
                }
            }
            foreach (var temper in tempers) GD.Print($"{temper,-10} {won.GetValueOrDefault(temper)}/{captureCount} captured, {rounds[temper] / (float)captureCount:0.0} rounds on average");
            GD.Print($"stalled: {captureStalls}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("signatures"))
        {
            // The signature classes' mechanics, each proven directly: a summon hits only foes, an
            // illusion every foe on the field, a death mark fells its target after three of its turns,
            // sleep skips turns until a hit, a finishing blow fells a worn-down foe about half the time,
            // and a spliced-in ability opens its form's class.
            (Tactics.Battle b, Tactics.Unit user, Tactics.Unit foe, Tactics.Unit friend) Setup(ulong seed = 3)
            {
                var b = Tactics.Encounter.Create(seed);
                var user = b.Units.First(u => u.Side == Tactics.Side.Player && u.Size == 1);
                var foe = b.Units.First(u => u.Side == Tactics.Side.Wild && u.Size == 1);
                var friend = b.Units.First(u => u.Side == Tactics.Side.Player && u != user && u.Size == 1);
                var dirs = new[] { Godot.Vector2I.Left, Godot.Vector2I.Right, Godot.Vector2I.Up, Godot.Vector2I.Down };
                for (int y = 0; y < b.Map.H; y++)
                for (int x = 0; x < b.Map.W; x++)
                {
                    var p = new Godot.Vector2I(x, y);
                    if (!b.CanStand(user, p)) continue;
                    var free = dirs.Select(d => p + d).Where(q => b.Map.InBounds(q) && b.CanStand(foe, q) && b.Map.HeightAt(q) == b.Map.HeightAt(p)).ToList();
                    if (free.Count < 2) continue;
                    user.Pos = p; foe.Pos = free[0]; friend.Pos = free[1];
                    y = b.Map.H; break;
                }
                return (b, user, foe, friend);
            }
            int Hurt(Tactics.Battle b, int from, Tactics.Unit u) => b.Events.Skip(from).OfType<Tactics.DamageEvent>().Count(e => e.U == u && !e.Miss && e.Amount > 0);

            {
                var (b, user, foe, friend) = Setup();
                var sk = Classes.ClassBook.Skill("cinder_drake");
                int at = b.Events.Count;
                b.UseSkill(user, sk, foe.Pos);
                var foes = b.Units.Where(u => u.Side == Tactics.Side.Wild && u.Tiles.Any(b.SkillArea(user, sk, foe.Pos).Contains)).ToList();
                GD.Print($"summon: Cinder Drake hit {foes.Count(f => Hurt(b, at, f) > 0)}/{foes.Count} foes in its area, {b.Units.Count(u => u.Side == Tactics.Side.Player && Hurt(b, at, u) > 0)} of yours (friend beside the foe: {Hurt(b, at, friend)})");
            }
            {
                var (b, user, _, _) = Setup();
                var sk = Classes.ClassBook.Skill("fever_dream");
                int at = b.Events.Count;
                b.UseSkill(user, sk, user.Pos);
                var foes = b.Units.Where(u => u.Side == Tactics.Side.Wild && !u.Gone).ToList();
                int reached = b.Events.Skip(at).OfType<Tactics.DamageEvent>().Where(e => e.U.Side == Tactics.Side.Wild).Select(e => e.U).Distinct().Count();
                GD.Print($"illusion: Fever Dream reached {reached}/{foes.Count} foes across the field (hit or dodged), {b.Units.Count(u => u.Side == Tactics.Side.Player && Hurt(b, at, u) > 0)} of yours hurt");
            }
            {
                var (b, user, foe, _) = Setup();
                var sk = Classes.ClassBook.Skill("death_mark");
                for (int k = 0; k < 20 && foe.DoomTurns == 0; k++) { b.UseSkill(user, sk, foe.Pos); user.Cooldowns.Clear(); }
                int turns = 0;
                for (int k = 0; k < 200 && !foe.Gone && foe.Side == Tactics.Side.Wild && b.Winner == null; k++)
                {
                    var u = b.NextTurn();
                    if (u == null) break;
                    if (u == foe) turns++;
                    b.EndTurn(u);
                }
                GD.Print($"doom: {foe.Name} ({foe.Attitude}) {(foe.Gone ? "fell" : foe.Side == Tactics.Side.Player ? "fell and was won over (bested one on one)" : "still stands")} at its turn {turns} after the mark (the mark is 3)");
            }
            {
                var (b, user, foe, _) = Setup();
                var sk = Classes.ClassBook.Skill("sleep_dart");
                for (int k = 0; k < 20 && foe.SleepTurns == 0; k++) { b.UseSkill(user, sk, foe.Pos); user.Cooldowns.Clear(); }
                bool slept = foe.SleepTurns > 0;
                int skipped = 0;
                for (int k = 0; k < 60 && skipped == 0 && b.Winner == null; k++)
                {
                    int at = b.Events.Count;
                    var u = b.NextTurn();
                    if (u == null) break;
                    if (b.Events.Skip(at).OfType<Tactics.StatusEvent>().Any(e => e.U == foe && e.Text == "asleep")) skipped++;
                    b.EndTurn(u);
                }
                int left = foe.SleepTurns;
                var hit = Classes.ClassBook.Skill("quick_strike");
                for (int k = 0; k < 20 && foe.SleepTurns > 0; k++) { b.UseSkill(user, hit, foe.Pos); user.Cooldowns.Clear(); }
                GD.Print($"sleep: asleep {slept}; skipped a turn {skipped > 0} ({left} left); a hit wakes it: {foe.SleepTurns == 0}");
            }
            {
                int tries = 0, finished = 0, landed = 0;
                var sk = Classes.ClassBook.Skill("last_breath");
                for (int k = 0; k < 60; k++)
                {
                    var (b, user, foe, _) = Setup((ulong)k + 1);
                    int dmg = Godot.Mathf.RoundToInt(b.Preview(user, foe).dmg * sk.Power);
                    foe.Hp = Math.Min(foe.MaxHp, dmg + Math.Max(1, foe.MaxHp / 6));
                    int at = b.Events.Count;
                    b.UseSkill(user, sk, foe.Pos);
                    tries++;
                    if (Hurt(b, at, foe) == 0) continue;
                    landed++;
                    if (b.Events.Skip(at).OfType<Tactics.StatusEvent>().Any(e => e.U == foe && e.Text == "finished!")) finished++;
                }
                GD.Print($"finishing blow: {finished}/{landed} landed hits on a worn-down foe finished it (about half expected), {tries} tries");
            }
            {
                var spec = Species.Generate(0x51CE);
                while (Species.FormOf(spec) == Form.Hydra) spec = Species.Generate(spec.Seed + 1);
                var c = Creature.Specimen(spec, 0);
                var hydrarch = Classes.ClassBook.Classes.First(x => x.Id == "Hydrarch");
                bool before = hydrarch.NeedsPart(c);
                spec.Spliced = new[] { Form.Hydra };
                GD.Print($"splice: a {Species.FormOf(spec).ToString().ToLowerInvariant()} meets the Hydrarch's need: {before} before, {hydrarch.NeedsPart(c)} with the hydra's ability spliced in");
            }
            {
                // Stop: frozen for its turns, and a hit doesn't free it.
                var (b, user, foe, _) = Setup(5);
                var sk = Classes.ClassBook.Skill("stop");
                for (int k = 0; k < 20 && foe.StopTurns == 0; k++) { b.UseSkill(user, sk, foe.Pos); user.Cooldowns.Clear(); }
                var hit = Classes.ClassBook.Skill("quick_strike");
                for (int k = 0; k < 10 && foe.Hp == foe.MaxHp; k++) { b.UseSkill(user, hit, foe.Pos); user.Cooldowns.Clear(); }
                int still = foe.StopTurns, skipped = 0;
                for (int k = 0; k < 80 && b.Winner == null && skipped < 2; k++)
                {
                    int at = b.Events.Count;
                    var u = b.NextTurn();
                    if (u == null) break;
                    if (b.Events.Skip(at).OfType<Tactics.StatusEvent>().Any(e => e.U == foe && e.Text == "stopped")) skipped++;
                    b.EndTurn(u);
                }
                GD.Print($"stop: frozen for {still} turns after being hit; it skipped {skipped}; now {foe.StopTurns} left");
            }
            {
                // Quicken: the ally goes next.
                var (b, user, _, friend) = Setup(6);
                b.UseSkill(user, Classes.ClassBook.Skill("quicken"), friend.Pos);
                b.EndTurn(user);
                var next = b.NextTurn();
                GD.Print($"quicken: the next turn is {next?.Name}'s (the quickened ally is {friend.Name})");
            }
            {
                // Traps: set on a tile, hidden from the foe, sprung by the first foe to step there.
                var (b, user, foe, _) = Setup(9);
                var sk = Classes.ClassBook.Skill("snare_trap");
                var tiles = b.SkillTiles(user, sk);
                var spot = tiles.Where(t => t != foe.Pos && b.CanStand(foe, t)).OrderBy(t => Math.Abs(t.X - foe.Pos.X) + Math.Abs(t.Y - foe.Pos.Y)).First();
                b.UseSkill(user, sk, spot);
                int set = b.Traps.Count, hp = foe.Hp;
                foe.Moved = false;
                b.Move(foe, spot);
                var sprung = b.Events.OfType<Tactics.TrapEvent>().FirstOrDefault();
                GD.Print($"trap: {set} set; {foe.Name} walked onto it: sprung {sprung != null}, HP {hp} -> {foe.Hp}, rooted {foe.Rooted}; traps left {b.Traps.Count}");
                var (b2, user2, foe2, _) = Setup(9);
                var blast = Classes.ClassBook.Skill("blast_trap");
                var spot2 = b2.SkillTiles(user2, blast).Where(t => b2.CanStand(foe2, t)).OrderBy(t => Math.Abs(t.X - foe2.Pos.X) + Math.Abs(t.Y - foe2.Pos.Y)).First();
                b2.UseSkill(user2, blast, spot2);
                var mover = b2.Units.First(u => u.Side == Tactics.Side.Player && u != user2);
                mover.Moved = false;
                b2.Move(mover, spot2);
                GD.Print($"trap: your own creature walks over your Blast Trap without springing it: {b2.Traps.Count == 1}");
            }
            {
                // Mirror Shape: the foe's form ability, for 3 turns.
                var (b, user, foe, _) = Setup(7);
                b.UseSkill(user, Classes.ClassBook.Skill("mirror_shape"), foe.Pos);
                string took = user.Borrowed?.Name ?? "nothing";
                for (int k = 0; k < 3; k++) b.EndTurn(user);
                GD.Print($"shape: took {took} from a {Species.FormOf(foe.Creature.Species).ToString().ToLowerInvariant()}; after 3 of its turns it has {(user.Borrowed == null ? "its own shape back" : "still " + user.Borrowed.Name)}");
            }
            {
                // Mimic: learns what a foe uses on it.
                var (b, _, foe, friend) = Setup(8);
                friend.Training ??= new Classes.Training();
                friend.Training.Class = "Mimic";
                var sk = Classes.ClassBook.Skill("hex_bolt");
                b.UseSkill(foe, sk, friend.Pos);
                GD.Print($"mimic: {friend.Name} learned {string.Join(", ", b.Learned.Where(l => l.u == friend).Select(l => Classes.ClassBook.Skill(l.skill).Name))} when a foe used Hex Bolt on it");
            }
            {
                // Trials: a signature class stays locked until its trial is won.
                Ranching.Ranch.SavePath = "user://trials_test.json";
                var ranch = Ranching.Ranch.New();
                var rec = ranch.Creatures[0];
                foreach (var cls in new[] { "Mystic", "Howler" }) { rec.Training.Class = cls; rec.Training.Earn(400); }
                var conjurer = Classes.ClassBook.Class("Conjurer");
                var before = Classes.ClassBook.StandingOf(conjurer, ranch.Get(rec.Id), rec.Training);
                ranch.Flags.Add(World.TrialBook.Flag("Conjurer"));
                var after = Classes.ClassBook.StandingOf(conjurer, ranch.Get(rec.Id), rec.Training);
                var board = World.TrialBook.Open(ranch).Select(t => t.Class).ToList();
                GD.Print($"trial: Conjurer for {rec.Name} is {before} before its trial, {after} after; trials on offer at the start: {string.Join(", ", board)}");
                // Fought through: winning the Reaper's trial opens the class and moves the undercity.
                int under = ranch.UnderStanding;
                var trial = World.TrialBook.ToMission(World.TrialBook.Get("Reaper"));
                bool won = false;
                for (int k = 0; k < 6 && !won; k++)
                {
                    trial.Seed += (ulong)k;
                    var tb = trial.CreateBattle(ranch.SquadCreatures());
                    ranch.LeadUnits(tb);
                    while (tb.Winner == null && tb.Round <= 30) { var u = tb.NextTurn(); if (u == null) break; tb.AiTurn(u); }
                    ranch.AfterBattle(tb, trial);
                    won = tb.Winner == Tactics.Side.Player;
                }
                GD.Print($"trial fought: won {won}; flag {ranch.Flags.Contains(World.TrialBook.Flag("Reaper"))}; undercity standing {under} -> {ranch.UnderStanding}; {string.Join(" ", ranch.Report.Where(l => l.StartsWith("Trial won")))}; still on offer: {World.TrialBook.Open(ranch).Any(t => t.Class == "Reaper")}");
                Classes.ClassBook.TrialDone = _ => true;
            }
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("skilltest"))
        {
            // Every skill in play: your side gets all of them and the AI fights 20 battles.
            var used = new HashSet<string>();
            var reacted = new HashSet<string>();
            int stalls = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var battle = Tactics.Encounter.Create(seed, null, Tactics.BattleMap.Names[(int)(seed % 5)]);
                foreach (var u in battle.Units.Where(u => u.Side == Tactics.Side.Player)) u.Skills = Classes.ClassBook.Skills.Concat(Classes.AugmentBook.Skills).ToList();
                // Each player creature also wears a reaction, cycling through all of them across the battles.
                var reactions = Classes.ClassBook.Passives.Where(p => p.Kind == Classes.PassiveKind.Reaction).ToList();
                int ri = (int)seed * 3;
                foreach (var u in battle.Units.Where(u => u.Side == Tactics.Side.Player)) u.Training.Reaction = reactions[ri++ % reactions.Count].Id;
                // Teleports aren't AI material, so try one by hand.
                var first = battle.Units[0];
                var blink = Classes.ClassBook.Skill("blink");
                var spot = battle.SkillTiles(first, blink).FirstOrDefault(new Godot.Vector2I(-1, -1));
                if (spot.X >= 0) { battle.UseSkill(first, blink, spot); first.Acted = false; first.Cooldowns.Clear(); }
                while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                if (battle.Winner == null) stalls++;
                foreach (var e in battle.Events.OfType<Tactics.SkillEvent>()) used.Add(e.Skill.Id);
                foreach (var e in battle.Events.OfType<Tactics.LogEvent>()) foreach (var r in reactions) if (e.Text.Contains(r.Name)) reacted.Add(r.Id);
                foreach (var e in battle.Events.OfType<Tactics.StatusEvent>()) foreach (var (id, word) in new[] { ("sidestep", "x"), ("spell_ward", "braced"), ("brace", "braced"), ("adrenal_gland", "adrenaline"), ("rallying_cry", "rallied"), ("protector", "protective"), ("premonition", "foreseen") }) if (e.Text.Contains(word) && word != "x") reacted.Add(id);
            }
            GD.Print($"AI sweep: {used.Count}/{Classes.ClassBook.Skills.Length} skills chosen by the AI in 20 battles; stalls: {stalls}");
            GD.Print($"reactions seen firing: {reacted.Count}/{Classes.ClassBook.Passives.Count(p => p.Kind == Classes.PassiveKind.Reaction)} ({string.Join(", ", reacted.OrderBy(x => x))})");

            // Then every skill fired once by hand, with a foe and an ally placed right beside the user.
            int fired = 0;
            var failed = new List<string>();
            foreach (var sk in Classes.ClassBook.Skills.Concat(Classes.AugmentBook.Skills))
            {
                var battle = Tactics.Encounter.Create(3);
                var user = battle.Units.First(u => u.Side == Tactics.Side.Player && u.Size == 1);
                var foe = battle.Units.First(u => u.Side == Tactics.Side.Wild && u.Size == 1);
                var friend = battle.Units.First(u => u.Side == Tactics.Side.Player && u != user && u.Size == 1);
                // Find open, level ground for the user with two free tiles beside it.
                var dirs = new[] { Godot.Vector2I.Left, Godot.Vector2I.Right, Godot.Vector2I.Up, Godot.Vector2I.Down };
                for (int y = 0; y < battle.Map.H; y++)
                for (int x = 0; x < battle.Map.W; x++)
                {
                    var p = new Godot.Vector2I(x, y);
                    if (!battle.CanStand(user, p)) continue;
                    var free = dirs.Select(d => p + d).Where(q => battle.Map.InBounds(q) && battle.CanStand(foe, q) && battle.Map.HeightAt(q) == battle.Map.HeightAt(p)).ToList();
                    if (free.Count < 2) continue;
                    user.Pos = p; foe.Pos = free[0]; friend.Pos = free[1];
                    y = battle.Map.H; break;
                }
                var tiles = battle.SkillTiles(user, sk);
                var aim = tiles.OrderByDescending(t => battle.SkillTargets(user, sk, t).Count).FirstOrDefault(new Godot.Vector2I(-1, -1));
                if (aim.X < 0) { failed.Add($"{sk.Id} (no target)"); continue; }
                try { battle.UseSkill(user, sk, aim); fired++; }
                catch (Exception e) { failed.Add($"{sk.Id} ({e.GetType().Name}: {e.Message})"); }
            }
            GD.Print($"direct: {fired}/{Classes.ClassBook.Skills.Length + Classes.AugmentBook.Skills.Length} skills (augments too) fired without error; problems: {(failed.Count == 0 ? "none" : string.Join("; ", failed))}");

            // Every reaction, directly: a defender wearing it is attacked up to 30 times until its effect shows.
            var proven = new List<string>();
            var unproven = new List<string>();
            foreach (var rx in Classes.ClassBook.Passives.Where(p => p.Kind == Classes.PassiveKind.Reaction))
            {
                var battle = Tactics.Encounter.Create(3);
                var players = battle.Units.Where(u => u.Side == Tactics.Side.Player && u.Size == 1 && u.Attitude == Attitude.Curious).ToList();
                var defender = players[0];
                var friend = players.Count > 1 ? players[1] : battle.Units.First(u => u.Side == Tactics.Side.Player && u != defender && u.Size == 1);
                var attacker = battle.Units.First(u => u.Side == Tactics.Side.Wild && u.Size == 1);
                bool ranged = rx.React == Classes.ReactionKind.ReturnFire;
                // Open, level ground: the defender, a friend beside it, the attacker beside it (or 3 away for Return Fire).
                var dirs = new[] { Godot.Vector2I.Left, Godot.Vector2I.Right, Godot.Vector2I.Up, Godot.Vector2I.Down };
                for (int y = 0; y < battle.Map.H; y++)
                for (int x = 0; x < battle.Map.W; x++)
                {
                    var p = new Godot.Vector2I(x, y);
                    if (!battle.CanStand(defender, p)) continue;
                    var near = dirs.Select(d => p + d).Where(q => battle.Map.InBounds(q) && battle.CanStand(attacker, q) && battle.Map.HeightAt(q) == battle.Map.HeightAt(p)).ToList();
                    var far = p + Godot.Vector2I.Right * 3;
                    if (near.Count < 2 || (ranged && (!battle.Map.InBounds(far) || !battle.CanStand(attacker, far)))) continue;
                    // Leave room to step back, straight away from the attacker.
                    var back = p - (near[0] - p);
                    if (!battle.Map.InBounds(back) || !battle.CanStand(defender, back) || battle.Map.HeightAt(back) != battle.Map.HeightAt(p)) continue;
                    // The friend stands beside, never on the tile behind.
                    var side = near.Skip(1).FirstOrDefault(q => q != back, new Godot.Vector2I(-1, -1));
                    if (side.X < 0) continue;
                    defender.Pos = p; friend.Pos = side; attacker.Pos = ranged ? far : near[0];
                    y = battle.Map.H; break;
                }
                var wearer = rx.React is Classes.ReactionKind.Protector or Classes.ReactionKind.Guard ? friend : defender;
                wearer.Training.Reaction = rx.Id;
                defender.Def = 40; // survive every blow
                bool seen = false;
                for (int k = 0; k < 30 && !seen; k++)
                {
                    battle.Events.Clear();
                    defender.Hp = rx.React == Classes.ReactionKind.Adrenaline ? (defender.MaxHp + 1) / 2 : defender.MaxHp;
                    defender.AdrenalineUsed = false;
                    defender.Buffs.Clear(); friend.Buffs.Clear(); attacker.Stunned = false; attacker.Hp = attacker.MaxHp;
                    var start = defender.Pos;
                    if (ranged) { attacker.Cooldowns.Clear(); battle.UseSkill(attacker, Classes.ClassBook.Skill("hex_bolt"), defender.Pos); }
                    else battle.Attack(attacker, defender);
                    seen = rx.React switch
                    {
                        Classes.ReactionKind.Counter or Classes.ReactionKind.ReturnFire => battle.Events.OfType<Tactics.StrikeEvent>().Any(e => e.A == defender),
                        Classes.ReactionKind.Retreat => defender.Pos != start,
                        Classes.ReactionKind.Brace => defender.Buffs.Any(b => b.stat == Classes.StatKind.Def),
                        Classes.ReactionKind.Protector or Classes.ReactionKind.RallyCry => friend.Buffs.Any(b => b.stat == Classes.StatKind.Atk),
                        Classes.ReactionKind.Burst or Classes.ReactionKind.Shock => battle.Events.OfType<Tactics.DamageEvent>().Any(e => e.U == attacker && !e.Miss),
                        Classes.ReactionKind.Dodge => battle.Events.OfType<Tactics.StatusEvent>().Any(e => e.Text.Contains("foreseen")),
                        Classes.ReactionKind.Guard => battle.Events.OfType<Tactics.LogEvent>().Any(e => e.Text.Contains("shields")),
                        Classes.ReactionKind.Adrenaline => battle.Events.OfType<Tactics.StatusEvent>().Any(e => e.Text.Contains("adrenaline")),
                        Classes.ReactionKind.DirtyRetort => attacker.Stunned,
                        _ => false,
                    };
                    if (defender.Pos != start && rx.React != Classes.ReactionKind.Retreat) defender.Pos = start;
                }
                (seen ? proven : unproven).Add(rx.Id);
            }
            GD.Print($"reactions proven directly: {proven.Count}/{proven.Count + unproven.Count}; not seen: {(unproven.Count == 0 ? "none" : string.Join(", ", unproven))}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("ranchtest"))
        {
            // The loop without a screen: fight, recruit, breed, save, load, and check that every
            // creature survives the save file pixel for pixel. Uses its own save file.
            Ranching.Ranch.SavePath = "user://ranch_test.json";
            var ranch = Ranching.Ranch.New();
            for (int fight = 0; fight < 3; fight++)
            {
                var mission = Tactics.Mission.Board(ranch.Encounter, ranch.Codex)[0];
                var battle = mission.CreateBattle(ranch.SquadCreatures());
                ranch.LeadUnits(battle);
                while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                var joined = ranch.AfterBattle(battle, mission);
                GD.Print($"encounter {ranch.Encounter - 1}: {battle.Winner}, joined: {string.Join(", ", joined.Select(id => ranch.Get(id).Species.Name))}");
                foreach (var line in ranch.Report) GD.Print("  " + line);
                var pair = ranch.Creatures.Where(r => r.Rested).Take(2).Select(r => r.Id).ToList();
                var (child, result) = ranch.Breed(pair[0], pair[1]);
                GD.Print($"  bred {ranch.Get(pair[0]).Species.Name} x {ranch.Get(pair[1]).Species.Name} -> {ranch.Get(child).Species.Name} (gen {ranch.Get(child).Generation})");
            }
            // Multiclassing: Blaze masters Brawler (and its passive), then trains as a Skirmisher with Brawler as secondary.
            var blazeT = ranch.Record(ranch.Creatures.First(r => r.Species.Name == "Blaze").Id).Training;
            GD.Print($"  Blaze earns: {string.Join(", ", blazeT.Earn(200))}; equipped {blazeT.Passive}");
            blazeT.Class = "Skirmisher"; blazeT.Secondary = "Brawler";
            GD.Print($"  Blaze earns: {string.Join(", ", blazeT.Earn(200))}; usable now: {string.Join(", ", blazeT.Usable().Select(s => s.Name))}");
            var blazeC = ranch.Get(ranch.Creatures.First(r => r.Species.Name == "Blaze").Id);
            GD.Print($"  Blaze job tree: " + string.Join(", ", Classes.ClassBook.Classes.Select(k => $"{k.Id}={Classes.ClassBook.StandingOf(k, blazeC, blazeT)}")));

            // The lab: graft wings onto Tank (a walker), fit brass, infuse chem blood, then breed him.
            ranch.Chem += 200;
            int tank = ranch.Creatures.First(r => r.Species.Name == "Tank").Id;
            var tankBefore = ranch.Get(tank);
            string wingKey = ranch.Codex.First(k => k.StartsWith("Wings.") && k != "Wings.None");
            GD.Print($"codex: {ranch.Codex.Count} parts; chem {ranch.Chem}");
            GD.Print("  " + Ranching.Lab.GraftPart(ranch, tank, "Wings", (int)Enum.Parse<Wings>(wingKey.Split('.')[1])));
            ranch.Items["rivet_gun"] = 1;
            GD.Print("  " + ranch.Install(tank, "rivet_gun"));
            GD.Print("  " + Ranching.Lab.Infuse(ranch, tank, Strain.Chemtech));
            var tankAfter = ranch.Get(tank);
            GD.Print($"  Tank moves: {tankBefore.MoveType} -> {tankAfter.MoveType}; strain {tankAfter.Species.Strain}; chem left {ranch.Chem}");
            foreach (var r in ranch.Creatures) r.Rested = true;
            int mate = ranch.Creatures.First(r => r.Id != tank && r.Species.Name == "Sprocket").Id;
            var (pup, _) = ranch.Breed(tank, mate);
            var pupC = ranch.Get(pup);
            GD.Print($"  bred Tank x Sprocket -> {pupC.Species.Name}: wings {pupC.Parts.Wings} (grafts don't pass), strain {pupC.Species.Strain}, grafts {pupC.Species.Grafts}");
            string labPath = ProjectSettings.GlobalizePath("res://.shots/lab.png");
            CreatureSheet.Compose(new List<Creature> { tankBefore, tankAfter, pupC }, false, 4, 3).SavePng(labPath);

            // A duel with the first rival tamer: the report, and their record kept in the save.
            var duel = Tactics.Mission.Duel(Party.RivalBook.All[0], ranch.Encounter, 0, 77);
            var duelBattle = duel.CreateBattle(ranch.SquadCreatures());
            ranch.LeadUnits(duelBattle);
            while (duelBattle.Winner == null && duelBattle.Round <= 30) { var u = duelBattle.NextTurn(); if (u == null) break; duelBattle.AiTurn(u); }
            ranch.AfterBattle(duelBattle, duel);
            GD.Print($"duel: {duel.Title} -> {duelBattle.Winner}; {string.Join(" ", ranch.Report.Where(l => l.Contains(duel.Rival.Name) || l.Contains("codex")))}");
            ranch.Rivals["huntress"] = 2;
            ranch.RivalLosses["occultist"] = 2;
            // Rival prizes: every one in the Brass Baron's hoard (eggs too), and the odds of a draw over 1000 wins.
            foreach (var prize in Party.RivalBook.All[0].Prizes) GD.Print("  " + ranch.Award(Party.RivalBook.All[0], prize, 0xE99 + (ulong)ranch.NextId));
            var drawn = new Dictionary<Party.Rarity, int>();
            for (ulong k = 0; k < 1000; k++) { var p = Party.RivalBook.Draw(Party.RivalBook.All[2], ranch.Titles, k); drawn[p.Rarity] = drawn.GetValueOrDefault(p.Rarity) + 1; }
            GD.Print($"  prize odds over 1000 wins against {Party.RivalBook.All[2].Name}: {string.Join(", ", Enum.GetValues<Party.Rarity>().Select(r => $"{Party.RivalBook.Word(r)} {drawn.GetValueOrDefault(r) / 10f:0.#}%"))}");
            GD.Print("  " + ranch.Rename(tank, "  Gearjaw the Unbelievably Long  "));

            ranch.Save();
            var loaded = Ranching.Ranch.Load();
            int same = 0;
            foreach (var rec in ranch.Creatures)
            {
                var before = CreaturePainter.Paint(ranch.Get(rec.Id), false).GetData();
                var after = CreaturePainter.Paint(loaded.Get(rec.Id), false).GetData();
                if (before.AsSpan().SequenceEqual(after)) same++;
                else GD.Print($"  MISMATCH after load: {ranch.Get(rec.Id).Species.Name}");
            }
            int trained = ranch.Creatures.Count(r => { var l = loaded.Record(r.Id).Training; return l.Class == r.Training.Class && l.Secondary == r.Training.Secondary && l.Passive == r.Training.Passive && l.Reaction == r.Training.Reaction && l.Masteries.SequenceEqual(r.Training.Masteries) && l.Passives.SequenceEqual(r.Training.Passives) && l.Innate == r.Training.Innate && l.Skills.SequenceEqual(r.Training.Skills) && l.JpIn(l.Class) == r.Training.JpIn(r.Training.Class); });
            GD.Print($"training: {trained}/{ranch.Creatures.Count} identical after save and load; " + string.Join("; ", ranch.Creatures.Select(r => $"{ranch.Get(r.Id).Species.Name} {r.Training.Class} [{string.Join(",", r.Training.Skills)}]{(r.Training.Innate != null ? " innate " + r.Training.Innate : "")}")));
            GD.Print($"roundtrip: {same}/{ranch.Creatures.Count} creatures identical after save and load; squad {string.Join(",", loaded.Squad)}; next encounter {loaded.Encounter}");
            GD.Print($"names: {(loaded.Get(tank).Name == ranch.Get(tank).Name && loaded.Get(tank).Nickname != null ? "kept" : "LOST")} after save and load ({loaded.Get(tank).Name}, a {loaded.Get(tank).Species.Name})");
            bool rivalsKept = ranch.Rivals.Count == loaded.Rivals.Count && ranch.Rivals.All(kv => loaded.Rivals.GetValueOrDefault(kv.Key) == kv.Value)
                && ranch.RivalLosses.Count == loaded.RivalLosses.Count && ranch.RivalLosses.All(kv => loaded.RivalLosses.GetValueOrDefault(kv.Key) == kv.Value);
            GD.Print($"rivals: {(rivalsKept ? "identical" : "DIFFERENT")} after save and load; {string.Join(", ", loaded.Rivals.Select(kv => $"{kv.Key} beaten {kv.Value}x").Concat(loaded.RivalLosses.Select(kv => $"{kv.Key} lost to {kv.Value}x running")))}");
            bool prizesKept = loaded.Titles.SetEquals(ranch.Titles) && loaded.Items.Count == ranch.Items.Count && ranch.Items.All(kv => loaded.Items.GetValueOrDefault(kv.Key) == kv.Value)
                && loaded.Creatures.Count(r => r.Shiny) == ranch.Creatures.Count(r => r.Shiny);
            GD.Print($"prizes: {(prizesKept ? "identical" : "DIFFERENT")} after save and load; titles {string.Join(", ", loaded.Titles)}; bag {string.Join(", ", loaded.Items.Select(kv => $"{kv.Key} x{kv.Value}"))}; shiny {loaded.Creatures.Count(r => r.Shiny)}");
            bool party = ranch.Fielded.SequenceEqual(loaded.Fielded) && ranch.Squad.All(id => ranch.LeaderOf(id)?.Id == loaded.LeaderOf(id)?.Id);
            GD.Print($"tamers: {(party ? "identical" : "DIFFERENT")} after save and load; fielded {string.Join(", ", loaded.Fielded)}; "
                + string.Join("; ", loaded.Fielded.Select(w => $"{w} leads {string.Join(", ", loaded.Squad.Where(id => loaded.LeaderOf(id)?.Id == w).Select(id => loaded.Get(id).Species.Name))}")));
            // An old save: a squad of four, no tamers, past the encounter where the second joins.
            var old = Ranching.Ranch.New();
            foreach (var r in old.Creatures.Take(4).ToList()) { old.Squad.Remove(r.Id); old.Squad.Add(r.Id); }
            old.LedBy.Clear();
            old.Fielded = new List<string> { Party.TamerBook.You };
            old.Encounter = 4;
            old.Tidy();
            GD.Print($"old save: squad of {old.Squad.Count} kept under {old.Fielded.Count} tamers ({string.Join("; ", old.Fielded.Select(w => $"{w} {old.Led(w)}"))})");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("breeding", out var bred))
        {
            // Rows of: parent A, parent B, then four children they could have.
            ulong bseed = ulong.TryParse(bred, out var bv) ? bv : 1;
            var pool = CreatureSheet.SpeciesSheet(bseed, 8);
            var rows = new List<Creature>();
            for (int k = 0; k < 4; k++)
            {
                Creature a = pool[2 * k], b = pool[2 * k + 1];
                rows.Add(a);
                rows.Add(b);
                for (int j = 0; j < 4; j++)
                {
                    var r = Breeding.Breed(a, b, SeededRng.Derive(bseed, (ulong)(k * 10 + j)));
                    rows.Add(r.Child);
                    GD.Print($"{a.Species.Name} x {b.Species.Name} -> {r.Child.Species.Name}: from A [{string.Join(", ", r.FromA)}], from B [{string.Join(", ", r.FromB)}] {string.Join("; ", r.Notes)}");
                }
            }
            string bpath = ProjectSettings.GlobalizePath("res://.shots/breeding.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(bpath)!);
            CreatureSheet.Compose(rows, false, 3, 6).SavePng(bpath);
            GD.Print($"wrote {bpath}");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("forms", out var formsArg))
        {
            // One creature of every form (the first species of that plan from seed N on), front and
            // back, standing on board tiles: the check on silhouettes. --views does the same for a sheet.
            // --only=Lung,Jelly keeps just those forms, picked exactly as in the full sheet; --many=K shows K of each.
            ulong start = ulong.TryParse(formsArg, out var fs) ? fs : 1;
            int many = args.TryGetValue("many", out var manyArg) && int.TryParse(manyArg, out var mv) ? mv : 1;
            var only = args.TryGetValue("only", out var onlyArg) ? onlyArg.Split(',').Select(n => n.Trim().ToLowerInvariant()).ToHashSet() : null;
            var list = new List<Creature>();
            foreach (var form in Enum.GetValues<Form>().Where(f => f != Form.Auto))
            {
                var plan = Enum.GetValues<BodyPlan>().First(p => Species.AllFormsFor(p).Contains(form));
                int found = 0;
                for (ulong seed = start; seed < start + 4000; seed++)
                {
                    var fsp = Species.Generate(seed);
                    if (fsp.Plan != plan) continue;
                    fsp.Form = form;
                    var fc = Creature.Specimen(fsp, 0);
                    if (args.ContainsKey("medium")) fc.Size = SizeClass.Medium;
                    if (only == null || only.Contains(form.ToString().ToLowerInvariant())) list.Add(fc);
                    start = seed + 1;
                    if (++found >= many) break;
                }
            }
            if (args.TryGetValue("idle", out var idleName))
            {
                // The idle loop as numbered frames, for a GIF: .shots/idle/NAME/f00.png onward.
                string dir = ProjectSettings.GlobalizePath($"res://.shots/idle/{(idleName == "" ? "loop" : idleName)}");
                System.IO.Directory.CreateDirectory(dir);
                int iscale = args.TryGetValue("scale", out var isc) && int.TryParse(isc, out var isv) ? isv : 3;
                int icols = args.TryGetValue("columns", out var icol) && int.TryParse(icol, out var icv) ? icv : 4;
                // --actions plays every action in turn instead of only breathing.
                var frames = args.ContainsKey("actions")
                    ? CreatureSheet.ComposeAnimation(list, iscale, icols, CreatureSheet.ActionReel())
                    : CreatureSheet.ComposeIdle(list, iscale, icols);
                for (int fi = 0; fi < frames.Count; fi++) frames[fi].SavePng($"{dir}/f{fi:00}.png");
                GD.Print($"wrote {frames.Count} frames to {dir}");
                GetTree().Quit();
                return;
            }
            string fpath = ProjectSettings.GlobalizePath("res://.shots/forms.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fpath)!);
            CreatureSheet.ComposeViews(list, four, args.TryGetValue("scale", out var fsc) && int.TryParse(fsc, out var fsv) ? fsv : 2,
                args.TryGetValue("columns", out var fcol) && int.TryParse(fcol, out var fcv) ? fcv : 6).SavePng(fpath);
            GD.Print($"wrote {fpath}: " + string.Join(", ", list.Select(c => $"{c.Species.Form} {c.Species.Name}")));
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("views", out var viewsArg))
        {
            ulong vs = ulong.TryParse(viewsArg, out var vsv) ? vsv : 1;
            string vpath = ProjectSettings.GlobalizePath("res://.shots/views.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(vpath)!);
            CreatureSheet.ComposeViews(CreatureSheet.SpeciesSheet(vs, 24), four, 2, 6).SavePng(vpath);
            GD.Print($"wrote {vpath}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("replaytest"))
        {
            // The lockstep check: play battles AI against AI on every map, then rebuild each one from
            // its commands alone and compare the state's fingerprint after every turn.
            int same = 0, total = 0, turns = 0;
            var problems = new List<string>();
            for (ulong seed = 1; seed <= 14; seed++)
            {
                string map = Tactics.BattleMap.Names[(int)(seed % (ulong)Tactics.BattleMap.Names.Length)];
                Tactics.Battle Make() => Tactics.Encounter.Create(seed, null, map);
                var battle = Make();
                while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                var (ok, matched) = Tactics.Battle.Replay(battle, Make);
                total++; turns += matched;
                if (ok) same++; else problems.Add($"seed {seed} on {map}: diverged at turn {matched} of {battle.TurnHashes.Count}");
            }
            // A duel against every rival tamer: trained squads, boosts and every skill they bring.
            foreach (var rival in Party.RivalBook.All)
            {
                ulong duelSeed = 100 + (ulong)total;
                Tactics.Battle Make() => Tactics.Mission.Duel(rival, (ulong)rival.AppearsAt + 2, 1, duelSeed).CreateBattle(Tactics.Encounter.StarterSquad());
                var battle = Make();
                while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                var (ok, matched) = Tactics.Battle.Replay(battle, Make);
                total++; turns += matched;
                if (ok) same++; else problems.Add($"duel with {rival.Name}: diverged at turn {matched} of {battle.TurnHashes.Count}");
            }
            GD.Print($"replay: {same}/{total} battles rebuilt identically from their commands alone ({turns} turns checked); {(problems.Count == 0 ? "no divergence" : string.Join("; ", problems))}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("props"))
        {
            // Every prop in every region's colours: one row per region, each on a board tile.
            string[] themes = { "Undercity", "Uptown", "Mire", "Ruins", "Cavern" };
            var kinds = Enum.GetValues<Tactics.Prop>();
            int cell = 64, scale = args.TryGetValue("scale", out var psc) && int.TryParse(psc, out var psv) ? psv : 3;
            var propSheet = Image.CreateEmpty(kinds.Length * cell, themes.Length * cell, false, Image.Format.Rgba8);
            propSheet.Fill(Color.FromHtml("#16121f"));
            for (int r = 0; r < themes.Length; r++)
            for (int i = 0; i < kinds.Length; i++)
            {
                var (img, a) = Tactics.PropArt.Paint(kinds[i], r, themes[r]);
                int gx = i * cell + cell / 2, gy = r * cell + cell - 14;
                for (int y = -12; y <= 12; y++)
                for (int x = -24; x <= 24; x++)
                    if (Mathf.Abs(x) / 24f + Mathf.Abs(y) / 12f <= 1 && gx + x >= 0 && gx + x < propSheet.GetWidth()) propSheet.SetPixel(gx + x, gy + y, Color.FromHtml("#2a2438"));
                propSheet.BlendRect(img, new Rect2I(0, 0, img.GetWidth(), img.GetHeight()), new Vector2I(gx - a.X, gy - a.Y));
            }
            propSheet.Resize(propSheet.GetWidth() * scale, propSheet.GetHeight() * scale, Image.Interpolation.Nearest);
            string ppath = ProjectSettings.GlobalizePath("res://.shots/props.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ppath)!);
            propSheet.SavePng(ppath);
            GD.Print($"wrote {ppath}: {kinds.Length} props x {themes.Length} regions");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("webkit", out var webDir))
        {
            // What the game's page on ecec.dev shows: strays, menu frames, glyphs (SpriteLab/WebKit.cs).
            string wdir = webDir == "" ? ProjectSettings.GlobalizePath("res://.shots/webkit") : webDir;
            SpriteLab.WebKit.Write(wdir, args.TryGetValue("from", out var wfrom) && ulong.TryParse(wfrom, out var wf) ? wf : 9001);
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("icons"))
        {
            // Every skill icon, then every glyph, at x4: the readability check.
            string ipath = ProjectSettings.GlobalizePath("res://.shots/icons.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ipath)!);
            var sheetImg = UI.SkillIcons.Sheet();
            var glyphs = UI.Glyphs.Names.ToList();
            int gw = 15, grows = (glyphs.Count + gw - 1) / gw;
            var all = Image.CreateEmpty(sheetImg.GetWidth(), sheetImg.GetHeight() + grows * 16 + 4, false, Image.Format.Rgba8);
            all.Fill(Color.FromHtml("#16121f"));
            all.BlitRect(sheetImg, new Rect2I(0, 0, sheetImg.GetWidth(), sheetImg.GetHeight()), Vector2I.Zero);
            for (int i = 0; i < glyphs.Count; i++) UI.Glyphs.Draw(all, glyphs[i], 6 + i % gw * 24, sheetImg.GetHeight() + 2 + i / gw * 16);
            all.Resize(all.GetWidth() * 4, all.GetHeight() * 4, Image.Interpolation.Nearest);
            all.SavePng(ipath);
            GD.Print($"wrote {ipath}: {Classes.ClassBook.Skills.Length} skill icons, {glyphs.Count} glyphs");
            GetTree().Quit();
            return;
        }

        if (args.TryGetValue("export", out var file))
        {
            int Int(string key, int fallback) => args.TryGetValue(key, out var v) && int.TryParse(v, out var x) ? x : fallback;
            int from = Int("from", 1), count = Int("count", 24), total = from - 1 + count;
            var creatures = species != null ? CreatureSheet.Siblings(species, total)
                : drawPets ? CreatureSheet.DrawPetsSheet(total)
                : CreatureSheet.SpeciesSheet(sheet, total);
            from = Math.Min(from, creatures.Count + 1);
            Export(file, creatures.GetRange(from - 1, Math.Min(count, creatures.Count - from + 1)), from, four, Int("scale", 3), Int("columns", 8));
            GetTree().Quit();
            return;
        }

        _lab = () => new SpriteLabScreen { SheetSeed = sheet, SpeciesView = species, DrawPets = drawPets, FourColor = four };
        _autoPlay = args.ContainsKey("autoplay");
        _goTo = args.GetValueOrDefault("goto");
        _shotFrames = args.TryGetValue("shot", out var shot) && int.TryParse(shot, out var f) ? f : -1;
        _recordFrames = args.TryGetValue("record", out var recordArg) && int.TryParse(recordArg, out var recordCount) ? recordCount : 0;
        _recordWhole = args.ContainsKey("recordwhole");

        if (args.ContainsKey("lab") || drawPets || species != null) { _home = _lab; Show(_lab()); return; }
        // --uiboard=NAME: one menu style's parts side by side over a board, to compare the styles (with --shot).
        if (args.TryGetValue("uiboard", out var boardSkin))
        {
            UI.UiSkin.TryParse(boardSkin, out var shown);
            Show(new UI.StyleBoard { Skin = shown });
            return;
        }
        if (args.TryGetValue("battle", out var oneOff))
        {
            // A one-off battle with the default squad, no ranch involved.
            ulong seed = ulong.TryParse(oneOff, out var bs) ? bs : 1;
            // --duel=N: the Nth rival tamer's squad instead of a wild pack (--enc=E sets how far along, --wins=W how often
            // beaten, --losses=L how many duels in a row they've won).
            Tactics.Mission duel = null;
            if (args.TryGetValue("duel", out var duelArg) && int.TryParse(duelArg, out var duelIndex))
            {
                ulong duelEnc = args.TryGetValue("enc", out var encArg) && ulong.TryParse(encArg, out var ev) ? ev : 4;
                int duelWins = args.TryGetValue("wins", out var winsArg) && int.TryParse(winsArg, out var wv) ? wv : 0;
                int duelLosses = args.TryGetValue("losses", out var lossArg) && int.TryParse(lossArg, out var lv) ? lv : 0;
                duel = Tactics.Mission.Duel(Party.RivalBook.All[duelIndex % Party.RivalBook.All.Length], duelEnc, duelWins, seed, duelLosses);
            }
            // --bag: a few shop items in the bag, to see them in the command bar.
            System.Action<Tactics.Battle> bag = args.ContainsKey("bag") ? b => b.Pack(new Dictionary<string, int> { ["snare_net"] = 3, ["soothing_chime"] = 1, ["tonic"] = 2, ["smelling_salts"] = 1 }) : null;
            _home = () => new Tactics.BattleScreen { Seed = seed, Mission = duel, Prepare = bag, AutoPlay = _autoPlay, DemoAim = args.ContainsKey("aim"), DemoFx = args.ContainsKey("fxdemo"), DemoSignatures = args.TryGetValue("fxdemo", out var fxWhich) && fxWhich == "signature", DemoShiny = args.ContainsKey("shiny"), TurnSeconds = args.TryGetValue("timer", out var timerArg) && float.TryParse(timerArg, out var timerSeconds) ? timerSeconds : 0, MapName = args.TryGetValue("map", out var mapName) ? mapName : "Undercity Foundry" };
            Show(_home());
            return;
        }
        // The game proper: the title (save slots and settings), then the base, battles from it, and back.
        // Test and screenshot flags go straight in; --title opens the title anyway (=new|load|settings for a panel),
        // --slots=NAME keeps the slots in other files.
        _home = () => Base(new List<int>());
        if (args.TryGetValue("slots", out var slotName) && slotName != "") Ranching.Ranch.SlotPrefix = $"user://{slotName}";
        string[] direct = { "save", "newranch", "autoplay", "station", "labmode", "jobsmode", "partymode", "vatdemo", "scene", "mapmode", "boardmode", "dispatchdemo", "shopdemo", "goto", "choicedemo", "encounter", "losses", "select", "pause" };
        if (args.ContainsKey("title") || !direct.Any(args.ContainsKey))
        {
            _titlePanel = args.GetValueOrDefault("title");
            ShowTitle();
            return;
        }
        if (args.TryGetValue("save", out var saveName) && saveName != "") Ranching.Ranch.SavePath = $"user://{saveName}.json";
        _demoPause = args.ContainsKey("pause");
        _ranch = args.ContainsKey("newranch") ? Ranching.Ranch.New() : Ranching.Ranch.Load();
        // --choicedemo: a main mission's choice waiting (screenshots; use it with --save=NAME).
        if (args.ContainsKey("choicedemo")) _ranch.Pending ??= "main_foundry";
        // --encounter=E jumps the ranch ahead (for looking at later boards; use it with --save=NAME).
        if (args.TryGetValue("encounter", out var encounterArg) && ulong.TryParse(encounterArg, out var encounterAt)) _ranch.Encounter = encounterAt;
        // --losses=L: every rival has beaten you L times running (the board shows them cocky).
        if (args.TryGetValue("losses", out var ranchLosses) && int.TryParse(ranchLosses, out var rl)) foreach (var r in Party.RivalBook.All) _ranch.RivalLosses[r.Id] = rl;
        bool vatDemo = args.ContainsKey("vatdemo");
        // Screenshots open a station directly: --labmode, --jobsmode, --partymode, --vatdemo, --station=ID (with --select=NAME).
        string station = args.GetValueOrDefault("station") ?? (args.ContainsKey("labmode") ? "lab" : args.ContainsKey("jobsmode") ? "yard" : args.ContainsKey("partymode") ? "war" : vatDemo ? "vats" : null);
        if (station != null) { Show(Station(station, vatDemo, args.GetValueOrDefault("select"))); return; }
        // --mapmode (or --boardmode, from before the map) opens on the country map.
        _demoDispatch = args.ContainsKey("dispatchdemo");
        _demoShop = args.ContainsKey("shopdemo");
        // --scene=ID plays a cutscene on its own, then goes home (with --autoplay its lines move on by themselves).
        if (args.TryGetValue("scene", out var sceneId) && Story.SceneBook.Get(sceneId) is { } scene)
            Show(new Story.CutsceneScreen { Scene = scene, Ranch = _ranch, Auto = _autoPlay, Done = () => Show(_home()) });
        else if (args.ContainsKey("mapmode") || args.ContainsKey("boardmode") || _demoDispatch || _demoShop) ShowMap();
        else Show(_home());
    }

    // Home is the base you walk around; its stations open the ranch's systems one at a time.
    Ranching.BaseScreen Base(List<int> joined)
    {
        var home = new Ranching.BaseScreen { Ranch = _ranch, Use = UseStation, Fight = StartRanchBattle, JustJoined = joined, AutoFight = _autoPlay, DemoGoTo = _goTo, DemoPause = _demoPause, ToTitle = ShowTitle };
        _goTo = null; // a demo walk happens once
        _demoPause = false;
        return home;
    }

    async void PressPad(string[] presses, float after)
    {
        await ToSignal(GetTree().CreateTimer(after), SceneTreeTimer.SignalName.Timeout);
        foreach (var p in presses)
        {
            Key? key = p.Trim().ToLowerInvariant() switch
            {
                "up" => Key.Up, "down" => Key.Down, "left" => Key.Left, "right" => Key.Right,
                "a" => Key.Enter, "b" => Key.Escape, "start" => UI.Phone.Start, _ => null,
            };
            if (key is { } k)
            {
                UI.Phone.Frame.View.Key(k, true, false);
                UI.Phone.Frame.View.Key(k, false, false);
                GD.Print($"pad: {p.Trim()}, focus on {GetViewport().GuiGetFocusOwner()?.GetType().Name ?? "nothing"} {(GetViewport().GuiGetFocusOwner() as Button)?.Text}");
            }
            await ToSignal(GetTree().CreateTimer(p.Trim() == "wait" ? 1.0 : 0.4), SceneTreeTimer.SignalName.Timeout);
        }
    }

    void ShowTitle()
    {
        Show(new UI.TitleScreen { Start = StartSlot, Open = _titlePanel });
        _titlePanel = null;
    }

    /// <summary>Plays a save slot: a new game there, or the one saved in it. It's the slot to continue next time.</summary>
    void StartSlot(int slot, bool fresh)
    {
        Ranching.Ranch.SavePath = Ranching.Ranch.SlotPath(slot);
        _ranch = fresh ? Ranching.Ranch.New() : Ranching.Ranch.Load();
        if (fresh) _ranch.Save();
        UI.Settings.LastSlot = slot;
        UI.Settings.Save();
        Ranching.BaseScreen.Standing = null;
        Show(_home());
    }

    /// <summary>A station of the base, used: the gate goes out to the map; the rest open their part of the ranch.</summary>
    void UseStation(string id)
    {
        if (id == "gate") ShowMap();
        else Show(Station(id));
    }

    Ranching.RanchScreen Station(string id, bool vatDemo = false, string select = null) =>
        new() { Ranch = _ranch, Station = id, Back = () => Show(_home()), Fight = StartRanchBattle, OpenMap = ShowMap, VatDemo = vatDemo, StartOn = select, ToTitle = ShowTitle };

    void ShowMap() => Show(new World.CountryScreen { Ranch = _ranch, Fight = StartRanchBattle, Back = () => Show(_home()), DemoDispatch = _demoDispatch, DemoShop = _demoShop, ToTitle = ShowTitle });
    bool _demoDispatch, _demoShop;

    System.Func<Node> _lab, _home;
    Ranching.Ranch _ranch;
    bool _autoPlay;
    Node _screen;
    int _shotFrames = -1, _recordFrames, _recorded;
    bool _recordWhole;
    string _goTo; // --goto=ID: at the base, walk to that station and use it
    string _titlePanel; // --title=new|load|settings: open the title on that panel
    bool _demoPause;   // --pause: open the base on its pause menu

    void Show(Node screen)
    {
        if (_autoPlay) GD.Print($"screen: {screen.GetType().Name}"); // trace the attract loop
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }

    void StartRanchBattle(Tactics.Mission mission)
    {
        // A main mission's cutscene plays first, the first time (Esc skips it).
        if (mission.Story != null && World.StoryBook.Get(mission.Story)?.Scene is { } sceneId && Story.SceneBook.Get(sceneId) is { } scene && _ranch.Flags.Add("seen:" + sceneId))
        {
            _ranch.Save();
            Show(new Story.CutsceneScreen { Scene = scene, Ranch = _ranch, Mission = mission, Auto = _autoPlay, Done = () => Fight(mission) });
            return;
        }
        Fight(mission);
    }

    void Fight(Tactics.Mission mission)
    {
        Show(new Tactics.BattleScreen
        {
            Seed = mission.Seed, Mission = mission, Squad = _ranch.SquadCreatures(), AutoPlay = _autoPlay, Prepare = _ranch.LeadUnits, TurnSeconds = UI.Settings.TurnTimer,
            Finished = battle =>
            {
                var joined = _ranch.AfterBattle(battle, mission);
                // A coliseum cup goes straight on to its next round while you keep winning.
                if (mission.Cup != null && battle.Winner == Tactics.Side.Player && mission.Round < 3)
                {
                    StartRanchBattle(World.CupBook.Round(_ranch, World.CupBook.Get(mission.Cup), mission.Round + 1));
                    return;
                }
                Show(Base(joined));
            },
        });
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.N })
        {
            UI.Settings.Muted = !Audio.Sound.Muted;
            UI.Settings.Apply();
            UI.Settings.Save();
            Audio.Sound.Play("click");
            return;
        }
        // Tab swaps between the Sprite Lab and home (the ranch, or a one-off battle), never mid-fight.
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab }) return;
        if (_screen is SpriteLabScreen) Show(_home());
        else if (_screen is Ranching.RanchScreen or Ranching.BaseScreen || _ranch == null) Show(_lab());
    }

    public override void _Process(double delta)
    {
        // --shot=N: save frame N to .shots/shot.png and quit (how screen changes get checked).
        // --record=K: then save the next K frames' middle (960x540, full size) to .shots/rec/ (run with --fixed-fps 30 for real time);
        // with --recordwhole, the whole screen at half size instead (cutscenes, whose dialogue sits low).
        if (_shotFrames < 0 || --_shotFrames > 0) return;
        string dir = ProjectSettings.GlobalizePath("res://.shots");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var frame = GetTree().Root.GetTexture().GetImage(); // the whole window: on a phone, the pad too
        if (_recorded < _recordFrames)
        {
            DirAccess.MakeDirRecursiveAbsolute($"{dir}/rec");
            if (_recordWhole) frame.Resize(frame.GetWidth() / 2, frame.GetHeight() / 2, Image.Interpolation.Bilinear);
            else frame = frame.GetRegion(new Rect2I(frame.GetWidth() / 4, frame.GetHeight() / 4, frame.GetWidth() / 2, frame.GetHeight() / 2));
            frame.SavePng($"{dir}/rec/f{_recorded++:000}.png");
            _shotFrames = 1;
            return;
        }
        frame.SavePng($"{dir}/shot.png");
        GetTree().Quit();
    }

    /// <summary>
    /// Trains the way a player would, for the headless country loop: once a class's skills and passives
    /// are learned, move on to the next open one (favoured first) and keep the best learned set as the secondary.
    /// </summary>
    static void Retrain(Creature c, Classes.Training t)
    {
        if (t.Stars(t.Class) < 2) return;
        var open = Classes.ClassBook.Available(c, t).Where(k => t.Stars(k.Id) < 2).Select(k => k.Id).ToList();
        if (open.Count == 0) return;
        t.Class = open.FirstOrDefault(k => Classes.ClassBook.Favoured(c, k)) ?? open[0];
        t.Secondary = t.Jp.Keys.Where(k => k != t.Class && t.LearnedIn(k) > 0).OrderByDescending(t.LearnedIn).ThenByDescending(t.JpIn).FirstOrDefault();
    }

    static void Export(string file, List<Creature> creatures, int from, bool four, int scale, int columns)
    {
        string path = System.IO.Path.IsPathRooted(file) ? file : ProjectSettings.GlobalizePath("res://" + file);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var err = CreatureSheet.Compose(creatures, four, scale, columns).SavePng(path);
        GD.Print($"export {(err == Error.Ok ? "wrote" : "FAILED " + err)} {path}");
        for (int i = 0; i < creatures.Count; i++)
            GD.Print($"#{from + i,-3}" + CreatureSheet.Describe(creatures[i]).Replace("\n", "\n     "));
    }

    static Dictionary<string, string> ParseArgs(string[] raw)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in raw)
        {
            if (!a.StartsWith("--")) continue;
            int eq = a.IndexOf('=');
            if (eq < 0) d[a[2..]] = "";
            else d[a[2..eq]] = a[(eq + 1)..];
        }
        return d;
    }
}
