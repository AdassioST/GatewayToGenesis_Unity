using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Each legend's personal grimoire (<see cref="LegendGrimoires"/>): the Symphony Cards it has learned from those the
/// civilization owns, as many as it has pages. Saved with the legend's record. A legend whose grimoire the player never
/// touched learns its own bindings' cards by itself.
/// </summary>
public partial class LegendProgress
{
    /// <summary>A legend's grimoire changed (learned or forgot a card).</summary>
    public event Action<string> GrimoireChanged;

    /// <summary>Pages in a legend's grimoire.</summary>
    public int GrimoirePages(string legendName) => LegendGrimoires.Pages(Soul(legendName), Greats(legendName));

    /// <summary>The Symphony Card ids a legend carries (its choice, or what it learns by itself).</summary>
    public IReadOnlyList<string> PersonalGrimoire(string legendName)
    {
        if (legendName == null || !_recruited.TryGetValue(legendName, out var r) || r.lost) return Array.Empty<string>();
        var owned = Grimoire.Current();
        int pages = GrimoirePages(legendName);
        if (!r.grimoireChosen) return LegendGrimoires.Default(r.soul, owned, pages);
        // Only cards the civilization still knows, and no more than the pages hold.
        return (r.grimoire ?? new List<string>()).Where(id => owned.cards.Any(c => c != null && c.id == id)).Take(pages).ToList();
    }

    public string WhyNotLearn(string legendName, SymphonyCardData card)
    {
        if (!IsRecruited(legendName)) return "No such legend.";
        return LegendGrimoires.WhyNotLearn(card, Soul(legendName), PersonalGrimoire(legendName), GrimoirePages(legendName), Grimoire.Current());
    }

    /// <summary>A legend learns a Symphony Card into its grimoire (false: <see cref="WhyNotLearn"/>).</summary>
    public bool Learn(string legendName, SymphonyCardData card)
    {
        if (WhyNotLearn(legendName, card) != null) return false;
        var r = _recruited[legendName];
        r.grimoire = PersonalGrimoire(legendName).ToList();
        r.grimoire.Add(card.id);
        r.grimoireChosen = true;
        GameLog.Event($"{legendName} learns {card.DisplayName} into its grimoire", Log);
        GrimoireChanged?.Invoke(legendName);
        Changed?.Invoke();
        return true;
    }

    /// <summary>A legend lets a card go from its grimoire, freeing its page.</summary>
    public bool Forget(string legendName, string cardId)
    {
        if (!IsRecruited(legendName)) return false;
        var r = _recruited[legendName];
        var list = PersonalGrimoire(legendName).ToList();
        if (list.RemoveAll(id => string.Equals(id, cardId, StringComparison.OrdinalIgnoreCase)) == 0) return false;
        r.grimoire = list;
        r.grimoireChosen = true;
        GrimoireChanged?.Invoke(legendName);
        Changed?.Invoke();
        return true;
    }
}
