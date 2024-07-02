
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HousingManage : MonoBehaviour
{
    // Start is called before the first frame update
   public ResourceSO housing;
   public ResourceSO wood;

    void Update(){
        UpdateHousing();

    }
   private void UpdateHousing(){
    housing.addAmount=7;
    if(wood.amount> 30){
        housing.addResource();
        wood.amount-=30;
    }
   }
}
