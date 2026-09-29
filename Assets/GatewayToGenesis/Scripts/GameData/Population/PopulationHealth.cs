using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The people's health (vault: Arcanorian Ecology.md, "Creatures and the Health of People"): five pressures, each 0-1, dormant (hidden, harmless) until their
/// causes wake them, each with its own causes and its own treatments. Rules in <see cref="HealthRules"/>, every number
/// in <see cref="HealthSettings"/> (Resources/Population/Health); created by <see cref="GenesisLoop"/>, saved whole
/// (<see cref="_state"/> in GameSnapshot.Schema).
///
/// - Nutrition: stores of one kind or of peaches alone, starvation, The Inescapable Hunger's stages.
/// - Disease Burden: The Great Plague's stages, crowding, the disease vectors (0 until the ecology feeds them: E9).
/// - Sanitation: the settlements' Composure strain, ruins near them, crowding.
/// - Exposure: the Echo of Silence, harsh weather over the Capital, vagrants without a roof.
/// - Harmonic Stability: the Capital cell's Dissonance, Vibrational Fallout and Static Criticality.
/// Each pressure belongs to a size of people (<see cref="HealthRules.Emergence"/>): below its checkpoint its causes do
/// not count, so a small people only needs food. Each Seventh the levels step toward their targets. An active pressure acts only through the existing formulas: the
/// food a new citizen needs (<see cref="ThresholdFactor"/>, read by <see cref="PopGrowthLogic"/>), deaths, and morale
/// ("Health: {pressure}" sources). Stories read it through the "health" condition domain; the notices and the
/// population tooltip show a pressure only while it is active.
/// </summary>
public class PopulationHealth : SingletonBehaviour<PopulationHealth>
{
    private const LogChannel Log = LogChannel.Population;
    public const string SourcePrefix = "Health: ";
    // The Echo of Silence is the fourth Echo of the Cycle (WeatherProfileSO's EchoType order).
    private const int SilenceEcho = 4;

    public HealthSettings Settings { get; private set; }
    public HealthTuning Tuning => Settings != null && Settings.tuning != null ? Settings.tuning : _defaults;

    // Saved (GameSnapshot.Schema).
    private HealthState _state = new HealthState();

    private readonly HealthTuning _defaults = new HealthTuning();
    private readonly Dictionary<HealthPressure, int> _appliedMorale = new Dictionary<HealthPressure, int>();
    private TimeSystemLogic _time;

    /// <summary>A Seventh of health passed (levels, what is active).</summary>
    public event Action Changed;
    /// <summary>A pressure woke (true) or fell dormant (false).</summary>
    public event Action<HealthPressure, bool> PressureChanged;

    // ===== STATIC READS (safe with no health in the scene) =====

    /// <summary>The food a new citizen needs × this (1 without the system or with every pressure dormant).</summary>
    public static float ThresholdFactor => Instance != null ? HealthRules.GrowthFactor(Instance._state, Instance.Specs, Instance.Tuning) : 1f;

    // ===== QUERIES =====

    public HealthState State => _state;

    public IEnumerable<PressureSpec> Specs => Settings != null ? Settings.pressures.Where(p => p != null) : Enumerable.Empty<PressureSpec>();

    public PressureSpec Spec(HealthPressure pressure) => Settings != null ? Settings.Spec(pressure) : null;

    public string Title(HealthPressure pressure)
    {
        var spec = Spec(pressure);
        return spec != null && !string.IsNullOrEmpty(spec.title) ? spec.title : HealthRules.DefaultTitle(pressure);
    }

    public bool IsActive(HealthPressure pressure) => _state.IsActive(pressure);

    /// <summary>The level, 0-1, only while active (a dormant pressure is hidden: 0).</summary>
    public float Level(HealthPressure pressure)
    {
        var p = _state.Of(pressure);
        return p.active ? p.level : 0f;
    }

    public List<HealthPressure> Active => HealthRules.All.Where(IsActive).ToList();

    /// <summary>
    /// For the "health" condition domain: a pressure's level 0-100 while active (0 while dormant; "Nutrition", "Disease
    /// Burden", "disease_burden"...), or with no target the number of active pressures. False for an unknown pressure.
    /// </summary>
    public bool TryValue(string target, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(target)) { value = Active.Count; return true; }
        if (!HealthRules.TryParse(target, out var pressure, Specs)) return false;
        value = Mathf.Round(Level(pressure) * 100f);
        return true;
    }

    /// <summary>A pressure's own causes now, as named factors.</summary>
    public List<CrisisFactor> Causes(HealthPressure pressure) => HealthRules.Causes(pressure, GatherInputs(), Tuning);

    /// <summary>The treatments of a pressure and how much of each applies now (0-1).</summary>
    public List<(HealthTreatment treatment, float share)> Treatments(HealthPressure pressure)
    {
        var spec = Spec(pressure);
        var list = new List<(HealthTreatment, float)>();
        if (spec == null) return list;
        var inputs = GatherInputs();
        foreach (var t in spec.treatments.Where(t => t != null)) list.Add((t, HealthRules.TreatmentShare(Count(t, inputs), t.fullAt)));
        return list;
    }

    /// <summary>What a treatment is called for the player.</summary>
    public static string TreatmentName(HealthTreatment t)
    {
        switch (t.kind)
        {
            case TreatmentKind.StoreVariety: return "Varied stores";
            case TreatmentKind.FreeHousing: return "Spare homes";
            case TreatmentKind.Coherence: return "Coherence at the Capital";
            case TreatmentKind.ResonanceAnchors: return "Resonance Anchors";
            case TreatmentKind.Enclave: return HealthRules.TryEnclaveFamily(t.name, out var family) ? $"{family} Enclave Suzerainty" : t.name;
            default: return t.name;
        }
    }

    // "-30%" for what a treatment takes away, "room ×2" for how far it raises the checkpoints, or both.
    private static string TreatmentEffect(HealthTreatment t, float share)
    {
        var parts = new List<string>();
        if (t.ease > 0f) parts.Add($"-{t.ease * share:P0}");
        if (t.capacity > 0f) parts.Add($"room ×{1f + t.capacity * share:0.##}");
        return string.Join(", ", parts);
    }

    /// <summary>A pressure for a tooltip or notice: its level, what drives it and what eases it.</summary>
    public string Describe(HealthPressure pressure)
    {
        var p = _state.Of(pressure);
        var spec = Spec(pressure);
        var rows = new List<string>();
        if (spec != null && !string.IsNullOrEmpty(spec.description)) rows.Add(TooltipText.Muted(spec.description));
        string trend = p.target > p.level + 0.005f ? " (rising)" : p.target < p.level - 0.005f ? " (easing)" : string.Empty;
        rows.Add(TooltipText.Row("Level", TooltipText.Bad($"{p.level:P0}") + trend));
        var causes = Causes(pressure);
        if (causes.Count > 0) rows.Add(TooltipText.Row("Driven by", string.Join(", ", causes.OrderByDescending(c => c.value).Select(c => c.label))));
        var inputs = GatherInputs();
        int people = inputs.People;
        float capacity = Capacity(spec, inputs);
        float emergence = HealthRules.Emergence(spec, people, capacity);
        int checkpoint = HealthRules.Checkpoint(spec, capacity);
        if (spec != null && emergence < 0.995f)
            rows.Add(TooltipText.Row("The people", people < checkpoint
                ? TooltipText.Good($"too few for it ({people:N0} of {checkpoint:N0}): easing")
                : TooltipText.Warn($"{emergence:P0} of its weight at {people:N0}; full at {Mathf.CeilToInt(Mathf.Max(spec.fullAtPeople, spec.emergesAtPeople) * capacity):N0}")));
        float susceptibility = HealthRules.Susceptibility(spec, _state);
        if (susceptibility > 1.005f) rows.Add(TooltipText.Row("Weakened by", string.Join(", ", spec.cascades.Where(c => c != null && _state.IsActive(c.from)).Select(c => Title(c.from)))));
        if (p.scar > 0.005f) rows.Add(TooltipText.Row("Scar Spectra", TooltipText.Warn($"+{p.scar * Tuning.scarSensitivity:P0} sensitivity")));
        var treatments = Treatments(pressure);
        var easing = treatments.Where(t => t.share > 0f).Select(t => $"{TreatmentName(t.treatment)} {TooltipText.Good(TreatmentEffect(t.treatment, t.share))}").ToList();
        var missing = treatments.Where(t => t.share <= 0f).Select(t => TreatmentName(t.treatment)).ToList();
        if (easing.Count > 0) rows.Add(TooltipText.Row("Eased by", string.Join(", ", easing)));
        if (missing.Count > 0) rows.Add(TooltipText.Row("Could be eased by", string.Join(", ", missing)));
        if (spec != null)
        {
            var effects = new List<string>();
            if (spec.growthWeight > 0f) effects.Add($"growth needs {p.level * spec.growthWeight:P0} more Food");
            int morale = HealthRules.MoralePenalty(p, spec);
            if (morale > 0) effects.Add($"-{morale} morale");
            if (spec.deathsPerSeventh > 0f) effects.Add($"{p.level * spec.deathsPerSeventh:P1} of the people die each Seventh{(spec.vagrantsFirst ? " (vagrants first)" : string.Empty)}");
            if (effects.Count > 0) rows.Add(TooltipText.Row("Effects", string.Join("; ", effects)));
        }
        return string.Join("\n", rows);
    }

    /// <summary>Lines for the population tooltip: one per active pressure (none while all are dormant).</summary>
    public List<string> TooltipRows() => Active.Select(p => TooltipText.Row(Title(p), TooltipText.Bad($"{_state.Of(p).level:P0}"))).ToList();

    // ===== LIFETIME =====

    protected override void OnSingletonAwake()
    {
        Settings = Resources.Load<HealthSettings>("Population/Health");
        if (Settings == null) GameLog.Warning("No Resources/Population/Health: the people's health uses its default tuning and has no pressures.", Log);
    }

    protected override void OnSingletonDestroy()
    {
        if (_time != null) _time.OnSeventhChange -= OnSeventh;
    }

    private void Start()
    {
        TimeSystemLogic.WhenReady(this, time =>
        {
            _time = time;
            _time.OnSeventhChange += OnSeventh;
        });
    }

    // ===== THE SEVENTH =====

    private void OnSeventh(int seventh)
    {
        if (SaveSession.Restoring || Settings == null) return;
        Tick();
    }

    /// <summary>A Seventh of health: targets from causes, cascades, scars and treatments; the levels step; the effects follow.</summary>
    public void Tick()
    {
        var inputs = GatherInputs();
        var targets = new Dictionary<HealthPressure, float>();
        foreach (var pressure in HealthRules.All)
        {
            var spec = Spec(pressure);
            if (spec == null) { targets[pressure] = 0f; continue; }
            float causes = HealthRules.Sum(HealthRules.Causes(pressure, inputs, Tuning)) * HealthRules.Emergence(spec, inputs.People, Capacity(spec, inputs));
            float treatment = HealthRules.Treatment(spec.treatments.Where(t => t != null).Select(t => t.ease * HealthRules.TreatmentShare(Count(t, inputs), t.fullAt)), Tuning.maxTreatment);
            targets[pressure] = HealthRules.Target(causes, HealthRules.Susceptibility(spec, _state), _state.Of(pressure).scar, treatment, Tuning);
        }
        var changes = HealthRules.Step(_state, targets, Tuning);
        foreach (var change in changes)
        {
            GameLog.Event($"{Title(change.pressure)} {(change.woke ? "wakes" : "falls dormant")} ({_state.Of(change.pressure).level:P0})", Log);
            PressureChanged?.Invoke(change.pressure, change.woke);
        }
        ApplyMorale();
        ApplyDeaths();
        TellCheckpoints(GatherInputs());
        Changed?.Invoke();
    }

    // A notice the first time the people reach a pressure's checkpoint: a new kind of care their numbers now ask for.
    private void TellCheckpoints(in HealthInputs inputs)
    {
        foreach (var spec in Specs)
        {
            var p = _state.Of(spec.pressure);
            int checkpoint = HealthRules.Checkpoint(spec, Capacity(spec, inputs));
            p.emerged = HealthRules.NextEmerged(p.emerged, inputs.People, checkpoint, Tuning.checkpointResetShare, out bool reached);
            if (!reached) continue;
            GameLog.Event($"The people reach {inputs.People}: {Title(spec.pressure)} can take hold (checkpoint {checkpoint}).", Log);
            string title = string.IsNullOrEmpty(spec.emergenceTitle) ? $"{Title(spec.pressure)} can take hold" : spec.emergenceTitle;
            string body = string.IsNullOrEmpty(spec.emergenceNotice) ? $"The people number {{people}}: {Title(spec.pressure)} can wake from now on." : spec.emergenceNotice;
            NotificationFeed.Push(title, body.Replace("{people}", inputs.People.ToString("N0")), NotificationFeed.Topic.Crisis, null, "health-checkpoint:" + spec.pressure);
        }
    }

    // What the pressure's treatments' capacity multiplies its checkpoints by now.
    private static float Capacity(PressureSpec spec, in HealthInputs inputs)
    {
        if (spec?.treatments == null) return 1f;
        var list = new List<(float, float)>();
        foreach (var t in spec.treatments)
            if (t != null && t.capacity > 0f) list.Add((t.capacity, HealthRules.TreatmentShare(Count(t, inputs), t.fullAt)));
        return HealthRules.Capacity(list);
    }

    // ===== INPUTS =====

    /// <summary>What the civilization's health is made of now.</summary>
    public HealthInputs GatherInputs()
    {
        var inputs = new HealthInputs { crisisLoads = new float[HealthRules.All.Length] };
        var people = PopGrowthLogic.Instance;
        if (people != null)
        {
            inputs.population = people.population;
            inputs.vagrants = people.vagrants;
            inputs.starving = people.isFoodScarce && people.TotalPeople > 0;
        }
        var pantry = Pantry.Instance;
        if (pantry != null)
        {
            inputs.storesHeld = pantry.Value > 0f;
            inputs.storeVariety = pantry.Variety;
            inputs.peachShare = pantry.PeachShare;
        }
        var ages = AgeProgression.Instance;
        if (ages != null && ages.Current != null && ages.Current.HasCrisis)
        {
            inputs.crisis = ages.Current.crisisTitle;
            foreach (var pressure in HealthRules.All)
                inputs.crisisLoads[(int)pressure] = HealthRules.CrisisLoad(Settings != null ? Settings.crises : null, inputs.crisis, ages.StageReached, pressure);
        }
        var time = TimeSystemLogic.Instance;
        inputs.echoOfSilence = time != null && time.CurrentEcho == SilenceEcho;
        var weather = CelestialWeatherSystemLogic.Instance;
        var profile = weather != null ? weather.ActiveWeatherProfile : null;
        if (profile != null && Settings != null) inputs.weather = HealthRules.Harshness(Settings.weathers, profile.name);
        var world = WorldSystem.Instance;
        var map = world != null ? world.Map : null;
        if (map != null)
        {
            var settlements = map.Settlements;
            if (settlements.Count > 0)
            {
                inputs.settlementStrain = settlements.Average(s => Mathf.Clamp01(s.strain / 100f));
                int reach = Mathf.Max(0, Tuning.ruinReach);
                inputs.ruinsNear = map.Ruins.Count(r => r != null && settlements.Any(s => HexCoord.Distance(r.coord, s.coord) <= reach));
            }
            var capital = map.Get(map.Capital);
            if (capital != null)
            {
                inputs.dissonance = capital.dissonance;
                inputs.fallout = capital.fallout;
                inputs.criticality = capital.cascade;
            }
        }
        // The disease vectors come from the ecology (E10): creatures that carry sickness living near your settlements
        // (rats in the granaries, roaches in the middens). The Luminant Moths (E9) will join them.
        inputs.vectorPressure = map != null ? WorldEcology.VectorPressure(map, world.Settings.generation, world.AgeNumber) : 0f;
        return inputs;
    }

    // How far a treatment has come: its count toward fullAt.
    private static float Count(HealthTreatment t, in HealthInputs inputs)
    {
        switch (t.kind)
        {
            case TreatmentKind.Technology: return GameValues.Get("technology", t.name);
            case TreatmentKind.Civic: return GameValues.Get("civic", t.name);
            case TreatmentKind.Building: return GameValues.Get("building", t.name);
            case TreatmentKind.Score: return GameValues.Get("score", t.name);
            case TreatmentKind.StoreVariety: return Mathf.Max(0, inputs.storeVariety - 1);
            case TreatmentKind.FreeHousing:
                var pop = PopGrowthLogic.Instance;
                return pop != null ? 100f * Mathf.Max(0, pop.housing - pop.population) / Mathf.Max(1, inputs.People) : 0f;
            case TreatmentKind.Coherence:
                var map = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
                var capital = map != null ? map.Get(map.Capital) : null;
                return capital != null ? capital.coherence : 0f;
            case TreatmentKind.ResonanceAnchors:
                var world = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
                return world != null ? world.Settlements.Count(s => s.anchor) : 0f;
            case TreatmentKind.Enclave:
                var enclaves = WorldSystem.Instance != null ? WorldSystem.Instance.Map : null;
                if (enclaves == null || !HealthRules.TryEnclaveFamily(t.name, out var family)) return 0f;
                return enclaves.Enclaves.Count(e => e != null && e.suzerain && e.family == family);
            default: return 0f;
        }
    }

    // ===== EFFECTS =====

    // Morale: one "Health: {pressure}" source each, re-applied only when its number changes.
    private void ApplyMorale()
    {
        foreach (var spec in Specs)
        {
            int penalty = HealthRules.MoralePenalty(_state.Of(spec.pressure), spec);
            if (_appliedMorale.TryGetValue(spec.pressure, out int applied) && applied == penalty) continue;
            _appliedMorale[spec.pressure] = penalty;
            string source = SourcePrefix + Title(spec.pressure);
            if (penalty <= 0) EffectRouter.RemoveSource(source);
            else EffectRouter.ApplySet(source, new[] { new GameEffect(GameEffectType.MoraleModifier, -penalty, ModifierType.Add) });
        }
    }

    private void ApplyDeaths()
    {
        var people = PopGrowthLogic.Instance;
        if (people == null) return;
        foreach (var spec in Specs)
        {
            int deaths = HealthRules.Deaths(_state.Of(spec.pressure), spec, people.population + people.vagrants);
            var (citizens, vagrants) = HealthRules.Split(deaths, people.population, people.vagrants, spec.vagrantsFirst, Tuning.survivorFloor);
            if (vagrants > 0) people.ModifyVagrants(-vagrants);
            if (citizens > 0) people.ProcessEventDeaths(citizens, Title(spec.pressure));
            if (citizens + vagrants > 0) GameLog.Event($"{Title(spec.pressure)} took {citizens} citizen(s) and {vagrants} vagrant(s).", Log);
        }
    }
}
