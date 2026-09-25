using System;
using System.Collections.Generic;
using System.Text;
using Ink.Runtime;

/// <summary>
/// Everything the event screens need from Ink, read once at start-up from each compiled story with a
/// preview story (queries live, actions inert): each knot's text, its choices and where they lead, the
/// consequences written on buttons and the parsed chorus choices. Screens never walk Ink themselves.
/// </summary>
public static class EventStoryIndex
{
    public sealed class Choice
    {
        /// <summary>Choice text as compiled (markers included).</summary>
        public string text;
        /// <summary>Button label (markers removed).</summary>
        public string label;
        /// <summary>Top-level knot the choice leads to; null when the story ends there.</summary>
        public string target;
        /// <summary>Consequences written on the button (&amp;C consequences: ...).</summary>
        public List<EventConsequence> consequences = new List<EventConsequence>();
    }

    public sealed class Knot
    {
        public string name;
        /// <summary>Compiled story the knot came from.</summary>
        public string source;
        public ScreenType screenType;
        public string content = string.Empty;
        public readonly List<Choice> choices = new List<Choice>();
        /// <summary>Knot the story flows into when this one has no choices.</summary>
        public string next;
        /// <summary>Parsed decisions (chorus knots only).</summary>
        public readonly List<ChorusChoiceData> chorusChoices = new List<ChorusChoiceData>();

        public Choice FirstChoice => choices.Count > 0 ? choices[0] : null;

        /// <summary>Where a single-button screen continues: the first choice's target, else the fall-through knot.</summary>
        public string ContinueTarget
        {
            get
            {
                string target = FirstChoice != null ? FirstChoice.target : next;
                return target == name ? null : target;
            }
        }

        /// <summary>Content with surrounding whitespace removed.</summary>
        public string Text => (content ?? string.Empty).Replace("\r\n", "\n").Trim();

        /// <summary>The last line of text, used as the chorus question.</summary>
        public string LastLine
        {
            get
            {
                var lines = Text.Split('\n');
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (lines[i].Trim().Length > 0) return lines[i].Trim();
                }
                return string.Empty;
            }
        }
    }

    private const LogChannel Log = LogChannel.Events;

    private static readonly Dictionary<string, Knot> Knots = new Dictionary<string, Knot>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> IndexedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> ProblemList = new List<string>();

    public static int Count => Knots.Count;
    public static IEnumerable<Knot> All => Knots.Values;
    /// <summary>Authoring problems found while indexing (unknown keys, bad numbers, missing knots, ...).</summary>
    public static IReadOnlyList<string> Problems => ProblemList;

    // Also runs when play starts, so a play session without a domain reload indexes its stories afresh.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        Knots.Clear();
        IndexedSources.Clear();
        ProblemList.Clear();
    }

    public static bool IsIndexed(string source) => source != null && IndexedSources.Contains(source);

    public static bool TryGet(string knotName, out Knot knot)
    {
        knot = null;
        string name = EventScript.TopLevelKnot(knotName);
        return name != null && Knots.TryGetValue(name, out knot);
    }

    public static Knot Get(string knotName) => TryGet(knotName, out var knot) ? knot : null;

    public static List<ChorusChoiceData> GetChorusChoices(string knotName) => TryGet(knotName, out var knot) ? knot.chorusChoices : new List<ChorusChoiceData>();

    /// <summary>
    /// Index every knot of a compiled story and return the story nodes (event entry knots) it declares.
    /// Indexing the same source twice replaces nothing and returns an empty list.
    /// </summary>
    public static List<StoryNode> IndexStory(string compiledJson, string source)
    {
        var nodes = new List<StoryNode>();
        if (string.IsNullOrEmpty(compiledJson) || !IndexedSources.Add(source ?? string.Empty)) return nodes;

        Story reader, simulator;
        try
        {
            reader = InkFunctions.CreatePreviewStory(compiledJson);
            simulator = InkFunctions.CreatePreviewStory(compiledJson);
        }
        catch (Exception e)
        {
            Report($"{source}: compiled Ink could not be loaded: {e.Message}");
            return nodes;
        }

        var named = reader.mainContentContainer != null ? reader.mainContentContainer.namedContent : null;
        if (named == null) return nodes;

        foreach (var knotName in new List<string>(named.Keys))
        {
            // Ink keeps its variable declarations in "global decl"; real knots never contain spaces.
            if (string.IsNullOrEmpty(knotName) || knotName.IndexOf(' ') >= 0) continue;

            var problems = new List<string>();
            try
            {
                if (EventScript.TryParseStoryNode(knotName, reader.TagsForContentAtPath(knotName), out var node, problems)) nodes.Add(node);
                var knot = ReadKnot(reader, simulator, knotName, source, problems);
                if (Knots.ContainsKey(knotName)) problems.Add($"{knotName}: defined in more than one story; the one in '{source}' is used.");
                Knots[knotName] = knot;
            }
            catch (Exception e)
            {
                problems.Add($"{knotName}: could not be read ({e.Message}).");
            }
            foreach (var problem in problems) Report($"{source}: {problem}");
        }
        GameLog.Event($"Indexed '{source}': {nodes.Count} events, {Knots.Count} knots total", Log);
        return nodes;
    }

    private static Knot ReadKnot(Story reader, Story simulator, string knotName, string source, List<string> problems)
    {
        var knot = new Knot { name = knotName, source = source, screenType = EventScript.ScreenTypeOf(knotName) };

        reader.ResetState();
        reader.ChoosePathString(knotName);
        var content = new StringBuilder();
        while (reader.canContinue)
        {
            // Ink looks ahead past diverts, so leaving the knot shows up before its next line is read.
            string top = EventScript.TopLevelKnot(reader.state.currentPathString);
            if (!string.IsNullOrEmpty(top) && top != knotName)
            {
                knot.next = top;
                break;
            }
            content.Append(reader.Continue());
            if (reader.currentChoices.Count > 0) break;
        }
        knot.content = content.ToString();

        if (knot.next == null && reader.currentChoices.Count == 0)
        {
            string top = EventScript.TopLevelKnot(reader.state.currentPathString);
            if (!string.IsNullOrEmpty(top) && top != knotName) knot.next = top;
        }

        for (int i = 0; i < reader.currentChoices.Count; i++)
        {
            string text = reader.currentChoices[i].text;
            var choice = new Choice
            {
                text = text,
                label = EventScript.ChoiceLabel(text),
                target = ResolveChoiceTarget(simulator, knotName, i),
            };
            if (knot.screenType == ScreenType.Chorus)
            {
                var data = EventScript.ParseChorusChoice(text, choice.target, problems);
                if (data != null) knot.chorusChoices.Add(data);
            }
            else
            {
                choice.consequences = EventScript.ParseButtonConsequences(text, problems);
            }
            knot.choices.Add(choice);
        }
        return knot;
    }

    /// <summary>Top-level knot reached by taking a choice, found by simulating it; null when the story ends.</summary>
    private static string ResolveChoiceTarget(Story simulator, string knotName, int index)
    {
        simulator.ResetState();
        simulator.ChoosePathString(knotName);
        while (simulator.canContinue && simulator.currentChoices.Count == 0) simulator.Continue();
        if (simulator.currentChoices.Count <= index) return null;

        var targetPath = simulator.currentChoices[index].targetPath;
        string direct = EventScript.TopLevelKnot(targetPath != null ? targetPath.ToString() : null);
        if (!string.IsNullOrEmpty(direct) && direct != knotName) return direct;

        simulator.ChooseChoiceIndex(index);
        for (int guard = 0; guard < 256; guard++)
        {
            string top = EventScript.TopLevelKnot(simulator.state.currentPathString);
            if (!string.IsNullOrEmpty(top) && top != knotName) return top;
            if (!simulator.canContinue) break;
            simulator.Continue();
        }
        return null;
    }

    /// <summary>
    /// Cross-knot checks: every outcome and button leads to a knot that exists, choruses offer the
    /// Idealism and Realism slots the screen needs, unlock_event names an event entry knot, and every
    /// resource, section, building, technology or weather named exists (<see cref="EventContentCheck"/>).
    /// </summary>
    public static List<string> Validate(ICollection<string> eventKnots = null)
    {
        var problems = new List<string>();
        foreach (var knot in Knots.Values)
        {
            foreach (var choice in knot.choices)
            {
                if (!string.IsNullOrEmpty(choice.target) && !Knots.ContainsKey(choice.target)) problems.Add($"{knot.name}: '{choice.label}' leads to unknown knot '{choice.target}'.");
                CheckUnlocks(knot.name, choice.consequences, eventKnots, problems);
                EventContentCheck.Consequences($"{knot.name}: '{choice.label}'", choice.consequences, problems);
            }

            if (knot.screenType != ScreenType.Chorus) continue;
            if (knot.chorusChoices.Count == 0) problems.Add($"{knot.name}: chorus has no choices.");
            bool idealism = false, realism = false;
            foreach (var choice in knot.chorusChoices)
            {
                if (choice.choiceId == "idealism") idealism = true;
                else if (choice.choiceId == "realism") realism = true;
                else if (choice.choiceId != "pragmatism") problems.Add($"{knot.name}: choice '{choice.title}' must start with Idealism., Realism. or Pragmatism. (found '{choice.choiceId}').");
                foreach (var target in choice.TargetKnots())
                {
                    if (!Knots.ContainsKey(target)) problems.Add($"{knot.name}: '{choice.title}' leads to unknown knot '{target}'.");
                }
                CheckUnlocks(knot.name, choice.consequences, eventKnots, problems);
                CheckUnlocks(knot.name, choice.successConsequences, eventKnots, problems);
                CheckUnlocks(knot.name, choice.failureConsequences, eventKnots, problems);
                CheckUnlocks(knot.name, choice.critSuccessConsequences, eventKnots, problems);
                CheckUnlocks(knot.name, choice.critFailureConsequences, eventKnots, problems);
                CheckUnlocks(knot.name, choice.rareEventConsequences, eventKnots, problems);
                string owner = $"{knot.name}: '{choice.title}'";
                EventContentCheck.Conditions(owner, choice.requirements, problems);
                EventContentCheck.Conditions(owner, choice.requirementsCost, problems);
                EventContentCheck.Consequences(owner, choice.consequences, problems);
                EventContentCheck.Consequences(owner, choice.successConsequences, problems);
                EventContentCheck.Consequences(owner, choice.failureConsequences, problems);
                EventContentCheck.Consequences(owner, choice.critSuccessConsequences, problems);
                EventContentCheck.Consequences(owner, choice.critFailureConsequences, problems);
                EventContentCheck.Consequences(owner, choice.rareEventConsequences, problems);
            }
            if (knot.chorusChoices.Count > 0 && (!idealism || !realism)) problems.Add($"{knot.name}: a chorus needs both an Idealism and a Realism choice.");
        }
        return problems;
    }

    private static void CheckUnlocks(string knotName, List<EventConsequence> consequences, ICollection<string> eventKnots, List<string> problems)
    {
        if (consequences == null || eventKnots == null) return;
        foreach (var c in consequences)
        {
            if (c.type == EventConsequence.ConsequenceType.UnlockEvent && !eventKnots.Contains(c.targetName))
            {
                problems.Add($"{knotName}: unlock_event '{c.targetName}' is not an event entry knot.");
            }
        }
    }

    private static void Report(string problem)
    {
        ProblemList.Add(problem);
        GameLog.Warning(problem, Log);
    }
}
