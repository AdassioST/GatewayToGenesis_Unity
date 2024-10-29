using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private ResourceController rsController;
    private ProductionEntityController peController;
    private ResourceCalculator calculator;
    private ResourceAndProductionMediator calculatorMediator;


    // Start is called before the first frame update
    void Awake()
    {
        // Generate calculator
        this.calculator = new ResourceCalculator();
        //Generate controllers
        this.rsController = new ResourceController(calculator);
        this.peController = new ProductionEntityController();
        //Generate resources and pe
        Resource r1 = new Resource("R01", "Elderwood", "", 2000f, 4f, 0f);
        Resource r2 = new Resource("R02", "Duskstone", "", 7f, 3f, 0f);
        Resource r3 = new Resource("R03", "Glimmerfern", "", 0f, 2f, 0f);
        Resource r4 = new Resource("R04", "Emberwhisper", "", 0f, 1f, 0f);

        this.rsController.AddResource("R01", r1);
        this.rsController.AddResource("R02", r2);
        this.rsController.AddResource("R03", r3);
        this.rsController.AddResource("R04", r4);

        Dictionary<string, float> baseCost1 = new Dictionary<string, float>
        {
            {"R01", 125f}
        };
        Dictionary<string, float> inputResources1 = new Dictionary<string, float> 
        { 
            {"R02", 1f }
        };
        Dictionary<string, float> outputResources1 = new Dictionary<string, float>
        {
            {"R01", 3f}
        };
        ProductionEntity pe1 = new ProductionEntity("PE01", "Timber Camp", 0f, baseCost1, inputResources1, outputResources1);

        Dictionary<string, float> baseCost2 = new Dictionary<string, float>
        {
            {"R02", 75f}
        };
        Dictionary<string, float> inputResources2 = new Dictionary<string, float>
        {
            {"R02", 1f }
        };
        Dictionary<string, float> outputResources2 = new Dictionary<string, float>
        {
            {"R03", 3},
            {"R04", 1},
        };
        ProductionEntity pe2 = new ProductionEntity("PE02", "Moonlit Grove", 0f, baseCost2, inputResources2, outputResources2);

        this.peController.AddProductionEntity("PE01", pe1);
        this.peController.AddProductionEntity("PE02", pe2);

        //Generate mediator
        this.calculatorMediator = new ResourceAndProductionMediator(this.rsController, this.peController, this.calculator);

    }
    void Start()
    {
        StartCoroutine(Timer());
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            gusiClick("R01");
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            gusiClick("R02");
        }

        else if (Input.GetKeyDown(KeyCode.Y))
        {
            int randomNum = Random.Range(1,3);
            switch (randomNum)
            {
                case 1:
                    gusiPower("R01");
                    break;
                case 2:
                    gusiPower("R02");
                    break;
                case 3:
                    gusiPower("R03");
                    break;
                case 4:
                    gusiPower("R04");
                    break;
            }
        }

        else if (Input.GetKeyDown(KeyCode.P))
        {
            int randomNum = 1;
            switch (randomNum)
            {
                case 1:
                    gusiNewProductionEntity("PE01");
                    break;
                case 2:
                    gusiNewProductionEntity("PE02");
                    break;
            }
        }
    }

    void gusiClick(string resourceID)
    {
        var curr = this.rsController.GetResourceAmount(resourceID);
        var cp = this.rsController.GetClickPower(resourceID);
        var newVal = curr + cp;
        this.rsController.UpdateAmount(resourceID, newVal);
        var newAmm = this.rsController.GetResourceAmount(resourceID);
        Debug.Log($"ResourceID: {resourceID} Click power: {cp} Amount: {newAmm}");
    }

    void gusiPower(string resourceID)
    {
        var currCP = this.rsController.GetClickPower(resourceID);
        this.rsController.UpdateClickPower(resourceID);
        var newCP = this.rsController.GetClickPower(resourceID);
        Debug.Log($"ResourceID: {resourceID} Click power: {newCP}");
    }

    void gusiNewProductionEntity(string productionEntityID)
    {
        var currAmm = this.peController.GetProductionEntityAmount(productionEntityID);
        this.peController.AddProductionEntityAmount(productionEntityID);
        currAmm = this.peController.GetProductionEntityAmount(productionEntityID);
        Debug.Log($"Prod Ent: {productionEntityID} Amount: {currAmm}");

    }

    void gusiPassiveGeneration()
    {
        // Get Resources sorted by Passive Generation
        var resourcesList = this.rsController.resources.OrderBy(o => o.Value.passiveGeneration).ThenBy(o => o.Value.id).ToList();
        // Passive generation for all resources
        foreach (var resource in resourcesList)
        {
            bool inDisabled = this.peController.IsProductionEntityDisabledByResourceID(resource.Key);
            var pg = this.rsController.GetPassiveGeneration(resource.Key);
            if (pg == 0 && !inDisabled) { continue; }
            var curr = this.rsController.GetResourceAmount(resource.Key);
            var updateAmount = pg + curr;
            if (updateAmount < 0)
            {
                // Deactivate production entities
                this.peController.DeactivateProductionEntityByResource(resource.Key, pg, curr);
                // Calculate again update amount
                pg = this.rsController.GetPassiveGeneration(resource.Key);
                curr = this.rsController.GetResourceAmount(resource.Key);
                updateAmount = pg + curr;
            }
            else if (updateAmount > 0 && inDisabled)
            {
                this.peController.ActivateProductionEntityByResourceID(resource.Key, pg, curr);
            }
            if (updateAmount >= 0 && pg != 0)
            {
                Debug.Log($"Resource {resource.Key} Passive Gen: {pg} Prev amount: {curr} New amount: {updateAmount}");
                this.rsController.UpdateAmount(resource.Key, updateAmount);
            }
        }
    }

    private IEnumerator Timer()
    {
        while (true)
        {
            gusiPassiveGeneration();
            yield return new WaitForSeconds(5f);
        }
    }
}
