using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One requirement or cost on a chorus choice: an icon for what it needs, a panel that is green while it is
/// met (or affordable) and red otherwise, and a tooltip worded by <see cref="EventText"/> with the required
/// and current values. Costs read "Costs 5 Elderwood" and are paid when the choice is made.
/// </summary>
public class RequirementSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image requirementIcon;
    [SerializeField] private Image panel; // Green when met, red when not

    [Header("Unique Requirement Icons (Assign in Inspector)")]
    [SerializeField] private Sprite populationIcon;
    [SerializeField] private Sprite housingIcon;
    [SerializeField] private Sprite vagrantsIcon;
    [SerializeField] private Sprite deathsIcon;
    [SerializeField] private Sprite vagrantDeathsIcon;
    [SerializeField] private Sprite trueDeathsIcon;
    [SerializeField] private Sprite technologyIcon;
    [SerializeField] private Sprite scoreIcon;
    [SerializeField] private Sprite foodIcon;
    [SerializeField] private Sprite moraleIcon;

    [Header("Requirement Data")]
    [SerializeField] private EventCondition condition;
    [SerializeField] private bool isCost;

    private bool isRequirementMet;

    /// <summary>Show <paramref name="eventCondition"/>; <paramref name="cost"/> marks a price rather than a threshold.</summary>
    public void InitializeRequirement(EventCondition eventCondition, bool cost = false)
    {
        condition = eventCondition;
        isCost = cost;
        if (requirementIcon != null)
        {
            requirementIcon.sprite = ResolveIcon(condition);
            requirementIcon.enabled = requirementIcon.sprite != null;
        }
        RefreshRequirementStatus();
    }

    /// <summary>Re-check the requirement and refresh the panel colour and tooltip.</summary>
    public void RefreshRequirementStatus()
    {
        if (condition == null) return;
        isRequirementMet = condition.Evaluate();
        if (panel != null) panel.color = isRequirementMet ? Color.green : Color.red;
        TooltipTrigger.Ensure(gameObject).SetCustom(EventText.DescribeRequirement(condition, isCost), null,
            isCost ? "Cost" + TooltipText.Separator + "paid when chosen" : "Requirement", EventText.DescribeRequirementStatus(condition, isCost));
    }

    public bool IsRequirementMet() => isRequirementMet;

    public bool IsCost => isCost;

    public EventCondition GetCondition() => condition;

    private Sprite ResolveIcon(EventCondition requirement)
    {
        if (requirement == null) return null;
        string target = requirement.targetName;
        switch (requirement.Domain)
        {
            case "population": return populationIcon;
            case "housing": return housingIcon;
            case "vagrants": return vagrantsIcon;
            case "deaths": return deathsIcon;
            case "vagrant_deaths": return vagrantDeathsIcon;
            case "true_deaths": return trueDeathsIcon;
            case "technology": return technologyIcon;
            case "score":
            case "event_completed": return scoreIcon;
            case "stat":
                return IsNamed(target, "morale") && moraleIcon != null ? moraleIcon : UnitIcon(target);
            case "resource":
                return GameCatalog.HasRole(target, ResourceRole.Food) && foodIcon != null ? foodIcon : UnitIcon(target);
            default:
                return UnitIcon(target);
        }
    }

    private static bool IsNamed(string target, string name) => string.Equals(target, name, System.StringComparison.OrdinalIgnoreCase);

    private static Sprite UnitIcon(string unitName) =>
        GameUnitsLogic.Instance != null && !string.IsNullOrEmpty(unitName) ? GameUnitsLogic.Instance.GetGameUnitIconByName(unitName) : null;
}
