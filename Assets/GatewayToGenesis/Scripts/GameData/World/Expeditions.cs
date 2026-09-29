using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One legend of an expedition as the rules see it.</summary>
public struct PartyMember
{
    public string name;
    public bool director;
    public ComposureState state;
    public float strain;
}

/// <summary>
/// The civilization's Ambition as its expeditions feel it (<see cref="CivilizationProperties.Expedition"/>): multipliers
/// on the road's cost and on the time the party takes to advance. 1 is no change; lower is better.
/// </summary>
public struct ExpeditionAmbition
{
    /// <summary>The cost: the outfit paid, attrition taken (the land, hunger, strain and mishaps) and rations eaten or spoiled.</summary>
    public float cost;
    /// <summary>The time to advance: the party walks at its pace divided by this (0.8 walks 25% faster).</summary>
    public float time;

    public static readonly ExpeditionAmbition None = new ExpeditionAmbition { cost = 1f, time = 1f };
}

/// <summary>A mishap that struck an expedition (<see cref="Expeditions.RollMishap"/>).</summary>
public sealed class Mishap
{
    public MishapSpec spec;
    /// <summary>The legend it strikes (the Director for the party's own mishaps).</summary>
    public string target;
    /// <summary>A quarrel's other legend (null otherwise).</summary>
    public string second;
    /// <summary>How the party answered it (<see cref="PartyShapes.Respond"/>); struck in full until then.</summary>
    public MishapOutcome outcome;
    /// <summary>The legend who handled it when <see cref="outcome"/> is <see cref="MishapOutcome.Eased"/>.</summary>
    public string helper;
}

/// <summary>
/// Expeditions, with no scene state (tested in <c>ExpeditionTests</c>): parties of legends that walk the world in
/// place of generic scouts and settlers. <see cref="WorldSystem"/> keeps them on the map and <see cref="LegendProgress"/>
/// carries what the road does to each legend's Composure.
/// <list type="bullet">
/// <item>Slots: each legend in the field takes one of the civilization's expedition slots (<see cref="Slots"/>): a base,
/// more per point of the Capital's Government Capacity (the vault: Capital improvements expand it) and per Hollow
/// Watchpost.</item>
/// <item>The party: a Director and companions, up to <see cref="ExpeditionSettings.maxParty"/>. The unit's numbers are per
/// legend (<see cref="Effective"/>): more legends carry and eat more; settlers slow it and let it found a settlement;
/// the Director's Soul Leitmotif gives the party one strength, after the vault's class affinities.</item>
/// <item>Hardship (<see cref="Hardship"/>): when things go bad (wear past a threshold, hunger, exhaustion, harsh
/// weather, Dissonance, Vibrational Fallout, a dead zone) every member's Composure strains each Seventh, the Director's most.</item>
/// <item>Solace (<see cref="Solace"/>): moonlit groves, Glimmerfern, silver rivers and the lakes they feed, and fair land
/// ease Composure on top of the road's recovery; strongest on a silver-fed lake's Glimmerfern shore.</item>
/// <item>Weight (<see cref="Burden"/>): rations and cargo against what the party carries at ease; light it walks faster,
/// burdened slower and wearier. Cargo can be dropped (WorldSystem.DropCargo).</item>
/// <item>Mishaps (<see cref="MishapRisk"/>, <see cref="RollMishap"/>): the worse things go, and the more strained its
/// legends, the likelier a mishap strikes one of them each Seventh, adding wear and strain in turn.</item>
/// <item>Party size (<see cref="PartyShapes"/>): Solo, Duo, Trio and Company each walk, wear, strain, court mishaps and
/// answer them differently; the shape scales the unit, the hardship and the risk here.</item>
/// <item>Ambition (<see cref="ExpeditionAmbition"/>): the civilization's Expedition Cost scales the outfit (WorldSystem:
/// forming, companions, settlers' goods and food), the wear the road deals (attrition from the land, hunger, strain and
/// mishaps) and the rations eaten or spoiled; its Completion Time scales how long the party takes to advance.</item>
/// </list>
/// </summary>
public static class Expeditions
{
    // ===== MISHAPS AVAILABLE =====

    /// <summary>The mishaps used when the settings list none (proposals).</summary>
    public static readonly IReadOnlyList<MishapSpec> DefaultMishaps = new[]
    {
        new MishapSpec { kind = MishapKind.Injury, name = "Injury", weight = 3f, strain = 8f, attrition = 4f },
        new MishapSpec { kind = MishapKind.Fever, name = "Fever", weight = 2f, strain = 6f, attrition = 8f },
        new MishapSpec { kind = MishapKind.SpoiledRations, name = "Spoiled rations", weight = 2f, strain = 3f, rationsLost = 0.3f },
        new MishapSpec { kind = MishapKind.LostBearings, name = "Lost bearings", weight = 2f, strain = 4f, fatigue = 20f },
        new MishapSpec { kind = MishapKind.Quarrel, name = "Quarrel", weight = 1.5f, strain = 5f },
        new MishapSpec { kind = MishapKind.Whispers, name = "Whispers of Dissonance", weight = 2f, strain = 12f },
        new MishapSpec { kind = MishapKind.Ambush, name = "Ambush", weight = 2f, strain = 6f, partyStrain = 3f, attrition = 12f },
        new MishapSpec { kind = MishapKind.Desertion, name = "Desertion", weight = 3f, partyStrain = 4f },
        new MishapSpec { kind = MishapKind.Lure, name = "Lured by a bloom", weight = 3f, strain = 10f, partyStrain = 2f, attrition = 10f, fatigue = 15f },
    };

    public static IReadOnlyList<MishapSpec> Mishaps(ExpeditionSettings x) =>
        x != null && x.mishaps != null && x.mishaps.Count(m => m != null) > 0 ? x.mishaps.Where(m => m != null).ToList() : DefaultMishaps;

    // ===== WHAT SURVEYS TURN UP =====

    /// <summary>The survey events used when the settings list none (proposals: no vault source).</summary>
    public static readonly IReadOnlyList<SurveyFindSpec> DefaultSurveyFinds = new[]
    {
        new SurveyFindSpec { kind = SurveyFindKind.Vantage, name = "Old waymarks", text = "follows old waymarks to a high place above {0}: the land around comes out of the fog.", weight = 3f, reveal = 3 },
        new SurveyFindSpec { kind = SurveyFindKind.Spring, name = "A hidden spring", text = "finds a hidden spring at {0}: the party drinks, fills its packs and rests.", weight = 2f, rest = 25f },
        new SurveyFindSpec { kind = SurveyFindKind.Relic, name = "Traces of those before", text = "finds traces of those who walked {0} long ago: every legend of the party remembers them.", weight = 2f, fragments = 2 },
    };

    public static IReadOnlyList<SurveyFindSpec> SurveyFinds(ExpeditionSettings x) =>
        x != null && x.surveyFinds != null && x.surveyFinds.Count(f => f != null && f.weight > 0f) > 0 ? x.surveyFinds.Where(f => f != null && f.weight > 0f).ToList() : DefaultSurveyFinds;

    /// <summary>
    /// What exploring a cell turns up, from two draws in [0, 1): whether an event happens (<paramref name="eventDraw"/>
    /// below the chance, then <paramref name="pickDraw"/> picks it by weight) and whether spare resources are found. A
    /// survey sent to walk every hex (<paramref name="surveyed"/>) is likelier to turn up both than a party passing by.
    /// </summary>
    public static (SurveyFindSpec find, bool cache) RollSurvey(ExpeditionSettings x, bool surveyed, double eventDraw, double pickDraw, double cacheDraw, float odds = 1f)
    {
        if (x == null) return (null, false);
        // Cover multiplies both chances (odds; capped so something can always stay hidden).
        float Raised(float chance) => odds > 1f ? Math.Min(Math.Max(chance, 0.9f), chance * odds) : chance * Math.Max(0f, odds);
        float eventChance = Raised(surveyed ? x.surveyEventChance : x.passingEventChance);
        float cacheChance = Raised(surveyed ? x.surveyCacheChance : x.passingCacheChance);
        SurveyFindSpec find = null;
        if (eventDraw < eventChance)
        {
            var finds = SurveyFinds(x);
            float total = finds.Sum(f => f.weight), at = (float)pickDraw * total;
            foreach (var f in finds)
            {
                find = f;
                if ((at -= f.weight) < 0f) break;
            }
        }
        return (find, cacheDraw < cacheChance);
    }

    /// <summary>
    /// A band of survivors found exploring a cell: its size, or 0. One <paramref name="draw"/> per cell decides both ways
    /// of exploring it, so a cell explored in passing and surveyed later never yields a second band: the survey finds one
    /// only where the draw fell between the passing and the survey chances (<paramref name="exploredBefore"/>).
    /// </summary>
    public static int SurvivorBand(ExpeditionSettings x, bool surveyed, bool exploredBefore, string macroBiome, double draw, double sizeDraw)
    {
        if (x == null) return 0;
        float odds = x.survivorHavens != null && x.survivorHavens.Contains(macroBiome) ? x.havenOdds
                   : x.survivorBarrens != null && x.survivorBarrens.Contains(macroBiome) ? x.barrenOdds : 1f;
        double passing = Math.Min(0.95f, Math.Max(0f, x.passingSurvivorChance * odds));
        double survey = Math.Max(passing, Math.Min(0.95f, Math.Max(0f, x.surveySurvivorChance * odds)));
        bool found = !surveyed ? draw < passing : exploredBefore ? draw >= passing && draw < survey : draw < survey;
        if (!found) return 0;
        int min = Math.Max(1, Math.Min(x.survivorBandMin, x.survivorBandMax)), max = Math.Max(min, Math.Max(x.survivorBandMin, x.survivorBandMax));
        return min + Math.Min(max - min, (int)(Math.Max(0d, sizeDraw) * (max - min + 1)));
    }

    /// <summary>Sevenths a band found <paramref name="cells"/> cells away takes to reach the Capital (at least one).</summary>
    public static int SurvivorTravel(ExpeditionSettings x, int cells) =>
        Math.Max(1, (int)Math.Ceiling(Math.Max(0, cells) * Math.Max(0f, x != null ? x.survivorSeventhsPerCell : 1f)));

    /// <summary>
    /// Spare resources found on a cell: its forage times <see cref="ExpeditionSettings.cacheForage"/>, or where nothing
    /// can be foraged its ground's yields for <see cref="ExpeditionSettings.cacheYieldSeconds"/>; times <paramref name="multiplier"/>.
    /// </summary>
    public static List<ResourceAmount> Cache(ExpeditionSettings x, WorldGenSettings settings, WorldTile t, float multiplier)
    {
        var found = WorldUnits.ForageOf(settings, t, x.cacheForage * Math.Max(0f, multiplier));
        if (found.Count > 0) return found;
        var terrain = t != null ? settings.Terrain(t.terrain) : null;
        return (terrain?.yields ?? new List<ResourceAmount>()).Where(y => y != null && !string.IsNullOrEmpty(y.resource) && y.amount > 0f)
            .Select(y => new ResourceAmount { resource = y.resource, amount = y.amount * x.cacheYieldSeconds * Math.Max(0f, multiplier) }).ToList();
    }

    // ===== SLOTS AND THE PARTY =====

    /// <summary>The civilization's expedition slots: the base, per point of Government Capacity, per slot building.</summary>
    public static int Slots(ExpeditionSettings x, int governmentCapacity, int slotBuildings) =>
        Math.Max(0, x.baseSlots + x.slotsPerCapacity * Math.Max(0, governmentCapacity) + x.slotsPerBuilding * Math.Max(0, slotBuildings));

    /// <summary>The legends of an expedition, its Director first.</summary>
    public static IEnumerable<string> Members(WorldUnit unit)
    {
        if (unit == null) yield break;
        if (!string.IsNullOrEmpty(unit.leader)) yield return unit.leader;
        if (unit.companions == null) yield break;
        foreach (var name in unit.companions)
            if (!string.IsNullOrEmpty(name)) yield return name;
    }

    public static int PartySize(WorldUnit unit) => Members(unit).Count();

    public static bool IsMember(WorldUnit unit, string legend) =>
        !string.IsNullOrEmpty(legend) && Members(unit).Any(n => string.Equals(n, legend, StringComparison.OrdinalIgnoreCase));

    /// <summary>Slots taken by the legends of these expeditions.</summary>
    public static int SlotsUsed(IEnumerable<WorldUnit> expeditions) => expeditions?.Sum(PartySize) ?? 0;

    /// <summary>Why one more legend cannot join a party of <paramref name="partySize"/> with <paramref name="freeSlots"/> slots free, or null.</summary>
    public static string WhyNotAddLegend(int partySize, int freeSlots, ExpeditionSettings x)
    {
        if (partySize >= Math.Max(1, x.maxParty)) return $"A party holds at most {Math.Max(1, x.maxParty)} legends.";
        if (freeSlots <= 0) return "Every expedition slot is taken: raise Government Capacity or build a Hollow Watchpost.";
        return null;
    }

    /// <summary>
    /// Who leads when the Director goes: the first companion. Removes <paramref name="legend"/> from the party; true
    /// when it was a member.
    /// </summary>
    public static bool Remove(WorldUnit unit, string legend)
    {
        if (unit == null || string.IsNullOrEmpty(legend)) return false;
        if (string.Equals(unit.leader, legend, StringComparison.OrdinalIgnoreCase))
        {
            var next = unit.companions?.FirstOrDefault(n => !string.IsNullOrEmpty(n));
            unit.leader = next;
            if (next != null) unit.companions.Remove(next);
            return true;
        }
        return unit.companions != null && unit.companions.RemoveAll(n => string.Equals(n, legend, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    /// <summary>Make <paramref name="legend"/> (a companion) the Director; the Director steps back among the companions.</summary>
    public static bool MakeDirector(WorldUnit unit, string legend)
    {
        if (unit?.companions == null || !unit.companions.Any(n => string.Equals(n, legend, StringComparison.OrdinalIgnoreCase))) return false;
        unit.companions.RemoveAll(n => string.Equals(n, legend, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(unit.leader)) unit.companions.Insert(0, unit.leader);
        unit.leader = legend;
        return true;
    }

    /// <summary>
    /// The unit an expedition walks as: <paramref name="perLegend"/>'s numbers for its party. Every legend carries and eats
    /// its own rations and forages its share; settlers eat a share each, slow the party and let it found a settlement;
    /// companions bring back more; the Director's Soul Leitmotif adds one strength (<see cref="Strength"/>). Once the
    /// civilization knows how (<paramref name="canImprove"/>), the legends improve hotspots themselves, quicker with more hands.
    /// The civilization's <paramref name="ambition"/> lightens the wear and the rations and quickens the march.
    /// </summary>
    public static UnitSpec Effective(UnitSpec perLegend, WorldUnit unit, string directorBinding, ExpeditionSettings x, bool canImprove = false,
        ExpeditionAmbition? ambition = null)
    {
        if (perLegend == null) return null;
        var s = perLegend.Clone();
        int party = Math.Max(1, PartySize(unit));
        int settlers = unit != null ? Math.Max(0, unit.settlers) : 0;
        float people = party + settlers * Math.Max(0f, x.settlerWeight);
        s.supplyCapacity = perLegend.supplyCapacity * people;
        s.supplyUsePerSeventh = perLegend.supplyUsePerSeventh * people;
        s.forageRationsPerSeventh = perLegend.forageRationsPerSeventh * people;
        s.stamina = perLegend.stamina * (settlers > 0 ? Math.Max(0.05f, x.settlerPace) : 1f);
        // Its weight: light it walks a little faster, burdened slower and wearier (in steps, so the spec is cached).
        float burden = unit != null ? BurdenStep(unit, x) : 1f;
        s.stamina *= LoadPace(x, burden);
        s.fatiguePerTravelCost *= LoadFatigue(x, burden);
        s.rewardMultiplier = perLegend.rewardMultiplier * (1f + Math.Max(0f, x.companionRewards) * (party - 1));
        // Its size: how fast it walks, wears, sees and works (a retreat is faster still, for those small enough).
        var shape = PartyShapes.Of(x, party);
        s.stamina *= Math.Max(0.05f, shape.pace);
        if (unit != null && unit.retreating && shape.retreatPace > 0f) s.stamina *= shape.retreatPace;
        s.wearMultiplier *= Math.Max(0f, shape.wear);
        s.sight += shape.sight;
        // Surveys and forage (improvements already scale with the hands, improveHands).
        float work = Math.Max(0.05f, shape.workTime);
        s.surveySevenths *= work;
        s.forageSevenths *= work;
        s.mesoSurveySevenths *= work;
        s.harvestSevenths *= work;
        s.plantSevenths *= work;
        if (settlers > 0) s.abilities |= UnitAbility.Settle;
        if (canImprove)
        {
            s.abilities |= UnitAbility.Improve;
            s.workSevenths = perLegend.workSevenths * (float)Math.Pow(Math.Max(0.05f, x.improveHands), party - 1);
        }
        else s.abilities &= ~UnitAbility.Improve;
        // Its charter: builders only work the land (faster, wearing less); a cultural party only celebrates.
        switch (unit != null ? unit.charter : ExpeditionCharter.Expedition)
        {
            case ExpeditionCharter.Builders:
                s.abilities &= UnitAbility.Improve | UnitAbility.Plant | UnitAbility.Forage;
                s.workSevenths *= Math.Max(0.05f, x.builderWork);
                s.plantSevenths *= Math.Max(0.05f, x.builderWork);
                s.forageSevenths *= Math.Max(0.05f, x.builderWork);
                s.wearMultiplier *= Math.Max(0f, x.builderWear);
                break;
            case ExpeditionCharter.CulturalParty:
                s.abilities = UnitAbility.Celebrate;
                s.wearMultiplier *= Math.Max(0f, x.partyWear);
                break;
        }
        switch (MagicBindings.Canonical(directorBinding))
        {
            case "Luminance": s.sight += x.luminanceSight; break;
            case "Cindergale": s.stamina *= x.cindergalePace; break;
            case "Crystal": s.wearMultiplier *= x.crystalWear; break;
            case "Void": s.supplyUsePerSeventh *= x.voidRations; break;
            case "Strand": s.rewardMultiplier *= x.strandRewards; break;
            case "Flux": s.campRecoveryPerSeventh *= x.fluxRest; break;
        }
        // Ambition: the road costs less (wear and rations) and the party advances sooner.
        var a = ambition ?? ExpeditionAmbition.None;
        s.wearMultiplier *= Math.Max(0f, a.cost);
        s.supplyUsePerSeventh *= Math.Max(0f, a.cost);
        s.stamina /= Math.Max(0.05f, a.time);
        return s;
    }

    // ===== WEIGHT =====

    /// <summary>Burden is read in steps of this (the party's unit is cached per step).</summary>
    public const float BurdenSteps = 20f;

    /// <summary>The weight of one unit of <paramref name="resource"/> as cargo (<see cref="ExpeditionSettings.cargoWeights"/>; 1 when not listed).</summary>
    public static float WeightOf(ExpeditionSettings x, string resource)
    {
        var entry = x?.cargoWeights?.Find(w => w != null && string.Equals(w.resource, resource, StringComparison.OrdinalIgnoreCase));
        return entry != null ? Math.Max(0f, entry.amount) : 1f;
    }

    /// <summary>The weight of the cargo an expedition carries (seeds weigh nothing).</summary>
    public static float CargoWeight(WorldUnit unit, ExpeditionSettings x) =>
        unit?.cargo == null ? 0f : unit.cargo.Where(a => a != null && a.amount > 0f).Sum(a => a.amount * WeightOf(x, a.resource));

    /// <summary>Everything it carries: rations and cargo.</summary>
    public static float Load(WorldUnit unit, ExpeditionSettings x) =>
        unit == null || x == null ? 0f : Math.Max(0f, unit.supplies) * Math.Max(0f, x.rationWeight) + CargoWeight(unit, x);

    /// <summary>What the party carries at ease: <see cref="ExpeditionSettings.loadPerLegend"/> per legend, settlers carrying their own share.</summary>
    public static float LoadAtEase(WorldUnit unit, ExpeditionSettings x)
    {
        if (x == null) return 0f;
        int settlers = unit != null ? Math.Max(0, unit.settlers) : 0;
        return Math.Max(0f, x.loadPerLegend) * (Math.Max(1, PartySize(unit)) + settlers * Math.Max(0f, x.settlerWeight));
    }

    /// <summary>Its load over what it carries at ease (0 nothing, 1 at ease, above 1 burdened).</summary>
    public static float Burden(WorldUnit unit, ExpeditionSettings x)
    {
        float ease = LoadAtEase(unit, x);
        return ease > 0f ? Load(unit, x) / ease : 0f;
    }

    /// <summary><see cref="Burden"/> rounded to its step (what the party's unit is built from).</summary>
    public static float BurdenStep(WorldUnit unit, ExpeditionSettings x) => (float)Math.Round(Burden(unit, x) * BurdenSteps) / BurdenSteps;

    /// <summary>The pace a burden allows: faster travelling light, 1 up to the load at ease, slower beyond it (never below the floor).</summary>
    public static float LoadPace(ExpeditionSettings x, float burden)
    {
        if (x == null) return 1f;
        burden = Math.Max(0f, burden);
        if (x.lightBelow > 0f && burden < x.lightBelow) return 1f + (x.lightPace - 1f) * (1f - burden / x.lightBelow);
        if (burden <= 1f) return 1f;
        return Math.Max(Math.Max(0.05f, x.minLoadPace), 1f - Math.Max(0f, x.overloadSlowdown) * (burden - 1f));
    }

    /// <summary>Travel fatigue multiplier for a burden (1 up to the load at ease).</summary>
    public static float LoadFatigue(ExpeditionSettings x, float burden) => x == null ? 1f : 1f + Math.Max(0f, x.overloadFatigue) * Math.Max(0f, burden - 1f);

    /// <summary>"light: walks 8% faster", "burdened: walks 30% slower" or null at ease, for the card.</summary>
    public static string LoadSummary(ExpeditionSettings x, float burden)
    {
        float pace = LoadPace(x, burden);
        if (pace > 1.0005f) return $"travelling light: walks {pace - 1f:P0} faster";
        if (pace < 0.9995f) return $"burdened: walks {1f - pace:P0} slower and tires {LoadFatigue(x, burden) - 1f:P0} sooner";
        return null;
    }

    // ===== SOLACE =====

    /// <summary>
    /// Composure strain the place eases per Seventh for every legend of a party, on top of its recovery
    /// (<see cref="ComposureContext.solace"/>): what grows and lies there (a moonlit grove, Glimmerfern's light), a silver
    /// river near, the shore of a lake one runs into, and fair land. Vibrational Fallout drowns it out. The strongest
    /// place is Glimmerfern on a silver-fed lake's shore.
    /// </summary>
    public static float Solace(UnitSurroundings at, ExpeditionSettings x)
    {
        if (x == null) return 0f;
        float s = Math.Max(0f, at.solace);
        if (at.mapped)
        {
            s += Near(at.silverRiverSteps, x.silverRiverReach) * Math.Max(0f, x.silverRiverSolace);
            s += Near(at.silverLakeSteps, x.silverLakeReach) * Math.Max(0f, x.silverLakeSolace);
        }
        s += Math.Max(0f, at.beauty) * Math.Max(0f, x.beautySolace);
        s *= 1f - Clamp01(at.fallout);
        return Math.Max(0f, Math.Min(Math.Max(0f, x.maxSolace), s));
    }

    // 1 on it, fading to 0 past the reach.
    private static float Near(int steps, int reach) => steps < 0 || steps > reach ? 0f : 1f - steps / (float)(Math.Max(0, reach) + 1);

    /// <summary>What Ambition does for expeditions, in a line for the card (null when it does nothing).</summary>
    public static string AmbitionSummary(ExpeditionAmbition ambition)
    {
        // Ambition only ever lightens (StatManager caps it like the Saving Roll): 1 or more says nothing.
        var parts = new List<string>();
        if (ambition.cost < 0.9995f) parts.Add($"{(1f - ambition.cost) * 100f:0.#}% cheaper outfit, less wear and rations");
        if (ambition.time < 0.9995f && ambition.time > 0f) parts.Add($"walks {(1f / ambition.time - 1f) * 100f:0.#}% faster");
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    /// <summary>What a Director of <paramref name="binding"/> gives the party, in a line for the card.</summary>
    public static string Strength(string binding, ExpeditionSettings x)
    {
        switch (MagicBindings.Canonical(binding))
        {
            case "Luminance": return $"sees {x.luminanceSight} hexes farther";
            case "Cindergale": return $"walks {x.cindergalePace - 1f:P0} faster";
            case "Crystal": return $"wears {1f - x.crystalWear:P0} slower";
            case "Void": return $"eats {1f - x.voidRations:P0} less";
            case "Strand": return $"brings back {x.strandRewards - 1f:P0} more";
            case "Flux": return $"rests {x.fluxRest - 1f:P0} faster in camp";
            case "Resonance": return $"bears {1f - x.resonanceHardship:P0} less hardship";
            default: return null;
        }
    }

    // ===== HARDSHIP =====

    /// <summary>
    /// Composure strain per Seventh the road puts on one member: wear past <see cref="ExpeditionSettings.attritionFrom"/>,
    /// hunger, marching exhausted, and outside settlements Dissonance and harsh weather. The Director carries more; a
    /// Resonance Director keeps the party together; a healing Eleos Bloom eases what is left.
    /// </summary>
    public static float Hardship(WorldUnit unit, UnitSurroundings at, bool director, string directorBinding, ExpeditionSettings x)
    {
        if (unit == null || unit.Missing) return 0f;
        float h = Math.Max(0f, unit.attrition - x.attritionFrom) / Math.Max(1f, 100f - x.attritionFrom) * Math.Max(0f, x.attritionStrain);
        if (unit.hungry) h += Math.Max(0f, x.hungerStrain);
        if (unit.fatigue >= WorldUnits.StrainFatigue) h += Math.Max(0f, x.exhaustionStrain);
        if (!at.settlement)
        {
            h += Clamp01(at.dissonance) * Math.Max(0f, x.dissonanceStrain) + at.Exposure * Math.Max(0f, x.exposureStrain);
            // Vibrational Fallout broadcasts back into every Soul Leitmotif; a dead zone starves the attuned.
            h += Clamp01(at.fallout) * Math.Max(0f, x.falloutStrain) + Thin(at, x) * Math.Max(0f, x.thinStrain);
        }
        // Its size: a companion shares the weight, a crowd adds its own; solitude and friction weigh even on a good road.
        var shape = PartyShapes.Of(x, unit);
        h *= Math.Max(0f, shape.hardship);
        if (!at.settlement) h += Math.Max(0f, shape.roadStrain);
        if (director) h *= Math.Max(0f, x.directorShare);
        if (MagicBindings.Canonical(directorBinding) == "Resonance") h *= Math.Max(0f, x.resonanceHardship);
        // A healing Eleos Bloom nearby takes some of the weight in its light.
        return Math.Max(0f, h - Math.Max(0f, at.sanctuary));
    }

    /// <summary>How deep into a dead zone the place is, 0-1 (density under <see cref="ExpeditionSettings.thinDensityBelow"/>).</summary>
    public static float Thin(UnitSurroundings at, ExpeditionSettings x) =>
        !at.densityKnown || x == null || x.thinDensityBelow <= 0f || at.density >= x.thinDensityBelow ? 0f : 1f - Clamp01(at.density) / x.thinDensityBelow;

    // ===== MISHAPS =====

    /// <summary>
    /// The chance per Seventh that a mishap strikes: nothing when all goes well; the road's troubles (wear from 25,
    /// hunger, exhaustion, danger, Dissonance, harsh weather) count outside settlements, strained legends anywhere.
    /// </summary>
    public static float MishapRisk(WorldUnit unit, UnitSurroundings at, IEnumerable<PartyMember> party, ExpeditionSettings x)
    {
        if (unit == null || unit.Missing) return 0f;
        float risk = 0f;
        var shape = PartyShapes.Of(x, party?.Count() ?? PartySize(unit));
        if (!at.settlement)
        {
            risk += Math.Max(0f, unit.attrition - 25f) / 75f * x.attritionRisk;
            if (unit.hungry) risk += x.hungerRisk;
            if (unit.fatigue >= WorldUnits.StrainFatigue) risk += x.exhaustionRisk;
            // A lone legend goes unseen; a company is heard coming.
            risk += Clamp01(at.danger) * x.dangerRisk * Math.Max(0f, shape.dangerRisk) + Clamp01(at.dissonance) * x.dissonanceRisk + at.Exposure * x.exposureRisk;
            // A predatory bloom finds the lonely as easily as a crowd.
            risk += Clamp01(at.lure) * Math.Max(0f, x.lureRisk);
            // White Noise in every thread, and wild magic leaping along its edge.
            risk += Clamp01(at.fallout) * Math.Max(0f, x.falloutRisk) + Clamp01(at.cascade) * Math.Max(0f, x.cascadeRisk);
            risk += Math.Max(0f, shape.baseRisk);
        }
        foreach (var m in party ?? Enumerable.Empty<PartyMember>())
            risk += m.state >= ComposureState.Spiraling ? x.spiralingRisk : m.state == ComposureState.Fractured ? x.fracturedRisk : 0f;
        risk *= Math.Max(0f, shape.mishapRisk);
        return Math.Max(0f, Math.Min(Math.Max(0f, x.maxRisk), risk));
    }

    /// <summary>
    /// Whether a mishap of <paramref name="kind"/> can strike the party where it stands (in a settlement only quarrels and
    /// desertion). Quarrels need two legends and one strained to the party shape's <see cref="PartyShape.quarrelFrom"/>.
    /// </summary>
    public static bool CanHappen(MishapKind kind, WorldUnit unit, UnitSurroundings at, IList<PartyMember> party, ExpeditionSettings x = null)
    {
        if (unit == null || party == null || party.Count == 0) return false;
        switch (kind)
        {
            case MishapKind.Quarrel:
                var shape = x != null ? PartyShapes.Of(x, party.Count) : null;
                var from = shape != null ? shape.quarrelFrom : ComposureState.Fractured;
                return party.Count >= 2 && (shape == null || shape.quarrelWeight > 0f) && party.Any(m => m.state >= from);
            case MishapKind.Desertion: return party.Any(m => !m.director && m.state >= ComposureState.Spiraling);
        }
        if (at.settlement) return false;
        switch (kind)
        {
            case MishapKind.Injury: return true;
            case MishapKind.Fever: return unit.hungry || at.Exposure >= 0.15f || unit.attrition >= 40f;
            case MishapKind.SpoiledRations: return unit.supplies > 0.5f;
            case MishapKind.LostBearings: return unit.Moving || at.Exposure >= 0.15f;
            case MishapKind.Whispers: return at.dissonance >= 0.2f || at.fallout >= 0.1f || at.cascade >= 0.2f;
            case MishapKind.Ambush: return at.danger >= 0.25f;
            case MishapKind.Lure: return at.lure >= 0.2f;
            default: return false;
        }
    }

    /// <summary>
    /// A Seventh's roll: whether a mishap strikes (<paramref name="chance"/> under <see cref="MishapRisk"/>), which one
    /// (<paramref name="pick"/>, by weight among those that can happen here) and whom (<paramref name="aim"/>): injuries,
    /// fevers and whispers find the most strained likeliest, an ambush anyone, the party's own mishaps the Director, a
    /// quarrel the most strained and one other, a desertion the most strained Spiraling companion. The three numbers are
    /// in [0, 1) (WorldSystem draws them from the world's seed, so the same fortune replays). Null when nothing strikes.
    /// A quarrel's second legend is the next most strained.
    /// </summary>
    public static Mishap RollMishap(WorldUnit unit, UnitSurroundings at, IList<PartyMember> party, ExpeditionSettings x, double chance, double pick, double aim)
    {
        if (party == null || party.Count == 0 || chance >= MishapRisk(unit, at, party, x)) return null;
        var possible = Mishaps(x).Where(m => m.weight > 0f && CanHappen(m.kind, unit, at, party, x)).ToList();
        if (possible.Count == 0) return null;
        // The bigger the party, the likelier its trouble is a quarrel.
        float quarrels = Math.Max(0f, PartyShapes.Of(x, party.Count).quarrelWeight);
        float Weight(MishapSpec m) => m.kind == MishapKind.Quarrel ? m.weight * quarrels : m.weight;
        possible = possible.Where(m => Weight(m) > 0f).ToList();
        if (possible.Count == 0) return null;
        double roll = pick * possible.Sum(Weight);
        var spec = possible[possible.Count - 1];
        foreach (var m in possible)
        {
            if (roll < Weight(m)) { spec = m; break; }
            roll -= Weight(m);
        }
        var mishap = new Mishap { spec = spec };
        var director = party.FirstOrDefault(m => m.director);
        switch (spec.kind)
        {
            case MishapKind.SpoiledRations:
            case MishapKind.LostBearings:
                mishap.target = director.name ?? party[0].name;
                break;
            case MishapKind.Ambush:
                mishap.target = party[Math.Min(party.Count - 1, (int)(aim * party.Count))].name;
                break;
            case MishapKind.Quarrel:
                // The one it strikes turns on the next most strained.
                mishap.target = ByStrain(party, aim).name;
                mishap.second = party.Where(m => m.name != mishap.target).OrderByDescending(m => m.strain).ThenBy(m => m.name, StringComparer.Ordinal).First().name;
                break;
            case MishapKind.Desertion:
                mishap.target = party.Where(m => !m.director && m.state >= ComposureState.Spiraling).OrderByDescending(m => m.strain).ThenBy(m => m.name, StringComparer.Ordinal).First().name;
                break;
            default:
                mishap.target = ByStrain(party, aim).name;
                break;
        }
        return mishap;
    }

    // The more strained a legend, the likelier it is struck (strain + 10 each, so the calm are never spared entirely).
    private static PartyMember ByStrain(IList<PartyMember> party, double aim)
    {
        double total = party.Sum(m => Math.Max(0f, m.strain) + 10f);
        double roll = aim * total;
        foreach (var m in party)
        {
            double w = Math.Max(0f, m.strain) + 10f;
            if (roll < w) return m;
            roll -= w;
        }
        return party[party.Count - 1];
    }

    /// <summary>
    /// What a mishap does to the party itself: wear, weariness and lost rations (strain and desertion are the legends',
    /// applied by the caller). The wear and the lost rations are the road's cost, times <paramref name="cost"/> (Ambition).
    /// </summary>
    public static void Afflict(WorldUnit unit, MishapSpec spec, float share = 1f, float cost = 1f)
    {
        if (unit == null || spec == null) return;
        share = Clamp01(share);
        cost = Math.Max(0f, cost);
        unit.attrition = Math.Max(0f, Math.Min(100f, unit.attrition + Math.Max(0f, spec.attrition) * share * cost));
        unit.fatigue = Math.Max(0f, Math.Min(100f, unit.fatigue + Math.Max(0f, spec.fatigue) * share));
        unit.supplies = Math.Max(0f, unit.supplies * (1f - Clamp01(Clamp01(spec.rationsLost) * share * cost)));
        unit.mishaps++;
    }

    /// <summary>The notice for a mishap: its title and one line.</summary>
    public static (string title, string text) Describe(Mishap mishap, string expedition, string place)
    {
        var (title, text) = Struck(mishap, expedition, place);
        switch (mishap.outcome)
        {
            case MishapOutcome.Evaded:
                return ($"Ambush slipped: {expedition}", $"{expedition} is set upon near {place}, but {mishap.target} slips away before the trap closes.");
            case MishapOutcome.Eased when mishap.spec.kind == MishapKind.Desertion:
                return ($"Desertion averted: {mishap.target}", $"{mishap.target} means to abandon {expedition} at {place}; {mishap.helper} talks them into staying.");
            case MishapOutcome.Eased:
                return (title, $"{text} {mishap.helper} {Handles(mishap.spec.kind, mishap.target)}, and the worst of it passes.");
            default:
                return (title, text);
        }
    }

    // What the helper does about it, for the notice.
    private static string Handles(MishapKind kind, string target)
    {
        switch (kind)
        {
            case MishapKind.Injury: return $"binds {target}'s wound";
            case MishapKind.Fever: return $"nurses {target} through the night";
            case MishapKind.SpoiledRations: return "saves what can be saved";
            case MishapKind.LostBearings: return "finds the way again";
            case MishapKind.Quarrel: return "steps between them";
            case MishapKind.Whispers: return $"keeps {target} talking until the whispers fade";
            case MishapKind.Ambush: return "rallies the others";
            case MishapKind.Desertion: return $"talks {target} into staying";
            case MishapKind.Lure: return $"pulls {target} free of the bloom";
            default: return "steps in";
        }
    }

    private static (string title, string text) Struck(Mishap mishap, string expedition, string place)
    {
        var spec = mishap.spec;
        string name = spec.name ?? spec.kind.ToString();
        switch (spec.kind)
        {
            case MishapKind.Injury: return ($"{name}: {mishap.target}", $"{mishap.target} is hurt on the road near {place}. {expedition} slows.");
            case MishapKind.Fever: return ($"{name}: {mishap.target}", $"{mishap.target} falls sick at {place}; the sickness spreads through {expedition}.");
            case MishapKind.SpoiledRations: return ($"{name} in {expedition}", $"Part of {expedition}'s rations spoil at {place}; {mishap.target} answers for it.");
            case MishapKind.LostBearings: return ($"{name}: {expedition}", $"{mishap.target} loses the way near {place}; {expedition} wears itself out finding it again.");
            case MishapKind.Quarrel: return ($"{name} in {expedition}", $"{mishap.target} and {mishap.second} quarrel at {place}.");
            case MishapKind.Whispers: return ($"{name}: {mishap.target}", $"Dissonance gnaws at {mishap.target} at {place}.");
            case MishapKind.Ambush: return ($"{name}: {expedition}", $"{expedition} is attacked near {place}; {mishap.target} takes the worst of it.");
            case MishapKind.Desertion: return ($"{name}: {mishap.target}", $"{mishap.target} abandons {expedition} at {place} and makes for home.");
            case MishapKind.Lure: return ($"{name}: {mishap.target}", $"Something near {place} calls to {mishap.target} in a voice they know, and {mishap.target} follows it into an Eleos Bloom's grasp. {expedition} loses a day tearing them free.");
            default: return (name, $"{expedition}: {name} at {place}.");
        }
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
