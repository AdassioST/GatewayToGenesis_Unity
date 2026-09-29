using NUnit.Framework;

public class WorldExplorationAppearanceTests
{
    [Test]
    public void KnowledgeRemainsDistinctWithoutAddingMarkers()
    {
        var tile = new WorldTile();
        Assert.AreEqual(0, WorldRenderer.KnowledgeByte(tile));
        tile.revealed = true; Assert.AreEqual(100, WorldRenderer.KnowledgeByte(tile));
        tile.known = true; Assert.AreEqual(200, WorldRenderer.KnowledgeByte(tile));
        tile.microSurveyMask = 2;
        Assert.AreEqual(255, WorldRenderer.MicroKnowledgeByte(tile, 1));
        Assert.AreEqual(160, WorldRenderer.MicroKnowledgeByte(tile, 2));
        tile.explored = true; Assert.AreEqual(255, WorldRenderer.KnowledgeByte(tile));
    }
    [Test]
    public void VisibleGroundKeepsColourAndSurveyedGroundHasFullColour()
    {
        var s = WorldExplorationAppearance.Saturation;
        Assert.Greater(s.x, 0); Assert.Less(s.x, s.y);
        Assert.Less(s.y, s.z); Assert.Less(s.z, s.w); Assert.AreEqual(1, s.w);
    }
}

