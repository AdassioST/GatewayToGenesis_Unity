using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Notification", menuName = "Notification", order = 0)]
public class NotificationsSO : ScriptableObject
{
    public int amount;

    public float modifier;
     public string text="none";

     public string tag = "";

    public int getAmount(){
        return amount;
    }

    public int setAmount(int amount){
        this.amount = amount;
        return amount;
    }

     public float getModifier(){
        return modifier;
    }

    public float setModifier(float modifier){
        this.modifier = modifier;
       
        return modifier;
    }

    public string setText(){
        if(tag=="modifier"){
            text = modifier.ToString();
        }
        else{
            text=amount.ToString();
        }
        return text;
    }
        
    
}

