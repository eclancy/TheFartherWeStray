using System;
using System.Collections.Generic;
using Godot;

namespace CreatureTactics.Tactics;

public enum CommandKind { Move, Attack, Skill, Berry, Undo, Flee, EndTurn }

/// <summary>
/// One action in a battle, as data: who acts (an index into <see cref="Battle.Units"/>), what they
/// do, and where or at whom. Every action goes through <see cref="Battle.Order"/> as one of these,
/// so a battle can be saved, replayed, or played over a network by sending just its commands: the
/// groundwork for online PvP (lockstep). Small and serialisable on purpose.
/// </summary>
public readonly record struct Command(CommandKind Kind, int Unit, int TileX = 0, int TileY = 0, int Target = -1, string Skill = null)
{
    public Vector2I Tile => new(TileX, TileY);
}

public sealed partial class Battle
{
    /// <summary>Every command given so far, in order.</summary>
    public readonly List<Command> History = new();
    /// <summary>The state's fingerprint after each turn ended, to check two copies of a battle agree.</summary>
    public readonly List<ulong> TurnHashes = new();

    /// <summary>Gives an order, from a player, the AI or a replay. All actions come through here.</summary>
    public void Order(Command c)
    {
        var u = Units[c.Unit];
        History.Add(c);
        switch (c.Kind)
        {
            case CommandKind.Move: Move(u, c.Tile); break;
            case CommandKind.Attack: Attack(u, Units[c.Target]); break;
            case CommandKind.Skill: UseSkill(u, Classes.ClassBook.Skill(c.Skill), c.Tile); break;
            case CommandKind.Berry: OfferBerry(u, c.Tile); break;
            case CommandKind.Undo: UndoMove(u); break;
            case CommandKind.Flee: Flee(u); break;
            case CommandKind.EndTurn: EndTurn(u); break;
        }
        if (c.Kind is CommandKind.EndTurn or CommandKind.Flee) TurnHashes.Add(StateHash());
    }

    public void Order(CommandKind kind, Unit u, Vector2I tile = default, Unit target = null, Classes.SkillDef skill = null) =>
        Order(new Command(kind, Units.IndexOf(u), tile.X, tile.Y, target == null ? -1 : Units.IndexOf(target), skill?.Id));

    /// <summary>
    /// A fingerprint of everything the rules care about: the round, berries, the dice, and every
    /// creature's place, health, statuses and cooldowns. Two machines with the same fingerprint
    /// after a turn are in the same battle.
    /// </summary>
    public ulong StateHash()
    {
        ulong h = 14695981039346656037UL;
        void Mix(long v) { h ^= unchecked((ulong)v); h *= 1099511628211UL; }
        Mix(Round); Mix(Berries); Mix((long)_rng.State); Mix(Winner == null ? -1 : (int)Winner.Value);
        foreach (var u in Units)
        {
            Mix(u.Pos.X); Mix(u.Pos.Y); Mix(u.Hp); Mix(u.MaxHp); Mix(u.Gone ? 1 : 0); Mix((int)u.Side);
            Mix(u.PoisonTurns); Mix(u.PoisonDmg); Mix(u.Stunned ? 1 : 0); Mix(u.Rooted ? 1 : 0); Mix(u.Weathered);
            foreach (var (stat, amount, turns) in u.Buffs) Mix((int)stat * 10000 + amount * 100 + turns);
            foreach (var cd in u.Cooldowns) Mix(cd.Value);
        }
        return h;
    }

    /// <summary>
    /// Plays a recorded battle again from its commands alone, in a fresh copy built the same way,
    /// and checks the fingerprint after every turn. Returns how many turns matched, and whether all did.
    /// </summary>
    public static (bool same, int turns) Replay(Battle recorded, Func<Battle> fresh)
    {
        var b = fresh();
        int ci = 0, turn = 0;
        while (ci < recorded.History.Count && b.Winner == null)
        {
            if (b.NextTurn() == null) break;
            while (ci < recorded.History.Count)
            {
                var c = recorded.History[ci++];
                b.Order(c);
                if (c.Kind is CommandKind.EndTurn or CommandKind.Flee) break;
            }
            if (turn >= recorded.TurnHashes.Count || turn >= b.TurnHashes.Count || b.TurnHashes[turn] != recorded.TurnHashes[turn]) return (false, turn);
            turn++;
        }
        return (turn == recorded.TurnHashes.Count, turn);
    }
}
