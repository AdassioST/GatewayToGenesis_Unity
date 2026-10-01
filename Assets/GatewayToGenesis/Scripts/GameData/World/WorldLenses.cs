using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A way of reading the world map: each lens paints one layer of the same geography.</summary>
public enum WorldLens
{
    Normal,
    Settle,
    LandFertility,
    Coherence,
    MagicalFertility,
    Authority,
    Trade,
    Resources,
    Danger,
    Terrain,
    MacroBiomes,
    Composition,
    Weather,
    Beauty,
    Territory,
    Desirability,
    Culture,
    /// <summary>The Emotional Register: each cell in the colour of the feeling it holds most (living and imprinted).</summary>
    Feelings,
}

/// <summary>
/// The world view's lenses (like a strategy game's map lenses), each tied to City Development: the Settle lens paints
/// the City Development a settlement could reach on each cell; the others paint one of its terms (land fertility,
/// Coherence and leylines, magical fertility, trade, grandfields, danger) or the ground it stands on (authority,
/// terrain, biomes, composition). The hover line says what the cell under the cursor adds to a settlement there.
/// No scene state: the renderer turns <see cref="Value"/> into colour.
/// </summary>
public static class WorldLenses
{
    public static readonly WorldLens[] All = (WorldLens[])Enum.GetValues(typeof(WorldLens));

    public static string Name(WorldLens lens)
    {
        switch (lens)
        {
            case WorldLens.Weather: return "Weather";
            case WorldLens.Beauty: return "Beauty";
            case WorldLens.Feelings: return "Feelings";
            case WorldLens.Territory: return "Territorial pull";
            case WorldLens.Desirability: return "Desirability";
            case WorldLens.Culture: return "Culture";
            case WorldLens.Normal: return "Normal";
            case WorldLens.Settle: return "Settle";
            case WorldLens.LandFertility: return "Land fertility";
            case WorldLens.Coherence: return "Leylines & Coherence";
            case WorldLens.MagicalFertility: return "Magical fertility";
            case WorldLens.Authority: return "Authority";
            case WorldLens.Trade: return "Trade";
            case WorldLens.Resources: return "Resources";
            case WorldLens.Danger: return "Danger";
            case WorldLens.Terrain: return "Height";
            case WorldLens.MacroBiomes: return "Macro Biomes";
            default: return "Composition";
        }
    }

    /// <summary>The City Development term a lens shows, or null for lenses of the ground itself.</summary>
    /// <summary>A one-word label for the lens bar.</summary>
    public static string ShortName(WorldLens lens)
    {
        switch (lens)
        {
            case WorldLens.LandFertility: return "Fertility";
            case WorldLens.Coherence: return "Coherence";
            case WorldLens.MagicalFertility: return "Magic";
            case WorldLens.Territory: return "Pull";
            default: return Name(lens);
        }
    }

    public static DevelopmentTerm? Term(WorldLens lens)
    {
        switch (lens)
        {
            case WorldLens.LandFertility: return DevelopmentTerm.LandFertility;
            case WorldLens.Coherence: return DevelopmentTerm.Coherence;
            case WorldLens.MagicalFertility: return DevelopmentTerm.MagicalFertility;
            case WorldLens.Trade: return DevelopmentTerm.Trade;
            case WorldLens.Resources: return DevelopmentTerm.Resources;
            case WorldLens.Danger: return DevelopmentTerm.Danger;
            case WorldLens.Beauty: return DevelopmentTerm.Beauty;
            default: return null;
        }
    }

    /// <summary>
    /// The shared 0-100% scale the percentage lenses (Coherence, Land fertility, Magical fertility) paint with, as
    /// (value, r, g, b) stops blended between: black and red at the bottom, orange at 25%, yellow at 50%, green at 75%,
    /// light then dark blue above, so a cell at the top (<see cref="ScalePeak"/>) can be gold amid the blue.
    /// </summary>
    public static readonly (float at, float r, float g, float b)[] ScaleStops =
    {
        (0f, 0.03f, 0.02f, 0.03f),
        (0.125f, 0.62f, 0.07f, 0.07f),
        (0.25f, 0.95f, 0.47f, 0.08f),
        (0.5f, 0.96f, 0.9f, 0.25f),
        (0.75f, 0.22f, 0.74f, 0.3f),
        (0.875f, 0.42f, 0.74f, 1f),
        (1f, 0.07f, 0.16f, 0.58f),
    };

    /// <summary>The scale's gold: a cell at 100% (as the hover rounds it).</summary>
    public static readonly (float r, float g, float b) ScaleGold = (1f, 0.76f, 0.1f);

    public const float ScalePeak = 0.995f;

    /// <summary>The scale's colour at <paramref name="v"/> (0-1): gold at the peak, else blended between the stops.</summary>
    public static (float r, float g, float b) ScaleColor(float v)
    {
        if (v >= ScalePeak) return ScaleGold;
        v = Math.Max(0f, v);
        for (int i = 1; i < ScaleStops.Length; i++)
        {
            var hi = ScaleStops[i];
            if (v > hi.at) continue;
            var lo = ScaleStops[i - 1];
            float k = (v - lo.at) / (hi.at - lo.at);
            return (lo.r + (hi.r - lo.r) * k, lo.g + (hi.g - lo.g) * k, lo.b + (hi.b - lo.b) * k);
        }
        var top = ScaleStops[ScaleStops.Length - 1];
        return (top.r, top.g, top.b);
    }

    /// <summary>The scale's band as a word for the hover line, matching its colours at each 25%.</summary>
    public static string ScaleWord(float v) =>
        v >= ScalePeak ? "peak" : v >= 0.75f ? "high" : v >= 0.5f ? "fair" : v >= 0.25f ? "low" : "very low";

    /// <summary>Lenses that switch the leyline layer on (magic reads along the lines).</summary>
    public static bool ShowsLeylines(WorldLens lens) => lens == WorldLens.Coherence || lens == WorldLens.MagicalFertility;

    /// <summary>
    /// What a lens measures and why it matters, in a sentence or two. Its colours are not named here: the map key
    /// draws each one as a swatch beside its meaning (WorldRenderer.LensKey), and the hover names the colour under the
    /// cursor.
    /// </summary>
    public static string Legend(WorldLens lens)
    {
        switch (lens)
        {
            case WorldLens.Settle: return "The City Development a settlement could reach on each cell, capped by Coherence.";
            case WorldLens.LandFertility: return "Soil, freshwater and climate: the heaviest weight in City Development.";
            case WorldLens.Coherence: return "How whole the magic is; leylines drawn. High Coherence raises City Development and its ceiling.";
            case WorldLens.MagicalFertility: return "Coherence times the flow usable this Age. Silver rivers and leyline junctions feed it.";
            case WorldLens.Authority: return "Who administers each cell. Developing Towns may be founded only on your own authority.";
            case WorldLens.Trade: return "Roads and a connected Trade Nexus raise nearby City Development.";
            case WorldLens.Resources: return "Resource sites and Grandfields, and what sites do to the land around them. An Outpost extracts a Grandfield.";
            case WorldLens.Danger: return "Threats cast no aura: what hunts shows only by the signs your explorers find. Too much suffering pools into Formless Masses.";
            case WorldLens.Terrain: return "The lie of the land and the depth of the sea.";
            case WorldLens.MacroBiomes: return "Each cell's Macro Biome, blended across the intersections.";
            case WorldLens.Weather: return "Each weather front in its own colour; crisis fronts take priority. Inspect a tile or city for its local conditions.";
            case WorldLens.Composition: return "How the world was assembled: Quadrants, their Macro Biomes, handmade interiors and the intersections between them.";
            case WorldLens.Feelings: return "The feeling each cell holds most, brighter the more it holds. Your people give theirs off; battles, hunts and loss imprint the land; Eleos Blooms drink what they catalogue; too much pools into Formless Masses.";
            case WorldLens.Beauty: return "Society adopts and works fair land first; beauty adds a little City Development and eases administration.";
            case WorldLens.Territory: return "Which seat pulls each cell, how hard, and where society will adopt land next.";
            case WorldLens.Culture: return "How long your people have lived on the land (not whether they agree: each settlement keeps its own customs). Rootedness grows on held land, fastest near settlements.";
            case WorldLens.Desirability: return "Where people want to live: beauty, Coherence, magical and land fertility, leylines and the hexes around; danger and dissonance drive them off. Settlements grow faster on desirable ground and outskirt tributaries develop toward it.";
            default: return "The ground as it is.";
        }
    }

    /// <summary>The scale's bands for the map key, low to high (the words of <see cref="ScaleWord"/>); the last is the gold peak.</summary>
    public static readonly (float from, float to, string label)[] ScaleBands =
    {
        (0f, 0.25f, "Very low, under 25%"),
        (0.25f, 0.5f, "Low, 25-50%"),
        (0.5f, 0.75f, "Fair, 50-75%"),
        (0.75f, ScalePeak, "High, 75% and up"),
        (ScalePeak, 1f, "Peak, 100%"),
    };

    /// <summary>
    /// The lens's value at a cell, 0-1 (what the renderer colours). <paramref name="potential"/> is the Settle
    /// field from <see cref="CityDevelopment.PotentialField"/> (0-100), used by the Settle lens.
    /// </summary>
    public static float Value(WorldLens lens, WorldMap map, WorldTile t, float[] potential)
    {
        if (t == null) return 0f;
        switch (lens)
        {
            case WorldLens.Settle: return potential != null && t.index < potential.Length ? potential[t.index] / 100f : 0f;
            case WorldLens.Terrain: return t.elevation;
            case WorldLens.Territory: return Math.Max(0f, Math.Min(1f, t.pull));
            case WorldLens.Desirability: return WorldDesirability.Of(map, t);
            case WorldLens.Culture: return CultureField.Of(t.index);
            default:
                var term = Term(lens);
                return term.HasValue ? CityDevelopment.TileScore(map, t, term.Value) : 0f;
        }
    }

    /// <summary>What the cell under the cursor means in this lens, and what it adds to a settlement there.</summary>
    public static string Hover(WorldLens lens, WorldMap map, WorldTile t, SettlementRules rules, float[] potential, WorldGenSettings settings = null)
    {
        if (t == null || rules == null) return null;
        switch (lens)
        {
            case WorldLens.Settle:
                if (t.water || t.impassable) return "No settlement can stand here";
                float p = potential != null && t.index < potential.Length ? potential[t.index] : 0f;
                return $"City Development potential {p:0}, {SettleWord(p)} (ceiling {CityDevelopment.Ceiling(rules, t):0})";
            case WorldLens.LandFertility:
                return $"Land fertility {t.landFertility:P0} ({ScaleWord(t.landFertility)}): +{rules.landWeight * t.landFertility:0.#} City Development if worked on average";
            case WorldLens.Coherence:
                string lines = t.junction >= 3 ? ", a Leyline Basin" : t.junction == 2 ? ", a Leyline Convergence" : t.leylines != 0 ? ", on a leyline" : string.Empty;
                return $"Coherence {t.coherence:P0} ({ScaleWord(t.coherence)}){lines}: +{rules.coherenceWeight * t.coherence:0.#} City Development, ceiling {CityDevelopment.Ceiling(rules, t):0}";
            case WorldLens.MagicalFertility:
                return $"Magical fertility {t.magicFertility:P0} ({ScaleWord(t.magicFertility)}){(t.silver ? ", silver water" : string.Empty)}: +{rules.magicWeight * t.magicFertility:0.#} City Development";
            case WorldLens.Authority:
                return t.authorityId == WorldAuthority.Player ? $"Your Administrative Authority ({t.administrativeAuthority:P0} reach): Developing Towns may be founded here"
                    : t.authorityId == WorldAuthority.Outpost ? "A detached Outpost"
                    : t.authorityId == WorldAuthority.Wilderness ? (t.water || t.impassable ? "Unclaimed" : "Wilderness: Outposts only") : $"Held by {Owner(map, t)}";
            case WorldLens.Trade:
                if (t.nexus != null) return $"Trade Nexus site: {WorldSites.NexusName(t.nexus)} (+{rules.nexusWeight:0} City Development once your roads reach it)";
                if (t.tradeNode) return $"Trade Node on a road (+{rules.roadWeight:0} City Development alongside)";
                return t.road ? $"Road (+{rules.roadWeight:0} City Development alongside)" : "No road";
            case WorldLens.Resources:
                var site = WorldResources.SiteAt(map, t);
                if (site != null && WorldResources.Identified(map, site))
                    return $"{site.name}: land value {site.landValue:+0.#;-0.#;0} ({rules.resourceWeight * site.landValue:+0.#;-0.#;0} City Development to a settlement working it)";
                if (site != null && t.known) return "Something here is not yet identified: survey it";
                return t.grandfield >= 0 ? $"Grandfield, density {t.grandfieldDensity:P0}: an Outpost here extracts it (+{rules.grandfieldWeight:0} City Development nearby)" : "No resource site or grandfield";
            case WorldLens.Danger:
                {
                    var parts = new List<string>();
                    if (t.danger > 0.01f) parts.Add($"Hazard {t.danger:P0}: -{rules.dangerWeight * t.danger:0.#} City Development, slower travel");
                    if (t.known && t.signs > 0.05f) parts.Add($"fresh signs of a hunter ({t.signs:P0})");
                    if (t.known && t.suffering > 0.02f) parts.Add($"suffering {t.suffering:0.##}{(WorldSuffering.Pressure(t, t.suffering) >= WorldSuffering.SpawnPressure ? ": a Formless Mass may pool here" : string.Empty)}");
                    return parts.Count > 0 ? string.Join("; ", parts) : t.sacred ? "Sacred ground: calm" : "Calm";
                }
            case WorldLens.Terrain:
                return $"Height {t.elevation:P0}";
            case WorldLens.Beauty:
                if (t.water) return "Water";
                return $"{Capitalized(WorldBeauty.Word(t.beauty))} ({t.beauty:+0.00;-0.00;0}): {rules.beautyWeight * t.beauty:+0.#;-0.#;0} City Development if worked, {(rules.territory?.beautyWork ?? 0f) * t.beauty:+0%;-0%;0%} to its yields when held";
            case WorldLens.Territory:
                return TerritoryHover(map, t, settings);
            case WorldLens.Desirability:
                return DesirabilityHover(map, t);
            case WorldLens.Feelings:
                {
                    if (!t.known) return "Unknown ground";
                    var feel = WorldSuffering.Feelings(t);
                    if (feel.Total < 0.02f) return "Quiet: it holds no feeling to speak of";
                    return $"Holds {feel.Words(3)} ({feel.Total:0.##}){(WorldSuffering.Pressure(t, t.suffering) >= WorldSuffering.SpawnPressure ? ": heavy enough for a Formless Mass" : string.Empty)}";
                }
            case WorldLens.Culture:
                return CultureHover(t);
            case WorldLens.MacroBiomes:
                if (t.water && !t.lake) return "Open sea: no Macro Biome";
                return t.macroBiome == null ? "No Macro Biome" : $"{settings?.MacroBiome(t.macroBiome)?.name ?? t.macroBiome}{(t.composition == WorldComposition.Intersection ? ", blended across an intersection" : string.Empty)}";
            case WorldLens.Composition:
                return CompositionHover(t, settings);
            case WorldLens.Weather:
                {
                    var weather = CelestialWeatherSystemLogic.Instance?.WeatherAt(t.coord);
                    if (weather == null) return "No weather front over it";
                    return string.IsNullOrEmpty(weather.weatherDescription) ? weather.weatherDisplayName : $"{weather.weatherDisplayName}: {weather.weatherDescription}";
                }
            default:
                return null;
        }
    }

    /// <summary>The Settle lens's band for a City Development potential (0-100), matching its red, amber and green.</summary>
    public static string SettleWord(float potential) => potential >= 45f ? "rich" : potential >= 15f ? "fair" : "poor";

    /// <summary>How the cell was assembled: its Quadrant and Macro Biome, a handmade interior, a seam, or an intersection.</summary>
    public static string CompositionHover(WorldTile t, WorldGenSettings settings)
    {
        if (t == null) return null;
        if (t.composition == WorldComposition.Ocean) return "Ocean";
        if (t.composition == WorldComposition.Intersection) return "An intersection: procedural ground joining the Macro Biomes";
        string biome = t.macroBiome != null ? settings?.MacroBiome(t.macroBiome)?.name ?? t.macroBiome : null;
        string where = t.quadrant != null ? $"Quadrant {t.quadrant}{(biome != null ? $", {biome}" : string.Empty)}" : biome ?? "A Macro Biome";
        return where + (t.handmadeTile != null ? ": a handmade interior" : t.seam ? ": a seam at its edge" : string.Empty);
    }

    private static string Capitalized(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>The territorial reading of a cell: who pulls it, how hard, and whether society will adopt it (and why not).</summary>
    public static string TerritoryHover(WorldMap map, WorldTile t, WorldGenSettings settings)
    {
        if (t == null || map == null) return null;
        if (t.water || t.impassable) return "No one can live here: no pull holds it";
        var seat = map.territory?.Seat(t.pullSeat);
        string pulled = seat != null ? $"pulled by {seat.name} ({seat.held:0.#}/{seat.maxCells} cells held)" : "no seat of yours reaches it";
        if (WorldAuthority.IsPlayers(t.authorityId) && !WorldHoldings.Fillable(t, t.authorityId)) return $"Yours: pull {t.pull:0.00}, {pulled}";
        if (t.authorityId != WorldAuthority.Wilderness) return t.rivalPull > 0f ? $"Held by another authority (its pull {t.rivalPull:0.00})" : "Held by another authority";
        if (seat == null) return t.rivalPull > 0f ? $"Wilderness under a rival's pull ({t.rivalPull:0.00})" : "Wilderness beyond every pull";
        string why = WorldTerritory.WhyNotAdopt(map, settings, t, seat);
        float priority = WorldTerritory.Priority(map, settings, WorldTerritory.RulesOf(map), t, seat);
        string part = t.microHeldMask != 0 ? $" {WorldMap.SettledHexes(t)}/{WorldMap.OpenHexes(t)} hexes yours{(WorldAuthority.IsPlayers(t.authorityId) ? " (de facto)" : string.Empty)}." : string.Empty;
        return $"Pull {t.pull:0.00}, {pulled}; adoption priority {priority:0.00}.{part} {why ?? "Society will adopt it hex by hex in its turn."}";
    }

    /// <summary>The culture's reading of a cell: how deeply its ways have taken root, and whether they grow or fade here.</summary>
    public static string CultureHover(WorldTile t)
    {
        if (t == null) return null;
        if (t.water) return "Water";
        float p = CultureField.Of(t.index);
        bool held = WorldAuthority.IsPlayers(t.authorityId);
        if (p <= 0f) return held ? $"Your land, but the {CultureField.Name} ways have not reached it yet" : "No culture of yours here";
        string depth = p >= 0.9f ? "long lived in" : p >= 0.6f ? "deep-rooted" : p >= 0.3f ? "taking root" : "newly arrived";
        return $"{CultureField.Name} rootedness {p:P0} ({depth}){(held ? string.Empty : ": fading, the land is no longer held")}";
    }

    /// <summary>A cell's desirability in words, with its three largest parts.</summary>
    public static string DesirabilityHover(WorldMap map, WorldTile t)
    {
        if (t == null || map == null) return null;
        if (t.water || t.impassable) return "No one lives here";
        float v = WorldDesirability.Of(map, t);
        var parts = WorldDesirability.Breakdown(map, t).OrderByDescending(p => Math.Abs(p.share)).Take(3).Select(p => $"{p.what} {p.share * 100f:+0;-0}");
        return $"{Capitalized(WorldDesirability.Word(v))} ground, desirability {v:P0} ({string.Join(", ", parts)}): settlements grow x{WorldDesirability.RulesOf(map).GrowthFactor(v):0.0#} here, a tributary develops toward {100f * v:0}";
    }

    private static string Owner(WorldMap map, WorldTile t)
    {
        if (t.authorityId.StartsWith("enclave:", StringComparison.Ordinal) && int.TryParse(t.authorityId.Substring(8), out int i) && i >= 0 && i < map.Enclaves.Count)
            return map.Enclaves[i].name;
        return t.authorityId;
    }
}
