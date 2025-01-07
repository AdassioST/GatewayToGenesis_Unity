using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProductionLogic : MonoBehaviour
{
    private IGameUnitSlot unitSlot;

    void Start()
    {
        unitSlot = GetComponent<IGameUnitSlot>();

        if (unitSlot != null)
        {
            InvokeRepeating("PassiveProduction", 0.1f, 1f);
        }

    }

    void Update() { }

    private void PassiveProduction()
    {
        if (unitSlot is GameResourceSlot resourceSlot)
        {
            ChangeUnitAmount(resourceSlot.productionRate);
        }
    }

    public void ChangeUnitAmount(float amount)
    {
        if (unitSlot == null) return;

        if (unitSlot is GameResourceSlot resourceSlot)
        {
            //IF RESOURCE ADD MAX STORAGE
            resourceSlot.amount = Mathf.Clamp(resourceSlot.amount + amount, 0f, resourceSlot.maxAmount);
            resourceSlot.RefreshProductionAmount();
        }

        if (unitSlot is GameProductionSlot productionSlot)
        {
            unitSlot.maxAmount += amount;

            productionSlot.RefreshProductionAmount();
        }
    }

}
