using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Progressive sentence reveal system for dark fantasy RPG narrative text
/// Implements Sunless Skies style progressive fade-in with configurable timing and opacity levels
/// Works directly with existing VerseScreen prefab structure
/// 
/// NEW BEHAVIOR: User input (any key, mouse click, or touch) advances to the next sentence,
/// allowing players to read at their own pace instead of auto-play.
/// 
/// SKIP FUNCTIONALITY: If 3+ consecutive inputs are detected within a short time window,
/// the system will skip to the end and show all text with only the last sentence active.
/// 
/// DOTWEEN INTEGRATION: Uses DOTween for smooth fade animations instead of manual coroutines.
/// </summary>
public class ProgressiveSentenceRevealLogic : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] public float sentenceFadeInDuration = 1.0f;
    [SerializeField] public float sentenceFadeOutDuration = 0.8f; // Duration for fading out dimmed text
    [SerializeField] public float pastSentenceOpacity = 0.65f;
    [SerializeField] public float activeSentenceOpacity = 1.0f;
    [SerializeField] public int sentencesPerGroup = 2; // Display 1 sentence at a time by default
    [SerializeField] public Ease fadeInEase = Ease.InOutSine;
    [SerializeField] public Ease fadeOutEase = Ease.InOutSine;
    
    [Header("Skip Settings")]
    [SerializeField] private float skipDetectionWindow = 0.8f; // Time window to detect consecutive inputs
    [SerializeField] private int skipTriggerCount = 3; // Number of consecutive inputs to trigger skip
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private Button continueButton;
    
    // Private state
    private string[] sentences;
    private int currentSentenceIndex = 0;
    private bool isRevealComplete = false;
    private Coroutine revealCoroutine;
    private string originalText;

    // DOTween fade tracking system
    private Dictionary<int, Tween> fadeTweens = new Dictionary<int, Tween>();
    private Dictionary<int, float> currentSentenceOpacities = new Dictionary<int, float>();

    // Skip detection system
    private Queue<float> inputTimes = new Queue<float>();
    private bool skipTriggered = false;
    
    // Events
    public System.Action OnRevealComplete;
    public System.Action OnContinuePressed;
    
    private void Awake()
    {
        // Auto-find content text component if not assigned
        if (contentText == null)
        {
            Transform contentTransform = transform.Find("Content");
            if (contentTransform != null)
            {
                contentText = contentTransform.GetComponent<TextMeshProUGUI>();
            }
        }
        
        // Auto-find continue button if not assigned
        if (continueButton == null)
        {
            continueButton = transform.Find("Button")?.GetComponent<Button>();
        }
        
        // Set up continue button
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonPressed);
            // Button starts as SetActive(false) in prefab, we'll enable it when reveal is complete
        }
    }
    
    private void Update()
    {
        // Check for skip input detection only when actively revealing
        if (revealCoroutine != null && !skipTriggered && !isRevealComplete)
        {
            CheckForSkipInput();
        }
    }
    
    private void OnDestroy()
    {
        // Kill all active tweens to prevent memory leaks
        foreach (var tween in fadeTweens.Values)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
        fadeTweens.Clear();
    }
    
    /// <summary>
    /// Check for rapid input that should trigger skip
    /// </summary>
    private void CheckForSkipInput()
    {
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            float currentTime = Time.time;
            
            // Record this input time
            inputTimes.Enqueue(currentTime);
            
            // Remove old input times outside the detection window
            while (inputTimes.Count > 0 && currentTime - inputTimes.Peek() > skipDetectionWindow)
            {
                inputTimes.Dequeue();
            }
            
            // Check if we have enough consecutive inputs to trigger skip
            if (inputTimes.Count >= skipTriggerCount && !skipTriggered)
            {
                EventSystemLogic.Instance.LogEvent($"Skip triggered! {inputTimes.Count} inputs detected within {skipDetectionWindow}s window.", "ChorusScreenManager");
                skipTriggered = true;
                SkipToEnd();
            }
        }
    }
    
    /// <summary>
    /// Start the progressive sentence reveal with the given text
    /// </summary>
    /// <param name="fullText">Complete paragraph text to reveal progressively</param>
    public void StartReveal(string fullText)
    {
        if (string.IsNullOrEmpty(fullText)) return;
        
        // Stop any existing reveal
        StopReveal();
        
        // Parse the text into sentences
        sentences = ParseSentences(fullText);
        if (sentences == null || sentences.Length == 0) return;
        
        // Initialize opacity tracking for all sentences
        InitializeOpacityTracking();
        
        // Reset state
        currentSentenceIndex = 0;
        isRevealComplete = false;
        skipTriggered = false;
        
        // Reset skip detection
        ResetSkipDetection();
        
        // Set initial text state
        SetTextWithOpacityTags();
        
        // Start the reveal coroutine
        revealCoroutine = StartCoroutine(ProgressiveRevealCoroutine());
    }
    
    /// <summary>
    /// Stop the current reveal and reset to initial state
    /// </summary>
    public void StopReveal()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
        
        // Kill all active tweens
        KillAllFadeTweens();
        
        // Reset state
        currentSentenceIndex = 0;
        isRevealComplete = false;
        skipTriggered = false;
        
        // Hide continue button
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Kill all active fade tweens
    /// </summary>
    private void KillAllFadeTweens()
    {
        foreach (var tween in fadeTweens.Values)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
        fadeTweens.Clear();
    }
    
    /// <summary>
    /// Reset skip detection system
    /// </summary>
    private void ResetSkipDetection()
    {
        inputTimes.Clear();
        skipTriggered = false;
    }
    
    /// <summary>
    /// Parse text into sentences based on punctuation
    /// </summary>
    private string[] ParseSentences(string text)
    {
        if (string.IsNullOrEmpty(text)) return new string[0];
        
        // Split by sentence-ending punctuation and clean up
        string[] rawSentences = text.Split(new char[] { '.', '!', '?' }, System.StringSplitOptions.RemoveEmptyEntries);
        List<string> cleanSentences = new List<string>();
        
        foreach (string sentence in rawSentences)
        {
            string cleanSentence = sentence.Trim();
            if (!string.IsNullOrEmpty(cleanSentence))
            {
                // Add back the punctuation that was removed
                if (text.Contains(cleanSentence + "."))
                    cleanSentence += ".";
                else if (text.Contains(cleanSentence + "!"))
                    cleanSentence += "!";
                else if (text.Contains(cleanSentence + "?"))
                    cleanSentence += "?";
                
                cleanSentences.Add(cleanSentence);
            }
        }
        
        return cleanSentences.ToArray();
    }
    
    /// <summary>
    /// Set text with opacity tags for all sentences
    /// </summary>
    private void SetTextWithOpacityTags()
    {
        if (contentText == null || sentences == null) return;
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < sentences.Length; i++)
        {
            float opacity = 0f; // All sentences start invisible for smooth fade-in
            
            // Use the original text color from the TextMeshPro component
            Color textColor = contentText.color;
            string colorTag = ColorUtility.ToHtmlStringRGBA(new Color(textColor.r, textColor.g, textColor.b, opacity));
            
            sb.Append($"<color=#{colorTag}>{sentences[i]}</color>");
            
            if (i < sentences.Length - 1)
            {
                sb.Append(" "); // Add space between sentences
            }
        }
        
        contentText.text = sb.ToString();
    }

    /// <summary>
    /// Fade in a single sentence using DOTween
    /// </summary>
    private IEnumerator FadeInSentence(int sentenceIndex)
    {
        if (sentenceIndex < 0 || sentenceIndex >= sentences.Length) yield break;
        
        // Kill any existing tween for this sentence
        if (fadeTweens.ContainsKey(sentenceIndex))
        {
            fadeTweens[sentenceIndex].Kill();
        }
        
        // If this isn't the first sentence, fade out the previous active sentence
        if (sentenceIndex > 0)
        {
            yield return StartCoroutine(FadeOutSentence(sentenceIndex - 1));
        }
        
        // Create DOTween for smooth fade-in
        float startOpacity = 0f;
        float targetOpacity = activeSentenceOpacity;
        
        // Use DOTween to animate the opacity
        Tween fadeTween = DOTween.To(
            () => startOpacity,
            (value) => {
                if (!skipTriggered)
                {
                    UpdateTextWithCurrentState(sentenceIndex, value);
                }
            },
            targetOpacity,
            sentenceFadeInDuration
        )
        .SetEase(fadeInEase)
        .OnComplete(() => {
            if (!skipTriggered)
            {
                // Ensure final opacity is set and previous sentence is dimmed
                UpdateTextWithCurrentState(sentenceIndex, targetOpacity);
            }
        });
        
        // Store the tween for potential cancellation
        fadeTweens[sentenceIndex] = fadeTween;
        
        // Wait for the tween to complete or be interrupted
        yield return fadeTween.WaitForCompletion();
        
        // Clean up the tween reference
        if (fadeTweens.ContainsKey(sentenceIndex))
        {
            fadeTweens.Remove(sentenceIndex);
        }
    }
    
    /// <summary>
    /// Fade out a sentence to dimmed state using DOTween
    /// </summary>
    private IEnumerator FadeOutSentence(int sentenceIndex)
    {
        if (sentenceIndex < 0 || sentenceIndex >= sentences.Length) yield break;
        
        // Kill any existing tween for this sentence
        if (fadeTweens.ContainsKey(sentenceIndex))
        {
            fadeTweens[sentenceIndex].Kill();
        }
        
        // Get current opacity and animate to dimmed state
        float startOpacity = currentSentenceOpacities.ContainsKey(sentenceIndex) ? 
            currentSentenceOpacities[sentenceIndex] : activeSentenceOpacity;
        float targetOpacity = pastSentenceOpacity;
        
        // Use DOTween to animate the opacity to dimmed state
        Tween fadeOutTween = DOTween.To(
            () => startOpacity,
            (value) => {
                if (!skipTriggered)
                {
                    // Update just this sentence's opacity without affecting others
                    UpdateSingleSentenceOpacity(sentenceIndex, value);
                }
            },
            targetOpacity,
            sentenceFadeOutDuration
        )
        .SetEase(fadeOutEase)
        .OnComplete(() => {
            if (!skipTriggered)
            {
                // Ensure final dimmed opacity is set
                UpdateSingleSentenceOpacity(sentenceIndex, targetOpacity);
            }
        });
        
        // Store the tween for potential cancellation
        fadeTweens[sentenceIndex] = fadeOutTween;
        
        // Wait for the tween to complete or be interrupted
        yield return fadeOutTween.WaitForCompletion();
        
        // Clean up the tween reference
        if (fadeTweens.ContainsKey(sentenceIndex))
        {
            fadeTweens.Remove(sentenceIndex);
        }
    }
    
    /// <summary>
    /// Update the opacity of a single sentence without affecting the entire text
    /// This is used for smooth fade-out transitions
    /// </summary>
    private void UpdateSingleSentenceOpacity(int sentenceIndex, float opacity)
    {
        if (contentText == null || sentences == null || sentenceIndex < 0 || sentenceIndex >= sentences.Length) return;
        
        // Update the stored opacity
        currentSentenceOpacities[sentenceIndex] = opacity;
        
        // Rebuild the entire text with updated opacity for this sentence
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < sentences.Length; i++)
        {
            float currentOpacity;
            
            if (i == sentenceIndex)
            {
                // Use the new opacity for this sentence
                currentOpacity = opacity;
            }
            else
            {
                // Use the stored opacity for other sentences
                currentOpacity = currentSentenceOpacities.ContainsKey(i) ? currentSentenceOpacities[i] : 0f;
            }
            
            // Use the original text color from the TextMeshPro component
            Color textColor = contentText.color;
            string colorTag = ColorUtility.ToHtmlStringRGBA(new Color(textColor.r, textColor.g, textColor.b, currentOpacity));
            sb.Append($"<color=#{colorTag}>{sentences[i]}</color>");
            
            if (i < sentences.Length - 1)
            {
                sb.Append(" ");
            }
        }
        
        contentText.text = sb.ToString();
    }

    /// <summary>
    /// Update the entire text with proper opacity states for all sentences
    /// </summary>
    private void UpdateTextWithCurrentState(int activeSentenceIndex, float activeOpacity)
    {
        if (contentText == null || sentences == null) return;
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < sentences.Length; i++)
        {
            float opacity;
            
            if (sentencesPerGroup == 1)
            {
                // Single sentence mode (current behavior)
                if (i < activeSentenceIndex)
                {
                    opacity = pastSentenceOpacity; // Past sentences are dimmed
                }
                else if (i == activeSentenceIndex)
                {
                    opacity = activeOpacity; // Current sentence gets the specified opacity
                }
                else
                {
                    opacity = 0f; // Future sentences remain invisible
                }
            }
            else
            {
                // Group mode
                int currentGroup = activeSentenceIndex / sentencesPerGroup;
                int sentenceGroup = i / sentencesPerGroup;
                
                if (sentenceGroup < currentGroup)
                {
                    opacity = pastSentenceOpacity; // Past groups are dimmed
                }
                else if (sentenceGroup == currentGroup)
                {
                    opacity = activeOpacity; // Current group gets the specified opacity
                }
                else
                {
                    opacity = 0f; // Future groups remain invisible
                }
            }
            
            // Update the stored opacity for this sentence
            currentSentenceOpacities[i] = opacity;
            
            // Use the original text color from the TextMeshPro component
            Color textColor = contentText.color;
            string colorTag = ColorUtility.ToHtmlStringRGBA(new Color(textColor.r, textColor.g, textColor.b, opacity));
            sb.Append($"<color=#{colorTag}>{sentences[i]}</color>");
            
            if (i < sentences.Length - 1)
            {
                sb.Append(" ");
            }
        }
        
        contentText.text = sb.ToString();
    }

    /// <summary>
    /// Main coroutine for progressive reveal
    /// </summary>
    private IEnumerator ProgressiveRevealCoroutine()
    {
        if (sentences == null || sentences.Length == 0) yield break;
        
        if (sentencesPerGroup == 1)
        {
            // Single sentence mode (current behavior)
            yield return StartCoroutine(ProgressiveRevealSingleSentence());
        }
        else
        {
            // Group mode
            yield return StartCoroutine(ProgressiveRevealGroups());
        }
    }
    
    /// <summary>
    /// Progressive reveal for single sentence mode
    /// </summary>
    private IEnumerator ProgressiveRevealSingleSentence()
    {
        // Start with first sentence visible
        currentSentenceIndex = 0;
        
        // Process each sentence one by one
        for (int i = 0; i < sentences.Length; i++)
        {
            // Check if skip was triggered
            if (skipTriggered) yield break;
            
            currentSentenceIndex = i;
            
            // Fade in current sentence (including the first one)
            yield return StartCoroutine(FadeInSentence(i));
            
            // Check if skip was triggered during fade
            if (skipTriggered) yield break;
            
            // Wait for user input before next sentence (except for the last sentence)
            if (i < sentences.Length - 1)
            {
                yield return StartCoroutine(WaitForAnyInput());
                // Check again after waiting for input
                if (skipTriggered) yield break;
            }
        }
        
        // All sentences revealed (only if skip wasn't triggered)
        if (!skipTriggered)
        {
            isRevealComplete = true;
            OnRevealComplete?.Invoke();
            
            // Show continue button
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
            }
        }
    }
    
    /// <summary>
    /// Progressive reveal for group mode
    /// </summary>
    private IEnumerator ProgressiveRevealGroups()
    {
        int totalGroups = Mathf.CeilToInt((float)sentences.Length / sentencesPerGroup);
        
        // Start with first group visible
        currentSentenceIndex = 0;
        
        // Process each group
        for (int groupIndex = 0; groupIndex < totalGroups; groupIndex++)
        {
            // Check if skip was triggered
            if (skipTriggered) yield break;
            
            int startSentenceIndex = groupIndex * sentencesPerGroup;
            int endSentenceIndex = Mathf.Min(startSentenceIndex + sentencesPerGroup - 1, sentences.Length - 1);
            
            currentSentenceIndex = endSentenceIndex; // Set to last sentence in group
            
            // Fade in current group (including the first one)
            yield return StartCoroutine(FadeInSentenceGroup(startSentenceIndex, endSentenceIndex));
            
            // Check if skip was triggered during fade
            if (skipTriggered) yield break;
            
            // Wait for user input before next group (except for the last group)
            if (groupIndex < totalGroups - 1)
            {
                yield return StartCoroutine(WaitForAnyInput());
                // Check again after waiting for input
                if (skipTriggered) yield break;
            }
        }
        
        // All groups revealed (only if skip wasn't triggered)
        if (!skipTriggered)
        {
            isRevealComplete = true;
            OnRevealComplete?.Invoke();
            
            // Show continue button
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
            }
        }
    }
    
    /// <summary>
    /// Fade in a group of sentences using DOTween
    /// </summary>
    private IEnumerator FadeInSentenceGroup(int startIndex, int endIndex)
    {
        // Kill any existing tweens for this group
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (fadeTweens.ContainsKey(i))
            {
                fadeTweens[i].Kill();
            }
        }
        
        // If this isn't the first group, fade out the previous active group
        if (startIndex > 0)
        {
            int previousGroupStart = Mathf.Max(0, startIndex - sentencesPerGroup);
            int previousGroupEnd = startIndex - 1;
            yield return StartCoroutine(FadeOutSentenceGroup(previousGroupStart, previousGroupEnd));
        }
        
        float startOpacity = 0f;
        float targetOpacity = activeSentenceOpacity;
        
        // Create DOTween for smooth fade-in of the entire group
        Tween groupFadeTween = DOTween.To(
            () => startOpacity,
            (value) => {
                if (!skipTriggered)
                {
                    UpdateTextWithCurrentState(endIndex, value);
                }
            },
            targetOpacity,
            sentenceFadeInDuration
        )
        .SetEase(fadeInEase)
        .OnComplete(() => {
            if (!skipTriggered)
            {
                // Ensure final opacity is set
                UpdateTextWithCurrentState(endIndex, targetOpacity);
            }
        });
        
        // Store the tween for potential cancellation
        fadeTweens[endIndex] = groupFadeTween;
        
        // Wait for the tween to complete or be interrupted
        yield return groupFadeTween.WaitForCompletion();
        
        // Clean up the tween reference
        if (fadeTweens.ContainsKey(endIndex))
        {
            fadeTweens.Remove(endIndex);
        }
    }
    
    /// <summary>
    /// Fade out a group of sentences to dimmed state using DOTween
    /// </summary>
    private IEnumerator FadeOutSentenceGroup(int startIndex, int endIndex)
    {
        if (startIndex < 0 || endIndex >= sentences.Length || startIndex > endIndex) yield break;
        
        // Kill any existing tweens for this group
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (fadeTweens.ContainsKey(i))
            {
                fadeTweens[i].Kill();
            }
        }
        
        // Get current opacity and animate to dimmed state
        float startOpacity = activeSentenceOpacity;
        float targetOpacity = pastSentenceOpacity;
        
        // Use DOTween to animate the opacity to dimmed state for the entire group
        Tween groupFadeOutTween = DOTween.To(
            () => startOpacity,
            (value) => {
                if (!skipTriggered)
                {
                    // Update the entire group's opacity to dimmed state
                    UpdateGroupOpacity(startIndex, endIndex, value);
                }
            },
            targetOpacity,
            sentenceFadeOutDuration
        )
        .SetEase(fadeOutEase)
        .OnComplete(() => {
            if (!skipTriggered)
            {
                // Ensure final dimmed opacity is set for the entire group
                UpdateGroupOpacity(startIndex, endIndex, targetOpacity);
            }
        });
        
        // Store the tween for potential cancellation (use startIndex as key)
        fadeTweens[startIndex] = groupFadeOutTween;
        
        // Wait for the tween to complete or be interrupted
        yield return groupFadeOutTween.WaitForCompletion();
        
        // Clean up the tween reference
        if (fadeTweens.ContainsKey(startIndex))
        {
            fadeTweens.Remove(startIndex);
        }
    }
    
    /// <summary>
    /// Update the opacity of a group of sentences to dimmed state
    /// This is used for smooth fade-out transitions of groups
    /// </summary>
    private void UpdateGroupOpacity(int startIndex, int endIndex, float opacity)
    {
        if (contentText == null || sentences == null || startIndex < 0 || endIndex >= sentences.Length) return;
        
        // Update the stored opacity for all sentences in the group
        for (int i = startIndex; i <= endIndex; i++)
        {
            currentSentenceOpacities[i] = opacity;
        }
        
        // Rebuild the entire text with updated opacity for this group
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < sentences.Length; i++)
        {
            float currentOpacity;
            
            if (i >= startIndex && i <= endIndex)
            {
                // Use the new opacity for sentences in this group
                currentOpacity = opacity;
            }
            else
            {
                // Use the stored opacity for other sentences
                currentOpacity = currentSentenceOpacities.ContainsKey(i) ? currentSentenceOpacities[i] : 0f;
            }
            
            // Use the original text color from the TextMeshPro component
            Color textColor = contentText.color;
            string colorTag = ColorUtility.ToHtmlStringRGBA(new Color(textColor.r, textColor.g, textColor.b, currentOpacity));
            sb.Append($"<color=#{colorTag}>{sentences[i]}</color>");
            
            if (i < sentences.Length - 1)
            {
                sb.Append(" ");
            }
        }
        
        contentText.text = sb.ToString();
    }
    
    /// <summary>
    /// Wait for any user input (keyboard, mouse, or touch) - simplified since skip is handled in Update
    /// </summary>
    private IEnumerator WaitForAnyInput()
    {
        float inputCooldown = 0.1f; // Prevent rapid inputs
        
        while (true)
        {
            // Check if skip was triggered from Update
            if (skipTriggered) yield break;
            
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                // Wait for cooldown to prevent accidental rapid inputs
                yield return new WaitForSeconds(inputCooldown);
                yield break;
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Handle continue button press
    /// </summary>
    private void OnContinueButtonPressed()
    {
        if (isRevealComplete)
        {
            OnContinuePressed?.Invoke();
        }
    }
    
    /// <summary>
    /// Check if reveal is complete
    /// </summary>
    public bool IsRevealComplete()
    {
        return isRevealComplete;
    }
    
    /// <summary>
    /// Get current sentence index
    /// </summary>
    public int GetCurrentSentenceIndex()
    {
        return currentSentenceIndex;
    }
    
    /// <summary>
    /// Get total sentence count
    /// </summary>
    public int GetTotalSentenceCount()
    {
        return sentences?.Length ?? 0;
    }
    
    /// <summary>
    /// Manually advance to the next sentence (useful for testing or external control)
    /// </summary>
    public void AdvanceToNextSentence()
    {
        if (isRevealComplete || skipTriggered) return;
        
        if (sentencesPerGroup == 1)
        {
            // Single sentence mode
            if (currentSentenceIndex >= sentences.Length - 1) return;
            
            // Fade out current sentence to dimmed state
            StartCoroutine(FadeOutSentence(currentSentenceIndex));
            
            currentSentenceIndex++;
        }
        else
        {
            // Group mode
            int currentGroup = currentSentenceIndex / sentencesPerGroup;
            int totalGroups = Mathf.CeilToInt((float)sentences.Length / sentencesPerGroup);
            
            if (currentGroup >= totalGroups - 1) return;
            
            // Fade out current group to dimmed state
            int currentGroupStart = currentGroup * sentencesPerGroup;
            int currentGroupEnd = Mathf.Min(currentGroupStart + sentencesPerGroup - 1, sentences.Length - 1);
            StartCoroutine(FadeOutSentenceGroup(currentGroupStart, currentGroupEnd));
            
            // Move to next group
            int nextGroup = currentGroup + 1;
            int startSentenceIndex = nextGroup * sentencesPerGroup;
            int endSentenceIndex = Mathf.Min(startSentenceIndex + sentencesPerGroup - 1, sentences.Length - 1);
            currentSentenceIndex = endSentenceIndex;
        }
        
        // Update the text to show the new state
        UpdateTextWithCurrentState(currentSentenceIndex, activeSentenceOpacity);
        
        // If this is the last sentence/group, mark as complete
        if (sentencesPerGroup == 1)
        {
            if (currentSentenceIndex >= sentences.Length - 1)
            {
                isRevealComplete = true;
                OnRevealComplete?.Invoke();
                
                // Show continue button
                if (continueButton != null)
                {
                    continueButton.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            int currentGroup = currentSentenceIndex / sentencesPerGroup;
            int totalGroups = Mathf.CeilToInt((float)sentences.Length / sentencesPerGroup);
            
            if (currentGroup >= totalGroups - 1)
            {
                isRevealComplete = true;
                OnRevealComplete?.Invoke();
                
                // Show continue button
                if (continueButton != null)
                {
                    continueButton.gameObject.SetActive(true);
                }
            }
        }
    }

    /// <summary>
    /// Skip to the end of the reveal - shows all text with only last sentence active
    /// </summary>
    [ContextMenu("Skip to End")]
    public void SkipToEnd()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
        
        // Kill all active tweens
        KillAllFadeTweens();
        
        // Mark as complete and set to last sentence
        currentSentenceIndex = sentences.Length - 1;
        isRevealComplete = true;
        skipTriggered = true;
        
        // Show all sentences: past ones dimmed, last one active
        ShowAllTextWithLastActive();
        
        // Show continue button
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
        }
        
        OnRevealComplete?.Invoke();
        
        EventSystemLogic.Instance.LogEvent("ProgressiveSentenceRevealLogic: Skipped to end - all text visible with last sentence active", "ChorusScreenManager");
    }
    
    /// <summary>
    /// Show all text with appropriate opacity - past sentences dimmed, last sentence active
    /// </summary>
    private void ShowAllTextWithLastActive()
    {
        if (contentText == null || sentences == null) return;
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // Calculate which sentences should be highlighted based on sentencesPerGroup
        int sentencesToHighlight = Mathf.Min(sentencesPerGroup, sentences.Length);
        int highlightStartIndex = sentences.Length - sentencesToHighlight;
        
        for (int i = 0; i < sentences.Length; i++)
        {
            float opacity;
            
            if (i < highlightStartIndex)
            {
                opacity = pastSentenceOpacity; // Past sentences are dimmed
            }
            else
            {
                opacity = activeSentenceOpacity; // Last sentencesPerGroup sentences are fully active
            }
            
            // Update the stored opacity for this sentence
            currentSentenceOpacities[i] = opacity;
            
            // Use the original text color from the TextMeshPro component
            Color textColor = contentText.color;
            string colorTag = ColorUtility.ToHtmlStringRGBA(new Color(textColor.r, textColor.g, textColor.b, opacity));
            sb.Append($"<color=#{colorTag}>{sentences[i]}</color>");
            
            if (i < sentences.Length - 1)
            {
                sb.Append(" ");
            }
        }
        
        contentText.text = sb.ToString();
    }
    
    /// <summary>
    /// Reset the reveal to initial state
    /// </summary>
    [ContextMenu("Reset Reveal")]
    public void ResetReveal()
    {
        StopReveal();
        
        // Reset text to original state
        if (contentText != null && !string.IsNullOrEmpty(originalText))
        {
            contentText.text = originalText;
        }
    }

    /// <summary>
    /// Change the number of sentences displayed per group
    /// </summary>
    /// <param name="newGroupSize">New group size (1 = single sentence, >1 = group mode)</param>
    public void SetSentencesPerGroup(int newGroupSize)
    {
        if (newGroupSize < 1)
        {
            Debug.LogWarning("ProgressiveSentenceRevealLogic: Group size must be at least 1");
            return;
        }
        
        sentencesPerGroup = newGroupSize;
        EventSystemLogic.Instance.LogEvent($"ProgressiveSentenceRevealLogic: Changed to {sentencesPerGroup} sentences per group", "ChorusScreenManager");
        
        // If currently revealing, restart with new settings
        if (revealCoroutine != null && !string.IsNullOrEmpty(originalText))
        {
            StartReveal(originalText);
        }
    }
    
    /// <summary>
    /// Get current sentences per group setting
    /// </summary>
    public int GetSentencesPerGroup()
    {
        return sentencesPerGroup;
    }
    
    /// <summary>
    /// Get current group information (useful for debugging and external control)
    /// </summary>
    public (int currentGroup, int totalGroups, int startSentence, int endSentence) GetCurrentGroupInfo()
    {
        if (sentences == null || sentences.Length == 0)
        {
            return (0, 0, 0, 0);
        }
        
        if (sentencesPerGroup == 1)
        {
            return (currentSentenceIndex, sentences.Length, currentSentenceIndex, currentSentenceIndex);
        }
        
        int currentGroup = currentSentenceIndex / sentencesPerGroup;
        int totalGroups = Mathf.CeilToInt((float)sentences.Length / sentencesPerGroup);
        int startSentence = currentGroup * sentencesPerGroup;
        int endSentence = Mathf.Min(startSentence + sentencesPerGroup - 1, sentences.Length - 1);
        
        return (currentGroup, totalGroups, startSentence, endSentence);
    }
    
    /// <summary>
    /// Check if skip was triggered during this reveal
    /// </summary>
    public bool WasSkipTriggered()
    {
        return skipTriggered;
    }
    
    /// <summary>
    /// Get current skip detection settings
    /// </summary>
    public (float window, int count) GetSkipSettings()
    {
        return (skipDetectionWindow, skipTriggerCount);
    }
    
    /// <summary>
    /// Set skip detection parameters
    /// </summary>
    public void SetSkipSettings(float window, int count)
    {
        skipDetectionWindow = Mathf.Max(0.1f, window);
        skipTriggerCount = Mathf.Max(1, count);
        EventSystemLogic.Instance.LogEvent($"ProgressiveSentenceRevealLogic: Skip settings updated - Window: {skipDetectionWindow}s, Count: {skipTriggerCount}", "ChorusScreenManager");
    }
    
    /// <summary>
    /// Initialize opacity tracking for all sentences
    /// </summary>
    private void InitializeOpacityTracking()
    {
        if (sentences == null || sentences.Length == 0) return;
        
        // Kill any existing fade tweens
        KillAllFadeTweens();
        
        // Clear opacity tracking
        currentSentenceOpacities.Clear();
        
        for (int i = 0; i < sentences.Length; i++)
        {
            currentSentenceOpacities[i] = 0f; // All sentences start invisible
        }
    }
    
    /// <summary>
    /// Get the current opacity of a specific sentence
    /// </summary>
    /// <param name="sentenceIndex">Index of the sentence</param>
    /// <returns>Current opacity (0 for invisible, 1 for fully visible)</returns>
    public float GetSentenceOpacity(int sentenceIndex)
    {
        if (currentSentenceOpacities.ContainsKey(sentenceIndex))
        {
            return currentSentenceOpacities[sentenceIndex];
        }
        return 0f; // Default to invisible if not found
    }
    
    /// <summary>
    /// Set the opacity of a specific sentence
    /// </summary>
    /// <param name="sentenceIndex">Index of the sentence</param>
    /// <param name="opacity">Opacity value (0 for invisible, 1 for fully visible)</returns>
    public void SetSentenceOpacity(int sentenceIndex, float opacity)
    {
        if (currentSentenceOpacities.ContainsKey(sentenceIndex))
        {
            currentSentenceOpacities[sentenceIndex] = Mathf.Clamp01(opacity);
        }
    }
    
    /// <summary>
    /// Set the ease type for fade-in animations
    /// </summary>
    /// <param name="ease">DOTween ease type for fade-in</param>
    public void SetFadeInEase(Ease ease)
    {
        fadeInEase = ease;
    }
    
    /// <summary>
    /// Set the ease type for fade-out animations
    /// </summary>
    /// <param name="ease">DOTween ease type for fade-out</param>
    public void SetFadeOutEase(Ease ease)
    {
        fadeOutEase = ease;
    }
    
    /// <summary>
    /// Get current fade ease settings
    /// </summary>
    /// <returns>Tuple with fade-in and fade-out ease types</returns>
    public (Ease fadeIn, Ease fadeOut) GetFadeEaseSettings()
    {
        return (fadeInEase, fadeOutEase);
    }
    
    /// <summary>
    /// Immediately fade out a sentence to dimmed state (useful for instant transitions)
    /// </summary>
    /// <param name="sentenceIndex">Index of the sentence to fade out</param>
    public void FadeOutSentenceImmediate(int sentenceIndex)
    {
        if (sentenceIndex < 0 || sentenceIndex >= sentences.Length) return;
        
        // Kill any existing tween for this sentence
        if (fadeTweens.ContainsKey(sentenceIndex))
        {
            fadeTweens[sentenceIndex].Kill();
            fadeTweens.Remove(sentenceIndex);
        }
        
        // Immediately set to dimmed state
        UpdateSingleSentenceOpacity(sentenceIndex, pastSentenceOpacity);
    }
    
    /// <summary>
    /// Immediately fade out a group of sentences to dimmed state (useful for instant transitions)
    /// </summary>
    /// <param name="startIndex">Start index of the group</param>
    /// <param name="endIndex">End index of the group</param>
    public void FadeOutSentenceGroupImmediate(int startIndex, int endIndex)
    {
        if (startIndex < 0 || endIndex >= sentences.Length || startIndex > endIndex) return;
        
        // Kill any existing tweens for this group
        for (int i = startIndex; i <= endIndex; i++)
        {
            if (fadeTweens.ContainsKey(i))
            {
                fadeTweens[i].Kill();
                fadeTweens.Remove(i);
            }
        }
        
        // Immediately set to dimmed state
        UpdateGroupOpacity(startIndex, endIndex, pastSentenceOpacity);
    }
    
    /// <summary>
    /// Check if any fade animations are currently running
    /// </summary>
    /// <returns>True if any fade tweens are active</returns>
    public bool IsFadeAnimationRunning()
    {
        foreach (var tween in fadeTweens.Values)
        {
            if (tween != null && tween.IsActive())
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Get the number of active fade animations
    /// </summary>
    /// <returns>Count of active fade tweens</returns>
    public int GetActiveFadeAnimationCount()
    {
        int count = 0;
        foreach (var tween in fadeTweens.Values)
        {
            if (tween != null && tween.IsActive())
            {
                count++;
            }
        }
        return count;
    }
} 