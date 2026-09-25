using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Decision", menuName = "Decision", order = 1)]
public class Decision : ScriptableObject
{

    [SerializeField] public string _name;
    [SerializeField] string _idealism;
    [SerializeField] string _realism;
    [SerializeField] string _pragmatism;
    [SerializeField] public Requirement _requirement;
    
    
    
    
   
    



}

[System.Serializable]
public struct Requirement
{
    public string _statname;
    public int _statvalue;

   
}




