using System.Collections;
using System.Collections.Generic;

public class ResourcePassiveGenerationComparer : IComparer<Resource>
{
    public int Compare(Resource x, Resource y)
    {
        return x.passiveGeneration.CompareTo(y.passiveGeneration);
    }
}
public class Resource
{
    public string id { get; }
    public string name { get; }
    public string category { get; }
    public float amount { get; set; }
    public float baseClickPower { get; set; }
    public float clickPower { get; set; }
    public float passiveGeneration { get; set; }

    public Resource(string id, string name, string category, float amount, float baseClickPower, float passiveGeneration)
    {
        this.id = id;
        this.name = name;
        this.category = category;
        this.amount = amount;
        this.baseClickPower = baseClickPower;
        this.clickPower = baseClickPower;
        this.passiveGeneration = passiveGeneration;
    }
}
