using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Shared tables in the real scene (T05, CultureSystem.Hospitality.cs): a public welcome in the Capital takes exactly the
/// soup it planned, records its use once and reports nothing as eaten, commits one Hospitality occurrence with the
/// recipe's id, and eases the Capital's strain; a repeat waits; a table the stores cannot hold fails with nothing paid
/// or granted; raw ingredients are refused; a patron's luxury stays private in the access report; recovery support
/// needs strain and serves an invented dish by its recipe id; all of it survives a save of the culture.
/// </summary>
public class HospitalityPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private static int _eaten;

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);
    private static float Held(string resource) => GameUnitsLogic.Instance.GetResourceAmountExact(resource);
    private static void OnEaten(string resource, float amount) => _eaten++;

    [UnityTest]
    public IEnumerator ATableServesExactlyOnce_AndItsPolicyDecidesWhoHasAPlace()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null || Pantry.Instance == null || PopGrowthLogic.Instance == null); i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        Time.timeScale = 1;
        for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;) yield return null;
        Scenario();
        yield return new ExitPlayMode();
    }

    // The whole scenario in a plain method: its lambdas capture locals, which EnterPlayMode's domain reload would lose.
    private static void Scenario()
    {
        var culture = CultureSystem.Instance;
        var map = WorldSystem.Instance.Map;
        var capital = WorldCivilization.Capital(map);
        var state = (CultureState)typeof(CultureSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(culture);
        var used = (Dictionary<string, float>)typeof(CultureSystem).GetField("_usedThisSeventh", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(culture);
        ICultureQuery query = culture;
        Assert.IsFalse(culture.PreviewTable(new TableRequest { settlement = capital.id, menu = { "Peach Soup" } }).ok, "no tables before the founding");
        Assert.IsTrue(culture.ApplyConsequence("myth song", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        PopGrowthLogic.Instance.isFoodScarce = false;
        Give("Deep-Rooted Grain", 40f);
        Give("Peach Soup", 20f);
        capital.strain = 30f;

        // A public welcome in the Capital: exact soup, its use heard once, nothing reported as eaten.
        var request = new TableRequest { settlement = capital.id, policy = TablePolicy.PublicWelcome, menu = { "Peach Soup" } };
        var preview = culture.PreviewTable(request);
        Assert.IsTrue(preview.ok, preview.reason);
        float portions = preview.portions.Single().amount;
        Assert.AreEqual(HospitalityRules.TableSize(TablePolicy.PublicWelcome, capital.development, culture.HospitalityTuning) / 2f, portions, 1e-3f, "Peach Soup is worth 2 food value");
        Assert.IsTrue(preview.groups.Any(g => g.Contains("the people of")));
        float soup = Held("Peach Soup"), unity = culture.UnityHeld, strain = capital.strain;
        used.TryGetValue("Peach Soup", out float usedBefore);
        _eaten = 0;
        Pantry.Eaten += OnEaten;
        var set = culture.SetTable(request);
        Pantry.Eaten -= OnEaten;
        Assert.IsTrue(set.succeeded, set.reason);
        Assert.AreEqual(soup - portions, Held("Peach Soup"), 1e-3f, "exactly the planned portions");
        Assert.AreEqual(usedBefore + portions, used["Peach Soup"], 1e-3f, "its use is recorded once, for the foodways");
        Assert.AreEqual(0, _eaten, "served portions are never also a meal against hunger");
        Assert.Greater(culture.UnityHeld, unity);
        Assert.AreEqual(strain - culture.HospitalityTuning.publicRelief, capital.strain, 1e-3f);
        var o = query.PendingOccurrences().Single(x => x.key == set.occurrences.Single());
        Assert.AreEqual(CulturalOccurrenceKind.Hospitality, o.kind);
        Assert.AreEqual("peach-soup", o.recipe.id, "the recipe by id, not by name");
        Assert.AreEqual(capital.id, o.settlement);
        Assert.AreEqual(1, query.RecentTables().Count);
        Assert.AreEqual(1f / Mathf.Max(1, map.Settlements.Count), query.SharedTableCoverage(), 1e-3f, "the Capital is reached; the rest are not");

        // The same place waits; a table the stores cannot hold fails with nothing paid or granted.
        StringAssert.Contains("not long ago", culture.SetTable(request).reason);
        state.sevenths += culture.HospitalityTuning.cooldownSevenths;
        Give("Peach Soup", -(Held("Peach Soup") - 0.5f));
        soup = Held("Peach Soup");
        unity = culture.UnityHeld;
        strain = capital.strain;
        var poor = culture.SetTable(request);
        Assert.IsFalse(poor.succeeded);
        StringAssert.Contains("lack", poor.reason);
        Assert.AreEqual(soup, Held("Peach Soup"), 1e-4f);
        Assert.AreEqual(unity, culture.UnityHeld, 1e-4f, "no partial reward");
        Assert.AreEqual(strain, capital.strain, 1e-4f);
        Assert.AreEqual(1, query.RecentTables().Count);

        // Raw ingredients go through the kitchen, never straight to the table.
        StringAssert.Contains("ingredient", culture.PreviewTable(new TableRequest { settlement = capital.id, menu = { "Peach Pits" } }).reason);

        // A patron's Sweets stay with their guests: the access report says so.
        var patrons = culture.PatronChoices();
        if (patrons.Count > 0)
        {
            Give("Honeyed Grain Porridge", 10f);
            var hosted = culture.SetTable(new TableRequest { settlement = capital.id, policy = TablePolicy.PatronHosted, patron = patrons[0], menu = { "Honeyed Grain Porridge" } });
            Assert.IsTrue(hosted.succeeded, hosted.reason);
            var row = query.TableAccess().rows.Single(r => r.settlement == capital.id);
            CollectionAssert.Contains(row.privateOnly, "Sweets");
            Assert.IsTrue(query.TableAccess().problems.Any(p => p.Contains("patron")));
        }

        // Recovery support needs a community under strain; then it serves a dish the people invented, by its recipe id.
        state.sevenths += culture.HospitalityTuning.cooldownSevenths;
        GameUnitsLogic.Instance.GetTechnologySlot("Echoes of Hunger").UnlockTechnology();
        Give("Ash-Bread", 10f);
        Give("Earth-Beans", 10f);
        var inputs = new List<ResourceAmount> { new ResourceAmount { resource = "Ash-Bread", amount = 3f }, new ResourceAmount { resource = "Earth-Beans", amount = 1f } };
        var own = culture.Invent("Hearth Crumble", KitchenMethod.Cook, inputs);
        Assert.IsNotNull(own, culture.WhyNotInvent("Hearth Crumble", KitchenMethod.Cook, inputs));
        Give(own.name, 10f);
        var food = query.ServableFoods().Single(f => f.resource == own.name);
        Assert.IsTrue(food.invented, "the people's own dish is offered like any other");
        var recovery = new TableRequest { settlement = capital.id, policy = TablePolicy.RecoverySupport, menu = { own.name } };
        capital.strain = 0f;
        StringAssert.Contains("not under strain", culture.PreviewTable(recovery).reason);
        capital.strain = 40f;
        var eased = culture.SetTable(recovery);
        Assert.IsTrue(eased.succeeded, eased.reason);
        Assert.AreEqual(40f - culture.HospitalityTuning.recoveryRelief, capital.strain, 1e-3f, "recovery support eases strain most");
        Assert.AreEqual(own.id, query.PendingOccurrences().Single(x => x.key == eased.occurrences.Single()).recipe.id, "the invented recipe by id");

        // Tables survive a save of the culture.
        int tables = query.RecentTables().Count;
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        culture.AfterRestore();
        Assert.AreEqual(tables, query.RecentTables().Count);
        Assert.AreEqual("Peach Soup", query.RecentTables().Last().served.Single().name);
        Assert.AreEqual(own.name, query.RecentTables().First().served.Single().name, "the invented dish is found again by its recipe after the load");
        StringAssert.Contains("not long ago", culture.PreviewTable(request).reason, "the cooldown is saved: a reload does not reopen the table");
    }
}
