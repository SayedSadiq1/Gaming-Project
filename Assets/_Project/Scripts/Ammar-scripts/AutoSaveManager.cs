using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Auto-Save Manager (full state persistence)
//  Saves & restores:
//    • Player position, rotation, health, current weapon's ammo
//    • Per-scene list of killed enemies (they stay dead after reload)
//  Auto-spawns in every gameplay scene via SceneManager.sceneLoaded.
// ─────────────────────────────────────────────────────────────────────────────
public class AutoSaveManager : MonoBehaviour
{
    public static AutoSaveManager Instance { get; private set; }

    public float saveInterval = 5f;
    float _timer;

    // ── Pause flag (toggle from menu: Facility Breach → AutoSave: Pause/Resume).
    //    Reads PlayerPrefs key 'FB_AutoSave_Paused' so it persists in the editor
    //    and at runtime. When true, all save AND restore calls early-out.
    public static bool IsPaused => PlayerPrefs.GetInt("FB_AutoSave_Paused", 0) == 1;

    // ── Dead enemy tracking (per scene, lifetime of session) ───────────────
    readonly List<Vector3> _deadEnemiesThisScene = new List<Vector3>();
    string _currentSceneKey = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Debug.Log("[AutoSave] Bootstrap — subscribing to sceneLoaded.");
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("[AutoSave] OnSceneLoaded → " + scene.name);

        if (scene.name == "MainMenu")
        {
            Debug.Log("[AutoSave] Skipping MainMenu.");
            return;
        }

        if (Instance == null)
        {
            var go = new GameObject("AutoSaveManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AutoSaveManager>();
            Debug.Log("[AutoSave] ✓ Created AutoSaveManager.");
        }
        else
        {
            Debug.Log("[AutoSave] AutoSaveManager already exists.");
            Instance._timer = 0f;
        }

        Instance._currentSceneKey = scene.name;
        Instance.LoadDeadEnemyListForScene();

        if (SaveSystem.ContinueRequested)
        {
            Debug.Log("[AutoSave] ContinueRequested = true → restoring in 0.5s.");
            SaveSystem.ContinueRequested = false;
            Instance.Invoke(nameof(SafeRestore), 0.5f);
        }
        else
        {
            Debug.Log("[AutoSave] ContinueRequested = false → new game.");
        }

        // Always subscribe to enemy deaths so kills get persisted
        Instance.Invoke(nameof(SubscribeToEnemyDeaths), 0.7f);
        // Apply previously-killed enemies regardless of restore flag
        Instance.Invoke(nameof(RemovePreviouslyDeadEnemies), 0.6f);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= saveInterval)
        {
            _timer = 0f;
            SafeSave();
        }
    }

    void OnApplicationQuit() { Debug.Log("[AutoSave] Quitting — final save."); SafeSave(); }
    void OnApplicationPause(bool paused) { if (paused) SafeSave(); }

    // ════════════════════════════════════════════════════════════════════════
    //  SAVE / RESTORE
    // ════════════════════════════════════════════════════════════════════════
    void SafeSave()
    {
        if (IsPaused) { return; }   // paused via menu — silently skip
        try { SaveCurrentState(); }
        catch (System.Exception e) { Debug.LogWarning("[AutoSave] Save: " + e.Message); }
    }
    void SafeRestore()
    {
        if (IsPaused) { Debug.Log("[AutoSave] PAUSED — skipping restore."); return; }
        try { RestoreState(); }
        catch (System.Exception e) { Debug.LogWarning("[AutoSave] Restore: " + e.Message); }
    }

    void SaveCurrentState()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogWarning("[AutoSave] No Player tag, skipping."); return; }

        float hp    = ReadHealthCurrent(player);
        int   ammo  = ReadCurrentAmmo(player);
        int   objs  = ReadObjectivesDone();
        int   frags = ReadFragCount();
        Vector3 pos = player.transform.position;

        SaveSystem.Save(
            SceneManager.GetActiveScene().name,
            pos, player.transform.eulerAngles.y,
            hp, ammo, objs, frags);

        Debug.Log($"[AutoSave] ✓ Saved pos={pos} HP={hp} Ammo={ammo}");
    }

    void RestoreState()
    {
        Debug.Log("[AutoSave] RestoreState invoked.");
        if (!PlayerPrefs.HasKey("FB_Save_PosX"))
        {
            Debug.Log("[AutoSave] No saved position — default spawn.");
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogWarning("[AutoSave] No Player tag for restore."); return; }

        var pos = SaveSystem.GetSavedPos();
        if (float.IsNaN(pos.x) || pos.magnitude > 100000f)
        {
            Debug.LogWarning("[AutoSave] Saved position invalid, skipping teleport.");
            return;
        }

        // Teleport
        var cc = player.GetComponent<CharacterController>();
        bool ccWasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        player.transform.position = pos;
        player.transform.rotation = Quaternion.Euler(0f, SaveSystem.GetSavedRotY(), 0f);
        if (cc != null) cc.enabled = ccWasEnabled;

        // Health + ammo
        WriteHealthCurrent(player, SaveSystem.GetSavedHealth());
        WriteCurrentAmmo(player, SaveSystem.GetSavedAmmo());

        Debug.Log($"[AutoSave] ✓ Restored to {pos} HP={SaveSystem.GetSavedHealth()} Ammo={SaveSystem.GetSavedAmmo()}");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DEAD ENEMY TRACKING
    // ════════════════════════════════════════════════════════════════════════
    void SubscribeToEnemyDeaths()
    {
        int count = 0;
        var all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c == null || c.GetType().Name != "Health") continue;
            if (c.gameObject.CompareTag("Player")) continue;

            // Capture position so the closure has it after the enemy is destroyed
            Vector3 capturedPos = c.transform.position;
            var field = c.GetType().GetField("OnDie");
            if (field == null) continue;

            UnityAction handler = () =>
            {
                Debug.Log("[AutoSave] Enemy died at " + capturedPos);
                if (!_deadEnemiesThisScene.Contains(capturedPos))
                {
                    _deadEnemiesThisScene.Add(capturedPos);
                    SaveDeadEnemyListForScene();
                }
            };

            var current = field.GetValue(c) as UnityAction;
            field.SetValue(c, current + handler);
            count++;
        }
        Debug.Log($"[AutoSave] Subscribed to {count} enemy Health.OnDie events.");
    }

    void RemovePreviouslyDeadEnemies()
    {
        if (_deadEnemiesThisScene.Count == 0)
        {
            Debug.Log("[AutoSave] No previously-killed enemies in this scene.");
            return;
        }

        var all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removed = 0;
        foreach (var c in all)
        {
            if (c == null || c.GetType().Name != "Health") continue;
            if (c.gameObject.CompareTag("Player")) continue;

            Vector3 enemyPos = c.transform.position;
            foreach (var deadPos in _deadEnemiesThisScene)
            {
                if (Vector3.Distance(enemyPos, deadPos) < 1.5f)
                {
                    Destroy(c.gameObject);
                    removed++;
                    break;
                }
            }
        }
        Debug.Log($"[AutoSave] Removed {removed} previously-killed enemies.");
    }

    void SaveDeadEnemyListForScene()
    {
        if (string.IsNullOrEmpty(_currentSceneKey)) return;
        var sb = new System.Text.StringBuilder();
        foreach (var p in _deadEnemiesThisScene)
        {
            if (sb.Length > 0) sb.Append(';');
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0:F2},{1:F2},{2:F2}", p.x, p.y, p.z);
        }
        PlayerPrefs.SetString("FB_DeadEnemies_" + _currentSceneKey, sb.ToString());
        PlayerPrefs.Save();
    }

    void LoadDeadEnemyListForScene()
    {
        _deadEnemiesThisScene.Clear();
        if (string.IsNullOrEmpty(_currentSceneKey)) return;
        string s = PlayerPrefs.GetString("FB_DeadEnemies_" + _currentSceneKey, "");
        if (string.IsNullOrEmpty(s)) return;

        foreach (var entry in s.Split(';'))
        {
            var parts = entry.Split(',');
            if (parts.Length != 3) continue;
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                _deadEnemiesThisScene.Add(new Vector3(x, y, z));
            }
        }
        Debug.Log($"[AutoSave] Loaded {_deadEnemiesThisScene.Count} previously-killed enemies for scene '{_currentSceneKey}'.");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  REFLECTION HELPERS
    // ════════════════════════════════════════════════════════════════════════
    static float ReadHealthCurrent(GameObject p)
    {
        try
        {
            foreach (var c in p.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "Health") continue;
                var prop = c.GetType().GetProperty("CurrentHealth");
                if (prop != null) return (float)prop.GetValue(c);
            }
        } catch { }
        return 100f;
    }

    static void WriteHealthCurrent(GameObject p, float value)
    {
        try
        {
            foreach (var c in p.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "Health") continue;
                var prop = c.GetType().GetProperty("CurrentHealth");
                if (prop != null && prop.CanWrite) prop.SetValue(c, value);
                return;
            }
        } catch { }
    }

    static int ReadCurrentAmmo(GameObject p)
    {
        try
        {
            foreach (var c in p.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "WeaponController") continue;
                if (!c.gameObject.activeSelf) continue;
                var m = c.GetType().GetMethod("GetCurrentAmmo");
                if (m != null) return (int)m.Invoke(c, null);
            }
        } catch { }
        return 30;
    }

    static void WriteCurrentAmmo(GameObject p, int ammo)
    {
        try
        {
            foreach (var c in p.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "WeaponController") continue;
                if (!c.gameObject.activeSelf) continue;
                var field = c.GetType().GetField("m_CurrentAmmo",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(c, (float)ammo);
                    Debug.Log("[AutoSave] Restored ammo on " + c.gameObject.name + " → " + ammo);
                }
            }
        } catch { }
    }

    static int ReadObjectivesDone()
    {
        try
        {
            var t = System.Type.GetType("ServerHack, Assembly-CSharp");
            if (t == null) return 0;
            var prop = t.GetProperty("HackedCount", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) return (int)prop.GetValue(null);
        } catch { }
        return 0;
    }

    static int ReadFragCount()
    {
        try
        {
            foreach (var c in Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c == null || c.GetType().Name != "GrenadeSystem") continue;
                var f = c.GetType().GetField("grenadeCount",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return (int)f.GetValue(c);
            }
        } catch { }
        return 3;
    }
}
