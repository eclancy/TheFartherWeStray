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
    public List<string> Infusions = new(); // brewed at the lab to tune its stats, not passed down
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
        Traits = new List<string>(c.Traits), Augments = new List<string>(c.Augments), Infusions = new List<string>(c.Infusions),
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
            Infusions = augmented ? new List<string>(Infusions ?? new List<string>()) : new List<string>(),
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
    public HashSet<Form> Forms = new();            // kinds of creature met, whose abilities the lab can splice in
    public Dictionary<string, int> Rivals = new();  // times you've beaten each rival tamer (their squads grow)
    public Dictionary<string, int> RivalLosses = new();  // duels lost to each in a row since (they bring fewer)
    public HashSet<string> Titles = new();         // titles won from rivals
    // The country: where your party is, the day, contracts won (a story milestone), main missions won.
    public string Location = World.Country.Home;
    public int Day = 1;
    public int ContractsWon;
    public HashSet<string> Story = new();
    public Dictionary<int, AwayRecord> Away = new(); // creatures off on dispatches
    public HashSet<string> Cups = new();             // coliseum cups won
    public List<World.Wanderer> Wanderers = new();   // wild packs roaming the country
    public Dictionary<string, int> Items = new();    // the bag: gear not worn, items, extra glowberries
    // The story so far: standing with the two sides, flags set by choices and catches, a choice waiting, the ending reached.
    public int CityStanding, UnderStanding;
    public HashSet<string> Flags = new();
    public string Pending;   // a main mission whose choice is still to be made
    public string Ending;
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
    /// <summary>Set once a save has been brought up to tiers (new ranches start with it).</summary>
    public const string TiersFlag = "save:tiers";

    public static Ranch New()
    {
        var r = new Ranch();
        r.Flags.Add(TiersFlag);
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

    // ------------------------------------------------------------------ save slots

    public const int Slots = 3;
    /// <summary>Where the slots live: slot 1 is the save from before slots (ranch.json), the rest ranch2, ranch3.</summary>
    public static string SlotPrefix = "user://ranch";

    public static string SlotPath(int slot) => slot == 1 ? $"{SlotPrefix}.json" : $"{SlotPrefix}{slot}.json";
    public static bool SlotUsed(int slot) => FileAccess.FileExists(SlotPath(slot));

    /// <summary>The ranch in a slot, read without making it the one being played.</summary>
    public static Ranch Peek(int slot)
    {
        if (!SlotUsed(slot)) return null;
        var keep = SavePath;
        SavePath = SlotPath(slot);
        try { return Load(); }
        finally { SavePath = keep; }
    }

    /// <summary>When a slot was last saved (local time), or null if it's empty.</summary>
    public static System.DateTime? SlotSaved(int slot) =>
        SlotUsed(slot) ? System.DateTimeOffset.FromUnixTimeSeconds((long)FileAccess.GetModifiedTime(SlotPath(slot))).LocalDateTime : null;

    static string Plural(int n, string what) => $"{n} {what}{(n == 1 ? "" : "s")}";

    public static void DeleteSlot(int slot)
    {
        if (SlotUsed(slot)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SlotPath(slot)));
    }

    /// <summary>A slot in a line: where you are, the day, the roster, the story so far.</summary>
    public string Summary() =>
        $"{World.Country.Get(Location)?.Name ?? "The Ranch"}  ·  day {Day}  ·  {Creatures.Count} creatures  ·  {Chem} chem  ·  {Plural(Story.Count(m => World.StoryBook.Get(m)?.Ending == null), "main mission")}"
        + (Ending != null ? $"  ·  ending: {World.StoryBook.Get(Ending)?.Ending}" : "");

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
        // Saves from before tiers: a creature caught wild whose blood isn't the one its species is born
        // with was infused at the lab. (Ones bred before tiers can't be told apart: they stay as they are.)
        foreach (var rec in Creatures) Forms.Add(Species.FormOf(rec.Species));
        if (Flags.Add(TiersFlag))
            foreach (var rec in Creatures.Where(rec => rec.Generation == 0 && rec.Species.Origin == null && !rec.Species.Infused))
            {
                var wild = Species.Generate(rec.Species.Seed);
                if (wild.Name != rec.Species.Name || wild.Strain == rec.Species.Strain) continue;
                rec.Species = rec.Species.Clone();
                rec.Species.Infused = true;
                _cache.Remove(rec.Id);
            }
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

    /// <summary>Buys a ware at the shop where you stand (cheaper where you're well thought of). Returns what happened.</summary>
    public string Buy(World.Ware ware)
    {
        if (!ware.At.Contains(Location)) return $"{World.ShopBook.Name(ware.Id)} is sold at {string.Join(" or ", ware.At.Select(a => World.Country.Get(a).Name))}.";
        int price = World.ShopBook.PriceAt(this, ware, Location);
        if (Chem < price) return $"Not enough chem: {World.ShopBook.Name(ware.Id)} costs {price}.";
        Chem -= price;
        Items[ware.Id] = Items.GetValueOrDefault(ware.Id) + 1;
        return $"Bought {World.ShopBook.Name(ware.Id)} for {price} chem ({Items[ware.Id]} in the bag).";
    }

    /// <summary>Makes the choice waiting after a main mission: standing shifts, and a flag remembers it.</summary>
    public string Decide(string choice)
    {
        if (World.StoryBook.Get(Pending) is not { } main) return "";
        var c = World.StoryBook.ChoicesAfter(main).First(x => x.Id == choice);
        Flags.Add($"{main.Id}:{c.Id}");
        Pending = null;
        return $"{c.Text} ({World.Standing.Shift(this, c.City, c.Under)})";
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
        var before = Classes.TierBook.Of(Get(id));
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
        // Undercity work: the undercity takes note.
        string moved = World.Standing.Shift(this, 0, World.Standing.Augment);
        return $"{rec.Name} has {an} {aug.Name} bolted on: it can use {skill.Name}.{(scrapped != null ? $" The {scrapped} is scrapped." : "")}{TierNote(id, before)} ({moved})";
    }

    /// <summary>A line for when work at the lab changes a creature's tier ("" if it doesn't).</summary>
    public string TierNote(int id, Classes.Tier before)
    {
        var now = Classes.TierBook.Of(Get(id));
        if (now == before) return "";
        return before == Classes.Tier.Thoroughbred ? $" It's {Classes.TierBook.Words(now)} now, no longer a thoroughbred." : $" It's {Classes.TierBook.Words(now)} now.";
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
            string moved = well && job.Location != null ? World.Standing.ForWork(this, job.Location, World.Standing.Dispatch) : "";
            lines.Add($"{Record(id).Name} is back from \"{job.Title}\": {(well ? job.Success : job.Failure)}{(well ? $" +{job.Reward} chem{(moved != "" ? $"; {moved}" : "")}." : "")}");
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
        // Breeding true is the city's way; crossing strains into a chimera is the undercity's.
        if (Classes.TierBook.Chimera(result.Child)) World.Standing.Shift(this, 0, World.Standing.Breeding);
        else World.Standing.Shift(this, World.Standing.Breeding, 0);
        Number(result.Child.Species);
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

    /// <summary>Siblings of the same pair splice to the same name, so later ones get numbered.</summary>
    void Number(Species species)
    {
        string name = species.Name;
        for (int n = 2; Creatures.Any(r => r.Species.Name == species.Name); n++) species.Name = $"{name} {Roman(n)}";
    }

    /// <summary>Takes a prize won from a rival: into the bag or the purse, a title, or an egg that hatches at the ranch. Returns the report line.</summary>
    public string Award(RivalDef rival, Prize prize, ulong seed)
    {
        string won = $"Prize from {rival.Name} ({RivalBook.Word(prize.Rarity)})";
        if (prize.Egg)
        {
            var c = RivalBook.Egg(rival, seed, prize.Shiny);
            Number(c.Species);
            int id = Add(c);
            Record(id).Training = new Training { Class = DefaultClass(c), Innate = ClassBook.WildSkill(c) };
            Lab.Learn(this, c);
            string traits = string.Join(" and ", c.Traits.Select(t => ClassBook.Passive(t).Name));
            string what = $"{c.Species.Strain.Label()} {Species.FormOf(c.Species)}".ToLowerInvariant();
            return $"{won}: an egg from their line hatches{(c.Shiny ? ", shiny" : "")}! {c.Name}, {("aeiou".Contains(what[0]) ? "an" : "a")} {what}, born with {traits}.";
        }
        if (prize.Title != null)
        {
            Titles.Add(prize.Title);
            return $"{won}: the title \"{prize.Title}\".";
        }
        if (prize.Chem > 0)
        {
            Chem += prize.Chem;
            return $"{won}: a purse of {prize.Chem} chem.";
        }
        Items[prize.Item] = Items.GetValueOrDefault(prize.Item) + prize.Count;
        return $"{won}: {(prize.Count > 1 ? $"{prize.Count} x " : "")}{World.ShopBook.Name(prize.Item)}, into the bag.";
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
            // Work for a side raises your standing with it.
            int work = mission.Story != null ? World.Standing.MainMission : mission.Repeatable ? World.Standing.Regular
                : mission.Cup == null && mission.Roamer == 0 && mission.Rival == null ? World.Standing.Contract : 0;
            string moved = won && mission.Location != null ? World.Standing.ForWork(this, mission.Location, work) : "";
            Report.Add(won ? $"Mission complete: {mission.Title}. +{mission.Reward} chem{(moved != "" ? $"; {moved}" : "")}." : $"Mission failed: {mission.Title}.");
            if (won && mission.Story is { } story)
            {
                Story.Add(story);
                var main = World.StoryBook.Get(story);
                if (main.Ending != null)
                {
                    Ending = story;
                    Report.Add($"Ending reached: {main.Ending}. (The story to tell it is still to come; you can play on.)");
                }
                else
                {
                    Report.Add($"Main mission won: the road to {World.Country.Get(main.Opens).Name} is open.");
                    Pending = story;
                }
            }
            else if (won && mission.Cup == null && mission.Roamer == 0 && mission.Rumour == null) ContractsWon++;
            if (mission.Rumour is { } rumour && battle.Target is { Side: Tactics.Side.Player })
            {
                Flags.Add($"caught:{rumour}");
                Report.Add($"The rumour was true: {battle.Target.Name} is yours.");
            }
            if (mission.Cup is { } cupId)
            {
                var cup = World.CupBook.Get(cupId);
                if (won && mission.Round == 3)
                {
                    Cups.Add(cupId);
                    Report.Add($"Champion of the {cup.Name}! {World.Standing.Shift(this, World.Standing.Cup, 0)}.");
                }
                else if (!won) Report.Add($"Out of the {cup.Name} in round {mission.Round}.");
            }
            else if (mission.Rival is { } rival)
            {
                int wins = Rivals.GetValueOrDefault(rival.Id) + (won ? 1 : 0);
                if (won) { Rivals[rival.Id] = wins; RivalLosses.Remove(rival.Id); }
                else RivalLosses[rival.Id] = RivalLosses.GetValueOrDefault(rival.Id) + 1;
                int fewer = RivalBook.Fewer(RivalLosses.GetValueOrDefault(rival.Id));
                Report.Add(won ? $"{rival.Name} is beaten ({wins} {(wins == 1 ? "win" : "wins")}); they'll train harder for next time."
                    : $"{rival.Name} wins this time, and grows cocky: they'll bring {(fewer == 1 ? "one creature" : "two creatures")} fewer next time.");
                // Every win draws a prize from their hoard, by rarity.
                ulong roll = SeededRng.Derive(SeededRng.Hash(rival.Id) ^ Encounter, (ulong)wins);
                if (won) Report.Add(Award(rival, RivalBook.Draw(rival, Titles, roll), roll));
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
