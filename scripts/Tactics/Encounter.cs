using System.Collections.Generic;
using System.Linq;
using CreatureTactics.Creatures;

namespace CreatureTactics.Tactics;

/// <summary>Sets up a battle: your squad against a wild pack generated from a seed.</summary>
public static class Encounter
{
    /// <summary>The starting squad, for now four of the DrawPets cast. There's no ranch yet.</summary>
    static readonly string[] Squad = { "Blaze", "Sprocket", "Tank", "Hoot" };

    public static Battle Create(ulong seed)
    {
        var squad = Squad.Select(n => Creature.Specimen(DrawPetsCast.All.First(s => s.Name == n), 0));
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
        EndEvent end => end.Winner == Side.Player ? "== Victory" : "== Defeat",
        _ => e.ToString(),
    };
}
