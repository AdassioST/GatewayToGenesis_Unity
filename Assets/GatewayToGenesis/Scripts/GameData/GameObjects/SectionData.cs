using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Section Data", menuName = "UI Notifications/Section Data", order = 2)]
public class SectionData : ScriptableObject
{
    public new string name;

    public string title;
    public string description;

    // Static dictionary to store SectionData
    public static Dictionary<string, SectionData> sectionDataDictionary;

    // Static initializer to load all SectionData at startup
    public static void InitializeSectionDataDictionary()
    {
        // Use centralized validator for section loading
        sectionDataDictionary = GameAssetValidator.GetAllSections();
        
        if (GameLoggingSystem.Instance != null)
        {
            GameLoggingSystem.Instance.LogEvent(
                $"Initialized section dictionary with {sectionDataDictionary.Count} sections from centralized validator",
                "SectionData"
            );
        }
    }

    // Static helper method to get SectionData by name
    public static SectionData GetSectionData(string sectionName)
    {
        sectionDataDictionary.TryGetValue(sectionName, out var data);

        return data;
    }
}
