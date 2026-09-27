using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's achievements: the vault's list (Resources/Achievements/Achievements.md, copied verbatim from
/// Worldbuilding/Events/Achievement.md by Tools > Gateway to Genesis > Import Lore From Vault) and which are unlocked.
///
/// Systems report what happens with <see cref="Report"/> (static, safe with no scene object); the rules of which
/// happening earns which achievement are <see cref="AchievementTriggers"/>, the state <see cref="AchievementTracker"/>.
/// Listeners (the unlock toast, the Library's Achievements shelf) subscribe to <see cref="Unlocked"/>.
/// </summary>
public static class Achievements
{
    public const string NotePath = "Achievements/Achievements";
    private const LogChannel Log = LogChannel.Events;

    private static AchievementTracker _tracker;
    private static readonly List<string> _problems = new List<string>();

    /// <summary>Raised once per achievement, the first time it is earned.</summary>
    public static event Action<AchievementDefinition> Unlocked;

    // Statics survive between play sessions when domain reload is disabled; the note may have changed too.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Invalidate()
    {
        _tracker = null;
        _problems.Clear();
        Unlocked = null;
    }

    public static AchievementTracker Tracker
    {
        get
        {
            if (_tracker == null) Load();
            return _tracker;
        }
    }

    /// <summary>Problems in the achievement note (shape breaks to fix in the vault), for ContentValidator.</summary>
    public static IReadOnlyList<string> Problems
    {
        get
        {
            if (_tracker == null) Load();
            return _problems;
        }
    }

    /// <summary>
    /// Tell the achievements something happened, after it has committed. Awards (and announces) whatever it earns for
    /// the first time through <see cref="AchievementAward.Commit"/>; an award whose profile write fails is logged and
    /// left unmarked, so the same happening or the next save completes it without paying twice.
    /// </summary>
    public static void Report(AchievementEvent happening)
    {
        if (SaveSession.Restoring || SaveSession.Current == null) return;
        var earned = Tracker.Candidates(happening);
        if (earned.Count == 0) return;
        var evidence = AwardEvidence.Of(happening, GameAge.Id, GameAge.Number, DateTime.UtcNow);
        bool any = false;
        foreach (var achievement in earned)
        {
            bool announced;
            try { announced = SaveSession.Earn(achievement.id, evidence); }
            catch (Exception e)
            {
                GameLog.Error($"Achievement '{achievement.id}' could not be recorded ({e.Message}); it completes on the next report or save.", Log);
                continue;
            }
            if (!announced) continue;
            any = true;
            GameLog.Event($"Achievement unlocked: {AchievementNote.Plain(achievement.title)}", Log);
            Unlocked?.Invoke(achievement);
        }
        if (any) Library.Invalidate(); // the Game Wiki's Achievements shelf shows the new status
    }

    private static void Load()
    {
        _problems.Clear();
        string markdown = GameCatalog.LoadText(NotePath);
        if (markdown == null)
        {
            _problems.Add($"There is no achievement list: Resources/{NotePath}.md is missing (run Tools > Gateway to Genesis > Import Lore From Vault)");
            _tracker = new AchievementTracker(null);
            return;
        }
        var parsed = AchievementNote.Parse(markdown);
        _problems.AddRange(parsed.problems);
        _tracker = new AchievementTracker(parsed.achievements);
    }
}
