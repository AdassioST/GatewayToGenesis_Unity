using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public interface IGameUnitSlot
{
    GameUnit gameUnit { get; set; }
    float clickPower { get; set; } // Default 0 for non-resource slots
    float amount { get; set; }
    float maxAmount { get; set; }

}
