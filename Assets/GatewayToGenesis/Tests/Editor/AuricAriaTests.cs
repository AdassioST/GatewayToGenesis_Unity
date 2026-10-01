using NUnit.Framework;

/// <summary>
/// The Auric Aria's voice (<see cref="AuricAria"/>, <see cref="AuricWords"/>): announcements are heard before lines, the
/// same words are never queued twice, old news is dropped, words cut short go back to the head, words stay up long
/// enough to read and at least as long as their recorded voice, and stage directions are set apart.
/// </summary>
public class AuricAriaTests
{
    private bool _persist;

    [SetUp]
    public void SetUp()
    {
        // With memory only, the voice is on whatever this machine's Options say.
        _persist = Tutorials.Persist;
        Tutorials.Persist = false;
        AuricAria.Clear();
        AuricAria.VoiceSeconds = null;
    }

    [TearDown]
    public void TearDown()
    {
        AuricAria.Clear();
        AuricAria.VoiceSeconds = null;
        Tutorials.Persist = _persist;
    }

    [Test]
    public void AnnouncementsAreHeardBeforeLinesAndEachIdOnce()
    {
        AuricAria.Say("line.a", "(softly) One.");
        AuricAria.Announce("done.a", "whisper", "Title A");
        AuricAria.Say("line.b", "Two.");
        AuricAria.Announce("done.b", "whisper", "Title B");
        AuricAria.Say("line.a", "(softly) One, again.");

        Assert.AreEqual(new[] { "done.a", "done.b", "line.b", "line.a" }, Ids());
        Assert.IsTrue(AuricAria.TryNext(out var first));
        Assert.AreEqual(AuricForm.Announcement, first.form);
        Assert.AreEqual("Title A", first.title);
    }

    [Test]
    public void OldNewsIsDroppedAndCutWordsComeBackFirst()
    {
        for (int i = 0; i < 6; i++) AuricAria.Say("line." + i, "Words " + i);
        Assert.AreEqual(4, AuricAria.Waiting.Count, "only a few wait their turn");
        Assert.AreEqual("line.0", AuricAria.Waiting[0].id, "the first said are kept");

        Assert.IsTrue(AuricAria.TryNext(out var said));
        AuricAria.Resume(said);
        Assert.AreEqual("line.0", AuricAria.Waiting[0].id, "cut short: said again next");
        Assert.AreEqual(4, AuricAria.Waiting.Count);
    }

    [Test]
    public void NothingEmptyIsSaid()
    {
        AuricAria.Say("line.empty", "");
        AuricAria.Announce("done.empty", "whisper only", null);
        Assert.AreEqual(0, AuricAria.Waiting.Count);
    }

    [Test]
    public void WordsStayLongEnoughToReadAndToHear()
    {
        var shortLine = AuricWords.Line("a", "Hush.");
        var longLine = AuricWords.Line("b", "Every hex hums a note of its own, survey here and listen to what it keeps for you.");
        var announcement = AuricWords.Announcement("c", "Listen", "Home");
        Assert.AreEqual(3.5f, AuricAria.HoldSeconds(shortLine, 0f), 1e-3f, "a short line still stays a moment");
        Assert.AreEqual(5.5f, AuricAria.HoldSeconds(announcement, 0f), 1e-3f, "an announcement stays longer");
        Assert.Greater(AuricAria.HoldSeconds(longLine, 0f), AuricAria.HoldSeconds(shortLine, 0f), "more words, longer");
        Assert.AreEqual(12.6f, AuricAria.HoldSeconds(shortLine, 12f), 1e-3f, "never cut before her voice ends");
        Assert.AreEqual(2f, AuricAria.HoldSeconds(AuricWords.Line("d", "Now.", 2f), 0f), 1e-3f, "its own seconds");

        AuricAria.VoiceSeconds = id => id == "a" ? 9f : 0f;
        Assert.AreEqual(9.6f, AuricAria.HoldSeconds(shortLine), 1e-3f, "the recorded voice is asked for");
    }

    [Test]
    public void StageDirectionsAreSetApart()
    {
        string styled = AuricWords.Styled("(sob) ... It's okay, sometimes things die...");
        StringAssert.StartsWith("<i>", styled);
        StringAssert.Contains("(sob)", styled);
        StringAssert.EndsWith(" ... It's okay, sometimes things die...", styled);
        Assert.AreEqual("No directions.", AuricWords.Styled("No directions."));
        Assert.AreEqual(string.Empty, AuricWords.Styled(null));
        Assert.AreEqual("Listen Home Every soul", AuricWords.Announcement("x", "Listen", "Home", "Every soul").Transcript);
    }

    private static string[] Ids()
    {
        var ids = new string[AuricAria.Waiting.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = AuricAria.Waiting[i].id;
        return ids;
    }
}
