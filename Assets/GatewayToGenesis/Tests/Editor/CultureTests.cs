using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The culture's rules (<see cref="CultureRules"/>, <see cref="FoundingMyths"/>, <see cref="CultureTuning"/>) with no scene:
/// names (Iridia: Iridian), the founding myths' baselines, leanings drifting toward what the culture lives, reforms,
/// foodways by class and the national food, spread over held land, the kitchen labels of the stores, and the story
/// grammar ("culture:" consequences and the {nation} tokens). Pure, so they also run outside Unity.
/// </summary>
public class CultureTests
{
    // ===== NAMES =====

    [TestCase("Iridia", "Iridian")]
    [TestCase("Vaelora", "Vaeloran")]
    [TestCase("Aster", "Asterian")]
    [TestCase("Kethe", "Kethean")]
    [TestCase("Solvary", "Solvarian")]
    [TestCase("iridia", "Iridian")]
    public void DemonymsAreSuggestedFromTheName(string name, string demonym)
    {
        Assert.AreEqual(demonym, CultureRules.SuggestDemonym(name));
        Assert.AreEqual(demonym, CultureRules.SuggestAdjective(name), "the culture is named after its people unless the player says otherwise");
    }

    [Test]
    public void NamesAreCleanedAndChecked()
    {
        Assert.AreEqual("The Hollow Crown", CultureRules.CleanName("  the   hollow crown "));
        Assert.IsNull(CultureRules.WhyNotName("Iridia"));
        Assert.IsNull(CultureRules.WhyNotName("Xian-K'in"));
        Assert.IsNotNull(CultureRules.WhyNotName("I"), "too short");
        Assert.IsNotNull(CultureRules.WhyNotName(new string('a', CultureRules.MaxNameLength + 1)), "too long");
        Assert.IsNotNull(CultureRules.WhyNotName("Iridia<b>"), "no markup");
        Assert.IsNotNull(CultureRules.WhyNotName("12"), "letters only");
        Assert.AreEqual("Iridians", CultureRules.Plural("Iridian"));
    }

    [Test]
    public void StoryTokensNameTheCulture()
    {
        var identity = new CultureIdentity { name = "Iridia", demonym = "Iridian", adjective = "Iridian" };
        Assert.AreEqual("The Iridians of Iridia keep the Iridian way; Earth-Beans is their national food.",
            CultureRules.Expand("The {people} of {nation} keep the {culture} way; {national_food} is their {national_label}.", identity, null, "Earth-Beans"));
        Assert.AreEqual("your people of your nation", CultureRules.Expand("{people} of {nation}", new CultureIdentity()), "unnamed, the words fall back");
        Assert.AreEqual("Glimmerfern, the national spice", CultureRules.Expand("{national_food}, the {national_label}", identity, null, "Glimmerfern", FoodClass.Spice));
        Assert.AreEqual("no tokens", CultureRules.Expand("no tokens", identity));
    }

    // ===== FOUNDING MYTHS =====

    [Test]
    public void EachFoundingMythIsAChorusStanceWithABaseline()
    {
        CollectionAssert.AreEquivalent(new[] { "Idealism", "Realism", "Pragmatism" }, FoundingMyths.All.Select(m => m.stance));
        foreach (var myth in FoundingMyths.All)
        {
            Assert.AreEqual(1f, myth.baseline.Values.Sum(), 1e-3f, $"{myth.id}'s baseline is a whole culture");
            Assert.AreSame(myth, FoundingMyths.Find(myth.id));
            Assert.AreSame(myth, FoundingMyths.Find(myth.stance.ToLowerInvariant()));
            Assert.IsNotEmpty(myth.effects, $"{myth.id} gives something while it is told");
            foreach (var effect in myth.effects) Assert.IsTrue(effect.ValidateShape().isValid, $"{myth.id}: {effect}");
            var baseline = CultureRules.Baseline(myth);
            Assert.AreEqual(myth.baseline.OrderByDescending(p => p.Value).First().Key, CultureRules.Ranked(baseline)[0]);
        }
        Assert.AreEqual(EnclaveFamily.Weaver, CultureRules.Ranked(CultureRules.Baseline(FoundingMyths.Song))[0], "to keep the song");
        Assert.AreEqual(EnclaveFamily.Agromagical, CultureRules.Ranked(CultureRules.Baseline(FoundingMyths.Hearth))[0], "to share the hearth");
        Assert.AreEqual(EnclaveFamily.Industrious, CultureRules.Ranked(CultureRules.Baseline(FoundingMyths.Ruins))[0], "to build on the ruins");
    }

    // ===== LEANINGS =====

    [Test]
    public void AMythAloneIsItsMyth()
    {
        var target = CultureRules.Target(new CultureInputs { myth = FoundingMyths.Hearth }, new CultureTuning());
        CollectionAssert.AreEqual(CultureRules.Baseline(FoundingMyths.Hearth), target);
    }

    [Test]
    public void WhatTheCultureLivesPullsItsTarget()
    {
        var tuning = new CultureTuning();
        var inputs = new CultureInputs { myth = FoundingMyths.Song };
        inputs.pillars["aureus"] = 1f; inputs.pillars["regalia"] = 1f; inputs.pillars["waltz"] = 1f; inputs.pillars["chorus"] = 5f;
        inputs.civics.Add(EnclaveFamily.Militant);
        inputs.used[EnclaveFamily.Domestication] = 50f;
        inputs.land[EnclaveFamily.Industrious] = 3f;
        inputs.districts.Add(EnclaveFamily.Trading);
        var target = CultureRules.Target(inputs, tuning);
        var myth = CultureRules.Baseline(FoundingMyths.Song);
        Assert.AreEqual(1f, target.Sum(), 1e-4f);
        Assert.Greater(target[(int)EnclaveFamily.Esoteric], myth[(int)EnclaveFamily.Esoteric], "a strong Chorus pillar leans Esoteric beyond the myth");
        foreach (var f in new[] { EnclaveFamily.Militant, EnclaveFamily.Domestication, EnclaveFamily.Industrious })
            Assert.Greater(target[(int)f], 0f, $"{f} comes from what is lived, not the myth");
        float total = tuning.mythWeight + tuning.pillarWeight + tuning.civicWeight + tuning.usedWeight + tuning.landWeight + tuning.districtWeight;
        Assert.AreEqual(tuning.civicWeight / total, target[(int)EnclaveFamily.Militant], 1e-4f, "one civic weighs its weight");
    }

    [Test]
    public void LeaningsDriftSlowlyAndReformsMoveAtOnce()
    {
        var from = CultureRules.Baseline(FoundingMyths.Song);
        var to = new float[CultureRules.Families.Length];
        to[(int)EnclaveFamily.Militant] = 1f;
        var drifted = CultureRules.Drift(from, to, 0.1f);
        Assert.AreEqual(1f, drifted.Sum(), 1e-4f);
        Assert.AreEqual(0.1f, drifted[(int)EnclaveFamily.Militant], 1e-4f, "a tenth of the way");
        Assert.AreEqual(from[(int)EnclaveFamily.Weaver] * 0.9f, drifted[(int)EnclaveFamily.Weaver], 1e-4f);

        var reformed = CultureRules.Reform(from, EnclaveFamily.Esoteric, 0.2f);
        Assert.AreEqual(from[(int)EnclaveFamily.Esoteric] * 0.8f + 0.2f, reformed[(int)EnclaveFamily.Esoteric], 1e-4f);
        Assert.AreEqual(1f, reformed.Sum(), 1e-4f);
        Assert.AreEqual("Weaver", CultureRules.Character(from));
        var close = new float[CultureRules.Families.Length];
        close[(int)EnclaveFamily.Weaver] = 0.5f; close[(int)EnclaveFamily.Auric] = 0.45f;
        Assert.AreEqual("Weaver and Auric", CultureRules.Character(close));
        Assert.AreEqual("Unformed", CultureRules.Character(new float[CultureRules.Families.Length]));
    }

    [Test]
    public void TablesFindFamiliesForCivicsResourcesAndLand()
    {
        var tuning = new CultureTuning();
        Assert.AreEqual(EnclaveFamily.Agromagical, tuning.FamilyOfCivic("culinary alchemists"), "the vault's civic families");
        Assert.AreEqual(EnclaveFamily.Esoteric, tuning.FamilyOfCivic("Sky Glass Burials"));
        Assert.AreEqual(EnclaveFamily.Militant, tuning.FamilyOfCivic("Military Academy"));
        Assert.IsNull(tuning.FamilyOfCivic("No Such Civic"));
        Assert.AreEqual(EnclaveFamily.Weaver, tuning.FamilyOfResource("Silverreed"));
        Assert.AreEqual(EnclaveFamily.Agromagical, tuning.FamilyOfResource("Eleos Tea"));
        Assert.AreEqual(EnclaveFamily.Weaver, tuning.FamilyOfLand("moonlit-grove"), "the first word wins: moonlit before grove");
        Assert.AreEqual(EnclaveFamily.Agromagical, tuning.FamilyOfLand("skeletal-orchard"));
        Assert.AreEqual(EnclaveFamily.Industrious, tuning.FamilyOfLand("peaks"));
        Assert.IsNull(tuning.FamilyOfLand("open-water"));
        foreach (var family in CultureRules.Families)
            Assert.IsTrue(tuning.character.Any(c => c.family == family), $"{family} has a character effect");
        foreach (var c in tuning.character) Assert.IsTrue(c.ToEffect().ValidateShape().isValid, $"{c.family}: {c.ToEffect()}");
        CollectionAssert.AreEqual(new[] { EnclaveFamily.Auric, EnclaveFamily.Regal, EnclaveFamily.Weaver, EnclaveFamily.Esoteric },
            new[] { "aureus", "regalia", "waltz", "chorus" }.Select(CultureRules.FamilyOfPillar));
    }

    // ===== FOODWAYS =====

    [Test]
    public void FamiliarityFollowsEachClassApart()
    {
        var tuning = new CultureTuning { familiarityRate = 0.5f, heldWeight = 0.5f };
        var ways = new List<Foodway>();
        var diet = new[]
        {
            new DietEntry("Dried Auric Peaches", FoodClass.Edible, held: 30f, used: 0f),
            new DietEntry("Earth-Beans", FoodClass.Edible, held: 10f, used: 0f),
            new DietEntry("Glimmerfern", FoodClass.Spice, held: 4f, used: 0f),
            new DietEntry("Bitter Roots", FoodClass.Edible, held: 0f, used: 0f),
        };
        CultureRules.Taste(ways, diet, tuning);
        Assert.AreEqual(3, ways.Count, "a food never held nor eaten is not known");
        Assert.AreEqual(0.375f, ways.Single(w => w.resource == "Dried Auric Peaches").familiarity, 1e-4f, "half way to its 75% share of the edibles");
        Assert.AreEqual(0.5f, ways.Single(w => w.resource == "Glimmerfern").familiarity, 1e-4f, "the only spice: all of its class");
        Assert.AreEqual(1, ways.Single(w => w.resource == "Dried Auric Peaches").firstSevenths);
        Assert.AreEqual(0, ways.Single(w => w.resource == "Earth-Beans").firstSevenths);

        // Eating shifts the table: what was drawn weighs half, what is held the other half.
        CultureRules.Taste(ways, new[] { new DietEntry("Dried Auric Peaches", FoodClass.Edible, 10f, 0f), new DietEntry("Earth-Beans", FoodClass.Edible, 10f, 40f) }, tuning);
        var beans = ways.Single(w => w.resource == "Earth-Beans");
        Assert.AreEqual(1, beans.firstSevenths, "beans are first at the table now");
        Assert.AreEqual(0, ways.Single(w => w.resource == "Dried Auric Peaches").firstSevenths);
        Assert.AreEqual(40f, beans.eaten, 1e-4f);
        Assert.AreEqual(0.125f + (0.75f - 0.125f) / 2f, beans.familiarity, 1e-4f, "0.125 moving half way to its 75% share");
    }

    [Test]
    public void AFoodBecomesNationalOnceFamiliarAndFirstForLong()
    {
        var tuning = new CultureTuning { familiarityRate = 0.2f, nationalFamiliarity = 0.45f, nationalSevenths = 5, maxNationalPerClass = 1 };
        var ways = new List<Foodway>();
        var diet = new[] { new DietEntry("Earth-Beans", FoodClass.Edible, 10f, 0f), new DietEntry("Ash-Bread", FoodClass.Edible, 2f, 0f), new DietEntry("Peach Pits", FoodClass.Ingredient, 3f, 0f) };
        int sevenths = 0;
        Foodway next = null;
        while (next == null && sevenths < 50)
        {
            CultureRules.Taste(ways, diet, tuning);
            sevenths++;
            next = CultureRules.NextNational(ways, tuning);
        }
        Assert.IsNotNull(next);
        Assert.AreEqual("Earth-Beans", next.resource, "edibles come first");
        Assert.GreaterOrEqual(sevenths, tuning.nationalSevenths);
        next.national = true;
        next = CultureRules.NextNational(ways, tuning);
        Assert.AreEqual("Peach Pits", next.resource, "then an ingredient: the edibles' class is full");
        Assert.AreEqual("national ingredient", CultureRules.NationalLabel(next.cuisine));
        next.declined = true;
        Assert.IsNull(CultureRules.NextNational(ways, tuning), "a declined food is not offered again");
        next.familiarity = 0.1f;
        CultureRules.Taste(ways, new DietEntry[0], tuning);
        Assert.IsFalse(next.declined, "until its familiarity has fallen");
    }

    [Test]
    public void TheStoresEatEdiblesFirstAndNeverSpices()
    {
        var beans = new FoodKind { resource = "Earth-Beans", foodValue = 0.5f, spoilPerSeventh = 0.01f };
        var honey = new FoodKind { resource = "Wild Honey", foodValue = 1.5f, spoilPerSeventh = 0.5f, cuisine = FoodClass.Ingredient };
        var fern = new FoodKind { resource = "Glimmerfern", foodValue = 2f, cuisine = FoodClass.Spice };
        var stock = new List<FoodStock> { new FoodStock(beans, 4f), new FoodStock(honey, 10f), new FoodStock(fern, 10f) };
        Assert.AreEqual(2f + 15f, PantryRules.Value(stock), 1e-4f, "spices feed no one");
        var taken = PantryRules.Draw(stock, 5f);
        Assert.AreEqual("Earth-Beans", taken[0].resource, "edibles first, though honey spoils faster");
        Assert.AreEqual(4f, taken[0].amount, 1e-4f);
        Assert.AreEqual("Wild Honey", taken[1].resource, "then ingredients, raw");
        Assert.AreEqual(2f, taken[1].amount, 1e-4f);
        Assert.IsFalse(PantryRules.Draw(stock, 100f).Any(t => t.resource == "Glimmerfern"), "never spices");
        var peaches = new FoodKind { resource = "Dried Auric Peaches", foodValue = 1f, leaves = "Peach Pits", leavesPerUnit = 0.25f };
        Assert.AreEqual(("Peach Pits", 2f), PantryRules.Leftover(peaches, 8f).Value);
        Assert.IsNull(PantryRules.Leftover(beans, 8f));
    }

    [Test]
    public void EleosTeasAreDrunkLikeFoodButKeepATableOfTheirOwn()
    {
        Assert.IsTrue(CultureRules.Feeds(FoodClass.EleosTea));
        Assert.IsFalse(CultureRules.Feeds(FoodClass.Spice));
        Assert.AreEqual(CultureRules.EatingTier(FoodClass.Edible), CultureRules.EatingTier(FoodClass.EleosTea), "drunk alongside the edibles");
        Assert.Less(CultureRules.EatingTier(FoodClass.EleosTea), CultureRules.EatingTier(FoodClass.Ingredient));
        CollectionAssert.AreEquivalent(System.Enum.GetValues(typeof(FoodClass)), CultureRules.TableClasses, "every category has its place at the table");
        Assert.AreEqual("Teas", CultureRules.ClassName(FoodClass.EleosTea, true));
        Assert.AreEqual("national tea", CultureRules.NationalLabel(FoodClass.EleosTea));

        var tea = new FoodKind { resource = "Eleos Tea", foodValue = 0.5f, spoilPerSeventh = 0.2f, cuisine = FoodClass.EleosTea };
        var beans = new FoodKind { resource = "Earth-Beans", foodValue = 0.5f, spoilPerSeventh = 0.01f };
        var pits = new FoodKind { resource = "Peach Pits", foodValue = 0.2f, spoilPerSeventh = 0.5f, cuisine = FoodClass.Ingredient };
        var stock = new List<FoodStock> { new FoodStock(beans, 10f), new FoodStock(tea, 10f), new FoodStock(pits, 10f) };
        Assert.AreEqual(5f + 5f + 2f, PantryRules.Value(stock), 1e-4f, "teas feed");
        var taken = PantryRules.Draw(stock, 7f);
        Assert.AreEqual("Eleos Tea", taken[0].resource, "with the edibles, the more perishable first");
        Assert.AreEqual("Earth-Beans", taken[1].resource, "then the edibles, before any ingredient");
        Assert.IsFalse(taken.Any(t => t.resource == "Peach Pits"));

        // A tea at the table is its own class: it does not compete with the edibles.
        var ways = new List<Foodway>();
        CultureRules.Taste(ways, new[] { new DietEntry("Earth-Beans", FoodClass.Edible, 5f, 0f), new DietEntry("Eleos Tea", FoodClass.EleosTea, 1f, 0f) }, new CultureTuning { familiarityRate = 1f });
        Assert.AreEqual(1f, ways.Single(w => w.resource == "Eleos Tea").familiarity, 1e-4f, "all of the teas");
        Assert.AreEqual(1f, ways.Single(w => w.resource == "Earth-Beans").familiarity, 1e-4f, "all of the edibles");
    }

    [Test]
    public void BeveragesAreTheirOwnClass_DrunkForJoyAndOpenedLastForHunger()
    {
        Assert.IsTrue(CultureRules.Feeds(FoodClass.Beverage), "an ale still has something in it");
        Assert.Greater(CultureRules.EatingTier(FoodClass.Beverage), CultureRules.EatingTier(FoodClass.Ingredient), "the cellar is opened after the raw ingredients");
        Assert.AreEqual("Beverages", CultureRules.ClassName(FoodClass.Beverage, true));
        Assert.AreEqual("Beverage", CultureRules.ClassName(FoodClass.Beverage));
        Assert.AreEqual("national drink", CultureRules.NationalLabel(FoodClass.Beverage));
        Assert.AreNotEqual(CultureRules.ClassName(FoodClass.EleosTea), CultureRules.ClassName(FoodClass.Beverage), "a drink is not a tea");
        Assert.Less(CultureRules.TableOrder(FoodClass.EleosTea), CultureRules.TableOrder(FoodClass.Beverage));
        Assert.Less(CultureRules.TableOrder(FoodClass.Beverage), CultureRules.TableOrder(FoodClass.Ingredient));

        var ale = new FoodKind { resource = "Rootgrain Ale", foodValue = 0.6f, spoilPerSeventh = 0.9f, cuisine = FoodClass.Beverage };
        var beans = new FoodKind { resource = "Earth-Beans", foodValue = 0.5f, spoilPerSeventh = 0.01f };
        var pits = new FoodKind { resource = "Peach Pits", foodValue = 0.2f, spoilPerSeventh = 0.002f, cuisine = FoodClass.Ingredient };
        var stock = new List<FoodStock> { new FoodStock(ale, 10f), new FoodStock(beans, 4f), new FoodStock(pits, 5f) };
        Assert.AreEqual(6f + 2f + 1f, PantryRules.Value(stock), 1e-4f);
        var taken = PantryRules.Draw(stock, 4f);
        Assert.AreEqual(new[] { "Earth-Beans", "Peach Pits", "Rootgrain Ale" }, taken.Select(t => t.resource).ToArray(), "however perishable the ale, bread and pits go first");
        Assert.AreEqual(10f / 6f, taken[2].amount, 1e-4f, "only what is still needed");

        // At the table a drink competes only with other drinks for the national drink.
        var ways = new List<Foodway>();
        CultureRules.Taste(ways, new[] { new DietEntry("Earth-Beans", FoodClass.Edible, 5f, 0f), new DietEntry("Rootgrain Ale", FoodClass.Beverage, 1f, 0f), new DietEntry("Eleos Tea", FoodClass.EleosTea, 3f, 0f) },
            new CultureTuning { familiarityRate = 1f });
        Assert.AreEqual(1f, ways.Single(w => w.resource == "Rootgrain Ale").familiarity, 1e-4f, "all of the drinks");
        Assert.AreEqual(1f, ways.Single(w => w.resource == "Eleos Tea").familiarity, 1e-4f, "the tea keeps its own table");
    }

    // ===== SPREAD =====

    [Test]
    public void HeldLandTakesTheCultureNearSettlementsFirstAndLostLandLetsItFade()
    {
        var tuning = new CultureTuning { spreadPerSeventh = 0.1f, seatSpreadBonus = 0.2f, fadePerSeventh = 0.5f };
        Assert.AreEqual(1f, CultureRules.Nearness(0, 4));
        Assert.AreEqual(0.5f, CultureRules.Nearness(2, 4), 1e-4f);
        Assert.AreEqual(0f, CultureRules.Nearness(6, 4));
        float atSeat = CultureRules.Spread(0f, true, 1f, tuning), far = CultureRules.Spread(0f, true, 0f, tuning);
        Assert.AreEqual(0.3f, atSeat, 1e-4f);
        Assert.AreEqual(0.1f, far, 1e-4f);
        Assert.Less(CultureRules.Spread(0.9f, true, 1f, tuning) - 0.9f, atSeat, "slower as it fills");
        Assert.LessOrEqual(CultureRules.Spread(1f, true, 1f, tuning), 1f);
        Assert.AreEqual(0.4f, CultureRules.Spread(0.8f, false, 1f, tuning), 1e-4f, "no longer held: it fades");
        Assert.AreEqual(0.5f, CultureRules.Cohesion(new[] { 1f, 0f, 0.5f }), 1e-4f);
        Assert.AreEqual(0f, CultureRules.Cohesion(new float[0]));
    }

    // ===== ACHIEVEMENTS =====

    [Test]
    public void AchievementRules_Culture()
    {
        var rebirth = AchievementTriggers.Rules["digestive-rebirth"];
        Assert.IsTrue(rebirth(AchievementEvent.Of(AchievementSignal.CultureReformed, 1, true)), "a reform that digests a ruin");
        Assert.IsFalse(rebirth(AchievementEvent.Of(AchievementSignal.CultureReformed, 1, false)), "a reform with no ruin is not a rebirth");
        Assert.IsTrue(rebirth(AchievementEvent.Of(AchievementSignal.RuinCivicAdopted)), "the civic half still counts");
        Assert.IsTrue(AchievementTriggers.Rules["ship-of-theseus"](AchievementEvent.Of(AchievementSignal.FoundingCivicsGone, 2)));
        Assert.IsFalse(AchievementTriggers.Rules["ship-of-theseus"](AchievementEvent.Of(AchievementSignal.CultureReformed, 1, true)));
    }

    // ===== STORIES =====

    [Test]
    public void CultureConsequencesParseAndAreWorded()
    {
        var myth = EventScript.ParseConsequence("culture:myth hearth +1");
        Assert.AreEqual(EventConsequence.ConsequenceType.CultureChange, myth.type);
        Assert.AreEqual("myth hearth", myth.targetName);
        StringAssert.Contains("To Share the Hearth", EventText.DescribeConsequence(myth));
        Assert.AreEqual(EventConsequence.ConsequenceType.CultureChange, EventScript.ParseConsequence("culture:leaning Weaver +10").type);
        Assert.AreEqual(EventConsequence.ConsequenceType.CultureChange, EventScript.ParseConsequence("culture:embrace +1").type);
        var problems = new List<string>();
        Assert.IsNull(EventScript.ParseConsequence("culture:myth nothing +1", problems));
        Assert.IsNull(EventScript.ParseConsequence("culture:leaning Nobody +5", problems));
        Assert.AreEqual(2, problems.Count);
        Assert.IsTrue(CultureRules.ParseConsequence("presence", out string verb, out _));
        Assert.AreEqual("presence", verb);
        Assert.IsTrue(GameValues.IsKnownDomain("culture"));
        Assert.IsTrue(GameValues.IsKnownDomain("national_food"));
    }
}
