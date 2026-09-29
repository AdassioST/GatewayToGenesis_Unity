using UnityEngine;

/// <summary>
/// The edicts' tuning (optional: Resources/Government/Edicts; without it the code's defaults apply). The stances and
/// edicts themselves are <see cref="EdictCatalog"/>. Every number is a proposal.
/// </summary>
[CreateAssetMenu(fileName = "Edicts", menuName = "Game Object/Edict Settings", order = 16)]
public class EdictSettings : ScriptableObject
{
    public EdictTuning tuning = new EdictTuning();
}
