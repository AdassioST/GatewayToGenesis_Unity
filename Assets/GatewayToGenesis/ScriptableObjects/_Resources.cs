using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[CreateAssetMenu(fileName = "Resource", menuName = "Resource", order = 0)]
public class _Resources : ScriptableObject {
    public float amount;
    public int clickPower;

    public int threshhold;

    public int addAmount;

    public string tag;

    public string text="none";

    public void addResource() {
        amount=amount + addAmount;
    }
       
    
    private void removeResource(float subAmount){
        amount= amount -subAmount; 
    }

    public string setText(){
        if(tag=="food"){
             text = amount.ToString() + "/" + threshhold.ToString();
        }
        else{
        text = amount.ToString();
        }
        return text;

    }

    
}
