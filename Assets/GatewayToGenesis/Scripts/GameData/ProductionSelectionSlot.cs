using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One resource in a <see cref="ResourceSelectionWindow"/> or the HUD selection list. Clicking it points a HUD
/// click button ("BuildingMaterial" or "VitalResource") at this resource. The button is found when first
/// needed, so a slot created while the HUD is hidden still works.
/// </summary>
public class ProductionSelectionSlot : MonoBehaviour, IGameUnitSlot
{
    public GameUnit gameUnit { get; set; }

    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    private ClickLogic targetClickLogic;
    private string targetButtonName;

    private void Awake()
    {
        if (icon == null)
        {
            var iconChild = transform.Find("Icon");
            if (iconChild != null) icon = iconChild.GetComponent<Image>();
        }
        if (button == null) button = GetComponent<Button>();
        // The prefab binds SelectThisResource in the inspector; wire it here only when it does not.
        if (button != null && button.onClick.GetPersistentEventCount() == 0) button.onClick.AddListener(SelectThisResource);
    }

    /// <summary>Show <paramref name="newGameUnit"/> and aim the slot at the HUD button called <paramref name="targetButton"/>.</summary>
    public void InitializeSelectionSlot(GameUnit newGameUnit, string targetButton = "BuildingMaterial")
    {
        gameUnit = newGameUnit;
        gameObject.name = newGameUnit.name;
        if (icon != null) icon.sprite = newGameUnit.icon;
        if (targetButton != targetButtonName) targetClickLogic = null;
        targetButtonName = targetButton;
    }

    /// <summary>Point the target click button at this resource (bound to the button's OnClick).</summary>
    public void SelectThisResource()
    {
        var target = TargetClickLogic();
        if (gameUnit == null || target == null)
        {
            GameLog.Warning($"Selection slot '{name}' has no {(gameUnit == null ? "resource" : $"'{targetButtonName}' click button")} to set.", LogChannel.Units);
            return;
        }
        target.SetActiveResource(gameUnit.name);
    }

    private ClickLogic TargetClickLogic()
    {
        if (targetClickLogic != null || string.IsNullOrEmpty(targetButtonName)) return targetClickLogic;
        foreach (var candidate in FindObjectsByType<ClickLogic>(FindObjectsInactive.Include))
        {
            if (candidate.name == targetButtonName)
            {
                targetClickLogic = candidate;
                break;
            }
        }
        return targetClickLogic;
    }
}
