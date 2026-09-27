using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// How a legend's soul reads in its tooltip (<see cref="TooltipContent.Legend"/>): the Soul Leitmotif and Composure
/// up front, the Legend Traits, binding scores and Motif Awakenings behind "More...". Trait and state text is the
/// vault's, as written; [[links]] become keywords. Free of Unity, so the wording is testable.
/// </summary>
public static class LegendSoulText
{
    /// <summary>The two lines under the legend's quote: its Soul Leitmotif and its Composure.</summary>
    public static string Summary(LegendSoul soul, ComposureTuning tuning)
    {
        if (soul == null) return null;
        string ornaments = soul.ornaments.Count == 0 ? TooltipText.Muted("no Ornament yet")
            : string.Join(", ", soul.ornaments.Select(o => $"[[{o}]]")) + TooltipText.Muted(soul.ornaments.Count == 1 ? " Ornament" : " Ornaments");
        return $"{TooltipText.Row("[[Soul Leitmotif]]", $"[[{soul.leitmotif}]]{TooltipText.Separator}{ornaments}")}\n{TooltipText.Row("[[Composure]]", Composure(soul, tuning))}";
    }

    /// <summary>"Fractured (48 of 100)", coloured by how close the legend is to breaking; the Awakened State while it lasts.</summary>
    public static string Composure(LegendSoul soul, ComposureTuning tuning)
    {
        var state = ComposureRules.StateOf(soul.strain, tuning);
        string name = state == ComposureState.Spiraling || state == ComposureState.Surrender ? TooltipText.Bad(state.ToString())
            : state == ComposureState.Fractured ? TooltipText.Warn(state.ToString())
            : state == ComposureState.Pristine ? TooltipText.Good(state.ToString())
            : TooltipText.Value(state.ToString());
        string text = $"{name} {TooltipText.Muted($"({soul.strain:0} of {tuning.surrenderAt:0})")}";
        if (soul.surgeSevenths > 0) text = $"{TooltipText.Good("Awakened State")} {TooltipText.Muted($"({soul.surgeSevenths} Sevenths)")}{TooltipText.Separator}{text}";
        return text;
    }

    /// <summary>What the legend's Composure does to its council bonuses, for the "When Assigned" line ("x0.9 while Fractured").</summary>
    public static string CouncilNote(LegendSoul soul, float factor, ComposureTuning tuning)
    {
        if (soul == null || System.Math.Abs(factor - 1f) < 0.001f) return null;
        if (soul.surgeSevenths > 0) return $"x{factor:0.##} in the Awakened State";
        return $"x{factor:0.##} while {ComposureRules.StateOf(soul.strain, tuning)}";
    }

    /// <summary>The long read: the Composure state as the vault describes it, the Wish, the Expression, the bindings and the Motif Awakenings.</summary>
    public static string Details(LegendSoul soul, LegendTraitCatalog catalog, IReadOnlyDictionary<string, int> bindings, ComposureLore lore, ComposureTuning tuning)
    {
        if (soul == null) return null;
        catalog = catalog ?? new LegendTraitCatalog();
        var sb = new StringBuilder();

        var state = ComposureRules.StateOf(soul.strain, tuning);
        string described = lore?.Describe(state);
        sb.Append(TooltipText.Heading("Composure", state.ToString()));
        if (!string.IsNullOrEmpty(described)) sb.Append("\n").Append(TooltipText.Quote($"<i>{KeywordMarkup.FromMarkdown(described)}</i>"));
        sb.Append("\n").Append(TooltipText.Muted(ComposureAdvice(state)));

        if (soul.wish.Count > 0)
        {
            sb.Append("\n\n").Append(TooltipText.Heading("The Wish", TooltipText.Muted("origin")));
            foreach (var name in soul.wish)
            {
                var trait = catalog.Origin(name);
                if (trait == null) { sb.Append("\n").Append(TooltipText.Bullet(name)); continue; }
                string line = $"{TooltipText.Value(trait.name)} {TooltipText.Muted($"({trait.kind}: {trait.bindingEffect})")}";
                if (!string.IsNullOrEmpty(trait.description)) line += $"\n<i>{trait.description}</i>";
                if (!string.IsNullOrEmpty(trait.narrativeEffect)) line += "\n" + TooltipText.Muted(trait.narrativeEffect);
                sb.Append("\n").Append(TooltipText.Bullet(line));
            }
        }

        if (soul.expression.Count > 0)
        {
            sb.Append("\n\n").Append(TooltipText.Heading("The Expression", TooltipText.Muted("personality")));
            for (int i = 0; i < soul.expression.Count; i++)
            {
                string line;
                if (soul.HasEvolved(i))
                {
                    string from = i < soul.startingExpression.Count ? soul.startingExpression[i] : null;
                    line = $"{TooltipText.Value(soul.expression[i])} {TooltipText.Muted($"(was {from}, along [[{soul.evolvedAlong[i]}]])")}";
                    string how = catalog.Middle(soul.expression[i])?.whyAndHow;
                    if (!string.IsNullOrEmpty(how)) line += "\n" + TooltipText.Muted(how);
                }
                else
                {
                    var trait = catalog.Personality(soul.expression[i]);
                    line = TooltipText.Value(soul.expression[i]);
                    if (trait != null && trait.elements.Count > 0) line += " " + TooltipText.Muted($"({string.Join(", ", trait.elements)})");
                    if (!string.IsNullOrEmpty(trait?.quote)) line += $"\n<i>{trait.quote}</i>";
                }
                sb.Append("\n").Append(TooltipText.Bullet(line));
            }
        }

        if (bindings != null && bindings.Count > 0)
        {
            sb.Append("\n\n").Append(TooltipText.Heading("Bindings", TooltipText.Muted("proficiency")));
            foreach (var binding in MagicBindings.All.OrderByDescending(b => bindings.TryGetValue(b, out int v) ? v : 0))
            {
                int score = bindings.TryGetValue(binding, out int value) ? value : 0;
                string mark = binding == soul.leitmotif ? " (Soul Leitmotif)" : soul.ornaments.Contains(binding) ? " (Ornament)" : string.Empty;
                sb.Append("\n").Append(TooltipText.Row($"[[{binding}]]{TooltipText.Muted(mark)}", $"{TooltipText.Value(score.ToString())} {TooltipText.Muted(MagicBindings.Proficiency(score))}"));
            }
        }

        if (soul.awakenings.Count > 0)
        {
            sb.Append("\n\n").Append(TooltipText.Heading("Motif Awakenings"));
            foreach (var line in soul.awakenings) sb.Append("\n").Append(TooltipText.Bullet(line));
        }
        return sb.ToString();
    }

    /// <summary>The game's rule for each state, in a line (the vault's description sits above it).</summary>
    public static string ComposureAdvice(ComposureState state)
    {
        switch (state)
        {
            case ComposureState.Pristine: return "A moment of joy. It fades back to Clouded.";
            case ComposureState.Clouded: return "The normal state. The council's crises and its dead strain it; rest mends it.";
            case ComposureState.Fractured: return "Council bonuses dim. Healing back to Clouded, best done away from the council, brings a Motif Awakening.";
            case ComposureState.Spiraling: return "Council bonuses dim further. Unseat them to rest before they reach Surrender.";
            default: return "Lost to Dissonance.";
        }
    }
}
