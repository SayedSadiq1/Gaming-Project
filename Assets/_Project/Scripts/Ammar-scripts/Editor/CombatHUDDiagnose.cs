// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Combat HUD Diagnostic
//  Top menu → Facility Breach → Diagnose Combat HUD
//
//  Walks through every requirement the HUD needs to update health, ammo, and
//  weapon swap. Prints PASS / FAIL for each step in the Console so any
//  teammate can see exactly what's wrong in their scene.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEditor;

public static class CombatHUDDiagnose
{
    [MenuItem("Facility Breach/Diagnose Combat HUD")]
    public static void Run()
    {
        Debug.Log("──────── Combat HUD Diagnostic ────────");

        // 1) CombatHUD canvas
        var hud = Object.FindFirstObjectByType<CombatHUD>();
        if (hud == null)
        {
            Debug.LogError("[✗] No CombatHUD in scene. Run 'Facility Breach → Build Combat HUD' first.");
            return;
        }
        Debug.Log("[✓] CombatHUD found in scene.");

        // 2) Player tag
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[✗] No GameObject has the 'Player' tag.\n" +
                "FIX: Select your Player GameObject in the hierarchy → top-of-inspector " +
                "'Tag' dropdown → choose 'Player'.");
            return;
        }
        Debug.Log("[✓] Player tagged: " + player.name);

        // 3) Health component (any class named Health — usually Unity.FPS.Game.Health)
        Component health = null;
        foreach (var c in player.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "Health") { health = c; break; }
        }
        if (health == null)
        {
            Debug.LogError("[✗] Player has no Health component.\n" +
                "FIX: Add the FPS Microgame's Health component to your Player " +
                "(Inspector → Add Component → 'Health'). Or use the FPS Microgame's " +
                "Player.prefab from Assets/FPS/Prefabs/ as a starting point.");
            return;
        }
        Debug.Log("[✓] Health component: " + health.GetType().FullName);

        // 4) HUDBridge attached
        var bridge = player.GetComponent<HUDBridge>();
        if (bridge == null)
        {
            Debug.LogError("[✗] Player has no HUDBridge. Re-run 'Facility Breach → Build Combat HUD'.");
            return;
        }
        Debug.Log("[✓] HUDBridge attached to Player.");

        // 5) HUDBridge hud reference
        if (bridge.hud == null)
        {
            Debug.LogWarning("[!] HUDBridge.hud is null — will auto-lookup at runtime, but cleaner to re-run Setup.");
        }
        else
        {
            Debug.Log("[✓] HUDBridge.hud is wired to CombatHUD.");
        }

        // 6) PlayerWeaponsManager (for weapon HUD)
        Component pwm = null;
        foreach (var c in player.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "PlayerWeaponsManager") { pwm = c; break; }
        }
        if (pwm == null)
            Debug.LogWarning("[!] No PlayerWeaponsManager on Player — ammo/weapon parts of the HUD won't update.");
        else
            Debug.Log("[✓] PlayerWeaponsManager found.");

        Debug.Log("──────── DIAGNOSTIC COMPLETE ────────");
        Debug.Log("If everything is [✓], the HUD should update on damage. " +
                  "If health still doesn't drop in Play mode, the enemy isn't calling Health.TakeDamage() — " +
                  "that means the enemy script doesn't use the FPS Microgame's damage system.");
    }
}
