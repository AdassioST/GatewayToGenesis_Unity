using UnityEngine;

[CreateAssetMenu(fileName = "New Time Unit", menuName = "Time Units/Time Unit", order = 1)]
public class TimeUnit : ScriptableObject
{
    public string unitName;
    public string description;

    public Sprite icon; // Icon for display (e.g., for the UI)
}
