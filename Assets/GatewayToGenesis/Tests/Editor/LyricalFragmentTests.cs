using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Lyrical Fragments (<see cref="LyricalFragments"/>): seven kinds, each tagged with its binding; a legend's purse of
/// them sets its rank and grows its bindings; themes and the Underdog; and how stories author them (the
/// <c>fragment:</c> consequence and the ballad tags, <see cref="EventScript"/>). Pure: runs outside Unity.
/// </summary>
public class LyricalFragmentTests
{
    [TestCase("Meaning", FragmentKind.Meaning)]
    [TestCase("Fragment of Vision", FragmentKind.Vision)]
    [TestCase("fragment of catharsis", FragmentKind.Catharsis)]
    [TestCase("Void", FragmentKind.Acceptance)]
    [TestCase("Strand", FragmentKind.Rebirth)]
    [TestCase("Luminance", FragmentKind.Lucidity)]
    public void TryParse_ReadsKindsAndBindings(string text, FragmentKind kind)
    {
        Assert.IsTrue(LyricalFragments.TryParse(text, out var parsed));
        Assert.AreEqual(kind, parsed);
        Assert.AreEqual(kind, LyricalFragments.OfBinding(LyricalFragments.Binding(kind)), "each kind is its binding's");
    }

    [Test]
    public void TryParse_RejectsAnythingElse()
    {
        Assert.IsFalse(LyricalFragments.TryParse("Renown", out _));
        Assert.IsFalse(LyricalFragments.TryParse("", out _));
    }

    [Test]
    public void ThemesAreArchetypesOrKinds()
    {
        Assert.IsTrue(LyricalFragments.TryParseTheme("Emotional Core", out var p, out var s));
        Assert.AreEqual((FragmentKind.Catharsis, FragmentKind.Meaning), (p, s));
        Assert.IsTrue(LyricalFragments.TryParseTheme("sacrificial_lamb", out p, out s));
        Assert.AreEqual((FragmentKind.Acceptance, FragmentKind.Rebirth), (p, s));
        Assert.IsTrue(LyricalFragments.TryParseTheme("Defiance", out p, out s));
        Assert.AreEqual((FragmentKind.Defiance, FragmentKind.Defiance), (p, s), "a single kind pays both halves in it");
        Assert.IsFalse(LyricalFragments.TryParseTheme("Heroism", out _, out _));
        var reward = LyricalFragments.ThemeReward("Leader", 2f);
        Assert.AreEqual(10, reward.Single(a => a.kind == FragmentKind.Meaning).amount, "Leader: Meaning x5, scaled");
        Assert.AreEqual(6, reward.Single(a => a.kind == FragmentKind.Vision).amount, "and Vision x3");
    }

    [Test]
    public void APurseGrowsTheBindingsAndNeverGoesBelowZero()
    {
        var purse = new Dictionary<string, int>();
        Assert.AreEqual(25, LyricalFragments.Add(purse, FragmentKind.Vision, 25));
        Assert.AreEqual(-25, LyricalFragments.Add(purse, FragmentKind.Vision, -40), "only what was there is taken");
        LyricalFragments.Add(purse, FragmentKind.Vision, 23);
        LyricalFragments.Add(purse, FragmentKind.Defiance, 4);
        Assert.AreEqual(27, LyricalFragments.Total(purse));
        Assert.AreEqual(FragmentKind.Vision, LyricalFragments.Dominant(purse));
        var bonuses = LyricalFragments.BindingBonuses(purse, 10);
        Assert.AreEqual(2, bonuses["Crystal"], "23 Vision: two points of Crystal");
        Assert.IsFalse(bonuses.ContainsKey("Cindergale"), "4 Defiance is not a point yet");
        Assert.AreEqual("23 Vision, 4 Defiance", LyricalFragments.Describe(purse));
    }

    [Test]
    public void TheUnderdogEarnsDoubleOnlyAgainstTheOdds()
    {
        var tuning = new FragmentTuning();
        Assert.IsTrue(LyricalFragments.IsUnderdog(tuning.underdogThreshold, tuning));
        Assert.IsFalse(LyricalFragments.IsUnderdog(tuning.underdogThreshold - 1, tuning));
        Assert.AreEqual(2f, LyricalFragments.Multiplier(true, true, tuning));
        Assert.AreEqual(1f, LyricalFragments.Multiplier(true, false, tuning));
        Assert.AreEqual(1f, LyricalFragments.Multiplier(false, true, tuning));
        var shared = LyricalFragments.Share(new[] { new FragmentAward(FragmentKind.Meaning, 1) }, 0.1f);
        Assert.AreEqual(1, shared.Single().amount, "a share is at least one of each kind");
    }

    [Test]
    public void NamesArePluralOnlyForMany()
    {
        Assert.AreEqual("Fragment of Meaning", LyricalFragments.Name(FragmentKind.Meaning, 1));
        Assert.AreEqual("Fragments of Meaning", LyricalFragments.Name(FragmentKind.Meaning, 3));
        Assert.AreEqual("3 Fragments of Vision", new FragmentAward(FragmentKind.Vision, 3).ToString());
    }

    // ===== AUTHORING =====

    [Test]
    public void TheFragmentConsequenceParses()
    {
        var problems = new List<string>();
        var c = EventScript.ParseConsequences("fragment:protagonist Lucidity +3; fragments:council Meaning +2", problems);
        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual(2, c.Count);
        Assert.AreEqual(EventConsequence.ConsequenceType.FragmentChange, c[0].type);
        Assert.AreEqual("protagonist Lucidity", c[0].targetName);
        Assert.AreEqual(3, c[0].value);
        EventScript.ParseConsequences("fragment:protagonist +3", problems);
        Assert.AreEqual(1, problems.Count, "a fragment needs its kind");
    }

    [Test]
    public void TheBalladTagsParse()
    {
        var problems = new List<string>();
        var tags = new List<string>
        {
            "title: The Ruin-Song, Verse I", "conditions: seventh:1", "event_type: Mystical",
            "ballad: ruin_song", "verse: 1", "ballad_title: The Ballad of the Ruin-Song", "theme: Scholar", "cast: expedition, co:2",
        };
        Assert.IsTrue(EventScript.TryParseStoryNode("ballad_ruin_song_1", tags, out var node, problems));
        CollectionAssert.IsEmpty(problems);
        Assert.AreEqual("ruin_song", node.ballad);
        Assert.AreEqual(1, node.verse);
        Assert.AreEqual("The Ballad of the Ruin-Song", node.balladTitle);
        Assert.AreEqual("Scholar", node.theme);
        Assert.AreEqual("expedition, co:2", node.cast);

        tags[3] = "ballad: ruin_song";
        tags[4] = "verse: 0";
        tags[6] = "theme: Heroism";
        tags[7] = "cast: heroes";
        EventScript.TryParseStoryNode("bad", tags, out _, problems);
        Assert.AreEqual(3, problems.Count, string.Join("\n", problems));
    }
}
