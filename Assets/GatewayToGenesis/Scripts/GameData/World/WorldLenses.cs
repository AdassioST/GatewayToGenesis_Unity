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
    Biomes,
    Composition,
    Weather,
    Beauty,
    Territory,
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
            case WorldLens.Territory: return "Territorial pull";
            case WorldLens.Normal: return "Normal";
            case WorldLens.Settle: return "Settle";
            case WorldLens.LandFertility: return "Land fertility";
            case WorldLens.Coherence: return "Leylines & Coherence";
            case WorldLens.MagicalFertility: return "Magical fertility";
            case WorldLens.Authority: return "Authority";
            case WorldLens.Trade: return "Trade";
            case WorldLens.Resources: return "Grandfields";
            case WorldLens.Danger: return "Danger";
            case WorldLens.Terrain: return "Height";
            case WorldLens.Biomes: return "Biomes";
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
            case WorldLens.Resources: return DevelopmentTerm.Grandfields;
            case WorldLens.Danger: return DevelopmentTerm.Danger;
            case WorldLens.Beauty: return DevelopmentTerm.Beauty;
            default: return null;
        }
    }

    /// <summary>Lenses that switch the leyline layer on (magic reads along the lines).</summary>
    public static bool ShowsLeylines(WorldLens lens) => lens == WorldLens.Coherence || lens == WorldLens.MagicalFertility;

    public static string Legend(WorldLens lens)
    {
        switch (lens)
        {
            case WorldLens.Settle: return "City Development a settlement could reach here: red poor, amber fair, green rich; bright where you may found a town now. Capped by Coherence.";
            case WorldLens.LandFertility: return "Land fertility (soil, freshwater, climate): brown barren, green rich. The heaviest weight in City Development.";
            case WorldLens.Coherence: return "Coherence: dark low, violet high, white Sacred Sites, red dissonance; leylines drawn. High Coherence raises City Development and its ceiling.";
            case WorldLens.MagicalFertility: return "Magical fertility (Coherence x usable flow this Age): teal low, gold rich; silver rivers and junctions feed it.";
            case WorldLens.Authority: return "Administrative Authority: green yours (brighter nearer an anchor), cyan detached Outposts, amber enclaves, orange independent claims, grey wilderness.";
            case WorldLens.Trade: return "Trade: gold Trade Nexus sites, tan roads, white Trade Nodes. Roads and a connected nexus raise nearby City Development.";
            case WorldLens.Resources: return "Resource Grandfields: each field's colour, densest at its heart. An Outpost on one extracts it (once per field).";
            case WorldLens.Danger: return "Danger (threat spawn potential): clear to deep red. It slows travel and weighs on City Development; Sacred ground stays calm.";
            case WorldLens.Terrain: return "Height: green lowlands, pale highlands; blue water.";
            case WorldLens.Biomes: return "Each cell's biome, blended across procedural seams.";
            case WorldLens.Weather: return "Weather footprints share a colour. Inspect a tile or city for its local conditions; crisis fronts take priority.";
            case WorldLens.Composition: return "Sector slots, handmade interiors and connective terrain.";
            case WorldLens.Beauty: return "Beauty: murky brown hideous, grey plain, rose-gold beautiful. Society adopts and works fair land first; beauty adds a little City Development and eases administration.";
            case WorldLens.Territory: return "Territorial pull: your land tinted by the seat that holds it (brighter where it pulls harder); amber the wilderness it pulls, brightest where society will adopt next; red where a rival pulls harder.";
            default: return "The ground as it is.";
        }
    }

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
                return $"City Development potential {p:0} (ceiling {CityDevelopment.Ceiling(rules, t):0})";
            case WorldLens.LandFertility:
                return $"Land fertility {t.landFertility:P0}: +{rules.landWeight * t.landFertility:0.#} City Development if worked on average";
            case WorldLens.Coherence:
                string lines = t.junction >= 3 ? ", a Leyline Basin" : t.junction == 2 ? ", a Leyline Convergence" : t.leylines != 0 ? ", on a leyline" : string.Empty;
                return $"Coherence {t.coherence:P0}{lines}: +{rules.coherenceWeight * t.coherence:0.#} City Development, ceiling {CityDevelopment.Ceiling(rules, t):0}";
            case WorldLens.MagicalFertility:
                return $"Magical fertility {t.magicFertility:P0}{(t.silver ? ", silver water" : string.Empty)}: +{rules.magicWeight * t.magicFertility:0.#} City Development";
            case WorldLens.Authority:
                return t.authorityId == WorldAuthority.Player ? $"Your Administrative Authority ({t.administrativeAuthority:P0} reach): Developing Towns may be founded here"
                    : t.authorityId == WorldAuthority.Outpost ? "A detached Outpost"
                    : t.authorityId == WorldAuthority.Wilderness ? (t.water || t.impassable ? "Unclaimed" : "Wilderness: Outposts only") : $"Held by {Owner(map, t)}";
            case WorldLens.Trade:
                if (t.nexus != null) return $"Trade Nexus site: {WorldSites.NexusName(t.nexus)} (+{rules.nexusWeight:0} City Development once your roads reach it)";
                if (t.tradeNode) return $"Trade Node on a road (+{rules.roadWeight:0} City Development alongside)";
                return t.road ? $"Road (+{rules.roadWeight:0} City Development alongside)" : "No road";
            case WorldLens.Resources:
                return t.grandfield >= 0 ? $"Grandfield, density {t.grandfieldDensity:P0}: an Outpost here extracts it (+{rules.grandfieldWeight:0} City Development nearby)" : "No grandfield";
            case WorldLens.Danger:
                return t.danger > 0.01f ? $"Danger {t.danger:P0}: -{rules.dangerWeight * t.danger:0.#} City Development, slower travel" : t.sacred ? "Sacred ground: calm" : "Calm";
            case WorldLens.Terrain:
                return $"Height {t.elevation:P0}";
            case WorldLens.Beauty:
                if (t.water) return "Water";
                return $"{Capitalized(WorldBeauty.Word(t.beauty))} ({t.beauty:+0.00;-0.00;0}): {rules.beautyWeight * t.beauty:+0.#;-0.#;0} City Development if worked, {(rules.territory?.beautyWork ?? 0f) * t.beauty:+0%;-0%;0%} to its yields when held";
            case WorldLens.Territory:
                return TerritoryHover(map, t, settings);
            default:
                return null;
        }
    }

    private static string Capitalized(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>The territorial reading of a cell: who pulls it, how hard, and whether society will adopt it (and why not).</summary>
    public static string TerritoryHover(WorldMap map, WorldTile t, WorldGenSettings settings)
    {
        if (t == null || map == null) return null;
        if (t.water || t.impassable) return "No one can live here: no pull holds it";
        var seat = map.territory?.Seat(t.pullSeat);
        string pulled = seat != null ? $"pulled by {seat.name} ({seat.held}/{seat.maxCells} cells held)" : "no seat of yours reaches it";
        if (WorldAuthority.IsPlayers(t.authorityId)) return $"Yours: pull {t.pull:0.00}, {pulled}";
        if (t.authorityId != WorldAuthority.Wilderness) return t.rivalPull > 0f ? $"Held by another authority (its pull {t.rivalPull:0.00})" : "Held by another authority";
        if (seat == null) return t.rivalPull > 0f ? $"Wilderness under a rival's pull ({t.rivalPull:0.00})" : "Wilderness beyond every pull";
        string why = WorldTerritory.WhyNotAdopt(map, settings, t, seat);
        float priority = WorldTerritory.Priority(map, settings, WorldTerritory.RulesOf(map), t, seat);
        return $"Pull {t.pull:0.00}, {pulled}; adoption priority {priority:0.00}. {why ?? "Society will adopt it in its turn."}";
    }

    private static string Owner(WorldMap map, WorldTile t)
    {
        if (t.authorityId.StartsWith("enclave:", StringComparison.Ordinal) && int.TryParse(t.authorityId.Substring(8), out int i) && i >= 0 && i < map.Enclaves.Count)
            return map.Enclaves[i].name;
        return t.authorityId;
    }
}
