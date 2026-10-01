using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Battlefield lanes, distinct from a section's front/back/support role.</summary>
public enum BattleLane { Top, Middle, Bottom }
public enum BattleTerritory { Attacker, Neutral, Defender }
/// <summary>Line is the default for existing formations. Append new doctrines to preserve saved values.</summary>
public enum BattleStance { Line, Spearhead, Crescent }
public enum BattleScale { EliteEngagement, MicroEngagement, FormationEngagement, MediumArmy, GrandArmy }

public static class BattleScaleRules
{
    public static BattleScale ForSize(int combatants)
    {
        if (combatants < 1) throw new ArgumentOutOfRangeException(nameof(combatants));
        if (combatants <= 10) return BattleScale.EliteEngagement;
        if (combatants <= 100) return BattleScale.MicroEngagement;
        if (combatants <= 999) return BattleScale.FormationEngagement;
        if (combatants <= 10000) return BattleScale.MediumArmy;
        return BattleScale.GrandArmy;
    }
}

/// <summary>Doctrine matchup only; positional bonuses and terrain adjustments belong to the spatial resolver.</summary>
public static class BattleStances
{
    public static bool Counters(BattleStance stance, BattleStance other) =>
        (stance == BattleStance.Spearhead && other == BattleStance.Line) ||
        (stance == BattleStance.Line && other == BattleStance.Crescent) ||
        (stance == BattleStance.Crescent && other == BattleStance.Spearhead);
}

/// <summary>An immutable hex in the document's 5 / 6 / 5 layout. Rank 1 is front, rank 2 rear, 0 neutral.</summary>
public sealed class BattleHex
{
    public int Id { get; }
    public BattleLane Lane { get; }
    public int Column { get; }
    public int X { get; }
    public BattleTerritory Territory { get; }
    public int Rank { get; }

    internal BattleHex(int id, BattleLane lane, int column, int x, BattleTerritory territory, int rank)
    {
        Id = id; Lane = lane; Column = column; X = x; Territory = territory; Rank = rank;
    }
}

/// <summary>
/// The sixteen primary combat hexes: F F N E E / F F N N E E / F F N E E.
/// Doubled horizontal coordinates give the Middle lane its half-hex offset. Neighboring hexes share an edge;
/// the two Middle neutral hexes are separate, adjacent positions. Geometry is shared by both resolution modes.
/// </summary>
public static class BattleHexLayout
{
    public const int HexCount = 16;
    public static readonly IReadOnlyList<BattleHex> Hexes = Array.AsReadOnly(Build());
    private static readonly IReadOnlyList<int>[] Neighbors = BuildNeighbors();

    private static BattleHex[] Build()
    {
        var hexes = new List<BattleHex>();
        foreach (BattleLane lane in Enum.GetValues(typeof(BattleLane)))
        {
            int length = lane == BattleLane.Middle ? 6 : 5;
            for (int c = 0; c < length; c++)
            {
                var territory = c < 2 ? BattleTerritory.Attacker : c >= length - 2 ? BattleTerritory.Defender : BattleTerritory.Neutral;
                int rank = territory == BattleTerritory.Neutral ? 0 : c == 0 || c == length - 1 ? 2 : 1;
                hexes.Add(new BattleHex(hexes.Count, lane, c, 2 * c - (lane == BattleLane.Middle ? 1 : 0), territory, rank));
            }
        }
        return hexes.ToArray();
    }

    private static IReadOnlyList<int>[] BuildNeighbors() => Hexes.Select(h =>
        (IReadOnlyList<int>)Array.AsReadOnly(Hexes.Where(n =>
            (n.Lane == h.Lane && Math.Abs(n.X - h.X) == 2) ||
            (Math.Abs((int)n.Lane - (int)h.Lane) == 1 && Math.Abs(n.X - h.X) == 1))
            .Select(n => n.Id).ToArray())).ToArray();

    public static BattleHex At(int id)
    {
        if (id < 0 || id >= HexCount) throw new ArgumentOutOfRangeException(nameof(id));
        return Hexes[id];
    }

    public static IReadOnlyList<int> Adjacent(int id) { At(id); return Neighbors[id]; }
    public static bool AreAdjacent(int first, int second) { At(second); return Adjacent(first).Contains(second); }
    public static bool IsNative(int id, bool attacker) => At(id).Territory == (attacker ? BattleTerritory.Attacker : BattleTerritory.Defender);
    public static IEnumerable<BattleHex> Native(bool attacker, int rank = 0) => Hexes.Where(h => IsNative(h.Id, attacker) && (rank == 0 || h.Rank == rank));

    /// <summary>Graph distance, including both Middle neutral hexes. Terrain does not change geometric distance.</summary>
    public static int Distance(int first, int second)
    {
        At(first); At(second);
        var distances = Enumerable.Repeat(-1, HexCount).ToArray();
        var queue = new Queue<int>();
        distances[first] = 0;
        queue.Enqueue(first);
        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            if (id == second) return distances[id];
            foreach (int next in Adjacent(id))
            {
                if (distances[next] >= 0) continue;
                distances[next] = distances[id] + 1;
                queue.Enqueue(next);
            }
        }
        throw new InvalidOperationException("The combat battlefield must be connected.");
    }
}

/// <summary>A sparse per-hex override on Battlefield; at runtime every hex has one terrain record.</summary>
[Serializable]
public sealed class BattleHexTerrain
{
    public int veil;
    public int hex;
    public BattleGround ground;
    public bool blocked, river;
    public bool ford, bridge, wall, highGround, forest, snow, narrowPass, leyline, harmonicChannel;
    public float elevation, coherence, dissonance;
    public bool overrideLoom;
    public BattleHexTerrain Clone() => (BattleHexTerrain)MemberwiseClone();
}

/// <summary>
/// Spatial view of the actual BattleRun sides, not a second copy of their Integrity or Composure.
/// Geometry, terrain and occupancy are shared by the spatial rules, movement and Stance cascade.
/// </summary>
public sealed class BattleSpatialState
{
    public BattleSide Attacker { get; }
    public BattleSide Defender { get; }
    public IReadOnlyList<BattleHexTerrain> Terrain { get; }

    internal BattleSpatialState(BattleSide attacker, BattleSide defender, Battlefield field, CombatSettings settings = null)
    {
        Attacker = attacker ?? throw new ArgumentNullException(nameof(attacker));
        Defender = defender ?? throw new ArgumentNullException(nameof(defender));
        field = field ?? new Battlefield();
        var terrain = BattleHexLayout.Hexes.Select(h => new BattleHexTerrain
        {
            hex = h.Id,
            ground = h.Territory == BattleTerritory.Attacker ? field.attackerGround ?? field.ground : field.ground,
            river = h.Territory == BattleTerritory.Neutral && field.riverCrossing,
            coherence = field.coherence, dissonance = field.dissonance, leyline = field.leyline,
            forest = (h.Territory == BattleTerritory.Attacker ? field.attackerGround ?? field.ground : field.ground) == BattleGround.Forest,
        }).ToArray();
        var overridden = new HashSet<int>();
        foreach (var entry in field.hexes ?? new List<BattleHexTerrain>())
        {
            if (entry == null) throw new ArgumentException("A battlefield terrain override cannot be null.", nameof(field));
            BattleHexLayout.At(entry.hex);
            if (!overridden.Add(entry.hex)) throw new ArgumentException("A battlefield hex has duplicate terrain overrides.", nameof(field));
            terrain[entry.hex] = entry.Clone();
        }
        Terrain = Array.AsReadOnly(terrain);
        ValidatePositions(attacker);
        ValidatePositions(defender);
        BattleDeploymentLogic.Deploy(attacker, true, Terrain, BattleDeploymentLogic.Doctrine(settings, attacker));
        BattleDeploymentLogic.Deploy(defender, false, Terrain, BattleDeploymentLogic.Doctrine(settings, defender));
    }

    private void ValidatePositions(BattleSide side)
    {
        foreach (var section in side.sections.Where(s => s.Standing))
        {
            if (section.battleHex == -1) continue;
            BattleHexLayout.At(section.battleHex);
            if (Terrain[section.battleHex].blocked || Terrain[section.battleHex].wall) throw new ArgumentException($"{section.name} occupies blocked combat ground.");
        }
    }

    public BattleSide Side(bool attacker) => attacker ? Attacker : Defender;
    public BattleScale Scale(bool attacker) => BattleScaleRules.ForSize(Side(attacker).CombatantCount);
    public IEnumerable<CombatSection> Occupants(int hex, bool attacker)
    {
        BattleHexLayout.At(hex);
        return Side(attacker).sections.Where(s => s.Standing && s.battleHex == hex);
    }
    public bool IsStacked(int hex, bool attacker) => Occupants(hex, attacker).Skip(1).Any();
    public bool IsEngaged(int hex) => Occupants(hex, true).Any() && Occupants(hex, false).Any();
}
