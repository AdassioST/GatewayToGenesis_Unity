using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Production Ink metadata, routes, fragment rewards and player-triggered issue scheduling.</summary>
public class SettlementStoryTests
{
    private static readonly string[] Sources = { "SettlementBallads", "SettlementMiscellany", "SettlementIssues" };
    private List<EventVolume> volumes;

    [SetUp]
    public void LoadProductionStories()
    {
        EventStoryIndex.Clear();
        volumes = Sources.Select(name =>
        {
            var asset = Resources.Load<TextAsset>("Events/" + name);
            Assert.IsNotNull(asset, name + " needs its compiled JSON");
            Assert.IsTrue(InkDrivenEventSetup.IsCompiledInk(asset.text), name + " loaded an Ink source instead of compiled JSON");
            return InkDrivenEventSetup.CreateVolume(asset);
        }).ToList();
    }

    [TearDown]
    public void ClearIndex() => EventStoryIndex.Clear();

    [Test]
    public void FourThreeVerseBalladsTwentyEventsAndFiveIssuesAreRegistered()
    {
        CollectionAssert.IsEmpty(EventStoryIndex.Problems);
        CollectionAssert.IsEmpty(InkDrivenEventSetup.Validate(volumes));
        Assert.AreEqual(12, volumes[0].storyNodes.Count);
        Assert.AreEqual(20, volumes[1].storyNodes.Count);
        Assert.AreEqual(5, volumes[2].storyNodes.Count);
        var ballads = volumes[0].storyNodes.GroupBy(n => n.ballad).ToList();
        Assert.AreEqual(4, ballads.Count);
        foreach (var ballad in ballads)
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ballad.Select(n => n.verse).OrderBy(n => n));
        Assert.IsTrue(volumes[2].storyNodes.All(n => n.isIssue));
        Assert.IsFalse(volumes.Take(2).SelectMany(v => v.storyNodes).Any(n => n.isIssue));
        foreach (var node in volumes.SelectMany(v => v.storyNodes))
        {
            Assert.IsTrue(node.storyConditions.Any(c => c.Domain == "event_completed" && c.targetName == node.nodeName &&
                c.comparison == ComparisonOperator.LessThan && c.requiredValue == 1), node.nodeName + " must be once per world");
            Assert.IsNotEmpty(node.theme);
            Assert.IsNotEmpty(node.cast);
        }
    }

    [Test]
    public void EveryDecisionHasTwoTerminatingRewardPathsAndNoUnsupportedTimedEffect()
    {
        foreach (var node in volumes.SelectMany(v => v.storyNodes))
        {
            var entry = EventStoryIndex.Get(node.nodeName);
            var chorus = EventStoryIndex.Get(entry.ContinueTarget);
            Assert.AreEqual(ScreenType.Chorus, chorus.screenType, node.nodeName);
            Assert.AreEqual(2, chorus.chorusChoices.Count, node.nodeName);
            foreach (var choice in chorus.chorusChoices)
            {
                var effects = EventStoryIndex.ConsequencesAlong(choice.destinationPath, out bool decision);
                Assert.IsFalse(decision, node.nodeName + " should finish this episode after its choice");
                Assert.AreEqual(1, effects.Count(e => e.type == EventConsequence.ConsequenceType.FragmentChange), node.nodeName);
                var reward = effects.Single(e => e.type == EventConsequence.ConsequenceType.FragmentChange);
                Assert.IsTrue(BalladActors.SplitTarget(reward.targetName, out string who, out _));
                Assert.AreEqual("cast", who);
                Assert.Greater(reward.value, 0);
                var opus = effects.Where(e => e.type == EventConsequence.ConsequenceType.LesserOpus).ToList();
                Assert.AreEqual(node.verse == 3 ? 1 : 0, opus.Count, node.nodeName);
                if (opus.Count == 1)
                {
                    Assert.IsTrue(LesserOpusCatalog.SplitTarget(opus[0].targetName, out string recipient, out var definition));
                    Assert.AreEqual("cast", recipient);
                    Assert.AreEqual(node.ballad, definition.ballad);
                    Assert.AreEqual(1, opus[0].value);
                }
                foreach (var effect in effects.Where(e => e.durationSevenths > 0))
                    Assert.IsTrue(EventSystemLogic.TryGetEffect(effect, out _), node.nodeName + " has a duration that the engine cannot undo");
                var outcome = EventStoryIndex.Get(choice.destinationPath);
                Assert.AreEqual(ScreenType.Outro, EventStoryIndex.Get(outcome.ContinueTarget).screenType);
            }
            if (node.verse != 3)
                Assert.IsTrue(chorus.chorusChoices.Any(c => c.requirements.Count == 0 && c.requirementsCost.Count == 0),
                    node.nodeName + " must have an unaffordable-cost fallback");
        }
    }

    [Test]
    public void EachBalladChainsOnlyForwardAndEitherOpeningLeavesExactlyOneFinaleChoice()
    {
        foreach (var group in volumes[0].storyNodes.GroupBy(n => n.ballad))
        {
            var verses = group.OrderBy(n => n.verse).ToList();
            Assert.IsTrue(verses[0].isUnlocked);
            for (int i = 0; i < verses.Count; i++)
            {
                var node = verses[i];
                var chorus = EventStoryIndex.Get(node.nodeName + "_chorus");
                if (i > 0)
                {
                    Assert.IsFalse(node.isUnlocked);
                    Assert.IsTrue(node.storyConditions.Any(c => c.Domain == "event_completed" && c.targetName == verses[i - 1].nodeName && c.requiredValue == 1));
                }
                foreach (var choice in chorus.chorusChoices)
                {
                    var unlocks = EventStoryIndex.ConsequencesAlong(choice.destinationPath, out _)
                        .Where(c => c.type == EventConsequence.ConsequenceType.UnlockEvent).ToList();
                    Assert.AreEqual(i < 2 ? 1 : 0, unlocks.Count);
                    if (i < 2) Assert.AreEqual(verses[i + 1].nodeName, unlocks[0].targetName);
                }
            }
            var first = EventStoryIndex.Get(verses[0].nodeName + "_chorus");
            var final = EventStoryIndex.Get(verses[2].nodeName + "_chorus");
            foreach (var route in first.chorusChoices)
            {
                var scores = EventStoryIndex.ConsequencesAlong(route.destinationPath, out _)
                    .Where(c => c.type == EventConsequence.ConsequenceType.ScoreChange).ToDictionary(c => c.targetName, c => c.value);
                int available = final.chorusChoices.Count(c => c.requirements.All(r => r.Domain == "score" &&
                    GameValues.Compare(scores.TryGetValue(r.targetName, out int value) ? value : 0, r.comparison, r.requiredValue)));
                Assert.AreEqual(1, available, group.Key + " loses its opening branch or softlocks the finale");
            }
        }
    }

    [Test]
    public void IssuesAreNeverRandomOffersAndRecheckTheirLiveGates()
    {
        bool ready = true;
        GameValues.Register("settlement_story_test", (string target, out float value) => { value = ready ? 1 : 0; return true; });
        var go = new GameObject("Settlement issue scheduler test");
        try
        {
            var manager = go.AddComponent<EventVolumeManager>();
            foreach (var volume in volumes)
            {
                foreach (var node in volume.storyNodes)
                {
                    node.isUnlocked = true;
                    node.storyConditions = new List<EventCondition> {
                        new EventCondition { type = EventCondition.ConditionType.ValueCheck, domain = "settlement_story_test",
                            requiredValue = 1, comparison = ComparisonOperator.Equals }
                    };
                }
                manager.AddVolume(volume);
            }
            Assert.AreEqual(5, manager.AvailableIssues().Count());
            for (int i = 0; i < 20; i++) Assert.IsFalse(manager.FindBestAvailableStory().isIssue);
            var issue = manager.AvailableIssues().First();
            Assert.IsTrue(manager.IsIssueAvailable(issue));
            ready = false;
            Assert.IsFalse(manager.IsIssueAvailable(issue), "a stale notice must not start a resolved issue");
            Assert.IsEmpty(manager.AvailableIssues());
            Assert.IsNull(manager.FindBestAvailableStory());
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
