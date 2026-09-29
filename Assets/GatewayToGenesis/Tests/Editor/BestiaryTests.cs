using System.Linq;
using NUnit.Framework;

/// <summary>The bestiary's taxonomy (CreatureTaxonomy; vault: Arcanorian Ecology.md, "The Nature of a Species").</summary>
public class BestiaryTests
{
    [Test]
    public void Taxonomy_HasAllOfAecorsSubgroupsUnderTheirDiets()
    {
        Assert.AreEqual(21, CreatureTaxonomy.Profiles.Count);
        Assert.AreEqual(21, CreatureTaxonomy.Profiles.Select(p => (p.diet, p.subgroup)).Distinct().Count(), "each diet-subgroup pair once");
        CollectionAssert.AreEqual(
            new[] { CreatureSubgroup.Frightful, CreatureSubgroup.Docile, CreatureSubgroup.Venerable, CreatureSubgroup.Territorial, CreatureSubgroup.Benign, CreatureSubgroup.Wrathful },
            CreatureTaxonomy.SubgroupsOf(CreatureDiet.Herbivore));
        CollectionAssert.AreEqual(
            new[] { CreatureSubgroup.Smart, CreatureSubgroup.Erratic, CreatureSubgroup.Territorial, CreatureSubgroup.Benign },
            CreatureTaxonomy.SubgroupsOf(CreatureDiet.Omnivore));
        CollectionAssert.AreEqual(
            new[] { CreatureSubgroup.Unobtrusive, CreatureSubgroup.Jingoistic, CreatureSubgroup.Solitary, CreatureSubgroup.Social, CreatureSubgroup.Isolationist, CreatureSubgroup.Marauder, CreatureSubgroup.Trapper },
            CreatureTaxonomy.SubgroupsOf(CreatureDiet.Carnivore));
        CollectionAssert.AreEqual(
            new[] { CreatureSubgroup.Frightful, CreatureSubgroup.Hiding, CreatureSubgroup.Territorial, CreatureSubgroup.Docile },
            CreatureTaxonomy.SubgroupsOf(CreatureDiet.Detritivore));
    }

    [Test]
    public void Taxonomy_StancesFollowAecorsDivisions()
    {
        CreatureStance StanceOf(CreatureDiet d, CreatureSubgroup s) => CreatureTaxonomy.Profile(d, s).stance;
        Assert.AreEqual(CreatureStance.Passive, StanceOf(CreatureDiet.Herbivore, CreatureSubgroup.Venerable));
        Assert.AreEqual(CreatureStance.Aggressive, StanceOf(CreatureDiet.Herbivore, CreatureSubgroup.Territorial));
        Assert.AreEqual(CreatureStance.Neutral, StanceOf(CreatureDiet.Omnivore, CreatureSubgroup.Territorial), "the same name sits under another stance for another diet");
        Assert.AreEqual(CreatureStance.Territorial, StanceOf(CreatureDiet.Carnivore, CreatureSubgroup.Social));
        Assert.AreEqual(CreatureStance.Apex, StanceOf(CreatureDiet.Carnivore, CreatureSubgroup.Trapper));
        Assert.AreEqual(CreatureStance.Neutral, StanceOf(CreatureDiet.Detritivore, CreatureSubgroup.Docile));
        Assert.IsTrue(CreatureTaxonomy.Profiles.All(p => !string.IsNullOrEmpty(p.summary) && p.sizes.Length > 0 && p.groupMin >= 1 && p.groupMax >= p.groupMin && p.pace != ReproductionPace.Typical));
    }

    [Test]
    public void Taxonomy_RejectsSubgroupsADietDoesNotHave()
    {
        Assert.IsFalse(CreatureTaxonomy.IsValid(CreatureDiet.Herbivore, CreatureSubgroup.Trapper));
        Assert.IsFalse(CreatureTaxonomy.IsValid(CreatureDiet.Detritivore, CreatureSubgroup.Venerable));
        Assert.IsFalse(CreatureTaxonomy.IsValid(CreatureDiet.Carnivore, CreatureSubgroup.Docile), "AECOR has no passive carnivores");
        Assert.IsTrue(CreatureTaxonomy.IsValid(CreatureDiet.Omnivore, CreatureSubgroup.Erratic));
    }

    [Test]
    public void Taxonomy_NamesAndApexPredators()
    {
        Assert.AreEqual("Frightful Herbivore", CreatureTaxonomy.Name(CreatureDiet.Herbivore, CreatureSubgroup.Frightful));
        Assert.AreEqual("Trapper Apex Predator", CreatureTaxonomy.Name(CreatureDiet.Carnivore, CreatureSubgroup.Trapper));
        Assert.AreEqual(ThreatResponse.HuntsLoudly, CreatureTaxonomy.Profile(CreatureDiet.Carnivore, CreatureSubgroup.Marauder).response, "Marauders announce themselves");
        Assert.AreEqual(ThreatResponse.Lures, CreatureTaxonomy.Profile(CreatureDiet.Carnivore, CreatureSubgroup.Trapper).response);
    }

    [Test]
    public void Species_TakeTheirSubgroupsTraitsUnlessTheyStateTheirOwn()
    {
        var wolf = new SpeciesSpec { id = "wolf", diet = CreatureDiet.Carnivore, subgroup = CreatureSubgroup.Social };
        Assert.AreEqual((4, 12), CreatureTaxonomy.GroupOf(wolf));
        Assert.AreEqual(ReproductionPace.Medium, CreatureTaxonomy.PaceOf(wolf));
        var bees = new SpeciesSpec { id = "bee", diet = CreatureDiet.Herbivore, subgroup = CreatureSubgroup.Territorial, groupMin = 500, groupMax = 5000, reproduction = ReproductionPace.Fast };
        Assert.AreEqual((500, 5000), CreatureTaxonomy.GroupOf(bees));
        Assert.AreEqual(ReproductionPace.Fast, CreatureTaxonomy.PaceOf(bees));
        Assert.AreEqual("alone", CreatureTaxonomy.GroupWords(1, 1));
        Assert.AreEqual("alone or in pairs", CreatureTaxonomy.GroupWords(1, 2));
        Assert.AreEqual("in groups of 4-12", CreatureTaxonomy.GroupWords(4, 12));
        Assert.AreEqual("very slow", CreatureTaxonomy.PaceWord(ReproductionPace.VerySlow));
    }
}
