using UnityEngine;
using Unity.FPS.Game;

// Place this on a weapon prefab that is a child of the enemy.
// Assign the REAL weapon projectile prefab (e.g. Projectile_AK74) — not Enemy_Bullet.
// EnemyGunAI calls Fire(targetPoint) on this.
public class EnemyWeaponMount : MonoBehaviour
{
    [Header("Fire Point")]
    [Tooltip("The muzzle tip of the weapon — drag the MuzzlePoint child here.")]
    public Transform firePoint;

    [Header("Projectile")]
    [Tooltip("Use the real weapon projectile (Projectile_AK74, Projectile_Pistol, etc.).")]
    public GameObject projectilePrefab;

    [Tooltip("Overrides the projectile's own Damage value. Set to 0 to use the prefab default.")]
    public float damageOverride = 0f;

    [Header("Effects")]
    public GameObject muzzleFlashPrefab;
    public float muzzleFlashDuration = 0.08f;

    [Header("Audio")]
    public AudioClip fireSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake  = false;
        _audio.spatialBlend = 1f;
    }

    // Called by EnemyGunAI — targetPoint is the world position to shoot toward.
    public void Fire(Vector3 targetPoint)
    {
        if (firePoint == null || projectilePrefab == null) return;

        Vector3    dir = (targetPoint - firePoint.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir);

        GameObject go = Instantiate(projectilePrefab, firePoint.position, rot);

        // Initialize the real projectile — works with ProjectileStandard, impact VFX, damage, etc.
        ProjectileBase pb = go.GetComponent<ProjectileBase>();
        if (pb != null)
        {
            pb.ShootFromEnemy(transform.root.gameObject);

            // Optional damage override so each weapon can deal different damage.
            if (damageOverride > 0f)
            {
                var ps = go.GetComponent<Unity.FPS.Gameplay.ProjectileStandard>();
                if (ps != null) ps.Damage = damageOverride;

                // The M107 sniper projectile is a PiercingSniperProjectile (Damage = 500
                // for the player's one-shot). When an ENEMY fires it, honor the override
                // here too, otherwise it deals the full 500 and instantly kills the player.
                var piercing = go.GetComponent<Ammar.Facility.PiercingSniperProjectile>();
                if (piercing != null) piercing.Damage = damageOverride;
            }
        }

        // Muzzle flash
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, rot, firePoint);
            Destroy(flash, muzzleFlashDuration);
        }

        // Fire sound
        if (fireSound != null && _audio != null)
            _audio.PlayOneShot(fireSound, volume);
    }
}
