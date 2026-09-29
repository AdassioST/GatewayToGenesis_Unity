using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The five pressures on the people's health (AECOR's conditions sorted by cause, at the scale of a civilization). Values
/// are serialized by index: append only.
/// </summary>
public enum HealthPressure { Nutrition, DiseaseBurden, Sanitation, Exposure, HarmonicStability }

/// <summary>What eases a pressure. Each kind is counted from the game and reaches its full ease at <see cref="HealthTreatment.fullAt"/>.</summary>
public enum TreatmentKind
{
    /// <summary>A technology researched (1 or 0).</summary>
    Technology,
    /// <summary>A civic active (1 or 0).</summary>
    Civic,
    /// <summary>Production units of this name built.</summary>
    Building,
    /// <summary>An event score's points (Ink scores such as plague_preparation).</summary>
    Score,
    /// <summary>Kinds of stored food held in quantity beyond the first (the famine's lesson: the land is a chord, not a note).</summary>
    StoreVariety,
    /// <summary>Free homes, as a percentage of the people (5 = one spare home for every 20 people), so it means the same in a village and a city.</summary>
    FreeHousing,
    /// <summary>Coherence of the Capital's cell, 0-1.</summary>
    Coherence,
    /// <summary>Settlements bound as Resonance Anchors.</summary>
    ResonanceAnchors,
    /// <summary>Enclaves of this family (Agromagical, Domestication...) under your Suzerainty (Enclave.md: Agromagical Enclaves keep Health and Sanitation).</summary>
    Enclave,
}

/// <summary>One treatment of a pressure: what it is, how much it eases the pressure at full strength, and when it is full.</summary>
[Serializable]
public class HealthTreatment
{
    public TreatmentKind kind;
    [Tooltip("The technology, civic, production unit, event score or Enclave family (empty for the kinds counted from the world).")]
    public string name;
    [Tooltip("Share of the pressure it takes away at full strength (0.3 = 30%). Treatments combine as 1 - (1 - a)(1 - b)...")]
    [Range(0f, 1f)] public float ease = 0.2f;
    [Tooltip("Raises the pressure's population checkpoints: both × (1 + Σ capacity). 1 = the people can double before it bites " +
             "the same; the natural ceiling technologies raise (the ramp is logarithmic, so each +1 is one more doubling's room).")]
    public float capacity;
    [Tooltip("Count at which it reaches its full ease, proportionally below (1 for a technology or civic).")]
    public float fullAt = 1f;
}

/// <summary>A cascade (AECOR's sickness impairments): while <see cref="from"/> is active, this pressure's own causes weigh more.</summary>
[Serializable]
public class HealthCascade
{
    public HealthPressure from;
    [Tooltip("Causes × (1 + weight × the source's level): 0.6 means a pressure at 0.5 makes this one's causes weigh 30% more.")]
    public float weight = 0.5f;
}

/// <summary>One pressure: its name and meaning, what it does while active, what eases it, and what makes it worse.</summary>
[Serializable]
public class PressureSpec
{
    public HealthPressure pressure;
    public string title;
    [TextArea(2, 5)] public string description;
    [Header("Population checkpoint")]
    [Tooltip("People (citizens and vagrants) below which this pressure does not exist: none of its causes count. A small band only needs food. " +
             "Raised by the capacity of its treatments.")]
    public int emergesAtPeople;
    [Tooltip("People at which its causes count in full; between the checkpoint and this they grow by the same step each doubling (logarithmic).")]
    public int fullAtPeople;
    [Tooltip("The notice when the people first reach the checkpoint ({people} is replaced by their number).")]
    public string emergenceTitle;
    [TextArea(2, 4)] public string emergenceNotice;
    [Header("Effects")]
    [Tooltip("While active, the Food a new citizen needs × (1 + level × this).")]
    public float growthWeight = 0.4f;
    [Tooltip("While active, the share of the population lost each Seventh at level 1 (proportionally below).")]
    public float deathsPerSeventh = 0.01f / GrowthRules.SeventhsPerYear;
    [Tooltip("While active, morale at level 1 falls by this (proportionally below).")]
    public float morale = 10f;
    [Tooltip("Its deaths fall on the vagrants first (the ones without a roof).")]
    public bool vagrantsFirst;
    public List<HealthTreatment> treatments = new List<HealthTreatment>();
    public List<HealthCascade> cascades = new List<HealthCascade>();
}

/// <summary>What an Age Crisis's stages add to one pressure's causes (stage 1 first).</summary>
[Serializable]
public class CrisisPressure
{
    [Tooltip("The crisis title as written in the Age asset (The Inescapable Hunger, The Great Plague).")]
    public string crisis;
    public HealthPressure pressure;
    [Tooltip("Added to the pressure's causes at each stage reached; stages beyond the list keep the last value.")]
    public List<float> stageLoads = new List<float>();
}

/// <summary>A weather that exposes the people when it lies over the Capital.</summary>
[Serializable]
public class HarshWeather
{
    [Tooltip("The weather profile's asset name (Resources/WeatherProfiles).")]
    public string weather;
    [Range(0f, 1f)] public float harshness = 0.5f;
}

/// <summary>How each cause weighs and how fast the pressures move. Every value is a proposal (Canon Gaps).</summary>
[Serializable]
public class HealthTuning
{
    [Header("Activation")]
    [Tooltip("A dormant pressure wakes once its level reaches this: hidden and harmless below it.")]
    [Range(0f, 1f)] public float activateAt = 0.15f;
    [Tooltip("An active pressure sleeps again once its level falls below this.")]
    [Range(0f, 1f)] public float dormantAt = 0.08f;
    [Tooltip("Most a pressure's level rises and falls each Seventh toward what its causes and treatments make it.")]
    public float risePerSeventh = 0.08f;
    public float fallPerSeventh = 0.05f;
    [Tooltip("Most all treatments together take away from one pressure (0.85 = 85%).")]
    [Range(0f, 1f)] public float maxTreatment = 0.85f;

    [Header("Scar Spectra (Pure Light.md: severe stress leaves durable marks that increase sensitivity)")]
    [Tooltip("A pressure active at or above this level scars the people.")]
    [Range(0f, 1f)] public float scarFrom = 0.45f;
    public float scarPerSeventh = 0.02f;
    public float scarHealPerSeventh = 0.005f;
    [Range(0f, 1f)] public float scarCap = 0.5f;
    [Tooltip("Causes × (1 + scar × this).")]
    public float scarSensitivity = 1f;

    [Header("Effects")]
    [Tooltip("The food threshold factor from every active pressure together never goes above this.")]
    public float maxGrowthFactor = 2.5f;
    [Tooltip("Citizens the pressures never take (like the crisis's survivor floor).")]
    public int survivorFloor = 5;
    [Tooltip("A checkpoint's notice comes again only after the people fell below this share of it (so a people hovering at the line is told once).")]
    [Range(0f, 1f)] public float checkpointResetShare = 0.8f;

    [Header("Nutrition")]
    [Tooltip("Stores of a single kind (with anything stored): this weight; a second kind halves it, a third ends it.")]
    public float monotonyWeight = 0.12f;
    [Tooltip("Kinds of stored food that end monotony.")]
    public int varietyTarget = 3;
    [Tooltip("Stores that are mostly Auric peaches: this weight when all of them are, proportionally above the threshold share.")]
    public float peachWeight = 0.1f;
    [Range(0f, 1f)] public float peachThreshold = 0.6f;
    public float starvingWeight = 0.35f;

    [Header("Disease Burden")]
    [Tooltip("The share of the people without a home, times this.")]
    public float crowdingDisease = 0.3f;
    [Tooltip("The vectors' pressure (0-1: the ecology's Luminant Moths, E9), times this.")]
    public float vectorWeight = 0.6f;

    [Header("Sanitation")]
    [Tooltip("The settlements' mean Composure strain (0-1), times this.")]
    public float strainWeight = 0.3f;
    [Tooltip("Per ruin within the reach of a settlement, up to the cap.")]
    public float perRuinNear = 0.05f;
    public float ruinCap = 0.15f;
    public int ruinReach = 2;
    public float crowdingSanitation = 0.25f;

    [Header("Exposure")]
    [Tooltip("The Echo of Silence (the winter of the Cycle).")]
    public float silenceWeight = 0.12f;
    [Tooltip("Harsh weather over the Capital (its harshness 0-1), times this.")]
    public float weatherWeight = 0.25f;
    [Tooltip("The share of the people without a home, times this.")]
    public float unshelteredWeight = 0.35f;

    [Header("Harmonic Stability")]
    [Tooltip("Dissonance of the Capital's cell (0-1), times this.")]
    public float dissonanceWeight = 0.35f;
    [Tooltip("Vibrational Fallout of the Capital's cell (0-1), times this.")]
    public float falloutWeight = 0.5f;
    [Tooltip("Static Criticality: the Chaotic Resonant Cascade along the fallout's edge at the Capital (0-1), times this.")]
    public float criticalityWeight = 0.6f;
}

/// <summary>
/// The people's health (Resources/Population/Health), read by <see cref="PopulationHealth"/>: the five pressures, what
/// each does while active, its treatments and cascades, which crisis stages load which pressure, the harsh weathers and
/// the tuning. Every number and name is a proposal (Canon Gaps.md).
/// </summary>
[CreateAssetMenu(fileName = "Health", menuName = "Game Object/Health Settings", order = 14)]
public class HealthSettings : ScriptableObject
{
    public HealthTuning tuning = new HealthTuning();
    public List<PressureSpec> pressures = new List<PressureSpec>();
    public List<CrisisPressure> crises = new List<CrisisPressure>();
    public List<HarshWeather> weathers = new List<HarshWeather>();

    public PressureSpec Spec(HealthPressure pressure) => pressures.Find(p => p != null && p.pressure == pressure);
}
