using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Manages legend leaders, their assignments to council seats, and the application of combined bonuses
/// </summary>
public class LegendLeaderLogic : MonoBehaviour
{
    public static LegendLeaderLogic Instance { get; private set; }
    
    [Header("Council Assignment")]
    [SerializeField] private int seventhsForActivation = 0; // How many sevenths until council effects activate (1 seventh = ~3 minutes)
    
    // Available legends loaded from Resources
    private Dictionary<string, LegendData> availableLegends = new Dictionary<string, LegendData>();
    
    // Active legends (assigned to council seats)
    private Dictionary<int, LegendData> assignedLegends = new Dictionary<int, LegendData>();
    
    // Events for UI updates
    public System.Action<CouncilSeat, LegendData> OnLegendAssigned; // (seat, legend)
    public System.Action<CouncilSeat> OnLegendRemoved; // (seat)
    public System.Action<CouncilSeat> OnCouncilSeatActivated; // (seat)
    public System.Action<CouncilSeat> OnCouncilSeatChanged; // (seat)
    public System.Action OnCouncilCompositionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate LegendLeaderLogic found, destroying the new one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        LoadAvailableLegends();
        
        // Subscribe to time system for seventh processing
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange += OnSeventhChange;
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
        TimeSystemLogic.Instance.OnSeventhChange += OnSeventhChange;
    }

    private void OnDestroy()
    {
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.OnSeventhChange -= OnSeventhChange;
        }
    }

    private void Update()
    {
        // Debug input for testing legend system
        if (Input.GetKeyDown(KeyCode.L))
        {
            PrintLegendInfo();
        }
    }

    /// <summary>
    /// Load all available legends from Resources/Legends folder
    /// </summary>
    private void LoadAvailableLegends()
    {
        availableLegends.Clear();
        LegendData[] legends = Resources.LoadAll<LegendData>("Legends");
        
        foreach (var legend in legends)
        {
            if (!availableLegends.ContainsKey(legend.legendName))
            {
                // Validate legend bonuses during loading
                if (legend.bonuses != null && legend.bonuses.Count > 0)
                {
                    foreach (var bonus in legend.bonuses)
                    {
                        var (isValid, errorMessage) = bonus.ValidateBonus();
                        if (!isValid)
                        {
                            Debug.LogWarning($"[LegendLeaderLogic] Invalid bonus in {legend.legendName}: {errorMessage}");
                        }
                    }
                }
                
                availableLegends[legend.legendName] = legend;
                GameLoggingSystem.Instance.LogEvent($"Loaded legend: {legend.legendName} (Class: {legend.legendClass}, Rarity: {legend.rarity})", "LegendLeaderLogic");
            }
            else
            {
                Debug.LogWarning($"[LegendLeaderLogic] Duplicate legend found: {legend.legendName}");
            }
        }
        
        GameLoggingSystem.Instance.LogEvent($"Loaded {availableLegends.Count} legends from Resources", "LegendLeaderLogic");
    }

    /// <summary>
    /// Assign a legend to a council seat
    /// </summary>
    public bool AssignLegendToSeat(LegendData legend, int seatIndex)
    {
        if (legend == null)
        {
            Debug.LogWarning("[LegendLeaderLogic] Cannot assign null legend to seat");
            return false;
        }
        
        // Use GovernmentLogic for seat assignment
        if (GovernmentLogic.Instance != null)
        {
            return GovernmentLogic.Instance.AssignLegendToSeat(legend, seatIndex);
        }
        
        Debug.LogError("[LegendLeaderLogic] GovernmentLogic.Instance is null - cannot assign legend to seat");
        return false;
    }

    /// <summary>
    /// Remove a legend from a council seat
    /// </summary>
    public bool RemoveLegendFromSeat(int seatIndex)
    {
        // Use GovernmentLogic for seat removal
        if (GovernmentLogic.Instance != null)
        {
            return GovernmentLogic.Instance.RemoveLegendFromSeat(seatIndex);
        }
        
        Debug.LogError("[LegendLeaderLogic] GovernmentLogic.Instance is null - cannot remove legend from seat");
        return false;
    }

    /// <summary>
    /// Check if a legend is currently assigned to any seat
    /// </summary>
    public bool IsLegendAssigned(string legendName)
    {
        return assignedLegends.Values.Any(l => l.legendName == legendName);
    }

    /// <summary>
    /// Get the seat index where a legend is assigned
    /// </summary>
    public int GetLegendSeatIndex(string legendName)
    {
        foreach (var kvp in assignedLegends)
        {
            if (kvp.Value.legendName == legendName)
            {
                return kvp.Key;
            }
        }
        return -1; // Not assigned
    }

    /// <summary>
    /// Get all currently assigned legends
    /// </summary>
    public Dictionary<int, LegendData> GetAssignedLegends()
    {
        return new Dictionary<int, LegendData>(assignedLegends);
    }

    /// <summary>
    /// Get all available legends
    /// </summary>
    public List<LegendData> GetAvailableLegends()
    {
        return availableLegends.Values.ToList();
    }

    /// <summary>
    /// Get legends by class
    /// </summary>
    public List<LegendData> GetLegendsByClass(LegendClass legendClass)
    {
        return availableLegends.Values.Where(l => l.legendClass == legendClass).ToList();
    }

    /// <summary>
    /// Get legends that can be assigned to a specific seat
    /// </summary>
    public List<LegendData> GetCompatibleLegends(int seatIndex)
    {
        if (GovernmentLogic.Instance == null) return new List<LegendData>();
        
        var seat = GovernmentLogic.Instance.GetCouncilSeat(seatIndex);
        if (seat == null) return new List<LegendData>();
        
        var compatible = new List<LegendData>();
        
        foreach (var legend in availableLegends.Values)
        {
            if (seat.CanAssignLegend(legend) && !IsLegendAssigned(legend.legendName))
            {
                compatible.Add(legend);
            }
        }
        
        return compatible;
    }


    
    /// <summary>
    /// Validate bonus values before applying to prevent broken game balance
    /// </summary>
    private bool ValidateBonusValue(float value, GameEffectType bonusType)
    {
        switch (bonusType)
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
                
            case GameEffectType.ConstructionCostModifier:
                return value >= -90 && value <= 200; // Reasonable construction cost limits
                
            case GameEffectType.ProductionScalingBonus:
                return value > 0 && value <= 100; // Only positive, reasonable scaling limits
                
            case GameEffectType.SpecialAbility:
                return true; // Special abilities don't have numeric validation
                
            default:
                return false; // Unknown bonus type
        }
    }

    /// <summary>
    /// Process seventh changes for council seat activation
    /// </summary>
    private void OnSeventhChange(int newSeventh)
    {
        if (GovernmentLogic.Instance == null) return;
        
        // Process all council seats
        for (int i = -1; i < 6; i++) // -1 for Head of State, 0-5 for regular seats (7 total)
        {
            var seat = GovernmentLogic.Instance.GetCouncilSeat(i);
            if (seat != null && seat.assignedLegend != null)
            {
                seat.ProcessSeventh();
                
                // Check if seat just became active
                if (seat.IsActive() && seat.seventhsUntilActive == 0)
                {
                    OnCouncilSeatActivated?.Invoke(seat);
                    
                    GameLoggingSystem.Instance.LogEvent($"Council seat {seat.GetEffectiveTitle()} (seat {i}) is now active with {seat.assignedLegend.legendName} after {seventhsForActivation} sevenths", "LegendLeaderLogic");
                }
            }
        }
        
        // Process seat bonuses for all active seats
        if (GovernmentLogic.Instance != null)
        {
            GovernmentLogic.Instance.ProcessAllSeatBonuses();
        }
    }
    
    /// <summary>
    /// Get the configured sevenths for activation delay
    /// </summary>
    public int GetSeventhsForActivation()
    {
        return seventhsForActivation;
    }
    
    /// <summary>
    /// Set the sevenths for activation delay (for runtime configuration)
    /// </summary>
    public void SetSeventhsForActivation(int newSevenths)
    {
        seventhsForActivation = Mathf.Max(1, newSevenths);
        GameLoggingSystem.Instance.LogEvent($"Sevenths for activation set to {seventhsForActivation}", "LegendLeaderLogic");
    }



    /// <summary>
    /// Debug method to print all legend information
    /// </summary>
    [ContextMenu("Print Legend Info")]
    public void PrintLegendInfo()
    {
        GameLoggingSystem.Instance.LogEvent("=== LEGEND SYSTEM INFO ===", "LegendLeaderLogic");
        GameLoggingSystem.Instance.LogEvent($"Available Legends: {availableLegends.Count}", "LegendLeaderLogic");
        GameLoggingSystem.Instance.LogEvent($"Assigned Legends: {assignedLegends.Count}", "LegendLeaderLogic");
        
        GameLoggingSystem.Instance.LogEvent("=== AVAILABLE LEGENDS ===", "LegendLeaderLogic");
        foreach (var legend in availableLegends.Values)
        {
            GameLoggingSystem.Instance.LogEvent($"  {legend.legendName} ({legend.legendClass}) - {legend.originStory}", "LegendLeaderLogic");
        }
        
        GameLoggingSystem.Instance.LogEvent("=== ASSIGNED LEGENDS ===", "LegendLeaderLogic");
        foreach (var kvp in assignedLegends)
        {
            var seat = GovernmentLogic.Instance?.GetCouncilSeat(kvp.Key);
            string seatTitle = seat?.GetEffectiveTitle() ?? "Unknown Seat";
            GameLoggingSystem.Instance.LogEvent($"  Seat {kvp.Key} ({seatTitle}): {kvp.Value.legendName} ({kvp.Value.legendClass})", "LegendLeaderLogic");
        }
        
        GameLoggingSystem.Instance.LogEvent("=== COUNCIL SEATS STATUS ===", "LegendLeaderLogic");
        if (GovernmentLogic.Instance != null)
        {
            for (int i = -1; i < 6; i++) // -1 for Head of State, 0-5 for regular seats (7 total)
            {
                var seat = GovernmentLogic.Instance.GetCouncilSeat(i);
                if (seat != null)
                {
                    string status = seat.assignedLegend != null ? 
                        $"Assigned: {seat.assignedLegend.legendName} (Active: {seat.IsActive()})" : 
                        "Empty";
                    GameLoggingSystem.Instance.LogEvent($"  Seat {i} ({seat.GetEffectiveTitle()}): {status}", "LegendLeaderLogic");
                }
            }
        }
        
        // Show total legend bonuses
        if (StatManager.Instance != null)
        {
            GameLoggingSystem.Instance.LogEvent("=== TOTAL LEGEND BONUSES ===", "LegendLeaderLogic");
            
            // Pillar bonuses
            foreach (var pillar in new[] { "aureus", "regalia", "waltz", "chorus" })
            {
                float totalBonus = StatManager.Instance.GetPillarBonus(pillar);
                if (totalBonus > 0)
                {
                    var sources = StatManager.Instance.GetBonusSources("pillar", pillar);
                    var legendSources = sources.Where(kvp => kvp.Key.StartsWith("Legend:")).ToList();
                    if (legendSources.Count > 0)
                    {
                        GameLoggingSystem.Instance.LogEvent($"  {pillar}: +{totalBonus:F1} total from legends", "LegendLeaderLogic");
                        foreach (var source in legendSources)
                        {
                            string legendName = source.Key.Replace("Legend: ", "");
                            GameLoggingSystem.Instance.LogEvent($"    +{source.Value:F1} from {legendName}", "LegendLeaderLogic");
                        }
                    }
                }
            }
            
            // Global stat bonuses (morale, satisfaction)
            var maxMoraleBonus = StatManager.Instance.GetGlobalBonus("maxMorale");
            var moraleBalanceBonus = StatManager.Instance.GetGlobalBonus("moraleBalance");
            
            if (maxMoraleBonus > 0 || moraleBalanceBonus != 0)
            {
                GameLoggingSystem.Instance.LogEvent("  Morale Bonuses:", "LegendLeaderLogic");
                if (maxMoraleBonus > 0)
                    GameLoggingSystem.Instance.LogEvent($"    Max Morale: +{maxMoraleBonus:F1} (more room for positive morale)", "LegendLeaderLogic");
                if (moraleBalanceBonus != 0)
                    GameLoggingSystem.Instance.LogEvent($"    Morale Balance: {moraleBalanceBonus:F1} (easier to stay above balance)", "LegendLeaderLogic");
            }
            
            // Housing bonuses
            if (PopGrowthLogic.Instance != null)
            {
                var housingSources = PopGrowthLogic.Instance.GetHousingBonusSources();
                var legendHousingBonuses = housingSources.Where(kvp => kvp.Key.StartsWith("Legend:")).ToList();
                
                if (legendHousingBonuses.Count > 0)
                {
                    GameLoggingSystem.Instance.LogEvent("  Housing Bonuses:", "LegendLeaderLogic");
                    foreach (var kvp in legendHousingBonuses)
                    {
                        string legendName = kvp.Key.Replace("Legend: ", "");
                        GameLoggingSystem.Instance.LogEvent($"    +{kvp.Value} housing from {legendName}", "LegendLeaderLogic");
                    }
                    GameLoggingSystem.Instance.LogEvent($"    Total Housing Bonus: +{PopGrowthLogic.Instance.GetTotalHousingBonus()} (persistent, cannot be destroyed)", "LegendLeaderLogic");
                }
            }
            
            // Production scaling bonuses
            if (GameUnitsLogic.Instance != null)
            {
                var scalingBonuses = GameUnitsLogic.Instance.GetAllProductionScalingBonuses();
                var legendScalingBonuses = scalingBonuses.Where(kvp => kvp.Value.Any(source => source.Key.StartsWith("Legend:"))).ToList();
                
                if (legendScalingBonuses.Count > 0)
                {
                    GameLoggingSystem.Instance.LogEvent("  Production Scaling Bonuses:", "LegendLeaderLogic");
                    foreach (var kvp in legendScalingBonuses)
                    {
                        string productionUnit = kvp.Key;
                        foreach (var source in kvp.Value)
                        {
                            if (source.Key.StartsWith("Legend:"))
                            {
                                string legendName = source.Key.Replace("Legend: ", "");
                                Debug.Log($"    +{source.Value} per {productionUnit} from {legendName}");
                            }
                        }
                    }
                }
            }
        }
    }
    
    // Seat bonus processing is now handled by GovernmentLogic.cs
}
