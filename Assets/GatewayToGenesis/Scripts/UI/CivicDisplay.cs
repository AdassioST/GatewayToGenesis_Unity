using UnityEngine;
using UnityEngine.UI;

/// <summary>An active civic on the Government tab (in its tier's row). Re-bound in place when the civics change.</summary>
public class CivicDisplay : MonoBehaviour, ITooltipSource
{
    [Header("UI References")]
    [SerializeField] private Image civicIcon;

    private CivicData _civic;

    public void Bind(CivicData civic)
    {
        _civic = civic;
        if (civicIcon != null && _civic != null) civicIcon.sprite = _civic.icon;
    }

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => TooltipContent.Civic(_civic, data);
}
