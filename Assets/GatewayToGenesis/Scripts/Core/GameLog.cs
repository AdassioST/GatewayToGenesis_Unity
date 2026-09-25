using System;
using UnityEngine;

/// <summary>
/// What a log message is about. Each system logs to one channel (its <c>const LogChannel Log</c>); channels are
/// switched on in the scene's <see cref="GameLoggingSystem"/> (one multi-select field). New system: pick an
/// existing channel, or append a value here (next free bit).
/// </summary>
[Flags]
public enum LogChannel
{
    None = 0,
    /// <summary>Resources, buildings, research, clicking, production rates.</summary>
    Units = 1 << 0,
    /// <summary>Population, housing, food demand, villagers.</summary>
    Population = 1 << 1,
    Time = 1 << 2,
    /// <summary>Weather selection and weather effects.</summary>
    Weather = 1 << 3,
    /// <summary>Lighting, sky and particles (frequent).</summary>
    WeatherVisuals = 1 << 4,
    /// <summary>Asset catalogs and start-up content validation.</summary>
    Content = 1 << 5,
    /// <summary>Event triggers, consequences, Ink stories.</summary>
    Events = 1 << 6,
    /// <summary>Event and chorus screens.</summary>
    EventScreens = 1 << 7,
    Stats = 1 << 8,
    /// <summary>Government compass, council seats and legends.</summary>
    Council = 1 << 9,
    Civics = 1 << 10,
    /// <summary>Every effect applied or removed through EffectRouter.</summary>
    Effects = 1 << 11,
    /// <summary>Government tab, seat and legend displays.</summary>
    GovernmentUI = 1 << 12,
    /// <summary>Tabs, hotkeys and tooltips.</summary>
    UI = 1 << 13,
}

/// <summary>
/// The one logging entry point for gameplay code. <see cref="Event"/> prints only when its channel is enabled;
/// <see cref="Warning"/> and <see cref="Error"/> always print. Safe from any code (static, Awake, tests):
/// with no <see cref="GameLoggingSystem"/> in the scene every channel is off.
/// </summary>
public static class GameLog
{
    /// <summary>Channels whose events print. Set by <see cref="GameLoggingSystem"/>; everything is off without one.</summary>
    public static LogChannel Enabled { get; set; }

    // Statics survive between play sessions when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlaySession() => Enabled = LogChannel.None;

    /// <summary>True when events on <paramref name="channel"/> print. Guard costly string building with it.</summary>
    public static bool IsEnabled(LogChannel channel) => (Enabled & channel) != 0;

    /// <summary>Diagnostic message, printed only when the channel is enabled.</summary>
    public static void Event(string message, LogChannel channel)
    {
        if (IsEnabled(channel)) Debug.Log($"[{channel}] {message}");
    }

    /// <summary>Always printed: content or configuration problems a designer must fix.</summary>
    public static void Warning(string message, LogChannel channel) => Debug.LogWarning($"[{channel}] {message}");

    /// <summary>Always printed: programming errors.</summary>
    public static void Error(string message, LogChannel channel) => Debug.LogError($"[{channel}] {message}");
}
