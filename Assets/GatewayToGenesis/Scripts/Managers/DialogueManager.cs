using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Ink;

public class DialogueManager : MonoBehaviour
{
    [SerializeField] private TextAsset inkJson;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log(inkJson.text);
        }
    }
}
