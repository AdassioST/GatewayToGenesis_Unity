using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A resonance plague (vault: Pure Light.md, "Pathogenic Weaknesses of Pure Light"): an illness tuned so narrowly to a few
/// Pure Light lineages' Fundamental Frequency that their near-immunity to ordinary sickness means nothing against it.
/// Dragon's Bane and Slime Blight are canon; the rest are proposals (Canon Gaps). Every number is a proposal.
/// </summary>
[Serializable]
public class ResonancePlagueSpec
{
    public string id;
    public string name;
    [UnityEngine.Tooltip("What it is and how it kills, told once the people understand a host (SpeciesLevel.Understood).")]
    [UnityEngine.TextArea(1, 4)] public string description;
    [UnityEngine.Tooltip("What people call it before they understand it (diseases begin as folklore: vault, Arcanorian Ecology.md).")]
    public string folklore;
    [UnityEngine.Tooltip("The species it is tuned to (ids). Canon: Pure Light beings only; high-Structure lineages shrug it off.")]
    public List<string> hosts = new List<string>();
    [UnityEngine.Tooltip("A cell of the hosts' range is torn at this much Dissonance (with Static Criticality): Dissonance Blooms open in their Light channels there.")]
    [UnityEngine.Range(0f, 1f)] public float dissonanceAt = 0.15f;
    [UnityEngine.Tooltip("Chance each Echo that it breaks out in a Macro Biome whose hosts' range is all torn; times the torn share of the range, and the Echo's plague rhythm.")]
    [UnityEngine.Range(0f, 1f)] public float chance = 0.5f;
    [UnityEngine.Tooltip("Share of its hosts in the Macro Biome that die each Echo it runs: once the threshold is crossed the collapse is sudden (canon).")]
    [UnityEngine.Range(0f, 1f)] public float lethality = 0.5f;
    [UnityEngine.Tooltip("Echoes it runs in one Macro Biome.")]
    [UnityEngine.Min(1)] public int duration = 2;
    [UnityEngine.Tooltip("Chance each Echo it runs that it reaches each neighbouring Macro Biome where its hosts live (times the Echo's plague rhythm).")]
    [UnityEngine.Range(0f, 1f)] public float spread = 0.3f;
    [UnityEngine.Tooltip("Echoes after it passes during which it cannot return there: the survivors carry its scar spectra.")]
    [UnityEngine.Min(0)] public int immunity = 4;
}

/// <summary>A resonance plague in one Macro Biome (saved): running while <see cref="echoesLeft"/> is above 0, then remembered until <see cref="immuneUntil"/>.</summary>
[Serializable]
public class Outbreak
{
    public string plague;
    /// <summary>The Macro Biome's slot (<see cref="WorldTile.habitatSlot"/>).</summary>
    public int slot;
    public int echoesLeft;
    /// <summary>The Echo count (the world's grown Echoes) it began at, and until which it cannot return once passed.</summary>
    public int began, immuneUntil;

    public bool Running => echoesLeft > 0;
}

public enum PlagueChangeKind { BrokeOut, Spread, Passed }

/// <summary>What a plague did at an Echo, for the notices.</summary>
public class PlagueChange
{
    public PlagueChangeKind kind;
    public Outbreak outbreak;
    public ResonancePlagueSpec plague;
    /// <summary>The host populations it struck (or, when it passed, the survivors).</summary>
    public List<Population> hosts = new List<Population>();
}

/// <summary>
/// Resonance plagues among the Macro Biomes' creatures (vault: Arcanorian Ecology.md, "Resonance Plagues"), with no scene state (tested in
/// <c>CreatureSchemaTests</c>). Once an Echo, before the populations grow (<see cref="WorldEcology.Tick"/>):
///
/// - A running outbreak kills <see cref="ResonancePlagueSpec.lethality"/> of its hosts in its Macro Biome, may reach each
///   neighbouring Macro Biome where hosts live, and passes after <see cref="ResonancePlagueSpec.duration"/> Echoes; it
///   cannot return there for <see cref="ResonancePlagueSpec.immunity"/> Echoes.
/// - Where hosts live on torn ground it may break out, the likelier the more of their range is torn (cells whose
///   Dissonance and Static Criticality reach <see cref="ResonancePlagueSpec.dissonanceAt"/>): around Dissonance Seeds,
///   Vibrational Fallout and the Chaotic Resonant Cascade, and wherever the Loom is broken later.
/// Rolls are deterministic (the world's seed, the plague, the Macro Biome and the Echo). The Echo's rhythm
/// (<see cref="EchoRhythm.plagues"/>) weighs every roll. Knowledge: a plague is known by its folklore until one of its
/// hosts is Understood (<see cref="Name"/>).
/// </summary>
public static class WorldPlagues
{
    public static List<PlagueChange> Tick(WorldMap map, WorldGenSettings settings, int age, int echo, int season = 0)
    {
        var changes = new List<PlagueChange>();
        if (map == null || settings?.plagues == null || settings.plagues.Count == 0) return changes;
        map.Outbreaks = map.Outbreaks ?? new List<Outbreak>();
        float pace = Math.Max(0f, WorldRhythm.Of(settings, season)?.plagues ?? 1f);
        map.Outbreaks.RemoveAll(o => o == null || (!o.Running && echo >= o.immuneUntil) || settings.Plague(o.plague) == null);

        // Running outbreaks: they kill, spread and pass.
        foreach (var o in map.Outbreaks.Where(o => o.Running).ToList())
        {
            var plague = settings.Plague(o.plague);
            var struck = Hosts(map, plague, o.slot).ToList();
            foreach (var p in struck) p.groups = Math.Max(0f, p.groups * (1f - Clamp01(plague.lethality)));
            foreach (int n in Neighbours(map, o.slot))
            {
                if (!CanStrike(map, plague, n, echo) || Roll(map, plague, n, echo, 1) >= plague.spread * pace) continue;
                var spread = Begin(map, plague, n, echo);
                changes.Add(new PlagueChange { kind = PlagueChangeKind.Spread, outbreak = spread, plague = plague, hosts = Hosts(map, plague, n).ToList() });
            }
            if (--o.echoesLeft > 0) continue;
            o.immuneUntil = echo + Math.Max(0, plague.immunity);
            changes.Add(new PlagueChange { kind = PlagueChangeKind.Passed, outbreak = o, plague = plague, hosts = struck.Where(p => p.groups > 0f).ToList() });
        }

        // New outbreaks where the hosts live on torn ground.
        foreach (var plague in settings.plagues.Where(p => p != null && p.hosts != null && p.hosts.Count > 0))
        {
            foreach (int slot in map.Populations.Where(p => p.groups > 0f && IsHost(plague, p.species)).Select(p => p.slot).Distinct().OrderBy(s => s).ToList())
            {
                float torn = CanStrike(map, plague, slot, echo) ? Torn(map, settings, plague, slot, age) : 0f;
                if (torn <= 0f || Roll(map, plague, slot, echo, 0) >= plague.chance * torn * pace) continue;
                var o = Begin(map, plague, slot, echo);
                changes.Add(new PlagueChange { kind = PlagueChangeKind.BrokeOut, outbreak = o, plague = plague, hosts = Hosts(map, plague, slot).ToList() });
            }
        }
        return changes;
    }

    /// <summary>How much of the hosts' range in a Macro Biome is torn: the share of its cells whose Dissonance plus Static Criticality reach <see cref="ResonancePlagueSpec.dissonanceAt"/> (0-1).</summary>
    public static float Torn(WorldMap map, WorldGenSettings settings, ResonancePlagueSpec plague, int slot, int age)
    {
        float total = 0f;
        int count = 0;
        foreach (string host in plague.hosts)
        {
            if (!WorldEcology.Habitat(map, settings, host, age).TryGetValue(slot, out var cells)) continue;
            foreach (int i in cells)
            {
                if (map[i].dissonance + map[i].cascade >= plague.dissonanceAt) total++;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    /// <summary>The plague is tuned to <paramref name="species"/>.</summary>
    public static bool IsHost(ResonancePlagueSpec plague, string species) =>
        plague?.hosts != null && plague.hosts.Any(h => string.Equals(h, species, StringComparison.OrdinalIgnoreCase));

    /// <summary>The plagues tuned to a species.</summary>
    public static IEnumerable<ResonancePlagueSpec> PlaguesOf(WorldGenSettings settings, string species) =>
        (settings?.plagues ?? new List<ResonancePlagueSpec>()).Where(p => IsHost(p, species));

    /// <summary>The outbreaks running where a den's species lives (a plague tuned to it, in its Macro Biome).</summary>
    public static List<(Outbreak outbreak, ResonancePlagueSpec plague)> At(WorldMap map, WorldGenSettings settings, ResourceSite site)
    {
        var species = WorldEcology.SpeciesAt(settings, site);
        var result = new List<(Outbreak, ResonancePlagueSpec)>();
        if (species == null || map?.Outbreaks == null) return result;
        int slot = map[site.center].habitatSlot;
        foreach (var o in map.Outbreaks.Where(o => o.Running && o.slot == slot))
            if (settings.Plague(o.plague) is ResonancePlagueSpec plague && IsHost(plague, species.id)) result.Add((o, plague));
        return result;
    }

    /// <summary>Its name once its cause is understood, its folklore before ("the falling sickness").</summary>
    public static string Name(ResonancePlagueSpec plague, bool understood) =>
        plague == null ? null : understood || string.IsNullOrEmpty(plague.folklore) ? plague.name : plague.folklore;

    // A plague can strike a Macro Biome where its hosts live, it is not running, and the survivors are not immune.
    private static bool CanStrike(WorldMap map, ResonancePlagueSpec plague, int slot, int echo) =>
        Hosts(map, plague, slot).Any() &&
        !map.Outbreaks.Any(o => o.slot == slot && string.Equals(o.plague, plague.id, StringComparison.OrdinalIgnoreCase) && (o.Running || echo < o.immuneUntil));

    private static IEnumerable<Population> Hosts(WorldMap map, ResonancePlagueSpec plague, int slot) =>
        map.Populations.Where(p => p.slot == slot && p.groups > 0f && IsHost(plague, p.species));

    private static Outbreak Begin(WorldMap map, ResonancePlagueSpec plague, int slot, int echo)
    {
        map.Outbreaks.RemoveAll(o => o.slot == slot && string.Equals(o.plague, plague.id, StringComparison.OrdinalIgnoreCase));
        var o = new Outbreak { plague = plague.id, slot = slot, echoesLeft = Math.Max(1, plague.duration), began = echo };
        map.Outbreaks.Add(o);
        return o;
    }

    // Deterministic: the world's seed, the plague, the Macro Biome and the Echo (stream 0 breaking out, 1 spreading).
    private static float Roll(WorldMap map, ResonancePlagueSpec plague, int slot, int echo, int stream)
    {
        int key = 17;
        foreach (char c in plague.id ?? string.Empty) key = key * 31 + c;
        return (float)WorldNoise.Hash01(map.seed * 7919 + key * 13 + stream, slot, echo);
    }

    private static IEnumerable<int> Neighbours(WorldMap map, int slot) =>
        map.Stencil != null && slot >= 0 && slot < map.Stencil.Slots.Count ? map.Stencil.Slots[slot].neighbours : Enumerable.Empty<int>();

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
