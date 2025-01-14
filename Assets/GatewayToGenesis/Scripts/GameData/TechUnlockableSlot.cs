using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TechUnlockableSlot : MonoBehaviour
{
    public Image slot, techIcon;

    public void InitializeTechUnlockable(TechUnlockable unlockableData)
    {
        slot.sprite = unlockableData.slotImage;

        if (unlockableData.unlockableType != TechUnlockableType.Special)
        {
            techIcon.sprite = unlockableData.gameUnit.icon;
        }
        else
        {
            techIcon.enabled = false;
        }
    }
}