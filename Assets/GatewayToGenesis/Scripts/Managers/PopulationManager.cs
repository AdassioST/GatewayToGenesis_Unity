using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class PopulationManager : MonoBehaviour
{

    public ResourceSO food;

    public ResourceSO pop;
    public ResourceSO housing;

    public int popInc;

    private int overflowMax=100;

    public TMP_Text clickPower;
    void Start(){
         InvokeRepeating("Starvation", 2.0f, 5.0f);
    }
    void Update(){
        AddPopulation();
        UpdateClickPower();
       
       
       
       
    }


 private void UpdateClickPower(){
        if(pop.amount==pop.threshhold){
            food.clickPower++;
            pop.threshhold = pop.threshhold*10;
        }
        clickPower.text = food.clickPower.ToString();
        food.addAmount = food.clickPower;
    }
        private void AddPopulation()
    {
        //A�adir el check de si hay casas libres. Si no hay disponibles y no se pueden vagabundos, no aumenta la poblaci�n. Si se puede, se a�aden como vagabundos, no como pop.
        //Cada pop utiliza 1 casa. Los vagabundos se convierten en pop cuando vuelve a haber casas disponibles cada cierto tiempo. Ej. 100 vagabundos se vuelven 10 pop cada 2 segundos por cada 10 casas libres.

        if (food.amount >= food.threshhold && housing.amount > 0)
        {
            pop.addResource();

            Debug.Log("Population is now: " + pop.amount);

            float foodOverflow = food.placeholder - pop.threshhold;

           FoodOverflow(foodOverflow);
            food.amount = food.amount + food.placeholder - food.threshhold;

            housing.removeResource(1);


        }
        else if(food.placeholder > food.threshhold && housing.amount == 0){
            float foodOverflow = food.placeholder - pop.threshhold;
            FoodOverflow(foodOverflow);
        }

       

    }

 

    private void Starvation(){
        if(food.amount==0){
        pop.removeResource(1);
        }
        
    }
    private void FoodOverflow(float foodOverflow){
         if (foodOverflow > 0)
            {
                food.placeholder = food.placeholder / 3 + Mathf.Clamp(foodOverflow, 0, overflowMax);

                Debug.Log("Food Overflow of: " + foodOverflow);

            }
            else
            {
                //Est� 3 para tener un fijo de comida del siguiente nivel sin entrar enseguida al status de starving

                food.placeholder = Mathf.Round(food.placeholder / 3);
            }

    }

    


    // Start is called before the first frame update
}
