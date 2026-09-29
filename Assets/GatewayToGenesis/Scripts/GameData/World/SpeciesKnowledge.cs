using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// How much a civilization knows of one species (vault: Arcanorian Ecology.md, "Knowledge of Creatures"). Derived from the map every time
/// it is asked, never saved. The numbers are what <c>GameValues</c> reads ("species_known:grey-wolf" is 2 once identified).
/// </summary>
public enum SpeciesLevel
{
    /// <summary>Nothing of it is known (and nothing about it may show anywhere).</summary>
    Unknown = 0,
    /// <summary>A den of it stands where the map can see it, still unsurveyed: the map says only "unidentified fauna".</summary>
    Sighted = 1,
    /// <summary>A den of it has been surveyed: its name, subgroup and nature are known (and remembered: <see cref="SpeciesLore"/>).</summary>
    Identified = 2,
    /// <summary>Watched and hunted long enough (<see cref="SpeciesLore.Observations"/>): how it treats your people and whether its numbers grow or fall.</summary>
    Observed = 3,
    /// <summary>Observed, and a technology carries <see cref="SpeciesLore.StudiesUnlock"/>: its numbers, its overall behavior, what it hunts and what hunts it.</summary>
    Understood = 4,
    /// <summary>
    /// Identified, and kept by a Domestication Enclave you hold as suzerain (<see cref="WorldEnclaveEcology.KeptForYou"/>):
    /// its keepers share everything they know. Derived from the map, never saved: it lasts while they keep it.
    /// </summary>
    Mastered = 5,
    // Still to come: Rumored (an expedition story names it before anyone has found it) beside Sighted, once its reader exists.
}

/// <summary>A species the civilization has identified, and the places its identified dens were found.</summary>
public sealed class KnownSpecies
{
    public SpeciesSpec species;
    public SpeciesLevel level = SpeciesLevel.Identified;
    /// <summary>Its identified dens standing now (sighted but unsurveyed ones are not known to be its own).</summary>
    public List<ResourceSite> dens = new List<ResourceSite>();
    /// <summary>Places remembered where no identified den of it stands any longer (a faded bloom, a lost den).</summary>
    public List<string> formerPlaces = new List<string>();
}

/// <summary>
/// The bestiary's knowledge, with no scene state (tested in <c>SpeciesKnowledgeTests</c>). It only reads the map: a
/// species is <see cref="SpeciesLevel.Sighted"/> once a den of it (a resource site naming it,
/// <see cref="ResourceSiteSpec.species"/>) is sighted (<see cref="WorldResources.Sighted"/>), and
/// <see cref="SpeciesLevel.Identified"/> once such a den is identified (<see cref="WorldResources.Identified"/>).
///
/// Spoiler rule (the owner's: the UI only shows what the civilization knows): nothing here tells a player-facing surface
/// about a species it has not identified. Sighted dens are listed one by one as unidentified sightings, never grouped by
/// species (that would tell which ones are alike), and only when they are Fauna (an unidentified bloom listed in the
/// bestiary would say it is a creature). The Atonalis are threats, never species, and never appear here.
/// </summary>
public static class SpeciesKnowledge
{
    /// <summary>The <see cref="TechUnlockableType.Special"/> unlockable that opens the Bestiary.</summary>
    public const string BestiaryUnlock = "Bestiary";

    /// <summary>GameValues domain: identified species (no target), or one species' <see cref="SpeciesLevel"/> (target: its id).</summary>
    public const string SpeciesKnownDomain = "species_known";

    /// <summary>GameValues domain: identified resource sites, planted ones aside (target: a kind word such as "fauna", or a site id).</summary>
    public const string SitesIdentifiedDomain = "sites_identified";

    /// <summary>GameValues domain: one species' groups over what its land holds, 0-100 (target: its id; <see cref="SpeciesLore.PopulationPercent"/>).</summary>
    public const string SpeciesPopulationDomain = "species_population";

    /// <summary>GameValues domain: one species' temper toward your people, -100 to +100, 0 before you have met it (target: its id).</summary>
    public const string SpeciesBehaviorDomain = "species_behavior";

    /// <summary>
    /// A domain that reads the creatures: a technology's Enlightenment may reward it, nothing may require it (the Q5 draw
    /// can leave a species out of a world).
    /// </summary>
    public static bool IsDiscoveryDomain(string domain) =>
        string.Equals(domain, SpeciesKnownDomain, StringComparison.OrdinalIgnoreCase) || string.Equals(domain, SitesIdentifiedDomain, StringComparison.OrdinalIgnoreCase)
        || IsLivingDomain(domain);

    // The domains that read a living species (a target is required).
    private static bool IsLivingDomain(string domain) =>
        string.Equals(domain, SpeciesPopulationDomain, StringComparison.OrdinalIgnoreCase) || string.Equals(domain, SpeciesBehaviorDomain, StringComparison.OrdinalIgnoreCase);

    // ===== LEVELS =====

    /// <summary>
    /// What the civilization knows of <paramref name="speciesId"/>: the map's own reading (sighted, identified), raised by
    /// the saved memory in <paramref name="lore"/> (identified long ago, observed, understood) when there is one.
    /// </summary>
    public static SpeciesLevel LevelOf(WorldMap map, WorldGenSettings settings, string speciesId, LoreView lore = null)
    {
        if (map == null || settings == null || string.IsNullOrEmpty(speciesId)) return SpeciesLevel.Unknown;
        var level = SpeciesLevel.Unknown;
        foreach (var (site, spec) in Dens(map, settings))
        {
            if (!string.Equals(spec.species, speciesId, StringComparison.OrdinalIgnoreCase)) continue;
            if (WorldResources.Identified(map, site)) { level = SpeciesLevel.Identified; break; }
            if (IsSighted(map, site, spec)) level = SpeciesLevel.Sighted;
        }
        return Mastery(map, settings, speciesId, Max(level, Remembered(lore, speciesId)));
    }

    /// <summary>Every species known at all, with its level (unknown ones are left out).</summary>
    public static Dictionary<string, SpeciesLevel> Levels(WorldMap map, WorldGenSettings settings, LoreView lore = null)
    {
        var levels = new Dictionary<string, SpeciesLevel>(StringComparer.OrdinalIgnoreCase);
        if (map == null || settings == null) return levels;
        foreach (var (site, spec) in Dens(map, settings))
        {
            var level = WorldResources.Identified(map, site) ? SpeciesLevel.Identified : IsSighted(map, site, spec) ? SpeciesLevel.Sighted : SpeciesLevel.Unknown;
            if (level == SpeciesLevel.Unknown) continue;
            if (!levels.TryGetValue(spec.species, out var known) || known < level) levels[spec.species] = level;
        }
        foreach (var record in lore?.state?.species ?? new List<SpeciesRecord>())
        {
            if (record == null || string.IsNullOrEmpty(record.species)) continue;
            var level = Remembered(lore, record.species);
            if (level == SpeciesLevel.Unknown) continue;
            levels[record.species] = levels.TryGetValue(record.species, out var known) ? Max(known, level) : level;
        }
        foreach (string id in levels.Keys.ToList()) levels[id] = Mastery(map, settings, id, levels[id]);
        return levels;
    }

    /// <summary>How many species are identified.</summary>
    public static int IdentifiedCount(WorldMap map, WorldGenSettings settings, LoreView lore = null) =>
        Levels(map, settings, lore).Count(p => p.Value >= SpeciesLevel.Identified);

    // What the saved memory alone knows of a species.
    private static SpeciesLevel Remembered(LoreView lore, string speciesId) => SpeciesLore.Level(lore?.Of(speciesId), lore != null && lore.understands);

    private static SpeciesLevel Max(SpeciesLevel a, SpeciesLevel b) => a >= b ? a : b;

    // A species you know is Mastered while keepers you hold as suzerain keep it (WorldEnclaveEcology).
    private static SpeciesLevel Mastery(WorldMap map, WorldGenSettings settings, string speciesId, SpeciesLevel level) =>
        level >= SpeciesLevel.Identified && WorldEnclaveEcology.KeptForYou(map, settings, speciesId) ? SpeciesLevel.Mastered : level;

    /// <summary>
    /// The identified species, by name, each with its level, its identified dens standing now, and (with
    /// <paramref name="lore"/>) the species remembered though no identified den of it stands any longer. A species the
    /// catalog does not have is skipped (the validator reports it).
    /// </summary>
    public static List<KnownSpecies> Identified(WorldMap map, WorldGenSettings settings, LoreView lore = null)
    {
        var known = new Dictionary<string, KnownSpecies>(StringComparer.OrdinalIgnoreCase);
        if (map == null || settings == null) return new List<KnownSpecies>();
        foreach (var (site, spec) in Dens(map, settings))
        {
            if (!WorldResources.Identified(map, site)) continue;
            var species = settings.Species(spec.species);
            if (species == null) continue;
            if (!known.TryGetValue(species.id, out var entry)) known[species.id] = entry = new KnownSpecies { species = species };
            entry.dens.Add(site);
        }
        foreach (var record in lore?.state?.species ?? new List<SpeciesRecord>())
        {
            if (record == null || !record.identified) continue;
            var species = settings.Species(record.species);
            if (species == null) continue;
            if (!known.TryGetValue(species.id, out var entry)) known[species.id] = entry = new KnownSpecies { species = species };
            var standing = new HashSet<string>(entry.dens.Select(d => Place(map, settings, d)));
            entry.formerPlaces.AddRange(record.places.Where(p => !string.IsNullOrEmpty(p) && !standing.Contains(p)));
        }
        foreach (var entry in known.Values) entry.level = Mastery(map, settings, entry.species.id, Max(SpeciesLevel.Identified, Remembered(lore, entry.species.id)));
        return known.Values.OrderBy(k => k.species.name ?? k.species.id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Sighted, unsurveyed Fauna sites, one by one (not grouped by species, and blooms left to the map): what the
    /// bestiary may say about creatures no one has identified yet.
    /// </summary>
    public static List<ResourceSite> UnidentifiedSightings(WorldMap map, WorldGenSettings settings)
    {
        var sightings = new List<ResourceSite>();
        if (map == null || settings == null) return sightings;
        foreach (var site in map.ResourceSites)
        {
            if (site == null || site.kind != ResourceKind.Fauna || site.cells.Count == 0 || WorldResources.Identified(map, site)) continue;
            if (IsSighted(map, site, settings.ResourceSite(site.spec))) sightings.Add(site);
        }
        return sightings;
    }

    /// <summary>Identified resource sites (planting is not discovery): all, or those of one kind ("fauna") or one site id.</summary>
    public static int SitesIdentified(WorldMap map, WorldGenSettings settings, string target = null)
    {
        if (map == null) return 0;
        bool all = string.IsNullOrWhiteSpace(target);
        string wanted = all ? null : target.Trim();
        return map.ResourceSites.Count(site => site != null && !site.planted && site.cells.Count > 0 && WorldResources.Identified(map, site)
            && (all || string.Equals(site.spec, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(WorldResources.KindWord(site.kind), wanted, StringComparison.OrdinalIgnoreCase)));
    }

    // ===== GAME VALUES =====

    /// <summary>
    /// The value of a domain on the creatures (<see cref="IsDiscoveryDomain"/>): what GameValues answers. 0 for any other
    /// domain. The living domains read the world as it is (stories may test it; Enlightenment hides their progress).
    /// </summary>
    public static float Value(WorldMap map, WorldGenSettings settings, string domain, string target, LoreView lore = null)
    {
        if (string.Equals(domain, SitesIdentifiedDomain, StringComparison.OrdinalIgnoreCase)) return SitesIdentified(map, settings, target);
        string id = string.IsNullOrWhiteSpace(target) ? null : target.Trim();
        if (string.Equals(domain, SpeciesPopulationDomain, StringComparison.OrdinalIgnoreCase)) return id == null ? 0f : SpeciesLore.PopulationPercent(map, id);
        if (string.Equals(domain, SpeciesBehaviorDomain, StringComparison.OrdinalIgnoreCase)) return id == null || map == null ? 0f : SpeciesLore.BehaviorPercent(map, id);
        if (!string.Equals(domain, SpeciesKnownDomain, StringComparison.OrdinalIgnoreCase)) return 0f;
        return id == null ? IdentifiedCount(map, settings, lore) : (float)LevelOf(map, settings, id, lore);
    }

    /// <summary>
    /// Whether a goal's progress may show ("(1/2)"). A goal on one species may not: its halfway level (Sighted) would
    /// say which unidentified den belongs to that species, and its numbers or temper are known only once understood.
    /// </summary>
    public static bool HidesProgress(string domain, string target) =>
        (string.Equals(domain, SpeciesKnownDomain, StringComparison.OrdinalIgnoreCase) || IsLivingDomain(domain)) && !string.IsNullOrWhiteSpace(target);

    /// <summary>
    /// Why an Enlightenment goal on a discovery domain can never be met, or null when it is fine: a species named must
    /// be one of <paramref name="species"/>, and a species' level runs from 1 (sighted) to 5 (mastered); a living
    /// domain names a species and asks for 1 to 100; a site target must be a resource kind word or one of
    /// <paramref name="sites"/>.
    /// </summary>
    public static string GoalProblem(string domain, string target, float required, ICollection<string> species, ICollection<string> sites)
    {
        string name = (target ?? string.Empty).Trim();
        if (string.Equals(domain, SpeciesKnownDomain, StringComparison.OrdinalIgnoreCase))
        {
            if (name.Length == 0) return required < 1f ? "asks for fewer than one identified species" : null;
            if (species == null || !species.Contains(name)) return $"names unknown species '{name}' (an id from World.asset generation.species; the Atonalis are never species)";
            if (required < (float)SpeciesLevel.Sighted || required > (float)SpeciesLevel.Mastered) return $"asks '{name}' for level {required}, but a species' level runs from 1 (sighted) to 5 (mastered)";
            return null;
        }
        if (IsLivingDomain(domain))
        {
            if (name.Length == 0) return "names no species (the target is a species id)";
            if (species == null || !species.Contains(name)) return $"names unknown species '{name}' (an id from World.asset generation.species; the Atonalis are never species)";
            if (required < 1f || required > 100f) return $"asks '{name}' for {required}, but it runs up to 100 and a goal of 0 or less is met before anything happens";
            return null;
        }
        if (string.Equals(domain, SitesIdentifiedDomain, StringComparison.OrdinalIgnoreCase))
        {
            if (required < 1f) return "asks for fewer than one identified site";
            bool kind = Enum.GetValues(typeof(ResourceKind)).Cast<ResourceKind>().Any(k => string.Equals(WorldResources.KindWord(k), name, StringComparison.OrdinalIgnoreCase));
            if (name.Length > 0 && !kind && (sites == null || !sites.Contains(name))) return $"names '{name}', which is neither a resource kind (fauna, flora...) nor a resource site id";
        }
        return null;
    }

    // ===== PLACES =====

    /// <summary>Where a den stands, as the hover card says it: "Violet Grove, North-East Sector"; in an intersection, "Violet Grove, an intersection".</summary>
    public static string Place(WorldMap map, WorldGenSettings settings, ResourceSite site)
    {
        if (map == null || site == null || site.center < 0 || site.center >= map.Count) return "somewhere unknown";
        var tile = map[site.center];
        var biome = settings?.MacroBiome(tile.macroBiome);
        string biomeName = biome != null && !string.IsNullOrEmpty(biome.name) ? biome.name : null;
        if (tile.composition == WorldComposition.Intersection) return biomeName != null ? $"{biomeName}, an intersection" : "an intersection";
        if (biomeName == null) return "the wilds";
        return WorldSectors.Name(tile.sector) is string sector ? $"{biomeName}, {sector}" : biomeName;
    }

    /// <summary>The places of <paramref name="dens"/>, each once, in the order first found, with how many dens stand there.</summary>
    public static List<(string place, int dens)> Places(WorldMap map, WorldGenSettings settings, IEnumerable<ResourceSite> dens)
    {
        var places = new List<(string place, int dens)>();
        foreach (var site in dens ?? Enumerable.Empty<ResourceSite>())
        {
            string place = Place(map, settings, site);
            int i = places.FindIndex(p => p.place == place);
            if (i < 0) places.Add((place, 1));
            else places[i] = (place, places[i].dens + 1);
        }
        return places;
    }

    // ===== HELPERS =====

    /// <summary>Every standing resource site that names a species (dens and Trapper blooms), with its spec.</summary>
    public static IEnumerable<(ResourceSite site, ResourceSiteSpec spec)> Dens(WorldMap map, WorldGenSettings settings)
    {
        foreach (var site in map.ResourceSites)
        {
            if (site == null || site.cells.Count == 0) continue;
            var spec = settings.ResourceSite(site.spec);
            if (spec == null || string.IsNullOrEmpty(spec.species)) continue;
            yield return (site, spec);
        }
    }

    // A site is sighted when any of its cells shows it (WorldResources.Sighted, the map's own rule).
    private static bool IsSighted(WorldMap map, ResourceSite site, ResourceSiteSpec spec) =>
        site.cells.Any(c => c >= 0 && c < map.Count && WorldResources.Sighted(map[c], site, spec));
}
