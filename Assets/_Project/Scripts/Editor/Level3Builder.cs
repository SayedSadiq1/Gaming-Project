// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Level 3 Builder  (L-SHAPED WINDING DESIGN)
//  Usage: top menu → Facility Breach → Build Level 3
//  Output: Assets/_Project/Scenes/Level3.unity
//
//  Top-down shape:  player walks EAST, then turns 90° SOUTH, then exits.
//
//   ┌────────┐ y=0
//   │ SPAWN  │
//   │ LOBBY  │
//   └───┬────┘
//       │
//       │ stair down (sloped, y=0 → y=-5)
//       │
//   ┌───┴──────────────────────────────┬─────┐
//   │                                  │     │  y=-5
//   │  EAST CORRIDOR  (long, 36m)      │     │
//   │  ── with LAB A & LAB B branching│     │
//   │     off to the NORTH ──          │     │
//   └───────────────────────────┬──────┘     │
//                               │            │
//                               │ SOUTH      │
//                               │ CORRIDOR   │
//                               │            │
//                               └────┬───────┘
//                                    │
//                              ┌─────┴───────┐
//                              │             │
//                              │   LAB C     │
//                              │  (big room) │
//                              │             │
//                              └─────┬───────┘
//                                    │
//                                    │ stair up (sloped, y=-5 → y=0)
//                                    │
//                              ┌─────┴───────┐
//                              │  EXIT ROOM  │  y=0
//                              └─────────────┘
//
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Level3Builder
{
    const float H        = 4f;     // floor → ceiling distance
    const float T        = 0.3f;   // wall / floor / ceiling thickness
    const float Y_UP     = 0f;     // upper floor level
    const float Y_DOWN   = -5f;    // lower floor level

    // Stair geometry: floor slopes 5m vertical over 12m horizontal
    const float STAIR_ANGLE = 22.6199f;
    const float STAIR_LEN   = 13f;   // hypotenuse: sqrt(144+25)

    static Material s_wall;
    static Material s_floor;
    static Material s_server;
    static Transform s_root;

    // ─────────────────────────────────────────────────────────────────────────
    [MenuItem("Facility Breach/Build Level 3")]
    static void Build()
    {
        EnsureFolder("Assets/_Project", "Materials");
        EnsureFolder("Assets/_Project", "Scenes");

        s_wall   = GetOrCreateMat("Level3_Wall",   new Color32(0x60, 0x60, 0x65, 0xFF));
        s_floor  = GetOrCreateMat("Level3_Floor",  new Color32(0x42, 0x42, 0x46, 0xFF));
        s_server = GetOrCreateMat("Level3_Server", new Color32(0x00, 0xFF, 0x55, 0xFF), emissive: true);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        s_root = new GameObject("Level3_Root").transform;

        BuildSpawnLobby();        // Y_UP
        BuildStairDown();         // slope Y_UP → Y_DOWN
        BuildEastCorridor();      // Y_DOWN, runs east 36m
        BuildLabA();              // Y_DOWN, branches NORTH off east corridor
        BuildLabB();              // Y_DOWN, branches NORTH off east corridor (further east)
        BuildSouthCorridor();     // Y_DOWN, 90° turn south at east end
        BuildLabC();              // Y_DOWN, big room at south
        BuildStairUpAndExit();    // slope Y_DOWN → Y_UP, then exit room

        SetupLighting();
        PlaceEssentials();

        EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/Level3.unity");
        AssetDatabase.Refresh();
        Debug.Log("[Level3Builder] Done — L-shaped multi-floor scene saved to Assets/_Project/Scenes/Level3.unity");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 1 — SPAWN LOBBY  (UPPER, y=0)
    //  x:[0..10]  z:[0..10]   (10 × 10)
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildSpawnLobby()
    {
        float yMid = Y_UP + H/2;
        Box("Lobby_Floor",   5, Y_UP - T/2,    5, 10, T, 10, true);
        Box("Lobby_Ceiling", 5, Y_UP + H + T/2,5, 10, T, 10);
        Box("Lobby_Back",    5, yMid, -T/2,            10+2*T, H, T);
        Box("Lobby_Left",   -T/2, yMid, 5,             T, H, 10);
        Box("Lobby_Right",   10 + T/2, yMid, 5,        T, H, 10);
        // Front wall (z=10) — gap x:[3..7] for stair connector
        Box("Lobby_Front_L", 1.5f, yMid, 10 + T/2,     3, H, T);
        Box("Lobby_Front_R", 8.5f, yMid, 10 + T/2,     3, H, T);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 2 — STAIR DOWN  z:[10..26]
    //  z:[10..14] flat connector at y=0
    //  z:[14..26] sloped, y=0 → y=-5
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildStairDown()
    {
        float yMid = Y_UP + H/2;

        // Flat connector  x:[3..7]  z:[10..14]
        Box("Conn_Floor",    5, Y_UP - T/2,    12,  4, T, 4, true);
        Box("Conn_Ceiling",  5, Y_UP + H + T/2,12,  4, T, 4);
        Box("Conn_WallL",    3 - T/2, yMid, 12,    T, H, 4);
        Box("Conn_WallR",    7 + T/2, yMid, 12,    T, H, 4);

        // Sloped section  x:[3..7]  z:[14..26]
        SlopedBox("Stair_Floor",   new Vector3(5, -2.5f, 20),  new Vector3(4, T, STAIR_LEN), STAIR_ANGLE, true);
        SlopedBox("Stair_Ceiling", new Vector3(5,  1.5f, 20),  new Vector3(4, T, STAIR_LEN), STAIR_ANGLE);
        // Tall walls covering both Y levels
        Box("Stair_WallL",   3 - T/2, -0.5f, 20,  T, 9, 12);
        Box("Stair_WallR",   7 + T/2, -0.5f, 20,  T, 9, 12);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 3 — EAST CORRIDOR  (LOWER, y=-5)
    //  x:[3..42]  z:[26..32]   (39 × 6)  — long winding hallway
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildEastCorridor()
    {
        float yMid = Y_DOWN + H/2;
        // Floor + ceiling
        Box("EC_Floor",   22.5f, Y_DOWN - T/2,    29, 39, T, 6, true);
        Box("EC_Ceiling", 22.5f, Y_DOWN + H + T/2,29, 39, T, 6);

        // South wall (z=26) — gap x:[3..7] for stair, otherwise solid
        Box("EC_South_L", 1.5f, yMid, 26 - T/2,  3, H, T);   // x:0..3 (filler)
        Box("EC_South_R", 24.5f, yMid, 26 - T/2, 35, H, T);  // x:7..42

        // North wall (z=32) — gaps for Lab A (x:10..22) and Lab B (x:26..38)
        Box("EC_North_A", 6.5f,  yMid, 32 + T/2, 7, H, T);   // x:3..10
        Box("EC_North_B", 24,    yMid, 32 + T/2, 4, H, T);   // x:22..26 (between labs)
        Box("EC_North_C", 40,    yMid, 32 + T/2, 4, H, T);   // x:38..42 (after Lab B)

        // West cap (x=3) — solid (continues stair wall)
        // East cap (x=42) — gap z:[26..32] for south corridor connection (so no east wall)
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 4 — LAB A  (LOWER, y=-5)
    //  x:[10..22]  z:[32..46]   (12 × 14)   — NORTH branch off east corridor
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildLabA()
    {
        float yMid = Y_DOWN + H/2;
        Box("LabA_Floor",   16, Y_DOWN - T/2,    39, 12, T, 14, true);
        Box("LabA_Ceiling", 16, Y_DOWN + H + T/2,39, 12, T, 14);
        Box("LabA_North",   16, yMid, 46 + T/2,  12+2*T, H, T);
        Box("LabA_West",    10 - T/2, yMid, 39,  T, H, 14);
        Box("LabA_East",    22 + T/2, yMid, 39,  T, H, 14);
        // South wall (z=32) — full gap (open to east corridor) — no wall
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 5 — LAB B  (LOWER, y=-5)
    //  x:[26..38]  z:[32..46]   (12 × 14)   — NORTH branch, further east
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildLabB()
    {
        float yMid = Y_DOWN + H/2;
        Box("LabB_Floor",   32, Y_DOWN - T/2,    39, 12, T, 14, true);
        Box("LabB_Ceiling", 32, Y_DOWN + H + T/2,39, 12, T, 14);
        Box("LabB_North",   32, yMid, 46 + T/2,  12+2*T, H, T);
        Box("LabB_West",    26 - T/2, yMid, 39,  T, H, 14);
        Box("LabB_East",    38 + T/2, yMid, 39,  T, H, 14);
        // South wall (z=32) — open to east corridor
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 6 — SOUTH CORRIDOR  (LOWER, y=-5)
    //  x:[38..42]  z:[26..52]   (4 × 26)   — vertical leg of the L
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildSouthCorridor()
    {
        float yMid = Y_DOWN + H/2;
        // Floor + ceiling cover full z range (incl. overlap with east corridor)
        Box("SC_Floor",   40, Y_DOWN - T/2,    39, 4, T, 26, true);
        Box("SC_Ceiling", 40, Y_DOWN + H + T/2,39, 4, T, 26);

        // Walls (only on outer sides — no walls at z=26 or z=52, those are connections)
        Box("SC_West",    38 - T/2, yMid, 42,  T, H, 20);   // x=38, z:32..52 (below Lab B east wall)
        Box("SC_East",    42 + T/2, yMid, 39,  T, H, 26);   // outer east wall, full length
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 7 — LAB C  (LOWER, y=-5)
    //  x:[26..46]  z:[52..68]   (20 × 16)   — big terminal room at south end
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildLabC()
    {
        float yMid = Y_DOWN + H/2;
        Box("LabC_Floor",   36, Y_DOWN - T/2,    60, 20, T, 16, true);
        Box("LabC_Ceiling", 36, Y_DOWN + H + T/2,60, 20, T, 16);

        // North wall (z=52) — gap x:[38..42] for south corridor entry
        Box("LabC_North_L", 32, yMid, 52 - T/2,  12, H, T);   // x:26..38
        Box("LabC_North_R", 44, yMid, 52 - T/2,  4,  H, T);   // x:42..46

        // South wall (z=68) — gap x:[34..38] for stair up
        Box("LabC_South_L", 30, yMid, 68 + T/2,  8, H, T);    // x:26..34
        Box("LabC_South_R", 42, yMid, 68 + T/2,  8, H, T);    // x:38..46

        Box("LabC_West",    26 - T/2, yMid, 60,  T, H, 16);
        Box("LabC_East",    46 + T/2, yMid, 60,  T, H, 16);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Section 8 — STAIR UP + EXIT ROOM  (returns to UPPER, y=0)
    //  Stair up:  x:[34..38]  z:[68..80]   (slope y=-5 → y=0)
    //  Exit room: x:[30..42]  z:[80..92]   (12 × 12) at y=0
    // ─────────────────────────────────────────────────────────────────────────
    static void BuildStairUpAndExit()
    {
        // Stair up — going +Z direction, ascending → negative X rotation
        SlopedBox("StairUp_Floor",   new Vector3(36, -2.5f, 74),  new Vector3(4, T, STAIR_LEN), -STAIR_ANGLE, true);
        SlopedBox("StairUp_Ceiling", new Vector3(36,  1.5f, 74),  new Vector3(4, T, STAIR_LEN), -STAIR_ANGLE);
        Box("StairUp_WallL",  34 - T/2, -0.5f, 74,  T, 9, 12);
        Box("StairUp_WallR",  38 + T/2, -0.5f, 74,  T, 9, 12);

        // Exit room (upper floor)
        float yMid = Y_UP + H/2;
        Box("Exit_Floor",   36, Y_UP - T/2,    86, 12, T, 12, true);
        Box("Exit_Ceiling", 36, Y_UP + H + T/2,86, 12, T, 12);
        // Back wall (z=80) — gap x:[34..38] for stair
        Box("Exit_Back_L",  32, yMid, 80 - T/2,  4, H, T);   // x:30..34
        Box("Exit_Back_R",  40, yMid, 80 - T/2,  4, H, T);   // x:38..42
        // Other walls — solid (it's a dead-end exit)
        Box("Exit_Front",   36, yMid, 92 + T/2, 12+2*T, H, T);
        Box("Exit_West",    30 - T/2, yMid, 86,  T, H, 12);
        Box("Exit_East",    42 + T/2, yMid, 86,  T, H, 12);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LIGHTING
    // ─────────────────────────────────────────────────────────────────────────
    static void SetupLighting()
    {
        RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.28f);

        var dl = new GameObject("DirLight").AddComponent<Light>();
        dl.transform.SetParent(s_root, false);
        dl.type = LightType.Directional;
        dl.intensity = 0.4f;
        dl.color = new Color(0.95f, 0.92f, 0.82f);
        dl.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Color warm = new Color(1f, 0.92f, 0.75f);
        Color cool = new Color(0.70f, 0.88f, 1.0f);
        Color red  = new Color(1f, 0.30f, 0.20f);

        // SPAWN LOBBY
        PL("PL_Lobby_1", 3f, Y_UP + H - 0.3f, 3f, warm, 12f, 14f);
        PL("PL_Lobby_2", 7f, Y_UP + H - 0.3f, 7f, warm, 12f, 14f);

        // CONNECTOR + STAIR
        PL("PL_Conn",    5f, Y_UP + H - 0.3f, 12f, warm, 10f, 12f);
        PL("PL_Stair_D", 5f, -0.5f,           20f, warm, 13f, 16f);

        // EAST CORRIDOR — 4 lights along its 36m length
        PL("PL_EC_1",  9f, Y_DOWN + H - 0.3f, 29f, warm, 11f, 14f);
        PL("PL_EC_2", 18f, Y_DOWN + H - 0.3f, 29f, warm, 11f, 14f);
        PL("PL_EC_3", 28f, Y_DOWN + H - 0.3f, 29f, warm, 11f, 14f);
        PL("PL_EC_4", 38f, Y_DOWN + H - 0.3f, 29f, warm, 11f, 14f);

        // LAB A & B (cool blue)
        PL("PL_LabA_1", 13f, Y_DOWN + H - 0.3f, 36f, cool, 12f, 16f);
        PL("PL_LabA_2", 19f, Y_DOWN + H - 0.3f, 42f, cool, 12f, 16f);
        PL("PL_LabB_1", 29f, Y_DOWN + H - 0.3f, 36f, cool, 12f, 16f);
        PL("PL_LabB_2", 35f, Y_DOWN + H - 0.3f, 42f, cool, 12f, 16f);

        // SOUTH CORRIDOR
        PL("PL_SC_1", 40f, Y_DOWN + H - 0.3f, 36f, warm, 10f, 12f);
        PL("PL_SC_2", 40f, Y_DOWN + H - 0.3f, 46f, warm, 10f, 12f);

        // LAB C (big — 4 lights cool)
        PL("PL_LabC_1", 30f, Y_DOWN + H - 0.3f, 56f, cool, 13f, 17f);
        PL("PL_LabC_2", 42f, Y_DOWN + H - 0.3f, 56f, cool, 13f, 17f);
        PL("PL_LabC_3", 30f, Y_DOWN + H - 0.3f, 64f, cool, 13f, 17f);
        PL("PL_LabC_4", 42f, Y_DOWN + H - 0.3f, 64f, cool, 13f, 17f);

        // STAIR UP
        PL("PL_Stair_U", 36f, -0.5f, 74f, warm, 13f, 16f);

        // EXIT (red emergency)
        PL("PL_Exit", 36f, Y_UP + H - 0.3f, 86f, red, 11f, 14f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ESSENTIALS — Player + GameManager + ServerTerminals
    // ─────────────────────────────────────────────────────────────────────────
    static void PlaceEssentials()
    {
        // GameManager (cursor lock + game state)
        var gmPfb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/GameManager.prefab");
        if (gmPfb != null)
        {
            var gm = (GameObject)PrefabUtility.InstantiatePrefab(gmPfb);
            if (gm != null) gm.name = "GameManager";
        }
        else Debug.LogWarning("[Level3Builder] GameManager.prefab not found.");

        // Player — spawns in the spawn lobby
        var playerPfb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/Player.prefab");
        if (playerPfb != null)
        {
            var p = (GameObject)PrefabUtility.InstantiatePrefab(playerPfb);
            if (p != null) p.transform.position = new Vector3(5f, Y_UP + 1f, 2f);
        }
        else
        {
            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(s_root, false);
            spawn.transform.position = new Vector3(5f, Y_UP + 1f, 2f);
        }

        // Server terminal stubs — one in each lab
        var srvRoot = new GameObject("ServerTerminals");
        srvRoot.transform.SetParent(s_root, false);

        Vector3[] srvPos =
        {
            new Vector3(16f, Y_DOWN + 0.7f, 44f),  // Lab A — back center
            new Vector3(32f, Y_DOWN + 0.7f, 44f),  // Lab B — back center
            new Vector3(36f, Y_DOWN + 0.7f, 66f),  // Lab C — back center
        };
        for (int i = 0; i < srvPos.Length; i++)
        {
            var srv = GameObject.CreatePrimitive(PrimitiveType.Cube);
            srv.name = $"ServerTerminal_{i + 1}";
            srv.transform.SetParent(srvRoot.transform, false);
            srv.transform.position = srvPos[i];
            srv.transform.localScale = new Vector3(0.8f, 1.4f, 0.8f);
            if (s_server != null) srv.GetComponent<Renderer>().sharedMaterial = s_server;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    static void Box(string name, float cx, float cy, float cz,
                    float sx, float sy, float sz, bool isFloor = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(s_root, false);
        go.transform.localPosition = new Vector3(cx, cy, cz);
        go.transform.localScale    = new Vector3(sx, sy, sz);
        go.GetComponent<Renderer>().sharedMaterial = isFloor ? s_floor : s_wall;
    }

    static void SlopedBox(string name, Vector3 pos, Vector3 size, float angleDeg, bool isFloor = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(s_root, false);
        go.transform.localPosition    = pos;
        go.transform.localEulerAngles = new Vector3(angleDeg, 0, 0);
        go.transform.localScale       = size;
        go.GetComponent<Renderer>().sharedMaterial = isFloor ? s_floor : s_wall;
    }

    static void PL(string name, float x, float y, float z,
                   Color col, float intensity, float range)
    {
        var l = new GameObject(name).AddComponent<Light>();
        l.transform.SetParent(s_root, false);
        l.transform.localPosition = new Vector3(x, y, z);
        l.type      = LightType.Point;
        l.color     = col;
        l.intensity = intensity;
        l.range     = range;
    }

    static Material GetOrCreateMat(string name, Color32 color, bool emissive = false)
    {
        string path = $"Assets/_Project/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        Color c = color;
        if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color"))      mat.SetColor("_Color",     c);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0f);

        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", c * 1.5f);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string parent, string folder)
    {
        string full = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
