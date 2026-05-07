using UnityEngine;

public class SmokeGrenade : ThrowableBase
{
    [Header("Smoke Settings")]
    public float SmokeDuration = 7f;
    public GameObject SmokePrefab;

    public override void Explode()
    {
        if (SmokePrefab != null)
        {
            GameObject smoke = Instantiate(SmokePrefab, transform.position, Quaternion.identity);
            var ps = smoke.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                ps.Play();
            }
            Destroy(smoke, SmokeDuration + 3f); // Extra time for particles to fade
        }

        base.Explode();
    }
}
