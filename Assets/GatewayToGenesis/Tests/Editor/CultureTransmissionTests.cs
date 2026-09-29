using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Apprenticeships and cultural institutions (T06), with no scene (<see cref="TransmissionRules"/>): an order takes its
/// Sevenths and a seat, pauses without losing its progress when its teacher is away, completes once (a bearer, a variant
/// that keeps its parent, or a record that keeps no one practising), institutions stay within their Ages and grow into
/// their successors, and the whole record survives a save.
/// </summary>
public class CultureTransmissionTests
{
    private static readonly TransmissionTuning Tuning = new TransmissionTuning();

    private static TeachingDraft Song(string learner = "Vaelia", TeachingMode mode = TeachingMode.Preserve, int settlement = 1) => new TeachingDraft
    {
        tradition = "trad-1", traditionName = "The Evening Song", definition = "evening-song", food = false, mode = mode,
        teacherKind = TeacherKind.Legend, teacher = "Oren", teacherLabel = "Oren",
        learnerKind = mode == TeachingMode.Record ? LearnerKind.None : LearnerKind.Legend, learner = mode == TeachingMode.Record ? null : learner, learnerLabel = learner,
        institution = string.Empty, settlement = settlement, settlementName = "Ashford",
    };

    private static TeachingDraft Loaf(TeachingMode mode, string institution = "", CultureEntityRef dish = null) => new TeachingDraft
    {
        tradition = "trad-2", traditionName = "The Ash-Loaf Table", definition = "ash-loaf-table", food = true, mode = mode,
        teacherKind = TeacherKind.Community, teacher = "1", teacherLabel = "the people of Ashford",
        learnerKind = mode == TeachingMode.Record ? LearnerKind.None : LearnerKind.Community, learner = mode == TeachingMode.Record ? null : "2", learnerLabel = mode == TeachingMode.Record ? null : "Brightwater",
        institution = institution, settlement = 1, settlementName = "Ashford", recipe = dish ?? CultureEntityRef.Of(CultureEntityKind.Recipe, "ash-loaf", "Ash-Loaf"),
    };

    private static InstitutionSpec Hearth => Tuning.HearthSpec;

    private static string Why(TransmissionState s, TeachingDraft d, InstitutionSpec place = null) => TransmissionRules.WhyNotStart(s, Tuning, d, place ?? Hearth, null);

    [Test]
    public void AnOrderTakesItsSevenths_AndCompletesOnce_ABearer()
    {
        var s = new TransmissionState();
        Assert.IsNull(Why(s, Song()));
        var o = TransmissionRules.Start(s, Tuning, Song(), 10, "age-of-desolation");
        Assert.AreEqual("teach-1", o.id);
        Assert.AreEqual(1, o.complexity);
        Assert.AreEqual(Tuning.seventhsPerComplexity, o.required, "a simple song: one point of complexity");
        for (int now = 11; now < 11 + o.required - 1; now++) Assert.AreEqual(TeachingStep.Progressed, TransmissionRules.Advance(o, null, now));
        Assert.AreEqual(TeachingStep.None, TransmissionRules.Advance(o, null, 11 + o.required - 2), "the same Seventh counts once");
        Assert.AreEqual(TeachingStep.Ready, TransmissionRules.Advance(o, null, 11 + o.required - 1));
        string made = TransmissionRules.Complete(s, o, 20, "age-of-desolation");
        Assert.AreEqual("taught-1", made);
        var bearer = s.bearers.Single();
        Assert.AreEqual("Vaelia", bearer.who);
        Assert.AreEqual("Oren", bearer.taughtBy);
        Assert.AreEqual(TeachingStatus.Completed, o.status);
        Assert.IsNull(TransmissionRules.Complete(s, o, 21, "age-of-desolation"), "a completed order makes nothing again");
        Assert.AreEqual(TeachingStep.None, TransmissionRules.Advance(o, null, 22));
        Assert.AreEqual(1, s.bearers.Count);
        StringAssert.Contains("already carries", Why(s, Song()));
    }

    [Test]
    public void TeachingCompetesForARealSeat_APausedOrderKeepsIt_CancellingFreesIt()
    {
        var s = new TransmissionState();
        var first = TransmissionRules.Start(s, Tuning, Song("Vaelia"), 1, null);
        var other = Song("Isolde");
        other.teacher = other.teacherLabel = "Maren";
        StringAssert.Contains("Every seat", Why(s, other), "the hearth seats one teaching");
        TransmissionRules.Advance(first, "Oren is away with the Second Expedition.", 2);
        StringAssert.Contains("Every seat", Why(s, other), "a paused teaching keeps its seat");
        other.settlement = 2;
        other.settlementName = "Brightwater";
        Assert.IsNull(Why(s, other), "another settlement's hearth is free");
        Assert.IsTrue(TransmissionRules.Cancel(first, 3));
        other.settlement = 1;
        Assert.IsNull(Why(s, other), "cancelling frees the seat");
        Assert.IsFalse(TransmissionRules.Cancel(first, 4), "cancelled once");
        Assert.IsNull(TransmissionRules.Complete(s, first, 5, null), "a cancelled order makes nothing");
    }

    [Test]
    public void AnUnavailableTeacherPausesPredictably_AndTheOrderResumesWithItsProgress()
    {
        var s = new TransmissionState();
        var o = TransmissionRules.Start(s, Tuning, Song(), 1, null);
        TransmissionRules.Advance(o, null, 2);
        Assert.AreEqual(TeachingStep.Paused, TransmissionRules.Advance(o, "Oren is away with the Second Expedition.", 3));
        Assert.AreEqual(TeachingStatus.Paused, o.status);
        Assert.AreEqual(3, o.pausedSince);
        Assert.AreEqual(1, o.progress, "its progress is kept");
        Assert.AreEqual(TeachingStep.Waiting, TransmissionRules.Advance(o, "Oren is lost to Dissonance.", 4), "told once, then it waits");
        Assert.AreEqual("Oren is lost to Dissonance.", o.pausedReason);
        Assert.AreEqual(3, o.pausedSince);
        Assert.IsTrue(TransmissionRules.Reassign(o, TeacherKind.Community, "1", "the people of Ashford", Tuning));
        Assert.AreEqual(TeachingStep.Progressed, TransmissionRules.Advance(o, null, 5));
        Assert.AreEqual(TeachingStatus.Active, o.status);
        Assert.AreEqual(2, o.progress, "another teacher takes over where it stopped");
        Assert.IsNull(o.pausedReason);
    }

    [Test]
    public void ARecordIsNotAPractitioner_AndHelpsOnlySimplePracticesBeTaughtAgain()
    {
        var s = new TransmissionState();
        var flavorLog = Tuning.Institution("flavor-log");
        var ballads = Tuning.Institution("ballad-plays");
        StringAssert.Contains("keeps no records", Why(s, Song(mode: TeachingMode.Record)), "the hearth writes nothing down");
        var write = Song(mode: TeachingMode.Record);
        write.institution = "inst-1";
        Assert.IsNull(Why(s, write, ballads));
        var o = TransmissionRules.Start(s, Tuning, write, 1, null);
        for (int now = 2; TransmissionRules.Advance(o, null, now) != TeachingStep.Ready; now++) { }
        string record = TransmissionRules.Complete(s, o, 9, null, institutionName: "Guild of Ballads and Plays of Ashford");
        Assert.AreEqual("rec-1", record);
        Assert.IsEmpty(s.bearers, "writing it down teaches no one");
        Assert.AreEqual("Oren", s.records.Single().writtenFrom);
        StringAssert.Contains("already", Why(s, write, ballads), "written once at a place");

        // Taught from the record: a recovery, slower.
        var again = Song("Isolde");
        again.teacherKind = TeacherKind.Record; again.teacher = "rec-1"; again.teacherLabel = "the record";
        Assert.IsNull(Why(s, again));
        var recovery = TransmissionRules.Start(s, Tuning, again, 10, null);
        Assert.IsTrue(recovery.recovery);
        Assert.Greater(recovery.required, TransmissionRules.Required(Tuning, 1, false));

        // A complex practice (an adapted loaf) cannot be learned from a record alone.
        var loaf = Loaf(TeachingMode.Adapt, "inst-2");
        loaf.teacherKind = TeacherKind.Record; loaf.teacher = "rec-9";
        StringAssert.Contains("record alone", Why(s, loaf, flavorLog));
        var written = Loaf(TeachingMode.Record, "inst-2");
        written.teacherKind = TeacherKind.Record;
        StringAssert.Contains("not copied", Why(s, written, flavorLog));
    }

    [Test]
    public void AnAdaptedFormKeepsItsParent_AndItsOwnDish()
    {
        var s = new TransmissionState();
        var flavorLog = Tuning.Institution("flavor-log");
        var ember = CultureEntityRef.Of(CultureEntityKind.Recipe, "invented-ember-bread", "Ember Bread");
        StringAssert.Contains("Too complex for", Why(s, Loaf(TeachingMode.Adapt, dish: ember)), "adapting a loaf is beyond the hearth");
        StringAssert.Contains("does not teach", Why(s, Loaf(TeachingMode.Adapt, "inst-1", ember), Tuning.Institution("ballad-plays")));
        var draft = Loaf(TeachingMode.Adapt, "inst-1", ember);
        Assert.IsNull(Why(s, draft, flavorLog));
        var o = TransmissionRules.Start(s, Tuning, draft, 1, null);
        Assert.AreEqual(3, o.complexity);
        for (int now = 2; TransmissionRules.Advance(o, null, now) != TeachingStep.Ready; now++) { }
        string made = TransmissionRules.Complete(s, o, 20, "age-of-renewal", TransmissionRules.VariantName(o.traditionName, "Brightwater", "Ember Bread", "Ash-Loaf"));
        var v = s.Variant(made);
        Assert.AreEqual("trad-2", v.parent);
        Assert.AreEqual("ash-loaf-table", v.parentDefinition);
        Assert.AreEqual(2, v.settlement);
        Assert.AreEqual("Brightwater", v.settlementName);
        Assert.AreEqual("invented-ember-bread", v.recipe.id, "the variant's own dish, by id");
        Assert.AreEqual("The Ash-Loaf Table of Brightwater, with Ember Bread", v.name);
        Assert.AreEqual(v.id, s.bearers.Single().variant, "Brightwater's people keep their form");
        StringAssert.Contains("its own form", Why(s, Loaf(TeachingMode.Adapt, "inst-1", ember), flavorLog), "one local form per community");
    }

    [Test]
    public void LaterAgeGuildsCannotAppearEarly_AndAFlavorLogGrowsIntoACookingGuild()
    {
        var s = new TransmissionState();
        var flavorLog = Tuning.Institution("flavor-log");
        var guild = Tuning.Institution("cooking-guild");
        StringAssert.Contains("Ages I-III", TransmissionRules.WhyNotFound(s, flavorLog, "feast-hall", "3:feast-hall", 0), "no Flavor Log in the Age of Desolation");
        Assert.IsNull(TransmissionRules.WhyNotFound(s, flavorLog, "feast-hall", "3:feast-hall", 1));
        StringAssert.Contains("meets in a", TransmissionRules.WhyNotFound(s, flavorLog, "song-hall", "3:song-hall", 1));
        for (int age = 0; age < 4; age++) Assert.IsNotNull(TransmissionRules.WhyNotFound(s, guild, "feast-hall", "3:feast-hall", age), $"no Cooking Guild in Age {age}");
        StringAssert.Contains("needs no founding", TransmissionRules.WhyNotFound(s, Tuning.HearthSpec, null, null, 0));

        var log = TransmissionRules.Found(s, flavorLog, "3:feast-hall", 3, "Ashford", null, 5, "age-of-renewal");
        Assert.AreEqual("Flavor Log of Ashford", log.name);
        StringAssert.Contains("already", TransmissionRules.WhyNotFound(s, flavorLog, "feast-hall", "3:feast-hall", 1));
        var other = new InstitutionSpec { id = "other", name = "Another", venues = { "feast-hall" }, minAge = 0 };
        StringAssert.Contains("one institution", TransmissionRules.WhyNotFound(s, other, "feast-hall", "3:feast-hall", 2), "never two at one landmark");
        var d = Loaf(TeachingMode.Preserve, log.id);
        var o = TransmissionRules.Start(s, Tuning, d, 6, null);
        TransmissionRules.Advance(o, null, 7);

        Assert.IsNull(TransmissionRules.WhyNotFound(s, guild, "feast-hall", "3:feast-hall", 4), "the Flavor Log standing there may grow into the guild");
        var grown = TransmissionRules.Found(s, guild, "3:feast-hall", 3, "Ashford", null, 30, "age-of-embers");
        Assert.AreEqual(grown.id, log.evolvedInto);
        Assert.AreEqual(log.id, grown.evolvedFrom);
        Assert.AreEqual(grown.id, o.institution, "its teaching moves over");
        Assert.AreEqual(1, o.progress, "with its progress");
        Assert.AreEqual(grown, TransmissionRules.At(s, "3:feast-hall").Single());
    }

    [Test]
    public void OnePersonTeachesOneThingAtATime_AndNoOneTeachesThemselves()
    {
        var s = new TransmissionState();
        TransmissionRules.Start(s, Tuning, Song("Vaelia"), 1, null);
        var busy = Song("Isolde", settlement: 2);
        StringAssert.Contains("already teaching", Why(s, busy));
        var pupil = Song("Vaelia", settlement: 2);
        pupil.teacher = pupil.teacherLabel = "Maren";
        StringAssert.Contains("already busy", Why(s, pupil));
        var self = Song("Maren", settlement: 3);
        self.teacher = self.teacherLabel = "Maren";
        StringAssert.Contains("themselves", Why(s, self));
        // A village may make its own form of what it keeps.
        var own = Loaf(TeachingMode.Adapt, "inst-1");
        own.learner = "1"; own.learnerLabel = "Ashford";
        Assert.IsNull(Why(s, own, Tuning.Institution("flavor-log")));
        var again = Loaf(TeachingMode.Preserve);
        again.learner = "1"; again.learnerLabel = "Ashford";
        again.settlement = 4; again.settlementName = "Elsewhere";
        StringAssert.Contains("keeps it already", Why(s, again));
    }

    [Test]
    public void Save_TheTeachingRoundTripsInsideTheCultureState_AndOlderEnvelopesLoad()
    {
        var culture = new CultureState();
        CultureMigration.Ensure(culture);
        var s = culture.extensions.transmission;
        var log = TransmissionRules.Found(s, Tuning.Institution("flavor-log"), "3:feast-hall", 3, "Ashford", null, 5, "age-of-renewal");
        var ember = CultureEntityRef.Of(CultureEntityKind.Recipe, "invented-ember-bread", "Ember Bread");
        var done = TransmissionRules.Start(s, Tuning, Loaf(TeachingMode.Adapt, log.id, ember), 6, "age-of-renewal");
        for (int now = 7; TransmissionRules.Advance(done, null, now) != TeachingStep.Ready; now++) { }
        TransmissionRules.Complete(s, done, 20, "age-of-renewal", "The Ash-Loaf Table of Brightwater, with Ember Bread");
        var open = TransmissionRules.Start(s, Tuning, Song(), 21, "age-of-renewal");
        TransmissionRules.Advance(open, "Oren is away with the Second Expedition.", 22);

        var loaded = (CultureState)SaveStateCodec.Read(SaveStateCodec.Write(culture, typeof(CultureState)), typeof(CultureState));
        var t = loaded.extensions.transmission;
        TransmissionRules.Ensure(t);
        Assert.AreEqual(2, t.orders.Count);
        Assert.AreEqual(TeachingStatus.Completed, t.Order(done.id).status);
        Assert.IsNull(TransmissionRules.Complete(t, t.Order(done.id), 30, null), "a reload never completes an order twice");
        Assert.AreEqual(1, t.variants.Count);
        Assert.AreEqual("invented-ember-bread", t.variants[0].recipe.id, "the custom recipe is saved by its id");
        Assert.AreEqual(TeachingStatus.Paused, t.Order(open.id).status);
        Assert.AreEqual("Oren is away with the Second Expedition.", t.Order(open.id).pausedReason);
        Assert.AreEqual(log.id, t.institutions.Single().id);
        var next = TransmissionRules.Start(t, Tuning, Song("Isolde", settlement: 2), 31, null);
        Assert.AreEqual("teach-3", next.id, "ids are never given twice");

        // An envelope saved before teaching existed loads with a fresh, empty record.
        var older = SaveStateCodec.Write(new CultureExtensionState(), typeof(CultureExtensionState));
        older.children.RemoveAll(c => c.name == "transmission");
        var envelope = (CultureExtensionState)SaveStateCodec.Read(older, typeof(CultureExtensionState));
        Assert.IsNotNull(envelope.transmission);
        Assert.IsEmpty(envelope.transmission.orders);
    }

    [Test]
    public void FinishedOrdersAreBounded_WhatTheyMadeIsKept_AndIdsAreNeverReused()
    {
        var s = new TransmissionState();
        var tuning = new TransmissionTuning { finishedOrdersKept = 2 };
        for (int i = 0; i < 5; i++)
        {
            var o = TransmissionRules.Start(s, tuning, Song("Learner" + i), i * 10, null);
            for (int now = i * 10 + 1; TransmissionRules.Advance(o, null, now) != TeachingStep.Ready; now++) { }
            TransmissionRules.Complete(s, o, i * 10 + 5, null);
            TransmissionRules.Trim(s, tuning);
        }
        Assert.AreEqual(2, s.orders.Count);
        Assert.AreEqual(5, s.bearers.Count, "every bearer taught stays");
        s.nextOrder = 1;
        TransmissionRules.Ensure(s);
        Assert.AreEqual(6, s.nextOrder, "a stale counter never reuses a kept id");
    }

    [Test]
    public void TheAuthoredInstitutionsMeetInRealLandmarks_AndNameTheirAgesAndCanon()
    {
        var life = new CultureLifeTuning();
        var traditions = new TraditionTuning();
        Assert.IsTrue(Tuning.HearthSpec.informal && Tuning.HearthSpec.food && Tuning.HearthSpec.other, "the hearth teaches everything, from the first Age");
        Assert.AreEqual(0, Tuning.HearthSpec.minAge);
        foreach (var spec in Tuning.institutions.Where(i => !i.informal))
        {
            Assert.IsNotEmpty(spec.venues, spec.id);
            foreach (var venue in spec.venues) Assert.IsNotNull(life.Landmark(venue), $"{spec.id} meets in {venue}");
            Assert.Greater(spec.minAge, 0, $"{spec.id} is a named institution: not in the Age of Desolation");
            Assert.IsTrue(spec.keepsRecords, spec.id);
            Assert.AreEqual(CanonStatus.ExplicitCanon, spec.canon, spec.id);
            StringAssert.Contains("Civic.md", spec.canonNote + spec.vault);
            if (!string.IsNullOrEmpty(spec.evolvesFrom)) Assert.IsNotNull(Tuning.Institution(spec.evolvesFrom), spec.id);
        }
        Assert.IsTrue(Tuning.institutions.Any(i => !i.informal && i.food), "a culinary institution");
        Assert.IsTrue(Tuning.institutions.Any(i => !i.informal && i.other), "a song institution");
        foreach (var c in Tuning.complexity) Assert.IsNotNull(traditions.Definition(c.definition), c.definition);
        Assert.AreEqual(1, Tuning.ComplexityOf("evening-song", false));
        Assert.AreEqual(2, Tuning.ComplexityOf("ash-loaf-table", true));
        Assert.AreEqual(Tuning.foodComplexity, Tuning.ComplexityOf("an-unlisted-dish", true));
    }

    [Test]
    public void ContentValidationRejectsBrokenTeachingReferences()
    {
        var tuning = new TransmissionTuning();
        var problems = new List<string>();
        ContentValidator.ValidateTransmission(problems, tuning, new TraditionTuning(), new CultureLifeTuning());
        Assert.IsEmpty(problems);
        tuning.Institution("flavor-log").venues.Add("missing-hall");
        tuning.complexity.Add(new PracticeComplexity { definition = "missing-practice" });
        ContentValidator.ValidateTransmission(problems, tuning, new TraditionTuning(), new CultureLifeTuning());
        Assert.IsTrue(problems.Any(p => p.Contains("missing-hall")));
        Assert.IsTrue(problems.Any(p => p.Contains("missing-practice")));
    }
}
