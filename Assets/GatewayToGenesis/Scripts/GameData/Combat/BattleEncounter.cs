using System;
using System.Collections.Generic;
using System.Linq;

public enum BattleEncounterStep { Begin, Command, Crisis, Evacuate, Rhythm, Input, Complete, Abjuration, Spotlight }
[Serializable]
public sealed class BattleEncounterAction
{
    public BattleEncounterStep step;
    [NonSerialized] public BattleCommand command;
    public int index, beat;
    public double time;
    public BattleRhythmOptions options;
}
[Serializable]
public sealed class BattleEncounterState
{
    public string id;
    public int attackerUnit, defenderUnit;
    public bool playerAttacker, started, accepted, wasPaused;
    public double clock;
    [NonSerialized] public BattleSetup original;
    [NonSerialized] public BattleForecast forecast;
    public List<BattleEncounterAction> history = new List<BattleEncounterAction>();
}

/// <summary>Campaign effects wait for acceptance. A premonition loses only its charge, then replays untouched pre-contact input.</summary>
public sealed class BattleEncounter
{
    public BattleEncounterState State { get; }
    public BattleResolver.BattleRun Run { get; private set; }
    public BattleReport Report { get; private set; }
    public BattleSide Attacker => Run?.Attacker ?? working?.attacker;
    public BattleSide Defender => Run?.Defender ?? working?.defender;
    public bool Retried { get; private set; }
    public bool CanAutoResolve => !State.original.RequiresManual;
    private readonly CombatSettings settings;
    private readonly BattlePremonitions retries;
    private BattleSetup working;
    private bool replaying;
    public BattleEncounter(BattleEncounterState state, CombatSettings settings, BattlePremonitions retries)
    {
        State = state ?? throw new ArgumentNullException(nameof(state)); this.settings = settings ?? new CombatSettings(); this.retries = retries ?? new BattlePremonitions();
        if (state.forecast == null) state.forecast = BattleResolver.Forecast(state.original, this.settings, 12);
        if (state.started) RestoreHistory();
    }
    public void StartManual()
    {
        if (Run != null) return;
        if (State.accepted) throw new InvalidOperationException("This encounter has already been accepted.");
        Retried = false;
        State.started = true; working = State.original.Clone(State.original.seed);
        working.attacker.manual = State.playerAttacker; working.defender.manual = !State.playerAttacker;
        Run = BattleResolver.Begin(working, settings, 0); Run.Prediction = State.forecast;
    }
    private void Record(BattleEncounterAction action) { if (!replaying) State.history.Add(action); }
    public void BeginMeasure() { StartManual(); Run.BeginMeasure(); Record(new BattleEncounterAction { step = BattleEncounterStep.Begin }); }
    public string Command(BattleCommand command)
    {
        if (Run == null || command == null) return "Begin the Measure first.";
        if (command.attacker != State.playerAttacker) return "Compose your own army's Score.";
        string why;
        switch (command.kind)
        {
            case BattleCommandKind.Card: why = Run.CommitCard(command.attacker, command.handIndex, command.target, command.rendition, command.beat); break;
            case BattleCommandKind.Chord: why = Run.CommitChord(command.attacker, command.handIndex, command.minors, command.target, command.rendition, command.beat, command.minorBeats); break;
            case BattleCommandKind.Path: why = Run.ComposePath(command.attacker, command.section, command.path, command.stepBeats.Count > 0 ? command.stepBeats : null); break;
            case BattleCommandKind.Movement: why = Run.CommitMovement(command.attacker, command.section, command.hex); break;
            case BattleCommandKind.Order: why = Run.Order(command.attacker, command.section, command.order); break;
            case BattleCommandKind.Redraw: why = Run.Redraw(command.attacker); break;
            case BattleCommandKind.Retract: why = Run.Retract(command.attacker, command.chord); break;
            case BattleCommandKind.Stabilize: why = Run.Stabilize(command.attacker, command.chord, command.target, command.beat); break;
            case BattleCommandKind.Ground: why = Run.Ground(command.attacker, command.chord); break;
            case BattleCommandKind.Seer: why = Run.DesignateSeer(command.attacker, command.section); break;
            case BattleCommandKind.OverbeatCard: why = Run.CommitOverbeat(command.attacker, command.handIndex, command.target, command.chord); break;
            case BattleCommandKind.OverbeatStep: why = Run.CommitOverbeatStep(command.attacker, command.section, command.hex); break;
            case BattleCommandKind.ClimaxMajor: why = Run.CommitClimaxMajor(command.attacker, command.handIndex, command.target, command.fuse); break;
            case BattleCommandKind.GrandResolution: why = Run.CommitGrandResolution(command.attacker, command.section); break;
            default: why = Run.Perform(command.beats); break;
        }
        if (why == null) Record(new BattleEncounterAction { step = BattleEncounterStep.Command, command = Copy(command) }); return why;
    }
    private static BattleCommand Copy(BattleCommand c) => new BattleCommand { kind = c.kind, attacker = c.attacker, handIndex = c.handIndex, fuse = c.fuse,
        section = c.section, target = c.target, hex = c.hex, beats = c.beats, beat = c.beat, chord = c.chord, rendition = c.rendition, order = c.order,
        minors = new List<int>(c.minors), path = new List<int>(c.path), stepBeats = new List<int>(c.stepBeats), minorBeats = new List<int>(c.minorBeats) };
    public string Spotlight(int section)
    { string why = Run.Spotlight(State.playerAttacker, section); if (why == null) Record(new BattleEncounterAction { step = BattleEncounterStep.Spotlight, index = section }); return why; }
    public string Crisis(int hand, int beat)
    { string why = Run.CommitCrisisCard(State.playerAttacker, hand, beat); if (why == null) Record(new BattleEncounterAction { step = BattleEncounterStep.Crisis, index = hand, beat = beat }); return why; }
    public string Evacuate(int section)
    { string why = Run.Evacuate(State.playerAttacker, section); if (why == null) Record(new BattleEncounterAction { step = BattleEncounterStep.Evacuate, index = section }); return why; }
    public BattleRhythmChallenge Rhythm(double start, BattleRhythmOptions options)
    { var frame = Run.BeginRhythmBeat(State.playerAttacker, start, options); State.clock = start; Record(new BattleEncounterAction { step = BattleEncounterStep.Rhythm, time = start, options = options?.Clone() }); return frame; }
    public BattleRhythmChallenge Abjuration(double start)
    { var frame = Run.BeginAbjuration(start); State.clock = start; Record(new BattleEncounterAction { step = BattleEncounterStep.Abjuration, time = start }); return frame; }
    public string Input(int lane, double time)
    { string why = Run.RhythmInput(lane, time); if (why == null) { State.clock = time; Record(new BattleEncounterAction { step = BattleEncounterStep.Input, index = lane, time = time }); } return why; }
    public string Complete(double time)
    { string why = Run.CompleteRhythm(time); if (why == null) { State.clock = time; Record(new BattleEncounterAction { step = BattleEncounterStep.Complete, time = time }); } return why; }
    public string AutoResolve()
    {
        if (!CanAutoResolve) return "This encounter requires Humanize the Score.";
        if (State.accepted) return "This encounter has already been accepted.";
        if (State.started) return "This battle has already begun.";
        working = State.original.Clone(State.original.seed); Report = BattleResolver.Resolve(working, settings); return AssessAttempt();
    }
    public string FinishAttempt()
    {
        if (Run?.Over != true) return "Finish the battle before applying its aftermath.";
        if (State.accepted) return "This encounter has already been accepted.";
        Report = Run.Finish(); return AssessAttempt();
    }
    private string AssessAttempt()
    {
        bool lost = State.playerAttacker ? Report.winner <= 0 : Report.winner > 0;
        if (lost && retries.ConsumeLoss(State.id))
        {
            Retried = true; State.history.Clear(); State.started = false; Run = null; Report = null;
            return "The lost battle was a premonition. Its prepared charge was consumed; the force and campaign remain unchanged.";
        }
        State.accepted = true; Retried = false; return null;
    }
    private void RestoreHistory()
    {
        var history = State.history.ToList(); double clock = State.clock; replaying = true;
        try
        {
            StartManual();
            foreach (var a in history)
            {
                string why = null;
                switch (a.step)
                {
                    case BattleEncounterStep.Begin: BeginMeasure(); break;
                    case BattleEncounterStep.Command: why = Command(a.command); break;
                    case BattleEncounterStep.Crisis: why = Crisis(a.index, a.beat); break;
                    case BattleEncounterStep.Evacuate: why = Evacuate(a.index); break;
                    case BattleEncounterStep.Spotlight: why = Spotlight(a.index); break;
                    case BattleEncounterStep.Rhythm: Rhythm(a.time, a.options); break;
                    case BattleEncounterStep.Abjuration: Abjuration(a.time); break;
                    case BattleEncounterStep.Input: why = Input(a.index, a.time); break;
                    case BattleEncounterStep.Complete: why = Complete(a.time); break;
                }
                if (why != null) throw new InvalidOperationException("Saved battle replay failed: " + why);
            }
        }
        finally { replaying = false; State.clock = clock; }
    }
}
