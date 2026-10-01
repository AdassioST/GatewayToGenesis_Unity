using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>DSP-driven performance of a committed Measure. Returns to its caller's Composition after Assessment.</summary>
public sealed class BattlePerformancePlayer : MonoBehaviour
{
    private BattleResolver.BattleRun run;
    private bool attacker;
    private Action<BattleResolver.BattleRun> completed;
    private RectTransform panel, rows;
    private TextMeshProUGUI heading, instruction, feedback;
    private TooltipTheme theme;
    private readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
    private readonly List<InputAction> actions = new List<InputAction>();
    private readonly List<GameObject> sounds = new List<GameObject>();
    private readonly Dictionary<(SpellBinding, bool), AudioClip> clips = new Dictionary<(SpellBinding, bool), AudioClip>();
    private bool returning;
    private double dspOffset;
    private double savedClockOffset;
    private BattleEncounter encounter;

    public static BattlePerformancePlayer Play(BattleEncounter encounter, Action<BattleResolver.BattleRun> completed)
    {
        if (encounter?.Run == null) throw new ArgumentException("Begin this encounter first.", nameof(encounter));
        var player = new GameObject("Measure Performance").AddComponent<BattlePerformancePlayer>();
        player.encounter = encounter; player.run = encounter.Run; player.attacker = encounter.State.playerAttacker; player.completed = completed;
        if (player.run.Rhythm != null)
        {
            player.savedClockOffset = AudioSettings.dspTime + .2 - encounter.State.clock;
            player.ShowPhrase(player.run.Rhythm);
        }
        else if (player.run.AwaitingAbjuration) player.ShowPhrase(encounter.Abjuration(AudioSettings.dspTime + .2));
        else player.StartBeat();
        return player;
    }

    public static BattlePerformancePlayer Play(BattleResolver.BattleRun run, bool attacker, Action<BattleResolver.BattleRun> completed)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        var player = new GameObject("Measure Performance").AddComponent<BattlePerformancePlayer>();
        player.run = run; player.attacker = attacker; player.completed = completed;
        if (run.MeasureBeat == 0) player.StartBeat();
        else if (run.AwaitingAbjuration) player.ShowPhrase(run.BeginAbjuration(AudioSettings.dspTime + .2));
        else player.StartBeat();
        return player;
    }

    private void Awake()
    {
        theme = CodeUI.Theme(nameof(BattlePerformancePlayer));
        var canvas = CodeUI.Canvas(transform, "Performance Canvas", 4, out _);
        if (UnityEngine.EventSystems.EventSystem.current == null)
            new GameObject("Performance EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
        var root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        CodeUI.Solid(root, "Backdrop", new Color(.015f, .02f, .04f, .97f), true);
        panel = CodeUI.Panel(root, "Performance", new Vector2(.5f, .5f), new Vector2(.5f, .5f));
        panel.sizeDelta = new Vector2(1200f, 840f);
        CodeUI.Plate(panel, theme, canvas.GetComponent<UnityEngine.UI.CanvasScaler>());
        heading = CodeUI.Label(panel, "Phase", "", 32f, theme.titleColor, FontStyles.Normal, theme);
        CodeUI.Place(heading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(40, -90), new Vector2(-40, -30));
        instruction = CodeUI.Label(panel, "Instructions", "", 24f, theme.bodyColor, FontStyles.Normal, theme);
        CodeUI.Place(instruction.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(40, -150), new Vector2(-40, -95));
        rows = CodeUI.Panel(panel, "Lanes", Vector2.zero, Vector2.one);
        rows.offsetMin = new Vector2(40, 125); rows.offsetMax = new Vector2(-40, -175);
        var layout = rows.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 10; layout.childControlHeight = true; layout.childForceExpandHeight = true;
        layout.childControlWidth = true; layout.childForceExpandWidth = true;
        feedback = CodeUI.Label(panel, "Feedback", "", 22f, theme.bodyColor, FontStyles.Normal, theme);
        CodeUI.Place(feedback.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(40, 40), new Vector2(-40, 115));
        var assist = CodeUI.Label(panel, "Options hint", "Rhythm options are in Options → Audio", 18f, theme.bodyColor, FontStyles.Normal, theme);
        CodeUI.Place(assist.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(40, 8), new Vector2(-40, 38));
    }

    private void StartBeat()
    {
        if (run.Over || run.Phase == BattlePhase.Assessment) { Return(); return; }
        double start = AudioSettings.dspTime - savedClockOffset + .2;
        ShowPhrase(encounter == null ? run.BeginRhythmBeat(attacker, start, GameSettings.RhythmOptions) : encounter.Rhythm(start, GameSettings.RhythmOptions));
    }

    private void ShowPhrase(BattleRhythmChallenge phrase)
    {
        ClearSounds();
        foreach (var action in actions) action.Dispose(); actions.Clear();
        foreach (var label in labels) label.gameObject.SetActive(false);
        heading.text = $"Humanize the Score | Measure {run.Measure} · {(phrase.abjuration ? "Abjuration" : phrase.overbeat ? "Overbeat" : "Beat " + (run.MeasureBeat + 1))} · {run.ScoreBpm:0} BPM";
        instruction.text = phrase.abjuration ? "Answer the counter-frequency. Uncovered lanes land in full." : "Listen to the call, then echo it on the same lane. Click a lane or press its number.";
        dspOffset = AudioSettings.dspTime - Time.realtimeSinceStartupAsDouble;
        for (int i = 0; i < phrase.lanes.Count; i++)
        {
            int index = i;
            var lane = phrase.lanes[i];
            if (i == labels.Count)
            {
                var label = CodeUI.TextButton(rows, "", () => Tap(index, AudioSettings.dspTime), theme, 24f);
                labels.Add(label);
            }
            labels[i].gameObject.SetActive(true);
            labels[i].GetComponent<UnityEngine.UI.Button>().interactable = lane.playable;
            if (i < 9 && lane.playable)
            {
                var input = new InputAction("Echo" + i, InputActionType.Button, "<Keyboard>/digit" + (i + 1));
                input.performed += context => Tap(index, context.time + dspOffset);
                input.Enable(); actions.Add(input);
            }
            foreach (var at in lane.call)
                if (phrase.start + at + savedClockOffset > AudioSettings.dspTime) ScheduleTone(lane, phrase.start + at + savedClockOffset);
        }
        if (phrase.lanes.Count == 0) instruction.text = "The other Tracks perform at Clean. Both armies continue this Beat.";
    }

    private void Tap(int lane, double time)
    {
        if (returning || run?.Rhythm == null) return;
        time -= savedClockOffset;
        string why = encounter == null ? run.RhythmInput(lane, time) : encounter.Input(lane, time);
        feedback.text = why ?? "Echo recorded.";
    }

    private void Update()
    {
        if (run == null || returning) return;
        CodeUI.FitModal(panel);
        var phrase = run.Rhythm;
        if (phrase == null) return;
        double now = AudioSettings.dspTime - savedClockOffset;
        if (encounter != null) encounter.State.clock = now;
        for (int i = 0; i < phrase.lanes.Count; i++)
        {
            var lane = phrase.lanes[i];
            var pattern = string.Join(" ", lane.echo.Select(at => now > phrase.start + at + lane.cleanWindow ? "·" : Math.Abs(now - phrase.start - at) <= lane.cleanWindow ? "[o]" : "o"));
            labels[i].text = $"{(i < 9 ? (i + 1).ToString() : "Tap")}  {lane.name}\n{(lane.playable ? now < phrase.start + lane.echo[0] - lane.cleanWindow ? "Listen" : "Echo: " + pattern : "UNCOVERED")}{(lane.mode == BattleRhythmMode.Heartbeat ? " · pulse " + lane.bpm.ToString("0") + " BPM" : "")}";
        }
        if (!phrase.CanComplete(now)) return;
        if (encounter == null) run.CompleteRhythm(now); else encounter.Complete(now);
        feedback.text = string.Join(" · ", run.Report.rhythm.Last().results.Where(x => x.playable).Select(x => x.assisted ? "Clean (assisted)" : x.grade.ToString()));
        if (run.AwaitingAbjuration) ShowPhrase(encounter == null ? run.BeginAbjuration(now + .3) : encounter.Abjuration(now + .3));
        else if (run.Over || run.Phase == BattlePhase.Assessment) Return();
        else if (encounter != null && run.Crises(attacker).Any(x => x.mindBroken || x.cadenzaReady)) Return();
        else StartBeat();
    }

    private void ScheduleTone(BattleRhythmLane lane, double at)
    {
        var key = (lane.binding, lane.inverse);
        if (!clips.TryGetValue(key, out var clip))
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            var samples = new float[(int)(rate * .04)];
            double frequency = 220 * Math.Pow(2, (int)lane.binding / 12d);
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)(Math.Sin(2 * Math.PI * frequency * i / rate) * .25 * (1d - (double)i / samples.Length) * (lane.inverse ? -1 : 1));
            clip = AudioClip.Create("Score " + lane.binding, samples.Length, 1, rate, false); clip.SetData(samples, 0); clips[key] = clip;
        }
        var host = new GameObject("Call"); host.transform.SetParent(transform, false); sounds.Add(host);
        var source = host.AddComponent<AudioSource>(); source.playOnAwake = false; source.clip = clip;
        source.volume = GameSettings.Volume(SoundChannel.Interface) / Math.Max(1, run.Rhythm?.lanes.Count ?? 1); source.spatialBlend = 0;
        source.PlayScheduled(at);
    }

    private void ClearSounds() { foreach (var sound in sounds) if (sound != null) Destroy(sound); sounds.Clear(); }

    private void Return()
    {
        returning = true; ClearSounds(); foreach (var action in actions) action.Disable();
        completed?.Invoke(run); Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ClearSounds(); foreach (var action in actions) action.Dispose();
        foreach (var clip in clips.Values) if (clip != null) Destroy(clip);
    }
}
