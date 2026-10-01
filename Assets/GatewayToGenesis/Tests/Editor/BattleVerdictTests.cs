using System.Linq;
using NUnit.Framework;

/// <summary>
/// The battle's verdicts and its screen with no scene: seven tiers that mirror between the two sides, the Legendary
/// Victory only by hand against the forecast, and the pre-battle preview (strengths, what makes them, the prediction).
/// </summary>
public class BattleVerdictTests
{
    private static readonly CombatSettings Settings = new CombatSettings();

    private static SpeciesSpec Wolf => new SpeciesSpec { id = "wolf", name = "Wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social, size = CreatureSize.Medium, structure = 0.9f };

    private static BattleSide Formation(string template, bool manual = false)
    {
        var side = SymphonyDecks.Score(OrchestralFormations.Raise(Settings.Template(template), Settings, 0), new DeckSources { age = 0, symphony = Settings.Symphony });
        side.manual = manual;
        return side;
    }

    private static BattleSide Pack(int n) =>
        SymphonyDecks.Score(CreatureCombat.Side(Wolf, n), new DeckSources { age = 0, symphony = Settings.Symphony, species = id => Wolf });

    [TestCase(true, 0.1f, 0.6f, BattleOutcome.DecisiveVictory)]
    [TestCase(true, 0.3f, 0.5f, BattleOutcome.CloseVictory)]
    [TestCase(true, 0.15f, 0.3f, BattleOutcome.CloseVictory)]
    [TestCase(true, 0.6f, 0.9f, BattleOutcome.PyrrhicVictory)]
    [TestCase(false, 0.6f, 0.1f, BattleOutcome.CrushingDefeat)]
    [TestCase(false, 0.4f, 0.3f, BattleOutcome.CloseDefeat)]
    [TestCase(false, 0.9f, 0.6f, BattleOutcome.ValiantDefeat)]
    public void Tiers_ReadTheLossesOfBothSides(bool won, float own, float enemy, BattleOutcome expected)
    {
        Assert.AreEqual(expected, BattleVerdicts.Tier(won, own, enemy));
    }

    [Test]
    public void TheTwoVerdicts_MirrorEachOther()
    {
        for (float a = 0f; a <= 1f; a += 0.05f)
            for (float d = 0f; d <= 1f; d += 0.05f)
            {
                var winner = BattleVerdicts.Tier(true, a, d);
                Assert.AreEqual(BattleVerdicts.Mirror(winner), BattleVerdicts.Tier(false, d, a), $"winner lost {a:P0}, loser {d:P0}");
                Assert.IsTrue(BattleVerdicts.IsVictory(winner));
                Assert.IsFalse(BattleVerdicts.IsVictory(BattleVerdicts.Tier(false, d, a)));
            }
    }

    [Test]
    public void SevenTiers_EachWithItsWordsAndRank()
    {
        var all = System.Enum.GetValues(typeof(BattleOutcome)).Cast<BattleOutcome>().ToList();
        Assert.AreEqual(7, all.Count);
        Assert.AreEqual(4, all.Count(BattleVerdicts.IsVictory), "three victories and the Legendary");
        CollectionAssert.AllItemsAreUnique(all.Select(BattleVerdicts.Words).ToList());
        CollectionAssert.AllItemsAreUnique(all.Select(BattleVerdicts.Rank).ToList());
        Assert.AreEqual(0, BattleVerdicts.Rank(BattleOutcome.LegendaryVictory));
        Assert.IsTrue(all.All(o => !string.IsNullOrEmpty(BattleVerdicts.Meaning(o))));
    }

    [Test]
    public void MythicalVictoryRequiresManualPlayEvenWithoutACard()
    {
        Assert.IsTrue(BattleVerdicts.Legendary(won: true, byHand: true, predictedWinChance: 0.1f));
        Assert.IsFalse(BattleVerdicts.Legendary(won: true, byHand: false, predictedWinChance: 0.1f), "the auto-resolve never grants it");
        Assert.IsFalse(BattleVerdicts.Legendary(won: true, byHand: true, predictedWinChance: 0.6f), "a battle you were meant to win is not legendary");
        Assert.IsFalse(BattleVerdicts.Legendary(won: false, byHand: true, predictedWinChance: 0.1f));

        // A strong formation played by hand against a forecast that gave it nothing: the micro layer's win.
        foreach (bool byHand in new[] { true, false })
        {
            var run = BattleResolver.Begin(new BattleSetup { attacker = Formation("ember-host", manual: byHand), defender = Pack(4), field = new Battlefield { age = 0 }, seed = 2 }, Settings);
            run.Prediction = new BattleForecast { runs = 10, defenderWins = 10 };
            run.BeginMeasure();
            if (byHand)
            {
                int i = Enumerable.Range(0, run.Hand(true).Count).First(k => run.WhyNotPlay(true, k) == null);
                Assert.IsNull(run.CommitCard(true, i));
            }
            var report = run.Finish();
            Assert.IsTrue(report.AttackerWon);
            Assert.AreEqual(byHand, report.attacker.mythical);
            Assert.AreNotEqual(BattleOutcome.LegendaryVictory, report.attacker.outcome, "Mythical preserves its casualty verdict");
            Assert.IsFalse(BattleVerdicts.IsVictory(report.defender.outcome));
            if (byHand) Assert.AreEqual(true, report.legendaryFor);
        }
        var movementOnly = BattleResolver.Begin(new BattleSetup { attacker = Formation("ember-host", manual: true), defender = Pack(4), field = new Battlefield { age = 0 }, seed = 2 }, Settings);
        movementOnly.Prediction = new BattleForecast { runs = 10, defenderWins = 10 };
        var movementReport = movementOnly.Finish();
        Assert.IsTrue(movementReport.AttackerWon);
        Assert.IsTrue(movementReport.attacker.mythical, "manual movement and standing orders can win without playing a card");
    }

    [Test]
    public void AutoResolve_NeverGrantsTheLegendary()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            var r = BattleResolver.Resolve(new BattleSetup { attacker = Formation("ember-host"), defender = Formation("hearth-guard"), field = new Battlefield { age = 0 }, seed = seed }, Settings);
            Assert.AreNotEqual(BattleOutcome.LegendaryVictory, r.attacker.outcome);
            Assert.AreNotEqual(BattleOutcome.LegendaryVictory, r.defender.outcome);
            Assert.AreEqual(r.outcome, r.attacker.outcome);
        }
    }

    [Test]
    public void ThePreview_ShowsStrengthsAndWhatMakesThem()
    {
        var field = new Battlefield { age = 0, height = 0.2f, riverCrossing = true, ground = BattleGround.Hills };
        var p = BattlePreview.Of(new BattleSetup { attacker = Formation("ember-host"), defender = Formation("hearth-guard"), field = field, seed = 3 }, Settings, 10, "Battle of the Ford");
        Assert.AreEqual("Battle of the Ford", p.title);
        foreach (var s in new[] { p.attacker, p.defender })
        {
            Assert.AreEqual("Base strength", s.factors[0].label);
            Assert.AreEqual(s.strength, s.factors.Sum(f => f.amount), 0.5f, $"{s.name}: the modifiers add up to its strength");
            Assert.Greater(s.cards, 0);
        }
        Assert.IsTrue(p.defender.factors.Any(f => f.label == "Higher ground" && f.amount > 0f));
        Assert.IsTrue(p.attacker.factors.Any(f => f.label == "Crossing a river" && f.amount < 0f));
        Assert.IsTrue(p.attacker.factors.Any(f => f.label == "Symphony"));
        Assert.That(p.balance, Is.InRange(0f, 1f));
        Assert.AreEqual(p.attacker.Strength - p.defender.Strength, p.Advantage);
        StringAssert.Contains(BattleVerdicts.Words(p.attacker.predicted), p.Advice(true));
        Assert.AreEqual(BattleVerdicts.IsVictory(p.attacker.predicted), p.attacker.winChance >= 0.5f);
        Assert.IsNotNull(BattleVerdicts.Casualties(p.attacker.predictedLoss));
    }

    [Test]
    public void ThePrediction_FavoursTheStronger()
    {
        var field = new Battlefield { age = 0 };
        var strong = BattlePreview.Of(new BattleSetup { attacker = Formation("hearth-guard"), defender = Pack(4), field = field, seed = 1 }, Settings, 12);
        var weak = BattlePreview.Of(new BattleSetup { attacker = Formation("hearth-guard"), defender = Pack(40), field = field, seed = 1 }, Settings, 12);
        Assert.Greater(strong.balance, weak.balance);
        Assert.IsTrue(BattleVerdicts.IsVictory(strong.attacker.predicted));
        Assert.Less(BattleVerdicts.Rank(strong.attacker.predicted), BattleVerdicts.Rank(weak.attacker.predicted));
        Assert.Less(strong.attacker.predictedLoss, weak.attacker.predictedLoss);
    }
}
