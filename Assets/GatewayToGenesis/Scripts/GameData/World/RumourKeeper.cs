using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Keeps what the people have heard (<see cref="WorldRumours"/>), saved whole (<see cref="_state"/> in
/// GameSnapshot.Schema): the world's first rumours once it exists, one more each Echo from travellers, one from each
/// expedition that completes a journey, and the finding of each (Era Score, a notice naming what it was). Tuning is
/// <see cref="RumourTuning"/>'s code defaults (every number a proposal). Created by <see cref="GenesisLoop"/>.
/// </summary>
public class RumourKeeper : SingletonBehaviour<RumourKeeper>
{
    private const LogChannel Log = LogChannel.World;

    // Saved (GameSnapshot.Schema).
    private RumourState _state = new RumourState();

    private static readonly RumourTuning Defaults = new RumourTuning();
    private TimeSystemLogic _time;
    private WorldSystem _world;

    public RumourState State => _state;
    public static RumourTuning Tuning => Defaults;

    /// <summary>A rumour was heard or confirmed (the map redraws the areas it points at).</summary>
    public static event Action Changed;

    /// <summary>The state the map, the Bestiary and GameValues read; null with no keeper in the scene.</summary>
    public static RumourState Current => Instance != null ? Instance._state : null;

    protected override void OnSingletonDestroy()
    {
        if (_time != null) _time.OnEchoChange -= OnEcho;
        if (_world != null) _world.Changed -= OnWorldChanged;
    }

    private void Start()
    {
        _world = WorldSystem.Instance;
        if (_world != null) _world.Changed += OnWorldChanged;
        TimeSystemLogic.WhenReady(this, time =>
        {
            _time = time;
            _time.OnEchoChange += OnEcho;
        });
    }

    private bool Ready => !SaveSession.Restoring && _world != null && _world.Map != null && _world.Settings != null && _world.Map.Get(_world.Map.Capital) != null;

    // The Seventh counted from the calendar's start (a rumour remembers when it was heard).
    private static int Now
    {
        get
        {
            var t = TimeSystemLogic.Instance;
            if (t == null) return 0;
            return (((t.CurrentCycle - 1) * TimeSystemLogic.EchoesPerCycle + t.CurrentEcho - 1) * TimeSystemLogic.PhasesPerEcho + t.CurrentPhase - 1) * TimeSystemLogic.SeventhsPerPhase + t.CurrentSeventh - 1;
        }
    }

    // The world exists (the first rumours), an expedition came home (one more), something was found (confirmed).
    private void OnWorldChanged()
    {
        if (!Ready) return;
        bool changed = Confirm();
        if (!_state.started)
        {
            _state.started = true;
            _state.journeys = _world.JourneysCompleted;
            changed |= Hear(Tuning.atStart, "The people tell of what lies beyond the fog.");
        }
        int journeys = _world.JourneysCompleted;
        if (journeys > _state.journeys)
        {
            int times = journeys - _state.journeys;
            _state.journeys = journeys;
            changed |= Hear(Tuning.perJourney * times, "An expedition brought word home.");
        }
        if (changed) Changed?.Invoke();
    }

    private void OnEcho(int echo)
    {
        if (!Ready) return;
        bool changed = Confirm();
        changed |= Hear(Tuning.perEcho, "Travellers came with the turning of the Echo.");
        if (changed) Changed?.Invoke();
    }

    private bool Hear(int count, string why)
    {
        bool any = false;
        for (int i = 0; i < count; i++)
        {
            var r = WorldRumours.Hear(_state, _world.Map, _world.Settings.generation, Tuning, Now);
            if (r == null) break;
            any = true;
            GameLog.Event($"Rumour heard ({r.kind}, {r.key}): {r.text}", Log);
            NotificationFeed.Push("A rumour", $"{r.text} {TooltipText.Muted(why)}", NotificationFeed.Topic.Discovery, WorldView.ShowWorld, "rumour:" + r.key);
        }
        return any;
    }

    private bool Confirm()
    {
        var found = WorldRumours.Confirm(_state, _world.Map, _world.Settings.generation);
        foreach (var r in found)
        {
            string what = string.IsNullOrEmpty(r.truth) ? "what it spoke of" : r.truth;
            if (Tuning.eraConfirmed > 0) AgeProgression.Award(Tuning.eraConfirmed, $"Followed a rumour to {what}");
            GameLog.Event($"Rumour confirmed ({r.key}): {what}", Log);
            NotificationFeed.Push("The rumour was true", $"What travellers spoke of {r.direction}: {what}.", NotificationFeed.Topic.Discovery, WorldView.ShowWorld, "rumour-true:" + r.key);
        }
        return found.Count > 0;
    }

    /// <summary>GameValues "rumours": heard (no target), "open", "confirmed", or confirmed of one kind ("confirmed:landmark").</summary>
    public static float Value(string target)
    {
        var s = Current;
        if (s == null) return 0f;
        string t = (target ?? string.Empty).Trim();
        if (t.Length == 0 || string.Equals(t, "heard", StringComparison.OrdinalIgnoreCase)) return s.heard.Count;
        if (string.Equals(t, "open", StringComparison.OrdinalIgnoreCase)) return WorldRumours.Open(s).Count;
        if (string.Equals(t, "confirmed", StringComparison.OrdinalIgnoreCase)) return WorldRumours.Confirmed(s);
        if (t.StartsWith("confirmed:", StringComparison.OrdinalIgnoreCase)) return WorldRumours.Confirmed(s, t.Substring(10));
        return 0f;
    }
}
