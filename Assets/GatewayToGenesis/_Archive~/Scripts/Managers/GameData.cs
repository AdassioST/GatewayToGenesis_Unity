using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class GameData : MonoBehaviour
{
    [SerializeField] private int overflowMax = 100, baseHousing = 5, basePopulation = 1, baseClickPower = 1;

    private int housing, populationIncrease = 1;

    public int wood, food, freeHousing, population, populationGrowthThreshhold = 10, clickPower;

    private const string FOOD_TAG = "food", POPULATION_TAG = "pop", WOOD_TAG = "wood";

    public bool canHaveVagabonds;

    public ResourceSO resource;

    public string activeResourceTag = "food", foodStatus = "Stagnant";

    // Start is called before the first frame update
    void Start()
    {
        ResetToBaseResources();
    }

    // Update is called once per frame
    void Update()
    {
        UpdatePopulation();
        UpdateClickPower();
    }

    private void UpdatePopulation()
    {
        //A�adir el check de si hay casas libres. Si no hay disponibles y no se pueden vagabundos, no aumenta la poblaci�n. Si se puede, se a�aden como vagabundos, no como pop.
        //Cada pop utiliza 1 casa. Los vagabundos se convierten en pop cuando vuelve a haber casas disponibles cada cierto tiempo. Ej. 100 vagabundos se vuelven 10 pop cada 2 segundos por cada 10 casas libres.

        if (food >= populationGrowthThreshhold)
        {
            population += populationIncrease;

            Debug.Log("Population is now: " + population);

            int foodOverflow = food - populationGrowthThreshhold;

            if (foodOverflow > 0)
            {
                food = food / 3 + Mathf.Clamp(foodOverflow, 0, overflowMax);

                Debug.Log("Food Overflow of: " + foodOverflow);

            }
            else
            {
                //Est� 3 para tener un fijo de comida del siguiente nivel sin entrar enseguida al status de starving

                food = food / 3;
            }

        }

    }

    private void UpdateClickPower(){
        if(population==basePopulation*10){
            clickPower++;
            basePopulation=population;
        }
    }

    private void UpdateHousing(){
        if(wood>20){
            housing++;
            
        }

    }
    
    private void ResetToBaseResources()
    {
        food = 0; wood = 0;

        housing = baseHousing; 
        population = basePopulation;

        clickPower = baseClickPower;
    }

    public void AddResource(int amount, string resourceTag)
    {
        switch (resource.tag)
        {
            case FOOD_TAG:
                resource.addResource();

                break;

            case POPULATION_TAG:
                population += amount;

                break;

            case WOOD_TAG:
                wood += amount;

                break;


            default:
                Debug.LogWarning("Resource add came in but is not currently being handled " + resourceTag);
                break;
        }
    }

    public void AddActiveResource()
    {
        AddResource(clickPower, activeResourceTag);
    }
}
