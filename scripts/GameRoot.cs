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
///   --style=ID        art style: neon (default), ffta, painted, noir, grim (V cycles them in the Sprite Lab)
///   --lab             open the Sprite Lab (Tab switches)
///   --battle=N        a one-off battle with the default squad; --autoplay lets the AI play your side,
///                     --aim aims the first skill at the busiest tile (for screenshots of the skill panel)
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
        // A crash during setup quits instead of leaving a headless run hanging until its timeout.
        try { Start(); }
        catch (Exception e) { GD.PrintErr(e.ToString()); GetTree().Quit(1); }
    }

    void Start()
    {
        var args = ParseArgs(OS.GetCmdlineUserArgs());
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

        if (args.ContainsKey("skilltest"))
        {
            // Every skill in play: your side gets all of them and the AI fights 20 battles.
            var used = new HashSet<string>();
            var reacted = new HashSet<string>();
            int stalls = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var battle = Tactics.Encounter.Create(seed, null, Tactics.BattleMap.Names[(int)(seed % 5)]);
                foreach (var u in battle.Units.Where(u => u.Side == Tactics.Side.Player)) u.Skills = Classes.ClassBook.Skills.ToList();
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
            foreach (var sk in Classes.ClassBook.Skills)
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
            GD.Print($"direct: {fired}/{Classes.ClassBook.Skills.Length} skills fired without error; problems: {(failed.Count == 0 ? "none" : string.Join("; ", failed))}");

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
            GD.Print("  " + Ranching.Lab.AddMachine(ranch, tank, Graft.Plating));
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
            bool party = ranch.Fielded.SequenceEqual(loaded.Fielded) && ranch.Squad.All(id => ranch.LeaderOf(id)?.Id == loaded.LeaderOf(id)?.Id);
            GD.Print($"wardens: {(party ? "identical" : "DIFFERENT")} after save and load; fielded {string.Join(", ", loaded.Fielded)}; "
                + string.Join("; ", loaded.Fielded.Select(w => $"{w} leads {string.Join(", ", loaded.Squad.Where(id => loaded.LeaderOf(id)?.Id == w).Select(id => loaded.Get(id).Species.Name))}")));
            // An old save: a squad of four, no wardens, past the encounter where the second joins.
            var old = Ranching.Ranch.New();
            foreach (var r in old.Creatures.Take(4).ToList()) { old.Squad.Remove(r.Id); old.Squad.Add(r.Id); }
            old.LedBy.Clear();
            old.Fielded = new List<string> { Party.WardenBook.You };
            old.Encounter = 4;
            old.Tidy();
            GD.Print($"old save: squad of {old.Squad.Count} kept under {old.Fielded.Count} wardens ({string.Join("; ", old.Fielded.Select(w => $"{w} {old.Led(w)}"))})");
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
            // --only=Lung,Jelly keeps just those forms, picked exactly as in the full sheet.
            ulong start = ulong.TryParse(formsArg, out var fs) ? fs : 1;
            var only = args.TryGetValue("only", out var onlyArg) ? onlyArg.Split(',').Select(n => n.Trim().ToLowerInvariant()).ToHashSet() : null;
            var list = new List<Creature>();
            foreach (var form in Enum.GetValues<Form>().Where(f => f != Form.Auto))
            {
                var plan = Enum.GetValues<BodyPlan>().First(p => Species.AllFormsFor(p).Contains(form));
                for (ulong seed = start; seed < start + 4000; seed++)
                {
                    var fsp = Species.Generate(seed);
                    if (fsp.Plan != plan) continue;
                    fsp.Form = form;
                    var fc = Creature.Specimen(fsp, 0);
                    if (args.ContainsKey("medium")) fc.Size = SizeClass.Medium;
                    if (only == null || only.Contains(form.ToString().ToLowerInvariant())) list.Add(fc);
                    start = seed + 1;
                    break;
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
            GD.Print($"replay: {same}/{total} battles rebuilt identically from their commands alone ({turns} turns checked); {(problems.Count == 0 ? "no divergence" : string.Join("; ", problems))}");
            GetTree().Quit();
            return;
        }

        if (args.ContainsKey("props"))
        {
            // Every prop in every region's colours: one row per region, each on a board tile.
            string[] themes = { "Undercity", "Piltover", "Sump", "Ruins", "Cavern" };
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
        _shotFrames = args.TryGetValue("shot", out var shot) && int.TryParse(shot, out var f) ? f : -1;
        _recordFrames = args.TryGetValue("record", out var recordArg) && int.TryParse(recordArg, out var recordCount) ? recordCount : 0;

        if (args.ContainsKey("lab") || drawPets || species != null) { _home = _lab; Show(_lab()); return; }
        if (args.TryGetValue("battle", out var oneOff))
        {
            // A one-off battle with the default squad, no ranch involved.
            ulong seed = ulong.TryParse(oneOff, out var bs) ? bs : 1;
            _home = () => new Tactics.BattleScreen { Seed = seed, AutoPlay = _autoPlay, DemoAim = args.ContainsKey("aim"), DemoFx = args.ContainsKey("fxdemo"), DemoShiny = args.ContainsKey("shiny"), TurnSeconds = args.TryGetValue("timer", out var timerArg) && float.TryParse(timerArg, out var timerSeconds) ? timerSeconds : 0, MapName = args.TryGetValue("map", out var mapName) ? mapName : "Undercity Foundry" };
            Show(_home());
            return;
        }
        // The game proper: the ranch, battles from it, and back.
        if (args.TryGetValue("save", out var saveName) && saveName != "") Ranching.Ranch.SavePath = $"user://{saveName}.json";
        _ranch = args.ContainsKey("newranch") ? Ranching.Ranch.New() : Ranching.Ranch.Load();
        bool vatDemo = args.ContainsKey("vatdemo");
        _home = () => new Ranching.RanchScreen { Ranch = _ranch, Fight = StartRanchBattle, VatDemo = vatDemo, AutoFight = _autoPlay, StartInLab = args.ContainsKey("labmode"), StartOnParty = args.ContainsKey("partymode"), StartOnBoard = args.ContainsKey("boardmode"), StartOnJobs = args.ContainsKey("jobsmode") };
        Show(_home());
    }

    System.Func<Node> _lab, _home;
    Ranching.Ranch _ranch;
    bool _autoPlay;
    Node _screen;
    int _shotFrames = -1, _recordFrames, _recorded;

    void Show(Node screen)
    {
        if (_autoPlay) GD.Print($"screen: {screen.GetType().Name}"); // trace the attract loop
        _screen?.QueueFree();
        _screen = screen;
        AddChild(screen);
    }

    void StartRanchBattle(Tactics.Mission mission)
    {
        Show(new Tactics.BattleScreen
        {
            Seed = mission.Seed, Mission = mission, Squad = _ranch.SquadCreatures(), AutoPlay = _autoPlay, Prepare = _ranch.LeadUnits,
            Finished = battle =>
            {
                var joined = _ranch.AfterBattle(battle, mission);
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
        // --record=K: then save the next K frames' middle (960x540, full size) to .shots/rec/ (run with --fixed-fps 30 for real time).
        if (_shotFrames < 0 || --_shotFrames > 0) return;
        string dir = ProjectSettings.GlobalizePath("res://.shots");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var frame = GetViewport().GetTexture().GetImage();
        if (_recorded < _recordFrames)
        {
            DirAccess.MakeDirRecursiveAbsolute($"{dir}/rec");
            frame = frame.GetRegion(new Rect2I(frame.GetWidth() / 4, frame.GetHeight() / 4, frame.GetWidth() / 2, frame.GetHeight() / 2));
            frame.SavePng($"{dir}/rec/f{_recorded++:000}.png");
            _shotFrames = 1;
            return;
        }
        frame.SavePng($"{dir}/shot.png");
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
