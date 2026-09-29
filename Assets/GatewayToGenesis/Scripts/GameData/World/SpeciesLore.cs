using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a civilization remembers of one species (saved), whatever still stands on the map.</summary>
[Serializable]
public class SpeciesRecord
{
    public string species;
    /// <summary>One of its dens has been identified (it stays known when the den fades or is lost).</summary>
    public bool identified;
    /// <summary>Echoes an identified den of it lived within sight of land your people hold.</summary>
    public int watched;
    /// <summary>It has been watched and hunted long enough that its ways are known (<see cref="SpeciesLevel.Observed"/>).</summary>
    public bool observed;
    /// <summary>Where its identified dens were found ("Violet Grove, North-East Sector"), each place once, in the order found.</summary>
    public List<string> places = new List<string>();
    /// <summary>The highest level of knowledge whose discovery rewards were paid (E10; 0 none).</summary>
    [SaveOptionalField] public int rewarded;
    /// <summary>An Auric Enclave's scholars studied it (<see cref="WorldEnclaveEcology.Study"/>): understood without the research.</summary>
    [SaveOptionalField] public bool studied;
    /// <summary>The people's guesses at what the Bestiary asks of it (<see cref="SpeciesHypotheses"/>).</summary>
    [SaveOptionalField] public List<Hypothesis> hypotheses = new List<Hypothesis>();
    /// <summary>Echoes (bit e-1) one of its identified dens was watched in: "no season of its own" needs all four.</summary>
    [SaveOptionalField] public int echoMask;
    /// <summary>Hunts of it by your people already weighed as evidence.</summary>
    [SaveOptionalField] public int huntsWeighed;
    /// <summary>Every question the Bestiary asks of it was worked out by watching and hunting: understood without the research.</summary>
    [SaveOptionalField] public bool deduced;
}

/// <summary>What coming to know a species at one level gives, once (<see cref="SpeciesLore.Rewards"/>).</summary>
public class DiscoveryPayment
{
    public SpeciesSpec species;
    public SpeciesLevel level;
    public int eraScore;
    public List<ResourceAmount> resources = new List<ResourceAmount>();
}

/// <summary>The bestiary's memory (saved whole by <see cref="SpeciesLoreKeeper"/>).</summary>
[Serializable]
public class SpeciesLoreState
{
    public List<SpeciesRecord> species = new List<SpeciesRecord>();

    /// <summary>The species' record, or null when it was never identified.</summary>
    public SpeciesRecord Of(string id) =>
        string.IsNullOrEmpty(id) ? null : species.Find(r => r != null && string.Equals(r.species, id, StringComparison.OrdinalIgnoreCase));

    public SpeciesRecord Ensure(string id)
    {
        var record = Of(id);
        if (record == null) species.Add(record = new SpeciesRecord { species = id });
        return record;
    }
}

/// <summary>How knowledge of a species deepens (vault: Arcanorian Ecology.md, "Knowledge of Creatures"). Every number is a proposal (Canon Gaps).</summary>
[Serializable]
public class SpeciesLoreTuning
{
    [UnityEngine.Tooltip("Observations that make an identified species Observed: its ways toward your people and how its numbers go become known.")]
    [UnityEngine.Min(1)] public int observeAt = 3;
    [UnityEngine.Tooltip("An identified den counts as watched for an Echo when one of its hexes lies this close to land your people hold.")]
    [UnityEngine.Min(0)] public int watchReach = 2;
    [UnityEngine.Tooltip("Observations each hunt of the species by your people is worth.")]
    [UnityEngine.Min(0)] public int perHunt = 1;
    [UnityEngine.Tooltip("Observations each further place its dens were identified is worth (the first place is identification itself).")]
    [UnityEngine.Min(0)] public int perPlace = 1;

    [UnityEngine.Header("Discovery rewards (E10): Era Score for every species, once per level, before its own SpeciesSpec.discovery")]
    [UnityEngine.Min(0)] public int identifiedEra = 1;
    [UnityEngine.Min(0)] public int observedEra;
    [UnityEngine.Min(0)] public int understoodEra = 1;
    [UnityEngine.Tooltip("Mastered (5): domesticated or partnered through an Enclave (E7).")]
    [UnityEngine.Min(0)] public int masteredEra = 1;

    [UnityEngine.Header("Hypotheses (SpeciesHypotheses)")]
    [UnityEngine.Tooltip("Era Score for a guess confirmed with nothing ruled out before it (insight), once per question.")]
    [UnityEngine.Min(0)] public int insightEra = 1;

    /// <summary>The Era Score every species pays at a level (2 Identified ... 5 Mastered).</summary>
    public int EraFor(int level) => level == 2 ? identifiedEra : level == 3 ? observedEra : level == 4 ? understoodEra : level == 5 ? masteredEra : 0;
}

/// <summary>
/// The knowledge a surface may read (<see cref="SpeciesKnowledge"/>): the saved memory, its tuning, and whether a
/// researched technology lets the people understand what they have observed.
/// </summary>
public sealed class LoreView
{
    public SpeciesLoreState state;
    public SpeciesLoreTuning tuning;
    /// <summary>The <see cref="SpeciesLore.StudiesUnlock"/> technology is researched, or an Auric Enclave you hold as suzerain lends its scholars (E7).</summary>
    public bool understands;

    public SpeciesRecord Of(string id) => state?.Of(id);
}

/// <summary>
/// The second half of E5's knowledge ladder, with no scene state (tested in <c>SpeciesLoreTests</c>). Identified species
/// are remembered (<see cref="SpeciesRecord"/>) so a faded predatory bloom or a lost den does not take its knowledge with
/// it. An identified species becomes <see cref="SpeciesLevel.Observed"/> after enough observations: Echoes one of its
/// identified dens lived within sight of your land, hunts of it (E3's ledger, <see cref="BehaviorBond.hunts"/>), and
/// further places its dens were identified. An Observed species is <see cref="SpeciesLevel.Understood"/> while a
/// researched technology carries the <see cref="StudiesUnlock"/> Special unlockable.
///
/// What each level reveals (the Bestiary): Identified, its name and nature (the E1 card); Observed, how it treats your
/// people (its behavior toward you, E3) and whether its numbers grow or fall; Understood, its numbers, what newcomers meet
/// (its overall behavior), and what it hunts and what hunts it (identified species only).
/// </summary>
public static class SpeciesLore
{
    /// <summary>The <see cref="TechUnlockableType.Special"/> unlockable that turns what was observed into understanding.</summary>
    public const string StudiesUnlock = "Creature Studies";

    /// <summary>
    /// Remember every species with an identified den, and where it was found; then mark as Observed every species with
    /// enough observations. Returns the species that became Observed now (for the notices).
    /// </summary>
    public static List<string> Refresh(SpeciesLoreState state, WorldMap map, WorldGenSettings settings, SpeciesLoreTuning tuning)
    {
        var observed = new List<string>();
        if (state == null || map == null || settings == null) return observed;
        foreach (var (site, spec) in SpeciesKnowledge.Dens(map, settings))
        {
            if (!WorldResources.Identified(map, site)) continue;
            var record = state.Ensure(spec.species);
            record.identified = true;
            string place = SpeciesKnowledge.Place(map, settings, site);
            if (!record.places.Contains(place)) record.places.Add(place);
        }
        foreach (var record in state.species)
        {
            if (record == null || !record.identified || record.observed) continue;
            if (Observations(record, map, tuning) < Math.Max(1, tuning?.observeAt ?? 3)) continue;
            record.observed = true;
            observed.Add(record.species);
        }
        return observed;
    }

    /// <summary>
    /// An Echo: every identified species with an identified den within <see cref="SpeciesLoreTuning.watchReach"/> of land
    /// your people hold is watched once more. Then <see cref="Refresh"/>; returns the species that became Observed.
    /// </summary>
    public static List<string> Watch(SpeciesLoreState state, WorldMap map, WorldGenSettings settings, SpeciesLoreTuning tuning)
    {
        if (state == null || map == null || settings == null) return new List<string>();
        Refresh(state, map, settings, tuning);
        foreach (string id in WatchedNow(map, settings, tuning)) state.Ensure(id).watched++;
        return Refresh(state, map, settings, tuning);
    }

    /// <summary>The identified species with an identified den within <see cref="SpeciesLoreTuning.watchReach"/> of land your people hold: watched this Echo.</summary>
    public static HashSet<string> WatchedNow(WorldMap map, WorldGenSettings settings, SpeciesLoreTuning tuning)
    {
        var watched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (map == null || settings == null) return watched;
        int reach = Math.Max(0, tuning?.watchReach ?? 2);
        var held = new List<HexCoord>();
        for (int i = 0; i < map.Count; i++) if (WorldAuthority.IsPlayers(map[i].authorityId)) held.Add(map[i].coord);
        if (held.Count == 0) return watched;
        foreach (var (site, spec) in SpeciesKnowledge.Dens(map, settings))
        {
            if (watched.Contains(spec.species) || !WorldResources.Identified(map, site)) continue;
            if (site.cells.Any(c => c >= 0 && c < map.Count && held.Any(h => HexCoord.Distance(h, map[c].coord) <= reach))) watched.Add(spec.species);
        }
        return watched;
    }

    /// <summary>Observations of a species: Echoes watched, your people's hunts of it, and each further place its dens were identified.</summary>
    public static int Observations(SpeciesRecord record, WorldMap map, SpeciesLoreTuning tuning)
    {
        if (record == null) return 0;
        var t = tuning ?? new SpeciesLoreTuning();
        return record.watched + HuntsByYou(map, record.species) * t.perHunt + Math.Max(0, record.places.Count - 1) * t.perPlace;
    }

    /// <summary>Hunts of the species by your people (E3's ledger).</summary>
    public static int HuntsByYou(WorldMap map, string species) =>
        WorldBehavior.Of(map, species)?.Bond(WorldBehavior.Key(WorldAuthority.Player))?.hunts ?? 0;

    /// <summary>
    /// What the memory alone says of a species: Unknown until identified, then Identified, Observed or Understood (studied
    /// by an Auric Enclave: understood at once). Mastered is read from the map (<see cref="SpeciesKnowledge.LevelOf"/>).
    /// </summary>
    public static SpeciesLevel Level(SpeciesRecord record, bool understands)
    {
        if (record == null || !record.identified) return SpeciesLevel.Unknown;
        if (record.studied || record.deduced) return SpeciesLevel.Understood;
        if (!record.observed) return SpeciesLevel.Identified;
        return understands ? SpeciesLevel.Understood : SpeciesLevel.Observed;
    }

    // ===== DISCOVERY REWARDS (E10) =====

    /// <summary>
    /// What coming to know each remembered species has earned and not yet been paid: for every level from Identified up to
    /// what is known now (<paramref name="levelOf"/>, e.g. <see cref="SpeciesKnowledge.LevelOf"/>), the tuning's Era Score
    /// and the species' own <see cref="SpeciesSpec.discovery"/>. Marks them paid (<see cref="SpeciesRecord.rewarded"/>),
    /// so each level pays once, even when knowledge later falls back (a lapsed Suzerainty) and rises again.
    /// </summary>
    public static List<DiscoveryPayment> Rewards(SpeciesLoreState state, WorldGenSettings settings, SpeciesLoreTuning tuning, Func<string, SpeciesLevel> levelOf)
    {
        var due = new List<DiscoveryPayment>();
        if (state == null || settings == null || levelOf == null) return due;
        var t = tuning ?? new SpeciesLoreTuning();
        foreach (var record in state.species)
        {
            if (record == null || !record.identified) continue;
            var spec = settings.Species(record.species);
            if (spec == null) continue;
            int known = (int)levelOf(record.species);
            for (int level = Math.Max(record.rewarded + 1, (int)SpeciesLevel.Identified); level <= known; level++)
            {
                var pay = new DiscoveryPayment { species = spec, level = (SpeciesLevel)level, eraScore = t.EraFor(level) };
                foreach (var r in (spec.discovery ?? new List<DiscoveryReward>()).Where(r => r != null && (int)r.level == level))
                {
                    pay.eraScore += Math.Max(0, r.eraScore);
                    if (!string.IsNullOrEmpty(r.resource) && r.amount > 0f) pay.resources.Add(new ResourceAmount { resource = r.resource, amount = r.amount });
                }
                if (pay.eraScore > 0 || pay.resources.Count > 0) due.Add(pay);
            }
            record.rewarded = Math.Max(record.rewarded, known);
        }
        return due;
    }

    // ===== THE LIVING NUMBERS (read by GameValues and the Bestiary) =====

    /// <summary>The species' groups over what its land holds, in percent, over every Macro Biome it lives in (0 where it lives nowhere).</summary>
    public static float PopulationPercent(WorldMap map, string species)
    {
        if (map?.Populations == null || string.IsNullOrEmpty(species)) return 0f;
        float groups = 0f, capacity = 0f;
        foreach (var p in map.Populations)
        {
            if (p == null || !string.Equals(p.species, species, StringComparison.OrdinalIgnoreCase)) continue;
            groups += Math.Max(0f, p.groups);
            capacity += Math.Max(0f, p.capacity);
        }
        return capacity > 0.0001f ? (float)Math.Round(groups / capacity * 100f) : 0f;
    }

    /// <summary>The species' temper toward your people, -100 (harmed) to +100 (befriended); 0 before you have met it.</summary>
    public static float BehaviorPercent(WorldMap map, string species) =>
        WorldBehavior.Met(map, species, WorldAuthority.Player) ? (float)Math.Round(WorldBehavior.Temper(map, species, WorldAuthority.Player) * 100f) : 0f;
}
