using System;
using System.Linq;

public enum BattleForecastCategory { Overwhelming, Favored, Contested, Dangerous, Doomed }
public static class BattleLossBurden
{
    // Proposed category cutoffs; the design supplies qualitative meanings.
    public static BattleForecastCategory Forecast(float chance) => chance >= .9f ? BattleForecastCategory.Overwhelming : chance >= .65f ? BattleForecastCategory.Favored :
        chance > .35f ? BattleForecastCategory.Contested : chance > .1f ? BattleForecastCategory.Dangerous : BattleForecastCategory.Doomed;
    public static bool Mythical(bool won, bool manual, float chance) => won && manual && Forecast(chance) == BattleForecastCategory.Doomed;
    public static float Assess(BattleSide side, SideResult result)
    {
        var elites = side.sections.Where(x => x.eliteRole != BattleEliteRole.None).ToList();
        float permanent = elites.Count == 0 ? 0f : elites.Count(x => x.permanentDeath) / (float)elites.Count;
        float body = Math.Max(0f, Math.Min(1f, result.LossShare));
        if (side.CombatantCount <= 10) body = Math.Max(body, permanent);
        bool conductor = elites.Any(x => x.eliteRole == BattleEliteRole.Conductor && (x.permanentDeath || x.captured || x.evacuated || x.fled));
        float formation = side.sections.Count == 0 ? 0f : side.sections.Count(x => x.destroyed || x.captured) / (float)side.sections.Count;
        float equipment = side.sections.Count == 0 ? 0f : side.sections.Count(x => x.equipmentLost) / (float)side.sections.Count;
        return Math.Min(1f, body + (conductor ? .5f : 0f) + (side.StanceBroken ? .05f : 0f) + (side.CombatantCount > 10 ? formation * .1f : 0f)
            + equipment * .1f + (side.objectiveLost ? .25f : 0f));
    }
    public static BattleOutcome Victory(BattleSide side, float own, float enemy)
    {
        var outcome = BattleVerdicts.Tier(true, own, enemy);
        return side.CombatantCount <= 10 && side.sections.Any(x => x.permanentDeath) && outcome == BattleOutcome.DecisiveVictory ? BattleOutcome.CloseVictory : outcome;
    }
}
