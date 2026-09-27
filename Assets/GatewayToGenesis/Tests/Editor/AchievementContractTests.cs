using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The U00 award contract (<see cref="AchievementAward"/>): an unlock survives a failed profile write, a retry, a
/// crash before the world is saved and a reload without losing or duplicating rewards; renamed ids carry over
/// (<see cref="AchievementAliases"/>); evidence is kept once per award; the completion set never contains itself.
/// Plus X05's Age gates: EraUnlock civic requirements and the capability split between legend Ornaments and
/// Ornamental Magic (<see cref="AgeCapabilities"/>, owner correction D13).
/// </summary>
public class AchievementContractTests
{
    private const string Madman = "an-eccentric-madman";

    private static AchievementTracker Tracker(params string[] ids) =>
        new AchievementTracker(ids.Select((id, i) => new AchievementDefinition { id = id, title = id, order = i }));

    private static AwardEvidence Evidence(string source = "library:opened") =>
        AwardEvidence.Of(AchievementEvent.Of(AchievementSignal.LibraryOpened).From(source), "age-of-desolation", 0, new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc));

    private static WorldRewards RoundTrip(WorldRewards rewards) =>
        (WorldRewards)SaveStateCodec.Read(SaveStateCodec.Write(rewards, typeof(WorldRewards)), typeof(WorldRewards));

    // ===== THE AWARD TRANSACTION =====

    [Test]
    public void Commit_AwardsOnceWithEvidenceAndMarksTheTrackerLast()
    {
        var tracker = Tracker(Madman); var world = new WorldRewards(); var profile = new LifetimeProfile();
        int writes = 0;
        Assert.IsTrue(AchievementAward.Commit(Madman, 10, Evidence(), tracker, world, profile, () => writes++));
        Assert.IsFalse(AchievementAward.Commit(Madman, 10, Evidence("again"), tracker, world, profile, () => writes++), "announced once");
        Assert.AreEqual(10, world.Balance);
        Assert.AreEqual(1, writes, "the profile is written once");
        CollectionAssert.AreEqual(new[] { Madman }, profile.unlocked);
        Assert.AreEqual("library:opened", world.EvidenceFor(Madman).source, "the first evidence stands");
        Assert.AreEqual("LibraryOpened", world.EvidenceFor(Madman).signal);
        Assert.AreEqual(AchievementEvent.EvidenceVersion, world.EvidenceFor(Madman).version);
        world.Validate();
    }

    [Test]
    public void Commit_UnknownIdsAreNeverPaid()
    {
        var world = new WorldRewards();
        Assert.IsFalse(AchievementAward.Commit("not-an-achievement", 10, Evidence(), Tracker(Madman), world, new LifetimeProfile(), null));
        Assert.AreEqual(0, world.Balance);
        CollectionAssert.IsEmpty(world.unlocked);
    }

    [Test]
    public void Commit_AFailedProfileWriteLeavesItUnannouncedAndARetryPaysNothingTwice()
    {
        var tracker = Tracker(Madman); var world = new WorldRewards(); var profile = new LifetimeProfile();
        Assert.Throws<IOException>(() => AchievementAward.Commit(Madman, 10, Evidence(), tracker, world, profile, () => throw new IOException("disk full")));
        Assert.IsFalse(tracker.IsUnlocked(Madman), "not announced while the profile is unwritten");
        Assert.AreEqual(10, world.Balance, "the world ledger holds it");

        int writes = 0;
        Assert.IsTrue(AchievementAward.Commit(Madman, 10, Evidence("retry"), tracker, world, profile, () => writes++), "the retry announces it");
        Assert.AreEqual(10, world.Balance, "and pays nothing more");
        Assert.AreEqual("library:opened", world.EvidenceFor(Madman).source);
        Assert.AreEqual(0, writes, "the profile already held it in memory; the next world save writes it (SaveSession.Save merges the ledger)");
        world.Validate();
    }

    [Test]
    public void Commit_InterruptedBeforeTheWorldSaveReplaysOnceAndReloadRestoresWithoutEvidence()
    {
        var profile = new LifetimeProfile();
        var saved = RoundTrip(new WorldRewards()); // the world file on disk, before the award

        // Earned in play, written to the profile, then the game dies before the world is saved.
        var live = RoundTrip(saved);
        Assert.IsTrue(AchievementAward.Commit(Madman, 10, Evidence(), Tracker(Madman), live, profile, () => { }));

        // Reload: the world never held it, so replaying the happening pays this world once; the profile stays single.
        var reloaded = RoundTrip(saved); reloaded.Validate();
        var tracker = Tracker(Madman);
        CollectionAssert.IsEmpty(tracker.Restore(reloaded.unlocked));
        Assert.IsFalse(tracker.IsUnlocked(Madman));
        Assert.IsTrue(AchievementAward.Commit(Madman, 10, Evidence("replay"), tracker, reloaded, profile, () => { }));
        Assert.AreEqual(10, reloaded.Balance);
        CollectionAssert.AreEqual(new[] { Madman }, profile.unlocked);

        // Saved and reloaded again: restored, never re-earned, evidence carried rather than re-created.
        var again = RoundTrip(reloaded); again.Validate();
        var restored = Tracker(Madman);
        restored.Restore(again.unlocked);
        Assert.IsTrue(restored.IsUnlocked(Madman));
        Assert.IsFalse(AchievementAward.Commit(Madman, 10, Evidence("third"), restored, again, profile, () => { }));
        Assert.AreEqual(10, again.Balance);
        Assert.AreEqual("replay", again.EvidenceFor(Madman).source);
    }

    [Test]
    public void Evidence_MustNameAnAwardOnceAndOldLedgersWithoutItStillLoad()
    {
        var world = new WorldRewards { evidence = null };
        world.Earn(Madman, 10);
        world.Validate();
        Assert.IsNotNull(world.evidence, "a ledger from before evidence loads empty");

        world.evidence.Add(new AwardEvidence { achievement = "never-earned" });
        Assert.Throws<InvalidDataException>(() => world.Validate());
        world.evidence.Clear();
        world.evidence.Add(new AwardEvidence { achievement = Madman });
        world.evidence.Add(new AwardEvidence { achievement = Madman });
        Assert.Throws<InvalidDataException>(() => world.Validate());
    }

    // ===== RENAMED ACHIEVEMENTS =====

    private static readonly Dictionary<string, string> Renames = new Dictionary<string, string> { ["old-title"] = "new-title", ["older-title"] = "old-title" };

    [Test]
    public void Aliases_FollowChainsAndReportMistakes()
    {
        Assert.AreEqual("new-title", AchievementAliases.Canonical("older-title", Renames));
        Assert.AreEqual("kept", AchievementAliases.Canonical("kept", Renames));
        CollectionAssert.IsEmpty(AchievementAliases.Problems(new[] { "new-title" }, Renames));
        Assert.IsTrue(AchievementAliases.Problems(new[] { "new-title", "old-title" }, Renames).Any(p => p.Contains("also a current")));
        Assert.IsTrue(AchievementAliases.Problems(new[] { "other" }, Renames).Any(p => p.Contains("not in the achievement note")));
        var cycle = new Dictionary<string, string> { ["a"] = "b", ["b"] = "a" };
        Assert.IsTrue(AchievementAliases.Problems(new[] { "c" }, cycle).Any(p => p.Contains("cycle")));
    }

    [Test]
    public void Aliases_MigrateLedgersWithoutMintingOrLosingAnchors()
    {
        var world = new WorldRewards();
        world.Earn("older-title", 10); world.Record(new AwardEvidence { achievement = "older-title", source = "first" });
        world.Earn("new-title", 7);     // earned again under its new name before the alias existed
        world.Earn(Madman, 5);
        world.Spend(12);
        int balance = world.Balance;

        Assert.IsTrue(AchievementAliases.Migrate(world, Renames));
        world.Validate();
        CollectionAssert.AreEqual(new[] { "new-title", Madman }, world.unlocked);
        Assert.AreEqual(balance, world.Balance, "migration keeps every Anchor");
        Assert.AreEqual("first", world.EvidenceFor("new-title").source);
        Assert.IsFalse(AchievementAliases.Migrate(world, Renames), "idempotent");

        var profile = new LifetimeProfile { unlocked = new List<string> { "old-title", Madman, "new-title" } };
        Assert.IsTrue(AchievementAliases.Migrate(profile, Renames));
        CollectionAssert.AreEqual(new[] { "new-title", Madman }, profile.unlocked);
    }

    [Test]
    public void TrackerRestore_ReportsIdsItCannotPlace()
    {
        var tracker = Tracker(Madman);
        CollectionAssert.AreEqual(new[] { "retired-title" }, tracker.Restore(new[] { Madman, "retired-title", "retired-title" }));
        CollectionAssert.AreEqual(new[] { Madman }, tracker.UnlockedIds);
    }

    // ===== COMPLETION =====

    [Test]
    public void Completion_TheFinalAchievementNeverCountsItself()
    {
        var all = new[] { "a", "b", AchievementCompletion.FinalId }.Select((id, i) => new AchievementDefinition { id = id, title = id, order = i }).ToList();
        CollectionAssert.AreEqual(new[] { "a", "b" }, AchievementCompletion.Set(all));
        Assert.AreEqual((1, 2), AchievementCompletion.Progress(all, new[] { "a", AchievementCompletion.FinalId }));
    }

    // ===== AGE GATES (X05, D13) =====

    [TearDown] public void ResetAge() => GameAge.Set(GameAge.FirstAge, 0);

    [Test]
    public void Capabilities_LegendOrnamentsInAgeZeroNeverOpenOrnamentalMagic()
    {
        Assert.IsTrue(AgeCapabilities.IsAvailable(AgeCapabilities.LegendOrnament, 0), "D13: legends can acquire Ornaments in Age 0");
        Assert.IsFalse(AgeCapabilities.IsAvailable(AgeCapabilities.OrnamentalMagic, 0), "Ornamental Magic stays locked in the Age of Desolation");
        Assert.IsFalse(AgeCapabilities.IsAvailable(AgeCapabilities.OrnamentalMagic, 2));
        Assert.IsTrue(AgeCapabilities.IsAvailable(AgeCapabilities.OrnamentalMagic, 3), "Ages.md: Age III starts proper Ornamental Magic");
        Assert.IsFalse(AgeCapabilities.IsAvailable("unknown-capability", 99));

        GameAge.Set(GameAge.FirstAge, 0);
        Assert.AreEqual(0f, GameValues.Get("capability", AgeCapabilities.OrnamentalMagic));
        Assert.AreEqual(1f, GameValues.Get("capability", AgeCapabilities.LegendOrnament));
        Assert.IsFalse(GameValues.TryGet("capability", "unknown-capability", out _), "unknown capabilities never resolve");
        // A Motif Awakening's achievement does not ask the Age.
        CollectionAssert.Contains(AchievementTriggers.Earned(AchievementEvent.Of(AchievementSignal.MotifAwakened, 1)).ToList(), "alchemical-pelican");
    }

    [Test]
    public void EraUnlock_ChecksTheAgeReachedAndUnknownRequirementsFailClosed()
    {
        var renewal = new CivicRequirement { requirementType = RequirementType.EraUnlock, requirementTarget = "Age-Of-Renewal" };
        Assert.AreEqual("age_reached", renewal.ToCondition().domain);
        Assert.AreEqual("age-of-renewal", renewal.ToCondition().targetName);

        GameAge.Set(GameAge.FirstAge, 0);
        Assert.IsFalse(renewal.IsMet(), "before the Age");
        Assert.IsTrue(new CivicRequirement { requirementType = RequirementType.EraUnlock, requirementTarget = GameAge.FirstAge }.IsMet(), "the Age now current");
        GameAge.Set("age-of-renewal", 1);
        Assert.IsTrue(renewal.IsMet(), "at the Age");

        var byNumber = new CivicRequirement { requirementType = RequirementType.EraUnlock, requiredValue = 1, comparison = ComparisonType.GreaterEqual };
        Assert.AreEqual("age", byNumber.ToCondition().domain);
        Assert.IsNull(byNumber.ToCondition().targetName);

        Assert.IsFalse(new CivicRequirement { requirementType = (RequirementType)999 }.IsMet(), "an unknown requirement no longer passes");
    }
}
