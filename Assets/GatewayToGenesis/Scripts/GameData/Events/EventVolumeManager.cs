using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;

/// <summary>
/// Holds the event volumes (one per compiled Ink story), picks which story to offer, and moves a running
/// story from knot to knot. Screen types follow the knot naming convention (see <see cref="EventScript.ScreenTypeOf"/>).
///
/// Each volume also has a live Ink story bound to the game (<see cref="InkFunctions.Bind"/>). When a knot is
/// shown, its Ink logic runs once on that story, so functions like ~ ModifyStat("morale", 5) take effect when
/// the player reaches them. Text comes from <see cref="EventStoryIndex"/>.
/// </summary>
public class EventVolumeManager : SingletonBehaviour<EventVolumeManager>
{
    private const LogChannel Log = LogChannel.Events;

    [Header("Event Volumes")]
    [SerializeField] private List<EventVolume> eventVolumes = new List<EventVolume>();

    [Header("Current State")]
    [SerializeField] private EventVolume currentVolume;
    [SerializeField] private StoryNode currentStoryNode;
    [SerializeField] private int currentScreenIndex = 0;

    private readonly Dictionary<string, Story> volumeStories = new Dictionary<string, Story>();
    private readonly Dictionary<string, StoryNode> storyNodeLookup = new Dictionary<string, StoryNode>();
    private readonly Dictionary<StoryNode, EventVolume> volumeOfNode = new Dictionary<StoryNode, EventVolume>();
    private readonly List<StoryNode> candidates = new List<StoryNode>();

    protected override void OnSingletonAwake()
    {
        // Volumes assigned in the Inspector; Ink-built volumes arrive through AddVolume.
        foreach (var volume in eventVolumes) RegisterVolume(volume);
    }

    // ===== VOLUMES =====

    /// <summary>Add a volume (ignored when already present). Its story is indexed if it was not yet.</summary>
    public void AddVolume(EventVolume volume)
    {
        if (volume == null || eventVolumes.Contains(volume)) return;
        eventVolumes.Add(volume);
        RegisterVolume(volume);
    }

    private void RegisterVolume(EventVolume volume)
    {
        if (volume == null) return;
        foreach (var node in volume.storyNodes)
        {
            storyNodeLookup[$"{volume.volumeName}.{node.nodeName}"] = node;
            volumeOfNode[node] = volume;
        }

        if (volume.inkMasterfile == null)
        {
            GameLog.Warning($"Volume '{volume.volumeName}' has no compiled Ink assigned.", Log);
            return;
        }
        if (!EventStoryIndex.IsIndexed(volume.inkMasterfile.name)) EventStoryIndex.IndexStory(volume.inkMasterfile.text, volume.inkMasterfile.name);
        try
        {
            var story = new Story(volume.inkMasterfile.text);
            InkFunctions.Bind(story);
            volumeStories[volume.volumeName] = story;
        }
        catch (System.Exception e)
        {
            GameLog.Warning($"Volume '{volume.volumeName}': Ink story could not be created ({e.Message}).", Log);
        }
        GameLog.Event($"Registered volume '{volume.volumeName}' with {volume.storyNodes.Count} stories", Log);
    }

    // ===== CHOOSING A STORY =====

    /// <summary>
    /// The story to offer now: among unlocked stories whose conditions hold and that are off cooldown, one of
    /// the highest priority, picked at random. Null when none qualifies.
    /// </summary>
    public StoryNode FindBestAvailableStory()
    {
        candidates.Clear();
        int best = int.MinValue;
        foreach (var volume in eventVolumes)
        {
            if (!IsVolumeAvailable(volume)) continue;
            foreach (var node in volume.storyNodes)
            {
                if (node == null || !node.isUnlocked || !AreStoryConditionsMet(node)) continue;
                if (node.priority > best)
                {
                    best = node.priority;
                    candidates.Clear();
                }
                if (node.priority == best) candidates.Add(node);
            }
        }
        if (candidates.Count == 0) return null;

        var chosen = candidates[Random.Range(0, candidates.Count)];
        GameLog.Event($"Available: {candidates.Count} stor{(candidates.Count == 1 ? "y" : "ies")} at priority {best}; chose '{chosen.nodeName}'", Log);
        return chosen;
    }

    private static bool IsVolumeAvailable(EventVolume volume)
    {
        if (volume == null || !volume.isUnlocked) return false;
        foreach (var condition in volume.volumeConditions)
        {
            if (!condition.Evaluate()) return false;
        }
        return true;
    }

    private static bool AreStoryConditionsMet(StoryNode node)
    {
        var events = EventSystemLogic.Instance;
        if (node.cooldownSevenths > 0 && events != null && events.IsEventOnCooldown(node.nodeName, node.cooldownSevenths)) return false;
        foreach (var condition in node.storyConditions)
        {
            if (!condition.Evaluate()) return false;
        }
        return true;
    }

    /// <summary>Unlock a story authored with "# locked: true" (the unlock_event consequence). False when not found.</summary>
    public bool UnlockStory(string nodeName)
    {
        var node = FindStoryNode(nodeName);
        if (node == null) return false;
        if (!node.isUnlocked) GameLog.Event($"Story '{node.nodeName}' unlocked", Log);
        node.isUnlocked = true;
        return true;
    }

    /// <summary>A story by knot name in any volume.</summary>
    public StoryNode FindStoryNode(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return null;
        foreach (var volume in eventVolumes)
        {
            foreach (var node in volume.storyNodes)
            {
                if (string.Equals(node.nodeName, nodeName, System.StringComparison.OrdinalIgnoreCase)) return node;
            }
        }
        return null;
    }

    // ===== RUNNING A STORY =====

    /// <summary>Start a story at its first screen. The volume is looked up from the story when not given.</summary>
    public void StartStory(StoryNode storyNode, EventVolume volume)
    {
        if (storyNode == null) return;
        if (volumeOfNode.TryGetValue(storyNode, out var owner)) volume = owner;
        currentStoryNode = storyNode;
        currentVolume = volume;
        currentScreenIndex = 0;
        ExecuteNextScreen();
    }

    /// <summary>Show the next step of the story's opening flow (the splash); completes the story when there is none.</summary>
    public void ExecuteNextScreen()
    {
        if (currentStoryNode == null || currentScreenIndex >= currentStoryNode.screenFlow.Count)
        {
            CompleteStory();
            return;
        }
        var step = currentStoryNode.screenFlow[currentScreenIndex++];
        Show(new EventScreen
        {
            screenType = ConvertFlowTypeToScreenType(step.flowType),
            screenId = step.screenId,
            displayDuration = step.displayDuration,
            waitForInput = step.waitForInput,
            inkKnot = step.inkKnot
        });
    }

    /// <summary>Show a knot on the screen its name implies; an empty knot completes the story.</summary>
    public void NavigateToKnot(string knotName)
    {
        string knot = EventScript.TopLevelKnot(knotName);
        if (string.IsNullOrEmpty(knot))
        {
            CompleteStory();
            return;
        }
        if (!EventStoryIndex.TryGet(knot, out _)) GameLog.Warning($"Knot '{knot}' is not in any compiled story; the screen will be empty.", Log);
        var type = EventScript.ScreenTypeOf(knot);
        GameLog.Event($"Navigate → '{knot}' ({type})", Log);
        Show(new EventScreen { screenType = type, screenId = knot + "_screen", inkKnot = knot, waitForInput = true });
    }

    private void Show(EventScreen screen)
    {
        RunKnotLogic(screen.inkKnot);
        EventSystemLogic.Instance?.ExecuteScreen(screen);
    }

    // Run the knot's Ink once on the live story (functions with effects fire now); stop where the knot ends.
    private void RunKnotLogic(string knot)
    {
        var story = GetCurrentStory();
        if (story == null || string.IsNullOrEmpty(knot)) return;
        try
        {
            story.ChoosePathString(knot);
            for (int guard = 0; guard < 512 && story.canContinue; guard++)
            {
                string top = EventScript.TopLevelKnot(story.state.currentPathString);
                if (!string.IsNullOrEmpty(top) && top != knot) break;
                story.Continue();
                if (story.currentChoices.Count > 0) break;
            }
        }
        catch (System.Exception e)
        {
            GameLog.Warning($"Ink logic in '{knot}' failed: {e.Message}", Log);
        }
    }

    private static ScreenType ConvertFlowTypeToScreenType(ScreenFlowStep.FlowType flowType)
    {
        switch (flowType)
        {
            case ScreenFlowStep.FlowType.Verse: return ScreenType.Verse;
            case ScreenFlowStep.FlowType.Chorus: return ScreenType.Chorus;
            case ScreenFlowStep.FlowType.Bridge: return ScreenType.Bridge;
            case ScreenFlowStep.FlowType.Outro: return ScreenType.Outro;
            default: return ScreenType.Splash;
        }
    }

    /// <summary>
    /// End the running story; the one way a story ends (EventSystemLogic then applies its consequences, resumes
    /// time and restores the HUD). A second call for the same story is ignored.
    /// </summary>
    public void CompleteStory()
    {
        if (currentStoryNode == null)
        {
            GameLog.Warning("CompleteStory called with no story running.", Log);
            return;
        }
        currentStoryNode = null;
        currentScreenIndex = 0;
        if (EventSystemLogic.Instance != null) EventSystemLogic.Instance.OnStoryCompleted();
        else GameLog.Error("EventSystemLogic is missing; the story cannot complete.", Log);
    }

    // ===== ACCESSORS =====

    public StoryNode GetStoryNode(string volumeName, string nodeName) => storyNodeLookup.TryGetValue($"{volumeName}.{nodeName}", out var node) ? node : null;

    /// <summary>The live (game-bound) Ink story of the current volume.</summary>
    public Story GetCurrentStory() => currentVolume != null && currentVolume.volumeName != null && volumeStories.TryGetValue(currentVolume.volumeName, out var story) ? story : null;

    public StoryNode GetCurrentStoryNode() => currentStoryNode;

    public EventVolume GetCurrentVolume() => currentVolume;

    public int GetCurrentScreenIndex() => currentScreenIndex;

    public IReadOnlyList<EventVolume> GetVolumes() => eventVolumes;
}
