using UnityEditor;
using UnityEngine;
using Ammar.Facility;
using Unity.FPS.Game;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Sniper Piercing Setup  (idempotent / re-runnable repair)
//  Menu: Facility Breach → Set Up Piercing Sniper
//
//  Makes the M107 sniper a one-shot piercing weapon. Safe to re-run any time.
//  Steps:
//   1. Open Projectile_Sniper.prefab in isolation (so we can edit it cleanly).
//   2. Strip any "missing script" entries left by previous attempts.
//   3. Copy field values from ProjectileStandard (if still present) onto a new
//      PiercingSniperProjectile, then remove ProjectileStandard.
//   4. Save the projectile prefab.
//   5. Open M107.prefab, point its ProjectilePrefab field at the freshly-saved
//      PiercingSniperProjectile, fix ClipSize/MaxAmmo per GDD.
// ─────────────────────────────────────────────────────────────────────────────
public static class SniperPiercingSetup
{
    const string PROJECTILE_PATH = "Assets/_Project/Weapons/Projectile_Sniper.prefab";
    const string WEAPON_PATH     = "Assets/_Project/Weapons/M107.prefab";

    [MenuItem("Facility Breach/Set Up Piercing Sniper")]
    public static void SetUp()
    {
        // ── 1. Patch the projectile prefab ───────────────────────────────────
        var projRoot = PrefabUtility.LoadPrefabContents(PROJECTILE_PATH);
        if (projRoot == null)
        {
            EditorUtility.DisplayDialog("Sniper setup failed",
                "Couldn't load " + PROJECTILE_PATH, "OK");
            return;
        }

        // Remove "missing script" components left from previous runs
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(projRoot);
        if (removed > 0)
            Debug.Log($"[SniperPiercingSetup] Stripped {removed} missing-script components from Projectile_Sniper.");

        // Find existing ProjectileStandard (if any) so we can copy its settings
        var existing = projRoot.GetComponent<Unity.FPS.Gameplay.ProjectileStandard>();

        // Ensure a PiercingSniperProjectile exists
        var pierce = projRoot.GetComponent<PiercingSniperProjectile>();
        if (pierce == null) pierce = projRoot.AddComponent<PiercingSniperProjectile>();

        if (existing != null)
        {
            pierce.Radius                       = existing.Radius;
            pierce.Root                         = existing.Root;
            pierce.Tip                          = existing.Tip;
            pierce.MaxLifeTime                  = existing.MaxLifeTime;
            pierce.ImpactVfx                    = existing.ImpactVfx;
            pierce.ImpactVfxLifetime            = existing.ImpactVfxLifetime;
            pierce.ImpactVfxSpawnOffset         = existing.ImpactVfxSpawnOffset;
            pierce.ImpactSfxClip                = existing.ImpactSfxClip;
            pierce.HittableLayers               = existing.HittableLayers;
            pierce.Speed                        = Mathf.Max(existing.Speed, 500f);
            pierce.GravityDownAcceleration      = existing.GravityDownAcceleration;
            pierce.TrajectoryCorrectionDistance = existing.TrajectoryCorrectionDistance;
            pierce.InheritWeaponVelocity        = existing.InheritWeaponVelocity;

            Object.DestroyImmediate(existing, true);
        }
        else
        {
            // Defaults — pick reasonable values if no template existed
            if (pierce.Root == null) pierce.Root = projRoot.transform;
            if (pierce.Tip  == null && projRoot.transform.childCount > 0)
                pierce.Tip = projRoot.transform.GetChild(0);
            if (pierce.MaxLifeTime <= 0f) pierce.MaxLifeTime = 5f;
            if (pierce.Speed       <= 0f) pierce.Speed       = 500f;
            if (pierce.Radius      <= 0f) pierce.Radius      = 0.12f;
            if ((int)pierce.HittableLayers == 0) pierce.HittableLayers = -1;
        }

        pierce.Damage = 500f; // guaranteed one-shot

        EditorUtility.SetDirty(projRoot);
        PrefabUtility.SaveAsPrefabAsset(projRoot, PROJECTILE_PATH);
        PrefabUtility.UnloadPrefabContents(projRoot);
        AssetDatabase.ImportAsset(PROJECTILE_PATH, ImportAssetOptions.ForceUpdate);
        Debug.Log("[SniperPiercingSetup] Projectile_Sniper saved with PiercingSniperProjectile (Damage=500).");

        // ── 2. Re-link M107's ProjectilePrefab + ammo ────────────────────────
        var weaponRoot = PrefabUtility.LoadPrefabContents(WEAPON_PATH);
        if (weaponRoot == null)
        {
            EditorUtility.DisplayDialog("M107 not found",
                "Couldn't load " + WEAPON_PATH +
                "\n\nProjectile prefab IS fixed; you'll need to drag Projectile_Sniper into the M107's " +
                "WeaponController → ProjectilePrefab field manually.", "OK");
            return;
        }

        var wc = weaponRoot.GetComponent<WeaponController>();
        if (wc == null)
        {
            PrefabUtility.UnloadPrefabContents(weaponRoot);
            EditorUtility.DisplayDialog("M107 missing WeaponController",
                "No WeaponController found on M107 prefab.", "OK");
            return;
        }

        // Load the now-piercing projectile and pull its ProjectileBase reference
        var newProjectileAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PATH);
        var newProjectileBase  = newProjectileAsset != null
            ? newProjectileAsset.GetComponent<ProjectileBase>()
            : null;

        if (newProjectileBase == null)
        {
            PrefabUtility.UnloadPrefabContents(weaponRoot);
            EditorUtility.DisplayDialog("Setup failed",
                "PiercingSniperProjectile component not found on Projectile_Sniper after save. " +
                "Try running the menu once more.", "OK");
            return;
        }

        wc.ProjectilePrefab = newProjectileBase;
        wc.ClipSize         = 5;
        wc.MaxAmmo          = 5;

        EditorUtility.SetDirty(weaponRoot);
        PrefabUtility.SaveAsPrefabAsset(weaponRoot, WEAPON_PATH);
        PrefabUtility.UnloadPrefabContents(weaponRoot);
        AssetDatabase.ImportAsset(WEAPON_PATH, ImportAssetOptions.ForceUpdate);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Piercing sniper ready",
            "Projectile_Sniper → PiercingSniperProjectile (Damage=500, pierces enemies).\n" +
            "M107 ProjectilePrefab re-linked, ClipSize=5, MaxAmmo=5.\n\n" +
            "Reopen Level 3 and the sniper will fire one-shot piercing rounds.", "OK");
    }
}
