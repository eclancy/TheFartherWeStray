using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;
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
    public PaletteSpec Spec;
    public bool Shiny;
    public string Mutation;
    public float BodyJitter = 1, LegJitter = 1, HeadJitter = 1;
    public int Generation;
    public string ParentA, ParentB;
    public Training Training = new();

    public static CreatureRecord From(Creature c, int id) => new()
    {
        Id = id, Species = c.Species, Seed = c.Seed, Size = c.Size, Attitude = c.Attitude, Parts = c.Parts, Spec = c.Spec,
        Shiny = c.Shiny, Mutation = c.Mutation, BodyJitter = c.BodyJitter, LegJitter = c.LegJitter, HeadJitter = c.HeadJitter,
        Generation = c.Generation, ParentA = c.ParentA, ParentB = c.ParentB,
    };

    public Creature ToCreature() => new()
    {
        Species = Species, Seed = Seed, Size = Size, Attitude = Attitude, Parts = Parts, Spec = Spec,
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
    public const int SquadSize = 4;
    public static string SavePath = "user://ranch.json";

    public List<CreatureRecord> Creatures = new();
    public List<int> Squad = new();
    public ulong Encounter = 1;
    public int NextId = 1;
    public int Bred;

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
            if (ranch != null && ranch.Creatures.Count > 0) return ranch;
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
            r.Squad.Add(id);
        }
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

    public void Release(int id)
    {
        Creatures.RemoveAll(r => r.Id == id);
        Squad.Remove(id);
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
    public static string DefaultClass(Creature c) => c.Species.Strain switch
    {
        Strain.Arcane => "Mystic",
        Strain.Chemtech => "Alchemist",
        Strain.Clockwork => "Tinker",
        _ => c.Species.Plan is BodyPlan.Avian or BodyPlan.Insectoid ? "Skirmisher"
            : c.Armor is Armor.Shell or Armor.Crystal || c.Species.Plan == BodyPlan.Blob ? "Guardian" : "Brawler",
    };

    static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => n.ToString() };

    /// <summary>What happened after the last battle (JP and skills learned), for the ranch screen.</summary>
    [JsonIgnore] public List<string> Report = new();

    /// <summary>
    /// After a battle: the squad earns JP for its classes (more if it was still standing) and learns
    /// skills, recruits move in keeping their wild skill, everyone is rested, the next encounter is lined up.
    /// </summary>
    public List<int> AfterBattle(Battle battle)
    {
        Report.Clear();
        foreach (var u in battle.Units.Where(u => u.Training != null))
        {
            int jp = 12 + (u.Gone ? 0 : 4) + (battle.Winner == Tactics.Side.Player ? 4 : 0);
            foreach (var skill in u.Training.Earn(jp))
                Report.Add($"{u.Name} learned {skill.Name} ({skill.Class}).");
        }
        var joined = new List<int>();
        foreach (var u in battle.Recruited)
        {
            int id = Add(u.Creature);
            Record(id).Training = new Training { Class = DefaultClass(u.Creature), Innate = ClassBook.WildSkill(u.Creature) };
            joined.Add(id);
        }
        foreach (var r in Creatures) r.Rested = true;
        Encounter++;
        Save();
        return joined;
    }
}
