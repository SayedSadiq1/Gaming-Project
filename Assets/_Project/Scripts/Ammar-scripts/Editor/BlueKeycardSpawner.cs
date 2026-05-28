using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Blue Keycard Spawner
//  Menu: Facility Breach → Spawn Blue Keycard (team prefab)
//
//  Drops the team's keycard prefab (used on Levels 1 and 2) into the current
//  scene with keycardID = "blue_keycard". If one already exists, just fixes
//  its ID. Safe to run any number of times.
//
//  Placement priority:
//    1. The currently-selected GameObject in the hierarchy (so you can click
//       a spawn marker and run the menu).
//    2. The Player's position + 2m forward (so it lands near you, easy to grab
//       and drag in the scene view).
//    3. World origin as a last resort.
// ─────────────────────────────────────────────────────────────────────────────
public static class BlueKeycardSpawner
{
    const string TEAM_KEYCARD_PATH = "Assets/_Project/Prefabs/Level1/gate system/keycard.prefab";
    const string KEYCARD_ID        = "blue_keycard";

    [MenuItem("Facility Breach/Spawn Blue Keycard (team prefab)")]
    public static void Spawn()
    {
        var teamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TEAM_KEYCARD_PATH);
        if (teamPrefab == null)
        {
            EditorUtility.DisplayDialog("Keycard prefab missing",
                "Couldn't load:\n" + TEAM_KEYCARD_PATH +
                "\n\nMake sure your friend's keycard.prefab is at that path.", "OK");
            return;
        }

        // If a keycard already exists in the scene, just fix its ID.
        var existing = Object.FindFirstObjectByType<KeycardPickupTrigger>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.keycardID = KEYCARD_ID;
            EditorUtility.SetDirty(existing);
            existing.gameObject.SetActive(true);
            Selection.activeGameObject = existing.gameObject;
            EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
            EditorUtility.DisplayDialog("Keycard already in scene",
                "Found an existing keycard in the scene at " + existing.transform.position +
                ".\nSet keycardID='" + KEYCARD_ID + "' and re-enabled it.\n\n" +
                "Move it where you want it in the Scene view.", "OK");
            return;
        }

        // Decide where to spawn it.
        Vector3 spawnPos;
        Quaternion spawnRot = Quaternion.identity;
        string placement;

        var sel = Selection.activeGameObject;
        if (sel != null && !PrefabUtility.IsPartOfPrefabAsset(sel))
        {
            spawnPos  = sel.transform.position;
            spawnRot  = sel.transform.rotation;
            placement = "at selected object '" + sel.name + "'";
        }
        else
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                spawnPos  = player.transform.position + player.transform.forward * 2f + Vector3.up * 1f;
                placement = "2m in front of the Player";
            }
            else
            {
                spawnPos  = Vector3.zero;
                placement = "at world origin (no Player found)";
            }
        }

        // Spawn it as a prefab instance so prefab updates from your friend
        // automatically apply.
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(teamPrefab);
        Undo.RegisterCreatedObjectUndo(instance, "Spawn Blue Keycard");
        instance.transform.position = spawnPos;
        instance.transform.rotation = spawnRot;

        var trigger = instance.GetComponent<KeycardPickupTrigger>();
        if (trigger != null)
        {
            trigger.keycardID = KEYCARD_ID;
            EditorUtility.SetDirty(trigger);
        }
        else
        {
            Debug.LogWarning("[BlueKeycardSpawner] Spawned prefab has no KeycardPickupTrigger component. " +
                             "Doors using ChainDoorKeycardHolder won't recognise it until that script is on the prefab.");
        }

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);

        EditorUtility.DisplayDialog("Blue keycard spawned",
            "Spawned team keycard.prefab " + placement +
            " with keycardID='" + KEYCARD_ID + "'.\n\n" +
            "It's selected in the hierarchy — drag it to the exact spot you want in the Scene view, " +
            "then save the scene (Ctrl+S).", "OK");
    }
}
