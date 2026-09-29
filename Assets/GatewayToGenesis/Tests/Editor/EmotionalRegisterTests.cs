using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Emotional Register with no scene (<see cref="EmotionalRegister"/>, <see cref="EmotionalProfile"/>,
/// <see cref="WorldSuffering"/>): sixteen notes on eight axes, a healthy and a wounded face each; a mass hatches into the
/// Path whose hunger its feeding resembles (joy makes a Discant; mixed feelings make hybrids, the common birth); a people
/// gives off consonance when it thrives and dissonance when it suffers; consonance soothes the land; healers transmute
/// wounds into their healthy pair and an Atonalis feeding turns the healthy notes it preys on into wounds; the meter.
/// </summary>
public class EmotionalRegisterTests
{
    private static EmotionalRegister Of(params (Feeling f, float v)[] parts)
    {
        var r = new EmotionalRegister();
        foreach (var (f, v) in parts) r.Add(f, v);
        return r;
    }

    [Test]
    public void SixteenNotes_OnEightAxes_EachAHealthyAndAWoundedFace()
    {
        Assert.AreEqual(16, EmotionalRegister.All.Length);
        Assert.AreEqual(8, EmotionalRegister.Dissonant.Length);
        Assert.AreEqual(Feeling.Courage, EmotionalRegister.Pair(Feeling.Dread));
        Assert.AreEqual(Feeling.Dread, EmotionalRegister.Pair(Feeling.Courage));
        Assert.AreEqual(Feeling.Joy, EmotionalRegister.Pair(Feeling.Tumult));
        Assert.AreEqual(Feeling.Love, EmotionalRegister.Pair(Feeling.Longing));
        Assert.AreEqual(Polarity.Consonance, EmotionalRegister.PolarityOf(Feeling.Pride));
        Assert.AreEqual(EmotionalRegister.Axis(Feeling.Shame), EmotionalRegister.Axis(Feeling.Pride), "a pair lies on one axis");
        foreach (var f in EmotionalRegister.Dissonant)
            Assert.AreEqual(EmotionalProfile.PathOf(f), EmotionalProfile.Match(Of((f, 1f))).path, $"fed only {f}, it walks its own Path");
        var r = Of((Feeling.Joy, 0.3f), (Feeling.Pain, 0.1f));
        Assert.AreEqual(0.75f, r.Harmony, 1e-4f, "three quarters consonant");
    }

    [Test]
    public void TooMuchJoyMakesADiscant_AndMixedFeelingsMakeHybrids()
    {
        Assert.AreEqual(AtonalPath.Discant, EmotionalProfile.Match(Of((Feeling.Joy, 1f))).path, "a Discant preys on joy");
        Assert.AreEqual(AtonalPath.Discant, EmotionalProfile.Match(Of((Feeling.Joy, 0.6f), (Feeling.Tumult, 0.3f))).path, "a revel's high and its crash");
        Assert.AreEqual(AtonalPath.Erosyx, EmotionalProfile.Match(Of((Feeling.Longing, 0.5f), (Feeling.Vitality, 0.5f))).path, "desire draws the pleasure parasites' Path");
        var (path, hybrid) = EmotionalProfile.Match(Of((Feeling.Pain, 0.5f), (Feeling.Tumult, 0.5f)));
        CollectionAssert.AreEquivalent(new[] { AtonalPath.Carnalix, AtonalPath.Discant }, new[] { path, hybrid }, "tears and despair: a hybrid");
        Assert.AreEqual("Carnalix-Discant", WorldSuffering.PathName(AtonalPath.Carnalix, AtonalPath.Discant));
        // The land's feelings are always mixed: across many mixes of three notes, hybrids are the common birth, purists rarer.
        int hybrids = 0, purists = 0;
        var notes = EmotionalRegister.All;
        for (int a = 0; a < notes.Length; a++)
            for (int b = a + 1; b < notes.Length; b++)
                for (int c = b + 1; c < notes.Length; c += 3)
                {
                    var (_, h) = EmotionalProfile.Match(Of((notes[a], 0.5f), (notes[b], 0.3f), (notes[c], 0.2f)));
                    if (h != AtonalPath.None) hybrids++; else purists++;
                }
        Assert.Greater(hybrids, purists, $"hybrids {hybrids}, purists {purists}");
        Assert.Greater(purists, 0, "but purists exist");
    }

    [Test]
    public void APeopleGivesOffConsonanceWhenItThrives_AndDissonanceWhenItSuffers()
    {
        var content = WorldSuffering.SettlementFeelings(new SettlementMood { strain = 5f, happiness = 85f, joy = 0.5f, share = 1f, development = 60f, holidays = 2 });
        Assert.Greater(content.Harmony, 0.8f, "a thriving people's feelings are whole");
        Assert.Greater(content.joy, 0f);
        Assert.Greater(content.belonging, 0f);
        Assert.Greater(content.pride, 0f, "pride in what they built");
        Assert.Greater(content.devotion, 0f, "devotion in the holidays they keep");
        var besieged = WorldSuffering.SettlementFeelings(new SettlementMood { strain = 80f, harmedBy = WorldRuins.Danger, happiness = 45f, share = 1f });
        Assert.AreEqual(Feeling.Dread, besieged.Dominant, "hunters at the gates: dread");
        Assert.Less(besieged.Harmony, 0.3f);
        var pillaged = WorldSuffering.SettlementFeelings(new SettlementMood { strain = 80f, harmedBy = WorldRuins.Pillage, happiness = 45f, share = 1f });
        Assert.AreEqual(Feeling.Pain, pillaged.Dominant);
        Assert.Greater(pillaged.shame, 0f);
        var starving = WorldSuffering.SettlementFeelings(new SettlementMood { strain = 10f, nutrition = 1f, disease = 0.5f, happiness = 45f, share = 1f });
        Assert.AreEqual(Feeling.Pain, starving.Dominant, "hunger and sickness are the body's pain");
        Assert.AreEqual(0f, starving.vitality, 1e-5f, "and no vitality");
        var despairing = WorldSuffering.SettlementFeelings(new SettlementMood { strain = 10f, happiness = 5f, share = 1f });
        Assert.AreEqual(Feeling.Tumult, despairing.Dominant, "despair");
        var revels = WorldSuffering.SettlementFeelings(new SettlementMood { district = "indulgent", happiness = 60f, share = 0.35f });
        Assert.Greater(revels.longing, 0.2f, "the Indulgent District's revels: desire");
        Assert.Greater(revels.vitality, 0.2f);
    }

    [Test]
    public void ConsonanceSoothesTheLand()
    {
        Assert.AreEqual(0.6f, WorldSuffering.SufferingOf(Of((Feeling.Pain, 0.6f))), 1e-5f);
        Assert.AreEqual(0.4f, WorldSuffering.SufferingOf(Of((Feeling.Pain, 0.6f), (Feeling.Vitality, 0.4f))), 1e-5f, "half the healthy feeling offsets the wounds");
        Assert.AreEqual(0f, WorldSuffering.SufferingOf(Of((Feeling.Joy, 1f))), "joy alone presses no mass up");
        var t = new WorldTile { dissonance = 0.75f };
        Assert.AreEqual(0.6f, WorldSuffering.Feel(t, Feeling.Doubt), 1e-4f, "the torn Loom reads as Doubt");
    }

    [Test]
    public void HealersTransmuteWounds_AndAtonalisTurnHealthIntoWounds()
    {
        var scars = new List<SufferingScar> { new SufferingScar { cell = 0, feelings = Of((Feeling.Tumult, 0.4f), (Feeling.Love, 0.2f)) } };
        var sorrowbells = new List<FeelingWeight> { new FeelingWeight { feeling = Feeling.Tumult, weight = 50 }, new FeelingWeight { feeling = Feeling.Love, weight = 50 } };
        float drunk = WorldSuffering.Drink(scars, new[] { 0 }, sorrowbells, true, 1f, 4f);
        Assert.Greater(drunk, 0f);
        Assert.Less(scars[0].feelings.tumult, 0.4f, "the healer drinks the despair");
        Assert.Greater(scars[0].feelings.joy, 0f, "and breathes part of it back out as joy");
        var listener = new List<SufferingScar> { new SufferingScar { cell = 0, feelings = Of((Feeling.Tumult, 0.4f)) } };
        WorldSuffering.Drink(listener, new[] { 0 }, sorrowbells, false, 1f, 4f);
        Assert.AreEqual(0f, listener[0].feelings.joy, "a listener only drinks");

        var festival = new SufferingScar { cell = 0, feelings = Of((Feeling.Joy, 0.5f)) };
        WorldSuffering.Prey(festival, AtonalPath.Discant, AtonalPath.None, 4f);
        Assert.Less(festival.feelings.joy, 0.5f, "a Discant feeds on the festival's joy");
        Assert.Greater(festival.feelings.tumult, 0f, "and turns it into tumult");
        Assert.AreEqual(0.5f, festival.feelings.Total, 1e-4f, "transmuted, not destroyed");
    }

    [Test]
    public void TheMeterPairsEachHealthyFaceWithItsWound()
    {
        var r = Of((Feeling.Courage, 0.2f), (Feeling.Dread, 0.6f), (Feeling.Love, 0.2f));
        string meter = WorldSuffering.Meter(r);
        var lines = meter.Split('\n');
        Assert.AreEqual(2, lines.Length, "one line per axis that holds anything");
        StringAssert.Contains("Courage", lines[0]);
        StringAssert.Contains("Dread", lines[0]);
        StringAssert.Contains("Love", lines[1]);
        StringAssert.Contains("Longing", lines[1], "a pair shows even when empty");
        Assert.IsEmpty(WorldSuffering.Meter(new EmotionalRegister()));
        StringAssert.Contains("Dread 60%", r.Words(), "the register in words, culture-proof");
    }
}
