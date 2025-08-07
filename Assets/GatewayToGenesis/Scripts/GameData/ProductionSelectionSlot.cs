using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProductionSelectionSlot : MonoBehaviour, IGameUnitSlot
{
    // INTERFACES
    public GameUnit gameUnit { get; set; }
    public float clickPower { get; set; } = 0.0f; // Not used for selection slots
    public float amount { get; set; } = 0.0f; // Not used for selection slots
    public float maxAmount { get; set; } = 0.0f; // Not used for selection slots

    // UI COMPONENTS
    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    // Reference to the target button's ClickLogic (BuildingMaterial or VitalResource)
    private ClickLogic targetClickLogic;
    private string targetButtonName; // "BuildingMaterial" or "VitalResource"

    private void Start()
    {   
        InitializeComponents();
        SetupButtonListener();
    }

    private void InitializeComponents()
    {
        if (icon == null)
        {
            icon = transform.Find("Icon")?.GetComponent<Image>();
        }
        
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void SetupButtonListener()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            Debug.LogError($"Button component not found on {gameObject.name}");
        }
    }

    // Initialize the slot with GameUnit data and specify the target button
    public void InitializeSelectionSlot(GameUnit newGameUnit, string targetButton = "BuildingMaterial")
    {
        gameUnit = newGameUnit;
        targetButtonName = targetButton;
        gameObject.name = newGameUnit.name;
        
        if (icon != null)
        {
            icon.sprite = newGameUnit.icon;
        }
        
        FindSpecificTargetClickLogic(targetButton);
    }

    private void FindSpecificTargetClickLogic(string buttonName)
    {
        Transform targetButton = FindButtonInHUD(buttonName);
        if (targetButton != null)
        {
            targetClickLogic = targetButton.GetComponent<ClickLogic>();
            targetButtonName = buttonName;
        }
        else
        {
            // HUD might be disabled (e.g., when technology unlocks while in a tab)
            // This is expected behavior, so we don't log an error
            targetClickLogic = null;
            targetButtonName = buttonName;
        }
    }

    private Transform FindButtonInHUD(string buttonName)
    {
        Transform hud = GameObject.Find("HUD")?.transform;
        if (hud == null) return null;

        Transform clickerUI = hud.Find("ClickerUI");
        if (clickerUI == null) return null;

        Transform mainButton = clickerUI.Find("MainButton");
        if (mainButton == null) return null;

        Transform clickButtons = mainButton.Find("ClickButtons");
        if (clickButtons == null) return null;

        return clickButtons.Find(buttonName);
    }

    // Called when the button is clicked
    private void OnButtonClicked()
    {
        if (gameUnit != null && targetClickLogic != null)
        {
            targetClickLogic.SetActiveResource(gameUnit.name);
        }
        else
        {
            Debug.LogWarning($"GameUnit: {gameUnit != null}, TargetClickLogic: {targetClickLogic != null}");
        }
    }

    // Alternative method that can be called from Unity Events
    public void OnSlotClicked()
    {
        OnButtonClicked();
    }

    // Public method for Unity Events (easier to find in dropdown)
    public void SelectThisResource()
    {
        OnButtonClicked();
    }

    // Separate method for Building Material selection
    public void SelectForBuildingMaterial()
    {
        FindSpecificTargetClickLogic("BuildingMaterial");
        OnButtonClicked();
    }

    // Separate method for Vital Resource selection
    public void SelectForVitalResource()
    {
        FindSpecificTargetClickLogic("VitalResource");
        OnButtonClicked();
    }
} 