using NUnit.Framework;

/// <summary>
/// Enlightenment goals written as riddles (EnlightenedCondition.riddle): no goal and no progress shown until met, a clue
/// once the people are on the right path (part of the goal met, or a clue trigger reached), the plain goal once answered.
/// </summary>
public class RiddleTests
{
    private static float _goal, _hint;

    [SetUp]
    public void SetUp()
    {
        _goal = _hint = 0f;
        GameValues.Register("riddle_test_goal", (string t, out float v) => { v = _goal; return true; });
        GameValues.Register("riddle_test_hint", (string t, out float v) => { v = _hint; return true; });
    }

    private static EnlightenedCondition Riddle(float clueAt = 0.5f, string clueTrigger = null) => new EnlightenedCondition
    {
        type = EnlightenedType.GameValue, trigger = "riddle_test_goal", requiredAmount = 4f, description = "Identify 4 creatures",
        riddle = "What lives beyond the firelight has no names yet.", clue = "Send someone to stand where it stands.",
        clueAt = clueAt, clueTrigger = clueTrigger, clueAmount = 2f,
    };

    [Test]
    public void AnUnansweredRiddle_ShowsNeitherGoalNorProgress()
    {
        var goal = Riddle();
        Assert.IsTrue(goal.IsRiddle);
        Assert.AreEqual("What lives beyond the firelight has no names yet.", goal.RiddleText());
        Assert.AreEqual(string.Empty, goal.ProgressText(), "a riddle counts no progress");
        StringAssert.DoesNotContain("Identify", goal.RiddleText());
    }

    [Test]
    public void TheClue_ComesOnceHalfTheGoalIsMet()
    {
        var goal = Riddle();
        _goal = 1f;
        Assert.IsFalse(goal.ClueShown());
        _goal = 2f;
        Assert.IsTrue(goal.ClueShown());
        StringAssert.Contains("Clue: Send someone", goal.RiddleText());
        Assert.AreEqual(string.Empty, goal.ProgressText());
    }

    [Test]
    public void TheClue_CanComeFromAnotherValue()
    {
        var goal = Riddle(clueAt: 0f, clueTrigger: "riddle_test_hint");
        _goal = 3f;
        Assert.IsFalse(goal.ClueShown(), "clueAt 0: only the trigger brings it");
        _hint = 2f;
        Assert.IsTrue(goal.ClueShown());
    }

    [Test]
    public void AnAnsweredRiddle_SaysWhatItWas()
    {
        var goal = Riddle();
        _goal = 4f;
        Assert.IsTrue(goal.IsMet());
        Assert.AreEqual("Identify 4 creatures (the riddle answered)", goal.RiddleText());
    }

    [Test]
    public void APlainGoal_KeepsItsProgress()
    {
        var goal = Riddle();
        goal.riddle = null;
        _goal = 1f;
        Assert.IsFalse(goal.IsRiddle);
        Assert.AreNotEqual(string.Empty, goal.ProgressText());
    }
}
