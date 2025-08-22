using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Added for Dictionary
using DG.Tweening;

public class TabHotkeys : MonoBehaviour
{
    [Header("Tab References")]
    [SerializeField] private GameObject storageTab, productionTab, governmentTab, researchTab, HUD;
    [SerializeField] private GameObject eventTab; // Event overlay tab (acts like Government)

    [SerializeField] private GameObject[] tabs;

    [Header("Animation Settings")]
    [SerializeField] private float tabFadeDuration = 0.1f;
    [SerializeField] private Ease tabFadeEase = Ease.OutQuad;
    [SerializeField] private float hudFadeDuration = 0.1f;
    [SerializeField] private Ease hudFadeEase = Ease.OutQuad;

    public bool tabsDisabled;
    
    // Tab state memory for events
    private Dictionary<GameObject, bool> previousTabStates = new Dictionary<GameObject, bool>();
    private bool previousHUDState = false;
    private bool isEventActive = false;
    
    // DOTween tracking
    private Dictionary<GameObject, Tween> tabFadeTweens = new Dictionary<GameObject, Tween>();
    private Tween hudFadeTween;
    
    /// <summary>
    /// Public property to check if an event is currently active
    /// </summary>
    public bool IsEventActive => isEventActive;

    private void Start()
    {
        InitializeAllTabsAsInvisible();
    }
    
    private void OnDestroy()
    {
        // Kill all active tweens to prevent memory leaks
        KillAllTabTweens();
        if (hudFadeTween != null && hudFadeTween.IsActive())
        {
            hudFadeTween.Kill();
        }
    }
    
    /// <summary>
    /// Kill all active tab fade tweens
    /// </summary>
    private void KillAllTabTweens()
    {
        foreach (var tween in tabFadeTweens.Values)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
        tabFadeTweens.Clear();
    }

    /// <summary>
    /// Set HUD visibility using CanvasGroup with smooth fade
    /// </summary>
    private void SetHUDVisibility(bool visible)
    {
        if (HUD == null) return;
        
        // Kill any existing HUD fade tween
        if (hudFadeTween != null && hudFadeTween.IsActive())
        {
            hudFadeTween.Kill();
        }
        
        CanvasGroup hudCanvasGroup = HUD.GetComponent<CanvasGroup>();
        if (hudCanvasGroup == null)
        {
            hudCanvasGroup = HUD.AddComponent<CanvasGroup>();
        }
        
        float targetAlpha = visible ? 1f : 0f;
        
        // Create smooth fade tween
        hudFadeTween = hudCanvasGroup.DOFade(targetAlpha, hudFadeDuration)
            .SetEase(hudFadeEase)
            .OnComplete(() => {
                hudCanvasGroup.interactable = visible;
                hudCanvasGroup.blocksRaycasts = visible;
            });
    }

    private void InitializeAllTabsAsInvisible()
    {
        if (tabs == null || tabs.Length == 0) return;
        
        foreach (GameObject tab in tabs)
        {
            if (tab == null) continue;
            Transform display = tab.transform.Find("Display");
            if (display != null)
            {
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = display.gameObject.AddComponent<CanvasGroup>();
                }
                
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
        
        // Ensure HUD has CanvasGroup component and is visible
        if (HUD != null)
        {
            CanvasGroup hudCanvasGroup = HUD.GetComponent<CanvasGroup>();
            if (hudCanvasGroup == null)
            {
                hudCanvasGroup = HUD.AddComponent<CanvasGroup>();
            }
            hudCanvasGroup.alpha = 1f;
            hudCanvasGroup.interactable = true;
            hudCanvasGroup.blocksRaycasts = true;
        }
    }

    public void ToggleStorageTab()
    {
        if (tabsDisabled || isEventActive) return;

        ToggleTabDisplay(storageTab, allowIndependent: true);
    }

    public void ToggleProductionTab()
    {
        if (tabsDisabled || isEventActive) return;

        ToggleTabDisplay(productionTab, allowIndependent: true);
    }

    public void ToggleGovernmentTab()
    {
        if (tabsDisabled || isEventActive) return;
        if (governmentTab == null) return;

        ToggleTabDisplay(governmentTab, updateHUD: true);
    }

    public void ToggleResearchTab()
    {
        if (tabsDisabled || isEventActive) return;

        ToggleTabDisplay(researchTab, updateHUD: true);
    }

    private void ToggleTabDisplay(GameObject tab, bool updateHUD = false, bool allowIndependent = false)
    {
        if (tab == null) return;
        
        Transform display = tab.transform.Find("Display");
        if (display == null) return;

        TooltipSystemLogic.Instance.HideTooltip();

        CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = display.gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(WaitForCanvasGroupAndToggle(tab, updateHUD, allowIndependent));
            return;
        }

        bool isVisible = canvasGroup.alpha > 0.1f;

        if (!isVisible)
        {
            if (!allowIndependent)
            {
                ClearTabs();
            }
            
            // Smooth fade in
            FadeTabIn(tab, canvasGroup, updateHUD);
        }
        else
        {
            // Smooth fade out
            FadeTabOut(tab, canvasGroup, updateHUD);
        }
    }
    
    /// <summary>
    /// Smoothly fade in a tab
    /// </summary>
    private void FadeTabIn(GameObject tab, CanvasGroup canvasGroup, bool updateHUD)
    {
        // Kill any existing fade tween for this tab
        if (tabFadeTweens.ContainsKey(tab))
        {
            tabFadeTweens[tab].Kill();
        }
        
        // Create smooth fade-in tween
        Tween fadeTween = canvasGroup.DOFade(1f, tabFadeDuration)
            .SetEase(tabFadeEase)
            .OnComplete(() => {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                
                if (updateHUD)
                {
                    // Do not pause time when opening Research/Technology tab to allow research progression
                    if (tab != researchTab)
                    {
                        TimeSystemLogic.Instance.PauseTime(true);
                    }
                    SetHUDVisibility(false);
                }
            });
        
        // Store the tween for potential cancellation
        tabFadeTweens[tab] = fadeTween;
    }
    
    /// <summary>
    /// Smoothly fade out a tab
    /// </summary>
    private void FadeTabOut(GameObject tab, CanvasGroup canvasGroup, bool updateHUD)
    {
        // Kill any existing fade tween for this tab
        if (tabFadeTweens.ContainsKey(tab))
        {
            tabFadeTweens[tab].Kill();
        }
        
        // Create smooth fade-out tween
        Tween fadeTween = canvasGroup.DOFade(0f, tabFadeDuration)
            .SetEase(tabFadeEase)
            .OnComplete(() => {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                
                if (updateHUD)
                {
                    TimeSystemLogic.Instance.PauseTime(false);
                    SetHUDVisibility(true);
                }
            });
        
        // Store the tween for potential cancellation
        tabFadeTweens[tab] = fadeTween;
    }

    private IEnumerator WaitForCanvasGroupAndToggle(GameObject tab, bool updateHUD, bool allowIndependent)
    {
        yield return null;
        ToggleTabDisplay(tab, updateHUD, allowIndependent);
    }

    private void ClearTabs()
    {
        TooltipSystemLogic.Instance.HideTooltip();

        if (tabs == null || tabs.Length == 0) return;

        foreach (GameObject tab in tabs)
        {
            if (tab == null) continue;
            
            // Skip Storage and Production tabs as they are independent and should stay open
            if (tab == storageTab || tab == productionTab) continue;
            
            Transform display = tab.transform.Find("Display");
            if (display != null)
            {
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = display.gameObject.AddComponent<CanvasGroup>();
                }
                
                if (canvasGroup != null)
                {
                    // Smooth fade out for non-independent tabs
                    FadeTabOut(tab, canvasGroup, false);
                }
            }
        }
        
        if (TimeSystemLogic.Instance != null)
        {
            TimeSystemLogic.Instance.PauseTime(false);
        }
        if (HUD != null)
        {
            SetHUDVisibility(true);
        }
    }

    public void ToggleEventTab()
    {
        if (tabsDisabled || isEventActive) return;
        if (eventTab == null) return;

        ToggleTabDisplay(eventTab, updateHUD: true);
    }

    // ===== Event Tab State Management =====
    
    /// <summary>
    /// Remember current tab states and hide all tabs for event
    /// </summary>
    public void RememberTabStatesAndHideForEvent()
    {
        if (isEventActive) return; // Already in event mode
        
        // Remember current state of all tabs
        previousTabStates.Clear();
        foreach (GameObject tab in tabs)
        {
            if (tab == null) continue;
            
            Transform display = tab.transform.Find("Display");
            if (display != null)
            {
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    previousTabStates[tab] = canvasGroup.alpha > 0.1f;
                }
            }
        }
        
        // Remember HUD state
        if (HUD != null)
        {
            CanvasGroup hudCanvasGroup = HUD.GetComponent<CanvasGroup>();
            if (hudCanvasGroup == null)
            {
                hudCanvasGroup = HUD.AddComponent<CanvasGroup>();
            }
            previousHUDState = hudCanvasGroup.alpha > 0.1f;
            
            // Smoothly hide HUD during event
            SetHUDVisibility(false);
        }
        
        // Smoothly hide all tabs EXCEPT the Event tab
        foreach (GameObject tab in tabs)
        {
            if (tab == null || tab == eventTab) continue; // Skip Event tab
            
            Transform display = tab.transform.Find("Display");
            if (display != null)
            {
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = display.gameObject.AddComponent<CanvasGroup>();
                }
                
                if (canvasGroup != null)
                {
                    // Smooth fade out
                    FadeTabOut(tab, canvasGroup, false);
                }
            }
        }
        
        // Ensure Event tab is visible and functional
        if (eventTab != null)
        {
            Transform eventDisplay = eventTab.transform.Find("Display");
            if (eventDisplay != null)
            {
                CanvasGroup eventCanvasGroup = eventDisplay.GetComponent<CanvasGroup>();
                if (eventCanvasGroup == null)
                {
                    eventCanvasGroup = eventDisplay.gameObject.AddComponent<CanvasGroup>();
                }
                
                if (eventCanvasGroup != null)
                {
                    // Smooth fade in for event tab
                    FadeTabIn(eventTab, eventCanvasGroup, false);
                }
            }
        }
        
        // Mark as event active
        isEventActive = true;
        tabsDisabled = true;
    }
    
    /// <summary>
    /// Restore previous tab states after event ends
    /// </summary>
    public void RestoreTabStatesAfterEvent()
    {
        if (!isEventActive) return; // Not in event mode
        
        // Restore previous tab states (excluding Event tab)
        foreach (var kvp in previousTabStates)
        {
            GameObject tab = kvp.Key;
            bool wasVisible = kvp.Value;
            
            if (tab == null || tab == eventTab) continue; // Skip Event tab
            
            Transform display = tab.transform.Find("Display");
            if (display != null)
            {
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = display.gameObject.AddComponent<CanvasGroup>();
                }
                
                if (canvasGroup != null)
                {
                    if (wasVisible)
                    {
                        // Smooth fade in
                        FadeTabIn(tab, canvasGroup, false);
                    }
                    else
                    {
                        // Smooth fade out
                        FadeTabOut(tab, canvasGroup, false);
                    }
                }
            }
        }
        
        // Restore HUD state
        if (HUD != null)
        {
            SetHUDVisibility(previousHUDState);
        }

        // Smoothly hide the Event tab after event ends
        if (eventTab != null)
        {
            Transform eventDisplay = eventTab.transform.Find("Display");
            if (eventDisplay != null)
            {
                CanvasGroup eventCanvasGroup = eventDisplay.GetComponent<CanvasGroup>();
                if (eventCanvasGroup != null)
                {
                    // Smooth fade out for event tab
                    FadeTabOut(eventTab, eventCanvasGroup, false);
                }
            }
        }
        
        // Clear memory and restore functionality
        previousTabStates.Clear();
        isEventActive = false;
        tabsDisabled = false;
        
        // Resume time and show HUD if no tabs are visible
        bool anyTabVisible = false;
        foreach (GameObject tab in tabs)
        {
            if (tab == null || tab == eventTab) continue; // Skip Event tab
            Transform display = tab.transform.Find("Display");
            if (display != null)
            {
                CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                if (canvasGroup != null && canvasGroup.alpha > 0.1f)
                {
                    anyTabVisible = true;
                    break;
                }
            }
        }
        
        if (!anyTabVisible)
        {
            if (TimeSystemLogic.Instance != null)
            {
                TimeSystemLogic.Instance.PauseTime(false);
            }
            if (HUD != null)
            {
                SetHUDVisibility(true);
            }
        }
    }
    
    /// <summary>
    /// Set custom fade duration for tabs
    /// </summary>
    /// <param name="duration">Fade duration in seconds</param>
    public void SetTabFadeDuration(float duration)
    {
        tabFadeDuration = Mathf.Max(0.1f, duration);
    }
    
    /// <summary>
    /// Set custom fade duration for HUD
    /// </summary>
    /// <param name="duration">Fade duration in seconds</param>
    public void SetHUDFadeDuration(float duration)
    {
        hudFadeDuration = Mathf.Max(0.1f, duration);
    }
    
    /// <summary>
    /// Set custom ease type for tab transitions
    /// </summary>
    /// <param name="ease">DOTween ease type</param>
    public void SetTabFadeEase(Ease ease)
    {
        tabFadeEase = ease;
    }
    
    /// <summary>
    /// Set custom ease type for HUD transitions
    /// </summary>
    /// <param name="ease">DOTween ease type</param>
    public void SetHUDFadeEase(Ease ease)
    {
        hudFadeEase = ease;
    }
    
    /// <summary>
    /// Get current animation settings
    /// </summary>
    /// <returns>Tuple with tab and HUD fade durations and ease types</returns>
    public (float tabDuration, float hudDuration, Ease tabEase, Ease hudEase) GetAnimationSettings()
    {
        return (tabFadeDuration, hudFadeDuration, tabFadeEase, hudFadeEase);
    }
    
    /// <summary>
    /// Switch to event tab (called when starting event from notification)
    /// </summary>
    public void SwitchToEventTab()
    {
        if (eventTab == null) return;
        
        // Hide all other tabs first
        foreach (GameObject tab in tabs)
        {
            if (tab != null && tab != eventTab)
            {
                Transform display = tab.transform.Find("Display");
                if (display != null)
                {
                    CanvasGroup canvasGroup = display.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        FadeTabOut(tab, canvasGroup, false);
                    }
                }
            }
        }
        
        // Show event tab
        Transform eventDisplay = eventTab.transform.Find("Display");
        if (eventDisplay != null)
        {
            CanvasGroup eventCanvasGroup = eventDisplay.GetComponent<CanvasGroup>();
            if (eventCanvasGroup != null)
            {
                FadeTabIn(eventTab, eventCanvasGroup, true);
            }
        }
        
        // Hide HUD during event
        SetHUDVisibility(false);
    }
}
