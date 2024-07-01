using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public UnityEvent OnClick;

    [SerializeField] private InputActionReference click;

    private void OnEnable()
    {
        click.action.performed += PerformClick;
    }

    private void OnDisable()
    {
        click.action.performed -= PerformClick;
    }

    private void PerformClick(InputAction.CallbackContext obj)
    {
        OnClick?.Invoke();
    }

    // Add Axis Mappings Here
    void Update()
    {
        
    }
}
