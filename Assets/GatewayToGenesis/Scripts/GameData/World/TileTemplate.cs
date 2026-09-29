using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// A handmade tile: a small patch of meso cells drawn by hand for one biome (Resources/World/Tiles/*.txt). The
/// generator stamps tiles into the protected interiors of their biome's slots, turned by the seed within the tile's
/// allowed rotations; the ground around them is procedural and the seams between them are blended. Parsed with no
/// scene state (tested in <c>WorldGenerationTests</c>).
///
/// Format: header lines <c>key: value</c> (tile, biome, weight, rotations, legend, heights), a line <c>---</c>,
/// then the drawing as a honeycomb: one character per cell, cells two characters apart, each row shifted one
/// character from the last (the "doubled" hex layout). <c>.</c> leaves a cell to the generator.
/// <code>
/// tile: violet-grove/ring-glade
/// macrobiome: violet-grove
/// weight: 2
/// rotations: any          (or: none, or a list of sixths of a turn such as 0 2 4)
/// legend: V=violet-wood G=violet-glade L=lake
/// heights: L=0.27         (optional absolute heights, 0-1)
/// ---
///    V V V
///   V G G V
///  V G L G V
///   V G G V
///    V V V
/// </code>
/// </summary>
public class TileTemplate
{
    public string id;
    public string macroBiome;
    public float weight = 1f;
    /// <summary>Allowed rotations, sixths of a turn counter-clockwise (0 always means as drawn).</summary>
    public List<int> rotations = new List<int> { 0 };
    /// <summary>Terrain of each drawn cell, relative to the tile's centre.</summary>
    public Dictionary<HexCoord, string> cells = new Dictionary<HexCoord, string>();
    /// <summary>Absolute heights the generator pulls drawn cells towards (by cell), where the legend gives one.</summary>
    public Dictionary<HexCoord, float> heights = new Dictionary<HexCoord, float>();

    /// <summary>Cells from the centre to the farthest drawn cell.</summary>
    public int Radius => cells.Count == 0 ? 0 : cells.Keys.Max(c => HexCoord.Distance(c, HexCoord.Zero));

    /// <summary>The drawn cells turned by <paramref name="rotation"/> sixths of a turn.</summary>
    public IEnumerable<KeyValuePair<HexCoord, string>> Rotated(int rotation) =>
        cells.Select(pair => new KeyValuePair<HexCoord, string>(pair.Key.Rotate(rotation), pair.Value));

    public float HeightAt(HexCoord rotatedCell, int rotation, out bool has)
    {
        has = heights.TryGetValue(rotatedCell.Rotate(-rotation), out float h);
        return h;
    }

    public static TileTemplate Parse(string text, string fallbackId = null)
    {
        var tile = new TileTemplate { id = fallbackId };
        var legend = new Dictionary<char, string>();
        var heightByChar = new Dictionary<char, float>();
        var drawing = new List<string>();
        bool inDrawing = false;

        foreach (var raw in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            if (inDrawing)
            {
                if (raw.Trim().Length > 0) drawing.Add(raw.TrimEnd());
                continue;
            }
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("---"))
            {
                inDrawing = true;
                continue;
            }
            int colon = line.IndexOf(':');
            if (colon < 0) throw new FormatException($"Tile '{tile.id}': header line '{line}' has no 'key: value'.");
            string key = line.Substring(0, colon).Trim().ToLowerInvariant(), value = line.Substring(colon + 1).Trim();
            int comment = value.IndexOf('#');
            if (comment >= 0) value = value.Substring(0, comment).Trim();
            switch (key)
            {
                case "tile": tile.id = value; break;
                case "macrobiome": tile.macroBiome = value; break;
                case "weight": tile.weight = float.Parse(value, CultureInfo.InvariantCulture); break;
                case "rotations": tile.rotations = ParseRotations(value, tile.id); break;
                case "legend":
                    foreach (var (c, v) in Pairs(value, tile.id)) legend[c] = v;
                    break;
                case "heights":
                    foreach (var (c, v) in Pairs(value, tile.id)) heightByChar[c] = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                default: throw new FormatException($"Tile '{tile.id}': unknown header '{key}'.");
            }
        }
        if (string.IsNullOrEmpty(tile.id)) throw new FormatException("A tile has no 'tile:' id.");
        if (string.IsNullOrEmpty(tile.macroBiome)) throw new FormatException($"Tile '{tile.id}' names no biome.");
        if (drawing.Count == 0) throw new FormatException($"Tile '{tile.id}' has no drawing after '---'.");

        // Doubled layout: a cell's column is 2q + r, so column + row keeps one parity for every cell.
        var drawn = new List<(int col, int row, char c)>();
        for (int row = 0; row < drawing.Count; row++)
        {
            for (int col = 0; col < drawing[row].Length; col++)
            {
                char c = drawing[row][col];
                if (c != ' ' && c != '\t') drawn.Add((col, row, c));
            }
        }
        int parity = (drawn[0].col + drawn[0].row) & 1;
        var misplaced = drawn.Where(d => ((d.col + d.row) & 1) != parity).ToList();
        if (misplaced.Count > 0) throw new FormatException($"Tile '{tile.id}': '{misplaced[0].c}' on drawing line {misplaced[0].row + 1} sits between two cells (cells are two characters apart, each row shifted by one).");

        var coords = new List<(HexCoord hex, char c)>();
        foreach (var (col, row, c) in drawn)
        {
            int doubled = col - parity; // make column + row even
            coords.Add((new HexCoord((doubled - row) / 2, row), c));
        }
        // Centre the tile on the drawn cell nearest the middle of all drawn cells.
        double mq = coords.Average(p => p.hex.q), mr = coords.Average(p => p.hex.r);
        var center = HexCoord.Round(mq, mr);
        foreach (var (hex, c) in coords)
        {
            if (c == '.') continue;
            if (!legend.TryGetValue(c, out var terrain)) throw new FormatException($"Tile '{tile.id}': '{c}' is not in its legend.");
            var cell = hex - center;
            tile.cells[cell] = terrain;
            if (heightByChar.TryGetValue(c, out float h)) tile.heights[cell] = h;
        }
        if (tile.cells.Count == 0) throw new FormatException($"Tile '{tile.id}' draws no cell.");
        return tile;
    }

    private static List<int> ParseRotations(string value, string id)
    {
        string v = value.Trim().ToLowerInvariant();
        if (v == "any" || v == "all") return new List<int> { 0, 1, 2, 3, 4, 5 };
        if (v == "none" || v.Length == 0) return new List<int> { 0 };
        var list = new List<int>();
        foreach (var part in v.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part, out int n) || n < 0 || n > 5) throw new FormatException($"Tile '{id}': rotation '{part}' is not 0-5.");
            if (!list.Contains(n)) list.Add(n);
        }
        return list;
    }

    private static IEnumerable<(char, string)> Pairs(string value, string id)
    {
        foreach (var part in value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq != 1 || part.Length < 3) throw new FormatException($"Tile '{id}': '{part}' should read X=value.");
            if (part[0] == '.') throw new FormatException($"Tile '{id}': '.' is reserved for cells left to the generator.");
            yield return (part[0], part.Substring(2));
        }
    }
}
