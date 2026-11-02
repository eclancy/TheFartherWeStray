using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CreatureTactics.Creatures;
using CreatureTactics.SpriteLab;
using Godot;

namespace CreatureTactics;

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
///   --lab             open the Sprite Lab (Tab switches)
///   --battle=N        a one-off battle with the default squad; --autoplay lets the AI play your side
///   --autobattle=N    run a battle AI-vs-AI headless and print the log
///   --shot=N          save frame N to .shots/shot.png and quit
///   --export=FILE     write the sheet (or species) as a PNG and quit; works with --headless
///   --scale=N         export pixel scale (default 3)
///   --columns=N --from=N --count=N   export layout: columns, first slot (1-based) and how many (8, 1, 24)
/// </summary>
public partial class GameRoot : Node
{
    public override void _Ready()
    {
        var args = ParseArgs(OS.GetCmdlineUserArgs());
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

        if (args.ContainsKey("ranchtest"))
        {
            // The loop without a screen: fight, recruit, breed, save, load, and check that every
            // creature survives the save file pixel for pixel. Uses its own save file.
            Ranching.Ranch.SavePath = "user://ranch_test.json";
            var ranch = Ranching.Ranch.New();
            for (int fight = 0; fight < 3; fight++)
            {
                var battle = Tactics.Encounter.Create(ranch.Encounter, ranch.SquadCreatures());
                while (battle.Winner == null && battle.Round <= 30) { var u = battle.NextTurn(); if (u == null) break; battle.AiTurn(u); }
                var joined = ranch.AfterBattle(battle);
                GD.Print($"encounter {ranch.Encounter - 1}: {battle.Winner}, joined: {string.Join(", ", joined.Select(id => ranch.Get(id).Species.Name))}");
                var pair = ranch.Creatures.Where(r => r.Rested).Take(2).Select(r => r.Id).ToList();
                var (child, result) = ranch.Breed(pair[0], pair[1]);
                GD.Print($"  bred {ranch.Get(pair[0]).Species.Name} x {ranch.Get(pair[1]).Species.Name} -> {ranch.Get(child).Species.Name} (gen {ranch.Get(child).Generation})");
            }
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
            GD.Print($"roundtrip: {same}/{ranch.Creatures.Count} creatures identical after save and load; squad {string.Join(",", loaded.Squad)}; next encounter {loaded.Encounter}");
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
        _shotFrames = args.TryGetValue("shot", out var shot) && int.TryParse(shot, out var f) ? f : -1;

        if (args.ContainsKey("lab") || drawPets || species != null) { _home = _lab; Show(_lab()); return; }
        if (args.TryGetValue("battle", out var oneOff))
        {
            // A one-off battle with the default squad, no ranch involved.
            ulong seed = ulong.TryParse(oneOff, out var bs) ? bs : 1;
            _home = () => new Tactics.BattleScreen { Seed = seed, AutoPlay = _autoPlay };
            Show(_home());
            return;
        }
        // The game proper: the ranch, battles from it, and back.
        if (args.TryGetValue("save", out var saveName) && saveName != "") Ranching.Ranch.SavePath = $"user://{saveName}.json";
        _ranch = args.ContainsKey("newranch") ? Ranching.Ranch.New() : Ranching.Ranch.Load();
        bool vatDemo = args.ContainsKey("vatdemo");
        _home = () => new Ranching.RanchScreen { Ranch = _ranch, Fight = StartRanchBattle, VatDemo = vatDemo, AutoFight = _autoPlay };
        Show(_home());
    }

    System.Func<Node> _lab, _home;
    Ranching.Ranch _ranch;
    bool _autoPlay;
    Node _screen;
    int _shotFrames = -1;

    void Show(Node screen)
    {
        if (_autoPlay) GD.Print($"screen: {screen.GetType().Name}"); // trace the attract loop
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }

    void StartRanchBattle()
    {
        Show(new Tactics.BattleScreen
        {
            Seed = _ranch.Encounter, Squad = _ranch.SquadCreatures(), AutoPlay = _autoPlay,
            Finished = battle =>
            {
                var joined = _ranch.AfterBattle(battle);
                Show(new Ranching.RanchScreen { Ranch = _ranch, Fight = StartRanchBattle, JustJoined = joined, AutoFight = _autoPlay });
            },
        });
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Tab swaps between the Sprite Lab and home (the ranch, or a one-off battle), never mid-fight.
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab }) return;
        if (_screen is SpriteLabScreen) Show(_home());
        else if (_screen is Ranching.RanchScreen || _ranch == null) Show(_lab());
    }

    public override void _Process(double delta)
    {
        // --shot=N: save frame N to .shots/shot.png and quit (how screen changes get checked).
        if (_shotFrames < 0 || --_shotFrames > 0) return;
        string dir = ProjectSettings.GlobalizePath("res://.shots");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/shot.png");
        GetTree().Quit();
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
