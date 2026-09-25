using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// One chorus choice card, a view over a <see cref="ChorusChoiceData"/> from the story index: title and
/// description, a <see cref="RequirementSlot"/> per requirement and cost, the pillar <see cref="ChallengeSlot"/>,
/// a locked overlay, and a tooltip listing each outcome with its current odds and consequences. Selecting it
/// hands the choice to <see cref="ChorusScreenManager"/>, which checks, rolls and applies it.
/// </summary>
public class ChorusChoice : MonoBehaviour
{
    [Header("Optional UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Requirements UI")]
    [SerializeField] private Transform requirementsContainer;
    [SerializeField] private GameObject requirementSlotPrefab;

    [Header("Challenge UI")]
    [SerializeField] private Transform challengeContainer;
    [SerializeField] private GameObject challengeSlotPrefab;

    [Header("Lock Overlay")]
    [SerializeField] private GameObject lockedOverlay;

    private const LogChannel Log = LogChannel.EventScreens;

    private readonly List<RequirementSlot> requirementSlots = new List<RequirementSlot>();
    private ChallengeSlot challengeSlot;
    private ChorusScreenManager screenManager;

    /// <summary>The choice shown (shared with the story index; never modified here).</summary>
    public ChorusChoiceData Data { get; private set; }

    public string ChoiceId => Data != null ? Data.choiceId : null;

    private void Awake()
    {
        screenManager = GetComponentInParent<ChorusScreenManager>();
    }

    /// <summary>Show <paramref name="data"/>; <paramref name="pillarIcon"/> is the challenge pillar's icon.</summary>
    public void Bind(ChorusChoiceData data, Sprite pillarIcon)
    {
        Data = data;
        if (titleText != null) titleText.text = data != null ? data.title : string.Empty;
        if (descriptionText != null) descriptionText.text = data != null ? data.description : string.Empty;
        BuildRequirementSlots();
        BuildChallenge(pillarIcon);
        RefreshTooltip();
    }

    /// <summary>Refresh requirement colours, challenge odds and the outcome tooltip (game state changed).</summary>
    public void RefreshChoice()
    {
        if (Data == null) return;
        foreach (var slot in requirementSlots)
        {
            if (slot != null) slot.RefreshRequirementStatus();
        }
        if (challengeSlot != null) challengeSlot.RefreshChallengeDisplay();
        RefreshTooltip();
    }

    /// <summary>Make this choice. True when the chorus accepted it (available and no choice made yet).</summary>
    public bool SelectChoice()
    {
        if (screenManager == null) screenManager = GetComponentInParent<ChorusScreenManager>();
        return screenManager != null && screenManager.OnChoiceSelected(this);
    }

    /// <summary>Show or hide the lock; the card itself stays fully readable.</summary>
    public void SetLockedOverlay(bool isLocked)
    {
        if (lockedOverlay != null) lockedOverlay.SetActive(isLocked);
        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
    }

    // ===== REQUIREMENTS AND CHALLENGE =====

    private void BuildRequirementSlots()
    {
        requirementSlots.Clear();
        if (requirementsContainer == null) return;
        for (int i = requirementsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(requirementsContainer.GetChild(i).gameObject);
        }
        if (Data == null || !Data.HasRequirements) return;
        if (requirementSlotPrefab == null)
        {
            GameLog.Warning($"{name}: '{Data.title}' has requirements but no requirement slot prefab is assigned.", Log);
            return;
        }
        foreach (var requirement in Data.requirements) AddRequirementSlot(requirement, false);
        foreach (var cost in Data.requirementsCost) AddRequirementSlot(cost, true);
    }

    private void AddRequirementSlot(EventCondition condition, bool isCost)
    {
        if (condition == null) return;
        var slot = Instantiate(requirementSlotPrefab, requirementsContainer).GetComponent<RequirementSlot>();
        if (slot == null) return;
        slot.InitializeRequirement(condition, isCost);
        requirementSlots.Add(slot);
    }

    private void BuildChallenge(Sprite pillarIcon)
    {
        if (Data == null || !Data.hasChallenge)
        {
            if (challengeSlot != null) Destroy(challengeSlot.gameObject);
            challengeSlot = null;
            return;
        }
        if (challengeSlot == null)
        {
            if (challengeContainer == null) challengeContainer = transform.Find("ChallengeContainer");
            if (challengeContainer == null || challengeSlotPrefab == null)
            {
                GameLog.Warning($"{name}: '{Data.title}' has a challenge but no challenge container or slot prefab is assigned.", Log);
                return;
            }
            for (int i = challengeContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(challengeContainer.GetChild(i).gameObject);
            }
            challengeSlot = Instantiate(challengeSlotPrefab, challengeContainer).GetComponentInChildren<ChallengeSlot>(true);
            if (challengeSlot == null)
            {
                GameLog.Warning($"{name}: the challenge slot prefab has no ChallengeSlot component.", Log);
                return;
            }
        }
        challengeSlot.InitializeChallenge(Data.challengePillar, Data.challengeStrength, pillarIcon);
    }

    // ===== OUTCOME TOOLTIP =====

    private void RefreshTooltip()
    {
        string outcomes = Data != null ? DescribeOutcomes(Data) : string.Empty;
        var trigger = GetComponent<TooltipTrigger>();
        if (string.IsNullOrEmpty(outcomes))
        {
            if (trigger != null) trigger.enabled = false;
            return;
        }
        if (trigger == null) trigger = gameObject.AddComponent<TooltipTrigger>();
        trigger.enabled = true;
        trigger.SetCustom("Consequences", outcomes);
    }

    /// <summary>
    /// What choosing <paramref name="choice"/> does: costs and unconditional consequences first, then each
    /// outcome that can happen with its current odds (the same numbers the roll uses).
    /// </summary>
    public static string DescribeOutcomes(ChorusChoiceData choice)
    {
        var upfront = ChorusRules.CostConsequences(choice);
        upfront.AddRange(choice.consequences);
        bool rolls = choice.hasChallenge || (choice.rareEventPercent > 0 && !string.IsNullOrEmpty(choice.rareEventPath));
        if (!rolls)
        {
            var all = new List<EventConsequence>(upfront);
            all.AddRange(choice.successConsequences);
            return EventText.DescribeConsequences(all);
        }

        var stats = StatManager.Instance;
        float bonus = stats != null ? stats.GetSavingRollChancePercentCapped() : 0f;
        int pillar = choice.hasChallenge && stats != null ? stats.GetPillarValue(choice.challengePillar) : 0;
        var odds = ChorusRules.OutcomeOdds(choice, bonus, pillar);

        var sb = new StringBuilder();
        if (upfront.Count > 0) sb.AppendLine("<b>When Chosen:</b>").AppendLine(EventText.DescribeConsequences(upfront));
        AppendOutcome(sb, "Rare Event", ChorusOutcome.RareEvent, choice.rareEventConsequences, odds, upfront);
        AppendOutcome(sb, "Critical Success", ChorusOutcome.CriticalSuccess, choice.critSuccessConsequences, odds, upfront);
        AppendOutcome(sb, "On Success", ChorusOutcome.Success, choice.successConsequences, odds, upfront);
        AppendOutcome(sb, "Otherwise", ChorusOutcome.TimePasses, choice.successConsequences, odds, upfront);
        AppendOutcome(sb, "On Failure", ChorusOutcome.Failure, choice.failureConsequences, odds, upfront);
        AppendOutcome(sb, "Critical Failure", ChorusOutcome.CriticalFailure, choice.critFailureConsequences, odds, upfront);
        return sb.ToString().TrimEnd();
    }

    private static void AppendOutcome(StringBuilder sb, string label, ChorusOutcome outcome, List<EventConsequence> consequences,
        Dictionary<ChorusOutcome, int> odds, List<EventConsequence> upfront)
    {
        if (!odds.TryGetValue(outcome, out int percent) || percent <= 0) return;
        string lines = EventText.DescribeConsequences(consequences, true, upfront);
        if (sb.Length > 0) sb.AppendLine();
        sb.AppendLine($"<b>{label} ({percent}%):</b>");
        sb.AppendLine(string.IsNullOrEmpty(lines) ? "- No Immediate Effect" : lines);
    }
}
