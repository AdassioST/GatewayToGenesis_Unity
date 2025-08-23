using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeSystemLogic : MonoBehaviour
{
    [Header("Time System Settings")]
    [SerializeField] private bool enableTimeSystemLogicLogging; // Whether to log what the boss is doing

    public static TimeSystemLogic Instance { get; private set; }

    [SerializeField] private float secondsPerSeventh = 180f; // 1 Seventh = 3 minutes
    [SerializeField] private float slowMotionFactor = 3f; // Time slows down by this factor when events are pending
    
    // Public accessor for the base time value
    public float BaseSecondsPerSeventh => secondsPerSeventh;
    private float timeSinceLastSeventh = 0f;
    
    // Track slow motion periods to properly scale accumulated time
    private float slowMotionTimeAccumulated = 0f;
    private bool wasInSlowMotion = false;

    [SerializeField] private TMP_Text cycleText, echoText, seventhText;
    [SerializeField] private Image phaseImage;
    [SerializeField] private GameObject expandible, HUD;

    [SerializeField] private TimeUnit[] echoes, phases;

    public int CurrentCycle { get; private set; } = 1;
    public int CurrentEcho { get; private set; } = 1;
    public int CurrentPhase { get; private set; } = 1;
    public int CurrentSeventh { get; private set; } = 1;

    public event Action<int> OnPhaseChange, OnEchoChange, OnCycleChange, OnRitualSeventh, OnSeventhChange;

    private int totalPhaseIndex;

    [SerializeField] private List<string> cycleNames;

    private string currentCycleName;

    private Color originalColor;

    public bool canTrackTime, isTimePaused = true;
    private bool isSlowMotionActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate TimeSystemLogic found, destroying the new one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        currentCycleName = "Overture"; // The first cycle name

        originalColor = seventhText.color;

        UpdateUI();

    }

    private void Update()
    {
        if (!isTimePaused)
        {
            // Handle time accumulation with proper slow motion scaling
            if (isSlowMotionActive)
            {
                // In slow motion: accumulate time at reduced rate
                slowMotionTimeAccumulated += Time.deltaTime / slowMotionFactor;
                timeSinceLastSeventh += Time.deltaTime / slowMotionFactor;
                wasInSlowMotion = true;
            }
            else
            {
                // Normal time: accumulate normally
                if (wasInSlowMotion)
                {
                    // Transitioning from slow motion: convert accumulated slow motion time back to normal time
                    // The accumulated time was scaled down by slowMotionFactor, so we need to scale it back up
                    float convertedTime = slowMotionTimeAccumulated * slowMotionFactor;
                    timeSinceLastSeventh += convertedTime;
                    LogTimeSystem($"Slow motion ended: converted {slowMotionTimeAccumulated:F2}s slow motion → {convertedTime:F2}s normal time, total time: {timeSinceLastSeventh:F2}s");
                    slowMotionTimeAccumulated = 0f;
                    wasInSlowMotion = false;
                }
                else
                {
                    // Always normal time
                    timeSinceLastSeventh += Time.deltaTime;
                }
            }
        }

        // Check if we've reached the threshold for the next seventh
        if (timeSinceLastSeventh >= secondsPerSeventh && canTrackTime)
        {
            timeSinceLastSeventh = 0f;
            IncrementSeventh();
        }
    }

    private void IncrementSeventh()
    {
        CurrentSeventh++;

        if (CurrentSeventh > 21) // Transition to next phase
        {
            CurrentSeventh = 1;
            IncrementPhase();
        }
        else if (CurrentSeventh == 21) // Ritual Seventh
        {
            OnRitualSeventh?.Invoke(CurrentSeventh);
        }

        OnSeventhChange?.Invoke(CurrentSeventh);
        
        // Reset slow motion tracking when seventh changes to prevent accumulation across boundaries
        ResetSlowMotionTracking($"Seventh {CurrentSeventh}");
        
        UpdateUI();
    }

    private void IncrementPhase()
    {
        CurrentPhase++;
        totalPhaseIndex++;

        if (CurrentPhase > 3) // Transition to next echo
        {
            CurrentPhase = 1;
            IncrementEcho();
        }

        if (totalPhaseIndex >= phases.Length) // Reset the totalPhaseIndex at the end of the cycle
            totalPhaseIndex = 0;

        // Reset slow motion tracking when phase changes to prevent accumulation across boundaries
        ResetSlowMotionTracking($"Phase {CurrentPhase}");

        OnPhaseChange?.Invoke(CurrentPhase);
    }

    private void IncrementEcho()
    {
        CurrentEcho++;

        if (CurrentEcho > 4) // Transition to next cycle
        {
            CurrentEcho = 1;
            IncrementCycle();
        }

        // Reset slow motion tracking when echo changes to prevent accumulation across boundaries
        ResetSlowMotionTracking($"Echo {CurrentEcho}");

        OnEchoChange?.Invoke(CurrentEcho);
    }

    private void IncrementCycle()
    {
        CurrentCycle++;
        currentCycleName = CurrentCycle == 1 ? "Overture" : cycleNames[UnityEngine.Random.Range(0, cycleNames.Count)];
        
        // Reset slow motion tracking when cycle changes to prevent accumulation across boundaries
        ResetSlowMotionTracking($"Cycle {CurrentCycle} ({currentCycleName})");
        
        OnCycleChange?.Invoke(CurrentCycle);
    }

    public void UpdateUI()
    {
        if (!expandible.activeSelf || !HUD.activeSelf) return;

        // Update Cycle Text
        cycleText.text = $"Cycle {CurrentCycle} ◦ {currentCycleName}";

        // Update Echo Text
        if (CurrentEcho - 1 < echoes.Length)
        {
            echoText.text = echoes[CurrentEcho - 1].unitName;
            echoText.GetComponentInParent<TooltipTrigger>().customTitle = echoes[CurrentEcho - 1].description;

        }

        // Update Phase Image
        if (totalPhaseIndex < phases.Length)
        {
            phaseImage.sprite = phases[totalPhaseIndex].icon;

            phaseImage.GetComponentInParent<TooltipTrigger>().customTitle = phases[totalPhaseIndex].unitName;
            phaseImage.GetComponentInParent<TooltipTrigger>().customDescription = phases[totalPhaseIndex].description;
        }

        // Update Seventh Text and Color
        seventhText.text = CurrentSeventh.ToString();

        seventhText.color = CurrentSeventh == 21 ? Color.red : originalColor;

        seventhText.GetComponentInParent<TooltipTrigger>().customTitle = CurrentSeventh == 21 ? "Ritual Seventh" : "Seventh";
        seventhText.GetComponentInParent<TooltipTrigger>().customDescription = CurrentSeventh == 21 ? "The world has perfectly attuned!" : "The minimum unit of time tracking.";
    }

    public void PauseTime(bool pause)
    {
        if (!canTrackTime) return;

        isTimePaused = pause;

        UpdateUI();
    }
    
    /// <summary>
    /// Enable slow motion time (when events are pending)
    /// </summary>
    public void EnableSlowMotion()
    {
        if (!isSlowMotionActive)
        {
            isSlowMotionActive = true;
            wasInSlowMotion = true;
            LogTimeSystem($"Slow motion enabled - time will pass {slowMotionFactor}x slower");
        }
    }
    
    /// <summary>
    /// Disable slow motion time (when events are handled)
    /// </summary>
    public void DisableSlowMotion()
    {
        if (isSlowMotionActive)
        {
            isSlowMotionActive = false;
            LogTimeSystem("Slow motion disabled - time returns to normal speed");
        }
    }
    
    /// <summary>
    /// Get the effective seconds per seventh (considering slow motion)
    /// </summary>
    public float GetEffectiveSecondsPerSeventh()
    {
        if (isSlowMotionActive)
        {
            return secondsPerSeventh * slowMotionFactor;
        }
        return secondsPerSeventh;
    }
    
    /// <summary>
    /// Get current time progress towards next seventh (0.0 to 1.0)
    /// </summary>
    public float GetSeventhProgress()
    {
        return timeSinceLastSeventh / secondsPerSeventh;
    }
    
    /// <summary>
    /// Get remaining seconds until next seventh
    /// </summary>
    public float GetRemainingSecondsToSeventh()
    {
        if (isSlowMotionActive)
        {
            // In slow motion, calculate remaining time considering the slowdown
            float remainingNormalTime = secondsPerSeventh - timeSinceLastSeventh;
            return remainingNormalTime * slowMotionFactor;
        }
        return secondsPerSeventh - timeSinceLastSeventh;
    }
    
    /// <summary>
    /// Reset slow motion tracking to prevent accumulation across time boundaries
    /// </summary>
    private void ResetSlowMotionTracking(string boundaryName)
    {
        if (wasInSlowMotion)
        {
            LogTimeSystem($"{boundaryName} changed - resetting slow motion tracking");
            slowMotionTimeAccumulated = 0f;
            wasInSlowMotion = false;
        }
    }
    
    /// <summary>
    /// Log time system messages
    /// </summary>
    private void LogTimeSystem(string message)
    {
        if (enableTimeSystemLogicLogging)
        {
            Debug.Log($"[TimeSystemLogic] {message}");
        }
    }
}

