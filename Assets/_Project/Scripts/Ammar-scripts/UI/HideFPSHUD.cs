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

    static readonly string[] componentsToDisable =
    {
        "InGameMenuManager", "PlayerHealthBar", "AmmoCounter", "WeaponHUDManager",
        "Compass", "NotificationHUDManager", "ObjectiveHUDManager", "FramerateCounter",
        "FeedbackFlashHUD", "DisplayMessageManager", "CrosshairManager", "StanceHUD",
        "JetpackCounter"
    };

    void Hide()
    {
        // 1) Disable matching GameObjects by name
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

        // 2) Disable specific FPS UI scripts directly — even if their GameObject
        //    is named something we don't catch above. This stops InGameMenuManager
        //    NullRefs from spamming the console.
        var comps = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in comps)
        {
            if (c == null) continue;
            string typeName = c.GetType().Name;
            foreach (var target in componentsToDisable)
            {
                if (typeName == target && c is Behaviour beh)
                {
                    beh.enabled = false;
                    break;
                }
            }
        }
    }
}
