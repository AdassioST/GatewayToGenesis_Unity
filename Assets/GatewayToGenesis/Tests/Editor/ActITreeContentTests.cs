using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The Act I tree as authored in Resources (Docs/Planning/TECH_TREE_ACT_I.md): its technologies and prerequisites, the
/// Grimoire it grants by Echoes of Hunger, and the Age asset that makes Echoes of Hunger a pivot crisis rather than the
/// Age Crisis (The Inescapable Hunger begins in Act III).
/// </summary>
public class ActITreeContentTests
{
    private static List<TechnologyData> Tree() => Resources.LoadAll<TechnologyData>("Technology")
        .Where(t => t.gameUnit != null && t.gameUnit.section == "Age of Desolation").ToList();

    [Test]
    public void ActIHasFortyTechnologiesWithTheirPrerequisitesInOrder()
    {
        var tree = Tree();
        Assert.AreEqual(40, tree.Count);
        var names = new HashSet<string>(tree.Select(t => t.gameUnit.name));
        foreach (var t in tree)
            foreach (var before in t.techRequirements)
            {
                Assert.IsTrue(names.Contains(before), $"{t.gameUnit.name} needs '{before}', which is in the tree");
                Assert.Less(tree.First(x => x.gameUnit.name == before).tier, t.tier, $"{before} comes in an earlier tier than {t.gameUnit.name}");
            }
        foreach (var old in TechnologyAliases.Renamed.Keys) Assert.IsFalse(names.Contains(old), $"no technology is still called {old}");
    }

    [Test]
    public void ByEchoesOfHungerTheGrimoireHoldsTenSeatsForTenCards()
    {
        var all = Tree().Where(t => t.gameUnit.name != "Golden Orchard Belts" && t.gameUnit.name != "Golden Ash").SelectMany(t => t.techUnlockables);
        var state = Grimoire.Collect(all);
        Assert.AreEqual(4, state.symphonySeats);
        Assert.AreEqual(2, state.ceremonies);
        Assert.AreEqual(10, state.Seats);
        Assert.AreEqual(7, state.cards.Count, "one scripted card per binding");
        CollectionAssert.AreEquivalent(HarmonicCircle.Seven, state.cards.Select(c => c.binding), "every binding has its card");
        Assert.AreEqual(3, state.wildcards);
        Assert.IsTrue(state.cards.All(c => c.chord == ChordTier.Unison && c.tempo == SpellTempo.Staccato), "Act I knows only Staccato Unisons");
    }

    [Test]
    public void TheFirstCeremonyCanBeFullyVoicedWhenOstinatoOpensIt()
    {
        var tree = Tree().ToDictionary(t => t.gameUnit.name);
        var before = AgeRules.PathTo("Ostinato", n => tree.TryGetValue(n, out var t) ? t.techRequirements : null);
        var state = Grimoire.Collect(before.SelectMany(n => tree[n].techUnlockables));
        Assert.AreEqual(1, state.ceremonies);
        foreach (var voice in Grimoire.Voices)
            Assert.IsTrue(state.cards.Any(c => Grimoire.VoiceOf(c.binding) == voice), $"a card sings the {voice} by Ostinato");
    }

    [Test]
    public void EchoesOfHungerOpensActIIButTheInescapableHungerBeginsInActIII()
    {
        var age = Resources.LoadAll<AgeDefinition>("Ages").First(a => a.id == "age-of-desolation");
        Assert.AreEqual("Echoes of Hunger", age.actTechnologies[0].Trim(), "Echoes of Hunger is the Act I pivot");
        StringAssert.StartsWith("desolation_echoes_of_hunger", age.actOfFateStories[0], "its pivot crisis is told first");
        float actThree = 2f / 3f;
        foreach (var stage in age.crisisStages) Assert.GreaterOrEqual(stage.startsAt, actThree, $"{stage.name} begins in Act III");
        Assert.IsTrue(age.crisisStages.All(s => string.IsNullOrWhiteSpace(s.technology)), "no crisis stage waits on Echoes of Hunger");
        CollectionAssert.IsSubsetOf(age.preparationTechnologies, Tree().Select(t => t.gameUnit.name).ToList());
    }
}
