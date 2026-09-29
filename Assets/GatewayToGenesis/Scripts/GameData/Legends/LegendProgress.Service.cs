using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A legend's service: what it has become (its stars in the Greats, <see cref="LegendGreats"/>), the seats it has earned,
/// the conditions it carries (<see cref="LegendConditions"/>), and what battles do to it: the fragments, the strain,
/// going missing in action after a doomed battle and turning up in a settlement Sevenths later.
/// </summary>
public partial class LegendProgress
{
    /// <summary>A legend went missing in action: (legend, where).</summary>
    public event Action<string, string> WentMissing;
    /// <summary>A legend missing in action turned up in a settlement: (legend, settlement).</summary>
    public event Action<string, string> Returned;
    /// <summary>A condition was given, or ran out: (legend, condition).</summary>
    public event Action<string, LegendCondition> ConditionAdded, ConditionEnded;

    // ===== THE GREATS =====

    /// <summary>A legend's stars in each Great it has earned one in (empty: an Unattuned Legend).</summary>
    public Dictionary<LegendClass, int> Greats(string legendName) => LegendGreats.Standing(Purse(legendName), FragmentRules);

    public int Stars(string legendName, LegendClass great) => LegendGreats.Stars(Purse(legendName), great, FragmentRules);

    /// <summary>"3★ Great Sovereign · 2★ Great Vanguard", or "Unattuned Legend".</summary>
    public string GreatsTitle(string legendName) => LegendGreats.Title(Purse(legendName), FragmentRules);

    /// <summary>Whether a legend holds at least <paramref name="stars"/> in one of <paramref name="greats"/> (0: any legend).</summary>
    public bool Meets(string legendName, IEnumerable<LegendClass> greats, int stars) => LegendGreats.Meets(Purse(legendName), greats, stars, FragmentRules);

    /// <summary>Every seated legend earns fragments toward its seat's main Great (an Act of Fate): serving makes the Great.</summary>
    public void ServeSeats(string deed)
    {
        var government = GovernmentLogic.Instance;
        int amount = FragmentRules.seatService;
        if (government == null || amount <= 0) return;
        foreach (var (seat, legend) in government.GetAllAssignedLegends().ToList())
        {
            if (seat?.allowedLegendClasses == null || seat.allowedLegendClasses.Count == 0 || legend == null) continue;
            var great = seat.allowedLegendClasses[0];
            Award(legend.legendName, LegendGreats.KindOf(great), amount, $"{deed} as {seat.GetEffectiveTitle()}");
        }
    }

    // ===== IN BATTLE =====

    /// <summary>The legend as it fights (null when not met, lost, or missing in action).</summary>
    public BattleLegend BattleLegendOf(string legendName) =>
        !IsRecruited(legendName) || IsMissing(legendName) ? null : BattleLegend.Of(legendName, Soul(legendName), Bindings(legendName), Greats(legendName), Tuning);

    /// <summary>
    /// What a battle did to a legend (<see cref="BattleReport.legends"/>): its fragments, its conditions, and its strain,
    /// now, or when it turns up in a settlement if it went missing in action.
    /// </summary>
    public void ApplyBattleFate(LegendBattleFate fate)
    {
        if (fate == null || !IsRecruited(fate.name)) return;
        if (fate.fragments.Count > 0) Award(fate.name, fate.fragments, fate.deed);
        else if (!string.IsNullOrEmpty(fate.deed) && _recruited.TryGetValue(fate.name, out var r)) r.deeds.Add(fate.deed);
        foreach (var c in fate.conditions) AddCondition(fate.name, c.id, c.sevenths, c.source);
        if (fate.missing) GoMissing(fate.name, fate.missingSevenths, fate.strain, fate.section);
        else Strain(fate.name, fate.strain, fate.won ? "the weight of battle" : "a lost battle");
    }

    // ===== CONDITIONS =====

    public IReadOnlyList<LegendCondition> Conditions(string legendName) =>
        legendName != null && _recruited.TryGetValue(legendName, out var record) && record.lastingConditions != null ? record.lastingConditions : (IReadOnlyList<LegendCondition>)Array.Empty<LegendCondition>();

    public bool HasCondition(string legendName, string id) => LegendConditions.Has(Conditions(legendName), id);

    /// <summary>Gives a legend a condition for <paramref name="sevenths"/> Sevenths (0: until removed). The open end for other systems.</summary>
    public LegendCondition AddCondition(string legendName, string id, int sevenths, string source)
    {
        if (legendName == null || !_recruited.TryGetValue(legendName, out var record) || record.lost) return null;
        if (record.lastingConditions == null) record.lastingConditions = new List<LegendCondition>();
        var c = LegendConditions.Add(record.lastingConditions, id, sevenths, source);
        if (c == null) return null;
        GameLog.Event($"{legendName}: {LegendConditions.Describe(c)} ({source})", Log);
        ConditionAdded?.Invoke(legendName, c);
        Changed?.Invoke();
        return c;
    }

    public bool RemoveCondition(string legendName, string id)
    {
        if (legendName == null || !_recruited.TryGetValue(legendName, out var record)) return false;
        bool removed = LegendConditions.Remove(record.lastingConditions, id);
        if (removed) Changed?.Invoke();
        return removed;
    }

    // ===== MISSING IN ACTION =====

    public bool IsMissing(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record) && !record.lost && (record.missingSevenths > 0 || record.heldBy != null);

    // ===== CAPTIVES =====

    /// <summary>Held captive by an Atonalis (<see cref="TakeCaptive"/>): off the roster and off the map until freed.</summary>
    public bool IsCaptive(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record) && !record.lost && record.heldBy != null;

    /// <summary>Who holds the legend (the captor's key), or null.</summary>
    public string CaptorOf(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record) && !record.lost ? record.heldBy : null;

    /// <summary>Every legend held captive now.</summary>
    public IEnumerable<string> CaptiveNames => _recruited.Where(r => !r.Value.lost && r.Value.heldBy != null).Select(r => r.Key);

    /// <summary>
    /// An Atonalis took the legend captive after a battle (every Path but Carnalix): it leaves its seat and does not come
    /// home by itself. While held it is fed upon (<paramref name="feedPerSeventh"/> strain each Seventh; its Composure may
    /// break), until the band holding it (<paramref name="captor"/>) is destroyed and it is freed (<see cref="Free"/>).
    /// </summary>
    public void TakeCaptive(string legendName, string captor, float feedPerSeventh, float strainNow, string from)
    {
        if (legendName == null || captor == null || !_recruited.TryGetValue(legendName, out var record) || record.lost) return;
        record.heldBy = captor;
        record.heldFeed = Math.Max(0f, feedPerSeventh);
        record.missingSevenths = 0;
        record.missingStrain += Math.Max(0f, strainNow);
        record.missingFrom = from;
        record.deeds.Add($"Taken captive by {(string.IsNullOrEmpty(from) ? "the Atonalis" : from)}");
        var government = GovernmentLogic.Instance;
        var seat = government != null ? government.GetSeatWithLegend(legendName) : null;
        if (seat != null) government.RemoveLegendFromSeat(seat.seatIndex, bypassCooldown: true);
        GameLog.Event($"{legendName} is taken captive by {from}", Log);
        NotificationFeed.Push($"{legendName} is taken captive", $"{from} dragged {legendName} away. It will feed on them until the band that holds them is destroyed.",
            NotificationFeed.Topic.Council, key: "captive:" + legendName);
        WentMissing?.Invoke(legendName, from);
        NotifyRosterChanged();
    }

    /// <summary>The captor fell: the legend is free and turns up in a settlement after <paramref name="sevenths"/> Sevenths, carrying what it endured.</summary>
    public void Free(string legendName, int sevenths, string by)
    {
        if (legendName == null || !_recruited.TryGetValue(legendName, out var record) || record.lost || record.heldBy == null) return;
        record.heldBy = null;
        record.heldFeed = 0f;
        record.missingSevenths = Math.Max(1, sevenths);
        record.deeds.Add($"Freed from captivity{(string.IsNullOrEmpty(by) ? string.Empty : " by " + by)}");
        GameLog.Event($"{legendName} is freed{(string.IsNullOrEmpty(by) ? string.Empty : " by " + by)}", Log);
        NotificationFeed.Push($"{legendName} is freed", $"{legendName} is free{(string.IsNullOrEmpty(by) ? string.Empty : ", thanks to " + by)}, and makes for home.",
            NotificationFeed.Topic.Council, key: "freed:" + legendName);
        NotifyRosterChanged();
    }

    public int MissingSevenths(string legendName) => legendName != null && _recruited.TryGetValue(legendName, out var record) ? Math.Max(0, record.missingSevenths) : 0;

    /// <summary>
    /// A legend left a doomed battle alone and is missing in action: it leaves its seat, and turns up in a settlement
    /// after <paramref name="sevenths"/> Sevenths carrying <paramref name="strainOnReturn"/>.
    /// </summary>
    public void GoMissing(string legendName, int sevenths, float strainOnReturn, string from)
    {
        if (legendName == null || !_recruited.TryGetValue(legendName, out var record) || record.lost) return;
        record.missingSevenths = Math.Max(1, Math.Max(record.missingSevenths, sevenths));
        record.missingStrain += Math.Max(0f, strainOnReturn);
        record.missingFrom = from;
        record.deeds.Add($"Missing in action after {(string.IsNullOrEmpty(from) ? "a lost battle" : from)}");
        var government = GovernmentLogic.Instance;
        var seat = government != null ? government.GetSeatWithLegend(legendName) : null;
        if (seat != null) government.RemoveLegendFromSeat(seat.seatIndex, bypassCooldown: true);
        GameLog.Event($"{legendName} is missing in action ({record.missingSevenths} Sevenths)", Log);
        NotificationFeed.Push($"{legendName} is missing in action", $"When the battle was lost, {legendName} left the rest behind and slipped away alone. No one knows where they are.",
            NotificationFeed.Topic.Council, key: "missing:" + legendName);
        WentMissing?.Invoke(legendName, from);
        NotifyRosterChanged();
    }

    private void TurnUp(string legendName, Record record)
    {
        record.missingSevenths = 0;
        float strain = record.missingStrain;
        record.missingStrain = 0f;
        var world = WorldSystem.Instance != null && WorldSystem.Instance.Map != null ? WorldSystem.Instance.Map : null;
        string where = world != null ? WorldCivilization.Capital(world)?.name : null;
        where = string.IsNullOrEmpty(where) ? "one of your settlements" : where;
        record.deeds.Add($"Came home to {where} after going missing");
        GameLog.Event($"{legendName} turns up at {where}", Log);
        NotificationFeed.Push($"{legendName} returns", $"{legendName} turns up at {where}, alone and worn, with the weight of those left behind.",
            NotificationFeed.Topic.Council, key: "returned:" + legendName);
        Returned?.Invoke(legendName, where);
        NotifyRosterChanged();
        Strain(legendName, strain, "came home from missing in action");
    }

    /// <summary>Once a Seventh: conditions count down, and legends missing in action come closer to home.</summary>
    private bool TickService()
    {
        bool dirty = false;
        foreach (var entry in _recruited.Where(r => !r.Value.lost).ToList())
        {
            var record = entry.Value;
            foreach (var ended in LegendConditions.Tick(record.lastingConditions))
            {
                GameLog.Event($"{entry.Key} is no longer {LegendConditions.Name(ended.id)}", Log);
                ConditionEnded?.Invoke(entry.Key, ended);
            }
            // A captive is fed upon while it is held (its Composure may break: lost to Dissonance).
            if (record.heldBy != null)
            {
                if (record.heldFeed > 0f) Strain(entry.Key, record.heldFeed, "held captive by the Atonalis");
                continue;
            }
            if (record.missingSevenths > 0 && --record.missingSevenths == 0)
            {
                TurnUp(entry.Key, record);
                dirty = true;
            }
        }
        return dirty;
    }
}
