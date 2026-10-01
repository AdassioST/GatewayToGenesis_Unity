using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Auric Aria's recorded voice (none is recorded yet): when she begins to say something (<see cref="AuricAria.Began"/>)
/// its clip plays, if there is one at Resources/Audio/AuricAria/&lt;id&gt; (ids like "founding.feed", "hint.survey",
/// "moment.first-death"); when she falls silent it fades out. Her words stay up at least as long as their clip
/// (<see cref="AuricAria.VoiceSeconds"/>). Plays in the Voice channel (Options, Audio). Added by <see cref="Tutorials"/>.
/// </summary>
public class AuricVoiceOver : MonoBehaviour
{
    public const string Folder = "Audio/AuricAria/";
    private const float FadeSeconds = 0.5f;

    private AudioSource _source;
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private float _fade;
    private bool _fading;

    private void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        AuricAria.VoiceSeconds = Seconds;
        AuricAria.Began += Play;
        AuricAria.Ended += Stop;
    }

    private void OnDestroy()
    {
        AuricAria.Began -= Play;
        AuricAria.Ended -= Stop;
        if (AuricAria.VoiceSeconds == (System.Func<string, float>)Seconds) AuricAria.VoiceSeconds = null;
    }

    private AudioClip Clip(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!_clips.TryGetValue(id, out var clip)) _clips[id] = clip = Resources.Load<AudioClip>(Folder + id);
        return clip;
    }

    private float Seconds(string id) => Clip(id) is AudioClip clip ? clip.length : 0f;

    private void Play(AuricWords words)
    {
        var clip = Clip(words.id);
        if (clip == null) return;
        _source.Stop();
        _source.clip = clip;
        _fade = 1f;
        _fading = false;
        _source.volume = SoundVolume.Of(SoundChannel.Voice);
        _source.Play();
    }

    private void Stop(AuricWords words)
    {
        if (_source != null && _source.isPlaying && _source.clip != null && _source.clip == Clip(words.id)) _fading = true;
    }

    private void Update()
    {
        if (!_source.isPlaying) return;
        if (_fading && (_fade -= Time.unscaledDeltaTime / FadeSeconds) <= 0f)
        {
            _source.Stop();
            _fading = false;
            return;
        }
        _source.volume = SoundVolume.Of(SoundChannel.Voice) * _fade;
    }
}
