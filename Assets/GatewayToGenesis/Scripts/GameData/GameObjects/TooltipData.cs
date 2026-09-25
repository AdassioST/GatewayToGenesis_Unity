/// <summary>How the tooltip box is laid out.</summary>
public enum TooltipStyle
{
    Standard,
    /// <summary>Wide box with a centred title and no type line (section banners).</summary>
    Banner
}

/// <summary>
/// The content of one tooltip, section by section; empty sections are hidden. Built fresh by
/// <see cref="TooltipTrigger.TryBuild"/> (custom text or an <see cref="ITooltipSource"/>) and shown by
/// <see cref="TooltipSlot"/>. Wording for game objects lives in <see cref="TooltipContent"/>.
/// </summary>
public class TooltipData
{
    public string title;
    public string description;
    public string type;
    /// <summary>Costs and "Requires:" lists.</summary>
    public string requirements;
    /// <summary>Production rate breakdown (resource slots).</summary>
    public string modifiers;
    /// <summary>Storage breakdown (resource slots) or any custom breakdown.</summary>
    public string breakdown;
    /// <summary>What the thing does: produced resources, bonuses, active modifiers.</summary>
    public string effects;
    /// <summary>Technologies that must be unlocked first, or allowed classes.</summary>
    public string prerequisites;
    public TooltipStyle style;

    public bool HasContent =>
        !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(description) || !string.IsNullOrEmpty(type) ||
        !string.IsNullOrEmpty(requirements) || !string.IsNullOrEmpty(modifiers) || !string.IsNullOrEmpty(breakdown) ||
        !string.IsNullOrEmpty(effects) || !string.IsNullOrEmpty(prerequisites);

    public void Clear()
    {
        title = description = type = requirements = modifiers = breakdown = effects = prerequisites = null;
        style = TooltipStyle.Standard;
    }
}

/// <summary>
/// A component that can word the tooltip of a <see cref="TooltipTrigger"/> on itself or on one of its
/// children. The trigger asks the nearest source first and walks up the hierarchy until one answers, so a
/// new kind of UI element gets tooltips by implementing this, without touching the trigger.
/// </summary>
public interface ITooltipSource
{
    /// <summary>Fill <paramref name="data"/> for <paramref name="trigger"/>; false when this source does not cover it.</summary>
    bool BuildTooltip(TooltipTrigger trigger, TooltipData data);
}
