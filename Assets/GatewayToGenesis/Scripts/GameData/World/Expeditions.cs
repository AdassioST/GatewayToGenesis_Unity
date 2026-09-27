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
/// weather, Dissonance) every member's Composure strains each Seventh, the Director's most.</item>
/// <item>Mishaps (<see cref="MishapRisk"/>, <see cref="RollMishap"/>): the worse things go, and the more strained its
/// legends, the likelier a mishap strikes one of them each Seventh, adding wear and strain in turn.</item>
/// <item>Party size (<see cref="PartyShapes"/>): Solo, Duo, Trio and Company each walk, wear, strain, court mishaps and
/// answer them differently; the shape scales the unit, the hardship and the risk here.</item>
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
    };

    public static IReadOnlyList<MishapSpec> Mishaps(ExpeditionSettings x) =>
        x != null && x.mishaps != null && x.mishaps.Count(m => m != null) > 0 ? x.mishaps.Where(m => m != null).ToList() : DefaultMishaps;

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
    /// </summary>
    public static UnitSpec Effective(UnitSpec perLegend, WorldUnit unit, string directorBinding, ExpeditionSettings x, bool canImprove = false)
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
        if (settlers > 0) s.abilities |= UnitAbility.Settle;
        if (canImprove)
        {
            s.abilities |= UnitAbility.Improve;
            s.workSevenths = perLegend.workSevenths * (float)Math.Pow(Math.Max(0.05f, x.improveHands), party - 1);
        }
        else s.abilities &= ~UnitAbility.Improve;
        switch (MagicBindings.Canonical(directorBinding))
        {
            case "Luminance": s.sight += x.luminanceSight; break;
            case "Cindergale": s.stamina *= x.cindergalePace; break;
            case "Crystal": s.wearMultiplier *= x.crystalWear; break;
            case "Void": s.supplyUsePerSeventh *= x.voidRations; break;
            case "Strand": s.rewardMultiplier *= x.strandRewards; break;
            case "Flux": s.campRecoveryPerSeventh *= x.fluxRest; break;
        }
        return s;
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
    /// Resonance Director keeps the party together.
    /// </summary>
    public static float Hardship(WorldUnit unit, UnitSurroundings at, bool director, string directorBinding, ExpeditionSettings x)
    {
        if (unit == null || unit.Missing) return 0f;
        float h = Math.Max(0f, unit.attrition - x.attritionFrom) / Math.Max(1f, 100f - x.attritionFrom) * Math.Max(0f, x.attritionStrain);
        if (unit.hungry) h += Math.Max(0f, x.hungerStrain);
        if (unit.fatigue >= WorldUnits.StrainFatigue) h += Math.Max(0f, x.exhaustionStrain);
        if (!at.settlement) h += Clamp01(at.dissonance) * Math.Max(0f, x.dissonanceStrain) + at.Exposure * Math.Max(0f, x.exposureStrain);
        // Its size: a companion shares the weight, a crowd adds its own; solitude and friction weigh even on a good road.
        var shape = PartyShapes.Of(x, unit);
        h *= Math.Max(0f, shape.hardship);
        if (!at.settlement) h += Math.Max(0f, shape.roadStrain);
        if (director) h *= Math.Max(0f, x.directorShare);
        if (MagicBindings.Canonical(directorBinding) == "Resonance") h *= Math.Max(0f, x.resonanceHardship);
        return h;
    }

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
            case MishapKind.Whispers: return at.dissonance >= 0.2f;
            case MishapKind.Ambush: return at.danger >= 0.25f;
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

    /// <summary>What a mishap does to the party itself: wear, weariness and lost rations (strain and desertion are the legends', applied by the caller).</summary>
    public static void Afflict(WorldUnit unit, MishapSpec spec, float share = 1f)
    {
        if (unit == null || spec == null) return;
        share = Clamp01(share);
        unit.attrition = Math.Max(0f, Math.Min(100f, unit.attrition + Math.Max(0f, spec.attrition) * share));
        unit.fatigue = Math.Max(0f, Math.Min(100f, unit.fatigue + Math.Max(0f, spec.fatigue) * share));
        unit.supplies = Math.Max(0f, unit.supplies * (1f - Clamp01(spec.rationsLost) * share));
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
            default: return (name, $"{expedition}: {name} at {place}.");
        }
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
