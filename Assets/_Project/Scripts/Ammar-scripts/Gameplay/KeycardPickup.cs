using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Keycard Pickup
//  Drop on a keycard GameObject in the world. Player aims at it + presses F
//  (the standard interact key) → it's added to KeycardInventory and the
//  keycard is destroyed.
//
//  Uses the IInteractable system (same as servers + panels) so behaviour is
//  consistent across the whole level.
// ─────────────────────────────────────────────────────────────────────────────
public class KeycardPickup : MonoBehaviour, IInteractable
{
    [Header("Type")]
    public KeycardColor color = KeycardColor.Blue;

    [Header("Pickup feedback")]
    public AudioClip pickupSound;
    public GameObject pickupVFX;
    [Tooltip("0 = press once, >0 = hold for that many seconds")]
    public float holdDuration = 0f;   // instant press-to-pickup

    [Header("Idle animation")]
    public float rotateSpeed  = 90f;
    public float bobAmplitude = 0.10f;
    public float bobFrequency = 1.5f;

    Vector3 _basePos;
    bool    _picked;

    public string PromptText  => $"PRESS F TO PICK UP {color.ToString().ToUpper()} KEYCARD";
    public float  HoldDuration => holdDuration;

    void Start()
    {
        _basePos = transform.position;
    }

    void Update()
    {
        if (_picked) return;
        // Spin + bob so the keycard looks pickup-able
        transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        float y = _basePos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.position = new Vector3(_basePos.x, y, _basePos.z);
    }

    public bool CanInteract(GameObject interactor, out string reason)
    {
        reason = "";
        return !_picked;
    }

    public void OnInteract(GameObject interactor)
    {
        if (_picked) return;

        var inv = interactor.GetComponent<KeycardInventory>();
        if (inv == null) inv = interactor.GetComponentInParent<KeycardInventory>();
        if (inv == null) { Debug.LogWarning("[KeycardPickup] Player has no KeycardInventory component."); return; }

        inv.AddKeycard(color);
        _picked = true;

        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupVFX   != null) Instantiate(pickupVFX, transform.position, transform.rotation);

        Debug.Log($"[KeycardPickup] {color} keycard picked up by {interactor.name}.");
        Destroy(gameObject);
    }
}
