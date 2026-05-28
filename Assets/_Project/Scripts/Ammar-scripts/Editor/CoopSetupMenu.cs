using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Co-op Setup Menu
//
//  One menu item: Facility Breach → Set Up Co-op in Current Scene
//
//  Adds:
//    • CoopBootstrap GameObject (the runtime orchestrator)
//    • Player2Spawn empty positioned 2.5m to the right of the existing Player
//
//  Run this in the scene you want to play co-op in (e.g. Level-3.unity).
//  Then press Play — both players spawn, screen splits horizontally.
//
//  Wipes the co-op setup with a separate "Remove Co-op Setup" menu item.
// ─────────────────────────────────────────────────────────────────────────────
public static class CoopSetupMenu
{
    [MenuItem("Facility Breach/Set Up Co-op in Current Scene")]
    public static void SetUp()
    {
        var existing = GameObject.Find("CoopBootstrap");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Co-op already set up",
                "A CoopBootstrap GameObject already exists in this scene. " +
                "Replace it?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("No Player found",
                "This scene has no Player-tagged GameObject. " +
                "Open a level scene with a Player first.", "OK");
            return;
        }

        // Player2 spawn — find or create
        var p2spawn = GameObject.Find("Player2Spawn");
        if (p2spawn == null)
        {
            p2spawn = new GameObject("Player2Spawn");
            Undo.RegisterCreatedObjectUndo(p2spawn, "Create Player2Spawn");
            p2spawn.transform.position = player.transform.position + new Vector3(2.5f, 0, 0);
            p2spawn.transform.rotation = player.transform.rotation;
        }

        // Bootstrap GameObject
        var bootGO = new GameObject("CoopBootstrap");
        Undo.RegisterCreatedObjectUndo(bootGO, "Create CoopBootstrap");
        var boot = bootGO.AddComponent<CoopBootstrap>();
        boot.player2Spawn = p2spawn.transform;
        boot.player1OnTop = true;
        boot.debugLog     = true;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = bootGO;
        EditorUtility.DisplayDialog("Co-op set up",
            "Done!\n\n" +
            "P1 = Keyboard + Mouse (top half of screen)\n" +
            "P2 = Gamepad (bottom half)\n\n" +
            "Plug in a gamepad before pressing Play.\n" +
            "AutoSave will be auto-paused during co-op runs.",
            "OK");
        Debug.Log("[CoopSetupMenu] ✓ CoopBootstrap added. Player2Spawn at " + p2spawn.transform.position);
    }

    [MenuItem("Facility Breach/Remove Co-op Setup")]
    public static void Remove()
    {
        var boot = GameObject.Find("CoopBootstrap");
        var spawn = GameObject.Find("Player2Spawn");
        var retargeter = GameObject.Find("CoopEnemyRetargeter");

        int count = 0;
        if (boot != null) { Undo.DestroyObjectImmediate(boot); count++; }
        if (spawn != null) { Undo.DestroyObjectImmediate(spawn); count++; }
        if (retargeter != null) { Undo.DestroyObjectImmediate(retargeter); count++; }

        // Make sure AutoSave is unpaused
        PlayerPrefs.SetInt("FB_AutoSave_Paused", 0);
        PlayerPrefs.Save();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[CoopSetupMenu] Removed {count} co-op GameObject(s). AutoSave un-paused.");
    }
}
