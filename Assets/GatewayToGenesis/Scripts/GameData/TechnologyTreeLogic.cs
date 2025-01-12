using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameTechnologySlot;

public class TechnologyTreeLogic : MonoBehaviour
{
    [SerializeField] private GameObject emptySlotPrefab, slots;
    [SerializeField] private List<TechnologyData> eraTechnologies = new List<TechnologyData>();

    private TabBuilderLogic tabBuilderLogic;

    private void Start()
    {
        tabBuilderLogic = GetComponentInParent<TabBuilderLogic>();

        if (!tabBuilderLogic) Debug.LogError("TabBuilderLogic component not found in parent");

        InitializeTree(eraTechnologies);

        StartCoroutine(InitialVisibilityRefresh());
    }

    public void InitializeTree(List<TechnologyData> technologies)
    {
        // VFX LOGIC HERE

        BuildTechTree(technologies);
    }

    private void BuildTechTree(List<TechnologyData> technologies)
    {
        foreach (var techData in technologies)
        {
            if (techData == null) 
            { 
                Instantiate(emptySlotPrefab, slots.transform);

                continue;
            }

            tabBuilderLogic.AddNewUnit(techData.gameUnit);

            GameObject techSlotObj = slots.transform.Find(techData.gameUnit.name)?.gameObject;

            if (techSlotObj == null) 
            {
                Debug.LogError($"Slot for {techData.name} not found in the hierarchy.");

                continue;
            }

            var techSlot = techSlotObj.GetComponent<GameTechnologySlot>();

            if (techSlot != null) DetermineTechnologyVisibility(techSlot);
        }

        DetermineTechnologyVisibilityForAllSlots();
    }

    public void DetermineTechnologyVisibility(GameTechnologySlot techSlot)
    {
        if (techSlot?.technologyData == null) return;

        TechnologyData techData = techSlot.technologyData;

        // Handle technologies that are already unlocked
        if (techSlot.isUnlocked)
        {
            techSlot.techState = TechnologyState.Unlocked;
            HandleSlotVisibilityState(techSlot);
            return;
        }

        // Check for prerequisites
        bool hasPrerequisites = techData.techRequirements.Count > 0;
        bool allPrerequisitesUnlocked = true;
        bool anyPrerequisiteCurrentlyResearching = false;

        // Iterate over prerequisites and check their statuses
        foreach (var requiredTech in techData.techRequirements)
        {
            Transform requiredTechTransform = slots.transform.Find(requiredTech);

            if (requiredTechTransform == null)
            {
                allPrerequisitesUnlocked = false;
                continue;
            }

            var requiredTechSlot = requiredTechTransform.GetComponent<GameTechnologySlot>();

            if (requiredTechSlot == null)
            {
                allPrerequisitesUnlocked = false;
                continue;
            }

            if (!requiredTechSlot.isUnlocked) allPrerequisitesUnlocked = false;

            if (requiredTechSlot.techState == TechnologyState.CurrentResearchOption) anyPrerequisiteCurrentlyResearching = true;
        }

        // Reveal early when enlightened
        if (techSlot.enlightenedCompleted)
        {
            techSlot.techState = TechnologyState.NextResearchOption;
        }

        // Transition to CurrentResearchOption when all prerequisites met
        else if (allPrerequisitesUnlocked)
        {
            techSlot.techState = TechnologyState.CurrentResearchOption;
        }
        else if (hasPrerequisites)
        {
            techSlot.techState = anyPrerequisiteCurrentlyResearching ? TechnologyState.NextResearchOption : TechnologyState.Invisible;
        }
        else
        {
            techSlot.techState = TechnologyState.CurrentResearchOption;
        }

        // Transition Enlightened tech back to CurrentResearchOption
        if (allPrerequisitesUnlocked && techSlot.techState == TechnologyState.NextResearchOption)
        {
            techSlot.techState = TechnologyState.CurrentResearchOption;
        }

        HandleSlotVisibilityState(techSlot);
    }



    private void HandleSlotVisibilityState(GameTechnologySlot techSlot)
    {
        bool isActive = techSlot.techState != TechnologyState.Invisible;

        bool isUnavailable = techSlot.techState == TechnologyState.NextResearchOption;

        techSlot.displayComponent.SetActive(isActive);
        techSlot.unavailableFilter.SetActive(isUnavailable);
    }

    public void DetermineTechnologyVisibilityForAllSlots()
    {
        foreach (Transform slotTransform in slots.transform)
        {
            var techSlot = slotTransform.GetComponent<GameTechnologySlot>();

            if (techSlot?.technologyData != null) DetermineTechnologyVisibility(techSlot);
        }
    }

    private IEnumerator InitialVisibilityRefresh()
    {
        yield return null; // Wait one frame

        DetermineTechnologyVisibilityForAllSlots();
    }
}