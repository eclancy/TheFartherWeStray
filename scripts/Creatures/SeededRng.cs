using System.Collections.Generic;

namespace TheFartherWeStray.Creatures;

/// <summary>
/// Deterministic RNG (SplitMix64), so a seed draws the same creature on every machine and every
/// run. System.Random's algorithm is not guaranteed to stay the same across .NET versions.
/// </summary>
public sealed class SeededRng
{
    ulong _state;
    /// <summary>Where the stream is: two generators with the same state roll the same from here on.</summary>
    public ulong State => _state;

    public SeededRng(ulong seed) { _state = seed; }

    /// <summary>A stable seed from a name (FNV-1a; string hash codes change between runs).</summary>
    public static ulong Hash(string name)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in name) { h ^= c; h *= 1099511628211UL; }
        return h;
    }

    public ulong NextU64()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float Float() => (NextU64() >> 40) / (float)(1UL << 24);

    public float Range(float min, float max) => min + (max - min) * Float();

    public int Int(int maxExclusive) => (int)(NextU64() % (ulong)maxExclusive);

    public int Int(int min, int maxInclusive) => min + Int(maxInclusive - min + 1);

    public bool Chance(float p) => Float() < p;

    public T Pick<T>(IReadOnlyList<T> items) => items[Int(items.Count)];

    public T Weighted<T>(params (T item, float weight)[] options)
    {
        float total = 0;
        foreach (var o in options) total += o.weight;
        float roll = Float() * total;
        foreach (var o in options)
        {
            roll -= o.weight;
            if (roll < 0) return o.item;
        }
        return options[^1].item;
    }

    /// <summary>Derives an independent seed from a parent seed and a salt (sheet slot, sibling index...).</summary>
    public static ulong Derive(ulong seed, ulong salt) => new SeededRng(seed ^ (salt * 0xD6E8FEB86659FD93UL)).NextU64();
}
