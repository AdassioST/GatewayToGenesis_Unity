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
        BuildProductionUnit
    }

    public ClickMode currentMode = ClickMode.AddResource;

    public string activeResource;

    [SerializeField] Sprite affordableSprite, unaffordableSprite;

    private Image buttonImage;
    private GameProductionSlot productionSlot;

    private void Start()
    {
        if (gameUnitsLogic == null)
        {
            gameUnitsLogic = FindObjectOfType<GameUnitsLogic>();
        }

        buttonImage = GetComponent<Image>();

        productionSlot = GetComponent<GameProductionSlot>();
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

            default:
                Debug.LogWarning("Unhandled ClickMode: " + currentMode);
                break;
        }
    }
    private void RefreshButtonAppearance()
    {
        if (currentMode != ClickMode.BuildProductionUnit || productionSlot == null || buttonImage == null)
            return;

        bool canAfford = gameUnitsLogic.CanBuildProductionUnit(productionSlot.gameUnit.name);

        buttonImage.sprite = canAfford ? affordableSprite : unaffordableSprite;
    }
}
