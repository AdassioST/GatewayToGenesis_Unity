using System;
using System.Collections.Generic;

/// <summary>
/// What an Age allows, gated separately per capability (X05). Content asks with the "capability" condition value
/// (<see cref="GameValues"/>) or <see cref="IsAvailable(string)"/>; it never infers one capability from another.
///
/// The owner's correction D13 (2026-09-27): legends can acquire Ornaments in Age 0. What the Age of Desolation locks is
/// Ornamental Magic (Triad and most Dyad Chords) in Symphony of War and the other symphonic cards, so a legend's own
/// Ornament never unlocks that magic, and the magic's lock never blocks Motif Awakening. Settlement Ornaments are not
/// an Age capability at all: they need The Truth of Arcanoria and three complete Ages of presence (U02/U06).
/// </summary>
public static class AgeCapabilities
{
    /// <summary>A legend's Motif Awakening to an Ornament (D13: from Age 0).</summary>
    public const string LegendOrnament = "legend-ornament";
    /// <summary>Ornamental Magic in Symphony of War and other symphonic cards.</summary>
    public const string OrnamentalMagic = "ornamental-magic";
    /// <summary>Minor Unisons anyone can cast by instinct (Age 0: "Mostly Minor Note magic").</summary>
    public const string UnisonMinor = "unison-minor";
    /// <summary>A Major Unison sounded by an Awakened Legend, unreliably (Age 0: only strong Motif Awakenings manifest magic).</summary>
    public const string UnisonMajorAwakened = "unison-major-awakened";
    /// <summary>
    /// Ceremonial Arts as three casters, one Unison each (the owner's rule, Sept 29, 2026). Available from Age 0 because
    /// no single caster holds more than one note; it never opens <see cref="OrnamentalMagic"/> to one caster.
    /// </summary>
    public const string CeremonialTriad = "ceremonial-triad";

    /// <summary>The first Age number where each capability is available. Unknown capabilities are never available.</summary>
    public static readonly IReadOnlyDictionary<string, int> FirstAge = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [LegendOrnament] = 0,
        // Ages.md, Age III: "Starting point of proper Ornamental Magic"; Age 0 calls it largely unavailable. Ages I-II
        // say nothing either way, so it stays locked until III. D10 owns the per-card schedule (and any exceptions).
        [OrnamentalMagic] = 3,
        [UnisonMinor] = 0,
        [UnisonMajorAwakened] = 0,
        [CeremonialTriad] = 0,
    };

    public static bool IsKnown(string capability) => capability != null && FirstAge.ContainsKey(capability);

    public static bool IsAvailable(string capability, int ageNumber) =>
        capability != null && FirstAge.TryGetValue(capability, out int first) && ageNumber >= first;

    /// <summary>Available in the world's current Age (<see cref="GameAge"/>).</summary>
    public static bool IsAvailable(string capability) => IsAvailable(capability, GameAge.Number);
}
