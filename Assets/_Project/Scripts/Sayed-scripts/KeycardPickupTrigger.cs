using System;
using UnityEngine;

// Add this to the keycard GameObject in the scene.
// A SphereCollider trigger is added automatically — the existing MeshCollider is left alone.
// When the player walks into the trigger sphere, the keycard is collected and the object is destroyed.
public class KeycardPickupTrigger : MonoBehaviour
{
    [Tooltip("Must match the keycardID on the ChainDoorController this keycard unlocks.")]
    public string keycardID = "door_1";

    // Fired when the keycard is picked up — used by SecurityLockdown.
    public event Action OnPickedUp;

    [Header("Pickup trigger radius")]
    public float pickupRadius = 1.2f;

    [Header("Idle animation")]
    public float rotateSpeed = 90f;
    public float bobAmplitude = 0.10f;
    public float bobFrequency = 1.5f;

    public GameObject pickupEffect;
    public AudioClip pickupSound;

    Vector3 _basePos;

    void Awake()
    {
        // Add a dedicated sphere trigger so the MeshCollider is untouched.
        var sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = pickupRadius;
    }

    void Start()
    {
        _basePos = transform.position;
    }

    void Update()
    {
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        float y = _basePos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(_basePos.x, y, _basePos.z);
    }

    void OnTriggerEnter(Collider other)
    {
        var holder = other.GetComponent<ChainDoorKeycardHolder>()
                  ?? other.GetComponentInParent<ChainDoorKeycardHolder>();
        if (holder == null) return;

        holder.Collect(keycardID);
        OnPickedUp?.Invoke();

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
