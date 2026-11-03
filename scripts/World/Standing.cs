using System;
using System.Collections.Generic;
using TheFartherWeStray.Ranching;

namespace TheFartherWeStray.World;

/// <summary>
/// Standing with the two sides (the user's direction, 2026-11-02): the glorious city above and the
/// nefarious undercity, who hate each other. Work done for a side raises it; the choices after main
/// missions trade one for the other; fitting augments (the undercity's work) and breeding traits
/// (the city's way) lean it too. Where it leans decides which ending opens: justice for the
/// undercity, chaos in the city above, or peace between them. Every amount is here, in one place.
/// </summary>
public static class Standing
{
    public const int Min = -100, Max = 100;

    // What moves it, and by how much (to the side the work was for).
    public const int Contract = 2, Regular = 1, MainMission = 5, Dispatch = 1, Cup = 4, Augment = 3, Breeding = 1, Splice = 2, Infusion = 1;
    /// <summary>At this standing a side's shops take 15% off.</summary>
    public const int Friendly = 50;

    /// <summary>Whose work a place's jobs are: the city's, the undercity's, or nobody's (the wilds).</summary>
    public static Side? ClientOf(string location) => Country.Get(location)?.Side switch
    {
        Side.City => Side.City,
        Side.Undercity => Side.Undercity,
        _ => null,
    };

    public static int Of(Ranch r, Side side) => side == Side.City ? r.CityStanding : r.UnderStanding;

    /// <summary>Shifts standing; returns how it moved, in words ("" if it didn't).</summary>
    public static string Shift(Ranch r, int city, int under)
    {
        int c0 = r.CityStanding, u0 = r.UnderStanding;
        r.CityStanding = Math.Clamp(r.CityStanding + city, Min, Max);
        r.UnderStanding = Math.Clamp(r.UnderStanding + under, Min, Max);
        var parts = new List<string>();
        if (r.CityStanding != c0) parts.Add($"{(r.CityStanding > c0 ? "+" : "")}{r.CityStanding - c0} with the city");
        if (r.UnderStanding != u0) parts.Add($"{(r.UnderStanding > u0 ? "+" : "")}{r.UnderStanding - u0} with the undercity");
        return string.Join(", ", parts);
    }

    /// <summary>Raises standing with whoever a place's work is for.</summary>
    public static string ForWork(Ranch r, string location, int amount) => ClientOf(location) switch
    {
        Side.City => Shift(r, amount, 0),
        Side.Undercity => Shift(r, 0, amount),
        _ => "",
    };

    /// <summary>The percent a side's shops take off for you.</summary>
    public static int Discount(Ranch r, Side side) => side != Side.Wilds && Of(r, side) >= Friendly ? 15 : 0;

    /// <summary>Where you lean: "city", "undercity", or "balanced" (within 30 of each other).</summary>
    public static string Lean(Ranch r) => r.CityStanding - r.UnderStanding >= 30 ? "city" : r.UnderStanding - r.CityStanding >= 30 ? "undercity" : "balanced";
}
