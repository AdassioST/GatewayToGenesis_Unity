using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Emotional alchemy and evolution with no scene (<see cref="EmotionalAlchemy"/>, <see cref="EmotionalEvolution"/>):
/// cocktails poured by their scarcest note or eaten loosely; named compound feelings read from a mix (Anemoia); the niche
/// trade-off (a purist grows less but is superloaded on its exact cocktail, a generalist spreads on anything); lineages
/// that generalize when they starve, specialize when fed their exact cocktail, become named varieties and revert; and a
/// bloom's vigor through its palate.
/// </summary>
public class EmotionalAlchemyTests
{
    private static FeelingWeight W(Feeling f, float w) => new FeelingWeight { feeling = f, weight = w };

    private static EmotionalRegister Of(params (Feeling f, float v)[] parts)
    {
        var r = new EmotionalRegister();
        foreach (var (f, v) in parts) r.Add(f, v);
        return r;
    }

    private static readonly FeelingWeight[] Anemoia = { W(Feeling.Longing, 40), W(Feeling.Wonder, 30), W(Feeling.Estrangement, 20), W(Feeling.Love, 10) };

    [Test]
    public void ACocktailIsPouredByItsScarcestNote_OrEatenLoosely()
    {
        var exact = Of((Feeling.Longing, 0.8f), (Feeling.Wonder, 0.6f), (Feeling.Estrangement, 0.4f), (Feeling.Love, 0.2f));
        Assert.AreEqual(2f, EmotionalAlchemy.Servings(exact, Anemoia), 1e-4f, "twice the recipe: two servings");
        Assert.AreEqual(1f, EmotionalAlchemy.Fit(exact, Anemoia), 1e-4f);
        var noLove = Of((Feeling.Longing, 0.8f), (Feeling.Wonder, 0.6f), (Feeling.Estrangement, 0.4f));
        Assert.AreEqual(0f, EmotionalAlchemy.Servings(noLove, Anemoia), "without one note the exact cocktail cannot be made");
        Assert.AreEqual(1.8f, EmotionalAlchemy.Loose(noLove, Anemoia), 1e-4f, "but a loose eater takes what is there");
        Assert.AreEqual(1f, EmotionalAlchemy.Breadth(new[] { W(Feeling.Shame, 1) }), 1e-4f, "a purist leans on one note");
        Assert.Greater(EmotionalAlchemy.Breadth(Anemoia), 3f);
        StringAssert.StartsWith("Longing 40%, Wonder 30%", EmotionalAlchemy.Words(Anemoia));
    }

    [Test]
    public void NamedCompoundFeelings_AreReadFromAMix()
    {
        var air = EmotionalRegister.Of(Anemoia);
        air.Scale(0.01f);
        air.Add(Feeling.Pain, 2f);
        Assert.AreEqual(0, EmotionalAlchemy.Detect(air).Count(x => x.compound.id == "anemoia"), "drowned in pain it is not Anemoia");
        var dusk = EmotionalRegister.Of(Anemoia);
        Assert.AreEqual("anemoia", EmotionalAlchemy.Detect(dusk)[0].compound.id, "longing for a time never known");
        Assert.AreEqual("saudade", EmotionalAlchemy.Nearest(new[] { W(Feeling.Longing, 45), W(Feeling.Love, 35), W(Feeling.Tumult, 20) }).compound.id);
        Assert.AreEqual("grief", EmotionalAlchemy.Detect(Of((Feeling.Tumult, 0.4f), (Feeling.Love, 0.35f), (Feeling.Estrangement, 0.25f)))[0].compound.id, "grief is love that stays");
        Assert.IsTrue(EmotionalAlchemy.Compounds.Select(c => c.id).Distinct().Count() == EmotionalAlchemy.Compounds.Length, "every compound has its own id");
        var r = Of((Feeling.Dread, 0.5f));
        Assert.AreEqual(0.2f, EmotionalAlchemy.Transmute(r, Feeling.Dread, 0.4f), 1e-5f);
        Assert.AreEqual(0.2f, r.courage, 1e-5f, "dread faced becomes courage");
    }

    [Test]
    public void TheNicheTradeOff_APuristGrowsLessButIsSuperloaded()
    {
        var purist = new Palate { recipe = Anemoia.ToList(), specificity = 1f };
        var generalist = new Palate { recipe = Anemoia.ToList(), specificity = 0f };
        var exact = EmotionalRegister.Of(Anemoia);
        exact.Scale(0.005f); // 0.5 in all, in the exact proportion
        var p = EmotionalEvolution.Feed(purist, exact, 0.3f);
        var g = EmotionalEvolution.Feed(generalist, exact, 0.3f);
        Assert.Less(p.growth, g.growth, "the niche palate grows less");
        Assert.Greater(p.potency, 2f, "but fed its exact cocktail it is superloaded");
        Assert.AreEqual(1f, g.potency, 1e-3f, "a generalist gives ordinary gifts");
        var mismatch = Of((Feeling.Longing, 0.5f), (Feeling.Pain, 0.5f));
        var pm = EmotionalEvolution.Feed(purist, mismatch, 0.3f);
        var gm = EmotionalEvolution.Feed(generalist, mismatch, 0.3f);
        Assert.AreEqual(0f, pm.food, 1e-5f, "a purist starves where its cocktail cannot be made");
        Assert.Greater(gm.food, 0.4f, "a generalist eats the longing there");
        Assert.Less(pm.potency, p.potency);
    }

    [Test]
    public void ALineageGeneralizesWhenItStarves_AndSpecializesWhenFedItsCocktail()
    {
        // A purist of Anemoia on a land that holds only pain and joy: it loosens and takes them in.
        var starving = EmotionalEvolution.Seed("memory-marigolds", Anemoia, 0.8f);
        var harsh = Of((Feeling.Pain, 0.5f), (Feeling.Joy, 0.3f), (Feeling.Longing, 0.1f));
        var steps = new List<LineageEvent>();
        for (int i = 0; i < 30; i++) { var e = EmotionalEvolution.Evolve(starving, harsh, 0.3f, 0.1f, 0.0, "Memory Marigolds"); if (e != null) steps.Add(e); }
        Assert.AreEqual(30, starving.generation, "a generation each Echo");
        Assert.Less(starving.palate.specificity, 0.8f, "it grew looser");
        Assert.IsTrue(steps.Any(s => s.change == LineageChange.Generalized || s.change == LineageChange.Diverged));
        Assert.IsTrue(starving.palate.recipe.Any(l => l.feeling == Feeling.Pain), "it learned to eat the land's pain");
        Assert.IsNotNull(starving.variety, "far from its ancestors, a variety of its own");
        StringAssert.StartsWith("Memory Marigolds of ", starving.variety, "named for the compound it now resembles");

        // A generalist of Anemoia where Anemoia runs deep: it grows pickier and superloaded.
        var fed = EmotionalEvolution.Seed("memory-marigolds", Anemoia, 0.2f);
        var dusk = EmotionalRegister.Of(Anemoia);
        dusk.Scale(0.01f);
        float potencyBefore = EmotionalEvolution.Feed(fed.palate, dusk, 0.3f).potency;
        for (int i = 0; i < 20; i++) EmotionalEvolution.Evolve(fed, dusk, 0.3f, 0.1f, 0.0, "Memory Marigolds");
        Assert.Greater(fed.palate.specificity, 0.2f, "it specialized");
        Assert.Greater(fed.potency, potencyBefore, "and its gifts grew more potent");
        Assert.IsNull(fed.variety, "still its own kind: its cocktail is its ancestors'");

        // Evolution is slow: a roll above the step chance changes nothing but the generation.
        var still = EmotionalEvolution.Seed("x", Anemoia, 0.5f);
        Assert.IsNull(EmotionalEvolution.Evolve(still, harsh, 0.3f, 0.1f, 0.99, "X"));
        Assert.AreEqual(1, still.generation);
    }

    [Test]
    public void ABloomsVigorComesFromItsPalate_NeverFarBelowItsResidue()
    {
        var map = new WorldMap(1) { Capital = new HexCoord(999, 999) };
        HexHierarchy.ToWorld(HexCoord.Zero, HexHierarchy.Meso, out float x, out float y);
        var t = new WorldTile { coord = HexCoord.Zero, x = x, y = y, terrain = "plain", residue = 0.2f, settlement = -1 };
        map.Add(t);
        var shameMoss = new ResourceSiteSpec { id = "shame-moss", name = "Shame Moss", kind = ResourceKind.Bloom, residueNeed = 0.25f, specificity = 0.5f,
            flavors = { W(Feeling.Shame, 70), W(Feeling.Pride, 30) } };
        var site = new ResourceSite { spec = shameMoss.id, kind = ResourceKind.Bloom, center = 0, cells = { 0 } };
        Assert.AreEqual(0.6f * 0.2f / 0.25f, WorldResources.Vigor(map, site, shameMoss), 1e-4f, "with no shame about, it dims (three fifths of its residue)");
        var scars = new List<SufferingScar>();
        WorldSuffering.Add(scars, 0, Feeling.Shame, 0.35f);
        WorldSuffering.Add(scars, 0, Feeling.Pride, 0.15f);
        WorldSuffering.Apply(map, scars);
        Assert.AreEqual(1f, WorldResources.Vigor(map, site, shameMoss), 1e-4f, "where its cocktail runs, it thrives");
        Assert.Greater(WorldResources.Potency(map, site, shameMoss), 1.5f, "and on its exact cocktail its gifts are superloaded");
        site.lineage = EmotionalEvolution.Seed(shameMoss.id, new[] { W(Feeling.Joy, 1) }, 0.9f);
        Assert.AreSame(site.lineage.palate, WorldResources.PalateOf(site, shameMoss), "an evolved lineage eats as it has become");
        Assert.Less(WorldResources.Vigor(map, site, shameMoss), 1f, "and a joy-eater finds no joy here");
    }
}
