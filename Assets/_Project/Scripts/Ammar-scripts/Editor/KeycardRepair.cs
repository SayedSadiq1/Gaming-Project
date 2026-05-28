using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Keycard Repair
//  Menu: Facility Breach → Repair Keycard Visibility
//
//  The team keycard was spawned at the wrong height / inside the ceiling /
//  behind a wall. This menu:
//    • Finds the keycard in the scene
//    • Moves it to a clearly visible, player-reachable spot:
//        Player.position + Player.forward * 4m, at floor height + 1m
//    • Resets scale to 3,3,3 (the team prefab default)
//    • Re-enables all renderers + colliders in case anything was disabled
//    • Resets rotation so it sits flat and bobs vertically
// ─────────────────────────────────────────────────────────────────────────────
public static class KeycardRepair
{
    [MenuItem("Facility Breach/Repair Keycard Visibility")]
    public static void Repair()
    {
        var trigger = Object.FindFirstObjectByType<KeycardPickupTrigger>(FindObjectsInactive.Include);
        if (trigger == null)
        {
            EditorUtility.DisplayDialog("Keycard Repair",
                "No KeycardPickupTrigger found in the scene.\n" +
                "Run 'Restore Level 3 Scene Setup' first to spawn the team keycard.", "OK");
            return;
        }

        var keycard = trigger.gameObject;

        // ── Move to a sensible position relative to the Player ────────────────
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 newPos;
        if (player != null)
        {
            // 4m in front of the player, at the player's floor level + 1m up
            newPos = player.transform.position
                   + player.transform.forward * 4f
                   + Vector3.up * 1f;
            // Snap Y to be slightly above where the player can comfortably see it
            newPos.y = player.transform.position.y + 1f;
        }
        else
        {
            newPos = new Vector3(0f, 1f, 0f);
        }

        Undo.RecordObject(keycard.transform, "Repair Keycard");
        Vector3 oldPos = keycard.transform.position;
        keycard.transform.position = newPos;
        keycard.transform.rotation = Quaternion.identity; // flat
        keycard.transform.localScale = new Vector3(3f, 3f, 3f);

        // ── Re-enable any disabled renderers (in case the team prefab had any off) ─
        int enabledRenderers = 0;
        foreach (var r in keycard.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled) { r.enabled = true; enabledRenderers++; }
            if (!r.gameObject.activeSelf) { r.gameObject.SetActive(true); }
        }

        // ── Ensure trigger sphere is reasonable size ──────────────────────────
        trigger.pickupRadius = 1.5f;

        // ── Make sure the GameObject itself is active ─────────────────────────
        keycard.SetActive(true);

        EditorUtility.SetDirty(keycard);
        EditorUtility.SetDirty(trigger);
        EditorSceneManager.MarkSceneDirty(keycard.scene);

        // Select + ping it so the user can see it in the scene view
        Selection.activeGameObject = keycard;
        EditorGUIUtility.PingObject(keycard);
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorUtility.DisplayDialog("Keycard Repaired",
            $"Moved keycard from {oldPos:F2}\n" +
            $"to {newPos:F2}\n\n" +
            $"Re-enabled {enabledRenderers} renderer(s), reset scale to 3, pickup radius 1.5m.\n\n" +
            "It's now selected in the hierarchy + scene view focused on it.\n" +
            "Drag it manually if you want a different spot, then Ctrl+S to save.",
            "OK");

        Debug.Log($"[KeycardRepair] ✓ Keycard moved to {newPos:F2} (from {oldPos:F2}). Save the scene to persist.");
    }
}
