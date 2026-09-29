using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Seeds planted on your land (saved with the world): a planted resource site on <see cref="cell"/>.</summary>
[Serializable]
public class Planting
{
    public int cell;
    public string spec;
    public int age;
}

public enum GrowthKind { Sprouted, Turned, Faded, Volunteered }

/// <summary>What the land did at an Echo (<see cref="WorldResources.GrowEcho"/>): a patch sprouted, turned, or faded away.</summary>
public class GrowthChange
{
    public ResourceSite site;
    public GrowthKind kind;
    /// <summary>What it was before it turned or faded (spec id and name).</summary>
    public string from, fromName;
    /// <summary>A volunteer bloom: the settlement that drew it up, and what in its life did ("grief", "ease", or its district's id).</summary>
    public Settlement settlement;
    public string reason;
}

/// <summary>A resource site on the map: generated with its Age, or planted from carried seeds (derived from <see cref="Planting"/>).</summary>
[Serializable]
public class ResourceSite
{
    public int index;
    public string spec;
    public string name;
    public ResourceKind kind;
    public int center;
    public List<int> cells = new List<int>();
    public int age;
    public bool planted;
    /// <summary>What it adds to the land's value once identified (derived at each refresh: a planted patch counts its share).</summary>
    public float landValue;
    /// <summary>An Eleos Bloom's vigor, 0-1: the Emotional Residue it finds over what it needs (derived at each refresh; 1 for every other kind).</summary>
    [SaveOptionalField] public float vigor = 1f;
    /// <summary>How far its turn into another site has run (0-1, Echo by Echo), and how far a sprouted patch has faded.</summary>
    [SaveOptionalField] public float turning, fading;
    /// <summary>A den (<see cref="WorldEcology"/>): the Phase it was last hunted, counted from the game's start (-1: never). Dens can be hunted once a Phase, not once an Age.</summary>
    [SaveOptionalField] public int huntedPhase = -1;
    /// <summary>A volunteer bloom (<see cref="WorldResources.Volunteer"/>): the id of the settlement whose life drew it up (-1: wild). It fades once that settlement no longer draws it.</summary>
    [SaveOptionalField] public int tendedBy = -1;
    /// <summary>An Eleos Bloom's lineage: the palate it has evolved to (<see cref="EmotionalEvolution"/>); null while it eats as its kind first did.</summary>
    [SaveOptionalField] public Lineage lineage;
    /// <summary>How potent an Eleos Bloom's gifts are (derived at each refresh; 0 or 1 ordinary, up to superloaded).</summary>
    [SaveOptionalField] public float potency = 1f;
}

/// <summary>
/// Resource sites (<see cref="ResourceSiteSpec"/>), with no scene state (tested in <c>WorldResourcesTests</c>): the lesser
/// patches that make a tile worth having. The same map, settings and Age give the same sites.
///
/// - Knowledge: a site is sighted once its cell is known (only its kind shows: "unidentified flora"; a god-ray or a red
///   spire shows from afar) and identified once its cell is surveyed. Planted sites are always identified.
/// - Land value: an identified site adds City Development to a settlement working it (<see cref="CityDevelopment"/>,
///   the Resources term) and raises how eagerly society adopts its cell (<see cref="WorldTerritory.Priority"/>); a
///   blight lowers both. Held and identified, it yields (<see cref="WorldUnits.LandYields"/>).
/// - Neighbours: each site makes its own cell and those around it fairer or uglier (<see cref="WorldBeauty"/>), lends or
///   takes Coherence (handed to the magic, <see cref="WorldMagic.SetShifts"/>) and may lend land fertility.
/// - Harvest: an expedition gathers a site's harvest once an Age as cargo, without claiming the ground; on ground another
///   authority holds that raises its grievances (<see cref="Owner"/>). Some harvests give seeds, planted on fertile
///   ground you hold as a smaller patch (<see cref="WhyNotPlant"/>) that can change the soil itself.
/// - Eleos Blooms (kind Bloom; vault: Eleos Bloom.md) metabolize feeling: each cell carries Emotional Residue
///   (<see cref="Residue"/>: people nearby, the ruins of the fallen, unmetabolized Dissonance) and a bloom's vigor is
///   the residue it finds over what it needs. Everything a bloom does scales with its vigor, and below
///   <see cref="EleosSettings.witherBelow"/> it withers (it can neither move nor shine). Listeners drink Dissonance
///   around them (handed to the magic, <see cref="WorldMagic.SetDissonanceShifts"/>); healers ease the Composure of an
///   expedition beside them (<see cref="WorldTile.sanctuary"/>); predators cast danger (<see cref="WorldSites.RecomputeDanger"/>)
///   and lure parties in (<see cref="MishapKind.Lure"/>). Lumen Seeds only take where there is feeling to imprint them.
/// - The blooms live with the settlements (<see cref="Volunteer"/>): once Lumen Seeds reach the ground around one (seeds
///   planted nearby, whole Coherence, pollinators), a Content settlement draws up the gentle blooms, a Suffering one the
///   blooms of grief and shame, and districts their own (the Indulgent District's Lust Berries). Blooms that move creep
///   toward better ground Echo by Echo (<see cref="Drift"/>), and the creatures drawn to them (<see cref="SpeciesSpec.blooms"/>)
///   thrive and make their dens beside them (<see cref="BloomGround"/>, <see cref="WorldEcology"/>).
/// </summary>
public static class WorldResources
{
    // ===== PLACEMENT =====

    /// <summary>Place the sites that arrive in Age <paramref name="age"/> (once per kind).</summary>
    public static void PlaceAge(WorldMap map, WorldGenSettings settings, int age)
    {
        if (map == null || settings?.resourceSites == null) return;
        // Ground planted before this Age began is taken (the same after a load, when plantings are not marked yet).
        var planted = new HashSet<int>((map.Plantings ?? new List<Planting>()).Where(p => p != null && p.age < age).Select(p => p.cell));
        // Sterile blooms that grow by themselves are not placed with an Age: they sprout Echo by Echo (GrowEcho).
        foreach (var spec in settings.resourceSites.Where(s => s != null && !string.IsNullOrEmpty(s.id) && s.minAge == age && s.count > 0 && !s.sprouts)
            .OrderByDescending(s => s.requiresCoherentRefuge))
        {
            if (map.ResourceSites.Any(s => !s.planted && s.spec == spec.id)) continue;
            int placed = PlacePatches(map, settings, spec, age, spec.count, planted, "resource-site:" + spec.id, "resource-centre:" + spec.id);
            if (placed < spec.count) map.Report.notes.Add($"Resource site '{spec.id}': {placed} of {spec.count} placed (not enough fitting ground).");
        }
    }

    /// <summary>
    /// A new den of <paramref name="spec"/> in the range of the Macro Biome in <paramref name="slot"/>, where its creatures
    /// have spread (<see cref="WorldEcology"/>); null when no ground there fits. <paramref name="stream"/> names the draw.
    /// The Echo may pass what it already gathered, for speed (the result is the same): <paramref name="slotTiles"/>, every
    /// tile of the slot in map order, and <paramref name="drawn"/>, the species' <see cref="BloomGround"/>.
    /// </summary>
    public static ResourceSite PlaceDen(WorldMap map, WorldGenSettings settings, ResourceSiteSpec spec, int age, int slot, string stream,
        IEnumerable<WorldTile> slotTiles = null, Dictionary<int, float> drawn = null)
    {
        // Unmerged sprites remain the rarest variant even after migration founds new populations.
        if (spec.requiresCoherentRefuge && map.ResourceSites.Count(s => s.spec == spec.id) >= spec.count) return null;
        var planted = new HashSet<int>((map.Plantings ?? new List<Planting>()).Where(p => p != null).Select(p => p.cell));
        int before = map.ResourceSites.Count;
        // Creatures drawn to Eleos Blooms (SpeciesSpec.blooms) make their den beside the blooms that draw them, where any do.
        drawn = drawn ?? BloomGround(map, settings, settings.Species(spec.species));
        PlacePatches(map, settings, spec, age, 1, planted, "site:" + stream, "centre:" + stream, t => t.habitatSlot == slot,
            drawn.Count > 0 ? t => drawn.ContainsKey(t.index) : (Func<WorldTile, bool>)null, slotTiles);
        return map.ResourceSites.Count > before ? map.ResourceSites[map.ResourceSites.Count - 1] : null;
    }

    // Up to `wanted` new patches of `spec` on ground that fits, each from its own centre (the same map, streams and
    // ground give the same patches), centres that are `preferred` first. Returns how many were placed. `among` narrows
    // the search to tiles that include every one `where` accepts, in map order (the same centres, found sooner).
    private static int PlacePatches(WorldMap map, WorldGenSettings settings, ResourceSiteSpec spec, int age, int wanted, HashSet<int> planted, string siteStream, string centreStream, Func<WorldTile, bool> where = null, Func<WorldTile, bool> preferred = null, IEnumerable<WorldTile> among = null)
    {
        var rng = new Random(WorldNoise.Stream(map.seed, siteStream));
        int stream = WorldNoise.Stream(map.seed, centreStream);
        var centres = (among ?? map.Tiles).Where(t => (where == null || where(t)) && !planted.Contains(t.index) && map.StepsFromCapital(t.coord) >= spec.minDistance && Fits(map, settings, t, spec, true))
            .OrderBy(t => spec.requiresCoherentRefuge ? map.StepsFromCapital(t.coord) : 0)
            .ThenBy(t => preferred != null && preferred(t) ? 0 : 1)
            .ThenBy(t => WorldNoise.Hash01(stream, t.index, 0)).ToList();
        int placed = 0, size = Math.Max(1, spec.size);
        foreach (var centre in centres)
        {
            if (placed >= wanted) break;
            if (centre.resourceSite >= 0) continue;
            if (map.ResourceSites.Any(s => s.spec == spec.id && HexCoord.Distance(map[s.center].coord, centre.coord) < Math.Max(1, spec.spacing))) continue;
            var site = new ResourceSite { index = map.ResourceSites.Count, spec = spec.id, name = spec.name, kind = spec.kind, center = centre.index, age = age };
            site.cells.Add(centre.index);
            // A patch grows over neighbouring ground that suits it (the near-terrain and near-site rules are the centre's alone).
            var frontier = new List<int> { centre.index };
            var seen = new HashSet<int> { centre.index };
            while (site.cells.Count < size && frontier.Count > 0)
            {
                var next = new List<int>();
                foreach (int cell in frontier)
                    foreach (var n in map.NeighboursOf(map[cell]))
                        if (seen.Add(n.index) && (where == null || where(n)) && !planted.Contains(n.index) && Fits(map, settings, n, spec, false)) next.Add(n.index);
                foreach (int cell in next.OrderBy(_ => rng.NextDouble()))
                {
                    if (site.cells.Count >= size) break;
                    site.cells.Add(cell);
                }
                frontier = next;
            }
            foreach (int cell in site.cells) map[cell].resourceSite = site.index;
            map.ResourceSites.Add(site);
            placed++;
        }
        return placed;
    }

    /// <summary>
    /// The land moving with the game's clock, once an Echo (and once at the world's start, <paramref name="initial"/>,
    /// after the Old World's ruins). In order:
    /// - Turning: a patch whose turn is due (<see cref="TurnDue"/>: sorrow reaching Fated Flowers, Coherence reaching
    ///   Forsaken ones) runs <see cref="ResourceSiteSpec.turnPerEcho"/> further, plus
    ///   <see cref="ResourceSiteSpec.turnPerDissonance"/> per point of its Dissonance (high Dissonance decays Fated
    ///   Flowers within one Echo). Otherwise it recedes. At 1 it becomes <see cref="ResourceSiteSpec.turnsInto"/>, unless
    ///   that site's own turn would be due there at once (no flickering between the two).
    /// - Fading: a sprouted patch whose centre is no longer <see cref="Rooted"/> (the silver river moved with the
    ///   leylines, the Forsaken Flowers healed) fades by <see cref="ResourceSiteSpec.fadePerEcho"/>, and is gone at 1. A
    ///   volunteer bloom fades the same way once its settlement no longer draws it (<see cref="StillDrawn"/>).
    /// - Drifting: blooms that move creep a cell toward better ground (<see cref="Drift"/>).
    /// - Sprouting: every sterile bloom that <see cref="ResourceSiteSpec.sprouts"/> grows new patches where it is rooted,
    ///   <see cref="ResourceSiteSpec.sproutPerEcho"/> at most (the world's start: up to count), until count stand.
    /// - Volunteering: the settlements' lives draw blooms up around them (<see cref="Volunteer"/>).
    /// <paramref name="echo"/> counts the Echoes grown (saved by WorldSystem), so a world grows the same way once, and
    /// nothing grows on a load.
    /// </summary>
    public static List<GrowthChange> GrowEcho(WorldMap map, WorldGenSettings settings, int echo, int age, bool initial = false)
    {
        var changes = new List<GrowthChange>();
        if (map == null || settings?.resourceSites == null) return changes;
        var eleos = settings.eleos ?? new EleosSettings();
        Residue(map, eleos);
        var gone = new List<ResourceSite>();
        foreach (var site in map.ResourceSites.Where(s => !s.planted && s.cells.Count > 0).ToList())
        {
            var spec = settings.ResourceSite(site.spec);
            if (spec == null) continue;
            var into = !string.IsNullOrEmpty(spec.turnsInto) ? settings.ResourceSite(spec.turnsInto) : null;
            if (into != null && !initial)
            {
                float step = Math.Max(0f, spec.turnPerEcho);
                if (TurnDue(map, site, spec)) site.turning += Math.Max(0f, step + spec.turnPerDissonance * site.cells.Average(c => map[c].dissonance));
                else site.turning = Math.Max(0f, site.turning - step);
                if (site.turning >= 1f && !TurnDue(map, site, into))
                {
                    changes.Add(new GrowthChange { site = site, kind = GrowthKind.Turned, from = spec.id, fromName = spec.name });
                    site.spec = into.id;
                    site.name = into.name;
                    site.kind = into.kind;
                    site.turning = 0f;
                    site.fading = 0f;
                    continue;
                }
                site.turning = Math.Min(1f, site.turning);
            }
            // A volunteer bloom lives on its settlement's life; a sprouted one on its ways to grow.
            bool tended = site.tendedBy >= 0;
            float fade = tended ? eleos.volunteerFadePerEcho : spec.sprouts ? spec.fadePerEcho : 0f;
            if (fade > 0f && !initial)
            {
                bool holds = tended ? StillDrawn(map, settings, site, spec) : Rooted(map, map[site.center], spec);
                site.fading = holds ? Math.Max(0f, site.fading - fade) : site.fading + fade;
                if (site.fading >= 1f)
                {
                    gone.Add(site);
                    changes.Add(new GrowthChange { site = site, kind = GrowthKind.Faded, from = spec.id, fromName = spec.name });
                }
            }
        }
        if (gone.Count > 0)
        {
            map.ResourceSites.RemoveAll(gone.Contains);
            Mark(map);
        }
        // The blooms that move creep toward better ground (Fated Flowers toward history and away from sorrow).
        if (!initial)
            foreach (var site in map.ResourceSites.Where(s => !s.planted && s.cells.Count > 0).ToList())
                Drift(map, settings, site, settings.ResourceSite(site.spec), echo);
        var planted = new HashSet<int>((map.Plantings ?? new List<Planting>()).Where(p => p != null).Select(p => p.cell));
        // What grows beside other sites sprouts after them (Glimmerfern after the Forsaken Flowers it grows around).
        foreach (var spec in settings.resourceSites.Where(s => s != null && s.sprouts && !string.IsNullOrEmpty(s.id) && s.minAge <= age && s.count > 0)
                     .OrderBy(s => s.nearSites != null && s.nearSites.Count > 0 ? 1 : 0))
        {
            int standing = map.ResourceSites.Count(s => !s.planted && s.spec == spec.id);
            int wanted = spec.count - standing;
            if (!initial && spec.sproutPerEcho > 0) wanted = Math.Min(wanted, spec.sproutPerEcho);
            if (wanted <= 0) continue;
            int before = map.ResourceSites.Count;
            PlacePatches(map, settings, spec, age, wanted, planted, $"resource-sprout:{spec.id}:{echo}", $"resource-sprout-centre:{spec.id}:{echo}");
            foreach (var site in map.ResourceSites.Skip(before)) changes.Add(new GrowthChange { site = site, kind = GrowthKind.Sprouted });
        }
        // Last, what the settlements' lives draw up around them.
        if (!initial) changes.AddRange(Volunteer(map, settings, echo, age));
        return changes;
    }

    // ===== BLOOMS THAT MOVE =====

    // How much worse a cell may be than the one a drifting patch lets go of: on even ground blooms still wander.
    private const float WanderSlack = 0.03f;

    /// <summary>
    /// A patch creeping a cell this Echo (<see cref="ResourceSiteSpec.driftPerEcho"/>, a chance): it lets go of its
    /// poorest cell and takes the best fitting ground beside it (<see cref="DriftGround"/>). Volunteers stay by their
    /// settlement, a withering bloom cannot move (Eleos Bloom.md: no residue, no movement), and a den keeps to its Macro
    /// Biome. Returns whether it moved.
    /// </summary>
    public static bool Drift(WorldMap map, WorldGenSettings settings, ResourceSite site, ResourceSiteSpec spec, int echo)
    {
        if (map == null || site == null || spec == null || spec.driftPerEcho <= 0f || site.planted || site.tendedBy >= 0 || site.cells.Count == 0) return false;
        if (spec.kind == ResourceKind.Bloom && Vigor(map, site, spec) < (settings.eleos?.witherBelow ?? 0.2f)) return false;
        if (WorldNoise.Hash01(WorldNoise.Stream(map.seed, "bloom-drift:" + spec.id), site.center, echo) >= spec.driftPerEcho) return false;
        int jitter = WorldNoise.Stream(map.seed, $"bloom-drift-ground:{spec.id}:{echo}");
        float Score(WorldTile t) => DriftGround(map, t, spec) + 0.05f * (float)WorldNoise.Hash01(jitter, t.index, 0);
        int worst = site.cells.OrderBy(c => Score(map[c])).ThenBy(c => c).First();
        int slot = map[site.center].habitatSlot;
        bool den = !string.IsNullOrEmpty(spec.species);
        var own = new HashSet<int>(site.cells);
        WorldTile best = null;
        float bestScore = float.MinValue;
        foreach (int c in site.cells)
            foreach (var n in map.NeighboursOf(map[c]))
            {
                if (own.Contains(n.index) || (den && n.habitatSlot != slot) || !Fits(map, settings, n, spec, false)) continue;
                // One patch still: the new cell touches a cell that stays.
                if (site.cells.Count > 1 && !map.NeighboursOf(n).Any(m => m.index != worst && own.Contains(m.index))) continue;
                float s = Score(n);
                if (s > bestScore || (s == bestScore && best != null && n.index < best.index)) { best = n; bestScore = s; }
            }
        if (best == null || bestScore < Score(map[worst]) - WanderSlack) return false;
        map[worst].resourceSite = -1;
        site.cells.Remove(worst);
        site.cells.Add(best.index);
        best.resourceSite = site.index;
        if (site.center == worst) site.center = site.cells.OrderByDescending(c => Score(map[c])).ThenBy(c => c).First();
        return true;
    }

    /// <summary>
    /// How good a cell is for a drifting bloom: the residue it feeds on (enough is enough), the history and sorrow it
    /// needs, its ways to grow, and, for blooms that turn under sorrow (Fated Flowers), how far it is from sorrow.
    /// </summary>
    public static float DriftGround(WorldMap map, WorldTile t, ResourceSiteSpec spec)
    {
        if (t == null || spec == null) return 0f;
        float s = 0f;
        if (spec.residueNeed > 0f) s += Math.Min(t.residue, spec.residueNeed * 1.5f);
        if (spec.minimumHistory > 0f) s += t.history;
        if (spec.minimumHurt > 0f) s += Math.Min(t.hurt, spec.minimumHurt * 2f);
        if (spec.turnHurt > 0f) s -= 2f * t.hurt;
        if (spec.maximumHurt < 1f) s -= 2f * Math.Max(0f, t.hurt - spec.maximumHurt);
        bool ways = spec.nearSites.Count > 0 || spec.silverRiverReach > 0 || spec.silverLakeReach > 0;
        if (ways && Rooted(map, t, spec)) s += 0.5f;
        return s;
    }

    // ===== VOLUNTEER BLOOMS: WHAT A SETTLEMENT'S LIFE DRAWS UP =====

    /// <summary>A settlement's feelings this strong (their sum) draw up the blooms that catalogue the strongest of them.</summary>
    public const float FeelingDraw = 0.2f;
    /// <summary>How closely a people's feelings must match a bloom's cocktail (cosine) to draw it up.</summary>
    public const float FeelingFit = 0.55f;

    /// <summary>A settlement's mood as the blooms read it: Content at low Composure strain, Suffering at high, None between.</summary>
    public static BloomDraw MoodOf(EleosSettings eleos, Settlement s)
    {
        eleos = eleos ?? new EleosSettings();
        if (s == null) return BloomDraw.None;
        if (s.strain >= eleos.sufferingStrain) return BloomDraw.Suffering;
        return s.strain <= eleos.contentStrain ? BloomDraw.Content : BloomDraw.None;
    }

    /// <summary>
    /// Whether <paramref name="s"/>'s life draws <paramref name="spec"/> up beside it: its district is one the bloom
    /// follows, or its mood is the bloom's (<see cref="ResourceSiteSpec.drawnBy"/>). The sterile blooms and the
    /// unmerged-light refuges never volunteer.
    /// </summary>
    public static bool Draws(WorldGenSettings settings, Settlement s, ResourceSiteSpec spec) => DrawReason(settings, s, spec) != null;

    /// <summary>What in <paramref name="s"/>'s life draws <paramref name="spec"/> ("grief", "ease", or its district's id), or null.</summary>
    public static string DrawReason(WorldGenSettings settings, Settlement s, ResourceSiteSpec spec)
    {
        // Blooms that are a creature's den (WorldEcology) spread with their populations instead.
        if (s == null || spec == null || spec.kind != ResourceKind.Bloom || spec.sprouts || spec.requiresCoherentRefuge || !string.IsNullOrEmpty(spec.species)) return null;
        if (!string.IsNullOrEmpty(s.district) && spec.districts != null && spec.districts.Any(d => string.Equals(d, s.district, StringComparison.OrdinalIgnoreCase)))
            return s.district;
        // What its people feel most calls the bloom that catalogues it (Shame Moss where shame runs, Sorrowbells where grief does).
        var feelings = s.feelings;
        if (feelings != null && feelings.Total >= FeelingDraw && spec.flavors != null && spec.flavors.Count > 0 && EmotionalAlchemy.Fit(feelings, spec.flavors) >= FeelingFit)
            return EmotionalAlchemy.Shares(spec.flavors)[0].feeling.ToString().ToLowerInvariant();
        var mood = MoodOf(settings?.eleos, s);
        if (mood == BloomDraw.None || spec.drawnBy == BloomDraw.None) return null;
        if (spec.drawnBy != BloomDraw.Either && spec.drawnBy != mood) return null;
        return mood == BloomDraw.Suffering ? "grief" : "ease";
    }

    /// <summary>
    /// How Lumen Seeds reached the ground around <paramref name="s"/> (Eleos Bloom.md: no bloom without physical
    /// inheritance): "seeds" planted within <see cref="EleosSettings.seedReach"/>, "coherence" where the ground is whole
    /// enough for sprite pollinators, "pollinators" where a creature drawn to blooms thrives in its Macro Biome; null: none yet.
    /// </summary>
    public static string SeedSource(WorldMap map, WorldGenSettings settings, Settlement s)
    {
        var eleos = settings?.eleos ?? new EleosSettings();
        if (map == null || s == null) return null;
        foreach (var p in map.Plantings ?? new List<Planting>())
            if (p != null && p.cell >= 0 && p.cell < map.Count && HexCoord.Distance(map[p.cell].coord, s.coord) <= eleos.seedReach) return "seeds";
        float coherence = 0f;
        int land = 0;
        foreach (var c in HexCoord.Spiral(s.coord, Math.Max(1, eleos.volunteerReach)))
        {
            var t = map.Get(c);
            if (t == null || (t.water && !t.lake)) continue;
            coherence += t.coherence;
            land++;
        }
        if (land > 0 && coherence / land >= eleos.seedCoherence) return "coherence";
        var here = map.Get(s.coord);
        if (here != null && here.habitatSlot >= 0 && settings?.species != null)
            foreach (var species in settings.species)
            {
                if (species?.blooms == null || species.blooms.Count == 0) continue;
                var p = WorldEcology.Find(map, here.habitatSlot, species.id);
                if (p != null && p.groups > 0f && p.Abundance >= eleos.pollinatorAbundance) return "pollinators";
            }
        return null;
    }

    /// <summary>
    /// Once an Echo, each settlement whose ground holds Lumen Seeds (<see cref="SeedSource"/>) may draw a bloom up
    /// around it (<see cref="EleosSettings.volunteerChance"/>): one its mood or its district calls for (<see cref="Draws"/>),
    /// picked by <see cref="ResourceSiteSpec.volunteerWeight"/>, on ground people tend (<see cref="FitsTended"/>), up to
    /// <see cref="EleosSettings.volunteersPerSettlement"/> (and more beside a district). Deterministic by Echo.
    /// </summary>
    public static List<GrowthChange> Volunteer(WorldMap map, WorldGenSettings settings, int echo, int age)
    {
        var changes = new List<GrowthChange>();
        if (map?.Settlements == null || settings?.resourceSites == null) return changes;
        var eleos = settings.eleos ?? new EleosSettings();
        var planted = new HashSet<int>((map.Plantings ?? new List<Planting>()).Where(p => p != null).Select(p => p.cell));
        foreach (var s in map.Settlements.OrderBy(s => s.id).ToList())
        {
            int limit = eleos.volunteersPerSettlement + (string.IsNullOrEmpty(s.district) ? 0 : eleos.districtVolunteers);
            if (limit <= 0 || map.ResourceSites.Count(x => x.tendedBy == s.id) >= limit) continue;
            int stream = WorldNoise.Stream(map.seed, $"bloom-volunteer:{s.id}");
            if (WorldNoise.Hash01(stream, echo, 0) >= eleos.volunteerChance) continue;
            var drawn = settings.resourceSites.Where(spec => spec != null && !string.IsNullOrEmpty(spec.id) && spec.minAge <= age && spec.volunteerWeight > 0f && Draws(settings, s, spec)).ToList();
            if (drawn.Count == 0 || SeedSource(map, settings, s) == null) continue;
            // The pick first, then the others by weight, until one finds ground.
            float roll = (float)WorldNoise.Hash01(stream, echo, 1) * drawn.Sum(d => d.volunteerWeight);
            var pick = drawn.Last();
            foreach (var d in drawn)
            {
                roll -= d.volunteerWeight;
                if (roll < 0f) { pick = d; break; }
            }
            foreach (var spec in new[] { pick }.Concat(drawn.Where(d => d != pick).OrderByDescending(d => d.volunteerWeight).ThenBy(d => d.id, StringComparer.Ordinal)))
            {
                var site = PlaceVolunteer(map, settings, spec, s, age, echo, planted);
                if (site == null) continue;
                changes.Add(new GrowthChange { site = site, kind = GrowthKind.Volunteered, settlement = s, reason = DrawReason(settings, s, spec) });
                break;
            }
        }
        return changes;
    }

    // A volunteer patch of `spec` around `s`, or null when no tended ground there fits.
    private static ResourceSite PlaceVolunteer(WorldMap map, WorldGenSettings settings, ResourceSiteSpec spec, Settlement s, int age, int echo, HashSet<int> planted)
    {
        var eleos = settings.eleos ?? new EleosSettings();
        int reach = Math.Max(1, eleos.volunteerReach);
        int stream = WorldNoise.Stream(map.seed, $"bloom-volunteer-ground:{s.id}:{spec.id}:{echo}");
        bool Ok(WorldTile t) => t != null && !planted.Contains(t.index) && HexCoord.Distance(t.coord, s.coord) >= 1 && HexCoord.Distance(t.coord, s.coord) <= reach && FitsTended(map, settings, t, spec);
        var centre = HexCoord.Spiral(s.coord, reach).Select(map.Get).Where(Ok)
            .OrderBy(t => WorldNoise.Hash01(stream, t.index, 0)).FirstOrDefault();
        if (centre == null) return null;
        var site = new ResourceSite { index = map.ResourceSites.Count, spec = spec.id, name = spec.name, kind = spec.kind, center = centre.index, age = age, tendedBy = s.id };
        site.cells.Add(centre.index);
        int size = Math.Max(1, Math.Min(Math.Max(1, spec.size), eleos.volunteerSize));
        foreach (var n in map.NeighboursOf(centre).Where(Ok).OrderBy(t => WorldNoise.Hash01(stream, t.index, 1)))
        {
            if (site.cells.Count >= size) break;
            site.cells.Add(n.index);
        }
        foreach (int cell in site.cells) map[cell].resourceSite = site.index;
        map.ResourceSites.Add(site);
        return site;
    }

    /// <summary>
    /// Whether a volunteer bloom may take root on <paramref name="t"/>: open dry land with nothing on it, within the
    /// bloom's Coherence, and with feeling enough to imprint its Lumen Seed (half its residue need, as for planting).
    /// Ground people tend asks nothing of the wild's terrain, climate or history.
    /// </summary>
    public static bool FitsTended(WorldMap map, WorldGenSettings settings, WorldTile t, ResourceSiteSpec spec)
    {
        if (t == null || spec == null || t.water || t.impassable || t.coord == map.Capital || t.settlement >= 0) return false;
        if (t.resourceSite >= 0 || t.enclave >= 0 || t.HasFeature || t.grandfield >= 0) return false;
        var terrain = settings.Terrain(t.terrain);
        if (terrain == null || !terrain.passable || terrain.water) return false;
        float coherence = t.coherence - t.siteCoherence;
        if (coherence < spec.minimumCoherence || coherence > spec.maximumCoherence) return false;
        return t.residue >= spec.residueNeed * 0.5f;
    }

    /// <summary>Whether a volunteer bloom's settlement still stands near it and still draws it up.</summary>
    public static bool StillDrawn(WorldMap map, WorldGenSettings settings, ResourceSite site, ResourceSiteSpec spec)
    {
        if (site == null || site.tendedBy < 0) return true;
        var s = map.Settlements.Find(x => x.id == site.tendedBy);
        return s != null && HexCoord.Distance(s.coord, map[site.center].coord) <= Math.Max(1, settings.eleos?.volunteerReach ?? 2) + 1 && Draws(settings, s, spec);
    }

    // ===== BLOOMS IN THE FOOD WEB =====

    /// <summary>Whether <paramref name="species"/> is drawn to <paramref name="bloom"/> (<see cref="SpeciesSpec.blooms"/>: its id, its niche, or "bloom").</summary>
    public static bool DrawsCreature(SpeciesSpec species, ResourceSiteSpec bloom) =>
        species?.blooms != null && bloom != null && bloom.kind == ResourceKind.Bloom &&
        species.blooms.Any(b => string.Equals(b, bloom.id, StringComparison.OrdinalIgnoreCase) || string.Equals(b, "bloom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(b, NicheWord(bloom.niche), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The cells where thriving blooms draw <paramref name="species"/>, each with how strongly (the bloom's vigor times
    /// its falloff over <see cref="EleosSettings.drawReach"/>; the strongest bloom per cell). Empty for a species drawn to none.
    /// </summary>
    public static Dictionary<int, float> BloomGround(WorldMap map, WorldGenSettings settings, SpeciesSpec species) =>
        BloomGround(ThrivingBlooms(map, settings), species);

    /// <summary>The same from blooms already gathered (<see cref="ThrivingBlooms"/>, once an Echo for every species).</summary>
    public static Dictionary<int, float> BloomGround(List<(ResourceSiteSpec spec, List<(int cell, float strength)> reach)> blooms, SpeciesSpec species)
    {
        var ground = new Dictionary<int, float>();
        if (blooms == null || species?.blooms == null || species.blooms.Count == 0) return ground;
        foreach (var (spec, reach) in blooms)
        {
            if (!DrawsCreature(species, spec)) continue;
            foreach (var (cell, f) in reach)
                if (!ground.TryGetValue(cell, out float had) || f > had) ground[cell] = f;
        }
        return ground;
    }

    /// <summary>Every bloom that is not withering, with the cells it draws creatures over and how strongly (vigor x falloff).</summary>
    public static List<(ResourceSiteSpec spec, List<(int cell, float strength)> reach)> ThrivingBlooms(WorldMap map, WorldGenSettings settings)
    {
        var blooms = new List<(ResourceSiteSpec, List<(int, float)>)>();
        if (map == null || settings == null) return blooms;
        float wither = settings.eleos?.witherBelow ?? 0.2f;
        int reach = Math.Max(0, settings.eleos?.drawReach ?? 2);
        foreach (var site in map.ResourceSites)
        {
            if (site.kind != ResourceKind.Bloom) continue;
            var spec = settings.ResourceSite(site.spec);
            if (spec == null || spec.kind != ResourceKind.Bloom) continue;
            float vigor = Vigor(map, site, spec);
            if (vigor < wither) continue;
            blooms.Add((spec, Reach(map, site, reach).Select(r => (r.cell, vigor * r.falloff)).ToList()));
        }
        return blooms;
    }

    /// <summary>
    /// Whether <paramref name="spec"/>'s turn runs on <paramref name="site"/>'s ground this Echo: its mean hurt reaches
    /// <see cref="ResourceSiteSpec.turnHurt"/> (unless its Coherence holds, <see cref="ResourceSiteSpec.holdCoherence"/>),
    /// or its mean Coherence reaches <see cref="ResourceSiteSpec.turnCoherence"/>.
    /// </summary>
    public static bool TurnDue(WorldMap map, ResourceSite site, ResourceSiteSpec spec)
    {
        if (site == null || spec == null || site.cells.Count == 0) return false;
        float hurt = site.cells.Average(c => map[c].hurt), coherence = site.cells.Average(c => map[c].coherence);
        bool held = spec.holdCoherence > 0f && coherence >= spec.holdCoherence;
        return (spec.turnHurt > 0f && hurt >= spec.turnHurt && !held) || (spec.turnCoherence > 0f && coherence >= spec.turnCoherence);
    }

    // ===== SILVER RIVERS =====

    /// <summary>Steps recorded for a cell far from any silver river or silver-fed lake.</summary>
    public const int FarFromSilver = 99;

    // How far a silver river or a silver-fed lake is looked for (cells).
    private const int SilverSearch = 8;

    /// <summary>
    /// Cell steps from every cell to the nearest silver river (a leyline running down a real river) and to the nearest
    /// lake a silver river runs into (<see cref="WorldTile.silverRiverSteps"/>, <see cref="WorldTile.silverLakeSteps"/>):
    /// Glimmerfern "thrives in moonlit groves" along them. A lake counts whole once a silver river reaches any of it.
    /// </summary>
    public static void SilverShores(WorldMap map)
    {
        if (map == null) return;
        var river = new List<int>();
        var lakeSeeds = new List<int>();
        for (int i = 0; i < map.Count; i++)
        {
            var t = map[i];
            t.silverRiverSteps = t.silverLakeSteps = FarFromSilver;
            if (!t.silver || t.lake) continue;
            if (t.river) river.Add(i);
            if (t.downstream >= 0 && t.downstream < map.Count && map[t.downstream].lake) lakeSeeds.Add(t.downstream);
            foreach (var n in map.NeighboursOf(t)) if (n.lake) lakeSeeds.Add(n.index);
        }
        foreach (var t in map.Tiles) if (t.lake && t.silver) lakeSeeds.Add(t.index);
        // The whole lake a silver river runs into.
        var lake = new HashSet<int>();
        var open = new Queue<int>(lakeSeeds.Distinct());
        while (open.Count > 0)
        {
            int c = open.Dequeue();
            if (!lake.Add(c)) continue;
            foreach (var n in map.NeighboursOf(map[c])) if (n.lake && !lake.Contains(n.index)) open.Enqueue(n.index);
        }
        Steps(map, river, (t, s) => t.silverRiverSteps = s);
        Steps(map, lake, (t, s) => t.silverLakeSteps = s);
    }

    // Breadth-first steps from the sources, up to SilverSearch.
    private static void Steps(WorldMap map, IEnumerable<int> sources, Action<WorldTile, int> set)
    {
        var steps = new Dictionary<int, int>();
        var open = new Queue<int>();
        foreach (int s in sources) if (steps.TryAdd(s, 0)) open.Enqueue(s);
        while (open.Count > 0)
        {
            int c = open.Dequeue();
            int d = steps[c];
            set(map[c], d);
            if (d >= SilverSearch) continue;
            foreach (var n in map.NeighboursOf(map[c]))
                if (steps.TryAdd(n.index, d + 1)) open.Enqueue(n.index);
        }
    }

    /// <summary>Whether a site of <paramref name="spec"/> may stand on <paramref name="t"/> (<paramref name="centre"/>: its heart, where the near-terrain rule applies).</summary>
    public static bool Fits(WorldMap map, WorldGenSettings settings, WorldTile t, ResourceSiteSpec spec, bool centre, bool habitat = false)
    {
        if (t == null || spec == null || t.water || t.impassable || t.coord == map.Capital || t.settlement >= 0) return false;
        // Habitat (WorldEcology): where a den's creatures could live, whatever else stands there.
        if (!habitat && (t.resourceSite >= 0 || t.enclave >= 0 || t.HasFeature || t.grandfield >= 0)) return false;
        // The spec's own cheap checks first: most cells fail here, before the terrain table is searched.
        if (spec.terrains.Count > 0 && !spec.terrains.Any(x => string.Equals(x, t.terrain, StringComparison.OrdinalIgnoreCase))) return false;
        if (spec.covers != null && spec.covers.Count > 0 && !spec.covers.Any(x => string.Equals(x, t.cover, StringComparison.OrdinalIgnoreCase))) return false;
        if (spec.macroBiomes.Count > 0 && !spec.macroBiomes.Any(x => string.Equals(x, t.macroBiome, StringComparison.OrdinalIgnoreCase))) return false;
        if (t.elevation < spec.minElevation || t.elevation > spec.maxElevation || t.moisture < spec.minMoisture) return false;
        if (t.temperature > spec.maxTemperature) return false;
        var terrain = settings.Terrain(t.terrain);
        if (terrain == null || !terrain.passable || terrain.water) return false;
        if (spec.coastal && !map.NeighboursOf(t).Any(n => n.water && !n.lake)) return false;
        if (spec.requiresCoherentRefuge && !CoherentRefuge(t)) return false;
        if (NaturalDesirability(t) < spec.minimumDesirability) return false;
        if (spec.requiresSacred && !t.sacred && !map.NeighboursOf(t).Any(n => n.sacred)) return false;
        // The land as it is without the sites' own touch (so a world placed again after a load places the same sites).
        float fertility = t.landFertility - t.siteFertility, coherence = t.coherence - t.siteCoherence;
        if (fertility < spec.minimumFertility) return false;
        if (coherence < spec.minimumCoherence || coherence > spec.maximumCoherence || RawDissonance(map, t) < spec.minimumDissonance) return false;
        if (spec.requiresFreshwater && WorldTerritory.Freshwater(map, t) <= 0f) return false;
        if (spec.requiresSilverOrLeyline && !(t.silver || t.leylines != 0 || map.NeighboursOf(t).Any(n => n.silver))) return false;
        if (t.hurt > spec.maximumHurt || !Rooted(map, t, spec, centre)) return false;
        if (centre && spec.nearTerrains.Count > 0)
        {
            bool near = false;
            foreach (var c in HexCoord.Spiral(t.coord, Math.Max(1, spec.nearReach)))
            {
                var n = map.Get(c);
                if (n != null && spec.nearTerrains.Any(x => string.Equals(x, n.terrain, StringComparison.OrdinalIgnoreCase))) { near = true; break; }
            }
            if (!near) return false;
        }
        return true;
    }

    /// <summary>Unmerged light persists only at sacred ground or converging leyline families.</summary>
    public static bool CoherentRefuge(WorldTile t) =>
        t != null && t.coherence - t.siteCoherence >= 0.8f && (t.sacred || t.junction >= 2);

    public static float NaturalDesirability(WorldTile t) => t == null || t.water || t.impassable ? 0f :
        0.45f * (t.landFertility - t.siteFertility) + 0.35f * (t.coherence - t.siteCoherence) + 0.2f * t.magicFertility;

    /// <summary>
    /// What lets a spec grow on <paramref name="t"/>: the history it needs, and any one of its ways to grow (hurtful
    /// residue, a site it grows beside, a silver river, a lake a silver river feeds). The near-site rule is the centre's
    /// alone (<paramref name="centre"/>). A sprouted patch whose centre is no longer rooted fades.
    /// </summary>
    public static bool Rooted(WorldMap map, WorldTile t, ResourceSiteSpec spec, bool centre = true)
    {
        if (t == null || spec == null || t.history < spec.minimumHistory) return false;
        bool askHurt = spec.minimumHurt > 0f, askNear = spec.nearSites != null && spec.nearSites.Count > 0;
        bool askRiver = spec.silverRiverReach > 0, askLake = spec.silverLakeReach > 0;
        if (!askHurt && !askNear && !askRiver && !askLake) return true;
        return (askHurt && t.hurt >= spec.minimumHurt) || (askNear && (!centre || NearSite(map, t, spec)))
            || (askRiver && t.silverRiverSteps <= spec.silverRiverReach) || (askLake && t.silverLakeSteps <= spec.silverLakeReach);
    }

    // A site of one of spec.nearSites stands within nearReach cells.
    private static bool NearSite(WorldMap map, WorldTile t, ResourceSiteSpec spec)
    {
        foreach (var c in HexCoord.Spiral(t.coord, Math.Max(1, spec.nearReach)))
        {
            var n = map.Get(c);
            if (n == null || n.resourceSite < 0 || n.resourceSite >= map.ResourceSites.Count) continue;
            string near = map.ResourceSites[n.resourceSite].spec;
            if (spec.nearSites.Any(x => string.Equals(x, near, StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    // ===== DERIVED STATE =====

    /// <summary>Number the sites in order and mark each cell with its site (after the list was restored from a save).</summary>
    public static void Mark(WorldMap map)
    {
        if (map == null) return;
        foreach (var t in map.Tiles) t.resourceSite = -1;
        for (int i = 0; i < map.ResourceSites.Count; i++)
        {
            var site = map.ResourceSites[i];
            site.index = i;
            foreach (int cell in site.cells)
                if (cell >= 0 && cell < map.Count) map[cell].resourceSite = i;
        }
    }

    /// <summary>
    /// Re-derive the planted sites from <see cref="WorldMap.Plantings"/>, each cell's site, each site's land value, and
    /// what the sites do to the land around them: beauty, Coherence (handed to the magic, which recomputes its fields
    /// when it changed) and land fertility (added to <see cref="WorldTile.landFertility"/>, the previous share taken back
    /// first). Called by <see cref="WorldCivilization.Rebuild"/> before beauty and authority.
    /// </summary>
    public static void Refresh(WorldMap map, WorldGenSettings settings)
    {
        if (map == null || settings == null) return;
        map.ResourceSites.RemoveAll(s => s.planted);
        for (int i = 0; i < map.ResourceSites.Count; i++) map.ResourceSites[i].index = i;
        var dangerBefore = map.Tiles.Select(t => t.siteDanger).ToArray();
        foreach (var t in map.Tiles)
        {
            t.resourceSite = -1;
            t.landFertility -= t.siteFertility;
            t.siteBeauty = t.siteCoherence = t.siteFertility = 0f;
            t.siteDissonance = t.siteDanger = t.lure = t.sanctuary = 0f;
            t.solace = t.siteBounty = 0f;
        }
        foreach (var site in map.ResourceSites)
            foreach (int cell in site.cells) map[cell].resourceSite = site.index;
        foreach (var p in map.Plantings ?? new List<Planting>())
        {
            var spec = p != null ? settings.ResourceSite(p.spec) : null;
            if (spec == null || p.cell < 0 || p.cell >= map.Count || map[p.cell].resourceSite >= 0 || map[p.cell].water) continue;
            var site = new ResourceSite { index = map.ResourceSites.Count, spec = spec.id, name = $"Planted {spec.name}", kind = spec.kind, center = p.cell, age = p.age, planted = true };
            site.cells.Add(p.cell);
            map[p.cell].resourceSite = site.index;
            map.ResourceSites.Add(site);
        }

        int n = map.Count;
        var eleos = settings.eleos ?? new EleosSettings();
        Residue(map, eleos);
        var fertility = new float[n];
        foreach (var site in map.ResourceSites)
        {
            var spec = settings.ResourceSite(site.spec);
            if (spec == null) continue;
            site.vigor = Vigor(map, site, spec, settings.eleos);
            site.potency = Potency(map, site, spec, settings.eleos);
            // A bloom gives (and threatens) as much as it thrives; a withering one nothing at all.
            float strength = (site.planted ? Math.Min(1f, spec.plantedShare) : 1f) * Gift(site, settings);
            site.landValue = spec.landValue * strength;
            bool bloom = spec.kind == ResourceKind.Bloom;
            foreach (var (cell, falloff) in Reach(map, site, spec.auraRadius))
            {
                var t = map[cell];
                bool own = t.resourceSite == site.index;
                if (!own) t.siteBeauty += spec.beautyAura * falloff * strength;
                t.siteCoherence += spec.coherenceAura * falloff * strength;
                fertility[cell] += spec.fertilityAura * falloff * strength;
                if (!bloom) continue;
                t.siteDissonance += spec.dissonanceAura * falloff * strength;
                if (spec.niche == BloomNiche.Predator)
                {
                    // Quadratic like a threat's reach.
                    t.siteDanger = Math.Max(t.siteDanger, spec.dangerAura * falloff * falloff * strength);
                    t.lure = Math.Max(t.lure, falloff * strength);
                }
                if (spec.soothe > 0f) t.sanctuary = Math.Max(t.sanctuary, spec.soothe * falloff * strength);
            }
            if (site.planted) fertility[site.center] += spec.plantedFertility;
            // Solace: what the place does for a weary legend (Glimmerfern's light along silver water), strongest on the site.
            if (spec.solace > 0f)
                foreach (var (cell, falloff) in Reach(map, site, spec.auraRadius))
                    map[cell].solace = Math.Max(map[cell].solace, spec.solace * falloff * strength);
            // Bounty: living things to gather around it (roots, game, fish, fruit), for a forager or a camp.
            if (Living(spec.kind))
                foreach (var (cell, falloff) in Reach(map, site, BountyReach))
                    map[cell].siteBounty = Math.Min(1f, map[cell].siteBounty + falloff * strength * (site.planted ? 0.5f : 1f));
            // A den whose creatures have grown hostile toward you (WorldBehavior) is dangerous to come near.
            float hostile = WorldBehavior.DenDanger(map, settings, site);
            if (hostile > 0f)
                foreach (var (cell, falloff) in Reach(map, site, settings.behavior.hostileReach))
                    map[cell].siteDanger = Math.Max(map[cell].siteDanger, hostile * falloff * falloff);
            // In the season of hunger (WorldRhythm: Silence) a predator's den reaches toward your land nearby.
            float hunger = WorldRhythm.HungerDanger(map, settings, site);
            if (hunger > 0f)
                foreach (var (cell, falloff) in Reach(map, site, settings.rhythm.hungerReach))
                    if (WorldAuthority.IsPlayers(map[cell].authorityId)) map[cell].siteDanger = Math.Max(map[cell].siteDanger, hunger * falloff);
        }
        // The ground's own solace (a moonlit grove), added to what grows on it.
        foreach (var t in map.Tiles)
        {
            float ground = settings.Terrain(t.terrain)?.solace ?? 0f;
            if (ground > 0f) t.solace += ground;
        }
        var shifts = new float[n];
        var dissonance = new float[n];
        for (int i = 0; i < n; i++)
        {
            var t = map[i];
            shifts[i] = t.water && !t.lake ? 0f : t.siteCoherence;
            dissonance[i] = t.water && !t.lake ? 0f : t.siteDissonance;
            if (t.water || fertility[i] == 0f) continue;
            float before = t.landFertility;
            t.landFertility = Math.Max(0f, Math.Min(1f, before + fertility[i]));
            t.siteFertility = t.landFertility - before;
        }
        if (map.Magic != null)
        {
            bool coherence = map.Magic.SetShifts(shifts), drunk = map.Magic.SetDissonanceShifts(dissonance);
            if (coherence || drunk) map.Magic.Refield(map);
        }
        for (int i = 0; i < n; i++)
            if (map[i].siteDanger != dangerBefore[i])
            {
                WorldSites.RecomputeDanger(map);
                break;
            }
    }

    // ===== ELEOS BLOOMS =====

    /// <summary>
    /// Emotional Residue in every cell (<see cref="WorldTile.residue"/>, 0-1): the wild's own, the settlements' people
    /// (strained ones more), the ruins of the fallen, and the Dissonance left unmetabolized in the soil (before the
    /// blooms drink it).
    /// </summary>
    public static void Residue(WorldMap map, EleosSettings eleos)
    {
        if (map == null) return;
        eleos = eleos ?? new EleosSettings();
        int n = map.Count;
        var residue = new float[n];
        var hurt = new float[n];
        var history = new float[n];
        // The living feelings of the land (the eight of the Emotional Register): each settlement's people spread what they feel,
        // a ruin its grief and a way of life gone.
        var living = new EmotionalRegister[n];
        for (int i = 0; i < n; i++) living[i] = new EmotionalRegister();
        void SpreadFeelings(HexCoord at, EmotionalRegister feel, float scale, int reach)
        {
            if (feel == null || feel.Total * scale <= 1e-4f) return;
            foreach (var coord in HexCoord.Spiral(at, Math.Max(0, reach)))
            {
                var t = map.Get(coord);
                if (t != null) living[t.index].Add(feel, scale * (1f - HexCoord.Distance(coord, at) / (float)(reach + 1)));
            }
        }
        // Summed, fading with distance (feeling adds up); history keeps the strongest past at each cell.
        void Spread(float[] into, HexCoord at, float amount, int reach, bool strongest = false)
        {
            if (amount <= 0f) return;
            reach = Math.Max(0, reach);
            foreach (var coord in HexCoord.Spiral(at, reach))
            {
                var t = map.Get(coord);
                if (t == null) continue;
                float f = amount * (1f - HexCoord.Distance(coord, at) / (float)(reach + 1));
                into[t.index] = strongest ? Math.Max(into[t.index], f) : into[t.index] + f;
            }
        }
        foreach (var s in map.Settlements)
        {
            float amount = s.kind == SettlementKind.Capital ? eleos.capitalResidue : s.kind == SettlementKind.Major ? eleos.majorResidue : eleos.settlementResidue;
            float strain = eleos.strainResidue * Math.Max(0f, Math.Min(1f, s.strain / 100f));
            Spread(residue, s.coord, amount + strain, eleos.settlementReach);
            Spread(hurt, s.coord, strain, eleos.settlementReach);
            Spread(history, s.coord, Math.Min(eleos.settlementHistoryMax, eleos.settlementHistoryPerAge * s.agesPresent), 2, true);
            SpreadFeelings(s.coord, s.feelings, 0.5f, eleos.settlementReach);
        }
        foreach (var ruin in map.Ruins ?? new List<Ruin>())
        {
            Spread(residue, ruin.coord, eleos.ruinResidue, eleos.ruinReach);
            Spread(hurt, ruin.coord, eleos.ruinResidue, eleos.ruinReach);
            Spread(history, ruin.coord, eleos.ruinHistory, eleos.ruinHistoryReach, true);
            var grief = new EmotionalRegister { tumult = eleos.ruinResidue * 0.5f, estrangement = eleos.ruinResidue * 0.5f };
            SpreadFeelings(ruin.coord, grief, 1f, eleos.ruinReach);
        }
        var atonalis = eleos.atonalisThreats ?? new List<string>();
        foreach (var threat in map.Threats ?? new List<ThreatSite>())
            if (threat.cell >= 0 && threat.cell < n && atonalis.Any(a => string.Equals(a, threat.spec, StringComparison.OrdinalIgnoreCase)))
                Spread(history, map[threat.cell].coord, eleos.atonalisHistory, eleos.atonalisReach, true);
        var historic = eleos.historicTerrains ?? new List<string>();
        for (int i = 0; i < n; i++)
        {
            var t = map[i];
            if (t.HasFeature) Spread(history, t.coord, eleos.featureHistory, 1, true);
            if (t.sacred) Spread(history, t.coord, eleos.sacredHistory, 2, true);
            if (t.oldRoad) history[i] = Math.Max(history[i], eleos.oldRoadHistory);
            if (historic.Any(h => string.Equals(h, t.terrain, StringComparison.OrdinalIgnoreCase))) history[i] = Math.Max(history[i], eleos.historicTerrainHistory);
        }
        for (int i = 0; i < n; i++)
        {
            var t = map[i];
            bool sea = t.water && !t.lake;
            // Suffering the land holds (battles, hunts, loss: WorldSuffering) is part of its sorrow, and a battlefield is history.
            float sorrow = eleos.dissonanceResidue * RawDissonance(map, t) + 0.5f * t.suffering;
            if (t.suffering > 0.2f) history[i] = Math.Max(history[i], Math.Min(1f, t.suffering * 0.5f));
            t.residue = sea ? 0f : Clamp01(eleos.ambientResidue + residue[i] + sorrow + 0.3f * (t.imprint?.Consonance ?? 0f));
            t.hurt = sea ? 0f : Clamp01(hurt[i] + sorrow);
            t.history = sea ? 0f : Clamp01(history[i]);
            (t.living = t.living ?? new EmotionalRegister()).Clear();
            if (!sea) t.living.Add(living[i]);
            // Where the Loom is whole the land itself feels: Wonder where Coherence runs high or leylines pass, Devotion on sacred ground.
            if (!sea)
            {
                t.living.Add(Feeling.Wonder, Math.Max(0f, t.coherence - 0.6f) + (t.leylines != 0 ? 0.08f : 0f) + (t.sacred ? 0.2f : 0f));
                if (t.sacred) t.living.Add(Feeling.Devotion, 0.2f);
            }
        }
        SilverShores(map);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>Cells around a living resource site whose forage it enriches (<see cref="WorldTile.siteBounty"/>).</summary>
    public const int BountyReach = 2;

    /// <summary>A site of living things a forager can gather beside (not stone, light or ore).</summary>
    public static bool Living(ResourceKind kind) => kind != ResourceKind.Mineral && kind != ResourceKind.Light;

    /// <summary>A cell's Dissonance before what grows on it.</summary>
    public static float RawDissonance(WorldMap map, WorldTile t) =>
        t == null ? 0f : map?.Magic != null && map.Magic.Age >= 0 ? map.Magic.BaseDissonance(t.index) : Math.Max(0f, t.dissonance - t.siteDissonance);

    /// <summary>A bloom's vigor: the mean residue over its cells, over what it needs (1 for any other site, or one that needs none).</summary>
    public static float Vigor(WorldMap map, ResourceSite site, ResourceSiteSpec spec, EleosSettings eleos = null)
    {
        if (site == null || spec == null || spec.kind != ResourceKind.Bloom || spec.residueNeed <= 0f || site.cells.Count == 0) return 1f;
        float legacy = site.cells.Average(c => map[c].residue) / spec.residueNeed;
        var palate = PalateOf(site, spec);
        if (palate == null) return Math.Max(0f, Math.Min(1f, legacy));
        // A bloom that eats a cocktail grows by what it can make of it here (EmotionalEvolution.Feed: a niche palate grows
        // less), never below three fifths of what the residue alone would give it.
        var feeding = FeedingAt(map, site, spec, eleos);
        return Math.Max(0f, Math.Min(1f, Math.Max(feeding.growth, 0.6f * legacy)));
    }

    /// <summary>
    /// A bloom's palate: its lineage's as it has evolved, else its kind's (<see cref="ResourceSiteSpec.flavors"/> and
    /// <see cref="ResourceSiteSpec.specificity"/>); null for a bloom that eats residue of any kind.
    /// </summary>
    public static Palate PalateOf(ResourceSite site, ResourceSiteSpec spec)
    {
        if (site?.lineage?.palate != null && site.lineage.palate.recipe != null && site.lineage.palate.recipe.Count > 0) return site.lineage.palate;
        if (spec?.flavors == null || spec.flavors.Count == 0) return null;
        return new Palate { recipe = spec.flavors, specificity = spec.specificity };
    }

    /// <summary>What a bloom's palate finds over its cells (their feelings averaged): food, fit, growth and potency.</summary>
    public static Feeding FeedingAt(WorldMap map, ResourceSite site, ResourceSiteSpec spec, EleosSettings eleos = null)
    {
        var palate = PalateOf(site, spec);
        if (map == null || site == null || palate == null || site.cells.Count == 0) return new Feeding { growth = 1f, potency = 1f, fitness = 1f };
        return EmotionalEvolution.Feed(palate, FeelingsOver(map, site.cells), spec.residueNeed, (eleos ?? new EleosSettings()).ambientResidue, eleos?.evolution);
    }

    /// <summary>The feelings of some cells, averaged.</summary>
    public static EmotionalRegister FeelingsOver(WorldMap map, IList<int> cells)
    {
        var r = new EmotionalRegister();
        if (map == null || cells == null || cells.Count == 0) return r;
        foreach (int c in cells) r.Add(WorldSuffering.Feelings(map[c]));
        r.Scale(1f / cells.Count);
        return r;
    }

    /// <summary>How potent a bloom's gifts are now (its palate's feeding; 1 for a bloom that eats residue of any kind).</summary>
    public static float Potency(WorldMap map, ResourceSite site, ResourceSiteSpec spec, EleosSettings eleos = null) =>
        PalateOf(site, spec) == null ? 1f : Math.Max(0.1f, FeedingAt(map, site, spec, eleos).potency);

    /// <summary>A bloom too starved of feeling to move or shine.</summary>
    public static bool Withering(ResourceSite site, WorldGenSettings settings) =>
        IsBloom(site, settings) && site.vigor < (settings?.eleos?.witherBelow ?? 0.2f);

    /// <summary>The share of its land value, yields and auras a site gives: 1; a bloom its vigor, a withering one none.</summary>
    public static float Gift(ResourceSite site, WorldGenSettings settings) =>
        !IsBloom(site, settings) ? 1f : Withering(site, settings) ? 0f : site.vigor * (site.potency > 0f ? Math.Min(2.5f, site.potency) : 1f);

    /// <summary>An Eleos Bloom (by its spec, so a site saved before its kind changed follows the data).</summary>
    public static bool IsBloom(ResourceSite site, WorldGenSettings settings) =>
        site != null && (settings?.ResourceSite(site.spec)?.kind ?? site.kind) == ResourceKind.Bloom;

    public static string NicheWord(BloomNiche niche)
    {
        switch (niche)
        {
            case BloomNiche.Healer: return "healer";
            case BloomNiche.Predator: return "predator";
            default: return "listener";
        }
    }

    /// <summary>The cells a site's aura reaches and how strongly (1 on the site, fading to its radius), strongest per cell.</summary>
    public static List<(int cell, float falloff)> Reach(WorldMap map, ResourceSite site, int radius)
    {
        var best = new Dictionary<int, float>();
        radius = Math.Max(0, radius);
        foreach (int c in site.cells)
            foreach (var coord in HexCoord.Spiral(map[c].coord, radius))
            {
                var t = map.Get(coord);
                if (t == null) continue;
                float f = 1f - HexCoord.Distance(coord, map[c].coord) / (float)(radius + 1);
                if (!best.TryGetValue(t.index, out float had) || f > had) best[t.index] = f;
            }
        return best.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToList();
    }

    // ===== WHAT THE MAP KNOWS =====

    public static ResourceSite SiteAt(WorldMap map, WorldTile t) =>
        map != null && t != null && t.resourceSite >= 0 && t.resourceSite < map.ResourceSites.Count ? map.ResourceSites[t.resourceSite] : null;

    public static ResourceSiteSpec SpecAt(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        var site = SiteAt(map, t);
        return site != null ? settings?.ResourceSite(site.spec) : null;
    }

    /// <summary>Surveyed (any of its cells explored) or planted: its name, yields, harvest and land value are known.</summary>
    public static bool Identified(WorldMap map, ResourceSite site) => site != null && map != null && (site.planted || site.cells.Exists(c => map[c].explored));

    /// <summary>Something is known to be there: its cell is known, or it shows from afar. Inside concealing cover (a Deep
    /// Forest) nothing shows until the cell is explored.</summary>
    public static bool Sighted(WorldTile t, ResourceSite site, ResourceSiteSpec spec)
    {
        if (site == null || t == null) return false;
        if (site.planted) return true;
        if (t.concealed) return t.explored;
        return t.known || (spec != null && spec.visibleFromAfar && t.revealed);
    }

    public static string KindWord(ResourceKind kind)
    {
        switch (kind)
        {
            case ResourceKind.Crop: return "crop";
            case ResourceKind.Flora: return "flora";
            case ResourceKind.Fauna: return "fauna";
            case ResourceKind.Mineral: return "mineral";
            case ResourceKind.Light: return "light";
            case ResourceKind.Bloom: return "bloom";
            default: return "water";
        }
    }

    /// <summary>What a sighted but unsurveyed site looks like from where the expedition stood.</summary>
    public static string Hint(ResourceKind kind)
    {
        switch (kind)
        {
            case ResourceKind.Crop: return "something grows here in rows no one planted";
            case ResourceKind.Flora: return "a glow among the leaves";
            case ResourceKind.Fauna: return "tracks, and a light that moves";
            case ResourceKind.Mineral: return "a glint in the rock";
            case ResourceKind.Light: return "light falls here from nowhere";
            case ResourceKind.Bloom: return "petals that brighten as you draw near";
            default: return "the water sings";
        }
    }

    /// <summary>The site's name as the map knows it: its name once identified, "Unidentified flora" once sighted, else null.</summary>
    public static string Label(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        var site = SiteAt(map, t);
        if (site == null) return null;
        if (Identified(map, site)) return site.name;
        return Sighted(t, site, settings?.ResourceSite(site.spec)) ? $"Unidentified {KindWord(site.kind)}" : null;
    }

    /// <summary>What an identified site adds to its cell's land value (0 when none or not identified).</summary>
    public static float LandValue(WorldMap map, WorldTile t)
    {
        var site = SiteAt(map, t);
        return Identified(map, site) ? site.landValue : 0f;
    }

    /// <summary>How much of a wild site's yields a site gives: 1 wild; planted, its share times the cell's fertility over 0.5.</summary>
    public static float YieldScale(ResourceSite site, ResourceSiteSpec spec, WorldTile t)
    {
        if (site == null || spec == null) return 0f;
        if (!site.planted) return 1f;
        return Math.Max(0f, spec.plantedShare) * Math.Max(0.2f, Math.Min(1.6f, t.landFertility / 0.5f));
    }

    /// <summary>What an identified site yields per second on its cell (before improvement, beauty and administration), whether or not it is held.</summary>
    public static List<ResourceAmount> YieldsAt(WorldMap map, WorldGenSettings settings, WorldTile t)
    {
        var site = SiteAt(map, t);
        var spec = site != null ? settings.ResourceSite(site.spec) : null;
        if (spec == null || !Identified(map, site)) return new List<ResourceAmount>();
        // A patch's yields are shared among its cells; a bloom's follow its vigor.
        // A den's yields follow how many creatures its Macro Biome holds (WorldEcology); a depleted one gives nothing.
        float scale = YieldScale(site, spec, t) / Math.Max(1, site.cells.Count) * Gift(site, settings) * Plenty(map, settings, site);
        if (scale <= 0f) return new List<ResourceAmount>();
        return spec.yields.Where(y => y != null && !string.IsNullOrEmpty(y.resource) && y.amount != 0f)
            .Select(y => new ResourceAmount { resource = y.resource, amount = y.amount * scale }).ToList();
    }

    // ===== HARVEST AND PLANTING =====

    /// <summary>What a harvest here brings back (times <paramref name="multiplier"/>; a planted patch its share).</summary>
    public static List<ResourceAmount> HarvestAt(WorldMap map, WorldGenSettings settings, WorldTile t, float multiplier)
    {
        var site = SiteAt(map, t);
        var spec = site != null ? settings.ResourceSite(site.spec) : null;
        if (spec == null) return new List<ResourceAmount>();
        float scale = Math.Max(0f, multiplier) * YieldScale(site, spec, t) * HarvestShare(site, settings) * Plenty(map, settings, site);
        if (scale <= 0f) return new List<ResourceAmount>();
        return spec.harvest.Where(h => h != null && !string.IsNullOrEmpty(h.resource) && h.amount > 0f)
            .Select(h => new ResourceAmount { resource = h.resource, amount = h.amount * scale }).ToList();
    }

    /// <summary>A den already hunted in Phase <paramref name="phase"/> (-1: unknown, never).</summary>
    public static bool HuntedThisPhase(ResourceSite site, int phase) => site != null && phase >= 0 && site.huntedPhase == phase;

    /// <summary>"ready again in 5 Sevenths" (or "ready again next Phase" when the Sevenths are unknown).</summary>
    public static string HuntCooldownText(int seventhsLeft) =>
        seventhsLeft < 0 ? "ready again next Phase" : seventhsLeft == 1 ? "ready again in 1 Seventh" : $"ready again in {seventhsLeft} Sevenths";

    /// <summary>How much of its yields and harvest a den gives: its abundance (at most 1; nothing once depleted) times the season (<see cref="WorldRhythm.Yields"/>). 1 for every other site.</summary>
    public static float Plenty(WorldMap map, WorldGenSettings settings, ResourceSite site)
    {
        if (WorldEcology.SpeciesAt(settings, site) == null) return 1f;
        if (WorldEcology.PopulationAt(map, settings, site) == null) return WorldRhythm.Yields(map, settings);
        return WorldEcology.Depleted(map, settings, site) ? 0f : Math.Min(1f, WorldEcology.AbundanceAt(map, settings, site)) * WorldRhythm.Yields(map, settings);
    }

    /// <summary>
    /// The share of its harvest a site gives: 1; a den one hunt's share (<see cref="EcologySettings.huntYield"/>: it is
    /// hunted once a Phase); a bloom its vigor, a withering one only its fallen leaves.
    /// </summary>
    public static float HarvestShare(ResourceSite site, WorldGenSettings settings)
    {
        if (settings?.ecology != null && WorldEcology.SpeciesAt(settings, site) != null) return settings.ecology.huntYield;
        if (!IsBloom(site, settings)) return 1f;
        float fallen = settings?.eleos?.witheredHarvest ?? 0.25f;
        return Withering(site, settings) ? fallen : Math.Max(fallen, site.vigor);
    }

    /// <summary>
    /// Why no harvest can be gathered on <paramref name="t"/> in Age <paramref name="age"/>, or null. A den is hunted once a
    /// Phase: <paramref name="phase"/> is the game's Phase count (-1: unknown) and <paramref name="seventhsLeft"/> the
    /// Sevenths until the next Phase, for the cooldown's wording.
    /// </summary>
    public static string WhyNotHarvest(WorldMap map, WorldGenSettings settings, WorldTile t, int age, int phase = -1, int seventhsLeft = -1)
    {
        var site = SiteAt(map, t);
        if (site == null) return "No resource site here.";
        if (!Identified(map, site)) return "Survey it first: no one knows yet what it is.";
        var spec = settings.ResourceSite(site.spec);
        if (spec == null || spec.harvest.All(h => h == null || h.amount <= 0f))
            return string.IsNullOrEmpty(spec?.untouchable) ? "Nothing here can be taken." : spec.untouchable;
        // Dens (WorldEcology) can be hunted once a Phase, while enough creatures are left.
        if (WorldEcology.SpeciesAt(settings, site) != null)
        {
            if (WorldEcology.Depleted(map, settings, site)) return "Too few are left to hunt: leave them to recover.";
            return HuntedThisPhase(site, phase) ? $"Already hunted this Phase: {HuntCooldownText(seventhsLeft)}." : null;
        }
        if (t.harvestedAge == age + 1) return "Already harvested this Age.";
        return null;
    }

    /// <summary>
    /// The authority a harvest on <paramref name="t"/> takes from: null in the wilderness and on your own ground (yours or
    /// an Outpost's), else the holder's authority id (an enclave's, an independent claim's).
    /// </summary>
    public static string Owner(WorldTile t) =>
        t == null || t.authorityId == WorldAuthority.Wilderness || WorldAuthority.IsPlayers(t.authorityId) ? null : t.authorityId;

    /// <summary>The holder's name for an authority id (an enclave's name, an independent claim's feature), for notices.</summary>
    public static string OwnerName(WorldMap map, WorldGenSettings settings, string authority)
    {
        if (string.IsNullOrEmpty(authority)) return null;
        if (authority.StartsWith("enclave:", StringComparison.Ordinal) && int.TryParse(authority.Substring(8), out int e) && e >= 0 && e < map.Enclaves.Count)
            return map.Enclaves[e].name;
        int colon = authority.LastIndexOf(':');
        if (colon > 0 && int.TryParse(authority.Substring(colon + 1), out int cell) && cell >= 0 && cell < map.Count && map[cell].HasFeature)
            return settings?.Feature(map[cell].feature)?.name ?? authority.Substring(0, colon);
        return authority;
    }

    /// <summary>The enclave behind an authority id, or null.</summary>
    public static Enclave EnclaveOf(WorldMap map, string authority) =>
        authority != null && authority.StartsWith("enclave:", StringComparison.Ordinal) && int.TryParse(authority.Substring(8), out int e) && e >= 0 && e < map.Enclaves.Count ? map.Enclaves[e] : null;

    /// <summary>Why seeds of <paramref name="specId"/> cannot be planted on <paramref name="t"/>, or null: fertile, surveyed ground you hold with nothing growing on it.</summary>
    public static string WhyNotPlant(WorldMap map, WorldGenSettings settings, WorldTile t, string specId)
    {
        var spec = settings?.ResourceSite(specId);
        if (spec == null) return "No seeds to plant.";
        if (!spec.seeds) return $"{spec.name} is sterile: it cannot be planted.";
        if (t == null || t.water) return "Seeds are planted on dry land.";
        if (t.impassable || settings.Terrain(t.terrain)?.passable == false) return "Nothing takes root on this ground.";
        if (!WorldAuthority.IsPlayers(t.authorityId)) return "Plant on land you hold.";
        if (!t.explored) return "Survey it first.";
        if (t.resourceSite >= 0) return "Something already grows here.";
        if (t.settlement >= 0) return "A settlement stands here.";
        if (t.landFertility < spec.plantFertility) return $"{spec.name} needs land fertility {spec.plantFertility:P0} ({t.landFertility:P0} here).";
        if (NaturalDesirability(t) < spec.minimumDesirability) return $"{spec.name} needs natural land desirability {spec.minimumDesirability:P0}.";
        if (t.coherence < spec.plantCoherence) return $"{spec.name} needs Coherence {spec.plantCoherence:P0} ({t.coherence:P0} here).";
        // A Lumen Seed with no emotional imprint sprouts weak, pale and sterile: it is planted where people feel.
        float imprint = spec.residueNeed * 0.5f;
        if (spec.kind == ResourceKind.Bloom && t.residue < imprint)
            return $"Lumen Seeds of {spec.name} need Emotional Residue {imprint:P0} to take ({t.residue:P0} here): plant near your people.";
        return null;
    }

    /// <summary>Plant seeds (check <see cref="WhyNotPlant"/> first); the caller rebuilds the civilization so it takes root.</summary>
    public static Planting Plant(WorldMap map, WorldTile t, string specId, int age)
    {
        var planting = new Planting { cell = t.index, spec = specId, age = age };
        map.Plantings.Add(planting);
        return planting;
    }

    // ===== GRIEVANCES =====

    /// <summary>Raise <paramref name="amount"/> grievances with <paramref name="authority"/> in a ledger (authority id: grievances held against you).</summary>
    public static void Raise(Dictionary<string, float> ledger, string authority, float amount)
    {
        if (ledger == null || string.IsNullOrEmpty(authority) || amount <= 0f) return;
        ledger.TryGetValue(authority, out float had);
        ledger[authority] = had + amount;
    }

    /// <summary>Grievances fade with time; a ledger entry that reaches zero is forgotten.</summary>
    public static void Decay(Dictionary<string, float> ledger, float amount)
    {
        if (ledger == null || amount <= 0f) return;
        foreach (var key in ledger.Keys.ToList())
        {
            float left = ledger[key] - amount;
            if (left <= 0.01f) ledger.Remove(key);
            else ledger[key] = left;
        }
    }
}
