using System.Collections;
using System.Collections.Generic;

public class ProductionEntity
{
    public string id { get; }
    public string name { get; }
    public float amount { get; set; }
    public Dictionary<string, float> baseCost { get; set; }
    public Dictionary<string, float> nextCost { get; set; } // The cost of the next building
    public Dictionary<string, float> inputResources {  get; set; } // val represent baseConsume
    public Dictionary<string, float> outputResources { get; set; } // val represent baseProduce

    public ProductionEntity(string id, string name, float amount, Dictionary<string, float> baseCost, Dictionary<string, float> inputResources, Dictionary<string, float> outputResources)
    {
        this.id = id;
        this.name = name;
        this.amount = amount;
        this.baseCost = baseCost;
        this.nextCost = baseCost;
        this.inputResources = inputResources;
        this.outputResources = outputResources;
    }
}
