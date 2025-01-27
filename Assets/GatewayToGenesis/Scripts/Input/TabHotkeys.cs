using UnityEngine;

public class TabHotkeys : MonoBehaviour
{
    [SerializeField] private GameObject storageTab, productionTab, governmentTab, researchTab, HUD;

    [SerializeField] private GameObject[] tabs;

    public bool tabsDisabled;

    public void ToggleStorageTab()
    {
        if (tabsDisabled) return;

        ToggleTabDisplay(storageTab, allowIndependent: true);
    }

    public void ToggleProductionTab()
    {
        if (tabsDisabled) return;

        ToggleTabDisplay(productionTab, allowIndependent: true);
    }

    public void ToggleGovernmentTab()
    {
        if (tabsDisabled) return;

        ToggleTabDisplay(governmentTab, updateHUD: true);
    }

    public void ToggleResearchTab()
    {
        if (tabsDisabled) return;

        ToggleTabDisplay(researchTab, updateHUD: true);
    }

    private void ToggleTabDisplay(GameObject tab, bool updateHUD = false, bool allowIndependent = false)
    {
        Transform display = tab.transform.Find("Display");
        if (display == null)
        {
            Debug.LogError($"Tab {tab.name} does not have a 'Display' child object.");
            return;
        }

        TooltipSystemLogic.Instance.HideTooltip();

        bool isActive = display.gameObject.activeSelf;

        if (!isActive)
        {
            if (!allowIndependent)
            {
                ClearTabs();

            }
            display.gameObject.SetActive(true);

            if (updateHUD)
            {
                TimeSystemLogic.Instance.PauseTime(true); // Pause time when opening a tab that doesn't allow independence
                HUD.SetActive(false);
            }

            // Log for debugging and future VFX implementation
            if (allowIndependent && storageTab.transform.Find("Display").gameObject.activeSelf
                && productionTab.transform.Find("Display").gameObject.activeSelf)
            {
                //Debug.Log("Both StorageTab and ProductionTab are enabled.");
                // Add VFX or additional functionality here if both are active
            }
        }
        else
        {
            display.gameObject.SetActive(false);

            if (updateHUD)
            {
                TimeSystemLogic.Instance.PauseTime(false); // Resume time when closing the tab
                HUD.SetActive(true);
            }
        }
    }

    private void ClearTabs()
    {
        TooltipSystemLogic.Instance.HideTooltip();

        foreach (GameObject tab in tabs)
        {
            Transform display = tab.transform.Find("Display");
            if (display != null)
            {
                display.gameObject.SetActive(false);
            }
        }
    }
}
