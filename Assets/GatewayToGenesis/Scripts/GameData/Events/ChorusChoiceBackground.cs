using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

/// <summary>
/// Manually assigned component for choice background elements
/// Handles token drops and choice execution without auto-setup
/// </summary>
public class ChorusChoiceBackground : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Choice Configuration")]
    [SerializeField] private string choiceId; // "idealism", "realism", "pragmatism"
    [SerializeField] private ChorusChoiceData choiceData; // Assign in inspector
    
    [Header("Visual Settings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage; // Assign manually for drop detection
    [SerializeField] private float baseAlpha = 0.35f;
    [SerializeField] private float maxAlpha = 1.0f;
    
    [Header("References")]
    [SerializeField] private ChorusScreenManager chorusManager;
    [SerializeField] private TMP_Text hoverDescriptionText; // Optional: shows quoted title on background
    
    private Tween alphaTween;
    private float originalBaseAlpha;
    private int originalBackgroundLayer;
    private int originalChoiceLayer;
    private bool isAvailable;
    
    private void Start()
    {
        // Validate required components
        if (canvasGroup == null)
        {
            Debug.LogError($"[ChorusChoiceBackground] {gameObject.name}: CanvasGroup is required! Assign it in the inspector.");
            enabled = false;
            return;
        }
        
        if (chorusManager == null)
        {
            Debug.LogError($"[ChorusChoiceBackground] {gameObject.name}: ChorusManager reference is required! Assign it in the inspector.");
            enabled = false;
            return;
        }
        
        // Validate Image component for Event System registration
        if (backgroundImage == null)
        {
            Debug.LogError($"[ChorusChoiceBackground] {gameObject.name}: Background Image is required! Assign it in the inspector for drop detection.");
            enabled = false;
            return;
        }
        
        // Ensure raycastTarget is enabled for drop detection
        if (!backgroundImage.raycastTarget)
        {
            backgroundImage.raycastTarget = true;
            Debug.Log($"[ChorusChoiceBackground] {gameObject.name}: Enabled raycastTarget on assigned Image component");
        }
        
        // Ensure Image component is on this GameObject for Event System registration
        Image localImage = GetComponent<Image>();
        if (localImage == null)
        {
            localImage = gameObject.AddComponent<Image>();
            localImage.color = new Color(1f, 1f, 1f, 0.1f); // Nearly transparent
            localImage.raycastTarget = true;
            Debug.Log($"[ChorusChoiceBackground] {gameObject.name}: Added local Image component for Event System registration");
        }
        else if (!localImage.raycastTarget)
        {
            localImage.raycastTarget = true;
            Debug.Log($"[ChorusChoiceBackground] {gameObject.name}: Enabled raycastTarget on local Image component");
        }
        
        // Set initial alpha
        originalBaseAlpha = baseAlpha;
        canvasGroup.alpha = baseAlpha;
        originalBackgroundLayer = gameObject.layer;
        // Defer fetching choice object until choices are initialized
        originalChoiceLayer = LayerMask.NameToLayer("UI");
        
        Debug.Log($"[ChorusChoiceBackground] {gameObject.name} initialized for choice: {choiceId}");
        Debug.Log($"[ChorusChoiceBackground] {gameObject.name}: IDropHandler interface check - {(this is IDropHandler ? "IMPLEMENTED" : "NOT IMPLEMENTED")}");
        Debug.Log($"[ChorusChoiceBackground] {gameObject.name}: Local Image component - {(GetComponent<Image>() != null ? "EXISTS" : "MISSING")}");
        Debug.Log($"[ChorusChoiceBackground] {gameObject.name}: Local Image raycastTarget - {(GetComponent<Image>()?.raycastTarget == true ? "ENABLED" : "DISABLED")}");
    }
    
    /// <summary>
    /// Initialize with choice data (called by ChorusScreenManager)
    /// </summary>
    public void InitializeChoice(string id, ChorusChoiceData data)
    {
        choiceId = id;
        choiceData = data;
        Debug.Log($"[ChorusChoiceBackground] {gameObject.name} initialized with choice: {choiceId}");
        Debug.Log($"[ChorusChoiceBackground] choice hasRequirements={choiceData?.HasRequirements}, reqCount={choiceData?.Requirements?.Count ?? 0}");

        // Set hover description to quoted title for immersion
        if (hoverDescriptionText != null && choiceData != null && !string.IsNullOrEmpty(choiceData.Title))
        {
            hoverDescriptionText.text = $"\"{choiceData.Title}\"";
        }

        // Availability visuals: dim unavailable choices based on requirements
        bool available = IsChoiceAvailable();
        isAvailable = available;
        Debug.Log($"[ChorusChoiceBackground] availability for {choiceId}: {available}");
        ApplyAvailabilityVisuals(available);
    }

    private bool IsChoiceAvailable()
    {
        if (chorusManager == null || choiceData == null) return true;
        // Gate availability by both explicit requirements and affordability of costs
        System.Collections.Generic.List<EventCondition> gating = new System.Collections.Generic.List<EventCondition>();
        if (choiceData.Requirements != null) gating.AddRange(choiceData.Requirements);
        if (choiceData.RequirementsCost != null) gating.AddRange(choiceData.RequirementsCost);
        if (gating.Count == 0) return true;
        foreach (var r in gating)
        {
            bool met = r.Evaluate();
            Debug.Log($"[ChorusChoiceBackground] req check for {choiceId}: type={r.type}, target='{r.targetName}', cmp={r.comparison}, value={r.requiredValue}, met={met}");
            if (!met) return false;
        }
        return true;
    }

    private void ApplyAvailabilityVisuals(bool available)
    {
        // Background alpha: 0.35 when available, 0.15 when unavailable (locked)
        if (canvasGroup != null)
        {
            canvasGroup.alpha = available ? baseAlpha : 0.15f;
        }

        var choice = GetCorrespondingChoiceObject();
        if (choice != null)
        {
            // Foreground choice content remains at 1 always; lock state is shown via overlay
            var cg = choice.GetComponent<CanvasGroup>();
            if (cg == null) cg = choice.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            // Toggle lock overlay on the choice
            choice.SetLockedOverlay(!available);

            // Change layers to UIBlock for unavailable choices so token cannot be dropped through
            int uiBlock = LayerMask.NameToLayer("UIBlock");
            if (uiBlock >= 0)
            {
                gameObject.layer = available ? originalBackgroundLayer : uiBlock;
                if (available)
                {
                    if (originalChoiceLayer < 0) originalChoiceLayer = choice.gameObject.layer;
                    choice.gameObject.layer = originalChoiceLayer;
                }
                else
                {
                    if (originalChoiceLayer < 0) originalChoiceLayer = choice.gameObject.layer;
                    choice.gameObject.layer = uiBlock;
                }
            }
        }
    }
    
    /// <summary>
    /// Handle token drop on this background
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"[ChorusChoiceBackground] OnDrop called on {gameObject.name} for choice: {choiceId}");
        Debug.Log($"[ChorusChoiceBackground] Event data: pointerDrag={(eventData.pointerDrag != null ? eventData.pointerDrag.name : "NULL")}");
        
        // Check if dropped object is the decision token
        if (eventData.pointerDrag != null && eventData.pointerDrag.GetComponent<DecisionToken>() != null)
        {
            Debug.Log($"[ChorusChoiceBackground] DecisionToken detected on drop for {choiceId}");
            
            if (choiceData != null && chorusManager != null)
            {
                // Block unavailable choices based on requirements
                if (!IsChoiceAvailable())
                {
                    Debug.Log($"[ChorusChoiceBackground] Choice {choiceId} unavailable due to requirements. Ignoring drop.");
                    // Snap token back (DecisionToken will handle return when not dropped on a valid target)
                    return;
                }
                // Find and trigger the corresponding choice object
                ChorusChoice choiceObject = GetCorrespondingChoiceObject();
                if (choiceObject != null)
                {
                    Debug.Log($"[ChorusChoiceBackground] Triggering choice selection for {choiceId}");
                    choiceObject.SelectChoice();
                    
                    // Fade out the token
                    FadeOutToken(eventData.pointerDrag);
                    
                    // Notify chorus manager
                        chorusManager.OnChoiceMade(choiceId);
                }
                else
                {
                    Debug.LogWarning($"[ChorusChoiceBackground] No corresponding choice object found for {choiceId}");
                }
            }
            else
            {
                Debug.LogWarning($"[ChorusChoiceBackground] Missing choice data or chorus manager for {choiceId}");
            }
        }
        else
        {
            Debug.LogWarning($"[ChorusChoiceBackground] OnDrop called but no DecisionToken found. pointerDrag: {(eventData.pointerDrag != null ? eventData.pointerDrag.name : "NULL")}");
        }
    }
    
    /// <summary>
    /// Get the corresponding ChorusChoice object for this background
    /// </summary>
    private ChorusChoice GetCorrespondingChoiceObject()
    {
        if (chorusManager == null) return null;
        
        Transform choicesContainer = chorusManager.GetChoicesContainer();
        if (choicesContainer == null) return null;
        
        // Look for choice object with matching ID (case-insensitive, safe)
        ChorusChoice[] choiceObjects = choicesContainer.GetComponentsInChildren<ChorusChoice>(true);
        string myId = choiceId ?? string.Empty;
        foreach (var choice in choiceObjects)
        {
            if (choice == null) continue;
            string otherId = choice.ChoiceId ?? string.Empty;
            if (string.Equals(otherId, myId, System.StringComparison.OrdinalIgnoreCase))
            {
                return choice;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Fade out the dropped token
    /// </summary>
    private void FadeOutToken(GameObject token)
    {
        CanvasGroup tokenCanvasGroup = token.GetComponent<CanvasGroup>();
        if (tokenCanvasGroup != null)
        {
            tokenCanvasGroup.DOFade(0f, 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLink(token)
                .OnComplete(() => {
                    if (token != null)
                    {
                        token.SetActive(false);
                    }
                });
        }
        else
        {
            token.SetActive(false);
        }
    }
    
    /// <summary>
    /// Update alpha based on token distance from center
    /// </summary>
    public void UpdateAlphaByTokenDistance(float distanceFromCenter, float maxDistance)
    {
        if (canvasGroup == null) return;
        if (!isAvailable) return; // locked choices remain static
        
        float normalizedDistance = Mathf.Clamp01(distanceFromCenter / maxDistance);
        float targetAlpha = Mathf.Lerp(baseAlpha, maxAlpha, normalizedDistance);
        
        // Kill existing tween and animate to new alpha
        if (alphaTween != null && alphaTween.IsActive())
        {
            alphaTween.Kill();
        }
        
        alphaTween = canvasGroup.DOFade(targetAlpha, 0.3f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }
    
    /// <summary>
    /// Reset alpha to base value
    /// </summary>
    public void ResetToBaseAlpha()
    {
        if (canvasGroup == null) return;
        if (!isAvailable) return; // locked choices remain static
        
        if (alphaTween != null && alphaTween.IsActive())
        {
            alphaTween.Kill();
        }
        
        alphaTween = canvasGroup.DOFade(baseAlpha, 0.5f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }
    
    /// <summary>
    /// Handle pointer enter - increase alpha
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Disable mouse hover visuals; highlighting is controlled by token drag logic
        return;
    }
    
    /// <summary>
    /// Handle pointer exit - reset alpha
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        // Disable mouse hover visuals; highlighting is controlled by token drag logic
        return;
    }
    
    /// <summary>
    /// Get the choice ID for this background
    /// </summary>
    public string GetChoiceId()
    {
        return choiceId;
    }

    public bool IsAvailable()
    {
        return isAvailable;
    }
    
    /// <summary>
    /// Get the associated choice data
    /// </summary>
    public ChorusChoiceData GetChoiceData()
    {
        return choiceData;
    }
    
    private void OnDestroy()
    {
        if (alphaTween != null && alphaTween.IsActive())
        {
            alphaTween.Kill();
        }
    }

    /// <summary>
    /// Temporarily apply a new base alpha for drag state and tween to it
    /// </summary>
    public void ApplyDragBaseAlpha(float dragBaseAlpha)
    {
        if (!isAvailable) return; // do not alter locked choices
        baseAlpha = dragBaseAlpha;
        ResetToBaseAlpha();
    }

    /// <summary>
    /// Restore original base alpha and tween to it
    /// </summary>
    public void RestoreOriginalBaseAlpha()
    {
        if (!isAvailable) return; // do not alter locked choices
        baseAlpha = originalBaseAlpha;
        ResetToBaseAlpha();
    }
} 