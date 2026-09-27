using System;
using System.Collections.Generic;

/// <summary>
/// The world's three scales on one index-7 axial lattice (Docs/Planning/WORLD_GENERATION.md §3, roadmap WG02), with
/// no scene state (tested in <c>WorldGenerationTests</c>).
///
/// A parent (Q, R) has its centre at <c>F(Q, R) = (2Q - R, Q + 3R)</c> in its children's lattice and owns that centre
/// plus its six neighbours: seven children, every child exactly one parent (the transform's determinant is 7 and the
/// seven offsets cover its seven residues). One application groups micro hexes into meso cells (the playable
/// strategy hexes); two more group 49 meso cells into a macro aggregate (the intermediate index exists in data only).
///
/// World positions are measured on the micro lattice (pointy-top, circumradius 1, <see cref="HexCoord.ToPixel"/>).
/// Each level's lattice is the one below turned by about 19.1 degrees and scaled by sqrt 7, so a parent's true
/// outline is the union of its children, never a larger regular hexagon.
/// </summary>
public static class HexHierarchy
{
    /// <summary>Children of a parent, relative to its centre: the centre, then the six neighbours.</summary>
    public static readonly HexCoord[] ChildOffsets =
    {
        new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1),
        new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1),
    };

    // Offset whose residue (3q + r) mod 7 is the index.
    private static readonly HexCoord[] OffsetByResidue = BuildResidueTable();

    /// <summary>Levels above micro: 1 = meso, 2 = intermediate, 3 = macro.</summary>
    public const int Meso = 1, Intermediate = 2, Macro = 3;

    /// <summary>World units between the centres of two neighbouring micro hexes (circumradius 1).</summary>
    public static readonly double MicroSpacing = Math.Sqrt(3.0);

    private static HexCoord[] BuildResidueTable()
    {
        var table = new HexCoord[7];
        foreach (var offset in ChildOffsets) table[Mod7(3 * offset.q + offset.r)] = offset;
        return table;
    }

    private static int Mod7(int value) => ((value % 7) + 7) % 7;

    /// <summary>Centre of a parent in its children's lattice.</summary>
    public static HexCoord ChildCenter(HexCoord parent) => new HexCoord(2 * parent.q - parent.r, parent.q + 3 * parent.r);

    /// <summary>The one parent that owns <paramref name="child"/> (exact for negative coordinates).</summary>
    public static HexCoord Parent(HexCoord child)
    {
        var offset = OffsetByResidue[Mod7(3 * child.q + child.r)];
        int dq = child.q - offset.q, dr = child.r - offset.r;
        // F^-1(x, y) = ((3x + y) / 7, (-x + 2y) / 7); both divide exactly for the right offset.
        return new HexCoord((3 * dq + dr) / 7, (-dq + 2 * dr) / 7);
    }

    /// <summary>Which of the seven children <paramref name="child"/> is (its offset from the parent's centre).</summary>
    public static HexCoord OffsetInParent(HexCoord child) => OffsetByResidue[Mod7(3 * child.q + child.r)];

    public static IEnumerable<HexCoord> Children(HexCoord parent)
    {
        var center = ChildCenter(parent);
        foreach (var offset in ChildOffsets) yield return center + offset;
    }

    /// <summary>The ancestor <paramref name="levels"/> levels up (0: the hex itself).</summary>
    public static HexCoord Ancestor(HexCoord hex, int levels)
    {
        for (int i = 0; i < levels; i++) hex = Parent(hex);
        return hex;
    }

    /// <summary>The micro hex at the centre of a cell <paramref name="level"/> levels up.</summary>
    public static HexCoord MicroCenter(HexCoord cell, int level)
    {
        for (int i = 0; i < level; i++) cell = ChildCenter(cell);
        return cell;
    }

    /// <summary>World position (micro units) of the centre of a cell <paramref name="level"/> levels up.</summary>
    public static void ToWorld(HexCoord cell, int level, out float x, out float y) => MicroCenter(cell, level).ToPixel(1f, out x, out y);

    /// <summary>The micro hex containing a world point.</summary>
    public static HexCoord MicroAt(float x, float y) => HexCoord.FromPixel(x, y, 1f);

    /// <summary>The cell <paramref name="level"/> levels up containing a world point (by ownership of its micro hex).</summary>
    public static HexCoord CellAt(float x, float y, int level) => Ancestor(MicroAt(x, y), level);

    /// <summary>World distance between the centres of neighbouring cells <paramref name="level"/> levels up.</summary>
    public static double Spacing(int level) => MicroSpacing * Math.Pow(Math.Sqrt(7.0), level);

    /// <summary>World area of one cell <paramref name="level"/> levels up (7^level micro hexes).</summary>
    public static double Area(int level) => 1.5 * Math.Sqrt(3.0) * Math.Pow(7.0, level);
}
