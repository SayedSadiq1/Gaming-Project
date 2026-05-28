using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────────────────────
//  Facility Breach — Keycard Spawner (E-press to pick up)
//
//  Companion to WeaponPickup. Drop this on an empty GameObject ("Keycard
//  Spawner") in the scene, drag the team's keycard prefab into the
//  KeycardVisual field, and:
//   • The visual floats + rotates above the spawner
//   • When the Player is within PickupRange, a "[E] Pick up Blue Keycard"
//     prompt appears centered on screen
//   • Pressing E adds the keycard ID to their ChainDoorKeycardHolder and
//     destroys the spawner
//
//  Same UX as the Weapon Spawner — no walk-on auto-collect.
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
    [Tooltip("Display name shown in the [E] prompt.")]
    public string DisplayName = "Blue Keycard";
    [Tooltip("Distance from the player at which the [E] prompt shows + works.")]
    public float PickupRange = 2.5f;
    public float BobHeight   = 0.15f;
    public float BobSpeed    = 1.5f;
    public float RotateSpeed = 90f;
    public float VisualScale = 1f;

    [Header("On pickup (optional)")]
    public AudioClip  pickupSound;
    public GameObject pickupEffect;

    GameObject _visual;
    Vector3    _visualBasePos;
    bool       _pickedUp;
    Transform  _player;
    bool       _showPrompt;

    static GUIStyle s_PromptStyle;

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

        // Find the Player so we can do proximity check + give the keycard.
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
    }

    void Update()
    {
        if (_pickedUp || _visual == null) return;

        // Bob + rotate the visual
        float bob = Mathf.Sin(Time.time * BobSpeed * Mathf.PI * 2f) * BobHeight;
        _visual.transform.localPosition = _visualBasePos + Vector3.up * bob;
        _visual.transform.Rotate(Vector3.up, RotateSpeed * Time.deltaTime, Space.Self);

        // Re-find the player if it spawned later
        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            _player = p.transform;
        }

        // Proximity check
        float dist = Vector3.Distance(_player.position, transform.position);
        _showPrompt = dist <= PickupRange;

        // E-press to pick up (new Input System)
        var kb = Keyboard.current;
        if (_showPrompt && kb != null && kb.eKey.wasPressedThisFrame)
        {
            TryPickup();
        }
    }

    void TryPickup()
    {
        if (_player == null) return;
        var holder = _player.GetComponent<ChainDoorKeycardHolder>()
                  ?? _player.GetComponentInParent<ChainDoorKeycardHolder>()
                  ?? _player.GetComponentInChildren<ChainDoorKeycardHolder>();
        if (holder == null)
        {
            Debug.LogWarning("[KeycardSpawner] Player has no ChainDoorKeycardHolder — run 'Restore Level 3 Scene Setup' to add it.");
            return;
        }

        _pickedUp = true;
        holder.Collect(KeycardID);

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        Debug.Log($"[KeycardSpawner] Player collected keycard '{KeycardID}' from {name}.");
        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (!_showPrompt || _pickedUp) return;

        if (s_PromptStyle == null)
        {
            s_PromptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            s_PromptStyle.normal.textColor = new Color(0f, 0.78f, 1f, 1f); // cyan to match theme
        }

        string label = $"[E] Pick up {DisplayName}";
        var size = s_PromptStyle.CalcSize(new GUIContent(label));
        float x = (Screen.width  - size.x) * 0.5f;
        float y = Screen.height * 0.62f;

        // shadow
        var prev = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.Label(new Rect(x + 2, y + 2, size.x, size.y), label, s_PromptStyle);
        GUI.color = prev;
        GUI.Label(new Rect(x, y, size.x, size.y), label, s_PromptStyle);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.78f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, PickupRange);
    }
}
