using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    private Image buttonImage;

    private GameProductionSlot productionSlot;

    public GameTechnologySlot technologySlot;

    private void Start()
    {
        if (gameUnitsLogic == null)
        {
            gameUnitsLogic = FindAnyObjectByType<GameUnitsLogic>();
        }

        buttonImage = GetComponent<Image>();

        productionSlot = GetComponent<GameProductionSlot>();
        technologySlot = GetComponent<GameTechnologySlot>();
    }

    private void Update()
    {
        RefreshButtonAppearance();
    }

    public void OnButtonClick()
    {
        if (gameUnitsLogic == null)
        {
            Debug.LogError("GameUnitsLogic is not assigned in ", this);
            return;
        }

        switch (currentMode)
        {
            case ClickMode.AddResource:
                gameUnitsLogic.ChangeResourceFromName(activeResource, 0, true);
                break;

            case ClickMode.BuildProductionUnit:
                GameProductionSlot productionSlot = GetComponentInParent<GameProductionSlot>();
                string productionUnitName = productionSlot.gameUnit?.name;

                if (!string.IsNullOrEmpty(productionUnitName))
                {
                    gameUnitsLogic.BuildProductionUnit(productionUnitName);
                }
                break;

            case ClickMode.UnlockTechnology:
                GameTechnologySlot gameTechnologySlot = GetComponent<GameTechnologySlot>();

                if (!technologySlot.isUnlocked && !technologySlot.alreadyClicked)
                {
                    gameUnitsLogic.StartTechnologyProgress(technologySlot);
                }
                break;

            default:
                Debug.LogWarning("Unhandled ClickMode: " + currentMode);
                break;
        }
        
        // Ensure click power is maintained at minimum values after processing clicks
        if (currentMode == ClickMode.AddResource && !string.IsNullOrEmpty(activeResource))
        {
            var resourceSlot = gameUnitsLogic.GetResourceSlotFromName(activeResource);
            if (resourceSlot != null)
            {
                resourceSlot.EnsureMinimumClickPower();
            }
        }
    }

    private void RefreshButtonAppearance()
    {
        if (currentMode != ClickMode.BuildProductionUnit || productionSlot == null || buttonImage == null)
            return;

        bool canAfford = gameUnitsLogic.CanBuildProductionUnit(productionSlot.gameUnit.name);
        buttonImage.sprite = canAfford ? affordableSprite : unaffordableSprite;
    }

    public void SetActiveResource(string resourceName)
    {
        activeResource = resourceName;
    }
}
