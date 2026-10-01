using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>A prepared, isolated Measure for native input/audio verification, without applying world aftermath.</summary>
public static class BattleRhythmRehearsal
{
    [MenuItem("Tools/Gateway to Genesis/Combat/Rhythm Rehearsal")]
    public static void Open()
    {
        if (!Application.isPlaying) { Debug.Log("Enter Play mode in ClickerScreen, then open Rhythm Rehearsal."); return; }
        BattlePerformancePlayer.Play(Prepare(), true, run => Debug.Log($"Rhythm rehearsal assessed: {run.Report.rhythm.Count} phrases, {run.Beat} battlefield Beats."));
    }

    public static BattleResolver.BattleRun Prepare()
    {
        CombatSection Unit(string name, int hex) => new CombatSection { name = name, battleHex = hex, count = 1, maxIntegrity = 200, integrity = 200,
            maxComposure = 100, composure = 100, speed = 0, row = FormationRow.Front, eliteRole = BattleEliteRole.Guard };
        var side = new BattleSide { name = "Rehearsal", manual = true, sections = { Unit("Vanguard", 7), Unit("Ward keeper", 6), Unit("Mender", 2) } };
        side.sections[0].composure = 20;
        side.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "rehearsal-strike", name = "Cadence strike",
            effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, 32) { range = 6 } } } });
        side.deck.Add(new DeckCard { voice = 1, card = new CombatCard { id = "rehearsal-ward", name = "Early Ward", purpose = SpellPurpose.Defensive,
            effects = { new CardEffect(CardOp.Guard, CardAim.Ally, 30) { flat = true, range = 6 } } } });
        side.deck.Add(new DeckCard { voice = 2, card = new CombatCard { id = "rehearsal-heartbeat", name = "Meet the heartbeat",
            effects = { new CardEffect(CardOp.CoRegulate, CardAim.Ally, 8) { range = 6, durationBeats = 3 } } } });
        var enemy = new BattleSide { name = "Threat", manual = true, sections = { Unit("Enemy", 8) } };
        enemy.deck.Add(new DeckCard { voice = 0, card = new CombatCard { id = "rehearsal-threat", name = "Committed threat",
            effects = { new CardEffect(CardOp.IntegrityDamage, CardAim.Enemy, 50) { range = 6 } } } });
        var settings = new CombatSettings { tuning = new CombatTuning { maxMeasures = 6, variance = 0, fatigueFrom = 100 },
            symphony = new SymphonySettings { tuning = new SymphonyTuning { handExtra = 12 } } };
        var run = BattleResolver.Begin(new BattleSetup { attacker = side, defender = enemy, field = new Battlefield { age = 3 }, seed = 123 }, settings, 1);
        run.BeginMeasure(); run.Spotlight(true, 0); run.Spotlight(true, 2);
        foreach (var spec in new[] { ("rehearsal-strike", 0, 4), ("rehearsal-ward", 0, 1), ("rehearsal-heartbeat", 0, 4) })
        {
            int index = run.Hand(true).ToList().FindIndex(c => c.card.id == spec.Item1);
            string why = run.CommitCard(true, index, spec.Item2, beat: spec.Item3);
            if (why != null) throw new InvalidOperationException(why);
        }
        string failure = run.CommitCard(false, 0, 0);
        if (failure != null) throw new InvalidOperationException(failure);
        return run;
    }
}
