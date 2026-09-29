using System.Linq;

public partial class CultureSystem
{
    // A calendar host is explicitly chosen. It never steals a festival booking or invents a cast.
    private string WhyNotPerformanceHost(WorldUnit unit, string id, PerformanceIntent intent, PerformanceBooking booking = null)
    {
        var o = ObservanceOf(id);
        var world = WorldSystem.Instance;
        if (o == null || o.ended) return "The observance no longer exists.";
        if (unit == null || unit.Missing) return "The performing party is gone.";
        var place = world?.SettlementAt(unit);
        if (place == null || o.settlement != place.id) return "The party must remain at the observance's settlement.";
        if (unit.Moving || unit.Working) return "The party must be free, with no journey or work under way.";
        if (Voices(unit).Count == 0) return "No voices remain in the party.";
        if (intent == PerformanceIntent.Remembrance && o.objective != ObservanceObjective.Remembrance) return "This piece needs a remembrance observance.";
        if (o.objective != ObservanceObjective.Remembrance && o.objective != ObservanceObjective.Performance && o.objective != ObservanceObjective.Celebration)
            return "This observance is not a performance, celebration or remembrance.";
        // The public calendar shows dates strictly after today. On the booked day, inspect the
        // unresolved occasion instead, before the observance advances its saved cursor.
        var raw = ObservanceData.Get(id);
        bool due = booking != null && booking.occasion == Today && raw != null &&
            ((raw.postponedTo == Today && raw.postponedFrom > raw.lastResolved) ||
             (raw.lastResolved < Today && raw.postponedFrom != Today && ObservanceCalendar.Falls(raw, Today)));
        if (!due && (o.nextDate < Today || o.seventhsUntil > PerformanceTuning.planSevenths)) return "Plan within the performance planning window before the observance.";
        if (booking != null && !due && booking.occasion != o.nextDate) return "The observance's planned date changed or passed.";
        return null;
    }

    /// <summary>The calendar calls this before advancing the occasion cursor. The booking's single completion gate owns rewards.</summary>
    private void ResolveObservancePerformance(Observance o, ObservanceOccasion occasion, bool kept)
    {
        foreach (var booking in PerformanceData.bookings.Where(b => PerformanceRules.Active(b) && b.observance == o.id).ToList())
        {
            var unit = WorldSystem.Instance?.UnitById(booking.unit);
            string why = !kept ? "The observance was not kept." : WhyNotPerformanceHost(unit, o.id, booking.intent, booking);
            if (booking.occasion != occasion.date || booking.occasion != Today) why = "The booked occasion changed or was missed.";
            if (why != null)
            {
                PerformanceRules.End(PerformanceData, booking, PerformanceStatus.Cancelled, why, PieceName(booking.repertoire), _state.sevenths, AgeIdNow, PerformanceTuning);
                continue;
            }
            CompletePerformance(unit, WorldSystem.Instance.SettlementAt(unit), Voices(unit), o.id);
        }
    }
}
