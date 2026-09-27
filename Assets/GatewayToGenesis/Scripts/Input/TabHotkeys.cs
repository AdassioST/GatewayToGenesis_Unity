using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Opens and closes the HUD tabs (bound to the tab buttons and hotkeys in the scene) and hides them while an
/// event is told. A tab is shown by fading the CanvasGroup on its "Display" child. Storage and production are
/// side panels that stay open beside anything; the other tabs are exclusive: opening one closes the others and
/// hides the HUD.
/// </summary>
public class TabHotkeys : SingletonBehaviour<TabHotkeys>
{
    private const LogChannel Log = LogChannel.UI;
    // The one by-name link to the tab prefabs (ARCHITECTURE.md §10): every tab keeps its panel under this child.
    private const string DisplayChildName = "Display";
    private const float VisibleAlpha = 0.1f;

    [Header("Tab References")]
    [SerializeField] private GameObject storageTab, productionTab, governmentTab, researchTab, HUD;
    [SerializeField] private GameObject eventTab; // Event overlay tab (acts like Government)

    [Tooltip("Extra tabs to manage. The tabs above are always managed, whether or not they are listed here.")]
    [SerializeField] private GameObject[] tabs;

    [Header("Animation Settings")]
    [SerializeField] private float tabFadeDuration = 0.1f;
    [SerializeField] private Ease tabFadeEase = Ease.OutQuad;
    [SerializeField] private float hudFadeDuration = 0.1f;
    [SerializeField] private Ease hudFadeEase = Ease.OutQuad;

    public bool tabsDisabled;

    // Each tab's panel, resolved once.
    private readonly Dictionary<GameObject, CanvasGroup> displays = new Dictionary<GameObject, CanvasGroup>();
    private readonly Dictionary<GameObject, bool> openBeforeEvent = new Dictionary<GameObject, bool>();
    private CanvasGroup hudGroup;
    private bool hudVisibleBeforeEvent;
    private bool isEventActive;

    /// <summary>True while an event story has the screen (tabs are hidden and cannot be toggled).</summary>
    public bool IsEventActive => isEventActive;

    /// <summary>
    /// The capital view's root canvases (those holding the HUD and the tabs), which the world view fades out while
    /// the world has the screen. The event overlay may sit inside one of them: see <see cref="EventOverlay"/>.
    /// </summary>
    public List<Canvas> CapitalCanvases()
    {
        var canvases = new List<Canvas>();
        foreach (var go in new[] { storageTab, productionTab, governmentTab, researchTab, HUD })
        {
            var canvas = go != null ? go.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) continue;
            canvas = canvas.rootCanvas;
            if (!canvases.Contains(canvas)) canvases.Add(canvas);
        }
        return canvases;
    }

    /// <summary>The event overlay, which stays visible over the world view (stories are told wherever the player is).</summary>
    public GameObject EventOverlay => eventTab;

    /// <summary>The capital's HUD (its NotificationGrid holds the notices, <see cref="NotificationFeed"/>).</summary>
    public GameObject Hud => HUD;

    protected override void OnSingletonAwake()
    {
        foreach (var tab in new[] { storageTab, productionTab, governmentTab, researchTab, eventTab }) Register(tab);
        if (tabs != null) foreach (var tab in tabs) Register(tab);
        if (HUD != null) hudGroup = GetOrAddCanvasGroup(HUD);
    }

    private void Start()
    {
        foreach (var group in displays.Values)
        {
            group.alpha = 0f;
            SetInteractive(group, false);
        }
        if (hudGroup != null)
        {
            hudGroup.alpha = 1f;
            SetInteractive(hudGroup, true);
        }
    }

    protected override void OnSingletonDestroy()
    {
        foreach (var group in displays.Values) if (group != null) group.DOKill();
        if (hudGroup != null) hudGroup.DOKill();
    }

    private void Register(GameObject tab)
    {
        if (tab == null || displays.ContainsKey(tab)) return;
        var display = tab.transform.Find(DisplayChildName);
        if (display == null)
        {
            GameLog.Warning($"Tab '{tab.name}' has no '{DisplayChildName}' child, so it can never be shown.", Log);
            return;
        }
        displays[tab] = GetOrAddCanvasGroup(display.gameObject);
    }

    // ===== Tab buttons and hotkeys =====

    public void ToggleStorageTab() => Toggle(storageTab, exclusive: false);

    public void ToggleProductionTab() => Toggle(productionTab, exclusive: false);

    public void ToggleGovernmentTab() => Toggle(governmentTab, exclusive: true);

    public void ToggleResearchTab() => Toggle(researchTab, exclusive: true);

    public void ToggleEventTab() => Toggle(eventTab, exclusive: true);

    /// <summary>Open the research tab (left open if it already is), e.g. from a notice.</summary>
    public void OpenResearchTab() { if (researchTab != null && !IsOpen(researchTab)) ToggleResearchTab(); }

    /// <summary>Open the government tab (left open if it already is), e.g. from a notice.</summary>
    public void OpenGovernmentTab() { if (governmentTab != null && !IsOpen(governmentTab)) ToggleGovernmentTab(); }

    private void Toggle(GameObject tab, bool exclusive)
    {
        // The White-Haven Library has the screen (and its search field the keyboard).
        if (tabsDisabled || isEventActive || LibraryWindow.IsOpen || WorldView.IsOpen || tab == null || !displays.ContainsKey(tab)) return;
        HideTooltip();

        if (IsOpen(tab))
        {
            FadeTab(tab, false, exclusive);
            return;
        }
        if (exclusive) CloseExclusiveTabs();
        FadeTab(tab, true, exclusive);
    }

    // The tab being opened hides the HUD when it finishes fading in, so the HUD is left alone here.
    private void CloseExclusiveTabs()
    {
        foreach (var tab in displays.Keys)
        {
            if (tab != storageTab && tab != productionTab) FadeTab(tab, false, ownsHUD: false);
        }
        SetTimePaused(false);
    }

    // ===== Events =====

    /// <summary>Remember which tabs are open, then hide them all and the HUD, and show the event tab.</summary>
    public void RememberTabStatesAndHideForEvent()
    {
        if (isEventActive) return;

        openBeforeEvent.Clear();
        foreach (var tab in displays.Keys)
        {
            if (tab != eventTab) openBeforeEvent[tab] = IsOpen(tab);
        }
        hudVisibleBeforeEvent = hudGroup != null && hudGroup.alpha > VisibleAlpha;

        SetHUDVisibility(false);
        foreach (var tab in openBeforeEvent.Keys) FadeTab(tab, false, ownsHUD: false);
        if (eventTab != null) FadeTab(eventTab, true, ownsHUD: false);

        isEventActive = true;
        tabsDisabled = true;
    }

    /// <summary>Hide the event tab and reopen exactly what was open before the event.</summary>
    public void RestoreTabStatesAfterEvent()
    {
        if (!isEventActive) return;

        foreach (var pair in openBeforeEvent) FadeTab(pair.Key, pair.Value, ownsHUD: false);
        if (eventTab != null) FadeTab(eventTab, false, ownsHUD: false);

        openBeforeEvent.Clear();
        isEventActive = false;
        tabsDisabled = false;

        // No tab that can be reopened pauses time (research and government never do).
        SetTimePaused(false);
        SetHUDVisibility(hudVisibleBeforeEvent);
    }

    /// <summary>Show the event tab over everything (called when an event starts from its notification).</summary>
    public void SwitchToEventTab()
    {
        if (eventTab == null) return;

        foreach (var tab in displays.Keys)
        {
            if (tab != eventTab) FadeTab(tab, false, ownsHUD: false);
        }
        FadeTab(eventTab, true, ownsHUD: true);
        SetHUDVisibility(false);
    }

    // ===== Fading =====

    private bool IsOpen(GameObject tab) => displays.TryGetValue(tab, out var group) && group.alpha > VisibleAlpha;

    /// <summary>
    /// Fade a tab in or out. An exclusive tab (<paramref name="ownsHUD"/>) hides the HUD once it is shown and
    /// brings it back once it is hidden; of those, only the event tab pauses time, so research keeps progressing.
    /// </summary>
    private void FadeTab(GameObject tab, bool show, bool ownsHUD)
    {
        if (!displays.TryGetValue(tab, out var group)) return;

        Fade(group, show, tabFadeDuration, tabFadeEase, () =>
        {
            if (!ownsHUD) return;
            if (!show) SetTimePaused(false);
            else if (tab != researchTab && tab != governmentTab) SetTimePaused(true);
            SetHUDVisibility(!show);
        });
    }

    private void SetHUDVisibility(bool visible)
    {
        if (hudGroup != null) Fade(hudGroup, visible, hudFadeDuration, hudFadeEase, null);
    }

    // A hidden panel stops taking clicks as soon as it starts fading out; a shown one once it is fully visible.
    private static void Fade(CanvasGroup group, bool show, float duration, Ease ease, Action onComplete)
    {
        group.DOKill();
        if (!show) SetInteractive(group, false);
        group.DOFade(show ? 1f : 0f, duration).SetEase(ease).OnComplete(() =>
        {
            if (show) SetInteractive(group, true);
            onComplete?.Invoke();
        });
    }

    private static void SetInteractive(CanvasGroup group, bool interactive)
    {
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        var group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    private static void SetTimePaused(bool paused)
    {
        var time = TimeSystemLogic.Instance;
        if (time != null) time.PauseTime(paused);
    }

    private static void HideTooltip()
    {
        var tooltips = TooltipSystemLogic.Instance;
        if (tooltips != null) tooltips.HideTooltip();
    }
}
