using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays and manages a challenge slot for a Chorus Screen choice
/// Simplified version with just icon and chance image - tooltips handle text display
/// </summary>
public class ChallengeSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image pillarIcon;
    [SerializeField] private Image chanceImage; // Visual representation of success chance
    
    [Header("Challenge Data")]
    [SerializeField] private string pillarType; // "aureus", "regalia", "waltz", "chorus"
    [SerializeField] private int requiredStrength = 10;
    [SerializeField] private float successChance = 0f;
    
    [Header("Chance Sprites")]
    [SerializeField] private Sprite chanceIconFated;     // Above 90% - Blessed by fate
    [SerializeField] private Sprite chanceIconBlessed;   // 60-90% - Blessed
    [SerializeField] private Sprite chanceIconGamble;    // 40-60% - Gamble
    [SerializeField] private Sprite chanceIconCursed;    // 10-40% - Cursed
    [SerializeField] private Sprite chanceIconForsaken;  // Below 10% - Forsaken
    
    // Public properties for external access
    public string PillarType => pillarType;
    public int RequiredStrength => requiredStrength;
    public float SuccessChance => successChance;
    public Sprite PillarIcon => pillarIcon != null ? pillarIcon.sprite : null;
    
    private StatManager statManager;
    
    private void Start()
    {
        statManager = FindObjectOfType<StatManager>();
        UpdateChallengeDisplay();
    }
    
    /// <summary>
    /// Initialize the challenge slot with data
    /// </summary>
    public void InitializeChallenge(string pillar, int required, Sprite icon = null)
    {
        pillarType = pillar;
        requiredStrength = required;
        
        if (icon != null && pillarIcon != null)
        {
            pillarIcon.sprite = icon;
        }
        
        UpdateChallengeDisplay();
    }
    
    /// <summary>
    /// Update the challenge display and calculate success chance
    /// </summary>
    public void UpdateChallengeDisplay()
    {
        if (string.IsNullOrEmpty(pillarType)) return;
        
        // Calculate success chance
        CalculateSuccessChance();
        
        // Update chance image
        UpdateChanceImage();

        // Attach/update tooltip triggers for pillar and chance icons
        var pillarGo = pillarIcon != null ? pillarIcon.gameObject : null;
        if (pillarGo != null)
        {
            var t = pillarGo.GetComponent<TooltipTrigger>();
            if (t == null) t = pillarGo.AddComponent<TooltipTrigger>();
            t.useCustomTooltip = true;
            t.customTitle = $"{FormatPillarName(pillarType)} Challenge";
            t.customDescription = $"Needs {requiredStrength} {FormatPillarName(pillarType)} for 100%";
            t.customType = string.Empty;
        }
        var chanceGo = chanceImage != null ? chanceImage.gameObject : null;
        if (chanceGo != null)
        {
            var t2 = chanceGo.GetComponent<TooltipTrigger>();
            if (t2 == null) t2 = chanceGo.AddComponent<TooltipTrigger>();
            t2.useCustomTooltip = true;
            t2.customTitle = GetLuckTitle(successChance);
            t2.customDescription = $"{successChance:F0}% Chance";
            t2.customType = string.Empty;
        }
    }
    
    /// <summary>
    /// Calculate the success chance based on current pillar strength
    /// </summary>
    private void CalculateSuccessChance()
    {
        if (statManager == null) return;
        
        int currentStrength = statManager.GetPillarValue(pillarType);
        
        // Calculate success chance: (Current / Required) * 100, capped at 100%
        successChance = Mathf.Clamp((float)currentStrength / requiredStrength * 100f, 0f, 100f);
    }
    
    /// <summary>
    /// Update the chance image based on success chance
    /// </summary>
    private void UpdateChanceImage()
    {
        if (chanceImage == null) return;
        
        // Select appropriate sprite based on chance percentage
        Sprite targetSprite = GetChanceSprite(successChance);
        if (targetSprite != null)
        {
            chanceImage.sprite = targetSprite;
        }
    }
    
    /// <summary>
    /// Get the appropriate chance sprite based on success percentage
    /// </summary>
    private Sprite GetChanceSprite(float chance)
    {
        if (chance > 90f) return chanceIconFated;      // Above 90% - Blessed by fate
        if (chance >= 60f) return chanceIconBlessed;   // 60-90% - Blessed
        if (chance >= 40f) return chanceIconGamble;    // 40-60% - Gamble
        if (chance >= 10f) return chanceIconCursed;    // 10-40% - Cursed
        return chanceIconForsaken;                      // Below 10% - Forsaken
    }

    private string GetLuckTitle(float chance)
    {
        if (chance > 90f) return "It's Fated Luck!";
        if (chance >= 60f) return "It's Blessed Luck!";
        if (chance >= 40f) return "It's Gamble Luck!";
        if (chance >= 10f) return "It's Cursed Luck!";
        return "It's Forsaken Luck!";
    }
    
    /// <summary>
    /// Get the current success chance
    /// </summary>
    public float GetSuccessChance()
    {
        return successChance;
    }
    
    /// <summary>
    /// Get the required strength for this challenge
    /// </summary>
    public int GetRequiredStrength()
    {
        return requiredStrength;
    }
    
    /// <summary>
    /// Get the pillar type for this challenge
    /// </summary>
    public string GetPillarType()
    {
        return pillarType;
    }
    
    /// <summary>
    /// Check if the challenge was successful (for resolution)
    /// </summary>
    public bool IsChallengeSuccessful()
    {
        // Generate a random number and check if it's within the success chance
        float randomRoll = Random.Range(0f, 100f);
        return randomRoll <= successChance;
    }
    
    /// <summary>
    /// Refresh the challenge display (called when stats change)
    /// </summary>
    public void RefreshChallengeDisplay()
    {
        UpdateChallengeDisplay();
    }
    
    /// <summary>
    /// Set the pillar icon sprite
    /// </summary>
    public void SetPillarIcon(Sprite icon)
    {
        if (pillarIcon != null)
        {
            pillarIcon.sprite = icon;
        }
    }
    
    /// <summary>
    /// Get tooltip text for this challenge
    /// </summary>
    public string GetTooltipText()
    {
        string pillarName = FormatPillarName(pillarType);
        return $"{pillarName} Challenge\nRequired: {requiredStrength}\nSuccess Chance: {successChance:F0}%";
    }
    
    /// <summary>
    /// Format the pillar name for display
    /// </summary>
    private string FormatPillarName(string pillar)
    {
        switch (pillar.ToLower())
        {
            case "aureus": return "Aureus";
            case "regalia": return "Regalia";
            case "waltz": return "Waltz";
            case "chorus": return "Chorus";
            default: return pillar;
        }
    }
} 