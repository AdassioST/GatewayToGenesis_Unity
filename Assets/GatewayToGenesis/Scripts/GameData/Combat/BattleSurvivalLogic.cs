using System;
using System.Collections.Generic;

public enum BattleSurvivalCause { DeathKnell, SavingGrace, Deathblow, EliteCheck, Checkmate, Evacuated, Captured }

[Serializable]
public sealed class BattleSurvivalTuning
{
    // Proposed probabilities. Neither Piety nor any support can make survival certain.
    public float failureBase = .3f, failurePerCheck = .12f, wounds = .2f, pietyPerTier = .025f;
    public float bondGrace = .15f, medicineGrace = .1f, relicGrace = .08f;
    public float minimumFailure = .05f, maximumFailure = .95f, knellCapability = .15f;
}

[Serializable]
public sealed class BattleSurvivalEvent
{
    public int measure, beat, voice, check;
    public bool attacker;
    public BattleSurvivalCause cause;
    public string character, detail;
    public float failureChance, roll;
}

[Serializable]
public sealed class BattlePopulationFate
{
    public bool attacker;
    public string section, unitId;
    public int original, healthy, dead, wounded, recoverable, missing, captured;
    public int Accounted => healthy + dead + wounded + recoverable + missing + captured;
}

public static class BattleSurvivalLogic
{
    public static float FailureChance(BattleSurvivalTuning tuning, int previousChecks, float woundShare, int pietyTier, float bond, bool medicine, bool relic)
    {
        tuning = tuning ?? new BattleSurvivalTuning();
        float chance = tuning.failureBase + Math.Max(0, previousChecks) * tuning.failurePerCheck + Math.Max(0f, woundShare) * tuning.wounds
            - Math.Max(0, pietyTier) * tuning.pietyPerTier - Math.Max(0f, Math.Min(1f, bond)) * tuning.bondGrace
            - (medicine ? tuning.medicineGrace : 0f) - (relic ? tuning.relicGrace : 0f);
        return Math.Max(Math.Max(.001f, tuning.minimumFailure), Math.Min(Math.Min(.999f, tuning.maximumFailure), chance));
    }

    public static BattlePopulationFate Population(CombatSection section, float startIntegrity, bool attacker, float woundedShare)
    {
        var fate = new BattlePopulationFate { attacker = attacker, section = section.name, unitId = section.unitId, original = Math.Max(0, section.count) };
        if (section.eliteRole != BattleEliteRole.None)
        {
            if (section.destroyed) fate.dead = fate.original;
            else if (section.captured) fate.captured = fate.original;
            else if (section.evacuated || section.fled) fate.missing = fate.original;
            else if (section.deathKnell || section.integrity < startIntegrity) fate.wounded = fate.original;
            else fate.healthy = fate.original;
            return fate;
        }
        int lost = Math.Min(fate.original, Math.Max(0, (int)Math.Round(fate.original * Math.Max(0, startIntegrity - Math.Max(0, section.integrity)) / Math.Max(1f, startIntegrity))));
        int injured = Math.Min(lost, Math.Max(0, (int)Math.Round(lost * Math.Max(0f, Math.Min(.9f, woundedShare)))));
        fate.dead = lost - injured; fate.recoverable = injured / 4; fate.wounded = injured - fate.recoverable;
        int remaining = fate.original - lost;
        if (section.captured) fate.captured = remaining;
        else if (section.fled) { fate.missing = remaining / 10; fate.healthy = remaining - fate.missing; }
        else fate.healthy = remaining;
        return fate;
    }
}
