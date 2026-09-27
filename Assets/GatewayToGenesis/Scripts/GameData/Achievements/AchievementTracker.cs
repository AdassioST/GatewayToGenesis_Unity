using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Which achievements are unlocked in this session, and the rule for unlocking: each one once, only if it exists, and
/// only through <see cref="AchievementTriggers"/>. Pure, so it is tested without Unity. It mirrors the world's reward
/// ledger (<see cref="WorldRewards"/>): <see cref="AchievementAward.Commit"/> marks it only after the ledger and the
/// lifetime profile hold the award, and loading a world rebuilds it with <see cref="Restore"/>.
/// </summary>
public class AchievementTracker
{
    private readonly Dictionary<string, AchievementDefinition> _byId = new Dictionary<string, AchievementDefinition>();
    private readonly List<string> _unlocked = new List<string>();

    public AchievementTracker(IEnumerable<AchievementDefinition> definitions)
    {
        foreach (var definition in definitions ?? Enumerable.Empty<AchievementDefinition>())
        {
            if (definition != null && !string.IsNullOrEmpty(definition.id)) _byId[definition.id] = definition;
        }
    }

    public IReadOnlyCollection<AchievementDefinition> All => _byId.Values;

    /// <summary>Unlocked ids, in the order they were earned.</summary>
    public IReadOnlyList<string> UnlockedIds => _unlocked;

    public bool IsUnlocked(string id) => id != null && _unlocked.Contains(id);

    public AchievementDefinition Get(string id) => id != null && _byId.TryGetValue(id, out var definition) ? definition : null;

    /// <summary>The achievements <paramref name="happening"/> would earn for the first time, without marking them.</summary>
    public List<AchievementDefinition> Candidates(AchievementEvent happening)
    {
        var candidates = new List<AchievementDefinition>();
        foreach (var id in AchievementTriggers.Earned(happening))
        {
            if (_byId.TryGetValue(id, out var definition) && !_unlocked.Contains(id)) candidates.Add(definition);
        }
        return candidates;
    }

    /// <summary>Apply a happening: the achievements it earns for the first time (empty when none). Marks them at once:
    /// the game awards through <see cref="AchievementAward.Commit"/> instead, which persists before marking.</summary>
    public List<AchievementDefinition> Report(AchievementEvent happening)
    {
        var earned = Candidates(happening);
        foreach (var achievement in earned) Unlock(achievement.id);
        return earned;
    }

    /// <summary>Unlock one achievement directly (debug tools, restoring a save). False if unknown or already unlocked.</summary>
    public bool Unlock(string id)
    {
        if (id == null || !_byId.ContainsKey(id) || _unlocked.Contains(id)) return false;
        _unlocked.Add(id);
        return true;
    }

    /// <summary>Replace the unlocked set. Renamed ids follow <see cref="AchievementAliases"/>; the ids that match no
    /// achievement in the note are returned (for a warning) rather than silently forgotten.</summary>
    public List<string> Restore(IEnumerable<string> unlockedIds)
    {
        _unlocked.Clear();
        var unknown = new List<string>();
        foreach (var id in unlockedIds ?? Enumerable.Empty<string>())
        {
            string current = AchievementAliases.Canonical(id);
            if (!Unlock(current) && !_byId.ContainsKey(current ?? "") && id != null && !unknown.Contains(id)) unknown.Add(id);
        }
        return unknown;
    }
}
