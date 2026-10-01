using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The keys the player can rebind (Options, Controls): the hotkeys of <c>PlayerControls.inputactions</c> (tabs, Library,
/// world map), read through <see cref="GameInput"/>, plus keys kept in a small action map here
/// (the world map's next idle party, Space to pause time). Escape stays the key that backs out and opens the menu. Overrides are saved in PlayerPrefs
/// and laid over every new <see cref="PlayerControls"/> (one is made with each scene). Rebinding a key already in use
/// swaps the two.
/// </summary>
public static class KeyBindings
{
    /// <summary>One line of the Controls page.</summary>
    public sealed class Entry
    {
        public string id, name, group;
        /// <summary>Shown but not rebindable (its keys are given in <see cref="fixedKeys"/>, or read from its action).</summary>
        public bool locked;
        public string fixedKeys;
    }

    private const string AssetKey = "g2g.bindings", ExtraKey = "g2g.bindings.world";

    public static readonly List<Entry> Entries = new List<Entry>
    {
        new Entry { id = "storage", name = "Storage", group = "Capital" },
        new Entry { id = "production", name = "Production", group = "Capital" },
        new Entry { id = "government", name = "Government", group = "Capital" },
        new Entry { id = "research", name = "Research", group = "Capital" },
        new Entry { id = "library", name = "White-Haven Library", group = "Capital" },
        new Entry { id = "map", name = "World map", group = "World" },
        new Entry { id = "next-idle", name = "Next idle party", group = "World" },
        new Entry { id = "pause", name = "Pause / resume time", group = "Always" },
        new Entry { id = "cancel", name = "Back / menu", group = "Always", locked = true },
        new Entry { id = "gather", name = "Gather, select", group = "Mouse", locked = true, fixedKeys = "Left click" },
        new Entry { id = "send", name = "Send the selected party", group = "Mouse", locked = true, fixedKeys = "Right click" },
        new Entry { id = "survey", name = "Survey a cell", group = "Mouse", locked = true, fixedKeys = "Shift + right click" },
        new Entry { id = "pan", name = "Look around the map", group = "Mouse", locked = true, fixedKeys = "Drag" },
        new Entry { id = "zoom", name = "Zoom (out of the capital: the world)", group = "Mouse", locked = true, fixedKeys = "Wheel" },
        new Entry { id = "plan", name = "Add a technology to the plan", group = "Mouse", locked = true, fixedKeys = "Shift + click" },
    };

    private static InputActionAsset _asset;
    private static InputActionMap _world;
    private static InputActionRebindingExtensions.RebindingOperation _operation;
    private static int _endedFrame = -10;

    /// <summary>A key is being listened for.</summary>
    public static bool Rebinding => _operation != null;

    /// <summary>A rebinding is under way or ended this frame or the last (its Escape must not also back out of a menu).</summary>
    public static bool Busy => Rebinding || Time.frameCount - _endedFrame <= 1;

    /// <summary>GameInput hands over its fresh controls: the saved keys are laid over them.</summary>
    public static void Register(InputActionAsset asset)
    {
        _asset = asset;
        if (_asset == null) return;
        try
        {
            string json = PlayerPrefs.GetString(AssetKey, string.Empty);
            if (!string.IsNullOrEmpty(json)) _asset.LoadBindingOverridesFromJson(json);
        }
        catch (Exception e) { GameLog.Warning("Saved key bindings could not be read: " + e.Message, LogChannel.Units); }
    }

    private static InputActionMap World
    {
        get
        {
            if (_world != null) return _world;
            _world = new InputActionMap("World");
            _world.AddAction("NextIdleParty", InputActionType.Button, "<Keyboard>/period");
            _world.AddAction("Pause", InputActionType.Button, "<Keyboard>/space");
            try
            {
                string json = PlayerPrefs.GetString(ExtraKey, string.Empty);
                if (!string.IsNullOrEmpty(json)) _world.LoadBindingOverridesFromJson(json);
            }
            catch (Exception) { }
            _world.Enable();
            return _world;
        }
    }

    /// <summary>The action behind an entry (null for mouse lines, or before the controls exist).</summary>
    public static InputAction Action(string id)
    {
        switch (id)
        {
            case "storage": return _asset?.FindAction("DefaultControls/Storage");
            case "production": return _asset?.FindAction("DefaultControls/Production");
            case "government": return _asset?.FindAction("DefaultControls/Government");
            case "research": return _asset?.FindAction("DefaultControls/Research");
            case "library": return _asset?.FindAction("DefaultControls/Library");
            case "map": return _asset?.FindAction("DefaultControls/Map");
            case "cancel": return _asset?.FindAction("DefaultControls/Cancel");
            case "next-idle": return World.FindAction("NextIdleParty");
            case "pause": return World.FindAction("Pause");
            default: return null;
        }
    }

    /// <summary>The key(s) of an entry, as the player reads them.</summary>
    public static string Keys(string id)
    {
        var entry = Entries.FirstOrDefault(e => e.id == id);
        if (entry != null && !string.IsNullOrEmpty(entry.fixedKeys)) return entry.fixedKeys;
        var action = Action(id);
        if (action == null) return id == "cancel" ? "Esc" : "-";
        string keys = action.GetBindingDisplayString(0);
        if (string.IsNullOrEmpty(keys)) return "unbound";
        // A lone mark (the period) is easy to miss: name it too.
        if (keys.Length == 1 && !char.IsLetterOrDigit(keys[0]))
        {
            string path = action.bindings[0].effectivePath ?? "";
            string name = path.Substring(path.LastIndexOf('/') + 1);
            if (name.Length > 1) return char.ToUpperInvariant(name[0]) + name.Substring(1) + " (" + keys + ")";
        }
        return keys;
    }

    /// <summary>The next-idle-party key went down this frame (not while a menu or a text field has the keyboard).</summary>
    public static bool NextIdlePartyPressed => !SaveMenu.BlocksGameplay && !Busy && !GameInput.IsTyping && World.FindAction("NextIdleParty").WasPressedThisFrame();

    /// <summary>The pause key went down this frame (not while a menu or a text field has the keyboard).</summary>
    public static bool PausePressed => !SaveMenu.BlocksGameplay && !Busy && !GameInput.IsTyping && World.FindAction("Pause").WasPressedThisFrame();

    /// <summary>
    /// Listen for the next key for <paramref name="id"/> (Escape cancels). <paramref name="done"/> gets a line to show:
    /// null when nothing changed, or what was swapped.
    /// </summary>
    public static bool StartRebind(string id, Action<string> done)
    {
        var entry = Entries.FirstOrDefault(e => e.id == id);
        var action = Action(id);
        if (entry == null || entry.locked || action == null || Rebinding) return false;
        string before = action.bindings[0].effectivePath;
        bool wasEnabled = action.enabled;
        action.Disable();
        _operation = action.PerformInteractiveRebinding(0)
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Pointer>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.05f)
            .OnCancel(op => Finish(action, wasEnabled, null, done))
            .OnComplete(op => Finish(action, wasEnabled, Swap(id, action, before), done))
            .Start();
        return true;
    }

    public static void CancelRebind() => _operation?.Cancel();

    private static void Finish(InputAction action, bool wasEnabled, string note, Action<string> done)
    {
        _operation?.Dispose();
        _operation = null;
        _endedFrame = Time.frameCount;
        if (wasEnabled) action.Enable();
        Save();
        done?.Invoke(note);
    }

    // The new key was another entry's: that one takes the old key.
    private static string Swap(string id, InputAction action, string before)
    {
        string now = action.bindings[0].effectivePath;
        foreach (var other in Entries)
        {
            if (other.id == id || other.locked) continue;
            var otherAction = Action(other.id);
            if (otherAction == null || otherAction.bindings.Count == 0 || otherAction.bindings[0].effectivePath != now) continue;
            otherAction.ApplyBindingOverride(0, before);
            return $"{other.name} moves to {Keys(other.id)}.";
        }
        return null;
    }

    /// <summary>Every key back to the game's own.</summary>
    public static void ResetAll()
    {
        CancelRebind();
        _asset?.RemoveAllBindingOverrides();
        World.RemoveAllBindingOverrides();
        Save();
    }

    private static void Save()
    {
        try
        {
            if (_asset != null) PlayerPrefs.SetString(AssetKey, _asset.SaveBindingOverridesAsJson());
            PlayerPrefs.SetString(ExtraKey, World.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }
        catch (Exception) { }
    }
}
