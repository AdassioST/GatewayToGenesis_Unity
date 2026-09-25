using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Narrative event flow: decides when a story is offered, runs it, and applies its consequences.
///
///   time tick → <see cref="CheckForAvailableEvents"/> → notification (time slows) → player opens it →
///   <see cref="TriggerStory"/> (time pauses) → screens add consequences → <see cref="OnStoryCompleted"/>
///
/// Consequences that are modifiers (production %, click power) are applied through <see cref="EffectRouter"/>.
/// Permanent ones accumulate under "Event: {title}"; timed ones get their own source and are removed with
/// <see cref="EffectRouter.RemoveSource"/> when they expire, so expiry always restores the exact previous state.
/// </summary>
public class EventSystemLogic : SingletonBehaviour<EventSystemLogic>
{
    private const LogChannel Log = LogChannel.Events;
    private const string DarkMoraleScore = "dark_morale";

    [Header("Event Data")]
    [SerializeField] private List<EventScore> eventScores = new List<EventScore>();

    // Read on use: scene singletons register in Awake, in no guaranteed order.
    private static GameUnitsLogic GameUnits => GameUnitsLogic.Instance;
    private static StatManager Stats => StatManager.Instance;
    private static TimeSystemLogic TimeSystem => TimeSystemLogic.Instance;
    private static EventVolumeManager Volumes => EventVolumeManager.Instance;

    [Header("Event Notification System")]
    [SerializeField] private Transform eventsContainer;
    [SerializeField] private GameObject eventNotificationPrefab;

    [Header("Rules")]
    [Tooltip("Satisfaction lost when a story triggered by the dark_morale score completes.")]
    [SerializeField] private int darkMoraleSatisfactionPenalty = 15;
    [Tooltip("Seventh changes to wait after loading before any event may trigger.")]
    [SerializeField] private int warmupSevenths = 1;

    /// <summary>True while a story is being told (time is paused).</summary>
    public bool isEventActive { get; private set; }

    private StoryNode currentStoryNode;
    private string currentEventTriggerSource = "unknown";
    private GameObject currentNotification;
    private int seventhChangeCount;
    private int seventhsSinceLastEvent;

    // Sevenths since each story last completed, keyed by node name.
    private readonly Dictionary<string, int> eventCooldowns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    // Completions keyed by node name and by title, for WitnessEvent conditions and the "event_completed" value.
    private readonly Dictionary<string, int> completionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private readonly List<EventConsequence> cumulativeConsequences = new List<EventConsequence>();

    private class TimedEffect
    {
        public string source;
        public int remainingSevenths;
    }
    private readonly List<TimedEffect> activeTimed = new List<TimedEffect>();
    private int timedEffectCounter;

    private EventScreenManager screenManager;
    private bool subscribedToTime;

    // ===== LIFECYCLE =====

    protected override void OnSingletonAwake()
    {
        // Explicit == null throughout: Unity's fake-null objects defeat ?? and ?.
        screenManager = GetComponent<EventScreenManager>();
        if (screenManager == null) screenManager = gameObject.AddComponent<EventScreenManager>();
    }

    private void Start() => StartCoroutine(SubscribeWhenReady());

    private IEnumerator SubscribeWhenReady()
    {
        var wait = new WaitForSeconds(0.1f);
        while (!AreSystemsReady()) yield return wait;

        TimeSystem.OnSeventhChange += OnSeventhChanged;
        TimeSystem.OnPhaseChange += OnTimeMilestone;
        TimeSystem.OnEchoChange += OnTimeMilestone;
        TimeSystem.OnCycleChange += OnTimeMilestone;
        TimeSystem.OnRitualSeventh += OnTimeMilestone;
        subscribedToTime = true;
        GameLog.Event("Event system ready", Log);
    }

    protected override void OnSingletonDestroy()
    {
        if (!subscribedToTime || TimeSystem == null) return;
        TimeSystem.OnSeventhChange -= OnSeventhChanged;
        TimeSystem.OnPhaseChange -= OnTimeMilestone;
        TimeSystem.OnEchoChange -= OnTimeMilestone;
        TimeSystem.OnCycleChange -= OnTimeMilestone;
        TimeSystem.OnRitualSeventh -= OnTimeMilestone;
    }

    private bool AreSystemsReady() => PopGrowthLogic.Instance != null && GameUnits != null && Stats != null && TimeSystem != null && Volumes != null;

    // ===== TIME TRIGGERS =====

    private void OnSeventhChanged(int newSeventh)
    {
        seventhChangeCount++;
        if (!isEventActive) seventhsSinceLastEvent++;
        foreach (var key in eventCooldowns.Keys.ToList()) eventCooldowns[key]++;

        TickTimedEffects();
        if (seventhChangeCount > warmupSevenths) CheckForAvailableEvents();
    }

    // Phase, echo, cycle and ritual changes are further chances for a story to trigger.
    private void OnTimeMilestone(int _)
    {
        if (seventhChangeCount > warmupSevenths) CheckForAvailableEvents();
    }

    /// <summary>Offer the best available story as a notification (never more than one pending).</summary>
    private void CheckForAvailableEvents()
    {
        if (isEventActive || currentNotification != null || !AreSystemsReady()) return;
        var story = Volumes.FindBestAvailableStory();
        if (story == null) return;
        currentEventTriggerSource = DetermineEventTriggerSource(story);
        GameLog.Event($"Offering '{story.storyTitle}' (trigger: {currentEventTriggerSource})", Log);
        CreateEventNotification(story);
    }

    /// <summary>Check for events now (debug tools, scripted moments). Respects the start-up warm-up.</summary>
    public void TriggerEventCheck()
    {
        if (seventhChangeCount <= warmupSevenths)
        {
            GameLog.Event($"Event check skipped: still warming up ({seventhChangeCount}/{warmupSevenths} sevenths)", Log);
            return;
        }
        CheckForAvailableEvents();
        EnsureSlowMotionConsistency();
    }

    // ===== NOTIFICATIONS =====

    private void CreateEventNotification(StoryNode storyNode)
    {
        if (eventsContainer == null || eventNotificationPrefab == null)
        {
            GameLog.Warning("Event notification container or prefab is not assigned.", Log);
            return;
        }

        var notification = Instantiate(eventNotificationPrefab, eventsContainer);
        notification.name = $"EventNotification_{storyNode.storyTitle}";
        currentNotification = notification;
        notification.AddComponent<EventNotificationData>().storyNode = storyNode;

        var canvasGroup = notification.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = notification.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 1f).SetEase(Ease.OutQuad);

        var button = notification.GetComponentInChildren<UnityEngine.UI.Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => StartEventFromNotification(notification));
        }
        else
        {
            GameLog.Warning("The event notification prefab needs a Button to be opened.", Log);
        }

        TimeSystemLogic.Instance?.EnableSlowMotion();
    }

    /// <summary>Open the story behind a notification (its button calls this). A notification opens once.</summary>
    public void StartEventFromNotification(GameObject notification)
    {
        var data = notification != null ? notification.GetComponent<EventNotificationData>() : null;
        var storyNode = data != null ? data.storyNode : null;
        if (storyNode == null) return;
        data.storyNode = null; // a second click while it fades out does nothing
        var button = notification.GetComponentInChildren<UnityEngine.UI.Button>();
        if (button != null) button.interactable = false;

        if (currentNotification == notification) currentNotification = null;
        var canvasGroup = notification.GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.DOFade(0f, 0.5f).SetEase(Ease.InQuad).OnComplete(() => { if (notification != null) Destroy(notification); });
        else Destroy(notification);

        TriggerStory(storyNode);
        var hotkeys = TabHotkeys.Instance;
        if (hotkeys != null) hotkeys.SwitchToEventTab();
    }

    // Slow motion is on exactly while a notification waits and no story runs.
    private void EnsureSlowMotionConsistency()
    {
        var time = TimeSystemLogic.Instance;
        if (time == null) return;
        bool shouldBeSlow = currentNotification != null && !isEventActive;
        bool isSlow = time.GetEffectiveSecondsPerSeventh() > time.BaseSecondsPerSeventh;
        if (shouldBeSlow && !isSlow) time.EnableSlowMotion();
        else if (!shouldBeSlow && isSlow) time.DisableSlowMotion();
    }

    // ===== STORY FLOW =====

    /// <summary>Start telling a story: pauses time and hides the HUD tabs.</summary>
    public void TriggerStory(StoryNode storyNode)
    {
        if (storyNode == null || isEventActive || !AreSystemsReady())
        {
            GameLog.Event($"Cannot start '{storyNode?.storyTitle}': {(isEventActive ? "another event is active" : "systems not ready")}", Log);
            return;
        }
        currentStoryNode = storyNode;
        isEventActive = true;
        TimeSystem.PauseTime(true);
        var hotkeys = TabHotkeys.Instance;
        if (hotkeys != null) hotkeys.RememberTabStatesAndHideForEvent();

        // A story's own consequences (its "# consequences:" tag) apply however it ends.
        ClearCumulativeConsequences();
        if (storyNode.storyConsequences != null) foreach (var consequence in storyNode.storyConsequences) AddConsequence(consequence);

        Volumes.StartStory(storyNode, Volumes.GetCurrentVolume());
        GameLog.Event($"Story started: {storyNode.storyTitle}", Log);
    }

    /// <summary>Start a specific story immediately with a custom trigger label (tests, scripted moments).</summary>
    public void TriggerSpecificEvent(StoryNode storyNode, string triggerSource = "manual")
    {
        if (isEventActive || storyNode == null) return;
        currentEventTriggerSource = triggerSource;
        TriggerStory(storyNode);
    }

    public void ExecuteScreen(EventScreen screen) => screenManager.ShowScreen(screen);

    /// <summary>
    /// End the story: apply its consequences, resume time and restore the HUD. Reached through
    /// <see cref="EventVolumeManager.CompleteStory"/>; runs once per story.
    /// </summary>
    public void OnStoryCompleted()
    {
        if (!isEventActive && currentStoryNode == null)
        {
            GameLog.Warning("OnStoryCompleted called with no story running; ignored.", Log);
            return;
        }
        var story = currentStoryNode;
        if (screenManager != null) screenManager.HideCurrentScreen();

        if (story != null && IsEventTriggeredByDarkMorale(story) && darkMoraleSatisfactionPenalty != 0)
        {
            StatManager.Instance?.ChangeSatisfactionPoints(-Mathf.Abs(darkMoraleSatisfactionPenalty), $"Dark Morale Event: {story.storyTitle}");
        }

        string title = story != null ? story.storyTitle : "Event";
        foreach (var consequence in cumulativeConsequences) ApplyConsequence(consequence, title);
        ClearCumulativeConsequences();

        if (TimeSystem != null) TimeSystem.PauseTime(false);
        TimeSystemLogic.Instance?.DisableSlowMotion();
        var hotkeys = TabHotkeys.Instance;
        if (hotkeys != null) hotkeys.RestoreTabStatesAfterEvent();

        if (story != null)
        {
            eventCooldowns[story.nodeName ?? title] = 0;
            RecordCompletion(story.nodeName);
            if (!string.Equals(story.nodeName, story.storyTitle, StringComparison.OrdinalIgnoreCase)) RecordCompletion(story.storyTitle);
        }

        isEventActive = false;
        currentStoryNode = null;
        // The next seventh brings this back to 0.
        seventhsSinceLastEvent = -1;
        GameLog.Event($"Story completed: {title}", Log);
    }

    private void RecordCompletion(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        completionCounts.TryGetValue(key, out int count);
        completionCounts[key] = count + 1;
    }

    // ===== CONSEQUENCES =====

    /// <summary>Queue a consequence; everything queued is applied when the story completes.</summary>
    public void AddConsequence(EventConsequence consequence)
    {
        if (consequence == null) return;
        cumulativeConsequences.Add(consequence);
        GameLog.Event($"Queued consequence: {consequence.type} {consequence.targetName} {consequence.value}{(consequence.durationSevenths > 0 ? $" for {consequence.durationSevenths} sevenths" : "")}", Log);
    }

    public List<EventConsequence> GetCumulativeConsequences() => new List<EventConsequence>(cumulativeConsequences);

    public void ClearCumulativeConsequences() => cumulativeConsequences.Clear();

    /// <summary>
    /// Modifier consequences as effects. Production changes are % output of a resource or section;
    /// click power changes are flat or % of a resource or section.
    /// </summary>
    public static bool TryGetEffect(EventConsequence c, out GameEffect effect)
    {
        switch (c.type)
        {
            case EventConsequence.ConsequenceType.ProductionPercentChange:
                effect = new GameEffect(GameEffectType.ResourceModifier, c.value, ModifierType.Percentage, c.targetName, ScopeType.Individual);
                return true;
            case EventConsequence.ConsequenceType.ProductionPercentChangeSection:
                effect = new GameEffect(GameEffectType.ResourceModifier, c.value, ModifierType.Percentage, c.targetName, ScopeType.Section);
                return true;
            case EventConsequence.ConsequenceType.ClickPowerChange:
                effect = new GameEffect(GameEffectType.ClickPowerBonus, c.value, ModifierType.Add, c.targetName, ScopeType.Individual);
                return true;
            case EventConsequence.ConsequenceType.ClickPowerPercentChange:
                effect = new GameEffect(GameEffectType.ClickPowerBonus, c.value, ModifierType.Percentage, c.targetName, ScopeType.Individual);
                return true;
            case EventConsequence.ConsequenceType.ClickPowerChangeSection:
                effect = new GameEffect(GameEffectType.ClickPowerBonus, c.value, ModifierType.Add, c.targetName, ScopeType.Section);
                return true;
            case EventConsequence.ConsequenceType.ClickPowerPercentChangeSection:
                effect = new GameEffect(GameEffectType.ClickPowerBonus, c.value, ModifierType.Percentage, c.targetName, ScopeType.Section);
                return true;
            default:
                effect = default;
                return false;
        }
    }

    private void ApplyConsequence(EventConsequence consequence, string storyTitle)
    {
        if (consequence == null) return;

        if (TryGetEffect(consequence, out var effect))
        {
            if (consequence.durationSevenths > 0)
            {
                string source = $"Event: {storyTitle} #{++timedEffectCounter}";
                if (EffectRouter.Apply(effect, source)) activeTimed.Add(new TimedEffect { source = source, remainingSevenths = consequence.durationSevenths });
            }
            else
            {
                EffectRouter.Apply(effect, $"Event: {storyTitle}");
            }
            return;
        }

        var pop = PopGrowthLogic.Instance;
        switch (consequence.type)
        {
            case EventConsequence.ConsequenceType.ScoreChange:
                ModifyEventScore(consequence.targetName, consequence.value);
                break;
            case EventConsequence.ConsequenceType.StatChange:
                if (Stats != null) Stats.ModifyStat(consequence.targetName, consequence.value);
                break;
            case EventConsequence.ConsequenceType.ResourceChange:
                if (GameUnits != null) GameUnits.ChangeResourceFromName(consequence.targetName, consequence.value, false);
                break;
            case EventConsequence.ConsequenceType.ProductionUnitChange:
                if (GameUnits != null) GameUnits.ChangeProductionUnitFromName(consequence.targetName, consequence.value);
                break;
            case EventConsequence.ConsequenceType.TechnologyEnlightened:
                EnlightenTechnology(consequence.targetName);
                break;
            case EventConsequence.ConsequenceType.PopulationChange:
                if (consequence.value < 0) pop?.ModifyPopulation(consequence.value);
                else GameLog.Warning($"PopulationChange {consequence.value} in '{storyTitle}' ignored: population can only be removed. Use VagrantsChange to add people.", Log);
                break;
            case EventConsequence.ConsequenceType.HousingChange:
                pop?.ModifyHousing(consequence.value);
                break;
            case EventConsequence.ConsequenceType.VagrantsChange:
                pop?.ModifyVagrants(consequence.value);
                break;
            case EventConsequence.ConsequenceType.DeathsChange:
                pop?.ProcessEventDeaths(consequence.value);
                break;
            case EventConsequence.ConsequenceType.DeathRecordsRevision:
                pop?.ReviseDeathRecords(consequence.value);
                break;
            case EventConsequence.ConsequenceType.WeatherChange:
                ApplyWeatherChange(consequence);
                break;
            case EventConsequence.ConsequenceType.UnlockEvent:
                if (Volumes == null || !Volumes.UnlockStory(consequence.targetName))
                {
                    GameLog.Warning($"unlock_event '{consequence.targetName}' in '{storyTitle}': no such story.", Log);
                }
                break;
            default:
                GameLog.Error($"Unhandled consequence type {consequence.type} in '{storyTitle}'.", Log);
                break;
        }
    }

    private void EnlightenTechnology(string technologyName)
    {
        var techSlot = GameUnits != null ? GameUnits.GetTechnologySlot(technologyName) : null;
        if (techSlot == null)
        {
            GameLog.Warning($"TechnologyEnlightened: '{technologyName}' is not in the research tab.", Log);
            return;
        }
        techSlot.enlightenedCompleted = true;
        techSlot.researchProgress = Mathf.Max(techSlot.researchProgress, Mathf.Clamp01(techSlot.enlightenedBonusPercent));
        techSlot.RefreshTechnologyUI();
        techSlot.UpdateProgressUI();
        if (techSlot.technologyTreeLogic != null) techSlot.technologyTreeLogic.DetermineTechnologyVisibility(techSlot);
    }

    // targetName is a weather profile or "clear"; value 1 makes it permanent, 0 lets procedural weather replace it.
    private static void ApplyWeatherChange(EventConsequence consequence)
    {
        var weather = CelestialWeatherSystemLogic.Instance;
        if (weather == null) return;
        if (string.Equals(consequence.targetName, "clear", StringComparison.OrdinalIgnoreCase))
        {
            weather.ClearWeather();
            return;
        }
        var profile = CelestialWeatherSystemLogic.FindWeatherProfile(consequence.targetName);
        if (profile == null)
        {
            GameLog.Warning($"WeatherChange: profile '{consequence.targetName}' not found.", Log);
            return;
        }
        weather.SetWeatherProfileFromEvent(profile, isPermanent: consequence.value == 1, ignoreEchoValidation: true);
    }

    private void TickTimedEffects()
    {
        for (int i = activeTimed.Count - 1; i >= 0; i--)
        {
            if (--activeTimed[i].remainingSevenths > 0) continue;
            EffectRouter.RemoveSource(activeTimed[i].source);
            GameLog.Event($"Timed consequence expired: {activeTimed[i].source}", Log);
            activeTimed.RemoveAt(i);
        }
    }

    // ===== SCORES, COOLDOWNS, COMPLETIONS =====

    public void ModifyEventScore(string scoreName, int change)
    {
        if (string.IsNullOrEmpty(scoreName)) return;
        var score = eventScores.Find(s => string.Equals(s.name, scoreName, StringComparison.OrdinalIgnoreCase));
        if (score == null)
        {
            score = new EventScore { name = scoreName, value = 0 };
            eventScores.Add(score);
        }
        score.value += change;
        GameLog.Event($"Score '{scoreName}' {change:+#;-#;0} → {score.value}", Log);
    }

    public int GetEventScore(string scoreName)
    {
        var score = eventScores.Find(s => string.Equals(s.name, scoreName, StringComparison.OrdinalIgnoreCase));
        return score?.value ?? 0;
    }

    /// <summary>Every event score recorded so far (diagnostics, saves).</summary>
    public IReadOnlyList<EventScore> GetEventScores() => eventScores;

    /// <summary>How many times a story (by node name or title) has completed.</summary>
    public int GetCompletionCount(string storyName) => storyName != null && completionCounts.TryGetValue(storyName, out int count) ? count : 0;

    public bool IsEventOnCooldown(string eventName, int requiredCooldown)
    {
        return requiredCooldown > 0 && eventName != null && eventCooldowns.TryGetValue(eventName, out int since) && since < requiredCooldown;
    }

    public int GetSeventhsSinceLastEvent() => seventhsSinceLastEvent;

    // ===== TRIGGER ANALYSIS =====

    private static readonly EventCondition.ConditionType[] TimeConditions =
    {
        EventCondition.ConditionType.SeventhCheck, EventCondition.ConditionType.PhaseCheck, EventCondition.ConditionType.EchoCheck,
        EventCondition.ConditionType.CycleCheck, EventCondition.ConditionType.RitualSeventhCheck
    };

    private static readonly EventCondition.ConditionType[] PopulationConditions =
    {
        EventCondition.ConditionType.PopulationCheck, EventCondition.ConditionType.HousingCheck, EventCondition.ConditionType.VagrantsCheck,
        EventCondition.ConditionType.DeathsCheck, EventCondition.ConditionType.VagrantDeathsCheck, EventCondition.ConditionType.TrueDeathsCheck
    };

    /// <summary>A label for what most likely triggered a story, by condition priority.</summary>
    private static string DetermineEventTriggerSource(StoryNode storyNode)
    {
        var conditions = storyNode?.storyConditions;
        if (conditions == null) return "unknown";
        var darkMorale = conditions.FirstOrDefault(IsDarkMoraleCondition);
        if (darkMorale != null) return $"dark_morale_score_{darkMorale.requiredValue}";
        var c = conditions.FirstOrDefault(x => TimeConditions.Contains(x.type));
        if (c != null) return $"time_based_{c.type}";
        c = conditions.FirstOrDefault(x => x.type == EventCondition.ConditionType.ResourceCheck);
        if (c != null) return $"resource_{c.targetName}_{c.comparison}_{c.requiredValue}";
        c = conditions.FirstOrDefault(x => x.type == EventCondition.ConditionType.TechnologyCheck);
        if (c != null) return $"technology_{c.targetName}";
        c = conditions.FirstOrDefault(x => x.type == EventCondition.ConditionType.StatCheck);
        if (c != null) return $"stat_{c.targetName}_{c.comparison}_{c.requiredValue}";
        c = conditions.FirstOrDefault(x => PopulationConditions.Contains(x.type));
        if (c != null) return $"population_{c.type}_{c.comparison}_{c.requiredValue}";
        c = conditions.FirstOrDefault(x => x.type == EventCondition.ConditionType.NoEventInSeventhsCheck);
        if (c != null) return $"no_events_{c.requiredValue}_sevenths";
        return "unknown_trigger";
    }

    private static bool IsDarkMoraleCondition(EventCondition condition) =>
        condition.type == EventCondition.ConditionType.ScoreCheck && string.Equals(condition.targetName, DarkMoraleScore, StringComparison.OrdinalIgnoreCase);

    private static bool IsEventTriggeredByDarkMorale(StoryNode storyNode) => storyNode.storyConditions != null && storyNode.storyConditions.Any(IsDarkMoraleCondition);

    // ===== ACCESSORS =====

    public void AddVolume(EventVolume volume)
    {
        if (Volumes != null) Volumes.AddVolume(volume);
    }

    public bool IsEventActive() => isEventActive;

    public StoryNode GetCurrentStoryNode() => currentStoryNode;

    public string GetCurrentEventTriggerSource() => currentEventTriggerSource;

    public StatManager GetStatManager() => Stats;

    public GameUnitsLogic GetGameUnitsLogic() => GameUnits;

    public TimeSystemLogic GetTimeSystem() => TimeSystem;

    public EventVolumeManager GetVolumeManager() => Volumes;

    public EventScreenManager GetScreenManager() => screenManager;
}

/// <summary>Stores the story a notification opens.</summary>
public class EventNotificationData : MonoBehaviour
{
    public StoryNode storyNode;
}
