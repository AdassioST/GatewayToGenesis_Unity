using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TechUnlockableSlot : MonoBehaviour
{
    public Image icon, techIcon;

    public void InitializeTechUnlockable(TechUnlockable unlockableData)
    {
        icon.sprite = unlockableData.icon;
        techIcon.sprite = unlockableData.gameUnit.icon;
    }
}
