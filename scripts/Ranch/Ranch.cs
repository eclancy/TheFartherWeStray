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

/// <summary>A creature away on a dispatch: the job, the day it's back, its odds of doing well.</summary>
public sealed class AwayRecord
{
    public string Job;
    public int BackOn, Chance;
}

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
    public string Nickname;   // the name you gave it (null: its species' name)
    public List<string> Traits;           // born with, passed down (null in saves from before traits: rolled on load)
    public List<string> Augments = new(); // bolted on at the lab, not passed down
    [JsonIgnore] public string Name => Nickname ?? Species.Name;
    public float BodyJitter = 1, LegJitter = 1, HeadJitter = 1;
    public int Generation;
    public string ParentA, ParentB;
    public Training Training = new();

    public static CreatureRecord From(Creature c, int id) => new()
    {
        Id = id, Species = c.Species, Seed = c.Seed, Size = c.Size, Attitude = c.Attitude, Parts = c.Parts, Spec = c.Spec, BirthParts = c.BirthParts,
        Shiny = c.Shiny, Mutation = c.Mutation, BodyJitter = c.BodyJitter, LegJitter = c.LegJitter, HeadJitter = c.HeadJitter,
        Generation = c.Generation, ParentA = c.ParentA, ParentB = c.ParentB, Nickname = c.Nickname,
        Traits = new List<string>(c.Traits), Augments = new List<string>(c.Augments),
    };

    /// <summary>The creature itself; its augments bolted on (their grafts on a copy of its species) unless <paramref name="augmented"/> is false, as for breeding.</summary>
    public Creature ToCreature(bool augmented = true)
    {
        var species = Species;
        if (augmented && Augments.Count > 0)
        {
            species = Species.Clone();
            foreach (var a in Augments) if (Classes.AugmentBook.Get(a) is { } aug) species.Grafts |= aug.Graft;
        }
        return new Creature
        {
            Species = species, Seed = Seed, Size = Size, Attitude = Attitude, Parts = Parts, Spec = Spec, BirthParts = BirthParts,
            Palette = Creature.BuildPalette(Spec, species), Shiny = Shiny, Mutation = Mutation, Nickname = Nickname,
            BodyJitter = BodyJitter, LegJitter = LegJitter, HeadJitter = HeadJitter,
            Generation = Generation, ParentA = ParentA, ParentB = ParentB,
            Traits = new List<string>(Traits ?? new List<string>()), Augments = augmented ? new List<string>(Augments) : new List<string>(),
        };
    }
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
    public Dictionary<string, int> Rivals = new();  // times you've beaten each rival tamer (their squads grow)
    // The country: where your party is, the day, contracts won (a story milestone), main missions won.
    public string Location = World.Country.Home;
    public int Day = 1;
    public int ContractsWon;
    public HashSet<string> Story = new();
    public Dictionary<int, AwayRecord> Away = new(); // creatures off on dispatches
    public HashSet<string> Cups = new();             // coliseum cups won
    public List<World.Wanderer> Wanderers = new();   // wild packs roaming the country
    public Dictionary<string, int> Items = new();    // the bag: gear not worn, items, extra glowberries
    public int NextWanderer = 1;

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
        World.Roaming.Spawn(r, new SeededRng(0x60A3)); // a pack is out there from the start
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
        if (Away.ContainsKey(id)) return false;
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
        // Saves from before traits: every creature gets the ones its seed was born with.
        foreach (var rec in Creatures.Where(rec => rec.Traits == null)) { rec.Traits = Classes.TraitBook.Roll(rec.Seed); _cache.Remove(rec.Id); }
        if (World.Country.Get(Location) is not { } here || !World.Country.IsOpen(this, here)) Location = World.Country.Home;
        Wanderers.RemoveAll(w => !World.Roaming.Roams(this, w.At));
        // The first pack is out there from the start (old saves get one too).
        if (NextWanderer == 1) World.Roaming.Spawn(this, new SeededRng(0x60A3));
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
        battle.Pack(Items);
    }

    /// <summary>Buys a ware at the shop where you stand. Returns what happened.</summary>
    public string Buy(World.Ware ware)
    {
        if (!ware.At.Contains(Location)) return $"{World.ShopBook.Name(ware.Id)} is sold at {string.Join(" or ", ware.At.Select(a => World.Country.Get(a).Name))}.";
        if (Chem < ware.Price) return $"Not enough chem: {World.ShopBook.Name(ware.Id)} costs {ware.Price}.";
        Chem -= ware.Price;
        Items[ware.Id] = Items.GetValueOrDefault(ware.Id) + 1;
        return $"Bought {World.ShopBook.Name(ware.Id)} for {ware.Price} chem ({Items[ware.Id]} in the bag).";
    }

    /// <summary>Puts gear from the bag on a creature (null: takes it off); what it wore goes back in the bag.</summary>
    public string Equip(int id, string gear)
    {
        var t = Record(id).Training;
        if (gear != null && Items.GetValueOrDefault(gear) <= 0) return "There's none in the bag.";
        if (t.Gear != null) Items[t.Gear] = Items.GetValueOrDefault(t.Gear) + 1;
        if (gear != null && --Items[gear] == 0) Items.Remove(gear);
        t.Gear = gear;
        return gear == null ? $"{Record(id).Name} takes its gear off." : $"{Record(id).Name} wears the {ClassBook.Passive(gear).Name}: {ClassBook.Passive(gear).Text}";
    }

    /// <summary>Bolts an augment from the bag onto a creature at the lab; a third scraps the oldest.</summary>
    public string Install(int id, string augment)
    {
        var rec = Record(id);
        var aug = Classes.AugmentBook.Get(augment);
        if (aug == null || Items.GetValueOrDefault(augment) <= 0) return "There's none in the bag: undercity shops sell augments.";
        string an = "AEIOU".Contains(aug.Name[0]) ? "an" : "a";
        if (rec.Augments.Contains(augment)) return $"{rec.Name} already has {an} {aug.Name}.";
        string scrapped = null;
        if (rec.Augments.Count >= Classes.AugmentBook.Slots) { scrapped = Classes.AugmentBook.Get(rec.Augments[0])?.Name; rec.Augments.RemoveAt(0); }
        rec.Augments.Add(augment);
        if (--Items[augment] == 0) Items.Remove(augment);
        Refresh(id);
        var skill = Classes.ClassBook.Skill(aug.Skill);
        return $"{rec.Name} has {an} {aug.Name} bolted on: it can use {skill.Name}.{(scrapped != null ? $" The {scrapped} is scrapped." : "")}";
    }

    /// <summary>Sends a creature off on a dispatch (out of the squad, if it was in it).</summary>
    public string Send(int id, World.DispatchDef job)
    {
        int chance = System.Math.Min(96, World.DispatchBook.Chance(job, Get(id), Record(id).Training));
        RemoveFromSquad(id);
        Away[id] = new AwayRecord { Job = job.Id, BackOn = Day + job.Days, Chance = chance };
        return $"{Record(id).Name} sets off on \"{job.Title}\": back in {job.Days} {(job.Days == 1 ? "day" : "days")}, {chance}% to go well.";
    }

    /// <summary>Everyone back from a dispatch by today: chem and training if it went well, a story either way.</summary>
    public List<string> ReturnDispatches()
    {
        var lines = new List<string>();
        foreach (var (id, away) in Away.Where(kv => kv.Value.BackOn <= Day).OrderBy(kv => kv.Key).ToList())
        {
            Away.Remove(id);
            if (World.DispatchBook.Get(away.Job) is not { } job || !Creatures.Any(r => r.Id == id)) continue;
            var rng = new SeededRng(SeededRng.Derive(SeededRng.Hash(job.Id) ^ (ulong)id, (ulong)away.BackOn));
            bool well = rng.Int(100) < away.Chance;
            var training = Record(id).Training;
            training.Earn(well ? 25 : 10);
            if (well) Chem += job.Reward;
            lines.Add($"{Record(id).Name} is back from \"{job.Title}\": {(well ? job.Success : job.Failure)}{(well ? $" +{job.Reward} chem." : "")}");
        }
        return lines;
    }

    /// <summary>One road's travel: a day passes (packs roam) and you arrive. Returns the pack waiting there, if any.</summary>
    public World.Wanderer TravelStep(string next)
    {
        Day++;
        World.Roaming.PassDay(this);
        Location = next;
        return Wanderers.FirstOrDefault(w => w.At == next);
    }

    /// <summary>Travels to a place along open roads, a day a road; stops early where a roaming pack waits, and returns it.</summary>
    public World.Wanderer TravelTo(string location)
    {
        var route = World.Country.Route(this, Location, location);
        if (route == null) { Location = location; return null; }
        foreach (var stop in route.Skip(1))
            if (TravelStep(stop) is { } met) return met;
        return null;
    }

    /// <summary>Gives a creature a name (blank: back to its species' name), up to 14 letters.</summary>
    public string Rename(int id, string name)
    {
        name = new string((name ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (name.Length > 14) name = name[..14].TrimEnd();
        var rec = Record(id);
        string old = rec.Name;
        rec.Nickname = name.Length == 0 || name == rec.Species.Name ? null : name;
        Refresh(id);
        return rec.Nickname == null ? $"{old} goes by {rec.Species.Name} again." : $"{old} is now called {rec.Nickname}.";
    }

    /// <summary>Rebuilds a creature after the lab changes its record.</summary>
    public void Refresh(int id) => _cache.Remove(id);

    public void Release(int id)
    {
        Away.Remove(id);
        if (Record(id).Training.Gear is { } worn) Items[worn] = Items.GetValueOrDefault(worn) + 1; // its gear stays
        Chem += 5; // salvage
        Creatures.RemoveAll(r => r.Id == id);
        RemoveFromSquad(id);
        _cache.Remove(id);
    }

    /// <summary>Breeds two rested creatures. The child arrives tired, like its parents.</summary>
    public (int id, Breeding.Result result) Breed(int a, int b)
    {
        // Parents as they were born to it: augments are bolted on, not blood.
        var result = Breeding.Breed(Record(a).ToCreature(augmented: false), Record(b).ToCreature(augmented: false), SeededRng.Derive(Encounter * 7919UL + (ulong)Bred, (ulong)(a * 131 + b)));
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
            if (won && mission.Story is { } story)
            {
                Story.Add(story);
                var opened = World.Country.Get(World.StoryBook.Get(story).Opens);
                Report.Add($"Main mission won: the road to {opened.Name} is open.");
            }
            else if (won && mission.Cup == null && mission.Roamer == 0) ContractsWon++;
            if (mission.Cup is { } cupId)
            {
                var cup = World.CupBook.Get(cupId);
                if (won && mission.Round == 3) { Cups.Add(cupId); Report.Add($"Champion of the {cup.Name}!"); }
                else if (!won) Report.Add($"Out of the {cup.Name} in round {mission.Round}.");
            }
            else if (mission.Rival is { } rival)
            {
                int wins = Rivals.GetValueOrDefault(rival.Id) + (won ? 1 : 0);
                if (won) Rivals[rival.Id] = wins;
                Report.Add(won ? $"{rival.Name} is beaten ({wins} {(wins == 1 ? "win" : "wins")}); they'll train harder for next time."
                    : $"{rival.Name} wins this time.");
            }
        }
        // Items used come out of the bag; glowberries only past the three every battle brings.
        foreach (var (item, n) in battle.Used)
            if ((Items[item] = Items.GetValueOrDefault(item) - n) <= 0) Items.Remove(item);
        int berriesUsed = 3 + battle.BerriesBrought - battle.Berries;
        if (battle.BerriesBrought > 0 && berriesUsed > 3 && (Items[World.ShopBook.Berry] -= berriesUsed - 3) <= 0) Items.Remove(World.ShopBook.Berry);
        // Only your own creatures train here (a rival's are rebuilt for every duel).
        foreach (var u in battle.Units.Where(u => u.Training != null && u.Side == Tactics.Side.Player))
        {
            // A favoured class (body-plan affinity) trains half again as fast.
            int jp = 12 + (u.Gone ? 0 : 4) + (battle.Winner == Tactics.Side.Player ? 4 : 0);
            jp = (int)(jp * ClassBook.JpRate(u.Creature, u.Training.Class));
            foreach (var name in u.Training.Earn(jp))
                Report.Add($"{u.Name} learned {name} ({u.Training.Class}).");
            if (u.Passive is { ChemBonus: > 0 } pick) { Chem += pick.ChemBonus; Report.Add($"{u.Name} pocketed {pick.ChemBonus} chem."); }
        }
        // Chem for the lab: more for a win and for every wild or rival creature driven off. Their parts enter the codex.
        bool Foe(Unit u) => u.Training == null || u.Side == Tactics.Side.Wild;
        int fled = battle.Units.Count(u => Foe(u) && u.Gone);
        int chem = (battle.Winner == Tactics.Side.Player ? 8 : 2) + 2 * fled;
        Chem += chem;
        Report.Add($"+{chem} chem (now {Chem}).");
        var before = new HashSet<string>(Codex);
        foreach (var u in battle.Units.Where(Foe)) Lab.Learn(this, u.Creature);
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
        // A beaten pack is gone. A battle takes a day: packs roam on, whoever's dispatch is done comes home.
        if (battle.Winner == Tactics.Side.Player && mission?.Roamer is > 0 and var roamer) Wanderers.RemoveAll(w => w.Id == roamer);
        Day++;
        World.Roaming.PassDay(this);
        Report.AddRange(ReturnDispatches());
        var party = Party.Select(w => w.Id).ToHashSet();
        Encounter++;
        foreach (var w in Party.Where(w => !party.Contains(w.Id)))
            Report.Add($"{w.Name} joins your party: {w.Specialty}. {w.Text}");
        Tidy();
        Save();
        return joined;
    }
}
