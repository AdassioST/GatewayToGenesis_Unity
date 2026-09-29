using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The kitchen's trials (rules in <see cref="KitchenTrials"/>, the loop in Docs/Planning/DISCOVERY_LOOP.md): before the
/// people name a dish or drink of their own they try a batch of it, which uses up what goes in. Only then do they know
/// what it makes, and each trial says how near it came to one of the recipes no one has written down. Finding one pays
/// Era Score once, and whatever the people keep from it is finer than its ingredients alone would make.
/// </summary>
public partial class CultureSystem
{
    /// <summary>The kitchen's log of trials (never null once the culture exists).</summary>
    public KitchenLog Kitchen
    {
        get
        {
            if (_state.extensions == null) _state.extensions = new CultureExtensionState();
            if (_state.extensions.kitchen == null) _state.extensions.kitchen = new KitchenLog();
            return _state.extensions.kitchen;
        }
    }

    public IReadOnlyList<HiddenRecipeSpec> HiddenRecipes => (IReadOnlyList<HiddenRecipeSpec>)Life.hiddenRecipes ?? Array.Empty<HiddenRecipeSpec>();

    /// <summary>The trial of this very mix (whatever the batch size), or null when it was never tried.</summary>
    public KitchenTrial TrialOf(KitchenMethod method, IList<ResourceAmount> inputs) => KitchenTrials.Tried(Kitchen, method, inputs);

    /// <summary>The people have tried a batch of this mix: they know what it makes and may name it.</summary>
    public bool HasTasted(KitchenMethod method, IList<ResourceAmount> inputs) => TrialOf(method, inputs) != null;

    /// <summary>Batches the kitchen may still try this Seventh.</summary>
    public int TriesLeft => KitchenTrials.TriesLeft(Kitchen, _state.sevenths, Mathf.Max(1, Life.trialsPerSeventh));

    /// <summary>The hidden recipe found whose mix this is, or null (not one, or not found yet).</summary>
    public HiddenRecipeSpec FoundHiddenRecipe(KitchenMethod method, IList<ResourceAmount> inputs)
    {
        var spec = KitchenTrials.Matching(HiddenRecipes, method, inputs);
        return spec != null && IsFound(spec.formula.id) ? spec : null;
    }

    /// <summary>The hidden recipe was found by a trial.</summary>
    public bool IsFound(string id) => Experiments.Of(Kitchen.formulas, id)?.found == true;

    /// <summary>What the cooks know "it will be something like": the known recipe closest to the mix, or null.</summary>
    public string ExpectedLike(KitchenMethod method, IList<ResourceAmount> inputs)
    {
        var list = (inputs ?? new List<ResourceAmount>()).Where(i => i != null && i.amount > 0f && FactsOf(i.resource) != null).ToList();
        return list.Count == 0 ? null : ClosestRecipe(method, list)?.dish;
    }

    /// <summary>Why a batch of this cannot be tried now, or null. It must be something that could be invented, and the stores must hold what goes in.</summary>
    public string WhyNotTry(KitchenMethod method, IList<ResourceAmount> inputs)
    {
        if (!_state.founded) return "The culture has not been founded yet: no one keeps a Flavor Log.";
        string why = CultureInvention.WhyNot(method, inputs, FactsOf, KnownRecipes(), Life);
        if (why != null) return why;
        if (TriesLeft <= 0) return $"The kitchen has tried {Mathf.Max(1, Life.trialsPerSeventh)} batches this Seventh: the next can be tried next Seventh.";
        foreach (var group in inputs.Where(i => i != null && i.amount > 0f).GroupBy(i => i.resource, StringComparer.OrdinalIgnoreCase))
        {
            float need = group.Sum(i => i.amount);
            if (!Has(group.Key, need)) return $"A batch needs {need:0.#} {group.Key} ({Held(group.Key):0.#} held).";
        }
        return null;
    }

    /// <summary>
    /// Try a batch: what goes in is used up, the cooks learn what it makes (kept in the log), and how near it came to a
    /// hidden recipe. A trial that makes one finds it: Era Score, a moment in the people's history, and a finer dish if
    /// they keep it. Null (nothing changed) when <see cref="WhyNotTry"/> refuses.
    /// </summary>
    public KitchenTrial TryBatch(KitchenMethod method, IList<ResourceAmount> inputs)
    {
        string why = WhyNotTry(method, inputs);
        if (why != null) { GameLog.Event($"Trial refused: {why}", Log); return null; }
        var list = inputs.Where(i => i != null && i.amount > 0f).GroupBy(i => i.resource, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ResourceAmount { resource = g.First().resource, amount = g.Sum(i => i.amount) }).ToList();
        var units = GameUnitsLogic.Instance;
        foreach (var input in list)
        {
            units?.ChangeResourceFromName(input.resource, -input.amount, false);
            RecordUse(input.resource, input.amount);
        }
        var life = Life;
        string noun = CultureInvention.Noun(method);
        var result = CultureInvention.Derive("trial", $"A trial {noun}", method, list, ClosestRecipe(method, list), FactsOf, LuxuriesOf, life);
        var match = KitchenTrials.Matching(HiddenRecipes, method, list);
        bool wasFound = match != null && IsFound(match.formula.id);
        if (match != null) KitchenTrials.Apply(match, result);
        var (trial, near, newHint) = KitchenTrials.Record(Kitchen, HiddenRecipes, method, list, result, _state.sevenths);
        if (match != null) trial.found = match.formula.id;
        var warmth = (Warmth)trial.warmth;
        GameLog.Event($"Trial {noun}: {string.Join(" + ", list.Select(i => $"{i.amount:0.#} {i.resource}"))} -> {warmth}{(near != null ? $" ({near.formula.id})" : string.Empty)}", Log);

        if (match != null && !wasFound)
        {
            string name = match.formula.name ?? match.formula.id;
            if (match.eraScore > 0) AgeProgression.Award(match.eraScore, $"Found {name}");
            AddJoy(life.inventionJoy);
            Remember("recipe-found", name, $"{Capital(PeopleWord)} found {name}, a recipe no one had written down: {match.formula.description}");
            NotificationFeed.Push($"{name}: a lost recipe found", $"{match.formula.description} Keep it from the kitchen to make it your people's own.",
                NotificationFeed.Topic.Culture, CultureWindow.Open, "culture:recipe-found:" + match.formula.id);
        }
        else if (newHint && near != null)
            NotificationFeed.Push("A new line in the Flavor Log", $"{KitchenTrials.WarmthWords(warmth)} The Flavor Log remembers more of it now.",
                NotificationFeed.Topic.Culture, CultureWindow.Open, $"culture:recipe-hint:{near.formula.id}:{warmth}");
        RaiseChanged();
        return trial;
    }

    /// <summary>A hidden recipe the people have heard of: the method is one they know, with the hints its trials have brought to light.</summary>
    public sealed class Whisper
    {
        public HiddenRecipeSpec spec;
        public FormulaProgress progress;
        public List<string> hints;
        public bool Found => progress != null && progress.found;
    }

    /// <summary>The hidden recipes whose way of making the people know (a kitchen, a crock, a still), found ones first.</summary>
    public List<Whisper> Whispers()
    {
        var known = new HashSet<KitchenMethod>(KnownRecipes().Select(r => r.method));
        return HiddenRecipes.Where(s => s?.formula != null && known.Contains(s.Method)).Select(s =>
        {
            var p = Experiments.Of(Kitchen.formulas, s.formula.id);
            return new Whisper { spec = s, progress = p, hints = s.formula.hints.Where(h => !string.IsNullOrWhiteSpace(h)).Take(Experiments.HintsShown(s.formula, p)).ToList() };
        }).OrderByDescending(w => w.Found).ToList();
    }

    /// <summary>GameValues "kitchen": trials (no target, or "trials"), "found" (hidden recipes found), or "found:&lt;id&gt;" (1 once found).</summary>
    public float KitchenValue(string target)
    {
        string t = (target ?? string.Empty).Trim();
        if (t.Length == 0 || string.Equals(t, "trials", StringComparison.OrdinalIgnoreCase)) return Kitchen.total;
        if (string.Equals(t, "found", StringComparison.OrdinalIgnoreCase)) return Kitchen.formulas.Count(f => f != null && f.found);
        if (t.StartsWith("found:", StringComparison.OrdinalIgnoreCase)) return IsFound(t.Substring(6).Trim()) ? 1f : 0f;
        return 0f;
    }
}
