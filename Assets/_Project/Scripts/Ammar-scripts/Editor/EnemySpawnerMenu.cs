using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Enemy Spawner Menu
//
//  Two menu items:
//    • Facility Breach → Create Enemy Spawner
//        Drops an empty GameObject in the scene with an EnemySpawner attached,
//        pre-wired with the team's default enemy prefab (Soldier_demo_gun).
//        Positioned at the SceneView pivot so it lands where you're looking.
//
//    • Facility Breach → Create Enemy Spawner with 3 Spawn Points
//        Same, plus three child empty "SpawnPoint" markers offset around it
//        (handy for ambush triangles).
// ─────────────────────────────────────────────────────────────────────────────
public static class EnemySpawnerMenu
{
    const string DEFAULT_ENEMY_PATH = "Assets/_Project/Prefabs/Enemies/Soldier_demo_gun.prefab";

    [MenuItem("Facility Breach/Create Enemy Spawner")]
    public static void CreateSpawner()
    {
        var go = NewSpawner("EnemySpawner");
        Selection.activeGameObject = go;
        Debug.Log($"[EnemySpawnerMenu] Created '{go.name}' at {go.transform.position}.");
    }

    [MenuItem("Facility Breach/Create Enemy Spawner with 3 Spawn Points")]
    public static void CreateSpawnerWithPoints()
    {
        var go  = NewSpawner("EnemySpawner_Ambush");
        var sp  = go.GetComponent<EnemySpawner>();

        var pts = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var p = new GameObject($"SpawnPoint_{i+1}");
            Undo.RegisterCreatedObjectUndo(p, "Create SpawnPoint");
            p.transform.SetParent(go.transform, false);
            float ang = (i / 3f) * Mathf.PI * 2f;
            p.transform.localPosition = new Vector3(Mathf.Cos(ang) * 2.0f, 0, Mathf.Sin(ang) * 2.0f);
            pts[i] = p.transform;
        }
        sp.spawnPoints = pts;
        sp.totalToSpawn = 3;
        sp.perWave      = 3;

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[EnemySpawnerMenu] Created ambush spawner with 3 spawn points.");
    }

    static GameObject NewSpawner(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Enemy Spawner");

        // Drop at SceneView pivot so it lands where the editor is looking
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) go.transform.position = sv.pivot;

        var spawner = go.AddComponent<EnemySpawner>();
        var enemy   = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_ENEMY_PATH);
        if (enemy != null)
        {
            spawner.enemyPrefabs = new[] { enemy };
            Debug.Log($"[EnemySpawnerMenu] Defaulted enemyPrefab to '{enemy.name}'.");
        }
        else
        {
            Debug.LogWarning($"[EnemySpawnerMenu] Default enemy prefab not found at '{DEFAULT_ENEMY_PATH}'. " +
                             "Drag any enemy prefab into the EnemySpawner.enemyPrefabs list manually.");
        }

        spawner.triggerMode    = EnemySpawner.TriggerMode.Manual;
        spawner.totalToSpawn   = 3;
        spawner.perWave        = 1;
        spawner.spawnInterval  = 0.3f;
        spawner.waveInterval   = 2.0f;
        spawner.scatterRadius  = 1.5f;
        spawner.snapToNavMesh  = true;

        // A trigger collider for OnTriggerEnter mode (disabled by default —
        // user enables it from the inspector when they switch trigger mode).
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = 3.0f;
        col.enabled   = false;   // off until user switches triggerMode

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return go;
    }
}
