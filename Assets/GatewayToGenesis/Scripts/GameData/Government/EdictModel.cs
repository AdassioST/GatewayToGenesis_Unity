using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One option of a stance: what the realm holds to while it is in force. Its pillar lean moves the political compass
/// (so the laws a nation keeps shape its government type), its effects apply while it is in force, and its levers are
/// read by the systems the stance governs (arrivals, births, caravans, the borders).
/// </summary>
public sealed class StanceOption
{
    public readonly string id, title, summary;
    /// <summary>Pillar this option leans toward (aureus, regalia, waltz, chorus), or null for a neutral option.</summary>
    public readonly string lean;
    public readonly IReadOnlyList<GameEffect> effects;
    /// <summary>Council areas whose seated legends approve of it, and those who oppose it.</summary>
    public readonly IReadOnlyList<string> favoredBy, opposedBy;
    /// <summary>Technology that must be researched before it can be decreed, or null.</summary>
    public string requiresTechnology;
    /// <summary>A Dissonant law (the vault's civic spectrum): it strains the council's accord a little by itself.</summary>
    public bool dissonant;
    public EdictLevers levers = EdictLevers.Neutral;

    public StanceOption(string id, string title, string summary, string lean, string[] favoredBy, string[] opposedBy, params GameEffect[] effects)
    {
        this.id = id;
        this.title = title;
        this.summary = summary;
        this.lean = lean;
        this.favoredBy = favoredBy ?? Array.Empty<string>();
        this.opposedBy = opposedBy ?? Array.Empty<string>();
        this.effects = effects ?? Array.Empty<GameEffect>();
    }
}

/// <summary>A standing law of the realm (Stellaris policies, Victoria laws): always one option in force.</summary>
public sealed class StanceDefinition
{
    public readonly string id, title, question;
    public readonly IReadOnlyList<StanceOption> options;
    /// <summary>The option in force when the edicts are established, unless the realm already lives another way.</summary>
    public readonly string defaultOption;

    public StanceDefinition(string id, string title, string question, string defaultOption, params StanceOption[] options)
    {
        this.id = id;
        this.title = title;
        this.question = question;
        this.defaultOption = defaultOption;
        this.options = options ?? Array.Empty<StanceOption>();
    }

    public StanceOption Option(string optionId) => options.FirstOrDefault(o => string.Equals(o.id, optionId, StringComparison.OrdinalIgnoreCase));
    public StanceOption Default => Option(defaultOption) ?? options.FirstOrDefault();
}

/// <summary>
/// A decree (Stellaris edicts, Civ's policy cards made temporary): sealed by the Head of State for a cost, in force for
/// <see cref="duration"/> Sevenths, then resting for <see cref="cooldown"/> before it can be sealed again. It fills
/// one of the council's edict slots while it lasts. The legend answering for its <see cref="area"/> strengthens it.
/// </summary>
public sealed class EdictDefinition
{
    public readonly string id, title, summary, area;
    public readonly IReadOnlyList<GameEffect> effects;
    public readonly string costResource;
    public readonly float cost;
    public readonly int duration, cooldown;
    public string requiresTechnology;
    public bool dissonant;

    public EdictDefinition(string id, string title, string summary, string area, string costResource, float cost, int duration, int cooldown, params GameEffect[] effects)
    {
        this.id = id;
        this.title = title;
        this.summary = summary;
        this.area = area;
        this.costResource = costResource;
        this.cost = cost;
        this.duration = duration;
        this.cooldown = cooldown;
        this.effects = effects ?? Array.Empty<GameEffect>();
    }
}

/// <summary>
/// The numbers other systems read from the laws in force (population, caravans, the borders). Neutral values change
/// nothing, so a game without edicts (or before they are established) plays exactly as before.
/// </summary>
public struct EdictLevers
{
    /// <summary>Births are multiplied by this (Hearth and Cradle).</summary>
    public float births;
    /// <summary>Caravans of survivors are drawn this much more (Strangers at the Gates).</summary>
    public float caravans;
    /// <summary>Rations issued to each survivor let in at the gates are multiplied by this.</summary>
    public float arrivalRations;
    /// <summary>Vagrants move into free homes this much faster.</summary>
    public float vagrantsHoused;
    /// <summary>True: only those with a home are let in, whatever the technology allows (Homes First).</summary>
    public bool turnAwayRoofless;
    /// <summary>The border policy this law sets, or null when it leaves the borders alone.</summary>
    public BorderPolicy? borders;

    public static EdictLevers Neutral => new EdictLevers { births = 1f, caravans = 1f, arrivalRations = 1f, vagrantsHoused = 1f };

    /// <summary>Two sets of levers in force together: multipliers multiply, a closed gate stays closed, the later border policy wins.</summary>
    public EdictLevers Combine(EdictLevers other) => new EdictLevers
    {
        births = births * other.births,
        caravans = caravans * other.caravans,
        arrivalRations = arrivalRations * other.arrivalRations,
        vagrantsHoused = vagrantsHoused * other.vagrantsHoused,
        turnAwayRoofless = turnAwayRoofless || other.turnAwayRoofless,
        borders = other.borders ?? borders,
    };
}

/// <summary>An edict in force: which, since when and until when (saved).</summary>
[Serializable]
public class ActiveEdict
{
    public string id;
    public int remaining;
    /// <summary>Strength it was sealed with (1, more when the legend answering for its area is on the council).</summary>
    public float strength = 1f;
    public string sealedBy;
    public int sealedSeventh;
}

/// <summary>Something the government decreed, for the Edicts section's record.</summary>
[Serializable]
public class DecreeMoment
{
    public string title;
    public string text;
    public int seventh;
    public string ageId;
}

/// <summary>Everything the edicts remember (saved whole in GameSnapshot.Schema).</summary>
[Serializable]
public class EdictState
{
    public bool established;
    /// <summary>Sevenths since the edicts were established.</summary>
    public int sevenths;
    /// <summary>Stance id → option in force.</summary>
    [UnityEngine.SerializeField] public Dictionary<string, string> stances = new Dictionary<string, string>();
    /// <summary>Stance id → Sevenths before it can change again.</summary>
    [UnityEngine.SerializeField] public Dictionary<string, int> stanceCooldowns = new Dictionary<string, int>();
    public List<ActiveEdict> active = new List<ActiveEdict>();
    /// <summary>Edict id → Sevenths before it can be sealed again.</summary>
    [UnityEngine.SerializeField] public Dictionary<string, int> edictCooldowns = new Dictionary<string, int>();
    public List<DecreeMoment> record = new List<DecreeMoment>();
    public int decrees;
}

/// <summary>What one seated legend thinks of the laws in force (the council's accord, faction approval in other 4X).</summary>
public struct LegendOpinion
{
    public string legend, seat;
    public int approves, opposes;
    public int Net => approves - opposes;
}

/// <summary>The edicts' numbers. Every one is a proposal.</summary>
[Serializable]
public class EdictTuning
{
    [UnityEngine.Tooltip("Council seats, counting the Head of State, before the government can issue edicts (the third opens with Call and Response).")]
    public int minCouncilSeats = 3;
    [UnityEngine.Tooltip("Edict slots: council seats (counting the Head of State) minus this. Three seats hold one edict, a full council of seven holds five.")]
    public int seatsBeforeFirstSlot = 2;
    [UnityEngine.Tooltip("Sevenths a stance must stand before it can change again (21 = one Phase).")]
    public int stanceCooldownSevenths = 21;
    [UnityEngine.Tooltip("Stance cooldowns are multiplied by this while the council is in discord.")]
    public float discordCooldownMultiplier = 2f;
    [UnityEngine.Tooltip("Accord (-100..100) at or above which the council is in harmony.")]
    public float harmonyAt = 25f;
    [UnityEngine.Tooltip("Accord at or below which the council is in discord.")]
    public float discordAt = -25f;
    [UnityEngine.Tooltip("Strength of an edict sealed while a legend answers for its area from a seat that covers it.")]
    public float answeredStrength = 1.25f;
    [UnityEngine.Tooltip("Strength when only a seat of a related area answers.")]
    public float relatedStrength = 1.1f;
    [UnityEngine.Tooltip("Legend Effectiveness while the council is in harmony.")]
    public float harmonyLegendEffectiveness = 5f;
    [UnityEngine.Tooltip("Morale while the council is in discord.")]
    public float discordMorale = -5f;
}
