using System;
using System.Collections.Generic;

/// <summary>What happened to a legend's soul (<see cref="LegendSoulLife"/>), for <see cref="LegendProgress"/> to act on.</summary>
public struct SoulEvent
{
    public enum Kind
    {
        /// <summary>Composure moved to another state (<see cref="from"/> to <see cref="to"/>).</summary>
        ComposureChanged,
        /// <summary>The Awakened State ended: the Soul Leitmotif collapsed back into cracks.</summary>
        Collapsed,
        /// <summary>A Motif Awakening, or the Catalytic Abyss of Emotion (<see cref="awakening"/>).</summary>
        Awakened,
        /// <summary>The Soul Leitmotif reached Surrender: the legend is lost to Dissonance.</summary>
        Lost,
    }

    public Kind kind;
    public ComposureState from, to;
    public AwakeningResult awakening;

    public bool Deeper => kind == Kind.ComposureChanged && to > from;
}

/// <summary>
/// One legend's life from Seventh to Seventh, with no scene state (tested in <c>LegendSoulTests</c>): Composure moves
/// (<see cref="ComposureRules.Next"/>), the Awakened State's surge runs out and collapses, a wound grows while the
/// Soul Leitmotif is cracked, and a healed wound is judged once: a Motif Awakening, the Catalytic Abyss of Emotion
/// or nothing. <see cref="LegendProgress"/> applies what it reports (notices, achievements, Lyrical Fragments, the council).
/// </summary>
public static class LegendSoulLife
{
    /// <summary>One Seventh for one legend.</summary>
    public static List<SoulEvent> Seventh(LegendSoul soul, in ComposureContext context, string legendName, LegendTraitCatalog catalog, int worldSeed, ComposureTuning t)
    {
        var events = new List<SoulEvent>();
        var before = ComposureRules.StateOf(soul.strain, t);
        soul.strain = ComposureRules.Next(soul.strain, context, t);
        if (soul.surgeSevenths > 0 && --soul.surgeSevenths == 0)
        {
            soul.strain = Math.Max(soul.strain, t.abyssAftermath);
            events.Add(new SoulEvent { kind = SoulEvent.Kind.Collapsed });
        }
        if (ComposureRules.StateOf(soul.strain, t) >= ComposureState.Fractured) soul.woundSevenths++;
        Settle(soul, before, legendName, catalog, worldSeed, t, events);
        return events;
    }

    /// <summary>Strain moved between Sevenths (a moment of joy lowers it): what that leads to.</summary>
    public static List<SoulEvent> Shift(LegendSoul soul, float change, string legendName, LegendTraitCatalog catalog, int worldSeed, ComposureTuning t)
    {
        var events = new List<SoulEvent>();
        var before = ComposureRules.StateOf(soul.strain, t);
        soul.strain = Math.Max(0f, soul.strain + change);
        Settle(soul, before, legendName, catalog, worldSeed, t, events);
        return events;
    }

    private static void Settle(LegendSoul soul, ComposureState before, string legendName, LegendTraitCatalog catalog, int worldSeed, ComposureTuning t, List<SoulEvent> events)
    {
        var now = ComposureRules.StateOf(soul.strain, t);
        if (now > soul.deepest) soul.deepest = now;
        if (now == before) return;
        events.Add(new SoulEvent { kind = SoulEvent.Kind.ComposureChanged, from = before, to = now });
        if (now > before)
        {
            if (now == ComposureState.Surrender) events.Add(new SoulEvent { kind = SoulEvent.Kind.Lost, from = before, to = now });
            return;
        }
        if (!ComposureRules.WoundHealed(now, soul.deepest)) return;
        // A wound has healed: it may awaken the legend; either way it is judged once, then forgotten.
        if (ComposureRules.HealingAwakens(now, soul.deepest, soul.woundSevenths, soul.ornaments.Count, t))
        {
            var result = LegendSoulRules.Awaken(soul, legendName, catalog, worldSeed);
            if (result.abyss) soul.surgeSevenths = Math.Max(1, t.abyssSevenths);
            events.Add(new SoulEvent { kind = SoulEvent.Kind.Awakened, from = before, to = now, awakening = result });
        }
        soul.deepest = now;
        soul.woundSevenths = 0;
    }
}
