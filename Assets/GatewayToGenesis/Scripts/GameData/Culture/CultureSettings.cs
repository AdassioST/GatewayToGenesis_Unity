using UnityEngine;

/// <summary>
/// The culture's tuning (Resources/Culture/Culture): the numbers and tables of <see cref="CultureTuning"/>, the technology
/// whose research tells the founding myth, and the stories it tells. A field missing from the asset keeps the code's
/// default. Every number is a proposal.
/// </summary>
[CreateAssetMenu(fileName = "Culture", menuName = "Game Object/Culture Settings", order = 14)]
public class CultureSettings : ScriptableObject
{
    [Tooltip("Researching it lets the people count the Sevenths, and tells the founding myth.")]
    public string foundingTechnology = "Horology";
    [Tooltip("The founding myth's story (a locked knot in Resources/Events/Culture.ink).")]
    public string foundingStory = "culture_founding_myth";
    [Tooltip("The story that asks whether a food the people have grown used to is theirs.")]
    public string nationalFoodStory = "culture_national_food";
    public CultureTuning tuning = new CultureTuning();
    [Tooltip("Unity, rites, festivals, holidays, landmarks, the kitchen, luxuries and happiness.")]
    public CultureLifeTuning life = new CultureLifeTuning();
    [Tooltip("Living traditions: how practices become customs, lie dormant and revive, and the authored traditions.")]
    public TraditionTuning traditions = new TraditionTuning();
    [Tooltip("Observances: the calendar's scales, rewards, preparation and postponement windows, and the authored observances (vigils, offerings, remembrances).")]
    public ObservanceTuning observances = new ObservanceTuning();
    [Tooltip("Syncretism and ruin inheritance: the authored blends of two traditions that met, and the numbers of taking in what a fallen people left.")]
    public SyncretismTuning syncretism = new SyncretismTuning();
}
