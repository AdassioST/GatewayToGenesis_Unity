using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class DataTracker : MonoBehaviour
{
   

    private bool hasUpdateStatus;

    public ResourceSO resource;


    public TMP_Text amount;
    public TMP_Text status;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        UpdateTrackerUI();
    }

    public void UpdateTrackerUI()
    {
       if(resource != null){
        amount.text = resource.setText();
       }
        //Esto solo funciona para la comida, hay que hacer que todos tengan su update respectivo. Poblaci�n, materiales para construir edificios, housing.
        //Tambi�n falta hacer que el Click Power se refleje en el n�mero del material activo. El material activo depende de los materiales que se necesitan para cada edificio. Por default es comida.
    }
}
