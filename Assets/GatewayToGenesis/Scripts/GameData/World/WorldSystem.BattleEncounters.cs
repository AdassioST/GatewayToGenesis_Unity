using System;
using System.Collections.Generic;
using System.Linq;

public partial class WorldSystem
{
    [SaveOptionalField] private BattleEncounterState _pendingEncounter;
    [SaveOptionalField] private BattlePremonitions _premonitions = new BattlePremonitions();
    [SaveOptionalField] private List<BattleRecord> _battleHistory = new List<BattleRecord>();
    private BattleEncounter _encounter;
    public BattleEncounter PendingEncounter => _encounter;
    public BattlePremonitions PreparedPremonitions => (_premonitions ?? new BattlePremonitions()).Clone();
    public IReadOnlyList<BattleRecord> BattleHistory => _battleHistory;

    public string PreparePremonition(BattlePremonitionKind kind)
    {
        if (_pendingEncounter != null) return "Premonitions must be prepared before contact.";
        _premonitions = _premonitions ?? new BattlePremonitions();
        return Combat.Preparation.Prepare(_premonitions, kind, _bank, Grimoire.Current().ceremonies > 0);
    }
    private bool RegisterUnlocked(BattlePremonitionKind kind) => !string.IsNullOrEmpty(Combat.Preparation.Technology(kind)) && Researched(Combat.Preparation.Technology(kind));
    private void PrepareEncounter(BattleSetup setup, WorldUnit attacker, WorldUnit defender)
    {
        _premonitions = _premonitions ?? new BattlePremonitions();
        var p = Combat.Preparation;
        _premonitions.advancedBossDivination = !string.IsNullOrEmpty(p.advancedDivinationTechnology) && Researched(p.advancedDivinationTechnology) ? Math.Max(0, p.advancedDivinationSlots) : 0;
        _premonitions.advancedBossProphetical = !string.IsNullOrEmpty(p.advancedPropheticalTechnology) && Researched(p.advancedPropheticalTechnology) ? Math.Max(0, p.advancedPropheticalSlots) : 0;
        string id = "battle:" + Map.seed + ":" + _battlesFought;
        _premonitions.BeginEncounter(id, setup.boss || setup.majorEncounter, RegisterUnlocked(BattlePremonitionKind.Divination), RegisterUnlocked(BattlePremonitionKind.Prophetical));
        _pendingEncounter = new BattleEncounterState { id = id, attackerUnit = attacker.id, defenderUnit = defender.id,
            playerAttacker = WorldBattles.IsPlayers(attacker), original = setup.Clone(setup.seed), wasPaused = TimeSystemLogic.Instance?.isTimePaused ?? true };
        RestoreEncounter();
    }
    private void RestoreEncounter()
    {
        _battleHistory = _battleHistory ?? new List<BattleRecord>(); _premonitions = _premonitions ?? new BattlePremonitions();
        LastBattleRecord = _battleHistory.LastOrDefault(); LastBattle = LastBattleRecord?.report;
        if (_pendingEncounter == null) { _encounter = null; return; }
        _encounter = new BattleEncounter(_pendingEncounter, Combat, _premonitions);
        TimeSystemLogic.Instance?.PauseTime(true);
        BattleEncounterWindow.Show(_encounter, AcceptEncounter);
    }
    private void AcceptEncounter()
    {
        if (_encounter?.State.accepted != true || _encounter.Report == null) return;
        var attacker = Map.Units.FirstOrDefault(u => u.id == _pendingEncounter.attackerUnit);
        var defender = Map.Units.FirstOrDefault(u => u.id == _pendingEncounter.defenderUnit);
        if (attacker == null || defender == null) throw new InvalidOperationException("The prepared encounter's forces are missing from the saved map.");
        var final = new BattleSetup { attacker = _encounter.Attacker, defender = _encounter.Defender, field = _pendingEncounter.original.field, seed = _pendingEncounter.original.seed };
        var record = Record(_pendingEncounter.original, attacker, defender, Place(Map.Get(defender.coord)));
        record.preview.forecast = _pendingEncounter.forecast;
        var notice = new List<string>();
        ApplyEncounterResult(attacker, defender, final, record, _encounter.Report, notice);
        _battleHistory.Add(record); if (_battleHistory.Count > 16) _battleHistory.RemoveAt(0);
        bool yours = _pendingEncounter.playerAttacker;
        if ((yours ? record.report.attacker : record.report.defender).mythical)
            Achievements.Report(AchievementEvent.Of(AchievementSignal.MythicalBattleWon).From(_pendingEncounter.id, yours ? final.attacker.name : final.defender.name));
        ReportPremonitionExhaustion();
        bool paused = _pendingEncounter.wasPaused; _pendingEncounter = null; _encounter = null;
        TimeSystemLogic.Instance?.PauseTime(paused);
        if (notice.Count > 0) Say(string.Join(" ", notice)); Changed?.Invoke();
    }
    public void ReportPremonitionExhaustion()
    {
        if (_pendingEncounter == null || !_premonitions.exhaustedAchievement) return;
        var foe = Map.Units.FirstOrDefault(u => u.id == (_pendingEncounter.playerAttacker ? _pendingEncounter.defenderUnit : _pendingEncounter.attackerUnit));
        if (foe?.identity == BandIdentity.Atonalis)
            Achievements.Report(AchievementEvent.Of(AchievementSignal.PremonitionsExhausted, _premonitions.attemptsLost).From(_pendingEncounter.id, "band:" + foe.id));
    }
}
