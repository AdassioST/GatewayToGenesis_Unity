using System.Linq;
using NUnit.Framework;

/// <summary>
/// Land held micro hex by micro hex by any holder (<see cref="WorldHoldings"/>): de facto from 4 of 7 hexes, core with
/// all of them, shared cells as disputes with grievances and casus belli, occupied cores to recover. No scene.
/// </summary>
public class WorldHoldingsTests
{
    private const string Rival = "rival:test";

    private static WorldGenSettings Gen()
    {
        var s = WorldGenerationTests.Settings();
        s.capitalAuthorityRadius = 0;
        s.startExploreRadius = 0;
        s.startRevealRadius = 1;
        return s;
    }

    private static WorldMap Fresh(WorldGenSettings settings)
    {
        var map = WorldGenerator.Generate(7, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());
        map.territoryRules = new TerritoryRules();
        map.KnowAround(map.Capital, 3, 4, id => settings.Terrain(id)?.passable != false);
        WorldCivilization.Rebuild(map, settings);
        return map;
    }

    // A wilderness cell beside the Capital with no crags (seven open hexes) and no settlement or enclave.
    private static WorldTile Beside(WorldMap map) =>
        map.NeighboursOf(map.Get(map.Capital)).First(t => !t.water && !t.impassable && t.microBlockedMask == 0 && t.authorityId == WorldAuthority.Wilderness && t.settlement < 0 && t.enclave < 0);

    private static int Hex(WorldTile t, int k) => t.index * MicroNavigation.PerCell + k;

    // Give hexes k of the cell to a holder, one by one.
    private static void Give(WorldMap map, WorldTile t, string holder, params int[] ks)
    {
        foreach (int k in ks) WorldHoldings.TakeHex(map, Hex(t, k), holder);
    }

    [Test]
    public void AHolderRulesACellDeFactoFromFourHexesAndMakesItCoreWithAllSeven()
    {
        var settings = Gen();
        var map = Fresh(settings);
        var t = Beside(map);
        Assert.AreEqual(4, WorldHoldings.DeFactoHexes(map));

        Give(map, t, WorldAuthority.Player, 1, 2, 3);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(WorldAuthority.Wilderness, t.authorityId, "three hexes do not rule it");
        Assert.AreEqual(HoldStatus.Partial, WorldHoldings.Status(map, t, WorldAuthority.Player));

        Give(map, t, WorldAuthority.Player, 0);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(WorldAuthority.Player, t.authorityId, "four rule it de facto");
        Assert.AreEqual(HoldStatus.DeFacto, t.hold);
        Assert.IsTrue(WorldHoldings.Fillable(t, WorldAuthority.Player), "its free hexes can still be taken");
        Assert.IsNull(WorldHoldings.HexHolder(map, Hex(t, 5)), "a free hex of a de facto cell is no one's");
        Assert.AreEqual(4f / 7f, WorldTerritory.HeldShare(map, t), 1e-5f);

        Give(map, t, WorldAuthority.Player, 4, 5, 6);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(HoldStatus.Core, t.hold, "all seven: core");
        Assert.AreEqual(1f, WorldTerritory.HeldShare(map, t), 1e-5f);
        Assert.IsNull(WorldHoldings.Dispute(map, t), "yours alone: no dispute");
    }

    [Test]
    public void FourAgainstThree_TheRulerHasACasusBelliToIntegrateAndTheMinorityAGrievance()
    {
        var settings = Gen();
        var map = Fresh(settings);
        var t = Beside(map);
        Give(map, t, WorldAuthority.Player, 0, 1, 2, 3);
        Give(map, t, Rival, 4, 5, 6);
        WorldCivilization.Rebuild(map, settings);

        Assert.AreEqual(WorldAuthority.Player, t.authorityId);
        Assert.AreEqual(HoldStatus.DeFacto, t.hold);
        Assert.AreEqual(Rival, WorldHoldings.HexHolder(map, Hex(t, 5)));
        Assert.AreEqual(HoldStatus.Partial, WorldHoldings.Status(map, t, Rival));
        Assert.AreEqual(0, WorldHoldings.FreeMask(map, t), "every hex held");
        StringAssert.Contains("only taken by force", WorldAuthority.WhyNotClaimHex(map, Hex(t, 5)));
        StringAssert.Contains("only taken by force", WorldAuthority.WhyNotClaim(map, t));

        var d = WorldHoldings.Dispute(map, t);
        Assert.IsNotNull(d);
        Assert.AreEqual(WorldAuthority.Player, d.ruler);
        Assert.AreEqual(4, d.HexesOf(WorldAuthority.Player));
        Assert.AreEqual(3, d.HexesOf(Rival));
        Assert.AreEqual(CasusBelli.Integrate, WorldHoldings.CasusBelliOf(d, WorldAuthority.Player), "integrate the rest as core");
        Assert.AreEqual(CasusBelli.None, WorldHoldings.CasusBelliOf(d, Rival));
        Assert.AreEqual(3, d.Grievance(Rival), "its three hexes live under your rule");
        Assert.AreEqual(0, d.Grievance(WorldAuthority.Player));
        Assert.AreEqual(1, WorldHoldings.Grievances(map, Rival).Count);
        Assert.AreEqual(1, WorldHoldings.CasusBelliFor(map, WorldAuthority.Player).Count);
        Assert.AreEqual(3f / 7f, 1f - WorldTerritory.HeldShare(map, t), 1e-5f, "you work only your four");

        // Taking the rival's hexes integrates the cell.
        Give(map, t, WorldAuthority.Player, 4, 5, 6);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(HoldStatus.Core, t.hold);
        Assert.IsNull(WorldHoldings.Dispute(map, t));
        Assert.IsEmpty(map.HexHoldings, "an emptied holding with no core claim is dropped");
    }

    [Test]
    public void ThreeAgainstFour_TheRivalRulesAndYourPeopleHoldAGrievance()
    {
        var settings = Gen();
        var map = Fresh(settings);
        var t = Beside(map);
        Give(map, t, WorldAuthority.Player, 0, 1, 2);
        Give(map, t, Rival, 3, 4, 5, 6);
        WorldCivilization.Rebuild(map, settings);

        Assert.AreEqual(Rival, t.authorityId, "the rival rules it de facto");
        Assert.AreEqual(HoldStatus.DeFacto, t.hold);
        Assert.AreEqual(3f / 7f, WorldTerritory.HeldShare(map, t), 1e-5f, "your three hexes are still yours");
        var d = WorldHoldings.Dispute(map, t);
        Assert.AreEqual(3, d.Grievance(WorldAuthority.Player));
        Assert.AreEqual(CasusBelli.Integrate, WorldHoldings.CasusBelliOf(d, Rival));
        StringAssert.Contains("Another authority", WorldAuthority.WhyNotClaim(map, t));
    }

    [Test]
    public void ATieRulesNoOne()
    {
        var settings = Gen();
        var map = Fresh(settings);
        var t = Beside(map);
        Give(map, t, WorldAuthority.Player, 0, 1, 2);
        Give(map, t, Rival, 3, 4, 5);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(WorldAuthority.Wilderness, t.authorityId);
        Assert.IsNull(WorldHoldings.Ruler(map, t));
        Assert.IsNotNull(WorldHoldings.Dispute(map, t), "still contested");
        Assert.IsNull(WorldAuthority.WhyNotClaimHex(map, Hex(t, 6)), "the last free hex can be claimed (it borders yours)");
    }

    [Test]
    public void AnOccupiedCoreIsYoursToRecover()
    {
        var settings = Gen();
        var map = Fresh(settings);
        var t = Beside(map);
        map.Claims.Add(t.index);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(HoldStatus.Core, t.hold, "an older whole-cell claim holds every hex");
        CollectionAssert.Contains(WorldHoldings.CoresOf(map, t), WorldAuthority.Player);

        // Occupied in part: still yours, but a core to recover.
        Assert.AreEqual(WorldAuthority.Player, WorldHoldings.TakeHex(map, Hex(t, 6), Rival));
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(WorldAuthority.Player, t.authorityId);
        Assert.AreEqual(HoldStatus.DeFacto, t.hold);
        Assert.AreEqual(CasusBelli.Recover, WorldHoldings.CasusBelliOf(WorldHoldings.Dispute(map, t), WorldAuthority.Player));
        Assert.AreEqual(1, WorldHoldings.Dispute(map, t).Grievance(WorldAuthority.Player), "one hex of your core held by another");

        // Occupied: the occupier rules it, the core claim stays yours.
        Give(map, t, Rival, 3, 4, 5);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(Rival, t.authorityId);
        var occupied = WorldHoldings.Occupied(map, WorldAuthority.Player);
        Assert.AreEqual(1, occupied.Count);
        Assert.AreEqual(t.index, occupied[0].cell);
        Assert.AreEqual(CasusBelli.Recover, WorldHoldings.CasusBelliOf(occupied[0], WorldAuthority.Player));
        Assert.AreEqual(4, occupied[0].Grievance(WorldAuthority.Player));

        // Recovered: core again, no dispute.
        Give(map, t, WorldAuthority.Player, 3, 4, 5, 6);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(HoldStatus.Core, t.hold);
        Assert.IsEmpty(WorldHoldings.Occupied(map, WorldAuthority.Player));
    }

    [Test]
    public void AnotherHoldersCoreClaimOutlivesItsHexes()
    {
        var settings = Gen();
        var map = Fresh(settings);
        var t = Beside(map);
        Give(map, t, Rival, 0, 1, 2, 3, 4, 5, 6);
        WorldHoldings.MarkCore(map, t.index, Rival);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(Rival, t.authorityId);
        Assert.AreEqual(HoldStatus.Core, t.hold);

        Give(map, t, WorldAuthority.Player, 0, 1, 2, 3, 4, 5, 6);
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(WorldAuthority.Player, t.authorityId, "taken whole");
        Assert.AreEqual(1, map.HexHoldings.Count, "the rival's claim remains with no hex");
        CollectionAssert.Contains(WorldHoldings.CoresOf(map, t), Rival);
        Assert.AreEqual(CasusBelli.Recover, WorldHoldings.CasusBelliOf(WorldHoldings.Dispute(map, t), Rival));
        Assert.AreEqual(7, WorldHoldings.Dispute(map, t).Grievance(Rival));
    }

    [Test]
    public void AHexTakenFromACellGivenWholeLeavesTheRestToItsAuthority()
    {
        var settings = Gen();
        var map = Fresh(settings);
        var capital = map.Get(map.Capital);
        Assert.IsTrue(capital.whole);
        int open = WorldMap.OpenHexes(capital);
        Assert.AreEqual(open, WorldHoldings.Hexes(map, capital, WorldAuthority.Player), "the Capital's cell is yours whole");
        int k = Enumerable.Range(1, 6).First(i => (capital.microBlockedMask & (1 << i)) == 0);
        Assert.AreEqual(WorldAuthority.Player, WorldHoldings.TakeHex(map, Hex(capital, k), Rival));
        WorldCivilization.Rebuild(map, settings);
        Assert.AreEqual(open - 1, WorldHoldings.Hexes(map, capital, WorldAuthority.Player));
        Assert.AreEqual(Rival, WorldHoldings.HexHolder(map, Hex(capital, k)));
        Assert.AreEqual(CasusBelli.Recover, WorldHoldings.CasusBelliOf(WorldHoldings.Dispute(map, capital), WorldAuthority.Player), "your settlement's ground is core");
    }
}
