using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// One chorus choice card, a view over a <see cref="ChorusChoiceData"/> from the story index: title and
/// description, a <see cref="RequirementSlot"/> per requirement and cost, the pillar <see cref="ChallengeSlot"/>,
/// a locked overlay, and a tooltip with the requirements, costs, and what success and failure each lead to
/// (with their current odds, following the story to its end or next decision). Rare events and critical
/// successes are never revealed; a possible critical failure is flagged. Selecting the card hands the choice
/// to <see cref="ChorusScreenManager"/>, which checks, rolls and applies it.
/// </summary>
public class ChorusChoice : MonoBehaviour, ITooltipSource
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
        if (titleText != null) titleText.text = data != null ? CultureSystem.ExpandText(data.title) : string.Empty;
        if (descriptionText != null) descriptionText.text = data != null ? CultureSystem.ExpandText(data.description) : string.Empty;
        BuildRequirementSlots();
        BuildChallenge(pillarIcon);
        EnsureTooltip();
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
        EnsureTooltip();
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

    // The card's tooltip is built live (TooltipSystemLogic refreshes it), so odds and totals stay current.
    private void EnsureTooltip()
    {
        var trigger = TooltipTrigger.Ensure(gameObject);
        trigger.useCustomTooltip = false;
        trigger.enabled = true;
    }

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData d)
    {
        if (Data == null) return false;
        var stats = StatManager.Instance;
        float bonus = stats != null ? stats.GetSavingRollChancePercentCapped() : 0f;
        int pillar = Data.hasChallenge && stats != null ? stats.GetPillarValue(Data.challengePillar) : 0;

        d.title = Data.title;
        string stance = EventText.Humanize(Data.choiceId);
        d.type = Data.hasChallenge ? $"{stance}{TooltipText.Separator}{EventText.Humanize(Data.challengePillar)} Challenge" : stance;
        d.requirements = DescribeRequirements(Data);
        d.effects = DescribeOutcomes(Data, ChorusRules.Preview(Data, bonus, pillar), EventStoryIndex.ConsequencesAlong);
        if (!ChorusRules.IsAvailable(Data)) d.notes = TooltipText.Bad("Not available: a requirement is not met.");
        return true;
    }

    /// <summary>Requirements (met or not) and the costs paid when the choice is made.</summary>
    public static string DescribeRequirements(ChorusChoiceData choice)
    {
        var lines = new List<string>();
        if (choice.requirements.Count > 0)
        {
            lines.Add(TooltipText.Heading("Requires"));
            foreach (var r in choice.requirements) if (r != null) lines.Add(RequirementLine(r, false));
        }
        if (choice.requirementsCost.Count > 0)
        {
            lines.Add(TooltipText.Heading("Costs", TooltipText.Muted("paid when chosen")));
            foreach (var c in choice.requirementsCost) if (c != null) lines.Add(RequirementLine(c, true));
        }
        return TooltipText.Lines(lines);
    }

    private static string RequirementLine(EventCondition condition, bool isCost)
    {
        bool met = condition.Evaluate();
        return TooltipText.Bullet(TooltipText.Judge(EventText.DescribeRequirement(condition, isCost), met), met ? TooltipText.GoodHex : TooltipText.BadHex);
    }

    /// <summary>
    /// What the choice leads to, as the player may know it: success and failure with their odds and everything
    /// each one sets in motion until the story ends or asks for another decision. Rare events and critical
    /// successes stay secret; a possible critical failure is only flagged.
    /// </summary>
    /// <param name="along">Consequences from a knot onward (<see cref="EventStoryIndex.ConsequencesAlong"/>).</param>
    public static string DescribeOutcomes(ChorusChoiceData choice, ChorusPreview preview, AlongPath along)
    {
        var costs = ChorusRules.CostConsequences(choice);
        var sections = new List<string>();
        if (!preview.hasChallenge)
        {
            sections.Add(Outcome(TooltipText.Heading("Outcome"), choice.successConsequences, choice, First(choice.successPath, choice.destinationPath), costs, along));
        }
        else
        {
            sections.Add(Outcome(TooltipText.Heading("On Success", TooltipText.Good($"{preview.successPercent}%")),
                choice.successConsequences, choice, First(choice.successPath, choice.destinationPath), costs, along));
            if (preview.failurePercent > 0)
            {
                string failure = Outcome(TooltipText.Heading("On Failure", TooltipText.Bad($"{preview.failurePercent}%")),
                    choice.failureConsequences, choice, First(choice.failurePath, choice.destinationPath), costs, along);
                if (preview.canFailCritically) failure += "\n" + TooltipText.Bullet(TooltipText.Bad("May end in Critical Failure"), TooltipText.BadHex);
                sections.Add(failure);
            }
        }
        return string.Join("\n\n", sections);
    }

    public delegate List<EventConsequence> AlongPath(string startKnot, out bool leadsToDecision);

    private static string Outcome(string heading, List<EventConsequence> outcomeConsequences, ChorusChoiceData choice, string knot,
        List<EventConsequence> costs, AlongPath along)
    {
        var all = new List<EventConsequence>();
        if (outcomeConsequences != null) all.AddRange(outcomeConsequences);
        if (choice.consequences != null) all.AddRange(choice.consequences);
        bool decision = false;
        if (along != null && !string.IsNullOrEmpty(knot)) all.AddRange(along(knot, out decision));

        var lines = new List<string> { heading };
        string effects = TooltipText.Consequences(all, costs);
        lines.Add(string.IsNullOrEmpty(effects) ? TooltipText.Bullet(TooltipText.Muted("Nothing changes yet")) : effects);
        if (decision) lines.Add(TooltipText.Bullet(TooltipText.Muted("<i>...then another decision</i>")));
        return TooltipText.Lines(lines);
    }

    private static string First(string a, string b) => !string.IsNullOrEmpty(a) ? a : b;
}
