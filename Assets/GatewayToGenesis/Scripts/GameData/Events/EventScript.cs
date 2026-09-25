using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// The event authoring grammar, parsed in one place. Every screen, the story builder, the validator and the
/// tests read Ink content through this class, so a new keyword or domain is added once and works everywhere.
///
/// Knot tags (on event entry knots):
///   # title: The Lost Caravan          # conditions: technology:Reconstruction; no_event_in_sevenths:2
///   # description: ...                # consequences: score:caravan_encountered +1; weather:clear
///   # event_type: Crisis               # cooldown: 8     # priority: 0     # locked: true
///
/// Choice text:  Type. Title &amp;D description &amp;C metadata
///   metadata is ';'-separated. Scalar keys: pillar, strength, challenge (pillar:strength), success, failure,
///   crit_success, crit_failure, rare_event, rare_event_percent. Group keys collect the entries that follow
///   them until the next key: requirements:, requirements:cost:, consequences:, and
///   success|failure|crit_success|crit_failure|rare_event:consequences:.
///
/// Conditions:   domain:target op value | domain:target value (means >=) | domain:target (means == 1)
///   Any <see cref="GameValues"/> domain works (resource, stat, score, technology, building, civic, weather, ...).
/// Consequences: type:target +value | technology:Name enlightened | weather:Profile[, permanent] | weather:clear
///   | unlock_event:knot. "duration:sevenths:N" (or "duration:N") times the consequence before it.
/// </summary>
public static class EventScript
{
    public const string DefaultChoiceDescription = "Make your choice.";

    /// <summary>A parsed chorus/verse choice text, split at its &amp;D and &amp;C markers.</summary>
    public struct ChoiceParts
    {
        /// <summary>Text before the markers ("Idealism. It's Hope."), shown on buttons.</summary>
        public string head;
        /// <summary>Text after &amp;D, or null.</summary>
        public string description;
        /// <summary>Text after &amp;C, or empty.</summary>
        public string metadata;
        /// <summary>Divert target written after "->" (raw Ink lines only), or null.</summary>
        public string destination;
    }

    private static readonly Regex ComparisonPattern = new Regex(@"^(?<target>.*?)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<value>[+-]?\d+)$", RegexOptions.CultureInvariant);
    private static readonly Regex TrailingNumberPattern = new Regex(@"^(?<target>.*?)\s*(?<value>[+-]?\d+)$", RegexOptions.CultureInvariant);

    private static readonly string[] OutcomeKeys = { "success", "failure", "crit_success", "crit_failure", "rare_event" };
    private static readonly HashSet<string> ScalarKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "pillar", "strength", "challenge", "rare_event_percent", "success", "failure", "crit_success", "crit_failure", "rare_event"
    };

    public static readonly string[] Pillars = { "aureus", "regalia", "waltz", "chorus" };

    private static readonly HashSet<string> BooleanDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "technology", "ritual_seventh", "civic", "government", "weather"
    };

    private static readonly Dictionary<string, EventCondition.ConditionType> ConditionTypes = new Dictionary<string, EventCondition.ConditionType>(StringComparer.OrdinalIgnoreCase)
    {
        { "score", EventCondition.ConditionType.ScoreCheck },
        { "stat", EventCondition.ConditionType.StatCheck },
        { "resource", EventCondition.ConditionType.ResourceCheck },
        { "technology", EventCondition.ConditionType.TechnologyCheck },
        { "seventh", EventCondition.ConditionType.SeventhCheck },
        { "phase", EventCondition.ConditionType.PhaseCheck },
        { "echo", EventCondition.ConditionType.EchoCheck },
        { "cycle", EventCondition.ConditionType.CycleCheck },
        { "ritual_seventh", EventCondition.ConditionType.RitualSeventhCheck },
        { "population", EventCondition.ConditionType.PopulationCheck },
        { "housing", EventCondition.ConditionType.HousingCheck },
        { "vagrants", EventCondition.ConditionType.VagrantsCheck },
        { "deaths", EventCondition.ConditionType.DeathsCheck },
        { "vagrant_deaths", EventCondition.ConditionType.VagrantDeathsCheck },
        { "true_deaths", EventCondition.ConditionType.TrueDeathsCheck },
        { "no_event_in_sevenths", EventCondition.ConditionType.NoEventInSeventhsCheck },
    };

    private static readonly Dictionary<string, EventConsequence.ConsequenceType> ConsequenceTypes = new Dictionary<string, EventConsequence.ConsequenceType>(StringComparer.OrdinalIgnoreCase)
    {
        { "score", EventConsequence.ConsequenceType.ScoreChange },
        { "stat", EventConsequence.ConsequenceType.StatChange },
        { "resource", EventConsequence.ConsequenceType.ResourceChange },
        { "production", EventConsequence.ConsequenceType.ProductionUnitChange },
        { "technology", EventConsequence.ConsequenceType.TechnologyEnlightened },
        { "unlock_event", EventConsequence.ConsequenceType.UnlockEvent },
        { "population", EventConsequence.ConsequenceType.PopulationChange },
        { "housing", EventConsequence.ConsequenceType.HousingChange },
        { "vagrants", EventConsequence.ConsequenceType.VagrantsChange },
        { "deaths", EventConsequence.ConsequenceType.DeathsChange },
        { "death_records_revision", EventConsequence.ConsequenceType.DeathRecordsRevision },
        { "production_percent", EventConsequence.ConsequenceType.ProductionPercentChange },
        { "production_percent_section", EventConsequence.ConsequenceType.ProductionPercentChangeSection },
        { "click_power", EventConsequence.ConsequenceType.ClickPowerChange },
        { "click_power_percent", EventConsequence.ConsequenceType.ClickPowerPercentChange },
        { "click_power_section", EventConsequence.ConsequenceType.ClickPowerChangeSection },
        { "click_power_percent_section", EventConsequence.ConsequenceType.ClickPowerPercentChangeSection },
        { "weather", EventConsequence.ConsequenceType.WeatherChange },
    };

    // Consequences whose target is implied by the type ("population:-10" needs no target name).
    private static readonly Dictionary<EventConsequence.ConsequenceType, string> ImpliedTargets = new Dictionary<EventConsequence.ConsequenceType, string>
    {
        { EventConsequence.ConsequenceType.PopulationChange, "population" },
        { EventConsequence.ConsequenceType.HousingChange, "housing" },
        { EventConsequence.ConsequenceType.VagrantsChange, "vagrants" },
        { EventConsequence.ConsequenceType.DeathsChange, "deaths" },
        { EventConsequence.ConsequenceType.DeathRecordsRevision, "deaths" },
    };

    // Tags copied into StoryNode.uiMetadata for the presentation layer.
    private static readonly HashSet<string> UiTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "splash_art", "event_type", "priority", "event_color", "button_text", "background", "speaker", "portrait", "layout"
    };
    private static readonly HashSet<string> StoryTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "title", "description", "conditions", "consequences", "cooldown", "locked", "screen_flow"
    };

    // ===== KNOTS =====

    /// <summary>Top-level knot of an Ink path: "hollow_caravan_verse_1.0.c-0" → "hollow_caravan_verse_1".</summary>
    public static string TopLevelKnot(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        path = path.Trim();
        int dot = path.IndexOf('.');
        return dot >= 0 ? path.Substring(0, dot) : path;
    }

    /// <summary>Screen a knot is shown on, by naming convention: *_chorus_N, *_bridge_N, *_outro, otherwise a verse.</summary>
    public static ScreenType ScreenTypeOf(string knot)
    {
        string name = TopLevelKnot(knot) ?? string.Empty;
        if (HasRole(name, "_chorus")) return ScreenType.Chorus;
        if (HasRole(name, "_outro")) return ScreenType.Outro;
        if (HasRole(name, "_bridge")) return ScreenType.Bridge;
        return ScreenType.Verse;
    }

    private static bool HasRole(string name, string role) =>
        name.EndsWith(role, StringComparison.OrdinalIgnoreCase) || name.IndexOf(role + "_", StringComparison.OrdinalIgnoreCase) >= 0;

    // ===== CONDITIONS =====

    public static bool TryGetConditionType(string domain, out EventCondition.ConditionType type) => ConditionTypes.TryGetValue(domain ?? string.Empty, out type);

    public static List<EventCondition> ParseConditions(string text, ICollection<string> problems = null)
    {
        var list = new List<EventCondition>();
        foreach (var part in Split(text))
        {
            var condition = ParseCondition(part, problems);
            if (condition != null) list.Add(condition);
        }
        return list;
    }

    /// <summary>One condition; null (with a problem noted) when it cannot be understood.</summary>
    public static EventCondition ParseCondition(string text, ICollection<string> problems = null)
    {
        string t = (text ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        SplitKey(t, out string domain, out string body);

        EventCondition condition;
        if (TryGetConditionType(domain, out var type))
        {
            condition = new EventCondition { type = type };
        }
        else if (GameValues.IsKnownDomain(domain))
        {
            condition = new EventCondition { type = EventCondition.ConditionType.ValueCheck, domain = domain.ToLowerInvariant() };
        }
        else
        {
            problems?.Add($"Unknown condition '{t}': '{domain}' is not a known domain.");
            return null;
        }

        if (condition.type == EventCondition.ConditionType.NoEventInSeventhsCheck)
        {
            if (!int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sevenths))
            {
                problems?.Add($"Condition '{t}' needs a number of sevenths.");
                return null;
            }
            condition.targetName = "sevenths";
            condition.requiredValue = sevenths;
            condition.comparison = ComparisonOperator.GreaterThanOrEqual;
            return condition;
        }

        var comparison = ComparisonPattern.Match(body);
        if (comparison.Success)
        {
            condition.targetName = TargetOrDomain(comparison.Groups["target"].Value, domain);
            condition.comparison = ParseOperator(comparison.Groups["op"].Value);
            condition.requiredValue = int.Parse(comparison.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return condition;
        }

        // "resource:Elderwood 5" means at least 5; yes/no domains keep numbers as part of the name ("Tier 2").
        var trailing = BooleanDomains.Contains(domain) ? Match.Empty : TrailingNumberPattern.Match(body);
        if (trailing.Success)
        {
            condition.targetName = TargetOrDomain(trailing.Groups["target"].Value, domain);
            condition.comparison = ComparisonOperator.GreaterThanOrEqual;
            condition.requiredValue = int.Parse(trailing.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return condition;
        }

        condition.targetName = TargetOrDomain(body, domain);
        condition.comparison = ComparisonOperator.Equals;
        condition.requiredValue = 1;
        return condition;
    }

    public static ComparisonOperator ParseOperator(string op)
    {
        switch ((op ?? string.Empty).Trim())
        {
            case "!=": return ComparisonOperator.NotEquals;
            case ">=": return ComparisonOperator.GreaterThanOrEqual;
            case "<=": return ComparisonOperator.LessThanOrEqual;
            case ">": return ComparisonOperator.GreaterThan;
            case "<": return ComparisonOperator.LessThan;
            default: return ComparisonOperator.Equals;
        }
    }

    // ===== CONSEQUENCES =====

    public static bool TryGetConsequenceType(string key, out EventConsequence.ConsequenceType type) => ConsequenceTypes.TryGetValue(key ?? string.Empty, out type);

    /// <summary>A consequence list; "duration:..." entries time the consequence written before them.</summary>
    public static List<EventConsequence> ParseConsequences(string text, ICollection<string> problems = null)
    {
        var list = new List<EventConsequence>();
        EventConsequence previous = null;
        foreach (var part in Split(text))
        {
            SplitKey(part, out string key, out string body);
            if (key.Equals("duration", StringComparison.OrdinalIgnoreCase))
            {
                int colon = body.LastIndexOf(':');
                string number = colon >= 0 ? body.Substring(colon + 1) : body;
                if (previous == null) problems?.Add($"'{part}' has no consequence before it to time.");
                else if (!int.TryParse(number.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sevenths)) problems?.Add($"'{part}' needs a number of sevenths.");
                else previous.durationSevenths = Math.Max(0, sevenths);
                continue;
            }
            var consequence = ParseConsequence(part, problems);
            if (consequence == null) continue;
            list.Add(consequence);
            previous = consequence;
        }
        return list;
    }

    /// <summary>One consequence; null (with a problem noted) when it cannot be understood.</summary>
    public static EventConsequence ParseConsequence(string text, ICollection<string> problems = null)
    {
        string t = (text ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        SplitKey(t, out string key, out string body);
        if (!TryGetConsequenceType(key, out var type))
        {
            problems?.Add($"Unknown consequence '{t}': '{key}' is not a consequence type.");
            return null;
        }

        switch (type)
        {
            case EventConsequence.ConsequenceType.WeatherChange:
            {
                if (body.Equals("clear", StringComparison.OrdinalIgnoreCase)) return new EventConsequence { type = type, targetName = "clear" };
                bool permanent = body.IndexOf("permanent", StringComparison.OrdinalIgnoreCase) >= 0;
                string profile = Regex.Replace(body, "permanent", string.Empty, RegexOptions.IgnoreCase).Replace(",", string.Empty).Trim();
                if (profile.Length == 0)
                {
                    problems?.Add($"'{t}' names no weather profile.");
                    return null;
                }
                return new EventConsequence { type = type, targetName = profile, value = permanent ? 1 : 0 };
            }
            case EventConsequence.ConsequenceType.TechnologyEnlightened:
            {
                string technology = Regex.Replace(body, @"\s*enlightened\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
                if (technology.Length == 0)
                {
                    problems?.Add($"'{t}' names no technology.");
                    return null;
                }
                return new EventConsequence { type = type, targetName = technology };
            }
            case EventConsequence.ConsequenceType.UnlockEvent:
            {
                string knot = TopLevelKnot(body);
                if (string.IsNullOrEmpty(knot))
                {
                    problems?.Add($"'{t}' names no event.");
                    return null;
                }
                return new EventConsequence { type = type, targetName = knot, value = 1 };
            }
        }

        var match = TrailingNumberPattern.Match(body);
        if (!match.Success)
        {
            problems?.Add($"Consequence '{t}' needs a signed amount at the end, e.g. '{key}:Name +5'.");
            return null;
        }
        string target = match.Groups["target"].Value.Trim();
        if (target.Length == 0 && !ImpliedTargets.TryGetValue(type, out target))
        {
            problems?.Add($"Consequence '{t}' needs a target before the amount.");
            return null;
        }
        return new EventConsequence
        {
            type = type,
            targetName = target,
            value = int.Parse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture)
        };
    }

    // ===== CHOICES =====

    /// <summary>Split a choice at its markers. Accepts compiled choice text or a raw "* ... -> target" line.</summary>
    public static ChoiceParts SplitChoice(string raw)
    {
        var parts = new ChoiceParts { metadata = string.Empty };
        string s = (raw ?? string.Empty).Trim();
        while (s.Length > 0 && (s[0] == '*' || s[0] == '+')) s = s.Substring(1).TrimStart();

        int arrow = s.LastIndexOf("->", StringComparison.Ordinal);
        if (arrow >= 0)
        {
            parts.destination = TopLevelKnot(s.Substring(arrow + 2));
            s = s.Substring(0, arrow).TrimEnd();
        }

        int d = s.IndexOf("&D", StringComparison.Ordinal);
        int c = s.IndexOf("&C", StringComparison.Ordinal);
        int headEnd = s.Length;
        if (d >= 0) headEnd = Math.Min(headEnd, d);
        if (c >= 0) headEnd = Math.Min(headEnd, c);
        parts.head = s.Substring(0, headEnd).Trim();

        if (d >= 0)
        {
            int end = c > d ? c : s.Length;
            parts.description = s.Substring(d + 2, end - d - 2).Trim();
        }
        if (c >= 0)
        {
            int end = d > c ? d : s.Length;
            parts.metadata = s.Substring(c + 2, end - c - 2).Trim();
        }
        return parts;
    }

    /// <summary>The player-facing label of a choice (markers and metadata removed).</summary>
    public static string ChoiceLabel(string raw) => SplitChoice(raw).head;

    /// <summary>Choice id from its type word: idealism, realism, pragmatism, or the word itself in snake_case.</summary>
    public static string ChoiceIdFor(string typeWord)
    {
        string lower = (typeWord ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Contains("idealis")) return "idealism";
        if (lower.Contains("realis")) return "realism";
        if (lower.Contains("pragmati")) return "pragmatism";
        return lower.Replace(' ', '_');
    }

    /// <summary>
    /// Metadata sections of a choice (after &amp;C). Scalar keys map to their value; groups map to their
    /// ';'-joined entries under "requirements", "requirements:cost", "consequences" and "&lt;outcome&gt;:consequences".
    /// </summary>
    public static Dictionary<string, string> ParseMetadata(string metadata, ICollection<string> problems = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string group = null;
        foreach (var part in Split(metadata))
        {
            SplitKey(part, out string key, out string rest);

            if (key.Equals("requirements", StringComparison.OrdinalIgnoreCase))
            {
                bool isCost = rest.StartsWith("cost:", StringComparison.OrdinalIgnoreCase) || rest.Equals("cost", StringComparison.OrdinalIgnoreCase);
                group = isCost ? "requirements:cost" : "requirements";
                Append(result, group, isCost ? (rest.Length > 5 ? rest.Substring(5) : string.Empty) : rest);
                continue;
            }
            if (key.Equals("consequences", StringComparison.OrdinalIgnoreCase))
            {
                group = "consequences";
                Append(result, group, rest);
                continue;
            }
            if (Array.IndexOf(OutcomeKeys, key.ToLowerInvariant()) >= 0
                && (rest.StartsWith("consequences:", StringComparison.OrdinalIgnoreCase) || rest.Equals("consequences", StringComparison.OrdinalIgnoreCase)))
            {
                group = key.ToLowerInvariant() + ":consequences";
                Append(result, group, rest.Length > 13 ? rest.Substring(13) : string.Empty);
                continue;
            }
            if (ScalarKeys.Contains(key))
            {
                group = null;
                result[key.ToLowerInvariant()] = rest;
                continue;
            }
            if (group != null)
            {
                Append(result, group, part);
                continue;
            }
            problems?.Add($"Unknown choice key '{key}' in '{part}'.");
        }
        return result;
    }

    /// <summary>Everything a chorus needs from one choice text.</summary>
    public static ChorusChoiceData ParseChorusChoice(string raw, string destination = null, ICollection<string> problems = null)
    {
        var parts = SplitChoice(raw);
        if (string.IsNullOrEmpty(parts.head))
        {
            problems?.Add($"Choice '{raw}' has no text.");
            return null;
        }

        string typeWord = parts.head, title = parts.head;
        int dot = parts.head.IndexOf('.');
        if (dot >= 0)
        {
            typeWord = parts.head.Substring(0, dot).Trim();
            title = parts.head.Substring(dot + 1).Trim();
        }

        var meta = ParseMetadata(parts.metadata, problems);
        var data = new ChorusChoiceData
        {
            choiceId = ChoiceIdFor(typeWord),
            title = title,
            description = string.IsNullOrEmpty(parts.description) ? DefaultChoiceDescription : parts.description,
            destinationPath = TopLevelKnot(destination ?? parts.destination),
            requirements = ParseConditions(Get(meta, "requirements"), problems),
            requirementsCost = ParseConditions(Get(meta, "requirements:cost"), problems),
            consequences = ParseConsequences(Get(meta, "consequences"), problems),
            successPath = TopLevelKnot(Get(meta, "success")),
            failurePath = TopLevelKnot(Get(meta, "failure")),
            critSuccessPath = TopLevelKnot(Get(meta, "crit_success")),
            critFailurePath = TopLevelKnot(Get(meta, "crit_failure")),
            rareEventPath = TopLevelKnot(Get(meta, "rare_event")),
            successConsequences = ParseConsequences(Get(meta, "success:consequences"), problems),
            failureConsequences = ParseConsequences(Get(meta, "failure:consequences"), problems),
            critSuccessConsequences = ParseConsequences(Get(meta, "crit_success:consequences"), problems),
            critFailureConsequences = ParseConsequences(Get(meta, "crit_failure:consequences"), problems),
            rareEventConsequences = ParseConsequences(Get(meta, "rare_event:consequences"), problems),
        };

        string rare = Get(meta, "rare_event_percent");
        if (!string.IsNullOrEmpty(rare))
        {
            if (int.TryParse(rare, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)) data.rareEventPercent = Math.Max(0, Math.Min(100, percent));
            else problems?.Add($"rare_event_percent '{rare}' is not a whole number.");
        }

        // Challenge: "pillar:waltz;strength:15" or the compact "challenge:waltz:15".
        string pillar = Get(meta, "pillar"), strength = Get(meta, "strength");
        string compact = Get(meta, "challenge");
        if (string.IsNullOrEmpty(pillar) && !string.IsNullOrEmpty(compact))
        {
            var segments = compact.Split(':');
            pillar = segments[0].Trim();
            if (segments.Length > 1) strength = segments[1].Trim();
        }
        if (!string.IsNullOrEmpty(pillar))
        {
            if (Array.IndexOf(Pillars, pillar.ToLowerInvariant()) < 0) problems?.Add($"Choice '{title}': '{pillar}' is not a pillar ({string.Join(", ", Pillars)}).");
            data.challengePillar = pillar.ToLowerInvariant();
            data.challengeStrength = int.TryParse(strength, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) && s > 0 ? s : 10;
            data.hasChallenge = true;
        }
        return data;
    }

    /// <summary>Consequences written on a single-button choice ("Accept&amp;C consequences: ...").</summary>
    public static List<EventConsequence> ParseButtonConsequences(string raw, ICollection<string> problems = null)
    {
        var parts = SplitChoice(raw);
        if (string.IsNullOrEmpty(parts.metadata)) return new List<EventConsequence>();
        return ParseConsequences(Get(ParseMetadata(parts.metadata, problems), "consequences"), problems);
    }

    // ===== STORY NODES =====

    /// <summary>"# key: value" or "key:value" → key and value. False when the tag has no key.</summary>
    public static bool TryParseTag(string tag, out string key, out string value)
    {
        key = value = null;
        string t = (tag ?? string.Empty).Trim().TrimStart('#').Trim();
        int colon = t.IndexOf(':');
        if (colon <= 0) return false;
        key = t.Substring(0, colon).Trim().ToLowerInvariant();
        value = t.Substring(colon + 1).Trim();
        return true;
    }

    /// <summary>
    /// A story node from an entry knot's tags. Knots without event tags are story beats and return false
    /// quietly; knots with some but not all of title, conditions and event_type are reported.
    /// </summary>
    public static bool TryParseStoryNode(string knotName, IEnumerable<string> tags, out StoryNode node, ICollection<string> problems = null)
    {
        node = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ui = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (!TryParseTag(tag, out string key, out string value)) continue;
                if (UiTags.Contains(key)) ui[key] = value;
                if (StoryTags.Contains(key) || UiTags.Contains(key)) values[key] = value;
                else problems?.Add($"{knotName}: unknown tag '{key}'.");
            }
        }

        string title = Get(values, "title"), conditions = Get(values, "conditions"), eventType = Get(values, "event_type");
        bool anyEventTag = !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(conditions) || !string.IsNullOrEmpty(eventType);
        if (!anyEventTag) return false;
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(conditions) || string.IsNullOrEmpty(eventType))
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(title)) missing.Add("title");
            if (string.IsNullOrEmpty(conditions)) missing.Add("conditions");
            if (string.IsNullOrEmpty(eventType)) missing.Add("event_type");
            problems?.Add($"{knotName}: event tags are incomplete (missing {string.Join(", ", missing)}); it will never trigger.");
            return false;
        }
        if (!Enum.TryParse(eventType, true, out EventType _)) problems?.Add($"{knotName}: event_type '{eventType}' is not one of {string.Join(", ", Enum.GetNames(typeof(EventType)))}.");

        node = new StoryNode
        {
            nodeName = knotName,
            storyTitle = title,
            storyDescription = Get(values, "description") ?? string.Empty,
            isUnlocked = !IsTrue(Get(values, "locked")),
            cooldownSevenths = ParseInt(Get(values, "cooldown"), 0, $"{knotName}: cooldown", problems),
            priority = ParseInt(Get(values, "priority"), 0, $"{knotName}: priority", problems),
            storyConditions = ParseConditions(conditions, problems),
            storyConsequences = ParseConsequences(Get(values, "consequences"), problems),
            uiMetadata = ui,
        };
        node.screenFlow.Add(new ScreenFlowStep
        {
            flowType = ScreenFlowStep.FlowType.Splash,
            screenId = knotName + "_splash",
            inkKnot = knotName,
            displayDuration = 3f,
            waitForInput = false
        });
        return true;
    }

    // ===== HELPERS =====

    private static IEnumerable<string> Split(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (var part in text.Split(';'))
        {
            string t = part.Trim();
            if (t.Length > 0) yield return t;
        }
    }

    private static void SplitKey(string text, out string key, out string rest)
    {
        int colon = text.IndexOf(':');
        key = (colon > 0 ? text.Substring(0, colon) : text).Trim();
        rest = colon > 0 ? text.Substring(colon + 1).Trim() : string.Empty;
    }

    private static string TargetOrDomain(string target, string domain)
    {
        string t = (target ?? string.Empty).Trim();
        return t.Length > 0 ? t : domain.ToLowerInvariant();
    }

    private static void Append(Dictionary<string, string> map, string key, string value)
    {
        value = (value ?? string.Empty).Trim();
        if (!map.TryGetValue(key, out string existing) || string.IsNullOrEmpty(existing)) map[key] = value;
        else if (value.Length > 0) map[key] = existing + ";" + value;
    }

    private static string Get(Dictionary<string, string> map, string key) => map.TryGetValue(key, out string value) ? value : null;

    private static bool IsTrue(string value) => value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static int ParseInt(string value, int fallback, string what, ICollection<string> problems)
    {
        if (string.IsNullOrEmpty(value)) return fallback;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)) return result;
        problems?.Add($"{what} '{value}' is not a whole number.");
        return fallback;
    }

    /// <summary>Readable one-line form of a condition, for logs and validation messages.</summary>
    public static string Describe(EventCondition c)
    {
        if (c == null) return "(none)";
        var sb = new StringBuilder();
        sb.Append(c.type == EventCondition.ConditionType.ValueCheck ? c.domain : EventCondition.DomainOf(c.type));
        sb.Append(':').Append(c.targetName).Append(' ').Append(GameValues.Symbol(c.comparison)).Append(' ').Append(c.requiredValue);
        return sb.ToString();
    }
}
