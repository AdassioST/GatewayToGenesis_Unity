using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Property", menuName = "Property", order = 0)]
public class Properties : ScriptableObject
{

    [SerializeField] string _name;
    [SerializeField] string _description;
    [SerializeField] int _value;


    public string Name
    {
        get { return _name; }
        set { _name = value; } // You can add validation here if needed
    }

    // Getter for Description
    public string Description
    {
        get { return _description; }
        set { _description = value; } // You can add validation here if needed
    }

    // Getter and Setter for Value
    public int Value
    {
        get { return _value; }
        set
        {
            if (value < 0)
            {
                Debug.LogWarning("Value cannot be negative. Setting to 0.");
                _value = 0;
            }
            else
            {
                _value = value;
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
