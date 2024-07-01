using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DataTracker : MonoBehaviour
{
    [SerializeField] GameData gameData;

    [SerializeField] private TMP_Text trackerText, statusText;

    private bool hasUpdateStatus;

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
        //Esto solo funciona para la comida, hay que hacer que todos tengan su update respectivo. Población, materiales para construir edificios, housing.

        //También falta hacer que el Click Power se refleje en el número del material activo. El material activo depende de los materiales que se necesitan para cada edificio. Por default es comida.

        trackerText.text = gameData.food + "/" + gameData.populationGrowthThreshhold;

        if (hasUpdateStatus)
        {
            //Solo Population no debería tener estado.

            statusText.text = gameData.foodStatus;
        }

    }
}
