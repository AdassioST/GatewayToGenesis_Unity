using UnityEngine;

/// <summary>
/// A named group of game units ("Vital Resource", "Hearthlands"...). Sections are loaded by
/// <see cref="GameCatalog.Sections"/> from Resources/Sections, keyed by <see cref="name"/>, and can be
/// targeted by effects with Section scope.
/// </summary>
[CreateAssetMenu(fileName = "New Section Data", menuName = "UI Notifications/Section Data", order = 2)]
public class SectionData : ScriptableObject
{
    public new string name;

    public string title;
    public string description;

    public static SectionData GetSectionData(string sectionName) => GameCatalog.Sections.TryGet(sectionName, out var data) ? data : null;
}
