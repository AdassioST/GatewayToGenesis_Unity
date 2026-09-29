using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Inheritance and blends in the real scene (T07, CultureSystem.Syncretism.cs): a fallen town's records justify only
/// their own way of living; its ways are digested once; its civic replaces one of yours (whose effects go); a tradition
/// kept there before the fall is taken up again; two traditions kept together are blended in their place (the parents
/// fall quiet and live on in it); everything survives two reloads and is carried in the next Age's lineage; renewal
/// waits for an Age to pass; a ruin that is gone, or never was, is inspected without a crash.
/// </summary>
public class SyncretismPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);

    private static CultureState State(CultureSystem culture) => (CultureState)typeof(CultureSystem).GetField("_state", Any).GetValue(culture);

    private static void Reload(CultureSystem culture)
    {
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", Any).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        culture.AfterRestore();
    }

    private static void PassAge(string ageId, string ageTitle, string crisis, string nextId, string nextTitle)
    {
        var ages = AgeProgression.Instance;
        var history = (List<AgeRecord>)typeof(AgeProgression).GetField("_history", Any).GetValue(ages);
        var record = new AgeRecord { ageId = ageId, ageTitle = ageTitle, crisisTitle = crisis, nextAgeId = nextId, nextAgeTitle = nextTitle, populationBefore = 100, deaths = 5 };
        history.Add(record);
        ((Action<AgeRecord>)typeof(AgeProgression).GetField("AgePassed", Any).GetValue(ages))?.Invoke(record);
    }

    // A town a few cells from the Capital (as CultureMemoryPlayTests founds one).
    private static Settlement FoundTown()
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        foreach (var kind in new[] { SettlementKind.Town, SettlementKind.Outpost })
            foreach (var t in map.Tiles.Where(t => !t.water && HexCoord.Distance(t.coord, map.Capital) >= 2 && HexCoord.Distance(t.coord, map.Capital) <= 8).OrderBy(t => HexCoord.Distance(t.coord, map.Capital)).ThenBy(t => t.index))
            {
                bool explored = t.explored, known = t.known;
                t.explored = t.known = true;
                if (WorldCivilization.WhyNotFound(map, world.Settings.generation, world.Rules, t.coord, kind) == null)
                    return WorldCivilization.Found(map, world.Settings.generation, world.Rules, t.coord, kind, GameAge.Number);
                t.explored = explored;
                t.known = known;
            }
        return null;
    }

    // A civic the ruin's people lived by, and one of yours in the same tier to replace: both with a known family, free of conflicts and requirements.
    private static (CivicData theirs, CivicData yours) CivicPair(CultureSystem culture)
    {
        var civics = CivicManager.Instance;
        bool Plain(CivicData c) => c != null && !civics.IsCivicActive(c.civicName) && (c.requiredCivics == null || c.requiredCivics.Count == 0) && (c.conflictingCivics == null || c.conflictingCivics.Count == 0);
        foreach (var yours in GameCatalog.Civics.All.Where(c => Plain(c) && c.effects != null && c.effects.Count > 0 && civics.GetAvailableSlots(c.tier) > 0).OrderBy(c => c.civicName, StringComparer.Ordinal))
        {
            var theirs = GameCatalog.Civics.All.Where(c => Plain(c) && c != yours && c.tier == yours.tier && culture.Tuning.FamilyOfCivic(c.civicName) != null)
                .OrderBy(c => c.civicName, StringComparer.Ordinal).FirstOrDefault();
            if (theirs != null) return (theirs, yours);
        }
        return (null, null);
    }

    [UnityTest]
    public IEnumerator AFallenPeoplesRecordsDecideWhatIsInherited_AndBlendsAreEarned()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 900 && (CultureSystem.Instance == null || GameUnitsLogic.Instance == null || WorldSystem.Instance == null || WorldSystem.Instance.Map == null
                                    || AgeProgression.Instance == null || CivicManager.Instance == null); i++) yield return null;
        var menu = UnityEngine.Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Any).SetValue(menu, false);
        Time.timeScale = 1;
        for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;) yield return null;
        if (TimeSystemLogic.Instance != null) TimeSystemLogic.Instance.isTimePaused = true;
        Scenario();

        // The Heritage panel builds over it (any exception fails the test).
        CultureWindow.Open();
        yield return null;
        var window = UnityEngine.Object.FindAnyObjectByType<CultureWindow>();
        var mode = typeof(CultureWindow).GetNestedType("LifeMode", BindingFlags.NonPublic);
        typeof(CultureWindow).GetMethod("SetLife", Any).Invoke(window, new[] { Enum.Parse(mode, "Heritage") });
        yield return null;
        CultureWindow.Hide();
        yield return new ExitPlayMode();
    }

    private static void Scenario()
    {
        var culture = CultureSystem.Instance;
        var world = WorldSystem.Instance;
        Assert.IsTrue(culture.ApplyConsequence("myth hearth", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        culture.Tick();

        // A town keeps Tales at the Hearth, then falls: the tradition is remembered in its ruins.
        var town = FoundTown();
        Assert.IsNotNull(town, "somewhere to found a town");
        var traditions = culture.Extensions.traditions;
        var tales = TraditionRules.Create(traditions, culture.TraditionTuning.Definition("hearth-tales"), town.id, null, new TraditionOrigin { text = "fixture" }, State(culture).sevenths);
        tales.stage = TraditionStage.Practiced;
        TraditionRules.Mark(tales, TraditionRules.PracticedMilestone);
        Assert.IsTrue(world.Pillage(town, 1000f, "in a test"), "the town falls");
        var ruin = world.Map.Ruins.Last(r => r.name == town.name);
        if (!tales.fallen) TraditionRules.SettlementFell(traditions, town.id, town.name, ruin.id);
        Assert.IsTrue(tales.fallen);

        // Before investigation nothing of its people is known.
        StringAssert.Contains("Investigate", culture.WhyNotReformToward(EnclaveFamily.Militant, ruin.id));
        var (theirs, yours) = CivicPair(culture);
        Assert.IsNotNull(theirs, "two civics of one tier to exchange");
        ruin.investigated = true;
        ruin.civic = theirs.civicName;
        ruin.binding = "Crystal";
        var family = culture.Tuning.FamilyOfCivic(theirs.civicName).Value;
        var unrelated = CultureRules.Families.First(f => f != family && f != EnclaveFamily.Auric);

        // Its records justify only their own ways.
        var h = culture.InspectRuin(ruin.id);
        CollectionAssert.Contains(h.families, family.ToString());
        CollectionAssert.Contains(h.families, "Auric", "the tradition that fell with it speaks too");
        CollectionAssert.DoesNotContain(h.families, unrelated.ToString());
        Assert.IsFalse(culture.Reform(unrelated, ruin.id), "an unrelated history justifies nothing");
        StringAssert.Contains("Nothing in the records", culture.WhyNotReformToward(unrelated, ruin.id));
        CollectionAssert.DoesNotContain(State(culture).digestedRuins, ruin.id, "a refused reform digests nothing");
        int reforms = culture.Reforms;
        Assert.IsTrue(culture.Reform(family, ruin.id), culture.WhyNotReformToward(family, ruin.id));
        Assert.AreEqual(reforms + 1, culture.Reforms);
        CollectionAssert.Contains(State(culture).digestedRuins, ruin.id);
        var adapt = culture.SyncretismDecisions().Last();
        Assert.AreEqual(InheritanceChoice.AdaptWays, adapt.choice);
        CollectionAssert.Contains(adapt.evidence, $"ruin:{ruin.id}:civic", "the record that justified it");
        Assert.IsNotEmpty(adapt.lost);
        State(culture).seventhsSinceReform = 999;
        Assert.IsFalse(culture.Reform(family, ruin.id), "a ruin feeds one reform");
        StringAssert.Contains("digested already", culture.WhyNotReformToward(family, ruin.id));

        // Its civic replaces one of yours: yours and its effects go; the ruin's one adoption is spent (the same ledger).
        Assert.IsTrue(CivicManager.Instance.UnlockCivic(yours.civicName, "test", inherited: true));
        bool ledgered = EffectRouter.HasSource(CivicManager.SourceName(yours));
        Debug.Log($"Replacing {yours.civicName} ({(ledgered ? "its effects in a ledger" : "no ledger effects")}) with {theirs.civicName}.");
        var swap = new InheritanceRequest { ruin = ruin.id, choice = InheritanceChoice.ReplaceCivic, replacing = yours.civicName };
        var preview = culture.PreviewInherit(swap);
        Assert.IsTrue(preview.succeeded, preview.reason);
        StringAssert.Contains(yours.civicName, preview.reason);
        Assert.IsTrue(culture.Inherit(swap).succeeded);
        Assert.IsFalse(CivicManager.Instance.IsCivicActive(yours.civicName));
        Assert.IsFalse(EffectRouter.HasSource(CivicManager.SourceName(yours)), "the replaced civic's effects are removed");
        Assert.IsTrue(CivicManager.Instance.IsCivicActive(theirs.civicName));
        Assert.IsTrue(ruin.civicAdopted);
        StringAssert.Contains("adopted from these ruins already", culture.PreviewInherit(new InheritanceRequest { ruin = ruin.id, choice = InheritanceChoice.PreserveCivic }).reason);

        // What its people kept is taken up again, once.
        var clue = culture.InspectRuin(ruin.id).clues.Single(c => c.kind == RuinClueKind.Tradition);
        int capital = WorldCivilization.Capital(world.Map).id;
        var revive = new InheritanceRequest { ruin = ruin.id, choice = InheritanceChoice.Revive, evidence = clue.evidence, settlement = capital };
        Assert.IsTrue(culture.Inherit(revive).succeeded, culture.PreviewInherit(revive).reason);
        var revived = traditions.instances.Single(i => i.definition == "hearth-tales" && i.settlement == capital && !i.fallen);
        Assert.IsTrue(revived.HasMilestone(SyncretismRules.InheritedMilestone));
        Assert.IsTrue(revived.links.Any(l => l.kind == CultureEntityKind.Ruin && l.id == ruin.id.ToString()));
        Assert.IsFalse(culture.Inherit(revive).succeeded, "once");

        // Two traditions kept together by the nation: the Loaf of Names, once its bread can be baked.
        var ash = TraditionRules.Create(traditions, culture.TraditionTuning.Definition("ash-loaf-table"), -1, null, new TraditionOrigin { text = "fixture" }, State(culture).sevenths);
        var names = TraditionRules.Create(traditions, culture.TraditionTuning.Definition("naming-of-the-lost"), -1, null, new TraditionOrigin { text = "fixture" }, State(culture).sevenths);
        foreach (var t in new[] { ash, names }) { t.stage = TraditionStage.Practiced; t.momentum = 1f; TraditionRules.Mark(t, TraditionRules.PracticedMilestone); }
        Assert.IsFalse(culture.HybridOffers().Any(o => o.rule.id == "loaf-of-names"), "not before Echoes of Hunger");
        StringAssert.Contains("Echoes of Hunger", culture.PreviewHybrid("loaf-of-names", -1, SyncretismMode.Adapt).reason);
        GameUnitsLogic.Instance.GetTechnologySlot("Echoes of Hunger").UnlockTechnology();
        var offer = culture.HybridOffers().Single(o => o.rule.id == "loaf-of-names");
        Assert.AreEqual(-1, offer.settlement);
        Assert.IsTrue(culture.PreviewHybrid("loaf-of-names", -1, SyncretismMode.SideBySide).succeeded, "keeping them apart is always possible");
        Give(culture.UnityResource, 50f);
        float unity = culture.UnityHeld;
        var replace = culture.PreviewHybrid("loaf-of-names", -1, SyncretismMode.Replace);
        Assert.IsTrue(replace.succeeded, replace.reason);
        StringAssert.Contains("Lets go of", replace.reason);
        Assert.IsTrue(culture.Hybridize("loaf-of-names", -1, SyncretismMode.Replace).succeeded);
        Assert.AreEqual(unity - culture.SyncretismTuning.Hybrid("loaf-of-names").replaceUnity, culture.UnityHeld, 1e-3f, "paid once");
        var loaf = traditions.instances.Single(i => i.definition == "loaf-of-names");
        Assert.IsTrue(loaf.Lived, "it took their place: a custom at once");
        Assert.AreEqual(loaf.id, ash.mergedInto);
        Assert.AreEqual(TraditionStage.Dormant, names.stage);
        Assert.AreEqual(1, culture.VariantOf(loaf.id).depth);
        Assert.IsFalse(culture.Hybridize("loaf-of-names", -1, SyncretismMode.Adapt).succeeded, "decided once");

        // A batch of Ash-Loaf feeds the blend, not the parent living on in it.
        int loafBefore = loaf.participations, ashBefore = ash.participations;
        Assert.IsTrue(culture.Record(new CulturalOccurrence
        {
            key = "test:ash-loaf:1", kind = CulturalOccurrenceKind.Production, subject = CultureEntityRef.Of(CultureEntityKind.Recipe, "ash-loaf", "Ash-Loaf"),
            settlement = -1, quantity = 1f, unit = CultureQuantityUnit.Batches,
        }));
        culture.Tick();
        loaf = traditions.instances.Single(i => i.definition == "loaf-of-names");
        Assert.AreEqual(loafBefore + 1, loaf.participations);
        Assert.AreEqual(ashBefore, traditions.instances.Single(i => i.id == ash.id).participations);

        // Two reloads: nothing duplicated, the blend's benefit re-applied.
        int decisions = culture.SyncretismDecisions().Count;
        Reload(culture);
        Reload(culture);
        traditions = culture.Extensions.traditions;
        Assert.AreEqual(decisions, culture.SyncretismDecisions().Count);
        Assert.AreEqual(2, culture.Variants().Count, "the blend and the tradition taken up");
        Assert.AreEqual(1, traditions.instances.Count(i => i.definition == "loaf-of-names"));
        Assert.IsTrue(culture.Tradition(loaf.id).benefitActive, "its benefit applies after the load");
        CollectionAssert.Contains(State(culture).digestedRuins, ruin.id);

        // Renewal waits for an Age to pass; the Age's lineage carries the inherited and the blended.
        StringAssert.Contains("No Age has passed", culture.WhyNotRenew(loaf.id));
        PassAge("age-of-desolation", "Age of Desolation", "The Inescapable Hunger", "age-of-renewal", "Age of Renewal");
        var lineage = culture.MemoryLineages().Last();
        Assert.IsTrue(lineage.practices.Any(p => p.kind == CultureEntityKind.Tradition && p.id == loaf.id), "the blend goes on into the next Age");
        Assert.IsTrue(lineage.practices.Any(p => p.kind == CultureEntityKind.Tradition && p.id == revived.id), "and what was taken up from the ruins");
        Assert.IsNull(culture.WhyNotRenew(loaf.id), culture.WhyNotRenew(loaf.id));
        Assert.IsTrue(culture.Renew(loaf.id).succeeded);
        Assert.AreEqual(2, culture.VariantOf(loaf.id).depth);
        StringAssert.Contains("No Age has passed since it was last renewed", culture.WhyNotRenew(loaf.id));

        // Missing data: a ruin that never was, and one gone from the map, are read without a crash.
        var none = culture.InspectRuin(987654);
        Assert.IsNull(none.record);
        Assert.IsFalse(culture.PreviewInherit(new InheritanceRequest { ruin = 987654, choice = InheritanceChoice.AdaptWays, family = "Weaver" }).succeeded);
        world.Map.Ruins.Remove(ruin);
        try
        {
            var gone = culture.InspectRuin(ruin.id);
            Assert.IsNotNull(gone.record, "known through what fell with it");
            Assert.IsFalse(gone.record.onMap);
            Assert.IsTrue(gone.lost.Any(l => l.Contains("gone from the map")));
            Assert.IsNotNull(culture.RuinHeritages());
        }
        finally
        {
            world.Map.Ruins.Add(ruin);
        }
    }
}
