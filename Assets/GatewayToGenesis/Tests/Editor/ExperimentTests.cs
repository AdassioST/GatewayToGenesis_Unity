using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The discovery loop's shared engine (Experiments) and the kitchen on top of it (KitchenTrials): mixes recognised
/// whatever their order and size, warmth that grows as a trial comes nearer, hints that come to light with it, a formula
/// found only by making it, and the whispered recipes shipped with the culture all findable with a real batch.
/// </summary>
public class ExperimentTests
{
    private static SecretFormula Cake() => new SecretFormula
    {
        id = "cake", name = "Cake", method = "Cook",
        parts =
        {
            new FormulaPart { component = "Honey", minShare = 0.35f, maxShare = 0.6f },
            new FormulaPart { component = "Grain", minShare = 0.25f, maxShare = 0.5f },
            new FormulaPart { component = "Pits", minShare = 0.05f, maxShare = 0.2f },
        },
        hints = { "first", "second", "third" },
    };

    private static Dictionary<string, float> Mix(params (string c, float a)[] parts) => Experiments.Shares(parts);

    private static List<ResourceAmount> In(params (string r, float a)[] parts) => parts.Select(p => new ResourceAmount { resource = p.r, amount = p.a }).ToList();

    [Test]
    public void Signature_IsTheSameForTheSameMix_WhateverTheOrderOrBatchSize()
    {
        Assert.AreEqual(Experiments.Signature("Cook", Mix(("Honey", 2), ("Grain", 1))), Experiments.Signature("Cook", Mix(("grain", 2), ("Honey", 4))));
        Assert.AreNotEqual(Experiments.Signature("Cook", Mix(("Honey", 2), ("Grain", 1))), Experiments.Signature("Ferment", Mix(("Honey", 2), ("Grain", 1))));
        Assert.AreNotEqual(Experiments.Signature("Cook", Mix(("Honey", 2), ("Grain", 1))), Experiments.Signature("Cook", Mix(("Honey", 1), ("Grain", 1))));
    }

    [Test]
    public void Warmth_GrowsAsTheMixComesNearer_AndOnlyTheRightMixFindsIt()
    {
        var cake = Cake();
        var none = new List<string>();
        Assert.AreEqual(Warmth.Nothing, Experiments.WarmthOf(new[] { cake }, none, "Cook", Mix(("Fish", 1))).warmth);
        Assert.AreEqual(Warmth.Faint, Experiments.WarmthOf(new[] { cake }, none, "Cook", Mix(("Honey", 1), ("Fish", 1))).warmth, "one part of three, in share");
        Assert.AreEqual(Warmth.Close, Experiments.WarmthOf(new[] { cake }, none, "Cook", Mix(("Honey", 2), ("Grain", 2), ("Fish", 1))).warmth, "two parts of three");
        Assert.AreEqual(Warmth.VeryClose, Experiments.WarmthOf(new[] { cake }, none, "Cook", Mix(("Honey", 2), ("Grain", 1), ("Pits", 1))).warmth, "every part, the pits too many");
        Assert.AreEqual(Warmth.Found, Experiments.WarmthOf(new[] { cake }, none, "Cook", Mix(("Honey", 3), ("Grain", 2), ("Pits", 1))).warmth);
        Assert.AreEqual(Warmth.Nothing, Experiments.WarmthOf(new[] { cake }, none, "Ferment", Mix(("Honey", 3), ("Grain", 2), ("Pits", 1))).warmth, "made the wrong way");
        Assert.AreEqual(Warmth.Nothing, Experiments.WarmthOf(new[] { cake }, new List<string> { "cake" }, "Cook", Mix(("Honey", 3), ("Grain", 2), ("Pits", 1))).warmth, "found ones warm nothing");
    }

    [Test]
    public void Exclusive_AnythingElseSpoilsIt()
    {
        var mead = Cake();
        mead.exclusive = true;
        Assert.IsTrue(Experiments.Matches(mead, "Cook", Mix(("Honey", 3), ("Grain", 2), ("Pits", 1))));
        Assert.IsFalse(Experiments.Matches(mead, "Cook", Mix(("Honey", 6), ("Grain", 4), ("Pits", 2), ("Salt", 1))));
    }

    [Test]
    public void Hints_ComeToLightAsTrialsComeNearer_AllOnceFound()
    {
        var cake = Cake();
        var progress = new List<FormulaProgress>();
        Assert.AreEqual(1, Experiments.HintsShown(cake, null), "the first is whispered from the start");
        Assert.IsFalse(Experiments.Record(progress, cake, Warmth.Faint), "faint brings nothing new");
        Assert.AreEqual(1, Experiments.HintsShown(cake, Experiments.Of(progress, "cake")));
        Assert.IsTrue(Experiments.Record(progress, cake, Warmth.Close));
        Assert.AreEqual(2, Experiments.HintsShown(cake, Experiments.Of(progress, "cake")));
        Assert.IsFalse(Experiments.Record(progress, cake, Warmth.Faint), "a colder trial never takes a hint back");
        Assert.IsTrue(Experiments.Record(progress, cake, Warmth.Found));
        Assert.IsTrue(Experiments.Of(progress, "cake").found);
        Assert.AreEqual(3, Experiments.HintsShown(cake, Experiments.Of(progress, "cake")));
        Assert.AreEqual(4, Experiments.Of(progress, "cake").near);
    }

    [Test]
    public void Problem_CatchesFormulasNoOneCouldFind()
    {
        Assert.IsNull(Experiments.Problem(Cake()));
        var greedy = Cake();
        greedy.parts[0].minShare = 0.9f;
        greedy.parts[0].maxShare = 0.95f;
        StringAssert.Contains("more than the whole", Experiments.Problem(greedy));
        var silent = Cake();
        silent.hints.Clear();
        StringAssert.Contains("no hint", Experiments.Problem(silent));
        var twice = Cake();
        twice.parts[1].component = "honey";
        StringAssert.Contains("twice", Experiments.Problem(twice));
    }

    // ===== THE KITCHEN =====

    [Test]
    public void EveryWhisperedRecipe_IsValid_AndFoundByABatchOfWholeUnits()
    {
        foreach (var spec in KitchenTrials.Defaults())
        {
            Assert.IsNull(KitchenTrials.Problem(spec, _ => true, r => r == "Glimmerfern"), spec.formula.id);
            // Some mix of 1-6 of each part (the dialog's steps) must be it.
            var parts = spec.formula.parts;
            bool found = false;
            foreach (var amounts in Amounts(parts.Count))
            {
                var mix = parts.Select((p, i) => new ResourceAmount { resource = p.component, amount = amounts[i] }).ToList();
                if (KitchenTrials.Matching(new[] { spec }, spec.Method, mix) != null) { found = true; break; }
            }
            Assert.IsTrue(found, $"{spec.formula.id} cannot be made from 1-6 units of each part");
        }
    }

    private static IEnumerable<int[]> Amounts(int n)
    {
        if (n == 0) { yield return new int[0]; yield break; }
        foreach (var rest in Amounts(n - 1))
            for (int a = 1; a <= 6; a++) yield return rest.Concat(new[] { a }).ToArray();
    }

    [Test]
    public void Record_RemembersEachMixOnce_KeepsWhatItFound_AndCountsTheSeventh()
    {
        var log = new KitchenLog();
        var specs = new List<HiddenRecipeSpec> { new HiddenRecipeSpec { formula = Cake() } };
        var (first, near, hint) = KitchenTrials.Record(log, specs, KitchenMethod.Cook, In(("Honey", 2), ("Grain", 2), ("Fish", 1)), new InventedRecipe(), 4);
        Assert.AreEqual((int)Warmth.Close, first.warmth);
        Assert.AreEqual("cake", near.formula.id);
        Assert.IsTrue(hint, "coming close brings the second hint");
        var (found, _, _) = KitchenTrials.Record(log, specs, KitchenMethod.Cook, In(("Honey", 3), ("Grain", 2), ("Pits", 1)), new InventedRecipe(), 4);
        Assert.AreEqual("cake", found.found);
        Assert.AreEqual(2, log.triedThisSeventh);
        Assert.AreEqual(1, KitchenTrials.TriesLeft(log, 4, 3));
        Assert.AreEqual(3, KitchenTrials.TriesLeft(log, 5, 3), "a new Seventh, new tries");
        // The same mix again (another batch size) replaces its trial and keeps what it found.
        var (again, _, _) = KitchenTrials.Record(log, specs, KitchenMethod.Cook, In(("Honey", 6), ("Grain", 4), ("Pits", 2)), new InventedRecipe(), 5);
        Assert.AreEqual("cake", again.found);
        Assert.AreEqual(2, log.trials.Count);
        Assert.AreSame(again, KitchenTrials.Tried(log, KitchenMethod.Cook, In(("Pits", 1), ("Grain", 2), ("Honey", 3))));
        Assert.IsNull(KitchenTrials.Tried(log, KitchenMethod.Ferment, In(("Pits", 1), ("Grain", 2), ("Honey", 3))));
        Assert.AreEqual(3, log.total);
    }

    [Test]
    public void Apply_MakesTheKeptDishFinerThanItsIngredients()
    {
        var spec = new HiddenRecipeSpec { formula = Cake(), foodValueMultiplier = 1.5f, neverSpoils = true, luxury = "Sweets", unity = 1f, faithPerUnit = 0.2f };
        spec.formula.description = "Baked hard for the road.";
        var dish = new InventedRecipe { foodValue = 2f, spoilPerSeventh = 0.05f, unity = 0.5f };
        KitchenTrials.Apply(spec, dish);
        Assert.AreEqual(3f, dish.foodValue, 1e-4f);
        Assert.AreEqual(0f, dish.spoilPerSeventh);
        CollectionAssert.Contains(dish.luxuries, "Sweets");
        Assert.AreEqual(1.5f, dish.unity, 1e-4f);
        Assert.AreEqual(0.2f, dish.faithPerUnit, 1e-4f);
        Assert.AreEqual("cake", dish.hidden);
        Assert.AreEqual("Baked hard for the road.", dish.description);
        Assert.DoesNotThrow(() => KitchenTrials.Apply(null, dish));
    }

    [Test]
    public void TheCellar_NeverBrewsABloom()
    {
        var spec = KitchenTrials.Defaults().First(s => s.Method == KitchenMethod.Ferment);
        spec.formula.parts.Add(new FormulaPart { component = "Glimmerfern", minShare = 0.01f, maxShare = 0.1f });
        StringAssert.Contains("Eleos Bloom", KitchenTrials.Problem(spec, _ => true, r => r == "Glimmerfern"));
    }
}
