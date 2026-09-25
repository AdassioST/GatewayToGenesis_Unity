using UnityEngine;

/// <summary>
/// Scene switch for <see cref="GameLog"/>: pick the channels whose events should print. Changes made in the
/// inspector during Play mode take effect immediately. Warnings and errors always print.
/// Runs first so events logged from other systems' Awake are not lost.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class GameLoggingSystem : MonoBehaviour
{
    [Tooltip("Channels whose diagnostic events print to the Console. Warnings and errors always print.")]
    [SerializeField] private LogChannel enabledChannels = LogChannel.None;

    public LogChannel EnabledChannels
    {
        get => enabledChannels;
        set { enabledChannels = value; GameLog.Enabled = value; }
    }

    private void Awake() => GameLog.Enabled = enabledChannels;

    private void OnValidate()
    {
        if (Application.isPlaying) GameLog.Enabled = enabledChannels;
    }
}
