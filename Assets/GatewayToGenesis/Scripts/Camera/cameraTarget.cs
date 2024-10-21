using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class cameraTarget : MonoBehaviour
{
 [SerializeField] Camera cam;   
 [SerializeField] Transform player;


 private void Update(){

    AimLogic();

 }

 private void AimLogic(){
    Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
    Vector3 targetPos = (mousePos + cam.transform.position)/2;
    Vector3 start=cam.transform.position;

   
    if(targetPos.x>=cam.transform.position.x+2 || targetPos.x<= cam.transform.position.x-2||targetPos.y>=cam.transform.position.y+1 || targetPos.y<= cam.transform.position.y-1){
        this.transform.position = Vector3.Lerp(start,targetPos,100.0f*Time.deltaTime); 
    }

    }
    
}

