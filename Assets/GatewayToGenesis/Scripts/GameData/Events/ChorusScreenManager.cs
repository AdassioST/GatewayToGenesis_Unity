using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// The chorus: the player drags the decision token onto Idealism, Realism or (when offered) Pragmatism.
/// Choices come from <see cref="EventStoryIndex"/>; the roll follows <see cref="ChorusRules"/>. The moment a
/// choice is made its costs and outcome consequences are queued (they apply when the story completes), the
/// result card is shown, and the next input continues to the knot the outcome leads to. A choice can only be
/// made once per chorus.
/// </summary>
public class ChorusScreenManager : MonoBehaviour
{
    private const LogChannel Log = LogChannel.EventScreens;

    [Header("Screen Variants")]
    [SerializeField] private GameObject fullOutcomesVariant; // 3 choices: Idealism, Realism, Pragmatism
    [SerializeField] private GameObject twoChoicesVariant;   // 2 choices: Idealism, Realism

    [Header("Static Pillar References")]
    [SerializeField] private TMP_Text aureusAmountText;
    [SerializeField] private TMP_Text chorusAmountText;
    [SerializeField] private TMP_Text regaliaAmountText;
    [SerializeField] private TMP_Text waltzAmountText;

    [Header("Choice Containers")]
    [SerializeField] private Transform fullOutcomesChoicesContainer;
    [SerializeField] private Transform twoChoicesChoicesContainer;

    [Header("Static Choice References - FullOutcomes")]
    [SerializeField] private ChorusChoice fullOutcomesIdealism;
    [SerializeField] private ChorusChoice fullOutcomesRealism;
    [SerializeField] private ChorusChoice fullOutcomesPragmatism;

    [Header("Static Choice References - TwoChoices")]
    [SerializeField] private ChorusChoice twoChoicesIdealism;
    [SerializeField] private ChorusChoice twoChoicesRealism;

    [Header("Background Elements")]
    [SerializeField] private Transform fullOutcomesBackgrounds; // Idealism, Realism, Pragmatism drop areas
    [SerializeField] private Transform twoChoicesBackgrounds;   // Idealism, Realism drop areas

    [Header("Decision Token")]
    [SerializeField] private GameObject decisionToken;
    [SerializeField] private Transform tokenStartPosition;

    [Header("Description Display")]
    [SerializeField] private TMP_Text descriptionText; // The chorus question (last line of the knot)

    [Header("Choice Prefabs")]
    [SerializeField] private GameObject requirementSlotPrefab;
    [SerializeField] private GameObject challengeResultPrefab;

    [Header("Pillar Icons")]
    [SerializeField] private Sprite aureusIcon;
    [SerializeField] private Sprite regaliaIcon;
    [SerializeField] private Sprite waltzIcon;
    [SerializeField] private Sprite chorusIcon;

    private EventScreen currentEventScreen;
    private readonly List<ChorusChoiceData> availableChoices = new List<ChorusChoiceData>();
    private readonly List<ChorusChoiceBackground> backgroundChoices = new List<ChorusChoiceBackground>();
    private bool isPragmatismAvailable;
    private bool isDragging;
    private bool choiceWasMade;
    private float nextRefresh;
    private const float RefreshInterval = 0.5f;

    private EventSystemLogic eventSystem;
    private StatManager statManager;

    private void Awake()
    {
        eventSystem = EventSystemLogic.Instance;
        statManager = StatManager.Instance;
    }

    private void Start()
    {
        UpdatePillarDisplay();
        if (decisionToken != null && tokenStartPosition != null) decisionToken.transform.position = tokenStartPosition.position;
    }

    private void OnEnable()
    {
        if (statManager == null) statManager = StatManager.Instance;
        if (statManager != null) statManager.OnPillarChanged += HandlePillarChanged;
        UpdatePillarDisplay();
        RefreshChoices();
    }

    private void OnDisable()
    {
        if (statManager != null) statManager.OnPillarChanged -= HandlePillarChanged;
    }

    // Resources keep flowing while the chorus is open, so a requirement can become met (or a cost
    // unaffordable) without a pillar changing; re-check a couple of times a second until a choice is made.
    private void Update()
    {
        if (choiceWasMade || isDragging || availableChoices.Count == 0 || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + RefreshInterval;
        RefreshAvailability();
    }

    private void HandlePillarChanged(string pillar, int newValue) => RefreshAvailability();

    private void RefreshAvailability()
    {
        RefreshChoices();
        foreach (var background in backgroundChoices)
        {
            if (background != null) background.RefreshAvailability();
        }
    }

    // ===== SETUP =====

    /// <summary>Fill the chorus from its knot: variant, choices, drop areas, pillars and question.</summary>
    public void InitializeChorusScreen(EventScreen screen)
    {
        currentEventScreen = screen;
        choiceWasMade = false;
        availableChoices.Clear();

        var knot = screen != null ? EventStoryIndex.Get(screen.inkKnot) : null;
        if (knot == null || knot.chorusChoices.Count == 0)
        {
            GameLog.Error($"Chorus '{screen?.inkKnot}' has no choices in the story index; continuing past it.", Log);
            Navigate(knot != null ? knot.ContinueTarget : null);
            return;
        }
        availableChoices.AddRange(knot.chorusChoices);
        isPragmatismAvailable = Find("pragmatism") != null;

        if (fullOutcomesVariant != null) fullOutcomesVariant.SetActive(isPragmatismAvailable);
        if (twoChoicesVariant != null) twoChoicesVariant.SetActive(!isPragmatismAvailable);
        if (fullOutcomesChoicesContainer != null) fullOutcomesChoicesContainer.gameObject.SetActive(isPragmatismAvailable);
        if (twoChoicesChoicesContainer != null) twoChoicesChoicesContainer.gameObject.SetActive(!isPragmatismAvailable);

        if (isPragmatismAvailable)
        {
            BindChoice(fullOutcomesIdealism, Find("idealism"));
            BindChoice(fullOutcomesRealism, Find("realism"));
            BindChoice(fullOutcomesPragmatism, Find("pragmatism"));
        }
        else
        {
            BindChoice(twoChoicesIdealism, Find("idealism"));
            BindChoice(twoChoicesRealism, Find("realism"));
        }
        BindBackgrounds(isPragmatismAvailable ? fullOutcomesBackgrounds : twoChoicesBackgrounds);

        UpdatePillarDisplay();
        if (descriptionText != null)
        {
            string question = knot.LastLine;
            descriptionText.text = string.IsNullOrEmpty(question) ? EventScript.DefaultChoiceDescription : CultureSystem.ExpandText(question);
        }
        GameLog.Event($"Chorus '{knot.name}': {availableChoices.Count} choices ({(isPragmatismAvailable ? "three" : "two")}-choice layout)", Log);
    }

    private ChorusChoiceData Find(string choiceId) =>
        availableChoices.Find(c => string.Equals(c.choiceId, choiceId, System.StringComparison.OrdinalIgnoreCase));

    private void BindChoice(ChorusChoice view, ChorusChoiceData data)
    {
        if (view == null || data == null) return;
        view.Bind(data, GetPillarIcon(data.challengePillar));
    }

    private void BindBackgrounds(Transform container)
    {
        backgroundChoices.Clear();
        if (container == null) return;
        foreach (var data in availableChoices)
        {
            var element = container.Find(data.choiceId);
            if (element == null) element = container.Find(EventText.Humanize(data.choiceId));
            var background = element != null ? element.GetComponent<ChorusChoiceBackground>() : null;
            if (background == null)
            {
                GameLog.Warning($"No ChorusChoiceBackground named '{data.choiceId}' under {container.name}.", Log);
                continue;
            }
            background.InitializeChoice(data.choiceId, data);
            backgroundChoices.Add(background);
        }
    }

    private Sprite GetPillarIcon(string pillarType)
    {
        switch ((pillarType ?? string.Empty).ToLowerInvariant())
        {
            case "aureus": return aureusIcon;
            case "regalia": return regaliaIcon;
            case "waltz": return waltzIcon;
            case "chorus": return chorusIcon;
            default: return null;
        }
    }

    private void UpdatePillarDisplay()
    {
        if (statManager == null) statManager = StatManager.Instance;
        if (statManager == null) return;
        SetPillar(aureusAmountText, "aureus");
        SetPillar(chorusAmountText, "chorus");
        SetPillar(regaliaAmountText, "regalia");
        SetPillar(waltzAmountText, "waltz");
    }

    private void SetPillar(TMP_Text label, string pillar)
    {
        if (label != null) label.text = statManager.GetPillarValue(pillar).ToString();
    }

    // ===== TOKEN DRAG =====

    /// <summary>Dragging started: available choices fade out of the way, locked ones stay visible.</summary>
    public void OnTokenDragStarted()
    {
        isDragging = true;
        FadeAvailableChoices(0f);
        foreach (var background in backgroundChoices)
        {
            if (background != null && background.IsAvailable()) background.ApplyDragBaseAlpha(0.15f);
        }
    }

    /// <summary>Dragging ended: without a choice the cards come back and the drop areas reset.</summary>
    public void OnTokenDragEnded()
    {
        isDragging = false;
        if (!choiceWasMade) FadeAvailableChoices(1f);
        foreach (var background in backgroundChoices)
        {
            if (background == null) continue;
            if (background.IsAvailable()) background.RestoreOriginalBaseAlpha();
            background.ResetToBaseAlpha();
        }
    }

    private void FadeAvailableChoices(float alpha)
    {
        var container = GetChoicesContainer();
        if (container == null) return;
        foreach (var choice in container.GetComponentsInChildren<ChorusChoice>(true))
        {
            if (choice == null) continue;
            var canvasGroup = choice.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = choice.gameObject.AddComponent<CanvasGroup>();
            DOTween.Kill(canvasGroup);
            if (IsAvailable(choice.ChoiceId)) canvasGroup.DOFade(alpha, 0.25f).SetEase(Ease.OutQuad).SetLink(choice.gameObject);
            else canvasGroup.alpha = 1f; // locked choices stay readable
        }
    }

    private bool IsAvailable(string choiceId)
    {
        foreach (var background in backgroundChoices)
        {
            if (background != null && string.Equals(background.GetChoiceId(), choiceId, System.StringComparison.OrdinalIgnoreCase)) return background.IsAvailable();
        }
        return true;
    }

    /// <summary>Brighten every available drop area by the token's distance from the centre.</summary>
    public void UpdateBackgroundAlphaByTokenDistance(float distanceFromCenter, float maxDistance)
    {
        if (!isDragging) return;
        foreach (var background in backgroundChoices)
        {
            if (background != null && background.IsAvailable()) background.UpdateAlphaByTokenDistance(distanceFromCenter, maxDistance);
        }
    }

    /// <summary>Highlight the drop area under the token; the others return to their base alpha.</summary>
    public void UpdateBackgroundHover(ChorusChoiceBackground activeBackground, float distanceFromCenter, float maxDistance)
    {
        if (!isDragging) return;
        foreach (var background in backgroundChoices)
        {
            if (background == null || !background.IsAvailable()) continue;
            if (background == activeBackground) background.UpdateAlphaByTokenDistance(distanceFromCenter, maxDistance);
            else background.ResetToBaseAlpha();
        }
    }

    public void ResetAllBackgroundsToBase()
    {
        foreach (var background in backgroundChoices)
        {
            if (background != null) background.ResetToBaseAlpha();
        }
    }

    public Transform GetChoicesContainer() => isPragmatismAvailable ? fullOutcomesChoicesContainer : twoChoicesChoicesContainer;

    public bool IsDragging() => isDragging;

    /// <summary>True once a choice has been made; the token can no longer be dropped.</summary>
    public bool IsChoiceLocked() => choiceWasMade;

    /// <summary>Refresh requirement, cost and challenge displays (pillars or resources changed).</summary>
    public void RefreshChoices()
    {
        if (isPragmatismAvailable)
        {
            if (fullOutcomesIdealism != null) fullOutcomesIdealism.RefreshChoice();
            if (fullOutcomesRealism != null) fullOutcomesRealism.RefreshChoice();
            if (fullOutcomesPragmatism != null) fullOutcomesPragmatism.RefreshChoice();
        }
        else
        {
            if (twoChoicesIdealism != null) twoChoicesIdealism.RefreshChoice();
            if (twoChoicesRealism != null) twoChoicesRealism.RefreshChoice();
        }
        UpdatePillarDisplay();
    }

    // ===== RESOLUTION =====

    /// <summary>
    /// The player chose <paramref name="choice"/>: pay, roll, queue the outcome and show the result card.
    /// False (nothing happens) when a choice was already made or this one is not available.
    /// </summary>
    public bool OnChoiceSelected(ChorusChoice choice)
    {
        if (choice == null || choiceWasMade) return false;
        var data = choice.Data != null ? choice.Data : Find(choice.ChoiceId);
        if (data == null)
        {
            GameLog.Warning($"No choice data for '{choice.ChoiceId}'.", Log);
            return false;
        }
        if (!ChorusRules.IsAvailable(data))
        {
            GameLog.Event($"'{data.title}' is not available; drop ignored.", Log);
            return false;
        }
        choiceWasMade = true;

        if (statManager == null) statManager = StatManager.Instance;
        float bonus = statManager != null ? statManager.GetSavingRollChancePercentCapped() : 0f;
        int pillar = data.hasChallenge && statManager != null ? statManager.GetPillarValue(data.challengePillar) : 0;
        var result = ChorusRules.Resolve(data, Random.Range(1, 101), bonus, pillar);
        EventSystemLogic.Instance?.RememberStoryChoice(data.title + (data.hasChallenge ? " — " + result.outcome : ""));
        string story = EventSystemLogic.Instance?.GetCurrentStoryNode()?.nodeName;
        Achievements.Report(AchievementEvent.Chorus(result).From($"chorus:{story}:{data.choiceId}", story));

        Queue(ChorusRules.CostConsequences(data));
        Queue(result.consequences);
        GameLog.Event($"'{data.title}': {result.outcome} (roll {result.naturalRoll}→{result.enhancedRoll}, success {result.successPercent}%{(result.savedByRoll ? ", saved by Piety" : "")}) → '{result.targetKnot ?? "(end)"}'", Log);

        ShowChallengeResult(choice, result);
        return true;
    }

    private void Queue(List<EventConsequence> consequences)
    {
        if (eventSystem == null) eventSystem = EventSystemLogic.Instance;
        if (eventSystem == null || consequences == null) return;
        foreach (var consequence in consequences) eventSystem.AddConsequence(consequence);
    }

    private void ShowChallengeResult(ChorusChoice choice, ChorusResolution result)
    {
        if (challengeResultPrefab == null)
        {
            Navigate(result.targetKnot);
            return;
        }

        var card = Instantiate(challengeResultPrefab, choice.transform.position, Quaternion.identity, transform);
        var canvasGroup = card.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = card.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        var resultText = FindText(card, "Result");
        if (resultText != null)
        {
            resultText.text = ResultTitle(result.outcome);
            resultText.color = ResultColor(result.outcome);
        }

        var rollText = FindText(card, "RollChance");
        if (rollText != null)
        {
            rollText.gameObject.SetActive(result.savedByRoll);
            if (result.savedByRoll)
            {
                bool triggered = result.outcome == ChorusOutcome.RareEvent || result.outcome == ChorusOutcome.CriticalSuccess;
                rollText.text = triggered ? "TRIGGERED BY ENHANCED ROLL CHANCE!" : "SAVED BY ENHANCED ROLL CHANCE!";
                rollText.color = result.outcome == ChorusOutcome.RareEvent ? RareColor : Color.green;
            }
        }

        canvasGroup.DOFade(1f, 0.5f).SetEase(Ease.OutQuad).SetLink(card)
            .OnComplete(() => StartCoroutine(WaitForInputAndProceed(card, canvasGroup, result.targetKnot)));
    }

    private IEnumerator WaitForInputAndProceed(GameObject card, CanvasGroup canvasGroup, string target)
    {
        while (card != null && !InputUtils.AnyKeyOrClickDown) yield return null;
        if (card == null) yield break;
        canvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad).SetLink(card).OnComplete(() =>
        {
            Destroy(card);
            Navigate(target);
        });
    }

    private static readonly Color RareColor = new Color(1f, 0.84f, 0f);

    private static string ResultTitle(ChorusOutcome outcome)
    {
        switch (outcome)
        {
            case ChorusOutcome.RareEvent: return "RARE EVENT!";
            case ChorusOutcome.CriticalSuccess: return "CRITICAL SUCCESS!";
            case ChorusOutcome.Success: return "SUCCESS!";
            case ChorusOutcome.Failure: return "FAILURE...";
            case ChorusOutcome.CriticalFailure: return "CRITICAL FAILURE!";
            default: return "TIME PASSES...";
        }
    }

    private static Color ResultColor(ChorusOutcome outcome)
    {
        switch (outcome)
        {
            case ChorusOutcome.RareEvent: return RareColor;
            case ChorusOutcome.CriticalSuccess:
            case ChorusOutcome.Success: return Color.green;
            case ChorusOutcome.Failure: return Color.red;
            case ChorusOutcome.CriticalFailure: return new Color(0.6f, 0f, 0f);
            default: return Color.gray;
        }
    }

    private static TMP_Text FindText(GameObject root, string childName)
    {
        var child = root.transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void Navigate(string knot)
    {
        var volumes = EventVolumeManager.Instance;
        if (volumes == null) return;
        if (string.IsNullOrEmpty(knot)) volumes.CompleteStory();
        else volumes.NavigateToKnot(knot);
    }
}
