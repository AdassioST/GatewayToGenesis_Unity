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
        sectionDataDictionary = new Dictionary<string, SectionData>();

        SectionData[] sectionDataArray = Resources.LoadAll<SectionData>("Sections");

        foreach (var sectionData in sectionDataArray)
        {
            if (!sectionDataDictionary.ContainsKey(sectionData.name))
            {
                sectionDataDictionary.Add(sectionData.name, sectionData);
            }
            else
            {
                Debug.LogWarning($"Duplicate SectionData found for {sectionData.name}, skipping.");
            }
        }
    }

    // Static helper method to get SectionData by name
    public static SectionData GetSectionData(string sectionName)
    {
        sectionDataDictionary.TryGetValue(sectionName, out var data);

        return data;
    }
}
