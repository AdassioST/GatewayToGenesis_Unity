using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Tooltip Data", menuName = "UI Notifications/Tooltip Data", order = 1)]
public class TooltipData : ScriptableObject
{
    public string tooltipTitle;
    public string tooltipDescription;


    //Usable for both Technology and ProductionUnits
    public string resourceRequirements; // E.g. 50: Gold, 75 Elderwood

    // Resource-specific fields
    public string productionModifiers; // E.g., "+200 Base, +50 Building Bonus"
    public string storageBreakdown; // E.g., "Max: 1000 (Building A: +500, Building B: +500)"

    // Production unit fields
    public string productionEffects; // E.g., "+5 Food per second"

    // Technology-specific fields

    public string techRequirements; // E.g., "Requires: Agriculture, Masonry"

    // Add any relevant data required for tooltips

}