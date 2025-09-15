using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLoggingSystem : MonoBehaviour
{

    public static GameLoggingSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    [Header("Full Event System Settings")]
    [SerializeField] private bool _enableAllGameLogicLogging;
    [SerializeField] private bool _enableAllEventSystemLogicLogging;
    [SerializeField] private bool _enableAllGovernmentLogicLogging;
    [SerializeField] private bool _enableAllEnvironmentLogicLogging;

    public bool enableAllGameLogicLogging
    {
        get => _enableAllGameLogicLogging;
        set
        {
            if (_enableAllGameLogicLogging != value)
            {
                _enableAllGameLogicLogging = value;
                UpdateGameLogicLogging();
            }
        }
    }

    public bool enableAllEventSystemLogicLogging
    {
        get => _enableAllEventSystemLogicLogging;
        set
        {
            if (_enableAllEventSystemLogicLogging != value)
            {
                _enableAllEventSystemLogicLogging = value;
                UpdateEventSystemLogging();
            }
        }
    }

    public bool enableAllGovernmentLogicLogging
    {
        get => _enableAllGovernmentLogicLogging;
        set
        {
            if (_enableAllGovernmentLogicLogging != value)
            {
                _enableAllGovernmentLogicLogging = value;
                UpdateGovernmentLogging();
            }
        }
    }

    public bool enableAllEnvironmentLogicLogging
    {
        get => _enableAllEnvironmentLogicLogging;
        set
        {
            if (_enableAllEnvironmentLogicLogging != value)
            {
                _enableAllEnvironmentLogicLogging = value;
                UpdateEnvironmentLogging();
            }
        }
    }

    [Header("Detailed Game Logic Settings")]
    public bool enableGameUnitsLogicLogging;
    public bool enablePopGrowthLogicLogging;

    [Header("Detailed Environment Logic Settings")]
    public bool enableTimeSystemLogicLogging;

    [Header("Detailed Event System Settings")]

    public bool enableEventSystemLogicLogging;
    public bool enableEventVolumeManagerLogging;
    public bool enableEventScreenManagerLogging;
    public bool enableInkStoryManagerLogging;
    public bool enableInkDrivenEventSetupLogging;
    public bool enableChorusScreenManagerLogging;
    public bool enableDecisionTokenLogging;

    [Header("Detailed Government Logic Settings")]
    public bool enableStatManagerLogging;
    public bool enableGovernmentLogicLogging;
    public bool enableGovernmentTabLogging;
    public bool enableCivicManagerLogging;
    public bool enableLegendLeaderLogicLogging;
    public bool enableSeatPositionDisplayLogging;
    public bool enableLeaderSlotDisplayLogging;

    // Track previous states to detect actual changes
    private bool _previousGameLogicState;
    private bool _previousEnvironmentState;
    private bool _previousEventSystemState;
    private bool _previousGovernmentState;

    private void Start()
    {
        InitializeLoggingSettings();
    }

    private void OnValidate()
    {
        // This method is called when values change in the inspector
        if (Application.isPlaying)
        {
            CheckForMainCategoryChanges();
        }
    }

    private void InitializeLoggingSettings()
    {
        // Initialize previous states
        _previousGameLogicState = enableAllGameLogicLogging;
        _previousEnvironmentState = enableAllEnvironmentLogicLogging;
        _previousEventSystemState = enableAllEventSystemLogicLogging;
        _previousGovernmentState = enableAllGovernmentLogicLogging;
        
        UpdateAllLoggingSettings();
    }

    private void CheckForMainCategoryChanges()
    {
        // Check for Game Logic changes
        if (_previousGameLogicState != enableAllGameLogicLogging)
        {
            UpdateGameLogicLogging();
            _previousGameLogicState = enableAllGameLogicLogging;
        }

        // Check for Environment changes
        if (_previousEnvironmentState != enableAllEnvironmentLogicLogging)
        {
            UpdateEnvironmentLogging();
            _previousEnvironmentState = enableAllEnvironmentLogicLogging;
        }

        // Check for Event System changes
        if (_previousEventSystemState != enableAllEventSystemLogicLogging)
        {
            UpdateEventSystemLogging();
            _previousEventSystemState = enableAllEventSystemLogicLogging;
        }

        // Check for Government changes
        if (_previousGovernmentState != enableAllGovernmentLogicLogging)
        {
            UpdateGovernmentLogging();
            _previousGovernmentState = enableAllGovernmentLogicLogging;
        }
    }

    private void UpdateAllLoggingSettings()
    {
        UpdateGameLogicLogging();
        UpdateEnvironmentLogging();
        UpdateEventSystemLogging();
        UpdateGovernmentLogging();
    }

    private void UpdateGameLogicLogging()
    {
        if (enableAllGameLogicLogging)
        {
            enableGameUnitsLogicLogging = true;
            enablePopGrowthLogicLogging = true;
        }
        else
        {
            enableGameUnitsLogicLogging = false;
            enablePopGrowthLogicLogging = false;
        }
    }

    private void UpdateEnvironmentLogging()
    {
        if (enableAllEnvironmentLogicLogging)
        {
            enableTimeSystemLogicLogging = true;
        }
        else
        {
            enableTimeSystemLogicLogging = false;
        }
    }

    private void UpdateEventSystemLogging()
    {
        if (enableAllEventSystemLogicLogging)
        {
            enableEventSystemLogicLogging = true;
            enableEventVolumeManagerLogging = true;
            enableEventScreenManagerLogging = true;
            enableInkStoryManagerLogging = true;
            enableInkDrivenEventSetupLogging = true;
            enableChorusScreenManagerLogging = true;
            enableDecisionTokenLogging = true;
        }
        else
        {
            enableEventSystemLogicLogging = false;
            enableEventVolumeManagerLogging = false;
            enableEventScreenManagerLogging = false;
            enableInkStoryManagerLogging = false;
            enableInkDrivenEventSetupLogging = false;
            enableChorusScreenManagerLogging = false;
            enableDecisionTokenLogging = false;
        }
    }

    private void UpdateGovernmentLogging()
    {
        if (enableAllGovernmentLogicLogging)
        {
            enableStatManagerLogging = true;
            enableGovernmentLogicLogging = true;
            enableCivicManagerLogging = true;
            enableLegendLeaderLogicLogging = true;
            enableGovernmentTabLogging = true;
            enableSeatPositionDisplayLogging = true;
            enableLeaderSlotDisplayLogging = true;
        }
        else
        {
            enableStatManagerLogging = false;
            enableGovernmentLogicLogging = false;
            enableCivicManagerLogging = false;
            enableLegendLeaderLogicLogging = false;
            enableGovernmentTabLogging = false;
            enableSeatPositionDisplayLogging = false;
            enableLeaderSlotDisplayLogging = false;
        }
    }

    /// <summary>
    /// Manually refresh all logging settings. Useful for runtime debugging.
    /// </summary>
    public void RefreshLoggingSettings()
    {
        UpdateAllLoggingSettings();
        Debug.Log("[GameLoggingSystem] Logging settings refreshed at runtime");
    }

    public void LogEvent(string message, string originScript)
    {
        switch (originScript)
        {
            case "EventSystemLogic":
                if (enableEventSystemLogicLogging)
                {
                    Debug.Log($"[EventSystemLogic] {message}");
                }
                break;
            case "EventVolumeManager":
                if (enableEventVolumeManagerLogging)
                {
                    Debug.Log($"[EventVolumeManager] {message}");
                }
                break;
            case "EventScreenManager":
                if (enableEventScreenManagerLogging)
                {
                    Debug.Log($"[EventScreenManager] {message}");
                }
                break;
            case "InkStoryManager":
                if (enableInkStoryManagerLogging)
                {
                    Debug.Log($"[InkStoryManager] {message}");
                }
                break;
            case "InkDrivenEventSetup":
                if (enableInkDrivenEventSetupLogging)
                {
                    Debug.Log($"[InkDrivenEventSetup] {message}");
                }
                break;
            case "ChorusScreenManager":
                if (enableChorusScreenManagerLogging)
                {
                    Debug.Log($"[ChorusScreenManager] {message}");
                }
                break;
            case "DecisionToken":
                if (enableDecisionTokenLogging)
                {
                    Debug.Log($"[DecisionToken] {message}");
                }
                break;
            case "GameUnitsLogic":
                if (enableGameUnitsLogicLogging)
                {
                    Debug.Log($"[GameUnitsLogic] {message}");
                }
                break;
            case "PopGrowthLogic":
                if (enablePopGrowthLogicLogging)
                {
                    Debug.Log($"[PopGrowthLogic] {message}");
                }
                break;
            case "StatManager":
                if (enableStatManagerLogging)
                {
                    Debug.Log($"[StatManager] {message}");
                }
                break;
            case "GovernmentLogic":
                if (enableGovernmentLogicLogging)
                {
                    Debug.Log($"[GovernmentLogic] {message}");
                }
                break;
            case "GovernmentTab":
                if (enableGovernmentTabLogging)
                {
                    Debug.Log($"[GovernmentTab] {message}");
                }
                break;
            case "CivicManager":
                if (enableCivicManagerLogging)
                {
                    Debug.Log($"[CivicManager] {message}");
                }
                break;
            case "LegendLeaderLogic":
                if (enableLegendLeaderLogicLogging)
                {
                    Debug.Log($"[LegendLeaderLogic] {message}");
                }
                break;
            case "SeatPositionDisplay":
                if (enableSeatPositionDisplayLogging)
                {
                    Debug.Log($"[SeatPositionDisplay] {message}");
                }
                break;
            case "LeaderSlotDisplay":
                if (enableLeaderSlotDisplayLogging)
                {
                    Debug.Log($"[LeaderSlotDisplay] {message}");
                }
                break;
            case "TimeSystemLogic":
                if (enableTimeSystemLogicLogging)
                {
                    Debug.Log($"[TimeSystemLogic] {message}");
                }
                break;
            default:
                Debug.LogWarning($"Unknown origin script: {originScript}");
                Debug.Log($"[Generic Debug] {message}");
                break;
        }
    }

}
