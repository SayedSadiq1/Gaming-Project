using UnityEngine;

public abstract class ThrowableBase : MonoBehaviour
{
    [Header("Settings")]
    public float Delay = 3f;
    public float ThrowForce = 6f; // Reduced for short-medium range
    public GameObject ExplosionEffect;
    
    [Header("Audio")]
    public AudioClip ExplosionSound;

    protected bool HasExploded = false;
    protected float Countdown;
    protected AudioSource AudioSource;

    protected virtual void Awake()
    {
        AudioSource = GetComponent<AudioSource>();
        if (AudioSource == null)
        {
            AudioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    protected virtual void Start()
    {
        Countdown = Delay;
    }

    protected virtual void Update()
    {
        Countdown -= Time.deltaTime;
        if (Countdown <= 0f && !HasExploded)
        {
            Explode();
        }
    }

    public virtual void Throw(Vector3 direction)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(direction * ThrowForce, ForceMode.Impulse);
        }
    }

    public virtual void Explode()
    {
        HasExploded = true;

        if (ExplosionEffect != null)
        {
            Instantiate(ExplosionEffect, transform.position, transform.rotation);
        }

        if (ExplosionSound != null)
        {
            // Play at point to ensure it's heard after object is destroyed
            AudioSource.PlayClipAtPoint(ExplosionSound, transform.position);
        }

        Destroy(gameObject);
    }
}
