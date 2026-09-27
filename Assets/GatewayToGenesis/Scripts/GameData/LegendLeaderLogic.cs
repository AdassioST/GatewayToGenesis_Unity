using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The legend roster and the council activation delay.
///
/// Legends are read from <see cref="GameCatalog.Legends"/> (Resources/Legends): add a LegendData asset and it
/// is available everywhere, once the civilization has met it (<see cref="LegendProgress"/>: a few are known from the
/// start, the rest are found on the world map). Seating, activation timers and bonuses belong to <see cref="GovernmentLogic"/>;
/// the methods here are conveniences that forward to it.
/// </summary>
public class LegendLeaderLogic : SingletonBehaviour<LegendLeaderLogic>
{
    private const LogChannel Log = LogChannel.Council;

    [Header("Council Assignment")]
    [Tooltip("Sevenths a newly seated legend waits before its bonuses apply (1 seventh is about 3 minutes).")]
    [SerializeField] private int seventhsForActivation = 3;

    private void Update()
    {
        if (InputUtils.DebugKeyDown(Key.L)) PrintLegendInfo();
    }

    // ===== ROSTER =====

    /// <summary>The legends the civilization has met and has at home (every legend when <see cref="LegendProgress"/> is absent).</summary>
    public List<LegendData> GetAvailableLegends() => Met().ToList();

    public List<LegendData> GetLegendsByClass(LegendClass legendClass) => Met().Where(l => l.legendClass == legendClass).ToList();

    // Met, not lost, and not away with an expedition (a legend on the road cannot sit on the council).
    private static IEnumerable<LegendData> Met()
    {
        var progress = LegendProgress.Instance;
        var world = WorldSystem.Instance != null && WorldSystem.Instance.Map != null ? WorldSystem.Instance : null;
        return GameCatalog.Legends.All.Where(l => l != null && (progress == null || progress.IsRecruited(l.legendName)) && (world == null || world.ExpeditionOf(l.legendName) == null));
    }

    /// <summary>Unseated legends that qualify for a seat.</summary>
    public List<LegendData> GetCompatibleLegends(int seatIndex)
    {
        var seat = GovernmentLogic.Instance != null ? GovernmentLogic.Instance.GetCouncilSeat(seatIndex) : null;
        if (seat == null) return new List<LegendData>();
        return Met().Where(l => seat.CanAssignLegend(l) && !IsLegendAssigned(l.legendName)).ToList();
    }

    // ===== ASSIGNMENT (forwarded to GovernmentLogic) =====

    public bool AssignLegendToSeat(LegendData legend, int seatIndex) => GovernmentLogic.Instance != null && GovernmentLogic.Instance.AssignLegendToSeat(legend, seatIndex);

    public bool RemoveLegendFromSeat(int seatIndex) => GovernmentLogic.Instance != null && GovernmentLogic.Instance.RemoveLegendFromSeat(seatIndex);

    public bool IsLegendAssigned(string legendName) => GetLegendSeatIndex(legendName) != int.MinValue;

    /// <summary>Seat index of a legend (-1 is the Head of State), or int.MinValue when unseated.</summary>
    public int GetLegendSeatIndex(string legendName)
    {
        var seat = GovernmentLogic.Instance != null ? GovernmentLogic.Instance.GetSeatWithLegend(legendName) : null;
        return seat != null ? seat.seatIndex : int.MinValue;
    }

    public Dictionary<int, LegendData> GetAssignedLegends()
    {
        var result = new Dictionary<int, LegendData>();
        if (GovernmentLogic.Instance == null) return result;
        foreach (var (seat, legend) in GovernmentLogic.Instance.GetAllAssignedLegends()) result[seat.seatIndex] = legend;
        return result;
    }

    // ===== ACTIVATION DELAY =====

    public int GetSeventhsForActivation() => seventhsForActivation;

    public void SetSeventhsForActivation(int newSevenths)
    {
        seventhsForActivation = Mathf.Max(0, newSevenths);
        GameLog.Event($"Sevenths for activation set to {seventhsForActivation}", Log);
    }

    // ===== DEBUG =====

    [ContextMenu("Print Legend Info")]
    public void PrintLegendInfo()
    {
        if (!GameLog.IsEnabled(Log)) return;
        GameLog.Event($"=== LEGENDS ({GameCatalog.Legends.Count}) ===", Log);
        foreach (var legend in GameCatalog.Legends.All)
        {
            int seat = GetLegendSeatIndex(legend.legendName);
            string where = seat == int.MinValue ? "unseated" : seat == GovernmentLogic.HeadOfStateIndex ? "Head of State" : $"seat {seat}";
            GameLog.Event($"  {legend.legendName} ({legend.legendClass}, {legend.rarity}) - {where}", Log);
            foreach (var bonus in legend.bonuses.Where(b => b != null)) GameLog.Event($"    {bonus.ToEffect().Describe()}", Log);
        }

        var stats = StatManager.Instance;
        if (stats == null) return;
        foreach (var stat in stats.GetAllStatNames())
        {
            foreach (var source in stats.GetBonusSources(stat).Where(s => s.Key.StartsWith("Legend:") || s.Key.StartsWith("Council Seat:")))
            {
                GameLog.Event($"  {stat}: {source.Value.Flat:+0.##;-0.##;0} / {source.Value.Percent:+0.##;-0.##;0}% from {source.Key}", Log);
            }
        }
    }
}
