using System.Collections.Generic;
using System.Linq;
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

    /// <summary>The vault's achievement list loads, and every unlock rule names an achievement in it (titles are the keys).</summary>
    [Test]
    public void EveryAchievementTriggerNamesAnAchievement()
    {
        Achievements.Invalidate();
        Assert.Greater(Achievements.Tracker.All.Count, 50, "Resources/Achievements/Achievements.md is missing or unreadable");
        foreach (var id in AchievementTriggers.Rules.Keys) Assert.IsNotNull(Achievements.Tracker.Get(id), $"No achievement '{id}' in the vault note");
    }

    /// <summary>
    /// The vault's Legend Trait and Composure notes load from Resources/Legends, the tuning asset reads back with sane
    /// thresholds, and every legend gets a whole soul: a primary binding, three personality traits and a Wish.
    /// </summary>
    [Test]
    public void EveryLegendHasASoulFromTheVaultNotes()
    {
        LegendLore.Invalidate();
        var traits = LegendLore.Traits;
        Assert.GreaterOrEqual(traits.origins.Count, 10, "Resources/Legends/Legend Trait.md is missing or its origin tables are unreadable");
        Assert.GreaterOrEqual(traits.personalities.Count, 50, "Resources/Legends/Legend Trait.md: too few personality traits");
        Assert.Greater(traits.middleTraits.Count, 20, "Resources/Legends/Legend Trait.md: the middle-trait table is unreadable");
        var composure = LegendLore.Composure;
        Assert.IsEmpty(composure.problems, string.Join("\n", composure.problems));
        Assert.IsNotNull(composure.Emphasis(ComposureState.Spiraling), "the Spiraling notice quotes the vault's bold line");
        Assert.IsNotNull(LegendLore.Settings, "Resources/Legends/LegendSettings.asset is missing");
        var tuning = LegendLore.ComposureTuning;
        Assert.That(tuning.cloudedAt < tuning.baseline && tuning.baseline < tuning.fracturedAt && tuning.fracturedAt < tuning.spiralingAt && tuning.spiralingAt < tuning.surrenderAt,
            "Composure thresholds must rise Pristine < baseline (Clouded) < Fractured < Spiraling < Surrender");
        CollectionAssert.IsEmpty(LegendLore.Problems().ToList());

        foreach (var legend in GameCatalog.Legends.All)
        {
            var soul = LegendSoulRules.Create(LegendSoulRules.Template.Of(legend), traits, LegendLore.SoulTuning, 1234, tuning.baseline);
            Assert.IsNotNull(MagicBindings.Canonical(soul.leitmotif), $"{legend.legendName}: no primary binding");
            Assert.AreEqual(LegendSoulRules.ExpressionCount, soul.expression.Count, $"{legend.legendName}: not three personality traits");
            Assert.AreEqual(1, soul.wish.Count, $"{legend.legendName}: no origin trait fits its rarity");
        }
    }

    /// <summary>Every legend class can serve somewhere: some default seat in Resources/Council accepts it (the Grand Archivist takes the Great Chronicler).</summary>
    [Test]
    public void EveryLegendClassHasACouncilSeat()
    {
        foreach (LegendClass legendClass in System.Enum.GetValues(typeof(LegendClass)))
        {
            bool accepted = GameCatalog.CouncilSeats.All.Any(seat => seat.allowedClasses != null && seat.allowedClasses.Contains(legendClass));
            Assert.IsTrue(accepted, $"No seat in Resources/Council accepts {LegendClasses.Title(legendClass)}");
        }
    }

    /// <summary>
    /// The council's areas load from Resources/Council/Council Areas, and with every default seat filled each area is
    /// answered by some seat (itself or a related one within reach): no story that asks for an area falls through to the
    /// Head of State while the whole council sits. A caravan at the gates goes to the Supreme Commander.
    /// </summary>
    [Test]
    public void EveryCouncilAreaIsAnsweredByTheDefaultSeats()
    {
        Assert.IsNotNull(CouncilAreaCatalog.Loaded, "No Council Areas asset in Resources/Council");
        var areas = CouncilAreaCatalog.Current;
        Assert.GreaterOrEqual(areas.Count, 10, "the areas asset stopped reading partway (check its YAML)");
        var seats = GameCatalog.CouncilSeats.All.Where(s => s != null).Select(s => new CouncilAreaRules.SeatView { title = s.title, areas = s.areas, holder = "Holder of " + s.title, order = s.order }).ToList();
        foreach (var area in areas)
            Assert.IsTrue(CouncilAreaRules.Find(area.id, seats, areas, CouncilAreaCatalog.CurrentMaxDistance).Found, $"No default seat answers for {area.name}");
        Assert.AreEqual("Supreme Commander", CouncilAreaRules.Find("defense", seats, areas, CouncilAreaCatalog.CurrentMaxDistance).seat);
    }

    /// <summary>The White-Haven Library holds both halves, the game's first Age, and an entry for every card that names one.</summary>
    [Test]
    public void TheLibraryAndTheKeywordCardsLoad()
    {
        Library.Invalidate();
        Keywords.Invalidate();
        CollectionAssert.IsEmpty(Library.Problems, "Library problems");
        var index = Library.Index;
        Assert.Greater(index.Entries.Count(e => !e.game), 0, "The Glossary is empty: run Tools > Gateway to Genesis > Import Lore From Vault");
        Assert.Greater(index.Entries.Count(e => e.game), 0, "The Game Wiki is empty");
        Assert.AreEqual(0, index.Age(GameAge.FirstAge)?.number, "The Library's ages table must know the first Age");

        var cards = Keywords.Cards;
        CollectionAssert.IsEmpty(cards.Problems, "Keyword masterfile problems");
        Assert.Greater(cards.Cards.Count, 0, "No keyword cards in Resources/Keywords/Keywords.md");
        var data = new TooltipData();
        foreach (var card in cards.Cards)
        {
            Assert.IsTrue(Keywords.TryBuild(card.Id, data), $"The card '{card.title}' builds no tooltip");
            Assert.IsFalse(string.IsNullOrWhiteSpace(data.summary) && string.IsNullOrWhiteSpace(data.effects), $"The card '{card.title}' says nothing in the first Age");
        }
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

    /// <summary>
    /// The first playable loop is authored end to end: the first Age exists, every Age's Act of Fate and crisis stories
    /// and every world feature's story are locked events, and the Age of Desolation always leads to the Age of Renewal.
    /// </summary>
    [Test]
    public void TheAgesTheWorldAndTheirStoriesFitTogether()
    {
        EventStoryIndex.Clear();
        try
        {
            var events = InkDrivenEventSetup.LoadVolumes().SelectMany(v => v.storyNodes).ToList();
            CollectionAssert.IsEmpty(ContentValidator.AgeAndWorldStoryProblems(events), "Age and world stories");
            CollectionAssert.IsEmpty(ContentValidator.BalladProblems(events), "ballads and their actors");
            var ruinSong = events.Where(e => e.ballad == "ruin_song").Select(e => e.verse).OrderBy(v => v).ToList();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ruinSong, "the Ruin-Song is a ballad of three verses");
        }
        finally
        {
            EventStoryIndex.Clear();
        }
        Assert.IsTrue(GameCatalog.Ages.TryGet(GameAge.FirstAge, out var first), "No Age of Desolation in Resources/Ages");
        CollectionAssert.AreEqual(new[] { "age-of-renewal" }, first.NextAgeTable().Select(n => n.ageId), "the famine always leads to the Age of Renewal");
        Assert.IsNotNull(GameCatalog.World.All.FirstOrDefault(), "No WorldSettings in Resources/World");
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
