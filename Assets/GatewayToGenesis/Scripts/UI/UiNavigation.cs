using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Tab enters UI navigation; arrows, submit and cancel remain owned by the EventSystem.</summary>
public sealed class UiNavigation : MonoBehaviour
{
    private InputAction tab, shift;
    private float nextRefresh;
    private readonly System.Collections.Generic.Dictionary<UnityEngine.UI.Selectable, UnityEngine.UI.Navigation> savedNavigation = new System.Collections.Generic.Dictionary<UnityEngine.UI.Selectable, UnityEngine.UI.Navigation>();
    private void Awake()
    {
        tab = new InputAction("UI focus", binding: "<Keyboard>/tab");
        shift = new InputAction("Reverse UI focus", binding: "<Keyboard>/shift");
        tab.performed += _ => Advance(shift.IsPressed() ? -1 : 1);
    }
    private void OnEnable() { tab?.Enable(); shift?.Enable(); }
    private void OnDisable() { tab?.Disable(); shift?.Disable(); RestoreNavigation(); }
    private void OnDestroy() { tab?.Dispose(); shift?.Dispose(); }
    private static bool Usable(UnityEngine.UI.Selectable selectable)
    {
        if (selectable.name == "Backdrop") return false;
        if (!selectable.IsActive() || (!selectable.IsInteractable() && selectable.GetComponent<TooltipTrigger>() == null) || selectable.navigation.mode == UnityEngine.UI.Navigation.Mode.None) return false;
        foreach (var group in selectable.GetComponentsInParent<CanvasGroup>())
        { if (!group.interactable || !group.blocksRaycasts || group.alpha < .01f) return false; if (group.ignoreParentGroups) break; }
        return selectable.transform is RectTransform rect && rect.rect.width > 0 && rect.rect.height > 0;
    }
    private static bool InOpenWindow(UnityEngine.UI.Selectable selectable)
    {
        var canvas = selectable.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        if (SaveMenu.BlocksGameplay) return canvas.name == "Menu Canvas";
        return (LibraryWindow.IsOpen && canvas.name == "Library Canvas")
            || (RumoursWindow.IsOpen && canvas.name == "Rumours Canvas")
            || (BattleWindow.IsOpen && canvas.name == "Battle Canvas")
            || (BattleEncounterWindow.IsOpen && (canvas.name == "Encounter Canvas" || canvas.name == "Performance Canvas"))
            || (BattleRecoveryWindow.IsOpen && canvas.name == "Recovery Canvas")
            || (SymphonyWindow.IsOpen && canvas.name == "Symphony Canvas")
            || (BestiaryWindow.IsOpen && canvas.name == "Bestiary Canvas")
            || (EraTimelineWindow.IsOpen && canvas.name == "Era Timeline Canvas")
            || (CultureWindow.IsOpen && canvas.name == "Culture Canvas")
            || (CultureAtlasWindow.IsOpen && canvas.name == "Atlas Canvas")
            || (CultureNamingDialog.IsOpen && canvas.name == "Culture Naming Canvas")
            || (RecipeInventionDialog.IsOpen && canvas.name == "Recipe Invention Canvas")
            || (BalladJournalView.IsOpen && canvas.name == "Ballads and Bonds")
            || (EdictsSection.IsOpen && selectable.GetComponentInParent<EdictsSection>() != null);
    }
    private static System.Collections.Generic.List<UnityEngine.UI.Selectable> Choices()
    {
        var choices = UnityEngine.UI.Selectable.allSelectablesArray.Where(Usable).ToList();
        if (OpenWindows.OverTheView || SaveMenu.BlocksGameplay)
        {
            choices.RemoveAll(s => !InOpenWindow(s));
            if (choices.Count > 0)
            {
                int top = choices.Max(s => s.GetComponentInParent<Canvas>()?.sortingOrder ?? 0);
                choices.RemoveAll(s => (s.GetComponentInParent<Canvas>()?.sortingOrder ?? 0) < top);
            }
        }
        return choices;
    }
    private void Update()
    {
        if (!(OpenWindows.OverTheView || SaveMenu.BlocksGameplay)) { if (savedNavigation.Count > 0) RestoreNavigation(); return; }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .25f;
        var events = EventSystem.current;
        if (events == null || KeyBindings.Busy) return;
        var choices = Choices();
        RestoreNavigation();
        for (int i = 0; i < choices.Count; i++)
        {
            savedNavigation[choices[i]] = choices[i].navigation;
            var previous = choices[(i + choices.Count - 1) % choices.Count];
            var next = choices[(i + 1) % choices.Count];
            var navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnLeft = previous, selectOnUp = previous, selectOnRight = next, selectOnDown = next };
            // Horizontal sliders keep Left/Right for changing their values; Up/Down moves focus.
            if (choices[i] is UnityEngine.UI.Slider) { navigation.selectOnLeft = null; navigation.selectOnRight = null; }
            choices[i].navigation = navigation;
        }
        var selected = events.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && choices.Any(s => s.gameObject == selected)) return;
        if (choices.Count > 0) events.SetSelectedGameObject(choices[0].gameObject);
    }
    private void RestoreNavigation()
    {
        foreach (var item in savedNavigation) if (item.Key != null) item.Key.navigation = item.Value;
        savedNavigation.Clear();
    }
    private void Advance(int direction)
    {
        var events = EventSystem.current;
        if (events == null || KeyBindings.Busy) return;
        var choices = Choices();
        if (choices.Count == 0) return;
        int current = choices.FindIndex(s => s.gameObject == events.currentSelectedGameObject);
        int next = current < 0 ? (direction > 0 ? 0 : choices.Count - 1) : (current + direction + choices.Count) % choices.Count;
        events.SetSelectedGameObject(choices[next].gameObject);
    }
}
