using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The observances' calendar and rules with no scene (T04, ObservanceRules): dates on the game's own clock, Echo and
/// Cycle rollover, occasions decided once (a time jump misses, never double-keeps), postponement, holidays linked on
/// their own day, the vigil's full Moon and grove, a quiet remembrance without feast or high spirits, preparation windows.
/// </summary>
public class ObservanceTests
{
    private static long D(int cycle, int echo, int phase, int seventh) => ObservanceCalendar.Date(cycle, echo, phase, seventh);

    private static Observance EveryEcho(int seventh, int phase, long lastResolved) =>
        new Observance { id = "obs-1", name = "Test Day", recurrence = ObservanceRecurrence.EveryEcho, seventh = seventh, phase = phase, echo = 1, lastResolved = lastResolved };

    private static ObservanceDefinition Def(string id) => new ObservanceTuning().Definition(id);

    private static ObservanceChecks Checks(string ground = "", bool causeKnown = false, float held = 100f, int age = 0) => new ObservanceChecks
    {
        age = age, groundOf = _ => ground, causeKnown = _ => causeKnown, held = _ => held,
    };

    // ===== THE CALENDAR =====

    [Test]
    public void ADate_RoundTripsThroughItsParts()
    {
        Assert.AreEqual(0, D(1, 1, 1, 1));
        Assert.AreEqual(ObservanceCalendar.PerCycle, D(2, 1, 1, 1));
        var p = ObservanceCalendar.Parts(D(3, 4, 2, 21));
        Assert.AreEqual((3, 4, 2, 21), p);
        Assert.AreEqual(D(3, 4, 2, 21), new CultureStamp { cycle = 3, echo = 4, phase = 2, seventh = 21 }.Absolute, "the same index as every culture stamp");
    }

    [Test]
    public void EachRecurrence_FallsOnItsOwnDays()
    {
        var echo = EveryEcho(5, 2, -1);
        Assert.IsTrue(ObservanceCalendar.Falls(echo, D(1, 1, 2, 5)));
        Assert.IsTrue(ObservanceCalendar.Falls(echo, D(4, 3, 2, 5)));
        Assert.IsFalse(ObservanceCalendar.Falls(echo, D(1, 1, 1, 5)));

        var cycle = new Observance { recurrence = ObservanceRecurrence.OncePerCycle, seventh = 3, phase = 1, echo = 2 };
        Assert.IsTrue(ObservanceCalendar.Falls(cycle, D(1, 2, 1, 3)));
        Assert.IsFalse(ObservanceCalendar.Falls(cycle, D(1, 1, 1, 3)), "only in its own Echo");

        var moon = new Observance { recurrence = ObservanceRecurrence.RitualSeventh, seventh = 21, phase = 0 };
        Assert.IsTrue(ObservanceCalendar.Falls(moon, D(1, 1, 1, 21)));
        Assert.IsTrue(ObservanceCalendar.Falls(moon, D(1, 3, 3, 21)));
        Assert.IsFalse(ObservanceCalendar.Falls(moon, D(1, 1, 1, 20)), "only under the full Moon");
    }

    [Test]
    public void NextDays_RollOverTheEchoAndTheCycle()
    {
        Assert.AreEqual(D(2, 1, 1, 3), ObservanceCalendar.NextOn(EveryEcho(3, 1, -1), D(1, 4, 3, 10)), "past the last Echo: the next Cycle's first");
        var harvest = new Observance { recurrence = ObservanceRecurrence.OncePerCycle, seventh = 3, phase = 1, echo = 2 };
        Assert.AreEqual(D(2, 2, 1, 3), ObservanceCalendar.NextOn(harvest, D(1, 2, 1, 4)), "missed this Cycle's: the next Cycle's");
        Assert.AreEqual(D(1, 2, 1, 3), ObservanceCalendar.NextOn(harvest, D(1, 2, 1, 3)), "inclusive");
        var moon = new Observance { recurrence = ObservanceRecurrence.RitualSeventh, seventh = 21, phase = 3 };
        Assert.AreEqual(D(1, 1, 3, 21), ObservanceCalendar.NextOn(moon, D(1, 1, 1, 1)));
        Assert.AreEqual(D(1, 2, 3, 21), ObservanceCalendar.NextOn(moon, D(1, 1, 3, 21) + 1), "after its Phase's Moon, the next Echo's");
        Assert.AreEqual(-1, ObservanceCalendar.NextOn(new Observance { recurrence = ObservanceRecurrence.EveryEcho, seventh = 30, phase = 1 }, 0), "a malformed day never falls");
    }

    // ===== OCCASIONS DECIDED ONCE =====

    [Test]
    public void ATimeJump_MissesThePassedDays_KeepsToday_AndNothingIsDecidedTwice()
    {
        var s = new ObservanceState();
        var o = EveryEcho(5, 1, D(1, 1, 1, 1));
        s.observances.Add(o);
        long today = D(1, 3, 1, 5);
        var due = ObservanceRules.Due(o, today);
        CollectionAssert.AreEqual(new[] { D(1, 1, 1, 5), D(1, 2, 1, 5), D(1, 3, 1, 5) }, due.Select(x => x.original).ToArray());
        CollectionAssert.AreEqual(new[] { false, false, true }, due.Select(x => x.keep).ToArray(), "only today's is kept; the jumped-over ones pass unkept");
        foreach (var (original, _, keep) in due)
            ObservanceRules.Resolve(s, o, original, new ObservanceOccasion { outcome = keep ? ObservanceOutcome.Kept : ObservanceOutcome.Missed }, new ObservanceTuning());
        Assert.AreEqual(1, o.kept);
        Assert.AreEqual(2, o.missed);
        Assert.AreEqual(today, o.lastResolved);
        Assert.IsEmpty(ObservanceRules.Due(o, today), "a second pass the same day decides nothing");
        ObservanceRules.Resolve(s, o, D(1, 1, 1, 5), null, null);
        Assert.AreEqual(today, o.lastResolved, "never moves back");
        Assert.AreEqual(3, s.history.Count);
    }

    [Test]
    public void TheHistory_IsBounded_WhileTheTalliesKeepEverything()
    {
        var s = new ObservanceState();
        var o = EveryEcho(1, 1, -1);
        var t = new ObservanceTuning { historyKept = 3 };
        for (int i = 0; i < 5; i++) ObservanceRules.Resolve(s, o, i * ObservanceCalendar.PerEcho, new ObservanceOccasion { outcome = ObservanceOutcome.Kept }, t);
        Assert.AreEqual(3, s.history.Count);
        Assert.AreEqual(5, o.kept);
    }

    [Test]
    public void APostponement_WaitsForItsNewDay_OnceOnly_AndNeverIntoTheNextOccasion()
    {
        var t = new ObservanceTuning();
        long today = D(1, 1, 1, 2);
        var o = EveryEcho(5, 1, today);
        var d = Def("first-golden-fruit");
        Assert.IsNull(ObservanceRules.WhyNotPostpone(o, d, today, 3, t));
        StringAssert.Contains("1 to", ObservanceRules.WhyNotPostpone(o, d, today, t.postponeMax + 1, t));
        ObservanceRules.Postpone(o, today, 3);
        Assert.AreEqual((D(1, 1, 1, 8), D(1, 1, 1, 5)), ObservanceCalendar.NextOccasion(o, today));
        StringAssert.Contains("already moved", ObservanceRules.WhyNotPostpone(o, d, today, 1, t));
        Assert.IsEmpty(ObservanceRules.Due(o, D(1, 1, 1, 5)), "its own day passes: it waits");
        Assert.AreEqual((D(1, 1, 1, 8), D(1, 1, 1, 5)), ObservanceCalendar.NextOccasion(o, D(1, 1, 1, 6)));
        var due = ObservanceRules.Due(o, D(1, 1, 1, 8));
        Assert.AreEqual(1, due.Count);
        Assert.AreEqual((D(1, 1, 1, 5), D(1, 1, 1, 8), true), due[0]);
        ObservanceRules.Resolve(null, o, due[0].original, new ObservanceOccasion { outcome = ObservanceOutcome.Kept }, t);
        Assert.AreEqual(-1, o.postponedFrom, "the move is spent");
        Assert.AreEqual(D(1, 2, 1, 5), ObservanceCalendar.NextOccasion(o, D(1, 1, 1, 8)).keepOn, "the next Echo keeps its own day");

        var far = new ObservanceTuning { postponeMax = 100 };
        StringAssert.Contains("next occasion", ObservanceRules.WhyNotPostpone(EveryEcho(5, 1, today), d, today, ObservanceCalendar.PerEcho, far));
        var moon = new Observance { name = "Moonlit Vigil", recurrence = ObservanceRecurrence.RitualSeventh, seventh = 21, lastResolved = today };
        StringAssert.Contains("full Moon", ObservanceRules.WhyNotPostpone(moon, Def("moonlit-vigil"), today, 1, t));
    }

    [Test]
    public void APostponementMissedByATimeJump_IsDecidedOnce()
    {
        long today = D(1, 1, 1, 2);
        var o = EveryEcho(5, 1, today);
        ObservanceRules.Postpone(o, today, 3);
        var due = ObservanceRules.Due(o, D(1, 1, 1, 12));
        Assert.AreEqual(1, due.Count, "its moved day passed unseen");
        Assert.IsFalse(due[0].keep);
        ObservanceRules.Resolve(null, o, due[0].original, new ObservanceOccasion { outcome = ObservanceOutcome.Missed }, null);
        Assert.IsEmpty(ObservanceRules.Due(o, D(1, 1, 1, 12)));
        Assert.AreEqual(1, o.missed);
    }

    // ===== HOLIDAYS =====

    [Test]
    public void Holidays_AreLinkedOnTheirOwnDay_Once()
    {
        var s = new ObservanceState();
        var holidays = new List<Holiday> { new Holiday { name = "The Day of Ash", occasion = "the Hunger", seventh = 7, phase = 2, echo = 3, cycle = 1, kept = 3 } };
        var now = new CultureStamp { cycle = 2, echo = 1, phase = 1, seventh = 4 };
        Assert.AreEqual(1, ObservanceRules.LinkHolidays(s, holidays, now));
        Assert.AreEqual(0, ObservanceRules.LinkHolidays(s, holidays, now), "idempotent");
        var o = s.observances.Single();
        Assert.IsTrue(o.legacy);
        Assert.IsFalse(o.customized);
        Assert.AreEqual(ObservanceRules.Anniversary, o.definition);
        Assert.AreEqual(ObservanceRecurrence.EveryEcho, o.recurrence);
        Assert.AreEqual((7, 2), (o.seventh, o.phase), "the holiday keeps its date");
        Assert.AreEqual(3, o.kept, "its tally, nothing invented");
        Assert.AreEqual(now.Absolute, o.lastResolved, "decided from today on: its past is not replayed");
        Assert.AreEqual(D(2, 1, 2, 7), ObservanceCalendar.NextOccasion(o, now.Absolute).keepOn);
        holidays[0].kept = 4;
        ObservanceRules.LinkHolidays(s, holidays, now);
        Assert.AreEqual(4, s.observances.Single().kept);
    }

    [Test]
    public void Ensure_RepairsAnOldOrPartialState()
    {
        var s = new ObservanceState { nextId = 1, observances = { new Observance { id = "obs-7", venue = null, cause = null, food = null }, null } };
        s.preparations.Add(new ObservancePreparation { observance = "obs-99" });
        s.preparations.Add(new ObservancePreparation { observance = "obs-7", reserved = null, food = null });
        ObservanceRules.Ensure(s);
        Assert.AreEqual(8, s.nextId, "ids are never reused");
        Assert.AreEqual(1, s.observances.Count);
        Assert.IsNotNull(s.observances[0].food);
        Assert.AreEqual(1, s.preparations.Count, "a table for an unknown observance is dropped");
        Assert.IsNotNull(s.preparations[0].reserved);
        Assert.AreEqual(ObservanceState.CurrentVersion, s.version);
    }

    // ===== ESTABLISHING =====

    [Test]
    public void TheVigil_KeepsToTheFullMoon_AndItsGrove()
    {
        var d = Def("moonlit-vigil");
        var t = new ObservanceTuning();
        var s = new ObservanceState();
        var plan = new ObservancePlan { definition = d.id, recurrence = ObservanceRecurrence.EveryEcho, seventh = 5, phase = 1, settlement = 2 };
        StringAssert.Contains("every Echo", ObservanceRules.WhyNotEstablish(d, plan, Checks("glimmerfern"), s, t));
        plan.recurrence = ObservanceRecurrence.RitualSeventh;
        plan.phase = 0;
        plan.settlement = -1;
        StringAssert.Contains("kept in a place", ObservanceRules.WhyNotEstablish(d, plan, Checks("glimmerfern"), s, t));
        plan.settlement = 2;
        StringAssert.Contains("Glimmerfern", ObservanceRules.WhyNotEstablish(d, plan, Checks(" plains grassland flat"), s, t));
        StringAssert.Contains("no longer stands", ObservanceRules.WhyNotEstablish(d, plan, Checks(null), s, t));
        plan.scale = ObservanceScale.Full;
        StringAssert.Contains("not kept at a full table", ObservanceRules.WhyNotEstablish(d, plan, Checks(" forest glimmerfern grove"), s, t));
        plan.scale = ObservanceScale.Quiet;
        Assert.IsNull(ObservanceRules.WhyNotEstablish(d, plan, Checks(" forest glimmerfern grove"), s, t));
        StringAssert.Contains("no longer be newly set apart", ObservanceRules.WhyNotEstablish(d, plan, Checks(" glimmerfern", age: 4), s, t), "not offered after its Ages");

        var o = ObservanceRules.Establish(s, d, plan, null, new CultureStamp { cycle = 1, echo = 1, phase = 1, seventh = 3 });
        Assert.AreEqual((21, 0), (o.seventh, o.phase), "every Ritual Seventh");
        Assert.AreEqual(D(1, 1, 1, 21), ObservanceCalendar.NextOccasion(o, D(1, 1, 1, 3)).keepOn);
        StringAssert.Contains("already kept there", ObservanceRules.WhyNotEstablish(d, plan, Checks(" glimmerfern"), s, t));
        plan.settlement = 5;
        Assert.IsNull(ObservanceRules.WhyNotEstablish(d, plan, Checks(" glimmerfern"), s, t), "another grove keeps its own");
    }

    [Test]
    public void AQuietRemembrance_NeedsACause_ButNoFeastNoSpiritsNoUnity()
    {
        var d = Def("day-of-remembrance");
        var t = new ObservanceTuning();
        var plan = new ObservancePlan { definition = d.id, recurrence = ObservanceRecurrence.EveryEcho, seventh = 9, phase = 3, scale = ObservanceScale.Quiet };
        StringAssert.Contains("remembers a loss", ObservanceRules.WhyNotEstablish(d, plan, Checks(held: 0f), new ObservanceState(), t));
        plan.cause = CultureEntityRef.Of(CultureEntityKind.Evidence, "ev-1", "The fall of Ashford");
        StringAssert.Contains("remembers a loss", ObservanceRules.WhyNotEstablish(d, plan, Checks(held: 0f, causeKnown: false), new ObservanceState(), t), "a cause the people do not remember");
        Assert.IsNull(ObservanceRules.WhyNotEstablish(d, plan, Checks(held: 0f, causeKnown: true), new ObservanceState(), t), "no Unity, no morale gate, nothing held at all");
        plan.scale = ObservanceScale.Full;
        StringAssert.Contains("not kept at a full table", ObservanceRules.WhyNotEstablish(d, plan, Checks(causeKnown: true), new ObservanceState(), t), "grief sets no feast");

        Assert.AreEqual(0f, ObservanceRules.FeastValue(t, 500, ObservanceScale.Quiet), "a quiet day asks nothing of the stores");
        var quiet = ObservanceRules.Rewards(t, ObservanceObjective.Remembrance, ObservanceScale.Quiet);
        Assert.AreEqual(0f, quiet.joy, "grief is not joy");
        Assert.AreEqual(0, quiet.morale);
        Assert.Greater(quiet.unity, 0f, "the people drawn together");
        Assert.Greater(ObservanceRules.Rewards(t, ObservanceObjective.Celebration, ObservanceScale.Full).joy, 0f);
    }

    [Test]
    public void Establishing_ChecksTheAgeTheCostTheRiteTheFoodAndTheCalendarsRoom()
    {
        var t = new ObservanceTuning { maxObservances = 1 };
        var fruit = Def("first-golden-fruit");
        var plan = new ObservancePlan { definition = fruit.id, recurrence = ObservanceRecurrence.OncePerCycle, seventh = 4, phase = 2, echo = 5, scale = ObservanceScale.Full };
        StringAssert.Contains("four Echoes", ObservanceRules.WhyNotEstablish(fruit, plan, Checks(), new ObservanceState(), t));
        plan.echo = 2;
        StringAssert.Contains("no longer be newly set apart", ObservanceRules.WhyNotEstablish(fruit, plan, Checks(age: 1), new ObservanceState(), t), "the first golden fruit is Age 0's");
        StringAssert.Contains("Needs 10 Unity", ObservanceRules.WhyNotEstablish(fruit, plan, Checks(held: 4f), new ObservanceState(), t));
        plan.repertoire = "remembrance";
        StringAssert.Contains("not kept with that rite", ObservanceRules.WhyNotEstablish(fruit, plan, Checks(), new ObservanceState(), t));
        plan.repertoire = "song";
        var c = Checks();
        c.whyNotRite = r => "Song needs Oral Tradition.";
        Assert.AreEqual("Song needs Oral Tradition.", ObservanceRules.WhyNotEstablish(fruit, plan, c, new ObservanceState(), t));
        plan.food = CultureEntityRef.Of(CultureEntityKind.Resource, "Peach Pits");
        c = Checks();
        c.whyNotFood = f => "Peach Pits is not a dish, a tea or a drink the stores keep.";
        StringAssert.Contains("not a dish", ObservanceRules.WhyNotEstablish(fruit, plan, c, new ObservanceState(), t));
        plan.food = CultureEntityRef.Of(CultureEntityKind.Resource, "Dried Auric Peaches");
        var s = new ObservanceState();
        Assert.IsNull(ObservanceRules.WhyNotEstablish(fruit, plan, Checks(), s, t));
        ObservanceRules.Establish(s, fruit, plan, null, new CultureStamp { cycle = 1, echo = 1, phase = 1, seventh = 1 });
        StringAssert.Contains("already keeps 1", ObservanceRules.WhyNotEstablish(Def("day-of-remembrance"), new ObservancePlan { recurrence = ObservanceRecurrence.EveryEcho, seventh = 1, phase = 1 }, Checks(causeKnown: true), s, t));
        ObservanceRules.LinkHolidays(s, new[] { new Holiday { name = "Joy Day", seventh = 2, phase = 1 } }, new CultureStamp());
        Assert.AreEqual(2, s.Active.Count(), "holidays are not counted against the observances' room");
        var unfounded = Checks();
        unfounded.founded = false;
        StringAssert.Contains("not been founded", ObservanceRules.WhyNotEstablish(fruit, plan, unfounded, new ObservanceState(), t));
    }

    // ===== THE TABLE =====

    [Test]
    public void ATable_IsPreparedOnlyAheadOfItsDay_OnceAndWithAChosenFood()
    {
        var t = new ObservanceTuning { prepareWindow = 7 };
        var s = new ObservanceState();
        long today = D(1, 1, 1, 1);
        var o = EveryEcho(5, 1, today);
        s.observances.Add(o);
        o.scale = ObservanceScale.Quiet;
        StringAssert.Contains("quietly", ObservanceRules.WhyNotPrepare(s, o, null, today, t, out _));
        o.scale = ObservanceScale.Full;
        StringAssert.Contains("Choose what is served", ObservanceRules.WhyNotPrepare(s, o, null, today, t, out _));
        o.food = CultureEntityRef.Of(CultureEntityKind.Resource, "Peach Soup");
        Assert.IsNull(ObservanceRules.WhyNotPrepare(s, o, null, today, t, out long keepOn));
        Assert.AreEqual(D(1, 1, 1, 5), keepOn);
        s.preparations.Add(new ObservancePreparation { observance = o.id, date = keepOn });
        StringAssert.Contains("already prepared", ObservanceRules.WhyNotPrepare(s, o, null, today, t, out _));
        s.preparations.Clear();
        var later = EveryEcho(20, 3, today);
        later.id = "obs-2";
        later.scale = ObservanceScale.Full;
        later.food = o.food;
        StringAssert.Contains("at most 7 Sevenths ahead", ObservanceRules.WhyNotPrepare(s, later, null, today, t, out _));
        var legacy = new Observance { id = "obs-3", legacy = true, recurrence = ObservanceRecurrence.EveryEcho, seventh = 2, phase = 1, lastResolved = today, scale = ObservanceScale.Full };
        StringAssert.Contains("holiday's table", ObservanceRules.WhyNotPrepare(s, legacy, null, today, t, out _));

        Assert.AreEqual(t.feastMinimum, ObservanceRules.FeastValue(t, 10, ObservanceScale.Full), 1e-4f, "at least the minimum");
        Assert.AreEqual(10f, ObservanceRules.FeastValue(t, 500, ObservanceScale.Full), 1e-4f);
        Assert.AreEqual(5f, ObservanceRules.FeastValue(t, 500, ObservanceScale.Modest), 1e-4f, "scaled down");
        Assert.AreEqual(2.5f, CultureServing.PortionsFor(5f, 2f), 1e-4f);
    }

    [Test]
    public void TheAuthoredObservances_CarryCanonAndStayQuietWhenTheyShould()
    {
        var t = new ObservanceTuning();
        Assert.IsNotNull(t.Definition(ObservanceRules.Anniversary));
        foreach (var d in t.definitions)
        {
            Assert.IsFalse(string.IsNullOrEmpty(d.canonSource), d.id);
            Assert.IsFalse(string.IsNullOrEmpty(d.canonNote), d.id);
            if (d.objective == ObservanceObjective.Remembrance || d.objective == ObservanceObjective.Gathering) Assert.IsTrue(d.Allows(ObservanceScale.Quiet), d.id + " must stay possible in hardship");
        }
        Assert.AreEqual(CanonStatus.ExplicitCanon, t.Definition("moonlit-vigil").canon);
        Assert.AreEqual(CanonStatus.ExplicitCanon, t.Definition("sky-glass-burial").canon);
        Assert.AreEqual(0f, t.Scale(ObservanceScale.Quiet).food);
    }
}
