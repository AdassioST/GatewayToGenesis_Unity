using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Territorial pull in the real scene (<see cref="WorldTerritory"/>): once the map is open, the Capital's society adopts
/// known land each Seventh, the realm's administration is reported to conditions (<see cref="GameValues"/>), the border
/// policy holds it, and the world view's Realm panel and the Beauty and Territorial pull lenses draw.
/// </summary>
public class TerritoryPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);

    [UnityTest]
    public IEnumerator TheCapitalAdoptsKnownLandAndTheRealmPanelReportsIt()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && (WorldSystem.Instance == null || WorldSystem.Instance.Map == null || Object.FindAnyObjectByType<WorldView>() == null); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) menu.GetType().GetField("visible", Private)?.SetValue(menu, false);
        Time.timeScale = 1;

        var world = WorldSystem.Instance;
        foreach (var tech in new[] { world.Settings.mapTechnology, world.Settings.unlockTechnology })
        {
            var slot = GameUnitsLogic.Instance.GetTechnologySlot(tech);
            if (slot != null && !GameUnitsLogic.Instance.IsTechnologyUnlocked(tech)) slot.UnlockTechnology();
        }
        yield return null;
        Assert.IsTrue(world.MapUnlocked && world.IsOpen, "the map and building beyond the walls are open");

        var map = world.Map;
        map.KnowAround(map.Capital, 5, 6, world.IsPassable);
        WorldCivilization.Rebuild(map, world.Settings.generation);
        Assert.IsNotNull(map.territory, "the territory is derived with the civilization");
        Assert.IsTrue(map.territory.Seats.Any(s => s.kind == SeatKind.Capital));
        int before = map.Tiles.Count(t => t.authorityId == WorldAuthority.Player);
        Assert.IsTrue(GameValues.TryGet("territory", "", out float held) && held == before, $"conditions read the land held ({held} vs {before})");

        // Hold the borders: nothing is adopted.
        world.SetBorderPolicy(BorderPolicy.Hold);
        for (int i = 0; i < 3; i++) Call(world, "TerritoryTick", 1f);
        Assert.AreEqual(before, map.Tiles.Count(t => t.authorityId == WorldAuthority.Player), "held borders adopt nothing");

        // Measured, but too few citizens in the Capital: no one goes out to settle.
        world.SetBorderPolicy(BorderPolicy.Measured);
        var people = PopGrowthLogic.Instance;
        int citizens = people.population;
        var rules = world.Rules.territory;
        people.population = rules.adoptionMinPopulation - 1;
        for (int i = 0; i < 3; i++) Call(world, "TerritoryTick", 1f);
        Assert.IsFalse(map.Tiles.Any(t => t.microHeldMask != 0 && t.authorityId == WorldAuthority.Wilderness), "no settlers below the minimum population");
        StringAssert.Contains("citizens", (string)typeof(WorldView).GetMethod("RealmText", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { world, world.Realm }));

        // Enough citizens: the Capital's society brings land in hex by hex, the best cell first, whole after seven.
        people.population = rules.adoptionFullPopulation;
        var first = world.NextAdoptions(1).FirstOrDefault();
        Assert.IsNotNull(first.seat, "some known land waits to be adopted");
        Call(world, "TerritoryTick", 1f);
        for (int i = 0; i < 60 && map[first.cell].microHeldMask == 0; i++) Call(world, "TerritoryTick", 1f);
        Assert.Greater(WorldMap.SettledHexes(map[first.cell]), 0, "a hex of the best candidate is settled");
        // Yours de facto from four hexes, core with all seven.
        for (int i = 0; i < 120 && map[first.cell].authorityId != WorldAuthority.Player; i++) Call(world, "TerritoryTick", 1f);
        Assert.AreEqual(WorldAuthority.Player, map[first.cell].authorityId, "the best candidate first");
        Assert.GreaterOrEqual(WorldMap.SettledHexes(map[first.cell]), WorldHoldings.DeFactoHexes(map), "de facto from four hexes");
        for (int i = 0; i < 120 && map[first.cell].hold != HoldStatus.Core; i++) Call(world, "TerritoryTick", 1f);
        people.population = citizens;
        int after = map.Tiles.Count(t => t.authorityId == WorldAuthority.Player);
        Assert.Greater(after, before, "society adopts land by itself");
        Assert.IsTrue(WorldTerritory.FullySettled(map[first.cell]), "every hex of it settled: core");
        var realm = world.Realm;
        Assert.AreEqual(map.Tiles.Count(t => t.authorityId == WorldAuthority.Player && t.hold != HoldStatus.DeFacto), realm.cells, "the Realm counts core cells");
        Assert.Greater(realm.capacity, 0f);
        Assert.IsTrue(GameValues.TryGet("admin_strain", "", out float strain) && Mathf.Abs(strain - realm.strain * 100f) < 0.5f);
        Assert.IsTrue(GameValues.TryGet("expansion", realm.favoured.ToString().ToLowerInvariant(), out float favoured) && favoured == 1f);

        // The world view: the Realm panel and the two new lenses.
        WorldView.ShowWorld();
        for (int i = 0; i < 90 && WorldView.Current != WorldView.Mode.World; i++) yield return null;
        var view = Object.FindAnyObjectByType<WorldView>();
        var popupType = view.GetType().GetNestedType("Popup", BindingFlags.NonPublic);
        Call(view, "ShowPopup", System.Enum.Parse(popupType, "Realm"));
        yield return null;
        // The panel is drawn from RealmText into the popup's pooled rows.
        var ledger = (string)typeof(WorldView).GetMethod("RealmText", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { world, world.Realm });
        StringAssert.Contains("Administrative Capacity", ledger);
        StringAssert.Contains("Wide or tall", ledger);
        foreach (var lens in new[] { WorldLens.Beauty, WorldLens.Territory })
        {
            Call(view, "SetLens", lens);
            yield return null;
            var renderer = (WorldRenderer)view.GetType().GetField("_renderer", Private).GetValue(view);
            Assert.AreEqual(lens, renderer.Lens);
        }
        yield return new ExitPlayMode();
    }
}
