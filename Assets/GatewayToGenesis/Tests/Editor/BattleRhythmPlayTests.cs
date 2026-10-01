using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BattleRhythmPlayTests
{
    [UnityTest]
    public IEnumerator DspPlayerPerformsFourBeatsAndOneFermata_WithTimeScaleZero()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/GatewayToGenesis/Scenes/ClickerScreen.unity");
        yield return new EnterPlayMode();
        bool assist = GameSettings.RhythmAssist;
        float scale = Time.timeScale;
        GameSettings.RhythmAssist = true;
        Time.timeScale = 0;
        var run = BattleRhythmRehearsal.Prepare();
        bool assessed = false;
        var player = BattlePerformancePlayer.Play(run, true, _ => assessed = true);
        double deadline = AudioSettings.dspTime + 15;
        while (!assessed && AudioSettings.dspTime < deadline) yield return null;
        GameSettings.RhythmAssist = assist; Time.timeScale = scale;
        Assert.IsTrue(assessed, "DSP timing must continue independently of scaled game time");
        Assert.AreEqual(4, run.Beat); Assert.AreEqual(1, run.Report.rhythm.Count(x => x.abjuration));
        Assert.AreEqual(4, run.Report.rhythm.Count(x => !x.abjuration));
        Assert.AreEqual(168, run.Defender.sections[0].integrity);
        if (player != null) Object.Destroy(player.gameObject);
        yield return new ExitPlayMode();
    }
}
