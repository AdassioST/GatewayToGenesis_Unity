using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The pointer on a technology slot, beyond its button's left click (which researches or plans it, see
/// <see cref="GameUnitsLogic.PlanResearch"/>): hovering lights the way to it in the tree's lines
/// (<see cref="TechTreeConnectors"/>), and a right click takes it out of the research plan. Added by
/// <see cref="TechnologyTreeLogic"/>; a hidden technology takes no pointer at all.
/// </summary>
public class TechnologySlotPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private TechnologyTreeLogic _tree;
    private GameTechnologySlot _slot;

    public void Bind(TechnologyTreeLogic tree, GameTechnologySlot slot)
    {
        _tree = tree;
        _slot = slot;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tree != null && _slot != null && _slot.isVisible) _tree.SetHovered(_slot, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tree != null) _tree.SetHovered(_slot, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || _slot == null || !_slot.isVisible) return;
        var units = GameUnitsLogic.Instance;
        if (units != null) units.RemoveFromPlan(_slot);
    }

    private void OnDisable()
    {
        if (_tree != null) _tree.SetHovered(_slot, false);
    }
}
