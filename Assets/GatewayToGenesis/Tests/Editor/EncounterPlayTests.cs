using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Encounters in the real scene (ClickerScreen, <see cref="WorldPursuit"/>): a timid band flees the party that gives
/// chase and is run down and fought; a Regular hostile band will not come near the Capital where the party stands, and
/// gives up; the same band as a beast attacks there; a band's card reads its temper and mind and orders nothing, and the
/// party's card offers the hunt. The world's moments are driven directly (WorldSystem.MoveUnits), so the test does not
/// wait on the clock. Helpers are static: locals captured before Enter Play Mode are lost to the domain reload.
/// </summary>
public class EncounterPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator BandsFleeChaseAndGiveUp()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        DismissSaveMenu();
        OpenTheMap();
        yield return null;
        int party = TheParty();
        RunDownATimidBand(party);
        yield return null;
        ARegularBandWillNotComeNearTheCapital(party);
        yield return null;
        TheCardsSayWhatABandIs(party);
        yield return null;
        OnlyYourPartiesTakeOrders();
        yield return null;
        WaterCreaturesStayInTheWater(party);
        yield return null;
        ExplorersReadTheSignsOfAHunter(party);
        yield return null;
        SufferingPoolsIntoAMassThatHatches(party);
        yield return null;
        APeoplesSufferingImprintsItsOwnLand(party);
        yield return null;
        ACaptiveIsFreedWhenItsCaptorFalls(party);
    }

    private static void Clear() => WorldSystem.Instance.Map.Units.RemoveAll(u => !WorldBattles.IsPlayers(u));

    private static void OnlyYourPartiesTakeOrders()
    {
        var world = WorldSystem.Instance;
        var band = world.Map.Units.First(u => !WorldBattles.IsPlayers(u));
        var from = WorldUnits.MicroPosition(band);
        Assert.IsFalse(world.GoMicro(band, from.Neighbor(0).Neighbor(0)), "a band is never yours to move");
        Assert.AreEqual(WorldSystem.NotYours, world.WhyNotGoMicro(band, from.Neighbor(0), out _, out _, out _));
        world.MakeCamp(band);
        Assert.IsFalse(band.Camping);
        world.SetAutoExplore(band, true);
        Assert.IsFalse(band.autoExplore);
        Clear();
    }

    private static void WaterCreaturesStayInTheWater(int partyId)
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        var grid = MicroNavigation.Grid(map, world.Settings.generation);
        var from = WorldUnits.MicroPosition(world.UnitById(partyId));
        HexCoord? water = null;
        for (int r = 1; r <= 60 && water == null; r++)
            foreach (var h in HexCoord.Spiral(from, r).Where(h => HexCoord.Distance(h, from) == r))
            {
                int id = MicroNavigation.Index(map, h);
                if (id >= 0 && WorldPursuit.IsWater(map, grid, id) && !map.Units.Any(u => WorldUnits.MicroPosition(u) == h)) { water = h; break; }
            }
        if (water == null) { Debug.Log("No water near the Capital on this map: the aquatic check is skipped."); return; }
        var trout = world.SpawnBand("river-trout", 5, water.Value, EnemyStance.Timid);
        Assert.IsNotNull(trout);
        Assert.AreEqual(CreatureHabitat.Water, trout.habitat, "World.asset: river trout live in the water");
        int troutId = trout.id;
        bool dry = false;
        Run(4f, () =>
        {
            var t = world.UnitById(troutId);
            if (t != null) dry |= !WorldPursuit.IsWater(map, grid, MicroNavigation.Index(map, WorldUnits.MicroPosition(t)));
            return false;
        });
        Assert.IsFalse(dry, "it never set a fin on land");
        Clear();
    }

    private static void ExplorersReadTheSignsOfAHunter(int partyId)
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        var party = world.UnitById(partyId);
        int sight = WorldUnits.Sight(map, party, world.SpecOf(party), world.Rules);
        var at = WorldUnits.MicroPosition(party);
        // A hunter just beyond sight (and outside your territory, where your people would report it).
        var cell = map.Tiles.Where(t => MicroNavigation.Walkable(t, world.Settings.generation) && !WorldAuthority.IsPlayers(t.authorityId))
            .Select(t => (t, d: HexCoord.Distance(at, MicroNavigation.Center(t.coord))))
            .Where(p => p.d >= sight + 2 && p.d <= sight + 3).OrderBy(p => p.t.index).Select(p => p.t).FirstOrDefault();
        if (cell == null) { Debug.Log("No wild ground at the right distance: the signs check is skipped."); return; }
        var hunter = world.SpawnBand(null, 1, MicroNavigation.Center(cell.coord), EnemyStance.Hostile, WorldSuffering.Hatchling.id);
        Assert.IsNotNull(hunter);
        hunter.truce = 100f; // it stays put: only its signs speak
        Assert.IsFalse(world.Sees(hunter), "out of sight");
        Run(3f, () => (world.LastNotice ?? string.Empty).Contains("something is hunting nearby"));
        StringAssert.Contains("something is hunting nearby", world.LastNotice ?? string.Empty, "the explorer finds its signs");
        Assert.Greater(cell.signs, 0f, "the signs lie on its cell");
        Clear();
    }

    private static void SufferingPoolsIntoAMassThatHatches(int partyId)
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        var at = WorldUnits.MicroPosition(world.UnitById(partyId));
        var cell = map.Tiles.Where(t => MicroNavigation.Walkable(t, world.Settings.generation))
            .Select(t => (t, d: HexCoord.Distance(at, MicroNavigation.Center(t.coord)))).Where(p => p.d >= 12 && p.d <= 16)
            .OrderBy(p => p.t.index).Select(p => p.t).First();
        world.AddSuffering(cell.index, Feeling.Pain, 2f);
        Run(2f, () => map.Units.Any(u => u.identity == BandIdentity.FormlessMass && u.homeCell == cell.index));
        var mass = map.Units.FirstOrDefault(u => u.identity == BandIdentity.FormlessMass && u.homeCell == cell.index);
        Assert.IsNotNull(mass, "the suffering pools into a Formless Mass");
        Assert.AreEqual(BandIntelligence.Instinctive, mass.intelligence, "limited intelligence");
        int id = mass.id;
        Run(10f, () => world.UnitById(id)?.bandActivity == BandActivity.Cocooned);
        Assert.AreEqual(BandActivity.Cocooned, world.UnitById(id)?.bandActivity, $"fed enough, it cocoons (fed {world.UnitById(id)?.fed?.Total:0.##})");
        Assert.Less(cell.suffering, 2f, "it drained the land");
        Run(WorldSuffering.CocoonSevenths + 1f, () => world.UnitById(id)?.identity == BandIdentity.Atonalis);
        var born = world.UnitById(id);
        Assert.AreEqual(BandIdentity.Atonalis, born?.identity, "an Atonalis hatches");
        Assert.AreEqual(AtonalPath.Carnalix, born.atonalPath, "fed on slaughter, it walks the Path of Carnalix");
        Assert.AreEqual(BandIntelligence.Instinctive, born.intelligence);
        Clear();
    }

    private static void APeoplesSufferingImprintsItsOwnLand(int partyId)
    {
        var world = WorldSystem.Instance;
        var map = world.Map;
        var capital = map.Settlements.First(s => s.kind == SettlementKind.Capital);
        var ground = map.Get(capital.coord);
        // The party steps well away: a mass that pools beside it is provoked and cut down at once.
        var party = world.UnitById(partyId);
        var away = map.Tiles.Where(t => MicroNavigation.Walkable(t, world.Settings.generation) && HexCoord.Distance(t.coord, capital.coord) >= 6)
            .OrderBy(t => HexCoord.Distance(t.coord, capital.coord)).ThenBy(t => t.index).First();
        WorldUnits.Stop(party);
        WorldUnits.Place(party, MicroNavigation.Center(away.coord));
        float strainBefore = capital.strain;
        string causeBefore = capital.harmedBy;
        try
        {
            capital.strain = 90f;
            capital.harmedBy = WorldRuins.Pillage;
            float before = ground.imprint?.pain ?? 0f;
            Run(2f);
            Assert.AreEqual(Feeling.Pain, capital.feelings.DominantWound, "a pillaged people feels pain");
            Assert.Greater(ground.imprint.pain, before, "and gives it off into its own ground");
            // Enough of it, and nothing drinking it: lingering Consciousness pools on your own land.
            world.AddSuffering(ground.index, Feeling.Pain, 1.2f);
            Run(2f, () => (world.LastNotice ?? string.Empty).Contains("your people have poured into"));
            StringAssert.Contains("your people have poured into", world.LastNotice ?? string.Empty, "a Formless Mass pools on your land, and you are told what it pooled from");
            Assert.IsTrue(map.Units.Any(u => u.identity == BandIdentity.FormlessMass && u.homeCell == ground.index), "the mass stands on your land");
        }
        finally
        {
            capital.strain = strainBefore;
            capital.harmedBy = causeBefore;
            Clear();
        }
    }

    private static void ACaptiveIsFreedWhenItsCaptorFalls(int partyId)
    {
        var world = WorldSystem.Instance;
        var party = world.UnitById(partyId);
        var progress = LegendProgress.Instance;
        string legend = party.leader;
        var band = world.SpawnBand(null, 2, Near(party, 4), EnemyStance.Hostile, WorldSuffering.Hatchling.id);
        band.atonalPath = AtonalPath.Anxithor;
        WorldPursuit.ApplyPath(band);
        progress.TakeCaptive(legend, "band:" + band.id, 3f, 0f, band.name);
        band.captives.Add(legend);
        Assert.IsTrue(progress.IsCaptive(legend));
        Assert.IsTrue(progress.IsMissing(legend), "a captive is off the roster");
        typeof(WorldSystem).GetMethod("RemoveFromMap", Private).Invoke(world, new object[] { band });
        Assert.IsFalse(progress.IsCaptive(legend), "its captor fell: it is free");
        Assert.Greater(progress.MissingSevenths(legend), 0, "and makes for home");
    }

    private static bool Ready() =>
        PopGrowthLogic.Instance != null && GovernmentLogic.Instance != null && LegendProgress.Instance != null && LegendProgress.Instance.RecruitedCount > 0 &&
        WorldSystem.Instance != null && WorldSystem.Instance.Map != null && GameUnitsLogic.Instance != null && AgeProgression.Instance != null && AgeProgression.Instance.Current != null;

    private static void DismissSaveMenu()
    {
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;
    }

    private static void OpenTheMap() => GameUnitsLogic.Instance.GetTechnologySlot(WorldSystem.Instance.Settings.mapTechnology).UnlockTechnology();

    private static int TheParty()
    {
        var world = WorldSystem.Instance;
        var unit = world.ExpeditionUnits.Single();
        // Only the bands this test raises: clear any the threats or dens sent out.
        world.Map.Units.RemoveAll(u => !WorldBattles.IsPlayers(u));
        return unit.id;
    }

    // Some Sevenths of the world's moments, stopping early once `done` holds.
    private static void Run(float sevenths, System.Func<bool> done = null)
    {
        var world = WorldSystem.Instance;
        var move = typeof(WorldSystem).GetMethod("MoveUnits", Private);
        for (float t = 0f; t < sevenths; t += 0.05f)
        {
            move.Invoke(world, new object[] { 0.05f });
            if (done != null && done()) return;
        }
    }

    // A free hex `distance` from the party, on ground it can reach.
    private static HexCoord Near(WorldUnit unit, int distance)
    {
        var world = WorldSystem.Instance;
        var from = WorldUnits.MicroPosition(unit);
        foreach (var hex in HexCoord.Spiral(from, distance).Where(h => HexCoord.Distance(h, from) == distance).OrderBy(h => h.q).ThenBy(h => h.r))
        {
            if (MicroNavigation.Index(world.Map, hex) < 0 || world.Map.Units.Any(u => WorldUnits.MicroPosition(u) == hex)) continue;
            if (MicroNavigation.ToNearest(world.Map, world.Settings.generation, from, hex, out var way, out _, out var reached) && reached == hex) return hex;
        }
        Assert.Fail($"no open ground {distance} hexes from the party");
        return from;
    }

    private static void RunDownATimidBand(int partyId)
    {
        var world = WorldSystem.Instance;
        var party = world.UnitById(partyId);
        var hares = world.SpawnBand("meadow-hare", 3, Near(party, 3), EnemyStance.Timid);
        Assert.IsNotNull(hares, "a band of hares");
        Assert.AreEqual(BandIdentity.Creature, hares.identity);
        Assert.AreEqual(BandIntelligence.Instinctive, hares.intelligence, "hares are beasts");
        Assert.IsTrue(world.Sees(hares), "three hexes off, the party sees them");
        int id = hares.id;
        BattleReport fought = null;
        System.Action<BattleReport, WorldUnit, WorldUnit> onBattle = (r, a, d) => { if (a.id == id || d.id == id) fought = r; };
        world.BattleFought += onBattle;
        try
        {
            Assert.IsNull(world.WhyNotEngage(party, hares), world.WhyNotEngage(party, hares));
            Assert.IsTrue(world.EngageBand(party, hares));
            Assert.IsTrue(party.sprinting, "the party runs after them");
            bool fled = false, tired = false;
            Run(20f, () =>
            {
                var h = world.UnitById(id);
                if (h != null) { fled |= h.bandActivity == BandActivity.Fleeing; tired |= h.endurance < 100f; }
                return fought != null;
            });
            Assert.IsTrue(fled, "the hares flee the party");
            Assert.IsTrue(tired, "running tires them");
            Assert.IsNotNull(fought, $"the party runs them down (party at {WorldUnits.MicroPosition(party)}, endurance {party.endurance:0}; hares {world.UnitById(id)?.bandActivity}, endurance {world.UnitById(id)?.endurance:0})");
            Assert.AreEqual(-1, party.quarryId, "the chase is over");
            Assert.IsFalse(party.sprinting);
        }
        finally { world.BattleFought -= onBattle; }
        world.Map.Units.RemoveAll(u => !WorldBattles.IsPlayers(u));
    }

    private static void ARegularBandWillNotComeNearTheCapital(int partyId)
    {
        var world = WorldSystem.Instance;
        var party = world.UnitById(partyId);
        // Back to the Capital's own hex, fresh.
        WorldUnits.Place(party, MicroNavigation.Center(world.Map.Capital));
        party.coord = world.Map.Capital;
        party.truce = 0f;
        party.endurance = 100f;
        party.winded = false;
        var wolves = world.SpawnBand("grey-wolf", 1, Near(party, 3), EnemyStance.Hostile);
        Assert.IsNotNull(wolves);
        Assert.AreEqual(BandIntelligence.Regular, wolves.intelligence, "a pack hunter thinks");
        Assert.IsTrue(WorldBattles.Angry(wolves), "red: hostile from the start");
        int id = wolves.id;
        bool fought = false;
        System.Action<BattleReport, WorldUnit, WorldUnit> onBattle = (r, a, d) => { if (a.id == id || d.id == id) fought = true; };
        world.BattleFought += onBattle;
        try
        {
            Run(2f, () => world.UnitById(id)?.bandActivity == BandActivity.Returning);
            Assert.AreEqual(BandActivity.Returning, world.UnitById(id)?.bandActivity, "it will not come near the settlement");
            Assert.IsFalse(fought);
            // The same band as a beast knows no such caution.
            var beast = world.UnitById(id);
            beast.intelligence = BandIntelligence.Instinctive;
            beast.bandActivity = BandActivity.Roaming;
            beast.chaseCooldown = 0f;
            beast.chaseTiles = 0;
            beast.homeCell = world.Map.Get(world.Map.Capital).index;
            Run(6f, () => fought);
            Assert.IsTrue(fought, "an Instinctive band attacks the party at the Capital");
        }
        finally { world.BattleFought -= onBattle; }
    }

    private static void TheCardsSayWhatABandIs(int partyId)
    {
        var world = WorldSystem.Instance;
        var party = world.UnitById(partyId);
        Assert.IsNotNull(party, "the party survived");
        world.Map.Units.RemoveAll(u => !WorldBattles.IsPlayers(u));
        party.truce = 0f;
        party.winded = false;
        var hares = world.SpawnBand("meadow-hare", 2, Near(party, 2), EnemyStance.Timid);
        var describe = typeof(WorldView).GetMethod("DescribeUnit", BindingFlags.NonPublic | BindingFlags.Static);
        var text = new StringBuilder();
        var actions = new List<(string label, string why, System.Action call)>();
        describe.Invoke(null, new object[] { world, hares, text, actions });
        StringAssert.Contains("Temper", text.ToString());
        StringAssert.Contains("Instinctive", text.ToString());
        Assert.AreEqual(0, actions.Count, "a band takes no orders from you");
        text.Clear();
        describe.Invoke(null, new object[] { world, party, text, actions });
        StringAssert.Contains("Endurance", text.ToString());
        Assert.IsTrue(actions.Any(a => a.label.StartsWith("Hunt " + hares.name)), string.Join(" | ", actions.Select(a => a.label)));
        Assert.IsTrue(actions.Any(a => a.label.StartsWith("Run")), "it can be told to run");
        Assert.IsFalse(WorldUnits.AwaitsOrders(hares), "a band is never an idle party");
    }
}
