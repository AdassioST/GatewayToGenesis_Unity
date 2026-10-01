using System;
using System.Collections.Generic;
using System.Linq;

public enum BattleRhythmMode { Execution, Abjuration, Heartbeat }

/// <summary>Timing, pattern and flow defaults are proposals; power/mitigation caps live in BattleMeasureTuning.</summary>
[Serializable]
public sealed class BattleRhythmTuning
{
    public float bpm = 90f, perfectMilliseconds = 35f, cleanMilliseconds = 100f;
    public float focusWindowPerTier = .1f, heartbeatAcceleration = .8f;
    [UnityEngine.Tooltip("Timing-window change per Tuning step from In Tune: Resonant and Sympathetic wider, Detuned narrower, Out of Tune narrowest.")]
    public float tuningWindowStep = .15f;
    public float resonanceWardPower = .1f;
    public int resonanceReachPerTiers = 2;
    [Obsolete("Fever now ignites and advances by flawless Measures, not phrase count.")]
    public int feverPhrases = 8;
}

[Serializable]
public sealed class BattleRhythmOptions
{
    /// <summary>Positive when device/input arrives late: subtract this from recorded DSP input times.</summary>
    public double latencyMilliseconds;
    public float windowScale = 1f;
    public bool assist;
    public BattleRhythmOptions Clone() => (BattleRhythmOptions)MemberwiseClone();
}

public sealed class BattleRhythmLane
{
    public long action { get; internal set; }
    public int voice { get; internal set; }
    public int note { get; internal set; }
    public int target { get; internal set; } = -1;
    public string name { get; internal set; }
    public bool playable { get; internal set; } = true;
    public bool inverse { get; internal set; }
    public BattleRhythmMode mode { get; internal set; }
    public SpellBinding binding { get; internal set; }
    public float bpm { get; internal set; }
    public double perfectWindow { get; internal set; }
    public double cleanWindow { get; internal set; }
    public IReadOnlyList<double> call { get; internal set; }
    public IReadOnlyList<double> echo { get; internal set; }
}

[Serializable]
public sealed class BattleRhythmInput
{
    public int lane;
    public double dspTime;
}

[Serializable]
public sealed class BattleRhythmResult
{
    public long action;
    public int voice, note, target, hits, missed, extra;
    public BattleExecution grade;
    public float rendition;
    public bool playable, assisted;
}

[Serializable]
public sealed class BattleRhythmRecord
{
    public int measure, beat;
    public bool attacker, abjuration, overbeat;
    public double start, end;
    public BattleRhythmOptions options;
    public List<BattleRhythmInput> inputs = new List<BattleRhythmInput>();
    public List<BattleRhythmResult> results = new List<BattleRhythmResult>();
}

/// <summary>One immutable audio-clock phrase and its recorded inputs. No Unity time, random rolls or sleeps.</summary>
public sealed class BattleRhythmChallenge
{
    public IReadOnlyList<BattleRhythmLane> lanes { get; }
    public double start { get; }
    public double end { get; }
    public bool abjuration { get; }
    public bool overbeat { get; }
    public BattleRhythmOptions options => configuration.Clone();
    private readonly BattleRhythmOptions configuration;
    private readonly List<BattleRhythmInput> recorded = new List<BattleRhythmInput>();
    private bool complete;
    public IReadOnlyList<BattleRhythmInput> inputs => recorded.Select(x => new BattleRhythmInput { lane = x.lane, dspTime = x.dspTime }).ToList().AsReadOnly();

    public BattleRhythmChallenge(double start, double duration, IEnumerable<BattleRhythmLane> lanes, BattleRhythmOptions options = null, bool abjuration = false, bool overbeat = false)
    {
        if (!Finite(start) || !Finite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        this.start = start; this.end = start + duration; this.abjuration = abjuration;
        this.lanes = lanes.ToList().AsReadOnly();
        this.overbeat = overbeat;
        configuration = (options ?? new BattleRhythmOptions()).Clone();
        if (!Finite(configuration.latencyMilliseconds)) configuration.latencyMilliseconds = 0;
        configuration.latencyMilliseconds = Math.Max(-250, Math.Min(250, configuration.latencyMilliseconds));
    }

    public string Input(int lane, double dspTime)
    {
        if (complete) return "The phrase has finished.";
        if (lane < 0 || lane >= lanes.Count || !lanes[lane].playable) return "This lane has no prepared defense or performer.";
        if (!Finite(dspTime)) return "An input needs a finite audio timestamp.";
        double at = dspTime - configuration.latencyMilliseconds / 1000d;
        if (at < start || at > end) return "The input is outside this phrase.";
        if (recorded.Any(x => x.lane == lane && x.dspTime >= dspTime)) return "Inputs on a lane must have increasing timestamps.";
        recorded.Add(new BattleRhythmInput { lane = lane, dspTime = dspTime });
        return null;
    }

    public bool CanComplete(double dspTime) => Finite(dspTime) && dspTime >= end + Math.Max(0, configuration.latencyMilliseconds / 1000d);

    public IReadOnlyList<BattleRhythmResult> Complete(double dspTime, float perfectPower = .25f)
    {
        if (!CanComplete(dspTime)) throw new InvalidOperationException("The response has not finished.");
        complete = true;
        return lanes.Select((lane, index) => Grade(lane, index, perfectPower)).ToList().AsReadOnly();
    }

    private BattleRhythmResult Grade(BattleRhythmLane lane, int index, float perfectPower)
    {
        var result = new BattleRhythmResult { action = lane.action, voice = lane.voice, note = lane.note, target = lane.target, playable = lane.playable,
            assisted = configuration.assist && lane.playable, grade = BattleExecution.Clean, rendition = 1f };
        if (!lane.playable) return result;
        if (configuration.assist) { result.hits = lane.echo.Count; return result; }
        var used = new HashSet<int>();
        double error = 0;
        bool perfect = true;
        foreach (var input in recorded.Where(x => x.lane == index))
        {
            double at = input.dspTime - configuration.latencyMilliseconds / 1000d;
            int closest = Enumerable.Range(0, lane.echo.Count).Where(i => !used.Contains(i)).OrderBy(i => Math.Abs(start + lane.echo[i] - at)).DefaultIfEmpty(-1).First();
            double delta = closest < 0 ? double.MaxValue : Math.Abs(start + lane.echo[closest] - at);
            if (delta > lane.cleanWindow + 1e-9) { result.extra++; continue; }
            used.Add(closest); result.hits++;
            perfect &= delta <= lane.perfectWindow + 1e-9;
            error += Math.Min(1, delta / Math.Max(.0001, lane.perfectWindow));
        }
        result.missed = lane.echo.Count - result.hits;
        if (result.missed > 0 || result.extra > 0) { result.grade = BattleExecution.Missed; result.rendition = .75f; }
        else if (perfect && result.hits > 0)
        {
            result.grade = BattleExecution.Perfect;
            result.rendition = 1f + Math.Max(0, Math.Min(.25f, perfectPower)) * (float)(1 - .5 * error / result.hits);
        }
        return result;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

public static class BattleRhythmLogic
{
    /// <summary>A call on the first half, echoed on the second. Larger stacks subdivide the phrase, across their Notes.</summary>
    public static BattleRhythmLane Lane(long action, int voice, int note, string name, SpellBinding binding, int complexity,
        double duration, BattleRhythmTuning tuning, BattleRhythmOptions options, int focus = 0, BattleRhythmMode mode = BattleRhythmMode.Execution, float heartbeatBpm = 0,
        int tuningSteps = 0)
    {
        int pulses = Math.Max(1, Math.Min(4, complexity));
        double subdivision = duration * .38 / pulses;
        if (mode == BattleRhythmMode.Heartbeat && heartbeatBpm > 0) subdivision = Math.Min(subdivision, 60d / heartbeatBpm / 4);
        double window = Math.Max(.5f, Math.Min(2f, options?.windowScale ?? 1f)) * (1 + Math.Max(0, focus) * Math.Max(0, tuning.focusWindowPerTier))
            * Math.Max(.25, 1 + Math.Max(-2, Math.Min(1, tuningSteps)) * Math.Max(0, tuning.tuningWindowStep));
        double clean = Math.Min(Math.Max(.005, tuning.cleanMilliseconds / 1000d * window), pulses == 1 ? duration * .3 : subdivision * .45);
        double perfect = Math.Min(clean, Math.Max(.001, tuning.perfectMilliseconds / 1000d * window));
        return new BattleRhythmLane { action = action, voice = voice, note = note, name = name, binding = binding, mode = mode, inverse = mode == BattleRhythmMode.Abjuration,
            bpm = heartbeatBpm > 0 ? heartbeatBpm : tuning.bpm, perfectWindow = perfect, cleanWindow = clean,
            call = Enumerable.Range(0, pulses).Select(i => duration * .08 + i * subdivision).ToList().AsReadOnly(),
            echo = Enumerable.Range(0, pulses).Select(i => duration * .58 + i * subdivision).ToList().AsReadOnly() };
    }

    public static float HeartbeatBpm(float scoreBpm, float composureShare, int beat, int beats, float acceleration = .8f) =>
        scoreBpm * (1 + Math.Max(0, acceleration) * (1 - Math.Max(0, Math.Min(1, composureShare))) * (1 - (float)Math.Max(0, beat) / Math.Max(1, beats - 1)));

    /// <summary>Calibration taps against known DSP pulses; robust median rejects an isolated late tap.</summary>
    public static double Calibrate(IEnumerable<double> pulses, IEnumerable<double> taps)
    {
        var errors = pulses.Zip(taps, (pulse, tap) => (tap - pulse) * 1000d).Where(e => !double.IsNaN(e) && !double.IsInfinity(e)).OrderBy(e => e).ToList();
        if (errors.Count == 0) return 0;
        double median = errors.Count % 2 == 1 ? errors[errors.Count / 2] : (errors[errors.Count / 2 - 1] + errors[errors.Count / 2]) / 2;
        return Math.Max(-250, Math.Min(250, median));
    }
}
