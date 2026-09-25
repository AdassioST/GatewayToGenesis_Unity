using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Reveals verse text a few sentences at a time (Sunless Skies style). Each input shows the next group:
/// the previous group dims to <see cref="pastSentenceOpacity"/>, then the new one fades in. Input during a
/// fade finishes it; <see cref="skipTriggerCount"/> quick inputs reveal everything. When all text is shown the
/// continue button appears and pressing it raises <see cref="OnContinuePressed"/> (once).
///
/// Text is split into sentences without losing anything: paragraph breaks, ellipses, closing quotes and rich
/// text survive, and a paragraph break always ends a group. Opacity uses TMP &lt;alpha&gt; tags, so the text keeps
/// its own colours.
/// </summary>
public class ProgressiveSentenceRevealLogic : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] public float sentenceFadeInDuration = 1.0f;
    [SerializeField] public float sentenceFadeOutDuration = 0.8f; // Duration for dimming the previous group
    [SerializeField] public float pastSentenceOpacity = 0.65f;
    [SerializeField] public float activeSentenceOpacity = 1.0f;
    [SerializeField] public int sentencesPerGroup = 2;
    [SerializeField] public Ease fadeInEase = Ease.InOutSine;
    [SerializeField] public Ease fadeOutEase = Ease.InOutSine;

    [Header("Skip Settings")]
    [SerializeField] private float skipDetectionWindow = 0.8f; // Seconds in which rapid inputs count toward a skip
    [SerializeField] private int skipTriggerCount = 3;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private Button continueButton;

    public System.Action OnRevealComplete;
    public System.Action OnContinuePressed;

    private readonly List<string> sentences = new List<string>();
    private readonly List<int> groupStarts = new List<int>();
    private float[] opacities = new float[0];
    private readonly StringBuilder builder = new StringBuilder();
    private readonly Queue<float> inputTimes = new Queue<float>();

    private int currentGroup = -1;
    private bool isRevealing;
    private bool isRevealComplete;
    private bool skipTriggered;
    private bool continueRaised;
    private Sequence stepSequence;
    private string originalText;

    private void Awake()
    {
        if (contentText == null)
        {
            var content = transform.Find("Content");
            if (content != null) contentText = content.GetComponent<TextMeshProUGUI>();
        }
        if (continueButton == null)
        {
            var button = transform.Find("Button");
            if (button != null) continueButton = button.GetComponent<Button>();
        }
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueButtonPressed);
    }

    private void OnDestroy()
    {
        KillStep();
        if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueButtonPressed);
    }

    private void Update()
    {
        if (!isRevealing || isRevealComplete || !InputUtils.AnyKeyOrClickDown) return;

        float now = Time.unscaledTime;
        inputTimes.Enqueue(now);
        while (inputTimes.Count > 0 && now - inputTimes.Peek() > skipDetectionWindow) inputTimes.Dequeue();
        if (inputTimes.Count >= Mathf.Max(2, skipTriggerCount))
        {
            SkipToEnd();
            return;
        }

        if (IsFadeAnimationRunning()) stepSequence.Complete(true);
        else ShowGroup(currentGroup + 1);
    }

    // ===== REVEAL =====

    /// <summary>Start revealing <paramref name="fullText"/> from its first group.</summary>
    public void StartReveal(string fullText)
    {
        StopReveal();
        if (string.IsNullOrEmpty(fullText) || contentText == null) return;

        originalText = fullText;
        sentences.Clear();
        sentences.AddRange(SplitSentences(fullText.Replace("\r\n", "\n").Trim()));
        BuildGroups();
        opacities = new float[sentences.Count];
        isRevealing = true;
        Render();
        ShowGroup(0);
    }

    /// <summary>Stop revealing and hide the continue button (the text stays as it is).</summary>
    public void StopReveal()
    {
        KillStep();
        isRevealing = false;
        isRevealComplete = false;
        skipTriggered = false;
        continueRaised = false;
        currentGroup = -1;
        inputTimes.Clear();
        if (continueButton != null) continueButton.gameObject.SetActive(false);
    }

    /// <summary>Show everything now: earlier groups dimmed, the last group bright.</summary>
    [ContextMenu("Skip to End")]
    public void SkipToEnd()
    {
        if (sentences.Count == 0) return;
        KillStep();
        skipTriggered = true;
        currentGroup = groupStarts.Count - 1;
        int lastStart = groupStarts[currentGroup];
        for (int i = 0; i < opacities.Length; i++) opacities[i] = i < lastStart ? pastSentenceOpacity : activeSentenceOpacity;
        Render();
        Complete();
    }

    /// <summary>Advance one group immediately (tests, external control).</summary>
    public void AdvanceToNextSentence()
    {
        if (!isRevealing || isRevealComplete) return;
        if (IsFadeAnimationRunning()) stepSequence.Complete(true);
        ShowGroup(currentGroup + 1);
    }

    [ContextMenu("Reset Reveal")]
    public void ResetReveal()
    {
        StopReveal();
        if (contentText != null && originalText != null) contentText.text = originalText;
    }

    private void ShowGroup(int group)
    {
        if (group < 0 || group >= groupStarts.Count) return;
        KillStep();

        int previous = currentGroup;
        currentGroup = group;
        stepSequence = DOTween.Sequence().SetLink(gameObject);
        if (previous >= 0)
        {
            stepSequence.Append(FadeGroup(previous, activeSentenceOpacity, pastSentenceOpacity, sentenceFadeOutDuration, fadeOutEase));
        }
        stepSequence.Append(FadeGroup(group, 0f, activeSentenceOpacity, sentenceFadeInDuration, fadeInEase));
        stepSequence.OnComplete(() =>
        {
            stepSequence = null;
            if (currentGroup >= groupStarts.Count - 1) Complete();
        });
    }

    private Tween FadeGroup(int group, float from, float to, float duration, Ease ease)
    {
        int start = groupStarts[group];
        int end = group + 1 < groupStarts.Count ? groupStarts[group + 1] : sentences.Count;
        return DOTween.To(() => from, value =>
        {
            for (int i = start; i < end; i++) opacities[i] = value;
            Render();
        }, to, Mathf.Max(0.01f, duration)).SetEase(ease);
    }

    private void Complete()
    {
        if (isRevealComplete) return;
        isRevealComplete = true;
        isRevealing = false;
        if (continueButton != null) continueButton.gameObject.SetActive(true);
        OnRevealComplete?.Invoke();
    }

    private void OnContinueButtonPressed()
    {
        if (!isRevealComplete || continueRaised) return;
        continueRaised = true;
        OnContinuePressed?.Invoke();
    }

    private void KillStep()
    {
        if (stepSequence != null && stepSequence.IsActive()) stepSequence.Kill();
        stepSequence = null;
    }

    private void Render()
    {
        if (contentText == null) return;
        builder.Clear();
        for (int i = 0; i < sentences.Count; i++)
        {
            int alpha = Mathf.Clamp(Mathf.RoundToInt(opacities[i] * 255f), 0, 255);
            builder.Append("<alpha=#").Append(alpha.ToString("X2")).Append('>').Append(sentences[i]);
        }
        builder.Append("<alpha=#FF>");
        contentText.text = builder.ToString();
    }

    // ===== SENTENCES =====

    private void BuildGroups()
    {
        groupStarts.Clear();
        int size = Mathf.Max(1, sentencesPerGroup);
        int inGroup = 0;
        for (int i = 0; i < sentences.Count; i++)
        {
            if (inGroup == 0) groupStarts.Add(i);
            inGroup++;
            // A paragraph break or a full group ends the group.
            if (inGroup >= size || sentences[i].IndexOf('\n') >= 0) inGroup = 0;
        }
    }

    /// <summary>
    /// Split text into sentences, each keeping its trailing spaces and line breaks, so joining them gives
    /// back the original text. A sentence ends after . ! ? or … (and any closing quotes or brackets) followed
    /// by whitespace, or at a line break. Text inside &lt;rich tags&gt; is never split.
    /// </summary>
    public static List<string> SplitSentences(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        int start = 0, i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '<')
            {
                int close = text.IndexOf('>', i);
                i = close > i ? close + 1 : i + 1;
                continue;
            }

            bool lineBreak = c == '\n';
            bool terminal = c == '.' || c == '!' || c == '?' || c == '…';
            if (!lineBreak && !terminal)
            {
                i++;
                continue;
            }

            int end = i + 1;
            if (terminal)
            {
                while (end < text.Length && (IsTerminal(text[end]) || IsCloser(text[end]))) end++;
                if (end < text.Length && !char.IsWhiteSpace(text[end]))
                {
                    i = end; // "3.5", "e.g.x" or "...word": not a sentence end
                    continue;
                }
            }
            while (end < text.Length && char.IsWhiteSpace(text[end])) end++;
            result.Add(text.Substring(start, end - start));
            start = i = end;
        }
        if (start < text.Length) result.Add(text.Substring(start));
        return result;
    }

    private static bool IsTerminal(char c) => c == '.' || c == '!' || c == '?' || c == '…';

    private static bool IsCloser(char c) => c == '"' || c == '\'' || c == '”' || c == '’' || c == ')' || c == ']' || c == '»';

    // ===== QUERIES AND SETTINGS =====

    public bool IsRevealComplete() => isRevealComplete;

    /// <summary>Index of the last sentence currently shown.</summary>
    public int GetCurrentSentenceIndex()
    {
        if (currentGroup < 0) return 0;
        return (currentGroup + 1 < groupStarts.Count ? groupStarts[currentGroup + 1] : sentences.Count) - 1;
    }

    public int GetTotalSentenceCount() => sentences.Count;

    public void SetSentencesPerGroup(int newGroupSize)
    {
        sentencesPerGroup = Mathf.Max(1, newGroupSize);
        if (!isRevealing && !isRevealComplete) BuildGroups();
    }

    public int GetSentencesPerGroup() => sentencesPerGroup;

    public (int currentGroup, int totalGroups, int startSentence, int endSentence) GetCurrentGroupInfo()
    {
        if (currentGroup < 0 || groupStarts.Count == 0) return (0, groupStarts.Count, 0, 0);
        return (currentGroup, groupStarts.Count, groupStarts[currentGroup], GetCurrentSentenceIndex());
    }

    public bool WasSkipTriggered() => skipTriggered;

    public (float window, int count) GetSkipSettings() => (skipDetectionWindow, skipTriggerCount);

    public void SetSkipSettings(float window, int count)
    {
        skipDetectionWindow = Mathf.Max(0.1f, window);
        skipTriggerCount = Mathf.Max(2, count);
    }

    public float GetSentenceOpacity(int sentenceIndex) => sentenceIndex >= 0 && sentenceIndex < opacities.Length ? opacities[sentenceIndex] : 0f;

    public void SetSentenceOpacity(int sentenceIndex, float opacity)
    {
        if (sentenceIndex < 0 || sentenceIndex >= opacities.Length) return;
        opacities[sentenceIndex] = Mathf.Clamp01(opacity);
        Render();
    }

    public void SetFadeInEase(Ease ease) => fadeInEase = ease;

    public void SetFadeOutEase(Ease ease) => fadeOutEase = ease;

    public (Ease fadeIn, Ease fadeOut) GetFadeEaseSettings() => (fadeInEase, fadeOutEase);

    public void FadeOutSentenceImmediate(int sentenceIndex) => SetSentenceOpacity(sentenceIndex, pastSentenceOpacity);

    public void FadeOutSentenceGroupImmediate(int startIndex, int endIndex)
    {
        for (int i = Mathf.Max(0, startIndex); i <= endIndex && i < opacities.Length; i++) opacities[i] = pastSentenceOpacity;
        Render();
    }

    public bool IsFadeAnimationRunning() => stepSequence != null && stepSequence.IsActive() && stepSequence.IsPlaying();

    public int GetActiveFadeAnimationCount() => IsFadeAnimationRunning() ? 1 : 0;
}
