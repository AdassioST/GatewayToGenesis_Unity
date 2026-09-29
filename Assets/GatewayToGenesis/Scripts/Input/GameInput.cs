using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The game's one input path. Every hotkey is an action in <c>PlayerControls.inputactions</c> (Q/W/E/R tabs,
/// L Library, M world map, Escape cancel), read through the generated <see cref="PlayerControls"/> wrapper; add or rebind keys
/// there, never by reading the keyboard. Scene objects react through the UnityEvents below (wired to
/// <see cref="TabHotkeys"/> in the scene); code subscribes to the static C# events, which outlive the scene object (the Library window).
///
/// Hotkeys are ignored while a text field has focus and while Ctrl or Alt is held (Ctrl + key is reserved for
/// developer shortcuts, <see cref="InputUtils.DebugKeyDown"/>). Cancel always fires: to the menu while it holds the screen,
/// else to the open windows, and to the quick menu when no window was open (<see cref="CancelUnclaimed"/>).
/// </summary>
[DefaultExecutionOrder(-500)]
public class GameInput : SingletonBehaviour<GameInput>
{
    public UnityEvent OnStorageTab, OnProductionTab, OnGovernmentTab, OnResearchTab;

    /// <summary>L: open or close the White-Haven Library.</summary>
    public static event Action LibraryPressed;
    /// <summary>M: open or close the world map.</summary>
    public static event Action MapPressed;
    /// <summary>Escape: close whatever is open.</summary>
    public static event Action CancelPressed;
    /// <summary>Escape while the menu holds the screen: the menu backs out, and nothing behind it closes.</summary>
    public static event Action MenuCancelPressed;
    /// <summary>Escape when no window was open to take it (<see cref="OpenWindows"/>): the quick menu opens.</summary>
    public static event Action CancelUnclaimed;

    private PlayerControls _controls;

    protected override void OnSingletonAwake()
    {
        _controls = new PlayerControls();
        // The player's own keys (Options, Controls) over the defaults.
        KeyBindings.Register(_controls.asset);
        var map = _controls.DefaultControls;
        map.Storage.performed += _ => Hotkey(() => OnStorageTab?.Invoke());
        map.Production.performed += _ => Hotkey(() => OnProductionTab?.Invoke());
        map.Government.performed += _ => Hotkey(() => OnGovernmentTab?.Invoke());
        map.Research.performed += _ => Hotkey(() => OnResearchTab?.Invoke());
        map.Library.performed += _ => Hotkey(() => LibraryPressed?.Invoke());
        map.Map.performed += _ => Hotkey(() => MapPressed?.Invoke());
        map.Cancel.performed += _ => Cancel();
    }

    private static void Cancel()
    {
        // The Escape that ends listening for a new key does nothing else.
        if (KeyBindings.Busy) return;
        if (SaveMenu.BlocksGameplay) { MenuCancelPressed?.Invoke(); return; }
        // Asked before the windows close themselves.
        bool claimed = OpenWindows.Any;
        CancelPressed?.Invoke();
        if (!claimed) CancelUnclaimed?.Invoke();
    }

    private void OnEnable() => _controls?.Enable();

    private void OnDisable() => _controls?.Disable();

    protected override void OnSingletonDestroy()
    {
        _controls?.Disable();
        _controls?.Dispose();
        _controls = null;
    }

    private static void Hotkey(Action action)
    {
        if (!SaveMenu.BlocksGameplay && !IsTyping && !ModifierHeld) action();
    }

    /// <summary>True while a text field (Library search...) has keyboard focus, so letters are typed, not hotkeys.</summary>
    public static bool IsTyping
    {
        get
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var field = selected != null ? selected.GetComponent<TMP_InputField>() : null;
            return field != null && field.isFocused;
        }
    }

    private static bool ModifierHeld
    {
        get
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed);
        }
    }
}
