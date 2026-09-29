using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>What one scale of an observance asks of the stores and gives, as shares of the full table.</summary>
[Serializable]
public class ObservanceScaleSpec
{
    public ObservanceScale scale;
    [Tooltip("Share of the full table's food served (0: none).")]
    public float food;
    [Tooltip("Share of the objective's full rewards given.")]
    public float rewards = 1f;
}

/// <summary>What keeping an observance gives at full scale, by what it is for.</summary>
[Serializable]
public class ObservanceObjectiveSpec
{
    public ObservanceObjective objective;
    public float unity;
    public int morale;
    public int moraleSevenths = 2;
    [Tooltip("Joy (0-1): a remembrance gives none, grief is not joy.")]
    public float joy;
    [Tooltip("Share of a rite's weight the day leans the culture toward the observance's way of living.")]
    public float lived = 0.5f;
}

/// <summary>
/// The observances' numbers and authored observances (<see cref="ObservanceRules"/>). The vault names none of these
/// numbers: scales, rewards, windows and caps are new game rules (Canon Gaps "Observances"). Each definition carries its
/// canon label and the vault note it rests on; the Moonlit Vigil and Sky Glass Burials are the vault's own (Civic.md,
/// Ages 0-III), the First Golden Fruit follows The Inescapable Hunger, the rest are the game's.
/// </summary>
[Serializable]
public class ObservanceTuning
{
    [Tooltip("Observances besides holidays on the calendar at once (holidays keep their own cap, CultureLifeTuning.maxHolidays).")]
    public int maxObservances = 5;
    [Tooltip("Sevenths before its day a table can be prepared (the food is taken from the stores then).")]
    public int prepareWindow = 7;
    [Tooltip("Most Sevenths one occasion can be postponed (never onto the next occasion).")]
    public int postponeMax = 7;
    [Tooltip("Food value of a full table per hundred citizens, and at least this much (a holiday's own numbers).")]
    public float feastPerHundred = 2f;
    public float feastMinimum = 3f;
    [Tooltip("Occasions kept in the history (older ones stay in each observance's tallies).")]
    public int historyKept = 40;

    public List<ObservanceScaleSpec> scales = new List<ObservanceScaleSpec>
    {
        new ObservanceScaleSpec { scale = ObservanceScale.Quiet, food = 0f, rewards = 0.5f },
        new ObservanceScaleSpec { scale = ObservanceScale.Modest, food = 0.5f, rewards = 0.75f },
        new ObservanceScaleSpec { scale = ObservanceScale.Full, food = 1f, rewards = 1f },
    };

    public List<ObservanceObjectiveSpec> objectives = new List<ObservanceObjectiveSpec>
    {
        new ObservanceObjectiveSpec { objective = ObservanceObjective.Celebration, unity = 8f, morale = 4, moraleSevenths = 2, joy = 0.3f },
        new ObservanceObjectiveSpec { objective = ObservanceObjective.Remembrance, unity = 4f, morale = 0, joy = 0f },
        new ObservanceObjectiveSpec { objective = ObservanceObjective.Hospitality, unity = 6f, morale = 3, moraleSevenths = 2, joy = 0.2f },
        new ObservanceObjectiveSpec { objective = ObservanceObjective.Performance, unity = 7f, morale = 2, moraleSevenths = 2, joy = 0.2f },
        new ObservanceObjectiveSpec { objective = ObservanceObjective.Gathering, unity = 3f, morale = 2, moraleSevenths = 2, joy = 0.1f },
    };

    public List<ObservanceDefinition> definitions = new List<ObservanceDefinition>
    {
        new ObservanceDefinition
        {
            id = ObservanceRules.Anniversary, name = "Holiday", family = null, objective = ObservanceObjective.Celebration, canon = CanonStatus.NewGameRule,
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Passage of Time/Echo.md",
            canonNote = "A day set apart and kept on it every Echo (one folk year): the game's holiday (a proposal in Canon Gaps.md). Set apart with high morale and Unity; each on the calendar adds max morale.",
            description = "A day set apart to celebrate what the people remember, kept on it every Echo.",
            recurrences = { ObservanceRecurrence.EveryEcho }, food = true, repertoire = { "song", "tales" },
        },
        new ObservanceDefinition
        {
            id = "day-of-remembrance", name = "Day of Remembrance", family = "Esoteric", objective = ObservanceObjective.Remembrance, canon = CanonStatus.CanonSupported,
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md",
            canonNote = "The mourning bakeries after the Hunger keep the dead with bread and names (The Inescapable Hunger). A day kept for a remembered loss every Echo is the game's form of it; it needs no high spirits and no feast.",
            description = "Once an Echo the people stop to remember a loss: the names spoken, a candle, perhaps a bread baked for the dead. It asks for no feast and no high spirits.",
            recurrences = { ObservanceRecurrence.EveryEcho }, needsCause = true, food = true, suggestedFoods = { "ash-loaf", "Candlevein Grief Tea" },
            repertoire = { "remembrance", "tales" }, scales = { ObservanceScale.Quiet, ObservanceScale.Modest },
        },
        new ObservanceDefinition
        {
            id = "first-golden-fruit", name = "Offering of the First Golden Fruit", family = "Agromagical", objective = ObservanceObjective.Hospitality, canon = CanonStatus.CanonSupported,
            canonSource = "Worldbuilding/Rise & Fall, Crisis/Crisis/The Inescapable Hunger.md", minAge = 0, maxAge = 0,
            canonNote = "The Inescapable Hunger, Phase I (early Age 0): the first golden fruit of each year becomes a ritual offering, the focus of minor rites, named offerings and tiny festivals. A folk year is a Lunar Cycle, one Echo; keeping it only once every Real Cycle as the Cycle's great offering is the game's adaptation.",
            description = "The first golden fruit is named, offered and shared at a common table: a tiny festival of the orchards.",
            recurrences = { ObservanceRecurrence.EveryEcho, ObservanceRecurrence.OncePerCycle }, food = true,
            suggestedFoods = { "Dried Auric Peaches", "peach-tart" }, repertoire = { "song" },
            establishCost = { new ResourceAmount { resource = "Unity", amount = 10f } },
        },
        new ObservanceDefinition
        {
            id = "moonlit-vigil", name = "Moonlit Vigil", family = "Weaver", objective = ObservanceObjective.Gathering, canon = CanonStatus.ExplicitCanon,
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Civic.md", minAge = 0, maxAge = 3,
            canonNote = "Civic.md: Moonlit Vigil (Weaver, Ages 0-III): courtship in Glimmerfern groves under a full Moon, in silence, kept by peasants and refugees, the folk tradition of the broken world. The full Moon is the Ritual Seventh (Cycle.md). Its Age IV successor, the Courting Grounds, is not offered.",
            description = "In a Glimmerfern grove under the full Moon, people meet in silence, listening for a matching frequency. Nothing is spent but the night.",
            recurrences = { ObservanceRecurrence.RitualSeventh }, grounds = { "glimmerfern", "grove", "glade" }, groundText = "a settlement by Glimmerfern, a grove or a glade",
            scales = { ObservanceScale.Quiet }, unique = true,
        },
        new ObservanceDefinition
        {
            id = "sky-glass-burial", name = "Sky Glass Burial", family = "Esoteric", objective = ObservanceObjective.Remembrance, canon = CanonStatus.ExplicitCanon,
            canonSource = "Worldbuilding/Society/Societal Resources/Foundation/Civic.md", minAge = 0, maxAge = 3,
            canonNote = "Civic.md: Sky Glass Burials (Esoteric, Ages 0-III): funerary 'sky burying' at high peaks where Sky Glass forms, a reverence to the skies for the passage into the Auroral Ribbons. Keeping the rite each Echo for a remembered loss, from a settlement near the peaks, is the game's form of it.",
            description = "The dead are carried to the peaks where Sky Glass forms and given to the sky; each Echo the people climb again to remember them.",
            recurrences = { ObservanceRecurrence.EveryEcho }, grounds = { "sky glass", "skyglass", "peak", "mountain" }, groundText = "a settlement near the peaks or Sky Glass",
            needsCause = true, repertoire = { "remembrance" }, scales = { ObservanceScale.Quiet, ObservanceScale.Modest },
            keepCost = { new ResourceAmount { resource = "Faith", amount = 3f } },
        },
    };

    public ObservanceDefinition Definition(string id) =>
        string.IsNullOrEmpty(id) ? null : definitions?.FirstOrDefault(d => d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase));

    public ObservanceScaleSpec Scale(ObservanceScale scale) =>
        scales?.FirstOrDefault(s => s != null && s.scale == scale) ?? new ObservanceScaleSpec { scale = scale, food = scale == ObservanceScale.Quiet ? 0f : 1f, rewards = 1f };

    public ObservanceObjectiveSpec Objective(ObservanceObjective objective) =>
        objectives?.FirstOrDefault(o => o != null && o.objective == objective) ?? new ObservanceObjectiveSpec { objective = objective };
}
