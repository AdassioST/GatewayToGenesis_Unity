using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input System replacements for the legacy UnityEngine.Input polling calls.
/// Safe to call when no keyboard or mouse is connected.
/// </summary>
public static class InputUtils
{
    public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    // Normalized to [-1, 1] per notch by the Input System's default scroll delta behavior, matching Input.mouseScrollDelta
    public static Vector2 MouseScrollDelta => Mouse.current != null ? Mouse.current.scroll.ReadValue() : Vector2.zero;

    public static bool LeftClickDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    public static bool RightClickDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

    public static bool AnyKeyDown => Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;

    public static bool AnyKeyOrClickDown => AnyKeyDown || LeftClickDown || RightClickDown;

    public static bool KeyDown(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

    /// <summary>
    /// Developer shortcut: Ctrl + key, only in the editor and development builds. Keeps debug actions from
    /// colliding with gameplay hotkeys (Q/W/E/R switch tabs) and out of release builds.
    /// </summary>
    public static bool DebugKeyDown(Key key)
    {
        if (!Debug.isDebugBuild || Keyboard.current == null) return false;
        return Keyboard.current.ctrlKey.isPressed && Keyboard.current[key].wasPressedThisFrame;
    }
}
