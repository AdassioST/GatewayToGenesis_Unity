using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns every compiled Ink story in Resources/{eventsFolderPath} into an <see cref="EventVolume"/>:
/// knots tagged as events become <see cref="StoryNode"/>s, and every knot is read into
/// <see cref="EventStoryIndex"/> for the screens. Adding content means adding or editing an .ink file;
/// no code changes. Authoring mistakes are logged once at start-up (and checked by the EditMode tests).
/// </summary>
public class InkDrivenEventSetup : MonoBehaviour
{
    private const LogChannel Log = LogChannel.Events;

    [Header("Auto Setup Settings")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private string eventsFolderPath = "Events";

    private void Start()
    {
        if (autoSetupOnStart) SetupAllVolumesFromInk();
    }

    /// <summary>Build and register a volume for every compiled Ink story in the events folder.</summary>
    public void SetupAllVolumesFromInk()
    {
        var volumes = LoadVolumes(eventsFolderPath);
        foreach (var volume in volumes)
        {
            if (EventSystemLogic.Instance != null) EventSystemLogic.Instance.AddVolume(volume);
        }

        var problems = Validate(volumes);
        foreach (var problem in problems) GameLog.Warning(problem, Log);
        GameLog.Event($"Created {volumes.Count} event volume(s) from Resources/{eventsFolderPath} ({EventStoryIndex.Count} knots, {EventStoryIndex.Problems.Count + problems.Count} authoring problem(s))", Log);
    }

    /// <summary>A volume for every compiled Ink story in Resources/<paramref name="folder"/> that declares events.</summary>
    public static List<EventVolume> LoadVolumes(string folder = "Events")
    {
        var volumes = new List<EventVolume>();
        foreach (var asset in Resources.LoadAll<TextAsset>(folder))
        {
            if (asset == null || !IsCompiledInk(asset.text)) continue; // .ink sources sit beside their .json
            var volume = CreateVolume(asset);
            if (volume != null) volumes.Add(volume);
        }
        return volumes;
    }

    /// <summary>
    /// Everything wrong with the loaded stories beyond single-knot parsing (those are in
    /// <see cref="EventStoryIndex.Problems"/>): broken knot links and unlocks, chorus layout, and names in
    /// story conditions, consequences, choices and outcomes that no catalog knows.
    /// </summary>
    public static List<string> Validate(IEnumerable<EventVolume> volumes)
    {
        var eventKnots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        foreach (var volume in volumes)
        {
            foreach (var node in volume.storyNodes)
            {
                eventKnots.Add(node.nodeName);
                EventContentCheck.Conditions(node.nodeName, node.storyConditions, problems);
                EventContentCheck.Consequences(node.nodeName, node.storyConsequences, problems);
            }
        }
        problems.AddRange(EventStoryIndex.Validate(eventKnots));
        return problems;
    }

    /// <summary>A volume for one compiled story, or null when it declares no events.</summary>
    public static EventVolume CreateVolume(TextAsset compiled)
    {
        if (compiled == null) return null;
        var nodes = EventStoryIndex.IndexStory(compiled.text, compiled.name);
        if (nodes.Count == 0)
        {
            GameLog.Event($"'{compiled.name}' declares no events (no knot has title, conditions and event_type tags)", Log);
            return null;
        }
        var volume = new EventVolume
        {
            volumeName = compiled.name,
            inkMasterfile = compiled,
            isUnlocked = true,
            priority = 5,
        };
        volume.storyNodes.AddRange(nodes);
        return volume;
    }

    public static bool IsCompiledInk(string text) => !string.IsNullOrEmpty(text) && text.TrimStart().StartsWith("{", StringComparison.Ordinal);
}
