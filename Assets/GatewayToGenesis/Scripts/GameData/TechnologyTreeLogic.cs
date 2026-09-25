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

        if (tabBuilderLogic == null)
        {
            GameLog.Error("TechnologyTreeLogic needs a TabBuilderLogic in its parents; the tree is not built.", LogChannel.Units);
            return;
        }

        InitializeTree(eraTechnologies);

        StartCoroutine(InitialVisibilityRefresh());
    }

    public void InitializeTree(List<TechnologyData> technologies)
    {
        BuildTechTree(technologies);
    }

    private void BuildTechTree(List<TechnologyData> technologies)
    {

        foreach (var techData in technologies)
        {
            if (techData == null || techData.gameUnit == null)
            {
                Instantiate(emptySlotPrefab, slots.transform);

                continue;
            }

            tabBuilderLogic.AddNewUnit(techData.gameUnit);

            GameObject techSlotObj = slots.transform.Find(techData.gameUnit.name)?.gameObject;

            if (techSlotObj == null) 
            {
                GameLog.Error($"Technology slot '{techData.gameUnit.name}' was not created under '{slots.name}'.", LogChannel.Units);

                continue;
            }

            var techSlot = techSlotObj.GetComponent<GameTechnologySlot>();
            if (techSlot == null) continue;

            // Each age has its own tree: the slot refreshes this one, not whichever tree a scene search finds.
            techSlot.technologyTreeLogic = this;
            DetermineTechnologyVisibility(techSlot);
        }

        DetermineTechnologyVisibilityForAllSlots();

        // The layout shows the second technology first.
        if (slots.transform.childCount >= 2) slots.transform.GetChild(1).SetAsFirstSibling();
    }

    public void DetermineTechnologyVisibility(GameTechnologySlot techSlot)
    {
        if (techSlot == null || techSlot.technologyData == null) return;

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

        // Reveal early when enlightened (always visible regardless of prerequisites)
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

        // Transition Enlightened tech to CurrentResearchOption if prerequisites become met later
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

            if (techSlot != null && techSlot.technologyData != null) DetermineTechnologyVisibility(techSlot);
        }
    }

    private IEnumerator InitialVisibilityRefresh()
    {
        yield return null; // Wait one frame

        DetermineTechnologyVisibilityForAllSlots();
    }
}