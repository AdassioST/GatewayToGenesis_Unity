using UnityEngine;

/// <summary>
/// A default council seat: a position (High Arbiter, Oracle...) that legends of several classes can hold.
/// Loaded by <see cref="GameCatalog.CouncilSeats"/> from Resources/Council (see the README there); the council's
/// regular positions open with these seats in <see cref="order"/>, and active civics add seats of their own.
/// The seat's tooltip also shows the lore of the keyword named like its <see cref="title"/>.
/// </summary>
[CreateAssetMenu(fileName = "New Council Seat", menuName = "Game Object/Council Seat")]
public class CouncilSeatData : ScriptableObject
{
    [Tooltip("Seat title, unique. How the council, logs and tooltips name the seat.")]
    public string title;
    [Tooltip("Flavour line for the seat's tooltip.")]
    [TextArea(2, 4)] public string description;
    public Sprite icon;
    [Tooltip("Order in the seat pool: council positions open with the lowest first.")]
    public int order;
    [Tooltip("The Greats this seat asks for (several: a High Arbiter can be a Great Sovereign, Justiciar or Architect), its main one first: serving in it grows that Great (LegendProgress.ServeSeats).")]
    public LegendClass[] allowedClasses;
    [Tooltip("Stars a legend needs in one of those Greats to hold the seat (0: any legend, an Unattuned Legend too; 3 the most). LegendGreats.")]
    [Range(0, 3)] public int requiredStars;
    [Tooltip("Areas the seat answers for, its main charge first (ids or aliases from Resources/Council/Council Areas): stories call on a seat by area, never by title.")]
    public string[] areas;
    [Tooltip("Bonuses the seat gives as soon as a legend sits in it.")]
    public DefaultSeatBonus[] bonuses;
}
