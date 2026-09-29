using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>A view of discovered ballads, using the scheduler's live gates without revealing future prose.</summary>
public static class BalladJournal
{
    public static string Date
    {
        get
        {
            var time = TimeSystemLogic.Instance;
            return time == null ? "" : $"Cycle {time.CurrentCycle}, Echo {time.CurrentEcho}, Phase {time.CurrentPhase}, Seventh {time.CurrentSeventh}";
        }
    }

    public static IEnumerable<StoryNode> NextVerses(BalladRecord ballad, IEnumerable<StoryNode> stories)
    {
        if (ballad.complete) return Enumerable.Empty<StoryNode>();
        var remaining = stories.Where(s => s != null && string.Equals(s.ballad, ballad.id, StringComparison.OrdinalIgnoreCase) && !ballad.versesTold.Contains(s.verse)).ToList();
        if (remaining.Count == 0) return remaining;
        int next = remaining.Min(s => s.verse);
        return remaining.Where(s => s.verse == next);
    }

    public static string Describe(BalladRecord ballad, EventVolumeManager volumes)
    {
        var text = new StringBuilder();
        text.AppendLine(TooltipText.Heading(ballad.title ?? ballad.id));
        text.AppendLine(ballad.complete ? "Completed" : $"Ongoing · {ballad.versesTold.Count} verses lived");
        if (ballad.cast != null)
            foreach (var actor in ballad.cast.Members) text.AppendLine($"{BalladActors.RoleOf(ballad.cast, actor)}: {actor}");
        text.AppendLine("Last developed: " + (ballad.lastDevelopment ?? "Earlier verses recorded before the journal was added."));
        if (!string.IsNullOrEmpty(ballad.lastChoice)) text.AppendLine("Your choice: " + ballad.lastChoice);
        if (!string.IsNullOrEmpty(ballad.lastDate)) text.AppendLine(ballad.lastDate);
        if (!ballad.complete && volumes != null)
        {
            var next = NextVerses(ballad, volumes.GetVolumes().SelectMany(v => v.storyNodes)).ToList();
            if (next.Count == 0) text.AppendLine("Awaiting another verse.");
            foreach (var story in next)
            {
                var gates = volumes.StoryBarriers(story);
                text.AppendLine($"Next verse {story.verse}: " + (gates.Count == 0 ? "Ready to continue" : string.Join("; ", gates)));
            }
        }
        return text.ToString().TrimEnd();
    }
}
