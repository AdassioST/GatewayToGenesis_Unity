using UnityEngine;
using UnityEngine.UI;

/// <summary>Button behaviour for HUD slots: gather a resource, build a production unit or start research.</summary>
public class ClickLogic : MonoBehaviour
{
    public GameUnitsLogic gameUnitsLogic;

    public enum ClickMode
    {
        AddResource,
        BuildProductionUnit,
        UnlockTechnology
    }

    public ClickMode currentMode = ClickMode.AddResource;

    public string activeResource;

    [SerializeField] Sprite affordableSprite, unaffordableSprite;
    [Tooltip("Seconds between affordability checks for build buttons.")]
    [SerializeField] private float affordabilityRefreshSeconds = 0.2f;

    private Image buttonImage;
    private GameProductionSlot productionSlot;
    public GameTechnologySlot technologySlot;
    private float nextAffordabilityCheck;

    private void Start()
    {
        if (gameUnitsLogic == null) gameUnitsLogic = GameUnitsLogic.Instance;
        buttonImage = GetComponent<Image>();
        productionSlot = GetComponent<GameProductionSlot>();
        if (productionSlot == null) productionSlot = GetComponentInParent<GameProductionSlot>();
        technologySlot = GetComponent<GameTechnologySlot>();
    }

    private void Update()
    {
        if (currentMode != ClickMode.BuildProductionUnit || Time.unscaledTime < nextAffordabilityCheck) return;
        nextAffordabilityCheck = Time.unscaledTime + affordabilityRefreshSeconds;
        RefreshButtonAppearance();
    }

    public void OnButtonClick()
    {
        if (gameUnitsLogic == null)
        {
            Debug.LogError("GameUnitsLogic is not assigned", this);
            return;
        }

        switch (currentMode)
        {
            case ClickMode.AddResource:
                if (!string.IsNullOrEmpty(activeResource)) gameUnitsLogic.ChangeResourceFromName(activeResource, 0, true);
                break;

            case ClickMode.BuildProductionUnit:
                string productionUnitName = productionSlot != null && productionSlot.gameUnit != null ? productionSlot.gameUnit.name : null;
                if (!string.IsNullOrEmpty(productionUnitName) && gameUnitsLogic.BuildProductionUnit(productionUnitName)) RefreshButtonAppearance();
                break;

            case ClickMode.UnlockTechnology:
                if (technologySlot != null && !technologySlot.isUnlocked && !technologySlot.alreadyClicked) gameUnitsLogic.StartTechnologyProgress(technologySlot);
                break;
        }
    }

    private void RefreshButtonAppearance()
    {
        if (productionSlot == null || productionSlot.gameUnit == null || buttonImage == null || gameUnitsLogic == null) return;
        buttonImage.sprite = gameUnitsLogic.CanBuildProductionUnit(productionSlot.gameUnit.name) ? affordableSprite : unaffordableSprite;
    }

    public void SetActiveResource(string resourceName)
    {
        activeResource = resourceName;
    }
}
