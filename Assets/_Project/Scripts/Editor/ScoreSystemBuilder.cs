// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Score System Builder
//  Usage: top menu → Facility Breach → Build Score System
//
//  Adds a ScoreManager to the current scene. The ScoreManager builds its
//  own HUD at runtime — no manual UI setup needed.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ScoreSystemBuilder
{
    [MenuItem("Facility Breach/Build Score System")]
    public static void Build()
    {
        // Remove old one if it exists
        var existing = GameObject.Find("ScoreManager");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
            Debug.Log("[ScoreSystemBuilder] Removed old ScoreManager.");
        }

        // Create ScoreManager
        var go = new GameObject("ScoreManager");
        var sm = go.AddComponent<ScoreManager>();
        sm.pointsPerKill = 100;

        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("[ScoreSystemBuilder] ✓ Score System added. Save your scene!");
    }

    [MenuItem("Facility Breach/Reset Score (PlayerPrefs)")]
    public static void ResetScore()
    {
        PlayerPrefs.DeleteKey("FB_Score");
        PlayerPrefs.DeleteKey("FB_Kills");
        PlayerPrefs.Save();
        Debug.Log("[ScoreSystemBuilder] Score and kills reset to 0.");
    }
}
