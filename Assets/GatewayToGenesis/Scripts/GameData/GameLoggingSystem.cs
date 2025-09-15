using System.Collections.Generic;
using UnityEngine;

public class GameLoggingSystem : MonoBehaviour
{
    public static GameLoggingSystem Instance { get; private set; }

    [Header("Full Event System Settings")]
    [SerializeField] private bool _enableAllGameLogicLogging;
    [SerializeField] private bool _enableAllEventSystemLogicLogging;
    [SerializeField] private bool _enableAllGovernmentLogicLogging;
    [SerializeField] private bool _enableAllEnvironmentLogicLogging;

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

    // Centralized category mapping system
    private readonly Dictionary<string, bool> _loggingStates = new Dictionary<string, bool>();
    private readonly Dictionary<string, string[]> _categoryMappings = new Dictionary<string, string[]>
    {
        { "GameLogic", new[] { "enableGameUnitsLogicLogging", "enablePopGrowthLogicLogging" } },
        { "Environment", new[] { "enableTimeSystemLogicLogging" } },
        { "EventSystem", new[] { "enableEventSystemLogicLogging", "enableEventVolumeManagerLogging", "enableEventScreenManagerLogging", "enableInkStoryManagerLogging", "enableInkDrivenEventSetupLogging", "enableChorusScreenManagerLogging", "enableDecisionTokenLogging" } },
        { "Government", new[] { "enableStatManagerLogging", "enableGovernmentLogicLogging", "enableCivicManagerLogging", "enableLegendLeaderLogicLogging", "enableGovernmentTabLogging", "enableSeatPositionDisplayLogging", "enableLeaderSlotDisplayLogging" } }
    };

    private readonly Dictionary<string, string> _scriptToFieldMap = new Dictionary<string, string>
    {
        { "EventSystemLogic", "enableEventSystemLogicLogging" },
        { "EventVolumeManager", "enableEventVolumeManagerLogging" },
        { "EventScreenManager", "enableEventScreenManagerLogging" },
        { "InkStoryManager", "enableInkStoryManagerLogging" },
        { "InkDrivenEventSetup", "enableInkDrivenEventSetupLogging" },
        { "ChorusScreenManager", "enableChorusScreenManagerLogging" },
        { "DecisionToken", "enableDecisionTokenLogging" },
        { "GameUnitsLogic", "enableGameUnitsLogicLogging" },
        { "PopGrowthLogic", "enablePopGrowthLogicLogging" },
        { "StatManager", "enableStatManagerLogging" },
        { "GovernmentLogic", "enableGovernmentLogicLogging" },
        { "GovernmentTab", "enableGovernmentTabLogging" },
        { "CivicManager", "enableCivicManagerLogging" },
        { "LegendLeaderLogic", "enableLegendLeaderLogicLogging" },
        { "SeatPositionDisplay", "enableSeatPositionDisplayLogging" },
        { "LeaderSlotDisplay", "enableLeaderSlotDisplayLogging" },
        { "TimeSystemLogic", "enableTimeSystemLogicLogging" }
    };

    // Property setters with centralized update logic
    public bool enableAllGameLogicLogging
    {
        get => _enableAllGameLogicLogging;
        set { if (_enableAllGameLogicLogging != value) { _enableAllGameLogicLogging = value; UpdateCategory("GameLogic"); } }
    }

    public bool enableAllEventSystemLogicLogging
    {
        get => _enableAllEventSystemLogicLogging;
        set { if (_enableAllEventSystemLogicLogging != value) { _enableAllEventSystemLogicLogging = value; UpdateCategory("EventSystem"); } }
    }

    public bool enableAllGovernmentLogicLogging
    {
        get => _enableAllGovernmentLogicLogging;
        set { if (_enableAllGovernmentLogicLogging != value) { _enableAllGovernmentLogicLogging = value; UpdateCategory("Government"); } }
    }

    public bool enableAllEnvironmentLogicLogging
    {
        get => _enableAllEnvironmentLogicLogging;
        set { if (_enableAllEnvironmentLogicLogging != value) { _enableAllEnvironmentLogicLogging = value; UpdateCategory("Environment"); } }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        InitializeDictionaries();
    }

    private void InitializeDictionaries()
    {
        _loggingStates["GameLogic"] = enableAllGameLogicLogging;
        _loggingStates["Environment"] = enableAllEnvironmentLogicLogging;
        _loggingStates["EventSystem"] = enableAllEventSystemLogicLogging;
        _loggingStates["Government"] = enableAllGovernmentLogicLogging;
    }

    private void Start() => InitializeLoggingSettings();

    private void OnValidate()
    {
        if (Application.isPlaying) CheckForMainCategoryChanges();
    }

    private void InitializeLoggingSettings()
    {
        _loggingStates["GameLogic"] = enableAllGameLogicLogging;
        _loggingStates["Environment"] = enableAllEnvironmentLogicLogging;
        _loggingStates["EventSystem"] = enableAllEventSystemLogicLogging;
        _loggingStates["Government"] = enableAllGovernmentLogicLogging;
        
        UpdateAllCategories();
    }

    private void CheckForMainCategoryChanges()
    {
        // Ensure dictionary is initialized
        if (_loggingStates.Count == 0)
        {
            InitializeDictionaries();
        }

        var categories = new[] { "GameLogic", "Environment", "EventSystem", "Government" };
        var mainStates = new[] { enableAllGameLogicLogging, enableAllEnvironmentLogicLogging, enableAllEventSystemLogicLogging, enableAllGovernmentLogicLogging };
        
        for (int i = 0; i < categories.Length; i++)
        {
            if (_loggingStates[categories[i]] != mainStates[i])
            {
                _loggingStates[categories[i]] = mainStates[i];
                UpdateCategory(categories[i]);
            }
        }
    }

    private void UpdateAllCategories()
    {
        foreach (var category in _categoryMappings.Keys)
            UpdateCategory(category);
    }

    private void UpdateCategory(string categoryName)
    {
        // Ensure dictionary is initialized
        if (_loggingStates.Count == 0)
        {
            InitializeDictionaries();
        }

        bool isEnabled = _loggingStates[categoryName];
        foreach (var fieldName in _categoryMappings[categoryName])
        {
            var field = GetType().GetField(fieldName);
            field?.SetValue(this, isEnabled);
        }
    }

    public void RefreshLoggingSettings()
    {
        UpdateAllCategories();
        Debug.Log("[GameLoggingSystem] Logging settings refreshed at runtime");
    }

    public void LogEvent(string message, string originScript)
    {
        if (_scriptToFieldMap.TryGetValue(originScript, out string fieldName))
        {
            var field = GetType().GetField(fieldName);
            if (field != null && (bool)field.GetValue(this))
            {
                Debug.Log($"[{originScript}] {message}");
            }
        }
        else
        {
            Debug.LogWarning($"Unknown origin script: {originScript}");
            Debug.Log($"[Generic Debug] {message}");
        }
    }
}