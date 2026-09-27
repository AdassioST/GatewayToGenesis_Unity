using System;
using System.Collections.Generic;

/// <summary>
/// The rules of the council, with no scene state. <see cref="GovernmentLogic"/> owns the seats, legends and
/// cooldown timers and asks these rules what they mean; every rule here is tested in <c>CoreSystemsTests</c>.
///
/// Timing: a seat's own bonuses apply the moment a legend sits in it; the legend's bonuses apply once it has
/// finished activating (<see cref="CouncilSeat.seventhsUntilActive"/> reaches 0). An empty seat gives nothing.
///
/// Scaling, in three phases so Legend Effectiveness (LE) is settled before it scales anything:
///   Authority            legend bonuses × Head of State multiplier × growth
///   LegendEffectiveness  legend bonuses × Head of State multiplier × growth
///   Scaled               legend bonuses × Head of State multiplier × growth × (1 + LE%)
/// Growth is the legend's rank (<see cref="LegendGrowthRules"/>) times its Composure (<see cref="ComposureRules.CouncilFactor"/>).
/// Seat bonuses are never scaled.
/// </summary>
public static class CouncilRules
{
    public enum Phase { Authority, LegendEffectiveness, Scaled }

    /// <summary>Why a legend assignment does or does not happen, and how.</summary>
    public enum Assignment
    {
        /// <summary>The target seat is on cooldown.</summary>
        TargetOnCooldown,
        /// <summary>The seat is locked or the legend does not qualify for it.</summary>
        NotEligible,
        /// <summary>The legend already sits there: nothing to do.</summary>
        AlreadySeated,
        /// <summary>The legend sits in another seat that is on cooldown, so it cannot leave.</summary>
        PreviousSeatOnCooldown,
        /// <summary>The two legends trade seats.</summary>
        Swap,
        /// <summary>The legend takes the seat (leaving its previous seat, unseating any occupant).</summary>
        Move
    }

    /// <summary>A seat's bonuses apply as soon as a legend sits in it.</summary>
    public static bool SeatBonusesApply(bool hasLegend) => hasLegend;

    /// <summary>A legend's bonuses apply once its activation countdown has finished.</summary>
    public static bool LegendBonusesApply(bool hasLegend, int seventhsUntilActive) => hasLegend && seventhsUntilActive <= 0;

    /// <summary>What <see cref="GovernmentLogic"/> knows about one seated position.</summary>
    public struct SeatState
    {
        public string seatSource;
        public IEnumerable<SeatBonus> seatBonuses;
        public bool hasLegend;
        public string legendSource;
        public IEnumerable<LegendBonus> legendBonuses;
        public int seventhsUntilActive;
        public bool isHeadOfState;
        /// <summary>The legend's growth from its rank (<see cref="LegendGrowthRules.Multiplier"/>) and its Composure
        /// (<see cref="LegendProgress.CouncilMultiplier"/>); 0 or less counts as 1.</summary>
        public float legendGrowth;
    }

    public readonly struct CouncilEffect
    {
        public readonly string source;
        public readonly GameEffect effect;
        /// <summary>Head of State multiplier × growth for a legend bonus; 0 marks a seat bonus, which is never scaled.</summary>
        public readonly float legendMultiplier;

        public CouncilEffect(string source, GameEffect effect, float legendMultiplier)
        {
            this.source = source;
            this.effect = effect;
            this.legendMultiplier = legendMultiplier;
        }

        public bool IsLegendBonus => legendMultiplier > 0f;
        public Phase Phase => PhaseOf(effect);
    }

    /// <summary>Every effect the council gives right now, by the timing rules above.</summary>
    public static List<CouncilEffect> Collect(IEnumerable<SeatState> seats, float headOfStateMultiplier)
    {
        var effects = new List<CouncilEffect>();
        foreach (var seat in seats)
        {
            if (SeatBonusesApply(seat.hasLegend) && seat.seatBonuses != null)
            {
                foreach (var bonus in seat.seatBonuses)
                {
                    if (bonus != null && bonus.bonusType != SeatBonusType.CivicBonus) effects.Add(new CouncilEffect(seat.seatSource, bonus.ToEffect(), 0f));
                }
            }
            if (LegendBonusesApply(seat.hasLegend, seat.seventhsUntilActive) && seat.legendBonuses != null)
            {
                float multiplier = (seat.isHeadOfState ? headOfStateMultiplier : 1f) * (seat.legendGrowth > 0f ? seat.legendGrowth : 1f);
                foreach (var bonus in seat.legendBonuses)
                {
                    if (bonus != null) effects.Add(new CouncilEffect(seat.legendSource, bonus.ToEffect(), multiplier));
                }
            }
        }
        return effects;
    }

    public static Phase PhaseOf(in GameEffect effect)
    {
        if (effect.type == GameEffectType.SubstatBonus && string.Equals(effect.target, "authority", StringComparison.OrdinalIgnoreCase)) return Phase.Authority;
        if (effect.type == GameEffectType.DerivedStatBonus && string.Equals(effect.target, "legendEffectiveness", StringComparison.OrdinalIgnoreCase)) return Phase.LegendEffectiveness;
        return Phase.Scaled;
    }

    /// <summary>1 + LE% (LE rounded, never below 0).</summary>
    public static float EffectivenessMultiplier(float legendEffectiveness) => 1f + Math.Max(0, (int)Math.Round(legendEffectiveness)) / 100f;

    /// <summary>The multiplier an effect is applied with, given the Legend Effectiveness multiplier of this pass.</summary>
    public static float Multiplier(in CouncilEffect entry, float effectivenessMultiplier)
    {
        if (!entry.IsLegendBonus) return 1f;
        return entry.Phase == Phase.Scaled ? entry.legendMultiplier * effectivenessMultiplier : entry.legendMultiplier;
    }

    /// <summary>How long a position is locked after its legend changes.</summary>
    public static int CooldownFor(int seatIndex, int seatCooldownSevenths, int headOfStateCooldownSevenths)
    {
        return seatIndex == GovernmentLogic.HeadOfStateIndex ? headOfStateCooldownSevenths : seatCooldownSevenths;
    }

    /// <summary>
    /// Decide how seating a legend plays out. Checked in order: the target's cooldown, eligibility, already
    /// seated there, the cooldown of the seat it would leave, then swap (target occupied and its occupant
    /// qualifies for the legend's previous seat) or move.
    /// </summary>
    public static Assignment PlanAssignment(bool targetOnCooldown, bool eligible, bool alreadyInTarget,
        bool hasPreviousSeat, bool previousOnCooldown, bool targetOccupied, bool occupantFitsPreviousSeat)
    {
        if (targetOnCooldown) return Assignment.TargetOnCooldown;
        if (!eligible) return Assignment.NotEligible;
        if (alreadyInTarget) return Assignment.AlreadySeated;
        if (hasPreviousSeat && previousOnCooldown) return Assignment.PreviousSeatOnCooldown;
        if (hasPreviousSeat && targetOccupied && occupantFitsPreviousSeat) return Assignment.Swap;
        return Assignment.Move;
    }
}
