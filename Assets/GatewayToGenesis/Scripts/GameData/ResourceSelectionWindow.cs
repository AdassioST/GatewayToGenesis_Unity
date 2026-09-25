using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pop-up list of the resources one HUD click button can gather (Building Materials, Vital Resources).
/// Picking a slot points that button at the resource. The window rebuilds its slots when opened, adds one when
/// a matching resource is unlocked while it is open, and closes on a click outside it. Which button a resource
/// belongs to is decided once, by <see cref="ClickButtonFor"/>; a new kind of click button is a new subclass.
/// </summary>
public abstract class ResourceSelectionWindow : MonoBehaviour
{
    [Header("Slot Management")]
    [SerializeField] private Transform slotsParent; // The Content transform where slots are created

    /// <summary>HUD click button name ("BuildingMaterial", "VitalResource") this window feeds.</summary>
    protected abstract string TargetButton { get; }

    private static readonly List<ResourceSelectionWindow> OpenWindows = new List<ResourceSelectionWindow>();
    private RectTransform rectTransform;

    /// <summary>The HUD click button that gathers <paramref name="unit"/>, or null when no button does.</summary>
    public static string ClickButtonFor(GameUnit unit)
    {
        if (unit == null) return null;
        if (unit.type == "Building Material") return "BuildingMaterial";
        if (unit.type == "Vital Resource" || unit.role == ResourceRole.Food) return "VitalResource";
        return null;
    }

    /// <summary>A resource was unlocked: every open window it belongs to gets a slot for it.</summary>
    public static void NotifyResourceAdded(GameUnit unit)
    {
        foreach (var window in OpenWindows.ToArray())
        {
            if (window != null) window.CreateSlotForNewResource(unit);
        }
    }

    private void Awake() => rectTransform = GetComponent<RectTransform>();

    private void OnEnable() => OpenWindows.Add(this);

    private void OnDisable() => OpenWindows.Remove(this);

    private void Update()
    {
        if (InputUtils.LeftClickDown && rectTransform != null && !RectTransformUtility.RectangleContainsScreenPoint(rectTransform, InputUtils.MousePosition, null))
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>Rebuild the slots and show the window.</summary>
    public void Open()
    {
        RefreshSlots();
        gameObject.SetActive(true);
    }

    public void RefreshSlots()
    {
        if (slotsParent == null) return;
        foreach (Transform child in slotsParent) Destroy(child.gameObject);
        var storage = StorageTab();
        if (storage == null) return;
        foreach (var unit in storage.units)
        {
            if (Accepts(unit)) CreateSlot(unit, storage);
        }
    }

    /// <summary>Add a slot for a resource unlocked while the window is open.</summary>
    public void CreateSlotForNewResource(GameUnit unit)
    {
        if (slotsParent == null || !Accepts(unit) || slotsParent.Find(unit.name) != null) return;
        var storage = StorageTab();
        if (storage != null) CreateSlot(unit, storage);
    }

    private bool Accepts(GameUnit unit) => ClickButtonFor(unit) == TargetButton;

    private static TabBuilderLogic StorageTab()
    {
        var units = GameUnitsLogic.Instance;
        return units != null && units.storageTab != null && units.storageTab.productionSelectionSlotPrefab != null ? units.storageTab : null;
    }

    private void CreateSlot(GameUnit unit, TabBuilderLogic storage)
    {
        var slot = Instantiate(storage.productionSelectionSlotPrefab, slotsParent);
        slot.name = unit.name;
        if (slot.TryGetComponent(out ProductionSelectionSlot selection)) selection.InitializeSelectionSlot(unit, TargetButton);
    }
}
