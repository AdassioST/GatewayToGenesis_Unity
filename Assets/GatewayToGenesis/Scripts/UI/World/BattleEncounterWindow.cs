using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Composition is held world time. Every control submits the same commands used by rehearsal and the automatic composer.</summary>
public sealed class BattleEncounterWindow : MonoBehaviour
{
    private static BattleEncounterWindow instance;
    private BattleEncounter encounter;
    private Action accepted;
    private TooltipTheme theme;
    private RectTransform book, body;
    private TextMeshProUGUI heading, feedback;
    private int core = -1, target = -1, voice, noteBeat = 4;
    private int overbeatCard = -1;
    private long overbeatWorking;
    private readonly Dictionary<int, int> minors = new Dictionary<int, int>();
    private readonly List<int> path = new List<int>(), steps = new List<int>();
    private bool performing;
    private BattlePerformancePlayer player;
    private string focusName;
    private int focusIndex = -1;
    public static bool IsOpen => instance != null;

    public static void Show(BattleEncounter encounter, Action accepted)
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = new GameObject("Battle Encounter").AddComponent<BattleEncounterWindow>();
        instance.encounter = encounter; instance.accepted = accepted; instance.Build(); instance.Refresh();
        if (encounter.Run?.Rhythm != null || encounter.Run?.AwaitingAbjuration == true) instance.Perform();
    }
    private void Build()
    {
        theme = CodeUI.Theme(nameof(BattleEncounterWindow));
        var canvas = CodeUI.Canvas(transform, "Encounter Canvas", 3, out var scaler);
        var root = CodeUI.Panel(canvas.transform, "Root", Vector2.zero, Vector2.one);
        CodeUI.Solid(root, "Backdrop", new Color(.02f, .03f, .04f, .98f), true);
        book = CodeUI.Panel(root, "Score", new Vector2(.5f, .5f), new Vector2(.5f, .5f)); book.sizeDelta = new Vector2(1700, 960);
        CodeUI.Plate(book, theme, scaler);
        heading = CodeUI.Label(book, "Heading", "", 29, theme.titleColor, FontStyles.Normal, theme);
        CodeUI.Place(heading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(30, -85), new Vector2(-30, -20));
        feedback = CodeUI.Label(book, "Feedback", "", 20, theme.bodyColor, FontStyles.Normal, theme);
        CodeUI.Place(feedback.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(30, 15), new Vector2(-30, 100));
        var area = CodeUI.Panel(book, "Scroll", Vector2.zero, Vector2.one); area.offsetMin = new Vector2(30, 110); area.offsetMax = new Vector2(-30, -95);
        CodeUI.Solid(area, "Surface", new Color(0, 0, 0, .2f), true);
        var scroll = area.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 45;
        var viewport = CodeUI.Panel(area, "Viewport", Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<RectMask2D>();
        body = CodeUI.Panel(viewport, "Controls", new Vector2(0, 1), Vector2.one); body.pivot = new Vector2(.5f, 1);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        layout.childControlWidth = true; layout.childForceExpandWidth = true;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = body;
    }
    private RectTransform Row(float height = 56)
    {
        var row = CodeUI.Panel(body, "Row", Vector2.zero, Vector2.one);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 8; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        layout.childControlHeight = true; layout.childForceExpandHeight = true;
        return row;
    }
    private void Label(string text, float height = 55)
    { var label = CodeUI.Label(Row(height), "Description", text, 20, theme.bodyColor, FontStyles.Normal, theme); label.textWrappingMode = TextWrappingModes.Normal; }
    private void Button(Transform row, string text, Action click)
    { CodeUI.TextButton(row, text, click, theme, 20); }
    private void Help(string title, string text)
    {
        var row = Row();
        var help = IconActionButton.Create(row, title, PixelIcon.Help, () => TooltipSystemLogic.Instance?.ShowFocused(row.GetComponentInChildren<TooltipTrigger>()), text, theme);
        help.gameObject.AddComponent<LayoutElement>().preferredWidth = 56;
    }
    private void Clear()
    {
        var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if (selected != null && selected.transform.IsChildOf(body))
        {
            focusName = selected.name;
            focusIndex = Array.IndexOf(body.GetComponentsInChildren<Button>(), selected.GetComponent<Button>());
        }
        foreach (Transform child in body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    }
    private void Message(string why) { feedback.text = why ?? "Score updated."; Refresh(); }
    private BattleCommand Command(BattleCommandKind kind) => new BattleCommand { kind = kind, attacker = encounter.State.playerAttacker };
    private void Refresh()
    {
        if (performing) return;
        Clear(); bool side = encounter.State.playerAttacker;
        float chance = BattleVerdicts.WinChance(encounter.State.forecast, side);
        heading.text = $"{encounter.State.original.attacker.name} vs {encounter.State.original.defender.name} | {BattleLossBurden.Forecast(chance)} ({chance:P0})";
        Label("Objective: " + (encounter.State.original.objective ?? "Hold the field"), 42);
        var run = encounter.Run;
        if (run == null)
        {
            var row = Row(); Button(row, "Humanize the Score", () => { encounter.BeginMeasure(); ResetSelection(); Refresh(); });
            TooltipTrigger.Ensure(row.GetChild(0).gameObject).SetCustom("Humanize the Score", "Turn the written score into a living performance. Compose your Tracks, then play their rhythm: timing, expression and response shape the battle beyond its forecast. Rhythm assistance, wider timing windows and latency calibration are available in Options / Audio.");
            if (encounter.CanAutoResolve) Button(row, "Resolve automatically", () => Finish(encounter.AutoResolve()));
            else Label("This encounter requires Humanize the Score.", 42);
            Help("Performance & premonitions", $"The forecast reads the written score; your performance brings it to life, like human expression beyond MIDI. Prepared premonitions: {WorldSystem.Instance?.PreparedPremonitions.Remaining ?? 0}. A lost prepared attempt restores this force to its original state.");
            return;
        }
        if (run.Over) { Button(Row(), "Accept battle result", () => Finish(encounter.FinishAttempt())); return; }
        bool compose = run.MeasureBeat == 0;
        Label($"Measure {run.Measure} | {run.Phase} | Next Beat {Math.Min(4, run.MeasureBeat + 1)} | Bandwidth {run.Bandwidth(side)} | Seer Track {run.SeerVoice(side)}, Clarity {run.Clarity(side)}");
        int fever = run.TempoPercentage(side), pips = run.TempoStreak(side);
        string marking = fever > 0
            ? $"{run.TempoMovement(side)} {fever}%" + (run.TempoFaltering(side) ? " | FALTERING: a second imperfect Measure breaks the Fever" : "") +
              (run.ClimaxTier(side) != BattleClimaxTier.None ? $" | Climax allowance: {run.ClimaxTier(side)}" : "")
            : $"Ordinary Measure | Fever pips {pips}/{BattleTempoFever.Pips}: four consecutive Perfect sequences ignite Tempo Crescendo";
        var layers = run.MusicLayers(side);
        Label(fever > 0 ? $"{run.TempoMovement(side)} {fever}%" + (run.TempoFaltering(side) ? " | Faltering" : "") : $"Tempo {pips}/{BattleTempoFever.Pips}", 42);
        Help("Tempo & expression", marking + (string.IsNullOrEmpty(run.TempoChange(side)) ? "" : $" | Last Assessment: {run.TempoChange(side)}") +
            " | Each Interference weighs 1 + the Fever; friendly Collapse amplifies with it." + (layers.Count > 0 ? " | Score: " + string.Join(", ", layers) : ""));
        if (run.ResolutionComposed(side)) Label("Resolution composed: committing declares it, and Tempo Fever ends at this Measure's Assessment, resolved or collapsed.");
        var actionRow = Row(); Button(actionRow, compose ? "Commit and perform" : "Continue performance", Perform);
        if (compose) Button(actionRow, "Redraw Bell (Beats 1–2 Rest)", () => { Message(encounter.Command(Command(BattleCommandKind.Redraw))); ResetSelection(); Refresh(); });
        var yours = side ? run.Attacker : run.Defender;
        var enemy = side ? run.Defender : run.Attacker;
        var unitRow = Row();
        foreach (var track in run.Score(side))
        {
            int at = track.voice;
            Button(unitRow, (voice == at ? "[" : "") + track.name + (track.spotlit ? " ★" : "") + (track.tuning == BattleTuningState.Sympathetic ? " (Sympathetic)" : "") + (voice == at ? "]" : ""),
                () => { voice = at; path.Clear(); steps.Clear(); Refresh(); });
        }
        foreach (var track in run.Score(side)) Label($"{track.name}: " + string.Join(" | ", Enumerable.Range(1, 4).Select(b => $"{b}: {track.beats[b]}{(track.steps[b] >= 0 ? " → hex " + track.steps[b] : "")}")), 40);
        foreach (var track in run.Score(side).Where(t => run.HasOverbeat(side, t.voice))) Label(track.name + " Overbeat: " + run.OverbeatDescription(side, track.voice), 40);
        var selected = voice >= 0 && voice < yours.sections.Count ? yours.sections[voice] : null;
        if (selected != null)
        {
            Label($"{selected.name}: Integrity {selected.integrity:0}/{selected.maxIntegrity:0}, Battle Composure {selected.composure:0}/{selected.maxComposure:0}" +
                (selected.deathKnell ? " | DEATH KNELL" : "") + (selected.eliteCheckmate ? " | CHECKMATE" : selected.eliteCheck ? " | CHECK" : ""));
            var row = Row(); if (compose) Button(row, "Spotlight selected Track", () => Message(encounter.Spotlight(voice)));
            if (compose && run.Measure == 1 && selected.leader != null) Button(row, "Designate Seer", () => { var cmd = Command(BattleCommandKind.Seer); cmd.section = voice; Message(encounter.Command(cmd)); });
            if (selected.eliteRole != BattleEliteRole.None) Button(row, "Evacuate selected elite", () => Message(encounter.Evacuate(voice)));
            if (compose && run.HasOverbeat(side, voice)) Button(row, "Last selected hex as Overbeat Step", () =>
            { if (path.Count == 0) { Message("Select a destination hex first."); return; } var cmd = Command(BattleCommandKind.OverbeatStep); cmd.section = voice; cmd.hex = path.Last(); Message(encounter.Command(cmd)); });
            if (compose) foreach (BattleStandingOrder order in Enum.GetValues(typeof(BattleStandingOrder)))
            { var captured = order; Button(row, order.ToString(), () => { var cmd = Command(BattleCommandKind.Order); cmd.section = voice; cmd.order = captured; Message(encounter.Command(cmd)); }); }
        }
        foreach (BattleLane lane in Enum.GetValues(typeof(BattleLane)))
        {
            var row = Row(80);
            foreach (var hex in BattleHexLayout.Hexes.Where(h => h.Lane == lane))
            {
                int at = hex.Id;
                var occupants = run.Attacker.sections.Concat(run.Defender.sections).Where(x => x.Standing && x.battleHex == at).Select(x => x.name);
                string ground = run.Spatial.Terrain.FirstOrDefault(x => x.hex == at)?.blocked == true ? " BLOCKED" : "";
                Button(row, $"{at} {hex.Territory}{ground}\n{string.Join(", ", occupants)}", () =>
                {
                    if (!compose) { feedback.text = "Geometry changes on the shared Beats."; return; }
                    path.Add(at); steps.Add(noteBeat); Refresh();
                });
            }
        }
        var beatRow = Row(); foreach (int beat in Enumerable.Range(1, 4)) { int b = beat; Button(beatRow, (noteBeat == b ? "[" : "") + "Beat " + b + (noteBeat == b ? "]" : ""), () => { noteBeat = b; Refresh(); }); }
        if (compose && selected != null)
        {
            Label("Path: " + string.Join(" → ", path.Select((h, i) => $"{h} (Beat {steps[i]})")) + ". Select each hex with its arrival Beat; future Steps use the following Measure.", 45);
            var row = Row(); Button(row, "Write path (earliest legal Beats)", () => WritePath(false)); Button(row, "Write timed path", () => WritePath(true)); Button(row, "Clear path selection", () => { path.Clear(); steps.Clear(); Refresh(); });
        }
        var hand = run.Hand(side);
        for (int i = 0; i < hand.Count; i++)
        {
            int at = i; var card = hand[i];
            Button(Row(45), $"{(core == i || minors.ContainsKey(i) ? "✓ " : "")}{card.Name} | {card.card.noteRole} | {card.card.purpose}" + (minors.ContainsKey(i) ? $" | Minor Beat {minors[i]}" : ""), () =>
            {
                overbeatCard = at;
                if (card.card.noteRole == BattleNoteRole.Minor) { if (minors.ContainsKey(at)) minors.Remove(at); else minors[at] = noteBeat; }
                else { core = at; target = -1; }
                Refresh();
            });
        }
        if (compose && overbeatCard >= 0 && overbeatCard < hand.Count && run.TempoPercentage(side) > 0)
            Button(Row(), "Write selected card on Overbeat: " + hand[overbeatCard].Name, () =>
            { var cmd = Command(BattleCommandKind.OverbeatCard); cmd.handIndex = overbeatCard; cmd.target = target; cmd.chord = overbeatWorking; var why = encounter.Command(cmd); if (why == null) ResetSelection(); Message(why); });
        if (compose && overbeatCard >= 0 && overbeatCard < hand.Count && run.ClimaxTier(side) != BattleClimaxTier.None)
        {
            // Spending the Climax Overbeat's allowance declares the Resolution.
            var climaxRow = Row();
            Button(climaxRow, "Climax Major (Resolve): " + hand[overbeatCard].Name, () => ClimaxMajor(false));
            if (run.ClimaxTier(side) >= BattleClimaxTier.FusedFinale) Button(climaxRow, "Fused Finale with its Track's Chord", () => ClimaxMajor(true));
        }
        if (compose && selected != null && run.ClimaxTier(side) == BattleClimaxTier.GrandResolution)
            Button(Row(), "Declare the Grand Resolution Chord from " + selected.name, () =>
            { var cmd = Command(BattleCommandKind.GrandResolution); cmd.section = voice; Message(encounter.Command(cmd)); });
        if (core >= 0 && core < hand.Count)
        {
            var card = hand[core];
            Label(card.card.Summary, 80);
            var row = Row(); Button(row, "Default legal target", () => { target = -1; Refresh(); });
            foreach (var unit in run.LegalTargets(side, core, noteBeat))
            {
                int index = yours.sections.Contains(unit) ? yours.sections.IndexOf(unit) : enemy.sections.IndexOf(unit);
                Button(row, (target == index ? "✓ " : "") + unit.name, () => { target = index; Refresh(); });
            }
            var cmd = SelectedCommand();
            if (compose)
            {
                var buttons = Row(); Button(buttons, "Rehearse selection", () => { var preview = run.Preview(new[] { cmd, Command(BattleCommandKind.Wait) }); feedback.text = preview.failures.Count > 0 ? string.Join("\n", preview.failures) : string.Join(" | ", preview.after.intents.Where(x => x.attacker == side).Select(x => x.name + " in " + x.Remaining(run.Beat) + " Beats")); });
                Button(buttons, "Write Core / ordered Minors", () => { string why = encounter.Command(cmd); if (why == null) ResetSelection(); Message(why); });
            }
            if (card.card.corrupted || card.card.cathartic) Button(Row(), "Perform crisis card on selected upcoming Beat", () => { string why = encounter.Crisis(core, noteBeat); if (why == null) ResetSelection(); Message(why); });
        }
        foreach (var crisis in run.Crises(side)) Label($"{crisis.character}: {crisis.expression} | resolved this Measure {crisis.resolvedThisMeasure}/2 | " +
            (crisis.cadenzaReady ? "Cadenza ready" : crisis.mindBroken ? "Corrupted hand" : "Coherent") + (crisis.steadied ? " | Steadied by Co-Regulation" : ""), 40);
        if (run.ConductorMaladaptation(side) != BattleConductorMaladaptation.None)
            Label($"Conductor in Mind Break: the army inherits {run.ConductorMaladaptation(side)} behavior on the Tracks it no longer composes. A Corroded Gambit brings the carrier signal back.", 40);
        foreach (var intent in run.IntentsFor(side)) Label($"{(intent.attacker == side ? "Your" : "Enemy")} {intent.name}: " +
            (intent.dueBeat < 0 ? "Timing hidden" : intent.Remaining(run.Beat) + " Beats") + (intent.unverified ? " [unverified]" : $", target {intent.target}") +
            (intent.holdLimit > 0 ? $", Hold {intent.sounding}/{intent.holdLimit}, Ward requirement {intent.minimumWardBeats}" : "") +
            (intent.damageCertain ? $", Integrity {intent.expectedIntegrity:0}, Composure {intent.expectedComposure:0}" : "") + (intent.confirmed ? " [confirmed]" : ""), 40);
        foreach (var reaction in run.PreparedReactions.Where(r => r.attacker == side || run.IntentClarity(side, r.attacker, r.voice) >= 2))
            Label($"{(reaction.attacker == side ? "Your" : "Enemy")} Reaction: {reaction.card}, {reaction.rule.trigger}, {reaction.remainingUses} charges, until Beat {reaction.expires}", 40);
        foreach (var weave in run.PreparedWeaves.Where(x => x.attacker == side))
        {
            long id = weave.action; var row = Row();
            if (compose && run.TempoPercentage(side) > 0) Button(row, "Overbeat Minor destination: " + id, () => { overbeatWorking = id; Message("Selected working " + id); });
            Button(row, "Retract working " + id, () => { var cmd = Command(BattleCommandKind.Retract); cmd.chord = id; Message(encounter.Command(cmd)); });
            if (weave.suspended)
            {
                Button(row, "Ground suspended working " + id, () => { var cmd = Command(BattleCommandKind.Ground); cmd.chord = id; Message(encounter.Command(cmd)); });
                Button(row, "Stabilize on selected Beat / target", () => { var cmd = Command(BattleCommandKind.Stabilize); cmd.chord = id; cmd.target = target; cmd.beat = noteBeat; Message(encounter.Command(cmd)); });
            }
        }
    }
    private BattleCommand SelectedCommand()
    {
        var cmd = Command(minors.Count == 0 ? BattleCommandKind.Card : BattleCommandKind.Chord); cmd.handIndex = core; cmd.target = target; cmd.beat = noteBeat;
        cmd.minors = minors.Keys.ToList(); cmd.minorBeats = cmd.minors.Select(i => minors[i]).ToList(); return cmd;
    }
    private void ClimaxMajor(bool fuse)
    {
        var cmd = Command(BattleCommandKind.ClimaxMajor); cmd.handIndex = overbeatCard; cmd.target = target; cmd.fuse = fuse;
        var why = encounter.Command(cmd); if (why == null) ResetSelection(); Message(why);
    }
    private void WritePath(bool timed)
    { var cmd = Command(BattleCommandKind.Path); cmd.section = voice; cmd.path = path.ToList(); if (timed) cmd.stepBeats = steps.ToList(); Message(encounter.Command(cmd)); }
    private void ResetSelection() { core = target = overbeatCard = -1; overbeatWorking = 0; minors.Clear(); path.Clear(); steps.Clear(); }
    private void Perform()
    {
        performing = true; book.gameObject.SetActive(false);
        player = BattlePerformancePlayer.Play(encounter, run =>
        {
            performing = false; book.gameObject.SetActive(true); ResetSelection();
            if (run.Over) Finish(encounter.FinishAttempt());
            else { if (run.Phase == BattlePhase.Assessment) encounter.BeginMeasure(); Refresh(); }
        });
    }
    private void Finish(string why)
    {
        if (!encounter.State.accepted) { WorldSystem.Instance?.ReportPremonitionExhaustion(); feedback.text = why; ResetSelection(); Refresh(); return; }
        accepted?.Invoke(); Destroy(gameObject);
    }
    private void LateUpdate()
    {
        if (performing || book == null) return;
        CodeUI.FitModal(book);
        if (focusIndex < 0 || UnityEngine.EventSystems.EventSystem.current == null) return;
        var buttons = body.GetComponentsInChildren<Button>();
        if (buttons.Length > 0)
        {
            var same = buttons.FirstOrDefault(b => b.name == focusName) ?? buttons[Math.Min(focusIndex, buttons.Length - 1)];
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(same.gameObject);
        }
        focusIndex = -1;
    }
    private void OnDestroy() { if (player != null) Destroy(player.gameObject); if (instance == this) instance = null; }
}
