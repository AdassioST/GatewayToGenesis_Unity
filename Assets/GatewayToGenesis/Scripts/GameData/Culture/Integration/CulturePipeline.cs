using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The order in which the culture's Seventh is processed, once per Seventh, never by MonoBehaviour subscription order:
/// committed source actions, then the occurrences deduplicated, then local participation and contact, then the
/// traditions' lifecycle, then social effects, then what the player is told.
/// </summary>
public enum CulturePhase
{
    SourceActions,
    Dedupe,
    LocalParticipation,
    TraditionLifecycle,
    SocialEffects,
    Notifications,
}

/// <summary>Why a feature is asked to reconcile its derived state (effects, caches) with what is saved.</summary>
public enum CultureReconcileReason
{
    /// <summary>A save was restored: re-apply derived effects, drop caches; never grant rewards.</summary>
    Restored,
    /// <summary>The culture was just founded.</summary>
    Founded,
    /// <summary>The culture system started in a scene (a new game, or before a load).</summary>
    Started,
}

/// <summary>
/// A culture feature taking part in the Seventh (traditions, local cultures, memory, observances...). Any non-abstract
/// class with a parameterless constructor implementing it is found by <see cref="CultureFeatures"/> and run in
/// <see cref="Phase"/>, then <see cref="Order"/>, then type name: no shared registry to edit. Features keep their saved
/// state in their own part of <see cref="CultureExtensionState"/> and must be idempotent in <see cref="Reconcile"/>.
/// </summary>
public interface ICultureFeature
{
    /// <summary>Stable id ("traditions", "memory", "local").</summary>
    string Id { get; }
    CulturePhase Phase { get; }
    int Order { get; }
    /// <summary>The feature's part of a Seventh.</summary>
    void Seventh(CultureSeventh context);
    /// <summary>Re-apply derived effects and reset transient caches from saved state; never grant rewards or replay events.</summary>
    void Reconcile(CultureSystem culture, CultureReconcileReason reason);
}

/// <summary>What a feature sees of the Seventh being processed.</summary>
public sealed class CultureSeventh
{
    /// <summary>The culture system (null in pure tests).</summary>
    public CultureSystem Culture { get; }
    public CultureState State { get; }
    public CultureExtensionState Extensions => State?.extensions;
    public CultureStamp Now { get; }
    private readonly List<CulturalOccurrence> _occurrences;
    /// <summary>This Seventh's occurrences, deduplicated, in the order committed (a feature's own records during the Seventh are appended).</summary>
    public IReadOnlyList<CulturalOccurrence> Occurrences => _occurrences;
    /// <summary>Notices for the player, pushed after the last phase (title, text, key).</summary>
    public readonly List<(string title, string text, string key)> Notices = new List<(string, string, string)>();

    public CultureSeventh(CultureSystem culture, CultureState state, CultureStamp now, List<CulturalOccurrence> occurrences)
    {
        Culture = culture;
        State = state;
        Now = now;
        _occurrences = occurrences ?? new List<CulturalOccurrence>();
    }

    internal void Append(CulturalOccurrence o)
    {
        if (o != null) _occurrences.Add(o);
    }
}

/// <summary>The culture features of this build, found once by reflection and kept in their deterministic order.</summary>
public static class CultureFeatures
{
    private static List<Type> _types;

    /// <summary>Every feature type, sorted by phase, order and full name.</summary>
    public static IReadOnlyList<Type> Types
    {
        get
        {
            if (_types != null) return _types;
            var found = new List<(Type type, CulturePhase phase, int order)>();
            foreach (var type in typeof(ICultureFeature).Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(ICultureFeature).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null) continue;
                var probe = (ICultureFeature)Activator.CreateInstance(type);
                found.Add((type, probe.Phase, probe.Order));
            }
            _types = found.OrderBy(f => f.phase).ThenBy(f => f.order).ThenBy(f => f.type.FullName, StringComparer.Ordinal).Select(f => f.type).ToList();
            return _types;
        }
    }

    /// <summary>A fresh instance of every feature, in order (a culture system keeps its own set).</summary>
    public static List<ICultureFeature> Create() => Types.Select(t => (ICultureFeature)Activator.CreateInstance(t)).ToList();

    /// <summary>
    /// Run a Seventh through <paramref name="features"/> in order. A feature that throws is logged and skipped so one
    /// broken module cannot stop the others (the Seventh still ends).
    /// </summary>
    public static void Run(IEnumerable<ICultureFeature> features, CultureSeventh context, Action<ICultureFeature, Exception> failed = null)
    {
        foreach (var feature in features ?? Enumerable.Empty<ICultureFeature>())
        {
            try { feature.Seventh(context); }
            catch (Exception e) { failed?.Invoke(feature, e); }
        }
    }
}
