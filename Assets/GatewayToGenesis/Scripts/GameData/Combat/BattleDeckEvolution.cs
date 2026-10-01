using System;
using System.Collections.Generic;
using System.Linq;

public enum BattlePollution { None, Wound, Burn, Restrained, Agony, Slimed, Panic, Bleeding, Frostbite, MindControl }

[Serializable]
public sealed class BattleDeckAlteration
{
    public string card, name;
    public bool trauma, opus;
    public float belowComposure = .5f, power = .5f;
    public BattlePollution after = BattlePollution.Agony;
    public BattleDeckAlteration Clone() => (BattleDeckAlteration)MemberwiseClone();
}

[Serializable]
public sealed class BattleDeckEvolution
{
    public List<BattlePollution> pollution = new List<BattlePollution>();
    public List<BattleDeckAlteration> alterations = new List<BattleDeckAlteration>();
    public BattleDeckEvolution Clone() => new BattleDeckEvolution { pollution = pollution.ToList(), alterations = alterations.Select(x => x.Clone()).ToList() };
    public string IntegrateHome(IEnumerable<string> selectedCards)
    {
        var selected = (selectedCards ?? Array.Empty<string>()).Distinct().ToList();
        if (selected.Count > 2) return "Choose at most two Field Alterations for Legend Opus.";
        if (selected.Any(id => !alterations.Any(a => a.card == id && !a.trauma))) return "Only qualifying Field Alterations can be integrated.";
        var kept = alterations.Where(a => a.opus || selected.Contains(a.card) && !a.trauma).GroupBy(a => a.card).Select(g => g.First().Clone()).ToList();
        foreach (var a in kept) a.opus = true;
        pollution.Clear(); alterations = kept; return null;
    }
    public void RestInField() { /* Field rest never removes Trauma or permanently integrates an improvisation. */ }
    public CombatCard Alter(CombatCard card)
    {
        var altered = card.Clone();
        var a = alterations.FirstOrDefault(x => x.card == card.id);
        if (a != null) { altered.alteredBelowComposure = a.belowComposure; altered.alteredPower = a.power; altered.pollutionAfter = a.after; }
        return altered;
    }
}

public static class BattlePollutionCards
{
    public static CombatCard Of(BattlePollution kind) => new CombatCard { id = "pollution-" + kind.ToString().ToLowerInvariant(),
        name = kind.ToString(), kind = CardKind.Skill, pollution = kind, purpose = SpellPurpose.Utility,
        catchline = "Contaminates the hand. Purge it to restore clean draws." };
}
