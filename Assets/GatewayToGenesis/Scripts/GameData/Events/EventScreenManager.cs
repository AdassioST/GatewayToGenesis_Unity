using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Ink.Runtime;

/// <summary>
/// Manages the display and interaction of event screens
/// </summary>
public class EventScreenManager : MonoBehaviour
{
    [Header("Debugging")]
    [SerializeField] private bool enableChorusDebugLogging = false;
    [Header("Screen Prefabs")]
    [SerializeField] private GameObject splashScreenPrefab;
    [SerializeField] private GameObject verseScreenPrefab;
    [SerializeField] private GameObject chorusScreenPrefab;
    [SerializeField] private GameObject bridgeScreenPrefab;
    [SerializeField] private GameObject outroScreenPrefab;

    [Header("UI References")]
    [SerializeField] private Canvas eventCanvas;
    [SerializeField] private Transform screenContainer;
    [SerializeField] private Image vignette; // Previously named "fade" component
    
    [Header("Type Text Colors By Event Type")]
    [SerializeField] private Color environmentalTextColor = new Color(0.7f, 1f, 0.7f);
    [SerializeField] private Color mysticalTextColor = new Color(0.95f, 0.75f, 0.95f);
    [SerializeField] private Color socialTextColor = new Color(0.75f, 0.8f, 1f);
    [SerializeField] private Color crisisTextColor = new Color(1f, 0.75f, 0.75f);
    
    [Header("Animation Settings")]
    [SerializeField] private float screenFadeInDuration = 0.3f;
    [SerializeField] private float screenFadeOutDuration = 0.15f;
    [SerializeField] private Ease screenFadeInEase = Ease.OutQuad;
    
    [Header("Vignette Transparency Settings")]
    [SerializeField] private float vignetteTransitionDuration = 0.3f;
    [SerializeField] private Ease vignetteTransitionEase = Ease.OutQuad;

    private EventSystemLogic eventSystem;
    private InkStoryManager inkManager;
    private EventVolumeManager volumeManager;

    private GameObject currentScreen;
    private EventScreen currentEventScreen;
    private bool isScreenComplete = false;

    private ProgressiveSentenceRevealLogic progressiveRevealLogic;

    private Dictionary<GameObject, Tween> screenFadeTweens = new Dictionary<GameObject, Tween>();
    private Tween currentVignetteTween;

    private void Awake()
    {
        eventSystem = FindObjectOfType<EventSystemLogic>();
        inkManager = FindObjectOfType<InkStoryManager>();
        volumeManager = FindObjectOfType<EventVolumeManager>();
        progressiveRevealLogic = FindObjectOfType<ProgressiveSentenceRevealLogic>();
    }

    private void OnDestroy()
    {
        KillAllScreenTweens();
        
        // Kill vignette tween
        if (currentVignetteTween != null && currentVignetteTween.IsActive())
        {
            currentVignetteTween.Kill();
        }
    }

    private void KillAllScreenTweens()
    {
        foreach (var tween in screenFadeTweens.Values)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
        screenFadeTweens.Clear();
    }

    public void ShowScreen(EventScreen screen)
    {
        if (screen == null || screenContainer == null) return;

        currentEventScreen = screen;
        isScreenComplete = false;

        // Clear existing screens
        ClearScreensExceptFade();

        // Create and show new screen based on type
        switch (screen.screenType)
        {
            case ScreenType.Splash:
                ShowSplashScreen(screen);
                break;
            case ScreenType.Verse:
                ShowVerseScreen(screen);
                break;
            case ScreenType.Chorus:
                ShowChorusScreen(screen);
                break;
            case ScreenType.Bridge:
                ShowBridgeScreen(screen);
                break;
            case ScreenType.Outro:
                ShowOutroScreen(screen);
                break;
        }
    }

    private void ClearScreensExceptFade()
    {
        if (screenContainer == null) return;

        KillAllScreenTweens();

        foreach (Transform child in screenContainer)
        {
            if (child != null)
            {
                // Skip the vignette component - it should persist across screen changes
                if (child.GetComponent<Image>() == vignette)
                    continue;
                    
                FadeScreenOut(child.gameObject);
            }
        }
    }

    private void FadeScreenOut(GameObject screen)
    {
        if (screen == null) return;
        
        CanvasGroup canvasGroup = screen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = screen.AddComponent<CanvasGroup>();
        }
        
        if (screenFadeTweens.ContainsKey(screen))
        {
            screenFadeTweens[screen].Kill();
        }
        
        Tween fadeTween = canvasGroup.DOFade(0f, screenFadeOutDuration)
            .SetEase(screenFadeInEase)
            .OnComplete(() => {
                if (screen != null) Destroy(screen);
            });
        
        screenFadeTweens[screen] = fadeTween;
    }
    
    private void FadeScreenIn(GameObject screen)
    {
        if (screen == null) return;
        
        CanvasGroup canvasGroup = screen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = screen.AddComponent<CanvasGroup>();
        }
        
        if (screenFadeTweens.ContainsKey(screen))
        {
            screenFadeTweens[screen].Kill();
        }
        
        canvasGroup.alpha = 0f;
        
        Tween fadeTween = canvasGroup.DOFade(1f, screenFadeInDuration)
            .SetEase(screenFadeInEase);
        
        screenFadeTweens[screen] = fadeTween;
    }
    
    /// <summary>
    /// Animate vignette transparency based on screen type
    /// </summary>
    private void AnimateVignetteForScreenType(ScreenType screenType)
    {
        if (vignette == null) return;
        
        // Kill any existing vignette tween
        if (currentVignetteTween != null && currentVignetteTween.IsActive())
        {
            currentVignetteTween.Kill();
        }
        
        // Set target alpha based on screen type
        float targetAlpha = GetVignetteAlphaForScreenType(screenType);
        
        // Animate to target alpha
        currentVignetteTween = vignette.DOFade(targetAlpha, vignetteTransitionDuration)
            .SetEase(vignetteTransitionEase);
    }
    
    /// <summary>
    /// Get target vignette alpha for screen type
    /// </summary>
    private float GetVignetteAlphaForScreenType(ScreenType screenType)
    {
        switch (screenType)
        {
            case ScreenType.Splash: return 0.45f;
            case ScreenType.Verse: return 0.45f;
            case ScreenType.Chorus: return 0.95f;
            case ScreenType.Bridge: return 0.80f;
            case ScreenType.Outro: return 0.45f;
            default: return 0.45f;
        }
    }

    private void ShowSplashScreen(EventScreen screen)
    {
        if (screenContainer == null) return;

        // Create the splash screen
        GameObject splashScreen = Instantiate(splashScreenPrefab, screenContainer);
        currentScreen = splashScreen;

        // Ensure CanvasGroup component exists
        CanvasGroup canvasGroup = splashScreen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = splashScreen.AddComponent<CanvasGroup>();
        }

        // Get UI components
        Transform contentTransform = splashScreen.transform.Find("Content");
        Transform titleTransform = splashScreen.transform.Find("Title");
        Transform subtitleTransform = splashScreen.transform.Find("Subtitle");
        Transform backgroundTransform = splashScreen.transform.Find("Background");

        if (contentTransform != null)
        {
            TextMeshProUGUI contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                // Get content from Ink if available, otherwise use description
                // Prefer precompiled content
                string splashContent = GetPrecompiledContentForKnot(screen.inkKnot);
                if (string.IsNullOrEmpty(splashContent))
                {
                    splashContent = screen.description ?? "Welcome to the story";
                }
                contentText.text = splashContent;
            }
        }

        if (titleTransform != null)
        {
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                // Get title from current story node if available
                StoryNode current = volumeManager?.GetCurrentStoryNode();
                titleText.text = current?.storyTitle ?? screen.title ?? "Story Begin";
            }
        }

        if (subtitleTransform != null)
        {
            TextMeshProUGUI subtitleText = subtitleTransform.GetComponent<TextMeshProUGUI>();
            if (subtitleText != null)
            {
                // Use description as subtitle if available
                subtitleText.text = screen.description ?? "";
            }
        }

        if (backgroundTransform != null)
        {
            Image backgroundImage = backgroundTransform.GetComponent<Image>();
            if (backgroundImage != null)
            {
                // Use splashImage if available, otherwise leave as prefab default
                if (screen.splashImage != null)
                {
                    backgroundImage.sprite = screen.splashImage;
                }
            }
        }

        // Add initial story consequences to cumulative list
        StoryNode currentStory = volumeManager?.GetCurrentStoryNode();
        if (currentStory != null && currentStory.storyConsequences != null)
        {
            foreach (var consequence in currentStory.storyConsequences)
            {
                if (eventSystem != null)
                {
                    eventSystem.AddConsequence(consequence);
                }
            }
        }
        
        // Setup buttons and other elements
        SetupSplashElements(screen, GetCurrentStoryUIMetadata());

        // Hook tooltip for splash image: show event description in tooltip title
        Transform imgTransform = splashScreen.transform.Find("Image");
        var tooltipGo = imgTransform != null ? imgTransform.gameObject : (backgroundTransform != null ? backgroundTransform.gameObject : null);
        if (tooltipGo != null)
        {
            var trigger = tooltipGo.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = tooltipGo.AddComponent<TooltipTrigger>();
            trigger.useCustomTooltip = true;
            // Use story description from current node
            StoryNode current = volumeManager?.GetCurrentStoryNode();
            trigger.customTitle = current != null && !string.IsNullOrEmpty(current.storyDescription) ? current.storyDescription : (screen.description ?? string.Empty);
            trigger.customDescription = string.Empty;
            trigger.customType = string.Empty;
        }

        // Animate vignette for this screen type
        AnimateVignetteForScreenType(ScreenType.Splash);

        // Smooth fade in with proper transparency
        FadeScreenIn(splashScreen);
    }

    private void ShowVerseScreen(EventScreen screen)
    {
        if (screenContainer == null) return;

        // Create the verse screen
        GameObject verseScreen = Instantiate(verseScreenPrefab, screenContainer);
        currentScreen = verseScreen;

        // Ensure CanvasGroup component exists
        CanvasGroup canvasGroup = verseScreen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = verseScreen.AddComponent<CanvasGroup>();
        }

        // Get UI components and populate from existing event system data
        Transform contentTransform = verseScreen.transform.Find("Content");
        Transform titleTransform = verseScreen.transform.Find("Title");

        if (contentTransform != null)
        {
            TextMeshProUGUI contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                // Get content from Ink if available, otherwise use description
                string verseContent = GetPrecompiledContentForKnot(screen.inkKnot);
                if (string.IsNullOrEmpty(verseContent))
                {
                    verseContent = screen.description ?? "Verse content";
                }
                contentText.text = verseContent;
            }
        }

        if (titleTransform != null)
        {
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                // Get title from current story node if available
                StoryNode current = volumeManager?.GetCurrentStoryNode();
                titleText.text = current?.storyTitle ?? screen.title ?? "Verse";
            }
        }

        // Setup progressive reveal system with the content
        string contentForReveal = GetPrecompiledContentForKnot(screen.inkKnot);
        if (string.IsNullOrEmpty(contentForReveal))
        {
            contentForReveal = screen.description ?? "Verse content";
        }
        SetupProgressiveRevealSystem(contentForReveal);

        // Setup verse button label (click handled by progressive reveal's continue)
        SetupVerseButton(screen);

        // Add tooltip to show consequences for this verse only
        Transform buttonTransform = currentScreen.transform.Find("Button");
        if (buttonTransform != null)
        {
            var btnGo = buttonTransform.gameObject;
            // Build preview text for this verse only (no cumulative)
            string preview = BuildVerseTooltipConsequencesText(screen.inkKnot);
            
            // Only add tooltip if there are actual consequences to show
            if (!string.IsNullOrEmpty(preview))
            {
                var trigger = btnGo.GetComponent<TooltipTrigger>();
                if (trigger == null) trigger = btnGo.AddComponent<TooltipTrigger>();
                trigger.useCustomTooltip = true;
                trigger.isBreakdownDisplay = true; // reuse storageBreakdown area in tooltip
                trigger.customTitle = "Has Happened...";
                trigger.customDescription = string.Empty;
                trigger.customType = string.Empty;
                trigger.customStorageBreakdown = preview;
            }
            else
            {
                // Remove tooltip trigger if no consequences (prevents empty tooltip box)
                var existingTrigger = btnGo.GetComponent<TooltipTrigger>();
                if (existingTrigger != null)
                {
                    DestroyImmediate(existingTrigger);
                }
            }
        }

        // Animate vignette for this screen type
        AnimateVignetteForScreenType(ScreenType.Verse);

        // Smooth fade in with proper transparency
        FadeScreenIn(verseScreen);
    }

    private void ShowChorusScreen(EventScreen screen)
    {
        if (screenContainer == null) return;

        Debug.Log($"[EventScreenManager] ShowChorusScreen called with screen: {screen?.screenId}, inkKnot: {screen?.inkKnot}");

        // Clear any existing chorus screens first
        ClearScreensExceptFade();

        // Create the chorus screen
        GameObject chorusScreen = Instantiate(chorusScreenPrefab, screenContainer);
        currentScreen = chorusScreen;

        Debug.Log($"[EventScreenManager] Chorus screen instantiated: {chorusScreen.name}");

        // Ensure CanvasGroup component exists
        CanvasGroup canvasGroup = chorusScreen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = chorusScreen.AddComponent<CanvasGroup>();
        }

        // Initialize the ChorusScreenManager if it exists
        ChorusScreenManager chorusManager = chorusScreen.GetComponent<ChorusScreenManager>();
        Debug.Log($"[EventScreenManager] ChorusScreenManager component found: {(chorusManager != null ? "YES" : "NO")}");
        
        if (chorusManager != null)
        {
            Debug.Log($"[EventScreenManager] Calling InitializeChorusScreen on ChorusScreenManager");
            
            // Wait a frame to ensure the component is fully initialized
            // Propagate debug flag to Chorus
            try { chorusManager.SetDebugLogging(enableChorusDebugLogging); } catch {}
            StartCoroutine(InitializeChorusScreenDelayed(chorusManager, screen));
        }
        else
        {
            Debug.LogWarning("[EventScreenManager] No ChorusScreenManager found, using legacy setup");
            // Fallback to old behavior if no ChorusScreenManager
            SetupLegacyChorusScreen(chorusScreen, screen);
        }

        // Smooth fade in with proper transparency
        FadeScreenIn(chorusScreen);

        // Animate vignette for this screen type
        AnimateVignetteForScreenType(ScreenType.Chorus);
    }
    
    /// <summary>
    /// Setup chorus screen using legacy method (fallback)
    /// </summary>
    private void SetupLegacyChorusScreen(GameObject chorusScreen, EventScreen screen)
    {
        // Get UI components and populate from existing event system data
        Transform contentTransform = chorusScreen.transform.Find("Content");
        Transform titleTransform = chorusScreen.transform.Find("Title");

        if (contentTransform != null)
        {
            TextMeshProUGUI contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                // Get content from Ink if available, otherwise use description
                string chorusContent = GetPrecompiledContentForKnot(screen.inkKnot);
                if (string.IsNullOrEmpty(chorusContent))
                {
                    chorusContent = screen.description ?? "Make a choice";
                }
                // Avoid displaying the verse button text/choice label in the content; content extractor should already stop before choices
                contentText.text = chorusContent;
            }
        }

        if (titleTransform != null)
        {
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                // Get title from current story node if available
                StoryNode current = volumeManager?.GetCurrentStoryNode();
                titleText.text = current?.storyTitle ?? screen.title ?? "Decision Point";
            }
        }

        // Start auto-advance coroutine for legacy behavior
        StartCoroutine(AutoAdvanceChorus());
    }
    
    /// <summary>
    /// Initialize chorus screen with a delay to ensure component is ready
    /// </summary>
    private IEnumerator InitializeChorusScreenDelayed(ChorusScreenManager chorusManager, EventScreen screen)
    {
        // Wait a frame to ensure the component is fully initialized
        yield return null;
        
        Debug.Log($"[EventScreenManager] Initializing ChorusScreenManager after delay");
        
        try
        {
            chorusManager.InitializeChorusScreen(screen);
            Debug.Log($"[EventScreenManager] InitializeChorusScreen call completed successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EventScreenManager] Error calling InitializeChorusScreen: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Auto-advance chorus screen after a delay (placeholder for future decision system)
    /// </summary>
    private IEnumerator AutoAdvanceChorus()
    {
        yield return new WaitForSeconds(3f); // Wait 3 seconds
        
        // Auto-advance to next screen
        if (volumeManager != null)
        {
            volumeManager.ExecuteNextScreen();
        }
    }

    private void ShowBridgeScreen(EventScreen screen)
    {
        if (screenContainer == null) return;

        // Create the bridge screen
        GameObject bridgeScreen = Instantiate(bridgeScreenPrefab, screenContainer);
        currentScreen = bridgeScreen;

        // Ensure CanvasGroup component exists
        CanvasGroup canvasGroup = bridgeScreen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = bridgeScreen.AddComponent<CanvasGroup>();
        }

        // Get UI components and populate from existing event system data
        Transform contentTransform = bridgeScreen.transform.Find("Content");
        Transform titleTransform = bridgeScreen.transform.Find("Title");

        if (contentTransform != null)
        {
            TextMeshProUGUI contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                // Get content from Ink if available, otherwise use description
                string bridgeContent = GetPrecompiledContentForKnot(screen.inkKnot);
                if (string.IsNullOrEmpty(bridgeContent))
                {
                    bridgeContent = screen.description ?? "Bridge content";
                }
                contentText.text = bridgeContent;
            }
        }

        if (titleTransform != null)
        {
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                // Get title from current story node if available
                StoryNode current = volumeManager?.GetCurrentStoryNode();
                titleText.text = current?.storyTitle ?? screen.title ?? "Bridge";
            }
        }



        // Smooth fade in with proper transparency
        FadeScreenIn(bridgeScreen);

        // Animate vignette for this screen type
        AnimateVignetteForScreenType(ScreenType.Bridge);

        // Start auto-advance coroutine
        StartCoroutine(WaitForAnyInputThenAdvance());
    }

    private void ShowOutroScreen(EventScreen screen)
    {
        if (screenContainer == null) return;

        // Create the outro screen
        GameObject outroScreen = Instantiate(outroScreenPrefab, screenContainer);
        currentScreen = outroScreen;

        // Ensure CanvasGroup component exists
        CanvasGroup canvasGroup = outroScreen.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = outroScreen.AddComponent<CanvasGroup>();
        }

        // Get UI components and populate from existing event system data
        Transform contentTransform = outroScreen.transform.Find("Content");
        Transform titleTransform = outroScreen.transform.Find("Title");

        if (contentTransform != null)
        {
            TextMeshProUGUI contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                // Get content from Ink if available, otherwise use description
                string outroContent = GetPrecompiledContentForKnot(screen.inkKnot);
                if (string.IsNullOrEmpty(outroContent))
                {
                    outroContent = screen.description ?? "Story complete";
                }
                contentText.text = outroContent;
            }
        }

        if (titleTransform != null)
        {
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                // Get title from current story node if available
                StoryNode current = volumeManager?.GetCurrentStoryNode();
                titleText.text = current?.storyTitle ?? screen.title ?? "Outro";
            }
        }

        // Setup buttons and other elements
        SetupOutroElements(screen);
        SetupOutroButton(screen);

        // Animate vignette for this screen type
        AnimateVignetteForScreenType(ScreenType.Outro);

        // Smooth fade in with proper transparency
        FadeScreenIn(outroScreen);
    }

    /// <summary>
    /// Get UI metadata from current story node
    /// </summary>
    private Dictionary<string, string> GetCurrentStoryUIMetadata()
    {
        if (volumeManager != null)
        {
            StoryNode currentStory = volumeManager.GetCurrentStoryNode();
            if (currentStory != null && currentStory.uiMetadata != null)
            {
                return currentStory.uiMetadata;
            }
        }
        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Get current story node name (used for loading default splash art)
    /// </summary>
    private string GetCurrentStoryNodeName()
    {
        if (volumeManager != null)
        {
            StoryNode currentStory = volumeManager.GetCurrentStoryNode();
            if (currentStory != null)
            {
                return currentStory.nodeName;
            }
        }
        return null;
    }

    /// <summary>
    /// Resolve the event type string, prioritizing metadata if provided
    /// </summary>
    private string GetEventTypeString(Dictionary<string, string> uiMetadata, EventScreen screen)
    {
        if (uiMetadata != null && uiMetadata.ContainsKey("event_type") && !string.IsNullOrEmpty(uiMetadata["event_type"]))
        {
            return uiMetadata["event_type"].Trim();
        }
        return screen.eventType.ToString();
    }

    private EventType GetEventTypeFromMetadata(Dictionary<string, string> uiMetadata, EventType fallback)
    {
        if (uiMetadata != null && uiMetadata.ContainsKey("event_type"))
        {
            string typeStr = uiMetadata["event_type"].Trim();
            if (System.Enum.TryParse<EventType>(typeStr, true, out var parsed))
            {
                return parsed;
            }
        }
        return fallback;
    }

    private Color GetHardTextColorByEventType(EventType type)
    {
        switch (type)
        {
            case EventType.Environmental: return environmentalTextColor;
            case EventType.Mystical: return mysticalTextColor;
            case EventType.Social: return socialTextColor;
            case EventType.Crisis: return crisisTextColor;
            default: return Color.white;
        }
    }

    /// <summary>
    /// Set up splash screen UI elements with metadata support
    /// </summary>
    private void SetupSplashElements(EventScreen screen, Dictionary<string, string> uiMetadata)
    {
        if (currentScreen == null) return;
        
        // Background: static per prefab (do not change)

        // Splash image
        Transform imageTransform = currentScreen.transform.Find("Image");
        if (imageTransform != null)
        {
            Image splashImage = imageTransform.GetComponent<Image>();
            if (splashImage != null)
            {
                // Preferred: load art named after the current story node
                string nodeName = GetCurrentStoryNodeName();
                bool applied = false;
                if (!string.IsNullOrEmpty(nodeName))
                {
                    // Try Resources root first, then EventArt subfolder
                    Sprite nodeSprite = Resources.Load<Sprite>(nodeName) ?? Resources.Load<Sprite>($"EventArt/{nodeName}");
                    if (nodeSprite != null)
                    {
                        splashImage.sprite = nodeSprite;
                        applied = true;
                    }
                }

                // Fallbacks: metadata-provided splash_art, then preassigned sprite
                if (!applied && uiMetadata.ContainsKey("splash_art"))
                {
                    Sprite splashSprite = LoadSpriteFromResources(uiMetadata["splash_art"]);
                    if (splashSprite != null)
                    {
                        splashImage.sprite = splashSprite;
                        applied = true;
                    }
                }

                if (!applied && screen.splashImage != null)
                {
                    splashImage.sprite = screen.splashImage;
                }
            }
        }

        // Event color (now driven by type-based sprite from Resources/EventArt)
        Transform colorTransform = currentScreen.transform.Find("EventColor");
        if (colorTransform != null)
        {
            Image colorImage = colorTransform.GetComponent<Image>();
            if (colorImage != null)
            {
                // Load sprite based on event type name: Resources/EventArt/EventColor_{Type}
                string typeString = GetEventTypeString(uiMetadata, screen);
                Sprite typeColorSprite = Resources.Load<Sprite>($"EventArt/EventColor_{typeString}");
                if (typeColorSprite != null)
                {
                    colorImage.sprite = typeColorSprite;
                    colorImage.color = Color.white; // ensure sprite displays as-authored
                }
                else
                {
                    // Fallback: legacy metadata color or enum-based color tint
                    if (uiMetadata.ContainsKey("event_color"))
                    {
                        colorImage.color = ParseColorFromMetadata(uiMetadata["event_color"]);
                    }
                    else
                    {
                        colorImage.color = GetEventColor(screen.eventType);
                    }
                }
            }
        }

        // Content text (always from Ink when available)
        Transform contentTransform = currentScreen.transform.Find("Content");
        if (contentTransform != null)
        {
            TextMeshProUGUI contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                string splashText = GetPrecompiledContentForKnot(screen.inkKnot);
                if (string.IsNullOrEmpty(splashText))
                {
                    // Fallback to description if Ink not found
                    StoryNode current = volumeManager?.GetCurrentStoryNode();
                    splashText = current?.storyDescription ?? screen.description;
                }
                // Wrap in quotes to create book-like feel, trim trailing newline/whitespace
                string splashTrimmed = splashText.Replace("\r\n", "\n").TrimEnd('\n', '\r', ' ');
                contentText.text = $"\"{splashTrimmed}\"";

                // Keep content text color unchanged (only Type text is tinted)
            }
        }

        // Event type
        Transform typeTransform = currentScreen.transform.Find("Type");
        if (typeTransform != null)
        {
            TextMeshProUGUI typeText = typeTransform.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                // Use event type from metadata if available
                if (uiMetadata.ContainsKey("event_type"))
                {
                    typeText.text = uiMetadata["event_type"];
                }
                else
                {
                    typeText.text = screen.eventType.ToString();
                }
                
                // Hard set Type text color by event type
                EventType effectiveType = GetEventTypeFromMetadata(uiMetadata, screen.eventType);
                typeText.color = GetHardTextColorByEventType(effectiveType);
            }
        }

        // Event name/title (keep fixed colors)
        Transform nameTransform = currentScreen.transform.Find("Name");
        if (nameTransform != null)
        {
            TextMeshProUGUI nameText = nameTransform.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                StoryNode current = volumeManager?.GetCurrentStoryNode();
                nameText.text = current?.storyTitle ?? screen.title;
            }
        }

        // Button text
        Transform buttonTextTransform = currentScreen.transform.Find("ButtonText");
        if (buttonTextTransform != null)
        {
            TextMeshProUGUI buttonText = buttonTextTransform.GetComponent<TextMeshProUGUI>();
            if (buttonText != null)
            {
                // Use button text from metadata if available
                if (uiMetadata.ContainsKey("button_text"))
                {
                    buttonText.text = uiMetadata["button_text"];
                }
                else
                {
                    buttonText.text = screen.buttonText ?? "Continue";
                }
            }
        }
        
        // Setup button click handlers
        SetupSplashButtonsFromInkChoices(screen);
    }

    /// <summary>
    /// Set up the verse screen button for manual progression
    /// </summary>
    private void SetupVerseButton(EventScreen screen)
    {
        Transform buttonTransform = currentScreen.transform.Find("Button");
        Transform buttonTextTransform = currentScreen.transform.Find("ButtonText");
        if (buttonTransform != null)
        {
            Button button = buttonTransform.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonTextTransform ? buttonTextTransform.GetComponent<TextMeshProUGUI>() : null;
            if (buttonText == null && buttonTransform != null)
            {
                // Fallback: find TMP label under the button
                buttonText = buttonTransform.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (button != null)
            {
                // Only set label here; let ProgressiveSentenceRevealLogic trigger OnVerseContinuePressed
                string label = GetPrecompiledFirstChoiceText(screen.inkKnot);
                label = FormatChoiceLabel(label);
                if (buttonText != null)
                {
                    if (!string.IsNullOrEmpty(label))
                    {
                        buttonText.text = label;
                        Debug.Log($"[EventScreenManager] Verse button label set to '{label}' for knot '{screen.inkKnot}'");
                    }
                    else
                    {
                        // Robust fallback: use runtime extraction or final default
                        string runtimeLabel = GetRuntimeFirstChoiceText(screen.inkKnot);
                        string finalLabel = !string.IsNullOrEmpty(runtimeLabel) ? FormatChoiceLabel(runtimeLabel) : (screen.buttonText ?? "Continue");
                        buttonText.text = finalLabel;
                        Debug.Log($"[EventScreenManager] Verse button label fallback to '{finalLabel}' for knot '{screen.inkKnot}'");
                    }
                }
                // Do not add an extra onClick here to avoid double invocation with progressive reveal's handler
            }
        }
    }





    /// <summary>
    /// Set up the splash screen button for manual progression
    /// </summary>
    private void SetupSplashButtonsFromInkChoices(EventScreen screen)
    {
        // Primary button and label
        Transform buttonTransform = currentScreen.transform.Find("Button");
        Transform buttonTextTransform = currentScreen.transform.Find("ButtonText");
        Button button = buttonTransform ? buttonTransform.GetComponent<Button>() : null;
        TextMeshProUGUI buttonText = buttonTextTransform ? buttonTextTransform.GetComponent<TextMeshProUGUI>() : null;

        if (button == null || buttonText == null)
        {
            return;
        }

        // Pull first available choice from precompiled index and navigate directly to its target
        string choiceText = null;
        System.Action onClick = null;

        string firstChoiceText = GetPrecompiledFirstChoiceText(screen.inkKnot);
        string firstChoiceTarget = GetPrecompiledFirstChoiceTarget(screen.inkKnot);
        Debug.Log($"[EventScreenManager] Splash resolve: knot='{screen.inkKnot}', precompiledLabel='{firstChoiceText}', target='{firstChoiceTarget}'");
        if (!string.IsNullOrEmpty(firstChoiceText))
        {
            choiceText = FormatChoiceLabel(firstChoiceText);
            onClick = () =>
            {
                string target = GetPrecompiledFirstChoiceTarget(screen.inkKnot);
                target = InkDrivenEventSetup.NormalizeKnotName(target) ?? target;
                if (!string.IsNullOrEmpty(target))
                {
                    Debug.Log($"[EventScreenManager] Splash click navigating to '{target}'");
                    volumeManager?.NavigateToKnot(target);
                }
            };
            Debug.Log($"[EventScreenManager] Splash choice label set to '{choiceText}' from knot '{screen.inkKnot}'");
        }

        // Fallback if no Ink choice available
        if (string.IsNullOrEmpty(choiceText))
        {
            Dictionary<string, string> uiMetadata = GetCurrentStoryUIMetadata();
            choiceText = uiMetadata.ContainsKey("button_text") ? uiMetadata["button_text"] : (screen.buttonText ?? "Continue");
            onClick = () => OnSplashButtonPressed();
        }

        button.onClick.RemoveAllListeners();
        if (onClick != null) button.onClick.AddListener(() => onClick());
        else
        {
            button.onClick.AddListener(() => OnSplashButtonPressed());
        }
        buttonText.text = choiceText;
    }

    /// <summary>
    /// Get the first Ink choice text available at a given knot using precompiled data
    /// </summary>
    private string GetFirstChoiceTextForKnot(string knotName)
    {
        return GetPrecompiledFirstChoiceText(knotName);
    }

    /// <summary>
    /// Handle splash button press - advance to next screen
    /// </summary>
    private void OnSplashButtonPressed()
    {
        LogScreen("Splash button pressed - advancing to next screen");
        // Prefer navigating to first precompiled choice target, else complete
        string target = GetPrecompiledFirstChoiceTarget(currentEventScreen?.inkKnot);
        if (!string.IsNullOrEmpty(target))
        {
            volumeManager?.NavigateToKnot(target);
        }
        else
        {
            volumeManager?.CompleteStory();
        }
    }



    /// <summary>
    /// Set up the progressive reveal system for verse content
    /// </summary>
    private void SetupProgressiveRevealSystem(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            LogScreen("Warning: No content provided for progressive reveal system");
            return;
        }

        // Find or add the progressive reveal logic component
        progressiveRevealLogic = currentScreen.GetComponent<ProgressiveSentenceRevealLogic>();
        if (progressiveRevealLogic == null)
        {
            progressiveRevealLogic = currentScreen.AddComponent<ProgressiveSentenceRevealLogic>();
        }

        // Set up event handlers
        progressiveRevealLogic.OnRevealComplete += OnVerseRevealComplete;
        progressiveRevealLogic.OnContinuePressed += OnVerseContinuePressed;
        
        // Start the progressive reveal with content
        progressiveRevealLogic.StartReveal(content);
    }

    /// <summary>
    /// Handle verse reveal completion
    /// </summary>
    private void OnVerseRevealComplete()
    {
        LogScreen("Verse progressive reveal completed");
        // The continue button is now visible and interactable
    }

    /// <summary>
    /// Handle verse continue button press
    /// </summary>
    private void OnVerseContinuePressed()
    {
        LogScreen("Verse continue button pressed - advancing to next screen");
        
        // Clean up event handlers
        if (progressiveRevealLogic != null)
        {
            progressiveRevealLogic.OnRevealComplete -= OnVerseRevealComplete;
            progressiveRevealLogic.OnContinuePressed -= OnVerseContinuePressed;
        }
        
        // Navigate using edges with loop prevention and runtime fallback
        if (volumeManager != null && !string.IsNullOrEmpty(currentEventScreen?.inkKnot))
        {
            string source = currentEventScreen.inkKnot;
            string target = GetPrecompiledFirstChoiceTarget(source);
            Debug.Log($"[EventScreenManager] Verse continue: source='{source}', precompiledTarget='{target}'");
            if (string.IsNullOrEmpty(target))
            {
                // If no target from edges, try runtime resolution before giving up
                string runtimeTarget = GetRuntimeSingleChoiceTarget(source);
                if (!string.IsNullOrEmpty(runtimeTarget))
                {
                    Debug.Log($"[EventScreenManager] Verse continue: runtimeTarget='{runtimeTarget}'");
                    target = runtimeTarget;
                }
            }
            if (target == source)
            {
                string runtimeTarget = GetRuntimeSingleChoiceTarget(source);
                if (!string.IsNullOrEmpty(runtimeTarget) && runtimeTarget != source)
                {
                    Debug.Log($"[EventScreenManager] Verse continue: resolved loop with runtimeTarget='{runtimeTarget}'");
                    target = runtimeTarget;
                }
                else if (InkDrivenEventSetup.TryGetKnot(source, out var knot) && !string.IsNullOrEmpty(knot.nextKnotIfNoChoices))
                {
                    Debug.Log($"[EventScreenManager] Verse continue: using nextKnotIfNoChoices='{knot.nextKnotIfNoChoices}'");
                    target = knot.nextKnotIfNoChoices;
                }
                else
                {
                    target = null;
                }
            }
            // Normalize before navigating
            if (!string.IsNullOrEmpty(target))
            {
                target = InkDrivenEventSetup.NormalizeKnotName(target) ?? target;
                Debug.Log($"[EventScreenManager] Verse continue: normalized target='{target}'");
            }
            if (!string.IsNullOrEmpty(target))
            {
                // Accumulate verse-level consequences from the current knot's single choice (&C consequences: ...)
                string firstChoiceText = GetPrecompiledFirstChoiceText(source);
                var choiceConsequences = ParseVerseChoiceConsequences(firstChoiceText);
                if (choiceConsequences != null && choiceConsequences.Count > 0 && eventSystem != null)
                {
                    foreach (var ec in choiceConsequences) eventSystem.AddConsequence(ec);
                    Debug.Log($"[EventScreenManager] Accumulated {choiceConsequences.Count} consequences from verse '{source}'");
                }

                // Apply any deferred consequences for this verse before navigation
                var deferred = InkDrivenEventSetup.GetAndClearDeferredConsequences(target);
                if (deferred != null && deferred.Count > 0 && eventSystem != null)
                {
                    foreach (var ec in deferred)
                    {
                        eventSystem.AddConsequence(ec);
                    }
                    Debug.Log($"[EventScreenManager] Applied {deferred.Count} deferred consequences for verse '{target}'");
                }

                // If this is clearly a chorus knot by suffix, navigate expecting chorus
                var lower = target.ToLower();
                if (lower.EndsWith("_chorus") || lower.Contains("_chorus_"))
                {
                    Debug.Log($"[EventScreenManager] Verse continue -> navigating to Chorus knot '{target}'");
                }
                else
                {
                    Debug.Log($"[EventScreenManager] Verse continue -> navigating to knot '{target}'");
                }

                volumeManager.NavigateToKnot(target);
                return;
            }
        }
        // Complete if nothing to navigate to
        volumeManager?.CompleteStory();
    }

    /// <summary>
    /// If a knot yields exactly one choice, return its target path; otherwise null
    /// </summary>
    private string GetPrecompiledFirstChoiceText(string knotName)
    {
        if (string.IsNullOrEmpty(knotName)) return null;
        if (InkDrivenEventSetup.TryGetKnot(knotName, out var knot) && knot.choices != null && knot.choices.Count > 0)
        {
            return knot.choices[0].text;
        }
        // Fallback: runtime extraction
        return GetRuntimeFirstChoiceText(knotName);
    }

    private string GetPrecompiledFirstChoiceTarget(string knotName)
    {
        if (string.IsNullOrEmpty(knotName)) return null;
        if (InkDrivenEventSetup.TryGetKnot(knotName, out var knot))
        {
            if (knot.choices != null && knot.choices.Count > 0)
            {
                var ret = knot.choices[0].targetPath;
                return InkDrivenEventSetup.NormalizeKnotName(ret) ?? ret;
            }
            if (!string.IsNullOrEmpty(knot.nextKnotIfNoChoices))
            {
                var ret = knot.nextKnotIfNoChoices;
                return InkDrivenEventSetup.NormalizeKnotName(ret) ?? ret;
            }
        }
        // Fallback: runtime extraction
        return GetRuntimeSingleChoiceTarget(knotName);
    }

    // Format a raw choice text for display, stripping &D and &C segments
    private string FormatChoiceLabel(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        int dIdx = raw.IndexOf("&D ", System.StringComparison.Ordinal);
        int cIdx = raw.IndexOf("&C ", System.StringComparison.Ordinal);
        int cut = raw.Length;
        if (dIdx >= 0) cut = Mathf.Min(cut, dIdx);
        if (cIdx >= 0) cut = Mathf.Min(cut, cIdx);
        string head = raw.Substring(0, cut).Trim();
        // If author used legacy ' -> target' inline, strip it as well
        int arrow = head.IndexOf("->", System.StringComparison.Ordinal);
        if (arrow >= 0) head = head.Substring(0, arrow).Trim();
        // If choice has the typical "* " prefix trimmed upstream, we just return head
        // For safety, remove a leading '*' if present
        if (head.StartsWith("* ")) head = head.Substring(2).Trim();
        return head;
    }

    // Parse &C consequences from a verse/bridge single-button choice text
    private List<EventConsequence> ParseVerseChoiceConsequences(string choiceText)
    {
        var list = new List<EventConsequence>();
        if (string.IsNullOrEmpty(choiceText)) return list;
        int cIdx = choiceText.IndexOf("&C ", System.StringComparison.Ordinal);
        if (cIdx < 0) return list;
        string metadata = choiceText.Substring(cIdx + 3).Trim();
        if (string.IsNullOrEmpty(metadata)) return list;

        // Find "consequences:" section within metadata
        int consIdx = metadata.IndexOf("consequences:", System.StringComparison.OrdinalIgnoreCase);
        if (consIdx < 0) return list;
        string consPayload = metadata.Substring(consIdx + "consequences:".Length).Trim();

        // Stop at next top-level section marker if found
        string[] stopKeys = new [] { "success:", "failure:", "crit_success:", "crit_failure:", "rare_event:", "rare_event_percent:", "pillar:", "strength:", "challenge:" };
        int stopAt = consPayload.Length;
        foreach (var key in stopKeys)
        {
            int idx = consPayload.IndexOf(key, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < stopAt) stopAt = idx;
        }
        consPayload = consPayload.Substring(0, stopAt).Trim().TrimEnd(';');
        if (string.IsNullOrEmpty(consPayload)) return list;

        var items = consPayload.Split(';');
        EventConsequence lastEc = null;
        foreach (var raw in items)
        {
            string item = raw.Trim();
            if (string.IsNullOrEmpty(item)) continue;
            // Attach duration to last parsed eligible consequence
            if (item.StartsWith("duration", System.StringComparison.OrdinalIgnoreCase))
            {
                if (lastEc != null)
                {
                    int idx = item.IndexOf(':');
                    if (idx >= 0)
                    {
                        string tail = item.Substring(idx + 1).Trim();
                        // support duration:sevenths:N and duration:N
                        int lastColon = tail.LastIndexOf(':');
                        string numStr = lastColon >= 0 ? tail.Substring(lastColon + 1).Trim() : tail;
                        if (int.TryParse(numStr, out int dur))
                        {
                            lastEc.durationSevenths = Mathf.Max(0, dur);
                        }
                    }
                }
                continue;
            }
            var ec = ParseSingleConsequenceInline(item);
            if (ec != null)
            {
                list.Add(ec);
                lastEc = ec;
            }
        }
        return list;
    }

    private EventConsequence ParseSingleConsequenceInline(string str)
    {
        // Supported formats:
        //  - type:TargetName +/-Value
        //  - technology:Tech Name enlightened
        int colon = str.IndexOf(':');
        if (colon <= 0) return null;
        string type = str.Substring(0, colon).Trim();
        string body = str.Substring(colon + 1).Trim();

        // Special case for technology enlightened (no numeric value)
        if (type.Equals("technology", System.StringComparison.OrdinalIgnoreCase) &&
            body.IndexOf("enlightened", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            string targetTech = body.Replace("enlightened", "", System.StringComparison.OrdinalIgnoreCase).Trim();
            return new EventConsequence
            {
                type = MapConsequenceType(type),
                targetName = targetTech,
                value = 0
            };
        }

        int lastSpace = body.LastIndexOf(' ');
        if (lastSpace <= 0) return null;
        string target = body.Substring(0, lastSpace).Trim();
        string valStr = body.Substring(lastSpace + 1).Trim();
        if (!int.TryParse(valStr, out int value)) return null;
        return new EventConsequence
        {
            type = MapConsequenceType(type),
            targetName = target,
            value = value
        };
    }

    private EventConsequence.ConsequenceType MapConsequenceType(string type)
    {
        switch ((type ?? string.Empty).ToLower())
        {
            case "score": return EventConsequence.ConsequenceType.ScoreChange;
            case "resource": return EventConsequence.ConsequenceType.ResourceChange;
            case "production": return EventConsequence.ConsequenceType.ProductionUnitChange;
            case "stat": return EventConsequence.ConsequenceType.StatChange;
            case "population": return EventConsequence.ConsequenceType.PopulationChange;
            case "housing": return EventConsequence.ConsequenceType.HousingChange;
            case "vagrants": return EventConsequence.ConsequenceType.VagrantsChange;
            case "deaths": return EventConsequence.ConsequenceType.DeathsChange;
            case "death_records_revision": return EventConsequence.ConsequenceType.DeathRecordsRevision;
            case "technology": return EventConsequence.ConsequenceType.TechnologyEnlightened;
            case "production_percent": return EventConsequence.ConsequenceType.ProductionPercentChange;
            case "production_percent_section": return EventConsequence.ConsequenceType.ProductionPercentChangeSection;
            case "click_power": return EventConsequence.ConsequenceType.ClickPowerChange;
            case "click_power_percent": return EventConsequence.ConsequenceType.ClickPowerPercentChange;
            case "click_power_section": return EventConsequence.ConsequenceType.ClickPowerChangeSection;
            case "click_power_percent_section": return EventConsequence.ConsequenceType.ClickPowerPercentChangeSection;
            default: return EventConsequence.ConsequenceType.ScoreChange;
        }
    }

    /// <summary>
    /// Get content from Ink for the current screen step using screen.inkKnot
    /// </summary>
    private string GetInkContentForScreen(EventScreen screen)
    {
        return GetPrecompiledContentForKnot(screen.inkKnot);
    }

    /// <summary>
    /// Build content text for a knot by continuing until choices appear or content ends (fallback method)
    /// </summary>
    private string GetInkContentForKnot(string knotName)
    {
        // Fallback-only; prefer precompiled
        if (string.IsNullOrEmpty(knotName)) return string.Empty;
        EventVolume vol = volumeManager?.GetCurrentVolume();
        if (vol == null || vol.inkMasterfile == null) return string.Empty;
        try
        {
            Story tmp = new Story(vol.inkMasterfile.text);
            tmp.ChoosePathString(knotName);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            while (tmp.canContinue)
            {
                string line = tmp.Continue();
                if (!string.IsNullOrEmpty(line)) sb.Append(line);
                if (tmp.currentChoices != null && tmp.currentChoices.Count > 0) break;
            }
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    private string GetPrecompiledContentForKnot(string knotName)
    {
        if (string.IsNullOrEmpty(knotName)) return string.Empty;
        if (InkDrivenEventSetup.TryGetKnot(knotName, out var knot))
        {
            if (!string.IsNullOrEmpty(knot.content)) return knot.content;
        }
        // Fallback to runtime content extraction if precompiled missing
        return GetInkContentForKnot(knotName);
    }

    /// <summary>
    /// Fallback: read first choice text at runtime from compiled story
    /// </summary>
    private string GetRuntimeFirstChoiceText(string knotName)
    {
        EventVolume vol = volumeManager?.GetCurrentVolume();
        if (string.IsNullOrEmpty(knotName) || vol == null || vol.inkMasterfile == null) return null;
        try
        {
            Story tmp = new Story(vol.inkMasterfile.text);
            tmp.ChoosePathString(knotName);
            while (tmp.canContinue && (tmp.currentChoices == null || tmp.currentChoices.Count == 0))
            {
                tmp.Continue();
            }
            if (tmp.currentChoices != null && tmp.currentChoices.Count > 0)
            {
                return tmp.currentChoices[0].text;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Fallback: if exactly one choice exists at runtime, return its target
    /// </summary>
    private string GetRuntimeSingleChoiceTarget(string knotName)
    {
        EventVolume vol = volumeManager?.GetCurrentVolume();
        if (string.IsNullOrEmpty(knotName) || vol == null || vol.inkMasterfile == null) return null;
        try
        {
            Story tmp = new Story(vol.inkMasterfile.text);
            tmp.ChoosePathString(knotName);
            while (tmp.canContinue && (tmp.currentChoices == null || tmp.currentChoices.Count == 0))
            {
                tmp.Continue();
            }
            if (tmp.currentChoices != null && tmp.currentChoices.Count >= 1)
            {
                var c0 = tmp.currentChoices[0];
                string raw = c0.targetPath != null ? c0.targetPath.ToString() : null;
                string normalized = InkDrivenEventSetup.NormalizeKnotName(raw) ?? raw;
                if (!string.IsNullOrEmpty(normalized) && normalized != knotName)
                {
                    return normalized;
                }
                // If targetPath is null or resolves to self, simulate the choice to discover the next knot
                try
                {
                    Story sim = new Story(vol.inkMasterfile.text);
                    sim.ChoosePathString(knotName);
                    while (sim.canContinue && (sim.currentChoices == null || sim.currentChoices.Count == 0))
                    {
                        sim.Continue();
                    }
                    if (sim.currentChoices != null && sim.currentChoices.Count > 0)
                    {
                        sim.ChooseChoiceIndex(0);
                        int guard = 0;
                        string last = InkDrivenEventSetup.GetTopLevelKnotNameFromPath(sim.state.currentPathString);
                        while (guard++ < 64 && sim.canContinue)
                        {
                            sim.Continue();
                            string top = InkDrivenEventSetup.GetTopLevelKnotNameFromPath(sim.state.currentPathString);
                            if (!string.IsNullOrEmpty(top) && top != knotName) return top;
                            if (!string.IsNullOrEmpty(top)) last = top;
                        }
                        return last;
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }



    /// <summary>
    /// Handle bridge continue button press
    /// </summary>
    private void OnBridgeContinuePressed()
    {
        LogScreen("Bridge continue pressed - advancing to next screen");
        isScreenComplete = true;
        // Navigate by Ink edges rather than static flow
        if (volumeManager != null && currentEventScreen != null)
        {
            string source = currentEventScreen.inkKnot;
            string target = GetPrecompiledFirstChoiceTarget(source);
            if (string.IsNullOrEmpty(target))
            {
                target = GetRuntimeSingleChoiceTarget(source);
            }
            if (!string.IsNullOrEmpty(target))
            {
                // Accumulate bridge-level consequences from the button choice (&C consequences: ...), if any
                string firstChoiceText = GetPrecompiledFirstChoiceText(source);
                var bridgeConsequences = ParseVerseChoiceConsequences(firstChoiceText);
                if (bridgeConsequences != null && bridgeConsequences.Count > 0 && eventSystem != null)
                {
                    foreach (var ec in bridgeConsequences) eventSystem.AddConsequence(ec);
                    LogScreen($"Accumulated {bridgeConsequences.Count} consequences from bridge '{source}'");
                }
                target = InkDrivenEventSetup.NormalizeKnotName(target) ?? target;
                LogScreen($"Bridge continue navigating to '{target}' from '{source}'");
                volumeManager.NavigateToKnot(target);
                return;
            }
            // If no target, complete
            LogScreen("Bridge had no target; completing story");
            volumeManager.CompleteStory();
        }
    }

    private IEnumerator WaitForAnyInputThenAdvance()
    {
        // Wait until any key or mouse button is pressed or screen is destroyed
        while (currentScreen != null)
        {
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                OnBridgeContinuePressed();
                yield break;
            }
            yield return null;
        }
    }





    /// <summary>
    /// Set up outro screen UI elements
    /// </summary>
    private void SetupOutroElements(EventScreen screen)
    {
        if (currentScreen == null) return;
        
        // Background: static per prefab (do not change)

        // Title
        Transform titleTransform = currentScreen.transform.Find("Title");
        if (titleTransform != null)
        {
            TextMeshProUGUI titleText = titleTransform.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                StoryNode current = volumeManager?.GetCurrentStoryNode();
                titleText.text = current?.storyTitle ?? screen.title ?? "Complete";
            }
        }

        // Content from Ink if present
        Transform contentTransform = currentScreen.transform.Find("Content");
        if (contentTransform != null)
        {
            TextMeshProUGUI contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            if (contentText != null)
            {
                string outroText = GetPrecompiledContentForKnot(screen.inkKnot);
                if (string.IsNullOrEmpty(outroText))
                {
                    outroText = screen.description ?? "Story complete.";
                }
                contentText.text = outroText;
            }
        }

        // Consequences header text (HasHappened)
        Transform happenedTransform = currentScreen.transform.Find("HasHappened");
        if (happenedTransform != null)
        {
            TextMeshProUGUI happenedText = happenedTransform.GetComponent<TextMeshProUGUI>();
            if (happenedText != null)
            {
                happenedText.text = "Has Happened...";
            }
        }

        // Consequences list - now shows cumulative consequences from entire story flow
        Transform consequencesTransform = currentScreen.transform.Find("Consequences");
        if (consequencesTransform != null)
        {
            TextMeshProUGUI consequencesText = consequencesTransform.GetComponent<TextMeshProUGUI>();
            if (consequencesText != null)
            {
                // Get cumulative consequences from EventSystemLogic instead of just the story node
                List<EventConsequence> cumulativeConsequences = eventSystem?.GetCumulativeConsequences() ?? new List<EventConsequence>();
                consequencesText.text = BuildConsequencesPreviewText(cumulativeConsequences);
            }
        }

        // Button text (prefer Ink choice text at outro knot)
        Transform buttonTextTransform = currentScreen.transform.Find("ButtonText");
        if (buttonTextTransform != null)
        {
            TextMeshProUGUI buttonText = buttonTextTransform.GetComponent<TextMeshProUGUI>();
            if (buttonText != null)
            {
                string outroChoice = GetPrecompiledFirstChoiceText(screen.inkKnot);
                outroChoice = FormatChoiceLabel(outroChoice);
                buttonText.text = !string.IsNullOrEmpty(outroChoice) ? outroChoice : (screen.buttonText ?? "Continue");
            }
        }
    }

    /// <summary>
    /// Set up the outro screen button for manual progression
    /// </summary>
    private void SetupOutroButton(EventScreen screen)
    {
        if (currentScreen == null)
        {
            LogScreen("ERROR: currentScreen is null in SetupOutroButton");
            return;
        }

        Transform buttonTransform = currentScreen.transform.Find("Button");
        if (buttonTransform != null)
        {
            Button button = buttonTransform.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnOutroButtonPressed());
                LogScreen($"Outro button setup complete - listener added");
            }
            else
            {
                LogScreen($"ERROR: Button component not found on Button transform");
            }
        }
        else
        {
            LogScreen($"ERROR: Button transform not found. Available children: {GetChildrenNames(currentScreen.transform)}");
        }
    }

    private string GetChildrenNames(Transform parent)
    {
        if (parent == null) return "null";
        string[] names = new string[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            names[i] = parent.GetChild(i).name;
        }
        return string.Join(", ", names);
    }

    /// <summary>
    /// Handle outro button press - complete the story
    /// </summary>
    private void OnOutroButtonPressed()
    {
        LogScreen("Outro button pressed - completing story");
        
        // Prevent multiple button presses
        if (isScreenComplete)
        {
            LogScreen("Button already pressed, ignoring duplicate press");
            return;
        }
        
        isScreenComplete = true;
        
        // Disable the button immediately to prevent multiple clicks
        Transform buttonTransform = currentScreen?.transform.Find("Button");
        if (buttonTransform != null)
        {
            Button button = buttonTransform.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = false;
                LogScreen("Button disabled to prevent multiple presses");
            }
        }
        
        // Kill any DOTween tweens running on the current screen to prevent "missing target" errors
        if (currentScreen != null)
        {
            // Kill all tweens on this screen's components
            DOTween.Kill(currentScreen);
            
            // Also kill any tweens on child objects that might have tweens
            CanvasGroup[] canvasGroups = currentScreen.GetComponentsInChildren<CanvasGroup>();
            foreach (var cg in canvasGroups)
            {
                if (cg != null) DOTween.Kill(cg);
            }
            
            // Kill any tweens on the screen's transform
            DOTween.Kill(currentScreen.transform);
            
            LogScreen("Killed all DOTween tweens on current screen to prevent errors");
        }
        
        // Complete the story through the event system
        if (eventSystem != null)
        {
            LogScreen("Calling eventSystem.OnStoryCompleted()");
            eventSystem.OnStoryCompleted();
        }
        else if (volumeManager != null)
        {
            LogScreen("eventSystem is null, calling volumeManager.CompleteStory()");
            volumeManager.CompleteStory();
        }
        else
        {
            LogScreen("ERROR: Both eventSystem and volumeManager are null!");
        }
    }

    /// <summary>
    /// Get event color based on event type
    /// </summary>
    private Color GetEventColor(EventType eventType)
    {
        switch (eventType)
        {
            case EventType.Environmental:
                return new Color(0.2f, 0.8f, 0.2f); // Green
            case EventType.Mystical:
                return new Color(0.8f, 0.2f, 0.8f); // Purple
            case EventType.Social:
                return new Color(0.2f, 0.2f, 0.8f); // Blue
            case EventType.Crisis:
                return new Color(0.8f, 0.2f, 0.2f); // Red
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Load sprite from Resources folder
    /// </summary>
    private Sprite LoadSpriteFromResources(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
            return null;

        // Try to load from different possible paths
        string[] possiblePaths = {
            $"EventArt/{spriteName}",
            $"UI/{spriteName}",
            $"Sprites/{spriteName}",
            spriteName
        };

        foreach (string path in possiblePaths)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                LogScreen($"Loaded sprite '{spriteName}' from path '{path}'");
                return sprite;
            }
        }

        LogScreen($"Failed to load sprite '{spriteName}' from any path");
        return null;
    }

    /// <summary>
    /// Parse color from metadata string
    /// </summary>
    private Color ParseColorFromMetadata(string colorString)
    {
        switch (colorString.ToLower())
        {
            case "red":
                return Color.red;
            case "green":
                return Color.green;
            case "blue":
                return Color.blue;
            case "yellow":
                return Color.yellow;
            case "purple":
                return new Color(0.8f, 0.2f, 0.8f);
            case "orange":
                return new Color(1f, 0.5f, 0f);
            case "cyan":
                return Color.cyan;
            case "magenta":
                return Color.magenta;
            case "white":
                return Color.white;
            case "black":
                return Color.black;
            case "gray":
            case "grey":
                return Color.gray;
            default:
                // Try to parse as hex color
                if (colorString.StartsWith("#") && colorString.Length == 7)
                {
                    if (ColorUtility.TryParseHtmlString(colorString, out Color color))
                    {
                        return color;
                    }
                }
                return Color.white; // Default fallback
        }
    }

    /// <summary>
    /// Check if current screen is complete
    /// </summary>
    public bool IsScreenComplete(EventScreen screen)
    {
        return isScreenComplete;
    }

    /// <summary>
    /// Hide current screen
    /// </summary>
    public void HideCurrentScreen()
    {
        if (currentScreen != null)
        {
            // Clean up progressive reveal logic if it exists
            if (progressiveRevealLogic != null)
            {
                progressiveRevealLogic.OnRevealComplete -= OnVerseRevealComplete;
                progressiveRevealLogic.OnContinuePressed -= OnVerseContinuePressed;
                progressiveRevealLogic = null;
            }
            
            // Kill all DOTween tweens before destroying the screen to prevent errors
            DOTween.Kill(currentScreen);
            DOTween.Kill(currentScreen.transform);
            
            // Kill tweens on all child components that might have tweens
            CanvasGroup[] canvasGroups = currentScreen.GetComponentsInChildren<CanvasGroup>();
            foreach (var cg in canvasGroups)
            {
                if (cg != null) DOTween.Kill(cg);
            }
            
            // Also kill any tweens on child transforms
            Transform[] children = currentScreen.GetComponentsInChildren<Transform>();
            foreach (var child in children)
            {
                if (child != null) DOTween.Kill(child);
            }
            
            Destroy(currentScreen);
            currentScreen = null;
        }
        
        isScreenComplete = false;
        currentEventScreen = null;
    }

    /// <summary>
    /// Build a human-readable, multi-line preview of consequences with new totals
    /// </summary>
    private string BuildConsequencesPreviewText(List<EventConsequence> consequences)
    {
        if (consequences == null || consequences.Count == 0)
        {
            return "";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (EventConsequence c in consequences)
        {
            switch (c.type)
            {
                case EventConsequence.ConsequenceType.ResourceChange:
                {
                    int current = EventSystemLogic.Instance?.GetGameUnitsLogic()?.GetResourceAmount(c.targetName) ?? 0;
                    int next = current + c.value;
                    string verb = c.value >= 0 ? "Gained" : "Lost";
                    sb.AppendLine($"- You've {verb} {Mathf.Abs(c.value)} x {c.targetName} (New Total {Mathf.Max(next,0)})");
                    break;
                }
                case EventConsequence.ConsequenceType.ScoreChange:
                {
                    int current = EventSystemLogic.Instance?.GetEventScore(c.targetName) ?? 0;
                    int next = current + c.value;
                    string verb = c.value >= 0 ? "Gained" : "Lost";
                    string display = FormatScoreDisplayName(c.targetName);
                    sb.AppendLine($"- You've {verb} {Mathf.Abs(c.value)} x {display} (New Total {next})");
                    break;
                }
                case EventConsequence.ConsequenceType.StatChange:
                {
                    StatManager sm = EventSystemLogic.Instance?.GetStatManager();
                    int current = sm != null ? sm.GetStatValue(c.targetName) : 0;
                    int next = current + c.value;
                    string verb = c.value >= 0 ? "Gained" : "Lost";
                    // Capitalize the first letter of the stat name for better readability
                    string capitalizedStatName = !string.IsNullOrEmpty(c.targetName) ? 
                        char.ToUpper(c.targetName[0]) + c.targetName.Substring(1).ToLower() : 
                        c.targetName;
                    sb.AppendLine($"- You've {verb} {Mathf.Abs(c.value)} {capitalizedStatName} (New Total {next})");
                    break;
                }
                case EventConsequence.ConsequenceType.ProductionUnitChange:
                {
                    GameUnitsLogic gul = EventSystemLogic.Instance?.GetGameUnitsLogic();
                    int current = 0;
                    if (gul != null && gul.productionTab != null)
                    {
                        GameObject slotObj = gul.productionTab.slots.Find(s => s.name == c.targetName);
                        if (slotObj != null)
                        {
                            GameProductionSlot ps = slotObj.GetComponent<GameProductionSlot>();
                            if (ps != null)
                            {
                                current = Mathf.RoundToInt(ps.maxAmount);
                            }
                        }
                    }
                    int next = current + c.value;
                    string verb = c.value >= 0 ? "Gained" : "Lost";
                    sb.AppendLine($"- You've {verb} {Mathf.Abs(c.value)} x {c.targetName} (New Total {Mathf.Max(next,0)})");
                    break;
                }
                case EventConsequence.ConsequenceType.TechnologyEnlightened:
                {
                    sb.AppendLine($"- You've Gained Enlightenment on {c.targetName}");
                    break;
                }
                case EventConsequence.ConsequenceType.UnlockEvent:
                {
                    sb.AppendLine($"- You've Unlocked Event {c.targetName}");
                    break;
                }
                case EventConsequence.ConsequenceType.PopulationChange:
                {
                    if (PopGrowthLogic.Instance != null)
                    {
                        int current = PopGrowthLogic.Instance.population;
                        int next = current + c.value;
                        
                        // Population can only be lost through events (deaths)
                        sb.AppendLine($"- {Mathf.Abs(c.value)} Population Have Been Killed (New Total {Mathf.Max(next, 0)})");
                    }
                    else
                    {
                        sb.AppendLine($"- {Mathf.Abs(c.value)} Population Have Been Killed");
                    }
                    break;
                }
                case EventConsequence.ConsequenceType.HousingChange:
                {
                    if (PopGrowthLogic.Instance != null)
                    {
                        int current = PopGrowthLogic.Instance.housing;
                        int next = current + c.value;
                        
                        if (c.value > 0)
                        {
                            // New housing was built
                            sb.AppendLine($"- {c.value} New Housing Units Have Been Gained (New Total {Mathf.Max(next, 0)})");
                        }
                        else
                        {
                            // Housing was destroyed or lost
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Housing Units Have Been Lost (New Total {Mathf.Max(next, 0)})");
                        }
                    }
                    else
                    {
                        if (c.value > 0)
                        {
                            sb.AppendLine($"- {c.value} New Housing Units Have Been Gained");
                        }
                        else
                        {
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Housing Units Have Been Lost");
                        }
                    }
                    break;
                }
                case EventConsequence.ConsequenceType.VagrantsChange:
                {
                    if (PopGrowthLogic.Instance != null)
                    {
                        int current = PopGrowthLogic.Instance.vagrants;
                        int next = current + c.value;
                        
                        if (c.value > 0)
                        {
                            // New vagrants arrived
                            sb.AppendLine($"- {c.value} New Vagrants Have Arrived (New Total {Mathf.Max(next, 0)})");
                        }
                        else
                        {
                            // Vagrants were killed
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Vagrants Have Been Killed (New Total {Mathf.Max(next, 0)})");
                        }
                    }
                    else
                    {
                        if (c.value > 0)
                        {
                            sb.AppendLine($"- {c.value} New Vagrants Have Arrived");
                        }
                        else
                        {
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Vagrants Have Been Killed");
                        }
                    }
                    break;
                }
                case EventConsequence.ConsequenceType.DeathsChange:
                {
                    if (PopGrowthLogic.Instance != null)
                    {
                        int current = PopGrowthLogic.Instance.deaths;
                        int next = current + c.value;
                        
                        if (c.value > 0)
                        {
                            // Additional deaths occurred
                            sb.AppendLine($"- {c.value} Additional Deaths Have Been Recorded (New Total {Mathf.Max(next, 0)})");
                        }
                        else
                        {
                            // Deaths were reduced (unusual but handle gracefully)
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Deaths Have Been Wiped From The Historical Records (New Total {Mathf.Max(next, 0)})");
                        }
                    }
                    else
                    {
                        if (c.value > 0)
                        {
                            sb.AppendLine($"- {c.value} Additional Deaths Have Been Recorded");
                        }
                        else
                        {
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Deaths Have Been Wiped From The Historical Records");
                        }
                    }
                    break;
                }
                case EventConsequence.ConsequenceType.DeathRecordsRevision:
                {
                    if (PopGrowthLogic.Instance != null)
                    {
                        int current = PopGrowthLogic.Instance.deaths;
                        int next = current + c.value;
                        
                        if (c.value < 0)
                        {
                            // Death records were wiped (evil empire revisionism)
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Deaths Have Been Wiped From The Public Records (New Total {Mathf.Max(next, 0)})");
                        }
                        else
                        {
                            // Additional deaths added to public records
                            sb.AppendLine($"- {c.value} Additional Deaths Have Been Added To The Public Records (New Total {Mathf.Max(next, 0)})");
                        }
                    }
                    else
                    {
                        if (c.value < 0)
                        {
                            sb.AppendLine($"- {Mathf.Abs(c.value)} Deaths Have Been Wiped From The Public Records");
                        }
                        else
                        {
                            sb.AppendLine($"- {c.value} Additional Deaths Have Been Added To The Public Records");
                        }
                    }
                    break;
                }
                case EventConsequence.ConsequenceType.ProductionPercentChange:
                {
                    string sign = c.value >= 0 ? "+" : "-";
                    string dur = c.durationSevenths > 0 ? $" (For {c.durationSevenths} Sevenths)" : string.Empty;
                    sb.AppendLine($"- Production {sign}{Mathf.Abs(c.value)}% For {c.targetName}{dur}");
                    break;
                }
                case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
                {
                    string sign = c.value >= 0 ? "+" : "-";
                    string dur = c.durationSevenths > 0 ? $" (For {c.durationSevenths} Sevenths)" : string.Empty;
                    sb.AppendLine($"- Production {sign}{Mathf.Abs(c.value)}% For Section {c.targetName}{dur}");
                    break;
                }
                case EventConsequence.ConsequenceType.ClickPowerChange:
                {
                    string sign = c.value >= 0 ? "+" : "-";
                    string dur = c.durationSevenths > 0 ? $" (For {c.durationSevenths} Sevenths)" : string.Empty;
                    sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)} For {c.targetName}{dur}");
                    break;
                }
                case EventConsequence.ConsequenceType.ClickPowerPercentChange:
                {
                    string sign = c.value >= 0 ? "+" : "-";
                    string dur = c.durationSevenths > 0 ? $" (For {c.durationSevenths} Sevenths)" : string.Empty;
                    sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)}% For {c.targetName}{dur}");
                    break;
                }
                case EventConsequence.ConsequenceType.ClickPowerChangeSection:
                {
                    string sign = c.value >= 0 ? "+" : "-";
                    string dur = c.durationSevenths > 0 ? $" (For {c.durationSevenths} Sevenths)" : string.Empty;
                    sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)} For Section {c.targetName}{dur}");
                    break;
                }
                case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                {
                    string sign = c.value >= 0 ? "+" : "-";
                    string dur = c.durationSevenths > 0 ? $" (For {c.durationSevenths} Sevenths)" : string.Empty;
                    sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)}% For Section {c.targetName}{dur}");
                    break;
                }
                default:
                {
                    sb.AppendLine($"- Unknown Consequence Type: {c.type} For {c.targetName} (Value: {c.value})");
                    break;
                }
            }
        }

        return sb.ToString();
    }

    

    // Centralized formatter for Event Score display names
    private string FormatScoreDisplayName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        string spaced = raw.Replace('_', ' ');
        string lower = spaced.ToLower();
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lower);
    }

    /// <summary>
    /// Log screen events
    /// </summary>
    private void LogScreen(string message)
    {
        Debug.Log($"[EventScreenManager] {message}");
    }

    /// <summary>
    /// Get the fade alpha value based on the screen type.
    /// </summary>
    // Removed redundant opacity system - all screens now use full opacity (1.0)

    // Simplified animation system - removed redundant setters
    

    
    // Simplified fade checking - removed redundant methods
    
    /// <summary>
    /// Immediately fade out all screens without animation (useful for instant transitions)
    /// </summary>
    public void FadeOutAllScreensImmediate()
    {
        if (screenContainer == null) return;
        
        // Kill all screen fade tweens
        KillAllScreenTweens();
        
        // Immediately destroy all child screens (but preserve vignette)
        foreach (Transform child in screenContainer)
        {
            if (child != null)
            {
                // Skip the vignette component - it should persist
                if (child.GetComponent<Image>() == vignette)
                    continue;
                    
                Destroy(child.gameObject);
            }
        }
        
        currentScreen = null;
    }
    
    /// <summary>
    /// Immediately fade out current screen without animation (useful for instant transitions)
    /// </summary>
    public void FadeOutCurrentScreenImmediate()
    {
        if (currentScreen == null) return;
        
        // Kill any existing fade tween for this screen
        if (screenFadeTweens.ContainsKey(currentScreen))
        {
            screenFadeTweens[currentScreen].Kill();
            screenFadeTweens.Remove(currentScreen);
        }
        
        // Immediately destroy the current screen
        Destroy(currentScreen);
        currentScreen = null;
    }

    /// <summary>
    /// Build the verse tooltip consequences text for a given knot's first choice.
    /// Parses the choice's &C consequences and formats them into a human-readable preview.
    /// Returns an empty string if none are found.
    /// </summary>
    private string BuildVerseTooltipConsequencesText(string knotName)
    {
        if (string.IsNullOrEmpty(knotName)) return string.Empty;
        string firstChoiceText = GetPrecompiledFirstChoiceText(knotName);
        var verseConsequences = ParseVerseChoiceConsequences(firstChoiceText);
        return BuildConsequencesPreviewText(verseConsequences);
    }
}