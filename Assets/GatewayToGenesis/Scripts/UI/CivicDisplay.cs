using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a single civic in the government tab
/// </summary>
public class CivicDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image civicIcon;
    
    private CivicData civicData;
    private CivicTier civicTier;
    
    /// <summary>
    /// Initialize the civic display with data
    /// </summary>
    public void Initialize(CivicData civic, CivicTier tier)
    {
        civicData = civic;
        civicTier = tier;
        
        UpdateDisplay();
    }
    
    /// <summary>
    /// Update the visual display
    /// </summary>
    private void UpdateDisplay()
    {
        if (civicData == null) return;
        
        // Update icon
        if (civicIcon != null)
        {
            civicIcon.sprite = civicData.icon;
        }
    }
    
    /// <summary>
    /// Get the civic data
    /// </summary>
    public CivicData GetCivicData()
    {
        return civicData;
    }
    
    /// <summary>
    /// Get the civic tier
    /// </summary>
    public CivicTier GetCivicTier()
    {
        return civicTier;
    }
} 