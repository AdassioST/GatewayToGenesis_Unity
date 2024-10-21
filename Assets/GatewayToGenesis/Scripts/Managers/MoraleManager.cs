using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoraleManager : MonoBehaviour
{

    [SerializeField] float _morale;
    // Start is called before the first frame update
    void Start()
    {
        InvokeRepeating("moraleDecrease", 2.0f, 1f);
        InvokeRepeating("moraleIncrease", 2.0f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(_morale);
    }

    void moraleIncrease(){
        if(_morale<100){
            _morale+=0.5f;
        }
    }

    void moraleDecrease(){
        if(_morale>100){
            _morale-=0.5f;
        }
    }

}
