using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A seat in the Government tab's seat pool (default seats and seats of active civics) that can replace the
/// selected position. Re-bound in place when the pool changes.
/// </summary>
public class CivicDetailedDisplay : MonoBehaviour, ITooltipSource
{
    [Header("UI References")]
    [SerializeField] private Image seatIcon;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI effectsText;
    [SerializeField] private TextMeshProUGUI leaderClassText;
    [SerializeField] private Button seatButton;

    private string _seatTitle;

    /// <summary>The player picked this seat for the selected position.</summary>
    public event Action<string> OnCivicClicked;

    private void Awake()
    {
        if (seatButton != null) seatButton.onClick.AddListener(OnSeatClicked);
    }

    private void OnDestroy()
    {
        if (seatButton != null) seatButton.onClick.RemoveListener(OnSeatClicked);
    }

    public void Bind(string seatTitle)
    {
        _seatTitle = seatTitle;
        if (string.IsNullOrEmpty(_seatTitle) || GovernmentLogic.Instance == null) return;

        var (title, effects, leaderClasses, icon) = GovernmentLogic.Instance.GetSeatDisplayInfo(_seatTitle);
        if (titleText != null) titleText.text = title;
        if (effectsText != null) effectsText.text = effects;
        if (leaderClassText != null) leaderClassText.text = leaderClasses;
        if (seatIcon != null) seatIcon.sprite = icon;
        if (seatButton != null) seatButton.interactable = true;
    }

    private void OnSeatClicked()
    {
        if (!string.IsNullOrEmpty(_seatTitle)) OnCivicClicked?.Invoke(_seatTitle);
    }

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => TooltipContent.SeatOption(_seatTitle, data);
}
