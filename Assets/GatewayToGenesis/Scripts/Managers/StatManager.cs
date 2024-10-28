using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatManager : MonoBehaviour
{


    // Dictionary to hold player stats
    private Dictionary<string, int> stats = new Dictionary<string, int>();

    private void Start()
    {
        // Initialize stats
        stats["intelligence"] = 10;
        stats["strength"] = 5;
    }

    // This function checks if a stat meets a required value
    public bool CheckStat(string statName, int requiredValue)
    {
        if (stats.TryGetValue(statName.ToLower(), out int value))
        {
            return value >= requiredValue;
        }
        return false; // Stat not found
    }

    // Optionally, add a method to modify stats
    public void UpdateStat(string statName, int newValue)
    {
        if (stats.ContainsKey(statName.ToLower()))
        {
            stats[statName.ToLower()] = newValue;
        }
        else
        {
            stats[statName.ToLower()] = newValue; // Add new stat if it doesn't exist
        }
    }

}
