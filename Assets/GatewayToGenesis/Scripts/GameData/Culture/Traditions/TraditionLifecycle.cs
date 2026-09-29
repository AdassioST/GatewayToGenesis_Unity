using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The traditions' one lifecycle orchestrator: in the Seventh's <see cref="CulturePhase.TraditionLifecycle"/> phase it
/// feeds the Seventh's deduplicated occurrences to <see cref="TraditionRules.Advance"/> and says what changed. Pure (it
/// works on the culture's saved state), so a replay of the same history after a load gives the same traditions.
/// </summary>
public static class TraditionLifecycle
{
    public static List<TraditionChange> Run(CultureSeventh context, TraditionTuning tuning, Func<TraditionDefinition, bool> available)
    {
        if (context?.State == null || tuning == null) return new List<TraditionChange>();
        var x = CultureMigration.Ensure(context.State);
        return TraditionRules.Advance(x.traditions, tuning.definitions, context.Occurrences, context.State.sevenths, tuning, available);
    }

    /// <summary>The player-facing line for a change (null: nothing worth telling).</summary>
    public static (string title, string text)? Notice(TraditionChange change, TraditionDefinition d, string place, string people)
    {
        if (d == null || change.instance == null) return null;
        string where = string.IsNullOrEmpty(place) ? string.Empty : $" in {place}";
        switch (change.kind)
        {
            case TraditionChangeKind.Established:
                return ($"A custom: {d.name}", $"{Capital(people)} keep {d.name}{where} now. Recognise it for the nation, preserve it as a local custom, or decide later (Culture window: Traditions).");
            case TraditionChangeKind.Offered when !change.first:
                return ($"{d.name} waits", $"The decision about {d.name}{where} you put off is waiting again (Culture window: Traditions).");
            case TraditionChangeKind.Lapsed when change.instance.Established:
                return ($"{d.name} lies dormant", $"No one has kept {d.name}{where} for a long while: its benefits stop, but it is remembered and can be revived.");
            case TraditionChangeKind.Revived:
                return ($"{d.name} revived", $"{Capital(people)} have taken up {d.name}{where} again.");
            default:
                return null;
        }
    }

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}

/// <summary>The traditions' lifecycle, run in its phase of every Seventh.</summary>
public sealed class TraditionLifecycleFeature : ICultureFeature
{
    public string Id => "traditions";
    public CulturePhase Phase => CulturePhase.TraditionLifecycle;
    public int Order => 0;

    public void Seventh(CultureSeventh context)
    {
        var culture = context.Culture;
        var tuning = culture != null ? culture.TraditionTuning : new TraditionTuning();
        var changes = TraditionLifecycle.Run(context, tuning, culture != null ? (Func<TraditionDefinition, bool>)culture.TraditionAvailable : null);
        culture?.OnTraditionChanges(context, changes);
    }

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) => culture?.ReconcileTraditions();
}

/// <summary>The traditions' benefits and their lean on the culture, run in the social-effects phase of every Seventh.</summary>
public sealed class TraditionEffectsFeature : ICultureFeature
{
    public string Id => "tradition-effects";
    public CulturePhase Phase => CulturePhase.SocialEffects;
    public int Order => 0;

    public void Seventh(CultureSeventh context) => context.Culture?.ApplyTraditionEffects(true);

    public void Reconcile(CultureSystem culture, CultureReconcileReason reason) { }
}
