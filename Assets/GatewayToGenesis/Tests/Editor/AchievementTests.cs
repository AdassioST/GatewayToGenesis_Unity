using System.Linq;
using NUnit.Framework;

/// <summary>
/// The achievement note's reader (<see cref="AchievementNote"/>), the unlock rules (<see cref="AchievementTriggers"/>)
/// and the unlocked state (<see cref="AchievementTracker"/>). Pure, so they also run outside Unity. The real note is
/// checked by ContentTests.
/// </summary>
public class AchievementTests
{
    private const string Sample =
        "#mechanic #event \n" +
        "\n" +
        "### Progressing through [[Ages]]\n" +
        "\n" +
        "Survive through [[Ages]] 0\n" +
        "_\"The Brown [[Auric Peach]]: Escape [[The Inescapable Hunger]].\"_\n" +
        "\n" +
        "### [[Stellar Legacy Score]]\n" +
        "\n" +
        "Fill all the slots of a [[Civilization]]'s Government Council by appointing 6 [[Legend]]s and a [[Head of State]]\n" +
        "_\"A League of [[Legend]]s: And together, we are the League of [[Legend]]s!\"_\n" +
        "\n" +
        "Witness any [[Legend]] share a [[Scorching Truth]] and be rejected\n" +
        "_\"The Cave's Exit: The blinding [[Luminance]] was so perfect that no one believed they had seen it.\"\n" +
        "\n" +
        "_\"Critically Thinking Hater: \"_\n" +
        "Wish for the End of the Third Actor\n" +
        "_The Purest of All Love: Learn to love without possession._  \n" +
        "Win the Crusades for the [[Auric Aria]] in [[Ages]] V\n" +
        "*\"For The Sovereign!: Win the Holy War for the [[Aureus Pillar]].\"*\n" +
        "Survive through the [[Holy War]]\n" +
        "_\"Genesis 1:5: The Aria called [[Luminance]] Day.\"_\n";

    private static AchievementNote.Result Parse() => AchievementNote.Parse(Sample);

    // ===== THE NOTE =====

    [Test]
    public void Note_ReadsRequirementTitleAndFlavorVerbatim()
    {
        var first = Parse().achievements[0];
        Assert.AreEqual("the-brown-auric-peach", first.id);
        Assert.AreEqual("Progressing through [[Ages]]", first.category);
        Assert.AreEqual("Survive through [[Ages]] 0", first.requirement);
        Assert.AreEqual("The Brown [[Auric Peach]]", first.title);
        Assert.AreEqual("Escape [[The Inescapable Hunger]].", first.flavor);
    }

    [Test]
    public void Note_TitlesSplitAtTheFirstColonAndSpaceOnly()
    {
        var genesis = Parse().achievements.Single(a => a.id == "genesis-1-5");
        Assert.AreEqual("Genesis 1:5", genesis.title);
        Assert.AreEqual("The Aria called [[Luminance]] Day.", genesis.flavor);
    }

    [Test]
    public void Note_AcceptsEveryQuoteShapeTheVaultUses()
    {
        var ids = Parse().achievements.Select(a => a.id).ToList();
        CollectionAssert.Contains(ids, "the-purest-of-all-love", "_Title: flavor_ without inner quotes");
        CollectionAssert.Contains(ids, "for-the-sovereign", "*\"...\"* with asterisks");
        CollectionAssert.Contains(ids, "the-cave-s-exit", "an unclosed quote is still read");
        Assert.AreEqual("Win the Holy War for the [[Aureus Pillar]].", Parse().achievements.Single(a => a.id == "for-the-sovereign").flavor);
    }

    [Test]
    public void Note_ReportsShapeProblemsInsteadOfRepairingThem()
    {
        var result = Parse();
        Assert.IsTrue(result.problems.Any(p => p.Contains("not closed")), "the unclosed quote is reported");
        Assert.IsTrue(result.problems.Any(p => p.Contains("Critically Thinking Hater") && p.Contains("no requirement")), "a quote without a requirement is reported");
        Assert.IsFalse(result.achievements.Any(a => a.title.StartsWith("Critically")), "and left out");
    }

    [Test]
    public void Note_PlainTextDropsLinkBrackets()
    {
        Assert.AreEqual("A League of Legends", AchievementNote.Plain("A League of [[Legend]]s"));
        Assert.AreEqual("the Moon", AchievementNote.Plain("the [[Moon|Moon]]"));
        Assert.AreEqual("Silver", AchievementNote.Plain("[[Silver Blood|Silver]]"));
    }

    // ===== RULES =====

    private static ChorusResolution Roll(ChorusOutcome outcome, int chance, bool saved = false) =>
        new ChorusResolution { outcome = outcome, successPercent = chance, savedByRoll = saved };

    private static string[] Earned(AchievementEvent e) => AchievementTriggers.Earned(e).ToArray();

    [Test]
    public void Rules_ChorusCriticalsAndPiety()
    {
        CollectionAssert.AreEqual(new[] { "heads-you-vanish" }, Earned(AchievementEvent.Chorus(Roll(ChorusOutcome.CriticalFailure, 50))));
        CollectionAssert.AreEqual(new[] { "butterfly-survives-the-storm" }, Earned(AchievementEvent.Chorus(Roll(ChorusOutcome.CriticalSuccess, 10))));
        CollectionAssert.IsEmpty(Earned(AchievementEvent.Chorus(Roll(ChorusOutcome.CriticalSuccess, 11))), "only at Forsaken odds (10% or less)");
        CollectionAssert.AreEqual(new[] { "the-aria-was-listening-that-day" }, Earned(AchievementEvent.Chorus(Roll(ChorusOutcome.Success, 40, saved: true))));
        CollectionAssert.IsEmpty(Earned(AchievementEvent.Chorus(Roll(ChorusOutcome.Success, 40))));
    }

    [Test]
    public void Rules_FullCouncilNeedsSixLegendsAndAHeadOfState()
    {
        CollectionAssert.AreEqual(new[] { "a-league-of-legends" }, Earned(AchievementEvent.Of(AchievementSignal.CouncilChanged, 6, flag: true)));
        CollectionAssert.IsEmpty(Earned(AchievementEvent.Of(AchievementSignal.CouncilChanged, 6, flag: false)));
        CollectionAssert.IsEmpty(Earned(AchievementEvent.Of(AchievementSignal.CouncilChanged, 5, flag: true)));
    }

    [Test]
    public void Rules_LibraryAndDeathLedger()
    {
        CollectionAssert.AreEqual(new[] { "an-eccentric-madman" }, Earned(AchievementEvent.Of(AchievementSignal.LibraryOpened)));
        CollectionAssert.AreEqual(new[] { "the-trolley-that-kept-moving" }, Earned(AchievementEvent.Of(AchievementSignal.DeathLedgerRevised, -3)));
        CollectionAssert.IsEmpty(Earned(AchievementEvent.Of(AchievementSignal.DeathLedgerRevised, 0)));
    }

    [Test]
    public void Rules_FirstBirth()
    {
        CollectionAssert.AreEqual(new[] { "there-is-beauty-in-that" }, Earned(AchievementEvent.Of(AchievementSignal.PeopleBorn, 1)), "the first child born");
        CollectionAssert.AreEqual(new[] { "there-is-beauty-in-that" }, Earned(AchievementEvent.Of(AchievementSignal.PeopleBorn, 40)), "a later report still carries it (the award is idempotent)");
        CollectionAssert.IsEmpty(Earned(AchievementEvent.Of(AchievementSignal.PeopleBorn, 0)), "no one born yet");
    }

    [Test]
    public void Rules_LossAndRuins()
    {
        CollectionAssert.AreEqual(new[] { "digestive-rebirth" }, Earned(AchievementEvent.Of(AchievementSignal.RuinCivicAdopted)), "a civic adopted from the ruins of a fallen settlement");
        CollectionAssert.AreEqual(new[] { "404" }, Earned(AchievementEvent.Of(AchievementSignal.SettlementLost, flag: true)), "a settlement lost beyond your Administrative Authority");
        CollectionAssert.IsEmpty(Earned(AchievementEvent.Of(AchievementSignal.SettlementLost, flag: false)), "not one lost inside it");
        CollectionAssert.AreEqual(new[] { "welcome-back-traitors" }, Earned(AchievementEvent.Of(AchievementSignal.SettlementReclaimed)), "a settlement founded on the ruins of one of yours");
    }

    // ===== TRACKER =====

    private static AchievementTracker Tracker(params string[] ids) =>
        new AchievementTracker(ids.Select((id, i) => new AchievementDefinition { id = id, title = id, order = i }));

    [Test]
    public void Tracker_UnlocksEachAchievementOnce()
    {
        var tracker = Tracker("heads-you-vanish");
        var failure = AchievementEvent.Chorus(Roll(ChorusOutcome.CriticalFailure, 50));
        Assert.AreEqual(1, tracker.Report(failure).Count);
        Assert.AreEqual(0, tracker.Report(failure).Count, "already unlocked");
        Assert.IsTrue(tracker.IsUnlocked("heads-you-vanish"));
    }

    [Test]
    public void Tracker_IgnoresRulesForAchievementsNotInTheNote()
    {
        var tracker = Tracker("something-else");
        CollectionAssert.IsEmpty(tracker.Report(AchievementEvent.Of(AchievementSignal.LibraryOpened)));
        Assert.IsFalse(tracker.Unlock("an-eccentric-madman"));
    }

    [Test]
    public void Tracker_RestoreKeepsOnlyKnownIdsInOrder()
    {
        var tracker = Tracker("a", "b", "c");
        tracker.Restore(new[] { "c", "gone", "a", "c" });
        CollectionAssert.AreEqual(new[] { "c", "a" }, tracker.UnlockedIds);
    }
}
