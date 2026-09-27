using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Who legends are: the vault's Legend Trait and Composure notes as the game reads them (<see cref="LegendTraitNote"/>,
/// <see cref="ComposureNote"/>), a legend's soul (<see cref="LegendSoulRules"/>), Composure (<see cref="ComposureRules"/>),
/// the achievements they earn and their tooltip wording (<see cref="LegendSoulText"/>). Pure, so they also run outside
/// Unity; the real notes are checked by ContentTests.
/// </summary>
public class LegendSoulTests
{
    // The shapes the vault's Legend Trait note uses, edge cases included (a missing quote, a typo'd element, prose, a
    // one-element row, alternatives written three ways, a citation residue, sections the game does not read yet).
    private const string TraitNote =
        "### 1. The Wish - Origin and Physical [[Legend Trait]]s\n" +
        "\n" +
        "Every [[Legend]] begins in the friction of their own [[Fundamental Frequency]].\n" +
        "\n" +
        "### All Origin & Physical [[Legend Trait]]s\n" +
        "\n" +
        "**[[Dissonance]] Origin [[Legend Trait]]s\n" +
        "\n" +
        "Heir to Shame\n" +
        "\n" +
        "| [[Legend Trait]] | Scaled Cost | Underdog Points | Binding Effect | Narrative Effect |\n" +
        "| --- | --- | --- | --- | --- |\n" +
        "| **Harmonic Static** <br> *Your presence disrupts the Great Harmonic Loom.* | –100 | +100 | –1 to all Bindings | +2× Fragments of Lucidity when stabilizing a Leyline. |\n" +
        "\n" +
        "**Classical Origin [[Legend Trait]]s**\n" +
        "\n" +
        "| Trait | Scaled Cost | Underdog Points | Binding Effect | Narrative Effect |\n" +
        "| --- | --- | --- | --- | --- |\n" +
        "| **Leyline Drifter** <br> *You were raised along the shifting paths.* | 0 | +50 | +1 Flux, –1 Crystal | Can instinctively sense Leyline convergence points. |\n" +
        "\n" +
        "**[[Consonance]] Origin [[Legend Trait]]s**\n" +
        "\n" +
        "| Trait | Scaled Cost | Underdog Points | Binding Effect | Narrative Effect |\n" +
        "| --- | --- | --- | --- | --- |\n" +
        "| **Harmonic Prodigy** <br> *Your soul naturally attunes.* | +150 | 0 | +1 to all Bindings | Spells cost 10% less Essence Sacrifice. |\n" +
        "| **Crystal-Clarity Born** <br> *Your mind is a prism.* | +100 | 0 | +3 Crystal | Start with one rare Crystal-based spell known. |\n" +
        "\n" +
        "### All Personality [[Legend Trait]]s\n" +
        "\n" +
        "**Emotional & Relational Personality [[Legend Trait]]s** ([[Selenea]] attuned)\n" +
        "\n" +
        " - [[Moody]], [[Serene]].\n" +
        "\n" +
        "| **[[Legend Trait]]** | **Element Evolution** | **Middle [[Legend Trait]] Path** |\n" +
        "| --- | --- | --- |\n" +
        "| [[Moody]]<br><br>_\".\"_ | [[Flux]]<br><br>[[Void]]<br><br>[[Luminance]] | [[Flux]]: Tends to perform emotions -> [[Stage Performer]]<br><br>[[Void]]: Detaches -> [[Impostor Syndrome]]<br><br>[[Luminance]]: Finds objective truth -> [[Clarity Seeker]]<br><br> |\n" +
        "| [[Serene]]<br><br>_\"The mind only rings clear.\"_ | [[Resonance]]<br><br>[[Strand]]<br><br>[[Crystal]] | [[Resonance]]: Inner harmony -> [[Soul Tuner]]<br><br>[[Strand]]: Anchors -> [[Diplomatic Harmonist]] \\| [[Nurturing Caregiver]]<br><br>[[Crystal]]: Defensive will -> [[Determined Achiever]]<br><br> |\n" +
        "\n" +
        "**Action & Will Personality [[Legend Trait]]s** ([[Auric Aria]] attuned)\n" +
        "\n" +
        "| **[[Legend Trait]]** | **Element Evolution** | **Middle [[Legend Trait]] Path** |\n" +
        "| --- | --- | --- |\n" +
        "| [[Selfless]]<br><br>_\".\"_ | [[Resonance]]<br><br>[[Void]]<br><br>[[Strand]] | [[Resources]]: Tunes -> [[Savior Complex]]<br><br>[[Void]]: Gives essence -> [[Sacrificial Lamb]]<br><br>[[Strand]]: Warps -> [[Over-giving Devout]]<br><br> |\n" +
        "| [[Volatile]]<br><br>_\"Life is just so fun.\"_ | [[Cindergale]]<br><br>[[Luminance]]<br><br>[[Flux]] | [[Cindergale]]: [[Fiery Passion]]<br><br>[[Luminance]]: <br><br>[[Flux]]: |\n" +
        "| [[Lustful]]<br><br>_\"I am starving...\"_ | [[Cindergale]]<br><br>[[Flux]]<br><br>[[Strand]]<br> | [[Cindergale]]: Hunger -> [[Consuming Gale]]<br><br>[[Flux]]: Floods -> [[Self-Indulgent Slave]] or [[Romantic Hedonist]]<br><br>[[Strand]]: Devotion -> [[Over-giving Devout]]<br><br> |\n" +
        "\n" +
        "**Cognitive & Logic Personality [[Legend Trait]]s** ([[Lacrimosa]] attuned)\n" +
        "\n" +
        "| **[[Legend Trait]]** | **Element Evolution** | **Middle [[Legend Trait]] Path** |\n" +
        "| --- | --- | --- |\n" +
        "| [[Hermetic]] | Luminance / Void / Strand | They retreat from others to chase truth (Luminance).Arcanorian-Bible.pdf​ |\n" +
        "| [[Reverent]]<br><br>_\"I know there is something greater.\"_ | [[Luminance]] | [[Clarity Seeker]] |\n" +
        "\n" +
        "[[Naive]] -> Idealist, [[Broken Optimist]]\n" +
        "\n" +
        "| Middle Trait | Element Pairing | Development & Path to Apex (Why & How) |\n" +
        "| --- | --- | --- |\n" +
        "| [[Clarity Seeker]] | [[Luminance]]<br><br>[[Crystal]]<br><br> | **Why & How:** Deceived in the past, they demand absolute, piercing truth. |\n" +
        "| Stage Performer | [[Flux]]<br><br>[[Resonance]]<br><br> | **Why & How:** They use performed emotion. |\n" +
        "|  |  |  |\n" +
        "\n" +
        "### All Personality [[Apex Trait]]s\n" +
        "\n" +
        "**Heart-Thread Weaver** [[Resonance]]\n" +
        "\n" +
        "### All [[Spellweaver]] [[Legend Trait]]s\n" +
        "\n" +
        "| **Masterful Tactician** | [[Cindergale]] | Winning 5 battles -> [[Cunning Edge]] |\n";

    private const string ComposureText =
        "#mechanic #spellweaving \n" +
        "\n" +
        "The level of stress, sanity, and meter of the internal [[Coherence]] of any [[Spellweaver]].\n" +
        "\n" +
        "- Pristine: The perfect rest state for a [[Soul Leitmotif]].\n" +
        "- Clouded: This is the normal state for most [[Spellweaver]]s.\n" +
        " \n" +
        "- Fractured: This is the first sign of actual damage.\n" +
        "- Spiraling: This is the final level. **This is the last point where anyone can intervene to save them.** Any breach further.\n" +
        "- Surrender: Once a [[Soul Leitmotif]] reaches this point. **The refusal to process pain feels exactly like mercy.**\n";

    private static LegendTraitCatalog Catalog() => LegendTraitNote.Parse(TraitNote);

    private static readonly ComposureTuning Tuning = new ComposureTuning();

    private static LegendSoulRules.Template Architect(string name = "Vaelia") =>
        new LegendSoulRules.Template { name = name, legendClass = LegendClass.Architect, rarity = GameRarity.Common };

    // A soul with a known Expression, as if Spiraling had just been healed.
    private static LegendSoul Wounded() => new LegendSoul
    {
        leitmotif = "Crystal",
        expression = new List<string> { "Moody", "Serene", "Lustful" },
        startingExpression = new List<string> { "Moody", "Serene", "Lustful" },
        evolvedAlong = new List<string> { "", "", "" },
        strain = 30f,
        deepest = ComposureState.Spiraling,
    };

    // ===== THE LEGEND TRAIT NOTE =====

    [Test]
    public void TraitNote_ReadsOriginTablesWithTheirKindsNumbersAndBindingEffects()
    {
        var catalog = Catalog();
        Assert.AreEqual(4, catalog.origins.Count);

        var harmonicStatic = catalog.Origin("Harmonic Static");
        Assert.AreEqual(OriginKind.Dissonance, harmonicStatic.kind);
        Assert.AreEqual(-100, harmonicStatic.scaledCost);
        Assert.AreEqual(100, harmonicStatic.underdogPoints);
        Assert.AreEqual("Your presence disrupts the Great Harmonic Loom.", harmonicStatic.description);
        Assert.AreEqual("–1 to all Bindings", harmonicStatic.bindingEffect, "the cell is kept as written");
        Assert.IsTrue(MagicBindings.All.All(b => harmonicStatic.bindings[b] == -1), "\"to all Bindings\" reaches all seven");
        StringAssert.Contains("× Fragments of Lucidity", harmonicStatic.narrativeEffect);

        var drifter = catalog.Origin("leyline drifter");
        Assert.AreEqual(OriginKind.Classical, drifter.kind);
        Assert.AreEqual(1, drifter.bindings["Flux"]);
        Assert.AreEqual(-1, drifter.bindings["Crystal"]);
        Assert.AreEqual(2, drifter.bindings.Count);

        var prodigy = catalog.Origin("Harmonic Prodigy");
        Assert.AreEqual(OriginKind.Consonance, prodigy.kind);
        Assert.AreEqual(150, prodigy.scaledCost);
        Assert.AreEqual(3, catalog.Origin("Crystal-Clarity Born").bindings["Crystal"]);
        Assert.IsNull(catalog.Origin("Heir to Shame"), "names without a table row carry no numbers and are left out");
    }

    [Test]
    public void TraitNote_ReadsPersonalityRowsGroupsQuotesElementsAndPaths()
    {
        var catalog = Catalog();
        CollectionAssert.AreEqual(new[] { "Moody", "Serene", "Selfless", "Volatile", "Lustful", "Hermetic", "Reverent" }, catalog.personalities.Select(p => p.name).ToArray());

        var moody = catalog.Personality("Moody");
        Assert.AreEqual("Emotional & Relational", moody.group);
        Assert.AreEqual("Selenea", moody.attunement);
        Assert.IsNull(moody.quote, "the vault's _\".\"_ is a placeholder, not a quote");
        CollectionAssert.AreEqual(new[] { "Flux", "Void", "Luminance" }, moody.elements);
        CollectionAssert.AreEqual(new[] { "Stage Performer" }, moody.PathFor("Flux").middleTraits);
        Assert.AreEqual("Tends to perform emotions", moody.PathFor("Flux").description);

        var serene = catalog.Personality("Serene");
        Assert.AreEqual("\"The mind only rings clear.\"", serene.quote);
        CollectionAssert.AreEqual(new[] { "Diplomatic Harmonist", "Nurturing Caregiver" }, serene.PathFor("Strand").middleTraits, "an escaped | offers alternatives");

        var lustful = catalog.Personality("Lustful");
        Assert.AreEqual("Action & Will", lustful.group);
        Assert.AreEqual("Auric Aria", lustful.attunement);
        CollectionAssert.AreEqual(new[] { "Self-Indulgent Slave", "Romantic Hedonist" }, lustful.PathFor("Flux").middleTraits, "\"or\" offers alternatives");

        var volatileTrait = catalog.Personality("Volatile");
        CollectionAssert.AreEqual(new[] { "Fiery Passion" }, volatileTrait.PathFor("Cindergale").middleTraits, "a middle trait written without \"->\"");
        Assert.IsEmpty(volatileTrait.PathFor("Luminance").middleTraits);
    }

    [Test]
    public void TraitNote_ReportsGapsInsteadOfGuessing()
    {
        var catalog = Catalog();
        var selfless = catalog.Personality("Selfless");
        Assert.IsNull(selfless.PathFor("Resonance"), "\"[[Resources]]\" is not read as Resonance");
        CollectionAssert.AreEqual(new[] { "Sacrificial Lamb" }, selfless.PathFor("Void").middleTraits);
        Assert.IsTrue(catalog.problems.Any(p => p.Contains("Selfless") && p.Contains("\"Resources\"")));

        var hermetic = catalog.Personality("Hermetic");
        CollectionAssert.AreEqual(new[] { "Luminance", "Void", "Strand" }, hermetic.elements, "plain element names are read too");
        Assert.IsEmpty(hermetic.paths);
        Assert.IsTrue(catalog.problems.Any(p => p.StartsWith("Hermetic: its Middle Legend Trait Path is prose")));
        Assert.IsTrue(catalog.problems.Any(p => p.Contains("Arcanorian-Bible.pdf") && p.Contains("Hermetic")));

        var reverent = catalog.Personality("Reverent");
        CollectionAssert.AreEqual(new[] { "Clarity Seeker" }, reverent.PathFor("Luminance").middleTraits, "a bare middle trait belongs to the row's only element");
        Assert.IsTrue(catalog.problems.Any(p => p.StartsWith("Reverent: its Element Evolution lists 1 binding")));
        Assert.IsTrue(catalog.problems.Any(p => p.StartsWith("Moody: no line of speech")));
        Assert.IsTrue(catalog.problems.Any(p => p.StartsWith("Volatile: no middle trait written for its Luminance, Flux paths")));
        Assert.IsTrue(catalog.problems.Any(p => p.Contains("missing from the middle-trait table") && p.Contains("Impostor Syndrome") && !p.Contains("Clarity Seeker")));
    }

    [Test]
    public void TraitNote_ReadsTheMiddleTraitTableAndSkipsSectionsItDoesNotUseYet()
    {
        var catalog = Catalog();
        CollectionAssert.AreEqual(new[] { "Clarity Seeker", "Stage Performer" }, catalog.middleTraits.Select(m => m.name).ToArray());
        var seeker = catalog.Middle("Clarity Seeker");
        CollectionAssert.AreEqual(new[] { "Luminance", "Crystal" }, seeker.elements);
        Assert.AreEqual("Deceived in the past, they demand absolute, piercing truth.", seeker.whyAndHow);
        Assert.IsNull(catalog.Personality("Masterful Tactician"), "Spellweaver traits are not read yet");
        Assert.IsNull(catalog.Personality("Heart-Thread Weaver"), "Apex traits are not read yet");
    }

    [Test]
    public void TraitNote_EmptyOrShapelessNotesSayWhatIsMissing()
    {
        Assert.IsTrue(LegendTraitNote.Parse(null).IsEmpty);
        var shapeless = LegendTraitNote.Parse("# Legend Trait\n\nNo tables here.");
        Assert.IsTrue(shapeless.IsEmpty);
        Assert.AreEqual(2, shapeless.problems.Count(p => p.StartsWith("No ")));
    }

    // ===== THE COMPOSURE NOTE =====

    [Test]
    public void ComposureNote_ReadsTheLeadTheFiveStatesAndTheirEmphasis()
    {
        var lore = ComposureNote.Parse(ComposureText);
        Assert.IsEmpty(lore.problems);
        Assert.AreEqual("The level of stress, sanity, and meter of the internal [[Coherence]] of any [[Spellweaver]].", lore.lead);
        Assert.AreEqual(5, lore.states.Count);
        Assert.AreEqual("This is the first sign of actual damage.", lore.Describe(ComposureState.Fractured));
        Assert.AreEqual("This is the last point where anyone can intervene to save them.", lore.Emphasis(ComposureState.Spiraling));
        Assert.AreEqual("This is the final level.", lore.FirstSentence(ComposureState.Spiraling));
        Assert.IsNull(lore.Emphasis(ComposureState.Clouded));
        Assert.AreEqual(4, ComposureNote.Parse("- Pristine: calm").problems.Count(p => p.StartsWith("No bullet")), "the four states it does not describe");
    }

    // ===== COMPOSURE =====

    [TestCase(0f, ComposureState.Pristine)]
    [TestCase(9.9f, ComposureState.Pristine)]
    [TestCase(10f, ComposureState.Clouded)]
    [TestCase(20f, ComposureState.Clouded)]
    [TestCase(39.9f, ComposureState.Clouded)]
    [TestCase(40f, ComposureState.Fractured)]
    [TestCase(69.9f, ComposureState.Fractured)]
    [TestCase(70f, ComposureState.Spiraling)]
    [TestCase(99.9f, ComposureState.Spiraling)]
    [TestCase(100f, ComposureState.Surrender)]
    [TestCase(140f, ComposureState.Surrender)]
    public void Composure_StrainFallsIntoTheFiveStates(float strain, ComposureState expected)
    {
        Assert.AreEqual(expected, ComposureRules.StateOf(strain, Tuning));
    }

    [Test]
    public void Composure_RestEasesTowardTheCloudedBaselineWithoutPassingIt()
    {
        var rest = new ComposureContext();
        Assert.AreEqual(ComposureState.Clouded, ComposureRules.StateOf(Tuning.baseline, Tuning), "the baseline is Clouded, the vault's normal state");
        Assert.AreEqual(73f, ComposureRules.Next(75f, rest, Tuning), 1e-4);
        Assert.AreEqual(20f, ComposureRules.Next(21f, rest, Tuning), 1e-4, "never below the baseline");
        Assert.AreEqual(7f, ComposureRules.Next(5f, rest, Tuning), 1e-4, "joy below the baseline fades back up");
        Assert.AreEqual(74.5f, ComposureRules.Next(75f, new ComposureContext { onExpedition = true }, Tuning), 1e-4);
        Assert.AreEqual(74.75f, ComposureRules.Next(75f, new ComposureContext { seated = true }, Tuning), 1e-4);
    }

    [Test]
    public void Composure_OnlyTheCouncilCarriesTheCrisisAndItsDead()
    {
        var declared = new ComposureContext { seated = true, crisisBegun = true, crisisDeclared = true };
        Assert.AreEqual(30f + Tuning.declaredCrisisLoad - Tuning.seatedRecovery, ComposureRules.Next(30f, declared, Tuning), 1e-4);
        var unannounced = new ComposureContext { seated = true, crisisBegun = true };
        Assert.AreEqual(Tuning.unannouncedCrisisLoad, ComposureRules.Load(unannounced, Tuning), 1e-4);

        var grief = new ComposureContext { seated = true, griefShare = 0.1f };
        Assert.AreEqual(0.1f * Tuning.griefScale, ComposureRules.Load(grief, Tuning), 1e-4);
        var massGrief = new ComposureContext { seated = true, griefShare = 0.9f };
        Assert.AreEqual(Tuning.griefCapPerSeventh, ComposureRules.Load(massGrief, Tuning), 1e-4, "grief is capped per Seventh");

        var resting = new ComposureContext { crisisBegun = true, crisisDeclared = true, griefShare = 0.5f };
        Assert.AreEqual(0f, ComposureRules.Load(resting, Tuning), "a legend who stepped down is shielded");
    }

    [Test]
    public void Composure_GriefIsTheShareOfThePeopleLost()
    {
        Assert.AreEqual(0.25f, ComposureRules.GriefShare(10, 15, 15), 1e-4, "5 of the 20 alive before");
        Assert.AreEqual(0f, ComposureRules.GriefShare(10, 10, 15));
        Assert.AreEqual(1f, ComposureRules.GriefShare(0, 4, 0), 1e-4, "everyone");
    }

    [Test]
    public void Composure_DimsTheCouncilOnlyOnceCracked()
    {
        Assert.AreEqual(1f, ComposureRules.CouncilFactor(ComposureState.Pristine, Tuning));
        Assert.AreEqual(1f, ComposureRules.CouncilFactor(ComposureState.Clouded, Tuning), "the vault: Clouded is where a Spellweaver acts with most confidence");
        Assert.AreEqual(Tuning.fracturedCouncil, ComposureRules.CouncilFactor(ComposureState.Fractured, Tuning));
        Assert.AreEqual(Tuning.spiralingCouncil, ComposureRules.CouncilFactor(ComposureState.Spiraling, Tuning));
        Assert.AreEqual(0f, ComposureRules.CouncilFactor(ComposureState.Surrender, Tuning));
    }

    [Test]
    public void Composure_HealingAwakensOnlyAfterARealWound()
    {
        int wound = Tuning.woundSevenths;
        Assert.IsTrue(ComposureRules.HealingAwakens(ComposureState.Clouded, ComposureState.Fractured, wound, 0, Tuning));
        Assert.IsTrue(ComposureRules.HealingAwakens(ComposureState.Pristine, ComposureState.Spiraling, wound, 1, Tuning));
        Assert.IsFalse(ComposureRules.HealingAwakens(ComposureState.Clouded, ComposureState.Fractured, wound - 1, 0, Tuning), "a crack that barely opened");
        Assert.IsFalse(ComposureRules.HealingAwakens(ComposureState.Fractured, ComposureState.Spiraling, wound, 0, Tuning), "not healed yet");
        Assert.IsFalse(ComposureRules.HealingAwakens(ComposureState.Clouded, ComposureState.Clouded, wound, 0, Tuning), "never cracked");

        Assert.IsTrue(ComposureRules.WoundHealed(ComposureState.Clouded, ComposureState.Fractured));
        Assert.IsFalse(ComposureRules.WoundHealed(ComposureState.Fractured, ComposureState.Spiraling));
    }

    [Test]
    public void Composure_OnlyALegendBroughtBackFromSpiralingReachesTheAbyss()
    {
        int wound = Tuning.woundSevenths;
        int full = LegendSoulRules.MaxOrnaments;
        Assert.IsFalse(ComposureRules.HealingAwakens(ComposureState.Clouded, ComposureState.Fractured, wound * 3, full, Tuning),
            "with both Ornaments a Fractured wound heals quietly, so the Awakened State's own collapse cannot restart it");
        Assert.IsTrue(ComposureRules.HealingAwakens(ComposureState.Clouded, ComposureState.Spiraling, wound, full, Tuning));
        Assert.AreEqual(ComposureState.Fractured, ComposureRules.StateOf(Tuning.abyssAftermath, Tuning), "the collapse after the Awakened State cracks the gem, no deeper");
    }

    // ===== THE SOUL =====

    [Test]
    public void Soul_TakesTheClassAffinityAndLeansItsFirstTraitTowardIt()
    {
        var soul = LegendSoulRules.Create(Architect(), Catalog(), new SoulTuning(), 42, Tuning.baseline);
        Assert.AreEqual("Crystal", soul.leitmotif, "a Great Architect's affinity is Crystal");
        Assert.AreEqual(LegendSoulRules.ExpressionCount, soul.expression.Count, "exactly three personality traits");
        Assert.AreEqual(soul.expression.Count, soul.expression.Distinct().Count());
        Assert.AreEqual("Serene", soul.expression[0], "the only trait here that points toward Crystal");
        CollectionAssert.AreEqual(soul.expression, soul.startingExpression);
        Assert.IsTrue(soul.evolvedAlong.All(e => e == ""));
        Assert.AreEqual(Tuning.baseline, soul.strain);
        Assert.AreEqual(1, soul.wish.Count);
        Assert.LessOrEqual(Catalog().Origin(soul.wish[0]).scaledCost, new SoulTuning().commonBudget, "a Common legend's origin fits its budget");
    }

    [Test]
    public void Soul_IsTheSameForTheSameWorldAndDiffersBetweenWorlds()
    {
        var catalog = Catalog();
        var a = LegendSoulRules.Create(Architect(), catalog, new SoulTuning(), 7, 20f);
        var b = LegendSoulRules.Create(Architect(), catalog, new SoulTuning(), 7, 20f);
        CollectionAssert.AreEqual(a.expression, b.expression);
        CollectionAssert.AreEqual(a.wish, b.wish);
        var worlds = Enumerable.Range(1, 20).Select(seed => string.Join("/", LegendSoulRules.Create(Architect(), catalog, new SoulTuning(), seed, 20f).expression)).Distinct().Count();
        Assert.Greater(worlds, 1, "other worlds draw other traits");
        Assert.AreNotEqual(LegendSoulRules.Seed(1, "Vaelia", 0), LegendSoulRules.Seed(1, "Wynnievere", 0));
    }

    [Test]
    public void Soul_AuthoredTraitsWinAndUnknownOnesAreDropped()
    {
        var template = new LegendSoulRules.Template
        {
            name = "Orphael", legendClass = LegendClass.Concertist, rarity = GameRarity.Common, leitmotif = "cindergale",
            personality = new List<string> { "Lustful", "Not A Trait" }, origins = new List<string> { "Harmonic Prodigy" },
        };
        var soul = LegendSoulRules.Create(template, Catalog(), new SoulTuning(), 3, 20f);
        Assert.AreEqual("Cindergale", soul.leitmotif, "an authored leitmotif wins over the class affinity");
        Assert.AreEqual("Lustful", soul.expression[0]);
        Assert.AreEqual(3, soul.expression.Count, "the rest is drawn");
        CollectionAssert.DoesNotContain(soul.expression, "Not A Trait");
        CollectionAssert.AreEqual(new[] { "Harmonic Prodigy" }, soul.wish, "an authored origin ignores the rarity budget");
    }

    [Test]
    public void Soul_WithoutTheNoteHasALeitmotifButNoTraits()
    {
        var soul = LegendSoulRules.Create(Architect(), new LegendTraitCatalog(), new SoulTuning(), 1, 20f);
        Assert.AreEqual("Crystal", soul.leitmotif);
        Assert.IsEmpty(soul.expression);
        Assert.IsEmpty(soul.wish);
    }

    [Test]
    public void Soul_BindingsAddThePrimaryTheWishOrnamentsAndEvolutions()
    {
        var soul = new LegendSoul
        {
            leitmotif = "Crystal",
            wish = new List<string> { "Harmonic Static" },
            ornaments = new List<string> { "Flux" },
            expression = new List<string> { "Stage Performer" },
            evolvedAlong = new List<string> { "Flux" },
        };
        var sheet = LegendSoulRules.Bindings(soul, Catalog(), new SoulTuning());
        Assert.AreEqual(5 + 5 - 1, sheet["Crystal"], "base, the vault's +5 primary, Harmonic Static's -1 to all");
        Assert.AreEqual(5 - 1 + 3 + 2, sheet["Flux"], "an Ornament and a trait evolved along it");
        Assert.AreEqual(4, sheet["Void"]);
        Assert.AreEqual("Apprentice", MagicBindings.Proficiency(sheet["Crystal"]));
        var floor = LegendSoulRules.Bindings(soul, Catalog(), new SoulTuning { baseScore = 0 });
        Assert.AreEqual(MagicBindings.MinimumScore, floor["Void"], "never below the scale's 1");
    }

    [TestCase(1, "Terrible")]
    [TestCase(5, "Apprentice")]
    [TestCase(13, "Average")]
    [TestCase(21, "Skilled")]
    [TestCase(28, "Advanced")]
    [TestCase(33, "Expert")]
    [TestCase(42, "Master")]
    public void Bindings_ProficiencyFollowsTheVaultsTable(int score, string proficiency)
    {
        Assert.AreEqual(proficiency, MagicBindings.Proficiency(score));
    }

    [Test]
    public void Bindings_EveryClassHasItsVaultAffinity()
    {
        Assert.AreEqual("Resonance", MagicBindings.AffinityOf(LegendClass.Sovereign));
        Assert.AreEqual("Cindergale", MagicBindings.AffinityOf(LegendClass.Vanguard));
        Assert.AreEqual("Strand", MagicBindings.AffinityOf(LegendClass.Chronicler));
        Assert.AreEqual(MagicBindings.All.Length, System.Enum.GetValues(typeof(LegendClass)).Cast<LegendClass>().Select(MagicBindings.AffinityOf).Distinct().Count());
    }

    // ===== MOTIF AWAKENING =====

    [Test]
    public void Awakening_EmbellishesAnOrnamentAndEvolvesATraitAlongIt()
    {
        var catalog = Catalog();
        var soul = Wounded();
        var result = LegendSoulRules.Awaken(soul, "Vaelia", catalog, 11);

        Assert.IsFalse(result.abyss);
        Assert.AreEqual(1, result.ornament, "the primary Ornament first");
        CollectionAssert.AreEqual(new[] { result.element }, soul.ornaments);
        Assert.AreNotEqual("Crystal", result.element, "never the primary binding");
        var leaning = new[] { "Moody", "Serene", "Lustful" }.SelectMany(t => catalog.Personality(t).elements).Distinct();
        CollectionAssert.Contains(leaning.ToList(), result.element, "an element its personality traits lean toward");

        Assert.IsTrue(result.Evolved, "every element here has a middle trait written for it");
        int index = soul.startingExpression.IndexOf(result.evolvedFrom);
        Assert.AreEqual(result.evolvedTo, soul.expression[index]);
        Assert.AreEqual(result.element, soul.evolvedAlong[index]);
        CollectionAssert.Contains(catalog.Personality(result.evolvedFrom).PathFor(result.element).middleTraits, result.evolvedTo);
        CollectionAssert.AreEqual(new[] { "Moody", "Serene", "Lustful" }, soul.startingExpression, "the starting Expression is remembered");
        Assert.IsTrue(result.afterSpiraling);
        Assert.AreEqual(1, soul.awakenings.Count);
        StringAssert.StartsWith($"Motif Awakening to {result.element}, the primary Ornament", soul.awakenings[0]);
        CollectionAssert.Contains(LegendSoulRules.MiddleTraits(soul).ToList(), result.evolvedTo);
    }

    [Test]
    public void Awakening_ThirdTimeDrownsInTheCatalyticAbyss()
    {
        var catalog = Catalog();
        var soul = Wounded();
        var first = LegendSoulRules.Awaken(soul, "Vaelia", catalog, 11);
        var second = LegendSoulRules.Awaken(soul, "Vaelia", catalog, 11);
        Assert.AreEqual(2, second.ornament, "then the secondary");
        Assert.AreNotEqual(first.element, second.element);
        Assert.IsFalse(soul.awakenedState);

        var third = LegendSoulRules.Awaken(soul, "Vaelia", catalog, 11);
        Assert.IsTrue(third.abyss, "the vault: a completed Soul Leitmotif drowns in the Catalytic Abyss of Emotion");
        Assert.IsTrue(soul.awakenedState);
        Assert.AreEqual(LegendSoulRules.MaxOrnaments, soul.ornaments.Count, "no third Ornament");
        Assert.IsFalse(third.Evolved);
    }

    [Test]
    public void Awakening_IsDeterministicAndTwoSoulsOfOneTemplateGrowApart()
    {
        var catalog = Catalog();
        var a = LegendSoulRules.Create(Architect(), catalog, new SoulTuning(), 5, 20f);
        var b = LegendSoulRules.Create(Architect(), catalog, new SoulTuning(), 5, 20f);
        CollectionAssert.AreEqual(a.expression, b.expression, "the same template in the same world starts alike");

        var result = LegendSoulRules.Awaken(a, "Vaelia", catalog, 5);
        Assert.IsEmpty(b.ornaments, "one legend's awakening leaves the other untouched");
        CollectionAssert.AreEqual(b.startingExpression, b.expression);
        Assert.AreEqual(result.element, LegendSoulRules.Awaken(b, "Vaelia", catalog, 5).element, "the same draw for the same world and legend");
        Assert.IsFalse(result.afterSpiraling, "a fresh soul has not been Spiraling");
    }

    [Test]
    public void Awakening_WithNoLeaningTraitsTakesAnyBindingNotYetHeld()
    {
        var soul = new LegendSoul { leitmotif = "Void", expression = new List<string> { "Not In The Note" }, evolvedAlong = new List<string> { "" } };
        var result = LegendSoulRules.Awaken(soul, "Nameless", Catalog(), 1);
        Assert.IsNotNull(result.element);
        Assert.AreNotEqual("Void", result.element);
        Assert.IsFalse(result.Evolved);
    }

    // ===== A LEGEND'S LIFE, SEVENTH BY SEVENTH =====

    private static readonly ComposureContext Crisis = new ComposureContext { seated = true, crisisBegun = true, crisisDeclared = true };
    private static readonly ComposureContext Rest = new ComposureContext();

    // Live Sevenths in one context until the state is reached (or give up), gathering what happened.
    private static List<SoulEvent> LiveUntil(LegendSoul soul, ComposureContext context, System.Func<LegendSoul, bool> done, LegendTraitCatalog catalog, int limit = 400)
    {
        var events = new List<SoulEvent>();
        for (int i = 0; i < limit && !done(soul); i++) events.AddRange(LegendSoulLife.Seventh(soul, context, "Vaelia", catalog, 9, Tuning));
        Assert.IsTrue(done(soul), "the state was never reached");
        return events;
    }

    private static ComposureState State(LegendSoul soul) => ComposureRules.StateOf(soul.strain, Tuning);

    [Test]
    public void Life_ACrisisCracksTheCouncilAndRestHealsItIntoAMotifAwakening()
    {
        var catalog = Catalog();
        var soul = LegendSoulRules.Create(Architect(), catalog, new SoulTuning(), 9, Tuning.baseline);

        var falling = LiveUntil(soul, Crisis, s => State(s) == ComposureState.Spiraling, catalog);
        Assert.IsTrue(falling.Any(e => e.Deeper && e.from == ComposureState.Clouded && e.to == ComposureState.Fractured));
        Assert.IsTrue(falling.Any(e => e.Deeper && e.to == ComposureState.Spiraling));
        Assert.IsFalse(falling.Any(e => e.kind == SoulEvent.Kind.Awakened), "no awakening while the wound is open");
        Assert.GreaterOrEqual(soul.woundSevenths, Tuning.woundSevenths);

        var healing = LiveUntil(soul, Rest, s => State(s) == ComposureState.Clouded, catalog);
        var awakening = healing.Single(e => e.kind == SoulEvent.Kind.Awakened).awakening;
        Assert.IsFalse(awakening.abyss);
        Assert.AreEqual(1, awakening.ornament);
        Assert.IsTrue(awakening.afterSpiraling, "healed from Spiraling (You Are Filled With Determination, once a trait evolves)");
        Assert.AreEqual(ComposureState.Clouded, soul.deepest, "the healed wound is forgotten");
        Assert.AreEqual(0, soul.woundSevenths);
        LiveUntil(soul, Rest, s => s.strain <= Tuning.baseline, catalog);
        Assert.AreEqual(Tuning.baseline, soul.strain, 1e-3, "rest settles at the Clouded baseline");
    }

    [Test]
    public void Life_ACrackThatBarelyOpenedHealsQuietly()
    {
        var soul = LegendSoulRules.Create(Architect(), Catalog(), new SoulTuning(), 9, Tuning.fracturedAt + 1f);
        soul.deepest = ComposureState.Fractured;
        soul.woundSevenths = Tuning.woundSevenths - 4;
        var events = LiveUntil(soul, Rest, s => State(s) == ComposureState.Clouded, Catalog());
        Assert.IsFalse(events.Any(e => e.kind == SoulEvent.Kind.Awakened));
        Assert.IsEmpty(soul.ornaments);
        Assert.AreEqual(0, soul.woundSevenths, "judged once, then forgotten");
        Assert.AreEqual(ComposureState.Clouded, soul.deepest);
    }

    [Test]
    public void Life_SurrenderComesOnlyAfterAWarningAndLosesTheLegend()
    {
        var soul = LegendSoulRules.Create(Architect(), Catalog(), new SoulTuning(), 9, Tuning.baseline);
        var massDeaths = new ComposureContext { seated = true, crisisBegun = true, crisisDeclared = true, griefShare = 0.5f };
        var events = new List<SoulEvent>();
        for (int i = 0; i < 40 && !events.Any(e => e.kind == SoulEvent.Kind.Lost); i++)
            events.AddRange(LegendSoulLife.Seventh(soul, massDeaths, "Vaelia", Catalog(), 9, Tuning));

        int lost = events.FindIndex(e => e.kind == SoulEvent.Kind.Lost);
        int spiraling = events.FindIndex(e => e.Deeper && e.to == ComposureState.Spiraling);
        Assert.GreaterOrEqual(lost, 0, "the council's dead broke the legend");
        Assert.GreaterOrEqual(spiraling, 0);
        Assert.Less(spiraling, lost - 1, "Spiraling was reported in an earlier Seventh: there was time to intervene");
        Assert.AreEqual(SoulEvent.Kind.Lost, events.Last().kind);
    }

    [Test]
    public void Life_TheAwakenedStateSurgesThenCollapsesWithoutStartingAgain()
    {
        var catalog = Catalog();
        var soul = Wounded();
        soul.ornaments.AddRange(new[] { "Flux", "Strand" });
        soul.strain = Tuning.spiralingAt + 2f;
        soul.woundSevenths = Tuning.woundSevenths;

        var healing = LiveUntil(soul, Rest, s => State(s) <= ComposureState.Clouded, catalog);
        Assert.IsTrue(healing.Single(e => e.kind == SoulEvent.Kind.Awakened).awakening.abyss, "both Ornaments: the Catalytic Abyss of Emotion");
        Assert.AreEqual(Tuning.abyssSevenths, soul.surgeSevenths);

        var surge = LiveUntil(soul, Rest, s => s.surgeSevenths == 0, catalog);
        Assert.AreEqual(1, surge.Count(e => e.kind == SoulEvent.Kind.Collapsed));
        Assert.AreEqual(ComposureState.Fractured, State(soul), "the gem collapses back into cracks");

        var afterwards = LiveUntil(soul, Rest, s => State(s) <= ComposureState.Clouded, catalog);
        Assert.IsFalse(afterwards.Any(e => e.kind == SoulEvent.Kind.Awakened), "the collapse is not a wound that reopens the Abyss");
        Assert.AreEqual(0, soul.surgeSevenths);
    }

    [Test]
    public void Life_JoyCanHealAWoundAndAwakenTheLegend()
    {
        var soul = Wounded();
        soul.strain = Tuning.fracturedAt + 5f;
        soul.deepest = ComposureState.Fractured;
        soul.woundSevenths = Tuning.woundSevenths;
        var events = LegendSoulLife.Shift(soul, -Tuning.rankJoy, "Vaelia", Catalog(), 9, Tuning);
        Assert.AreEqual(ComposureState.Clouded, State(soul));
        var awakening = events.Single(e => e.kind == SoulEvent.Kind.Awakened).awakening;
        Assert.IsFalse(awakening.afterSpiraling, "a Fractured wound, not a Spiraling one");
        Assert.AreEqual(1, soul.ornaments.Count, "the vault: intense joy can trigger Motif Awakenings as well");
    }

    // ===== ACHIEVEMENTS =====

    [Test]
    public void Achievements_SpiralingMotifAwakeningsAndTheAbyssAreEarned()
    {
        List<string> Earned(AchievementEvent e) => AchievementTriggers.Earned(e).ToList();
        CollectionAssert.DoesNotContain(Earned(AchievementEvent.Of(AchievementSignal.ComposureFell, (int)ComposureState.Fractured)), "let-s-go-to-therapy");
        CollectionAssert.Contains(Earned(AchievementEvent.Of(AchievementSignal.ComposureFell, (int)ComposureState.Spiraling)), "let-s-go-to-therapy");
        CollectionAssert.Contains(Earned(AchievementEvent.Of(AchievementSignal.ComposureFell, (int)ComposureState.Surrender)), "let-s-go-to-therapy", "falling past Spiraling reached it");

        var plain = Earned(AchievementEvent.Of(AchievementSignal.MotifAwakened, 1));
        CollectionAssert.Contains(plain, "alchemical-pelican");
        CollectionAssert.DoesNotContain(plain, "you-are-filled-with-determination");
        CollectionAssert.Contains(Earned(AchievementEvent.Of(AchievementSignal.MotifAwakened, 2, true)), "you-are-filled-with-determination");
        CollectionAssert.AreEqual(new[] { "the-crux-of-nigredo" }, Earned(AchievementEvent.Of(AchievementSignal.CatalyticAbyss)));
    }

    // ===== THE TOOLTIP =====

    [Test]
    public void Text_SummaryNamesTheLeitmotifOrnamentsAndComposure()
    {
        var soul = Wounded();
        soul.ornaments.Add("Flux");
        soul.strain = 75f;
        string summary = LegendSoulText.Summary(soul, Tuning);
        StringAssert.Contains("[[Soul Leitmotif]]", summary);
        StringAssert.Contains("[[Crystal]]", summary);
        StringAssert.Contains("[[Flux]]", summary);
        StringAssert.Contains("Spiraling", summary);
        Assert.AreEqual("x0.7 while Spiraling", LegendSoulText.CouncilNote(soul, 0.7f, Tuning));
        Assert.IsNull(LegendSoulText.CouncilNote(soul, 1f, Tuning));
        soul.surgeSevenths = 3;
        Assert.AreEqual("x2 in the Awakened State", LegendSoulText.CouncilNote(soul, 2f, Tuning));
        StringAssert.Contains("Awakened State", LegendSoulText.Composure(soul, Tuning));
    }

    [Test]
    public void Text_DetailsQuoteTheVaultForTraitsAndTheState()
    {
        var catalog = Catalog();
        var soul = Wounded();
        soul.wish.Add("Leyline Drifter");
        soul.strain = 75f;
        LegendSoulRules.Awaken(soul, "Vaelia", catalog, 11);
        string details = LegendSoulText.Details(soul, catalog, LegendSoulRules.Bindings(soul, catalog, new SoulTuning()), ComposureNote.Parse(ComposureText), Tuning);
        StringAssert.Contains("This is the final level.", details);
        StringAssert.Contains("<b>This is the last point where anyone can intervene to save them.</b>", details, "the vault's bold survives as bold");
        StringAssert.Contains("You were raised along the shifting paths.", details);
        for (int i = 0; i < soul.expression.Count; i++)
        {
            // An unevolved trait shows its line of speech; an evolved one shows what it became and from what.
            if (soul.HasEvolved(i)) StringAssert.Contains($"was {soul.startingExpression[i]}", details);
            else if (catalog.Personality(soul.expression[i]).quote != null) StringAssert.Contains(catalog.Personality(soul.expression[i]).quote, details);
        }
        StringAssert.Contains("Motif Awakenings", details);
        StringAssert.Contains("Bindings", details);
        foreach (ComposureState state in System.Enum.GetValues(typeof(ComposureState))) Assert.IsNotEmpty(LegendSoulText.ComposureAdvice(state));
    }
}
