using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro; // Added for TMP_Text

/// <summary>
/// Displays a single leader slot in the leader pool
/// </summary>
public class LeaderSlotDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image leaderSprite;
    [SerializeField] private Button leaderButton;
    [SerializeField] private GameObject equippedIndicator; // Shows if legend is equipped elsewhere
    [SerializeField] private TMP_Text equippedText; // Shows current seat if equipped
    
    private LegendData legendData;
    private int targetSeatIndex;
    
    // Events
    public event Action<LegendData> OnLeaderSelected;
    
    private void Awake()
    {
        if (leaderButton != null)
        {
            leaderButton.onClick.AddListener(OnLeaderButtonClicked);
        }
    }
    
    private void OnDestroy()
    {
        if (leaderButton != null)
        {
            leaderButton.onClick.RemoveListener(OnLeaderButtonClicked);
        }
    }
    
    /// <summary>
    /// Initialize the leader slot with data
    /// </summary>
    public void Initialize(LegendData legend, int seatIndex)
    {
        legendData = legend;
        targetSeatIndex = seatIndex;
        
        UpdateDisplay();
    }
    
    /// <summary>
    /// Update the visual display
    /// </summary>
    private void UpdateDisplay()
    {
        if (legendData == null) return;
        
        // Update sprite
        if (leaderSprite != null)
        {
            leaderSprite.sprite = legendData.portrait;
        }
        
        // Check if legend is equipped elsewhere
        bool isEquippedElsewhere = false;
        string currentSeatName = "";
        bool isEquippedToCurrentSeat = false;
        
        if (GovernmentLogic.Instance != null)
        {
            var currentSeat = GovernmentLogic.Instance.GetSeatWithLegend(legendData.legendName);
            if (currentSeat != null)
            {
                isEquippedElsewhere = true;
                currentSeatName = currentSeat.GetEffectiveTitle();
                
                // Check if this legend is already assigned to the seat we're looking at
                if (targetSeatIndex == -1)
                {
                    // Head of State
                    isEquippedToCurrentSeat = (currentSeat.seatIndex == -1);
                }
                else
                {
                    // Regular seat
                    isEquippedToCurrentSeat = (currentSeat.seatIndex == targetSeatIndex);
                }
            }
        }
        
        // Update equipped indicator
        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(isEquippedElsewhere);
        }
        
        // Update equipped text
        if (equippedText != null)
        {
            equippedText.text = GetStatusText(isEquippedElsewhere, isEquippedToCurrentSeat, currentSeatName);
            equippedText.color = GetStatusColor(isEquippedElsewhere, isEquippedToCurrentSeat);
        }
        
        // Button is always interactable since we filter at source level
        if (leaderButton != null)
        {
            leaderButton.interactable = true;
        }
    }
    
    /// <summary>
    /// Get descriptive status text for the legend
    /// </summary>
    private string GetStatusText(bool isEquippedElsewhere, bool isEquippedToCurrentSeat, string currentSeatName)
    {
        if (!isEquippedElsewhere)
        {
            return "Available";
        }
        
        if (isEquippedToCurrentSeat)
        {
            return "Currently Assigned";
        }
        
        // Legend is equipped to a different seat - explain the swap
        if (targetSeatIndex == -1)
        {
            return $"Swap from {currentSeatName}";
        }
        else
        {
            return $"Swap from {currentSeatName}";
        }
    }
    
    /// <summary>
    /// Get color for the status text based on legend state
    /// </summary>
    private Color GetStatusColor(bool isEquippedElsewhere, bool isEquippedToCurrentSeat)
    {
        if (!isEquippedElsewhere)
        {
            return Color.green; // Available - green
        }
        
        if (isEquippedToCurrentSeat)
        {
            return Color.blue; // Currently assigned - blue
        }
        
        return new Color(1f, 0.5f, 0f); // Swap operation - orange
    }
    
    /// <summary>
    /// Handle leader button click
    /// </summary>
    private void OnLeaderButtonClicked()
    {
        // Check cooldown before allowing assignment
        if (GovernmentLogic.Instance != null && !GovernmentLogic.Instance.CanChangeSeat(targetSeatIndex))
        {
            int remainingCooldown = GovernmentLogic.Instance.GetSeatCooldownRemaining(targetSeatIndex);
            string seatName = (targetSeatIndex == -1) ? "Head of State" : $"Seat {targetSeatIndex}";
            GameLoggingSystem.Instance.LogEvent($"Cannot assign legend - {seatName} is on cooldown for {remainingCooldown} more sevenths", "LeaderSlotDisplay");
            return;
        }
        
        OnLeaderSelected?.Invoke(legendData);
    }
    
    /// <summary>
    /// Get the legend data
    /// </summary>
    public LegendData GetLegendData()
    {
        return legendData;
    }
    
    /// <summary>
    /// Get the target seat index
    /// </summary>
    public int GetTargetSeatIndex()
    {
        return targetSeatIndex;
    }
    
    /// <summary>
    /// Refresh the display (called when data changes)
    /// </summary>
    public void Refresh()
    {
        UpdateDisplay();
    }
} 