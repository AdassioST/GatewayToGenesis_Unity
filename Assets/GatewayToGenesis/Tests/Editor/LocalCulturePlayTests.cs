using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Local cultures in the real scene (T02, CultureSystem.Local.cs, WorldSystem.CulturalContact.cs): the Capital begins a
/// custom with a local gathering; a hamlet raised on its outskirts remembers it through its founders and is in contact
/// by its road; a cultural party carries the Capital's custom and completes a visit once; people admitted at the gates
/// keep exactly the origin the population reports (unknown stays unknown); no land changes hands; all of it survives a
/// save of the culture.
/// </summary>
public class LocalCulturePlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";

    private static void Give(string resource, float amount) => GameUnitsLogic.Instance.ChangeResourceFromName(resource, amount, false);

    // A seed world may hold only the Capital's own ring at the start: take held (else wild) ground within the hub's reach
    // and hold it, so the fixture never depends on how far the starting territory reaches.
    private static WorldTile HamletSite(WorldMap map, Settlement capital)
    {
        int reach = System.Math.Max(2, WorldTributaries.RulesOf(map).hubReach);
        var site = map.Tiles.Where(t => (WorldAuthority.IsPlayers(t.authorityId) || t.authorityId == WorldAuthority.Wilderness) && !t.water && !t.impassable
                && t.settlement < 0 && t.enclave < 0 && HexCoord.Distance(t.coord, capital.coord) >= 2 && HexCoord.Distance(t.coord, capital.coord) <= reach)
            .OrderBy(t => WorldAuthority.IsPlayers(t.authorityId) ? 0 : 1).ThenBy(t => HexCoord.Distance(t.coord, capital.coord)).ThenBy(t => t.index).First();
        site.authorityId = WorldAuthority.Player;
        return site;
    }

    // Pin the hamlet's road open: no threat's source on it and no cell of it held by another authority (a random seed may put one there).
    private static void ClearRoad(WorldMap map, Settlement hamlet)
    {
        foreach (var road in map.Routes.Where(r => r != null && (r.from == hamlet.id || r.to == hamlet.id)))
            foreach (var c in road.cells)
            {
                var t = map.Get(c);
                if (t == null) continue;
                map.Threats.RemoveAll(th => th.cell == t.index);
                if (t.authorityId != WorldAuthority.Wilderness && !WorldAuthority.IsPlayers(t.authorityId)) t.authorityId = WorldAuthority.Wilderness;
            }
    }

    [UnityTest]
    public IEnumerator ACustomBeginsTravelsByRoadAndPartyAndArrivalsKeepTheirOrigins()
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

    // The whole scenario in a plain method: its lambdas capture locals, and an iterator's closure made before
    // EnterPlayMode's domain reload would be lost (NRE).
    private static void Scenario()
    {
        var culture = CultureSystem.Instance;
        var world = WorldSystem.Instance;
        var map = world.Map;
        var capital = WorldCivilization.Capital(map);
        Assert.IsNotNull(culture.WhyNotLocalGathering(capital, "evening-song"), "no customs before the founding");
        Assert.IsTrue(culture.ApplyConsequence("myth song", 1, "Why Were We Founded?"));
        Assert.IsTrue(culture.Name("Iridia", null, null, out _));
        Give("Deep-Rooted Grain", 80f);

        // The Capital begins its Evening of Song with a gathering: paid, recorded, its origin its own.
        Assert.IsTrue(culture.LocalGatheringChoices(capital).Any(c => c.spec.id == "evening-song" && c.why == null));
        var gathered = culture.HoldLocalGathering(capital, "evening-song");
        Assert.IsTrue(gathered.succeeded, gathered.reason);
        Assert.AreEqual(1, gathered.paid.Count);
        Assert.AreEqual(1, gathered.occurrences.Count);
        var song = culture.LocalProfile(capital.id).practices.Single(p => p.practice == "evening-song");
        Assert.AreEqual(LocalPracticeStage.Practiced, song.stage);
        Assert.AreEqual(PracticeChannel.Originated, song.origin.channel);
        Assert.IsNotNull(culture.WhyNotLocalGathering(capital, "evening-song"), "one gathering a week");

        // A hamlet on the outskirts: its founders are the Capital's people, its road joins them.
        var site = HamletSite(map, capital);
        var hamlet = WorldTributaries.Found(map, world.Settings.generation, world.Rules, site.coord, capital, world.AgeNumber);
        typeof(WorldSystem).GetMethod("CulturalTributaryFounding", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(world, new object[] { hamlet, capital });
        var known = culture.LocalProfile(hamlet.id).practices.Single(p => p.practice == "evening-song");
        Assert.AreEqual(LocalPracticeStage.Exposed, known.stage, "remembered, not yet kept");
        Assert.AreEqual(PracticeChannel.Founders, known.origin.channel);
        Assert.AreEqual(capital.id, known.origin.fromSettlement);
        ClearRoad(map, hamlet);
        world.ResetCulturalContact();
        // From here on only culture happens: no land may change hands.
        var authority = map.Tiles.Select(t => t.authorityId).ToList();
        Assert.IsTrue(culture.ContactEdges().Any(e => e.open && (e.a == hamlet.id || e.b == hamlet.id)), "its road is a route of contact");

        // A Seventh: the road carries the song a little further; nothing is taken up without gathering.
        float before = known.exposure;
        culture.Tick();
        var after = culture.LocalProfile(hamlet.id).practices.Single(p => p.practice == "evening-song");
        Assert.Greater(after.exposure, before - 1e-4f);
        Assert.AreEqual(LocalPracticeStage.Exposed, after.stage);

        // A cultural party takes the song up in the Capital and performs it in the hamlet: once per festival.
        var party = new WorldUnit { id = 90210, name = "Cultural Party of Vaelia", coord = capital.coord, charter = ExpeditionCharter.CulturalParty };
        Assert.IsNull(culture.WhyNotCarry(party, capital, "evening-song"));
        culture.Carry(party, capital, "evening-song");
        Assert.AreEqual("evening-song", culture.CarriedBy(party.id).Single().practice);
        string first = culture.CompleteLocalVisit(hamlet, party, new[] { "Vaelia" });
        Assert.IsNotNull(first);
        float shown = culture.LocalProfile(hamlet.id).practices.Single(p => p.practice == "evening-song").exposure;
        Assert.IsNull(culture.CompleteLocalVisit(hamlet, party, new[] { "Vaelia" }), "the same festival never counts twice");
        Assert.AreEqual(shown, culture.LocalProfile(hamlet.id).practices.Single(p => p.practice == "evening-song").exposure, 1e-4f);
        var taken = culture.HoldLocalGathering(hamlet, "evening-song");
        Assert.IsTrue(taken.succeeded, taken.reason);
        Assert.AreEqual(LocalPracticeStage.Practiced, culture.LocalProfile(hamlet.id).practices.Single(p => p.practice == "evening-song").stage);
        Assert.AreEqual(PracticeChannel.Founders, culture.LocalProfile(hamlet.id).practices.Single(p => p.practice == "evening-song").origin.channel, "its first provenance stays");

        // Arrivals: exactly the origin reported. Returning settlers from the hamlet name it; a story's arrivals stay unknown.
        var people = PopGrowthLogic.Instance;
        people.AddMigrants(2, "settlement:" + hamlet.id, "settlers", capital.id);
        people.AddMigrants(3);
        var arrivals = culture.ArrivalsAt(capital.id);
        Assert.IsTrue(arrivals.Any(a => a.originKind == ArrivalOriginKind.Settlement && a.people == 2 && a.originLabel == hamlet.name));
        var (_, _, unknown) = culture.ArrivalTotals(-1);
        Assert.AreEqual(3, unknown, "people from a story, destination and origin unsaid, are recorded as unknown");
        // Survivors let in at the gates carry the place they were found, never a culture.
        people.housing = people.population + 500;
        people.SurvivorsFound(4, 0, "survivors:" + site.index);
        // Everyone waiting is let in, each for its rations (a few at a time, whatever the stores can hold).
        var admit = typeof(PopGrowthLogic).GetMethod("AdmitFoundingMigrants", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int round = 0; round < 60 && people.WaitingMigrants > 0; round++)
        {
            Give(GameCatalog.ResourceFor(ResourceRole.Food).name, 60f);
            admit.Invoke(people, new object[] { 1000 });
        }
        Assert.AreEqual(0, people.WaitingMigrants);
        Assert.AreEqual(4, culture.ArrivalsAt(capital.id).Where(a => a.originKind == ArrivalOriginKind.Place).Sum(a => a.people), "survivors found at a known place");
        Assert.IsTrue(culture.ArrivalsAt(capital.id).Any(a => a.originKind == ArrivalOriginKind.Place && a.originLabel != null), "the place is named, not a culture");
        CollectionAssert.AreEqual(authority, map.Tiles.Select(t => t.authorityId).ToList(), "gatherings, contact, visits and arrivals moved no land");

        // All of it survives a save of the culture.
        var saved = JsonUtility.ToJson(SaveStateCodec.Capture(culture, "_state"));
        typeof(CultureSystem).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(culture, new CultureState());
        SaveStateCodec.Restore(culture, JsonUtility.FromJson<StateNode>(saved), "_state");
        culture.AfterRestore();
        Assert.AreEqual(LocalPracticeStage.Practiced, culture.LocalProfile(hamlet.id).practices.Single(p => p.practice == "evening-song").stage);
        Assert.AreEqual("evening-song", culture.CarriedBy(party.id).Single().practice);
        Assert.IsTrue(culture.ArrivalsAt(capital.id).Any(a => a.originKind == ArrivalOriginKind.Place));
        Assert.IsNull(culture.CompleteLocalVisit(hamlet, party, new[] { "Vaelia" }), "a reload does not replay a visit");
        var atlas = CultureAtlasReadModel.Capture(culture);
        string comparison = CultureAtlasReadModel.Compare(atlas.places.Single(p => p.settlement == capital.id), atlas.places.Single(p => p.settlement == hamlet.id));
        StringAssert.Contains(capital.name, comparison);
        StringAssert.Contains(hamlet.name, comparison);
        CollectionAssert.AreEqual(authority, map.Tiles.Select(t => t.authorityId).ToList(), "atlas comparison transfers no land");
    }
}
