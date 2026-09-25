using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Checks the authored assets under Resources (legends, civics, weather, production units, technologies and
/// the compiled Ink stories) with the same rules the game uses at start-up. A failure lists every problem to fix in the Console.
/// </summary>
public class ContentTests
{
    [SetUp]
    public void ReloadAssets() => GameCatalog.InvalidateAll();

    [Test]
    public void AllContentValidates()
    {
        int problems = ContentValidator.ValidateAll(force: true);
        Assert.AreEqual(0, problems, "Content problems were logged as warnings above; fix the named assets.");
    }

    [Test]
    public void CatalogsFindContent()
    {
        Assert.Greater(GameCatalog.Resources.Count, 0, "No resources in Resources/GameUnits/GameResources");
        Assert.Greater(GameCatalog.ProductionUnits.Count, 0, "No production units in Resources/Production");
        Assert.Greater(GameCatalog.Technologies.Count, 0, "No technologies in Resources/Technology");
        Assert.Greater(GameCatalog.Legends.Count, 0, "No legends in Resources/Legends");
    }

    /// <summary>Every compiled story parses, links up, and names only resources, sections, buildings, technologies and weather that exist.</summary>
    [Test]
    public void AllStoriesValidate()
    {
        EventStoryIndex.Clear();
        try
        {
            var volumes = InkDrivenEventSetup.LoadVolumes();
            Assert.Greater(volumes.Count, 0, "No compiled Ink stories with events in Resources/Events");
            var problems = new List<string>(EventStoryIndex.Problems);
            problems.AddRange(InkDrivenEventSetup.Validate(volumes));
            Assert.IsEmpty(problems, "Story problems:\n" + string.Join("\n", problems));
        }
        finally
        {
            EventStoryIndex.Clear();
        }
    }

    // The catalog skips units without a GameUnit, so check the raw assets.
    [Test]
    public void EveryProductionUnitHasAGameUnit()
    {
        foreach (var unit in UnityEngine.Resources.LoadAll<ProductionUnitData>(GameCatalog.ProductionUnits.ResourcesPath))
        {
            Assert.IsNotNull(unit.gameUnit, $"Production unit asset '{unit.name}' has no GameUnit");
        }
    }
}
