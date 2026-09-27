using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>Local authored Lesser Opus definitions. Names are placeholder story content, not imported canon.</summary>
public sealed class LesserOpusDefinition
{
    public readonly string id, ballad, name, description, futureTitle;
    public LesserOpusDefinition(string id, string ballad, string name, string description, string futureTitle)
    { this.id = id; this.ballad = ballad; this.name = name; this.description = description; this.futureTitle = futureTitle; }
}

public static class LesserOpusCatalog
{
    public static readonly IReadOnlyList<LesserOpusDefinition> All = new[] {
        new LesserOpusDefinition("orchard-seeds", "sb_orchard", "Three Empty Seed-Pouches", "Returned the forgotten crop record to the growers instead of letting sweetness erase it.", "Keeper of the Unsown"),
        new LesserOpusDefinition("orchard-harvest", "sb_orchard", "The Harvest's Full Account", "Named the crops lost beside the meals gained, preserving the cost of a necessary harvest.", "The Honest Harvester"),
        new LesserOpusDefinition("crossing-measure", "sb_crossing", "A Map with Two Names", "Preserved both guides' corrections and made uncertainty part of a shared road.", "The One Who Measured Twice"),
        new LesserOpusDefinition("crossing-promise", "sb_crossing", "Where We Can See One Another", "Kept the guides' promise to return together beside the route instructions.", "Keeper of the Return Path"),
        new LesserOpusDefinition("threshold-search", "sb_threshold", "The Question That Kept Travelling", "Kept searching for the absent while making a waiting place hospitable to the living.", "Bearer of the Unanswered Name"),
        new LesserOpusDefinition("threshold-welcome", "sb_threshold", "A Chair Turned Sideways", "Made room for a stranger without replacing the person still remembered at the threshold.", "The One Who Kept a Place"),
        new LesserOpusDefinition("stillhour-record", "sb_stillhour", "The Bowl in the Quiet", "Left uncertainty visible rather than inventing an ending for a preserved moment.", "Witness of the Honest Blank"),
        new LesserOpusDefinition("stillhour-voice", "sb_stillhour", "The Voice Outside", "Recorded how living companions brought one another home from the quiet.", "The Answering Voice"),
    };

    public static LesserOpusDefinition Find(string id) => All.FirstOrDefault(d => string.Equals(d.id, id, StringComparison.Ordinal));

    // Ink: lesser_opus:cast orchard-seeds +1. No arbitrary free-text titles or numeric award quantities.
    public static bool SplitTarget(string target, out string who, out LesserOpusDefinition definition)
    {
        who = null; definition = null;
        int split = (target ?? string.Empty).LastIndexOf(' ');
        if (split < 1) return false;
        who = target.Substring(0, split).Trim();
        definition = Find(target.Substring(split + 1).Trim());
        return who.Length > 0 && definition != null;
    }
}

[Serializable]
public sealed class BalladParticipation
{
    public string storyId, balladId, role;
    public int verse, age;
}

[Serializable]
public sealed class EarnedLesserOpus
{
    // Snapshot authored wording so later editorial renames do not rewrite the legend's history.
    public string id, balladId, name, description, futureTitle, sourceStory, resolutionRole;
    public int age;
    public List<BalladParticipation> participation = new List<BalladParticipation>();
}

/// <summary>
/// Saved per legend, retained after loss. Lesser Opus are earned by default at an authored resolution.
/// Associated titles are locked future unlockables: no title ownership, fragment debit, passive bonus,
/// Magnum Opus or achievement is implied by this local narrative recognition.
/// </summary>
[Serializable]
public sealed class LegendBalladHistory
{
    public List<BalladParticipation> participation = new List<BalladParticipation>();
    public List<EarnedLesserOpus> lesserOpus = new List<EarnedLesserOpus>();

    public bool Record(string story, string ballad, int verse, string role, int age)
    {
        if (string.IsNullOrEmpty(story) || string.IsNullOrEmpty(ballad) || verse < 1 || string.IsNullOrEmpty(role) ||
            participation.Any(p => p.storyId == story)) return false;
        participation.Add(new BalladParticipation { storyId = story, balladId = ballad, verse = verse, role = role, age = age });
        return true;
    }

    public bool Resolve(LesserOpusDefinition definition, string story, string ballad, int verse, bool finale, int age)
    {
        if (definition == null || !finale || definition.ballad != ballad || verse < 1 ||
            lesserOpus.Any(o => o.balladId == ballad)) return false;
        var resolution = participation.FirstOrDefault(p => p.storyId == story && p.balladId == ballad && p.verse == verse);
        if (resolution == null) return false; // no award to an uncast legend, or one replaced before resolution
        lesserOpus.Add(new EarnedLesserOpus {
            id = definition.id, balladId = ballad, name = definition.name, description = definition.description,
            futureTitle = definition.futureTitle, sourceStory = story, resolutionRole = resolution.role, age = age,
            participation = participation.Where(p => p.balladId == ballad).Select(p => new BalladParticipation {
                storyId = p.storyId, balladId = p.balladId, verse = p.verse, role = p.role, age = p.age
            }).ToList()
        });
        return true;
    }

    public string Describe()
    {
        if (lesserOpus.Count == 0) return string.Empty;
        var text = new StringBuilder("Lesser Opus");
        foreach (var opus in lesserOpus)
        {
            text.Append("\n").Append(opus.name).Append(" — ").Append(opus.resolutionRole)
                .Append("; Age ").Append(opus.age).Append("\n").Append(opus.description);
            text.Append("\nParticipation: ").Append(string.Join(", ", opus.participation.OrderBy(p => p.verse)
                .Select(p => "verse " + p.verse + " (" + p.role + ")")));
            text.Append("\nTitle — Locked: ").Append(opus.futureTitle)
                .Append(". Requires Lyrical Fragments; unlocking is not yet available.");
        }
        return text.ToString();
    }
}
