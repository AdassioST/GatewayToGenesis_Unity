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
    public bool enableAllGameLogicLogging;
    public bool enableAllEventSystemLogicLogging;
    public bool enableAllGovernmentLogicLogging;
    public bool enableAllEnvironmentLogicLogging;

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

    private void Start()
    {
        InitializeLoggingSettings();
    }

    private void InitializeLoggingSettings()
    {
        if (enableAllGameLogicLogging)
        {
            enableGameUnitsLogicLogging = true;
            enablePopGrowthLogicLogging = true;
        }

        if (enableAllEnvironmentLogicLogging)
        {
            enableTimeSystemLogicLogging = true;
        }

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
