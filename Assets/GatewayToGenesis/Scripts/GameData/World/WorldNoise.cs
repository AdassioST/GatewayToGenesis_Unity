using System;

/// <summary>
/// Seeded, world-coordinate noise for the generator (no UnityEngine, so it is the same in tests and every
/// platform): gradient noise, fractal sums, ridges, and stable hashes. Each purpose gets its own stream
/// (<see cref="Stream"/>) so adding a feature never moves the continent (WORLD_GENERATION.md §4).
/// </summary>
public sealed class WorldNoise
{
    private readonly int[] _perm = new int[512];

    public WorldNoise(int seed)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        var rng = new Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    /// <summary>A seed for one purpose of one world ("terrain", "slots", "magic:2"...), stable across versions.</summary>
    public static int Stream(int seed, string purpose)
    {
        unchecked
        {
            uint hash = 2166136261u ^ (uint)seed;
            foreach (char c in purpose ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            hash ^= (uint)seed * 2654435761u;
            return (int)(Mix(hash) & 0x7fffffff);
        }
    }

    private static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x7feb352du;
            h ^= h >> 15;
            h *= 0x846ca68bu;
            h ^= h >> 16;
            return h;
        }
    }

    /// <summary>A stable value in [0, 1) for integer coordinates (per-cell dithering).</summary>
    public static double Hash01(int seed, int x, int y)
    {
        unchecked
        {
            uint h = Mix((uint)seed * 0x9E3779B9u ^ Mix((uint)x * 0x85EBCA6Bu ^ Mix((uint)y * 0xC2B2AE35u)));
            return (h & 0xffffff) / (double)0x1000000;
        }
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static double Grad(int hash, double x, double y)
    {
        switch (hash & 7)
        {
            case 0: return x + y;
            case 1: return x - y;
            case 2: return -x + y;
            case 3: return -x - y;
            case 4: return x;
            case 5: return -x;
            case 6: return y;
            default: return -y;
        }
    }

    /// <summary>Gradient noise in about [-1, 1].</summary>
    public double Noise(double x, double y)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
        double xf = x - xi, yf = y - yi;
        int X = xi & 255, Y = yi & 255;
        double u = Fade(xf), v = Fade(yf);
        int aa = _perm[_perm[X] + Y], ab = _perm[_perm[X] + Y + 1], ba = _perm[_perm[X + 1] + Y], bb = _perm[_perm[X + 1] + Y + 1];
        double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    /// <summary>Fractal sum of <paramref name="octaves"/> octaves, about [-1, 1].</summary>
    public double Fbm(double x, double y, int octaves, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Noise(x, y);
            norm += amp;
            amp *= gain;
            x = x * lacunarity + 17.3;
            y = y * lacunarity - 9.1;
        }
        return sum / norm;
    }

    /// <summary>Ridged noise in [0, 1]: sharp crests where the noise crosses zero (mountain chains).</summary>
    public double Ridge(double x, double y, int octaves)
    {
        double sum = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            double n = 1 - Math.Abs(Noise(x, y));
            sum += amp * n * n;
            norm += amp;
            amp *= 0.5;
            x = x * 2 + 5.7;
            y = y * 2 - 3.3;
        }
        return sum / norm;
    }

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double Smooth(double edge0, double edge1, double x)
    {
        double t = Math.Max(0, Math.Min(1, (x - edge0) / (edge1 - edge0)));
        return t * t * (3 - 2 * t);
    }
}
