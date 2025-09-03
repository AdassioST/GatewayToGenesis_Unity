using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central manager for all civic systems including acquisition, removal, effects, and council management
/// </summary>
public class CivicManager : MonoBehaviour
{
    public static CivicManager Instance { get; private set; }

    [Header("Civic System Settings")]
    [SerializeField] private bool enableCivicLogging = true;
    
    [Header("Civic Slot Configuration")]
    [SerializeField] private int startingAeonicSlots = 1;
    [SerializeField] private int startingMajorSlots = 1;
    [SerializeField] private int startingMinorSlots = 2;
    [SerializeField] private int maxAeonicSlots = 2;
    [SerializeField] private int maxMajorSlots = 4;
    [SerializeField] private int maxMinorSlots = 6;

    [Header("Council System")]
    [SerializeField] private int councilSize = 6; // Temporary council size for testing
    [SerializeField] private bool enableCouncilSystem = true;

    // Active civics by tier
    private List<CivicData> activeAeonicCivics = new List<CivicData>();
    private List<CivicData> activeMajorCivics = new List<CivicData>();
    private List<CivicData> activeMinorCivics = new List<CivicData>();

    // Council positions
    private List<CouncilPosition> councilPositions = new List<CouncilPosition>();
    
    // Available civics loaded from Resources
    private Dictionary<string, CivicData> availableCivics = new Dictionary<string, CivicData>();
    
    // Civic removal tracking for penalties
    private Dictionary<string, CivicRemovalPenalty> removalPenalties = new Dictionary<string, CivicRemovalPenalty>();

    // Events for UI updates
    public System.Action<CivicData, bool> OnCivicChanged; // (civic, wasAdded)
    public System.Action<CouncilPosition> OnCouncilPositionChanged;
    public System.Action OnCivicSlotsChanged;
    
    // UI update events
    public System.Action OnCivicPoolChanged; // When civic availability changes
    public System.Action OnActiveCivicsChanged; // When active civics change

    /// <summary>
    /// Council position for Major/Aeonic civics (Legacy - now using CouncilSeat system)
    /// </summary>
    [System.Serializable]
    public class CouncilPosition
    {
        public string positionName;
        public CivicData sourceCivic;
        public bool isOccupied;
        public int priority;
        public string description;
        
        public CouncilPosition(string name, CivicData civic, int priority)
        {
            this.positionName = name;
            this.sourceCivic = civic;
            this.priority = priority;
            this.isOccupied = true;
            this.description = civic?.councilPosition?.title ?? civic?.civicName ?? "Unknown Position";
        }
    }

    /// <summary>
    /// Tracks penalties for removed civics
    /// </summary>
    [System.Serializable]
    public class CivicRemovalPenalty
    {
        public string civicName;
        public int satisfactionPenalty;
        public int moralePenaltyPerSeventh;
        public int remainingSevenths;
        public string source; // What caused the removal
        
        public CivicRemovalPenalty(string civic, int satisfaction, int morale, int duration, string source)
        {
            this.civicName = civic;
            this.satisfactionPenalty = satisfaction;
            this.moralePenaltyPerSeventh = morale;
            this.remainingSevenths = duration;
            this.source = source;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate CivicManager found, destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        LoadAvailableCivics();
        InitializeCouncilSystem();
        
        // Subscribe to time system for penalty processing
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange += OnSeventhTick;
        }
        else
        {
            StartCoroutine(SubscribeToTimeWhenReady());
        }
    }

    private System.Collections.IEnumerator SubscribeToTimeWhenReady()
    {
        while (TimeSystemLogic.Instance == null)
        {
            yield return null;
        }
        TimeSystemLogic.Instance.OnSeventhChange += OnSeventhTick;
    }

    private void OnDestroy()
    {
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange -= OnSeventhTick;
        }
    }

    private void Update()
    {
        // Debug input for testing civics system
        if (Input.GetKeyDown(KeyCode.C))
        {
            PrintCivicInfo();
        }
        
        if (Input.GetKeyDown(KeyCode.U))
        {
            // Test unlocking a civic
            bool success = UnlockCivic("Innovation Council", "Debug Input");
            if (enableCivicLogging)
            {
                Debug.Log($"[CivicManager] Debug unlock attempt: {(success ? "SUCCESS" : "FAILED")}");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Test removing a civic - only if it's actually active
            if (IsCivicActive("Innovation Council"))
            {
                bool success = RemoveCivic("Innovation Council", "Debug Input");
                if (enableCivicLogging)
                {
                    Debug.Log($"[CivicManager] Debug remove attempt: {(success ? "SUCCESS" : "FAILED")}");
                }
            }
            else
            {
                Debug.LogWarning($"[CivicManager] Debug remove failed: Innovation Council is not active");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.G))
        {
            // Test unlocking Guild Masters
            bool success = UnlockCivic("Guild Masters", "Debug Input");
            if (enableCivicLogging)
            {
                Debug.Log($"[CivicManager] Debug unlock attempt: {(success ? "SUCCESS" : "FAILED")}");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.F))
        {
            // Test unlocking Military Academy
            bool success = UnlockCivic("Military Academy", "Debug Input");
            if (enableCivicLogging)
            {
                Debug.Log($"[CivicManager] Debug unlock attempt: {(success ? "SUCCESS" : "FAILED")}");
            }
        }
    }

    /// <summary>
    /// Load all available civics from Resources/Civics folder
    /// </summary>
    private void LoadAvailableCivics()
    {
        availableCivics.Clear();
        CivicData[] civics = Resources.LoadAll<CivicData>("Civics");
        
        foreach (var civic in civics)
        {
            if (!availableCivics.ContainsKey(civic.civicName))
            {
                // Validate civic requirements during loading
                if (civic.requirements != null && civic.requirements.Count > 0)
                {
                    foreach (var requirement in civic.requirements)
                    {
                        var (isValid, errorMessage) = requirement.ValidateRequirement();
                        if (!isValid)
                        {
                            Debug.LogWarning($"[CivicManager] Invalid requirement in {civic.civicName}: {errorMessage}");
                        }
                    }
                }
                
                // Validate civic effects during loading
                if (civic.effects != null && civic.effects.Count > 0)
                {
                    foreach (var effect in civic.effects)
                    {
                        var (isValid, errorMessage) = effect.ValidateEffect();
                        if (!isValid)
                        {
                            Debug.LogWarning($"[CivicManager] Invalid effect in {civic.civicName}: {errorMessage}");
                        }
                    }
                }
                
                availableCivics[civic.civicName] = civic;
                if (enableCivicLogging)
                {
                    Debug.Log($"[CivicManager] Loaded civic: {civic.civicName} (Tier: {civic.tier}, Rarity: {civic.rarity})");
                }
            }
            else
            {
                Debug.LogWarning($"[CivicManager] Duplicate civic found: {civic.civicName}");
            }
        }
        
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Loaded {availableCivics.Count} civics from Resources");
        }
    }

    /// <summary>
    /// Initialize the council system with empty positions
    /// </summary>
    private void InitializeCouncilSystem()
    {
        if (!enableCouncilSystem) return;
        
        councilPositions.Clear();
        for (int i = 0; i < councilSize; i++)
        {
            councilPositions.Add(new CouncilPosition($"Position {i + 1}", null, int.MaxValue));
        }
        
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Initialized council with {councilSize} positions");
        }
    }

    /// <summary>
    /// Main function to unlock/equip a civic
    /// </summary>
    public bool UnlockCivic(string civicName, string source = "Manual")
    {
        if (!availableCivics.TryGetValue(civicName, out CivicData civic))
        {
            Debug.LogWarning($"[CivicManager] Civic not found: {civicName}");
            return false;
        }

        // Check if civic is already active
        if (IsCivicActive(civicName))
        {
            Debug.LogWarning($"[CivicManager] Civic already active: {civicName}");
            return false;
        }

        // Check requirements
        if (!CheckCivicRequirements(civic))
        {
            Debug.LogWarning($"[CivicManager] Civic requirements not met: {civicName}");
            return false;
        }

        // Check conflicts
        if (HasConflictingCivics(civic))
        {
            Debug.LogWarning($"[CivicManager] Civic conflicts with active civics: {civicName}");
            return false;
        }

        // Check if we have slots available
        if (!HasAvailableSlot(civic.tier))
        {
            Debug.LogWarning($"[CivicManager] No available slots for {civic.tier} tier civic: {civicName}");
            return false;
        }

        // Add the civic
        AddCivic(civic, source);
        
        // Notify listeners
        OnCivicChanged?.Invoke(civic, true);
        OnCivicSlotsChanged?.Invoke();
        OnActiveCivicsChanged?.Invoke();
        
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Successfully unlocked civic: {civicName} from '{source}'");
        }
        
        // Force a UI refresh to ensure the civic detailed displays appear immediately in the civic pool
        StartCoroutine(ForceUIRefreshAfterCivicUnlock(civic));
        
        return true;
    }

    /// <summary>
    /// Remove a civic (with penalties)
    /// </summary>
    public bool RemoveCivic(string civicName, string source = "Manual")
    {
        CivicData civic = GetActiveCivic(civicName);
        if (civic == null)
        {
            Debug.LogWarning($"[CivicManager] Civic not active: {civicName}");
            return false;
        }

        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Removing civic: {civicName} from '{source}'");
        }

        // Handle council seat replacement BEFORE removing the civic
        if (civic.grantsCouncilPosition)
        {
            // First unregister the civic from the council system to remove it from available pool
            if (GovernmentLogic.Instance != null)
            {
                GovernmentLogic.Instance.UnregisterCivicCouncilPosition(civic);
            }
            
            // Immediately trigger civic pool refresh to remove CivicDetailed objects
            if (enableCivicLogging)
            {
                Debug.Log($"[CivicManager] Immediately refreshing civic pool to remove CivicDetailed for '{civic.civicName}'");
            }
            OnCivicPoolChanged?.Invoke();
            
            // Then replace the seat with default (this triggers UI refresh)
            ReplaceCivicCouncilSeatWithDefault(civic);
        }

        // Remove from active list
        RemoveCivicFromList(civic);
        
        // Apply removal penalties
        ApplyRemovalPenalties(civic, source);
        
        // Notify listeners
        OnCivicChanged?.Invoke(civic, false);
        OnCivicSlotsChanged?.Invoke();
        OnActiveCivicsChanged?.Invoke();
        
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Successfully removed civic: {civicName} from '{source}'");
        }
        
        return true;
    }

    /// <summary>
    /// Check if a civic meets all requirements
    /// </summary>
    private bool CheckCivicRequirements(CivicData civic)
    {
        if (civic.requirements == null || civic.requirements.Count == 0)
            return true;

        foreach (var requirement in civic.requirements)
        {
            if (!CheckSingleRequirement(requirement))
            {
                if (enableCivicLogging)
                {
                    Debug.Log($"[CivicManager] Requirement not met: {requirement.GetAutoDescription()}");
                }
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Check a single requirement
    /// </summary>
    private bool CheckSingleRequirement(CivicRequirement requirement)
    {
        if (StatManager.Instance == null) return false;
        
        switch (requirement.requirementType)
        {
            case RequirementType.PillarStat:
                int pillarValue = StatManager.Instance.GetPillarValue(requirement.requirementTarget);
                return CompareValues(pillarValue, requirement.requiredValue, requirement.comparison);
                
            case RequirementType.SubstatStat:
                int substatValue = StatManager.Instance.GetSubstatValue(requirement.requirementTarget);
                return CompareValues(substatValue, requirement.requiredValue, requirement.comparison);
                
            case RequirementType.GovernmentType:
                if (GovernmentLogic.Instance == null) return false;
                var gov = GovernmentLogic.Instance.GetCurrentGovernment();
                return gov.type.ToString() == requirement.requirementTarget;
                
            case RequirementType.CivicPresent:
                return IsCivicActive(requirement.requirementTarget);
                
            case RequirementType.CivicAbsent:
                return !IsCivicActive(requirement.requirementTarget);
                
            case RequirementType.SatisfactionLevel:
                int satisfactionLevel = StatManager.Instance.GetSatisfactionLevel();
                return CompareValues(satisfactionLevel, requirement.requiredValue, requirement.comparison);
                
            case RequirementType.MoraleLevel:
                int morale = StatManager.Instance.GetMorale();
                return CompareValues(morale, requirement.requiredValue, requirement.comparison);
                
            case RequirementType.EraUnlock:
                // Future system - always return true for now
                return true;
                
            default:
                return false;
        }
    }

    /// <summary>
    /// Compare two values based on comparison type
    /// </summary>
    private bool CompareValues(float actual, float required, ComparisonType comparison)
    {
        switch (comparison)
        {
            case ComparisonType.GreaterThan: return actual > required;
            case ComparisonType.GreaterEqual: return actual >= required;
            case ComparisonType.Equal: return Mathf.Approximately(actual, required);
            case ComparisonType.LessEqual: return actual <= required;
            case ComparisonType.LessThan: return actual < required;
            case ComparisonType.NotEqual: return !Mathf.Approximately(actual, required);
            default: return false;
        }
    }

    /// <summary>
    /// Check if civic conflicts with active civics
    /// </summary>
    private bool HasConflictingCivics(CivicData civic)
    {
        if (civic.conflictingCivics == null || civic.conflictingCivics.Count == 0)
            return false;

        foreach (string conflictingCivic in civic.conflictingCivics)
        {
            if (IsCivicActive(conflictingCivic))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Check if we have available slots for a tier
    /// </summary>
    private bool HasAvailableSlot(CivicTier tier)
    {
        switch (tier)
        {
            case CivicTier.Aeonic:
                return activeAeonicCivics.Count < GetMaxSlots(CivicTier.Aeonic);
            case CivicTier.Major:
                return activeMajorCivics.Count < GetMaxSlots(CivicTier.Major);
            case CivicTier.Minor:
                return activeMinorCivics.Count < GetMaxSlots(CivicTier.Minor);
            default:
                return false;
        }
    }

    /// <summary>
    /// Get maximum slots for a tier
    /// </summary>
    private int GetMaxSlots(CivicTier tier)
    {
        switch (tier)
        {
            case CivicTier.Aeonic: return maxAeonicSlots;
            case CivicTier.Major: return maxMajorSlots;
            case CivicTier.Minor: return maxMinorSlots;
            default: return 0;
        }
    }

    /// <summary>
    /// Add a civic to the active list
    /// </summary>
    private void AddCivic(CivicData civic, string source)
    {
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Adding civic '{civic.civicName}' (Tier: {civic.tier}) from '{source}'");
        }
        
        switch (civic.tier)
        {
            case CivicTier.Aeonic:
                activeAeonicCivics.Add(civic);
                break;
            case CivicTier.Major:
                activeMajorCivics.Add(civic);
                break;
            case CivicTier.Minor:
                activeMinorCivics.Add(civic);
                break;
        }

        // Verify the civic was added
        if (!IsCivicActive(civic.civicName))
        {
            Debug.LogError($"[CivicManager] Failed to add civic '{civic.civicName}' - not found in active lists after addition");
            return;
        }

        // Add council position if granted
        if (civic.grantsCouncilPosition)
        {
            AddCouncilPosition(civic);
        }

        // Apply civic effects
        ApplyCivicEffects(civic, true);
        
        // Trigger immediate council recalculation to reflect civic effects
        if (GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.ProcessAllSeatBonuses();
        }
        
        // Register with GovernmentLogic if this civic grants a council position
        if (civic.grantsCouncilPosition && GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.RegisterCivicCouncilPosition(civic);
        }
        
        // Notify listeners
        OnCivicChanged?.Invoke(civic, true);
        OnCivicSlotsChanged?.Invoke();
        OnActiveCivicsChanged?.Invoke();
        
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Successfully added civic '{civic.civicName}' to active {civic.tier} civics");
        }
    }

    /// <summary>
    /// Remove a civic from the active list
    /// </summary>
    private void RemoveCivicFromList(CivicData civic)
    {
        switch (civic.tier)
        {
            case CivicTier.Aeonic:
                activeAeonicCivics.Remove(civic);
                break;
            case CivicTier.Major:
                activeMajorCivics.Remove(civic);
                break;
            case CivicTier.Minor:
                activeMinorCivics.Remove(civic);
                break;
        }

        // Remove civic effects
        ApplyCivicEffects(civic, false);
        
        // Clear all bonuses from this civic source
        if (StatManager.Instance != null)
        {
            StatManager.Instance.ClearBonusesFromSource($"Civic: {civic.civicName}");
        }
        
        // Trigger immediate council recalculation to reflect civic effect removal
        if (GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.ProcessAllSeatBonuses();
        }
        
        // Clear housing bonuses from this civic source
        if (PopGrowthLogic.Instance != null)
        {
            PopGrowthLogic.Instance.ClearHousingBonusesFromSource($"Civic: {civic.civicName}");
        }
        
        // Clear production scaling bonuses from this civic source
        if (GameUnitsLogic.Instance != null)
        {
            GameUnitsLogic.Instance.ClearProductionScalingBonusesFromSource($"Civic: {civic.civicName}");
        }
        
        // Unregister from GovernmentLogic if this civic granted a council position
        // Note: This is now handled earlier in RemoveCivic() before seat replacement
        // to ensure UI refresh shows the correct available seats
        
        // Remove from active civics list
        // This line was removed as per the edit hint, as the civic was already removed from its list.
        // activeCivics.Remove(civic); 
        
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Removed {civic.civicName} and cleared all associated bonuses");
        }
    }

    /// <summary>
    /// Apply or remove civic effects
    /// </summary>
    private void ApplyCivicEffects(CivicData civic, bool isAdding)
    {
        if (StatManager.Instance == null) return;
        
        foreach (var effect in civic.effects)
        {
            // Validate effect before applying
            var (isValid, errorMessage) = effect.ValidateEffect();
            if (!isValid)
            {
                Debug.LogWarning($"[CivicManager] Invalid effect in {civic.civicName}: {errorMessage}");
                continue;
            }
            
            // Validate bonus value before applying
            if (!ValidateBonusValue(effect.modifierValue, effect.effectType))
            {
                Debug.LogWarning($"[CivicManager] Invalid bonus value {effect.modifierValue} for {effect.effectType} in {civic.civicName}");
                continue;
            }
            
            float value = isAdding ? effect.modifierValue : -effect.modifierValue;
            
            switch (effect.effectType)
            {
                case GameEffectType.PillarBonus:
                    // Apply to pillar stats using the persistent bonus system
                    if (!string.IsNullOrEmpty(effect.targetStat))
                    {
                        if (isAdding)
                        {
                            StatManager.Instance.AddPillarBonus(effect.targetStat, effect.modifierValue, $"Civic: {civic.civicName}");
                        }
                        else
                        {
                            StatManager.Instance.RemovePillarBonus(effect.targetStat, $"Civic: {civic.civicName}");
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            Debug.Log($"[CivicManager] {action} pillar effect: {effect.targetStat} +{effect.modifierValue} ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.SubstatBonus:
                    // Apply to substats using the persistent bonus system
                    if (!string.IsNullOrEmpty(effect.targetStat))
                    {
                        if (isAdding)
                        {
                            StatManager.Instance.AddSubstatBonus(effect.targetStat, effect.modifierValue, $"Civic: {civic.civicName}");
                        }
                        else
                        {
                            StatManager.Instance.RemoveSubstatBonus(effect.targetStat, $"Civic: {civic.civicName}");
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            Debug.Log($"[CivicManager] {action} substat effect: {effect.targetStat} +{effect.modifierValue} ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.DerivedStatBonus:
                    // Apply to derived stats using the persistent bonus system
                    if (!string.IsNullOrEmpty(effect.targetStat))
                    {
                        if (isAdding)
                        {
                            StatManager.Instance.AddDerivedStatBonus(effect.targetStat, effect.modifierValue, $"Civic: {civic.civicName}");
                        }
                        else
                        {
                            StatManager.Instance.RemoveDerivedStatBonus(effect.targetStat, $"Civic: {civic.civicName}");
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            Debug.Log($"[CivicManager] {action} derived stat effect: {effect.targetStat} +{effect.modifierValue} ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.MaxMoraleModifier:
                    // Apply max morale modifier (more room for positive morale)
                    if (effect.modifierValue != 0)
                    {
                        if (isAdding)
                        {
                            StatManager.Instance.AddGlobalBonus("maxMorale", Mathf.RoundToInt(effect.modifierValue), $"Civic: {civic.civicName}");
                        }
                        else
                        {
                            StatManager.Instance.RemoveGlobalBonus("maxMorale", $"Civic: {civic.civicName}");
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            Debug.Log($"[CivicManager] {action} max morale effect: +{effect.modifierValue} ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.MoraleBalanceModifier:
                    // Apply morale balance modifier (easier to stay above balance)
                    if (effect.modifierValue != 0)
                    {
                        if (isAdding)
                        {
                            // Decrease morale balance (easier to stay above balance)
                            StatManager.Instance.AddGlobalBonus("moraleBalance", -Mathf.RoundToInt(effect.modifierValue), $"Civic: {civic.civicName}");
                        }
                        else
                        {
                            // Remove morale balance bonus (restore original balance)
                            StatManager.Instance.RemoveGlobalBonus("moraleBalance", $"Civic: {civic.civicName}");
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            Debug.Log($"[CivicManager] {action} morale balance effect: Balance -{effect.modifierValue} ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.SatisfactionThresholdModifier:
                    // Apply satisfaction threshold modifier using the persistent bonus system
                    if (effect.modifierValue != 0)
                    {
                        if (isAdding)
                        {
                            StatManager.Instance.AddGlobalBonus("satisfactionUpgradeThreshold", Mathf.RoundToInt(effect.modifierValue), $"Civic: {civic.civicName}");
                        }
                        else
                        {
                            StatManager.Instance.RemoveGlobalBonus("satisfactionUpgradeThreshold", $"Civic: {civic.civicName}");
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            Debug.Log($"[CivicManager] {action} satisfaction threshold effect: +{effect.modifierValue} ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.ResourceModifier:
                    // Apply resource production modifier using GlobalProductionManager
                    if (GlobalProductionManager.Instance != null)
                    {
                        if (effect.scope == ScopeType.Global)
                        {
                            // Apply to all resources
                            GlobalProductionManager.Instance.AdjustPercentageModifierForSection("", effect.modifierValue, effect.modifierValue > 0, isAdding, $"Civic: {civic.civicName}");
                        }
                        else if (effect.scope == ScopeType.Section && !string.IsNullOrEmpty(effect.targetStat))
                        {
                            // Apply to specific section
                            GlobalProductionManager.Instance.AdjustPercentageModifierForSection(effect.targetStat, effect.modifierValue, effect.modifierValue > 0, isAdding, $"Civic: {civic.civicName}");
                        }
                        else if (!string.IsNullOrEmpty(effect.targetStat))
                        {
                            // Apply to specific resource
                            GlobalProductionManager.Instance.AdjustPercentageModifier(
                                effect.targetStat, 
                                effect.modifierValue, 
                                effect.modifierValue > 0, 
                                isAdding, 
                                $"Civic: {civic.civicName}"
                            );
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            string target = effect.scope == ScopeType.Global ? "all resources" : 
                                         effect.scope == ScopeType.Section ? $"{effect.targetStat} section" : 
                                         effect.targetStat;
                            Debug.Log($"[CivicManager] {action} resource modifier: {target} +{effect.modifierValue}% ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.ProductionModifier:
                    // Apply production efficiency modifier using GameUnitsLogic
                    if (GameUnitsLogic.Instance != null)
                    {
                        if (effect.scope == ScopeType.Global)
                        {
                            // Apply to all building types
                            GameUnitsLogic.Instance.AdjustProductionModifierByType("Building", effect.modifierValue, isAdding, $"Civic: {civic.civicName}");
                            GameUnitsLogic.Instance.AdjustProductionModifierByType("Unit", effect.modifierValue, isAdding, $"Civic: {civic.civicName}");
                        }
                        else if (effect.scope == ScopeType.Section && !string.IsNullOrEmpty(effect.targetStat))
                        {
                            // Apply to specific section
                            GameUnitsLogic.Instance.AdjustProductionModifierBySection(effect.targetStat, effect.modifierValue, isAdding, $"Civic: {civic.civicName}");
                        }
                        else if (!string.IsNullOrEmpty(effect.targetStat))
                        {
                            if (effect.targetStat.ToLower() == "building" || effect.targetStat.ToLower() == "unit")
                            {
                                // Apply to specific building type
                                GameUnitsLogic.Instance.AdjustProductionModifierByType(effect.targetStat, effect.modifierValue, isAdding, $"Civic: {civic.civicName}");
                            }
                            else
                            {
                                // Apply to specific section
                                GameUnitsLogic.Instance.AdjustProductionModifierBySection(effect.targetStat, effect.modifierValue, isAdding, $"Civic: {civic.civicName}");
                            }
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            string target = effect.scope == ScopeType.Global ? "all buildings" : 
                                         effect.scope == ScopeType.Section ? $"{effect.targetStat} section" : 
                                         effect.targetStat;
                            Debug.Log($"[CivicManager] {action} production modifier: {target} +{effect.modifierValue}% ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.ClickPowerBonus:
                    // Apply click power modifier using GameUnitsLogic
                    if (GameUnitsLogic.Instance != null)
                    {
                        if (effect.scope == ScopeType.Global)
                        {
                            // Apply to all resources
                            GameUnitsLogic.Instance.AdjustClickPowerPercentForSection("", effect.modifierValue);
                        }
                        else if (effect.scope == ScopeType.Section && !string.IsNullOrEmpty(effect.targetStat))
                        {
                            // Apply to specific section
                            GameUnitsLogic.Instance.AdjustClickPowerPercentForSection(effect.targetStat, effect.modifierValue);
                        }
                        else if (!string.IsNullOrEmpty(effect.targetStat))
                        {
                            // Apply to specific resource
                            GameUnitsLogic.Instance.AdjustClickPowerPercent(effect.targetStat, effect.modifierValue);
                        }
                        
                        if (enableCivicLogging)
                        {
                            string action = isAdding ? "Applied" : "Removed";
                            string target = effect.scope == ScopeType.Global ? "all resources" : 
                                         effect.scope == ScopeType.Section ? $"{effect.targetStat} section" : 
                                         effect.targetStat;
                            Debug.Log($"[CivicManager] {action} click power modifier: {target} +{effect.modifierValue}% ({effect.GetAutoDescription()})");
                        }
                    }
                    break;
                    
                case GameEffectType.HousingBonus:
                    // Apply housing bonus using PopGrowthLogic persistent system
                    if (effect.modifierValue != 0)
                    {
                        if (PopGrowthLogic.Instance != null)
                        {
                            if (isAdding)
                            {
                                PopGrowthLogic.Instance.AddHousingBonus(Mathf.RoundToInt(effect.modifierValue), $"Civic: {civic.civicName}");
                            }
                            else
                            {
                                PopGrowthLogic.Instance.RemoveHousingBonus($"Civic: {civic.civicName}");
                            }
                            
                            if (enableCivicLogging)
                            {
                                string action = isAdding ? "Applied" : "Removed";
                                Debug.Log($"[CivicManager] {action} housing bonus: +{effect.modifierValue} ({effect.GetAutoDescription()})");
                            }
                        }
                    }
                    break;
                    
                case GameEffectType.ProductionScalingBonus:
                    // Apply production scaling bonus (bonus per production unit)
                    if (!string.IsNullOrEmpty(effect.targetStat) && effect.modifierValue > 0)
                    {
                        if (GameUnitsLogic.Instance != null)
                        {
                            // Use conditionStat if available, otherwise use targetStat as the production unit
                            string productionUnitName = !string.IsNullOrEmpty(effect.conditionStat) ? effect.conditionStat : effect.targetStat;
                            
                            if (isAdding)
                            {
                                GameUnitsLogic.Instance.AddProductionScalingBonus(productionUnitName, effect.modifierValue, $"Civic: {civic.civicName}");
                            }
                            else
                            {
                                GameUnitsLogic.Instance.RemoveProductionScalingBonus(productionUnitName, $"Civic: {civic.civicName}");
                            }
                            
                            if (enableCivicLogging)
                            {
                                string action = isAdding ? "Applied" : "Removed";
                                string description = !string.IsNullOrEmpty(effect.conditionStat) 
                                    ? $"+{effect.modifierValue} {effect.targetStat} per {effect.conditionStat}"
                                    : $"+{effect.modifierValue} {effect.targetStat} per production unit";
                                Debug.Log($"[CivicManager] {action} production scaling bonus: {description} ({effect.GetAutoDescription()})");
                            }
                        }
                    }
                    break;
                    
                case GameEffectType.SpecialAbility:
                    // Handle special abilities
                    if (enableCivicLogging)
                    {
                        string action = isAdding ? "Applied" : "Removed";
                        Debug.Log($"[CivicManager] {action} special ability: {effect.GetAutoDescription()}");
                    }
                    break;
            }
        }
    }
    
    /// <summary>
    /// Validate bonus values before applying to prevent broken game balance
    /// </summary>
    private bool ValidateBonusValue(float value, GameEffectType effectType)
    {
        switch (effectType)
        {
            case GameEffectType.PillarBonus:
                return value >= -50 && value <= 100; // Reasonable pillar limits
                
            case GameEffectType.SubstatBonus:
                return value >= -30 && value <= 75; // Reasonable substat limits
                
            case GameEffectType.DerivedStatBonus:
                // Allow percentage values (e.g., 25 = 25%, 100 = 100%)
                // Also allow small decimal values for fine-tuning
                return value >= -100 && value <= 500; // Reasonable derived stat limits
                
            case GameEffectType.MaxMoraleModifier:
                return value >= 0 && value <= 500; // Only positive, reasonable max morale
                
            case GameEffectType.MoraleBalanceModifier:
                return value >= 0 && value <= 200; // Only positive, reasonable balance reduction
                
            case GameEffectType.SatisfactionThresholdModifier:
                return value >= -100 && value <= 200; // Reasonable satisfaction threshold
                
            case GameEffectType.ResourceModifier:
                return value >= -90 && value <= 500; // Reasonable resource production limits
                
            case GameEffectType.ProductionModifier:
                return value >= -90 && value <= 500; // Reasonable production efficiency limits
                
            case GameEffectType.ClickPowerBonus:
                return value >= -90 && value <= 1000; // Reasonable click power limits
                
            case GameEffectType.HousingBonus:
                return value >= 0 && value <= 1000; // Only positive, reasonable housing limits
                
            case GameEffectType.ProductionScalingBonus:
                return value > 0 && value <= 100; // Only positive, reasonable scaling limits
                
            case GameEffectType.SpecialAbility:
                return true; // Special abilities don't have numeric validation
                
            default:
                return false; // Unknown effect type
        }
    }

    /// <summary>
    /// Apply removal penalties
    /// </summary>
    private void ApplyRemovalPenalties(CivicData civic, string source)
    {
        if (civic.satisfactionPenalty != 0 || civic.moralePenaltyPerSeventh != 0)
        {
            // Apply immediate satisfaction penalty
            if (civic.satisfactionPenalty != 0)
            {
                StatManager.Instance.ModifySatisfactionPoints(civic.satisfactionPenalty, $"Civic Removal: {civic.civicName}");
            }

            // Track ongoing morale penalty
            if (civic.moralePenaltyPerSeventh != 0 && civic.removalPenaltyDuration > 0)
            {
                removalPenalties[civic.civicName] = new CivicRemovalPenalty(
                    civic.civicName, 
                    civic.satisfactionPenalty, 
                    civic.moralePenaltyPerSeventh, 
                    civic.removalPenaltyDuration, 
                    source
                );
            }
        }
    }

    /// <summary>
    /// Add a council position for a civic
    /// </summary>
    private void AddCouncilPosition(CivicData civic)
    {
        if (!enableCouncilSystem) return;

        // For the new GovernmentLogic system, we just need to ensure the civic is registered
        // The actual council seat creation happens in GovernmentLogic.RegisterCivicCouncilPosition
        if (enableCivicLogging)
        {
            string positionName = civic.councilPosition?.title ?? civic.civicName;
            Debug.Log($"[CivicManager] Civic {civic.civicName} grants council position: {positionName}");
            Debug.Log($"[CivicManager] Council position will be registered with GovernmentLogic system");
        }
        
        // The old councilPositions system is deprecated - we use GovernmentLogic now
        // This method is kept for compatibility but no longer manages actual positions
    }
    

    
    /// <summary>
    /// Convert civic effect type to seat bonus type
    /// </summary>
    private SeatBonusType ConvertCivicEffectToSeatBonus(GameEffectType civicEffectType)
    {
        switch (civicEffectType)
        {
            case GameEffectType.PillarBonus: return SeatBonusType.PillarBonus;
            case GameEffectType.SubstatBonus: return SeatBonusType.SubstatBonus;
            case GameEffectType.DerivedStatBonus: return SeatBonusType.DerivedStatBonus;
            case GameEffectType.ResourceModifier: return SeatBonusType.ResourceModifier;
            case GameEffectType.ProductionModifier: return SeatBonusType.ProductionModifier;
            case GameEffectType.ClickPowerBonus: return SeatBonusType.ClickPowerBonus;
            case GameEffectType.MaxMoraleModifier: return SeatBonusType.MaxMoraleModifier;
            case GameEffectType.MoraleBalanceModifier: return SeatBonusType.MoraleBalanceModifier;
            case GameEffectType.SatisfactionThresholdModifier: return SeatBonusType.SatisfactionThresholdModifier;
            case GameEffectType.HousingBonus: return SeatBonusType.HousingBonus;
            case GameEffectType.ProductionScalingBonus: return SeatBonusType.ProductionScalingBonus;
            default: return SeatBonusType.CivicBonus;
        }
    }

    /// <summary>
    /// Replace a civic's council seat with a default seat when the civic is removed
    /// This ensures the council seat is properly replaced and any assigned legend becomes unassigned
    /// </summary>
    private void ReplaceCivicCouncilSeatWithDefault(CivicData civic)
    {
        if (GovernmentLogic.Instance == null) return;

        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Replacing civic council seat for '{civic.civicName}' with default seat");
        }

        // Find which position this civic seat occupies
        int position = GovernmentLogic.Instance.GetSeatPosition(civic.councilPosition?.title ?? civic.civicName);
        
        if (position >= 0)
        {
            // Get the current seat at this position
            var currentSeat = GovernmentLogic.Instance.GetSeatAtPosition(position);
            
            if (currentSeat != null && currentSeat.sourceCivic == civic)
            {
                if (enableCivicLogging)
                {
                    Debug.Log($"[CivicManager] Found civic seat '{civic.civicName}' at position {position} with legend: {currentSeat.assignedLegend?.legendName ?? "none"}");
                }
                
                // Remove any assigned legend from this seat
                if (currentSeat.assignedLegend != null)
                {
                    if (enableCivicLogging)
                    {
                        Debug.Log($"[CivicManager] Removing assigned legend '{currentSeat.assignedLegend.legendName}' from civic seat '{civic.civicName}'");
                    }
                    
                    // Remove the legend assignment
                    GovernmentLogic.Instance.RemoveLegendFromSeat(position);
                }

                // Replace the civic seat with a default seat
                bool replaced = GovernmentLogic.Instance.ResetSeatToDefault(position);
                
                if (replaced)
                {
                    if (enableCivicLogging)
                    {
                        Debug.Log($"[CivicManager] Successfully replaced civic seat '{civic.civicName}' with default seat at position {position}");
                        
                        // Verify the replacement worked
                        var newSeat = GovernmentLogic.Instance.GetSeatAtPosition(position);
                        if (newSeat != null)
                        {
                            string newTitle = newSeat.GetEffectiveTitle();
                            string newCivic = newSeat.sourceCivic?.civicName ?? "none";
                            string newLegend = newSeat.assignedLegend?.legendName ?? "none";
                            
                            Debug.Log($"[CivicManager] Verification - New seat at position {position}: Title='{newTitle}', Civic='{newCivic}', Legend='{newLegend}'");
                        }
                    }
                    
                    // Force a UI refresh to ensure the display is updated immediately
                    StartCoroutine(ForceUIRefreshAfterSeatReplacement());
                }
                else
                {
                    Debug.LogWarning($"[CivicManager] Failed to replace civic seat '{civic.civicName}' with default seat at position {position}");
                }
            }
            else
            {
                if (enableCivicLogging)
                {
                    Debug.Log($"[CivicManager] Civic seat '{civic.civicName}' not found at position {position} or doesn't match source civic");
                    if (currentSeat != null)
                    {
                        Debug.Log($"[CivicManager] Current seat at position {position}: Title='{currentSeat.GetEffectiveTitle()}', Civic='{currentSeat.sourceCivic?.civicName ?? "none"}'");
                    }
                }
            }
        }
        else
        {
            if (enableCivicLogging)
            {
                Debug.Log($"[CivicManager] Civic seat '{civic.civicName}' position not found in GovernmentLogic system");
            }
        }
    }
    
    /// <summary>
    /// Force a UI refresh after seat replacement to ensure immediate visual update
    /// </summary>
    private System.Collections.IEnumerator ForceUIRefreshAfterSeatReplacement()
    {
        // Wait for the end of frame to ensure all seat replacement logic is complete
        yield return new WaitForEndOfFrame();
        
        // Force GovernmentLogic to trigger final UI update events
        if (GovernmentLogic.Instance != null)
        {
            if (enableCivicLogging)
            {
                Debug.Log($"[CivicManager] Final UI refresh after seat replacement");
            }
            
            // Trigger the council composition changed event to force final UI refresh
            GovernmentLogic.Instance.ForceRecalculation();
        }
    }
    
    /// <summary>
    /// Force a UI refresh after civic unlock to ensure immediate visual update in civic pool
    /// </summary>
    private System.Collections.IEnumerator ForceUIRefreshAfterCivicUnlock(CivicData civic)
    {
        // Wait for the end of frame to ensure all civic unlock logic is complete
        yield return new WaitForEndOfFrame();
        
        if (enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Forcing UI refresh after civic unlock: {civic.civicName}");
        }
        
        // Trigger civic pool changed event to force UI refresh
        OnCivicPoolChanged?.Invoke();
        
        // Also trigger government recalculation if this civic grants a council position
        if (civic.grantsCouncilPosition && GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.ForceRecalculation();
        }
    }

    /// <summary>
    /// Remove a council position for a civic (Legacy method - now handled by ReplaceCivicCouncilSeatWithDefault)
    /// </summary>
    private void RemoveCouncilPosition(CivicData civic)
    {
        if (!enableCouncilSystem) return;

        foreach (var position in councilPositions)
        {
            if (position.sourceCivic == civic)
            {
                position.sourceCivic = null;
                position.isOccupied = false;
                position.positionName = $"Position {councilPositions.IndexOf(position) + 1}";
                position.description = "Empty Position";
                position.priority = int.MaxValue;
                
                OnCouncilPositionChanged?.Invoke(position);
                break;
            }
        }
    }

    /// <summary>
    /// Process time-based penalties
    /// </summary>
    private void OnSeventhTick(int newSeventh)
    {
        if (removalPenalties.Count == 0) return;

        var toRemove = new List<string>();
        
        foreach (var penalty in removalPenalties.Values)
        {
            // Apply morale penalty
            if (penalty.moralePenaltyPerSeventh != 0 && StatManager.Instance != null)
            {
                StatManager.Instance.ApplyMoraleShift(penalty.moralePenaltyPerSeventh, $"Civic Removal Penalty: {penalty.civicName}");
            }

            // Decrease duration
            penalty.remainingSevenths--;
            
            if (penalty.remainingSevenths <= 0)
            {
                toRemove.Add(penalty.civicName);
            }
        }

        // Remove expired penalties
        foreach (var civicName in toRemove)
        {
            removalPenalties.Remove(civicName);
        }

        if (toRemove.Count > 0 && enableCivicLogging)
        {
            Debug.Log($"[CivicManager] Expired removal penalties for: {string.Join(", ", toRemove)}");
        }
    }

    // Public getter methods
    public bool IsCivicActive(string civicName) => GetActiveCivic(civicName) != null;
    
    public CivicData GetActiveCivic(string civicName)
    {
        return activeAeonicCivics.FirstOrDefault(c => c.civicName == civicName) ??
               activeMajorCivics.FirstOrDefault(c => c.civicName == civicName) ??
               activeMinorCivics.FirstOrDefault(c => c.civicName == civicName);
    }
    
    public List<CivicData> GetActiveCivics(CivicTier tier)
    {
        switch (tier)
        {
            case CivicTier.Aeonic: return new List<CivicData>(activeAeonicCivics);
            case CivicTier.Major: return new List<CivicData>(activeMajorCivics);
            case CivicTier.Minor: return new List<CivicData>(activeMinorCivics);
            default: return new List<CivicData>();
        }
    }
    
    public List<CivicData> GetAllActiveCivics()
    {
        var allCivics = new List<CivicData>();
        allCivics.AddRange(activeAeonicCivics);
        allCivics.AddRange(activeMajorCivics);
        allCivics.AddRange(activeMinorCivics);
        return allCivics;
    }
    
    public List<CouncilPosition> GetCouncilPositions() => new List<CouncilPosition>(councilPositions);
    
    public int GetActiveCivicCount(CivicTier tier)
    {
        switch (tier)
        {
            case CivicTier.Aeonic: return activeAeonicCivics.Count;
            case CivicTier.Major: return activeMajorCivics.Count;
            case CivicTier.Minor: return activeMinorCivics.Count;
            default: return 0;
        }
    }
    
    public int GetMaxCivicSlots(CivicTier tier) => GetMaxSlots(tier);
    
    public int GetAvailableSlots(CivicTier tier) => GetMaxSlots(tier) - GetActiveCivicCount(tier);
    
    /// <summary>
    /// Get starting slots for a tier (for new games)
    /// </summary>
    public int GetStartingSlots(CivicTier tier)
    {
        switch (tier)
        {
            case CivicTier.Aeonic: return startingAeonicSlots;
            case CivicTier.Major: return startingMajorSlots;
            case CivicTier.Minor: return startingMinorSlots;
            default: return 0;
        }
    }
    
    /// <summary>
    /// Check if a tier has reached its starting slot limit
    /// </summary>
    public bool HasReachedStartingLimit(CivicTier tier)
    {
        return GetActiveCivicCount(tier) >= GetStartingSlots(tier);
    }
    
    /// <summary>
    /// Check if a tier can use starting slots (for new game progression)
    /// </summary>
    public bool CanUseStartingSlots(CivicTier tier)
    {
        return GetActiveCivicCount(tier) < GetStartingSlots(tier);
    }
    
    /// <summary>
    /// Get remaining starting slots for a tier
    /// </summary>
    public int GetRemainingStartingSlots(CivicTier tier)
    {
        return Mathf.Max(0, GetStartingSlots(tier) - GetActiveCivicCount(tier));
    }

    /// <summary>
    /// Check if a civic can be unlocked with current conditions
    /// </summary>
    public (bool canUnlock, List<string> reasons) CanUnlockCivic(string civicName)
    {
        var reasons = new List<string>();
        
        if (!availableCivics.TryGetValue(civicName, out CivicData civic))
        {
            reasons.Add("Civic not found");
            return (false, reasons);
        }

        // Check if already active
        if (IsCivicActive(civicName))
        {
            reasons.Add("Civic already active");
            return (false, reasons);
        }

        // Check requirements
        if (civic.requirements != null && civic.requirements.Count > 0)
        {
            foreach (var requirement in civic.requirements)
            {
                if (!CheckSingleRequirement(requirement))
                {
                    reasons.Add($"Requirement not met: {requirement.GetAutoDescription()}");
                }
            }
        }

        // Check conflicts
        if (HasConflictingCivics(civic))
        {
            reasons.Add("Conflicts with active civics");
        }

        // Check slots
        if (!HasAvailableSlot(civic.tier))
        {
            reasons.Add($"No available {civic.tier} tier slots");
        }

        bool canUnlock = reasons.Count == 0;
        return (canUnlock, reasons);
    }

    /// <summary>
    /// Get all civics that can currently be unlocked
    /// </summary>
    public List<CivicData> GetUnlockableCivics()
    {
        var unlockable = new List<CivicData>();
        
        foreach (var civic in availableCivics.Values)
        {
            if (!IsCivicActive(civic.civicName))
            {
                var (canUnlock, _) = CanUnlockCivic(civic.civicName);
                if (canUnlock)
                {
                    unlockable.Add(civic);
                }
            }
        }
        
        return unlockable;
    }
    
    /// <summary>
    /// Get all active bonuses from civics for a specific stat type
    /// </summary>
    /// <param name="statType">Type of stat ("pillar", "substat", "derived", "global")</param>
    /// <param name="statName">Name of the stat</param>
    /// <returns>Dictionary of civic names and their bonus values</returns>
    public Dictionary<string, float> GetCivicBonuses(string statType, string statName)
    {
        var civicBonuses = new Dictionary<string, float>();
        
        if (StatManager.Instance == null) return civicBonuses;
        
        var allSources = StatManager.Instance.GetBonusSources(statType, statName);
        
        foreach (var source in allSources)
        {
            if (source.Key.StartsWith("Civic: "))
            {
                string civicName = source.Key.Substring(7); // Remove "Civic: " prefix
                civicBonuses[civicName] = source.Value;
            }
        }
        
        return civicBonuses;
    }
    
    /// <summary>
    /// Get total bonus from all active civics for a specific stat type
    /// </summary>
    /// <param name="statType">Type of stat ("pillar", "substat", "derived", "global")</param>
    /// <param name="statName">Name of the stat</param>
    /// <returns>Total bonus value from all civics</returns>
    public float GetTotalCivicBonus(string statType, string statName)
    {
        var civicBonuses = GetCivicBonuses(statType, statName);
        return civicBonuses.Values.Sum();
    }
    
    /// <summary>
    /// Check if a civic is providing a bonus to a specific stat
    /// </summary>
    /// <param name="civicName">Name of the civic</param>
    /// <param name="statType">Type of stat ("pillar", "substat", "derived", "global")</param>
    /// <param name="statName">Name of the stat</param>
    /// <returns>True if the civic provides a bonus to this stat</returns>
    public bool IsCivicProvidingBonus(string civicName, string statType, string statName)
    {
        var civicBonuses = GetCivicBonuses(statType, statName);
        return civicBonuses.ContainsKey(civicName);
    }

    /// <summary>
    /// Debug method to print all civic information
    /// </summary>
    [ContextMenu("Print Civic Info")]
    public void PrintCivicInfo()
    {
        if (!enableCivicLogging) return;

        Debug.Log("=== CIVIC SYSTEM INFO ===");
        Debug.Log($"Available Civics: {availableCivics.Count}");
        Debug.Log($"Active Aeonic: {activeAeonicCivics.Count}/{maxAeonicSlots} (Starting: {startingAeonicSlots})");
        Debug.Log($"Active Major: {activeMajorCivics.Count}/{maxMajorSlots} (Starting: {startingMajorSlots})");
        Debug.Log($"Active Minor: {activeMinorCivics.Count}/{maxMinorSlots} (Starting: {startingMinorSlots})");
        
        Debug.Log("=== ACTIVE CIVICS ===");
        foreach (var civic in GetAllActiveCivics())
        {
            Debug.Log($"  {civic.civicName} ({civic.tier}) - {civic.description}");
            
            // Show active bonuses from this civic
            if (StatManager.Instance != null)
            {
                foreach (var effect in civic.effects)
                {
                    if (effect.effectType == GameEffectType.PillarBonus && !string.IsNullOrEmpty(effect.targetStat))
                    {
                        var sources = StatManager.Instance.GetBonusSources("pillar", effect.targetStat);
                        if (sources.ContainsKey($"Civic: {civic.civicName}"))
                        {
                            Debug.Log($"    → Pillar Bonus: {effect.targetStat} +{effect.modifierValue}");
                        }
                    }
                    else if (effect.effectType == GameEffectType.SubstatBonus && !string.IsNullOrEmpty(effect.targetStat))
                    {
                        var sources = StatManager.Instance.GetBonusSources("substat", effect.targetStat);
                        if (sources.ContainsKey($"Civic: {civic.civicName}"))
                        {
                            Debug.Log($"    → Substat Bonus: {effect.targetStat} +{effect.modifierValue}");
                        }
                    }
                    else if (effect.effectType == GameEffectType.DerivedStatBonus && !string.IsNullOrEmpty(effect.targetStat))
                    {
                        var sources = StatManager.Instance.GetBonusSources("derived", effect.targetStat);
                        if (sources.ContainsKey($"Civic: {civic.civicName}"))
                        {
                            Debug.Log($"    → Derived Stat Bonus: {effect.targetStat} +{effect.modifierValue}");
                        }
                    }
                }
            }
        }
        
        Debug.Log("=== COUNCIL POSITIONS ===");
        foreach (var position in councilPositions)
        {
            string status = position.isOccupied ? $"Occupied by {position.sourceCivic?.civicName}" : "Empty";
            Debug.Log($"  {position.positionName}: {status}");
        }
        
        Debug.Log("=== REMOVAL PENALTIES ===");
        foreach (var penalty in removalPenalties.Values)
        {
            Debug.Log($"  {penalty.civicName}: {penalty.moralePenaltyPerSeventh} morale per seventh, {penalty.remainingSevenths} sevenths remaining");
        }
        
        // Show total civic bonuses
        if (StatManager.Instance != null)
        {
            Debug.Log("=== TOTAL CIVIC BONUSES ===");
            
            // Pillar bonuses
            foreach (var pillar in new[] { "aureus", "regalia", "waltz", "chorus" })
            {
                float totalBonus = GetTotalCivicBonus("pillar", pillar);
                if (totalBonus > 0)
                {
                    Debug.Log($"  {pillar}: +{totalBonus:F1} total from civics");
                }
            }
            
            // Substat bonuses
            foreach (var substat in new[] { "innovation", "piety", "authority", "ambition", "symphony", "euphony", "arcane", "secrecy" })
            {
                float totalBonus = GetTotalCivicBonus("substat", substat);
                if (totalBonus > 0)
                {
                    Debug.Log($"  {substat}: +{totalBonus:F1} total from civics");
                }
            }
            
            // Derived stat bonuses
            foreach (var derived in new[] { "discoveryEfficiency", "savingRollChance", "legendEffectiveness", "expeditionCostMod", "expeditionTimeMod", "satisfactionEffectiveness", "moraleLossMod", "moraleRecoveryMod", "clickPowerBonus", "magicEffectiveness" })
            {
                float totalBonus = GetTotalCivicBonus("derived", derived);
                if (totalBonus > 0)
                {
                    Debug.Log($"  {derived}: +{totalBonus:F1} total from civics");
                }
            }
            
            // Global stat bonuses (morale, satisfaction)
            var maxMoraleBonus = GetTotalCivicBonus("global", "maxMorale");
            var moraleBalanceBonus = GetTotalCivicBonus("global", "moraleBalance");
            var satisfactionBonus = GetTotalCivicBonus("global", "satisfactionUpgradeThreshold");
            
            if (maxMoraleBonus > 0 || moraleBalanceBonus != 0 || satisfactionBonus > 0)
            {
                Debug.Log("  Global Bonuses:");
                if (maxMoraleBonus > 0)
                    Debug.Log($"    Max Morale: +{maxMoraleBonus:F1} (more room for positive morale)");
                if (moraleBalanceBonus != 0)
                    Debug.Log($"    Morale Balance: {moraleBalanceBonus:F1} (easier to stay above balance)");
                if (satisfactionBonus > 0)
                    Debug.Log($"    Satisfaction Threshold: +{satisfactionBonus:F1} (easier upgrades)");
            }
            
            // Housing bonuses
            if (PopGrowthLogic.Instance != null)
            {
                var housingSources = PopGrowthLogic.Instance.GetHousingBonusSources();
                var civicHousingBonuses = housingSources.Where(kvp => kvp.Key.StartsWith("Civic:")).ToList();
                
                if (civicHousingBonuses.Count > 0)
                {
                    Debug.Log("  Housing Bonuses:");
                    foreach (var kvp in civicHousingBonuses)
                    {
                        string civicName = kvp.Key.Replace("Civic: ", "");
                        Debug.Log($"    +{kvp.Value} housing from {civicName}");
                    }
                    Debug.Log($"    Total Housing Bonus: +{PopGrowthLogic.Instance.GetTotalHousingBonus()} (persistent, cannot be destroyed)");
                }
            }
            
            // Production scaling bonuses
            if (GameUnitsLogic.Instance != null)
            {
                var scalingBonuses = GameUnitsLogic.Instance.GetAllProductionScalingBonuses();
                var civicScalingBonuses = scalingBonuses.Where(kvp => kvp.Value.Any(source => source.Key.StartsWith("Civic:"))).ToList();
                
                if (civicScalingBonuses.Count > 0)
                {
                    Debug.Log("  Production Scaling Bonuses:");
                    foreach (var kvp in civicScalingBonuses)
                    {
                        string productionUnit = kvp.Key;
                        foreach (var source in kvp.Value)
                        {
                            if (source.Key.StartsWith("Civic:"))
                            {
                                string civicName = source.Key.Replace("Civic: ", "");
                                Debug.Log($"    +{source.Value} per {productionUnit} from {civicName}");
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get all available civic names (loaded from Resources)
    /// </summary>
    public List<string> GetAllAvailableCivicNames()
    {
        return availableCivics.Keys.ToList();
    }
    
    /// <summary>
    /// Get all available civics (loaded from Resources)
    /// </summary>
    public List<CivicData> GetAllAvailableCivics()
    {
        return availableCivics.Values.ToList();
    }
} 
