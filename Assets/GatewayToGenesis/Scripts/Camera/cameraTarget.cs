using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class cameraTarget : MonoBehaviour
{
 [SerializeField] Camera cam;   
 [SerializeField] Transform player;
 [SerializeField] float xThreshold;
 [SerializeField] float yThreshold;

 private void Update(){

    AimLogic();

 }

 private void AimLogic(){
    Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
    Vector3 targetPos = (mousePos + cam.transform.position)/2;
    Vector3 start=cam.transform.position;

    targetPos.x = Mathf.Clamp(targetPos.x, -xThreshold + player.position.x, xThreshold + player.position.x);
    targetPos.y = Mathf.Clamp(targetPos.y, -yThreshold + player.position.y,yThreshold +player.position.y);
    if(targetPos.x>=cam.transform.position.x+2 || targetPos.x<= cam.transform.position.x-2||targetPos.y>=cam.transform.position.y+1.5 || targetPos.y<= cam.transform.position.y-1.5){
        cam.transform.position = Vector3.Lerp(start,targetPos,2.0f*Time.deltaTime); 
    }

    }
    
}

