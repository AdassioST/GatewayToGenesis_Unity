using System.Linq;
using UnityEngine;

/// <summary>The capital's optional screens share one ribbon instead of separate permanent launchers.</summary>
public sealed class QuickActionsHud : MonoBehaviour
{
    private QuickActionBar bar;
    private IconActionButton map, culture;
    private float refreshAt;
    private void Start()
    {
        var theme = CodeUI.Theme(nameof(QuickActionsHud));
        if (theme == null) { enabled = false; return; }
        var canvas = CodeUI.Canvas(transform, "Capital Quick Actions", 6, out var scaler);
        bar = QuickActionBar.Create(canvas.transform, theme, scaler);
        bar.Rect.anchoredPosition = new Vector2(24, 24);
        map = bar.Add("World", PixelIcon.Compass, WorldView.Toggle, "Open the world map. Explore, select a destination and give your parties orders. M toggles the map.", theme, true);
        culture = bar.Add("Culture", PixelIcon.Culture, () => CultureAtlasWindow.Open(), "Your cultural atlas: traditions, choices and history.", theme, true);
        bar.Add("Ballads & Bonds", PixelIcon.Music, BalladJournalView.ToggleJournal, "Ongoing ballads and the relationships between legends.", theme);
        bar.Add("Bestiary", PixelIcon.Beast, BestiaryWindow.Toggle, "Identified creatures and their known habitats.", theme, visible: () => BestiaryHud.Unlocked);
        bar.Add("Rumours", PixelIcon.Rumour, RumoursWindow.Toggle, "Follow travellers' rumours to discoveries on the map.", theme);
        bar.Add("Chronicle", PixelIcon.Chronicle, EraTimelineWindow.Toggle, "Your Era Score and the events that earned it.", theme);
        bar.Add("Library", PixelIcon.Book, LibraryWindow.Toggle, "Browse the White-Haven Library. L opens the library.", theme);
        bar.Reflow();
    }
    private void Update()
    {
        if (bar == null || Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .25f;
        bool show = !WorldView.IsOpen && !SaveMenu.BlocksGameplay && !OpenWindows.OverTheView && !(EventSystemLogic.Instance != null && EventSystemLogic.Instance.isEventActive);
        if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
        if (!show) return;
        bool unlocked = WorldSystem.Instance != null && WorldSystem.Instance.MapUnlocked;
        map.Button.interactable = unlocked;
        TooltipTrigger.Ensure(map.gameObject).SetCustom("World", unlocked ? "Open the world map (M). Select a settlement to form a party; select ground to choose a destination." : "Research the world-map technology to explore beyond the capital.");
        var system = CultureSystem.Instance;
        bool choice = system != null && (system.OpenDisputes().Any() || system.PendingTraditionChoices().Count > 0);
        culture.SetState(false, choice);
        TooltipTrigger.Ensure(culture.gameObject).SetCustom("Cultural atlas", CultureHud.Opportunity(system));
        bar.Reflow();
    }
}
