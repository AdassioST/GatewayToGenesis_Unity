using System;
using System.Collections.Generic;
using System.Linq;

public enum BattlePremonitionKind { Divination, Prophetical }

[Serializable]
public sealed class BattlePreparationTuning
{
    // Authoring keys; late-game technologies must grant these Registers. Costs are proposed tuning.
    public string divinationTechnology = "Divination Arts", propheticalTechnology = "Prophetical Arts";
    public List<ResourceAmount> divinationCost = new List<ResourceAmount> { new ResourceAmount { resource = "Faith", amount = 10 } };
    public List<ResourceAmount> propheticalCost = new List<ResourceAmount> { new ResourceAmount { resource = "Faith", amount = 20 } };
    public string advancedDivinationTechnology, advancedPropheticalTechnology;
    public int advancedDivinationSlots, advancedPropheticalSlots;
    public string Technology(BattlePremonitionKind kind) => kind == BattlePremonitionKind.Divination ? divinationTechnology : propheticalTechnology;
    public IEnumerable<ResourceAmount> Cost(BattlePremonitionKind kind) => kind == BattlePremonitionKind.Divination ? divinationCost : propheticalCost;
    public string Prepare(BattlePremonitions ledger, BattlePremonitionKind kind, IConscriptionBank bank, bool ceremony)
    {
        var cost = (Cost(kind) ?? Enumerable.Empty<ResourceAmount>()).Where(c => c != null).GroupBy(c => c.resource)
            .Select(g => new ResourceAmount { resource = g.Key, amount = g.Sum(c => c.amount) }).ToList();
        return ledger.Prepare(kind, bank != null && !string.IsNullOrEmpty(Technology(kind)) && bank.Has(Technology(kind)), ceremony, () =>
        {
            if (cost.Count == 0 || cost.Any(c => string.IsNullOrEmpty(c.resource) || c.amount <= 0 || float.IsNaN(c.amount) || float.IsInfinity(c.amount) || bank.Amount(c.resource) < c.amount)) return false;
            foreach (var c in cost) bank.Spend(c.resource, c.amount); return true;
        });
    }
}

/// <summary>Prepared before contact. Spending a charge changes only this ledger; the encounter retries its untouched input.</summary>
[Serializable]
public sealed class BattlePremonitions
{
    public int divination, prophetical, advancedBossDivination, advancedBossProphetical;
    public int attemptsLost;
    public bool exhaustedAchievement;
    public string encounter;
    public int Remaining => divination + prophetical;
    public int Capacity(BattlePremonitionKind kind, bool boss) => kind == BattlePremonitionKind.Divination
        ? 2 + (boss ? Math.Max(0, advancedBossDivination) : 0) : 1 + (boss ? Math.Max(0, advancedBossProphetical) : 0);
    public string Prepare(BattlePremonitionKind kind, bool technologyUnlocked, bool ceremonyAvailable, Func<bool> payResources)
    {
        if (!technologyUnlocked || !ceremonyAvailable) return "Prepare this premonition through its unlocked Register and Ceremonial Arts.";
        int current = kind == BattlePremonitionKind.Divination ? divination : prophetical;
        if (current >= Capacity(kind, false)) return "All premonition slots of this kind are prepared.";
        if (payResources == null || !payResources()) return "The preparation's resource cost is unavailable.";
        if (kind == BattlePremonitionKind.Divination) divination++; else prophetical++;
        return null;
    }
    public void BeginEncounter(string id, bool majorBoss, bool divinationUnlocked, bool propheticalUnlocked)
    {
        if (id == encounter) return; // Reload or retry must never recharge the same boss.
        encounter = id; attemptsLost = 0; exhaustedAchievement = false;
        if (majorBoss)
        {
            if (divinationUnlocked) divination = Capacity(BattlePremonitionKind.Divination, true);
            if (propheticalUnlocked) prophetical = Capacity(BattlePremonitionKind.Prophetical, true);
        }
    }
    public bool ConsumeLoss(string id)
    {
        if (encounter != id || Remaining <= 0) return false;
        if (divination > 0) divination--; else prophetical--;
        attemptsLost++;
        if (attemptsLost >= 3 && Remaining == 0) exhaustedAchievement = true;
        return true;
    }
    public BattlePremonitions Clone() => (BattlePremonitions)MemberwiseClone();
}
