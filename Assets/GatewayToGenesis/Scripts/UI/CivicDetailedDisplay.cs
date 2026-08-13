using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays detailed seat information in the civic pool for seat replacement
/// </summary>
public class CivicDetailedDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image seatIcon;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI effectsText;
    [SerializeField] private TextMeshProUGUI leaderClassText;
    [SerializeField] private Button seatButton;
    
    private string seatTitle;
    private CouncilSeat seatData;
    
    // Event for when a seat is selected
    public System.Action<string> OnCivicClicked;
    
    private void Awake()
    {
        if (seatButton != null)
        {
            seatButton.onClick.AddListener(OnSeatClicked);
        }
    }
    
    private void OnDestroy()
    {
        if (seatButton != null)
        {
            seatButton.onClick.RemoveListener(OnSeatClicked);
        }
    }
    
    /// <summary>
    /// Initialize the seat detailed display with seat title
    /// </summary>
    public void Initialize(string seatTitle)
    {
        this.seatTitle = seatTitle;
        UpdateDisplay();
    }
    
    /// <summary>
    /// Update the visual display
    /// </summary>
    private void UpdateDisplay()
    {
        if (string.IsNullOrEmpty(seatTitle)) return;
        
        if (GovernmentLogic.Instance != null)
        {
            // Get seat display info (works for both civic and default seats)
            var (title, effects, leaderClasses, icon) = GovernmentLogic.Instance.GetSeatDisplayInfo(seatTitle);
            
            // Update title
            if (titleText != null)
            {
                titleText.text = title;
            }
            
            // Update effects text
            if (effectsText != null)
            {
                effectsText.text = effects;
            }
            
            // Update leader class text
            if (leaderClassText != null)
            {
                leaderClassText.text = leaderClasses;
            }
            
            // Update icon
            if (seatIcon != null)
            {
                seatIcon.sprite = icon;
            }
        }
        else
        {
            // Fallback if GovernmentLogic is not available
            if (titleText != null)
                titleText.text = seatTitle;
            if (effectsText != null)
                effectsText.text = "Effects: N/A";
            if (leaderClassText != null)
                leaderClassText.text = "Leader Classes: N/A";
            if (seatIcon != null)
                seatIcon.sprite = null;
        }
        
        // Button is always interactable for seat replacement
        if (seatButton != null)
        {
            seatButton.interactable = true;
        }
    }
    

    
    /// <summary>
    /// Handle seat button click
    /// </summary>
    private void OnSeatClicked()
    {
        if (!string.IsNullOrEmpty(seatTitle))
        {
            OnCivicClicked?.Invoke(seatTitle);
        }
    }
    
    /// <summary>
    /// Get the seat title
    /// </summary>
    public string GetSeatTitle()
    {
        return seatTitle;
    }
    
    /// <summary>
    /// Refresh the display (called when data changes)
    /// </summary>
    public void Refresh()
    {
        UpdateDisplay();
    }
} 