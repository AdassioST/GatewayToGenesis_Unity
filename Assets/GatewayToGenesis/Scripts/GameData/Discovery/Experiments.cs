using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One component of a <see cref="SecretFormula"/> and the share of the whole it must make up.</summary>
[Serializable]
public class FormulaPart
{
    [UnityEngine.Tooltip("A component: a resource for the kitchen (later a note, an element or a binding for spells).")]
    public string component;
    [UnityEngine.Tooltip("The least share of the whole it may make up (0-1).")]
    [UnityEngine.Range(0f, 1f)] public float minShare = 0.01f;
    [UnityEngine.Tooltip("The most share of the whole it may make up (0-1).")]
    [UnityEngine.Range(0f, 1f)] public float maxShare = 1f;
}

/// <summary>
/// Something no one has made yet, and what must go into it (a lost recipe; later a spell). It is found only by trying:
/// a trial whose mix falls inside every part's share, made the right way, finds it. Its hints come to light as the
/// trials come closer (<see cref="Experiments.HintsShown"/>).
/// </summary>
[Serializable]
public class SecretFormula
{
    public string id;
    [UnityEngine.Tooltip("What it is called once found (the player may rename what they make of it).")]
    public string name;
    [UnityEngine.Tooltip("How it is made: the kitchen's Cook, Ferment or Distill (later a spell's way of weaving).")]
    public string method;
    public List<FormulaPart> parts = new List<FormulaPart>();
    [UnityEngine.Tooltip("Nothing but its parts may go in: anything else spoils it.")]
    public bool exclusive;
    [UnityEngine.Tooltip("Riddles, vaguest first: the first is whispered from the start, the next once a trial comes close, the last once one comes very close.")]
    public List<string> hints = new List<string>();
    [UnityEngine.TextArea(1, 4)] public string description;
}

/// <summary>How near a trial came to a formula no one has found yet (<see cref="Experiments.WarmthOf"/>).</summary>
public enum Warmth
{
    /// <summary>Nothing of any formula in it.</summary>
    Nothing,
    /// <summary>Something of one: at least one of its parts, at the right share.</summary>
    Faint,
    /// <summary>Half of it or more.</summary>
    Close,
    /// <summary>Three quarters or more: every part in it, some at the wrong share, or all but one.</summary>
    VeryClose,
    /// <summary>It: every part at its share, made the right way.</summary>
    Found,
}

/// <summary>What a civilization has learned about one formula (saved by whoever keeps the trials).</summary>
[Serializable]
public class FormulaProgress
{
    public string id;
    /// <summary>The closest any trial has come (<see cref="Warmth"/> as an int).</summary>
    public int best;
    public bool found;
    /// <summary>Trials that came near it at all (Faint or better).</summary>
    public int near;
}

/// <summary>
/// Hypothesis and test over mixtures, with no scene state (tested in <c>ExperimentTests</c>): the player chooses what goes
/// into something and how it is made, tries it, and learns what it does and how close it came to anything no one has
/// made yet. Shared by the kitchen (<see cref="CultureSystem"/>'s trials) and meant for spells later. The rules:
/// every trial teaches something (<see cref="Warmth"/>, and the hints it brings to light), the same mix is recognised
/// again (<see cref="Signature"/>), and a formula is found only by making it.
/// </summary>
public static class Experiments
{
    /// <summary>Each component's share of the whole (amounts over their sum), summed per component. Empty with nothing in it.</summary>
    public static Dictionary<string, float> Shares(IEnumerable<(string component, float amount)> mix)
    {
        var list = (mix ?? Enumerable.Empty<(string, float)>()).Where(m => !string.IsNullOrEmpty(m.component) && m.amount > 0f).ToList();
        float total = list.Sum(m => m.amount);
        var shares = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (total <= 0f) return shares;
        foreach (var (component, amount) in list)
        {
            shares.TryGetValue(component, out float was);
            shares[component] = was + amount / total;
        }
        return shares;
    }

    /// <summary>
    /// The same mix made the same way gives the same key, whatever the order or the batch size ("Cook|earth-beans:0.667|wild honey:0.333").
    /// </summary>
    public static string Signature(string method, IDictionary<string, float> shares) =>
        (method ?? string.Empty) + "|" + string.Join("|", (shares ?? new Dictionary<string, float>()).Where(p => p.Value > 0f)
            .Select(p => (key: p.Key.Trim().ToLowerInvariant(), share: Math.Round(p.Value, 3))).OrderBy(p => p.key, StringComparer.Ordinal)
            .Select(p => p.key + ":" + p.share.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));

    private static float ShareOf(IDictionary<string, float> shares, string component) =>
        component != null && shares != null && shares.TryGetValue(component.Trim(), out float s) ? s : 0f;

    private static bool Within(FormulaPart part, float share) =>
        share >= Math.Max(0f, part.minShare) - 1e-4f && share <= Math.Max(part.minShare, part.maxShare) + 1e-4f;

    /// <summary>Share of the mix that is none of the formula's parts.</summary>
    public static float Extra(SecretFormula formula, IDictionary<string, float> shares)
    {
        if (formula == null || shares == null) return 0f;
        var parts = new HashSet<string>(formula.parts.Where(p => p != null && !string.IsNullOrEmpty(p.component)).Select(p => p.component.Trim()), StringComparer.OrdinalIgnoreCase);
        return shares.Where(p => !parts.Contains(p.Key)).Sum(p => p.Value);
    }

    /// <summary>
    /// How much of the formula a mix holds, 0-1: each part present at its share counts whole, present at the wrong share
    /// half, absent nothing; an exclusive formula loses what else went in. 0 when made another way.
    /// </summary>
    public static float Match(SecretFormula formula, string method, IDictionary<string, float> shares)
    {
        if (formula == null || shares == null || shares.Count == 0 || !string.Equals(formula.method, method, StringComparison.OrdinalIgnoreCase)) return 0f;
        var parts = formula.parts.Where(p => p != null && !string.IsNullOrEmpty(p.component)).ToList();
        if (parts.Count == 0) return 0f;
        float score = parts.Sum(p => { float s = ShareOf(shares, p.component); return s <= 0f ? 0f : Within(p, s) ? 1f : 0.5f; }) / parts.Count;
        if (formula.exclusive) score *= 1f - Math.Min(1f, Extra(formula, shares));
        return score;
    }

    /// <summary>The mix is the formula: every part at its share, made its way (and nothing else in it when exclusive).</summary>
    public static bool Matches(SecretFormula formula, string method, IDictionary<string, float> shares)
    {
        if (formula == null || shares == null || !string.Equals(formula.method, method, StringComparison.OrdinalIgnoreCase)) return false;
        var parts = formula.parts.Where(p => p != null && !string.IsNullOrEmpty(p.component)).ToList();
        if (parts.Count == 0 || parts.Any(p => !Within(p, ShareOf(shares, p.component)))) return false;
        return !formula.exclusive || Extra(formula, shares) <= 1e-4f;
    }

    /// <summary>A match score read as <see cref="Warmth"/> (a full match is <see cref="Warmth.Found"/>).</summary>
    public static Warmth Read(float score, bool matches) =>
        matches ? Warmth.Found : score >= 0.75f - 1e-4f ? Warmth.VeryClose : score >= 0.5f - 1e-4f ? Warmth.Close : score > 0f ? Warmth.Faint : Warmth.Nothing;

    /// <summary>
    /// The nearest a trial came to any formula not yet <paramref name="found"/> (by id), and which one; (Nothing, null)
    /// when it holds nothing of any. On a tie the first listed wins, so a replay reads the same.
    /// </summary>
    public static (Warmth warmth, SecretFormula formula) WarmthOf(IEnumerable<SecretFormula> formulas, ICollection<string> found, string method, IDictionary<string, float> shares)
    {
        var best = (warmth: Warmth.Nothing, formula: (SecretFormula)null);
        float bestScore = 0f;
        foreach (var f in formulas ?? Enumerable.Empty<SecretFormula>())
        {
            if (f == null || string.IsNullOrEmpty(f.id) || (found != null && found.Contains(f.id))) continue;
            bool matches = Matches(f, method, shares);
            float score = matches ? 2f : Match(f, method, shares);
            if (score <= bestScore + 1e-5f) continue;
            bestScore = score;
            best = (Read(score, matches), f);
        }
        return best;
    }

    /// <summary>How many of a formula's hints are known: the first from the start, the second once a trial came Close, the third once one came Very close (all once found).</summary>
    public static int HintsShown(SecretFormula formula, FormulaProgress progress)
    {
        int count = formula?.hints?.Count ?? 0;
        if (count == 0) return 0;
        if (progress != null && progress.found) return count;
        int best = progress?.best ?? 0;
        int shown = 1 + (best >= (int)Warmth.Close ? 1 : 0) + (best >= (int)Warmth.VeryClose ? 1 : 0);
        return Math.Min(count, shown);
    }

    /// <summary>
    /// Records a trial against one formula: its best warmth, how often trials came near, and whether it is found.
    /// Returns true when this trial brought a hint to light that was not known before.
    /// </summary>
    public static bool Record(List<FormulaProgress> progress, SecretFormula formula, Warmth warmth)
    {
        if (progress == null || formula == null || string.IsNullOrEmpty(formula.id) || warmth == Warmth.Nothing) return false;
        var p = progress.Find(x => x != null && string.Equals(x.id, formula.id, StringComparison.OrdinalIgnoreCase));
        if (p == null) progress.Add(p = new FormulaProgress { id = formula.id });
        int before = HintsShown(formula, p);
        p.near++;
        p.best = Math.Max(p.best, (int)warmth);
        if (warmth == Warmth.Found) p.found = true;
        return HintsShown(formula, p) > before;
    }

    /// <summary>The saved progress on a formula, or null.</summary>
    public static FormulaProgress Of(IEnumerable<FormulaProgress> progress, string id) =>
        string.IsNullOrEmpty(id) ? null : progress?.FirstOrDefault(p => p != null && string.Equals(p.id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Why a formula cannot be what it claims, or null: an id, a way of making, parts with sane shares that can all fit in one mix.</summary>
    public static string Problem(SecretFormula formula)
    {
        if (formula == null) return "is empty";
        if (string.IsNullOrWhiteSpace(formula.id)) return "has no id";
        if (string.IsNullOrWhiteSpace(formula.method)) return "has no way of making";
        var parts = formula.parts?.Where(p => p != null).ToList() ?? new List<FormulaPart>();
        if (parts.Count == 0) return "has no parts";
        if (parts.Any(p => string.IsNullOrWhiteSpace(p.component))) return "has a part with no component";
        if (parts.Select(p => p.component.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != parts.Count) return "names a component twice";
        if (parts.Any(p => p.minShare <= 0f || p.maxShare < p.minShare || p.maxShare > 1f)) return "has a part whose shares are not 0 < min <= max <= 1";
        if (parts.Sum(p => p.minShare) > 1f + 1e-4f) return "asks for more than the whole (its least shares sum past 1)";
        if (formula.exclusive && parts.Sum(p => p.maxShare) < 1f - 1e-4f) return "is exclusive but its most shares cannot fill the whole";
        if (formula.hints == null || formula.hints.Count(h => !string.IsNullOrWhiteSpace(h)) == 0) return "has no hint: no one could ever look for it";
        return null;
    }
}
