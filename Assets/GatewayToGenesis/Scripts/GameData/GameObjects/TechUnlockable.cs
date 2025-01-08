using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Tech Unlockable", menuName = "Game Object/Tech Unlockable", order = 4)]
public class TechUnlockable : ScriptableObject
{
    public GameUnit gameUnit;

    public Sprite icon;
    public TechUnlockableType unlockableType;
}
public enum TechUnlockableType
{
    Arts,
    Building,
    ClickPower,
    Modifier,
    Unit,
    Special
}