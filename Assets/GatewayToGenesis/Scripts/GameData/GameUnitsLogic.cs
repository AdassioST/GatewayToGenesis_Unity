using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameUnitsLogic : MonoBehaviour
{
    [SerializeField] private TabBuilderLogic storageTab;
    [SerializeField] private TabBuilderLogic productionTab;
    [SerializeField] private TabBuilderLogic researchTab;
    private void Awake()
    {

    }
    public void ChangeResourceFromName(string name, float amount, bool changeFromClickPower)
    {
        bool isUnitPresent = storageTab.slots.Any(r => r.name == name);

        if (isUnitPresent)
        {
            GameObject slot = storageTab.slots.Find((x) => x.name == name);

            if (changeFromClickPower)
            {
                amount = slot.GetComponent<GameResourceSlot>().clickPower;
            }

            slot.GetComponent<ProductionLogic>().ChangeResourceAmount(amount);
        }
        else
        {
            Debug.LogWarning($"Resource {name} is not in the resource storage slots");
        }

    }

    public void ChangeProductionUnitFromName(string name, float amount)
    {
        bool isUnitPresent = storageTab.slots.Any(r => r.name == name);

        if (isUnitPresent)
        {
            GameObject slot = storageTab.slots.Find((x) => x.name == name);

            slot.GetComponent<ProductionLogic>().ChangeResourceAmount(amount);
        }
        else
        {
            Debug.LogWarning($"Production Unit {name} is not in the production storage slots");
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("s"))
        {
            ChangeResourceFromName("Elderwood", 3, false);
        }
    }
}
