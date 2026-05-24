using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Lose Scene Redirect
//
//  The FPS Microgame's GameFlowManager loads "LoseScene" on player death.
//  Instead of editing the FPS prefab (no FPS edits rule), we patch the
//  GameFlowManager component's `LoseSceneName` field at runtime so it loads
//  OUR DeathScreen instead.
//
//  Runs after every scene load. Also includes a safety net: if "LoseScene"
//  ever does load (e.g. someone called LoadScene directly), redirect to
//  DeathScreen immediately.
// ─────────────────────────────────────────────────────────────────────────────
public static class LoseSceneRedirect
{
    const string OUR_DEATH_SCENE = "DeathScreen";
    const string FPS_LOSE_SCENE  = "LoseScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Safety net — if the old LoseScene loaded anyway, jump to ours.
        if (scene.name == FPS_LOSE_SCENE)
        {
            if (Application.CanStreamedLevelBeLoaded(OUR_DEATH_SCENE))
            {
                Debug.Log("[LoseSceneRedirect] LoseScene loaded — redirecting to DeathScreen.");
                SceneManager.LoadScene(OUR_DEATH_SCENE);
            }
            return;
        }

        // Skip menus / our own death scene
        if (scene.name == OUR_DEATH_SCENE || scene.name == "MainMenu") return;

        // Patch the GameFlowManager in this gameplay scene so future deaths
        // load our DeathScreen instead of LoseScene.
        var gfmType = System.Type.GetType("Unity.FPS.Game.GameFlowManager, Assembly-CSharp");
        if (gfmType == null) return;

        var gfms = Object.FindObjectsByType(gfmType, FindObjectsSortMode.None);
        if (gfms == null || gfms.Length == 0) return;

        var field = gfmType.GetField("LoseSceneName");
        if (field == null) return;

        int patched = 0;
        foreach (var gfm in gfms)
        {
            try
            {
                string current = field.GetValue(gfm) as string;
                if (current != OUR_DEATH_SCENE)
                {
                    field.SetValue(gfm, OUR_DEATH_SCENE);
                    patched++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LoseSceneRedirect] Couldn't patch GameFlowManager: {e.Message}");
            }
        }

        if (patched > 0)
            Debug.Log($"[LoseSceneRedirect] Patched {patched} GameFlowManager(s) → LoseSceneName = '{OUR_DEATH_SCENE}'.");
    }
}
