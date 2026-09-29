using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Bestiary's hypotheses (SpeciesHypotheses): the questions asked, the evidence that tests each (a watch in the
/// guessed Echo, every watch, a hunt), ruled-out answers, insight, and understanding by deduction.
/// </summary>
public class SpeciesHypothesesTests
{
    private static SpeciesSpec Wolf() => new SpeciesSpec
    {
        id = "wolf", name = "Grey Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social,
        breedingEcho = 2, niche = HarmonicNiche.Leyline, prey = { "elk" },
    };

    private static SpeciesSpec Elk() => new SpeciesSpec { id = "elk", name = "Taiga Elk", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Docile };

    private static List<HypothesisOption> Options(SpeciesSpec s, HypothesisQuestion q) =>
        SpeciesHypotheses.Options(q, s, new[] { Wolf(), Elk() }, e => $"Echo {e}");

    [Test]
    public void Questions_PreyOnlyForMeatEaters()
    {
        CollectionAssert.AreEqual(new[] { HypothesisQuestion.Breeding, HypothesisQuestion.Niche, HypothesisQuestion.Prey }, SpeciesHypotheses.Questions(Wolf()));
        CollectionAssert.AreEqual(new[] { HypothesisQuestion.Breeding, HypothesisQuestion.Niche }, SpeciesHypotheses.Questions(Elk()));
    }

    [Test]
    public void PreyOptions_AreTheIdentifiedCreatures_NotItself_AndNone()
    {
        var keys = Options(Wolf(), HypothesisQuestion.Prey).Select(o => o.key).ToList();
        CollectionAssert.AreEqual(new[] { "elk", SpeciesHypotheses.None }, keys);
    }

    [Test]
    public void Breeding_IsTestedOnlyByAWatchInTheGuessedEcho()
    {
        var wolf = Wolf();
        var record = new SpeciesRecord { species = "wolf", identified = true };
        Assert.IsTrue(SpeciesHypotheses.Guess(record, HypothesisQuestion.Breeding, "3", Options(wolf, HypothesisQuestion.Breeding)));
        var h = SpeciesHypotheses.Of(record, HypothesisQuestion.Breeding);
        Assert.AreEqual(Verdict.Untested, SpeciesHypotheses.Test(h, wolf, Evidence.Watch, 1, 1), "another Echo says nothing of Echo 3");
        Assert.AreEqual(Verdict.Untested, SpeciesHypotheses.Test(h, wolf, Evidence.Hunt, 3, 0), "a hunt says nothing of breeding");
        var wrong = SpeciesHypotheses.Test(h, wolf, Evidence.Watch, 3, 0);
        Assert.AreEqual(Verdict.Refuted, wrong);
        SpeciesHypotheses.Apply(h, wrong);
        CollectionAssert.Contains(h.ruledOut, "3");
        Assert.IsNull(h.guess);
        Assert.IsFalse(SpeciesHypotheses.Guess(record, HypothesisQuestion.Breeding, "3", Options(wolf, HypothesisQuestion.Breeding)), "ruled out");
        Assert.IsTrue(SpeciesHypotheses.Guess(record, HypothesisQuestion.Breeding, "2", Options(wolf, HypothesisQuestion.Breeding)));
        var right = SpeciesHypotheses.Test(h, wolf, Evidence.Watch, 2, 0);
        Assert.AreEqual(Verdict.Confirmed, right);
        SpeciesHypotheses.Apply(h, right);
        Assert.IsTrue(h.confirmed);
        Assert.IsFalse(h.insight, "one guess fell first: no insight");
        Assert.IsFalse(SpeciesHypotheses.Guess(record, HypothesisQuestion.Breeding, "1", Options(wolf, HypothesisQuestion.Breeding)), "answered");
    }

    [Test]
    public void NoSeasonOfItsOwn_NeedsAllFourEchoes_AndFallsInTheBreedingEcho()
    {
        var elk = Elk();
        var h = new Hypothesis { question = HypothesisQuestion.Breeding, guess = SpeciesHypotheses.None };
        Assert.AreEqual(Verdict.Untested, SpeciesHypotheses.Test(h, elk, Evidence.Watch, 4, 0b0111));
        Assert.AreEqual(Verdict.Confirmed, SpeciesHypotheses.Test(h, elk, Evidence.Watch, 4, 0b1111));
        var wolf = Wolf();
        Assert.AreEqual(Verdict.Refuted, SpeciesHypotheses.Test(h, wolf, Evidence.Watch, 2, 0b0010), "its young crowd into Echo 2");
        Assert.AreEqual(Verdict.Untested, SpeciesHypotheses.Test(h, wolf, Evidence.Watch, 1, 0b1101));
    }

    [Test]
    public void Niche_IsTestedByAnyWatch_Prey_ByAHunt()
    {
        var wolf = Wolf();
        var niche = new Hypothesis { question = HypothesisQuestion.Niche, guess = "Coherent" };
        Assert.AreEqual(Verdict.Refuted, SpeciesHypotheses.Test(niche, wolf, Evidence.Watch, 1, 0));
        niche.guess = "Leyline";
        Assert.AreEqual(Verdict.Confirmed, SpeciesHypotheses.Test(niche, wolf, Evidence.Watch, 3, 0));
        Assert.AreEqual(Verdict.Untested, SpeciesHypotheses.Test(niche, wolf, Evidence.Hunt, 0, 0));
        var prey = new Hypothesis { question = HypothesisQuestion.Prey, guess = "elk" };
        Assert.AreEqual(Verdict.Untested, SpeciesHypotheses.Test(prey, wolf, Evidence.Watch, 1, 0));
        Assert.AreEqual(Verdict.Confirmed, SpeciesHypotheses.Test(prey, wolf, Evidence.Hunt, 0, 0));
        prey.guess = SpeciesHypotheses.None;
        Assert.AreEqual(Verdict.Refuted, SpeciesHypotheses.Test(prey, wolf, Evidence.Hunt, 0, 0));
    }

    [Test]
    public void EveryQuestionAnswered_IsUnderstanding_AndFirstGuessesAreInsight()
    {
        var wolf = Wolf();
        var record = new SpeciesRecord { species = "wolf", identified = true, observed = false };
        foreach (var (q, key, evidence, echo) in new[] { (HypothesisQuestion.Breeding, "2", Evidence.Watch, 2), (HypothesisQuestion.Niche, "Leyline", Evidence.Watch, 1), (HypothesisQuestion.Prey, "elk", Evidence.Hunt, 0) })
        {
            Assert.IsFalse(SpeciesHypotheses.Deduced(record, wolf));
            SpeciesHypotheses.Guess(record, q, key, Options(wolf, q));
            var h = SpeciesHypotheses.Of(record, q);
            SpeciesHypotheses.Apply(h, SpeciesHypotheses.Test(h, wolf, evidence, echo, 0));
            Assert.IsTrue(h.insight, q.ToString());
        }
        Assert.IsTrue(SpeciesHypotheses.Deduced(record, wolf));
        record.deduced = true;
        Assert.AreEqual(SpeciesLevel.Understood, SpeciesLore.Level(record, understands: false), "worked out without the research");
        Assert.IsTrue(SpeciesHypotheses.Knows(record, HypothesisQuestion.Niche, SpeciesLevel.Identified));
        Assert.IsFalse(SpeciesHypotheses.Knows(new SpeciesRecord(), HypothesisQuestion.Niche, SpeciesLevel.Observed), "Observed alone no longer tells the niche");
        Assert.IsTrue(SpeciesHypotheses.Knows(new SpeciesRecord(), HypothesisQuestion.Niche, SpeciesLevel.Understood));
    }

    [Test]
    public void Guess_RefusesWhatIsNoOption()
    {
        var record = new SpeciesRecord { species = "wolf", identified = true };
        Assert.IsFalse(SpeciesHypotheses.Guess(record, HypothesisQuestion.Prey, "dragon", Options(Wolf(), HypothesisQuestion.Prey)));
        Assert.IsFalse(SpeciesHypotheses.Guess(record, HypothesisQuestion.Niche, "Sky", Options(Wolf(), HypothesisQuestion.Niche)));
    }

    [Test]
    public void Report_SaysWhatWasLearned()
    {
        StringAssert.Contains("does not breed in the Echo 3", SpeciesHypotheses.Report(HypothesisQuestion.Breeding, "3", "Echo 3", Verdict.Refuted, "Grey Wolf"));
        StringAssert.Contains("hunts the Taiga Elk", SpeciesHypotheses.Report(HypothesisQuestion.Prey, "elk", "Taiga Elk", Verdict.Confirmed, "Grey Wolf"));
    }
}
