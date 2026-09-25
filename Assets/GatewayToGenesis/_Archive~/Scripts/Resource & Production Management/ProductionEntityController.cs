using System.Collections;
using System.Collections.Generic;

public class ProductionEntityController : ResourceAndProductionMediatorComponent
{
    private class ProductionEntityDisableInfo
    {
        public string productionID { get; set; }
        public string resourceID { get; set; }

        public ProductionEntityDisableInfo(string productionID, string resourceID)
        {
            this.productionID = productionID;
            this.resourceID = resourceID;
        }
    }

    public Dictionary<string, ProductionEntitySO> productionsSO;
    private Dictionary<string, ProductionEntity> productionEntities;
    private List<ProductionEntityDisableInfo> productionEntitiesToDisable;
    private List<ProductionEntityDisableInfo> productionEntitiesDisabled;

    public ProductionEntityController()
    {
        this.productionEntities = new Dictionary<string, ProductionEntity>();
        this.productionEntitiesToDisable = new List<ProductionEntityDisableInfo>();
        this.productionEntitiesDisabled = new List<ProductionEntityDisableInfo>();
    }

    public void AddProductionEntityAmount(string productionEntityID)
    {
        ProductionEntity production = productionEntities[productionEntityID];
        production.amount += 1f;
        this.NotifyNewProductionEntity(production.id);
    }

    public void SubstractProductionEntityAmount(string productionEntityID)
    {
        ProductionEntity production = productionEntities[productionEntityID];
        production.amount -= 1f;
    }

    public void AddProductionEntity(string productionEntityID, ProductionEntity production)
    {
        this.productionEntities.Add(productionEntityID, production);
    }

    public float GetProductionEntityAmount(string productionEntityID)
    {
        return this.GetProductionEntityByID(productionEntityID).amount;
    }

    public Dictionary<string, float> GetBaseCost(string productionEntityID)
    {
        return this.GetProductionEntityByID(productionEntityID).baseCost;
    }

    public Dictionary<string, float> GetNextCost(string productionEntityID)
    {
        return this.GetProductionEntityByID(productionEntityID).nextCost;
    }

    public void UpdateNextCost(string productionEntityID,Dictionary<string, float> newCost)
    {
        if (productionEntities.ContainsKey((productionEntityID)))
        {
            ProductionEntity production = productionEntities[productionEntityID];
            production.nextCost = newCost;
        }
    }

    public Dictionary<string, float> GetInputResources(string productionEntityID)
    {
        return this.GetProductionEntityByID(productionEntityID).inputResources;
    }

    public Dictionary<string, float> GetOutputResources(string productionEntityID)
    {
        return this.GetProductionEntityByID(productionEntityID).outputResources;
    }

    public void AddProductionEntityToProductionEntityToDisable(string productionEntityID, string resourceID)
    {
        this.productionEntitiesToDisable.Add(new ProductionEntityDisableInfo(productionEntityID, resourceID));
    }

    public void DeactivateProductionEntityByResource(string resourceID, float currentPassiveGeneration, float currentResourceAmount)
    {
        float passiveGeneration = currentPassiveGeneration;
        int i = 0;
        List<int> indexesToRemove = new List<int>();
        foreach (var p in this.productionEntitiesToDisable)
        {
            if (p.resourceID == resourceID)
            {
                passiveGeneration += this.GetInputResources(p.productionID)[resourceID];
                this.SubstractProductionEntityAmount(p.productionID);
                this.NotifyDeactivationProductionEntity(p.productionID);
                this.productionEntitiesDisabled.Add(p);
                indexesToRemove.Add(i);
                i++;
                if ((currentResourceAmount + passiveGeneration) >= 0) { break; }
            }
        }
        this.RemoveElementsOfListFromIndexesList(this.productionEntitiesToDisable, indexesToRemove);
    }

    public void ActivateProductionEntityByResourceID(string resourceID, float currentPassiveGeneration, float currentResourceAmount)
    {
        int i = 0;
        List<int> indexesToRemove = new List<int>();
        float resourceAmount = currentResourceAmount;
        foreach (var p in this.productionEntitiesDisabled)
        {
            if (p.resourceID == resourceID)
            {
                float currAmount = this.GetProductionEntityAmount(p.productionID);
                float inputResourceAmount = this.GetInputResources(p.productionID)[p.resourceID];
                resourceAmount -= inputResourceAmount;
                this.UpdateProductionEntityAmount(p.productionID, currAmount + 1);
                this.NotifyActivationProductionEntity(p.productionID);
                indexesToRemove.Add(i);
                i++;
                if (resourceAmount <= 0) { break; }
            }
        }
        this.RemoveElementsOfListFromIndexesList(this.productionEntitiesDisabled, indexesToRemove);
    }

    public bool IsProductionEntityDisabledByResourceID(string resourceID)
    {
        foreach (var p in this.productionEntitiesDisabled)
        {
            if (p.resourceID == resourceID) { return true; }
        }
        return false;
    }

    private ProductionEntity GetProductionEntityByID(string productionEntityID)
    {
        return productionEntities[productionEntityID];
    }

    private void UpdateProductionEntityAmount(string productionEntityID, float amount)
    {
        ProductionEntity production = productionEntities[productionEntityID];
        production.amount = amount;
    }

    private void RemoveElementsOfListFromIndexesList<T>(List<T> originalList, List<int> indexesToRemove)
    {
        indexesToRemove.Sort((a, b) => b.CompareTo(a));
        foreach (var p in indexesToRemove)
        {
            originalList.RemoveAt(p);
        }
    }

    private void NotifyNewProductionEntity(string productionEntityID)
    {
        this._mediator.notify(this, "new_building", productionEntityID);
    }

    private void NotifyDeactivationProductionEntity(string productionEntityID)
    {
        this._mediator.notify(this, "deactivation_production_entity", productionEntityID);
    }

    private void NotifyActivationProductionEntity(string productionEntityID)
    {
        this._mediator.notify(this, "activation_production_entity", productionEntityID);
    }
}
