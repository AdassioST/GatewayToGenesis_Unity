using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ResourceAndProductionMediatorComponent
{
    protected IResourceAndProductionMediator _mediator;

    public void SetMediator(IResourceAndProductionMediator mediator)
    {
        this._mediator = mediator;
    }
}

public interface IResourceAndProductionMediator
{
    void notify(ResourceAndProductionMediatorComponent component, string trigger, string item_id);
}

public class ResourceAndProductionMediator : IResourceAndProductionMediator
{

    private ResourceCalculator calculator;
    private ResourceController resourceController;
    private ProductionEntityController productionEntityController;

    public ResourceAndProductionMediator(ResourceController resourceController, ProductionEntityController productionEntityController, ResourceCalculator calculator)
    {
        this.calculator = calculator;
        this.resourceController = resourceController;
        this.resourceController.SetMediator(this);
        this.productionEntityController = productionEntityController;
        this.productionEntityController.SetMediator(this);
    }

    public void notify(ResourceAndProductionMediatorComponent sender, string trigger, string item_id)
    {
        if (trigger == "new_building")
        {
            // Manage events when a new bulding is added
            if (!this.CheckTransactionForNewProductionEntity(item_id))
            {
                Debug.Log("insufficient_resources");
                this.notify(sender, "insufficient_resources", item_id);
                return;
            }
            Debug.Log("new_building_built");
            reactOnNewProductionEntity(item_id);

        }
        else if (trigger == "updated_click_power")
        {
            // Manage events when click power was updated
            Debug.Log("updated_click_power");
        }
        else if (trigger == "insufficient_resources")
        {
            // Undo changes done when creating new bulding
            productionEntityController.SubstractProductionEntityAmount(item_id);
        }
        else if (trigger == "deactivation_production_entity")
        {
            // Recalculate passive generation of resources of this deactivated production entity
            Debug.Log("deactivation_production_entity");
            this.ModifyPassiveGenerationOfProductionEntity(item_id, true);
        }
        else if (trigger == "activation_production_entity")
        {
            Debug.Log("activation_production_entity");
            this.ModifyPassiveGenerationOfProductionEntity(item_id, false);
        }
    }

    private bool CheckTransactionForNewProductionEntity(string productionEntityID)
    {
        bool flag = true;
        // Get Price
        Dictionary<string, float> resourcesCost = this.productionEntityController.GetNextCost(productionEntityID);
        List<string> resourcesIDs = resourcesCost.Keys.ToList();
        // Get Resources amounts
        foreach (string resourceID in resourcesIDs)
        {
            float currResourceAmount = this.resourceController.GetResourceAmount(resourceID);
            Debug.Log($"Production: {productionEntityID} --> Resource {resourceID} Cost {resourcesCost[resourceID]} Current Amount: {currResourceAmount}");
            if ((currResourceAmount - resourcesCost[resourceID]) < 0) { flag = false; break; }
        }
        return flag;
    }

    private void ModifyPassiveGenerationOfProductionEntity(string productionEntityID, bool productionEntityDeactivated)
    {
        float currProductionEntityAmount = this.productionEntityController.GetProductionEntityAmount(productionEntityID);
        // Calculate input resources
        Dictionary<string, float> inputResources = this.productionEntityController.GetInputResources(productionEntityID);
        // Update passive generation of each input resource
        foreach (KeyValuePair<string, float> resource in inputResources)
        {
            string resourceID = resource.Key;
            float currentAmount = resourceController.GetPassiveGeneration(resourceID);
            float inputAmount = resource.Value;
            float finalAmount = productionEntityDeactivated ? currentAmount + inputAmount : currentAmount - inputAmount;
            if (finalAmount < 0 && !productionEntityDeactivated)
            {
                this.productionEntityController.AddProductionEntityToProductionEntityToDisable(productionEntityID, resourceID);
            }
            this.resourceController.UpdatePassiveGeneration(resourceID, finalAmount);
        }

        Dictionary<string, float> outputResources = this.productionEntityController.GetOutputResources(productionEntityID);
        // Update passive generation of each output resource
        foreach (KeyValuePair<string, float> resource in outputResources)
        {
            string resourceID = resource.Key;
            float currentAmount = resourceController.GetPassiveGeneration(resourceID);
            float outputAmount = resource.Value;
            float finalAmount = productionEntityDeactivated ? currentAmount - outputAmount : currentAmount + outputAmount;
            this.resourceController.UpdatePassiveGeneration(resourceID, finalAmount);
        }
    }

    private void reactOnNewProductionEntity(string productionEntityID)
    {
        float currProductionEntityAmount = this.productionEntityController.GetProductionEntityAmount(productionEntityID);
        // Update material amounts 
        // Get Cost
        Dictionary<string, float> resourcesCost = this.productionEntityController.GetNextCost(productionEntityID);
        List<string> resourcesIDs = resourcesCost.Keys.ToList();
        // Get Resources amounts
        foreach (string resourceID in resourcesIDs)
        {
            float newResourceAmount = this.resourceController.GetResourceAmount(resourceID) - resourcesCost[resourceID];
            Debug.Log($"Resource: {resourceID} Substract amount: {newResourceAmount}");
            this.resourceController.UpdateAmount(resourceID, newResourceAmount);
        }

        this.ModifyPassiveGenerationOfProductionEntity(productionEntityID, false);

        //Calculate next cost
        Dictionary<string, float> baseCost = this.productionEntityController.GetBaseCost(productionEntityID);
        Dictionary<string, float> newCost = this.calculator.CalculateNextCost(currProductionEntityAmount, baseCost);
        this.productionEntityController.UpdateNextCost(productionEntityID, newCost);

    }
}