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
    [Tooltip("Legend classes that can hold this seat (several: a High Arbiter can be a Great Sovereign, Justiciar or Architect).")]
    public LegendClass[] allowedClasses;
    [Tooltip("Areas the seat answers for, its main charge first (ids or aliases from Resources/Council/Council Areas): stories call on a seat by area, never by title.")]
    public string[] areas;
    [Tooltip("Bonuses the seat gives as soon as a legend sits in it.")]
    public DefaultSeatBonus[] bonuses;
}
