using System;
using System.Linq;

public static partial class BattleResolver
{
    public sealed partial class BattleRun
    {
        private void SurvivalEvent(CombatSection unit, BattleSurvivalCause cause, string detail, float chance = 0f, float roll = 0f)
        {
            var side = ownerOf[unit];
            report.survival.Add(new BattleSurvivalEvent { attacker = side.attacker, voice = side.side.sections.IndexOf(unit), character = unit.leader?.name ?? unit.name,
                measure = m, beat = beat, check = unit.deathblowChecks, cause = cause, detail = detail, failureChance = chance, roll = roll });
        }

        private void DamageSurvival(CombatSection unit, float integrity, float composure)
        {
            if (!unit.deathKnell || !unit.Standing || integrity <= 0f && composure <= 0f) return;
            var s = ownerOf[unit]; var tune = combatSettings.Survival;
            float bond = s.side.Standing.Where(x => x != unit && SpatialRules.Support(x, unit)).Select(x => Bond(s, x, unit)).DefaultIfEmpty(0f).Max();
            bool medicine = s.side.Standing.Any(x => x != unit && x.mending > 0f && SpatialRules.Support(x, unit));
            int piety = BattleMeasureMath.Mastery(unit.leader?.piety ?? 0);
            float chance = BattleSurvivalLogic.FailureChance(tune, unit.deathblowChecks, unit.severeWounds, piety, bond, medicine, unit.savingRelic);
            float roll = rng.NextFloat(); unit.deathblowChecks++;
            if (roll < chance)
            {
                unit.destroyed = true; unit.permanentDeath = true; unit.committed = false;
                SurvivalEvent(unit, BattleSurvivalCause.Deathblow, "The Deathblow Check failed; this death is permanent.", chance, roll);
                Emit(s, unit, BattleSpatialCause.EliteCasualty, "An elite died after a failed Deathblow Check.");
                if (unit.leader == s.conductor) { s.bar = 0f; s.conductorFallen = true; }
            }
            else SurvivalEvent(unit, BattleSurvivalCause.SavingGrace, "Survived at the edge; the next check is more dangerous.", chance, roll);
        }

        private void EnterDeathKnell(CombatSection unit)
        {
            if (unit.eliteRole == BattleEliteRole.None || unit.integrity > 0f || unit.deathKnell || unit.destroyed) return;
            unit.integrity = 0f; unit.deathKnell = true; unit.knellCapability = combatSettings.Survival.knellCapability;
            unit.severeWounds = Math.Max(unit.severeWounds, .5f);
            SurvivalEvent(unit, BattleSurvivalCause.DeathKnell, "Zero Integrity: one Step per Measure, reduced capability, every later damaging event risks death.");
            Emit(ownerOf[unit], unit, BattleSpatialCause.EliteCasualty, "An elite entered Death Knell; bonded witnesses are shaken.");
        }

        private void EliteChecks()
        {
            foreach (var s in new[] { a, d })
                foreach (var unit in s.side.Standing.Where(x => x.eliteRole != BattleEliteRole.None && x.battleHex >= 0).ToList())
                {
                    bool check = s.enemy.side.Standing.Any(x => !x.deathKnell && SpatialRules.Distance(unit.battleHex, x.battleHex) <= SpatialRules.Reach(x)
                        && !SpatialRules.Protected(unit, s.attacker, x.battleHex));
                    bool mate = check && SpatialRules.Surrounded(s.attacker, unit);
                    if (check && !unit.eliteCheck) SurvivalEvent(unit, BattleSurvivalCause.EliteCheck, "An unscreened elite is under direct threat.");
                    if (mate && !unit.eliteCheckmate) { SurvivalEvent(unit, BattleSurvivalCause.Checkmate, "All physical exits are blocked; Subjugation is a danger, not automatic death."); Record(s, unit, BattleSpatialCause.Subjugation, "Elite Checkmate."); }
                    unit.eliteCheck = check; unit.eliteCheckmate = mate;
                }
        }

        /// <summary>Evacuation spends the player's choice and leaves a living elite missing in action.</summary>
        public string Evacuate(bool attacker, int section)
        {
            if (Over || !open || rhythm != null || pendingCadence != null) return "Evacuation is available between Beats of an open Measure.";
            var side = Of(attacker); if (section < 0 || section >= side.side.sections.Count) return "No such combatant.";
            var unit = side.side.sections[section];
            if (unit.eliteRole == BattleEliteRole.None || !unit.Standing) return "Only a standing elite can evacuate.";
            if (SpatialRules.Surrounded(attacker, unit)) return "All physical exits are blocked; evacuation needs an ally to open one.";
            unit.evacuated = unit.fled = true; unit.committed = false;
            SurvivalEvent(unit, BattleSurvivalCause.Evacuated, "Left the battlefield alive and missing in action.");
            foreach (var c in chords.Values.Where(c => c.Live && c.owner.unit == unit).ToList()) Suspend(c, "The performer evacuated.");
            if (unit.leader == side.conductor) { side.bar = 0f; side.conductorFallen = true; }
            Emit(side, unit, BattleSpatialCause.EliteCasualty, "An elite evacuated; command and screening change.");
            FlushReactionsAndHits(); return null;
        }

        private void PopulationFates()
        {
            foreach (var s in new[] { a, d })
                foreach (var unit in s.side.sections)
                    report.population.Add(BattleSurvivalLogic.Population(unit, s.startIntegrity[unit], s.attacker, t.baseWounded + s.woundedShare + s.Of(unit).wounded));
        }
    }
}
