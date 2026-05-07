using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.FPS.Game;

namespace Aegis.GrenadeSystem.HiEx
{
    public class HiExGrenade : MonoBehaviour
    {
        // Explosion effects
        [Header("Explosion Effects")]
        [SerializeField] GameObject explosionEffectPrefab;
        [SerializeField] Vector3 explosionParticleOffset = new Vector3(0, 1, 0);


        //explosion settings
        [Header("Explosion Settings")]
        [SerializeField] float explosionDelay = 3f;
        [SerializeField] float explosionForce = 1000f;
        [SerializeField] float explosionForceRadius = 5f;

        // Damage settings
        [Header("Damage Settings")]
        [SerializeField] float closeRadius = 1f;
        [SerializeField] float nearRadius = 5f;
        [SerializeField] float farRadius = 7f;

        [SerializeField] float closeDam = 100f; // Adjusted for project health scales
        [SerializeField] float nearDam = 50f;
        [SerializeField] float farDam = 10f;


        // Audio effects
        [Header("Audio Effects")]
        [SerializeField] GameObject audioSourcePrefab;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip impact;
        [SerializeField] AudioClip[] explosionSounds;

        // internal variables
        float countdown;
        bool hasexploded = false;

        private void Awake()
        {
            audioSource = gameObject.GetComponent<AudioSource>();
        }


        private void Start()
        {
            // set the timer
            countdown = explosionDelay;
        }

        private void Update()
        {
            // if the grenade hasn't exploded, reduce the timer
            if (!hasexploded)
            {
                countdown -= Time.deltaTime;
                if (countdown <= 0)
                {
                    Explode();
                    hasexploded = true;
                }
            }


        }


        //explode function - what happens when the timer reaches 0
        void Explode()
        {

            // instantiate explosion effect at this game object
            GameObject explosionEffect = Instantiate(explosionEffectPrefab, transform.position + explosionParticleOffset, Quaternion.identity);

            Destroy(explosionEffect, 1.9f);

            PlaySoundAtPosition();

            ApplyExplosiveForce();

            ApplyDamage();

            Destroy(gameObject);
        }


        //Function to apply damage to the player or to enemies
        void ApplyDamage()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, farRadius);
            HashSet<Health> damagedObjects = new HashSet<Health>();

            foreach (var hit in hits)
            {
                Health health = hit.GetComponentInParent<Health>();
                if (health != null && !damagedObjects.Contains(health))
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    float damage = 0;

                    if (dist <= closeRadius) damage = closeDam;
                    else if (dist <= nearRadius) damage = nearDam;
                    else damage = farDam;

                    health.TakeDamage(damage, gameObject);
                    damagedObjects.Add(health);
                    Debug.Log(health.gameObject.name + " took " + damage + " damage from Aegis grenade.");
                }
            }
        }


        //Function to apply physics explosive force to objects near the explosion
        void ApplyExplosiveForce()
        {
            //Create a list of all colliders of objects within the radius of the explosion force
            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionForceRadius);

            //for every collider collected, apply an explosive force originating from the position of the explosion
            foreach (Collider nearbyobject in colliders)
            {
                Rigidbody rb = nearbyobject.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    rb.AddExplosionForce(explosionForce, transform.position, explosionForceRadius);
                }
            }
        }

        //Function to play explosion sound effect by instantiating a new object to play that sound at the explosion
        void PlaySoundAtPosition()
        {
            if (audioSourcePrefab == null) return;
            GameObject audiosourceObject = Instantiate(audioSourcePrefab, transform.position, Quaternion.identity);

            if (explosionSounds != null && explosionSounds.Length > 0)
            {
                int rand = Random.Range(0, explosionSounds.Length);
                AudioSource instantiatedAudioSource = audiosourceObject.GetComponent<AudioSource>();
                instantiatedAudioSource.spatialBlend = 1;
                instantiatedAudioSource.clip = explosionSounds[rand];
                instantiatedAudioSource.Play();
                Destroy(audiosourceObject, instantiatedAudioSource.clip.length);
            }
        }

        //Function to play an impact sound effect if the thrown grenade hits something, but has not exploded yet
        private void OnCollisionEnter(Collision collision)
        {
            if (impact != null && audioSource != null)
            {
                audioSource.clip = impact;
                audioSource.spatialBlend = 1;
                audioSource.Play();
            }
        }
    }
}
