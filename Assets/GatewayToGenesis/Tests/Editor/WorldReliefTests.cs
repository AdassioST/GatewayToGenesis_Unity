using System;
using System.Linq;
using NUnit.Framework;

public class WorldReliefTests
{
    [TestCase(7)]
    [TestCase(42)]
    [TestCase(113)]
    public void TerracesAndBasinsPreserveDrainageAndCapital(int seed)
    {
        var settings = WorldGenerationTests.Settings();
        var start = settings.Biome("start");
        start.terraces = 0.9f;
        start.elevation = 0.49f;
        start.relief = 0.18f;
        start.tilt = 0.16f;
        settings.naturalBasins = 20;
        var map = WorldGenerator.Generate(seed, settings, WorldGenerationTests.ParsedStencil(), WorldGenerationTests.Tiles());
        CollectionAssert.IsEmpty(map.Report.errors);
        Assert.AreEqual("S1", map.Get(map.Capital).sector);
        Assert.IsFalse(map.Get(map.Capital).water);
        Assert.Greater(map.Tiles.Where(t => t.sector == "S1").Max(t => t.elevation) - map.Tiles.Where(t => t.sector == "S1").Min(t => t.elevation), 0.04f);
        foreach (var tile in map.Tiles)
        {
            Assert.IsFalse(float.IsNaN(tile.elevation));
            Assert.GreaterOrEqual(tile.waterDepth, 0f);
            Assert.IsNotEmpty(tile.landform);
            if (tile.downstream >= 0 && (!tile.water || tile.lake))
                Assert.LessOrEqual(map[tile.downstream].elevation, tile.elevation + 0.0001f);
        }
    }

    [Test]
    public void TerraceSettingChangesCatalogFingerprint()
    {
        var settings = WorldGenerationTests.Settings();
        string before = WorldGenerator.CatalogHash(settings, WorldGenerationTests.Tiles());
        settings.Biome("start").terraces = 0.9f;
        Assert.AreNotEqual(before, WorldGenerator.CatalogHash(settings, WorldGenerationTests.Tiles()));
    }

    [Test]
    public void WeatherFootprintsCoverHexRegionsSectorsAndWorld()
    {
        var front = new WorldWeatherFront { center = HexCoord.Zero, radius = 3 };
        Assert.IsTrue(front.Covers(new WorldTile { coord = new HexCoord(3, 0) }));
        Assert.IsFalse(front.Covers(new WorldTile { coord = new HexCoord(4, 0) }));
        front.extent = WeatherExtent.Sectors;
        front.sectors.AddRange(new[] { "S1", "S3" });
        Assert.IsTrue(front.Covers(new WorldTile { sector = "s3" }));
        Assert.IsFalse(front.Covers(new WorldTile { sector = "S2" }));
        front.extent = WeatherExtent.World;
        Assert.IsTrue(front.Covers(new WorldTile { coord = new HexCoord(999, -450) }));
        Assert.IsFalse(front.Covers(null));
    }

    [Test]
    public void LocalWeatherAffectsTravelWithoutOpeningImpassableGround()
    {
        var tile = new WorldTile();
        float baseline = WorldPaths.StepCost(tile, null);
        tile.weatherTravelMultiplier = 1.25f;
        Assert.AreEqual(baseline * 1.25f, WorldPaths.StepCost(tile, null), 0.0001f);
        tile.water = true;
        Assert.IsTrue(float.IsPositiveInfinity(WorldPaths.StepCost(tile, null)));
    }
}
