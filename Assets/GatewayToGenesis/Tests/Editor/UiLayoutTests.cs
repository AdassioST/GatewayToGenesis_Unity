using NUnit.Framework;

public class UiLayoutTests
{
    [TestCase(320, 480, true, 1.5f)]
    [TestCase(480, 720, true, 1.5f)]
    [TestCase(1280, 720, false, 1f)]
    [TestCase(1920, 1080, true, 1f)]
    [TestCase(2560, 1080, false, 1.5f)]
    public void ExpandedRibbonFitsAndKeepsEveryActionReachable(float width, float height, bool labels, float scale)
    {
        var layout = new ActionBarLayout(12, width - 48, height - 180, labels, scale);
        Assert.LessOrEqual(layout.Width, width - 48);
        Assert.LessOrEqual(layout.Height, height - 180);
        Assert.GreaterOrEqual(layout.CellWidth, 56);
        Assert.GreaterOrEqual(layout.CellHeight, 56);
        Assert.GreaterOrEqual(layout.Columns * layout.Rows, 12);
        Assert.GreaterOrEqual(layout.ContentHeight + 24, layout.Height);
    }
    [Test]
    public void CollapsedRibbonOnlyReservesItsVisibleActions()
    {
        var compact = new ActionBarLayout(4, 1872, 900, false, 1);
        var expanded = new ActionBarLayout(12, 1872, 900, false, 1);
        Assert.AreEqual(1, compact.Rows);
        Assert.Less(compact.Width, expanded.Width);
        Assert.AreEqual(80, compact.Height);
    }
}
