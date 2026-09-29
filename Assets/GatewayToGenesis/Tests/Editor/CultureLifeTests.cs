using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The life of the culture with no scene (<see cref="CultureLifeRules"/>, <see cref="CultureNaming"/>,
/// <see cref="CultureCalendar"/>, the expedition charters in <see cref="Expeditions.Effective"/>): names given in the
/// culture's voice, the holiday calendar, Unity, happiness (surviving and living), luxuries, the kitchen and why a
/// holiday cannot be set apart. Pure, so they also run outside Unity.
/// </summary>
public class CultureLifeTests
{
    [Test]
    public void OnlyWantedCategoriesSpendStock_AndGlassIsKept()
    {
        var life = new CultureLifeTuning();
        var plan = CultureLifeRules.Luxuries(life, _ => 100f, life.livingFrom);
        Assert.AreEqual(1, plan.draws.Count);
        Assert.AreEqual("Ornaments", plan.draws[0].category, "equally satisfying lasting amenities win before consuming stores");
        Assert.IsEmpty(plan.draws[0].takes);
        Assert.AreEqual(0.45f, plan.draws[0].enjoys.Sum(x => x.amount), 0.001f);
        Assert.AreEqual(0.18f, plan.Faith, 0.001f);
        Assert.AreEqual(1f, plan.score);
    }

    [Test]
    public void SacredTeaGivesFaithOnlyForTheQuantityConsumed()
    {
        var life = new CultureLifeTuning();
        var plan = CultureLifeRules.Luxuries(life, r => r == "Candlevein Grief Tea" ? 1f : 0f, 150);
        Assert.AreEqual(1f, plan.draws.Sum(d => d.Taken), 0.001f);
        Assert.AreEqual(0.4f, plan.Faith, 0.001f);
        Assert.AreEqual(1f / 1.5f, plan.score, 0.001f);
        var ordinary = CultureLifeRules.Luxuries(life, r => r == "Hearthleaf Tea" ? 10f : 0f, 150);
        Assert.AreEqual(1f, ordinary.score);
        Assert.AreEqual(0f, ordinary.Faith, "not every tea is sacred");
        var scarce = CultureLifeRules.Luxuries(life, r => 10f, 150, mayConsume: _ => false);
        Assert.IsTrue(scarce.draws.All(d => d.takes.Count == 0), "survival can reserve edible stock while durable glass is still enjoyed");
    }

    [Test]
    public void SharedGoodsCannotMeetTwoCategoriesWithTheSameUnit()
    {
        var life = new CultureLifeTuning
        {
            livingFrom = 100, peoplePerLuxury = 1,
            luxuries = new List<LuxurySpec>
            {
                new LuxurySpec { category = "A", resources = { "Shared" }, perHundred = 1f },
                new LuxurySpec { category = "B", resources = { "Shared" }, perHundred = 1f },
            },
        };
        var plan = CultureLifeRules.Luxuries(life, _ => 1f, 200);
        Assert.AreEqual(1f, plan.draws.Sum(d => d.Taken), 0.001f);
        Assert.AreEqual(0.25f, plan.score, 0.001f);
    }

    [Test]
    public void LivingBloomsNeedSurveyOwnershipAndVigor_AndNeverNeedHarvesting()
    {
        var settings = EcologyTests.Settings();
        var map = EcologyTests.Fresh(42, settings);
        var site = map.ResourceSites.First();
        settings.resourceSites.Add(new ResourceSiteSpec { id = "vow-orchids", kind = ResourceKind.Bloom });
        site.spec = "vow-orchids";
        site.kind = ResourceKind.Bloom;
        site.vigor = 1f;
        var tile = map[site.center];
        var life = new CultureLifeTuning();
        tile.authorityId = WorldAuthority.Wilderness;
        tile.explored = false;
        Assert.IsEmpty(CultureLifeRules.Amenities(map, settings, life));
        tile.explored = true;
        Assert.IsEmpty(CultureLifeRules.Amenities(map, settings, life));
        tile.authorityId = WorldAuthority.Player;
        var places = CultureLifeRules.Amenities(map, settings, life);
        Assert.AreEqual(1, places.Count);
        Assert.AreEqual(3f, places[0].amount, 0.001f);
        var plan = CultureLifeRules.Luxuries(life, _ => 0f, 150, places);
        Assert.AreEqual(1f, plan.score);
        Assert.IsEmpty(plan.draws.SelectMany(d => d.takes));
        Assert.AreEqual(0.6f, plan.Faith, 0.001f);
        site.vigor = 0f;
        Assert.IsEmpty(CultureLifeRules.Amenities(map, settings, life), "a withered garden gives no amenities or Faith");
    }

    [Test]
    public void CulturalGoodsAreRealResources_WithPantryRecipesAndMapSources()
    {
        GameCatalog.InvalidateAll();
        var life = new CultureLifeTuning();
        var pantry = UnityEngine.Resources.Load<PantrySettings>("Food/Pantry");
        var gen = GameCatalog.World.All.First().generation;
        foreach (var name in new[] { "Auric Saffron", "Silver Salt", "Candlevein Grief Tea", "Lullroot Tea", "Hearthleaf Tea", "Saffron Riverfish Pilaf", "Honeyed Peach Tart" })
        {
            Assert.IsTrue(GameCatalog.IsResource(name), name);
            Assert.IsNotNull(pantry.Kind(name), name);
            Assert.IsTrue(life.luxuries.Any(l => l.resources.Contains(name)), name);
            Assert.IsTrue(life.IsDish(name) || gen.resourceSites.Any(s => s.harvest.Any(h => h.resource == name)), name + " has a source");
        }
        Assert.AreEqual(0f, pantry.Kind("Auric Saffron").foodValue, "spice is not sustenance");
        Assert.IsTrue(life.Good("Sky Glass").amenity);
        var problems = new List<string>();
        typeof(ContentValidator).GetMethod("ValidateCultureLife", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { problems });
        CollectionAssert.IsEmpty(problems);
    }

    [TestCase(3)]
    [TestCase(1234)]
    [TestCase(98765)]
    public void SpecialtyResourcesRespectTheirMapHabitats(int seed)
    {
        GameCatalog.InvalidateAll();
        var gen = GameCatalog.World.All.First().generation;
        var map = WorldGenerator.Generate(seed, gen, WorldSystem.LoadStencil(gen), WorldSystem.LoadTiles(gen));
        WorldSites.PlaceAge(map, gen, 1);
        var saffron = map.ResourceSites.Where(s => s.spec == "auric-saffron").ToList();
        var candidates = map.Tiles.Where(t => gen.ResourceSite("auric-saffron").terrains.Contains(t.terrain)).ToList();
        UnityEngine.Debug.Log($"Luxury habitat seed {seed}: {saffron.Count} saffron; maximum natural desirability {candidates.Max(WorldResources.NaturalDesirability):0.000}");
        Assert.IsNotEmpty(saffron);
        foreach (var site in map.ResourceSites.Where(s => s.spec == "auric-saffron" || s.spec == "hearthleaf-grove"))
        {
            var spec = gen.ResourceSite(site.spec);
            Assert.GreaterOrEqual(WorldResources.NaturalDesirability(map[site.center]) + 0.001f, spec.minimumDesirability);
        }
        foreach (var site in map.ResourceSites.Where(s => s.spec == "silver-salt"))
        {
            var tile = map[site.center];
            Assert.IsTrue(map.NeighboursOf(tile).Any(n => n.water && !n.lake));
            Assert.GreaterOrEqual(tile.coherence - tile.siteCoherence + 0.001f, 0.65f);
        }
        Assert.IsTrue(map.ResourceSites.Any(s => s.spec == "sacred-skyglass"));
        foreach (var site in map.ResourceSites.Where(s => s.spec == "sacred-skyglass"))
            Assert.IsTrue(map[site.center].sacred || map.NeighboursOf(map[site.center]).Any(n => n.sacred));
    }

    private static readonly NamingVoice Weaver = new NamingVoice(EnclaveFamily.Weaver, "song", "Iridian", "Iridia");
    private static readonly NamingVoice Esoteric = new NamingVoice(EnclaveFamily.Esoteric, "hearth");

    // ===== NAMES =====

    [Test]
    public void TheSameCultureNamingTheSamePlaceSaysTheSameThing()
    {
        int seed = CultureNaming.Seed("place", 4, "trading", "Iridia", 0);
        Assert.AreEqual(seed, CultureNaming.Seed("place", 4, "trading", "Iridia", 0), "seeds do not depend on the run");
        Assert.AreEqual(CultureNaming.District(Weaver, EnclaveFamily.Trading, seed), CultureNaming.District(Weaver, EnclaveFamily.Trading, seed));
        Assert.AreNotEqual(seed, CultureNaming.Seed("place", 5, "trading", "Iridia", 0));
    }

    [Test]
    public void DistrictsAreNamedForWhatTheyAreInTheCulturesVoice()
    {
        var kinds = new[] { "Market", "Bazaar" };
        var voices = CultureNaming.WordsOf(EnclaveFamily.Weaver);
        var motifs = CultureNaming.MotifsOf("song");
        bool voiced = false;
        for (int seed = 0; seed < 60; seed++)
        {
            string name = CultureNaming.District(Weaver, EnclaveFamily.Trading, seed);
            Assert.IsTrue(kinds.Any(k => name.Contains(k)), $"'{name}' says what the district is");
            voiced |= voices.Any(w => name.Contains(w)) || motifs.Any(m => name.Contains(m)) || name.Contains("Iridian");
        }
        Assert.IsTrue(voiced, "a Weaver people of the Song names in verses, its myth or its own name");
    }

    [Test]
    public void AHamletIsOnePlaceName()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            string name = CultureNaming.District(Esoteric, null, seed);
            Assert.IsFalse(name.Contains(" "), $"'{name}' is one word (Moonford)");
            Assert.IsTrue(new[] { "Moon", "Star", "Veil", "Silver" }.Any(p => name.StartsWith(p)), $"'{name}' begins in the Esoteric voice");
        }
    }

    [Test]
    public void NamesAreNeverGivenTwice()
    {
        var taken = new HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            string name = CultureNaming.Landmark(Esoteric, "Temple", 7, taken);
            Assert.IsFalse(taken.Contains(name), name);
            StringAssert.Contains("Temple", name);
            taken.Add(name);
        }
    }

    [Test]
    public void HolidaysRememberTheirOccasion()
    {
        Assert.AreEqual("The Feast of Peach Soup", CultureNaming.Holiday(Weaver, "national-food", "Peach Soup", 3));
        Assert.AreEqual("Iridia Naming Day", CultureNaming.Holiday(Weaver, "named", null, 3));
        Assert.AreEqual("The Consecration of Moon Temple", CultureNaming.Holiday(Esoteric, "landmark", "The Moon Temple", 3));
        Assert.AreEqual("The Songfeast of Renewal", CultureNaming.Holiday(Weaver, "reform", null, 3));
        StringAssert.StartsWith("The Vigil of the", CultureNaming.Holiday(Esoteric, "joy", null, 3), "an Esoteric people keeps vigils");
    }

    // ===== THE CALENDAR =====

    [Test]
    public void AHolidayComesRoundEveryEcho()
    {
        var h = new Holiday { seventh = 20, phase = 2 };
        Assert.AreEqual("the 20th Seventh of the second Phase of every Echo", CultureCalendar.Day(h));
        Assert.AreEqual("the Ritual Seventh of the first Phase of every Echo", CultureCalendar.Day(21, 1));
        Assert.AreEqual(0, CultureCalendar.SeventhsUntil(h, 20, 2), "today");
        Assert.AreEqual(1, CultureCalendar.SeventhsUntil(h, 19, 2), "tomorrow");
        Assert.AreEqual(62, CultureCalendar.SeventhsUntil(h, 21, 2), "just past: a whole Echo less a Seventh");
        Assert.AreEqual(22, CultureCalendar.SeventhsUntil(h, 19, 1), "a Phase and a Seventh ahead");
        Assert.AreEqual(63, CultureCalendar.SeventhsPerEcho);
        Assert.AreEqual("the 20th Seventh of the Phase of Flourish, Echo of Resonance, Cycle 2",
            CultureCalendar.Established(new Holiday { seventh = 20, phase = 2, phaseName = "Phase of Flourish", echoName = "Echo of Resonance", cycle = 2 }));
    }

    [TestCase(1, "1st")]
    [TestCase(2, "2nd")]
    [TestCase(3, "3rd")]
    [TestCase(11, "11th")]
    [TestCase(12, "12th")]
    [TestCase(21, "21st")]
    public void Ordinals(int n, string word) => Assert.AreEqual(word, CultureCalendar.Ordinal(n));

    // ===== UNITY =====

    [Test]
    public void UnityComesFromSongRitesDistrictsAndLandmarks()
    {
        var t = new CultureLifeTuning();
        var u = CultureLifeRules.Unity(t, 0.5f, true, 2, new[] { 100f }, 1.5f, 50f);
        Assert.AreEqual(t.songUnity * 0.5f + t.songMythUnity, u.song, 1e-4f, "half a Weaver people sings half as much, plus the Song myth");
        Assert.AreEqual(2 * t.civicUnity, u.civics, 1e-4f);
        Assert.AreEqual(2 * t.districtUnityPer50, u.districts, 1e-4f, "100 development is two steps of 50");
        Assert.AreEqual(1.5f, u.landmarks, 1e-4f);
        Assert.AreEqual(1f, u.multiplier, 1e-4f, "at happiness 50");
        Assert.AreEqual(u.Base, u.Total, 1e-4f);
    }

    [Test]
    public void HappyPeopleSingMore()
    {
        Assert.AreEqual(0.5f, CultureLifeRules.HappinessMultiplier(0f, 0.5f), 1e-4f);
        Assert.AreEqual(1f, CultureLifeRules.HappinessMultiplier(50f, 0.5f), 1e-4f);
        Assert.AreEqual(1.5f, CultureLifeRules.HappinessMultiplier(100f, 0.5f), 1e-4f);
    }

    // ===== HAPPINESS =====

    private static WellbeingInputs Secure(int population) => new WellbeingInputs { population = population, storedValue = population, variety = 3 };

    [Test]
    public void OnTheFrontierThePeopleOnlyAskToSurvive()
    {
        var t = new CultureLifeTuning();
        var w = CultureLifeRules.Happiness(Secure(40), t);
        Assert.AreEqual(0f, w.weight);
        Assert.AreEqual(100f, w.happiness, 1e-3f, "fed, housed, secure and varied: nothing more is asked");
        var hungry = Secure(40);
        hungry.hungry = true;
        Assert.AreEqual(50f, CultureLifeRules.Happiness(hungry, t).happiness, 1e-3f, "hunger takes half of it");
    }

    [Test]
    public void OnceThePeopleAreManyLivingWeighsAsMuchAsSurviving()
    {
        var t = new CultureLifeTuning();
        var bread = CultureLifeRules.Happiness(Secure(t.livingFull), t);
        Assert.AreEqual(1f, bread.weight);
        Assert.AreEqual(50f, bread.happiness, 1e-3f, "bread and shelter alone are half of a grown people's happiness");
        var full = Secure(t.livingFull);
        full.luxury = 1f; full.dishShare = 0.5f; full.joy = 1f; full.landmarks = 1f;
        Assert.AreEqual(100f, CultureLifeRules.Happiness(full, t).happiness, 1e-3f);
        Assert.AreEqual(0.5f, CultureLifeRules.LivingWeight((t.livingFrom + t.livingFull) / 2, t), 0.01f);
    }

    [Test]
    public void HappinessMovesMorale()
    {
        var t = new CultureLifeTuning();
        Assert.AreEqual(0, CultureLifeRules.Morale(50f, t));
        Assert.AreEqual(t.happinessMoraleCap, CultureLifeRules.Morale(100f, t));
        Assert.AreEqual(-t.happinessMoraleCap, CultureLifeRules.Morale(0f, t));
        Assert.AreEqual(4, CultureLifeRules.Morale(70f, t));
        Assert.AreEqual("joyful", CultureLifeRules.MoodWord(95f));
        Assert.AreEqual("miserable", CultureLifeRules.MoodWord(10f));
    }

    [Test]
    public void JoyFades()
    {
        Assert.AreEqual(0.85f, CultureLifeRules.Joy(1f, 0f, 0.15f), 1e-4f);
        Assert.AreEqual(1f, CultureLifeRules.Joy(0.9f, 0.5f, 0.15f), 1e-4f, "capped at 1");
    }

    // ===== LUXURIES =====

    [Test]
    public void TheFrontierWantsNoLuxuries()
    {
        var t = new CultureLifeTuning();
        var plan = CultureLifeRules.Luxuries(t, _ => 100f, t.livingFrom - 1);
        Assert.AreEqual(0, plan.wanted);
        Assert.AreEqual(0, plan.draws.Count, "nothing is taken");
        Assert.AreEqual(0f, plan.score);
    }

    [Test]
    public void AGrowingPeopleWantsMoreKindsOfLuxury()
    {
        var t = new CultureLifeTuning();
        Assert.AreEqual(1, CultureLifeRules.WantedLuxuries(t.livingFrom, t));
        Assert.AreEqual(3, CultureLifeRules.WantedLuxuries(t.livingFrom + 2 * t.peoplePerLuxury, t));
        Assert.AreEqual(t.luxuries.Count, CultureLifeRules.WantedLuxuries(100000, t), "never more than there are");
    }

    [Test]
    public void LuxuriesAreTakenAsFarAsTheStoresReach()
    {
        var t = new CultureLifeTuning();
        var held = new Dictionary<string, float> { { "Wild Honey", 2f }, { "Honeyed Grain Porridge", 10f }, { "Eleos Tea", 1f } };
        int people = t.livingFrom + t.peoplePerLuxury; // 400: two kinds wanted
        var plan = CultureLifeRules.Luxuries(t, r => held.TryGetValue(r, out float v) ? v : 0f, people);
        Assert.AreEqual(2, plan.wanted);
        var sweets = plan.draws.First(d => d.category == "Sweets");
        Assert.AreEqual(4f, sweets.want, 1e-4f, "one per hundred citizens");
        Assert.AreEqual(2f, sweets.takes.First(x => x.resource == "Wild Honey").amount, 1e-4f, "all the honey first");
        Assert.AreEqual(2f, sweets.takes.First(x => x.resource == "Honeyed Grain Porridge").amount, 1e-4f, "then porridge for the rest");
        Assert.AreEqual(1f, sweets.Met, 1e-4f);
        var teas = plan.draws.First(d => d.category == "Teas");
        Assert.AreEqual(0.25f, teas.Met, 1e-4f, "one tea for four wanted");
        Assert.AreEqual((1f + 0.25f) / 2f, plan.score, 1e-4f, "the two best-met kinds, averaged");
    }

    // ===== THE KITCHEN =====

    private static RecipeSpec Soup => new RecipeSpec { id = "peach-soup", dish = "Peach Soup", output = 2f, inputs = { CultureLifeTuning.Res("Dried Auric Peaches", 2f), CultureLifeTuning.Res("Earth-Beans", 2f) } };

    [Test]
    public void TheKitchenCooksWholeBatchesFromWhatIsHeld()
    {
        var held = new Dictionary<string, float> { { "Dried Auric Peaches", 9f }, { "Earth-Beans", 4.5f } };
        Assert.AreEqual(2, CultureLifeRules.Batches(Soup, r => held.TryGetValue(r, out float v) ? v : 0f), "the beans run out after two");
        Assert.AreEqual(0, CultureLifeRules.Batches(Soup, r => r == "Earth-Beans" ? 10f : 0f), "no peaches, no soup");
        Assert.AreEqual(2f, CultureLifeRules.Yield(Soup, false, 0.25f), 1e-4f);
        Assert.AreEqual(2.5f, CultureLifeRules.Yield(Soup, true, 0.25f), 1e-4f, "a civic of the kitchen makes more");
    }

    [Test]
    public void EveryDefaultDishFeedsMoreThanWhatWentIntoIt()
    {
        // The food values of Resources/Food/Pantry (proposals).
        var values = new Dictionary<string, float>
        {
            { "Dried Auric Peaches", 1f }, { "Earth-Beans", 0.5f }, { "Bitter Roots", 0.75f }, { "Deep-Rooted Grain", 1.5f }, { "Ash-Bread", 0.3f },
            { "Behemoth Meat", 5f }, { "Highland Rice", 1.2f }, { "Game Meat", 2f }, { "River Fish", 1.2f }, { "Wild Honey", 1.5f }, { "Peach Pits", 0.2f },
            { "Glimmerfern", 0f }, { "Peach Soup", 2f }, { "Pit-Oil Flatbread", 1.6f }, { "Riverfish Rice", 1.6f }, { "Hunter's Stew", 1.6f },
            { "Honeyed Grain Porridge", 1.8f }, { "Ash-Loaf", 1f }, { "Behemoth Feast Roast", 2.2f },
            { "Saffron Riverfish Pilaf", 2.4f }, { "Honeyed Peach Tart", 2.2f },
        };
        foreach (var recipe in new CultureLifeTuning().recipes.Where(r => !r.InCellar))
        {
            var (input, output) = CultureLifeRules.FoodValue(recipe, r => values.TryGetValue(r, out float v) ? v : 0f, recipe.output);
            Assert.Greater(output, input, $"{recipe.dish}: {input} in, {output} out");
        }
    }

    [Test]
    public void TheCellarBrewsAndDistilsDrinks_NeverFromABloom()
    {
        var t = new CultureLifeTuning();
        var cellar = t.recipes.Where(r => r.InCellar).ToList();
        Assert.IsNotEmpty(cellar.Where(r => r.method == KitchenMethod.Ferment), "ales, meads and wines");
        Assert.IsNotEmpty(cellar.Where(r => r.method == KitchenMethod.Distill), "spirits");
        // What the Eleos Blooms give (World.asset's Bloom sites) and the teas: none of it goes into a drink.
        var bloomsAndTeas = new[] { "Glimmerfern", "Eleos Tea", "Candlevein Grief Tea", "Lullroot Tea", "Hearthleaf Tea" };
        foreach (var r in cellar)
        {
            Assert.IsTrue(t.IsDrink(r.dish) && !t.IsDish(r.dish), r.dish);
            CollectionAssert.IsEmpty(r.inputs.Select(i => i.resource).Intersect(bloomsAndTeas), r.dish);
            if (r.method == KitchenMethod.Distill)
                Assert.IsTrue(r.inputs.Any(i => t.IsDrink(i.resource)), $"{r.dish} is distilled from something the cellar fermented");
        }
        Assert.IsTrue(t.IsDish("Peach Soup") && !t.IsDrink("Peach Soup"));
        Assert.AreEqual("Brew", cellar.First(r => r.method == KitchenMethod.Ferment).Verb);
        Assert.AreEqual("Distilled", cellar.First(r => r.method == KitchenMethod.Distill).Done);
        Assert.AreEqual("Cook", t.Recipe("peach-soup").Verb);
        // Every drink is a luxury of its own category, apart from the teas.
        var drinks = t.luxuries.Single(l => l.category == "Wines & Spirits");
        CollectionAssert.AreEquivalent(cellar.Select(r => r.dish), drinks.resources);
        CollectionAssert.IsEmpty(t.luxuries.Single(l => l.category == "Teas").resources.Intersect(drinks.resources));
        foreach (var r in cellar) Assert.IsNotNull(new CultureTuning().FamilyOfResource(r.dish), r.dish + " leans the culture");
    }

    [Test]
    public void TheCellarsDrinksAreRealBeverages()
    {
        GameCatalog.InvalidateAll();
        var life = new CultureLifeTuning();
        var pantry = UnityEngine.Resources.Load<PantrySettings>("Food/Pantry");
        foreach (var r in life.recipes.Where(r => r.InCellar))
        {
            Assert.IsTrue(GameCatalog.IsResource(r.dish), r.dish);
            Assert.AreEqual(FoodClass.Beverage, pantry.Kind(r.dish).cuisine, r.dish);
        }
        Assert.AreEqual(0f, pantry.Kind("Auric Peach Brandy").spoilPerSeventh, "a spirit keeps");
        Assert.IsTrue(pantry.Kind("Auric Peach Wine").peach, "a peach wine is still the peach");
        Assert.IsFalse(pantry.kinds.Any(k => k.cuisine == FoodClass.Beverage && !life.IsDrink(k.resource)), "every beverage has a cellar recipe");
    }

    // ===== HOLIDAYS =====

    [Test]
    public void AHolidayNeedsAFestivalHighMoraleAndUnity()
    {
        var t = new CultureLifeTuning();
        var none = new List<Holiday>();
        Assert.IsNotNull(CultureLifeRules.WhyNotHoliday(t, false, 1, 50, 999f, none, 999, 5, 1), "no culture");
        StringAssert.Contains("festival", CultureLifeRules.WhyNotHoliday(t, true, 0, 50, 999f, none, 999, 5, 1));
        StringAssert.Contains("Morale", CultureLifeRules.WhyNotHoliday(t, true, 1, t.holidayMoraleMargin - 1, 999f, none, 999, 5, 1));
        StringAssert.Contains("Unity", CultureLifeRules.WhyNotHoliday(t, true, 1, 50, t.holidayUnityCost - 1f, none, 999, 5, 1));
        Assert.IsNull(CultureLifeRules.WhyNotHoliday(t, true, 1, t.holidayMoraleMargin, t.holidayUnityCost, none, 999, 5, 1));
        var one = new List<Holiday> { new Holiday { seventh = 5, phase = 1 } };
        StringAssert.Contains("still new", CultureLifeRules.WhyNotHoliday(t, true, 1, 50, 999f, one, t.holidayCooldownSevenths - 1, 6, 1));
        StringAssert.Contains("already a holiday", CultureLifeRules.WhyNotHoliday(t, true, 1, 50, 999f, one, 999, 5, 1));
        Assert.IsNull(CultureLifeRules.WhyNotHoliday(t, true, 1, 50, 999f, one, 999, 6, 1));
        var full = Enumerable.Range(1, t.maxHolidays).Select(i => new Holiday { seventh = i, phase = 3 }).ToList();
        StringAssert.Contains("already keeps", CultureLifeRules.WhyNotHoliday(t, true, 1, 50, 999f, full, 999, 20, 1));
        Assert.AreEqual(t.holidayUnityCost + t.holidayUnityCostStep * 2, CultureLifeRules.HolidayCost(2, t), 1e-4f, "each holiday costs more");
    }

    // ===== CHARTERS =====

    private static readonly UnitSpec PerLegend = new UnitSpec
    {
        id = "expedition", name = "Expedition", role = UnitRole.Expedition, stamina = 6f, sight = 5, supplyCapacity = 12f, supplyUsePerSeventh = 1f,
        rewardMultiplier = 1f, wearMultiplier = 1f, workSevenths = 2f, plantSevenths = 1.5f, forageSevenths = 1f,
        abilities = UnitAbility.Survey | UnitAbility.SurveyMeso | UnitAbility.Forage | UnitAbility.AutoExplore | UnitAbility.Harvest | UnitAbility.Plant,
    };

    private static ExpeditionSettings Neutral() => new ExpeditionSettings { shapes = new List<PartyShape> { new PartyShape { name = "Neutral" } } };

    [Test]
    public void BuildersOnlyWorkTheLandAndWorkItFaster()
    {
        var x = Neutral();
        var explorers = Expeditions.Effective(PerLegend, new WorldUnit { id = 1, leader = "Vaelia" }, null, x, canImprove: true);
        var builders = Expeditions.Effective(PerLegend, new WorldUnit { id = 2, leader = "Vaelia", charter = ExpeditionCharter.Builders }, null, x, canImprove: true);
        Assert.IsTrue(WorldUnits.Can(explorers, UnitAbility.SurveyMeso), "an expedition explores");
        Assert.IsTrue(WorldUnits.Can(explorers, UnitAbility.Improve), "and improves once it knows how");
        Assert.AreEqual(UnitAbility.Improve | UnitAbility.Plant | UnitAbility.Forage, builders.abilities);
        Assert.AreEqual(explorers.workSevenths * x.builderWork, builders.workSevenths, 1e-4f);
        Assert.AreEqual(explorers.wearMultiplier * x.builderWear, builders.wearMultiplier, 1e-4f);
    }

    [Test]
    public void ACulturalPartyOnlyCelebrates()
    {
        var x = Neutral();
        var party = Expeditions.Effective(PerLegend, new WorldUnit { id = 3, leader = "Vaelia", charter = ExpeditionCharter.CulturalParty, settlers = 5 }, null, x, canImprove: true);
        Assert.AreEqual(UnitAbility.Celebrate, party.abilities, "no surveys, settling or improving");
        Assert.AreEqual(x.partyWear, party.wearMultiplier, 1e-4f);
        Assert.AreSame(UnitAbilities.Festival, UnitAbilities.For(UnitTask.Festival));
    }
}
