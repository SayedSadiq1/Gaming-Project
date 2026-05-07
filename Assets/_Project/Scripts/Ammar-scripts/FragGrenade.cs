using UnityEngine;

public class FragGrenade : ThrowableBase
{
    [Header("Frag Settings")]
    public float DamageRadius = 8f;
    public float DamageAmount = 100f;
    public float ExplosionForce = 700f;

    public override void Explode()
    {
        // 1. Damage and Physics Force
        Collider[] colliders = Physics.OverlapSphere(transform.position, DamageRadius);
        foreach (Collider hit in colliders)
        {
            // Damage
            var health = hit.GetComponentInParent<Unity.FPS.Game.Health>();
            if (health != null)
            {
                health.TakeDamage(DamageAmount, gameObject);
            }

            // Physics Force
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(ExplosionForce, transform.position, DamageRadius);
            }
        }

        base.Explode();
    }
}
