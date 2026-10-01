using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>How firmly a holder holds a cell, by the micro hexes it holds (<see cref="WorldHoldings"/>).</summary>
public enum HoldStatus
{
    /// <summary>No hex of it.</summary>
    None,
    /// <summary>Some hexes, too few to rule the cell: the hexes are its own, the cell is not.</summary>
    Partial,
    /// <summary>At least <see cref="TerritoryRules.deFactoHexes"/> hexes, more than anyone else: the cell answers to it
    /// (its authority), but others may still hold the rest.</summary>
    DeFacto,
    /// <summary>Every open hex: core territory, fully integrated.</summary>
    Core,
}

/// <summary>Why a holder may press a claim to a cell by force (<see cref="WorldHoldings.CasusBelliOf"/>).</summary>
public enum CasusBelli
{
    None,
    /// <summary>It rules the cell de facto while another holds hexes of it: take them to make the cell core.</summary>
    Integrate,
    /// <summary>A core of its own that another holds in part or rules: recover it.</summary>
    Recover,
}

/// <summary>
/// Hexes of one cell held by a holder other than you (a rival, an occupier), saved with the world
/// (<see cref="WorldMap.HexHoldings"/>). Yours live in <see cref="WorldTile.microHeldMask"/>.
/// </summary>
[Serializable]
public class HexHolding
{
    public int cell;
    /// <summary>The holder's authority id.</summary>
    public string holder;
    /// <summary>Its micro hexes of the cell (bit k: <see cref="HexHierarchy.ChildOffsets"/>[k]).</summary>
    public int mask;
    /// <summary>It integrated the cell once (held every hex): its core claim stands while others hold any of it, even
    /// with no hex left.</summary>
    public bool core;
}

/// <summary>A cell shared between holders, or a core another holds in part (<see cref="WorldHoldings.Disputes"/>).</summary>
public class TerritoryDispute
{
    public int cell;
    /// <summary>Who rules it now (de facto or core), or null while no one holds enough hexes.</summary>
    public string ruler;
    /// <summary>Every holder with hexes of it, most first, and how many (the ruler included).</summary>
    public readonly List<(string holder, int hexes)> shares = new List<(string, int)>();
    /// <summary>Holders that count it as core territory (they integrated it once, or it lies in their settlements' reach).</summary>
    public readonly List<string> cores = new List<string>();
    /// <summary>Hexes of the cell anyone can hold (crags aside).</summary>
    public int open;

    public int HexesOf(string holder) => shares.Where(s => WorldHoldings.Same(s.holder, holder)).Sum(s => s.hexes);

    /// <summary>
    /// What <paramref name="holder"/> resents here, in hexes: those of its own core others hold, or, holding hexes
    /// under another's rule, the hexes it holds (its people live under a foreign authority). 0 when it has no grievance.
    /// </summary>
    public int Grievance(string holder)
    {
        int mine = HexesOf(holder);
        if (cores.Any(c => WorldHoldings.Same(c, holder))) return open - mine;
        return mine > 0 && !WorldHoldings.Same(ruler, holder) ? mine : 0;
    }
}

/// <summary>
/// Land held micro hex by micro hex, by any holder, with no scene state (tested in <c>WorldHoldingsTests</c>). Each of a
/// cell's seven hexes has at most one holder (<see cref="HexHolder"/>): yours are in
/// <see cref="WorldTile.microHeldMask"/> (your Outposts' too), other holders' in <see cref="WorldMap.HexHoldings"/>, and
/// a cell given whole to an authority (<see cref="WorldTile.whole"/>: a settlement's reach, an enclave) holds every hex
/// no one else does.
///
/// A holder with at least <see cref="TerritoryRules.deFactoHexes"/> hexes of a cell, and more than anyone else, rules it
/// de facto (<see cref="WorldAuthority.Establish"/> makes it the cell's authority); holding every hex makes it core
/// territory. A core claim outlasts the land: yours are the cells in <see cref="WorldMap.Claims"/> and
/// <see cref="WorldMap.Adopted"/>, another holder's are its holdings marked <see cref="HexHolding.core"/>, and a cell
/// given whole is its authority's core. A cell shared between holders, or a core another holds any of, is a
/// <see cref="TerritoryDispute"/>: the minority holds a grievance, the ruler a casus belli to integrate it, the core
/// holder one to recover it (<see cref="Grievances"/>, <see cref="CasusBelliOf"/>, <see cref="Occupied"/>).
/// <see cref="TakeHex"/> moves a hex between holders (occupation, cession); rebuild the civilization after.
/// </summary>
public static class WorldHoldings
{
    /// <summary>Hexes needed to rule a cell de facto (<see cref="TerritoryRules.deFactoHexes"/>, 1 to 7).</summary>
    public static int DeFactoHexes(WorldMap map) => Math.Max(1, Math.Min(MicroNavigation.PerCell, WorldTerritory.RulesOf(map).deFactoHexes));

    /// <summary>The same holder: yours and your Outposts' land count as one.</summary>
    public static bool Same(string a, string b) => a == b || (a != null && b != null && WorldAuthority.IsPlayers(a) && WorldAuthority.IsPlayers(b));

    /// <summary>Hexes of the cell anyone can hold (crags aside), as a mask.</summary>
    public static int OpenMask(WorldTile t) => t == null || t.water || t.impassable ? 0 : 127 & ~t.microBlockedMask;

    // ===== WHO HOLDS WHAT =====

    /// <summary>Other holders' hexes by cell (built on demand from <see cref="WorldMap.HexHoldings"/>).</summary>
    public static IReadOnlyList<HexHolding> At(WorldMap map, int cell)
    {
        if (map == null) return Array.Empty<HexHolding>();
        if (map.holdingIndex == null)
        {
            map.holdingIndex = new Dictionary<int, List<HexHolding>>();
            foreach (var h in map.HexHoldings)
            {
                if (h == null || string.IsNullOrEmpty(h.holder)) continue;
                if (!map.holdingIndex.TryGetValue(h.cell, out var list)) map.holdingIndex[h.cell] = list = new List<HexHolding>();
                list.Add(h);
            }
        }
        return map.holdingIndex.TryGetValue(cell, out var found) ? found : (IReadOnlyList<HexHolding>)Array.Empty<HexHolding>();
    }

    /// <summary>Call after changing <see cref="WorldMap.HexHoldings"/> by hand.</summary>
    public static void Invalidate(WorldMap map)
    {
        if (map != null) map.holdingIndex = null;
    }

    // The key your hexes of a cell go by: its authority when it is yours or your Outpost's, else yours.
    private static string PlayersKey(WorldTile t) => WorldAuthority.IsPlayers(t.authorityId) ? t.authorityId : WorldAuthority.Player;

    // Every hex someone holds explicitly (yours and the holdings).
    private static int Explicit(WorldMap map, WorldTile t)
    {
        int mask = t.microHeldMask;
        foreach (var h in At(map, t.index)) mask |= h.mask;
        return mask & OpenMask(t);
    }

    /// <summary>The hexes of <paramref name="t"/> <paramref name="holder"/> holds: its own, and those no one else holds of a cell given it whole.</summary>
    public static int MaskOf(WorldMap map, WorldTile t, string holder)
    {
        if (map == null || t == null || string.IsNullOrEmpty(holder)) return 0;
        int open = OpenMask(t);
        int mask = 0;
        if (WorldAuthority.IsPlayers(holder)) mask = t.microHeldMask;
        else foreach (var h in At(map, t.index)) if (h.holder == holder) mask |= h.mask;
        if (t.whole && Same(t.authorityId, holder)) mask |= open & ~Explicit(map, t);
        return mask & open;
    }

    public static int Hexes(WorldMap map, WorldTile t, string holder) => MicroNavigation.Crags(MaskOf(map, t, holder));

    /// <summary>
    /// Whether <paramref name="holder"/> can still take free hexes of the cell by settling or claiming: wilderness, or a
    /// cell it rules de facto (not one given whole, not core).
    /// </summary>
    public static bool Fillable(WorldTile t, string holder) =>
        t != null && !t.water && !t.impassable && !t.whole
        && (t.authorityId == WorldAuthority.Wilderness || (Same(t.authorityId, holder) && t.hold == HoldStatus.DeFacto));

    /// <summary>Hexes of the cell no one holds (none of a cell given whole).</summary>
    public static int FreeMask(WorldMap map, WorldTile t) => t == null || t.whole ? 0 : OpenMask(t) & ~Explicit(map, t);

    /// <summary>
    /// Who holds micro hex <paramref name="id"/>, or null: you (your Outpost's cell: the Outpost), another holder, or the
    /// authority of a cell given whole. A crag goes with its cell's authority.
    /// </summary>
    public static string HexHolder(WorldMap map, int id)
    {
        if (map == null || id < 0 || id >= map.Count * MicroNavigation.PerCell) return null;
        var t = map[id / MicroNavigation.PerCell];
        if (t.water) return null;
        int bit = 1 << (id % MicroNavigation.PerCell);
        if ((t.microBlockedMask & bit) != 0 || t.impassable) return t.authorityId == WorldAuthority.Wilderness ? null : t.authorityId;
        if ((t.microHeldMask & bit) != 0) return PlayersKey(t);
        foreach (var h in At(map, t.index)) if ((h.mask & bit) != 0) return h.holder;
        return t.whole && t.authorityId != WorldAuthority.Wilderness ? t.authorityId : null;
    }

    /// <summary>Every holder with hexes of the cell and how many, most first (yours under your key for the cell).</summary>
    public static List<(string holder, int hexes)> Shares(WorldMap map, WorldTile t)
    {
        var shares = new List<(string holder, int hexes)>();
        if (map == null || t == null || t.water) return shares;
        int mine = Hexes(map, t, WorldAuthority.Player);
        if (mine > 0) shares.Add((PlayersKey(t), mine));
        foreach (var holder in At(map, t.index).Select(h => h.holder).Distinct())
        {
            int n = Hexes(map, t, holder);
            if (n > 0) shares.Add((holder, n));
        }
        if (t.whole && !WorldAuthority.IsPlayers(t.authorityId) && t.authorityId != WorldAuthority.Wilderness && shares.All(s => s.holder != t.authorityId))
        {
            int n = Hexes(map, t, t.authorityId);
            if (n > 0) shares.Add((t.authorityId, n));
        }
        return shares.OrderByDescending(s => s.hexes).ThenBy(s => s.holder, StringComparer.Ordinal).ToList();
    }

    /// <summary>How firmly <paramref name="holder"/> holds the cell (<see cref="HoldStatus"/>).</summary>
    public static HoldStatus Status(WorldMap map, WorldTile t, string holder)
    {
        int open = MicroNavigation.Crags(OpenMask(t));
        int n = Hexes(map, t, holder);
        if (n <= 0 || open <= 0) return HoldStatus.None;
        if (n >= open) return HoldStatus.Core;
        if (n >= DeFactoHexes(map) && Shares(map, t).All(s => Same(s.holder, holder) || s.hexes < n)) return HoldStatus.DeFacto;
        return HoldStatus.Partial;
    }

    /// <summary>
    /// Who rules the cell by its hexes, or null: the holder with at least <see cref="DeFactoHexes"/> hexes and more than
    /// anyone else (a tie rules no one). A cell given whole is its authority's unless another rules it this way.
    /// </summary>
    public static string Ruler(WorldMap map, WorldTile t)
    {
        var shares = Shares(map, t);
        if (shares.Count > 0 && shares[0].hexes >= DeFactoHexes(map) && (shares.Count == 1 || shares[1].hexes < shares[0].hexes)) return shares[0].holder;
        return t != null && t.whole && t.authorityId != WorldAuthority.Wilderness ? t.authorityId : null;
    }

    // ===== CORES =====

    /// <summary>
    /// Holders that count the cell as core territory: you when it is in <see cref="WorldMap.Claims"/> or
    /// <see cref="WorldMap.Adopted"/>, holders that integrated it (<see cref="HexHolding.core"/>), and the authority it
    /// was given whole.
    /// </summary>
    public static List<string> CoresOf(WorldMap map, WorldTile t)
    {
        var cores = new List<string>();
        if (map == null || t == null || t.water) return cores;
        if (map.Claims.Contains(t.index) || map.Adopted.Contains(t.index)) cores.Add(WorldAuthority.Player);
        foreach (var h in At(map, t.index)) if (h.core && !cores.Any(c => Same(c, h.holder))) cores.Add(h.holder);
        if (t.whole && t.authorityId != WorldAuthority.Wilderness && !cores.Any(c => Same(c, t.authorityId))) cores.Add(t.authorityId);
        return cores;
    }

    /// <summary>Mark the cell core territory of <paramref name="holder"/> (not yours: yours are kept by <see cref="WorldTerritory.Complete"/>).</summary>
    public static void MarkCore(WorldMap map, int cell, string holder)
    {
        if (map == null || WorldAuthority.IsPlayers(holder) || string.IsNullOrEmpty(holder)) return;
        Holding(map, cell, holder).core = true;
    }

    // ===== MOVING HEXES =====

    private static HexHolding Holding(WorldMap map, int cell, string holder)
    {
        var h = At(map, cell).FirstOrDefault(x => x.holder == holder);
        if (h != null) return h;
        h = new HexHolding { cell = cell, holder = holder };
        map.HexHoldings.Add(h);
        Invalidate(map);
        return h;
    }

    /// <summary>
    /// Give micro hex <paramref name="id"/> to <paramref name="holder"/> (null: to no one), taking it from whoever held it
    /// (a hex of a cell given whole is taken from its authority simply by another holding it). Returns who held it
    /// before. Crags are never held. Rebuild the civilization after (<see cref="WorldCivilization.Rebuild"/>): the ruler
    /// and the core may change. Holdings emptied of hexes are kept while they carry a core claim.
    /// </summary>
    public static string TakeHex(WorldMap map, int id, string holder)
    {
        if (map == null || id < 0 || id >= map.Count * MicroNavigation.PerCell) return null;
        var t = map[id / MicroNavigation.PerCell];
        int bit = 1 << (id % MicroNavigation.PerCell);
        if ((OpenMask(t) & bit) == 0) return null;
        string before = HexHolder(map, id);
        t.microHeldMask &= ~bit;
        t.microClaimMask &= ~bit;
        foreach (var h in At(map, t.index)) h.mask &= ~bit;
        if (map.HexHoldings.RemoveAll(h => h.cell == t.index && h.mask == 0 && !h.core) > 0) Invalidate(map);
        if (string.IsNullOrEmpty(holder)) return before;
        if (WorldAuthority.IsPlayers(holder)) t.microHeldMask |= bit;
        else Holding(map, t.index, holder).mask |= bit;
        return before;
    }

    // ===== DISPUTES =====

    /// <summary>
    /// Every disputed cell: shared between holders (a minority holds hexes under another's rule, or no one rules it), or a
    /// core another holds any hex of. Cells only you and the wilderness share are not disputes.
    /// </summary>
    public static List<TerritoryDispute> Disputes(WorldMap map)
    {
        var disputes = new List<TerritoryDispute>();
        if (map == null) return disputes;
        var cells = new SortedSet<int>(map.HexHoldings.Where(h => h != null).Select(h => h.cell));
        foreach (var t in map.Tiles)
            if (t.microHeldMask != 0 && !WorldAuthority.IsPlayers(t.authorityId) && t.authorityId != WorldAuthority.Wilderness) cells.Add(t.index);
        foreach (int c in map.Claims.Concat(map.Adopted)) if (c >= 0 && c < map.Count && At(map, c).Count > 0) cells.Add(c);
        foreach (int c in cells)
        {
            if (c < 0 || c >= map.Count) continue;
            var d = Dispute(map, map[c]);
            if (d != null) disputes.Add(d);
        }
        return disputes;
    }

    /// <summary>The dispute over one cell, or null when it has none.</summary>
    public static TerritoryDispute Dispute(WorldMap map, WorldTile t)
    {
        if (map == null || t == null || t.water) return null;
        var shares = Shares(map, t);
        var cores = CoresOf(map, t);
        // A core someone else holds any of, or more than one holder on the cell.
        bool occupied = cores.Any(c => shares.Any(s => !Same(s.holder, c)));
        if (shares.Count < 2 && !occupied) return null;
        var d = new TerritoryDispute { cell = t.index, ruler = Ruler(map, t), open = MicroNavigation.Crags(OpenMask(t)) };
        d.shares.AddRange(shares);
        d.cores.AddRange(cores);
        return d;
    }

    /// <summary>The casus belli <paramref name="holder"/> has over a dispute: recover its core, integrate what it rules, or none.</summary>
    public static CasusBelli CasusBelliOf(TerritoryDispute d, string holder)
    {
        if (d == null || string.IsNullOrEmpty(holder)) return CasusBelli.None;
        bool others = d.shares.Any(s => !Same(s.holder, holder));
        if (d.cores.Any(c => Same(c, holder)) && others) return CasusBelli.Recover;
        if (Same(d.ruler, holder) && others) return CasusBelli.Integrate;
        return CasusBelli.None;
    }

    /// <summary>The disputes <paramref name="holder"/> has a grievance in, with how many hexes it resents (<see cref="TerritoryDispute.Grievance"/>).</summary>
    public static List<(TerritoryDispute dispute, int hexes)> Grievances(WorldMap map, string holder) =>
        Disputes(map).Select(d => (d, d.Grievance(holder))).Where(x => x.Item2 > 0).ToList();

    /// <summary>The disputes <paramref name="holder"/> could go to war over, and why.</summary>
    public static List<(TerritoryDispute dispute, CasusBelli why)> CasusBelliFor(WorldMap map, string holder) =>
        Disputes(map).Select(d => (d, CasusBelliOf(d, holder))).Where(x => x.Item2 != CasusBelli.None).ToList();

    /// <summary>Cores of <paramref name="holder"/> another holds any hex of (occupied in part, or ruled by the occupier): what it needs to recover.</summary>
    public static List<TerritoryDispute> Occupied(WorldMap map, string holder) =>
        Disputes(map).Where(d => d.cores.Any(c => Same(c, holder)) && d.shares.Any(s => !Same(s.holder, holder))).ToList();
}
