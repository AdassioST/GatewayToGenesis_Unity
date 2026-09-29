using System.Collections.Generic;

/// <summary>
/// The festival's performance adapter (T08): the festival stays the single completion owner (FinishFestival); at its
/// end it hands the culture the party, the place and the voices still with it, once, and the culture gives the planned
/// performance (<see cref="CultureSystem.CompletePerformance"/>). Nothing here grants a reward of its own.
/// </summary>
public partial class WorldSystem
{
    /// <summary>What the festival's notice adds for the party's planned performance, or null (none planned).</summary>
    private string CompleteCulturalPerformance(WorldUnit unit, Settlement s, IList<string> members) =>
        CultureSystem.Instance != null ? CultureSystem.Instance.CompletePerformance(unit, s, members) : null;
}
