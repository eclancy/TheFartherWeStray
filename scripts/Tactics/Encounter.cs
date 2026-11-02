using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Classes;
using CreatureTactics.Creatures;

namespace CreatureTactics.Tactics;

/// <summary>Sets up a battle: your squad against a wild pack generated from a seed.</summary>
public static class Encounter
{
    /// <summary>The default squad when there is no ranch (--battle runs): four of the DrawPets cast.</summary>
    static readonly (string name, string cls)[] Squad = { ("Blaze", "Brawler"), ("Sprocket", "Skirmisher"), ("Tank", "Guardian"), ("Hoot", "Mystic") };

    public static IEnumerable<(Creature, Training)> StarterSquad() =>
        Squad.Select(s => (Creature.Specimen(DrawPetsCast.All.First(sp => sp.Name == s.name), 0), Training.Starter(s.cls)));

    public static Battle Create(ulong seed, IEnumerable<(Creature, Training)> squad = null)
    {
        squad ??= StarterSquad();
        var wild = new List<Creature>();
        for (int i = 0; i < 4; i++) wild.Add(Creature.Specimen(SeededRng.Derive(seed, 0xF00D + (ulong)i), 0));
        return new Battle(BattleMap.Foundry(), squad, wild, seed);
    }

    /// <summary>One line of text per event, for the battle log and headless runs.</summary>
    public static string Describe(BattleEvent e) => e switch
    {
        TurnEvent t => $"-- {t.U.Name}'s turn ({t.Side}, {t.Hp}/{t.U.MaxHp} HP)",
        MoveEvent m => $"{m.U.Name} moves to {m.Path[^1]}",
        StrikeEvent s => $"{s.A.Name} attacks {s.T.Name}",
        DamageEvent { Miss: true } d => $"  {d.U.Name} dodges",
        DamageEvent d => $"  {d.U.Name} takes {d.Amount}",
        PushEvent p => $"  {p.U.Name} is knocked back to {p.To}",
        FaintEvent f => $"  {f.U.Name} is out",
        RecruitEvent r => $"** {r.U.Name} is recruited: {r.Why}",
        HealEvent h => $"  {h.U.Name} heals {h.Amount}",
        BerryEvent { Placed: true } b => $"  a glowberry at {b.Tile}",
        BerryEvent b => $"  the glowberry at {b.Tile} is taken",
        LogEvent l => $"  {l.Text}",
        SkillEvent s => $"{s.U.Name} uses {s.Skill.Name}",
        StatusEvent st => $"  {st.U.Name}: {st.Text}",
        EndEvent end => end.Winner == Side.Player ? "== Victory" : "== Defeat",
        _ => e.ToString(),
    };
}
