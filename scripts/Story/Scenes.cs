using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TheFartherWeStray.Tactics;
using Godot;

namespace TheFartherWeStray.Story;

/// <summary>
/// A cutscene (built 2026-11-02 at the user's pick): a map, and a script of who stands where and what
/// they do and say, played on the board by <see cref="CutsceneScreen"/>. Scripts are plain lines so the
/// story can be written as text:
/// <code>
/// # a comment
/// cast you person you at player face ne          a person, by look id (side city|undercity|wilds; name "Shown Name"; hidden)
/// cast blaze creature squad1 at player+1,0       a creature: squad1.. (your squad), pack1.. (the battle's pack), a cast name, or wild:SEED
/// title Lantern Row                              a caption
/// look: The road is blocked.                     a line of dialogue (narrate TEXT for no speaker)
/// walk you player+2,-1                           walk there (leave WHO [TILE]: walk off and fade; appear WHO: fade in)
/// face you look       face you ne                turn toward someone, or ne nw se sw
/// emote blaze !                                  a balloon: ! ? ... or a glyph name (sound, sparkle, skull...)
/// pose blaze cast                                strike, cast, hurt, faint
/// pan look        pan 6,4                         move the camera
/// wait 0.5        fade in        fade out        shake
/// sound gong      music battle   music stop
/// &amp; walk blaze 5,5                                a leading &amp; runs the step alongside the next one
/// </code>
/// Tiles are "X,Y", or "player" / "wild" (the map's two starting sides), optionally "+DX,DY" or "-DX,DY" from them.
/// </summary>
public sealed class SceneDef
{
    public string Id, Title, Map, Music;
    public string Script;
    public bool Placeholder;   // stand-in content for the story to replace
}

public sealed record CastLine(int Line, string Id, string Kind, string Ref, World.Side Side, string Name, string At, string Face, bool Hidden);
public sealed record Step(int Line, string Verb, string Who, string[] Args, string Text, bool Alongside);

/// <summary>A scene's script, read into its cast and its steps, with anything it couldn't read.</summary>
public sealed class Script
{
    public readonly List<CastLine> Cast = new();
    public readonly List<Step> Steps = new();
    public readonly List<string> Problems = new();

    static readonly string[] Verbs = { "walk", "face", "emote", "pose", "appear", "leave", "pan", "wait", "fade", "sound", "music", "shake", "title", "narrate" };
    static readonly Regex Say = new(@"^([a-z0-9_]+)\s*:\s*(.+)$");

    public static Script Parse(string text)
    {
        var s = new Script();
        var lines = text.Replace("\r", "").Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            bool along = line.StartsWith('&');
            if (along) line = line[1..].Trim();
            if (Say.Match(line) is { Success: true } m && m.Groups[1].Value is not ("title" or "narrate"))
            {
                s.Steps.Add(new Step(n + 1, "say", m.Groups[1].Value, Array.Empty<string>(), m.Groups[2].Value.Trim(), along));
                continue;
            }
            var words = Tokens(line);
            string verb = words[0];
            if (verb == "cast") { s.ReadCast(n + 1, words); continue; }
            if (!Verbs.Contains(verb)) { s.Problems.Add($"line {n + 1}: don't know \"{verb}\""); continue; }
            if (verb is "title" or "narrate")
            {
                s.Steps.Add(new Step(n + 1, verb, null, Array.Empty<string>(), line[verb.Length..].Trim(), along));
                continue;
            }
            // Steps about someone name them first; the rest are about the scene.
            bool aboutSomeone = verb is "walk" or "face" or "emote" or "pose" or "appear" or "leave" or "pan" && words.Count > 1 && !Tiles.Looks(words[1]);
            string who = aboutSomeone ? words[1] : null;
            var args = words.Skip(aboutSomeone ? 2 : 1).ToArray();
            s.Steps.Add(new Step(n + 1, verb, who, args, null, along));
        }
        return s;
    }

    void ReadCast(int line, List<string> w)
    {
        if (w.Count < 4) { Problems.Add($"line {line}: cast needs an id, person or creature, and who"); return; }
        string id = w[1], kind = w[2], who = w[3], name = null, at = "player", face = null;
        var side = World.Side.Wilds;
        bool hidden = false;
        for (int i = 4; i < w.Count; i++)
            switch (w[i])
            {
                case "at" when i + 1 < w.Count: at = w[++i]; break;
                case "face" when i + 1 < w.Count: face = w[++i]; break;
                case "name" when i + 1 < w.Count: name = w[++i]; break;
                case "side" when i + 1 < w.Count:
                    side = w[++i] switch { "city" => World.Side.City, "undercity" => World.Side.Undercity, _ => World.Side.Wilds };
                    break;
                case "hidden": hidden = true; break;
                default: Problems.Add($"line {line}: cast doesn't know \"{w[i]}\""); break;
            }
        if (kind is not ("person" or "creature")) Problems.Add($"line {line}: cast {id} must be a person or a creature, not \"{kind}\"");
        if (Cast.Any(c => c.Id == id)) Problems.Add($"line {line}: {id} is cast twice");
        Cast.Add(new CastLine(line, id, kind, who, side, name, at, face, hidden));
    }

    /// <summary>Words, with "quoted phrases" kept whole.</summary>
    static List<string> Tokens(string line) =>
        Regex.Matches(line, "\"([^\"]*)\"|(\\S+)").Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToList();
}

/// <summary>Tiles in scripts: "X,Y", or "player" / "wild" plus an optional "+DX,DY" (or "-DX,DY"), and the four facings.</summary>
public static class Tiles
{
    static readonly Regex Form = new(@"^(?:(player|wild)(?:([+-]\d+),(-?\d+))?|(-?\d+),(-?\d+))$");

    public static bool Looks(string s) => Form.IsMatch(s);

    public static Vector2I? Resolve(string s, BattleMap map)
    {
        var m = Form.Match(s ?? "");
        if (!m.Success) return null;
        if (m.Groups[4].Success) return new Vector2I(int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture));
        var anchor = m.Groups[1].Value == "player" ? map.PlayerAnchor : map.WildAnchor;
        return m.Groups[2].Success ? anchor + new Vector2I(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)) : anchor;
    }

    /// <summary>A facing as a step across the grid: ne and sw run along Y, nw and se along X (as the board is drawn).</summary>
    public static Vector2I? Facing(string s) => s switch
    {
        "ne" => new Vector2I(0, -1), "sw" => new Vector2I(0, 1), "nw" => new Vector2I(-1, 0), "se" => new Vector2I(1, 0), _ => null,
    };
}

/// <summary>
/// Every scene. A main mission names the one that plays before its battle (<c>MainMission.Scene</c>);
/// <c>--scene=ID</c> plays one on its own and <c>--scenetest</c> reads them all. The story is the user's:
/// scenes marked Placeholder are stand-ins showing what the player can do, to be replaced.
/// </summary>
public static class SceneBook
{
    public static readonly SceneDef[] All =
    {
        new()
        {
            Id = "road_to_foundry", Title = "The road to the Foundry", Map = "Lantern Row", Music = "map", Placeholder = true,
            Script = @"
# A stand-in: the first main mission, given a reason. Replace with the story's own.
cast you person you at player face ne
cast look person lookout side undercity name ""Lookout"" at player-4,-5 face se
cast one creature squad1 at player+1,-1 face ne
cast two creature squad2 at player-1,0 face ne
cast three creature squad3 at player-1,-2 face ne
cast punk1 person street_a side undercity name ""Local"" at player-5,-4 face se
cast punk2 person street_b side undercity name ""Local"" at player+2,-4 face sw
cast p1 creature pack1 at wild hidden face sw
cast p2 creature pack2 at wild+3,0 hidden face sw
cast p3 creature pack3 at wild-2,0 hidden face sw
pan you
fade in
title Lantern Row
& walk look player-2,-3
wait 0.4
emote look !
look: You the tamer from the ranch? Good. The road to the Foundry's blocked.
look: A wild pack took over the street last night. Nothing's getting through.
face you look
emote you ...
pan wild
& appear p1
& appear p2
appear p3
sound gong
& emote punk1 !
& emote punk2 !
& emote p1 !
& emote p2 !
emote p3 !
& pose p1 cast
pose p2 cast
pan you
& emote one !
& emote two !
emote three !
face look wild
look: There they are. Clear them out, and the Foundry's yours to reach.
& walk you player-2,-2
& walk one player+0,-3
walk two player-2,-1
face you wild
pose one cast
music stop
fade out
",
        },
    };

    public static SceneDef Get(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>What's wrong with a scene: lines not understood, unknown names, tiles off the map or where nobody can stand.</summary>
    public static IEnumerable<string> Problems(SceneDef scene)
    {
        var script = Script.Parse(scene.Script);
        foreach (var p in script.Problems) yield return $"{scene.Id} {p}";
        BattleMap map;
        try { map = BattleMap.ByName(scene.Map); } catch { map = null; }
        if (map == null) { yield return $"{scene.Id}: no map called {scene.Map}"; yield break; }
        if (scene.Music != null && Audio.SongBook.Get(scene.Music) == null) yield return $"{scene.Id}: no music called {scene.Music}";
        var ids = script.Cast.Select(c => c.Id).ToHashSet();
        bool Bad(string tile, out string why)
        {
            why = null;
            if (Tiles.Resolve(tile, map) is not { } t) { why = $"\"{tile}\" isn't a tile"; return true; }
            if (!map.InBounds(t)) { why = $"{tile} is off the map"; return true; }
            if (map.Blocked(t) || map.TerrainAt(t) is Terrain.Pit or Terrain.Water or Terrain.Chem or Terrain.Lava) { why = $"{tile} ({t}) is no place to stand"; return true; }
            return false;
        }
        foreach (var c in script.Cast)
            if (Bad(c.At, out var why)) yield return $"{scene.Id} line {c.Line}: {c.Id} at {why}";
        foreach (var s in script.Steps)
        {
            if (s.Verb is "say" or "walk" or "face" or "emote" or "pose" or "appear" or "leave" && (s.Who == null || !ids.Contains(s.Who)))
                yield return $"{scene.Id} line {s.Line}: nobody called {s.Who ?? "(no one)"} is cast";
            string why = null;
            bool bad = s.Verb switch
            {
                "walk" => s.Args.Length != 1 || Bad(s.Args[0], out why),
                "leave" => s.Args.Length == 1 && Bad(s.Args[0], out why),
                "pan" => s.Who == null && (s.Args.Length != 1 || Tiles.Resolve(s.Args[0], map) == null),
                "face" => s.Args.Length != 1 || Tiles.Facing(s.Args[0]) == null && !ids.Contains(s.Args[0]) && Tiles.Resolve(s.Args[0], map) == null,
                "emote" => s.Args.Length != 1,
                "pose" => s.Args.Length != 1 || s.Args[0] is not ("strike" or "cast" or "hurt" or "faint"),
                "wait" => s.Args.Length != 1 || !float.TryParse(s.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                "fade" => s.Args.Length != 1 || s.Args[0] is not ("in" or "out"),
                "sound" => s.Args.Length != 1 || !Audio.SoundBook.All.ContainsKey(s.Args[0]),
                "music" => s.Args.Length != 1 || s.Args[0] != "stop" && Audio.SongBook.Get(s.Args[0]) == null,
                _ => false,
            };
            if (bad) yield return $"{scene.Id} line {s.Line}: {s.Verb} {string.Join(" ", s.Args)}{(why != null ? $": {why}" : " doesn't read")}";
        }
    }
}
