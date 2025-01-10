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

    [SerializeField] GameObject unavailableFilter;

    // Cooldown variables for UnlockTechnology only
    private float unlockSwapCooldown = 1.0f;  // Cooldown time (seconds)
    private float lastClickTime = 0f;          // Last click time to track cooldown
    private bool isCooldownActive = false;    // Flag to track if cooldown is active

    private void Start()
    {
        if (gameUnitsLogic == null)
        {
            gameUnitsLogic = FindObjectOfType<GameUnitsLogic>();
        }

        buttonImage = GetComponent<Image>();

        productionSlot = GetComponent<GameProductionSlot>();
        technologySlot = GetComponent<GameTechnologySlot>();
    }

    private void Update()
    {
        RefreshTechSlotAppearance();
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
    }

    private void RefreshButtonAppearance()
    {
        if (currentMode != ClickMode.BuildProductionUnit || productionSlot == null || buttonImage == null)
            return;

        bool canAfford = gameUnitsLogic.CanBuildProductionUnit(productionSlot.gameUnit.name);
        buttonImage.sprite = canAfford ? affordableSprite : unaffordableSprite;
    }

    private void RefreshTechSlotAppearance()
    {
        if (currentMode != ClickMode.UnlockTechnology || technologySlot == null || technologySlot.isUnlocked || technologySlot.gameUnit == null)
            return;

        bool canAfford = gameUnitsLogic.CanUnlockTechnology(technologySlot.gameUnit.name);
        unavailableFilter.SetActive(!canAfford);
    }
    private void ResetCooldown()
    {
        if (Time.time - lastClickTime >= unlockSwapCooldown)
        {
            isCooldownActive = false;
        }
    }
}
