using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game calendar: 21 sevenths per phase, 3 phases per echo, 4 echoes per cycle.
/// The 21st seventh of each phase is the Ritual Seventh.
/// While an event is pending, time runs slower by <see cref="slowMotionFactor"/>. The player can pause it all with
/// Space (<see cref="PlayerPaused"/>); <see cref="TimeFlowHud"/> shows how time flows.
/// </summary>
public class TimeSystemLogic : SingletonBehaviour<TimeSystemLogic>
{
    private const LogChannel Log = LogChannel.Time;

    public const int SeventhsPerPhase = 21;
    public const int PhasesPerEcho = 3;
    public const int EchoesPerCycle = 4;

    [SerializeField] private float secondsPerSeventh = 180f;
    [Tooltip("While an event notification is pending, time passes this many times slower.")]
    [SerializeField] private float slowMotionFactor = 3f;

    [SerializeField] private TMP_Text cycleText, echoText, seventhText;
    [SerializeField] private Image phaseImage;
    [SerializeField] private GameObject expandible, HUD;

    [SerializeField] private TimeUnit[] echoes, phases;

    [SerializeField] private List<string> cycleNames;

    public bool canTrackTime, isTimePaused = true;

    public float BaseSecondsPerSeventh => secondsPerSeventh;

    public int CurrentCycle { get; private set; } = 1;
    public int CurrentEcho { get; private set; } = 1;
    public int CurrentPhase { get; private set; } = 1;
    public int CurrentSeventh { get; private set; } = 1;

    public bool IsRitualSeventh => CurrentSeventh == SeventhsPerPhase;

    /// <summary>The current Cycle's name ("Overture").</summary>
    public string CycleName => currentCycleName;

    public event Action<int> OnPhaseChange, OnEchoChange, OnCycleChange, OnRitualSeventh, OnSeventhChange;

    private float timeSinceLastSeventh;
    private int totalPhaseIndex;
    private string currentCycleName = "Overture";
    private Color originalColor;
    private bool isSlowMotionActive;

    /// <summary>
    /// Run <paramref name="onReady"/> with the time system as soon as it exists (immediately if it already does).
    /// Replaces per-system "wait for TimeSystemLogic" coroutines.
    /// </summary>
    public static void WhenReady(MonoBehaviour owner, Action<TimeSystemLogic> onReady)
    {
        if (Instance != null)
        {
            onReady(Instance);
            return;
        }
        if (owner != null && owner.isActiveAndEnabled) owner.StartCoroutine(WaitForInstance(onReady));
    }

    private static IEnumerator WaitForInstance(Action<TimeSystemLogic> onReady)
    {
        while (Instance == null) yield return null;
        onReady(Instance);
    }

    private void Start()
    {
        if (seventhText != null) originalColor = seventhText.color;
        UpdateUI();
    }

    // ===== THE PLAYER'S PAUSE =====

    /// <summary>
    /// The player stopped time (Space, or the time indicator): everything that stops while a story is told stops too
    /// (the calendar, production, the pantry, the people, units on the map, research). Kept apart from
    /// <see cref="isTimePaused"/>, which stories, open tabs and cards set and clear on their own, so neither undoes the other.
    /// Not saved: a world always loads running.
    /// </summary>
    public bool PlayerPaused { get; private set; }

    /// <summary>The calendar stands still: the player paused, or a story, tab or card holds it.</summary>
    public bool IsStopped => PlayerPaused || isTimePaused || WorldSystem.Instance?.PendingEncounter != null;

    /// <summary>
    /// The simulation stands still: the player paused or a story is being told. Production, the pantry, the people, the
    /// crowd and units on the map check this.
    /// </summary>
    public static bool SimulationHeld =>
        (Instance != null && Instance.PlayerPaused) || (EventSystemLogic.Instance != null && EventSystemLogic.Instance.IsEventActive());

    public void SetPlayerPaused(bool paused)
    {
        if (PlayerPaused == paused) return;
        PlayerPaused = paused;
        GameLog.Event(paused ? "Paused by the player" : "Resumed by the player", Log);
    }

    public void TogglePlayerPause() => SetPlayerPaused(!PlayerPaused);

    /// <summary>The calendar's box on the HUD while it shows (null before Horology or while hidden).</summary>
    public RectTransform CalendarRect => expandible != null && expandible.activeInHierarchy && HUD != null && HUD.activeInHierarchy
        ? expandible.transform as RectTransform : null;

    private void Update()
    {
        if (SaveMenu.BlocksGameplay) return;
        if (KeyBindings.PausePressed) TogglePlayerPause();
        if (!IsStopped && canTrackTime)
        {
            timeSinceLastSeventh += Time.deltaTime / (isSlowMotionActive ? Mathf.Max(1f, slowMotionFactor) : 1f);
        }

        if (canTrackTime && timeSinceLastSeventh >= secondsPerSeventh)
        {
            timeSinceLastSeventh -= secondsPerSeventh;
            IncrementSeventh();
        }
    }

    private void IncrementSeventh()
    {
        CurrentSeventh++;
        if (CurrentSeventh > SeventhsPerPhase)
        {
            CurrentSeventh = 1;
            IncrementPhase();
        }
        else if (CurrentSeventh == SeventhsPerPhase)
        {
            OnRitualSeventh?.Invoke(CurrentSeventh);
        }

        OnSeventhChange?.Invoke(CurrentSeventh);
        UpdateUI();
    }

    private void IncrementPhase()
    {
        CurrentPhase++;
        totalPhaseIndex++;
        if (CurrentPhase > PhasesPerEcho)
        {
            CurrentPhase = 1;
            IncrementEcho();
        }
        if (phases == null || totalPhaseIndex >= phases.Length) totalPhaseIndex = 0;
        OnPhaseChange?.Invoke(CurrentPhase);
    }

    private void IncrementEcho()
    {
        CurrentEcho++;
        if (CurrentEcho > EchoesPerCycle)
        {
            CurrentEcho = 1;
            IncrementCycle();
        }
        OnEchoChange?.Invoke(CurrentEcho);
    }

    private void IncrementCycle()
    {
        CurrentCycle++;
        if (cycleNames != null && cycleNames.Count > 0) currentCycleName = cycleNames[UnityEngine.Random.Range(0, cycleNames.Count)];
        GameLog.Event($"Cycle {CurrentCycle} ({currentCycleName}) begins", Log);
        OnCycleChange?.Invoke(CurrentCycle);
    }

    public void UpdateUI()
    {
        if (expandible == null || HUD == null || !expandible.activeSelf || !HUD.activeSelf) return;

        if (cycleText != null) cycleText.text = KeywordMarkup.SafeGlyphs($"Cycle {CurrentCycle} ◦ {currentCycleName}");

        var echo = CurrentEchoUnit;
        if (echo != null && echoText != null)
        {
            echoText.text = echo.unitName;
            SetTooltip(echoText, echo.unitName, echo.description);
        }

        var phase = CurrentPhaseUnit;
        if (phase != null && phaseImage != null)
        {
            phaseImage.sprite = phase.icon;
            SetTooltip(phaseImage, phase.unitName, phase.description);
        }

        if (seventhText != null)
        {
            seventhText.text = CurrentSeventh.ToString();
            seventhText.color = IsRitualSeventh ? Color.red : originalColor;
            SetTooltip(seventhText, IsRitualSeventh ? "Ritual Seventh" : "Seventh", IsRitualSeventh ? "The world has perfectly attuned!" : "The minimum unit of time tracking.");
        }
    }

    /// <summary>The echo now playing (null when the calendar has no echoes configured).</summary>
    public TimeUnit CurrentEchoUnit => echoes != null && CurrentEcho >= 1 && CurrentEcho <= echoes.Length ? echoes[CurrentEcho - 1] : null;

    /// <summary>The phase now playing (null when the calendar has no phases configured).</summary>
    public TimeUnit CurrentPhaseUnit => phases != null && totalPhaseIndex < phases.Length ? phases[totalPhaseIndex] : null;

    // The title is also the calendar term's keyword, so the tooltip carries its lore.
    private static void SetTooltip(Component anchor, string title, string description)
    {
        var tooltip = anchor.GetComponentInParent<TooltipTrigger>();
        if (tooltip == null) return;
        tooltip.SetCustom(title, description ?? tooltip.customDescription, tooltip.customType, keyword: title);
    }

    public void PauseTime(bool pause)
    {
        if (!canTrackTime) return;
        isTimePaused = pause;
        UpdateUI();
    }

    /// <summary>Slow time down while an event is waiting for the player.</summary>
    public void EnableSlowMotion()
    {
        if (isSlowMotionActive) return;
        isSlowMotionActive = true;
        GameLog.Event($"Slow motion on: time passes {slowMotionFactor}x slower", Log);
    }

    public void DisableSlowMotion()
    {
        if (!isSlowMotionActive) return;
        isSlowMotionActive = false;
        GameLog.Event("Slow motion off", Log);
    }

    public bool IsSlowMotionActive => isSlowMotionActive;

    /// <summary>Real seconds a full seventh takes right now.</summary>
    public float GetEffectiveSecondsPerSeventh() => isSlowMotionActive ? secondsPerSeventh * slowMotionFactor : secondsPerSeventh;

    /// <summary>Progress through the current seventh, 0 to 1.</summary>
    public float GetSeventhProgress() => secondsPerSeventh > 0f ? Mathf.Clamp01(timeSinceLastSeventh / secondsPerSeventh) : 0f;

    /// <summary>Real seconds until the next seventh at the current speed.</summary>
    public float GetRemainingSecondsToSeventh()
    {
        float remaining = Mathf.Max(0f, secondsPerSeventh - timeSinceLastSeventh);
        return isSlowMotionActive ? remaining * slowMotionFactor : remaining;
    }
}
