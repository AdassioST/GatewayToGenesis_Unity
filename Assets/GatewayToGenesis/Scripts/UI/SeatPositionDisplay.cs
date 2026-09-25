using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Displays a single council seat position in the government tab
/// </summary>
public class SeatPositionDisplay : MonoBehaviour, ITooltipSource
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image spriteImage;
    [SerializeField] private Button seatButton;
    [SerializeField] private Image activeIndicator; // Red = inactive, Green = active
    [SerializeField] private Image cooldownIndicator; // Deep blue = on cooldown, Cyan = ready
    [Header("Default Assets")]
    [Tooltip("Default icon to show when no legend is assigned to this seat")]
    [SerializeField] private Sprite defaultIcon; // Default icon for unassigned seats
    
    [Header("Status Colors")]
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.red;
    [SerializeField] private Color readyColor = Color.cyan;
    [SerializeField] private Color cooldownColor = new Color(0f, 0f, 0.5f, 1f); // Deep blue
    
    private CouncilSeat councilSeat;
    private int seatIndex;
    
    // Events
    public event Action<int> OnSeatClicked;
    
    private void Awake()
    {
        if (seatButton != null)
        {
            seatButton.onClick.AddListener(OnSeatButtonClicked);
        }
    }
    
    // Removed per-frame updates; status is now refreshed via event-driven calls
    
    private void OnDestroy()
    {
        if (seatButton != null)
        {
            seatButton.onClick.RemoveListener(OnSeatButtonClicked);
        }
    }
    
    /// <summary>
    /// Initialize the seat display with data
    /// </summary>
    public void Initialize(CouncilSeat seat, int index)
    {
        councilSeat = seat;
        seatIndex = index;
        
        UpdateDisplay();
    }
    
    /// <summary>
    /// Update the visual display
    /// </summary>
    private void UpdateDisplay()
    {
        if (councilSeat == null) return;
        
        // Update title
        if (titleText != null)
        {
            titleText.text = councilSeat.GetEffectiveTitle();
        }
        
        // Update name and sprite based on legend assignment
        if (councilSeat.assignedLegend != null)
        {
            if (nameText != null)
                nameText.text = councilSeat.assignedLegend.legendName;
            if (spriteImage != null)
                spriteImage.sprite = councilSeat.assignedLegend.portrait;
        }
        else
        {
            if (nameText != null)
                nameText.text = "Unassigned";
            if (spriteImage != null)
                spriteImage.sprite = defaultIcon;
        }
        
        // Update status indicators
        UpdateStatusIndicators();
        
        // Update button interactability (only locked seats should be non-interactable)
        // Seats on cooldown can still be clicked to show messages, but won't trigger actions
        if (seatButton != null)
        {
            bool canInteract = councilSeat.isUnlocked; // Remove cooldown check from interactability
            seatButton.interactable = canInteract;
        }
    }
    
    /// <summary>
    /// Update the active and cooldown status indicators
    /// </summary>
    private void UpdateStatusIndicators()
    {
        // Update active indicator (red = inactive, green = active)
        if (activeIndicator != null)
        {
            bool isActive = councilSeat != null && councilSeat.IsActive();
            activeIndicator.color = isActive ? activeColor : inactiveColor;
        }
        
        // Update cooldown indicator (deep blue = on cooldown, cyan = ready)
        if (cooldownIndicator != null)
        {
            bool onCooldown = IsOnCooldown();
            cooldownIndicator.color = onCooldown ? cooldownColor : readyColor;
        }
    }
    
    /// <summary>
    /// Check if this seat is currently on cooldown
    /// </summary>
    private bool IsOnCooldown()
    {
        if (GovernmentLogic.Instance == null) return false;
        return !GovernmentLogic.Instance.CanChangeSeat(seatIndex);
    }
    
    /// <summary>
    /// Handle seat button click
    /// </summary>
    private void OnSeatButtonClicked()
    {
        if (councilSeat != null && councilSeat.isUnlocked)
        {
            // Check cooldown before allowing interaction
            if (IsOnCooldown())
            {
                int remainingCooldown = GovernmentLogic.Instance.GetSeatCooldownRemaining(seatIndex);
                GameLog.Event($"Seat {seatIndex} ({councilSeat.GetEffectiveTitle()}) is on cooldown for {remainingCooldown} more sevenths", LogChannel.GovernmentUI);
                return;
            }
            
            OnSeatClicked?.Invoke(seatIndex);
        }
    }
    
    /// <summary>
    /// Get the council seat data
    /// </summary>
    public CouncilSeat GetCouncilSeat()
    {
        return councilSeat;
    }
    
    /// <summary>
    /// Get the seat index
    /// </summary>
    public int GetSeatIndex()
    {
        return seatIndex;
    }
    
    /// <summary>
    /// Check if the leader/civic selection should be shown (only if not on cooldown)
    /// </summary>
    /// <returns>True if selection UI should be shown, false if on cooldown</returns>
    public bool ShouldShowSelection()
    {
        return !IsOnCooldown();
    }
    
    /// <summary>
    /// Refresh the display (called when seat data changes)
    /// </summary>
    public void Refresh()
    {
        UpdateDisplay();
    }

    /// <summary>
    /// The portrait shows the seated legend (or an assign prompt), the indicators explain themselves, and
    /// everything else on the seat shows the seat.
    /// </summary>
    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data)
    {
        if (councilSeat == null) return false;
        var target = trigger.gameObject;
        if (spriteImage != null && target == spriteImage.gameObject) return TooltipContent.SeatPortrait(councilSeat, data);
        if (activeIndicator != null && target == activeIndicator.gameObject) return TooltipContent.SeatActivity(councilSeat, data);
        if (cooldownIndicator != null && target == cooldownIndicator.gameObject) return TooltipContent.SeatCooldownIndicator(councilSeat, data);
        return TooltipContent.Seat(councilSeat, data);
    }
}
