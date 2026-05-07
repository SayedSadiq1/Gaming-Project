using UnityEngine;
using Unity.FPS.Game;

[RequireComponent(typeof(WeaponController))]
public class MeleeWeapon : MonoBehaviour
{
    [Header("Melee Settings")]
    public float Damage = 50f;
    public float Range = 2.5f; // Increased slightly
    public float HitSphereRadius = 0.5f; // Use SphereCast for easier hits
    public LayerMask TargetLayers = -1;

    [Header("Melee Audio")]
    public AudioClip SwingSound;
    public AudioClip HitSound;

    private WeaponController m_WeaponController;
    private AudioSource m_AudioSource;

    void Awake()
    {
        m_WeaponController = GetComponent<WeaponController>();
        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null) m_AudioSource = gameObject.AddComponent<AudioSource>();
        
        m_WeaponController.OnShoot += OnAttack;
    }

    void Update()
    {
        // Keep ammo full to simulate no ammo usage
        if (m_WeaponController.GetCurrentAmmo() < m_WeaponController.MaxAmmo)
        {
            m_WeaponController.UseAmmo(-(m_WeaponController.MaxAmmo - m_WeaponController.GetCurrentAmmo()));
        }
    }

    void OnAttack()
    {
        // Play swing sound immediately
        if (SwingSound != null && m_AudioSource != null)
        {
            m_AudioSource.PlayOneShot(SwingSound);
        }

        // Perform melee hit check using SphereCast (more reliable than a single ray)
        RaycastHit hit;
        Transform muzzle = m_WeaponController.WeaponMuzzle;
        if (muzzle == null) muzzle = transform;
        
        // SphereCast from muzzle forward
        if (Physics.SphereCast(muzzle.position, HitSphereRadius, muzzle.forward, out hit, Range, TargetLayers, QueryTriggerInteraction.Ignore))
        {
            // Look for Damageable in parents (e.g. Soldier_demo has Damageable)
            Damageable damageable = hit.collider.GetComponentInParent<Damageable>();
            if (damageable != null)
            {
                damageable.InflictDamage(Damage, false, m_WeaponController.Owner);
                
                // Play hit sound
                if (HitSound != null && m_AudioSource != null)
                {
                    m_AudioSource.PlayOneShot(HitSound);
                }

                Debug.Log("Melee Hit: " + hit.collider.gameObject.name);
            }
        }
    }

    void OnDestroy()
    {
        if (m_WeaponController != null)
        {
            m_WeaponController.OnShoot -= OnAttack;
        }
    }
}
