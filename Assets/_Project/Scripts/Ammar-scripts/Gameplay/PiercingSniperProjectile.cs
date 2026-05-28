using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Piercing Sniper Projectile
//
//  Drop-in replacement for ProjectileStandard, but:
//  • Damage = 500 (one-shot kill on any standard enemy)
//  • Does NOT self-destruct on first hit — instead it keeps flying and damages
//    every enemy whose collider it passes through, until it either hits a
//    non-Damageable surface (wall/floor) or its MaxLifeTime expires.
//
//  Use on the M107 sniper rifle for the "one shot, multiple stacked kills" feel.
// ─────────────────────────────────────────────────────────────────────────────
namespace Ammar.Facility
{
    public class PiercingSniperProjectile : ProjectileBase
    {
        [Header("General")] public float Radius = 0.04f;
        public Transform Root;
        public Transform Tip;
        public float MaxLifeTime = 5f;
        public GameObject ImpactVfx;
        public float ImpactVfxLifetime = 2f;
        public float ImpactVfxSpawnOffset = 0.1f;
        public AudioClip ImpactSfxClip;
        public LayerMask HittableLayers = -1;

        [Header("Movement")] public float Speed = 300f;
        public float GravityDownAcceleration = 0f;
        public float TrajectoryCorrectionDistance = 5f;
        public bool InheritWeaponVelocity = false;

        [Header("Damage")]
        [Tooltip("500 = guaranteed one-shot on any standard FPS Microgame enemy (100 HP).")]
        public float Damage = 500f;

        ProjectileBase m_ProjectileBase;
        Vector3 m_LastRootPosition;
        Vector3 m_Velocity;
        bool m_HasTrajectoryOverride;
        float m_ShootTime;
        Vector3 m_TrajectoryCorrectionVector;
        Vector3 m_ConsumedTrajectoryCorrectionVector;
        List<Collider> m_IgnoredColliders;

        // colliders we've already damaged on this projectile, so we don't tick the
        // same enemy multiple frames in a row while the sphere cast still overlaps it
        readonly HashSet<Collider> m_AlreadyHit = new HashSet<Collider>();

        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        void OnEnable()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            m_ProjectileBase.OnShoot += OnShootHandler;
            Destroy(gameObject, MaxLifeTime);
        }

        void OnShootHandler()
        {
            m_ShootTime = Time.time;
            m_LastRootPosition = Root != null ? Root.position : transform.position;
            m_Velocity = transform.forward * Speed;
            m_IgnoredColliders = new List<Collider>();
            transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Ignore owner's colliders
            if (m_ProjectileBase.Owner != null)
            {
                var ownerColliders = m_ProjectileBase.Owner.GetComponentsInChildren<Collider>();
                m_IgnoredColliders.AddRange(ownerColliders);
            }

            // Trajectory correction (so center-of-screen aim works even though the
            // muzzle is offset from the camera).
            var pwm = m_ProjectileBase.Owner ? m_ProjectileBase.Owner.GetComponent<PlayerWeaponsManager>() : null;
            if (pwm != null)
            {
                m_HasTrajectoryOverride = true;
                Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition - pwm.WeaponCamera.transform.position);
                m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle,
                    pwm.WeaponCamera.transform.forward);

                if (TrajectoryCorrectionDistance == 0)
                {
                    transform.position += m_TrajectoryCorrectionVector;
                    m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
                }
                else if (TrajectoryCorrectionDistance < 0)
                {
                    m_HasTrajectoryOverride = false;
                }
            }
        }

        void Update()
        {
            // Movement
            transform.position += m_Velocity * Time.deltaTime;
            if (InheritWeaponVelocity)
                transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Trajectory drift
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude <
                m_TrajectoryCorrectionVector.sqrMagnitude && Root != null)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                Vector3 correctionThisFrame =
                    (distanceThisFrame / TrajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;
                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                    m_HasTrajectoryOverride = false;
                transform.position += correctionThisFrame;
            }

            transform.forward = m_Velocity.normalized;

            if (GravityDownAcceleration > 0)
                m_Velocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;

            // Hit detection — sphere cast across this frame's travel distance
            if (Root != null && Tip != null)
            {
                Vector3 displacement = Tip.position - m_LastRootPosition;
                float dist = displacement.magnitude;
                if (dist > 0f)
                {
                    var hits = Physics.SphereCastAll(m_LastRootPosition, Radius,
                        displacement.normalized, dist, HittableLayers, k_TriggerInteraction);

                    // sort by distance so we damage in pierce order
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                    foreach (var hit in hits)
                    {
                        if (!IsHitValid(hit)) continue;
                        if (m_AlreadyHit.Contains(hit.collider)) continue;

                        var damageable = hit.collider.GetComponent<Damageable>();
                        if (damageable != null)
                        {
                            // Enemy — pierce through
                            damageable.InflictDamage(Damage, false, m_ProjectileBase.Owner);
                            SpawnImpact(hit.point, hit.normal);
                            m_AlreadyHit.Add(hit.collider);
                            m_IgnoredColliders.Add(hit.collider);
                            // do NOT destroy — continue checking further hits
                        }
                        else
                        {
                            // Wall/world surface — stop here
                            SpawnImpact(hit.point, hit.normal);
                            Destroy(gameObject);
                            return;
                        }
                    }
                }
            }

            m_LastRootPosition = Root != null ? Root.position : transform.position;
        }

        bool IsHitValid(RaycastHit hit)
        {
            if (hit.collider.GetComponent<IgnoreHitDetection>()) return false;
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null) return false;
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider)) return false;
            return true;
        }

        void SpawnImpact(Vector3 point, Vector3 normal)
        {
            if (ImpactVfx != null)
            {
                var go = Instantiate(ImpactVfx, point + normal * ImpactVfxSpawnOffset,
                    Quaternion.LookRotation(normal));
                if (ImpactVfxLifetime > 0) Destroy(go, ImpactVfxLifetime);
            }
            if (ImpactSfxClip != null)
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);
        }
    }
}
