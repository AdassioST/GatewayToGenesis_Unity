using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The ballad actors of a story: a protagonist and co-protagonist roles, as an expedition has a Director and
/// companions. One for the story being told (<see cref="EventSystemLogic.CurrentCast"/>), and one kept per ballad so its
/// actors carry from verse to verse. Field names are part of the save: rename with care.
/// </summary>
[Serializable]
public class BalladCast
{
    public string protagonist;
    public List<string> coProtagonists = new List<string>();
    /// <summary>Co-protagonist roles the story offers.</summary>
    public int coSlots;
    /// <summary>How the protagonist came to the role ("the Supreme Commander (Defense)", "the Head of State", "their expedition", "chosen").</summary>
    public string how;

    public bool Empty => string.IsNullOrEmpty(protagonist);

    /// <summary>The protagonist, then the co-protagonists.</summary>
    public IEnumerable<string> Members
    {
        get
        {
            if (!string.IsNullOrEmpty(protagonist)) yield return protagonist;
            foreach (var co in coProtagonists) if (!string.IsNullOrEmpty(co)) yield return co;
        }
    }

    public bool Has(string legend) => legend != null && Members.Any(m => string.Equals(m, legend, StringComparison.OrdinalIgnoreCase));

    public int FreeCoSlots => Math.Max(0, coSlots - coProtagonists.Count(c => !string.IsNullOrEmpty(c)));

    public BalladCast Clone() => new BalladCast { protagonist = protagonist, coProtagonists = new List<string>(coProtagonists), coSlots = coSlots, how = how };
}

/// <summary>A ballad on its way: the verses told, its actors, and whether its finale has been sung. Saved.</summary>
[Serializable]
public class BalladRecord
{
    public string id;
    public string title;
    public string theme;
    public List<int> versesTold = new List<int>();
    public BalladCast cast;
    public bool complete;
    [SaveOptionalField] public string lastDevelopment;
    [SaveOptionalField] public string lastChoice;
    [SaveOptionalField] public string lastDate;
}

/// <summary>What a story's <c># cast:</c> tag asks for.</summary>
public struct CastSpec
{
    /// <summary>An area of the council's affairs ("defense"): the seat that answers for it plays the protagonist (<see cref="CouncilAreaRules"/>).</summary>
    public string area;
    /// <summary>A council seat by title ("Supreme Commander"); prefer <see cref="area"/>, which survives seats being renamed or replaced.</summary>
    public string seat;
    /// <summary>A legend by name.</summary>
    public string legend;
    /// <summary>The expedition that found the story plays it (the default whenever one did).</summary>
    public bool expedition;
    /// <summary>Co-protagonist roles; null for the default (a ballad's, or none).</summary>
    public int? co;
    /// <summary>No default: the player chooses (the Head of State is not called).</summary>
    public bool open;
}

/// <summary>
/// Ballad actors, with no scene state (tested in <c>BalladActorTests</c>): every event has faces, the legends who play
/// it. This is the local, one-story form of the Fate Stage idea; the Fate Stage itself (vault: Fate Stage.md, "the
/// dynamic framework through which World Events and Age Crises are performed") is reserved for super ballads and is
/// not built yet. Who plays a story is found in this order, and the player may change it before the story ends:
/// <list type="number">
/// <item>A ballad's actors carry from its earlier verses (while its protagonist is still free to play).</item>
/// <item>The expedition that found the story: its Director is the protagonist, its companions co-protagonists.</item>
/// <item>A legend the story names (<c># cast: legend:Name</c>).</item>
/// <item>The seat that answers for the area the story calls for (<c># cast: area:defense</c>: a caravan at the gates
/// is the defense's business), else the seat of the closest related area (<see cref="CouncilAreaRules"/>); or a seat
/// by title (<c>seat:Supreme Commander</c>).</item>
/// <item>The Head of State.</item>
/// <item>No one: the player assigns a legend (the council first).</item>
/// </list>
/// Rewards are Lyrical Fragments (<see cref="LyricalFragments"/>): <c>fragment:</c> consequences name roles, a story with
/// a theme pays every actor a little of it, and a ballad's finale pays its theme's primary ×5 and secondary ×3
/// (the vault's Role Archetype table) to the protagonist and a share to each co-protagonist.
/// </summary>
public static class BalladActors
{
    /// <summary>Roles a consequence can name instead of a legend.</summary>
    public const string Protagonist = "protagonist", Co = "co", CoProtagonists = "coprotagonists", Cast = "cast", Leader = "leader", Council = "council";

    public static readonly string[] Roles = { Protagonist, Co, CoProtagonists, Cast, Leader, Council };

    public static bool IsRole(string who) => who != null && Roles.Any(r => string.Equals(r, who.Trim().Replace("-", string.Empty).Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase));

    // ===== THE CAST TAG =====

    /// <summary>
    /// <c># cast:</c> entries, separated by commas or semicolons: <c>area:defense</c>, <c>seat:Title</c>,
    /// <c>legend:Name</c>, <c>expedition</c>, <c>co:N</c> and <c>open</c> (no default protagonist; the player chooses).
    /// </summary>
    public static CastSpec ParseCast(string text, ICollection<string> problems = null, string where = null)
    {
        var spec = new CastSpec();
        if (string.IsNullOrWhiteSpace(text)) return spec;
        foreach (var raw in text.Split(',', ';'))
        {
            string part = raw.Trim();
            if (part.Length == 0) continue;
            int colon = part.IndexOf(':');
            string key = (colon > 0 ? part.Substring(0, colon) : part).Trim().ToLowerInvariant();
            string value = colon > 0 ? part.Substring(colon + 1).Trim() : string.Empty;
            switch (key)
            {
                case "area": spec.area = value; break;
                case "seat": spec.seat = value; break;
                case "legend": spec.legend = value; break;
                case "expedition": spec.expedition = true; break;
                case "open": spec.open = true; break;
                case "co":
                    if (int.TryParse(value, out int co) && co >= 0) spec.co = co;
                    else problems?.Add($"{where}: cast 'co:{value}' needs a number of co-protagonist roles.");
                    break;
                default:
                    problems?.Add($"{where}: unknown cast entry '{part}' (area:name, seat:Title, legend:Name, expedition, co:N, open).");
                    break;
            }
            if ((key == "area" || key == "seat" || key == "legend") && value.Length == 0) problems?.Add($"{where}: cast '{part}' names no one.");
        }
        return spec;
    }

    // ===== CASTING =====

    /// <summary>What the scene knows when a story begins (delegates keep the rules free of scene objects).</summary>
    public struct Stage
    {
        /// <summary>A legend free to play (met, not lost, not away on another expedition).</summary>
        public Func<string, bool> available;
        /// <summary>The seated legend answering for an area, given who is free (<see cref="GovernmentLogic.AnswerFor"/>).</summary>
        public Func<string, Func<string, bool>, CouncilAreaRules.Answer> answerFor;
        /// <summary>The legend holding a seat by title (null when none holds it or no such seat is open).</summary>
        public Func<string, string> holderOf;
        /// <summary>An area's display name ("Defense"); null leaves the tag's own word.</summary>
        public Func<string, string> areaName;
        public string headOfState;
        public int balladCoProtagonists;
    }

    private static string AreaName(string area, Stage stage) => stage.areaName?.Invoke(area) ?? area;

    /// <summary>Cast a story (see the class summary for the order). Never null; empty when no one can play it.</summary>
    public static BalladCast Resolve(CastSpec spec, bool isBallad, BalladCast carried, BalladCast summoned, Stage stage)
    {
        Func<string, bool> free = n => !string.IsNullOrEmpty(n) && (stage.available == null || stage.available(n));
        var cast = new BalladCast { coSlots = spec.co ?? (isBallad ? Math.Max(0, stage.balladCoProtagonists) : 0) };

        if (carried != null && free(carried.protagonist))
        {
            cast.protagonist = carried.protagonist;
            cast.how = carried.how;
            cast.coSlots = Math.Max(cast.coSlots, carried.coSlots);
            foreach (var co in carried.coProtagonists) AddCo(cast, co, free);
            return cast;
        }
        if (summoned != null && free(summoned.protagonist))
        {
            cast.protagonist = summoned.protagonist;
            cast.how = summoned.how ?? "their expedition";
            // Everyone who walked there is on stage.
            cast.coSlots = Math.Max(cast.coSlots, summoned.coProtagonists.Count);
            foreach (var co in summoned.coProtagonists) AddCo(cast, co, free);
            return cast;
        }
        if (free(spec.legend))
        {
            cast.protagonist = spec.legend;
            cast.how = "named in the story";
            return cast;
        }
        if (!string.IsNullOrEmpty(spec.area) && stage.answerFor != null)
        {
            var answer = stage.answerFor(spec.area, free);
            if (answer.Found && free(answer.legend))
            {
                cast.protagonist = answer.legend;
                string area = AreaName(spec.area, stage);
                cast.how = answer.distance == 0 ? $"the {answer.seat} ({area})" : $"the {answer.seat} (closest to {area})";
                return cast;
            }
        }
        string holder = !string.IsNullOrEmpty(spec.seat) ? stage.holderOf?.Invoke(spec.seat) : null;
        if (free(holder))
        {
            cast.protagonist = holder;
            cast.how = "the " + spec.seat;
            return cast;
        }
        if (!spec.open && free(stage.headOfState))
        {
            string asked = !string.IsNullOrEmpty(spec.area) ? AreaName(spec.area, stage) : spec.seat;
            cast.protagonist = stage.headOfState;
            cast.how = string.IsNullOrEmpty(asked) ? "the Head of State" : $"the Head of State (no one answers for {asked})";
        }
        return cast;
    }

    /// <summary>Add a co-protagonist while a role is open (never the protagonist, never twice).</summary>
    public static bool AddCo(BalladCast cast, string legend, Func<string, bool> free = null)
    {
        if (cast == null || string.IsNullOrEmpty(legend) || cast.Has(legend) || cast.FreeCoSlots <= 0) return false;
        if (free != null && !free(legend)) return false;
        cast.coProtagonists.Add(legend);
        return true;
    }

    /// <summary>Make <paramref name="legend"/> the protagonist; a co-protagonist who takes the lead leaves its role, and the old protagonist takes it.</summary>
    public static void SetProtagonist(BalladCast cast, string legend, string how)
    {
        if (cast == null || string.IsNullOrEmpty(legend)) return;
        string old = cast.protagonist;
        int index = cast.coProtagonists.FindIndex(c => string.Equals(c, legend, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            if (!string.IsNullOrEmpty(old)) cast.coProtagonists[index] = old;
            else cast.coProtagonists.RemoveAt(index);
        }
        cast.protagonist = legend;
        cast.how = how;
    }

    /// <summary>Take a legend off the stage (the protagonist's role passes to the first co-protagonist).</summary>
    public static bool Remove(BalladCast cast, string legend)
    {
        if (cast == null || string.IsNullOrEmpty(legend)) return false;
        if (string.Equals(cast.protagonist, legend, StringComparison.OrdinalIgnoreCase))
        {
            cast.protagonist = cast.coProtagonists.FirstOrDefault(c => !string.IsNullOrEmpty(c));
            if (cast.protagonist != null) cast.coProtagonists.Remove(cast.protagonist);
            cast.how = cast.protagonist != null ? "stepped up" : null;
            return true;
        }
        return cast.coProtagonists.RemoveAll(c => string.Equals(c, legend, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    /// <summary>Who may be put on stage: the council first (the Head of State, then the seats), then the others, by name; those already on it left out.</summary>
    public static List<string> Candidates(IEnumerable<string> council, IEnumerable<string> others, BalladCast cast, Func<string, bool> available)
    {
        var list = new List<string>();
        foreach (var name in (council ?? Enumerable.Empty<string>()).Concat((others ?? Enumerable.Empty<string>()).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrEmpty(name) || list.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            if (cast != null && cast.Has(name)) continue;
            if (available != null && !available(name)) continue;
            list.Add(name);
        }
        return list;
    }

    // ===== ROLES IN CONSEQUENCES =====

    /// <summary>The legends a consequence names: a role of the cast, the council, or a legend by name.</summary>
    public static List<string> Targets(string who, BalladCast cast, Func<List<string>> council, string lastLeader = null)
    {
        string role = (who ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        var list = new List<string>();
        switch (role)
        {
            case Council:
                list.AddRange(council?.Invoke() ?? new List<string>());
                break;
            case Protagonist:
            case Leader:
                // "leader" is the older form: whoever led the expedition that found the story, now its protagonist.
                if (cast != null && !cast.Empty) list.Add(cast.protagonist);
                else if (role == Leader && !string.IsNullOrEmpty(lastLeader)) list.Add(lastLeader);
                break;
            case Co:
            case CoProtagonists:
                if (cast != null) list.AddRange(cast.coProtagonists.Where(c => !string.IsNullOrEmpty(c)));
                break;
            case Cast:
                if (cast != null) list.AddRange(cast.Members);
                break;
            default:
                if (!string.IsNullOrWhiteSpace(who)) list.Add(who.Trim());
                break;
        }
        return list;
    }

    /// <summary>"protagonist Lucidity" or "Vittoria Frauter Fragment of Vision": who and which kind. False when no kind ends it.</summary>
    public static bool SplitTarget(string target, out string who, out FragmentKind kind)
    {
        who = null;
        kind = FragmentKind.Meaning;
        string t = (target ?? string.Empty).Trim();
        int of = t.LastIndexOf(" Fragment of ", StringComparison.OrdinalIgnoreCase);
        if (of > 0 && LyricalFragments.TryParse(t.Substring(of + 1), out kind))
        {
            who = t.Substring(0, of).Trim();
            return who.Length > 0;
        }
        int space = t.LastIndexOf(' ');
        if (space <= 0 || !LyricalFragments.TryParse(t.Substring(space + 1), out kind)) return false;
        who = t.Substring(0, space).Trim();
        return who.Length > 0;
    }

    // ===== BALLADS =====

    /// <summary>The ballad's finale: the highest verse among its stories.</summary>
    public static int FinaleVerse(IEnumerable<StoryNode> stories, string ballad) =>
        (stories ?? Enumerable.Empty<StoryNode>()).Where(s => s != null && string.Equals(s.ballad, ballad, StringComparison.OrdinalIgnoreCase)).Select(s => s.verse).DefaultIfEmpty(0).Max();

    /// <summary>The first theme and title any verse of a ballad gives.</summary>
    public static (string theme, string title) BalladInfo(IEnumerable<StoryNode> stories, string ballad)
    {
        var verses = (stories ?? Enumerable.Empty<StoryNode>()).Where(s => s != null && string.Equals(s.ballad, ballad, StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.verse).ToList();
        return (verses.Select(v => v.theme).FirstOrDefault(t => !string.IsNullOrEmpty(t)), verses.Select(v => v.balladTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t)));
    }

    /// <summary>
    /// What a ballad's finale pays: its theme's reward to the protagonist, a share to each co-protagonist. Empty
    /// without a theme or a cast.
    /// </summary>
    public static List<(string legend, List<FragmentAward> reward)> BalladRewards(string theme, BalladCast cast, FragmentTuning tuning)
    {
        var result = new List<(string, List<FragmentAward>)>();
        tuning = tuning ?? new FragmentTuning();
        var full = LyricalFragments.ThemeReward(theme, tuning.balladScale);
        if (full.Count == 0 || cast == null || cast.Empty) return result;
        result.Add((cast.protagonist, full));
        var share = LyricalFragments.Share(full, tuning.coProtagonistShare);
        foreach (var co in cast.coProtagonists.Where(c => !string.IsNullOrEmpty(c))) result.Add((co, share));
        return result;
    }

    /// <summary>What a story with a theme pays each legend on stage (its primary kind); empty without a theme.</summary>
    public static List<FragmentAward> ThemedEventReward(string theme, FragmentTuning tuning)
    {
        tuning = tuning ?? new FragmentTuning();
        if (tuning.themedEventFragments <= 0 || !LyricalFragments.TryParseTheme(theme, out var primary, out _)) return new List<FragmentAward>();
        return new List<FragmentAward> { new FragmentAward(primary, tuning.themedEventFragments) };
    }

    /// <summary>"Protagonist", or "Co-protagonist" for a legend on stage in a supporting role; null off stage.</summary>
    public static string RoleOf(BalladCast cast, string legend)
    {
        if (cast == null || string.IsNullOrEmpty(legend)) return null;
        if (string.Equals(cast.protagonist, legend, StringComparison.OrdinalIgnoreCase)) return "Protagonist";
        return cast.coProtagonists.Any(c => string.Equals(c, legend, StringComparison.OrdinalIgnoreCase)) ? "Co-protagonist" : null;
    }
}
