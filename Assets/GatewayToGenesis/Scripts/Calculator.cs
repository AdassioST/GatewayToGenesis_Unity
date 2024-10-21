using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ComponentCalculatorMediator
{
    protected ICalculatorMediator _mediator;

    public void SetMediator(ICalculatorMediator mediator)
    {
        this._mediator = mediator;
    }
}

public interface ICalculatorMediator
{
    void notify(ComponentCalculatorMediator component, string trigger, string item_id);
}

public class WorkflowValidator
{
    private Calculator calculator;
    private ResourceController resource_controller;
    private ProductionEntityController production_entity_controller;

    public WorkflowValidator(ResourceController resourceController, ProductionEntityController productionEntityController, Calculator calculator)
    {
        this.resource_controller = resourceController;
        this.production_entity_controller = productionEntityController;
        this.calculator = calculator;
    }
    
    public bool CheckTransactionForNewProductionEntity(string productionEntityID)
    {
        bool flag = true;
        // Get Price
        Dictionary<string, float> resourcesCost = this.production_entity_controller.GetNextCost(productionEntityID);
        List<string> resourcesIDs = resourcesCost.Keys.ToList();
        // Get Resources amounts
        foreach (string resourceID in resourcesIDs)
        { 
            float currResourceAmount = this.resource_controller.GetResourceAmount(resourceID);
            Debug.Log($"Production: {productionEntityID} --> Resource {resourceID} Cost {resourcesCost[resourceID]} Current Amount: {currResourceAmount}");
            if ((currResourceAmount - resourcesCost[resourceID]) < 0) { flag = false; break; }
        }
        return flag;
    }
}

public class CalculatorMediator:ICalculatorMediator
{
    
    private Calculator calculator;
    private ResourceController resourceController;
    private ProductionEntityController productionController;
    private WorkflowValidator checker;

    public CalculatorMediator(ResourceController resourceController, ProductionEntityController productionEntityController, Calculator calculator)
    {
        this.calculator = calculator;
        this.resourceController = resourceController;
        this.resourceController.SetMediator(this);
        this.productionController = productionEntityController;
        this.productionController.SetMediator(this);
        this.checker = new WorkflowValidator(resourceController, productionEntityController, calculator);
    }

    public void notify(ComponentCalculatorMediator sender, string trigger, string item_id)
    {
        if (trigger == "new_building")
        {
            // Manage events when a new bulding is added
            if (!this.checker.CheckTransactionForNewProductionEntity(item_id))
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
            productionController.SubstractProductionEntityAmount(item_id);
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

    private void ModifyPassiveGenerationOfProductionEntity(string productionEntityID, bool productionEntityDeactivated)
    {
        float currProductionEntityAmount = this.productionController.GetProductionEntityAmount(productionEntityID);
        //Debug.Log($"PE {productionEntityID} Amount {currProductionEntityAmount}");
        // Calculate input resources
        Dictionary<string, float> inputResources = this.productionController.GetInputResources(productionEntityID);
        // Update passive generation of each input resource
        foreach (KeyValuePair<string, float> resource in inputResources)
        {
            string resourceID = resource.Key;
            float currentAmount = resourceController.GetPassiveGeneration(resourceID);
            float inputAmount = resource.Value;
            float finalAmount = productionEntityDeactivated ? currentAmount + inputAmount : currentAmount - inputAmount;
            if (finalAmount < 0 && !productionEntityDeactivated)
            {
                this.productionController.AddProductionEntityToProductionEntityToDisable(productionEntityID, resourceID);
            }
            //Debug.Log($"Resource {resourceID} New PG {finalAmount}");
            this.resourceController.UpdatePassiveGeneration(resourceID, finalAmount);
        }

        Dictionary<string, float> outputResources = this.productionController.GetOutputResources(productionEntityID);
        // Update passive generation of each output resource
        foreach (KeyValuePair<string, float> resource in outputResources)
        {
            string resourceID = resource.Key;
            float currentAmount = resourceController.GetPassiveGeneration(resourceID);
            float outputAmount = resource.Value;
            float finalAmount = productionEntityDeactivated ? currentAmount - outputAmount : currentAmount + outputAmount;
            //Debug.Log($"Resource {resourceID} New PG {finalAmount}");
            this.resourceController.UpdatePassiveGeneration(resourceID, finalAmount);
        }
    }

    private void reactOnNewProductionEntity(string productionEntityID)
    {
        float currProductionEntityAmount = this.productionController.GetProductionEntityAmount(productionEntityID);
        // Update material amounts 
            // Get Cost
        Dictionary<string, float> resourcesCost = this.productionController.GetNextCost(productionEntityID);
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
        Dictionary<string, float> baseCost = this.productionController.GetBaseCost(productionEntityID);
        Dictionary<string, float> newCost = this.calculator.CalculateNextCost(currProductionEntityAmount, baseCost);
        this.productionController.UpdateNextCost(productionEntityID, newCost);

    }
}

public class Calculator
{
    public Calculator() { }
    public float CalculateClickPower(float baseClickPower)
    {
        //TODO: Add Bonuses
        return 2 * baseClickPower;
    }

    public Dictionary<string, float> CalculateResourceFactorByBuildingCount(float productionEntityAmount, Dictionary<string, float> resourcesDict)
    {
        if (resourcesDict == null) { return null; }
        //TODO: Add bonuses
        foreach (string resourceID in resourcesDict.Keys.ToList())
        {
            resourcesDict[resourceID] *= productionEntityAmount;
        }
        return resourcesDict;
    }

    public Dictionary<string, float> CalculateNextCost(float productionEntityAmount, Dictionary<string, float> baseCost)
    {
        Dictionary<string, float> newCost = new Dictionary<string, float>();
        foreach (string resourceID in baseCost.Keys.ToList())
        {
            float newCostByResource = baseCost[resourceID] * (1 + Mathf.Exp((0.05f / 1) * productionEntityAmount));
            newCost.Add(resourceID, newCostByResource);
        }
        return newCost;
    }
}
