using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shows a tooltip while the pointer is over this element. The content is either the custom text set here
/// (in the inspector, or from code with <see cref="SetCustom"/>) or comes from the nearest
/// <see cref="ITooltipSource"/> on this object or a parent that covers it. An element whose "Display" child
/// is hidden (an invisible technology) shows nothing. <see cref="TooltipSystemLogic"/> keeps an open
/// tooltip up to date, so sources never push refreshes themselves.
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public bool useCustomTooltip, isBreakdownDisplay, isProductionModifiers;

    public string customTitle, customDescription, customType;
    public string customStorageBreakdown;
    public TooltipStyle style;

    private static readonly List<ITooltipSource> SourceBuffer = new List<ITooltipSource>();
    private Transform _display;
    private bool _displayLooked;
    // Set from code (SetCustom): the keyword whose lore a custom tooltip carries.
    private string _loreKeyword;
    private bool _loreSummary = true;

    /// <summary>The trigger on <paramref name="target"/>, added when missing.</summary>
    public static TooltipTrigger Ensure(GameObject target)
    {
        var trigger = target.GetComponent<TooltipTrigger>();
        return trigger != null ? trigger : target.AddComponent<TooltipTrigger>();
    }

    /// <summary>
    /// Fixed text for this element; an open tooltip for it updates at once. <paramref name="keyword"/> (a keyword id
    /// or word) adds that keyword's lore under the text (<see cref="Keywords.AddLore"/>); its summary is left out
    /// when <paramref name="loreSummary"/> is false (the element's own text already says it).
    /// </summary>
    public void SetCustom(string title, string description, string type = null, string breakdown = null, TooltipStyle layout = TooltipStyle.Standard,
        string keyword = null, bool loreSummary = true)
    {
        useCustomTooltip = true;
        customTitle = title ?? string.Empty;
        customDescription = description ?? string.Empty;
        customType = type ?? string.Empty;
        customStorageBreakdown = breakdown ?? string.Empty;
        isBreakdownDisplay = !string.IsNullOrEmpty(breakdown);
        style = layout;
        _loreKeyword = keyword;
        _loreSummary = loreSummary;
        var system = TooltipSystemLogic.Instance;
        if (system != null) system.RefreshIfShowing(this);
    }

    /// <summary>Fill <paramref name="data"/> for this element; false when there is nothing to show.</summary>
    public bool TryBuild(TooltipData data)
    {
        data.Clear();
        if (!IsDisplayed()) return false;
        data.style = style;

        if (useCustomTooltip)
        {
            data.title = customTitle;
            data.description = customDescription;
            data.type = customType;
            if (isBreakdownDisplay) data.breakdown = customStorageBreakdown;
            if (!string.IsNullOrEmpty(_loreKeyword)) Keywords.AddLore(data, _loreKeyword, _loreSummary);
            return data.HasContent;
        }

        for (var node = transform; node != null; node = node.parent)
        {
            node.GetComponents(SourceBuffer);
            foreach (var source in SourceBuffer)
            {
                if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                if (source.BuildTooltip(this, data)) return data.HasContent;
                data.Clear();
                data.style = style;
            }
        }
        return false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var system = TooltipSystemLogic.Instance;
        if (system != null) system.Show(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var system = TooltipSystemLogic.Instance;
        if (system != null) system.Exit(this, eventData.pointerCurrentRaycast.gameObject);
    }

    public void OnSelect(BaseEventData eventData) => TooltipSystemLogic.Instance?.ShowFocused(this);
    public void OnDeselect(BaseEventData eventData) => TooltipSystemLogic.Instance?.ReleaseFocus(this);

    private void OnDisable()
    {
        var system = TooltipSystemLogic.Instance;
        if (system != null) system.Release(this);
    }

    private bool IsDisplayed()
    {
        if (!_displayLooked)
        {
            _display = transform.Find("Display");
            _displayLooked = true;
        }
        return _display == null || _display.gameObject.activeSelf;
    }
}
