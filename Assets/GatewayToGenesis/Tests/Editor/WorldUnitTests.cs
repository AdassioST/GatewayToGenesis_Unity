using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Units on the map with no scene (<see cref="WorldUnits"/>, <see cref="MicroNavigation"/>, <see cref="UnitAbilities"/>):
/// the micro travel grid (seven hexes per cell, one neighbour pattern), orders at the micro and meso scales (the exact
/// hex, the cell's centre, the nearest hex that can be reached), what a step costs (ground, crags, fords, roads,
/// leylines, slopes), stamina spent as travel fatigue per Seventh, smooth positions between hexes, rations, fatigue
/// and attrition, camp, surveys hex by hex and of a whole meso hex, sight, and builders' improvements.
/// </summary>
public class WorldUnitTests
{
    private static WorldGenSettings Settings()
    {
        var s = new WorldGenSettings();
        s.terrains.Add(new TerrainSpec { id = "plain", name = "Plain", moveCost = 1f, fertility = 0.5f, yields = { new ResourceAmount { resource = "Food", amount = 0.1f } } });
        s.terrains.Add(new TerrainSpec { id = "bog", name = "Bog", moveCost = 2f });
        s.terrains.Add(new TerrainSpec { id = "peaks", name = "Peaks", passable = false });
        s.terrains.Add(new TerrainSpec { id = "lake", name = "Lake", passable = false, water = true });
        return s;
    }

    // Meso cells within radius of the origin (plain unless named), flat, explored; the capital far outside.
    private static WorldMap Disk(int radius, Dictionary<HexCoord, string> ground = null)
    {
        var map = new WorldMap(1) { Capital = new HexCoord(999, 999) };
        foreach (var coord in HexCoord.Spiral(HexCoord.Zero, radius))
        {
            HexHierarchy.ToWorld(coord, HexHierarchy.Meso, out float x, out float y);
            string terrain = ground != null && ground.TryGetValue(coord, out var g) ? g : "plain";
            map.Add(new WorldTile { coord = coord, x = x, y = y, terrain = terrain, impassable = terrain == "peaks", water = terrain == "lake", explored = true, revealed = true, known = true, landFertility = 0.5f });
        }
        return map;
    }

    // A straight line of cells (q = 0..n-1); the given cells get other ground.
    private static WorldMap Line(int n, Dictionary<int, string> ground = null)
    {
        var map = new WorldMap(1) { Capital = new HexCoord(999, 999) };
        for (int q = 0; q < n; q++)
        {
            HexHierarchy.ToWorld(new HexCoord(q, 0), HexHierarchy.Meso, out float x, out float y);
            string terrain = ground != null && ground.TryGetValue(q, out var g) ? g : "plain";
            map.Add(new WorldTile { coord = new HexCoord(q, 0), x = x, y = y, terrain = terrain, impassable = terrain == "peaks", explored = true, revealed = true, known = true });
        }
        return map;
    }

    private static readonly UnitSpec Scout = new UnitSpec
    {
        id = "scout", name = "Scout", role = UnitRole.Scout, stamina = 6f, sight = 5, knowRadius = 1, surveyRadius = 1, surveySevenths = 1f,
        abilities = UnitAbility.Survey | UnitAbility.SurveyMeso | UnitAbility.Forage | UnitAbility.AutoExplore,
    };

    private static WorldUnit At(HexCoord meso) => new WorldUnit { coord = meso };

    // ===== THE GRID =====

    [Test]
    public void EveryCellOwnsSevenMicroHexesAndTheirNeighboursFollowOnePattern()
    {
        var map = Disk(3);
        for (int id = 0; id < map.Count * MicroNavigation.PerCell; id++)
        {
            var hex = MicroNavigation.Coord(map, id);
            Assert.AreEqual(id, MicroNavigation.Index(map, hex), $"micro hex {hex} round-trips");
            Assert.AreEqual(map[id / MicroNavigation.PerCell].coord, HexHierarchy.Parent(hex));
            for (int d = 0; d < 6; d++)
                Assert.AreEqual(MicroNavigation.Index(map, hex.Neighbor(d)), MicroNavigation.Neighbour(map, id, d), $"neighbour {d} of {hex}");
        }
        for (int d = 0; d < 6; d++)
        {
            var contacts = MicroNavigation.Contacts(d);
            Assert.IsNotEmpty(contacts, "neighbouring cells always touch");
            foreach (var (mine, theirs) in contacts)
            {
                var a = HexHierarchy.ChildCenter(HexCoord.Zero) + HexHierarchy.ChildOffsets[mine];
                var b = HexHierarchy.ChildCenter(HexCoord.Directions[d]) + HexHierarchy.ChildOffsets[theirs];
                Assert.AreEqual(1, HexCoord.Distance(a, b), "a contact is a step across the border");
            }
        }
    }

    [Test]
    public void AScoutWalksMicroHexByMicroHexSpendingItsStaminaEachSeventh()
    {
        var settings = Settings();
        var map = Disk(6);
        var unit = At(HexCoord.Zero);
        Assert.IsTrue(WorldUnits.Plan(map, settings, unit, new HexCoord(4, 0), out var path, out float fatigue));
        Assert.AreEqual(12, path.Count, "four cells east is twelve micro hexes");
        Assert.AreEqual(12f, fatigue, 1e-4f, "open plain costs 1 a hex");
        Assert.AreEqual(MicroNavigation.Center(new HexCoord(4, 0)), MicroNavigation.Coord(map, path[path.Count - 1]), "a meso order ends at the cell's centre hex");
        WorldUnits.Order(map, unit, path);
        Assert.AreEqual(2f, WorldUnits.SeventhsLeft(map, settings, unit, Scout), 1e-4f, "six hexes a Seventh");
        var half = WorldUnits.Move(map, settings, unit, Scout, 0.5f);
        Assert.AreEqual(3, half.entered.Count, "half a Seventh, three hexes");
        Assert.AreEqual(1, HexCoord.Distance(unit.microCoord, unit.path[0]), "the road ahead starts beside it");
        Assert.AreEqual(HexHierarchy.Parent(unit.microCoord), unit.coord, "its cell follows its hex");
        var before = WorldUnits.Position(map, settings, unit);
        WorldUnits.Move(map, settings, unit, Scout, 0.05f);
        var between = WorldUnits.Position(map, settings, unit);
        Assert.AreNotEqual(before, between, "drawn between hexes while walking");
        var rest = WorldUnits.Move(map, settings, unit, Scout, 10f);
        Assert.IsTrue(rest.arrived);
        Assert.AreEqual(new HexCoord(4, 0), unit.coord);
        Assert.AreEqual(MicroNavigation.Center(new HexCoord(4, 0)), unit.microCoord);
        Assert.IsFalse(unit.Moving);
    }

    // A unit caught part-way into its next hex (a share t of the step walked) and asked to go to one of three hexes
    // around: its position never jumps; straight on keeps the step walked; an about-face first walks back as far as it
    // came; a turn to one side walks on when it is nearly there and turns back when it has barely left.
    [TestCase(0.9f)]
    [TestCase(0.1f)]
    public void AReorderMidStepNeverTeleportsAndAnAboutFaceCostsTheGroundCovered(float t)
    {
        var settings = Settings();
        var map = Disk(3);
        var start = MicroNavigation.Center(HexCoord.Zero);
        var ahead = start + HexCoord.Directions[0];
        var aside = start + HexCoord.Directions[1];   // beside both the hex it left and the one it steps into
        var behind = start - HexCoord.Directions[0];
        Assert.AreEqual(1, HexCoord.Distance(aside, ahead));

        WorldUnit Walking()
        {
            var unit = At(HexCoord.Zero);
            WorldUnits.Initialize(unit, Scout);
            WorldUnits.Order(map, unit, new List<int> { MicroNavigation.Index(map, ahead) });
            WorldUnits.Move(map, settings, unit, Scout, t / Scout.stamina);
            Assert.AreEqual(t, unit.progress, 1e-4f, "part-way into the step (plain costs 1)");
            return unit;
        }
        WorldUnits.Planner To(HexCoord target) => (HexCoord from, out List<int> p, out float f, out HexCoord r) => MicroNavigation.ToNearest(map, settings, from, target, out p, out f, out r);
        void Reorder(WorldUnit unit, HexCoord target, out float fatigue)
        {
            var was = WorldUnits.Position(map, settings, unit);
            Assert.IsTrue(WorldUnits.PlanFromStep(map, settings, unit, To(target), out var path, out fatigue, out _));
            WorldUnits.Order(map, unit, path);
            var now = WorldUnits.Position(map, settings, unit);
            Assert.AreEqual(was.x, now.x, 1e-4f, "no jump");
            Assert.AreEqual(was.y, now.y, 1e-4f, "no jump");
            Assert.AreEqual(fatigue, WorldUnits.FatigueLeft(map, settings, unit), 1e-4f, "the plan's fatigue is what is left to walk");
        }

        var on = Walking();
        Reorder(on, ahead + HexCoord.Directions[0], out float straight);
        Assert.AreEqual(2f - t, straight, 1e-4f, "straight on, the step walked counts");
        Assert.AreEqual(ahead, on.path[0]);

        var back = Walking();
        Reorder(back, behind, out float aboutFace);
        Assert.AreEqual(t + 1f, aboutFace, 1e-4f, "an about-face walks back first");
        Assert.AreEqual(start, back.path[0], "it returns to the hex it left");
        WorldUnits.Move(map, settings, back, Scout, 10f);
        Assert.AreEqual(behind, back.microCoord);

        var side = Walking();
        Reorder(side, aside, out float turn);
        Assert.AreEqual(1f + Nearer(t), turn, 1e-4f, "a turn aside costs the shorter of walking on and turning back");
        Assert.AreEqual(t > 0.5f ? ahead : start, side.path[0]);
        Assert.Less(turn, aboutFace + 1e-4f, "a turn aside is never dearer than an about-face");
    }

    private static float Nearer(float t) => System.Math.Min(t, 1f - t);

    [Test]
    public void MicroOrdersGoToTheExactHexAndBlockedTargetsToTheNearestThatCanBeReached()
    {
        var settings = Settings();
        var map = Disk(4, new Dictionary<HexCoord, string> { { new HexCoord(2, 0), "lake" } });
        var unit = At(HexCoord.Zero);
        var rim = MicroNavigation.Center(new HexCoord(-2, 1)) + HexHierarchy.ChildOffsets[3];
        Assert.IsTrue(WorldUnits.PlanMicro(map, settings, unit, rim, out var path, out _));
        Assert.AreEqual(rim, MicroNavigation.Coord(map, path.Last()), "a micro order ends on the hex itself");

        // A lake: the meso order stops on the nearest shore hex, one step from the water's centre at most two away.
        Assert.IsTrue(MicroNavigation.ToMeso(map, settings, WorldUnits.MicroPosition(unit), new HexCoord(2, 0), out var shore, out _, out var reached));
        Assert.AreNotEqual(new HexCoord(2, 0), HexHierarchy.Parent(reached), "it does not walk into the lake");
        Assert.AreEqual(2, HexCoord.Distance(reached, MicroNavigation.Center(new HexCoord(2, 0))), "the nearest ground to the lake's heart");
        Assert.IsTrue(MicroNavigation.Enterable(map, settings, reached));

        // The centre hex of a cell barred by hand: the nearest ring around it, never the barred hex.
        var target = new HexCoord(-3, 1);
        map.Get(target).microBlockedMask = 1;
        MicroNavigation.Invalidate(map);
        Assert.IsTrue(MicroNavigation.ToMeso(map, settings, WorldUnits.MicroPosition(unit), target, out _, out _, out var beside));
        Assert.AreEqual(1, HexCoord.Distance(beside, MicroNavigation.Center(target)));
        Assert.IsFalse(MicroNavigation.ToMeso(map, settings, WorldUnits.MicroPosition(unit), new HexCoord(40, 0), out _, out _, out _), "no way beyond the edge of the world");
    }

    [Test]
    public void CragsAndWaterAreWalkedAroundAndTheWayIsTheCheapest()
    {
        var settings = Settings();
        var map = Disk(4);
        var grid = MicroNavigation.Grid(map, settings);
        var from = MicroNavigation.Center(new HexCoord(-2, 0));
        var to = MicroNavigation.Center(new HexCoord(2, 0));
        Assert.IsTrue(MicroNavigation.Find(map, settings, from, to, out var straight, out float open));
        // Crags on the straight way make it wind around them.
        foreach (int id in straight.Take(straight.Count - 1))
        {
            var cell = map[id / MicroNavigation.PerCell];
            if (id % MicroNavigation.PerCell != 0) cell.microBlockedMask |= 1 << (id % MicroNavigation.PerCell);
        }
        MicroNavigation.Invalidate(map);
        Assert.IsTrue(MicroNavigation.Find(map, settings, from, to, out var winding, out float around));
        Assert.Greater(around, open, "the way round the crags is longer");
        Assert.IsTrue(winding.All(id => (map[id / MicroNavigation.PerCell].microBlockedMask & (1 << (id % MicroNavigation.PerCell))) == 0), "no step onto a crag");
        for (int i = 1; i < winding.Count; i++) Assert.AreEqual(1, HexCoord.Distance(MicroNavigation.Coord(map, winding[i - 1]), MicroNavigation.Coord(map, winding[i])), "a path steps from neighbour to neighbour");
        // The search is exact: the same cost as a plain Dijkstra over the whole grid.
        grid = MicroNavigation.Grid(map, settings);
        int start = MicroNavigation.Index(map, from), goal = MicroNavigation.Index(map, to);
        Assert.IsTrue(grid.Search(start, id => id == goal, to, -1, MicroNavigation.MaxPlan, int.MaxValue, out _, out float dijkstra));
        Assert.AreEqual(dijkstra, around, 1e-3f, "A* finds the cheapest way");
    }

    [Test]
    public void GroundLeylinesRoadsFordsAndSlopesSetWhatEachStepCosts()
    {
        var settings = Settings();
        var bog = Disk(4, HexCoord.Spiral(HexCoord.Zero, 4).ToDictionary(c => c, _ => "bog"));
        var from = MicroNavigation.Center(HexCoord.Zero);
        var to = MicroNavigation.Center(new HexCoord(2, 0));
        MicroNavigation.Find(bog, settings, from, to, out _, out float onBog);
        Assert.AreEqual(12f, onBog, 1e-3f, "six bog hexes cost twice as much as plain");
        foreach (var t in bog.Tiles) t.leylines = 1;
        MicroNavigation.Find(bog, settings, from, to, out _, out float onLeyline);
        Assert.AreEqual(onBog * WorldPaths.LeylineFactor, onLeyline, 1e-3f, "leylines lighten the way");

        // A road is laid hex by hex along its line: walking it is lighter still.
        var map = Disk(4);
        map.Routes.Add(new TradeRoute { cells = { new HexCoord(-3, 0), new HexCoord(-2, 0), new HexCoord(-1, 0), HexCoord.Zero, new HexCoord(1, 0), new HexCoord(2, 0), new HexCoord(3, 0) } });
        map.CivilizationVersion++;
        MicroNavigation.Find(map, settings, MicroNavigation.Center(new HexCoord(-3, 0)), MicroNavigation.Center(new HexCoord(3, 0)), out var along, out float onRoad);
        Assert.Less(onRoad, along.Count, "a road halves the steps along it");
        var grid = MicroNavigation.Grid(map, settings);
        Assert.IsTrue(along.Count(id => (grid.Marks(id) & MicroGrid.Mark.Road) != 0) >= along.Count - 2, "the way keeps to the road");

        // A river is forded where it is crossed, unless a road bridges it.
        var wet = Disk(4);
        wet.Rivers.Add(new RiverPath { cells = { wet.Get(new HexCoord(0, -3)).index, wet.Get(new HexCoord(0, -2)).index, wet.Get(new HexCoord(0, -1)).index, wet.Get(HexCoord.Zero).index, wet.Get(new HexCoord(0, 1)).index, wet.Get(new HexCoord(0, 2)).index, wet.Get(new HexCoord(0, 3)).index } });
        MicroNavigation.Find(wet, settings, MicroNavigation.Center(new HexCoord(-2, 0)), MicroNavigation.Center(new HexCoord(2, 0)), out var crossing, out float forded);
        var wetGrid = MicroNavigation.Grid(wet, settings);
        Assert.IsTrue(crossing.Any(id => (wetGrid.Marks(id) & (MicroGrid.Mark.Ford | MicroGrid.Mark.GreatFord)) != 0), "a river is a line no step slips past");
        Assert.Greater(forded, 12f, "fording costs more than dry ground");

        // Climbing tires more than going down.
        var hill = Disk(2);
        hill.Get(new HexCoord(1, 0)).elevation = 0.1f;
        var hillGrid = MicroNavigation.Grid(hill, settings);
        int low = -1, high = -1;
        foreach (var (mine, theirs) in MicroNavigation.Contacts(0))
        {
            low = hill.Get(HexCoord.Zero).index * MicroNavigation.PerCell + mine;
            high = hill.Get(new HexCoord(1, 0)).index * MicroNavigation.PerCell + theirs;
            break;
        }
        Assert.Greater(hillGrid.Step(low, high), hillGrid.Step(high, low), "uphill is harder than downhill");
        Assert.Greater(hillGrid.Step(high, low), hillGrid.Enter(low), "a steep descent still costs a little more");
    }

    [Test]
    public void GroundThatTurnedImpassableEndsTheJourney()
    {
        var settings = Settings();
        var walker = At(HexCoord.Zero);
        WorldUnits.Plan(Line(8), settings, walker, new HexCoord(6, 0), out var clear, out _);
        var walled = Line(8, new Dictionary<int, string> { { 4, "peaks" } });
        Assert.IsFalse(WorldUnits.Plan(walled, settings, At(HexCoord.Zero), new HexCoord(6, 0), out _, out _), "no way round a wall in a line");
        WorldUnits.Order(walled, walker, clear);
        var step = WorldUnits.Move(walled, settings, walker, Scout, 10f);
        Assert.IsTrue(step.blocked, "ground that turned impassable ends the journey");
        Assert.AreEqual(new HexCoord(3, 0), walker.coord, "it stops before the wall");
        Assert.IsFalse(walker.Moving);
    }

    // ===== NEEDS =====

    private static UnitSpec Walker() => new UnitSpec
    {
        id = "walker", name = "Walker", stamina = 6f, sight = 5, supplyCapacity = 12f, supplyUsePerSeventh = 1f, fatiguePerTravelCost = 1.2f,
        workFatiguePerSeventh = 4f, campRecoveryPerSeventh = 25f, starvationAttritionPerSeventh = 8f, recoveryPerSeventh = 3f, forageRationsPerSeventh = 1.2f,
    };

    private static UnitSurroundings Wild(float fertility = 0f) => new UnitSurroundings { weather = 1f, fertility = fertility };

    [Test]
    public void RationsAreEatenDrawnInsideYourAuthorityAndGatheredInCamp()
    {
        var spec = Walker();
        var rules = new ProvisionRules();
        var unit = At(HexCoord.Zero);
        WorldUnits.Initialize(unit, spec);
        Assert.AreEqual(12f, unit.supplies, "it sets out with full rations");
        WorldUnits.Needs(unit, spec, rules, Wild(), UnitActivity.Moving, 2f);
        Assert.AreEqual(10f, unit.supplies, 1e-4f, "a ration a Seventh on the march");
        WorldUnits.Needs(unit, spec, rules, new UnitSurroundings { weather = 1.5f }, UnitActivity.Moving, 2f);
        Assert.AreEqual(7.5f, unit.supplies, 1e-4f, "harsh weather eats more");

        // Inside your authority the stores fill it up (and pay for it).
        float paid = 0f;
        var report = WorldUnits.Needs(unit, spec, rules, new UnitSurroundings { weather = 1f, held = true, settlement = true }, UnitActivity.Idle, 1f, value => { paid += value; return value; });
        Assert.AreEqual(12f, unit.supplies, 1e-4f, "a settlement fills its packs");
        Assert.AreEqual(paid, report.drawn * rules.foodValuePerRation, 1e-4f, "every ration drawn is paid in food value");
        float before = unit.supplies = 5f;
        WorldUnits.Needs(unit, spec, rules, new UnitSurroundings { weather = 1f, held = true }, UnitActivity.Idle, 1f, value => 0f);
        Assert.Less(unit.supplies, before, "empty stores feed no one");

        // In camp it eats less and lives off the land by its fertility.
        unit.supplies = 5f;
        WorldUnits.Camp(unit);
        var barren = WorldUnits.Needs(unit, spec, rules, Wild(0f), UnitActivity.Camping, 1f);
        Assert.AreEqual(5f - rules.campRationShare, unit.supplies, 1e-4f, "camp eats less");
        Assert.AreEqual(0f, barren.gathered, 1e-6f, "barren ground gives nothing");
        var fertile = WorldUnits.Needs(unit, spec, rules, Wild(1f), UnitActivity.Camping, 1f);
        Assert.AreEqual(spec.forageRationsPerSeventh, fertile.gathered, 1e-4f, "the richest ground feeds a camp");
    }

    [Test]
    public void HungerStrainWeatherAndDangerWearAUnitDownAndRestWhileFedHealsIt()
    {
        var spec = Walker();
        var rules = new ProvisionRules();
        var unit = At(HexCoord.Zero);
        WorldUnits.Initialize(unit, spec);
        unit.supplies = 0f;
        var hungry = WorldUnits.Needs(unit, spec, rules, Wild(), UnitActivity.Moving, 1f);
        Assert.AreEqual(1f, hungry.hungry, 1e-4f);
        Assert.IsTrue(unit.hungry);
        Assert.AreEqual(spec.starvationAttritionPerSeventh, unit.attrition, 1e-3f, "hunger wears it down");
        float fed = WorldUnits.Efficiency(unit);
        Assert.Less(fed, 1f, "a hungry unit falters");

        var hale = At(HexCoord.Zero);
        WorldUnits.Initialize(hale, spec);
        WorldUnits.Needs(hale, spec, rules, new UnitSurroundings { weather = 1.25f, danger = 0.5f, dissonance = 0.2f }, UnitActivity.Moving, 1f);
        Assert.AreEqual(0.25f * rules.exposureAttrition + 0.5f * rules.dangerAttrition + 0.2f * rules.dissonanceAttrition, hale.attrition, 1e-3f, "weather, danger and dissonance wear it down");
        float worn = hale.attrition;
        WorldUnits.Needs(hale, spec, rules, new UnitSurroundings { weather = 1f, held = true, settlement = true }, UnitActivity.Idle, 1f, v => v);
        Assert.AreEqual(worn - spec.recoveryPerSeventh * 3f, hale.attrition, 1e-3f, "a settlement shelters and heals it");
        WorldUnits.Camp(hale);
        float inCamp = hale.attrition = 20f;
        WorldUnits.Needs(hale, spec, rules, Wild(), UnitActivity.Camping, 1f);
        Assert.Less(hale.attrition, inCamp, "rest in camp while fed heals it");

        var strained = At(HexCoord.Zero);
        WorldUnits.Initialize(strained, spec);
        strained.fatigue = 95f;
        WorldUnits.Needs(strained, spec, rules, Wild(), UnitActivity.Moving, 1f);
        Assert.AreEqual(rules.exhaustionAttrition, strained.attrition, 1e-3f, "marching exhausted wears it down");
    }

    [Test]
    public void TheRoadTiresRestHealsAndWearinessSlowsTheWalk()
    {
        var settings = Settings();
        var spec = Walker();
        var rules = new ProvisionRules();
        var map = Disk(6);
        var fresh = At(HexCoord.Zero);
        WorldUnits.Plan(map, settings, fresh, new HexCoord(4, 0), out var path, out _);
        WorldUnits.Order(map, fresh, path);
        var step = WorldUnits.Move(map, settings, fresh, spec, 1f);
        Assert.AreEqual(6, step.entered.Count);
        Assert.AreEqual(6f * spec.fatiguePerTravelCost, fresh.fatigue, 1e-3f, "fatigue by what it walked");

        var weary = At(HexCoord.Zero);
        WorldUnits.Initialize(weary, spec);
        weary.fatigue = 100f;
        Assert.AreEqual(0.5f, WorldUnits.Efficiency(weary), 1e-4f, "spent, it walks at half its pace");
        WorldUnits.Plan(map, settings, weary, new HexCoord(4, 0), out var same, out _);
        WorldUnits.Order(map, weary, same);
        Assert.AreEqual(3, WorldUnits.Move(map, settings, weary, spec, 1f).entered.Count, "three hexes a Seventh instead of six");

        weary.fatigue = 60f;
        WorldUnits.Needs(weary, spec, rules, Wild(), UnitActivity.Idle, 1f);
        Assert.AreEqual(60f - spec.campRecoveryPerSeventh * rules.idleRecoveryShare, weary.fatigue, 1e-3f, "standing rests a little");
        WorldUnits.Camp(weary);
        Assert.IsTrue(weary.Camping);
        Assert.IsFalse(weary.Moving, "making camp by hand ends the journey");
        WorldUnits.Needs(weary, spec, rules, Wild(), UnitActivity.Camping, 1f);
        Assert.Less(weary.fatigue, 60f - spec.campRecoveryPerSeventh * rules.idleRecoveryShare - 10f, "camp rests far better");
        var work = At(HexCoord.Zero);
        WorldUnits.Initialize(work, spec);
        WorldUnits.Needs(work, spec, rules, Wild(), UnitActivity.Working, 1f, null, 1.5f);
        Assert.AreEqual(spec.workFatiguePerSeventh * 1.5f, work.fatigue, 1e-3f, "work tires by its toll");

        // A camp it made by itself keeps its road.
        var resting = At(HexCoord.Zero);
        WorldUnits.Plan(map, settings, resting, new HexCoord(4, 0), out var road, out _);
        WorldUnits.Order(map, resting, road);
        WorldUnits.Camp(resting, resting: true);
        Assert.IsTrue(resting.Moving && resting.Camping && resting.resting);
        WorldUnits.BreakCamp(resting);
        Assert.IsTrue(resting.Moving && !resting.Camping, "it walks on once rested");
    }

    [Test]
    public void AJourneyTellsItsRationsAndSevenths()
    {
        var spec = Walker();
        var unit = At(HexCoord.Zero);
        WorldUnits.Initialize(unit, spec);
        Assert.AreEqual(2f, WorldUnits.SeventhsFor(unit, spec, 12f), 1e-4f);
        Assert.AreEqual(2f, WorldUnits.RationsFor(unit, spec, 12f), 1e-4f, "a ration a Seventh");
        Assert.AreEqual(12f, WorldUnits.RationSevenths(unit, spec), 1e-4f);
        Assert.AreEqual("fresh", WorldUnits.FatigueWord(0f));
        Assert.AreEqual("exhausted", WorldUnits.FatigueWord(100f));
        Assert.AreEqual("failing", WorldUnits.AttritionWord(90f));
    }

    // ===== ABILITIES =====

    [Test]
    public void SurveysWorkHexByHexAndEveryHexSurveyedExploresTheCell()
    {
        var settings = Settings();
        var map = Disk(3);
        foreach (var t in map.Tiles) t.explored = false;
        var unit = At(HexCoord.Zero);
        WorldUnits.Initialize(unit, Scout);

        // At the heart of a cell, the seven around it are the whole cell.
        var around = UnitAbilities.Targets(map, settings, unit, Scout, UnitAbilities.Survey);
        Assert.AreEqual(7, around.Count);
        Assert.IsNull(WorldUnits.WhyNotSurvey(map, settings, unit, Scout, UnitAbilities.Survey));
        bool explored = false;
        foreach (var c in around) explored |= map.SurveyMicro(c);
        Assert.IsTrue(explored && map.Get(HexCoord.Zero).explored, "every hex surveyed explores the cell");
        Assert.AreEqual("Already surveyed.", WorldUnits.WhyNotSurvey(map, settings, unit, Scout, UnitAbilities.Survey));

        // On a cell's rim the survey reaches into its neighbours; a cell needs all its hexes.
        var rim = At(new HexCoord(1, 0));
        WorldUnits.Place(rim, MicroNavigation.Center(new HexCoord(1, 0)) + HexHierarchy.ChildOffsets[1]);
        var reach = UnitAbilities.Targets(map, settings, rim, Scout, UnitAbilities.Survey);
        Assert.Greater(reach.Select(c => HexHierarchy.Parent(c)).Distinct().Count(), 1, "the hexes around a rim hex lie in more than one cell");
        foreach (var c in reach) map.SurveyMicro(c);
        Assert.IsFalse(map.Get(new HexCoord(1, 0)).explored, "part of a cell is not all of it");
        Assert.Greater(WorldMap.UnsurveyedHexes(map.Get(new HexCoord(1, 0))), 0);

        // The whole meso hex from anywhere in it; quicker when part of it is done. Crags need no surveying.
        var sweep = UnitAbilities.Targets(map, settings, rim, Scout, UnitAbilities.SurveyMeso);
        Assert.IsTrue(sweep.All(c => HexHierarchy.Parent(c) == new HexCoord(1, 0)), "a meso survey keeps to its cell");
        Assert.AreEqual(WorldMap.UnsurveyedHexes(map.Get(new HexCoord(1, 0))), sweep.Count);
        float full = UnitAbilities.Duration(map, settings, At(new HexCoord(-1, 0)), Scout, UnitAbilities.SurveyMeso);
        float part = UnitAbilities.Duration(map, settings, rim, Scout, UnitAbilities.SurveyMeso);
        Assert.AreEqual(Scout.mesoSurveySevenths, full, 1e-4f);
        Assert.Less(part, full, "the part already surveyed is not surveyed again");
        map.Get(new HexCoord(1, 0)).microBlockedMask = 1 << 3;
        MicroNavigation.Invalidate(map);
        bool done = false;
        foreach (var c in UnitAbilities.Targets(map, settings, rim, Scout, UnitAbilities.SurveyMeso)) done |= map.SurveyMicro(c);
        Assert.IsTrue(done, "the crag aside, every hex was surveyed");

        Assert.AreEqual(AbilityReach.Meso, UnitAbilities.For(UnitTask.SurveyMeso).reach);
        Assert.AreSame(UnitAbilities.Survey, UnitAbilities.For(UnitAbility.Survey));
        Assert.AreEqual("It cannot survey.", WorldUnits.WhyNotSurvey(map, settings, unit, new UnitSpec(), UnitAbilities.SurveyMeso));
    }

    [Test]
    public void ScoutsMakeTheHexesAroundThemKnownAndExplorersRangeOutToUnknownCells()
    {
        var settings = Settings();
        var map = Disk(4);
        foreach (var t in map.Tiles) t.explored = t.known = false;
        var unit = At(HexCoord.Zero);
        WorldUnits.Initialize(unit, Scout);
        foreach (var c in MicroNavigation.Area(map, unit.microCoord, AbilityReach.Around, 1)) map.KnowMicro(c);
        Assert.IsTrue(map.Get(HexCoord.Zero).known, "any hex known makes its cell known");
        Assert.IsTrue(MicroNavigation.Known(map, unit.microCoord) && !MicroNavigation.Surveyed(map, unit.microCoord), "walked is not surveyed");
        Assert.IsTrue(WorldUnits.NextToScout(map, settings, unit, 20f, out var next));
        Assert.IsFalse(map.Get(HexHierarchy.Parent(next)).known, "it heads for a cell no unit has passed over");
        Assert.AreEqual(2, HexCoord.Distance(next, unit.microCoord), "the nearest such ground");
        foreach (var t in map.Tiles) t.known = true;
        Assert.IsFalse(WorldUnits.NextToScout(map, settings, unit, 20f, out _), "nothing left to explore in reach");
    }

    [Test]
    public void SightReachesFartherFromLeylinesAndRidgesAndNarrowsWhenSpent()
    {
        var map = Line(3);
        var rules = new SettlementRules();
        var unit = At(new HexCoord(1, 0));
        WorldUnits.Initialize(unit, Scout);
        Assert.AreEqual(Scout.sight, WorldUnits.Sight(map, unit, Scout, rules));
        map.Get(unit.coord).leylines = 1;
        Assert.AreEqual(Scout.sight + rules.leylineRevealBonus, WorldUnits.Sight(map, unit, Scout, rules));
        map.Get(unit.coord).leylines = 0;
        map.Get(unit.coord).landform = "High ridge";
        Assert.AreEqual(Scout.sight + 2, WorldUnits.Sight(map, unit, Scout, rules), "a ridge is a vantage point");
        unit.fatigue = 100f;
        unit.attrition = 90f;
        Assert.AreEqual(Scout.sight, WorldUnits.Sight(map, unit, Scout, rules), "a spent unit sees less");
    }

    [Test]
    public void BuildersImproveHeldHotspotsAndTheYieldGrows()
    {
        var settings = Settings();
        var rules = new SettlementRules();
        var map = Line(4);
        var cell = map.Get(new HexCoord(1, 0));
        StringAssert.StartsWith("Hold it first", WorldUnits.WhyNotImprove(map, settings, rules, cell));
        Assert.IsEmpty(WorldUnits.LandYields(map, settings, rules), "unheld ground yields nothing");
        foreach (var t in map.Tiles) t.authorityId = WorldAuthority.Player;
        Assert.IsNull(WorldUnits.WhyNotImprove(map, settings, rules, cell));
        float before = WorldUnits.LandYields(map, settings, rules).Where(y => y.resource == "Food").Sum(y => y.amount);
        Assert.AreEqual(0.4f, before, 1e-4f);
        cell.improvement = 2;
        float after = WorldUnits.LandYields(map, settings, rules).Where(y => y.resource == "Food").Sum(y => y.amount);
        Assert.AreEqual(before + 0.1f * rules.improvementBonus * 2, after, 1e-4f, "each level adds half the hotspot's yield");
        cell.improvement = rules.maxImprovement;
        Assert.AreEqual("Improved as far as it goes.", WorldUnits.WhyNotImprove(map, settings, rules, cell));
        cell.explored = false;
        Assert.IsFalse(WorldUnits.IsHotspot(map, settings, cell), "only known ground is a hotspot");
    }

    // ===== A GENERATED WORLD =====

    [Test]
    public void GeneratedWorldsKeepWalkableNeighboursJoinedAndPlanLongWaysQuickly()
    {
        var settings = WorldGenerationTests.Settings();
        var map = WorldGenerator.Generate(7, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());
        int crags = map.Tiles.Sum(t => MicroNavigation.Crags(t.microBlockedMask));
        Assert.Greater(crags, 0, "steep ground breaks into crags");
        Assert.IsTrue(map.Tiles.All(t => (t.microBlockedMask & 1) == 0), "never a cell's centre");
        foreach (var t in map.Tiles.Where(t => MicroNavigation.Walkable(t, settings)))
            for (int d = 0; d < 6; d++)
            {
                int n = map.Neighbour(t.index, d);
                if (n < 0 || !MicroNavigation.Walkable(map[n], settings)) continue;
                Assert.IsTrue(MicroNavigation.Contacts(d).Any(c => (t.microBlockedMask & (1 << c.mine)) == 0 && (map[n].microBlockedMask & (1 << c.theirs)) == 0),
                    $"{t.coord} and {map[n].coord} keep a crossing");
            }

        var watch = Stopwatch.StartNew();
        var grid = MicroNavigation.Grid(map, settings);
        long build = watch.ElapsedMilliseconds;
        var capital = MicroNavigation.Center(map.Capital);
        var far = map.Tiles.Where(t => MicroNavigation.Walkable(t, settings)).OrderByDescending(t => HexCoord.Distance(t.coord, map.Capital))
            .Select(t => MicroNavigation.Center(t.coord)).First(c => grid.SameGround(MicroNavigation.Index(map, capital), MicroNavigation.Index(map, c)));
        watch.Restart();
        Assert.IsTrue(MicroNavigation.Find(map, settings, capital, far, out var path, out float fatigue), "the farthest reachable ground");
        long search = watch.ElapsedMilliseconds;
        Assert.Greater(path.Count, 60);
        Assert.Less(build + search, 5000, $"grid {build} ms, search {search} ms over {path.Count} hexes");
        var cost = 0f;
        int at = MicroNavigation.Index(map, capital);
        foreach (int id in path)
        {
            cost += grid.Step(at, id);
            at = id;
        }
        Assert.AreEqual(fatigue, cost, fatigue * 1e-4f, "the fatigue told is the sum of its steps");
    }
    [Test]
    public void Surveys_ACellIsSeenHexByHexAndASurveyVisitsEachHex()
    {
        var settings = Settings();
        var map = Disk(2);
        foreach (var t in map.Tiles) t.explored = t.known = false;
        var cell = map.Get(new HexCoord(1, 0));
        Assert.AreEqual(7, WorldMap.OpenHexes(cell));
        Assert.IsFalse(WorldMap.FullySeen(cell));

        // Walking through the heart with a sight of one hex sees all seven.
        foreach (var c in MicroNavigation.Area(map, MicroNavigation.Center(cell.coord), AbilityReach.Around, 1)) map.KnowMicro(c);
        Assert.IsTrue(WorldMap.FullySeen(cell), "the heart and the six around it are the whole cell");
        Assert.AreEqual(7, WorldMap.SeenHexes(cell));
        Assert.AreEqual(0, WorldMap.SurveyedHexes(cell), "seen is not surveyed");

        // A survey still has every hex to visit, even once the cell is explored in passing; crags need no visit.
        map.Explore(cell.coord);
        Assert.AreEqual(7, UnitAbilities.CellTargets(map, settings, cell).Count, "explored in passing, not surveyed");
        cell.microBlockedMask = 1 << 2;
        MicroNavigation.Invalidate(map);
        var left = UnitAbilities.CellTargets(map, settings, cell);
        Assert.AreEqual(6, left.Count);
        Assert.IsTrue(left.All(id => id / MicroNavigation.PerCell == cell.index), "a survey keeps to its cell");
        map.SurveyMicro(MicroNavigation.Coord(map, left[0]));
        Assert.AreEqual(5, UnitAbilities.CellTargets(map, settings, cell).Count);
        Assert.AreEqual(Scout.mesoSurveySevenths / 7f, UnitAbilities.HexSevenths(Scout), 1e-5f, "each hex its share of the whole survey");
    }

    // ===== ROUTES: TOURS, OTHERS IN THE WAY, MANY TARGETS AT ONCE =====

    [Test]
    public void ASurveyTourWalksTheCellsHexesInTheOrderThatWalksLeast()
    {
        var settings = Settings();
        // Uneven ground around the cell surveyed, so the order matters, and a crag inside it.
        var map = Disk(3, new Dictionary<HexCoord, string> { { new HexCoord(2, -1), "bog" }, { new HexCoord(1, 1), "bog" }, { new HexCoord(0, 1), "bog" } });
        var cell = map.Get(new HexCoord(1, 0));
        cell.terrain = "bog";
        cell.microBlockedMask = 1 << 4;
        MicroNavigation.Invalidate(map);
        var grid = MicroNavigation.Grid(map, settings);
        var targets = UnitAbilities.CellTargets(map, settings, cell);
        Assert.AreEqual(6, targets.Count, "the crag is not surveyed");
        var from = MicroNavigation.Center(new HexCoord(-2, 1));
        Assert.IsTrue(MicroNavigation.NextOnTour(map, settings, from, targets, cell.coord, out var path, out float first, out float tour));
        Assert.IsTrue(targets.Contains(path.Last()), "the way ends on a hex to survey");

        // Every order of the six, weighed by the true walking costs: none walks less than the tour chosen.
        float Walk(HexCoord a, int b) => MicroNavigation.Find(map, settings, a, MicroNavigation.Coord(map, b), out _, out float c) ? c : float.PositiveInfinity;
        var toward = targets.ToDictionary(t => t, t => Walk(from, t));
        var between = targets.ToDictionary(t => t, t => targets.ToDictionary(u => u, u => t == u ? 0f : Walk(MicroNavigation.Coord(map, t), u)));
        float bestTour = float.PositiveInfinity, bestFirst = 0f;
        foreach (var order in Permutations(targets))
        {
            float c = toward[order[0]];
            for (int i = 1; i < order.Count; i++) c += between[order[i - 1]][order[i]];
            if (c < bestTour - 1e-4f) { bestTour = c; bestFirst = toward[order[0]]; }
        }
        Assert.AreEqual(bestTour, tour, 1e-3f, "the tour is the cheapest of all orders");
        Assert.AreEqual(toward[path.Last()], first, 1e-3f, "and the way to its first hex is the cheapest way there");
        Assert.AreEqual(first, path.Aggregate((at: MicroNavigation.Index(map, from), sum: 0f), (s, id) => (id, s.sum + grid.Step(s.at, id))).sum, 1e-3f);

        // The whole tour, hex by hex (the plan the map numbers): every target once, from the first hex walked to, and
        // its walk is the tour's cost.
        var plan = new List<int>();
        Assert.IsTrue(MicroNavigation.NextOnTour(map, settings, from, targets, cell.coord, out _, out _, out _, order: plan));
        CollectionAssert.AreEquivalent(targets, plan);
        Assert.AreEqual(path.Last(), plan[0], "it starts where the way leads");
        float planned = toward[plan[0]];
        for (int i = 1; i < plan.Count; i++) planned += between[plan[i - 1]][plan[i]];
        Assert.AreEqual(tour, planned, 1e-3f, "the order walked is the cheapest tour");

        // Standing on a hex still to survey, that hex comes first (no walk).
        Assert.IsTrue(MicroNavigation.NextOnTour(map, settings, MicroNavigation.Coord(map, targets[2]), targets, cell.coord, out var none, out float zero, out _, order: plan));
        Assert.AreEqual(0, none.Count);
        Assert.AreEqual(0f, zero, 1e-6f);
        Assert.AreEqual(targets[2], plan[0], "the plan begins on the hex it stands on");
    }

    private static IEnumerable<List<int>> Permutations(List<int> items)
    {
        if (items.Count <= 1) { yield return new List<int>(items); yield break; }
        for (int i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, k) => k != i).ToList();
            foreach (var tail in Permutations(rest))
            {
                tail.Insert(0, items[i]);
                yield return tail;
            }
        }
    }

    [Test]
    public void WaysGoAroundHexesOthersHoldAndOneSearchPricesManyTargets()
    {
        var settings = Settings();
        var map = Disk(4);
        var grid = MicroNavigation.Grid(map, settings);
        var from = MicroNavigation.Center(new HexCoord(-2, 0));
        var to = MicroNavigation.Center(new HexCoord(2, 0));
        Assert.IsTrue(MicroNavigation.Find(map, settings, from, to, out var straight, out float open));
        // Others stand on the middle of the straight way: the way goes round them, a little longer.
        var held = new HashSet<int>(straight.Take(straight.Count - 1).Skip(straight.Count / 2 - 1).Take(2));
        int start = MicroNavigation.Index(map, from), goal = MicroNavigation.Index(map, to);
        Assert.IsTrue(grid.Search(start, id => id == goal, to, 0, MicroNavigation.MaxPlan, int.MaxValue, out var around, out float detour, held.Contains));
        Assert.IsFalse(around.Any(held.Contains), "never through a hex others hold");
        Assert.Greater(detour, open - 1e-4f);
        Assert.Less(detour, open * 1.5f, "a short way round");

        // A target others stand on: the nearest hex beside it that is free.
        var guarded = new HashSet<int> { goal };
        Assert.IsTrue(MicroNavigation.ToNearest(map, settings, from, to, out var beside, out _, out var reached, avoid: guarded.Contains));
        Assert.AreEqual(1, HexCoord.Distance(reached, to));
        Assert.IsFalse(beside.Contains(goal));

        // Many targets priced in one search: each the same as its own search.
        var targets = new List<int> { goal, MicroNavigation.Index(map, MicroNavigation.Center(new HexCoord(0, 2))), MicroNavigation.Index(map, MicroNavigation.Center(new HexCoord(-1, -2))) };
        var costs = new float[targets.Count];
        Assert.AreEqual(3, grid.CostsTo(start, targets, HexCoord.Zero, 20, MicroNavigation.MaxPlan, int.MaxValue, costs));
        for (int i = 0; i < targets.Count; i++)
        {
            Assert.IsTrue(MicroNavigation.Find(map, settings, from, MicroNavigation.Coord(map, targets[i]), out _, out float single));
            Assert.AreEqual(single, costs[i], 1e-3f);
        }
    }
}
