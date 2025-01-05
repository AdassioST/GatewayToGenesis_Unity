using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public UnityEvent OnClick, OnStorageTab, OnProductionTab, OnGovernmentTab, OnResearchTab;

    public UnityEvent<Vector2> OnPointerInput;

    [SerializeField] private InputActionReference storage, production, government, research, pointerPosition;

    private void Awake()
    {

    }
    private void OnEnable()
    {
        storage.action.performed += ToggleStorageTab;
        production.action.performed += ToggleProductionTab;
        government.action.performed += ToggleGovernmentTab;
        research.action.performed += ToggleResearchTab;
    }

    private void OnDisable()
    {
        storage.action.performed -= ToggleStorageTab;
        production.action.performed -= ToggleProductionTab;
        government.action.performed -= ToggleGovernmentTab;
        research.action.performed -= ToggleResearchTab;
    }

    // Add Axis Mappings Here
    void Update()
    {

    }
    private void PerformClick(InputAction.CallbackContext obj)
    {
        OnClick?.Invoke();
    }

    private void ToggleStorageTab(InputAction.CallbackContext context)
    {
        OnStorageTab?.Invoke();
    }

    private void ToggleProductionTab(InputAction.CallbackContext context)
    {
        OnProductionTab?.Invoke();
    }

    private void ToggleGovernmentTab(InputAction.CallbackContext context)
    {
        OnGovernmentTab?.Invoke();
    }

    private void ToggleResearchTab(InputAction.CallbackContext context)
    {
        OnResearchTab?.Invoke();
    }
    private Vector2 GetPointerInput()
    {
        Vector2 mousePos = pointerPosition.action.ReadValue<Vector2>();
        return Camera.main.ScreenToViewportPoint(mousePos);
    }
}
