using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Text;

public enum ChoiceValidationType
{
	None,
	Requirements,
	Challenge,
	Both
}

/// <summary>
/// UI component for a single Chorus decision choice.
/// Provides the API used by ChorusScreenManager and ChorusChoiceBackground.
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
	[SerializeField] private GameObject lockedOverlay; // Assign per choice (Idealism/Locked, etc.)

	// Backing fields
	private string choiceId;
	private string title;
	private string description;
	private string hoverDescription;
	private string destinationPath;
	private string successPath;
	private string failurePath;
	private ChoiceValidationType validationType = ChoiceValidationType.None;
	private List<EventCondition> requirements = new List<EventCondition>();
	private List<EventConsequence> successConsequences = new List<EventConsequence>();
	private List<EventConsequence> failureConsequences = new List<EventConsequence>();
	private List<EventConsequence> consequences = new List<EventConsequence>();
	private bool hasChallenge = false;
	private ChallengeSlot challengeSlot;

	private ChorusScreenManager screenManager;

	// Public API required by other components
	public string ChoiceId => choiceId;
	public string Title => title;
	public string Description => description;
	public string HoverDescription => hoverDescription;
	public string DestinationPath => destinationPath;
	public string SuccessPath => successPath;
	public string FailurePath => failurePath;
	public ChoiceValidationType ValidationType => validationType;
	public bool HasChallenge => hasChallenge;
	public List<EventCondition> Requirements => requirements;
	public List<EventConsequence> SuccessConsequences => successConsequences;
	public List<EventConsequence> FailureConsequences => failureConsequences;
	public List<EventConsequence> Consequences => consequences;
	public ChallengeSlot Challenge => challengeSlot;

	private void Awake()
	{
		screenManager = GetComponentInParent<ChorusScreenManager>();
	}

	// Signature expected by ChorusScreenManager.UpdateChoiceObject
	public void InitializeChoice(
		string choiceId,
		string title,
		string description,
		string hoverDescription,
		List<EventCondition> requirements,
		object _unused,
		string destinationPath,
		int _choiceIndex,
		string successPath,
		string failurePath,
		ChoiceValidationType validationType,
		List<EventConsequence> successConsequences,
		List<EventConsequence> failureConsequences,
		List<EventConsequence> consequences)
	{
		this.choiceId = choiceId;
		this.title = title;
		this.description = description;
		this.hoverDescription = hoverDescription;
		this.destinationPath = destinationPath;
		this.successPath = successPath;
		this.failurePath = failurePath;
		this.validationType = validationType;
		this.requirements = requirements ?? new List<EventCondition>();
		this.successConsequences = successConsequences ?? new List<EventConsequence>();
		this.failureConsequences = failureConsequences ?? new List<EventConsequence>();
		this.consequences = consequences ?? new List<EventConsequence>();

		if (titleText != null) titleText.text = this.title ?? string.Empty;
		if (descriptionText != null) descriptionText.text = this.description ?? string.Empty;

		if (requirements != null && requirements.Count > 0)
		{
			Debug.Log($"[ChorusChoice] {name} building {requirements.Count} requirement slots for choiceId='{this.choiceId}'");
		}
		else
		{
			Debug.Log($"[ChorusChoice] {name} has no requirements for choiceId='{this.choiceId}'");
		}
		BuildRequirementSlots();
		AttachOrUpdateConsequencesTooltip();
	}

	public void ConfigureChallenge(string pillarType, int requiredStrength, Sprite icon)
	{
		hasChallenge = !string.IsNullOrEmpty(pillarType) && requiredStrength > 0;
		if (!hasChallenge)
		{
			if (challengeSlot != null)
			{
				Destroy(challengeSlot.gameObject);
				challengeSlot = null;
			}
			return;
		}
		EnsureChallengeSlotInstance();
		if (challengeSlot != null)
		{
			challengeSlot.InitializeChallenge(pillarType, requiredStrength, icon);
		}
	}

	public void RefreshChoice()
	{
		// Refresh requirement statuses
		if (requirementsContainer != null)
		{
			var slots = requirementsContainer.GetComponentsInChildren<RequirementSlot>(true);
			foreach (var rs in slots)
			{
				rs.RefreshRequirementStatus();
			}
		}
		// Refresh challenge visuals with current StatManager runtime values
		if (challengeSlot != null)
		{
			challengeSlot.RefreshChallengeDisplay();
		}
		// Refresh consequences tooltip (for updated new totals)
		AttachOrUpdateConsequencesTooltip();
	}

	public void SelectChoice()
	{
		if (screenManager == null) screenManager = GetComponentInParent<ChorusScreenManager>();
		if (screenManager != null)
		{
			screenManager.OnChoiceSelected(this);
		}
	}

	public void SetLockedOverlay(bool isLocked)
	{
		if (lockedOverlay != null)
		{
			lockedOverlay.SetActive(isLocked);
		}
		// Ensure choice content remains fully visible regardless of lock state (overlay handles lock visuals)
		var cg = GetComponent<CanvasGroup>();
		if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
		cg.alpha = 1f;
	}

	private void BuildRequirementSlots()
	{
		if (requirementsContainer == null || requirementSlotPrefab == null)
		{
			if (requirements != null && requirements.Count > 0)
			{
				Debug.LogWarning($"[ChorusChoice] {name}: Requirements present but missing container or slot prefab. containerNull={(requirementsContainer==null)}, prefabNull={(requirementSlotPrefab==null)}");
			}
			return;
		}
		// Clear
		for (int i = requirementsContainer.childCount - 1; i >= 0; i--)
		{
			Destroy(requirementsContainer.GetChild(i).gameObject);
		}
		// Build
		foreach (var cond in requirements)
		{
			var go = Instantiate(requirementSlotPrefab, requirementsContainer);
			var rs = go.GetComponent<RequirementSlot>();
			if (rs != null)
			{
				rs.InitializeRequirement(cond);
				// RequirementSlot handles its own tooltip setup in UpdateRequirementDisplay()
			}
		}
	}

	private void AttachOrUpdateConsequencesTooltip()
	{
		// Build tooltip content
		BuildChoiceConsequencesTooltip(out string title, out string desc);

		// If there's nothing to show, avoid adding a trigger
		if (string.IsNullOrEmpty(desc))
		{
			var existing = GetComponent<TooltipTrigger>();
			if (existing != null && existing.useCustomTooltip && string.IsNullOrEmpty(existing.customTitle) && string.IsNullOrEmpty(existing.customDescription))
			{
				// Leave as-is to avoid flicker; no-op
			}
			return;
		}

		var trig = GetComponent<TooltipTrigger>();
		if (trig == null) trig = gameObject.AddComponent<TooltipTrigger>();
		trig.useCustomTooltip = true;
		trig.customTitle = title;
		trig.customDescription = desc;
		trig.customType = string.Empty;
	}

	private void BuildChoiceConsequencesTooltip(out string title, out string description)
	{
		title = "Consequences";
		var sb = new StringBuilder();

		bool hasSuccess = successConsequences != null && successConsequences.Count > 0;
		bool hasFailure = failureConsequences != null && failureConsequences.Count > 0;
		bool hasDirect = consequences != null && consequences.Count > 0;

		if (hasSuccess || hasFailure)
		{
			if (hasSuccess)
			{
				sb.AppendLine("On Success:");
				sb.Append(BuildConsequencesPreviewText(successConsequences));
			}
			if (hasFailure)
			{
				if (hasSuccess) sb.AppendLine();
				sb.AppendLine("On Failure:");
				sb.Append(BuildConsequencesPreviewText(failureConsequences));
			}
		}
		else if (hasDirect)
		{
			sb.Append(BuildConsequencesPreviewText(consequences));
		}

		description = sb.ToString().TrimEnd();
	}

	// Local copy of the preview builder to avoid cross-class dependency
	private string BuildConsequencesPreviewText(List<EventConsequence> consequences)
	{
		if (consequences == null || consequences.Count == 0) return string.Empty;

		var sb = new StringBuilder();
		foreach (var c in consequences)
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
					sb.AppendLine($"- You've {verb} {Mathf.Abs(c.value)} {c.targetName} (New Total {next})");
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
					sb.AppendLine($"- You've Unlocked event {c.targetName}");
					break;
				}
				case EventConsequence.ConsequenceType.PopulationChange:
				{
					if (PopGrowthLogic.Instance != null)
					{
						int current = PopGrowthLogic.Instance.population;
						int next = current + c.value;
						sb.AppendLine($"- {Mathf.Abs(c.value)} population have been killed (New Total {Mathf.Max(next, 0)})");
					}
					else
					{
						sb.AppendLine($"- {Mathf.Abs(c.value)} population have been killed");
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
							sb.AppendLine($"- {c.value} new housing units have been gained (New Total {Mathf.Max(next, 0)})");
						else
							sb.AppendLine($"- {Mathf.Abs(c.value)} housing units have been lost (New Total {Mathf.Max(next, 0)})");
					}
					else
					{
						if (c.value > 0) sb.AppendLine($"- {c.value} new housing units have been gained");
						else sb.AppendLine($"- {Mathf.Abs(c.value)} housing units have been lost");
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
							sb.AppendLine($"- {c.value} new vagrants have arrived (New Total {Mathf.Max(next, 0)})");
						else
							sb.AppendLine($"- {Mathf.Abs(c.value)} vagrants have been killed (New Total {Mathf.Max(next, 0)})");
					}
					else
					{
						if (c.value > 0) sb.AppendLine($"- {c.value} new vagrants have arrived");
						else sb.AppendLine($"- {Mathf.Abs(c.value)} vagrants have been killed");
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
							sb.AppendLine($"- {c.value} additional deaths have been recorded (New Total {Mathf.Max(next, 0)})");
						else
							sb.AppendLine($"- {Mathf.Abs(c.value)} deaths have been wiped from the historical records (New Total {Mathf.Max(next, 0)})");
					}
					else
					{
						if (c.value > 0) sb.AppendLine($"- {c.value} additional deaths have been recorded");
						else sb.AppendLine($"- {Mathf.Abs(c.value)} deaths have been wiped from the historical records");
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
							sb.AppendLine($"- {Mathf.Abs(c.value)} deaths have been wiped from the public records (New Total {Mathf.Max(next, 0)})");
						else
							sb.AppendLine($"- {c.value} additional deaths have been added to the public records (New Total {Mathf.Max(next, 0)})");
					}
					else
					{
						if (c.value < 0) sb.AppendLine($"- {Mathf.Abs(c.value)} deaths have been wiped from the public records");
						else sb.AppendLine($"- {c.value} additional deaths have been added to the public records");
					}
					break;
				}
				case EventConsequence.ConsequenceType.ProductionPercentChange:
				{
					string sign = c.value >= 0 ? "+" : "-";
					string dur = c.durationSevenths > 0 ? $" (for {c.durationSevenths} sevenths)" : string.Empty;
					sb.AppendLine($"- Production {sign}{Mathf.Abs(c.value)}% for {c.targetName}{dur}");
					break;
				}
				case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
				{
					string sign = c.value >= 0 ? "+" : "-";
					string dur = c.durationSevenths > 0 ? $" (for {c.durationSevenths} sevenths)" : string.Empty;
					sb.AppendLine($"- Production {sign}{Mathf.Abs(c.value)}% for section {c.targetName}{dur}");
					break;
				}
				case EventConsequence.ConsequenceType.ClickPowerChange:
				{
					string sign = c.value >= 0 ? "+" : "-";
					string dur = c.durationSevenths > 0 ? $" (for {c.durationSevenths} sevenths)" : string.Empty;
					sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)} for {c.targetName}{dur}");
					break;
				}
				case EventConsequence.ConsequenceType.ClickPowerPercentChange:
				{
					string sign = c.value >= 0 ? "+" : "-";
					string dur = c.durationSevenths > 0 ? $" (for {c.durationSevenths} sevenths)" : string.Empty;
					sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)}% for {c.targetName}{dur}");
					break;
				}
				case EventConsequence.ConsequenceType.ClickPowerChangeSection:
				{
					string sign = c.value >= 0 ? "+" : "-";
					string dur = c.durationSevenths > 0 ? $" (for {c.durationSevenths} sevenths)" : string.Empty;
					sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)} for section {c.targetName}{dur}");
					break;
				}
				case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
				{
					string sign = c.value >= 0 ? "+" : "-";
					string dur = c.durationSevenths > 0 ? $" (for {c.durationSevenths} sevenths)" : string.Empty;
					sb.AppendLine($"- Click Power {sign}{Mathf.Abs(c.value)}% for section {c.targetName}{dur}");
					break;
				}
				default:
				{
					sb.AppendLine($"- Unknown consequence type: {c.type} for {c.targetName} (value: {c.value})");
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

	private void EnsureChallengeSlotInstance()
	{
		if (challengeSlot != null) return;
		if (challengeContainer == null)
		{
			var t = transform.Find("ChallengeContainer");
			if (t != null) challengeContainer = t;
		}
		if (challengeContainer == null)
		{
			Debug.LogWarning($"[ChorusChoice] {name}: Missing challengeContainer reference");
			return;
		}
		if (challengeSlotPrefab == null)
		{
			Debug.LogWarning($"[ChorusChoice] {name}: Missing challengeSlotPrefab reference");
			return;
		}
		for (int i = challengeContainer.childCount - 1; i >= 0; i--)
		{
			Destroy(challengeContainer.GetChild(i).gameObject);
		}
		var go = Instantiate(challengeSlotPrefab, challengeContainer);
		challengeSlot = go.GetComponent<ChallengeSlot>();
		if (challengeSlot == null)
		{
			challengeSlot = go.GetComponentInChildren<ChallengeSlot>(true);
			if (challengeSlot == null)
			{
				Debug.LogWarning($"[ChorusChoice] {name}: ChallengeSlot component not found on prefab or its children");
			}
		}
	}
}