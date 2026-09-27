using UnityEngine;

/// <summary>A legend's face in the ballad actors view: its tooltip is the legend's own.</summary>
public class LegendFace : MonoBehaviour, ITooltipSource
{
    public LegendData legend;

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => legend != null && TooltipContent.Legend(legend, data);
}
