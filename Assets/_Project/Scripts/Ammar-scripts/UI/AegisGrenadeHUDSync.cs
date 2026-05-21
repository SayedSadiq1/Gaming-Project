using System.Reflection;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Aegis Grenade → HUD Sync
//  Reads the private `grenadeCount` field on Aegis77's GrenadeSystem (via
//  reflection — no Aegis namespace import) and pushes the value into the
//  CombatHUD frag counter every frame.
//
//  USAGE:
//    1. Remove the ThrowableManager component from your Player (it duplicates
//       Aegis's throw logic and causes the "two grenades" bug).
//    2. Add this AegisGrenadeHUDSync component to the same GameObject that has
//       the Aegis GrenadeSystem (or anywhere — it auto-searches the scene).
// ─────────────────────────────────────────────────────────────────────────────
public class AegisGrenadeHUDSync : MonoBehaviour
{
    public CombatHUD hud;
    public bool verboseLogs = false;

    Component _grenadeSystem;
    FieldInfo _grenadeCountField;
    int _lastCount = int.MinValue;

    void Start()
    {
        if (hud == null) hud = Object.FindFirstObjectByType<CombatHUD>();
        FindGrenadeSystem();
    }

    void FindGrenadeSystem()
    {
        var all = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c == null) continue;
            if (c.GetType().Name != "GrenadeSystem") continue;
            _grenadeSystem = c;
            // grenadeCount is a private serialized field in Aegis's script
            _grenadeCountField = c.GetType().GetField("grenadeCount",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (verboseLogs)
                Debug.Log("[AegisGrenadeHUDSync] Found GrenadeSystem on " + c.gameObject.name +
                          " (field found = " + (_grenadeCountField != null) + ")");
            return;
        }
        Debug.LogWarning("[AegisGrenadeHUDSync] No Aegis GrenadeSystem found in scene.");
    }

    void Update()
    {
        if (hud == null || _grenadeSystem == null || _grenadeCountField == null) return;
        int count = (int)_grenadeCountField.GetValue(_grenadeSystem);
        if (count != _lastCount)
        {
            _lastCount = count;
            hud.SetFragCount(count);
            if (verboseLogs) Debug.Log("[AegisGrenadeHUDSync] Synced HUD frag count = " + count);
        }
    }
}
