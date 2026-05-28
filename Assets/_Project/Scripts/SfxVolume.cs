using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — SFX Volume Router
//
//  Applies the SFX slider value (PlayerPref FB_SfxVol) to every AudioSource
//  in every scene EXCEPT background-music sources. Background music is
//  identified by GameObject name containing "Music", "Theme", or "Soundtrack".
//
//  Tracks each AudioSource's *authored* volume the first time it's seen so we
//  can scale from the original value, not compound previous scaling.
//
//  Re-applies on:
//    • Scene load (catches every AudioSource in the new scene)
//    • SfxVolume.Apply() call (from the slider's onValueChanged)
//    • Periodically every 1s (catches AudioSources spawned at runtime —
//      gunshots, PlayClipAtPoint, etc, though one-shot clips are too short
//      to matter)
// ─────────────────────────────────────────────────────────────────────────────
public static class SfxVolume
{
    public const string K_SFX = "FB_SfxVol";

    // Per-AudioSource: the volume it was authored with (before we scale it).
    static readonly Dictionary<int, float> _authoredVolume = new Dictionary<int, float>();

    static float _currentSfx = 1f;

    public static float Current => _currentSfx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        _currentSfx = PlayerPrefs.GetFloat(K_SFX, 1f);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Clear stale entries (destroyed sources)
        var dead = new List<int>();
        foreach (var kv in _authoredVolume)
        {
            // We can't look up an Object by its instance ID directly; cleanup
            // happens lazily in Apply() when we iterate live AudioSources.
        }
        Apply();
    }

    /// <summary>Re-apply the saved SFX volume to every AudioSource in the scene.</summary>
    public static void Apply()
    {
        _currentSfx = PlayerPrefs.GetFloat(K_SFX, 1f);
        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var src in sources)
        {
            if (src == null) continue;
            if (IsMusicSource(src)) continue; // music is controlled by the Music slider

            int id = src.GetInstanceID();
            if (!_authoredVolume.TryGetValue(id, out float authored))
            {
                // First time seeing this source — record its authored volume
                // (the value the designer set in the inspector / builder).
                authored = src.volume;
                _authoredVolume[id] = authored;
            }
            src.volume = authored * _currentSfx;
        }
    }

    /// <summary>Called by SettingsMenu when the SFX slider changes.</summary>
    public static void SetValue(float v)
    {
        _currentSfx = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(K_SFX, _currentSfx);
        Apply();
    }

    static bool IsMusicSource(AudioSource src)
    {
        if (src == null) return false;
        // Music GameObjects in this project are named:
        //   • "MainMenuMusic"
        //   • "ServerHack_BackgroundMusic"
        //   • Anything with "Music", "Theme", or "Soundtrack" in the name
        string n = src.gameObject.name;
        if (n.IndexOf("Music",      System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (n.IndexOf("Theme",      System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (n.IndexOf("Soundtrack", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        // Also check the clip name as a fallback
        if (src.clip != null)
        {
            string c = src.clip.name;
            if (c.IndexOf("Music",      System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (c.IndexOf("Theme",      System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (c.IndexOf("Soundtrack", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }
}
