using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One legend in the Government tab's legend pool, offered for the selected seat. Re-bound in place when the pool
/// changes. The status line says whether picking it seats a free legend, moves or swaps a seated one, or does nothing.
/// </summary>
public class LeaderSlotDisplay : MonoBehaviour, ITooltipSource
{
    [Header("UI References")]
    [SerializeField] private Image leaderSprite;
    [SerializeField] private Button leaderButton;
    [Tooltip("Shown while the legend sits in any seat")]
    [SerializeField] private GameObject equippedIndicator;
    [SerializeField] private TMP_Text equippedText;

    private static readonly Color AvailableColor = Color.green;
    private static readonly Color AssignedColor = new Color(0.4f, 0.7f, 1f);
    private static readonly Color SwapColor = new Color(1f, 0.5f, 0f);

    private LegendData _legend;
    private int _targetSeatIndex;

    /// <summary>The player picked this legend for the selected seat.</summary>
    public event Action<LegendData> OnLeaderSelected;

    private void Awake()
    {
        if (leaderButton != null) leaderButton.onClick.AddListener(OnLeaderButtonClicked);
    }

    private void OnDestroy()
    {
        if (leaderButton != null) leaderButton.onClick.RemoveListener(OnLeaderButtonClicked);
    }

    /// <summary>Show <paramref name="legend"/> as a candidate for the seat at <paramref name="seatIndex"/> (Head of State = -1).</summary>
    public void Bind(LegendData legend, int seatIndex)
    {
        _legend = legend;
        _targetSeatIndex = seatIndex;
        if (_legend == null) return;

        if (leaderSprite != null) leaderSprite.sprite = _legend.portrait;
        var current = GovernmentLogic.Instance != null ? GovernmentLogic.Instance.GetSeatWithLegend(_legend.legendName) : null;
        bool seated = current != null;
        bool here = seated && current.seatIndex == _targetSeatIndex;

        if (equippedIndicator != null) equippedIndicator.SetActive(seated);
        if (equippedText != null)
        {
            equippedText.text = !seated ? "Available" : here ? "Currently Assigned" : $"Swap from {current.GetEffectiveTitle()}";
            equippedText.color = !seated ? AvailableColor : here ? AssignedColor : SwapColor;
        }
        if (leaderButton != null) leaderButton.interactable = true;
    }

    // GovernmentLogic.AssignLegendToSeat decides (and logs) whether the seat can change now.
    private void OnLeaderButtonClicked()
    {
        if (_legend != null) OnLeaderSelected?.Invoke(_legend);
    }

    public bool BuildTooltip(TooltipTrigger trigger, TooltipData data) => TooltipContent.Legend(_legend, data);
}
