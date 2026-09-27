using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// How an expedition of a given size walks the world (<see cref="PartyShapes"/>). The largest shape whose
/// <see cref="size"/> is not above the party's applies: by default Solo (1), Duo (2), Trio (3) and Company (4 and
/// more). The field defaults are neutral (a shape that changes nothing); the proposals live in
/// <see cref="PartyShapes.Defaults"/> and World.asset. Every number is a proposal.
/// </summary>
[Serializable]
public class PartyShape
{
    public string name = "Party";
    [Tooltip("Legends from which this shape applies.")]
    public int size = 1;

    [Header("The road")]
    [Tooltip("Pace multiplier (few legends move light and fast; a company waits for its slowest).")]
    public float pace = 1f;
    [Tooltip("Attrition multiplier (one legend has fewer mouths, blisters and fevers to pass around).")]
    public float wear = 1f;
    [Tooltip("Micro hexes of sight added (more eyes).")]
    public int sight;
    [Tooltip("Surveys, forage and improvements take this much of their time (more hands, quicker work).")]
    public float workTime = 1f;

    [Header("Composure")]
    [Tooltip("The road's hardship on each member is multiplied by this (a companion shares the weight; a crowd adds its own).")]
    public float hardship = 1f;
    [Tooltip("Strain per Seventh on each member outside your settlements even when all goes well: solitude for one, friction for many.")]
    public float roadStrain;

    [Header("Mishaps")]
    [Tooltip("The chance of a mishap is multiplied by this (more people, more that goes wrong).")]
    public float mishapRisk = 1f;
    [Tooltip("Chance per Seventh outside your settlements even when all goes well (accidents of a crowd).")]
    public float baseRisk;
    [Tooltip("The danger part of the risk is multiplied by this (one legend goes unseen; a company is conspicuous).")]
    public float dangerRisk = 1f;
    [Tooltip("A quarrel's weight among the mishaps is multiplied by this (0: no one to quarrel with).")]
    public float quarrelWeight = 1f;
    [Tooltip("Quarrels break out once any member is this strained (a company quarrels while merely Clouded).")]
    public ComposureState quarrelFrom = ComposureState.Fractured;
    [Tooltip("Chance the others handle a mishap (tend the wound, find the way, mediate, talk a deserter down): only ExpeditionSettings.easedShare of its harm lands. Alone, no one helps.")]
    [Range(0f, 1f)] public float resolve;
    [Tooltip("Chance to slip away from an ambush unharmed (and withdraw, when the party can retreat).")]
    [Range(0f, 1f)] public float evade;

    [Header("Moves")]
    [Tooltip("Pace while retreating to safe ground (0: the party is too many to slip away).")]
    public float retreatPace;
    [Tooltip("A legend this alone can go to ground: missing in action, it turns up later at the nearest ground of your territory.")]
    public bool canVanish;
}

/// <summary>What became of a mishap once the party answered it (<see cref="PartyShapes.Respond"/>).</summary>
public enum MishapOutcome
{
    /// <summary>It landed in full.</summary>
    Struck,
    /// <summary>The others handled it: only part of its harm landed.</summary>
    Eased,
    /// <summary>The party slipped away from it (an ambush) and took no wear.</summary>
    Evaded,
}

/// <summary>
/// Party size as a tactical choice, with no scene state (tested in <c>ExpeditionTests</c>). A legend alone walks fast,
/// wears slowly, goes unseen, can retreat from danger and even go to ground, but strains in solitude and has no one to
/// tend its wounds; a duo shares the weight; a trio handles most troubles; a company of four or more is slow, loud and
/// quarrelsome but rarely undone by one mishap and works the land quickest. <see cref="Expeditions"/> reads the shape
/// for the unit, hardship and mishaps; <see cref="WorldSystem"/> for the moves.
/// </summary>
public static class PartyShapes
{
    /// <summary>The shapes used when the settings list none (proposals).</summary>
    public static readonly IReadOnlyList<PartyShape> Defaults = new[]
    {
        new PartyShape
        {
            name = "Solo", size = 1, pace = 1.25f, wear = 0.8f, workTime = 1.25f, hardship = 1.35f, roadStrain = 0.75f,
            mishapRisk = 0.8f, dangerRisk = 0.6f, quarrelWeight = 0f, resolve = 0f, evade = 0.5f, retreatPace = 1.5f, canVanish = true,
        },
        new PartyShape
        {
            name = "Duo", size = 2, pace = 1.1f, wear = 0.9f, workTime = 1.05f, hardship = 0.9f,
            mishapRisk = 0.95f, dangerRisk = 0.8f, quarrelWeight = 0.75f, resolve = 0.3f, evade = 0.2f, retreatPace = 1.25f,
        },
        new PartyShape
        {
            name = "Trio", size = 3, workTime = 0.95f, baseRisk = 0.02f, mishapRisk = 1.1f, resolve = 0.45f,
        },
        new PartyShape
        {
            name = "Company", size = 4, pace = 0.85f, wear = 1.1f, sight = 1, workTime = 0.85f, hardship = 1.1f, roadStrain = 0.25f,
            mishapRisk = 1.25f, baseRisk = 0.04f, dangerRisk = 1.25f, quarrelWeight = 1.5f, quarrelFrom = ComposureState.Clouded, resolve = 0.55f,
        },
    };

    private static readonly PartyShape Neutral = new PartyShape();

    public static IReadOnlyList<PartyShape> All(ExpeditionSettings x) =>
        x?.shapes != null && x.shapes.Any(s => s != null) ? x.shapes.Where(s => s != null).OrderBy(s => s.size).ToList() : Defaults;

    /// <summary>The shape of a party of <paramref name="size"/> legends: the largest listed shape not above it (the smallest for an empty party).</summary>
    public static PartyShape Of(ExpeditionSettings x, int size)
    {
        var all = All(x);
        if (all.Count == 0) return Neutral;
        PartyShape best = all[0];
        foreach (var s in all)
            if (s.size <= Math.Max(1, size)) best = s;
        return best;
    }

    public static PartyShape Of(ExpeditionSettings x, WorldUnit unit) => Of(x, Expeditions.PartySize(unit));

    // ===== ANSWERING A MISHAP =====

    /// <summary>
    /// How the party answers a mishap that struck it (<paramref name="roll"/> in [0, 1)): an ambush may be slipped
    /// (<see cref="PartyShape.evade"/>); otherwise another member able to help (not Spiraling or worse, not the one struck
    /// or the other quarreller) may handle it (<see cref="PartyShape.resolve"/>), the calmest stepping in. A quarrel needs
    /// a third legend to mediate. Sets <see cref="Mishap.outcome"/> and <see cref="Mishap.helper"/>.
    /// </summary>
    public static MishapOutcome Respond(Mishap mishap, IList<PartyMember> party, ExpeditionSettings x, double roll)
    {
        if (mishap == null) return MishapOutcome.Struck;
        mishap.outcome = MishapOutcome.Struck;
        mishap.helper = null;
        if (party == null || party.Count == 0) return mishap.outcome;
        var shape = Of(x, party.Count);
        double r = Math.Max(0.0, Math.Min(0.999999, roll));
        if (mishap.spec != null && mishap.spec.kind == MishapKind.Ambush && shape.evade > 0f)
        {
            if (r < shape.evade) return mishap.outcome = MishapOutcome.Evaded;
            r = (r - shape.evade) / Math.Max(1e-6, 1.0 - shape.evade);
        }
        var helper = Helpers(mishap, party).FirstOrDefault();
        if (helper.name == null || r >= shape.resolve) return mishap.outcome;
        mishap.helper = helper.name;
        return mishap.outcome = MishapOutcome.Eased;
    }

    // Who can step in, calmest first: neither struck nor quarrelling, and not Spiraling or worse.
    private static IEnumerable<PartyMember> Helpers(Mishap mishap, IList<PartyMember> party) =>
        party.Where(m => m.name != null && m.name != mishap.target && m.name != mishap.second && m.state < ComposureState.Spiraling)
            .OrderBy(m => m.strain).ThenBy(m => m.name, StringComparer.Ordinal);

    /// <summary>The share of a mishap's wear (attrition, fatigue, rations) that lands.</summary>
    public static float WearShare(MishapOutcome outcome, ExpeditionSettings x) =>
        outcome == MishapOutcome.Struck ? 1f : outcome == MishapOutcome.Eased ? Clamp01(x.easedShare) : 0f;

    /// <summary>The share of a mishap's strain that lands (a slipped ambush still frightens).</summary>
    public static float StrainShare(MishapOutcome outcome, ExpeditionSettings x) => outcome == MishapOutcome.Struck ? 1f : Clamp01(x.easedShare);

    // ===== RETREAT AND GOING TO GROUND =====

    /// <summary>Ground an expedition can retreat to: held by you, or calm (danger and Dissonance under the settings' thresholds).</summary>
    public static bool Safe(bool held, float danger, float dissonance, ExpeditionSettings x) =>
        held || (danger < x.safeDanger && dissonance < x.safeDissonance);

    /// <summary>Why the party cannot retreat where it stands, or null (the way to safe ground is the caller's).</summary>
    public static string WhyNotRetreat(WorldUnit unit, UnitSurroundings at, ExpeditionSettings x)
    {
        if (unit == null) return "No expedition selected.";
        if (unit.Missing) return "No one knows where it is.";
        var shape = Of(x, unit);
        if (shape.retreatPace <= 0f) return $"A {shape.name.ToLowerInvariant()} is too many to slip away: it can only stand or walk back.";
        if (unit.retreating) return "It is already retreating.";
        if (at.settlement || Safe(at.held, at.danger, at.dissonance, x)) return "Nothing here to retreat from.";
        return null;
    }

    /// <summary>
    /// Why the expedition's legend cannot go to ground (missing in action), or null: only a shape that
    /// <see cref="PartyShape.canVanish"/>, never with settlers in its care or in a settlement, and only once its legend
    /// is <see cref="ExpeditionSettings.vanishFrom"/> or worse (<paramref name="leaderState"/>).
    /// </summary>
    public static string WhyNotVanish(WorldUnit unit, UnitSurroundings at, ComposureState leaderState, ExpeditionSettings x)
    {
        if (unit == null) return "No expedition selected.";
        if (unit.Missing) return "It is missing already.";
        if (!Of(x, unit).canVanish) return "Only a legend walking alone can go to ground: a party holds together.";
        if (unit.settlers > 0) return "It will not abandon the settlers in its care.";
        if (at.settlement) return "It is safe here already.";
        if (leaderState < x.vanishFrom) return $"{unit.leader} is {leaderState}: only a legend {x.vanishFrom} or worse slips away from everything.";
        return null;
    }

    /// <summary>Sevenths a legend stays missing: the settings' base, plus the walk back to your territory made slowly and unseen.</summary>
    public static float MissingSevenths(ExpeditionSettings x, float walkSevenths) =>
        Math.Max(1f, Math.Max(0f, x.missingSevenths) + Math.Max(0f, walkSevenths) * Math.Max(0f, x.missingSlowness));

    /// <summary>
    /// The legend drops out of sight: its road, work and rations are left behind, settlers in its care scatter, and it
    /// will turn up at <paramref name="reappearAt"/> after <paramref name="sevenths"/>.
    /// </summary>
    public static void Vanish(WorldUnit unit, HexCoord reappearAt, float sevenths)
    {
        if (unit == null) return;
        WorldUnits.Stop(unit);
        unit.task = UnitTask.None;
        unit.workLeft = 0f;
        unit.onArrival = UnitTask.None;
        unit.autoExplore = unit.returning = unit.retreating = unit.resting = false;
        unit.hungry = false;
        unit.supplies = 0f;
        unit.settlers = 0;
        unit.missingTo = reappearAt;
        unit.missingSevenths = Math.Max(1f, sevenths);
    }

    /// <summary>A Seventh passes for a missing legend; true when it turns up.</summary>
    public static bool Tick(WorldUnit unit)
    {
        if (unit == null || !unit.Missing) return false;
        unit.missingSevenths = Math.Max(0f, unit.missingSevenths - 1f);
        return unit.missingSevenths <= 1e-4f;
    }

    /// <summary>It turns up at the ground it was making for, worn and weary, with no rations, resting in camp.</summary>
    public static void Reappear(WorldUnit unit, ExpeditionSettings x)
    {
        if (unit == null) return;
        unit.missingSevenths = 0f;
        WorldUnits.Stop(unit);
        WorldUnits.Place(unit, unit.missingTo);
        unit.attrition = Math.Max(0f, Math.Min(99f, x.reappearAttrition));
        unit.fatigue = Math.Max(0f, Math.Min(100f, x.reappearFatigue));
        unit.supplies = 0f;
        unit.warnings = 0;
        WorldUnits.Camp(unit, resting: true);
    }

    // ===== THE CARD =====

    /// <summary>What the party's size does, in a line for the card.</summary>
    public static string Summary(PartyShape s)
    {
        var parts = new List<string>();
        void Rate(float v, string more, string less) { if (Math.Abs(v - 1f) >= 0.005f) parts.Add(v > 1f ? $"{more} {v - 1f:P0}" : $"{less} {1f - v:P0}"); }
        Rate(s.pace, "walks faster by", "walks slower by");
        Rate(s.wear, "wears faster by", "wears slower by");
        Rate(s.hardship, "feels hardship more by", "feels hardship less by");
        if (s.roadStrain > 0f) parts.Add($"+{s.roadStrain:0.##} strain a Seventh on the road ({(s.size <= 1 ? "solitude" : "friction")})");
        Rate(s.mishapRisk, "mishaps likelier by", "mishaps rarer by");
        if (s.resolve > 0f) parts.Add($"{s.resolve:P0} of mishaps handled by the others");
        else parts.Add("no one to help when things go wrong");
        if (s.quarrelWeight <= 0f) parts.Add("no quarrels");
        else if (s.quarrelFrom < ComposureState.Fractured) parts.Add($"quarrels from {s.quarrelFrom}");
        if (s.evade > 0f) parts.Add($"slips {s.evade:P0} of ambushes");
        if (s.retreatPace > 0f) parts.Add("can retreat");
        if (s.canVanish) parts.Add("can go to ground");
        if (s.sight > 0) parts.Add($"+{s.sight} sight");
        Rate(1f / Math.Max(0.05f, s.workTime), "works faster by", "works slower by");
        return string.Join(", ", parts);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
