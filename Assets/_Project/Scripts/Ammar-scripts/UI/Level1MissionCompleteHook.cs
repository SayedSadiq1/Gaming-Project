using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 1 Mission Complete Hook
//
//  • Replaces Sayed's SecurityLockdown alarm clip with the Server3Alarm so
//    both levels share the same alarm sound.
//  • Watches SecurityLockdown.OnLockdownLifted (Objective 3 complete) and
//    shows the Mission Complete screen with KILLS pulled from ScoreManager.
//  • NEXT LEVEL button always works — tries Level2, then a fallback list,
//    then MainMenu so it never gets stuck.
//
//  Auto-installs on Level 1 load. Doesn't modify any teammate scripts.
// ─────────────────────────────────────────────────────────────────────────────
public class Level1MissionCompleteHook : MonoBehaviour
{
    [Tooltip("Scene to load when NEXT LEVEL is clicked.")]
    public string nextSceneName = "Level2";

    [Tooltip("Seconds to wait after Objective 3 completes before showing the Mission Complete screen.")]
    public float delayBeforeShow = 3.5f;

    [Tooltip("Path to the alarm clip that should replace Sayed's. Loaded via Resources/AssetDatabase fallback.")]
    public string alarmClipResourcePath = "SFX/Server3Alarm";

    SecurityLockdown _lockdown;
    bool _shown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level1") return;
        if (Object.FindFirstObjectByType<Level1MissionCompleteHook>() != null) return;

        var host = new GameObject("Level1MissionCompleteHook");
        host.AddComponent<Level1MissionCompleteHook>();
    }

    void Start()
    {
        _lockdown = Object.FindFirstObjectByType<SecurityLockdown>(FindObjectsInactive.Include);

        if (_lockdown != null)
        {
            // Replace the alarm sound with ours
            ReplaceAlarmSound();

            _lockdown.OnLockdownLifted += OnLevelComplete;
            Debug.Log("[Level1Complete] Hooked into SecurityLockdown.OnLockdownLifted.");
        }
        else
        {
            Debug.LogWarning("[Level1Complete] No SecurityLockdown found in Level 1.");
        }
    }

    void OnDestroy()
    {
        if (_lockdown != null) _lockdown.OnLockdownLifted -= OnLevelComplete;
    }

    void ReplaceAlarmSound()
    {
        if (_lockdown == null) return;

        // Try to load the alarm clip from any location. Server3Alarm is the
        // canonical name; we look for it via reflection on AssetDatabase
        // (editor-only) AND via Resources.Load for builds.
        AudioClip clip = null;

        // 1. Look at any ServerHack already in the scene (none in Level 1
        //    typically, but harmless) and steal its alarmLoopSound.
        var existingHack = Object.FindFirstObjectByType<ServerHack>(FindObjectsInactive.Include);
        if (existingHack != null && existingHack.alarmLoopSound != null)
            clip = existingHack.alarmLoopSound;

        // 2. Resources fallback (works in builds)
        if (clip == null)
            clip = Resources.Load<AudioClip>(alarmClipResourcePath);

        // 3. Editor-only AssetDatabase lookup
#if UNITY_EDITOR
        if (clip == null)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("Server3Alarm t:AudioClip");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
#endif

        if (clip != null)
        {
            _lockdown.alarmLoop = clip;
            Debug.Log($"[Level1Complete] Replaced SecurityLockdown alarm with '{clip.name}'.");
        }
        else
        {
            Debug.LogWarning("[Level1Complete] Couldn't find Server3Alarm clip — Sayed's original alarm will play.");
        }
    }

    void OnLevelComplete()
    {
        if (_shown) return;
        _shown = true;
        StartCoroutine(ShowAfterDelay());
    }

    IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeShow);

        // Pull KILL count from ScoreManager
        int kills = 0;
        var scoreMgr = Object.FindFirstObjectByType<ScoreManager>(FindObjectsInactive.Include);
        if (scoreMgr != null) kills = scoreMgr.Kills;

        string stats =
            "<color=#00C8FF>OBJECTIVES</color>\n3 / 3 COMPLETE\n\n" +
            $"<color=#00C8FF>KILLS</color>\n{kills}\n\n" +
            "<color=#00C8FF>STATUS</color>\nFACILITY GATE CLEARED";

        // Pick the scene to load: configured first, then sane fallbacks.
        string chosen = ResolveNextScene();
        MissionCompleteScreen.Show("LEVEL 1 COMPLETE", stats, chosen, fadeDuration: 1.8f);
        Debug.Log($"[Level1Complete] Mission Complete shown. Next scene = '{chosen}'.");
    }

    string ResolveNextScene()
    {
        // Try configured name first
        if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
            return nextSceneName;

        // Common spellings of "Level 2" we might find in Build Settings
        string[] fallbacks = { "Level2", "Level 2", "Level-2", "level2" };
        foreach (var name in fallbacks)
        {
            if (Application.CanStreamedLevelBeLoaded(name)) return name;
        }

        // Final fallback — MissionCompleteScreen will then redirect to MainMenu
        return nextSceneName;
    }
}
