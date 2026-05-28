using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 5 Grenade Setup
//  Menu: Facility Breach → Add Grenade System to Player (current scene)
//
//  Level 5 is missing the throwable-grenade system that Level 3 has. This adds
//  the Aegis GrenadeSystem (press G to throw, starts with 3) to the Player in
//  the currently open scene and wires every field in code:
//      • player      → the Player root
//      • throwPoint  → the player's view camera (spawn position/rotation)
//      • camera      → the player's view camera (throw direction)
//      • hiexgrenade → Hi-Ex Grenade Thrown.prefab
//  It also adds AegisGrenadeHUDSync so the HUD frag counter tracks the grenades
//  (harmless if the scene has no CombatHUD).
//
//  HOW TO USE: open Level5 scene → Facility Breach → Add Grenade System... →
//  the script saves the scene for you. Safe to re-run (idempotent).
// ─────────────────────────────────────────────────────────────────────────────
public static class Level5GrenadeSetup
{
    const string GRENADE_PREFAB_PATH =
        "Assets/_Project/Weapons/AegisGrenades/Prefabs/Hi-Ex Grenade Thrown.prefab";

    [MenuItem("Facility Breach/Add Grenade System to Player (current scene)")]
    public static void AddGrenadeSystem()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("=== Level 5 Grenade Setup ===");

        // ── 1. Find the Player ────────────────────────────────────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // Fallback: search for the FPS character controller by type name
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb != null && mb.GetType().Name == "PlayerCharacterController")
                {
                    player = mb.gameObject;
                    break;
                }
            }
        }
        if (player == null)
        {
            log.AppendLine("✗ No Player found (no GameObject tagged 'Player' and no " +
                           "PlayerCharacterController). Open the Level 5 scene and re-run.");
            EditorUtility.DisplayDialog("Grenade Setup", log.ToString(), "OK");
            Debug.LogWarning(log.ToString());
            return;
        }
        log.AppendLine("✓ Player found: " + player.name);

        // ── 2. Find the player's view camera ──────────────────────────────────
        Camera cam = null;
        var cams = player.GetComponentsInChildren<Camera>(true);
        foreach (var c in cams)
        {
            if (c != null && c.CompareTag("MainCamera")) { cam = c; break; }
        }
        if (cam == null && cams.Length > 0) cam = cams[0];
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            log.AppendLine("✗ No Camera found under the Player. Cannot set throw direction.");
            EditorUtility.DisplayDialog("Grenade Setup", log.ToString(), "OK");
            Debug.LogWarning(log.ToString());
            return;
        }
        log.AppendLine("✓ View camera: " + cam.name);

        // ── 3. Load the thrown-grenade prefab ─────────────────────────────────
        var grenadePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GRENADE_PREFAB_PATH);
        if (grenadePrefab == null)
            log.AppendLine("✗ Grenade prefab not found at " + GRENADE_PREFAB_PATH +
                           " — adding the component anyway; assign 'hiexgrenade' manually.");
        else
            log.AppendLine("✓ Grenade prefab loaded.");

        // ── 4. Add / fetch the Aegis GrenadeSystem ────────────────────────────
        var grenadeSystem = player.GetComponent<Aegis.GrenadeSystem.HiEx.GrenadeSystem>();
        if (grenadeSystem == null)
        {
            grenadeSystem = player.AddComponent<Aegis.GrenadeSystem.HiEx.GrenadeSystem>();
            log.AppendLine("✓ Added GrenadeSystem component to Player.");
        }
        else
        {
            log.AppendLine("✓ Player already had GrenadeSystem — re-wiring fields.");
        }

        // ── 5. Wire the private [SerializeField] fields via SerializedObject ───
        var so = new SerializedObject(grenadeSystem);
        SetObjectField(so, "player", player);
        SetObjectField(so, "throwPoint", cam.transform);
        SetObjectField(so, "camera", cam.transform);
        if (grenadePrefab != null) SetObjectField(so, "hiexgrenade", grenadePrefab);

        // Make sure throwing actually works with sane defaults if this is fresh.
        var countProp = so.FindProperty("grenadeCount");
        if (countProp != null && countProp.intValue <= 0) countProp.intValue = 3;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("✓ GrenadeSystem fields wired (player, throwPoint, camera, hiexgrenade).");

        // ── 6. HUD sync (frag counter) ─────────────────────────────────────────
        if (player.GetComponent<AegisGrenadeHUDSync>() == null)
        {
            player.AddComponent<AegisGrenadeHUDSync>();
            log.AppendLine("✓ Added AegisGrenadeHUDSync (HUD frag counter).");
        }
        else
        {
            log.AppendLine("✓ AegisGrenadeHUDSync already present.");
        }

        // ── 7. Save ────────────────────────────────────────────────────────────
        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveOpenScenes();
        log.AppendLine("✓ Scene saved. Press Play and hit G to throw a grenade.");

        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("Grenade Setup Complete", log.ToString(), "OK");
    }

    static void SetObjectField(SerializedObject so, string field, Object value)
    {
        var prop = so.FindProperty(field);
        if (prop != null) prop.objectReferenceValue = value;
        else Debug.LogWarning("[Level5GrenadeSetup] Field not found: " + field);
    }
}
