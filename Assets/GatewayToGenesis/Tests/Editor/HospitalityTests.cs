using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Hospitality and shared tables (T05) with no scene: the table's serving plan and checks (<see cref="TableServing"/>;
/// the spend itself is the culture's serving boundary), the policies' distinct consequences, coverage, the access
/// report, patron standing, recipe identity, the bounded living contribution and the table's contact between
/// neighbours (T02). Pure, so they also run outside Unity.
/// </summary>
public class HospitalityTests
{
    private static readonly HospitalityTuning Tuning = new HospitalityTuning();

    /// <summary>A pantry in memory: what is held and each kind.</summary>
    private sealed class Stores : IServingStores
    {
        public readonly Dictionary<string, float> held = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, FoodKind> kinds = new Dictionary<string, FoodKind>(StringComparer.OrdinalIgnoreCase);

        public Stores Add(string resource, float amount, float foodValue, FoodClass cuisine = FoodClass.Edible)
        {
            kinds[resource] = new FoodKind { resource = resource, foodValue = foodValue, cuisine = cuisine };
            held[resource] = amount;
            return this;
        }

        public float Held(string resource) => held.TryGetValue(resource, out float v) ? v : 0f;
        public FoodKind Kind(string resource) => resource != null && kinds.TryGetValue(resource, out var k) ? k : null;
        public float StoredValue => held.Where(h => kinds[h.Key].cuisine != FoodClass.Spice).Sum(h => h.Value * kinds[h.Key].foodValue);
    }

    private static Stores Cellar() => new Stores()
        .Add("Peach Soup", 10f, 1.5f).Add("Rootgrain Ale", 6f, 0.5f, FoodClass.Beverage).Add("Honeyed Peach Tart", 4f, 2f)
        .Add("Peach Pits", 20f, 0.2f, FoodClass.Ingredient).Add("Auric Saffron", 5f, 0f, FoodClass.Spice).Add("Earth-Beans", 40f, 0.5f);

    // ===== A TABLE'S SERVING =====

    [Test]
    public void APlan_NamesExactFoods_WithPortionsFromEachFoodsOwnValue()
    {
        var stores = Cellar();
        var plan = TableServing.Plan(new[] { "Peach Soup", "Rootgrain Ale" }, 6f, stores);
        Assert.IsNull(plan.invalid);
        Assert.AreEqual(2f, plan.items[0].amount, 1e-4f, "3 food value of soup at 1.5 each");
        Assert.AreEqual(6f, plan.items[1].amount, 1e-4f, "3 food value of ale at 0.5 each: portions follow each food's own value");
        Assert.IsNull(TableServing.WhyNot(plan, stores, 0f, false));
        var items = plan.Items();
        CollectionAssert.AreEqual(new[] { "Peach Soup", "Rootgrain Ale" }, items.Select(i => i.resource), "the boundary is handed exact resources, never generic food value");
        CollectionAssert.AreEqual(new[] { 2f, 6f }, items.Select(i => i.amount));
    }

    [Test]
    public void InsufficientStock_RefusesTheWholeTable()
    {
        var stores = Cellar();
        var plan = TableServing.Plan(new[] { "Peach Soup", "Honeyed Peach Tart" }, 20f, stores);
        string why = TableServing.WhyNot(plan, stores, 0f, false);
        StringAssert.Contains("Honeyed Peach Tart", why, "the boundary's own all-or-nothing check names what is short");
        StringAssert.DoesNotContain("Peach Soup (", why, "the soup is held in full");
        Assert.AreEqual(why, CultureServing.WhyNot(plan.Items(), stores.Held), "the table asks the same question the serving boundary asks at commit");
    }

    [Test]
    public void ASettledPlan_RecordsOnlyWhatLeftTheStores()
    {
        var stores = Cellar();
        var plan = TableServing.Plan(new[] { "Peach Soup", "Rootgrain Ale" }, 6f, stores);
        TableServing.Settle(plan, new[] { new ResourceAmount { resource = "Peach Soup", amount = 2f }, new ResourceAmount { resource = "Rootgrain Ale", amount = 5f } });
        Assert.AreEqual(5f, plan.items[1].amount, 1e-4f);
        Assert.AreEqual(2.5f, plan.items[1].foodValue, 1e-4f, "the record never claims more than was taken");
        Assert.AreEqual(5.5f, plan.FoodValue, 1e-4f);
    }

    [Test]
    public void RawIngredientsAndSpices_CannotBeServed_OnlyFinishedFoods()
    {
        var stores = Cellar();
        StringAssert.Contains("ingredient", TableServing.Plan(new[] { "Peach Pits" }, 2f, stores).invalid, "raw inputs go through the kitchen's recipe checks");
        StringAssert.Contains("spice", TableServing.Plan(new[] { "Auric Saffron" }, 2f, stores).invalid);
        StringAssert.Contains("not a food", TableServing.Plan(new[] { "Mystery Stew" }, 2f, stores).invalid, "an unknown food is refused, never invented");
        Assert.IsNotNull(TableServing.Plan(new[] { "Peach Soup", "Peach Soup" }, 2f, stores).invalid, "each food once");
        Assert.IsNotNull(TableServing.Plan(new[] { "Peach Soup", "Rootgrain Ale", "Earth-Beans", "Honeyed Peach Tart" }, 2f, stores).invalid, "at most three foods");
        Assert.IsNotNull(TableServing.Plan(new string[0], 2f, stores).invalid);
        Assert.IsNull(TableServing.Plan(new[] { "Rootgrain Ale" }, 2f, stores).invalid, "a drink from the cellar can be shared");
    }

    [Test]
    public void TheSurvivalReserve_AndHunger_ComeFirst()
    {
        var stores = Cellar();
        float value = stores.StoredValue;
        var plan = TableServing.Plan(new[] { "Earth-Beans" }, 6f, stores);
        StringAssert.Contains("survival", TableServing.WhyNot(plan, stores, value - 3f, false), "a table never draws the stores below the reserve");
        Assert.IsNull(TableServing.WhyNot(plan, stores, value - 6f, false));
        StringAssert.Contains("hungry", TableServing.WhyNot(plan, stores, 0f, true));
    }

    // ===== POLICIES AND COVERAGE =====

    private static TableRecord Table(TablePolicy policy, int settlement, int seventh, bool luxury = false, string patron = null) => new TableRecord
    {
        key = $"table-{settlement}-{seventh}", policy = policy, settlement = settlement, place = "Town " + settlement, seventh = seventh, patron = patron, foodValue = 6f,
        served = { new ServedLine { resource = luxury ? "Honeyed Peach Tart" : "Earth-Beans", amount = 3f, foodValue = 6f, luxuries = luxury ? new List<string> { "Sweets" } : new List<string>() } },
        luxuryShared = luxury && policy != TablePolicy.PatronHosted,
    };

    [Test]
    public void APublicMeal_AndAPatronsTable_HaveDistinctConsequences()
    {
        var open = HospitalityRules.Effects(TablePolicy.PublicWelcome, true, false, Tuning);
        var patron = HospitalityRules.Effects(TablePolicy.PatronHosted, true, false, Tuning);
        var recovery = HospitalityRules.Effects(TablePolicy.RecoverySupport, false, false, Tuning);
        Assert.IsTrue(open.reachesNeighbours && open.sharesLuxury && open.coverage >= 1f);
        Assert.IsFalse(patron.reachesNeighbours || patron.sharesLuxury, "the same dish at a patron's table stays with their guests");
        Assert.IsTrue(patron.rewardsPatron);
        Assert.Less(patron.coverage, open.coverage);
        Assert.Greater(patron.unity, open.unity, "the patron's prestige pays in Unity, not in reach");
        Assert.Greater(recovery.relief, open.relief, "recovery support eases strain most");
        Assert.Greater(HospitalityRules.Effects(TablePolicy.PublicWelcome, false, true, Tuning).unity, HospitalityRules.Effects(TablePolicy.PublicWelcome, false, false, Tuning).unity,
            "the Feast of Abundance makes redistribution legitimacy");
    }

    [Test]
    public void Coverage_IsPerSettlement_BoundedAndExpiring()
    {
        var s = HospitalityRules.Ensure(null);
        var standing = new List<int> { 1, 2, 3, 4 };
        for (int i = 0; i < 5; i++) HospitalityRules.Record(s, Table(TablePolicy.PublicWelcome, 1, 10 + i), false, Tuning);
        Assert.AreEqual(0.25f, HospitalityRules.Coverage(s, standing, 15, Tuning), 1e-4f, "five tables in one town reach one town: no farming");
        HospitalityRules.Record(s, Table(TablePolicy.PatronHosted, 2, 15, patron: "Vaelia"), true, Tuning);
        Assert.AreEqual((1f + Tuning.patronCoverage) / 4f, HospitalityRules.Coverage(s, standing, 16, Tuning), 1e-4f, "a patron's table reaches few");
        Assert.AreEqual(0f, HospitalityRules.Coverage(s, standing, 15 + Tuning.coverageWindowSevenths + 1, Tuning), "it fades when no table is set");
        Assert.AreEqual(Tuning.coverageLiving, HospitalityRules.LivingFrom(1f, Tuning), 1e-4f);
        Assert.AreEqual(Tuning.coverageLiving, HospitalityRules.LivingFrom(5f, Tuning), 1e-4f, "the living contribution is bounded");
    }

    [Test]
    public void TheLivingContribution_IsSeparateFromLuxury_AndChangesNothingWithoutTables()
    {
        var x = new WellbeingInputs { population = 2000, luxury = 0.5f, joy = 0.2f };
        float without = CultureLifeRules.Living(x);
        x.sharedTables = HospitalityRules.LivingFrom(1f, Tuning);
        Assert.AreEqual(without + Tuning.coverageLiving, CultureLifeRules.Living(x), 1e-4f, "a place at the table counts without any luxury demand");
        x.luxury = 0f;
        x.sharedTables = 0f;
        Assert.AreEqual(0.3f * 0.2f, CultureLifeRules.Living(x), 1e-4f, "no tables: living as before");
    }

    [Test]
    public void ACooldown_AndAPatronsRest_StopRepeats()
    {
        var s = HospitalityRules.Ensure(null);
        HospitalityRules.Record(s, Table(TablePolicy.PatronHosted, 1, 10, patron: "Vaelia"), true, Tuning);
        Assert.AreEqual(Tuning.cooldownSevenths - 2, HospitalityRules.Wait(s, 1, 12, Tuning));
        Assert.AreEqual(0, HospitalityRules.Wait(s, 2, 12, Tuning), "another town may set its own");
        Assert.IsFalse(HospitalityRules.PatronRewardDue(s, "Vaelia", 12, Tuning), "standing is earned once in a while, not per table");
        Assert.IsTrue(HospitalityRules.PatronRewardDue(s, "Vaelia", 10 + Tuning.patronRestSevenths, Tuning));
        Assert.IsTrue(HospitalityRules.PatronRewardDue(s, "Oren", 12, Tuning));
    }

    // ===== ACCESS =====

    [Test]
    public void LuxuryMonopolization_IsAVisibleAccessProblem()
    {
        var s = HospitalityRules.Ensure(null);
        var towns = new List<(int, string)> { (1, "Ashford"), (2, "Riverford") };
        var held = new List<(string, float)> { ("Honeyed Peach Tart", 12f) };
        var hoard = HospitalityRules.Access(s, towns, 5, held, Tuning);
        CollectionAssert.AreEqual(new[] { "12 Honeyed Peach Tart" }, hoard.hoarded, "held, never shared");
        StringAssert.StartsWith("Held but not shared", hoard.problems[0]);

        HospitalityRules.Record(s, Table(TablePolicy.PatronHosted, 1, 6, luxury: true, patron: "Vaelia"), true, Tuning);
        var patron = HospitalityRules.Access(s, towns, 7, held, Tuning);
        CollectionAssert.AreEqual(new[] { "Sweets" }, patron.rows[0].privateOnly);
        Assert.IsTrue(patron.problems.Any(p => p.Contains("reached only a patron's guests")));
        Assert.IsNotEmpty(patron.hoarded, "a patron's table is not an open sharing");

        HospitalityRules.Record(s, Table(TablePolicy.PublicWelcome, 1, 8, luxury: true), false, Tuning);
        var open = HospitalityRules.Access(s, towns, 9, held, Tuning);
        Assert.IsEmpty(open.hoarded);
        CollectionAssert.AreEqual(new[] { "Sweets" }, open.rows[0].shared);
        Assert.IsTrue(open.problems.Any(p => p.Contains("not in Riverford")), "shared in one town and not the other: named, not averaged away");
        Assert.AreEqual(0.5f, open.coverage, 1e-4f);
    }

    [Test]
    public void APoorSettlement_TakesPartWithPlainFood_WithoutPretendingToLuxury()
    {
        var s = HospitalityRules.Ensure(null);
        HospitalityRules.Record(s, Table(TablePolicy.PublicWelcome, 2, 4, luxury: false), false, Tuning);
        var report = HospitalityRules.Access(s, new List<(int, string)> { (2, "Riverford") }, 5, new List<(string, float)>(), Tuning);
        Assert.AreEqual(1f, report.rows[0].reach, "a plain shared pot is a full place at the table");
        Assert.IsEmpty(report.rows[0].shared, "but no luxury is claimed for it");
    }

    // ===== IDENTITY, HISTORY, SAVES =====

    [Test]
    public void AServedRecipe_KeepsItsIdentity_ThroughARename()
    {
        var line = new ServedLine { resource = "Grandmother's Stew", recipe = "invented-3", amount = 2f };
        Assert.AreEqual("Hearth Stew", HospitalityRules.ServedName(line, id => id == "invented-3" ? "Hearth Stew" : null), "the recipe id finds its new name");
        Assert.AreEqual("Grandmother's Stew", HospitalityRules.ServedName(line, id => null), "a recipe no longer known shows the name it was served under");
        Assert.AreEqual("Wild Honey", HospitalityRules.ServedName(new ServedLine { resource = "Wild Honey" }, id => "wrong"), "a food with no recipe is its resource");
    }

    [Test]
    public void History_StaysBounded_AndTheSummariesKeepTheTotals()
    {
        var s = HospitalityRules.Ensure(null);
        var tuning = new HospitalityTuning { tablesKept = 3 };
        for (int i = 0; i < 10; i++) HospitalityRules.Record(s, Table(TablePolicy.PublicWelcome, 1, i * 7), false, tuning);
        Assert.AreEqual(3, s.tables.Count);
        Assert.AreEqual(10, HospitalityRules.Summary(s, 1).tables);
        Assert.AreEqual(63, HospitalityRules.Summary(s, 1).lastShared);
    }

    [Test]
    public void HospitalityState_SurvivesTheSaveCodec_AndAnOlderEnvelopeLoads()
    {
        var s = HospitalityRules.Ensure(null);
        HospitalityRules.Record(s, Table(TablePolicy.PatronHosted, 1, 6, luxury: true, patron: "Vaelia"), true, Tuning);
        s.tables[0].served[0].recipe = "honeyed-porridge";
        s.serial = 1;
        var back = (HospitalityState)SaveStateCodec.Read(SaveStateCodec.Write(s, typeof(HospitalityState)), typeof(HospitalityState));
        Assert.AreEqual(TablePolicy.PatronHosted, back.tables.Single().policy);
        Assert.AreEqual("honeyed-porridge", back.tables.Single().served.Single().recipe);
        CollectionAssert.AreEqual(new[] { "Sweets" }, back.tables.Single().served.Single().luxuries);
        Assert.AreEqual(6, HospitalityRules.Patron(back, "Vaelia").lastRewarded);
        Assert.AreEqual(1, back.serial);
        var envelope = SaveStateCodec.Write(new CultureExtensionState(), typeof(CultureExtensionState));
        envelope.children.RemoveAll(c => c.name == "hospitality");
        var older = (CultureExtensionState)SaveStateCodec.Read(envelope, typeof(CultureExtensionState));
        Assert.IsNotNull(older.hospitality);
        Assert.IsEmpty(older.hospitality.tables, "nothing is invented for an older save");
    }

    // ===== CONTACT AT AN OPEN TABLE (T02) =====

    [Test]
    public void AnOpenTable_LetsNeighboursMeetEachOthersCustoms_Once()
    {
        var local = LocalCultureRules.Ensure(new LocalCultureState());
        var lt = new LocalCultureTuning();
        LocalCultureRules.Gather(local, 1, "Riverford", "crossing-songs", " river", 1, null, 0, lt);
        LocalCultureRules.Gather(local, 2, "Goldbough", "golden-fruit", " orchard", 1, null, 0, lt);
        var changes = LocalCultureRules.Table(local, "table-1", 1, "Riverford", new[] { (2, "Goldbough") }, Tuning.tableExposure, 2, null, 0, lt);
        Assert.AreEqual(2, changes.Count, "each side meets the other's custom");
        var met = LocalCultureRules.Practice(LocalCultureRules.Profile(local, 2), "crossing-songs");
        Assert.AreEqual(PracticeChannel.Table, met.origin.channel);
        Assert.AreEqual(LocalPracticeStage.Exposed, met.stage, "met at a table is not kept");
        StringAssert.Contains("the shared table in Riverford", LocalCultureRules.ProvenanceWords(met.origin));
        Assert.IsEmpty(LocalCultureRules.Table(local, "table-1", 1, "Riverford", new[] { (2, "Goldbough") }, Tuning.tableExposure, 2, null, 0, lt), "the same table counts once");
        Assert.IsEmpty(LocalCultureRules.Table(local, "table-2", 1, "Riverford", new (int, string)[0], Tuning.tableExposure, 3, null, 0, lt), "no guests by road, no exchange");
    }
}
