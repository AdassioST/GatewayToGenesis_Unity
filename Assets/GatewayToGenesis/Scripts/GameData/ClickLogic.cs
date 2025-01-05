using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClickLogic : MonoBehaviour
{
    public GameUnitsLogic gameUnitsLogic;

    public string activeResource = "Duskstone";

    public void OnButtonClick()
    {
        if (gameUnitsLogic != null)
        {
            gameUnitsLogic.ChangeResourceFromName(activeResource, 0, true);
        }
    }
}
