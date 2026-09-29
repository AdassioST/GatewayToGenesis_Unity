using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// The people's health in numbers (<see cref="HealthRules"/>), with no scene: pressures dormant until their causes wake
/// them, the named causes of each, cascades (untreated Nutrition makes Disease Burden's causes weigh more), Scar
/// Spectra, treatments, and the effects through the existing formulas (food threshold factor, deaths, morale).
/// </summary>
public class HealthRulesTests
{
    private static HealthTuning Tuning() => new HealthTuning();

    private static PressureSpec Spec(HealthPressure pressure, float growth = 0.5f, float deaths = 0.02f, float morale = 20f, bool vagrantsFirst = false) =>
        new PressureSpec { pressure = pressure, title = HealthRules.DefaultTitle(pressure), growthWeight = growth, deathsPerSeventh = deaths, morale = morale, vagrantsFirst = vagrantsFirst };

    private static Dictionary<HealthPressure, float> Targets(HealthPressure pressure, float target) =>
        HealthRules.All.ToDictionary(p => p, p => p == pressure ? target : 0f);

    // Steps the state with one pressure pointed at a target until the level stops moving.
    private static void Settle(HealthState state, HealthPressure pressure, float target, HealthTuning t, int sevenths = 40)
    {
        for (int i = 0; i < sevenths; i++) HealthRules.Step(state, Targets(pressure, target), t);
    }

    [Test]
    public void DormantPressuresDoNothing()
    {
        var state = new HealthState();
        var specs = HealthRules.All.Select(p => Spec(p)).ToList();
        Assert.AreEqual(1f, HealthRules.GrowthFactor(state, specs, Tuning()), 1e-5f);
        foreach (var spec in specs)
        {
            Assert.AreEqual(0, HealthRules.MoralePenalty(state.Of(spec.pressure), spec));
            Assert.AreEqual(0, HealthRules.Deaths(state.Of(spec.pressure), spec, 100));
        }
        // A level below the waking threshold stays hidden and harmless.
        var t = Tuning();
        Settle(state, HealthPressure.Nutrition, t.activateAt - 0.02f, t);
        var nutrition = state.Of(HealthPressure.Nutrition);
        Assert.IsFalse(nutrition.active);
        Assert.Greater(nutrition.level, 0.1f, "the level is tracked while dormant");
        Assert.AreEqual(1f, HealthRules.GrowthFactor(state, specs, t), 1e-5f);
        Assert.AreEqual(0, HealthRules.MoralePenalty(nutrition, specs[0]));
    }

    [Test]
    public void MonotonyFallsWithEachKindOfStoredFoodAndCannotWakeNutritionAlone()
    {
        var t = Tuning();
        Assert.AreEqual(t.monotonyWeight, HealthRules.Monotony(true, 1, t), 1e-5f, "stores of one kind");
        Assert.AreEqual(t.monotonyWeight * 0.5f, HealthRules.Monotony(true, 2, t), 1e-5f);
        Assert.AreEqual(0f, HealthRules.Monotony(true, 3, t), 1e-5f, "three kinds end monotony");
        Assert.AreEqual(0f, HealthRules.Monotony(false, 0, t), 1e-5f, "empty stores are no monotony");
        Assert.Less(t.monotonyWeight, t.activateAt, "a peach cellar alone must not wake Nutrition at the world's start");
    }

    [Test]
    public void TheHungersStagesWakeNutrition()
    {
        var t = Tuning();
        var crises = new List<CrisisPressure> { new CrisisPressure { crisis = "The Inescapable Hunger", pressure = HealthPressure.Nutrition, stageLoads = new List<float> { 0.1f, 0.2f } } };
        Assert.AreEqual(0f, HealthRules.CrisisLoad(crises, "The Inescapable Hunger", -1, HealthPressure.Nutrition), "before the crisis begins");
        Assert.AreEqual(0.1f, HealthRules.CrisisLoad(crises, "the inescapable hunger", 0, HealthPressure.Nutrition), 1e-5f);
        Assert.AreEqual(0.2f, HealthRules.CrisisLoad(crises, "The Inescapable Hunger", 3, HealthPressure.Nutrition), 1e-5f, "later stages keep the last load");
        Assert.AreEqual(0f, HealthRules.CrisisLoad(crises, "The Great Plague", 1, HealthPressure.Nutrition));
        Assert.AreEqual(0f, HealthRules.CrisisLoad(crises, "The Inescapable Hunger", 1, HealthPressure.DiseaseBurden));

        var inputs = new HealthInputs { storesHeld = true, storeVariety = 1, crisis = "The Inescapable Hunger", crisisLoads = new float[5] };
        inputs.crisisLoads[(int)HealthPressure.Nutrition] = 0.2f;
        var causes = HealthRules.Causes(HealthPressure.Nutrition, inputs, t);
        Assert.IsTrue(causes.Any(c => c.label == "The Inescapable Hunger" && System.Math.Abs(c.value - 0.2f) < 1e-5f));
        Assert.IsTrue(causes.Any(c => c.label == "Stores of one kind"));
        var state = new HealthState();
        Settle(state, HealthPressure.Nutrition, HealthRules.Target(HealthRules.Sum(causes), 1f, 0f, 0f, t), t);
        Assert.IsTrue(state.IsActive(HealthPressure.Nutrition));
    }

    [Test]
    public void EachPressureHasItsOwnCauses()
    {
        var t = Tuning();
        var i = new HealthInputs { population = 30, vagrants = 10, starving = true, echoOfSilence = true, weather = 1f, settlementStrain = 0.5f, ruinsNear = 5,
            dissonance = 1f, fallout = 1f, criticality = 1f, vectorPressure = 1f };
        float unhoused = 10f / 40f;
        Assert.AreEqual(t.starvingWeight, HealthRules.Sum(HealthRules.Causes(HealthPressure.Nutrition, i, t)), 1e-5f);
        Assert.AreEqual(t.crowdingDisease * unhoused + t.vectorWeight, HealthRules.Sum(HealthRules.Causes(HealthPressure.DiseaseBurden, i, t)), 1e-5f);
        Assert.AreEqual(t.strainWeight * 0.5f + t.ruinCap + t.crowdingSanitation * unhoused, HealthRules.Sum(HealthRules.Causes(HealthPressure.Sanitation, i, t)), 1e-5f);
        Assert.AreEqual(t.silenceWeight + t.weatherWeight + t.unshelteredWeight * unhoused, HealthRules.Sum(HealthRules.Causes(HealthPressure.Exposure, i, t)), 1e-5f);
        Assert.AreEqual(t.dissonanceWeight + t.falloutWeight + t.criticalityWeight, HealthRules.Sum(HealthRules.Causes(HealthPressure.HarmonicStability, i, t)), 1e-5f);
        Assert.IsEmpty(HealthRules.Causes(HealthPressure.DiseaseBurden, new HealthInputs { population = 50 }, t), "a housed people with no vectors has no disease");
    }

    [Test]
    public void VectorPressureFeedsDiseaseBurden()
    {
        var t = Tuning();
        var causes = HealthRules.Causes(HealthPressure.DiseaseBurden, new HealthInputs { population = 50, vectorPressure = 0.5f }, t);
        Assert.AreEqual(1, causes.Count);
        Assert.AreEqual("Disease vectors", causes[0].label);
        Assert.AreEqual(0.5f * t.vectorWeight, causes[0].value, 1e-5f);
    }

    [Test]
    public void APressureWakesAtOneThresholdAndSleepsBelowALowerOne()
    {
        var t = Tuning();
        Assert.IsFalse(HealthRules.NextActive(false, t.activateAt - 0.01f, t));
        Assert.IsTrue(HealthRules.NextActive(false, t.activateAt, t));
        Assert.IsTrue(HealthRules.NextActive(true, (t.activateAt + t.dormantAt) / 2f, t), "an active pressure holds between the thresholds");
        Assert.IsFalse(HealthRules.NextActive(true, t.dormantAt - 0.01f, t));

        var state = new HealthState();
        var changes = new List<HealthRules.Change>();
        for (int i = 0; i < 20; i++) changes.AddRange(HealthRules.Step(state, Targets(HealthPressure.Exposure, 0.4f), t));
        Assert.AreEqual(1, changes.Count);
        Assert.IsTrue(changes[0].woke);
        for (int i = 0; i < 20; i++) changes.AddRange(HealthRules.Step(state, Targets(HealthPressure.Exposure, 0f), t));
        Assert.AreEqual(2, changes.Count);
        Assert.IsFalse(changes[1].woke);
        Assert.AreEqual(0, state.Of(HealthPressure.Exposure).activeSevenths);
    }

    [Test]
    public void TheLevelMovesAStepEachSeventh()
    {
        var t = Tuning();
        Assert.AreEqual(t.risePerSeventh, HealthRules.Approach(0f, 1f, t), 1e-5f);
        Assert.AreEqual(0.5f - t.fallPerSeventh, HealthRules.Approach(0.5f, 0f, t), 1e-5f);
        Assert.AreEqual(0.21f, HealthRules.Approach(0.2f, 0.21f, t), 1e-5f, "a small gap closes at once");
    }

    [Test]
    public void UntreatedNutritionRaisesDiseaseBurdensSusceptibility()
    {
        var t = Tuning();
        var disease = Spec(HealthPressure.DiseaseBurden);
        disease.cascades.Add(new HealthCascade { from = HealthPressure.Nutrition, weight = 0.6f });
        var state = new HealthState();
        Assert.AreEqual(1f, HealthRules.Susceptibility(disease, state), 1e-5f);
        Settle(state, HealthPressure.Nutrition, 0.5f, t);
        Assert.IsTrue(state.IsActive(HealthPressure.Nutrition));
        Assert.AreEqual(1f + 0.6f * 0.5f, HealthRules.Susceptibility(disease, state), 1e-4f);
        Assert.AreEqual(0.2f * 1.3f, HealthRules.Target(0.2f, HealthRules.Susceptibility(disease, state), 0f, 0f, t), 1e-4f);
        Assert.AreEqual(0f, HealthRules.Target(0f, HealthRules.Susceptibility(disease, state), 0f, 0f, t), "susceptibility alone makes no disease");
        // A dormant source weakens nothing.
        state.Of(HealthPressure.Nutrition).active = false;
        Assert.AreEqual(1f, HealthRules.Susceptibility(disease, state), 1e-5f);
    }

    [Test]
    public void TreatmentsCombineAndAreCapped()
    {
        Assert.AreEqual(0.5f, HealthRules.TreatmentShare(2f, 4f), 1e-5f);
        Assert.AreEqual(1f, HealthRules.TreatmentShare(9f, 4f), 1e-5f);
        Assert.AreEqual(1f, HealthRules.TreatmentShare(1f, 0f), 1e-5f, "fullAt 0: present is full");
        Assert.AreEqual(1f - 0.5f * 0.8f, HealthRules.Treatment(new[] { 0.5f, 0.2f }, 1f), 1e-5f);
        Assert.AreEqual(0.85f, HealthRules.Treatment(new[] { 0.9f, 0.9f }, 0.85f), 1e-5f);
        Assert.AreEqual(0f, HealthRules.Treatment(new float[0], 0.85f));
        var t = Tuning();
        Assert.AreEqual(0.4f * 0.7f, HealthRules.Target(0.4f, 1f, 0f, 0.3f, t), 1e-5f);
    }

    [Test]
    public void ScarSpectraGrowUnderSeverePressureAndHealSlowly()
    {
        var t = Tuning();
        Assert.AreEqual(t.scarPerSeventh, HealthRules.NextScar(0f, true, t.scarFrom, t), 1e-5f);
        Assert.AreEqual(0f, HealthRules.NextScar(0f, true, t.scarFrom - 0.01f, t), "a mild pressure leaves no scar");
        Assert.AreEqual(t.scarCap, HealthRules.NextScar(t.scarCap, true, 1f, t), 1e-5f);
        Assert.AreEqual(0.2f - t.scarHealPerSeventh, HealthRules.NextScar(0.2f, false, 0f, t), 1e-5f);
        Assert.AreEqual(0.2f * (1f + 0.3f * t.scarSensitivity), HealthRules.Target(0.2f, 1f, 0.3f, 0f, t), 1e-5f, "the scar makes the same causes weigh more");

        var state = new HealthState();
        Settle(state, HealthPressure.DiseaseBurden, 0.8f, t);
        Assert.Greater(state.Of(HealthPressure.DiseaseBurden).scar, 0f);
    }

    [Test]
    public void ActivePressuresRaiseTheFoodThresholdUpToACap()
    {
        var t = Tuning();
        var specs = new List<PressureSpec> { Spec(HealthPressure.Nutrition, growth: 0.6f), Spec(HealthPressure.Exposure, growth: 0.4f) };
        var state = new HealthState();
        state.Of(HealthPressure.Nutrition).active = true;
        state.Of(HealthPressure.Nutrition).level = 0.5f;
        state.Of(HealthPressure.Exposure).level = 0.9f;
        Assert.AreEqual(1f + 0.5f * 0.6f, HealthRules.GrowthFactor(state, specs, t), 1e-5f, "dormant Exposure adds nothing");
        state.Of(HealthPressure.Exposure).active = true;
        Assert.AreEqual(1f + 0.3f + 0.36f, HealthRules.GrowthFactor(state, specs, t), 1e-5f);
        t.maxGrowthFactor = 1.5f;
        Assert.AreEqual(1.5f, HealthRules.GrowthFactor(state, specs, t), 1e-5f);
    }

    [Test]
    public void MoraleFallsWithTheLevel()
    {
        var spec = Spec(HealthPressure.HarmonicStability, morale: 20f);
        var p = new PressureState { pressure = HealthPressure.HarmonicStability, level = 0.45f, active = true };
        Assert.AreEqual(9, HealthRules.MoralePenalty(p, spec));
        p.active = false;
        Assert.AreEqual(0, HealthRules.MoralePenalty(p, spec));
    }

    [Test]
    public void DeathsCarryTheirFractionsToTheNextSeventh()
    {
        var spec = Spec(HealthPressure.DiseaseBurden, deaths: 0.02f);
        var p = new PressureState { pressure = HealthPressure.DiseaseBurden, level = 0.5f, active = true };
        // 50 people × 0.5 × 0.02 = 0.5 a Seventh: none, then one.
        Assert.AreEqual(0, HealthRules.Deaths(p, spec, 50));
        Assert.AreEqual(0.5f, p.deathCarry, 1e-4f);
        Assert.AreEqual(1, HealthRules.Deaths(p, spec, 50));
        Assert.AreEqual(0f, p.deathCarry, 1e-4f);
        Assert.AreEqual(0, HealthRules.Deaths(p, spec, 0));
    }

    [Test]
    public void DeathsFallOnTheRooflessFirstAndSpareTheSurvivorFloor()
    {
        Assert.AreEqual((1, 3), HealthRules.Split(4, 20, 3, true, 5), "exposure takes the vagrants first");
        Assert.AreEqual((4, 0), HealthRules.Split(4, 20, 3, false, 5));
        Assert.AreEqual((2, 3), HealthRules.Split(9, 7, 3, false, 5), "citizens never below the floor");
        Assert.AreEqual((0, 0), HealthRules.Split(0, 20, 3, false, 5));
    }

    [Test]
    public void HarshWeatherIsLookedUpByName()
    {
        var weathers = new List<HarshWeather> { new HarshWeather { weather = "Boiling Rain", harshness = 0.6f } };
        Assert.AreEqual(0.6f, HealthRules.Harshness(weathers, "boiling rain"), 1e-5f);
        Assert.AreEqual(0f, HealthRules.Harshness(weathers, "Calm Winds"));
        Assert.AreEqual(0f, HealthRules.Harshness(weathers, null));
    }

    [Test]
    public void PressuresAreNamedFreely()
    {
        Assert.IsTrue(HealthRules.TryParse("Disease Burden", out var p) && p == HealthPressure.DiseaseBurden);
        Assert.IsTrue(HealthRules.TryParse("disease_burden", out p) && p == HealthPressure.DiseaseBurden);
        Assert.IsTrue(HealthRules.TryParse("HarmonicStability", out p) && p == HealthPressure.HarmonicStability);
        Assert.IsTrue(HealthRules.TryParse("nutrition", out p) && p == HealthPressure.Nutrition);
        var specs = new[] { new PressureSpec { pressure = HealthPressure.Exposure, title = "The Cold" } };
        Assert.IsTrue(HealthRules.TryParse("the cold", out p, specs) && p == HealthPressure.Exposure);
        Assert.IsFalse(HealthRules.TryParse("plague", out _));
        Assert.IsFalse(HealthRules.TryParse("", out _));
    }

    // The ladder of Health.asset: band, settled village, crowded village, town, city, great city.
    private static List<PressureSpec> Ladder()
    {
        var specs = HealthRules.All.Select(p => Spec(p)).ToList();
        int[,] people = { { 25, 150 }, { 100, 1000 }, { 300, 3000 }, { 1000, 8000 }, { 2500, 10000 } };
        for (int i = 0; i < specs.Count; i++) { specs[i].emergesAtPeople = people[i, 0]; specs[i].fullAtPeople = people[i, 1]; }
        return specs;
    }

    // Every cause at its worst, the Age Crisis included.
    private static HealthInputs Worst(int population, int vagrants)
    {
        var i = new HealthInputs { population = population, vagrants = vagrants, starving = true, storesHeld = true, storeVariety = 1, peachShare = 1f,
            crisis = "Crisis", crisisLoads = new[] { 0.4f, 0.5f, 0.3f, 0.3f, 0.3f }, echoOfSilence = true, weather = 1f, settlementStrain = 1f, ruinsNear = 5,
            dissonance = 1f, fallout = 1f, criticality = 1f, vectorPressure = 1f };
        return i;
    }

    private static List<HealthPressure> Awake(List<PressureSpec> specs, HealthInputs inputs, int sevenths = 30)
    {
        var t = Tuning();
        var state = new HealthState();
        for (int s = 0; s < sevenths; s++)
            HealthRules.Step(state, specs.ToDictionary(p => p.pressure, p =>
                HealthRules.Target(HealthRules.Sum(HealthRules.Causes(p.pressure, inputs, t)) * HealthRules.Emergence(p, inputs.People), 1f, 0f, 0f, t)), t);
        return HealthRules.All.Where(state.IsActive).ToList();
    }

    [Test]
    public void APressureGrowsByTheSameStepEachDoublingFromItsCheckpoint()
    {
        var spec = new PressureSpec { emergesAtPeople = 100, fullAtPeople = 1000 };
        Assert.AreEqual(0f, HealthRules.Emergence(spec, 99), "below the checkpoint the pressure does not exist");
        Assert.AreEqual(0f, HealthRules.Emergence(spec, 100), 1e-5f);
        Assert.AreEqual(0.5f, HealthRules.Emergence(spec, 316), 0.01f, "halfway in a logarithmic ramp is the geometric mean");
        float step1 = HealthRules.Emergence(spec, 200) - HealthRules.Emergence(spec, 100), step2 = HealthRules.Emergence(spec, 800) - HealthRules.Emergence(spec, 400);
        Assert.AreEqual(step1, step2, 1e-4f, "every doubling adds the same weight");
        Assert.AreEqual(1f, HealthRules.Emergence(spec, 1000), 1e-5f);
        Assert.AreEqual(1f, HealthRules.Emergence(spec, 20000), 1e-5f);
        Assert.AreEqual(1f, HealthRules.Emergence(new PressureSpec(), 0), 1e-5f, "no checkpoint: always present");
        Assert.AreEqual(1f, HealthRules.Emergence(new PressureSpec { emergesAtPeople = 40 }, 40), 1e-5f, "a checkpoint with no ramp is full at once");
        Assert.AreEqual(1f, HealthRules.Emergence(null, 0), 1e-5f);
        Assert.AreEqual(40, new HealthInputs { population = 30, vagrants = 10 }.People, "vagrants count among the people");
    }

    [Test]
    public void TechnologiesRaiseTheCheckpointsAsCeilings()
    {
        var spec = new PressureSpec { emergesAtPeople = 100, fullAtPeople = 1000 };
        Assert.AreEqual(2.25f, HealthRules.Capacity(new[] { (1f, 1f), (0.5f, 0.5f), (-1f, 1f) }), 1e-5f, "1 + Σ capacity × share, never below 1");
        Assert.AreEqual(1f, HealthRules.Capacity(null), 1e-5f);
        Assert.AreEqual(100, HealthRules.Checkpoint(spec));
        Assert.AreEqual(200, HealthRules.Checkpoint(spec, 2f));
        Assert.AreEqual(0f, HealthRules.Emergence(spec, 150, 2f), "a sanitation technology lets the town grow past the old checkpoint untouched");
        Assert.AreEqual(0.5f, HealthRules.Emergence(spec, 632, 2f), 0.01f, "the whole ramp moves: one doubling of room");
        Assert.AreEqual(HealthRules.Emergence(spec, 300), HealthRules.Emergence(spec, 600, 2f), 1e-4f);
    }

    [Test]
    public void ReachingACheckpointIsToldOnce()
    {
        Assert.IsFalse(HealthRules.NextEmerged(false, 99, 100, 0.8f, out bool reached) || reached);
        Assert.IsTrue(HealthRules.NextEmerged(false, 100, 100, 0.8f, out reached) && reached, "reaching it is news");
        Assert.IsTrue(HealthRules.NextEmerged(true, 5000, 100, 0.8f, out reached) && !reached, "staying above it is not");
        Assert.IsTrue(HealthRules.NextEmerged(true, 90, 100, 0.8f, out reached) && !reached, "hovering at the line keeps the flag");
        Assert.IsFalse(HealthRules.NextEmerged(true, 79, 100, 0.8f, out reached) || reached, "well below it, the next reach is news again");
        Assert.IsFalse(HealthRules.NextEmerged(false, 500, 0, 0.8f, out reached) || reached, "no checkpoint: never news");
    }

    [Test]
    public void ABandOnlyNeedsFood()
    {
        // Twenty people starving in the Echo of Silence on a fraying Loom, in the middle of an Age Crisis: no pressure.
        CollectionAssert.IsEmpty(Awake(Ladder(), Worst(20, 0)));
    }

    [Test]
    public void PressuresEmergeInOrderAsThePeopleGrow()
    {
        var specs = Ladder();
        CollectionAssert.AreEqual(new[] { HealthPressure.Nutrition }, Awake(specs, Worst(60, 0)));
        CollectionAssert.AreEqual(new[] { HealthPressure.Nutrition, HealthPressure.DiseaseBurden }, Awake(specs, Worst(250, 0)));
        CollectionAssert.AreEqual(new[] { HealthPressure.Nutrition, HealthPressure.DiseaseBurden, HealthPressure.Sanitation }, Awake(specs, Worst(1500, 0)));
        CollectionAssert.AreEqual(new[] { HealthPressure.Nutrition, HealthPressure.DiseaseBurden, HealthPressure.Sanitation, HealthPressure.Exposure }, Awake(specs, Worst(2000, 0)));
        CollectionAssert.AreEqual(HealthRules.All, Awake(specs, Worst(8000, 0)));
    }

    [Test]
    public void APressureFadesWhenThePeopleFallBelowItsCheckpoint()
    {
        var t = Tuning();
        var disease = Ladder()[(int)HealthPressure.DiseaseBurden];
        var state = new HealthState();
        float Target(int people) => HealthRules.Target(0.6f * HealthRules.Emergence(disease, people), 1f, 0f, 0f, t);
        for (int i = 0; i < 20; i++) HealthRules.Step(state, Targets(HealthPressure.DiseaseBurden, Target(1000)), t);
        Assert.IsTrue(state.IsActive(HealthPressure.DiseaseBurden));
        // The sickness took its share: sixty are left, too few to carry it.
        for (int i = 0; i < 20; i++) HealthRules.Step(state, Targets(HealthPressure.DiseaseBurden, Target(60)), t);
        Assert.IsFalse(state.IsActive(HealthPressure.DiseaseBurden));
    }

    [Test]
    public void EnclaveTreatmentsNameAFamily()
    {
        Assert.IsTrue(HealthRules.TryEnclaveFamily("Agromagical", out var f) && f == EnclaveFamily.Agromagical);
        Assert.IsTrue(HealthRules.TryEnclaveFamily("domestication enclave", out f) && f == EnclaveFamily.Domestication);
        Assert.IsFalse(HealthRules.TryEnclaveFamily("Enclave", out _));
        Assert.IsFalse(HealthRules.TryEnclaveFamily("Healers", out _));
        Assert.IsFalse(HealthRules.TryEnclaveFamily(null, out _));
    }
}
