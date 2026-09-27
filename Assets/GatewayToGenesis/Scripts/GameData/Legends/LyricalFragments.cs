using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The seven Lyrical Fragments (vault: Stellar Legacy Score.md, "The Narrative Engine of The Eternal Symphony"), each
/// tied to a binding. The vault: "Every significant action during Events generates a Lyrical Fragment tagged with
/// their respective Binding/Emotion."
/// </summary>
public enum FragmentKind
{
    /// <summary>Resonance: "Earned through acts of self-discovery, purpose and unity."</summary>
    Meaning,
    /// <summary>Luminance: "Earned through pursuit of truth and clarity."</summary>
    Lucidity,
    /// <summary>Flux: "Earned through high states of emotional release."</summary>
    Catharsis,
    /// <summary>Void: "Earned through sacrifice and letting go."</summary>
    Acceptance,
    /// <summary>Cindergale: "Earned through resistance and overcoming."</summary>
    Defiance,
    /// <summary>Crystal: "Earned through will, determination, and creation."</summary>
    Vision,
    /// <summary>Strand: "Earned through transformation."</summary>
    Rebirth,
}

/// <summary>
/// The Role Archetypes of the Fate Stage (vault: Fate Stage.md, "Ballad Resolution, Magnum Opuss, and Legend
/// Titles"): what a role's theme pays in fragments, a primary kind ×5 and a secondary kind ×3. Ballads borrow the table
/// for their themes; the Fate Stage itself (super ballads) is not built yet.
/// </summary>
public enum RoleArchetype { None, Leader, Resistor, Scholar, EmotionalCore, SacrificialLamb }

/// <summary>An amount of one kind of fragment (a deed's reward, a ballad's theme).</summary>
[Serializable]
public class FragmentAward
{
    public FragmentKind kind;
    public int amount;

    public FragmentAward() { }
    public FragmentAward(FragmentKind kind, int amount) { this.kind = kind; this.amount = amount; }

    public override string ToString() => $"{amount} {LyricalFragments.Name(kind, amount)}";
}

/// <summary>
/// The numbers of Lyrical Fragments: what each deed pays, how fragments grow a legend, the Underdog, and the Fate
/// Stage's rewards. Proposals (roadmap D08) except where a tooltip quotes the vault; tune in
/// Resources/Legends/LegendSettings.asset.
/// </summary>
[Serializable]
public class FragmentTuning
{
    [Header("Growth")]
    [Tooltip("Fragments of a kind that add one point to its binding (a legend who earns Vision grows in Crystal).")]
    public int fragmentsPerBindingPoint = 10;

    [Header("The Underdog (vault: Legend Trait.md)")]
    [Tooltip("The Underdog meter (the Underdog Points of the legend's origin traits) from which a legend is an Underdog. The vault names \"a certain threshold\" without a number.")]
    public int underdogThreshold = 75;
    [Tooltip("The vault: \"You already gain ×2 Lyrical Fragments when you 'punch above your station.'\"")]
    public float underdogMultiplier = 2f;

    [Header("The council and the Ages")]
    public List<FragmentAward> actOfFate = new List<FragmentAward> { new FragmentAward(FragmentKind.Meaning, 3) };
    [Tooltip("Each seated legend, when an Age Crisis is survived (resistance and overcoming; the Age's passage transforms).")]
    public List<FragmentAward> crisisCarried = new List<FragmentAward> { new FragmentAward(FragmentKind.Defiance, 6), new FragmentAward(FragmentKind.Rebirth, 4) };

    [Header("The soul (vault: \"All Motif Awakenings generate Fragment of Meaning and Fragment of Defiance\")")]
    public List<FragmentAward> motifAwakening = new List<FragmentAward> { new FragmentAward(FragmentKind.Meaning, 4), new FragmentAward(FragmentKind.Defiance, 4) };
    [Tooltip("The Catalytic Abyss of Emotion: a high state of emotional release.")]
    public List<FragmentAward> catalyticAbyss = new List<FragmentAward> { new FragmentAward(FragmentKind.Catharsis, 8) };

    [Header("Expeditions (each member)")]
    [Tooltip("A long road walked to its end.")]
    public List<FragmentAward> journey = new List<FragmentAward> { new FragmentAward(FragmentKind.Defiance, 2) };
    [Tooltip("The kind a discovery pays (its feature's own amount): the pursuit of truth.")]
    public FragmentKind discovery = FragmentKind.Lucidity;
    public List<FragmentAward> founding = new List<FragmentAward> { new FragmentAward(FragmentKind.Vision, 3) };
    [Tooltip("Each improvement of a hotspot (the legends' own work: there are no builders).")]
    public List<FragmentAward> improvement = new List<FragmentAward> { new FragmentAward(FragmentKind.Vision, 1) };
    [Tooltip("An improvement that reaches the last level (a masterwork), on top of the improvement.")]
    public List<FragmentAward> masterwork = new List<FragmentAward> { new FragmentAward(FragmentKind.Vision, 3) };
    [Tooltip("The legend a mishap struck, if it is still standing.")]
    public List<FragmentAward> mishapEndured = new List<FragmentAward> { new FragmentAward(FragmentKind.Defiance, 1) };
    [Tooltip("The companions of a legend lost to Dissonance on the road: letting go.")]
    public List<FragmentAward> companionLost = new List<FragmentAward> { new FragmentAward(FragmentKind.Acceptance, 3) };
    [Tooltip("Every member of an expedition that broke (attrition 100) and came home.")]
    public List<FragmentAward> brokenRoad = new List<FragmentAward> { new FragmentAward(FragmentKind.Acceptance, 2) };

    [Header("Ballads and their actors")]
    [Tooltip("A ballad's theme pays its primary kind ×5 and secondary kind ×3 (the vault's Role Archetype table) times this.")]
    public float balladScale = 1f;
    [Tooltip("A co-protagonist's share of the protagonist's ballad reward (at least one of each kind).")]
    public float coProtagonistShare = 0.6f;
    [Tooltip("Each legend on stage, for every event with a theme it plays through (the theme's primary kind).")]
    public int themedEventFragments = 1;
    [Tooltip("Co-protagonist roles a ballad offers besides its protagonist (a verse's cast tag may ask for others).")]
    public int balladCoProtagonists = 2;
}

/// <summary>
/// How Lyrical Fragments work, with no scene state (tested in <c>LyricalFragmentTests</c>). They are how legends grow
/// (the vault: "XP is replaced by these Lyrical Fragments"): their total sets the rank (<see cref="LegendGrowthRules"/>),
/// and each kind strengthens its binding (<see cref="BindingBonus"/>). A purse is a legend's fragments by kind name.
/// </summary>
public static class LyricalFragments
{
    /// <summary>The vault's Role Archetype table: the primary kind ×5, the secondary ×3.</summary>
    public const int PrimaryWeight = 5, SecondaryWeight = 3;

    public static readonly FragmentKind[] All = (FragmentKind[])Enum.GetValues(typeof(FragmentKind));

    public static readonly RoleArchetype[] Archetypes = { RoleArchetype.Leader, RoleArchetype.Resistor, RoleArchetype.Scholar, RoleArchetype.EmotionalCore, RoleArchetype.SacrificialLamb };

    /// <summary>"Fragment of Meaning".</summary>
    public static string Name(FragmentKind kind) => "Fragment of " + kind;

    /// <summary>"1 Fragment of Meaning" wording: "Fragments of Meaning" for any count but one.</summary>
    public static string Name(FragmentKind kind, int count) => (Math.Abs(count) == 1 ? "Fragment of " : "Fragments of ") + kind;

    /// <summary>The binding a kind is tagged with.</summary>
    public static string Binding(FragmentKind kind)
    {
        switch (kind)
        {
            case FragmentKind.Meaning: return "Resonance";
            case FragmentKind.Lucidity: return "Luminance";
            case FragmentKind.Catharsis: return "Flux";
            case FragmentKind.Acceptance: return "Void";
            case FragmentKind.Defiance: return "Cindergale";
            case FragmentKind.Vision: return "Crystal";
            default: return "Strand";
        }
    }

    /// <summary>The kind tagged with a binding (null for no binding).</summary>
    public static FragmentKind? OfBinding(string binding)
    {
        string canonical = MagicBindings.Canonical(binding);
        foreach (var kind in All) if (string.Equals(Binding(kind), canonical, StringComparison.OrdinalIgnoreCase)) return kind;
        return null;
    }

    /// <summary>"Meaning", "Fragment of Meaning" or its binding ("Resonance").</summary>
    public static bool TryParse(string text, out FragmentKind kind)
    {
        kind = FragmentKind.Meaning;
        string t = (text ?? string.Empty).Trim();
        if (t.StartsWith("Fragment of ", StringComparison.OrdinalIgnoreCase)) t = t.Substring("Fragment of ".Length).Trim();
        if (t.Length == 0) return false;
        foreach (var k in All)
            if (string.Equals(k.ToString(), t, StringComparison.OrdinalIgnoreCase)) { kind = k; return true; }
        var byBinding = OfBinding(t);
        if (byBinding.HasValue) { kind = byBinding.Value; return true; }
        return false;
    }

    /// <summary>A role's primary and secondary kinds (the vault's table).</summary>
    public static (FragmentKind primary, FragmentKind secondary) Of(RoleArchetype role)
    {
        switch (role)
        {
            case RoleArchetype.Resistor: return (FragmentKind.Defiance, FragmentKind.Acceptance);
            case RoleArchetype.Scholar: return (FragmentKind.Lucidity, FragmentKind.Rebirth);
            case RoleArchetype.EmotionalCore: return (FragmentKind.Catharsis, FragmentKind.Meaning);
            case RoleArchetype.SacrificialLamb: return (FragmentKind.Acceptance, FragmentKind.Rebirth);
            default: return (FragmentKind.Meaning, FragmentKind.Vision);
        }
    }

    /// <summary>"Leader", "Emotional Core", "emotional_core", "Sacrificial Lamb"...</summary>
    public static bool TryParseArchetype(string text, out RoleArchetype role)
    {
        role = RoleArchetype.None;
        string t = new string((text ?? string.Empty).Where(char.IsLetter).ToArray());
        if (t.Length == 0) return false;
        foreach (var r in Archetypes)
            if (string.Equals(r.ToString(), t, StringComparison.OrdinalIgnoreCase)) { role = r; return true; }
        return false;
    }

    /// <summary>"Emotional Core".</summary>
    public static string ArchetypeName(RoleArchetype role) => role == RoleArchetype.EmotionalCore ? "Emotional Core" : role == RoleArchetype.SacrificialLamb ? "Sacrificial Lamb" : role.ToString();

    /// <summary>
    /// A theme as written on a ballad: a Role Archetype (its primary and secondary kinds) or a single kind of fragment
    /// (both halves of the reward in that kind). False for anything else.
    /// </summary>
    public static bool TryParseTheme(string theme, out FragmentKind primary, out FragmentKind secondary)
    {
        primary = secondary = FragmentKind.Meaning;
        if (TryParseArchetype(theme, out var role))
        {
            (primary, secondary) = Of(role);
            return true;
        }
        if (!TryParse(theme, out primary)) return false;
        secondary = primary;
        return true;
    }

    /// <summary>What a theme pays its protagonist: primary ×5 and secondary ×3, times <paramref name="scale"/> (empty for an unknown theme).</summary>
    public static List<FragmentAward> ThemeReward(string theme, float scale)
    {
        var reward = new List<FragmentAward>();
        if (!TryParseTheme(theme, out var primary, out var secondary)) return reward;
        Add(reward, primary, Mathf.RoundToInt(PrimaryWeight * Math.Max(0f, scale)));
        Add(reward, secondary, Mathf.RoundToInt(SecondaryWeight * Math.Max(0f, scale)));
        return reward;
    }

    /// <summary>A share of a reward (at least one of each kind it holds, for any share above zero).</summary>
    public static List<FragmentAward> Share(IEnumerable<FragmentAward> reward, float share)
    {
        var result = new List<FragmentAward>();
        if (reward == null || share <= 0f) return result;
        foreach (var award in reward) if (award != null && award.amount > 0) Add(result, award.kind, Math.Max(1, Mathf.RoundToInt(award.amount * share)));
        return result;
    }

    /// <summary>A reward times a multiplier (the Underdog), each amount rounded.</summary>
    public static List<FragmentAward> Scale(IEnumerable<FragmentAward> reward, float multiplier) =>
        (reward ?? Enumerable.Empty<FragmentAward>()).Where(a => a != null).Select(a => new FragmentAward(a.kind, Mathf.RoundToInt(a.amount * multiplier))).Where(a => a.amount != 0).ToList();

    // Merge an amount into a reward list, one entry per kind.
    private static void Add(List<FragmentAward> reward, FragmentKind kind, int amount)
    {
        if (amount == 0) return;
        var existing = reward.FirstOrDefault(a => a.kind == kind);
        if (existing != null) existing.amount += amount;
        else reward.Add(new FragmentAward(kind, amount));
    }

    // ===== A LEGEND'S PURSE =====

    public static int Count(IReadOnlyDictionary<string, int> purse, FragmentKind kind) =>
        purse != null && purse.TryGetValue(kind.ToString(), out int n) ? n : 0;

    public static int Total(IReadOnlyDictionary<string, int> purse) => purse == null ? 0 : All.Sum(k => Count(purse, k));

    /// <summary>Add (or take) fragments; a kind never goes below zero. Returns what actually changed.</summary>
    public static int Add(Dictionary<string, int> purse, FragmentKind kind, int amount)
    {
        if (purse == null || amount == 0) return 0;
        int before = Count(purse, kind);
        int after = Math.Max(0, before + amount);
        purse[kind.ToString()] = after;
        return after - before;
    }

    /// <summary>Binding points a count of fragments adds to its binding.</summary>
    public static int BindingBonus(int count, int perPoint) => perPoint <= 0 ? 0 : Math.Max(0, count) / perPoint;

    /// <summary>Every binding's bonus from a purse (bindings with none left out).</summary>
    public static Dictionary<string, int> BindingBonuses(IReadOnlyDictionary<string, int> purse, int perPoint)
    {
        var bonuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kind in All)
        {
            int bonus = BindingBonus(Count(purse, kind), perPoint);
            if (bonus > 0) bonuses[Binding(kind)] = bonus;
        }
        return bonuses;
    }

    /// <summary>The kind a legend holds most of (null with none): the colour of its growth so far.</summary>
    public static FragmentKind? Dominant(IReadOnlyDictionary<string, int> purse)
    {
        FragmentKind? best = null;
        int most = 0;
        foreach (var kind in All)
        {
            int n = Count(purse, kind);
            if (n > most) { most = n; best = kind; }
        }
        return best;
    }

    /// <summary>"12 Meaning, 3 Vision" (kinds held, most first).</summary>
    public static string Describe(IReadOnlyDictionary<string, int> purse) =>
        string.Join(", ", All.Select(k => (k, n: Count(purse, k))).Where(x => x.n > 0).OrderByDescending(x => x.n).Select(x => $"{x.n} {x.k}"));

    // ===== THE UNDERDOG =====

    /// <summary>The Underdog meter: the Underdog Points of the legend's origin traits (vault: Legend Trait.md).</summary>
    public static int UnderdogMeter(LegendSoul soul, LegendTraitCatalog catalog)
    {
        if (soul == null || catalog == null) return 0;
        return soul.wish.Select(catalog.Origin).Where(t => t != null).Sum(t => t.underdogPoints);
    }

    public static bool IsUnderdog(int meter, FragmentTuning tuning) => tuning != null && tuning.underdogThreshold > 0 && meter >= tuning.underdogThreshold;

    /// <summary>
    /// What a deed's fragments are multiplied by: ×2 for an Underdog who punches above its station (a deed against
    /// the odds), otherwise 1.
    /// </summary>
    public static float Multiplier(bool underdog, bool againstTheOdds, FragmentTuning tuning) =>
        underdog && againstTheOdds && tuning != null ? Math.Max(1f, tuning.underdogMultiplier) : 1f;
}
