using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Keycard Spawner (walk-on collectable)
//
//  Companion to WeaponPickup. Drop this on an empty GameObject ("Keycard
//  Spawner") in the scene, drag the team's keycard prefab into the
//  KeycardVisual field, and:
//   • The visual floats + rotates above the spawner
//   • When the Player walks into the pickup radius, the keycard ID is added
//     to their ChainDoorKeycardHolder
//   • The spawner destroys itself after pickup
//
//  No E-press needed — walking into the trigger collects it (same as the
//  team's original KeycardPickupTrigger).
// ─────────────────────────────────────────────────────────────────────────────
public class KeycardSpawner : MonoBehaviour
{
    [Header("What to give the player")]
    [Tooltip("Visual prefab — drag the team's keycard.prefab (or any mesh) here. " +
             "It's spawned as a non-functional child for show.")]
    public GameObject KeycardVisual;

    [Tooltip("ID handed to ChainDoorKeycardHolder.Collect. Must match the door's keycardID.")]
    public string KeycardID = "blue_keycard";

    [Header("Pickup feel")]
    [Tooltip("Radius of the auto-pickup sphere. Player walks within → collects.")]
    public float PickupRadius = 1.5f;
    public float BobHeight    = 0.15f;
    public float BobSpeed     = 1.5f;
    public float RotateSpeed  = 90f;
    public float VisualScale  = 1f;

    [Header("On pickup (optional)")]
    public AudioClip  pickupSound;
    public GameObject pickupEffect;

    GameObject _visual;
    Vector3    _visualBasePos;
    bool       _pickedUp;
    SphereCollider _trigger;

    void Start()
    {
        // ── Build the floating visual ─────────────────────────────────────────
        if (KeycardVisual != null)
        {
            _visual = Instantiate(KeycardVisual, transform);
            _visual.transform.localPosition = Vector3.zero;
            _visual.transform.localRotation = Quaternion.identity;
            _visual.transform.localScale    = Vector3.one * VisualScale;

            // Strip functional scripts/colliders on the visual — the spawner
            // owns the pickup trigger.
            foreach (var b in _visual.GetComponentsInChildren<KeycardPickupTrigger>(true)) b.enabled = false;
            foreach (var b in _visual.GetComponentsInChildren<KeycardSpawner>(true))
                if (b != this) b.enabled = false;
            foreach (var col in _visual.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var rb  in _visual.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity  = false;
            }
            foreach (var a in _visual.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;

            _visualBasePos = _visual.transform.localPosition;
        }
        else
        {
            Debug.LogWarning($"[KeycardSpawner] '{name}' has no Keycard Visual assigned — pickup will still work but nothing will be visible.");
        }

        // ── Add our own pickup trigger sphere ─────────────────────────────────
        _trigger = gameObject.GetComponent<SphereCollider>();
        if (_trigger == null) _trigger = gameObject.AddComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.radius    = PickupRadius;
    }

    void Update()
    {
        if (_pickedUp || _visual == null) return;

        // Bob + rotate the visual only — the spawner GameObject stays still so
        // the trigger sphere doesn't drift.
        float bob = Mathf.Sin(Time.time * BobSpeed * Mathf.PI * 2f) * BobHeight;
        _visual.transform.localPosition = _visualBasePos + Vector3.up * bob;
        _visual.transform.Rotate(Vector3.up, RotateSpeed * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_pickedUp) return;
        var holder = other.GetComponent<ChainDoorKeycardHolder>()
                  ?? other.GetComponentInParent<ChainDoorKeycardHolder>();
        if (holder == null) return;

        _pickedUp = true;
        holder.Collect(KeycardID);

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        Debug.Log($"[KeycardSpawner] Player collected keycard '{KeycardID}' from {name}.");
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.78f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, PickupRadius);
    }
}
