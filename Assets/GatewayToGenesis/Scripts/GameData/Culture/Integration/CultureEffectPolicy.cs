using System.Collections.Generic;
using System.Linq;

/// <summary>
/// How culture features apply game effects (the effect-source policy every culture task follows):
/// - One named source per thing that gives (a tradition's definition, a memorial, an observance), always applied whole
///   with <see cref="EffectRouter.ApplySet"/>, so re-applying never stacks and removing is one call.
/// - Sources are "Culture: " + the thing's authored name; a feature re-applies every source it could own (empty when
///   inactive), so a load or a lapse never leaves a stale contribution behind.
/// - Derived effects are re-applied from saved state in <see cref="ICultureFeature.Reconcile"/> after a load, never by
///   replaying the events that earned them; rewards (resources, moments, achievements) are granted once, gated by
///   saved milestones.
/// - Effects are small, capped per feature (traditions: <see cref="TraditionTuning.maxActiveBenefits"/>) and never
///   multiplied per settlement.
/// </summary>
public static class CultureEffectPolicy
{
    public const string Prefix = "Culture: ";

    public static string Source(string name) => Prefix + name;

    /// <summary>
    /// Apply each source's effects (an empty list removes the source) when <paramref name="key"/> differs from the last
    /// applied key; returns the key to keep. Pass a null last key to force it (after a load).
    /// </summary>
    public static string ApplyAll(string lastKey, IEnumerable<(string source, List<GameEffect> effects)> sources)
    {
        var list = sources.ToList();
        string key = string.Join("|", list.Select(s => s.source + ":" + string.Join(",", s.effects.Select(e => e.ToString()))));
        if (key == lastKey) return lastKey;
        foreach (var (source, effects) in list) EffectRouter.ApplySet(source, effects);
        return key;
    }
}
