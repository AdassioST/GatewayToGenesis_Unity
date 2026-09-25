using System.Collections.Generic;
using UnityEngine;
using Ink.Runtime;

/// <summary>
/// A stand-alone Ink runner for tools and debugging: load any compiled story (bound with
/// <see cref="InkFunctions"/>, like the game's), jump around, continue, choose and inspect variables.
/// The game's events do not use it; <see cref="EventVolumeManager"/> owns the live stories and
/// <see cref="EventStoryIndex"/> the precompiled knots.
/// </summary>
public class InkStoryManager : MonoBehaviour
{
    private const LogChannel Log = LogChannel.Events;

    [Header("Ink Settings")]
    [SerializeField] private bool saveStoryState = true;

    private Story currentStory;
    private TextAsset currentStoryAsset;

    /// <summary>Load <paramref name="storyAsset"/> (compiled Ink JSON) and bind the game functions.</summary>
    public void LoadStory(TextAsset storyAsset)
    {
        if (storyAsset == null)
        {
            LogStory("Cannot load a null story asset.");
            return;
        }
        currentStoryAsset = storyAsset;
        currentStory = new Story(storyAsset.text);
        InkFunctions.Bind(currentStory, LogStory);
        LogStory($"Loaded story: {storyAsset.name}");
    }

    public Story GetCurrentStory() => currentStory;

    public TextAsset GetCurrentStoryAsset() => currentStoryAsset;

    public bool HasStory() => currentStory != null;

    public void UnloadStory()
    {
        currentStory = null;
        currentStoryAsset = null;
        LogStory("Story unloaded");
    }

    // ===== NAVIGATION =====

    public void JumpToKnot(string knotName)
    {
        if (currentStory == null) return;
        currentStory.ChoosePathString(knotName);
        LogStory($"Jumped to knot: {knotName}");
    }

    /// <summary>Continue as far as the story goes and return the text.</summary>
    public string ContinueStory()
    {
        if (currentStory == null || !currentStory.canContinue) return string.Empty;
        string text = currentStory.ContinueMaximally();
        LogStory($"Story continued: {text}");
        return text;
    }

    public bool CanContinue() => currentStory != null && currentStory.canContinue;

    public List<Choice> GetCurrentChoices() => currentStory != null ? currentStory.currentChoices : new List<Choice>();

    public bool HasChoices() => currentStory != null && currentStory.currentChoices.Count > 0;

    public void MakeChoice(int choiceIndex)
    {
        if (currentStory == null || choiceIndex < 0 || choiceIndex >= currentStory.currentChoices.Count) return;
        currentStory.ChooseChoiceIndex(choiceIndex);
        LogStory($"Made choice: {choiceIndex}");
    }

    public List<string> GetCurrentTags() => currentStory != null ? currentStory.currentTags : new List<string>();

    public string GetCurrentPath() => currentStory != null ? currentStory.state.currentPathString : string.Empty;

    /// <summary>Every top-level knot of the loaded story.</summary>
    public List<string> GetAvailableKnots()
    {
        var knots = new List<string>();
        var named = currentStory != null && currentStory.mainContentContainer != null ? currentStory.mainContentContainer.namedContent : null;
        if (named != null) knots.AddRange(named.Keys);
        return knots;
    }

    /// <summary>Whether the loaded story has a top-level knot called <paramref name="knotName"/> (does not move the story).</summary>
    public bool KnotExists(string knotName)
    {
        var named = currentStory != null && currentStory.mainContentContainer != null ? currentStory.mainContentContainer.namedContent : null;
        return named != null && !string.IsNullOrEmpty(knotName) && named.ContainsKey(EventScript.TopLevelKnot(knotName));
    }

    // ===== VARIABLES AND STATE =====

    public void SetVariable(string variableName, object value)
    {
        if (currentStory == null) return;
        currentStory.variablesState[variableName] = value;
        LogStory($"Set variable {variableName} = {value}");
    }

    public object GetVariable(string variableName) => currentStory != null ? currentStory.variablesState[variableName] : null;

    public void ObserveVariable(string variableName, System.Action<string, object> callback)
    {
        if (currentStory != null && callback != null) currentStory.ObserveVariable(variableName, (name, value) => callback(name, value));
    }

    /// <summary>Every global variable of the loaded story with its current value.</summary>
    public Dictionary<string, object> GetStoryVariables()
    {
        var variables = new Dictionary<string, object>();
        if (currentStory == null) return variables;
        foreach (string name in currentStory.variablesState) variables[name] = currentStory.variablesState[name];
        return variables;
    }

    public string SaveStoryState()
    {
        if (currentStory == null || !saveStoryState) return string.Empty;
        LogStory("Story state saved");
        return currentStory.state.ToJson();
    }

    public void LoadStoryState(string stateJson)
    {
        if (currentStory == null || string.IsNullOrEmpty(stateJson)) return;
        currentStory.state.LoadJson(stateJson);
        LogStory("Story state loaded");
    }

    public void ResetStory()
    {
        if (currentStory == null) return;
        currentStory.ResetState();
        LogStory("Story reset");
    }

    private void LogStory(string message)
    {
        GameLog.Event(message, Log);
    }
}
