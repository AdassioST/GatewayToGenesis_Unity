using System.Collections.Generic;
using UnityEngine;
using TMPro;

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
			}
		}
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