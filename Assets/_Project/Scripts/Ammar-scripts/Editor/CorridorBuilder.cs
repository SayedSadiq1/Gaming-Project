// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Corridor Builder
//  Builds straight corridors between two points using the iPoly3D Server Room
//  modular prefabs (floor, wall, ceiling, ceiling_light) so the corridor
//  visually matches the existing server rooms.
//
//  Usage: top menu → Facility Breach → Corridor Builder
//    1) Place two empty GameObjects in the scene (or use existing transforms)
//       at the START and END of the corridor (e.g. at the doorways).
//    2) Drag them into Start/End in the window.
//    3) Click "Build Corridor".
//
//  Output: A new GameObject "Corridor_<start>_to_<end>" with all tiles inside,
//          easy to move/delete as a group.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class CorridorBuilderWindow : EditorWindow
{
    // Refs
    Transform startPoint;
    Transform endPoint;

    // Geometry
    float tileSize       = 2.0f;   // length covered by one floor tile
    float corridorWidth  = 4.0f;   // distance between the two walls
    float corridorHeight = 3.0f;   // floor-to-ceiling

    // What to spawn
    bool spawnFloor    = true;
    bool spawnWalls    = true;
    bool spawnCeiling  = true;
    bool spawnLights   = true;
    int  lightEveryN   = 3;

    // Prefabs (auto-loaded from iPoly3D path on enable)
    GameObject floorPrefab;
    GameObject wallPrefab;
    GameObject ceilingPrefab;
    GameObject ceilingLightPrefab;

    Transform lastBuilt;

    [MenuItem("Facility Breach/Corridor Builder")]
    static void Open()
    {
        var w = GetWindow<CorridorBuilderWindow>("Corridor Builder");
        w.minSize = new Vector2(340, 460);
        w.Show();
    }

    void OnEnable()
    {
        TryAutoLoadPrefabs();
    }

    void TryAutoLoadPrefabs()
    {
        string baseP = "Assets/iPoly3D/Server Room/Prefabs/";
        if (floorPrefab == null)        floorPrefab        = AssetDatabase.LoadAssetAtPath<GameObject>(baseP + "floor.prefab");
        if (wallPrefab == null)         wallPrefab         = AssetDatabase.LoadAssetAtPath<GameObject>(baseP + "wall.prefab");
        if (ceilingPrefab == null)      ceilingPrefab      = AssetDatabase.LoadAssetAtPath<GameObject>(baseP + "ceiling_single.prefab");
        if (ceilingLightPrefab == null) ceilingLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(baseP + "ceiling_light.prefab");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("CORRIDOR BUILDER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Step 1: Create two empty GameObjects in your scene at the doorway positions where the corridor should start and end. " +
            "(GameObject → Create Empty, then position at the door.)\n\n" +
            "Step 2: Drag them into the slots below.\n\n" +
            "Step 3: Adjust width/height if needed, then Build.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Endpoints", EditorStyles.boldLabel);
        startPoint = (Transform)EditorGUILayout.ObjectField("Start Point", startPoint, typeof(Transform), true);
        endPoint   = (Transform)EditorGUILayout.ObjectField("End Point",   endPoint,   typeof(Transform), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
        tileSize       = EditorGUILayout.FloatField("Tile Size (length)",  tileSize);
        corridorWidth  = EditorGUILayout.FloatField("Corridor Width",      corridorWidth);
        corridorHeight = EditorGUILayout.FloatField("Corridor Height",     corridorHeight);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("What to Spawn", EditorStyles.boldLabel);
        spawnFloor   = EditorGUILayout.Toggle("Floor",   spawnFloor);
        spawnWalls   = EditorGUILayout.Toggle("Walls",   spawnWalls);
        spawnCeiling = EditorGUILayout.Toggle("Ceiling", spawnCeiling);
        using (new EditorGUI.DisabledScope(!spawnCeiling))
        {
            spawnLights = EditorGUILayout.Toggle("Ceiling Lights", spawnLights);
            using (new EditorGUI.DisabledScope(!spawnLights))
                lightEveryN = Mathf.Max(1, EditorGUILayout.IntField("Light Every N tiles", lightEveryN));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefabs (auto-loaded from iPoly3D)", EditorStyles.boldLabel);
        floorPrefab        = (GameObject)EditorGUILayout.ObjectField("Floor",   floorPrefab,        typeof(GameObject), false);
        wallPrefab         = (GameObject)EditorGUILayout.ObjectField("Wall",    wallPrefab,         typeof(GameObject), false);
        ceilingPrefab      = (GameObject)EditorGUILayout.ObjectField("Ceiling", ceilingPrefab,      typeof(GameObject), false);
        ceilingLightPrefab = (GameObject)EditorGUILayout.ObjectField("C-Light", ceilingLightPrefab, typeof(GameObject), false);
        if (GUILayout.Button("Reload Prefabs", GUILayout.Width(120)))
            TryAutoLoadPrefabs();

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(startPoint == null || endPoint == null))
        {
            if (GUILayout.Button("BUILD CORRIDOR", GUILayout.Height(36)))
                Build();
        }

        if (lastBuilt != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last built: " + lastBuilt.name);
            if (GUILayout.Button("Delete Last Corridor"))
            {
                Undo.DestroyObjectImmediate(lastBuilt.gameObject);
                lastBuilt = null;
            }
        }
    }

    void Build()
    {
        if (startPoint == null || endPoint == null) return;
        if (floorPrefab == null && wallPrefab == null && ceilingPrefab == null)
        {
            EditorUtility.DisplayDialog("Corridor Builder",
                "No prefabs assigned. Click 'Reload Prefabs' or assign them manually.", "OK");
            return;
        }

        // Direction & length on horizontal plane only
        Vector3 a   = startPoint.position;
        Vector3 b   = endPoint.position;
        Vector3 dir = b - a;
        dir.y = 0;
        float length = dir.magnitude;
        if (length < 0.1f) { Debug.LogWarning("[CorridorBuilder] Endpoints are too close."); return; }
        dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

        // How many tiles fit (round to fit length)
        int tileCount = Mathf.Max(1, Mathf.RoundToInt(length / tileSize));
        // Recompute effective tile size so the corridor exactly spans start→end
        float effTile = length / tileCount;
        Quaternion rot = Quaternion.LookRotation(dir);

        // Parent
        var parent = new GameObject($"Corridor_{startPoint.name}_to_{endPoint.name}");
        Undo.RegisterCreatedObjectUndo(parent, "Build Corridor");
        parent.transform.position = (a + b) * 0.5f;

        for (int i = 0; i < tileCount; i++)
        {
            // Position of this tile's CENTER along the corridor
            Vector3 pos = a + dir * (effTile * (i + 0.5f));
            pos.y = a.y; // align with start point's Y

            // FLOOR
            if (spawnFloor && floorPrefab != null)
                Spawn(floorPrefab, pos, rot, $"Floor_{i:00}", parent.transform);

            // WALLS (left + right)
            if (spawnWalls && wallPrefab != null)
            {
                Vector3 leftPos  = pos - right * (corridorWidth * 0.5f);
                Vector3 rightPos = pos + right * (corridorWidth * 0.5f);

                Spawn(wallPrefab, leftPos,  rot * Quaternion.Euler(0,  90, 0), $"WallL_{i:00}", parent.transform);
                Spawn(wallPrefab, rightPos, rot * Quaternion.Euler(0, -90, 0), $"WallR_{i:00}", parent.transform);
            }

            // CEILING (alternates with light)
            if (spawnCeiling)
            {
                bool useLight = spawnLights && ceilingLightPrefab != null && (i % lightEveryN == 0);
                var ceil = useLight ? ceilingLightPrefab : ceilingPrefab;
                if (ceil != null)
                {
                    Vector3 ceilPos = pos + Vector3.up * corridorHeight;
                    Spawn(ceil, ceilPos, rot, useLight ? $"CeilingLight_{i:00}" : $"Ceiling_{i:00}", parent.transform);
                }
            }
        }

        lastBuilt = parent.transform;
        Selection.activeGameObject = parent;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CorridorBuilder] Built {tileCount} tiles ({length:0.0}m) between {startPoint.name} and {endPoint.name}.");
    }

    static void Spawn(GameObject prefab, Vector3 pos, Quaternion rot, string name, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = pos;
        go.transform.rotation = rot;
    }
}
