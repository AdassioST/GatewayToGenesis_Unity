using System;
using System.Collections.Generic;

/// <summary>
/// A hex on the world map in axial coordinates (q, r), pointy-top (redblobgames' conventions: s = -q - r).
/// Pure math, tested in <c>WorldMapTests</c>: neighbours, distance, rings and the pixel conversion the map view
/// uses for placing and hit-testing tiles.
/// </summary>
[Serializable]
public struct HexCoord : IEquatable<HexCoord>
{
    public int q, r;

    public HexCoord(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    public int S => -q - r;

    public static readonly HexCoord Zero = new HexCoord(0, 0);

    /// <summary>The six directions, counter-clockwise from east.</summary>
    public static readonly HexCoord[] Directions =
    {
        new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
        new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1),
    };

    public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.q + b.q, a.r + b.r);
    public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.q - b.q, a.r - b.r);
    public static HexCoord operator *(HexCoord a, int k) => new HexCoord(a.q * k, a.r * k);
    public static bool operator ==(HexCoord a, HexCoord b) => a.q == b.q && a.r == b.r;
    public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);

    public bool Equals(HexCoord other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
    public override int GetHashCode() => (q * 397) ^ r;
    public override string ToString() => $"({q}, {r})";

    public HexCoord Neighbor(int direction) => this + Directions[((direction % 6) + 6) % 6];

    public IEnumerable<HexCoord> Neighbors()
    {
        for (int i = 0; i < 6; i++) yield return Neighbor(i);
    }

    /// <summary>Turned about the origin by <paramref name="steps"/> sixths of a turn, counter-clockwise (Directions[i] becomes Directions[i + steps]).</summary>
    public HexCoord Rotate(int steps)
    {
        var hex = this;
        for (int i = 0, n = ((steps % 6) + 6) % 6; i < n; i++) hex = new HexCoord(hex.q + hex.r, -hex.q);
        return hex;
    }

    public static int Distance(HexCoord a, HexCoord b)
    {
        var d = a - b;
        return (Math.Abs(d.q) + Math.Abs(d.r) + Math.Abs(d.S)) / 2;
    }

    public int DistanceTo(HexCoord other) => Distance(this, other);

    /// <summary>The hexes exactly <paramref name="radius"/> steps from <paramref name="center"/> (just the centre for 0).</summary>
    public static List<HexCoord> Ring(HexCoord center, int radius)
    {
        var ring = new List<HexCoord>();
        if (radius <= 0)
        {
            ring.Add(center);
            return ring;
        }
        var hex = center + Directions[4] * radius;
        for (int side = 0; side < 6; side++)
        {
            for (int step = 0; step < radius; step++)
            {
                ring.Add(hex);
                hex = hex.Neighbor(side);
            }
        }
        return ring;
    }

    /// <summary>Every hex within <paramref name="radius"/> of <paramref name="center"/>, ring by ring outwards.</summary>
    public static List<HexCoord> Spiral(HexCoord center, int radius)
    {
        var all = new List<HexCoord>();
        for (int k = 0; k <= radius; k++) all.AddRange(Ring(center, k));
        return all;
    }

    /// <summary>Hexes in a map of <paramref name="radius"/>: 1 + 3r(r+1).</summary>
    public static int CountWithin(int radius) => radius < 0 ? 0 : 1 + 3 * radius * (radius + 1);

    // ===== PIXELS (pointy-top; y grows upwards, as in a Unity RectTransform) =====

    /// <summary>Centre of the hex for hexes of circumradius <paramref name="size"/>.</summary>
    public void ToPixel(float size, out float x, out float y)
    {
        x = size * (float)(Math.Sqrt(3.0) * (q + r / 2.0));
        y = -size * 1.5f * r;
    }

    /// <summary>The hex containing a point (inverse of <see cref="ToPixel"/>).</summary>
    public static HexCoord FromPixel(float x, float y, float size)
    {
        double fq = (Math.Sqrt(3.0) / 3.0 * x - 1.0 / 3.0 * -y) / size;
        double fr = (2.0 / 3.0 * -y) / size;
        return Round(fq, fr);
    }

    /// <summary>Cube rounding of fractional axial coordinates.</summary>
    public static HexCoord Round(double fq, double fr)
    {
        double fs = -fq - fr;
        double rq = Math.Round(fq), rr = Math.Round(fr), rs = Math.Round(fs);
        double dq = Math.Abs(rq - fq), dr = Math.Abs(rr - fr), ds = Math.Abs(rs - fs);
        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds) rr = -rq - rs;
        return new HexCoord((int)rq, (int)rr);
    }

    /// <summary>
    /// Which quarter of the world a hex lies in, by the direction of its centre from the capital: 0 north-east,
    /// 1 north-west, 2 south-west, 3 south-east (the capital itself is 0).
    /// </summary>
    public int Quarter()
    {
        ToPixel(1f, out float x, out float y);
        bool east = x > 0f || (Math.Abs(x) < 0.0001f && y >= 0f);
        bool north = y > 0f || (Math.Abs(y) < 0.0001f && x >= 0f);
        if (north) return east ? 0 : 1;
        return east ? 3 : 2;
    }
}
