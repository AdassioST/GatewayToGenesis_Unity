using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProductionLogic : MonoBehaviour
{
    private GameResourceSlot resourceSlot;

    void Start()
    {
        resourceSlot = GetComponent<GameResourceSlot>();
        InvokeRepeating("PassiveProduction", 0.1f, 1f);
    }

    void Update() { }

    private void PassiveProduction()
    {
        if (resourceSlot != null)
            ChangeResourceAmount(resourceSlot.productionRate);
    }

    public void ChangeResourceAmount(float amount)
    {
        if (resourceSlot != null)
        {
            resourceSlot.amount = Mathf.Clamp(resourceSlot.amount + amount, 0f, resourceSlot.maxStorage);
            resourceSlot.RefreshProductionAmount();
        }
    }
}
