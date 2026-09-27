using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Builds one HUD tab (Storage, Production or Technology): a section per GameUnit.section and a slot
/// per GameUnit. Slots are indexed by unit name; use <see cref="TryGetSlot{T}"/> instead of searching
/// <see cref="slots"/>.
/// </summary>
public class TabBuilderLogic : MonoBehaviour
{
    public List<GameUnit> units = new List<GameUnit>();
    public List<GameObject> sections = new List<GameObject>(), slots = new List<GameObject>();

    [SerializeField] private GameObject tabContent, newSectionPrefab, newSlotPrefab;

    [Header("Tab Configuration")]
    [SerializeField] public TabType tabType;

    [Header("HUD Production Selection (Storage Only)")]
    [SerializeField] public GameObject productionSelectionSlotPrefab;
    [SerializeField] public Transform hudProductionSelectionContent;

    public GameUnit initializationUnit;

    public bool hasInitializationUnit;

    public enum TabType
    {
        Storage,
        Production,
        Technology
    }

    /// <summary>Raised after a new unit's slot is created and registered.</summary>
    public event Action<GameUnit, GameObject> OnUnitAdded;

    private readonly Dictionary<string, GameObject> _slotByName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> _sectionByName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    private void Start()
    {
        if (hasInitializationUnit) AddNewUnit(initializationUnit);
        if (tabType == TabType.Storage) CreateHUDProductionSelectionSlotsForExistingResources();
    }

    public bool HasUnit(string unitName) => unitName != null && _slotByName.ContainsKey(unitName);

    public bool TryGetSlot(string unitName, out GameObject slot)
    {
        slot = null;
        return unitName != null && _slotByName.TryGetValue(unitName, out slot) && slot != null;
    }

    public bool TryGetSlot<T>(string unitName, out T component) where T : Component
    {
        component = null;
        return TryGetSlot(unitName, out var slot) && (component = slot.GetComponent<T>()) != null;
    }

    public T GetSlot<T>(string unitName) where T : Component => TryGetSlot<T>(unitName, out T component) ? component : null;

    /// <summary>Create the section and slot for <paramref name="unit"/> if missing. Returns the slot.</summary>
    public GameObject AddNewUnit(GameUnit unit)
    {
        if (unit == null)
        {
            GameLog.Warning($"{name}: tried to add a null GameUnit.", LogChannel.Units);
            return null;
        }
        if (TryGetSlot(unit.name, out var existing)) return existing;

        var section = GetOrCreateSection(unit.section);
        GameObject newSlot = Instantiate(newSlotPrefab);
        newSlot.name = unit.name;
        Transform slotsParent = section.transform.Find("Slots");
        newSlot.transform.SetParent(slotsParent != null ? slotsParent : section.transform, false);

        units.Add(unit);
        slots.Add(newSlot);
        _slotByName[unit.name] = newSlot;

        IGameUnitSlot slotComponent = newSlot.GetComponent<IGameUnitSlot>();
        if (slotComponent != null)
        {
            if (slotComponent is GameProductionSlot productionSlot) productionSlot.InitializeSlot(unit);
            else slotComponent.gameUnit = unit;

            RegisterWithProduction(slotComponent);

            if (slotComponent is GameResourceSlot resourceSlot)
            {
                var breakdown = GameUnitsLogic.Instance != null ? GameUnitsLogic.Instance.storageBreakdown : null;
                if (breakdown != null && !breakdown.ContainsKey(unit.name))
                {
                    breakdown[unit.name] = new Dictionary<string, float> { { "Base", resourceSlot.maxAmount } };
                }
                if (tabType == TabType.Storage)
                {
                    CreateHUDProductionSelectionSlot(unit);
                    ResourceSelectionWindow.NotifyResourceAdded(unit);
                }
            }
        }

        OnUnitAdded?.Invoke(unit, newSlot);
        return newSlot;
    }

    private GameObject GetOrCreateSection(string sectionName)
    {
        sectionName ??= string.Empty;
        if (_sectionByName.TryGetValue(sectionName, out var section) && section != null) return section;

        section = Instantiate(newSectionPrefab);
        section.name = sectionName;
        section.transform.SetParent(tabContent.transform);
        sections.Add(section);
        _sectionByName[sectionName] = section;

        var label = section.transform.Find("Banner/Name");
        if (label != null && label.TryGetComponent(out TextMeshProUGUI text)) text.text = sectionName;

        if (GameCatalog.Sections.TryGet(sectionName, out var sectionData))
        {
            var banner = section.transform.Find("Banner");
            if (banner != null && banner.TryGetComponent(out TooltipTrigger tooltipTrigger))
            {
                // The section's canon keyword adds its lore (the banner line already is its summary).
                tooltipTrigger.SetCustom(sectionData.title, sectionData.description, sectionData.name, layout: TooltipStyle.Banner,
                    keyword: "section:" + sectionData.name.ToLowerInvariant(), loreSummary: false);
            }
        }
        return section;
    }

    private static void RegisterWithProduction(IGameUnitSlot slotComponent)
    {
        var production = GlobalProductionManager.Instance;
        if (production == null) return;
        if (slotComponent is GameResourceSlot resourceSlot) production.AddResourceSlot(resourceSlot);
        else if (slotComponent is GameProductionSlot productionSlot) production.AddProductionSlot(productionSlot);
        else if (slotComponent is GameTechnologySlot technologySlot) production.AddTechnologySlot(technologySlot);
    }

    // ===== HUD PRODUCTION SELECTION (Storage tab only) =====

    private void CreateHUDProductionSelectionSlot(GameUnit gameUnit)
    {
        if (tabType != TabType.Storage || productionSelectionSlotPrefab == null || hudProductionSelectionContent == null) return;

        string targetButton = ResourceSelectionWindow.ClickButtonFor(gameUnit);
        if (targetButton == null || hudProductionSelectionContent.Find(gameUnit.name) != null) return;

        GameObject newSelectionSlot = Instantiate(productionSelectionSlotPrefab, hudProductionSelectionContent);
        newSelectionSlot.name = gameUnit.name;
        if (newSelectionSlot.TryGetComponent(out ProductionSelectionSlot selectionSlot))
        {
            selectionSlot.InitializeSelectionSlot(gameUnit, targetButton);
        }
    }

    private void CreateHUDProductionSelectionSlotsForExistingResources()
    {
        if (productionSelectionSlotPrefab == null || hudProductionSelectionContent == null) return;
        foreach (Transform child in hudProductionSelectionContent) Destroy(child.gameObject);
        foreach (GameUnit unit in units) CreateHUDProductionSelectionSlot(unit);
    }

    public Transform GetHUDProductionSelectionContent() => hudProductionSelectionContent;
}
