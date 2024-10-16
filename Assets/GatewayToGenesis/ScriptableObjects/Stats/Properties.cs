using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Property", menuName = "Property", order = 0)]
public class Properties : ScriptableObject
{

    [SerializeField] string _name;
    [SerializeField] string _description;
    [SerializeField] int _value;



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
