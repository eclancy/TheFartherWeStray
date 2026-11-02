using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
using CreatureTactics.Party;
using CreatureTactics.Tactics;
using Godot;

namespace CreatureTactics.Ranching;

/// <summary>One creature you own, as saved: its whole species (bred species exist nowhere else) and the individual.</summary>
public sealed class CreatureRecord
{
    public int Id;
    public bool Rested = true; // can breed; a battle rests everyone
    public Species Species;
    public ulong Seed;
    public SizeClass Size;
    public Attitude Attitude;
    public PartSet Parts;
    public PartSet? BirthParts; // set once the lab grafts a part: what breeding passes on
    public PaletteSpec Spec;
    public bool Shiny;
    public string Mutation;
    public float BodyJitter = 1, LegJitter = 1, HeadJitter = 1;
    public int Generation;
    public string ParentA, ParentB;
    public Training Training = new();

    public static CreatureRecord From(Creature c, int id) => new()
    {
        Id = id, Species = c.Species, Seed = c.Seed, Size = c.Size, Attitude = c.Attitude, Parts = c.Parts, Spec = c.Spec, BirthParts = c.BirthParts,
        Shiny = c.Shiny, Mutation = c.Mutation, BodyJitter = c.BodyJitter, LegJitter = c.LegJitter, HeadJitter = c.HeadJitter,
        Generation = c.Generation, ParentA = c.ParentA, ParentB = c.ParentB,
    };

    public Creature ToCreature() => new()
    {
        Species = Species, Seed = Seed, Size = Size, Attitude = Attitude, Parts = Parts, Spec = Spec, BirthParts = BirthParts,
        Palette = Creature.BuildPalette(Spec, Species), Shiny = Shiny, Mutation = Mutation,
        BodyJitter = BodyJitter, LegJitter = LegJitter, HeadJitter = HeadJitter,
        Generation = Generation, ParentA = ParentA, ParentB = ParentB,
    };
}

/// <summary>
/// The ranch: every creature you own, the squad you take into battle, and the save file. Wild
/// creatures you recruit come here; bred ones are born here (game-ideas.md, the game loop).
/// </summary>
public sealed class Ranch
{
    public static string SavePath = "user://ranch.json";

    public List<CreatureRecord> Creatures = new();
    public List<int> Squad = new();
    // The tamers going into the next battle (at most two), and which of them leads each squad creature.
    public List<string> Fielded = new() { TamerBook.You };
    public Dictionary<int, string> LedBy = new();
    /// <summary>Three creatures for each tamer in the field.</summary>
    [JsonIgnore] public int SquadSize => TamerBook.CreaturesEach * Fielded.Count;
    public ulong Encounter = 1;
    public int NextId = 1;
    public int Bred;
    public int Chem;                              // lab currency, earned in battle
    public HashSet<string> Codex = new();          // parts met in battle, graftable at the lab ("Wings.Bat")

    readonly Dictionary<int, Creature> _cache = new();

    static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Ranch Load()
    {
        if (!FileAccess.FileExists(SavePath)) return New();
        try
        {
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            var ranch = JsonSerializer.Deserialize<Ranch>(f.GetAsText(), Json);
            if (ranch != null && ranch.Creatures.Count > 0) {
                // Saves from before the lab: fill the codex from the roster.
                if (ranch.Codex.Count == 0) foreach (var r in ranch.Creatures) Lab.Learn(ranch, ranch.Get(r.Id));
                ranch.Tidy();
                return ranch;
            }
        }
        catch (JsonException e)
        {
            GD.PushWarning($"ranch save unreadable, starting fresh: {e.Message}");
        }
        return New();
    }

    /// <summary>A fresh ranch with four starters from the DrawPets cast.</summary>
    public static Ranch New()
    {
        var r = new Ranch();
        foreach (var (c, t) in Tactics.Encounter.StarterSquad())
        {
            int id = r.Add(c);
            r.Record(id).Training = t;
            r.AddToSquad(id);
        }
        foreach (var rec in r.Creatures) Lab.Learn(r, r.Get(rec.Id));
        return r;
    }

    public void Save()
    {
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(this, Json));
    }

    public int Add(Creature c, bool rested = true)
    {
        int id = NextId++;
        var record = CreatureRecord.From(c, id);
        record.Rested = rested;
        Creatures.Add(record);
        _cache[id] = c;
        return id;
    }

    public CreatureRecord Record(int id) => Creatures.First(r => r.Id == id);

    public Creature Get(int id)
    {
        if (!_cache.TryGetValue(id, out var c)) _cache[id] = c = Record(id).ToCreature();
        return c;
    }

    public List<(Creature, Training)> SquadCreatures() => Squad.Select(id => (Get(id), Record(id).Training)).ToList();

    // ------------------------------------------------------------------ tamers

    [JsonIgnore] public IEnumerable<TamerDef> Party => TamerBook.Joined(Encounter);
    public TamerDef LeaderOf(int id) => LedBy.TryGetValue(id, out var w) ? TamerBook.Get(w) : null;
    public int Led(string tamer) => Squad.Count(id => LeaderOf(id)?.Id == tamer);

    /// <summary>Into the squad under the first fielded tamer with room; false if every tamer's hands are full.</summary>
    public bool AddToSquad(int id, string tamer = null)
    {
        if (Squad.Contains(id)) RemoveFromSquad(id);
        tamer ??= Fielded.FirstOrDefault(w => Led(w) < TamerBook.CreaturesEach);
        if (tamer == null || !Fielded.Contains(tamer) || Led(tamer) >= TamerBook.CreaturesEach) return false;
        Squad.Add(id);
        LedBy[id] = tamer;
        return true;
    }

    public void RemoveFromSquad(int id)
    {
        Squad.Remove(id);
        LedBy.Remove(id);
    }

    /// <summary>Sends a tamer into the field, or stands them down (their creatures leave the squad with them).</summary>
    public bool ToggleField(string tamer)
    {
        if (Fielded.Contains(tamer))
        {
            if (Fielded.Count == 1) return false; // someone has to lead
            Fielded.Remove(tamer);
            foreach (var id in Squad.Where(id => LedBy.GetValueOrDefault(id) == tamer).ToList()) RemoveFromSquad(id);
            return true;
        }
        if (Fielded.Count >= TamerBook.MaxFielded || !Party.Any(w => w.Id == tamer)) return false;
        Fielded.Add(tamer);
        return true;
    }

    /// <summary>
    /// Keeps the party consistent: only tamers who've joined are fielded, a newcomer takes the field
    /// if there's room, and every squad creature has a fielded leader with room (older saves had a
    /// squad of four and no tamers).
    /// </summary>
    public void Tidy()
    {
        var joined = Party.Select(w => w.Id).ToHashSet();
        Fielded.RemoveAll(w => !joined.Contains(w));
        foreach (var w in Party)
            if (Fielded.Count < TamerBook.MaxFielded && !Fielded.Contains(w.Id)) Fielded.Add(w.Id);
        if (Fielded.Count == 0) Fielded.Add(TamerBook.You);
        var squad = Squad.ToList();
        var leaders = squad.ToDictionary(id => id, id => LedBy.GetValueOrDefault(id));
        Squad.Clear();
        LedBy.Clear();
        foreach (var id in squad)
            if (!(leaders[id] is { } w && Fielded.Contains(w) && AddToSquad(id, w))) AddToSquad(id);
    }

    /// <summary>Puts each squad creature in a battle under its tamer.</summary>
    public void LeadUnits(Battle battle)
    {
        foreach (var id in Squad) battle.Lead(Get(id), LeaderOf(id));
    }

    /// <summary>Rebuilds a creature after the lab changes its record.</summary>
    public void Refresh(int id) => _cache.Remove(id);

    public void Release(int id)
    {
        Chem += 5; // salvage
        Creatures.RemoveAll(r => r.Id == id);
        RemoveFromSquad(id);
        _cache.Remove(id);
    }

    /// <summary>Breeds two rested creatures. The child arrives tired, like its parents.</summary>
    public (int id, Breeding.Result result) Breed(int a, int b)
    {
        var result = Breeding.Breed(Get(a), Get(b), SeededRng.Derive(Encounter * 7919UL + (ulong)Bred, (ulong)(a * 131 + b)));
        Bred++;
        Record(a).Rested = Record(b).Rested = false;
        // Siblings of the same pair splice to the same name, so later ones get numbered.
        string name = result.Child.Species.Name;
        for (int n = 2; Creatures.Any(r => r.Species.Name == result.Child.Species.Name); n++)
            result.Child.Species.Name = $"{name} {Roman(n)}";
        int id = Add(result.Child, rested: false);

        // The child is born knowing one skill from a parent (the notes' MVP: "a part mix plus 1 skill").
        var known = Record(a).Training.Skills.Concat(Record(b).Training.Skills)
            .Append(Record(a).Training.Innate).Append(Record(b).Training.Innate).Where(s => s != null).Distinct().ToList();
        var training = new Training { Class = DefaultClass(result.Child) };
        if (known.Count > 0)
        {
            var rng = new SeededRng(SeededRng.Derive(result.Child.Seed, 0x5C11));
            training.Innate = known[rng.Int(known.Count)];
            result.Notes.Add($"born knowing {ClassBook.Skill(training.Innate).Name}");
        }
        Record(id).Training = training;
        return (id, result);
    }

    /// <summary>A sensible first class for a newcomer, from what it is.</summary>
    public static string DefaultClass(Creature c)
    {
        string pick = c.Species.Strain switch
        {
            Strain.Arcane => "Mystic",
            Strain.Chemtech => "Alchemist",
            Strain.Clockwork => "Tinker",
            _ => c.Armor is Armor.Shell or Armor.Crystal ? "Guardian" : ClassBook.Affinity[c.Species.Plan].favoured[0],
        };
        // Never a class the body bars or can't open yet (a tinker needs hands): fall back to its first favoured class.
        var t = new Training();
        return ClassBook.StandingOf(ClassBook.Class(pick), c, t) == ClassBook.Standing.Open ? pick : ClassBook.Affinity[c.Species.Plan].favoured[0];
    }

    static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => n.ToString() };

    /// <summary>What happened after the last battle (JP and skills learned), for the ranch screen.</summary>
    [JsonIgnore] public List<string> Report = new();

    /// <summary>
    /// After a battle: the squad earns JP for its classes (more if it was still standing) and learns
    /// skills, recruits move in keeping their wild skill, everyone is rested, the next encounter is lined up.
    /// </summary>
    public List<int> AfterBattle(Battle battle, Mission mission = null)
    {
        Report.Clear();
        if (mission != null)
        {
            bool won = battle.Winner == Tactics.Side.Player;
            if (won) Chem += mission.Reward;
            Report.Add(won ? $"Mission complete: {mission.Title}. +{mission.Reward} chem." : $"Mission failed: {mission.Title}.");
        }
        foreach (var u in battle.Units.Where(u => u.Training != null))
        {
            // A favoured class (body-plan affinity) trains half again as fast.
            int jp = 12 + (u.Gone ? 0 : 4) + (battle.Winner == Tactics.Side.Player ? 4 : 0);
            jp = (int)(jp * ClassBook.JpRate(u.Creature, u.Training.Class));
            foreach (var name in u.Training.Earn(jp))
                Report.Add($"{u.Name} learned {name} ({u.Training.Class}).");
            if (ClassBook.Passive(u.Training.Passive) is { ChemBonus: > 0 } pick) { Chem += pick.ChemBonus; Report.Add($"{u.Name} pocketed {pick.ChemBonus} chem."); }
        }
        // Chem for the lab: more for a win and for every wild creature driven off. Their parts enter the codex.
        int fled = battle.Units.Count(u => u.Training == null && u.Gone);
        int chem = (battle.Winner == Tactics.Side.Player ? 8 : 2) + 2 * fled;
        Chem += chem;
        Report.Add($"+{chem} chem (now {Chem}).");
        var before = new HashSet<string>(Codex);
        foreach (var u in battle.Units.Where(u => u.Training == null)) Lab.Learn(this, u.Creature);
        var found = Codex.Except(before).Select(k => k.Replace(".", ": ")).ToList();
        if (found.Count > 0) Report.Add($"New in the codex: {string.Join(", ", found)}.");

        var joined = new List<int>();
        foreach (var u in battle.Recruited)
        {
            int id = Add(u.Creature);
            Record(id).Training = new Training { Class = DefaultClass(u.Creature), Innate = ClassBook.WildSkill(u.Creature) };
            joined.Add(id);
        }
        foreach (var r in Creatures) r.Rested = true;
        var party = Party.Select(w => w.Id).ToHashSet();
        Encounter++;
        foreach (var w in Party.Where(w => !party.Contains(w.Id)))
            Report.Add($"{w.Name} joins your party: {w.Specialty}. {w.Text}");
        Tidy();
        Save();
        return joined;
    }
}
