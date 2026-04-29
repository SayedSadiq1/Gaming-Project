using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Key Bindings
//  Central registry of rebindable controls. Persists via PlayerPrefs.
//  Other scripts read with KeyBindings.Get("Shoot") instead of hardcoding KeyCode.Mouse0.
// ─────────────────────────────────────────────────────────────────────────────
public static class KeyBindings
{
    // ── Action list (order = display order in UI) ──
    public static readonly string[] ActionOrder =
    {
        "MoveForward", "MoveBack", "MoveLeft", "MoveRight",
        "Jump", "Sprint", "Crouch",
        "Shoot", "Aim", "Reload",
        "Interact", "Pause"
    };

    public static readonly Dictionary<string, string> Display = new()
    {
        { "MoveForward", "Move Forward" },
        { "MoveBack",    "Move Backward" },
        { "MoveLeft",    "Move Left" },
        { "MoveRight",   "Move Right" },
        { "Jump",        "Jump" },
        { "Sprint",      "Sprint" },
        { "Crouch",      "Crouch" },
        { "Shoot",       "Shoot / Fire" },
        { "Aim",         "Aim Down Sights" },
        { "Reload",      "Reload" },
        { "Interact",    "Interact" },
        { "Pause",       "Pause / Menu" },
    };

    public static readonly Dictionary<string, KeyCode> Defaults = new()
    {
        { "MoveForward", KeyCode.W },
        { "MoveBack",    KeyCode.S },
        { "MoveLeft",    KeyCode.A },
        { "MoveRight",   KeyCode.D },
        { "Jump",        KeyCode.Space },
        { "Sprint",      KeyCode.LeftShift },
        { "Crouch",      KeyCode.LeftControl },
        { "Shoot",       KeyCode.Mouse0 },
        { "Aim",         KeyCode.Mouse1 },
        { "Reload",      KeyCode.R },
        { "Interact",    KeyCode.E },
        { "Pause",       KeyCode.Escape },
    };

    static Dictionary<string, KeyCode> _bindings;
    const string PREF_PREFIX = "FB_Bind_";

    static void EnsureLoaded()
    {
        if (_bindings != null) return;
        _bindings = new Dictionary<string, KeyCode>();
        foreach (var kv in Defaults)
        {
            int saved = PlayerPrefs.GetInt(PREF_PREFIX + kv.Key, (int)kv.Value);
            _bindings[kv.Key] = (KeyCode)saved;
        }
    }

    public static KeyCode Get(string action)
    {
        EnsureLoaded();
        return _bindings.TryGetValue(action, out var k) ? k :
               (Defaults.TryGetValue(action, out var d) ? d : KeyCode.None);
    }

    public static void Set(string action, KeyCode key)
    {
        EnsureLoaded();
        _bindings[action] = key;
        PlayerPrefs.SetInt(PREF_PREFIX + action, (int)key);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        EnsureLoaded();
        foreach (var kv in Defaults)
        {
            _bindings[kv.Key] = kv.Value;
            PlayerPrefs.DeleteKey(PREF_PREFIX + kv.Key);
        }
        PlayerPrefs.Save();
    }

    // Pretty-print a KeyCode for the UI ("LeftShift" → "L Shift", "Mouse0" → "Left Click")
    public static string Pretty(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Mouse0: return "Left Click";
            case KeyCode.Mouse1: return "Right Click";
            case KeyCode.Mouse2: return "Middle Click";
            case KeyCode.Mouse3: return "Mouse 4";
            case KeyCode.Mouse4: return "Mouse 5";
            case KeyCode.LeftShift:   return "Left Shift";
            case KeyCode.RightShift:  return "Right Shift";
            case KeyCode.LeftControl: return "Left Ctrl";
            case KeyCode.RightControl:return "Right Ctrl";
            case KeyCode.LeftAlt:     return "Left Alt";
            case KeyCode.RightAlt:    return "Right Alt";
            case KeyCode.Space:       return "Space";
            case KeyCode.Escape:      return "Esc";
            case KeyCode.Return:      return "Enter";
            default:                  return k.ToString();
        }
    }

    // Helper for game logic
    public static bool GetKey(string action)     => Input.GetKey(Get(action));
    public static bool GetKeyDown(string action) => Input.GetKeyDown(Get(action));
    public static bool GetKeyUp(string action)   => Input.GetKeyUp(Get(action));
}
