using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One biome slot of the stencil: a block of same-sector cells (WORLD_GENERATION.md §1). Its tags say where it
/// lies (north, south, east, west, coast or inland, core), which the biome catalog's requirements are checked against.
/// </summary>
public class StencilSlot
{
    public int index;
    public string sector;
    /// <summary>Stable id of the slot: sector, instance and a number ("S5 South-East 2").</summary>
    public string id;
    /// <summary>Stable id of the sector instance it belongs to ("S5 South-East").</summary>
    public string instance;
    public readonly List<(int col, int row)> cells = new List<(int col, int row)>();
    /// <summary>Centre in stencil cells (column, row; row 0 is north).</summary>
    public float centerCol, centerRow;
    public readonly HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Slots separated from this one by P alone (any sector): their seams are solved together.</summary>
    public readonly List<int> neighbours = new List<int>();

    public override string ToString() => id;
}

/// <summary>
/// The composition stencil (Resources/World/Composition.txt): a grid of S1-S7 sector cells, P (procedural connective
/// terrain) and W (ocean), north at the top. Parsed with no scene state (tested in <c>WorldGenerationTests</c>).
/// Each connected block of one sector is a slot; blocks of a sector that only P separates form one sector instance.
/// </summary>
public class WorldStencil
{
    public const string Ocean = "W", Connective = "P";

    public int Width { get; private set; }
    public int Height { get; private set; }
    public List<StencilSlot> Slots { get; } = new List<StencilSlot>();

    private string[,] _tokens;
    private int[,] _slotAt;

    public string At(int col, int row) => col >= 0 && row >= 0 && col < Width && row < Height ? _tokens[col, row] : Ocean;

    /// <summary>The slot owning a stencil cell, or -1 (P, W, off the stencil).</summary>
    public int SlotAt(int col, int row) => col >= 0 && row >= 0 && col < Width && row < Height ? _slotAt[col, row] : -1;

    public static bool IsSector(string token) => token != null && token.Length > 1 && (token[0] == 'S' || token[0] == 's');

    public IEnumerable<string> SectorIds => Slots.Select(s => s.sector).Distinct(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<StencilSlot> SlotsOf(string sector) => Slots.Where(s => string.Equals(s.sector, sector, StringComparison.OrdinalIgnoreCase));

    /// <summary>Parse the stencil. Lines starting with # are comments; tokens are separated by spaces.</summary>
    public static WorldStencil Parse(string text)
    {
        var rows = new List<string[]>();
        foreach (var raw in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            rows.Add(line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
        if (rows.Count == 0) throw new FormatException("The world stencil is empty.");
        int width = rows[0].Length;
        for (int r = 0; r < rows.Count; r++)
        {
            if (rows[r].Length != width) throw new FormatException($"Stencil row {r + 1} has {rows[r].Length} cells, the first row {width}.");
        }

        var stencil = new WorldStencil { Width = width, Height = rows.Count };
        stencil._tokens = new string[width, rows.Count];
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < width; c++)
            {
                string token = rows[r][c].ToUpperInvariant();
                if (token != Ocean && token != Connective && !IsSector(token)) throw new FormatException($"Stencil cell {c + 1},{r + 1} reads '{rows[r][c]}': expected W, P or S1-S7.");
                stencil._tokens[c, r] = token;
            }
        }
        stencil.FindSlots();
        return stencil;
    }

    private void FindSlots()
    {
        _slotAt = new int[Width, Height];
        for (int c = 0; c < Width; c++) for (int r = 0; r < Height; r++) _slotAt[c, r] = -1;

        // Blocks: 4-connected cells of one sector, found in reading order so indices are stable.
        for (int r = 0; r < Height; r++)
        {
            for (int c = 0; c < Width; c++)
            {
                if (_slotAt[c, r] >= 0 || !IsSector(_tokens[c, r])) continue;
                var slot = new StencilSlot { index = Slots.Count, sector = _tokens[c, r] };
                var open = new Stack<(int, int)>();
                open.Push((c, r));
                _slotAt[c, r] = slot.index;
                while (open.Count > 0)
                {
                    var (x, y) = open.Pop();
                    slot.cells.Add((x, y));
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || _slotAt[nx, ny] >= 0 || _tokens[nx, ny] != slot.sector) continue;
                        _slotAt[nx, ny] = slot.index;
                        open.Push((nx, ny));
                    }
                }
                slot.cells.Sort((a, b) => a.row != b.row ? a.row.CompareTo(b.row) : a.col.CompareTo(b.col));
                slot.centerCol = (float)slot.cells.Average(p => p.col);
                slot.centerRow = (float)slot.cells.Average(p => p.row);
                Slots.Add(slot);
            }
        }

        // Neighbours: only P between them (Chebyshev gap of one cell).
        foreach (var a in Slots)
        {
            foreach (var b in Slots)
            {
                if (a != b && Gap(a, b) <= 2) a.neighbours.Add(b.index);
            }
        }

        NameInstances();
        foreach (var slot in Slots) Tag(slot);
    }

    private static int Gap(StencilSlot a, StencilSlot b)
    {
        int best = int.MaxValue;
        foreach (var p in a.cells)
            foreach (var q in b.cells)
                best = Math.Min(best, Math.Max(Math.Abs(p.col - q.col), Math.Abs(p.row - q.row)));
        return best;
    }

    private void NameInstances()
    {
        // Union same-sector neighbours into instances.
        var root = Enumerable.Range(0, Slots.Count).ToArray();
        int Find(int i) => root[i] == i ? i : root[i] = Find(root[i]);
        foreach (var slot in Slots)
            foreach (int n in slot.neighbours)
                if (string.Equals(Slots[n].sector, slot.sector, StringComparison.OrdinalIgnoreCase)) root[Find(n)] = Find(slot.index);

        foreach (var group in Slots.GroupBy(s => Find(s.index)))
        {
            var members = group.OrderBy(s => s.index).ToList();
            float col = members.Average(s => s.centerCol), row = members.Average(s => s.centerRow);
            string instance = $"{members[0].sector} {Compass(col, row)}";
            // Two instances of one sector in the same direction keep distinct ids.
            string unique = instance;
            for (int n = 2; Slots.Any(s => s.instance == unique); n++) unique = $"{instance} {n}";
            for (int i = 0; i < members.Count; i++)
            {
                members[i].instance = unique;
                members[i].id = members.Count > 1 ? $"{unique} {i + 1}" : unique;
            }
        }
    }

    /// <summary>Direction of a stencil position from the stencil's centre ("North-West", "Centre").</summary>
    public string Compass(float col, float row)
    {
        float dx = col + 0.5f - Width / 2f, dy = Height / 2f - (row + 0.5f);
        if (Math.Abs(dx) < 1.5f && Math.Abs(dy) < 1.5f) return "Centre";
        string ns = dy > Math.Abs(dx) * 0.41f ? "North" : dy < -Math.Abs(dx) * 0.41f ? "South" : string.Empty;
        string ew = dx > Math.Abs(dy) * 0.41f ? "East" : dx < -Math.Abs(dy) * 0.41f ? "West" : string.Empty;
        return ns.Length > 0 && ew.Length > 0 ? $"{ns}-{ew}" : ns + ew;
    }

    private void Tag(StencilSlot slot)
    {
        float dx = slot.centerCol + 0.5f - Width / 2f, dy = Height / 2f - (slot.centerRow + 0.5f);
        if (dy > 1f) slot.tags.Add("north");
        if (dy < -1f) slot.tags.Add("south");
        if (dx > 1f) slot.tags.Add("east");
        if (dx < -1f) slot.tags.Add("west");

        bool coast = slot.cells.Any(p =>
        {
            for (int x = p.col - 2; x <= p.col + 2; x++)
                for (int y = p.row - 2; y <= p.row + 2; y++)
                    if (At(x, y) == Ocean) return true;
            return false;
        });
        slot.tags.Add(coast ? "coast" : "inland");

        bool core = string.Equals(slot.sector, "S1", StringComparison.OrdinalIgnoreCase)
            || slot.neighbours.Any(n => string.Equals(Slots[n].sector, "S1", StringComparison.OrdinalIgnoreCase));
        if (core) slot.tags.Add("core");
    }
}
