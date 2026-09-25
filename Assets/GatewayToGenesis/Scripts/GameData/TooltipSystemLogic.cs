using UnityEngine;
using DG.Tweening;

/// <summary>
/// Owns the one tooltip box. <see cref="TooltipTrigger"/>s ask to be shown on hover; the box is created once
/// and reused, fades in and out, and while it is open its content is rebuilt a few times a second (and at
/// once on <see cref="RefreshAllTooltips"/>), so live numbers stay current without every trigger polling.
/// A trigger that is disabled or destroyed while shown takes the box down with it.
/// </summary>
public class TooltipSystemLogic : SingletonBehaviour<TooltipSystemLogic>
{
    [SerializeField] private TooltipSlot tooltipSlotPrefab;
    [Tooltip("Seconds between content refreshes while a tooltip is open.")]
    [SerializeField] private float refreshInterval = 0.25f;

    private const float FadeInSeconds = 0.095f;
    private const float FadeOutSeconds = 0.075f;

    private TooltipSlot _slot;
    private CanvasGroup _slotGroup;
    private TooltipTrigger _current;
    private readonly TooltipData _data = new TooltipData();
    private float _nextRefresh;

    public bool isTooltipActive => _current != null;

    /// <summary>The trigger whose tooltip is open, if any.</summary>
    public TooltipTrigger Current => _current;

    private void Update()
    {
        if (_current == null) return;
        if (!_current.isActiveAndEnabled)
        {
            HideTooltip();
            return;
        }
        if (Time.unscaledTime >= _nextRefresh) Rebuild();
    }

    /// <summary>Show <paramref name="trigger"/>'s tooltip, replacing any other.</summary>
    public void Show(TooltipTrigger trigger)
    {
        if (trigger == null) return;
        if (trigger.TryBuild(_data)) ShowBuilt(trigger);
        else if (trigger == _current) HideTooltip();
    }

    // _data already holds trigger's content.
    private void ShowBuilt(TooltipTrigger trigger)
    {
        bool wasHidden = _current == null;
        _current = trigger;
        EnsureSlot();
        if (_slot == null) return;
        _slot.Show(_data);
        _nextRefresh = Time.unscaledTime + refreshInterval;
        if (wasHidden) FadeIn();
    }

    /// <summary>The pointer left <paramref name="trigger"/>: hide, or hand over to the trigger now under the pointer.</summary>
    public void Exit(TooltipTrigger trigger, GameObject nowHovered)
    {
        if (trigger != _current) return;
        var next = nowHovered != null ? nowHovered.GetComponentInParent<TooltipTrigger>() : null;
        if (next != null && next != trigger && next.TryBuild(_data))
        {
            ShowBuilt(next);
            return;
        }
        HideTooltip();
    }

    /// <summary>A trigger is going away; take its tooltip down.</summary>
    public void Release(TooltipTrigger trigger)
    {
        if (trigger == _current) HideTooltip();
    }

    public void HideTooltip()
    {
        _current = null;
        if (_slot == null || _slotGroup == null) return;
        DOTween.Kill(_slotGroup);
        var slot = _slot;
        _slotGroup.DOFade(0f, FadeOutSeconds).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(slot.gameObject)
            .OnComplete(() => { if (_current == null && slot != null) slot.gameObject.SetActive(false); });
    }

    /// <summary>Game state changed: rebuild the open tooltip now.</summary>
    public void RefreshAllTooltips()
    {
        if (_current != null) Rebuild();
    }

    /// <summary>Rebuild the open tooltip if it belongs to <paramref name="trigger"/>.</summary>
    public void RefreshIfShowing(TooltipTrigger trigger)
    {
        if (trigger != null && trigger == _current) Rebuild();
    }

    private void Rebuild()
    {
        _nextRefresh = Time.unscaledTime + refreshInterval;
        if (_current == null || _slot == null) return;
        if (_current.TryBuild(_data)) _slot.Show(_data);
        else HideTooltip();
    }

    private void EnsureSlot()
    {
        if (_slot == null)
        {
            if (tooltipSlotPrefab == null)
            {
                GameLog.Error("TooltipSystemLogic has no tooltip slot prefab assigned.", LogChannel.UI);
                return;
            }
            // Instantiated at the root and then parented keeping its world scale, as the box has always been sized.
            _slot = Instantiate(tooltipSlotPrefab);
            _slot.transform.SetParent(transform);
            _slotGroup = _slot.GetComponent<CanvasGroup>();
            if (_slotGroup == null) _slotGroup = _slot.gameObject.AddComponent<CanvasGroup>();
            _slotGroup.blocksRaycasts = false;
            _slotGroup.interactable = false;
            _slotGroup.alpha = 0f;
        }
        _slot.gameObject.SetActive(true);
        _slot.transform.SetAsLastSibling();
    }

    private void FadeIn()
    {
        DOTween.Kill(_slotGroup);
        _slotGroup.alpha = 0f;
        _slotGroup.DOFade(1f, FadeInSeconds).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(_slot.gameObject);
    }
}
