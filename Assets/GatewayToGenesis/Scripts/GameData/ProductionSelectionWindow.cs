using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ProductionSelectionWindow : MonoBehaviour
{
    [Header("Slot Management")]
    [SerializeField] private Transform slotsParent; // The Content transform where slots are created

    private void Update()
    {
        // Close window if click is outside this window's RectTransform
        if (Input.GetMouseButtonDown(0))
        {
            RectTransform windowRect = GetComponent<RectTransform>();
            if (!RectTransformUtility.RectangleContainsScreenPoint(windowRect, Input.mousePosition, null))
            {
                gameObject.SetActive(false);
            }
        }
    }

    // Public method for Unity OnClick events
    public void OpenForBuildingMaterials()
    {
        RefreshSlots();
        gameObject.SetActive(true);
    }

    // Refresh slots for Building Materials only
    public void RefreshSlots()
    {
        if (slotsParent == null) return;

        ClearExistingSlots();
        CreateBuildingMaterialSlots();
    }

    private void ClearExistingSlots()
    {
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }
    }

    private void CreateBuildingMaterialSlots()
    {
        TabBuilderLogic storageTab = FindStorageTab();
        if (storageTab?.productionSelectionSlotPrefab == null) return;

        foreach (GameUnit unit in storageTab.units)
        {
            if (unit.type == "Building Material")
            {
                CreateSelectionSlot(unit, storageTab);
            }
        }
    }

    private TabBuilderLogic FindStorageTab()
    {
        TabBuilderLogic[] allTabs = FindObjectsByType<TabBuilderLogic>(FindObjectsSortMode.None);
        foreach (TabBuilderLogic tab in allTabs)
        {
            if (tab.tabType == TabBuilderLogic.TabType.Storage)
            {
                return tab;
            }
        }
        return null;
    }

    // Create a selection slot for a Building Material
    private void CreateSelectionSlot(GameUnit gameUnit, TabBuilderLogic storageTab)
    {
        GameObject newSlot = Instantiate(storageTab.productionSelectionSlotPrefab, slotsParent);
        newSlot.name = gameUnit.name;
        
        ProductionSelectionSlot selectionSlot = newSlot.GetComponent<ProductionSelectionSlot>();
        if (selectionSlot != null)
        {
            selectionSlot.InitializeSelectionSlot(gameUnit, "BuildingMaterial");
        }
    }

    // Create a slot for a new resource that was just added
    public void CreateSlotForNewResource(GameUnit newResource)
    {
        if (newResource.type == "Building Material")
        {
            TabBuilderLogic storageTab = FindStorageTab();
            if (storageTab?.productionSelectionSlotPrefab != null)
            {
                CreateSelectionSlot(newResource, storageTab);
            }
        }
    }
} 