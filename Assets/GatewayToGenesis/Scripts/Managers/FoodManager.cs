using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FoodManager : MonoBehaviour
{
    public ResourceSO food;
    public ResourceSO pop;

    public NotificationsSO notification;
    float demand = 0f;
    float basedemand = 1f;
    float addModifiers = 1f;
    float sLevel = 1f;

    float productionMod=1;
    float baseInc =2.0f;
    float pMultipliers = 1.8f;

    float fIncrease;
    // Start is called before the first frame update
    void Start()
    {
        InvokeRepeating("foodDecrease", 2.0f, 1f);
        InvokeRepeating("foodIncrease", 2.0f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        notification.modifier=Mathf.Round((fIncrease-demand)*10f)*0.1f;
    }
    void foodDecrease(){


        demand = basedemand + addModifiers * Mathf.Exp((0.05f/sLevel)*pop.getAmount());
        if (food.getAmount()-demand > 0) {
            food.removeResource(demand);
        }
        else{
            food.setAmount(0);
        }
       
    }

    void foodIncrease(){

        if (food.getAmount()>0) {
        fIncrease = (baseInc +productionMod) * pMultipliers;
        food.setAmount(food.getAmount() + fIncrease);
        }
    }
  
}
