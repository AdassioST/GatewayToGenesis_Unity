using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "New Technology Data", menuName = "Game Object/Technology Data", order = 3)]
public class TechnologyData : ScriptableObject
{
    public GameUnit gameUnit;

    [Tooltip("Unlocking this technology triggers an event check, so events conditioned on it can fire immediately.")]
    public bool isEventTech;

    public int tier;

    public int satisfactionPoints = 0;

    public List<string> techRequirements = new List<string>(); // Prerequisite technologies by name.

    public List<string> resourceRequirements = new List<string>();
    public List<float> resourceAmount = new List<float>();

    // Renamed from "eureka"; the attribute keeps conditions authored before the rename.
    [FormerlySerializedAs("eurekaConditions")]
    public List<EnlightenedCondition> enlightenedConditions = new List<EnlightenedCondition>();

    public List<TechUnlockable> techUnlockables = new List<TechUnlockable>();

    /// <summary>Every Enlightenment goal is met (false for a technology with none: only a story can enlighten it).</summary>
    public bool EnlightenmentMet()
    {
        bool any = false;
        foreach (var goal in enlightenedConditions)
        {
            if (goal == null) continue;
            if (!goal.IsMet()) return false;
            any = true;
        }
        return any;
    }

    /// <summary>
    /// The Enlightenment goals in words ("Build 3 Decaying Hut"), each with its progress when <paramref name="withProgress"/>
    /// ("(1/3)"). An unanswered riddle is only named as one (<paramref name="shortRiddles"/>: the card's bar has no room
    /// for it) or read out whole with its clue; an answered one says what it was.
    /// </summary>
    public string DescribeEnlightenment(bool withProgress = false, bool shortRiddles = false)
    {
        var parts = new List<string>();
        foreach (var goal in enlightenedConditions)
        {
            if (goal == null) continue;
            if (goal.IsRiddle && !goal.IsMet()) { parts.Add(shortRiddles ? "A riddle (hover to read)" : goal.RiddleText()); continue; }
            string progress = withProgress ? goal.ProgressText() : string.Empty;
            parts.Add(string.IsNullOrEmpty(progress) ? goal.Describe() : $"{goal.Describe()} {progress}");
        }
        return string.Join("; ", parts);
    }

    /// <summary>Some goal of its Enlightenment is a riddle (answered or not).</summary>
    public bool HasRiddle()
    {
        foreach (var goal in enlightenedConditions) if (goal != null && goal.IsRiddle) return true;
        return false;
    }
}

public enum EnlightenedType
{
    GatherResourceFromClick,
    ConstructUnit,
    WitnessEvent,
    AccumulateResource,
    UnlockTechnology,
    ReachProductionRate,
    /// <summary>Any <see cref="GameValues"/> value: trigger "domain:target" ("population:", "journeys:"), met at or above the amount.</summary>
    GameValue
}

/// <summary>
/// A goal that enlightens a technology (the game's eureka): once all of a technology's goals are met it is uncovered at
/// once, even before its prerequisites, and part of its research is done for free
/// (<see cref="GameUnitsLogic.EnlightenTechnology(GameTechnologySlot, string)"/>; <see cref="TechnologyTreeLogic"/>
/// checks the goals). Evaluated through <see cref="GameValues"/>: every type maps to one value domain.
/// </summary>
[System.Serializable]
public class EnlightenedCondition
{
    public EnlightenedType type;
    public string trigger;        // Resource, production unit, event or technology name.
    public float requiredAmount;  // Threshold for counting conditions.
    public string description;

    [Header("Riddle (a goal to work out rather than to read)")]
    [Tooltip("Shown instead of the goal, with no progress, until the goal is met (empty: the goal is shown plainly with its progress).")]
    [TextArea(1, 4)] public string riddle;
    [Tooltip("A plainer clue shown under the riddle once the player is on the right path: the goal part met (clueAt), or clueTrigger reached.")]
    [TextArea(1, 3)] public string clue;
    [Tooltip("Share of the goal (0-1) that brings the clue out; 0: only clueTrigger does.")]
    [Range(0f, 1f)] public float clueAt = 0.5f;
    [Tooltip("Optional GameValue 'domain:target' that also brings the clue out once it reaches clueAmount (a species known, a place found).")]
    public string clueTrigger;
    public float clueAmount = 1f;

    public bool IsMet() => TryProgress(out float current, out float required) && current >= required;

    /// <summary>The goal is authored as a riddle (<see cref="riddle"/>).</summary>
    public bool IsRiddle => !string.IsNullOrWhiteSpace(riddle);

    /// <summary>The riddle's clue is known: the goal is met, partly met (<see cref="clueAt"/>), or <see cref="clueTrigger"/> reached.</summary>
    public bool ClueShown()
    {
        if (!IsRiddle || string.IsNullOrWhiteSpace(clue)) return false;
        if (TryProgress(out float current, out float required))
        {
            if (current >= required) return true;
            if (clueAt > 0f && required > 0f && current > 0f && current / required >= clueAt - 1e-4f) return true;
        }
        if (string.IsNullOrWhiteSpace(clueTrigger)) return false;
        string text = clueTrigger.Trim();
        int colon = text.IndexOf(':');
        string domain = colon < 0 ? text : text.Substring(0, colon).Trim();
        string target = colon < 0 ? string.Empty : text.Substring(colon + 1).Trim();
        return GameValues.TryGet(domain, target, out float value) && value >= clueAmount;
    }

    /// <summary>The riddle, and its clue once known ("… Clue: …"); the plain goal once met.</summary>
    public string RiddleText()
    {
        if (!IsRiddle) return Describe();
        if (IsMet()) return $"{Describe()} (the riddle answered)";
        return ClueShown() ? $"{riddle.Trim()} Clue: {clue.Trim()}" : riddle.Trim();
    }

    /// <summary>
    /// The value the goal reads now and the value it needs. False when its system is missing or its trigger is empty
    /// (the goal then waits). Counts of things (buildings, stories) need at least one; a technology reads 1 once researched.
    /// </summary>
    public bool TryProgress(out float current, out float required)
    {
        current = 0f;
        required = requiredAmount;
        switch (type)
        {
            case EnlightenedType.GatherResourceFromClick: return GameValues.TryGet("clicked", trigger, out current);
            case EnlightenedType.ConstructUnit: required = Mathf.Max(1f, requiredAmount); return GameValues.TryGet("building", trigger, out current);
            case EnlightenedType.WitnessEvent: required = Mathf.Max(1f, requiredAmount); return GameValues.TryGet("event_completed", trigger, out current);
            case EnlightenedType.AccumulateResource: return GameValues.TryGet("resource", trigger, out current);
            case EnlightenedType.UnlockTechnology: required = 1f; return GameValues.TryGet("technology", trigger, out current);
            case EnlightenedType.ReachProductionRate: return GameValues.TryGet("production_rate", trigger, out current);
            case EnlightenedType.GameValue:
                if (string.IsNullOrEmpty(trigger)) return false;
                SplitTrigger(out string domain, out string target);
                return GameValues.TryGet(domain, target, out current);
            default: return false;
        }
    }

    /// <summary>"(120/250)" after the wording, "(3.5/55 /s)" for a rate, nothing for a goal simply met or not, or for a
    /// goal whose halfway value is a secret (one species: <see cref="SpeciesKnowledge.HidesProgress"/>).</summary>
    public string ProgressText() => TryProgress(out float current, out float required) && !HidesProgress() && !IsRiddle
        ? TechTreeRules.ProgressText(current, required, type == EnlightenedType.ReachProductionRate)
        : string.Empty;

    private bool HidesProgress()
    {
        if (type != EnlightenedType.GameValue) return false;
        SplitTrigger(out string domain, out string target);
        return SpeciesKnowledge.HidesProgress(domain, target);
    }

    /// <summary>The authored wording, or one made from the goal when none was written ("Build 3 Timber Camp").</summary>
    public string Describe()
    {
        if (!string.IsNullOrWhiteSpace(description)) return description.Trim();
        string amount = TechTreeRules.Short(requiredAmount);
        switch (type)
        {
            case EnlightenedType.GatherResourceFromClick: return $"Gather {amount} {trigger} by hand";
            case EnlightenedType.ConstructUnit: return $"Build {TechTreeRules.Short(Mathf.Max(1f, requiredAmount))} {trigger}";
            case EnlightenedType.WitnessEvent: return $"Witness {trigger}";
            case EnlightenedType.AccumulateResource: return $"Store {amount} {trigger}";
            case EnlightenedType.UnlockTechnology: return $"Research {trigger}";
            case EnlightenedType.ReachProductionRate: return $"Produce {amount} {trigger} per second";
            default:
                SplitTrigger(out string domain, out string target);
                return string.IsNullOrEmpty(target) ? $"Reach {amount} {domain.Replace('_', ' ')}" : $"Reach {amount} {target} ({domain.Replace('_', ' ')})";
        }
    }

    /// <summary>A <see cref="EnlightenedType.GameValue"/> trigger's parts: "weather:Aurean Winds" → ("weather", "Aurean Winds").</summary>
    public void SplitTrigger(out string domain, out string target)
    {
        string text = trigger ?? string.Empty;
        int colon = text.IndexOf(':');
        domain = (colon < 0 ? text : text.Substring(0, colon)).Trim();
        target = colon < 0 ? string.Empty : text.Substring(colon + 1).Trim();
    }
}
