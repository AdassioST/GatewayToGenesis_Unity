using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One council position on the Government tab: the regular seats (spawned by <see cref="GovernmentTab"/>) and the
/// Head of State (placed in the scene). A view is bound once and re-bound in place whenever the council changes, so
/// it is never destroyed just to show new state.
///
/// Activity indicator: red = no legend, yellow = legend seated (the seat's bonuses already apply) but still
/// activating, green = the legend's own bonuses apply too (<see cref="CouncilRules"/>). Cooldown indicator: deep blue
/// while the legend cannot be changed, cyan when it can.
/// </summary>
public class SeatPositionDisplay : MonoBehaviour, ITooltipSource
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image spriteImage;
    [SerializeField] private Button seatButton;
    [SerializeField] private Image activeIndicator;
    [SerializeField] private Image cooldownIndicator;

    [Header("Default Assets")]
    [Tooltip("Portrait shown while no legend sits in the seat")]
    [SerializeField] private Sprite defaultIcon;

    [Header("Status Colors")]
    [SerializeField] private Color activeColor = Color.green;
    [Tooltip("Legend seated: the seat's bonuses apply, the legend's own are still activating")]
    [SerializeField] private Color activatingColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color inactiveColor = Color.red;
    [SerializeField] private Color readyColor = Color.cyan;
    [SerializeField] private Color cooldownColor = new Color(0f, 0f, 0.5f, 1f);

    private CouncilSeat _seat;
    private int _seatIndex;

    /// <summary>The player clicked this position (and its legend can be changed now).</summary>
    public event Action<int> OnSeatClicked;

    public CouncilSeat Seat => _seat;

    private void Awake()
    {
        if (seatButton != null) seatButton.onClick.AddListener(OnSeatButtonClicked);
    }

    private void OnDestroy()
    {
        if (seatButton != null) seatButton.onClick.RemoveListener(OnSeatButtonClicked);
    }

    /// <summary>Show <paramref name="seat"/> at position <paramref name="index"/> (Head of State = -1). Cheap: call it on every change.</summary>
    public void Bind(CouncilSeat seat, int index)
    {
        _seat = seat;
        _seatIndex = index;
        if (_seat == null) return;

        if (titleText != null) titleText.text = _seat.GetEffectiveTitle();
        var legend = _seat.assignedLegend;
        if (nameText != null) nameText.text = legend != null ? legend.legendName : "Unassigned";
        if (spriteImage != null) spriteImage.sprite = legend != null && legend.portrait != null ? legend.portrait : defaultIcon;

        if (activeIndicator != null)
        {
            activeIndicator.color = legend == null ? inactiveColor : _seat.IsActive() ? activeColor : activatingColor;
        }
        if (cooldownIndicator != null) cooldownIndicator.color = IsOnCooldown() ? cooldownColor : readyColor;
        if (seatButton != null) seatButton.interactable = _seat.isUnlocked;
    }

    private bool IsOnCooldown() => GovernmentLogic.Instance != null && !GovernmentLogic.Instance.CanChangeSeat(_seatIndex);

    private void OnSeatButtonClicked()
    {
        if (_seat == null || !_seat.isUnlocked) return;
        if (IsOnCooldown())
        {
            // The cooldown indicator and its tooltip tell the player how long is left.
            GameLog.Event($"{_seat.GetEffectiveTitle()} can change again in {GovernmentLogic.Instance.GetSeatCooldownRemaining(_seatIndex)} sevenths", LogChannel.GovernmentUI);
            return;
        }
        OnSeatClicked?.Invoke(_seatIndex);
    }

    /// <summary>
    /// The portrait shows the seated legend (or an assign prompt), the indicators explain themselves, and
    /// everything else on the seat shows the seat.
    /// </summary>
    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data)
    {
        if (_seat == null) return false;
        var target = trigger.gameObject;
        if (spriteImage != null && target == spriteImage.gameObject) return TooltipContent.SeatPortrait(_seat, data);
        if (activeIndicator != null && target == activeIndicator.gameObject) return TooltipContent.SeatActivity(_seat, data);
        if (cooldownIndicator != null && target == cooldownIndicator.gameObject) return TooltipContent.SeatCooldownIndicator(_seat, data);
        return TooltipContent.Seat(_seat, data);
    }
}
