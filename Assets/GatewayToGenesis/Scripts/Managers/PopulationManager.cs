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

    public NotificationsSO vagabond;
    public NotificationsSO death;

    private int vagabondThreshold;

    public int activeVagabonds;

    public int popInc;

    private int overflowMax=100;

    public TMP_Text clickPower;
    void Start(){
         InvokeRepeating("Starvation", 2.0f, 5.0f);
    }
    void Update(){
        AddPopulation();
        UpdateClickPower();
        checkVagabonds();   
    }


 private void UpdateClickPower(){
        if(pop.getAmount()==pop.threshhold){
            food.setClickPower(food.getClickPower()+1);
            pop.threshhold = pop.threshhold*10;
        }
        clickPower.text = food.getClickPower().ToString();
        food.addAmount = food.getClickPower();
    }
        private void AddPopulation()
    {
        //A�adir el check de si hay casas libres. Si no hay disponibles y no se pueden vagabundos, no aumenta la poblaci�n. Si se puede, se a�aden como vagabundos, no como pop.
        //Cada pop utiliza 1 casa. Los vagabundos se convierten en pop cuando vuelve a haber casas disponibles cada cierto tiempo. Ej. 100 vagabundos se vuelven 10 pop cada 2 segundos por cada 10 casas libres.

        if (food.getAmount() >= food.threshhold && housing.getAmount() > 0 && activeVagabonds==0)
        {
            pop.addResource();

            Debug.Log("Population is now: " + pop.getAmount());

            float foodOverflow = food.placeholder - pop.threshhold;

           FoodOverflow(foodOverflow);
            food.setAmount(food.getAmount() + food.placeholder - food.threshhold); 

            housing.removeResource(1);


        }
        else if(food.placeholder > food.threshhold && housing.getAmount() == 0){

            float foodOverflow = food.placeholder - pop.threshhold;
            FoodOverflow(foodOverflow);
            
        }
        
    
            
    
    }
    private void Starvation(){
        if(food.getAmount()==0){
        pop.removeResource(1);
        death.setAmount(death.getAmount()+1);
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

    private void checkVagabonds(){
        vagabondThreshold=(int)pop.getAmount()/10;
        if(activeVagabonds < vagabondThreshold && housing.getAmount()==0 && food.getAmount() >= food.threshhold){
            float foodOverflow = food.placeholder - pop.threshhold;

           FoodOverflow(foodOverflow);
            food.setAmount(food.getAmount() + food.placeholder - food.threshhold); 
            Debug.Log("Vagabonds is now: " + activeVagabonds);
            activeVagabonds++;
        }
        else if(activeVagabonds !=0 && housing.getAmount() !=0){
            activeVagabonds--;
            pop.setAmount(pop.getAmount()+1);
            housing.removeResource(1);
        }
        vagabond.setAmount(activeVagabonds);
       
    }
    // Start is called before the first frame update
}
