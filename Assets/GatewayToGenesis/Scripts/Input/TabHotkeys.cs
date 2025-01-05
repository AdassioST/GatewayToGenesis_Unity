using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TabHotkeys : MonoBehaviour
{
    [SerializeField] private GameObject storageTab, productionTab, governmentTab, researchTab, HUD;

    [SerializeField] private GameObject[] tabs;

    public bool tabsDisabled;
    public void ToggleStorageTab()
    {
        if (tabsDisabled) return;

        if (storageTab.activeSelf == false) 
        { 
            storageTab.SetActive(true);

            //ADD HERE VFX IF PRODUCTION IS ALSO ENABLED
        }
        else
        {
            storageTab.SetActive(false);
        }

    }

    public void ToggleProductionTab()
    {
        if (tabsDisabled) return;

        if (productionTab.activeSelf == false)
        {
            productionTab.SetActive(true);

            //ADD HERE VFX IF STORAGE IS ALSO ENABLED
        }
        else
        {
            productionTab.SetActive(false);
        }

    }

    public void ToggleGovernmentTab()
    {
        if (tabsDisabled) return;

        if (governmentTab.activeSelf == false)
        {
            HUD.SetActive(false);

            ClearTabs();
            governmentTab.SetActive(true);
        }
        else
        {
            governmentTab.SetActive(false);

            ClearTabs();
            HUD.SetActive(true);
        }

    }

    public void ToggleResearchTab()
    {
        if (tabsDisabled) return;

        if (researchTab.activeSelf == false)
        {
            HUD.SetActive(false);

            ClearTabs();
            researchTab.SetActive(true);
        }
        else
        {
            researchTab.SetActive(false);

            ClearTabs();
            HUD.SetActive(true);
        }

    }

    private void ClearTabs()
    {
        foreach(GameObject tab in tabs)
        {
            tab.SetActive(false);
        }
    }
}
