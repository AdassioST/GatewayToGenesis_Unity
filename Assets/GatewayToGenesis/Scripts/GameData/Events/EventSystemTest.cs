using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// Scene debug harness for the event system, driven from the component's context menu (and optionally once
/// on start). It only uses the managers' public API: check for events, start a story by knot name, list or
/// reset scores, validate the story index and print the state of every manager.
/// </summary>
public class EventSystemTest : MonoBehaviour
{
    [Header("Test Controls")]
    [SerializeField] private bool testOnStart = false;
    [SerializeField] private float startTestDelaySeconds = 1.0f;

    [Header("Targets")]
    [Tooltip("Entry knot started by 'Trigger Test Story'.")]
    [SerializeField] private string testNodeName = "weeping_princess";

    private static EventSystemLogic Events => EventSystemLogic.Instance;

    private void Start()
    {
        if (testOnStart) StartCoroutine(RunTestAfterDelay());
    }

    private IEnumerator RunTestAfterDelay()
    {
        if (startTestDelaySeconds > 0f) yield return new WaitForSeconds(startTestDelaySeconds);
        float waited = 0f;
        while ((Events == null || Events.GetVolumeManager() == null) && waited < 5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        if (Events == null)
        {
            GameLog.Warning("EventSystemLogic is not ready; auto test skipped.", LogChannel.Events);
            yield break;
        }
        Say("Auto test after start-up delay");
        TestEventSystem();
    }

    [ContextMenu("Test Event System")]
    public void TestEventSystem()
    {
        if (!Ready()) return;
        Say("Checking for available events...");
        Events.TriggerEventCheck();
        Say(Events.IsEventActive() ? $"Event active: {Events.GetCurrentStoryNode()?.storyTitle}" : "No event started (conditions, cooldowns or warm-up).");
    }

    [ContextMenu("Trigger Test Story")]
    public void TestManualStoryTrigger()
    {
        if (!Ready()) return;
        var volumes = Events.GetVolumeManager();
        var story = volumes != null ? volumes.FindStoryNode(testNodeName) : null;
        if (story == null)
        {
            Say($"No story with entry knot '{testNodeName}' is loaded.");
            return;
        }
        Say($"Starting '{story.storyTitle}'...");
        Events.TriggerSpecificEvent(story, "EventSystemTest");
    }

    [ContextMenu("Show Current Scores")]
    public void ShowCurrentScores()
    {
        if (!Ready()) return;
        var scores = Events.GetEventScores();
        Say(scores.Count == 0 ? "No event scores recorded yet." : "Event scores: " + string.Join(", ", scores.Select(s => $"{s.name}={s.value}")));
    }

    [ContextMenu("Reset All Scores")]
    public void ResetAllScores()
    {
        if (!Ready()) return;
        foreach (var score in Events.GetEventScores().ToList())
        {
            if (score.value != 0) Events.ModifyEventScore(score.name, -score.value);
        }
        Say("All event scores reset to 0.");
    }

    [ContextMenu("Validate Stories")]
    public void ValidateStories()
    {
        var problems = EventStoryIndex.Validate();
        Say(problems.Count == 0 ? $"Story index OK: {EventStoryIndex.Count} knots." : $"{problems.Count} story problems:\n- " + string.Join("\n- ", problems));
    }

    [ContextMenu("Test Time Pausing")]
    public void TestTimePausing()
    {
        var time = Events != null ? Events.GetTimeSystem() : null;
        if (time == null)
        {
            Say("TimeSystemLogic not found.");
            return;
        }
        bool before = time.isTimePaused;
        time.PauseTime(true);
        bool paused = time.isTimePaused;
        time.PauseTime(before);
        Say($"Time pausing: was {before}, paused → {paused}, restored → {time.isTimePaused}");
    }

    [ContextMenu("Open Event Tab (Toggle)")]
    public void ToggleEventTab()
    {
        var hotkeys = TabHotkeys.Instance;
        if (hotkeys != null) hotkeys.ToggleEventTab();
        Say(hotkeys != null ? "Toggled the event tab." : "TabHotkeys not found in the scene.");
    }

    [ContextMenu("Diagnose Event System")]
    public void DiagnoseEventSystem()
    {
        if (!Ready()) return;
        var volumes = Events.GetVolumeManager();
        var screens = Events.GetScreenManager();
        var time = Events.GetTimeSystem();
        var screen = screens != null ? screens.CurrentEventScreen : null;
        Say("Event system state:" +
            $"\n  Event active: {Events.IsEventActive()} (trigger: {Events.GetCurrentEventTriggerSource()})" +
            $"\n  Story: {Events.GetCurrentStoryNode()?.storyTitle ?? "none"}" +
            $"\n  Queued consequences: {Events.GetCumulativeConsequences().Count}" +
            $"\n  Sevenths since last event: {Events.GetSeventhsSinceLastEvent()}" +
            $"\n  Time paused: {(time != null ? time.isTimePaused.ToString() : "no time system")}" +
            $"\n  Volumes: {(volumes != null ? volumes.GetVolumes().Count : 0)}, indexed knots: {EventStoryIndex.Count}" +
            $"\n  Volume manager story: {volumes?.GetCurrentStoryNode()?.storyTitle ?? "none"}" +
            $"\n  Screen: {(screen != null ? $"{screen.screenType} '{screen.inkKnot}' (complete: {screens.IsScreenComplete(screen)})" : "none")}");
    }

    private bool Ready()
    {
        if (Events != null) return true;
        GameLog.Error("EventSystemLogic not found.", LogChannel.Events);
        return false;
    }

    private void Say(string message)
    {
        GameLog.Event(message, LogChannel.Events);
    }
}
