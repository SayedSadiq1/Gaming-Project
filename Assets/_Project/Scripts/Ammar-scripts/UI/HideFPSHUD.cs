using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Hide FPS Microgame HUD
//  Disables the FPS Microgame's built-in HUD (GameHUD + children) at runtime
//  so only the new CombatHUDCanvas is visible. Doesn't touch Assets/FPS/.
//
//  Auto-added to CombatHUDCanvas by the builder. Re-runs every 0.5s for the
//  first 5 seconds to catch any HUD pieces that spawn late.
// ─────────────────────────────────────────────────────────────────────────────
public class HideFPSHUD : MonoBehaviour
{
    [Tooltip("Substring matches to hide.")]
    public string[] hideThese =
    {
        "GameHUD", "FeedbackFlashCanvas", "WeaponHUDManager", "Compass",
        "PlayerHealth", "NotificationsRect", "ObjectivesRect", "DisplayMessageRect",
        "FramerateCounter", "BottomLeftcorner", "PauseMenuInfo",
        "InGameMenu", "HUD"
    };

    [Tooltip("GameObjects whose name contains this are NEVER hidden.")]
    public string[] exclude = { "CombatHUDCanvas", "MainMenu", "PauseMenu" };

    void Start()
    {
        InvokeRepeating(nameof(Hide), 0.1f, 0.5f);
        Invoke(nameof(StopRepeat), 5f);
    }
    void StopRepeat() => CancelInvoke(nameof(Hide));

    void Hide()
    {
        // Disable any matching GameObject in the scene (Canvas or not)
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t == null) continue;
            string n = t.gameObject.name;

            bool excluded = false;
            foreach (var ex in exclude)
                if (!string.IsNullOrEmpty(ex) && n.IndexOf(ex, System.StringComparison.OrdinalIgnoreCase) >= 0)
                { excluded = true; break; }
            if (excluded) continue;

            foreach (var target in hideThese)
            {
                if (string.Equals(n, target, System.StringComparison.OrdinalIgnoreCase))
                {
                    t.gameObject.SetActive(false);
                    break;
                }
            }
        }
    }
}
