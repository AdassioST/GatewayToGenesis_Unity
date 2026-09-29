using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class CultureAtlasTests
{
    [Test]
    public void NamesNeverMergeLocalPracticesInstitutionsOrVariants()
    {
        var atlas = new CultureAtlasReadModel();
        atlas.Add("LocalPractice", "flavor-log", "Flavor Log", "local");
        atlas.Add("Institution", "flavor-log", "Flavor Log", "institution");
        atlas.Add("TeachingVariant", "var-1", "Same name", "taught");
        atlas.Add("Tradition", "var-1", "Same name", "lived");
        Assert.AreEqual(4, atlas.nodes.Count);
        Assert.AreEqual("institution", atlas.Resolve("Institution:flavor-log").detail);
    }

    [Test]
    public void ReplacingAPlaceholderPreservesTheBiographyAndMissingReferencesStayUnknown()
    {
        var atlas = new CultureAtlasReadModel();
        var loaf = atlas.Add("Recipe", "invented-1", "A player's loaf", "Original recipe");
        atlas.Add("Evidence", "loss-1", "A loss", "placeholder");
        atlas.Join(loaf.key, "Evidence:loss-1");
        atlas.Add("Evidence", "loss-1", "The Hunger", "Actual record");
        CollectionAssert.Contains(atlas.Resolve("Evidence:loss-1").links, loaf.key);
        atlas.Join(loaf.key, "Landmark:gone");
        StringAssert.Contains("cannot be inferred", atlas.Resolve("Landmark:gone").detail);
        Assert.AreEqual("Original recipe", loaf.detail);
        Assert.AreEqual(1, atlas.Browse("player", "Recipe").Count());
    }

    [Test]
    public void LocalComparisonExplainsPracticeWithoutConvertingExposureToAdoption()
    {
        var song = new LocalPracticeView { practice = "song", name = "Song", stage = LocalPracticeStage.Practiced, provenanceText = "originated here", gatherings = 3 };
        var exposed = new LocalPracticeView { practice = "song", name = "Song", stage = LocalPracticeStage.Exposed, provenanceText = "origin unknown" };
        var a = new SettlementProfileView { name = "First", practices = new[] { song } };
        var b = new SettlementProfileView { name = "Second", practices = new[] { exposed } };
        string result = CultureAtlasReadModel.Compare(a, b);
        StringAssert.Contains("origin unknown", result);
        StringAssert.Contains("Shared recorded customs: 0", result);
        Assert.AreEqual(LocalPracticeStage.Exposed, exposed.stage);
    }

    [Test]
    public void ARecipeCanReachEvidenceVenueAndPracticeWithinThreeSelections()
    {
        var a = new CultureAtlasReadModel();
        foreach (var key in new[] { "Recipe:loaf", "Evidence:hunger", "Tradition:table", "Settlement:1", "Account:claim" })
        { var parts = key.Split(':'); a.Add(parts[0], parts[1], key, key); }
        a.Join("Recipe:loaf", "Evidence:hunger"); a.Join("Recipe:loaf", "Tradition:table");
        a.Join("Tradition:table", "Settlement:1"); a.Join("Tradition:table", "Account:claim");
        foreach (string key in new[] { "Evidence:hunger", "Tradition:table", "Settlement:1" }) Assert.LessOrEqual(Distance(a, "Recipe:loaf", key), 3);
        Assert.AreNotEqual(a.Resolve("Evidence:hunger"), a.Resolve("Account:claim"));
    }

    public static int Distance(CultureAtlasReadModel atlas, string from, string to)
    {
        var seen = new HashSet<string>(); var queue = new Queue<(string key, int depth)>(); queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var next = queue.Dequeue(); if (next.key == to) return next.depth;
            if (!seen.Add(next.key)) continue;
            foreach (string link in atlas.Resolve(next.key).links) queue.Enqueue((link, next.depth + 1));
        }
        return int.MaxValue;
    }
}
