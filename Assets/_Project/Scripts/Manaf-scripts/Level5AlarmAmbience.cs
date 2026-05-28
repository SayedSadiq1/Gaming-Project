using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 5 Alarm Ambience
//
//  Plays a looping 2D alarm/siren in the background so Level 5 feels like a
//  base-breakout. Reuses the shared Server3Alarm clip (same as Level 1 / 3).
//
//  Two ways to use it — both work:
//    • Zero setup: it self-installs on Level 5 load and auto-finds the clip
//      (works in the editor straight away).
//    • Build-safe: drop this component on an empty GameObject in Level5 and
//      drag the Server3Alarm clip into the Alarm Clip slot. A serialized
//      reference is the only way the clip is guaranteed to ship in a build.
//
//  Volume respects the SFX slider (SfxVolume scans non-music AudioSources).
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class Level5AlarmAmbience : MonoBehaviour
{
    [Tooltip("Drag Server3Alarm.mp3 here to guarantee it ships in a build. " +
             "If left empty, it's auto-located (editor / Resources / scene).")]
    public AudioClip alarmClip;

    [Tooltip("Authored volume before the SFX slider scales it. Handover suggests ~0.6.")]
    [Range(0f, 1f)]
    public float volume = 0.6f;

    AudioSource _src;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level5") return;
        if (Object.FindFirstObjectByType<Level5AlarmAmbience>() != null) return; // already placed in scene
        new GameObject("Level5 Alarm").AddComponent<Level5AlarmAmbience>();
    }

    void Start()
    {
        var clip = ResolveClip();
        if (clip == null)
        {
            Debug.LogWarning("[Level5Alarm] Couldn't find Server3Alarm clip. " +
                             "Drag it into the Alarm Clip slot to be safe.", this);
            return;
        }

        _src = GetComponent<AudioSource>();
        if (_src == null) _src = gameObject.AddComponent<AudioSource>();

        _src.clip          = clip;
        _src.loop          = true;
        _src.playOnAwake   = false;
        _src.spatialBlend  = 0f;   // 2D — heard everywhere
        _src.volume        = volume;
        _src.Play();

        // Register with the SFX router so the slider controls it immediately
        // (otherwise it isn't scaled until the next slider change / scene load).
        SfxVolume.Apply();
    }

    AudioClip ResolveClip()
    {
        if (alarmClip != null) return alarmClip;

        // Steal from a ServerHack already in the scene, if any.
        var hack = Object.FindFirstObjectByType<ServerHack>(FindObjectsInactive.Include);
        if (hack != null && hack.alarmLoopSound != null) return hack.alarmLoopSound;

        // Resources fallback (works in builds IF the clip lives under a Resources folder).
        var res = Resources.Load<AudioClip>("SFX/Server3Alarm");
        if (res != null) return res;

#if UNITY_EDITOR
        // Editor-only: locate by name anywhere in the project.
        var guids = UnityEditor.AssetDatabase.FindAssets("Server3Alarm t:AudioClip");
        if (guids.Length > 0)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
#endif
        return null;
    }
}
