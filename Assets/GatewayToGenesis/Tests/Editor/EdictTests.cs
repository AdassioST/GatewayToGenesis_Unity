using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The Edicts' rules (<see cref="EdictRules"/>, <see cref="EdictCatalog"/>) with no scene: establishment at three
/// council seats, edict slots, stances and their cooldowns, sealing and resting edicts, the levers other systems read,
/// the council's accord and the story values. Pure, so they also run outside Unity.
/// </summary>
public class EdictTests
{
    private static readonly EdictTuning Tuning = new EdictTuning();

    private static EdictState Established(BorderPolicy borders = BorderPolicy.Measured) =>
        new EdictState { established = true, stances = EdictRules.Founding(borders) };

    private static bool All(string technology) => true;
    private static bool Rich(string resource, float amount) => true;

    // ===== CATALOG =====

    [Test]
    public void EveryStanceHasItsDefaultAndUniqueOptions()
    {
        Assert.AreEqual(EdictCatalog.Stances.Count, EdictCatalog.Stances.Select(s => s.id).Distinct().Count());
        foreach (var stance in EdictCatalog.Stances)
        {
            Assert.IsNotNull(stance.Default, stance.id);
            Assert.AreEqual(stance.defaultOption, stance.Default.id, $"{stance.id}'s default option exists");
            Assert.AreEqual(stance.options.Count, stance.options.Select(o => o.id).Distinct().Count(), stance.id);
            Assert.IsNull(stance.Default.requiresTechnology, $"{stance.id}'s default needs nothing: it is the realm's custom");
            foreach (var option in stance.options)
                foreach (var effect in option.effects) Assert.IsTrue(effect.ValidateShape().isValid, $"{option.id}: {effect}");
        }
        Assert.AreEqual(EdictCatalog.Edicts.Count, EdictCatalog.Edicts.Select(e => e.id).Distinct().Count());
        foreach (var edict in EdictCatalog.Edicts)
        {
            Assert.Greater(edict.duration, 0, edict.id);
            Assert.IsFalse(string.IsNullOrEmpty(edict.area), $"{edict.id} names the council area that answers for it");
            foreach (var effect in edict.effects) Assert.IsTrue(effect.ValidateShape().isValid, $"{edict.id}: {effect}");
        }
    }

    [Test]
    public void DefaultStancesChangeNothingTheRealmAlreadyDid()
    {
        var levers = EdictRules.Levers(Established());
        Assert.AreEqual(1f, levers.births);
        Assert.AreEqual(1f, levers.caravans);
        Assert.AreEqual(1f, levers.arrivalRations);
        Assert.AreEqual(1f, levers.vagrantsHoused);
        Assert.IsFalse(levers.turnAwayRoofless);
        Assert.AreEqual(BorderPolicy.Measured, levers.borders);
        Assert.IsEmpty(EdictRules.StanceEffects(Established()), "the customs give no effects of their own");
    }

    [Test]
    public void EveryBorderPolicyHasItsStance()
    {
        foreach (BorderPolicy policy in Enum.GetValues(typeof(BorderPolicy)))
        {
            var option = EdictCatalog.BorderOption(policy);
            Assert.IsNotNull(option, policy.ToString());
            Assert.AreEqual(policy, EdictRules.Levers(Established(policy)).borders, "the founding keeps the realm's border policy");
        }
    }

    // ===== ESTABLISHMENT AND SLOTS =====

    [TestCase(0, false, 0)]
    [TestCase(1, false, 0)]
    [TestCase(2, true, 1)]
    [TestCase(4, true, 3)]
    [TestCase(6, true, 5)]
    public void EdictsAreEstablishedAtThreeSeats(int regularPositions, bool established, int capacity)
    {
        int seats = EdictRules.Seats(regularPositions);
        Assert.AreEqual(established, EdictRules.CanEstablish(seats, Tuning), "the Head of State counts as a seat");
        Assert.AreEqual(capacity, EdictRules.Capacity(seats, Tuning));
    }

    [Test]
    public void NothingIsDecreedBeforeTheEdictsAreEstablished()
    {
        var state = new EdictState();
        Assert.IsNotNull(EdictRules.WhyNotChange(state, "strangers", "sealed", All));
        Assert.IsNotNull(EdictRules.WhyNotSeal(state, EdictCatalog.Edicts[0], 5, "Amadea", All, Rich));
        var levers = EdictRules.Levers(state);
        Assert.IsNull(levers.borders, "the Realm panel keeps the borders until then");
        Assert.AreEqual(0f, EdictRules.Value(state, "edicts", "established", 0, 0f));
    }

    // ===== STANCES =====

    [Test]
    public void AChangedStanceStandsAPhase()
    {
        var state = Established();
        Assert.IsNull(EdictRules.WhyNotChange(state, "strangers", "sealed", All));
        state.stances["strangers"] = "sealed";
        state.stanceCooldowns["strangers"] = EdictRules.StanceCooldown(Tuning, EdictRules.Mood.Steady);
        Assert.AreEqual(21, state.stanceCooldowns["strangers"]);
        StringAssert.Contains("changed recently", EdictRules.WhyNotChange(state, "strangers", "welcome", All));
        StringAssert.Contains("already the law", EdictRules.WhyNotChange(state, "strangers", "sealed", All));
        Assert.IsNull(EdictRules.WhyNotChange(state, "cradle", "many_hearths", All), "other stances are free");
        for (int i = 0; i < 21; i++) EdictRules.Tick(state);
        Assert.IsNull(EdictRules.WhyNotChange(state, "strangers", "welcome", All));
        Assert.AreEqual(42, EdictRules.StanceCooldown(Tuning, EdictRules.Mood.Discord), "a council in discord makes laws stand longer");
    }

    [Test]
    public void SomeOptionsNeedATechnology()
    {
        var state = Established();
        StringAssert.Contains("Efficient Rations", EdictRules.WhyNotChange(state, "roofless", "almshouses", t => false));
        Assert.IsNull(EdictRules.WhyNotChange(state, "roofless", "almshouses", t => t == "Efficient Rations"));
    }

    [Test]
    public void StancesSetTheLevers()
    {
        var state = Established();
        state.stances["roofless"] = "homes_first";
        state.stances["strangers"] = "sealed";
        state.stances["cradle"] = "many_hearths";
        state.stances["borders"] = "hold";
        var levers = EdictRules.Levers(state);
        Assert.IsTrue(levers.turnAwayRoofless);
        Assert.AreEqual(0f, levers.caravans, "sealed gates draw no caravans");
        Assert.AreEqual(1.25f, levers.births, 1e-4);
        Assert.AreEqual(BorderPolicy.Hold, levers.borders);
        state.stances["strangers"] = "welcome";
        levers = EdictRules.Levers(state);
        Assert.AreEqual(1.5f, levers.caravans, 1e-4);
        Assert.AreEqual(0.75f, levers.arrivalRations, 1e-4);
    }

    [Test]
    public void LeaningStancesPullTheirPillar()
    {
        var state = Established();
        state.stances["weaving"] = "open_orchestra";
        state.stances["hearing"] = "outward";
        var pillars = EdictRules.StanceEffects(state).Where(e => e.type == GameEffectType.PillarBonus).ToList();
        Assert.IsTrue(pillars.Any(e => e.target == "waltz" && e.value == EdictCatalog.PillarLean));
        Assert.IsTrue(pillars.Any(e => e.target == "chorus" && e.value == EdictCatalog.PillarLean));
        foreach (var option in EdictCatalog.Stances.SelectMany(s => s.options).Where(o => !string.IsNullOrEmpty(o.lean)))
            Assert.IsTrue(option.effects.Any(e => e.type == GameEffectType.PillarBonus && e.target == option.lean), $"{option.id} pulls {option.lean}");
    }

    // ===== EDICTS =====

    [Test]
    public void EdictsNeedAHeadOfStateASlotAndTheirCost()
    {
        var state = Established();
        var granaries = EdictCatalog.Edict("open_granaries");
        StringAssert.Contains("Head of State", EdictRules.WhyNotSeal(state, granaries, 1, null, All, Rich));
        StringAssert.Contains("Needs 40 Food", EdictRules.WhyNotSeal(state, granaries, 1, "Amadea", All, (r, a) => false));
        Assert.IsNull(EdictRules.WhyNotSeal(state, granaries, 1, "Amadea", All, Rich));
        state.active.Add(new ActiveEdict { id = "call_to_toil", remaining = 21 });
        StringAssert.Contains("slot", EdictRules.WhyNotSeal(state, granaries, 1, "Amadea", All, Rich));
        Assert.IsNull(EdictRules.WhyNotSeal(state, granaries, 2, "Amadea", All, Rich), "a second slot takes it");
        StringAssert.Contains("already in force", EdictRules.WhyNotSeal(state, EdictCatalog.Edict("call_to_toil"), 2, "Amadea", All, Rich));
    }

    [Test]
    public void EdictsRunOutThenRest()
    {
        var state = Established();
        var granaries = EdictCatalog.Edict("open_granaries");
        state.active.Add(new ActiveEdict { id = granaries.id, remaining = granaries.duration });
        for (int i = 1; i < granaries.duration; i++) Assert.IsEmpty(EdictRules.Tick(state));
        CollectionAssert.AreEqual(new[] { granaries.id }, EdictRules.Tick(state));
        Assert.IsFalse(EdictRules.IsActive(state, granaries.id));
        Assert.AreEqual(granaries.cooldown, EdictRules.Cooldown(state, granaries.id, false));
        StringAssert.Contains("rests", EdictRules.WhyNotSeal(state, granaries, 1, "Amadea", All, Rich));
        for (int i = 0; i < granaries.cooldown; i++) EdictRules.Tick(state);
        Assert.IsNull(EdictRules.WhyNotSeal(state, granaries, 1, "Amadea", All, Rich));
    }

    [Test]
    public void TheLegendAnsweringForAnEdictStrengthensIt()
    {
        Assert.AreEqual(1f, EdictRules.Strength(false, 0, Tuning));
        Assert.AreEqual(Tuning.answeredStrength, EdictRules.Strength(true, 0, Tuning));
        Assert.AreEqual(Tuning.relatedStrength, EdictRules.Strength(true, 1, Tuning));
        var effects = EdictRules.EdictEffects(new ActiveEdict { id = "scholars_vigil", strength = 1.25f });
        Assert.AreEqual(15f * 1.25f, effects.Single().value, 1e-4);
    }

    // ===== THE COUNCIL'S ACCORD =====

    private static EdictRules.Judge Judge(string legend, params string[] areas) => new EdictRules.Judge { legend = legend, seat = "Seat", areas = areas };

    [Test]
    public void LegendsWeighTheLawsByTheirSeatsAreas()
    {
        var state = Established();
        state.stances["strangers"] = "sealed";   // favored by security/defense, opposed by diplomacy/economy
        state.stances["roofless"] = "homes_first"; // favored by security/governance, opposed by welfare
        var opinions = EdictRules.Opinions(state, new[] { Judge("Commander", "defense", "security"), Judge("Treasurer", "economy", "welfare"), Judge(null, "defense") });
        Assert.AreEqual(2, opinions.Count, "an empty seat has no opinion");
        Assert.AreEqual(2, opinions[0].approves);
        Assert.AreEqual(2, opinions[1].opposes);
        float accord = EdictRules.Accord(state, opinions, 0, 0);
        Assert.AreEqual(0f, accord, 1e-3, "one approves of both, one objects to both, the centrist government is indifferent");
        Assert.Greater(EdictRules.Accord(state, opinions.Take(1).ToList(), 0, 0), Tuning.harmonyAt);
        Assert.Less(EdictRules.Accord(state, opinions.Skip(1).ToList(), 0, 0), Tuning.discordAt);
    }

    [Test]
    public void TheCompassWeighsLeaningLaws()
    {
        var sealedGates = EdictCatalog.Strangers.Option("sealed"); // leans Regalia
        Assert.AreEqual(1, EdictRules.Resonance(sealedGates, 1, 0), "a Regalia-leaning government welcomes a Regalia law");
        Assert.AreEqual(0, EdictRules.Resonance(sealedGates, -1, 0), "a slight Waltz lean tolerates it");
        Assert.AreEqual(-1, EdictRules.Resonance(sealedGates, -2, 0), "a Waltz government resists it");
        Assert.AreEqual(1, EdictRules.Resonance(EdictCatalog.Hearing.Option("inward"), 0, -1), "Aureus is the negative Chorus-Aureus side");
        Assert.AreEqual(0, EdictRules.Resonance(EdictCatalog.Strangers.Option("measured_welcome"), 3, 3));
    }

    [Test]
    public void TheCustomsAloneLeaveTheCouncilSteady()
    {
        Assert.AreEqual(0f, EdictRules.Accord(Established(), new List<LegendOpinion>(), 3, 3));
        Assert.AreEqual(EdictRules.Mood.Steady, EdictRules.MoodOf(0f, Tuning));
        Assert.IsNotEmpty(EdictRules.MoodEffects(EdictRules.Mood.Harmony, Tuning));
        Assert.IsNotEmpty(EdictRules.MoodEffects(EdictRules.Mood.Discord, Tuning));
        Assert.IsEmpty(EdictRules.MoodEffects(EdictRules.Mood.Steady, Tuning));
    }

    // ===== STORIES =====

    [Test]
    public void StoriesReadTheEdicts()
    {
        var state = Established();
        state.stances["strangers"] = "sealed";
        state.active.Add(new ActiveEdict { id = "call_to_toil", remaining = 3 });
        state.decrees = 4;
        Assert.AreEqual(1f, EdictRules.Value(state, "edicts", "", 2, 0f));
        Assert.AreEqual(2f, EdictRules.Value(state, "edicts", "capacity", 2, 0f));
        Assert.AreEqual(1f, EdictRules.Value(state, "edicts", "active", 2, 0f));
        Assert.AreEqual(-33f, EdictRules.Value(state, "edicts", "accord", 2, -33.4f));
        Assert.AreEqual(4f, EdictRules.Value(state, "edicts", "decrees", 2, 0f));
        Assert.AreEqual(1f, EdictRules.Value(state, "stance", "strangers:sealed", 2, 0f));
        Assert.AreEqual(1f, EdictRules.Value(state, "stance", "Strangers:Sealed", 2, 0f));
        Assert.AreEqual(0f, EdictRules.Value(state, "stance", "strangers:welcome", 2, 0f));
        Assert.AreEqual(1f, EdictRules.Value(state, "stance", "cradle:natures_pace", 2, 0f), "defaults read as the law");
        Assert.AreEqual(1f, EdictRules.Value(state, "edict", "call_to_toil", 2, 0f));
        Assert.AreEqual(0f, EdictRules.Value(state, "edict", "open_granaries", 2, 0f));
    }
}
