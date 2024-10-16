using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Pillar", menuName = "Pillar", order = 0)]
public class PillarCivilization : ScriptableObject
{
    

    [SerializeField] string _name;
    [SerializeField] string _description;
    [SerializeField] int _value;

    [SerializeField] Properties property1;
    [SerializeField] Properties property2;



    

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
