using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[CreateAssetMenu(fileName = "Resource", menuName = "Resource", order = 0)]
public class ResourceSO : ScriptableObject 
{
    //Datos del Excel
    [SerializeField] int id = 1, tier = 1;

    public string resourceName = "", resourceType = "", resourceDescription = "";

    public bool hasSpecialProperties;

    //Datos del Gayvian

    [SerializeField] float amount;
    [SerializeField] int clickPower;

    public int threshhold;

    public int addAmount;

    public float placeholder;

    public string tag;

    public string text="none";

    public void addResource() {
        if(tag=="food"){
            placeholder=placeholder+addAmount;
             
        }
        amount=amount + addAmount;
    }
       
    
    public void removeResource(float subAmount){
        amount= amount -subAmount; 
    }

    public string setText(){
        if(tag=="food"){
             text = placeholder.ToString() + "/" + threshhold.ToString();
        }
        else{
        text = amount.ToString();
        }
        return text;

    }

    public float getAmount(){
        return amount;
    }
    public float setAmount(float amount){
        this.amount = amount;
        return amount;
    }

     public int getClickPower(){
        return clickPower;
    }
    public int setClickPower(int clickPower){
        this.clickPower =  clickPower;
        return clickPower;
        
    }
        
}


    
 
