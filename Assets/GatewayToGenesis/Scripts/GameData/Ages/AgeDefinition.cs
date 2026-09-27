using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>One stage of an Age Crisis: when it begins and what the world does then.</summary>
[Serializable]
public class CrisisStageSpec
{
    [Tooltip("What the stage is called once the player can see it (\"Peach Sickness\").")]
    public string name;
    [Tooltip("When the stage begins, as a share of the Age (0.5 = halfway).")]
    [Range(0f, 1f)] public float startsAt = 0.5f;
    [Tooltip("Technology whose research begins this stage (the Age waits at the stage until then; researching it early begins it at once). Empty: time alone.")]
    public string technology;
    [Tooltip("Ink knot offered when the stage begins (authored with '# locked: true'); empty for none.")]
    public string story;
    [Tooltip("From this stage on, the crisis is named on the HUD (\"crises never announce themselves\" before that) and its Food penalty applies.")]
    public bool declared;
}

/// <summary>An Age that may follow, chosen by how the crisis went (<see cref="CrisisRules.NextAge"/>).</summary>
[Serializable]
public class NextAgeSpec
{
    [Tooltip("Id of the following Age (its asset's id).")]
    public string age;
    [Tooltip("Taken when the crisis severity is at or below this (entries are tried in order; the last is the fallback).")]
    [Range(0f, 1f)] public float maxSeverity = 1f;
}

/// <summary>
/// An Age of Magic: its Acts of Fate, its Age Crisis and the passage out of it. One asset per Age in
/// Resources/Ages, keyed by <see cref="id"/> (the slug of the vault title, as <see cref="GameAge"/> and the keyword
/// cards' <c>@age-...</c> states name it). <see cref="AgeProgression"/> plays it; <see cref="AgeRules"/> and
/// <see cref="CrisisRules"/> are its rules. Durations and crisis numbers are prototype proposals (roadmap D01-D03).
/// </summary>
[CreateAssetMenu(fileName = "New Age", menuName = "Game Object/Age Definition", order = 10)]
public class AgeDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Slug of the vault title: \"age-of-desolation\".")]
    public string id;
    [Tooltip("The vault's \"Ages N\" folder: 0 for the Age of Desolation.")]
    public int number;
    public string title;
    [Tooltip("The vault note's epigraph, verbatim.")]
    [TextArea(1, 3)] public string epigraph;

    [Header("Acts of Fate")]
    [Tooltip("Sevenths each Act lasts. Three Acts, four in the long Ages (the vault's II and V).")]
    public List<int> actSevenths = new List<int> { 63, 63, 63 };
    [Tooltip("Ink knot offered at each boundary between Acts (Act II begins, Act III begins...); empty entries offer nothing.")]
    public List<string> actOfFateStories = new List<string>();
    [Tooltip("Ink knot offered when the Age begins; empty for none.")]
    public string openingStory;
    [Tooltip("For each boundary between Acts (Act II begins, Act III begins...): the technology whose research opens it. The Age waits at the boundary until it is researched and opens it at once when researched early, so the technology tree drives the Age. Empty entries keep the Act's sevenths.")]
    public List<string> actTechnologies = new List<string>();

    [Header("Era Score")]
    [Tooltip("Era Score an Event Technology awards when researched (the owner's rule: 2).")]
    public int eventTechnologyEraScore = 2;
    [Tooltip("Era Score an Event or Crisis Technology awards when enlightened (the owner's rule: 1). Other technologies award none.")]
    public int enlightenedTechnologyEraScore = 1;
    [Tooltip("Technologies that each grant Crisis Advantages when researched (Echoes of Hunger: 3).")]
    public List<string> advantageTechnologies = new List<string>();
    public int advantagesPerTechnology = 3;

    [Header("Age Crisis")]
    public string crisisTitle;
    [Tooltip("Stages in order; the first begins the crisis (halfway through the Age in the prototype).")]
    public List<CrisisStageSpec> crisisStages = new List<CrisisStageSpec>();
    [Tooltip("Technologies that prepare for this crisis: each one researched lowers its severity.")]
    public List<string> preparationTechnologies = new List<string>();
    [Tooltip("Event score that prepares for the crisis (raised by choices in the Age's stories).")]
    public string preparationScore;
    public string preparationLabel = "Preparation";
    [Tooltip("Event score of desperate short-term relief (ash-bread): it saves lives now at a later cost.")]
    public string reliefScore;
    public string reliefLabel = "Desperate relief";
    [Tooltip("Event score that makes the crisis worse (monocrop drift).")]
    public string aggravationScore;
    public string aggravationLabel = "Aggravation";
    [Tooltip("World features whose explored tiles ease this crisis (fertile ground for a famine).")]
    public string easingFeatureTag;
    public string easingLabel = "Explored land";
    public CrisisTuning tuning = new CrisisTuning();

    [Header("Passage")]
    [Tooltip("The Ages that may follow, chosen by severity. Empty: the chronicle stops at the end of this Age (for now).")]
    public List<NextAgeSpec> nextAges = new List<NextAgeSpec>();

    public bool HasCrisis => crisisStages != null && crisisStages.Count > 0;

    public int Length => AgeRules.Length(actSevenths);

    /// <summary>The technology gating each beat of <paramref name="schedule"/> (null where time alone moves the Age).</summary>
    public List<string> Gates(IReadOnlyList<AgeBeat> schedule) => schedule.Select(b =>
        b.kind == AgeBeatKind.ActOfFate && actTechnologies != null && b.index - 1 < actTechnologies.Count ? NullIfEmpty(actTechnologies[b.index - 1]) :
        b.kind == AgeBeatKind.CrisisStage && crisisStages != null && b.index < crisisStages.Count ? NullIfEmpty(crisisStages[b.index]?.technology) : null).ToList();

    private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public List<AgeBeat> Schedule() => AgeRules.Schedule(actSevenths, crisisStages != null ? crisisStages.Select(s => s != null ? s.startsAt : 1f).ToList() : null);

    public CrisisRules.Labels Labels => new CrisisRules.Labels
    {
        preparation = string.IsNullOrEmpty(preparationLabel) ? "Preparation" : preparationLabel,
        relief = string.IsNullOrEmpty(reliefLabel) ? "Desperate relief" : reliefLabel,
        aggravation = string.IsNullOrEmpty(aggravationLabel) ? "Aggravation" : aggravationLabel,
        easingTiles = string.IsNullOrEmpty(easingLabel) ? "Explored land" : easingLabel,
    };

    public List<(string ageId, float maxSeverity)> NextAgeTable() =>
        nextAges != null ? nextAges.Where(n => n != null && !string.IsNullOrEmpty(n.age)).Select(n => (n.age, n.maxSeverity)).ToList() : new List<(string, float)>();

    /// <summary>"Act II of III".</summary>
    public string ActLabel(int act) => $"Act {AgeRules.Roman(act + 1)} of {AgeRules.Roman(Math.Max(1, actSevenths != null ? actSevenths.Count : 1))}";
}
