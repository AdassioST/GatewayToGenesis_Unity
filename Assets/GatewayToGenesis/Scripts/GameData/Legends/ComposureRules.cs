using System;
using UnityEngine;

/// <summary>
/// The five stress levels of a Soul Leitmotif (Composure.md: "Composure consists of 5 stress levels on the Soul
/// Leitmotif that mirror that of the growth phases of an Atonalis"). Ordered from calm to lost, so a higher value is
/// deeper. Saved by name.
/// </summary>
public enum ComposureState { Pristine, Clouded, Fractured, Spiraling, Surrender }

/// <summary>
/// The numbers behind Composure. Every one of them is a proposal (roadmap D08: composure thresholds are the owner's
/// to approve); the vault gives the five states and what each means, not where they begin. Strain runs from 0 (the
/// calmest Pristine) to <see cref="surrenderAt"/>. Rates are per Seventh.
/// </summary>
[Serializable]
public class ComposureTuning
{
    [Header("Where each state begins (strain)")]
    [Tooltip("Below this a Soul Leitmotif is Pristine: moments of joy (vault: \"Momentarily, a Soul Leitmotif can become pristine on intense moments of joy\").")]
    public float cloudedAt = 10f;
    [Tooltip("Black cracks in the casing: the first real damage, and the state a Motif Awakening needs.")]
    public float fracturedAt = 40f;
    [Tooltip("The last point where anyone can intervene.")]
    public float spiralingAt = 70f;
    [Tooltip("The Soul Leitmotif breaks: the legend is lost to Dissonance.")]
    public float surrenderAt = 100f;

    [Header("Rest")]
    [Tooltip("Where strain settles with nothing pressing (vault: Clouded \"is the normal state for most Spellweavers\").")]
    public float baseline = 20f;
    [Tooltip("Strain eased per Seventh toward the baseline by a legend who is neither seated nor on an expedition (or whose expedition is camped in one of your settlements).")]
    public float restRecovery = 2f;
    [Tooltip("Strain eased per Seventh toward the baseline by a legend on an expedition (the road's hardship is added on top).")]
    [UnityEngine.Serialization.FormerlySerializedAs("leadingRecovery")]
    public float expeditionRecovery = 0.5f;
    [Tooltip("Strain eased per Seventh toward the baseline by a seated legend.")]
    public float seatedRecovery = 0.25f;

    [Header("What strains the council")]
    [Tooltip("Strain per Seventh on each seated legend once the Age Crisis has begun, before it is named.")]
    public float unannouncedCrisisLoad = 0.4f;
    [Tooltip("Strain per Seventh on each seated legend while the Age Crisis is named.")]
    public float declaredCrisisLoad = 0.9f;
    [Tooltip("Strain on each seated legend for the share of the people who died (0.1 = a tenth of everyone -> +5 at 50).")]
    public float griefScale = 50f;
    [Tooltip("The most grief can add in one Seventh.")]
    public float griefCapPerSeventh = 15f;

    [Header("Joy")]
    [Tooltip("Strain eased at once when a legend rises in rank.")]
    public float rankJoy = 12f;

    [Header("Motif Awakening")]
    [Tooltip("Sevenths a legend must spend Fractured or deeper before healing back to Clouded awakens a new Ornament.")]
    public int woundSevenths = 7;
    [Tooltip("The Catalytic Abyss of Emotion: the legend's own council bonuses are multiplied by this while the Awakened State lasts...")]
    public float abyssSurge = 2f;
    [Tooltip("...for this many Sevenths...")]
    public int abyssSevenths = 7;
    [Tooltip("...after which the Soul Leitmotif collapses to this strain.")]
    public float abyssAftermath = 55f;

    [Header("The council")]
    [Tooltip("What a legend's own council bonuses are multiplied by while Fractured.")]
    public float fracturedCouncil = 0.9f;
    [Tooltip("What a legend's own council bonuses are multiplied by while Spiraling (vault: the gem \"flickers or dims\").")]
    public float spiralingCouncil = 0.7f;
}

/// <summary>What bears on one legend's Composure during a Seventh.</summary>
public struct ComposureContext
{
    /// <summary>On the council, Head of State included.</summary>
    public bool seated;
    /// <summary>Walking with an expedition on the world map.</summary>
    public bool onExpedition;
    /// <summary>Its expedition is camped in one of your settlements: it rests as at home.</summary>
    public bool restingAtSettlement;
    /// <summary>What the road puts on it this Seventh (<see cref="Expeditions.Hardship"/>).</summary>
    public float hardship;
    public bool crisisBegun, crisisDeclared;
    /// <summary>The share of the people who died since the last Seventh (0-1).</summary>
    public float griefShare;
}

/// <summary>
/// Composure, the stress and sanity meter of every Spellweaver (vault: Composure.md), with no scene state; tested in
/// <c>LegendSoulTests</c>. Strain rises under the weight the council carries (an Age Crisis, the dead) and the road an
/// expedition walks (its hardship when things go bad), and eases toward the Clouded baseline, fastest in rest. Only the
/// council bears the crisis and its dead; only the road's walkers bear its hardship; a legend at home is shielded.
/// </summary>
public static class ComposureRules
{
    public static ComposureState StateOf(float strain, ComposureTuning t)
    {
        if (strain >= t.surrenderAt) return ComposureState.Surrender;
        if (strain >= t.spiralingAt) return ComposureState.Spiraling;
        if (strain >= t.fracturedAt) return ComposureState.Fractured;
        if (strain >= t.cloudedAt) return ComposureState.Clouded;
        return ComposureState.Pristine;
    }

    /// <summary>What weighs on a legend this Seventh (before recovery): the council's crisis and dead, or the road's hardship.</summary>
    public static float Load(in ComposureContext c, ComposureTuning t)
    {
        if (c.onExpedition) return Math.Max(0f, c.hardship);
        if (!c.seated) return 0f;
        float load = c.crisisDeclared ? t.declaredCrisisLoad : c.crisisBegun ? t.unannouncedCrisisLoad : 0f;
        return load + Math.Min(t.griefCapPerSeventh, Math.Max(0f, c.griefShare) * t.griefScale);
    }

    /// <summary>How fast a legend eases toward the baseline this Seventh.</summary>
    public static float Recovery(in ComposureContext c, ComposureTuning t) =>
        c.seated ? t.seatedRecovery : c.onExpedition && !c.restingAtSettlement ? t.expeditionRecovery : t.restRecovery;

    /// <summary>
    /// Strain after one Seventh: the load is added, then strain eases toward the baseline by the recovery rate
    /// without passing it (joy below the baseline fades back up the same way). Never below 0.
    /// </summary>
    public static float Next(float strain, in ComposureContext c, ComposureTuning t)
    {
        float next = strain + Load(c, t);
        float recovery = Recovery(c, t);
        if (next > t.baseline) next = Math.Max(t.baseline, next - recovery);
        else if (next < t.baseline) next = Math.Min(t.baseline, next + recovery);
        return Math.Max(0f, next);
    }

    /// <summary>What a legend's own council bonuses are multiplied by in <paramref name="state"/> (Surrender: nothing).</summary>
    public static float CouncilFactor(ComposureState state, ComposureTuning t)
    {
        switch (state)
        {
            case ComposureState.Fractured: return t.fracturedCouncil;
            case ComposureState.Spiraling: return t.spiralingCouncil;
            case ComposureState.Surrender: return 0f;
            default: return 1f;
        }
    }

    /// <summary>A wound has healed: the legend is back to Clouded or calmer after being Fractured or deeper. Each wound is judged once, then forgotten.</summary>
    public static bool WoundHealed(ComposureState now, ComposureState deepest) =>
        now <= ComposureState.Clouded && deepest >= ComposureState.Fractured && deepest < ComposureState.Surrender;

    /// <summary>
    /// Whether healing awakens: a legend who spent at least <see cref="ComposureTuning.woundSevenths"/> Fractured or
    /// deeper and is back to Clouded or calmer (vault, Motif Awakening.md: "your healing is your responsibility").
    /// Once both Ornaments are embellished, what is left is the Catalytic Abyss of Emotion, and only a legend brought
    /// back from Spiraling reaches it (the vault: it "requires an authentic catalyst... This is why it is rare").
    /// </summary>
    public static bool HealingAwakens(ComposureState now, ComposureState deepest, int woundSevenths, int ornaments, ComposureTuning t) =>
        WoundHealed(now, deepest) && woundSevenths >= t.woundSevenths &&
        (ornaments < LegendSoulRules.MaxOrnaments || deepest >= ComposureState.Spiraling);

    /// <summary>The share of the people lost between two counts of the dead (0 when nobody died or no one was left).</summary>
    public static float GriefShare(int deathsBefore, int deathsNow, int peopleNow)
    {
        int died = deathsNow - deathsBefore;
        if (died <= 0) return 0f;
        return died / (float)Math.Max(1, peopleNow + died);
    }
}
