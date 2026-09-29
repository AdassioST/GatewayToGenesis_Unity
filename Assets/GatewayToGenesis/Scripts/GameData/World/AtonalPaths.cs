using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>How one of the Eight-Born Paths hunts on the map (<see cref="AtonalPaths.Of"/>).</summary>
public sealed class PathTraits
{
    public AtonalPath path;
    /// <summary>The vault's cleverness tiers (Atonalis.md): Carnalix animal-like, Signath outside the scale, the reactive middle, the mirrors, Animach smartest.</summary>
    public BandIntelligence intelligence;
    /// <summary>Hexes it chases, and how long it keeps looking for a lost quarry (x its intelligence's memory).</summary>
    public int pursuit;
    public float memory = 1f;
    /// <summary>It takes your legends captive (every Path but Carnalix) and holds them to feed on.</summary>
    public bool captures;
    /// <summary>It drains the body (Carnalix): heavier wounds, it grows on what it takes, and it is never sated.</summary>
    public bool drains;
    /// <summary>It preys on creature bands too (flesh is flesh to a Carnalix).</summary>
    public bool eatsFlesh;
    /// <summary>It will not face a party of more than one (Erosyx: "Do not face them alone").</summary>
    public bool soloOnly;
    /// <summary>It moves in fits across contradictory truths (Signath): a share of its steps go astray.</summary>
    public float erratic;
    /// <summary>Paranoid about its ground (Anxithor): hostile to any of yours inside it, and lets you go outside it.</summary>
    public bool territorial;
    /// <summary>Share of your side's Composure it takes before the first blow (despair, fear, doubt).</summary>
    public float nerveBite;
    /// <summary>It remembers a victim that escaped and goes after it again whenever it sees it (Violux).</summary>
    public bool grudge;
    /// <summary>It roams a fixed loop (Obsessian).</summary>
    public bool loops;
    /// <summary>Strain a captive's Composure takes each Seventh while held (it is fed upon).</summary>
    public float feedOnCaptive;
    /// <summary>One line for the card: how it hunts, after the vault's Behavior.</summary>
    public string summary;
}

/// <summary>
/// The Eight-Born Paths on the map (vault: Eight-Born Paths.md, Atonalis.md), with no scene state (tested in
/// <c>WorldSufferingTests</c>). Each Path's behavior in the vault becomes how its bands hunt; a threat's band draws its
/// Path by the vault's shares unless the threat lists its own. Every number is a proposal.
/// </summary>
public static class AtonalPaths
{
    private static readonly Dictionary<AtonalPath, PathTraits> Table = new Dictionary<AtonalPath, PathTraits>
    {
        [AtonalPath.Anxithor] = new PathTraits { path = AtonalPath.Anxithor, intelligence = BandIntelligence.Regular, pursuit = 8, captures = true, territorial = true, nerveBite = 0.15f, feedOnCaptive = 3f,
            summary = "Paranoid and preemptively hostile inside its ground; drags captives home and guards them, feeding on their terror." },
        [AtonalPath.Discant] = new PathTraits { path = AtonalPath.Discant, intelligence = BandIntelligence.Regular, pursuit = 10, captures = true, nerveBite = 0.3f, feedOnCaptive = 2.5f,
            summary = "Does not kill; corrupts. Its despair takes your nerve before the first blow, and it carries the broken away." },
        [AtonalPath.Obsessian] = new PathTraits { path = AtonalPath.Obsessian, intelligence = BandIntelligence.Regular, pursuit = 12, memory = 2f, captures = true, loops = true, nerveBite = 0.1f, feedOnCaptive = 2f,
            summary = "Walks the same loop over and over; loses you only to find your trail again. It takes captives into its loop." },
        [AtonalPath.Signath] = new PathTraits { path = AtonalPath.Signath, intelligence = BandIntelligence.Regular, pursuit = 10, captures = true, erratic = 0.3f, nerveBite = 0.2f, feedOnCaptive = 2f,
            summary = "Moves across contradictory truths: its way is never straight. It dissolves certainty and takes captives." },
        [AtonalPath.Carnalix] = new PathTraits { path = AtonalPath.Carnalix, intelligence = BandIntelligence.Instinctive, pursuit = 14, drains = true, eatsFlesh = true,
            summary = "Hunts like a starving animal, anything of flesh. It takes no captives: it drains you, grows on what it takes, and is never sated." },
        [AtonalPath.Animach] = new PathTraits { path = AtonalPath.Animach, intelligence = BandIntelligence.Smart, pursuit = 12, captures = true, nerveBite = 0.1f, feedOnCaptive = 2f,
            summary = "The cleverest: many selves agree on a plan. It waits for good ground and takes captives." },
        [AtonalPath.Violux] = new PathTraits { path = AtonalPath.Violux, intelligence = BandIntelligence.Smart, pursuit = 40, memory = 4f, captures = true, grudge = true, nerveBite = 0.1f, feedOnCaptive = 3f,
            summary = "Methodical and relentless: it may pursue the same victim for years, and never forgets one that escaped." },
        [AtonalPath.Erosyx] = new PathTraits { path = AtonalPath.Erosyx, intelligence = BandIntelligence.Smart, pursuit = 12, captures = true, soloOnly = true, nerveBite = 0.2f, feedOnCaptive = 2f,
            summary = "Comes only for those who travel alone: do not face them alone. It takes its victim away." },
    };

    private static readonly PathTraits NoPath = new PathTraits { path = AtonalPath.None, intelligence = BandIntelligence.Regular, pursuit = 14, captures = true, summary = string.Empty };

    /// <summary>The Path's traits (a band with no Path hunts like a Nascent Atonalis of none in particular).</summary>
    public static PathTraits Of(AtonalPath path) => Table.TryGetValue(path, out var t) ? t : NoPath;

    /// <summary>The vault's shares of the Paths (Eight-Born Paths.md), in the enum's order.</summary>
    public static readonly (AtonalPath path, float share)[] Shares =
    {
        (AtonalPath.Anxithor, 16f), (AtonalPath.Discant, 15.5f), (AtonalPath.Obsessian, 12f), (AtonalPath.Signath, 11.5f),
        (AtonalPath.Carnalix, 13.5f), (AtonalPath.Animach, 13f), (AtonalPath.Violux, 13.3f), (AtonalPath.Erosyx, 5.2f),
    };

    /// <summary>The Path a threat's new band carries: one of its listed Paths (evenly), else drawn by the vault's shares; <paramref name="roll"/> 0-1.</summary>
    public static AtonalPath Draw(IList<AtonalPath> allowed, double roll)
    {
        roll = Math.Max(0d, Math.Min(0.999999d, roll));
        var pool = (allowed ?? new List<AtonalPath>()).Where(p => p != AtonalPath.None).Distinct().ToList();
        if (pool.Count > 0) return pool[(int)(roll * pool.Count)];
        float total = Shares.Sum(s => s.share), at = (float)roll * total;
        foreach (var (path, share) in Shares)
        {
            if (at < share) return path;
            at -= share;
        }
        return Shares[Shares.Length - 1].path;
    }
}
