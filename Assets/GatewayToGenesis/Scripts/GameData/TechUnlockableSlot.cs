using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TechUnlockableSlot : MonoBehaviour, ITooltipSource
{
    public Image slot, techIcon;

    public GameTechnologySlot technologySlot;
    public GameProductionSlot productionSlot;
    public TechUnlockable techUnlockableData;

    public ProductionUnitData productionUnitData;

    public void InitializeTechUnlockable(TechUnlockable unlockableData)
    {
        slot.sprite = unlockableData.slotImage;

        techUnlockableData = unlockableData;

        // Cards about no particular unit (Special hooks, a council seat, a demand modifier) show their slot image alone.
        if (unlockableData.unlockableType != TechUnlockableType.Special && unlockableData.gameUnit != null)
        {
            techIcon.sprite = unlockableData.gameUnit.icon;

            productionUnitData = GetProductionUnitDataFromGameUnit(unlockableData.gameUnit);
        }
        else
        {
            techIcon.enabled = false;
        }
    }
    public ProductionUnitData GetProductionUnitDataFromGameUnit(GameUnit gameUnit)
    {
        return gameUnit != null && GameCatalog.ProductionUnits.TryGet(gameUnit.name, out var data) ? data : null;
    }

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => TooltipContent.Unlockable(techUnlockableData, data);
}