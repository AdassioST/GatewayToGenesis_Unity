using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>One pressure's state (saved with <see cref="HealthState"/>).</summary>
[Serializable]
public class PressureState
{
    public HealthPressure pressure;
    /// <summary>0-1. Tracked while dormant too, but it does nothing until the pressure wakes.</summary>
    public float level;
    /// <summary>The level its causes and treatments pointed at last Seventh (for the tooltip's direction).</summary>
    public float target;
    public bool active;
    /// <summary>Sevenths it has been active in a row.</summary>
    public int activeSevenths;
    /// <summary>Scar Spectra, 0 to the cap: severe stress remembered, which makes the same causes weigh more.</summary>
    public float scar;
    /// <summary>Fractions of a death carried to the next Seventh.</summary>
    public float deathCarry;
    /// <summary>The people have reached its checkpoint (and were told); cleared once they fall well below it.</summary>
    [SaveOptionalField] public bool emerged;
}

/// <summary>The people's health: one state per pressure (saved whole by <see cref="PopulationHealth"/>).</summary>
[Serializable]
public class HealthState
{
    public List<PressureState> pressures = new List<PressureState>();
    public int sevenths;

    /// <summary>The pressure's state, created dormant when missing.</summary>
    public PressureState Of(HealthPressure pressure)
    {
        var state = pressures.Find(p => p != null && p.pressure == pressure);
        if (state == null) pressures.Add(state = new PressureState { pressure = pressure });
        return state;
    }

    public bool IsActive(HealthPressure pressure) => pressures.Any(p => p != null && p.pressure == pressure && p.active);
}

/// <summary>What the civilization's health is made of now. <see cref="PopulationHealth"/> gathers it from the systems.</summary>
public struct HealthInputs
{
    public int population, vagrants;
    /// <summary>Everyone the pressures can reach: citizens and vagrants (the population checkpoints count these).</summary>
    public int People => Math.Max(0, population) + Math.Max(0, vagrants);
    public bool starving;
    /// <summary>Anything in the stores at all, the kinds held in quantity, and the share that is Auric peaches.</summary>
    public bool storesHeld;
    public int storeVariety;
    public float peachShare;
    /// <summary>The Age Crisis's title and what its stage reached adds to each pressure (indexed by <see cref="HealthPressure"/>).</summary>
    public string crisis;
    public float[] crisisLoads;
    /// <summary>The settlements' mean Composure strain, 0-1, and the ruins within reach of one.</summary>
    public float settlementStrain;
    public int ruinsNear;
    public bool echoOfSilence;
    /// <summary>How harsh the Capital's weather is, 0-1.</summary>
    public float weather;
    /// <summary>The Capital's cell, each 0-1.</summary>
    public float dissonance, fallout, criticality;
    /// <summary>The disease vectors' pressure, 0-1 (the ecology's, later the Luminant Moths: E9).</summary>
    public float vectorPressure;

    public float CrisisLoad(HealthPressure pressure) => crisisLoads != null && (int)pressure < crisisLoads.Length ? Math.Max(0f, crisisLoads[(int)pressure]) : 0f;
}

/// <summary>
/// The people's health in numbers, with no scene state (tested in <c>HealthRulesTests</c>). AECOR tracked a survivor's
/// vitality with conditions sorted by cause, each with its own treatment; here the civilization carries five pressures,
/// each 0-1 and dormant (hidden, harmless) until its causes wake it:
/// - Causes: named factors (like the crisis's), so the player can read why a pressure is as high as it is.
/// - Cascades: an active pressure makes another's causes weigh more (untreated Nutrition raises Disease Burden's
///   susceptibility), as AECOR's sickness impairments did.
/// - Scar Spectra: a pressure held high scars the people, and the scar makes the same causes weigh more long after.
/// - Treatments: each eases its pressure by a share; together they combine as 1 - (1 - a)(1 - b)... up to a cap.
/// - The level moves toward its target a step each Seventh, wakes at one threshold and sleeps below a lower one.
/// - Population checkpoints: a pressure does not exist below its own number of people, and its causes grow to full
///   weight by a second number. A band needs only food; a settled village learns hunger of one crop, a crowded one
///   sickness, a town its filth, a city its roofless, a great city the strain on the Loom.
/// While active it acts only through the existing formulas: the food threshold factor, deaths and morale.
/// </summary>
public static class HealthRules
{
    public static readonly HealthPressure[] All = (HealthPressure[])Enum.GetValues(typeof(HealthPressure));

    /// <summary>The player's name of a pressure when the settings do not give one.</summary>
    public static string DefaultTitle(HealthPressure pressure) => pressure == HealthPressure.DiseaseBurden ? "Disease Burden" : pressure == HealthPressure.HarmonicStability ? "Harmonic Stability" : pressure.ToString();

    /// <summary>
    /// A pressure from a story's or designer's name for it: the enum name, its title, spaced or with underscores or
    /// hyphens ("DiseaseBurden", "Disease Burden", "disease_burden"), or a settings title.
    /// </summary>
    public static bool TryParse(string name, out HealthPressure pressure, IEnumerable<PressureSpec> specs = null)
    {
        pressure = default;
        string key = Squash(name);
        if (key.Length == 0) return false;
        foreach (var p in All)
        {
            if (Squash(p.ToString()) != key && Squash(DefaultTitle(p)) != key) continue;
            pressure = p;
            return true;
        }
        var spec = specs?.FirstOrDefault(s => s != null && Squash(s.title) == key);
        if (spec == null) return false;
        pressure = spec.pressure;
        return true;
    }

    /// <summary>An Enclave treatment's family: "Agromagical" or "Agromagical Enclave", any case.</summary>
    public static bool TryEnclaveFamily(string name, out EnclaveFamily family)
    {
        family = default;
        string key = Squash(name);
        if (key.EndsWith("enclave")) key = key.Substring(0, key.Length - "enclave".Length);
        if (key.Length == 0) return false;
        foreach (EnclaveFamily f in Enum.GetValues(typeof(EnclaveFamily)))
        {
            if (Squash(f.ToString()) != key) continue;
            family = f;
            return true;
        }
        return false;
    }

    private static string Squash(string name) => new string((name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Share of the people without a home (vagrants among citizens and vagrants), 0-1.</summary>
    public static float Unhoused(int population, int vagrants)
    {
        int people = Math.Max(0, population) + Math.Max(0, vagrants);
        return people <= 0 ? 0f : Math.Max(0, vagrants) / (float)people;
    }

    /// <summary>
    /// Stores of one kind: the full weight with a single kind held (with anything stored), less with each more kind,
    /// nothing once <see cref="HealthTuning.varietyTarget"/> kinds are held.
    /// </summary>
    public static float Monotony(bool storesHeld, int variety, HealthTuning t)
    {
        if (!storesHeld) return 0f;
        int target = Math.Max(2, t.varietyTarget);
        return t.monotonyWeight * Clamp01((target - Math.Max(0, variety)) / (float)(target - 1));
    }

    /// <summary>A pressure's own causes as named factors (none of them negative; treatments come after).</summary>
    public static List<CrisisFactor> Causes(HealthPressure pressure, in HealthInputs i, HealthTuning t)
    {
        t = t ?? new HealthTuning();
        var factors = new List<CrisisFactor>();
        float unhoused = Unhoused(i.population, i.vagrants);
        switch (pressure)
        {
            case HealthPressure.Nutrition:
                Add(factors, "Stores of one kind", Monotony(i.storesHeld, i.storeVariety, t));
                float threshold = Math.Min(0.99f, Math.Max(0f, t.peachThreshold));
                Add(factors, "Stores of peaches alone", i.storesHeld ? t.peachWeight * Clamp01((i.peachShare - threshold) / (1f - threshold)) : 0f);
                Add(factors, "Starvation", i.starving ? t.starvingWeight : 0f);
                break;
            case HealthPressure.DiseaseBurden:
                Add(factors, "Crowding", t.crowdingDisease * unhoused);
                Add(factors, "Disease vectors", t.vectorWeight * Clamp01(i.vectorPressure));
                break;
            case HealthPressure.Sanitation:
                Add(factors, "Strained settlements", t.strainWeight * Clamp01(i.settlementStrain));
                Add(factors, "Ruins nearby", Math.Min(t.ruinCap, Math.Max(0, i.ruinsNear) * t.perRuinNear));
                Add(factors, "Crowding", t.crowdingSanitation * unhoused);
                break;
            case HealthPressure.Exposure:
                Add(factors, "The Echo of Silence", i.echoOfSilence ? t.silenceWeight : 0f);
                Add(factors, "Harsh weather", t.weatherWeight * Clamp01(i.weather));
                Add(factors, "Vagrants without a roof", t.unshelteredWeight * unhoused);
                break;
            case HealthPressure.HarmonicStability:
                Add(factors, "Dissonance", t.dissonanceWeight * Clamp01(i.dissonance));
                Add(factors, "Vibrational Fallout", t.falloutWeight * Clamp01(i.fallout));
                Add(factors, "Static Criticality", t.criticalityWeight * Clamp01(i.criticality));
                break;
        }
        Add(factors, string.IsNullOrEmpty(i.crisis) ? "The Age Crisis" : i.crisis, i.CrisisLoad(pressure));
        return factors;
    }

    public static float Sum(IEnumerable<CrisisFactor> factors) => factors?.Sum(f => f.value) ?? 0f;

    /// <summary>
    /// How much of a pressure's causes count for this many people, 0-1: nothing below its checkpoint
    /// (<see cref="PressureSpec.emergesAtPeople"/> × <paramref name="capacity"/>), full from
    /// <see cref="PressureSpec.fullAtPeople"/> × capacity, and between them the same step for every doubling of the
    /// people (ln(people / checkpoint) / ln(full / checkpoint)): a village of 200 feels its crowding as much more than one
    /// of 100 as a city of 8,000 does more than one of 4,000. Every cause is weighed by it, the Age Crisis's too: a crisis
    /// still strikes a small people through its own losses, but cannot give it a pressure it is too few to have.
    /// </summary>
    public static float Emergence(PressureSpec spec, int people, float capacity = 1f)
    {
        if (spec == null || spec.emergesAtPeople <= 0) return 1f;
        float scale = Math.Max(1f, capacity);
        double from = spec.emergesAtPeople * scale, full = Math.Max(spec.fullAtPeople, spec.emergesAtPeople) * scale;
        if (people < from) return 0f;
        if (full <= from) return 1f;
        return Clamp01((float)(Math.Log(people / from) / Math.Log(full / from)));
    }

    /// <summary>The people a pressure's checkpoint stands at, raised by its treatments' capacity.</summary>
    public static int Checkpoint(PressureSpec spec, float capacity = 1f) =>
        spec == null ? 0 : (int)Math.Ceiling(Math.Max(0, spec.emergesAtPeople) * Math.Max(1f, capacity) - 1e-4f);

    /// <summary>What the treatments' capacity multiplies the checkpoints by: 1 + Σ capacity × how much of each applies.</summary>
    public static float Capacity(IEnumerable<(float capacity, float share)> treatments)
    {
        float multiplier = 1f;
        if (treatments != null) foreach (var (capacity, share) in treatments) multiplier += Math.Max(0f, capacity) * Clamp01(share);
        return multiplier;
    }

    /// <summary>
    /// Whether the people stand at a pressure's checkpoint now (true), and whether that is news: reaching it tells once;
    /// the flag clears only below <paramref name="resetShare"/> of it, so a people hovering at the line is not told again.
    /// </summary>
    public static bool NextEmerged(bool emerged, int people, int checkpoint, float resetShare, out bool reached)
    {
        reached = false;
        if (checkpoint <= 0) return emerged;
        if (!emerged && people >= checkpoint) { reached = true; return true; }
        if (emerged && people < checkpoint * Clamp01(resetShare)) return false;
        return emerged;
    }

    /// <summary>What the crisis's stage reached (0-based, -1 before it begins) adds to a pressure: stages beyond the list keep the last value.</summary>
    public static float CrisisLoad(IEnumerable<CrisisPressure> crises, string crisis, int stageReached, HealthPressure pressure)
    {
        if (crises == null || string.IsNullOrEmpty(crisis) || stageReached < 0) return 0f;
        float load = 0f;
        foreach (var c in crises)
        {
            if (c == null || c.pressure != pressure || c.stageLoads == null || c.stageLoads.Count == 0 ||
                !string.Equals(c.crisis?.Trim(), crisis.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            load += Math.Max(0f, c.stageLoads[Math.Min(stageReached, c.stageLoads.Count - 1)]);
        }
        return load;
    }

    /// <summary>How harsh a weather is (0 when it is not listed).</summary>
    public static float Harshness(IEnumerable<HarshWeather> weathers, string weather)
    {
        if (weathers == null || string.IsNullOrEmpty(weather)) return 0f;
        var match = weathers.FirstOrDefault(w => w != null && string.Equals(w.weather, weather, StringComparison.OrdinalIgnoreCase));
        return match != null ? Clamp01(match.harshness) : 0f;
    }

    /// <summary>Causes × (1 + Σ weight × level of each active source): the cascades of the other pressures.</summary>
    public static float Susceptibility(PressureSpec spec, HealthState state)
    {
        float s = 1f;
        if (spec?.cascades == null || state == null) return s;
        foreach (var c in spec.cascades)
        {
            if (c == null || c.from == spec.pressure) continue;
            var source = state.Of(c.from);
            if (source.active) s += Math.Max(0f, c.weight) * Clamp01(source.level);
        }
        return s;
    }

    /// <summary>How much of a treatment applies: its count toward <paramref name="fullAt"/>, 0-1.</summary>
    public static float TreatmentShare(float count, float fullAt) => fullAt <= 0f ? (count > 0f ? 1f : 0f) : Clamp01(count / fullAt);

    /// <summary>All treatments together: 1 - (1 - a)(1 - b)..., never above <paramref name="cap"/>.</summary>
    public static float Treatment(IEnumerable<float> eases, float cap)
    {
        float remaining = 1f;
        if (eases != null) foreach (float e in eases) remaining *= 1f - Clamp01(e);
        return Math.Min(Clamp01(cap), 1f - remaining);
    }

    /// <summary>Where the level heads: the causes, weighed by cascades and scars, less what the treatments take away.</summary>
    public static float Target(float causes, float susceptibility, float scar, float treatment, HealthTuning t)
    {
        float weighed = Math.Max(0f, causes) * Math.Max(1f, susceptibility) * (1f + Math.Max(0f, scar) * Math.Max(0f, t.scarSensitivity));
        return Clamp01(weighed) * (1f - Clamp01(treatment));
    }

    /// <summary>One Seventh's step of the level toward the target (at most rise up, fall down).</summary>
    public static float Approach(float level, float target, HealthTuning t)
    {
        float delta = target - level;
        delta = delta > 0f ? Math.Min(delta, Math.Max(0f, t.risePerSeventh)) : Math.Max(delta, -Math.Max(0f, t.fallPerSeventh));
        return Clamp01(level + delta);
    }

    /// <summary>Dormant until the level reaches <see cref="HealthTuning.activateAt"/>; active until it falls below <see cref="HealthTuning.dormantAt"/>.</summary>
    public static bool NextActive(bool active, float level, HealthTuning t) => active ? level >= Math.Min(t.dormantAt, t.activateAt) : level >= t.activateAt;

    /// <summary>The scar grows while the pressure is active and severe, and slowly heals otherwise.</summary>
    public static float NextScar(float scar, bool active, float level, HealthTuning t)
    {
        float cap = Clamp01(t.scarCap);
        if (active && level >= t.scarFrom) return Math.Min(cap, scar + Math.Max(0f, t.scarPerSeventh));
        return Math.Max(0f, Math.Min(cap, scar) - Math.Max(0f, t.scarHealPerSeventh));
    }

    /// <summary>A pressure that woke (true) or fell dormant (false) this Seventh.</summary>
    public readonly struct Change
    {
        public readonly HealthPressure pressure;
        public readonly bool woke;

        public Change(HealthPressure pressure, bool woke)
        {
            this.pressure = pressure;
            this.woke = woke;
        }
    }

    /// <summary>
    /// A Seventh of health: every pressure's level steps toward its target, wakes or sleeps, and scars. Targets must be
    /// worked out from the state before the step (cascades read last Seventh's levels). Returns what woke or slept.
    /// </summary>
    public static List<Change> Step(HealthState state, IReadOnlyDictionary<HealthPressure, float> targets, HealthTuning t)
    {
        var changes = new List<Change>();
        if (state == null) return changes;
        t = t ?? new HealthTuning();
        state.sevenths++;
        foreach (var pressure in All)
        {
            var p = state.Of(pressure);
            p.target = targets != null && targets.TryGetValue(pressure, out float target) ? Clamp01(target) : 0f;
            p.level = Approach(p.level, p.target, t);
            bool was = p.active;
            p.active = NextActive(was, p.level, t);
            p.activeSevenths = p.active ? p.activeSevenths + 1 : 0;
            p.scar = NextScar(p.scar, p.active, p.level, t);
            if (!p.active) p.deathCarry = 0f;
            if (was != p.active) changes.Add(new Change(pressure, p.active));
        }
        return changes;
    }

    /// <summary>The food a new citizen needs × this: 1 + Σ level × growth weight of every active pressure, at most the cap.</summary>
    public static float GrowthFactor(HealthState state, IEnumerable<PressureSpec> specs, HealthTuning t)
    {
        float factor = 1f;
        if (state == null || specs == null) return factor;
        foreach (var spec in specs.Where(s => s != null))
        {
            var p = state.Of(spec.pressure);
            if (p.active) factor += Clamp01(p.level) * Math.Max(0f, spec.growthWeight);
        }
        return Math.Min(Math.Max(1f, t?.maxGrowthFactor ?? 2.5f), factor);
    }

    /// <summary>Morale lost to one pressure (0 while dormant).</summary>
    public static int MoralePenalty(PressureState p, PressureSpec spec) =>
        p == null || spec == null || !p.active ? 0 : (int)Math.Round(Clamp01(p.level) * Math.Max(0f, spec.morale), MidpointRounding.AwayFromZero);

    /// <summary>
    /// People one pressure takes this Seventh: people × level × rate, whole deaths only, the fraction carried in
    /// <see cref="PressureState.deathCarry"/>. 0 while dormant.
    /// </summary>
    public static int Deaths(PressureState p, PressureSpec spec, int people)
    {
        if (p == null || spec == null || !p.active || people <= 0) return 0;
        float expected = people * Clamp01(p.level) * Math.Max(0f, spec.deathsPerSeventh) + Math.Max(0f, p.deathCarry);
        int deaths = (int)Math.Floor(expected + 1e-5f);
        p.deathCarry = Math.Max(0f, expected - deaths);
        return deaths;
    }

    /// <summary>
    /// Who dies: (citizens, vagrants). Vagrants first when the pressure falls on the roofless, citizens first otherwise;
    /// citizens never below the survivor floor.
    /// </summary>
    public static (int citizens, int vagrants) Split(int deaths, int population, int vagrants, bool vagrantsFirst, int survivorFloor)
    {
        if (deaths <= 0) return (0, 0);
        int citizensAvailable = Math.Max(0, population - Math.Max(0, survivorFloor)), vagrantsAvailable = Math.Max(0, vagrants);
        int first = Math.Min(deaths, vagrantsFirst ? vagrantsAvailable : citizensAvailable);
        int second = Math.Min(deaths - first, vagrantsFirst ? citizensAvailable : vagrantsAvailable);
        return vagrantsFirst ? (second, first) : (first, second);
    }

    private static void Add(List<CrisisFactor> factors, string label, float value)
    {
        if (value > 0.0001f) factors.Add(new CrisisFactor(label, value));
    }

    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
}
