using System;
using System.Collections.Generic;
using System.Globalization;
using CreatureTactics.Creatures;
using CreatureTactics.SpriteLab;
using Godot;

namespace CreatureTactics;

/// <summary>
/// Entry point: a battle by default, or the Sprite Lab (the creature generator prototype).
/// Command-line options come after "--", e.g. `--path . -- --sheet=42 --four`:
///   --sheet=N         start on sheet N
///   --species=X       open the species view: a species seed (16 hex digits, shown on hover) or a DrawPets name
///   --drawpets        start on the DrawPets cast
///   --four            start in 4-colour mode
///   --lab             open the Sprite Lab instead of a battle (Tab switches)
///   --battle=N        battle encounter seed; --autoplay lets the AI play your side
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
        _battle = () => new Tactics.BattleScreen
        {
            Seed = args.TryGetValue("battle", out var b) && ulong.TryParse(b, out var bs) ? bs : 1,
            AutoPlay = args.ContainsKey("autoplay"),
        };
        bool lab = args.ContainsKey("lab") || args.ContainsKey("drawpets") || species != null;
        Show(lab ? _lab() : _battle());
        _shotFrames = args.TryGetValue("shot", out var shot) && int.TryParse(shot, out var f) ? f : -1;
    }

    System.Func<Node> _lab, _battle;
    Node _screen;
    int _shotFrames;

    void Show(Node screen)
    {
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Tab swaps between the battle and the Sprite Lab.
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab })
            Show(_screen is SpriteLabScreen ? _battle() : _lab());
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
