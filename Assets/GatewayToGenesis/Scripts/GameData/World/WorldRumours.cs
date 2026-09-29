using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What a rumour is about.</summary>
public enum RumourKind
{
    /// <summary>A landmark: a feature seen from afar or tagged "landmark".</summary>
    Landmark,
    /// <summary>Any other feature worth the walk: a legend waiting, a one-of-a-kind place.</summary>
    Place,
    SacredSite,
    /// <summary>A resource site that shows from afar (a spire, a god-ray).</summary>
    Wonder,
    /// <summary>The ruins of the Old World.</summary>
    Ruin,
    Enclave,
    /// <summary>A creature's den (the Bestiary lists these as rumoured creatures, never by name).</summary>
    Creature,
}

/// <summary>One thing heard of (saved): what it is about, where it is said to be, and whether it was found.</summary>
[Serializable]
public class Rumour
{
    /// <summary>What it is about ("feature:812", "den:1204"): each thing is rumoured once.</summary>
    public string key;
    public RumourKind kind;
    /// <summary>The cell it is about (never shown: the map shows <see cref="area"/>).</summary>
    public int cell;
    /// <summary>The cell the rumour points at: near the thing, never on it.</summary>
    public int area;
    /// <summary>What was heard, where, as the player reads it.</summary>
    public string text;
    /// <summary>Its compass words from the capital ("north-east") and its Sector once its Macro Biome is known.</summary>
    public string direction;
    /// <summary>The Seventh (from the calendar's count) it was heard in.</summary>
    public int heard;
    public bool confirmed;
    /// <summary>What it turned out to be (its name), once confirmed.</summary>
    public string truth;
    /// <summary>The species a creature rumour is about (never shown until it is identified).</summary>
    public string species;
}

/// <summary>Everything heard (saved whole by <see cref="RumourKeeper"/>).</summary>
[Serializable]
public class RumourState
{
    public List<Rumour> heard = new List<Rumour>();
    /// <summary>Rumours told so far (it seeds the next choice, so a save replays the same one).</summary>
    public int told;
    /// <summary>The world's start rumours were told.</summary>
    public bool started;
    /// <summary>Journeys completed when rumours were last brought home (an expedition returning brings one).</summary>
    public int journeys;
}

/// <summary>How rumours come and what following one pays. Every number is a proposal (Canon Gaps "Rumours").</summary>
[Serializable]
public class RumourTuning
{
    [UnityEngine.Tooltip("Rumours the people already tell when the world begins.")]
    [UnityEngine.Min(0)] public int atStart = 2;
    [UnityEngine.Tooltip("Rumours travellers bring each Echo.")]
    [UnityEngine.Min(0)] public int perEcho = 1;
    [UnityEngine.Tooltip("Rumours an expedition brings home when it completes a journey.")]
    [UnityEngine.Min(0)] public int perJourney = 1;
    [UnityEngine.Tooltip("Most rumours waiting to be followed at once (none are heard past it).")]
    [UnityEngine.Min(1)] public int maxOpen = 5;
    [UnityEngine.Tooltip("How far (cells) a rumour's area may lie from the thing itself.")]
    [UnityEngine.Min(0)] public int fuzz = 3;
    [UnityEngine.Tooltip("Radius (cells) of the area a rumour points at on the map.")]
    [UnityEngine.Min(1)] public int areaRadius = 5;
    [UnityEngine.Tooltip("Rumours of things this close to the capital (cells) are not told: the people can see them.")]
    [UnityEngine.Min(0)] public int minDistance = 5;
    [UnityEngine.Tooltip("Era Score for finding what a rumour spoke of (on top of whatever finding it pays).")]
    [UnityEngine.Min(0)] public int eraConfirmed = 1;
    [UnityEngine.Tooltip("Cells from the capital read as 'a few days away' and 'far'; past the second, 'at the edge of what anyone knows'.")]
    public int nearCells = 12, farCells = 30;
}

/// <summary>
/// Rumours, with no scene state (tested in <c>RumourTests</c>). Landmarks no longer show through the fog: the people hear
/// of them instead, as a direction from the capital (and the Sector once its Macro Biome has been seen, WorldSectors),
/// how far, and a vague word of what is there. The map marks the area a rumour points at, never the place; finding the
/// thing confirms the rumour (Era Score) and names it. The loop (Docs/Planning/DISCOVERY_LOOP.md): a rumour is a
/// hypothesis about the map that only going there can test.
/// </summary>
public static class WorldRumours
{
    /// <summary>Something that could be rumoured: its key, kind, cell and (for dens) species.</summary>
    public struct Subject
    {
        public string key;
        public RumourKind kind;
        public int cell;
        public string species;
        /// <summary>What it is called, told once it is found.</summary>
        public string name;
    }

    // ===== WHAT CAN BE RUMOURED =====

    /// <summary>Everything worth a rumour that the people have not found yet, in map order.</summary>
    public static List<Subject> Subjects(WorldMap map, WorldGenSettings settings)
    {
        var list = new List<Subject>();
        if (map == null || settings == null) return list;
        var capital = map.Get(map.Capital);
        foreach (var t in map.Tiles)
        {
            if (!t.HasFeature || t == capital) continue;
            var f = settings.Feature(t.feature);
            if (f == null) continue;
            bool landmark = f.visibleFromAfar || string.Equals(f.tag, "landmark", StringComparison.OrdinalIgnoreCase);
            bool place = landmark || f.recruitsLegend || f.count == 1 || !string.IsNullOrEmpty(f.rumour);
            if (place) list.Add(new Subject { key = "feature:" + t.index, kind = landmark ? RumourKind.Landmark : RumourKind.Place, cell = t.index, name = f.name });
        }
        foreach (int site in map.Magic?.SacredSites ?? new List<int>())
            list.Add(new Subject { key = "sacred:" + site, kind = RumourKind.SacredSite, cell = site, name = "a Sacred Site" });
        foreach (var site in map.ResourceSites)
        {
            if (site == null || site.planted || site.cells.Count == 0) continue;
            var spec = settings.ResourceSite(site.spec);
            if (spec == null) continue;
            if (!string.IsNullOrEmpty(spec.species))
                list.Add(new Subject { key = "den:" + site.center, kind = RumourKind.Creature, cell = site.center, species = spec.species, name = site.name });
            else if (spec.visibleFromAfar)
                list.Add(new Subject { key = "site:" + site.center, kind = RumourKind.Wonder, cell = site.center, name = site.name });
        }
        foreach (var ruin in map.Ruins.Where(r => r != null && r.ancient))
        {
            var t = map.Get(ruin.coord);
            if (t != null) list.Add(new Subject { key = "ruin:" + t.index, kind = RumourKind.Ruin, cell = t.index, name = $"the ruins of {ruin.name}" });
        }
        foreach (var e in map.Enclaves.Where(e => e != null))
        {
            var t = map.Get(e.coord);
            if (t != null) list.Add(new Subject { key = "enclave:" + e.index, kind = RumourKind.Enclave, cell = t.index, name = e.name });
        }
        return list.Where(s => !Found(map, settings, s)).ToList();
    }

    /// <summary>The people have found what the subject is: seen it (a landmark from afar, a Sacred Site, an Enclave), sighted a den, or walked the ground.</summary>
    public static bool Found(WorldMap map, WorldGenSettings settings, Subject s) => Found(map, settings, s.kind, s.key, s.cell);

    public static bool Found(WorldMap map, WorldGenSettings settings, Rumour r) => r != null && Found(map, settings, r.kind, r.key, r.cell);

    private static bool Found(WorldMap map, WorldGenSettings settings, RumourKind kind, string key, int cell)
    {
        if (map == null || cell < 0 || cell >= map.Count) return true;
        var t = map[cell];
        switch (kind)
        {
            case RumourKind.Landmark:
            {
                var f = t.HasFeature ? settings?.Feature(t.feature) : null;
                return t.known || (f != null && f.visibleFromAfar && t.revealed && !WorldCover.Hides(t));
            }
            case RumourKind.SacredSite:
            case RumourKind.Enclave: return t.revealed;
            case RumourKind.Wonder:
            case RumourKind.Creature:
            {
                var site = map.ResourceSites.FirstOrDefault(s => s != null && s.center == cell);
                return site == null || WorldResources.Sighted(t, site, settings?.ResourceSite(site.spec));
            }
            default: return t.known;
        }
    }

    // ===== WHERE =====

    /// <summary>"north-east", "south" ... of a cell seen from another (y grows north); "here" for the same cell.</summary>
    public static string Compass(WorldTile from, WorldTile to)
    {
        if (from == null || to == null || from == to) return "here";
        var sector = WorldSectors.Of(to.x - from.x, to.y - from.y, 0f);
        return (WorldSectors.Name(sector) ?? "Central Sector").Replace(" Sector", string.Empty).ToLowerInvariant();
    }

    /// <summary>"a few days away", "far", "at the edge of what anyone knows".</summary>
    public static string Distance(int cells, RumourTuning t)
    {
        t = t ?? new RumourTuning();
        return cells <= t.nearCells ? "a few days away" : cells <= t.farCells ? "far off" : "at the edge of what anyone knows";
    }

    /// <summary>
    /// Where a rumour says a thing is: its compass direction and distance from the capital, and, once any of its Macro
    /// Biome has been seen, its Sector there ("to the north-east, far off, in the North Sector of the Emberwild").
    /// </summary>
    public static string Where(WorldMap map, WorldGenSettings settings, int cell, RumourTuning t)
    {
        var capital = map?.Get(map.Capital);
        if (capital == null || cell < 0 || cell >= map.Count) return "somewhere beyond the fog";
        var tile = map[cell];
        string where = $"to the {Compass(capital, tile)}, {Distance(HexCoord.Distance(capital.coord, tile.coord), t)}";
        var biome = settings?.MacroBiome(tile.macroBiome);
        string sector = WorldSectors.Name(tile.sector);
        bool biomeSeen = biome != null && tile.slot >= 0 && map.Tiles.Any(o => o.slot == tile.slot && o.revealed);
        return biomeSeen && sector != null ? $"{where}, in the {sector} of the {biome.name}" : where;
    }

    // ===== WHAT IS SAID =====

    /// <summary>What travellers say of it, never its name: vague, true, and enough to go looking.</summary>
    public static string Saying(WorldMap map, WorldGenSettings settings, Subject s)
    {
        switch (s.kind)
        {
            case RumourKind.Landmark:
            case RumourKind.Place:
            {
                var f = map[s.cell].HasFeature ? settings.Feature(map[s.cell].feature) : null;
                if (f != null && !string.IsNullOrWhiteSpace(f.rumour)) return f.rumour.Trim();
                if (f != null && f.recruitsLegend) return "a stranger who waits alone, as if for someone";
                if (f != null && string.Equals(f.tag, "fertile", StringComparison.OrdinalIgnoreCase)) return "a place where the land gives without being asked";
                return s.kind == RumourKind.Landmark ? "a shape against the sky that no one has walked to" : "a place people speak of in low voices";
            }
            case RumourKind.SacredSite: return "a place where the air hums and the heart goes quiet";
            case RumourKind.Wonder:
            {
                var site = map.ResourceSites.FirstOrDefault(r => r != null && r.center == s.cell);
                return site != null ? WorldResources.Hint(site.kind) : "something that shines at night";
            }
            case RumourKind.Ruin: return "broken stones of a city older than the Ages";
            case RumourKind.Enclave:
            {
                var e = map.Enclaves.FirstOrDefault(x => x != null && map.Get(x.coord)?.index == s.cell);
                return e != null ? $"a people living apart, keeping the old ways of the {e.family}" : "a people living apart";
            }
            case RumourKind.Creature: return CreatureSaying(settings.Species(s.species));
            default: return "something no one can name";
        }
    }

    /// <summary>How hunters speak of a creature no one has seen: its size, how it meets people, and whether it shines. Never its name.</summary>
    public static string CreatureSaying(SpeciesSpec species)
    {
        if (species == null) return "tracks no one knows";
        var profile = CreatureTaxonomy.Profile(species.diet, species.subgroup);
        string size = species.size == CreatureSize.Gargantuan ? "creatures as big as hills" : species.size == CreatureSize.Large ? "large beasts"
            : species.size == CreatureSize.Small ? "small creatures" : "beasts";
        string ways = profile == null ? "no one has seen clearly" : ResponseWords(profile.response);
        string light = species.binding != BindingOrgan.None || CreatureTaxonomy.IsPureLightBeing(species) ? ", with a light about them" : string.Empty;
        return $"{size} that {ways}{light}";
    }

    private static string ResponseWords(ThreatResponse response)
    {
        switch (response)
        {
            case ThreatResponse.FleesOnSight: return "vanish the moment they are seen";
            case ThreatResponse.FleesWhenThreatened: return "graze calmly until someone comes too close";
            case ThreatResponse.Hides: return "are never where you looked a moment ago";
            case ThreatResponse.Unmoved: return "do not so much as look up when people pass";
            case ThreatResponse.AvoidsConflict: return "steal from camps and are gone before dawn";
            case ThreatResponse.DefendsWhenThreatened: return "keep to themselves, and fight when cornered";
            case ThreatResponse.DefendsTerritory: return "drive off anyone who crosses a line only they can see";
            case ThreatResponse.Expands: return "swarm over the ground a little further every year";
            case ThreatResponse.Unpredictable: return "limp as if hurt, then turn on whoever follows";
            case ThreatResponse.AttacksOnSight: return "charge at anything that moves";
            case ThreatResponse.Hunts: return "hunt together in the dark";
            case ThreatResponse.HuntsLoudly: return "shriek before they strike";
            case ThreatResponse.Lures: return "are too beautiful to be safe";
            default: return "let people come near";
        }
    }

    /// <summary>Who tells it: travellers, hunters, pilgrims...</summary>
    public static string Teller(RumourKind kind)
    {
        switch (kind)
        {
            case RumourKind.Creature: return "Hunters speak of";
            case RumourKind.SacredSite: return "Pilgrims speak of";
            case RumourKind.Enclave: return "Traders speak of";
            case RumourKind.Ruin: return "The old songs tell of";
            default: return "Travellers speak of";
        }
    }

    // ===== HEARING AND FINDING =====

    /// <summary>Rumours heard and not yet confirmed.</summary>
    public static List<Rumour> Open(RumourState state) => state?.heard.Where(r => r != null && !r.confirmed).ToList() ?? new List<Rumour>();

    /// <summary>
    /// Hear one rumour: of something not yet found and not already rumoured, beyond <see cref="RumourTuning.minDistance"/>,
    /// nearer things likelier. Its area lies within <see cref="RumourTuning.fuzz"/> of the thing, never on it when it can
    /// help it. Null when nothing is left to hear or <see cref="RumourTuning.maxOpen"/> rumours already wait.
    /// </summary>
    public static Rumour Hear(RumourState state, WorldMap map, WorldGenSettings settings, RumourTuning t, int heardAt)
    {
        if (state == null || map == null || settings == null) return null;
        t = t ?? new RumourTuning();
        if (Open(state).Count >= Math.Max(1, t.maxOpen)) return null;
        var capital = map.Get(map.Capital);
        if (capital == null) return null;
        var told = new HashSet<string>(state.heard.Where(r => r != null).Select(r => r.key), StringComparer.Ordinal);
        var candidates = Subjects(map, settings).Where(s => !told.Contains(s.key))
            .Select(s => (s, d: HexCoord.Distance(capital.coord, map[s.cell].coord))).Where(p => p.d >= t.minDistance).ToList();
        if (candidates.Count == 0) return null;
        var random = new Random(unchecked(map.seed * 7919 + state.told * 104729 + 17));
        double total = candidates.Sum(p => 1.0 / (1.0 + p.d / 10.0));
        double roll = random.NextDouble() * total;
        var pick = candidates[candidates.Count - 1].s;
        foreach (var (s, d) in candidates)
        {
            roll -= 1.0 / (1.0 + d / 10.0);
            if (roll <= 0) { pick = s; break; }
        }
        int area = Area(map, pick.cell, t.fuzz, random);
        string where = Where(map, settings, pick.cell, t);
        var rumour = new Rumour
        {
            key = pick.key, kind = pick.kind, cell = pick.cell, area = area, heard = heardAt, species = pick.species,
            direction = where, text = $"{Teller(pick.kind)} {Saying(map, settings, pick)}, {where}.",
        };
        state.heard.Add(rumour);
        state.told++;
        return rumour;
    }

    // A cell within fuzz of the thing (a random one, never the thing itself when another is in reach).
    private static int Area(WorldMap map, int cell, int fuzz, Random random)
    {
        var at = map[cell].coord;
        var near = new List<int>();
        if (fuzz > 0)
            for (int i = 0; i < map.Count; i++)
            {
                int d = HexCoord.Distance(at, map[i].coord);
                if (d >= 1 && d <= fuzz && !map[i].water) near.Add(i);
            }
        return near.Count == 0 ? cell : near[random.Next(near.Count)];
    }

    /// <summary>Rumours whose thing has now been found: marked confirmed and named. Returns them (for the notices and Era Score).</summary>
    public static List<Rumour> Confirm(RumourState state, WorldMap map, WorldGenSettings settings)
    {
        var confirmed = new List<Rumour>();
        if (state == null || map == null) return confirmed;
        foreach (var r in Open(state))
        {
            if (!Found(map, settings, r)) continue;
            r.confirmed = true;
            r.truth = TruthOf(map, settings, r);
            confirmed.Add(r);
        }
        return confirmed;
    }

    // What the rumour turned out to be: a place's name, a site's, a creature's only once identified (a sighting is not a name).
    private static string TruthOf(WorldMap map, WorldGenSettings settings, Rumour r)
    {
        var t = map[r.cell];
        switch (r.kind)
        {
            case RumourKind.Landmark:
            case RumourKind.Place: return t.HasFeature ? settings.Feature(t.feature)?.name : null;
            case RumourKind.SacredSite: return "a Sacred Site";
            case RumourKind.Wonder:
            {
                var site = map.ResourceSites.FirstOrDefault(s => s != null && s.center == r.cell);
                return site != null ? WorldResources.Label(map, settings, t) : null;
            }
            case RumourKind.Ruin: return WorldRuins.At(map, t.coord) is Ruin ruin ? $"the ruins of {ruin.name}" : "ruins";
            case RumourKind.Enclave: return map.Enclaves.FirstOrDefault(e => e != null && e.coord == t.coord)?.name;
            case RumourKind.Creature: return "a den of unidentified fauna";
            default: return null;
        }
    }

    /// <summary>The open rumours whose area covers a cell (for the map's hover card).</summary>
    public static IEnumerable<Rumour> Covering(RumourState state, WorldMap map, int cell, RumourTuning t)
    {
        if (state == null || map == null || cell < 0 || cell >= map.Count) yield break;
        int radius = Math.Max(1, (t ?? new RumourTuning()).areaRadius);
        foreach (var r in Open(state))
            if (r.area >= 0 && r.area < map.Count && HexCoord.Distance(map[r.area].coord, map[cell].coord) <= radius) yield return r;
    }

    /// <summary>Creature rumours not yet confirmed (the Bestiary's "rumoured creatures").</summary>
    public static List<Rumour> RumouredCreatures(RumourState state) => Open(state).Where(r => r.kind == RumourKind.Creature).ToList();

    /// <summary>Rumours confirmed: all, or of one kind ("landmark", "creature"...).</summary>
    public static int Confirmed(RumourState state, string kind = null) =>
        state?.heard.Count(r => r != null && r.confirmed && (string.IsNullOrWhiteSpace(kind) || string.Equals(r.kind.ToString(), kind.Trim(), StringComparison.OrdinalIgnoreCase))) ?? 0;
}
