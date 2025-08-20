using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
/// <summary>
/// Draggable decision token for the Chorus Screen
/// Updated to work with new alpha transition system
/// </summary>
public class DecisionToken : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Token Settings")]
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private Ease returnEase = Ease.OutBack;
    
    [Header("Visual Feedback")]
    [SerializeField] private Image tokenImage;
    
    [Header("Alpha System")]
    [SerializeField] private float maxDistanceForAlpha = 200f; // Distance from center where alpha reaches max
    
    [Header("Raycast Filtering")]
    [SerializeField] private LayerMask uiBlockMask; // Layers that block hover/drop (e.g., ChorusScreen/Background)
    
    private Vector3 startPosition;
    private Vector3 screenCenter;
    private Canvas parentCanvas;
    private RectTransform rectTransform;
    private bool isDragging = false;
    private ChorusScreenManager chorusManager;
    
    private void Start()
    {
        startPosition = transform.position;
        parentCanvas = GetComponentInParent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
        chorusManager = GetComponentInParent<ChorusScreenManager>();
        
        // Calculate screen center
        if (parentCanvas != null)
        {
            screenCenter = parentCanvas.transform.position;
        }
        
        // Tag is not needed for drag and drop functionality
        // gameObject.tag = "DecisionToken";
    }
    
    /// <summary>
    /// Begin dragging the token
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        
        // Keep original alpha during dragging; visual change only on confirmed drop
        
        // Bring to front
        if (parentCanvas != null)
        {
            transform.SetAsLastSibling();
        }
        
        // Notify ChorusScreenManager that dragging has started
        if (chorusManager != null)
        {
            chorusManager.OnTokenDragStarted();
        }
    }
    
    /// <summary>
    /// Continue dragging the token
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        
        // Convert screen position to canvas position
        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform, 
            eventData.position, 
            eventData.pressEventCamera, 
            out localPointerPosition))
        {
            rectTransform.localPosition = localPointerPosition;
            
            // Calculate distance from center and update background alpha
            UpdateBackgroundAlpha(eventData);
        }
    }
    
    /// <summary>
    /// End dragging the token
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        
        // Keep original alpha when drag ends (no change here)
        
        // Check if we dropped on a valid choice
        bool droppedOnChoice = CheckIfDroppedOnChoice(eventData);
        
        Debug.Log($"[DecisionToken] OnEndDrag - droppedOnChoice: {droppedOnChoice}");
        
        if (!droppedOnChoice)
        {
            Debug.Log("[DecisionToken] Not dropped on choice, returning to start position");
            // Return to start position if not dropped on a choice
            ReturnToStartPosition();
        }
        else
        {
            Debug.Log("[DecisionToken] Dropped on choice, staying in place");
        }
        
        // Notify ChorusScreenManager that dragging has ended
        if (chorusManager != null)
        {
            chorusManager.OnTokenDragEnded();
        }
    }
    
    /// <summary>
    /// Update background alpha based on token distance from center
    /// </summary>
    private void UpdateBackgroundAlpha(PointerEventData eventData)
    {
        if (chorusManager == null) return;
        
        // Calculate distance from center
        float distanceFromCenter = Vector3.Distance(transform.position, screenCenter);
        
        // Determine which background (if any) is under the pointer
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        ChorusChoiceBackground activeBackground = null;
        foreach (var result in results)
        {
            // Skip self
            if (result.gameObject == gameObject) continue;
            // If a UI block layer is on top, stop hover
            if (((1 << result.gameObject.layer) & uiBlockMask) != 0)
            {
                activeBackground = null;
                break;
            }
            var bg = result.gameObject.GetComponent<ChorusChoiceBackground>();
            if (bg != null)
            {
                activeBackground = bg;
                break;
            }
        }
        
        if (activeBackground != null)
        {
            chorusManager.UpdateBackgroundHover(activeBackground, distanceFromCenter, maxDistanceForAlpha);
        }
        else
        {
            chorusManager.ResetAllBackgroundsToBase();
        }
    }
    
    /// <summary>
    /// Check if the token was dropped on a valid choice
    /// </summary>
    private bool CheckIfDroppedOnChoice(PointerEventData eventData)
    {
        // Check what's under the pointer
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        Debug.Log($"[DecisionToken] Raycast found {results.Count} objects under pointer");
        
        foreach (var result in results)
        {
            Debug.Log($"[DecisionToken] Checking GameObject: {result.gameObject.name} at layer {result.gameObject.layer}");
            
            // Skip self
            if (result.gameObject == gameObject) continue;
            
            // If a blocking UI element is above any choices, cancel drop
            if (((1 << result.gameObject.layer) & uiBlockMask) != 0)
            {
                Debug.Log("[DecisionToken] Drop blocked by UI block layer");
                return false;
            }
            
            // Check if we dropped on a ChorusChoiceBackground (the actual choice areas)
            ChorusChoiceBackground backgroundChoice = result.gameObject.GetComponent<ChorusChoiceBackground>();
            if (backgroundChoice != null)
            {
                Debug.Log($"[DecisionToken] SUCCESS: Found ChorusChoiceBackground on {result.gameObject.name}");
                // Trigger the background's drop handling to execute the choice logic
                backgroundChoice.OnDrop(eventData);
                return true;
            }
            else
            {
                Debug.Log($"[DecisionToken] No ChorusChoiceBackground component found on {result.gameObject.name}");
            }
        }
        
        Debug.Log("[DecisionToken] No valid drop target found");
        return false;
    }
    
    /// <summary>
    /// Return the token to its start position
    /// </summary>
    private void ReturnToStartPosition()
    {
        if (rectTransform != null)
        {
            rectTransform.DOMove(startPosition, returnDuration)
                .SetEase(returnEase)
                .SetLink(gameObject)
                .OnComplete(() => {
                    if (this != null)
                    {
                        // Ensure we're exactly at the start position
                        transform.position = startPosition;
                    }
                });
        }
    }
    
    /// <summary>
    /// Reset the token to its start position immediately
    /// </summary>
    public void ResetToStartPosition()
    {
        if (rectTransform != null)
        {
            rectTransform.DOKill(); // Stop any ongoing animations
            transform.position = startPosition;
        }
    }
    
    /// <summary>
    /// Set the token image
    /// </summary>
    public void SetTokenImage(Sprite sprite)
    {
        if (tokenImage != null)
        {
            tokenImage.sprite = sprite;
        }
    }
    
    /// <summary>
    /// Check if the token is currently being dragged
    /// </summary>
    public bool IsDragging()
    {
        return isDragging;
    }
    
    /// <summary>
    /// Get the start position of the token
    /// </summary>
    public Vector3 GetStartPosition()
    {
        return startPosition;
    }
    
    /// <summary>
    /// Set a new start position for the token
    /// </summary>
    public void SetStartPosition(Vector3 newPosition)
    {
        startPosition = newPosition;
        if (!isDragging)
        {
            transform.position = startPosition;
        }
    }
    
    /// <summary>
    /// Get the current distance from center
    /// </summary>
    public float GetDistanceFromCenter()
    {
        return Vector3.Distance(transform.position, screenCenter);
    }
} 