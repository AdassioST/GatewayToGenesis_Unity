using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>How the game window is shown.</summary>
public enum WindowMode { Fullscreen, Borderless, Windowed }

/// <summary>A family of sounds with its own volume (<see cref="SoundVolume"/> puts an AudioSource in one).</summary>
public enum SoundChannel { Music, Effects, Ambience, Interface }

/// <summary>
/// The player's settings (the Options menu, <see cref="MenuView"/>), kept in PlayerPrefs on this machine, apart from any
/// save: they belong to the player, not to a world. Values are read where they are used (the camera reads the pan
/// speed every frame); what needs pushing to Unity (window, frame rate, listener volume) is applied by
/// <see cref="SettingsApplier"/> when <see cref="Changed"/> fires. Every default is a proposal.
/// </summary>
public static class GameSettings
{
    private const string Prefix = "g2g.settings.";

    /// <summary>A setting changed: the section it belongs to ("audio", "display", "general").</summary>
    public static event Action<string> Changed;

    // ===== GENERAL =====

    /// <summary>Minutes between autosaves (0: never).</summary>
    public static int AutosaveMinutes { get => Int("autosave", 2); set => SetInt("autosave", Mathf.Max(0, value), "general"); }
    public static readonly int[] AutosaveChoices = { 0, 2, 5, 10, 15 };

    /// <summary>The game pauses while its window is in the background.</summary>
    public static bool PauseInBackground { get => Bool("pauseBackground", false); set => SetBool("pauseBackground", value, "general"); }

    /// <summary>The capital's view drifts when the pointer rests at a screen edge.</summary>
    public static bool EdgeScrolling { get => Bool("edgeScroll", true); set => SetBool("edgeScroll", value, "general"); }

    /// <summary>Speed of panning (edges) and zooming (the wheel), 0.5 to 2.</summary>
    public static float PanSpeed { get => Float("pan", 1f); set => SetFloat("pan", Mathf.Clamp(value, 0.5f, 2f), "general"); }
    public static float ZoomSpeed { get => Float("zoom", 1f); set => SetFloat("zoom", Mathf.Clamp(value, 0.5f, 2f), "general"); }

    /// <summary>How long the pointer holds still before a tooltip turns solid, times the theme's own (0.5 to 2).</summary>
    public static float TooltipHold { get => Float("tooltipHold", 1f); set => SetFloat("tooltipHold", Mathf.Clamp(value, 0.5f, 2f), "general"); }
    public static readonly float[] TooltipHoldChoices = { 0.5f, 1f, 1.5f, 2f };

    // ===== DISPLAY =====

    public static WindowMode Window { get => (WindowMode)Int("window", (int)WindowMode.Borderless); set => SetInt("window", (int)value, "display"); }

    /// <summary>The chosen resolution (0 x 0: the desktop's).</summary>
    public static Vector2Int Resolution
    {
        get => new Vector2Int(Int("width", 0), Int("height", 0));
        set { SetInt("width", Mathf.Max(0, value.x), null); SetInt("height", Mathf.Max(0, value.y), "display"); }
    }

    public static bool VSync { get => Bool("vsync", true); set => SetBool("vsync", value, "display"); }

    /// <summary>Most frames a second without VSync (0: no limit).</summary>
    public static int FrameCap { get => Int("fps", 60); set => SetInt("fps", Mathf.Max(0, value), "display"); }
    public static readonly int[] FrameCapChoices = { 30, 60, 120, 144, 0 };

    /// <summary>Brightness of the scene (not the menus), -1 to 1 (0: as drawn).</summary>
    public static float Brightness { get => Float("brightness", 0f); set => SetFloat("brightness", Mathf.Clamp(value, -1f, 1f), "display"); }

    /// <summary>Pulses, swells and breathing glows hold still.</summary>
    public static bool ReduceMotion { get => Bool("reduceMotion", false); set => SetBool("reduceMotion", value, "display"); }

    // ===== AUDIO =====

    public static float MasterVolume { get => Float("volume.master", 0.8f); set => SetFloat("volume.master", Mathf.Clamp01(value), "audio"); }

    public static float Volume(SoundChannel channel) => Float("volume." + channel, channel == SoundChannel.Music ? 0.7f : 0.8f);

    public static void SetVolume(SoundChannel channel, float value) => SetFloat("volume." + channel, Mathf.Clamp01(value), "audio");

    /// <summary>Silence while the window is in the background.</summary>
    public static bool MuteInBackground { get => Bool("muteBackground", true); set => SetBool("muteBackground", value, "audio"); }

    // ===== RESOLUTIONS =====

    /// <summary>The screen's resolutions, largest last, each size once.</summary>
    public static List<Vector2Int> Resolutions()
    {
        var list = new List<Vector2Int>();
        try { list = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct().OrderBy(r => r.x * r.y).ToList(); }
        catch (Exception) { }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    // ===== DEFAULTS =====

    private static readonly Dictionary<string, string[]> Sections = new Dictionary<string, string[]>
    {
        { "general", new[] { "autosave", "pauseBackground", "edgeScroll", "pan", "zoom", "tooltipHold" } },
        { "display", new[] { "window", "width", "height", "vsync", "fps", "brightness", "reduceMotion" } },
        { "audio", new[] { "volume.master", "volume.Music", "volume.Effects", "volume.Ambience", "volume.Interface", "muteBackground" } },
    };

    /// <summary>Put one section ("general", "display", "audio") back to its defaults.</summary>
    public static void ResetSection(string section)
    {
        if (!Sections.TryGetValue(section, out var keys)) return;
        foreach (var key in keys) { Floats.Remove(key); Ints.Remove(key); }
        try { foreach (var key in keys) PlayerPrefs.DeleteKey(Prefix + key); PlayerPrefs.Save(); } catch (Exception) { }
        Changed?.Invoke(section);
    }

    // ===== STORAGE (every access guarded: PlayerPrefs can throw where storage is refused) =====

    // Some settings are read every frame (the camera, the tooltips): values are kept once read.
    private static readonly Dictionary<string, float> Floats = new Dictionary<string, float>();
    private static readonly Dictionary<string, int> Ints = new Dictionary<string, int>();

    private static float Float(string key, float fallback)
    {
        if (Floats.TryGetValue(key, out float value)) return value;
        try { value = PlayerPrefs.GetFloat(Prefix + key, fallback); } catch (Exception) { value = fallback; }
        return Floats[key] = value;
    }

    private static int Int(string key, int fallback)
    {
        if (Ints.TryGetValue(key, out int value)) return value;
        try { value = PlayerPrefs.GetInt(Prefix + key, fallback); } catch (Exception) { value = fallback; }
        return Ints[key] = value;
    }

    private static bool Bool(string key, bool fallback) => Int(key, fallback ? 1 : 0) != 0;

    private static void SetFloat(string key, float value, string section)
    {
        Floats[key] = value;
        try { PlayerPrefs.SetFloat(Prefix + key, value); PlayerPrefs.Save(); } catch (Exception) { }
        if (section != null) Changed?.Invoke(section);
    }

    private static void SetInt(string key, int value, string section)
    {
        Ints[key] = value;
        try { PlayerPrefs.SetInt(Prefix + key, value); PlayerPrefs.Save(); } catch (Exception) { }
        if (section != null) Changed?.Invoke(section);
    }

    private static void SetBool(string key, bool value, string section) => SetInt(key, value ? 1 : 0, section);
}
