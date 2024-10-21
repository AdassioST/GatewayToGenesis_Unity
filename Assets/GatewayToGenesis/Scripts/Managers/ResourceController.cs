using System.Collections;
using System.Collections.Generic;

public class ResourceController : ComponentCalculatorMediator
{
    public Dictionary<string, ResourceSO> resourcesSO;
    public Dictionary<string, Resource> resources;
    public Calculator calculator;

    public ResourceController(Calculator calculator)
    {
        this.calculator = calculator;
        this.resources = new Dictionary<string, Resource>();
    }
    public void UpdateClickPower(string resource_id)
    {
        if (resources.ContainsKey(resource_id))
        {
            Resource resource = resources[resource_id];
            resource.clickPower = calculator.CalculateClickPower(resource.baseClickPower);
            this.NotifyUpdatedClickPower(resource.id);
        }
    }

    public void UpdatePassiveGeneration(string resource_id, float amount)
    {
        if (resources.ContainsKey(resource_id))
        {
            Resource resource = resources[resource_id];
            resource.passiveGeneration = amount;
        }
    }

    public void UpdateAmount(string resource_id, float amount)
    {
        if (resources.ContainsKey(resource_id))
        {
            Resource resource = resources[resource_id];
            resource.amount = amount;
        }
    }

    private Resource GetResourceByID(string resource_id)
    {
        if (resources.ContainsKey(resource_id))
        {
            return resources[resource_id];
        }
        return null;
    }

    public void AddResource(string resourceID, Resource resource)
    {
        this.resources.Add(resourceID, resource);
    }

    public float GetResourceAmount(string resource_id)
    {
        return this.GetResourceByID(resource_id).amount;
    }

    public float GetBaseClickPower(string resource_id)
    {
        return this.GetResourceByID(resource_id).baseClickPower;
    }

    public float GetClickPower(string resource_id)
    {
        return this.GetResourceByID(resource_id).clickPower;
    }

    public float GetPassiveGeneration(string resource_id)
    {
        return this.GetResourceByID(resource_id).passiveGeneration;
    }

    private void NotifyUpdatedClickPower(string resource_id)
    {
        this._mediator.notify(this, "updated_click_power", resource_id);
    }
}
