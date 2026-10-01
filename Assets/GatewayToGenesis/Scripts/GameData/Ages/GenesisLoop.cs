using UnityEngine;

/// <summary>
/// Puts the first playable loop into the game scene without touching the scene file: once a scene with the calendar
/// (<see cref="TimeSystemLogic"/>) has loaded, one object carries the Ages (<see cref="AgeProgression"/>), the world
/// (<see cref="WorldSystem"/>), the legends' growth (<see cref="LegendProgress"/>) and their views (the Age banner and
/// the world view, the faces of the story being told). Scenes without a calendar (menus) are left alone.
/// </summary>
public static class GenesisLoop
{
    private static GameObject _host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_host != null || Object.FindAnyObjectByType<TimeSystemLogic>() == null) return;
        _host = new GameObject("Genesis Loop");
        _host.AddComponent<LegendProgress>();
        _host.AddComponent<AgeProgression>();
        _host.AddComponent<Pantry>();
        _host.AddComponent<CultureSystem>();
        _host.AddComponent<EdictSystem>();
        _host.AddComponent<WorldSystem>();
        _host.AddComponent<AgeBanner>();
        _host.AddComponent<TimeFlowHud>();
        _host.AddComponent<WorldView>();
        _host.AddComponent<EraScoreHud>();
        _host.AddComponent<CultureHud>();
        _host.AddComponent<NotificationFeed>();
        _host.AddComponent<BalladActorsView>();
        _host.AddComponent<BalladJournalView>();
        _host.AddComponent<QuickActionsHud>();
        _host.AddComponent<UiNavigation>();
        _host.AddComponent<Tutorials>();
        _host.AddComponent<PopulationHealth>();
        _host.AddComponent<SpeciesLoreKeeper>();
        _host.AddComponent<RumourKeeper>();
    }

    // Statics survive between play sessions when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { _host = null; UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded; }
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Bootstrap();
}
