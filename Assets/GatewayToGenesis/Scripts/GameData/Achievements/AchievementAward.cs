using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The one way an achievement is awarded, in an order that survives interruption (U00 award contract).
///
/// 1. The world's reward ledger records it with its Anchors and evidence (in memory; the next world save persists it).
/// 2. The lifetime profile merges it and is written.
/// 3. Only then the session's <see cref="AchievementTracker"/> marks it, and the caller announces it.
///
/// Every step is idempotent, so a failure at any point converges on retry without paying twice: a failed profile write
/// leaves the Tracker unmarked, the same happening (or the next world save, which merges the ledger into the profile)
/// finishes the award, and the ledger refuses a second payment. A crash before the world is saved loses the world's
/// record along with the happening that earned it; replaying that happening pays that world once, while the profile
/// union stays single. Loading rebuilds the Tracker from the ledger (<see cref="AchievementTracker.Restore"/>) and
/// never emits evidence. Pure, so it is tested without Unity.
/// </summary>
public static class AchievementAward
{
    /// <summary>Award <paramref name="id"/>. True when the Tracker newly marked it: announce it exactly then.
    /// Throws what <paramref name="writeProfile"/> throws; nothing is marked in the Tracker in that case.</summary>
    public static bool Commit(string id, int anchors, AwardEvidence evidence, AchievementTracker tracker, WorldRewards world, LifetimeProfile profile, Action writeProfile)
    {
        if (string.IsNullOrEmpty(id) || tracker == null || tracker.Get(id) == null || tracker.IsUnlocked(id)) return false;
        if (world != null)
        {
            if (world.Earn(id, anchors) && evidence != null)
            {
                evidence.achievement = id;
                world.Record(evidence);
            }
        }
        if (profile != null && !profile.unlocked.Contains(id))
        {
            profile.Merge(new[] { id });
            writeProfile?.Invoke();
        }
        return tracker.Unlock(id);
    }
}

/// <summary>
/// What earned an achievement, kept in the world's reward ledger (one per award, <see cref="WorldRewards.evidence"/>).
/// Identities are stable ids, never display text; restored saves carry these records but never create them.
/// </summary>
[Serializable]
public sealed class AwardEvidence
{
    /// <summary>Version of this record's shape (<see cref="AchievementEvent.EvidenceVersion"/> when written).</summary>
    public int version = AchievementEvent.EvidenceVersion;
    public string achievement;
    /// <summary>The <see cref="AchievementSignal"/> name that earned it (a name, so reordering the enum is safe).</summary>
    public string signal;
    /// <summary>Stable key of the committed happening (e.g. "age-passed:age-of-desolation:1").</summary>
    public string source;
    /// <summary>Stable id of the entity concerned (a legend, an Age, a story); empty when none.</summary>
    public string subject;
    public string ageId;
    public int ageNumber;
    public string utc;

    public static AwardEvidence Of(AchievementEvent happening, string ageId, int ageNumber, DateTime utc) => new AwardEvidence
    {
        signal = happening.signal.ToString(),
        source = happening.source ?? "",
        subject = happening.subject ?? "",
        ageId = ageId ?? "",
        ageNumber = ageNumber,
        utc = utc.ToString("O"),
    };
}

/// <summary>
/// Renamed achievements. Ids are slugs of vault titles, so a retitled achievement gets a new id: add
/// <c>["old-id"] = "new-id"</c> here in the same change, and saves, profiles and the Tracker carry the old unlock over
/// instead of dropping it (or paying its Anchors twice). Never remove an entry; <see cref="Problems"/> is checked by
/// ContentTests against the imported note.
/// </summary>
public static class AchievementAliases
{
    public static readonly IReadOnlyDictionary<string, string> Renamed = new Dictionary<string, string>
    {
    };

    /// <summary>The current id for <paramref name="id"/> (itself when it was never renamed).</summary>
    public static string Canonical(string id, IReadOnlyDictionary<string, string> renamed = null)
    {
        renamed = renamed ?? Renamed;
        var seen = new HashSet<string>();
        while (id != null && renamed.TryGetValue(id, out var next) && seen.Add(id)) id = next;
        return id;
    }

    /// <summary>Alias mistakes: an alias that is still a live id, one whose target is unknown, and cycles.</summary>
    public static List<string> Problems(IEnumerable<string> liveIds, IReadOnlyDictionary<string, string> renamed = null)
    {
        renamed = renamed ?? Renamed;
        var live = new HashSet<string>(liveIds ?? Enumerable.Empty<string>());
        var problems = new List<string>();
        foreach (var alias in renamed)
        {
            if (live.Contains(alias.Key)) problems.Add($"Achievement alias '{alias.Key}' is also a current achievement id.");
            string target = Canonical(alias.Key, renamed);
            if (renamed.ContainsKey(target)) problems.Add($"Achievement alias '{alias.Key}' is part of a rename cycle.");
            else if (!live.Contains(target)) problems.Add($"Achievement alias '{alias.Key}' leads to '{target}', which is not in the achievement note.");
        }
        return problems;
    }

    /// <summary>Rename old ids in a profile's unlock list (keeping first-earned order, no duplicates).</summary>
    public static bool Migrate(LifetimeProfile profile, IReadOnlyDictionary<string, string> renamed = null)
    {
        if (profile?.unlocked == null) return false;
        var migrated = Distinct(profile.unlocked.Select(id => Canonical(id, renamed)));
        bool changed = !migrated.SequenceEqual(profile.unlocked);
        profile.unlocked = migrated;
        return changed;
    }

    /// <summary>
    /// Rename old ids in a world's reward ledger. When both an old and a new id were earned (the alias arrived late),
    /// they merge into the first award and keep the sum of their Anchors: migration never mints or removes currency.
    /// </summary>
    public static bool Migrate(WorldRewards rewards, IReadOnlyDictionary<string, string> renamed = null)
    {
        if (rewards == null) return false;
        bool changed = false;
        var awards = new List<AnchorAward>();
        foreach (var award in rewards.awards)
        {
            string id = Canonical(award.achievement, renamed);
            changed |= id != award.achievement;
            var kept = awards.FirstOrDefault(a => a.achievement == id);
            if (kept != null) { kept.amount = checked(kept.amount + award.amount); changed = true; }
            else awards.Add(new AnchorAward { achievement = id, amount = award.amount });
        }
        var unlocked = Distinct(rewards.unlocked.Select(id => Canonical(id, renamed)));
        changed |= !unlocked.SequenceEqual(rewards.unlocked);
        var evidence = new List<AwardEvidence>();
        foreach (var record in rewards.evidence ?? new List<AwardEvidence>())
        {
            if (record == null) continue;
            string id = Canonical(record.achievement, renamed);
            changed |= id != record.achievement;
            record.achievement = id;
            if (evidence.All(e => e.achievement != id)) evidence.Add(record); else changed = true;
        }
        rewards.awards = awards; rewards.unlocked = unlocked; rewards.evidence = evidence;
        return changed;
    }

    private static List<string> Distinct(IEnumerable<string> ids)
    {
        var result = new List<string>();
        foreach (var id in ids) if (id != null && !result.Contains(id)) result.Add(id);
        return result;
    }
}

/// <summary>
/// The versioned set "100% Gateway To Genesis" counts (D04/D11). Version 1 is every achievement in the note except the
/// final one itself, so the last achievement can never require itself. Whether mutually exclusive endings or #96 join
/// the same final transaction is still D04/D11: bump <see cref="Version"/> when the set's rule changes, so progress
/// is always reported against a named set rather than a moving catalog.
/// </summary>
public static class AchievementCompletion
{
    public const int Version = 1;
    public const string FinalId = "gateway-to-genesis";

    public static List<string> Set(IEnumerable<AchievementDefinition> all) =>
        (all ?? Enumerable.Empty<AchievementDefinition>()).Where(a => a != null && !string.IsNullOrEmpty(a.id) && a.id != FinalId)
            .OrderBy(a => a.order).Select(a => a.id).Distinct().ToList();

    /// <summary>How many of the set <paramref name="unlocked"/> holds, and the set's size.</summary>
    public static (int earned, int total) Progress(IEnumerable<AchievementDefinition> all, IEnumerable<string> unlocked)
    {
        var set = Set(all);
        var have = new HashSet<string>(unlocked ?? Enumerable.Empty<string>());
        return (set.Count(have.Contains), set.Count);
    }
}
