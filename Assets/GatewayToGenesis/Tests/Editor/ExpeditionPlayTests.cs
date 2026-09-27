using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Expeditions of legends in the real scene (ClickerScreen): the map's technology sends the first expedition out free
/// under a legend no seat holds; a council legend leaves its seat to join it and is no longer offered for the council;
/// the road's hardship strains every member's Composure, the Director's most; mishaps strike its legends (a Spiraling
/// companion deserts and comes home); worn out entirely it breaks and its legends limp home strained; settlers join and
/// rejoin the citizens when the party disbands. Seventh ticks and mishaps are driven directly so the test does not wait
/// on the clock or on fortune. Helpers are static: locals captured before Enter Play Mode are lost to the domain reload.
/// </summary>
public class ExpeditionPlayTests
{
    private const string Scene = "Assets/GatewayToGenesis/Scenes/ClickerScreen.unity";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator LegendsWalkAsExpeditionsAndTheRoadStrainsThem()
    {
        EditorSceneManager.OpenScene(Scene);
        yield return new EnterPlayMode();
        for (int i = 0; i < 600 && !Ready(); i++) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        DismissSaveMenu();

        OpenTheMap();
        yield return null;
        int id = CheckTheFirstExpedition();
        string companion = JoinFromTheCouncil(id);
        yield return null;

        CheckHardship(id, companion);
        CheckMishaps(id, companion);
        yield return null;
        CheckBreaking(id);
        yield return null;
        CheckSettlers();
    }

    private static bool Ready() =>
        PopGrowthLogic.Instance != null && GovernmentLogic.Instance != null && LegendProgress.Instance != null && LegendProgress.Instance.RecruitedCount > 0 &&
        WorldSystem.Instance != null && WorldSystem.Instance.Map != null && GameUnitsLogic.Instance != null && AgeProgression.Instance != null && AgeProgression.Instance.Current != null;

    private static void DismissSaveMenu()
    {
        var menu = Object.FindAnyObjectByType<SaveMenu>();
        if (menu != null) typeof(SaveMenu).GetField("visible", Private).SetValue(menu, false);
        Time.timeScale = 1;
        Assert.IsFalse(SaveMenu.BlocksGameplay, "the save menu still holds the game");
    }

    private static void Seventh()
    {
        typeof(LegendProgress).GetMethod("OnSeventh", Private).Invoke(LegendProgress.Instance, new object[] { 0 });
    }

    // What settlers cost besides citizens (World.asset expeditions.settlerCost).
    private static void GiveSettlerCost()
    {
        foreach (var cost in WorldSystem.Instance.ExpeditionRules.settlerCost)
            GameUnitsLogic.Instance.ChangeResourceFromName(cost.resource, cost.amount * 2f, false);
    }

    private static void OpenTheMap()
    {
        var world = WorldSystem.Instance;
        Assert.AreEqual(0, world.Map.Units.Count, "no expedition before the map opens");
        var slot = GameUnitsLogic.Instance.GetTechnologySlot(world.Settings.mapTechnology);
        Assert.IsNotNull(slot, "the map's technology is not in the tree");
        slot.UnlockTechnology();
        Assert.IsTrue(world.MapUnlocked);
    }

    private static int CheckTheFirstExpedition()
    {
        var world = WorldSystem.Instance;
        var unit = world.ExpeditionUnits.SingleOrDefault();
        Assert.IsNotNull(unit, $"the first expedition sets out free (legends free: {string.Join(", ", world.Candidates())}; notice '{world.LastNotice}')");
        Assert.IsNotNull(unit.leader, "a legend directs it");
        Assert.IsNull(GovernmentLogic.Instance.GetSeatWithLegend(unit.leader), "its Director holds no seat");
        Assert.AreEqual(world.Map.Capital, unit.coord, "it waits at the Capital");
        StringAssert.StartsWith(unit.leader, unit.name, "it is known by its Director");
        Assert.AreEqual(1, world.ExpeditionSlotsUsed);
        Assert.GreaterOrEqual(world.ExpeditionSlots, 3, "the base and the Capital's Government Capacity");
        Assert.AreEqual(12f, world.SpecOf(unit).supplyCapacity, 1e-3, "a party of one carries one legend's rations");
        Assert.IsFalse(LegendLeaderLogic.Instance.GetAvailableLegends().Any(l => l.legendName == unit.leader), "a legend on the road is not offered for the council");
        return unit.id;
    }

    // A seated legend joins at the Capital: it leaves its seat, and the party grows.
    private static string JoinFromTheCouncil(int id)
    {
        var world = WorldSystem.Instance;
        var unit = world.UnitById(id);
        var government = GovernmentLogic.Instance;
        string seated = world.Candidates().FirstOrDefault();
        Assert.IsNotNull(seated, "a second legend is known at the start");
        SeatOnTheCouncil(seated);
        Assert.IsTrue(world.Candidates().Contains(seated), "a seated legend is offered too");
        var seat = government.GetSeatWithLegend(seated);
        government.ForceResetAllCooldowns();
        Assert.IsNull(world.WhyNotAddCompanion(unit, seated), world.WhyNotAddCompanion(unit, seated));
        Assert.IsTrue(world.AddCompanion(unit, seated));
        Assert.IsNull(government.GetSeatWithLegend(seated), $"{seated} left the {seat.GetEffectiveTitle()} seat");
        Assert.AreEqual(2, Expeditions.PartySize(unit));
        Assert.AreEqual(2, world.ExpeditionSlotsUsed);
        Assert.AreEqual(24f, world.SpecOf(unit).supplyCapacity, 1e-3, "two legends carry twice the rations");
        Assert.IsFalse(LegendLeaderLogic.Instance.GetAvailableLegends().Any(l => l.legendName == seated));
        return seated;
    }

    private static void SeatOnTheCouncil(string name)
    {
        var government = GovernmentLogic.Instance;
        var legend = GameCatalog.Legends.Get(name, nameof(ExpeditionPlayTests));
        var seats = Enumerable.Range(0, GovernmentLogic.RegularSeatCount).Append(GovernmentLogic.HeadOfStateIndex).ToList();
        if (!seats.Any(i => government.CanAssignLegendToSeat(legend, i))) government.UnlockNextCouncilSeat();
        int index = seats.First(i => government.CanAssignLegendToSeat(legend, i));
        Assert.IsTrue(government.AssignLegendToSeat(legend, index, bypassCooldown: true), $"{name} could not be seated");
    }

    // Worn and hungry, the road strains both legends each Seventh, the Director most.
    private static void CheckHardship(int id, string companion)
    {
        var world = WorldSystem.Instance;
        var legends = LegendProgress.Instance;
        var unit = world.UnitById(id);
        string director = unit.leader;
        Seventh();
        float directorBefore = legends.Soul(director).strain, companionBefore = legends.Soul(companion).strain;
        unit.attrition = 80f;
        unit.hungry = true;
        float hardship = world.HardshipOf(companion, out var walksWith, out bool resting);
        Assert.AreSame(unit, walksWith);
        Assert.IsFalse(resting, "on the road, not camped in a settlement");
        Assert.Greater(hardship, 0f, "a worn, hungry party carries hardship");
        Assert.Greater(world.HardshipOf(director, out _, out _), hardship, "the Director carries more");
        Seventh();
        Assert.Greater(legends.Soul(director).strain - directorBefore, legends.Soul(companion).strain - companionBefore, "the Director strains most");
        Assert.Greater(legends.Soul(companion).strain, companionBefore, "the road strains its companion");
        Assert.AreEqual(0f, world.MishapRisk(unit), 1e-5, "at the Capital the road's mishaps cannot reach it");
        Assert.Greater(Expeditions.MishapRisk(unit, new UnitSurroundings { weather = 1f }, world.Party(unit), world.ExpeditionRules), 0f, "in the wild a worn, hungry party courts mishaps");
    }

    // A fever strikes the companion; then, Spiraling, it deserts and comes home.
    private static void CheckMishaps(int id, string companion)
    {
        var world = WorldSystem.Instance;
        var legends = LegendProgress.Instance;
        var unit = world.UnitById(id);
        var strike = typeof(WorldSystem).GetMethod("Strike", Private);
        var fever = Expeditions.Mishaps(world.ExpeditionRules).First(m => m.kind == MishapKind.Fever);
        float before = legends.Soul(companion).strain, attrition = unit.attrition;
        strike.Invoke(world, new object[] { unit, new Mishap { spec = fever, target = companion } });
        Assert.AreEqual(before + fever.strain, legends.Soul(companion).strain, 0.01f, "the fever strains the legend it strikes");
        Assert.AreEqual(System.Math.Min(100f, attrition + fever.attrition), unit.attrition, 0.01f, "and wears the party");
        StringAssert.Contains(companion, world.LastNotice);

        legends.Soul(companion).strain = LegendLore.ComposureTuning.spiralingAt + 5f;
        var desertion = Expeditions.Mishaps(world.ExpeditionRules).First(m => m.kind == MishapKind.Desertion);
        strike.Invoke(world, new object[] { unit, new Mishap { spec = desertion, target = companion } });
        Assert.IsFalse(Expeditions.IsMember(unit, companion), "the deserter left the party");
        Assert.IsNull(world.ExpeditionOf(companion));
        Assert.IsTrue(LegendLeaderLogic.Instance.GetAvailableLegends().Any(l => l.legendName == companion), "home again, it can sit on the council");
    }

    // Worn out entirely, a lone legend goes to ground and turns up later in your territory; a party breaks, and its
    // legends limp home strained.
    private static void CheckBreaking(int id)
    {
        var world = WorldSystem.Instance;
        var legends = LegendProgress.Instance;
        var condition = typeof(WorldSystem).GetMethod("Condition", Private);
        var unit = world.UnitById(id);
        string director = unit.leader;
        Assert.AreEqual(1, Expeditions.PartySize(unit), "the deserter left it alone");
        Assert.AreEqual("Solo", world.PartyShapeOf(unit).name);

        float before = legends.Soul(director).strain;
        unit.attrition = 100f;
        Assert.IsTrue((bool)condition.Invoke(world, new object[] { unit, world.SpecOf(unit) }));
        Assert.AreSame(unit, world.UnitById(id), "alone, it does not break...");
        Assert.IsTrue(unit.Missing, "...it goes to ground");
        Assert.AreSame(unit, world.ExpeditionOf(director), "still away: not free for the council");
        Assert.AreEqual(before + world.ExpeditionRules.breakStrain * 0.5f, legends.Soul(director).strain, 0.01f, "the road weighs lighter on one who slips away");
        StringAssert.Contains("goes to ground", world.LastNotice);
        Assert.IsNotNull(world.WhyNotGo(unit, world.Map.Capital, out _, out _), "no one can order it");
        var tick = typeof(WorldSystem).GetMethod("TickMissing", Private);
        for (int i = 0; i < 100 && unit.Missing; i++) tick.Invoke(world, null);
        Assert.IsFalse(unit.Missing, "it turns up");
        Assert.AreEqual(world.ExpeditionRules.reappearAttrition, unit.attrition, 0.01f);
        Assert.IsTrue(unit.Camping, "and makes camp");
        Assert.IsTrue(WorldUnits.Held(world.Map, world.Map.Get(unit.coord)) || world.Map.Get(unit.coord).settlement >= 0, "inside your territory");

        // With a companion it is a Duo, and a Duo breaks.
        GovernmentLogic.Instance.ForceResetAllCooldowns();
        var capital = WorldCivilization.Capital(world.Map);
        WorldUnits.Place(unit, MicroNavigation.Center(capital.coord));
        string companion = world.Candidates().First();
        Assert.IsTrue(world.AddCompanion(unit, companion), world.WhyNotAddCompanion(unit, companion));
        Assert.AreEqual("Duo", world.PartyShapeOf(unit).name);
        before = legends.Soul(director).strain;
        unit.attrition = 100f;
        Assert.IsTrue((bool)condition.Invoke(world, new object[] { unit, world.SpecOf(unit) }));
        Assert.IsNull(world.UnitById(id), "the expedition is gone");
        Assert.IsNull(world.ExpeditionOf(director));
        Assert.AreEqual(before + world.ExpeditionRules.breakStrain, legends.Soul(director).strain, 0.01f, "the road came home with it");
        StringAssert.Contains("breaks", world.LastNotice);
    }

    // Settlers join a new party at the Capital and rejoin the citizens when it disbands there.
    private static void CheckSettlers()
    {
        var world = WorldSystem.Instance;
        var pop = PopGrowthLogic.Instance;
        var capital = WorldCivilization.Capital(world.Map);
        GiveSettlerCost();
        pop.population = System.Math.Max(pop.population, 30);
        GovernmentLogic.Instance.ForceResetAllCooldowns();
        string director = world.Candidates().First();
        var unit = world.FormExpedition(capital, director);
        Assert.IsNotNull(unit, world.WhyNotForm(capital, director));
        int citizens = pop.population;
        Assert.IsNull(world.WhyNotTakeSettlers(unit), world.WhyNotTakeSettlers(unit));
        Assert.IsTrue(world.TakeSettlers(unit));
        Assert.AreEqual(citizens - world.ExpeditionRules.settlers, pop.population, "the settlers leave the city");
        Assert.IsTrue(WorldUnits.Can(world.SpecOf(unit), UnitAbility.Settle), "escorting settlers, it can found a settlement");
        Assert.Less(world.SpecOf(unit).stamina, world.Settings.Unit(world.ExpeditionRules.unit).stamina, "settlers slow the party");
        Assert.IsTrue(world.Disband(unit));
        Assert.AreEqual(citizens, pop.population, "back home, the settlers are citizens again");
        Assert.IsNull(world.ExpeditionOf(director));
    }
}
