using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Game Unit", menuName = "Game Object/Unit", order = 1)]
public class GameUnit : ScriptableObject
{
    public new string name = "";
    public string type = "", section = "", description = "";

    public Sprite icon;

}
