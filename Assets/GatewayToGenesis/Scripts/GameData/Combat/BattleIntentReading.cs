using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Loss of information is explicit; an illusion is never presented as confirmed truth.</summary>
public static class BattleIntentReading
{
    public static IReadOnlyList<BattleActionIntent> Read(IEnumerable<BattleActionIntent> intents, bool observer, int clarity, Func<BattleActionIntent, int> veil)
    {
        var visible = new List<BattleActionIntent>();
        foreach (var original in intents)
        {
            int effective = clarity - Math.Max(0, veil?.Invoke(original) ?? 0);
            if (original.attacker != observer && (original.phantom && effective >= 2 || original.minor && effective >= 0 && effective < 1)) continue;
            var intent = original.Clone();
            if (intent.attacker == observer) { visible.Add(intent); continue; }
            intent.unverified = intent.phantom || effective < 0;
            intent.confirmed &= !intent.unverified;
            if (effective < 0) { intent.expectedIntegrity = intent.expectedComposure = intent.expectedGuard = intent.expectedWard = 0; intent.damageCertain = false; }
            if (effective < 1) { intent.holdLimit = intent.sounding = intent.minimumWardBeats = 0; intent.abjuredOnlyBy.Clear(); }
            if (intent.unverified)
            {
                intent.target = intent.to = -1; intent.card = null; intent.binding = SpellBinding.Unattuned;
                intent.name = effective <= -2 ? "Activity on Track" : "Unverified working";
                if (effective <= -2) intent.dueBeat = -1;
            }
            visible.Add(intent);
        }
        return visible;
    }
}
