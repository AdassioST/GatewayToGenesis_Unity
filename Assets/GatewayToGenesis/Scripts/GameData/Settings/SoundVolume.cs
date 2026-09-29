using UnityEngine;

/// <summary>
/// Puts an AudioSource in a <see cref="SoundChannel"/>: its volume is <see cref="baseVolume"/> times the channel's
/// (Options, Audio), kept current as the player moves the slider. The master volume is the listener's
/// (<see cref="SettingsApplier"/>). The game has no sounds yet: every sound added later should carry one of these.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundVolume : MonoBehaviour
{
    public SoundChannel channel = SoundChannel.Effects;
    [Range(0f, 1f)] public float baseVolume = 1f;

    private AudioSource _source;

    /// <summary>The volume a sound of <paramref name="channel"/> plays at (before the master volume).</summary>
    public static float Of(SoundChannel channel, float baseVolume = 1f) => Mathf.Clamp01(baseVolume) * GameSettings.Volume(channel);

    private void OnEnable()
    {
        _source = GetComponent<AudioSource>();
        GameSettings.Changed += OnChanged;
        Apply();
    }

    private void OnDisable() => GameSettings.Changed -= OnChanged;

    private void OnChanged(string section)
    {
        if (section == "audio") Apply();
    }

    private void Apply()
    {
        if (_source != null) _source.volume = Of(channel, baseVolume);
    }
}
